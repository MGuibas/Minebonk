using System;
using System.Collections.Generic;
using UnityEngine;

namespace MegabonkSteve
{
    // While Steve plays, the ground, sand, dirt and rock of every map are drawn with Minecraft block textures.
    // The game's own materials are edited in memory and put back when the run ends.
    internal static class McTerrain
    {
        private class Saved { public Material mat; public Texture tex; public Color color; public bool hadColor; public string prop; public Dictionary<string, Texture> others = new Dictionary<string, Texture>(); }

        private static readonly Dictionary<int, Saved> saved = new Dictionary<int, Saved>();
        private static readonly HashSet<string> logged = new HashSet<string>();
        private static float nextScan;
        private static readonly string[] texProps = { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo", "_AlbedoMap", "_MainTexture", "_Diffuse", "_BaseTexture", "_Texture" };
        private static readonly HashSet<string> shadersLogged = new HashSet<string>();

        // keyword -> block texture and the tint vanilla applies to it (grass and leaves are grey in the texture)
        private static string Classify(string key, out Color tint)
        {
            tint = Color.white;
            key = key.ToLowerInvariant();
            if (key.Contains("water") || key.Contains("sky") || key.Contains("cloud") || key.Contains("lava")) return null;
            if (key.Contains("sand") || key.Contains("desert") || key.Contains("dune")) return "block/sand";
            if (key.Contains("snow") || key.Contains("ice")) return "block/snow";
            if (key.Contains("dirt") || key.Contains("path") || key.Contains("mud") || key.Contains("soil")) return "block/dirt";
            if (key.Contains("filltop")) { tint = new Color(0.57f, 0.74f, 0.35f, 1f); return "block/grass_block_top"; }
            if (key.Contains("fillmiddle") || key.Contains("fill")) return "block/dirt";
            if (key.Contains("stone") || key.Contains("rock") || key.Contains("cliff") || key.Contains("cobble") || key.Contains("brick") || key.Contains("mountain"))
                return "block/stone";
            if (key.Contains("grass") || key.Contains("ground") || key.Contains("terrain") || key.Contains("floor") || key.Contains("meadow") || key.Contains("forest"))
            {
                tint = new Color(0.57f, 0.74f, 0.35f, 1f);
                return "block/grass_block_top";
            }
            return null;
        }

        // The block texture with the vanilla biome tint baked in (grass is grey in the file), so it works whatever colour slot the shader has.
        private static readonly Dictionary<string, Texture2D> baked = new Dictionary<string, Texture2D>();

        private static Texture2D Baked(string block, Color tint)
        {
            string key = block + "|" + tint;
            Texture2D t;
            if (baked.TryGetValue(key, out t) && t != null) return t;
            var src = McAssets.Tex(block);
            if (src == null) return null;
            if (tint == Color.white) { baked[key] = src; return src; }
            var px = src.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var q = px[i];
                q.r = (byte)(q.r * tint.r); q.g = (byte)(q.g * tint.g); q.b = (byte)(q.b * tint.b);
                px[i] = q;
            }
            t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            t.SetPixels32(px); t.Apply(); t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Repeat;
            Keep.It(t);
            baked[key] = t;
            return t;
        }

        public static void Tick()
        {
            if (Time.time < nextScan) return;
            nextScan = Time.time + 12f;
            try { ScanRenderers(); } catch (Exception e) { if (Time.time < 120f) Plugin.Logger.LogWarning("terrain scan: " + e.Message); }
        }

        private static void ScanRenderers()
        {
            foreach (var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
            {
                if (r == null || !r.enabled) continue;
                var b = r.bounds;
                // only big surfaces: terrain tiles, floors, large rock faces
                if (Mathf.Max(b.size.x, b.size.z) < 30f) continue;
                var mats = r.sharedMaterials;
                foreach (var m in mats)
                {
                    if (m == null) continue;
                    int id = m.GetInstanceID();
                    if (saved.ContainsKey(id)) continue;
                    string texName = "";
                    string prop = null;
                    foreach (var p in texProps) if (m.HasProperty(p)) { prop = p; var t = m.GetTexture(p); if (t != null) texName = t.name; break; }
                    string key = m.name + " " + texName + " " + r.gameObject.name;
                    string logKey = m.name + "|" + texName;
                    if (logged.Add(logKey))
                        Plugin.Dbg("terrain material: '" + m.name + "' shader=" + (m.shader != null ? m.shader.name : "?") + " tex='" + texName + "' on '" + r.gameObject.name + "' size=" + b.size.x.ToString("0") + "x" + b.size.z.ToString("0"));
                    Color tint = Color.white;
                    string block = prop != null ? Classify(key, out tint) : null;
                    if (block == null) { saved[id] = null; continue; }
                    var tex = Baked(block, tint);
                    if (tex == null) continue;
                    var s = new Saved { mat = m, prop = prop, tex = m.GetTexture(prop) };
                    tex.wrapMode = TextureWrapMode.Repeat;
                    // every albedo-like slot the shader has gets the block texture
                    foreach (var p in texProps)
                        if (m.HasProperty(p)) { s.others[p] = m.GetTexture(p); m.SetTexture(p, tex); }
                    try { if (shadersLogged.Add(m.shader.name)) { var names = m.GetTexturePropertyNames(); string ls = ""; foreach (var nm in names) ls += nm + " "; Plugin.Logger.LogInfo("shader " + m.shader.name + " textures: " + ls); } } catch { }
                    saved[id] = s;
                    Plugin.Logger.LogInfo("terrain reskinned: " + m.name + " -> " + block);
                }
            }
        }

        // ---------------------------------------------------------------- grass tufts

        private static Mesh grassMeshMc;
        private static Mesh grassOrigMesh;
        private static GrassChunkManager grassOwner;

        // Two crossed planes of the short_grass sprite, one small quad per visible texel (the grass material has no alpha).
        private static Mesh BuildGrassMesh(Texture2D src, Color tint)
        {
            var px = src.GetPixels32();
            int w = src.width, h = src.height, step = Mathf.Max(1, w / 8);
            var verts = new Il2CppSystem.Collections.Generic.List<Vector3>();
            var uvs = new Il2CppSystem.Collections.Generic.List<Vector2>();
            var cols = new Il2CppSystem.Collections.Generic.List<Color>();
            var nrm = new Il2CppSystem.Collections.Generic.List<Vector3>();
            var tris = new Il2CppSystem.Collections.Generic.List<int>();
            for (int y = 0; y < h; y += step)
                for (int x = 0; x < w; x += step)
                {
                    int sx = Mathf.Min(x + step / 2, w - 1), sy = Mathf.Min(y + step / 2, h - 1);
                    var p = px[sy * w + sx];
                    if (p.a < 40) continue;
                    float x0 = x / (float)w - 0.5f, x1 = (x + step) / (float)w - 0.5f, y0 = y / (float)h, y1 = (y + step) / (float)h;
                    var uv = new Vector2((sx + 0.5f) / w, (sy + 0.5f) / h);
                    var c = new Color(p.r / 255f * tint.r, p.g / 255f * tint.g, p.b / 255f * tint.b, 1f);
                    for (int plane = 0; plane < 2; plane++)
                    {
                        int i0 = verts.Count;
                        if (plane == 0) { verts.Add(new Vector3(x0, y0, 0)); verts.Add(new Vector3(x1, y0, 0)); verts.Add(new Vector3(x1, y1, 0)); verts.Add(new Vector3(x0, y1, 0)); }
                        else { verts.Add(new Vector3(0, y0, x0)); verts.Add(new Vector3(0, y0, x1)); verts.Add(new Vector3(0, y1, x1)); verts.Add(new Vector3(0, y1, x0)); }
                        for (int i = 0; i < 4; i++) { uvs.Add(uv); cols.Add(c); nrm.Add(Vector3.up); }
                        tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2); tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 3);
                        tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 1); tris.Add(i0); tris.Add(i0 + 3); tris.Add(i0 + 2);
                    }
                }
            var m = new Mesh();
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetColors(cols); m.SetNormals(nrm); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return Keep.It(m);
        }

        // Called when a map builds its grass: the blades become Minecraft grass.
        public static void SkinGrass(GrassChunkManager gm)
        {
            if (gm == null) return;
            var tint = new Color(0.57f, 0.74f, 0.35f, 1f);
            var tex = Baked("block/short_grass", tint);
            if (tex == null) return;
            if (grassMeshMc == null) grassMeshMc = BuildGrassMesh(McAssets.Tex("block/short_grass"), tint);
            grassOwner = gm; grassOrigMesh = gm.grassMesh;
            gm.grassMesh = grassMeshMc;
            var m = gm.grassMaterial;
            if (m != null)
            {
                int id = m.GetInstanceID();
                if (!saved.ContainsKey(id) || saved[id] == null)
                {
                    var s = new Saved { mat = m };
                    foreach (var p in texProps) if (m.HasProperty(p)) { s.others[p] = m.GetTexture(p); m.SetTexture(p, tex); }
                    saved[id] = s;
                }
                try { Plugin.Dbg("grass material " + m.name + " shader=" + m.shader.name); } catch { }
            }
            Plugin.Logger.LogInfo("grass swapped for Minecraft grass");
        }

        // Back to the game's own look (the run ended, or another character is playing).
        public static void Restore()
        {
            foreach (var kv in saved)
            {
                var s = kv.Value;
                if (s == null || s.mat == null) continue;
                try
                {
                    foreach (var o in s.others) s.mat.SetTexture(o.Key, o.Value);
                    string colorProp = s.mat.HasProperty("_BaseColor") ? "_BaseColor" : (s.mat.HasProperty("_Color") ? "_Color" : null);
                    if (s.hadColor && colorProp != null) s.mat.SetColor(colorProp, s.color);
                }
                catch { }
            }
            try { if (grassOwner != null && grassOrigMesh != null) grassOwner.grassMesh = grassOrigMesh; } catch { }
            grassOwner = null;
            saved.Clear();
        }
    }
}
