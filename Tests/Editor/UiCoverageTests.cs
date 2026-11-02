using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Meowdieval.FoliageForge.Tests
{
    /// <summary>Checks that the tree and grass inspectors place every serialized field exactly once, and that they build with the shared stylesheet.</summary>
    public sealed class UiCoverageTests
    {
        /// <summary>Leaf property paths: structs are walked into, arrays and plain values are one entry (same convention as TreeRecipeEditor.LeafPaths).</summary>
        static List<string> Leaves(SerializedObject so)
        {
            var result = new List<string>();
            SerializedProperty it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = it.propertyType == SerializedPropertyType.Generic && !it.isArray;
                if (it.propertyPath == "m_Script" || enter) continue;
                result.Add(it.propertyPath);
            }
            return result;
        }

        // PlacedFieldPaths is internal and the package has no InternalsVisibleTo for the test assembly, so call it by reflection.
        static string[] Placed(Type editor)
        {
            MethodInfo m = editor.GetMethod("PlacedFieldPaths", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(m, Is.Not.Null, editor.Name + ".PlacedFieldPaths not found");
            return ((IEnumerable<string>)m.Invoke(null, null)).ToArray();
        }

        // FoliageUi is internal (no InternalsVisibleTo), so read its Sheet property by reflection.
        static StyleSheet UiSheet()
        {
            Type t = typeof(TreeRecipeEditor).Assembly.GetType("Meowdieval.FoliageForge.FoliageUi");
            Assert.That(t, Is.Not.Null, "FoliageUi not found");
            return (StyleSheet)t.GetProperty("Sheet", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
        }

        static bool Covers(string placed, string leaf) => leaf == placed || leaf.StartsWith(placed + ".");

        /// <summary>True when the path resolves; "Variants.X" is checked against the first array element.</summary>
        static bool Exists(SerializedObject so, string path)
        {
            if (path.StartsWith("Variants."))
            {
                SerializedProperty v = so.FindProperty("Variants");
                if (v == null || v.arraySize == 0) return true; // nothing to check against
                path = "Variants.Array.data[0]." + path.Substring("Variants.".Length);
            }
            return so.FindProperty(path) != null;
        }

        static void AssertCoverage(ScriptableObject target, Type editor, bool allowOverlap)
        {
            using (var so = new SerializedObject(target))
            {
                string[] placed = Placed(editor);
                List<string> leaves = Leaves(so);
                Assert.That(leaves, Is.Not.Empty);

                string[] missing = leaves.Where(l => !placed.Any(p => Covers(p, l))).ToArray();
                string[] stale = placed.Distinct().Where(p => !Exists(so, p)).ToArray();
                // exact repeats in the placed table; ancestor+child (Features, Features.X) is intended for grass, so only tree counts owners
                string[] dupes = placed.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
                string[] doubled = allowOverlap ? new string[0] : leaves.Where(l => placed.Count(p => Covers(p, l)) > 1).ToArray();

                Assert.That(missing, Is.Empty, editor.Name + ": fields not placed: " + string.Join(", ", missing));
                Assert.That(stale, Is.Empty, editor.Name + ": placed paths that do not exist: " + string.Join(", ", stale));
                Assert.That(dupes, Is.Empty, editor.Name + ": paths placed twice: " + string.Join(", ", dupes));
                Assert.That(doubled, Is.Empty, editor.Name + ": fields owned by several entries: " + string.Join(", ", doubled));
            }
        }

        [Test]
        public void test_tree_recipe_every_field_placed_exactly_once()
        {
            var recipe = ScriptableObject.CreateInstance<TreeRecipe>();
            try { AssertCoverage(recipe, typeof(TreeRecipeEditor), false); }
            finally { UnityEngine.Object.DestroyImmediate(recipe); }
        }

        [Test]
        public void test_grass_profile_every_field_placed_exactly_once()
        {
            var profile = ScriptableObject.CreateInstance<GrassProfile>();
            try { AssertCoverage(profile, typeof(GrassProfileEditor), true); }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }

        [Test]
        public void test_grass_variant_fields_all_placed()
        {
            var profile = ScriptableObject.CreateInstance<GrassProfile>();
            try
            {
                using (var so = new SerializedObject(profile))
                {
                    SerializedProperty v = so.FindProperty("Variants");
                    Assume.That(v.arraySize, Is.GreaterThan(0));
                    string[] placed = Placed(typeof(GrassProfileEditor));
                    var missing = new List<string>();
                    SerializedProperty e = v.GetArrayElementAtIndex(0);
                    SerializedProperty end = e.GetEndProperty();
                    bool enter = true;
                    while (e.NextVisible(enter) && !SerializedProperty.EqualContents(e, end))
                    {
                        enter = e.propertyType == SerializedPropertyType.Generic && !e.isArray;
                        if (enter) continue;
                        string name = e.propertyPath.Substring(e.propertyPath.LastIndexOf('.') + 1);
                        if (!placed.Contains("Variants." + name)) missing.Add(name);
                    }
                    Assert.That(missing, Is.Empty, "GrassVariant fields not placed: " + string.Join(", ", missing));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }

        [Test]
        public void test_foliage_ui_sheet_loads_and_defines_key_classes()
        {
            StyleSheet sheet = UiSheet();
            Assert.That(sheet, Is.Not.Null, "FoliageForge.uss did not load");
            string path = AssetDatabase.GetAssetPath(sheet);
            string text = File.ReadAllText(Path.GetFullPath(path));
            foreach (string cls in new[] { ".ff-chip", ".ff-row", ".ff-field" })
                StringAssert.Contains(cls, text);
        }

        [TestCase(typeof(TreeRecipe))]
        [TestCase(typeof(GrassProfile))]
        public void test_inspector_builds_with_stylesheet(Type targetType)
        {
            ScriptableObject target = ScriptableObject.CreateInstance(targetType);
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(target);
            try
            {
                VisualElement root = null;
                Assert.DoesNotThrow(() => root = editor.CreateInspectorGUI());
                Assert.That(root, Is.Not.Null);
                Assert.That(UiSheet(), Is.Not.Null);
                Assert.That(root.styleSheets.Contains(UiSheet()), "FoliageUi sheet not applied to the inspector root");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(editor);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
