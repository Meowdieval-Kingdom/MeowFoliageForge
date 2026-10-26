using UnityEditor;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// Grass profile inspector, also shown in the Foliage Forge window's Grass tab: look (biome, quality), grass types, grouped feature
    /// switches, then the shared settings.
    /// </summary>
    [CustomEditor(typeof(GrassProfile))]
    public sealed class GrassProfileEditor : Editor
    {
        const string AddTypesKey = "FoliageForge.Grass.AddTypes";

        private static readonly (string title, string[] fields)[] FeatureGroups =
        {
            ("Motion", new[] { "Wind", "WindTexture", "Gusts", "Trample", "TrampleRT", "PerspectiveCorrection" }),
            ("Colour", new[] { "Texture", "Gradient", "Variation", "Patches", "GroundBlend", "GroundMap", "TrailTint" }),
            ("Lighting", new[] { "StylizedShading", "Rim", "Translucency", "ReceiveShadows", "CastShadows", "AdditionalLights", "LightRamp" }),
            ("Performance", new[] { "DistanceFade", "DistanceLod", "AlphaToCoverage", "FrustumCulling", "MeshLod", "CpuThinning" }),
        };

        static readonly string[] Handled = { "m_Script", "Variants", "Features", "Biome" };

        public override void OnInspectorGUI()
        {
            var profile = (GrassProfile)target;
            DrawLook(profile);
            serializedObject.Update();
            DrawTypes();
            DrawFeatures(profile);
            DrawTriangles(profile);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Variants"), new GUIContent("Type details (texture, size, colour)"), true);
            DrawRest();
            serializedObject.ApplyModifiedProperties();
        }

        // biome and quality pick once, here; the window's Grass tab shows this same inspector
        private static void DrawLook(GrassProfile profile)
        {
            EditorGUILayout.LabelField("Look", EditorStyles.boldLabel);
            bool add = EditorPrefs.GetBool(AddTypesKey, false);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Biome: {profile.Biome}", GUILayout.Width(130));
                bool now = GUILayout.Toggle(add, new GUIContent("Add types only", "Off: a biome replaces the grass types, colours and wind feel. On: it only adds its grass types to the list and changes nothing else."));
                if (now != add) EditorPrefs.SetBool(AddTypesKey, now);
                add = now;
            }
            string[] biomes = System.Enum.GetNames(typeof(GrassBiome));
            int biome = GUILayout.SelectionGrid(-1, biomes, 3);
            if (biome >= 0)
            {
                Undo.RecordObject(profile, "Grass Biome");
                GrassBiomes.Apply(profile, (GrassBiome)biome, add ? GrassApplyMode.AddTypes : GrassApplyMode.Replace);
                EditorUtility.SetDirty(profile);
            }
            EditorGUILayout.LabelField("Quality tier: density, draw distance and the costly features in one click.", EditorStyles.miniLabel);
            int q = GUILayout.SelectionGrid(-1, System.Enum.GetNames(typeof(GrassProfile.Quality)), 3);
            if (q >= 0)
            {
                Undo.RecordObject(profile, "Grass Quality");
                profile.ApplyQuality((GrassProfile.Quality)q);
                EditorUtility.SetDirty(profile);
            }
            EditorGUILayout.Space(4);
        }

        // on/off, Mix % and Density per type, so the most used values are not buried in the Variants foldouts
        private void DrawTypes()
        {
            EditorGUILayout.LabelField("Grass types", EditorStyles.boldLabel);
            SerializedProperty types = serializedObject.FindProperty("Variants");
            if (types.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No grass types. Pick a Biome above, or press + under Type details.", MessageType.Warning);
                return;
            }
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 50f;
            for (int i = 0; i < types.arraySize; i++)
            {
                SerializedProperty v = types.GetArrayElementAtIndex(i);
                SerializedProperty enabled = v.FindPropertyRelative("Enabled");
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(enabled, new GUIContent(v.FindPropertyRelative("Name").stringValue, "Grow this type. Off keeps its settings but scatters none."), GUILayout.Width(140));
                    using (new EditorGUI.DisabledScope(!enabled.boolValue))
                    {
                        EditorGUILayout.PropertyField(v.FindPropertyRelative("Mix"), new GUIContent("Mix %", "Share of this type's Density that grows where types combine. 100 = full density."));
                        EditorGUILayout.PropertyField(v.FindPropertyRelative("Density"), new GUIContent("Density", "Clumps per square metre."), GUILayout.Width(110));
                    }
                }
            }
            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUILayout.Space(4);
        }

        private void DrawFeatures(GrassProfile profile)
        {
            SerializedProperty features = serializedObject.FindProperty("Features");
            EditorGUILayout.LabelField($"Features ({profile.Features.EnabledCount()} on)", EditorStyles.boldLabel);
            foreach ((string title, string[] fields) in FeatureGroups)
            {
                int on = 0;
                foreach (string f in fields) if (features.FindPropertyRelative(f).boolValue) on++;
                string key = "FoliageForge.Grass.Group." + title;
                bool open = EditorGUILayout.Foldout(SessionState.GetBool(key, title != "Performance"), $"{title} ({on}/{fields.Length})", true);
                SessionState.SetBool(key, open);
                if (!open) continue;
                EditorGUI.indentLevel++;
                foreach (string f in fields) EditorGUILayout.PropertyField(features.FindPropertyRelative(f));
                EditorGUI.indentLevel--;
            }
            if (profile.Features.LightRamp && profile.LightRamp == null)
                EditorGUILayout.HelpBox("Light Ramp is on but no ramp texture is assigned (Light ramp section below); it stays off until one is.", MessageType.Info);
            if (profile.Features.TrailTint && !profile.Features.TrampleRT)
                EditorGUILayout.HelpBox("Trail Tint reads the trample map; switch on Trample Map too.", MessageType.Info);
            EditorGUILayout.Space(4);
        }

        // triangles per clump at each mesh LOD, one line per enabled type
        internal static void DrawTriangles(GrassProfile profile)
        {
            if (profile.Variants == null || profile.Variants.Length == 0) return;
            var sb = new System.Text.StringBuilder("Triangles per clump (LOD 0 / 1 / 2)");
            foreach (GrassVariant gv in profile.Variants)
            {
                if (!gv.Enabled) continue;
                sb.Append($"\n{gv.Name}: {GrassMeshes.TriangleCount(gv, 0)} / {GrassMeshes.TriangleCount(gv, 1)} / {GrassMeshes.TriangleCount(gv, 2)}");
            }
            EditorGUILayout.HelpBox(sb.ToString(), MessageType.None);
            EditorGUILayout.Space(4);
        }

        // Draw, Placement, Shading, Gusts, Patches and light
        private void DrawRest()
        {
            SerializedProperty it = serializedObject.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
                if (System.Array.IndexOf(Handled, it.name) < 0) EditorGUILayout.PropertyField(it, true);
        }
    }

    /// <summary>Grass field inspector: rebuild button, live counts and a shortcut to the Foliage Brush.</summary>
    [CustomEditor(typeof(GrassField))]
    public sealed class GrassFieldEditor : Editor
    {
        public override bool RequiresConstantRepaint() => true;

        public override void OnInspectorGUI()
        {
            var field = (GrassField)target;
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (field.Profile == null)
                EditorGUILayout.HelpBox("Assign a Grass Profile (Create > Meowdieval > Foliage Forge > Grass Profile).", MessageType.Warning);
            else
                EditorGUILayout.HelpBox(
                    $"{field.InstanceCount:N0} clumps in {field.ChunkCount} chunks (scatter {field.LastRebuildMs:F0} ms)\n" +
                    $"Last camera: {field.DrawnLastCamera:N0} clumps, {field.DrawCallsLastCamera} instanced calls", MessageType.None);
            if (field.Profile != null) GrassProfileEditor.DrawTriangles(field.Profile);
            if (GUILayout.Button("Rescatter")) field.Rebuild();
            if (GUILayout.Button("Capture Ground Colour", GUILayout.Height(22))) CaptureGround(field);

            EditorGUILayout.Space();
            if (GUILayout.Button("Paint With Foliage Brush"))
            {
                Selection.activeGameObject = field.gameObject;
                FoliageBrush.Target = BrushTarget.Grass;
                FoliageBrush.Save();
                FoliageBrush.Open();
            }
            using (new EditorGUI.DisabledScope(field.StrokeCount == 0))
                if (GUILayout.Button($"Clear Paint ({field.StrokeCount} strokes)") && EditorUtility.DisplayDialog("Clear grass paint", "Remove every brush stroke?", "Clear", "Cancel"))
                {
                    Undo.RecordObject(field, "Clear Grass Paint");
                    field.ClearStrokes();
                    EditorUtility.SetDirty(field);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(field);
                }
        }

        // top-down render of the ground layers over the box, saved as a texture the shader tints the grass with
        private static void CaptureGround(GrassField field)
        {
            Bounds b = field.WorldBounds;
            const int res = 512;
            var go = new GameObject("GroundCapture") { hideFlags = HideFlags.HideAndDontSave };
            var rt = RenderTexture.GetTemporary(res, res, 24);
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.z);
                cam.aspect = 1f;
                cam.transform.position = new Vector3(b.center.x, b.max.y + 5f, b.center.z);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = b.size.y + 20f;
                cam.cullingMask = field.Ground.value;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.gray;
                cam.targetTexture = rt;
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(res, res, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
                RenderTexture.active = prev;

                const string folder = "Assets/Foliage Forge";
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Foliage Forge");
                string path = $"{folder}/{field.name}_GroundMap.png";
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                Undo.RecordObject(field, "Capture Ground Colour");
                field.GroundMap = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                EditorUtility.SetDirty(field);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
                DestroyImmediate(go);
            }
        }

        [MenuItem("GameObject/Meowdieval/Grass Field", false, 20)]
        private static void Create(MenuCommand cmd)
        {
            var go = new GameObject("Grass Field");
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            var field = go.AddComponent<GrassField>();
            field.Profile = FindOrCreateProfile();
            Undo.RegisterCreatedObjectUndo(go, "Create Grass Field");
            Selection.activeGameObject = go;
        }

        internal static GrassProfile FindOrCreateProfile()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:GrassProfile"))
                return AssetDatabase.LoadAssetAtPath<GrassProfile>(AssetDatabase.GUIDToAssetPath(guid));
            const string folder = "Assets/Foliage Forge";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Foliage Forge");
            var profile = CreateInstance<GrassProfile>();
            AssetDatabase.CreateAsset(profile, folder + "/Grass.asset");
            return profile;
        }
    }
}
