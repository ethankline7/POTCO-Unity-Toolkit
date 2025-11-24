Shader "EggImporter/ParticleGUI"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _AlphaTex ("Alpha Mask (Optional)", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        _Alpha ("Alpha Multiplier", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0 // Off by default for particles
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100
        
        // Standard particle/GUI settings
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull [_Cull]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _AlphaTex;
            fixed4 _Color;
            float _Alpha;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample Main Texture
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Sample Alpha Mask (Standard POTCO Alpha Map V-Flip)
                fixed4 alphaSample = tex2D(_AlphaTex, float2(i.uv.x, 1.0 - i.uv.y));
                
                // Apply Vertex Color and Tint (Unlit)
                col *= i.color * _Color;
                
                // Apply Gradient Alpha Logic (ShoreFoam style)
                // If alpha mask is present (not white), use its Red channel as alpha multiplier
                // allowing for smooth gradients
                if (alphaSample.r < 0.99 || alphaSample.g < 0.99 || alphaSample.b < 0.99)
                {
                    col.a *= alphaSample.r;
                }
                
                col.a *= _Alpha; // Global alpha multiplier

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
