using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


namespace Enviro 
{
    [Serializable]
    public class EnviroEffectTypes
    {
#if ENVIRO_VFXGRAPH
        public UnityEngine.VFX.VisualEffect myVFXGraph;
#endif
        public GameObject prefabVFXGraph;
        public Vector3 localPositionOffsetVFXGraph;
        public Vector3 localRotationOffsetVFXGraph;
        public float maxEmissionVFXGraph; 

        public ParticleSystem mySystem; 
        public string name;
        public GameObject prefab;
        public Vector3 localPositionOffset;
        public Vector3 localRotationOffset;
        public float emissionRate = 0f;
        public float maxEmission; 
    }

    [Serializable]
    public class EnviroEffects
    {
        public enum EnviroEffectSystemType 
        {
            ParticleSystem,
            VFXGraph,
            Both
        } 

        public EnviroEffectSystemType enviroEffectSystemType = EnviroEffectSystemType.VFXGraph;
        public List<EnviroEffectTypes> effectTypes = new List<EnviroEffectTypes>();
        [Range(0f,2f)]
        public float particeEmissionRateModifier = 1f;
    }
 
    [Serializable]
    [ExecuteInEditMode]
    public class EnviroEffectsModule : EnviroModule
    {  
        public Enviro.EnviroEffects Settings;
        public EnviroEffectsModule preset;

        //Inspector
        public bool showSetupControls;
        public bool showEmissionControls;
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

        private void Setup()
        {
            if(!active)
               return; 

            CreateEffects();
        }  
 
        private void Cleanup()
        {
            if(EnviroManager.instance.Objects.effects != null)
               DestroyImmediate(EnviroManager.instance.Objects.effects);
        }

        public override void UpdateModule ()
        { 
            UpdateEffects();
        }

        public void CreateEffects() 
        {
            if(EnviroManager.instance.Objects.effects != null)
            {
               EnviroHelper.DestroyExtended(EnviroManager.instance.Objects.effects);
               EnviroManager.instance.Objects.effects = null;
            }
     

            if(EnviroManager.instance.Objects.effects == null)
            {
                EnviroManager.instance.Objects.effects = new GameObject();
                EnviroManager.instance.Objects.effects.hideFlags = HideFlags.DontSave;
                EnviroManager.instance.Objects.effects.name = "Effects";
                EnviroManager.instance.Objects.effects.transform.SetParent(EnviroManager.instance.transform);
                EnviroManager.instance.Objects.effects.transform.localPosition = Vector3.zero;
            }

            for(int i = 0; i < Settings.effectTypes.Count; i++)
            {
                if(Settings.effectTypes[i].mySystem != null)
                {
                     EnviroHelper.DestroyExtended(Settings.effectTypes[i].mySystem.gameObject);
                    Settings.effectTypes[i].mySystem = null;
                }  

            
               

                GameObject sys;
                  
                if(Settings.effectTypes[i].prefab != null)
                {
                   sys = Instantiate(Settings.effectTypes[i].prefab,Settings.effectTypes[i].localPositionOffset,Quaternion.identity);
                   sys.transform.SetParent(EnviroManager.instance.Objects.effects.transform);
                   sys.name = Settings.effectTypes[i].name + " Particle System";
                   sys.transform.localPosition = Settings.effectTypes[i].localPositionOffset;
                   sys.transform.localEulerAngles = Settings.effectTypes[i].localRotationOffset;
                   Settings.effectTypes[i].mySystem = sys.GetComponent<ParticleSystem>();
               
                   if(Settings.effectTypes[i].emissionRate <= 0f)
                   {
                        Settings.effectTypes[i].mySystem.Clear();
                        Settings.effectTypes[i].mySystem.Stop();
                   } 
                      
                } 

                #if ENVIRO_VFXGRAPH
                if(Settings.effectTypes[i].prefabVFXGraph != null)
                {
                   sys = Instantiate(Settings.effectTypes[i].prefabVFXGraph,Settings.effectTypes[i].localPositionOffsetVFXGraph,Quaternion.identity);
                   sys.transform.SetParent(EnviroManager.instance.Objects.effects.transform);
                   sys.name = Settings.effectTypes[i].name + " VFX Graph";
                   sys.transform.localPosition = Settings.effectTypes[i].localPositionOffsetVFXGraph;
                   sys.transform.localEulerAngles = Settings.effectTypes[i].localRotationOffsetVFXGraph;
                   Settings.effectTypes[i].myVFXGraph = sys.GetComponent<UnityEngine.VFX.VisualEffect>();
                    
                   if(Settings.effectTypes[i].emissionRate <= 0f)
                   Settings.effectTypes[i].myVFXGraph.Stop(); 
                }
                #endif
            }
        }

        public float GetEmissionRate(ParticleSystem system)
        {
            return system.emission.rateOverTime.constantMax;
        }

        public void SetEmissionRate(ParticleSystem sys, float emissionRate)
        {
            var emission = sys.emission;
            var rate = emission.rateOverTime;
            rate.constantMax = emissionRate;
            emission.rateOverTime = rate;
        }

 #if ENVIRO_VFXGRAPH
        public float GetEmissionRate(UnityEngine.VFX.VisualEffect system)
        {
            return system.GetFloat("Emission Rate");
        }


        public void SetEmissionRate(UnityEngine.VFX.VisualEffect system, float emissionRate)
        {
            system.SetFloat("Emission Rate", emissionRate);

        
            if(EnviroManager.instance.Environment != null)
            {
                system.SetVector3("Wind",new UnityEngine.Vector3 (EnviroManager.instance.Environment.Settings.windDirectionX,0f,EnviroManager.instance.Environment.Settings.windDirectionY));
            }     
        }
#endif 
        private void UpdateEffects()
        {
            Shader.SetGlobalFloat("_EnviroLightIntensity", EnviroManager.instance.solarTime);

            
            for(int i = 0; i < Settings.effectTypes.Count; i++)
            {
            #if ENVIRO_VFXGRAPH
            if(Settings.enviroEffectSystemType == EnviroEffects.EnviroEffectSystemType.VFXGraph)
            {
                if(Settings.effectTypes[i].myVFXGraph != null) 
                {
                        float currentEmission = Settings.effectTypes[i].maxEmissionVFXGraph * Settings.effectTypes[i].emissionRate * Settings.particeEmissionRateModifier;

                        SetEmissionRate(Settings.effectTypes[i].myVFXGraph,currentEmission);

                        if(currentEmission > 0f && Settings.effectTypes[i].myVFXGraph.aliveParticleCount < 1)
                           Settings.effectTypes[i].myVFXGraph.Play();  
                }

                if(Settings.effectTypes[i].mySystem != null)
                {
                    SetEmissionRate(Settings.effectTypes[i].mySystem,0f);

                    if(Settings.effectTypes[i].mySystem.isPlaying)
                       Settings.effectTypes[i].mySystem.Stop();
                }

            } 
            else if(Settings.enviroEffectSystemType == EnviroEffects.EnviroEffectSystemType.ParticleSystem)
            {

                if(Settings.effectTypes[i].mySystem != null)
                {
                    float currentEmission = Settings.effectTypes[i].maxEmission * Settings.effectTypes[i].emissionRate * Settings.particeEmissionRateModifier;

                    SetEmissionRate(Settings.effectTypes[i].mySystem,currentEmission);

                    if(currentEmission > 0f && !Settings.effectTypes[i].mySystem.isPlaying)
                        Settings.effectTypes[i].mySystem.Play();
                }

                if(Settings.effectTypes[i].myVFXGraph != null) 
                {
                     SetEmissionRate(Settings.effectTypes[i].myVFXGraph,0f);

                     if (Settings.effectTypes[i].myVFXGraph.aliveParticleCount > 0)
                         Settings.effectTypes[i].myVFXGraph.Stop(); 
                }

            }
            else
            {
                if(Settings.effectTypes[i].myVFXGraph != null) 
                {
                    float currentEmission = Settings.effectTypes[i].maxEmissionVFXGraph * Settings.effectTypes[i].emissionRate * Settings.particeEmissionRateModifier;

                    SetEmissionRate(Settings.effectTypes[i].myVFXGraph,currentEmission);

                    if(currentEmission > 0f && Settings.effectTypes[i].myVFXGraph.aliveParticleCount < 1)
                        Settings.effectTypes[i].myVFXGraph.Play(); 
                }

                if(Settings.effectTypes[i].mySystem != null)
                {
                    float currentEmission = Settings.effectTypes[i].maxEmission * Settings.effectTypes[i].emissionRate * Settings.particeEmissionRateModifier;

                    SetEmissionRate(Settings.effectTypes[i].mySystem,currentEmission);

                    if(currentEmission > 0f && !Settings.effectTypes[i].mySystem.isPlaying)
                    Settings.effectTypes[i].mySystem.Play();
                }
            }

        #else
            if(Settings.effectTypes[i].mySystem != null)
            {
                float currentEmission = Settings.effectTypes[i].maxEmission * Settings.effectTypes[i].emissionRate * Settings.particeEmissionRateModifier;

                SetEmissionRate(Settings.effectTypes[i].mySystem,currentEmission);

                if(currentEmission > 0f && !Settings.effectTypes[i].mySystem.isPlaying)
                Settings.effectTypes[i].mySystem.Play();
            }
        #endif   
            }
        }


        //Save and Load
        public void LoadModuleValues ()
        {
            if(preset != null)
            {
                Settings = JsonUtility.FromJson<Enviro.EnviroEffects>(JsonUtility.ToJson(preset.Settings));
            } 
            else
            {
                Debug.Log("Please assign a saved module to load from!");
            }
        }

        public void SaveModuleValues ()
        {
#if UNITY_EDITOR
        EnviroEffectsModule t =  ScriptableObject.CreateInstance<EnviroEffectsModule>();
        t.name = "Effects Module";
        t.Settings = JsonUtility.FromJson<Enviro.EnviroEffects>(JsonUtility.ToJson(Settings));
 
        string assetPathAndName = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(EnviroHelper.assetPath + "/New " + t.name + ".asset");
        UnityEditor.AssetDatabase.CreateAsset(t, assetPathAndName);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
#endif
        }

        public void SaveModuleValues (EnviroEffectsModule module)
        {
            module.Settings = JsonUtility.FromJson<Enviro.EnviroEffects>(JsonUtility.ToJson(Settings));
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(module);
            UnityEditor.AssetDatabase.SaveAssets();
            #endif
        }
    }
}