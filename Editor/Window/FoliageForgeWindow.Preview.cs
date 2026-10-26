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
        private void ScheduleRegenerate()
        {
            if (rootVisualElement == null) return;
            _regen?.Pause();
            _regen = rootVisualElement.schedule.Execute(Regenerate);
            _regen.ExecuteLater(0);
        }

        int _blobSeed = int.MinValue;

        // a new seed grows new branches, so outside Blobs mode the blobs follow the new tips (a fresh recipe keeps its saved ones)
        private void FollowSeed()
        {
            if (_blobSeed != _recipe.Seed && _blobSeed != int.MinValue && !InBlobPanelMode && _recipe.Leaves.Layout == LeafLayout.Blobs)
            {
                _recipe.Blobs = TreeGenerator.SuggestBlobs(_recipe);
                _preview.Selected = -1;
            }
            _blobSeed = _recipe.Seed;
        }

        private void Regenerate()
        {
            if (_grassMode)
            {
                RegenerateGrass();
                return;
            }
            if (_recipe == null || _mesh == null) return;
            FollowSeed();
            _stats = TreeGenerator.Build(_recipe, _mesh);
            DestroyTempMaterials();
            _materials = TreeBaker.PreviewMaterials(_recipe);
            if (_preview.Mode == PreviewMode.Blob)
            {
                RebuildBlobView();
                PaintViewerControls();
            }
            if (_preview.Mode == PreviewMode.Variants) RebuildVariantView();
            RefreshStats();
            RefreshBakeButton();
            _view?.MarkDirtyRepaint();
        }

        readonly System.Collections.Generic.List<Mesh> _variantMeshes = new System.Collections.Generic.List<Mesh>();

        // Variants mode: one mesh per variant, standing in a row (1 m apart, centred on the origin)
        private void RebuildVariantView()
        {
            DestroyVariantMeshes();
            if (_recipe.Variants.Count == 0) _recipe.CreateVariants(); // recipes saved before the default existed
            if (_recipe.Variants.Count == 0)
            {
                _preview.VariantMeshes = null;
                _status.text = "No variants yet: Output tab > Type & Variants > Create 3 Variants.";
                return;
            }
            var offsets = new Vector3[_recipe.Variants.Count];
            float cursor = 0f;
            for (int i = 0; i < offsets.Length; i++)
            {
                TreeRecipe copy = _recipe.VariantRecipe(i);
                var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                TreeGenerator.Build(copy, mesh);
                DestroyImmediate(copy);
                _variantMeshes.Add(mesh);
                float width = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);
                offsets[i] = new Vector3(cursor + width * 0.5f - mesh.bounds.center.x, 0f, 0f);
                cursor += width + 1f;
            }
            for (int i = 0; i < offsets.Length; i++) offsets[i].x -= (cursor - 1f) * 0.5f;
            _preview.VariantMeshes = _variantMeshes.ToArray();
            _preview.VariantOffsets = offsets;
        }

        private void DestroyVariantMeshes()
        {
            foreach (Mesh m in _variantMeshes) if (m != null) DestroyImmediate(m);
            _variantMeshes.Clear();
        }

        private void DestroyTempMaterials()
        {
            foreach (Material m in _materials)
                if (m != null && (m.hideFlags & HideFlags.HideAndDontSave) != 0) DestroyImmediate(m);
            _materials = new Material[2];
        }

        private void DrawPreview()
        {
            Rect rect = _view.contentRect;
            if (_grassMode)
            {
                if (_patch != null) _preview.DrawGrass(new Rect(0f, 0f, rect.width, rect.height), _patch);
                return;
            }
            if (_recipe == null || _mesh == null) return;
            if (_preview.Mode == PreviewMode.Blob && _blobOk && _blobMesh != null)
            {
                var full = new Rect(0f, 0f, rect.width, rect.height);
                _preview.IgnoreRect = BlobViewer.ToolbarRect(full);
                _preview.Draw(full, _blobMesh, _materials, _blobList);
                BlobViewer.DrawToolbar(full, _preview, _blobSummary);
                return;
            }
            _preview.IgnoreRect = default;
            _preview.BlobPad = Mathf.Max(_recipe.Leaves.CardSize.x, _recipe.Leaves.CardSize.y) * 0.45f + _recipe.Leaves.NormalOffset;
            _preview.Draw(new Rect(0f, 0f, rect.width, rect.height), _mesh, _materials, _recipe.Blobs);
        }

        private void RefreshStats()
        {
            _statsBar.Clear();
            BudgetSettings budget = _recipe.Budget;
            bool heavy = _stats.Triangles > budget.MaxTriangles;
            _statsBar.Add(Chip("Triangles", _stats.Triangles.ToString("N0"), heavy ? Warn : Ok));
            _statsBar.Add(Chip("Cards", _stats.Cards.ToString("N0") + (_stats.CardsDropped > 0 ? $"  (-{_stats.CardsDropped} capped)" : string.Empty), _stats.CardsDropped > 0 ? Warn : Ok));
            _statsBar.Add(Chip("Vertices", _stats.Vertices.ToString("N0"), Neutral));
            _statsBar.Add(Chip("Draw calls", "2", Ok));
            _statsBar.Add(Chip("Mesh", $"{_stats.MeshBytes / 1024f:0.#} KB", Neutral));
            _statsBar.Add(Chip("Size", $"{_stats.Size.x:0.0} x {_stats.Size.y:0.0} x {_stats.Size.z:0.0} m", Neutral));
            if (_status != null && heavy) _status.text = $"Over the {budget.MaxTriangles:N0} triangle budget. Lower branch count or radial sides, or turn on Fit Cards To Triangles.";
            else if (_status != null && _status.text.StartsWith("Over the")) _status.text = string.Empty;
        }

        private static VisualElement Chip(string label, string value, Color color)
        {
            var chip = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row, backgroundColor = color, marginRight = 6, marginBottom = 4,
                    paddingLeft = 8, paddingRight = 8, paddingTop = 2, paddingBottom = 2,
                    borderTopLeftRadius = 3, borderTopRightRadius = 3, borderBottomLeftRadius = 3, borderBottomRightRadius = 3,
                },
            };
            chip.Add(new Label(label) { style = { opacity = 0.7f, marginRight = 6, color = Color.white } });
            chip.Add(new Label(value) { style = { unityFontStyleAndWeight = FontStyle.Bold, color = Color.white } });
            return chip;
        }
    }
}
