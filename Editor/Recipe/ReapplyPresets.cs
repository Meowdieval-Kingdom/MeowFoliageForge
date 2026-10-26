using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Re-applies each tree recipe's preset so existing recipes pick up preset changes.</summary>
    public static class ReapplyPresets
    {
        [MenuItem("Meowdieval/Foliage Forge/Re-apply Presets To Recipes", false, 5)]
        private static void Run()
        {
            var recipes = new List<TreeRecipe>();
            foreach (string guid in AssetDatabase.FindAssets("t:TreeRecipe"))
            {
                var recipe = AssetDatabase.LoadAssetAtPath<TreeRecipe>(AssetDatabase.GUIDToAssetPath(guid));
                if (recipe != null) recipes.Add(recipe);
            }

            if (recipes.Count == 0)
            {
                EditorUtility.DisplayDialog("Re-apply presets", "No tree recipes found.", "OK");
                return;
            }

            string names = string.Join("\n", recipes.ConvertAll(r => $"{r.name} ({r.LastPreset})"));
            if (!EditorUtility.DisplayDialog("Re-apply presets", $"Reset these recipes to their preset? Hand-tuned numbers and blobs are overwritten; textures, materials and variants are kept.\n\n{names}", "Re-apply", "Cancel")) return;

            foreach (TreeRecipe recipe in recipes)
            {
                Undo.RecordObject(recipe, "Re-apply preset");
                recipe.ApplyPreset(recipe.LastPreset, !recipe.Look.SolidCards);
                EditorUtility.SetDirty(recipe);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Foliage Forge: re-applied presets to {recipes.Count} recipes.");
        }
    }
}
