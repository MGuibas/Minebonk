using System;
using UnityEngine;

namespace MegabonkSteve
{
    // Builds a Minecraft-style extruded item mesh from a 16x16 icon: one cube per opaque pixel, only
    // the faces that show are emitted. Each quad samples its pixel centre, so a point-filtered
    // texture shows crisp colours.
    internal static class ItemMesh
    {
        public static Mesh Build(Texture2D icon)
        {
            var px = icon.GetPixels32();
            int w = icon.width, h = icon.height;
            var verts = new Il2CppSystem.Collections.Generic.List<Vector3>();
            var uvs = new Il2CppSystem.Collections.Generic.List<Vector2>();
            var tris = new Il2CppSystem.Collections.Generic.List<int>();
            float t = 0.5f; // half thickness in pixel units

            bool Solid(int x, int y) { return x >= 0 && y >= 0 && x < w && y < h && px[y * w + x].a > 10; }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uv)
            {
                int i0 = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                for (int i = 0; i < 4; i++) uvs.Add(uv);
                tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
                tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 3);
            }

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!Solid(x, y)) continue;
                    float cx = x - w / 2f, cy = y - h / 2f;
                    var uv = new Vector2((x + 0.5f) / w, (y + 0.5f) / h);
                    // front (+Z) and back (-Z)
                    Quad(new Vector3(cx, cy, t), new Vector3(cx + 1, cy, t), new Vector3(cx + 1, cy + 1, t), new Vector3(cx, cy + 1, t), uv);
                    Quad(new Vector3(cx + 1, cy, -t), new Vector3(cx, cy, -t), new Vector3(cx, cy + 1, -t), new Vector3(cx + 1, cy + 1, -t), uv);
                    if (!Solid(x - 1, y)) Quad(new Vector3(cx, cy, -t), new Vector3(cx, cy, t), new Vector3(cx, cy + 1, t), new Vector3(cx, cy + 1, -t), uv);
                    if (!Solid(x + 1, y)) Quad(new Vector3(cx + 1, cy, t), new Vector3(cx + 1, cy, -t), new Vector3(cx + 1, cy + 1, -t), new Vector3(cx + 1, cy + 1, t), uv);
                    if (!Solid(x, y - 1)) Quad(new Vector3(cx, cy, -t), new Vector3(cx + 1, cy, -t), new Vector3(cx + 1, cy, t), new Vector3(cx, cy, t), uv);
                    if (!Solid(x, y + 1)) Quad(new Vector3(cx, cy + 1, t), new Vector3(cx + 1, cy + 1, t), new Vector3(cx + 1, cy + 1, -t), new Vector3(cx, cy + 1, -t), uv);
                }

            var m = new Mesh();
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return Keep.It(m);
        }
    }
}

