using System.Collections; 
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using UnityEngine.Rendering;

namespace Enviro 
{
    [Serializable]
    public class EnviroFogSettings
    { 
        /// Volumetrics Settings
        public bool volumetrics = true;
        public enum Quality
        {
            Low,
            Medium,
            High 
        }
 
        public int steps = 32;
        public Quality quality;

        [Range(0f,2f)]
        public float scattering;

        public AnimationCurve scatteringMultiplier = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 1));
        [Range(0f,1f)]
        public float extinction;
        [Range(0f,1f)] 
        public float anistropy; 
        public float maxRange = 1000f;
        public float maxRangePointSpot = 100f;

        [Range(0f,1f)] 
        public float noiseIntensity;
        [Range(0f,0.01f)]
        public float noiseScale;
        public Vector3 windDirection;
        public Texture3D noise;
        public Texture2D ditheringTex;


        //Height Fog Settings
        public bool fog = true;

        public enum FogQualityMode
        {
            Normal,
            Simple
        }

        public FogQualityMode fogQualityMode;
        public Vector3 floatingPointOriginMod;

        public float globalFogHeight = 0f;

        [Range(0.0f,1f)]
        public float fogDensity = 0.02f; // This is the global density factor, which can be thought of as the fog layer's thickness.

        [Range(0.001f, 0.1f)]
        public float fogHeightFalloff = 0.2f; // Height density factor, controls how the density increases as height decreases. Smaller values make the transition larger.
         public float fogHeight = 0.0f;
        [Range(0.0f, 1f)]
        public float fogDensity2 = 0.02f;
        [Range(0.001f, 0.1f)]
        public float fogHeightFalloff2 = 0.2f;
        public float fogHeight2;   
        [Range(0.0f,1.0f)]
        public float fogMaxOpacity = 1.0f; 
        [Range(0.01f,5000.0f)]
        public float startDistance = 0.0f; 
        [Range(0.0f,1.0f)]
        public float fogColorBlend = 0.5f;
        public Color fogColorMod = Color.white;
        public bool blockScattering = false;

        [GradientUsage(true)]
        public Gradient ambientColorGradient;

    #if ENVIRO_HDRP
        //HDRP Fog
        public bool controlHDRPFog;
        public float fogAttenuationDistance = 400f;
        public float baseHeight = 0f;
        public float maxHeight = 50f;
        [GradientUsage(true)]
        public Gradient fogColorTint;
        
        //HDRP Volumetrics
        public bool controlHDRPVolumetrics;
        [GradientUsage(true)]
        public Gradient volumetricsColorTint;
        [Range(0f,1f)]
        public float ambientDimmer = 1f;
        [Range(0f,10f)]
        public float directLightMultiplier = 1f;
        [Range(0f,1f)]
        public float directLightShadowdimmer = 1f;
    #endif

    //Unity Fog
    public bool unityFog = false;
    public FogMode unityFogMode = FogMode.Exponential;
    public float unityFogDensity = 0.002f;
    public float unityFogStartDistance = 0f;
    public float unityFogEndDistance = 1000f;
    [GradientUsage(true)]     
    public Gradient unityFogColor = new Gradient();
    } 

    [Serializable]
    public class EnviroFogModule : EnviroModule
    {  
        //Settings
        public EnviroFogSettings Settings;
        public EnviroFogModule preset;
        public bool showFogControls;
        public bool showVolumetricsControls;
    #if ENVIRO_HDRP
        public bool showHDRPFogControls;
    #endif
        public bool showUnityFogControls;

        //Fog Zones and Lights
        public List<EnviroVolumetricFogLight> fogLights = new List<EnviroVolumetricFogLight>();
        private Light myLight;
        public float customFogDensityModifer = 1f;
        //Materials
        public Material fogMat;
        public Material volumetricsMat;
        public Material blurMat;
        public Material blurMat2;

        // Textures
        public RenderTexture volumetricsRenderTexture;
        #if ENVIRO_URP && UNITY_6000_0_OR_NEWER
        public UnityEngine.Rendering.RenderGraphModule.TextureHandle volumetricsRenderTextureHandle;
        public UnityEngine.Rendering.RTHandle volumetricsRenderTextureRT;
        Material blitThroughMat;
        #endif
        //Point Lights
        struct PointLightParams
        {
            public Vector3 pos;
            public float range;
            public Vector3 color;
            float padding;
        }

        PointLightParams[] m_PointLightParams;
        ComputeBuffer m_PointLightParamsCB;

        //Spot Lights
        struct SpotLightParams
        {
            public Vector3 pos;
            public float range;
            public Vector3 color;
            public Vector3 lightDirection;
            public float lightCosHalfAngle;
            float padding;
        }
        SpotLightParams[] m_SpotLightParams;
        ComputeBuffer m_SpotLightParamsCB;


    #if ENVIRO_HDRP
        public UnityEngine.Rendering.HighDefinition.Fog fogHDRP;
    #endif

    private EnviroVolumetricFogLight directionaLight;
    private EnviroVolumetricFogLight additionalLight;
       

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///       Update Functions
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public override void Enable()
        {
            if(EnviroManager.instance == null)
               return;

        #if !ENVIRO_HDRP
            if(EnviroManager.instance.Objects.directionalLight != null)
            {
                if(EnviroManager.instance.Objects.directionalLight.gameObject.GetComponent<EnviroVolumetricFogLight>() == null)
                   directionaLight = EnviroManager.instance.Objects.directionalLight.gameObject.AddComponent<EnviroVolumetricFogLight>();
            }  

            if(EnviroManager.instance.Objects.additionalDirectionalLight != null)
            {
                if(EnviroManager.instance.Objects.additionalDirectionalLight.gameObject.GetComponent<EnviroVolumetricFogLight>() == null)
                   additionalLight = EnviroManager.instance.Objects.additionalDirectionalLight.gameObject.AddComponent<EnviroVolumetricFogLight>();
            } 
        #endif
        }

        public override void Disable()
        {
            if(EnviroManager.instance == null)
               return;

            CleanupHeightFog();
            CleanupVolumetrics();
            
            if(EnviroManager.instance.Objects.directionalLight != null)
            {
                if(EnviroManager.instance.Objects.directionalLight.gameObject.GetComponent<EnviroVolumetricFogLight>() != null)
                   DestroyImmediate(EnviroManager.instance.Objects.directionalLight.gameObject.GetComponent<EnviroVolumetricFogLight>());
            } 

            if(EnviroManager.instance.Objects.additionalDirectionalLight != null)
            {
                if(EnviroManager.instance.Objects.additionalDirectionalLight.gameObject.GetComponent<EnviroVolumetricFogLight>() != null)
                   DestroyImmediate(EnviroManager.instance.Objects.additionalDirectionalLight.gameObject.GetComponent<EnviroVolumetricFogLight>());
            }
        }
             
        // Update Method
        public override void UpdateModule ()
        { 
            if(!active)
               return; 

            if(EnviroManager.instance == null)
               return; 

        #if ENVIRO_HDRP
            UpdateFogHDRP ();
        #else
            UpdateUnityFog ();
        #endif
        
            if(additionalLight != null && directionaLight != null)
            {
                if(EnviroManager.instance.isNight)
                {
                    additionalLight.enabled = true;
                    directionaLight.enabled = false;
                }
                else
                {
                    directionaLight.enabled = true;
                    additionalLight.enabled = false;
                }
            }
        }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///       Public general functions
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public bool AddLight (EnviroVolumetricFogLight light)
        {
            fogLights.Add(light);
            return true;
        }
        public void RemoveLight (EnviroVolumetricFogLight light)
        {
         if(fogLights.Contains(light))
            fogLights.Remove(light);
        }
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///       HDRP
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#if ENVIRO_HDRP
        private void UpdateFogHDRP ()
        {
            if(EnviroManager.instance.volumeHDRP != null && EnviroManager.instance.volumeProfileHDRP != null) 
            { 
                if(fogHDRP == null)
                {
                    UnityEngine.Rendering.HighDefinition.Fog TempFog;

                    if (EnviroManager.instance.volumeProfileHDRP.TryGet<UnityEngine.Rendering.HighDefinition.Fog>(out TempFog))
                    {
                        fogHDRP = TempFog;
                    }
                    else 
                    {
                        EnviroManager.instance.volumeProfileHDRP.Add<UnityEngine.Rendering.HighDefinition.Fog>();

                        if (EnviroManager.instance.volumeProfileHDRP.TryGet<UnityEngine.Rendering.HighDefinition.Fog>(out TempFog))
                        {
                            fogHDRP = TempFog;
                        }
                    }
                }
                else
                { 
                    if(Settings.controlHDRPFog)
                    {
                        fogHDRP.active = true;
                        fogHDRP.enabled.overrideState = true;
                        fogHDRP.enabled.value = Settings.controlHDRPFog;
                        fogHDRP.meanFreePath.overrideState = true;
                        fogHDRP.meanFreePath.value = Settings.fogAttenuationDistance;
                        fogHDRP.baseHeight.overrideState = true;
                        fogHDRP.baseHeight.value = Settings.baseHeight;
                        fogHDRP.maximumHeight.overrideState = true;
                        fogHDRP.maximumHeight.value = Settings.maxHeight;
                        fogHDRP.tint.overrideState = true;
                        fogHDRP.tint.value = Settings.fogColorTint.Evaluate(EnviroManager.instance.solarTime); 
                    }
                    else
                    {
                        fogHDRP.active = false;
                    }
                    if(Settings.controlHDRPVolumetrics)
                    {
                        fogHDRP.enableVolumetricFog.overrideState = true;
                        fogHDRP.enableVolumetricFog.value = true;
                        fogHDRP.albedo.overrideState = true;
                        fogHDRP.albedo.value = Settings.volumetricsColorTint.Evaluate(EnviroManager.instance.solarTime); 
                        fogHDRP.globalLightProbeDimmer.overrideState = true;
                        fogHDRP.globalLightProbeDimmer.value = Settings.ambientDimmer;

                        if(EnviroManager.instance.Lighting != null && EnviroManager.instance.Lighting.directionalLightHDRP != null)
                        {
                            EnviroManager.instance.Lighting.directionalLightHDRP.volumetricDimmer = Settings.directLightMultiplier;
                            EnviroManager.instance.Lighting.directionalLightHDRP.volumetricShadowDimmer = Settings.directLightShadowdimmer;
                        }
                        if(EnviroManager.instance.Lighting != null && EnviroManager.instance.Lighting.additionalLightHDRP != null)
                        { 
                            EnviroManager.instance.Lighting.additionalLightHDRP.volumetricDimmer = Settings.directLightMultiplier;
                            EnviroManager.instance.Lighting.additionalLightHDRP.volumetricShadowDimmer = Settings.directLightShadowdimmer;
                        }
                    }
                    else
                    {
                        fogHDRP.enableVolumetricFog.value = false;
                    }
                }
            }
        }
#endif

    private void UpdateUnityFog ()
    {
        RenderSettings.fog = Settings.unityFog; 
        RenderSettings.fogMode = Settings.unityFogMode;

        if(Settings.unityFogMode == FogMode.Linear)
        {
            RenderSettings.fogStartDistance = Settings.unityFogStartDistance;
            RenderSettings.fogEndDistance = Settings.unityFogEndDistance;
        }
        else
        {
            RenderSettings.fogDensity = Settings.unityFogDensity;
        }

        RenderSettings.fogColor = Settings.unityFogColor.Evaluate(EnviroManager.instance.solarTime) * (Settings.fogColorMod * EnviroManager.instance.solarTime);
    }
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///       Height Fog
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
 
        public void UpdateFogShader (Camera cam) 
        {  
            if(Settings.fogQualityMode == EnviroFogSettings.FogQualityMode.Simple)
            {
                Shader.EnableKeyword("ENVIRO_SIMPLEFOG");
                Shader.SetGlobalVector("_EnviroFogParameters", new Vector4(0f, Settings.fogHeightFalloff, Settings.fogDensity * 0.01f * customFogDensityModifer, Settings.fogHeight + Settings.globalFogHeight));
            }
            else
            {
                Shader.DisableKeyword("ENVIRO_SIMPLEFOG");
                Shader.SetGlobalVector("_EnviroFogParameters", new Vector4(0f, Settings.fogHeightFalloff, Settings.fogDensity * 0.01f * customFogDensityModifer, Settings.fogHeight + Settings.globalFogHeight));
                Shader.SetGlobalVector("_EnviroFogParameters2", new Vector4(0f, Settings.fogHeightFalloff2, Settings.fogDensity2 * 0.01f * customFogDensityModifer, Settings.fogHeight2 + Settings.globalFogHeight));
            }

            Shader.SetGlobalVector("_EnviroFogParameters3",new Vector4(1.0f - Settings.fogMaxOpacity,Settings.startDistance,Settings.blockScattering ? 0f : 1f,Settings.fogColorBlend));
            Shader.SetGlobalColor("_EnviroFogColor", Settings.ambientColorGradient.Evaluate(EnviroManager.instance.solarTime) * (Settings.fogColorMod * EnviroManager.instance.solarTime));
            if (EnviroManager.instance.Objects.worldAnchor != null) 
                Settings.floatingPointOriginMod = EnviroManager.instance.Objects.worldAnchor.transform.position;
            else
                Settings.floatingPointOriginMod = Vector3.zero;
             
            Shader.SetGlobalVector("_EnviroCameraPos",cam.transform.position - Settings.floatingPointOriginMod);
            Shader.SetGlobalVector("_EnviroWorldOffset",Settings.floatingPointOriginMod);
        } 
 
        public void RenderHeightFog(Camera cam, RenderTexture source, RenderTexture destination)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Enviro Fog Rendering");

            if(fogMat == null)
               fogMat = new Material(Shader.Find("Hidden/EnviroHeightFog"));

            UpdateFogShader(cam);

            fogMat.SetTexture("_MainTex",source); 
            Graphics.Blit(source, destination, fogMat);
            UnityEngine.Profiling.Profiler.EndSample();
        }

#if ENVIRO_URP 
#if UNITY_6000_0_OR_NEWER
        public void RenderHeightFogURP(EnviroURPRenderGraph renderer, UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, UnityEngine.Rendering.Universal.UniversalResourceData resourceData, UnityEngine.Rendering.Universal.UniversalCameraData cameraData, UnityEngine.Rendering.RenderGraphModule.TextureHandle src, UnityEngine.Rendering.RenderGraphModule.TextureHandle target)
        { 
             if(fogMat == null)
                fogMat = new Material(Shader.Find("Hidden/EnviroHeightFogURP"));
 
                UpdateFogShader(cameraData.camera);  
                fogMat.EnableKeyword("ENVIROURP");

            if(volumetricsRenderTextureHandle.IsValid())
            {
                renderer.Blit("Fog", renderGraph, fogMat, src, target, 0, volumetricsRenderTextureHandle,"_EnviroVolumetricFogTex"); 
            }
            else
            {
                renderer.Blit("Fog", renderGraph, fogMat, src, target, 0);      
            }
        }
#endif 
#if !UNITY_6000_4_OR_NEWER
        public void RenderHeightFogURP(Camera cam,EnviroURPRenderPass pass,UnityEngine.Rendering.CommandBuffer cmd, RenderTexture source, UnityEngine.Rendering.RenderTargetIdentifier destination)
        {
            if(fogMat == null)
               fogMat = new Material(Shader.Find("Hidden/EnviroHeightFog"));
 
            UpdateFogShader(cam);
            fogMat.EnableKeyword("ENVIROURP");


            pass.CustomBlit(cmd,cam.cameraToWorldMatrix,source,destination,fogMat);
        }
#endif
#endif

#if ENVIRO_HDRP
        public void RenderHeightFogHDRP(Camera cam,UnityEngine.Rendering.CommandBuffer cmd, UnityEngine.Rendering.RTHandle source, UnityEngine.Rendering.RTHandle destination)
        {
            if(fogMat == null)
               fogMat = new Material(Shader.Find("Hidden/EnviroHeightFogHDRP"));
   
            UpdateFogShader(cam);
            fogMat.SetTexture("_MainTex",source);
            
            cmd.Blit(source, destination, fogMat);
        }
#endif

        private void CleanupHeightFog()
        {
            if(EnviroManager.instance == null)
               return;

            if(fogMat != null)
               DestroyImmediate(fogMat);

            if(EnviroManager.instance.removeZoneParamsCB != null)
            EnviroHelper.ReleaseComputeBuffer(ref EnviroManager.instance.removeZoneParamsCB);

            if(EnviroManager.instance.clearZoneCB != null)
            EnviroHelper.ReleaseComputeBuffer(ref EnviroManager.instance.clearZoneCB);   

            if(EnviroManager.instance.clearCBPoint != null)
            EnviroHelper.ReleaseComputeBuffer(ref EnviroManager.instance.clearCBPoint);   

            if(EnviroManager.instance.clearCBSpot != null)
            EnviroHelper.ReleaseComputeBuffer(ref EnviroManager.instance.clearCBSpot);   
        }


/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///       Volumetrics
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        
        public void RenderVolumetrics(Camera camera, RenderTexture source)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Enviro Volumetrics Rendering");

            if(Settings.volumetrics == false || camera.cameraType == CameraType.Reflection)
            {       
                Shader.DisableKeyword("ENVIRO_VOLUMELIGHT"); 
                return;
            }
            else
            {
                Shader.EnableKeyword("ENVIRO_VOLUMELIGHT"); 
            }

            if(volumetricsMat == null)
               volumetricsMat = new Material(Shader.Find("Hidden/Volumetrics"));

            if(blurMat == null)
               blurMat = new Material(Shader.Find("Hidden/EnviroBlur"));

            CreateVolumetricsBuffers();
            SetUpPointLightBuffers();
            SetUpSpotLightBuffers();

            UpdateVolumetricsShader(volumetricsMat);

            //if(cloudsTex != null)
            //   volumetricsMat.SetTexture("_CloudsTex", cloudsTex);

            RenderTextureDescriptor desc = source.descriptor;
            desc.msaaSamples = 1;


            if(volumetricsRenderTexture == null || volumetricsRenderTexture.width != desc.width || volumetricsRenderTexture.height != desc.height)
            {
                if(volumetricsRenderTexture != null)
                 DestroyImmediate(volumetricsRenderTexture);
 
                volumetricsRenderTexture = new RenderTexture(desc);
            }

        if(Settings.quality == EnviroFogSettings.Quality.High)
        {      
            RenderTexture target = RenderTexture.GetTemporary(desc);
            
            volumetricsMat.SetTexture("_MainTex",source);
            Graphics.Blit(source,target,volumetricsMat);

            RenderTexture temp = RenderTexture.GetTemporary(desc);

            // horizontal bilateral blur at full res
            blurMat.SetTexture("_MainTex",target);
            Graphics.Blit(target, temp, blurMat, 0);
            // vertical bilateral blur at full res
            blurMat.SetTexture("_MainTex",temp);
            Graphics.Blit(temp, target, blurMat, 1);

            Graphics.Blit(target, volumetricsRenderTexture);
            RenderTexture.ReleaseTemporary(temp);
            RenderTexture.ReleaseTemporary(target);
        }
        else if(Settings.quality == EnviroFogSettings.Quality.Medium)
        {
            desc.width = source.width / 2;
            desc.height = source.height / 2;
            RenderTexture target = RenderTexture.GetTemporary(desc);
            RenderTexture depth = RenderTexture.GetTemporary(desc);
            depth.filterMode = FilterMode.Point;

            volumetricsMat.SetTexture("_MainTex",source);
            Graphics.Blit(source,target,volumetricsMat);

            blurMat.SetTexture("_MainTex",source);
            Graphics.Blit(source, depth, blurMat, 4);

            blurMat.SetTexture("_HalfResDepthBuffer", depth);
            blurMat.SetTexture("_HalfResColor", target);

            RenderTexture temp = RenderTexture.GetTemporary(desc);
            // horizontal bilateral blur at half res
             blurMat.SetTexture("_MainTex",target);
            Graphics.Blit(target, temp, blurMat, 2);
                
            // vertical bilateral blur at half res
             blurMat.SetTexture("_MainTex",temp);
            Graphics.Blit(temp, target, blurMat, 3);
                
            // upscale to full res
             blurMat.SetTexture("_MainTex",target);
            Graphics.Blit(target, volumetricsRenderTexture, blurMat, 5);
            RenderTexture.ReleaseTemporary(temp);
            RenderTexture.ReleaseTemporary(target);
            RenderTexture.ReleaseTemporary(depth);
        }
        else if (Settings.quality == EnviroFogSettings.Quality.Low)
        {
            desc.width = source.width / 2;
            desc.height = source.height / 2;
            RenderTexture depthHalf = RenderTexture.GetTemporary(desc);
            depthHalf.filterMode = FilterMode.Point;
            desc.width = source.width / 4;
            desc.height = source.height / 4;
            RenderTexture target = RenderTexture.GetTemporary(desc);
            RenderTexture depthQuarter = RenderTexture.GetTemporary(desc);
            depthQuarter.filterMode = FilterMode.Point;
            
            volumetricsMat.SetTexture("_MainTex",source);
            Graphics.Blit(source,target,volumetricsMat);

            blurMat.SetTexture("_MainTex",source);
            Graphics.Blit(source, depthHalf, blurMat,4);
            Graphics.Blit(source, depthQuarter, blurMat,6);


            blurMat.SetTexture("_HalfResDepthBuffer", depthHalf);
            blurMat.SetTexture("_QuarterResDepthBuffer", depthQuarter);
            //blurMat.SetTexture("_HalfResColor", target);
            blurMat.SetTexture("_QuarterResColor", target);


            RenderTexture temp = RenderTexture.GetTemporary(desc);

            // horizontal bilateral blur at half res
             blurMat.SetTexture("_MainTex",target);
            Graphics.Blit(target, temp, blurMat, 8);     
            // vertical bilateral blur at half res
             blurMat.SetTexture("_MainTex",temp);
            Graphics.Blit(temp, target, blurMat, 9);
                
            // upscale to full res
             blurMat.SetTexture("_MainTex",target);
            Graphics.Blit(target, volumetricsRenderTexture, blurMat, 7);
            RenderTexture.ReleaseTemporary(temp);
            RenderTexture.ReleaseTemporary(target);
            RenderTexture.ReleaseTemporary(depthHalf);
            RenderTexture.ReleaseTemporary(depthQuarter);
        }

        Shader.SetGlobalTexture("_EnviroVolumetricFogTex", volumetricsRenderTexture);
        UnityEngine.Profiling.Profiler.EndSample();
        }

#if ENVIRO_URP 
#if !UNITY_6000_4_OR_NEWER
        public void RenderVolumetricsURP(Camera camera, EnviroURPRenderPass pass, UnityEngine.Rendering.CommandBuffer cmd, RenderTexture source)
        { 
            if(Settings.volumetrics == false || camera.cameraType == CameraType.Reflection)
            {       
                Shader.DisableKeyword("ENVIRO_VOLUMELIGHT"); 
                return;
            }
            else
            {
                Shader.EnableKeyword("ENVIRO_VOLUMELIGHT"); 
            }

            //Shader.EnableKeyword("ENVIRO_VOLUMELIGHT"); 

            if(volumetricsMat == null)
               volumetricsMat = new Material(Shader.Find("Hidden/VolumetricsURP"));

            if(blurMat == null)
               blurMat = new Material(Shader.Find("Hidden/EnviroBlur"));

            CreateVolumetricsBuffers(); 
            SetUpPointLightBuffers();  
            SetUpSpotLightBuffers();

            UpdateVolumetricsShader(volumetricsMat);

            volumetricsMat.EnableKeyword("ENVIROURP");
            blurMat.EnableKeyword("ENVIROURP");

            RenderTextureDescriptor desc = source.descriptor;
            desc.colorFormat = RenderTextureFormat.ARGBHalf;
            desc.msaaSamples = 1; 
            desc.depthBufferBits = 0;


            if(volumetricsRenderTexture == null || volumetricsRenderTexture.width != desc.width || volumetricsRenderTexture.height != desc.height)
            {
                if(volumetricsRenderTexture != null)
                 DestroyImmediate(volumetricsRenderTexture);
 
                volumetricsRenderTexture = new RenderTexture(desc);
            }

        if(Settings.quality == EnviroFogSettings.Quality.High)
        {      
            RenderTexture target = RenderTexture.GetTemporary(desc);
 
            pass.CustomBlit(cmd,camera.cameraToWorldMatrix,source,target,volumetricsMat); 

            RenderTexture temp = RenderTexture.GetTemporary(desc);

            // horizontal bilateral blur at full res
            pass.CustomBlit(cmd,camera.cameraToWorldMatrix,target,temp,blurMat,0);
            // vertical bilateral blur at full res
            pass.CustomBlit(cmd,camera.cameraToWorldMatrix,temp,target,blurMat,1);

            //Graphics.Blit(target, volumetricsRenderTexture);
            pass.CustomBlit(cmd,camera.cameraToWorldMatrix,target,volumetricsRenderTexture);
            RenderTexture.ReleaseTemporary(temp);
            RenderTexture.ReleaseTemporary(target);
        }
        else if(Settings.quality == EnviroFogSettings.Quality.Medium)
        {
            desc.width = source.width / 2;
            desc.height = source.height / 2;
            RenderTexture target = RenderTexture.GetTemporary(desc);
            RenderTexture depth = RenderTexture.GetTemporary(desc);
            depth.filterMode = FilterMode.Point;

            pass.CustomBlit(cmd,camera.cameraToWorldMatrix,source,target,volumetricsMat);
            pass.CustomBlit(cmd,camera.cameraToWorldMatrix,source,depth,blurMat,4);

            blurMat.SetTexture("_HalfResDepthBuffer", depth);
            blurMat.SetTexture("_HalfResColor", target);

            RenderTexture temp = RenderTexture.GetTemporary(desc);
            // horizontal bilateral blur at half res
            pass.CustomBlit(cmd,camera.cameraToWorldMatrix,target,temp,blurMat,2);
                
            // vertical bilateral blur at half res
            pass.CustomBlit(cmd,camera.cameraToWorldMatrix,temp,target,blurMat,3);
                
            // upscale to full res
            pass.CustomBlit(cmd,camera.cameraToWorldMatrix,target,volumetricsRenderTexture,blurMat,5);
            RenderTexture.ReleaseTemporary(temp);
            RenderTexture.ReleaseTemporary(target);
            RenderTexture.ReleaseTemporary(depth);
        }
        else if (Settings.quality == EnviroFogSettings.Quality.Low)
        {
            desc.width = source.width / 2;
            desc.height = source.height / 2;
            RenderTexture depthHalf = RenderTexture.GetTemporary(desc);
            depthHalf.filterMode = FilterMode.Point;
            desc.width = source.width / 4;
            desc.height = source.height / 4;
            RenderTexture target = RenderTexture.GetTemporary(desc);
            RenderTexture depthQuarter = RenderTexture.GetTemporary(desc);
            depthQuarter.filterMode = FilterMode.Point;
            
            pass.CustomBlit(cmd,camera.cameraToWorldMatrix,source,target,volumetricsMat);

            pass.CustomBlit(cmd,camera.cameraToWorldMatrix,source,depthHalf,blurMat,4);
            pass.CustomBlit(cmd,camera.cameraToWorldMatrix,source,depthQuarter,blurMat,6);

            blurMat.SetTexture("_HalfResDepthBuffer", depthHalf);
            blurMat.SetTexture("_QuarterResDepthBuffer", depthQuarter);
            //blurMat.SetTexture("_HalfResColor", target);
            blurMat.SetTexture("_QuarterResColor", target);
 

            RenderTexture temp = RenderTexture.GetTemporary(desc);

            // horizontal bilateral blur at half res
            pass.CustomBlit(cmd,camera.cameraToWorldMatrix,target,temp,blurMat,8);     
            // vertical bilateral blur at half res
            pass.CustomBlit(cmd,camera.cameraToWorldMatrix,temp,target,blurMat,9);
                
            // upscale to full res
            pass.CustomBlit(cmd,camera.cameraToWorldMatrix,target,volumetricsRenderTexture,blurMat,7);
            RenderTexture.ReleaseTemporary(temp);
            RenderTexture.ReleaseTemporary(target);
            RenderTexture.ReleaseTemporary(depthHalf);
            RenderTexture.ReleaseTemporary(depthQuarter);
        }

            Shader.SetGlobalTexture("_EnviroVolumetricFogTex", volumetricsRenderTexture);
        }
#endif
#if UNITY_6000_0_OR_NEWER
        //URP New Render Graph
        public void RenderVolumetricsURP(EnviroURPRenderGraph renderer, UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, UnityEngine.Rendering.Universal.UniversalResourceData resourceData, UnityEngine.Rendering.Universal.UniversalCameraData cameraData, UnityEngine.Rendering.RenderGraphModule.TextureHandle src)
        { 
            if(Settings.volumetrics == false || cameraData.cameraType == CameraType.Reflection)
            {       
                Shader.DisableKeyword("ENVIRO_VOLUMELIGHT"); 
                return;
            }
            else
            {
                Shader.EnableKeyword("ENVIRO_VOLUMELIGHT"); 
            }

            if(volumetricsMat == null)
               volumetricsMat = new Material(Shader.Find("Hidden/VolumetricsURP"));

            volumetricsMat.EnableKeyword("ENVIROURP17");

            if(blurMat == null)
               blurMat = new Material(Shader.Find("Hidden/EnviroBlurURP"));

            if(blurMat2 == null) 
               blurMat2 = new Material(Shader.Find("Hidden/EnviroBlurURP"));

            CreateVolumetricsBuffers(); 
            SetUpPointLightBuffers();  
            SetUpSpotLightBuffers();

            UpdateVolumetricsShader(volumetricsMat);

            volumetricsMat.EnableKeyword("ENVIROURP");
            blurMat.EnableKeyword("ENVIROURP17");
            blurMat2.EnableKeyword("ENVIROURP17");
            
            //if(cloudsTex != null)
            //   volumetricsMat.SetTexture("_CloudsTex", cloudsTex);

            UnityEngine.Rendering.RenderGraphModule.TextureDesc desc = src.GetDescriptor(renderGraph);
  
            desc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
            desc.msaaSamples = UnityEngine.Rendering.MSAASamples.None; 
            desc.depthBufferBits = 0;
            

            RenderTextureDescriptor renderTextureDescriptor = new RenderTextureDescriptor(desc.width,desc.height,RenderTextureFormat.ARGBHalf,0);
            renderTextureDescriptor.dimension = desc.dimension;
            renderTextureDescriptor.volumeDepth = desc.slices;
            
            UnityEngine.Rendering.Universal.RenderingUtils.ReAllocateHandleIfNeeded(ref volumetricsRenderTextureRT, renderTextureDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "Enviro Volumetrics Mask" );
            //volumetricsRenderTextureHandle = renderGraph.CreateTexture(desc); 
            volumetricsRenderTextureHandle =  renderGraph.ImportTexture(volumetricsRenderTextureRT);
           

        if(Settings.quality == EnviroFogSettings.Quality.High)
        {      
            UnityEngine.Rendering.RenderGraphModule.TextureHandle target = renderGraph.CreateTexture(desc);
            
            renderer.Blit("Render Volumetrics Mask", renderGraph,volumetricsMat,src,target,0);

            UnityEngine.Rendering.RenderGraphModule.TextureHandle temp = renderGraph.CreateTexture(desc);

            // We somehow need two materials otherwise it does not set the correct main tex. Must be rendergraph bug?!
            renderer.Blit("Horizontal Blur", renderGraph,blurMat,target,temp,0);
            renderer.Blit("Vertical Blur", renderGraph,blurMat2,temp,target,1);
  
            if(blitThroughMat == null)
               blitThroughMat = new Material(Shader.Find("Hidden/EnviroBlitThroughURP17"));

            renderer.Blit("Volumetrics Final Blit", renderGraph,blitThroughMat,target,volumetricsRenderTextureHandle,0);

        }
        else if(Settings.quality == EnviroFogSettings.Quality.Medium)
        {
            desc.width = src.GetDescriptor(renderGraph).width / 2;
            desc.height = src.GetDescriptor(renderGraph).height / 2;
            UnityEngine.Rendering.RenderGraphModule.TextureHandle target = renderGraph.CreateTexture(desc);
            UnityEngine.Rendering.RenderGraphModule.TextureDesc descPointFilter = desc;
            descPointFilter.filterMode = FilterMode.Point;
            descPointFilter.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;

            UnityEngine.Rendering.RenderGraphModule.TextureHandle depth = renderGraph.CreateTexture(descPointFilter);

            renderer.Blit("Render Volumetrics Mask", renderGraph,volumetricsMat,src,target,0);        
            
            renderer.Blit("depth blur",renderGraph,blurMat,src,depth,4);


            UnityEngine.Rendering.RenderGraphModule.TextureHandle temp = renderGraph.CreateTexture(desc);
            
            // horizontal bilateral blur at half res
            renderer.Blit("horizontal bilateral blur", renderGraph,blurMat,target,temp,2, depth, "_HalfResDepthBuffer");//, target , "_HalfResColor");
                 
            // vertical bilateral blur at half res
            renderer.Blit("vertical bilateral blur", renderGraph,blurMat2,temp,target,3, depth,"_HalfResDepthBuffer");//, target , "_HalfResColor");
                
            // upscale to full res
            renderer.Blit("upscale", renderGraph,blurMat,target,volumetricsRenderTextureHandle,5, depth,"_HalfResDepthBuffer", target, "_HalfResColor");//, target , "_HalfResColor");
   
        }
        else if (Settings.quality == EnviroFogSettings.Quality.Low)
        {
            desc.width = src.GetDescriptor(renderGraph).width / 2;
            desc.height = src.GetDescriptor(renderGraph).height / 2;
            
            UnityEngine.Rendering.RenderGraphModule.TextureDesc depthHalfdesc = desc;
            depthHalfdesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
            depthHalfdesc.filterMode = FilterMode.Point;

            UnityEngine.Rendering.RenderGraphModule.TextureHandle depthHalf = renderGraph.CreateTexture(depthHalfdesc);

            desc.width = src.GetDescriptor(renderGraph).width / 4;
            desc.height = src.GetDescriptor(renderGraph).height / 4;
            UnityEngine.Rendering.RenderGraphModule.TextureHandle target = renderGraph.CreateTexture(desc);

            UnityEngine.Rendering.RenderGraphModule.TextureDesc depthQuarterdesc = desc;
            depthQuarterdesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
            depthQuarterdesc.filterMode = FilterMode.Point;

            UnityEngine.Rendering.RenderGraphModule.TextureHandle depthQuarter = renderGraph.CreateTexture(depthQuarterdesc);
            
            renderer.Blit("Render Volumetrics Mask", renderGraph,volumetricsMat,src,target,0);

            renderer.Blit("depth blur", renderGraph,blurMat,src,depthHalf,4);
            renderer.Blit("depth blur", renderGraph,blurMat,src,depthQuarter,6, depthHalf, "_HalfResDepthBuffer");
 
 
            UnityEngine.Rendering.RenderGraphModule.TextureHandle temp = renderGraph.CreateTexture(desc);

            // horizontal bilateral blur at half res 
            renderer.Blit("horizontal bilateral blur", renderGraph,blurMat,target,temp,8,depthQuarter, "_QuarterResDepthBuffer", target , "_QuarterResColor");     
            // vertical bilateral blur at half res
            renderer.Blit("horizontal bilateral blur", renderGraph,blurMat2,temp,target,9,depthQuarter, "_QuarterResDepthBuffer");         
            // upscale to full res 
            renderer.Blit("upscale", renderGraph,blurMat,target,volumetricsRenderTextureHandle,7,depthQuarter,"_QuarterResDepthBuffer", target , "_QuarterResColor");
    
        }
            if(Settings.volumetrics == true || cameraData.cameraType != CameraType.Reflection && volumetricsRenderTextureRT != null) 
            {   
               Shader.SetGlobalTexture("_EnviroVolumetricFogTex", volumetricsRenderTextureRT);
            }
        }
#endif
#endif

        private void UpdateVolumetricsShader(Material mat)
        {    
            if(EnviroManager.instance.Lighting != null)
            {
               myLight = EnviroHelper.GetDirectionalLight();
            }
            else
            {
                if(myLight == null)
                   myLight = EnviroHelper.GetDirectionalLight();
            }

            mat.SetInt("_Steps", Settings.steps);

            if(myLight == null)
            { 
                mat.SetVector("_DirLightDir", new Vector4(0f, 0f, 0f, 1.0f / 2.0f));
                Shader.SetGlobalColor("_EnviroDirLightColor", Color.white * 1.0f);
            }
            else
            {
                mat.SetVector("_DirLightDir", new Vector4(myLight.transform.forward.x, myLight.transform.forward.y, myLight.transform.forward.z, 1.0f / (myLight.range * myLight.range)));
                Shader.SetGlobalColor("_EnviroDirLightColor", myLight.color * myLight.intensity);
            }
            mat.SetFloat("_MaxRayLength", Settings.maxRange);
            mat.SetFloat("_MaxRayLengthLights", Settings.maxRangePointSpot);
 
            mat.SetVector("_WindDirection", new Vector4(Settings.windDirection.x, Settings.windDirection.y,Settings.windDirection.z));
            mat.SetVector("_NoiseData", new Vector4(Settings.noiseScale, Settings.noiseIntensity));
            mat.SetVector("_MieG", new Vector4(Settings.anistropy, 1 + (Settings.anistropy * Settings.anistropy), 2 * Settings.anistropy, 1.0f / (4.0f * Mathf.PI)));
            mat.SetVector("_VolumetricLight", new Vector4(Settings.scattering * Settings.scatteringMultiplier.Evaluate(EnviroManager.instance.solarTime), Settings.extinction, 1f, 0f));// - SkyboxExtinctionCoef));
 
            mat.SetTexture("_NoiseTexture",Settings.noise); 
            mat.SetTexture("_DitherTexture",Settings.ditheringTex);

            mat.SetVector("_Randomness", new Vector4(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value)); 
        }

        private void CreateVolumetricsBuffers()
        {
            int pointLightCount = 0, spotLightCount = 0;

            for(int i = 0; i < fogLights.Count; i++)
            {
                Enviro.EnviroVolumetricFogLight fogLight = fogLights[i];

                if (fogLight == null)
                    continue;

                bool isOn = fogLight.isOn;

                switch(fogLight.light.type)
                {
                    case LightType.Point: 	if (isOn) pointLightCount++; break;
                    case LightType.Spot: 	if (isOn) spotLightCount++; break;
                }
            } 

            EnviroHelper.CreateBuffer(ref m_PointLightParamsCB, pointLightCount, Marshal.SizeOf(typeof(PointLightParams)));
            EnviroHelper.CreateBuffer(ref m_SpotLightParamsCB, spotLightCount, Marshal.SizeOf(typeof(SpotLightParams)));
            EnviroHelper.CreateBuffer(ref EnviroManager.instance.clearCBPoint, 1, Marshal.SizeOf(typeof(PointLightParams)));
            EnviroHelper.CreateBuffer(ref EnviroManager.instance.clearCBSpot, 1, Marshal.SizeOf(typeof(SpotLightParams)));
        }  

        private void CleanupVolumetrics()
        {
            if(volumetricsMat != null)
               DestroyImmediate(volumetricsMat);

            if(blurMat != null)
                DestroyImmediate(blurMat);

            if(volumetricsRenderTexture != null)
                DestroyImmediate(volumetricsRenderTexture);

            EnviroHelper.ReleaseComputeBuffer(ref m_PointLightParamsCB);
            EnviroHelper.ReleaseComputeBuffer(ref m_SpotLightParamsCB);
            EnviroHelper.ReleaseComputeBuffer(ref EnviroManager.instance.clearCBSpot);  
            EnviroHelper.ReleaseComputeBuffer(ref EnviroManager.instance.clearCBPoint);  
        }

        void SetUpPointLightBuffers()
        {
            int count = m_PointLightParamsCB == null ? 0 : m_PointLightParamsCB.count;
            volumetricsMat.SetFloat("_PointLightsCount", count);

            if (count == 0)
            {
                // Can't not set the buffer
                volumetricsMat.SetBuffer("_PointLights", EnviroManager.instance.clearCBPoint);
                return;
            }
 
            if (m_PointLightParams == null || m_PointLightParams.Length != count)
                m_PointLightParams = new PointLightParams[count];

            int lightID = 0;
   
            for (int i = 0; i < fogLights.Count; i++)
            {
                Enviro.EnviroVolumetricFogLight fl = fogLights[i];

                if (fl == null || fl.light.type != LightType.Point || !fl.isOn)
                    continue;

                Light light = fl.light;
                m_PointLightParams[lightID].pos = light.transform.position;
                float range = light.range * fl.range;
                m_PointLightParams[lightID].range = 1.0f / (range * range);
                m_PointLightParams[lightID].color = new Vector3(light.color.r, light.color.g, light.color.b) * light.intensity * fl.intensity;
                lightID++;
            } 

            // TODO: try a constant buffer with setfloats instead for perf
            m_PointLightParamsCB.SetData(m_PointLightParams);
            volumetricsMat.SetBuffer("_PointLights", m_PointLightParamsCB);
        }

        void SetUpSpotLightBuffers()
        {
            int count = m_SpotLightParamsCB == null ? 0 : m_SpotLightParamsCB.count;
        
            volumetricsMat.SetFloat("_SpotLightsCount", count);

            if (count == 0)
            {
                // Can't not set the buffer
                volumetricsMat.SetBuffer("_SpotLights", EnviroManager.instance.clearCBSpot);
                return; 
            }

            if (m_SpotLightParams == null || m_SpotLightParams.Length != count)
                m_SpotLightParams = new SpotLightParams[count];

            int lightID = 0;

            for (int i = 0; i < fogLights.Count; i++)
                {
                Enviro.EnviroVolumetricFogLight fl = fogLights[i];

                if (fl == null || fl.light.type != LightType.Spot || !fl.isOn)
                    continue;

                Light light = fl.light;
                m_SpotLightParams[lightID].pos = light.transform.position;
                float range = light.range * fl.range;
                m_SpotLightParams[lightID].range = 1.0f / (range * range);
                m_SpotLightParams[lightID].color = new Vector3(light.color.r, light.color.g, light.color.b) * light.intensity * fl.intensity;

                m_SpotLightParams[lightID].lightDirection = light.transform.forward;
                m_SpotLightParams[lightID].lightCosHalfAngle = Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
                lightID++;
                }
            m_SpotLightParamsCB.SetData(m_SpotLightParams);
            volumetricsMat.SetBuffer("_SpotLights", m_SpotLightParamsCB);
        }


/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///       Save and Load
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
 
        public void LoadModuleValues ()
        {
            if(preset != null)
            {
                Settings = JsonUtility.FromJson<Enviro.EnviroFogSettings>(JsonUtility.ToJson(preset.Settings));
            }
            else
            { 
                Debug.Log("Please assign a saved module to load from!");
            }
        }
 
        public void SaveModuleValues ()
        {
#if UNITY_EDITOR
        EnviroFogModule t =  ScriptableObject.CreateInstance<EnviroFogModule>();
        t.name = "Fog Module";
        t.Settings = JsonUtility.FromJson<Enviro.EnviroFogSettings>(JsonUtility.ToJson(Settings));

        string assetPathAndName = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(EnviroHelper.assetPath + "/New " + t.name + ".asset");
        UnityEditor.AssetDatabase.CreateAsset(t, assetPathAndName);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
#endif 
        }

        public void SaveModuleValues (EnviroFogModule module)
        {
            module.Settings = JsonUtility.FromJson<Enviro.EnviroFogSettings>(JsonUtility.ToJson(Settings));
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(module);
            UnityEditor.AssetDatabase.SaveAssets();
            #endif
        }
    }
}