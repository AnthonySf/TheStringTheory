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
                float seamLift = saturate(1.0 - (abs(i.uv.y - _SplitY) / max(0.0001, _SplitSoftness * 2.4)));
                float backHalfMask = saturate(splitBlend + (seamLift * 0.22));
                float frontHalfMask = saturate((1.0 - splitBlend) + (seamLift * 0.22));
                float frontArcMask = smoothstep(0.10, 0.92, -sin(angle));
                frontArcMask *= frontHalfMask;

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
                float accentLuma = dot(_AccentColor.rgb, float3(0.2126, 0.7152, 0.0722));
                float darkAccentWeight = saturate((0.12 - accentLuma) / 0.12);
                float darkSwirlMask = saturate(pow(swirlMask, 0.34) * 1.95);

                if (_HalfMode > 0.5)
                {
                    float frontRing = ring;
                    float frontSwirl = swirlMask;
                    float frontDeepVoid = deepVoid;
                    float frontInnerShadow = innerShadow;
                    float frontEmberRim = emberRim;
                    float frontRimInnerGlow = rimInnerGlow;
                    float frontArcAccent = frontRing * frontArcMask;

                    fixed3 frontColor = lerp(_BaseColor.rgb, _CoreColor.rgb, frontDeepVoid);
                    frontColor *= lerp(0.78, 1.0, ripple * 0.08);
                    frontColor = lerp(frontColor, _RimColor.rgb, frontRing * 0.85);
                    frontColor += _RimColor.rgb * (frontEmberRim * 0.84);
                    frontColor += _AccentColor.rgb * (frontRimInnerGlow * 0.14);
                    frontColor += _AccentColor.rgb * (frontSwirl * 0.82);
                    frontColor += _CoreColor.rgb * (frontInnerShadow * 0.08);
                    frontColor += _RimColor.rgb * (frontArcAccent * (0.55 + frontRimInnerGlow * 0.35));
                    frontColor += _AccentColor.rgb * (frontArcAccent * 0.06);
                    float frontDarkSwirlMask = saturate(pow(darkSwirlMask, 0.82) * 1.08);
                    fixed3 darkFrontColor = lerp(frontColor, _AccentColor.rgb, frontDarkSwirlMask);
                    frontColor = lerp(frontColor, darkFrontColor, darkAccentWeight);

                    float frontAlpha = lerp(_AlphaFloor, 1.0, frontDeepVoid);
                    frontAlpha = max(frontAlpha, frontDeepVoid * _CoreColor.a);
                    frontAlpha = max(frontAlpha, frontRing * _RimColor.a);
                    frontAlpha = max(frontAlpha, frontSwirl * _AccentColor.a);
                    frontAlpha = max(frontAlpha, frontInnerShadow * _BaseColor.a);
                    frontAlpha = max(frontAlpha, frontArcAccent * _RimColor.a);
                    frontAlpha = lerp(frontAlpha, 1.0, frontDarkSwirlMask * darkAccentWeight);
                    frontAlpha = saturate(frontAlpha * portalMask * frontHalfMask);
                    return fixed4(saturate(frontColor), saturate(frontAlpha));
                }

                fixed3 color = lerp(_BaseColor.rgb, _CoreColor.rgb, deepVoid);
                color *= lerp(0.78, 1.0, ripple * 0.08);
                color = lerp(color, _RimColor.rgb, ring * 0.85);
                color += _RimColor.rgb * (emberRim * 0.84);
                color += _AccentColor.rgb * (rimInnerGlow * 0.14);
                color += _AccentColor.rgb * (swirlMask * 0.82);
                color += _CoreColor.rgb * (innerShadow * 0.08);
                float backDarkSwirlMask = saturate(pow(darkSwirlMask, 0.82) * 1.08);
                fixed3 darkBackColor = lerp(color, _AccentColor.rgb, backDarkSwirlMask);
                color = lerp(color, darkBackColor, darkAccentWeight);
                color = saturate(color);

                float alpha = lerp(_AlphaFloor, 1.0, deepVoid);
                alpha = max(alpha, deepVoid * _CoreColor.a);
                alpha = max(alpha, ring * _RimColor.a);
                alpha = max(alpha, swirlMask * _AccentColor.a);
                alpha = max(alpha, shadow * _BaseColor.a);
                alpha = lerp(alpha, 1.0, backDarkSwirlMask * darkAccentWeight);
                alpha = saturate(alpha * portalMask);
                if (_HalfMode < -0.5)
                    alpha *= backHalfMask;

                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
