#if ENVIRO_HDRP
using System.Collections;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.HighDefinition  
{
    class EnviroHDRPSkyRenderer : SkyRenderer
    {
        Material skyMat; 
        MaterialPropertyBlock m_PropertyBlock = new MaterialPropertyBlock();

        public EnviroHDRPSkyRenderer()
        {
 
        }

 
        public override void Build()
        {
            if(skyMat == null)
               skyMat = CoreUtils.CreateEngineMaterial(Shader.Find("Enviro/HDRP/Sky"));
        }
 
        public override void Cleanup()
        {
            CoreUtils.Destroy(skyMat);
        }
 
        protected override bool Update(BuiltinSkyParameters builtinParams)
        {
            return false;
        }

        public override void RenderSky(BuiltinSkyParameters builtinParams, bool renderForCubemap, bool renderSunDisk)
        {
            if (Enviro.EnviroManager.instance == null || Enviro.EnviroManager.instance.Sky == null)
                return;

            if (skyMat == null)
                Build();

            Enviro.EnviroManager.instance.Sky.UpdateSkybox(skyMat);

            if(Enviro.EnviroManager.instance.Sky != null && Enviro.EnviroManager.instance.Lighting != null)
            {
                Shader.SetGlobalColor("_AmbientColorTintHDRP", Enviro.EnviroManager.instance.Lighting.Settings.ambientColorTintHDRP.Evaluate(Enviro.EnviroManager.instance.solarTime));
            }
 
            var enviroSky = builtinParams.skySettings as EnviroHDRPSky;
       
            m_PropertyBlock.SetMatrix("_PixelCoordToViewDirWS", builtinParams.pixelCoordToViewDirMatrix);
            Shader.SetGlobalMatrix("_PixelCoordToViewDirWS", builtinParams.pixelCoordToViewDirMatrix);
            
            Shader.SetGlobalFloat("_EnviroSkyIntensity", GetSkyIntensity(enviroSky, builtinParams.debugSettings)); 
   
            if(Enviro.EnviroManager.instance.Objects.directionalLight != null)
                Enviro.EnviroManager.instance.Objects.directionalLight.transform.position = Vector3.zero;

            if(Enviro.EnviroManager.instance.Objects.additionalDirectionalLight != null)
                Enviro.EnviroManager.instance.Objects.additionalDirectionalLight.transform.position = Vector3.zero;

            if(builtinParams.hdCamera.camera.cameraType == CameraType.Reflection && renderForCubemap)
            {
             //   return;
            }
     
               CoreUtils.DrawFullScreen(builtinParams.commandBuffer, skyMat, m_PropertyBlock, renderForCubemap ? 0 : 1);
            }
        }
    }
#endif
