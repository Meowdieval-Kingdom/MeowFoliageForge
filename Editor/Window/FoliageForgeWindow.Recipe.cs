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
        private void SetRecipe(TreeRecipe recipe)
        {
            if (_recipe != null && !EditorUtility.IsPersistent(_recipe)) DestroyImmediate(_recipe); // drop the old working copy; its asset is untouched
            _source = recipe != null && EditorUtility.IsPersistent(recipe) ? recipe : null;
            _recipe = recipe != null ? WorkingCopy(recipe) : null;
            _preview.Selected = -1;
            _blobSeed = int.MinValue;
            BindRecipe();
            RefreshGallery();
        }

        // every value of the saved recipe, in a temporary object that edits and previews never write back until Bake
        private static TreeRecipe WorkingCopy(TreeRecipe source)
        {
            TreeRecipe copy = Instantiate(source);
            copy.hideFlags = HideFlags.DontSave;
            copy.name = source.name;
            return copy;
        }

        private void BindRecipe()
        {
            if (_inspectorHost == null) return;
            _inspectorHost.Clear();
            if (_recipe == null && _source != null) _recipe = WorkingCopy(_source); // after a script reload the working copy is gone
            if (_recipe == null)
            {
                // no recipe picked: work on a scratch one; baking (or New) saves it as an asset
                _recipe = CreateInstance<TreeRecipe>();
                _recipe.hideFlags = HideFlags.DontSave;
                _recipe.name = TreePreset.Maple.ToString();
                _recipe.ApplyPreset(TreePreset.Maple, TexturedPresets);
            }

            _recipeField?.SetValueWithoutNotify(_source);
            _inspectorHost.Add(_unsaved);
            var inspector = new InspectorElement(_recipe);
            _inspectorHost.Add(inspector);
            var so = new SerializedObject(_recipe);
            inspector.TrackSerializedObjectValue(so, _ => ScheduleRegenerate()); // new element per bind: one element can track only one SerializedObject
            Regenerate();
        }

        // saves a scratch recipe as a new asset and links it; false if cancelled
        private bool EnsureSource()
        {
            if (_recipe == null) return false;
            if (_source != null) return true;
            string dir = TreeBaker.EnsurePath(TreeBaker.OutputFolder + "/Recipes");
            string path = EditorUtility.SaveFilePanelInProject("Save Tree Recipe", _recipe.name, "asset", "Choose where to save the recipe.", dir);
            if (string.IsNullOrEmpty(path)) return false;
            TreeRecipe asset = Instantiate(_recipe);
            asset.hideFlags = HideFlags.None;
            asset.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _source = asset;
            _recipe.name = asset.name;
            _recipeField?.SetValueWithoutNotify(_source);
            return true;
        }

        private void CreateRecipe()
        {
            if (_source == null)
            {
                EnsureSource();
                RefreshGallery();
                RefreshBakeButton();
                return;
            }
            string dir = TreeBaker.EnsurePath(TreeBaker.OutputFolder + "/Recipes");
            string path = EditorUtility.SaveFilePanelInProject("New Tree Recipe", "NewTreeRecipe", "asset", "Choose where to save the recipe.", dir);
            if (string.IsNullOrEmpty(path)) return;
            var recipe = CreateInstance<TreeRecipe>();
            recipe.ApplyPreset(TreePreset.Maple, TexturedPresets);
            AssetDatabase.CreateAsset(recipe, path);
            AssetDatabase.SaveAssets();
            SetRecipe(recipe);
        }

        const string TexturedKey = "FoliageForge.TexturedPresets";

        // presets load without textures unless switched on (kept across sessions)
        static bool TexturedPresets
        {
            get => EditorPrefs.GetBool(TexturedKey, false);
            set => EditorPrefs.SetBool(TexturedKey, value);
        }

        private void ApplyPreset(TreePreset preset, bool textured)
        {
            if (_recipe == null) return;
            Undo.RecordObject(_recipe, "Apply Tree Preset");
            _recipe.ApplyPreset(preset, textured);
            if (_source == null) _recipe.name = preset.ToString(); // the save panel offers the tree's name (Spruce.asset)
            EditorUtility.SetDirty(_recipe);
            BindRecipe();
        }

        private void SwapTextures()
        {
            if (_recipe == null) return;
            Undo.RecordObject(_recipe, "Swap Tree Textures");
            _recipe.SetTextured(_recipe.Look.SolidCards);
            EditorUtility.SetDirty(_recipe);
            BindRecipe();
        }

        private void Reroll()
        {
            if (_recipe == null) return;
            Undo.RecordObject(_recipe, "Reroll Tree Seed");
            _recipe.Seed = UnityEngine.Random.Range(1, 99999);
            EditorUtility.SetDirty(_recipe);
            BindRecipe();
        }
    }
}
