using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Enviro
{
    public class EnviroConfiguration : ScriptableObject
    {
        public string version = "";
        public EnviroTimeModule timeModule;
        public EnviroLightingModule lightingModule;
        public EnviroReflectionsModule reflectionsModule;
        public EnviroSkyModule Sky;
        public EnviroFogModule fogModule;
        public EnviroVolumetricCloudsModule volumetricCloudModule;
        public EnviroFlatCloudsModule flatCloudModule;
        public EnviroWeatherModule Weather;
        public EnviroAuroraModule Aurora;
        public EnviroAudioModule Audio;
        public EnviroEffectsModule Effects;
        public EnviroLightningModule Lightning;
        public EnviroQualityModule Quality;
        public EnviroEnvironmentModule Environment;
    }

    public class EnviroConfigurationCreation
    {
        #if UNITY_EDITOR
        [UnityEditor.MenuItem("Assets/Create/Enviro3/Configuration")]
        #endif
        public static EnviroConfiguration CreateMyAsset()
        {
            EnviroConfiguration config = ScriptableObject.CreateInstance<EnviroConfiguration>();
            config.version = "3.3.0";
            #if UNITY_EDITOR
            // Create and save the new profile with unique name
            string path = UnityEditor.AssetDatabase.GetAssetPath (UnityEditor.Selection.activeObject);
            if (path == "")
            {
                path = "Assets/Enviro 3 - Sky and Weather";
            }

            path = System.IO.Path.GetDirectoryName(path);
            if(!System.IO.Directory.Exists(path))
            System.IO.Directory.CreateDirectory(path);
            
            string assetPathAndName = UnityEditor.AssetDatabase.GenerateUniqueAssetPath (path + "/New " + "Enviro Configuration" + ".asset");
            UnityEditor.AssetDatabase.CreateAsset (config, assetPathAndName);
            UnityEditor.AssetDatabase.SaveAssets ();
            UnityEditor.AssetDatabase.Refresh();
            #endif
            return config;
        }
    }
}
