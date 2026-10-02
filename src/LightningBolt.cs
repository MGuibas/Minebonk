using System;
using System.Collections.Generic;
using UnityEngine;

namespace MegabonkSteve
{
    // A lightning bolt generated with vanilla's own algorithm (LightningBoltRenderer): an 8-segment random walk,
    // four stacked layers of increasing width and the same 0.45/0.45/0.5 colour at 0.3 alpha, drawn additively.
    // It lives like vanilla's: a few ticks, then 2-4 short re-rolled flashes separated by random gaps.
    internal class LightningBolt
    {
        private const float Block = 2.5f; // world units per Minecraft block

        private static Material mat;
        private readonly GameObject go;
        private readonly MeshFilter mf;
        private readonly Vector3 origin;
        private float t;
        private int flashesLeft;
        private float visibleUntil, nextFlashAt;
        private bool visible;
        private bool done;
        private readonly System.Random rnd = new System.Random();

        public bool Done { get { return done; } }

        public LightningBolt(Vector3 groundPos)
        {
            origin = groundPos;
            go = new GameObject("lightning");
            go.transform.position = groundPos;
            mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GetMat();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flashesLeft = 0;           // one strike only: no re-flashes
            Show(0.22f);
        }

        // Additive, depth-tested vertex-coloured material; falls back to the UI shader if the debug one is stripped.
        private static Material GetMat()
        {
            if (mat != null) return mat;
            Shader sh = Shader.Find("Hidden/Internal-Colored");
            if (sh != null)
            {
                mat = Keep.It(new Material(sh));
                mat.SetInt("_SrcBlend", 5);   // SrcAlpha
                mat.SetInt("_DstBlend", 1);   // One (additive)
                mat.SetInt("_Cull", 0);
                mat.SetInt("_ZWrite", 0);
                mat.SetInt("_ZTest", 8);      // Always, so the flash reads through foliage and walls
            }
            else
            {
                sh = Shader.Find("UI/Default");
                mat = Keep.It(sh != null ? new Material(sh) : new Material(Shader.Find("Sprites/Default")));
            }
            return mat;
        }

        private void Show(float seconds)
        {
            visible = true;
            visibleUntil = t + seconds;
            mf.sharedMesh = BuildMesh(rnd.Next());
            go.SetActive(true);
        }

        public void Update(float dt)
        {
            if (done) return;
            t += dt;
            // From right next to it a 128-block bolt fills the whole view with streaks, so hide it when the camera is close.
            if (visible && Camera.main != null)
            {
                Vector3 c = Camera.main.transform.position;
                bool near = new Vector2(c.x - origin.x, c.z - origin.z).magnitude < 16f;
                if (go.activeSelf == near) go.SetActive(!near);
            }
            if (visible && t >= visibleUntil)
            {
                visible = false;
                go.SetActive(false);
                if (flashesLeft > 0) nextFlashAt = t + (float)rnd.NextDouble() * 0.5f;   // vanilla: up to 10 ticks of gap
                else done = true;
            }
            else if (!visible && flashesLeft > 0 && t >= nextFlashAt)
            {
                flashesLeft--;
                Show(0.06f);
            }
            if (done) UnityEngine.Object.Destroy(go);
        }

        // Port of LightningBoltRenderer.render / quad, in world units.
        private Mesh BuildMesh(int seedValue)
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();

            var r = new System.Random(seedValue);
            var ax = new float[8]; var az = new float[8];
            float fx = 0f, fz = 0f;
            for (int i = 7; i >= 0; i--)
            {
                ax[i] = fx; az[i] = fz;
                fx += r.Next(11) - 5;
                fz += r.Next(11) - 5;
            }

            float endX = 0f, endZ = 0f;
            Color col = new Color(0.45f, 0.45f, 0.5f, 0.45f);
            // Each additive layer is a bit brighter so the glow reads against a bright scene.
            for (int k1 = 0; k1 < 4; k1++)
            {
                var r1 = new System.Random(seedValue);
                for (int j = 0; j < 3; j++)
                {
                    int k = 7;
                    int l = 0;
                    if (j > 0) k = 7 - j;
                    if (j > 0) l = k - 2;
                    float f2 = ax[k] - fx;
                    float f3 = az[k] - fz;
                    for (int i1 = k; i1 >= l; i1--)
                    {
                        float f4 = f2, f5 = f3;
                        if (j == 0) { f2 += r1.Next(11) - 5; f3 += r1.Next(11) - 5; }
                        else { f2 += r1.Next(31) - 15; f3 += r1.Next(31) - 15; }
                        if (k1 == 0 && j == 0 && i1 == 0) { endX = f2; endZ = f3; }
                        float f10 = 0.1f + k1 * 0.2f;
                        if (j == 0) f10 *= i1 * 0.1f + 1f;
                        float f11 = 0.1f + k1 * 0.2f;
                        if (j == 0) f11 *= (i1 - 1) * 0.1f + 1f;
                        Quad(verts, cols, tris, f2, f3, i1, f4, f5, col, f10, f11, false, false, true, false);
                        Quad(verts, cols, tris, f2, f3, i1, f4, f5, col, f10, f11, true, false, true, true);
                        Quad(verts, cols, tris, f2, f3, i1, f4, f5, col, f10, f11, true, true, false, true);
                        Quad(verts, cols, tris, f2, f3, i1, f4, f5, col, f10, f11, false, true, false, false);
                    }
                }
            }

            // Pin the bottom of the bolt to the strike point (vanilla's wanders, ours must land on the target).
            Vector3 shift = new Vector3(endX * Block, 0f, endZ * Block);
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i] - shift;

            var m = new Mesh();
            var vl = new Il2CppSystem.Collections.Generic.List<Vector3>();
            var cl = new Il2CppSystem.Collections.Generic.List<Color>();
            var tl = new Il2CppSystem.Collections.Generic.List<int>();
            foreach (var v in verts) vl.Add(v);
            foreach (var c in cols) cl.Add(c);
            foreach (var i in tris) tl.Add(i);
            m.SetVertices(vl);
            m.SetColors(cl);
            m.SetTriangles(tl, 0);
            m.RecalculateBounds();
            return m;
        }

        private static void Quad(List<Vector3> verts, List<Color> cols, List<int> tris, float x1, float z1, int y, float x2, float z2,
                                 Color c, float off1, float off2, bool shiftEast1, bool shiftSouth1, bool shiftEast2, bool shiftSouth2)
        {
            int i0 = verts.Count;
            // Vanilla's y runs 16 blocks per segment and the bolt is mirrored so segment 7 is the top.
            verts.Add(new Vector3(x1 + (shiftEast1 ? off2 : -off2), y * 16f, z1 + (shiftSouth1 ? off2 : -off2)) * Block);
            verts.Add(new Vector3(x2 + (shiftEast1 ? off1 : -off1), (y + 1) * 16f, z2 + (shiftSouth1 ? off1 : -off1)) * Block);
            verts.Add(new Vector3(x2 + (shiftEast2 ? off1 : -off1), (y + 1) * 16f, z2 + (shiftSouth2 ? off1 : -off1)) * Block);
            verts.Add(new Vector3(x1 + (shiftEast2 ? off2 : -off2), y * 16f, z1 + (shiftSouth2 ? off2 : -off2)) * Block);
            for (int i = 0; i < 4; i++) cols.Add(c);
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
            tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 3);
        }
    }

    // Short white flash over the whole screen, like the sky lighting up during a strike.
    internal static class McFlash
    {
        public static float Level;
        public static void Pulse(float amount) { Level = Mathf.Max(Level, amount); }
    }
}

