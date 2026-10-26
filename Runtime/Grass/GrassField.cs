using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// Scatters grass over the ground inside its box and draws it with GPU instancing, one instanced call per 1023 clumps
    /// per chunk. Chunks are frustum-culled per camera; distance thinning, fade, wind, trample and lighting run in the
    /// shader. Scatter is deterministic from the profile seed and runs on enable (and a moment after edits); nothing is saved.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Meowdieval/Foliage Forge/Grass Field")]
    public sealed class GrassField : MonoBehaviour
    {
        const int MaxPerCall = 1023;

        sealed class Chunk
        {
            public Bounds Bounds;
            public bool HasBounds;
            public List<Matrix4x4>[] Building;
            public Matrix4x4[][] PerVariant;
            public float[][] Keys; // per variant, ascending shader keep values matching PerVariant
        }

        [SerializeField] GrassProfile _profile;
        [Tooltip("Centre of the trample map window (Features > Trample Map). Empty follows the camera. Set it to the player or army centre when the camera sits far from the action.")]
        [SerializeField] Transform _trampleTarget;
        [Tooltip("Box (local to this object) that grass fills. Rays are cast down from the top of the box.")]
        [SerializeField] Vector3 _size = new Vector3(40f, 10f, 40f);
        [Tooltip("Layers grass may grow on. Leave out units, buildings and props.")]
        [SerializeField] LayerMask _ground = ~0;
        [Tooltip("Top-down colour of the ground under the box (Capture Ground Colour in the inspector). Used when Features > Ground Map is on.")]
        [SerializeField] Texture2D _groundMap;
        [Tooltip("Fill the whole box automatically. Off leaves only what is painted with the brush.")]
        [SerializeField] bool _autoFill = true;
        [SerializeField, HideInInspector] GrassStroke[] _strokes = new GrassStroke[0];

        readonly List<Chunk> _chunks = new List<Chunk>();
        Material[] _materials = new Material[0];
        Mesh[][] _meshes = new Mesh[0][]; // per variant, per mesh LOD (null entries fall back to LOD 0)
        readonly Plane[] _planes = new Plane[6];
        int _revision = -1;
        float _rebuildAt;
        bool _dirty = true;

        /// <summary>Profile that drives this field.</summary>
        public GrassProfile Profile
        {
            get => _profile;
            set
            {
                _profile = value;
                _dirty = true;
            }
        }

        /// <summary>Layers grass grows on.</summary>
        public LayerMask Ground
        {
            get => _ground;
            set
            {
                _ground = value;
                _dirty = true;
            }
        }

        /// <summary>Whether the box fills itself, or only brush strokes grow grass.</summary>
        public bool AutoFill
        {
            get => _autoFill;
            set
            {
                _autoFill = value;
                _dirty = true;
            }
        }

        /// <summary>Brush dabs, in order.</summary>
        public int StrokeCount => _strokes.Length;

        /// <summary>Adds a brush dab. Pass rebuild = false while dragging and call <see cref="Rebuild"/> once on release.</summary>
        public void AddStroke(GrassStroke stroke, bool rebuild = true)
        {
            System.Array.Resize(ref _strokes, _strokes.Length + 1);
            _strokes[_strokes.Length - 1] = stroke;
            if (rebuild) _dirty = true;
        }

        /// <summary>Removes every brush dab.</summary>
        public void ClearStrokes()
        {
            _strokes = new GrassStroke[0];
            _dirty = true;
        }

        /// <summary>Ground colour map, or null.</summary>
        public Texture2D GroundMap
        {
            get => _groundMap;
            set
            {
                _groundMap = value;
                _dirty = true;
            }
        }

        /// <summary>Clumps scattered, all variants.</summary>
        public int InstanceCount { get; private set; }

        /// <summary>Clumps submitted to the GPU on the last camera draw.</summary>
        public int DrawnLastCamera { get; private set; }

        /// <summary>Instanced draw calls issued on the last camera draw.</summary>
        public int DrawCallsLastCamera { get; private set; }

        /// <summary>Chunk count after the last scatter.</summary>
        public int ChunkCount => _chunks.Count;

        /// <summary>Scatter time of the last rebuild in milliseconds.</summary>
        public float LastRebuildMs { get; private set; }

        /// <summary>Axis-aligned world bounds of the (possibly rotated) box.</summary>
        public Bounds WorldBounds
        {
            get
            {
                Vector3 h = _size * 0.5f;
                var b = new Bounds(transform.position, Vector3.zero);
                for (int i = 0; i < 8; i++)
                    b.Encapsulate(transform.TransformPoint(new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z)));
                return b;
            }
        }

        /// <summary>Queues a rescatter.</summary>
        public void Rebuild() => _dirty = true;

        private void OnEnable()
        {
            _dirty = true;
            GrassTrampleRT.AddUser();
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
#if UNITY_EDITOR
            UnityEditor.Undo.undoRedoPerformed += Rebuild; // OnValidate is not reliably called after an undo
#endif
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            GrassTrampleRT.RemoveUser();
#if UNITY_EDITOR
            UnityEditor.Undo.undoRedoPerformed -= Rebuild;
#endif
            _chunks.Clear();
            InstanceCount = 0;
            for (int i = 0; i < _materials.Length; i++) Dispose(ref _materials[i]);
            DisposeMeshes();
        }

        private void DisposeMeshes()
        {
            for (int i = 0; i < _meshes.Length; i++)
                if (_meshes[i] != null)
                    for (int l = 0; l < _meshes[i].Length; l++) Dispose(ref _meshes[i][l]);
        }

        private void OnValidate()
        {
            _dirty = true;
            _rebuildAt = Time.realtimeSinceStartup + 0.3f;
        }

        private static void Dispose<T>(ref T o) where T : UnityEngine.Object
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
            o = null;
        }

        private void Update()
        {
            if (_profile == null) return;
            if (_profile.Revision != _revision)
            {
                _revision = _profile.Revision;
                _dirty = true;
                _rebuildAt = Time.realtimeSinceStartup + 0.3f;
            }
            if (_dirty && Time.realtimeSinceStartup >= _rebuildAt)
            {
                _dirty = false;
                Scatter();
            }
#if UNITY_EDITOR
            if (!Application.isPlaying) SyncMaterials(); // live look edits; builds sync once per scatter
#endif
        }

        private void Scatter()
        {
            float t0 = Time.realtimeSinceStartup;
            _chunks.Clear();
            InstanceCount = 0;
            if (_profile == null || _profile.Shader == null || _profile.Variants == null) return;

            DisposeMeshes();
            for (int i = 0; i < _materials.Length; i++) Dispose(ref _materials[i]);

            var lookup = new Dictionary<Vector2Int, Chunk>();
            float chunkSize = _profile.ChunkSize;
            float cosSlope = Mathf.Cos(_profile.SlopeLimit * Mathf.Deg2Rad);
            int budget = _profile.MaxInstances;
            int variantCount = _profile.Variants.Length;
            if (_meshes.Length != variantCount)
            {
                _meshes = new Mesh[variantCount][];
                _materials = new Material[variantCount];
            }
            var points = new List<Vector3>();
            var yaws = new List<float>();
            var seqs = new List<int>();
            var lens = new List<float>();
            var stage = new List<Vector3>();
            var stageYaws = new List<float>();
            float rayLength = _size.y * Mathf.Abs(transform.lossyScale.y);
            float strokeRay = 6f * Mathf.Abs(transform.lossyScale.y);
            Matrix4x4 toWorld = transform.localToWorldMatrix, toLocal = transform.worldToLocalMatrix;
            PhysicsScene physics = gameObject.scene.GetPhysicsScene(); // prefab stages have their own

            for (int v = 0; v < variantCount; v++)
            {
                GrassVariant gv = _profile.Variants[v];
                if (gv.MixedDensity <= 0f || budget <= 0) continue;

                _meshes[v] = new Mesh[3];
                for (int l = 0; l < (_profile.Features.MeshLod ? 3 : 1); l++) _meshes[v][l] = GrassMeshes.Build(gv, l);
                var rng = new System.Random(_profile.Seed * 7919 + v * 104729);
                points.Clear();
                yaws.Clear();
                seqs.Clear();
                lens.Clear();
                if (_autoFill)
                {
                    GrassScatter.Candidates(_profile, v, new Vector2(_size.x, _size.z), _size.y * 0.5f, toWorld, rng, budget * 2, stage, stageYaws, true);
                    points.AddRange(stage);
                    yaws.AddRange(stageYaws);
                    for (int k = 0; k < stage.Count; k++)
                    {
                        seqs.Add(0);
                        lens.Add(rayLength);
                    }
                }
                for (int si = 0; si < _strokes.Length; si++)
                {
                    if (_strokes[si].Erase) continue;
                    stage.Clear();
                    stageYaws.Clear();
                    GrassScatter.StrokeCandidates(_profile, v, _strokes[si], toWorld, stage, stageYaws);
                    points.AddRange(stage);
                    yaws.AddRange(stageYaws);
                    for (int k = 0; k < stage.Count; k++)
                    {
                        seqs.Add(si + 1);
                        lens.Add(strokeRay);
                    }
                }
                int count = points.Count;
                if (count == 0) continue;

                var commands = new NativeArray<RaycastCommand>(count, Allocator.TempJob);
                var results = new NativeArray<RaycastHit>(count, Allocator.TempJob);
                var query = new QueryParameters(_ground.value);
                for (int i = 0; i < count; i++) commands[i] = new RaycastCommand(physics, points[i], Vector3.down, query, lens[i]);
                RaycastCommand.ScheduleBatch(commands, results, 64, 1).Complete();

                for (int i = 0; i < count && budget > 0; i++)
                {
                    RaycastHit hit = results[i];
                    if (hit.normal.sqrMagnitude < 0.5f || hit.normal.y < cosSlope) continue;
                    if (GrassScatter.Erased(toLocal.MultiplyPoint3x4(hit.point), seqs[i], _strokes, v)) continue;
                    Matrix4x4 m = GrassScatter.Clump(_profile, gv, rng, hit.point, hit.normal, yaws[i], out float height, out float width);

                    var key = new Vector2Int(Mathf.FloorToInt(hit.point.x / chunkSize), Mathf.FloorToInt(hit.point.z / chunkSize));
                    if (!lookup.TryGetValue(key, out Chunk chunk))
                    {
                        chunk = new Chunk { Building = new List<Matrix4x4>[variantCount], PerVariant = new Matrix4x4[variantCount][], Keys = new float[variantCount][] };
                        lookup[key] = chunk;
                    }
                    var clump = new Bounds(hit.point + Vector3.up * (height * 0.5f), new Vector3(width * 2f, height, width * 2f));
                    if (chunk.HasBounds) chunk.Bounds.Encapsulate(clump);
                    else
                    {
                        chunk.Bounds = clump;
                        chunk.HasBounds = true;
                    }
                    (chunk.Building[v] ??= new List<Matrix4x4>()).Add(m);
                    budget--;
                    InstanceCount++;
                }
                commands.Dispose();
                results.Dispose();
            }

            foreach (Chunk chunk in lookup.Values)
            {
                for (int v = 0; v < chunk.Building.Length; v++)
                {
                    if (chunk.Building[v] == null) continue;
                    Matrix4x4[] arr = chunk.Building[v].ToArray();
                    var keys = new float[arr.Length];
                    for (int i = 0; i < arr.Length; i++)
                    {
                        Vector3 p = arr[i].GetPosition();
                        keys[i] = GrassLod.Keep(p.x, p.z);
                    }
                    Array.Sort(keys, arr); // ascending, so the clumps the shader keeps at a given share are a prefix
                    chunk.PerVariant[v] = arr;
                    chunk.Keys[v] = keys;
                }
                chunk.Building = null;
                chunk.Bounds.Expand(new Vector3(0.6f, 0.2f, 0.6f)); // wind headroom
                _chunks.Add(chunk);
            }

            SyncMaterials();
            LastRebuildMs = (Time.realtimeSinceStartup - t0) * 1000f;
        }

        private void SyncMaterials()
        {
            if (_profile == null || _profile.Shader == null) return;
            Bounds wb = WorldBounds;
            var rect = new Vector4(wb.min.x, wb.min.z, 1f / Mathf.Max(wb.size.x, 0.01f), 1f / Mathf.Max(wb.size.z, 0.01f));
            for (int v = 0; v < _meshes.Length && v < _profile.Variants.Length; v++)
            {
                if (_meshes[v] == null || _meshes[v][0] == null) continue;
                Material m = _materials[v];
                if (m == null) m = _materials[v] = new Material(_profile.Shader) { hideFlags = HideFlags.HideAndDontSave };
                GrassMaterials.Apply(m, _profile, _profile.Variants[v], _groundMap, rect);
            }
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            DrawnLastCamera = 0;
            DrawCallsLastCamera = 0;
            if (_profile == null || _chunks.Count == 0) return;
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;

            GrassFeatures f = _profile.Features;
            float maxDist = _profile.DrawDistance;
            Vector3 camPos = cam.transform.position;
            if (f.WindTexture) GrassWindNoise.Ensure(); // statics reset on a domain reload and builds only sync materials once
            if (f.TrampleRT && GrassTrampleRT.Ensure(_profile))
            {
                // one camera drives the map: the target if set, else the game camera (any camera outside play mode)
                if (_trampleTarget != null) GrassTrampleRT.Tick(_profile, _trampleTarget.position);
                else if (cam.cameraType == CameraType.Game || !Application.isPlaying) GrassTrampleRT.Tick(_profile, camPos);
            }
            else if (f.Trample) GrassInteraction.Upload(camPos);
            GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            var shadows = f.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            bool thin = f.CpuThinning && f.DistanceLod;
            float lodNear = maxDist * _profile.LodMid;
            float lodFar = maxDist * Mathf.Max(_profile.LodFar, _profile.LodMid + 0.01f); // same values GrassMaterials.Apply gives the shader

            foreach (Chunk chunk in _chunks)
            {
                float sqr = chunk.Bounds.SqrDistance(camPos);
                if (sqr > maxDist * maxDist) continue;
                if (f.FrustumCulling && !GeometryUtility.TestPlanesAABB(_planes, chunk.Bounds)) continue;

                float dist = Mathf.Sqrt(sqr);
                int lod = f.MeshLod ? GrassLod.MeshLevel(dist, maxDist, _profile.LodMid, _profile.LodFar) : 0;
                float share = thin ? GrassLod.Share(dist, lodNear, lodFar, _profile.LodMinShare) : 1f;

                for (int v = 0; v < chunk.PerVariant.Length; v++)
                {
                    Matrix4x4[] list = chunk.PerVariant[v];
                    if (list == null || _materials[v] == null || _meshes[v] == null) continue;
                    Mesh mesh = _meshes[v][lod] != null ? _meshes[v][lod] : _meshes[v][0];
                    if (mesh == null) continue;
                    int total = thin ? GrassLod.PrefixCount(chunk.Keys[v], share) : list.Length;
                    if (total == 0) continue;
                    var rp = new RenderParams(_materials[v])
                    {
                        camera = cam,
                        layer = gameObject.layer,
                        shadowCastingMode = shadows,
                        receiveShadows = f.ReceiveShadows,
                        worldBounds = chunk.Bounds,
                    };
                    for (int start = 0; start < total; start += MaxPerCall)
                    {
                        int batch = Mathf.Min(MaxPerCall, total - start);
                        Graphics.RenderMeshInstanced(rp, mesh, 0, list, batch, start);
                        DrawnLastCamera += batch;
                        DrawCallsLastCamera++;
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.45f, 0.85f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            for (int i = 0; i < 48; i++)
            {
                float a0 = i * Mathf.PI / 24f, a1 = (i + 1) * Mathf.PI / 24f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * _size.x, 0f, Mathf.Sin(a0) * _size.z) * 0.5f;
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * _size.x, 0f, Mathf.Sin(a1) * _size.z) * 0.5f;
                Gizmos.DrawLine(p0 + Vector3.up * _size.y * 0.5f, p1 + Vector3.up * _size.y * 0.5f);
                Gizmos.DrawLine(p0 - Vector3.up * _size.y * 0.5f, p1 - Vector3.up * _size.y * 0.5f);
            }
        }
    }
}
