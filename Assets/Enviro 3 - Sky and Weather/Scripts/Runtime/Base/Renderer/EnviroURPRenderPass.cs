#if ENVIRO_URP
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering; 

namespace Enviro
{
    public class EnviroURPRenderPass : ScriptableRenderPass
    {      
        private static readonly int EnviroStageSkyPitchId = Shader.PropertyToID("_EnviroStageSkyPitch");
    
    #if !UNITY_6000_4_OR_NEWER
        public ScriptableRenderer scriptableRenderer { get; set; }
        
        private Material blitThroughMat;
        private string pName;

        private List<EnviroVolumetricCloudRenderer> volumetricCloudsRender = new List<EnviroVolumetricCloudRenderer>();
        private Vector3 floatingPointOriginMod = Vector3.zero;

        public EnviroURPRenderPass (string name)
        { 
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents - 1;
            pName = name;
        }  
  
        public void CustomBlit(CommandBuffer cmd,Matrix4x4 matrix, RenderTargetIdentifier source, RenderTargetIdentifier target, Material mat, int pass)
        {
            cmd.SetGlobalTexture("_MainTex", source);
            cmd.SetRenderTarget(target, 0, CubemapFace.Unknown, -1);
            cmd.DrawMesh(RenderingUtils.fullscreenMesh, matrix, mat,0, pass);
        }

        public void CustomBlit(CommandBuffer cmd,Matrix4x4 matrix, RenderTargetIdentifier source, RenderTargetIdentifier target, Material mat)
        {
            cmd.SetGlobalTexture("_MainTex", source);
            cmd.SetRenderTarget(target, 0, CubemapFace.Unknown, -1);
            cmd.DrawMesh(RenderingUtils.fullscreenMesh, matrix, mat,0);
        }

        public void CustomBlit(CommandBuffer cmd,Matrix4x4 matrix, RenderTargetIdentifier source, RenderTargetIdentifier target)
        {
            if(blitThroughMat == null)
               blitThroughMat = new Material(Shader.Find("Hidden/EnviroBlitThrough"));
          
            cmd.SetGlobalTexture("_MainTex", source);
            cmd.SetRenderTarget(target, 0, CubemapFace.Unknown, -1);
            cmd.DrawMesh(RenderingUtils.fullscreenMesh, matrix, blitThroughMat);
        }

#if UNITY_2022_3_OR_NEWER 
        public void CustomBlit(CommandBuffer cmd,RTHandle source, RTHandle target, Material mat)
        {
            Blitter.BlitCameraTexture(cmd,source,target,mat,0);
        } 

        public void CustomBlit(CommandBuffer cmd,RTHandle source, RTHandle target, Material mat, int pass)
        {
            Blitter.BlitCameraTexture(cmd,source,target,mat,pass);
        }

        public void CustomBlit(CommandBuffer cmd,RTHandle source, RTHandle target)
        {
            Blitter.BlitCameraTexture(cmd,source,target);
        }
#endif
#if  UNITY_6000_0_OR_NEWER
        [System.Obsolete]
#endif        
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
        #if UNITY_2022_3_OR_NEWER
            ConfigureTarget(scriptableRenderer.cameraColorTargetHandle);
        #else
            ConfigureTarget(scriptableRenderer.cameraColorTarget);
        #endif
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }
        
 #if  UNITY_6000_0_OR_NEWER
        [System.Obsolete]
#endif        
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if(GetCloudsRenderer(renderingData.cameraData.camera) == null)
            {
               CreateCloudsRenderer(renderingData.cameraData.camera);
            }
        }
 
        private EnviroVolumetricCloudRenderer CreateCloudsRenderer(Camera cam)
        {
            EnviroVolumetricCloudRenderer r = new EnviroVolumetricCloudRenderer();
            r.camera = cam;
            volumetricCloudsRender.Add(r);
            return r;
        }

        private EnviroVolumetricCloudRenderer GetCloudsRenderer(Camera cam)
        {
            for (int i = 0; i < volumetricCloudsRender.Count; i++)
            {
                if(volumetricCloudsRender[i].camera == cam)
                   return volumetricCloudsRender[i];
            }
            return CreateCloudsRenderer(cam);
        }

        private static Matrix4x4 ApplyEnviroStageSkyPitch(Matrix4x4 worldFromView)
        {
            float pitch = Mathf.Clamp(Shader.GetGlobalFloat(EnviroStageSkyPitchId), -18f, 18f);
            if (Mathf.Abs(pitch) < 0.001f)
                return worldFromView;

            return worldFromView * Matrix4x4.Rotate(Quaternion.Euler(-pitch, 0f, 0f));
        }

        private void SetMatrix(Camera myCam)
        {
        #if ENABLE_VR || ENABLE_XR_MODULE
            if (UnityEngine.XR.XRSettings.enabled && UnityEngine.XR.XRSettings.stereoRenderingMode == UnityEngine.XR.XRSettings.StereoRenderingMode.SinglePassInstanced && myCam.stereoEnabled) 
            {
                // Both stereo eye inverse view matrices
                Matrix4x4 left_world_from_view = myCam.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse;
                Matrix4x4 right_world_from_view = myCam.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse;

                // Both stereo eye inverse projection matrices, plumbed through GetGPUProjectionMatrix to compensate for render texture
                Matrix4x4 left_screen_from_view = myCam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                Matrix4x4 right_screen_from_view = myCam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
                Matrix4x4 left_view_from_screen = GL.GetGPUProjectionMatrix(left_screen_from_view, true).inverse;
                Matrix4x4 right_view_from_screen = GL.GetGPUProjectionMatrix(right_screen_from_view, true).inverse;

                // Negate [1,1] to reflect Unity's CBuffer state
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3)
                {
                    left_view_from_screen[1, 1] *= -1;
                    right_view_from_screen[1, 1] *= -1;
                }

                Shader.SetGlobalMatrix("_LeftWorldFromView", ApplyEnviroStageSkyPitch(left_world_from_view));
                Shader.SetGlobalMatrix("_RightWorldFromView", ApplyEnviroStageSkyPitch(right_world_from_view));
                Shader.SetGlobalMatrix("_LeftViewFromScreen", left_view_from_screen);
                Shader.SetGlobalMatrix("_RightViewFromScreen", right_view_from_screen);
            }
            else
            {
                // Main eye inverse view matrix
                Matrix4x4 left_world_from_view = myCam.cameraToWorldMatrix;

                // Inverse projection matrices, plumbed through GetGPUProjectionMatrix to compensate for render texture
                Matrix4x4 screen_from_view = myCam.projectionMatrix;
                Matrix4x4 left_view_from_screen = GL.GetGPUProjectionMatrix(screen_from_view, true).inverse;

                // Negate [1,1] to reflect Unity's CBuffer state
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3)
                    left_view_from_screen[1, 1] *= -1;

                Shader.SetGlobalMatrix("_LeftWorldFromView", ApplyEnviroStageSkyPitch(left_world_from_view));
                Shader.SetGlobalMatrix("_LeftViewFromScreen", left_view_from_screen);
            } 
        #else
                // Main eye inverse view matrix
                Matrix4x4 left_world_from_view = myCam.cameraToWorldMatrix;

                // Inverse projection matrices, plumbed through GetGPUProjectionMatrix to compensate for render texture
                Matrix4x4 screen_from_view = myCam.projectionMatrix;
                Matrix4x4 left_view_from_screen = GL.GetGPUProjectionMatrix(screen_from_view, true).inverse;

                // Negate [1,1] to reflect Unity's CBuffer state
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3)
                    left_view_from_screen[1, 1] *= -1;

                Shader.SetGlobalMatrix("_LeftWorldFromView", ApplyEnviroStageSkyPitch(left_world_from_view));
                Shader.SetGlobalMatrix("_LeftViewFromScreen", left_view_from_screen);
            #endif
        } 

        #if  UNITY_6000_0_OR_NEWER
        [System.Obsolete]
        #endif        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
           if(EnviroManager.instance == null || !EnviroManager.instance.gameObject.activeSelf)
               return; 
                
            CommandBuffer cmd = CommandBufferPool.Get(pName);

            if(EnviroHelper.ResetMatrix(renderingData.cameraData.camera))
                renderingData.cameraData.camera.ResetProjectionMatrix();

            EnviroQuality myQuality = EnviroHelper.GetQualityForCamera(renderingData.cameraData.camera);

            //Set what to render on this camera.
            bool renderVolumetricClouds = false;
            bool renderFog = false;

            if(EnviroManager.instance.Quality != null)
            {
                if(EnviroManager.instance.VolumetricClouds != null)
                    renderVolumetricClouds = myQuality.volumetricCloudsOverride.volumetricClouds;  

                if(EnviroManager.instance.Fog != null)
                    renderFog = myQuality.fogOverride.fog;  
            }
            else
            {
                if(EnviroManager.instance.VolumetricClouds != null)
                    renderVolumetricClouds = EnviroManager.instance.VolumetricClouds.settingsQuality.volumetricClouds;

                if(EnviroManager.instance.Fog != null)
                    renderFog = EnviroManager.instance.Fog.Settings.fog;
            }

            if (EnviroManager.instance.Objects.worldAnchor != null) 
                floatingPointOriginMod = EnviroManager.instance.Objects.worldAnchor.transform.position;
            else
                floatingPointOriginMod = Vector3.zero; 

            //Set some global matrixes used for all the enviro effects.
            SetMatrix(renderingData.cameraData.camera);
 
            //Create temporary texture and blit the camera content.
            RenderTexture sourceTemp = RenderTexture.GetTemporary(renderingData.cameraData.cameraTargetDescriptor);

    #if UNITY_2022_3_OR_NEWER 
            RenderTargetIdentifier cameraColorTarget = scriptableRenderer.cameraColorTargetHandle.nameID;
    #else
            RenderTargetIdentifier cameraColorTarget = scriptableRenderer.cameraColorTarget;      
    #endif   

            CustomBlit(cmd, Matrix4x4.identity,cameraColorTarget, new RenderTargetIdentifier(sourceTemp)); 

            //Render volumetrics mask first
            if(EnviroManager.instance.Fog != null && renderFog)
               EnviroManager.instance.Fog.RenderVolumetricsURP(renderingData.cameraData.camera,this,cmd,sourceTemp);

            if(!renderVolumetricClouds)
            Shader.SetGlobalTexture("_EnviroClouds", Texture2D.blackTexture);

            if(EnviroManager.instance.Fog != null && EnviroManager.instance.VolumetricClouds != null && renderVolumetricClouds && renderFog)
            { 
                RenderTexture temp1 = RenderTexture.GetTemporary(renderingData.cameraData.cameraTargetDescriptor);

                if(renderingData.cameraData.camera.transform.position.y - floatingPointOriginMod.y  < EnviroManager.instance.VolumetricClouds.settingsVolume.bottomCloudsHeight)
                { 
                    EnviroVolumetricCloudRenderer renderer = GetCloudsRenderer(renderingData.cameraData.camera);
                    EnviroManager.instance.VolumetricClouds.RenderVolumetricCloudsURP(renderingData,this,cmd, sourceTemp, temp1, renderer, myQuality);

                    if(EnviroManager.instance.VolumetricClouds.settingsGlobal.cloudShadows && renderingData.cameraData.camera.cameraType != CameraType.Reflection)
                    {
                        RenderTexture temp2 = RenderTexture.GetTemporary(renderingData.cameraData.cameraTargetDescriptor);
                        EnviroManager.instance.VolumetricClouds.RenderCloudsShadowsURP(this,renderingData.cameraData.camera,cmd,temp1,temp2,renderer);
                        EnviroManager.instance.Fog.RenderHeightFogURP(renderingData.cameraData.camera,this,cmd,temp2,cameraColorTarget);
                        RenderTexture.ReleaseTemporary(temp2);
                    }
                    else
                    {
                        EnviroManager.instance.Fog.RenderHeightFogURP(renderingData.cameraData.camera,this,cmd,temp1,cameraColorTarget);
                    }
                }
                else
                { 
                    EnviroManager.instance.Fog.RenderHeightFogURP(renderingData.cameraData.camera,this,cmd,sourceTemp,temp1);
                    EnviroVolumetricCloudRenderer renderer = GetCloudsRenderer(renderingData.cameraData.camera);
                  
                    
                    if(EnviroManager.instance.VolumetricClouds.settingsGlobal.cloudShadows && renderingData.cameraData.camera.cameraType != CameraType.Reflection)
                    {
                        RenderTexture temp2 = RenderTexture.GetTemporary(renderingData.cameraData.cameraTargetDescriptor);
                        EnviroManager.instance.VolumetricClouds.RenderCloudsShadowsURP(this,renderingData.cameraData.camera,cmd,temp1,temp2,renderer);
                        EnviroManager.instance.VolumetricClouds.RenderVolumetricCloudsURP(renderingData,this,cmd, temp2, cameraColorTarget, renderer, myQuality);
                        RenderTexture.ReleaseTemporary(temp2);
                    }
                    else
                    {
                        EnviroManager.instance.VolumetricClouds.RenderVolumetricCloudsURP(renderingData,this,cmd, temp1, cameraColorTarget, renderer, myQuality);
                    }  
                }

                context.ExecuteCommandBuffer(cmd);
                RenderTexture.ReleaseTemporary(temp1);
            }
            else if(EnviroManager.instance.VolumetricClouds != null && renderVolumetricClouds && !renderFog)
            {
                EnviroVolumetricCloudRenderer renderer = GetCloudsRenderer(renderingData.cameraData.camera);
                  
                if(EnviroManager.instance.VolumetricClouds.settingsGlobal.cloudShadows && renderingData.cameraData.camera.cameraType != CameraType.Reflection)
                {
                    RenderTexture temp1 = RenderTexture.GetTemporary(renderingData.cameraData.cameraTargetDescriptor);
                    EnviroManager.instance.VolumetricClouds.RenderCloudsShadowsURP(this,renderingData.cameraData.camera,cmd,sourceTemp,temp1,renderer);
                    EnviroManager.instance.VolumetricClouds.RenderVolumetricCloudsURP(renderingData,this,cmd, temp1, cameraColorTarget, renderer, myQuality);
                    RenderTexture.ReleaseTemporary(temp1); 
                }
                else
                {
                     EnviroManager.instance.VolumetricClouds.RenderVolumetricCloudsURP(renderingData,this,cmd, sourceTemp, cameraColorTarget, renderer, myQuality);
                } 
                context.ExecuteCommandBuffer(cmd); 
                
            } 
            else if (Enviro.EnviroManager.instance.Fog != null && renderFog)
            {
                EnviroManager.instance.Fog.RenderHeightFogURP(renderingData.cameraData.camera,this,cmd,sourceTemp,cameraColorTarget);
                context.ExecuteCommandBuffer(cmd);
            }
            else
            {
                //Render Nothing
            }

            if(!renderVolumetricClouds)
                Shader.SetGlobalTexture("_EnviroClouds", Texture2D.blackTexture);

            //Release source temp render texture
            CommandBufferPool.Release(cmd);
            RenderTexture.ReleaseTemporary(sourceTemp);
        }
        #endif
    }
    
}
#endif
