using UnityEditor;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Inspector for <see cref="GrassGpuField"/>: the grouped fields plus warnings for the setups that draw nothing.</summary>
    [CustomEditor(typeof(GrassGpuField))]
    public sealed class GrassGpuFieldEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (!GrassGpuField.IsSupported)
                EditorGUILayout.HelpBox("This device does not support compute shaders. Grass GPU Field switches itself off; use Grass Field instead.", MessageType.Error);
            var field = (GrassGpuField)target;
            if (field.Profile == null)
                EditorGUILayout.HelpBox("Assign a Grass Profile (Create > Meowdieval > Foliage Forge > Grass Profile).", MessageType.Warning);
            else if (field.Profile.Shader == null)
                EditorGUILayout.HelpBox("The Grass Profile has no shader assigned.", MessageType.Warning);
            DrawDefaultInspector();
            EditorGUILayout.HelpBox(
                "GPU placement does not raycast: no slope limit, ground layers or brush strokes. Ground comes from the height texture or the flat plane at this object's Y. " +
                "Stats: " + field.CandidatesLastCamera + " candidates, " + field.CellsPerSideLastCamera + " cells per side (last camera).", MessageType.None);
        }
    }
}
