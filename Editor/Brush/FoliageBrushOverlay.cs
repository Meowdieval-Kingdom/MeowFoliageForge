using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Meowdieval.FoliageForge
{
    /// <summary>Scene-view panel of the Foliage Brush: mode, target, brush settings, thumbnail palette, ground mask and live hints.</summary>
    [Overlay(typeof(SceneView), Id, "Foliage Brush", false)]
    public sealed class FoliageBrushOverlay : Overlay
    {
        /// <summary>Overlay id.</summary>
        public const string Id = "meowdieval-foliage-brush";

        const string TileSizeKey = "FoliageForge.BrushTileSize";
        // thumbnail edge in px for the Small / Medium / Large tile chips
        static readonly int[] TileThumbs = { 40, 56, 80 };
        static readonly string[] TileNames = { "Small", "Medium", "Large" };
        static int _tileSize = -1;
        readonly Button[] _tileChips = new Button[3];

        static int TileSize
        {
            get
            {
                if (_tileSize < 0) _tileSize = Mathf.Clamp(EditorPrefs.GetInt(TileSizeKey, 1), 0, TileThumbs.Length - 1);
                return _tileSize;
            }
            set
            {
                _tileSize = Mathf.Clamp(value, 0, TileThumbs.Length - 1);
                EditorPrefs.SetInt(TileSizeKey, _tileSize);
            }
        }

        Label _target, _stats, _paletteMix, _grassMix;
        Button _select, _create, _paint, _erase, _off, _grassTarget, _propsTarget, _addLayer;
        Slider _radius;
        VisualElement _grassDensity, _props, _palette, _grassTypes, _rotation, _clusterOptions, _edgeOptions;
        GrassProfile _typesFor;
        string _typesKey;
        double _pumpUntil;
        readonly List<(Toggle on, Slider mix)> _typeRows = new List<(Toggle, Slider)>();
        LayerMaskField _ground;
        HelpBox _status;
        FoliageForgeManager _paletteFor;
        int _otherCount;
        readonly HashSet<GameObject> _baked = new HashSet<GameObject>();
        readonly List<(VisualElement tile, GameObject prefab, IntegerField mix)> _tiles = new List<(VisualElement, GameObject, IntegerField)>();
        readonly List<(Image image, GameObject prefab)> _pending = new List<(Image, GameObject)>();

        /// <summary>Shows the panel in every open scene view.</summary>
        public static void ShowAll()
        {
            foreach (SceneView view in SceneView.sceneViews)
                if (view.TryGetOverlay(Id, out Overlay overlay))
                {
                    overlay.displayed = true;
                    overlay.collapsed = false;
                }
        }

        /// <summary>Subscribes to project changes so new bakes show up.</summary>
        public override void OnCreated()
        {
            minSize = new Vector2(260f, 120f); // lets the panel be resized wider
            maxSize = new Vector2(720f, 4000f);
            EditorApplication.projectChanged += RebuildPalette;
        }

        /// <summary>Unsubscribes.</summary>
        public override void OnWillBeDestroyed() => EditorApplication.projectChanged -= RebuildPalette;

        /// <summary>Builds the panel; all state lives in <see cref="FoliageBrush"/>, so a rebuild loses nothing.</summary>
        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement { style = { minWidth = 260, flexGrow = 1 } };
            FoliageUi.Apply(root); // the shared sheet only reaches elements below a root that called Apply (idempotent)

            VisualElement head = Row(root);
            _target = new Label { style = { flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold, unityTextAlign = TextAnchor.MiddleLeft, overflow = Overflow.Hidden } };
            head.Add(_target);
            _select = new Button(SelectTarget) { text = "Select", tooltip = "Select the Foliage Forge the brush paints into." };
            _select.AddToClassList("ff-button");
            head.Add(_select);
            _create = new Button(() =>
            {
                FoliageForgeManagerEditor.CreateInScene(null);
                FoliageBrush.Invalidate();
            }) { text = "Create Foliage Forge", tooltip = "Adds a Foliage Forge (manager, Grass Field, Props root) to the open scene." };
            _create.AddToClassList("ff-button");
            _create.AddToClassList("ff-button--primary");
            root.Add(_create);

            VisualElement modes = FoliageUi.WrapRow();
            root.Add(modes);
            _paint = ModeButton(modes, "Paint", "Left-drag in the scene view paints. Hold Shift to erase instead.", () =>
            {
                FoliageBrush.Erase = false;
                FoliageBrush.Open();
                Refresh();
            });
            _erase = ModeButton(modes, "Erase", "Left-drag removes props, or grass laid down earlier. Hold Shift to paint instead.", () =>
            {
                FoliageBrush.Erase = true;
                FoliageBrush.Open();
                Refresh();
            });
            _off = ModeButton(modes, "Off", "Leave the brush (Esc in the scene view).", () =>
            {
                FoliageBrush.Close();
                Refresh();
            });

            VisualElement targets = FoliageUi.WrapRow();
            root.Add(targets);
            _grassTarget = ModeButton(targets, "Grass paint", "Paint grass strokes on the Grass Field with its Grass Profile. Grass appears when you release the mouse.", () => SetTarget(BrushTarget.Grass));
            _propsTarget = ModeButton(targets, "Prefabs", "Place the palette prefabs (baked trees and grass batches) under the Props root.", () => SetTarget(BrushTarget.Props));

            _radius = AddSlider(root, "Radius", "Brush radius in metres. Shift+scroll in the scene view.", FoliageBrush.MinRadius, FoliageBrush.MaxRadius, FoliageBrush.Radius, v => FoliageBrush.Radius = v);
            _grassDensity = new VisualElement();
            root.Add(_grassDensity);
            AddSlider(_grassDensity, "Density", "Density of each grass dab as a multiple of the profile's own density.", 0.1f, 2f, FoliageBrush.GrassDensity, v => FoliageBrush.GrassDensity = v);
            _props = new VisualElement();
            root.Add(_props);
            AddSlider(_props, "Density", "Share of the free spots filled per dab.", 0.05f, 1f, FoliageBrush.Density, v => FoliageBrush.Density = v);
            AddSlider(_props, "Tree spacing", "Minimum distance between trees.", 0.25f, 15f, FoliageBrush.Spacing, v => FoliageBrush.Spacing = v);
            AddSlider(_props, "Grass spacing", "Minimum distance between grass batches. Low values overlap different batches into a tuft.", 0.1f, 5f, FoliageBrush.GrassSpacing, v => FoliageBrush.GrassSpacing = v);
            var variants = new Toggle("Use variants") { value = FoliageBrush.UseVariants, tooltip = "A placed tree is swapped for a weighted random variant from its Tree Variant Set (Oak_V1 can become Oak_V2). Erasing a tree also erases its sibling variants." };
            Narrow(variants.labelElement);
            variants.RegisterValueChangedCallback(e =>
            {
                FoliageBrush.UseVariants = e.newValue;
                FoliageBrush.Save();
            });
            _props.Add(variants);
            var cluster = new Toggle("Cluster") { value = FoliageBrush.Cluster, tooltip = "Group same-prefab props around a few random centres per dab instead of mixing the palette evenly. Spacing still applies." };
            Narrow(cluster.labelElement);
            _props.Add(cluster);
            _clusterOptions = new VisualElement();
            _props.Add(_clusterOptions);
            AddSlider(_clusterOptions, "Cluster radius", "Rough size of one same-prefab group in metres. Smaller = more, tighter groups per dab.", 0.5f, 5f, FoliageBrush.ClusterRadius, v => FoliageBrush.ClusterRadius = v);
            Show(_clusterOptions, FoliageBrush.Cluster);
            cluster.RegisterValueChangedCallback(e =>
            {
                FoliageBrush.Cluster = e.newValue;
                FoliageBrush.Save();
                Show(_clusterOptions, e.newValue);
            });

            var edge = new Toggle("Edge bias") { value = FoliageBrush.EdgeBias, tooltip = "Prefer spots near the brush rim and near ledges or water edges. Fewer props land per dab." };
            Narrow(edge.labelElement);
            _props.Add(edge);
            _edgeOptions = new VisualElement();
            _props.Add(_edgeOptions);
            AddSlider(_edgeOptions, "Edge strength", "0 = no bias, 1 = nothing lands at the dab centre.", 0f, 1f, FoliageBrush.EdgeStrength, v => FoliageBrush.EdgeStrength = v);
            AddSlider(_edgeOptions, "Edge size", "Distance in metres at which the ground is probed: a miss or a drop there counts as an edge.", 0.25f, 5f, FoliageBrush.EdgeSize, v => FoliageBrush.EdgeSize = v);
            Show(_edgeOptions, FoliageBrush.EdgeBias);
            edge.RegisterValueChangedCallback(e =>
            {
                FoliageBrush.EdgeBias = e.newValue;
                FoliageBrush.Save();
                Show(_edgeOptions, e.newValue);
            });

            VisualElement scale = Row(_props);
            var min = new FloatField("Scale") { value = FoliageBrush.Scale.x, tooltip = "Random scale range: min, max.", style = { flexGrow = 2, flexBasis = 0 } };
            Narrow(min.labelElement);
            min.RegisterValueChangedCallback(e =>
            {
                FoliageBrush.Scale.x = Mathf.Max(0.01f, e.newValue);
                FoliageBrush.Save();
            });
            var max = new FloatField { value = FoliageBrush.Scale.y, tooltip = "Random scale range: min, max.", style = { flexGrow = 1, flexBasis = 0, minWidth = 56 } };
            max.RegisterValueChangedCallback(e =>
            {
                FoliageBrush.Scale.y = Mathf.Max(0.01f, e.newValue);
                FoliageBrush.Save();
            });
            scale.Add(min);
            scale.Add(max);

            var randomRot = new Toggle("Random rotation") { value = FoliageBrush.RandomRotation, tooltip = "Turn each placed prop by a random amount." };
            Narrow(randomRot.labelElement);
            _props.Add(randomRot);
            _rotation = new VisualElement();
            _props.Add(_rotation);
            var rotMode = new EnumField("Rotation mode", FoliageBrush.RotationMode) { tooltip = "World: turn around world axes. Align to surface: stand the prop on the ground normal first, then turn around its own axes." };
            Narrow(rotMode.labelElement);
            rotMode.RegisterValueChangedCallback(e =>
            {
                FoliageBrush.RotationMode = (BrushRotationMode)e.newValue;
                FoliageBrush.Save();
            });
            _rotation.Add(rotMode);
            var axes = new EnumFlagsField("Axes", FoliageBrush.RotationAxes) { tooltip = "Axes that get a random rotation." };
            Narrow(axes.labelElement);
            axes.RegisterValueChangedCallback(e =>
            {
                FoliageBrush.RotationAxes = (BrushRotationAxes)e.newValue;
                FoliageBrush.Save();
            });
            _rotation.Add(axes);
            VisualElement amount = Row(_rotation);
            var rmin = new FloatField("Amount") { value = FoliageBrush.RotationRange.x, tooltip = "Random rotation in degrees: min, max.", style = { flexGrow = 2, flexBasis = 0 } };
            Narrow(rmin.labelElement);
            rmin.RegisterValueChangedCallback(e =>
            {
                FoliageBrush.RotationRange.x = e.newValue;
                FoliageBrush.Save();
            });
            var rmax = new FloatField { value = FoliageBrush.RotationRange.y, tooltip = "Random rotation in degrees: min, max.", style = { flexGrow = 1, flexBasis = 0, minWidth = 56 } };
            rmax.RegisterValueChangedCallback(e =>
            {
                FoliageBrush.RotationRange.y = e.newValue;
                FoliageBrush.Save();
            });
            amount.Add(rmin);
            amount.Add(rmax);
            Show(_rotation, FoliageBrush.RandomRotation);
            randomRot.RegisterValueChangedCallback(e =>
            {
                FoliageBrush.RandomRotation = e.newValue;
                FoliageBrush.Save();
                Show(_rotation, e.newValue);
            });

            VisualElement paletteHead = FoliageUi.WrapRow();
            _props.Add(paletteHead);
            var paletteTitle = FoliageUi.GroupLabel("Palette (click to toggle)");
            paletteTitle.style.flexGrow = 1;
            paletteHead.Add(paletteTitle);
            var addSelected = new Button(AddSelected) { text = "+ Selected", tooltip = "Add the prefabs selected in the Project window." };
            addSelected.AddToClassList("ff-button");
            paletteHead.Add(addSelected);
            var bake = new Button(() => FoliageForgeWindow.Open()) { text = "Bake...", tooltip = "Open the Foliage Forge Editor to bake trees and grass batches." };
            bake.AddToClassList("ff-button");
            paletteHead.Add(bake);
            VisualElement tileRow = FoliageUi.WrapRow();
            tileRow.Add(FoliageUi.GroupLabel("Tile size", "Size of the palette thumbnails."));
            for (int i = 0; i < TileNames.Length; i++)
            {
                int size = i;
                _tileChips[i] = FoliageUi.ChipButton(TileNames[i], TileSize == i, _ =>
                {
                    TileSize = size;
                    for (int k = 0; k < _tileChips.Length; k++) FoliageUi.SetChipOn(_tileChips[k], k == size);
                    RebuildPalette();
                }, null, false);
                tileRow.Add(_tileChips[i]);
            }
            _props.Add(tileRow);
            _paletteMix = new Label { tooltip = "Share of placements each ticked prefab gets, from the Mix % under its thumbnail.", style = { whiteSpace = WhiteSpace.Normal } };
            _paletteMix.AddToClassList("ff-muted");
            _props.Add(_paletteMix);
            var scroll = new ScrollView { style = { maxHeight = 280 } };
            _palette = new VisualElement();
            scroll.Add(_palette);
            _props.Add(scroll);
            _grassTypes = new VisualElement();
            root.Add(_grassTypes);

            _ground = new LayerMaskField("Ground") { tooltip = "Layers the brush and the grass land on (needs colliders). Keep placed props off these layers." };
            Narrow(_ground.labelElement);
            _ground.RegisterValueChangedCallback(e => FoliageBrush.SetGroundMask(e.newValue));
            root.Add(_ground);
            _addLayer = new Button(() => FoliageBrush.SetGroundMask(FoliageBrush.GroundMask | 1 << FoliageBrush.CursorLayer));
            _addLayer.AddToClassList("ff-button");
            root.Add(_addLayer);
            _status = new HelpBox();
            root.Add(_status);
            _stats = new Label();
            _stats.AddToClassList("ff-muted");
            root.Add(_stats);

            root.schedule.Execute(Refresh).Every(200);
            RebuildPalette();
            Refresh();
            return root;
        }

        private void Refresh()
        {
            if (_target == null) return;
            FoliageForgeManager m = FoliageBrush.Manager;
            GrassField g = FoliageBrush.Grass;
            bool any = m != null || g != null, active = FoliageBrush.IsActive;
            _target.text = m != null ? m.name : g != null ? g.name + " (Grass Field)" : "No Foliage Forge in this scene";
            Show(_select, any);
            Show(_create, !any);
            Mark(_paint, active && !FoliageBrush.Erase);
            Mark(_erase, active && FoliageBrush.Erase);
            Mark(_off, !active);
            Mark(_grassTarget, FoliageBrush.Target == BrushTarget.Grass);
            Mark(_propsTarget, FoliageBrush.Target == BrushTarget.Props);
            Show(_grassDensity, FoliageBrush.Target == BrushTarget.Grass);
            Show(_props, FoliageBrush.Target == BrushTarget.Props);
            GrassProfile gp = g != null ? g.Profile : null;
            Show(_grassTypes, FoliageBrush.Target == BrushTarget.Grass && gp != null);
            RefreshGrassTypes(gp);
            if (EditorApplication.timeSinceStartup < _pumpUntil) EditorApplication.QueuePlayerLoopUpdate(); // let Grass Fields see the profile edit
            if (!Mathf.Approximately(_radius.value, FoliageBrush.Radius)) _radius.SetValueWithoutNotify(FoliageBrush.Radius);
            _ground.SetEnabled(any);
            _ground.SetValueWithoutNotify(FoliageBrush.GroundMask);

            bool wrongLayer = active && FoliageBrush.Hovering && FoliageBrush.Ground == BrushGround.WrongLayer;
            Show(_addLayer, wrongLayer);
            if (wrongLayer) _addLayer.text = $"Add layer '{LayerMask.LayerToName(FoliageBrush.CursorLayer)}' to Ground";
            string problem = FoliageBrush.Problem(active);
            _status.messageType = problem != null ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info;
            _status.text = problem ?? (active ? FoliageBrush.Note : null) ?? (active
                ? $"Left-drag to {(FoliageBrush.Erase ? "erase" : "paint")}. Shift: {(FoliageBrush.Erase ? "paint" : "erase")}. Shift+scroll: radius. Esc: exit."
                : "Pick Paint or Erase to start brushing.");

            string stats = m != null && m.Props != null ? $"{m.Props.childCount} props" : "no props";
            if (g != null) stats += $", {g.StrokeCount} grass strokes, {g.InstanceCount:N0} clumps";
            _stats.text = stats;

            if (m != _paletteFor || (m != null && OtherCount(m) != _otherCount)) RebuildPalette();
            foreach (var (tile, prefab, mix) in _tiles)
            {
                int at = m != null ? m.Palette.IndexOf(prefab) : -1;
                MarkTile(tile, at >= 0);
                Show(mix, at >= 0);
                if (at >= 0 && mix.value != Mathf.RoundToInt(m.PaletteWeight(at))) mix.SetValueWithoutNotify(Mathf.RoundToInt(m.PaletteWeight(at)));
            }
            var names = new List<string>();
            var weights = new List<float>();
            if (m != null)
                for (int i = 0; i < m.Palette.Count; i++)
                    if (m.Palette[i] != null)
                    {
                        names.Add(m.Palette[i].name);
                        weights.Add(m.PaletteWeight(i));
                    }
            _paletteMix.text = names.Count > 0 ? "Mix: " + FoliageBrush.MixSummary(names, weights) : "";
        }

        private void RefreshGrassTypes(GrassProfile p)
        {
            string key = p == null || p.Variants == null ? "" : string.Join("|", System.Array.ConvertAll(p.Variants, v => v.Name));
            if (p != _typesFor || key != _typesKey) RebuildGrassTypes(p, key);
            if (p == null || _grassMix == null) return;
            var names = new List<string>();
            var weights = new List<float>();
            for (int i = 0; i < _typeRows.Count; i++)
            {
                GrassVariant v = p.Variants[i];
                var (on, mix) = _typeRows[i];
                on.SetValueWithoutNotify(v.Enabled);
                if (!Mathf.Approximately(mix.value, v.Mix)) mix.SetValueWithoutNotify(v.Mix);
                mix.SetEnabled(v.Enabled);
                names.Add(v.Name);
                weights.Add(v.MixedDensity);
            }
            _grassMix.text = "Clump share: " + FoliageBrush.MixSummary(names, weights);
        }

        // one row per grass type of the profile: on/off and Mix %, written to the asset
        private void RebuildGrassTypes(GrassProfile p, string key)
        {
            _grassTypes.Clear();
            _typeRows.Clear();
            _grassMix = null;
            _typesFor = p;
            _typesKey = key;
            if (p == null || p.Variants == null) return;
            _grassTypes.Add(FoliageUi.GroupLabel($"Grass types ({p.name})"));
            for (int i = 0; i < p.Variants.Length; i++)
            {
                int index = i;
                VisualElement row = Row(_grassTypes);
                var on = new Toggle { tooltip = "Paint this grass type.", style = { marginRight = 2 } };
                on.RegisterValueChangedCallback(e => EditType(p, index, v => v.Enabled = e.newValue));
                var mix = new Slider(p.Variants[i].Name, 0f, 100f) { showInputField = true, tooltip = "Mix %: share of this type's density that grows (0..100).", style = { flexGrow = 1 } };
                Narrow(mix.labelElement);
                mix.RegisterValueChangedCallback(e => EditType(p, index, v => v.Mix = e.newValue));
                row.Add(on);
                row.Add(mix);
                _typeRows.Add((on, mix));
            }
            _grassMix = new Label { style = { whiteSpace = WhiteSpace.Normal } };
            _grassMix.AddToClassList("ff-muted");
            _grassTypes.Add(_grassMix);
            var grassHint = new Label("These edit the Grass Profile: every stroke and fill using it re-blends.");
            grassHint.AddToClassList("ff-hint");
            _grassTypes.Add(grassHint);
        }

        private void EditType(GrassProfile p, int index, System.Action<GrassVariant> edit)
        {
            if (p == null || index >= p.Variants.Length) return;
            Undo.RecordObject(p, "Grass Type Mix");
            edit(p.Variants[index]);
            p.Revision++;
            EditorUtility.SetDirty(p);
            _pumpUntil = EditorApplication.timeSinceStartup + 1.0;
        }

        private int OtherCount(FoliageForgeManager m)
        {
            int n = 0;
            foreach (GameObject p in m.Palette) if (p != null && !_baked.Contains(p)) n++;
            return n;
        }

        private void RebuildPalette()
        {
            if (_palette == null) return;
            _palette.Clear();
            AssetPreview.SetPreviewTextureCacheSize(256); // the default evicts thumbnails once a palette has more than a few prefabs
            _tiles.Clear();
            _pending.Clear();
            _baked.Clear();
            FoliageForgeManager m = FoliageBrush.Manager;
            _paletteFor = m;
            var trees = new List<GameObject>();
            var batches = new List<GameObject>();
            foreach (GameObject go in TreeBaker.BakedPrefabs())
            {
                _baked.Add(go);
                (go.GetComponent<GrassBatch>() != null ? batches : trees).Add(go);
            }
            var others = new List<GameObject>();
            if (m != null)
                foreach (GameObject p in m.Palette)
                    if (p != null && !_baked.Contains(p) && !others.Contains(p)) others.Add(p);
            _otherCount = m != null ? OtherCount(m) : 0;

            Section("Trees", trees, "No baked trees yet: bake one in the Foliage Forge Editor.");
            Section("Grass batches", batches, "No grass batches yet: bake them in the editor's Grass tab.");
            if (others.Count > 0) Section("Other prefabs", others, null);

            // thumbnails render asynchronously; keep asking until each one is ready
            int tries = 0;
            IVisualElementScheduledItem poll = null;
            poll = _palette.schedule.Execute(() =>
            {
                for (int i = _pending.Count - 1; i >= 0; i--)
                {
                    GameObject prefab = _pending[i].prefab;
                    Texture2D preview = prefab != null ? PrefabThumbnail.Get(prefab) : null;
                    if (prefab != null && preview == null) continue;
                    if (preview != null) _pending[i].image.image = preview;
                    _pending.RemoveAt(i);
                }
                if (_pending.Count == 0 || ++tries > 400) poll.Pause();
            }).Every(150);
        }

        private void Section(string title, List<GameObject> prefabs, string empty)
        {
            _palette.Add(FoliageUi.GroupLabel($"{title} ({prefabs.Count})"));
            if (prefabs.Count == 0)
            {
                var emptyLabel = new Label(empty) { style = { whiteSpace = WhiteSpace.Normal } };
                emptyLabel.AddToClassList("ff-muted");
                _palette.Add(emptyLabel);
                return;
            }
            var grid = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            foreach (GameObject go in prefabs) grid.Add(Tile(go));
            _palette.Add(grid);
        }

        private VisualElement Tile(GameObject prefab)
        {
            int thumb = TileThumbs[TileSize];
            var tile = new VisualElement
            {
                tooltip = $"{prefab.name}\n{AssetDatabase.GetAssetPath(prefab)}\nClick to add to or remove from the palette.",
                style =
                {
                    width = thumb + 10, height = StyleKeyword.Auto, minWidth = 0, marginRight = 3, marginBottom = 3, paddingTop = 3, paddingBottom = 3, paddingLeft = 2, paddingRight = 2,
                    alignItems = Align.Center,
                },
            };
            tile.AddToClassList("ff-chip"); // shared idle / hover look; ff-chip--on (accent) marks palette members, see MarkTile
            Texture2D preview = PrefabThumbnail.Get(prefab);
            var image = new Image { image = preview != null ? preview : AssetPreview.GetMiniThumbnail(prefab), scaleMode = ScaleMode.ScaleToFit, style = { width = thumb, height = thumb } };
            if (preview == null) _pending.Add((image, prefab));
            tile.Add(image);
            tile.Add(new Label(prefab.name) { style = { fontSize = 11, maxWidth = thumb + 6, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis, whiteSpace = WhiteSpace.NoWrap, unityFontStyleAndWeight = FontStyle.Normal } });
            tile.RegisterCallback<ClickEvent>(e =>
            {
                FoliageForgeManager m = FoliageBrush.Manager;
                if (m == null) return;
                FoliageBrush.SetInPalette(m, new[] { prefab }, !m.Palette.Contains(prefab));
                Refresh();
            });
            var mix = new IntegerField { isDelayed = true, tooltip = "Mix % (0..100): how often the brush picks this prefab next to the other ticked ones. 0 = never.", style = { width = thumb } };
            mix.RegisterCallback<ClickEvent>(e => e.StopPropagation()); // typing must not toggle the tile
            mix.RegisterValueChangedCallback(e =>
            {
                FoliageForgeManager m = FoliageBrush.Manager;
                if (m != null) FoliageBrush.SetPaletteWeight(m, prefab, e.newValue);
                Refresh();
            });
            tile.Add(mix);
            _tiles.Add((tile, prefab, mix));
            return tile;
        }

        private static void MarkTile(VisualElement tile, bool on) => FoliageUi.SetChipOn(tile, on);

        private void SetTarget(BrushTarget target)
        {
            FoliageBrush.Target = target;
            FoliageBrush.Save();
            Refresh();
        }

        private static void SelectTarget()
        {
            if (FoliageBrush.Manager != null) Selection.activeObject = FoliageBrush.Manager.gameObject;
            else if (FoliageBrush.Grass != null) Selection.activeObject = FoliageBrush.Grass.gameObject;
        }

        private static void AddSelected()
        {
            FoliageForgeManager m = FoliageBrush.Manager;
            if (m == null) return;
            var picked = new List<GameObject>();
            foreach (Object o in Selection.objects)
                if (o is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go)) picked.Add(go);
            if (picked.Count > 0) FoliageBrush.SetInPalette(m, picked, true);
        }

        private static VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2 } };
            parent.Add(row);
            return row;
        }

        private static Button ModeButton(VisualElement row, string text, string tooltip, System.Action click)
        {
            Button button = FoliageUi.ChipButton(text, false, _ => click(), tooltip, false);
            button.style.flexGrow = 1;
            button.style.flexBasis = 0;
            row.Add(button);
            return button;
        }

        private static void Mark(Button button, bool on) => FoliageUi.SetChipOn(button, on);

        private static void Show(VisualElement element, bool on) => element.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        private static void Narrow(Label label)
        {
            // same label column as .ff-field: at least 110px, ~40%, wraps instead of clipping
            label.style.minWidth = 110;
            label.style.width = new Length(40f, LengthUnit.Percent);
            label.style.flexShrink = 0;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.overflow = Overflow.Visible;
        }

        private static Slider AddSlider(VisualElement parent, string label, string tooltip, float min, float max, float value, System.Action<float> set)
        {
            var slider = new Slider(label, min, max) { value = value, showInputField = true, tooltip = tooltip };
            Narrow(slider.labelElement);
            slider.RegisterValueChangedCallback(e =>
            {
                set(e.newValue);
                FoliageBrush.Save();
            });
            parent.Add(slider);
            return slider;
        }
    }
}
