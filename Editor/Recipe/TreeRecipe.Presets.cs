using System;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    public partial class TreeRecipe
    {
        /// <summary>Resets trunk, branch, leaf and look numbers to the preset. Your textures and materials stay; untextured drops the pack textures and uses solid cards.</summary>
        public void ApplyPreset(TreePreset preset, bool textured = true)
        {
            if (string.IsNullOrEmpty(TypeId) || TypeId == LastPreset.ToString()) TypeId = preset.ToString(); // a typed name stays
            LastPreset = preset;
            if (Variants.Count == 0) CreateVariants();
            ResetSettings(preset, textured);
            ApplyBase(preset);
            Stylize(preset);
            Canopy(preset);
            Branches.MinSides = 6;
            if (!textured && Leaves.Layout == LeafLayout.Blobs) ApplyBlobLook();
            BlobPresetFinish(preset);
            ApplyCore(preset);
            ApplyBudget(preset);
            ApplyRecipeAsset(preset);
            Look.StylizedShading = true;
            if (Leaves.Layout == LeafLayout.Blobs) Blobs = TreeGenerator.SuggestBlobs(this);
        }

        // every tree gets a shell under its leaves except spruce (fronds) and dead (no leaves)
        private void ApplyCore(TreePreset preset)
        {
            if (preset == TreePreset.Spruce || preset == TreePreset.Dead) return;
            Look.CoreMesh = true;
            bool matchCottonCandy = preset == TreePreset.Maple; // Maple.asset carries the CottonCandy.asset core
            Look.CoreAlign = matchCottonCandy;
            Look.CoreSegments = matchCottonCandy ? 8 : CoreSegmentsLow;
            if (!matchCottonCandy) return;
            // the core sits GroupVolume below the blob surface: more volume and less normal offset pull it inward
            Leaves.GroupVolume = 0.5f;
            Leaves.NormalOffset = 0.2f;
        }

        // low-poly caps applied last so they win over every preset and look; the generator drops leaf cards to stay inside them
        private const int CoreSegmentsLow = 6;
        private const int MaxTrianglesPerTree = 3000; // hard ceiling is 7000

        private void ApplyBudget(TreePreset preset)
        {
            if (preset == TreePreset.Dead) return;
            Budget.MaxTriangles = MaxTrianglesPerTree;
            Budget.MaxCards = 1000;
            Budget.FitCardsToTriangles = true;
            Trunk.RadialSides = Mathf.Min(Trunk.RadialSides, 5);
            Trunk.HeightSegments = Mathf.Min(Trunk.HeightSegments, 3);
            Branches.Segments = Mathf.Min(Branches.Segments, 2);
            Branches.MinSides = 3;
        }

        private void ResetSettings(TreePreset preset, bool textured)
        {
            Trunk = new TrunkSettings();
            Branches = new BranchSettings();
            Leaves = new LeafSettings();
            Renormalize = new RenormalizeSettings();
            Blobs = new System.Collections.Generic.List<CanopyBlob>();
            Budget.MaxCards = 1200;
            Budget.MaxTriangles = 6000;

            bool stylized = Look.StylizedShading;
            Texture2D leafTex = Look.LeafTexture, barkTex = Look.BarkTexture, mossTex = Look.MossTexture;
            LeafShapeSettings oldShapes = LeafShapes ?? new LeafShapeSettings();
            LeafShapes = new LeafShapeSettings();
            if (!PresetTextures.IsReplaceable(leafTex)) // the grid belongs to a custom texture
            {
                LeafShapes.Columns = oldShapes.Columns;
                LeafShapes.Rows = oldShapes.Rows;
            }
            Material bark = Look.Bark, leaf = Look.Leaf;
            Look = new LookSettings
            {
                Bark = bark,
                Leaf = leaf,
                StylizedShading = stylized,
                LeafTexture = PresetTextures.IsReplaceable(leafTex) ? (textured ? PresetTextures.Leaf(preset) : null) : leafTex,
                BarkTexture = PresetTextures.IsReplaceable(barkTex) ? (textured ? PresetTextures.Bark(preset) : null) : barkTex,
                MossTexture = PresetTextures.IsReplaceable(mossTex) ? (textured ? PresetTextures.Moss() : null) : mossTex,
                SolidCards = !textured,
            };
        }

        private void ApplyBase(TreePreset preset)
        {
            switch (preset)
            {
                case TreePreset.Dead:
                    DeadBase();
                    break;
                case TreePreset.Maple:
                    MapleBase();
                    break;
                case TreePreset.Spruce:
                    SpruceBase();
                    break;
            }
        }

        private void DeadBase()
        {
            Trunk.Height = 3.5f;
            Trunk.Bend = 0.5f;
            Branches.Count = 6;
            Branches.PitchRange = new Vector2(35f, 70f);
            Branches.LengthRatio = 0.6f;
            Branches.Wander = 0.6f;
            Leaves.CardsPerBranch = 0;
            Leaves.CrownOnTrunkTop = false;
            Look.BarkBottom = new Color(0.1f, 0.08f, 0.07f);
            Look.BarkTop = new Color(0.35f, 0.3f, 0.27f);
            Look.MossAmount = 0.1f;
        }

        // wide airy crown: thin stems, small leaves in puffs at the branch tips
        private void MapleBase()
        {
            Trunk.Height = 4.2f;
            Trunk.BaseRadius = 0.28f;
            Trunk.TipRadiusRatio = 0.2f;
            Trunk.Bend = 0.12f;
            Trunk.Lean = 0.02f;
            Trunk.Flare = 0.7f;
            Branches.Count = 10;
            Branches.StartEnd = new Vector2(0.45f, 0.95f);
            Branches.PitchRange = new Vector2(35f, 65f);
            Branches.LengthRatio = 0.85f;
            Branches.LengthProfile = new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.5f));
            Branches.RadiusRatio = 0.4f;
            Branches.Segments = 4;
            Branches.SubCount = 4;
            Branches.SubLengthRatio = 0.55f;
            Branches.Droop = 0.1f;
            Leaves.CardsPerBranch = 30;
            Leaves.ClusterRadius = 0.5f;
            Leaves.CardSize = new Vector2(0.4f, 0.65f);
            Leaves.RollJitter = 180f;
            Leaves.Attach = 0.2f;
            Leaves.Anchor = new Vector2(0.5f, 0.5f);
            Leaves.UpBias = 0.3f;
            Budget.MaxCards = 2400;
            Budget.MaxTriangles = 10000;
            Look.LeafBottom = new Color(0.4f, 0.08f, 0.04f);
            Look.LeafTop = new Color(1f, 0.55f, 0.1f);
            Look.ColorVariation = 0.9f;
            Look.GradientJitter = 0.35f;
            Look.MossAmount = 0.1f;
        }

        // tall dense cone
        private void SpruceBase()
        {
            Trunk.Height = 7.5f;
            Trunk.BaseRadius = 0.24f;
            Trunk.TipRadiusRatio = 0.04f;
            Trunk.Bend = 0.05f;
            Trunk.Lean = 0.01f;
            Branches.Levels = 1;
            Branches.Count = 48;
            Branches.StartEnd = new Vector2(0.38f, 0.98f);
            Branches.PitchRange = new Vector2(65f, 95f);
            Branches.LengthRatio = 0.38f;
            Branches.LengthProfile = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.05f));
            Branches.RadiusRatio = 0.289f;
            Branches.Droop = 0.5f;
            Branches.Wander = 0.08f;
            Branches.Segments = 3;
            Branches.WhorlSize = 6;
            Leaves.CardsPerBranch = 10;
            Leaves.ClusterRadius = 0.4f;
            Leaves.CardSize = new Vector2(0.7f, 1.05f);
            Leaves.TipBias = 0.15f;
            Leaves.NormalBlend = 0.9f;
            Leaves.UpBias = 0.1f;
            Leaves.Attach = 0.8f;
            Leaves.Anchor = new Vector2(0.5f, 0.5f);
            Look.LeafBottom = new Color(0.02f, 0.1f, 0.12f);
            Look.LeafTop = new Color(0.12f, 0.4f, 0.28f);
            Look.ColorVariation = 0.25f;
            Look.GradientJitter = 0.2f;
            Renormalize.Tightness = 0.341f;
            Renormalize.Strength = 0.333f;
            Renormalize.AOStrength = 0.459f;
        }

        /// <summary>Swaps the texture slots of the current recipe without resetting anything else. Your own textures are never touched; solid cards follow the flag.</summary>
        public void SetTextured(bool textured)
        {
            LookSettings k = Look;
            if (PresetTextures.IsReplaceable(k.LeafTexture)) k.LeafTexture = textured ? PresetTextures.Leaf(LastPreset) : null;
            if (PresetTextures.IsReplaceable(k.BarkTexture)) k.BarkTexture = textured ? PresetTextures.Bark(LastPreset) : null;
            if (PresetTextures.IsReplaceable(k.MossTexture)) k.MossTexture = textured ? PresetTextures.Moss() : null;
            if (!textured && PresetTextures.IsReplaceable(k.FillTexture)) k.FillTexture = null;
            k.SolidCards = !textured;
        }

        // deciduous crowns are blobs filled with small cards; branch tips place the blobs unless the preset fixes them
        private void Canopy(TreePreset preset)
        {
            switch (preset)
            {
                case TreePreset.Oak:
                    OakDefault();
                    break;
                case TreePreset.Spruce:
                    SpruceDefault();
                    break;
                case TreePreset.Maple:
                    MapleDefault();
                    break;
                case TreePreset.Dead:
                    break; // bare wood
                default:
                    BlobPreset(preset);
                    break;
            }
        }

        // the values of Assets/FoliageForge/Recipes/Oak_Default/Oak_Default.asset (textures stay as they are)
        private void OakDefault()
        {
            TrunkSettings t = Trunk;
            t.Height = 7f;
            t.BaseRadius = 0.3f;
            t.TipRadiusRatio = 0.25f;
            t.RadialSides = 6;
            t.HeightSegments = 2;
            t.Bend = 0.2f;
            t.Lean = 0.058f;
            t.Flare = 0.8f;
            t.TaperPower = 3f;
            t.Twist = -72f;
            t.RootLobes = 0;
            t.RootLobeDepth = 0.6f;
            t.RootLobeHeight = 0.2f;
            t.Gnarl = 0.08f;
            t.NoiseBend = 0.04f;
            t.NoiseFrequency = 1.5f;
            t.Splits = 2;
            t.SplitHeight = 0.5f;
            t.SplitAngle = 30f;
            t.Braid = 0.1f;
            t.Roots = 0;
            t.RootLength = 0.25f;
            t.RootRadius = 0.393f;
            t.RootWander = 0.437f;
            t.RootCurl = 0f;
            BranchSettings b = Branches;
            b.Levels = 2;
            b.Count = 8;
            b.StartEnd = new Vector2(0.3f, 0.85f);
            b.PitchRange = new Vector2(30f, 50f);
            b.LengthRatio = 0.37f;
            b.LengthProfile = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.5f));
            b.RadiusRatio = 0.5f;
            b.Droop = -0.1f;
            b.Wander = 0.511f;
            b.Segments = 5;
            b.SubCount = 3;
            b.SubLengthRatio = 0.5f;
            b.WhorlSize = 2;
            b.NoiseBend = 0.25f;
            b.NoiseFrequency = 0.97f;
            b.LengthNoise = 0.3f;
            b.Asymmetry = 0f;
            b.Broken = 0f;
            LeafSettings l = Leaves;
            l.Layout = LeafLayout.Blobs;
            l.CardsPerBranch = 30;
            l.CardsPerBlob = 0;
            l.Coverage = 1f;
            l.GroupVolume = 1f;
            l.NormalOffset = 0.5f;
            l.PlacementBias = 0f;
            l.GroupQuads = 3;
            l.GroupDepth = 0.5f;
            l.GroupJitter = 1f;
            l.Smoothness = 1f;
            l.BlobCount = 6;
            l.StrandLength = 1.5f;
            l.ClusterRadius = 1f;
            l.CardSize = new Vector2(0.42f, 1f);
            l.Aspect = 1f;
            l.TipBias = 0.096f;
            l.NormalBlend = 0.83f;
            l.UpBias = 0f;
            l.Attach = 0.319f;
            l.Anchor = new Vector2(0.484375f, 0.046666667f);
            l.Roll = 0f;
            l.RollJitter = 180f;
            l.CrownOnTrunkTop = true;
            l.OnInnerBranches = false;
            l.Intersecting = true;
            l.CardsPerCluster = 0;
            l.BranchTipBias = 0.8f;
            l.Billboard = BillboardMode.Off;
            l.Envelope = CrownShape.Dome;
            l.EnvelopeStrength = 0.6f;
            l.Clumps = 0;
            l.ClumpGather = 0.425f;
            l.Puffiness = 1f;
            l.TintPerClump = true;
            l.TipClumps = false;
            l.Shell = 0.45f;
            l.FlattenBottom = 0.5f;
            l.ClumpHeight = 1f;
            LeafShapes.Fold = 0f;
            LeafShapes.Curl = 0f;
            LeafShapes.Twist = 0f;
            LeafShapes.Jitter = 0.3f;
            Renormalize.Enabled = true;
            Renormalize.Tightness = 0.35f;
            Renormalize.Strength = 1f;
            Renormalize.AOStrength = 0.6f;
            Budget.MaxCards = 2400;
            Budget.MaxTriangles = 5000;
            LookSettings k = Look;
            k.LeafBottom = new Color(0.07f, 0.2f, 0.1f);
            k.LeafTop = new Color(0.55f, 0.8f, 0.25f);
            k.GradientBias = 1f;
            k.GradientJitter = 0.1f;
            k.ColorVariation = 0f;
            k.Wrap = 0.35f;
            k.Translucency = 0.4f;
            k.TranslucencyColor = new Color(0.6f, 0.9f, 0.2f);
            k.TranslucencyFocus = 3f;
            k.ShadowColor = new Color(0.08f, 0.22f, 0.25f);
            k.ShadowAmount = 0.5f;
            k.ShadowSoftness = 1f;
            k.RimColor = new Color(0.2557371f, 0.433f, 0.21734904f);
            k.RimStrength = 0f;
            k.RimPower = 0.5f;
            k.HueShift = 0f;
            k.TreeVariation = 0.4f;
            k.PatchColor = new Color(0.94f, 0.76f, 0.25f, 0.6f);
            k.PatchAmount = 0f;
            k.PatchScale = 0.02f;
            k.FillScale = 1f;
            k.FillImpact = 0.5f;
            k.BarkBottom = new Color(0.16f, 0.1f, 0.07f);
            k.BarkTop = new Color(0.42f, 0.3f, 0.2f);
            k.BarkTiling = 0.3f;
            k.MossColor = new Color(0.25f, 0.42f, 0.12f);
            k.MossAmount = 0.2f;
            k.MossSharpness = 6f;
            k.MossTiling = 1.5f;
        }

        // the values of Assets/FoliageForge/Recipes/Maple.asset (textures stay as they are)
        private void MapleDefault()
        {
            TrunkSettings t = Trunk;
            t.Height = 4.2f;
            t.BaseRadius = 0.28f;
            t.TipRadiusRatio = 0.2f;
            t.RadialSides = 8;
            t.HeightSegments = 7;
            t.Bend = 0.12f;
            t.Lean = 0.02f;
            t.Flare = 0.7f;
            t.TaperPower = 0.65f;
            t.Twist = 0f;
            t.RootLobes = 5;
            t.RootLobeDepth = 0.5f;
            t.RootLobeHeight = 0.2f;
            t.Gnarl = 0.06f;
            t.NoiseBend = 0f;
            t.NoiseFrequency = 1.5f;
            t.Splits = 1;
            t.SplitHeight = 0.5f;
            t.SplitAngle = 25f;
            t.Braid = 0f;
            t.Roots = 4;
            t.RootLength = 0.3f;
            t.RootRadius = 0.6f;
            t.RootWander = 0.4f;
            t.RootCurl = 25f;
            BranchSettings b = Branches;
            b.Levels = 2;
            b.Count = 10;
            b.StartEnd = new Vector2(0.45f, 0.95f);
            b.PitchRange = new Vector2(35f, 65f);
            b.LengthRatio = 0.85f;
            b.LengthProfile = new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.5f));
            b.RadiusRatio = 0.4f;
            b.Droop = 0.1f;
            b.Wander = 0.35f;
            b.Segments = 4;
            b.SubCount = 4;
            b.SubLengthRatio = 0.55f;
            b.WhorlSize = 1;
            b.YawStep = new Vector2(137.508f, 137.508f);
            b.NoiseBend = 0.1f;
            b.NoiseFrequency = 2f;
            b.LengthNoise = 0.25f;
            b.Asymmetry = 0.15f;
            b.Broken = 0f;
            LeafSettings l = Leaves;
            l.Layout = LeafLayout.Blobs;
            l.CardsPerBranch = 30;
            l.CardsPerBlob = 0;
            l.Coverage = 1f;
            l.GroupVolume = 0f;
            l.NormalOffset = 0.5f;
            l.PlacementBias = 1f;
            l.GroupQuads = 3;
            l.GroupDepth = 0.5f;
            l.GroupJitter = 1f;
            l.Smoothness = 1f;
            l.BlobCount = 6;
            l.BlobSize = new Vector2(0.4f, 3f);
            l.BranchFollow = 1f;
            l.StrandLength = 1.5f;
            l.ClusterRadius = 0.78f;
            l.CardSize = new Vector2(0.42f, 0.64f);
            l.Aspect = 1f;
            l.TipBias = 0.45f;
            l.NormalBlend = 0.85f;
            l.UpBias = 0.3f;
            l.Attach = 0f;
            l.Anchor = new Vector2(0.5f, 0.25f);
            l.Roll = 0f;
            l.RollJitter = 180f;
            l.CrownOnTrunkTop = true;
            l.OnInnerBranches = false;
            l.Intersecting = true;
            l.CardsPerCluster = 0;
            l.BranchTipBias = 1f;
            l.Billboard = BillboardMode.Off;
            l.Envelope = CrownShape.Dome;
            l.EnvelopeStrength = 0.5f;
            l.Clumps = 0;
            l.ClumpGather = 0.35f;
            l.Puffiness = 1f;
            l.TintPerClump = true;
            l.TipClumps = false;
            l.Shell = 0.5f;
            l.FlattenBottom = 0.4f;
            l.ClumpHeight = 1f;
            LeafShapes.Fold = 0f;
            LeafShapes.Curl = 0f;
            LeafShapes.Twist = 0f;
            LeafShapes.Jitter = 0.3f;
            Renormalize.Enabled = true;
            Renormalize.Tightness = 0.35f;
            Renormalize.Strength = 1f;
            Renormalize.AOStrength = 0.6f;
            Budget.MaxCards = 2400;
            Budget.MaxTriangles = 10000;
            Budget.FitCardsToTriangles = true;
            LookSettings k = Look;
            k.LeafBottom = new Color(0.4f, 0.08f, 0.04f);
            k.LeafTop = new Color(0.90099996f, 0.49466667f, 0.09186666f);
            k.GradientBias = 1f;
            k.GradientJitter = 0.35f;
            k.ColorVariation = 0f;
            k.Wrap = 0.35f;
            k.Translucency = 0.4f;
            k.TranslucencyColor = new Color(0.6f, 0.9f, 0.2f);
            k.TranslucencyFocus = 3f;
            k.ShadowColor = new Color(0.42f, 0.08f, 0.1f);
            k.ShadowAmount = 0.6f;
            k.ShadowSoftness = 0.35f;
            k.RimColor = new Color(1f, 0.3098039f, 0.25098038f);
            k.RimStrength = 0.35f;
            k.RimPower = 3f;
            k.HueShift = 0f;
            k.TreeVariation = 0f;
            k.PatchColor = new Color(0.94f, 0.76f, 0.25f, 0.6f);
            k.PatchAmount = 0f;
            k.PatchScale = 0.25f;
            k.FillScale = 1.15f;
            k.FillImpact = 0.5f;
            k.BarkBottom = new Color(0.16f, 0.1f, 0.07f);
            k.BarkTop = new Color(0.42f, 0.3f, 0.2f);
            k.BarkTiling = 1f;
            k.MossColor = new Color(0.25f, 0.42f, 0.12f);
            k.MossAmount = 0.1f;
            k.MossSharpness = 6f;
            k.MossTiling = 1.5f;
        }

        // the values of Assets/FoliageForge/Recipes/Spruce_Default/Spruce_Default.asset (textures stay as they are)
        private void SpruceDefault()
        {
            TrunkSettings t = Trunk;
            t.Height = 7.5f;
            t.BaseRadius = 0.3f;
            t.TipRadiusRatio = 0.04f;
            t.RadialSides = 5;
            t.HeightSegments = 7;
            t.Bend = 0.05f;
            t.Lean = 0.01f;
            t.Flare = 0.4f;
            t.TaperPower = 0.75f;
            t.Twist = -307f;
            t.RootLobes = 4;
            t.RootLobeDepth = 0.35f;
            t.RootLobeHeight = 0.1f;
            t.Gnarl = 0f;
            t.NoiseBend = 0.05f;
            t.NoiseFrequency = 1.5f;
            t.Splits = 0;
            t.SplitHeight = 0.4f;
            t.SplitAngle = 20f;
            t.Braid = 0f;
            t.Roots = 0;
            t.RootLength = 0.4f;
            t.RootRadius = 0.6f;
            t.RootWander = 0.4f;
            t.RootCurl = 25f;
            BranchSettings b = Branches;
            b.Levels = 1;
            b.Count = 48;
            b.StartEnd = new Vector2(0.38f, 0.98f);
            b.PitchRange = new Vector2(65f, 95f);
            b.LengthRatio = 0.38f;
            b.LengthProfile = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.05f));
            b.RadiusRatio = 0.289f;
            b.Droop = 0.5f;
            b.Wander = 0.08f;
            b.Segments = 3;
            b.SubCount = 3;
            b.SubLengthRatio = 0.5f;
            b.WhorlSize = 6;
            b.NoiseBend = 0.05f;
            b.NoiseFrequency = 2f;
            b.LengthNoise = 0.1f;
            b.Asymmetry = 0f;
            b.Broken = 0f;
            LeafSettings l = Leaves;
            l.Layout = LeafLayout.Fronds;
            l.CardsPerBranch = 3;
            l.CardsPerBlob = 60;
            l.NormalOffset = 0f;
            l.BlobCount = 7;
            l.StrandLength = 1.5f;
            l.ClusterRadius = 0.4f;
            l.CardSize = new Vector2(0.8f, 1.1f);
            l.Aspect = 1f;
            l.TipBias = 0.15f;
            l.NormalBlend = 0.9f;
            l.UpBias = 0.1f;
            l.Attach = 0.8f;
            l.Anchor = new Vector2(0.4915254f, 0.02f);
            l.Roll = 0f;
            l.RollJitter = 70f;
            l.CrownOnTrunkTop = true;
            l.OnInnerBranches = false;
            l.Intersecting = true;
            l.CardsPerCluster = 0;
            l.BranchTipBias = 0f;
            l.Billboard = BillboardMode.Off;
            l.Envelope = CrownShape.Cone;
            l.EnvelopeStrength = 0.7f;
            l.Clumps = 0;
            l.ClumpGather = 0.35f;
            l.Puffiness = 0.6f;
            l.TintPerClump = true;
            l.TipClumps = false;
            l.Shell = 0f;
            l.FlattenBottom = 0f;
            l.ClumpHeight = 1f;
            LeafShapes.WidthSegments = 1;
            LeafShapes.LengthSegments = 1;
            LeafShapes.Fold = -25f;
            LeafShapes.Curl = 148f;
            LeafShapes.Twist = 0f;
            LeafShapes.Jitter = 0.3f;
            Renormalize.Enabled = true;
            Renormalize.Tightness = 0.341f;
            Renormalize.Strength = 0.333f;
            Renormalize.AOStrength = 0.459f;
            Budget.MaxCards = 1200;
            Budget.MaxTriangles = 6000;
            Budget.FitCardsToTriangles = true;
            Budget.AddTrunkCollider = true;
            LookSettings k = Look;
            k.LeafBottom = new Color(0.08511956f, 0.191f, 0.031141296f);
            k.LeafTop = new Color(0.33905622f, 0.5254902f, 0.16078432f);
            k.GradientBias = 1.5f;
            k.GradientJitter = 0.35f;
            k.ColorVariation = 0.25f;
            k.Wrap = 0.35f;
            k.Translucency = 0.4f;
            k.TranslucencyColor = new Color(0.6f, 0.9f, 0.2f);
            k.TranslucencyFocus = 3f;
            k.ShadowColor = new Color(0.02f, 0.08f, 0.12f);
            k.ShadowAmount = 0.6f;
            k.ShadowSoftness = 0.35f;
            k.RimColor = new Color(0.6f, 0.9f, 0.7f);
            k.RimStrength = 0f;
            k.RimPower = 5.28f;
            k.HueShift = 0.091f;
            k.TreeVariation = 1f;
            k.PatchColor = new Color(0.94f, 0.76f, 0.25f, 0.6f);
            k.PatchAmount = 0.3f;
            k.PatchScale = 0.25f;
            k.FillScale = 0.57f;
            k.FillImpact = 0.5f;
            k.BarkBottom = new Color(0.16f, 0.1f, 0.07f);
            k.BarkTop = new Color(0.42f, 0.3f, 0.2f);
            k.BarkTiling = 1f;
            k.MossColor = new Color(0.25f, 0.42f, 0.12f);
            k.MossAmount = 0f;
            k.MossSharpness = 6f;
            k.MossTiling = 1.5f;
        }

        // stylized layer on top of each preset: three-tone leaves, puffy clumps, folded cards
        private void Stylize(TreePreset preset)
        {
            Look.ShadowAmount = 0.6f;
            Look.RimStrength = 0.35f;
            Look.HueShift = 0.03f;
            Look.TreeVariation = 0.3f;
            Look.PatchAmount = 0.3f;
            switch (preset)
            {
                case TreePreset.Dead:
                    StylizeDead();
                    break;
                case TreePreset.Maple:
                    StylizeMaple();
                    break;
                case TreePreset.Spruce:
                    StylizeSpruce();
                    break;
            }
        }

        // broken stubs over exposed roots
        private void StylizeDead()
        {
            TrunkSettings t = Trunk;
            BranchSettings b = Branches;
            t.TaperPower = 0.6f;
            t.Twist = 60f;
            t.RootLobes = 4;
            t.RootLobeDepth = 0.7f;
            t.Gnarl = 0.15f;
            t.NoiseBend = 0.06f;
            t.Roots = 5;
            t.RootLength = 0.3f;
            b.NoiseBend = 0.15f;
            b.Broken = 0.45f;
        }

        // autumn: red shadows, orange rim, lopsided multi-stem crown
        private void StylizeMaple()
        {
            TrunkSettings t = Trunk;
            BranchSettings b = Branches;
            LeafSettings l = Leaves;
            LookSettings k = Look;
            t.TaperPower = 0.65f;
            t.RootLobes = 5;
            t.RootLobeDepth = 0.5f;
            t.Gnarl = 0.06f;
            t.Splits = 1;
            t.SplitHeight = 0.5f;
            t.SplitAngle = 25f;
            t.Twist = 0f;
            t.Roots = 4;
            t.RootLength = 0.3f;
            b.Asymmetry = 0.15f;
            b.NoiseBend = 0.1f;
            b.LengthNoise = 0.25f;
            l.Envelope = CrownShape.Dome;
            l.EnvelopeStrength = 0.5f;
            l.TipClumps = true;
            l.Puffiness = 0.8f;
            l.Shell = 0.45f;
            l.FlattenBottom = 0.35f;
            l.ClumpHeight = 0.7f;
            LeafShapes.Fold = 20f;
            k.ShadowColor = new Color(0.42f, 0.08f, 0.1f);
            k.RimColor = new Color(1f, 0.6f, 0.25f);
            k.HueShift = 0.05f;
            k.TreeVariation = 0.45f;
        }

        // fronds along every branch and a leader on top
        private void StylizeSpruce()
        {
            TrunkSettings t = Trunk;
            LeafSettings l = Leaves;
            LookSettings k = Look;
            t.RootLobes = 4;
            t.RootLobeDepth = 0.35f;
            t.RootLobeHeight = 0.1f;
            l.Envelope = CrownShape.Cone;
            l.EnvelopeStrength = 0.7f;
            l.Layout = LeafLayout.Fronds;
            l.CardsPerBranch = 3;
            l.RollJitter = 70f;
            l.CardSize = new Vector2(0.8f, 1.1f);
            LeafShapes.Fold = -25f;
            k.ShadowColor = new Color(0.02f, 0.08f, 0.12f);
            k.RimColor = new Color(0.6f, 0.9f, 0.7f);
            k.RimStrength = 0.3f;
        }
    }
}
