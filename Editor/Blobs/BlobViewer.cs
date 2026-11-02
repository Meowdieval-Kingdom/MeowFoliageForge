using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// Generates one canopy blob on its own, so its leaf groups can be judged without the rest of the tree.
    /// The blob is built from a throwaway copy of the recipe (destroyed straight away); the real recipe is never written.
    /// </summary>
    public static class BlobViewer
    {
        /// <summary>Blob the inspector inset shows; the Foliage Forge window keeps it in step with its selection.</summary>
        public static int PreferredBlob;

        const float PollSeconds = 0.15f;
        static readonly string[] DisplayNames = { "Shaded", "Normals", "Wireframe" };

        /// <summary>
        /// Builds blob <paramref name="blobIndex"/> (clamped) of <paramref name="source"/> alone, moved to the origin, into <paramref name="mesh"/>.
        /// <paramref name="blob"/> is the recentred blob. False when the recipe has no blobs. <paramref name="source"/> is not modified.
        /// </summary>
        public static bool Build(TreeRecipe source, int blobIndex, Mesh mesh, out TreeStats stats, out CanopyBlob blob)
        {
            stats = default;
            blob = default;
            if (source == null || mesh == null || source.Blobs == null || source.Blobs.Count == 0) return false;
            blob = source.Blobs[Mathf.Clamp(blobIndex, 0, source.Blobs.Count - 1)];
            blob.Position = Vector3.zero;
            TreeRecipe temp = Throwaway(source, blob);
            try
            {
                stats = TreeGenerator.Build(temp, mesh);
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
            return true;
        }

        // a copy of the recipe holding only this blob and a stub trunk, with the caps lifted so the preview shows every group
        private static TreeRecipe Throwaway(TreeRecipe source, CanopyBlob blob)
        {
            TreeRecipe r = Object.Instantiate(source);
            r.hideFlags = HideFlags.HideAndDontSave;
            TrunkSettings t = r.Trunk;
            t.Height = 0.2f;
            t.BaseRadius = 0.01f;
            t.Bend = 0f;
            t.Lean = 0f;
            t.Flare = 0f;
            t.Twist = 0f;
            t.RootLobes = 0;
            t.Gnarl = 0f;
            t.NoiseBend = 0f;
            t.Splits = 0;
            t.Roots = 0;
            r.Branches.Count = 0;
            r.Branches.Broken = 0f;
            LeafSettings l = r.Leaves;
            l.Layout = LeafLayout.Blobs;
            // these act on the whole crown or on branches, which are gone
            l.Envelope = CrownShape.None;
            l.Clumps = 0;
            l.TipClumps = false;
            l.BranchTipBias = 0f;
            l.CrownOnTrunkTop = false;
            BudgetSettings b = r.Budget;
            b.MaxCards = 1000000;
            b.MaxTriangles = 100000000;
            b.FitCardsToTriangles = false;
            b.AddTrunkCollider = false;
            r.Blobs = new List<CanopyBlob> { blob };
            return r;
        }

        /// <summary>One line of counts for a built blob: leaf groups, quads and triangles.</summary>
        public static string Summary(TreeRecipe source, TreeStats stats, Mesh mesh)
        {
            int perGroup = Mathf.Max(1, source.Leaves.GroupQuads);
            int tris = mesh != null && mesh.subMeshCount > 1 ? (int)(mesh.GetIndexCount(1) / 3) : 0;
            return $"Groups {stats.Cards / perGroup}   Quads {stats.Cards}   Triangles {tris}";
        }

        /// <summary>
        /// A small orbit view of one blob (<see cref="PreferredBlob"/>, first by default) with a display toolbar. It regenerates a moment
        /// after <paramref name="recipe"/> changes and frees its preview when removed from the panel.
        /// </summary>
        public static VisualElement CreateInset(TreeRecipe recipe) => new Inset(recipe);

        const float ButtonHeight = 24f, BarPad = 6f, BarSpace = 4f, BarMaxWidth = 460f;
        static readonly float[] ButtonWidths = { 216f, 62f, 72f, 72f }; // display toggle group (3 segments), Cage, Sphere, Arrows
        static readonly Rect[] Slots = new Rect[4];
        static string _lastSummary = string.Empty;
        static GUIStyle _buttonStyle, _summaryStyle;

        // the toolbar sits at the top of the preview rect; its height follows the wrapped rows and summary text
        internal static Rect ToolbarRect(Rect view) => Layout(view, _lastSummary);

        // flows the buttons into rows that fit view.width, then the word-wrapped summary below; fills Slots, returns the backdrop rect
        private static Rect Layout(Rect view, string summary)
        {
            EnsureStyles();
            float width = Mathf.Max(Mathf.Min(view.width - 8f, BarMaxWidth), 60f);
            float inner = width - 2f * BarPad;
            float x = 0f, y = 0f;
            for (int i = 0; i < ButtonWidths.Length; i++)
            {
                float w = Mathf.Min(ButtonWidths[i], inner);
                if (x > 0f && x + w > inner)
                {
                    x = 0f;
                    y += ButtonHeight + BarSpace;
                }
                Slots[i] = new Rect(view.x + 4f + BarPad + x, view.y + 4f + BarPad + y, w, ButtonHeight);
                x += w + BarSpace;
            }
            float height = 2f * BarPad + y + ButtonHeight;
            if (!string.IsNullOrEmpty(summary)) height += BarSpace + _summaryStyle.CalcHeight(new GUIContent(summary), inner);
            return new Rect(view.x + 4f, view.y + 4f, width, height);
        }

        private static void EnsureStyles()
        {
            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(EditorStyles.miniButton) { fontSize = 11, alignment = TextAnchor.MiddleCenter, fixedHeight = 0f };
                _summaryStyle = new GUIStyle(EditorStyles.label) { fontSize = 11, wordWrap = true };
                _summaryStyle.normal.textColor = Color.white;
            }
        }

        /// <summary>Draws the display and overlay toolbar plus <paramref name="summary"/> over the preview; call after drawing it.</summary>
        internal static void DrawToolbar(Rect view, TreePreview preview, string summary)
        {
            summary ??= string.Empty;
            _lastSummary = summary;
            Rect bar = Layout(view, summary);
            EditorGUI.DrawRect(bar, new Color(0.05f, 0.05f, 0.06f, 0.88f));
            preview.Display = (BlobDisplay)GUI.Toolbar(Slots[0], (int)preview.Display, DisplayNames, _buttonStyle);
            preview.ShowCage = GUI.Toggle(Slots[1], preview.ShowCage, "Cage", _buttonStyle);
            preview.ShowReference = GUI.Toggle(Slots[2], preview.ShowReference, "Sphere", _buttonStyle);
            preview.ShowNormalArrows = GUI.Toggle(Slots[3], preview.ShowNormalArrows, "Arrows", _buttonStyle);
            if (summary.Length > 0)
            {
                float top = Slots[0].y;
                for (int i = 1; i < Slots.Length; i++) top = Mathf.Max(top, Slots[i].y);
                float inner = bar.width - 2f * BarPad;
                GUI.Label(new Rect(bar.x + BarPad, top + ButtonHeight + BarSpace, inner, bar.yMax - BarPad - (top + ButtonHeight + BarSpace)), summary, _summaryStyle);
            }
        }

        // inspector element: label, then an orbit view of the preferred blob that follows the recipe
        sealed class Inset : VisualElement
        {
            readonly TreeRecipe _recipe;
            readonly IMGUIContainer _view;
            readonly List<CanopyBlob> _blob = new List<CanopyBlob>();
            TreePreview _preview;
            Mesh _mesh;
            Material[] _materials = new Material[2];
            string _summary = string.Empty;
            int _hash;
            bool _built;
            IVisualElementScheduledItem _poll;

            public Inset(TreeRecipe recipe)
            {
                _recipe = recipe;
                style.flexShrink = 0;
                style.marginTop = 4;
                FoliageUi.Apply(this);
                var hint = new Label("Single blob. Leaf settings are global: every blob uses the same ones.");
                hint.AddToClassList("ff-hint");
                Add(hint);
                // flexible: prefers 240px, shrinks to 200px in a cramped inspector, grows when the parent gives it room
                _view = new IMGUIContainer(Draw) { style = { flexBasis = 240, flexGrow = 1, flexShrink = 1, minHeight = 200, marginTop = 2 } };
                Add(_view);
                RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    _built = false;
                    _poll = schedule.Execute(Poll).Every((long)(PollSeconds * 1000f));
                });
                RegisterCallback<DetachFromPanelEvent>(_ => Release());
            }

            // the revision is the recipe's serialized text plus the blob shown; cheap at this rate, and it covers edits from any source
            private void Poll()
            {
                if (_recipe == null) return;
                int hash = JsonUtility.ToJson(_recipe).GetHashCode() * 31 + PreferredBlob;
                if (_built && hash == _hash) return;
                _hash = hash;
                Rebuild();
                _view.MarkDirtyRepaint();
            }

            private void Rebuild()
            {
                _mesh ??= new Mesh { hideFlags = HideFlags.HideAndDontSave };
                DestroyTempMaterials();
                _materials = TreeBaker.PreviewMaterials(_recipe);
                _blob.Clear();
                if (Build(_recipe, PreferredBlob, _mesh, out TreeStats stats, out CanopyBlob blob))
                {
                    _blob.Add(blob);
                    _summary = Summary(_recipe, stats, _mesh);
                }
                else _summary = string.Empty;
                if (_preview != null) _preview.MeshVersion++;
                _built = true;
            }

            private void Draw()
            {
                Rect rect = new Rect(0f, 0f, _view.contentRect.width, _view.contentRect.height);
                if (_blob.Count == 0)
                {
                    GUI.Label(new Rect(8f, 8f, rect.width - 16f, 20f), "No blobs in this recipe.");
                    return;
                }
                _preview ??= new TreePreview { Mode = PreviewMode.Blob };
                _preview.IgnoreRect = ToolbarRect(rect);
                _preview.Draw(rect, _mesh, _materials, _blob);
                DrawToolbar(rect, _preview, _summary);
            }

            private void DestroyTempMaterials()
            {
                foreach (Material m in _materials)
                    if (m != null && (m.hideFlags & HideFlags.HideAndDontSave) != 0) Object.DestroyImmediate(m);
                _materials = new Material[2];
            }

            private void Release()
            {
                _poll?.Pause();
                _preview?.Dispose();
                _preview = null;
                if (_mesh != null) Object.DestroyImmediate(_mesh);
                _mesh = null;
                DestroyTempMaterials();
                _blob.Clear();
            }
        }
    }
}
