
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

float3 InverseLerp(float lowThreshold, float hiThreshold, float3 value)
{
	return (value - lowThreshold) / (hiThreshold - lowThreshold);
}
float ClampedInverseLerp(float lowThreshold, float hiThreshold, float value)
{
	return saturate(InverseLerp(lowThreshold, hiThreshold, value));
} 


void ParticleZones(float3 pos, inout float density)
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
 
            float feather = 1.0;
            feather = (1.0 - smoothstep (_EnviroRemovalZones[i].radius * feather, _EnviroRemovalZones[i].radius, distsq));

            float contribution = feather * _EnviroRemovalZones[i].density;
            density = clamp(density + contribution,0,1);
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
        }
    }
}

