#include_with_pragmas "FogInclude.cginc"

float3 ApplyFog(float3 sceneColor, float2 uv, float3 wPos, float linearDepth)
{
    return ApplyFogAndVolumetricLights(sceneColor,uv,wPos,linearDepth);
}