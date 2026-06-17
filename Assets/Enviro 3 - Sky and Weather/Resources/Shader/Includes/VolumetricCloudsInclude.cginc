#ifndef PI
#define PI 3.14159265
#endif

#ifndef TWO_PI
#define TWO_PI 6.28318530
#endif

#ifndef INV_PI
#define INV_PI 0.31830989
#endif

uniform Texture3D _Noise;
SamplerState sampler_Noise;
uniform Texture3D _DetailNoise; 
SamplerState sampler_DetailNoise;
uniform Texture2D _WeatherMap;
SamplerState sampler_WeatherMap;
uniform Texture2D _CurlNoise;
SamplerState sampler_CurlNoise;

uniform sampler2D _BottomsOffsetNoise;      
uniform sampler2D _BlueNoise;

float4 _BlueNoise_TexelSize;
float3 _WorldOffset;
    
uniform float4x4 _InverseProjection;
uniform float4x4 _InverseRotation;
uniform float4x4 _InverseProjectionRight;
uniform float4x4 _InverseRotationRight;

float4x4 _LeftWorldFromView;
float4x4 _RightWorldFromView;
float4x4 _LeftViewFromScreen;
float4x4 _RightViewFromScreen;

uniform float4 _CloudsParameter; 
uniform float4 _CloudsParameter2; 

uniform float4 _Steps;
uniform float4 _CloudsLighting;
uniform float4 _CloudsLighting2;
uniform float4 _CloudsLightingExtended; 
uniform float4 _CloudsLightingExtended2;

uniform float4 _CloudsMultiScattering;
uniform float4 _CloudsMultiScattering2;

uniform float4 _CloudsErosionIntensity; //x = Base, y = Detail
uniform float4 _CloudsNoiseSettings; //x = Base, y = Detail

uniform float4 _CloudsShape1;
uniform float4 _CloudsShape2;

uniform float4 _CloudDensityScale;
uniform float4 _CloudsCoverageSettings; //x = _GlobalCoverage, y = Bottom Coverage Mod, z = Top coverage mod, w = Clouds Up Morph Intensity
uniform float _GlobalCoverage;
uniform float4 _CloudsAnimation;
uniform float4 _CloudsWindDirection;

uniform float3 _LightDir;
uniform float _stepsInDepth;
uniform float _LODDistance;
uniform float3 _CameraPosition;
uniform float4 _Resolution;
uniform float4 _Randomness;
uniform float _EnviroDepthTest; 
uniform float _SolarTime;
////              
const float env_inf = 1e10;

struct RaymarchParameters 
{
    //Lighting
    float scatteringCoef;
    float silverLiningIntensity;
    float silverLiningSpread;
    float edgeHighlightStrength;
    float lightningIntensity;
    float exposure;
    float attenuation;
    float lightStep;
    float lightAbsorb;
    float multiScatterStrength;
    float multiScatterFalloff;
    float ambientFloor;

    //Height
    float4 cloudsParameter;

    //Density
    float density;
    float densitySmoothness;

    //Erosion
    float baseErosion;
    float detailErosion;

    int minSteps, maxSteps;

    float baseNoiseUV;
    float detailNoiseUV;

    float baseErosionIntensity;
    float detailErosionIntensity;

    float baseNoiseMultiplier;
    float detailNoiseMultiplier;

    float bottomShape;
    float midShape;
    float topShape;
    float topLayer;
    float rampShape;
    float cloudTypeShaping;
};

void InitRaymarchParameters(inout RaymarchParameters parameters)
{
    parameters.scatteringCoef = _CloudsLighting.x;
    parameters.silverLiningIntensity = _CloudsLighting.y;
    parameters.silverLiningSpread = _CloudsLighting.w;

    parameters.multiScatterStrength = _CloudsMultiScattering.x;
    parameters.multiScatterFalloff = _CloudsMultiScattering.y;
    parameters.ambientFloor = _CloudsMultiScattering.z;
    parameters.exposure = _CloudsMultiScattering.w;

    parameters.lightningIntensity = _CloudsLightingExtended.x;
    parameters.attenuation = _CloudsLightingExtended.y;
    parameters.lightStep = _CloudsLightingExtended.z;
    parameters.lightAbsorb = _CloudsLightingExtended.w;

    parameters.edgeHighlightStrength = _CloudsLighting.z;

    parameters.cloudsParameter = _CloudsParameter;

    parameters.density = _CloudDensityScale.x;
    parameters.densitySmoothness = _CloudDensityScale.z;
        
    parameters.baseErosion = _CloudsErosionIntensity.x;
    parameters.detailErosion = _CloudsErosionIntensity.y;

    parameters.baseNoiseMultiplier = _CloudsNoiseSettings.z; 
    parameters.detailNoiseMultiplier = _CloudsNoiseSettings.w;

    parameters.minSteps = _Steps.x; 
    parameters.maxSteps = _Steps.y;

    parameters.baseNoiseUV = _CloudsNoiseSettings.x;
    parameters.detailNoiseUV = _CloudsNoiseSettings.y;

    parameters.baseErosionIntensity = _CloudsErosionIntensity.x;
    parameters.detailErosionIntensity = _CloudsErosionIntensity.y;
        
    parameters.bottomShape = _CloudsShape1.x;
    parameters.midShape = _CloudsShape1.y;
    parameters.topShape = _CloudsShape1.z;
    parameters.topLayer = _CloudsShape1.w;

    parameters.rampShape = _CloudsCoverageSettings.w;

    parameters.cloudTypeShaping = _CloudsCoverageSettings.z;
} 

float HenryGreensteinNorm(float cosTheta, float g)
{
    float g2 = g * g;
    return (1.0 - g2) / (4.0 * PI * pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5));
}

float RemapEnviro(float org_val, float org_min, float org_max, float new_min, float new_max)
{
    return new_min + saturate(((org_val - org_min) / (org_max - org_min))*(new_max - new_min));
}

float4 GetWeather(float3 pos) 
{
    float2 uv = pos.xz * 0.0000025;
    return _WeatherMap.SampleLevel(sampler_WeatherMap,uv, 0); 
} 

float GetSamplingHeight(float3 pos, float3 center, float4 parameters)
{
    return (length(pos - center) - (parameters.w + parameters.x)) * parameters.z;
}
    
float3 ScreenSpaceDither(float2 vScreenPos, float lum)
{
    float d = dot(float2(131.0, 312.0), vScreenPos.xy); //+ _Time TODO
    float3 vDither = float3(d, d, d);
    vDither.rgb = frac(vDither.rgb / float3(103.0, 71.0, 97.0)) - float3(0.5, 0.5, 0.5);
    return (vDither.rgb / 15.0) * 1.0 * lum;
}

float GetRaymarchEndFromSceneDepth(float sceneDepth, float maxRange) 
{
    float raymarchEnd = 0.0f;
#if ENVIRO_DEPTH_BLENDING
    if (sceneDepth >= 0.99f) 
    {	
        raymarchEnd = maxRange;
    }
    else 
    { 
        raymarchEnd = sceneDepth * _ProjectionParams.z;	   
    }
#else
    if(_EnviroDepthTest > 0 )
    {
        if (sceneDepth >= 0.99f) 
        {	
            raymarchEnd = maxRange;
        }
        else 
        { 
            raymarchEnd = sceneDepth * _ProjectionParams.z;	   
        }
    }
    else
    {
    raymarchEnd = maxRange;	
    }
#endif
    return raymarchEnd;
}

float4 GetHeightGradient(float cloudType)
{
    // x,y = bottom smoothstep, z,w = top smoothstep
    const float4 CloudGradient1 = float4(0.0, 0.07, 0.08, 0.15); // Strato
    const float4 CloudGradient2 = float4(0.0, 0.20, 0.42, 0.60); // Cumulus
    const float4 CloudGradient3 = float4(0.0, 0.08, 0.75, 0.98); // Nimbus

    float a = 1.0 - saturate(cloudType * 2.0);          // 0→0.5 strato
    float b = 1.0 - abs(cloudType - 0.5) * 2.0;         // around 0.5 cumulus
    float c = saturate(cloudType - 0.5) * 2.0;          // 0.5→1 nimbus

    return CloudGradient1 * a + CloudGradient2 * b + CloudGradient3 * c;
}

float GradientStep(float a, float4 g)
{
    return smoothstep(g.x, g.y, a) - smoothstep(g.z, g.w, a);
}

// ---------- Final profile with varying bottom ----------
float CloudTypeShaping(float3 worldPos, float cloudType, RaymarchParameters p)
{
    float _BottomVariation = 1000;      // e.g. 500.0 m of random variation
    float _NoiseScale = 0.000025;         // e.g. 0.001 for slow world-scale noise

    // noise-based base variation
    float baseOffset = 0;
#if ENVIRO_VARIABLE_BOTTOM
    baseOffset = tex2Dlod(_BottomsOffsetNoise, float4(worldPos.xz * _NoiseScale,0,0)).r * _BottomVariation;
#endif     
    float bottom = p.cloudsParameter.x + baseOffset;
    float top    = bottom + (p.cloudsParameter.y - p.cloudsParameter.x);

    // normalized height
    float h = saturate((worldPos.y - bottom) / (top - bottom));

    // choose gradient band based on type
    float4 gradient = GetHeightGradient(cloudType);

    // final vertical profile
    return GradientStep(h, gradient);
}


float CloudVerticalShaping(float heightNorm,
                           float bottomCtrl,
                           float midCtrl,
                           float topCtrl,
                           float ramp)
{
    // --- Define segments ---
    float2 bottomRange = float2(-0.1, 0.25); 
    float2 midRange    = float2(0.20, 0.45);
    float2 topRange    = float2(0.40, 0.8); // top segment fades before 1.0

    // --- Compute smooth ramps ---
    float bottom = smoothstep(bottomRange.x, bottomRange.y, heightNorm) *
                   (1.0 - smoothstep(bottomRange.x + ramp, bottomRange.y + ramp, heightNorm));

    float mid    = smoothstep(midRange.x, midRange.y, heightNorm) *
                   (1.0 - smoothstep(midRange.x + ramp, midRange.y + ramp, heightNorm));

    float top    = smoothstep(topRange.x, topRange.y, heightNorm) *
                   (1.0 - smoothstep(topRange.x + ramp, topRange.y + ramp, heightNorm));

    // --- Fade top influence out at absolute top ---
    //top *= 1.0 - smoothstep(0.95, 1.0, heightNorm);

    // --- Normalize weights so controls are balanced ---
    float sum = bottom + mid + top + 1e-5;
    bottom /= sum;
    mid    /= sum;
    top    /= sum;

    // --- Apply coverage controls ---
    float bias = bottomCtrl * bottom + midCtrl * mid + topCtrl * top;

    // --- Map to envelope multiplier ---
    // center at 1.0 = no change, limit to avoid full fill
    float envelope = 1.0 + bias * 0.5; // adjust scale as needed
    //envelope = envelope;

    //Bottom Round
    float edgeRound = 1.0 - exp(-heightNorm * 8.0); 
    envelope *= lerp(1.0, edgeRound, 0.9 * bottom);

    
    
    // --- Subtle round-top modifier ---
    float edgeRoundTop = 1.0 - exp(-(1.0 - heightNorm) * 8.0);  // only affects highest ~0.8–1.0
    envelope *= lerp(1.0, edgeRoundTop, 1.5 * top);

     if(heightNorm > 0.90)  
     envelope = 0;

    return envelope;
}

float CloudTopLayer(float heightNorm, float baseNoise, float topLayerAdd)
{
    const float topMin = 0.90;
    const float topMax = 0.94;
    float verticalFalloff = smoothstep(topMin, topMax, heightNorm) - smoothstep(topMax, 1.0, heightNorm);

    float coverage = baseNoise;
    return verticalFalloff * coverage * topLayerAdd; 
}

// Sample Cloud Density
float CalculateCloudDensity(float3 pos, float3 PlanetCenter, RaymarchParameters parameters, float4 weather, float mip, float lod, bool details)
{
    const float baseFreq = 1e-5;
    
    // Get Height fraction
    float height = GetSamplingHeight(pos, PlanetCenter, parameters.cloudsParameter);

    // wind settings
    float cloud_top_offset = 1000.0;
    float3 advectedPos = pos + float3(_CloudsWindDirection.z, 0.0f, _CloudsWindDirection.w);

    float3 windDir = normalize(float3(_CloudsWindDirection.x, 0, _CloudsWindDirection.y));
    advectedPos += height * windDir * cloud_top_offset;

    float mip1 = mip + lod;
    float4 coord = float4(advectedPos * baseFreq * parameters.baseNoiseUV, mip1);

    float4 baseNoise = _Noise.SampleLevel(sampler_Noise, coord.xyz,coord.w) * parameters.baseNoiseMultiplier;
    float low_freq_fBm = (baseNoise.g * 0.625) + (baseNoise.b * 0.25) + (baseNoise.a * 0.125);
    float base_cloud = RemapEnviro(baseNoise.r, -(1.0 - low_freq_fBm) * (parameters.baseErosionIntensity), 1.0, 0.0, 1.0) ;
        
    float targetShape = CloudVerticalShaping(height,parameters.bottomShape,parameters.midShape,parameters.topShape,parameters.rampShape);
    float heightGradient = CloudTypeShaping(pos,saturate(weather.g + 0.1), parameters); 
    float shapedCloud = base_cloud * targetShape;

    shapedCloud *= lerp(1.0, heightGradient, parameters.cloudTypeShaping);
    shapedCloud += CloudTopLayer(height, base_cloud, parameters.topLayer);
   
   
    float cloud_coverage = saturate(1-weather.r); 
    
    if(height > 0.90)     
    cloud_coverage = saturate(1-weather.b); 

    float cloudDensity = RemapEnviro(shapedCloud, cloud_coverage, 1.0, 0.0, 1.0);

    //DETAIL
    [branch]  
    if (details)
    { 		  
        float mip2 = mip + lod;

        coord = float4(advectedPos * baseFreq * parameters.detailNoiseUV, mip2); 
        
        //Curl
        float distToCam = distance(pos, _CameraPosition); 
        float curlFade = saturate(1.0 - distToCam / 30000.0f);  

        float2 curlA = _CurlNoise.SampleLevel(sampler_CurlNoise, coord.xz * 3, 0).rb;
        float2 curlB = _CurlNoise.SampleLevel(sampler_CurlNoise, coord.xy * 3, 0).rb;
        float2 curl = pow(lerp(curlA, curlB, 0.5),0.15); 

        float curlAmp = parameters.attenuation * curlFade; 
        curl *= curlAmp * saturate(pow(1-height, 0.5)); 

        coord.xz += curl;
        coord.y += curl.y * 0.5;

        coord.xyz += float3(0.0 , _CloudsAnimation.z, 0.0);	
 
        float3 detailNoise = _DetailNoise.SampleLevel(sampler_DetailNoise, coord.xyz, coord.w).rgb * parameters.detailNoiseMultiplier;
        float high_freq_fBm = (detailNoise.r * 0.625) + (detailNoise.g * 0.25) + (detailNoise.b * 0.125);
        float high_freq_noise_modifier = lerp(high_freq_fBm, 1.0f - high_freq_fBm, saturate(height * 10));	 		
        
        cloudDensity = RemapEnviro(cloudDensity, saturate(high_freq_noise_modifier * parameters.detailErosionIntensity), 1.0, 0.0, 1.0); 
  
    }  

if(height > 0.9)
cloudDensity *= 0.2;

return cloudDensity;
}     


float hash11(float n)
{
    return frac(sin(n) * 43758.5453);
}

float GetDensityAlongRay(float3 pos, float3 PlanetCenter, RaymarchParameters parameters, float3 LightDirection, float4 weather, float lod)
{
    // Extended shadow sampling pattern (near → far)
    static const float shadowSampleDistance[6] = {0.25, 1.0, 3.0, 8.0, 16.0, 32.0};
    
    float mipBiasScale = 0.1;
    float opticalDepth = 0.0; 
    float mipOffset = 0.0;
    float distanceTraveled = 0.0;
    float sunHeight = saturate(dot(LightDirection, float3(0,1,0)));
    float airPathFactor = lerp(0.6, 1.4, pow(sunHeight, 0.5));  

    [loop]
    for (int i = 0; i < 6; i++)
    {
        float altitude = pos.y; 
        float densityFalloff = exp(-altitude / 5000.0) * 0.2;   
 
        // Add normalization constant to bring it back in expected rang
        float baseStep = shadowSampleDistance[i] * 512.0 * densityFalloff * airPathFactor;
      
        distanceTraveled += baseStep;

        // Small jitter to avoid banding and create natural shadow softness
        float jitter = (hash11(dot(pos + LightDirection * distanceTraveled, 37.45)) - 0.5) * baseStep * 0.35;
        
        // Sample position along the light ray
        float3 samplePos = pos + LightDirection * (distanceTraveled + jitter);

        // Density sampling (use higher quality for near samples)
        float density = CalculateCloudDensity(samplePos, PlanetCenter, parameters, weather, mipOffset, lod, i < 2);

        // Exponential weight curve for smoother integration
        float weight = exp(-0.7 * i); 

        opticalDepth += weight * density * baseStep;

        // Adaptive mip bias — increases faster in thick regions
        mipOffset += saturate(baseStep * mipBiasScale * (1.0 + density * 0.5));
    }

    return opticalDepth;
}


float3 _LightningCenter;
float  _LightningRadius;
float  _LightningStart;
float  _LightningDuration;
float  _GlobalTime;

float LightningIntensity(float t)
{
    if (t < 0 || t > _LightningDuration) return 0;

    // ---- Parameters ----
    float flickerStrength = 0.25; // 0..1   amplitude of flicker
    float flickerCycles   = 3;   // number of flickers across duration
    float endBoost        = 2;        // 1 = none, 2 = double brightness at end

    // ---- Duration-based scaling ----
    float riseRate  = 1.0 / max(0.15 * _LightningDuration, 0.001);
    float decayRate = 1.0 / max(_LightningDuration, 0.001);

    float rise  = saturate(t * riseRate);
    float decay = exp(-t * decayRate * _LightningDuration);
    float envelope = rise * decay;

    // ---- Flicker ----
    float flickerFreq = flickerCycles / max(_LightningDuration, 0.001);
    float flicker = 1.0 + flickerStrength * sin(t * flickerFreq * 6.2831853);

    // ---- Late-time boost ----
    // Progress through flash (0 → 1)
    float progress = saturate(t / _LightningDuration);
    // Stronger near the end (ease-in curve)
    float lateBoost = 1.0 + (endBoost - 1.0) * pow(progress, 3.0);

    return envelope * flicker * lateBoost;
}


float SampleEnergy(
    float3 pos, float cosTheta, float3 center,
    RaymarchParameters p, float3 LightDirection,
    float height, float ds_lod, float step_size,
    float4 weather, float lod)
{
    //--------------------------------
    // 1. Optical depth along light
    //--------------------------------
    float tau      = GetDensityAlongRay(pos, center, p, LightDirection, weather, lod);
    float sigma_t  = max(p.lightAbsorb, 0.0005);
    float opticalD = sigma_t * tau;
    float sunHeight = saturate(dot(LightDirection, float3(0, 1, 0)));
    float horizonFactor = smoothstep(0.0, 0.3, sunHeight);
    //--------------------------------
    // 2. Direct scattering
    //-------------------------------- 
    //float T         = exp(-opticalD); 
    float T = exp(-pow(opticalD, 1.05));
    float g         = 0.9 - p.silverLiningSpread;
    float hgForward = HenryGreensteinNorm(cosTheta, g); 
    float hgIso     = 0.25 * INV_PI;
    float phase     = lerp(hgIso + hgForward,hgForward, p.edgeHighlightStrength);  
    float adaptiveDirectScattering = lerp(p.silverLiningIntensity * 2, p.silverLiningIntensity * 0.5, horizonFactor);
    float direct = T * phase * p.scatteringCoef * adaptiveDirectScattering * 5;

    //--------------------------------
    // 3. Multi-scattering (indirect)
    //--------------------------------
    float msBase = 1.0 - exp(-opticalD * 0.5);
    float adaptiveFalloff = lerp(p.multiScatterFalloff * 1.5, p.multiScatterFalloff, horizonFactor);
    float msFalloff = exp(-opticalD * adaptiveFalloff );
    float adaptiveScattering = lerp(p.multiScatterStrength * 4, p.multiScatterStrength, horizonFactor);
    float msEnergy = adaptiveScattering * msBase * msFalloff;
    float phaseMS  = 0.25 * INV_PI;
    float indirect = msEnergy * phaseMS * p.scatteringCoef * 5;
    
    //--------------------------------
    // 3a Clouds Tweaks
    //--------------------------------

    float backlit = smoothstep(0.6, 1.0, cosTheta);
    direct *= lerp(1.0, 2.0, backlit);

    //--------------------------------
    // 4. Ambient floor
    //--------------------------------
    float ambientFloor = p.ambientFloor * (1.0 - exp(-opticalD * 0.1));

    //--------------------------------
    // 5. Lightning
    //--------------------------------
    float lightning = 0.0;
    #if ENVIRO_LIGHTNING
        float dist      = length(pos - _LightningCenter);
        float softness  = 0.5;
        float falloff   = exp(-pow(dist / _LightningRadius, 2.0) * softness);
        float flash     = LightningIntensity(_Time.y - _LightningStart);
        float densityAtten = exp(-tau * 0.004);
        float dayFactor = _SolarTime;
        float lightningAdapt = lerp(200.0, 0.001, pow(_SolarTime,0.25)); 
        lightning = flash * falloff * densityAtten * lightningAdapt * p.lightningIntensity; 
    #endif

    //--------------------------------
    // 6. Combine total energy
    //--------------------------------
    float energy = direct + indirect + ambientFloor + lightning;

    return max(energy * p.exposure, 0.0);
}





float2 squareUV(float2 uv) 
{
    float width = _Resolution.x;
    float height = _Resolution.y;
    float scale = 400;
    float x = uv.x * width;
    float y = uv.y * height;
    return float2 (x/scale, y/scale);
}


bool ray_trace_sphere(float3 center, float3 rd, float3 offset, float radius, out float t1, out float t2) {
    float3 p = center - offset;
    float b = dot(p, rd);
    float c = dot(p, p) - (radius * radius);

    float f = b * b - c;
    if (f >= 0.0) {
        float dem = sqrt(f);
        t1 = -b - dem;
        t2 = -b + dem;
        return true;
    }
    return false;
}


bool resolve_ray_start_end(float3 ws_origin, float3 ws_ray, float3 center, RaymarchParameters parameter, out float start, out float end) 
{
    start = 0;
    end = 0;
    //case includes on ground, inside atm, above atm.
    float ot1, ot2, it1, it2;
    bool outIntersected = ray_trace_sphere(ws_origin, ws_ray, center, parameter.cloudsParameter.w + parameter.cloudsParameter.y, ot1, ot2);
    if (!outIntersected || ot2 < 0.0f)
        return false;	//you see nothing.

    bool inIntersected = ray_trace_sphere(ws_origin, ws_ray, center, parameter.cloudsParameter.w + parameter.cloudsParameter.x, it1, it2);
    
    if (inIntersected) 
    {
        if (it1 * it2 < 0) 
        {
            //we're on ground.
            start = max(it2, 0);
            end = ot2;
        }
        else 
        {
            start = 0.0f;
            //we're inside atm, or above atm.
            if (ot1 * ot2 < 0.0) 
            {	          
                //Inside atm.
                if (it2 > 0.0)  
                {
                    //Look down.
                    end = it1;
                }
                else  
                {
                    //Look up.
                    end = ot2;
                } 

                start = 0.0f;
            } 
            else 
            {	//Outside atm
                if (ot1 < 0.0) 
                {
                    return false;
                }
                else 
                {
                    start = ot1;
                    end = it1;
                }
            }
        }
    }     
    else 
    {
        end = ot2;
        start = max(ot1, 0);
    }
    return true;
}

int inside = 0;

float2 ResolveRay(float3 pos, float3 ray, float3 center, float raymarchEnd, RaymarchParameters parameter)
{
    float sampleStart = 0;
    float sampleEnd = 0;
    
    if (!resolve_ray_start_end(pos, ray,center, parameter, sampleStart, sampleEnd)) 
    {
        return float2(0,0);
    } 

    float3 sampleStartPos = pos + (ray * sampleStart);
    if (sampleEnd <= sampleStart)
    {	
        return float2(0,0);
    }

    float ch = length(pos - center) -  parameter.cloudsParameter.w;
    //float height = RemapEnviro(pos.y, parameter.cloudsParameter.x, parameter.cloudsParameter.y * 0.75, 0, 1);
    //float end = lerp(_CloudsCoverageSettings.y * 0.85,_CloudsCoverageSettings.y * 1.25,height);

    raymarchEnd = min(raymarchEnd, _CloudsCoverageSettings.y);

    if (ch < parameter.cloudsParameter.x)
    { 
        sampleEnd = min(raymarchEnd, sampleEnd);
        inside = 0;
        
        if(ray.y < 0)
            return float2(0,0);
    }
    else if (ch > parameter.cloudsParameter.y)
    {
        sampleEnd = min(raymarchEnd, sampleEnd);
        inside = 0;
    }
    else                                               
    {     
        sampleEnd = min(raymarchEnd, sampleEnd);
        inside = 0;  
    } 
            
    return float2(max(0.0f,sampleStart), min(raymarchEnd, sampleEnd));
}

float CalculateLodMips(float distanceToCamera)
{
    return lerp(0.0, lerp(5.0,0.0,_LODDistance), saturate((distanceToCamera - 3000.0) / (100000.0 - 3000.0)));
}


float3 Raymarch (float3 cameraPos, float3 ray, float2 hitDistance, float3 center, RaymarchParameters parameters, float offset)
{                   
    float cloud_test = 0.0;
    int zero_density_sample_count = 0;
    float sampled_density_previous = -1.0;

    float alpha = 1.0; 
    float intensity = 0.0;
    float depth = 0.0;
    float depthWeightSum = 0.000001;
    float trans = 1.0f;

    int steps = (int)lerp(parameters.minSteps, parameters.maxSteps, ray.y);
    
    //int steps = parameters.maxSteps;
    float rayStepLength = (hitDistance.y - hitDistance.x) / steps;
    float3 rayStep = ray * rayStepLength;

    float3 pos = (cameraPos + (hitDistance.x) * ray);  
    pos += (offset * rayStepLength) * ray;
    //pos += rayStep;  
    pos += rayStep;
    float3 sampleEndPos = cameraPos + ray * hitDistance.y;
    float eyeToEnd = distance(cameraPos, sampleEndPos);
    float cosTheta = dot(ray, normalize(_LightDir));
    
    [loop]
    for (int i = 0; i < steps; i++)
    {   

    //Calculate projection height
    float height = GetSamplingHeight(pos, center, parameters.cloudsParameter);

    //Get out of expensive raymarching			
    if (alpha <= 0.01 || height > 1.0 || height < 0.0 || _CloudsCoverageSettings.x <= -0.9)
        break; 
    
    // Get Weather Data                                                                                
    float4  weather = GetWeather(pos);

    float distanceToCamera = length(pos - cameraPos);
    float lod = CalculateLodMips(distanceToCamera);

    if (cloud_test > 0.0) 
    {  
        float sampled_density = CalculateCloudDensity(pos, center, parameters, weather, 0, lod, true);

        if (sampled_density == 0.0 && sampled_density_previous == 0.0)
        { 
            zero_density_sample_count++;
        } 

        if (zero_density_sample_count < 8 && sampled_density != 0.0)
        { 
           
            float extinction = pow(max(parameters.density * sampled_density, 0.0001), parameters.densitySmoothness);
            float transmittance = exp(-extinction * rayStepLength);

            float ds_lod_local = sampled_density * (1.0 + lod * 0.8);
            float luminance = SampleEnergy(pos, cosTheta, center, parameters, _LightDir, height, sampled_density, rayStepLength,weather,lod);
            float integScatt = (luminance - luminance * transmittance);
        
            float depthWeight = trans;
            depth += depthWeight * distanceToCamera;
            depthWeightSum += depthWeight;

            intensity += trans * integScatt;

            trans *= transmittance;
            alpha *= max(transmittance, 0.0);

            if (alpha <= 0.01)  
                alpha = 0.0;
        }
        // if not, then set cloud_test to zero so that we go back to the cheap sample case
        else if(zero_density_sample_count >= 8)
        {
            cloud_test = 0.0;
            zero_density_sample_count = 0;
        }

        sampled_density_previous = sampled_density;
        pos += rayStep;   
    }
    else  
    {   
        // sample density the cheap way, only using the low frequency noise
        cloud_test = CalculateCloudDensity(pos, center, parameters, weather, 0, lod, (bool)inside);
        
        if (cloud_test == 0.0) 
        { 
                pos += rayStep * 2;
        }
        else  //take a step back and capture area we skipped.
        {
            pos -= rayStep; 
        }
    }
    

}

float distance = depth / depthWeightSum;

if (distance <= 0.0) 
{
    distance = length(sampleEndPos - cameraPos);
} 

alpha = saturate(1.0f - alpha);

return float3(intensity,distance,alpha);

}

float3 CalculateWorldPosition (float2 uv, float depth)
{
    float4x4 proj, eyeToWorld;

    if (unity_StereoEyeIndex == 0)
    {
        proj = _LeftViewFromScreen;
        eyeToWorld = _LeftWorldFromView;
    }
    else
    {
        proj = _RightViewFromScreen;
        eyeToWorld = _RightWorldFromView;
    }

    //bit of matrix math to take the screen space coord (u,v,depth) and transform to world space
    float2 uvClip = uv * 2.0 - 1.0;
    float clipDepth = depth; // Fix for OpenGl Core thanks to Lars Bertram
    clipDepth = (UNITY_NEAR_CLIP_VALUE < 0) ? clipDepth * 2 - 1 : clipDepth;
    float4 clipPos = float4(uvClip, clipDepth, 1.0);
    float4 viewPos = mul(proj, clipPos); // inverse projection by clip position
    viewPos /= viewPos.w; // perspective division
    return float3(mul(eyeToWorld, viewPos).xyz);
}

float RaymarchShadows (float3 cameraPos, float3 worldPos,float3 ray,float3 center, RaymarchParameters parameters, float offset,float depth)
{ 
    if(depth == 0.0f)
        return 0.0;

    int steps = 16;
    float worldDotLight = saturate(dot(float3(0, 1, 0), _LightDir));
    float bottomDist = max(0, parameters.cloudsParameter.x ) / worldDotLight;
    float topDist = max(0, parameters.cloudsParameter.y ) / worldDotLight;

    float rayStepLength = (topDist - bottomDist) / steps;
    float3 rayStep = _LightDir * rayStepLength;

    float3 pos =  worldPos + bottomDist * _LightDir;

    float4 weather = GetWeather(pos);
    float intensity = 0.05;

    float shadowIntensity = 0.0;

    float _Softness = 2.0f;

    [unroll]
    for (int i = 0; i < steps; i++)
    {
        float3 samplePos = rayStepLength * i * _LightDir + pos;
        float sampleResult = CalculateCloudDensity(samplePos, center, parameters, weather, 0, 0, true) * intensity;
        float result = sampleResult * (rayStepLength / (i + 1)); 
        shadowIntensity += result;

        //  if (shadowIntensity > 0.99) 
        //      break;
    }
    return (shadowIntensity);
}