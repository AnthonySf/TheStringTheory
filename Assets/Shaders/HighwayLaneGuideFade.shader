Shader "Custom/HighwayLaneGuideFade"
{
    Properties
    {
        _Color ("Color", Color) = (0.12, 0.26, 0.55, 0.85)
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 0)
        _FadeStart ("Fade Start", Range(0, 1)) = 0
        _FadeEnd ("Fade End", Range(0, 1)) = 0.38
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _EmissionColor;
            float _FadeStart;
            float _FadeEnd;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float localZ01 : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localZ01 = saturate(v.vertex.z + 0.5);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float fade = smoothstep(_FadeStart, _FadeEnd, i.localZ01);
                fixed4 col = _Color;
                col.a *= fade;
                col.rgb += _EmissionColor.rgb * fade;
                return col;
            }
            ENDCG
        }
    }
}
