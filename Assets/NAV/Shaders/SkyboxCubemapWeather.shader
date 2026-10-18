// Cubemap skybox with an overcast blend, for DayNightLighting.
// Same inputs as Unity's Skybox/Cubemap (_Tex, _Tint, _Exposure, _Rotation) so the scene's
// HDRI material values can be copied over 1:1, plus:
//   _Overcast       0 = sky as-is, 1 = fully desaturated towards _OvercastColor
//   _OvercastColor  the grey the sky's luminance is mapped onto when overcast
// Skybox/Cubemap's _Tint can only multiply colour, so a blue sky stayed blue in rain.
Shader "NAV/Skybox/Cubemap Weather"
{
    Properties
    {
        _Tint ("Tint Color", Color) = (.5, .5, .5, .5)
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
        _Rotation ("Rotation", Range(0, 360)) = 0
        [NoScaleOffset] _Tex ("Cubemap (HDR)", Cube) = "grey" {}
        _Overcast ("Overcast", Range(0, 1)) = 0
        _OvercastColor ("Overcast Color", Color) = (.55, .57, .6, 1)
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            samplerCUBE _Tex;
            half4 _Tex_HDR;
            half4 _Tint;
            half _Exposure;
            float _Rotation;
            half _Overcast;
            half4 _OvercastColor;

            float3 RotateAroundYInDegrees(float3 vertex, float degrees)
            {
                float alpha = degrees * UNITY_PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float3(mul(m, vertex.xz), vertex.y).xzy;
            }

            struct appdata_t
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 rotated = RotateAroundYInDegrees(v.vertex.xyz, _Rotation);
                o.vertex = UnityObjectToClipPos(rotated);
                o.texcoord = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half4 tex = texCUBE(_Tex, i.texcoord);
                half3 c = DecodeHDR(tex, _Tex_HDR);
                c = c * _Tint.rgb * unity_ColorSpaceDouble.rgb;
                half luminance = dot(c, half3(0.299, 0.587, 0.114));
                // Overcast: keep the cloud shapes (luminance) but drop the colour.
                c = lerp(c, luminance * _OvercastColor.rgb * 1.6, _Overcast);
                c *= _Exposure;
                return half4(c, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
