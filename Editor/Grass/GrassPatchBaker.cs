using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meowdieval.FoliageForge
{
    /// <summary>
    /// Bakes each enabled grass type of a profile into its own small circular batch prefab (one mesh, one material) at the type's
    /// full Density: Mix % is not baked in, batches are blended with the brush palette's own Mix %. Each vertex carries its clump's origin in UV1, so wind, gusts, trample and
    /// distance thinning still act per clump. Baking again overwrites the same assets in place.
    /// </summary>
    static class GrassPatchBaker
    {
        public static GameObject[] Bake(GrassProfile profile, float size)
        {
            var result = new List<GameObject>();
            if (profile == null || profile.Shader == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(profile))) return result.ToArray();
            string folder = TreeBaker.EnsurePath($"{TreeBaker.OutputFolder}/Grass/{profile.name}");
            var used = new HashSet<string>();
            int variants = profile.Variants.Length;
            for (int v = 0; v < variants; v++)
            {
                GrassVariant gv = profile.Variants[v];
                if (!gv.Enabled || gv.Density <= 0f) continue;
                string part = Sanitize(gv.Name);
                if (!used.Add(part)) part += v;
                GameObject prefab = BakeVariant(profile, v, size, folder, $"{profile.name}_{part}");
                if (prefab != null) result.Add(prefab);
            }
            AssetDatabase.SaveAssets();
            return result.ToArray();
        }

        private static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return string.IsNullOrWhiteSpace(s) ? "Grass" : s.Trim();
        }

        private static GameObject BakeVariant(GrassProfile profile, int v, float size, string folder, string name)
        {
            GrassVariant gv = profile.Variants[v];
            Mesh clump = GrassMeshes.Build(gv);
            Vector3[] cv = clump.vertices, cn = clump.normals;
            Vector2[] cu = clump.uv;
            int[] ct = clump.triangles;
            Object.DestroyImmediate(clump);

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector4>();
            var tris = new List<int>();
            var points = new List<Vector3>();
            var yaws = new List<float>();
            var rng = new System.Random(profile.Seed * 7919 + v * 104729);
            GrassScatter.Candidates(profile, v, new Vector2(size, size), 0f, Matrix4x4.identity, rng, 20000, points, yaws, true, applyMix: false);
            for (int i = 0; i < points.Count; i++)
            {
                Matrix4x4 m = GrassScatter.Clump(profile, gv, rng, points[i], Vector3.up, yaws[i], out float height, out _);
                Matrix4x4 normalMatrix = m.inverse.transpose;
                Vector3 origin = m.GetColumn(3);
                int start = verts.Count;
                for (int k = 0; k < cv.Length; k++)
                {
                    verts.Add(m.MultiplyPoint3x4(cv[k]));
                    normals.Add(normalMatrix.MultiplyVector(cn[k]).normalized);
                    uv0.Add(cu[k]);
                    uv1.Add(new Vector4(origin.x, origin.y, origin.z, height));
                }
                for (int k = 0; k < ct.Length; k++) tris.Add(start + ct[k]);
            }
            if (tris.Count == 0) return null;

            var fresh = new Mesh { name = name + "_Mesh", indexFormat = IndexFormat.UInt32 };
            fresh.SetVertices(verts);
            fresh.SetNormals(normals);
            fresh.SetUVs(0, uv0);
            fresh.SetUVs(1, uv1);
            fresh.SetTriangles(tris, 0);
            fresh.RecalculateBounds();
            Bounds b = fresh.bounds;
            b.Expand(new Vector3(1.2f, 0.4f, 1.2f)); // wind headroom
            fresh.bounds = b;
            Mesh mesh = SaveInPlace(fresh, $"{folder}/{name}_Mesh.asset");

            string matPath = $"{folder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(profile.Shader) { name = name };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = profile.Shader;
            GrassMaterials.Apply(mat, profile, gv, null, new Vector4(-size * 0.5f, -size * 0.5f, 1f / size, 1f / size), true);
            EditorUtility.SetDirty(mat);

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = profile.Features.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = profile.Features.ReceiveShadows;
            var batch = go.AddComponent<GrassBatch>();
            batch.Profile = profile;
            batch.Variant = v;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{folder}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // keeps the asset (and its GUID, so prefabs stay linked) when it already exists
        private static Mesh SaveInPlace(Mesh fresh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            EditorUtility.CopySerialized(fresh, existing);
            Object.DestroyImmediate(fresh);
            return existing;
        }
    }
}
