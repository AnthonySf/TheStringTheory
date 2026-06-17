Shader "Custom/TabsTexturedTransparent"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ZWrite ("ZWrite", Float) = 0
        _Cull ("Cull", Float) = 0
        _ZTest ("ZTest", Float) = 8
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull [_Cull]
        ZWrite [_ZWrite]
        ZTest [_ZTest]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _BaseColor;

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
                fixed4 tint = _Color.a > 0.0001 ? _Color : _BaseColor;
                fixed4 tex = tex2D(_MainTex, i.uv);
                return fixed4(tex.rgb * tint.rgb, tex.a * tint.a);
            }
            ENDCG
        }
    }
}
