using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MegabonkSteve
{
    // The vanilla Minecraft HUD built on a uGUI canvas with the real sprites from the user's install.
    // Coordinates are Minecraft GUI pixels measured from bottom-centre.
    internal class McHud
    {
        private readonly GameObject canvasGo;
        private readonly Canvas canvas;
        private readonly CanvasScaler scaler;
        private readonly RectTransform root;

        private readonly RawImage[] slotIcons = new RawImage[9];
        private readonly Text[] slotCount = new Text[9];
        private readonly RawImage[] cooldownOverlay = new RawImage[9];
        private RawImage selection, offhandIcon;
        private Text offhandCount;
        private readonly RawImage[] heartBase = new RawImage[10];
        private readonly RawImage[] heartFill = new RawImage[10];
        private readonly RawImage[] heartAbs = new RawImage[10];
        private readonly RawImage[] foodBase = new RawImage[10];
        private readonly RawImage[] foodFill = new RawImage[10];
        private readonly RawImage[] armorIcons = new RawImage[10];
        private RawImage xpBg, xpFill, crosshair, atkBg, atkFill, atkFull;
        private Text levelText, nameText, promptText;
        private readonly Text[] toasts = new Text[6];
        private readonly float[] toastUntil = new float[6];

        public readonly float[] cooldown = new float[9]; // remaining fraction 0..1 per slot
        public bool targetInReach;
        private RawImage flashImg;

        private Texture2D tHeartFull, tHeartHalf, tHeartCont, tAbsFull, tAbsHalf, tFoodFull, tFoodHalf, tFoodEmpty;
        private Texture2D tArmorFull, tArmorHalf, tArmorEmpty;
        private Font font;

        public McHud()
        {
            canvasGo = new GameObject("MC_HUD");
            UnityEngine.Object.DontDestroyOnLoad(canvasGo);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = Mathf.Max(2f, Mathf.Round(Screen.height / 360f));
            canvasGo.SetActive(false); // shown on the first Tick, once the intro cinematic is over

            var rootGo = NewUi("root", typeof(RectTransform));
            root = rootGo.GetComponent<RectTransform>();
            root.SetParent(canvasGo.transform, false);
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;

            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (font == null) { try { font = Font.CreateDynamicFontFromOSFont("Consolas", 16); } catch { } }

            string h = "gui/sprites/hud/";
            tHeartFull = McAssets.Tex(h + "heart/full");
            tHeartHalf = McAssets.Tex(h + "heart/half");
            tHeartCont = McAssets.Tex(h + "heart/container");
            tAbsFull = McAssets.Tex(h + "heart/absorbing_full");
            tAbsHalf = McAssets.Tex(h + "heart/absorbing_half");
            tFoodFull = McAssets.Tex(h + "food_full");
            tFoodHalf = McAssets.Tex(h + "food_half");
            tFoodEmpty = McAssets.Tex(h + "food_empty");
            tArmorFull = McAssets.Tex(h + "armor_full");
            tArmorHalf = McAssets.Tex(h + "armor_half");
            tArmorEmpty = McAssets.Tex(h + "armor_empty");

            Add(McAssets.Tex(h + "hotbar"), -91f, 0f, 182f, 22f);
            for (int i = 0; i < 9; i++) slotIcons[i] = Add(null, -88f + 20f * i, 3f, 16f, 16f);
            // Item cooldown overlay (vanilla draws a translucent white block rising from the bottom of the slot).
            for (int i = 0; i < 9; i++)
            {
                cooldownOverlay[i] = Add(Texture2D.whiteTexture, -88f + 20f * i, 3f, 16f, 16f);
                cooldownOverlay[i].color = new Color(1f, 1f, 1f, 0.5f);
                cooldownOverlay[i].enabled = false;
            }
            for (int i = 0; i < 9; i++) slotCount[i] = AddText(root, -88f + 20f * i + 6f, 1f, 12f, 9f, 7, TextAnchor.LowerRight);
            selection = Add(McAssets.Tex(h + "hotbar_selection"), -92f, -1f, 24f, 24f);
            Add(McAssets.Tex(h + "hotbar_offhand_left"), -120f, -1f, 29f, 24f);
            offhandIcon = Add(null, -117f, 3f, 16f, 16f);
            offhandCount = AddText(root, -117f + 6f, 1f, 12f, 9f, 7, TextAnchor.LowerRight);

            for (int i = 0; i < 10; i++)
            {
                heartBase[i] = Add(tHeartCont, -91f + 8f * i, 30f, 9f, 9f);
                heartFill[i] = Add(tHeartFull, -91f + 8f * i, 30f, 9f, 9f);
                heartAbs[i] = Add(tAbsFull, -91f + 8f * i, 40f, 9f, 9f);
                foodBase[i] = Add(tFoodEmpty, 82f - 8f * i, 30f, 9f, 9f);
                foodFill[i] = Add(tFoodFull, 82f - 8f * i, 30f, 9f, 9f);
                armorIcons[i] = Add(tArmorEmpty, -91f + 8f * i, 40f, 9f, 9f);
                armorIcons[i].enabled = false;
            }

            xpBg = Add(McAssets.Tex(h + "experience_bar_background"), -91f, 24f, 182f, 5f);
            xpFill = Add(McAssets.Tex(h + "experience_bar_progress"), -91f, 24f, 182f, 5f);

            // Crosshair sits at screen centre, not at the bottom.
            crosshair = AddCentre(McAssets.Tex(h + "crosshair"), 15f, 15f, 0f);
            atkBg = AddCentre(McAssets.Tex(h + "crosshair_attack_indicator_background"), 16f, 4f, -10f);
            atkFill = AddCentre(McAssets.Tex(h + "crosshair_attack_indicator_progress"), 16f, 4f, -10f);
            atkFull = AddCentre(McAssets.Tex(h + "crosshair_attack_indicator_full"), 16f, 16f, -14f);

            // Full-screen flash used by lightning.
            var fg = NewUi("flash", typeof(RectTransform), typeof(RawImage));
            var frt = fg.GetComponent<RectTransform>();
            frt.SetParent(canvasGo.transform, false);
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            flashImg = fg.GetComponent<RawImage>();
            flashImg.texture = Texture2D.whiteTexture;
            flashImg.raycastTarget = false;
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            flashImg.enabled = false;

            levelText = AddText(root, 0f, 35f, 100f, 12f, 8, TextAnchor.MiddleCenter);
            levelText.color = new Color(0.5f, 1f, 0.15f, 1f);
            nameText = AddText(root, 0f, 59f, 200f, 12f, 8, TextAnchor.MiddleCenter);
            promptText = AddText(root, 0f, 74f, 260f, 12f, 8, TextAnchor.MiddleCenter);
            promptText.color = new Color(1f, 1f, 0.7f, 1f);
            promptIcon = Add(MouseIcon(), -5f, 88f, 10f, 14f); promptIcon.enabled = false;
            for (int i = 0; i < toasts.Length; i++) toasts[i] = AddText(root, 100f, 44f + i * 10f, 170f, 10f, 7, TextAnchor.MiddleLeft);
            rewardImg = AddCentre(null, 36f, 36f, 70f); rewardImg.enabled = false;
            rewardTitle = AddText(root, 0f, 206f, 300f, 12f, 10, TextAnchor.MiddleCenter);
            rewardDesc = AddText(root, 0f, 192f, 320f, 10f, 7, TextAnchor.MiddleCenter);
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? -110f : 110f;
                var im = AddCentre(null, 36f, 36f, 70f); im.enabled = false; im.rectTransform.anchoredPosition = new Vector2(sx, 70f);
                var tt = AddText(root, sx, 206f, 200f, 12f, 9, TextAnchor.MiddleCenter);
                var dd = AddText(root, sx, 156f, 190f, 40f, 7, TextAnchor.UpperCenter);
                dd.horizontalOverflow = HorizontalWrapMode.Wrap;
                if (side == 0) { rewardImgL = im; rewardTitleL = tt; rewardDescL = dd; } else { rewardImgR = im; rewardTitleR = tt; rewardDescR = dd; }
            }
        }

        private static GameObject NewUi(string name, params Type[] comps)
        {
            var arr = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Type>(comps.Length);
            for (int i = 0; i < comps.Length; i++) arr[i] = Il2CppInterop.Runtime.Il2CppType.From(comps[i]);
            return new GameObject(name, arr);
        }

        private RawImage Add(Texture2D tex, float x, float y, float w, float h)
        {
            var go = NewUi("img", typeof(RectTransform), typeof(RawImage));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            var img = go.GetComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            img.enabled = tex != null;
            return img;
        }

        private RawImage AddCentre(Texture2D tex, float w, float h, float yOffset)
        {
            var go = NewUi("img", typeof(RectTransform), typeof(RawImage));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvasGo.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, yOffset);
            rt.sizeDelta = new Vector2(w, h);
            var img = go.GetComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            img.enabled = tex != null;
            return img;
        }

        private Text AddText(RectTransform parent, float x, float y, float w, float h, int size, TextAnchor anchor)
        {
            var go = NewUi("text", typeof(RectTransform), typeof(Text), typeof(Shadow));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = anchor == TextAnchor.LowerRight ? new Vector2(1f, 0f) : new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(x + (anchor == TextAnchor.LowerRight ? w : 0f), y);
            rt.sizeDelta = new Vector2(w, h);
            var t = go.GetComponent<Text>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = "";
            var sh = go.GetComponent<Shadow>();
            sh.effectColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            sh.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        // Totem of Undying pop-up: the totem swells at the centre of the screen and fades.
        private RawImage totemImg;
        private float totemStart = -10f;

        public void PopTotem(Texture2D icon)
        {
            if (totemImg == null)
            {
                totemImg = AddCentre(icon, 64f, 64f, 0f);
                totemImg.transform.SetAsLastSibling();
            }
            totemImg.texture = icon;
            totemStart = Time.time;
        }

        private void TickTotem()
        {
            if (totemImg == null) return;
            float p = (Time.time - totemStart) / 1.0f;
            if (p < 0f || p > 1f) { totemImg.enabled = false; return; }
            totemImg.enabled = totemImg.texture != null;
            float s = p < 0.25f ? Mathf.Lerp(0.2f, 4.2f, p / 0.25f) : Mathf.Lerp(4.2f, 5.2f, (p - 0.25f) / 0.75f);
            totemImg.rectTransform.localScale = new Vector3(s, s, 1f);
            totemImg.color = new Color(1f, 1f, 1f, p < 0.75f ? 1f : 1f - (p - 0.75f) / 0.25f);
        }


        // Centre-screen notice for a reward: the item's icon, its name in the rarity colour, and what it does.
        private RawImage rewardImg;
        private Text rewardTitle, rewardDesc;
        private float rewardUntil = -10f;

        public void ShowReward(Texture icon, string title, string desc, Color c)
        {
            if (rewardImg == null) return;
            rewardImg.texture = icon; rewardImg.enabled = icon != null;
            rewardTitle.text = title; rewardTitle.color = c;
            rewardDesc.text = desc;
            rewardUntil = Time.unscaledTime + 4.5f;
        }

        private void TickReward()
        {
            if (rewardImg == null) return;
            float left = rewardUntil - Time.unscaledTime;
            float a = Mathf.Clamp01(left * 2f);
            var tc = rewardTitle.color; rewardTitle.color = new Color(tc.r, tc.g, tc.b, a);
            rewardDesc.color = new Color(1f, 1f, 1f, a);
            rewardImg.color = new Color(1f, 1f, 1f, a);
            if (a <= 0f) { rewardImg.enabled = false; rewardTitle.text = ""; rewardDesc.text = ""; }
            TickRewardPair(a);
        }
        // Two rewards at once (a boss chest): one on the left, one on the right, each with its icon, name and description.
        private RawImage rewardImgL, rewardImgR;
        private Text rewardTitleL, rewardTitleR, rewardDescL, rewardDescR;

        public void ShowRewardPair(Texture iconA, string titleA, string descA, Color cA, Texture iconB, string titleB, string descB, Color cB)
        {
            if (rewardImgL == null) return;
            rewardImg.enabled = false; rewardTitle.text = ""; rewardDesc.text = "";
            rewardImgL.texture = iconA; rewardImgL.enabled = iconA != null; rewardTitleL.text = titleA; rewardTitleL.color = cA; rewardDescL.text = descA;
            rewardImgR.texture = iconB; rewardImgR.enabled = iconB != null; rewardTitleR.text = titleB; rewardTitleR.color = cB; rewardDescR.text = descB;
            rewardUntil = Time.unscaledTime + 6f;
        }

        private void TickRewardPair(float a)
        {
            if (rewardImgL == null) return;
            var tl = rewardTitleL.color; rewardTitleL.color = new Color(tl.r, tl.g, tl.b, a);
            var tr = rewardTitleR.color; rewardTitleR.color = new Color(tr.r, tr.g, tr.b, a);
            rewardDescL.color = rewardDescR.color = new Color(1f, 1f, 1f, a);
            rewardImgL.color = rewardImgR.color = new Color(1f, 1f, 1f, a);
            if (a <= 0f)
            {
                rewardImgL.enabled = rewardImgR.enabled = false;
                rewardTitleL.text = rewardTitleR.text = ""; rewardDescL.text = rewardDescR.text = "";
            }
        }

        // Active effects, top right like vanilla: the effect icon with the time left under it.
        private readonly RawImage[] effImg = new RawImage[6];
        private readonly Text[] effTxt = new Text[6];
        private readonly Dictionary<string, Texture2D> effTex = new Dictionary<string, Texture2D>();

        private void BuildEffects()
        {
            for (int i = 0; i < effImg.Length; i++)
            {
                var go = NewUi("eff", typeof(RectTransform), typeof(RawImage));
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(canvasGo.transform, false);
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-100f - i * 26f, -14f);   // left of the minimap
                rt.sizeDelta = new Vector2(18f, 18f);
                effImg[i] = go.GetComponent<RawImage>(); effImg[i].raycastTarget = false; effImg[i].enabled = false;
                var tgo = NewUi("efft", typeof(RectTransform), typeof(Text), typeof(Shadow));
                var trt = tgo.GetComponent<RectTransform>();
                trt.SetParent(canvasGo.transform, false);
                trt.anchorMin = trt.anchorMax = new Vector2(1f, 1f);
                trt.pivot = new Vector2(1f, 1f);
                trt.anchoredPosition = new Vector2(-100f - i * 26f + 4f, -33f);
                trt.sizeDelta = new Vector2(26f, 10f);
                var t = tgo.GetComponent<Text>();
                if (font != null) t.font = font;
                t.fontSize = 7; t.alignment = TextAnchor.UpperRight; t.color = Color.white; t.raycastTarget = false;
                t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
                var sh = tgo.GetComponent<Shadow>(); sh.effectColor = new Color(0.1f, 0.1f, 0.1f, 1f); sh.effectDistance = new Vector2(1f, -1f);
                effTxt[i] = t;
            }
        }

        // names: mob_effect texture names; seconds: time left for each.
        public void SetEffects(List<KeyValuePair<string, float>> list)
        {
            if (effImg[0] == null) BuildEffects();
            for (int i = 0; i < effImg.Length; i++)
            {
                if (list != null && i < list.Count)
                {
                    Texture2D t;
                    if (!effTex.TryGetValue(list[i].Key, out t)) { t = McAssets.Tex("mob_effect/" + list[i].Key); effTex[list[i].Key] = t; }
                    effImg[i].texture = t; effImg[i].enabled = t != null;
                    int secs = Mathf.CeilToInt(list[i].Value);
                    effTxt[i].text = (secs / 60) + ":" + (secs % 60).ToString("00");
                    effTxt[i].color = secs <= 10 ? new Color(1f, 0.5f, 0.5f) : Color.white;
                }
                else { effImg[i].enabled = false; effTxt[i].text = ""; }
            }
        }

        // Small item/notice lines stacked beside the hotbar, each fading after a few seconds.
        public void AddToast(string text)
        {
            for (int i = toasts.Length - 1; i > 0; i--) { toasts[i].text = toasts[i - 1].text; toastUntil[i] = toastUntil[i - 1]; }
            toasts[0].text = text;
            toastUntil[0] = Time.unscaledTime + 3.5f;
        }

        private RawImage promptIcon;

        // A little mouse with its right button lit: the interaction hint.
        private static Texture2D MouseIcon()
        {
            var px = new Color32[10 * 14];
            var line = new Color32(30, 30, 30, 255); var body = new Color32(200, 200, 205, 255); var left = new Color32(120, 120, 128, 255); var right = new Color32(255, 215, 60, 255);
            for (int y = 0; y < 14; y++)
                for (int x = 0; x < 10; x++)
                {
                    bool corner = (x == 0 || x == 9) && (y == 0 || y == 13);
                    if (corner) continue;
                    bool edge = x == 0 || x == 9 || y == 0 || y == 13;
                    Color32 c = edge ? line : body;
                    if (!edge && y >= 7) c = x <= 4 ? left : right;
                    if (!edge && (x == 5 && y >= 7)) c = line;
                    if (!edge && y == 7) c = line;
                    px[y * 10 + x] = c;
                }
            var t = new Texture2D(10, 14, TextureFormat.RGBA32, false);
            t.SetPixels32(px); t.Apply(); t.filterMode = FilterMode.Point;
            return Keep.It(t);
        }

        public void SetPrompt(string s) { if (promptText != null) promptText.text = s ?? ""; if (promptIcon != null) promptIcon.enabled = !string.IsNullOrEmpty(s); }

        public void SetVisible(bool v) { if (canvasGo != null) canvasGo.SetActive(v); }

        public void SetOffhand(Texture2D tex, int count)
        {
            offhandIcon.texture = tex;
            offhandIcon.enabled = tex != null;
            offhandCount.text = count > 1 ? count.ToString() : "";
        }

        // hearts/food/absorption/armor are in half-units (0..20).
        public void Tick(ItemDef[] defs, int[] counts, int selected, float hearts, float absorption, float food, float armor,
                         float xpProgress, int xpLevel, float attackStrength, bool showAttack, string itemName, float nameAlpha)
        {
            scaler.scaleFactor = Mathf.Max(2f, Mathf.Round(Screen.height / 360f));
            TickTotem();
            TickReward();
            for (int i = 0; i < toasts.Length; i++)
            {
                float left = toastUntil[i] - Time.unscaledTime;
                toasts[i].color = new Color(1f, 1f, 1f, Mathf.Clamp01(left * 2f));
            }
            if (McFlash.Level > 0.005f)
            {
                flashImg.enabled = true;
                flashImg.color = new Color(1f, 1f, 1f, McFlash.Level);
                McFlash.Level = Mathf.Max(0f, McFlash.Level - Time.unscaledDeltaTime * 2.5f);
            }
            else flashImg.enabled = false;

            for (int i = 0; i < 9; i++)
            {
                var tex = defs[i] != null ? defs[i].icon : null;
                slotIcons[i].texture = tex;
                slotIcons[i].enabled = tex != null;
                slotCount[i].text = (defs[i] != null && counts[i] > 1) ? counts[i].ToString() : "";
            }
            selection.rectTransform.anchoredPosition = new Vector2(-92f + 20f * selected, -1f);
            for (int i = 0; i < 9; i++)
            {
                float c = Mathf.Clamp01(cooldown[i]);
                cooldownOverlay[i].enabled = c > 0f;
                cooldownOverlay[i].rectTransform.sizeDelta = new Vector2(16f, Mathf.Ceil(16f * c));
            }
            atkFull.enabled = showAttack && attackStrength >= 1f && targetInReach && atkFull.texture != null;

            float absY = armor > 0f ? 50f : 40f;
            for (int i = 0; i < 10; i++)
            {
                float f = hearts - i * 2f;
                heartFill[i].enabled = f > 0f;
                heartFill[i].texture = f >= 2f ? tHeartFull : tHeartHalf;
                float a = absorption - i * 2f;
                heartAbs[i].enabled = a > 0f;
                heartAbs[i].texture = a >= 2f ? tAbsFull : tAbsHalf;
                heartAbs[i].rectTransform.anchoredPosition = new Vector2(-91f + 8f * i, absY);

                float fd = food - i * 2f;
                foodFill[i].enabled = fd > 0f;
                foodFill[i].texture = fd >= 2f ? tFoodFull : tFoodHalf;

                float ar = armor - i * 2f;
                armorIcons[i].enabled = armor > 0f;
                armorIcons[i].texture = ar >= 2f ? tArmorFull : (ar >= 1f ? tArmorHalf : tArmorEmpty);
            }

            xpFill.uvRect = new Rect(0f, 0f, Mathf.Clamp01(xpProgress), 1f);
            xpFill.rectTransform.sizeDelta = new Vector2(182f * Mathf.Clamp01(xpProgress), 5f);
            levelText.text = xpLevel > 0 ? xpLevel.ToString() : "";

            atkBg.enabled = atkFill.enabled = showAttack && attackStrength < 1f && atkBg.texture != null;
            atkFill.uvRect = new Rect(0f, 0f, Mathf.Clamp01(attackStrength), 1f);
            atkFill.rectTransform.sizeDelta = new Vector2(16f * Mathf.Clamp01(attackStrength), 4f);
            atkFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            atkFill.rectTransform.anchoredPosition = new Vector2(-8f, -10f);

            nameText.text = nameAlpha > 0f ? itemName : "";
            nameText.color = new Color(1f, 1f, 1f, nameAlpha);
        }

        public void Destroy()
        {
            if (canvasGo != null) UnityEngine.Object.Destroy(canvasGo);
        }
    }
}

