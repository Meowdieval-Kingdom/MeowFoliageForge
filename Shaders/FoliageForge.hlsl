#ifndef FOLIAGE_FORGE_INCLUDED
#define FOLIAGE_FORGE_INCLUDED

// Set globally by FoliageWind. Both stay zero when no wind component exists, which leaves only leaf flutter.
float4 _FF_WindDir;     // xyz = world direction (horizontal, normalized), w = strength
float4 _FF_WindParams;  // x = speed, y = gust frequency, z = gust contrast, w = entropy
float4 _FF_GustColor;   // rgb = tint a passing gust leaves on the leaves, a = strength (0 = off)

float3 FF_Rotate(float3 v, float3 axis, float angle)
{
    float s, c;
    sincos(angle, s, c);
    return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
}

// Hue rotation around the grey axis. Negative turns move green toward yellow (warm), positive toward blue (cool).
float3 FF_Hue(float3 c, float turns)
{
    return max(0.0, FF_Rotate(c, float3(0.57735027, 0.57735027, 0.57735027), turns * 6.2831853));
}

float FF_Hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

// smooth value noise 0..1
float FF_Noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(FF_Hash(i), FF_Hash(i + float2(1, 0)), f.x), lerp(FF_Hash(i + float2(0, 1)), FF_Hash(i + float2(1, 1)), f.x), f.y);
}

float FF_Gust(float3 worldPos)
{
    // noise-offset phase (entropy) so neighbouring trees do not sway in lockstep
    float phase = dot(worldPos.xz, _FF_WindDir.xz) * _FF_WindParams.y - _Time.y * _FF_WindParams.x + (FF_Noise(worldPos.xz * 0.15) - 0.5) * _FF_WindParams.w;
    float wave = sin(phase) * 0.5 + 0.5;
    wave += (sin(phase * 2.3 + worldPos.z * 0.35) * 0.5 + 0.5) * 0.35;
    return lerp(1.0, wave, _FF_WindParams.z);
}

// Vertex colour: R = height 0..1, G = sway weight, B = leaf flag (>0.5) with baked occlusion in the upper half, A = per-branch random.
// UV1: xyz = card pivot (object space), w = per-card random / flutter phase 0..1.
// UV2 / UV3 (only when the recipe uses billboards): xyz = unit right / up axis of the card in object space.
// Billboard: 0 = off, 1 = view-aligned, 2 = rotate around the vertical axis only (keeps the card's own tilt).
void FoliageVertex_float(float3 PosOS, float4 VColor, float4 Pivot, float Flutter, float FlutterSpeed, float Sway, float Billboard, float4 Right, float4 Up, out float3 OutPos)
{
    float3 wind = _FF_WindDir.xyz;
    float strength = _FF_WindDir.w;
    float isLeaf = step(0.5, VColor.b);

    float3 posW = TransformObjectToWorld(PosOS);
    float3 pivotW = TransformObjectToWorld(Pivot.xyz);
    float3 anchor = lerp(posW, pivotW, isLeaf);
    float gust = FF_Gust(anchor);

    float3 local = posW - pivotW;
    // a mesh baked without billboard data has zero axes; skip it instead of collapsing the cards
    float bb = step(0.5, Billboard) * isLeaf * step(0.5, dot(Right.xyz, Right.xyz));
    float3 rightW = TransformObjectToWorldDir(Right.xyz);
    float3 upW = TransformObjectToWorldDir(Up.xyz);
    float a = dot(local, rightW);
    float b = dot(local, upW);
    float3 camRight = UNITY_MATRIX_V[0].xyz;
    float3 camUp = UNITY_MATRIX_V[1].xyz;
    float3 yawRight = normalize(float3(camRight.x, 0.0, camRight.z) + float3(1e-5, 0.0, 0.0));
    float yawOnly = step(1.5, Billboard);
    float3 billboarded = lerp(camRight, yawRight, yawOnly) * a + lerp(camUp, upW, yawOnly) * b;
    local = lerp(local, billboarded, bb);

    float3 axis = normalize(cross(float3(0, 1, 0), wind) + float3(1e-4, 0, 0));
    float t = _Time.y * FlutterSpeed + Pivot.w * 6.2831853;
    float angle = (sin(t) + 0.5 * sin(t * 2.17 + 1.3)) * Flutter * (0.6 + strength * gust) * isLeaf;
    posW = pivotW + lerp(local, FF_Rotate(local, axis, angle), isLeaf);

    float sway = strength * gust * VColor.g * Sway;
    posW += wind * sway;
    posW.y -= abs(sway) * 0.2;

    OutPos = TransformWorldToObject(posW);
}

void FoliageVertex_half(half3 PosOS, half4 VColor, half4 Pivot, half Flutter, half FlutterSpeed, half Sway, half Billboard, half4 Right, half4 Up, out half3 OutPos)
{
    float3 o;
    FoliageVertex_float(PosOS, VColor, Pivot, Flutter, FlutterSpeed, Sway, Billboard, Right, Up, o);
    OutPos = o;
}

// Trunk and branches: sway only, same wind curve as the foliage so the crown stays attached.
void FoliageBarkVertex_float(float3 PosOS, float4 VColor, float Sway, out float3 OutPos)
{
    float3 posW = TransformObjectToWorld(PosOS);
    float sway = _FF_WindDir.w * FF_Gust(posW) * VColor.g * Sway;
    posW += _FF_WindDir.xyz * sway;
    posW.y -= abs(sway) * 0.2;
    OutPos = TransformWorldToObject(posW);
}

void FoliageBarkVertex_half(half3 PosOS, half4 VColor, half Sway, out half3 OutPos)
{
    float3 o;
    FoliageBarkVertex_float(PosOS, VColor, Sway, o);
    OutPos = o;
}

// Stylized shading (all off at 0): ShadowAmount blends the side facing away from the sun into ShadowColor (subsurface) over a
// soft band of width ShadowSoftness; HueShift warms the lit side and cools the shadow side; RimStrength adds a fresnel rim on
// the clump edges; TreeVariation shifts hue and brightness per tree from its world position.
// Blob look: Bands (0 = off, 2..5) steps the light into that many flat tones; SkyTone blends the smooth normal's up component into the
// gradient height so the dark-to-light ramp follows the blob; Solid (>0.5) ignores the texture colour and alpha. SolidShape > 0.5 then cuts each card to a
// round blob (1) or a ragged pointed leaf (2) with a noise-eroded edge (UV assumed to span 0..1), otherwise the card stays a flat opaque quad.
// ContinuousColor (>0.5) replaces the per-card random (gradient jitter, tint, brightness) with world-space noise so neighbouring cards blend, reads VColor.R as
// the vertex's height inside its own blob, and lets GrainAmount add fine world-space breakup to colour and to the light before it is banded.
void FoliageSurface_float(UnityTexture2D Tex, float2 UV, float4 VColor, float4 Pivot, float4 Bottom, float4 Top, float Bias, float2 Scroll, float Variation, float Jitter, float3 NormalWS, float Wrap, float3 PosWS, float4 TransColor, float Trans, float TransFocus,
    float4 ShadowColor, float ShadowAmount, float ShadowSoftness, float4 RimColor, float RimStrength, float RimPower, float HueShift, float TreeVariation,
    float4 PatchColor, float PatchAmount, float PatchScale, UnityTexture2D FillTex, float FillScale, float FillImpact, float Bands, float SkyTone, float Solid, float SolidShape, float ContinuousColor, float GrainAmount, out float3 Color, out float Alpha, out float3 Emission)
{
    float2 uv = UV + Scroll * _Time.y * (0.5 + VColor.a);
    float4 tex = SAMPLE_TEXTURE2D(Tex.tex, Tex.samplerstate, uv);
    if (Solid > 0.5)
    {
        tex = float4(1.0, 1.0, 1.0, 1.0);
        if (SolidShape > 1.5)
        {
            // pointed leaf outline with a noise-ragged edge
            float2 q = UV * 2.0 - 1.0;
            float d = pow(max(1.0 - pow(abs(q.y), 1.7), 0.0), 0.7) - abs(q.x) * 1.05 + (FF_Noise(UV * 7.0 + Pivot.w * 19.0) - 0.5) * 0.55;
            tex.a = saturate(d / (fwidth(d) * 1.5 + 0.02));
        }
        else if (SolidShape > 0.5)
        {
            float d = 0.95 - length(UV * 2.0 - 1.0) - FF_Noise(UV * 7.0 + Pivot.w * 19.0) * 0.3;
            tex.a = saturate(d / (fwidth(d) * 1.5 + 0.05));
        }
    }
    float cont = step(0.5, ContinuousColor) * step(0.5, VColor.b);
    // world-space noise: coarse for tint and gradient, fine for grain (the lighting term below reuses grain)
    float coarse = FF_Noise(PosWS.xz * 1.3 + PosWS.y * float2(0.37, 0.61));
    float coarse2 = FF_Noise(PosWS.xz * 0.8 + PosWS.y * float2(0.71, 0.29) + 17.0);
    float grain = FF_Noise(PosWS.xz * 6.0 + PosWS.y * float2(1.9, 2.3)) - 0.5;
    // gradient height, nudged per card so the dark-to-light ramp does not read as a clean gradient
    float cardRand = lerp(frac(sin(Pivot.w * 91.7 + 3.1) * 43758.5453), coarse, cont);
    float h = saturate(VColor.r + (cardRand - 0.5) * Jitter * step(0.5, VColor.b));
    if (SkyTone > 0.0) h = lerp(h, normalize(NormalWS).y * 0.5 + 0.5, SkyTone);
    h = pow(h, max(Bias, 0.01));
    // leaf vertex colour B = 0.5 + 0.5 * baked occlusion (wood is 0, old meshes 1 = unoccluded)
    float ao = lerp(1.0, saturate((VColor.b - 0.5) * 2.0), step(0.5, VColor.b));
    float3 baseCol = lerp(Bottom.rgb, Top.rgb, h);
    // per-card random (A): shift toward the yellow-warm side and vary brightness; wood carries no leaf flag, so it is untouched
    float leaf = step(0.5, VColor.b);
    float r = lerp(frac(VColor.a * 7.31 + 0.17), coarse, cont);
    float3 warm = baseCol * float3(1.25, 1.05, 0.55);
    float3 cool = baseCol * float3(0.8, 0.95, 1.15);
    float3 shifted = r < 0.5 ? lerp(baseCol, cool, (0.5 - r) * 2.0) : lerp(baseCol, warm, (r - 0.5) * 2.0);
    shifted *= 1.0 + (lerp(frac(VColor.a * 13.7), coarse2, cont) - 0.5) * 0.4;
    baseCol = lerp(baseCol, shifted, saturate(Variation) * leaf);
    // dry / autumn patches: world-space noise blends a second colour over the leaves (0 amount = off)
    float patch = smoothstep(1.0 - PatchAmount - 0.12, 1.0 - PatchAmount + 0.12, FF_Noise(PosWS.xz * PatchScale)) * step(0.001, PatchAmount) * leaf;
    baseCol = lerp(baseCol, PatchColor.rgb, patch * PatchColor.a);
    Color = baseCol * tex.rgb * ao * (1.0 + grain * GrainAmount * cont);
    Alpha = tex.a;

    // painterly fill: one texture tiled in world space across all cards (white when empty, so no effect)
    float3 fill = SAMPLE_TEXTURE2D(FillTex.tex, FillTex.samplerstate, float2(PosWS.x + PosWS.z, PosWS.y) * FillScale).rgb;
    Color *= lerp(1.0, saturate(dot(fill, 0.3333) * 1.4), FillImpact * leaf);

    // wrap lighting: the lit side of URP's Lambert ends at N.L = 0, so add the missing part of the remapped (N.L * 0.5 + 0.5) term
    // as emission. Unshadowed on purpose, which keeps leaf cards from shadowing each other.
    Emission = 0;
#ifndef SHADERGRAPH_PREVIEW
    Light mainLight = GetMainLight();
    float3 n = normalize(NormalWS);
    float ndl = dot(n, mainLight.direction) + grain * GrainAmount * cont * 0.5;

    // three tones: lit base colour, a soft band, then the subsurface colour on the far side. The subsurface colour also goes
    // into emission, so the shadow side reads as that colour instead of going dark.
    float lit = smoothstep(-ShadowSoftness, ShadowSoftness, ndl);
    float ndlWrap = ndl;
    // bands: quantise the remapped N.L into flat steps; each edge ramps over a fraction of the softness plus one pixel so it stays crisp without aliasing
    if (Bands > 1.5)
    {
        float x = min(ndl * 0.5 + 0.5, 0.9999) * Bands;
        float w = ShadowSoftness * 0.15 + fwidth(x);
        lit = min(floor(x) + smoothstep(1.0 - w, 1.0, frac(x)), Bands - 1.0) / (Bands - 1.0);
        ndlWrap = lit * 2.0 - 1.0;
    }
    float3 shadowTone = FF_Hue(ShadowColor.rgb * tex.rgb * ao, HueShift);
    Color = FF_Hue(Color, HueShift * (1.0 - 2.0 * lit));
    float shade = (1.0 - lit) * saturate(ShadowAmount);
    Color = lerp(Color, shadowTone, shade);
    Emission = shadowTone * mainLight.color * shade * 0.6;

    Emission += Color * mainLight.color * max(0.0, saturate(ndlWrap * 0.5 + 0.5) - saturate(ndlWrap)) * Wrap;

    // translucency: light shining through the card toward the eye. The light direction is bent toward the normal (scatter),
    // then tightened with a power. Unshadowed, so leaves glow even where the crown above them shades them.
    float3 view = GetWorldSpaceNormalizeViewDir(PosWS);
    float through = pow(saturate(dot(view, -normalize(mainLight.direction + n * 0.3))), max(TransFocus, 1.0));
    Emission += Color * TransColor.rgb * mainLight.color * through * Trans;

    // gust tint: the travelling gust wave warms the leaves it passes over. Skipped when there is no tint or no wind.
    [branch] if (_FF_GustColor.a * _FF_WindDir.w > 0.0)
    {
        float gust = saturate(FF_Gust(PosWS) - 0.3);
        Emission += Color * _FF_GustColor.rgb * _FF_GustColor.a * (gust * gust * gust) * saturate(_FF_WindDir.w * 4.0) * leaf;
    }

    // fresnel rim on the (clump-rounded) normals; abs so both card faces get it. Stronger on the lit side.
    float rim = pow(1.0 - abs(dot(n, view)), max(RimPower, 0.5)) * RimStrength;
    Emission += RimColor.rgb * mainLight.color * rim * (0.35 + 0.65 * lit);
#endif

    // per-tree variation from the object's world position, so copies of one prefab differ without extra materials
    float3 origin = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
    float treeRand = frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453);
    float turns = (treeRand - 0.5) * 0.12 * TreeVariation;
    float bright = 1.0 + (frac(treeRand * 7.13) - 0.5) * 0.35 * TreeVariation;
    Color = FF_Hue(Color, turns) * bright;
    Emission = FF_Hue(Emission, turns) * bright;
}

void FoliageSurface_half(UnityTexture2D Tex, half2 UV, half4 VColor, half4 Pivot, half4 Bottom, half4 Top, half Bias, half2 Scroll, half Variation, half Jitter, half3 NormalWS, half Wrap, half3 PosWS, half4 TransColor, half Trans, half TransFocus,
    half4 ShadowColor, half ShadowAmount, half ShadowSoftness, half4 RimColor, half RimStrength, half RimPower, half HueShift, half TreeVariation,
    half4 PatchColor, half PatchAmount, half PatchScale, UnityTexture2D FillTex, half FillScale, half FillImpact, half Bands, half SkyTone, half Solid, half SolidShape, half ContinuousColor, half GrainAmount, out half3 Color, out half Alpha, out half3 Emission)
{
    float3 c, e;
    float a;
    FoliageSurface_float(Tex, UV, VColor, Pivot, Bottom, Top, Bias, Scroll, Variation, Jitter, NormalWS, Wrap, PosWS, TransColor, Trans, TransFocus,
        ShadowColor, ShadowAmount, ShadowSoftness, RimColor, RimStrength, RimPower, HueShift, TreeVariation, PatchColor, PatchAmount, PatchScale, FillTex, FillScale, FillImpact, Bands, SkyTone, Solid, SolidShape, ContinuousColor, GrainAmount, c, a, e);
    Color = c;
    Alpha = a;
    Emission = e;
}

// Bark UVs come from the mesh (u around the tube, v along it in metres), so the texture rides the wood when it sways.
// u is repeated a whole number of times so the seam at u = 0/1 stays invisible.
float2 FF_BarkUV(float2 uv, float tiling)
{
    return float2(uv.x * max(1.0, round(3.0 * tiling)), uv.y * tiling);
}

// Bark: monochrome texture tinted by a height gradient, with moss growing on upward-facing surfaces.
// The moss mask uses the world-space normal; it follows the sway only through the normal, never the texture.
void FoliageBarkSurface_float(UnityTexture2D Tex, UnityTexture2D MossTex, float2 UV, float3 NormalWS, float4 VColor,
    float4 Bottom, float4 Top, float Tiling, float4 MossColor, float MossAmount, float MossSharpness, float MossTiling, out float3 Color)
{
    float3 bark = lerp(Bottom.rgb, Top.rgb, saturate(VColor.r)) * SAMPLE_TEXTURE2D(Tex.tex, Tex.samplerstate, FF_BarkUV(UV, Tiling)).rgb;

    float mossTex = SAMPLE_TEXTURE2D(MossTex.tex, MossTex.samplerstate, FF_BarkUV(UV, MossTiling)).r;
    float amount = saturate(MossAmount);
    float threshold = lerp(1.05, -0.35, amount);
    float mask = saturate((normalize(NormalWS).y - threshold + (mossTex - 0.5) * 0.5) * MossSharpness) * saturate(amount * 8.0);
    Color = lerp(bark, MossColor.rgb * lerp(0.7, 1.2, mossTex), mask);
}

void FoliageBarkSurface_half(UnityTexture2D Tex, UnityTexture2D MossTex, half2 UV, half3 NormalWS, half4 VColor,
    half4 Bottom, half4 Top, half Tiling, half4 MossColor, half MossAmount, half MossSharpness, half MossTiling, out half3 Color)
{
    float3 c;
    FoliageBarkSurface_float(Tex, MossTex, UV, NormalWS, VColor, Bottom, Top, Tiling, MossColor, MossAmount, MossSharpness, MossTiling, c);
    Color = c;
}

#endif
