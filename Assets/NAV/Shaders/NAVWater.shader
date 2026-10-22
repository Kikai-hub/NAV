// NAV's water for URP (replaces the flat Lit plane; SUIMONO 2 is built-in-pipeline only, its
// wave normal maps and foam mask are reused here).
//  - colour by depth below the surface (camera depth texture): clear shallows showing the
//    refracted ground (camera opaque texture), deep water fading to Deep Color;
//  - two scrolling wave normal maps in world space (tiling independent of the plane's size);
//  - lit by the main light and the ambient probe, so it darkens with the day/night cycle instead
//    of glowing at night; sun/moon highlight, fresnel reflection of the ambient sky;
//  - foam where the water meets the shore (foam mask red channel) and faint whitecaps (green);
//  - fog like the rest of the scene.
Shader "NAV/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.32, 0.62, 0.62, 1)
        _DeepColor ("Deep Color", Color) = (0.07, 0.26, 0.36, 1)
        _DepthDistance ("Depth To Deep (m)", Float) = 4
        _Clarity ("Shallow Clarity", Range(0, 1)) = 0.75

        [Normal][NoScaleOffset] _WaveNormalA ("Wave Normal A", 2D) = "bump" {}
        [Normal][NoScaleOffset] _WaveNormalB ("Wave Normal B", 2D) = "bump" {}
        _WaveTilingA ("Wave A Tile Size (m)", Float) = 9
        _WaveTilingB ("Wave B Tile Size (m)", Float) = 5
        _WaveSpeed ("Wave Speed", Float) = 0.05
        _WaveStrength ("Wave Strength", Range(0, 2)) = 0.6

        _Refraction ("Refraction", Range(0, 0.1)) = 0.025
        _ReflectionStrength ("Sky Reflection", Range(0, 1)) = 0.6
        _FresnelPower ("Fresnel Power", Float) = 4
        _SpecularPower ("Highlight Sharpness", Float) = 180
        _SpecularStrength ("Highlight Strength", Float) = 1.5

        [NoScaleOffset] _FoamMask ("Foam Mask (R shore, G whitecaps)", 2D) = "black" {}
        _FoamColor ("Foam Color", Color) = (0.92, 0.95, 0.95, 1)
        _FoamDistance ("Shore Foam Width (m)", Float) = 0.9
        _FoamTiling ("Foam Tile Size (m)", Float) = 6
        _WhitecapAmount ("Whitecaps", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            // The shader composes the refracted scene itself, so it writes an opaque result.
            Blend One Zero
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_WaveNormalA); SAMPLER(sampler_WaveNormalA);
            TEXTURE2D(_WaveNormalB); SAMPLER(sampler_WaveNormalB);
            TEXTURE2D(_FoamMask); SAMPLER(sampler_FoamMask);

            // Ripples from things moving in the water (WaterInteraction): x, z, start time
            // (_Time.y clock), strength. Globals, so every water material shares them.
            #define RIPPLE_COUNT 16
            float4 _NAVWaterRipples[RIPPLE_COUNT];
            #define RIPPLE_SPEED 1.4
            #define RIPPLE_LIFE 2.6
            #define RIPPLE_WAVENUMBER 9.0

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthDistance;
                half _Clarity;
                float _WaveTilingA;
                float _WaveTilingB;
                float _WaveSpeed;
                half _WaveStrength;
                half _Refraction;
                half _ReflectionStrength;
                half _FresnelPower;
                half _SpecularPower;
                half _SpecularStrength;
                half4 _FoamColor;
                float _FoamDistance;
                float _FoamTiling;
                half _WhitecapAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.screenPos = ComputeScreenPos(positions.positionCS);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half3 WaveNormal(float2 xz)
            {
                float t = _Time.y * _WaveSpeed;
                half3 a = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveNormalA, sampler_WaveNormalA, xz / _WaveTilingA + float2(t, t * 0.6)), _WaveStrength);
                half3 b = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveNormalB, sampler_WaveNormalB, xz / _WaveTilingB + float2(-t * 0.7, t * 1.1)), _WaveStrength);
                // Whiteout blend, then tangent (x, y) -> world (x, z) for a flat, upward surface.
                half3 n = normalize(half3(a.xy + b.xy, a.z * b.z));
                return normalize(half3(n.x, n.z, n.y));
            }

            // Expanding rings around each ripple source: a few wave crests inside a widening,
            // fading band. Adds their slope to the surface normal and returns a crest highlight.
            half Ripples(float2 xz, inout half3 normalWS)
            {
                half2 slope = 0;
                half crest = 0;
                for (int i = 0; i < RIPPLE_COUNT; i++)
                {
                    float4 ripple = _NAVWaterRipples[i];
                    float age = _Time.y - ripple.z;
                    if (ripple.w <= 0 || age < 0 || age > RIPPLE_LIFE)
                    {
                        continue;
                    }

                    float2 offset = xz - ripple.xy;
                    float dist = length(offset);
                    float x = dist - age * RIPPLE_SPEED;
                    float width = 0.35 + age * 0.35;
                    float envelope = exp(-(x * x) / (width * width));
                    float life = 1 - age / RIPPLE_LIFE;
                    float strength = envelope * life * life * ripple.w;
                    float phase = x * RIPPLE_WAVENUMBER;
                    slope += (offset / max(dist, 0.001)) * (cos(phase) * strength);
                    crest += saturate(sin(phase)) * strength;
                }

                normalWS = normalize(normalWS + half3(slope.x, 0, slope.y) * 0.6);
                return saturate(crest * 0.35);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float surfaceDepth = input.screenPos.w;
                float waterDepth = max(0, LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams) - surfaceDepth);

                half3 normalWS = WaveNormal(input.positionWS.xz);
                half rippleCrest = Ripples(input.positionWS.xz, normalWS);
                half3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                // Refraction: offset the scene lookup by the waves, but not onto things in front
                // of the water (re-check depth at the offset point).
                float depthFade = saturate(waterDepth / _DepthDistance);
                float2 offset = normalWS.xz * _Refraction * saturate(waterDepth);
                float2 refractUV = screenUV + offset;
                float refractedDepth = LinearEyeDepth(SampleSceneDepth(refractUV), _ZBufferParams) - surfaceDepth;
                if (refractedDepth < 0)
                {
                    refractUV = screenUV;
                }
                else
                {
                    depthFade = saturate(refractedDepth / _DepthDistance);
                }

                half3 sceneColor = SampleSceneColor(refractUV);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = mainLight.shadowAttenuation;
                half3 ambient = SampleSH(half3(0, 1, 0));
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = ambient + mainLight.color * (ndotl * shadow);

                // Body colour: shallows let the ground through, deep water is its own colour -
                // both lit, so night water is dark instead of glowing.
                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthFade) * lighting;
                // Light is absorbed with depth, so things just under the surface (a swimmer's body)
                // are clearly underwater rather than looking like they stand in the shallows.
                half seeThrough = _Clarity * (1 - depthFade) * exp(-waterDepth * 0.9);
                half3 color = lerp(waterColor, sceneColor * lerp(1, _ShallowColor.rgb * 1.4, 0.5), seeThrough);

                half fresnel = pow(1 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                half3 sky = SampleSH(reflect(-viewDirWS, normalWS));
                color = lerp(color, sky, saturate(fresnel * _ReflectionStrength));

                half3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                half specular = pow(saturate(dot(normalWS, halfDir)), _SpecularPower) * _SpecularStrength;
                color += mainLight.color * (specular * shadow);

                // Foam: shore band (red channel thresholded by closeness) + sparse whitecaps.
                float2 foamUV = input.positionWS.xz / _FoamTiling + normalWS.xz * 0.05;
                half4 foamMask = SAMPLE_TEXTURE2D(_FoamMask, sampler_FoamMask, foamUV + _Time.y * _WaveSpeed * 0.3);
                half shore = 1 - saturate(waterDepth / _FoamDistance);
                half shoreFoam = saturate((foamMask.r - (1 - shore)) * 4) * step(0.001, shore);
                half whitecaps = saturate((foamMask.g - (1 - _WhitecapAmount)) * 3) * depthFade;
                half foam = saturate(shoreFoam + whitecaps + rippleCrest);
                color = lerp(color, _FoamColor.rgb * lighting, foam);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
