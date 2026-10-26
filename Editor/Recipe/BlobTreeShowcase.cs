using System;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Nine untextured blob-look trees: presets (Oak, Poplar, Sapling, Fir, Twisted, CottonCandy, Maple) plus field tweaks for the rest. Unsaved recipes: save or bake them yourself.</summary>
    public static class BlobTreeShowcase
    {
        public static TreeRecipe[] Create(bool highQuality = false)
        {
            _high = highQuality;
            return new[]
            {
                Make("Oak", TreePreset.Oak, r => { }),
                Make("Poplar", TreePreset.Poplar, r => { }),
                Make("Umbrella", TreePreset.Oak, Umbrella),
                Make("Sapling", TreePreset.Sapling, r => { }),
                Make("MapleAutumn", TreePreset.Maple, Autumn),
                Make("Spruce", TreePreset.Fir, r => { }),
                Make("Twisted", TreePreset.Twisted, r => { }),
                Make("CottonCandy", TreePreset.CottonCandy, r => { }),
                Make("FloatingIslands", TreePreset.Oak, Islands),
            };
        }

        static bool _high;

        private static TreeRecipe Make(string id, TreePreset preset, Action<TreeRecipe> tweak)
        {
            var r = ScriptableObject.CreateInstance<TreeRecipe>();
            r.ApplyPreset(preset, false);
            r.name = id;
            if (preset == TreePreset.Oak || preset == TreePreset.Maple)
            {
                if (preset == TreePreset.Oak)
                {
                    r.Look.LeafBottom = new Color(0.12f, 0.32f, 0.12f);
                    r.Look.LeafTop = new Color(0.5f, 0.78f, 0.25f);
                }
                r.Leaves.CardsPerBranch = 0;
                r.Leaves.BlobSize = new Vector2(0.4f, 2f);
                r.Look.GrainAmount = 0.5f;
                r.Look.RimStrength = 0.3f;
            }
            tweak(r);
            if (_high)
            {
                r.Budget.MaxCards = 8000;
                r.Budget.MaxTriangles = 24000;
                r.Leaves.GroupQuads = 3;
                r.Look.CoreSegments = 20;
                r.Leaves.CardSize /= 1.5f; // the preset's cards are already grown for the game budget
            }
            r.TypeId = id;
            r.Blobs = TreeGenerator.SuggestBlobs(r);
            return r;
        }

        private static void Umbrella(TreeRecipe r)
        {
            r.Seed = 7;
            TrunkSettings t = r.Trunk;
            BranchSettings b = r.Branches;
            LeafSettings l = r.Leaves;
            t.Height = 5f;
            t.Bend = 0.3f;
            t.Twist = 0f;
            t.Splits = 1;
            t.SplitHeight = 0.55f;
            t.SplitAngle = 35f;
            b.Count = 9;
            b.StartEnd = new Vector2(0.6f, 0.95f);
            t.Height = 6.5f;
            b.PitchRange = new Vector2(55f, 80f);
            b.LengthRatio = 0.9f;
            b.LengthProfile = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.8f));
            l.BlobCount = 8;
            l.BlobSize = new Vector2(0.4f, 1.8f);
            l.BlobOverlap = 1.3f;
            l.BlobSizeSpread = 0.2f;
            l.BlobFlatBelly = 0.9f;
            l.BlobTaper = 0f;
            l.Envelope = CrownShape.Umbrella;
        }

        private static void Autumn(TreeRecipe r)
        {
            r.Seed = 5;
            LookSettings k = r.Look;
            LeafSettings l = r.Leaves;
            k.LeafBottom = new Color(0.55f, 0.1f, 0.05f);
            k.LeafTop = new Color(1f, 0.62f, 0.12f);
            k.PatchColor = new Color(0.8f, 0.15f, 0.05f, 0.6f);
            k.PatchAmount = 0.5f;
            k.PatchScale = 0.3f;
            k.ShadowColor = new Color(0.4f, 0.08f, 0.15f);
            k.RimColor = new Color(1f, 0.7f, 0.3f);
            l.BlobCount = 7;
            l.BlobOverlap = 1.35f;
            r.Trunk.Roots = 0;
            r.Trunk.RootLobes = 3;
        }

        private static void Islands(TreeRecipe r)
        {
            r.Seed = 8;
            TrunkSettings t = r.Trunk;
            BranchSettings b = r.Branches;
            LeafSettings l = r.Leaves;
            LookSettings k = r.Look;
            t.Height = 7f;
            t.BaseRadius = 0.15f;
            t.Twist = 0f;
            t.Gnarl = 0f;
            t.RootLobes = 0;
            t.Splits = 0;
            t.Bend = 0.25f;
            b.Count = 12;
            b.StartEnd = new Vector2(0.3f, 1f);
            b.PitchRange = new Vector2(20f, 60f);
            b.LengthRatio = 1f;
            b.RadiusRatio = 0.3f;
            b.Levels = 1;
            b.Wander = 0.3f;
            b.Droop = -0.2f;
            l.BlobCount = 12;
            l.BlobOverlap = 0.7f;
            l.BlobSize = new Vector2(0.3f, 0.8f);
            l.BlobSizeSpread = 0.8f;
            l.BlobFlatBelly = 0.9f;
            l.BlobTaper = 0.2f;
            l.CardSize = new Vector2(0.45f, 0.9f);
            k.LeafBottom = new Color(0.05f, 0.3f, 0.3f);
            k.LeafTop = new Color(0.45f, 0.95f, 0.6f);
        }
    }
}
