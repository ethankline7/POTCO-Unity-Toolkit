Shader "EggImporter/VertexColorTextureTransparent"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlendTex ("Blend Texture (optional)", 2D) = "white" {}
        _AlphaTex ("Alpha Mask (optional)", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _SwapUVChannels ("Swap UV Channels", Float) = 0
        _MainTexWrap ("Main Tex Wrap", Vector) = (0,0,0,0)
        _BlendTexWrap ("Blend Tex Wrap", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        Cull [_Cull]
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
        #pragma surface surf BrightLambert vertex:vert alpha:fade
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

        float2 ApplyWrapMode(float2 uv, float2 wrapMode)
        {
            float2 result = uv;
            if (wrapMode.x > 0.5) result.x = saturate(result.x);
            if (wrapMode.y > 0.5) result.y = saturate(result.y);
            return result;
        }

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.uv_MainTex = v.texcoord.xy;
            o.uv2_BlendTex = v.texcoord1.xy;
            o.color = v.color;
        }

        half4 LightingBrightLambert(SurfaceOutput s, half3 lightDir, half atten)
        {
            half NdotL = dot(s.Normal, lightDir);
            half diff = NdotL * 0.3 + 0.3;
            half4 c;
            half3 ambient = s.Albedo * 0.05;
            half3 directional = s.Albedo * _LightColor0.rgb * (diff * atten);
            c.rgb = ambient + directional;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            float2 mainUV = ApplyWrapMode(IN.uv_MainTex, _MainTexWrap.xy);
            float2 blendUV = ApplyWrapMode(IN.uv2_BlendTex, _BlendTexWrap.xy);

            fixed4 texColor = tex2D(_MainTex, mainUV);
            fixed4 blendColor = tex2D(_BlendTex, blendUV);

            if (blendColor.r < 0.99 || blendColor.g < 0.99 || blendColor.b < 0.99)
            {
                texColor *= blendColor;
            }

            fixed4 finalColor = texColor * IN.color * _Color;
            o.Albedo = finalColor.rgb;

            fixed4 alphaTexColor = tex2D(_AlphaTex, float2(IN.uv_MainTex.x, 1.0 - IN.uv_MainTex.y));

            if (alphaTexColor.r < 0.99 || alphaTexColor.g < 0.99 || alphaTexColor.b < 0.99)
            {
                fixed aMask = alphaTexColor.r * 0.5;
                o.Alpha = aMask * finalColor.a;
            }
            else
            {
                o.Alpha = finalColor.a;
            }
        }
        ENDCG
    }

    Fallback "Legacy Shaders/Transparent/VertexLit"
}
