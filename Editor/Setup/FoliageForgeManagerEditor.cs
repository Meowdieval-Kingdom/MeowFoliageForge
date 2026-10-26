using UnityEditor;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Foliage Forge Manager inspector: opens the Foliage Brush, stats and clear buttons, and the prefab-creating menu item.</summary>
    [CustomEditor(typeof(FoliageForgeManager))]
    public sealed class FoliageForgeManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var m = (FoliageForgeManager)target;
            DrawDefaultInspector();

            EditorGUILayout.Space();
            bool active = FoliageBrush.IsActive;
            if (GUILayout.Button(active ? "Close Foliage Brush" : "Open Foliage Brush", GUILayout.Height(26)))
            {
                if (active) FoliageBrush.Close();
                else FoliageBrush.Open();
            }
            EditorGUILayout.HelpBox("Paint, erase and pick the palette in the Foliage Brush panel of the scene view (also Meowdieval > Foliage Forge > Foliage Brush, or the Tools overlay).", MessageType.None);
            if (GUILayout.Button("Open Foliage Forge Window")) FoliageForgeWindow.Open();

            if (m.Props != null)
                for (int i = 0; i < m.Props.childCount; i++)
                    if ((m.Ground.value & (1 << m.Props.GetChild(i).gameObject.layer)) != 0)
                    {
                        EditorGUILayout.HelpBox("Placed props are on a layer that is in the Ground mask, so grass grows on them and new props stack on them. Move the props or the mask to another layer.", MessageType.Warning);
                        break;
                    }

            EditorGUILayout.Space();
            string stats = m.Props != null ? $"{m.Props.childCount} props" : "no props root";
            if (m.Grass != null) stats += $", {m.Grass.StrokeCount} grass strokes, {m.Grass.InstanceCount:N0} clumps";
            EditorGUILayout.LabelField(stats, EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(m.Grass == null || m.Grass.StrokeCount == 0))
                    if (GUILayout.Button("Clear Grass Paint") && EditorUtility.DisplayDialog("Clear grass paint", "Remove every grass brush stroke?", "Clear", "Cancel"))
                    {
                        Undo.RecordObject(m.Grass, "Clear Grass Paint");
                        m.Grass.ClearStrokes();
                        EditorUtility.SetDirty(m.Grass);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(m.Grass);
                    }
                using (new EditorGUI.DisabledScope(m.Props == null || m.Props.childCount == 0))
                    if (GUILayout.Button("Clear Props") && EditorUtility.DisplayDialog("Clear props", "Delete every placed prop?", "Delete", "Cancel"))
                        for (int i = m.Props.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(m.Props.GetChild(i).gameObject);
            }
        }

        [MenuItem("GameObject/Meowdieval/Foliage Forge", false, 19)]
        private static void Create(MenuCommand cmd) => CreateInScene(cmd.context as GameObject);

        const string PrefabPath = "Packages/com.meowdieval.foliageforge/Prefabs/Meowdieval Foliage Forge.prefab";

        // places the prefab shipped with the package (manager, grass field, props root, foliage wind) in the scene
        internal static void CreateInScene(GameObject parent)
        {
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (saved == null)
            {
                Debug.LogError($"[Foliage Forge] Prefab missing: {PrefabPath}");
                return;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(saved);
            GameObjectUtility.SetParentAndAlign(instance, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Create Foliage Forge");
            Selection.activeGameObject = instance;
        }
    }
}
