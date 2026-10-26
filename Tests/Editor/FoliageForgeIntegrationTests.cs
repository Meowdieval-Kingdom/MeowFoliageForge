using System.Diagnostics;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Meowdieval.FoliageForge.Tests
{
    public class FoliageForgeIntegrationTests
    {
        const string Folder = "Assets/__FoliageForgeTest";

        string _savedOutput;

        [SetUp]
        public void SetUp()
        {
            _savedOutput = TreeBaker.OutputFolder;
            TreeBaker.OutputFolder = Folder + "/Out";
        }

        [TearDown]
        public void TearDown()
        {
            TreeBaker.OutputFolder = _savedOutput;
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void Shader_ImportsAndCompilesWithoutErrors()
        {
            Shader shader = TreeBaker.LoadShader();
            Assert.IsNotNull(shader, "FoliageForge.shadergraph did not import as a Shader");
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "Shader has compile errors");
            foreach (ShaderMessage m in ShaderUtil.GetShaderMessages(shader))
            {
                Debug.Log($"Shader message ({m.severity}): {m.message} {m.messageDetails} line {m.line}");
                Assert.AreNotEqual(ShaderCompilerMessageSeverity.Error, m.severity, m.message);
            }
            Assert.IsTrue(shader.isSupported);
            foreach (string prop in new[] { "_BaseMap", "_BottomColor", "_TopColor", "_GradientBias", "_LeafCutoff", "_TexScroll", "_Flutter", "_FlutterSpeed", "_Sway", "_ColorVariation", "_Wrap", "_Translucency", "_TranslucencyColor", "_TranslucencyFocus",
                "_ShadowColor", "_ShadowAmount", "_ShadowSoftness", "_RimColor", "_RimStrength", "_RimPower", "_HueShift", "_TreeVariation" })
                Assert.GreaterOrEqual(shader.FindPropertyIndex(prop), 0, "missing shader property " + prop);
        }

        [Test]
        public void BarkShader_ImportsAndCompilesWithoutErrors()
        {
            Shader shader = TreeBaker.LoadBarkShader();
            Assert.IsNotNull(shader, "FoliageForgeBark.shadergraph did not import as a Shader");
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "Bark shader has compile errors");
            foreach (ShaderMessage m in ShaderUtil.GetShaderMessages(shader))
            {
                Debug.Log($"Bark shader message ({m.severity}): {m.message} {m.messageDetails} line {m.line}");
                Assert.AreNotEqual(ShaderCompilerMessageSeverity.Error, m.severity, m.message);
            }
            foreach (string prop in new[] { "_BaseMap", "_MossMap", "_BottomColor", "_TopColor", "_Tiling", "_MossColor", "_MossAmount", "_MossSharpness", "_MossTiling", "_Sway" })
                Assert.GreaterOrEqual(shader.FindPropertyIndex(prop), 0, "missing bark shader property " + prop);
        }

        [Test]
        public void Bake_SyncsLookIntoManagedMaterials()
        {
            AssetDatabase.CreateFolder("Assets", "__FoliageForgeTest");
            var recipe = ScriptableObject.CreateInstance<TreeRecipe>();
            recipe.ApplyPreset(TreePreset.Oak);
            recipe.Look.GradientJitter = 0.7f;
            recipe.Look.MossAmount = 0.6f;
            recipe.Leaves.Billboard = BillboardMode.YAxis;
            AssetDatabase.CreateAsset(recipe, Folder + "/Oak.asset");

            GameObject prefab = TreeBaker.Bake(recipe);
            Material[] mats = prefab.GetComponent<MeshRenderer>().sharedMaterials;
            Assert.AreEqual(TreeBaker.LoadBarkShader(), mats[0].shader, "bark must use the bark shader");
            Assert.AreEqual(0.6f, mats[0].GetFloat("_MossAmount"), 1e-4f);
            Assert.AreEqual(0.7f, mats[1].GetFloat("_GradientJitter"), 1e-4f);
            Assert.AreEqual(2f, mats[1].GetFloat("_Billboard"));
            Assert.AreEqual(Vector4.zero, mats[1].GetVector("_TexScroll"), "scrolling slides the sprite out of its card");

            recipe.Look.MossAmount = 0.1f;
            TreeBaker.Bake(recipe);
            Assert.AreEqual(0.1f, mats[0].GetFloat("_MossAmount"), 1e-4f, "re-bake must push the recipe values into the saved material");
        }

        [Test]
        public void Bake_AddsLodGroupWithCheaperMeshes()
        {
            AssetDatabase.CreateFolder("Assets", "__FoliageForgeTest");
            var recipe = ScriptableObject.CreateInstance<TreeRecipe>();
            recipe.ApplyPreset(TreePreset.Oak);
            AssetDatabase.CreateAsset(recipe, Folder + "/Oak.asset");

            GameObject prefab = TreeBaker.Bake(recipe);
            var group = prefab.GetComponent<LODGroup>();
            Assert.IsNotNull(group, "prefab needs an LODGroup");
            LOD[] lods = group.GetLODs();
            Assert.AreEqual(3, lods.Length);
            int tris0 = prefab.GetComponent<MeshFilter>().sharedMesh.triangles.Length;
            int tris1 = lods[1].renderers[0].GetComponent<MeshFilter>().sharedMesh.triangles.Length;
            int tris2 = lods[2].renderers[0].GetComponent<MeshFilter>().sharedMesh.triangles.Length;
            Assert.Less(tris1, tris0);
            Assert.Less(tris2, tris1);
            Assert.Greater(lods[0].screenRelativeTransitionHeight, lods[1].screenRelativeTransitionHeight);
            Assert.Greater(lods[1].screenRelativeTransitionHeight, lods[2].screenRelativeTransitionHeight);

            recipe.Lods.Generate = false;
            prefab = TreeBaker.Bake(recipe);
            Assert.IsNull(prefab.GetComponent<LODGroup>());
        }

        [Test]
        public void Intersecting_DoublesTheCardsOfAnUncappedTree()
        {
            var recipe = ScriptableObject.CreateInstance<TreeRecipe>();
            recipe.ApplyPreset(TreePreset.Oak);
            recipe.Leaves.Layout = LeafLayout.Clusters; // Blobs layout ignores Intersecting
            recipe.Budget.MaxCards = 100000;
            recipe.Budget.MaxTriangles = 1000000;
            var mesh = new Mesh();
            recipe.Leaves.Intersecting = false;
            int single = TreeGenerator.Build(recipe, mesh).Cards;
            recipe.Leaves.Intersecting = true;
            int crossed = TreeGenerator.Build(recipe, mesh).Cards;
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(recipe);
            Assert.AreEqual(single * 2, crossed);
        }

        [Test]
        public void Blobs_AutoBlobsFillTheCanopyAndCardsStayInsideThem()
        {
            var recipe = ScriptableObject.CreateInstance<TreeRecipe>();
            recipe.ApplyPreset(TreePreset.Oak);
            recipe.Leaves.BlobCount = 5;
            recipe.Blobs = TreeGenerator.SuggestBlobs(recipe);
            recipe.Leaves.Layout = LeafLayout.Blobs;
            recipe.Leaves.Shell = 1f;
            var mesh = new Mesh();
            TreeStats stats = TreeGenerator.Build(recipe, mesh);
            int count = recipe.Blobs.Count;
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(recipe);
            Assert.AreEqual(5, count);
            Assert.Greater(stats.Cards, 0);
        }

        [Test]
        public void Presets_BlobPresetsLoadWithTheirBlobs()
        {
            foreach (TreePreset preset in new[] { TreePreset.Oak, TreePreset.Maple })
            {
                var recipe = ScriptableObject.CreateInstance<TreeRecipe>();
                recipe.ApplyPreset(preset);
                Assert.AreEqual(LeafLayout.Blobs, recipe.Leaves.Layout, preset.ToString());
                Assert.Greater(recipe.Blobs.Count, 0, preset.ToString());
                Object.DestroyImmediate(recipe);
            }
        }

        [Test]
        public void Blobs_NormalOffsetPushesCardsOutward()
        {
            float Spread(float offset)
            {
                var recipe = ScriptableObject.CreateInstance<TreeRecipe>();
                recipe.ApplyPreset(TreePreset.Oak);
                recipe.Leaves.NormalOffset = offset;
                var mesh = new Mesh();
                TreeGenerator.Build(recipe, mesh);
                float sum = 0f;
                foreach (Vector3 v in mesh.vertices) sum += v.magnitude;
                float mean = sum / mesh.vertexCount;
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(recipe);
                return mean;
            }
            Assert.Greater(Spread(0.4f), Spread(0f));
        }

        [Test]
        public void Shader_StylizedMaterialGetsPatchAndFillValues()
        {
            var recipe = ScriptableObject.CreateInstance<TreeRecipe>();
            recipe.ApplyPreset(TreePreset.Oak);
            recipe.Look.StylizedShading = true;
            recipe.Look.PatchAmount = 0.5f;
            recipe.Look.FillImpact = 0.7f;
            TreeBaker.EnsurePath(Folder);
            AssetDatabase.CreateAsset(recipe, Folder + "/StylizedProbe.asset"); // Bake needs a saved recipe
            GameObject prefab = TreeBaker.Bake(recipe);
            Material leaf = prefab.GetComponent<MeshRenderer>().sharedMaterials[1];
            Assert.AreEqual(0.5f, leaf.GetFloat("_PatchAmount"), 1e-4f);
            Assert.AreEqual(0.7f, leaf.GetFloat("_FillImpact"), 1e-4f);
            recipe.Look.StylizedShading = false;
            prefab = TreeBaker.Bake(recipe);
            leaf = prefab.GetComponent<MeshRenderer>().sharedMaterials[1];
            Assert.AreEqual(0f, leaf.GetFloat("_PatchAmount"), 1e-4f);
            // the recipe is an asset; TearDown deletes the folder
        }

        [Test]
        public void BlobCage_DraggedPointPushesTheSurfaceOut()
        {
            var blob = new CanopyBlob { Position = Vector3.zero, Radii = Vector3.one };
            Vector3 up = new Vector3(0.005f, 1f, 0f).normalized; // a hair off the pole so the ray hits a face, not the shared vertex
            float before = BlobCage.Extent(blob, up);
            blob = BlobCage.EnsureDeform(blob);
            blob.Deform[0] = Vector3.up * 1f; // top pole
            float after = BlobCage.Extent(blob, up);
            Assert.AreEqual(1f, before, 0.1f);
            Assert.AreEqual(before + 1f, after, 0.15f);
        }

        [Test]
        public void Bake_WritesMeshMaterialsAndPrefab()
        {
            AssetDatabase.CreateFolder("Assets", "__FoliageForgeTest");
            var recipe = ScriptableObject.CreateInstance<TreeRecipe>();
            recipe.ApplyPreset(TreePreset.Oak);
            AssetDatabase.CreateAsset(recipe, Folder + "/Oak.asset");

            GameObject prefab = TreeBaker.Bake(recipe);
            Assert.IsNotNull(prefab);
            var renderer = prefab.GetComponent<MeshRenderer>();
            Assert.AreEqual(2, renderer.sharedMaterials.Length);
            Assert.IsNotNull(renderer.sharedMaterials[0]);
            Assert.IsNotNull(renderer.sharedMaterials[1]);
            Assert.AreEqual(TreeBaker.LoadShader(), renderer.sharedMaterials[1].shader);
            Assert.IsNotNull(renderer.sharedMaterials[1].GetTexture("_BaseMap"), "leaf material has no texture");
            Mesh mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(2, mesh.subMeshCount);
            StringAssert.StartsWith(Folder + "/Out/Oak/", AssetDatabase.GetAssetPath(prefab), "prefab must land in the output folder");
            CollectionAssert.Contains(TreeBaker.BakedPrefabs(), prefab, "overview must list the baked prefab");
            Assert.AreEqual(recipe, TreeBaker.RecipeFor(prefab));

            GameObject again = TreeBaker.Bake(recipe);
            Assert.AreEqual(AssetDatabase.GetAssetPath(prefab), AssetDatabase.GetAssetPath(again));
            Assert.AreEqual(mesh, again.GetComponent<MeshFilter>().sharedMesh, "re-bake must keep the same mesh asset");
        }

        [Test]
        public void Window_RebindingRecipes_DoesNotThrow()
        {
            var a = ScriptableObject.CreateInstance<TreeRecipe>();
            var b = ScriptableObject.CreateInstance<TreeRecipe>();
            var window = EditorWindow.GetWindow<FoliageForgeWindow>();
            try
            {
                FoliageForgeWindow.Open(a);
                FoliageForgeWindow.Open(b);
                FoliageForgeWindow.Open(a);
            }
            finally
            {
                window.Close();
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void Window_LoadingAPickedPrefab_LoadsItsRecipeAndHasNothingToOverwrite()
        {
            TreeBaker.EnsurePath(Folder);
            TreeRecipe oak = ScriptableObject.CreateInstance<TreeRecipe>(), pine = ScriptableObject.CreateInstance<TreeRecipe>();
            oak.ApplyPreset(TreePreset.Oak);
            pine.ApplyPreset(TreePreset.Spruce);
            AssetDatabase.CreateAsset(oak, Folder + "/Oak.asset");
            AssetDatabase.CreateAsset(pine, Folder + "/Pine.asset");
            TreeBaker.Bake(oak);
            GameObject pinePrefab = TreeBaker.Bake(pine);
            var window = EditorWindow.GetWindow<FoliageForgeWindow>();
            try
            {
                FoliageForgeWindow.Open(oak);
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var type = typeof(FoliageForgeWindow);
                type.GetField("_picked", flags).SetValue(window, pinePrefab);
                type.GetMethod("LoadPicked", flags).Invoke(window, null);
                Assert.AreSame(pine, type.GetField("_source", flags).GetValue(window), "the picked prefab's recipe is linked");
                var working = (TreeRecipe)type.GetField("_recipe", flags).GetValue(window);
                Assert.AreNotSame(pine, working, "edits go to a temporary copy");
                Assert.AreEqual(pine.Leaves.Layout, working.Leaves.Layout);
                Assert.AreEqual(pine.Seed, working.Seed);
                var button = (UnityEditor.UIElements.ToolbarButton)type.GetField("_bakeButton", flags).GetValue(window);
                Assert.AreEqual("Prefab Up To Date", button.text);

                int savedSeed = pine.Seed;
                working.Seed = savedSeed + 7;
                type.GetMethod("RefreshBakeButton", flags).Invoke(window, null);
                Assert.AreEqual(savedSeed, pine.Seed, "editing does not touch the saved recipe");
                StringAssert.StartsWith("Overwrite Prefab", button.text);

                type.GetMethod("Bake", flags).Invoke(window, null);
                Assert.AreEqual(savedSeed + 7, pine.Seed, "Bake writes the working copy into the saved recipe");
                type.GetMethod("RefreshBakeButton", flags).Invoke(window, null);
                Assert.AreEqual("Prefab Up To Date", button.text);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void Blobs_RandomizeIsSeededAndSizedByItsSettings()
        {
            var recipe = ScriptableObject.CreateInstance<TreeRecipe>();
            recipe.ApplyPreset(TreePreset.Oak);
            var a = TreeGenerator.RandomBlobs(recipe, 9, 1f, 0.8f, 0.7f, 5);
            var b = TreeGenerator.RandomBlobs(recipe, 9, 1f, 0.8f, 0.7f, 5);
            var c = TreeGenerator.RandomBlobs(recipe, 9, 1f, 0.8f, 0.7f, 6);
            Assert.AreEqual(9, a.Count);
            Assert.AreEqual(a[3].Position, b[3].Position, "same seed, same blobs");
            Assert.AreNotEqual(a[3].Position, c[3].Position, "another seed rolls again");
            Assert.Less(a[4].Radii.y, a[4].Radii.x * 1.3f, "flatness squashes the blobs");
            Assert.AreEqual(LeafLayout.Blobs, TreeRecipeFor(TreePreset.Oak).Leaves.Layout, "Oak is a blob canopy");
        }

        [Test]
        public void Blobs_RotationTurnsTheCage()
        {
            var blob = new CanopyBlob { Position = Vector3.zero, Radii = new Vector3(2f, 1f, 1f), Rotation = new Vector3(0f, 90f, 0f) };
            Vector3 east = BlobCage.World(blob, 9); // equator point on +x before rotation
            Assert.AreEqual(0f, east.x, 0.01f);
            Assert.AreEqual(2f * 1.08f, Mathf.Abs(east.z), 0.01f, "the long axis now points along z");
            Assert.AreEqual(2f, BlobCage.Extent(blob, Vector3.forward), 0.05f);
            Assert.AreEqual(1f, BlobCage.Extent(blob, Vector3.right), 0.05f);
        }

        private static TreeRecipe TreeRecipeFor(TreePreset preset)
        {
            var r = ScriptableObject.CreateInstance<TreeRecipe>();
            r.ApplyPreset(preset);
            return r;
        }

        [Test]
        public void Generation_EveryPreset_StaysInsideBudget()
        {
            foreach (TreePreset preset in System.Enum.GetValues(typeof(TreePreset)))
            {
                var recipe = ScriptableObject.CreateInstance<TreeRecipe>();
                recipe.ApplyPreset(preset);
                var mesh = new Mesh();
                TreeGenerator.Build(recipe, mesh);
                var sw = Stopwatch.StartNew();
                const int runs = 5;
                TreeStats stats = default;
                for (int i = 0; i < runs; i++) stats = TreeGenerator.Build(recipe, mesh);
                sw.Stop();
                double ms = sw.Elapsed.TotalMilliseconds / runs;
                Debug.Log($"[FF perf] {preset}: {stats.Triangles} tris, {stats.Vertices} verts, {stats.Cards} cards (-{stats.CardsDropped}), {stats.MeshBytes / 1024f:0.#} KB, {stats.Size.x:0.0}x{stats.Size.y:0.0}x{stats.Size.z:0.0} m, {ms:0.0} ms/build");
                Assert.LessOrEqual(stats.Triangles, recipe.Budget.MaxTriangles, preset + " exceeds the triangle budget");
                Assert.LessOrEqual(stats.Cards, recipe.Budget.MaxCards);
                Assert.Less(ms, 150.0, preset + " takes too long to rebuild for live editing");
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(recipe);
            }
        }
    }
}
