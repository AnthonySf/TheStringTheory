Shader "Custom/HighwaySlideRibbon"
{
    Properties
    {
        _CenterColor ("Center Color", Color) = (0.15, 0.45, 1.0, 0.34)
        _EdgeColor ("Edge Color", Color) = (0.8, 0.94, 1.0, 0.9)
        _EmissionColor ("Emission Color", Color) = (0.6, 0.85, 1.0, 0.0)
        _HalfWidth ("Half Width", Float) = 0.18
        _CurveP0 ("Curve P0", Vector) = (0, 0, 0, 0)
        _CurveP1 ("Curve P1", Vector) = (0, 0, 0.6, 0)
        _CurveP2 ("Curve P2", Vector) = (1, 0, 1.2, 0)
        _CurveP3 ("Curve P3", Vector) = (1, 0, 1.8, 0)
        _VisibleStart01 ("Visible Start 01", Range(0, 1)) = 0
        _VisibleFadeSoftness01 ("Visible Fade Softness 01", Range(0.001, 0.05)) = 0.015
        _LengthFadeSoftness01 ("Length Fade Softness 01", Range(0.001, 0.08)) = 0.02
        _FlatLightStrength ("Flat Light Strength", Range(0, 1)) = 0
        _PathMode ("Path Mode", Range(0, 1)) = 0
        _CornerRoundness ("Corner Roundness", Float) = 0
        _DarkBandStart01 ("Dark Band Start 01", Range(0, 1)) = 0
        _DarkBandEnd01 ("Dark Band End 01", Range(0, 1)) = 0
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
            float _HalfWidth;
            float4 _CurveP0;
            float4 _CurveP1;
            float4 _CurveP2;
            float4 _CurveP3;
            float _VisibleStart01;
            float _VisibleFadeSoftness01;
            float _LengthFadeSoftness01;
            float _FlatLightStrength;
            float _PathMode;
            float _CornerRoundness;
            float _DarkBandStart01;
            float _DarkBandEnd01;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float riseStrength : TEXCOORD1;
            };

            float3 BezierPoint(float3 p0, float3 p1, float3 p2, float3 p3, float t)
            {
                float omt = 1.0 - t;
                float omt2 = omt * omt;
                float t2 = t * t;
                return (omt2 * omt * p0) +
                       (3.0 * omt2 * t * p1) +
                       (3.0 * omt * t2 * p2) +
                       (t2 * t * p3);
            }

            float3 BezierTangent(float3 p0, float3 p1, float3 p2, float3 p3, float t)
            {
                float omt = 1.0 - t;
                return (3.0 * omt * omt * (p1 - p0)) +
                       (6.0 * omt * t * (p2 - p1)) +
                       (3.0 * t * t * (p3 - p2));
            }

            void StepPathPointAndTangent(float3 p0, float3 p1, float3 p2, float3 p3, float t, out float3 pos, out float3 tangent)
            {
                if (_CornerRoundness <= 0.0001)
                {
                    float len01 = max(distance(p0, p1), 0.0001);
                    float len12 = max(distance(p1, p2), 0.0001);
                    float len23 = max(distance(p2, p3), 0.0001);
                    float total = len01 + len12 + len23;
                    float split01 = len01 / total;
                    float split12 = (len01 + len12) / total;

                    if (t <= split01)
                    {
                        float localT = split01 > 0.0001 ? t / split01 : 0.0;
                        pos = lerp(p0, p1, localT);
                        tangent = normalize(p1 - p0);
                        return;
                    }

                    if (t <= split12)
                    {
                        float denom = max(0.0001, split12 - split01);
                        float localT = (t - split01) / denom;
                        pos = lerp(p1, p2, localT);
                        tangent = normalize(p2 - p1);
                        return;
                    }

                    float denomTop = max(0.0001, 1.0 - split12);
                    float localTTop = (t - split12) / denomTop;
                    pos = lerp(p2, p3, localTTop);
                    tangent = normalize(p3 - p2);
                    return;
                }

                float len01 = max(distance(p0, p1), 0.0001);
                float len12 = max(distance(p1, p2), 0.0001);
                float len23 = max(distance(p2, p3), 0.0001);
                float3 dir01 = normalize(p1 - p0);
                float3 dir12 = normalize(p2 - p1);
                float3 dir23 = normalize(p3 - p2);
                float radius1 = min(_CornerRoundness, min(len01, len12) * 0.5);
                float radius2 = min(_CornerRoundness, min(len12, len23) * 0.5);
                float3 line1End = p1 - (dir01 * radius1);
                float3 corner1End = p1 + (dir12 * radius1);
                float3 corner2Start = p2 - (dir12 * radius2);
                float3 corner2End = p2 + (dir23 * radius2);

                float seg0 = max(distance(p0, line1End), 0.0001);
                float seg1 = max(distance(line1End, p1) + distance(p1, corner1End), 0.0001);
                float seg2 = max(distance(corner1End, corner2Start), 0.0001);
                float seg3 = max(distance(corner2Start, p2) + distance(p2, corner2End), 0.0001);
                float seg4 = max(distance(corner2End, p3), 0.0001);
                float total = seg0 + seg1 + seg2 + seg3 + seg4;
                float split0 = seg0 / total;
                float split1 = (seg0 + seg1) / total;
                float split2 = (seg0 + seg1 + seg2) / total;
                float split3 = (seg0 + seg1 + seg2 + seg3) / total;

                if (t <= split0)
                {
                    float localT = split0 > 0.0001 ? t / split0 : 0.0;
                    pos = lerp(p0, line1End, localT);
                    tangent = dir01;
                    return;
                }

                if (t <= split1)
                {
                    float denom1 = max(0.0001, split1 - split0);
                    float localT = (t - split0) / denom1;
                    float omt = 1.0 - localT;
                    pos = (omt * omt * line1End) + (2.0 * omt * localT * p1) + (localT * localT * corner1End);
                    tangent = normalize((2.0 * omt * (p1 - line1End)) + (2.0 * localT * (corner1End - p1)));
                    return;
                }

                if (t <= split2)
                {
                    float denom2 = max(0.0001, split2 - split1);
                    float localT = (t - split1) / denom2;
                    pos = lerp(corner1End, corner2Start, localT);
                    tangent = normalize(corner2Start - corner1End);
                    return;
                }

                if (t <= split3)
                {
                    float denom3 = max(0.0001, split3 - split2);
                    float localT = (t - split2) / denom3;
                    float omt = 1.0 - localT;
                    pos = (omt * omt * corner2Start) + (2.0 * omt * localT * p2) + (localT * localT * corner2End);
                    tangent = normalize((2.0 * omt * (p2 - corner2Start)) + (2.0 * localT * (corner2End - p2)));
                    return;
                }

                float denom4 = max(0.0001, 1.0 - split3);
                float localT4 = (t - split3) / denom4;
                pos = lerp(corner2End, p3, localT4);
                tangent = dir23;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float t = saturate(v.uv.y);
                float side = lerp(-1.0, 1.0, v.uv.x);
                float3 p0 = _CurveP0.xyz;
                float3 p1 = _CurveP1.xyz;
                float3 p2 = _CurveP2.xyz;
                float3 p3 = _CurveP3.xyz;

                float3 curvePos;
                float3 tangent;
                if (_PathMode > 0.5)
                {
                    StepPathPointAndTangent(p0, p1, p2, p3, t, curvePos, tangent);
                }
                else
                {
                    curvePos = BezierPoint(p0, p1, p2, p3, t);
                    tangent = normalize(BezierTangent(p0, p1, p2, p3, t));
                }
                if (dot(tangent, tangent) < 0.0001)
                    tangent = float3(0.0, 0.0, 1.0);

                float3 upAxis = float3(0.0, 1.0, 0.0);
                float3 widthAxis = cross(upAxis, tangent);
                if (dot(widthAxis, widthAxis) < 0.0001)
                    widthAxis = float3(1.0, 0.0, 0.0);
                widthAxis = normalize(widthAxis);

                float3 localPos = curvePos + (widthAxis * (side * _HalfWidth));
                float4 worldPos = mul(unity_ObjectToWorld, float4(localPos, 1.0));
                o.pos = mul(UNITY_MATRIX_VP, worldPos);
                o.uv = float2(v.uv.x, t);
                float horizontalMagnitude = max(0.0001, length(float2(tangent.x, tangent.z)));
                float slope = abs(tangent.y) / horizontalMagnitude;
                o.riseStrength = smoothstep(0.06, 0.38, slope);
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
