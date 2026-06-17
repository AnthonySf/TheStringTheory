#include_with_pragmas "FogIncludeHLSL.hlsl"


void ApplyFog_float(float3 sceneColor, float2 uv, float3 wPos, float linearDepth,out float3 finalColor)
{
    finalColor = sceneColor;
    finalColor = ApplyFogAndVolumetricLights(sceneColor,uv,wPos,linearDepth);
}



void ParticleZones_float(float3 pos, float density, out float alpha)
{
    alpha = 1;

    for (int i = 0; i < _EnviroRemovalZonesCount; i++)
    {
        if(_EnviroRemovalZones[i].type == 0) 
        { 
            float3 dir = _EnviroRemovalZones[i].pos - pos;
            float3 axis = _EnviroRemovalZones[i].axis;
            float3 dirAlongAxis = dot(dir, axis) * axis;

            dir = dir + dirAlongAxis * _EnviroRemovalZones[i].stretch;
            float distsq = dot(dir, dir);
 
            float feather = 1.0;
            feather = (1.0 - smoothstep (_EnviroRemovalZones[i].radius * feather, _EnviroRemovalZones[i].radius, distsq));

            float contribution = feather * _EnviroRemovalZones[i].density;
            density = clamp(density + contribution,0,1);
            alpha = density;
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
            density = clamp(density,0,1);
            alpha = density;
        }
    }
}

