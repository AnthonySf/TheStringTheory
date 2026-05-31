Shader "Custom/HighwayCharacterFade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _FadeStartY ("Fade Start Y", Range(0, 1)) = 0.62
        _FadeEndY ("Fade End Y", Range(0, 1)) = 0.38
        _MissFlashColor ("Miss Flash Color", Color) = (1, 0.34, 0.10, 1)
        _MissFlashStrength ("Miss Flash Strength", Range(0, 1)) = 0
        _MissFlashSpeed ("Miss Flash Speed", Range(0, 40)) = 14
        _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest [_ZTest]
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _FadeStartY;
            float _FadeEndY;
            fixed4 _MissFlashColor;
            float _MissFlashStrength;
            float _MissFlashSpeed;

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
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                float fade = smoothstep(_FadeEndY, _FadeStartY, i.uv.y);
                col.a *= fade;
                float flashWave = sin((_Time.y * _MissFlashSpeed) + (i.uv.y * 11.0) + (i.uv.x * 6.0));
                float flashBand = smoothstep(0.15, 0.95, (flashWave * 0.5) + 0.5);
                float flash = saturate(_MissFlashStrength * (0.72 + (flashBand * 0.28))) * saturate(col.a * 3.0);
                col.rgb = lerp(col.rgb, max(col.rgb * 0.58, _MissFlashColor.rgb * 0.92), flash * 0.82);
                col.rgb += _MissFlashColor.rgb * flash * 0.34;
                return col;
            }
            ENDCG
        }
    }
}
