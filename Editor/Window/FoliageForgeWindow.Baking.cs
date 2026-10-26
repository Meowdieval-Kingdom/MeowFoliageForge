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
        private void Bake()
        {
            if (_grassMode)
            {
                BakeGrass();
                return;
            }
            if (_recipe == null) return;
            FollowSeed(); // a seed edited a moment ago has not regenerated yet; bake the blobs that belong to it
            if (!EnsureSource()) return;
            if (TreeBaker.LoadShader() == null)
            {
                _status.text = "The Foliage Forge shader is not imported yet. Wait for the import to finish, then bake again.";
                return;
            }
            // the only place the saved recipe changes: the working copy is written into it, then its prefab is rebuilt
            _recipe.name = _source.name;
            EditorUtility.CopySerialized(_recipe, _source);
            GameObject prefab = TreeBaker.Bake(_source);
            if (prefab == null)
            {
                _status.text = "Could not bake: the recipe is not a saved asset.";
                return;
            }
            _recipe.Look.Bark = _source.Look.Bark; // the bake fills empty material slots on the asset; keep the working copy in step
            _recipe.Look.Leaf = _source.Look.Leaf;
            _status.text = $"Baked {AssetDatabase.GetAssetPath(prefab)}";
            ShowBakedKind(false);
            RefreshGallery();
            Regenerate();
        }

        private void BakeVariants()
        {
            if (_grassMode || _recipe == null) return;
            if (_recipe.Variants.Count == 0)
            {
                _status.text = "No variants yet: Output tab > Type & Variants > Create 3 Variants.";
                return;
            }
            FollowSeed();
            if (!EnsureSource()) return;
            if (TreeBaker.LoadShader() == null)
            {
                _status.text = "The Foliage Forge shader is not imported yet. Wait for the import to finish, then bake again.";
                return;
            }
            _recipe.name = _source.name;
            EditorUtility.CopySerialized(_recipe, _source);
            TreeVariantSet set = TreeBaker.BakeVariants(_source);
            _recipe.Look.Bark = _source.Look.Bark;
            _recipe.Look.Leaf = _source.Look.Leaf;
            _status.text = $"Baked {set.Prefabs.Count} variants, set {AssetDatabase.GetAssetPath(set)}";
            ShowBakedKind(false);
            RefreshGallery();
            Regenerate();
        }

        // Bake only ever writes the linked recipe and its own prefab. The button says whether that prefab exists and whether the working copy is ahead of the saved recipe.
        private void RefreshBakeButton()
        {
            if (_bakeButton == null) return;
            bool scratch = _grassMode ? _grass == null || !EditorUtility.IsPersistent(_grass) : _source == null;
            if (_unsaved != null)
            {
                _unsaved.text = _grassMode
                    ? "Scratch grass profile, not saved yet. Pick a profile above, or save it (Bake also asks where to save it)."
                    : "Scratch tree, not saved yet. Pick a recipe above, or save it (Bake also asks where to save it).";
                _unsaved.style.display = scratch ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_grassMode)
            {
                bool has = !scratch && AssetDatabase.IsValidFolder($"{TreeBaker.OutputFolder}/Grass/{_grass.name}");
                _bakeButton.text = has ? "Overwrite Prefabs" : "Bake Prefabs";
                _bakeButton.tooltip = (scratch ? "Asks where to save this scratch profile first. " : string.Empty)
                    + $"Bakes each enabled grass type as its own circular batch prefab in {TreeBaker.OutputFolder}/Grass/{(_grass != null ? _grass.name : "<profile>")}. Mix batches into tufts with the brush."
                    + (has ? " Batches exist already; edits made since the last bake are not tracked, so Overwrite shows either way." : string.Empty);
                _bakeButton.style.backgroundColor = has ? new Color(0.7f, 0.42f, 0.1f) : new Color(0.2f, 0.45f, 0.3f);
                return;
            }
            string prefabPath = _source != null ? $"{TreeBaker.OutputFolder}/{_source.name}/{_source.name}.prefab" : null;
            bool exists = prefabPath != null && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
            if (!exists)
            {
                _bakeButton.text = "Bake Prefab";
                _bakeButton.tooltip = scratch ? "Asks where to save this scratch recipe, then creates its prefab." : "Creates the prefab for this recipe.";
                _bakeButton.style.backgroundColor = new Color(0.2f, 0.45f, 0.3f);
                return;
            }
            bool changed = _recipe != null && JsonUtility.ToJson(_recipe) != JsonUtility.ToJson(_source);
            _bakeButton.text = changed ? "Overwrite Prefab  ●" : "Prefab Up To Date";
            _bakeButton.tooltip = changed ? $"Unsaved changes. Baking writes them into {AssetDatabase.GetAssetPath(_source)} and overwrites {prefabPath}, nothing else." : $"{prefabPath} matches this recipe.";
            _bakeButton.style.backgroundColor = changed ? new Color(0.7f, 0.42f, 0.1f) : new Color(0.28f, 0.28f, 0.28f);
        }

        private void SaveAsNew()
        {
            if (_grassMode)
            {
                SaveGrassAsNew();
                return;
            }
            if (_recipe == null) return;
            string dir = TreeBaker.EnsurePath(TreeBaker.OutputFolder + "/Recipes");
            string path = EditorUtility.SaveFilePanelInProject("Save As New Tree", _recipe.name + "Copy", "asset", "Name the new recipe. Its prefab is baked under the same name.", dir);
            if (string.IsNullOrEmpty(path)) return;
            TreeRecipe copy = Instantiate(_recipe);
            copy.hideFlags = HideFlags.None;
            copy.name = Path.GetFileNameWithoutExtension(path);
            copy.Look.Bark = null; // the copy gets its own materials instead of re-tinting the original's
            copy.Look.Leaf = null;
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssets();
            SetRecipe(copy);
            Bake();
        }
    }
}
