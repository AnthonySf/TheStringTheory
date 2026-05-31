Shader "Custom/HighwayFretBoundaryGlow"
{
    Properties
    {
        _Color ("Base Color", Color) = (0.20, 0.22, 0.25, 0.18)
        _BaseColor ("Base Color", Color) = (0.20, 0.22, 0.25, 0.18)
        [HDR]_EmissionColor ("Base Emission", Color) = (0, 0, 0, 0)
        [HDR]_FlashColor ("Flash Color", Color) = (0.12, 0.70, 1, 1)
        _FlashProgress ("Flash Progress", Range(0, 1)) = 0
        _FlashStrength ("Flash Strength", Range(0, 1)) = 0
        _FlashSoftness ("Flash Softness", Range(0.001, 0.5)) = 0.18
        _GlowWidth ("Glow Width", Range(0, 2)) = 0.45
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+120" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _Color;
            half4 _BaseColor;
            half4 _EmissionColor;
            half4 _FlashColor;
            float _FlashProgress;
            float _FlashStrength;
            float _FlashSoftness;
            float _GlowWidth;

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
                float strength = saturate(_FlashStrength);
                float width = 1.0 + (_GlowWidth * strength * 0.18);
                v.vertex.xz *= width;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localY01 = saturate(v.vertex.y + 0.5);
                return o;
            }

            float CenterOutMask(float localY01)
            {
                float centerDistance = abs(localY01 - 0.5) * 2.0;
                float progress = saturate(_FlashProgress);
                float softness = max(0.001, _FlashSoftness);
                float edge = max(progress + 0.0001, min(1.0, progress + softness));
                return 1.0 - smoothstep(progress, edge, centerDistance);
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 baseColor = _Color.a > 0.0001 ? _Color : _BaseColor;
                float flash = CenterOutMask(i.localY01) * saturate(_FlashStrength);
                half3 rgb = baseColor.rgb + (_EmissionColor.rgb * 0.10);
                half3 flashRgb = _FlashColor.rgb * (1.0 + flash * 2.35);
                rgb = lerp(rgb, flashRgb, saturate(flash));
                rgb += _FlashColor.rgb * flash * 0.72;
                half alpha = saturate(max(baseColor.a, _BaseColor.a) + (_FlashColor.a * flash * 1.18));
                return half4(rgb, alpha);
            }
            ENDCG
        }

        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragGlow
            #include "UnityCG.cginc"

            half4 _FlashColor;
            float _FlashProgress;
            float _FlashStrength;
            float _FlashSoftness;
            float _GlowWidth;

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
                float strength = saturate(_FlashStrength);
                float width = 1.0 + (_GlowWidth * strength * 0.35);
                v.vertex.xz *= width;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localY01 = saturate(v.vertex.y + 0.5);
                return o;
            }

            float CenterOutMask(float localY01)
            {
                float centerDistance = abs(localY01 - 0.5) * 2.0;
                float progress = saturate(_FlashProgress);
                float softness = max(0.001, _FlashSoftness);
                float edge = max(progress + 0.0001, min(1.0, progress + softness));
                return 1.0 - smoothstep(progress, edge, centerDistance);
            }

            half4 fragGlow(v2f i) : SV_Target
            {
                float centerDistance = abs(i.localY01 - 0.5) * 2.0;
                float band = CenterOutMask(i.localY01);
                float core = 1.0 - smoothstep(0.0, 0.24, centerDistance);
                float flash = saturate(_FlashStrength) * band;
                half3 glow = _FlashColor.rgb * (flash * 5.2 + core * flash * 2.35);
                half alpha = saturate(flash * _FlashColor.a * 1.35);
                return half4(glow, alpha);
            }
            ENDCG
        }
    }
}
