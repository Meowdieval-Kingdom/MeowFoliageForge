using System.Collections.Generic;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    public partial class TreeRecipe
    {
        // Blob presets start from the Oak canopy; each method below overrides what makes that tree itself.
        // Game budget and leaf look come from ApplyBlobLook / BlobPresetFinish.
        private void BlobPreset(TreePreset preset)
        {
            OakDefault();
            Leaves.CardsPerBranch = 0;
            Leaves.BlobSize = new Vector2(0.4f, 2f);
            Look.LeafBottom = new Color(0.12f, 0.32f, 0.12f);
            Look.LeafTop = new Color(0.5f, 0.78f, 0.25f);
            switch (preset)
            {
                case TreePreset.Poplar:
                    Poplar();
                    break;
                case TreePreset.Sapling:
                    Sapling();
                    break;
                case TreePreset.Twisted:
                    Twisted();
                    break;
                case TreePreset.CottonCandy:
                    CottonCandy();
                    break;
                case TreePreset.Cherry:
                    Cherry();
                    break;
                case TreePreset.Bush:
                    Bush();
                    break;
                case TreePreset.Hedge:
                case TreePreset.HedgeHigh:
                    Hedge();
                    break;
                case TreePreset.Fir:
                    Fir();
                    break;
            }
        }

        // slim column
        private void Poplar()
        {
            TrunkSettings t = Trunk;
            BranchSettings b = Branches;
            LeafSettings l = Leaves;
            Seed = 4;
            t.Height = 9f;
            t.BaseRadius = 0.22f;
            t.Twist = 0f;
            t.Splits = 0;
            t.Bend = 0.08f;
            b.Count = 12;
            b.StartEnd = new Vector2(0.25f, 0.95f);
            b.PitchRange = new Vector2(12f, 28f);
            b.LengthRatio = 0.22f;
            b.LengthProfile = new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.4f));
            l.BlobCount = 5;
            l.BlobOverlap = 1.5f;
            l.BlobSizeSpread = 0.3f;
            l.BlobFlatBelly = 0f;
            l.BlobTaper = 0.6f;
            l.Envelope = CrownShape.Column;
        }

        // young whippy tree: thin bendy trunk, a few drooping limbs, small soft blobs with the tuned trees' leaf look
        private void Sapling()
        {
            TrunkSettings t = Trunk;
            BranchSettings b = Branches;
            LeafSettings l = Leaves;
            Seed = 3;
            t.Height = 3.2f;
            t.BaseRadius = 0.09f;
            t.TipRadiusRatio = 0.3f;
            t.Flare = 0.3f;
            t.Twist = 25f;
            t.Splits = 0;
            t.Bend = 0.85f;
            t.Lean = 0.15f;
            t.Gnarl = 0f;
            t.NoiseBend = 0.2f;
            t.NoiseFrequency = 2f;
            b.Count = 5;
            b.Levels = 1;
            b.LengthRatio = 0.45f;
            b.StartEnd = new Vector2(0.45f, 0.95f);
            b.PitchRange = new Vector2(30f, 60f);
            b.Wander = 0.7f;
            b.NoiseBend = 0.4f;
            b.LengthNoise = 0.4f;
            b.Asymmetry = 0.4f;
            b.Droop = 0.15f;
            l.BlobCount = 3;
            l.BlobOverlap = 1.4f;
            l.BlobSizeSpread = 1f;
            l.BlobFlatBelly = 0.3f;
            l.BlobTaper = 0.5f;
            l.BranchFollow = 1f;
            l.CardSize = new Vector2(0.3f, 0.7f);
            l.BlobSize = new Vector2(0.3f, 1.2f);
        }

        private void Twisted()
        {
            TrunkSettings t = Trunk;
            BranchSettings b = Branches;
            LeafSettings l = Leaves;
            LookSettings k = Look;
            Seed = 9;
            t.Height = 6.5f;
            t.BaseRadius = 0.35f;
            t.Twist = 300f;
            t.Gnarl = 0.35f;
            t.NoiseBend = 0.3f;
            t.Bend = 0.5f;
            t.Lean = 0.2f;
            t.RootLobes = 4;
            t.Splits = 1;
            t.SplitAngle = 45f;
            b.Count = 6;
            b.LengthRatio = 0.7f;
            b.Asymmetry = 0.8f;
            b.Wander = 0.8f;
            b.NoiseBend = 0.4f;
            b.LengthNoise = 0.6f;
            l.BlobCount = 5;
            l.BlobOverlap = 0.9f;
            l.BlobSizeSpread = 1f;
            l.BlobSize = new Vector2(0.5f, 1.7f);
            l.BlobFlatBelly = 0.2f;
            l.BlobTaper = 0.8f;
            l.BranchFollow = 1f;
            l.CardSize = new Vector2(0.5f, 1.2f);
            k.LeafBottom = new Color(0.05f, 0.12f, 0.2f);
            k.LeafTop = new Color(0.3f, 0.7f, 0.55f);
            k.ShadowColor = new Color(0.1f, 0.05f, 0.25f);
            k.BarkTop = new Color(0.3f, 0.22f, 0.3f);
        }

        // the tuned Cotton Candy asset
        private void CottonCandy()
        {
            TrunkSettings t = Trunk;
            BranchSettings b = Branches;
            LeafSettings l = Leaves;
            LookSettings k = Look;
            Seed = 6;
            t.Height = 5f;
            t.BaseRadius = 0.12f;
            t.Twist = 0f;
            t.Splits = 1;
            t.Gnarl = 0f;
            t.RootLobes = 0;
            t.Bend = 0.25f;
            b.Count = 7;
            b.LengthRatio = 0.7f;
            b.Levels = 1;
            l.BlobCount = 7;
            l.BlobOverlap = 2f;
            l.BlobSizeSpread = 0.4f;
            l.BlobSize = new Vector2(0.5f, 1.5f);
            l.BlobFlatBelly = 0f;
            l.BlobTaper = 1f;
            l.NormalOffset = 0.2f;
            l.GroupJitter = 1f;
            l.CardSize = new Vector2(0.5f, 0.9f);
            l.Coverage = 0.5f;
            l.GroupVolume = 0.5f;
            l.GroupDepth = 0.5f;
            l.GroupQuads = 3;
            l.GroupTilt = 70f;
            l.GroupTiltJitter = 25f;
            l.PlacementBias = 0.75f;
            l.BranchTipBias = 0.5f;
            k.LeafBottom = new Color(0.8f, 0.45f, 0.85f);
            k.LeafTop = new Color(1f, 0.75f, 0.9f);
            k.ShadowColor = new Color(0.55f, 0.35f, 0.8f);
            k.RimColor = new Color(1f, 0.9f, 1f);
            k.PatchColor = new Color(0.6f, 0.85f, 1f, 0.6f);
            k.PatchAmount = 0.4f;
            k.PatchScale = 0.4f;
            k.BarkBottom = new Color(0.35f, 0.25f, 0.35f);
            k.BarkTop = new Color(0.65f, 0.5f, 0.6f);
            k.MossAmount = 0f;
        }

        // cherry tree: a short forked trunk with spreading, slightly arching limbs under Cotton Candy's soft puffs of blossom
        private void Cherry()
        {
            TrunkSettings t = Trunk;
            BranchSettings b = Branches;
            LeafSettings l = Leaves;
            LookSettings k = Look;
            Seed = 12;
            t.Height = 4f;
            t.BaseRadius = 0.2f;
            t.TipRadiusRatio = 0.3f;
            t.Flare = 1.1f;
            t.Twist = 0f;
            t.Splits = 1;
            t.SplitHeight = 0.45f;
            t.SplitAngle = 32f;
            t.Bend = 0.3f;
            t.Gnarl = 0.12f;
            t.NoiseBend = 0.1f;
            t.RootLobes = 3;
            b.Levels = 2;
            b.Count = 8;
            b.StartEnd = new Vector2(0.4f, 0.95f);
            b.LengthRatio = 0.6f;
            b.PitchRange = new Vector2(45f, 75f);
            b.Wander = 0.55f;
            b.Asymmetry = 0.25f;
            b.Droop = -0.05f;
            l.BlobCount = 6;
            l.BlobOverlap = 1.6f;
            l.BlobSize = new Vector2(0.5f, 1.5f);
            l.BlobSizeSpread = 0.5f;
            l.BlobFlatBelly = 0.2f;
            l.BlobTaper = 0.8f;
            l.BranchFollow = 1f;
            l.CardSize = new Vector2(0.4f, 0.8f);
            k.LeafBottom = new Color(0.85f, 0.45f, 0.6f);
            k.LeafTop = new Color(1f, 0.82f, 0.9f);
            k.ShadowColor = new Color(0.6f, 0.3f, 0.5f);
            k.RimColor = new Color(1f, 0.92f, 0.96f);
            k.PatchColor = new Color(1f, 1f, 1f, 0.6f);
            k.PatchAmount = 0.35f;
            k.PatchScale = 0.3f;
            k.BarkBottom = new Color(0.14f, 0.08f, 0.08f);
            k.BarkTop = new Color(0.4f, 0.26f, 0.26f);
            k.MossAmount = 0f;
        }

        // Cotton Candy's fluffy leaf groups, which replace the shared Twisted-style values for the cherry
        private void CherryPuffs()
        {
            LeafSettings l = Leaves;
            l.NormalOffset = 0.2f;
            l.GroupVolume = 0.5f;
            l.Coverage = 0.5f;
            l.PlacementBias = 0.75f;
            l.BranchTipBias = 0.5f;
            l.GroupQuads = 3;
            l.GroupDepth = 0.5f;
            l.GroupJitter = 1f;
            Look.RimStrength = 0.35f;
            Look.GrainAmount = 0.2f;
        }

        // round and fluffy; the numbers come from ApplyBushAsset
        private void Bush() => Stub(0.5f, 0.05f);

        // long flat-topped row; HedgeHigh uses the same settings with a taller blob layout
        private void Hedge()
        {
            Stub(0.2f, 0.01f);
            Leaves.CardSize = new Vector2(0.25f, 0.5f);
            Look.LeafBottom = new Color(0.08f, 0.26f, 0.1f);
            Look.LeafTop = new Color(0.38f, 0.65f, 0.2f);
        }

        // dense regular cone: stacked narrowing tiers on a straight trunk that leaves a leader on top
        private void Fir()
        {
            TrunkSettings t = Trunk;
            LookSettings k = Look;
            Stub(8f, 0.25f);
            Seed = 2;
            t.TipRadiusRatio = 0.08f;
            t.Bend = 0.02f;
            t.NoiseBend = 0f;
            t.Flare = 0.4f;
            Leaves.CardSize = new Vector2(0.3f, 0.7f);
            k.LeafBottom = new Color(0.03f, 0.14f, 0.12f);
            k.LeafTop = new Color(0.16f, 0.45f, 0.3f);
            k.ShadowColor = new Color(0.02f, 0.1f, 0.15f);
            k.MossAmount = 0f;
        }

        // shape presets (Bush, Hedge, Fir) get their canopy from PresetBlobs, so branches and crown anchoring are minimal
        private void Stub(float height, float radius)
        {
            TrunkSettings t = Trunk;
            BranchSettings b = Branches;
            Seed = 1;
            t.Height = height;
            t.BaseRadius = radius;
            t.Twist = 0f;
            t.Splits = 0;
            t.Gnarl = 0f;
            t.RootLobes = 0;
            t.Bend = 0f;
            b.Count = 1;
            b.Levels = 1;
            b.LengthRatio = 0.3f;
            Leaves.CrownOnTrunkTop = false;
        }

        // What CottonCandy, Hedge, Twisted, Oak and Maple share in their tuned recipes, for the presets without a recipe of their own.
        // Shape and colours stay the preset's.
        private void ApplySharedAssetTuning(TreePreset preset)
        {
            LeafSettings l = Leaves;
            LookSettings k = Look;
            Trunk.TaperPower = 3f;
            Branches.YawStep = new Vector2(137.508f, 137.508f);
            Branches.NoiseFrequency = 0.97f;
            l.Smoothness = 1f;
            l.TipBias = 0.096f;
            l.NormalBlend = 0.83f;
            l.Attach = 0.319f;
            l.RollJitter = 180f;
            l.GroupTilt = 70f;
            l.GroupTiltJitter = 25f;
            l.UpBias = 0f;
            l.Anchor = new Vector2(0.5f, 0.25f);
            l.Envelope = preset == TreePreset.Poplar ? CrownShape.Column : CrownShape.Dome;
            l.EnvelopeStrength = 0.6f;
            l.ClumpGather = 0.425f;
            l.Puffiness = 1f;
            l.Shell = 0.45f;
            l.FlattenBottom = 0.5f;
            l.Coverage = 0.5f;
            l.GroupVolume = 0.6f;
            l.NormalOffset = 0.5f;
            l.GroupQuads = 3;
            l.GroupDepth = 0.5f;
            l.GroupJitter = 1f;
            l.BranchTipBias = 0.8f;
            Renormalize.Enabled = true;
            Renormalize.Tightness = 0.35f;
            Renormalize.Strength = 1f;
            Renormalize.AOStrength = 0.6f;
            Budget.AddTrunkCollider = true;
            Lods.Generate = true;
            Lods.Lod1Screen = 0.4f;
            Lods.Lod2Screen = 0.12f;
            Lods.CullScreen = 0.02f;
            Lods.Lod1CardShare = 0.5f;
            Lods.Lod2CardShare = 0.2f;
            Lods.Lod2Intersecting = true;
            k.ContinuousColor = true;
            k.Bands = 2;
            k.GradientBias = 1f;
            k.GradientJitter = 0.1f;
            k.Wrap = 0.35f;
            k.Translucency = 0.4f;
            k.TranslucencyColor = new Color(0.6f, 0.9f, 0.2f, 1f);
            k.TranslucencyFocus = 3f;
            k.ShadowAmount = 0.5f;
            k.ShadowSoftness = 1f;
            k.TreeVariation = 0.4f;
            k.BarkTiling = 0.3f;
        }

        // runs after ApplyBlobLook, which resets grain and rim
        private void BlobPresetFinish(TreePreset preset)
        {
            if (!IsBlobPreset(preset)) return;
            Look.GrainAmount = 0.5f;
            Look.RimStrength = 0.3f;
            if (preset == TreePreset.Hedge || preset == TreePreset.HedgeHigh) ApplyHedgeLook();
            if (preset == TreePreset.CottonCandy)
            {
                Look.GrainAmount = 0.2f;
                Look.RimStrength = 0.35f;
            }
        }

        // Hedge.asset values that ApplyBlobLook would otherwise change (textures stay as they are)
        private void ApplyHedgeLook()
        {
            Branches.MinSides = 6;
            Branches.Segments = 5;
            LeafSettings l = Leaves;
            LookSettings k = Look;
            l.Coverage = 1f;
            l.GroupVolume = 0.5f;
            l.NormalOffset = 0.1f;
            l.PlacementBias = 0.25f;
            l.GroupQuads = 1;
            l.GroupDepth = 0.5f;
            l.Smoothness = 1f;
            l.BlobOverlap = 1f;
            l.BlobSizeSpread = 1f;
            l.BlobFlatBelly = 1f;
            l.BlobTaper = 1f;
            l.BranchTipBias = 0f;
            l.CardSize = new Vector2(0.25f, 0.5f);
            Lods.Lod1CardShare = 0.5f;
            Lods.Lod2CardShare = 0.2f;
            k.Bands = 0;
            k.ColorVariation = 0f;
            k.Translucency = 0.4f;
            k.ContinuousColor = false;
        }

        private static bool IsBlobPreset(TreePreset p) => p != TreePreset.Oak && p != TreePreset.Dead && p != TreePreset.Maple && p != TreePreset.Spruce;

        /// <summary>The fixed blob layout of Bush, Hedge, HedgeHigh and Fir (shapes, not branch tips); null for every other preset. Auto Blobs, new seeds and variants keep it.</summary>
        public List<CanopyBlob> PresetBlobs()
        {
            switch (LastPreset)
            {
                case TreePreset.Bush:
                    return BushBlobs();
                case TreePreset.Hedge:
                    return HedgeBlobs(1);
                case TreePreset.HedgeHigh:
                    return HedgeBlobs(2);
                case TreePreset.Fir:
                    return FirBlobs();
                default:
                    return null;
            }
        }

        private static CanopyBlob Blob(float x, float y, float z, float rx, float ry, float rz, float flat = 0f) =>
            new CanopyBlob { Position = new Vector3(x, y, z), Radii = new Vector3(rx, ry, rz), FlatBelly = flat };

        // one round, slightly flat-bellied blob
        private static List<CanopyBlob> BushBlobs() => new List<CanopyBlob> { Blob(0f, 0.9f, 0f, 1.2f, 1f, 1.2f, 0.15f) };

        // rows of five blobs; one row is the low hedge, two stacked rows the tall wall
        private static List<CanopyBlob> HedgeBlobs(int rows)
        {
            var list = new List<CanopyBlob>();
            for (int row = 0; row < rows; row++)
                for (int i = 0; i < 5; i++)
                    list.Add(rows == 1
                        ? Blob(-2.4f + i * 1.2f, 0.5f, 0f, 0.9f, 0.7f, 0.8f, 0.5f)
                        : Blob(-2.4f + i * 1.2f, 0.8f + row * 1.1f, 0f, 0.9f, 0.9f, 0.8f, row == 0 ? 0.5f : 0.2f));
            return list;
        }

        // six narrowing tiers; the top one reaches the trunk tip (8 m) so the leader is covered
        private static List<CanopyBlob> FirBlobs()
        {
            var list = new List<CanopyBlob>();
            for (int i = 0; i < 6; i++)
            {
                float u = i / 5f;
                list.Add(Blob(0f, 1.7f + u * 5.3f, 0f, Mathf.Lerp(1.7f, 0.4f, u), Mathf.Lerp(0.8f, 0.7f, u), Mathf.Lerp(1.7f, 0.4f, u), 0.6f));
            }
            return list;
        }

    }
}
