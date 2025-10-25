Shader "Skybox/POTCO Sky"
{
    Properties
    {
        [Header(Cloud Layers POTCO MultiStage System)]
        _CloudLayerA ("Cloud Layer A", 2D) = "white" {}
        _CloudLayerB ("Cloud Layer B", 2D) = "white" {}
        _CloudLayerC ("Cloud Layer C", 2D) = "white" {}
        _CloudOpaque ("Cloud Opaque Mask", 2D) = "white" {}
        _CloudTransparent ("Cloud Transparent Mask", 2D) = "white" {}

        [Header(Cloud Animation 400s Cycle)]
        _CloudSpeedA ("Cloud Speed A", Vector) = (0.005, 0.0025, 0, 0)
        _CloudSpeedB ("Cloud Speed B", Vector) = (-0.005, 0, 0, 0)
        _CloudScale ("Cloud UV Scale", Float) = 1.0
        _CloudBlendAB ("Cloud Layer Blend", Range(0, 1)) = 0.5
        _CloudIntensity ("Cloud Intensity", Range(0, 2)) = 1.0

        [Header(Stars)]
        _StarsTex ("Stars Texture", 2D) = "black" {}
        _StarsIntensity ("Stars Intensity", Range(0, 1)) = 0.25
        _StarsScale ("Stars Scale", Float) = 1.0
        _StarsFadeStart ("Stars Fade Start", Range(-1, 1)) = -0.2
        _StarsFadeEnd ("Stars Fade End", Range(-1, 1)) = 0.3
        _StarsHeightFade ("Stars Height Fade", Float) = 1.0

        [Header(Sky Colors Stage Blending)]
        _SkyColorTopA ("Sky Top Color A", Color) = (0.5, 0.7, 1.0, 1.0)
        _SkyColorTopB ("Sky Top Color B", Color) = (0.3, 0.5, 0.8, 1.0)
        _SkyColorHorizonA ("Sky Horizon Color A", Color) = (0.7, 0.8, 1.0, 1.0)
        _SkyColorHorizonB ("Sky Horizon Color B", Color) = (0.6, 0.75, 0.9, 1.0)
        _SkyColorBottomA ("Sky Bottom Color A", Color) = (0.4, 0.6, 0.9, 1.0)
        _SkyColorBottomB ("Sky Bottom Color B", Color) = (0.5, 0.65, 0.85, 1.0)
        _StageBlend ("Stage Blend", Range(0, 1)) = 0

        [Header(Sun)]
        _SunTex ("Sun Texture", 2D) = "white" {}
        _SunColor ("Sun Color", Color) = (1, 0.95, 0.8, 1)
        _SunSize ("Sun Size", Range(0, 0.5)) = 0.04
        _SunIntensity ("Sun Intensity", Range(0, 10)) = 3.0
        _SunGlowSize ("Sun Glow Size", Range(0, 1)) = 0.3
        _SunGlowIntensity ("Sun Glow Intensity", Range(0, 5)) = 1.0
        _SunDirection ("Sun Direction", Vector) = (0, 0.4, 1, 0)

        [Header(Moon)]
        _MoonTex ("Moon Texture", 2D) = "white" {}
        _MoonGlow ("Moon Glow Texture", 2D) = "white" {}
        _MoonHalo ("Moon Halo Texture", 2D) = "white" {}
        _MoonColor ("Moon Color", Color) = (0.8, 0.85, 0.9, 1)
        _MoonSize ("Moon Size", Range(0, 0.5)) = 0.03
        _MoonIntensity ("Moon Intensity", Range(0, 5)) = 1.0
        _MoonGlowSize ("Moon Glow Size", Range(0, 1)) = 0.2
        _MoonGlowIntensity ("Moon Glow Intensity", Range(0, 3)) = 0.5
        _MoonDirection ("Moon Direction", Vector) = (0, -0.3, -1, 0)

        [Header(Overall)]
        _Brightness ("Brightness", Range(0, 3)) = 1.0
        _Exposure ("Exposure", Range(0, 8)) = 1.3
        _Contrast ("Contrast", Range(0.5, 2)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            // Cloud textures
            sampler2D _CloudLayerA;
            sampler2D _CloudLayerB;
            sampler2D _CloudLayerC;
            sampler2D _CloudOpaque;
            sampler2D _CloudTransparent;
            float4 _CloudSpeedA;
            float4 _CloudSpeedB;
            float _CloudScale;
            float _CloudBlendAB;
            float _CloudIntensity;

            // Stars
            sampler2D _StarsTex;
            float _StarsIntensity;
            float _StarsScale;
            float _StarsFadeStart;
            float _StarsFadeEnd;
            float _StarsHeightFade;

            // Sky colors - Stage system
            float4 _SkyColorTopA;
            float4 _SkyColorTopB;
            float4 _SkyColorHorizonA;
            float4 _SkyColorHorizonB;
            float4 _SkyColorBottomA;
            float4 _SkyColorBottomB;
            float _StageBlend;

            // Sun
            sampler2D _SunTex;
            float4 _SunColor;
            float _SunSize;
            float _SunIntensity;
            float _SunGlowSize;
            float _SunGlowIntensity;
            float3 _SunDirection;

            // Moon
            sampler2D _MoonTex;
            sampler2D _MoonGlow;
            sampler2D _MoonHalo;
            float4 _MoonColor;
            float _MoonSize;
            float _MoonIntensity;
            float _MoonGlowSize;
            float _MoonGlowIntensity;
            float3 _MoonDirection;

            // Overall
            float _Brightness;
            float _Exposure;
            float _Contrast;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.viewDir = v.texcoord;
                return o;
            }

            // Spherical projection for skybox textures
            float2 GetSphereUV(float3 dir)
            {
                float2 uv;
                uv.x = atan2(dir.x, dir.z) / (2.0 * UNITY_PI) + 0.5;
                uv.y = asin(clamp(dir.y, -1.0, 1.0)) / UNITY_PI + 0.5;
                return uv;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 viewDir = normalize(i.viewDir);
                float heightFactor = viewDir.y; // -1 (bottom) to +1 (top)

                // === SKY GRADIENT (Stage A/B Blending like POTCO) ===
                float3 skyColorTop = lerp(_SkyColorTopA.rgb, _SkyColorTopB.rgb, _StageBlend);
                float3 skyColorHorizon = lerp(_SkyColorHorizonA.rgb, _SkyColorHorizonB.rgb, _StageBlend);
                float3 skyColorBottom = lerp(_SkyColorBottomA.rgb, _SkyColorBottomB.rgb, _StageBlend);

                float3 skyColor;
                if (heightFactor > 0)
                {
                    // Above horizon: blend horizon to top
                    float t = pow(heightFactor, 0.5); // Slight curve
                    skyColor = lerp(skyColorHorizon, skyColorTop, t);
                }
                else
                {
                    // Below horizon: blend horizon to bottom
                    float t = pow(-heightFactor, 0.5);
                    skyColor = lerp(skyColorHorizon, skyColorBottom, t);
                }

                // === CLOUD LAYERS (Multi-layer system like POTCO) ===
                // Get base UV coordinates
                float2 baseUV = GetSphereUV(viewDir);

                // Layer A - Primary cloud layer
                float2 cloudOffsetA = _CloudSpeedA.xy * _Time.y;
                float2 cloudUVA = (baseUV + cloudOffsetA) * _CloudScale;
                float3 cloudsA = tex2D(_CloudLayerA, cloudUVA).rgb;

                // Layer B - Secondary cloud layer with different speed
                float2 cloudOffsetB = _CloudSpeedB.xy * _Time.y;
                float2 cloudUVB = (baseUV + cloudOffsetB) * _CloudScale * 1.2; // Slightly different scale
                float3 cloudsB = tex2D(_CloudLayerB, cloudUVB).rgb;

                // Layer C - Tertiary detail layer
                float2 cloudOffsetC = (_CloudSpeedA.xy * 0.5) * _Time.y; // Half speed of A
                float2 cloudUVC = (baseUV + cloudOffsetC) * _CloudScale * 0.8; // Slightly different scale
                float3 cloudsC = tex2D(_CloudLayerC, cloudUVC).rgb;

                // Blend layers A and B using CloudBlendAB parameter
                float3 cloudsAB = lerp(cloudsA, cloudsB, _CloudBlendAB);

                // Multiply with layer C for detail
                float3 clouds = cloudsAB * cloudsC;

                // Fade clouds near bottom of sky
                float cloudFade = saturate(heightFactor * 1.5 + 0.3);

                // Apply clouds as subtle overlay - blend between sky and cloud-tinted sky
                // This prevents dark areas from looking transparent
                float cloudStrength = _CloudIntensity * cloudFade * 0.3; // Reduced strength
                skyColor = lerp(skyColor, skyColor * clouds * 1.5, cloudStrength);

                // === STARS (Additive with fade) ===
                float2 starsUV = GetSphereUV(viewDir) * _StarsScale;
                float4 stars = tex2D(_StarsTex, starsUV);

                // Fade stars based on sun direction (appears at night)
                float3 sunDir = normalize(_SunDirection);
                float sunDot = dot(viewDir, sunDir);
                float starsFade = 1.0 - saturate((sunDot - _StarsFadeStart) / (_StarsFadeEnd - _StarsFadeStart));

                // Height-based fade (controllable via _StarsHeightFade: 0=disabled, 1=full fade)
                float starsHeightFade = saturate(heightFactor * 2.0);
                starsFade *= lerp(1.0, starsHeightFade, _StarsHeightFade);

                skyColor += stars.rgb * _StarsIntensity * starsFade;

                // === SUN ===
                float sunDist = distance(viewDir, sunDir);

                // Sun disk
                float sunMask = 1.0 - saturate(sunDist / _SunSize);
                sunMask = pow(sunMask, 2.0);
                float2 sunUV = (viewDir.xy - sunDir.xy) / _SunSize + 0.5;
                float4 sunTex = tex2D(_SunTex, sunUV);

                // Sun glow
                float sunGlow = 1.0 - saturate(sunDist / _SunGlowSize);
                sunGlow = pow(sunGlow, 3.0);

                float3 sunContribution = (_SunColor.rgb * sunTex.rgb * sunMask * _SunIntensity) +
                                        (_SunColor.rgb * sunGlow * _SunGlowIntensity);
                skyColor += sunContribution;

                // === MOON ===
                float3 moonDir = normalize(_MoonDirection);
                float moonDist = distance(viewDir, moonDir);

                // Moon disk
                float moonMask = 1.0 - saturate(moonDist / _MoonSize);
                moonMask = pow(moonMask, 2.0);
                float2 moonUV = (viewDir.xy - moonDir.xy) / _MoonSize + 0.5;
                float4 moonTex = tex2D(_MoonTex, moonUV);

                // Moon glow
                float moonGlow = 1.0 - saturate(moonDist / _MoonGlowSize);
                moonGlow = pow(moonGlow, 4.0);
                float4 moonGlowTex = tex2D(_MoonGlow, (viewDir.xy - moonDir.xy) / _MoonGlowSize + 0.5);

                // Moon halo
                float moonHaloSize = _MoonGlowSize * 2.0;
                float moonHaloMask = 1.0 - saturate(moonDist / moonHaloSize);
                float4 moonHaloTex = tex2D(_MoonHalo, (viewDir.xy - moonDir.xy) / moonHaloSize + 0.5);

                float3 moonContribution = (_MoonColor.rgb * moonTex.rgb * moonMask * _MoonIntensity) +
                                         (_MoonColor.rgb * moonGlowTex.rgb * moonGlow * _MoonGlowIntensity) +
                                         (_MoonColor.rgb * moonHaloTex.rgb * moonHaloMask * _MoonGlowIntensity * 0.3);
                skyColor += moonContribution;

                // === FINAL ADJUSTMENTS ===
                // Brightness
                skyColor *= _Brightness;

                // Contrast
                skyColor = (skyColor - 0.5) * _Contrast + 0.5;

                // Exposure
                skyColor *= _Exposure;

                // Clamp to prevent over-bright
                skyColor = saturate(skyColor);

                return fixed4(skyColor, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
