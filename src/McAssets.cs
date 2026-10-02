using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;

namespace MegabonkSteve
{
    // Loads real Minecraft textures at runtime from the user's own install (client jar + launcher skin).
    // Nothing is copied into the mod or redistributed.
    internal static class McAssets
    {
        private static ZipArchive zip;
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        public static bool Ready;

        public static void Init()
        {
            if (Ready) return;
            try
            {
                string mc = McResources.Root();
                if (mc == null) { Plugin.Logger.LogWarning("No Minecraft resources found (install Minecraft, or accept the download prompt)"); return; }
                string versions = Path.Combine(mc, "versions");
                var jars = Directory.GetFiles(versions, "*.jar", SearchOption.AllDirectories)
                    .Where(j => !Path.GetFileName(j).StartsWith("fabric"))
                    .OrderByDescending(j => new FileInfo(j).Length);
                foreach (var jar in jars)
                {
                    var z = ZipFile.OpenRead(jar);
                    if (z.GetEntry("assets/minecraft/textures/gui/sprites/hud/hotbar.png") != null)
                    {
                        zip = z;
                        Plugin.Logger.LogInfo("Minecraft assets from " + jar);
                        break;
                    }
                    z.Dispose();
                }
                Ready = zip != null;
            }
            catch (Exception e) { Plugin.Logger.LogError("McAssets.Init: " + e.Message); }
            if (!Ready) Plugin.Logger.LogWarning("No Minecraft client jar found; using built-in icons");
        }

        // path relative to assets/minecraft/textures, no extension, e.g. "gui/sprites/hud/hotbar".
        public static Texture2D Tex(string path)
        {
            Texture2D t;
            if (cache.TryGetValue(path, out t)) return t;
            if (zip == null) return null;
            var e = zip.GetEntry("assets/minecraft/textures/" + path + ".png");
            if (e == null) { cache[path] = null; return null; }
            using (var s = e.Open())
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                t = FromPng(ms.ToArray());
            }
            cache[path] = t;
            return t;
        }

        public static Texture2D FromPng(byte[] bytes)
        {
            int w, h;
            Color32[] px;
            if (!PngDecoder.Decode(bytes, out w, out h, out px)) return null;
            int opaque = 0;
            foreach (var c in px) if (c.a > 0) opaque++;
            Plugin.Dbg("tex " + w + "x" + h + " opaque=" + opaque + " " + PngDecoder.Info);
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            Keep.It(t);
            return t;
        }

        // The user's own launcher skin (64x64), else the default Steve.
        public static Texture2D Skin(out bool slim)
        {
            { bool s2; var chosen = McSkins.Selected(out s2); if (chosen != null) { slim = s2; return chosen; } }
            slim = false;
            try
            {
                string mc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
                string f = Path.Combine(mc, "launcher_custom_skins.json");
                if (File.Exists(f))
                {
                    string json = File.ReadAllText(f);
                    int i = json.IndexOf("\"skinImage\"");
                    if (i >= 0)
                    {
                        int a = json.IndexOf("base64,", i) + 7;
                        int b = json.IndexOf('"', a);
                        var png = Convert.FromBase64String(json.Substring(a, b - a));
                        var t = FromPng(png);
                        int s = json.IndexOf("\"slim\"", i);
                        if (s >= 0) slim = json.Substring(s, 20).Contains("true");
                        if (t.width == 64 && t.height == 64) return t;
                    }
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("Custom skin: " + e.Message); }
            return Tex("entity/player/wide/steve");
        }
    }
}


