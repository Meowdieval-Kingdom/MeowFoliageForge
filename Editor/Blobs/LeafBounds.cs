using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Finds the opaque part of a leaf texture so cards map to the leaf instead of its transparent padding.</summary>
    public static class LeafBounds
    {
        const int CellRes = 128;
        const int MaxRes = 1024;
        const float Cutoff = 0.5f;
        static readonly Dictionary<(int id, int cols, int rows), (Hash128 hash, List<Rect> cells)> Cache = new Dictionary<(int, int, int), (Hash128, List<Rect>)>();
        static readonly Rect Full = new Rect(0f, 0f, 1f, 1f);

        /// <summary>UV rectangle covering every pixel with alpha above the cutoff. The whole texture when it is null or has no alpha.</summary>
        public static Rect Of(Texture2D tex) => Cells(tex, 1, 1)[0];

        /// <summary>
        /// The opaque part of every cell of a <paramref name="cols"/> x <paramref name="rows"/> sprite sheet, in UV space.
        /// Cells without opaque pixels are left out; never empty (falls back to the whole texture).
        /// </summary>
        public static List<Rect> Cells(Texture2D tex, int cols, int rows)
        {
            cols = Mathf.Max(1, cols);
            rows = Mathf.Max(1, rows);
            if (tex == null) return Grid(cols, rows);
            var key = (tex.GetInstanceID(), cols, rows);
            Hash128 hash = tex.imageContentsHash;
            if (Cache.TryGetValue(key, out var cached) && cached.hash == hash) return cached.cells;
            List<Rect> cells = Measure(tex, cols, rows);
            Cache[key] = (hash, cells);
            return cells;
        }

        // untrimmed cells, for the generated placeholder texture
        private static List<Rect> Grid(int cols, int rows)
        {
            var list = new List<Rect>();
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    list.Add(new Rect(x / (float)cols, y / (float)rows, 1f / cols, 1f / rows));
            return list;
        }

        private static List<Rect> Measure(Texture2D tex, int cols, int rows)
        {
            int resX = Mathf.Min(CellRes * cols, MaxRes), resY = Mathf.Min(CellRes * rows, MaxRes);
            var rt = RenderTexture.GetTemporary(resX, resY, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            var read = new Texture2D(resX, resY, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, resX, resY), 0, 0);
                Color32[] px = read.GetPixels32();
                byte cut = (byte)(Cutoff * 255f);
                var cells = new List<Rect>();
                // bottom row first, matching UV space
                for (int cy = 0; cy < rows; cy++)
                    for (int cx = 0; cx < cols; cx++)
                    {
                        int x0 = cx * resX / cols, x1 = (cx + 1) * resX / cols, y0 = cy * resY / rows, y1 = (cy + 1) * resY / rows;
                        int minX = x1, minY = y1, maxX = -1, maxY = -1;
                        for (int y = y0; y < y1; y++)
                            for (int x = x0; x < x1; x++)
                                if (px[y * resX + x].a > cut)
                                {
                                    if (x < minX) minX = x;
                                    if (x > maxX) maxX = x;
                                    if (y < minY) minY = y;
                                    if (y > maxY) maxY = y;
                                }
                        if (maxX >= 0) cells.Add(Rect.MinMaxRect(minX / (float)resX, minY / (float)resY, (maxX + 1) / (float)resX, (maxY + 1) / (float)resY));
                    }
                if (cells.Count == 0) cells.Add(Full);
                return cells;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(read);
            }
        }
    }
}
