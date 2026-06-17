#include_with_pragmas "FogIncludeHLSL.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/SkyUtils.hlsl"
float _EnviroSkyIntensity;

float3 ApplyFog(float3 sceneColor, float2 uv, float3 wPos, float linearDepth)
{
    float4 fog = GetExponentialHeightFog(wPos,linearDepth);
    fog.rgb *= _EnviroSkyIntensity * GetCurrentExposureMultiplier();
    return ApplyVolumetricLights(fog,sceneColor,uv);
}

