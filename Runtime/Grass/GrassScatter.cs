using System.Collections.Generic;
using UnityEngine;

namespace Meowdieval.FoliageForge
{
    /// <summary>One brush dab on a Grass Field, in the field's local space: paints a disc of grass or erases everything laid down before it.</summary>
    [System.Serializable]
    public struct GrassStroke
    {
        /// <summary>Centre, local to the field.</summary>
        public Vector3 Point;
        /// <summary>Radius in local units.</summary>
        public float Radius;
        /// <summary>True removes grass from earlier layers inside the disc instead of adding.</summary>
        public bool Erase;
        /// <summary>Seeds the scatter of this dab so a repaint reproduces it.</summary>
        public int Seed;
        /// <summary>Density multiplier of this dab. 0 (strokes saved before it existed) counts as 1.</summary>
        public float Density;

        /// <summary>Erase dabs only: bit i set erases type i. 0 (strokes saved before it existed) erases every type.</summary>
        public int TypeMask;

        /// <summary>True when this dab erases type <paramref name="variant"/>.</summary>
        public bool Hits(int variant) => TypeMask == 0 || variant >= 32 || (TypeMask & (1 << variant)) != 0;

        /// <summary><see cref="Density"/> with the legacy 0 read as 1.</summary>
        public float EffectiveDensity => Density > 0f ? Density : 1f;
    }

    /// <summary>Scatter rules shared by the Grass Field (ray-cast ground) and the editor preview patch (flat ground).</summary>
    public static class GrassScatter
    {
        /// <summary>
        /// Jittered-grid candidate points over a local box, thinned by the variant's clustering noise and (with
        /// <paramref name="applyMix"/>) its Mix %. Lowering Mix thins a stable subset instead of reshuffling. Points are returned
        /// in world space at the top of the box; the caller finds the ground below each.
        /// </summary>
        public static void Candidates(GrassProfile profile, int variant, Vector2 size, float topY, Matrix4x4 localToWorld,
            System.Random rng, int maxCandidates, List<Vector3> points, List<float> yaws, bool circular = false, bool applyMix = true)
        {
            GrassVariant gv = profile.Variants[variant];
            points.Clear();
            yaws.Clear();
            float area = size.x * size.y * Mathf.Abs(localToWorld.lossyScale.x * localToWorld.lossyScale.z);
            int target = Mathf.Min(Mathf.CeilToInt(area * gv.Density), maxCandidates);
            if (target <= 0) return;
            int nx = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(target * size.x / Mathf.Max(size.y, 0.01f))));
            int nz = Mathf.Max(1, Mathf.CeilToInt(target / (float)nx));
            float noiseOffset = variant * 137.3f + profile.Seed * 3.7f;
            float mix = applyMix ? GrassVariant.ApplyMix(1f, gv.Mix) : 1f;
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    float lx = ((x + (float)rng.NextDouble()) / nx - 0.5f) * size.x;
                    float lz = ((z + (float)rng.NextDouble()) / nz - 0.5f) * size.y;
                    float yaw = (float)rng.NextDouble() * 360f;
                    float keep = (float)rng.NextDouble();
                    if (circular && lx * lx / (size.x * size.x * 0.25f) + lz * lz / (size.y * size.y * 0.25f) > 1f) continue;
                    Vector3 world = localToWorld.MultiplyPoint3x4(new Vector3(lx, topY, lz));
                    float cover = mix;
                    if (gv.Clustering > 0f)
                    {
                        float n = Mathf.PerlinNoise(world.x * gv.ClusterScale + noiseOffset, world.z * gv.ClusterScale + noiseOffset);
                        cover *= Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.65f, n)), gv.Clustering);
                    }
                    if (keep > cover) continue;
                    points.Add(world);
                    yaws.Add(yaw);
                }
        }

        /// <summary>
        /// Candidates of one paint dab: a seeded random scatter of the variant's density times its Mix % times the dab's density over the disc. Appended to the lists,
        /// at <paramref name="localToWorld"/>, 2 local units above the dab so the caller can ray-cast down.
        /// </summary>
        public static void StrokeCandidates(GrassProfile profile, int variant, GrassStroke stroke, Matrix4x4 localToWorld,
            List<Vector3> points, List<float> yaws, int maxCandidates = 4000)
        {
            GrassVariant gv = profile.Variants[variant];
            float worldRadius = stroke.Radius * Mathf.Abs(localToWorld.lossyScale.x);
            int count = Mathf.Min(Mathf.CeilToInt(Mathf.PI * worldRadius * worldRadius * GrassVariant.ApplyMix(gv.Density, gv.Mix) * stroke.EffectiveDensity), maxCandidates);
            var rng = new System.Random(stroke.Seed * 31 + variant * 7919 + 1);
            for (int i = 0; i < count; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = stroke.Radius * Mathf.Sqrt((float)rng.NextDouble());
                Vector3 local = stroke.Point + new Vector3(Mathf.Cos(angle) * r, 2f, Mathf.Sin(angle) * r);
                points.Add(localToWorld.MultiplyPoint3x4(local));
                yaws.Add((float)rng.NextDouble() * 360f);
            }
        }

        /// <summary>
        /// True when a later erase dab covers the point. <paramref name="seq"/> is the layer the candidate came from: 0 for the box
        /// fill, i + 1 for stroke i, so only strokes after it can erase it. Only dabs that include <paramref name="variant"/> in
        /// their <see cref="GrassStroke.TypeMask"/> erase it. Works in the field's local space.
        /// </summary>
        public static bool Erased(Vector3 localPoint, int seq, GrassStroke[] strokes, int variant = 0)
        {
            if (strokes == null) return false;
            for (int j = seq; j < strokes.Length; j++)
            {
                if (!strokes[j].Erase || !strokes[j].Hits(variant)) continue;
                float dx = localPoint.x - strokes[j].Point.x, dz = localPoint.z - strokes[j].Point.z;
                if (dx * dx + dz * dz <= strokes[j].Radius * strokes[j].Radius) return true;
            }
            return false;
        }

        /// <summary>Instance transform for one clump standing on <paramref name="point"/>; also returns its size for bounds.</summary>
        public static Matrix4x4 Clump(GrassProfile profile, GrassVariant gv, System.Random rng, Vector3 point, Vector3 normal, float yaw, out float height, out float width)
        {
            height = Mathf.Lerp(gv.Height.x, gv.Height.y, (float)rng.NextDouble());
            width = Mathf.Lerp(gv.Width.x, gv.Width.y, (float)rng.NextDouble());
            Vector3 up = Vector3.Slerp(Vector3.up, normal, profile.NormalAlign);
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.AngleAxis(yaw, Vector3.up);
            return Matrix4x4.TRS(point - up * 0.02f, rot, new Vector3(width, height, width));
        }
    }

    /// <summary>Writes a profile's look and feature switches into a grass material.</summary>
    public static class GrassMaterials
    {
        private static float B(bool b) => b ? 1f : 0f;

        /// <summary>
        /// Pushes every setting of <paramref name="profile"/> and variant <paramref name="gv"/> into <paramref name="m"/>.
        /// <paramref name="groundMap"/> and <paramref name="mapRect"/> (xy corner, zw size, world xz) feed the ground colour feature.
        /// </summary>
        public static void Apply(Material m, GrassProfile profile, GrassVariant gv, Texture2D groundMap, Vector4 mapRect, bool baked = false)
        {
            GrassFeatures f = profile.Features;
            m.enableInstancing = true;
            m.SetTexture("_BaseMap", gv.Texture != null ? gv.Texture : Texture2D.whiteTexture);
            m.SetFloat("_Cutoff", profile.AlphaCutoff);
            m.SetColor("_Bottom", gv.Bottom);
            m.SetColor("_Top", gv.Top);
            m.SetColor("_Tip", gv.Tip);
            m.SetFloat("_TipStart", gv.TipStart);
            m.SetFloat("_TipHue", gv.TipHueVariation);
            m.SetFloat("_WindWeight", gv.WindWeight);
            m.SetFloat("_GradientBias", profile.GradientBias);
            m.SetFloat("_RootAO", profile.RootDarkening);
            m.SetFloat("_Variation", profile.ColorVariation);
            m.SetColor("_PatchColor", profile.PatchColor);
            m.SetFloat("_PatchAmount", profile.PatchAmount);
            m.SetFloat("_PatchScale", profile.PatchScale);
            m.SetColor("_ShadowColor", profile.ShadowColor);
            m.SetFloat("_ShadowSoftness", profile.ShadowSoftness);
            m.SetColor("_RimColor", profile.RimColor);
            m.SetFloat("_RimPower", profile.RimPower);
            m.SetFloat("_RimStrength", profile.RimStrength);
            m.SetColor("_TransColor", profile.TranslucencyColor);
            m.SetFloat("_TransStrength", profile.TranslucencyStrength);
            m.SetColor("_GroundColor", profile.GroundColor);
            m.SetFloat("_GroundBlend", profile.GroundBlendHeight);
            m.SetFloat("_Flutter", profile.Flutter);
            m.SetFloat("_NormalUp", profile.NormalUp);
            m.SetFloat("_ShadowDistance", profile.ShadowDistance);
            m.SetFloat("_AddLightStrength", profile.AdditionalLightStrength);
            m.SetFloat("_TrampleStrength", profile.TrampleStrength);
            m.SetFloat("_GroundMapStrength", profile.GroundMapStrength);
            m.SetFloat("_FadeEnd", profile.DrawDistance);
            m.SetFloat("_FadeStart", profile.DrawDistance * 0.75f);
            m.SetFloat("_LodNear", profile.DrawDistance * profile.LodMid);
            m.SetFloat("_LodFar", profile.DrawDistance * Mathf.Max(profile.LodFar, profile.LodMid + 0.01f));
            m.SetFloat("_LodMinShare", profile.LodMinShare);
            bool map = f.GroundMap && groundMap != null;
            m.SetTexture("_GroundMap", map ? groundMap : Texture2D.whiteTexture);
            m.SetVector("_GroundMapRect", mapRect);
            m.SetFloat("_AlphaToMask", B(f.AlphaToCoverage));
            m.SetFloat("_GustSpeed", profile.GustSpeed);
            m.SetFloat("_GustFrequency", profile.GustFrequency);
            m.SetFloat("_GustIntensity", profile.GustIntensity);
            m.SetColor("_GustColor", profile.GustColor);
            m.SetFloat("_PatchBlur", profile.PatchBlur);
            m.SetVector("_PatchOffset", profile.PatchOffset);
            m.SetFloat("_LightContrib", profile.LightContribution);
            m.SetFloat("_ShadowStrength", profile.ShadowStrength);
            bool windTex = f.WindTexture && GrassWindNoise.Ensure();
            bool trampleMap = f.TrampleRT && !baked && GrassTrampleRT.Ensure(profile); // baked patches have no field to drive the map
            bool ramp = f.LightRamp && profile.LightRamp != null;
            m.SetTexture("_LightRamp", ramp ? profile.LightRamp : Texture2D.whiteTexture);
            m.SetColor("_TrailTop", profile.TrailTopColor);
            m.SetColor("_TrailBottom", profile.TrailBottomColor);
            m.SetFloat("_TrailStrength", profile.TrailStrength);
            m.SetFloat("_PerspectiveStrength", profile.PerspectiveStrength);
            m.SetVector("_Use6", new Vector4(B(windTex), B(trampleMap), B(trampleMap && f.TrailTint), B(ramp)));
            m.SetVector("_Use7", new Vector4(B(f.PerspectiveCorrection), 0f, 0f, 0f));
            m.SetVector("_Use5", new Vector4(B(f.Gusts), B(baked), 0f, 0f)); // baked: clump origins come from UV1, not the object pivot
            m.SetVector("_Use", new Vector4(B(f.Wind), B(f.Texture), B(f.Gradient), B(f.Variation)));
            m.SetVector("_Use2", new Vector4(B(f.Patches), B(f.StylizedShading), B(f.Rim), B(f.Translucency)));
            m.SetVector("_Use3", new Vector4(B(f.GroundBlend), B(f.DistanceFade), B(f.ReceiveShadows), B(map)));
            m.SetVector("_Use4", new Vector4(B(f.AlphaToCoverage), B(f.AdditionalLights), B(f.Trample && !trampleMap), B(f.DistanceLod)));
        }
    }
}
