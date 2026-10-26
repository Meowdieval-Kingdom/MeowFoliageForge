using System.IO;
using UnityEditor;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>A small procedural leaf-cluster alpha texture so a fresh tree looks right with no art assets.</summary>
    public static class LeafTexture
    {
        const int Size = 128;
        static Texture2D _temporary;

        /// <summary>Loads the shared texture from <paramref name="folder"/>, writing it first if needed.</summary>
        public static Texture2D GetOrCreate(string folder)
        {
            string path = $"{folder}/LeafCards.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            Texture2D tex = Build();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.streamingMipmaps = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>An unsaved copy for previews.</summary>
        public static Texture2D Temporary()
        {
            if (_temporary == null) _temporary = Build();
            return _temporary;
        }

        private static Texture2D Build()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[Size * Size];
            var rng = new System.Random(7);
            var blobs = new Vector3[5];
            for (int i = 0; i < blobs.Length; i++)
                blobs[i] = new Vector3(0.25f + (float)rng.NextDouble() * 0.5f, 0.25f + (float)rng.NextDouble() * 0.5f, 0.16f + (float)rng.NextDouble() * 0.1f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = x / (float)(Size - 1), v = y / (float)(Size - 1);
                    float best = 0f;
                    foreach (Vector3 b in blobs)
                    {
                        float d = Vector2.Distance(new Vector2(u, v), new Vector2(b.x, b.y)) / b.z;
                        best = Mathf.Max(best, 1f - d);
                    }
                    byte a = (byte)(Mathf.Clamp01(best * 6f) * 255f);
                    px[y * Size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }
    }
}
