using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Enviro
{
    [Serializable]
    public class EnviroWeather 
    {
        public List<EnviroWeatherType> weatherTypes = new List<EnviroWeatherType>();
        public float cloudsTransitionSpeed = 1f;
        public float fogTransitionSpeed = 1f;
        public float lightingTransitionSpeed = 1f; 
        public float skyTransitionSpeed = 1f; 
        public float effectsTransitionSpeed = 1f;
        public float auroraTransitionSpeed = 1f;
        public float environmentTransitionSpeed = 1f;
        public float audioTransitionSpeed = 1f;
    }  

    [Serializable]
    [ExecuteInEditMode] 
    public class EnviroWeatherModule : EnviroModule
    {  
        public Enviro.EnviroWeather Settings;
        public EnviroWeatherModule preset;
        public EnviroWeatherType targetWeatherType;
        public float weatherBlendProgress = 0f;

        //Zones
        public bool globalAutoWeatherChange = true;
        //Trigger
        public BoxCollider triggerCollider;
        public Rigidbody triggerRB;

        //UI
        public bool showWeatherPresetsControls,showTransitionControls,showZoneControls;

        private bool instantTransition = false;

        public override void Enable ()
        { 
            if(EnviroManager.instance == null)
               return;

            if(targetWeatherType == null && Settings.weatherTypes.Count > 0)
               targetWeatherType = Settings.weatherTypes[0];

            if(EnviroManager.instance.defaultZone != null)
               EnviroManager.instance.currentZone = EnviroManager.instance.defaultZone;

            weatherBlendProgress = 1f;

            Setup();
        } 

        public override void Disable ()
        { 
            if(EnviroManager.instance == null)
               return;

            Cleanup();
        }

        private void Setup()
        {
            if(EnviroManager.instance.gameObject.GetComponent<BoxCollider>() == null)
               triggerCollider = EnviroManager.instance.gameObject.AddComponent<BoxCollider>();
            else
               triggerCollider = EnviroManager.instance.gameObject.GetComponent<BoxCollider>();
            
            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector3(0.1f,0.1f,0.1f);

            if(EnviroManager.instance.gameObject.GetComponent<Rigidbody>() == null)
               triggerRB = EnviroManager.instance.gameObject.AddComponent<Rigidbody>();
            else
               triggerRB = EnviroManager.instance.gameObject.GetComponent<Rigidbody>();
 
            triggerRB.isKinematic = true;
        }  

        private void Cleanup()
        {
            if(triggerCollider != null)
               DestroyImmediate(triggerCollider);

            if(triggerRB != null)
               DestroyImmediate(triggerRB);
        } 

        /// Adds weather type to the list or creates a new one.
        public void CreateNewWeatherType()
        {
            EnviroWeatherType type = EnviroWeatherTypeCreation.CreateMyAsset();
            Settings.weatherTypes.Add(type);       
        #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
        #endif
        }

        /// Removes the weather type from the list.
        public void RemoveWeatherType(EnviroWeatherType type)
        {
            Settings.weatherTypes.Remove(type);
        #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
        #endif
        }

        //Cleans the list from null entries.
        public void CleanupList() 
        {
            for (int i = 0; i < Settings.weatherTypes.Count; i++)
            {
                if(Settings.weatherTypes[i] == null)
                    Settings.weatherTypes.RemoveAt(i);
            } 
        } 
 
        private IEnumerator InstantTransition()
        {
            yield return null;
            instantTransition = false;
        }

        // Update Method
        public override void UpdateModule ()
        { 
            if(!active)
               return; 

             if(EnviroManager.instance == null)
               return;
               
            //Instant changes when not playing or instant change is triggered
            if(!Application.isPlaying || instantTransition)
            {
                if(targetWeatherType != null)
                {
                    BlendVolumetricCloudsOverride(1f , 1f);
                    BlendFlatCloudsOverride(1f);  
                    BlendLightingOverride(1f);
                    BlendSkyOverride(1f);      
                    BlendEffectsOverride(1f); 
                    BlendAuroraOverride(1f);       
                    BlendFogOverride(1f);      
                    BlendAudioOverride(1f);
                    BlendEnvironmentOverride(1f);
                    BlendLightningOverride(1f);
                } 

                if(instantTransition)
                   instantTransition = false;
            }
            else
            {
                if(targetWeatherType != null)
                { 
                    BlendVolumetricCloudsOverride(Settings.cloudsTransitionSpeed * Time.deltaTime, 1f);
                    BlendFlatCloudsOverride(Settings.cloudsTransitionSpeed * Time.deltaTime);
                    BlendLightingOverride(Settings.lightingTransitionSpeed * Time.deltaTime);  
                    BlendSkyOverride(Settings.skyTransitionSpeed * Time.deltaTime);    
                    BlendEffectsOverride(Settings.effectsTransitionSpeed * Time.deltaTime);   
                    BlendAuroraOverride(Settings.auroraTransitionSpeed * Time.deltaTime);   
                    BlendFogOverride(Settings.fogTransitionSpeed * Time.deltaTime);      
                    BlendAudioOverride(Settings.audioTransitionSpeed * Time.deltaTime);
                    BlendEnvironmentOverride(Settings.environmentTransitionSpeed * Time.deltaTime);
                    BlendLightningOverride(1f);
                    UpdateWeatherBlendProgress(Settings.cloudsTransitionSpeed * Time.deltaTime);
                }
            }
        } 
 
        private void BlendLightingOverride(float blendTime)
        { 
            EnviroLightingModule lighting = EnviroManager.instance.Lighting;
            
            if(lighting != null) 
            {
                lighting.Settings.directLightIntensityModifier = Mathf.Lerp(lighting.Settings.directLightIntensityModifier, targetWeatherType.lightingOverride.directLightIntensityModifier,blendTime); 
                lighting.Settings.ambientIntensityModifier = Mathf.Lerp(lighting.Settings.ambientIntensityModifier, targetWeatherType.lightingOverride.ambientIntensityModifier,blendTime); 
                lighting.Settings.shadowIntensity = Mathf.Lerp(lighting.Settings.shadowIntensity, targetWeatherType.lightingOverride.shadowIntensity,blendTime); 
            }
        } 

        private void BlendSkyOverride(float blendTime)
        {
            EnviroSkyModule sky = EnviroManager.instance.Sky;
            
            if(sky != null) 
            {
                sky.Settings.mieScatteringMultiplier = Mathf.Lerp(sky.Settings.mieScatteringMultiplier, targetWeatherType.skyOverride.mieScatteringMultiplier,blendTime); 
             
                sky.Settings.skyColorExponent = Mathf.Lerp(sky.Settings.skyColorExponent, targetWeatherType.skyOverride.skyColorExponent,blendTime); 
                sky.Settings.skyColorTint = Color.Lerp(sky.Settings.skyColorTint, targetWeatherType.skyOverride.skyColorTint,blendTime); 
            }
        } 

        private void BlendFogOverride(float blendTime)
        {
            EnviroFogModule fog = EnviroManager.instance.Fog;
            
            if(fog != null)
            {
                fog.Settings.fogDensity = Mathf.Lerp(fog.Settings.fogDensity, targetWeatherType.fogOverride.fogDensity,blendTime); 
                fog.Settings.fogHeightFalloff = Mathf.Lerp(fog.Settings.fogHeightFalloff, targetWeatherType.fogOverride.fogHeightFalloff,blendTime);
                fog.Settings.fogHeight = Mathf.Lerp(fog.Settings.fogHeight, targetWeatherType.fogOverride.fogHeight,blendTime);

                fog.Settings.fogDensity2 = Mathf.Lerp(fog.Settings.fogDensity2, targetWeatherType.fogOverride.fogDensity2,blendTime); 
                fog.Settings.fogHeightFalloff2 = Mathf.Lerp(fog.Settings.fogHeightFalloff2, targetWeatherType.fogOverride.fogHeightFalloff2,blendTime);
                fog.Settings.fogHeight2 = Mathf.Lerp(fog.Settings.fogHeight2, targetWeatherType.fogOverride.fogHeight2,blendTime); 

                fog.Settings.fogColorBlend = Mathf.Lerp(fog.Settings.fogColorBlend, targetWeatherType.fogOverride.fogColorBlend,blendTime);
                fog.Settings.fogColorMod = Color.Lerp(fog.Settings.fogColorMod, targetWeatherType.fogOverride.fogColorMod,blendTime); 

                fog.Settings.scattering = Mathf.Lerp(fog.Settings.scattering, targetWeatherType.fogOverride.scattering,blendTime);
                fog.Settings.extinction = Mathf.Lerp(fog.Settings.extinction, targetWeatherType.fogOverride.extinction,blendTime);
                fog.Settings.anistropy = Mathf.Lerp(fog.Settings.anistropy, targetWeatherType.fogOverride.anistropy,blendTime);
 
                #if ENVIRO_HDRP
                fog.Settings.fogAttenuationDistance = Mathf.Lerp(fog.Settings.fogAttenuationDistance, targetWeatherType.fogOverride.fogAttenuationDistance,blendTime); 
                fog.Settings.baseHeight = Mathf.Lerp(fog.Settings.baseHeight, targetWeatherType.fogOverride.baseHeight,blendTime); 
                fog.Settings.maxHeight = Mathf.Lerp(fog.Settings.maxHeight, targetWeatherType.fogOverride.maxHeight,blendTime); 
                
                fog.Settings.ambientDimmer = Mathf.Lerp(fog.Settings.ambientDimmer, targetWeatherType.fogOverride.ambientDimmer,blendTime);
                fog.Settings.directLightMultiplier = Mathf.Lerp(fog.Settings.directLightMultiplier, targetWeatherType.fogOverride.directLightMultiplier,blendTime);
                fog.Settings.directLightShadowdimmer = Mathf.Lerp(fog.Settings.ambientDimmer, targetWeatherType.fogOverride.directLightShadowdimmer,blendTime);
                #endif

                fog.Settings.unityFogDensity = Mathf.Lerp(fog.Settings.unityFogDensity, targetWeatherType.fogOverride.unityFogDensity,blendTime);
                fog.Settings.unityFogStartDistance = Mathf.Lerp(fog.Settings.unityFogStartDistance, targetWeatherType.fogOverride.unityFogStartDistance,blendTime);
                fog.Settings.unityFogEndDistance = Mathf.Lerp(fog.Settings.unityFogEndDistance, targetWeatherType.fogOverride.unityFogEndDistance,blendTime);

            }
        }

        private void BlendEffectsOverride(float blendTime)
        {
            EnviroEffectsModule effects = EnviroManager.instance.Effects;
            
            if(effects != null)
            {
                for (int i = 0; i < effects.Settings.effectTypes.Count; i++)
                {
                    bool hasOverride = false;

                    for(int a = 0; a < targetWeatherType.effectsOverride.effectsOverride.Count; a++)
                    {
                        if(effects.Settings.effectTypes[i].name == targetWeatherType.effectsOverride.effectsOverride[a].name)
                        {
                           effects.Settings.effectTypes[i].emissionRate = Mathf.Lerp(effects.Settings.effectTypes[i].emissionRate, targetWeatherType.effectsOverride.effectsOverride[a].emission,blendTime); 
                           hasOverride = true;
                        }
                    } 

                    if(!hasOverride)
                    {
                        effects.Settings.effectTypes[i].emissionRate = Mathf.Lerp(effects.Settings.effectTypes[i].emissionRate, 0f,blendTime); 
                    }
                }
            }
        }

        private void BlendVolumetricCloudsOverride(float blendTime, float blendTime2)
        {
            EnviroVolumetricCloudsModule clouds = EnviroManager.instance.VolumetricClouds;

            if(clouds != null) 
            {   
                clouds.settingsGlobal.ambientLighIntensity = Mathf.Lerp(clouds.settingsGlobal.ambientLighIntensity, targetWeatherType.cloudsOverride.ambientLightIntensity,blendTime);
              
                clouds.settingsVolume.baseNoiseUVMultiplier = Mathf.Lerp(clouds.settingsVolume.baseNoiseUVMultiplier, targetWeatherType.cloudsOverride.baseNoiseUVMultiplier,blendTime2);
                clouds.settingsVolume.detailNoiseUVMultiplier = Mathf.Lerp(clouds.settingsVolume.detailNoiseUVMultiplier, targetWeatherType.cloudsOverride.detailNoiseUVMultiplier,blendTime2);

                clouds.settingsVolume.coverage = Mathf.Lerp(clouds.settingsVolume.coverage, targetWeatherType.cloudsOverride.coverage,blendTime);
                clouds.settingsVolume.dilateCoverage = Mathf.Lerp(clouds.settingsVolume.dilateCoverage, targetWeatherType.cloudsOverride.dilateCoverage,blendTime);
                clouds.settingsVolume.dilateType = Mathf.Lerp(clouds.settingsVolume.dilateType, targetWeatherType.cloudsOverride.dilateType,blendTime);
                clouds.settingsVolume.cloudsTypeModifier = Mathf.Lerp(clouds.settingsVolume.cloudsTypeModifier, targetWeatherType.cloudsOverride.typeModifier,blendTime);
                clouds.settingsVolume.cloudTypeShaping = Mathf.Lerp(clouds.settingsVolume.cloudTypeShaping, targetWeatherType.cloudsOverride.cloudTypeShaping,blendTime);
                clouds.settingsVolume.silverLiningIntensity = Mathf.Lerp(clouds.settingsVolume.silverLiningIntensity, targetWeatherType.cloudsOverride.silverLiningIntensity,blendTime);
                clouds.settingsVolume.edgeHighlightStrength = Mathf.Lerp(clouds.settingsVolume.edgeHighlightStrength, targetWeatherType.cloudsOverride.edgeHighlightStrength,blendTime);

 
                clouds.settingsVolume.bottomShape = Mathf.Lerp(clouds.settingsVolume.bottomShape, targetWeatherType.cloudsOverride.bottomShape,blendTime);
                clouds.settingsVolume.midShape = Mathf.Lerp(clouds.settingsVolume.midShape, targetWeatherType.cloudsOverride.midShape,blendTime);
                clouds.settingsVolume.topShape = Mathf.Lerp(clouds.settingsVolume.topShape, targetWeatherType.cloudsOverride.topShape,blendTime);
                clouds.settingsVolume.topLayer = Mathf.Lerp(clouds.settingsVolume.topLayer, targetWeatherType.cloudsOverride.topLayer,blendTime);
                clouds.settingsVolume.rampShape = Mathf.Lerp(clouds.settingsVolume.rampShape, targetWeatherType.cloudsOverride.rampShape,blendTime);


                clouds.settingsVolume.scatteringIntensity = Mathf.Lerp(clouds.settingsVolume.scatteringIntensity, targetWeatherType.cloudsOverride.scatteringIntensity,blendTime);
                clouds.settingsVolume.multiScatterStrength = Mathf.Lerp(clouds.settingsVolume.multiScatterStrength, targetWeatherType.cloudsOverride.multiScatterStrength,blendTime);
                clouds.settingsVolume.multiScatterFalloff = Mathf.Lerp(clouds.settingsVolume.multiScatterFalloff, targetWeatherType.cloudsOverride.multiScatterFalloff,blendTime);
                clouds.settingsVolume.ambientFloor = Mathf.Lerp(clouds.settingsVolume.ambientFloor, targetWeatherType.cloudsOverride.ambientFloor,blendTime);
                clouds.settingsVolume.lightningIntensity = Mathf.Lerp(clouds.settingsVolume.lightningIntensity, targetWeatherType.cloudsOverride.lightningIntensity,blendTime);
                clouds.settingsVolume.exposure = Mathf.Lerp(clouds.settingsVolume.exposure, targetWeatherType.cloudsOverride.exposure,blendTime);
                clouds.settingsVolume.silverLiningSpread = Mathf.Lerp(clouds.settingsVolume.silverLiningSpread, targetWeatherType.cloudsOverride.silverLiningSpread,blendTime);
                clouds.settingsVolume.absorbtion = Mathf.Lerp(clouds.settingsVolume.absorbtion, targetWeatherType.cloudsOverride.ligthAbsorbtion,blendTime);
                
                clouds.settingsVolume.density = Mathf.Lerp(clouds.settingsVolume.density, targetWeatherType.cloudsOverride.density,blendTime);
                clouds.settingsVolume.densitySmoothness = Mathf.Lerp(clouds.settingsVolume.densitySmoothness, targetWeatherType.cloudsOverride.densitySmoothness,blendTime);
                clouds.settingsVolume.baseErosionIntensity = Mathf.Lerp(clouds.settingsVolume.baseErosionIntensity, targetWeatherType.cloudsOverride.baseErosionIntensity,blendTime);
                clouds.settingsVolume.detailErosionIntensity = Mathf.Lerp(clouds.settingsVolume.detailErosionIntensity, targetWeatherType.cloudsOverride.detailErosionIntensity,blendTime);
                clouds.settingsVolume.curlIntensity = Mathf.Lerp(clouds.settingsVolume.curlIntensity, targetWeatherType.cloudsOverride.curlIntensity,blendTime);   

                clouds.settingsVolume.baseNoiseMultiplier = Mathf.Lerp(clouds.settingsVolume.baseNoiseMultiplier, targetWeatherType.cloudsOverride.baseNoiseMultiplier,blendTime);
                clouds.settingsVolume.detailNoiseMultiplier = Mathf.Lerp(clouds.settingsVolume.detailNoiseMultiplier, targetWeatherType.cloudsOverride.detailNoiseMultiplier,blendTime);
            }
        }

        private void BlendFlatCloudsOverride(float blendTime)
        {
            EnviroFlatCloudsModule flatClouds = EnviroManager.instance.FlatClouds;
            
            if(flatClouds != null)
            {
                flatClouds.settings.cirrusCloudsAlpha = Mathf.Lerp(flatClouds.settings.cirrusCloudsAlpha, targetWeatherType.flatCloudsOverride.cirrusCloudsAlpha,blendTime);
                flatClouds.settings.cirrusCloudsCoverage = Mathf.Lerp(flatClouds.settings.cirrusCloudsCoverage, targetWeatherType.flatCloudsOverride.cirrusCloudsCoverage,blendTime);
                flatClouds.settings.cirrusCloudsColorPower = Mathf.Lerp(flatClouds.settings.cirrusCloudsColorPower, targetWeatherType.flatCloudsOverride.cirrusCloudsColorPower,blendTime);
                flatClouds.settings.flatCloudsCoverage = Mathf.Lerp(flatClouds.settings.flatCloudsCoverage, targetWeatherType.flatCloudsOverride.flatCloudsCoverage,blendTime);
                flatClouds.settings.flatCloudsDensity = Mathf.Lerp(flatClouds.settings.flatCloudsDensity, targetWeatherType.flatCloudsOverride.flatCloudsDensity,blendTime);
                flatClouds.settings.flatCloudsLightIntensity = Mathf.Lerp(flatClouds.settings.flatCloudsLightIntensity, targetWeatherType.flatCloudsOverride.flatCloudsLightIntensity,blendTime);
                flatClouds.settings.flatCloudsAmbientIntensity = Mathf.Lerp(flatClouds.settings.flatCloudsAmbientIntensity, targetWeatherType.flatCloudsOverride.flatCloudsAmbientIntensity,blendTime);
                flatClouds.settings.flatCloudsShadowIntensity = Mathf.Lerp(flatClouds.settings.flatCloudsShadowIntensity, targetWeatherType.flatCloudsOverride.flatCloudsShadowIntensity,blendTime);     
            }
        }

        private void BlendAuroraOverride(float blendTime)
        {
            EnviroAuroraModule aurora = EnviroManager.instance.Aurora;
            
            if(aurora != null)
            {
                aurora.Settings.auroraIntensityModifier = Mathf.Lerp(aurora.Settings.auroraIntensityModifier, targetWeatherType.auroraOverride.auroraIntensity,blendTime); 
            }
        }

        private void BlendEnvironmentOverride(float blendTime)
        {
            EnviroEnvironmentModule environment = EnviroManager.instance.Environment;
            
            if(environment != null)
            {
                environment.Settings.temperatureWeatherMod = Mathf.Lerp(environment.Settings.temperatureWeatherMod, targetWeatherType.environmentOverride.temperatureWeatherMod,blendTime);
                environment.Settings.wetnessTarget = Mathf.Lerp(environment.Settings.wetnessTarget, targetWeatherType.environmentOverride.wetnessTarget,blendTime); 
                environment.Settings.snowTarget = Mathf.Lerp(environment.Settings.snowTarget, targetWeatherType.environmentOverride.snowTarget,blendTime); 
            
                environment.Settings.windDirectionX = Mathf.Lerp(environment.Settings.windDirectionX, targetWeatherType.environmentOverride.windDirectionX,blendTime); 
                environment.Settings.windDirectionY = Mathf.Lerp(environment.Settings.windDirectionY, targetWeatherType.environmentOverride.windDirectionY,blendTime); 
                environment.Settings.windSpeed = Mathf.Lerp(environment.Settings.windSpeed, targetWeatherType.environmentOverride.windSpeed,blendTime); 
                environment.Settings.windTurbulence = Mathf.Lerp(environment.Settings.windTurbulence, targetWeatherType.environmentOverride.windTurbulence,blendTime); 
            } 
        }

        private void BlendAudioOverride(float blendTime)
        {
            EnviroAudioModule audio = EnviroManager.instance.Audio;
            
            if(audio != null)
            {
                for(int i = 0; i < audio.Settings.ambientClips.Count; i++)
                {
                    bool hasOverride = false;

                    for(int a = 0; a < targetWeatherType.audioOverride.ambientOverride.Count; a++)
                    {
                        if(targetWeatherType.audioOverride.ambientOverride[a].name == audio.Settings.ambientClips[i].name)
                        {
                            audio.Settings.ambientClips[i].volume = Mathf.Lerp(audio.Settings.ambientClips[i].volume ,targetWeatherType.audioOverride.ambientOverride[a].volume,blendTime); 
                            hasOverride = true;
                        }
                    }

                    if(!hasOverride)
                        audio.Settings.ambientClips[i].volume = Mathf.Lerp(audio.Settings.ambientClips[i].volume ,0f,blendTime); 
                }

                for(int i = 0; i < audio.Settings.weatherClips.Count; i++)
                {
                    bool hasOverride = false;

                    for(int a = 0; a < targetWeatherType.audioOverride.weatherOverride.Count; a++)
                    {
                        if(targetWeatherType.audioOverride.weatherOverride[a].name == audio.Settings.weatherClips[i].name)
                        {
                            audio.Settings.weatherClips[i].volume = Mathf.Lerp(audio.Settings.weatherClips[i].volume ,targetWeatherType.audioOverride.weatherOverride[a].volume,blendTime); 
                            hasOverride = true;
                        }
                    }

                    if(!hasOverride)
                        audio.Settings.weatherClips[i].volume = Mathf.Lerp(audio.Settings.weatherClips[i].volume ,0f,blendTime); 
                }          
            }

        }

        private void BlendLightningOverride(float blendTime)
        {
            EnviroLightningModule lightning = EnviroManager.instance.Lightning;
             
            if(lightning != null)
            {
                lightning.Settings.lightningStorm = targetWeatherType.lightningOverride.lightningStorm; 
                lightning.Settings.randomLightingDelay = Mathf.Lerp(lightning.Settings.randomLightingDelay, targetWeatherType.lightningOverride.randomLightningDelay,blendTime); 
            }
        } 

        private void UpdateWeatherBlendProgress(float blendTime)
        {
                //float currentCov = Math.Abs(EnviroManager.instance.VolumetricClouds.settingsVolume.coverage);
                //float targetCov = Math.Abs(targetWeatherType.cloudsOverride.coverageLayer1);
                //weatherBlendProgress = Mathf.Lerp(1f,0f,targetCov - currentCov);   
                weatherBlendProgress = Mathf.Lerp(weatherBlendProgress, 1f,blendTime);
                weatherBlendProgress = Mathf.Clamp01(weatherBlendProgress);
  
                if(weatherBlendProgress >= 0.99)
                   weatherBlendProgress = 1f;
        }



        //Changes the Weather to new type.
        public void ChangeWeather(EnviroWeatherType type)
        { 
            if(targetWeatherType != type)
            {
                EnviroManager.instance.NotifyWeatherChanged(type);
                EnviroManager.instance.NotifyZoneWeatherChanged(type,null);
            }

            if(EnviroManager.instance.currentZone != null)
               EnviroManager.instance.currentZone.currentWeatherType = type;
    
            targetWeatherType = type;
            weatherBlendProgress = 0f;
        }
        public void ChangeWeather(string typeName)
        {
            for(int i = 0; i < Settings.weatherTypes.Count; i++)
            {
                if(Settings.weatherTypes[i].name == typeName)
                {
                    if(targetWeatherType != Settings.weatherTypes[i])
                    {
                        EnviroManager.instance.NotifyWeatherChanged(Settings.weatherTypes[i]);
                        EnviroManager.instance.NotifyZoneWeatherChanged(Settings.weatherTypes[i],null);
                    }

                    if(EnviroManager.instance.currentZone != null)
                       EnviroManager.instance.currentZone.currentWeatherType = Settings.weatherTypes[i];

                    targetWeatherType = Settings.weatherTypes[i];
                    weatherBlendProgress = 0f;
                }
            }
        }

        public void ChangeWeather(int index)
        {
            for(int i = 0; i < Settings.weatherTypes.Count; i++)
            {
                if(i == index)
                {
                    if(targetWeatherType != Settings.weatherTypes[i])
                    {
                        EnviroManager.instance.NotifyWeatherChanged(Settings.weatherTypes[i]);
                        EnviroManager.instance.NotifyZoneWeatherChanged(Settings.weatherTypes[i],null);
                    }

                    if(EnviroManager.instance.currentZone != null)
                       EnviroManager.instance.currentZone.currentWeatherType = Settings.weatherTypes[i];

                    targetWeatherType = Settings.weatherTypes[i];
                     weatherBlendProgress = 0f;
                    return;
                }
            } 
        }

        public void ChangeZoneWeather(int weather, int zone)
        {
            if(EnviroManager.instance.zones.Count >= zone && Settings.weatherTypes.Count >= weather)
            {
                EnviroManager.instance.zones[zone].currentWeatherType = Settings.weatherTypes[weather];
                EnviroManager.instance.NotifyZoneWeatherChanged(Settings.weatherTypes[weather],EnviroManager.instance.zones[zone]);
            }  
        }

        public void ChangeWeatherInstant(EnviroWeatherType type)
        {
            if(targetWeatherType != type)
            {
                EnviroManager.instance.NotifyWeatherChanged(type);
                EnviroManager.instance.NotifyZoneWeatherChanged(type,null);
            }
 
            if(EnviroManager.instance.currentZone != null)
               EnviroManager.instance.currentZone.currentWeatherType = type;

            targetWeatherType = type;
            weatherBlendProgress = 1f;
            instantTransition = true;
        }

        public void ChangeWeatherInstant(string typeName)
        {
            for(int i = 0; i < Settings.weatherTypes.Count; i++)
            {
                if(Settings.weatherTypes[i].name == typeName)
                {
                    if(targetWeatherType != Settings.weatherTypes[i])
                    {
                        EnviroManager.instance.NotifyWeatherChanged(Settings.weatherTypes[i]);
                        EnviroManager.instance.NotifyZoneWeatherChanged(Settings.weatherTypes[i],null);
                    }

                    if(EnviroManager.instance.currentZone != null)
                       EnviroManager.instance.currentZone.currentWeatherType = Settings.weatherTypes[i];

                    targetWeatherType = Settings.weatherTypes[i];
                    weatherBlendProgress = 1f;
                    instantTransition = true;
                }
            }
        }
 
        public void ChangeWeatherInstant(int index)
        {
            for(int i = 0; i < Settings.weatherTypes.Count; i++)
            {
                if(i == index)
                {
                    if(targetWeatherType != Settings.weatherTypes[i])
                    {
                        EnviroManager.instance.NotifyWeatherChanged(Settings.weatherTypes[i]);
                        EnviroManager.instance.NotifyZoneWeatherChanged(Settings.weatherTypes[i],null);
                    }

                    if(EnviroManager.instance.currentZone != null)
                       EnviroManager.instance.currentZone.currentWeatherType = Settings.weatherTypes[i];

                    targetWeatherType = Settings.weatherTypes[i];
                    weatherBlendProgress = 1f;
                    instantTransition = true;
                    return;
                }
            }
        }

        public void RegisterZone(EnviroZone zone)
        {
            EnviroManager.instance.zones.Add(zone);
        }

        public void RemoveZone(EnviroZone zone)
        {
            if(EnviroManager.instance.zones.Contains(zone))
               EnviroManager.instance.zones.Remove(zone);
        }

        //Save and Load
        public void LoadModuleValues ()
        {
            if(preset != null)
            {
                Settings = JsonUtility.FromJson<Enviro.EnviroWeather>(JsonUtility.ToJson(preset.Settings));
            }
            else
            {
                Debug.Log("Please assign a saved module to load from!");
            }
        }
 
        public void SaveModuleValues ()
        {
#if UNITY_EDITOR
        EnviroWeatherModule t =  ScriptableObject.CreateInstance<EnviroWeatherModule>();
        t.name = "Weather Module";
        t.Settings = JsonUtility.FromJson<Enviro.EnviroWeather>(JsonUtility.ToJson(Settings));
 
        string assetPathAndName = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(EnviroHelper.assetPath + "/New " + t.name + ".asset");
        UnityEditor.AssetDatabase.CreateAsset(t, assetPathAndName);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
#endif
        }

        public void SaveModuleValues (EnviroWeatherModule module)
        {
            module.Settings = JsonUtility.FromJson<Enviro.EnviroWeather>(JsonUtility.ToJson(Settings));
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(module);
            UnityEditor.AssetDatabase.SaveAssets();
            #endif
        }
    }
}