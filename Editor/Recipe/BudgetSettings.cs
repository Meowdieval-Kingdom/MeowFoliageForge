using System;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Hard limits that keep a baked tree cheap.</summary>
    [Serializable]
    public class BudgetSettings
    {
        [Tooltip("Upper bound on leaf cards. Cards beyond the cap (or the triangle fit) are dropped, evenly across branches.")]
        [Min(8)] public int MaxCards = 1200;
        [Tooltip("Triangle budget for the whole tree. With Fit Cards To Triangles on, leaf cards are reduced until the tree fits.")]
        [Min(100)] public int MaxTriangles = 6000;
        [Tooltip("Derives the leaf card cap from Max Triangles minus the wood, so any recipe stays inside the budget.")]
        public bool FitCardsToTriangles = true;
        public bool AddTrunkCollider = true;
    }
}
