using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Builds the unit clump mesh for a grass variant: crossed cards, origin at the root, 1 m wide and 1 m tall.</summary>
    public static class GrassMeshes
    {
        // lod 0 = as authored, 1 = one card fewer (at least 2) with one segment, 2 = at most 2 cards with one segment
        private static void Shape(GrassVariant v, int lod, out int cards, out int segs)
        {
            cards = Mathf.Clamp(v.Cards, 1, 6);
            segs = Mathf.Clamp(v.Segments, 1, 4);
            if (lod <= 0) return;
            cards = lod == 1 ? Mathf.Min(cards, Mathf.Max(2, cards - 1)) : Mathf.Min(cards, 2);
            segs = 1;
        }

        /// <summary>Triangles in one clump mesh at the given LOD (0 = full).</summary>
        public static int TriangleCount(GrassVariant v, int lod)
        {
            Shape(v, lod, out int cards, out int segs);
            return cards * segs * 2;
        }

        /// <summary>UV.x runs across a card, UV.y from root (0) to tip (1); the shader uses UV.y as the wind weight.</summary>
        public static Mesh Build(GrassVariant v, int lod = 0)
        {
            Shape(v, lod, out int cards, out int segs);
            int rowVerts = segs + 1;
            var verts = new Vector3[cards * rowVerts * 2];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new int[cards * segs * 6];
            var rng = new System.Random(cards * 31 + segs);

            for (int c = 0; c < cards; c++)
            {
                float yaw = (c + 0.25f * (float)rng.NextDouble()) * Mathf.PI / cards;
                var along = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                var facing = new Vector3(-Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (v.Spread * 0.5f * (float)rng.NextDouble() * (cards > 1 ? 1f : 0f));
                float cardHeight = 0.85f + 0.15f * (float)rng.NextDouble();
                float cardWidth = 0.85f + 0.15f * (float)rng.NextDouble();
                float lean = (float)rng.NextDouble() * 2f - 1f;

                for (int r = 0; r < rowVerts; r++)
                {
                    float t = r / (float)segs;
                    // curve toward the facing direction, plus a small random lean
                    Vector3 centre = offset + facing * ((v.Bend * t * t) + lean * 0.06f * t) + Vector3.up * (t * cardHeight);
                    Vector3 normal = (facing - Vector3.up * (v.Bend * 2f * t)).normalized;
                    for (int s = 0; s < 2; s++)
                    {
                        int i = c * rowVerts * 2 + r * 2 + s;
                        verts[i] = centre + along * ((s - 0.5f) * cardWidth);
                        // normals fan outward across the card width, so a blade reads round instead of flat
                        normals[i] = (normal + along * ((s - 0.5f) * 0.9f)).normalized;
                        uvs[i] = new Vector2(s, t);
                    }
                }
                for (int r = 0; r < segs; r++)
                {
                    int i = c * rowVerts * 2 + r * 2;
                    int k = (c * segs + r) * 6;
                    tris[k] = i;
                    tris[k + 1] = i + 2;
                    tris[k + 2] = i + 1;
                    tris[k + 3] = i + 1;
                    tris[k + 4] = i + 2;
                    tris[k + 5] = i + 3;
                }
            }

            var mesh = new Mesh { name = "GrassClump", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>
    /// Shared tiling wind-noise texture: value noise on a <see cref="Cells"/> x <see cref="Cells"/> wrapped lattice, built in code at first
    /// use (no asset). The grass shader samples it in the vertex stage in place of its per-vertex hashed noise.
    /// </summary>
    public static class GrassWindNoise
    {
        /// <summary>Texture edge in pixels.</summary>
        public const int Size = 128;

        /// <summary>Lattice cells per tile; FF_WIND_CELLS in FoliageForgeGrass.hlsl must match.</summary>
        public const int Cells = 16;

        static readonly int TextureId = Shader.PropertyToID("_FF_WindNoise");
        static Texture2D _texture;

        private static float Lattice(int x, int y)
        {
            x = ((x % Cells) + Cells) % Cells;
            y = ((y % Cells) + Cells) % Cells;
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFFu) / 16777216f;
            }
        }

        /// <summary>Noise in [0,1] at texture coordinates (u, v); periodic with period 1 in both.</summary>
        public static float Sample(float u, float v)
        {
            float px = u * Cells, py = v * Cells;
            int ix = Mathf.FloorToInt(px), iy = Mathf.FloorToInt(py);
            float fx = px - ix, fy = py - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(Lattice(ix, iy), Lattice(ix + 1, iy), fx), Mathf.Lerp(Lattice(ix, iy + 1), Lattice(ix + 1, iy + 1), fx), fy);
        }

        /// <summary>Texel values (row-major, 0..255) sampled at pixel centres, so bilinear wrap-around is seamless.</summary>
        public static byte[] Generate()
        {
            var data = new byte[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    data[y * Size + x] = (byte)Mathf.RoundToInt(Sample((x + 0.5f) / Size, (y + 0.5f) / Size) * 255f);
            return data;
        }

        /// <summary>Creates the texture if needed and binds it as the global _FF_WindNoise. Cheap to call every frame.</summary>
        public static bool Ensure()
        {
            if (_texture == null)
            {
                _texture = new Texture2D(Size, Size, TextureFormat.R8, false, true)
                {
                    name = "FF_WindNoise",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _texture.SetPixelData(Generate(), 0);
                _texture.Apply(false, true);
            }
            Shader.SetGlobalTexture(TextureId, _texture);
            return true;
        }
    }

    /// <summary>Distance LOD helpers: mesh level per chunk and CPU clump thinning that matches the shader's hash and share curve.</summary>
    public static class GrassLod
    {
        private static uint Pcg(uint v)
        {
            uint s = v * 747796405u + 2891336453u;
            uint w = ((s >> (int)((s >> 28) + 4u)) ^ s) * 277803737u;
            return (w >> 22) ^ w;
        }

        /// <summary>Per-clump keep value in [0,1) from the clump origin; the shader keeps the clump while this is at most the share.</summary>
        public static float Keep(float x, float z)
        {
            uint a = (uint)(int)Mathf.Floor(x * 1000f + 0.5f);
            uint b = (uint)(int)Mathf.Floor(z * 1000f + 0.5f);
            return (Pcg(a ^ Pcg(b)) & 0xFFFFFFu) / 16777216f;
        }

        /// <summary>Share of clumps kept at a distance: 1 up to lodNear, easing down to minShare at lodFar (metres).</summary>
        public static float Share(float dist, float lodNear, float lodFar, float minShare)
        {
            float far = Mathf.Max(lodFar, lodNear + 0.01f);
            float t = Mathf.Clamp01((dist - lodNear) / (far - lodNear));
            return Mathf.Lerp(1f, minShare, t * t * (3f - 2f * t));
        }

        /// <summary>How many leading keys (sorted ascending) are at most the share.</summary>
        public static int PrefixCount(float[] sortedKeys, float share)
        {
            int lo = 0, hi = sortedKeys.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (sortedKeys[mid] <= share) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }

        /// <summary>Mesh LOD for a chunk at its nearest distance: 0 below LodMid, 1 below LodFar (both fractions of the draw distance), else 2.</summary>
        public static int MeshLevel(float dist, float drawDistance, float lodMid, float lodFar)
        {
            if (dist < lodMid * drawDistance) return 0;
            return dist < lodFar * drawDistance ? 1 : 2;
        }
    }
}
