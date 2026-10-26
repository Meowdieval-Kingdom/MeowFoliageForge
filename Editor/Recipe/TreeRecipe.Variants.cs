using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Meowdieval.FoliageForge
{
    /// <summary>One variant of a recipe: the same tree with another seed, a size factor and one blob more or fewer.</summary>
    [Serializable]
    public class TreeVariant
    {
        public int Seed = 1;
        [Range(0.5f, 1.5f)] public float ScaleMultiplier = 1f;
        [Tooltip("Blobs layout: blobs added to or removed from Leaves > Blob Count (at least 1 stays).")]
        [Range(-1, 1)] public int BlobCountDelta;
    }

    public partial class TreeRecipe
    {
        /// <summary>Variants created by <see cref="CreateVariants"/>.</summary>
        public const int DefaultVariantCount = 3;

        /// <summary>The tree type name: <see cref="TypeId"/>, or the preset's name while that is empty.</summary>
        public string EffectiveTypeId => string.IsNullOrEmpty(TypeId) ? LastPreset.ToString() : TypeId;

        /// <summary>Replaces <see cref="Variants"/> with <paramref name="count"/> new ones: random seeds, and sizes and blob counts that differ a little.</summary>
        public void CreateVariants(int count = DefaultVariantCount)
        {
            float[] scales = { 1f, 0.92f, 1.08f };
            int[] deltas = { 0, -1, 1 };
            Variants = new List<TreeVariant>();
            for (int i = 0; i < Mathf.Max(1, count); i++)
                Variants.Add(new TreeVariant { ScaleMultiplier = scales[i % scales.Length], BlobCountDelta = deltas[i % deltas.Length] });
            RerollVariantSeeds();
        }

        /// <summary>Gives every variant a new random seed, all different from each other and from <see cref="Seed"/>.</summary>
        public void RerollVariantSeeds()
        {
            var used = new HashSet<int> { Seed };
            foreach (TreeVariant v in Variants)
            {
                int seed;
                do seed = UnityEngine.Random.Range(1, 99999);
                while (!used.Add(seed));
                v.Seed = seed;
            }
        }

        /// <summary>
        /// A temporary copy of the recipe shaped by variant <paramref name="index"/>: its seed, sizes scaled, blob count shifted (and the blobs
        /// re-suggested for the new branches). This recipe is not changed. The caller destroys the copy.
        /// </summary>
        public TreeRecipe VariantRecipe(int index)
        {
            TreeVariant v = Variants[index];
            TreeRecipe r = Object.Instantiate(this);
            r.hideFlags = HideFlags.HideAndDontSave;
            r.name = name;
            r.Variants = new List<TreeVariant>();
            r.Seed = v.Seed;
            float s = Mathf.Max(0.1f, v.ScaleMultiplier);
            r.Trunk.Height *= s;
            r.Trunk.BaseRadius *= s;
            r.Leaves.CardSize *= s;
            r.Leaves.BlobSize *= s;
            r.Leaves.ClusterRadius *= s;
            r.Leaves.StrandLength *= s;
            r.Leaves.BlobCount = Mathf.Max(1, Leaves.BlobCount + v.BlobCountDelta);
            if (r.Leaves.Layout == LeafLayout.Blobs) r.Blobs = TreeGenerator.SuggestBlobs(r);
            return r;
        }
    }
}
