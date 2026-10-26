using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    public static partial class FoliageBrush
    {
        internal static void SceneGUI(SceneView view)
        {
            Event e = Event.current;
            int id = GUIUtility.GetControlID(Hash, FocusType.Passive);
            EventType type = e.type; // mouse events below check hotControl themselves
            _shift = e.shift;
            switch (type)
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    return;
                case EventType.KeyDown when e.keyCode == KeyCode.Escape:
                    if (GUIUtility.hotControl == id) GUIUtility.hotControl = 0;
                    e.Use();
                    Close();
                    return;
                case EventType.ScrollWheel when e.shift:
                    float d = e.delta.y != 0f ? e.delta.y : e.delta.x; // some platforms turn shift+scroll into horizontal scroll
                    Radius = Mathf.Clamp(Radius * (d > 0f ? 0.9f : 1.1f), MinRadius, MaxRadius);
                    Save();
                    e.Use();
                    view.Repaint();
                    return;
                case EventType.MouseLeaveWindow:
                    _hover = false;
                    view.Repaint();
                    return;
                case EventType.MouseUp when GUIUtility.hotControl == id && e.button == 0:
                    GUIUtility.hotControl = 0;
                    EndStroke();
                    e.Use();
                    return;
            }

            if (type == EventType.MouseMove || type == EventType.MouseDrag || type == EventType.MouseDown) _hover = true;
            bool hasTarget = Manager != null || Grass != null;
            if (!hasTarget) Ground = BrushGround.None;
            else if (_hover && (type == EventType.Repaint || type == EventType.MouseMove || type == EventType.MouseDrag || type == EventType.MouseDown))
            {
                if (type != EventType.Repaint) Physics.SyncTransforms();
                Probe(e.mousePosition);
            }

            if (type == EventType.Repaint)
            {
                if (_hover) Draw();
                return;
            }
            if (type == EventType.MouseMove)
            {
                view.Repaint();
                return;
            }
            if (type == EventType.MouseDown && e.button == 0 && !e.alt && HandleUtility.nearestControl == id)
            {
                GUIUtility.hotControl = id;
                e.Use();
                string problem = Problem(true);
                if (problem != null)
                {
                    view.ShowNotification(new GUIContent(problem));
                    return;
                }
                BeginStroke();
                Dab();
            }
            else if (type == EventType.MouseDrag && GUIUtility.hotControl == id)
            {
                e.Use();
                view.Repaint();
                if (_stroking && Problem(true) == null && (_point - _lastDab).sqrMagnitude >= Radius * Radius * 0.16f) Dab();
            }
        }

        // collider in the Ground mask, else any collider (to name the layer that is missing), else the plane at the target's height
        private static void Probe(Vector2 mouse)
        {
            GameObject owner = _manager != null ? _manager.gameObject : _grass.gameObject;
            Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
            PhysicsScene physics = owner.scene.GetPhysicsScene(); // prefab stages have their own
            Transform skip = _manager != null ? _manager.Props : null;
            if (Cast(physics, ray, GroundMask, skip, out RaycastHit hit)) Ground = BrushGround.Collider;
            else if (Cast(physics, ray, Physics.DefaultRaycastLayers, skip, out hit))
            {
                Ground = BrushGround.WrongLayer;
                CursorLayer = hit.collider.gameObject.layer;
                _cursorObject = hit.collider.name;
            }
            else if (new Plane(Vector3.up, owner.transform.position).Raycast(ray, out float distance))
            {
                Ground = BrushGround.Plane;
                _point = ray.GetPoint(distance);
                _normal = Vector3.up;
                return;
            }
            else
            {
                Ground = BrushGround.None;
                return;
            }
            _point = hit.point;
            _normal = hit.normal;
        }

        // nearest hit that is not part of the props being painted (so trees do not become ground)
        private static bool Cast(PhysicsScene scene, Ray ray, int mask, Transform skip, out RaycastHit best)
        {
            best = default;
            int n = scene.Raycast(ray.origin, ray.direction, Hits, Mathf.Infinity, mask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (skip != null && Hits[i].collider.transform.IsChildOf(skip)) continue;
                if (Hits[i].distance >= nearest) continue;
                nearest = Hits[i].distance;
                best = Hits[i];
                found = true;
            }
            return found;
        }

        private static void Draw()
        {
            if (Ground == BrushGround.None) return;
            string problem = Problem(true);
            bool erase = Erase != _shift;
            Color c = problem != null ? new Color(0.65f, 0.65f, 0.65f)
                : erase ? new Color(1f, 0.35f, 0.3f)
                : Ground == BrushGround.Plane ? new Color(1f, 0.8f, 0.25f)
                : new Color(0.4f, 1f, 0.45f);
            Handles.color = new Color(c.r, c.g, c.b, 0.08f);
            Handles.DrawSolidDisc(_point, _normal, Radius);
            Handles.color = new Color(c.r, c.g, c.b, 0.95f);
            Handles.DrawWireDisc(_point, _normal, Radius, 2f);
            _labelStyle ??= new GUIStyle(EditorStyles.whiteBoldLabel);
            string text = problem ?? (erase ? "Erase" : Target == BrushTarget.Grass ? "Paint grass" : $"Paint ({PaletteCount(_manager)} prefabs)");
            Handles.Label(_point, text, _labelStyle);
        }

        private static void BeginStroke()
        {
            _strokeErase = Erase != _shift;
            _strokeTarget = Target;
            Undo.IncrementCurrentGroup();
            _undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(_strokeErase ? "Erase Foliage" : "Paint Foliage");
            StrokePalette.Clear();
            StrokeWeights.Clear();
            StrokeSets.Clear();
            FillVariantOf();
            TreeSpots.Clear();
            BatchSpots.Clear();
            if (_manager != null && _manager.Props != null)
            {
                for (int i = 0; i < _manager.Palette.Count; i++)
                    if (_manager.Palette[i] != null && _manager.PaletteWeight(i) > 0f)
                    {
                        StrokePalette.Add(_manager.Palette[i]);
                        StrokeWeights.Add(_manager.PaletteWeight(i));
                        StrokeSets.Add(null);
                    }
                foreach (TreeVariantSet set in _manager.VariantSets)
                    if (UsableSet(set))
                    {
                        StrokePalette.Add(set.Pick(new System.Random(1)));
                        StrokeWeights.Add(100f);
                        StrokeSets.Add(set);
                    }
                Transform root = _manager.Props;
                for (int i = 0; i < root.childCount; i++)
                    (root.GetChild(i).GetComponent<GrassBatch>() != null ? BatchSpots : TreeSpots).Add(root.GetChild(i).position);
            }
            _stroking = true;
        }

        private static void FillVariantOf()
        {
            VariantOf.Clear();
            if (!UseVariants) return;
            foreach (string guid in AssetDatabase.FindAssets("t:TreeVariantSet"))
            {
                var set = AssetDatabase.LoadAssetAtPath<TreeVariantSet>(AssetDatabase.GUIDToAssetPath(guid));
                if (set == null) continue;
                foreach (GameObject p in set.Prefabs) if (p != null) VariantOf[p] = set;
            }
        }

        /// <summary>Finishes the current stroke: collapses it into one undo step and rescatters painted grass.</summary>
        public static void EndStroke()
        {
            if (!_stroking) return;
            _stroking = false;
            Undo.CollapseUndoOperations(_undoGroup);
            if (_strokeTarget == BrushTarget.Grass && _grass != null) _grass.Rebuild();
        }
    }
}
