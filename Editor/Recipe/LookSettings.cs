using System;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Shape of a solid leaf card.</summary>
    public enum SolidLeafShape { Square, RoundSoft, Leaf }

    /// <summary>Materials used by the baked prefab. Empty slots are created on first bake and then left alone, so edit them freely.</summary>
    [Serializable]
    public class LookSettings
    {
        [Tooltip("Your own materials. Leave empty and the tool keeps a material per recipe in sync with the values below.")]
        public Material Bark;
        public Material Leaf;
        [Tooltip("Material of the inner core shell. Leave empty and the tool keeps one in sync with the leaf look.")]
        public Material CoreMaterial;

        [Header("Leaves")]
        [Tooltip("Monochrome leaf-cluster texture, alpha = cutout. Empty uses a generated placeholder.")]
        public Texture2D LeafTexture;
        [Tooltip("Solid leaf cards: ignore the leaf texture's alpha and shape so every card is a flat opaque quad (the textureless look).")]
        public bool SolidCards;
        [Tooltip("Square keeps solid cards as flat quads; Round Soft cuts each to a round blob with a noise-eroded edge.")]
        public SolidLeafShape SolidShape;
        [Tooltip("Blob look: tint, brightness and gradient follow world-space noise and the height inside each blob instead of a random value per card, so cards melt into one mass.")]
        public bool ContinuousColor;
        [Tooltip("Fine world-space breakup of colour and light (needs Continuous Color). 0 = off.")]
        [Range(0f, 1f)] public float GrainAmount;
        [Tooltip("Blobs layout: adds a slightly inset smooth shell per blob (own submesh and material) that fills the gaps between cards.")]
        public bool CoreMesh;
        [Tooltip("Off (default): the core is the blob's own shape and orientation, inset evenly, so the cards sit on it. On: the core is also trimmed on the side Leaves.PlacementBias leaves bare.")]
        public bool CoreAlign;
        [Tooltip("Core mesh: longitude segments of each blob's shell (latitude rings follow at 70%). Triangles per blob = 2 x segments x rings.")]
        [Range(4, 24)] public int CoreSegments = 10;
        public Color LeafBottom = new Color(0.07f, 0.2f, 0.1f);
        public Color LeafTop = new Color(0.55f, 0.8f, 0.25f);
        [Tooltip("Above 1 keeps the lower crown dark for longer, below 1 brightens it sooner.")]
        [Range(0.2f, 4f)] public float GradientBias = 1f;
        [Tooltip("Random per-card shift of the dark-to-light ramp so it does not read as a clean gradient.")]
        [Range(0f, 1f)] public float GradientJitter = 0.25f;
        [Tooltip("Per-branch warm/cool tint and brightness variation.")]
        [Range(0f, 1f)] public float ColorVariation = 0.5f;

        [Tooltip("Soft light: how far light wraps around leaf cards past the terminator. 0 = plain Lambert.")]
        [Range(0f, 1f)] public float Wrap = 0.35f;
        [Tooltip("Light shining through the leaves toward the camera, strongest when the sun is behind them. 0 = off.")]
        [Range(0f, 2f)] public float Translucency = 0.4f;
        [Tooltip("Tint of the light that passes through the leaves.")]
        public Color TranslucencyColor = new Color(0.6f, 0.9f, 0.2f);
        [Tooltip("How tightly the glow hugs the line from sun to camera. Higher = smaller, brighter spot.")]
        [Range(1f, 8f)] public float TranslucencyFocus = 3f;

        [Header("Stylized Shading")]
        [Tooltip("Turns on shadow colour, rim, hue shift and per-tree variation below. Off keeps plain wrapped lighting.")]
        public bool StylizedShading;
        [Tooltip("Colour of the side facing away from the sun (subsurface). Keeps the texture detail.")]
        public Color ShadowColor = new Color(0.08f, 0.22f, 0.25f);
        [Tooltip("How much the shadow side turns into Shadow Color. 0 = off.")]
        [Range(0f, 1f)] public float ShadowAmount;
        [Tooltip("Width of the soft band between lit and shadow side. Low = toon-like edge.")]
        [Range(0.02f, 1f)] public float ShadowSoftness = 0.35f;
        [Tooltip("Steps the light into this many flat tones that follow the smooth blob normals. 0 = off (smooth), 2 to 5 = banded.")]
        [Range(0, 5)] public int Bands;
        [Tooltip("Lets the blob's smooth normal drive the dark-bottom to light-top ramp instead of only the card height. 0 = off.")]
        [Range(0f, 1f)] public float SkyTone;
        [Tooltip("Rim (fresnel) colour on the clump edges.")]
        public Color RimColor = new Color(1f, 0.85f, 0.5f);
        [Tooltip("Rim brightness. 0 = off.")]
        [Range(0f, 2f)] public float RimStrength;
        [Tooltip("How thin the rim is. Higher = only the outermost edge.")]
        [Range(0.5f, 8f)] public float RimPower = 3f;
        [Tooltip("Hue shift: the lit side turns warmer and the shadow side cooler by this much.")]
        [Range(0f, 0.25f)] public float HueShift;
        [Tooltip("Per-tree hue and brightness change from its world position, so copies of one prefab differ. 0 = off.")]
        [Range(0f, 1f)] public float TreeVariation;
        [Tooltip("Colour of the patches (dry, autumn) that world-space noise blends over the leaves. Alpha = strength.")]
        public Color PatchColor = new Color(0.94f, 0.76f, 0.25f, 0.6f);
        [Tooltip("How much of the foliage the patches cover. 0 = off.")]
        [Range(0f, 1f)] public float PatchAmount;
        [Tooltip("Patch size: noise cells per metre. Low = big patches.")]
        [Range(0.02f, 2f)] public float PatchScale = 0.25f;
        [Tooltip("Painterly fill texture, tiled in world space over every card so neighbouring cards do not repeat one leaf. Empty = off.")]
        public Texture2D FillTexture;
        [Tooltip("Fill texture tiles per metre.")]
        [Range(0.1f, 8f)] public float FillScale = 1f;
        [Tooltip("How strongly the fill texture darkens and varies the leaf colour.")]
        [Range(0f, 1f)] public float FillImpact = 0.5f;

        [Header("Bark")]
        [Tooltip("Monochrome bark texture, mapped around the trunk and branches by the mesh UVs. Empty is plain.")]
        public Texture2D BarkTexture;
        public Color BarkBottom = new Color(0.16f, 0.1f, 0.07f);
        public Color BarkTop = new Color(0.42f, 0.3f, 0.2f);
        [Min(0.05f)] public float BarkTiling = 1f;

        [Header("Moss")]
        [Tooltip("Monochrome breakup mask for the moss edge. Empty gives a clean edge.")]
        public Texture2D MossTexture;
        public Color MossColor = new Color(0.25f, 0.42f, 0.12f);
        [Tooltip("0 = no moss. Higher values let moss creep further down the sides.")]
        [Range(0f, 1f)] public float MossAmount;
        [Range(1f, 20f)] public float MossSharpness = 6f;
        [Min(0.05f)] public float MossTiling = 1.5f;
    }
}
