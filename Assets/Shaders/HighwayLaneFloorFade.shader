Shader "Custom/HighwayLaneFloorFade"
{
    Properties
    {
        _Color ("Color", Color) = (0.025, 0.03, 0.045, 0.14)
        _EdgeFadeLeft ("Edge Fade Left", Range(0, 0.5)) = 0.01
        _EdgeFadeRight ("Edge Fade Right", Range(0, 0.5)) = 0.01
        _FrontBackFade ("Front Back Fade", Range(0, 0.5)) = 0.45
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
            float _EdgeFadeLeft;
            float _EdgeFadeRight;
            float _FrontBackFade;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float localX01 : TEXCOORD0;
                float localZ01 : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localX01 = saturate(v.vertex.x + 0.5);
                o.localZ01 = saturate(v.vertex.z + 0.5);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float leftFade = smoothstep(0.0, max(0.0001, _EdgeFadeLeft), i.localX01);
                float rightFade = 1.0 - smoothstep(1.0 - max(0.0001, _EdgeFadeRight), 1.0, i.localX01);
                float frontFade = smoothstep(0.0, max(0.0001, _FrontBackFade), i.localZ01);
                float backFade = 1.0 - smoothstep(1.0 - max(0.0001, _FrontBackFade), 1.0, i.localZ01);
                float fade = saturate(leftFade * rightFade * frontFade * backFade);

                fixed4 col = _Color;
                col.a *= fade;
                return col;
            }
            ENDCG
        }
    }
}
