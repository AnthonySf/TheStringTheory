Shader "Custom/ArcadeDrumKit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseMap ("Base Map", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _RmoMap ("Roughness Metallic Occlusion", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _RimColor ("Rim Color", Color) = (0.05, 0.42, 0.58, 1)
        _AccentColor ("Accent Color", Color) = (0.58, 0.06, 0.44, 1)
        _ShellGlowColor ("Shell Glow Color", Color) = (0.62, 0.04, 0.07, 1)
        _FakeLightDirection ("Fake Light Direction", Vector) = (-0.35, 0.86, -0.38, 0)
        _StageExposure ("Stage Exposure", Range(0, 2)) = 0.58
        _ShadowColor ("Shadow Color", Color) = (0.018, 0.026, 0.065, 1)
        _KeyLightColor ("Key Light Color", Color) = (0.72, 0.82, 0.94, 1)
        _FillLightColor ("Fill Light Color", Color) = (0.11, 0.055, 0.24, 1)
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.095
        _KeyLightStrength ("Key Light Strength", Range(0, 2)) = 0.78
        _TopLightStrength ("Top Light Strength", Range(0, 1)) = 0.10
        _FillLightStrength ("Fill Light Strength", Range(0, 1)) = 0.035
        _PulseSpeed ("Pulse Speed", Float) = 4.2
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.10
        _StripeStrength ("Stripe Strength", Range(0, 1)) = 0.035
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.12
        _HitGlowEdgeStrength ("Hit Glow Edge Strength", Range(0, 5)) = 1.65
        _TargetGlowColor ("Target Glow Color", Color) = (0, 0, 0, 1)
        _TargetGlowStrength ("Target Glow Strength", Range(0, 1)) = 0
        _TargetGlowCenter ("Target Glow Center", Vector) = (0, 0, 0, 0)
        _TargetGlowExtents ("Target Glow Extents", Vector) = (1, 1, 1, 0)
        _TargetGlowPlaneMask ("Target Glow Plane Mask", Vector) = (1, 1, 0, 0)
        _TargetGlowDepthSide ("Target Glow Depth Side", Float) = 1
        _TargetGlowSurfaceMode ("Target Glow Surface Mode", Float) = 0
        _DrumImpactColor ("Drum Impact Color", Color) = (0, 0, 0, 1)
        _DrumImpactStrength ("Drum Impact Strength", Range(0, 1)) = 0
        _DrumImpactProgress ("Drum Impact Progress", Range(0, 1)) = 1
        _DrumSuccessImpactColor ("Drum Success Impact Color", Color) = (0, 0, 0, 1)
        _DrumSuccessImpactStrength ("Drum Success Impact Strength", Range(0, 1)) = 0
        _DrumSuccessImpactProgress ("Drum Success Impact Progress", Range(0, 1)) = 1
        _SrcBlend ("Src Blend", Float) = 1
        _DstBlend ("Dst Blend", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
        _Cull ("Cull", Float) = 0
        _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags { "Queue" = "Geometry+80" "RenderType" = "Opaque" }
        Blend [_SrcBlend] [_DstBlend]
        Cull [_Cull]
        ZWrite [_ZWrite]
        ZTest [_ZTest]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _BumpMap;
            sampler2D _RmoMap;
            fixed4 _Color;
            fixed4 _BaseColor;
            float4 _RimColor;
            float4 _AccentColor;
            float4 _ShellGlowColor;
            float4 _FakeLightDirection;
            float _StageExposure;
            float4 _ShadowColor;
            float4 _KeyLightColor;
            float4 _FillLightColor;
            float _AmbientStrength;
            float _KeyLightStrength;
            float _TopLightStrength;
            float _FillLightStrength;
            float _PulseSpeed;
            float _PulseStrength;
            float _StripeStrength;
            float _RimStrength;
            float _HitGlowEdgeStrength;
            float4 _TargetGlowColor;
            float _TargetGlowStrength;
            float4 _TargetGlowCenter;
            float4 _TargetGlowExtents;
            float4 _TargetGlowPlaneMask;
            float _TargetGlowDepthSide;
            float _TargetGlowSurfaceMode;
            float4 _DrumImpactColor;
            float _DrumImpactStrength;
            float _DrumImpactProgress;
            float4 _DrumSuccessImpactColor;
            float _DrumSuccessImpactStrength;
            float _DrumSuccessImpactProgress;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 localPos : TEXCOORD3;
                float3 worldTangent : TEXCOORD4;
                float3 worldBinormal : TEXCOORD5;
                float3 localNormal : TEXCOORD6;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                o.worldBinormal = cross(o.worldNormal, o.worldTangent) * v.tangent.w * unity_WorldTransformParams.w;
                o.localPos = v.vertex.xyz;
                o.localNormal = normalize(v.normal);
                return o;
            }

            float GetSaturation(float3 color)
            {
                float maxChannel = max(color.r, max(color.g, color.b));
                float minChannel = min(color.r, min(color.g, color.b));
                return maxChannel - minChannel;
            }

            float3 ApplyDrumNormalMap(float2 uv, float3 worldNormal, float3 worldTangent, float3 worldBinormal)
            {
                float3 baseNormal = normalize(worldNormal);
                float tangentLength = dot(worldTangent, worldTangent);
                float binormalLength = dot(worldBinormal, worldBinormal);
                float3 tangentNormal = UnpackNormal(tex2D(_BumpMap, uv));
                float3 t = worldTangent * rsqrt(max(0.0001, tangentLength));
                float3 b = worldBinormal * rsqrt(max(0.0001, binormalLength));
                float3 mappedNormal = normalize((t * tangentNormal.x) + (b * tangentNormal.y) + (baseNormal * tangentNormal.z));
                float tangentValid = step(0.0001, min(tangentLength, binormalLength));
                return normalize(lerp(baseNormal, mappedNormal, tangentValid));
            }

            float3 GetViewFacingNormal(float3 normal, float3 viewDir)
            {
                return normalize(normal * (dot(normal, viewDir) < 0.0 ? -1.0 : 1.0));
            }

            float3 ApplyStageDrumLighting(
                float3 baseTex,
                float3 normal,
                float3 viewDir,
                float3 lightDir,
                float occlusion,
                out float light01,
                out float keyLight,
                out float topLight)
            {
                float3 litNormal = GetViewFacingNormal(normal, viewDir);
                keyLight = pow(saturate(dot(litNormal, lightDir)), 1.45);
                topLight = pow(saturate(dot(litNormal, float3(0.0, 1.0, 0.0))), 1.20);
                float fillLight = pow(saturate(dot(litNormal, normalize(float3(0.55, 0.22, -0.76)))), 2.0);
                float rim = pow(saturate(1.0 - abs(dot(litNormal, viewDir))), 2.7);

                light01 = saturate(
                    _AmbientStrength +
                    (keyLight * _KeyLightStrength) +
                    (topLight * _TopLightStrength) +
                    (fillLight * _FillLightStrength));
                light01 *= occlusion;

                float shadowWeight = saturate(1.0 - light01);
                float3 stageTint = (_ShadowColor.rgb * (0.54 + shadowWeight * 0.34)) +
                                   (_FillLightColor.rgb * (fillLight * _FillLightStrength * 0.42));
                float3 litTint = lerp(_FillLightColor.rgb, _KeyLightColor.rgb, saturate(keyLight + topLight * 0.35));
                float3 tonalColor = lerp(stageTint, litTint, saturate(light01 * 1.08));
                float contrast = lerp(0.24, 0.92, saturate(light01));
                float rimLift = rim * _RimStrength * 0.28;

                return baseTex * tonalColor * contrast * _StageExposure + (_RimColor.rgb * rimLift);
            }

            float4 ComputeTargetGlowMasks(float3 localPos, float3 localNormal)
            {
                float strength = saturate(pow(saturate(_TargetGlowStrength), 0.62) * 1.32);
                float3 extents = max(abs(_TargetGlowExtents.xyz), float3(0.0001, 0.0001, 0.0001));
                float3 local01 = (localPos - _TargetGlowCenter.xyz) / extents;
                float3 planeMask = saturate(_TargetGlowPlaneMask.xyz);
                float3 depthAxis = normalize(max(float3(0.0001, 0.0001, 0.0001), 1.0 - planeMask));
                float planeAxisCount = max(1.0, planeMask.x + planeMask.y + planeMask.z);
                float3 one = float3(1.0, 1.0, 1.0);
                float planeDistance = sqrt(dot(local01 * local01 * planeMask, one) / planeAxisCount);
                float signedDepth = dot(local01 * depthAxis, one);
                float targetDepthSide = step(0.0, _TargetGlowDepthSide) * 2.0 - 1.0;
                float targetFacingDepth = signedDepth * targetDepthSide;
                float depthDistance = dot(abs(local01) * (one - planeMask), one);
                float edgeLine = smoothstep(0.52, 0.66, planeDistance) *
                                 (1.0 - smoothstep(0.90, 1.08, planeDistance));
                float innerBloom = smoothstep(0.40, 0.58, planeDistance) *
                                   (1.0 - smoothstep(0.96, 1.14, planeDistance));
                float edgeGuard = smoothstep(0.38, 0.54, planeDistance);
                float targetSideMask = smoothstep(-0.05, 0.28, targetFacingDepth);
                float capDepthMask = smoothstep(0.48, 0.84, targetFacingDepth);
                float capFacing = abs(dot(normalize(localNormal), depthAxis));
                float hitFaceMask = smoothstep(0.18, 0.58, capFacing);
                float cymbalMode = saturate(_TargetGlowSurfaceMode);
                float pulse = 0.90 + 0.10 * sin(_Time.y * 10.0 + _TargetGlowCenter.x * 0.9 + _TargetGlowCenter.y * 1.7);
                float capSurfaceMask = targetSideMask * capDepthMask * hitFaceMask;
                float planeSurfaceMask = targetSideMask * hitFaceMask * (1.0 - smoothstep(0.70, 1.02, depthDistance)) * 0.55;
                float drumSurfaceMask = saturate(max(capSurfaceMask, planeSurfaceMask));
                float cymbalSurfaceMask = smoothstep(0.04, 0.28, capFacing);
                float surfaceMask = lerp(drumSurfaceMask, cymbalSurfaceMask, cymbalMode);
                float baseMask = surfaceMask * strength * pulse;
                float cymbalEdgeLine = smoothstep(0.52, 0.62, planeDistance) *
                                       (1.0 - smoothstep(0.74, 0.84, planeDistance));
                float cymbalInnerBloom = smoothstep(0.46, 0.60, planeDistance) *
                                         (1.0 - smoothstep(0.78, 0.92, planeDistance)) * 0.38;
                float edgeMask = lerp(edgeLine * edgeGuard, cymbalEdgeLine, cymbalMode);
                float haloMask = lerp(innerBloom * edgeGuard, cymbalInnerBloom, cymbalMode);
                return float4(saturate(edgeMask * baseMask), saturate(haloMask * baseMask), surfaceMask, planeDistance);
            }

            float ComputeInwardImpactMask(float planeDistance, float surfaceMask, float progress, float strength, float widthScale)
            {
                progress = saturate(progress);
                strength = saturate(strength);
                float radius = lerp(0.95, 0.08, progress);
                float ringWidth = lerp(0.215, 0.120, progress) * max(0.2, widthScale);
                float ring = 1.0 - smoothstep(ringWidth, ringWidth + 0.070, abs(planeDistance - radius));
                float pressure = smoothstep(max(0.0, radius - 0.31), max(0.0, radius - 0.06), planeDistance) *
                                 (1.0 - smoothstep(radius + 0.03, radius + 0.20, planeDistance));
                float centerPunch = smoothstep(0.90, 1.0, progress) * (1.0 - smoothstep(0.00, 0.24, planeDistance));
                return saturate(surfaceMask * strength * ((ring * 1.75) + (pressure * 0.76) + (centerPunch * 1.55)));
            }

            float3 BuildImpactColor(float3 sourceColor, float valueBoost)
            {
                sourceColor = max(sourceColor, float3(0.001, 0.001, 0.001));
                float sourceMax = max(sourceColor.r, max(sourceColor.g, sourceColor.b));
                float sourceMin = min(sourceColor.r, min(sourceColor.g, sourceColor.b));
                float sourceRange = max(0.001, sourceMax - sourceMin);
                float3 hueColor = sourceColor / max(0.001, sourceMax);
                float3 saturatedColor = saturate((sourceColor - sourceMin) / sourceRange);
                float saturationAvailable = smoothstep(0.035, 0.16, sourceRange);
                return max(hueColor * valueBoost, lerp(hueColor, saturatedColor, saturationAvailable * 0.94) * valueBoost);
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 tex = tex2D(_MainTex, i.uv);
                float3 rmo = tex2D(_RmoMap, i.uv).rgb;
                float3 baseTex = tex.rgb * _Color.rgb * _BaseColor.rgb;
                float3 normal = ApplyDrumNormalMap(i.uv, i.worldNormal, i.worldTangent, i.worldBinormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float3 lightDir = normalize(_FakeLightDirection.xyz);
                float roughness = saturate(rmo.r);
                float metallic = saturate(rmo.g) * 0.55;
                float occlusion = lerp(0.42, 1.00, saturate(rmo.b));

                float light01;
                float keyLight;
                float topLight;
                float3 color = ApplyStageDrumLighting(baseTex, normal, viewDir, lightDir, occlusion, light01, keyLight, topLight);
                float3 litNormal = GetViewFacingNormal(normal, viewDir);
                float fresnel = pow(saturate(1.0 - abs(dot(litNormal, viewDir))), 2.35);
                float3 halfDir = normalize(lightDir + viewDir);
                float specular = pow(saturate(dot(litNormal, halfDir)), lerp(58.0, 15.0, roughness));
                specular *= lerp(0.018, 0.13, metallic) * occlusion * saturate(keyLight + topLight * 0.35);

                float luminance = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                float saturation = GetSaturation(tex.rgb);
                float headMask = saturate((luminance - 0.52) * 2.4) * saturate(1.0 - saturation * 2.8);
                float cymbalMask = saturate((tex.r + tex.g - tex.b * 2.0 - 0.46) * 1.25) * saturate(luminance * 1.15);
                float shellMask = saturate((tex.r - tex.g * 1.35 - tex.b * 1.15) * 2.15);

                float3 headTone = baseTex * lerp(float3(0.13, 0.17, 0.25), float3(0.54, 0.61, 0.70), light01);
                float3 cymbalTone = baseTex * lerp(float3(0.22, 0.18, 0.10), float3(0.78, 0.58, 0.30), light01);
                float3 shellTone = baseTex * lerp(float3(0.30, 0.08, 0.10), float3(0.68, 0.24, 0.24), saturate(light01 + shellMask * 0.12));
                color = lerp(color, headTone * _StageExposure, headMask * 0.34);
                color = lerp(color, cymbalTone * _StageExposure, cymbalMask * 0.20);
                color = lerp(color, shellTone * _StageExposure, shellMask * 0.10);

                float pulse = saturate(0.5 + 0.5 * sin(_Time.y * _PulseSpeed + i.localPos.x * 0.85 - i.localPos.y * 1.15));
                float stripeWave = 0.5 + 0.5 * sin(i.localPos.x * 2.9 + i.localPos.y * 5.7 + i.localPos.z * 0.75 + _Time.y * 3.2);
                float stripe = pow(saturate(stripeWave), 18.0);
                float beat = _PulseStrength * (0.35 + pulse * 0.65);
                float3 rimColor = lerp(_RimColor.rgb, _ShellGlowColor.rgb, shellMask * 0.85);
                float3 accentColor = lerp(_AccentColor.rgb, _ShellGlowColor.rgb, shellMask * 0.65);

                color += rimColor * fresnel * _RimStrength * (0.38 + beat) * saturate(light01 + 0.28);
                color += accentColor * stripe * _StripeStrength * (0.25 + beat) * saturate(1.08 - headMask * 0.45) * saturate(light01 + 0.35);
                color += specular * lerp(float3(1.0, 0.95, 0.84), baseTex, metallic * 0.45);

                float4 targetGlowMasks = ComputeTargetGlowMasks(i.localPos, i.localNormal);
                float targetEdgeMask = targetGlowMasks.x;
                float targetHaloMask = targetGlowMasks.y;
                float targetSurfaceMask = targetGlowMasks.z;
                float targetPlaneDistance = targetGlowMasks.w;
                float3 targetSourceColor = max(_TargetGlowColor.rgb, float3(0.001, 0.001, 0.001));
                float targetSourceMax = max(targetSourceColor.r, max(targetSourceColor.g, targetSourceColor.b));
                float targetSourceMin = min(targetSourceColor.r, min(targetSourceColor.g, targetSourceColor.b));
                float targetSourceRange = max(0.001, targetSourceMax - targetSourceMin);
                float3 targetHueColor = targetSourceColor / max(0.001, targetSourceMax);
                float3 saturatedTargetColor = saturate((targetSourceColor - targetSourceMin) / targetSourceRange);
                float saturationAvailable = smoothstep(0.035, 0.16, targetSourceRange);
                float3 targetGlowColor = max(targetHueColor * 0.78, lerp(targetHueColor, saturatedTargetColor, saturationAvailable * 0.92));
                float edgeContrastMask = saturate(targetHaloMask * 0.50 + targetEdgeMask * 0.44);
                color = lerp(color, color * 0.12, edgeContrastMask);
                color = lerp(color, targetGlowColor * 1.12, saturate(targetEdgeMask * 0.98));
                color += targetGlowColor * targetEdgeMask * _HitGlowEdgeStrength * 0.40;
                color += targetGlowColor * targetHaloMask * 0.135;

                float normalImpactMask = ComputeInwardImpactMask(targetPlaneDistance, targetSurfaceMask, _DrumImpactProgress, _DrumImpactStrength, 1.0);
                float successImpactMask = ComputeInwardImpactMask(targetPlaneDistance, targetSurfaceMask, _DrumSuccessImpactProgress, _DrumSuccessImpactStrength, 1.24);
                float normalImpactCore = saturate(normalImpactMask * 1.34);
                float successImpactCore = saturate(successImpactMask * 1.58);
                float3 normalImpactColor = BuildImpactColor(_DrumImpactColor.rgb, 1.18);
                float3 successImpactColor = BuildImpactColor(_DrumSuccessImpactColor.rgb, 1.34);
                color = lerp(color, color * 0.035, saturate((normalImpactCore * 0.42) + (successImpactCore * 0.56)));
                color = lerp(color, normalImpactColor, saturate(normalImpactCore * 0.94));
                color += normalImpactColor * normalImpactCore * 1.58;
                color = lerp(color, successImpactColor, saturate(successImpactCore * 0.98));
                color += successImpactColor * successImpactCore * 2.70;

                return float4(color, 1.0);
            }
            ENDCG
        }
    }
}
