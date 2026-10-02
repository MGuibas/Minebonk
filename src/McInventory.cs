using System;
using UnityEngine;
using UnityEngine.UI;

namespace MegabonkSteve
{
    // Item storage shared by the hotbar, the survival inventory, armor and offhand.
    // 0-8 hotbar, 9-35 main inventory, 36-39 armor (helmet, chest, legs, boots), 40 offhand.
    public class Inv
    {
        public const int Count = 41;
        public const int Offhand = 40;
        public ItemDef[] defs = new ItemDef[Count];
        public int[] cnts = new int[Count];

        public void Set(int i, ItemDef d, int n) { defs[i] = d; cnts[i] = d == null ? 0 : n; }
    }

    // The vanilla survival inventory screen, drawn from the real container texture of the user's install.
    internal class McInventory
    {
        private readonly Inv inv;
        private readonly Action onChanged;
        private GameObject canvasGo;
        private CanvasScaler scaler;
        private RectTransform panel;
        private readonly RawImage[] icons = new RawImage[Inv.Count];
        private readonly Text[] counts = new Text[Inv.Count];
        private RawImage cursorIcon, tipBg;
        private Text cursorCount, tipText;
        private Font font;

        private ItemDef curDef;
        private int curCnt;
        public bool IsOpen { get; private set; }
        private float savedTimeScale = 1f;
        private CursorLockMode savedLock;
        private bool savedVisible;

        private const float W = 176f, H = 166f;

        public McInventory(Inv inv, Action onChanged)
        {
            this.inv = inv;
            this.onChanged = onChanged;
        }

        private static GameObject NewUi(string name, params Type[] comps)
        {
            var arr = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Type>(comps.Length);
            for (int i = 0; i < comps.Length; i++) arr[i] = Il2CppInterop.Runtime.Il2CppType.From(comps[i]);
            return new GameObject(name, arr);
        }

        // Top-left of the 16x16 item area of a slot, in GUI pixels inside the 176x166 panel.
        private static Vector2 SlotPos(int i)
        {
            if (i < 9) return new Vector2(8f + 18f * i, 142f);
            if (i < 36) { int j = i - 9; return new Vector2(8f + 18f * (j % 9), 84f + 18f * (j / 9)); }
            if (i < 40) return new Vector2(8f, 8f + 18f * (i - 36));
            return new Vector2(77f, 62f);
        }

        private static bool Accepts(int slot, ItemDef d)
        {
            if (d == null) return true;
            if (slot >= 36 && slot < 40)
            {
                int a = slot - 36;
                if (d.kind == ItemKind.Armor) return d.armorSlot == a;
                return a == 1 && d.kind == ItemKind.Elytra;
            }
            return true;
        }

        private void Build()
        {
            canvasGo = new GameObject("MC_INVENTORY");
            UnityEngine.Object.DontDestroyOnLoad(canvasGo);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6000;
            scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (font == null) { try { font = Font.CreateDynamicFontFromOSFont("Consolas", 16); } catch { } }

            // Dimmed background.
            var dim = NewUi("dim", typeof(RectTransform), typeof(RawImage));
            var drt = dim.GetComponent<RectTransform>();
            drt.SetParent(canvasGo.transform, false);
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            var dimImg = dim.GetComponent<RawImage>();
            dimImg.texture = Texture2D.whiteTexture;
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            dimImg.raycastTarget = false;

            var bg = NewUi("panel", typeof(RectTransform), typeof(RawImage));
            panel = bg.GetComponent<RectTransform>();
            panel.SetParent(canvasGo.transform, false);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(W, H);
            var bgImg = bg.GetComponent<RawImage>();
            bgImg.texture = McAssets.Tex("gui/container/inventory");
            bgImg.uvRect = new Rect(0f, 1f - H / 256f, W / 256f, H / 256f);
            bgImg.raycastTarget = false;

            // Steve's front view in the player box, composed from the skin (base layer plus overlay layer).
            var front = BuildFront();
            if (front != null)
            {
                var pg = NewUi("player", typeof(RectTransform), typeof(RawImage));
                var prt = pg.GetComponent<RectTransform>();
                prt.SetParent(panel, false);
                prt.anchorMin = prt.anchorMax = new Vector2(0f, 1f);
                prt.pivot = new Vector2(0.5f, 1f);
                prt.anchoredPosition = new Vector2(51f, -14f);
                prt.sizeDelta = new Vector2(32f, 64f);
                var pimg = pg.GetComponent<RawImage>();
                pimg.texture = front;
                pimg.raycastTarget = false;
            }

            for (int i = 0; i < Inv.Count; i++)
            {
                Vector2 p = SlotPos(i);
                var g = NewUi("slot" + i, typeof(RectTransform), typeof(RawImage));
                var rt = g.GetComponent<RectTransform>();
                rt.SetParent(panel, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(p.x, -p.y);
                rt.sizeDelta = new Vector2(16f, 16f);
                icons[i] = g.GetComponent<RawImage>();
                icons[i].raycastTarget = false;
                icons[i].enabled = false;
                counts[i] = MakeText(rt, 7, TextAnchor.LowerRight, new Vector2(16f, 9f), new Vector2(0f, 0f), new Vector2(1f, 0f));
            }

            // Cursor stack and tooltip live on the canvas root.
            var cg = NewUi("cursor", typeof(RectTransform), typeof(RawImage));
            var crt = cg.GetComponent<RectTransform>();
            crt.SetParent(canvasGo.transform, false);
            crt.anchorMin = crt.anchorMax = Vector2.zero;
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(16f, 16f);
            cursorIcon = cg.GetComponent<RawImage>();
            cursorIcon.raycastTarget = false;
            cursorIcon.enabled = false;
            cursorCount = MakeText(crt, 7, TextAnchor.LowerRight, new Vector2(16f, 9f), Vector2.zero, new Vector2(1f, 0f));

            var tg = NewUi("tip", typeof(RectTransform), typeof(RawImage));
            var trt = tg.GetComponent<RectTransform>();
            trt.SetParent(canvasGo.transform, false);
            trt.anchorMin = trt.anchorMax = Vector2.zero;
            trt.pivot = new Vector2(0f, 0f);
            tipBg = tg.GetComponent<RawImage>();
            tipBg.texture = Texture2D.whiteTexture;
            tipBg.color = new Color(0.07f, 0f, 0.07f, 0.92f);
            tipBg.raycastTarget = false;
            tipBg.enabled = false;
            tipText = MakeText(trt, 8, TextAnchor.MiddleLeft, new Vector2(150f, 12f), new Vector2(4f, 0f), Vector2.zero);

            canvasGo.SetActive(false);
        }

        public Texture2D skin;

        // 16x32 front view: head, torso, arms, legs, each with its overlay layer blended on top.
        private Texture2D BuildFront()
        {
            if (skin == null) return null;
            var sp = skin.GetPixels32();
            int sw = skin.width;
            var o = new Color32[16 * 32];
            Color32 S(int x, int y) { return sp[(sw - 1 - y) * sw + x]; } // y from the top, as skin coordinates are
            void Blit(int sx, int sy, int w, int h, int dx, int dy)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var c = S(sx + x, sy + y);
                        if (c.a > 0) o[(31 - (dy + y)) * 16 + dx + x] = c;
                    }
            }
            Blit(8, 8, 8, 8, 4, 0);        // head
            Blit(20, 20, 8, 12, 4, 8);     // torso
            Blit(44, 20, 4, 12, 0, 8);     // right arm (viewer's left)
            Blit(36, 52, 4, 12, 12, 8);    // left arm
            Blit(4, 20, 4, 12, 4, 20);     // right leg
            Blit(20, 52, 4, 12, 8, 20);    // left leg
            Blit(40, 8, 8, 8, 4, 0);       // hat layer
            Blit(20, 36, 8, 12, 4, 8);     // jacket
            Blit(44, 36, 4, 12, 0, 8);     // right sleeve
            Blit(52, 52, 4, 12, 12, 8);    // left sleeve
            Blit(4, 36, 4, 12, 4, 20);     // right pants
            Blit(4, 52, 4, 12, 8, 20);     // left pants
            var t = new Texture2D(16, 32, TextureFormat.RGBA32, false);
            t.SetPixels32(o); t.Apply();
            t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp;
            return Keep.It(t);
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
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = go.GetComponent<Shadow>();
            sh.effectColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            sh.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        public void Open()
        {
            if (IsOpen) return;
            if (canvasGo == null) Build();
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
            // Give a held stack back to the first free slot.
            if (curDef != null)
            {
                for (int i = 0; i < 36 && curCnt > 0; i++) TryPut(i);
                curDef = null; curCnt = 0;
            }
            Time.timeScale = savedTimeScale <= 0f ? 1f : savedTimeScale;
            Cursor.lockState = savedLock;
            Cursor.visible = savedVisible;
            canvasGo.SetActive(false);
            onChanged();
        }

        private void TryPut(int i)
        {
            if (curDef == null) return;
            if (inv.defs[i] == null) { inv.Set(i, curDef, curCnt); curDef = null; curCnt = 0; }
            else if (inv.defs[i] == curDef && curDef.maxStack > 1)
            {
                int put = Mathf.Min(curCnt, curDef.maxStack - inv.cnts[i]);
                inv.cnts[i] += put; curCnt -= put;
                if (curCnt <= 0) curDef = null;
            }
        }

        private int HitSlot(Vector2 mouse, float gs)
        {
            float left = Screen.width / 2f - W / 2f * gs;
            float top = Screen.height / 2f + H / 2f * gs;
            float gx = (mouse.x - left) / gs;
            float gy = (top - mouse.y) / gs;
            for (int i = 0; i < Inv.Count; i++)
            {
                Vector2 p = SlotPos(i);
                if (gx >= p.x - 1f && gx < p.x + 17f && gy >= p.y - 1f && gy < p.y + 17f) return i;
            }
            return -1;
        }

        private void Click(int slot, bool right)
        {
            var sd = inv.defs[slot];
            int sc = inv.cnts[slot];
            if (curDef == null)
            {
                if (sd == null) return;
                if (right && sc > 1)
                {
                    int take = (sc + 1) / 2;
                    curDef = sd; curCnt = take; inv.cnts[slot] = sc - take;
                }
                else { curDef = sd; curCnt = sc; inv.Set(slot, null, 0); }
            }
            else
            {
                if (!Accepts(slot, curDef)) return;
                if (sd == null)
                {
                    int put = right ? 1 : curCnt;
                    inv.Set(slot, curDef, put); curCnt -= put;
                }
                else if (sd == curDef && sd.maxStack > 1)
                {
                    int put = right ? 1 : Mathf.Min(curCnt, sd.maxStack - sc);
                    if (sc + put > sd.maxStack) put = sd.maxStack - sc;
                    inv.cnts[slot] += put; curCnt -= put;
                }
                else if (!right)
                {
                    var od = sd; int oc = sc;
                    inv.Set(slot, curDef, curCnt);
                    curDef = od; curCnt = oc;
                }
                if (curCnt <= 0) { curDef = null; curCnt = 0; }
            }
            onChanged();
        }

        // Moves as much of `count` of `d` into slots [from, to], merging with existing stacks first; returns the leftover.
        private int Distribute(ItemDef d, int count, int from, int to)
        {
            if (d.maxStack > 1)
                for (int i = from; i <= to && count > 0; i++)
                    if (inv.defs[i] == d && inv.cnts[i] < d.maxStack)
                    {
                        int put = Mathf.Min(count, d.maxStack - inv.cnts[i]);
                        inv.cnts[i] += put; count -= put;
                    }
            for (int i = from; i <= to && count > 0; i++)
                if (inv.defs[i] == null)
                {
                    int put = Mathf.Min(count, Mathf.Max(1, d.maxStack));
                    inv.Set(i, d, put); count -= put;
                }
            return count;
        }

        // Shift-click: hotbar <-> main inventory, armor and elytra go straight onto the body, worn items come off.
        private void QuickMove(int slot)
        {
            var d = inv.defs[slot];
            if (d == null) return;
            int c = inv.cnts[slot];
            int left;
            if (slot >= 36)
            {
                left = Distribute(d, c, 9, 35);
                if (left > 0) left = Distribute(d, left, 0, 8);
            }
            else
            {
                if (d.kind == ItemKind.Armor || d.kind == ItemKind.Elytra)
                {
                    int target = d.kind == ItemKind.Elytra ? 37 : 36 + d.armorSlot;
                    if (inv.defs[target] == null)
                    {
                        inv.Set(target, d, 1);
                        inv.cnts[slot]--; if (inv.cnts[slot] <= 0) inv.Set(slot, null, 0);
                        onChanged();
                        return;
                    }
                }
                left = slot < 9 ? Distribute(d, c, 9, 35) : Distribute(d, c, 0, 8);
            }
            if (left <= 0) inv.Set(slot, null, 0); else inv.cnts[slot] = left;
            onChanged();
        }

        private void HotbarSwap(int slot, int hot)
        {
            if (slot == hot) return;
            var a = inv.defs[slot]; int ac = inv.cnts[slot];
            var b = inv.defs[hot]; int bc = inv.cnts[hot];
            if (!Accepts(slot, b) || !Accepts(hot, a)) return;
            inv.Set(slot, b, bc);
            inv.Set(hot, a, ac);
            onChanged();
        }

        public void Tick()
        {
            if (!IsOpen) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            float gs = Mathf.Max(2f, Mathf.Round(Screen.height / 360f));
            scaler.scaleFactor = gs;

            Vector2 mouse = Input.mousePosition;
            int hover = HitSlot(mouse, gs);
            if (hover >= 0)
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (Input.GetMouseButtonDown(0))
                {
                    if (shift && curDef == null) QuickMove(hover); else Click(hover, false);
                }
                else if (Input.GetMouseButtonDown(1)) Click(hover, true);

                // Number keys swap the hovered slot with that hotbar slot.
                for (int n = 0; n < 9; n++)
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + n)) && curDef == null) HotbarSwap(hover, n);
            }

            for (int i = 0; i < Inv.Count; i++)
            {
                var d = inv.defs[i];
                icons[i].texture = d != null ? d.icon : null;
                icons[i].enabled = d != null && d.icon != null;
                counts[i].text = d != null && inv.cnts[i] > 1 ? inv.cnts[i].ToString() : "";
            }

            var crt = cursorIcon.rectTransform;
            crt.anchoredPosition = mouse / gs;
            cursorIcon.texture = curDef != null ? curDef.icon : null;
            cursorIcon.enabled = curDef != null && curDef.icon != null;
            cursorCount.text = curDef != null && curCnt > 1 ? curCnt.ToString() : "";

            // Tooltip with the item name when hovering a filled slot and holding nothing.
            bool showTip = hover >= 0 && inv.defs[hover] != null && curDef == null;
            tipBg.enabled = showTip;
            tipText.text = showTip ? inv.defs[hover].name : "";
            if (showTip)
            {
                var trt = tipBg.rectTransform;
                float w = inv.defs[hover].name.Length * 4.6f + 8f;
                trt.sizeDelta = new Vector2(w, 12f);
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
