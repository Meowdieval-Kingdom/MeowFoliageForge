using System;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Main stem settings.</summary>
    [Serializable]
    public class TrunkSettings
    {
        [Min(0.2f)] public float Height = 3.2f;
        [Min(0.01f)] public float BaseRadius = 0.22f;
        [Range(0.01f, 1f)] public float TipRadiusRatio = 0.1f;
        [Range(3, 16)] public int RadialSides = 8;
        [Range(2, 24)] public int HeightSegments = 7;
        [Range(0f, 1f)] public float Bend = 0.35f;
        [Range(0f, 0.6f)] public float Lean = 0.08f;
        [Range(0f, 1.5f)] public float Flare = 0.4f;

        [Header("Character")]
        [Tooltip("How fast the trunk thins. Low values keep it thick and narrow it near the top (chunky, cartoon); high values thin it early.")]
        [Range(0.2f, 3f)] public float TaperPower = 0.75f;
        [Tooltip("Degrees the trunk turns from base to top. Spirals the bark texture, root lobes and gnarl.")]
        [Range(-360f, 360f)] public float Twist;
        [Tooltip("Root buttresses: ridges the trunk base splits into. 0 = round. Rounds the trunk's radial sides up to a multiple of this.")]
        [Range(0, 8)] public int RootLobes;
        [Tooltip("How far the root lobes stick out, as a share of the radius.")]
        [Range(0f, 1.5f)] public float RootLobeDepth = 0.5f;
        [Tooltip("How far up the trunk the lobes reach, 0 to 1.")]
        [Range(0.02f, 1f)] public float RootLobeHeight = 0.2f;
        [Tooltip("Lumpy, uneven surface on all wood, as a share of the radius. 0 = smooth tubes.")]
        [Range(0f, 0.5f)] public float Gnarl;

        [Header("Noise")]
        [Tooltip("Smooth sideways bends along the trunk (and stems), as a share of its height. 0 = off.")]
        [Range(0f, 0.5f)] public float NoiseBend;
        [Tooltip("Bends per trunk length. Low = one lazy curve, high = wiggly.")]
        [Range(0.2f, 6f)] public float NoiseFrequency = 1.5f;

        [Header("Split")]
        [Tooltip("Extra stems the trunk splits into. 0 = one trunk, 1 = Y fork, more = multi-stem.")]
        [Range(0, 4)] public int Splits;
        [Tooltip("Where the trunk splits, 0 (ground) to 1 (top).")]
        [Range(0f, 0.95f)] public float SplitHeight = 0.4f;
        [Tooltip("Degrees each stem leans away from the trunk axis.")]
        [Range(0f, 60f)] public float SplitAngle = 20f;
        [Tooltip("Turns of the stems around each other from the split to the top. Use with a low split for braided trunks.")]
        [Range(-2f, 2f)] public float Braid;

        [Header("Roots")]
        [Tooltip("Exposed roots snaking out over the ground. 0 = none.")]
        [Range(0, 12)] public int Roots;
        [Tooltip("Root length as a share of trunk height.")]
        [Range(0.05f, 1.5f)] public float RootLength = 0.4f;
        [Tooltip("Root thickness where it leaves the trunk, as a share of the base radius.")]
        [Range(0.1f, 1f)] public float RootRadius = 0.6f;
        [Tooltip("Sideways snaking along the ground.")]
        [Range(0f, 1f)] public float RootWander = 0.4f;
        [Tooltip("Degrees the root tips curl up out of the ground.")]
        [Range(0f, 120f)] public float RootCurl = 25f;
    }
}
