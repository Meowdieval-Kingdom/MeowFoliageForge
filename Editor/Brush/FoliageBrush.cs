using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// Scene-view foliage brush, run by <see cref="FoliageBrushTool"/> and set up in <see cref="FoliageBrushOverlay"/>. Paints into the
    /// selected Foliage Forge Manager or Grass Field, else the first manager in the open stage. Grass dabs are stored as strokes on the
    /// field and rescattered on mouse release; props are prefab instances placed under the manager's Props root.
    /// </summary>
    public static partial class FoliageBrush
    {
        const string Prefs = "FoliageForge.Brush.";
        /// <summary>Brush radius limits in metres.</summary>
        public const float MinRadius = 0.25f, MaxRadius = 25f;

        /// <summary>Grass strokes or palette prefabs.</summary>
        public static BrushTarget Target;
        /// <summary>Erase instead of paint (Shift inverts it while brushing).</summary>
        public static bool Erase;
        /// <summary>Brush radius in metres.</summary>
        public static float Radius;
        /// <summary>Share of the free spots a prop dab fills (0..1).</summary>
        public static float Density;
        /// <summary>Density multiplier of a grass dab (0.1..2), stored on each stroke.</summary>
        public static float GrassDensity;
        /// <summary>Minimum distance between trees and other non-grass props.</summary>
        public static float Spacing;
        /// <summary>Minimum distance between grass batches.</summary>
        public static float GrassSpacing;
        /// <summary>Places a weighted random variant of the set a palette tree belongs to (Oak_V1 may become Oak_V2) instead of the prefab itself.</summary>
        public static bool UseVariants;
        /// <summary>Group props of one prefab around a few random centres per dab instead of mixing the palette evenly.</summary>
        public static bool Cluster;
        /// <summary>Cluster radius in metres: roughly one centre per this much of the brush area.</summary>
        public static float ClusterRadius;
        /// <summary>Prefer spots near the brush rim and near ledges or water edges.</summary>
        public static bool EdgeBias;
        /// <summary>How strongly the rim is preferred (0 = not at all, 1 = the dab centre stays empty).</summary>
        public static float EdgeStrength;
        /// <summary>Distance in metres at which the ground is probed for ledges and gaps.</summary>
        public static float EdgeSize;
        /// <summary>Random uniform scale range of placed props (x min, y max).</summary>
        public static Vector2 Scale;
        /// <summary>Rotate placed props randomly.</summary>
        public static bool RandomRotation;
        /// <summary>Frame the random rotation turns in.</summary>
        public static BrushRotationMode RotationMode;
        /// <summary>Axes that get a random rotation.</summary>
        public static BrushRotationAxes RotationAxes;
        /// <summary>Random rotation range in degrees (x min, y max), picked per axis.</summary>
        public static Vector2 RotationRange;

        /// <summary>What the cursor found on the last scene-view event while the tool was active.</summary>
        public static BrushGround Ground { get; private set; }
        /// <summary>Layer of the collider under the cursor when <see cref="Ground"/> is WrongLayer.</summary>
        public static int CursorLayer { get; private set; }
        /// <summary>True while the mouse is over a scene view with the tool active.</summary>
        public static bool Hovering => _hover;

        static string _cursorObject;
        static Vector3 _point, _normal;
        static bool _hover, _shift;
        static FoliageForgeManager _manager;
        static GrassField _grass;
        static bool _stale = true;
        static bool _stroking, _strokeErase;
        static BrushTarget _strokeTarget;
        static int _undoGroup;
        static Vector3 _lastDab;
        static GUIStyle _labelStyle;
        static readonly List<GameObject> StrokePalette = new List<GameObject>();
        static readonly List<float> StrokeWeights = new List<float>();
        static readonly List<TreeVariantSet> StrokeSets = new List<TreeVariantSet>(); // parallel to StrokePalette; null for plain prefabs
        static readonly Dictionary<GameObject, TreeVariantSet> VariantOf = new Dictionary<GameObject, TreeVariantSet>(); // any variant prefab -> its set; filled per stroke when UseVariants
        static readonly List<Vector3> TreeSpots = new List<Vector3>(), BatchSpots = new List<Vector3>();
        static readonly RaycastHit[] Hits = new RaycastHit[64];
        static readonly int Hash = "FoliageForgeBrush".GetHashCode();

        static FoliageBrush()
        {
            Target = (BrushTarget)EditorPrefs.GetInt(Prefs + "Target", (int)BrushTarget.Props);
            Radius = EditorPrefs.GetFloat(Prefs + "Radius", 3f);
            Density = EditorPrefs.GetFloat(Prefs + "Density", 1f);
            GrassDensity = EditorPrefs.GetFloat(Prefs + "GrassDensity", 1f);
            Spacing = EditorPrefs.GetFloat(Prefs + "Spacing", 2.5f);
            GrassSpacing = EditorPrefs.GetFloat(Prefs + "GrassSpacing", 1f);
            UseVariants = EditorPrefs.GetBool(Prefs + "UseVariants", false);
            Cluster = EditorPrefs.GetBool(Prefs + "Cluster", false);
            ClusterRadius = EditorPrefs.GetFloat(Prefs + "ClusterRadius", 2f);
            EdgeBias = EditorPrefs.GetBool(Prefs + "EdgeBias", false);
            EdgeStrength = EditorPrefs.GetFloat(Prefs + "EdgeStrength", 0.5f);
            EdgeSize = EditorPrefs.GetFloat(Prefs + "EdgeSize", 1f);
            Scale = new Vector2(EditorPrefs.GetFloat(Prefs + "ScaleMin", 0.9f), EditorPrefs.GetFloat(Prefs + "ScaleMax", 1.2f));
            RandomRotation = EditorPrefs.GetBool(Prefs + "RandomRotation", true);
            RotationMode = (BrushRotationMode)EditorPrefs.GetInt(Prefs + "RotationMode", (int)BrushRotationMode.World);
            RotationAxes = (BrushRotationAxes)EditorPrefs.GetInt(Prefs + "RotationAxes", (int)BrushRotationAxes.Y);
            RotationRange = new Vector2(EditorPrefs.GetFloat(Prefs + "RotationMin", 0f), EditorPrefs.GetFloat(Prefs + "RotationMax", 360f));
            Selection.selectionChanged += Invalidate;
            EditorApplication.hierarchyChanged += Invalidate;
        }

        /// <summary>Stores the brush settings in EditorPrefs.</summary>
        public static void Save()
        {
            EditorPrefs.SetInt(Prefs + "Target", (int)Target);
            EditorPrefs.SetFloat(Prefs + "Radius", Radius);
            EditorPrefs.SetFloat(Prefs + "Density", Density);
            EditorPrefs.SetFloat(Prefs + "GrassDensity", GrassDensity);
            EditorPrefs.SetFloat(Prefs + "Spacing", Spacing);
            EditorPrefs.SetFloat(Prefs + "GrassSpacing", GrassSpacing);
            EditorPrefs.SetBool(Prefs + "UseVariants", UseVariants);
            EditorPrefs.SetBool(Prefs + "Cluster", Cluster);
            EditorPrefs.SetFloat(Prefs + "ClusterRadius", ClusterRadius);
            EditorPrefs.SetBool(Prefs + "EdgeBias", EdgeBias);
            EditorPrefs.SetFloat(Prefs + "EdgeStrength", EdgeStrength);
            EditorPrefs.SetFloat(Prefs + "EdgeSize", EdgeSize);
            EditorPrefs.SetFloat(Prefs + "ScaleMin", Scale.x);
            EditorPrefs.SetFloat(Prefs + "ScaleMax", Scale.y);
            EditorPrefs.SetBool(Prefs + "RandomRotation", RandomRotation);
            EditorPrefs.SetInt(Prefs + "RotationMode", (int)RotationMode);
            EditorPrefs.SetInt(Prefs + "RotationAxes", (int)RotationAxes);
            EditorPrefs.SetFloat(Prefs + "RotationMin", RotationRange.x);
            EditorPrefs.SetFloat(Prefs + "RotationMax", RotationRange.y);
        }

        /// <summary>True while the Foliage Brush tool is the active scene tool.</summary>
        public static bool IsActive => ToolManager.activeToolType == typeof(FoliageBrushTool);

        /// <summary>Activates the brush tool and shows its panel in every scene view.</summary>
        public static void Open()
        {
            ToolManager.SetActiveTool<FoliageBrushTool>();
            FoliageBrushOverlay.ShowAll();
        }

        /// <summary>Leaves the brush tool for the previous scene tool.</summary>
        public static void Close()
        {
            if (IsActive) ToolManager.RestorePreviousPersistentTool();
        }

        /// <summary>Manager the brush paints into, or null.</summary>
        public static FoliageForgeManager Manager
        {
            get
            {
                Resolve();
                return _manager;
            }
        }

        /// <summary>Grass Field the brush paints into: the manager's, or a selected stand-alone field.</summary>
        public static GrassField Grass
        {
            get
            {
                Resolve();
                return _grass;
            }
        }

        /// <summary>Ground mask in use: the manager's, else the stand-alone field's.</summary>
        public static int GroundMask => Manager != null ? Manager.Ground.value : Grass != null ? Grass.Ground.value : 0;

        internal static void Invalidate() => _stale = true;

        // the selected manager or field wins and stays the target until another one is selected; otherwise the first manager in the stage
        private static void Resolve()
        {
            bool lost = (!ReferenceEquals(_manager, null) && _manager == null) || (!ReferenceEquals(_grass, null) && _grass == null);
            if (!_stale && !lost) return;
            _stale = false;
            StageHandle stage = StageUtility.GetCurrentStageHandle();
            GameObject sel = Selection.activeGameObject;
            if (sel != null && stage.Contains(sel))
            {
                var m = sel.GetComponentInParent<FoliageForgeManager>();
                GrassField g = m != null ? null : sel.GetComponentInParent<GrassField>();
                if (m != null || g != null)
                {
                    _manager = m;
                    _grass = g;
                }
            }
            if (_manager != null && !stage.Contains(_manager.gameObject)) _manager = null;
            if (_grass != null && !stage.Contains(_grass.gameObject)) _grass = null;
            if (_manager == null && _grass == null)
            {
                _manager = stage.FindComponentOfType<FoliageForgeManager>();
                if (_manager == null && PrefabStageUtility.GetCurrentPrefabStage() == null) _manager = Object.FindFirstObjectByType<FoliageForgeManager>();
            }
            if (_manager != null) _grass = _manager.Grass;
        }

        /// <summary>Why a dab would do nothing right now, or null. <paramref name="cursor"/> also checks the ground under the cursor.</summary>
        public static string Problem(bool cursor)
        {
            FoliageForgeManager m = Manager;
            GrassField g = Grass;
            if (m == null && g == null) return "No Foliage Forge in this scene. Click Create Foliage Forge.";
            bool erase = cursor ? Erase != _shift : Erase;
            if (Target == BrushTarget.Props)
            {
                if (m == null) return "This Grass Field has no Foliage Forge Manager, so it has no props. Switch to Grass paint.";
                if (m.Props == null) return "The Foliage Forge Manager has no Props root.";
                if (!erase && PaletteCount(m) == 0) return "Palette empty or every entry at 0%: click prefabs in the Foliage Brush panel and set their Mix %.";
            }
            else
            {
                if (g == null) return "The Foliage Forge Manager has no Grass Field.";
                if (g.Profile == null) return "The Grass Field has no Grass Profile.";
                if (!erase && System.Array.TrueForAll(g.Profile.Variants, v => v.MixedDensity <= 0f)) return "Every grass type is off or at 0% Mix: tick one in the Foliage Brush panel.";
            }
            if (!cursor || !_hover) return null;
            switch (Ground)
            {
                case BrushGround.None:
                    return "No ground under the cursor.";
                case BrushGround.WrongLayer:
                    return $"'{_cursorObject}' is on layer '{LayerMask.LayerToName(CursorLayer)}', which is not in the Ground mask.";
                case BrushGround.Plane when Target == BrushTarget.Grass:
                    return "No collider under the cursor. Grass only grows on colliders in the Ground mask.";
            }
            return null;
        }

        /// <summary>Non-blocking note about the cursor (flat-plane fallback), or null.</summary>
        public static string Note => _hover && Ground == BrushGround.Plane && Target == BrushTarget.Props
            ? $"No collider under the cursor: props land on a flat plane at the Foliage Forge's height (Y {_point.y:0.##})."
            : null;
    }
}
