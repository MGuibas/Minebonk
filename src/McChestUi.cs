using System;
using UnityEngine;
using UnityEngine.UI;

namespace MegabonkSteve
{
    // The chest screen: 27 chest slots above the player's inventory, drawn from the vanilla generic container texture.
    // Click to pick up, right click for half or one, shift-click to send a stack to the other side.
    internal class McChestUi
    {
        private readonly Inv inv;
        private readonly Action onChanged;
        private readonly Func<ItemDef, int, bool> give;
        private GameObject canvasGo;
        private CanvasScaler scaler;
        private RectTransform panel;
        private Font font;
        private readonly RawImage[] icons = new RawImage[27 + 36];
        private readonly Text[] counts = new Text[27 + 36];
        private RawImage cursorIcon, tipBg;
        private Text cursorCount, tipText, title;
        private McChest chest;
        private ItemDef curDef;
        private int curCnt;
        private float savedTimeScale = 1f;
        private CursorLockMode savedLock;
        private bool savedVisible;

        public bool IsOpen { get; private set; }

        private const float W = 176f, H = 167f;

        public McChestUi(Inv inv, Action onChanged, Func<ItemDef, int, bool> give)
        {
            this.inv = inv; this.onChanged = onChanged; this.give = give;
        }

        private static GameObject NewUi(string name, params Type[] comps)
        {
            var arr = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Type>(comps.Length);
            for (int i = 0; i < comps.Length; i++) arr[i] = Il2CppInterop.Runtime.Il2CppType.From(comps[i]);
            return new GameObject(name, arr);
        }

        // Unified slot ids: 0-35 player inventory (hotbar first), 100-126 chest.
        private static Vector2 SlotPos(int id)
        {
            if (id >= 100) { int i = id - 100; return new Vector2(8f + 18f * (i % 9), 18f + 18f * (i / 9)); }
            if (id < 9) return new Vector2(8f + 18f * id, 143f);
            int j = id - 9;
            return new Vector2(8f + 18f * (j % 9), 85f + 18f * (j / 9));
        }

        private ItemDef D(int id) { return id >= 100 ? chest.defs[id - 100] : inv.defs[id]; }
        private int C(int id) { return id >= 100 ? chest.cnts[id - 100] : inv.cnts[id]; }
        private void Put(int id, ItemDef d, int n)
        {
            if (n <= 0) d = null;
            if (id >= 100) { chest.defs[id - 100] = d; chest.cnts[id - 100] = d == null ? 0 : n; }
            else inv.Set(id, d, n);
        }

        private void Build()
        {
            canvasGo = new GameObject("MC_CHEST_UI");
            UnityEngine.Object.DontDestroyOnLoad(canvasGo);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6050;
            scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (font == null) { try { font = Font.CreateDynamicFontFromOSFont("Consolas", 16); } catch { } }

            var dim = NewUi("dim", typeof(RectTransform), typeof(RawImage));
            var drt = dim.GetComponent<RectTransform>();
            drt.SetParent(canvasGo.transform, false);
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            var di = dim.GetComponent<RawImage>(); di.texture = Texture2D.whiteTexture; di.color = new Color(0f, 0f, 0f, 0.55f); di.raycastTarget = false;

            var pg = NewUi("panel", typeof(RectTransform));
            panel = pg.GetComponent<RectTransform>();
            panel.SetParent(canvasGo.transform, false);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(W, H);

            var tex = McAssets.Tex("gui/container/generic_54");
            Piece(tex, 0f, 0f, 176f, 71f, 0f, 71f);       // header + three rows
            Piece(tex, 0f, 71f, 176f, 96f, 126f, 96f);    // player inventory part

            title = MakeText(panel, 8, TextAnchor.MiddleLeft, new Vector2(120f, 10f), new Vector2(8f, -7f), new Vector2(0f, 1f));
            title.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            title.text = "Chest";

            for (int k = 0; k < icons.Length; k++)
            {
                int id = k < 27 ? 100 + k : k - 27;
                Vector2 p = SlotPos(id);
                var g = NewUi("slot" + k, typeof(RectTransform), typeof(RawImage));
                var rt = g.GetComponent<RectTransform>();
                rt.SetParent(panel, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(p.x, -p.y);
                rt.sizeDelta = new Vector2(16f, 16f);
                icons[k] = g.GetComponent<RawImage>();
                icons[k].raycastTarget = false;
                icons[k].enabled = false;
                counts[k] = MakeText(rt, 7, TextAnchor.LowerRight, new Vector2(16f, 9f), Vector2.zero, new Vector2(1f, 0f));
            }

            var cg = NewUi("cursor", typeof(RectTransform), typeof(RawImage));
            var crt = cg.GetComponent<RectTransform>();
            crt.SetParent(canvasGo.transform, false);
            crt.anchorMin = crt.anchorMax = Vector2.zero;
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(16f, 16f);
            cursorIcon = cg.GetComponent<RawImage>(); cursorIcon.raycastTarget = false; cursorIcon.enabled = false;
            cursorCount = MakeText(crt, 7, TextAnchor.LowerRight, new Vector2(16f, 9f), Vector2.zero, new Vector2(1f, 0f));

            var tg = NewUi("tip", typeof(RectTransform), typeof(RawImage));
            var trt = tg.GetComponent<RectTransform>();
            trt.SetParent(canvasGo.transform, false);
            trt.anchorMin = trt.anchorMax = Vector2.zero;
            trt.pivot = Vector2.zero;
            tipBg = tg.GetComponent<RawImage>();
            tipBg.texture = Texture2D.whiteTexture; tipBg.color = new Color(0.07f, 0f, 0.07f, 0.92f); tipBg.raycastTarget = false; tipBg.enabled = false;
            tipText = MakeText(trt, 8, TextAnchor.MiddleLeft, new Vector2(150f, 12f), new Vector2(4f, 0f), Vector2.zero);

            canvasGo.SetActive(false);
        }

        private void Piece(Texture2D tex, float x, float y, float w, float h, float srcY, float srcH)
        {
            var g = NewUi("bg", typeof(RectTransform), typeof(RawImage));
            var rt = g.GetComponent<RectTransform>();
            rt.SetParent(panel, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            var img = g.GetComponent<RawImage>();
            img.texture = tex;
            img.uvRect = new Rect(0f, 1f - (srcY + srcH) / 256f, w / 256f, srcH / 256f);
            img.raycastTarget = false;
        }

        private Text MakeText(RectTransform parent, int size, TextAnchor anchor, Vector2 sz, Vector2 pos, Vector2 anchorMax)
        {
            var go = NewUi("t", typeof(RectTransform), typeof(Text), typeof(Shadow));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchorMax;
            rt.pivot = anchor == TextAnchor.LowerRight ? new Vector2(1f, 0f) : new Vector2(0f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sz;
            var t = go.GetComponent<Text>();
            if (font != null) t.font = font;
            t.fontSize = size; t.alignment = anchor; t.color = Color.white; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = go.GetComponent<Shadow>();
            sh.effectColor = new Color(0.1f, 0.1f, 0.1f, 1f); sh.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        public void Open(McChest c)
        {
            if (IsOpen || c == null) return;
            if (canvasGo == null) Build();
            chest = c;
            IsOpen = true;
            savedTimeScale = Time.timeScale;
            savedLock = Cursor.lockState;
            savedVisible = Cursor.visible;
            Time.timeScale = 0f;
            canvasGo.SetActive(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (curDef != null)
            {
                // The stack in the hand goes back to the inventory (or into the chest if there is no room).
                if (!give(curDef, curCnt)) for (int i = 0; i < 27 && curCnt > 0; i++) if (chest.defs[i] == null) { Put(100 + i, curDef, curCnt); curCnt = 0; }
                curDef = null; curCnt = 0;
            }
            Time.timeScale = savedTimeScale <= 0f ? 1f : savedTimeScale;
            Cursor.lockState = savedLock;
            Cursor.visible = savedVisible;
            canvasGo.SetActive(false);
            McSound.Play("block.chest.close", 1f, 0.9f + UnityEngine.Random.value * 0.1f);
            chest = null;
            onChanged();
        }

        private int Hit(Vector2 mouse, float gs)
        {
            float left = Screen.width / 2f - W / 2f * gs;
            float top = Screen.height / 2f + H / 2f * gs;
            float gx = (mouse.x - left) / gs, gy = (top - mouse.y) / gs;
            for (int id = 0; id < 36; id++) { Vector2 p = SlotPos(id); if (gx >= p.x - 1 && gx < p.x + 17 && gy >= p.y - 1 && gy < p.y + 17) return id; }
            for (int i = 0; i < 27; i++) { Vector2 p = SlotPos(100 + i); if (gx >= p.x - 1 && gx < p.x + 17 && gy >= p.y - 1 && gy < p.y + 17) return 100 + i; }
            return -1;
        }

        private void Click(int id, bool right)
        {
            var sd = D(id); int sc = C(id);
            if (curDef == null)
            {
                if (sd == null) return;
                if (right && sc > 1) { int take = (sc + 1) / 2; curDef = sd; curCnt = take; Put(id, sd, sc - take); }
                else { curDef = sd; curCnt = sc; Put(id, null, 0); }
            }
            else
            {
                if (id < 36 && id >= 36) return;
                if (sd == null) { int put = right ? 1 : curCnt; Put(id, curDef, put); curCnt -= put; }
                else if (sd == curDef && sd.maxStack > 1)
                {
                    int put = right ? 1 : Mathf.Min(curCnt, sd.maxStack - sc);
                    if (sc + put > sd.maxStack) put = sd.maxStack - sc;
                    Put(id, sd, sc + put); curCnt -= put;
                }
                else if (!right) { var od = sd; int oc = sc; Put(id, curDef, curCnt); curDef = od; curCnt = oc; }
                if (curCnt <= 0) { curDef = null; curCnt = 0; }
            }
            onChanged();
        }

        // Shift-click: chest -> inventory, inventory -> chest.
        private void QuickMove(int id)
        {
            var d = D(id); if (d == null) return;
            int left = C(id);
            if (id >= 100) { left = Spread(d, left, 0, 35, false); }
            else { left = Spread(d, left, 0, 26, true); }
            Put(id, left > 0 ? d : null, left);
            onChanged();
        }

        private int Spread(ItemDef d, int count, int from, int to, bool toChest)
        {
            for (int pass = 0; pass < 2 && count > 0; pass++)
                for (int i = from; i <= to && count > 0; i++)
                {
                    int id = toChest ? 100 + i : i;
                    var cur = D(id);
                    if (pass == 0 && cur == d && d.maxStack > 1 && C(id) < d.maxStack)
                    { int put = Mathf.Min(count, d.maxStack - C(id)); Put(id, d, C(id) + put); count -= put; }
                    else if (pass == 1 && cur == null)
                    { int put = Mathf.Min(count, Mathf.Max(1, d.maxStack)); Put(id, d, put); count -= put; }
                }
            return count;
        }

        public void Tick()
        {
            if (!IsOpen) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            float gs = Mathf.Max(2f, Mathf.Round(Screen.height / 360f));
            scaler.scaleFactor = gs;
            Vector2 mouse = Input.mousePosition;
            int hover = Hit(mouse, gs);
            if (hover >= 0)
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (Input.GetMouseButtonDown(0)) { if (shift && curDef == null) QuickMove(hover); else Click(hover, false); }
                else if (Input.GetMouseButtonDown(1)) Click(hover, true);
            }
            for (int k = 0; k < icons.Length; k++)
            {
                int id = k < 27 ? 100 + k : k - 27;
                var d = D(id);
                icons[k].texture = d != null ? d.icon : null;
                icons[k].enabled = d != null && d.icon != null;
                counts[k].text = d != null && C(id) > 1 ? C(id).ToString() : "";
            }
            var crt = cursorIcon.rectTransform;
            crt.anchoredPosition = mouse / gs;
            cursorIcon.texture = curDef != null ? curDef.icon : null;
            cursorIcon.enabled = curDef != null && curDef.icon != null;
            cursorCount.text = curDef != null && curCnt > 1 ? curCnt.ToString() : "";

            bool tip = hover >= 0 && D(hover) != null && curDef == null;
            tipBg.enabled = tip;
            tipText.text = tip ? D(hover).name : "";
            if (tip)
            {
                var trt = tipBg.rectTransform;
                trt.sizeDelta = new Vector2(D(hover).name.Length * 4.6f + 8f, 12f);
                trt.anchoredPosition = mouse / gs + new Vector2(8f, 6f);
            }
        }

        public void Destroy()
        {
            if (IsOpen) { Time.timeScale = savedTimeScale <= 0f ? 1f : savedTimeScale; Cursor.lockState = savedLock; Cursor.visible = savedVisible; }
            if (canvasGo != null) UnityEngine.Object.Destroy(canvasGo);
        }
    }
}
