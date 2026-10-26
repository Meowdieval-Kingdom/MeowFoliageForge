using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meowdieval.FoliageForge
{
    /// <summary>What the preview shows: the wood as lines, wood plus editable canopy blobs, the finished tree, one blob's leaves alone at the origin, or the recipe's variants in a row.</summary>
    public enum PreviewMode { Wireframe, Blobs, Finished, Blob, Variants }

    /// <summary>How the single-blob viewer draws the leaves: lit, colour = normal, or edges.</summary>
    public enum BlobDisplay { Shaded, Normals, Wireframe }
}
