using System.Collections.Generic;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    public static partial class TreeGenerator
    {
        /// <summary>True when the recipe bakes an inner core shell per blob (submesh 2).</summary>
        public static bool HasCore(TreeRecipe r) => r.Leaves.Layout == LeafLayout.Blobs && r.Look.CoreMesh;

        sealed partial class Builder
        {
            const float CoreMinInset = 0.3f, CoreMargin = 0.02f; // the shell never shrinks below 30% of the surface distance
            readonly List<int> _core = new List<int>();
            readonly List<(int blob, int first, int end)> _coreRanges = new List<(int, int, int)>();
            float[] _blobHeight; // per vertex: 0..1 height inside its blob, -1 for wood

            // vertex colour B carries 0.5 + 0.5 * AO from how deep the vertex sits inside the union of blobs; R later carries the height inside its own blob
            private void BlobShading(List<CanopyBlob> blobs)
            {
                if (HasCore(_r)) EmitCore(blobs);
                float ao = _r.Renormalize.Enabled ? _r.Renormalize.AOStrength : 0f;
                _blobHeight = new float[_pos.Count];
                for (int i = 0; i < _blobHeight.Length; i++) _blobHeight[i] = -1f;
                var up = new float[blobs.Count];
                var down = new float[blobs.Count];
                for (int c = 0; c < blobs.Count; c++)
                {
                    up[c] = Mathf.Max(0.01f, BlobCage.Extent(blobs[c], new Vector3(0.005f, 1f, 0f).normalized));
                    down[c] = Mathf.Max(0.01f, BlobCage.Extent(blobs[c], new Vector3(0.005f, -1f, 0f).normalized));
                }
                foreach (Card card in _cards)
                    Shade(blobs, card.Clump, card.V0, card.V0 + card.VCount, ao, up, down, 0f);
                foreach (var range in _coreRanges)
                    Shade(blobs, range.blob, range.first, range.end, ao, up, down, 0.5f);
            }

            private void Shade(List<CanopyBlob> blobs, int blob, int first, int end, float ao, float[] up, float[] down, float minDepth)
            {
                CanopyBlob own = blobs[blob];
                for (int v = first; v < end; v++)
                {
                    float depth = minDepth;
                    foreach (CanopyBlob b in blobs)
                    {
                        Vector3 d = Quaternion.Inverse(BlobCage.Rot(b)) * (_pos[v] - b.Position);
                        var q = new Vector3(d.x / Mathf.Max(b.Radii.x, 1e-3f), d.y / Mathf.Max(b.Radii.y, 1e-3f), d.z / Mathf.Max(b.Radii.z, 1e-3f));
                        depth = Mathf.Max(depth, 1f - q.magnitude);
                    }
                    float occlusion = Mathf.Clamp01(depth * 2f);
                    Color32 c = _col[v];
                    c.b = (byte)(128 + Mathf.RoundToInt((1f - ao * occlusion) * 127f)); // 128..255 keeps the leaf flag
                    _col[v] = c;
                    float y = _pos[v].y - own.Position.y;
                    _blobHeight[v] = Mathf.Clamp01(0.5f + 0.5f * (y >= 0f ? y / up[blob] : y / down[blob]));
                }
            }

            // with Look.CoreAlign the shell shrinks on the side the leaves avoid, so it follows Leaves.PlacementBias (+1 top keeps the upper half, -1 underneath keeps the lower)
            private float CoreAlign(float dirY) => _r.Look.CoreAlign ? Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(-dirY * Mathf.Sign(_r.Leaves.PlacementBias)) * Mathf.Abs(_r.Leaves.PlacementBias)) : 1f;

            // a UV sphere per blob, inset to just under the deepest card, with the blob's smooth normals; UV at the card centre keeps the round mask open
            private void EmitCore(List<CanopyBlob> blobs)
            {
                float deepestSize = _r.Leaves.CardSize.y * _cardGrow;
                int segs = _r.Look.CoreSegments, rings = Mathf.Max(3, Mathf.RoundToInt(segs * 0.7f));
                for (int c = 0; c < blobs.Count; c++)
                {
                    CanopyBlob bl = blobs[c];
                    int baseIndex = _pos.Count;
                    float phase = H(c, 91);
                    byte rnd = (byte)(Mathf.Repeat(c * 0.618034f + 0.31f, 1f) * 255f);
                    for (int iy = 0; iy <= rings; iy++)
                        for (int ix = 0; ix <= segs; ix++)
                        {
                            float polar = iy / (float)rings * Mathf.PI, az = ix / (float)segs * Mathf.PI * 2f;
                            var dir = new Vector3(Mathf.Sin(polar) * Mathf.Cos(az), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(az));
                            Vector3 probe = iy == 0 || iy == rings ? (dir + new Vector3(0.005f, 0f, 0f)).normalized : dir; // off the pole, so the ray hits a face
                            float ext = BlobCage.Extent(bl, probe);
                            // the deepest a card centre can sit (see the card placement in Blobs.cs): the shell stops there, so every card rests on it
                            float deepest = ext * (1f - _r.Leaves.GroupVolume) - _r.Leaves.GroupDepth * deepestSize + _r.Leaves.NormalOffset;
                            float reach = Mathf.Clamp(deepest - CoreMargin, ext * CoreMinInset, ext * 0.97f);
                            _pos.Add(bl.Position + dir * (reach * CoreAlign(dir.y)));
                            _nor.Add(BlobCage.EllipsoidNormal(bl, dir));
                            _uv.Add(new Vector2(0.5f, 0.5f));
                            _uv1.Add(new Vector4(bl.Position.x, bl.Position.y, bl.Position.z, phase));
                            _col.Add(new Color32(0, 255, 255, rnd));
                        }
                    for (int iy = 0; iy < rings; iy++)
                        for (int ix = 0; ix < segs; ix++)
                        {
                            int a = baseIndex + iy * (segs + 1) + ix, b = a + 1, d = a + segs + 1, e = d + 1;
                            _core.Add(a);
                            _core.Add(b);
                            _core.Add(d);
                            _core.Add(b);
                            _core.Add(e);
                            _core.Add(d);
                        }
                    _coreRanges.Add((c, baseIndex, _pos.Count));
                }
            }
        }
    }
}
