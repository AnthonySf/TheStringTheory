Shader "Hidden/StringTheory/SongEndEdgeShine"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _Resolution ("Resolution", Vector) = (1024, 768, 0, 0)
        _TimeValue ("Time", Float) = 0
        _EdgeWidthPx ("Edge Width", Float) = 3
        _CornerRadiusPx ("Corner Radius", Float) = 30
        _SoftnessPx ("Softness", Float) = 1.5
        _BaseAlpha ("Base Alpha", Float) = 0.11
        _ShineAlpha ("Shine Alpha", Float) = 1.00
        _ShineLength ("Shine Length", Float) = 0.10
        _ShineSoftness ("Shine Softness", Float) = 0.08
        _ShineSpeed ("Shine Speed", Float) = 0.22
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Resolution;
            float _TimeValue;
            float _EdgeWidthPx;
            float _CornerRadiusPx;
            float _SoftnessPx;
            float _BaseAlpha;
            float _ShineAlpha;
            float _ShineLength;
            float _ShineSoftness;
            float _ShineSpeed;

            float sdRoundedBox(float2 p, float2 halfSize, float radius)
            {
                float2 q = abs(p) - halfSize + radius;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            float wrappedDistance(float a, float b)
            {
                float d = abs(a - b);
                return min(d, 1.0 - d);
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 resolution = max(_Resolution.xy, float2(2.0, 2.0));
                float2 local = (input.uv - 0.5) * resolution;
                float2 outerHalf = max((resolution * 0.5) - 1.0, float2(2.0, 2.0));
                float outerRadius = min(_CornerRadiusPx, min(outerHalf.x, outerHalf.y) - 1.0);
                float innerInset = max(_EdgeWidthPx, 1.0);
                float2 innerHalf = max(outerHalf - innerInset, float2(1.0, 1.0));
                float innerRadius = max(outerRadius - innerInset, 0.0);

                float outerSdf = sdRoundedBox(local, outerHalf, outerRadius);
                float innerSdf = sdRoundedBox(local, innerHalf, innerRadius);
                float outerMask = 1.0 - smoothstep(0.0, _SoftnessPx, outerSdf);
                float innerMask = 1.0 - smoothstep(0.0, _SoftnessPx, innerSdf);
                float borderMask = saturate(outerMask - innerMask);

                float topDist = 1.0 - input.uv.y;
                float rightDist = 1.0 - input.uv.x;
                float bottomDist = input.uv.y;
                float leftDist = input.uv.x;

                float perimeterProgress;
                if (topDist <= rightDist && topDist <= bottomDist && topDist <= leftDist)
                {
                    perimeterProgress = input.uv.x * 0.25;
                }
                else if (rightDist <= bottomDist && rightDist <= leftDist)
                {
                    perimeterProgress = 0.25 + ((1.0 - input.uv.y) * 0.25);
                }
                else if (bottomDist <= leftDist)
                {
                    perimeterProgress = 0.5 + ((1.0 - input.uv.x) * 0.25);
                }
                else
                {
                    perimeterProgress = 0.75 + (input.uv.y * 0.25);
                }

                float shineHead = frac(_TimeValue * _ShineSpeed);
                float shineDist = wrappedDistance(perimeterProgress, shineHead);
                float shine = 1.0 - smoothstep(_ShineLength, _ShineLength + _ShineSoftness, shineDist);

                float glowReach = max(_EdgeWidthPx * 5.5, 12.0);
                float glowMask = 1.0 - smoothstep(_EdgeWidthPx * 0.25, glowReach, abs(outerSdf));
                float travelingGlow = glowMask * pow(shine, 0.72) * 0.72;
                float borderAlpha = borderMask * (_BaseAlpha + (shine * _ShineAlpha));
                float alpha = saturate(borderAlpha + travelingGlow);

                return fixed4(1.0, 1.0, 1.0, saturate(alpha));
            }
            ENDCG
        }
    }
}
