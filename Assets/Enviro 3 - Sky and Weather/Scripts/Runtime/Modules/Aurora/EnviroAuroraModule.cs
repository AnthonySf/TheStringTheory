using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Enviro
{
    [Serializable]
    public class EnviroAurora
    { 
        public bool useAurora = true;
        [Header("Aurora Intensity")]
        [Range(0f,1f)]
        public float auroraIntensityModifier = 1f;
        public AnimationCurve auroraIntensity = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 0.1f), new Keyframe(1f, 0f));
 
        // 
        [Header("Aurora Color and Brightness")]
        public Color auroraColor = new Color(0.1f, 0.5f, 0.7f);
        public float auroraBrightness = 75f;
        public float auroraContrast = 10f;
        // 
        [Header("Aurora Height and Scale")]
        public float auroraHeight = 20000f;
        [Range(0f, 0.025f)]
        public float auroraScale = 0.01f;
        //
        [Header("Aurora Performance")]
        [Range(8, 32)]
        public int auroraSteps = 20;
        //
        [Header("Aurora Modelling and Animation")]
        public Vector4 auroraLayer1Settings = new Vector4(0.1f, 0.1f, 0f, 0.5f);
        public Vector4 auroraLayer2Settings = new Vector4(5f, 5f, 0f, 0.5f);
        public Vector4 auroraColorshiftSettings = new Vector4(0.05f, 0.05f, 0f, 5f);
        [Range(0f, 0.1f)]
        public float auroraSpeed = 0.005f;
        [Header("Aurora Textures")]
        public Texture2D aurora_layer_1;
        public Texture2D aurora_layer_2;
        public Texture2D aurora_colorshift;
    } 

    [Serializable]
    public class EnviroAuroraModule : EnviroModule
    {  
        public Enviro.EnviroAurora Settings;
        public EnviroAuroraModule preset;
        public bool showAuroraControls;

        
        // Update Method
        public override void UpdateModule ()
        {  

            if(!active)
               return; 

            if(EnviroManager.instance == null)
               return;

            if(EnviroManager.instance.Sky != null)
            {
                UpdateAuroraShader();
            }
        }

        public void UpdateAuroraShader ()
        {
            if(!Settings.useAurora)
            {
                Shader.SetGlobalFloat("_Aurora", 0f);
                return;
            } 
             else
                Shader.SetGlobalFloat("_Aurora", 1f);

            if (Settings.aurora_layer_1 != null)
                Shader.SetGlobalTexture("_Aurora_Layer_1", Settings.aurora_layer_1);

            if (Settings.aurora_layer_2 != null)
                Shader.SetGlobalTexture("_Aurora_Layer_2", Settings.aurora_layer_2);

            if (Settings.aurora_colorshift != null)
                Shader.SetGlobalTexture("_Aurora_Colorshift", Settings.aurora_colorshift);

            Shader.SetGlobalFloat("_AuroraIntensity", Mathf.Clamp01(Settings.auroraIntensityModifier * Settings.auroraIntensity.Evaluate(EnviroManager.instance.solarTime)));
            Shader.SetGlobalFloat("_AuroraBrightness", Settings.auroraBrightness);
            Shader.SetGlobalFloat("_AuroraContrast", Settings.auroraContrast);
            Shader.SetGlobalColor("_AuroraColor", Settings.auroraColor);
            Shader.SetGlobalFloat("_AuroraHeight", Settings.auroraHeight);
            Shader.SetGlobalFloat("_AuroraScale", Settings.auroraScale);
            Shader.SetGlobalFloat("_AuroraSpeed", Settings.auroraSpeed);
            Shader.SetGlobalFloat("_AuroraSteps", Settings.auroraSteps);
            Shader.SetGlobalFloat("_AuroraSteps", Settings.auroraSteps);
            Shader.SetGlobalVector("_Aurora_Tiling_Layer1", Settings.auroraLayer1Settings);
            Shader.SetGlobalVector("_Aurora_Tiling_Layer2", Settings.auroraLayer2Settings);
            Shader.SetGlobalVector("_Aurora_Tiling_ColorShift", Settings.auroraColorshiftSettings);
        }

        //Save and Load
        public void LoadModuleValues ()
        {
            if(preset != null)
            {
                Settings = JsonUtility.FromJson<Enviro.EnviroAurora>(JsonUtility.ToJson(preset.Settings));
            }
            else
            {
                Debug.Log("Please assign a saved module to load from!");
            }
        } 

        public void SaveModuleValues ()
        {
#if UNITY_EDITOR
        EnviroAuroraModule t =  ScriptableObject.CreateInstance<EnviroAuroraModule>();
        t.name = "Aurora Preset";
        t.Settings = JsonUtility.FromJson<Enviro.EnviroAurora>(JsonUtility.ToJson(Settings));
 
        string assetPathAndName = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(EnviroHelper.assetPath + "/New " + t.name + ".asset");
        UnityEditor.AssetDatabase.CreateAsset(t, assetPathAndName);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
#endif
        }
        public void SaveModuleValues (EnviroAuroraModule module)
        {
            module.Settings = JsonUtility.FromJson<Enviro.EnviroAurora>(JsonUtility.ToJson(Settings));

            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(module);
            UnityEditor.AssetDatabase.SaveAssets();
            #endif
        }
    }
}