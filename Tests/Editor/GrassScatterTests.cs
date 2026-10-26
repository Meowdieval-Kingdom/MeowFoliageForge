using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Meowdieval.FoliageForge.Tests
{
    public class GrassScatterTests
    {
        private static GrassStroke Stroke(float x, float z, float r, bool erase) =>
            new GrassStroke { Point = new Vector3(x, 0f, z), Radius = r, Erase = erase, Seed = 7 };

        [Test]
        public void test_grass_scatter_later_erase_removes_earlier_layers()
        {
            GrassStroke[] strokes = { Stroke(0, 0, 2, false), Stroke(0, 0, 2, true) };
            Assert.IsTrue(GrassScatter.Erased(Vector3.zero, 0, strokes), "box fill is older than the erase");
            Assert.IsTrue(GrassScatter.Erased(Vector3.zero, 1, strokes), "paint stroke 0 is older than the erase");
        }

        [Test]
        public void test_grass_scatter_paint_after_erase_survives()
        {
            GrassStroke[] strokes = { Stroke(0, 0, 2, true), Stroke(0, 0, 2, false) };
            Assert.IsTrue(GrassScatter.Erased(Vector3.zero, 0, strokes));
            Assert.IsFalse(GrassScatter.Erased(Vector3.zero, 2, strokes), "stroke 1 was painted after the erase");
        }

        [Test]
        public void test_grass_scatter_outside_radius_kept()
        {
            GrassStroke[] strokes = { Stroke(0, 0, 2, true) };
            Assert.IsFalse(GrassScatter.Erased(new Vector3(3f, 0f, 0f), 0, strokes));
        }

        [Test]
        public void test_grass_scatter_stroke_candidates_deterministic_inside_disc()
        {
            var profile = ScriptableObject.CreateInstance<GrassProfile>();
            var a = new System.Collections.Generic.List<Vector3>();
            var b = new System.Collections.Generic.List<Vector3>();
            var yaw = new System.Collections.Generic.List<float>();
            GrassStroke s = Stroke(1, 2, 3, false);
            GrassScatter.StrokeCandidates(profile, 0, s, Matrix4x4.identity, a, yaw);
            GrassScatter.StrokeCandidates(profile, 0, s, Matrix4x4.identity, b, yaw);
            Assert.Greater(a.Count, 0);
            CollectionAssert.AreEqual(a, b);
            foreach (Vector3 p in a) Assert.LessOrEqual(new Vector2(p.x - 1f, p.z - 2f).magnitude, 3.001f);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void test_grass_variant_apply_mix_scales_density()
        {
            Assert.AreEqual(5f, GrassVariant.ApplyMix(10f, 50f), 1e-5f);
            Assert.AreEqual(10f, GrassVariant.ApplyMix(10f, 100f), 1e-5f);
            Assert.AreEqual(0f, GrassVariant.ApplyMix(10f, 0f), 1e-5f);
            Assert.AreEqual(10f, GrassVariant.ApplyMix(10f, 150f), 1e-5f, "clamped to 100%");
            Assert.AreEqual(0f, new GrassVariant { Density = 10f, Mix = 100f, Enabled = false }.MixedDensity, "disabled types scatter nothing");
        }

        [Test]
        public void test_grass_scatter_stroke_candidates_half_mix_halves_count()
        {
            var profile = ScriptableObject.CreateInstance<GrassProfile>();
            var points = new List<Vector3>();
            var yaw = new List<float>();
            GrassStroke s = Stroke(0, 0, 2, false);
            profile.Variants[0].Density = 10f;
            int Count(float mix)
            {
                profile.Variants[0].Mix = mix;
                points.Clear();
                GrassScatter.StrokeCandidates(profile, 0, s, Matrix4x4.identity, points, yaw);
                return points.Count;
            }
            Assert.AreEqual(Mathf.CeilToInt(Mathf.PI * 4f * 10f), Count(100f));
            Assert.AreEqual(Mathf.CeilToInt(Mathf.PI * 4f * 5f), Count(50f));
            Assert.AreEqual(0, Count(0f));
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void test_grass_scatter_erase_type_mask_spares_other_types()
        {
            GrassStroke e = Stroke(0, 0, 2, true);
            e.TypeMask = 1 << 1;
            GrassStroke[] strokes = { e };
            Assert.IsTrue(GrassScatter.Erased(Vector3.zero, 0, strokes, 1));
            Assert.IsFalse(GrassScatter.Erased(Vector3.zero, 0, strokes, 0), "type 0 is not in the mask");
            e.TypeMask = 0;
            Assert.IsTrue(GrassScatter.Erased(Vector3.zero, 0, new[] { e }, 3), "legacy mask 0 erases every type");
        }

        [Test]
        public void test_grass_scatter_stroke_density_scales_count_and_keeps_subset()
        {
            var profile = ScriptableObject.CreateInstance<GrassProfile>();
            profile.Variants[0].Density = 10f;
            profile.Variants[0].Mix = 100f;
            var full = new List<Vector3>();
            var half = new List<Vector3>();
            var legacy = new List<Vector3>();
            var yaw = new List<float>();
            GrassStroke s = Stroke(0, 0, 2, false);
            s.Density = 1f;
            GrassScatter.StrokeCandidates(profile, 0, s, Matrix4x4.identity, full, yaw);
            s.Density = 0.5f;
            GrassScatter.StrokeCandidates(profile, 0, s, Matrix4x4.identity, half, yaw);
            s.Density = 0f;
            GrassScatter.StrokeCandidates(profile, 0, s, Matrix4x4.identity, legacy, yaw);
            Assert.AreEqual(Mathf.CeilToInt(Mathf.PI * 4f * 5f), half.Count);
            foreach (Vector3 p in half) CollectionAssert.Contains(full, p);
            CollectionAssert.AreEqual(full, legacy, "density 0 (saved before the field existed) reads as 1");
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void test_grass_scatter_candidates_lower_mix_keeps_subset()
        {
            var profile = ScriptableObject.CreateInstance<GrassProfile>();
            profile.Variants[0].Clustering = 0f;
            var full = new List<Vector3>();
            var part = new List<Vector3>();
            var raw = new List<Vector3>();
            var yaw = new List<float>();
            GrassScatter.Candidates(profile, 0, new Vector2(4f, 4f), 0f, Matrix4x4.identity, new System.Random(5), 10000, full, yaw);
            profile.Variants[0].Mix = 50f;
            GrassScatter.Candidates(profile, 0, new Vector2(4f, 4f), 0f, Matrix4x4.identity, new System.Random(5), 10000, part, yaw);
            GrassScatter.Candidates(profile, 0, new Vector2(4f, 4f), 0f, Matrix4x4.identity, new System.Random(5), 10000, raw, yaw, false, applyMix: false);
            Assert.Greater(part.Count, 0);
            Assert.Less(part.Count, full.Count);
            foreach (Vector3 p in part) CollectionAssert.Contains(full, p);
            CollectionAssert.AreEqual(full, raw, "bakes ignore Mix");
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void test_grass_lod_keep_in_unit_range_and_deterministic()
        {
            for (int i = -50; i < 50; i++)
            {
                float x = i * 1.37f, z = i * -0.71f + 3f;
                float k = GrassLod.Keep(x, z);
                Assert.GreaterOrEqual(k, 0f);
                Assert.Less(k, 1f);
                Assert.AreEqual(k, GrassLod.Keep(x, z));
            }
            Assert.AreNotEqual(GrassLod.Keep(1f, 2f), GrassLod.Keep(2f, 1f), "x and z are hashed asymmetrically");
        }

        [Test]
        public void test_grass_lod_share_hits_endpoints()
        {
            Assert.AreEqual(1f, GrassLod.Share(0f, 10f, 40f, 0.3f), 1e-5f);
            Assert.AreEqual(1f, GrassLod.Share(10f, 10f, 40f, 0.3f), 1e-5f);
            Assert.AreEqual(0.3f, GrassLod.Share(40f, 10f, 40f, 0.3f), 1e-5f);
            Assert.AreEqual(0.3f, GrassLod.Share(100f, 10f, 40f, 0.3f), 1e-5f);
            Assert.AreEqual(0.65f, GrassLod.Share(25f, 10f, 40f, 0.3f), 1e-5f, "smoothstep midpoint is 0.5");
        }

        [Test]
        public void test_grass_lod_prefix_count_boundaries()
        {
            float[] keys = { 0.1f, 0.2f, 0.2f, 0.5f, 0.9f };
            Assert.AreEqual(0, GrassLod.PrefixCount(keys, 0.05f));
            Assert.AreEqual(1, GrassLod.PrefixCount(keys, 0.1f), "keep <= share");
            Assert.AreEqual(3, GrassLod.PrefixCount(keys, 0.2f));
            Assert.AreEqual(5, GrassLod.PrefixCount(keys, 1f));
            Assert.AreEqual(0, GrassLod.PrefixCount(new float[0], 1f));
        }

        [Test]
        public void test_grass_lod_mesh_level_follows_distance_fractions()
        {
            Assert.AreEqual(0, GrassLod.MeshLevel(0f, 100f, 0.3f, 0.75f));
            Assert.AreEqual(1, GrassLod.MeshLevel(30f, 100f, 0.3f, 0.75f));
            Assert.AreEqual(1, GrassLod.MeshLevel(74f, 100f, 0.3f, 0.75f));
            Assert.AreEqual(2, GrassLod.MeshLevel(75f, 100f, 0.3f, 0.75f));
        }

        [Test]
        public void test_grass_meshes_triangle_count_drops_with_lod()
        {
            var gv = new GrassVariant { Cards = 4, Segments = 3 };
            Assert.AreEqual(24, GrassMeshes.TriangleCount(gv, 0));
            Assert.AreEqual(6, GrassMeshes.TriangleCount(gv, 1));
            Assert.AreEqual(4, GrassMeshes.TriangleCount(gv, 2));
            gv.Cards = 1;
            Assert.AreEqual(2, GrassMeshes.TriangleCount(gv, 2));
        }

        [Test]
        public void test_grass_features_enabled_count_includes_lod_switches()
        {
            var f = new GrassFeatures();
            int on = f.EnabledCount();
            f.MeshLod = !f.MeshLod;
            Assert.AreNotEqual(on, f.EnabledCount());
            f.MeshLod = !f.MeshLod;
            f.CpuThinning = !f.CpuThinning;
            Assert.AreNotEqual(on, f.EnabledCount());
        }

        [Test]
        public void test_grass_features_enabled_count_includes_new_switches()
        {
            var f = new GrassFeatures();
            int on = f.EnabledCount();
            f.TrampleRT = true;
            f.TrailTint = true;
            f.LightRamp = true;
            f.PerspectiveCorrection = true;
            Assert.AreEqual(on + 4, f.EnabledCount());
            f.WindTexture = !f.WindTexture;
            Assert.AreEqual(on + 3, f.EnabledCount(), "wind texture defaults on");
            Assert.IsFalse(new GrassFeatures().TrampleRT, "trample map is opt-in");
        }

        [Test]
        public void test_grass_quality_light_drops_trample_map_and_never_enables_it()
        {
            var profile = ScriptableObject.CreateInstance<GrassProfile>();
            profile.ApplyQuality(GrassProfile.Quality.Lush);
            Assert.IsFalse(profile.Features.TrampleRT);
            profile.Features.TrampleRT = true;
            profile.ApplyQuality(GrassProfile.Quality.Light);
            Assert.IsFalse(profile.Features.TrampleRT);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void test_grass_trample_snap_corner_moves_in_whole_texels()
        {
            const float size = 32f;
            const int res = 128;
            const float texel = size / res;
            Vector2 a = GrassTrampleMath.SnapCorner(new Vector2(10.01f, -3.3f), size, res);
            Vector2 b = GrassTrampleMath.SnapCorner(new Vector2(10.01f + texel * 0.3f, -3.3f), size, res);
            Assert.AreEqual(a.x / texel, Mathf.Round(a.x / texel), 1e-3f);
            Assert.AreEqual(a.y / texel, Mathf.Round(a.y / texel), 1e-3f);
            Assert.LessOrEqual(Mathf.Abs(a.x - b.x), texel + 1e-4f);
            Vector2 shift = GrassTrampleMath.UvShift(b, a, size);
            Assert.AreEqual(Mathf.Round(shift.x * res), shift.x * res, 1e-3f, "shift is a whole number of texels");
            Assert.AreEqual(0f, GrassTrampleMath.UvShift(a, a, size).sqrMagnitude);
        }

        [Test]
        public void test_grass_trample_window_contains_centre_and_maps_to_unit_uv()
        {
            Vector2 centre = new Vector2(5f, 7f);
            Vector2 corner = GrassTrampleMath.SnapCorner(centre, 40f, 256);
            Assert.IsTrue(GrassTrampleMath.Contains(centre, corner, 40f));
            Vector2 uv = GrassTrampleMath.WorldToUv(centre, corner, 40f);
            Assert.AreEqual(0.5f, uv.x, 0.01f);
            Assert.AreEqual(0.5f, uv.y, 0.01f);
            Assert.IsFalse(GrassTrampleMath.Contains(centre + new Vector2(25f, 0f), corner, 40f));
        }

        [Test]
        public void test_grass_trample_decay_reaches_ten_percent_at_recovery_time()
        {
            Assert.AreEqual(0.1f, GrassTrampleMath.Decay(3f, 3f), 1e-5f);
            Assert.AreEqual(1f, GrassTrampleMath.Decay(0f, 3f), 1e-6f);
            Assert.AreEqual(GrassTrampleMath.Decay(2f, 3f), GrassTrampleMath.Decay(1f, 3f) * GrassTrampleMath.Decay(1f, 3f), 1e-5f, "frame rate independent");
        }

        [Test]
        public void test_grass_trample_resolution_clamps_to_power_of_two_range()
        {
            Assert.AreEqual(64, GrassTrampleMath.ClampResolution(10));
            Assert.AreEqual(256, GrassTrampleMath.ClampResolution(250));
            Assert.AreEqual(1024, GrassTrampleMath.ClampResolution(5000));
        }

        [Test]
        public void test_grass_wind_noise_tiles_seamlessly_and_stays_in_range()
        {
            for (int i = 0; i < 40; i++)
            {
                float u = i * 0.0237f, v = i * 0.0411f;
                Assert.AreEqual(GrassWindNoise.Sample(u, v), GrassWindNoise.Sample(u + 1f, v), 1e-4f);
                Assert.AreEqual(GrassWindNoise.Sample(u, v), GrassWindNoise.Sample(u, v + 3f), 1e-4f);
                float n = GrassWindNoise.Sample(u, v);
                Assert.GreaterOrEqual(n, 0f);
                Assert.LessOrEqual(n, 1f);
            }
            Assert.AreEqual(GrassWindNoise.Sample(0.9999f, 0.3f), GrassWindNoise.Sample(0f, 0.3f), 0.02f, "no seam at the tile edge");
            byte[] data = GrassWindNoise.Generate();
            Assert.AreEqual(GrassWindNoise.Size * GrassWindNoise.Size, data.Length);
            CollectionAssert.AreEqual(data, GrassWindNoise.Generate(), "deterministic");
        }
    }
}
