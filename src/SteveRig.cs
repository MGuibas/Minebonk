using System;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.Actors;
using Assets.Scripts.Actors.Player;

namespace MegabonkSteve
{
    // Blocky Steve built from boxes, parented under the player's renderer object.
    public class SteveRig : MonoBehaviour
    {
        public SteveRig(IntPtr ptr) : base(ptr) { }

        public PlayerRenderer playerRenderer;
        public Transform armR, armL, legR, legL, head, body, upper;
        public Material itemMat;
        public Texture2D skinTex;
        public bool slim;
        public Transform heldRoot;
        public float unit;
        public int armW = 4;
        private float swingTime = -10f;
        private float phase;
        private float swing;

        public static SteveRig Attach(PlayerRenderer pr)
        {
            var parent = pr.rendererObject != null ? pr.rendererObject.transform : pr.transform;
            if (parent.Find("SteveRig") != null) return null;

            // Match the original model's height so camera/collider stay sensible.
            float worldHeight = 2f;
            if (pr.renderer != null && pr.renderer.bounds.size.y > 0.2f) worldHeight = pr.renderer.bounds.size.y;
            float parentScale = Mathf.Max(0.0001f, parent.lossyScale.y);
            float unit = (worldHeight / parentScale) / 32f; // 32 skin pixels tall

            // Hide the original skinned mesh and anything attached to it.
            if (pr.renderer != null) pr.renderer.enabled = false;

            var root = new GameObject("SteveRig");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            var rig = root.AddComponent<SteveRig>();
            rig.playerRenderer = pr;

            Material baseMat = pr.defaultMaterial != null ? pr.defaultMaterial
                : (pr.renderer != null ? pr.renderer.sharedMaterial : null);
            var mat = Keep.It(baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Unlit/Texture")));
            McAssets.Init();
            bool slim;
            var tex = McAssets.Skin(out slim) ?? SteveTextures.GetSkin();
            int armW = slim ? 3 : 4;
            rig.skinTex = tex; rig.slim = slim; rig.unit = unit; rig.armW = armW;
            mat.mainTexture = tex;
            foreach (var n in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" })
                if (mat.HasProperty(n)) mat.SetTexture(n, tex);
            foreach (var n in new[] { "_Color", "_BaseColor" })
                if (mat.HasProperty(n)) mat.SetColor(n, Color.white);

            // Everything above the hips hangs off `upper`, so crouching leans the torso, head and arms only.
            var upper = new GameObject("upper").transform;
            upper.SetParent(root.transform, false);
            upper.localPosition = new Vector3(0f, 12f * unit, 0f);
            rig.upper = upper;

            Transform Part(Transform par, string name, Vector3 pivotPx, Vector3 sizePx, int u, int v, bool hangFromPivot)
            {
                var pivot = new GameObject(name).transform;
                pivot.SetParent(par, false);
                Vector3 local = pivotPx;
                if (par == upper) local -= new Vector3(0f, 12f, 0f);
                pivot.localPosition = local * unit;
                var mesh = new GameObject(name + "_mesh");
                mesh.transform.SetParent(pivot, false);
                mesh.transform.localPosition = hangFromPivot ? new Vector3(0, -sizePx.y / 2f * unit, 0) : Vector3.zero;
                mesh.AddComponent<MeshFilter>().sharedMesh = BoxMesh(sizePx.x * unit, sizePx.y * unit, sizePx.z * unit,
                    (int)sizePx.x, (int)sizePx.y, (int)sizePx.z, u, v);
                mesh.AddComponent<MeshRenderer>().sharedMaterial = mat;
                return pivot;
            }

            rig.legR = Part(root.transform, "legR", new Vector3(2, 12, 0), new Vector3(4, 12, 4), 0, 16, true);
            rig.legL = Part(root.transform, "legL", new Vector3(-2, 12, 0), new Vector3(4, 12, 4), 16, 48, true);
            rig.body = Part(upper, "body", new Vector3(0, 18, 0), new Vector3(8, 12, 4), 16, 16, false);
            rig.armR = Part(upper, "armR", new Vector3(4 + armW / 2f, 24, 0), new Vector3(armW, 12, 4), 40, 16, true);
            rig.armL = Part(upper, "armL", new Vector3(-(4 + armW / 2f), 24, 0), new Vector3(armW, 12, 4), 32, 48, true);
            rig.head = Part(upper, "head", new Vector3(0, 28, 0), new Vector3(8, 8, 8), 0, 0, false);

            Plugin.Logger.LogInfo("Steve model attached, unit=" + unit);
            rig.itemMat = Keep.It(new Material(mat));
            rig.BuildLayers();
            var mp = pr.GetComponentInParent<MyPlayer>();
            if (mp != null && MinecraftMode.Instance == null)
            {
                var mm = mp.gameObject.AddComponent<MinecraftMode>();
                mm.playerRenderer = pr;
                mm.rig = rig;
                rig.SetHeld(mm.inv.defs[0] != null ? mm.inv.defs[0].icon : null);
            }
            return rig;
        }

        // ------------------------------------------------------------ cut-out layers (skin overlay, armor, wings)

        private static readonly Dictionary<Texture2D, Material> layerMats = new Dictionary<Texture2D, Material>();

        // Same shader as the body (so lighting and outlines match), with a different texture.
        private Material LayerMat(Texture2D tex)
        {
            Material m;
            if (layerMats.TryGetValue(tex, out m) && m != null) return m;
            m = Keep.It(new Material(itemMat));
            m.mainTexture = tex;
            foreach (var n in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" })
                if (m.HasProperty(n)) m.SetTexture(n, tex);
            layerMats[tex] = m;
            return m;
        }

        private GameObject AddCutout(Transform pivot, bool hang, Vector3 baseSize, float inflate, int u, int v, Texture2D tex, bool mirror)
        {
            var go = new GameObject("layer");
            go.transform.SetParent(pivot, false);
            go.transform.localPosition = hang ? new Vector3(0f, -baseSize.y / 2f * unit, 0f) : Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = CutoutBox(tex, (baseSize.x + 2f * inflate) * unit, (baseSize.y + 2f * inflate) * unit,
                (baseSize.z + 2f * inflate) * unit, (int)baseSize.x, (int)baseSize.y, (int)baseSize.z, u, v, mirror);
            go.AddComponent<MeshRenderer>().sharedMaterial = LayerMat(tex);
            return go;
        }

        private readonly List<GameObject> armorObjs = new List<GameObject>();

        // Second skin layer (hat, jacket, sleeves, pants): only the opaque pixels become geometry.
        public void BuildLayers()
        {
            if (skinTex == null || skinTex.width != 64 || skinTex.height != 64) return;
            AddCutout(head, false, new Vector3(8, 8, 8), 0.5f, 32, 0, skinTex, false);
            AddCutout(body, false, new Vector3(8, 12, 4), 0.5f, 16, 32, skinTex, false);
            AddCutout(armR, true, new Vector3(armW, 12, 4), 0.5f, 40, 32, skinTex, false);
            AddCutout(armL, true, new Vector3(armW, 12, 4), 0.5f, 48, 48, skinTex, false);
            AddCutout(legR, true, new Vector3(4, 12, 4), 0.5f, 0, 32, skinTex, false);
            AddCutout(legL, true, new Vector3(4, 12, 4), 0.5f, 0, 48, skinTex, false);
        }

        // Armor from the vanilla equipment textures (layer 1: helmet, chest, boots; layer 2: leggings).
        public void SetArmor(ItemDef[] worn)
        {
            foreach (var o in armorObjs) if (o != null) UnityEngine.Object.Destroy(o);
            armorObjs.Clear();
            if (worn == null || unit <= 0f) return;

            string Mat(ItemDef d) { return !string.IsNullOrEmpty(d.tier) ? d.tier : (d.name.StartsWith("Netherite") ? "netherite" : "diamond"); }
            var helmet = worn[0]; var chest = worn[1]; var legs = worn[2]; var boots = worn[3];

            if (helmet != null && helmet.kind == ItemKind.Armor)
            {
                var t = McAssets.Tex("entity/equipment/humanoid/" + Mat(helmet));
                if (t != null) armorObjs.Add(AddCutout(head, false, new Vector3(8, 8, 8), 1f, 0, 0, t, false));
            }
            if (chest != null && chest.kind == ItemKind.Armor)
            {
                var t = McAssets.Tex("entity/equipment/humanoid/" + Mat(chest));
                if (t != null)
                {
                    armorObjs.Add(AddCutout(body, false, new Vector3(8, 12, 4), 1f, 16, 16, t, false));
                    armorObjs.Add(AddCutout(armR, true, new Vector3(armW, 12, 4), 1f, 40, 16, t, false));
                    armorObjs.Add(AddCutout(armL, true, new Vector3(armW, 12, 4), 1f, 40, 16, t, true));
                }
            }
            if (legs != null && legs.kind == ItemKind.Armor)
            {
                var t = McAssets.Tex("entity/equipment/humanoid_leggings/" + Mat(legs));
                if (t != null)
                {
                    armorObjs.Add(AddCutout(body, false, new Vector3(8, 12, 4), 0.5f, 16, 16, t, false));
                    armorObjs.Add(AddCutout(legR, true, new Vector3(4, 12, 4), 0.5f, 0, 16, t, false));
                    armorObjs.Add(AddCutout(legL, true, new Vector3(4, 12, 4), 0.5f, 0, 16, t, true));
                }
            }
            if (boots != null && boots.kind == ItemKind.Armor)
            {
                var t = McAssets.Tex("entity/equipment/humanoid/" + Mat(boots));
                if (t != null)
                {
                    armorObjs.Add(AddCutout(legR, true, new Vector3(4, 12, 4), 1f, 0, 16, t, false));
                    armorObjs.Add(AddCutout(legL, true, new Vector3(4, 12, 4), 1f, 0, 16, t, true));
                }
            }
        }

        // ------------------------------------------------------------ poses

        // Whole body (gliding) and torso only (crouching), both pivoting at the hips.
        public float tiltTarget, crouchTarget;
        private float tilt, crouch;
        private Transform wingRoot, wingL, wingR;
        private bool wingsOut;

        private void ApplyTilt()
        {
            float k = Mathf.Clamp01(Time.deltaTime * 9f);
            tilt = Mathf.Lerp(tilt, tiltTarget, k);
            crouch = Mathf.Lerp(crouch, crouchTarget, k);
            var q = Quaternion.Euler(tilt, 0f, 0f);
            Vector3 hips = new Vector3(0f, 12f * unit, 0f);
            transform.localRotation = q;
            transform.localPosition = hips - q * hips + new Vector3(0f, -crouch / 28f * 1.5f * unit, 0f);
            if (upper != null) upper.localRotation = Quaternion.Euler(crouch, 0f, 0f);
        }

        // Elytra on the back, from the vanilla texture, built from its opaque pixels.
        public void SetElytra(bool on)
        {
            if (!on)
            {
                if (wingRoot != null) UnityEngine.Object.Destroy(wingRoot.gameObject);
                wingRoot = null; wingL = wingR = null;
                return;
            }
            if (wingRoot != null || unit <= 0f || upper == null) return;
            var tex = McAssets.Tex("entity/equipment/wings/elytra");
            if (tex == null) return;
            var root = new GameObject("elytra");
            root.transform.SetParent(upper, false);
            wingRoot = root.transform;
            for (int s = -1; s <= 1; s += 2)
            {
                var pivot = new GameObject(s < 0 ? "wingL" : "wingR").transform;
                pivot.SetParent(wingRoot, false);
                pivot.localPosition = new Vector3(s * 1f * unit, (24f - 12f) * unit, -2.5f * unit);
                var wm = new GameObject("mesh");
                wm.transform.SetParent(pivot, false);
                wm.transform.localPosition = new Vector3(s * 5f * unit, -10f * unit, 0f);
                // The left wing is the right one mirrored.
                wm.AddComponent<MeshFilter>().sharedMesh = CutoutBox(tex, 10f * unit, 20f * unit, 2f * unit, 10, 20, 2, 22, 0, s < 0);
                wm.AddComponent<MeshRenderer>().sharedMaterial = LayerMat(tex);
                if (s < 0) wingL = pivot; else wingR = pivot;
            }
            wingsOut = false;
            ApplyWings(false);
        }

        public void SetGliding(bool g)
        {
            if (g == wingsOut) return;
            wingsOut = g;
            ApplyWings(g);
        }

        private void ApplyWings(bool spread)
        {
            if (wingL == null || wingR == null) return;
            float z = spread ? 40f : 12f, x = spread ? 10f : 15f;
            wingR.localRotation = Quaternion.Euler(x, 0f, z);
            wingL.localRotation = Quaternion.Euler(x, 0f, -z);
        }

        public void Swing() { swingTime = Time.time; }

        // Shield in the left arm, in front of the forearm (third person). Raised while blocking.
        public bool blocking;
        private Transform shieldRoot;
        private static Mesh shPlate, shGrip;

        public void SetShield(bool on)
        {
            if (shieldRoot != null) UnityEngine.Object.Destroy(shieldRoot.gameObject);
            shieldRoot = null;
            if (!on || armL == null || itemMat == null || unit <= 0f) return;
            var tex = FirstPerson.RealShield();
            if (tex == null) return;
            if (shPlate == null) { shPlate = BoxMesh(12f, 22f, 1f, 12, 22, 1, 0, 0); shGrip = BoxMesh(2f, 6f, 6f, 2, 6, 6, 26, 0); }
            var mat = Keep.It(new Material(itemMat));
            mat.mainTexture = tex;
            foreach (var n in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" })
                if (mat.HasProperty(n)) mat.SetTexture(n, tex);
            var go = new GameObject("shield3p");
            go.transform.SetParent(armL, false);
            go.transform.localPosition = new Vector3(0f, -6f * unit, 3.2f * unit);
            go.transform.localScale = Vector3.one * unit;
            var plate = new GameObject("plate"); plate.transform.SetParent(go.transform, false);
            plate.AddComponent<MeshFilter>().sharedMesh = shPlate; plate.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var grip = new GameObject("grip"); grip.transform.SetParent(go.transform, false);
            grip.transform.localPosition = new Vector3(0f, 0f, -3.5f);
            grip.AddComponent<MeshFilter>().sharedMesh = shGrip; grip.AddComponent<MeshRenderer>().sharedMaterial = mat;
            shieldRoot = go.transform;
        }

        // Minecraft-style handheld item: the 16x16 icon extruded into a thin voxel slab.
        public void SetHeld(Texture2D icon, Vector3 grip = default(Vector3))
        {
            if (heldRoot != null) UnityEngine.Object.Destroy(heldRoot.gameObject);
            heldRoot = null;
            if (icon == null || armR == null || itemMat == null) return;
            var go = new GameObject("held");
            go.transform.SetParent(armR, false);
            go.transform.localPosition = new Vector3(0f, -10f * unit, 1.5f * unit);
            go.transform.localRotation = Quaternion.Euler(0f, -90f, -45f);   // blade forward, the handle in the hand
            go.transform.localScale = Vector3.one * unit * 1.1f;
            var mat = Keep.It(new Material(itemMat));
            mat.mainTexture = icon;
            foreach (var n in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" })
                if (mat.HasProperty(n)) mat.SetTexture(n, icon);
            var m = new GameObject("held_m");
            m.transform.SetParent(go.transform, false);
            m.transform.localPosition = -grip;
            m.AddComponent<MeshFilter>().sharedMesh = ItemMesh.Build(icon);
            m.AddComponent<MeshRenderer>().sharedMaterial = mat;
            heldRoot = go.transform;
        }

        // Set every frame by MinecraftMode.
        public float moveSpeed;         // horizontal speed in blocks per tick
        public bool flying, crouching, aiming;
        public float headPitch, headYaw; // degrees, relative to the body
        private float limbSwing, limbAmount;

        private void LateUpdate()
        {
            ApplyTilt();
            float ticks = Time.deltaTime * 20f;

            // Vanilla limb animation: amplitude follows the distance walked, so standing still means no swinging,
            // and gliding hangs the limbs.
            float targetAmt = flying ? 0f : Mathf.Min(1f, moveSpeed * 4f);
            limbAmount += (targetAmt - limbAmount) * Mathf.Min(1f, 0.4f * ticks);
            limbSwing += limbAmount * ticks;
            float ph = limbSwing * 0.6662f;
            float rad = Mathf.Rad2Deg;
            float legA = Mathf.Cos(ph) * 1.4f * limbAmount * rad;
            float armA = Mathf.Cos(ph + Mathf.PI) * 2f * limbAmount * 0.5f * rad;
            float idle = Mathf.Sin(Time.time * 20f * 0.067f) * 0.05f * rad * (1f - limbAmount);
            float lean = crouching ? 22f : 0f;

            float st = Time.time - swingTime;
            if (st < 0.28f && armR)
            {
                float k = Mathf.Sin(st / 0.28f * Mathf.PI);
                armR.localRotation = Quaternion.Euler(-100f * k, 0f, 0f);
            }
            else if (armR) armR.localRotation = Quaternion.Euler(armA + lean, 0f, idle + 1f);
            if (armL) armL.localRotation = Quaternion.Euler(-armA + lean, 0f, -idle - 1f);
            if (aiming)
            {
                // Bow drawn: both arms out in front, pitched with the head.
                if (armR) armR.localRotation = Quaternion.Euler(-88f + headPitch * 0.5f, -6f, 0f);
                if (armL) armL.localRotation = Quaternion.Euler(-82f + headPitch * 0.5f, 22f, 0f);
            }
            if (blocking && shieldRoot != null && armL) armL.localRotation = Quaternion.Euler(-62f + headPitch * 0.3f, 14f, 0f);
            if (shieldRoot != null)
            {
                // Held at the side with its face out to the left; swung round to face forward when blocking.
                Vector3 restPos = new Vector3(-(armW / 2f + 1.3f) * unit, -6f * unit, 0.5f * unit);
                Vector3 blockPos = new Vector3(0f, -6f * unit, 3.2f * unit);
                float k = Mathf.Min(1f, Time.deltaTime * 12f);
                shieldRoot.localPosition = Vector3.Lerp(shieldRoot.localPosition, blocking ? blockPos : restPos, k);
                shieldRoot.localRotation = Quaternion.Slerp(shieldRoot.localRotation, blocking ? Quaternion.identity : Quaternion.Euler(0f, -90f, 0f), k);
            }
            if (legR) legR.localRotation = Quaternion.Euler(-legA, 0f, 0f);
            if (legL) legL.localRotation = Quaternion.Euler(legA, 0f, 0f);
            if (head) head.localRotation = Quaternion.Euler(headPitch, headYaw, 0f);
        }

        // ------------------------------------------------------------ geometry

        // The six faces of a Minecraft box: normal, corner positions (bottom-left, bottom-right, top-right, top-left
        // as seen from outside) and the matching skin pixel coordinates (top-left origin).
        private static void Faces(float sx, float sy, float sz, int w, int h, int d, int u, int v,
                                  Action<Vector3, Vector3[], Vector2[]> face, bool swapTB = false)
        {
            int topU = swapTB ? u + d + w : u + d, botU = swapTB ? u + d : u + d + w;
            float x = sx / 2f, y = sy / 2f, z = sz / 2f;
            // Front (+Z): u grows toward -X.
            face(Vector3.forward,
                new[] { new Vector3(x, -y, z), new Vector3(-x, -y, z), new Vector3(-x, y, z), new Vector3(x, y, z) },
                new[] { new Vector2(u + d, v + d + h), new Vector2(u + d + w, v + d + h), new Vector2(u + d + w, v + d), new Vector2(u + d, v + d) });
            // Back (-Z): u grows toward +X.
            face(Vector3.back,
                new[] { new Vector3(-x, -y, -z), new Vector3(x, -y, -z), new Vector3(x, y, -z), new Vector3(-x, y, -z) },
                new[] { new Vector2(u + 2 * d + w, v + d + h), new Vector2(u + 2 * d + 2 * w, v + d + h), new Vector2(u + 2 * d + 2 * w, v + d), new Vector2(u + 2 * d + w, v + d) });
            // Right (+X): u grows toward +Z.
            face(Vector3.right,
                new[] { new Vector3(x, -y, -z), new Vector3(x, -y, z), new Vector3(x, y, z), new Vector3(x, y, -z) },
                new[] { new Vector2(u, v + d + h), new Vector2(u + d, v + d + h), new Vector2(u + d, v + d), new Vector2(u, v + d) });
            // Left (-X): u grows toward -Z.
            face(Vector3.left,
                new[] { new Vector3(-x, -y, z), new Vector3(-x, -y, -z), new Vector3(-x, y, -z), new Vector3(-x, y, z) },
                new[] { new Vector2(u + d + w, v + d + h), new Vector2(u + 2 * d + w, v + d + h), new Vector2(u + 2 * d + w, v + d), new Vector2(u + d + w, v + d) });
            // Top (+Y).
            face(Vector3.up,
                new[] { new Vector3(x, y, -z), new Vector3(-x, y, -z), new Vector3(-x, y, z), new Vector3(x, y, z) },
                new[] { new Vector2(topU, v), new Vector2(topU + w, v), new Vector2(topU + w, v + d), new Vector2(topU, v + d) });
            // Bottom (-Y).
            face(Vector3.down,
                new[] { new Vector3(x, -y, z), new Vector3(-x, -y, z), new Vector3(-x, -y, -z), new Vector3(x, -y, -z) },
                new[] { new Vector2(botU, v), new Vector2(botU + w, v), new Vector2(botU + w, v + d), new Vector2(botU, v + d) });
        }

        private static Mesh Finish(List<Vector3> verts, List<Vector2> uvs, List<int> tris, List<Color> cols = null)
        {
            var m = new Mesh();
            m.SetVertices((Il2CppSystem.Collections.Generic.List<Vector3>)ToIl2Cpp(verts));
            m.SetUVs(0, (Il2CppSystem.Collections.Generic.List<Vector2>)ToIl2Cpp(uvs));
            if (cols != null) m.SetColors((Il2CppSystem.Collections.Generic.List<Color>)ToIl2Cpp(cols));
            m.SetTriangles((Il2CppSystem.Collections.Generic.List<int>)ToIl2Cpp(tris), 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return Keep.It(m);
        }

        // Box with Minecraft-style UV layout on a 64x64 skin. Size in object units, w/h/d in skin pixels.
        internal static Mesh BoxMesh(float sx, float sy, float sz, int w, int h, int d, int u, int v)
        {
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            Faces(sx, sy, sz, w, h, d, u, v, (normal, p, skinPx) =>
            {
                int i0 = verts.Count;
                for (int i = 0; i < 4; i++) { verts.Add(p[i]); uvs.Add(new Vector2(skinPx[i].x / 64f, 1f - skinPx[i].y / 64f)); }
                bool flip = Vector3.Dot(Vector3.Cross(p[1] - p[0], p[2] - p[0]), normal) < 0f;
                int[] order = flip ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
                foreach (var o in order) tris.Add(i0 + o);
            });
            return Finish(verts, uvs, tris);
        }

        // The same box, but every face is made of one quad per opaque texel, so transparent texels simply
        // aren't there. Works with any texture size (armor and elytra textures are 64x32) and any shader.
        internal static Mesh CutoutBox(Texture2D tex, float sx, float sy, float sz, int w, int h, int d, int u, int v, bool mirror, bool colors = false, float alphaMul = 1f, float rgbMul = 1f, bool swapTB = false)
        {
            int tw = tex.width, th = tex.height;
            var px = tex.GetPixels32();
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            var cols = colors ? new List<Color>() : null;
            Faces(sx, sy, sz, w, h, d, u, v, (normal, p, s) =>
            {
                Vector2 e1 = s[1] - s[0], e3 = s[3] - s[0];
                float l1 = e1.sqrMagnitude, l3 = e3.sqrMagnitude;
                Vector3 a = p[1] - p[0], b = p[3] - p[0];
                float xa = Mathf.Min(s[0].x, s[2].x), xb = Mathf.Max(s[0].x, s[2].x);
                float ya = Mathf.Min(s[0].y, s[2].y), yb = Mathf.Max(s[0].y, s[2].y);

                Vector3 Pos(float sxp, float syp)
                {
                    Vector2 r = new Vector2(sxp, syp) - s[0];
                    float al = Vector2.Dot(r, e1) / l1, be = Vector2.Dot(r, e3) / l3;
                    return p[0] + a * al + b * be;
                }

                for (int py = (int)ya; py < (int)yb; py++)
                    for (int pxl = (int)xa; pxl < (int)xb; pxl++)
                    {
                        if (pxl < 0 || pxl >= tw || py < 0 || py >= th) continue;
                        if (px[(th - 1 - py) * tw + pxl].a < 20) continue;
                        Vector3 c0 = Pos(pxl, py), c1 = Pos(pxl + 1, py), c2 = Pos(pxl + 1, py + 1), c3 = Pos(pxl, py + 1);
                        var uv = new Vector2((pxl + 0.5f) / tw, 1f - (py + 0.5f) / th);
                        int i0 = verts.Count;
                        verts.Add(c0); verts.Add(c1); verts.Add(c2); verts.Add(c3);
                        for (int i = 0; i < 4; i++) uvs.Add(uv);
                        if (cols != null)
                        {
                            Color32 c32 = px[(th - 1 - py) * tw + pxl];
                            Color col = new Color(c32.r / 255f * rgbMul, c32.g / 255f * rgbMul, c32.b / 255f * rgbMul, c32.a / 255f * alphaMul);
                            for (int i = 0; i < 4; i++) cols.Add(col);
                        }
                        bool flip = Vector3.Dot(Vector3.Cross(c1 - c0, c2 - c0), normal) < 0f;
                        int[] order = flip ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
                        foreach (var o in order) tris.Add(i0 + o);
                    }
            }, swapTB);
            if (mirror)
            {
                for (int i = 0; i < verts.Count; i++) verts[i] = new Vector3(-verts[i].x, verts[i].y, verts[i].z);
                for (int i = 0; i < tris.Count; i += 3) { int t = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = t; }
            }
            return Finish(verts, uvs, tris, cols);
        }

        private static object ToIl2Cpp<T>(List<T> src)
        {
            var l = new Il2CppSystem.Collections.Generic.List<T>();
            foreach (var e in src) l.Add(e);
            return l;
        }
    }
}



