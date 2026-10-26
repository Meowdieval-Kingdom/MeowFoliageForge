using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    public static partial class TreeBaker
    {
        /// <summary>
        /// Bakes every variant of the recipe as <c>{OutputFolder}/{recipe}/{recipe}_V1.prefab</c>, <c>_V2</c>, ... sharing the recipe's two materials,
        /// and writes <c>{recipe}_Variants.asset</c> listing them. Prefabs of variants that no longer exist are deleted; set weights are kept.
        /// </summary>
        /// <returns>The set asset, or null when the recipe is not a saved asset or has no variants.</returns>
        public static TreeVariantSet BakeVariants(TreeRecipe recipe)
        {
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(recipe)) || recipe.Variants.Count == 0) return null;

            string name = recipe.name;
            string folder = EnsurePath($"{OutputFolder}/{name}");
            EnsureMaterials(recipe, folder, name);

            string setPath = $"{folder}/{name}_Variants.asset";
            var set = AssetDatabase.LoadAssetAtPath<TreeVariantSet>(setPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<TreeVariantSet>();
                AssetDatabase.CreateAsset(set, setPath);
            }
            var weights = new List<float>();
            var prefabs = new List<GameObject>();
            for (int i = 0; i < recipe.Variants.Count; i++)
            {
                TreeRecipe copy = recipe.VariantRecipe(i);
                prefabs.Add(BakePrefab(copy, folder, $"{name}_V{i + 1}"));
                Object.DestroyImmediate(copy);
                weights.Add(set.Weight(i));
            }
            for (int i = recipe.Variants.Count + 1; AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{name}_V{i}.prefab") != null; i++)
                foreach (string suffix in new[] { ".prefab", ".asset", "_LOD1.asset", "_LOD2.asset" })
                    AssetDatabase.DeleteAsset($"{folder}/{name}_V{i}{suffix}");

            set.TypeId = recipe.EffectiveTypeId;
            set.Prefabs = prefabs;
            set.Weights = weights;
            EditorUtility.SetDirty(set);
            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssets();
            return set;
        }
    }
}
