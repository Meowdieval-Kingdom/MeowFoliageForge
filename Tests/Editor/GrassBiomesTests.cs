using NUnit.Framework;
using UnityEngine;

namespace Meowdieval.FoliageForge.Tests
{
    public class GrassBiomesTests
    {
        GrassProfile _profile;

        [SetUp]
        public void SetUp() => _profile = ScriptableObject.CreateInstance<GrassProfile>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_profile);

        [Test]
        public void test_grass_biomes_every_biome_has_types_with_unique_source_ids()
        {
            foreach (GrassBiome b in System.Enum.GetValues(typeof(GrassBiome)))
            {
                GrassVariant[] v = GrassBiomes.Variants(b);
                Assert.Greater(v.Length, 0, b.ToString());
                Assert.AreEqual(v.Length, new System.Collections.Generic.HashSet<string>(System.Array.ConvertAll(v, x => x.SourceId)).Count, b.ToString());
            }
        }

        [Test]
        public void test_grass_biomes_quality_scaling_survives_renaming_a_type()
        {
            GrassBiomes.Apply(_profile, GrassBiome.Forest);
            GrassVariant fern = System.Array.Find(_profile.Variants, v => v.Name == "Fern");
            fern.Name = "My fern";
            _profile.ApplyQuality(GrassProfile.Quality.Balanced);
            float expected = GrassBiomes.Default("Forest.Fern", null, GrassBiome.Meadow).Density * 0.55f;
            Assert.AreEqual(expected, fern.Density, 1e-5f);
        }

        [Test]
        public void test_grass_biomes_add_types_merges_without_duplicates_or_recolouring()
        {
            GrassBiomes.Apply(_profile, GrassBiome.Meadow);
            Color ground = _profile.GroundColor;
            int before = _profile.Variants.Length;
            GrassBiomes.Apply(_profile, GrassBiome.Forest, GrassApplyMode.AddTypes);
            int after = _profile.Variants.Length;
            GrassBiomes.Apply(_profile, GrassBiome.Forest, GrassApplyMode.AddTypes);
            Assert.AreEqual(before + GrassBiomes.Variants(GrassBiome.Forest).Length, after);
            Assert.AreEqual(after, _profile.Variants.Length, "second merge adds nothing");
            Assert.AreEqual(ground, _profile.GroundColor);
            Assert.AreEqual(GrassBiome.Meadow, _profile.Biome);
        }

        [Test]
        public void test_grass_biomes_replace_keeps_mix_of_same_ingredient()
        {
            GrassBiomes.Apply(_profile, GrassBiome.Forest);
            _profile.Variants[0].Mix = 40f;
            GrassBiomes.Apply(_profile, GrassBiome.Forest);
            Assert.AreEqual(40f, _profile.Variants[0].Mix);
        }
    }
}
