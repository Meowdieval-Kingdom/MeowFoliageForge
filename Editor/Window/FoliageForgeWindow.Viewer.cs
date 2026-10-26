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
    public sealed partial class FoliageForgeWindow
    {
        private VisualElement BuildViewerRow()
        {
            VisualElement row = BlobRow();
            row.style.marginTop = 4;
            row.style.paddingTop = 4;
            row.style.borderTopWidth = 1;
            row.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
            row.Add(new Label("Blob viewer") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginRight = 8 } });
            _viewerToggle = new Button(() => SetMode(_preview.Mode == PreviewMode.Blob ? PreviewMode.Blobs : PreviewMode.Blob));
            row.Add(_viewerToggle);

            _viewerNav = BlobRow();
            _viewerNavLabel = new Label { style = { marginLeft = 6, marginRight = 6 } };
            _viewerNav.Add(new Button(() => StepViewedBlob(-1)) { text = "<", tooltip = "Previous blob." });
            _viewerNav.Add(_viewerNavLabel);
            _viewerNav.Add(new Button(() => StepViewedBlob(1)) { text = ">", tooltip = "Next blob." });
            row.Add(_viewerNav);

            row.Add(ViewerInt("Groups", "Leaf groups in each blob (Cards Per Blob).", 0, 400, l => l.CardsPerBlob, (l, v) => l.CardsPerBlob = v));
            row.Add(ViewerInt("Quads", "Quads in each group, fanned around the group's up axis.", 1, 6, l => l.GroupQuads, (l, v) => l.GroupQuads = v));
            row.Add(ViewerFloat("Depth", "Group Depth.", 0f, 0.5f, l => l.GroupDepth, (l, v) => l.GroupDepth = v));
            row.Add(ViewerFloat("Jitter", "Group Jitter.", 0f, 1f, l => l.GroupJitter, (l, v) => l.GroupJitter = v));
            row.Add(ViewerFloat("Tilt", "Group Tilt: degrees between a group's axis and the surface normal. 0 = fins, 90 = lying on the surface.", 0f, 90f, l => l.GroupTilt, (l, v) => l.GroupTilt = v));
            row.Add(ViewerFloat("Tilt jitter", "Group Tilt Jitter: random +- degrees per group.", 0f, 90f, l => l.GroupTiltJitter, (l, v) => l.GroupTiltJitter = v));
            row.Add(ViewerFloat("Smoothness", "Smoothness.", 0f, 1f, l => l.Smoothness, (l, v) => l.Smoothness = v));
            row.Add(ViewerFloat("Offset", "Normal Offset: pushes every group out of its blob along the blob normal, in metres.", 0f, 0.5f, l => l.NormalOffset, (l, v) => l.NormalOffset = v));

            var size = new Vector2Field("Card size") { tooltip = "Card Size: width and height of every quad, in metres. Global to all blobs.", style = { minWidth = 220, marginRight = 8 } };
            size.RegisterValueChangedCallback(e => EditLeaves("Edit Card Size", l => l.CardSize = e.newValue));
            _viewerSync.Add(() =>
            {
                if (_recipe != null && !IsTyping(size)) size.SetValueWithoutNotify(_recipe.Leaves.CardSize);
            });
            row.Add(size);

            row.Add(new Label("Leaf settings are global: every blob uses the same ones.") { style = { opacity = 0.65f, marginLeft = 4 } });
            PaintViewerControls();
            return row;
        }

        private SliderInt ViewerInt(string label, string tooltip, int min, int max, Func<LeafSettings, int> get, Action<LeafSettings, int> set)
        {
            var s = new SliderInt(label, min, max) { tooltip = tooltip, showInputField = true, style = { width = 190, marginRight = 8 } };
            s.labelElement.style.minWidth = 48;
            s.labelElement.style.width = 48;
            s.RegisterValueChangedCallback(e => EditLeaves("Edit " + label, l => set(l, e.newValue)));
            _viewerSync.Add(() =>
            {
                if (_recipe != null && !IsTyping(s)) s.SetValueWithoutNotify(get(_recipe.Leaves));
            });
            return s;
        }

        private Slider ViewerFloat(string label, string tooltip, float min, float max, Func<LeafSettings, float> get, Action<LeafSettings, float> set)
        {
            var s = new Slider(label, min, max) { tooltip = tooltip, showInputField = true, style = { width = 190, marginRight = 8 } };
            s.labelElement.style.minWidth = 48;
            s.labelElement.style.width = 48;
            s.RegisterValueChangedCallback(e => EditLeaves("Edit " + label, l => set(l, e.newValue)));
            _viewerSync.Add(() =>
            {
                if (_recipe != null && !IsTyping(s)) s.SetValueWithoutNotify(get(_recipe.Leaves));
            });
            return s;
        }

        // writes to the real recipe, undoably; the viewer follows through the regenerate
        private void EditLeaves(string undoName, Action<LeafSettings> apply)
        {
            if (_recipe == null) return;
            Undo.RecordObject(_recipe, undoName);
            apply(_recipe.Leaves);
            EditorUtility.SetDirty(_recipe);
            ScheduleRegenerate();
            _view?.MarkDirtyRepaint();
        }

        private static bool IsTyping(VisualElement field) => field.focusController?.focusedElement is VisualElement fe && field.Contains(fe);

        private void StepViewedBlob(int delta)
        {
            if (_recipe == null || _recipe.Blobs.Count == 0) return;
            int n = _recipe.Blobs.Count;
            _preview.Selected = ((Mathf.Max(0, _preview.Selected) + delta) % n + n) % n;
            RebuildBlobView();
            PaintViewerControls();
            _view?.MarkDirtyRepaint();
        }

        private void PaintViewerControls()
        {
            if (_viewerToggle == null) return;
            bool viewing = _preview.Mode == PreviewMode.Blob;
            _viewerToggle.text = viewing ? "Full Tree" : "Blob Viewer";
            _viewerToggle.tooltip = viewing ? "Back to the full tree with its blobs." : "Show the selected blob (the first when none is selected) alone at the origin.";
            _viewerNav.style.display = viewing ? DisplayStyle.Flex : DisplayStyle.None;
            if (viewing && _recipe != null) _viewerNavLabel.text = $"Blob {Mathf.Max(0, _preview.Selected) + 1} / {_recipe.Blobs.Count}";
        }

        // generates the viewed blob alone into _blobMesh; the Blob preview mode draws that instead of the tree
    }
}
