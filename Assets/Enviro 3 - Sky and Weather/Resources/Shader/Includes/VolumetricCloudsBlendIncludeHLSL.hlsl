#ifndef ENVIRO_SIMPLESKY_KEYWORD
#define ENVIRO_SIMPLESKY_KEYWORD
#pragma multi_compile __ ENVIRO_SIMPLESKY
#endif
 
TEXTURE2D_X(_EnviroClouds);
SAMPLER(sampler_EnviroClouds);

float3 _AmbientColor;
float3 _DirectLightColor;
float _AtmosphereColorSaturateDistance;
float4 _CloudsParameter;
float _SolarTime;
uniform float _EnviroActive;

float ComputeAmbient(float4 cloudsColor, float normalizedHeight)
{
    float _AmbientDistanceScale = 0.25;
    // Height influence: higher clouds receive more ambient sky light
    // 0.7 base → 1.3 peak (keep within a modest range for realism)
    float heightFactor = lerp(0.7, 1.3, normalizedHeight);

    // Distance influence: nearby clouds look brighter, far clouds fade
    // _AmbientDistanceScale lets you control falloff distance
    float distFalloff = exp(-cloudsColor.g * _AmbientDistanceScale);

    // Combine
    float ambient = cloudsColor.a * heightFactor * distFalloff;
    return saturate(ambient);
}
 
 

float4 GetCloudColor(float4 cloudsColor, float3 worldPos)
{
    float3 viewDir = normalize(worldPos.xyz - _WorldSpaceCameraPos.xyz);
    
    float3 sunColor = pow(_DirectLightColor.rgb,2) * 2;
    float3 skyColor = float3(1,1,1);
     
    #if ENVIRO_SIMPLESKY
        skyColor = GetSkyColorSimple(viewDir, 0.005f);  
    #else
        skyColor = GetSkyColor(viewDir, 0.005f);  
    #endif

    float3 cloudsPos = _WorldSpaceCameraPos + viewDir * cloudsColor.g;
    float normalizedHeight = saturate((cloudsPos.y - _CloudsParameter.x) / (_CloudsParameter.y - _CloudsParameter.x));  
    float ambient = ComputeAmbient(cloudsColor.a, normalizedHeight);   
    float4 finalColor = float4(cloudsColor.r * sunColor + _AmbientColor * ambient, cloudsColor.a);
 
    float p = smoothstep(0.55, 0.60, _SolarTime); 
    float dayNightModifier = lerp(1.5, 0.1, p);

    float baseDist = _AtmosphereColorSaturateDistance;

    float kShadowRatio  = dayNightModifier;
    static const float kMidRatio       = 1.0;
    static const float kHighlightRatio = 2.0;

    float3 brightness = saturate(finalColor.rgb);
    float  luminance  = dot(brightness, float3(0.299,0.587,0.114));

    // Blend distance mapping with dynamic base
    float distLow  = lerp(baseDist * kShadowRatio, baseDist * kMidRatio,saturate(luminance * 2.0));
    float blendDist = lerp(distLow, baseDist * kHighlightRatio,saturate((luminance - 0.5) * 2.0));
    // Final factor
    float atmosphericBlendFactor = saturate(exp(-cloudsColor.g / blendDist));
    finalColor.rgb = lerp(skyColor, finalColor.rgb, atmosphericBlendFactor);

    
    float cloudLuminance = dot(finalColor.rgb,float3(0.299,0.587,0.114));
    float ghostMask = step(0.1,finalColor.a) * step(cloudLuminance,0.001);
    finalColor.a *= (1- ghostMask);
        
    return finalColor;
}



float3 ApplyClouds(float3 sceneColor, float2 uv, float3 worldPos)
{   
    if(_EnviroActive > 0)
    {
        float4 cloudsColor = SAMPLE_TEXTURE2D_X(_EnviroClouds,sampler_EnviroClouds, uv);
        float4 finalColor = GetCloudColor(cloudsColor,worldPos);
        return sceneColor.rgb * saturate(1 - finalColor.a) + finalColor.rgb * finalColor.a;
    }
    else
    {
        return sceneColor.rgb;
    }
}
