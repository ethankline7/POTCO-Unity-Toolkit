Shader "EggImporter/VertexColorTexture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _AlphaTex ("Alpha Mask (optional)", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.1
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        LOD 200
        Cull Off
        ZWrite On

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert alphatest:_Cutoff
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
                fixed aMask = step(0.5, alphaTexColor.r);
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