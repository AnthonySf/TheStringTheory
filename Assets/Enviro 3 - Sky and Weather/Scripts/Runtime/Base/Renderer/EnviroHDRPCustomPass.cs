#if ENVIRO_HDRP
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;

namespace Enviro
{
    class EnviroHDRPCustomPass : CustomPass
    {
        private Material blitTrough;
        private List<EnviroVolumetricCloudRenderer> volumetricCloudsRender = new List<EnviroVolumetricCloudRenderer>();
        private Vector3 floatingPointOriginMod = Vector3.zero;

        private RTHandle sourceHandle;
        private RTHandle temp1Handle;
        private RTHandle temp2Handle;

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            if (blitTrough == null)
                blitTrough = new Material(Shader.Find("Hidden/Enviro/BlitTroughHDRP"));

            // We allocate persistent RTHandles dynamically when Execute runs for the first time
            sourceHandle = null;
            temp1Handle = null;
            temp2Handle = null;
        }
        
        
   public RTHandle ReallocateIfNeeded(RTHandle handle, RenderTextureDescriptor desc, string name)
    {
    bool needsRealloc = handle == null || handle.rt == null;

    if (!needsRealloc)
    {
        var hDesc = handle.rt.descriptor;

        // Compare only the format & dimension
        if (hDesc.graphicsFormat != desc.graphicsFormat ||
            hDesc.dimension != desc.dimension)
        {
            needsRealloc = true;
        }
    }

    if (needsRealloc)
    {
        if (handle != null)
            RTHandles.Release(handle);

        // Allocate with scale = 1, dynamic scaling will adjust automatically
        handle = RTHandles.Alloc(
            Vector2.one,
            colorFormat: desc.graphicsFormat,
            dimension: desc.dimension,
            enableRandomWrite: false,
            useMipMap: desc.useMipMap,
            name: name,
            useDynamicScale: true
        );
    }

    return handle;
}
        protected override void Execute(CustomPassContext ctx)
        {
            HDCamera camera = ctx.hdCamera;

            if (ctx.cameraColorBuffer == null || ctx.cameraColorBuffer.rt == null ||
                camera.camera.cameraType == CameraType.Preview ||
                !EnviroHelper.CanRenderOnCamera(camera.camera) || EnviroManager.instance == null && EnviroManager.instance.configuration != null)
                return;

           
        var desc = ctx.cameraColorBuffer.rt.descriptor;

        // Reallocate only if needed
        sourceHandle = ReallocateIfNeeded(sourceHandle, desc, "Enviro Source");
        temp1Handle = ReallocateIfNeeded(temp1Handle, desc, "Enviro Temp1");

        if (EnviroManager.instance.VolumetricClouds != null && EnviroManager.instance.VolumetricClouds.settingsGlobal.cloudShadows)
            temp2Handle = ReallocateIfNeeded(temp2Handle, desc, "Enviro Temp2");



            // Copy camera buffer to persistent source
            HDUtils.BlitCameraTexture(ctx.cmd, ctx.cameraColorBuffer, sourceHandle);

            // Get quality and flags
            EnviroQuality myQuality = EnviroHelper.GetQualityForCamera(camera.camera);
            bool renderVolumetricClouds = false;
            bool renderFog = false;
            if(myQuality == null)
            {
                renderVolumetricClouds = EnviroManager.instance.VolumetricClouds != null && EnviroManager.instance.VolumetricClouds.settingsQuality.volumetricClouds;
                renderFog = EnviroManager.instance.Fog != null && EnviroManager.instance.Fog.Settings.fog;
            }
            else
            {
                renderVolumetricClouds = EnviroManager.instance.VolumetricClouds != null && myQuality.volumetricCloudsOverride.volumetricClouds;
                renderFog = EnviroManager.instance.Fog != null && myQuality.fogOverride.fog;
            }

            floatingPointOriginMod = EnviroManager.instance.Objects?.worldAnchor != null
                ? EnviroManager.instance.Objects.worldAnchor.transform.position
                : Vector3.zero;

            // Ensure clouds renderer exists
            if (renderVolumetricClouds && GetCloudsRenderer(camera.camera) == null)
                CreateCloudsRenderer(camera.camera);

            SetMatrix(camera.camera);

            EnviroVolumetricCloudRenderer renderer = GetCloudsRenderer(camera.camera);

            if(!renderVolumetricClouds)
            Shader.SetGlobalTexture("_EnviroClouds", Texture2D.blackTexture);

            // ----- Depth-aware render logic -----
            if (renderVolumetricClouds && renderFog)
            {
                if (camera.camera.transform.position.y - floatingPointOriginMod.y < EnviroManager.instance.VolumetricClouds.settingsVolume.bottomCloudsHeight)
                {
                    EnviroManager.instance.VolumetricClouds.RenderVolumetricCloudsHDRP(camera.camera, ctx.cmd, sourceHandle, temp1Handle, renderer, myQuality);

                    if (EnviroManager.instance.VolumetricClouds.settingsGlobal.cloudShadows &&
                        camera.camera.cameraType != CameraType.Reflection)
                    {
                        EnviroManager.instance.VolumetricClouds.RenderCloudsShadowsHDRP(camera.camera, ctx.cmd, temp1Handle, temp2Handle, renderer);
                      
                        EnviroManager.instance.Fog.RenderHeightFogHDRP(camera.camera, ctx.cmd, temp2Handle, ctx.cameraColorBuffer);
                    }
                    else
                    {
                      
                        EnviroManager.instance.Fog.RenderHeightFogHDRP(camera.camera, ctx.cmd, temp1Handle, ctx.cameraColorBuffer);
                    }
                }
                else
                {
                  
                    EnviroManager.instance.Fog.RenderHeightFogHDRP(camera.camera, ctx.cmd, sourceHandle, temp1Handle);

                    if (EnviroManager.instance.VolumetricClouds.settingsGlobal.cloudShadows &&
                        camera.camera.cameraType != CameraType.Reflection)
                    {
                        EnviroManager.instance.VolumetricClouds.RenderCloudsShadowsHDRP(camera.camera, ctx.cmd, temp1Handle, temp2Handle, renderer);
                      
                        EnviroManager.instance.VolumetricClouds.RenderVolumetricCloudsHDRP(camera.camera, ctx.cmd, temp2Handle, ctx.cameraColorBuffer, renderer, myQuality);
                    }
                    else
                    {
                     
                        EnviroManager.instance.VolumetricClouds.RenderVolumetricCloudsHDRP(camera.camera, ctx.cmd, temp1Handle, ctx.cameraColorBuffer, renderer, myQuality);
                    }
                }
            }
            else if (renderVolumetricClouds)
            {
                if (EnviroManager.instance.VolumetricClouds.settingsGlobal.cloudShadows &&
                    camera.camera.cameraType != CameraType.Reflection)
                {
                    EnviroManager.instance.VolumetricClouds.RenderCloudsShadowsHDRP(camera.camera, ctx.cmd, sourceHandle, temp1Handle, renderer);
                 
                    EnviroManager.instance.VolumetricClouds.RenderVolumetricCloudsHDRP(camera.camera, ctx.cmd, temp1Handle, ctx.cameraColorBuffer, renderer, myQuality);
                }
                else
                {
                  
                    EnviroManager.instance.VolumetricClouds.RenderVolumetricCloudsHDRP(camera.camera, ctx.cmd, sourceHandle, ctx.cameraColorBuffer, renderer, myQuality);
                }
            }
            else if (renderFog)
            {
              
                EnviroManager.instance.Fog.RenderHeightFogHDRP(camera.camera, ctx.cmd, sourceHandle, ctx.cameraColorBuffer);
            }
            else
            {
               // blitTrough.SetTexture("_InputTexture", sourceHandle);
               // CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ctx.cameraDepthBuffer, ClearFlag.None);
               // HDUtils.DrawFullScreen(ctx.cmd, blitTrough);
            }

            if (!renderVolumetricClouds)
                Shader.SetGlobalTexture("_EnviroClouds", Texture2D.blackTexture);


     
        }

        protected override void Cleanup()
        {
            if (blitTrough != null) CoreUtils.Destroy(blitTrough);

            if (sourceHandle != null) RTHandles.Release(sourceHandle);
            if (temp1Handle != null) RTHandles.Release(temp1Handle);
            if (temp2Handle != null) RTHandles.Release(temp2Handle);

            for (int i = 0; i < volumetricCloudsRender.Count; i++)
                CleanCloudsRenderer(volumetricCloudsRender[i]);
        }

        // ---- Cloud renderer helpers -----
        private EnviroVolumetricCloudRenderer CreateCloudsRenderer(Camera cam)
        {
            EnviroVolumetricCloudRenderer r = new EnviroVolumetricCloudRenderer();
            r.camera = cam;
            volumetricCloudsRender.Add(r);
            return r;
        }

        private void CleanCloudsRenderer(EnviroVolumetricCloudRenderer renderer)
        {
            if (renderer.fullBuffer != null)
                foreach (var b in renderer.fullBuffer) if (b != null) CoreUtils.Destroy(b);

            if (renderer.undersampleBuffer != null) CoreUtils.Destroy(renderer.undersampleBuffer);
            if (renderer.downsampledDepth != null) CoreUtils.Destroy(renderer.downsampledDepth);
            if (renderer.raymarchMat != null) CoreUtils.Destroy(renderer.raymarchMat);
            if (renderer.reprojectMat != null) CoreUtils.Destroy(renderer.reprojectMat);
            if (renderer.blendAndLightingMat != null) CoreUtils.Destroy(renderer.blendAndLightingMat);
            if (renderer.depthMat != null) CoreUtils.Destroy(renderer.depthMat);
            if (renderer.shadowMat != null) CoreUtils.Destroy(renderer.shadowMat);
        }

        private EnviroVolumetricCloudRenderer GetCloudsRenderer(Camera cam)
        {
            foreach (var r in volumetricCloudsRender)
                if (r.camera == cam) return r;

            return CreateCloudsRenderer(cam);
        }

       private void SetMatrix(Camera myCam)
        {
        #if ENABLE_VR && ENABLE_XR_MODULE
            if (UnityEngine.XR.XRSettings.enabled && UnityEngine.XR.XRSettings.stereoRenderingMode == UnityEngine.XR.XRSettings.StereoRenderingMode.SinglePassInstanced) 
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

                Shader.SetGlobalMatrix("_LeftWorldFromView", left_world_from_view);
                Shader.SetGlobalMatrix("_RightWorldFromView", right_world_from_view);
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

                Shader.SetGlobalMatrix("_LeftWorldFromView", left_world_from_view);
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

                Shader.SetGlobalMatrix("_LeftWorldFromView", left_world_from_view);
                Shader.SetGlobalMatrix("_LeftViewFromScreen", left_view_from_screen);
            #endif
        } 
    }
}
#endif