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
            private void EmitLeaves(Branch trunk, List<Branch> terminals, ref int cards, ref int dropped)
            {
                LeafSettings l = _r.Leaves;
                var tops = new List<Branch>();
                if (l.CrownOnTrunkTop)
                {
                    if (_stems.Count > 0) tops.AddRange(_stems);
                    else tops.Add(trunk);
                }
                if (l.Layout == LeafLayout.Fronds)
                {
                    EmitFronds(terminals, tops, ref cards, ref dropped);
                    return;
                }
                bool hang = l.Layout == LeafLayout.Hanging;
                bool blobMode = l.Layout == LeafLayout.Blobs;
                List<CanopyBlob> blobs = null;
                if (blobMode) blobs = _r.Blobs.Count > 0 ? _r.Blobs : Suggest(l.BlobCount);
                int clusters = blobMode ? blobs.Count : terminals.Count + tops.Count;
                int perCluster = blobMode ? l.CardsPerBlob : l.CardsPerBranch;
                if (clusters == 0 || (perCluster <= 0 && !blobMode)) return;
                int[] blobCounts = blobMode ? BlobGroupCounts(blobs) : null;

                int wanted = blobMode ? Sum(blobCounts) : perCluster * clusters;
                LeafShapeSettings shape = _r.LeafShapes;
                bool shaped = IsShaped(shape, out int ws, out int ls);
                int perCard = blobMode ? Mathf.Max(1, l.GroupQuads) : l.CardsPerCluster > 0 ? l.CardsPerCluster : l.Intersecting ? 2 : 1; // a crossed pair counts as two cards
                int cap = Mathf.Max(8, _r.Budget.MaxCards / perCard);
                if (_r.Budget.FitCardsToTriangles)
                    cap = Mathf.Clamp((_r.Budget.MaxTriangles - _wood.Count / 3 - CoreTriangles(blobs)) / (2 * ws * ls * perCard), 0, cap);
                if (cap <= 0)
                {
                    dropped = wanted;
                    return;
                }
                if (blobMode)
                {
                    if (wanted > cap)
                        for (int c = 0; c < blobCounts.Length; c++) blobCounts[c] = Mathf.Max(1, Mathf.FloorToInt(blobCounts[c] * (float)cap / wanted));
                    // the one-group-per-blob floor above can overshoot the cap on small budgets: take the surplus from the fullest blobs
                    for (int over = Sum(blobCounts) - cap; over > 0; over--)
                    {
                        int fullest = 0;
                        for (int c = 1; c < blobCounts.Length; c++) if (blobCounts[c] > blobCounts[fullest]) fullest = c;
                        blobCounts[fullest]--;
                    }
                    int made = Sum(blobCounts);
                    dropped = Mathf.Max(0, wanted - made);
                    _cardGrow = Mathf.Min(Mathf.Sqrt(wanted / (float)Mathf.Max(1, made)), 2.5f); // fewer groups get larger cards, so the blob stays covered
                    EmitBlobGroups(blobs, terminals, blobCounts, ws, ls, shaped, ref cards);
                    BlobShading(blobs);
                    return;
                }
                if (wanted > cap) perCluster = Mathf.Max(1, cap / clusters);
                int total = Mathf.Min(perCluster * clusters, cap);
                dropped = Mathf.Max(0, wanted - total);
                // hanging strands: whole strands per branch; fewer cards keep the length and just space out
                float spacing = Mathf.Max(0.05f, (l.CardSize.x + l.CardSize.y) * 0.25f);
                int strandCards = hang ? Mathf.Clamp(Mathf.CeilToInt(l.StrandLength / spacing), 1, perCluster) : 1;
                float step = hang ? l.StrandLength / strandCards : 0f;

                var centers = new List<Vector3>(total);
                var branchRandom = new List<float>(total);
                var tangents = new List<Vector3>(total);
                var clusterOf = new List<int>(total);
                for (int c = 0; c < clusters && centers.Count < total; c++)
                {
                    bool top = c >= terminals.Count;
                    Branch br = top ? tops[c - terminals.Count] : terminals[c];
                    Vector3 root = Vector3.zero, drift = Vector3.zero;
                    for (int i = 0; i < perCluster && centers.Count < total; i++)
                    {
                        if (hang)
                        {
                            int k = i % strandCards;
                            if (k == 0)
                            {
                                root = PointAt(br, top ? 1f : Mathf.Lerp(l.TipBias, 1f, F()), out _, out _);
                                Vector2 h = UnitHorizontal();
                                drift = new Vector3(h.x, 0f, h.y) * 0.15f;
                            }
                            centers.Add(root + Vector3.down * (k * step) + drift * (k * step));
                            branchRandom.Add(br.Random);
                            tangents.Add(Vector3.down);
                            clusterOf.Add(c);
                            continue;
                        }
                        float f = top ? 1f : Mathf.Lerp(l.TipBias, 1f, F());
                        if (l.TipClumps) f = 1f;
                        Vector3 p = PointAt(br, f, out _, out Vector3 tan);
                        // attached leaves sit on the branch, not around it; a tip clump always surrounds its tip
                        Vector3 o = InsideSphere() * l.ClusterRadius * (l.TipClumps ? 1f : 1f - l.Attach);
                        o.y *= 0.75f;
                        centers.Add(p + o);
                        branchRandom.Add(br.Random);
                        tangents.Add(top ? Vector3.up : tan);
                        clusterOf.Add(c);
                    }
                }

                ShapeCrown(centers);
                int[] clumpOf = l.TipClumps ? TipClumps(centers, clusterOf, branchRandom) : GatherClumps(centers, branchRandom);
                ShapeClumps(centers, clumpOf);
                List<Rect> cells = LeafBounds.Cells(_r.Look.LeafTexture, shape.Columns, shape.Rows);

                Vector3 min = centers[0], max = centers[0];
                foreach (Vector3 c in centers)
                {
                    min = Vector3.Min(min, c);
                    max = Vector3.Max(max, c);
                }
                Vector3 crown = (min + max) * 0.5f;
                Vector3 radii = Vector3.Max((max - min) * 0.5f + Vector3.one * l.ClusterRadius, Vector3.one * 0.1f);

                for (int i = 0; i < centers.Count; i++)
                {
                    Vector3 c = centers[i];
                    Vector3 outward = c - (l.TipClumps ? _clumpCenters[clumpOf[i]] : crown); // tip clump cards lie on their own puff
                    outward = outward.sqrMagnitude < 1e-4f ? Vector3.up : outward.normalized;
                    Vector3 n = (Vector3.Lerp(Unit(), outward, 0.6f) + Vector3.up * l.UpBias).normalized;
                    if (hang)
                    {
                        // cards face out sideways so the strand reads as a hanging curtain; Roll then turns each leaf
                        n = Vector3.Lerp(n, new Vector3(outward.x, 0f, outward.z), 0.7f);
                        n.y *= 0.3f;
                        n = n.sqrMagnitude < 1e-4f ? Vector3.forward : n.normalized;
                    }
                    Vector3 right = Vector3.Cross(n, Vector3.up);
                    right = right.sqrMagnitude < 1e-4f ? Vector3.right : right.normalized;
                    Vector3 up = Vector3.Cross(right, n);
                    if (l.Attach > 0f && !hang)
                    {
                        // the leaf grows from the branch: its axis follows the branch tip and fans out sideways
                        Vector3 side = Unit();
                        side = Vector3.ProjectOnPlane(side, tangents[i]);
                        side = side.sqrMagnitude < 1e-4f ? Vector3.Cross(tangents[i], Vector3.right) : side.normalized;
                        float along = 0.7f, fan = 0.7f;
                        Vector3 axis = (tangents[i] * along + side * fan + Vector3.up * l.UpBias * 0.5f).normalized;
                        Vector3 flat = Vector3.ProjectOnPlane(outward + Vector3.up * 0.2f, axis);
                        Vector3 nA = flat.sqrMagnitude < 1e-4f ? n : flat.normalized;
                        Vector3 rightA = Vector3.Cross(nA, axis).normalized;
                        n = Vector3.Slerp(n, nA, l.Attach).normalized;
                        right = Vector3.Slerp(right, rightA, l.Attach);
                        right = Vector3.ProjectOnPlane(right, n).normalized;
                        up = Vector3.Cross(right, n);
                    }
                    Quaternion roll = Quaternion.AngleAxis(l.Roll + (F() * 2f - 1f) * l.RollJitter, n);
                    right = roll * right;
                    up = roll * up;
                    if (l.FlattenBottom > 0f && clumpOf != null && c.y < _clumpCenters[clumpOf[i]].y)
                    {
                        // cards under the clump centre turn face-down, so the underside reads as a flat shelf
                        n = Vector3.Slerp(n, Vector3.down, l.FlattenBottom).normalized;
                        right = Vector3.ProjectOnPlane(right, n);
                        right = right.sqrMagnitude < 1e-4f ? Vector3.right : right.normalized;
                        up = Vector3.Cross(right, n);
                    }
                    Rect content = cells[cells.Count > 1 ? Mathf.Min((int)(F() * cells.Count), cells.Count - 1) : 0];
                    float contentAspect = content.height / Mathf.Max(content.width, 1e-3f); // height per unit width of the opaque part
                    // size is the longer side of the opaque part, so a thin blade and a round leaf read at the same scale
                    float size = Mathf.Lerp(l.CardSize.x, l.CardSize.y, F());
                    float w = contentAspect > 1f ? size / contentAspect : size;
                    float h = (contentAspect > 1f ? size : size * contentAspect) * l.Aspect;
                    // move the card so the leaf's anchor (the stem) sits on the branch point instead of its middle
                    c += (right * ((0.5f - l.Anchor.x) * w) + up * ((0.5f - l.Anchor.y) * h)) * (hang ? 1f : l.Attach);
                    // push each card a hair along its normal, on a golden-ratio sequence (no RNG draw, so shapes stay the same), so
                    // overlapping near-parallel cards never share a depth and flicker against each other
                    c += n * ((Mathf.Repeat(i * 0.618034f, 1f) - 0.5f) * 0.04f * size);
                    byte rnd = (byte)(branchRandom[i] * 255f);
                    int clump = clumpOf != null ? clumpOf[i] : 0;
                    if (shaped)
                    {
                        float j = shape.Jitter;
                        Vector3 bend = new Vector3(shape.Fold, shape.Curl, shape.Twist);
                        if (j > 0f) bend = Vector3.Scale(bend, new Vector3(1f + (F() * 2f - 1f) * j, 1f + (F() * 2f - 1f) * j, 1f + (F() * 2f - 1f) * j));
                        Vector2 anchor = new Vector2((l.Anchor.x - 0.5f) * w, (l.Anchor.y - 0.5f) * h);
                        if (l.CardsPerCluster > 0)
                        {
                            for (int k = 0; k < l.CardsPerCluster; k++)
                            {
                                GroupAxes(right, n, k, l.CardsPerCluster, out Vector3 gr, out Vector3 gn);
                                EmitShapedCard(c, gr, up, gn, w, h, anchor, bend, ws, ls, crown, radii, l.NormalBlend, F(), rnd, content, clump);
                                cards++;
                            }
                            continue;
                        }
                        EmitShapedCard(c, right, up, n, w, h, anchor, bend, ws, ls, crown, radii, l.NormalBlend, F(), rnd, content, clump);
                        cards++;
                        if (l.Intersecting)
                        {
                            EmitShapedCard(c, n, up, right, w, h, anchor, bend, ws, ls, crown, radii, l.NormalBlend, F(), rnd, content, clump);
                            cards++;
                        }
                        continue;
                    }
                    if (l.CardsPerCluster > 0)
                    {
                        for (int k = 0; k < l.CardsPerCluster; k++)
                        {
                            GroupAxes(right, n, k, l.CardsPerCluster, out Vector3 gr, out Vector3 gn);
                            EmitCard(c, gr * (w * 0.5f), up * (h * 0.5f), gn, crown, radii, l.NormalBlend, F(), rnd, content, clump);
                            cards++;
                        }
                        continue;
                    }
                    EmitCard(c, right * (w * 0.5f), up * (h * 0.5f), n, crown, radii, l.NormalBlend, F(), rnd, content, clump);
                    cards++;
                    if (l.Intersecting)
                    {
                        // the crossing card turns 90 degrees about the up axis: its width runs along the old normal
                        EmitCard(c, n * (w * 0.5f), up * (h * 0.5f), right, crown, radii, l.NormalBlend, F(), rnd, content, clump);
                        cards++;
                    }
                }
            }

            /// <summary>True when cards need more than one quad; <paramref name="ws"/> x <paramref name="ls"/> is the grid each card uses.</summary>
            private static bool IsShaped(LeafShapeSettings s, out int ws, out int ls)
            {
                ws = s.Fold != 0f ? Mathf.Max(2, s.WidthSegments) : Mathf.Max(1, s.WidthSegments);
                ls = s.Curl != 0f || s.Twist != 0f ? Mathf.Max(3, s.LengthSegments) : Mathf.Max(1, s.LengthSegments);
                return ws > 1 || ls > 1;
            }

            // Sapling-style pruning: shortens level 1 branches whose tips stick out of the crown envelope, so the
            // leaves pulled onto the silhouette do not leave bare wood poking out of it. Twigs grow afterwards.
            private void PruneToEnvelope(Branch trunk, List<Branch> level1)
            {
                LeafSettings l = _r.Leaves;
                if (l.Envelope == CrownShape.None || l.EnvelopeStrength <= 0f || level1.Count == 0) return;
                Vector3 min = trunk.P[trunk.P.Count - 1], max = min;
                foreach (Branch stem in _stems)
                {
                    Vector3 top = stem.P[stem.P.Count - 1];
                    min = Vector3.Min(min, top);
                    max = Vector3.Max(max, top);
                }
                foreach (Branch br in level1)
                {
                    Vector3 tip = br.P[br.P.Count - 1];
                    min = Vector3.Min(min, tip);
                    max = Vector3.Max(max, tip);
                }
                Vector3 mid = (min + max) * 0.5f;
                float radius = Mathf.Max(((max.x - min.x) + (max.z - min.z)) * 0.25f, 0.05f);
                float span = Mathf.Max(max.y - min.y, 0.05f);
                foreach (Branch br in level1)
                {
                    Vector3 tip = br.P[br.P.Count - 1];
                    float d = new Vector2(tip.x - mid.x, tip.z - mid.z).magnitude;
                    float allowed = CrownProfile(l.Envelope, (tip.y - min.y) / span) * radius;
                    if (d <= allowed) continue;
                    float k = Mathf.Lerp(1f, Mathf.Max(allowed / d, 0.25f), l.EnvelopeStrength);
                    Vector3 o = br.P[0];
                    for (int i = 1; i < br.P.Count; i++) br.P[i] = o + (br.P[i] - o) * k;
                    br.Length *= k;
                }
            }

            // Pulls leaf positions horizontally onto a silhouette sized to the crown, so the tree reads as one clean shape.
            private void ShapeCrown(List<Vector3> centers)
            {
                LeafSettings l = _r.Leaves;
                if (l.Envelope == CrownShape.None || l.EnvelopeStrength <= 0f || centers.Count == 0) return;
                Vector3 min = centers[0], max = centers[0];
                foreach (Vector3 c in centers)
                {
                    min = Vector3.Min(min, c);
                    max = Vector3.Max(max, c);
                }
                Vector3 mid = (min + max) * 0.5f;
                float radius = Mathf.Max(((max.x - min.x) + (max.z - min.z)) * 0.25f, 0.05f); // round, even if the branches are lopsided
                float span = Mathf.Max(max.y - min.y, 0.05f);
                for (int i = 0; i < centers.Count; i++)
                {
                    Vector3 p = centers[i];
                    var flat = new Vector2(p.x - mid.x, p.z - mid.z);
                    float d = flat.magnitude;
                    float golden = i * 2.39996f; // leaves on the axis get spread out on a golden-angle spiral
                    Vector2 dir = d < 1e-4f ? new Vector2(Mathf.Cos(golden), Mathf.Sin(golden)) : flat / d;
                    float target = CrownProfile(l.Envelope, (p.y - min.y) / span) * radius;
                    Vector2 moved = dir * Mathf.Lerp(d, target, l.EnvelopeStrength);
                    centers[i] = new Vector3(mid.x + moved.x, p.y, mid.z + moved.y);
                }
            }

            /// <summary>Envelope radius 0..1 at crown height <paramref name="t"/> (0 bottom, 1 top).</summary>
            private static float CrownProfile(CrownShape shape, float t)
            {
                t = Mathf.Clamp01(t);
                float r;
                switch (shape)
                {
                    case CrownShape.Sphere:
                        r = Mathf.Sqrt(1f - (2f * t - 1f) * (2f * t - 1f));
                        break;
                    case CrownShape.Dome:
                        r = Mathf.Sqrt(1f - t * t);
                        break;
                    case CrownShape.Cone:
                        r = 1f - t;
                        break;
                    case CrownShape.Column:
                        r = Mathf.Sqrt(1f - Mathf.Pow(Mathf.Abs(2f * t - 1f), 4f));
                        break;
                    case CrownShape.Umbrella:
                        r = Mathf.Sqrt(1f - (1f - t) * (1f - t));
                        break;
                    default:
                        r = 1f;
                        break;
                }
                return Mathf.Max(r, 0.05f);
            }

            // Splits the leaves into evenly spread clumps (farthest-point seeds, then two k-means passes, no RNG draws)
            // and pulls each leaf toward its clump's centre. Returns the clump of each leaf, or null when clumps are off.
            private int[] GatherClumps(List<Vector3> centers, List<float> branchRandom)
            {
                LeafSettings l = _r.Leaves;
                int k = Mathf.Min(l.Clumps, centers.Count);
                if (k <= 0) return null;
                var seeds = new List<Vector3> { centers[0] };
                var dist = new float[centers.Count];
                for (int i = 0; i < centers.Count; i++) dist[i] = (centers[i] - seeds[0]).sqrMagnitude;
                while (seeds.Count < k)
                {
                    int far = 0;
                    for (int i = 1; i < dist.Length; i++) if (dist[i] > dist[far]) far = i;
                    seeds.Add(centers[far]);
                    for (int i = 0; i < dist.Length; i++) dist[i] = Mathf.Min(dist[i], (centers[i] - centers[far]).sqrMagnitude);
                }
                var of = new int[centers.Count];
                for (int pass = 0; pass < 2; pass++)
                {
                    var sum = new Vector3[k];
                    var count = new int[k];
                    for (int i = 0; i < centers.Count; i++)
                    {
                        int best = 0;
                        for (int c = 1; c < k; c++)
                            if ((centers[i] - seeds[c]).sqrMagnitude < (centers[i] - seeds[best]).sqrMagnitude) best = c;
                        of[i] = best;
                        sum[best] += centers[i];
                        count[best]++;
                    }
                    for (int c = 0; c < k; c++) if (count[c] > 0) seeds[c] = sum[c] / count[c];
                }
                for (int i = 0; i < centers.Count; i++)
                {
                    centers[i] = Vector3.Lerp(centers[i], seeds[of[i]], l.ClumpGather);
                    if (l.TintPerClump) branchRandom[i] = Mathf.Repeat(of[i] * 0.618034f + 0.31f, 1f);
                }
                _clumpCenters = seeds;
                return of;
            }

            // One clump per end branch tip (cards were scattered around the tips): its centre is the mean of its cards.
            private int[] TipClumps(List<Vector3> centers, List<int> clusterOf, List<float> branchRandom)
            {
                int k = 0;
                foreach (int c in clusterOf) k = Mathf.Max(k, c + 1);
                var sum = new Vector3[k];
                var count = new int[k];
                for (int i = 0; i < centers.Count; i++)
                {
                    sum[clusterOf[i]] += centers[i];
                    count[clusterOf[i]]++;
                }
                _clumpCenters = new List<Vector3>(k);
                for (int c = 0; c < k; c++) _clumpCenters.Add(count[c] > 0 ? sum[c] / count[c] : Vector3.zero);
                if (_r.Leaves.TintPerClump)
                    for (int i = 0; i < centers.Count; i++) branchRandom[i] = Mathf.Repeat(clusterOf[i] * 0.618034f + 0.31f, 1f);
                return clusterOf.ToArray();
            }

            // Shell pushes each clump's cards out to its average radius (a round puff instead of a filled ball);
            // ClumpHeight squashes or stretches it, FlattenBottom flattens its underside into a shelf.
            private void ShapeClumps(List<Vector3> centers, int[] clumpOf)
            {
                LeafSettings l = _r.Leaves;
                if (clumpOf == null || (l.Shell <= 0f && l.FlattenBottom <= 0f && l.ClumpHeight == 1f)) return;
                int k = _clumpCenters.Count;
                var radius = new float[k];
                var count = new int[k];
                for (int i = 0; i < centers.Count; i++)
                {
                    radius[clumpOf[i]] += (centers[i] - _clumpCenters[clumpOf[i]]).magnitude;
                    count[clumpOf[i]]++;
                }
                for (int c = 0; c < k; c++) radius[c] = count[c] > 0 ? radius[c] / count[c] : 0f;
                for (int i = 0; i < centers.Count; i++)
                {
                    Vector3 o = _clumpCenters[clumpOf[i]];
                    Vector3 d = centers[i] - o;
                    float m = d.magnitude;
                    if (m > 1e-5f) d *= Mathf.Lerp(m, radius[clumpOf[i]], l.Shell) / m;
                    d.y *= l.ClumpHeight;
                    if (d.y < 0f) d.y *= 1f - l.FlattenBottom;
                    centers[i] = o + d;
                }
            }
        }
    }
}
