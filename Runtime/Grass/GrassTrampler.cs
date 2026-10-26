using System.Collections.Generic;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// Pushes grass aside around this object. Put it on units, projectiles or effects. The Grass Field sends the
    /// <see cref="GrassInteraction.MaxPushers"/> nearest ones to the shader each frame (needs Features &gt; Trample).
    /// </summary>
    [AddComponentMenu("Meowdieval/Foliage Forge/Grass Trampler")]
    public sealed class GrassTrampler : MonoBehaviour
    {
        [Tooltip("Radius of the flattened circle, in metres.")]
        [SerializeField, Min(0.05f)] float _radius = 0.7f;

        /// <summary>Radius of the flattened circle, in metres.</summary>
        public float Radius { get => _radius; set => _radius = Mathf.Max(0.05f, value); }

        private void OnEnable() => GrassInteraction.Add(this);

        private void OnDisable() => GrassInteraction.Remove(this);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.7f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }

    /// <summary>Registry of active <see cref="GrassTrampler"/>s and the upload to the grass shader.</summary>
    public static class GrassInteraction
    {
        /// <summary>Entries the shader accepts.</summary>
        public const int MaxPushers = 16;

        static readonly List<GrassTrampler> Active = new List<GrassTrampler>();
        static readonly Vector4[] Packed = new Vector4[MaxPushers];
        static readonly int ArrayId = Shader.PropertyToID("_FF_Trample");
        static readonly int CountId = Shader.PropertyToID("_FF_TrampleCount");
        static int _lastFrame = -1;

        internal static void Add(GrassTrampler t)
        {
            if (!Active.Contains(t)) Active.Add(t);
        }

        internal static void Remove(GrassTrampler t) => Active.Remove(t);

        /// <summary>Copies up to <paramref name="max"/> pushers whose circle touches the square around <paramref name="center"/> (xz, half edge <paramref name="halfExtent"/>); returns the count.</summary>
        public static int Collect(Vector4[] dst, int max, Vector2 center, float halfExtent)
        {
            int n = 0;
            for (int i = 0; i < Active.Count && n < max; i++)
            {
                Vector3 p = Active[i].transform.position;
                float r = Active[i].Radius;
                if (Mathf.Abs(p.x - center.x) > halfExtent + r || Mathf.Abs(p.z - center.y) > halfExtent + r) continue;
                dst[n++] = new Vector4(p.x, p.y, p.z, r);
            }
            return n;
        }

        /// <summary>Uploads the pushers nearest to <paramref name="from"/> (once per frame).</summary>
        public static void Upload(Vector3 from)
        {
            if (_lastFrame == Time.frameCount) return;
            _lastFrame = Time.frameCount;
            int n = Active.Count;
            if (n > MaxPushers) Active.Sort((a, b) => (a.transform.position - from).sqrMagnitude.CompareTo((b.transform.position - from).sqrMagnitude));
            n = Mathf.Min(n, MaxPushers);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = Active[i].transform.position;
                Packed[i] = new Vector4(p.x, p.y, p.z, Active[i].Radius);
            }
            Shader.SetGlobalVectorArray(ArrayId, Packed);
            Shader.SetGlobalFloat(CountId, n);
        }
    }

    /// <summary>Pure maths of the trample map window: texel snapping, window mapping, fade. No Unity objects, so it is unit-testable.</summary>
    public static class GrassTrampleMath
    {
        /// <summary>Smallest and largest map resolution.</summary>
        public const int MinResolution = 64, MaxResolution = 1024;

        /// <summary>Nearest power of two inside the allowed range.</summary>
        public static int ClampResolution(int resolution) => Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), MinResolution, MaxResolution);

        /// <summary>
        /// World xz of the window's corner for a window of <paramref name="windowSize"/> metres centred near <paramref name="center"/>, moved in
        /// whole texels so the map's contents never swim as the centre moves.
        /// </summary>
        public static Vector2 SnapCorner(Vector2 center, float windowSize, int resolution)
        {
            float texel = windowSize / resolution;
            float half = windowSize * 0.5f;
            return new Vector2(Mathf.Floor((center.x - half) / texel) * texel, Mathf.Floor((center.y - half) / texel) * texel);
        }

        /// <summary>UV offset to add to a new-window uv to read the same world point from the previous map.</summary>
        public static Vector2 UvShift(Vector2 newCorner, Vector2 oldCorner, float windowSize) => (newCorner - oldCorner) / windowSize;

        /// <summary>Window uv (0..1 inside) of a world xz point.</summary>
        public static Vector2 WorldToUv(Vector2 world, Vector2 corner, float windowSize) => (world - corner) / windowSize;

        /// <summary>Whether a world xz point lies inside the window.</summary>
        public static bool Contains(Vector2 world, Vector2 corner, float windowSize)
        {
            Vector2 uv = WorldToUv(world, corner, windowSize);
            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        /// <summary>Multiplier for the map over <paramref name="dt"/> seconds: a footprint falls to 10% after <paramref name="recoverySeconds"/>.</summary>
        public static float Decay(float dt, float recoverySeconds) => Mathf.Pow(0.1f, Mathf.Max(0f, dt) / Mathf.Max(recoverySeconds, 0.01f));
    }

    /// <summary>
    /// Render-texture trample map: a window of <see cref="GrassProfile.TrampleWindowSize"/> metres that follows the camera (or a target), where
    /// every <see cref="GrassTrampler"/> in range is stamped each frame and old footprints fade. Two half-float maps ping-pong through the
    /// TrampleSplat pass of the grass shader; the grass vertex shader reads the result (_FF_TrampleRT, _FF_TrampleRect). One map is shared by all
    /// grass fields; the first field drawn each frame supplies the settings.
    /// </summary>
    public static class GrassTrampleRT
    {
        /// <summary>Pushers the splat pass takes per frame.</summary>
        public const int MaxSplat = 64;

        static readonly int TextureId = Shader.PropertyToID("_FF_TrampleRT");
        static readonly int RectId = Shader.PropertyToID("_FF_TrampleRect");
        static readonly int SplatRectId = Shader.PropertyToID("_FF_SplatRect");
        static readonly int SplatParamsId = Shader.PropertyToID("_FF_SplatParams");
        static readonly int SplatPushersId = Shader.PropertyToID("_FF_SplatPushers");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly Vector4[] Pushers = new Vector4[MaxSplat];

        static RenderTexture _current, _spare;
        static Material _material;
        static int _pass = -1, _resolution, _users, _lastFrame = -1;
        static float _size;
        static Vector2 _corner;
        static bool _hasCorner;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void HookReload() => UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Release;
#endif

        /// <summary>True once the map exists.</summary>
        public static bool Ready => _current != null;

        /// <summary>A grass field started using the map.</summary>
        public static void AddUser() => _users++;

        /// <summary>A grass field stopped; the last one frees the maps.</summary>
        public static void RemoveUser()
        {
            _users = Mathf.Max(0, _users - 1);
            if (_users == 0) Release();
        }

        /// <summary>Creates (or recreates after a settings change) the maps and binds them as globals. False when the shader or format is unusable.</summary>
        public static bool Ensure(GrassProfile profile)
        {
            if (profile == null || profile.Shader == null) return false;
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) return false;
            int res = GrassTrampleMath.ClampResolution(profile.TrampleMapResolution);
            float size = Mathf.Max(1f, profile.TrampleWindowSize);
            if (_material == null || _material.shader != profile.Shader)
            {
                if (_material != null) Object.DestroyImmediate(_material);
                _material = new Material(profile.Shader) { hideFlags = HideFlags.HideAndDontSave };
                _pass = _material.FindPass("TrampleSplat");
            }
            if (_pass < 0) return false;
            if (_current == null || _spare == null || _resolution != res || _size != size)
            {
                DestroyMaps();
                _resolution = res;
                _size = size;
                _current = NewMap(res);
                _spare = NewMap(res);
                _hasCorner = false;
            }
            Bind();
            return true;
        }

        private static RenderTexture NewMap(int res)
        {
            var rt = new RenderTexture(res, res, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = "FF_TrampleMap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                hideFlags = HideFlags.HideAndDontSave,
            };
            rt.Create();
            Clear(rt);
            return rt;
        }

        private static void Clear(RenderTexture rt)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = prev;
        }

        private static void Bind()
        {
            Shader.SetGlobalTexture(TextureId, _current);
            Shader.SetGlobalVector(RectId, new Vector4(_corner.x, _corner.y, 1f / _size, 1f / _size));
        }

        /// <summary>
        /// Moves the window to <paramref name="center"/>, fades the map and stamps the pushers in range. Runs once per frame; later calls only rebind.
        /// </summary>
        public static void Tick(GrassProfile profile, Vector3 center)
        {
            if (!Ensure(profile) || _lastFrame == Time.frameCount) return;
            _lastFrame = Time.frameCount;

            Vector2 corner = GrassTrampleMath.SnapCorner(new Vector2(center.x, center.z), _size, _resolution);
            Vector2 shift = _hasCorner ? GrassTrampleMath.UvShift(corner, _corner, _size) : Vector2.zero;
            float decay = GrassTrampleMath.Decay(Mathf.Min(Time.deltaTime, 0.1f), profile.TrampleRecoverySeconds);
            int n = GrassInteraction.Collect(Pushers, MaxSplat, new Vector2(corner.x + _size * 0.5f, corner.y + _size * 0.5f), _size * 0.5f);

            _material.SetVector(SplatRectId, new Vector4(corner.x, corner.y, _size, _resolution));
            _material.SetVector(SplatParamsId, new Vector4(decay, n, shift.x, shift.y));
            _material.SetVectorArray(SplatPushersId, Pushers);
            _material.SetTexture(MainTexId, _current);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(_current, _spare, _material, _pass);
            RenderTexture.active = prev;

            (_current, _spare) = (_spare, _current);
            _corner = corner;
            _hasCorner = true;
            Bind();
        }

        private static void DestroyMaps()
        {
            if (_current != null)
            {
                _current.Release();
                Object.DestroyImmediate(_current);
            }
            if (_spare != null)
            {
                _spare.Release();
                Object.DestroyImmediate(_spare);
            }
            _current = _spare = null;
        }

        /// <summary>Frees the maps and the splat material.</summary>
        public static void Release()
        {
            DestroyMaps();
            if (_material != null) Object.DestroyImmediate(_material);
            _material = null;
            _pass = -1;
            _hasCorner = false;
        }
    }
}
