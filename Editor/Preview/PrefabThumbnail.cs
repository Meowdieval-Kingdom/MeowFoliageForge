using UnityEditor;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// Gallery thumbnails, rendered once at bake time and saved beside the prefab as {name}_Thumb.png.
    /// AssetPreview draws shader-graph foliage pink, and rendering on demand from UI code breaks URP pipeline creation,
    /// so the galleries only load the saved PNG (AssetPreview stays the fallback for prefabs baked before this existed).
    /// </summary>
    static class PrefabThumbnail
    {
        const int Size = 128;

        static string PathFor(string prefabPath) => prefabPath.Substring(0, prefabPath.Length - ".prefab".Length) + "_Thumb.png";

        /// <summary>Renders mesh + materials to {folder}/{name}_Thumb.png. Never throws: a missing thumbnail just falls back.</summary>
        public static void Save(string prefabPath, Mesh mesh, Material[] mats)
        {
            if (mesh == null || mats == null || string.IsNullOrEmpty(prefabPath)) return;
            var pru = new PreviewRenderUtility();
            try
            {
                Bounds b = mesh.bounds;
                Camera cam = pru.camera;
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(b.extents.magnitude * 0.8f, 0.1f);
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
                for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                    if (mats[s] != null) pru.DrawMesh(mesh, Matrix4x4.identity, mats[s], s);
                pru.Render(true);
                Texture2D tex = pru.EndStaticPreview();
                string path = PathFor(prefabPath);
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Foliage Forge: could not save a thumbnail for {prefabPath}: {e.Message}");
            }
            finally
            {
                pru.Cleanup();
            }
        }

        /// <summary>The saved thumbnail, else AssetPreview's (null while it renders).</summary>
        public static Texture2D Get(GameObject prefab)
        {
            if (prefab == null) return null;
            string path = AssetDatabase.GetAssetPath(prefab);
            if (path.EndsWith(".prefab"))
            {
                var saved = AssetDatabase.LoadAssetAtPath<Texture2D>(PathFor(path));
                if (saved != null) return saved;
            }
            return AssetPreview.GetAssetPreview(prefab);
        }
    }
}
