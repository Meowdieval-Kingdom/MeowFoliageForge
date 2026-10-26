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
            // k blobs over the branch tips (farthest-point seeds, three k-means passes); each wraps its tips plus the cluster radius
            // points on the leaf-carrying branches (outer half) and the trunk top, for blobs that must sit on wood
            public List<Vector3> BranchPoints(System.Random rng)
            {
                List<Branch> terminals = BuildSkeleton(out Branch trunk);
                var pts = new List<Vector3>();
                foreach (Branch br in terminals)
                    for (int k = 0; k < 3; k++) pts.Add(PointAt(br, Mathf.Lerp(0.5f, 1f, (float)rng.NextDouble()), out _, out _));
                foreach (Branch top in _stems.Count > 0 ? _stems : new List<Branch> { trunk }) pts.Add(top.P[top.P.Count - 1]);
                return pts;
            }

            // a small round blob centred on the tip of the trunk (or the stems)
            public CanopyBlob TrunkTopBlob()
            {
                BuildSkeleton(out Branch trunk);
                Branch top = _stems.Count > 0 ? _stems[0] : trunk;
                float r = Mathf.Max(0.3f, _r.Leaves.ClusterRadius * 0.5f);
                return new CanopyBlob { Position = top.P[top.P.Count - 1], Radii = new Vector3(r, r * 1.3f, r), FlatBelly = 0.2f };
            }

            public List<CanopyBlob> Suggest(int count)
            {
                List<Branch> terminals = BuildSkeleton(out Branch trunk);
                var tips = new List<Vector3>();
                foreach (Branch br in terminals) tips.Add(br.P[br.P.Count - 1]);
                if (_r.Leaves.CrownOnTrunkTop)
                    foreach (Branch top in _stems.Count > 0 ? _stems : new List<Branch> { trunk }) tips.Add(top.P[top.P.Count - 1]);
                var blobs = new List<CanopyBlob>();
                int k = Mathf.Min(count, tips.Count);
                if (k <= 0) return blobs;
                var seeds = new List<Vector3> { tips[0] };
                var dist = new float[tips.Count];
                for (int i = 0; i < tips.Count; i++) dist[i] = (tips[i] - seeds[0]).sqrMagnitude;
                while (seeds.Count < k)
                {
                    int far = 0;
                    for (int i = 1; i < dist.Length; i++) if (dist[i] > dist[far]) far = i;
                    seeds.Add(tips[far]);
                    for (int i = 0; i < dist.Length; i++) dist[i] = Mathf.Min(dist[i], (tips[i] - tips[far]).sqrMagnitude);
                }
                var of = new int[tips.Count];
                for (int pass = 0; pass < 3; pass++)
                {
                    var sum = new Vector3[k];
                    var n = new int[k];
                    for (int i = 0; i < tips.Count; i++)
                    {
                        int best = 0;
                        for (int c = 1; c < k; c++) if ((tips[i] - seeds[c]).sqrMagnitude < (tips[i] - seeds[best]).sqrMagnitude) best = c;
                        of[i] = best;
                        sum[best] += tips[i];
                        n[best]++;
                    }
                    for (int c = 0; c < k; c++) if (n[c] > 0) seeds[c] = sum[c] / n[c];
                }
                float pad = _r.Leaves.ClusterRadius;
                for (int c = 0; c < k; c++)
                {
                    // the blob is centred on the cluster's tip nearest the middle, so it sits on a branch
                    int onBranch = -1;
                    for (int i = 0; i < tips.Count; i++)
                        if (of[i] == c && (onBranch < 0 || (tips[i] - seeds[c]).sqrMagnitude < (tips[onBranch] - seeds[c]).sqrMagnitude)) onBranch = i;
                    if (onBranch >= 0) seeds[c] = tips[onBranch];
                    float rx = 0f, ry = 0f, reach = 0f;
                    int owned = 0;
                    Vector3 flow = Vector3.zero;
                    for (int i = 0; i < tips.Count; i++)
                    {
                        if (of[i] != c) continue;
                        if (i < terminals.Count)
                        {
                            Vector3 along = terminals[i].P[terminals[i].P.Count - 1] - terminals[i].P[0];
                            reach += along.magnitude;
                            owned++;
                            flow += along;
                        }
                        Vector3 d = tips[i] - seeds[c];
                        rx = Mathf.Max(rx, new Vector2(d.x, d.z).magnitude);
                        ry = Mathf.Max(ry, Mathf.Abs(d.y));
                    }
                    // the branches feeding a blob are as long as the foliage they carry is wide
                    float bp = pad + (owned > 0 ? 0.35f * reach / owned : 0f);
                    var radii = new Vector3(rx + bp, ry + bp * 0.8f, rx + bp);
                    var rotation = Vector3.zero;
                    if (_r.Leaves.BranchFollow > 0f && flow.sqrMagnitude > 1e-4f)
                    {
                        // stretch the blob along the branches that feed it: z = their direction, x/y = how far the tips spread across it
                        Quaternion q = Quaternion.LookRotation(flow.normalized);
                        Quaternion inv = Quaternion.Inverse(q);
                        float lz = 0f, lx = 0f, ly = 0f;
                        for (int i = 0; i < tips.Count; i++)
                        {
                            if (of[i] != c) continue;
                            Vector3 d = inv * (tips[i] - seeds[c]);
                            lz = Mathf.Max(lz, Mathf.Abs(d.z));
                            lx = Mathf.Max(lx, Mathf.Abs(d.x));
                            ly = Mathf.Max(ly, Mathf.Abs(d.y));
                        }
                        float f = _r.Leaves.BranchFollow;
                        float round = Mathf.Max(radii.x, radii.y);
                        var stretched = new Vector3(lx + bp * 0.8f, ly + bp * 0.7f, lz + bp * 1.1f);
                        radii = Vector3.Lerp(new Vector3(round, round * 0.8f, round), stretched, f);
                        rotation = q.eulerAngles;
                    }
                    blobs.Add(new CanopyBlob { Position = seeds[c], Radii = radii * _r.Leaves.BlobOverlap, Rotation = rotation, FlatBelly = _r.Leaves.BlobFlatBelly, Taper = _r.Leaves.BlobTaper });
                }

                // sizes: spread to about 3:1 with the highest blob largest, clamped to BlobSize, then grown until every owned tip lies well inside
                var mean = new float[k];
                var height = new float[k];
                for (int c = 0; c < k; c++)
                {
                    mean[c] = (blobs[c].Radii.x + blobs[c].Radii.y + blobs[c].Radii.z) / 3f;
                    height[c] = blobs[c].Position.y;
                }
                float[] scale = TreeGenerator.SizeScales(mean, height, _r.Leaves.BlobSizeSpread);
                Vector2 size = _r.Leaves.BlobSize;
                for (int c = 0; c < k; c++)
                {
                    CanopyBlob b = blobs[c];
                    Vector3 r = b.Radii * scale[c];
                    b.Radii = new Vector3(Mathf.Clamp(r.x, size.x, size.y), Mathf.Clamp(r.y, size.x, size.y), Mathf.Clamp(r.z, size.x, size.y));
                    var owned = new List<Vector3>();
                    for (int i = 0; i < tips.Count; i++) if (of[i] == c) owned.Add(tips[i]);
                    b.Radii *= Mathf.Min(TreeGenerator.TipGrowth(b, owned, 0.75f), 2f); // tips poke out of BlobSize-capped blobs rather than stay bare, hence past the cap
                    blobs[c] = b;
                }
                return blobs;
            }

            // Blobs layout: leaf groups sit on each blob's surface. A group is GroupQuads quads sharing one up axis (the surface normal, tilted toward
            // the sky by UpBias) fanned evenly around it. Directions are a Fibonacci sphere per blob; jitter and spin use hashes, sizes and rolls the RNG.
            float _cardGrow = 1f;

            private int CoreTriangles(List<CanopyBlob> blobs) => blobs != null && HasCore(_r) ? blobs.Count * 2 * _r.Look.CoreSegments * Mathf.Max(3, Mathf.RoundToInt(_r.Look.CoreSegments * 0.7f)) : 0;

            private static int Sum(int[] a)
            {
                int n = 0;
                foreach (int x in a) n += x;
                return n;
            }

            // groups per blob: CardsPerBlob when set, else enough that Coverage layers of leaf area blanket the blob's surface (and its volume share)
            private int[] BlobGroupCounts(List<CanopyBlob> blobs)
            {
                LeafSettings l = _r.Leaves;
                var counts = new int[blobs.Count];
                float size = (l.CardSize.x + l.CardSize.y) * 0.5f;
                float cardArea = Mathf.Max(0.0025f, size * size * 0.6f);
                for (int i = 0; i < counts.Length; i++)
                {
                    if (l.CardsPerBlob > 0)
                    {
                        counts[i] = l.CardsPerBlob;
                        continue;
                    }
                    Vector3 h = BlobCage.HalfExtents(blobs[i]);
                    const float p = 1.6075f; // Knud Thomsen's ellipsoid surface approximation
                    float area = 4f * Mathf.PI * Mathf.Pow((Mathf.Pow(h.x * h.y, p) + Mathf.Pow(h.x * h.z, p) + Mathf.Pow(h.y * h.z, p)) / 3f, 1f / p);
                    counts[i] = Mathf.Max(4, Mathf.CeilToInt(l.Coverage * area / cardArea * (1f + l.GroupVolume)));
                }
                return counts;
            }

            private void EmitBlobGroups(List<CanopyBlob> blobs, List<Branch> terminals, int[] counts, int ws, int ls, bool shaped, ref int cards)
            {
                LeafSettings l = _r.Leaves;
                LeafShapeSettings shape = _r.LeafShapes;
                List<Rect> cells = LeafBounds.Cells(_r.Look.LeafTexture, shape.Columns, shape.Rows);
                int quads = Mathf.Max(1, l.GroupQuads);
                List<Vector3> tipPoints = null;
                if (l.BranchTipBias > 0f)
                {
                    tipPoints = new List<Vector3>(terminals.Count);
                    foreach (Branch tb in terminals) tipPoints.Add(PointAt(tb, 1f, out _, out _));
                }
                for (int c = 0; c < blobs.Count; c++)
                {
                    CanopyBlob bl = blobs[c];
                    int perBlob = counts[c];
                    float spacing = Mathf.Sqrt(4f * Mathf.PI / perBlob); // angle between neighbouring groups, so jitter scales with the count
                    float spin = H(c, 41) * Mathf.PI * 2f;
                    for (int i = 0; i < perBlob; i++)
                    {
                        int id = c * 1009 + i;
                        float y = 1f - 2f * (i + 0.5f) / perBlob;
                        if (l.PlacementBias != 0f) y = Mathf.Pow((y + 1f) * 0.5f, Mathf.Pow(2f, -2f * l.PlacementBias)) * 2f - 1f; // + gathers groups on top, - underneath, 0 all around
                        float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)), phi = spin + i * 2.39996323f;
                        Vector3 dir = new Vector3(Mathf.Cos(phi) * ring, y, Mathf.Sin(phi) * ring)
                            + new Vector3(H(id, 51) - 0.5f, H(id, 52) - 0.5f, H(id, 53) - 0.5f) * (l.GroupJitter * spacing);
                        dir.Normalize();

                        Rect content = cells[cells.Count > 1 ? Mathf.Min((int)(F() * cells.Count), cells.Count - 1) : 0];
                        float contentAspect = content.height / Mathf.Max(content.width, 1e-3f);
                        float size = Mathf.Lerp(l.CardSize.x, l.CardSize.y, F()) * _cardGrow;
                        float w = contentAspect > 1f ? size / contentAspect : size;
                        float h = (contentAspect > 1f ? size : size * contentAspect) * l.Aspect;

                        Vector3 sn = TreeGenerator.BlobSurfaceNormal(bl, dir);
                        float inward = l.GroupVolume * H(id, 54) * H(id, 55); // most groups ride the surface, some sink into the volume
                        Vector3 spot = bl.Position + dir * (BlobCage.Extent(bl, dir) * (1f - inward) - l.GroupDepth * size + l.NormalOffset);
                        if (tipPoints != null)
                        {
                            Vector3 half = BlobCage.HalfExtents(bl);
                            spot = TreeGenerator.PullToTip(spot, bl.Position, Mathf.Max(half.x, Mathf.Max(half.y, half.z)) * 1.1f, tipPoints, l.BranchTipBias, i);
                        }
                        float tilt = Mathf.Clamp(l.GroupTilt + (H(id, 71) * 2f - 1f) * l.GroupTiltJitter, 0f, 180f);
                        Vector3 axis = TreeGenerator.GroupUp(sn, tilt, H(id, 70) * Mathf.PI * 2f, l.UpBias);
                        spot += axis * ((0.5f - l.Anchor.y) * h); // the leaf's stem, not its middle, sits on the anchor

                        Vector3 right = Vector3.Cross(axis, Vector3.up);
                        right = right.sqrMagnitude < 1e-4f ? Vector3.right : right.normalized;
                        right = Quaternion.AngleAxis(l.Roll + (F() * 2f - 1f) * l.RollJitter, axis) * right;
                        Vector3 face = Vector3.Cross(right, axis);

                        byte rnd = (byte)((l.TintPerClump ? Mathf.Repeat(c * 0.618034f + 0.31f, 1f) : F()) * 255f);
                        float phase = F();
                        Vector3 bend = Vector3.zero;
                        Vector2 anchor = new Vector2((l.Anchor.x - 0.5f) * w, (l.Anchor.y - 0.5f) * h);
                        if (shaped)
                        {
                            float j = shape.Jitter;
                            bend = new Vector3(shape.Fold, shape.Curl, shape.Twist);
                            if (j > 0f) bend = Vector3.Scale(bend, new Vector3(1f + (F() * 2f - 1f) * j, 1f + (F() * 2f - 1f) * j, 1f + (F() * 2f - 1f) * j));
                        }
                        for (int k = 0; k < quads; k++)
                        {
                            GroupAxes(right, face, k, quads, out Vector3 gr, out Vector3 gn);
                            if (Vector3.Dot(gn, dir + Vector3.up) < 0f) gn = -gn; // own normal faces outward/up
                            int first = _pos.Count;
                            if (shaped) EmitShapedCard(spot, gr, axis, gn, w, h, anchor, bend, ws, ls, bl.Position, Vector3.one, 0f, phase, rnd, content, c);
                            else EmitCard(spot, gr * (w * 0.5f), axis * (h * 0.5f), gn, bl.Position, Vector3.one, 0f, phase, rnd, content, c);
                            Vector3 lift = gn * ((H(id, 56 + k) - 0.5f) * size * 0.12f); // keeps overlapping cards off each other's plane (z-fighting)
                            for (int v = first; v < _pos.Count; v++)
                            {
                                _pos[v] += lift;
                                _nor[v] = Vector3.Slerp(_nor[v], sn, l.Smoothness).normalized;
                            }
                            cards++;
                        }
                    }
                }
            }
        }
    }
}
