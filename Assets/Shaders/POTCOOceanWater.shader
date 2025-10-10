Shader "POTCO/Ocean Water"
{
    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _DetailMap ("Detail Map", 2D) = "white" {}
        _WaterColor ("Water Color", Color) = (0.3, 0.5, 0.7, 1)
        _ReflectionTex ("Reflection Texture", 2D) = "black" {}

        [Header(UV Animation)]
        _UVScale ("UV Scale", Vector) = (0.15, 0.12, 0, 0)
        _UVSpeedA ("UV Speed A", Vector) = (0.03, 0.015, 0, 0)
        _UVSpeedB ("UV Speed B", Vector) = (-0.02, 0.008, 0, 0)
        _TimeSec ("Time", Float) = 0

        [Header(Waves)]
        _Wave0 ("Wave 0 (Amp, Wavelength, Speed)", Vector) = (0.25, 8, 1.2, 0)
        _WaveDir0 ("Wave 0 Direction", Vector) = (0.34, 0.94, 0, 0)
        _Wave1 ("Wave 1 (Amp, Wavelength, Speed)", Vector) = (0.15, 5, 1.8, 0)
        _WaveDir1 ("Wave 1 Direction", Vector) = (-0.87, 0.5, 0, 0)
        _Wave2 ("Wave 2 (Amp, Wavelength, Speed)", Vector) = (0.08, 2.5, 2.2, 0)
        _WaveDir2 ("Wave 2 Direction", Vector) = (0.26, 0.97, 0, 0)
        _Wave3 ("Wave 3 (Amp, Wavelength, Speed)", Vector) = (0.05, 1.5, 2.5, 0)
        _WaveDir3 ("Wave 3 Direction", Vector) = (0.71, 0.71, 0, 0)

        [Header(Appearance)]
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.5
        _FresnelPower ("Fresnel Power", Range(0.1, 10)) = 5
        _Smoothness ("Smoothness", Range(0, 1)) = 0.8
        _DepthFade ("Depth Fade Distance", Float) = 5.0
        _ColorTint ("Color Tint Strength", Range(0, 2)) = 1.0
        _Brightness ("Brightness", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                float3 positionOS : TEXCOORD5;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_DetailMap);
            SAMPLER(sampler_DetailMap);
            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _WaterColor;
                float4 _UVScale;
                float4 _UVSpeedA;
                float4 _UVSpeedB;
                float _TimeSec;
                float4 _Wave0, _WaveDir0;
                float4 _Wave1, _WaveDir1;
                float4 _Wave2, _WaveDir2;
                float4 _Wave3, _WaveDir3;
                float _ReflectionStrength;
                float _FresnelPower;
                float _Smoothness;
                float _DepthFade;
                float _ColorTint;
                float _Brightness;
                float4x4 _ReflectionMatrix;
            CBUFFER_END

            // Simple vertical wave calculation (up/down motion only, no horizontal displacement)
            float3 GerstnerWave(float3 posLocal, float4 wave, float2 direction)
            {
                float amplitude = wave.x;
                float wavelength = wave.y;
                float speed = wave.z;

                float k = 2.0 * PI / wavelength;
                float c = sqrt(9.8 / k);
                float2 d = normalize(direction);
                float f = k * (dot(d, posLocal.xz) - c * _TimeSec * speed);

                // Only vertical displacement - no horizontal (x, z) movement
                return float3(
                    0,
                    amplitude * sin(f),
                    0
                );
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Use object-space position for stable wave calculations
                float3 positionOS = input.positionOS.xyz;

                // Apply Gerstner waves in object space
                float3 waveOffset = float3(0, 0, 0);
                waveOffset += GerstnerWave(positionOS, _Wave0, _WaveDir0.xy);
                waveOffset += GerstnerWave(positionOS, _Wave1, _WaveDir1.xy);
                waveOffset += GerstnerWave(positionOS, _Wave2, _WaveDir2.xy);
                waveOffset += GerstnerWave(positionOS, _Wave3, _WaveDir3.xy);

                // Apply wave offset in object space, then transform to world space
                positionOS += waveOffset;
                float3 positionWS = TransformObjectToWorld(positionOS);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);

                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.positionOS = input.positionOS.xyz;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Animated UVs using world coordinates (ocean texture stays fixed in world space)
                float2 uv1 = input.positionWS.xz * _UVScale.xy + _UVSpeedA.xy * _TimeSec;
                float2 uv2 = input.positionWS.xz * _UVScale.xy * 0.7 + _UVSpeedB.xy * _TimeSec;

                // Sample and combine normals (these are in tangent space, so just use for distortion)
                half3 normal1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1));
                half3 normal2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv2));
                half3 normalTS = normalize(normal1 + normal2);

                // For fresnel, use world-space normal (up vector) since we don't have tangent space setup
                half3 normal = half3(0, 1, 0);

                // Base color with tint control
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv1);
                baseColor.rgb = lerp(baseColor.rgb, baseColor.rgb * _WaterColor.rgb, _ColorTint);
                baseColor.rgb *= _Brightness;

                // View direction
                float3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);

                // Fresnel effect using world-space up normal (ensure minimum value for visibility)
                float fresnel = pow(1.0 - saturate(dot(normal, viewDirWS)), _FresnelPower);
                fresnel = max(fresnel, 0.9); // Minimum 90% reflection

                // Planar reflection - project world position into reflection texture space
                float4 reflectionProjPos = mul(_ReflectionMatrix, float4(input.positionWS, 1.0));
                float2 reflectionUV = reflectionProjPos.xy / reflectionProjPos.w;

                // Distort with normals (use tangent space normals for distortion)
                reflectionUV += normalTS.xy * 0.05;

                // Sample reflection with bounds check
                half4 reflection = half4(0, 0, 0, 0);
                if (reflectionProjPos.w > 0)
                {
                    reflection = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, reflectionUV);
                }

                // Combine base color with reflection using fresnel
                half3 finalColor = lerp(baseColor.rgb, reflection.rgb, fresnel * _ReflectionStrength);

                // Apply fog
                finalColor = MixFog(finalColor, input.fogFactor);

                // Output with alpha
                return half4(finalColor, baseColor.a);
            }
            ENDHLSL
        }
    }

    // Fallback for Built-in Render Pipeline
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                UNITY_FOG_COORDS(4)
                float3 localPos : TEXCOORD5;
            };

            sampler2D _BaseMap;
            sampler2D _NormalMap;
            sampler2D _ReflectionTex;
            float4 _WaterColor;
            float4 _UVScale;
            float4 _UVSpeedA;
            float4 _UVSpeedB;
            float _TimeSec;
            float4 _Wave0, _WaveDir0;
            float4 _Wave1, _WaveDir1;
            float4 _Wave2, _WaveDir2;
            float4 _Wave3, _WaveDir3;
            float _ReflectionStrength;
            float _FresnelPower;
            float4x4 _ReflectionMatrix;

            float3 GerstnerWave(float3 posLocal, float4 wave, float2 dir)
            {
                float amp = wave.x;
                float wl = wave.y;
                float spd = wave.z;
                float k = 6.28318 / wl;
                float c = sqrt(9.8 / k);
                float2 d = normalize(dir);
                float f = k * (dot(d, posLocal.xz) - c * _TimeSec * spd);
                // Only vertical displacement - no horizontal movement
                return float3(0, amp * sin(f), 0);
            }

            v2f vert(appdata v)
            {
                v2f o;

                // Use object-space position for stable wave calculations
                float3 posLocal = v.vertex.xyz;

                // Apply waves in object space
                float3 waveOffset = float3(0, 0, 0);
                waveOffset += GerstnerWave(posLocal, _Wave0, _WaveDir0.xy);
                waveOffset += GerstnerWave(posLocal, _Wave1, _WaveDir1.xy);
                waveOffset += GerstnerWave(posLocal, _Wave2, _WaveDir2.xy);
                waveOffset += GerstnerWave(posLocal, _Wave3, _WaveDir3.xy);

                posLocal += waveOffset;
                float3 worldPos = mul(unity_ObjectToWorld, float4(posLocal, 1.0)).xyz;

                o.worldPos = worldPos;
                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.pos);
                o.localPos = v.vertex.xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Animated UVs using world coordinates (ocean texture stays fixed in world space)
                float2 uv1 = i.worldPos.xz * _UVScale.xy + _UVSpeedA.xy * _TimeSec;
                float2 uv2 = i.worldPos.xz * _UVScale.xy * 0.7 + _UVSpeedB.xy * _TimeSec;

                fixed3 normal1 = UnpackNormal(tex2D(_NormalMap, uv1));
                fixed3 normal2 = UnpackNormal(tex2D(_NormalMap, uv2));
                fixed3 normalTS = normalize(normal1 + normal2);

                // For fresnel, use world-space up normal
                fixed3 normal = fixed3(0, 1, 0);

                fixed4 baseColor = tex2D(_BaseMap, uv1) * _WaterColor;

                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);
                fresnel = max(fresnel, 0.9); // Minimum 90% reflection

                // Planar reflection - project world position into reflection texture space
                float4 reflectionProjPos = mul(_ReflectionMatrix, float4(i.worldPos, 1.0));
                float2 reflectionUV = reflectionProjPos.xy / reflectionProjPos.w;

                // Distort with normals (use tangent space normals for distortion)
                reflectionUV += normalTS.xy * 0.05;

                // Sample reflection with bounds check
                fixed4 reflection = fixed4(0, 0, 0, 0);
                if (reflectionProjPos.w > 0)
                {
                    reflection = tex2D(_ReflectionTex, reflectionUV);
                }

                fixed3 finalColor = lerp(baseColor.rgb, reflection.rgb, fresnel * _ReflectionStrength);

                UNITY_APPLY_FOG(i.fogCoord, finalColor);
                return fixed4(finalColor, baseColor.a);
            }
            ENDCG
        }
    }

    FallBack "Transparent/Diffuse"
}
