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

        // the toolbar sits at the top of the preview rect
        internal static Rect ToolbarRect(Rect view) => new Rect(view.x + 4f, view.y + 4f, Mathf.Min(view.width - 8f, 430f), 20f);

        /// <summary>Draws the display and overlay toolbar plus <paramref name="summary"/> over the preview; call after drawing it.</summary>
        internal static void DrawToolbar(Rect view, TreePreview preview, string summary)
        {
            Rect bar = ToolbarRect(view);
            EditorGUI.DrawRect(new Rect(bar.x - 2f, bar.y - 2f, bar.width + 4f, bar.height + 22f), new Color(0.1f, 0.1f, 0.1f, 0.6f));
            var shown = new Rect(bar.x, bar.y, 190f, bar.height);
            preview.Display = (BlobDisplay)GUI.Toolbar(shown, (int)preview.Display, DisplayNames, EditorStyles.miniButton);
            float x = shown.xMax + 6f;
            preview.ShowCage = GUI.Toggle(new Rect(x, bar.y, 50f, bar.height), preview.ShowCage, "Cage", EditorStyles.miniButton);
            preview.ShowReference = GUI.Toggle(new Rect(x + 52f, bar.y, 58f, bar.height), preview.ShowReference, "Sphere", EditorStyles.miniButton);
            preview.ShowNormalArrows = GUI.Toggle(new Rect(x + 112f, bar.y, 60f, bar.height), preview.ShowNormalArrows, "Arrows", EditorStyles.miniButton);
            if (_summaryStyle == null)
            {
                _summaryStyle = new GUIStyle(EditorStyles.miniLabel);
                _summaryStyle.normal.textColor = Color.white;
            }
            GUI.Label(new Rect(bar.x, bar.y + 21f, bar.width, 18f), summary, _summaryStyle);
        }

        static GUIStyle _summaryStyle;

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
                Add(new Label("Single blob. Leaf settings are global: every blob uses the same ones.") { style = { opacity = 0.75f, whiteSpace = WhiteSpace.Normal } });
                _view = new IMGUIContainer(Draw) { style = { height = 240, marginTop = 2 } };
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
