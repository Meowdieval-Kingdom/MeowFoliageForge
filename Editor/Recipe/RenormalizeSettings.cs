using System;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// Foliage renormalizer: replaces leaf normals with the gradient of a smoothed density proxy built around the
    /// cards, and bakes ambient occlusion into the leaf vertex colour. Costs nothing at runtime.
    /// </summary>
    [Serializable]
    public class RenormalizeSettings
    {
        public bool Enabled = true;
        [Tooltip("0 = loose proxy, smooth crown shading. 1 = tight proxy that hugs each leaf clump.")]
        [Range(0f, 1f)] public float Tightness = 0.35f;
        [Tooltip("How far the card normals move toward the proxy normals.")]
        [Range(0f, 1f)] public float Strength = 1f;
        [Tooltip("Darkening of leaves deep inside the crown. 0 turns the baked occlusion off.")]
        [Range(0f, 1f)] public float AOStrength = 0.6f;
    }
}
