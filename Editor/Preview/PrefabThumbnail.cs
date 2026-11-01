using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>Gallery thumbnails. AssetPreview draws shader-graph foliage pink, so mesh prefabs are drawn with a PreviewRenderUtility (as the window preview does).</summary>
    static class PrefabThumbnail
    {
        const int Size = 128;
        // ponytail: cache lives until domain reload; keyed by asset dependency hash so rebakes refresh
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(GameObject prefab)
        {
            if (prefab == null) return null;
            if (!prefab.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null || !prefab.TryGetComponent(out MeshRenderer mr))
                return AssetPreview.GetAssetPreview(prefab);

            string path = AssetDatabase.GetAssetPath(prefab);
            string key = path + AssetDatabase.GetAssetDependencyHash(path);
            if (Cache.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

            var pru = new PreviewRenderUtility();
            try
            {
                Mesh mesh = mf.sharedMesh;
                Bounds b = mesh.bounds;
                Camera cam = pru.camera;
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(b.extents.magnitude * 0.75f, 0.1f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.55f, 0.7f, 0.85f);
                cam.transform.rotation = Quaternion.Euler(20f, 35f, 0f);
                cam.transform.position = b.center - cam.transform.forward * 100f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 300f;
                pru.lights[0].intensity = 1.3f;
                pru.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                pru.ambientColor = new Color(0.35f, 0.4f, 0.45f);

                pru.BeginStaticPreview(new Rect(0, 0, Size, Size));
                Material[] mats = mr.sharedMaterials;
                for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                    if (mats[s] != null) pru.DrawMesh(mesh, Matrix4x4.identity, mats[s], s);
                pru.Render(true);
                return Cache[key] = pru.EndStaticPreview();
            }
            finally
            {
                pru.Cleanup();
            }
        }
    }
}
