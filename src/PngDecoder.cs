using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace MegabonkSteve
{
    // Minimal PNG reader (non-interlaced, 8/16-bit and palette) because the game build strips
    // ImageConversion. Output rows are bottom-up so they can go straight into Texture2D.SetPixels32.
    internal static class PngDecoder
    {
        public static string Info = "";

        public static bool Decode(byte[] png, out int width, out int height, out Color32[] pixels)
        {
            width = height = 0; pixels = null;
            if (png.Length < 33 || png[0] != 0x89 || png[1] != 0x50) return false;

            int pos = 8, bitDepth = 8, colorType = 6, interlace = 0;
            byte[] palette = null, trns = null;
            var idat = new MemoryStream();
            while (pos + 8 <= png.Length)
            {
                int len = (png[pos] << 24) | (png[pos + 1] << 16) | (png[pos + 2] << 8) | png[pos + 3];
                string type = "" + (char)png[pos + 4] + (char)png[pos + 5] + (char)png[pos + 6] + (char)png[pos + 7];
                int d = pos + 8;
                if (type == "IHDR")
                {
                    width = (png[d] << 24) | (png[d + 1] << 16) | (png[d + 2] << 8) | png[d + 3];
                    height = (png[d + 4] << 24) | (png[d + 5] << 16) | (png[d + 6] << 8) | png[d + 7];
                    bitDepth = png[d + 8]; colorType = png[d + 9]; interlace = png[d + 12];
                }
                else if (type == "PLTE") { palette = new byte[len]; Array.Copy(png, d, palette, 0, len); }
                else if (type == "tRNS") { trns = new byte[len]; Array.Copy(png, d, trns, 0, len); }
                else if (type == "IDAT") idat.Write(png, d, len);
                else if (type == "IEND") break;
                pos = d + len + 4;
            }
            Info = "ct=" + colorType + " bd=" + bitDepth + " il=" + interlace;
            if (interlace != 0 || width <= 0 || height <= 0) return false;

            int channels = colorType == 6 ? 4 : colorType == 2 ? 3 : colorType == 4 ? 2 : 1;
            int bitsPerPixel = channels * bitDepth;
            int bpp = Math.Max(1, bitsPerPixel / 8);
            int stride = (width * bitsPerPixel + 7) / 8;

            byte[] raw;
            idat.Position = 2; // skip zlib header
            using (var ds = new DeflateStream(idat, CompressionMode.Decompress))
            using (var ms = new MemoryStream())
            {
                ds.CopyTo(ms);
                raw = ms.ToArray();
            }
            if (raw.Length < (stride + 1) * height) return false;

            var cur = new byte[stride];
            var prev = new byte[stride];
            pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int ro = y * (stride + 1);
                int filter = raw[ro];
                Array.Copy(raw, ro + 1, cur, 0, stride);
                for (int i = 0; i < stride; i++)
                {
                    int a = i >= bpp ? cur[i - bpp] : 0;
                    int b = prev[i];
                    int c = i >= bpp ? prev[i - bpp] : 0;
                    int x = cur[i];
                    switch (filter)
                    {
                        case 1: x += a; break;
                        case 2: x += b; break;
                        case 3: x += (a + b) / 2; break;
                        case 4:
                            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                            x += (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c);
                            break;
                    }
                    cur[i] = (byte)x;
                }
                for (int xp = 0; xp < width; xp++)
                {
                    Color32 col;
                    if (colorType == 3)
                    {
                        int idx;
                        if (bitDepth == 8) idx = cur[xp];
                        else
                        {
                            int bit = xp * bitDepth;
                            idx = (cur[bit / 8] >> (8 - bitDepth - (bit % 8))) & ((1 << bitDepth) - 1);
                        }
                        byte al = (trns != null && idx < trns.Length) ? trns[idx] : (byte)255;
                        col = new Color32(palette[idx * 3], palette[idx * 3 + 1], palette[idx * 3 + 2], al);
                    }
                    else
                    {
                        int step = bitDepth == 16 ? 2 : 1;
                        int o = xp * channels * step;
                        if (colorType == 6) col = new Color32(cur[o], cur[o + step], cur[o + 2 * step], cur[o + 3 * step]);
                        else if (colorType == 2) col = new Color32(cur[o], cur[o + step], cur[o + 2 * step], 255);
                        else if (colorType == 4) col = new Color32(cur[o], cur[o], cur[o], cur[o + step]);
                        else
                        {
                            // Greyscale, 1/2/4/8/16 bit, with an optional single transparent grey value.
                            int raw1;
                            if (bitDepth >= 8) raw1 = cur[xp * step];
                            else
                            {
                                int bit = xp * bitDepth;
                                raw1 = (cur[bit / 8] >> (8 - bitDepth - (bit % 8))) & ((1 << bitDepth) - 1);
                            }
                            int g = bitDepth >= 8 ? raw1 : raw1 * 255 / ((1 << bitDepth) - 1);
                            bool clear = trns != null && trns.Length >= 2 && (bitDepth == 16 ? raw1 == trns[0] : raw1 == trns[1]);
                            col = new Color32((byte)g, (byte)g, (byte)g, (byte)(clear ? 0 : 255));
                        }
                    }
                    pixels[(height - 1 - y) * width + xp] = col;
                }
                var t = prev; prev = cur; cur = t;
            }
            return true;
        }
    }
}
