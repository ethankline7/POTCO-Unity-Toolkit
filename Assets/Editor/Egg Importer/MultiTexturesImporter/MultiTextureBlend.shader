Shader "EggImporter/MultiTextureBlend"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _BlendTex ("Blend Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _BlendMode ("Blend Mode", Range(0,1)) = 0.5
        _BlendScale ("Blend UV Scale", Float) = 32.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf BrightLambert vertex:vert
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        sampler2D _BlendTex;
        fixed4 _Color;
        float _BlendMode;
        float _BlendScale;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv2_BlendTex;
            fixed4 color : COLOR;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.uv_MainTex = v.texcoord.xy;
            o.uv2_BlendTex = v.texcoord1.xy;
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
            // Sample base texture (ground) with corrected UVs
            fixed4 baseColor = tex2D(_MainTex, IN.uv_MainTex);
            
            // Sample blend texture (grass) with scaled UVs for tiling
            fixed4 blendColor = tex2D(_BlendTex, IN.uv2_BlendTex * _BlendScale);
            
            // Direct multiplicative blending - multiply textures together for darkening effect
            fixed4 finalColor = baseColor * blendColor;
            
            // Apply vertex colors and material color
            finalColor *= IN.color * _Color;
            
            o.Albedo = finalColor.rgb;
            o.Alpha = finalColor.a;
        }
        ENDCG
    }
}