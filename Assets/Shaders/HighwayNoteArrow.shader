Shader "Custom/HighwayNoteArrow"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.2, 0.9, 0.35, 1.0)
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
                o.uv = v.uv;
                return o;
            }

            float Cross2D(float2 a, float2 b)
            {
                return (a.x * b.y) - (a.y * b.x);
            }

            float2 ClosestPointOnSegment(float2 p, float2 a, float2 b)
            {
                float2 ab = b - a;
                float denom = max(dot(ab, ab), 0.0001);
                float t = saturate(dot(p - a, ab) / denom);
                return a + (ab * t);
            }

            bool PointInTriangle(float2 p, float2 a, float2 b, float2 c)
            {
                float s0 = Cross2D(b - a, p - a);
                float s1 = Cross2D(c - b, p - b);
                float s2 = Cross2D(a - c, p - c);
                bool allPositive = (s0 >= 0.0) && (s1 >= 0.0) && (s2 >= 0.0);
                bool allNegative = (s0 <= 0.0) && (s1 <= 0.0) && (s2 <= 0.0);
                return allPositive || allNegative;
            }

            float TriangleSignedDistance(float2 p)
            {
                float2 a = float2(0.50, 0.90);
                float2 b = float2(0.86, 0.16);
                float2 c = float2(0.14, 0.16);

                float2 p0 = ClosestPointOnSegment(p, a, b);
                float2 p1 = ClosestPointOnSegment(p, b, c);
                float2 p2 = ClosestPointOnSegment(p, c, a);

                float d0 = dot(p - p0, p - p0);
                float d1 = dot(p - p1, p - p1);
                float d2 = dot(p - p2, p - p2);
                float dist = sqrt(min(d0, min(d1, d2)));

                return PointInTriangle(p, a, b, c) ? dist : -dist;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float signedDistance = TriangleSignedDistance(i.uv);

                float bodyMask = smoothstep(-0.006, 0.010, signedDistance);
                float borderMask = smoothstep(-0.095, -0.006, signedDistance) * (1.0 - bodyMask);
                float glowMask = smoothstep(-0.18, -0.03, signedDistance) * (1.0 - bodyMask);

                if (bodyMask <= 0.001 && borderMask <= 0.001 && glowMask <= 0.001)
                    discard;

                float x = abs((i.uv.x * 2.0) - 1.0);
                float y = saturate((i.uv.y - 0.16) / 0.74);
                float fillLight = pow(saturate(1.0 - x), 1.2) * lerp(0.76, 1.0, y);

                float3 darkFill = _BaseColor.rgb * 0.24;
                float3 midFill = _BaseColor.rgb * 0.40;
                float3 fillColor = lerp(darkFill, midFill, fillLight);
                float3 rimColor = float3(1.0, 1.0, 1.0);

                float3 color = fillColor * bodyMask;
                color += rimColor * saturate(borderMask * 1.75);
                color += rimColor * glowMask * 0.82;

                float alpha = saturate(bodyMask * 0.99 + borderMask * 0.98 + glowMask * 0.42);
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
