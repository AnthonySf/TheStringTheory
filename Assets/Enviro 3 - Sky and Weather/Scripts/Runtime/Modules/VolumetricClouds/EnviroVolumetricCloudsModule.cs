using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Enviro 
{   
  
    [Serializable] 
    public class EnviroVolumetricCloudsQuality 
    {
		public bool volumetricClouds = true;
        public bool lightningSupport = true;
        public bool variableBottomNoise = false;

        [Range(1,6)]
        public int downsampling = 4;
        [Range(32,256)]
		public int stepsLayer1 = 128;
        [Range(32,256)]
		public int stepsLayer2 = 64;
        [Range(0f,2f)]
        public float blueNoiseIntensity = 1f;
        [Range(0f,10f)]
		public float reprojectionBlendTime = 10f;
        [Range(0f,1f)]
		public float lodDistance = 0.25f;

    }

    [Serializable] 
    public class EnviroCloudGlobalSettings 
    {
        public Vector3 floatingPointOriginMod;
        public Vector3 cloudScrollOffset;
        public Gradient sunLightColorGradient;
        public Gradient moonLightColorGradient;
        public Gradient ambientColorGradient;

        public Color sunLightColor;
        public Color moonLightColor;
        public Color ambientColor;

        public bool depthBlending = false;
        public bool depthTest = true;

        public Texture3D noise; 
        public Texture3D detailNoise;
        public Texture2D curlTex;
        public Texture2D bottomsOffsetNoise;
        public Texture2D blueNoise;

        public Texture customWeatherMap;
 
        public float cloudsWorldScale = 5000000f;
        public float maxRenderDistance = 75000f;
        public float atmosphereColorSaturateDistance = 15000f;

        [Range(0.0f, 2.0f)]
        public float ambientLighIntensity = 1f;

        public bool cloudShadows = true;
        [Range(0.0f, 2.0f)]
        public float cloudShadowsIntensity = 1f;

        [Range(0.0f, 1f)]
        public float cloudsTravelSpeed = 0.5f;
    }


    [Serializable] 
    public class EnviroCloudLayerSettings 
    { 
        [Range(-1f,1f)]
        public float cloudsWindDirectionXModifier = 1f;
        [Range(-1f,1f)]
        public float cloudsWindDirectionYModifier = 1f;
        [Range(-0.1f,0.1f)] 
        public float windSpeedModifier = 0.1f;
        [Range(0f,0.1f)]
        public float windUpwards = 0.1f;

        [Range(-1f,1f)]
        public float coverage = 1f;
        public float worleyFreq2 = 12f;
        public float worleyFreq1 = 48f;
        [Range(0f,1f)]
        public float dilateCoverage = 0.5f;
        [Range(0f,1f)]
        public float dilateType = 0.5f;
        [Range(0f,1f)]
        public float cloudsTypeModifier = 0.5f;
        public Vector2 locationOffset;

        public float bottomCloudsHeight = 2000f;
        public float topCloudsHeight = 8000f;
 
        [Range(0f,2f)]
        public float density = 1.0f;
        [Range(0f,2f)]
        public float densitySmoothness = 1.0f;
        [Range(0f, 2f)]
        public float scatteringIntensity = 3f;
        [Range(0f, 1f)] 
        public float edgeHighlightStrength = 0.5f;
        [Range(0f, 1f)] 
        public float silverLiningSpread = 0.15f;
        [Range(0f,2f)]
        public float silverLiningIntensity = 1.0f;
        [Range(0f,2f)]
        public float lightningIntensity = 0.5f;
        [Range(0f,1f)]
        public float curlIntensity = 1.0f;
        [Range(0f,0.25f)] 
        public float lightStepModifier = 0.1f;



        [Range(0f,1f)]
        public float multiScatterStrength = 0.5f;
        [Range(0f,1f)]
        public float multiScatterFalloff = 0.02f;
        [Range(0f,0.5f)]
        public float ambientFloor = 0.02f;
        [Range(0f,0.5f)]
        public float absorbtion = 0.25f;  
        [Range(0f,2f)]
        public float exposure = 1.0f;

        public float baseNoiseUV = 32f;
        public float detailNoiseUV = 32f;

        [Range(0.0f, 2.0f)]
        public float baseNoiseUVMultiplier = 1f;
        [Range(0.0f, 2.0f)]
        public float detailNoiseUVMultiplier= 1f;

        [Range(0.0f, 1.0f)]
        public float baseErosionIntensity = 0f;

        [Range(0.0f, 1.0f)]
        public float baseNoiseMultiplier = 1f;
        [Range(0.0f, 1.0f)]
        public float detailErosionIntensity = 0.2f;
        [Range(0.0f, 1.0f)]
        public float detailNoiseMultiplier = 1f;
        
        [Range(-1.0f, 1.0f)]
        public float bottomShape = 0.0f; 
        public float midShape = 0.0f;    
        [Range(-1.0f, 1.0f)]     
        public float topShape = 0.0f;  
        [Range(-1.0f, 1.0f)]
        public float topLayer =  0.0f; 
        [Range(0.0f, 1.0f)]
        public float cloudTypeShaping = 1.0f;
        
        [Range(0.0f, 2.0f)]
        public float rampShape = 0.5f;
    }

    public class EnviroVolumetricCloudRenderer
    { 
        // Clouds Rendering
        public Camera camera;
        public Material raymarchMat;
        public Material reprojectMat;
        public Material depthMat;
        public Material blendAndLightingMat; 
        public Material shadowMat;
        public RenderTexture[] fullBuffer;
        public int fullBufferIndex; 
        public RenderTexture undersampleBuffer;
        public RenderTexture downsampledDepth;
        public Matrix4x4 prevV;
        public Matrix4x4 prevVRight;
        public float lastSkyPitch = float.PositiveInfinity;
        public int frame = 0;
        public bool firstFrame = true;

#if ENVIRO_HDRP
    public UnityEngine.Rendering.RTHandle[] fullBufferHandles;
    public UnityEngine.Rendering.RTHandle undersampleBufferHandle;
    public UnityEngine.Rendering.RTHandle downsampledDepthHandle;
#endif
#if ENVIRO_URP
#if UNITY_6000_0_OR_NEWER
    public UnityEngine.Rendering.RenderGraphModule.TextureHandle[] fullBufferHandles;
    public UnityEngine.Rendering.RenderGraphModule.TextureHandle undersampleBufferHandle;
    public UnityEngine.Rendering.RenderGraphModule.TextureHandle downsampledDepthHandle;

    public UnityEngine.Rendering.RTHandle[] fullBufferRTHandles;
    public UnityEngine.Rendering.RTHandle undersampleRTBufferHandle;
    public UnityEngine.Rendering.RTHandle downsampledRTDepthHandle;
#endif
#endif
    }

    [Serializable]
    [ExecuteInEditMode]
    public class EnviroVolumetricCloudsModule : EnviroModule
    {   
        private static readonly int EnviroStageSkyPitchId = Shader.PropertyToID("_EnviroStageSkyPitch");

        //Base
        public EnviroCloudLayerSettings settingsVolume;
        public EnviroCloudGlobalSettings settingsGlobal;
        public EnviroVolumetricCloudsQuality settingsQuality;
        public EnviroVolumetricCloudsModule preset;


        //Inspector
        public bool showGlobalControls;
        public bool showVolumeSettings; 
        public bool showCoverageControls;
        public bool showLightingControls;
        public bool showDensityControls;
        public bool showTextureControls;
        public bool showWindControls;

        //Wind
        public Vector3 cloudAnimLayer1;
        public Vector3 cloudAnimLayer2;
        public Vector3 cloudAnimNonScaledLayer1;
        public Vector3 cloudAnimNonScaledLayer2;

        //Weather Map
        public RenderTexture weatherMap;
        private Material weatherMapMat;
        private ComputeShader weatherMapCS;

        private Light dirLight;


        private Vector3 lastOffset = Vector3.zero;

        private Texture2DArray blackArray;

        //Update Method
        public override void UpdateModule ()
        { 
            if(!active)
               return; 
               
            if (EnviroManager.instance == null)
                return;

            if(!settingsQuality.volumetricClouds)
               return;
            
            UpdateWind();

            UnityEngine.Profiling.Profiler.BeginSample("Enviro Clouds Weather Map");
            weatherMap = EnviroManager.instance.VolumetricClouds.RenderWeatherMap();
            UnityEngine.Profiling.Profiler.EndSample(); 
        }
 
        private void CreateBlackArray()
        {
            Color[] colors = new Color[16];
 
            for(int i = 0; i < colors.Length; i++)
            {
                colors[i] = new Color(0,0,0,0);
            }
            blackArray = new Texture2DArray(4,4,2,DefaultFormat.LDR,TextureCreationFlags.None);
            blackArray.SetPixels(colors,0); 
            blackArray.SetPixels(colors,1);
            blackArray.Apply();
        }


        private bool IsURPCompabilityMode ()
        {
            #if ENVIRO_URP && UNITY_6000_0_OR_NEWER
                
            #if UNITY_6000_4_OR_NEWER
                return false;
            #else
                return GraphicsSettings.GetRenderPipelineSettings< UnityEngine.Rendering.Universal.RenderGraphSettings>().enableRenderCompatibilityMode;
            #endif 

            #else
                return true;
            #endif
        }
        public override void Enable()
        {

            CreateBlackArray();

        }

        private static float GetEnviroStageSkyPitch()
        {
            return Mathf.Clamp(Shader.GetGlobalFloat(EnviroStageSkyPitchId), -18f, 18f);
        }

        private static Matrix4x4 ApplyEnviroStageSkyPitch(Matrix4x4 cameraToWorld)
        {
            float pitch = GetEnviroStageSkyPitch();
            if (Mathf.Abs(pitch) < 0.001f)
                return cameraToWorld;

            return cameraToWorld * Matrix4x4.Rotate(Quaternion.Euler(-pitch, 0f, 0f));
        }

        private static Matrix4x4 BuildEnviroStageSkyPitchMatrix()
        {
            float pitch = GetEnviroStageSkyPitch();
            if (Mathf.Abs(pitch) < 0.001f)
                return Matrix4x4.identity;

            return Matrix4x4.Rotate(Quaternion.Euler(-pitch, 0f, 0f));
        }

        private static Matrix4x4 BuildEnviroStagePreviousViewProjection(Matrix4x4 projection, Matrix4x4 cameraToWorld)
        {
            return projection * ApplyEnviroStageSkyPitch(cameraToWorld).inverse;
        }

        private static void UpdateTemporalCloudHistoryForSkyPitch(EnviroVolumetricCloudRenderer renderer)
        {
            if (renderer == null)
                return;

            float pitch = GetEnviroStageSkyPitch();
            if (Mathf.Abs(renderer.lastSkyPitch - pitch) <= 0.001f)
                return;

            renderer.firstFrame = true;
            renderer.lastSkyPitch = pitch;
        }

        public override void Disable() 
        {
            if(weatherMapMat != null)
               DestroyImmediate(weatherMapMat);

            if(weatherMap != null)
               DestroyImmediate(weatherMap);
        }

        public void RenderCloudsShadows(RenderTexture source, RenderTexture destination, EnviroVolumetricCloudRenderer renderer)
        {
             if(renderer.shadowMat == null)
                renderer.shadowMat = new Material(Shader.Find("Hidden/EnviroApplyShadows"));

                if(renderer.undersampleBuffer == null)
                   return;

                renderer.shadowMat.SetTexture("_CloudsTex",renderer.undersampleBuffer);
                renderer.shadowMat.SetFloat("_Intensity",EnviroManager.instance.VolumetricClouds.settingsGlobal.cloudShadowsIntensity);

                renderer.shadowMat.SetTexture("_MainTex",source);
                Graphics.Blit(source,destination,renderer.shadowMat);
        }

#if ENVIRO_URP 
#if !UNITY_6000_4_OR_NEWER
        public void RenderCloudsShadowsURP(EnviroURPRenderPass pass,Camera cam, UnityEngine.Rendering.CommandBuffer cmd, RenderTexture source, UnityEngine.Rendering.RenderTargetIdentifier destination, EnviroVolumetricCloudRenderer renderer)
        { 
             if(renderer.shadowMat == null)
                renderer.shadowMat = new Material(Shader.Find("Hidden/EnviroApplyShadows"));

                if(renderer.undersampleBuffer == null)
                   return;

                renderer.shadowMat.SetTexture("_CloudsTex",renderer.undersampleBuffer);
                renderer.shadowMat.SetFloat("_Intensity",EnviroManager.instance.VolumetricClouds.settingsGlobal.cloudShadowsIntensity);

                renderer.shadowMat.EnableKeyword("ENVIROURP");
                pass.CustomBlit(cmd,cam.cameraToWorldMatrix,source,destination,renderer.shadowMat);
        } 
#endif
#endif

#if ENVIRO_HDRP
        public void RenderCloudsShadowsHDRP(Camera cam, UnityEngine.Rendering.CommandBuffer cmd, UnityEngine.Rendering.RTHandle source, UnityEngine.Rendering.RTHandle destination, EnviroVolumetricCloudRenderer renderer)
        {
             if(renderer.shadowMat == null)
                renderer.shadowMat = new Material(Shader.Find("Hidden/EnviroApplyShadowsHDRP"));

                if(renderer.undersampleBufferHandle == null)
                   return;

                renderer.shadowMat.SetTexture("_MainTex",source);
                renderer.shadowMat.SetTexture("_CloudsTex",renderer.undersampleBufferHandle);
                renderer.shadowMat.SetVector("_HandleScales", new Vector4(1/renderer.undersampleBufferHandle.rtHandleProperties.rtHandleScale.x,1/renderer.undersampleBufferHandle.rtHandleProperties.rtHandleScale.y,1,1)); 
                renderer.shadowMat.SetFloat("_Intensity",EnviroManager.instance.VolumetricClouds.settingsGlobal.cloudShadowsIntensity);
                cmd.Blit(source,destination,renderer.shadowMat);
        }
#endif
        /// Render Clouds
        public void RenderVolumetricClouds(Camera cam, RenderTexture source, RenderTexture destination, EnviroVolumetricCloudRenderer renderer, EnviroQuality quality)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Enviro Clouds Rendering");

            int downsampling = settingsQuality.downsampling;

            if(quality != null)
               downsampling = quality.volumetricCloudsOverride.downsampling;

            int width = cam.pixelWidth / downsampling;
            int height = cam.pixelHeight / downsampling;

            if(cam.cameraType != CameraType.Reflection)
            {
                if (renderer.fullBuffer == null || renderer.fullBuffer.Length != 2)
                {
                    renderer.fullBuffer = new RenderTexture[2];
                }
                renderer.fullBufferIndex = (renderer.fullBufferIndex + 1) % 2;

                renderer.firstFrame |= CreateRenderTexture(ref renderer.fullBuffer[0], width, height, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear,source.descriptor);
                renderer.firstFrame |= CreateRenderTexture(ref renderer.fullBuffer[1], width, height, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear,source.descriptor);
            }
            renderer.firstFrame |= CreateRenderTexture(ref renderer.undersampleBuffer, width, height, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear,source.descriptor);

            renderer.frame++;
         
            if(renderer.frame > 64)
              renderer.frame = 0;   
            UpdateTemporalCloudHistoryForSkyPitch(renderer);
 
            if(renderer.depthMat == null)
               renderer.depthMat = new Material(Shader.Find("Hidden/EnviroVolumetricCloudsDepth"));
 
            CreateRenderTexture(ref renderer.downsampledDepth, width, height, RenderTextureFormat.RFloat, FilterMode.Point, source.descriptor);
          
            renderer.depthMat.SetTexture("_MainTex", source);
            renderer.depthMat.SetVector("_CameraDepthTexture_TexelSize", new Vector4(1 / source.width, 1 / source.height, source.width, source.height));


            if (downsampling > 1)  
            {
                Graphics.Blit(source, renderer.downsampledDepth, renderer.depthMat, 0);  //Downsample the texture.
            } 
            else 
            {
                Graphics.Blit(source, renderer.downsampledDepth, renderer.depthMat, 1);  //Just copy it.
            }        
  
            //1. Raymarch
            SetRaymarchShader(cam,renderer, quality);
            renderer.raymarchMat.SetTexture("_MainTex", source);
            Graphics.Blit(source,renderer.undersampleBuffer,renderer.raymarchMat);


            //Pass 2: Reprojection
            if(cam.cameraType != CameraType.Reflection)
            {
                if(renderer.reprojectMat == null)
                   renderer.reprojectMat = new Material(Shader.Find("Hidden/EnviroVolumetricCloudsReproject"));
                SetReprojectShader(cam, renderer, quality);
    
                if (renderer.firstFrame) 
                {
                    Graphics.Blit(renderer.undersampleBuffer, renderer.fullBuffer[renderer.fullBufferIndex]);
                }

                renderer.reprojectMat.SetTexture("_MainTex", renderer.fullBuffer[renderer.fullBufferIndex]);
                Graphics.Blit(renderer.fullBuffer[renderer.fullBufferIndex], renderer.fullBuffer[renderer.fullBufferIndex ^ 1], renderer.reprojectMat);
            } 
            //Pass 3: Lighting and Blending
            if(renderer.blendAndLightingMat == null)
               renderer.blendAndLightingMat = new Material(Shader.Find("Hidden/EnviroVolumetricCloudsBlend"));
            SetBlendShader(cam,renderer);
            renderer.blendAndLightingMat.SetTexture("_MainTex", source);
            Graphics.Blit(source, destination, renderer.blendAndLightingMat);

        #if ENABLE_VR || ENABLE_XR_MODULE
            if(UnityEngine.XR.XRSettings.enabled && UnityEngine.XR.XRSettings.stereoRenderingMode == UnityEngine.XR.XRSettings.StereoRenderingMode.SinglePassInstanced && cam.stereoEnabled)
            {
                renderer.prevV = BuildEnviroStagePreviousViewProjection(cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left), cam.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse);
                renderer.prevVRight = BuildEnviroStagePreviousViewProjection(cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right), cam.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse);
            }
            else
            {
                renderer.prevV = BuildEnviroStagePreviousViewProjection(cam.projectionMatrix, cam.cameraToWorldMatrix);
            }
        #else
                renderer.prevV = BuildEnviroStagePreviousViewProjection(cam.projectionMatrix, cam.cameraToWorldMatrix);
        #endif

            renderer.firstFrame = false;

            Shader.SetGlobalTexture("_EnviroCloudsTex", renderer.undersampleBuffer);
            UnityEngine.Profiling.Profiler.EndSample();
        }

 #if ENVIRO_URP 
 #if UNITY_6000_0_OR_NEWER
        public void RenderVolumetricCloudsURP(EnviroURPRenderGraph pass,UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, UnityEngine.Rendering.Universal.UniversalResourceData resourceData, UnityEngine.Rendering.Universal.UniversalCameraData cameraData, UnityEngine.Rendering.RenderGraphModule.TextureHandle src, UnityEngine.Rendering.RenderGraphModule.TextureHandle target, EnviroVolumetricCloudRenderer renderer, EnviroQuality quality)
        {
            
            //UnityEngine.Profiling.Profiler.BeginSample("Enviro Clouds Rendering");

            int downsampling = settingsQuality.downsampling;

            if(quality != null)
               downsampling = quality.volumetricCloudsOverride.downsampling;

            int width = cameraData.camera.pixelWidth / downsampling;
            int height = cameraData.camera.pixelHeight / downsampling; 

            UnityEngine.Rendering.RenderGraphModule.TextureDesc t = src.GetDescriptor(renderGraph);
            RenderTextureDescriptor dsc = new RenderTextureDescriptor(t.width,t.height,RenderTextureFormat.ARGBHalf,0);
            dsc.vrUsage = t.vrUsage;
            dsc.width = width;
            dsc.height = height;
            dsc.dimension = t.dimension;
            dsc.volumeDepth = t.slices;
            
            if(cameraData.camera.cameraType != CameraType.Reflection)
            {
                if (renderer.fullBufferHandles == null || renderer.fullBufferHandles.Length != 2)
                {
                    renderer.fullBufferHandles = new UnityEngine.Rendering.RenderGraphModule.TextureHandle[2];
                }

                if (renderer.fullBufferRTHandles == null || renderer.fullBufferRTHandles.Length != 2)
                {
                    renderer.fullBufferRTHandles = new UnityEngine.Rendering.RTHandle[2];
                }

                renderer.fullBufferIndex = (renderer.fullBufferIndex + 1) % 2;
        

            renderer.firstFrame |= UnityEngine.Rendering.Universal.RenderingUtils.ReAllocateHandleIfNeeded(ref renderer.fullBufferRTHandles[0], dsc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "Enviro Clouds History Buffer 0" );
            renderer.firstFrame |= UnityEngine.Rendering.Universal.RenderingUtils.ReAllocateHandleIfNeeded(ref renderer.fullBufferRTHandles[1], dsc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "Enviro Clouds History Buffer 1" );
            
            renderer.fullBufferHandles[0] = renderGraph.ImportTexture(renderer.fullBufferRTHandles[0]);
            renderer.fullBufferHandles[1] = renderGraph.ImportTexture(renderer.fullBufferRTHandles[1]);

            }    
            
            renderer.firstFrame |= UnityEngine.Rendering.Universal.RenderingUtils.ReAllocateHandleIfNeeded(ref renderer.undersampleRTBufferHandle, dsc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "Enviro Clouds Undersample Buffer" );
            renderer.undersampleBufferHandle = renderGraph.ImportTexture(renderer.undersampleRTBufferHandle);
            
            renderer.frame++;
 
            if(renderer.frame > 64)
              renderer.frame = 0;   
            UpdateTemporalCloudHistoryForSkyPitch(renderer);
 
            if(renderer.depthMat == null)
               renderer.depthMat = new Material(Shader.Find("Hidden/EnviroVolumetricCloudsDepthURP"));
            
            renderer.depthMat.SetVector("_CameraDepthTexture_TexelSize", new Vector4(1 / cameraData.cameraTargetDescriptor.width, 1 / cameraData.cameraTargetDescriptor.height, cameraData.cameraTargetDescriptor.width, cameraData.cameraTargetDescriptor.height));
            
            SetToURP(renderer.depthMat);   
            CreateRenderTexture(ref renderer.downsampledDepthHandle,renderGraph, width, height, GraphicsFormat.R32_SFloat, FilterMode.Point, t);
            
            if (downsampling > 1) 
            {   
                pass.Blit("Downsample Depth", renderGraph,renderer.depthMat,src,renderer.downsampledDepthHandle,0);
            } 
            else 
            {
                pass.Blit("Copy Depth", renderGraph,renderer.depthMat,src,renderer.downsampledDepthHandle,1);
            }        
  
            //1. Raymarch
            SetRaymarchShader(cameraData.camera,renderer, quality);
            SetToURP(renderer.raymarchMat);     
            
            pass.Blit("Raymarch", renderGraph,renderer.raymarchMat,src,renderer.undersampleBufferHandle,0, renderer.downsampledDepthHandle, "_DownsampledDepth");

     


            //Pass 2: Reprojection
            if(cameraData.camera.cameraType != CameraType.Reflection)
            {     
                if(renderer.reprojectMat == null)
                   renderer.reprojectMat = new Material(Shader.Find("Hidden/EnviroVolumetricCloudsReprojectURP"));

                SetReprojectShader(cameraData.camera, renderer, quality);
                SetToURP(renderer.reprojectMat);

                if (renderer.firstFrame)  
                    pass.Blit("Reproject First Frame", renderGraph,renderer.reprojectMat,renderer.undersampleBufferHandle,renderer.fullBufferHandles[renderer.fullBufferIndex],0, renderer.downsampledDepthHandle, "_DownsampledDepth", renderer.undersampleBufferHandle, "_UndersampleCloudTex");              
             
                pass.Blit("Reproject", renderGraph,renderer.reprojectMat,renderer.fullBufferHandles[renderer.fullBufferIndex],renderer.fullBufferHandles[renderer.fullBufferIndex ^ 1],0, renderer.downsampledDepthHandle, "_DownsampledDepth", renderer.undersampleBufferHandle, "_UndersampleCloudTex");           
            }

            //Pass 3: Lighting and Blending       
            if(renderer.blendAndLightingMat == null)
               renderer.blendAndLightingMat = new Material(Shader.Find("Hidden/EnviroVolumetricCloudsBlendURP"));

            SetBlendShader(cameraData.camera,renderer);
            SetToURP(renderer.blendAndLightingMat);     
          

            if(cameraData.camera.cameraType == CameraType.Reflection)
            { 
                pass.Blit("Blend", renderGraph,renderer.blendAndLightingMat,src,target,0, renderer.downsampledDepthHandle,"_DownsampledDepth", renderer.undersampleBufferHandle, "_CloudTex");  
                return;
            } 

            ///Apply Clouds Shadows
            if(EnviroManager.instance.VolumetricClouds.settingsGlobal.cloudShadows)
            {
                RenderTextureDescriptor dscFull = new RenderTextureDescriptor(t.width,t.height,RenderTextureFormat.ARGBHalf,0);
                dscFull.vrUsage = t.vrUsage;
                dscFull.dimension = t.dimension;
                dscFull.volumeDepth = t.slices;

                UnityEngine.Rendering.RenderGraphModule.TextureHandle temp2 = UnityEngine.Rendering.Universal.UniversalRenderer.CreateRenderGraphTexture(renderGraph, dscFull, "Temp2", false);

                if(renderer.shadowMat == null)
                renderer.shadowMat = new Material(Shader.Find("Hidden/EnviroApplyShadowsURP"));

                renderer.shadowMat.SetFloat("_Intensity",EnviroManager.instance.VolumetricClouds.settingsGlobal.cloudShadowsIntensity);
                renderer.shadowMat.EnableKeyword("ENVIROURP");


                if(cameraData.camera.transform.position.y - settingsGlobal.floatingPointOriginMod.y < EnviroManager.instance.VolumetricClouds.settingsVolume.bottomCloudsHeight)
                {
                    pass.Blit("Blend", renderGraph,renderer.blendAndLightingMat,src,temp2,0, renderer.downsampledDepthHandle,"_DownsampledDepth", renderer.fullBufferHandles[renderer.fullBufferIndex ^ 1], "_CloudTex");   
                    pass.Blit("Apply Cloud Shadows", renderGraph,renderer.shadowMat,temp2,target,0, renderer.undersampleBufferHandle, "_CloudsTex");        
                }
                else
                {
                    pass.Blit("Apply Cloud Shadows", renderGraph,renderer.shadowMat,src,temp2,0, renderer.undersampleBufferHandle, "_CloudsTex");        
                    pass.Blit("Blend", renderGraph,renderer.blendAndLightingMat,temp2,target,0, renderer.downsampledDepthHandle,"_DownsampledDepth", renderer.fullBufferHandles[renderer.fullBufferIndex ^ 1], "_CloudTex");   
                } 
            }
            else
            {   
                pass.Blit("Blend", renderGraph,renderer.blendAndLightingMat,src,target,0, renderer.downsampledDepthHandle,"_DownsampledDepth", renderer.fullBufferHandles[renderer.fullBufferIndex ^ 1], "_CloudTex");   
            }



        #if ENABLE_VR || ENABLE_XR_MODULE
            if(UnityEngine.XR.XRSettings.enabled && UnityEngine.XR.XRSettings.stereoRenderingMode == UnityEngine.XR.XRSettings.StereoRenderingMode.SinglePassInstanced && cameraData.camera.stereoEnabled)
            {
                renderer.prevV = BuildEnviroStagePreviousViewProjection(cameraData.camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left), cameraData.camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse);
                renderer.prevVRight = BuildEnviroStagePreviousViewProjection(cameraData.camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right), cameraData.camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse);
            }
            else
            {
                renderer.prevV = BuildEnviroStagePreviousViewProjection(cameraData.camera.projectionMatrix, cameraData.camera.cameraToWorldMatrix);
            }
        #else
                renderer.prevV = BuildEnviroStagePreviousViewProjection(cameraData.camera.projectionMatrix, cameraData.camera.cameraToWorldMatrix);
        #endif

            renderer.firstFrame = false;
        }

#endif
#if !UNITY_6000_4_OR_NEWER
        public void RenderVolumetricCloudsURP(UnityEngine.Rendering.Universal.RenderingData renderingData, EnviroURPRenderPass pass, UnityEngine.Rendering.CommandBuffer cmd,  RenderTexture source, UnityEngine.Rendering.RenderTargetIdentifier destination, EnviroVolumetricCloudRenderer renderer, EnviroQuality quality)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Enviro Clouds Rendering");

            int downsampling = settingsQuality.downsampling;

            if(quality != null)
               downsampling = quality.volumetricCloudsOverride.downsampling;

            int width = renderingData.cameraData.camera.pixelWidth / downsampling;
            int height = renderingData.cameraData.camera.pixelHeight / downsampling;

            if(renderingData.cameraData.camera.cameraType != CameraType.Reflection)
            {
                if (renderer.fullBuffer == null || renderer.fullBuffer.Length != 2)
                {
                    renderer.fullBuffer = new RenderTexture[2];
                }
                renderer.fullBufferIndex = (renderer.fullBufferIndex + 1) % 2;
        

            renderer.firstFrame |= CreateRenderTexture(ref renderer.fullBuffer[0], width, height, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear,source.descriptor);
            renderer.firstFrame |= CreateRenderTexture(ref renderer.fullBuffer[1], width, height, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear,source.descriptor);
            }  
            
            renderer.firstFrame |= CreateRenderTexture(ref renderer.undersampleBuffer, width , height, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear,source.descriptor);

            renderer.frame++;
 
            if(renderer.frame > 64)
              renderer.frame = 0;   
            UpdateTemporalCloudHistoryForSkyPitch(renderer);
 
            if(renderer.depthMat == null)
               renderer.depthMat = new Material(Shader.Find("Hidden/EnviroVolumetricCloudsDepth"));
            
            renderer.depthMat.SetVector("_CameraDepthTexture_TexelSize", new Vector4(1 / renderingData.cameraData.cameraTargetDescriptor.width, 1 / renderingData.cameraData.cameraTargetDescriptor.height, renderingData.cameraData.cameraTargetDescriptor.width, renderingData.cameraData.cameraTargetDescriptor.height));
            
            SetToURP(renderer.depthMat);   
            CreateRenderTexture(ref renderer.downsampledDepth, width, height, RenderTextureFormat.RFloat, FilterMode.Point, source.descriptor);
            
            if (downsampling > 1) 
            {
                pass.CustomBlit(cmd,renderingData.cameraData.camera.cameraToWorldMatrix,source,renderer.downsampledDepth,renderer.depthMat,0);
            } 
            else 
            {
                pass.CustomBlit(cmd,renderingData.cameraData.camera.cameraToWorldMatrix,source,renderer.downsampledDepth,renderer.depthMat,1);
            }        
  
            //1. Raymarch
            
            SetRaymarchShader(renderingData.cameraData.camera,renderer, quality);
           
            SetToURP(renderer.raymarchMat);     
            pass.CustomBlit(cmd,renderingData.cameraData.camera.cameraToWorldMatrix,source,renderer.undersampleBuffer,renderer.raymarchMat);
  
            //Pass 2: Reprojection
            if(renderingData.cameraData.camera.cameraType != CameraType.Reflection)
            {     
                if(renderer.reprojectMat == null)
                   renderer.reprojectMat = new Material(Shader.Find("Hidden/EnviroVolumetricCloudsReproject"));
                SetReprojectShader(renderingData.cameraData.camera, renderer, quality);
                SetToURP(renderer.reprojectMat);
                
                if (renderer.firstFrame) 
                    pass.CustomBlit(cmd,renderingData.cameraData.camera.cameraToWorldMatrix,renderer.undersampleBuffer,renderer.fullBuffer[renderer.fullBufferIndex]);

                pass.CustomBlit(cmd,renderingData.cameraData.camera.cameraToWorldMatrix,renderer.fullBuffer[renderer.fullBufferIndex],renderer.fullBuffer[renderer.fullBufferIndex ^ 1],renderer.reprojectMat);
            }

            //Pass 3: Lighting and Blending       
            if(renderer.blendAndLightingMat == null)
               renderer.blendAndLightingMat = new Material(Shader.Find("Hidden/EnviroVolumetricCloudsBlend"));
            SetBlendShader(renderingData.cameraData.camera,renderer);
            SetToURP(renderer.blendAndLightingMat); 
            pass.CustomBlit(cmd,renderingData.cameraData.camera.cameraToWorldMatrix,source,destination,renderer.blendAndLightingMat);

 
        #if ENABLE_VR || ENABLE_XR_MODULE
            if(UnityEngine.XR.XRSettings.enabled && UnityEngine.XR.XRSettings.stereoRenderingMode == UnityEngine.XR.XRSettings.StereoRenderingMode.SinglePassInstanced && renderingData.cameraData.camera.stereoEnabled)
            {
                renderer.prevV = BuildEnviroStagePreviousViewProjection(renderingData.cameraData.camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left), renderingData.cameraData.camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse);
                renderer.prevVRight = BuildEnviroStagePreviousViewProjection(renderingData.cameraData.camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right), renderingData.cameraData.camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse);
            }
            else
            {
                renderer.prevV = BuildEnviroStagePreviousViewProjection(renderingData.cameraData.camera.projectionMatrix, renderingData.cameraData.camera.cameraToWorldMatrix);
            }
        #else
                renderer.prevV = BuildEnviroStagePreviousViewProjection(renderingData.cameraData.camera.projectionMatrix, renderingData.cameraData.camera.cameraToWorldMatrix);
        #endif
        
           
           renderer.firstFrame = false;
 
            UnityEngine.Profiling.Profiler.EndSample();
        }
#endif
#endif

#if ENVIRO_HDRP 
        public void RenderVolumetricCloudsHDRP(Camera cam, UnityEngine.Rendering.CommandBuffer cmd, UnityEngine.Rendering.RTHandle source, UnityEngine.Rendering.RTHandle destination, EnviroVolumetricCloudRenderer renderer, EnviroQuality quality)
        { 
            //UnityEngine.Profiling.Profiler.BeginSample("Enviro Clouds Rendering");
 
            int downsampling = settingsQuality.downsampling;

            if(quality != null)
               downsampling = quality.volumetricCloudsOverride.downsampling;

            int width = cam.pixelWidth / downsampling;
            int height = cam.pixelHeight / downsampling;

            RenderTextureDescriptor desc = source.rt.descriptor;
            //desc.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
  
            if(cam.cameraType != CameraType.Reflection)
            {
                if (renderer.fullBuffer == null || renderer.fullBuffer.Length != 2)
                {
                    renderer.fullBuffer = new RenderTexture[2];
                    renderer.fullBufferHandles = new UnityEngine.Rendering.RTHandle[2];
                }
                renderer.fullBufferIndex = (renderer.fullBufferIndex + 1) % 2;

                renderer.firstFrame |= CreateRenderTexture(ref renderer.fullBuffer[0], width, height, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear,desc);
                renderer.firstFrame |= CreateRenderTexture(ref renderer.fullBuffer[1], width, height, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear,desc);
                
                renderer.fullBufferHandles[0] = UnityEngine.Rendering.RTHandles.Alloc(renderer.fullBuffer[0]);
                renderer.fullBufferHandles[1] = UnityEngine.Rendering.RTHandles.Alloc(renderer.fullBuffer[1]);     
            }
            renderer.firstFrame |= CreateRenderTexture(ref renderer.undersampleBuffer, width , height, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear,desc);
            renderer.undersampleBufferHandle = UnityEngine.Rendering.RTHandles.Alloc(renderer.undersampleBuffer);


            renderer.frame++; 

            if(renderer.frame > 64)
              renderer.frame = 0;   
            UpdateTemporalCloudHistoryForSkyPitch(renderer);
 
            if(renderer.depthMat == null)
               renderer.depthMat = new Material(Shader.Find("Hidden/EnviroVolumetricCloudsDepthHDRP"));
 
            CreateRenderTexture(ref renderer.downsampledDepth, width, height, RenderTextureFormat.RFloat, FilterMode.Point, desc);
            renderer.downsampledDepthHandle = UnityEngine.Rendering.RTHandles.Alloc(renderer.downsampledDepth);      
            renderer.depthMat.SetTexture("_MainTex", source);

            if (downsampling > 1) 
            {
                cmd.Blit(source, renderer.downsampledDepthHandle, renderer.depthMat, 0);  //Downsample the texture.
            } 
            else 
            {
                cmd.Blit(source, renderer.downsampledDepthHandle, renderer.depthMat, 1);  //Just copy it.
            }        

           
            //1. Raymarch 
            SetRaymarchShader(cam,renderer, quality);
            renderer.raymarchMat.SetTexture("_MainTex", source);
            cmd.Blit(source,renderer.undersampleBufferHandle,renderer.raymarchMat);


            //Pass 2: Reprojection
            if(cam.cameraType != CameraType.Reflection)
            {
                if(renderer.reprojectMat == null)
                   renderer.reprojectMat = new Material(Shader.Find("Hidden/EnviroVolumetricCloudsReprojectHDRP"));

                SetReprojectShader(cam, renderer, quality);
    
                if (renderer.firstFrame) 
                {
                    cmd.Blit(renderer.undersampleBufferHandle, renderer.fullBufferHandles[renderer.fullBufferIndex]);
                }
 
                renderer.reprojectMat.SetTexture("_MainTex", renderer.fullBufferHandles[renderer.fullBufferIndex]);
                renderer.reprojectMat.SetVector("_MainTexHandleScale", new Vector4(1/renderer.fullBufferHandles[renderer.fullBufferIndex].rtHandleProperties.rtHandleScale.x,1/renderer.fullBufferHandles[renderer.fullBufferIndex].rtHandleProperties.rtHandleScale.y,renderer.fullBuffer[renderer.fullBufferIndex].width,renderer.fullBuffer[renderer.fullBufferIndex].height));
                cmd.Blit(renderer.fullBufferHandles[renderer.fullBufferIndex], renderer.fullBufferHandles[renderer.fullBufferIndex ^ 1], renderer.reprojectMat);
            }
            //Pass 3: Lighting and Blending
            if(renderer.blendAndLightingMat == null)
               renderer.blendAndLightingMat = new Material(Shader.Find("Hidden/EnviroVolumetricCloudsBlendHDRP"));

            SetBlendShader(cam,renderer);
            renderer.blendAndLightingMat.SetTexture("_MainTex", source); 
            cmd.Blit(source, destination, renderer.blendAndLightingMat);

            #if ENABLE_VR || ENABLE_XR_MODULE
            if(UnityEngine.XR.XRSettings.enabled && UnityEngine.XR.XRSettings.stereoRenderingMode == UnityEngine.XR.XRSettings.StereoRenderingMode.SinglePassInstanced && cam.stereoEnabled)
            {
                renderer.prevV = BuildEnviroStagePreviousViewProjection(cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left), cam.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse);
                renderer.prevVRight = BuildEnviroStagePreviousViewProjection(cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right), cam.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse);
            }
            else
            {
                renderer.prevV = BuildEnviroStagePreviousViewProjection(cam.projectionMatrix, cam.cameraToWorldMatrix);
            }
        #else
                renderer.prevV = BuildEnviroStagePreviousViewProjection(cam.projectionMatrix, cam.cameraToWorldMatrix);
        #endif
           
           
           
           
            renderer.firstFrame = false;

 
            //UnityEngine.Profiling.Profiler.EndSample();
        }
#endif

        void SetRaymarchShader (Camera cam, EnviroVolumetricCloudRenderer renderer, EnviroQuality quality)
        {
            if(renderer.raymarchMat == null)
        #if ENVIRO_HDRP 
             renderer.raymarchMat = new Material(Shader.Find("Hidden/EnviroCloudsRaymarchHDRP"));
        #elif ENVIRO_URP && UNITY_6000_0_OR_NEWER
        if (IsURPCompabilityMode ())
           renderer.raymarchMat = new Material(Shader.Find("Hidden/EnviroCloudsRaymarch"));
        else
           renderer.raymarchMat = new Material(Shader.Find("Hidden/EnviroCloudsRaymarchURP"));
        #else
             renderer.raymarchMat = new Material(Shader.Find("Hidden/EnviroCloudsRaymarch"));
        #endif

            if(dirLight == null)
            {
               dirLight = EnviroHelper.GetDirectionalLight();
            }
            //Check if we use dual lightmode and change the light only in that case. Otherwise keep it to the cached one.
            else if (EnviroManager.instance.Lighting != null && EnviroManager.instance.Lighting.Settings.lightingMode == EnviroLighting.LightingMode.Dual)
            {
               dirLight = EnviroHelper.GetDirectionalLight();
            }

            EnviroCloudLayerSettings layer1 = settingsVolume;
            EnviroCloudGlobalSettings global = settingsGlobal;
            
            float blueNoiseIntensity = settingsQuality.blueNoiseIntensity;
            float lodDistance = settingsQuality.lodDistance;
            Vector4 steps = new Vector4(settingsQuality.stepsLayer1,settingsQuality.stepsLayer1,settingsQuality.stepsLayer2,settingsQuality.stepsLayer2);
            int downsample = settingsQuality.downsampling;

            bool lightningSupport =  settingsQuality.lightningSupport;
            bool variableBottomsNoise =  settingsQuality.variableBottomNoise;

            if(quality != null)
            {
                blueNoiseIntensity = quality.volumetricCloudsOverride.blueNoiseIntensity;
                steps = new Vector4(quality.volumetricCloudsOverride.stepsLayer1,quality.volumetricCloudsOverride.stepsLayer1,0,0);
                lodDistance = quality.volumetricCloudsOverride.lodDistance;
                downsample = quality.volumetricCloudsOverride.downsampling;
                lightningSupport = quality.volumetricCloudsOverride.lightningSupport;
                variableBottomsNoise= quality.volumetricCloudsOverride.variableBottomNoise;
            }

            if(EnviroManager.instance.Lightning == null)
                lightningSupport = false;

            if(lightningSupport)
            renderer.raymarchMat.EnableKeyword("ENVIRO_LIGHTNING");
            else
            renderer.raymarchMat.DisableKeyword("ENVIRO_LIGHTNING");

            if(variableBottomsNoise)
            renderer.raymarchMat.EnableKeyword("ENVIRO_VARIABLE_BOTTOM");
            else
            renderer.raymarchMat.DisableKeyword("ENVIRO_VARIABLE_BOTTOM");
                  
            //Textures
            renderer.raymarchMat.SetTexture("_Noise", settingsGlobal.noise);
            renderer.raymarchMat.SetTexture("_DetailNoise", settingsGlobal.detailNoise); 
            renderer.raymarchMat.SetTexture("_CurlNoise", settingsGlobal.curlTex);
            if(settingsGlobal.bottomsOffsetNoise != null)
            renderer.raymarchMat.SetTexture("_BottomsOffsetNoise", settingsGlobal.bottomsOffsetNoise);

            if(weatherMap != null)
               renderer.raymarchMat.SetTexture("_WeatherMap",weatherMap);
            else if (settingsGlobal.customWeatherMap != null)
               renderer.raymarchMat.SetTexture("_WeatherMap",settingsGlobal.customWeatherMap);

            //Matrix
            renderer.raymarchMat.SetMatrix("_EnviroStageCloudPitchMatrix", BuildEnviroStageSkyPitchMatrix());
        #if ENABLE_VR || ENABLE_XR_MODULE
            if(UnityEngine.XR.XRSettings.enabled && UnityEngine.XR.XRSettings.stereoRenderingMode == UnityEngine.XR.XRSettings.StereoRenderingMode.SinglePassInstanced && cam.stereoEnabled)
            {
                renderer.raymarchMat.SetMatrix("_InverseProjection", cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left).inverse);
                renderer.raymarchMat.SetMatrix("_InverseRotation", cam.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse);
                renderer.raymarchMat.SetMatrix("_InverseProjectionRight", cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right).inverse);
                renderer.raymarchMat.SetMatrix("_InverseRotationRight", cam.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse);
            }
            else
            {
                renderer.raymarchMat.SetMatrix("_InverseProjection", cam.projectionMatrix.inverse);
                renderer.raymarchMat.SetMatrix("_InverseRotation", cam.cameraToWorldMatrix);
            }
        #else
                renderer.raymarchMat.SetMatrix("_InverseProjection", cam.projectionMatrix.inverse);
                renderer.raymarchMat.SetMatrix("_InverseRotation", cam.cameraToWorldMatrix);
        #endif

            if (EnviroManager.instance.Objects.worldAnchor != null)
                settingsGlobal.floatingPointOriginMod = EnviroManager.instance.Objects.worldAnchor.transform.position;
            else
                 settingsGlobal.floatingPointOriginMod = Vector3.zero;
             
           settingsGlobal.floatingPointOriginMod += settingsGlobal.cloudScrollOffset;

            renderer.raymarchMat.SetVector("_CameraPosition", cam.transform.position - settingsGlobal.floatingPointOriginMod);
            renderer.raymarchMat.SetVector("_WorldOffset", settingsGlobal.floatingPointOriginMod);

      
            renderer.raymarchMat.SetVector("_Steps", steps);
            
            if(dirLight != null)
            renderer.raymarchMat.SetVector("_LightDir", -dirLight.transform.forward);  
            else
            renderer.raymarchMat.SetVector("_LightDir", Vector3.zero);  

            renderer.raymarchMat.SetVector("_CloudsNoiseSettings", new Vector4(layer1.baseNoiseUV * layer1.baseNoiseUVMultiplier, layer1.detailNoiseUV* layer1.detailNoiseUVMultiplier, layer1.baseNoiseMultiplier, layer1.detailNoiseMultiplier));

            renderer.raymarchMat.SetVector("_CloudsLighting", new Vector4(layer1.scatteringIntensity, layer1.silverLiningIntensity, layer1.edgeHighlightStrength, layer1.silverLiningSpread));
          
            renderer.raymarchMat.SetVector("_CloudsLightingExtended", new Vector4(layer1.lightningIntensity, layer1.curlIntensity, layer1.lightStepModifier, layer1.absorbtion));   
          
            renderer.raymarchMat.SetVector("_CloudsMultiScattering", new Vector4(layer1.multiScatterStrength, layer1.multiScatterFalloff, layer1.ambientFloor, layer1.exposure));
           
            renderer.raymarchMat.SetVector("_CloudsShape1", new Vector4(layer1.bottomShape, layer1.midShape, layer1.topShape, layer1.topLayer));

            renderer.raymarchMat.SetVector("_CloudsParameter", new Vector4(layer1.bottomCloudsHeight, layer1.topCloudsHeight, 1 / (layer1.topCloudsHeight - layer1.bottomCloudsHeight), settingsGlobal.cloudsWorldScale));
    
            renderer.raymarchMat.SetFloat("_BlueNoiseIntensity",blueNoiseIntensity);
            renderer.raymarchMat.SetVector("_CloudDensityScale", new Vector4(layer1.density, 0f, layer1.densitySmoothness, 0f));
            renderer.raymarchMat.SetVector("_CloudsCoverageSettings", new Vector4(layer1.coverage, settingsGlobal.maxRenderDistance, layer1.cloudTypeShaping, layer1.rampShape));
            renderer.raymarchMat.SetVector("_CloudsAnimation", new Vector4(cloudAnimLayer1.x, cloudAnimLayer1.y, cloudAnimLayer1.z, 0f));
            if(EnviroManager.instance.Environment != null)
            { 
                renderer.raymarchMat.SetVector("_CloudsWindDirection", new Vector4(EnviroManager.instance.Environment.Settings.windDirectionX * settingsVolume.cloudsWindDirectionXModifier, EnviroManager.instance.Environment.Settings.windDirectionY * settingsVolume.cloudsWindDirectionYModifier, cloudAnimNonScaledLayer1.x,cloudAnimNonScaledLayer1.y));
            }         
            else
            {
                 renderer.raymarchMat.SetVector("_CloudsWindDirection", new Vector4(settingsVolume.cloudsWindDirectionXModifier, settingsVolume.cloudsWindDirectionYModifier, cloudAnimNonScaledLayer1.x,cloudAnimNonScaledLayer1.y));
            } 

            renderer.raymarchMat.SetVector("_CloudsErosionIntensity", new Vector4(1- layer1.baseErosionIntensity, layer1.detailErosionIntensity,0f, 0f));
            renderer.raymarchMat.SetFloat("_LODDistance", lodDistance);
    
            #if ENVIRO_HDRP
            renderer.raymarchMat.SetTexture("_DownsampledDepth", renderer.downsampledDepthHandle);
            renderer.raymarchMat.SetVector("_DepthHandleScale", new Vector4(1/renderer.downsampledDepthHandle.rtHandleProperties.rtHandleScale.x,1/renderer.downsampledDepthHandle.rtHandleProperties.rtHandleScale.y,renderer.downsampledDepth.width,renderer.downsampledDepth.height));
            #elif ENVIRO_URP && UNITY_6000_0_OR_NEWER
            if (IsURPCompabilityMode ())
                renderer.raymarchMat.SetTexture("_DownsampledDepth", renderer.downsampledDepth);       
            //Needs to be set directly in builder!
            #else
            renderer.raymarchMat.SetTexture("_DownsampledDepth", renderer.downsampledDepth); 
            #endif   
 
            renderer.raymarchMat.SetInt("_Frame", renderer.frame);
      
            renderer.raymarchMat.SetTexture("_BlueNoise",settingsGlobal.blueNoise);
        
            renderer.raymarchMat.SetVector("_Randomness", new Vector4(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value));  
           
            renderer.raymarchMat.SetVector("_Resolution", new Vector4(cam.pixelWidth, cam.pixelHeight,0f,0f));


            if(settingsGlobal.cloudShadows)
                renderer.raymarchMat.EnableKeyword("ENVIRO_CLOUD_SHADOWS");
            else
                renderer.raymarchMat.DisableKeyword("ENVIRO_CLOUD_SHADOWS");

            renderer.raymarchMat.SetFloat("_DepthTest", settingsGlobal.depthTest ? 1f : 0f);
            renderer.raymarchMat.SetFloat("_SolarTime", EnviroManager.instance.solarTime);

            SetDepthBlending(renderer.raymarchMat);

        } 
 
        void SetReprojectShader (Camera cam, EnviroVolumetricCloudRenderer renderer, EnviroQuality quality)
        {
          
            float reprojectionBlendTime = settingsQuality.reprojectionBlendTime;
                    
            if(quality != null)
            {
                reprojectionBlendTime = quality.volumetricCloudsOverride.reprojectionBlendTime;
            }

            SetDepthBlending(renderer.reprojectMat);
            renderer.reprojectMat.SetFloat("_EnviroCloudHistoryReset", renderer.firstFrame ? 1f : 0f);

        #if ENVIRO_HDRP
            renderer.reprojectMat.SetTexture("_DownsampledDepth", renderer.downsampledDepthHandle);
            renderer.reprojectMat.SetVector("_DepthHandleScale", new Vector4(1/renderer.downsampledDepthHandle.rtHandleProperties.rtHandleScale.x,1/renderer.downsampledDepthHandle.rtHandleProperties.rtHandleScale.y,renderer.downsampledDepth.width,renderer.downsampledDepth.height));
            renderer.reprojectMat.SetTexture("_UndersampleCloudTex", renderer.undersampleBufferHandle);
            renderer.reprojectMat.SetVector("_UndersampleCloudTexScale", new Vector4(1/renderer.undersampleBufferHandle.rtHandleProperties.rtHandleScale.x,1/renderer.undersampleBufferHandle.rtHandleProperties.rtHandleScale.y,renderer.undersampleBuffer.width,renderer.undersampleBuffer.height));
        #elif UNITY_6000_0_OR_NEWER && ENVIRO_URP
        if (IsURPCompabilityMode ())
        {
            renderer.reprojectMat.SetTexture("_DownsampledDepth", renderer.downsampledDepth);
            renderer.reprojectMat.SetTexture("_UndersampleCloudTex", renderer.undersampleBuffer);
        } 
            //Set directly in builder!
        #else
            renderer.reprojectMat.SetTexture("_DownsampledDepth", renderer.downsampledDepth);
            renderer.reprojectMat.SetTexture("_UndersampleCloudTex", renderer.undersampleBuffer);
        #endif         
  #if ENABLE_VR || ENABLE_XR_MODULE
            if(UnityEngine.XR.XRSettings.enabled && UnityEngine.XR.XRSettings.stereoRenderingMode == UnityEngine.XR.XRSettings.StereoRenderingMode.SinglePassInstanced) 
            {
                renderer.reprojectMat.SetMatrix("_PrevVP", renderer.prevV);
                renderer.reprojectMat.SetMatrix("_PrevVPRight", renderer.prevVRight);
                renderer.reprojectMat.SetVector("_ProjectionExtents", EnviroHelper.GetProjectionExtents(cam,Camera.StereoscopicEye.Left));
                renderer.reprojectMat.SetVector("_ProjectionExtentsRight", EnviroHelper.GetProjectionExtents(cam,Camera.StereoscopicEye.Right));
            }
            else 
            {
                renderer.reprojectMat.SetMatrix("_PrevVP", renderer.prevV);
                renderer.reprojectMat.SetVector("_ProjectionExtents", EnviroHelper.GetProjectionExtents(cam));
            }
        #else
             renderer.reprojectMat.SetMatrix("_PrevVP", renderer.prevV);
             renderer.reprojectMat.SetVector("_ProjectionExtents", EnviroHelper.GetProjectionExtents(cam));
        #endif


            Matrix4x4 ctw;  

            if(lastOffset != settingsGlobal.floatingPointOriginMod)
            {
                ctw = Matrix4x4.TRS(cam.transform.position - (settingsGlobal.floatingPointOriginMod - lastOffset), cam.transform.rotation, Vector3.one);
                ctw = ApplyEnviroStageSkyPitch(ctw);
                renderer.reprojectMat.SetMatrix("_CamToWorld",  ctw);

                lastOffset = settingsGlobal.floatingPointOriginMod;
            }
            else
            {
                ctw = Matrix4x4.TRS(cam.transform.position, cam.transform.rotation, Vector3.one);
                ctw = ApplyEnviroStageSkyPitch(ctw);
                renderer.reprojectMat.SetMatrix("_CamToWorld",  ctw);
            }
            renderer.reprojectMat.SetFloat("_BlendTime", reprojectionBlendTime);     
        }

        void SetBlendShader(Camera cam, EnviroVolumetricCloudRenderer renderer)
        {
            SetDepthBlending(renderer.blendAndLightingMat);

#if ENABLE_VR || ENABLE_XR_MODULE
            if (UnityEngine.XR.XRSettings.enabled && UnityEngine.XR.XRSettings.stereoRenderingMode == UnityEngine.XR.XRSettings.StereoRenderingMode.SinglePassInstanced)
            {
                renderer.blendAndLightingMat.SetVector("_ProjectionExtents", EnviroHelper.GetProjectionExtents(cam, Camera.StereoscopicEye.Left));
                renderer.blendAndLightingMat.SetVector("_ProjectionExtentsRight", EnviroHelper.GetProjectionExtents(cam, Camera.StereoscopicEye.Right));
            }
            else
            {
                renderer.blendAndLightingMat.SetVector("_ProjectionExtents", EnviroHelper.GetProjectionExtents(cam));
            }
#else
            renderer.blendAndLightingMat.SetVector("_ProjectionExtents", EnviroHelper.GetProjectionExtents(cam));
#endif

#if ENVIRO_HDRP
            renderer.blendAndLightingMat.SetTexture("_DownsampledDepth", renderer.downsampledDepthHandle);
            renderer.blendAndLightingMat.SetVector("_DepthHandleScale", new Vector4(1/renderer.downsampledDepthHandle.rtHandleProperties.rtHandleScale.x,1/renderer.downsampledDepthHandle.rtHandleProperties.rtHandleScale.y,renderer.downsampledDepth.width,renderer.downsampledDepth.height));
#elif ENVIRO_URP && UNITY_6000_0_OR_NEWER
            if (IsURPCompabilityMode ())
            {
                renderer.blendAndLightingMat.SetTexture("_DownsampledDepth", renderer.downsampledDepth);
            }
            //Set directly in builder
#else
            renderer.blendAndLightingMat.SetTexture("_DownsampledDepth", renderer.downsampledDepth);
#endif

            Matrix4x4 camtowolrd = ApplyEnviroStageSkyPitch(Matrix4x4.TRS(cam.transform.position, cam.transform.rotation, Vector3.one));
            renderer.blendAndLightingMat.SetMatrix("_CamToWorld", camtowolrd);

            Color directLightColor;

            if (!EnviroManager.instance.isNight)
            {
                directLightColor = settingsGlobal.sunLightColorGradient.Evaluate(EnviroManager.instance.solarTime);
            }
            else
            {
                directLightColor = settingsGlobal.moonLightColorGradient.Evaluate(EnviroManager.instance.lunarTime);
            }

            Shader.SetGlobalColor("_DirectLightColor", directLightColor);
            Shader.SetGlobalColor("_AmbientColor", settingsGlobal.ambientColorGradient.Evaluate(EnviroManager.instance.solarTime) * settingsGlobal.ambientLighIntensity);
            Shader.SetGlobalFloat("_AtmosphereColorSaturateDistance", settingsGlobal.atmosphereColorSaturateDistance);
            Shader.SetGlobalVector("_CloudsParameter", new Vector4(settingsVolume.bottomCloudsHeight, settingsVolume.topCloudsHeight, 1 / (settingsVolume.topCloudsHeight - settingsVolume.bottomCloudsHeight), settingsGlobal.cloudsWorldScale));
            Shader.SetGlobalFloat("_SolarTime", EnviroManager.instance.solarTime);



            //We don't use reprojection pass for reflections.
#if ENVIRO_HDRP
            if(cam.cameraType == CameraType.Reflection)
            {
               renderer.blendAndLightingMat.SetTexture("_CloudTex", renderer.undersampleBufferHandle);
               renderer.blendAndLightingMat.SetVector("_HandleScales", new Vector4(1 / renderer.undersampleBufferHandle.rtHandleProperties.rtHandleScale.x, 1 / renderer.undersampleBufferHandle.rtHandleProperties.rtHandleScale.y,1,1));
            }  
            else
            {
               renderer.blendAndLightingMat.SetTexture("_CloudTex", renderer.fullBufferHandles[renderer.fullBufferIndex ^ 1]);
               renderer.blendAndLightingMat.SetVector("_HandleScales", new Vector4(1/ renderer.fullBufferHandles[renderer.fullBufferIndex ^ 1].rtHandleProperties.rtHandleScale.x, 1/ renderer.fullBufferHandles[renderer.fullBufferIndex ^ 1].rtHandleProperties.rtHandleScale.y,1,1));
               //renderer.blendAndLightingMat.SetTexture("_CloudTex", renderer.undersampleBufferHandle);
               //renderer.blendAndLightingMat.SetVector("_HandleScales", new Vector4(1 / renderer.undersampleBufferHandle.rtHandleProperties.rtHandleScale.x, 1 / renderer.undersampleBufferHandle.rtHandleProperties.rtHandleScale.y,1,1));
            
               // Shader.SetGlobalTexture("_EnviroVolumetricClouds", renderer.fullBufferHandles[renderer.fullBufferIndex ^ 1]);
            }
#elif ENVIRO_URP && UNITY_6000_0_OR_NEWER
            if (IsURPCompabilityMode ())
            {
                if (cam.cameraType == CameraType.Reflection)
                {
                    renderer.blendAndLightingMat.SetTexture("_CloudTex", renderer.undersampleBuffer);
                }
                else
                {
                    renderer.blendAndLightingMat.SetTexture("_CloudTex", renderer.fullBuffer[renderer.fullBufferIndex ^ 1]);
                }
            }
            // Set directly in builder.
#else
            if(cam.cameraType == CameraType.Reflection)
            { 
               renderer.blendAndLightingMat.SetTexture("_CloudTex", renderer.undersampleBuffer);
            }   
            else
            {
              renderer.blendAndLightingMat.SetTexture("_CloudTex", renderer.fullBuffer[renderer.fullBufferIndex ^ 1]);
            } 
#endif

            if (renderer.camera != null && (renderer.camera.transform.position.y - settingsGlobal.floatingPointOriginMod.y) <= settingsVolume.bottomCloudsHeight)
            {
#if ENVIRO_HDRP
                
                    if(blackArray == null)
                    CreateBlackArray(); 
                    
                    Shader.SetGlobalTexture("_EnviroClouds", blackArray);

#else

                if (cam.stereoEnabled)
                {
                    if (blackArray == null)
                        CreateBlackArray();

                    Shader.SetGlobalTexture("_EnviroClouds", blackArray);
                }
                else
                {
                    Shader.SetGlobalTexture("_EnviroClouds", Texture2D.blackTexture);
                }


#endif

                return;
            }

#if ENVIRO_HDRP
            if(renderer != null && renderer.fullBufferHandles != null && renderer.fullBufferHandles.Length >= 2 && renderer.fullBufferHandles[renderer.fullBufferIndex ^ 1] != null)
            {
                Shader.SetGlobalTexture("_EnviroClouds", renderer.fullBufferHandles[renderer.fullBufferIndex ^ 1]);
            } 
#elif ENVIRO_URP && UNITY_6000_0_OR_NEWER
            if (IsURPCompabilityMode ())
            {
                 if(renderer != null && renderer.fullBuffer != null && renderer.fullBuffer.Length >= 2 && renderer.fullBuffer[renderer.fullBufferIndex ^ 1] != null)
                {
                    Shader.SetGlobalTexture("_EnviroClouds", renderer.fullBuffer[renderer.fullBufferIndex ^ 1]);
                } 
            }
            else
            {
                if (renderer != null && renderer.fullBufferRTHandles != null && renderer.fullBufferRTHandles.Length >= 2 && renderer.fullBufferRTHandles[renderer.fullBufferIndex ^ 1] != null)
                {
                    Shader.SetGlobalTexture("_EnviroClouds", renderer.fullBufferRTHandles[renderer.fullBufferIndex ^ 1]);
                }
            }
        #else
            if(renderer != null && renderer.fullBuffer != null && renderer.fullBuffer.Length >= 2 && renderer.fullBuffer[renderer.fullBufferIndex ^ 1] != null)
            {
                Shader.SetGlobalTexture("_EnviroClouds", renderer.fullBuffer[renderer.fullBufferIndex ^ 1]);
            } 
#endif
            }

         private void SetDepthBlending(Material mat)
        {        
            if(settingsGlobal.depthBlending)
                mat.EnableKeyword("ENVIRO_DEPTH_BLENDING");
            else
                mat.DisableKeyword("ENVIRO_DEPTH_BLENDING");
        }

        private void SetToURP(Material mat)
        {        
            mat.EnableKeyword("ENVIROURP");
        }


#if ENVIRO_URP && UNITY_6000_0_OR_NEWER
        UnityEngine.Rendering.RenderGraphModule.TextureDesc cloudsDescriptor;
        public bool CreateRenderTexture(ref UnityEngine.Rendering.RenderGraphModule.TextureHandle texture, UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, int width, int height, GraphicsFormat format, FilterMode filterMode, UnityEngine.Rendering.RenderGraphModule.TextureDesc dsc)
        {   
            dsc.width = width;
            dsc.height = height;
            dsc.colorFormat = format; 
            dsc.depthBufferBits = 0;
            dsc.msaaSamples = MSAASamples.None;
            dsc.filterMode = filterMode;
            //texture = UnityEngine.Rendering.Universal.UniversalRenderer.CreateRenderGraphTexture(renderGraph, d, "Temp Texture", false, filterMode);  
            texture = renderGraph.CreateTexture(dsc);

            if(cloudsDescriptor.width != dsc.width || cloudsDescriptor.height != dsc.height || cloudsDescriptor.vrUsage != dsc.vrUsage)
            { 
               cloudsDescriptor = dsc;
               return true;
            }
            else
            {
               cloudsDescriptor = dsc;
               return false;
            }  
        }
#endif


        public bool CreateRenderTexture(ref RenderTexture texture, int width, int height, RenderTextureFormat format, FilterMode filterMode, RenderTextureDescriptor dsc)
        {
            if(texture != null && (texture.width != width || texture.height != height || texture.vrUsage != dsc.vrUsage))
            {
                DestroyImmediate(texture);
                texture = null;             
            }
            if(texture == null)
            {
                RenderTextureDescriptor d = dsc;
                d.width = width;
                d.height = height;
                d.colorFormat = format;
                d.depthBufferBits = 0;
                texture = new RenderTexture(d);
                texture.antiAliasing = 1;
                texture.useMipMap = false;
                texture.filterMode = filterMode;
                texture.Create();

                return true;
            }
            else
            {
                return false;
            }
        }
  
        public RenderTexture RenderWeatherMap()
        {
            if(settingsGlobal.customWeatherMap != null)
               return null;

            if(weatherMapMat == null)
               weatherMapMat = new Material(Shader.Find("Enviro3/Standard/WeatherTexture"));

            if (weatherMap == null)
            {
                RenderTextureFormat format = RenderTextureFormat.ARGBFloat;
                #if UNITY_IOS || UNITY_ANDROID
                format = RenderTextureFormat.ARGBHalf;
                #endif
                weatherMap = new RenderTexture(512, 512, 0, format);
                weatherMap.wrapMode = TextureWrapMode.Repeat;                   
            } 
  
            weatherMapMat.SetFloat("_CoverageLayer1",settingsVolume.coverage);    
            weatherMapMat.SetFloat("_WorleyFreq1Layer1", settingsVolume.worleyFreq1);
            weatherMapMat.SetFloat("_WorleyFreq2Layer1",settingsVolume.worleyFreq2);
            weatherMapMat.SetFloat("_DilateCoverageLayer1",settingsVolume.dilateCoverage);
            weatherMapMat.SetFloat("_DilateTypeLayer1",settingsVolume.dilateType);
            weatherMapMat.SetFloat("_CloudsTypeModifierLayer1",settingsVolume.cloudsTypeModifier);
            weatherMapMat.SetVector("_LocationOffset",new Vector4(settingsVolume.locationOffset.x,settingsVolume.locationOffset.y,0f,0f));
            weatherMapMat.SetVector("_WindDirectionLayer1", cloudAnimNonScaledLayer1);
            weatherMapMat.SetVector("_WindDirectionLayer2", cloudAnimNonScaledLayer2);
        
            Graphics.Blit(null,weatherMap,weatherMapMat);
            return weatherMap;
        }


        private void UpdateWind()
        {      
            if(EnviroManager.instance.Environment != null)
            {
                cloudAnimLayer1 += new Vector3(
                (EnviroManager.instance.Environment.Settings.windSpeed * settingsVolume.windSpeedModifier * EnviroManager.instance.Environment.Settings.windDirectionX * settingsVolume.cloudsWindDirectionXModifier) * Time.deltaTime,
                (EnviroManager.instance.Environment.Settings.windSpeed * settingsVolume.windSpeedModifier * EnviroManager.instance.Environment.Settings.windDirectionY * settingsVolume.cloudsWindDirectionYModifier) * Time.deltaTime,
                (-1f * settingsVolume.windUpwards * Time.deltaTime));
                            
                cloudAnimLayer1 = EnviroHelper.PingPong(cloudAnimLayer1);
                cloudAnimNonScaledLayer1 += new Vector3((settingsVolume.windSpeedModifier * EnviroManager.instance.Environment.Settings.windSpeed * EnviroManager.instance.Environment.Settings.windDirectionX * settingsVolume.cloudsWindDirectionXModifier) * Time.deltaTime * 4f, (settingsVolume.windSpeedModifier * EnviroManager.instance.Environment.Settings.windSpeed * EnviroManager.instance.Environment.Settings.windDirectionY * settingsVolume.cloudsWindDirectionYModifier) * Time.deltaTime* 4f, -1f * EnviroManager.instance.Environment.Settings.windSpeed * Time.deltaTime ) * settingsGlobal.cloudsTravelSpeed * 0.2f;
           
            }
            else 
            {
                cloudAnimLayer1 += new Vector3(
                (settingsVolume.windSpeedModifier * settingsVolume.cloudsWindDirectionXModifier) * Time.deltaTime,
                (settingsVolume.windSpeedModifier * settingsVolume.cloudsWindDirectionYModifier) * Time.deltaTime,
                (-1f * settingsVolume.windUpwards * Time.deltaTime));
                            
                cloudAnimLayer1 = EnviroHelper.PingPong(cloudAnimLayer1);          
                cloudAnimNonScaledLayer1 += new Vector3((settingsVolume.windSpeedModifier * settingsVolume.cloudsWindDirectionXModifier) * Time.deltaTime* 4f, (settingsVolume.windSpeedModifier * settingsVolume.cloudsWindDirectionYModifier) * Time.deltaTime* 4f, -1f * settingsVolume.windUpwards * Time.deltaTime) * settingsGlobal.cloudsTravelSpeed * 0.2f;
            }
        
        }

        //Save and Load
        public void LoadModuleValues ()
        {
            if(preset != null)
            { 
                settingsVolume = JsonUtility.FromJson<Enviro.EnviroCloudLayerSettings>(JsonUtility.ToJson(preset.settingsVolume));
                settingsGlobal = JsonUtility.FromJson<Enviro.EnviroCloudGlobalSettings>(JsonUtility.ToJson(preset.settingsGlobal));
            }
            else
            {
                Debug.Log("Please assign a saved module to load from!");
            }
        }

        public void SaveModuleValues ()
        {
#if UNITY_EDITOR
        EnviroVolumetricCloudsModule t =  ScriptableObject.CreateInstance<EnviroVolumetricCloudsModule>();
        t.name = "Cloud Module";
        t.settingsVolume = JsonUtility.FromJson<Enviro.EnviroCloudLayerSettings>(JsonUtility.ToJson(settingsVolume));
        t.settingsGlobal = JsonUtility.FromJson<Enviro.EnviroCloudGlobalSettings>(JsonUtility.ToJson(settingsGlobal));

        string assetPathAndName = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(EnviroHelper.assetPath + "/New " + t.name + ".asset");
        UnityEditor.AssetDatabase.CreateAsset(t, assetPathAndName);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
#endif
        }

        public void SaveModuleValues (EnviroVolumetricCloudsModule module)
        {
            module.settingsVolume = JsonUtility.FromJson<Enviro.EnviroCloudLayerSettings>(JsonUtility.ToJson(settingsVolume));
            module.settingsGlobal = JsonUtility.FromJson<Enviro.EnviroCloudGlobalSettings>(JsonUtility.ToJson(settingsGlobal));
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(module);
            UnityEditor.AssetDatabase.SaveAssets();
            #endif
        }
    }
}
