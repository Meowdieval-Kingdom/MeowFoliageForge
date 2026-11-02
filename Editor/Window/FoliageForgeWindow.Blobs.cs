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
        // the blob panel serves both blob modes: editing the set (Blobs) and viewing one blob alone (Blob)
        bool InBlobPanelMode => _preview.Mode == PreviewMode.Blobs || _preview.Mode == PreviewMode.Blob;

        private void SetMode(PreviewMode mode)
        {
            // the selection carries between the two blob modes and is dropped when leaving them
            bool keep = (mode == PreviewMode.Blobs || mode == PreviewMode.Blob) && InBlobPanelMode;
            int selected = _preview.Selected;
            _preview.Mode = mode;
            _preview.Selected = keep ? selected : -1;
            foreach (Action paint in _modePaint) paint();
            _blobPanel.style.display = InBlobPanelMode ? DisplayStyle.Flex : DisplayStyle.None;
            if (mode == PreviewMode.Blobs || mode == PreviewMode.Blob) UseBlobLayout();
            else Regenerate();
            if (mode == PreviewMode.Blob)
            {
                if (_recipe.Blobs.Count == 0)
                {
                    SetMode(PreviewMode.Blobs);
                    return;
                }
                if (_preview.Selected < 0) _preview.Selected = 0;
                RebuildBlobView();
            }
            PaintViewerControls();
            _view?.MarkDirtyRepaint();
        }

        // the blobs only shape the tree in the Blobs layout, so entering the mode switches to it and fills an empty list
        private void UseBlobLayout()
        {
            if (_recipe.Leaves.Layout == LeafLayout.Blobs && _recipe.Blobs.Count > 0) return;
            Undo.RecordObject(_recipe, "Use Canopy Blobs");
            _recipe.Leaves.Layout = LeafLayout.Blobs;
            if (_recipe.Blobs.Count == 0) _recipe.Blobs = TreeGenerator.SuggestBlobs(_recipe);
            EditorUtility.SetDirty(_recipe);
            BindRecipe();
        }

        Vector3Field _positionField, _radiiField, _rotationField;
        VisualElement _blobSelected;

        private static VisualElement BlobRow() => FoliageUi.WrapRow();

        // a Vector3 field that writes one property of the selected blob, undoably
        private Vector3Field BlobVector(string label, string tooltip, System.Action<CanopyBlobRef, Vector3> set)
        {
            var field = new Vector3Field(label) { tooltip = tooltip };
            field.AddToClassList("ff-vector");
            field.labelElement.AddToClassList("ff-input-label");
            field.RegisterValueChangedCallback(e =>
            {
                if (_recipe == null || _preview.Selected < 0 || _preview.Selected >= _recipe.Blobs.Count) return;
                Undo.RecordObject(_recipe, "Edit Blob");
                var r = new CanopyBlobRef { Blob = _recipe.Blobs[_preview.Selected] };
                set(r, e.newValue);
                _recipe.Blobs[_preview.Selected] = r.Blob;
                EditorUtility.SetDirty(_recipe);
                ScheduleRegenerate();
                _view?.MarkDirtyRepaint();
            });
            return field;
        }

        // CanopyBlob is a struct; the setters above edit it through this box
        sealed class CanopyBlobRef
        {
            public CanopyBlob Blob;
            public Vector3 Position { set => Blob.Position = value; }
            public Vector3 Radii { set => Blob.Radii = value; }
            public Vector3 Rotation { set => Blob.Rotation = value; }
        }

        // third blob panel row: open the single-blob viewer, step through blobs, and the leaf values that shape one blob's groups

        private void RebuildBlobView()
        {
            _blobList.Clear();
            _blobOk = false;
            if (_recipe == null || _blobMesh == null) return;
            _blobOk = BlobViewer.Build(_recipe, _preview.Selected, _blobMesh, out TreeStats stats, out CanopyBlob blob);
            if (_blobOk)
            {
                _blobList.Add(blob);
                _blobSummary = BlobViewer.Summary(_recipe, stats, _blobMesh);
            }
            _preview.MeshVersion++;
        }

        // shows the selected blob's panel and values; follows the drags and Alt + scroll edits made in the preview
        private void SyncBlobFields()
        {
            foreach (Action sync in _viewerSync) sync();
            if (_preview.Selected >= 0) BlobViewer.PreferredBlob = _preview.Selected;
            bool has = _recipe != null && _preview.Selected >= 0 && _preview.Selected < _recipe.Blobs.Count;
            _blobSelected.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
            if (!has) return;
            CanopyBlob blob = _recipe.Blobs[_preview.Selected];
            Sync(_positionField, blob.Position);
            Sync(_radiiField, blob.Radii);
            Sync(_rotationField, blob.Rotation);
        }

        private static void Sync(Vector3Field field, Vector3 value)
        {
            bool typing = field.focusController?.focusedElement is VisualElement fe && field.Contains(fe);
            if (!typing) field.SetValueWithoutNotify(value);
        }

        private void AutoBlobs()
        {
            if (_recipe == null) return;
            Undo.RecordObject(_recipe, "Auto Blobs");
            _recipe.Leaves.Layout = LeafLayout.Blobs;
            _recipe.Blobs = TreeGenerator.SuggestBlobs(_recipe);
            _preview.Selected = -1;
            EditorUtility.SetDirty(_recipe);
            BindRecipe();
        }

        // quick-iteration roll: settings live in the window, not the recipe
        float _randCount = 7, _randSize = 0.9f, _randSpread = 0.8f, _randFlat = 0.75f;

        private void RandomizeBlobs()
        {
            if (_recipe == null) return;
            Undo.RecordObject(_recipe, "Randomize Blobs");
            _recipe.Leaves.Layout = LeafLayout.Blobs;
            _recipe.Blobs = TreeGenerator.RandomBlobs(_recipe, Mathf.RoundToInt(_randCount), _randSize, _randSpread, _randFlat, UnityEngine.Random.Range(1, int.MaxValue));
            _preview.Selected = -1;
            EditorUtility.SetDirty(_recipe);
            BindRecipe();
        }

        private void AddBlob()
        {
            if (_recipe == null) return;
            Undo.RecordObject(_recipe, "Add Canopy Blob");
            _recipe.Leaves.Layout = LeafLayout.Blobs;
            _recipe.Blobs.Add(new CanopyBlob { Position = new Vector3(0f, _recipe.Trunk.Height, 0f), Radii = Vector3.one * 0.8f });
            _preview.Selected = _recipe.Blobs.Count - 1;
            EditorUtility.SetDirty(_recipe);
            BindRecipe();
        }

        private void ResetShape()
        {
            if (_recipe == null || _preview.Selected < 0 || _preview.Selected >= _recipe.Blobs.Count) return;
            Undo.RecordObject(_recipe, "Reset Blob Shape");
            CanopyBlob blob = _recipe.Blobs[_preview.Selected];
            blob.Deform = null;
            _recipe.Blobs[_preview.Selected] = blob;
            EditorUtility.SetDirty(_recipe);
            BindRecipe();
        }

        private void DeleteBlob()
        {
            if (_recipe == null || _preview.Selected < 0 || _preview.Selected >= _recipe.Blobs.Count) return;
            Undo.RecordObject(_recipe, "Delete Canopy Blob");
            _recipe.Blobs.RemoveAt(_preview.Selected);
            _preview.Selected = -1;
            EditorUtility.SetDirty(_recipe);
            BindRecipe();
        }
    }
}
