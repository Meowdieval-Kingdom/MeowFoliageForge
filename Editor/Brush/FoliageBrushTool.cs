using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Foliage Brush scene tool. Works with any selection and paints into the scene's Foliage Forge (see <see cref="FoliageBrush"/>).</summary>
    [EditorTool("Foliage Brush", null)]
    public sealed class FoliageBrushTool : EditorTool
    {
        GUIContent _icon;

        /// <summary>Tools overlay icon.</summary>
        public override GUIContent toolbarIcon => _icon ??= new GUIContent(AssetPreview.GetMiniTypeThumbnail(typeof(UnityEngine.Tree)), "Foliage Brush: paint trees, grass batches and grass (Foliage Forge)");

        /// <summary>Shows the brush panel when the tool is picked.</summary>
        public override void OnActivated()
        {
            FoliageBrush.Invalidate();
            FoliageBrushOverlay.ShowAll();
            SceneView.RepaintAll();
        }

        /// <summary>Closes an unfinished stroke.</summary>
        public override void OnWillBeDeactivated()
        {
            FoliageBrush.EndStroke();
            SceneView.RepaintAll();
        }

        /// <summary>Runs the brush in scene views.</summary>
        public override void OnToolGUI(EditorWindow window)
        {
            if (window is SceneView view) FoliageBrush.SceneGUI(view);
        }

        [MenuItem("Meowdieval/Foliage Forge/Foliage Brush", false, 4)]
        private static void Menu() => FoliageBrush.Open();
    }
}
