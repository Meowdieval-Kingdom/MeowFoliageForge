using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// Turns a <see cref="TreeRecipe"/> into a two-submesh mesh: wood first, leaf cards second.
    /// Vertex colour R = height 0..1, G = sway weight, B = 1 on leaf cards, A = per-branch random.
    /// UV1 on leaf cards = pivot xyz and flutter phase, which the shader needs for per-card movement.
    /// </summary>
    public static partial class TreeGenerator
    {
        /// <summary>Fills <paramref name="mesh"/> from the recipe. Deterministic for a given recipe.</summary>
        public static TreeStats Build(TreeRecipe recipe, Mesh mesh)
        {
            return new Builder(recipe).Build(mesh);
        }

        /// <summary>Canopy blobs spread over the branch tips of the recipe's tree, for the Auto Blobs button. Deterministic.</summary>
        public static List<CanopyBlob> SuggestBlobs(TreeRecipe recipe)
        {
            List<CanopyBlob> fixedBlobs = recipe.PresetBlobs();
            if (fixedBlobs == null) return new Builder(recipe).Suggest(recipe.Leaves.BlobCount);
            // a fixed layout ignores branch tips, so Crown On Trunk Top adds one cap blob on the trunk tip
            if (recipe.Leaves.CrownOnTrunkTop) fixedBlobs.Add(new Builder(recipe).TrunkTopBlob());
            return fixedBlobs;
        }

        /// <summary>Quad <paramref name="k"/> of a group of <paramref name="count"/> that share one up axis: the first card's width and normal turned
        /// about up by k / count of a half turn, so the quads fan evenly (two = crossed, three = a star).</summary>
        public static void GroupAxes(Vector3 right, Vector3 normal, int k, int count, out Vector3 groupRight, out Vector3 groupNormal)
        {
            float a = Mathf.PI * k / Mathf.Max(1, count);
            float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            groupRight = right * cs + normal * sn;
            groupNormal = normal * cs - right * sn;
        }

        /// <summary>The shared axis of a leaf group: the surface normal <paramref name="normal"/> tipped <paramref name="tiltDeg"/> degrees toward the surface
        /// tangent that points <paramref name="spin"/> radians around the normal, then leaned toward the sky by <paramref name="upBias"/>. Tilt 0 = the normal itself.</summary>
        public static Vector3 GroupUp(Vector3 normal, float tiltDeg, float spin, float upBias)
        {
            Vector3 t = Vector3.Cross(normal, Vector3.up);
            t = t.sqrMagnitude < 1e-4f ? Vector3.Cross(normal, Vector3.right) : t;
            t = Quaternion.AngleAxis(spin * Mathf.Rad2Deg, normal) * t.normalized;
            float a = tiltDeg * Mathf.Deg2Rad;
            Vector3 axis = normal * Mathf.Cos(a) + t * Mathf.Sin(a) + Vector3.up * upBias;
            return axis.sqrMagnitude < 1e-4f ? normal : axis.normalized;
        }

        /// <summary>Scale per blob that spreads the mean radii <paramref name="mean"/> to about 3:1 largest to smallest, blended in by <paramref name="spread"/>.
        /// A blob scores higher the higher it sits (<paramref name="height"/>) and the larger it already is, so the crown ends up biggest. Overall size is kept.</summary>
        public static float[] SizeScales(float[] mean, float[] height, float spread)
        {
            int n = mean.Length;
            var scale = new float[n];
            for (int i = 0; i < n; i++) scale[i] = 1f;
            if (n < 2 || spread <= 0f) return scale;
            float avg = 0f, h0 = float.MaxValue, h1 = float.MinValue, m0 = float.MaxValue, m1 = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                avg += mean[i] / n;
                h0 = Mathf.Min(h0, height[i]);
                h1 = Mathf.Max(h1, height[i]);
                m0 = Mathf.Min(m0, mean[i]);
                m1 = Mathf.Max(m1, mean[i]);
            }
            var score = new float[n];
            float s0 = float.MaxValue, s1 = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                float hn = h1 - h0 > 1e-4f ? (height[i] - h0) / (h1 - h0) : 0.5f;
                float mn = m1 - m0 > 1e-4f ? (mean[i] - m0) / (m1 - m0) : 0.5f;
                score[i] = 0.6f * hn + 0.4f * mn;
                s0 = Mathf.Min(s0, score[i]);
                s1 = Mathf.Max(s1, score[i]);
            }
            for (int i = 0; i < n; i++)
            {
                float t = s1 - s0 > 1e-4f ? (score[i] - s0) / (s1 - s0) : 0.5f;
                scale[i] = Mathf.Lerp(1f, avg * Mathf.Lerp(0.5f, 1.5f, t) / Mathf.Max(mean[i], 1e-3f), spread);
            }
            return scale;
        }

        /// <summary>Factor (at least 1) to grow <paramref name="blob"/> by so every one of <paramref name="tips"/> lies within <paramref name="inside"/> of its
        /// surface, as a fraction of the radius there (0.75 = at least a quarter radius deep). The blob's shape scales linearly with its radii, so one factor serves.</summary>
        public static float TipGrowth(CanopyBlob blob, List<Vector3> tips, float inside)
        {
            float grow = 1f;
            foreach (Vector3 tip in tips)
            {
                Vector3 d = tip - blob.Position;
                float dist = d.magnitude;
                if (dist < 1e-3f) continue;
                grow = Mathf.Max(grow, dist / Mathf.Max(BlobCage.Extent(blob, d / dist) * inside, 1e-3f));
            }
            return grow;
        }

        /// <summary>Outward surface normal of <paramref name="blob"/> in the direction <paramref name="localDir"/> from its centre (tree space, as for
        /// <see cref="BlobCage.Extent"/>). The ellipsoid gradient for a plain blob; a deformed blob blends its cage triangle's normal with it.</summary>
        public static Vector3 BlobSurfaceNormal(CanopyBlob blob, Vector3 localDir)
        {
            Vector3 dir = localDir.sqrMagnitude < 1e-10f ? Vector3.up : localDir.normalized;
            Vector3 e = BlobCage.EllipsoidNormal(blob, dir);
            if (!BlobCage.IsDeformed(blob) || !BlobCage.CageNormal(blob, dir, out Vector3 c)) return e;
            Vector3 n = e + c;
            return n.sqrMagnitude < 1e-6f ? e : n.normalized;
        }

        /// <summary>Pulls <paramref name="p"/> toward one of the <paramref name="tips"/> lying within <paramref name="reach"/> of
        /// <paramref name="center"/>, picked by <paramref name="index"/> so successive cards visit every tip. Unchanged when none is in reach.</summary>
        public static Vector3 PullToTip(Vector3 p, Vector3 center, float reach, List<Vector3> tips, float amount, int index)
        {
            int near = 0;
            float r2 = reach * reach;
            foreach (Vector3 t in tips) if ((t - center).sqrMagnitude <= r2) near++;
            if (near == 0 || amount <= 0f) return p;
            int pick = index % near;
            foreach (Vector3 t in tips)
            {
                if ((t - center).sqrMagnitude > r2) continue;
                if (pick-- == 0) return Vector3.Lerp(p, t, Mathf.Clamp01(amount) * 0.75f);
            }
            return p;
        }

        /// <summary>A fresh random set of canopy blobs over the recipe's crown, for quick iteration. Size is the mean blob radius in metres, spread how far out
        /// from the trunk they reach (0..1), flatness the height-to-width ratio of each blob. Pass a new seed for a new roll.</summary>
        public static List<CanopyBlob> RandomBlobs(TreeRecipe recipe, int count, float size, float spread, float flatness, int seed)
        {
            var rng = new System.Random(seed);
            float F() => (float)rng.NextDouble();
            List<Vector3> pts = new Builder(recipe).BranchPoints(rng);
            var list = new List<CanopyBlob>();
            if (pts.Count == 0) return list;
            // the first blob crowns the trunk top; the rest sit on random branch points, kept apart by Spread
            float apart = size * spread * 1.4f;
            for (int i = 0; i < count; i++)
            {
                Vector3 at = pts[pts.Count - 1];
                if (i > 0)
                    for (int tries = 0; tries < 24; tries++)
                    {
                        at = pts[rng.Next(pts.Count)];
                        bool free = true;
                        foreach (CanopyBlob b in list) if ((b.Position - at).magnitude < apart)
                        {
                            free = false;
                            break;
                        }
                        if (free) break;
                    }
                float s = size * Mathf.Lerp(0.75f, 1.3f, F());
                list.Add(new CanopyBlob
                {
                    Position = at,
                    Radii = new Vector3(s, s * flatness * Mathf.Lerp(0.85f, 1.15f, F()), s * Mathf.Lerp(0.85f, 1.15f, F())),
                    Rotation = new Vector3((F() - 0.5f) * 24f, F() * 360f, (F() - 0.5f) * 24f),
                });
            }
            return list;
        }

        sealed class Branch
        {
            public readonly List<Vector3> P = new List<Vector3>();
            public readonly List<float> R = new List<float>();
            public int Level;
            public float Length;
            public float Random;
            public bool Main;      // the trunk below any split: carries the root lobes
            public bool NoLeaves;  // roots and broken branches
            public bool Broken;    // snapped off: thick, jagged end
            public float Yaw;      // stems: direction they lean out to, in degrees
            public float USpan = 1f; // share of the full trunk height this path covers (a split trunk is shorter)
        }

        // one leaf card in the vertex/index lists; flat cards are 4 vertices and 6 indices, shaped cards a grid
        struct Card
        {
            public int V0, VCount, I0, ICount, Clump;
            public Vector3 Right, Up;
        }
    }
}
