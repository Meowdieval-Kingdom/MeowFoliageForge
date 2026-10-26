using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Meowdieval.FoliageForge.Tests
{
    public class FoliageBrushTests
    {
        [Test]
        public void test_foliage_brush_spacing_ignores_height_and_far_points()
        {
            var placed = new List<Vector3> { new Vector3(0f, 5f, 0f), new Vector3(10f, 0f, 0f) };
            Assert.IsTrue(FoliageBrush.TooClose(placed, new Vector3(1f, 0f, 0f), 2f), "height difference must not count");
            Assert.IsFalse(FoliageBrush.TooClose(placed, new Vector3(5f, 0f, 0f), 2f));
            Assert.IsFalse(FoliageBrush.TooClose(new List<Vector3>(), Vector3.zero, 2f), "another category's list is empty");
        }

        [Test]
        public void test_foliage_brush_weighted_pick_same_seed_same_sequence()
        {
            var weights = new List<float> { 10f, 30f, 60f };
            System.Random a = new System.Random(42), b = new System.Random(42);
            for (int i = 0; i < 100; i++) Assert.AreEqual(FoliageBrush.WeightedPick(weights, a), FoliageBrush.WeightedPick(weights, b));
        }

        [Test]
        public void test_foliage_brush_weighted_pick_share_follows_weights()
        {
            var weights = new List<float> { 75f, 25f };
            var rng = new System.Random(7);
            const int draws = 4000;
            int first = 0;
            for (int i = 0; i < draws; i++) if (FoliageBrush.WeightedPick(weights, rng) == 0) first++;
            Assert.That(first / (float)draws, Is.InRange(0.72f, 0.78f));
        }

        [Test]
        public void test_foliage_brush_weighted_pick_zero_weight_never_picked()
        {
            var weights = new List<float> { 0f, 50f, 0f, 50f };
            var rng = new System.Random(3);
            for (int i = 0; i < 1000; i++)
            {
                int pick = FoliageBrush.WeightedPick(weights, rng);
                Assert.IsTrue(pick == 1 || pick == 3, $"picked {pick}");
            }
        }

        [Test]
        public void test_foliage_brush_weighted_pick_all_zero_returns_minus_one()
        {
            Assert.AreEqual(-1, FoliageBrush.WeightedPick(new List<float> { 0f, 0f }, new System.Random(1)));
            Assert.AreEqual(-1, FoliageBrush.WeightedPick(new List<float>(), new System.Random(1)));
        }

        [Test]
        public void test_foliage_brush_edge_weight_rises_toward_rim_with_strength()
        {
            Assert.AreEqual(1f, FoliageBrush.EdgeWeight(0f, 0f), 1e-5f, "strength 0 keeps everything");
            Assert.AreEqual(0f, FoliageBrush.EdgeWeight(0f, 1f), 1e-5f, "full strength empties the centre");
            Assert.AreEqual(1f, FoliageBrush.EdgeWeight(1f, 1f), 1e-5f, "the rim is always kept");
            Assert.AreEqual(0.75f, FoliageBrush.EdgeWeight(0.5f, 0.5f), 1e-5f);
            Assert.Greater(FoliageBrush.EdgeWeight(0.8f, 0.6f), FoliageBrush.EdgeWeight(0.2f, 0.6f));
        }

        [Test]
        public void test_foliage_brush_cluster_index_picks_nearest_centre()
        {
            var centres = new List<Vector2> { new Vector2(0f, 0f), new Vector2(10f, 0f) };
            Assert.AreEqual(0, FoliageBrush.ClusterIndex(new Vector2(2f, 1f), centres));
            Assert.AreEqual(1, FoliageBrush.ClusterIndex(new Vector2(7f, 0f), centres));
            Assert.AreEqual(-1, FoliageBrush.ClusterIndex(Vector2.zero, new List<Vector2>()));
        }

        [Test]
        public void test_foliage_brush_mix_summary_normalised_shares()
        {
            Assert.AreEqual("Short 50% / Tall 30% / Fern 20%", FoliageBrush.MixSummary(new[] { "Short", "Tall", "Off", "Fern" }, new[] { 100f, 60f, 0f, 40f }));
            Assert.AreEqual("nothing", FoliageBrush.MixSummary(new[] { "Short" }, new[] { 0f }));
        }
    }
}
