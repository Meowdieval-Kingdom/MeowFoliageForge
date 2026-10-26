using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Pure helpers for <see cref="GrassGpuField"/>: frustum planes, map windows, cell grid sizing, dispatch and args layout.</summary>
    public static class GrassGpuMath
    {
        /// <summary>Flag bits shared with GrassPlacement.compute.</summary>
        public const uint FlagDensityTex = 1, FlagHeightScale = 2, FlagTypeFromB = 4, FlagHeightTex = 8, FlagHiZ = 16, FlagFrustum = 32, FlagDistanceThin = 64;

        /// <summary>Bytes per Blade record (float3 pos, yaw, height, width, seed, type). Matches the compute and HLSL structs.</summary>
        public const int BladeStride = 32;

        /// <summary>Threads per group in GrassPlacement.compute (numthreads x).</summary>
        public const int GroupSize = 64;

        /// <summary>Most thread groups along one dispatch axis.</summary>
        public const int MaxGroupsPerAxis = 65535;

        /// <summary>Byte size of one indexed indirect draw (five uints).</summary>
        public const int ArgsBytes = 20;

        /// <summary>Word index of each indirect args field.</summary>
        public const int ArgIndexCount = 0, ArgInstanceCount = 1, ArgStartIndex = 2, ArgBaseVertex = 3, ArgStartInstance = 4;

        /// <summary>
        /// Six inward-facing frustum planes (xyz normal, w offset, normalised) from a view-projection matrix in OpenGL clip convention,
        /// the same layout as GeometryUtility.CalculateFrustumPlanes: left, right, bottom, top, near, far.
        /// </summary>
        public static void FrustumPlanes(Matrix4x4 viewProj, Vector4[] planes)
        {
            Vector4 r0 = viewProj.GetRow(0), r1 = viewProj.GetRow(1), r2 = viewProj.GetRow(2), r3 = viewProj.GetRow(3);
            planes[0] = Normalize(r3 + r0);
            planes[1] = Normalize(r3 - r0);
            planes[2] = Normalize(r3 + r1);
            planes[3] = Normalize(r3 - r1);
            planes[4] = Normalize(r3 + r2);
            planes[5] = Normalize(r3 - r2);
        }

        private static Vector4 Normalize(Vector4 p)
        {
            float len = Mathf.Sqrt(p.x * p.x + p.y * p.y + p.z * p.z);
            return len > 1e-8f ? p / len : p;
        }

        /// <summary>Signed distance of a point to a plane from <see cref="FrustumPlanes"/>; negative is outside.</summary>
        public static float PlaneDistance(Vector4 plane, Vector3 p) => plane.x * p.x + plane.y * p.y + plane.z * p.z + plane.w;

        /// <summary>Window of a world rect for the shaders: xy = world xz corner, zw = 1 / size.</summary>
        public static Vector4 Window(Rect worldRect) => new Vector4(worldRect.xMin, worldRect.yMin, 1f / Mathf.Max(worldRect.width, 0.001f), 1f / Mathf.Max(worldRect.height, 0.001f));

        /// <summary>World xz to 0..1 map uv for a window from <see cref="Window"/>.</summary>
        public static Vector2 WorldToUv(Vector4 window, float x, float z) => new Vector2((x - window.x) * window.z, (z - window.y) * window.w);

        /// <summary>Cells per side of the square grid that covers a circle of <paramref name="radius"/>; one spare cell for the camera's partial cell.</summary>
        public static int CellsPerSide(float radius, float cellSize) => 2 * Mathf.CeilToInt(radius / Mathf.Max(cellSize, 0.01f)) + 2;

        /// <summary>Integer cell holding a world coordinate.</summary>
        public static int CellOf(float world, float cellSize) => Mathf.FloorToInt(world / Mathf.Max(cellSize, 0.01f));

        /// <summary>Grid origin cell: centred on the camera cell.</summary>
        public static int GridOrigin(float cameraCoord, float cellSize, int side) => CellOf(cameraCoord, cellSize) - side / 2;

        /// <summary>Candidates per cell for <paramref name="density"/> clumps per square metre.</summary>
        public static int PerCell(float density, float cellSize) => Mathf.Max(1, Mathf.CeilToInt(density * cellSize * cellSize));

        /// <summary>Probability a candidate is kept so the expected count per cell is density * cell^2 despite rounding up.</summary>
        public static float Accept(float density, float cellSize, int perCell) => perCell > 0 ? Mathf.Clamp01(density * cellSize * cellSize / perCell) : 0f;

        /// <summary>Total candidate threads for a grid.</summary>
        public static long TotalCandidates(int side, int perCell) => (long)side * side * perCell;

        /// <summary>
        /// Largest radius, at most <paramref name="radius"/>, whose grid stays under <paramref name="maxCandidates"/> threads.
        /// Keeps a dense profile with a long draw distance from queueing millions of threads.
        /// </summary>
        public static float FitRadius(float radius, float cellSize, int perCell, long maxCandidates)
        {
            while (radius > cellSize && TotalCandidates(CellsPerSide(radius, cellSize), perCell) > maxCandidates) radius *= 0.9f;
            return radius;
        }

        /// <summary>Group counts for <paramref name="total"/> threads; the shader row width is groupsX * GroupSize.</summary>
        public static void DispatchGroups(long total, out int groupsX, out int groupsY)
        {
            long groups = (total + GroupSize - 1) / GroupSize;
            groupsX = (int)System.Math.Max(1, System.Math.Min(groups, MaxGroupsPerAxis));
            groupsY = (int)System.Math.Max(1, (groups + groupsX - 1) / groupsX);
        }

        /// <summary>The five indirect args words for a draw of <paramref name="indexCount"/> indices with <paramref name="instances"/> instances.</summary>
        public static uint[] Args(uint indexCount, uint instances) => new uint[5] { indexCount, instances, 0, 0, 0 };
    }
}
