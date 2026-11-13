// Sun shafts for SunShaftsFeature (NAV, 2026-11-13): 0 = sky mask round the sun, 1 = radial blur
// towards the sun, 2 = additive composite. Screen-space, quarter resolution for 0 and 1.
Shader "Hidden/NAV/SunShafts"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    float4 _SunShaftsSun;     // xy = sun in viewport (0..1), z = aspect
    float4 _SunShaftsParams;  // x = threshold, y = sun radius (screen heights)
    float4 _SunShaftsColor;
    float _SunShaftsStep;

    #define SHAFT_SAMPLES 12
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "SunShaftsMask"
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float depth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    float sky = depth <= 0.000001 ? 1.0 : 0.0;
                #else
                    float sky = depth >= 0.999999 ? 1.0 : 0.0;
                #endif

                half3 colour = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                half brightness = max(max(colour.r, colour.g), colour.b);
                half bright = saturate((brightness - _SunShaftsParams.x) / max(0.0001, 1.0 - _SunShaftsParams.x * 0.5));

                float2 toSun = uv - _SunShaftsSun.xy;
                toSun.x *= _SunShaftsSun.z;
                float falloff = saturate(1.0 - length(toSun) / _SunShaftsParams.y);
                falloff *= falloff;

                return half4(sky * bright * falloff.xxx, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SunShaftsBlur"
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 delta = (_SunShaftsSun.xy - uv) * (_SunShaftsStep / SHAFT_SAMPLES);
                half3 sum = 0;
                half weight = 0;
                half w = 1.0;
                [unroll]
                for (int i = 0; i < SHAFT_SAMPLES; i++)
                {
                    sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb * w;
                    weight += w;
                    w *= 0.93;
                    uv += delta;
                }

                return half4(sum / weight, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SunShaftsComposite"
            Blend One One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                half3 shafts = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
                return half4(shafts * _SunShaftsColor.rgb, 0.0);
            }
            ENDHLSL
        }
    }
}
