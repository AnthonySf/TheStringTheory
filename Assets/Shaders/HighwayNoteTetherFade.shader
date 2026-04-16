Shader "Custom/HighwayNoteTetherFade"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.95)
        _FadeTop ("Fade Top", Range(0, 0.9)) = 0.5
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
            float _FadeTop;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float localY01 : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localY01 = saturate(v.vertex.y + 0.5);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float fade = 1.0 - smoothstep(1.0 - max(0.0001, _FadeTop), 1.0, i.localY01);
                fade *= fade;
                fixed4 col = _Color;
                col.a *= fade;
                return col;
            }
            ENDCG
        }
    }
}
