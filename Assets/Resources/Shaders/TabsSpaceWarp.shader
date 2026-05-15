Shader "Custom/TabsSpaceWarp"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.015, 0.028, 0.09, 1)
        _GlowColor ("Glow Color", Color) = (0.16, 0.82, 1.0, 1)
        _AccentColor ("Accent Color", Color) = (0.50, 0.38, 0.96, 1)
        _FlowSpeed ("Flow Speed", Float) = 0.58
        _LineIntensity ("Line Intensity", Float) = 1.35
        _SparkIntensity ("Spark Intensity", Float) = 1.15
        _BackdropMode ("Backdrop Mode", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            fixed4 _GlowColor;
            fixed4 _AccentColor;
            float _FlowSpeed;
            float _LineIntensity;
            float _SparkIntensity;
            float _BackdropMode;

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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float LineMask(float coord, float thickness, float softness)
            {
                float d = abs(frac(coord) - 0.5);
                return 1.0 - smoothstep(thickness, thickness + max(softness, 0.0001), d);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y * max(_FlowSpeed, 0.01);
                float backdrop = saturate(_BackdropMode);

                float2 centered = uv * 2.0 - 1.0;
                float radial = saturate(1.0 - dot(centered * float2(0.85, 1.20), centered * float2(0.85, 1.20)));
                float horizonBand = saturate(1.0 - abs(uv.y - 0.22) * 3.6);

                float depthT = saturate(pow(saturate(uv.y), 0.78));
                float perspectiveX = centered.x / lerp(0.16, 1.35, depthT);

                float majorRails = LineMask((perspectiveX * 2.6) + 0.5, 0.072, 0.05);
                float minorRails = LineMask((perspectiveX * 5.2) + 0.5, 0.034, 0.024) * 0.55;
                float microRails = LineMask((perspectiveX * 10.0) + 0.5, 0.012, 0.012) * 0.16;
                float railMask = saturate(majorRails + minorRails + microRails);

                float sweepBands = LineMask((uv.y + t * 0.34) * 4.0, 0.16, 0.08) * 0.22;
                float centralGlow = pow(saturate(1.0 - abs(perspectiveX) * 0.9), 2.4) * (0.18 + depthT * 0.55);

                float2 sparkCell = floor(float2((perspectiveX * 6.5) + 18.0, (uv.y + t * 0.72) * 22.0));
                float sparkSeed = Hash21(sparkCell);
                float spark = step(0.988, sparkSeed);
                float sparkPulse = smoothstep(0.25, 1.0, sin((t * 8.5) + (sparkSeed * 6.2831853)) * 0.5 + 0.5);
                float sparkMask = spark * sparkPulse * (0.22 + depthT * 0.78);

                float floorGlow = centralGlow + sweepBands * 0.8;
                float lineEnergy = railMask * _LineIntensity;
                float sparkEnergy = sparkMask * _SparkIntensity;
                float floorEnergy = lineEnergy + sparkEnergy + floorGlow;

                float2 starCell = floor(uv * float2(110.0, 70.0));
                float starNoise = Hash21(starCell);
                float stars = step(0.9925, starNoise) * (0.35 + 0.65 * Hash21(starCell + 13.4));
                float drift = Hash21(floor((uv + float2(t * 0.02, 0.0)) * float2(36.0, 18.0)));
                float nebula = saturate(radial * 1.35 + pow(max(0.0, 1.0 - abs(centered.x) * 1.2), 2.0) * 0.2);
                float backdropEnergy = nebula * 0.55 + stars * _SparkIntensity * 0.8 + horizonBand * 0.2 + drift * 0.05;

                float energy = lerp(floorEnergy, backdropEnergy, backdrop);

                fixed3 color = _BaseColor.rgb;
                color += _GlowColor.rgb * energy;
                color += _AccentColor.rgb * (sparkEnergy + sweepBands * 0.12 + stars * backdrop * 0.6);
                color = lerp(color, _GlowColor.rgb, horizonBand * backdrop * 0.22);

                float alpha = lerp(0.96, 0.92, backdrop);
                alpha = saturate(alpha + energy * 0.08);
                return fixed4(saturate(color), alpha);
            }
            ENDCG
        }
    }
}
