using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Meowdieval.FoliageForge.Tests
{
    public class TreeVariantTests
    {
        const string Folder = "Assets/__FoliageForgeVariantTest";

        string _savedOutput;

        [SetUp]
        public void SetUp()
        {
            _savedOutput = TreeBaker.OutputFolder;
            TreeBaker.OutputFolder = Folder + "/Out";
        }

        [TearDown]
        public void TearDown()
        {
            TreeBaker.OutputFolder = _savedOutput;
            AssetDatabase.DeleteAsset(Folder);
        }

        private static TreeRecipe Make(TreePreset preset)
        {
            var r = ScriptableObject.CreateInstance<TreeRecipe>();
            r.ApplyPreset(preset);
            return r;
        }

        private static int Hash(Mesh m) => m.vertices.Aggregate(17, (h, v) => h * 31 + v.GetHashCode());

        [Test]
        public void test_type_id_defaults_to_preset_name_and_keeps_a_typed_name()
        {
            foreach (TreePreset p in System.Enum.GetValues(typeof(TreePreset)))
            {
                TreeRecipe r = Make(p);
                Assert.AreEqual(p.ToString(), r.TypeId);
                Object.DestroyImmediate(r);
            }
            TreeRecipe typed = Make(TreePreset.Oak);
            typed.TypeId = "Grove";
            typed.ApplyPreset(TreePreset.Maple);
            Assert.AreEqual("Grove", typed.TypeId);
            Object.DestroyImmediate(typed);
        }

        [Test]
        public void test_type_id_follows_preset_until_edited()
        {
            TreeRecipe r = Make(TreePreset.Oak);
            r.ApplyPreset(TreePreset.Spruce);
            Assert.AreEqual("Spruce", r.TypeId);
            Object.DestroyImmediate(r);
        }

        [Test]
        public void test_variant_recipe_does_not_mutate_the_recipe()
        {
            TreeRecipe r = Make(TreePreset.Oak);
            r.Leaves.Layout = LeafLayout.Blobs;
            r.CreateVariants();
            string before = JsonUtility.ToJson(r);
            TreeRecipe copy = r.VariantRecipe(2);
            Assert.AreEqual(before, JsonUtility.ToJson(r));
            Assert.AreEqual(r.Variants[2].Seed, copy.Seed);
            Assert.AreEqual(r.Leaves.BlobCount + 1, copy.Leaves.BlobCount);
            Assert.Greater(copy.Trunk.Height, r.Trunk.Height);
            Object.DestroyImmediate(copy);
            Object.DestroyImmediate(r);
        }

        [Test]
        public void test_variant_blob_count_never_drops_below_one()
        {
            TreeRecipe r = Make(TreePreset.Oak);
            r.Leaves.BlobCount = 1;
            r.CreateVariants();
            TreeRecipe copy = r.VariantRecipe(1);
            Assert.AreEqual(1, copy.Leaves.BlobCount);
            Object.DestroyImmediate(copy);
            Object.DestroyImmediate(r);
        }

        [Test]
        public void test_variant_seeds_produce_different_meshes()
        {
            TreeRecipe r = Make(TreePreset.Oak);
            r.CreateVariants();
            Assert.AreEqual(r.Variants.Count, r.Variants.Select(v => v.Seed).Distinct().Count());
            var hashes = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < r.Variants.Count; i++)
            {
                TreeRecipe copy = r.VariantRecipe(i);
                var mesh = new Mesh();
                TreeGenerator.Build(copy, mesh);
                hashes.Add(Hash(mesh));
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(copy);
            }
            Assert.AreEqual(r.Variants.Count, hashes.Count);
            Object.DestroyImmediate(r);
        }

        [Test]
        public void test_bake_variants_writes_prefabs_and_set_asset()
        {
            AssetDatabase.CreateFolder("Assets", "__FoliageForgeVariantTest");
            TreeRecipe recipe = Make(TreePreset.Oak);
            recipe.CreateVariants();
            AssetDatabase.CreateAsset(recipe, Folder + "/Oak.asset");

            TreeVariantSet set = TreeBaker.BakeVariants(recipe);
            Assert.IsNotNull(set);
            Assert.AreEqual("Oak", set.TypeId);
            Assert.AreEqual(3, set.Prefabs.Count);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual($"{Folder}/Out/Oak/Oak_V{i + 1}.prefab", AssetDatabase.GetAssetPath(set.Prefabs[i]));
                Assert.AreEqual(1f, set.Weight(i));
            }
            Assert.AreEqual($"{Folder}/Out/Oak/Oak_Variants.asset", AssetDatabase.GetAssetPath(set));
            var mats = set.Prefabs.Select(p => p.GetComponent<MeshRenderer>().sharedMaterials[1]).Distinct().ToArray();
            Assert.AreEqual(1, mats.Length, "variants share the recipe's materials");
            Assert.IsNotNull(set.Pick(new System.Random(3)));

            recipe.Variants.RemoveAt(2);
            set = TreeBaker.BakeVariants(recipe);
            Assert.AreEqual(2, set.Prefabs.Count);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Out/Oak/Oak_V3.prefab"), "stale variant removed");
        }
    }
}
