using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Enviro
{
    [ExecuteInEditMode]
    [AddComponentMenu("Enviro 3/Volumetric Light")]
    public class EnviroVolumetricFogLight : MonoBehaviour
    {
        [Range(0f,2f)]
        public float intensity = 1.0f;
        [Range(0f,2f)]
        public float range = 1.0f;

        private Light myLight;

        private bool initialized = false;
        private CommandBuffer cascadeShadowCB;
        //private CommandBuffer shadowMatrixBuffer;



        public bool isOn
            {
                get
                {
                    if (!isActiveAndEnabled)
                        return false;

                    Init();

                    return myLight.enabled;
                }

                private set{}
            }
    
        new public Light light {get{Init(); return myLight;} private set{}}
 

        void OnEnable()
        { 
            Init();
            if(EnviroManager.instance != null && EnviroManager.instance.Fog != null)
               AddToLightManager();
        }

        void OnDisable() 
        {
        #if !ENVIRO_URP && !ENVIRO_HDRP
            if(cascadeShadowCB != null && myLight != null && myLight.type == LightType.Directional)
               myLight.RemoveCommandBuffer(LightEvent.AfterShadowMap, cascadeShadowCB);
        #endif
           // if(shadowMatrixBuffer != null && myLight != null && myLight.type == LightType.Directional)
           //    myLight.RemoveCommandBuffer(LightEvent.BeforeScreenspaceMask, shadowMatrixBuffer);

            if(EnviroManager.instance != null && EnviroManager.instance.Fog != null)
               RemoveFromLightManager();
        }

        void AddToLightManager()
        {
           bool addedToMgr = false;

           for(int i = 0; i < EnviroManager.instance.Fog.fogLights.Count; i++)
           {
               if(EnviroManager.instance.Fog.fogLights[i] == this)
               {
                    addedToMgr = true;
                    break;
               }
           }

           if(!addedToMgr)
              EnviroManager.instance.Fog.AddLight(this);
        }
            
        void RemoveFromLightManager()
        {
            for(int i = 0; i < EnviroManager.instance.Fog.fogLights.Count; i++)
            {
               if(EnviroManager.instance.Fog.fogLights[i] == this)
               {
                 EnviroManager.instance.Fog.RemoveLight(this);   
                 initialized = false; 
               }
           }
        } 

 
        private void Init()
        { 
            if (initialized)
                return;

            myLight = GetComponent<Light>();
            
    #if !ENVIRO_URP && !ENVIRO_HDRP
            if(myLight.type == LightType.Directional) 
            {
                cascadeShadowCB = new CommandBuffer();
                cascadeShadowCB.name = "Dir Light Command Buffer";
                cascadeShadowCB.SetGlobalTexture("_CascadeShadowMapTexture", new UnityEngine.Rendering.RenderTargetIdentifier(UnityEngine.Rendering.BuiltinRenderTextureType.CurrentActive));  
                myLight.AddCommandBuffer(LightEvent.AfterShadowMap, cascadeShadowCB);
                
                //shadowMatrixBuffer = new CommandBuffer();  
                //shadowMatrixBuffer.name = "Extract Shadow Matrix Buffer";  
                //myLight.AddCommandBuffer(LightEvent.BeforeShadowMap, shadowMatrixBuffer); 
            } 
    #endif

            initialized = true;
        }
    }
}
