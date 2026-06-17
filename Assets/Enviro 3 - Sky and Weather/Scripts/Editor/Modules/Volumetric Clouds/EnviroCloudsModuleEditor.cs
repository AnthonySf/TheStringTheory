using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Enviro 
{
    [CustomEditor(typeof(EnviroVolumetricCloudsModule))]
    public class EnviroCloudsModuleEditor : EnviroModuleEditor
    {  
        private EnviroVolumetricCloudsModule myTarget; 
        private SerializedProperty customWeatherMap,depthBlending,depthTest,sunLightColorGradient,moonLightColorGradient, ambientColorGradient,ambientLighIntensity,cloudShadows, cloudShadowsIntensity,
        
        noise, detailNoise, curlTex, blueNoise,bottomsOffsetNoise, cloudsWorldScale,maxRenderDistance, atmosphereColorSaturateDistance, cloudsTravelSpeed;
        //Properties Layer 1
        private SerializedProperty bottomCloudsHeightLayer1,topCloudsHeightLayer1,densityLayer1,densitySmoothnessLayer1, coverageLayer1,worleyFreq1Layer1, worleyFreq2Layer1, dilateCoverageLayer1, dilateTypeLayer1,cloudsTypeModifierLayer1, locationOffsetLayer1,
        scatteringIntensityLayer1, silverLiningSpreadLayer1, lightningIntensity,exposure, baseNoiseMultiplierLayer1,detailNoiseMultiplierLayer1,
        curlIntensityLayer1, lightStepModifierLayer1, lightAbsorbtionLayer1,baseNoiseUVLayer1, detailNoiseUVLayer1,rampShapeLayer1,edgeHighlightStrength,
        baseErosionIntensityLayer1, detailErosionIntensityLayer1, multiScatterStrengthLayer1, multiScatterFalloffLayer1,ambientFloorLayer1,cloudTypeShapingLayer1, bottomShapeLayer1,midShapeLayer1,topShapeLayer1,topLayerLayer1,silverLiningIntensityLayer1;
    
        //Properties Quality
        private SerializedProperty volumetricClouds,lightningSupport,variableBottomNoise, downsampling, stepsLayer1, blueNoiseIntensity, reprojectionBlendTime, lodDistance;

        private SerializedProperty windSpeedModifierLayer1, windUpwardsLayer1, cloudsWindDirectionXModifierLayer1, cloudsWindDirectionYModifierLayer1;
     
        //On Enable
        public override void OnEnable()
        {
            if(!target)
                return;

            myTarget = (EnviroVolumetricCloudsModule)target;
            serializedObj = new SerializedObject(myTarget);
            preset = serializedObj.FindProperty("preset");

            ambientColorGradient = serializedObj.FindProperty("settingsGlobal.ambientColorGradient");
            ambientLighIntensity = serializedObj.FindProperty("settingsGlobal.ambientLighIntensity"); 
            sunLightColorGradient = serializedObj.FindProperty("settingsGlobal.sunLightColorGradient");
            moonLightColorGradient = serializedObj.FindProperty("settingsGlobal.moonLightColorGradient");
            depthBlending = serializedObj.FindProperty("settingsGlobal.depthBlending"); 
            depthTest = serializedObj.FindProperty("settingsGlobal.depthTest"); 
            cloudShadows = serializedObj.FindProperty("settingsGlobal.cloudShadows");      
            cloudShadowsIntensity = serializedObj.FindProperty("settingsGlobal.cloudShadowsIntensity"); 
            noise = serializedObj.FindProperty("settingsGlobal.noise"); 
            detailNoise = serializedObj.FindProperty("settingsGlobal.detailNoise"); 
            curlTex = serializedObj.FindProperty("settingsGlobal.curlTex"); 
            bottomsOffsetNoise = serializedObj.FindProperty("settingsGlobal.bottomsOffsetNoise"); 
            blueNoise = serializedObj.FindProperty("settingsGlobal.blueNoise"); 
            cloudsWorldScale = serializedObj.FindProperty("settingsGlobal.cloudsWorldScale"); 
            maxRenderDistance = serializedObj.FindProperty("settingsGlobal.maxRenderDistance"); 
            atmosphereColorSaturateDistance = serializedObj.FindProperty("settingsGlobal.atmosphereColorSaturateDistance");         
            cloudsTravelSpeed = serializedObj.FindProperty("settingsGlobal.cloudsTravelSpeed");      
            customWeatherMap = serializedObj.FindProperty("settingsGlobal.customWeatherMap");          
             
            //Quality
            volumetricClouds = serializedObj.FindProperty("settingsQuality.volumetricClouds"); 
            lightningSupport = serializedObj.FindProperty("settingsQuality.lightningSupport"); 
            variableBottomNoise = serializedObj.FindProperty("settingsQuality.variableBottomNoise"); 
            
            downsampling = serializedObj.FindProperty("settingsQuality.downsampling"); 
            stepsLayer1 = serializedObj.FindProperty("settingsQuality.stepsLayer1"); 
            blueNoiseIntensity = serializedObj.FindProperty("settingsQuality.blueNoiseIntensity"); 
            reprojectionBlendTime = serializedObj.FindProperty("settingsQuality.reprojectionBlendTime"); 
            lodDistance = serializedObj.FindProperty("settingsQuality.lodDistance"); 

            //Layer 1
            bottomCloudsHeightLayer1 = serializedObj.FindProperty("settingsVolume.bottomCloudsHeight"); 
            topCloudsHeightLayer1 = serializedObj.FindProperty("settingsVolume.topCloudsHeight");           
            coverageLayer1 = serializedObj.FindProperty("settingsVolume.coverage"); 
            worleyFreq1Layer1 = serializedObj.FindProperty("settingsVolume.worleyFreq1"); 
            worleyFreq2Layer1 = serializedObj.FindProperty("settingsVolume.worleyFreq2"); 
            dilateCoverageLayer1 = serializedObj.FindProperty("settingsVolume.dilateCoverage"); 
            dilateTypeLayer1 = serializedObj.FindProperty("settingsVolume.dilateType"); 
            cloudsTypeModifierLayer1 = serializedObj.FindProperty("settingsVolume.cloudsTypeModifier"); 
            locationOffsetLayer1 = serializedObj.FindProperty("settingsVolume.locationOffset"); 
            densityLayer1 = serializedObj.FindProperty("settingsVolume.density");  
            rampShapeLayer1 = serializedObj.FindProperty("settingsVolume.rampShape");  
            densitySmoothnessLayer1 = serializedObj.FindProperty("settingsVolume.densitySmoothness");  
            scatteringIntensityLayer1 = serializedObj.FindProperty("settingsVolume.scatteringIntensity");  
            silverLiningSpreadLayer1 = serializedObj.FindProperty("settingsVolume.silverLiningSpread"); 
            silverLiningIntensityLayer1 = serializedObj.FindProperty("settingsVolume.silverLiningIntensity"); 
            edgeHighlightStrength = serializedObj.FindProperty("settingsVolume.edgeHighlightStrength"); 
            baseNoiseMultiplierLayer1 = serializedObj.FindProperty("settingsVolume.baseNoiseMultiplier"); 
            detailNoiseMultiplierLayer1= serializedObj.FindProperty("settingsVolume.detailNoiseMultiplier"); 
            lightningIntensity = serializedObj.FindProperty("settingsVolume.lightningIntensity");  
            exposure = serializedObj.FindProperty("settingsVolume.exposure");  
            curlIntensityLayer1 = serializedObj.FindProperty("settingsVolume.curlIntensity");  
            lightStepModifierLayer1 = serializedObj.FindProperty("settingsVolume.lightStepModifier");  
            lightAbsorbtionLayer1 = serializedObj.FindProperty("settingsVolume.absorbtion");
            baseNoiseUVLayer1 = serializedObj.FindProperty("settingsVolume.baseNoiseUV");
            detailNoiseUVLayer1 = serializedObj.FindProperty("settingsVolume.detailNoiseUV");
            baseErosionIntensityLayer1 = serializedObj.FindProperty("settingsVolume.baseErosionIntensity");
            detailErosionIntensityLayer1 = serializedObj.FindProperty("settingsVolume.detailErosionIntensity");
            multiScatterStrengthLayer1 = serializedObj.FindProperty("settingsVolume.multiScatterStrength");
            multiScatterFalloffLayer1 = serializedObj.FindProperty("settingsVolume.multiScatterFalloff");
            ambientFloorLayer1 = serializedObj.FindProperty("settingsVolume.ambientFloor");
            cloudTypeShapingLayer1 = serializedObj.FindProperty("settingsVolume.cloudTypeShaping");
            bottomShapeLayer1 = serializedObj.FindProperty("settingsVolume.bottomShape");
            midShapeLayer1 = serializedObj.FindProperty("settingsVolume.midShape");
            topShapeLayer1 = serializedObj.FindProperty("settingsVolume.topShape");
            topLayerLayer1 = serializedObj.FindProperty("settingsVolume.topLayer");
            windSpeedModifierLayer1 = serializedObj.FindProperty("settingsVolume.windSpeedModifier"); 
            windUpwardsLayer1 = serializedObj.FindProperty("settingsVolume.windUpwards"); 
            cloudsWindDirectionXModifierLayer1 = serializedObj.FindProperty("settingsVolume.cloudsWindDirectionXModifier"); 
            cloudsWindDirectionYModifierLayer1 = serializedObj.FindProperty("settingsVolume.cloudsWindDirectionYModifier"); 
        } 

        public override void OnInspectorGUI()
        {
            if(!target)
                return;

            base.OnInspectorGUI();

            GUI.backgroundColor = baseModuleColor;
            GUILayout.BeginVertical("",boxStyleModified);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.BeginHorizontal();
            myTarget.showModuleInspector = GUILayout.Toggle(myTarget.showModuleInspector, "Volumetric Clouds", headerFoldout);
            
  
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("x", EditorStyles.miniButtonRight,GUILayout.Width(18), GUILayout.Height(18)))
            {
                EnviroManager.instance.RemoveModule(EnviroManager.ModuleType.VolumetricClouds);
                DestroyImmediate(this);
                return;
            }                      
            EditorGUILayout.EndHorizontal();
            
            if(myTarget.showModuleInspector)
            {
                RenderDisableInputBox();
                serializedObj.UpdateIfRequiredOrScript ();
                EditorGUI.BeginChangeCheck();


                GUILayout.Space(10);

                GUI.backgroundColor = categoryModuleColor;
                GUILayout.BeginVertical("",boxStyleModified);
                GUI.backgroundColor = Color.white;
                myTarget.showGlobalControls = GUILayout.Toggle(myTarget.showGlobalControls, "Global Settings", headerFoldout);            
                if(myTarget.showGlobalControls)
                { 
                    GUILayout.Space(5);
                    GUILayout.Label("Quality", headerStyle);
                    DisableInputStartQuality();
                    EditorGUILayout.PropertyField(volumetricClouds);
                    GUILayout.Space(5);
                    EditorGUILayout.PropertyField(lightningSupport);
                    EditorGUILayout.PropertyField(variableBottomNoise);              
                    DisableInputEndQuality(); 
                    EditorGUILayout.PropertyField(depthBlending);
                    if(!myTarget.settingsGlobal.depthBlending)
                       EditorGUILayout.PropertyField(depthTest);
                    DisableInputStartQuality();
                    GUILayout.Space(5);
                    EditorGUILayout.PropertyField(downsampling);          
                    GUILayout.Space(5);
                    EditorGUILayout.PropertyField(stepsLayer1);
                    GUILayout.Space(5);
                    EditorGUILayout.PropertyField(blueNoiseIntensity);
                    EditorGUILayout.PropertyField(reprojectionBlendTime);
                    GUILayout.Space(5);
                    EditorGUILayout.PropertyField(lodDistance);
                    DisableInputEndQuality();
                    EditorGUILayout.PropertyField(maxRenderDistance);      
                    EditorGUILayout.PropertyField(cloudsWorldScale); 
                    EditorGUILayout.PropertyField(customWeatherMap);                  
                    GUILayout.Space(10);
                    GUILayout.Label("Textures", headerStyle);
                    EditorGUILayout.PropertyField(noise);
                    EditorGUILayout.PropertyField(detailNoise); 
                    EditorGUILayout.PropertyField(curlTex);
                    EditorGUILayout.PropertyField(bottomsOffsetNoise);            
                    EditorGUILayout.PropertyField(blueNoise);
                    GUILayout.Space(10);
                    GUILayout.Label("Lighting", headerStyle);
                    EditorGUILayout.PropertyField(sunLightColorGradient);
                    EditorGUILayout.PropertyField(moonLightColorGradient);
                    EditorGUILayout.PropertyField(ambientColorGradient);
                    DisableInputStart();
                    EditorGUILayout.PropertyField(ambientLighIntensity);
                    DisableInputEnd();
                    EditorGUILayout.PropertyField(atmosphereColorSaturateDistance);
                    GUILayout.Space(10);
                    GUILayout.Label("Wind", headerStyle);
                    EditorGUILayout.PropertyField(cloudsTravelSpeed);                 
                    GUILayout.Space(10);
                    GUILayout.Label("Shadows", headerStyle);
                    EditorGUILayout.PropertyField(cloudShadows);
                    EditorGUILayout.PropertyField(cloudShadowsIntensity);
                    
                }
                GUILayout.EndVertical();
                
                //Layer 1
                GUI.backgroundColor = categoryModuleColor;
                GUILayout.BeginVertical("",boxStyleModified);
                GUI.backgroundColor = Color.white;
                myTarget.showVolumeSettings = GUILayout.Toggle(myTarget.showVolumeSettings, "Volume Settings", headerFoldout);            
                if(myTarget.showVolumeSettings)
                {
                    //Coverage
                    GUILayout.BeginVertical("",boxStyleModified);
                    myTarget.showCoverageControls = GUILayout.Toggle(myTarget.showCoverageControls, "Coverage", headerFoldout);
                    
                    if(myTarget.showCoverageControls)
                    {                          
                        EditorGUILayout.PropertyField(bottomCloudsHeightLayer1);
                        EditorGUILayout.PropertyField(topCloudsHeightLayer1);

                        GUILayout.Space(10);
                        DisableInputStart();
                        EditorGUILayout.PropertyField(coverageLayer1);
                        DisableInputEnd();
                        EditorGUILayout.PropertyField(worleyFreq1Layer1);
                        EditorGUILayout.PropertyField(worleyFreq2Layer1);
                        DisableInputStart();
                        EditorGUILayout.PropertyField(dilateCoverageLayer1);
                        EditorGUILayout.PropertyField(dilateTypeLayer1);
                        EditorGUILayout.PropertyField(cloudsTypeModifierLayer1);                  
                        EditorGUILayout.PropertyField(cloudTypeShapingLayer1);                                 
                        EditorGUILayout.PropertyField(bottomShapeLayer1);    
                        EditorGUILayout.PropertyField(midShapeLayer1);    
                        EditorGUILayout.PropertyField(topShapeLayer1); 
                        EditorGUILayout.PropertyField(rampShapeLayer1);                   
                        EditorGUILayout.PropertyField(topLayerLayer1);
                        DisableInputEnd(); 
         
                        EditorGUILayout.PropertyField(locationOffsetLayer1);
                    }
                    GUILayout.EndVertical(); 

                    //Lighting
                    GUILayout.BeginVertical("",boxStyleModified);
                    myTarget.showLightingControls = GUILayout.Toggle(myTarget.showLightingControls, "Lighting", headerFoldout);
                    
                    if(myTarget.showLightingControls)
                    {
                        DisableInputStart();
                        EditorGUILayout.PropertyField(exposure);    
                        GUILayout.Space(5);   
                        EditorGUILayout.PropertyField(scatteringIntensityLayer1);              
                        EditorGUILayout.PropertyField(multiScatterStrengthLayer1); 
                        EditorGUILayout.PropertyField(multiScatterFalloffLayer1); 
                        EditorGUILayout.PropertyField(ambientFloorLayer1);               
                        GUILayout.Space(10);       
                        EditorGUILayout.PropertyField(silverLiningIntensityLayer1);   
                        EditorGUILayout.PropertyField(silverLiningSpreadLayer1);  
                        EditorGUILayout.PropertyField(edgeHighlightStrength);                                           
                        EditorGUILayout.PropertyField(lightningIntensity);  
                    
                        GUILayout.Space(10);
                        EditorGUILayout.PropertyField(lightAbsorbtionLayer1);  
                        DisableInputEnd(); 
                        EditorGUILayout.PropertyField(lightStepModifierLayer1);   
                    }
                    GUILayout.EndVertical(); 

                    //Density
                    GUILayout.BeginVertical("",boxStyleModified);
                    myTarget.showDensityControls = GUILayout.Toggle(myTarget.showDensityControls, "Density", headerFoldout);
                    
                    if(myTarget.showDensityControls)
                    {
                        DisableInputStart();
                        EditorGUILayout.PropertyField(densityLayer1); 
                        EditorGUILayout.PropertyField(densitySmoothnessLayer1);            
                        DisableInputEnd();   
                        EditorGUILayout.PropertyField(baseNoiseUVLayer1);   
                        EditorGUILayout.PropertyField(detailNoiseUVLayer1);
                        DisableInputStart();
                        EditorGUILayout.PropertyField(baseErosionIntensityLayer1);
                        EditorGUILayout.PropertyField(baseNoiseMultiplierLayer1);            
                        EditorGUILayout.PropertyField(detailErosionIntensityLayer1);
                        EditorGUILayout.PropertyField(detailNoiseMultiplierLayer1);  
                        EditorGUILayout.PropertyField(curlIntensityLayer1);  
                        DisableInputEnd();    
                    }
                    GUILayout.EndVertical();    

                    //Wind
                    GUILayout.BeginVertical("",boxStyleModified);
                    myTarget.showWindControls = GUILayout.Toggle(myTarget.showWindControls, "Wind", headerFoldout);
                    
                    if(myTarget.showWindControls)
                    {
                        EditorGUILayout.PropertyField(windSpeedModifierLayer1);
                        EditorGUILayout.PropertyField(windUpwardsLayer1);
                        GUILayout.Space(5);
                        EditorGUILayout.PropertyField(cloudsWindDirectionXModifierLayer1);    
                        EditorGUILayout.PropertyField(cloudsWindDirectionYModifierLayer1);    
                    }
                    GUILayout.EndVertical();   
                }
                GUILayout.EndVertical(); 
                //Layer End

         
                /// Save Load
                  GUI.backgroundColor = categoryModuleColor;
                GUILayout.BeginVertical("",boxStyleModified);
                GUI.backgroundColor = Color.white;
                myTarget.showSaveLoad = GUILayout.Toggle(myTarget.showSaveLoad, "Save/Load", headerFoldout);
                
                if(myTarget.showSaveLoad)
                {
                    EditorGUILayout.PropertyField(preset);
                    GUILayout.BeginHorizontal("",wrapStyle);

                    if(myTarget.preset != null)
                    {
                        if(GUILayout.Button("Load"))
                        {
                            myTarget.LoadModuleValues();
                        }
                        if(GUILayout.Button("Save"))
                        {
                            myTarget.SaveModuleValues(myTarget.preset);
                        }
                    }
                    if(GUILayout.Button("Save As New"))
                    {
                        myTarget.SaveModuleValues();
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
                /// Save Load End

                //Apply
                ApplyChanges ();
            }
            GUILayout.EndVertical();

            if(myTarget.showModuleInspector)
             GUILayout.Space(20);
        }
    }
}
