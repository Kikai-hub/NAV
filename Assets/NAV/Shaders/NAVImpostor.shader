// Far-away tree impostor (WorldGenerator.Impostors): an instanced quad that turns to face the
// camera around its own vertical axis and shows whichever of the atlas' N baked side views is
// closest to the angle it's seen from (frame k = the tree seen from k * 360/N degrees around
// its local forward - ImpostorBaker). Alpha-clipped, lit by the current sun colour and sky
// ambient (the atlas holds evenly lit colour), fogged like everything else, no shadows.
Shader "NAV/Impostor"
{
    Properties
    {
        _Atlas ("Atlas", 2D) = "white" {}
        _Frames ("Frames", Float) = 8
        _Size ("Size (width, height) m", Vector) = (8, 12, 0, 0)
        _BaseY ("Bottom (m below pivot)", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.4
        _Brightness ("Brightness", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Atlas);
            SAMPLER(sampler_Atlas);

            CBUFFER_START(UnityPerMaterial)
                float4 _Atlas_ST;
                float _Frames;
                float4 _Size;
                float _BaseY;
                float _Cutoff;
                float _Brightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;

                // Instance pivot (the tree's base), uniform scale and facing from its matrix.
                float3 pivot = TransformObjectToWorld(float3(0, 0, 0));
                float3 forward = TransformObjectToWorldDir(float3(0, 0, 1), false);
                float scale = length(forward);
                forward /= max(scale, 1e-4);
                float3 right = float3(forward.z, 0, -forward.x);

                float3 toCamera = _WorldSpaceCameraPos - pivot;
                toCamera.y = 0;
                toCamera = normalize(toCamera + float3(1e-4, 0, 0));
                // The viewer's right (the baked image's +u): cross(toCamera, up).
                float3 faceRight = normalize(cross(toCamera, float3(0, 1, 0)));

                // Seen-from angle around the tree's own axis -> nearest baked frame.
                float angle = atan2(dot(toCamera, right), dot(toCamera, forward));
                float frame = fmod(round(angle / (6.2831853 / _Frames)) + _Frames, _Frames);

                float3 world = pivot
                    + faceRight * (input.positionOS.x * _Size.x * scale)
                    + float3(0, 1, 0) * ((_BaseY + input.positionOS.y * _Size.y) * scale);

                output.positionCS = TransformWorldToHClip(world);
                output.uv = float2((frame + input.positionOS.x + 0.5) / _Frames, input.positionOS.y);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 colour = SAMPLE_TEXTURE2D(_Atlas, sampler_Atlas, input.uv);
                clip(colour.a - _Cutoff);

                // The atlas is already evenly lit; this only follows the time of day and weather.
                // Main light colour carries its intensity (above 1 by day) - capped, or noon
                // turns the far forest white.
                Light sun = GetMainLight();
                half3 light = SampleSH(half3(0, 1, 0)) * 0.5 + min(sun.color, 1.0) * 0.45;
                colour.rgb *= min(light, 1.0) * _Brightness;
                colour.rgb = MixFog(colour.rgb, input.fogFactor);
                return half4(colour.rgb, 1);
            }
            ENDHLSL
        }
    }
}
