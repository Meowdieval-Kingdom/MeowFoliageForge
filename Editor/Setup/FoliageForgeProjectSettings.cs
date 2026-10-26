using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Meowdieval.FoliageForge
{
    /// <summary>How much a setup row matters: Required rows break a shader or feature, Recommended rows cost quality or speed, Optional rows are taste.</summary>
    public enum SetupSeverity { Required, Recommended, Optional }

    /// <summary>One URP asset field Foliage Forge cares about. Read and written through SerializedObject, since several URP setters are internal.</summary>
    public sealed class PipelineSetting
    {
        /// <summary>Row title, serialized field name on UniversalRenderPipelineAsset, reason shown in the wizard, and target text.</summary>
        public string Name, Path, Why, Target;
        /// <summary>How much this setting matters.</summary>
        public SetupSeverity Severity;
        /// <summary>Whether a value (bools as 0/1, enums as their int value) meets the target.</summary>
        public Func<float, bool> Meets;
        /// <summary>The value the fix writes.</summary>
        public float FixValue;
        /// <summary>Turns a value into display text.</summary>
        public Func<float, string> Format;
        /// <summary>Optional bool field the fix also sets to true (URP only keeps it in sync through its C# setter).</summary>
        public string AlsoTrue;
    }

    /// <summary>Render-pipeline and project-settings checks for the Setup Wizard. Nothing here changes a setting until a Fix is clicked.</summary>
    public static class FoliageForgeProjectSettings
    {
        /// <summary>Layer the brush and grass land on by default.</summary>
        public const string GroundLayer = "Ground";
        const int FirstUserLayer = 8;
        const string TagManagerPath = "ProjectSettings/TagManager.asset";

        /// <summary>When true, pipeline checks and fixes cover every URP asset used by a quality level, not just the current one.</summary>
        public static bool AllQualityLevels;

        /// <summary>The URP asset fields Foliage Forge checks. MSAA is Required when a Grass Profile uses alpha to coverage; Additional Lights is Recommended when one uses additional lights.</summary>
        public static List<PipelineSetting> Settings(bool alphaToCoverage, bool additionalLights)
        {
            Func<float, bool> on = v => v > 0.5f;
            Func<float, string> onOff = v => v > 0.5f ? "On" : "Off";
            Func<float, string> lightMode = v => v < 0.5f ? "Disabled" : v < 1.5f ? "Per Pixel" : "Per Vertex";
            return new List<PipelineSetting>
            {
                new PipelineSetting { Name = "Main light per pixel", Path = "m_MainLightRenderingMode", Severity = SetupSeverity.Required, Meets = v => Mathf.Approximately(v, 1f), FixValue = 1f, Format = lightMode, Target = "Per Pixel", Why = "Grass and trees are lit and shadowed by the main light." },
                new PipelineSetting { Name = "LOD Cross Fade", Path = "m_EnableLODCrossFade", Severity = SetupSeverity.Required, Meets = on, FixValue = 1f, Format = onOff, Target = "On", Why = "Baked trees fade between LODs; off, they pop." },
                new PipelineSetting { Name = "MSAA", Path = "m_MSAA", Severity = alphaToCoverage ? SetupSeverity.Required : SetupSeverity.Recommended, Meets = v => v >= 4f, FixValue = 4f, Format = v => v <= 1f ? "Off" : v + "x", Target = "4x or more", Why = "Alpha to coverage grass edges need multisampling; without it they alias." },
                new PipelineSetting { Name = "SRP Batcher", Path = "m_UseSRPBatcher", Severity = SetupSeverity.Recommended, Meets = on, FixValue = 1f, Format = onOff, Target = "On", Why = "Batches the tree Shader Graph materials." },
                new PipelineSetting { Name = "Depth Texture", Path = "m_RequireDepthTexture", Severity = SetupSeverity.Recommended, Meets = on, FixValue = 1f, Format = onOff, Target = "On", Why = "SSAO and other depth effects see grass and trees through their depth passes." },
                new PipelineSetting { Name = "Main light shadows", Path = "m_MainLightShadowsSupported", AlsoTrue = "m_AnyShadowsSupported", Severity = SetupSeverity.Recommended, Meets = on, FixValue = 1f, Format = onOff, Target = "On", Why = "Trees and grass cast and receive sun shadows." },
                new PipelineSetting { Name = "Shadow distance", Path = "m_ShadowDistance", Severity = SetupSeverity.Optional, Meets = v => v >= 30f && v <= 150f, FixValue = 50f, Format = v => v + " m", Target = "30-150 m (fix sets 50 m)", Why = "Grass fades its shadows out toward this distance; too far blurs the shadow map." },
                new PipelineSetting { Name = "Soft shadows", Path = "m_SoftShadowsSupported", Severity = SetupSeverity.Optional, Meets = on, FixValue = 1f, Format = onOff, Target = "On", Why = "Softer leaf and blade shadow edges." },
                new PipelineSetting { Name = "Additional lights per pixel", Path = "m_AdditionalLightsRenderingMode", Severity = additionalLights ? SetupSeverity.Recommended : SetupSeverity.Optional, Meets = v => Mathf.Approximately(v, 1f), FixValue = 1f, Format = lightMode, Target = "Per Pixel", Why = additionalLights ? "A Grass Profile uses Features > Additional Lights (point and spot lights on grass)." : "Only needed if a Grass Profile turns on Features > Additional Lights." },
            };
        }

        /// <summary>First empty user layer slot (8-31), or -1 when all are taken.</summary>
        public static int FindFreeLayerSlot(IReadOnlyList<string> layers)
        {
            for (int i = FirstUserLayer; i < 32 && i < layers.Count; i++)
                if (string.IsNullOrWhiteSpace(layers[i])) return i;
            return -1;
        }

        /// <summary>The pipeline asset of the current quality level, falling back to the Graphics default.</summary>
        public static RenderPipelineAsset CurrentPipeline() => PipelineAt(QualitySettings.GetQualityLevel());

        /// <summary>True for a UniversalRenderPipelineAsset.</summary>
        public static bool IsUrp(RenderPipelineAsset rp) => rp != null && rp.GetType().Name.Contains("Universal");

        /// <summary>Distinct URP assets the pipeline rows check and fix: the current one, plus those of all quality levels when <see cref="AllQualityLevels"/> is set.</summary>
        public static List<RenderPipelineAsset> Targets()
        {
            var list = new List<RenderPipelineAsset>();
            RenderPipelineAsset current = CurrentPipeline();
            if (IsUrp(current)) list.Add(current);
            if (AllQualityLevels)
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    RenderPipelineAsset rp = PipelineAt(i);
                    if (IsUrp(rp) && !list.Contains(rp)) list.Add(rp);
                }
            return list;
        }

        private static RenderPipelineAsset PipelineAt(int level)
        {
            RenderPipelineAsset rp = QualitySettings.GetRenderPipelineAssetAt(level);
            return rp != null ? rp : GraphicsSettings.defaultRenderPipeline;
        }

        internal static void AddChecks(List<SetupCheck> list)
        {
            List<RenderPipelineAsset> targets = Targets();
            if (targets.Count > 0)
            {
                GrassUse(out bool a2c, out bool lights);
                string assets = string.Join(", ", targets.Select(t => t.name));
                List<SerializedObject> objects = targets.Select(t => new SerializedObject(t)).ToList();
                foreach (PipelineSetting s in Settings(a2c, lights))
                {
                    SerializedProperty shown = objects[0].FindProperty(s.Path);
                    if (shown == null)
                    {
                        list.Add(new SetupCheck { Name = s.Name, Severity = s.Severity, Detail = $"{s.Path} not found on {targets[0].name} (URP renamed it?). Set it by hand." });
                        continue;
                    }
                    PipelineSetting setting = s;
                    list.Add(new SetupCheck
                    {
                        Name = s.Name, Severity = s.Severity, Detail = s.Why,
                        Ok = objects.All(o =>
                        {
                            SerializedProperty p = o.FindProperty(setting.Path);
                            return p != null && setting.Meets(Read(p));
                        }),
                        Current = s.Format(Read(shown)), Target = s.Target,
                        SharedAsset = assets, FixLabel = "Fix in " + assets, Fix = () => Apply(targets, setting),
                    });
                }
            }

            int ground = LayerMask.NameToLayer(GroundLayer);
            list.Add(new SetupCheck
            {
                Name = "Ground layer", Severity = SetupSeverity.Recommended, Ok = ground >= 0,
                Detail = ground >= 0 ? $"Layer {ground}." : "The brush and grass land on it by default. The fix adds it to the first free user layer.",
                Current = ground >= 0 ? "Present" : "Missing", Target = "Present",
                SharedAsset = "Tags and Layers", FixLabel = "Add in Tags and Layers", Fix = AddGroundLayer,
            });

            var manager = Object.FindFirstObjectByType<FoliageForgeManager>();
            if (ground >= 0 && manager != null)
            {
                int bit = 1 << ground;
                bool ok = (manager.Ground.value & bit) != 0;
                list.Add(new SetupCheck
                {
                    Name = "Manager Ground mask includes Ground", Severity = SetupSeverity.Recommended, Ok = ok,
                    Detail = ok ? manager.name : "Grass and the brush ignore colliders on the Ground layer.",
                    Current = ok ? "Included" : "Not included", Target = "Included",
                    FixLabel = "Add Ground", Fix = () => AddToMask(manager, bit),
                });
            }
        }

        private static void GrassUse(out bool alphaToCoverage, out bool additionalLights)
        {
            alphaToCoverage = additionalLights = false;
            foreach (string guid in AssetDatabase.FindAssets("t:GrassProfile"))
            {
                var profile = AssetDatabase.LoadAssetAtPath<GrassProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile == null || profile.Features == null) continue;
                alphaToCoverage |= profile.Features.AlphaToCoverage;
                additionalLights |= profile.Features.AdditionalLights;
            }
        }

        private static float Read(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return p.boolValue ? 1f : 0f;
                case SerializedPropertyType.Float:
                    return p.floatValue;
                default:
                    return p.intValue;
            }
        }

        private static void Write(SerializedProperty p, float v)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    p.boolValue = v > 0.5f;
                    break;
                case SerializedPropertyType.Float:
                    p.floatValue = v;
                    break;
                default:
                    p.intValue = Mathf.RoundToInt(v);
                    break;
            }
        }

        private static void Apply(List<RenderPipelineAsset> targets, PipelineSetting s)
        {
            foreach (RenderPipelineAsset t in targets)
            {
                var so = new SerializedObject(t);
                SerializedProperty p = so.FindProperty(s.Path);
                if (p == null || s.Meets(Read(p))) continue;
                string old = s.Format(Read(p));
                Write(p, s.FixValue);
                if (s.AlsoTrue != null)
                {
                    SerializedProperty also = so.FindProperty(s.AlsoTrue);
                    if (also != null) also.boolValue = true;
                }
                so.ApplyModifiedProperties();
                Undo.SetCurrentGroupName("Foliage Forge: " + s.Name);
                EditorUtility.SetDirty(t);
                Debug.Log($"[Foliage Forge] Set {t.name} {s.Name}: {old} -> {s.Format(s.FixValue)}", t);
            }
            AssetDatabase.SaveAssets();
        }

        private static void AddGroundLayer()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
            if (assets.Length == 0)
            {
                Debug.LogError("[Foliage Forge] Could not load " + TagManagerPath);
                return;
            }
            var so = new SerializedObject(assets[0]);
            SerializedProperty layers = so.FindProperty("layers");
            var names = new string[layers.arraySize];
            for (int i = 0; i < names.Length; i++) names[i] = layers.GetArrayElementAtIndex(i).stringValue;
            int slot = FindFreeLayerSlot(names);
            if (slot < 0)
            {
                Debug.LogWarning("[Foliage Forge] No free user layer (8-31) for Ground. Free one in Project Settings > Tags and Layers.");
                return;
            }
            layers.GetArrayElementAtIndex(slot).stringValue = GroundLayer;
            so.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Foliage Forge: Add Ground layer");
            AssetDatabase.SaveAssets();
            Debug.Log($"[Foliage Forge] Set Tags and Layers layer {slot}: (empty) -> {GroundLayer}");
        }

        private static void AddToMask(FoliageForgeManager manager, int bit)
        {
            Undo.RecordObject(manager, "Add Ground to Foliage Forge mask");
            manager.Ground = manager.Ground.value | bit;
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            if (manager.Grass != null)
            {
                Undo.RecordObject(manager.Grass, "Add Ground to Foliage Forge mask");
                manager.Grass.Ground = manager.Ground;
                PrefabUtility.RecordPrefabInstancePropertyModifications(manager.Grass);
            }
        }
    }
}
