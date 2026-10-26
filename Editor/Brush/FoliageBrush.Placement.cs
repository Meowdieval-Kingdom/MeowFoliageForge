using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    public static partial class FoliageBrush
    {
        private static void Dab()
        {
            _lastDab = _point;
            if (_strokeTarget == BrushTarget.Grass)
            {
                if (_strokeErase && GrassEraseMask(_grass.Profile) == 0) return; // mask 0 means "every type" on a stroke, so no ticked type must erase nothing
                Undo.RecordObject(_grass, "Paint Grass");
                float scale = Mathf.Max(0.0001f, Mathf.Abs(_grass.transform.lossyScale.x));
                _grass.AddStroke(new GrassStroke
                {
                    Point = _grass.transform.InverseTransformPoint(_point),
                    Radius = Radius / scale,
                    Erase = _strokeErase,
                    Seed = Random.Range(1, int.MaxValue),
                    Density = GrassDensity,
                    TypeMask = _strokeErase ? GrassEraseMask(_grass.Profile) : 0,
                }, false);
                Dirty(_grass);
            }
            else if (_strokeErase) EraseProps();
            else PlaceProps();
        }

        /// <summary>Bit per enabled grass type of <paramref name="profile"/>: the types an erase dab removes.</summary>
        public static int GrassEraseMask(GrassProfile profile)
        {
            int mask = 0;
            for (int i = 0; i < profile.Variants.Length && i < 32; i++)
                if (profile.Variants[i].Enabled) mask |= 1 << i;
            return mask;
        }

        // only props whose source prefab (or the prefab of its variant set) is ticked in the palette
        private static void EraseProps()
        {
            Transform root = _manager.Props;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject go = root.GetChild(i).gameObject;
                Vector3 d = go.transform.position - _point;
                d.y = 0f;
                if (d.sqrMagnitude <= Radius * Radius && InErasePalette(go)) Undo.DestroyObjectImmediate(go);
            }
        }

        private static bool InErasePalette(GameObject instance)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            if (source == null) return false;
            for (int i = 0; i < StrokePalette.Count; i++)
                if (StrokePalette[i] == source || (StrokeSets[i] != null && StrokeSets[i].Prefabs.Contains(source))) return true;
            if (VariantOf.TryGetValue(source, out TreeVariantSet own))
                for (int i = 0; i < StrokePalette.Count; i++)
                    if (VariantOf.TryGetValue(StrokePalette[i], out TreeVariantSet other) && other == own) return true; // erasing one variant removes its siblings too
            return false;
        }

        private static void PlaceProps()
        {
            if (StrokePalette.Count == 0) return;
            bool trees = false, batches = false;
            foreach (GameObject p in StrokePalette)
                if (p.GetComponent<GrassBatch>() != null) batches = true;
                else trees = true;
            float densest = Mathf.Max(0.05f, Mathf.Min(trees ? Spacing : float.MaxValue, batches ? GrassSpacing : float.MaxValue));
            int attempts = Mathf.Clamp(Mathf.RoundToInt(Mathf.PI * Radius * Radius / (densest * densest) * 1.5f * Density), 1, 400);
            PhysicsScene physics = _manager.gameObject.scene.GetPhysicsScene();
            Transform root = _manager.Props;
            bool onPlane = Ground == BrushGround.Plane;
            var rng = new System.Random(Random.Range(int.MinValue, int.MaxValue));
            var centres = new List<Vector2>();
            var centrePrefabs = new List<int>();
            if (Cluster)
            {
                float cr = Mathf.Max(0.1f, ClusterRadius);
                int count = Mathf.Clamp(Mathf.CeilToInt(Radius * Radius / (cr * cr)), 1, 12);
                for (int c = 0; c < count; c++)
                {
                    centres.Add(Random.insideUnitCircle * Radius);
                    centrePrefabs.Add(WeightedPick(StrokeWeights, rng));
                }
            }
            for (int a = 0; a < attempts; a++)
            {
                Vector2 p = Random.insideUnitCircle * Radius;
                int pick = Cluster ? centrePrefabs[ClusterIndex(p, centres)] : WeightedPick(StrokeWeights, rng);
                GameObject prefab = StrokeSets[pick] != null ? StrokeSets[pick].Pick(rng) ?? StrokePalette[pick] : StrokePalette[pick];
                if (StrokeSets[pick] == null && VariantOf.TryGetValue(prefab, out TreeVariantSet variants)) prefab = variants.Pick(rng) ?? prefab;
                Vector3 normal = Vector3.up;
                var spot = new Vector3(_point.x + p.x, _point.y, _point.z + p.y);
                bool ledge = false;
                if (!onPlane)
                {
                    var ray = new Ray(spot + Vector3.up * (50f + Radius), Vector3.down);
                    if (!Cast(physics, ray, _manager.Ground.value, root, out RaycastHit ground)) continue;
                    spot = ground.point;
                    normal = ground.normal;
                    if (EdgeBias) ledge = IsGroundEdge(physics, spot, root);
                }
                if (EdgeBias && !ledge && (float)rng.NextDouble() > EdgeWeight(p.magnitude / Radius, EdgeStrength)) continue;
                bool isBatch = prefab.GetComponent<GrassBatch>() != null;
                List<Vector3> spots = isBatch ? BatchSpots : TreeSpots;
                if (TooClose(spots, spot, isBatch ? GrassSpacing : Spacing)) continue;

                GameObject go = PrefabUtility.IsPartOfPrefabAsset(prefab)
                    ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, root)
                    : Object.Instantiate(prefab, root);
                go.transform.SetPositionAndRotation(spot, PropRotation(normal));
                go.transform.localScale *= Random.Range(Mathf.Min(Scale.x, Scale.y), Mathf.Max(Scale.x, Scale.y));
                Undo.RegisterCreatedObjectUndo(go, "Place Prop");
                spots.Add(spot);
            }
        }

        /// <summary>
        /// Chance (0..1) that a spot at <paramref name="distFromCentre01"/> (0 = dab centre, 1 = rim) is kept when edge bias is on.
        /// Strength 0 keeps everything; strength 1 keeps spots in proportion to their distance from the centre.
        /// </summary>
        public static float EdgeWeight(float distFromCentre01, float strength)
        {
            return 1f - Mathf.Clamp01(strength) * (1f - Mathf.Clamp01(distFromCentre01));
        }

        /// <summary>Index of the centre nearest to <paramref name="point"/> (ties go to the first), or -1 when there are no centres.</summary>
        public static int ClusterIndex(Vector2 point, IReadOnlyList<Vector2> centres)
        {
            int best = -1;
            float bestSq = float.MaxValue;
            for (int i = 0; i < centres.Count; i++)
            {
                float sq = (centres[i] - point).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = i;
                }
            }
            return best;
        }

        // a ledge or shore: some probe at +-EdgeSize misses the Ground mask or lands well below the spot
        private static bool IsGroundEdge(PhysicsScene physics, Vector3 spot, Transform skip)
        {
            float size = Mathf.Max(0.1f, EdgeSize);
            var offsets = new[] { new Vector3(size, 0f, 0f), new Vector3(-size, 0f, 0f), new Vector3(0f, 0f, size), new Vector3(0f, 0f, -size) };
            foreach (Vector3 o in offsets)
            {
                var ray = new Ray(spot + o + Vector3.up * (50f + Radius), Vector3.down);
                if (!Cast(physics, ray, _manager.Ground.value, skip, out RaycastHit h) || h.point.y < spot.y - size * 0.5f) return true;
            }
            return false;
        }

        /// <summary>Random rotation for a prop on ground with the given normal, per the rotation settings; identity when random rotation is off.</summary>
        public static Quaternion PropRotation(Vector3 normal)
        {
            if (!RandomRotation) return RotationMode == BrushRotationMode.AlignToSurface ? Quaternion.FromToRotation(Vector3.up, normal) : Quaternion.identity;
            float lo = Mathf.Min(RotationRange.x, RotationRange.y), hi = Mathf.Max(RotationRange.x, RotationRange.y);
            var euler = new Vector3(
                (RotationAxes & BrushRotationAxes.X) != 0 ? Random.Range(lo, hi) : 0f,
                (RotationAxes & BrushRotationAxes.Y) != 0 ? Random.Range(lo, hi) : 0f,
                (RotationAxes & BrushRotationAxes.Z) != 0 ? Random.Range(lo, hi) : 0f);
            Quaternion turn = Quaternion.Euler(euler);
            return RotationMode == BrushRotationMode.AlignToSurface ? Quaternion.FromToRotation(Vector3.up, normal) * turn : turn;
        }

        /// <summary>
        /// True when a point in <paramref name="placed"/> is closer than <paramref name="spacing"/> to <paramref name="point"/>, height ignored.
        /// The brush keeps one list per category, so grass batches only keep their distance from grass batches and trees from trees.
        /// </summary>
        public static bool TooClose(List<Vector3> placed, Vector3 point, float spacing)
        {
            float sq = spacing * spacing;
            foreach (Vector3 q in placed)
            {
                float dx = q.x - point.x, dz = q.z - point.z;
                if (dx * dx + dz * dz < sq) return true;
            }
            return false;
        }
    }
}
