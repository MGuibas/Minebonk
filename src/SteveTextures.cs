using System;
using UnityEngine;

namespace MegabonkSteve
{
    // Paints a 64x64 Steve skin in the standard Minecraft layout, no external assets.
    internal static class SteveTextures
    {
        private static Texture2D skin;

        private static readonly Color32 Skin = new Color32(198, 146, 112, 255);
        private static readonly Color32 SkinDark = new Color32(168, 118, 88, 255);
        private static readonly Color32 Hair = new Color32(70, 46, 28, 255);
        private static readonly Color32 Shirt = new Color32(0, 168, 168, 255);
        private static readonly Color32 ShirtDark = new Color32(0, 140, 140, 255);
        private static readonly Color32 Pants = new Color32(52, 52, 150, 255);
        private static readonly Color32 Shoe = new Color32(104, 104, 104, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Iris = new Color32(70, 58, 140, 255);
        private static readonly Color32 Mouth = new Color32(120, 70, 56, 255);

        public static Texture2D GetSkin()
        {
            if (skin != null) return skin;
            var px = new Color32[64 * 64];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            Head(px, 0, 0);
            Box(px, 16, 16, 8, 12, 4, (face, x, y) => y < 0 ? Shirt : Shirt); // torso
            Limb(px, 40, 16, true);   // right arm
            Limb(px, 32, 48, true);   // left arm
            Limb(px, 0, 16, false);   // right leg
            Limb(px, 16, 48, false);  // left leg

            skin = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            skin.filterMode = FilterMode.Point;
            skin.wrapMode = TextureWrapMode.Clamp;
            skin.SetPixels32(px);
            skin.Apply();
            Keep.It(skin);
            return skin;
        }

        public static Texture MakeIcon()
        {
            // 16x16 head portrait for the character select grid.
            var px = new Color32[16 * 16];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    int fx = x / 2, fy = 7 - y / 2; // face pixel coordinates, top-left origin
                    px[y * 16 + x] = FaceColor(fx, fy);
                }
            var t = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.SetPixels32(px);
            t.Apply();
            Keep.It(t);
            return t;
        }

        private delegate Color32 Painter(int face, int x, int y);

        // Face indices follow the skin strip order: 0 top, 1 bottom, 2 right, 3 front, 4 left, 5 back.
        private static void Box(Color32[] px, int u, int v, int w, int h, int d, Painter p)
        {
            Fill(px, u + d, v, w, d, 0, p);
            Fill(px, u + d + w, v, w, d, 1, p);
            Fill(px, u, v + d, d, h, 2, p);
            Fill(px, u + d, v + d, w, h, 3, p);
            Fill(px, u + d + w, v + d, d, h, 4, p);
            Fill(px, u + 2 * d + w, v + d, w, h, 5, p);
        }

        private static void Fill(Color32[] px, int rx, int ry, int rw, int rh, int face, Painter p)
        {
            for (int y = 0; y < rh; y++)
                for (int x = 0; x < rw; x++)
                    Set(px, rx + x, ry + y, p(face, x, y));
        }

        private static void Set(Color32[] px, int x, int yTop, Color32 c)
        {
            px[(63 - yTop) * 64 + x] = c;
        }

        private static Color32 FaceColor(int x, int y)
        {
            if (y < 2) return Hair;
            if (y == 2 && (x == 0 || x == 7)) return Hair;
            if (y == 4) { if (x == 1 || x == 6) return White; if (x == 2 || x == 5) return Iris; }
            if (y == 5 && (x == 3 || x == 4)) return SkinDark;
            if (y == 6 && x >= 2 && x <= 5) return Mouth;
            return Skin;
        }

        private static void Head(Color32[] px, int u, int v)
        {
            Box(px, u, v, 8, 8, 8, (face, x, y) =>
            {
                switch (face)
                {
                    case 0: return Hair;
                    case 1: return Skin;
                    case 3: return FaceColor(x, y);
                    case 5: return Hair;
                    default: return y < 2 ? Hair : Skin;
                }
            });
        }

        private static void Limb(Color32[] px, int u, int v, bool arm)
        {
            Box(px, u, v, 4, 12, 4, (face, x, y) =>
            {
                if (face == 0) return arm ? Shirt : Pants;
                if (face == 1) return arm ? Skin : Shoe;
                if (arm) return y < 4 ? (x == 0 ? ShirtDark : Shirt) : Skin;
                return y >= 10 ? Shoe : Pants;
            });
        }
    }
}

