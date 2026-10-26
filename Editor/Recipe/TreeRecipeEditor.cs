using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Meowdieval.FoliageForge
{
    /// <summary>A titled foldout of recipe property paths; <see cref="Subs"/> nest inside it as collapsed foldouts.</summary>
    public sealed class GroupDef
    {
        /// <summary>Foldout title.</summary>
        public string Title;
        /// <summary>Serialized property paths shown in order. Paths starting with '@' are special elements, not properties.</summary>
        public string[] Paths;
        /// <summary>Nested foldouts shown after the paths.</summary>
        public GroupDef[] Subs = new GroupDef[0];

        /// <summary>Adds nested foldouts and returns this group.</summary>
        public GroupDef With(params GroupDef[] subs)
        {
            Subs = subs;
            return this;
        }
    }

    /// <summary>A named tab of the recipe inspector.</summary>
    public sealed class TabDef
    {
        /// <summary>Tab button text.</summary>
        public string Name;
        /// <summary>Foldouts shown while the tab is selected.</summary>
        public GroupDef[] Groups;
    }

    /// <summary>Shows a recipe as Shape / Foliage / Look / Output tabs and adds a shortcut to the Foliage Forge window.</summary>
    [CustomEditor(typeof(TreeRecipe))]
    public sealed class TreeRecipeEditor : Editor
    {
        const string TabKey = "FoliageForge.RecipeEditorTab";
        const string InsetToken = "@Inset";
        const string AnchorToken = "@Anchor";
        const string VariantsToken = "@Variants";

        /// <summary>Properties shown above the tabs, outside every group.</summary>
        public static readonly string[] HeaderPaths = { nameof(TreeRecipe.Seed) };

        /// <summary>Every recipe field belongs to exactly one group here; anything missing lands in an "Other" foldout.</summary>
        public static readonly TabDef[] Tabs =
        {
            Tab("Shape",
                Group("Trunk", Of(nameof(TreeRecipe.Trunk), nameof(TrunkSettings.Height), nameof(TrunkSettings.BaseRadius), nameof(TrunkSettings.TipRadiusRatio),
                    nameof(TrunkSettings.RadialSides), nameof(TrunkSettings.HeightSegments), nameof(TrunkSettings.Bend), nameof(TrunkSettings.Lean),
                    nameof(TrunkSettings.Flare), nameof(TrunkSettings.TaperPower), nameof(TrunkSettings.Twist), nameof(TrunkSettings.Gnarl),
                    nameof(TrunkSettings.NoiseBend), nameof(TrunkSettings.NoiseFrequency))).With(
                    Group("Splits", Of(nameof(TreeRecipe.Trunk), nameof(TrunkSettings.Splits), nameof(TrunkSettings.SplitHeight),
                        nameof(TrunkSettings.SplitAngle), nameof(TrunkSettings.Braid))),
                    Group("Roots", Of(nameof(TreeRecipe.Trunk), nameof(TrunkSettings.RootLobes), nameof(TrunkSettings.RootLobeDepth),
                        nameof(TrunkSettings.RootLobeHeight), nameof(TrunkSettings.Roots), nameof(TrunkSettings.RootLength),
                        nameof(TrunkSettings.RootRadius), nameof(TrunkSettings.RootWander), nameof(TrunkSettings.RootCurl)))),
                Group("Branches", Of(nameof(TreeRecipe.Branches), nameof(BranchSettings.Levels), nameof(BranchSettings.Count), nameof(BranchSettings.StartEnd),
                    nameof(BranchSettings.PitchRange), nameof(BranchSettings.LengthRatio), nameof(BranchSettings.LengthProfile),
                    nameof(BranchSettings.RadiusRatio), nameof(BranchSettings.Segments), nameof(BranchSettings.MinSides),
                    nameof(BranchSettings.SubCount), nameof(BranchSettings.SubLengthRatio), nameof(BranchSettings.WhorlSize), nameof(BranchSettings.YawStep))).With(
                    Group("Wobble", Of(nameof(TreeRecipe.Branches), nameof(BranchSettings.Wander), nameof(BranchSettings.NoiseBend),
                        nameof(BranchSettings.NoiseFrequency), nameof(BranchSettings.LengthNoise), nameof(BranchSettings.Droop))),
                    Group("Character", Of(nameof(TreeRecipe.Branches), nameof(BranchSettings.Asymmetry), nameof(BranchSettings.Broken))))),

            Tab("Foliage",
                Group("Layout", Of(nameof(TreeRecipe.Leaves), nameof(LeafSettings.Layout), nameof(LeafSettings.CardsPerBranch), nameof(LeafSettings.ClusterRadius),
                    nameof(LeafSettings.TipBias), nameof(LeafSettings.Attach), nameof(LeafSettings.OnInnerBranches), nameof(LeafSettings.StrandLength),
                    nameof(LeafSettings.CardsPerBlob), "Coverage", "GroupVolume", nameof(LeafSettings.PlacementBias), nameof(LeafSettings.BlobCount), nameof(LeafSettings.BlobSize), nameof(LeafSettings.BlobOverlap), nameof(LeafSettings.BlobSizeSpread), nameof(LeafSettings.BlobFlatBelly), nameof(LeafSettings.BlobTaper),
                    nameof(LeafSettings.BranchFollow), "GroupQuads", "GroupDepth", "GroupJitter", "GroupTilt", "GroupTiltJitter", "Smoothness",
                    nameof(LeafSettings.NormalOffset), nameof(LeafSettings.BranchTipBias))
                    .Concat(new[] { nameof(TreeRecipe.Blobs), InsetToken }).ToArray()),
                Group("Cards", Of(nameof(TreeRecipe.Leaves), nameof(LeafSettings.CardSize), nameof(LeafSettings.Aspect), nameof(LeafSettings.Anchor))
                    .Concat(new[] { AnchorToken })
                    .Concat(Of(nameof(TreeRecipe.Leaves), nameof(LeafSettings.Roll), nameof(LeafSettings.RollJitter), nameof(LeafSettings.UpBias),
                        nameof(LeafSettings.Intersecting), nameof(LeafSettings.CardsPerCluster), nameof(LeafSettings.Billboard), nameof(LeafSettings.CrownOnTrunkTop)))
                    .ToArray()).With(
                    Group("Leaf Shapes", Of(nameof(TreeRecipe.LeafShapes), nameof(LeafShapeSettings.Columns), nameof(LeafShapeSettings.Rows),
                        nameof(LeafShapeSettings.WidthSegments), nameof(LeafShapeSettings.LengthSegments), nameof(LeafShapeSettings.Fold),
                        nameof(LeafShapeSettings.Curl), nameof(LeafShapeSettings.Twist), nameof(LeafShapeSettings.Jitter)))),
                Group("Crown & Clumps", Of(nameof(TreeRecipe.Leaves), nameof(LeafSettings.Envelope), nameof(LeafSettings.EnvelopeStrength), nameof(LeafSettings.Clumps),
                    nameof(LeafSettings.ClumpGather), nameof(LeafSettings.TipClumps), nameof(LeafSettings.ClumpHeight), nameof(LeafSettings.TintPerClump),
                    nameof(LeafSettings.Shell), nameof(LeafSettings.FlattenBottom))),
                Group("Shading", Of(nameof(TreeRecipe.Leaves), nameof(LeafSettings.NormalBlend), nameof(LeafSettings.Puffiness))
                    .Concat(Of(nameof(TreeRecipe.Renormalize), nameof(RenormalizeSettings.Enabled), nameof(RenormalizeSettings.Tightness),
                        nameof(RenormalizeSettings.Strength), nameof(RenormalizeSettings.AOStrength))).ToArray())),

            Tab("Look",
                Group("Leaves", Of(nameof(TreeRecipe.Look), nameof(LookSettings.Leaf), nameof(LookSettings.LeafTexture), nameof(LookSettings.SolidCards), nameof(LookSettings.SolidShape), nameof(LookSettings.ContinuousColor),
                    nameof(LookSettings.GrainAmount), nameof(LookSettings.CoreMesh), nameof(LookSettings.CoreAlign), nameof(LookSettings.CoreSegments), nameof(LookSettings.CoreMaterial), nameof(LookSettings.LeafBottom),
                    nameof(LookSettings.LeafTop), nameof(LookSettings.GradientBias), nameof(LookSettings.GradientJitter), nameof(LookSettings.ColorVariation))),
                Group("Light", Of(nameof(TreeRecipe.Look), nameof(LookSettings.Wrap), nameof(LookSettings.Translucency), nameof(LookSettings.TranslucencyColor),
                    nameof(LookSettings.TranslucencyFocus))),
                Group("Stylized", Of(nameof(TreeRecipe.Look), nameof(LookSettings.StylizedShading), nameof(LookSettings.ShadowColor), nameof(LookSettings.ShadowAmount),
                    nameof(LookSettings.ShadowSoftness), nameof(LookSettings.Bands), nameof(LookSettings.SkyTone), nameof(LookSettings.RimColor), nameof(LookSettings.RimStrength), nameof(LookSettings.RimPower),
                    nameof(LookSettings.HueShift), nameof(LookSettings.TreeVariation), nameof(LookSettings.PatchColor), nameof(LookSettings.PatchAmount),
                    nameof(LookSettings.PatchScale), nameof(LookSettings.FillTexture), nameof(LookSettings.FillScale), nameof(LookSettings.FillImpact))),
                Group("Bark", Of(nameof(TreeRecipe.Look), nameof(LookSettings.Bark), nameof(LookSettings.BarkTexture), nameof(LookSettings.BarkBottom),
                    nameof(LookSettings.BarkTop), nameof(LookSettings.BarkTiling))),
                Group("Moss", Of(nameof(TreeRecipe.Look), nameof(LookSettings.MossTexture), nameof(LookSettings.MossColor), nameof(LookSettings.MossAmount),
                    nameof(LookSettings.MossSharpness), nameof(LookSettings.MossTiling)))),

            Tab("Output",
                Group("Budget", Of(nameof(TreeRecipe.Budget), nameof(BudgetSettings.MaxCards), nameof(BudgetSettings.MaxTriangles),
                    nameof(BudgetSettings.FitCardsToTriangles), nameof(BudgetSettings.AddTrunkCollider))),
                Group("LODs", Of(nameof(TreeRecipe.Lods), nameof(LodSettings.Generate), nameof(LodSettings.Lod1Screen), nameof(LodSettings.Lod2Screen),
                    nameof(LodSettings.CullScreen), nameof(LodSettings.Lod1CardShare), nameof(LodSettings.Lod2CardShare), nameof(LodSettings.Lod2Intersecting))),
                Group("Type & Variants", nameof(TreeRecipe.LastPreset), nameof(TreeRecipe.TypeId), nameof(TreeRecipe.Variants), VariantsToken)),
        };

        // Foliage fields by layout; everything else under Leaves shows in every layout
        static readonly HashSet<string> BlobsOnly = new HashSet<string>
        {
            "Leaves.CardsPerBlob", "Leaves.Coverage", "Leaves.GroupVolume", "Leaves.PlacementBias", "Leaves.BlobCount", "Leaves.BlobSize", "Leaves.BlobOverlap", "Leaves.BlobSizeSpread", "Leaves.BlobFlatBelly", "Leaves.BlobTaper", "Leaves.BranchFollow", "Leaves.GroupQuads", "Leaves.GroupDepth", "Leaves.GroupJitter", "Leaves.GroupTilt", "Leaves.GroupTiltJitter", "Leaves.Smoothness",
            "Leaves.NormalOffset", "Leaves.BranchTipBias", nameof(TreeRecipe.Blobs), InsetToken,
        };
        static readonly HashSet<string> NotInBlobs = new HashSet<string>
        {
            "Leaves.CardsPerBranch", "Leaves.ClusterRadius", "Leaves.TipBias", "Leaves.Attach", "Leaves.OnInnerBranches", "Leaves.StrandLength",
            "Leaves.Shell", "Leaves.FlattenBottom", "Leaves.Puffiness",
        };
        static readonly HashSet<string> StylizedFields = new HashSet<string>
        {
            "Look.ShadowColor", "Look.ShadowAmount", "Look.ShadowSoftness", "Look.Bands", "Look.SkyTone", "Look.RimColor", "Look.RimStrength", "Look.RimPower", "Look.HueShift",
            "Look.TreeVariation", "Look.PatchColor", "Look.PatchAmount", "Look.PatchScale", "Look.FillTexture", "Look.FillScale", "Look.FillImpact",
        };
        static readonly HashSet<string> SplitFields = new HashSet<string> { "Trunk.SplitHeight", "Trunk.SplitAngle", "Trunk.Braid" };
        static readonly HashSet<string> SubBranchFields = new HashSet<string> { "Branches.SubCount", "Branches.SubLengthRatio" };

        static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "Leaves.CardsPerBlob", "Groups per blob" },
            { nameof(TreeRecipe.Blobs), "Canopy Blobs" },
            { nameof(TreeRecipe.LastPreset), "Origin Preset" },
        };

        int _tab = -1;
        readonly List<(string path, VisualElement element)> _fields = new List<(string, VisualElement)>();

        private static GroupDef Group(string title, params string[] paths) => new GroupDef { Title = title, Paths = paths };

        private static TabDef Tab(string name, params GroupDef[] groups) => new TabDef { Name = name, Groups = groups };

        private static string[] Of(string root, params string[] names) => names.Select(n => $"{root}.{n}").ToArray();

        /// <summary>Every property path the tables place, special '@' elements excluded.</summary>
        public static IEnumerable<string> AllPaths()
        {
            foreach (string p in HeaderPaths) yield return p;
            foreach (TabDef tab in Tabs)
                foreach (GroupDef g in tab.Groups)
                    foreach (string p in Flatten(g))
                        yield return p;
        }

        private static IEnumerable<string> Flatten(GroupDef g)
        {
            foreach (string p in g.Paths) if (p[0] != '@') yield return p;
            foreach (GroupDef s in g.Subs)
                foreach (string p in Flatten(s))
                    yield return p;
        }

        /// <summary>How many table entries place this serialized property (entry path equal to it or an ancestor of it).</summary>
        public static int CountOwners(string propertyPath) =>
            AllPaths().Count(p => propertyPath == p || propertyPath.StartsWith(p + "."));

        /// <summary>Paths of the properties the inspector must place: structs are walked into, arrays and plain values are one entry.</summary>
        public static List<string> LeafPaths(SerializedObject so)
        {
            var result = new List<string>();
            SerializedProperty it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = it.propertyType == SerializedPropertyType.Generic && !it.isArray;
                if (it.propertyPath == "m_Script" || enter) continue;
                result.Add(it.propertyPath);
            }
            return result;
        }

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            _fields.Clear();
            if (_tab < 0) _tab = Mathf.Clamp(SessionState.GetInt(TabKey, 0), 0, Tabs.Length - 1);

            var root = new VisualElement { style = { paddingLeft = 4, paddingRight = 4 } };

            var top = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            top.Add(new PropertyField(serializedObject.FindProperty(nameof(TreeRecipe.Seed))) { style = { flexGrow = 1 } });
            top.Add(new Button(RerollSeed) { text = "Reroll", tooltip = "Pick a new random seed for a different variation." });
            root.Add(top);

            var bar = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
            var pages = new VisualElement[Tabs.Length];
            var buttons = new Button[Tabs.Length];
            void Select(int index)
            {
                _tab = index;
                SessionState.SetInt(TabKey, index);
                for (int i = 0; i < pages.Length; i++)
                {
                    pages[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
                    buttons[i].style.unityFontStyleAndWeight = i == index ? FontStyle.Bold : FontStyle.Normal;
                    buttons[i].style.backgroundColor = i == index ? new StyleColor(new Color(0.3f, 0.45f, 0.65f, 0.6f)) : new StyleColor(StyleKeyword.Null);
                }
            }
            for (int i = 0; i < Tabs.Length; i++)
            {
                int index = i;
                buttons[i] = new Button(() => Select(index)) { text = Tabs[i].Name, style = { flexGrow = 1, marginLeft = 0, marginRight = 0 } };
                bar.Add(buttons[i]);
                pages[i] = new VisualElement();
                foreach (GroupDef g in Tabs[i].Groups) pages[i].Add(BuildFoldout(g, true));
            }
            root.Add(bar);
            foreach (VisualElement page in pages) root.Add(page);

            // properties no table entry places still show up, so a new field is never lost
            string[] placed = AllPaths().ToArray();
            string[] orphans = LeafPaths(serializedObject)
                .Where(p => !placed.Any(o => p == o || p.StartsWith(o + "."))).ToArray();
            if (orphans.Length > 0) root.Add(BuildFoldout(Group("Other", orphans), false));

            Select(_tab);
            ApplyVisibility();
            root.TrackPropertyValue(serializedObject.FindProperty($"{nameof(TreeRecipe.Leaves)}.{nameof(LeafSettings.Layout)}"), _ => ApplyVisibility());
            root.TrackPropertyValue(serializedObject.FindProperty($"{nameof(TreeRecipe.Look)}.{nameof(LookSettings.StylizedShading)}"), _ => ApplyVisibility());
            root.TrackPropertyValue(serializedObject.FindProperty($"{nameof(TreeRecipe.Branches)}.{nameof(BranchSettings.Levels)}"), _ => ApplyVisibility());
            root.TrackPropertyValue(serializedObject.FindProperty($"{nameof(TreeRecipe.Trunk)}.{nameof(TrunkSettings.Splits)}"), _ => ApplyVisibility());

            // inside the Foliage Forge window the recipe is already open, so the shortcut would do nothing there
            var open = new Button(() => FoliageForgeWindow.Open((TreeRecipe)target)) { text = "Open in Foliage Forge", style = { marginTop = 8 } };
            root.Add(open);
            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                for (VisualElement e = root.parent; e != null; e = e.parent)
                    if (e.name == FoliageForgeWindow.InspectorHostName)
                    {
                        open.style.display = DisplayStyle.None;
                        return;
                    }
            });
            return root;
        }

        private VisualElement VariantButtons()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(new Button(() => EditVariants("Create Variants", r => r.CreateVariants())) { text = "Create 3 Variants", tooltip = "Replace the variants with three new ones: random seeds, slightly different sizes and blob counts." });
            row.Add(new Button(() => EditVariants("Reroll Variant Seeds", r => r.RerollVariantSeeds())) { text = "Reroll Seeds", tooltip = "Give every variant a new random seed, keeping its size and blob count." });
            return row;
        }

        private void EditVariants(string undoName, System.Action<TreeRecipe> edit)
        {
            var recipe = (TreeRecipe)target;
            Undo.RecordObject(recipe, undoName);
            edit(recipe);
            EditorUtility.SetDirty(recipe);
            serializedObject.Update();
        }

        private void RerollSeed()
        {
            SerializedProperty seed = serializedObject.FindProperty(nameof(TreeRecipe.Seed));
            seed.intValue = Random.Range(1, 99999);
            serializedObject.ApplyModifiedProperties();
        }

        private Foldout BuildFoldout(GroupDef group, bool open)
        {
            var foldout = new Foldout { text = group.Title, value = open, style = { marginTop = 2 } };
            foldout.Q<Toggle>().style.unityFontStyleAndWeight = FontStyle.Bold;
            foreach (string path in group.Paths)
            {
                VisualElement element;
                if (path == AnchorToken) element = new AnchorPicker(serializedObject);
                else if (path == VariantsToken) element = VariantButtons();
                else if (path == InsetToken)
                {
                    // the inset is a convenience; the inspector works without it
                    element = target is TreeRecipe recipe ? BlobViewer.CreateInset(recipe) : null;
                    if (element == null) continue;
                }
                else
                {
                    SerializedProperty prop = serializedObject.FindProperty(path);
                    if (prop == null) continue;
                    element = Labels.TryGetValue(path, out string label) ? new PropertyField(prop, label) : new PropertyField(prop);
                }
                if (path == nameof(TreeRecipe.LastPreset)) element.SetEnabled(false); // information only
                foldout.Add(element);
                _fields.Add((path, element));
            }
            foreach (GroupDef sub in group.Subs) foldout.Add(BuildFoldout(sub, false));
            return foldout;
        }

        private void ApplyVisibility()
        {
            if (serializedObject.targetObject == null) return;
            var layout = (LeafLayout)serializedObject.FindProperty($"{nameof(TreeRecipe.Leaves)}.{nameof(LeafSettings.Layout)}").enumValueIndex;
            bool stylized = serializedObject.FindProperty($"{nameof(TreeRecipe.Look)}.{nameof(LookSettings.StylizedShading)}").boolValue;
            int levels = serializedObject.FindProperty($"{nameof(TreeRecipe.Branches)}.{nameof(BranchSettings.Levels)}").intValue;
            int splits = serializedObject.FindProperty($"{nameof(TreeRecipe.Trunk)}.{nameof(TrunkSettings.Splits)}").intValue;
            foreach ((string path, VisualElement element) in _fields)
                element.style.display = IsVisible(path, layout, stylized, levels, splits) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Whether a field applies to the current layout, stylized toggle, branch levels and trunk splits.</summary>
        public static bool IsVisible(string path, LeafLayout layout, bool stylized, int levels, int splits)
        {
            bool blobs = layout == LeafLayout.Blobs;
            if (BlobsOnly.Contains(path)) return blobs;
            if (blobs && (NotInBlobs.Contains(path) || path.StartsWith(nameof(TreeRecipe.Renormalize) + "."))) return false;
            if (path == "Leaves.StrandLength") return layout == LeafLayout.Hanging;
            if (StylizedFields.Contains(path)) return stylized;
            if (SplitFields.Contains(path)) return splits > 0;
            if (path.StartsWith(nameof(TreeRecipe.Branches) + "."))
            {
                if (path == "Branches.Levels") return true;
                if (levels <= 0) return false;
                if (SubBranchFields.Contains(path)) return levels >= 2;
            }
            return true;
        }
    }
}
