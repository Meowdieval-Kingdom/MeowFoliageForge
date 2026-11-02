using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// The Foliage Forge workspace: recipe editor on the left, orbit preview and budget bar on the right.
    /// The tree regenerates a moment after any edit; nothing runs outside the editor.
    /// </summary>
    public sealed partial class FoliageForgeWindow : EditorWindow
    {
        /// <summary>Name of the element that hosts the recipe inspector; the inspector hides its own open button inside it.</summary>
        public const string InspectorHostName = "ff-inspector-host";

        // _recipe is the working copy the inspector and preview edit; _source is the saved recipe asset it was loaded from.
        // The asset only changes when Bake writes the working copy back into it.
        [SerializeField] TreeRecipe _recipe;
        [SerializeField] TreeRecipe _source;
        ObjectField _recipeField;
        [SerializeField] bool _windOn;
        [SerializeField] float _windDirection = 45f;
        [SerializeField] float _windStrength = 0.3f;

        Mesh _mesh;
        TreeStats _stats;
        Material[] _materials = new Material[2];
        TreePreview _preview;
        IMGUIContainer _view;
        VisualElement _inspectorHost;
        VisualElement _statsBar;
        Label _status;
        ToolbarButton _bakeButton;
        IVisualElementScheduledItem _regen;
        VisualElement _tiles;
        [SerializeField] bool _smallThumbs;
        Label _galleryTitle, _folderLabel;
        // responsive layout: the split is rebuilt when the width crosses the stacking breakpoint
        const float CompactWidth = 700f, StackedWidth = 900f;
        VisualElement _splitHost, _right, _secondaryRow;
        TwoPaneSplitView _split;
        bool? _stacked;
        Button _loadButton;
        GameObject _picked; // tile picked in the gallery, loaded by the button under it
        VisualElement _windPanel, _blobPanel;

        // Blob preview mode: the selected blob generated alone at the origin (see BlobViewer)
        Mesh _blobMesh;
        readonly System.Collections.Generic.List<CanopyBlob> _blobList = new System.Collections.Generic.List<CanopyBlob>();
        string _blobSummary = string.Empty;
        bool _blobOk;
        Button _viewerToggle;
        VisualElement _viewerNav;
        Label _viewerNavLabel;
        readonly System.Collections.Generic.List<Action> _viewerSync = new System.Collections.Generic.List<Action>();

        // grass mode: a GrassProfile asset edited in place, previewed as a flat patch
        [SerializeField] bool _grassMode;
        [SerializeField] GrassProfile _grass;
        [SerializeField] float _bakeSize = 2f;
        GrassPatch _patch;
        int _grassRevision = -1;
        ObjectField _grassField;
        VisualElement _treeTools, _bakeTools, _grassTools, _modeRow, _galleryBox;
        VisualElement _treePresets, _grassPresets;
        HelpBox _unsaved; // shown above the inspector while the tree or profile is a scratch object
        Button _tabTrees, _tabGrass;
        readonly System.Collections.Generic.List<Action> _modePaint = new System.Collections.Generic.List<Action>();
        [SerializeField] int _galleryFilter; // 0 all, 1 trees, 2 grass batches
        readonly System.Collections.Generic.List<Action> _filterPaint = new System.Collections.Generic.List<Action>();

        /// <summary>Opens the window, optionally on a recipe.</summary>
        [MenuItem("Meowdieval/Foliage Forge/Foliage Forge Editor", false, 1)]
        public static void Open() => Open(null);

        /// <summary>Opens the window showing <paramref name="recipe"/>.</summary>
        public static void Open(TreeRecipe recipe)
        {
            var window = GetWindow<FoliageForgeWindow>("Foliage Forge");
            window.minSize = new Vector2(420f, 480f);
            window.Show();
            window.Focus();
            if (recipe != null) window.SetRecipe(recipe);
        }

        [OnOpenAsset(1)]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            if (EditorUtility.EntityIdToObject((EntityId)instanceId) is not TreeRecipe recipe) return false;
            Open(recipe);
            return true;
        }

        private void OnEnable()
        {
            _preview = new TreePreview();
            _preview.BeforeBlobEdit = () =>
            {
                if (_recipe != null) Undo.RecordObject(_recipe, "Edit Canopy Blob");
            };
            _preview.AfterBlobEdit = () =>
            {
                if (_recipe == null) return;
                EditorUtility.SetDirty(_recipe);
                _view?.MarkDirtyRepaint();
                ScheduleRegenerate();
            };
            _mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            _blobMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            _patch = new GrassPatch();
            Undo.undoRedoPerformed += ScheduleRegenerate;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= ScheduleRegenerate;
            _preview?.Dispose();
            _patch?.Dispose();
            if (_grass != null && !EditorUtility.IsPersistent(_grass)) DestroyImmediate(_grass);
            if (_mesh != null) DestroyImmediate(_mesh);
            if (_blobMesh != null) DestroyImmediate(_blobMesh);
            DestroyVariantMeshes();
            DestroyTempMaterials();
            ReleaseWind();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.flexGrow = 1;
            FoliageUi.Apply(root);
            _unsaved = new HelpBox(string.Empty, HelpBoxMessageType.Info);
            _unsaved.Add(new Button(() =>
            {
                if (_grassMode) CreateGrass();
                else CreateRecipe();
            }) { text = "Save...", tooltip = "Save it as an asset so it can be picked again later." });
            root.Add(BuildToolbar());

            _inspectorHost = new ScrollView { name = InspectorHostName };
            _inspectorHost.AddToClassList("ff-inspector");

            // right column: view bar and preview keep their height; stats, status, panels and the gallery scroll below
            _right = new VisualElement();
            _right.AddToClassList("ff-right");
            _right.Add(BuildViewBar());
            _view = new IMGUIContainer(DrawPreview);
            _view.AddToClassList("ff-preview");
            _right.Add(_view);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("ff-right-scroll");
            scroll.contentContainer.AddToClassList("ff-side-content");
            _statsBar = new VisualElement();
            _statsBar.AddToClassList("ff-row");
            _statsBar.AddToClassList("ff-stats");
            scroll.Add(_statsBar);
            _status = new Label();
            _status.AddToClassList("ff-status");
            scroll.Add(_status);
            scroll.Add(BuildBlobPanel());
            scroll.Add(BuildWindPanel());
            _galleryBox = BuildGallery();
            scroll.Add(_galleryBox);
            _right.Add(scroll);

            _splitHost = new VisualElement { style = { flexGrow = 1 } };
            root.Add(_splitHost);
            RebuildSplit(false);
            root.RegisterCallback<GeometryChangedEvent>(e => ApplyResponsiveLayout(e.newRect.width));

            root.schedule.Execute(() =>
            {
                if (_grassMode && _grass != null && _grass.Revision != _grassRevision) RegenerateGrass();
            }).Every(50);
            RefreshGallery();
            SetGrassMode(_grassMode);
        }

        // width breakpoints: below CompactWidth secondary toolbar actions move into the More menu;
        // below StackedWidth the split goes vertical (preview on top, settings below)
        private void ApplyResponsiveLayout(float width)
        {
            if (float.IsNaN(width) || width <= 0f) return;
            rootVisualElement.EnableInClassList("ff-compact", width < CompactWidth);
            bool stacked = width < StackedWidth;
            if (_stacked != stacked) RebuildSplit(stacked);
        }

        // TwoPaneSplitView cannot be assumed to switch orientation in place, so a new one takes over the two panes
        private void RebuildSplit(bool stacked)
        {
            _stacked = stacked;
            _split?.RemoveFromHierarchy();
            _inspectorHost.RemoveFromHierarchy();
            _right.RemoveFromHierarchy();
            _right.EnableInClassList("ff-right--stacked", stacked);
            _inspectorHost.EnableInClassList("ff-inspector--stacked", stacked);
            if (stacked)
            {
                float top = Mathf.Max(300f, rootVisualElement.layout.height * 0.45f);
                _split = new TwoPaneSplitView(0, top, TwoPaneSplitViewOrientation.Vertical);
                _split.Add(_right);
                _split.Add(_inspectorHost);
            }
            else
            {
                _split = new TwoPaneSplitView(0, 360f, TwoPaneSplitViewOrientation.Horizontal);
                _split.Add(_inspectorHost);
                _split.Add(_right);
            }
            _split.style.flexGrow = 1;
            _splitHost.Add(_split);
        }

        private void OnProjectChange() => RefreshGallery();
    }
}
