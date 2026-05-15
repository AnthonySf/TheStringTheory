Shader "Custom/HighwayMuteSymbol"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.92, 0.03, 0.03, 1.0)
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

            float SegmentDistance(float2 p, float2 a, float2 b)
            {
                float2 ab = b - a;
                float denom = max(dot(ab, ab), 0.0001);
                float t = saturate(dot(p - a, ab) / denom);
                float2 closest = a + (ab * t);
                return length(p - closest);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv;

                float diagA = SegmentDistance(p, float2(0.18, 0.18), float2(0.82, 0.82));
                float diagB = SegmentDistance(p, float2(0.18, 0.82), float2(0.82, 0.18));
                float distanceToX = min(diagA, diagB);

                float bodyMask = 1.0 - smoothstep(0.105, 0.142, distanceToX);
                float glowMask = 1.0 - smoothstep(0.142, 0.34, distanceToX);

                if (bodyMask <= 0.001 && glowMask <= 0.001)
                    discard;

                float centerBoost = 1.0 - saturate(distanceToX / 0.11);
                float crossBoost = saturate((0.16 - abs(diagA - diagB)) / 0.16);

                float3 darkRed = _BaseColor.rgb * 0.24;
                float3 brightRed = lerp(_BaseColor.rgb, float3(1.0, 0.12, 0.12), 0.14);
                float3 fillColor = lerp(darkRed, brightRed, saturate(centerBoost * 0.78 + crossBoost * 0.18));
                float3 glowColor = float3(1.0, 0.06, 0.06);

                float3 color = fillColor * bodyMask;
                color += glowColor * glowMask * 1.95;

                float alpha = saturate(bodyMask * 1.0 + glowMask * 0.78);
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
