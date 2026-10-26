using System;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Grass look presets. A biome sets the grass types, colours and wind feel; the quality tier still owns density scaling and the costly features.</summary>
    public enum GrassBiome { Meadow, Forest, Tundra, Desert, Alien, Savanna, Swamp, Autumn, Ashlands }

    /// <summary>How a biome is applied: Replace swaps the whole look, AddTypes only merges the biome's grass types into the list.</summary>
    public enum GrassApplyMode { Replace, AddTypes }

    public static class GrassBiomes
    {
        private static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

        // each entry is one simple ingredient: a stock sprite from Textures/Flat and the type it scatters
        static (string sprite, GrassVariant type)[] Table(GrassBiome b)
        {
            switch (b)
            {
                case GrassBiome.Forest:
                    return new[]
                    {
                    ("0001", new GrassVariant { Name = "Moss lawn", Density = 12f, Height = new Vector2(0.12f, 0.22f), Width = new Vector2(0.55f, 0.8f), Bottom = C(0.04f, 0.15f, 0.06f), Top = C(0.14f, 0.4f, 0.14f), Tip = C(0.38f, 0.62f, 0.25f, 0.4f), WindWeight = 0.6f }),
                    ("0041", new GrassVariant { Name = "Fern", Density = 3f, Clustering = 0.8f, ClusterScale = 0.12f, Height = new Vector2(0.5f, 0.85f), Width = new Vector2(0.9f, 1.3f), Cards = 4, Bend = 0.5f, Spread = 0.2f, Bottom = C(0.05f, 0.2f, 0.08f), Top = C(0.2f, 0.5f, 0.17f), Tip = C(0.45f, 0.72f, 0.3f, 0.5f), WindWeight = 0.9f }),
                    ("0016", new GrassVariant { Name = "Broad blades", Density = 3f, Clustering = 0.5f, ClusterScale = 0.1f, Height = new Vector2(0.3f, 0.5f), Width = new Vector2(0.6f, 0.9f), Bottom = C(0.05f, 0.18f, 0.07f), Top = C(0.18f, 0.45f, 0.16f), Tip = C(0.4f, 0.65f, 0.28f, 0.4f), WindWeight = 0.8f }),
                    ("0026", new GrassVariant { Name = "Mushrooms", Density = 0.4f, Clustering = 0.95f, ClusterScale = 0.09f, Height = new Vector2(0.15f, 0.3f), Width = new Vector2(0.4f, 0.6f), Cards = 2, Bend = 0.02f, Bottom = C(0.75f, 0.7f, 0.6f), Top = C(0.6f, 0.35f, 0.2f), Tip = C(0.85f, 0.45f, 0.3f, 0.8f), TipStart = 0.6f, TipHueVariation = 0.1f, WindWeight = 0.1f }),
                };
                case GrassBiome.Tundra:
                    return new[]
                    {
                    ("0001", new GrassVariant { Name = "Moss", Density = 9f, Height = new Vector2(0.1f, 0.2f), Width = new Vector2(0.5f, 0.75f), Bottom = C(0.2f, 0.22f, 0.12f), Top = C(0.48f, 0.5f, 0.28f), Tip = C(0.8f, 0.88f, 0.9f, 0.45f), WindWeight = 0.8f }),
                    ("0011", new GrassVariant { Name = "Wind grass", Density = 3f, Clustering = 0.6f, ClusterScale = 0.1f, Height = new Vector2(0.35f, 0.6f), Width = new Vector2(0.6f, 0.9f), Bend = 0.45f, Bottom = C(0.28f, 0.25f, 0.14f), Top = C(0.62f, 0.56f, 0.34f), Tip = C(0.9f, 0.95f, 0.98f, 0.6f), TipStart = 0.6f, WindWeight = 1.7f }),
                    ("0017", new GrassVariant { Name = "Dry sprig", Density = 1f, Clustering = 0.85f, ClusterScale = 0.08f, Height = new Vector2(0.25f, 0.45f), Width = new Vector2(0.5f, 0.8f), Cards = 2, Bend = 0.2f, Bottom = C(0.3f, 0.27f, 0.18f), Top = C(0.6f, 0.55f, 0.42f), Tip = C(0.9f, 0.92f, 0.95f, 0.5f), WindWeight = 1.4f }),
                };
                case GrassBiome.Desert:
                    return new[]
                    {
                    ("0017", new GrassVariant { Name = "Dry sprigs", Density = 3f, Clustering = 0.8f, ClusterScale = 0.1f, Height = new Vector2(0.15f, 0.3f), Width = new Vector2(0.5f, 0.8f), Cards = 2, Bend = 0.3f, Bottom = C(0.35f, 0.27f, 0.12f), Top = C(0.72f, 0.58f, 0.28f), Tip = C(0.92f, 0.82f, 0.5f, 0.5f), WindWeight = 1.1f }),
                    ("0019", new GrassVariant { Name = "Spiky succulent", Density = 1.2f, Clustering = 0.9f, ClusterScale = 0.07f, Height = new Vector2(0.45f, 0.85f), Width = new Vector2(0.5f, 0.8f), Cards = 4, Bend = 0.12f, Spread = 0.06f, Bottom = C(0.3f, 0.3f, 0.12f), Top = C(0.62f, 0.62f, 0.3f), Tip = C(0.9f, 0.88f, 0.55f, 0.5f), WindWeight = 0.9f }),
                    ("0004", new GrassVariant { Name = "Sparse blades", Density = 0.8f, Clustering = 0.85f, ClusterScale = 0.08f, Height = new Vector2(0.3f, 0.5f), Width = new Vector2(0.5f, 0.8f), Cards = 2, Bend = 0.3f, Bottom = C(0.38f, 0.33f, 0.15f), Top = C(0.75f, 0.68f, 0.35f), Tip = C(0.95f, 0.88f, 0.6f, 0.5f), WindWeight = 1.2f }),
                };
                case GrassBiome.Alien:
                    return new[]
                    {
                    ("0001", new GrassVariant { Name = "Glow carpet", Density = 12f, Height = new Vector2(0.2f, 0.35f), Width = new Vector2(0.55f, 0.85f), Bottom = C(0.05f, 0.05f, 0.25f), Top = C(0.1f, 0.6f, 0.65f), Tip = C(0.4f, 1f, 0.85f, 0.7f), TipStart = 0.6f, TipHueVariation = 0.3f, WindWeight = 1.1f }),
                    ("0014", new GrassVariant { Name = "Curl reed", Density = 3.5f, Clustering = 0.65f, ClusterScale = 0.1f, Height = new Vector2(0.8f, 1.4f), Width = new Vector2(0.6f, 0.9f), Cards = 3, Bend = 0.6f, Bottom = C(0.2f, 0.04f, 0.3f), Top = C(0.55f, 0.15f, 0.7f), Tip = C(1f, 0.4f, 0.9f, 0.8f), TipStart = 0.55f, TipHueVariation = 0.5f, WindWeight = 1.5f }),
                    ("0010", new GrassVariant { Name = "Spore stalks", Density = 0.9f, Clustering = 0.9f, ClusterScale = 0.08f, Height = new Vector2(0.3f, 0.6f), Width = new Vector2(0.5f, 0.8f), Cards = 2, Bend = 0.15f, Bottom = C(0.05f, 0.2f, 0.25f), Top = C(0.2f, 0.7f, 0.5f), Tip = C(1f, 0.95f, 0.3f, 1f), TipStart = 0.5f, TipHueVariation = 0.9f, WindWeight = 0.6f }),
                };
                case GrassBiome.Savanna:
                    return new[]
                    {
                    ("0017", new GrassVariant { Name = "Dry tufts", Density = 5f, Clustering = 0.5f, ClusterScale = 0.1f, Height = new Vector2(0.2f, 0.4f), Width = new Vector2(0.6f, 0.9f), Cards = 3, Bend = 0.25f, Bottom = C(0.33f, 0.25f, 0.1f), Top = C(0.78f, 0.62f, 0.25f), Tip = C(0.95f, 0.85f, 0.5f, 0.5f), WindWeight = 1.1f }),
                    ("0006", new GrassVariant { Name = "Tall straw", Density = 3f, Clustering = 0.6f, ClusterScale = 0.09f, Height = new Vector2(0.8f, 1.3f), Width = new Vector2(0.7f, 1f), Bend = 0.4f, Bottom = C(0.4f, 0.3f, 0.1f), Top = C(0.85f, 0.7f, 0.3f), Tip = C(0.98f, 0.9f, 0.55f, 0.6f), TipStart = 0.65f, WindWeight = 1.4f }),
                    ("0019", new GrassVariant { Name = "Thorn sprig", Density = 0.8f, Clustering = 0.9f, ClusterScale = 0.07f, Height = new Vector2(0.4f, 0.7f), Width = new Vector2(0.5f, 0.8f), Cards = 4, Bend = 0.1f, Spread = 0.06f, Bottom = C(0.25f, 0.2f, 0.08f), Top = C(0.5f, 0.45f, 0.2f), Tip = C(0.8f, 0.7f, 0.35f, 0.4f), WindWeight = 0.7f }),
                };
                case GrassBiome.Swamp:
                    return new[]
                    {
                    ("0014", new GrassVariant { Name = "Reeds", Density = 3f, Clustering = 0.7f, ClusterScale = 0.1f, Height = new Vector2(0.9f, 1.5f), Width = new Vector2(0.5f, 0.8f), Cards = 3, Bend = 0.3f, Bottom = C(0.06f, 0.14f, 0.1f), Top = C(0.2f, 0.4f, 0.28f), Tip = C(0.5f, 0.6f, 0.35f, 0.5f), WindWeight = 0.8f }),
                    ("0001", new GrassVariant { Name = "Moss mat", Density = 12f, Height = new Vector2(0.08f, 0.16f), Width = new Vector2(0.55f, 0.8f), Bottom = C(0.05f, 0.12f, 0.06f), Top = C(0.2f, 0.35f, 0.14f), Tip = C(0.4f, 0.5f, 0.22f, 0.4f), WindWeight = 0.3f }),
                    ("0010", new GrassVariant { Name = "Cattail", Density = 0.8f, Clustering = 0.9f, ClusterScale = 0.08f, Height = new Vector2(0.7f, 1.1f), Width = new Vector2(0.5f, 0.7f), Cards = 2, Bend = 0.12f, Bottom = C(0.08f, 0.16f, 0.1f), Top = C(0.25f, 0.4f, 0.2f), Tip = C(0.35f, 0.2f, 0.1f, 0.9f), TipStart = 0.7f, WindWeight = 0.6f }),
                    ("0041", new GrassVariant { Name = "Fern", Density = 1.5f, Clustering = 0.8f, ClusterScale = 0.12f, Height = new Vector2(0.4f, 0.7f), Width = new Vector2(0.8f, 1.1f), Cards = 4, Bend = 0.4f, Spread = 0.15f, Bottom = C(0.04f, 0.14f, 0.08f), Top = C(0.15f, 0.38f, 0.22f), Tip = C(0.35f, 0.55f, 0.3f, 0.4f), WindWeight = 0.7f }),
                };
                case GrassBiome.Autumn:
                    return new[]
                    {
                    ("0001", new GrassVariant { Name = "Short lawn", Density = 13f, Height = new Vector2(0.2f, 0.35f), Width = new Vector2(0.55f, 0.8f), Bottom = C(0.2f, 0.16f, 0.05f), Top = C(0.55f, 0.45f, 0.15f), Tip = C(0.85f, 0.6f, 0.2f, 0.5f), WindWeight = 1f }),
                    ("0006", new GrassVariant { Name = "Russet blades", Density = 3.5f, Clustering = 0.65f, ClusterScale = 0.1f, Height = new Vector2(0.55f, 0.95f), Width = new Vector2(0.7f, 1f), Bend = 0.35f, Bottom = C(0.28f, 0.14f, 0.05f), Top = C(0.7f, 0.35f, 0.1f), Tip = C(0.95f, 0.55f, 0.15f, 0.6f), TipHueVariation = 0.12f, WindWeight = 1.3f }),
                    ("0016", new GrassVariant { Name = "Leaf litter", Density = 2f, Clustering = 0.85f, ClusterScale = 0.09f, Height = new Vector2(0.05f, 0.12f), Width = new Vector2(0.7f, 1.1f), Cards = 2, Bend = 0.05f, Bottom = C(0.3f, 0.12f, 0.05f), Top = C(0.75f, 0.3f, 0.1f), Tip = C(0.9f, 0.4f, 0.12f, 0.7f), TipHueVariation = 0.2f, WindWeight = 0.4f }),
                    ("0041", new GrassVariant { Name = "Fern", Density = 1.2f, Clustering = 0.8f, ClusterScale = 0.12f, Height = new Vector2(0.4f, 0.7f), Width = new Vector2(0.7f, 1f), Cards = 4, Bend = 0.4f, Spread = 0.15f, Bottom = C(0.25f, 0.15f, 0.05f), Top = C(0.6f, 0.4f, 0.12f), Tip = C(0.8f, 0.5f, 0.2f, 0.4f), WindWeight = 0.9f }),
                };
                case GrassBiome.Ashlands:
                    return new[]
                    {
                    ("0001", new GrassVariant { Name = "Ash lawn", Density = 9f, Height = new Vector2(0.1f, 0.2f), Width = new Vector2(0.55f, 0.8f), Bottom = C(0.08f, 0.08f, 0.08f), Top = C(0.28f, 0.27f, 0.26f), Tip = C(0.5f, 0.48f, 0.46f, 0.4f), WindWeight = 1.2f }),
                    ("0011", new GrassVariant { Name = "Charred stalks", Density = 2.5f, Clustering = 0.7f, ClusterScale = 0.1f, Height = new Vector2(0.35f, 0.7f), Width = new Vector2(0.5f, 0.8f), Cards = 2, Bend = 0.3f, Bottom = C(0.04f, 0.04f, 0.04f), Top = C(0.16f, 0.15f, 0.15f), Tip = C(0.35f, 0.3f, 0.28f, 0.4f), WindWeight = 1.5f }),
                    ("0017", new GrassVariant { Name = "Ember sprigs", Density = 0.9f, Clustering = 0.9f, ClusterScale = 0.08f, Height = new Vector2(0.25f, 0.5f), Width = new Vector2(0.5f, 0.8f), Cards = 2, Bend = 0.2f, Bottom = C(0.06f, 0.05f, 0.05f), Top = C(0.3f, 0.12f, 0.06f), Tip = C(1f, 0.45f, 0.1f, 0.9f), TipStart = 0.55f, TipHueVariation = 0.1f, WindWeight = 1.3f }),
                };
                default:
                    return new[]
                    {
                    ("0001", new GrassVariant { Name = "Short", Density = 14f, Height = new Vector2(0.25f, 0.4f), Width = new Vector2(0.55f, 0.8f), Cards = 3 }),
                    ("0006", new GrassVariant { Name = "Tall", Density = 4f, Clustering = 0.7f, ClusterScale = 0.1f, Height = new Vector2(0.7f, 1.1f), Width = new Vector2(0.7f, 1f), Bend = 0.35f, Bottom = C(0.12f, 0.3f, 0.08f), Top = C(0.45f, 0.72f, 0.2f), Tip = C(0.85f, 0.9f, 0.4f, 0.5f), WindWeight = 1.3f }),
                    ("0041", new GrassVariant { Name = "Fern", Density = 1.5f, Clustering = 0.8f, ClusterScale = 0.12f, Height = new Vector2(0.4f, 0.7f), Width = new Vector2(0.7f, 1f), Cards = 4, Bend = 0.4f, Spread = 0.15f, Bottom = C(0.06f, 0.22f, 0.08f), Top = C(0.25f, 0.55f, 0.18f), Tip = C(0.5f, 0.75f, 0.3f, 0.4f), WindWeight = 0.9f }),
                    ("0017", new GrassVariant { Name = "Dry sprig", Density = 1f, Clustering = 0.85f, ClusterScale = 0.08f, Height = new Vector2(0.3f, 0.55f), Width = new Vector2(0.5f, 0.8f), Cards = 2, Bend = 0.2f, Bottom = C(0.35f, 0.3f, 0.12f), Top = C(0.75f, 0.65f, 0.32f), Tip = C(0.92f, 0.85f, 0.55f, 0.5f), WindWeight = 1.2f }),
                };
            }
        }

        /// <summary>The simple grass types of a biome at full (Lush) density, Enabled and Mix 100. Texture is not part of it (see <see cref="Sprites"/>).</summary>
        public static GrassVariant[] Variants(GrassBiome b) => Array.ConvertAll(Table(b), e =>
        {
            e.type.SourceId = $"{b}.{e.type.Name}";
            return e.type;
        });

        /// <summary>
        /// The biome ingredient a type came from: matched by <paramref name="sourceId"/> across every biome, or, for types made before ids
        /// existed, by <paramref name="name"/> within <paramref name="fallback"/>. Null for hand-made types.
        /// </summary>
        public static GrassVariant Default(string sourceId, string name, GrassBiome fallback)
        {
            if (!string.IsNullOrEmpty(sourceId))
            {
                int dot = sourceId.IndexOf('.');
                if (dot > 0 && Enum.TryParse(sourceId.Substring(0, dot), out GrassBiome b))
                    return Array.Find(Variants(b), v => v.SourceId == sourceId);
                return null;
            }
            return Array.Find(Variants(fallback), v => v.Name == name);
        }

        /// <summary>Stock sprite number (Textures/Flat/sprite_####.png) of each type in <see cref="Variants"/>, same order.</summary>
        public static string[] Sprites(GrassBiome b) => Array.ConvertAll(Table(b), e => e.sprite);

        /// <summary>
        /// Replace: swaps the profile's grass types for the biome's (a type from the same ingredient keeps its sprite, on/off and Mix %;
        /// the others get the biome's stock sprite in the editor) and sets the profile colours and wind feel.
        /// AddTypes: appends the biome's types the profile does not have yet and leaves everything else untouched.
        /// </summary>
        public static void Apply(GrassProfile p, GrassBiome b, GrassApplyMode mode = GrassApplyMode.Replace)
        {
            GrassVariant[] src = Variants(b);
            string[] sprites = Sprites(b);
            if (mode == GrassApplyMode.AddTypes)
            {
                var list = new System.Collections.Generic.List<GrassVariant>(p.Variants ?? new GrassVariant[0]);
                for (int i = 0; i < src.Length; i++)
                {
                    if (list.Exists(v => v.SourceId == src[i].SourceId)) continue;
#if UNITY_EDITOR
                    src[i].Texture = GrassProfile.LoadSprite(sprites[i]);
#endif
                    list.Add(src[i]);
                }
                p.Variants = list.ToArray();
                p.Revision++;
                return;
            }
            for (int i = 0; i < src.Length; i++)
            {
                GrassVariant keep = p.Variants != null ? Array.Find(p.Variants, v => v.SourceId == src[i].SourceId || (string.IsNullOrEmpty(v.SourceId) && v.Name == src[i].Name)) : null;
                if (keep != null)
                {
                    src[i].Texture = keep.Texture;
                    src[i].Enabled = keep.Enabled;
                    src[i].Mix = keep.Mix;
                }
#if UNITY_EDITOR
                else src[i].Texture = GrassProfile.LoadSprite(sprites[i]);
#endif
            }
            p.Variants = src;
            p.Biome = b;

            p.ShadowColor = C(0.3f, 0.45f, 0.65f, 0.8f);
            p.GroundColor = C(0.16f, 0.24f, 0.1f, 0.85f);
            p.PatchColor = C(0.8f, 0.7f, 0.3f, 0.8f);
            p.PatchAmount = 0.3f;
            p.RimColor = C(0.9f, 1f, 0.7f);
            p.RimStrength = 0.4f;
            p.TranslucencyColor = C(0.7f, 1f, 0.3f);
            p.GustColor = C(0.95f, 0.83f, 0.78f, 0.3f);
            p.GustIntensity = 0.3f;
            p.GustSpeed = 0.6f;
            p.Flutter = 0.04f;
            p.LightContribution = 1f;

            switch (b)
            {
                case GrassBiome.Forest:
                    p.ShadowColor = C(0.12f, 0.25f, 0.3f, 0.85f);
                    p.GroundColor = C(0.1f, 0.12f, 0.06f, 0.9f);
                    p.PatchColor = C(0.35f, 0.22f, 0.1f, 0.8f); // leaf litter
                    p.PatchAmount = 0.35f;
                    p.TranslucencyColor = C(0.55f, 0.9f, 0.25f);
                    p.GustIntensity = 0.2f;
                    p.GustSpeed = 0.4f;
                    break;
                case GrassBiome.Tundra:
                    p.ShadowColor = C(0.35f, 0.5f, 0.75f, 0.9f);
                    p.GroundColor = C(0.35f, 0.33f, 0.28f, 0.8f);
                    p.PatchColor = C(0.92f, 0.96f, 1f, 0.9f); // snow
                    p.PatchAmount = 0.4f;
                    p.RimColor = C(0.85f, 0.95f, 1f);
                    p.TranslucencyColor = C(0.8f, 0.9f, 1f);
                    p.GustColor = C(0.85f, 0.92f, 1f, 0.4f);
                    p.GustIntensity = 0.45f;
                    p.GustSpeed = 1.1f;
                    break;
                case GrassBiome.Desert:
                    p.ShadowColor = C(0.55f, 0.35f, 0.5f, 0.8f);
                    p.GroundColor = C(0.62f, 0.5f, 0.3f, 0.85f);
                    p.PatchColor = C(0.85f, 0.7f, 0.45f, 0.9f); // bare sand
                    p.PatchAmount = 0.45f;
                    p.RimColor = C(1f, 0.9f, 0.6f);
                    p.TranslucencyColor = C(1f, 0.8f, 0.35f);
                    p.GustColor = C(1f, 0.88f, 0.65f, 0.35f);
                    p.GustSpeed = 0.9f;
                    break;
                case GrassBiome.Alien:
                    p.ShadowColor = C(0.2f, 0.1f, 0.5f, 0.9f);
                    p.GroundColor = C(0.08f, 0.04f, 0.2f, 0.85f);
                    p.PatchColor = C(0.1f, 0.9f, 0.8f, 0.8f);
                    p.PatchAmount = 0.35f;
                    p.RimColor = C(0.3f, 1f, 0.95f);
                    p.RimStrength = 0.9f;
                    p.TranslucencyColor = C(1f, 0.3f, 0.85f);
                    p.GustColor = C(0.4f, 1f, 0.9f, 0.5f);
                    p.GustIntensity = 0.4f;
                    p.Flutter = 0.08f;
                    p.LightContribution = 0.75f;
                    break;
                case GrassBiome.Savanna:
                    p.ShadowColor = C(0.5f, 0.35f, 0.4f, 0.8f);
                    p.GroundColor = C(0.45f, 0.32f, 0.15f, 0.85f);
                    p.PatchColor = C(0.85f, 0.65f, 0.3f, 0.85f);
                    p.PatchAmount = 0.4f;
                    p.RimColor = C(1f, 0.85f, 0.45f);
                    p.TranslucencyColor = C(1f, 0.75f, 0.3f);
                    p.GustColor = C(1f, 0.85f, 0.55f, 0.35f);
                    p.GustSpeed = 0.7f;
                    break;
                case GrassBiome.Swamp:
                    p.ShadowColor = C(0.08f, 0.2f, 0.25f, 0.9f);
                    p.GroundColor = C(0.07f, 0.1f, 0.06f, 0.95f);
                    p.PatchColor = C(0.15f, 0.25f, 0.18f, 0.8f);
                    p.PatchAmount = 0.35f;
                    p.RimStrength = 0.2f;
                    p.TranslucencyColor = C(0.4f, 0.8f, 0.4f);
                    p.GustColor = C(0.6f, 0.8f, 0.7f, 0.2f);
                    p.GustIntensity = 0.15f;
                    p.GustSpeed = 0.3f;
                    p.Flutter = 0.02f;
                    break;
                case GrassBiome.Autumn:
                    p.ShadowColor = C(0.4f, 0.25f, 0.4f, 0.8f);
                    p.GroundColor = C(0.25f, 0.15f, 0.07f, 0.9f);
                    p.PatchColor = C(0.7f, 0.25f, 0.1f, 0.85f);
                    p.PatchAmount = 0.4f;
                    p.RimColor = C(1f, 0.75f, 0.4f);
                    p.TranslucencyColor = C(1f, 0.6f, 0.2f);
                    p.GustColor = C(1f, 0.6f, 0.3f, 0.4f);
                    p.GustIntensity = 0.5f;
                    p.GustSpeed = 1f;
                    break;
                case GrassBiome.Ashlands:
                    p.ShadowColor = C(0.2f, 0.15f, 0.2f, 0.9f);
                    p.GroundColor = C(0.07f, 0.06f, 0.06f, 0.95f);
                    p.PatchColor = C(0.45f, 0.15f, 0.05f, 0.8f);
                    p.PatchAmount = 0.3f;
                    p.RimColor = C(1f, 0.5f, 0.15f);
                    p.RimStrength = 0.6f;
                    p.TranslucencyColor = C(1f, 0.4f, 0.1f);
                    p.GustColor = C(0.5f, 0.45f, 0.45f, 0.4f);
                    p.GustIntensity = 0.4f;
                    p.GustSpeed = 1.3f;
                    p.Flutter = 0.08f;
                    p.LightContribution = 0.8f;
                    break;
            }
            p.Revision++;
        }
    }
}
