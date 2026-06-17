using System.Collections;
using System.Collections.Generic; 
using UnityEngine;
using System;

namespace Enviro
{
    /// <summary>
    /// Interface for a lightning effect.
    /// </summary>
    public interface ILightningEffect
    {
        /// <summary>
        /// Casts the bolt.
        /// </summary>
        /// <param name="origin">The starting position of the bolt.</param>
        /// <param name="target">The target position of the bolt.</param>
        void CastBolt(Vector3 origin, Vector3 target);
    }

    [Serializable]
    public class EnviroLightning
    {
        public Lightning prefab;
        public GameObject customLightningEffect;

        public bool lightningStorm = false;
        [Range(1f,60f)]
        public float randomLightingDelay = 10.0f;
        [Range(0f,10000f)]
        public float randomSpawnRange = 5000.0f;
        [Range(0f,10000f)]
        public float randomTargetRange = 5000.0f;

        [Range(0f,10000f)]
        public float cloudsLightningRadius = 2500.0f;
        [Range(.1f,5f)]
        public float cloudsLightningDuration = 1.0f;
    } 

    [Serializable]
    public class EnviroLightningModule : EnviroModule
    {  
        public EnviroLightning Settings;
        public EnviroLightningModule preset;
        public bool showLightningControls;
        private bool spawned = false;

        private float lightningStart = -999f;

        // Update Method
        public override void UpdateModule ()
        { 
            if(!active)
               return; 

            if(Application.isPlaying && Settings.lightningStorm && Settings.prefab != null)
            {
                CastLightningBoltRandom();
            }
        }

        public void CastLightningBolt(Vector3 from, Vector3 to)
        {
            if(Settings.prefab != null)
            {
                GameObject lightningGameObject;
                if (Settings.customLightningEffect != null)
                {
                    lightningGameObject = Instantiate(Settings.customLightningEffect, from, Quaternion.identity);
                }
                else
                {
                    lightningGameObject = Instantiate(Settings.prefab, from, Quaternion.identity).gameObject;
                }

                var lightningEffect = lightningGameObject.GetComponent<ILightningEffect>();
                lightningEffect.CastBolt(from, to);
               

                //Play Thunder SFX with delay if Audio module is used.
                if (EnviroManager.instance.Audio != null)
                {
                    EnviroManager.instance.StartCoroutine(PlayThunderSFX(0.05f));
                }

                if(EnviroManager.instance.VolumetricClouds != null)
                {
                     EnviroManager.instance.StartCoroutine(CloudsFlash(0.25f,from));
                }     
            }
            else
            {
                Debug.Log("Please assign a lightning prefab in your Enviro Ligthning module!");
            }
        }

        public void CastLightningBoltRandom()
        {
            if(!spawned)
            {
                //Calculate some random spawn and target locations.
                Vector2 circlSpawn = UnityEngine.Random.insideUnitCircle * Settings.randomSpawnRange;
                Vector2 circlTarget = UnityEngine.Random.insideUnitCircle * Settings.randomTargetRange;
                float offset = 0f;
                
                if(EnviroManager.instance.Objects.worldAnchor != null)
                offset = EnviroManager.instance.Objects.worldAnchor.transform.position.y;
                
                float altitude = 2000f;
                
                if(EnviroManager.instance.VolumetricClouds != null)
                altitude = Mathf.Max(EnviroManager.instance.VolumetricClouds.settingsVolume.bottomCloudsHeight + 1000f, 1000f);
                
                Vector3 spawnPosition = new Vector3(circlSpawn.x + EnviroManager.instance.transform.position.x, offset + altitude, circlSpawn.y + EnviroManager.instance.transform.position.z);
                Vector3 targetPosition = new Vector3(circlTarget.x + spawnPosition.x,offset,circlTarget.y + spawnPosition.z);
         
                EnviroManager.instance.StartCoroutine(LightningStorm(spawnPosition,targetPosition));
            }
        } 
    
        private IEnumerator LightningStorm(Vector3 spwn, Vector3 targ)
        {
            spawned = true;
            CastLightningBolt(spwn,targ);
            yield return new WaitForSeconds(UnityEngine.Random.Range(Settings.randomLightingDelay, Settings.randomLightingDelay * 2f));  
            spawned = false;
        }

        private IEnumerator PlayThunderSFX(float delay)
        {
            yield return new WaitForSeconds(delay);
            EnviroManager.instance.Audio.PlayRandomThunderSFX();
        }
        private IEnumerator CloudsFlash(float delay, Vector3 from)
        {
            yield return new WaitForSeconds(delay);
            //Clouds  
            lightningStart = Time.time;
            
            // Pass to shader each frame
            Shader.SetGlobalVector("_LightningCenter", from);
            Shader.SetGlobalFloat("_LightningRadius", Settings.cloudsLightningRadius);
            Shader.SetGlobalFloat("_LightningStart", lightningStart);
            Shader.SetGlobalFloat("_LightningDuration", Settings.cloudsLightningDuration);
        }

        //Save and Load
        public void LoadModuleValues ()
        {
            if(preset != null)
            {
                Settings = JsonUtility.FromJson<Enviro.EnviroLightning>(JsonUtility.ToJson(preset.Settings));
            }
            else
            {
                Debug.Log("Please assign a saved module to load from!");
            } 
        }

        public void SaveModuleValues ()
        {
#if UNITY_EDITOR
        EnviroLightningModule t =  ScriptableObject.CreateInstance<EnviroLightningModule>();
        t.name = "Lightning Preset";
        t.Settings = JsonUtility.FromJson<Enviro.EnviroLightning>(JsonUtility.ToJson(Settings));
 
        string assetPathAndName = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(EnviroHelper.assetPath + "/New " + t.name + ".asset");
        UnityEditor.AssetDatabase.CreateAsset(t, assetPathAndName);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
#endif
        }
        public void SaveModuleValues (EnviroLightningModule module)
        {
            module.Settings = JsonUtility.FromJson<Enviro.EnviroLightning>(JsonUtility.ToJson(Settings));

            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(module);
            UnityEditor.AssetDatabase.SaveAssets();
            #endif
        }
    }
}