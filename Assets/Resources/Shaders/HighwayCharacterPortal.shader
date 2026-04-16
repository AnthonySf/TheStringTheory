Shader "Custom/HighwayCharacterPortal"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.07, 0.10, 0.19, 1)
        _RimColor ("Rim Color", Color) = (0.98, 0.43, 0.14, 1)
        _AccentColor ("Accent Color", Color) = (0.94, 0.57, 0.24, 1)
        _CoreColor ("Core Color", Color) = (0.03, 0.05, 0.12, 1)
        _GlowStrength ("Glow Strength", Range(0, 4)) = 1.82
        _AlphaFloor ("Alpha Floor", Range(0, 1)) = 0.78
        _SwirlSpeed ("Swirl Speed", Range(0.05, 4)) = 1.15
        _SwirlSharpness ("Swirl Sharpness", Range(0.5, 4)) = 1.55
        _RingThickness ("Ring Thickness", Range(0.02, 0.5)) = 0.07
        _Softness ("Softness", Range(0.01, 0.2)) = 0.065
        _RimSoftness ("Rim Softness", Range(0.001, 0.2)) = 0.02
        _HalfMode ("Half Mode", Range(-1, 1)) = 0
        _SplitY ("Split Y", Range(0, 1)) = 0.5
        _SplitSoftness ("Split Softness", Range(0.001, 0.2)) = 0.035
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
            fixed4 _RimColor;
            fixed4 _AccentColor;
            fixed4 _CoreColor;
            float _GlowStrength;
            float _AlphaFloor;
            float _SwirlSpeed;
            float _SwirlSharpness;
            float _RingThickness;
            float _Softness;
            float _RimSoftness;
            float _HalfMode;
            float _SplitY;
            float _SplitSoftness;

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

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv * 2.0 - 1.0;
                float radius = length(uv);
                float angle = atan2(uv.y, uv.x);
                float t = _Time.y * _SwirlSpeed;

                float portalMask = 1.0 - smoothstep(1.0 - _Softness, 1.0, radius);
                float crispRimSoftness = max(0.0015, _RimSoftness * 0.28);
                float ringOuter = 1.0 - smoothstep(1.0 - crispRimSoftness, 1.0, radius);
                float ringInner = 1.0 - smoothstep(1.0 - _RingThickness - crispRimSoftness, 1.0 - _RingThickness + crispRimSoftness, radius);
                float ring = saturate(ringOuter * (1.0 - ringInner));
                float splitBlend = smoothstep(_SplitY - _SplitSoftness, _SplitY + _SplitSoftness, i.uv.y);
                float backHalfMask = 1.0 - splitBlend;
                float frontArcMask = smoothstep(0.10, 0.92, -sin(angle));
                frontArcMask *= smoothstep(0.72, 0.95, radius);
                frontArcMask *= splitBlend;

                float core = pow(saturate(1.0 - radius), 1.55);
                float spiralA = 0.5 + 0.5 * sin((angle * 7.0) - (radius * 17.0) - (t * 4.6));
                float spiralB = 0.5 + 0.5 * sin((angle * -5.5) + (radius * 13.0) - (t * 2.9));
                float ripple = 0.5 + 0.5 * sin((radius * 26.0) - (t * 6.5));
                float deepVoid = pow(core, 2.35);
                float innerShadow = pow(core, 1.8);
                float swirlFlow = (spiralA * 0.56) + (spiralB * 0.44);
                float swirlDrift = 0.5 + 0.5 * sin((angle * 2.4) - (radius * 7.5) - (t * 1.35));
                float swirlBody = (swirlFlow * 0.72) + (swirlDrift * 0.28);
                float swirlMask = pow(saturate(swirlBody), max(0.5, _SwirlSharpness));
                swirlMask *= pow(core, 1.12) * (0.82 + ripple * 0.18);
                swirlMask *= 1.0 - saturate(ring * 0.42);
                float shadow = saturate(core * 1.1);
                float emberRim = saturate(pow(ring, 0.78) * (_GlowStrength * 1.02));
                float rimInnerGlow = saturate(pow(ring, 1.12) * (_GlowStrength * 0.48));

                if (_HalfMode > 0.5)
                {
                    float frontRing = ring * frontArcMask;
                    fixed3 frontColor = _RimColor.rgb * (frontRing * (1.0 + rimInnerGlow * 0.55));
                    frontColor += _AccentColor.rgb * (frontRing * 0.12);
                    float frontAlpha = frontRing * _RimColor.a;
                    return fixed4(saturate(frontColor), saturate(frontAlpha));
                }

                if (_HalfMode < -0.5)
                {
                    swirlMask *= backHalfMask;
                    deepVoid *= lerp(1.0, backHalfMask, 0.88);
                    innerShadow *= lerp(1.0, backHalfMask, 0.82);
                }

                fixed3 color = lerp(_BaseColor.rgb, _CoreColor.rgb, deepVoid);
                color *= lerp(0.78, 1.0, ripple * 0.08);
                color = lerp(color, _RimColor.rgb, ring * 0.85);
                color += _RimColor.rgb * (emberRim * 0.84);
                color += _AccentColor.rgb * (rimInnerGlow * 0.14);
                color += _AccentColor.rgb * (swirlMask * 0.82);
                color += _CoreColor.rgb * (innerShadow * 0.08);
                color = saturate(color);

                float alpha = lerp(_AlphaFloor, 1.0, deepVoid);
                alpha = max(alpha, deepVoid * _CoreColor.a);
                alpha = max(alpha, ring * _RimColor.a);
                alpha = max(alpha, swirlMask * _AccentColor.a);
                alpha = max(alpha, shadow * _BaseColor.a);
                alpha = saturate(alpha * portalMask);

                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
