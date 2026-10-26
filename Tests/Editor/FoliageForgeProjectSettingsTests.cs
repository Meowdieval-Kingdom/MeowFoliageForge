using NUnit.Framework;

namespace Meowdieval.FoliageForge.Tests
{
    public class FoliageForgeProjectSettingsTests
    {
        private static string[] Layers(params int[] used)
        {
            var layers = new string[32];
            for (int i = 0; i < layers.Length; i++) layers[i] = i < 8 && i != 3 ? "Builtin" + i : string.Empty;
            foreach (int i in used) layers[i] = "Used" + i;
            return layers;
        }

        private static PipelineSetting Find(string name, bool alphaToCoverage = false, bool additionalLights = false) =>
            FoliageForgeProjectSettings.Settings(alphaToCoverage, additionalLights).Find(s => s.Name == name);

        [Test]
        public void test_project_settings_layer_slot_skips_builtin_and_used_returns_first_free()
        {
            string[] layers = Layers(8, 9, 10);
            layers[12] = "  ";
            Assert.AreEqual(11, FoliageForgeProjectSettings.FindFreeLayerSlot(layers), "empty builtin slot 3 must not be used");
            layers[11] = "Used11";
            Assert.AreEqual(12, FoliageForgeProjectSettings.FindFreeLayerSlot(layers), "whitespace counts as free");
        }

        [Test]
        public void test_project_settings_layer_slot_all_used_returns_minus_one()
        {
            var used = new int[24];
            for (int i = 0; i < used.Length; i++) used[i] = i + 8;
            Assert.AreEqual(-1, FoliageForgeProjectSettings.FindFreeLayerSlot(Layers(used)));
        }

        [Test]
        public void test_project_settings_msaa_needs_4x_and_is_required_with_alpha_to_coverage()
        {
            PipelineSetting msaa = Find("MSAA", alphaToCoverage: true);
            Assert.IsFalse(msaa.Meets(1f));
            Assert.IsFalse(msaa.Meets(2f));
            Assert.IsTrue(msaa.Meets(4f));
            Assert.IsTrue(msaa.Meets(8f));
            Assert.AreEqual(SetupSeverity.Required, msaa.Severity);
            Assert.AreEqual(SetupSeverity.Recommended, Find("MSAA").Severity);
        }

        [Test]
        public void test_project_settings_additional_lights_severity_follows_grass_profiles()
        {
            PipelineSetting lights = Find("Additional lights per pixel", additionalLights: true);
            Assert.AreEqual(SetupSeverity.Recommended, lights.Severity);
            Assert.AreEqual(SetupSeverity.Optional, Find("Additional lights per pixel").Severity);
            Assert.IsTrue(lights.Meets(1f), "1 = LightRenderingMode.PerPixel");
            Assert.IsFalse(lights.Meets(2f), "2 = PerVertex");
            Assert.IsFalse(lights.Meets(0f), "0 = Disabled");
        }

        [Test]
        public void test_project_settings_every_fix_value_meets_its_target()
        {
            foreach (PipelineSetting s in FoliageForgeProjectSettings.Settings(true, true))
                Assert.IsTrue(s.Meets(s.FixValue), s.Name);
        }
    }
}
