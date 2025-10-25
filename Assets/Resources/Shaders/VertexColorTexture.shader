Shader "EggImporter/VertexColorTexture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlendTex ("Blend Texture (optional)", 2D) = "white" {}
        _AlphaTex ("Alpha Mask (optional)", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _SwapUVChannels ("Swap UV Channels", Float) = 0
        _MainTexWrap ("Main Tex Wrap", Vector) = (0,0,0,0)  // x=wrapU, y=wrapV (0=repeat, 1=clamp)
        _BlendTexWrap ("Blend Tex Wrap", Vector) = (0,0,0,0)
    }
    SubShader
    {
        LOD 200
        Cull [_Cull]
        
        CGPROGRAM
        #pragma surface surf BrightLambert vertex:vert alphatest:_Cutoff
        #pragma shader_feature SWAP_UV_CHANNELS
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        sampler2D _BlendTex;
        sampler2D _AlphaTex;
        fixed4 _Color;
        float _SwapUVChannels;
        float4 _MainTexWrap;
        float4 _BlendTexWrap;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv2_BlendTex;
            fixed4 color : COLOR;
        };

        // Manual UV wrap/clamp function
        float2 ApplyWrapMode(float2 uv, float2 wrapMode)
        {
            float2 result = uv;
            // wrapMode.x = wrapU (0=repeat, 1=clamp)
            // wrapMode.y = wrapV (0=repeat, 1=clamp)

            // If clamp mode (1), saturate to 0-1 range
            if (wrapMode.x > 0.5) result.x = saturate(result.x);
            if (wrapMode.y > 0.5) result.y = saturate(result.y);

            return result;
        }

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);

            // Simple UV assignment matching TRef order:
            // _MainTex uses UV0 (first TRef)
            // _BlendTex uses UV1 (second TRef)
            o.uv_MainTex = v.texcoord.xy;    // UV0 for main texture
            o.uv2_BlendTex = v.texcoord1.xy; // UV1 for blend texture

            o.color = v.color;
        }

        // Custom lighting model with ambient lighting so models aren't pitch black
        half4 LightingBrightLambert(SurfaceOutput s, half3 lightDir, half atten)
        {
            half NdotL = dot(s.Normal, lightDir);
            half diff = NdotL * 0.3 + 0.3; // Wrap lighting for brightness
            half4 c;
            // Add ambient lighting (5% minimum brightness) plus directional lighting
            half3 ambient = s.Albedo * 0.05; // 5% ambient light
            half3 directional = s.Albedo * _LightColor0.rgb * (diff * atten);
            c.rgb = ambient + directional;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            // Apply wrap modes to UVs
            float2 mainUV = ApplyWrapMode(IN.uv_MainTex, _MainTexWrap.xy);
            float2 blendUV = ApplyWrapMode(IN.uv2_BlendTex, _BlendTexWrap.xy);

            // Sample main texture
            fixed4 texColor = tex2D(_MainTex, mainUV);

            // Sample blend texture with UV2
            fixed4 blendColor = tex2D(_BlendTex, blendUV);

            // If blend texture is assigned (not white), multiply it with base texture
            if (blendColor.r < 0.99 || blendColor.g < 0.99 || blendColor.b < 0.99)
            {
                texColor *= blendColor;
            }

            // Apply vertex colors and material color
            fixed4 finalColor = texColor * IN.color * _Color;

            o.Albedo = finalColor.rgb;
            
            // Check if alpha texture is assigned (not white)
            fixed4 alphaTexColor = tex2D(_AlphaTex, float2(IN.uv_MainTex.x, 1.0 - IN.uv_MainTex.y));
            
            // If alpha texture is essentially white (1,1,1), use regular alpha
            // Otherwise use the alpha mask
            if (alphaTexColor.r < 0.99 || alphaTexColor.g < 0.99 || alphaTexColor.b < 0.99)
            {
                // Alpha mask is assigned - use it with binary cutoff
                fixed aMask = alphaTexColor.r * 0.5;
                o.Alpha = aMask * finalColor.a;
            }
            else
            {
                // No alpha mask - use regular alpha
                o.Alpha = finalColor.a;
            }
        }
        ENDCG
    }
    
    Fallback "Legacy Shaders/VertexLit"
}