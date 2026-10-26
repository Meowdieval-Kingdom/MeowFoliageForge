#ifndef FF_GRASS_INCLUDED
#define FF_GRASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// GPU placement path (GrassGpuField): passes that set FF_GRASS_GPU 1 before this include (and use target 4.5) can read their blade
// from _FF_Blades by instance id when _Use8.x is on. Other passes compile without the buffer.
#ifndef FF_GRASS_GPU
#define FF_GRASS_GPU 0
#endif

// Same globals FoliageWind writes for the trees, so grass and trees share one wind.
float4 _FF_WindDir;     // xyz = world direction, w = strength
float4 _FF_WindParams;  // x = speed, y = gust frequency, z = gust contrast, w = entropy

// Pushers (GrassTrampler): xyz = world position, w = radius. Count is how many entries are live.
#define FF_MAX_TRAMPLE 16
float4 _FF_Trample[FF_MAX_TRAMPLE];
float _FF_TrampleCount;

// Trample map (GrassTrampleRT): a camera-following window of pusher splats. rg = push direction xz scaled by strength, b = trail.
// _FF_TrampleRect: xy = world xz of the window corner, zw = 1 / window size. Wind noise: tiling value noise, FF_WIND_CELLS lattice cells per tile.
#define FF_WIND_CELLS 16.0
float4 _FF_TrampleRect;
TEXTURE2D(_FF_TrampleRT);
SAMPLER(sampler_FF_TrampleRT);
TEXTURE2D(_FF_WindNoise);
SAMPLER(sampler_FF_WindNoise);

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_GroundMap);
SAMPLER(sampler_GroundMap);
TEXTURE2D(_LightRamp);
SAMPLER(sampler_linear_clamp);

// Feature switches are plain uniforms (not keywords) so they work on runtime-made materials in builds and a switched-off
// feature costs one coherent branch.
// _Use  = wind, texture, gradient, variation       _Use2 = patches, stylized, rim, translucency
// _Use3 = ground blend, distance fade, receive shadows, ground map
// _Use4 = alpha to coverage, additional lights, trample, smooth distance LOD
// _Use5 = gusts, baked origins     _Use6 = wind texture, trample map, trail tint, light ramp     _Use7 = perspective correction
CBUFFER_START(UnityPerMaterial)
    float4 _Bottom, _Top, _Tip;
    float4 _PatchColor, _ShadowColor, _RimColor, _TransColor, _GroundColor;
    float4 _GroundMapRect;      // xy = world xz of the map's corner, zw = 1 / world size
    float4 _Use, _Use2, _Use3, _Use4;
    float4 _Use5;               // x = gusts
    float4 _Use6, _Use7;
    float4 _Use8;               // x = GPU blades (read _FF_Blades by instance id instead of the instance matrix)
    float4 _TrailTop, _TrailBottom;
    float _TrailStrength, _PerspectiveStrength;
    float4 _GustColor;          // rgb = colour lift, a = amount
    float2 _PatchOffset;
    float _GustSpeed, _GustFrequency, _GustIntensity, _PatchBlur, _LightContrib, _ShadowStrength;
    float _Cutoff, _TipStart, _TipHue, _GradientBias, _RootAO, _Variation;
    float _PatchAmount, _PatchScale, _ShadowSoftness, _RimPower, _RimStrength, _TransStrength;
    float _GroundBlend, _WindWeight, _Flutter, _FadeStart, _FadeEnd;
    float _NormalUp, _ShadowDistance, _LodNear, _LodFar, _LodMinShare;
    float _GroundMapStrength, _AddLightStrength, _TrampleStrength;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    float4 uv1 : TEXCOORD1;     // baked patches: xyz = clump origin (object space), w = clump height
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

#if FF_GRASS_GPU
// Mirrors Blade in GrassPlacement.compute (32 bytes); GrassGpuMath.BladeStride must match.
struct FFBlade
{
    float3 pos;
    float yaw;      // radians about +Y
    float height;   // clump scale y
    float width;    // clump scale xz
    float seed;     // 0..1, feeds per-clump colour variation
    float type;
};
StructuredBuffer<FFBlade> _FF_Blades;
static uint FFG_BladeId = 0;
#define FFG_VERT_ARGS , uint ffInstanceID : SV_InstanceID
#define FFG_BEGIN_VERT FFG_BladeId = ffInstanceID;
#else
#define FFG_VERT_ARGS
#define FFG_BEGIN_VERT
#endif
static float2 FFG_BladeSC = float2(0.0, 1.0); // sin, cos of the GPU blade's yaw

float FFG_Hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

uint FFG_Pcg(uint v)
{
    uint s = v * 747796405u + 2891336453u;
    uint w = ((s >> ((s >> 28) + 4u)) ^ s) * 277803737u;
    return (w >> 22) ^ w;
}

float FFG_Noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(FFG_Hash(i), FFG_Hash(i + float2(1, 0)), f.x), lerp(FFG_Hash(i + float2(0, 1)), FFG_Hash(i + float2(1, 1)), f.x), f.y);
}

// Gust noise: one fetch from the shared tiling texture, or the hashed value noise when the switch is off. Same scale either way.
float FFG_WindNoise(float2 p)
{
    float n;
    [branch] if (_Use6.x > 0.5) n = SAMPLE_TEXTURE2D_LOD(_FF_WindNoise, sampler_FF_WindNoise, p * (1.0 / FF_WIND_CELLS), 0).r;
    else n = FFG_Noise(p);
    return n;
}

// Same travelling gust wave as the trees (FoliageForge.hlsl FF_Gust).
float FFG_Gust(float3 worldPos)
{
    float phase = dot(worldPos.xz, _FF_WindDir.xz) * _FF_WindParams.y - _Time.y * _FF_WindParams.x + (FFG_WindNoise(worldPos.xz * 0.15) - 0.5) * _FF_WindParams.w;
    float wave = sin(phase) * 0.5 + 0.5;
    wave += (sin(phase * 2.3 + worldPos.z * 0.35) * 0.5 + 0.5) * 0.35;
    return lerp(1.0, wave, _FF_WindParams.z);
}

// Hue rotation around the grey axis, in turns.
float3 FFG_Hue(float3 c, float turns)
{
    float s, k;
    sincos(turns * 6.2831853, s, k);
    float3 axis = float3(0.57735027, 0.57735027, 0.57735027);
    return max(0.0, c * k + cross(axis, c) * s + axis * dot(axis, c) * (1.0 - k));
}

// Three tapered blades across the card, used when the texture feature is off.
float FFG_Blade(float2 uv)
{
    float x = frac(uv.x * 3.0) - 0.5;
    return step(0.0, (1.0 - uv.y * 0.95) - abs(x) * 2.0);
}

// Cutout shape: alpha from the sprite, or plain blades.
float FFG_Alpha(float2 uv)
{
    float a;
    [branch] if (_Use.y > 0.5) a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a;
    else a = FFG_Blade(uv);
    return a;
}

// Alpha rescaled so a mip-thinned edge keeps its coverage: > 0.5 means "inside" the cutoff, and the ramp is one pixel wide.
float FFG_Coverage(float a)
{
    return (a - _Cutoff) / max(fwidth(a), 1e-4) + 0.5;
}

// The forward pass's discard, so depth, depth-normals and shadows cover exactly the pixels the colour pass draws.
void FFG_Clip(float a)
{
    [branch] if (_Use4.x > 0.5) clip(FFG_Coverage(a) - 0.001);
    else clip(a - _Cutoff);
}

// Quibli-style gust: sharp bands travelling along the wind, broken up by slow noise. 0..1.
float FFG_GustBand(float2 xz)
{
    float2 dir = _FF_WindDir.xz;
    dir = dot(dir, dir) > 1e-4 ? normalize(dir) : float2(1.0, 0.0);
    float band = pow(1.0 - abs(sin(dot(xz, dir) * _GustFrequency - _Time.y * _GustSpeed * 1.5)), 3.0);
    float mask = smoothstep(0.2, 0.9, FFG_WindNoise(xz * 0.2 - dir * _Time.y * _GustSpeed));
    return band * mask;
}

// Trample map at a world xz: xy = push (already scaled by strength), z = trail, all faded to zero toward the window edge and outside it.
float3 FFG_TrampleMap(float2 xz)
{
    float2 uv = (xz - _FF_TrampleRect.xy) * _FF_TrampleRect.zw;
    float2 e = abs(uv * 2.0 - 1.0);
    float fade = saturate((1.0 - max(e.x, e.y)) * 8.0);
    return SAMPLE_TEXTURE2D_LOD(_FF_TrampleRT, sampler_FF_TrampleRT, uv, 0).xyz * fade;
}

// Colour of a flattened trail at height h (0 root .. 1 tip) laid over col; xz = clump origin.
float3 FFG_Trail(float3 col, float2 xz, float h)
{
    float trail = FFG_TrampleMap(xz).z * _TrailStrength;
    return lerp(col, lerp(_TrailBottom.rgb, _TrailTop.rgb, h), saturate(trail));
}

// Light response ramp: lit 0..1 in, light multiplier out. Clamps how bright the grass can get.
float3 FFG_Ramp(float lit)
{
    return SAMPLE_TEXTURE2D_LOD(_LightRamp, sampler_linear_clamp, float2(saturate(lit), 0.5), 0).rgb;
}

// Moves one vertex: smooth distance LOD (random clumps drop out and the rest widen), distance fade, wind, trample.
// uv.y is height 0 (root) to 1 (tip) and doubles as the bend weight. inst = xy origin.xz, z per-instance random.
// normalOS only feeds perspective correction; (0,0,0) skips it.
float3 FFG_DisplaceN(float3 positionOS, float3 normalOS, float2 uv, float4 uv1, out float4 inst)
{
    // a baked patch holds many clumps in one mesh, so each vertex carries its own clump's origin; instanced clumps use the pivot
    bool baked = _Use5.y > 0.5;
    float3 origin = TransformObjectToWorld(baked ? uv1.xyz : float3(0.0, 0.0, 0.0));
    float3 posWS = TransformObjectToWorld(positionOS);
    float3 normalWS = TransformObjectToWorldNormal(normalOS);
    float4x4 o2w = GetObjectToWorldMatrix();
    float scaleY = baked ? uv1.w : length(float3(o2w._m01, o2w._m11, o2w._m21));
    float scaleW = baked ? uv1.w : length(float3(o2w._m00, o2w._m10, o2w._m20));
    float seed = FFG_Hash(origin.xz);
#if FF_GRASS_GPU
    [branch] if (_Use8.x > 0.5)
    {
        // blade from the placement buffer: same clump the matrix path would draw (scale = width, height, width; yaw about +Y)
        FFBlade b = _FF_Blades[FFG_BladeId];
        sincos(b.yaw, FFG_BladeSC.x, FFG_BladeSC.y);
        float3 p = positionOS * float3(b.width, b.height, b.width);
        origin = b.pos;
        posWS = origin + float3(p.x * FFG_BladeSC.y + p.z * FFG_BladeSC.x, p.y, p.z * FFG_BladeSC.y - p.x * FFG_BladeSC.x);
        normalWS = float3(normalOS.x * FFG_BladeSC.y + normalOS.z * FFG_BladeSC.x, normalOS.y, normalOS.z * FFG_BladeSC.y - normalOS.x * FFG_BladeSC.x);
        scaleY = b.height;
        scaleW = b.width;
        seed = b.seed;
    }
#endif
    inst = float4(origin.xz, seed, 0.0);

    float3 local = posWS - origin;
    float d = distance(_WorldSpaceCameraPos, origin);
    [branch] if (_Use4.w > 0.5)
    {
        // share of clumps kept falls from 1 to _LodMinShare between near and far; survivors are a fixed random subset, so nothing flickers
        float share = lerp(1.0, _LodMinShare, smoothstep(_LodNear, max(_LodFar, _LodNear + 0.01), d));
        // integer hash of the origin in millimetres; GrassLod.Keep(x, z) on the CPU is its twin and must change together with this
        uint hx = (uint)(int)floor(origin.x * 1000.0 + 0.5);
        uint hz = (uint)(int)floor(origin.z * 1000.0 + 0.5);
        float keep = (float)(FFG_Pcg(hx ^ FFG_Pcg(hz)) & 0xFFFFFFu) / 16777216.0;
        local *= step(keep, share);
        local.xz *= pow(1.0 / max(share, 0.05), 0.35);
    }
    [branch] if (_Use3.y > 0.5)
        local *= saturate((_FadeEnd - d) / max(_FadeEnd - _FadeStart, 0.01));
    posWS = origin + local;

    // card seen nearly edge-on: open its side vertices toward the camera's right so it keeps some width. Roots stay put.
    [branch] if (_Use7.x > 0.5 && dot(normalOS, normalOS) > 1e-4)
    {
        float2 nh = normalWS.xz;
        float2 toCam = _WorldSpaceCameraPos.xz - origin.xz;
        nh = dot(nh, nh) > 1e-6 ? normalize(nh) : float2(0.0, 1.0);
        toCam = dot(toCam, toCam) > 1e-6 ? normalize(toCam) : float2(0.0, 1.0);
        float graze = 1.0 - abs(dot(nh, toCam));
        float2 right = float2(toCam.y, -toCam.x);
        float halfWidth = 0.5 * scaleW;
        float side = (uv.x - 0.5) * 2.0;
        posWS.xz += right * (side * graze * graze * halfWidth * _PerspectiveStrength * saturate(uv.y * 3.0));
    }

    [branch] if (_Use.x > 0.5)
    {
        float bend = uv.y * uv.y * _WindWeight;
        float push = _FF_WindDir.w * FFG_Gust(origin) * bend;
        posWS += _FF_WindDir.xyz * push;
        posWS.y -= abs(push) * 0.3;
        float t = _Time.y * 3.1 + inst.z * 6.2831853;
        posWS += float3(sin(t), 0.0, cos(t * 1.31)) * _Flutter * bend * (0.4 + _FF_WindDir.w);
    }

    [branch] if (_Use5.x > 0.5)
    {
        // card edges swing further than the middle, and the whole tip lifts a little, so a gust rolls through the lawn
        float s = FFG_GustBand(origin.xz);
        float2 dir = _FF_WindDir.xz;
        dir = dot(dir, dir) > 1e-4 ? normalize(dir) : float2(1.0, 0.0);
        float edge = 0.4 + abs(uv.x - 0.5) * 1.2;
        posWS.xz += dir * (s * uv.y * edge * scaleY * _GustIntensity);
        posWS.y -= s * uv.y * scaleY * _GustIntensity * 0.25;
        inst.w = s;
    }

    // trample map: the clump origin picks one texel, so a whole clump leans together
    [branch] if (_Use6.y > 0.5 && uv.y > 0.001)
    {
        float3 t = FFG_TrampleMap(origin.xz);
        float f = length(t.xy) * _TrampleStrength;
        posWS.xz += t.xy * (_TrampleStrength * uv.y * 0.6);
        posWS.y -= f * uv.y * 0.45 * (posWS.y - origin.y);
    }

    // root vertices don't move (every term is scaled by uv.y), so they skip the loop
    [branch] if (_Use4.z > 0.5 && uv.y > 0.001)
    {
        int count = min((int)_FF_TrampleCount, FF_MAX_TRAMPLE);
        [loop] for (int k = 0; k < count; k++)
        {
            float4 tr = _FF_Trample[k];
            float2 away = origin.xz - tr.xz;
            float dist = length(away);
            if (dist >= tr.w) continue;
            float f = saturate(1.0 - dist / max(tr.w, 0.01));
            f *= f * _TrampleStrength;
            posWS.xz += away / max(dist, 1e-3) * (f * uv.y * 0.6);
            posWS.y -= f * uv.y * 0.45 * (posWS.y - origin.y);
        }
    }
    return posWS;
}

// World normal of a vertex normal; call after FFG_DisplaceN so a GPU blade's yaw is applied.
float3 FFG_WorldNormal(float3 normalOS)
{
    float3 n = TransformObjectToWorldNormal(normalOS);
#if FF_GRASS_GPU
    [branch] if (_Use8.x > 0.5)
        n = float3(normalOS.x * FFG_BladeSC.y + normalOS.z * FFG_BladeSC.x, normalOS.y, normalOS.z * FFG_BladeSC.y - normalOS.x * FFG_BladeSC.x);
#endif
    return n;
}

float3 FFG_Displace(float3 positionOS, float2 uv, float4 uv1, out float4 inst)
{
    return FFG_DisplaceN(positionOS, float3(0.0, 0.0, 0.0), uv, uv1, inst);
}

#endif
