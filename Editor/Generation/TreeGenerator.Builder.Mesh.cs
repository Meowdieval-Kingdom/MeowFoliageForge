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
            private void EmitTube(Branch br, int sides)
            {
                TrunkSettings t = _r.Trunk;
                bool isTrunk = br.Level == 0;
                int lobes = br.Main ? t.RootLobes : 0;
                if (lobes > 0) sides = Mathf.Max(2, Mathf.CeilToInt(sides / (float)lobes)) * lobes; // every lobe gets a ring vertex on its ridge
                float twist = isTrunk ? t.Twist * Mathf.Deg2Rad : 0f;
                bool lumpy = lobes > 0 || t.Gnarl > 0f;
                float noiseSeed = NoiseSeed(br);
                int n = br.P.Count;
                int baseIndex = _pos.Count;
                Vector3 normalDir = Vector3.Cross(br.P[1] - br.P[0], Vector3.forward);
                if (normalDir.sqrMagnitude < 1e-4f) normalDir = Vector3.Cross(br.P[1] - br.P[0], Vector3.right);
                normalDir.Normalize();
                Vector3 prevTangent = (br.P[1] - br.P[0]).normalized;
                float v = 0f;
                const byte sway = 255; // one weight curve for all wood, so a branch base moves exactly like the trunk it grows from
                byte rnd = (byte)(br.Random * 255f);
                for (int i = 0; i < n; i++)
                {
                    Vector3 tangent = (br.P[Mathf.Min(i + 1, n - 1)] - br.P[Mathf.Max(i - 1, 0)]).normalized;
                    normalDir = Quaternion.FromToRotation(prevTangent, tangent) * normalDir;
                    prevTangent = tangent;
                    Vector3 binormal = Vector3.Cross(tangent, normalDir);
                    if (i > 0) v += (br.P[i] - br.P[i - 1]).magnitude;
                    float u = i / (float)(n - 1) * br.USpan;
                    for (int j = 0; j <= sides; j++)
                    {
                        float a = j / (float)sides * Mathf.PI * 2f;
                        float turned = a + twist * u;
                        Vector3 dir = Mathf.Cos(turned) * normalDir + Mathf.Sin(turned) * binormal;
                        float radius = br.R[i];
                        if (lumpy)
                        {
                            if (lobes > 0)
                            {
                                float ridge = 0.5f + 0.5f * Mathf.Cos(lobes * a);
                                radius *= 1f + t.RootLobeDepth * ridge * ridge * ridge * Mathf.Exp(-3f * u / t.RootLobeHeight);
                            }
                            // sampled on a circle around the tube, so the seam at j = 0 / sides matches
                            float noise = Mathf.PerlinNoise(noiseSeed + Mathf.Cos(a) * 1.5f, v * 1.2f + Mathf.Sin(a) * 1.5f);
                            radius *= 1f + (noise - 0.5f) * 2f * t.Gnarl;
                        }
                        Vector3 ringPoint = br.P[i] + dir * radius;
                        if (isTrunk && i == 0 && br.P[0].y < 1e-3f) ringPoint.y = br.P[0].y; // a leaning trunk tilts its base ring, so seat it flat on the ground
                        _pos.Add(ringPoint);
                        _nor.Add(dir);
                        _uv.Add(new Vector2(j / (float)sides, v));
                        _uv1.Add(Vector4.zero);
                        _col.Add(new Color32(0, sway, 0, rnd));
                    }
                }
                int stride = sides + 1;
                if (lumpy)
                {
                    // lobes and gnarl bend the surface away from the round normals, so take them from the neighbours
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < sides; j++)
                        {
                            int k = baseIndex + i * stride + j;
                            Vector3 around = _pos[baseIndex + i * stride + (j + 1) % sides] - _pos[baseIndex + i * stride + (j + sides - 1) % sides];
                            Vector3 along = _pos[baseIndex + Mathf.Min(i + 1, n - 1) * stride + j] - _pos[baseIndex + Mathf.Max(i - 1, 0) * stride + j];
                            Vector3 nrm = Vector3.Cross(along, around);
                            if (nrm.sqrMagnitude < 1e-12f) continue;
                            nrm.Normalize();
                            _nor[k] = Vector3.Dot(nrm, _nor[k]) < 0f ? -nrm : nrm;
                        }
                    for (int i = 0; i < n; i++) _nor[baseIndex + i * stride + sides] = _nor[baseIndex + i * stride];
                }
                for (int i = 0; i < n - 1; i++)
                {
                    for (int j = 0; j < sides; j++)
                    {
                        int a = baseIndex + i * stride + j;
                        int b = a + 1;
                        int c = a + stride;
                        int d = c + 1;
                        _wood.Add(a);
                        _wood.Add(c);
                        _wood.Add(b);
                        _wood.Add(b);
                        _wood.Add(c);
                        _wood.Add(d);
                    }
                }
                if (br.Broken)
                {
                    // snapped end: every other vertex of the last ring sticks out as a splinter, and a fan closes the tube
                    Vector3 tan = (br.P[n - 1] - br.P[n - 2]).normalized;
                    int ring = baseIndex + (n - 1) * stride;
                    float r = br.R[n - 1];
                    for (int j = 0; j <= sides; j++)
                        if (j % sides % 2 == 0) _pos[ring + j] += tan * (r * Mathf.Lerp(0.6f, 1.6f, H(j % sides, 11 + (int)(br.Random * 97f))));
                    int center = _pos.Count;
                    _pos.Add(br.P[n - 1] + tan * (r * 0.2f));
                    _nor.Add(tan);
                    _uv.Add(new Vector2(0.5f, v));
                    _uv1.Add(Vector4.zero);
                    _col.Add(new Color32(0, sway, 0, rnd));
                    for (int j = 0; j < sides; j++)
                    {
                        _wood.Add(center);
                        _wood.Add(ring + j);
                        _wood.Add(ring + j + 1);
                    }
                }
            }

            // Proxy = sum of gaussians at the card centres. Its negated gradient points away from the crowd, which is
            // the normal of a smooth shell around the foliage; sigma sets how tightly that shell hugs the clumps.
            // Sampled once per card; each corner gets the first-order gradient, d * (corner - centre), on top.
            private void Renormalize()
            {
                RenormalizeSettings s = _r.Renormalize;
                if (!s.Enabled || _cards.Count == 0 || _r.Leaves.Layout == LeafLayout.Blobs) return; // blob groups take their normals from the blob surface
                int cardCount = _cards.Count;
                var centers = new Vector3[cardCount];
                Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
                for (int k = 0; k < cardCount; k++)
                {
                    Vector4 u = _uv1[_cards[k].V0];
                    centers[k] = new Vector3(u.x, u.y, u.z);
                    min = Vector3.Min(min, centers[k]);
                    max = Vector3.Max(max, centers[k]);
                }
                float cluster = Mathf.Max(0.05f, _r.Leaves.ClusterRadius);
                float loose = Mathf.Max((max - min).magnitude * 0.3f, cluster * 1.5f);
                float sigma = Mathf.Lerp(loose, cluster * 0.9f, s.Tightness);
                float cutoff = 9f * sigma * sigma;
                float invCut = 1f / cutoff;

                // cards beyond 3 sigma add nothing, so bucket them in a grid of that size and only visit the 27 nearby cells
                float cell = Mathf.Max(Mathf.Sqrt(cutoff), 0.05f);
                var grid = new Dictionary<Vector3Int, List<int>>();
                Vector3Int CellOf(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell));
                for (int k = 0; k < cardCount; k++)
                {
                    Vector3Int key = CellOf(centers[k]);
                    if (!grid.TryGetValue(key, out List<int> bucket)) grid[key] = bucket = new List<int>();
                    bucket.Add(k);
                }

                float Density(Vector3 p, out Vector3 away)
                {
                    float d = 0f;
                    away = Vector3.zero;
                    Vector3Int c0 = CellOf(p);
                    for (int x = -1; x <= 1; x++)
                        for (int y = -1; y <= 1; y++)
                            for (int z = -1; z <= 1; z++)
                            {
                                if (!grid.TryGetValue(new Vector3Int(c0.x + x, c0.y + y, c0.z + z), out List<int> bucket)) continue;
                                foreach (int k in bucket)
                                {
                                    Vector3 v = p - centers[k];
                                    float m = v.sqrMagnitude;
                                    if (m > cutoff) continue;
                                    float q = 1f - m * invCut; // compact (1 - r²/R²)³ kernel, close to the gaussian and without Exp
                                    float w = q * q * q;
                                    d += w;
                                    away += v * w;
                                }
                            }
                    return d;
                }

                var density = new float[cardCount];
                var awayAt = new Vector3[cardCount];
                var inside = new float[cardCount];
                float maxInside = 1e-4f;
                for (int k = 0; k < cardCount; k++)
                {
                    density[k] = Density(centers[k], out awayAt[k]);
                    Vector3 n = awayAt[k].sqrMagnitude < 1e-10f ? Vector3.up : awayAt[k].normalized;
                    inside[k] = Density(centers[k] - n * sigma * 0.8f, out _);
                    maxInside = Mathf.Max(maxInside, inside[k]);
                }
                for (int k = 0; k < cardCount; k++)
                {
                    float ao = 1f - s.AOStrength * Mathf.Clamp01(inside[k] / maxInside);
                    byte aoByte = (byte)(128 + Mathf.RoundToInt(ao * 127f)); // 128..255 keeps the leaf flag (>0.5) and carries AO
                    Card card = _cards[k];
                    for (int v = card.V0; v < card.V0 + card.VCount; v++)
                    {
                        Vector3 away = awayAt[k] + (_pos[v] - centers[k]) * density[k];
                        Vector3 proxy = away.sqrMagnitude < 1e-10f ? _nor[v] : away.normalized;
                        _nor[v] = Vector3.Lerp(_nor[v], proxy, s.Strength * _r.Leaves.NormalBlend).normalized; // NormalBlend 0 keeps each card's own face
                        Color32 c = _col[v];
                        c.b = aoByte;
                        _col[v] = c;
                    }
                    FixWinding(card); // winding must still face the new normals
                }
            }

            // SpeedTree-style puffiness: every clump shades like a ball around its centre
            private void Puff()
            {
                LeafSettings l = _r.Leaves;
                float s = l.Puffiness * l.NormalBlend;
                if (_clumpCenters == null || s <= 0f || l.Layout == LeafLayout.Blobs) return;
                bool squashed = l.FlattenBottom > 0f || l.ClumpHeight != 1f;
                foreach (Card card in _cards)
                {
                    Vector3 o = _clumpCenters[card.Clump];
                    for (int v = card.V0; v < card.V0 + card.VCount; v++)
                    {
                        Vector3 d = _pos[v] - o;
                        if (squashed)
                        {
                            // normal of the squashed ellipsoid: divide by the vertical semi-axis squared
                            float h = l.ClumpHeight * (d.y < 0f ? Mathf.Max(1f - l.FlattenBottom, 0.15f) : 1f);
                            d.y /= h * h;
                        }
                        if (d.sqrMagnitude > 1e-8f) _nor[v] = Vector3.Lerp(_nor[v], d.normalized, s).normalized;
                    }
                    FixWinding(card);
                }
            }

            private TreeStats Finish(Mesh mesh, int cards, int dropped)
            {
                Renormalize();
                Puff();
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (Vector3 p in _pos)
                {
                    minY = Mathf.Min(minY, p.y);
                    maxY = Mathf.Max(maxY, p.y);
                }
                float span = Mathf.Max(0.001f, maxY - minY);
                for (int i = 0; i < _col.Count; i++)
                {
                    float h = Mathf.Clamp01((_pos[i].y - minY) / span);
                    Color32 c = _col[i];
                    c.r = (byte)Mathf.RoundToInt(h * 255f);
                    if (_r.Look.ContinuousColor && _blobHeight != null && i < _blobHeight.Length && _blobHeight[i] >= 0f) c.r = (byte)Mathf.RoundToInt(_blobHeight[i] * 255f);
                    c.g = (byte)Mathf.RoundToInt(Mathf.Pow(h, 1.6f) * c.g);
                    _col[i] = c;
                }

                mesh.Clear();
                mesh.name = _r.name;
                mesh.indexFormat = _pos.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(_pos);
                mesh.SetNormals(_nor);
                mesh.SetUVs(0, _uv);
                mesh.SetUVs(1, _uv1);
                bool billboard = _r.Leaves.Billboard != BillboardMode.Off && _cards.Count > 0;
                if (billboard)
                {
                    // card right/up axes for the shader's billboarding; wood keeps zeros, which the shader skips
                    var right = new Vector4[_pos.Count];
                    var up = new Vector4[_pos.Count];
                    foreach (Card card in _cards)
                        for (int v = card.V0; v < card.V0 + card.VCount; v++)
                        {
                            right[v] = card.Right;
                            up[v] = card.Up;
                        }
                    mesh.SetUVs(2, right);
                    mesh.SetUVs(3, up);
                }
                mesh.SetColors(_col);
                bool core = HasCore(_r);
                mesh.subMeshCount = 2 + (core ? 1 : 0);
                mesh.SetTriangles(_wood, 0);
                mesh.SetTriangles(_leaf, 1);
                if (core) mesh.SetTriangles(_core, 2);
                mesh.RecalculateBounds();
                mesh.Optimize();

                int indexBytes = mesh.indexFormat == IndexFormat.UInt16 ? 2 : 4;
                int bytes = _pos.Count * (12 + 12 + 8 + 16 + 4 + (billboard ? 32 : 0)) + (_wood.Count + _leaf.Count) * indexBytes;
                return new TreeStats(_pos.Count, (_wood.Count + _leaf.Count) / 3, cards, dropped, _branches.Count, bytes, mesh.bounds.size);
            }
        }
    }
}
