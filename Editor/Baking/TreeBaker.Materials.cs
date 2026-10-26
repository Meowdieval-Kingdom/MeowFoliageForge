using System.IO;
using UnityEditor;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    public static partial class TreeBaker
    {
        /// <summary>The material per submesh: bark, leaf, then the core shell when the recipe bakes it.</summary>
        public static Material[] MaterialSlots(TreeRecipe recipe)
        {
            LookSettings look = recipe.Look;
            var list = new System.Collections.Generic.List<Material> { look.Bark, look.Leaf };
            if (TreeGenerator.HasCore(recipe)) list.Add(look.CoreMaterial);
            return list.ToArray();
        }

        /// <summary>Fills empty bark and leaf slots with new materials saved beside the baked mesh, then syncs them to the recipe.</summary>
        public static void EnsureMaterials(TreeRecipe recipe, string folder, string name)
        {
            Shader leafShader = LoadShader(), barkShader = LoadBarkShader();
            if (leafShader == null || barkShader == null) return;
            LookSettings look = recipe.Look;
            if (look.Bark == null) look.Bark = SaveMaterial($"{folder}/{name}_Bark.mat", barkShader);
            if (look.Leaf == null) look.Leaf = SaveMaterial($"{folder}/{name}_Leaf.mat", leafShader);
            if (TreeGenerator.HasCore(recipe) && look.CoreMaterial == null) look.CoreMaterial = SaveMaterial($"{folder}/{name}_Core.mat", leafShader);
            SyncMaterials(recipe);
        }

        /// <summary>Writes the recipe's Look settings into its tool-managed materials. Materials you assigned yourself are left alone.</summary>
        public static void SyncMaterials(TreeRecipe recipe)
        {
            LookSettings look = recipe.Look;
            if (IsManaged(look.Bark))
            {
                ConfigureBark(look.Bark, recipe);
                EditorUtility.SetDirty(look.Bark);
            }
            if (IsManaged(look.Leaf))
            {
                ConfigureLeaf(look.Leaf, recipe);
                EditorUtility.SetDirty(look.Leaf);
            }
            if (IsManaged(look.CoreMaterial))
            {
                ConfigureLeaf(look.CoreMaterial, recipe, true);
                EditorUtility.SetDirty(look.CoreMaterial);
            }
        }

        /// <summary>Unsaved materials for the window preview; they follow the recipe live unless you assigned your own.</summary>
        public static Material[] PreviewMaterials(TreeRecipe recipe)
        {
            Shader leafShader = LoadShader(), barkShader = LoadBarkShader();
            if (leafShader == null || barkShader == null) return new Material[2];
            LookSettings look = recipe.Look;
            Material bark = look.Bark != null && !IsManaged(look.Bark) ? look.Bark : TempMaterial(barkShader, m => ConfigureBark(m, recipe));
            Material leaf = look.Leaf != null && !IsManaged(look.Leaf) ? look.Leaf : TempMaterial(leafShader, m => ConfigureLeaf(m, recipe));
            var list = new System.Collections.Generic.List<Material> { bark, leaf };
            if (TreeGenerator.HasCore(recipe))
                list.Add(look.CoreMaterial != null && !IsManaged(look.CoreMaterial) ? look.CoreMaterial : TempMaterial(leafShader, m => ConfigureLeaf(m, recipe, true)));
            return list.ToArray();
        }

        private static Material TempMaterial(Shader shader, System.Action<Material> configure)
        {
            var m = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            configure(m);
            return m;
        }

        private static Material SaveMaterial(string path, Shader shader)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static void ConfigureBark(Material m, TreeRecipe recipe)
        {
            Shader shader = LoadBarkShader();
            if (shader != null && m.shader != shader) m.shader = shader;
            LookSettings look = recipe.Look;
            m.SetTexture(BaseMapId, look.BarkTexture);
            m.SetTexture(MossMapId, look.MossTexture);
            m.SetColor(BottomId, look.BarkBottom);
            m.SetColor(TopId, look.BarkTop);
            m.SetFloat(TilingId, look.BarkTiling);
            m.SetColor(MossColorId, look.MossColor);
            m.SetFloat(MossAmountId, look.MossAmount);
            m.SetFloat(MossSharpnessId, look.MossSharpness);
            m.SetFloat(MossTilingId, look.MossTiling);
        }

        // core = the inner shell: always solid and square, so the round card mask never cuts it
        private static void ConfigureLeaf(Material m, TreeRecipe recipe, bool core = false)
        {
            LookSettings look = recipe.Look;
            string path = AssetDatabase.GetAssetPath(m);
            Texture2D map = look.LeafTexture != null ? look.LeafTexture
                : string.IsNullOrEmpty(path) ? LeafTexture.Temporary() : LeafTexture.GetOrCreate(Path.GetDirectoryName(path).Replace('\\', '/'));
            m.SetTexture(BaseMapId, map);
            m.SetColor(BottomId, look.LeafBottom);
            m.SetColor(TopId, look.LeafTop);
            m.SetFloat(BiasId, look.GradientBias);
            m.SetFloat(JitterId, look.GradientJitter);
            m.SetFloat(VariationId, look.ColorVariation);
            m.SetFloat(WrapId, look.Wrap);
            m.SetFloat(TransId, look.Translucency);
            m.SetColor(TransColorId, look.TranslucencyColor);
            m.SetFloat(TransFocusId, look.TranslucencyFocus);
            m.SetColor(PatchColorId, look.PatchColor);
            m.SetFloat(PatchAmountId, look.StylizedShading ? look.PatchAmount : 0f);
            m.SetFloat(PatchScaleId, look.PatchScale);
            m.SetTexture(FillMapId, look.StylizedShading ? look.FillTexture : null);
            m.SetFloat(FillScaleId, look.FillScale);
            m.SetFloat(FillImpactId, look.FillImpact);
            m.SetColor(ShadowColorId, look.ShadowColor);
            m.SetFloat(ShadowAmountId, look.StylizedShading ? look.ShadowAmount : 0f);
            m.SetFloat(ShadowSoftnessId, look.ShadowSoftness);
            m.SetFloat(BandsId, look.StylizedShading ? look.Bands : 0f);
            m.SetFloat(SkyToneId, look.StylizedShading ? look.SkyTone : 0f);
            m.SetFloat(SolidId, look.SolidCards || core ? 1f : 0f);
            m.SetFloat(SolidShapeId, core ? 0f : (float)look.SolidShape);
            m.SetFloat(ContinuousId, look.ContinuousColor ? 1f : 0f);
            m.SetFloat(GrainId, look.GrainAmount);
            m.SetColor(RimColorId, look.RimColor);
            m.SetFloat(RimStrengthId, look.StylizedShading ? look.RimStrength : 0f);
            m.SetFloat(RimPowerId, look.RimPower);
            m.SetFloat(HueShiftId, look.StylizedShading ? look.HueShift : 0f);
            m.SetFloat(TreeVariationId, look.StylizedShading ? look.TreeVariation : 0f);
            m.SetFloat(BillboardId, (float)recipe.Leaves.Billboard);
            m.SetVector(ScrollId, Vector4.zero); // a scrolling sprite slides out of its own card
        }
    }
}
