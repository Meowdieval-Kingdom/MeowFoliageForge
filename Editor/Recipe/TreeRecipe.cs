using System;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// All inputs for one procedural tree. The same recipe and seed always produce the same mesh.
    /// Generation is editor-only; builds only ever contain the baked mesh and prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTreeRecipe", menuName = "Meowdieval/Foliage Forge/Tree Recipe")]
    public partial class TreeRecipe : ScriptableObject
    {
        public int Seed = 1;
        [Tooltip("The preset this recipe was last started from. Shown for information; use the Preset menu to change it.")]
        public TreePreset LastPreset;
        [Tooltip("Tree type name, e.g. Oak. Empty = the preset's name. Baked variant sets carry it.")]
        public string TypeId = string.Empty;
        [Tooltip("Variants of this tree: each bakes as <Name>_V1, <Name>_V2, ... with its own seed, scale and blob count. The recipe itself is not changed.")]
        public System.Collections.Generic.List<TreeVariant> Variants = new System.Collections.Generic.List<TreeVariant>();
        [Tooltip("Canopy volumes for the Blobs leaf layout. Edit them in the Foliage Forge window's Blobs mode, or fill them with Auto Blobs. Empty = suggested on the fly.")]
        public System.Collections.Generic.List<CanopyBlob> Blobs = new System.Collections.Generic.List<CanopyBlob>();
        public TrunkSettings Trunk = new TrunkSettings();
        public BranchSettings Branches = new BranchSettings();
        public LeafSettings Leaves = new LeafSettings();
        public LeafShapeSettings LeafShapes = new LeafShapeSettings();
        public RenormalizeSettings Renormalize = new RenormalizeSettings();
        public BudgetSettings Budget = new BudgetSettings();
        public LodSettings Lods = new LodSettings();
        public LookSettings Look = new LookSettings();
    }
}
