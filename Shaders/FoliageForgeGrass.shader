// Stylized instanced grass. Drawn by GrassField with GPU instancing; each instance is a clump of textured cards.
// Feature switches (wind, texture, gradient, variation, patches, stylized shading, rim, translucency, ground blend,
// distance fade, receive shadows) are uniforms set from the GrassProfile.
Shader "Meowdieval/Foliage Forge/Grass"
{
    Properties
    {
        _BaseMap("Texture (alpha = shape)", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _Bottom("Bottom Color", Color) = (0.12, 0.3, 0.08, 1)
        _Top("Top Color", Color) = (0.45, 0.75, 0.2, 1)
        _Tip("Tip Color", Color) = (0.7, 0.9, 0.35, 1)
        _TipStart("Tip Start", Range(0, 1)) = 0.75
        _TipHue("Tip Hue Variation", Range(0, 1)) = 0
        _GradientBias("Gradient Bias", Range(0.2, 3)) = 1
        _RootAO("Root Darkening", Range(0, 1)) = 0.35
        _Variation("Color Variation", Range(0, 1)) = 0.4
        _PatchColor("Patch Color", Color) = (0.8, 0.7, 0.3, 1)
        _PatchAmount("Patch Amount", Range(0, 1)) = 0.3
        _PatchScale("Patch Scale", Float) = 0.08
        _ShadowColor("Shadow Color (a = amount)", Color) = (0.2, 0.3, 0.45, 1)
        _ShadowSoftness("Shadow Softness", Range(0.01, 1)) = 0.25
        _RimColor("Rim Color", Color) = (0.9, 1, 0.7, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 3
        _RimStrength("Rim Strength", Range(0, 2)) = 0.4
        _TransColor("Translucency Color", Color) = (0.7, 1, 0.3, 1)
        _TransStrength("Translucency Strength", Range(0, 2)) = 0.6
        _GroundColor("Ground Color (a = amount)", Color) = (0.2, 0.3, 0.12, 1)
        _GroundBlend("Ground Blend Height", Range(0.01, 1)) = 0.3
        _WindWeight("Wind Weight", Range(0, 2)) = 1
        _Flutter("Flutter", Range(0, 0.3)) = 0.04
        _FadeStart("Fade Start", Float) = 40
        _FadeEnd("Fade End", Float) = 60
        _NormalUp("Normal Up Blend", Range(0, 1)) = 0.7
        _ShadowDistance("Shadow Distance", Float) = 25
        _LodNear("LOD Near", Float) = 20
        _LodFar("LOD Far", Float) = 45
        _LodMinShare("LOD Min Share", Range(0.05, 1)) = 0.3
        _GroundMap("Ground Colour Map", 2D) = "white" {}
        _GroundMapRect("Ground Map Rect", Vector) = (0, 0, 0.01, 0.01)
        _GroundMapStrength("Ground Map Strength", Range(0, 1)) = 0.6
        _AddLightStrength("Additional Light Strength", Range(0, 2)) = 1
        _TrampleStrength("Trample Strength", Range(0, 2)) = 1
        _GustColor("Gust Color (a = amount)", Color) = (0.95, 0.83, 0.78, 0.3)
        _GustSpeed("Gust Speed", Range(0, 3)) = 0.6
        _GustFrequency("Gust Frequency", Range(0, 1)) = 0.2
        _GustIntensity("Gust Intensity", Range(0, 1)) = 0.3
        _PatchBlur("Patch Blurriness", Range(0, 1)) = 0.24
        _PatchOffset("Patch Offset", Vector) = (0, 0, 0, 0)
        _LightContrib("Light Contribution", Range(0, 1)) = 1
        _ShadowStrength("Shadow Strength", Range(0, 1)) = 1
        [HideInInspector] _AlphaToMask("Alpha To Coverage", Float) = 0
        _Use("Wind, Texture, Gradient, Variation", Vector) = (1, 1, 1, 1)
        _Use2("Patches, Stylized, Rim, Translucency", Vector) = (0, 1, 0, 0)
        _Use3("Ground, Fade, Shadows, Ground Map", Vector) = (1, 1, 0, 0)
        _Use4("Alpha To Coverage, Additional Lights, Trample, Smooth LOD", Vector) = (0, 0, 0, 1)
        _Use5("Gusts, Baked Origins", Vector) = (0, 0, 0, 0)
        _Use6("Wind Texture, Trample Map, Trail Tint, Light Ramp", Vector) = (0, 0, 0, 0)
        _Use7("Perspective Correction", Vector) = (0, 0, 0, 0)
        _Use8("GPU Blades (set by GrassGpuField)", Vector) = (0, 0, 0, 0)
        _LightRamp("Light Ramp (x = light 0..1)", 2D) = "white" {}
        _TrailTop("Trail Top Color", Color) = (0.7, 0.62, 0.3, 1)
        _TrailBottom("Trail Bottom Color", Color) = (0.25, 0.2, 0.08, 1)
        _TrailStrength("Trail Strength", Range(0, 1)) = 0.6
        _PerspectiveStrength("Perspective Strength", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #define FF_GRASS_GPU 1
            #include "FoliageForgeGrass.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 inst : TEXCOORD3;
                float fog : TEXCOORD4;
            };

            Varyings Vert(Attributes i FFG_VERT_ARGS)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                FFG_BEGIN_VERT
                Varyings o;
                float3 posWS = FFG_DisplaceN(i.positionOS.xyz, i.normalOS, i.uv, i.uv1, o.inst);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.uv = i.uv;
                // Grass shades as if it faced mostly up, which keeps a lawn soft instead of striped by card orientation.
                o.normalWS = normalize(lerp(FFG_WorldNormal(i.normalOS), float3(0, 1, 0), _NormalUp));
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float4 tex = float4(1, 1, 1, 1);
                [branch] if (_Use.y > 0.5) tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                else tex.a = FFG_Blade(i.uv);
                // alpha to coverage: the edge becomes a one-pixel coverage ramp (smooth with MSAA, mip-thinning compensated)
                float coverage = 1.0;
                [branch] if (_Use4.x > 0.5)
                {
                    coverage = saturate(FFG_Coverage(tex.a));
                    clip(coverage - 0.001);
                }
                else clip(tex.a - _Cutoff);

                float h = saturate(i.uv.y);
                float rnd = i.inst.z;
                float3 col = _Top.rgb;
                [branch] if (_Use.z > 0.5)
                {
                    col = lerp(_Bottom.rgb, _Top.rgb, pow(h, _GradientBias));
                    float3 tip = FFG_Hue(_Tip.rgb, (frac(rnd * 7.31) - 0.5) * _TipHue);
                    col = lerp(col, tip, smoothstep(_TipStart, min(_TipStart + 0.15, 1.0), h) * _Tip.a);
                    col *= lerp(1.0 - _RootAO, 1.0, saturate(h * 2.0));
                }
                col *= tex.rgb;

                [branch] if (_Use.w > 0.5)
                {
                    col = FFG_Hue(col, (frac(rnd * 13.7) - 0.5) * 0.08 * _Variation);
                    col *= 1.0 + (frac(rnd * 5.13) - 0.5) * 0.5 * _Variation;
                }
                [branch] if (_Use2.x > 0.5)
                {
                    float p = smoothstep(1.0 - _PatchAmount - _PatchBlur * 0.5, 1.0 - _PatchAmount + _PatchBlur * 0.5, FFG_Noise((i.inst.xy + _PatchOffset) * _PatchScale));
                    col = lerp(col, _PatchColor.rgb, p * _PatchColor.a);
                }
                [branch] if (_Use3.x > 0.5)
                    col = lerp(col, _GroundColor.rgb, (1.0 - smoothstep(0.0, _GroundBlend, h)) * _GroundColor.a);

                [branch] if (_Use3.w > 0.5)
                {
                    // tint toward the ground colour under the clump, strongest at the roots
                    float2 muv = saturate((i.inst.xy - _GroundMapRect.xy) * _GroundMapRect.zw);
                    float3 ground = SAMPLE_TEXTURE2D_LOD(_GroundMap, sampler_GroundMap, muv, 0).rgb;
                    col = lerp(col, ground * dot(col, 0.3333) * 2.0, _GroundMapStrength * (1.0 - h * 0.5));
                }

                [branch] if (_Use6.z > 0.5)
                    col = FFG_Trail(col, i.inst.xy, h);
                [branch] if (_Use5.x > 0.5)
                    col += _GustColor.rgb * length(col) * _GustColor.a * i.inst.w;
                Light light = GetMainLight();
                // MainLightShadow fades out toward the shadow distance; skipped entirely when the feature is off
                [branch] if (_Use3.z > 0.5)
                    light.shadowAttenuation = MainLightShadow(TransformWorldToShadowCoord(i.positionWS), i.positionWS, half4(1, 1, 1, 1), _MainLightOcclusionProbes);
                float3 n = normalize(i.normalWS);
                float ndl = dot(n, light.direction);
                float atten = lerp(1.0, light.shadowAttenuation, _ShadowStrength) * light.distanceAttenuation;
                float3 ambient = SampleSH(n);
                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);

                float3 albedo = col;
                float3 direct;
                float lit = saturate(ndl * 0.5 + 0.5) * atten;
                [branch] if (_Use2.y > 0.5)
                {
                    lit = smoothstep(-_ShadowSoftness, _ShadowSoftness, ndl) * atten;
                    albedo = lerp(col, col * _ShadowColor.rgb * 1.6, (1.0 - lit) * _ShadowColor.a);
                }
                direct = light.color * lit;
                [branch] if (_Use6.w > 0.5)
                    direct = light.color * FFG_Ramp(lit);
            #if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
                [branch] if (_Use4.y > 0.5)
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light extra = GetAdditionalLight(lightIndex, i.positionWS);
                        direct += extra.color * (extra.distanceAttenuation * saturate(dot(n, extra.direction) * 0.5 + 0.5) * _AddLightStrength);
                    LIGHT_LOOP_END
                }
            #endif
                // light contribution 0 = flat painted colour (Quibli look), 1 = fully lit
                float3 result = albedo * lerp(float3(1.0, 1.0, 1.0), ambient + direct, _LightContrib);

                [branch] if (_Use2.z > 0.5)
                    result += _RimColor.rgb * light.color * pow(1.0 - saturate(dot(n, view)), _RimPower) * _RimStrength * h * (0.35 + 0.65 * lit);
                [branch] if (_Use2.w > 0.5)
                {
                    // shadowed, so blades behind a building do not glow
                    float back = saturate(dot(view, -light.direction));
                    back *= back;
                    result += col * _TransColor.rgb * light.color * (back * back * atten) * _TransStrength * h;
                }

                return float4(MixFog(result, i.fog), coverage);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define FF_GRASS_GPU 1
            #include "FoliageForgeGrass.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes i FFG_VERT_ARGS)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                FFG_BEGIN_VERT
                Varyings o;
                float4 inst;
                float3 posWS = FFG_DisplaceN(i.positionOS.xyz, i.normalOS, i.uv, i.uv1, inst);
                float3 normalWS = FFG_WorldNormal(i.normalOS);
                // far clumps cast nothing: cheaper than alpha-testing a shadow nobody can read. Measured from the clump origin
                // (inst.xy) so a whole clump collapses together instead of tearing triangles at the threshold.
                bool tooFar = distance(_WorldSpaceCameraPos.xz, inst.xy) > _ShadowDistance;
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - posWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE * cs.w);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE * cs.w);
            #endif
                o.positionCS = tooFar ? float4(0, 0, 0, 0) : cs;
                o.uv = i.uv;
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                FFG_Clip(FFG_Alpha(i.uv));
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #define FF_GRASS_GPU 1
            #include "FoliageForgeGrass.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes i FFG_VERT_ARGS)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                FFG_BEGIN_VERT
                Varyings o;
                float4 inst;
                o.positionCS = TransformWorldToHClip(FFG_DisplaceN(i.positionOS.xyz, i.normalOS, i.uv, i.uv1, inst));
                o.uv = i.uv;
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                FFG_Clip(FFG_Alpha(i.uv));
                return i.positionCS.z; // URP renders the depth texture into R when it is a colour target
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #define FF_GRASS_GPU 1
            #include "FoliageForgeGrass.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes i FFG_VERT_ARGS)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                FFG_BEGIN_VERT
                Varyings o;
                float4 inst;
                o.positionCS = TransformWorldToHClip(FFG_DisplaceN(i.positionOS.xyz, i.normalOS, i.uv, i.uv1, inst));
                o.uv = i.uv;
                o.normalWS = normalize(lerp(FFG_WorldNormal(i.normalOS), float3(0, 1, 0), _NormalUp));
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                FFG_Clip(FFG_Alpha(i.uv));
                float3 n = normalize(i.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(n);
                return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
            #else
                return half4(n, 0.0);
            #endif
            }
            ENDHLSL
        }

        // Trample map update, drawn with Graphics.Blit by GrassTrampleRT (never by a camera: the LightMode matches no URP pass).
        // Reads the previous map shifted by the window move, fades it, then stamps the pushers. rg = push xz, b = trail.
        Pass
        {
            Name "TrampleSplat"
            Tags { "LightMode" = "FFTrampleSplat" }
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SplatVert
            #pragma fragment SplatFrag
            #include "FoliageForgeGrass.hlsl"

            #define FF_MAX_SPLAT 64
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _FF_SplatRect;       // xy = world xz of the new window corner, z = window size (m), w = resolution (px)
            float4 _FF_SplatParams;     // x = fade this frame, y = pusher count, zw = uv shift into the previous map
            float4 _FF_SplatPushers[FF_MAX_SPLAT];

            struct SplatV
            {
                float4 positionCS : SV_POSITION;
            };

            SplatV SplatVert(float4 positionOS : POSITION, float2 uv : TEXCOORD0)
            {
                SplatV o;
                o.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                return o;
            }

            float4 SplatFrag(SplatV i) : SV_Target
            {
                // pixel position, not the quad uv, so the map is written the way it is sampled whatever the API's render target flip
                float2 uv = i.positionCS.xy / _FF_SplatRect.w;
                float2 p = _FF_SplatRect.xy + uv * _FF_SplatRect.z;
                float2 puv = uv + _FF_SplatParams.zw;
                float inside = step(0.0, puv.x) * step(puv.x, 1.0) * step(0.0, puv.y) * step(puv.y, 1.0);
                float4 cur = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, puv, 0) * (_FF_SplatParams.x * inside);
                int count = min((int)_FF_SplatParams.y, FF_MAX_SPLAT);
                [loop] for (int k = 0; k < count; k++)
                {
                    float4 tr = _FF_SplatPushers[k];
                    float2 away = p - tr.xz;
                    float dist = length(away);
                    if (dist >= tr.w) continue;
                    float f = saturate(1.0 - dist / max(tr.w, 0.01));
                    float2 v = away / max(dist, 1e-3) * (f * f);
                    if (dot(v, v) > dot(cur.xy, cur.xy)) cur.xy = v;
                    cur.z = max(cur.z, f);
                }
                // half floats never reach zero by repeated fading, so cut the tail
                cur.xy *= step(0.004, length(cur.xy));
                cur.z *= step(0.004, cur.z);
                cur.w = 0.0;
                return cur;
            }
            ENDHLSL
        }
    }
}
