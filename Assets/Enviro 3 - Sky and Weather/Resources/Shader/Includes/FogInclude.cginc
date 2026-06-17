#include "SkyInclude.cginc"
#include "VolumetricCloudsBlendInclude.cginc"

#ifndef ENVIRO_VOLUMELIGHT_KEYWORD
#define ENVIRO_VOLUMELIGHT_KEYWORD
#pragma multi_compile __ ENVIRO_VOLUMELIGHT
#endif

#ifndef ENVIRO_SIMPLEFOG_KEYWORD
#define ENVIRO_SIMPLEFOG_KEYWORD
#pragma multi_compile __ ENVIRO_SIMPLEFOG
#endif



#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
UNITY_DECLARE_TEX2DARRAY(_EnviroVolumetricFogTex);
#else
sampler2D _EnviroVolumetricFogTex;  
#endif

float4 _EnviroVolumetricFogTex_TexelSize;
float4 _Screen_TexelSize; 

uniform float4 _EnviroFogParameters; //x = rayorigin1, y = falloff1, z = density1, w = height1
uniform float4 _EnviroFogParameters2; //x = rayorigin2, y = falloff2, z = density2, w = height2 
uniform float4 _EnviroFogParameters3; //x = maxDensity, y = startDistance, z = , w = sky blend
uniform float4 _EnviroFogColor; //Fog color
uniform float4 _EnviroDirLightColor;
uniform float3 _EnviroCameraPos;
uniform float3 _EnviroWorldOffset;

 
#if defined(SHADER_API_D3D11) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN)
struct EnviroRemovalZones
{
    float type; 
    float3 pos; 
    float radius;
    float3 size; 
    float3 axis; 
    float stretch;
    float density;
    float feather;
    float4x4 transform;
    float pad0;
    float pad1;
};

StructuredBuffer<EnviroRemovalZones> _EnviroRemovalZones : register(t1);
float _EnviroRemovalZonesCount;
#endif

int ihash(int n)
{
	n = (n<<13)^n;
	return (n*(n*n*15731+789221)+1376312589) & 2147483647;
}

float frand(int n)
{
	return ihash(n) / 2147483647.0;
}

float2 cellNoise(int2 p)
{
	int i = p.y*256 + p.x;
	return float2(frand(i), frand(i + 57)) - 0.5;//*2.0-1.0;
} 

float Pow2(float x) 
{ 
    return x * x;
}

float CalculateLineIntegral(float FogHeightFalloff, float RayDirectionY, float RayOriginTerms)
{
    float Falloff = FogHeightFalloff * RayDirectionY; 
    
    float LineIntegral = ((1.0f - exp2(-Falloff)) / Falloff); 
    float LineIntegralTaylor = log(2.0) - (0.5 * Pow2(log(2.0))) * Falloff;

    return RayOriginTerms * (abs(Falloff) > 0.01f ? LineIntegral : LineIntegralTaylor);
}

float3 InverseLerp(float lowThreshold, float hiThreshold, float value)
{
	return (value - lowThreshold) / (hiThreshold - lowThreshold);
}
float ClampedInverseLerp(float lowThreshold, float hiThreshold, float value)
{
	return saturate(InverseLerp(lowThreshold, hiThreshold, value));
} 

#if defined(SHADER_API_D3D11) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN)

void FogZones(float3 pos, inout float density)
{
    for (int i = 0; i < _EnviroRemovalZonesCount; i++)
    {
        if(_EnviroRemovalZones[i].type == 0) 
        { 
            float3 dir = _EnviroRemovalZones[i].pos - pos;
            float3 axis = _EnviroRemovalZones[i].axis;
            float3 dirAlongAxis = dot(dir, axis) * axis;

            dir = dir + dirAlongAxis * _EnviroRemovalZones[i].stretch;
            float distsq = dot(dir, dir);
            float radius = _EnviroRemovalZones[i].radius;
            float feather = _EnviroRemovalZones[i].feather;

            feather = (1.0 - smoothstep (radius * feather, radius, distsq));

            float contribution = feather * _EnviroRemovalZones[i].density;
            density = density + contribution;
            density = max(density,0.0);
        }
        else 
        {
            float influence = 1;
            float3 position = mul(_EnviroRemovalZones[i].transform, float4(pos, 1)).xyz;

            float x = ClampedInverseLerp(-0.5f, -0.5f + _EnviroRemovalZones[i].feather, position.x) - ClampedInverseLerp(0.5f - _EnviroRemovalZones[i].feather, 0.5f, position.x);
		    float y = ClampedInverseLerp(-0.5f, -0.5f + _EnviroRemovalZones[i].feather, position.y) - ClampedInverseLerp(0.5f - _EnviroRemovalZones[i].feather, 0.5f, position.y);
		    float z = ClampedInverseLerp(-0.5f, -0.5f + _EnviroRemovalZones[i].feather, position.z) - ClampedInverseLerp(0.5f - _EnviroRemovalZones[i].feather, 0.5f, position.z);
		    
            influence = x * y * z; 
 
            density += _EnviroRemovalZones[i].density * influence;
            density = max(density,0.0);
        }
    } 
} 
#endif



float4 GetExponentialHeightFog(float3 wPos, float linearDepth) 
{ 
    wPos = wPos - _EnviroWorldOffset;
    const half MinFogOpacity = _EnviroFogParameters3.x;
  
    float3 CameraToReceiver = wPos - _EnviroCameraPos.xyz;
    float camHeightLimiter = min(2000.0f,_EnviroCameraPos.y - _EnviroWorldOffset.y);
    float CameraToReceiverHeight = wPos.y - camHeightLimiter;
    float3 viewDirection = CameraToReceiver;
    float viewLength = length(viewDirection);
    viewDirection /= viewLength; 

    float fogAmount = 0;
 
    float RayDirectionY = CameraToReceiverHeight;
    
    float Exponent = _EnviroFogParameters.y * (camHeightLimiter - _EnviroFogParameters.w);
    float RayOriginTerms = _EnviroFogParameters.z * exp2(-Exponent);
    float ExponentSecond = _EnviroFogParameters2.y * (camHeightLimiter - _EnviroFogParameters2.w);
    float RayOriginTermsSecond = _EnviroFogParameters2.z * exp2(-ExponentSecond); 

    #if ENVIRO_SIMPLEFOG
    fogAmount = CalculateLineIntegral(_EnviroFogParameters.y, RayDirectionY, RayOriginTerms) * viewLength;
    #else
    fogAmount = (CalculateLineIntegral(_EnviroFogParameters.y, RayDirectionY, RayOriginTerms) + CalculateLineIntegral(_EnviroFogParameters2.y, RayDirectionY, RayOriginTermsSecond)) * viewLength;
    #endif

    //Start Distance
    if(viewLength <= _EnviroFogParameters3.y)
    {
        float fallOff = ClampedInverseLerp(0.0f,_EnviroFogParameters3.y, viewLength);
        fogAmount = fogAmount * pow(fallOff,6);
    }
 
    //Fog Zones
    fogAmount = clamp(fogAmount,0,10);
    #if defined(SHADER_API_D3D11) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN)
    FogZones(wPos,fogAmount);
    #endif

    float fogfactor = max(exp2(-fogAmount), MinFogOpacity);
    float scatteringFactor = saturate(linearDepth + _EnviroFogParameters3.z);

    // Color   
    #if ENVIRO_SIMPLESKY
    float4 sky = GetSkyColorSimple(viewDirection,0.005f,scatteringFactor);
    #else
    float4 sky = GetSkyColor(viewDirection,0.005f,scatteringFactor);
    #endif
    float3 inscatterColor = lerp(_EnviroFogColor.rgb,sky.rgb,_EnviroFogParameters3.w);
    float3 fogColor = inscatterColor * saturate(1 - fogfactor);

    return float4(fogColor, fogfactor);
}

float3 ApplyVolumetricLights(float4 fogColor, float3 sceneColor, float2 uv)
{  
    #if ENVIRO_VOLUMELIGHT
    float4 volumeLightsSample = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_EnviroVolumetricFogTex, uv);
     //uvs += cellNoise(uvs.xy * _Screen_TexelSize.zw) * _VolumeScatter_TexelSize.xy * 0.8;
    float3 volumeLightsDirectional = volumeLightsSample.a * _EnviroDirLightColor.rgb;
    float3 volumeLights = volumeLightsSample.rgb;  
    return (sceneColor.rgb * fogColor.a + fogColor.rgb * max(volumeLightsDirectional,0.75)) + volumeLights;
    //return (sceneColor.rgb * fogColor.a + fogColor.rgb) + volumeLightsDirectional + volumeLights; 
    #else
    return sceneColor.rgb * fogColor.a + fogColor.rgb; 
    #endif
}

float3 ApplyFogAndVolumetricLights(float3 sceneColor, float2 uv, float3 wPos, float linearDepth)
{
    if(_EnviroActive > 0)
    {
        float4 fog = GetExponentialHeightFog(wPos,linearDepth);
        return ApplyVolumetricLights(fog,sceneColor,uv);
    }
    else
    {
        return sceneColor;
    }
}

