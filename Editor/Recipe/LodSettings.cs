using System;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Lower-detail copies baked into the prefab as an LODGroup. Transition values are screen heights (0..1).</summary>
    [Serializable]
    public class LodSettings
    {
        [Tooltip("Bake LOD1 and LOD2 meshes and an LODGroup into the prefab.")]
        public bool Generate = true;
        [Tooltip("Screen height below which LOD0 switches to LOD1.")]
        [Range(0.02f, 1f)] public float Lod1Screen = 0.4f;
        [Tooltip("Screen height below which LOD1 switches to LOD2.")]
        [Range(0.01f, 0.5f)] public float Lod2Screen = 0.12f;
        [Tooltip("Screen height below which the tree is culled.")]
        [Range(0f, 0.2f)] public float CullScreen = 0.02f;
        [Tooltip("LOD1 keeps this share of the leaf cards (they grow to cover the same crown) and drops the twig level.")]
        [Range(0.1f, 1f)] public float Lod1CardShare = 0.5f;
        [Tooltip("LOD2 keeps this share of the leaf cards, on a simple trunk.")]
        [Range(0.05f, 1f)] public float Lod2CardShare = 0.2f;
        [Tooltip("LOD2 cards are crossed quads, which hold the silhouette from every side at very few triangles.")]
        public bool Lod2Intersecting = true;
    }
}
