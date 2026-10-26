using UnityEngine;

namespace Meowdieval.FoliageForge
{
    public partial class TreeRecipe
    {
        /// <summary>The untextured blob look: round soft cards, continuous colour, three bands, a core shell and baked blob AO.</summary>
        private void ApplyBlobLook()
        {
            LookSettings k = Look;
            k.StylizedShading = true; // bands, rim and shadow colour only run in stylized mode
            k.SolidShape = SolidLeafShape.Leaf;
            k.ContinuousColor = true;
            k.GrainAmount = 0.35f;
            k.Bands = 3;
            k.ColorVariation = 0.4f;
            k.RimStrength = 0.15f;
            k.CoreMesh = true;
            k.CoreAlign = false; // the core keeps the blob's shape; cards sit on top of it
            // cards ride on the blob surface and poke out of it as a leafy fringe
            Leaves.GroupDepth = 0.1f;
            Leaves.NormalOffset = 0.04f;
            Leaves.Smoothness = 1f;
            Leaves.GroupVolume = 0.15f;
            Leaves.BranchTipBias = 0f;
            Leaves.Coverage = 1.3f;
            k.Translucency = 0f;
            // game-ready: about 3000 triangles at LOD0; the core shell fills gaps so fewer, larger cards are enough
            k.CoreSegments = 10;
            Leaves.GroupQuads = 2;
            Leaves.CardSize *= 1.5f;
            Branches.MinSides = 3;
            Branches.Segments = Mathf.Min(Branches.Segments, 3);
            Trunk.HeightSegments = Mathf.Min(Trunk.HeightSegments, 4);
            Trunk.RadialSides = Mathf.Min(Trunk.RadialSides, 6);
            Budget.MaxCards = 1500;
            Budget.MaxTriangles = 3000;
            Budget.FitCardsToTriangles = true;
            Lods.Generate = true;
            Lods.Lod1CardShare = 0.4f;
            Lods.Lod2CardShare = 0.1f;
            Renormalize.Enabled = true;
            if (Renormalize.AOStrength < 0.5f) Renormalize.AOStrength = 0.6f;
        }
    }
}
