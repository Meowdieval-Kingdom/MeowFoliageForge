using NUnit.Framework;
using UnityEngine;

namespace Meowdieval.FoliageForge.Tests
{
    public class GrassGpuMathTests
    {
        private static Matrix4x4 ViewProj()
        {
            Matrix4x4 view = Matrix4x4.TRS(new Vector3(0f, 0f, -10f), Quaternion.identity, new Vector3(1f, 1f, -1f)).inverse; // camera at z = -10 looking +z
            return Matrix4x4.Perspective(60f, 1f, 0.3f, 100f) * view;
        }

        [Test]
        public void FrustumPlanes_PointInFront_IsInsideAllPlanes()
        {
            var planes = new Vector4[6];
            GrassGpuMath.FrustumPlanes(ViewProj(), planes);
            foreach (Vector4 p in planes) Assert.GreaterOrEqual(GrassGpuMath.PlaneDistance(p, new Vector3(0f, 0f, 5f)), 0f);
        }

        [Test]
        public void FrustumPlanes_PointBehindCamera_IsOutsideNearPlane()
        {
            var planes = new Vector4[6];
            GrassGpuMath.FrustumPlanes(ViewProj(), planes);
            Assert.Less(GrassGpuMath.PlaneDistance(planes[4], new Vector3(0f, 0f, -20f)), 0f);
        }

        [Test]
        public void FrustumPlanes_FarSidePoint_IsOutsideLeftPlane()
        {
            var planes = new Vector4[6];
            GrassGpuMath.FrustumPlanes(ViewProj(), planes);
            Assert.Less(GrassGpuMath.PlaneDistance(planes[0], new Vector3(-50f, 0f, 0f)), 0f);
        }

        [Test]
        public void FrustumPlanes_AgreesWithUnityPlanes()
        {
            var planes = new Vector4[6];
            Matrix4x4 vp = ViewProj();
            GrassGpuMath.FrustumPlanes(vp, planes);
            Plane[] unity = GeometryUtility.CalculateFrustumPlanes(vp);
            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(unity[i].normal.x, planes[i].x, 1e-4f);
                Assert.AreEqual(unity[i].normal.y, planes[i].y, 1e-4f);
                Assert.AreEqual(unity[i].normal.z, planes[i].z, 1e-4f);
                Assert.AreEqual(unity[i].distance, planes[i].w, 1e-3f);
            }
        }

        [Test]
        public void WorldToUv_MapsCornersAndCentre()
        {
            Vector4 w = GrassGpuMath.Window(new Rect(-10f, 20f, 40f, 20f));
            Assert.AreEqual(Vector2.zero, GrassGpuMath.WorldToUv(w, -10f, 20f));
            Assert.AreEqual(Vector2.one, GrassGpuMath.WorldToUv(w, 30f, 40f));
            Assert.AreEqual(new Vector2(0.5f, 0.5f), GrassGpuMath.WorldToUv(w, 10f, 30f));
        }

        [Test]
        public void CellsPerSide_CoversRadiusWithSpare()
        {
            Assert.AreEqual(2 * 30 + 2, GrassGpuMath.CellsPerSide(60f, 2f));
            Assert.AreEqual(2 * 4 + 2, GrassGpuMath.CellsPerSide(7f, 2f));
        }

        [Test]
        public void GridOrigin_CentresOnCameraCell()
        {
            int side = GrassGpuMath.CellsPerSide(10f, 2f);
            int origin = GrassGpuMath.GridOrigin(-1f, 2f, side);
            Assert.AreEqual(-1 - side / 2, origin);
            Assert.AreEqual(-1, GrassGpuMath.CellOf(-1f, 2f));
        }

        [Test]
        public void PerCell_RoundsUp_AndAcceptRestoresExpectedCount()
        {
            int per = GrassGpuMath.PerCell(1.3f, 2f);
            Assert.AreEqual(6, per);
            Assert.AreEqual(1.3f * 4f, per * GrassGpuMath.Accept(1.3f, 2f, per), 1e-4f);
            Assert.AreEqual(1, GrassGpuMath.PerCell(0f, 2f));
        }

        [Test]
        public void FitRadius_ShrinksUntilUnderCap()
        {
            float r = GrassGpuMath.FitRadius(200f, 1f, 20, 100000);
            Assert.LessOrEqual(GrassGpuMath.TotalCandidates(GrassGpuMath.CellsPerSide(r, 1f), 20), 100000);
            Assert.AreEqual(50f, GrassGpuMath.FitRadius(50f, 1f, 1, 100000), 1e-4f);
        }

        [Test]
        public void DispatchGroups_SplitsPastAxisLimit()
        {
            GrassGpuMath.DispatchGroups(64, out int x, out int y);
            Assert.AreEqual(1, x);
            Assert.AreEqual(1, y);
            GrassGpuMath.DispatchGroups(65L * 65535 * 64, out x, out y);
            Assert.AreEqual(65535, x);
            Assert.AreEqual(65, y);
            Assert.GreaterOrEqual((long)x * y * GrassGpuMath.GroupSize, 65L * 65535 * 64);
        }

        [Test]
        public void Args_LayoutMatchesIndirectDrawIndexedArgs()
        {
            uint[] a = GrassGpuMath.Args(36, 1234);
            Assert.AreEqual(36u, a[GrassGpuMath.ArgIndexCount]);
            Assert.AreEqual(1234u, a[GrassGpuMath.ArgInstanceCount]);
            Assert.AreEqual(0u, a[GrassGpuMath.ArgStartInstance]);
            Assert.AreEqual(GrassGpuMath.ArgsBytes, a.Length * sizeof(uint));
            Assert.AreEqual(GrassGpuMath.ArgsBytes, System.Runtime.InteropServices.Marshal.SizeOf<GraphicsBuffer.IndirectDrawIndexedArgs>());
            Assert.AreEqual(32, GrassGpuMath.BladeStride);
        }
    }
}
