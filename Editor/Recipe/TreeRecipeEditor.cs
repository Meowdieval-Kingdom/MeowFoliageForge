using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Meowdieval.FoliageForge
{
    /// <summary>A titled foldout of recipe property paths; <see cref="Subs"/> nest inside it as foldouts.</summary>
    public sealed class GroupDef
    {
        /// <summary>Foldout title.</summary>
        public string Title;
        /// <summary>Serialized property paths shown in order. Paths starting with '@' are special elements, not properties.</summary>
        public string[] Paths;
        /// <summary>Paths shown only while the section's Advanced toggle is on (or while a search matches them).</summary>
        public string[] AdvPaths = new string[0];
        /// <summary>Nested foldouts shown after the paths.</summary>
        public GroupDef[] Subs = new GroupDef[0];
        /// <summary>True for an advanced section: it is gated by the Advanced toggle of the basic section before it (or of its parent).</summary>
        public bool Advanced;
        /// <summary>Short muted text shown in the collapsed header; null for none.</summary>
        public System.Func<TreeRecipe, string> Summary;

        /// <summary>Adds nested foldouts and returns this group.</summary>
        public GroupDef With(params GroupDef[] subs)
        {
            Subs = subs;
            return this;
        }

        /// <summary>Adds paths hidden behind this section's Advanced toggle and returns this group.</summary>
        public GroupDef WithAdvanced(params string[] paths)
        {
            AdvPaths = paths;
            return this;
        }

        /// <summary>Marks the whole section advanced and returns this group.</summary>
        public GroupDef MakeAdvanced()
        {
            Advanced = true;
            return this;
        }

        /// <summary>Sets the collapsed-header summary and returns this group.</summary>
        public GroupDef Sum(System.Func<TreeRecipe, string> summary)
        {
            Summary = summary;
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
        const string StateKey = "FoliageForge.Recipe";
        const string InsetToken = "@Inset";
        const string AnchorToken = "@Anchor";
        const string VariantsToken = "@Variants";
        const string Dot = " · ";

        /// <summary>Properties shown above the tabs, outside every group.</summary>
        public static readonly string[] HeaderPaths = { nameof(TreeRecipe.Seed) };

        /// <summary>Every recipe field belongs to exactly one group here; anything missing lands in an "Other" foldout.</summary>
        public static readonly TabDef[] Tabs =
        {
            Tab("Shape",
                Group("Trunk", Of(nameof(TreeRecipe.Trunk), nameof(TrunkSettings.Height), nameof(TrunkSettings.BaseRadius), nameof(TrunkSettings.TipRadiusRatio),
                    nameof(TrunkSettings.Bend), nameof(TrunkSettings.Lean), nameof(TrunkSettings.Flare)))
                    .Sum(r => $"Height {r.Trunk.Height:0.#} m{Dot}base radius {r.Trunk.BaseRadius:0.##} m"),
                Group("Trunk detail", Of(nameof(TreeRecipe.Trunk), nameof(TrunkSettings.RadialSides), nameof(TrunkSettings.HeightSegments),
                    nameof(TrunkSettings.TaperPower), nameof(TrunkSettings.Twist), nameof(TrunkSettings.Gnarl), nameof(TrunkSettings.NoiseBend),
                    nameof(TrunkSettings.NoiseFrequency))).MakeAdvanced(),
                Group("Splits and Roots", Of(nameof(TreeRecipe.Trunk), nameof(TrunkSettings.Splits), nameof(TrunkSettings.SplitHeight),
                    nameof(TrunkSettings.SplitAngle), nameof(TrunkSettings.Braid), nameof(TrunkSettings.RootLobes), nameof(TrunkSettings.RootLobeDepth),
                    nameof(TrunkSettings.RootLobeHeight), nameof(TrunkSettings.Roots), nameof(TrunkSettings.RootLength),
                    nameof(TrunkSettings.RootRadius), nameof(TrunkSettings.RootWander), nameof(TrunkSettings.RootCurl))).MakeAdvanced(),
                Group("Branches", Of(nameof(TreeRecipe.Branches), nameof(BranchSettings.Levels), nameof(BranchSettings.Count), nameof(BranchSettings.StartEnd),
                    nameof(BranchSettings.PitchRange), nameof(BranchSettings.LengthRatio), nameof(BranchSettings.LengthProfile),
                    nameof(BranchSettings.RadiusRatio)))
                    .Sum(r => r.Branches.Levels <= 0 ? "No branches" : $"{r.Branches.Count} branches{Dot}{r.Branches.Levels} levels"),
                Group("Branch detail", Of(nameof(TreeRecipe.Branches), nameof(BranchSettings.Segments), nameof(BranchSettings.MinSides),
                    nameof(BranchSettings.SubCount), nameof(BranchSettings.SubLengthRatio), nameof(BranchSettings.WhorlSize), nameof(BranchSettings.YawStep))).With(
                    Group("Wobble", Of(nameof(TreeRecipe.Branches), nameof(BranchSettings.Wander), nameof(BranchSettings.NoiseBend),
                        nameof(BranchSettings.NoiseFrequency), nameof(BranchSettings.LengthNoise), nameof(BranchSettings.Droop))),
                    Group("Character", Of(nameof(TreeRecipe.Branches), nameof(BranchSettings.Asymmetry), nameof(BranchSettings.Broken)))).MakeAdvanced()),

            Tab("Foliage",
                Group("Layout", Of(nameof(TreeRecipe.Leaves), nameof(LeafSettings.Layout), nameof(LeafSettings.CardsPerBranch), nameof(LeafSettings.ClusterRadius),
                    nameof(LeafSettings.TipBias), nameof(LeafSettings.Attach), nameof(LeafSettings.OnInnerBranches), nameof(LeafSettings.StrandLength),
                    nameof(LeafSettings.CardsPerBlob)))
                    .Sum(r => r.Leaves.Layout == LeafLayout.Blobs
                        ? $"{r.Leaves.Layout}{Dot}{r.Leaves.CardsPerBlob} groups per blob"
                        : $"{r.Leaves.Layout}{Dot}{r.Leaves.CardsPerBranch} cards per branch"),
                Group("Blobs", Cat(new[] { nameof(TreeRecipe.Blobs), InsetToken },
                    Of(nameof(TreeRecipe.Leaves), nameof(LeafSettings.BlobCount), nameof(LeafSettings.BlobSize), nameof(LeafSettings.BlobOverlap),
                        nameof(LeafSettings.BlobSizeSpread), nameof(LeafSettings.BlobFlatBelly), nameof(LeafSettings.BlobTaper), nameof(LeafSettings.BranchFollow))))
                    .Sum(r => $"{(r.Blobs != null ? r.Blobs.Count : 0)} blobs"),
                Group("Leaf groups", Of(nameof(TreeRecipe.Leaves), nameof(LeafSettings.Coverage), nameof(LeafSettings.GroupVolume), nameof(LeafSettings.GroupQuads),
                    nameof(LeafSettings.GroupDepth), nameof(LeafSettings.GroupJitter), nameof(LeafSettings.GroupTilt), nameof(LeafSettings.GroupTiltJitter),
                    nameof(LeafSettings.NormalOffset), nameof(LeafSettings.BranchTipBias), nameof(LeafSettings.PlacementBias), nameof(LeafSettings.Smoothness))).MakeAdvanced(),
                Group("Cards", Cat(Of(nameof(TreeRecipe.Leaves), nameof(LeafSettings.CardSize), nameof(LeafSettings.Aspect), nameof(LeafSettings.Anchor)),
                    new[] { AnchorToken },
                    Of(nameof(TreeRecipe.Leaves), nameof(LeafSettings.Roll), nameof(LeafSettings.RollJitter), nameof(LeafSettings.UpBias),
                        nameof(LeafSettings.Intersecting), nameof(LeafSettings.CardsPerCluster), nameof(LeafSettings.Billboard), nameof(LeafSettings.CrownOnTrunkTop)))).With(
                    Group("Leaf Shapes", Of(nameof(TreeRecipe.LeafShapes), nameof(LeafShapeSettings.Columns), nameof(LeafShapeSettings.Rows),
                        nameof(LeafShapeSettings.WidthSegments), nameof(LeafShapeSettings.LengthSegments), nameof(LeafShapeSettings.Fold),
                        nameof(LeafShapeSettings.Curl), nameof(LeafShapeSettings.Twist), nameof(LeafShapeSettings.Jitter))).MakeAdvanced())
                    .Sum(r => $"{r.Leaves.CardSize.x:0.##} x {r.Leaves.CardSize.y:0.##} m"),
                Group("Crown and clumps", Of(nameof(TreeRecipe.Leaves), nameof(LeafSettings.Envelope), nameof(LeafSettings.EnvelopeStrength), nameof(LeafSettings.Clumps),
                    nameof(LeafSettings.ClumpGather), nameof(LeafSettings.TipClumps), nameof(LeafSettings.ClumpHeight), nameof(LeafSettings.TintPerClump),
                    nameof(LeafSettings.Shell), nameof(LeafSettings.FlattenBottom)))
                    .Sum(r => $"{r.Leaves.Envelope} crown{Dot}{r.Leaves.Clumps} clumps"),
                Group("Shading", Of(nameof(TreeRecipe.Leaves), nameof(LeafSettings.NormalBlend), nameof(LeafSettings.Puffiness)))
                    .WithAdvanced(Of(nameof(TreeRecipe.Renormalize), nameof(RenormalizeSettings.Enabled), nameof(RenormalizeSettings.Tightness),
                        nameof(RenormalizeSettings.Strength), nameof(RenormalizeSettings.AOStrength)))
                    .Sum(r => r.Renormalize.Enabled ? "Renormalize on" : "Renormalize off")),

            Tab("Look",
                Group("Leaf color", Of(nameof(TreeRecipe.Look), nameof(LookSettings.LeafTexture), nameof(LookSettings.LeafBottom), nameof(LookSettings.LeafTop),
                    nameof(LookSettings.GradientBias), nameof(LookSettings.GradientJitter), nameof(LookSettings.ColorVariation), nameof(LookSettings.SolidCards),
                    nameof(LookSettings.SolidShape), nameof(LookSettings.ContinuousColor)))
                    .WithAdvanced(Of(nameof(TreeRecipe.Look), nameof(LookSettings.GrainAmount)))
                    .Sum(r => r.Look.SolidCards ? "Solid cards" : r.Look.LeafTexture != null ? r.Look.LeafTexture.name : "No leaf texture"),
                Group("Lighting", Of(nameof(TreeRecipe.Look), nameof(LookSettings.Wrap), nameof(LookSettings.Translucency), nameof(LookSettings.TranslucencyColor),
                    nameof(LookSettings.TranslucencyFocus)))
                    .Sum(r => $"Wrap {r.Look.Wrap:0.##}{Dot}translucency {r.Look.Translucency:0.##}"),
                Group("Stylized", Of(nameof(TreeRecipe.Look), nameof(LookSettings.StylizedShading), nameof(LookSettings.ShadowColor), nameof(LookSettings.ShadowAmount),
                    nameof(LookSettings.ShadowSoftness), nameof(LookSettings.Bands), nameof(LookSettings.SkyTone), nameof(LookSettings.RimColor),
                    nameof(LookSettings.RimStrength), nameof(LookSettings.RimPower), nameof(LookSettings.HueShift), nameof(LookSettings.TreeVariation)))
                    .Sum(r => r.Look.StylizedShading ? $"On{Dot}{r.Look.Bands} bands" : "Off"),
                Group("Patches and fill", Of(nameof(TreeRecipe.Look), nameof(LookSettings.PatchColor), nameof(LookSettings.PatchAmount),
                    nameof(LookSettings.PatchScale), nameof(LookSettings.FillTexture), nameof(LookSettings.FillScale), nameof(LookSettings.FillImpact))).MakeAdvanced(),
                Group("Core shell", Of(nameof(TreeRecipe.Look), nameof(LookSettings.CoreMesh), nameof(LookSettings.CoreAlign), nameof(LookSettings.CoreSegments),
                    nameof(LookSettings.CoreMaterial)))
                    .Sum(r => r.Look.CoreMesh ? "On" : "Off"),
                Group("Bark and moss", Of(nameof(TreeRecipe.Look), nameof(LookSettings.Bark), nameof(LookSettings.BarkTexture), nameof(LookSettings.BarkBottom),
                    nameof(LookSettings.BarkTop), nameof(LookSettings.BarkTiling)))
                    .WithAdvanced(Of(nameof(TreeRecipe.Look), nameof(LookSettings.MossTexture), nameof(LookSettings.MossColor), nameof(LookSettings.MossAmount),
                        nameof(LookSettings.MossSharpness), nameof(LookSettings.MossTiling)))
                    .Sum(r => r.Look.Bark != null ? r.Look.Bark.name : "No bark material"),
                Group("Leaf material", Of(nameof(TreeRecipe.Look), nameof(LookSettings.Leaf)))
                    .Sum(r => r.Look.Leaf != null ? r.Look.Leaf.name : "None")),

            Tab("Output",
                Group("Budget and collider", Of(nameof(TreeRecipe.Budget), nameof(BudgetSettings.MaxCards), nameof(BudgetSettings.MaxTriangles),
                    nameof(BudgetSettings.FitCardsToTriangles), nameof(BudgetSettings.AddTrunkCollider)))
                    .Sum(r => $"Max {r.Budget.MaxCards} cards{Dot}{r.Budget.MaxTriangles} tris"),
                Group("LODs", Of(nameof(TreeRecipe.Lods), nameof(LodSettings.Generate), nameof(LodSettings.Lod1Screen), nameof(LodSettings.Lod2Screen),
                    nameof(LodSettings.CullScreen), nameof(LodSettings.Lod1CardShare), nameof(LodSettings.Lod2CardShare), nameof(LodSettings.Lod2Intersecting)))
                    .Sum(r => r.Lods.Generate ? $"On{Dot}LOD1 at {r.Lods.Lod1Screen:0.##}" : "Off"),
                Group("Type and variants", nameof(TreeRecipe.LastPreset), nameof(TreeRecipe.TypeId), nameof(TreeRecipe.Variants), VariantsToken)
                    .Sum(r => $"{(string.IsNullOrEmpty(r.TypeId) ? "No type id" : r.TypeId)}{Dot}{(r.Variants != null ? r.Variants.Count : 0)} variants")),
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

        /// <summary>One placed element (property field or special element) with its search text and owning section.</summary>
        private sealed class Entry
        {
            public string Path;
            public VisualElement Element;
            public string Search;
            public Section Section;
            /// <summary>Section whose Advanced toggle reveals this entry; null for always-shown entries.</summary>
            public Section Gate;
        }

        /// <summary>A built foldout and its UI state.</summary>
        private sealed class Section
        {
            public GroupDef Def;
            public Foldout Foldout;
            public Section Parent;
            public Section Gate;
            public int Tab = -1;
            public string Key;
            public bool Open;
            public bool AdvOn;
            public bool HasAdv;
            public int Avail;
            public int Shown;
            public VisualElement ChipRow;
            public Label Summary;
        }

        int _tab = -1;
        string _query = string.Empty;
        readonly List<Entry> _entries = new List<Entry>();
        readonly List<Section> _sections = new List<Section>();
        VisualElement[] _pages;
        Button[] _buttons;
        string _typeKey = "default";

        private static GroupDef Group(string title, params string[] paths) => new GroupDef { Title = title, Paths = paths };

        private static TabDef Tab(string name, params GroupDef[] groups) => new TabDef { Name = name, Groups = groups };

        private static string[] Of(string root, params string[] names) => names.Select(n => $"{root}.{n}").ToArray();

        private static string[] Cat(params string[][] parts) => parts.SelectMany(p => p).ToArray();

        /// <summary>Every property path the tables place, special '@' elements excluded.</summary>
        public static IEnumerable<string> AllPaths()
        {
            foreach (string p in HeaderPaths) yield return p;
            foreach (TabDef tab in Tabs)
                foreach (GroupDef g in tab.Groups)
                    foreach (string p in Flatten(g))
                        yield return p;
        }

        /// <summary>Every property path placed in the table (header, basic and advanced fields, nested groups); for the coverage test.</summary>
        internal static IEnumerable<string> PlacedFieldPaths() => AllPaths();

        private static IEnumerable<string> Flatten(GroupDef g)
        {
            foreach (string p in g.Paths) if (p[0] != '@') yield return p;
            foreach (string p in g.AdvPaths) if (p[0] != '@') yield return p;
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
            _entries.Clear();
            _sections.Clear();
            _query = string.Empty;
            _typeKey = target is TreeRecipe tr && !string.IsNullOrEmpty(tr.TypeId) ? tr.TypeId : "default";
            if (_tab < 0) _tab = Mathf.Clamp(SessionState.GetInt(TabKey, 0), 0, Tabs.Length - 1);

            var root = new VisualElement { style = { paddingLeft = 4, paddingRight = 4 } };
            FoliageUi.Apply(root); // idempotent: only adds the sheet when this root does not already have it

            var top = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            top.Add(new PropertyField(serializedObject.FindProperty(nameof(TreeRecipe.Seed))) { style = { flexGrow = 1 } });
            var reroll = new Button(RerollSeed) { text = "Reroll", tooltip = "Pick a new random seed for a different variation." };
            reroll.AddToClassList("ff-button");
            top.Add(reroll);
            root.Add(top);

            var search = new ToolbarSearchField { tooltip = "Filter every tab by field name or tooltip." };
            search.style.marginBottom = 4;
            search.style.marginLeft = 0;
            search.style.marginRight = 0;
            search.style.width = Length.Percent(100);
            search.RegisterValueChangedCallback(e =>
            {
                _query = (e.newValue ?? string.Empty).Trim().ToLowerInvariant();
                Refresh();
            });
            root.Add(search);

            VisualElement bar = FoliageUi.WrapRow();
            _pages = new VisualElement[Tabs.Length];
            _buttons = new Button[Tabs.Length];
            for (int i = 0; i < Tabs.Length; i++)
            {
                int index = i;
                _buttons[i] = FoliageUi.ChipButton(Tabs[i].Name, false, _ => Select(index), null, false);
                bar.Add(_buttons[i]);
                _pages[i] = new VisualElement();
                Section lastBasic = null;
                foreach (GroupDef g in Tabs[i].Groups)
                {
                    Section owner = g.Advanced ? lastBasic : null;
                    Section s = BuildSection(g, null, owner, i, $"{StateKey}.{_typeKey}.{Tabs[i].Name}.{g.Title}", true);
                    _pages[i].Add(s.Foldout);
                    if (!g.Advanced || lastBasic == null) lastBasic = s;
                }
            }
            root.Add(bar);
            foreach (VisualElement page in _pages) root.Add(page);

            // properties no table entry places still show up, so a new field is never lost
            string[] placed = AllPaths().ToArray();
            string[] orphans = LeafPaths(serializedObject)
                .Where(p => !placed.Any(o => p == o || p.StartsWith(o + "."))).ToArray();
            if (orphans.Length > 0)
                root.Add(BuildSection(Group("Other", orphans), null, null, -1, $"{StateKey}.{_typeKey}.Other", false).Foldout);

            // an Advanced chip for every section that has advanced content
            foreach (Section s in _sections.Where(x => x.HasAdv).ToList())
            {
                Section section = s;
                section.ChipRow = FoliageUi.WrapRow();
                section.ChipRow.Add(FoliageUi.ChipButton("Advanced", section.AdvOn, on =>
                {
                    section.AdvOn = on;
                    SessionState.SetBool(section.Key + ".adv", on);
                    Refresh();
                }, "Show the advanced settings of this section."));
                section.Foldout.Insert(0, section.ChipRow);
            }

            Select(_tab);
            Refresh();
            root.TrackPropertyValue(serializedObject.FindProperty($"{nameof(TreeRecipe.Leaves)}.{nameof(LeafSettings.Layout)}"), _ => Refresh());
            root.TrackPropertyValue(serializedObject.FindProperty($"{nameof(TreeRecipe.Look)}.{nameof(LookSettings.StylizedShading)}"), _ => Refresh());
            root.TrackPropertyValue(serializedObject.FindProperty($"{nameof(TreeRecipe.Branches)}.{nameof(BranchSettings.Levels)}"), _ => Refresh());
            root.TrackPropertyValue(serializedObject.FindProperty($"{nameof(TreeRecipe.Trunk)}.{nameof(TrunkSettings.Splits)}"), _ => Refresh());
            root.TrackSerializedObjectValue(serializedObject, _ => RefreshSummaries());

            // inside the Foliage Forge window the recipe is already open, so the shortcut would do nothing there
            var open = new Button(() => FoliageForgeWindow.Open((TreeRecipe)target)) { text = "Open in Foliage Forge", style = { marginTop = 8 } };
            open.AddToClassList("ff-button");
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

        private void Select(int index)
        {
            _tab = index;
            SessionState.SetInt(TabKey, index);
            for (int i = 0; i < _pages.Length; i++)
            {
                _pages[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
                FoliageUi.SetChipOn(_buttons[i], i == index);
            }
        }

        private VisualElement VariantButtons()
        {
            var row = FoliageUi.WrapRow();
            row.Add(new Button(() => EditVariants("Create Variants", r => r.CreateVariants())) { text = "Create 3 Variants", tooltip = "Replace the variants with three new ones: random seeds, slightly different sizes and blob counts." });
            row.Add(new Button(() => EditVariants("Reroll Variant Seeds", r => r.RerollVariantSeeds())) { text = "Reroll Seeds", tooltip = "Give every variant a new random seed, keeping its size and blob count." });
            foreach (VisualElement b in row.Children()) b.AddToClassList("ff-button");
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

        /// <summary>Builds a group as a foldout section. gate = the section whose Advanced toggle reveals this whole group (null = always available).</summary>
        private Section BuildSection(GroupDef group, Section parent, Section gate, int tab, string key, bool openByDefault)
        {
            bool advancedSection = gate != null;
            if (advancedSection) gate.HasAdv = true;
            var s = new Section { Def = group, Parent = parent, Gate = gate, Tab = tab, Key = key };
            s.Open = SessionState.GetBool(key + ".open", openByDefault || advancedSection);
            s.AdvOn = SessionState.GetBool(key + ".adv", false);
            var foldout = new Foldout { text = group.Title, value = s.Open, style = { marginTop = 2 } };
            s.Foldout = foldout;
            _sections.Add(s);

            Toggle header = foldout.Q<Toggle>();
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            if (group.Summary != null)
            {
                s.Summary = new Label();
                s.Summary.AddToClassList("ff-muted");
                s.Summary.style.marginLeft = 8;
                s.Summary.style.unityFontStyleAndWeight = FontStyle.Normal;
                (header.Q(className: "unity-toggle__input") ?? header).Add(s.Summary);
            }
            foldout.RegisterValueChangedCallback(e =>
            {
                if (!ReferenceEquals(e.target, foldout)) return; // nested foldouts bubble their changes up
                if (_query.Length == 0)
                {
                    s.Open = e.newValue;
                    SessionState.SetBool(key + ".open", e.newValue);
                }
                UpdateSummary(s);
            });

            foreach (string path in group.Paths) AddEntry(s, path, advancedSection ? gate : null);
            foreach (string path in group.AdvPaths)
            {
                s.HasAdv = true;
                AddEntry(s, path, gate ?? s);
            }
            foreach (GroupDef sub in group.Subs)
            {
                Section child = BuildSection(sub, s, sub.Advanced ? (gate ?? s) : gate, tab, $"{key}.{sub.Title}", false);
                foldout.Add(child.Foldout);
            }
            return s;
        }

        private void AddEntry(Section s, string path, Section gate)
        {
            VisualElement element;
            string search;
            if (path == AnchorToken) { element = new AnchorPicker(serializedObject); search = "anchor picker pivot"; }
            else if (path == VariantsToken) { element = VariantButtons(); search = "create variants reroll seeds"; }
            else if (path == InsetToken)
            {
                // the inset is a convenience; the inspector works without it
                element = target is TreeRecipe recipe ? BlobViewer.CreateInset(recipe) : null;
                if (element == null) return;
                search = "blob viewer inset preview";
            }
            else
            {
                SerializedProperty prop = serializedObject.FindProperty(path);
                if (prop == null) return;
                bool relabel = Labels.TryGetValue(path, out string label);
                var field = relabel ? new PropertyField(prop, label) : new PropertyField(prop);
                if (!prop.isArray) WidenLabel(field);
                element = field;
                search = $"{(relabel ? label : prop.displayName)} {prop.tooltip}";
            }
            if (path == nameof(TreeRecipe.LastPreset)) element.SetEnabled(false); // information only
            s.Foldout.Add(element);
            _entries.Add(new Entry { Path = path, Element = element, Search = search.ToLowerInvariant(), Section = s, Gate = gate });
        }

        /// <summary>Gives the bound field's label a readable column (>= 110px, 12px text, wraps instead of truncating).</summary>
        private static void WidenLabel(PropertyField field)
        {
            bool done = false;
            field.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (done) return;
                Label l = field.Q<Label>(className: "unity-base-field__label");
                if (l == null) return; // not bound yet; try again on the next layout pass
                done = true;
                l.style.minWidth = 110;
                l.style.width = Length.Percent(40);
                l.style.flexShrink = 0;
                l.style.fontSize = 12;
                l.style.whiteSpace = WhiteSpace.Normal;
                l.style.overflow = Overflow.Visible;
                l.style.textOverflow = TextOverflow.Clip;
            });
        }

        private void UpdateSummary(Section s)
        {
            if (s.Summary == null) return;
            bool show = _query.Length == 0 && !s.Foldout.value && target is TreeRecipe recipe;
            s.Summary.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) s.Summary.text = s.Def.Summary((TreeRecipe)target);
        }

        private void RefreshSummaries()
        {
            if (serializedObject.targetObject == null) return;
            foreach (Section s in _sections) UpdateSummary(s);
        }

        /// <summary>Recomputes what shows: layout/stylized/levels/splits rules, the search filter, Advanced toggles, section state and tab counts.</summary>
        private void Refresh()
        {
            if (serializedObject.targetObject == null) return;
            var layout = (LeafLayout)serializedObject.FindProperty($"{nameof(TreeRecipe.Leaves)}.{nameof(LeafSettings.Layout)}").enumValueIndex;
            bool stylized = serializedObject.FindProperty($"{nameof(TreeRecipe.Look)}.{nameof(LookSettings.StylizedShading)}").boolValue;
            int levels = serializedObject.FindProperty($"{nameof(TreeRecipe.Branches)}.{nameof(BranchSettings.Levels)}").intValue;
            int splits = serializedObject.FindProperty($"{nameof(TreeRecipe.Trunk)}.{nameof(TrunkSettings.Splits)}").intValue;

            bool searching = _query.Length > 0;
            string[] terms = _query.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            var tabCounts = new int[Tabs.Length];
            foreach (Section s in _sections) { s.Avail = 0; s.Shown = 0; }

            foreach (Entry e in _entries)
            {
                bool rule = IsVisible(e.Path, layout, stylized, levels, splits);
                bool match = !searching || terms.All(t => e.Search.Contains(t));
                bool gated = !searching && e.Gate != null && !e.Gate.AdvOn;
                bool show = rule && match && !gated;
                e.Element.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (rule)
                    for (Section s = e.Section; s != null; s = s.Parent) s.Avail++;
                if (!show) continue;
                for (Section s = e.Section; s != null; s = s.Parent) s.Shown++;
                if (searching && e.Section.Tab >= 0) tabCounts[e.Section.Tab]++;
            }

            foreach (Section s in _sections)
            {
                bool visible;
                if (searching) visible = s.Shown > 0;
                else visible = s.Avail > 0 && (s.Gate == null || s.Gate.AdvOn);
                s.Foldout.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                s.Foldout.SetValueWithoutNotify(searching ? s.Shown > 0 : s.Open);
                if (s.ChipRow != null) s.ChipRow.style.display = searching ? DisplayStyle.None : DisplayStyle.Flex;
                UpdateSummary(s);
            }

            for (int i = 0; i < _buttons.Length; i++)
            {
                _buttons[i].text = searching ? $"{Tabs[i].Name} ({tabCounts[i]})" : Tabs[i].Name;
                _buttons[i].EnableInClassList("ff-muted", searching && tabCounts[i] == 0);
            }
            if (searching && tabCounts[_tab] == 0)
            {
                int first = System.Array.FindIndex(tabCounts, c => c > 0);
                if (first >= 0) Select(first);
            }
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
