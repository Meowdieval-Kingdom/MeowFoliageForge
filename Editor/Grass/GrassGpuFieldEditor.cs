using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Meowdieval.FoliageForge
{
    /// <summary>Inspector for <see cref="GrassGpuField"/>: the grouped fields plus warnings for the setups that draw nothing.</summary>
    [CustomEditor(typeof(GrassGpuField))]
    public sealed class GrassGpuFieldEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var field = (GrassGpuField)target;
            var root = new VisualElement();
            FoliageUi.Apply(root);

            var unsupported = new HelpBox("This device does not support compute shaders. Grass GPU Field switches itself off; use Grass Field instead.", HelpBoxMessageType.Error);
            unsupported.style.display = GrassGpuField.IsSupported ? DisplayStyle.None : DisplayStyle.Flex;
            var noProfile = new HelpBox("Assign a Grass Profile (Create > Meowdieval > Foliage Forge > Grass Profile).", HelpBoxMessageType.Warning);
            var noShader = new HelpBox("The Grass Profile has no shader assigned.", HelpBoxMessageType.Warning);
            root.Add(unsupported);
            root.Add(noProfile);
            root.Add(noShader);
            GrassFieldEditor.FillDefault(root, serializedObject);
            var stats = new HelpBox("", HelpBoxMessageType.None);
            stats.style.marginTop = 6;
            root.Add(stats);

            root.schedule.Execute(() =>
            {
                if (field == null) return;
                noProfile.style.display = field.Profile == null ? DisplayStyle.Flex : DisplayStyle.None;
                noShader.style.display = field.Profile != null && field.Profile.Shader == null ? DisplayStyle.Flex : DisplayStyle.None;
                stats.text =
                    "GPU placement does not raycast: no slope limit, ground layers or brush strokes. Ground comes from the height texture or the flat plane at this object's Y. " +
                    "Stats: " + field.CandidatesLastCamera + " candidates, " + field.CellsPerSideLastCamera + " cells per side (last camera).";
            }).Every(250);
            return root;
        }
    }
}
