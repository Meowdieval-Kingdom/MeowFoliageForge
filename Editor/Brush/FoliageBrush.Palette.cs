using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    public static partial class FoliageBrush
    {
        /// <summary>Palette entries that still exist and have a Mix % above 0.</summary>
        public static int PaletteCount(FoliageForgeManager m)
        {
            int n = 0;
            for (int i = 0; i < m.Palette.Count; i++) if (m.Palette[i] != null && m.PaletteWeight(i) > 0f) n++;
            foreach (TreeVariantSet set in m.VariantSets) if (UsableSet(set)) n++;
            return n;
        }

        /// <summary>True when the set has a prefab with a weight above 0.</summary>
        public static bool UsableSet(TreeVariantSet set) => set != null && set.Pick(new System.Random(1)) != null;

        /// <summary>
        /// Adds (<paramref name="on"/>, at Mix 100%) or removes prefabs in the manager's palette, undoably; drops missing entries.
        /// Keeps <see cref="FoliageForgeManager.PaletteWeights"/> in step.
        /// </summary>
        public static void SetInPalette(FoliageForgeManager m, IEnumerable<GameObject> prefabs, bool on)
        {
            Undo.RecordObject(m, "Edit Foliage Palette");
            var kept = new List<GameObject>();
            var weights = new List<float>();
            for (int i = 0; i < m.Palette.Count; i++)
                if (m.Palette[i] != null)
                {
                    kept.Add(m.Palette[i]);
                    weights.Add(m.PaletteWeight(i));
                }
            foreach (GameObject p in prefabs)
            {
                int at = kept.IndexOf(p);
                if (!on && at >= 0)
                {
                    kept.RemoveAt(at);
                    weights.RemoveAt(at);
                }
                else if (on && at < 0)
                {
                    kept.Add(p);
                    weights.Add(100f);
                }
            }
            m.Palette = kept;
            m.PaletteWeights = weights;
            Dirty(m);
        }

        /// <summary>Sets the Mix % (clamped to 0..100) of a palette entry, undoably. Does nothing when the prefab is not in the palette.</summary>
        public static void SetPaletteWeight(FoliageForgeManager m, GameObject prefab, float percent)
        {
            int at = m.Palette.IndexOf(prefab);
            if (at < 0) return;
            Undo.RecordObject(m, "Palette Mix");
            while (m.PaletteWeights.Count < m.Palette.Count) m.PaletteWeights.Add(100f);
            m.PaletteWeights[at] = Mathf.Clamp(percent, 0f, 100f);
            Dirty(m);
        }

        /// <summary>Index picked with probability proportional to its weight; weights of 0 or less are never picked. -1 when none is positive.</summary>
        public static int WeightedPick(IReadOnlyList<float> weights, System.Random rng)
        {
            double total = 0;
            foreach (float w in weights) if (w > 0f) total += w;
            if (total <= 0) return -1;
            double r = rng.NextDouble() * total;
            int last = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f) continue;
                last = i;
                r -= weights[i];
                if (r < 0) return i;
            }
            return last; // rounding
        }

        /// <summary>"Short 50% / Tall 30% / Fern 20%": each positive weight's share of the total, in order; "nothing" when none is positive.</summary>
        public static string MixSummary(IReadOnlyList<string> names, IReadOnlyList<float> weights)
        {
            float total = 0f;
            foreach (float w in weights) if (w > 0f) total += w;
            if (total <= 0f) return "nothing";
            var parts = new List<string>();
            for (int i = 0; i < weights.Count; i++)
                if (weights[i] > 0f) parts.Add($"{names[i]} {Mathf.RoundToInt(weights[i] / total * 100f)}%");
            return string.Join(" / ", parts);
        }

        /// <summary>Sets the Ground mask on the manager and its Grass Field (or the stand-alone field), undoably.</summary>
        public static void SetGroundMask(int mask)
        {
            if (Manager != null)
            {
                Undo.RecordObject(Manager, "Ground Mask");
                Manager.Ground = mask;
                Dirty(Manager);
            }
            if (Grass != null)
            {
                Undo.RecordObject(Grass, "Ground Mask");
                Grass.Ground = mask;
                Dirty(Grass);
            }
        }

        private static void Dirty(Object o)
        {
            EditorUtility.SetDirty(o);
            PrefabUtility.RecordPrefabInstancePropertyModifications(o);
        }
    }
}
