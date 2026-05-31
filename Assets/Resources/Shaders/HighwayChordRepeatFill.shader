Shader "Custom/HighwayChordRepeatFill"
{
    Properties
    {
        _BottomColor ("Bottom Color", Color) = (0.16, 0.66, 0.92, 0.30)
        _TopColor ("Top Color", Color) = (0.04, 0.13, 0.18, 0.015)
        _ZWrite ("Z Write", Float) = 0
        _Cull ("Cull", Float) = 0
        _ZTest ("Z Test", Float) = 8
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        Cull [_Cull]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BottomColor;
            fixed4 _TopColor;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float localX01 : TEXCOORD0;
                float localY01 : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localX01 = saturate(v.vertex.x + 0.5);
                o.localY01 = saturate(v.vertex.y + 0.5);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float topFade = smoothstep(0.0, 1.0, i.localY01);
                float centerGlow = 1.0 - abs((i.localX01 * 2.0) - 1.0);
                fixed4 col = lerp(_BottomColor, _TopColor, topFade);
                col.a *= lerp(0.82, 1.08, centerGlow);
                col.a = saturate(col.a);
                return col;
            }
            ENDCG
        }
    }
}
