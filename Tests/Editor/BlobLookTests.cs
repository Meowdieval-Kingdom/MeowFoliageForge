using NUnit.Framework;
using UnityEngine;

namespace Meowdieval.FoliageForge.Tests
{
    public class BlobLookTests
    {
        TreeRecipe _recipe;
        Mesh _mesh;

        [SetUp]
        public void SetUp()
        {
            _recipe = ScriptableObject.CreateInstance<TreeRecipe>();
            _recipe.ApplyPreset(TreePreset.Oak, false);
            _mesh = new Mesh();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_recipe);
            Object.DestroyImmediate(_mesh);
        }

        [Test]
        public void UntexturedOak_GetsTheBlobLook()
        {
            LookSettings k = _recipe.Look;
            Assert.AreEqual(SolidLeafShape.Leaf, k.SolidShape);
            Assert.IsTrue(k.ContinuousColor);
            Assert.IsTrue(k.CoreMesh);
            Assert.AreEqual(3, k.Bands);
        }

        [Test]
        public void CoreMesh_AddsASubmeshWithTriangles()
        {
            TreeGenerator.Build(_recipe, _mesh);
            Assert.AreEqual(3, _mesh.subMeshCount);
            Assert.Greater(_mesh.GetTriangles(2).Length, 0);
            Assert.AreEqual(3, TreeBaker.MaterialSlots(_recipe).Length);
        }

        [Test]
        public void CoreAlign_TopBias_TrimsTheUndersideOfTheCore()
        {
            float LowestCoreY(bool align)
            {
                _recipe.Leaves.PlacementBias = 1f;
                _recipe.Look.CoreAlign = align;
                TreeGenerator.Build(_recipe, _mesh);
                float min = float.MaxValue;
                foreach (int i in _mesh.GetTriangles(2)) min = Mathf.Min(min, _mesh.vertices[i].y);
                return min;
            }
            Assert.Greater(LowestCoreY(true), LowestCoreY(false));
        }

        [Test]
        public void BlobLookOak_StaysInTheGameTriangleBudget()
        {
            TreeGenerator.Build(_recipe, _mesh);
            Assert.LessOrEqual(_mesh.triangles.Length / 3, 3500);
            foreach (int level in new[] { 1, 2 })
            {
                TreeRecipe lod = TreeBaker.LodRecipe(_recipe, level);
                var m = new Mesh();
                TreeGenerator.Build(lod, m);
                Assert.LessOrEqual(m.triangles.Length / 3, level == 1 ? 1500 : 600, "LOD" + level);
                Object.DestroyImmediate(lod);
                Object.DestroyImmediate(m);
            }
        }

        [Test]
        public void EveryPreset_BuildsANonEmptyMeshInTheTriangleBudget()
        {
            foreach (TreePreset p in System.Enum.GetValues(typeof(TreePreset)))
            {
                var r = ScriptableObject.CreateInstance<TreeRecipe>();
                r.ApplyPreset(p, false);
                Assert.AreEqual(p.ToString(), r.TypeId);
                var m = new Mesh();
                TreeGenerator.Build(r, m);
                int tris = m.triangles.Length / 3;
                Assert.Greater(tris, 0, p.ToString());
                Assert.LessOrEqual(tris, 3500, p.ToString());
                r.SetTextured(true);
                Assert.AreEqual(p, r.LastPreset);
                Object.DestroyImmediate(r);
                Object.DestroyImmediate(m);
            }
        }

        [Test]
        public void CoreMesh_Off_KeepsTwoSubmeshes()
        {
            _recipe.Look.CoreMesh = false;
            TreeGenerator.Build(_recipe, _mesh);
            Assert.AreEqual(2, _mesh.subMeshCount);
        }

        [Test]
        public void CoreTriangles_FaceOutward()
        {
            TreeGenerator.Build(_recipe, _mesh);
            Vector3[] v = _mesh.vertices;
            int[] t = _mesh.GetTriangles(2);
            int outward = 0, total = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (n.sqrMagnitude < 1e-12f) continue;
                total++;
                if (Vector3.Dot(n, _mesh.normals[t[i]]) > 0f) outward++;
            }
            Assert.Greater(outward, total * 0.9f);
        }

        [Test]
        public void LeafVertexColour_KeepsLeafFlagAndCarriesBlobAO()
        {
            TreeGenerator.Build(_recipe, _mesh);
            Color32[] c = _mesh.colors32;
            int leaf = 0, darkened = 0;
            foreach (Color32 col in c)
            {
                if (col.b == 0) continue; // wood
                leaf++;
                Assert.GreaterOrEqual(col.b, 128);
                if (col.b < 250) darkened++;
            }
            Assert.Greater(leaf, 0);
            Assert.Greater(darkened, 0, "blob depth should darken some leaf vertices");
        }

        [Test]
        public void LeafMaterial_GetsSolidShapeProperties()
        {
            Shader shader = TreeBaker.LoadShader();
            if (shader == null) Assert.Ignore("foliage shader not imported");
            Material[] mats = TreeBaker.PreviewMaterials(_recipe);
            Assert.AreEqual(1f, mats[1].GetFloat("_Solid"));
            Assert.AreEqual((float)SolidLeafShape.Leaf, mats[1].GetFloat("_SolidShape"));
            Assert.AreEqual(1f, mats[1].GetFloat("_ContinuousColor"));
            Assert.AreEqual(0f, mats[2].GetFloat("_SolidShape"), "the core shell is never masked");
        }
    }
}
