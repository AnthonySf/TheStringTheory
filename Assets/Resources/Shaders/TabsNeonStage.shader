Shader "Custom/TabsNeonStage"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.002, 0.005, 0.020, 1)
        _HorizonColor ("Horizon Color", Color) = (0.42, 0.34, 1.00, 1)
        _LeftAccentColor ("Left Accent Color", Color) = (0.88, 0.14, 0.88, 1)
        _RightAccentColor ("Right Accent Color", Color) = (0.10, 0.66, 1.00, 1)
        _PulseSpeed ("Pulse Speed", Float) = 0.72
        _HorizonStrength ("Horizon Strength", Float) = 1.00
        _VignetteStrength ("Vignette Strength", Float) = 1.0
        _StageTime ("Stage Time", Float) = 0
        _SkyLineStyle ("Sky Line Style", Float) = 1
        _SkyLineStrength ("Sky Line Strength", Float) = 1.0
        _SkyLineOpacity ("Sky Line Opacity", Float) = 1.0
        _SkyLineReflectionStrength ("Sky Line Reflection Strength", Float) = 0.35
        _SkyDotStrength ("Sky Dot Strength", Float) = 1.0
        _SkySideWashStrength ("Sky Side Wash Strength", Float) = 1.0
        _SkyCoreBrightness ("Sky Core Brightness", Float) = 1.0
        _SkyCoreSize ("Sky Core Size", Float) = 0.52
        _SkyCoreHeight ("Sky Core Height", Float) = 0.46
        _SkyCoreXOffset ("Sky Core X Offset", Float) = 0
        _SkyCoreFalloff ("Sky Core Falloff", Float) = 2.2
        _SkyOutsideDarkness ("Sky Outside Darkness", Float) = 1.0
        _SkyCorePurpleStrength ("Sky Core Purple Strength", Float) = 0.55
        _SkyCorePurpleFalloff ("Sky Core Purple Falloff", Float) = 2.0
        _SkyAuroraRidgeStrength ("Sky Aurora Ridge Strength", Float) = 1.0
        _SkyAuroraRidgeWhiteFalloffPosition ("Sky Aurora Ridge White Falloff Position", Float) = 0.36
        _SkyAuroraRidgeWhiteFalloffSharpness ("Sky Aurora Ridge White Falloff Sharpness", Float) = 0.62
        _SkyAuroraWaveBumpiness ("Sky Aurora Wave Bumpiness", Float) = 1.0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            fixed4 _HorizonColor;
            fixed4 _LeftAccentColor;
            fixed4 _RightAccentColor;
            float _PulseSpeed;
            float _HorizonStrength;
            float _VignetteStrength;
            float _StageTime;
            float _SkyLineStyle;
            float _SkyLineStrength;
            float _SkyLineOpacity;
            float _SkyLineReflectionStrength;
            float _SkyDotStrength;
            float _SkySideWashStrength;
            float _SkyCoreBrightness;
            float _SkyCoreSize;
            float _SkyCoreHeight;
            float _SkyCoreXOffset;
            float _SkyCoreFalloff;
            float _SkyOutsideDarkness;
            float _SkyCorePurpleStrength;
            float _SkyCorePurpleFalloff;
            float _SkyAuroraRidgeStrength;
            float _SkyAuroraRidgeWhiteFalloffPosition;
            float _SkyAuroraRidgeWhiteFalloffSharpness;
            float _SkyAuroraWaveBumpiness;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = normalize(v.vertex.xyz);
                return o;
            }

            float Band(float value, float center, float width, float softness)
            {
                float d = abs(value - center);
                return 1.0 - smoothstep(width, width + max(softness, 0.0001), d);
            }

            float Lobe(float value, float center, float width, float power)
            {
                return pow(saturate(1.0 - abs(value - center) / max(width, 0.0001)), power);
            }

            float DotField(float2 uv, float densityX, float densityY, float radius, float phase)
            {
                float2 cell = frac(float2(uv.x * densityX + phase, uv.y * densityY)) - 0.5;
                return 1.0 - smoothstep(radius, radius * 1.82, length(cell));
            }

            float AuroraWaveOffset(float travel, float amplitude, float phase, float t)
            {
                float bumpiness = max(_SkyAuroraWaveBumpiness, 0.0);
                float waveAmount = amplitude * lerp(0.18, 1.65, saturate(bumpiness / 2.0));
                float frequency = lerp(9.0, 30.0, saturate(bumpiness / 2.2));
                float primary = sin(travel * frequency - t * 0.86 + phase);
                float secondary = sin(travel * (frequency * 1.72 + 3.0) - t * 0.52 + phase * 1.41) * 0.34;
                float micro = sin(travel * (frequency * 2.45 + 7.0) + t * 0.22 + phase * 0.73) * 0.12;
                return (primary + secondary + micro) * waveAmount * smoothstep(0.10, 0.92, travel);
            }

            float HorizonWaveBeam(float x, float y, float anchor, float horizonY, float t, float amplitude, float phase)
            {
                float sideSign = anchor < 0.0 ? -1.0 : 1.0;
                float outward = (x - anchor) * sideSign;
                float travel = saturate(outward / 0.78);
                float active = step(0.0, outward);
                float lift = 0.040 + travel * 0.48;
                float wave = AuroraWaveOffset(travel, amplitude, phase, t);
                float lineY = horizonY + lift + wave;
                float coreWidth = lerp(0.0018, 0.0075, smoothstep(0.00, 1.0, travel));
                float softness = lerp(0.012, 0.062, smoothstep(0.02, 1.0, travel));
                float beam = Band(y, lineY, coreWidth, softness);
                float envelope = active
                    * smoothstep(0.010, 0.080, travel)
                    * smoothstep(1.02, 0.58, travel)
                    * smoothstep(horizonY - 0.040, horizonY + 0.040, y)
                    * smoothstep(0.94, 0.38, y);
                float originSpark = Lobe(x, anchor, 0.035, 2.2) * Band(y, horizonY + 0.030, 0.0025, 0.020);
                return beam * envelope + originSpark * 0.20;
            }

            float HorizonAuroraRidge(float x, float y, float anchor, float horizonY, float t, float amplitude, float phase)
            {
                float sideSign = anchor < 0.0 ? -1.0 : 1.0;
                float outward = (x - anchor) * sideSign;
                float travel = saturate(outward / 0.78);
                float active = step(0.0, outward);

                // Same path as HorizonWaveBeam, shifted only a little upward so this
                // reads as a highlight attached to the beam, not a detached gray band.
                float lift = 0.040 + travel * 0.48;
                float wave = AuroraWaveOffset(travel, amplitude, phase, t);

                float ridgeY = horizonY + lift + wave + lerp(0.006, 0.026, travel);
                float sharpCore = Band(y, ridgeY, lerp(0.0010, 0.0028, travel), lerp(0.0025, 0.0075, travel));
                float softBloom = Band(y, ridgeY, lerp(0.0035, 0.0120, travel), lerp(0.010, 0.032, travel));
                float envelope = active
                    * smoothstep(0.012, 0.070, travel)
                    * smoothstep(1.06, 0.54, travel)
                    * smoothstep(horizonY + 0.010, horizonY + 0.090, y)
                    * smoothstep(0.96, 0.38, y);
                return (sharpCore * lerp(1.45, 0.70, travel) + softBloom * lerp(0.18, 0.26, travel)) * envelope;
            }

            float AuroraRidgeWhiteMix(float x, float anchor)
            {
                float sideSign = anchor < 0.0 ? -1.0 : 1.0;
                float outward = (x - anchor) * sideSign;
                float travel = saturate(outward / 0.78);
                float whiteEnd = saturate(_SkyAuroraRidgeWhiteFalloffPosition);
                float sharpness = saturate(_SkyAuroraRidgeWhiteFalloffSharpness);
                float fadeWidth = lerp(0.56, 0.030, sharpness);
                float fadeStart = max(0.0, whiteEnd - fadeWidth);
                float activeWhite = step(0.001, whiteEnd);
                return activeWhite * (1.0 - smoothstep(fadeStart, whiteEnd, travel));
            }

            fixed3 AuroraRidgeContribution(float ridge, float whiteMix, fixed3 bandColor)
            {
                float tightCore = pow(saturate(ridge), 1.65);
                float coloredBody = max(0.0, ridge - tightCore * 0.30);
                fixed3 saturatedBand = bandColor * 1.30;
                fixed3 whiteCore = fixed3(0.88, 0.97, 1.0);
                fixed3 coreColor = lerp(saturatedBand, whiteCore, saturate(whiteMix));
                return saturatedBand * coloredBody * 0.52 + coreColor * tightCore * 0.22;
            }

            float HorizonWaveCatchLight(float x, float y, float anchor, float horizonY, float t, float amplitude, float phase)
            {
                float sideSign = anchor < 0.0 ? -1.0 : 1.0;
                float outward = (x - anchor) * sideSign;
                float travel = saturate(outward / 0.78);
                float active = step(0.0, outward);

                float lift = 0.040 + travel * 0.48;
                float wave = AuroraWaveOffset(travel, amplitude, phase, t);
                float lineY = horizonY + lift + wave;

                // Local bright pockets near the origin keep the aurora from
                // reading as a flat unicolor ribbon without making every beam
                // globally brighter.
                float nearPocket = exp(-pow((travel - 0.120) * 8.0, 2.0));
                float secondPocket = exp(-pow((travel - 0.255) * 7.2, 2.0)) * 0.52;
                float pocket = (nearPocket + secondPocket)
                    * active
                    * smoothstep(0.025, 0.095, travel)
                    * smoothstep(0.520, 0.180, travel);

                float vertical = Band(y, lineY + 0.003, 0.0038, 0.024);
                float shimmer = 0.88 + 0.12 * sin(t * 2.0 + phase + travel * 21.0);
                return vertical * pocket * shimmer;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float shaderTime = _StageTime > 0.0 ? _StageTime : _Time.y;
                float t = shaderTime * max(_PulseSpeed, 0.01);

                float x = dir.x;
                float y = dir.y;
                float horizonY = -0.105 + sin(t * 0.22) * 0.006;
                float coreX = x - _SkyCoreXOffset;
                float coreWidth = lerp(0.08, 0.92, saturate(_SkyCoreSize));
                float coreHeight = lerp(0.025, 0.80, saturate(_SkyCoreHeight));
                float horizontalCore = pow(saturate(1.0 - abs(coreX) / max(coreWidth, 0.001)), max(_SkyCoreFalloff, 0.001));
                float verticalCore = pow(saturate(1.0 - abs(y - (horizonY + 0.035)) / max(coreHeight, 0.001)), max(_SkyCoreFalloff, 0.001));
                float center = horizontalCore * verticalCore;
                float centerTight = pow(center, 2.6);
                float purpleWidth = lerp(coreWidth, 1.0, 0.42);
                float purpleHeight = max(coreHeight * 1.85, 0.04);
                float purpleHorizontal = pow(saturate(1.0 - abs(coreX) / max(purpleWidth, 0.001)), max(_SkyCorePurpleFalloff, 0.001));
                float purpleVertical = pow(saturate(1.0 - abs(y - (horizonY + 0.055)) / max(purpleHeight, 0.001)), max(_SkyCorePurpleFalloff, 0.001));
                float purpleCore = purpleHorizontal * purpleVertical;
                float purpleHalo = saturate(purpleCore - center * 0.35) * max(_SkyCorePurpleStrength, 0.0);
                float whiteCore = centerTight * max(_SkyCoreBrightness, 0.0);
                float side = smoothstep(0.34, 0.98, abs(x));
                float upper = smoothstep(horizonY, 0.88, y);
                float lower = smoothstep(horizonY + 0.02, -0.72, y);

                fixed3 color = _BaseColor.rgb;
                color = lerp(color, fixed3(0.012, 0.020, 0.072), upper * 0.72);
                color = lerp(color, fixed3(0.010, 0.003, 0.018), lower * 0.72);
                float outsideCore = saturate(1.0 - center);
                float outsideControl = max(_SkyOutsideDarkness, 0.05);
                float outsideGain = outsideControl >= 1.0
                    ? pow(1.0 / outsideControl, 1.45)
                    : lerp(1.0, 2.25, 1.0 - outsideControl);
                color += fixed3(0.030, 0.044, 0.180) * Band(y, horizonY + 0.18, 0.20, 0.34) * (0.36 + center * 0.78);
                color += fixed3(0.030, 0.012, 0.070) * Band(y, horizonY - 0.24, 0.24, 0.30) * lower;

                float pulse = 0.92 + 0.08 * sin(t * 1.18);
                float atmosphericBloom = Band(y, horizonY + 0.020, 0.055, 0.220) * _HorizonStrength;
                float portal = Lobe(x, 0.0, 0.52, 2.0) * Band(y, horizonY + 0.020, 0.105, 0.260);
                float portalCore = Lobe(x, 0.0, 0.26, 2.3) * Band(y, horizonY + 0.002, 0.045, 0.150);
                color += _HorizonColor.rgb * atmosphericBloom * (0.14 + centerTight * 0.34 + purpleHalo * 0.36);
                color += _HorizonColor.rgb * (portal * (0.22 + purpleHalo * 0.10) + portalCore * 0.30 * pulse);
                color += fixed3(1.0, 0.94, 1.0) * atmosphericBloom * whiteCore * 0.22;
                color += fixed3(1.0, 0.94, 1.0) * portalCore * whiteCore * 0.42;

                float leftWash = Lobe(x, -0.86, 0.50, 1.7) * Band(y, horizonY + 0.12, 0.36, 0.34);
                float rightWash = Lobe(x, 0.86, 0.54, 1.7) * Band(y, horizonY + 0.12, 0.36, 0.34);
                color += _LeftAccentColor.rgb * leftWash * 0.22 * _SkySideWashStrength;
                color += _RightAccentColor.rgb * rightWash * 0.24 * _SkySideWashStrength;

                float waveMask = side * smoothstep(horizonY - 0.08, horizonY + 0.08, y) * smoothstep(0.90, 0.40, y);
                float waveA = Band(y, horizonY + 0.16 + sin(abs(x) * 5.2 + t * 0.42) * 0.042, 0.0032, 0.020);
                float waveB = Band(y, horizonY + 0.28 + sin(abs(x) * 3.9 - t * 0.30 + 1.6) * 0.055, 0.0028, 0.018);
                float waveC = Band(y, horizonY + 0.39 + sin(abs(x) * 6.0 + t * 0.22 + 2.0) * 0.034, 0.0024, 0.014);
                float legacyWaveGlow = (waveA * 1.00 + waveB * 0.76 + waveC * 0.48) * waveMask;
                float lowerBeamGlow =
                    HorizonWaveBeam(x, y, -0.24, horizonY, t, 0.045, 0.00) +
                    HorizonWaveBeam(x, y,  0.24, horizonY, t, 0.045, 1.35);
                float upperBeamGlow =
                    HorizonWaveBeam(x, y, -0.46, horizonY, t, 0.060, 2.20) * 0.72 +
                    HorizonWaveBeam(x, y,  0.46, horizonY, t, 0.060, 3.10) * 0.72;
                float lineStyle = step(0.5, _SkyLineStyle);
                float lineGain = lerp(0.54, 0.70, lineStyle);
                float lineOpacity = saturate(_SkyLineOpacity);
                fixed3 sideWaveColor = lerp(_LeftAccentColor.rgb, _RightAccentColor.rgb, step(0.0, x)) * legacyWaveGlow;
                fixed3 layeredWaveColor = _LeftAccentColor.rgb * lowerBeamGlow + _RightAccentColor.rgb * upperBeamGlow;
                color += lerp(sideWaveColor, layeredWaveColor, lineStyle) * lineGain * _SkyLineStrength * lineOpacity;

                float catchLight =
                    HorizonWaveCatchLight(x, y, -0.24, horizonY, t, 0.045, 0.00) +
                    HorizonWaveCatchLight(x, y,  0.24, horizonY, t, 0.045, 1.35) +
                    HorizonWaveCatchLight(x, y, -0.46, horizonY, t, 0.060, 2.20) * 0.58 +
                    HorizonWaveCatchLight(x, y,  0.46, horizonY, t, 0.060, 3.10) * 0.58;
                color += fixed3(0.66, 0.88, 1.0) * catchLight * max(_SkyLineReflectionStrength, 0.0) * lineStyle * lineOpacity;

                float auroraGain = max(_SkyAuroraRidgeStrength, 0.0) * lineStyle * lineOpacity;
                float lowerRidgeA = HorizonAuroraRidge(x, y, -0.24, horizonY, t, 0.045, 0.00);
                float lowerRidgeB = HorizonAuroraRidge(x, y,  0.24, horizonY, t, 0.045, 1.35);
                float upperRidgeA = HorizonAuroraRidge(x, y, -0.46, horizonY, t, 0.060, 2.20) * 0.62;
                float upperRidgeB = HorizonAuroraRidge(x, y,  0.46, horizonY, t, 0.060, 3.10) * 0.62;
                fixed3 ridgeColor =
                    AuroraRidgeContribution(lowerRidgeA, AuroraRidgeWhiteMix(x, -0.24), _LeftAccentColor.rgb) +
                    AuroraRidgeContribution(lowerRidgeB, AuroraRidgeWhiteMix(x,  0.24), _LeftAccentColor.rgb) +
                    AuroraRidgeContribution(upperRidgeA, AuroraRidgeWhiteMix(x, -0.46), _RightAccentColor.rgb) +
                    AuroraRidgeContribution(upperRidgeB, AuroraRidgeWhiteMix(x,  0.46), _RightAccentColor.rgb);
                color += ridgeColor * auroraGain;

                float dotMask = side * Band(y, horizonY + 0.02, 0.40, 0.28);
                float dots = DotField(float2(abs(x) * 0.92, y * 0.62 + 0.50), 46.0, 36.0, 0.058, t * 0.035);
                color += lerp(_LeftAccentColor.rgb, _RightAccentColor.rgb, step(0.0, x)) * dots * dotMask * 0.155 * _SkyDotStrength;

                float floorSheen = lower * (0.08 + center * 0.16);
                color += _HorizonColor.rgb * floorSheen * 0.12;

                float skyAreaMask = smoothstep(horizonY - 0.26, horizonY + 0.04, y);
                color *= lerp(1.0, outsideGain, outsideCore * skyAreaMask * 0.98);

                float vignetteX = smoothstep(1.08, 0.18, abs(x));
                float vignetteY = smoothstep(1.06, 0.02, abs(y - 0.02) * 1.30);
                color *= lerp(0.52, 1.0, saturate(vignetteX * vignetteY * _VignetteStrength));

                return fixed4(saturate(color), 1.0);
            }
            ENDCG
        }
    }
}
