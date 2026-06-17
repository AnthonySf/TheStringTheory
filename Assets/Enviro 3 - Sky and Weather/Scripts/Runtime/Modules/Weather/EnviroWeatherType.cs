using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
  

namespace Enviro
{
	[Serializable] 
	public class EnviroWeatherTypeCloudsOverride
	{ 
		public float ambientLightIntensity = 1f;
		public float coverage = 0f;
		public float dilateCoverage = 0.5f;
		public float dilateType = 0.5f; 
		public float typeModifier = 0.5f;
		public float cloudTypeShaping = 1.0f;
		public float scatteringIntensity = 1.0f;		
		public float multiScatterStrength = 0.5f; 
		public float multiScatterFalloff = 0.02f;
		public float ambientFloor = 0.02f;
		public float lightningIntensity = 0.5f;
		public float exposure = 1f;
 		public float silverLiningSpread = 0.3f;
		public float silverLiningIntensity = 1f;
		public float edgeHighlightStrength = 1f;
		
		public float ligthAbsorbtion = 0.5f;
		public float density = 1.0f;
		public float densitySmoothness = 1.0f;  
		public float baseErosionIntensity = 0.4f;
        public float baseNoiseMultiplier = 1f;
		public float detailErosionIntensity = 0.25f;
		public float detailNoiseMultiplier = 1f;
		public float curlIntensity = 0.2f;
		public float bottomShape = 0.0f;
        public float midShape = 0.0f;    
        public float topShape = 0.0f;   
        public float topLayer = 0.0f;  
		public float rampShape = 1.0f;  


		public float baseNoiseUVMultiplier = 1.0f;  
		public float detailNoiseUVMultiplier = 1.0f;   
	}

	[Serializable]  
	public class EnviroWeatherTypeFlatCloudsOverride
	{
		public float cirrusCloudsAlpha = 0.5f;
		public float cirrusCloudsCoverage = 0.5f;
		public float cirrusCloudsColorPower = 1.0f;
		public float flatCloudsCoverage = 1.0f;
		public float flatCloudsDensity = 1.0f;
		public float flatCloudsLightIntensity = 1.0f;
		public float flatCloudsAmbientIntensity = 1.0f;
		public float flatCloudsShadowIntensity = 0.6f;
		public int flatCloudsShadowSteps = 8;
	} 

	[Serializable] 
	public class EnviroWeatherTypeLightingOverride
	{
		public float directLightIntensityModifier = 1.0f;
		public float ambientIntensityModifier = 1.0f;
		public float shadowIntensity = 1.0f;
	} 

	[Serializable]  
	public class EnviroWeatherTypeSkyOverride
	{
		public float intensity = 1.0f;
		public float mieScatteringMultiplier = 1.0f;
		public float skyColorExponent = 1.0f;
		public Color skyColorTint = Color.white;
	} 
 
 	[Serializable]  
	public class EnviroAudioOverrideType
	{
		public bool showEditor;
		public string name;
		public float volume;
		public bool spring;
		public bool summer;
		public bool autumn;
		public bool winter;
	}

	[Serializable]  
	public class EnviroWeatherTypeAudioOverride
	{
		public List<EnviroAudioOverrideType> ambientOverride = new List<EnviroAudioOverrideType>();
		public List<EnviroAudioOverrideType> weatherOverride = new List<EnviroAudioOverrideType>();
	}
	
	[Serializable] 
	public class EnviroWeatherTypeFogOverride
	{
		public float fogDensity = 0.02f; 
		public float fogHeightFalloff = 0.2f;
		public float fogHeight = 0.0f;
		public float fogDensity2 = 0.02f;
		public float fogHeightFalloff2 = 0.2f;
		public float fogHeight2;   
		public float fogColorBlend = 0.5f;
		public Color fogColorMod = Color.white;
		public float scattering = 0.015f;
		public float extinction = 0.01f;
		public float anistropy = 0.6f; 

		#if ENVIRO_HDRP 
		public float fogAttenuationDistance = 400f;	
		public float maxHeight = 250f;
		public float baseHeight = 0f;
		public float ambientDimmer = 1f;
		public float directLightMultiplier = 1f;
		public float directLightShadowdimmer = 1f;
		#endif

		public float unityFogDensity = 0.002f;
    	public float unityFogStartDistance = 0f;
    	public float unityFogEndDistance = 1000f;
	}

	[Serializable]  
	public class EnviroEffectsOverrideType
	{
		public bool showEditor;
		public string name;
		public float emission;
	}

	[Serializable]  
	public class EnviroWeatherTypeEffectsOverride
	{
		public List<EnviroEffectsOverrideType> effectsOverride = new List<EnviroEffectsOverrideType>();
	}

	[Serializable]  
	public class EnviroWeatherTypeAuroraOverride
	{
		public float auroraIntensity = 1f;
	} 

	[Serializable]  
	public class EnviroWeatherTypeEnvironmentOverride
	{
		public float temperatureWeatherMod = 0f;
		public float wetnessTarget = 0f;
		public float snowTarget = 0f;

		public float windDirectionX = 1f;
		public float windDirectionY = -1f;
		public float windSpeed = 0.25f; 
		public float windTurbulence = 0.25f;

	} 

	[Serializable]  
	public class EnviroWeatherTypeLightningOverride
	{
		public bool lightningStorm = false;
		public float randomLightningDelay = 1f;
	} 
 
	[Serializable]  
	public class EnviroWeatherType : ScriptableObject 
	{
		//Inspector 
		public bool showEditor, showEffectControls, showCloudControls, showFlatCloudControls, showFogControls, showSkyControls, showLightingControls, showAuroraControls,showEnvironmentControls, showAudioControls, showAmbientAudioControls, showWeatherAudioControls,showLightningControls;
		
		public EnviroWeatherTypeCloudsOverride cloudsOverride;
		public EnviroWeatherTypeFlatCloudsOverride flatCloudsOverride;
		public EnviroWeatherTypeLightingOverride lightingOverride;
		public EnviroWeatherTypeSkyOverride skyOverride;
		public EnviroWeatherTypeFogOverride fogOverride;
		public EnviroWeatherTypeAuroraOverride auroraOverride;
		public EnviroWeatherTypeEffectsOverride effectsOverride;
		public EnviroWeatherTypeAudioOverride audioOverride;
		public EnviroWeatherTypeLightningOverride lightningOverride;
		public EnviroWeatherTypeEnvironmentOverride environmentOverride;
	}


	public class EnviroWeatherTypeCreation {
		#if UNITY_EDITOR
		[UnityEditor.MenuItem("Assets/Create/Enviro3/Weather")]
		#endif
		public static EnviroWeatherType CreateMyAsset()
		{
			EnviroWeatherType wpreset = ScriptableObject.CreateInstance<EnviroWeatherType>();
			#if UNITY_EDITOR
			// Create and save the new profile with unique name
			string path = UnityEditor.AssetDatabase.GetAssetPath (UnityEditor.Selection.activeObject);
			if (path == "") 
			{
				path = EnviroHelper.assetPath + "/Profiles/Weather Types";
			} 
			string assetPathAndName = UnityEditor.AssetDatabase.GenerateUniqueAssetPath (path + "/New " + "Weather Type" + ".asset");
			UnityEditor.AssetDatabase.CreateAsset (wpreset, assetPathAndName);
			UnityEditor.AssetDatabase.SaveAssets ();
			UnityEditor.AssetDatabase.Refresh();
			#endif
			return wpreset;
		}


		public static GameObject GetAssetPrefab(string name)
		{
			#if UNITY_EDITOR
			string[] assets = UnityEditor.AssetDatabase.FindAssets(name, null);
			for (int idx = 0; idx < assets.Length; idx++)
			{
				string path = UnityEditor.AssetDatabase.GUIDToAssetPath(assets[idx]);
				if (path.Contains(".prefab"))
				{
					return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
				}
			}
			#endif
			return null;
		}

		public static Cubemap GetAssetCubemap(string name)
		{
			#if UNITY_EDITOR
			string[] assets = UnityEditor.AssetDatabase.FindAssets(name, null);
			for (int idx = 0; idx < assets.Length; idx++)
			{
				string path = UnityEditor.AssetDatabase.GUIDToAssetPath(assets[idx]);
				if (path.Contains(".png"))
				{
					return UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>(path);
				}
			}
			#endif
			return null;
		}

		public static Texture GetAssetTexture(string name)
		{
			#if UNITY_EDITOR
			string[] assets = UnityEditor.AssetDatabase.FindAssets(name, null);
			for (int idx = 0; idx < assets.Length; idx++)
			{
				string path = UnityEditor.AssetDatabase.GUIDToAssetPath(assets[idx]);
				if (path.Length > 0)
				{
					return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>(path);
				}
			}
			#endif
			return null;
		}
			
		public static Gradient CreateGradient()
		{
			Gradient nG = new Gradient ();
			GradientColorKey[] gClr = new GradientColorKey[2];
			GradientAlphaKey[] gAlpha = new GradientAlphaKey[2];
 
			gClr [0].color = Color.white;
			gClr [0].time = 0f;
			gClr [1].color = Color.white;
			gClr [1].time = 0f;

			gAlpha [0].alpha = 0f;
			gAlpha [0].time = 0f;
			gAlpha [1].alpha = 0f;
			gAlpha [1].time = 1f;

			nG.SetKeys (gClr, gAlpha);

			return nG;
		}
			
		public static Color GetColor (string hex)
		{
			Color clr = new Color ();	
			ColorUtility.TryParseHtmlString (hex, out clr);
			return clr;
		}
		
		public static Keyframe CreateKey (float value, float time)
		{
			Keyframe k = new Keyframe();
			k.value = value;
			k.time = time;
			return k;
		}

		public static Keyframe CreateKey (float value, float time, float inTangent, float outTangent)
		{
			Keyframe k = new Keyframe();
			k.value = value;
			k.time = time;
			k.inTangent = inTangent;
			k.outTangent = outTangent;
			return k;
		}		
	}
}
