using System.IO;
using UnityEditor;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Turns a recipe into saved assets: a mesh, two materials and a prefab, next to the recipe.</summary>
    public static partial class TreeBaker
    {
        public const string ShaderPath = "Packages/com.meowdieval.foliageforge/Shaders/FoliageForge.shadergraph";
        public const string BarkShaderPath = "Packages/com.meowdieval.foliageforge/Shaders/FoliageForgeBark.shadergraph";

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MossMapId = Shader.PropertyToID("_MossMap");
        static readonly int BottomId = Shader.PropertyToID("_BottomColor");
        static readonly int TopId = Shader.PropertyToID("_TopColor");
        static readonly int BiasId = Shader.PropertyToID("_GradientBias");
        static readonly int JitterId = Shader.PropertyToID("_GradientJitter");
        static readonly int VariationId = Shader.PropertyToID("_ColorVariation");
        static readonly int WrapId = Shader.PropertyToID("_Wrap");
        static readonly int TransId = Shader.PropertyToID("_Translucency");
        static readonly int TransColorId = Shader.PropertyToID("_TranslucencyColor");
        static readonly int TransFocusId = Shader.PropertyToID("_TranslucencyFocus");
        static readonly int PatchColorId = Shader.PropertyToID("_PatchColor");
        static readonly int PatchAmountId = Shader.PropertyToID("_PatchAmount");
        static readonly int PatchScaleId = Shader.PropertyToID("_PatchScale");
        static readonly int FillMapId = Shader.PropertyToID("_FillMap");
        static readonly int FillScaleId = Shader.PropertyToID("_FillScale");
        static readonly int FillImpactId = Shader.PropertyToID("_FillImpact");
        static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
        static readonly int ShadowAmountId = Shader.PropertyToID("_ShadowAmount");
        static readonly int ShadowSoftnessId = Shader.PropertyToID("_ShadowSoftness");
        static readonly int BandsId = Shader.PropertyToID("_Bands");
        static readonly int SkyToneId = Shader.PropertyToID("_SkyTone");
        static readonly int SolidId = Shader.PropertyToID("_Solid");
        static readonly int SolidShapeId = Shader.PropertyToID("_SolidShape");
        static readonly int ContinuousId = Shader.PropertyToID("_ContinuousColor");
        static readonly int GrainId = Shader.PropertyToID("_GrainAmount");
        static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
        static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
        static readonly int HueShiftId = Shader.PropertyToID("_HueShift");
        static readonly int TreeVariationId = Shader.PropertyToID("_TreeVariation");
        static readonly int BillboardId = Shader.PropertyToID("_Billboard");
        static readonly int ScrollId = Shader.PropertyToID("_TexScroll");
        static readonly int TilingId = Shader.PropertyToID("_Tiling");
        static readonly int MossColorId = Shader.PropertyToID("_MossColor");
        static readonly int MossAmountId = Shader.PropertyToID("_MossAmount");
        static readonly int MossSharpnessId = Shader.PropertyToID("_MossSharpness");
        static readonly int MossTilingId = Shader.PropertyToID("_MossTiling");

        /// <summary>The foliage shader, or null if the package assets are not imported yet.</summary>
        public static Shader LoadShader() => AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

        /// <summary>The trunk and branch shader (UV-mapped texture and moss), or null if not imported yet.</summary>
        public static Shader LoadBarkShader() => AssetDatabase.LoadAssetAtPath<Shader>(BarkShaderPath);

        const string OutputKey = "FoliageForge.OutputFolder.v2"; // v2: default moved to "Foliage Forge"; older saved choices are dropped
        const string DefaultOutput = "Assets/Foliage Forge";

        /// <summary>Project folder that receives one subfolder (mesh, materials, prefab) per baked recipe.</summary>
        public static string OutputFolder
        {
            get
            {
                string f = EditorPrefs.GetString(OutputKey, DefaultOutput);
                return f.StartsWith("Assets") ? f.TrimEnd('/') : DefaultOutput;
            }
            set => EditorPrefs.SetString(OutputKey, value);
        }

        /// <summary>True for the materials this tool saves beside a baked mesh; those follow the recipe's Look settings.</summary>
        public static bool IsManaged(Material m)
        {
            if (m == null) return false;
            string path = AssetDatabase.GetAssetPath(m);
            return path.StartsWith(OutputFolder + "/") || path.Contains("_Baked/");
        }

        /// <summary>Every prefab baked into <see cref="OutputFolder"/>.</summary>
        public static GameObject[] BakedPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder)) return new GameObject[0];
            var list = new System.Collections.Generic.List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { OutputFolder }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go != null && go.GetComponent<MeshFilter>() != null) list.Add(go);
            }
            // trees first, then grass batches, each by name
            list.Sort((a, b) =>
            {
                int byType = (a.GetComponent<GrassBatch>() != null).CompareTo(b.GetComponent<GrassBatch>() != null);
                return byType != 0 ? byType : string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase);
            });
            return list.ToArray();
        }

        /// <summary>The recipe a baked prefab came from (matched by name), or null.</summary>
        public static TreeRecipe RecipeFor(GameObject prefab)
        {
            foreach (string guid in AssetDatabase.FindAssets($"{prefab.name} t:TreeRecipe"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == prefab.name) return AssetDatabase.LoadAssetAtPath<TreeRecipe>(path);
            }
            return null;
        }

        /// <summary>Creates every missing folder of an Assets/... path.</summary>
        public static string EnsurePath(string path)
        {
            string current = "Assets";
            foreach (string part in path.Substring("Assets".Length).Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
            return current;
        }

        /// <summary>
        /// Writes <c>{OutputFolder}/{recipe}/</c> with mesh, materials and prefab. Existing assets are
        /// updated in place so scene references and material tweaks survive a re-bake.
        /// </summary>
        /// <returns>The prefab asset, or null when the recipe is not a saved asset.</returns>
        public static GameObject Bake(TreeRecipe recipe)
        {
            string recipePath = AssetDatabase.GetAssetPath(recipe);
            if (string.IsNullOrEmpty(recipePath)) return null;

            string folder = EnsurePath($"{OutputFolder}/{recipe.name}");
            EnsureMaterials(recipe, folder, recipe.name);
            GameObject prefab = BakePrefab(recipe, folder, recipe.name);
            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        // mesh, LODs and prefab for one recipe (or variant copy) under the given name; its Look materials must be filled already
        private static GameObject BakePrefab(TreeRecipe recipe, string folder, string name)
        {
            Mesh mesh = SaveMesh(recipe, $"{folder}/{name}.asset");

            string prefabPath = $"{folder}/{name}.prefab";
            var root = new GameObject(name);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = MaterialSlots(recipe);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            if (recipe.Budget.AddTrunkCollider)
            {
                var col = root.AddComponent<CapsuleCollider>();
                float h = recipe.Trunk.Height * 0.5f;
                col.center = new Vector3(0f, h, 0f);
                col.height = recipe.Trunk.Height;
                col.radius = recipe.Trunk.BaseRadius;
            }

            AddLods(recipe, root, renderer, folder, name);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Mesh SaveMesh(TreeRecipe recipe, string path)
        {
            Mesh fresh = new Mesh();
            TreeGenerator.Build(recipe, fresh);
            fresh.name = System.IO.Path.GetFileNameWithoutExtension(path);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            EditorUtility.CopySerialized(fresh, mesh);
            Object.DestroyImmediate(fresh);
            return mesh;
        }

        /// <summary>A cheaper copy of the recipe for a LOD level (1 or 2); the recipe asset itself is untouched.</summary>
        public static TreeRecipe LodRecipe(TreeRecipe recipe, int level)
        {
            TreeRecipe r = Object.Instantiate(recipe);
            r.name = recipe.name;
            LodSettings lod = recipe.Lods;
            float share = level == 1 ? lod.Lod1CardShare : lod.Lod2CardShare;
            bool crossed = level == 2 && lod.Lod2Intersecting;
            // dropping the twig level leaves fewer clusters, so each one carries the cards of the twigs it replaces
            int clusterFactor = recipe.Branches.Levels >= 2 ? Mathf.Max(1, recipe.Branches.SubCount) : 1;
            r.Branches.Levels = Mathf.Min(recipe.Branches.Levels, 1);
            r.Branches.MinSides = 3;
            r.Leaves.CardsPerBranch = Mathf.Max(1, Mathf.RoundToInt(recipe.Leaves.CardsPerBranch * clusterFactor * share / (crossed ? 2f : 1f)));
            r.Leaves.CardSize = recipe.Leaves.CardSize * Mathf.Min(1f / Mathf.Sqrt(share), 2.5f);
            if (recipe.Leaves.CardsPerBlob > 0) r.Leaves.CardsPerBlob = Mathf.Max(1, Mathf.RoundToInt(recipe.Leaves.CardsPerBlob * share / (crossed ? 2f : 1f))); // 0 = automatic: the larger cards above cover the blob with fewer groups
            if (recipe.Look.CoreMesh)
            {
                r.Look.CoreSegments = Mathf.Max(4, Mathf.RoundToInt(recipe.Look.CoreSegments * (level == 1 ? 0.6f : 0.4f)));
                r.Leaves.GroupQuads = level == 1 ? Mathf.Min(2, recipe.Leaves.GroupQuads) : 1;
            }
            r.Budget.MaxTriangles = Mathf.RoundToInt(recipe.Budget.MaxTriangles * (level == 1 ? 0.4f : 0.13f));
            r.Leaves.Intersecting = crossed;
            r.Trunk.RadialSides = Mathf.Max(4, recipe.Trunk.RadialSides / (level == 1 ? 2 : 3));
            r.Trunk.HeightSegments = Mathf.Max(3, Mathf.RoundToInt(recipe.Trunk.HeightSegments * (level == 1 ? 0.6f : 0.4f)));
            r.Branches.Segments = Mathf.Max(2, recipe.Branches.Segments - level);
            if (level == 2) r.Trunk.Roots = 0; // hidden under grass at that distance
            // shaped cards cost triangles: LOD1 halves their splits, LOD2 goes back to flat quads
            r.LeafShapes.WidthSegments = level == 1 ? Mathf.Max(1, recipe.LeafShapes.WidthSegments / 2) : 1;
            r.LeafShapes.LengthSegments = level == 1 ? Mathf.Max(1, recipe.LeafShapes.LengthSegments / 2) : 1;
            if (level == 2) r.LeafShapes.Fold = r.LeafShapes.Curl = r.LeafShapes.Twist = 0f;
            return r;
        }

        private static void AddLods(TreeRecipe recipe, GameObject root, MeshRenderer lod0, string folder, string name)
        {
            string[] meshPaths = { $"{folder}/{name}_LOD1.asset", $"{folder}/{name}_LOD2.asset" };
            if (!recipe.Lods.Generate)
            {
                foreach (string p in meshPaths) AssetDatabase.DeleteAsset(p);
                return;
            }

            var renderers = new Renderer[3];
            renderers[0] = lod0;
            for (int level = 1; level <= 2; level++)
            {
                TreeRecipe lodRecipe = LodRecipe(recipe, level);
                Mesh mesh = SaveMesh(lodRecipe, meshPaths[level - 1]);
                Object.DestroyImmediate(lodRecipe);
                var child = new GameObject("LOD" + level);
                child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = child.AddComponent<MeshRenderer>();
                r.sharedMaterials = lod0.sharedMaterials;
                r.shadowCastingMode = level == 1 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
                renderers[level] = r;
            }

            var group = root.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.CrossFade; // dithered blend, the shader graphs support LOD crossfade
            group.animateCrossFading = true;
            LodSettings l = recipe.Lods;
            float lod2 = Mathf.Min(l.Lod2Screen, l.Lod1Screen * 0.9f);
            group.SetLODs(new[]
            {
                new LOD(l.Lod1Screen, new[] { renderers[0] }),
                new LOD(lod2, new[] { renderers[1] }),
                new LOD(Mathf.Min(l.CullScreen, lod2 * 0.9f), new[] { renderers[2] }),
            });
            group.RecalculateBounds();
        }
    }
}
