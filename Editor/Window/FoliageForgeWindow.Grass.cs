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
        private void SetGrassMode(bool on)
        {
            _grassMode = on;
            DisplayStyle tree = on ? DisplayStyle.None : DisplayStyle.Flex;
            _treeTools.style.display = _treePresets.style.display = _modeRow.style.display = tree;
            _grassTools.style.display = _grassPresets.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            _blobPanel.style.display = !on && InBlobPanelMode ? DisplayStyle.Flex : DisplayStyle.None;
            FoliageUi.SetChipOn(_tabTrees, !on);
            FoliageUi.SetChipOn(_tabGrass, on);
            if (_status != null) _status.text = string.Empty;
            if (on) BindGrass();
            else BindRecipe();
            RefreshBakeButton();
            RefreshGallery();
        }

        private void SetGrass(GrassProfile profile)
        {
            if (_grass != null && !EditorUtility.IsPersistent(_grass)) DestroyImmediate(_grass);
            _grass = profile;
            BindGrass();
            RefreshBakeButton();
        }

        // the profile asset is edited in place; with none picked a scratch one stands in until New saves it
        private void BindGrass()
        {
            if (_inspectorHost == null) return;
            _inspectorHost.Clear();
            if (_grass == null)
            {
                _grass = CreateInstance<GrassProfile>();
                _grass.hideFlags = HideFlags.DontSave;
                _grass.name = "NewGrass";
            }
            _grassField?.SetValueWithoutNotify(EditorUtility.IsPersistent(_grass) ? _grass : null);
            _inspectorHost.Add(_unsaved);
            _inspectorHost.Add(new InspectorElement(_grass));
            RegenerateGrass();
        }

        // saves a copy of the current profile as a new asset; null if cancelled
        private GrassProfile SaveGrassCopy(string title, string defaultName)
        {
            string dir = TreeBaker.EnsurePath(TreeBaker.OutputFolder + "/Grass");
            string path = EditorUtility.SaveFilePanelInProject(title, defaultName, "asset", "Choose where to save the profile. Its batches are baked into a folder of the same name.", dir);
            if (string.IsNullOrEmpty(path)) return null;
            GrassProfile asset = _grass != null ? Instantiate(_grass) : CreateInstance<GrassProfile>();
            asset.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private void CreateGrass()
        {
            GrassProfile asset = SaveGrassCopy("New Grass Profile", "NewGrassProfile");
            if (asset != null) SetGrass(asset);
        }

        // one prefab per enabled grass type, so the brush can mix them into tufts
        private void BakeGrass()
        {
            if (_grass == null) return;
            if (!EditorUtility.IsPersistent(_grass))
            {
                GrassProfile asset = SaveGrassCopy("Save Grass Profile", _grass.name);
                if (asset == null) return;
                SetGrass(asset);
            }
            GameObject[] baked = GrassPatchBaker.Bake(_grass, _bakeSize);
            _status.text = baked.Length > 0
                ? $"Baked {baked.Length} grass batches into {TreeBaker.OutputFolder}/Grass/{_grass.name}"
                : "Nothing to bake: enable a variant and make sure the profile has a shader.";
            ShowBakedKind(true);
            RefreshGallery();
            RefreshBakeButton();
        }

        private void SaveGrassAsNew()
        {
            if (_grass == null) return;
            GrassProfile copy = SaveGrassCopy("Save As New Grass", _grass.name + "Copy");
            if (copy == null) return;
            SetGrass(copy);
            BakeGrass();
        }

        private void RegenerateGrass()
        {
            if (_grass == null || _patch == null) return;
            _grassRevision = _grass.Revision;
            _patch.Build(_grass);
            _statsBar.Clear();
            int on = _grass.Features.EnabledCount();
            _statsBar.Add(FoliageUi.StatChip("Preview clumps", _patch.ClumpCount.ToString("N0"), StatKind.Ok));
            _statsBar.Add(FoliageUi.StatChip("Triangles", _patch.Triangles.ToString("N0")));
            _statsBar.Add(FoliageUi.StatChip("Per metre", $"{_patch.ClumpCount / (_patch.Size * _patch.Size):0.0} clumps/m²"));
            _statsBar.Add(FoliageUi.StatChip("Features on", on.ToString()));
            _statsBar.Add(FoliageUi.StatChip("Draw distance", $"{_grass.DrawDistance:0} m"));
            var note = new Label("Flat preview patch. Trample, ground map, additional lights and shadows only show in the scene.");
            note.AddToClassList("ff-muted");
            note.AddToClassList("ff-hint");
            _statsBar.Add(note);
            _view?.MarkDirtyRepaint();
        }
    }
}
