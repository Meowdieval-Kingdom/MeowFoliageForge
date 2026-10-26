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
            // Card as a ws x ls grid, deformed around its anchor (the stem): fold across the middle, twist around the
            // length, then curl along it. bend = (fold, curl, twist) in degrees.
            private void EmitShapedCard(Vector3 c, Vector3 right, Vector3 up, Vector3 n, float w, float h, Vector2 anchor, Vector3 bend, int ws, int ls,
                Vector3 crown, Vector3 radii, float blend, float phase, byte rnd, Rect content, int clump)
            {
                int baseIndex = _pos.Count, indexStart = _leaf.Count, cols = ws + 1;
                float fold = bend.x * Mathf.Deg2Rad, curl = bend.y * Mathf.Deg2Rad, twist = bend.z * Mathf.Deg2Rad;
                Vector3 root = c + right * anchor.x + up * anchor.y;
                for (int iy = 0; iy <= ls; iy++)
                    for (int ix = 0; ix <= ws; ix++)
                    {
                        float s = ix / (float)ws, t = iy / (float)ls;
                        float dx = (s - 0.5f) * w - anchor.x, dy = (t - 0.5f) * h - anchor.y;
                        float across = dx * Mathf.Cos(fold), outward = Mathf.Abs(dx) * Mathf.Sin(fold);
                        float tw = twist * dy / h;
                        float a2 = across * Mathf.Cos(tw) - outward * Mathf.Sin(tw);
                        outward = across * Mathf.Sin(tw) + outward * Mathf.Cos(tw);
                        across = a2;
                        float along = dy;
                        if (Mathf.Abs(curl) > 1e-4f)
                        {
                            float rho = h / curl, theta = curl * dy / h;
                            along = (rho - outward) * Mathf.Sin(theta);
                            outward = rho - (rho - outward) * Mathf.Cos(theta);
                        }
                        _pos.Add(root + right * across + up * along + n * outward);
                        _uv.Add(new Vector2(Mathf.Lerp(content.xMin, content.xMax, s), Mathf.Lerp(content.yMin, content.yMax, t)));
                        _uv1.Add(new Vector4(c.x, c.y, c.z, phase));
                        _col.Add(new Color32(0, 255, 255, rnd));
                        _nor.Add(n);
                    }
                FinishGrid(baseIndex, indexStart, ws, ls, crown, radii, blend, clump, right, up);
            }

            // Normals (from the grid's own slope, oriented like the normals already written, then blended toward the
            // crown ellipsoid), triangles and the card record for a (ws + 1) x (ls + 1) vertex grid.
            private void FinishGrid(int baseIndex, int indexStart, int ws, int ls, Vector3 crown, Vector3 radii, float blend, int clump, Vector3 right, Vector3 up)
            {
                int cols = ws + 1;
                for (int iy = 0; iy <= ls; iy++)
                    for (int ix = 0; ix <= ws; ix++)
                    {
                        int k = baseIndex + iy * cols + ix;
                        Vector3 n = _nor[k];
                        Vector3 ds = _pos[baseIndex + iy * cols + Mathf.Min(ix + 1, ws)] - _pos[baseIndex + iy * cols + Mathf.Max(ix - 1, 0)];
                        Vector3 dt = _pos[baseIndex + Mathf.Min(iy + 1, ls) * cols + ix] - _pos[baseIndex + Mathf.Max(iy - 1, 0) * cols + ix];
                        Vector3 g = Vector3.Cross(dt, ds);
                        Vector3 own = g.sqrMagnitude < 1e-12f ? n : (Vector3.Dot(g, n) < 0f ? -g : g).normalized;
                        Vector3 d = _pos[k] - crown;
                        var e = new Vector3(d.x / radii.x, d.y / radii.y, d.z / radii.z);
                        _nor[k] = Vector3.Slerp(own, e.sqrMagnitude < 1e-4f ? own : e.normalized, blend).normalized;
                    }
                for (int iy = 0; iy < ls; iy++)
                    for (int ix = 0; ix < ws; ix++)
                    {
                        int a = baseIndex + iy * cols + ix, b = a + 1, d = a + cols + 1, e = a + cols;
                        _leaf.Add(a);
                        _leaf.Add(b);
                        _leaf.Add(d);
                        _leaf.Add(a);
                        _leaf.Add(d);
                        _leaf.Add(e);
                    }
                var card = new Card { V0 = baseIndex, VCount = _pos.Count - baseIndex, I0 = indexStart, ICount = _leaf.Count - indexStart, Clump = clump, Right = right, Up = up };
                _cards.Add(card);
                FixWinding(card);
            }

            // turns the card's triangles to face the side its normals point to
            private void FixWinding(Card card)
            {
                Vector3 avg = Vector3.zero, geo = Vector3.zero;
                for (int v = card.V0; v < card.V0 + card.VCount; v++) avg += _nor[v];
                for (int t = card.I0; t < card.I0 + card.ICount; t += 3)
                    geo += Vector3.Cross(_pos[_leaf[t + 1]] - _pos[_leaf[t]], _pos[_leaf[t + 2]] - _pos[_leaf[t]]);
                if (Vector3.Dot(geo, avg) >= 0f) return;
                for (int t = card.I0; t < card.I0 + card.ICount; t += 3) (_leaf[t + 1], _leaf[t + 2]) = (_leaf[t + 2], _leaf[t + 1]);
            }

            private void EmitCard(Vector3 c, Vector3 halfRight, Vector3 halfUp, Vector3 n, Vector3 crown, Vector3 radii, float blend, float phase, byte rnd, Rect content, int clump)
            {
                int baseIndex = _pos.Count;
                _cards.Add(new Card { V0 = baseIndex, VCount = 4, I0 = _leaf.Count, ICount = 6, Clump = clump, Right = halfRight.normalized, Up = halfUp.normalized });
                Vector3[] corners =
                {
                    c - halfRight - halfUp, c + halfRight - halfUp, c + halfRight + halfUp, c - halfRight + halfUp,
                };
                Vector2[] uvs = { new Vector2(content.xMin, content.yMin), new Vector2(content.xMax, content.yMin), new Vector2(content.xMax, content.yMax), new Vector2(content.xMin, content.yMax) };
                Vector3 avg = Vector3.zero;
                var normals = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    Vector3 d = corners[i] - crown;
                    var e = new Vector3(d.x / radii.x, d.y / radii.y, d.z / radii.z);
                    Vector3 smooth = e.sqrMagnitude < 1e-4f ? n : e.normalized;
                    normals[i] = Vector3.Slerp(n, smooth, blend).normalized;
                    avg += normals[i];
                }
                for (int i = 0; i < 4; i++)
                {
                    _pos.Add(corners[i]);
                    _nor.Add(normals[i]);
                    _uv.Add(uvs[i]);
                    _uv1.Add(new Vector4(c.x, c.y, c.z, phase));
                    _col.Add(new Color32(0, 255, 255, rnd));
                }
                Vector3 geo = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]);
                bool flip = Vector3.Dot(geo, avg) < 0f;
                int[] order = flip ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
                foreach (int o in order) _leaf.Add(baseIndex + o);
            }
        }
    }
}
