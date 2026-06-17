using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Enviro
{
    [Serializable]
    public class EnviroLighting
    {
        // DirectLighting
        public enum LightingMode
        {
            Single,
            Dual
        };

        public LightingMode lightingMode;
        public bool setDirectLighting = true;
        public int updateIntervallFrames = 2; 
        public AnimationCurve sunIntensityCurve;
        public AnimationCurve moonIntensityCurve;
        [GradientUsageAttribute(true)]
        public Gradient sunColorGradient;
        [GradientUsageAttribute(true)]
        public Gradient moonColorGradient;
        public AnimationCurve sunIntensityCurveHDRP = new AnimationCurve();
        public AnimationCurve moonIntensityCurveHDRP  = new AnimationCurve();
        public AnimationCurve lightColorTemperatureHDRP = new AnimationCurve();
        [GradientUsageAttribute(true)]
        public Gradient ambientColorTintHDRP;
        public float lightIntensityHDRP = 750f;
        public bool controlExposure = true;
        public AnimationCurve sceneExposure = new AnimationCurve();
        public bool controlIndirectLighting = true;
        public AnimationCurve diffuseIndirectIntensity = new AnimationCurve();
        public AnimationCurve reflectionIndirectIntensity = new AnimationCurve();

        [Range(0f,2f)]
        public float directLightIntensityModifier = 1f;

        //Ambient Lighting
        public bool setAmbientLighting = true;
        public UnityEngine.Rendering.AmbientMode ambientMode;
        [GradientUsage(true)]
        public Gradient ambientSkyColorGradient;
        [GradientUsage(true)]
        public Gradient ambientEquatorColorGradient;
        [GradientUsage(true)]
        public Gradient ambientGroundColorGradient;
        public AnimationCurve ambientIntensityCurve;

        [Range(0f,2f)]
        public float ambientIntensityModifier = 1f;

        public bool ambientUpdateEveryFrame = false;

        [Range(0f,2f)]
        public float ambientUpdateIntervall = 0.1f;
              
        [Range(0f,1f)]
        public float shadowIntensity = 1f;
    } 

    [Serializable]
    [ExecuteInEditMode]
    public class EnviroLightingModule : EnviroModule
    {
        public Enviro.EnviroLighting Settings;
        public EnviroLightingModule preset;

        private int currentFrame;
        private float lastAmbientSkyboxUpdate;

        //Inspector
        public bool showDirectLightingControls;
        public bool showAmbientLightingControls;
        public bool showReflectionControls;

        #if ENVIRO_HDRP
        public UnityEngine.Rendering.HighDefinition.HDAdditionalLightData directionalLightHDRP;
        public UnityEngine.Rendering.HighDefinition.HDAdditionalLightData additionalLightHDRP;
        public UnityEngine.Rendering.HighDefinition.Exposure exposureHDRP;
        public UnityEngine.Rendering.HighDefinition.IndirectLightingController indirectLightingHDRP;
        #endif

        public override void Enable ()
        {
            if(EnviroManager.instance == null)
               return;

            Setup();
        }

        public override void Disable ()
        {
            if(EnviroManager.instance == null)
               return;

            Cleanup();
        }

        //Applies changes when you switch the lighting mode.
        public void ApplyLightingChanges ()
        {
            Cleanup();
            Setup();
        }

        private void Setup()
        {
            if(EnviroManager.instance.Objects.directionalLight == null)
            {
                GameObject newLight = new GameObject();

                if(Settings.lightingMode == EnviroLighting.LightingMode.Single)
                    newLight.name = "Sun and Moon Directional Light";
                else
                    newLight.name = "Sun Directional Light";

                newLight.transform.SetParent(EnviroManager.instance.transform);
                newLight.transform.localPosition = Vector3.zero;
                EnviroManager.instance.Objects.directionalLight = newLight.AddComponent<Light>();
                EnviroManager.instance.Objects.directionalLight.type = LightType.Directional;
                EnviroManager.instance.Objects.directionalLight.shadows = LightShadows.Soft;
            }

            if(EnviroManager.instance.Objects.additionalDirectionalLight == null && Settings.lightingMode == EnviroLighting.LightingMode.Dual)
            {
                GameObject newLight = new GameObject();
                newLight.name = "Moon Directional Light";
                newLight.transform.SetParent(EnviroManager.instance.transform);
                newLight.transform.localPosition = Vector3.zero;
                EnviroManager.instance.Objects.additionalDirectionalLight = newLight.AddComponent<Light>();
                EnviroManager.instance.Objects.additionalDirectionalLight.type = LightType.Directional;
                EnviroManager.instance.Objects.additionalDirectionalLight.shadows = LightShadows.Soft;
            }
            else if (EnviroManager.instance.Objects.additionalDirectionalLight != null && Settings.lightingMode == EnviroLighting.LightingMode.Single)
            {
                DestroyImmediate(EnviroManager.instance.Objects.additionalDirectionalLight.gameObject);
            }
        }

        private void Cleanup()
        {
            if(EnviroManager.instance == null)
               return;

            if(EnviroManager.instance.Objects.directionalLight != null)
               DestroyImmediate(EnviroManager.instance.Objects.directionalLight.gameObject);

            if(EnviroManager.instance.Objects.additionalDirectionalLight != null)
               DestroyImmediate(EnviroManager.instance.Objects.additionalDirectionalLight.gameObject);
        }

        // Update Method
        public override void UpdateModule ()
        {
            if(!active)
               return; 

             if(EnviroManager.instance == null)
               return;

            currentFrame++;

            if(currentFrame >= Settings.updateIntervallFrames)
            {
                EnviroManager.instance.updateSkyAndLighting = true;
                currentFrame = 0;
            }
            else
            {
                EnviroManager.instance.updateSkyAndLighting = false;
            }



            //Update Direct Lighting
            if(EnviroManager.instance.Objects.directionalLight != null && Settings.setDirectLighting)
            {
                #if !ENVIRO_HDRP
                if(EnviroManager.instance.updateSkyAndLighting)
                   UpdateDirectLighting ();
                #else
                if(EnviroManager.instance.updateSkyAndLighting)
                    UpdateDirectLightingHDRP(); 
                #endif
            }
 
            if (Settings.setAmbientLighting)
            {
                #if !ENVIRO_HDRP
                UpdateAmbientLighting (Settings.ambientUpdateEveryFrame);
                #else
                if(EnviroManager.instance.updateSkyAndLighting)
                   UpdateAmbientLightingHDRP ();
                #endif
            }

            #if ENVIRO_HDRP
            if(EnviroManager.instance.updateSkyAndLighting)
                UpdateExposureHDRP ();
            #endif

     
        }

        public void UpdateDirectLighting ()
        {
            if(Settings.lightingMode == EnviroLighting.LightingMode.Single)
            {
                if(!EnviroManager.instance.isNight)
                {
                    //Set light to sun
                    EnviroManager.instance.Objects.directionalLight.transform.rotation = EnviroManager.instance.Objects.sun.transform.rotation;
                    EnviroManager.instance.Objects.directionalLight.intensity = Settings.sunIntensityCurve.Evaluate(EnviroManager.instance.solarTime) * Settings.directLightIntensityModifier;
                    EnviroManager.instance.Objects.directionalLight.color = Settings.sunColorGradient.Evaluate(EnviroManager.instance.solarTime);
                }
                else
                {
                    //Set light to moon
                    EnviroManager.instance.Objects.directionalLight.transform.rotation = EnviroManager.instance.Objects.moon.transform.rotation;
                    EnviroManager.instance.Objects.directionalLight.intensity = Settings.moonIntensityCurve.Evaluate(EnviroManager.instance.lunarTime) * Settings.directLightIntensityModifier;
                    EnviroManager.instance.Objects.directionalLight.color = Settings.moonColorGradient.Evaluate(EnviroManager.instance.lunarTime);
                }
                
                    EnviroManager.instance.Objects.directionalLight.shadowStrength = Settings.shadowIntensity;    
            }
            else
            {
                //Sun
                EnviroManager.instance.Objects.directionalLight.transform.rotation = EnviroManager.instance.Objects.sun.transform.rotation;
                EnviroManager.instance.Objects.directionalLight.intensity = Settings.sunIntensityCurve.Evaluate(EnviroManager.instance.solarTime) * Settings.directLightIntensityModifier;
                EnviroManager.instance.Objects.directionalLight.color = Settings.sunColorGradient.Evaluate(EnviroManager.instance.solarTime);
                EnviroManager.instance.Objects.directionalLight.shadowStrength = Settings.shadowIntensity;    
                //Moon
                EnviroManager.instance.Objects.additionalDirectionalLight.transform.rotation = EnviroManager.instance.Objects.moon.transform.rotation;
                EnviroManager.instance.Objects.additionalDirectionalLight.intensity = Settings.moonIntensityCurve.Evaluate(EnviroManager.instance.lunarTime) * Settings.directLightIntensityModifier;
                EnviroManager.instance.Objects.additionalDirectionalLight.color = Settings.moonColorGradient.Evaluate(EnviroManager.instance.lunarTime);
                EnviroManager.instance.Objects.additionalDirectionalLight.shadowStrength = Settings.shadowIntensity;               
            }

           
        }

#if ENVIRO_HDRP
        public void UpdateDirectLightingHDRP ()
        {
            if(directionalLightHDRP == null && EnviroManager.instance.Objects.directionalLight != null)
               directionalLightHDRP = EnviroManager.instance.Objects.directionalLight.gameObject.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();

            if(additionalLightHDRP == null && EnviroManager.instance.Objects.additionalDirectionalLight != null)
               additionalLightHDRP = EnviroManager.instance.Objects.additionalDirectionalLight.gameObject.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();

            if(Settings.lightingMode == EnviroLighting.LightingMode.Single)
            {
                if(!EnviroManager.instance.isNight)
                {
                    //Set light to sun
                    EnviroManager.instance.Objects.directionalLight.transform.rotation = EnviroManager.instance.Objects.sun.transform.rotation;
                    EnviroManager.instance.Objects.directionalLight.color = Settings.sunColorGradient.Evaluate(EnviroManager.instance.solarTime);
                    EnviroManager.instance.Objects.directionalLight.useColorTemperature = true;
                    EnviroManager.instance.Objects.directionalLight.colorTemperature = Settings.lightColorTemperatureHDRP.Evaluate(EnviroManager.instance.solarTime);

                #if UNITY_6000_0_OR_NEWER
                    EnviroManager.instance.Objects.directionalLight.intensity = Settings.sunIntensityCurve.Evaluate(EnviroManager.instance.solarTime) * Settings.lightIntensityHDRP * Settings.directLightIntensityModifier;
                #else
                    if(directionalLightHDRP != null)
                       directionalLightHDRP.SetIntensity(Settings.sunIntensityCurve.Evaluate(EnviroManager.instance.solarTime) * Settings.lightIntensityHDRP * Settings.directLightIntensityModifier);
                #endif
                 
                }
                else
                {
                    //Set light to moon
                    EnviroManager.instance.Objects.directionalLight.transform.rotation = EnviroManager.instance.Objects.moon.transform.rotation;
                    EnviroManager.instance.Objects.directionalLight.color = Settings.moonColorGradient.Evaluate(EnviroManager.instance.lunarTime);
                    EnviroManager.instance.Objects.directionalLight.useColorTemperature = true;
                    EnviroManager.instance.Objects.directionalLight.colorTemperature = Settings.lightColorTemperatureHDRP.Evaluate(EnviroManager.instance.solarTime);
#if UNITY_6000_0_OR_NEWER
                    EnviroManager.instance.Objects.directionalLight.intensity = Settings.moonIntensityCurve.Evaluate(EnviroManager.instance.lunarTime) * Settings.lightIntensityHDRP* Settings.directLightIntensityModifier;
#else
                    if (directionalLightHDRP != null)
                        directionalLightHDRP.SetIntensity(Settings.moonIntensityCurve.Evaluate(EnviroManager.instance.lunarTime) * Settings.lightIntensityHDRP * Settings.directLightIntensityModifier);
#endif
                }

                if(directionalLightHDRP != null)
                    directionalLightHDRP.shadowDimmer = Settings.shadowIntensity;
            }
            else
            {
                //Sun
                EnviroManager.instance.Objects.directionalLight.transform.rotation = EnviroManager.instance.Objects.sun.transform.rotation;
                EnviroManager.instance.Objects.directionalLight.color = Settings.sunColorGradient.Evaluate(EnviroManager.instance.solarTime);
                EnviroManager.instance.Objects.directionalLight.useColorTemperature = true;
                EnviroManager.instance.Objects.directionalLight.colorTemperature = Settings.lightColorTemperatureHDRP.Evaluate(EnviroManager.instance.solarTime);

                if(directionalLightHDRP != null)
                {
            #if UNITY_6000_0_OR_NEWER
                    EnviroManager.instance.Objects.directionalLight.intensity = Settings.sunIntensityCurve.Evaluate(EnviroManager.instance.solarTime) * Settings.lightIntensityHDRP* Settings.directLightIntensityModifier;
                #else
                    directionalLightHDRP.SetIntensity(Settings.sunIntensityCurve.Evaluate(EnviroManager.instance.solarTime) * Settings.lightIntensityHDRP * Settings.directLightIntensityModifier);
                #endif
                  
                   directionalLightHDRP.shadowDimmer = Settings.shadowIntensity;
                }
                //Moon
                if(EnviroManager.instance.Objects.additionalDirectionalLight != null)
                {
                    EnviroManager.instance.Objects.additionalDirectionalLight.transform.rotation = EnviroManager.instance.Objects.moon.transform.rotation;
                    EnviroManager.instance.Objects.additionalDirectionalLight.color = Settings.moonColorGradient.Evaluate(EnviroManager.instance.lunarTime);
                    EnviroManager.instance.Objects.additionalDirectionalLight.useColorTemperature = true;
                    EnviroManager.instance.Objects.additionalDirectionalLight.colorTemperature = Settings.lightColorTemperatureHDRP.Evaluate(EnviroManager.instance.solarTime);
                }

                if(additionalLightHDRP != null)
                {
#if UNITY_6000_0_OR_NEWER
                    EnviroManager.instance.Objects.directionalLight.intensity = Settings.moonIntensityCurve.Evaluate(EnviroManager.instance.lunarTime) * Settings.lightIntensityHDRP * Settings.directLightIntensityModifier;
                    #else
                    additionalLightHDRP.SetIntensity(Settings.moonIntensityCurve.Evaluate(EnviroManager.instance.lunarTime) * Settings.lightIntensityHDRP * Settings.directLightIntensityModifier);
                    #endif
                   
                    additionalLightHDRP.shadowDimmer = Settings.shadowIntensity;
                }
            }
        }

        public void UpdateAmbientLightingHDRP ()
        {
            if(EnviroManager.instance.volumeHDRP != null && EnviroManager.instance.volumeProfileHDRP != null)
            {
                if(indirectLightingHDRP == null)
                {
                    UnityEngine.Rendering.HighDefinition.IndirectLightingController TempIndirectLight;

                    if (EnviroManager.instance.volumeProfileHDRP.TryGet<UnityEngine.Rendering.HighDefinition.IndirectLightingController>(out TempIndirectLight))
                    {
                        indirectLightingHDRP = TempIndirectLight;
                    }
                    else
                    {
                        EnviroManager.instance.volumeProfileHDRP.Add<UnityEngine.Rendering.HighDefinition.IndirectLightingController>();

                        if (EnviroManager.instance.volumeProfileHDRP.TryGet<UnityEngine.Rendering.HighDefinition.IndirectLightingController>(out TempIndirectLight))
                        {
                            indirectLightingHDRP = TempIndirectLight;
                        }
                    }
                } 
                else
                {
                    if(Settings.controlIndirectLighting)
                    {
                        indirectLightingHDRP.active = true;
                        indirectLightingHDRP.indirectDiffuseLightingMultiplier.overrideState = true;
                        indirectLightingHDRP.indirectDiffuseLightingMultiplier.value = Settings.diffuseIndirectIntensity.Evaluate(EnviroManager.instance.solarTime) * Settings.ambientIntensityModifier;
                        indirectLightingHDRP.reflectionLightingMultiplier.overrideState = true;
                        indirectLightingHDRP.reflectionLightingMultiplier.value = Settings.reflectionIndirectIntensity.Evaluate(EnviroManager.instance.solarTime);
                    }
                    else
                    {
                        indirectLightingHDRP.active = false;
                    }
                }


            }
        }

        public void UpdateExposureHDRP ()
        {
            if(EnviroManager.instance.volumeHDRP != null && EnviroManager.instance.volumeProfileHDRP != null)
            {
                if(exposureHDRP == null)
                {
                    UnityEngine.Rendering.HighDefinition.Exposure TempExposure;

                    if (EnviroManager.instance.volumeProfileHDRP.TryGet<UnityEngine.Rendering.HighDefinition.Exposure>(out TempExposure))
                    {
                        exposureHDRP = TempExposure;
                    }
                    else
                    {
                        EnviroManager.instance.volumeProfileHDRP.Add<UnityEngine.Rendering.HighDefinition.Exposure>();

                        if (EnviroManager.instance.volumeProfileHDRP.TryGet<UnityEngine.Rendering.HighDefinition.Exposure>(out TempExposure))
                        {
                            exposureHDRP = TempExposure;
                        }
                    }
                } 
                else
                {
                    if(Settings.controlExposure)
                    {
                        exposureHDRP.active = true;
                        exposureHDRP.mode.overrideState = true;
                        exposureHDRP.mode.value = UnityEngine.Rendering.HighDefinition.ExposureMode.Fixed;
                        exposureHDRP.fixedExposure.overrideState = true;
                        exposureHDRP.fixedExposure.value = Settings.sceneExposure.Evaluate(EnviroManager.instance.solarTime);
                    }
                    else
                    {
                        exposureHDRP.active = false;
                    }
                }
            } 
        }
#endif

        public void UpdateAmbientLighting (bool forced = false)
        {
            RenderSettings.ambientMode = Settings.ambientMode;
 
            float intensity = Settings.ambientIntensityCurve.Evaluate(EnviroManager.instance.solarTime) *  Settings.ambientIntensityModifier;

            RenderSettings.ambientIntensity = intensity;
             
            if(forced)
            {
                UpdateAmbient(Settings.ambientMode,intensity);
                
                if(EnviroManager.instance.Time != null)
                {
                    lastAmbientSkyboxUpdate = EnviroManager.instance.Time.Settings.timeOfDay + Settings.ambientUpdateIntervall;
                }
            }
            else
            { 
                if(EnviroManager.instance.Time != null)
                {
                    if (lastAmbientSkyboxUpdate < EnviroManager.instance.Time.Settings.timeOfDay || lastAmbientSkyboxUpdate > EnviroManager.instance.Time.Settings.timeOfDay + (Settings.ambientUpdateIntervall + 0.01f))
                    {
                        UpdateAmbient(Settings.ambientMode,intensity);
                        lastAmbientSkyboxUpdate = EnviroManager.instance.Time.Settings.timeOfDay + Settings.ambientUpdateIntervall;
                    }
                }
                else
                {
                    if (lastAmbientSkyboxUpdate < Time.time)
                    {
                        UpdateAmbient(Settings.ambientMode,intensity);
                        lastAmbientSkyboxUpdate = Time.time + (Settings.ambientUpdateIntervall * 60);
                    }
                }
            }
        }

        private void UpdateAmbient(UnityEngine.Rendering.AmbientMode ambientMode, float intensity)
        {   
            switch (ambientMode)
            {
                case UnityEngine.Rendering.AmbientMode.Flat:
                    RenderSettings.ambientSkyColor = Settings.ambientSkyColorGradient.Evaluate(EnviroManager.instance.solarTime) * intensity;
                break;

                case UnityEngine.Rendering.AmbientMode.Trilight:
                    RenderSettings.ambientSkyColor = Settings.ambientSkyColorGradient.Evaluate(EnviroManager.instance.solarTime) * intensity;
                    RenderSettings.ambientEquatorColor = Settings.ambientEquatorColorGradient.Evaluate(EnviroManager.instance.solarTime) * intensity;
                    RenderSettings.ambientGroundColor = Settings.ambientGroundColorGradient.Evaluate(EnviroManager.instance.solarTime) * intensity;
                break;

                case UnityEngine.Rendering.AmbientMode.Skybox:
                    DynamicGI.UpdateEnvironment();
                break;
            }
        }

        //Save and Load
        public void LoadModuleValues ()
        {
            if(preset != null)
            {
                Settings = JsonUtility.FromJson<Enviro.EnviroLighting>(JsonUtility.ToJson(preset.Settings));
            }
            else
            {
                Debug.Log("Please assign a saved module to load from!");
            }
        }

        public void SaveModuleValues ()
        {
#if UNITY_EDITOR
        EnviroLightingModule t =  ScriptableObject.CreateInstance<EnviroLightingModule>();
        t.name = "Lighting Module";
        t.Settings = JsonUtility.FromJson<Enviro.EnviroLighting>(JsonUtility.ToJson(Settings));

        string assetPathAndName = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(EnviroHelper.assetPath + "/New " + t.name + ".asset");
        UnityEditor.AssetDatabase.CreateAsset(t, assetPathAndName);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
#endif
        }

        public void SaveModuleValues (EnviroLightingModule module)
        {
            module.Settings = JsonUtility.FromJson<Enviro.EnviroLighting>(JsonUtility.ToJson(Settings));
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(module);
            UnityEditor.AssetDatabase.SaveAssets();
            #endif
        }
    }
}