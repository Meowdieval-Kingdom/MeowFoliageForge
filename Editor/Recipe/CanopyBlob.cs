using System;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>One canopy volume: an ellipsoid, in tree space (metres from the trunk base), that leaf cards fill.</summary>
    [Serializable]
    public struct CanopyBlob
    {
        public Vector3 Position;
        [Tooltip("Half-size on each axis.")]
        public Vector3 Radii;
        [Tooltip("Euler angles in degrees. The blob's axes and cage turn with it.")]
        public Vector3 Rotation;
        [Tooltip("Offsets of the 26 cage points from the plain ellipsoid, set by dragging points in Blobs mode. Empty = a smooth ellipsoid.")]
        public Vector3[] Deform;
        [Tooltip("Flattens the underside into a shelf under a rounded dome. 0 = a plain ellipsoid, 1 = almost flat.")]
        [Range(0f, 1f)] public float FlatBelly;
        [Tooltip("Narrows the blob toward the end of its local +Z axis, so it is egg shaped. 0 = both ends alike.")]
        [Range(0f, 1f)] public float Taper;
    }
}
