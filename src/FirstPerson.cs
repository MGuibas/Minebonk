using System;
using UnityEngine;

namespace MegabonkSteve
{
    // Minecraft first-person view model, following vanilla's ItemInHandRenderer step by step: item arm transform,
    // 6-tick swing, eat/drink transform, hand height that drops after a hit and recovers with the attack charge,
    // and a real 3D shield. With an empty hand the skin's arm is shown.
    internal class FirstPerson
    {
        private readonly GameObject root;
        private readonly Material baseMat;
        private readonly Material armMat;
        private readonly Transform mainRoot, offRoot;
        private Transform armTf, mainItem, offItem, shieldTf;
        private bool hasMain;
        private float swingStart = -10f;
        private float height = 0f;       // vanilla mainHandHeight, 0..1
        public float targetHeight = 1f;
        public bool sprinting;
        private float bobPhase, bobAmount;
        private readonly float U;       // world units per Minecraft block, corrected for this camera's FOV
        private static Texture2D shieldTex;
        private static Mesh shieldPlate, shieldHandle;

        public FirstPerson(Transform cam, Material mat, Texture2D skin, bool slim)
        {
            baseMat = mat;
            var camera = cam.GetComponent<Camera>();
            float fov = camera != null ? camera.fieldOfView : 70f;
            U = 2.5f * Mathf.Tan(35f * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

            root = new GameObject("MC_ViewModel");
            root.transform.SetParent(cam, false);
            root.layer = cam.gameObject.layer;

            armMat = Keep.It(new Material(mat));
            armMat.mainTexture = skin;
            foreach (var n in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" })
                if (armMat.HasProperty(n)) armMat.SetTexture(n, skin);

            mainRoot = NewRoot("main_root");
            offRoot = NewRoot("off_root");

            int w = slim ? 3 : 4;
            var armGo = new GameObject("arm");
            armGo.transform.SetParent(mainRoot, false);
            armGo.layer = root.layer;
            armGo.transform.localScale = Vector3.one * (U / 16f * 0.75f);
            armGo.transform.localPosition = new Vector3(0.06f, -0.14f, -0.40f) * U;
            armGo.transform.localRotation = Quaternion.Euler(-97f, 0f, 0f);   // tilted a touch upward
            var meshGo = new GameObject("arm_mesh");
            meshGo.transform.SetParent(armGo.transform, false);
            meshGo.layer = root.layer;
            meshGo.transform.localPosition = new Vector3(0f, -6f, 0f);
            meshGo.AddComponent<MeshFilter>().sharedMesh = SteveRig.BoxMesh(w, 12f, 4f, w, 12, 4, 40, 16);
            meshGo.AddComponent<MeshRenderer>().sharedMaterial = armMat;
            armTf = armGo.transform;
        }

        private Transform NewRoot(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.layer = root.layer;
            return go.transform;
        }

        public void SetVisible(bool v) { if (root != null) root.SetActive(v); }

        private Transform BuildItem(Transform parent, Texture2D icon, int side)
        {
            var go = new GameObject("item");
            go.transform.SetParent(parent, false);
            go.layer = root.layer;
            // Vanilla display transform for generated/handheld items in first person:
            // translation (1.13, 3.2, 1.13)/16, rotation (0,-90,25), scale 0.68 (mirrored for Unity's handedness).
            go.transform.localScale = Vector3.one * (0.68f / 16f * U);
            go.transform.localRotation = Quaternion.Euler(0f, 90f * side, 25f * side);
            go.transform.localPosition = new Vector3(1.13f * side, 3.2f, -1.13f) / 16f * U;
            var mat = Keep.It(new Material(baseMat));
            mat.mainTexture = icon;
            foreach (var n in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" })
                if (mat.HasProperty(n)) mat.SetTexture(n, icon);
            go.AddComponent<MeshFilter>().sharedMesh = ItemMesh.Build(icon);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            SetLayerRecursive(go, root.layer);
            return go.transform;
        }

        public void SetHeld(Texture2D icon)
        {
            if (mainItem != null) UnityEngine.Object.Destroy(mainItem.gameObject);
            mainItem = null;
            height = 0f; // new item rises into view
            hasMain = icon != null;
            if (armTf != null) armTf.gameObject.SetActive(!hasMain);
            if (icon != null) mainItem = BuildItem(mainRoot, icon, 1);
        }

        // Swaps the held sprite (bow draw stages) without replaying the equip animation.
        public void SetHeldIcon(Texture2D icon)
        {
            if (mainItem != null) UnityEngine.Object.Destroy(mainItem.gameObject);
            mainItem = null;
            if (icon != null) mainItem = BuildItem(mainRoot, icon, 1);
        }

        // Bow drawn: vanilla's applyBowTransform plus the draw shake.
        public bool bowing;
        public float bowPower;      // 0..1 vanilla draw power
        public float bowTicks;      // ticks since the draw started

        public void SetOffhand(Texture2D icon, bool isShield)
        {
            if (offItem != null) UnityEngine.Object.Destroy(offItem.gameObject);
            offItem = null; shieldTf = null;
            if (isShield) { offItem = BuildShield(offRoot); shieldTf = offItem; }
            else if (icon != null) offItem = BuildItem(offRoot, icon, -1);
        }

        // Real shield: 12x22x1 plate with the wooden face plus the grip, from the vanilla model's cube sizes.
        private Transform BuildShield(Transform parent)
        {
            var go = new GameObject("shield");
            go.transform.SetParent(parent, false);
            go.layer = root.layer;
            if (shieldTex == null) shieldTex = RealShield() ?? PaintShield();
            if (shieldPlate == null)
            {
                shieldPlate = SteveRig.BoxMesh(12f, 22f, 1f, 12, 22, 1, 0, 0);
                shieldHandle = SteveRig.BoxMesh(2f, 6f, 6f, 2, 6, 6, 26, 0);
            }
            var mat = Keep.It(new Material(baseMat));
            mat.mainTexture = shieldTex;
            foreach (var n in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" })
                if (mat.HasProperty(n)) mat.SetTexture(n, shieldTex);
            float s = 0.74f / 16f * U;   // a little bigger
            var plate = new GameObject("plate");
            plate.transform.SetParent(go.transform, false);
            plate.layer = root.layer;
            plate.transform.localScale = Vector3.one * s;
            plate.AddComponent<MeshFilter>().sharedMesh = shieldPlate;
            plate.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var grip = new GameObject("grip");
            grip.transform.SetParent(go.transform, false);
            grip.layer = root.layer;
            grip.transform.localScale = Vector3.one * s;
            grip.transform.localPosition = new Vector3(0f, 0f, -3.5f * s);
            grip.AddComponent<MeshFilter>().sharedMesh = shieldHandle;
            grip.AddComponent<MeshRenderer>().sharedMaterial = mat;
            grip.SetActive(false);   // the handle faces the camera and stays out of sight
            return go.transform;
        }

        internal static Texture2D RealShield() { return McAssets.Tex("entity/shield/shield_base_nopattern"); }

        private static Texture2D PaintShield()
        {
            // Dark-oak style plank face with an iron rim, laid out on the standard 64x64 box UVs.
            var px = new Color32[64 * 64];
            var wood = new Color32[] { new Color32(70, 48, 22, 255), new Color32(82, 58, 28, 255), new Color32(62, 42, 20, 255) };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    px[(63 - y) * 64 + x] = wood[((x / 2) + (y / 5) * 3) % 3];
            Color32 iron = new Color32(150, 150, 156, 255);
            // front face region is (1,1)-(13,23); rim it
            for (int x = 1; x < 13; x++) { px[(63 - 1) * 64 + x] = iron; px[(63 - 22) * 64 + x] = iron; }
            for (int y = 1; y < 23; y++) { px[(63 - y) * 64 + 1] = iron; px[(63 - y) * 64 + 12] = iron; }
            // grip region
            for (int y = 0; y < 12; y++) for (int x = 26; x < 42; x++) px[(63 - y) * 64 + x] = new Color32(110, 80, 40, 255);
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            t.SetPixels32(px); t.Apply();
            t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp;
            return Keep.It(t);
        }

        // Shot fired: a quick kick back and up that eases out.
        private float recoilStart = -10f, recoilPower;
        public void Recoil(float power) { recoilStart = Time.time; recoilPower = power; }

        public void Swing() { swingStart = Time.time; }
        public bool Swinging { get { return Time.time - swingStart < 0.3f; } }

        private static Matrix4x4 T(float x, float y, float z) { return Matrix4x4.Translate(new Vector3(x, y, z)); }
        private static Matrix4x4 R(float x, float y, float z) { return Matrix4x4.Rotate(Quaternion.Euler(x, y, z)); }

        // Vanilla pose for one hand. side = +1 right, -1 left. eatFrac < 0 means not eating.
        private void Pose(Transform t, int side, float swing, float equip, float eatFrac, float eatTicksLeft)
        {
            Matrix4x4 m = Matrix4x4.identity;
            if (bowing && side == 1)
            {
                m = m * T(side * 0.56f, -0.52f - 0.6f * (1f - equip), 0.72f);
                m = m * T(side * -0.2785682f, 0.18344387f, -0.15731531f)
                      * R(13.935f, 0f, 0f) * R(0f, -side * 35.3f, 0f) * R(0f, 0f, side * -9.785f);
                float f12 = bowPower;
                if (f12 > 0.1f)
                {
                    float shake = Mathf.Sin((bowTicks - 0.1f) * 1.3f) * (f12 - 0.1f);
                    m = m * T(0f, shake * 0.004f, 0f);
                }
                m = m * T(0f, 0f, -f12 * 0.04f) * Matrix4x4.Scale(new Vector3(1f, 1f, 1f + f12 * 0.2f)) * R(0f, side * 45f, 0f);
            }
            else if (eatFrac >= 0f)
            {
                float f = eatTicksLeft + 1f;
                if (eatFrac < 0.8f) m = m * T(0f, Mathf.Abs(Mathf.Cos(f / 4f * Mathf.PI) * 0.1f), 0f);
                float f3 = 1f - Mathf.Pow(eatFrac, 27f);
                m = m * T(f3 * 0.6f * side, f3 * -0.5f, 0f)
                      * R(0f, -side * f3 * 90f, 0f) * R(-f3 * 10f, 0f, 0f) * R(0f, 0f, side * f3 * 30f);
                m = m * T(side * 0.56f, -0.52f - 0.6f * (1f - equip), 0.72f);
            }
            else
            {
                float j0 = 0f;
                if (swing > 0f && swing < 1f)
                {
                    float f = -0.4f * Mathf.Sin(Mathf.Sqrt(swing) * Mathf.PI);
                    float g = 0.2f * Mathf.Sin(Mathf.Sqrt(swing) * Mathf.PI * 2f);
                    float h = -0.2f * Mathf.Sin(swing * Mathf.PI);
                    m = m * T(side * f, g, -h);
                    j0 = 1f;
                }
                m = m * T(side * 0.56f, -0.52f - 0.6f * (1f - equip), 0.72f);
                if (j0 > 0f)
                {
                    float i = Mathf.Sin(swing * swing * Mathf.PI);
                    float j = Mathf.Sin(Mathf.Sqrt(swing) * Mathf.PI);
                    m = m * R(0f, -side * (45f - i * 20f), 0f) * R(0f, 0f, side * j * -20f)
                          * R(j * 80f, 0f, 0f) * R(0f, side * 45f, 0f);
                }
            }
            Vector3 p = m.GetColumn(3);
            t.localPosition = p * U;
            t.localRotation = m.rotation;
        }

        public void Tick(bool moving, bool blocking, float eatFrac, float eatTicksLeft)
        {
            if (root == null) return;
            bobAmount = Mathf.MoveTowards(bobAmount, moving ? (sprinting ? 1.8f : 1f) : 0f, Time.deltaTime * 5f);
            bobPhase += Time.deltaTime * (sprinting ? 12f : 9f) * bobAmount;

            // Hand height follows the attack charge: it dips after a swing and climbs back as the bar fills.
            float ticks = Time.deltaTime * 20f;
            height += Mathf.Clamp(targetHeight - height, -0.4f * ticks, 0.4f * ticks);

            float swing = (Time.time - swingStart) < 0.3f ? Mathf.Clamp01((Time.time - swingStart) / 0.3f) : 0f;
            Pose(mainRoot, 1, swing, height, eatFrac, eatTicksLeft);
            float rc = (Time.time - recoilStart) / 0.25f;
            if (rc >= 0f && rc < 1f)
            {
                float k = Mathf.Sin(rc * Mathf.PI) * (0.4f + 0.6f * recoilPower);
                mainRoot.localPosition += new Vector3(0f, 0.06f * k, -0.14f * k) * U;
                mainRoot.localRotation = mainRoot.localRotation * Quaternion.Euler(-9f * k, 0f, 0f);
            }

            Pose(offRoot, -1, 0f, 1f, -1f, 0f);
            if (shieldTf != null)
            {
                // Shield: grip toward the camera, face outward; raised to the middle while blocking.
                Vector3 rest = new Vector3(-0.64f, -1.18f, 0.62f) * U;   // low in the bottom-left corner, close to the camera
                Vector3 up = new Vector3(-0.58f, -1.12f, 0.66f) * U;
                offRoot.localPosition = Vector3.Lerp(offRoot.localPosition, blocking ? up : rest, Time.deltaTime * 14f);
                offRoot.localRotation = Quaternion.Slerp(offRoot.localRotation,
                    Quaternion.Euler(0f, 0f, blocking ? -18f : -4f), Time.deltaTime * 14f);   // turns to the right when blocking
                shieldTf.localPosition = Vector3.zero;
                shieldTf.localRotation = Quaternion.identity;
            }

            Vector3 bob = new Vector3(Mathf.Sin(bobPhase) * 0.02f, -Mathf.Abs(Mathf.Sin(bobPhase)) * 0.03f, 0f) * U * bobAmount;
            mainRoot.localPosition += bob;
            offRoot.localPosition += bob;
        }

        public void Destroy()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++) SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }
    }
}
