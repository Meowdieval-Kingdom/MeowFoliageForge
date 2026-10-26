using System.Collections.Generic;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// The control cage of a canopy blob: 26 points on a low-poly ellipsoid (two poles and three rings of eight).
    /// A blob's <see cref="CanopyBlob.Deform"/> offsets move those points; cards fill whatever the cage encloses.
    /// </summary>
    public static class BlobCage
    {
        /// <summary>Number of control points.</summary>
        public const int PointCount = 26;
        const int Sides = 8;
        /// <summary>The faceted cage sits inside its ellipsoid between points, so points are pushed out a little to keep the volume.</summary>
        const float Inflate = 1.08f;

        /// <summary>Unit directions of the control points: 0 top pole, 1-8 upper ring, 9-16 equator, 17-24 lower ring, 25 bottom pole.</summary>
        public static readonly Vector3[] Directions = BuildDirections();
        /// <summary>Triangle indices of the cage surface.</summary>
        public static readonly int[] Triangles = BuildTriangles();
        static readonly int[][] Neighbours = BuildNeighbours();

        private static Vector3[] BuildDirections()
        {
            var d = new Vector3[PointCount];
            d[0] = Vector3.up;
            d[PointCount - 1] = Vector3.down;
            for (int ring = 0; ring < 3; ring++)
            {
                float polar = (45f + 45f * ring) * Mathf.Deg2Rad;
                for (int j = 0; j < Sides; j++)
                {
                    float a = j * Mathf.PI * 2f / Sides;
                    d[1 + ring * Sides + j] = new Vector3(Mathf.Sin(polar) * Mathf.Cos(a), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(a));
                }
            }
            return d;
        }

        private static int[] BuildTriangles()
        {
            var t = new List<int>();
            for (int j = 0; j < Sides; j++)
            {
                int j1 = (j + 1) % Sides;
                t.Add(0);
                t.Add(1 + j1);
                t.Add(1 + j);
                for (int ring = 0; ring < 2; ring++)
                {
                    int a = 1 + ring * Sides, b = a + Sides;
                    t.Add(a + j);
                    t.Add(a + j1);
                    t.Add(b + j);
                    t.Add(a + j1);
                    t.Add(b + j1);
                    t.Add(b + j);
                }
                t.Add(PointCount - 1);
                t.Add(17 + j);
                t.Add(17 + j1);
            }
            return t.ToArray();
        }

        private static int[][] BuildNeighbours()
        {
            var sets = new HashSet<int>[PointCount];
            for (int i = 0; i < PointCount; i++) sets[i] = new HashSet<int>();
            for (int t = 0; t < Triangles.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = Triangles[t + k], b = Triangles[t + (k + 1) % 3];
                    sets[a].Add(b);
                    sets[b].Add(a);
                }
            var result = new int[PointCount][];
            for (int i = 0; i < PointCount; i++)
            {
                result[i] = new int[sets[i].Count];
                sets[i].CopyTo(result[i]);
            }
            return result;
        }

        /// <summary>Points adjacent to <paramref name="point"/> on the cage.</summary>
        public static int[] NeighboursOf(int point) => Neighbours[point];

        /// <summary>True when the blob has been deformed, so the cage defines its shape instead of the plain ellipsoid.</summary>
        public static bool IsDeformed(CanopyBlob blob) => blob.Deform != null && blob.Deform.Length == PointCount;

        /// <summary>The blob's orientation in tree space.</summary>
        public static Quaternion Rot(CanopyBlob blob) => Quaternion.Euler(blob.Rotation);

        /// <summary>True when the blob has a flat belly or taper, which the cage carries, so the cage defines its shape instead of the plain ellipsoid.</summary>
        public static bool HasShape(CanopyBlob blob) => blob.FlatBelly > 0f || blob.Taper > 0f;

        const float FlatDepth = 0.8f; // FlatBelly 1 leaves 20% of the underside's height
        const float TaperDepth = 0.5f; // Taper 1 narrows the +Z end to half and widens the -Z end by half

        private static float TaperScale(CanopyBlob blob, float z) => 1f - TaperDepth * blob.Taper * Mathf.Clamp(z / Mathf.Max(blob.Radii.z, 1e-3f), -1f, 1f);

        // ellipsoid point -> shaped point, in the blob's own frame
        private static Vector3 Shape(CanopyBlob blob, Vector3 p)
        {
            float s = TaperScale(blob, p.z);
            p.x *= s;
            p.y *= s;
            if (p.y < 0f) p.y *= 1f - FlatDepth * blob.FlatBelly;
            return p;
        }

        // inverse of Shape
        private static Vector3 Unshape(CanopyBlob blob, Vector3 p)
        {
            if (p.y < 0f) p.y /= 1f - FlatDepth * blob.FlatBelly;
            float s = TaperScale(blob, p.z);
            p.x /= s;
            p.y /= s;
            return p;
        }

        // point in the blob's own (unrotated) frame
        private static Vector3 Unrotated(CanopyBlob blob, int i)
        {
            Vector3 p = Vector3.Scale(Directions[i], blob.Radii) * Inflate;
            if (HasShape(blob)) p = Shape(blob, p);
            return IsDeformed(blob) ? p + blob.Deform[i] : p;
        }

        /// <summary>Control point <paramref name="i"/> relative to the blob's position, rotated into tree space.</summary>
        public static Vector3 Local(CanopyBlob blob, int i) => Rot(blob) * Unrotated(blob, i);

        /// <summary>A point inside the plain ellipsoid at unit direction <paramref name="dir"/> (tree space) scaled by <paramref name="rad"/>, in tree space.</summary>
        public static Vector3 EllipsoidPoint(CanopyBlob blob, Vector3 dir, float rad)
        {
            Quaternion q = Rot(blob);
            return blob.Position + q * Vector3.Scale(Quaternion.Inverse(q) * dir * rad, blob.Radii);
        }

        /// <summary>Control point <paramref name="i"/> in tree space.</summary>
        public static Vector3 World(CanopyBlob blob, int i) => blob.Position + Local(blob, i);

        /// <summary>Gives the blob a zeroed deform array if it has none.</summary>
        public static CanopyBlob EnsureDeform(CanopyBlob blob)
        {
            if (!IsDeformed(blob)) blob.Deform = new Vector3[PointCount];
            return blob;
        }

        /// <summary>Distance from the blob's centre to its surface along the unit direction <paramref name="dir"/>.</summary>
        public static float Extent(CanopyBlob blob, Vector3 dir)
        {
            if (IsDeformed(blob) || HasShape(blob))
            {
                float best = float.MaxValue;
                for (int t = 0; t < Triangles.Length; t += 3)
                {
                    float hit = RayTriangle(dir, Local(blob, Triangles[t]), Local(blob, Triangles[t + 1]), Local(blob, Triangles[t + 2]));
                    if (hit > 0f && hit < best) best = hit;
                }
                if (best < float.MaxValue) return best;
            }
            Vector3 ld = Quaternion.Inverse(Rot(blob)) * dir;
            var q = new Vector3(ld.x / Mathf.Max(blob.Radii.x, 1e-3f), ld.y / Mathf.Max(blob.Radii.y, 1e-3f), ld.z / Mathf.Max(blob.Radii.z, 1e-3f));
            return 1f / Mathf.Max(q.magnitude, 1e-4f);
        }

        // squared radius-normalised distance of an own-frame point from the unshaped ellipsoid; the shaped surface is its level set
        private static float ShapeField(CanopyBlob blob, Vector3 p)
        {
            Vector3 u = Unshape(blob, p);
            return u.x * u.x / Mathf.Max(blob.Radii.x * blob.Radii.x, 1e-6f) + u.y * u.y / Mathf.Max(blob.Radii.y * blob.Radii.y, 1e-6f) + u.z * u.z / Mathf.Max(blob.Radii.z * blob.Radii.z, 1e-6f);
        }

        // smooth normal of the flat-belly / tapered surface: gradient of ShapeField by central differences at the cage hit
        private static Vector3 ShapedNormal(CanopyBlob blob, Vector3 dir)
        {
            Quaternion q = Rot(blob);
            Vector3 p = Quaternion.Inverse(q) * dir * Extent(blob, dir);
            float e = 0.02f * Mathf.Max(blob.Radii.x + blob.Radii.y + blob.Radii.z, 0.03f) / 3f;
            var g = new Vector3(
                ShapeField(blob, p + Vector3.right * e) - ShapeField(blob, p - Vector3.right * e),
                ShapeField(blob, p + Vector3.up * e) - ShapeField(blob, p - Vector3.up * e),
                ShapeField(blob, p + Vector3.forward * e) - ShapeField(blob, p - Vector3.forward * e));
            return g.sqrMagnitude < 1e-12f ? dir.normalized : (q * g).normalized;
        }

        /// <summary>Outward unit normal of the plain ellipsoid (or, with a flat belly or taper, of that shaped surface) where it meets the direction <paramref name="dir"/> (tree space) from the blob's centre.</summary>
        public static Vector3 EllipsoidNormal(CanopyBlob blob, Vector3 dir)
        {
            if (HasShape(blob)) return ShapedNormal(blob, dir.normalized);
            Quaternion q = Rot(blob);
            Vector3 ld = Quaternion.Inverse(q) * dir;
            var g = new Vector3(ld.x / Mathf.Max(blob.Radii.x * blob.Radii.x, 1e-6f), ld.y / Mathf.Max(blob.Radii.y * blob.Radii.y, 1e-6f), ld.z / Mathf.Max(blob.Radii.z * blob.Radii.z, 1e-6f));
            return g.sqrMagnitude < 1e-12f ? dir.normalized : (q * g).normalized;
        }

        /// <summary>Outward unit normal of the cage triangle that the direction <paramref name="dir"/> (tree space) from the blob's centre passes through; false when it misses.</summary>
        public static bool CageNormal(CanopyBlob blob, Vector3 dir, out Vector3 normal)
        {
            normal = Vector3.up;
            float best = float.MaxValue;
            for (int t = 0; t < Triangles.Length; t += 3)
            {
                Vector3 a = Local(blob, Triangles[t]), b = Local(blob, Triangles[t + 1]), c = Local(blob, Triangles[t + 2]);
                float hit = RayTriangle(dir, a, b, c);
                if (hit <= 0f || hit >= best) continue;
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) continue;
                best = hit;
                normal = (Vector3.Dot(n, dir) < 0f ? -n : n).normalized;
            }
            return best < float.MaxValue;
        }

        /// <summary>Half-size of the box around the cage in the blob's own (unrotated) frame, used where an ellipsoid approximation is enough.</summary>
        public static Vector3 HalfExtents(CanopyBlob blob)
        {
            if (!IsDeformed(blob)) return blob.Radii;
            Vector3 max = Vector3.zero;
            for (int i = 0; i < PointCount; i++)
            {
                Vector3 p = Unrotated(blob, i);
                max = Vector3.Max(max, new Vector3(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z)));
            }
            return Vector3.Max(max, Vector3.one * 0.1f);
        }

        /// <summary>Fills <paramref name="mesh"/> with the cage surface in tree space, pushed out by <paramref name="pad"/> metres (the reach of the leaf cards beyond their centres).</summary>
        public static void BuildMesh(CanopyBlob blob, Mesh mesh, float pad = 0f)
        {
            var v = new Vector3[PointCount];
            for (int i = 0; i < PointCount; i++)
            {
                Vector3 local = Local(blob, i);
                v[i] = blob.Position + local + local.normalized * pad;
            }
            mesh.Clear();
            mesh.vertices = v;
            mesh.triangles = Triangles;
            mesh.RecalculateBounds();
        }

        // Moller-Trumbore from the origin; distance along dir, or -1
        private static float RayTriangle(Vector3 dir, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 e1 = b - a, e2 = c - a;
            Vector3 p = Vector3.Cross(dir, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-9f) return -1f;
            float inv = 1f / det;
            Vector3 s = -a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0f || u > 1f) return -1f;
            Vector3 q = Vector3.Cross(s, e1);
            float v = Vector3.Dot(dir, q) * inv;
            if (v < 0f || u + v > 1f) return -1f;
            return Vector3.Dot(e2, q) * inv;
        }
    }
}
