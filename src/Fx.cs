using System;
using System.Collections.Generic;
using UnityEngine;

namespace MegabonkSteve
{
    // Minecraft particles are flat 2D sprites that always face the camera. Each one here is a single quad per opaque texel
    // (the game's material has no alpha), animated through the vanilla frame sequence.
    internal static class Fx
    {
        public class Frames { public Mesh[] mesh; public Material[] mat; public int Count { get { return mesh.Length; } } }

        private class Sp
        {
            public GameObject go; public MeshFilter mf; public MeshRenderer mr; public Frames f;
            public Vector3 vel; public float life, max, size, grav, drag, growth; public bool loop;
        }

        private static readonly List<Sp> live = new List<Sp>();
        private static readonly Dictionary<string, Frames> cache = new Dictionary<string, Frames>();
        private static Material shared;

        // Unlit, vertex-coloured, alpha-blended and double-sided: the textures are baked into the vertex colours.
        private static Material Mat()
        {
            if (shared != null) return shared;
            Shader sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null) return null;
            var m = new Material(sh);
            m.SetInt("_SrcBlend", 5);   // SrcAlpha
            m.SetInt("_DstBlend", 10);  // OneMinusSrcAlpha
            m.SetInt("_Cull", 0);
            m.SetInt("_ZWrite", 0);
            m.SetInt("_ZTest", 4);
            m.renderQueue = 3000;
            shared = Keep.It(m);
            return shared;
        }

        // ---------------------------------------------------------------- building blocks

        // A flat, camera-facing mesh one unit wide: one quad per visible texel of the region, sampled every `step` texels,
        // coloured straight from the texture (times `tint`) with the texel's own alpha.
        public static Mesh Flat(Texture2D tex, int x0, int y0, int w, int h, int step, Color tint)
        {
            var px = tex.GetPixels32();
            int tw = tex.width, th = tex.height;
            var verts = new Il2CppSystem.Collections.Generic.List<Vector3>();
            var cols = new Il2CppSystem.Collections.Generic.List<Color>();
            var tris = new Il2CppSystem.Collections.Generic.List<int>();
            for (int y = 0; y < h; y += step)
                for (int x = 0; x < w; x += step)
                {
                    int sx = Mathf.Min(x0 + x + step / 2, tw - 1), sy = Mathf.Min(y0 + y + step / 2, th - 1);
                    var p = px[sy * tw + sx];
                    if (p.a < 12) continue;
                    float cx = x / (float)w - 0.5f, cy = y / (float)h - 0.5f, e = step / (float)w;
                    var c = new Color(p.r / 255f * tint.r, p.g / 255f * tint.g, p.b / 255f * tint.b, p.a / 255f * tint.a);
                    int i0 = verts.Count;
                    verts.Add(new Vector3(cx, cy, 0)); verts.Add(new Vector3(cx + e, cy, 0));
                    verts.Add(new Vector3(cx + e, cy + e, 0)); verts.Add(new Vector3(cx, cy + e, 0));
                    for (int i = 0; i < 4; i++) cols.Add(c);
                    tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 1); tris.Add(i0); tris.Add(i0 + 3); tris.Add(i0 + 2);
                }
            var m = new Mesh();
            m.SetVertices(verts); m.SetColors(cols); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return Keep.It(m);
        }

        // prefix_0..prefix_(n-1) from the particle folder, or a single `prefix` texture when n is 0. Big textures are thinned out.
        public static Frames Get(string prefix, int n, Color tint)
        {
            try { return GetInner(prefix, n, tint); }
            catch (Exception e) { Plugin.Logger.LogWarning("fx " + prefix + ": " + e.Message); return null; }
        }

        private static Frames GetInner(string prefix, int n, Color tint)
        {
            string key = prefix + "|" + n + "|" + tint;
            Frames f;
            if (cache.TryGetValue(key, out f)) return f;
            if (Mat() == null) return null;
            int count = Mathf.Max(1, n);
            var meshes = new List<Mesh>(); var ms = new List<Material>();
            for (int i = 0; i < count; i++)
            {
                var t = McAssets.Tex("particle/" + (n == 0 ? prefix : prefix + "_" + i));
                if (t == null) continue;
                int step = Mathf.Max(1, t.width / 16);
                meshes.Add(Flat(t, 0, 0, t.width, t.height, step, tint));
                ms.Add(Mat());
            }
            if (meshes.Count == 0) { cache[key] = null; return null; }
            f = new Frames { mesh = meshes.ToArray(), mat = ms.ToArray() };
            cache[key] = f;
            return f;
        }

        private static Sp SpawnRaw(Frames f, Vector3 pos, Vector3 vel, float size, float life, float grav, float drag, bool loop, float growth)
        {
            if (f == null || f.Count == 0 || f.mat[0] == null) return null;
            var go = new GameObject("fx");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = f.mesh[0];
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = f.mat[0];
            var s = new Sp { go = go, mf = mf, mr = mr, f = f, vel = vel, max = life, size = size, grav = grav, drag = drag, loop = loop, growth = growth };
            live.Add(s);
            return s;
        }

        public static void Spawn(Frames f, Vector3 pos, Vector3 vel, float size, float life, float grav = 0f, float drag = 0f, bool loop = false, float growth = 0f)
        {
            SpawnRaw(f, pos, vel, size, life, grav, drag, loop, growth);
        }

        // ---------------------------------------------------------------- vanilla particle recipes

        private static Vector3 Rand(float s) { return UnityEngine.Random.insideUnitSphere * s; }

        // The smoke / poof sprite (generic_0..7), tinted: used for dust, puffs and coloured clouds.
        public static void Puffs(Vector3 pos, Color tint, int count, float speed, float size)
        {
            var f = Get("generic", 8, tint);
            for (int i = 0; i < count; i++)
            {
                var v = Rand(speed); v.y = Mathf.Abs(v.y) * 0.8f + speed * 0.2f;
                Spawn(f, pos + Rand(size * 0.4f), v, size * UnityEngine.Random.Range(0.7f, 1.3f), UnityEngine.Random.Range(0.45f, 0.9f), -2f, 1.5f);
            }
        }

        // Vanilla explosion: the big animated blast plus rising smoke.
        public static void Explosion(Vector3 pos, float radius)
        {
            var f = Get("explosion", 16, Color.white);
            Spawn(f, pos, Vector3.zero, Mathf.Max(5f, radius * 2.6f), 0.55f);
            var smoke = Get("big_smoke", 12, new Color(0.45f, 0.45f, 0.45f));
            for (int i = 0; i < 5; i++)
            {
                var v = Rand(radius * 0.9f); v.y = Mathf.Abs(v.y) + 2f;
                Spawn(smoke, pos + Rand(radius * 0.5f), v, radius * UnityEngine.Random.Range(0.5f, 0.9f), UnityEngine.Random.Range(0.6f, 1.0f), 0f, 2f);
            }
        }

        public static void Flame(Vector3 pos, float size) { Spawn(Get("flame", 0, Color.white), pos + Rand(size * 0.2f), Rand(2f), size, 0.35f, -4f, 1f); }
        public static void Spark(Vector3 pos, float size) { Spawn(Get("spark", 8, Color.white), pos + Rand(size * 0.2f), Rand(3f), size, 0.4f, 0f, 1f); }
        public static void Hit(Vector3 pos) { var f = Get("critical_hit", 0, Color.white); for (int i = 0; i < 3; i++) Spawn(f, pos + Rand(0.8f), Rand(7f), 1.1f, 0.4f, 0f, 1.5f); }
        public static void Smoke(Vector3 pos, float size) { Spawn(Get("generic", 8, new Color(0.25f, 0.25f, 0.25f)), pos, Rand(1f) + Vector3.up * 1.5f, size, 0.6f, -1f, 1f); }

        public static void SonicBoom(Vector3 pos, float size) { Spawn(Get("sonic_boom", 16, Color.white), pos, Vector3.zero, size, 0.5f); }
        public static void Splash(Vector3 pos) { var f = Get("splash", 4, new Color(0.35f, 0.9f, 0.35f)); for (int i = 0; i < 10; i++) Spawn(f, pos + Rand(1.5f), Rand(8f) + Vector3.up * 5f, 1.1f, 0.6f, 20f, 0.5f, true); }
        public static void Swirl(Vector3 pos, Color tint) { Spawn(Get("effect", 8, tint), pos, Vector3.up * 1.5f, 1.3f, 0.8f, 0f, 1f); }

        // ---------------------------------------------------------------- update

        public static void Tick(float dt)
        {
            var cam = Camera.main;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var s = live[i];
                s.life += dt;
                if (s.go == null || s.life >= s.max) { if (s.go != null) UnityEngine.Object.Destroy(s.go); live.RemoveAt(i); continue; }
                float t = s.life / s.max;
                int n = s.f.Count;
                int fr = s.loop ? (int)(s.life * 12f) % n : Mathf.Min(n - 1, (int)(t * n));
                if (s.mf.sharedMesh != s.f.mesh[fr]) { s.mf.sharedMesh = s.f.mesh[fr]; s.mr.sharedMaterial = s.f.mat[fr]; }
                s.vel.y -= s.grav * dt;
                if (s.drag > 0f) s.vel *= Mathf.Max(0f, 1f - s.drag * dt);
                var tr = s.go.transform;
                tr.position += s.vel * dt;
                float sc = s.size * (1f + s.growth * t) * (t > 0.7f ? Mathf.Lerp(1f, 0.55f, (t - 0.7f) / 0.3f) : 1f);
                tr.localScale = Vector3.one * sc;
                if (cam != null) tr.rotation = cam.transform.rotation;
            }
            TickOrbs();
            TickIngots();
        }

        public static void Clear()
        {
            foreach (var s in live) if (s.go != null) UnityEngine.Object.Destroy(s.go);
            live.Clear();
        }

        // ---------------------------------------------------------------- gold and silver pickups: ingots

        private static readonly Dictionary<int, GameObject> ingots = new Dictionary<int, GameObject>();
        private static readonly Dictionary<string, Mesh> ingotMesh = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, Material> ingotMat = new Dictionary<string, Material>();

        // Gold coins become gold ingots and silver becomes iron ingots: a spinning slab of the item sprite.
        public static void SkinIngot(Component p, string icon)
        {
            if (p == null || AutoWeapons.baseMat == null) return;
            var tex = McAssets.Tex(icon);
            if (tex == null) return;
            Mesh mesh; Material mat;
            if (!ingotMesh.TryGetValue(icon, out mesh) || mesh == null) { mesh = ItemMesh.Build(tex); ingotMesh[icon] = mesh; }
            if (!ingotMat.TryGetValue(icon, out mat) || mat == null)
            {
                mat = Keep.It(new Material(AutoWeapons.baseMat));
                mat.mainTexture = tex;
                foreach (var n in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" })
                    if (mat.HasProperty(n)) mat.SetTexture(n, tex);
                ingotMat[icon] = mat;
            }
            int id = p.GetInstanceID();
            GameObject go;
            if (!ingots.TryGetValue(id, out go) || go == null)
            {
                go = new GameObject("MC_Ingot");
                go.transform.SetParent(p.transform, false);
                go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();
                ingots[id] = go;
            }
            foreach (var r in p.GetComponentsInChildren<Renderer>(true))
                if (r != null && r.gameObject != go && !(r is ParticleSystemRenderer)) r.enabled = false;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.transform.localScale = Vector3.one * 0.12f;
            go.transform.localPosition = Vector3.up * 0.5f;
        }

        private static void TickIngots()
        {
            if (ingots.Count == 0) return;
            List<int> dead = null;
            foreach (var kv in ingots)
            {
                var g = kv.Value;
                if (g == null) { (dead ?? (dead = new List<int>())).Add(kv.Key); continue; }
                if (!g.activeInHierarchy) continue;
                g.transform.localRotation = Quaternion.Euler(20f, Time.time * 140f, 0f);
            }
            if (dead != null) foreach (var k in dead) ingots.Remove(k);
        }

        // ---------------------------------------------------------------- experience orbs

        private class Orb { public Pickup pickup; public GameObject go; public MeshFilter mf; public MeshRenderer mr; public int icon = -1; }
        private static readonly Dictionary<int, Orb> orbs = new Dictionary<int, Orb>();
        private static Mesh[] orbMeshes = new Mesh[11 * 12];
        private static bool orbReady;
        private const int Phases = 12;
        private static bool orbWarned, orbLogged;

        private static int IconFor(int v)
        {
            return v >= 2477 ? 10 : v >= 1237 ? 9 : v >= 617 ? 8 : v >= 307 ? 7 : v >= 149 ? 6 : v >= 73 ? 5 : v >= 37 ? 4 : v >= 17 ? 3 : v >= 7 ? 2 : v >= 3 ? 1 : 0;
        }

        private static bool PrepareOrbs()
        {
            if (orbReady) return true;
            if (McAssets.Tex("entity/experience/experience_orb") == null || Mat() == null) return false;
            orbReady = true;
            return true;
        }

        // One mesh per icon and per pulse phase: the vanilla orb flickers between green and yellow.
        private static Mesh OrbMesh(int icon, int phase)
        {
            int k = icon * Phases + phase;
            if (orbMeshes[k] != null) return orbMeshes[k];
            var sheet = McAssets.Tex("entity/experience/experience_orb");
            int cell = sheet.width / 4;
            int row = icon / 4, col = icon % 4;
            float a = phase / (float)Phases * Mathf.PI * 2f;
            var tint = new Color((Mathf.Sin(a) + 1f) * 0.5f * 0.55f + 0.45f, 1f, (Mathf.Sin(a + 4.1887f) + 1f) * 0.1f + 0.3f, 1f);
            orbMeshes[k] = Flat(sheet, col * cell, sheet.height - cell * (row + 1), cell, cell, 1, tint);
            return orbMeshes[k];
        }

        // Called whenever the game sets up or changes an experience gem: swap its look for the vanilla orb.
        public static void SkinXp(Pickup p)
        {
            if (p == null || !PrepareOrbs()) { if (!orbWarned) { orbWarned = true; Plugin.Logger.LogWarning("orb sheet missing"); } return; }
            if (!orbLogged) { orbLogged = true; Plugin.Logger.LogInfo("first xp orb skinned, value " + p.GetValue()); }
            int id = p.GetInstanceID();
            Orb o;
            if (!orbs.TryGetValue(id, out o) || o.go == null)
            {
                var go = new GameObject("MC_Orb");
                go.transform.SetParent(p.transform, false);
                o = new Orb { pickup = p, go = go, mf = go.AddComponent<MeshFilter>(), mr = go.AddComponent<MeshRenderer>() };
                orbs[id] = o;
            }
            foreach (var r in p.GetComponentsInChildren<Renderer>(true))
                if (r != null && r.gameObject != o.go) r.enabled = false;
            int icon = IconFor(p.GetValue());
            if (icon != o.icon)
            {
                o.icon = icon;
                o.mf.sharedMesh = OrbMesh(icon, 0);
                o.go.transform.localScale = Vector3.one * (1.4f + icon * 0.12f);
            }
            o.go.transform.localPosition = Vector3.up * 0.4f;
            o.mr.sharedMaterial = Mat();
        }

        private static void TickOrbs()
        {
            if (orbs.Count == 0 || !orbReady) return;
            var cam = Camera.main;
            int phase = (int)(Time.time * 6f) % Phases;
            List<int> dead = null;
            foreach (var kv in orbs)
            {
                var o = kv.Value;
                if (o.go == null || o.pickup == null) { (dead ?? (dead = new List<int>())).Add(kv.Key); continue; }
                if (!o.go.activeInHierarchy) continue;
                o.mf.sharedMesh = OrbMesh(o.icon < 0 ? 0 : o.icon, phase);
                if (cam != null) o.go.transform.rotation = cam.transform.rotation;
            }
            if (dead != null) foreach (var k in dead) orbs.Remove(k);
        }
    }
}
