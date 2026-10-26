using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Meowdieval.FoliageForge.Tests
{
    public sealed class TreeRecipeEditorTests
    {
        TreeRecipe _recipe;
        SerializedObject _so;

        [SetUp]
        public void SetUp()
        {
            _recipe = ScriptableObject.CreateInstance<TreeRecipe>();
            _so = new SerializedObject(_recipe);
        }

        [TearDown]
        public void TearDown()
        {
            _so.Dispose();
            Object.DestroyImmediate(_recipe);
        }

        [Test]
        public void test_recipe_editor_every_field_has_exactly_one_group()
        {
            string[] leaves = TreeRecipeEditor.LeafPaths(_so).ToArray();
            Assert.That(leaves, Is.Not.Empty);

            string[] missing = leaves.Where(p => TreeRecipeEditor.CountOwners(p) == 0).ToArray();
            string[] doubled = leaves.Where(p => TreeRecipeEditor.CountOwners(p) > 1).ToArray();

            Assert.That(missing, Is.Empty, "fields without a group: " + string.Join(", ", missing));
            Assert.That(doubled, Is.Empty, "fields in several groups: " + string.Join(", ", doubled));
        }

        [Test]
        public void test_recipe_editor_every_group_path_exists_on_the_recipe()
        {
            string[] unknown = TreeRecipeEditor.AllPaths().Where(p => _so.FindProperty(p) == null).ToArray();

            Assert.That(unknown, Is.Empty, "group paths that match no serialized field: " + string.Join(", ", unknown));
        }

        [Test]
        public void test_recipe_editor_blobs_layout_hides_card_scatter_fields_and_shows_blob_fields()
        {
            Assert.That(TreeRecipeEditor.IsVisible("Leaves.CardsPerBranch", LeafLayout.Blobs, false, 2, 0), Is.False);
            Assert.That(TreeRecipeEditor.IsVisible("Renormalize.Tightness", LeafLayout.Blobs, false, 2, 0), Is.False);
            Assert.That(TreeRecipeEditor.IsVisible("Leaves.BlobCount", LeafLayout.Blobs, false, 2, 0), Is.True);
            Assert.That(TreeRecipeEditor.IsVisible("Leaves.BlobCount", LeafLayout.Clusters, false, 2, 0), Is.False);
            Assert.That(TreeRecipeEditor.IsVisible("Leaves.StrandLength", LeafLayout.Hanging, false, 2, 0), Is.True);
        }
    }
}
