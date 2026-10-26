using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Meowdieval.FoliageForge.Tests
{
    public class BlobViewerTests
    {
        const int Groups = 10;

        TreeRecipe _recipe;
        Mesh _mesh;

        [SetUp]
        public void SetUp()
        {
            _recipe = ScriptableObject.CreateInstance<TreeRecipe>();
            _recipe.Leaves.Layout = LeafLayout.Blobs;
            _recipe.Leaves.CardsPerBlob = Groups;
            _recipe.Leaves.Intersecting = false;
            _recipe.Blobs = new List<CanopyBlob>
            {
                new CanopyBlob { Position = new Vector3(1f, 3f, 2f), Radii = new Vector3(1f, 0.8f, 1f) },
                new CanopyBlob { Position = new Vector3(-2f, 4f, 0f), Radii = Vector3.one },
            };
            _mesh = new Mesh();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_mesh);
            Object.DestroyImmediate(_recipe);
        }

        [Test]
        public void test_blob_viewer_build_leaves_source_recipe_unmodified()
        {
            string before = JsonUtility.ToJson(_recipe);
            bool ok = BlobViewer.Build(_recipe, 0, _mesh, out _, out CanopyBlob blob);
            Assert.IsTrue(ok);
            Assert.AreEqual(before, JsonUtility.ToJson(_recipe));
            Assert.AreEqual(2, _recipe.Blobs.Count);
            Assert.AreEqual(new Vector3(1f, 3f, 2f), _recipe.Blobs[0].Position, "the source blob keeps its place");
            Assert.AreEqual(Vector3.zero, blob.Position, "the viewed blob is recentred");
        }

        [Test]
        public void test_blob_viewer_build_without_blobs_returns_false()
        {
            _recipe.Blobs.Clear();
            Assert.IsFalse(BlobViewer.Build(_recipe, 0, _mesh, out _, out _));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void test_blob_viewer_quad_count_is_groups_times_group_quads(int groupQuads)
        {
            _recipe.Leaves.GroupQuads = groupQuads;
            Assert.IsTrue(BlobViewer.Build(_recipe, 0, _mesh, out _, out _));
            Assert.AreEqual(2, _mesh.subMeshCount, "wood and leaves");
            int quads = (int)(_mesh.GetIndexCount(1) / 6);
            Assert.AreEqual(Groups * groupQuads, quads);
        }

        [Test]
        public void test_blob_viewer_build_destroys_its_temporary_recipe()
        {
            int before = Resources.FindObjectsOfTypeAll<TreeRecipe>().Length;
            BlobViewer.Build(_recipe, 1, _mesh, out _, out _);
            BlobViewer.Build(_recipe, 5, _mesh, out _, out _); // out of range clamps to the last blob
            Assert.AreEqual(before, Resources.FindObjectsOfTypeAll<TreeRecipe>().Length);
        }
    }
}
