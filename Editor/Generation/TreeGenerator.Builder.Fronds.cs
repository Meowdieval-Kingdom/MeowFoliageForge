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
            // Fronds: long cards laid along each end branch, following its curve (conifer sprays). Each trunk top gets
            // two crossed upright cards as the leader. CardsPerBranch = fronds per branch, fanned around it by RollJitter.
            private void EmitFronds(List<Branch> terminals, List<Branch> tops, ref int cards, ref int dropped)
            {
                LeafSettings l = _r.Leaves;
                LeafShapeSettings shape = _r.LeafShapes;
                int per = l.CardsPerBranch;
                if (per <= 0 || terminals.Count + tops.Count == 0) return;
                int ws = shape.Fold != 0f ? Mathf.Max(2, shape.WidthSegments) : Mathf.Max(1, shape.WidthSegments);
                int ls = Mathf.Max(4, shape.LengthSegments); // enough rows to follow the branch
                const int leader = 2;
                int wanted = per * terminals.Count + leader * tops.Count;
                int cap = Mathf.Max(8, _r.Budget.MaxCards);
                if (_r.Budget.FitCardsToTriangles) cap = Mathf.Clamp((_r.Budget.MaxTriangles - _wood.Count / 3) / (2 * ws * ls), 0, cap);
                if (wanted > cap) per = Mathf.Max(1, (cap - leader * tops.Count) / Mathf.Max(1, terminals.Count));

                Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
                foreach (Branch br in terminals)
                {
                    Vector3 p = br.P[br.P.Count - 1];
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                foreach (Branch br in tops)
                {
                    Vector3 p = br.P[br.P.Count - 1];
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                Vector3 crown = (min + max) * 0.5f;
                Vector3 radii = Vector3.Max((max - min) * 0.5f + Vector3.one * l.ClusterRadius, Vector3.one * 0.1f);
                List<Rect> cells = LeafBounds.Cells(_r.Look.LeafTexture, shape.Columns, shape.Rows);
                Rect Cell() => cells[cells.Count > 1 ? Mathf.Min((int)(F() * cells.Count), cells.Count - 1) : 0];

                float leaderStart = Mathf.Clamp(_r.Branches.StartEnd.y, 0.5f, 0.95f);
                foreach (Branch top in tops)
                    for (int k = 0; k < leader && cards < cap; k++, cards++)
                        EmitFrond(top, leaderStart, l.ClusterRadius * 1.5f, k * 90f, l.CardSize.x * 0.7f, shape.Fold, ws, ls, crown, radii, Cell());
                foreach (Branch br in terminals)
                    for (int k = 0; k < per && cards < cap; k++, cards++)
                    {
                        float t = per > 1 ? k / (per - 1f) : 0.5f;
                        float start = Mathf.Lerp(l.TipBias, (1f + l.TipBias) * 0.5f, per > 1 ? k / (float)per : 0f);
                        float roll = l.Roll + (t - 0.5f) * l.RollJitter + (F() * 2f - 1f) * 10f;
                        float fold = shape.Fold * (1f + (F() * 2f - 1f) * shape.Jitter);
                        EmitFrond(br, start, l.ClusterRadius, roll, Mathf.Lerp(l.CardSize.x, l.CardSize.y, F()), fold, ws, ls, crown, radii, Cell());
                    }
                dropped = Mathf.Max(0, wanted - cards);
            }

            // One frond from branch share f0 to the tip plus overhang, width w, rolled around the branch; fold droops (negative) or lifts its sides.
            private void EmitFrond(Branch br, float f0, float overhang, float roll, float w, float foldDeg, int ws, int ls, Vector3 crown, Vector3 radii, Rect content)
            {
                int baseIndex = _pos.Count, indexStart = _leaf.Count;
                float fold = foldDeg * Mathf.Deg2Rad;
                int last = br.P.Count - 1;
                Vector3 tip = br.P[last];
                Vector3 tipTan = (br.P[last] - br.P[last - 1]).normalized;
                float span = (1f - f0) * br.Length;
                float total = span + overhang;
                Vector3 pivot = PointAt(br, f0, out _, out _);
                float phase = F();
                byte rnd = (byte)(br.Random * 255f);
                Vector3 right0 = Vector3.right, up0 = Vector3.up;
                for (int iy = 0; iy <= ls; iy++)
                {
                    float s = iy / (float)ls * total;
                    Vector3 c, tan;
                    if (s <= span && span > 1e-4f) c = PointAt(br, f0 + (1f - f0) * s / span, out _, out tan);
                    else
                    {
                        c = tip + tipTan * (s - span);
                        tan = tipTan;
                    }
                    Vector3 across = Vector3.Cross(tan, Vector3.up);
                    if (across.sqrMagnitude < 1e-4f) across = Vector3.Cross(tan, Vector3.right);
                    across.Normalize();
                    Quaternion q = Quaternion.AngleAxis(roll, tan);
                    across = q * across;
                    Vector3 n = Vector3.Cross(across, tan).normalized; // the sky side of an unrolled frond
                    if (iy == 0)
                    {
                        right0 = across;
                        up0 = tan;
                    }
                    for (int ix = 0; ix <= ws; ix++)
                    {
                        float x = (ix / (float)ws - 0.5f) * w;
                        _pos.Add(c + across * (x * Mathf.Cos(fold)) + n * (Mathf.Abs(x) * Mathf.Sin(fold)));
                        _uv.Add(new Vector2(Mathf.Lerp(content.xMin, content.xMax, ix / (float)ws), Mathf.Lerp(content.yMin, content.yMax, iy / (float)ls)));
                        _uv1.Add(new Vector4(pivot.x, pivot.y, pivot.z, phase));
                        _col.Add(new Color32(0, 255, 255, rnd));
                        _nor.Add(n);
                    }
                }
                FinishGrid(baseIndex, indexStart, ws, ls, crown, radii, _r.Leaves.NormalBlend, 0, right0, up0);
            }
        }
    }
}
