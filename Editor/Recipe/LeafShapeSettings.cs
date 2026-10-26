using System;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Geometry of each leaf card, and the grid layout of a sprite-sheet leaf texture.</summary>
    [Serializable]
    public class LeafShapeSettings
    {
        [Header("Sprite Sheet")]
        [Tooltip("Columns in the leaf texture's grid. Every card picks a random cell; empty cells are skipped. 1 x 1 = one leaf.")]
        [Range(1, 8)] public int Columns = 1;
        [Tooltip("Rows in the leaf texture's grid.")]
        [Range(1, 8)] public int Rows = 1;

        [Header("Card Shape")]
        [Tooltip("Splits across each card. Fold uses at least 2.")]
        [Range(1, 4)] public int WidthSegments = 1;
        [Tooltip("Splits along each card. Curl and Twist use at least 3.")]
        [Range(1, 8)] public int LengthSegments = 1;
        [Tooltip("V-fold along the leaf's middle in degrees. Positive lifts the edges toward the light side.")]
        [Range(-80f, 80f)] public float Fold;
        [Tooltip("Bend from the stem to the tip in degrees. Positive curls toward the light side, negative droops.")]
        [Range(-180f, 180f)] public float Curl;
        [Tooltip("Turn around the leaf's length in degrees, from the stem to the tip.")]
        [Range(-180f, 180f)] public float Twist;
        [Tooltip("Random spread of Fold, Curl and Twist per card, as a share of each.")]
        [Range(0f, 1f)] public float Jitter = 0.3f;
    }
}
