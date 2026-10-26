using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// GPU placement alternative to <see cref="GrassField"/>. Nothing is scattered on the CPU: each camera dispatches
    /// GrassPlacement.compute over a grid of cells around it, the compute shader appends the surviving clumps to a buffer, and
    /// Graphics.RenderMeshIndirect draws them with the profile's grass material. Density comes from a texture window (or is uniform),
    /// ground height from a height texture (or the flat plane at this object's Y). Does not raycast: no slope limit, no ground layer
    /// mask, no brush strokes. Turns itself off with one log line when compute shaders are not supported.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Meowdieval/Foliage Forge/Grass GPU Field")]
    public sealed class GrassGpuField : MonoBehaviour
    {
        const int MaxCandidates = 8 * 1024 * 1024;
        const int CameraIdleFrames = 120;

        sealed class CameraBuffers
        {
            public GraphicsBuffer[] Blades;
            public GraphicsBuffer Counter;
            public GraphicsBuffer Args;
            public int Capacity;
            public int LastFrame;

            public void Release()
            {
                if (Blades != null) foreach (GraphicsBuffer b in Blades) b?.Release();
                Counter?.Release();
                Args?.Release();
                Blades = null;
                Counter = null;
                Args = null;
            }
        }

        [Header("Look")]
        [Tooltip("Grass profile. Supplies the types, material look, draw distance, LOD shares and seed.")]
        [SerializeField] GrassProfile _profile;
        [Tooltip("GrassPlacement.compute. Filled in automatically in the editor.")]
        [SerializeField] ComputeShader _compute;

        [Header("Maps")]
        [Tooltip("World rectangle (x = world X, y = world Z) the density and height textures cover. Outside it nothing grows when a density texture is set.")]
        [SerializeField] Rect _window = new Rect(-50f, -50f, 100f, 100f);
        [Tooltip("R = density 0..1, G = height scale (when enabled below), B = grass type (when enabled below). Empty grows everywhere at full density.")]
        [SerializeField] Texture2D _densityTexture;
        [Tooltip("Use the density texture's G channel as a height multiplier.")]
        [SerializeField] bool _heightScaleFromG;
        [Tooltip("Use the density texture's B channel to pick the grass type: 0 = first type, 1 = last. Off lets every type grow by its own density.")]
        [SerializeField] bool _typeFromB;
        [Tooltip("Ground height, R channel 0..1. Same window as the density texture. Empty puts grass on the flat plane at this object's Y.")]
        [SerializeField] Texture2D _heightTexture;
        [Tooltip("World height of height texture value 0 (a Terrain's position Y).")]
        [SerializeField] float _heightMin;
        [Tooltip("World height of height texture value 1 minus the value at 0 (a Terrain's size Y).")]
        [Min(0f)] [SerializeField] float _heightRange = 10f;

        [Header("Placement")]
        [Tooltip("Side of a placement cell in metres. Candidates per cell follow the type's density. Smaller cells give a finer, costlier grid.")]
        [Range(0.5f, 16f)] [SerializeField] float _cellSize = 2f;
        [Tooltip("Metres around the camera that are filled. 0 uses the profile's draw distance.")]
        [Min(0f)] [SerializeField] float _radius;
        [Tooltip("Capacity of the blade buffer per grass type and camera (32 bytes each). Blades past it are dropped.")]
        [Min(1000)] [SerializeField] int _maxBlades = 200000;
        [Tooltip("Multiplier on every type's density.")]
        [Min(0f)] [SerializeField] float _densityScale = 1f;

        [Header("Culling")]
        [Tooltip("Drop clumps outside the camera frustum in the compute shader.")]
        [SerializeField] bool _frustumCull = true;
        [Tooltip("Thin far clumps with the profile's LOD share, as the CPU path does. Needs Distance Lod in the profile features.")]
        [SerializeField] bool _distanceThin = true;
        [Tooltip("Drop clumps hidden behind a depth pyramid. Needs the texture below; skipped when it is empty. Assumes reversed-Z and a min-depth (farthest) mip chain of the camera depth.")]
        [SerializeField] bool _hiZCull;
        [Tooltip("Depth pyramid for Hi-Z culling: single channel, full mip chain, min depth per tile.")]
        [SerializeField] Texture _hiZTexture;
        [Tooltip("Depth slack for the Hi-Z test.")]
        [Range(0f, 0.01f)] [SerializeField] float _hiZBias = 0.0005f;

        readonly Dictionary<Camera, CameraBuffers> _cameras = new Dictionary<Camera, CameraBuffers>();
        readonly Vector4[] _planes = new Vector4[6];
        readonly List<Camera> _stale = new List<Camera>();
        Material[] _materials = new Material[0];
        Mesh[] _meshes = new Mesh[0];
        MaterialPropertyBlock[] _props = new MaterialPropertyBlock[0];
        int _revision = -1;
        int _kernelPlace = -1, _kernelArgs = -1;
        bool _failed;

        /// <summary>Profile that drives this field.</summary>
        public GrassProfile Profile
        {
            get => _profile;
            set
            {
                _profile = value;
                _revision = -1;
            }
        }

        /// <summary>Candidate threads dispatched for the last camera, all types.</summary>
        public long CandidatesLastCamera { get; private set; }

        /// <summary>Cells per side used for the last camera.</summary>
        public int CellsPerSideLastCamera { get; private set; }

        /// <summary>True when the compute shader and its kernels are usable.</summary>
        public static bool IsSupported => SystemInfo.supportsComputeShaders;

#if UNITY_EDITOR
        private void Reset() => _compute = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Packages/com.meowdieval.foliageforge/Shaders/GrassPlacement.compute");
#endif

        private void OnEnable()
        {
            _failed = false;
            _revision = -1;
            if (!IsSupported)
            {
                Fail("Grass GPU Field needs compute shader support, which this device does not have. Use Grass Field instead.");
                return;
            }
#if UNITY_EDITOR
            if (_compute == null) Reset();
#endif
            if (_compute == null)
            {
                Fail("Grass GPU Field has no GrassPlacement compute shader assigned.");
                return;
            }
            _kernelPlace = _compute.FindKernel("CSPlace");
            _kernelArgs = _compute.FindKernel("CSArgs");
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            ReleaseAll();
        }

        private void OnDestroy() => ReleaseAll();

        private void OnValidate() => _revision = -1;

        private void Fail(string message)
        {
            if (_failed) return;
            _failed = true;
            Debug.LogWarning(message, this);
            enabled = false;
        }

        private void ReleaseAll()
        {
            foreach (CameraBuffers c in _cameras.Values) c.Release();
            _cameras.Clear();
            for (int i = 0; i < _materials.Length; i++) Dispose(ref _materials[i]);
            for (int i = 0; i < _meshes.Length; i++) Dispose(ref _meshes[i]);
            _materials = new Material[0];
            _meshes = new Mesh[0];
        }

        private static void Dispose<T>(ref T o) where T : UnityEngine.Object
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
            o = null;
        }

        private void SyncResources()
        {
            for (int i = 0; i < _materials.Length; i++) Dispose(ref _materials[i]);
            for (int i = 0; i < _meshes.Length; i++) Dispose(ref _meshes[i]);
            int n = _profile.Variants.Length;
            _materials = new Material[n];
            _meshes = new Mesh[n];
            for (int v = 0; v < n; v++)
            {
                GrassVariant gv = _profile.Variants[v];
                if (gv.MixedDensity <= 0f) continue;
                _meshes[v] = GrassMeshes.Build(gv, 0);
                var m = _materials[v] = new Material(_profile.Shader) { hideFlags = HideFlags.HideAndDontSave };
                GrassMaterials.Apply(m, _profile, gv, null, Vector4.zero);
                m.SetVector("_Use8", new Vector4(1f, 0f, 0f, 0f));
            }
            _revision = _profile.Revision;
        }

        private CameraBuffers BuffersFor(Camera cam, int variants)
        {
            Camera id = cam;
            if (_cameras.TryGetValue(id, out CameraBuffers c) && (c.Capacity != _maxBlades || c.Blades.Length != variants))
            {
                c.Release();
                _cameras.Remove(id);
                c = null;
            }
            if (c == null)
            {
                c = new CameraBuffers { Capacity = _maxBlades, Blades = new GraphicsBuffer[variants] };
                for (int v = 0; v < variants; v++) c.Blades[v] = new GraphicsBuffer(GraphicsBuffer.Target.Append, _maxBlades, GrassGpuMath.BladeStride);
                c.Counter = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 1, 4);
                c.Args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GrassGpuMath.ArgsBytes);
                _cameras[id] = c;
            }
            c.LastFrame = Time.frameCount;
            return c;
        }

        private void ReleaseIdleCameras()
        {
            _stale.Clear();
            foreach (KeyValuePair<Camera, CameraBuffers> kv in _cameras)
                if (kv.Key == null || Time.frameCount - kv.Value.LastFrame > CameraIdleFrames) _stale.Add(kv.Key);
            foreach (Camera id in _stale)
            {
                _cameras[id].Release();
                _cameras.Remove(id);
            }
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (_profile == null || _profile.Shader == null || _profile.Variants == null || _compute == null) return;
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
            if (_profile.Revision != _revision || _materials.Length != _profile.Variants.Length) SyncResources();
            ReleaseIdleCameras();

            GrassFeatures f = _profile.Features;
            if (f.WindTexture) GrassWindNoise.Ensure();
            if (_props.Length != _profile.Variants.Length) System.Array.Resize(ref _props, _profile.Variants.Length);

            float radius = _radius > 0f ? _radius : _profile.DrawDistance;
            float cell = _cellSize;
            int variants = _profile.Variants.Length;
            CameraBuffers cb = BuffersFor(cam, variants);
            Vector3 camPos = cam.transform.position;

            uint flags = 0;
            if (_densityTexture != null)
            {
                flags |= GrassGpuMath.FlagDensityTex;
                if (_heightScaleFromG) flags |= GrassGpuMath.FlagHeightScale;
                if (_typeFromB) flags |= GrassGpuMath.FlagTypeFromB;
            }
            if (_heightTexture != null) flags |= GrassGpuMath.FlagHeightTex;
            if (_frustumCull) flags |= GrassGpuMath.FlagFrustum;
            if (_distanceThin && f.DistanceLod) flags |= GrassGpuMath.FlagDistanceThin;
            bool hiZ = _hiZCull && _hiZTexture != null;
            if (hiZ) flags |= GrassGpuMath.FlagHiZ;

            Matrix4x4 view = cam.worldToCameraMatrix;
            GrassGpuMath.FrustumPlanes(cam.projectionMatrix * view, _planes);
            Vector4 window = GrassGpuMath.Window(_window);
            Texture density = _densityTexture != null ? _densityTexture : Texture2D.whiteTexture;
            Texture height = _heightTexture != null ? _heightTexture : Texture2D.blackTexture;

            _compute.SetInt("_Flags", (int)flags);
            _compute.SetInt("_Seed", _profile.Seed);
            _compute.SetInt("_VariantCount", variants);
            _compute.SetFloat("_CellSize", cell);
            _compute.SetVector("_CamPos", new Vector4(camPos.x, camPos.y, camPos.z, radius));
            _compute.SetVectorArray("_Planes", _planes);
            _compute.SetVector("_Window", window);
            _compute.SetVector("_HeightWindow", window);
            _compute.SetVector("_HeightParams", new Vector4(_heightMin, _heightRange, transform.position.y, 0f));
            _compute.SetVector("_LodParams", new Vector4(radius * _profile.LodMid, radius * Mathf.Max(_profile.LodFar, _profile.LodMid + 0.01f), _profile.LodMinShare, 0f));
            _compute.SetMatrix("_ViewProj", GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * view);
            if (hiZ)
                _compute.SetVector("_HiZParams", new Vector4(_hiZTexture.mipmapCount, _hiZTexture.width, _hiZTexture.height, _hiZBias));
            _compute.SetTexture(_kernelPlace, "_DensityTex", density);
            _compute.SetTexture(_kernelPlace, "_HeightTex", height);
            _compute.SetTexture(_kernelPlace, "_HiZ", hiZ ? _hiZTexture : Texture2D.blackTexture);
            _compute.SetInt("_MaxBlades", _maxBlades);

            CandidatesLastCamera = 0;
            ShadowCastingMode shadows = f.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            var bounds = new Bounds(camPos, new Vector3(radius * 2f, radius * 2f, radius * 2f));

            for (int v = 0; v < variants; v++)
            {
                GrassVariant gv = _profile.Variants[v];
                Mesh mesh = _meshes[v];
                if (mesh == null || _materials[v] == null) continue;

                float density01 = gv.MixedDensity * _densityScale;
                if (density01 <= 0f) continue;
                int perCell = GrassGpuMath.PerCell(density01, cell);
                float fit = GrassGpuMath.FitRadius(radius, cell, perCell, MaxCandidates);
                int side = GrassGpuMath.CellsPerSide(fit, cell);
                long total = GrassGpuMath.TotalCandidates(side, perCell);
                GrassGpuMath.DispatchGroups(total, out int gx, out int gy);
                CandidatesLastCamera += total;
                CellsPerSideLastCamera = side;

                GraphicsBuffer blades = cb.Blades[v];
                blades.SetCounterValue(0);
                _compute.SetBuffer(_kernelPlace, "_Blades", blades);
                _compute.SetInt("_Variant", v);
                _compute.SetInt("_GridOriginX", GrassGpuMath.GridOrigin(camPos.x, cell, side));
                _compute.SetInt("_GridOriginZ", GrassGpuMath.GridOrigin(camPos.z, cell, side));
                _compute.SetInt("_Side", side);
                _compute.SetInt("_PerCell", perCell);
                _compute.SetInt("_Total", (int)total);
                _compute.SetInt("_DispatchWidth", gx * GrassGpuMath.GroupSize);
                _compute.SetFloat("_Accept", GrassGpuMath.Accept(density01, cell, perCell));
                _compute.SetVector("_SizeRange", new Vector4(gv.Height.x, gv.Height.y, gv.Width.x, gv.Width.y));
                _compute.SetVector("_CamPos", new Vector4(camPos.x, camPos.y, camPos.z, fit));
                _compute.Dispatch(_kernelPlace, gx, gy, 1);

                GraphicsBuffer.CopyCount(blades, cb.Counter, 0);
                _compute.SetBuffer(_kernelArgs, "_Counter", cb.Counter);
                _compute.SetBuffer(_kernelArgs, "_Args", cb.Args);
                _compute.SetInt("_ArgIndexCount", (int)mesh.GetIndexCount(0));
                _compute.Dispatch(_kernelArgs, 1, 1, 1);

                MaterialPropertyBlock props = _props[v] ??= new MaterialPropertyBlock();
                props.Clear();
                props.SetBuffer("_FF_Blades", blades);
                var rp = new RenderParams(_materials[v])
                {
                    camera = cam,
                    layer = gameObject.layer,
                    shadowCastingMode = shadows,
                    receiveShadows = f.ReceiveShadows,
                    worldBounds = bounds,
                    matProps = props,
                };
                Graphics.RenderMeshIndirect(rp, mesh, cb.Args);
            }
        }
    }
}
