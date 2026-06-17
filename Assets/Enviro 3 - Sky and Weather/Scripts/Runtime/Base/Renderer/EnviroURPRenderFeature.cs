#if ENVIRO_URP

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;


namespace Enviro
{ 
    public class EnviroURPRenderFeature : ScriptableRendererFeature
    { 

#if UNITY_6000_0_OR_NEWER
        private EnviroURPRenderGraph graph;
        private EnviroURPRenderPass pass;

        public override void Create()
        {
            #if !UNITY_6000_4_OR_NEWER
            pass = new EnviroURPRenderPass("Enviro Render Pass");
            #endif
            graph = new EnviroURPRenderGraph();
            graph.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents -1;
        } 

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
        #if UNITY_6000_4_OR_NEWER
                if(graph != null && EnviroHelper.CanRenderOnCamera(renderingData.cameraData.camera))
                {
                    renderer.EnqueuePass(graph);
                }
        #else 
            if(UnityEngine.Rendering.GraphicsSettings.GetRenderPipelineSettings< UnityEngine.Rendering.Universal.RenderGraphSettings>().enableRenderCompatibilityMode)
            {
                if(pass != null && EnviroHelper.CanRenderOnCamera(renderingData.cameraData.camera))
                {
                    pass.scriptableRenderer = renderer;
                    renderer.EnqueuePass(pass);
                }
            }
            else
            {
                if(graph != null && EnviroHelper.CanRenderOnCamera(renderingData.cameraData.camera))
                {
                    renderer.EnqueuePass(graph);
                }
            } 
        #endif
        }

#else
        private EnviroURPRenderPass pass;
        
        public override void Create()
        {
            pass = new EnviroURPRenderPass("Enviro Render Pass");
        } 

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if(pass != null && EnviroHelper.CanRenderOnCamera(renderingData.cameraData.camera))
            {
                pass.scriptableRenderer = renderer;
                renderer.EnqueuePass(pass);
            }
        }  

#endif
  
    }
}
#endif
