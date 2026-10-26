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
        static readonly Color Ok = new Color(0.2f, 0.45f, 0.3f);
        static readonly Color Warn = new Color(0.65f, 0.42f, 0.08f);
        static readonly Color Neutral = new Color(0.25f, 0.27f, 0.28f);

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
        ScrollView _tiles;
        Label _galleryTitle, _folderLabel;
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
            window.minSize = new Vector2(780f, 560f);
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
            _unsaved = new HelpBox(string.Empty, HelpBoxMessageType.Info);
            _unsaved.Add(new Button(() =>
            {
                if (_grassMode) CreateGrass();
                else CreateRecipe();
            }) { text = "Save...", tooltip = "Save it as an asset so it can be picked again later." });
            root.Add(BuildToolbar());

            var split = new TwoPaneSplitView(0, 360f, TwoPaneSplitViewOrientation.Horizontal) { style = { flexGrow = 1 } };
            _inspectorHost = new ScrollView { name = InspectorHostName };
            split.Add(_inspectorHost);

            var right = new VisualElement { style = { flexGrow = 1, minWidth = 320 } };
            right.Add(BuildViewBar());
            _view = new IMGUIContainer(DrawPreview) { style = { flexGrow = 1, flexShrink = 1, flexBasis = 0, minHeight = 120 } };
            right.Add(_view);
            _statsBar = new VisualElement { style = { flexShrink = 0, flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, paddingLeft = 6, paddingTop = 4, paddingBottom = 4 } };
            right.Add(_statsBar);
            _status = new Label { style = { flexShrink = 0, paddingLeft = 8, paddingRight = 8, paddingBottom = 4, opacity = 0.9f, whiteSpace = WhiteSpace.Normal } };
            right.Add(_status);
            _galleryBox = BuildGallery();
            right.Add(_galleryBox);
            split.Add(right);
            root.Add(split);

            root.schedule.Execute(() =>
            {
                if (_grassMode && _grass != null && _grass.Revision != _grassRevision) RegenerateGrass();
            }).Every(50);
            RefreshGallery();
            SetGrassMode(_grassMode);
        }

        private void OnProjectChange() => RefreshGallery();
    }
}
