Shader "Custom/TabsHorizonGlow"
{
    Properties
    {
        _LeftColor ("Left Color", Color) = (2.5, 0.25, 4.0, 1)
        _RightColor ("Right Color", Color) = (0.15, 2.2, 4.0, 1)
        _MidColor ("Mid Color", Color) = (0.5, 0.20, 1.5, 1)
        _CenterColor ("White Core Color", Color) = (4.0, 3.2, 5.0, 1)
        _Intensity ("Intensity", Float) = 1.0
        _CoreStrength ("Core Strength", Float) = 0.0
        _VerticalSharpness ("Vertical Sharpness", Float) = 4.0
        _HorizontalSharpness ("Horizontal Sharpness", Float) = 1.5
        _CoreWidth ("Core Width", Float) = 0.035
        _CoreSoftness ("Core Softness", Float) = 0.050
        _ShimmerStrength ("Shimmer Strength", Float) = 0.04
        _Alpha ("Alpha", Float) = 1.0
        _CenterBlendWidth ("Center Blend Width", Float) = 0.18
        _CenterBlendFalloff ("Center Blend Falloff", Float) = 2.8
        _CenterBlendStrength ("Center Blend Strength", Float) = 0.35
        _ColorSaturation ("Color Saturation", Float) = 1.0
        _EdgeBlurStrength ("Edge Blur Strength", Float) = 0.0
        _EdgeBlurStart ("Edge Blur Start", Float) = 0.72
        _EdgeBlurSharpness ("Edge Blur Sharpness", Float) = 2.0
        _StageTime ("Stage Time", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend One One
        Cull Off
        ZWrite Off
        ZTest [_ZTest]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _LeftColor;
            float4 _RightColor;
            float4 _MidColor;
            float4 _CenterColor;
            float _Intensity;
            float _CoreStrength;
            float _VerticalSharpness;
            float _HorizontalSharpness;
            float _CoreWidth;
            float _CoreSoftness;
            float _ShimmerStrength;
            float _Alpha;
            float _CenterBlendWidth;
            float _CenterBlendFalloff;
            float _CenterBlendStrength;
            float _ColorSaturation;
            float _EdgeBlurStrength;
            float _EdgeBlurStart;
            float _EdgeBlurSharpness;
            float _StageTime;

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
                o.uv = v.uv;
                return o;
            }

            float SafePow(float value, float power)
            {
                return pow(saturate(value), max(power, 0.001));
            }

            float3 SaturateColor(float3 color, float saturation)
            {
                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                return max(lerp(luminance.xxx, color, max(saturation, 0.0)), 0.0);
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = saturate(i.uv);
                float yDistance = abs(uv.y - 0.5) * 2.0;
                float xDistance = abs(uv.x - 0.5) * 2.0;

                float edgeStart = saturate(_EdgeBlurStart);
                float edgeRange = max(0.0001, 1.0 - edgeStart);
                float edgeT = saturate((xDistance - edgeStart) / edgeRange);
                float edgeBlur = pow(edgeT, max(_EdgeBlurSharpness, 0.001));
                float edgeBlurStrength = max(_EdgeBlurStrength, 0.0);
                float edgeBlurMask = saturate(edgeBlurStrength / 80.0) * edgeBlur;

                float sideTaper = smoothstep(0.18, 1.0, xDistance);
                float sideThin = lerp(1.0, 3.4, sideTaper);
                float verticalGlow = SafePow(1.0 - yDistance, _VerticalSharpness * sideThin);
                float edgeSoftness = lerp(0.065, 0.92, edgeBlurMask);
                float edgeHaze = exp(-pow(yDistance / max(edgeSoftness, 0.001), 2.0)) * edgeBlurMask;
                float horizontalFade = SafePow(1.0 - xDistance, _HorizontalSharpness);
                float taperedCoreWidth = _CoreWidth * lerp(1.0, 0.28, sideTaper);
                float taperedCoreSoftness = max(_CoreSoftness * lerp(1.0, 0.48, sideTaper), 0.0001);
                float core = 1.0 - smoothstep(taperedCoreWidth, taperedCoreWidth + taperedCoreSoftness, yDistance);
                float centerWidth = saturate(_CenterBlendWidth);
                float centerEdge = max(0.001, (1.0 - centerWidth) / max(_CenterBlendFalloff, 0.001));
                float centerFocus = centerWidth <= 0.0001
                    ? 0.0
                    : 1.0 - smoothstep(centerWidth, min(1.0, centerWidth + centerEdge), xDistance);
                float leftSideT = smoothstep(0.0, 1.0, saturate(uv.x * 2.0));
                float rightSideT = smoothstep(0.0, 1.0, saturate((uv.x - 0.5) * 2.0));

                float timeValue = _StageTime > 0.0 ? _StageTime : _Time.y;
                float shimmer = 1.0 + sin((uv.x * 7.0 + timeValue * 0.08) * 6.2831853) * _ShimmerStrength * horizontalFade;

                float cleanLineMask = saturate(verticalGlow * horizontalFade);
                float hazeMask = saturate(edgeHaze * lerp(0.20, 0.58, edgeBlurMask));
                float glowMask = saturate(max(cleanLineMask, hazeMask));
                float coreMask = core * SafePow(1.0 - xDistance, 0.72) * (1.0 - edgeBlurMask * 0.78);

                float3 leftColor = max(_LeftColor.rgb, 0.0);
                float3 rightColor = max(_RightColor.rgb, 0.0);
                float3 midColor = max(_MidColor.rgb, 0.0);
                float3 centerColor = max(_CenterColor.rgb, 0.0);
                float3 sideColor = uv.x < 0.5
                    ? lerp(leftColor, midColor, leftSideT)
                    : lerp(midColor, rightColor, rightSideT);
                sideColor = SaturateColor(sideColor, _ColorSaturation);
                float whiteBlend = saturate(centerFocus * _CenterBlendStrength);
                float3 lineColor = lerp(sideColor, centerColor, whiteBlend);
                float alpha = saturate(_Alpha);
                float3 color = lineColor * glowMask;
                color += centerColor * whiteBlend * (coreMask * (_CoreStrength + 0.35) + glowMask * 0.12);
                color *= _Intensity * shimmer * alpha;

                return float4(color, 0.0);
            }
            ENDCG
        }
    }
}
