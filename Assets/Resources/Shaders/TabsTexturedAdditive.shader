Shader "Custom/TabsTexturedAdditive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _AlphaAsColor ("Alpha As Color", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend One One
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _BaseColor;
            float _AlphaAsColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 tint = _Color.a > 0.0001 ? _Color : _BaseColor;
                float4 tex = tex2D(_MainTex, i.uv);
                float energy = saturate(tex.a * tint.a);
                float3 alphaShape = max(tex.rgb, tex.a.xxx);
                float3 source = lerp(tex.rgb, alphaShape, saturate(_AlphaAsColor));
                return float4(source * tint.rgb * energy, 0.0);
            }
            ENDCG
        }
    }
}
