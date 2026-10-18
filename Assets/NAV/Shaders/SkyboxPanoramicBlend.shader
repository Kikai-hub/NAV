// Equirectangular (lat-long) skybox that cross-fades between painted skies, for DayNightLighting.
// DayNightLighting picks the two time-of-day phases around the current hour (e.g. Sunrise -> Day)
// and feeds them in as _TexA/_TexB with _Blend, then fades the whole sky towards _WeatherTex
// (an overcast/rainy sky) by _WeatherBlend. Exposure is per layer because the painted night and
// overcast skies need different brightness than the day sky.
//   _TexA, _TexB, _Blend        time-of-day pair, 0 = A, 1 = B
//   _WeatherTex, _WeatherBlend  overcast sky, 0 = clear, 1 = fully overcast
//   _Tint, _Exposure            time-of-day layer look (same meaning as Skybox/Panoramic)
//   _WeatherExposure            overcast layer brightness (dim it at night)
//   _Rotation                   degrees around Y, applied to all layers
// On top, a sun and a moon disc are drawn where DayNightLighting says they are (the directional
// light points from them), so the painted skies should be the Sunless/Moonless variants. They
// are clipped by the horizon, hidden behind painted clouds (low-saturation sky) and faded out
// with _WeatherBlend. _SunColor/_MoonColor are linear HDR (intensity baked in), set via SetVector.
//   _SunDir/_MoonDir      world direction towards the body (fixed in world, not rotated with the sky)
//   _SunSize/_MoonSize    angular radius, degrees
//   _SunGlow/_MoonGlow    halo strength
//   _MoonPhase            lunar cycle 0 = new, 0.5 = full; waxing is lit on the right, waning on the left
//   _CloudOcclusion       0 = discs always on top, 1 = fully hidden behind painted clouds
// Finally a moving cloud layer is drawn over everything: a flat cloud deck overhead, shaped from
// _CloudNoise (R = shapes, G = billows, B = morph) whose three layers DayNightLighting scrolls at
// different speeds with the wind, so the clouds drift and slowly change shape. It fades out
// towards the horizon, where the painted (static, distant) clouds take over.
//   _CloudOffset1.xy/.zw, _CloudOffset2.xy   per-layer scroll, in texture UVs (wrapped to 0..1)
//   _CloudScale           world size of the clouds (smaller = bigger clouds)
//   _CloudCoverage        0 = clear .. 1 = overcast;  _CloudSoftness = edge fuzziness
//   _CloudLitColor/_CloudShadowColor   linear, from the current sun/moon and ambient light
//   _CloudLightDir        direction towards the light (sun by day, moon by night)
//   _CloudSilver          bright rim on clouds next to the light
Shader "NAV/Skybox/Panoramic Blend"
{
    Properties
    {
        _Tint ("Tint Color", Color) = (.5, .5, .5, .5)
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
        _Rotation ("Rotation", Range(0, 360)) = 0
        [NoScaleOffset] _TexA ("Sky A (lat-long)", 2D) = "grey" {}
        [NoScaleOffset] _TexB ("Sky B (lat-long)", 2D) = "grey" {}
        _Blend ("Blend A -> B", Range(0, 1)) = 0
        [NoScaleOffset] _WeatherTex ("Overcast Sky (lat-long)", 2D) = "grey" {}
        _WeatherBlend ("Overcast Blend", Range(0, 1)) = 0
        [Gamma] _WeatherExposure ("Overcast Exposure", Range(0, 8)) = 1.0
        _SunDir ("Sun Direction", Vector) = (0, 1, 0, 0)
        [HDR] _SunColor ("Sun Color (linear HDR)", Color) = (0, 0, 0, 1)
        _SunSize ("Sun Radius (deg)", Range(0.1, 10)) = 1.6
        _SunGlow ("Sun Glow", Range(0, 4)) = 0.6
        _MoonDir ("Moon Direction", Vector) = (0, -1, 0, 0)
        [HDR] _MoonColor ("Moon Color (linear HDR)", Color) = (0, 0, 0, 1)
        _MoonSize ("Moon Radius (deg)", Range(0.1, 10)) = 2.2
        _MoonGlow ("Moon Glow", Range(0, 4)) = 0.25
        _MoonPhase ("Moon Phase (0 new, 0.5 full)", Range(0, 1)) = 0.5
        _CloudOcclusion ("Cloud Occlusion", Range(0, 1)) = 0.8
        [NoScaleOffset] _CloudNoise ("Cloud Noise (tiling)", 2D) = "black" {}
        _CloudOffset1 ("Cloud Offsets 1", Vector) = (0, 0, 0, 0)
        _CloudOffset2 ("Cloud Offsets 2", Vector) = (0, 0, 0, 0)
        _CloudScale ("Cloud Scale", Range(0.05, 4)) = 0.35
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0
        _CloudSoftness ("Cloud Softness", Range(0.01, 0.3)) = 0.08
        _CloudOpacity ("Cloud Opacity", Range(0, 1)) = 0.95
        [HDR] _CloudLitColor ("Cloud Lit Color (linear)", Color) = (1, 1, 1, 1)
        [HDR] _CloudShadowColor ("Cloud Shadow Color (linear)", Color) = (0.4, 0.45, 0.5, 1)
        _CloudLightDir ("Cloud Light Direction", Vector) = (0, 1, 0, 0)
        _CloudSilver ("Cloud Silver Lining", Range(0, 4)) = 0.6
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
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _TexA;
            sampler2D _TexB;
            sampler2D _WeatherTex;
            half4 _Tint;
            half _Exposure;
            float _Rotation;
            half _Blend;
            half _WeatherBlend;
            half _WeatherExposure;
            float3 _SunDir;
            half4 _SunColor;
            float _SunSize;
            half _SunGlow;
            float3 _MoonDir;
            half4 _MoonColor;
            float _MoonSize;
            half _MoonGlow;
            float _MoonPhase;
            half _CloudOcclusion;
            sampler2D _CloudNoise;
            float4 _CloudOffset1;
            float4 _CloudOffset2;
            float _CloudScale;
            half _CloudCoverage;
            half _CloudSoftness;
            half _CloudOpacity;
            half4 _CloudLitColor;
            half4 _CloudShadowColor;
            float3 _CloudLightDir;
            half _CloudSilver;

            float3 RotateAroundYInDegrees(float3 vertex, float degrees)
            {
                float alpha = degrees * UNITY_PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float3(mul(m, vertex.xz), vertex.y).xzy;
            }

            // Same mapping as Unity's Skybox/Panoramic, so the asset's skies line up identically.
            float2 ToRadialCoords(float3 coords)
            {
                float3 n = normalize(coords);
                float latitude = acos(n.y);
                float longitude = atan2(n.z, n.x);
                float2 sphereCoords = float2(longitude, latitude) * float2(0.5 / UNITY_PI, 1.0 / UNITY_PI);
                return float2(0.5, 1.0) - sphereCoords;
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
                float3 worldDir : TEXCOORD1;
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
                // The textures turn with _Rotation; the sun and moon stay where the light is.
                o.worldDir = rotated;
                return o;
            }

            // Angle (radians) between the view ray and a body.
            float AngleTo(float3 dir, float3 body)
            {
                return acos(clamp(dot(dir, body), -1.0, 1.0));
            }

            half3 Sun(float3 dir, half cloudMask)
            {
                float angle = AngleTo(dir, _SunDir);
                float r = angle / radians(_SunSize);
                // Slight limb darkening so it reads as a ball of light, not a flat sticker.
                half disc = (1.0 - smoothstep(0.9, 1.0, r)) * lerp(0.8, 1.0, sqrt(saturate(1.0 - r * r)));
                half glow = _SunGlow * (0.6 * exp(-angle * 18.0) + 0.15 * exp(-angle * 4.0));
                return _SunColor.rgb * (disc * cloudMask + glow * lerp(0.5, 1.0, cloudMask));
            }

            // rgb = light to add, a = how much of the sky behind it the disc covers (the unlit
            // part of the moon still hides the stars).
            half4 Moon(float3 dir, half cloudMask)
            {
                float angle = AngleTo(dir, _MoonDir);
                float radius = radians(_MoonSize);
                float r = angle / radius;
                half disc = 1.0 - smoothstep(0.92, 1.0, r);

                // A few soft dark "seas" in the disc's own plane so it looks like the moon.
                float3 right = normalize(cross(float3(0, 1, 0), _MoonDir) + float3(1e-4, 0, 0));
                float3 up = cross(_MoonDir, right);
                float2 p = float2(dot(dir, right), dot(dir, up)) / sin(radius);
                // Loosely the real near side: a big merged patch on the left, smaller ones top-right
                // (kept asymmetric - three round spots read as a face).
                half seas = 1.0 - smoothstep(0.12, 0.32, length(p - float2(-0.38, 0.22)));
                seas = max(seas, 1.0 - smoothstep(0.1, 0.28, length(p - float2(-0.48, -0.15))));
                seas = max(seas, 1.0 - smoothstep(0.06, 0.18, length(p - float2(0.18, 0.38))));
                seas = max(seas, 1.0 - smoothstep(0.08, 0.22, length(p - float2(0.32, 0.05))));
                seas = max(seas, 0.6 * (1.0 - smoothstep(0.04, 0.14, length(p - float2(0.1, -0.3)))));
                half shade = (1.0 - 0.25 * seas) * lerp(0.75, 1.0, sqrt(saturate(1.0 - r * r)));

                // Phase: light the moon as a sphere. n is its surface normal in the disc frame
                // (x = right, y = up, z = towards the viewer); the light swings from behind (new)
                // through the right side (first quarter) to the front (full), then past full sin()
                // turns negative and the left side stays lit - the familiar northern-hemisphere
                // look: waxing lit on the right, waning on the left. (Lighting it from the real sun
                // would always light it from below, since the sun is under the horizon all night.)
                float3 n = float3(p, sqrt(saturate(1.0 - dot(p, p))));
                float phaseAngle = _MoonPhase * 2.0 * UNITY_PI;
                float3 light = float3(sin(phaseAngle), 0.0, -cos(phaseAngle));
                half lit = smoothstep(-0.04, 0.08, dot(n, light));
                // Very faint earthshine keeps the dark part just readable against the sky.
                half brightness = lerp(0.006, 1.0, lit);
                half illumination = 0.5 - 0.5 * cos(phaseAngle);

                half glow = _MoonGlow * illumination * (0.5 * exp(-angle * 25.0) + 0.2 * exp(-angle * 6.0));
                half3 rgb = _MoonColor.rgb * (disc * shade * brightness * cloudMask + glow * lerp(0.5, 1.0, cloudMask));
                return half4(rgb, disc * cloudMask);
            }

            // 0..~1 cloud density at a point on the cloud deck. Each noise layer scrolls at its
            // own speed (offsets from C#), so shapes drift, billow and morph instead of sliding rigidly.
            half CloudDensity(float2 p)
            {
                half shapes = tex2D(_CloudNoise, p * 0.25 + _CloudOffset1.xy).r;
                half billows = tex2D(_CloudNoise, p * 1.6 + _CloudOffset1.zw).g;
                half morph = tex2D(_CloudNoise, p * 0.4 + _CloudOffset2.xy).b;
                // Mostly lands in ~0.3..0.7 (measured over the texture); the fine billows break the
                // edges into cauliflower puffs to match the painted clouds.
                return shapes * 0.65 + morph * 0.35 + (billows - 0.5) * 0.18;
            }

            // rgb = cloud colour, a = coverage of what's behind it.
            half4 Clouds(float3 dir)
            {
                // Overhead only: fades out before the horizon, where the painted clouds are.
                // (No early-outs: texture sampling inside divergent branches breaks mip selection.)
                // Starts fairly high: low on the deck the projection stretches the noise into streaks.
                half horizonFade = smoothstep(0.12, 0.45, dir.y);

                // Project the view ray onto a flat deck; +0.12 keeps it finite near the horizon.
                float2 p = dir.xz / (dir.y + 0.12) * _CloudScale;
                half density = CloudDensity(p);

                // Coverage -> density threshold over the noise's real range, so coverage reads as
                // roughly the fraction of sky covered (0.45 ~ a third, 0.85 ~ nearly all).
                float threshold = lerp(0.70, 0.32, _CloudCoverage);
                half shape = smoothstep(threshold, threshold + _CloudSoftness, density) * step(1e-3, _CloudCoverage);

                // One step towards the light: more cloud between this point and the light = darker.
                float2 toLight = _CloudLightDir.xz;
                toLight = dot(toLight, toLight) > 1e-6 ? normalize(toLight) * 0.12 : float2(0, 0);
                half towardsLight = CloudDensity(p + toLight);
                half lit = saturate(0.85 + (density - towardsLight) * 4.0);
                // Thick cores are a little darker underneath than the thin edges.
                lit *= lerp(1.0, 0.8, saturate((density - threshold) / max(_CloudSoftness * 3.0, 1e-3)));

                half3 color = lerp(_CloudShadowColor.rgb, _CloudLitColor.rgb, lit);
                // Silver lining: thin edges glow when you look towards the light.
                half edge = shape * (1.0 - shape) * 4.0;
                half towardsLightView = pow(saturate(dot(dir, _CloudLightDir)), 8.0);
                color += _CloudLitColor.rgb * _CloudSilver * edge * towardsLightView;

                return half4(color, shape * _CloudOpacity * horizonFade);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = ToRadialCoords(i.texcoord);

                half3 a = tex2D(_TexA, uv).rgb;
                half3 b = tex2D(_TexB, uv).rgb;
                half3 painted = lerp(a, b, _Blend);
                half3 timeSky = painted * _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;

                half3 weatherSky = tex2D(_WeatherTex, uv).rgb * _WeatherExposure;
                half3 c = lerp(timeSky, weatherSky, _WeatherBlend);

                // Painted clouds are white/grey (low saturation), open sky is coloured.
                half maxC = max(painted.r, max(painted.g, painted.b));
                half minC = min(painted.r, min(painted.g, painted.b));
                half saturation = (maxC - minC) / max(maxC, 1e-3);
                half cloudMask = lerp(1.0, smoothstep(0.08, 0.3, saturation), _CloudOcclusion);

                float3 dir = normalize(i.worldDir);
                half visible = smoothstep(-0.005, 0.02, dir.y) * (1.0 - _WeatherBlend);
                half4 moon = Moon(dir, cloudMask);
                c *= 1.0 - 0.85 * moon.a * visible;
                c += (Sun(dir, cloudMask) + moon.rgb) * visible;

                half4 clouds = Clouds(dir);
                c = lerp(c, clouds.rgb, clouds.a);

                return half4(c, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
