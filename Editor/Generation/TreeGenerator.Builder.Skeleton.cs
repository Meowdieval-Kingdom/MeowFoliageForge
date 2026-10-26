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
            // trunk, stems, branches, twigs and roots; returns the branches that carry leaves
            private List<Branch> BuildSkeleton(out Branch trunk)
            {
                TrunkSettings t = _r.Trunk;
                BranchSettings b = _r.Branches;
                trunk = BuildTrunk(t);
                _branches.Add(trunk);
                SplitTrunk(trunk);
                int maxLevel = Mathf.Clamp(b.Levels, 0, 2);
                var level1 = new List<Branch>();
                if (maxLevel >= 1) BuildLevel1(trunk, level1);
                PruneToEnvelope(trunk, level1);
                if (maxLevel >= 2) BuildLevel2(level1);
                BuildRoots();

                var terminals = new List<Branch>();
                foreach (Branch br in _branches)
                    if (!br.NoLeaves && br.Level > 0 && (br.Level == maxLevel || (_r.Leaves.OnInnerBranches && br.Level == 1))) terminals.Add(br);
                return terminals;
            }

            private Branch BuildTrunk(TrunkSettings t)
            {
                var trunk = new Branch { Level = 0, Length = t.Height, Random = F(), Main = true };
                Vector2 leanDir = UnitHorizontal();
                _leanDir = new Vector3(leanDir.x, 0f, leanDir.y);
                Vector2 lean = leanDir * t.Lean;
                Vector3 dir = (Vector3.up + new Vector3(lean.x, 0f, lean.y)).normalized;
                Vector3 p = Vector3.zero;
                int segs = Mathf.Max(2, t.HeightSegments);
                float step = t.Height / segs;
                for (int i = 0; i <= segs; i++)
                {
                    float u = i / (float)segs;
                    trunk.P.Add(p);
                    float r = Mathf.Lerp(t.BaseRadius, t.BaseRadius * t.TipRadiusRatio, Mathf.Pow(u, t.TaperPower));
                    trunk.R.Add(r * (1f + t.Flare * Mathf.Exp(-u * 10f)));
                    dir = (dir + Unit() * t.Bend * 0.35f + Vector3.up * 0.2f).normalized;
                    p += dir * step;
                }
                Bend(trunk, t.NoiseBend * t.Height, t.NoiseFrequency, 0f);
                return trunk;
            }

            // Smooth Perlin bends at right angles to the path. The base stays put, so the path keeps its attachment.
            private void Bend(Branch br, float amount, float frequency, float salt)
            {
                if (amount <= 0f) return;
                int n = br.P.Count;
                Vector3 axis = br.P[n - 1] - br.P[0];
                if (axis.sqrMagnitude < 1e-8f) return;
                axis.Normalize();
                Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 side2 = Vector3.Cross(axis, side);
                float seed = NoiseSeed(br) + salt;
                for (int i = 1; i < n; i++)
                {
                    float s = i / (float)(n - 1);
                    float x = Mathf.PerlinNoise(seed, s * frequency) * 2f - 1f;
                    float y = Mathf.PerlinNoise(seed + 37.1f, s * frequency) * 2f - 1f;
                    br.P[i] += (side * x + side2 * y) * (amount * s);
                }
            }

            // Cuts the trunk at the split height and grows Splits + 1 stems from there. Each stem follows the old trunk path,
            // leaning out by SplitAngle and turning around the axis by Braid, so a low split gives braided trunks.
            private void SplitTrunk(Branch trunk)
            {
                TrunkSettings t = _r.Trunk;
                if (t.Splits <= 0) return;
                int last = trunk.P.Count - 1;
                int cut = Mathf.Clamp(Mathf.RoundToInt(t.SplitHeight * last), 1, last - 1);
                List<Vector3> path = trunk.P.GetRange(cut, last - cut + 1);
                List<float> radii = trunk.R.GetRange(cut, last - cut + 1);
                float rCut = trunk.R[cut];
                trunk.P.RemoveRange(cut + 1, last - cut);
                trunk.R.RemoveRange(cut + 1, last - cut);
                // one short ring narrowing inside the stems closes the cut
                trunk.P.Add(trunk.P[cut] + (trunk.P[cut] - trunk.P[cut - 1]).normalized * (rCut * 0.5f));
                trunk.R.Add(rCut * 0.3f);
                trunk.USpan = (cut + 1) / (float)last;
                _splitU = cut / (float)last;

                int count = t.Splits + 1;
                float rStem = rCut * Mathf.Min(0.85f, 1.15f / Mathf.Sqrt(count));
                float spread = Mathf.Tan(t.SplitAngle * Mathf.Deg2Rad);
                float yaw0 = H(1, 1) * 360f;
                for (int k = 0; k < count; k++)
                {
                    var stem = new Branch { Level = 0, Random = H(k, 2), Yaw = yaw0 + k * 360f / count + (H(k, 3) - 0.5f) * 40f };
                    float scale = k == 0 ? 1f : Mathf.Lerp(0.75f, 0.95f, H(k, 4));
                    float d = 0f;
                    for (int i = 0; i < path.Count; i++)
                    {
                        if (i > 0) d += (path[i] - path[i - 1]).magnitude;
                        float s = i / (float)(path.Count - 1);
                        float yaw = (stem.Yaw + t.Braid * 360f * s) * Mathf.Deg2Rad;
                        // every stem starts on the trunk's axis and drifts out over its first few radii, so its base never overhangs the cut
                        Vector3 p = path[i] + new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * (rCut * 0.55f * Mathf.SmoothStep(0f, 1f, d / (rCut * 3f)) + spread * d);
                        stem.P.Add(path[0] + (p - path[0]) * scale);
                        stem.R.Add(rStem * radii[i] / rCut);
                    }
                    stem.Length = d * scale;
                    Bend(stem, t.NoiseBend * t.Height * 0.5f, t.NoiseFrequency, 11f * (k + 1));
                    _stems.Add(stem);
                    _branches.Add(stem);
                }
            }

            // Point at trunk height share u. Above a split it lies on stem k (wrapped), which is returned in on.
            private Vector3 TrunkPoint(Branch trunk, float u, int k, out float radius, out Branch on)
            {
                if (_stems.Count == 0 || u <= _splitU)
                {
                    on = trunk;
                    float f = _stems.Count == 0 ? u : u / Mathf.Max(_splitU, 1e-4f) * (trunk.P.Count - 2) / (trunk.P.Count - 1);
                    return PointAt(trunk, f, out radius, out _);
                }
                on = _stems[k % _stems.Count];
                return PointAt(on, (u - _splitU) / (1f - _splitU), out radius, out _);
            }

            // a tube with few sides is narrower across its flat faces than its ring radius
            private static float InscribedFactor(int sides) => Mathf.Cos(Mathf.PI / Mathf.Max(3, sides));

            // Exposed roots: start inside the trunk base, dive to the ground, snake along it and curl up at the tip.
            private void BuildRoots()
            {
                TrunkSettings t = _r.Trunk;
                if (t.Roots <= 0) return;
                float startY = Mathf.Min(t.Height * 0.15f, t.BaseRadius * 2.5f);
                // roots leave the trunk where it really is (bent, leaning) and are no thicker than the trunk's flat faces there
                Vector3 axis = TrunkPoint(_branches[0], startY / t.Height, 0, out float trunkR, out _);
                float r0 = Mathf.Min(t.BaseRadius * t.RootRadius, trunkR * InscribedFactor(t.RadialSides) * 0.9f);
                float curl = t.RootCurl * Mathf.Deg2Rad;
                const int segs = 8;
                float yaw0 = H(0, 7) * 360f;
                for (int k = 0; k < t.Roots; k++)
                {
                    var root = new Branch { Level = 1, NoLeaves = true, Random = H(k, 8), Length = t.RootLength * t.Height * Mathf.Lerp(0.7f, 1.1f, H(k, 9)) };
                    float yawK = (yaw0 + k * 360f / t.Roots + (H(k, 10) - 0.5f) * 30f) * Mathf.Deg2Rad;
                    float seed = NoiseSeed(root) + 5f;
                    float step = root.Length / segs;
                    Vector3 p = axis + new Vector3(Mathf.Cos(yawK), 0f, Mathf.Sin(yawK)) * (trunkR * 0.15f);
                    p.y = startY;
                    float lift = 0f;
                    for (int i = 0; i <= segs; i++)
                    {
                        float s = i / (float)segs;
                        float r = r0 * Mathf.Lerp(1f, 0.25f, s);
                        if (i > 0)
                        {
                            float c = Mathf.Clamp01((s - 0.6f) / 0.4f);
                            float pitch = curl * c * c;
                            float yaw = yawK + (Mathf.PerlinNoise(seed, s * 2.5f) - 0.5f) * 2.4f * t.RootWander;
                            p += new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * (step * Mathf.Cos(pitch));
                            lift += Mathf.Sin(pitch) * step;
                            // the centre stays a little above the ground, so the root shows as a half-buried bulge that arches once
                            p.y = Mathf.Lerp(startY, r * 0.5f, Mathf.SmoothStep(0f, 1f, s / 0.35f)) + Mathf.Sin(Mathf.PI * s) * r * 0.5f + lift;
                        }
                        root.P.Add(p);
                        root.R.Add(r);
                    }
                    _branches.Add(root);
                }
            }

            private Vector2 UnitHorizontal()
            {
                float a = F() * Mathf.PI * 2f;
                return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }

            private void BuildLevel1(Branch trunk, List<Branch> output)
            {
                BranchSettings b = _r.Branches;
                int count = b.Count;
                if (count <= 0) return;
                int whorl = Mathf.Max(1, b.WhorlSize);
                int rings = Mathf.CeilToInt(count / (float)whorl);
                float yaw0 = F() * 360f, spiral = 0f;
                for (int k = 0; k < count; k++)
                {
                    int ring = k / whorl;
                    float u = rings <= 1 ? Mathf.Lerp(b.StartEnd.x, b.StartEnd.y, 0.5f)
                        : Mathf.Lerp(b.StartEnd.x, b.StartEnd.y, ring / (float)(rings - 1));
                    u = Mathf.Clamp01(u + (F() - 0.5f) * 0.03f);
                    float yaw = whorl > 1
                        ? yaw0 + ring * 37f + (k % whorl) * 360f / whorl + (F() - 0.5f) * 20f
                        : yaw0 + (spiral += Mathf.Lerp(b.YawStep.x, b.YawStep.y, H(k, 6))) - b.YawStep.x + (F() - 0.5f) * 20f;
                    float pitch = Mathf.Lerp(b.PitchRange.x, b.PitchRange.y, F());
                    Vector3 origin = TrunkPoint(trunk, u, k, out float radius, out Branch on);
                    if (on != trunk) yaw = on.Yaw + Mathf.DeltaAngle(on.Yaw, yaw) * 0.5f; // stem branches grow away from the other stems
                    Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * (Quaternion.Euler(pitch, 0f, 0f) * Vector3.up);
                    float length = _r.Trunk.Height * b.LengthRatio * Mathf.Max(0.05f, b.LengthProfile.Evaluate(u)) * Mathf.Lerp(0.85f, 1.15f, F());
                    var flat = new Vector3(dir.x, 0f, dir.z);
                    flat = flat.sqrMagnitude < 1e-6f ? Vector3.zero : flat.normalized;
                    if (b.LengthNoise > 0f)
                    {
                        float seed = Mathf.Repeat(_r.Seed * 0.381966f, 1f) * 100f;
                        length *= Mathf.Max(0.2f, 1f + (Mathf.PerlinNoise(seed + flat.x * 1.2f + u * 3f, seed + flat.z * 1.2f) - 0.5f) * 2f * b.LengthNoise);
                    }
                    if (b.Asymmetry > 0f) length *= Mathf.Max(0.2f, 1f + b.Asymmetry * 0.8f * Vector3.Dot(flat, _leanDir));
                    bool broken = b.Broken > 0f && H(k, 5) < b.Broken;
                    if (broken) length *= Mathf.Lerp(0.2f, 0.45f, H(k, 6));
                    // the branch base stays inside the trunk surface, even on a trunk with few sides
                    Branch br = Grow(origin, dir, length, Mathf.Min(radius * b.RadiusRatio, radius * InscribedFactor(_r.Trunk.RadialSides) * 0.9f), 1, b.Segments, broken ? 0.55f : 0.08f);
                    br.Broken = br.NoLeaves = broken;
                    output.Add(br);
                }
            }

            private void BuildLevel2(List<Branch> parents)
            {
                BranchSettings b = _r.Branches;
                if (b.SubCount <= 0) return;
                foreach (Branch parent in parents)
                {
                    if (parent.NoLeaves) continue;
                    for (int s = 0; s < b.SubCount; s++)
                    {
                        float f = Mathf.Clamp01(Mathf.Lerp(0.3f, 0.95f, (s + 0.5f) / b.SubCount) + (F() - 0.5f) * 0.1f);
                        Vector3 origin = PointAt(parent, f, out float radius, out Vector3 tangent);
                        Vector3 perp = Vector3.Cross(tangent, Unit());
                        perp = perp.sqrMagnitude < 1e-4f ? Vector3.right : perp.normalized;
                        Vector3 dir = Quaternion.AngleAxis(Mathf.Lerp(25f, 60f, F()), perp) * tangent;
                        float length = parent.Length * b.SubLengthRatio * Mathf.Lerp(0.8f, 1.2f, F());
                        Grow(origin, dir, length, radius * b.RadiusRatio, 2, Mathf.Max(2, b.Segments - 1));
                    }
                }
            }

            private Branch Grow(Vector3 origin, Vector3 dir, float length, float startRadius, int level, int segments, float tipRatio = 0.08f)
            {
                BranchSettings b = _r.Branches;
                var br = new Branch { Level = level, Length = length, Random = F() };
                Vector3 p = origin;
                float step = length / segments;
                dir.Normalize();
                for (int i = 0; i <= segments; i++)
                {
                    float u = i / (float)segments;
                    br.P.Add(p);
                    br.R.Add(Mathf.Lerp(startRadius, startRadius * tipRatio, u));
                    dir = (dir + Unit() * b.Wander * 0.5f + Vector3.down * b.Droop * 0.25f).normalized;
                    p += dir * step;
                }
                Bend(br, b.NoiseBend * length, b.NoiseFrequency, level * 3f);
                _branches.Add(br);
                return br;
            }

            private static Vector3 PointAt(Branch br, float f, out float radius, out Vector3 tangent)
            {
                int last = br.P.Count - 1;
                float x = Mathf.Clamp01(f) * last;
                int i = Mathf.Min((int)x, last - 1);
                float frac = x - i;
                radius = Mathf.Lerp(br.R[i], br.R[i + 1], frac);
                tangent = (br.P[i + 1] - br.P[i]).normalized;
                return Vector3.Lerp(br.P[i], br.P[i + 1], frac);
            }
        }
    }
}
