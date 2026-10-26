using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Meowdieval.FoliageForge.Tests
{
    public class BlobPlacementTests
    {
        [Test]
        public void GroupUp_TiltZeroIsTheSurfaceNormal()
        {
            Vector3 n = new Vector3(0.3f, 0.8f, -0.2f).normalized;
            Assert.AreEqual(0f, Vector3.Distance(n, TreeGenerator.GroupUp(n, 0f, 1.7f, 0f)), 1e-4f);
        }

        [Test]
        public void GroupUp_TiltNinetyLiesInTheTangentPlane()
        {
            Vector3 n = new Vector3(0.3f, 0.8f, -0.2f).normalized;
            for (int i = 0; i < 6; i++)
            {
                Vector3 a = TreeGenerator.GroupUp(n, 90f, i * 1.1f, 0f);
                Assert.AreEqual(0f, Vector3.Dot(a, n), 1e-4f);
                Assert.AreEqual(1f, a.magnitude, 1e-4f);
            }
        }

        [Test]
        public void GroupUp_SpinTurnsTheTiltAroundTheNormal()
        {
            Vector3 a = TreeGenerator.GroupUp(Vector3.forward, 60f, 0f, 0f);
            Vector3 b = TreeGenerator.GroupUp(Vector3.forward, 60f, Mathf.PI, 0f);
            Assert.AreEqual(Vector3.Dot(a, Vector3.forward), Vector3.Dot(b, Vector3.forward), 1e-4f);
            Assert.Less(Vector3.Dot(a, b), 0.9f);
        }

        [Test]
        public void SizeScales_SpreadToThreeToOneWithTheHighestLargest()
        {
            var mean = new[] { 1f, 1.1f, 0.9f, 1f };
            var height = new[] { 1f, 5f, 2f, 3f };
            float[] s = TreeGenerator.SizeScales(mean, height, 1f);
            var size = new float[4];
            for (int i = 0; i < 4; i++) size[i] = mean[i] * s[i];
            Assert.AreEqual(3f, Mathf.Max(size) / Mathf.Min(size), 0.01f);
            Assert.AreEqual(size[1], Mathf.Max(size), 1e-4f);
        }

        [Test]
        public void SizeScales_ZeroSpreadKeepsSizes()
        {
            float[] s = TreeGenerator.SizeScales(new[] { 1f, 2f }, new[] { 0f, 1f }, 0f);
            Assert.AreEqual(1f, s[0]);
            Assert.AreEqual(1f, s[1]);
        }

        [Test]
        public void TipGrowth_PutsTipsAtLeastAQuarterRadiusInside()
        {
            var blob = new CanopyBlob { Position = Vector3.zero, Radii = Vector3.one };
            var tips = new List<Vector3> { new Vector3(0.95f, 0f, 0f), new Vector3(0f, 0.2f, 0f) };
            float g = TreeGenerator.TipGrowth(blob, tips, 0.75f);
            Assert.Greater(g, 1f);
            blob.Radii *= g;
            Assert.LessOrEqual(0.95f, BlobCage.Extent(blob, Vector3.right) * 0.75f + 1e-3f);
            Assert.AreEqual(1f, TreeGenerator.TipGrowth(blob, new List<Vector3> { new Vector3(0.1f, 0f, 0f) }, 0.75f));
        }

        [Test]
        public void FlatBelly_FlattensTheUndersideAndTaperNarrowsOneEnd()
        {
            var plain = new CanopyBlob { Radii = new Vector3(1f, 1f, 1f) };
            var egg = plain;
            egg.FlatBelly = 1f;
            egg.Taper = 1f;
            Assert.Greater(BlobCage.Extent(plain, Vector3.down), BlobCage.Extent(egg, Vector3.down) * 2f);
            Assert.AreEqual(BlobCage.Extent(plain, Vector3.up), BlobCage.Extent(egg, Vector3.up), 0.3f);
            Vector3 wide = new Vector3(1f, 0f, -1f).normalized, narrow = new Vector3(1f, 0f, 1f).normalized;
            Assert.Greater(BlobCage.Extent(egg, wide), BlobCage.Extent(egg, narrow));
        }

        [Test]
        public void ShapedBlob_NormalsAreUnitAndPointOutward()
        {
            var blob = new CanopyBlob { Position = new Vector3(1f, 2f, 3f), Radii = new Vector3(1f, 0.8f, 1.4f), Rotation = new Vector3(10f, 40f, 0f), FlatBelly = 0.7f, Taper = 0.5f };
            for (int i = 0; i < 40; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                Vector3 n = TreeGenerator.BlobSurfaceNormal(blob, dir);
                Assert.AreEqual(1f, n.magnitude, 1e-3f);
                Assert.Greater(Vector3.Dot(n, dir), 0f);
            }
            Assert.Greater(Vector3.Dot(TreeGenerator.BlobSurfaceNormal(new CanopyBlob { Radii = Vector3.one, FlatBelly = 1f }, Vector3.down), Vector3.down), 0.99f);
        }
    }
}
