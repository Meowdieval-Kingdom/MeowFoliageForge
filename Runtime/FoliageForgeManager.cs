using System.Collections.Generic;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// Root of a placed-foliage setup: one Grass Field for painted grass, a Props root for painted trees and baked grass patches,
    /// and the palette the prop brush picks from. Select it and use the brush in the scene view; nothing here runs at play time.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meowdieval/Foliage Forge/Foliage Forge Manager")]
    public sealed class FoliageForgeManager : MonoBehaviour
    {
        [Tooltip("Grass layer. Painted strokes and the optional box fill live on it.")]
        public GrassField Grass;
        [Tooltip("Painted props (trees, baked grass patches) become children of this object.")]
        public Transform Props;
        [Tooltip("Prefabs the prop brush places. Add baked trees and baked grass patches.")]
        public List<GameObject> Palette = new List<GameObject>();
        [Tooltip("Mix % (0..100) of each Palette entry, same order. Edited in the Foliage Brush panel.")]
        [HideInInspector] public List<float> PaletteWeights = new List<float>();
        [Tooltip("Baked tree variant sets the prop brush places, each at Mix 100%; every placement picks a weighted random prefab of the set.")]
        public List<TreeVariantSet> VariantSets = new List<TreeVariantSet>();
        [Tooltip("Layers the brush and the grass land on. Keep the layers of placed props out of this mask, or grass grows on trees.")]
        public LayerMask Ground = 1;

        /// <summary>Mix % of palette entry <paramref name="index"/>; 100 when none is stored (managers saved before weights existed).</summary>
        public float PaletteWeight(int index) => index >= 0 && index < PaletteWeights.Count ? PaletteWeights[index] : 100f;

        private void OnValidate()
        {
            if (Grass != null && Grass.Ground.value != Ground.value) Grass.Ground = Ground;
            while (PaletteWeights.Count < Palette.Count) PaletteWeights.Add(100f); // inspector edits to Palette
            if (PaletteWeights.Count > Palette.Count) PaletteWeights.RemoveRange(Palette.Count, PaletteWeights.Count - Palette.Count);
        }
    }
}
