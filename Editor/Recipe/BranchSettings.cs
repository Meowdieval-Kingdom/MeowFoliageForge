using System;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Branch layout. Level 1 grows from the trunk, level 2 from level 1.</summary>
    [Serializable]
    public class BranchSettings
    {
        [Range(0, 2)] public int Levels = 2;
        [Range(0, 64)] public int Count = 7;
        [Tooltip("Trunk height range, 0 to 1, where level 1 branches grow.")]
        public Vector2 StartEnd = new Vector2(0.35f, 0.9f);
        [Tooltip("Angle away from straight up, in degrees. 90 is horizontal.")]
        public Vector2 PitchRange = new Vector2(45f, 80f);
        [Tooltip("Branch length as a fraction of trunk height, scaled by the profile below.")]
        [Min(0.05f)] public float LengthRatio = 0.75f;
        [Tooltip("Length multiplier along the trunk, bottom to top. Shapes the crown silhouette.")]
        public AnimationCurve LengthProfile = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0.35f));
        [Range(0.05f, 1f)] public float RadiusRatio = 0.55f;
        [Tooltip("Positive pulls branches down, negative lifts them.")]
        [Range(-1f, 2f)] public float Droop = 0.1f;
        [Range(0f, 1f)] public float Wander = 0.35f;
        [Range(2, 10)] public int Segments = 4;
        [Range(0, 8)] public int SubCount = 3;
        [Range(0.1f, 1f)] public float SubLengthRatio = 0.5f;
        [Tooltip("Branches per ring. 1 spirals around the trunk, 5 gives conifer whorls.")]
        [Range(1, 8)] public int WhorlSize = 1;
        [Tooltip("Degrees around the trunk between one branch and the next (min, max; a random step in between per branch). 137.5 both = the golden spiral that fills the crown evenly; 360/Count with a narrow range gives an even round crown. Ignored with whorls.")]
        public Vector2 YawStep = new Vector2(137.508f, 137.508f);
        [Tooltip("Smooth branches: minimum sides around every branch ring, LODs ignore it. Normals are always round, so more sides only rounds the silhouette. 3 = old look.")]
        [Range(3, 16)] public int MinSides = 3;

        [Header("Noise")]
        [Tooltip("Smooth bends along every branch, as a share of its length. 0 = off.")]
        [Range(0f, 0.5f)] public float NoiseBend;
        [Tooltip("Bends per branch length.")]
        [Range(0.2f, 6f)] public float NoiseFrequency = 2f;
        [Tooltip("Smooth length variation around and along the trunk: some branches reach out, neighbours stay short. 0 = off.")]
        [Range(0f, 1f)] public float LengthNoise;

        [Header("Character")]
        [Tooltip("Branches on the side the trunk leans to grow longer, the others shorter. Makes a lopsided crown.")]
        [Range(0f, 1f)] public float Asymmetry;
        [Tooltip("Share of level 1 branches snapped off short, with a jagged end and no twigs or leaves.")]
        [Range(0f, 1f)] public float Broken;
    }
}
