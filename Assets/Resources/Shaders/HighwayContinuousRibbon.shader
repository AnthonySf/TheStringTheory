Shader "Custom/HighwayContinuousRibbon"
{
    Properties
    {
        _CenterColor ("Center Color", Color) = (0.15, 0.45, 1.0, 0.34)
        _EdgeColor ("Edge Color", Color) = (0.8, 0.94, 1.0, 0.9)
        _EmissionColor ("Emission Color", Color) = (0.6, 0.85, 1.0, 0.0)
        _VisibleStart01 ("Visible Start 01", Range(0, 1)) = 0
        _VisibleFadeSoftness01 ("Visible Fade Softness 01", Range(0.001, 0.05)) = 0.015
        _LengthFadeSoftness01 ("Length Fade Softness 01", Range(0.001, 0.08)) = 0.02
        _FlatLightStrength ("Flat Light Strength", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _CenterColor;
            fixed4 _EdgeColor;
            fixed4 _EmissionColor;
            float _VisibleStart01;
            float _VisibleFadeSoftness01;
            float _LengthFadeSoftness01;
            float _FlatLightStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float riseStrength : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.riseStrength = saturate(v.uv2.x);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float edgeDistance = abs((i.uv.x * 2.0) - 1.0);
                float edge = smoothstep(0.90, 0.985, edgeDistance);
                float edgeGlow = smoothstep(0.52, 0.95, edgeDistance);
                float startFade = smoothstep(0.0, _LengthFadeSoftness01, i.uv.y);
                float endFade = 1.0 - smoothstep(1.0 - _LengthFadeSoftness01, 1.0, i.uv.y);
                float lengthFade = startFade * endFade;
                float visibleMask = smoothstep(_VisibleStart01 - _VisibleFadeSoftness01, _VisibleStart01 + _VisibleFadeSoftness01, i.uv.y);
                float darkBand = i.riseStrength;

                float curveLight = lerp(1.0 + (_FlatLightStrength * 0.40), 1.0 - (_FlatLightStrength * 0.62), darkBand);
                float emissionLight = lerp(1.0 + (_FlatLightStrength * 1.05), 1.0 - (_FlatLightStrength * 0.82), darkBand);
                float alphaLight = lerp(1.0 + (_FlatLightStrength * 0.08), 1.0 - (_FlatLightStrength * 0.14), darkBand);

                fixed4 col = lerp(_CenterColor, _EdgeColor, edge);
                col.rgb *= curveLight;
                col.a *= lengthFade * visibleMask * alphaLight;
                col.rgb += _EmissionColor.rgb * edgeGlow * lengthFade * visibleMask * emissionLight * 1.18;
                return col;
            }
            ENDCG
        }
    }
}
