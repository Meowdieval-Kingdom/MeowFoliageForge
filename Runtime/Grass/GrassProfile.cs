using System;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>One grass type (a single ingredient such as short blades or fern): a clump of textured cards, scattered at its own density times its Mix %.</summary>
    [Serializable]
    public sealed class GrassVariant
    {
        public string Name = "Grass";
        [Tooltip("Biome ingredient this type came from (e.g. Forest.Fern). Quality scaling and biome Apply match on it, so renaming the type is safe. Empty on hand-made types.")]
        public string SourceId;
        public bool Enabled = true;
        [Tooltip("Mix %: how much of this type's Density is used where types are combined (Grass Field fill and paint, window preview). 100 = full density, 0 = none. Baked batches ignore it; the brush palette has its own Mix %.")]
        [Range(0f, 100f)] public float Mix = 100f;
        [Tooltip("Cutout sprite (alpha = shape), tinted by the colours below. Empty uses plain blades when the Texture feature is off.")]
        public Texture2D Texture;

        [Header("Scatter")]
        [Tooltip("Clumps per square metre.")]
        [Min(0f)] public float Density = 6f;
        [Tooltip("0 = even carpet, 1 = grows only in patches.")]
        [Range(0f, 1f)] public float Clustering;
        [Tooltip("Patch size: noise frequency per metre.")]
        [Min(0.001f)] public float ClusterScale = 0.12f;

        [Header("Size (metres)")]
        public Vector2 Height = new Vector2(0.35f, 0.55f);
        public Vector2 Width = new Vector2(0.7f, 1f);

        [Header("Clump mesh")]
        [Tooltip("Cards crossed through the clump. More = thicker, more triangles.")]
        [Range(1, 6)] public int Cards = 3;
        [Tooltip("Vertical segments per card; the wind bends them.")]
        [Range(1, 4)] public int Segments = 2;
        [Tooltip("Static forward curve of each card.")]
        [Range(0f, 0.6f)] public float Bend = 0.2f;
        [Tooltip("How far the cards sit from the clump centre.")]
        [Range(0f, 0.5f)] public float Spread = 0.12f;

        [Header("Colour")]
        public Color Bottom = new Color(0.1f, 0.26f, 0.07f);
        public Color Top = new Color(0.4f, 0.7f, 0.18f);
        [Tooltip("Tip colour. Alpha = how strongly the tip is painted.")]
        public Color Tip = new Color(0.66f, 0.88f, 0.32f, 0.6f);
        [Range(0f, 1f)] public float TipStart = 0.75f;
        [Tooltip("Random hue shift of the tip per clump (flowers).")]
        [Range(0f, 1f)] public float TipHueVariation;
        [Range(0f, 2f)] public float WindWeight = 1f;

        /// <summary>Clumps per square metre after Mix; 0 when the type is disabled.</summary>
        public float MixedDensity => Enabled ? ApplyMix(Density, Mix) : 0f;

        /// <summary><paramref name="density"/> scaled by <paramref name="mixPercent"/> (clamped to 0..100); never negative.</summary>
        public static float ApplyMix(float density, float mixPercent) => Mathf.Max(0f, density) * Mathf.Clamp(mixPercent, 0f, 100f) / 100f;

        /// <summary>Copy of this variant, used by presets.</summary>
        public GrassVariant Clone() => (GrassVariant)MemberwiseClone();
    }

    /// <summary>
    /// Every shader feature that can be switched off to save GPU time. Off features cost one skipped branch.
    /// </summary>
    [Serializable]
    public sealed class GrassFeatures
    {
        [Tooltip("Bend with the shared Foliage Wind. Off also skips the gust noise.")] public bool Wind = true;
        [Tooltip("Sample the sprite for the card shape. Off draws three plain blades per card and saves the texture fetch.")] public bool Texture = true;
        [Tooltip("Bottom-to-top colour ramp, tip colour and root darkening. Off uses the Top colour flat.")] public bool Gradient = true;
        [Tooltip("Per-clump hue and brightness variation.")] public bool Variation = true;
        [Tooltip("World-space dry patches of a second colour.")] public bool Patches;
        [Tooltip("Banded toon lighting with a coloured shadow side. Off uses soft half-Lambert.")] public bool StylizedShading = true;
        [Tooltip("Fresnel highlight on the tips.")] public bool Rim;
        [Tooltip("Sun glowing through blades when it is behind them.")] public bool Translucency;
        [Tooltip("Darken the roots toward the ground colour so the grass sits in the soil.")] public bool GroundBlend = true;
        [Tooltip("Shrink clumps to nothing near the draw distance instead of popping.")] public bool DistanceFade = true;
        [Tooltip("Sample the main-light shadow map.")] public bool ReceiveShadows;
        [Tooltip("Cast shadows. Expensive on dense grass; usually leave off.")] public bool CastShadows;
        [Tooltip("Thin the lawn smoothly with distance on the GPU: random clumps drop out and the rest widen.")] public bool DistanceLod = true;
        [Tooltip("Smooth alpha-tested edges (needs MSAA) and keep thin blades from vanishing in the distance.")] public bool AlphaToCoverage = true;
        [Tooltip("Light from point and spot lights (campfires, spells). Needs additional lights enabled on the URP asset.")] public bool AdditionalLights;
        [Tooltip("Bend away from GrassTrampler objects (up to 16 nearest). Used when Trample Map is off.")] public bool Trample;
        [Tooltip("Tint the grass toward the Ground Map assigned on the Grass Field (capture it from the field's inspector).")] public bool GroundMap;
        [Tooltip("Quibli-style gusts: bright bands roll through the grass along the wind, swinging card edges and lifting the colour.")] public bool Gusts = true;
        [Tooltip("Skip chunks outside the camera view.")] public bool FrustumCulling = true;
        [Tooltip("Use simpler clump meshes (fewer cards, one segment) on far chunks.")] public bool MeshLod;
        [Tooltip("Skip the clumps the shader would thin out anyway, per chunk on the CPU. Needs Distance Lod.")] public bool CpuThinning = true;
        [Tooltip("Read gust noise from one shared tiling texture (generated in code) instead of hashing it per vertex. Fewer shader instructions, same noise scale. Off keeps the hashed noise.")] public bool WindTexture = true;
        [Tooltip("Camera-following trample map: every Grass Trampler in range is stamped into a texture that fades over time, so footprints stay and recover. Replaces the 16-pusher Trample list; needs a half-float render texture.")] public bool TrampleRT;
        [Tooltip("Tint grass that is lying flat in the trample map toward the trail colours. Needs Trample Map.")] public bool TrailTint;
        [Tooltip("Shape how the light lands on the grass with the Light Ramp texture, so lit grass is not brighter than its surroundings. Needs a ramp assigned under Light Ramp.")] public bool LightRamp;
        [Tooltip("Widen cards that are seen edge-on so grass does not thin out at grazing and top-down angles.")] public bool PerspectiveCorrection;

        /// <summary>How many of the switches are on.</summary>
        public int EnabledCount()
        {
            bool[] all = { Wind, Texture, Gradient, Variation, Patches, StylizedShading, Rim, Translucency, GroundBlend, DistanceFade, ReceiveShadows,
                CastShadows, DistanceLod, AlphaToCoverage, AdditionalLights, Trample, GroundMap, Gusts, FrustumCulling, MeshLod, CpuThinning,
                WindTexture, TrampleRT, TrailTint, LightRamp, PerspectiveCorrection };
            int n = 0;
            foreach (bool b in all) if (b) n++;
            return n;
        }
    }

    /// <summary>Settings for one grass look: a list of simple grass types, feature switches, and the shared shading and draw settings.</summary>
    [CreateAssetMenu(fileName = "NewGrassProfile", menuName = "Meowdieval/Foliage Forge/Grass Profile")]
    public sealed class GrassProfile : ScriptableObject
    {
        public enum Quality { Lush, Balanced, Light }

        public Shader Shader;
        [Tooltip("Last look preset applied. Quality tiers scale this biome's densities.")]
        public GrassBiome Biome;
        [Tooltip("Grass types: one simple ingredient each (add, remove, reorder, switch off). Each scatters at Density x Mix %.")]
        public GrassVariant[] Variants = Defaults();
        public GrassFeatures Features = new GrassFeatures();

        [Header("Draw")]
        [Tooltip("Grass beyond this distance (metres) is not drawn.")]
        [Min(5f)] public float DrawDistance = 60f;
        [Tooltip("Chunk edge in metres. Chunks are the culling and LOD unit.")]
        [Range(4f, 64f)] public float ChunkSize = 16f;
        [Tooltip("Fraction of the draw distance where thinning starts.")]
        [Range(0.05f, 0.9f)] public float LodMid = 0.3f;
        [Tooltip("Fraction of the draw distance where thinning reaches its minimum.")]
        [Range(0.2f, 1f)] public float LodFar = 0.75f;
        [Tooltip("Share of clumps kept at the far end of the LOD range.")]
        [Range(0.05f, 1f)] public float LodMinShare = 0.3f;
        [Tooltip("Hard cap on clumps across all variants.")]
        [Min(1000)] public int MaxInstances = 300000;

        [Header("Placement")]
        public int Seed = 1;
        [Tooltip("Ground steeper than this gets no grass (degrees).")]
        [Range(0f, 90f)] public float SlopeLimit = 40f;
        [Tooltip("Tilt clumps toward the ground normal.")]
        [Range(0f, 1f)] public float NormalAlign = 0.25f;

        [Header("Shading")]
        [Range(0f, 1f)] public float AlphaCutoff = 0.5f;
        [Range(0f, 1f)] public float RootDarkening = 0.35f;
        [Range(0.2f, 3f)] public float GradientBias = 1f;
        [Range(0f, 1f)] public float ColorVariation = 0.4f;
        public Color PatchColor = new Color(0.8f, 0.7f, 0.3f, 0.8f);
        [Range(0f, 1f)] public float PatchAmount = 0.3f;
        public float PatchScale = 0.08f;
        [Tooltip("Colour of the shadow side. Alpha = how far it goes.")]
        public Color ShadowColor = new Color(0.3f, 0.45f, 0.65f, 0.8f);
        [Range(0.01f, 1f)] public float ShadowSoftness = 0.25f;
        public Color RimColor = new Color(0.9f, 1f, 0.7f);
        [Range(0.5f, 8f)] public float RimPower = 3f;
        [Range(0f, 2f)] public float RimStrength = 0.4f;
        public Color TranslucencyColor = new Color(0.7f, 1f, 0.3f);
        [Range(0f, 2f)] public float TranslucencyStrength = 0.6f;
        [Tooltip("Colour the roots fade to. Alpha = amount.")]
        public Color GroundColor = new Color(0.16f, 0.24f, 0.1f, 0.85f);
        [Range(0.01f, 1f)] public float GroundBlendHeight = 0.3f;
        [Range(0f, 0.3f)] public float Flutter = 0.04f;
        [Tooltip("0 = each card shades with its own tilt (more depth), 1 = everything shades as if facing up (soft lawn).")]
        [Range(0f, 1f)] public float NormalUp = 0.7f;
        [Tooltip("Clumps further than this (metres) from the camera cast no shadow.")]
        [Min(1f)] public float ShadowDistance = 25f;
        [Range(0f, 2f)] public float AdditionalLightStrength = 1f;
        [Range(0f, 1f)] public float GroundMapStrength = 0.6f;
        [Range(0f, 2f)] public float TrampleStrength = 1f;

        [Header("Trample map (Features > Trample Map)")]
        [Tooltip("Map edge in pixels (rounded to a power of two, 64..1024). Texel size = Window Size / this.")]
        [Range(64, 1024)] public int TrampleMapResolution = 256;
        [Tooltip("Metres covered by the map around the camera (or the Grass Field's Trample Target). Grass outside it is not pushed.")]
        [Range(8f, 128f)] public float TrampleWindowSize = 48f;
        [Tooltip("Seconds for a footprint to fade to 10%. Short = grass springs back fast.")]
        [Range(0.2f, 20f)] public float TrampleRecoverySeconds = 3f;

        [Header("Trail tint (Features > Trail Tint)")]
        [Tooltip("Colour of flattened grass at the tip.")]
        public Color TrailTopColor = new Color(0.7f, 0.62f, 0.3f);
        [Tooltip("Colour of flattened grass at the root.")]
        public Color TrailBottomColor = new Color(0.25f, 0.2f, 0.08f);
        [Tooltip("0 = no tint, 1 = fully trail colour where the grass is freshly trampled; fades back as it recovers.")]
        [Range(0f, 1f)] public float TrailStrength = 0.6f;

        [Header("Light ramp (Features > Light Ramp)")]
        [Tooltip("Horizontal strip: left = unlit, right = fully lit. The sampled colour multiplies the sun colour. None = off.")]
        public Texture2D LightRamp;

        [Header("Perspective (Features > Perspective Correction)")]
        [Tooltip("How far edge-on cards are opened toward the camera. 0 = none.")]
        [Range(0f, 1f)] public float PerspectiveStrength = 0.6f;

        [Header("Gusts (wind sway and entropy come from Foliage Wind)")]
        [Range(0f, 3f)] public float GustSpeed = 0.6f;
        [Tooltip("Radians per metre along the wind; lower = wider bands.")]
        [Range(0f, 1f)] public float GustFrequency = 0.2f;
        [Range(0f, 1f)] public float GustIntensity = 0.3f;
        [Tooltip("Colour added inside a gust. Alpha = amount.")]
        public Color GustColor = new Color(0.95f, 0.83f, 0.78f, 0.3f);

        [Header("Patches and light")]
        [Tooltip("Softness of the patch edge.")]
        [Range(0f, 1f)] public float PatchBlur = 0.24f;
        [Tooltip("Slides the patch pattern across the world.")]
        public Vector2 PatchOffset;
        [Tooltip("0 = flat painted colour ignoring light, 1 = fully lit.")]
        [Range(0f, 1f)] public float LightContribution = 1f;
        [Tooltip("How much cast shadows darken the grass.")]
        [Range(0f, 1f)] public float ShadowStrength = 1f;

        /// <summary>Bumped whenever the asset is edited, so fields can rebuild.</summary>
        [NonSerialized] public int Revision;

        [SerializeField, HideInInspector] int _mixVersion;
#if UNITY_EDITOR
        [SerializeField, HideInInspector] bool _defaultsApplied;
        const string Root = "Packages/com.meowdieval.foliageforge/";

        /// <summary>Stock grass sprite from Textures/Flat by number (e.g. "0001"), or null.</summary>
        internal static Texture2D LoadSprite(string id) => UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}Textures/Flat/sprite_{id}.png");
#endif

        void OnEnable()
        {
            if (_mixVersion == 0 && Variants != null)
            {
                foreach (GrassVariant v in Variants) v.Mix = 100f; // assets saved before Mix existed
                _mixVersion = 1;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
#if UNITY_EDITOR
            if (Shader == null) Shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(Root + "Shaders/FoliageForgeGrass.shader");
            if (_defaultsApplied || Variants == null) return;
            string[] sprites = GrassBiomes.Sprites(GrassBiome.Meadow);
            bool all = true;
            for (int i = 0; i < Variants.Length && i < sprites.Length; i++)
            {
                Texture2D t = LoadSprite(sprites[i]);
                if (t == null) all = false;
                else Variants[i].Texture = t;
            }
            _defaultsApplied = all;
            if (all) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        void OnValidate() => Revision++;

        /// <summary>The stock types (Meadow biome): short blades, tall blades, fern, dry sprig.</summary>
        public static GrassVariant[] Defaults() => GrassBiomes.Variants(GrassBiome.Meadow);

        /// <summary>
        /// Applies a quality tier: scales density, draw distance and switches the costly features. Density is reset from the biome
        /// ingredient the type came from (<see cref="GrassVariant.SourceId"/>, or the current biome's type of the same name for older
        /// types); hand-made types keep their density.
        /// </summary>
        public void ApplyQuality(Quality q)
        {
            float density = q == Quality.Lush ? 1f : q == Quality.Balanced ? 0.55f : 0.25f;
            foreach (GrassVariant v in Variants)
            {
                GrassVariant d = GrassBiomes.Default(v.SourceId, v.Name, Biome);
                if (d != null) v.Density = d.Density * density;
            }
            DrawDistance = q == Quality.Lush ? 70f : q == Quality.Balanced ? 50f : 30f;
            Features.StylizedShading = true;
            Features.Rim = q == Quality.Lush;
            Features.Translucency = q == Quality.Lush;
            Features.Patches = q != Quality.Light;
            Features.ReceiveShadows = q == Quality.Lush;
            Features.CastShadows = false;
            Features.AlphaToCoverage = q != Quality.Light;
            Features.AdditionalLights = q == Quality.Lush;
            Features.Trample = q != Quality.Light;
            Features.Texture = q != Quality.Light;
            Features.Variation = q != Quality.Light;
            Features.Gusts = q != Quality.Light;
            Features.DistanceLod = true;
            Features.FrustumCulling = true;
            Features.MeshLod = false;
            Features.CpuThinning = true;
            Features.WindTexture = true;
            Features.TrampleRT &= q != Quality.Light; // never switched on by a tier, only off for Light
            Features.TrailTint &= Features.TrampleRT;
            Features.LightRamp &= LightRamp != null;
            Features.PerspectiveCorrection &= q != Quality.Light;
            TrampleMapResolution = q == Quality.Lush ? 512 : 256;
            TrampleWindowSize = q == Quality.Lush ? 64f : q == Quality.Balanced ? 48f : 32f;
            Revision++;
        }
    }
}
