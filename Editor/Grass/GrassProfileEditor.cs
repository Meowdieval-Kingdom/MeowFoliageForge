using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// Grass profile inspector (UI Toolkit), also shown in the Foliage Forge window's Grass tab. Tabs + search + per-section Advanced toggles.
    /// Every serialized property is a bound PropertyField; anything not placed on a tab lands in an "Other" section.
    /// </summary>
    [CustomEditor(typeof(GrassProfile))]
    public sealed class GrassProfileEditor : Editor
    {
        const string AddTypesKey = "FoliageForge.Grass.AddTypes";
        const string TabKey = "FoliageForge.Grass.Tab";
        const string SearchKey = "FoliageForge.Grass.Search";
        const string AdvKey = "FoliageForge.Grass.Adv.";
        const string CollapsedKey = "FoliageForge.Grass.Collapsed.";

        const int TabTypes = 0, TabFeatures = 1, TabAppearance = 2, TabInteraction = 3, TabDraw = 4;
        static readonly string[] TabNames = { "Types", "Features", "Appearance", "Interaction", "Draw and placement" };

        static readonly (string title, string[] fields)[] FeatureGroups =
        {
            ("Motion", new[] { "Wind", "WindTexture", "Gusts", "Trample", "TrampleRT", "PerspectiveCorrection" }),
            ("Colour", new[] { "Texture", "Gradient", "Variation", "Patches", "GroundBlend", "GroundMap", "TrailTint" }),
            ("Lighting", new[] { "StylizedShading", "Rim", "Translucency", "ReceiveShadows", "CastShadows", "AdditionalLights", "LightRamp" }),
            ("Performance", new[] { "DistanceFade", "DistanceLod", "AlphaToCoverage", "FrustumCulling", "MeshLod", "CpuThinning" }),
        };

        // top-level fields shown directly under their feature toggle on the Features tab
        static readonly (string feature, string field)[] FeatureDeps =
        {
            ("LightRamp", "LightRamp"),
            ("PerspectiveCorrection", "PerspectiveStrength"),
        };

        // fields of a section are space separated; adv fields only show with the section's Advanced toggle (or when searched)
        struct SecDef
        {
            public string Title, Feature, Fields, Adv;
            public SecDef(string title, string feature, string fields, string adv = "") { Title = title; Feature = feature; Fields = fields; Adv = adv; }
        }

        static readonly SecDef[] AppearanceDefs =
        {
            new SecDef("Colour", null, "ColorVariation RootDarkening", "GradientBias AlphaCutoff"),
            new SecDef("Patches", "Patches", "PatchColor PatchAmount PatchScale", "PatchBlur PatchOffset"),
            new SecDef("Shadow", null, "ShadowColor ShadowStrength", "ShadowSoftness ShadowDistance"),
            new SecDef("Light", null, "LightContribution", "NormalUp AdditionalLightStrength"),
            new SecDef("Rim", "Rim", "RimColor RimStrength", "RimPower"),
            new SecDef("Translucency", "Translucency", "TranslucencyColor TranslucencyStrength"),
            new SecDef("Ground", "GroundBlend", "GroundColor GroundBlendHeight GroundMapStrength"),
            new SecDef("Gusts", "Gusts", "GustColor GustIntensity GustSpeed", "GustFrequency"),
            new SecDef("Flutter", "Wind", "Flutter"),
        };

        static readonly SecDef[] InteractionDefs =
        {
            new SecDef("Trample", null, "TrampleStrength"),
            new SecDef("Trample map", "TrampleRT", "TrampleWindowSize TrampleRecoverySeconds", "TrampleMapResolution"),
            new SecDef("Trail tint", "TrailTint", "TrailTopColor TrailBottomColor TrailStrength"),
        };

        static readonly SecDef[] DrawDefs =
        {
            new SecDef("Shader and range", null, "Shader DrawDistance", "ChunkSize LodMid LodFar LodMinShare MaxInstances"),
            new SecDef("Placement", null, "SlopeLimit NormalAlign", "Seed"),
        };

        // per-type fields that live in the collapsed Type details section (Enabled, Mix and Density are on the Types rows)
        static readonly string[] TypeDetailFields =
        {
            "Name", "SourceId", "Texture", "Clustering", "ClusterScale", "Height", "Width", "Cards", "Segments", "Bend", "Spread",
            "Bottom", "Top", "Tip", "TipStart", "TipHueVariation", "WindWeight",
        };
        static readonly string[] TypeRowFields = { "Enabled", "Mix", "Density" };

        static string[] Split(string s) => string.IsNullOrEmpty(s) ? Array.Empty<string>() : s.Split(' ');

        /// <summary>Every serialized property path this inspector places (top-level names, "Features.X" and "Variants.X"). For the coverage test.</summary>
        internal static IEnumerable<string> PlacedFieldPaths()
        {
            yield return "Biome";
            yield return "Variants";
            yield return "Features";
            foreach ((string _, string[] fields) in FeatureGroups)
                foreach (string f in fields) yield return "Features." + f;
            foreach ((string _, string field) in FeatureDeps) yield return field;
            foreach (string f in TypeRowFields) yield return "Variants." + f;
            foreach (string f in TypeDetailFields) yield return "Variants." + f;
            foreach (SecDef[] defs in new[] { AppearanceDefs, InteractionDefs, DrawDefs })
                foreach (SecDef d in defs)
                {
                    foreach (string f in Split(d.Fields)) yield return f;
                    foreach (string f in Split(d.Adv)) yield return f;
                }
        }

        // ---------------------------------------------------------------- model

        sealed class Sec
        {
            public int Tab;
            public string Title, Feature, Key;
            public VisualElement Root, Body, Header;
            public Label Label;
            public Button AdvChip, CollapseChip;
            public bool Collapsible;
            public Func<string> Suffix;
            public readonly List<Entry> Entries = new List<Entry>();
        }

        sealed class Entry
        {
            public VisualElement El;
            public string Text;
            public int Tab;
            public Sec Sec;
            public bool Adv, NoCount, Visible;
            public Func<bool> Gate, Cond;
            public Foldout Fold;
        }

        sealed class TypeRow
        {
            public Label Name;
            public VisualElement Body;
            public Entry Entry;
            public Foldout Fold;
            public Sec Sec;
        }

        VisualElement _root;
        readonly VisualElement[] _pages = new VisualElement[TabNames.Length];
        readonly Button[] _tabButtons = new Button[TabNames.Length];
        readonly List<Entry> _entries = new List<Entry>();
        readonly List<Sec> _sections = new List<Sec>();
        readonly List<Foldout> _folds = new List<Foldout>();
        readonly List<TypeRow> _typeRows = new List<TypeRow>();
        readonly List<Button> _biomeChips = new List<Button>();
        readonly Dictionary<Foldout, bool> _foldAny = new Dictionary<Foldout, bool>();
        ToolbarSearchField _searchField;
        VisualElement _triBox;
        string _triKey;
        SerializedProperty _features;
        string _search = "";
        int _tab;
        int _builtTypeCount = -1;
        int[] _counts = new int[TabNames.Length];

        GrassProfile Profile => (GrassProfile)target;

        bool Feature(string name)
        {
            SerializedProperty p = _features?.FindPropertyRelative(name);
            return p != null && p.boolValue;
        }

        // ---------------------------------------------------------------- build

        public override VisualElement CreateInspectorGUI()
        {
            _root = new VisualElement();
            FoliageUi.Apply(_root);
            _features = serializedObject.FindProperty("Features");
            _search = SessionState.GetString(SearchKey, "");
            _tab = Mathf.Clamp(SessionState.GetInt(TabKey, TabTypes), 0, TabNames.Length - 1);

            _searchField = new ToolbarSearchField { tooltip = "Filter settings by name or tooltip across all tabs." };
            _searchField.style.marginBottom = 4;
            _searchField.style.width = Length.Percent(100);
            _searchField.SetValueWithoutNotify(_search);
            _searchField.RegisterValueChangedCallback(evt => OnSearchChanged(evt.newValue ?? ""));
            _root.Add(_searchField);

            VisualElement tabs = FoliageUi.WrapRow();
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                _tabButtons[i] = FoliageUi.ChipButton(TabNames[i], i == _tab, _ => SetTab(index), null, false);
                tabs.Add(_tabButtons[i]);
            }
            _root.Add(tabs);

            for (int i = 0; i < _pages.Length; i++)
            {
                _pages[i] = new VisualElement();
                _root.Add(_pages[i]);
            }

            BuildTypes();
            BuildFeatures();
            BuildDefs(TabAppearance, AppearanceDefs);
            BuildDefs(TabInteraction, InteractionDefs);
            _pages[TabDraw].Add(HintLabel("Rarely changed: draw range, LOD and placement. Every section has its own Advanced toggle."));
            BuildDefs(TabDraw, DrawDefs);
            BuildOther();

            ApplyFilter();
            _root.schedule.Execute(Refresh).Every(200);
            return _root;
        }

        static Label HintLabel(string text)
        {
            var l = new Label(text);
            l.AddToClassList("ff-hint");
            return l;
        }

        Sec NewSection(int tab, string title, string feature, bool collapsible = false)
        {
            var s = new Sec { Tab = tab, Title = title, Feature = feature, Collapsible = collapsible, Key = TabNames[tab] + "." + title };
            if (feature != null) s.Suffix = () => Feature(feature) ? "" : " (off in Features)";

            s.Root = new VisualElement();
            s.Root.AddToClassList("ff-panel");
            s.Header = new VisualElement();
            s.Header.style.flexDirection = FlexDirection.Row;
            s.Header.style.alignItems = Align.Center;
            s.Label = FoliageUi.GroupLabel(title);
            s.Label.style.flexGrow = 1;
            s.Label.style.marginLeft = 0;
            s.Header.Add(s.Label);
            s.Body = new VisualElement();
            s.Root.Add(s.Header);
            s.Root.Add(s.Body);

            if (collapsible)
            {
                bool collapsed = SessionState.GetBool(CollapsedKey + s.Key, true);
                s.CollapseChip = FoliageUi.ChipButton(collapsed ? "Show" : "Hide", !collapsed, null, "Show or hide this section.", false);
                s.CollapseChip.clicked += () =>
                {
                    bool now = !SessionState.GetBool(CollapsedKey + s.Key, true);
                    SessionState.SetBool(CollapsedKey + s.Key, now);
                    ApplyFilter();
                };
                StyleSmallChip(s.CollapseChip);
                s.Header.Add(s.CollapseChip);
            }
            _pages[tab].Add(s.Root);
            _sections.Add(s);
            return s;
        }

        static void StyleSmallChip(Button b)
        {
            b.style.height = 20;
            b.style.minWidth = 0;
            b.style.marginRight = 0;
            b.style.marginLeft = 4;
            b.style.fontSize = 11;
        }

        bool AdvOn(Sec s) => SessionState.GetBool(AdvKey + s.Key, false);

        static string Norm(string text, Sec s) => (text + " " + s.Title).ToLowerInvariant();

        Entry Add(Sec s, VisualElement el, string text, bool adv = false, Func<bool> gate = null, Func<bool> cond = null,
            Foldout fold = null, VisualElement parent = null, bool noCount = false)
        {
            (parent ?? s.Body).Add(el);
            var e = new Entry
            {
                El = el, Text = Norm(text, s), Tab = s.Tab, Sec = s, Adv = adv, Gate = gate, Cond = cond, Fold = fold, NoCount = noCount,
            };
            _entries.Add(e);
            s.Entries.Add(e);
            if (adv && s.AdvChip == null)
            {
                Sec sec = s;
                s.AdvChip = FoliageUi.ChipButton("Advanced", AdvOn(sec), on =>
                {
                    SessionState.SetBool(AdvKey + sec.Key, on);
                    ApplyFilter();
                }, "Show the rarely used settings of this section.");
                StyleSmallChip(s.AdvChip);
                s.Header.Add(s.AdvChip);
            }
            return e;
        }

        static void FixLabel(PropertyField f)
        {
            Label l = f.Q<Label>(className: "unity-base-field__label");
            if (l == null) return;
            l.style.minWidth = 110;
            l.style.fontSize = 12;
        }

        static PropertyField MakeField(SerializedProperty p, string label = null, string tooltip = null)
        {
            var f = new PropertyField(p, label ?? p.displayName);
            if (!string.IsNullOrEmpty(tooltip)) f.tooltip = tooltip;
            f.RegisterCallback<GeometryChangedEvent>(_ => FixLabel(f));
            return f;
        }

        Entry AddProp(Sec s, SerializedProperty p, bool adv = false, Func<bool> gate = null, VisualElement parent = null,
            Foldout fold = null, int indent = 0)
        {
            PropertyField f = MakeField(p);
            if (indent > 0) f.style.marginLeft = indent;
            return Add(s, f, p.displayName + " " + p.name + " " + p.tooltip, adv, gate, null, fold, parent);
        }

        void SetTab(int i)
        {
            _tab = i;
            SessionState.SetInt(TabKey, i);
            ApplyFilter();
        }

        void OnSearchChanged(string value)
        {
            _search = value.Trim();
            SessionState.SetString(SearchKey, _search);
            ApplyFilter();
            if (_search.Length == 0) return;
            if (_counts[_tab] == 0)
                for (int i = 0; i < _counts.Length; i++)
                    if (_counts[i] > 0) { SetTab(i); break; }
            foreach (KeyValuePair<Foldout, bool> kv in _foldAny)
                if (kv.Value) kv.Key.value = true;
        }

        // ---------------------------------------------------------------- Types tab

        void BuildTypes()
        {
            VisualElement page = _pages[TabTypes];
            page.Clear();
            _entries.RemoveAll(e => e.Tab == TabTypes);
            _sections.RemoveAll(s => s.Tab == TabTypes);
            foreach (Foldout f in _folds) _foldAny.Remove(f);
            _folds.Clear();
            _typeRows.Clear();
            _biomeChips.Clear();
            _triKey = null;

            SerializedProperty types = serializedObject.FindProperty("Variants");
            _builtTypeCount = types.arraySize;

            // Look: biome + quality presets
            Sec look = NewSection(TabTypes, "Look", null);
            var biomeBox = new VisualElement();
            biomeBox.Add(HintLabel("Biome: pick a look. Off replaces the grass types, colours and wind feel. With Add types only it just adds the biome's grass types and changes nothing else."));
            VisualElement biomeRow = FoliageUi.WrapRow();
            var biomes = (GrassBiome[])Enum.GetValues(typeof(GrassBiome));
            foreach (GrassBiome b in biomes)
            {
                GrassBiome biome = b;
                Button chip = FoliageUi.ChipButton(biome.ToString(), Profile.Biome == biome, _ => ApplyBiome(biome), "Apply the " + biome + " biome.", false);
                _biomeChips.Add(chip);
                biomeRow.Add(chip);
            }
            biomeBox.Add(biomeRow);
            VisualElement addRow = FoliageUi.WrapRow();
            addRow.Add(FoliageUi.ChipButton("Add types only", EditorPrefs.GetBool(AddTypesKey, false), on => EditorPrefs.SetBool(AddTypesKey, on),
                "Off: a biome replaces the grass types, colours and wind feel. On: it only adds its grass types to the list and changes nothing else."));
            biomeBox.Add(addRow);
            Add(look, biomeBox, "biome preset look replace add types only meadow forest");

            var qualityBox = new VisualElement();
            qualityBox.Add(HintLabel("Quality tier: density, draw distance and the costly features in one click."));
            VisualElement qualityRow = FoliageUi.WrapRow();
            foreach (GrassProfile.Quality q in (GrassProfile.Quality[])Enum.GetValues(typeof(GrassProfile.Quality)))
            {
                GrassProfile.Quality quality = q;
                qualityRow.Add(FoliageUi.ChipButton(quality.ToString(), false, _ => ApplyQuality(quality), "Apply the " + quality + " quality tier.", false));
            }
            qualityBox.Add(qualityRow);
            Add(look, qualityBox, "quality tier lush balanced light density draw distance");

            // Grass types: Enabled, Mix, Density per type
            Sec list = NewSection(TabTypes, "Grass types", null);
            var empty = new HelpBox("No grass types. Pick a Biome above, or press Add type.", HelpBoxMessageType.Warning);
            Add(list, empty, "no grass types add type", cond: () => _builtTypeCount == 0, noCount: true);
            for (int i = 0; i < types.arraySize; i++)
            {
                SerializedProperty v = types.GetArrayElementAtIndex(i);
                var row = new VisualElement();
                row.style.marginBottom = 4;
                var head = new VisualElement();
                head.style.flexDirection = FlexDirection.Row;
                head.style.alignItems = Align.Center;
                PropertyField enabled = MakeField(v.FindPropertyRelative("Enabled"), "", "Grow this type. Off keeps its settings but scatters none.");
                head.Add(enabled);
                var name = new Label(v.FindPropertyRelative("Name").stringValue);
                name.style.unityFontStyleAndWeight = FontStyle.Bold;
                name.style.fontSize = 12;
                head.Add(name);
                row.Add(head);
                var body = new VisualElement();
                PropertyField mix = MakeField(v.FindPropertyRelative("Mix"), "Mix %", "Share of this type's Density that grows where types combine. 100 = full density.");
                PropertyField density = MakeField(v.FindPropertyRelative("Density"), "Density", "Clumps per square metre.");
                body.Add(mix);
                body.Add(density);
                row.Add(body);
                Entry entry = Add(list, row, name.text + " enabled mix density clumps per square metre");
                _typeRows.Add(new TypeRow { Name = name, Body = body, Entry = entry, Sec = list });
            }
            var addButton = new Button(AddType) { text = "Add type", tooltip = "Append a new grass type." };
            addButton.AddToClassList("ff-button");
            addButton.style.alignSelf = Align.FlexStart;
            Add(list, addButton, "add type new grass");

            _triBox = new VisualElement();
            Add(list, _triBox, "triangles per clump lod estimate", cond: () => _triBox.childCount > 0, noCount: true);

            // Type details: everything else per type, collapsed
            Sec details = NewSection(TabTypes, "Type details", null, true);
            var placed = new HashSet<string>(TypeRowFields);
            foreach (string f in TypeDetailFields) placed.Add(f);
            for (int i = 0; i < types.arraySize; i++)
            {
                int index = i;
                SerializedProperty v = types.GetArrayElementAtIndex(i);
                var fold = new Foldout { text = v.FindPropertyRelative("Name").stringValue, value = false };
                details.Body.Add(fold);
                _folds.Add(fold);
                _typeRows[i].Fold = fold;

                var buttons = FoliageUi.WrapRow();
                buttons.Add(SmallButton("Move up", () => MoveType(index, index - 1), index > 0));
                buttons.Add(SmallButton("Move down", () => MoveType(index, index + 1), index < types.arraySize - 1));
                buttons.Add(SmallButton("Remove", () => RemoveType(index), true));
                Add(details, buttons, "move up down remove delete type reorder", fold: fold, parent: fold, noCount: true);

                foreach (string f in TypeDetailFields)
                {
                    SerializedProperty p = v.FindPropertyRelative(f);
                    if (p != null) AddProp(details, p, fold: fold, parent: fold);
                }
                foreach (string f in UnplacedChildren(v, placed))
                    AddProp(details, v.FindPropertyRelative(f), fold: fold, parent: fold);
            }
            if (types.arraySize == 0) details.Body.Add(HintLabel("No types yet."));

            page.Bind(serializedObject);
        }

        static Button SmallButton(string text, Action onClick, bool enabled)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("ff-button");
            b.SetEnabled(enabled);
            return b;
        }

        // child property names of parent that are not in `placed` (so a field added later is never hidden)
        static List<string> UnplacedChildren(SerializedProperty parent, HashSet<string> placed)
        {
            var result = new List<string>();
            SerializedProperty it = parent.Copy();
            SerializedProperty end = parent.GetEndProperty();
            if (!it.NextVisible(true)) return result;
            do
            {
                if (SerializedProperty.EqualContents(it, end)) break;
                if (!placed.Contains(it.name)) result.Add(it.name);
            }
            while (it.NextVisible(false));
            return result;
        }

        void ApplyBiome(GrassBiome biome)
        {
            GrassProfile p = Profile;
            bool add = EditorPrefs.GetBool(AddTypesKey, false);
            Undo.RecordObject(p, "Grass Biome");
            GrassBiomes.Apply(p, biome, add ? GrassApplyMode.AddTypes : GrassApplyMode.Replace);
            EditorUtility.SetDirty(p);
            serializedObject.Update();
            _root.schedule.Execute(BuildTypes);
        }

        void ApplyQuality(GrassProfile.Quality q)
        {
            GrassProfile p = Profile;
            Undo.RecordObject(p, "Grass Quality");
            p.ApplyQuality(q);
            EditorUtility.SetDirty(p);
            serializedObject.Update();
        }

        void AddType()
        {
            serializedObject.Update();
            SerializedProperty types = serializedObject.FindProperty("Variants");
            int n = types.arraySize;
            types.InsertArrayElementAtIndex(n);
            SerializedProperty e = types.GetArrayElementAtIndex(n);
            e.FindPropertyRelative("Name").stringValue = "Grass";
            e.FindPropertyRelative("SourceId").stringValue = "";
            serializedObject.ApplyModifiedProperties();
            _root.schedule.Execute(BuildTypes);
        }

        void RemoveType(int i)
        {
            serializedObject.Update();
            serializedObject.FindProperty("Variants").DeleteArrayElementAtIndex(i);
            serializedObject.ApplyModifiedProperties();
            _root.schedule.Execute(BuildTypes);
        }

        void MoveType(int from, int to)
        {
            serializedObject.Update();
            SerializedProperty types = serializedObject.FindProperty("Variants");
            if (to < 0 || to >= types.arraySize) return;
            types.MoveArrayElement(from, to);
            serializedObject.ApplyModifiedProperties();
            _root.schedule.Execute(BuildTypes);
        }

        // ---------------------------------------------------------------- Features tab

        void BuildFeatures()
        {
            GrassProfile profile = Profile;
            var placed = new HashSet<string>();
            foreach ((string title, string[] fields) in FeatureGroups)
            {
                string[] group = fields;
                Sec s = NewSection(TabFeatures, title, null);
                s.Suffix = () => " " + CountOn(group) + "/" + group.Length;
                foreach (string f in fields)
                {
                    string feature = f;
                    placed.Add(f);
                    SerializedProperty fp = _features.FindPropertyRelative(f);
                    AddProp(s, fp);
                    foreach ((string dep, string field) in FeatureDeps)
                        if (dep == feature)
                            AddProp(s, serializedObject.FindProperty(field), gate: () => Feature(feature), indent: 18);
                    if (f == "LightRamp")
                        Add(s, new HelpBox("Light Ramp is on but no ramp texture is assigned; it stays off until one is.", HelpBoxMessageType.Info),
                            "light ramp texture assigned", cond: () => Feature("LightRamp") && profile.LightRamp == null, noCount: true);
                    if (f == "TrailTint")
                        Add(s, new HelpBox("Trail Tint reads the trample map; switch on Trample Map too.", HelpBoxMessageType.Info),
                            "trail tint trample map", cond: () => Feature("TrailTint") && !Feature("TrampleRT"), noCount: true);
                }
            }

            List<string> extra = UnplacedChildren(_features, placed);
            if (extra.Count > 0)
            {
                Sec other = NewSection(TabFeatures, "Other features", null);
                foreach (string f in extra) AddProp(other, _features.FindPropertyRelative(f));
            }
        }

        int CountOn(string[] fields)
        {
            int on = 0;
            foreach (string f in fields) if (Feature(f)) on++;
            return on;
        }

        // ---------------------------------------------------------------- Appearance / Interaction / Draw

        void BuildDefs(int tab, SecDef[] defs)
        {
            foreach (SecDef d in defs)
            {
                Sec s = NewSection(tab, d.Title, d.Feature);
                foreach (string f in Split(d.Fields)) AddProp(s, serializedObject.FindProperty(f));
                foreach (string f in Split(d.Adv)) AddProp(s, serializedObject.FindProperty(f), adv: true);
            }
        }

        // fallback: any visible serialized property nobody placed
        void BuildOther()
        {
            var top = new HashSet<string> { "m_Script" };
            foreach (string path in PlacedFieldPaths())
                if (path.IndexOf('.') < 0) top.Add(path);
            Sec other = null;
            SerializedProperty it = serializedObject.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
            {
                if (top.Contains(it.name)) continue;
                if (other == null) other = NewSection(TabDraw, "Other", null);
                AddProp(other, serializedObject.FindProperty(it.name));
            }
        }

        // ---------------------------------------------------------------- filter + refresh

        static void SetShown(VisualElement e, bool shown) => e.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

        void ApplyFilter()
        {
            string[] terms = _search.Length == 0 ? Array.Empty<string>() : _search.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool searching = terms.Length > 0;
            _counts = new int[TabNames.Length];
            _foldAny.Clear();
            foreach (Foldout f in _folds) _foldAny[f] = false;

            foreach (Entry e in _entries)
            {
                bool match = true;
                foreach (string t in terms)
                    if (!e.Text.Contains(t)) { match = false; break; }
                bool cond = e.Cond == null || e.Cond();
                if (searching && match && cond && !e.NoCount) _counts[e.Tab]++;
                bool adv = !e.Adv || (searching && match) || AdvOn(e.Sec);
                bool gate = searching || e.Gate == null || e.Gate();
                e.Visible = match && cond && adv && gate && e.Tab == _tab;
                SetShown(e.El, e.Visible);
                if (e.Visible && e.Fold != null) _foldAny[e.Fold] = true;
            }
            foreach (KeyValuePair<Foldout, bool> kv in _foldAny) SetShown(kv.Key, kv.Value);

            foreach (Sec s in _sections)
            {
                bool any = false;
                foreach (Entry e in s.Entries) if (e.Visible) { any = true; break; }
                SetShown(s.Root, any);
                bool collapsed = s.Collapsible && SessionState.GetBool(CollapsedKey + s.Key, true);
                SetShown(s.Body, !collapsed || searching);
                if (s.CollapseChip != null)
                {
                    s.CollapseChip.text = collapsed ? "Show" : "Hide";
                    FoliageUi.SetChipOn(s.CollapseChip, !collapsed);
                }
                if (s.AdvChip != null) FoliageUi.SetChipOn(s.AdvChip, AdvOn(s));
            }

            for (int i = 0; i < _pages.Length; i++)
            {
                SetShown(_pages[i], i == _tab);
                _tabButtons[i].text = searching ? TabNames[i] + " (" + _counts[i] + ")" : TabNames[i];
                FoliageUi.SetChipOn(_tabButtons[i], i == _tab);
            }
        }

        void Refresh()
        {
            if (target == null || _root == null) return;
            serializedObject.UpdateIfRequiredOrScript();
            GrassProfile p = Profile;
            int count = serializedObject.FindProperty("Variants").arraySize;
            if (count != _builtTypeCount)
            {
                BuildTypes();
                return;
            }

            GrassVariant[] variants = p.Variants;
            for (int i = 0; i < _typeRows.Count && variants != null && i < variants.Length; i++)
            {
                TypeRow row = _typeRows[i];
                string n = variants[i].Name ?? "";
                if (row.Name.text != n)
                {
                    row.Name.text = n;
                    row.Entry.Text = Norm(n + " enabled mix density clumps per square metre", row.Entry.Sec);
                    if (row.Fold != null) row.Fold.text = n;
                }
                row.Body.SetEnabled(variants[i].Enabled);
            }

            var biomes = (GrassBiome[])Enum.GetValues(typeof(GrassBiome));
            for (int i = 0; i < _biomeChips.Count && i < biomes.Length; i++)
                FoliageUi.SetChipOn(_biomeChips[i], p.Biome == biomes[i]);

            RefreshTriangles(p);
            foreach (Sec s in _sections)
            {
                string suffix = s.Suffix != null ? s.Suffix() : "";
                string text = s.Title + suffix;
                if (s.Label.text != text) s.Label.text = text;
            }
            ApplyFilter();
        }

        // triangles per clump at each mesh LOD, one chip per enabled type
        void RefreshTriangles(GrassProfile p)
        {
            string key = TriangleKey(p);
            if (key == _triKey) return;
            _triKey = key;
            _triBox.Clear();
            if (key.Length == 0) return;
            _triBox.Add(HintLabel("Triangles per clump (LOD 0 / 1 / 2)"));
            VisualElement row = FoliageUi.WrapRow();
            foreach (GrassVariant gv in p.Variants)
            {
                if (!gv.Enabled) continue;
                row.Add(FoliageUi.StatChip(gv.Name, GrassMeshes.TriangleCount(gv, 0) + " / " + GrassMeshes.TriangleCount(gv, 1) + " / " + GrassMeshes.TriangleCount(gv, 2)));
            }
            _triBox.Add(row);
        }

        static string TriangleKey(GrassProfile p)
        {
            string text = TriangleText(p);
            return text ?? "";
        }

        /// <summary>Triangles per clump per enabled type as plain text (shared by the Grass Field inspector's HelpBox); null when there is nothing to show.</summary>
        internal static string TriangleText(GrassProfile profile)
        {
            if (profile == null || profile.Variants == null || profile.Variants.Length == 0) return null;
            var sb = new System.Text.StringBuilder("Triangles per clump (LOD 0 / 1 / 2)");
            foreach (GrassVariant gv in profile.Variants)
            {
                if (!gv.Enabled) continue;
                sb.Append($"\n{gv.Name}: {GrassMeshes.TriangleCount(gv, 0)} / {GrassMeshes.TriangleCount(gv, 1)} / {GrassMeshes.TriangleCount(gv, 2)}");
            }
            return sb.ToString();
        }
    }
}
