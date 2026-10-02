using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace MegabonkSteve
{
    // Every skin that can be found: the ones saved in the Minecraft launcher, the vanilla default skins, and any png dropped
    // into BepInEx\config\MegabonkSteve_skins. The choice is remembered between runs.
    internal static class McSkins
    {
        public class Entry
        {
            public string key, name;
            public bool slim;
            public Func<Texture2D> load;
            private Texture2D tex;
            public Texture2D Tex { get { if (tex == null) { try { tex = load(); } catch (Exception e) { Plugin.Logger.LogWarning("skin " + name + ": " + e.Message); } } return tex; } }
        }

        private static string ChoiceFile { get { return Path.Combine(BepInEx.Paths.ConfigPath, "MegabonkSteve_skin.txt"); } }
        public static string SkinFolder { get { return Path.Combine(BepInEx.Paths.ConfigPath, "MegabonkSteve_skins"); } }

        public static string Choice
        {
            get { try { return File.Exists(ChoiceFile) ? File.ReadAllText(ChoiceFile).Trim() : ""; } catch { return ""; } }
            set { try { File.WriteAllText(ChoiceFile, value ?? ""); } catch { } }
        }

        public static List<Entry> All()
        {
            var list = new List<Entry>();
            // Launcher skins
            try
            {
                string f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "launcher_custom_skins.json");
                if (File.Exists(f))
                {
                    using (var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(f)))
                    {
                        System.Text.Json.JsonElement cs;
                        if (doc.RootElement.TryGetProperty("customSkins", out cs))
                            foreach (var p in cs.EnumerateObject())
                            {
                                var o = p.Value;
                                System.Text.Json.JsonElement img, nm, sl;
                                if (!o.TryGetProperty("skinImage", out img)) continue;
                                string data = img.GetString() ?? "";
                                int c = data.IndexOf("base64,", StringComparison.Ordinal);
                                if (c < 0) continue;
                                string b64 = data.Substring(c + 7);
                                bool slim = o.TryGetProperty("slim", out sl) && sl.ValueKind == System.Text.Json.JsonValueKind.True;
                                string name = o.TryGetProperty("name", out nm) && !string.IsNullOrEmpty(nm.GetString()) ? nm.GetString() : p.Name;
                                list.Add(new Entry { key = "launcher:" + p.Name, name = name, slim = slim, load = () => Check(McAssets.FromPng(Convert.FromBase64String(b64))) });
                            }
                    }
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("launcher skins: " + e.Message); }

            // Vanilla defaults
            string[] names = { "steve", "alex", "ari", "efe", "kai", "makena", "noor", "sunny", "zuri" };
            bool[] slims = { false, true, false, true, false, true, true, false, false };
            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i]; bool sl = slims[i];
                list.Add(new Entry { key = "default:" + n, name = char.ToUpper(n[0]) + n.Substring(1), slim = sl, load = () => McAssets.Tex("entity/player/" + (sl ? "slim/" : "wide/") + n) });
            }

            // Dropped-in files: name_slim.png marks the slim arms
            try
            {
                if (Directory.Exists(SkinFolder))
                    foreach (var file in Directory.GetFiles(SkinFolder, "*.png"))
                    {
                        string path = file, bn = Path.GetFileNameWithoutExtension(file);
                        bool slim = bn.EndsWith("_slim", StringComparison.OrdinalIgnoreCase);
                        list.Add(new Entry { key = "file:" + Path.GetFileName(file), name = slim ? bn.Substring(0, bn.Length - 5) : bn, slim = slim, load = () => Check(McAssets.FromPng(File.ReadAllBytes(path))) });
                    }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("skin folder: " + e.Message); }
            return list;
        }

        private static Texture2D Check(Texture2D t) { return t != null && t.width == 64 && t.height == 64 ? t : null; }

        // The skin the character wears: the saved choice, else null (the caller falls back to the launcher skin, then Steve).
        public static Texture2D Selected(out bool slim)
        {
            slim = false;
            string key = Choice;
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var e in All())
                if (e.key == key) { var t = e.Tex; if (t != null) { slim = e.slim; return t; } }
            return null;
        }

        // Rebuilds every Steve model in the scene (the one in the run, or the preview on the character screen) with the new skin.
        public static void ApplyLive()
        {
            try
            {
                foreach (var rig in UnityEngine.Object.FindObjectsOfType<SteveRig>())
                {
                    var pr = rig.playerRenderer;
                    if (pr == null) continue;
                    rig.gameObject.name = "SteveRig_old";
                    rig.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(rig.gameObject);
                    var fresh = SteveRig.Attach(pr);
                    var mm = MinecraftMode.Instance;
                    if (fresh != null && mm != null && mm.playerRenderer == pr) mm.SwapRig(fresh);
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("apply skin: " + e.Message); }
        }
    }

    // F9: the skin picker, available on the character screen and during a run.
    public class SkinPicker : MonoBehaviour
    {
        public SkinPicker(IntPtr ptr) : base(ptr) { }

        public static bool IsOpen;

        private class Btn { public Rect r; public Func<string> text; public Action click; public Func<bool> active; public RawImage l, m, rr; public Text label; }

        private GameObject canvasGo;
        private CanvasScaler scaler;
        private RectTransform panel;
        private Font font;
        private readonly List<Btn> btns = new List<Btn>();
        private Texture2D tBtn, tBtnHi;
        private float W = 270f, H = 100f;
        private CursorLockMode savedLock;
        private bool savedVisible;
        private string current = "";
        private Text hint;

        private static GameObject NewUi(string name, params Type[] comps)
        {
            var arr = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Type>(comps.Length);
            for (int i = 0; i < comps.Length; i++) arr[i] = Il2CppInterop.Runtime.Il2CppType.From(comps[i]);
            return new GameObject(name, arr);
        }

        private RawImage Img(Transform parent, Texture tex, float x, float y, float w, float h, Rect uv)
        {
            var go = NewUi("img", typeof(RectTransform), typeof(RawImage));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            var img = go.GetComponent<RawImage>();
            img.texture = tex; img.uvRect = uv; img.raycastTarget = false;
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
            t.fontSize = size; t.alignment = anchor; t.color = Color.white; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = s;
            var sh = go.GetComponent<Shadow>();
            sh.effectColor = new Color(0.12f, 0.12f, 0.12f, 1f); sh.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        private void Build()
        {
            if (canvasGo != null) UnityEngine.Object.Destroy(canvasGo);
            btns.Clear();
            McAssets.Init();
            var entries = McSkins.All();
            if (entries.Count > 24) entries.RemoveRange(24, entries.Count - 24);
            int rows = (entries.Count + 1) / 2;
            H = 40f + rows * 23f + 22f;

            canvasGo = new GameObject("MC_SKINS");
            UnityEngine.Object.DontDestroyOnLoad(canvasGo);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6200;
            scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (font == null) { try { font = Font.CreateDynamicFontFromOSFont("Consolas", 16); } catch { } }
            string w = "gui/sprites/widget/";
            tBtn = McAssets.Tex(w + "button"); tBtnHi = McAssets.Tex(w + "button_highlighted");

            var bg = NewUi("panel", typeof(RectTransform), typeof(RawImage));
            panel = bg.GetComponent<RectTransform>();
            panel.SetParent(canvasGo.transform, false);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(W, H);
            var bgImg = bg.GetComponent<RawImage>();
            bgImg.texture = Texture2D.whiteTexture; bgImg.color = new Color(0.05f, 0.05f, 0.05f, 0.88f); bgImg.raycastTarget = false;

            Label(panel, "Skins (F9 to close)", 0f, 6f, W, 12f, 9, TextAnchor.MiddleCenter);
            current = McSkins.Choice;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                float x = 10f + (i % 2) * 128f, y = 24f + (i / 2) * 23f;
                var b = new Btn { r = new Rect(x, y, 124f, 20f), text = () => e.name, active = () => current == e.key };
                float cap = 3f, tw = tBtn != null ? tBtn.width : 200f;
                b.l = Img(panel, tBtn, x, y, cap, 20f, new Rect(0f, 0f, cap / tw, 1f));
                b.m = Img(panel, tBtn, x + cap, y, 124f - 2 * cap, 20f, new Rect(cap / tw, 0f, 1f - 2 * cap / tw, 1f));
                b.rr = Img(panel, tBtn, x + 124f - cap, y, cap, 20f, new Rect(1f - cap / tw, 0f, cap / tw, 1f));
                var t = e.Tex;
                if (t != null)
                {
                    Img(panel, t, x + 4f, y + 2f, 16f, 16f, new Rect(8f / 64f, 48f / 64f, 8f / 64f, 8f / 64f));    // face
                    Img(panel, t, x + 4f, y + 2f, 16f, 16f, new Rect(40f / 64f, 48f / 64f, 8f / 64f, 8f / 64f));   // hat layer
                }
                b.label = Label(panel, e.name + (e.slim ? " (slim)" : ""), x + 22f, y + 5f, 100f, 12f, 7, TextAnchor.MiddleLeft);
                b.click = () => { current = e.key; McSkins.Choice = e.key; McSkins.ApplyLive(); McSound.Play("entity.experience_orb.pickup", 0.5f, 1.4f); };
                btns.Add(b);
            }
            hint = Label(panel, "Your own: put a 64x64 png in BepInEx/config/MegabonkSteve_skins", 0f, H - 14f, W, 10f, 6, TextAnchor.MiddleCenter);
            hint.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            canvasGo.SetActive(false);
        }

        private void Open()
        {
            Build();
            IsOpen = true;
            savedLock = Cursor.lockState; savedVisible = Cursor.visible;
            canvasGo.SetActive(true);
        }

        private void Close()
        {
            IsOpen = false;
            Cursor.lockState = savedLock; Cursor.visible = savedVisible;
            if (canvasGo != null) canvasGo.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9)) { if (IsOpen) Close(); else Open(); }
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            float gs = Mathf.Max(2f, Mathf.Round(Screen.height / 360f));
            scaler.scaleFactor = gs;
            Vector2 mouse = Input.mousePosition;
            float left = Screen.width / 2f - W / 2f * gs, top = Screen.height / 2f + H / 2f * gs;
            float gx = (mouse.x - left) / gs, gy = (top - mouse.y) / gs;
            bool down = Input.GetMouseButtonDown(0);
            foreach (var b in btns)
            {
                bool over = b.r.Contains(new Vector2(gx, gy));
                bool act = b.active != null && b.active();
                b.l.texture = b.m.texture = b.rr.texture = over ? tBtnHi : tBtn;
                b.label.color = act ? new Color(1f, 1f, 0.45f) : (over ? new Color(1f, 1f, 0.7f) : Color.white);
                if (over && down) b.click();
            }
        }
    }
}
