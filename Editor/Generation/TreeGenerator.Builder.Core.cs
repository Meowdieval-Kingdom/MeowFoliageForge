using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meowdieval.FoliageForge
{
    public static partial class TreeGenerator
    {
        sealed partial class Builder
        {
            readonly TreeRecipe _r;
            readonly System.Random _rng;
            readonly List<Vector3> _pos = new List<Vector3>();
            readonly List<Vector3> _nor = new List<Vector3>();
            readonly List<Vector2> _uv = new List<Vector2>();
            readonly List<Vector4> _uv1 = new List<Vector4>();
            readonly List<Color32> _col = new List<Color32>();
            readonly List<int> _wood = new List<int>();
            readonly List<int> _leaf = new List<int>();
            readonly List<Branch> _branches = new List<Branch>();
            readonly List<Card> _cards = new List<Card>();
            readonly List<Branch> _stems = new List<Branch>(); // empty unless the trunk splits
            List<Vector3> _clumpCenters;
            Vector3 _leanDir;
            float _splitU;

            public Builder(TreeRecipe recipe)
            {
                _r = recipe;
                _rng = new System.Random(recipe.Seed);
            }

            private float F() => (float)_rng.NextDouble();

            private Vector3 Unit()
            {
                for (int i = 0; i < 16; i++)
                {
                    var v = new Vector3(F() * 2f - 1f, F() * 2f - 1f, F() * 2f - 1f);
                    float m = v.sqrMagnitude;
                    if (m > 0.01f && m <= 1f) return v / Mathf.Sqrt(m);
                }
                return Vector3.up;
            }

            private Vector3 InsideSphere() => Unit() * Mathf.Pow(F(), 0.4f);

            // deterministic 0..1 hash; newer features use it instead of the RNG, so switching them leaves the rest of the tree as it was
            private float H(int i, int salt)
            {
                float x = Mathf.Sin(i * 12.9898f + salt * 78.233f + (_r.Seed % 10007) * 0.1373f) * 43758.5453f;
                return x - Mathf.Floor(x);
            }

            private float NoiseSeed(Branch br) => Mathf.Repeat(_r.Seed * 0.618034f, 1f) * 100f + br.Random * 50f; // no RNG draw, so seeds keep their shape

            public TreeStats Build(Mesh mesh)
            {
                TrunkSettings t = _r.Trunk;

                List<Branch> terminals = BuildSkeleton(out Branch trunk);

                foreach (Branch br in _branches) EmitTube(br, br.Level == 0 ? t.RadialSides : Mathf.Max(3, _r.Branches.MinSides, t.RadialSides - 2 * br.Level));

                int cards = 0, dropped = 0;
                EmitLeaves(trunk, terminals, ref cards, ref dropped);
                return Finish(mesh, cards, dropped);
            }
        }
    }
}
