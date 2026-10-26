using System;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Starting points for a recipe. Applying one overwrites shape, leaf and look numbers; your textures and materials stay.</summary>
    public enum TreePreset
    {
        Oak = 0, Dead = 2, Maple = 3, Spruce = 5,
        // explicit values: 1, 4, 6-9, 17, 18 and 20 belonged to removed presets (Pine, Birch, old Cherry...) and stay unused so old assets cannot land on a new tree
        Twisted = 10, CottonCandy = 11, Bush = 12, Hedge = 13, Fir = 14, Sapling = 15, Cherry = 16, Poplar = 19, HedgeHigh = 21,
    }

    /// <summary>How leaf cards turn toward the camera. Needs UV2/UV3 axes in the mesh, which the generator only writes when this is not Off.</summary>
    public enum BillboardMode { Off = 0, ViewAligned = 1, YAxis = 2 }

    /// <summary>Silhouette the leaves are pulled onto, bottom to top of the crown.</summary>
    public enum CrownShape { None, Sphere, Dome, Cone, Column, Umbrella }

    /// <summary>How leaves sit on the end branches.</summary>
    public enum LeafLayout
    {
        /// <summary>Cards scattered around each end branch.</summary>
        Clusters,
        /// <summary>Long cards laid along each end branch (conifer fronds); the trunk top gets an upright leader.</summary>
        Fronds,
        /// <summary>Strands of cards hanging straight down from points along each end branch (willow).</summary>
        Hanging,
        /// <summary>Cards fill the canopy blobs listed in the recipe (editable in the preview's Blobs mode); each blob shades like a ball.</summary>
        Blobs,
    }
}
