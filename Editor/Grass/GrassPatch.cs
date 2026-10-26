using System;
using System.Collections.Generic;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>A small flat patch of a grass profile for the window preview: same scatter and materials as a Grass Field, drawn clump by clump.</summary>
    sealed class GrassPatch : IDisposable
    {
        public List<Matrix4x4>[] Clumps = new List<Matrix4x4>[0];
        public Mesh[] Meshes = new Mesh[0];
        public Material[] Materials = new Material[0];
        public int ClumpCount, Triangles;
        public float Size = 8f;

        public void Build(GrassProfile profile)
        {
            Clear();
            if (profile == null || profile.Shader == null) return;
            int variants = profile.Variants.Length;
            if (Meshes.Length != variants)
            {
                Clumps = new List<Matrix4x4>[variants];
                Meshes = new Mesh[variants];
                Materials = new Material[variants];
            }
            var points = new List<Vector3>();
            var yaws = new List<float>();
            int budget = 6000;
            for (int v = 0; v < variants && budget > 0; v++)
            {
                GrassVariant gv = profile.Variants[v];
                if (gv.MixedDensity <= 0f) continue;
                Meshes[v] = GrassMeshes.Build(gv);
                Materials[v] = new Material(profile.Shader) { hideFlags = HideFlags.HideAndDontSave };
                GrassMaterials.Apply(Materials[v], profile, gv, null, new Vector4(-Size * 0.5f, -Size * 0.5f, 1f / Size, 1f / Size));
                var rng = new System.Random(profile.Seed * 7919 + v * 104729);
                GrassScatter.Candidates(profile, v, new Vector2(Size, Size), 0f, Matrix4x4.identity, rng, budget, points, yaws, true);
                var list = Clumps[v] = new List<Matrix4x4>(points.Count);
                for (int i = 0; i < points.Count && budget > 0; i++, budget--)
                    list.Add(GrassScatter.Clump(profile, gv, rng, points[i], Vector3.up, yaws[i], out _, out _));
                ClumpCount += list.Count;
                Triangles += list.Count * Meshes[v].triangles.Length / 3;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < Meshes.Length; i++)
            {
                if (Meshes[i] != null) UnityEngine.Object.DestroyImmediate(Meshes[i]);
                if (Materials[i] != null) UnityEngine.Object.DestroyImmediate(Materials[i]);
                Meshes[i] = null;
                Materials[i] = null;
                Clumps[i] = null;
            }
            ClumpCount = Triangles = 0;
        }

        public void Dispose() => Clear();
    }
}
