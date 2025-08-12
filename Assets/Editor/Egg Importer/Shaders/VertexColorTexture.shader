Shader "EggImporter/VertexColorTexture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _AlphaTex ("Alpha Mask (optional)", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        LOD 200
        Cull [_Cull]
        
        CGPROGRAM
        #pragma surface surf BrightLambert vertex:vert alphatest:_Cutoff
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        sampler2D _AlphaTex;
        fixed4 _Color;

        struct Input
        {
            float2 uv_MainTex;
            fixed4 color : COLOR;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.uv_MainTex = v.texcoord.xy;
            o.color = v.color;
        }

        // Custom lighting model with ambient lighting so models aren't pitch black
        half4 LightingBrightLambert(SurfaceOutput s, half3 lightDir, half atten)
        {
            half NdotL = dot(s.Normal, lightDir);
            half diff = NdotL * 0.5 + 0.5; // Wrap lighting for brightness
            half4 c;
            // Add ambient lighting (15% minimum brightness) plus directional lighting
            half3 ambient = s.Albedo * 0.15; // 15% ambient light
            half3 directional = s.Albedo * _LightColor0.rgb * (diff * atten);
            c.rgb = ambient + directional;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            // Sample main texture
            fixed4 texColor = tex2D(_MainTex, IN.uv_MainTex);
            
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