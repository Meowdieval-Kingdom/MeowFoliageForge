using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Meowdieval.FoliageForge
{
    /// <summary>Grass field inspector: rebuild button, live counts and a shortcut to the Foliage Brush.</summary>
    [CustomEditor(typeof(GrassField))]
    public sealed class GrassFieldEditor : Editor
    {
        /// <summary>Default inspector as bound PropertyFields (m_Script read-only), for the field editors.</summary>
        internal static void FillDefault(VisualElement root, SerializedObject so)
        {
            SerializedProperty it = so.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
            {
                var pf = new PropertyField(it);
                if (it.propertyPath == "m_Script") pf.SetEnabled(false);
                root.Add(pf);
            }
        }

        internal static Button FieldButton(string text, System.Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("ff-button");
            b.style.marginTop = 2;
            return b;
        }

        public override VisualElement CreateInspectorGUI()
        {
            var field = (GrassField)target;
            var root = new VisualElement();
            FoliageUi.Apply(root);
            FillDefault(root, serializedObject);

            var warn = new HelpBox("Assign a Grass Profile (Create > Meowdieval > Foliage Forge > Grass Profile).", HelpBoxMessageType.Warning);
            var stats = new HelpBox("", HelpBoxMessageType.None);
            var tris = new HelpBox("", HelpBoxMessageType.None);
            warn.style.marginTop = 6;
            stats.style.marginTop = 6;
            root.Add(warn);
            root.Add(stats);
            root.Add(tris);

            var rescatter = FieldButton("Rescatter", () => field.Rebuild());
            rescatter.style.marginTop = 6;
            root.Add(rescatter);
            root.Add(FieldButton("Capture Ground Colour", () => CaptureGround(field)));

            var paint = FieldButton("Paint With Foliage Brush", () =>
            {
                Selection.activeGameObject = field.gameObject;
                FoliageBrush.Target = BrushTarget.Grass;
                FoliageBrush.Save();
                FoliageBrush.Open();
            });
            paint.style.marginTop = 10;
            root.Add(paint);
            var clear = FieldButton("Clear Paint", () =>
            {
                if (!EditorUtility.DisplayDialog("Clear grass paint", "Remove every brush stroke?", "Clear", "Cancel")) return;
                Undo.RecordObject(field, "Clear Grass Paint");
                field.ClearStrokes();
                EditorUtility.SetDirty(field);
                PrefabUtility.RecordPrefabInstancePropertyModifications(field);
            });
            root.Add(clear);

            // live counts (the IMGUI version repainted constantly)
            root.schedule.Execute(() =>
            {
                if (field == null) return;
                bool has = field.Profile != null;
                warn.style.display = has ? DisplayStyle.None : DisplayStyle.Flex;
                stats.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
                if (has)
                    stats.text = $"{field.InstanceCount:N0} clumps in {field.ChunkCount} chunks (scatter {field.LastRebuildMs:F0} ms)\n" +
                                 $"Last camera: {field.DrawnLastCamera:N0} clumps, {field.DrawCallsLastCamera} instanced calls";
                string tri = has ? GrassProfileEditor.TriangleText(field.Profile) : null;
                tris.style.display = tri != null ? DisplayStyle.Flex : DisplayStyle.None;
                if (tri != null) tris.text = tri;
                clear.text = $"Clear Paint ({field.StrokeCount} strokes)";
                clear.SetEnabled(field.StrokeCount != 0);
            }).Every(250);
            return root;
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
