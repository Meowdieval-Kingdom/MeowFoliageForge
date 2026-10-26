using System;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// Stand-in bark and moss textures for the presets, kept in the package's Textures/Noise folder. Missing files simply stay empty,
    /// so the package keeps working without them. Swap for real bark textures later (see Documentation~/textures.md).
    /// </summary>
    public static class PresetTextures
    {
        const string Root = "Packages/com.meowdieval.foliageforge/Textures/Noise/";

        private static Texture2D Load(string file) => UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(Root + file);

        /// <summary>Cloudy mask used for the moss edge on every preset.</summary>
        public static Texture2D Moss() => Load("Perlin_01-256x256.png");

        /// <summary>Bark stand-in for a preset.</summary>
        public static Texture2D Bark(TreePreset preset)
        {
            switch (preset)
            {
                case TreePreset.Dead:
                    return Load("Cracks_01-256x256.png");
                case TreePreset.Spruce:
                    return Load("Vein_03-256x256.png");
                default:
                    return Load("Vein_01-256x256.png");
            }
        }

        const string LeafRoot = "Packages/com.meowdieval.foliageforge/Textures/Shaded/";

        /// <summary>Leaf card for a preset, from the shaded sprites in the package's Textures folder. Dead trees have no leaves, so none.</summary>
        public static Texture2D Leaf(TreePreset preset)
        {
            string sprite;
            switch (preset)
            {
                case TreePreset.Spruce:
                    sprite = "0084";
                    break;
                case TreePreset.Maple:
                    sprite = "0086";
                    break;
                case TreePreset.Dead:
                    return null;
                default:
                    sprite = "0085";
                    break;
            }
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{LeafRoot}sprite_{sprite}.png");
        }

        /// <summary>True for empty slots and for textures from the package's Textures folder or the old noise pack, which a preset may replace. Your own textures are kept.</summary>
        public static bool IsReplaceable(Texture2D t)
        {
            if (t == null) return true;
            string path = UnityEditor.AssetDatabase.GetAssetPath(t);
            return path.StartsWith("Packages/com.meowdieval.foliageforge/Textures/") || path.Contains("SBS - Noise Texture Pack");
        }
    }
}
