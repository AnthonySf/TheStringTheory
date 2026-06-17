using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
 

namespace Enviro
{
	[Serializable]   
	public class EnviroSkyQualitySettings
	{
		public EnviroSky.SkyMode skyMode = EnviroSky.SkyMode.Normal;
	}
 
	[Serializable]   
	public class EnviroVolumetricCloudsQualitySettings
	{
		public bool volumetricClouds = true;
		public bool lightningSupport = true;
        public bool variableBottomNoise = false;

        public int downsampling = 4;
		public int stepsLayer1 = 128;
        public float blueNoiseIntensity = 1f;
		public float reprojectionBlendTime = 10f; 
		public float lodDistance = 0.25f;
	} 

	[Serializable]   
	public class EnviroFlatCloudsQualitySettings
	{
      public bool cirrusClouds = true;
	  public bool flatClouds = true;
	  public int flatCloudsShadowSteps = 8;
	}

	[Serializable]   
	public class EnviroAuroraQualitySettings
	{
      public bool aurora = true;
	  [Range(6,32)]
	  public int steps = 32;
	}

	[Serializable]   
	public class EnviroEffectsQualitySettings
	{
	  [Range(0f,2f)]
	  public float particeEmissionRateModifier = 1f;
	}

	[Serializable]   
	public class EnviroFogQualitySettings
	{
		public bool fog = true; 
		public EnviroFogSettings.FogQualityMode fogQualityMode = EnviroFogSettings.FogQualityMode.Normal;
		public bool volumetrics = true;
		public bool unityFog = false;
		public EnviroFogSettings.Quality quality;
		[Range(16,96)]
		public int steps = 32;
	}

	[Serializable]  
	public class EnviroQuality : ScriptableObject
	{
		//Inspector 
		public bool showEditor, showSky, showVolumeClouds, showFog, showFlatClouds, showEffects, showAurora;
		public EnviroSkyQualitySettings skyOverride;
		public EnviroVolumetricCloudsQualitySettings volumetricCloudsOverride;
		public EnviroFogQualitySettings fogOverride;
		public EnviroFlatCloudsQualitySettings flatCloudsOverride;
		public EnviroAuroraQualitySettings auroraOverride;
		public EnviroEffectsQualitySettings effectsOverride;
	}


	public class EnviroQualityCreation 
	{
		#if UNITY_EDITOR
		[UnityEditor.MenuItem("Assets/Create/Enviro3/Quality")]
		#endif 
		public static EnviroQuality CreateMyAsset()
		{
			EnviroQuality wpreset = ScriptableObject.CreateInstance<EnviroQuality>();
		#if UNITY_EDITOR
			// Create and save the new profile with unique name
			string path = UnityEditor.AssetDatabase.GetAssetPath (UnityEditor.Selection.activeObject);
			if (path == "")  
			{
				path = EnviroHelper.assetPath;
			} 
			string assetPathAndName = UnityEditor.AssetDatabase.GenerateUniqueAssetPath (path + "/New " + "Quality" + ".asset");
			UnityEditor.AssetDatabase.CreateAsset (wpreset, assetPathAndName);
			UnityEditor.AssetDatabase.SaveAssets ();
			UnityEditor.AssetDatabase.Refresh();
		#endif
			return wpreset;
		}
	}
}
