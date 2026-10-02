using System;
using System.Collections.Generic;
using System.IO;
using Actors.Enemies;
using Assets.Scripts.Actors.Enemies;
using UnityEngine;
using UnityEngine.UI;

namespace MegabonkSteve
{
    // Admin / test panel (F8): spawn mobs on demand, a few cheats, and the Minecraft-style sound slider.
    // Drawn with the real widget sprites (button, slider, handle) from the user's Minecraft install.
    internal class McAdmin
    {
        private class Btn
        {
            public Rect r;
            public Func<string> text;
            public Action click;
            public Func<bool> active;
            public RawImage l, m, rr;
            public Text label;
        }

        private GameObject canvasGo;
        private CanvasScaler scaler;
        private RectTransform panel;
        private Font font;
        private readonly List<Btn> btns = new List<Btn>();
        private Rect sliderRect;
        private RawImage sliderL, sliderM, sliderR, handle;
        private Text sliderText;
        private bool dragging;
        private Texture2D tBtn, tBtnHi, tBtnOff, tSlider, tSliderHi, tHandle, tHandleHi;

        public bool IsOpen { get; private set; }
        private CursorLockMode savedLock;
        private bool savedVisible;
        public int Qty = 1;
        public bool God;

        private const float W = 250f, H = 339f;

        public static string VolumeFile
        {
            get { return Path.Combine(BepInEx.Paths.ConfigPath, "MegabonkSteve_volume.txt"); }
        }

        public static void LoadVolume()
        {
            try
            {
                if (File.Exists(VolumeFile) && float.TryParse(File.ReadAllText(VolumeFile).Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float v))
                    McSound.Master = Mathf.Clamp01(v);
            }
            catch { }
        }

        private static void SaveVolume()
        {
            try { File.WriteAllText(VolumeFile, McSound.Master.ToString(System.Globalization.CultureInfo.InvariantCulture)); } catch { }
        }

        private static GameObject NewUi(string name, params Type[] comps)
        {
            var arr = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Type>(comps.Length);
            for (int i = 0; i < comps.Length; i++) arr[i] = Il2CppInterop.Runtime.Il2CppType.From(comps[i]);
            return new GameObject(name, arr);
        }

        private RawImage Img(Transform parent, Texture2D tex, float x, float y, float w, float h, Rect uv)
        {
            var go = NewUi("img", typeof(RectTransform), typeof(RawImage));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            var img = go.GetComponent<RawImage>();
            img.texture = tex;
            img.uvRect = uv;
            img.raycastTarget = false;
            return img;
        }

        private Text Label(Transform parent, string s, float x, float y, float w, float h, int size, TextAnchor anchor)
        {
            var go = NewUi("t", typeof(RectTransform), typeof(Text), typeof(Shadow));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            var t = go.GetComponent<Text>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = s;
            var sh = go.GetComponent<Shadow>();
            sh.effectColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            sh.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        // Three-slice widget: 3 px caps and a stretched middle, like the vanilla nine-slice buttons.
        private void ThreeSlice(Transform parent, Texture2D tex, float x, float y, float w, out RawImage l, out RawImage m, out RawImage r)
        {
            float cap = 3f;
            float tw = tex != null ? tex.width : 200f;
            l = Img(parent, tex, x, y, cap, 20f, new Rect(0f, 0f, cap / tw, 1f));
            m = Img(parent, tex, x + cap, y, w - 2 * cap, 20f, new Rect(cap / tw, 0f, 1f - 2 * cap / tw, 1f));
            r = Img(parent, tex, x + w - cap, y, cap, 20f, new Rect(1f - cap / tw, 0f, cap / tw, 1f));
        }

        private Btn AddBtn(float x, float y, float w, Func<string> text, Action click, Func<bool> active = null)
        {
            var b = new Btn { r = new Rect(x, y, w, 20f), text = text, click = click, active = active };
            ThreeSlice(panel, tBtn, x, y, w, out b.l, out b.m, out b.rr);
            b.label = Label(panel, text(), x, y + 5f, w, 12f, 8, TextAnchor.MiddleCenter);
            btns.Add(b);
            return b;
        }

        private void Build()
        {
            canvasGo = new GameObject("MC_ADMIN");
            UnityEngine.Object.DontDestroyOnLoad(canvasGo);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6100;
            scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (font == null) { try { font = Font.CreateDynamicFontFromOSFont("Consolas", 16); } catch { } }

            string w = "gui/sprites/widget/";
            tBtn = McAssets.Tex(w + "button"); tBtnHi = McAssets.Tex(w + "button_highlighted"); tBtnOff = McAssets.Tex(w + "button_disabled");
            tSlider = McAssets.Tex(w + "slider"); tSliderHi = McAssets.Tex(w + "slider_highlighted");
            tHandle = McAssets.Tex(w + "slider_handle"); tHandleHi = McAssets.Tex(w + "slider_handle_highlighted");

            var bg = NewUi("panel", typeof(RectTransform), typeof(RawImage));
            panel = bg.GetComponent<RectTransform>();
            panel.SetParent(canvasGo.transform, false);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(W, H);
            var bgImg = bg.GetComponent<RawImage>();
            bgImg.texture = Texture2D.whiteTexture;
            bgImg.color = new Color(0.05f, 0.05f, 0.05f, 0.82f);
            bgImg.raycastTarget = false;

            Label(panel, "Admin Panel", 0f, 6f, W, 12f, 9, TextAnchor.MiddleCenter);

            // Sound slider (vanilla options style)
            sliderRect = new Rect(25f, 22f, 200f, 20f);
            ThreeSlice(panel, tSlider, sliderRect.x, sliderRect.y, sliderRect.width, out sliderL, out sliderM, out sliderR);
            handle = Img(panel, tHandle, sliderRect.x, sliderRect.y, 8f, 20f, new Rect(0f, 0f, 1f, 1f));
            sliderText = Label(panel, "", sliderRect.x, sliderRect.y + 5f, sliderRect.width, 12f, 8, TextAnchor.MiddleCenter);

            // Quantity
            Label(panel, "Spawn amount", 25f, 50f, 100f, 10f, 7, TextAnchor.MiddleLeft);
            int[] qs = { 1, 5, 10, 25 };
            for (int i = 0; i < qs.Length; i++)
            {
                int q = qs[i];
                AddBtn(25f + i * 51f, 60f, 47f, () => (Qty == q ? "> x" + q + " <" : "x" + q), () => { Qty = q; }, () => Qty == q);
            }

            // Mobs
            var mobs = new[]
            {
                new KeyValuePair<string, EEnemy>("Zombie", EEnemy.Zombie), new KeyValuePair<string, EEnemy>("Husk", EEnemy.GoblinStrong),
                new KeyValuePair<string, EEnemy>("Drowned", EEnemy.GoblinTank), new KeyValuePair<string, EEnemy>("Z. Villager", EEnemy.Ghoul),
                new KeyValuePair<string, EEnemy>("Skeleton", EEnemy.Skeleton), new KeyValuePair<string, EEnemy>("Stray", EEnemy.SkeletonDusty),
                new KeyValuePair<string, EEnemy>("Wither Skel.", EEnemy.Pharaoh1), new KeyValuePair<string, EEnemy>("Pillager", EEnemy.Bandit),
                new KeyValuePair<string, EEnemy>("Creeper", EEnemy.BoomerSpider), new KeyValuePair<string, EEnemy>("Spider", EEnemy.Scorpion),
                new KeyValuePair<string, EEnemy>("Slime", EEnemy.Slime), new KeyValuePair<string, EEnemy>("Bee", EEnemy.Bee),
                new KeyValuePair<string, EEnemy>("Pig", EEnemy.MinibossPig), new KeyValuePair<string, EEnemy>("Cow", EEnemy.FrogBlue), new KeyValuePair<string, EEnemy>("Iron Golem", EEnemy.Pharaoh2),
            };
            Label(panel, "Mobs", 25f, 88f, 100f, 10f, 7, TextAnchor.MiddleLeft);
            for (int i = 0; i < 15; i++)
            {
                var m = mobs[i];
                float x = 25f + (i % 3) * 68f, y = 98f + (i / 3) * 23f;
                AddBtn(x, y, 64f, () => m.Key, () => { var mm = MinecraftMode.Instance; if (mm != null) mm.AdminSpawn(m.Value, Qty); });
            }

            // Actions
            float ay = 98f + 5 * 23f + 4f;
            AddBtn(25f, ay, 98f, () => "Kill all", () => { var mm = MinecraftMode.Instance; if (mm != null) mm.AdminKillAll(); });
            AddBtn(127f, ay, 98f, () => "Heal", () => { var mm = MinecraftMode.Instance; if (mm != null) mm.AdminHeal(); });
            AddBtn(25f, ay + 23f, 98f, () => "+1 Level", () => { var mm = MinecraftMode.Instance; if (mm != null) mm.AdminLevel(); });
            AddBtn(127f, ay + 23f, 98f, () => God ? "God mode: ON" : "God mode: OFF", () => { God = !God; }, () => God);

            // Bosses: the flag decides the model (Wither = stage boss, Guardian = miniboss, Dragon = final boss).
            float by = ay + 62f;
            Label(panel, "Bosses", 25f, by - 10f, 100f, 10f, 7, TextAnchor.MiddleLeft);
            AddBtn(25f, by, 47f, () => "Wither", () => { var mm = MinecraftMode.Instance; if (mm != null) mm.AdminSpawnBoss(EEnemy.Pharaoh1, EEnemyFlag.StageBoss); });
            AddBtn(76f, by, 47f, () => "Guard.", () => { var mm = MinecraftMode.Instance; if (mm != null) mm.AdminSpawnBoss(EEnemy.MinibossGolem, EEnemyFlag.Boss); });
            AddBtn(127f, by, 47f, () => "Dragon", () => { var mm = MinecraftMode.Instance; if (mm != null) mm.AdminSpawnBoss(EEnemy.GhostKing, EEnemyFlag.FinalBoss); });
            AddBtn(178f, by, 47f, () => "Warden", () => { var mm = MinecraftMode.Instance; if (mm != null) mm.AdminSpawnBoss(EEnemy.MinibossGolem, EEnemyFlag.SummonerMiniboss); });
            AddBtn(25f, by + 24f, 200f, () => "Full kit (old gear)", () => { var mm = MinecraftMode.Instance; if (mm != null) mm.GiveFullKit(); });

            canvasGo.SetActive(false);
        }

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        public void Open()
        {
            if (IsOpen) return;
            if (canvasGo == null) Build();
            IsOpen = true;
            savedLock = Cursor.lockState;
            savedVisible = Cursor.visible;
            canvasGo.SetActive(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            dragging = false;
            SaveVolume();
            Cursor.lockState = savedLock;
            Cursor.visible = savedVisible;
            canvasGo.SetActive(false);
        }

        public void Tick()
        {
            if (!IsOpen) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            float gs = Mathf.Max(2f, Mathf.Round(Screen.height / 360f));
            scaler.scaleFactor = gs;

            Vector2 mouse = Input.mousePosition;
            float left = Screen.width / 2f - W / 2f * gs;
            float top = Screen.height / 2f + H / 2f * gs;
            float gx = (mouse.x - left) / gs, gy = (top - mouse.y) / gs;
            bool down = Input.GetMouseButtonDown(0);

            // Slider
            bool overSlider = sliderRect.Contains(new Vector2(gx, gy));
            if (down && overSlider) dragging = true;
            if (dragging && !Input.GetMouseButton(0)) { dragging = false; SaveVolume(); McSound.Play("entity.experience_orb.pickup", 0.6f); }
            if (dragging) McSound.Master = Mathf.Clamp01((gx - sliderRect.x - 4f) / (sliderRect.width - 8f));
            float hx = sliderRect.x + McSound.Master * (sliderRect.width - 8f);
            handle.rectTransform.anchoredPosition = new Vector2(hx, -sliderRect.y);
            bool hi = overSlider || dragging;
            handle.texture = hi ? tHandleHi : tHandle;
            var st = hi ? tSliderHi : tSlider;
            sliderL.texture = sliderM.texture = sliderR.texture = st;
            sliderText.text = "Sound: " + Mathf.RoundToInt(McSound.Master * 100f) + "%";

            // Buttons
            foreach (var b in btns)
            {
                bool over = b.r.Contains(new Vector2(gx, gy));
                bool act = b.active != null && b.active();
                var tex = over ? tBtnHi : tBtn;
                b.l.texture = b.m.texture = b.rr.texture = tex;
                b.label.text = b.text();
                b.label.color = act ? new Color(1f, 1f, 0.55f) : (over ? new Color(1f, 1f, 0.6f) : Color.white);
                if (over && down) { b.click(); McSound.Play("entity.experience_orb.pickup", 0.5f, 1.4f); }
            }
        }

        public void Destroy()
        {
            if (IsOpen) { Cursor.lockState = savedLock; Cursor.visible = savedVisible; }
            if (canvasGo != null) UnityEngine.Object.Destroy(canvasGo);
        }
    }
}

