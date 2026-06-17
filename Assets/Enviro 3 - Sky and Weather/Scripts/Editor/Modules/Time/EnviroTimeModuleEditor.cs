using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Enviro
{
    [CustomEditor(typeof(EnviroTimeModule))]
    public class EnviroTimeModuleEditor : EnviroModuleEditor
    {  
        private EnviroTimeModule myTarget; 

        //Properties
        private SerializedProperty simulate,latitude,longitude,utcOffset,cycleLengthInMinutes,dayLengthModifier,nightLengthModifier,calenderType,daysInMonth,monthsInYear,customSunOffset,customSunRotation;  

        //On Enable
        public override void OnEnable()
        {
            if(!target)
                return;

            myTarget = (EnviroTimeModule)target;
            serializedObj = new SerializedObject(myTarget);
            preset = serializedObj.FindProperty("preset");
            simulate = serializedObj.FindProperty("Settings.simulate"); 
            latitude = serializedObj.FindProperty("Settings.latitude");
            longitude = serializedObj.FindProperty("Settings.longitude");
            utcOffset = serializedObj.FindProperty("Settings.utcOffset");
            calenderType = serializedObj.FindProperty("Settings.calenderType");
            daysInMonth = serializedObj.FindProperty("Settings.daysInMonth");
            monthsInYear = serializedObj.FindProperty("Settings.monthsInYear");

            customSunOffset = serializedObj.FindProperty("Settings.customSunOffset");
            customSunRotation = serializedObj.FindProperty("Settings.customSunRotation");
 
            cycleLengthInMinutes = serializedObj.FindProperty("Settings.cycleLengthInMinutes");
            dayLengthModifier = serializedObj.FindProperty("Settings.dayLengthModifier");
            nightLengthModifier = serializedObj.FindProperty("Settings.nightLengthModifier");
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
            myTarget.showModuleInspector = GUILayout.Toggle(myTarget.showModuleInspector, "Time", headerFoldout);
            
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("x", EditorStyles.miniButtonRight,GUILayout.Width(18), GUILayout.Height(18)))
            {
                EnviroManager.instance.RemoveModule(EnviroManager.ModuleType.Time);
                DestroyImmediate(this);
                return;
            } 
            
            EditorGUILayout.EndHorizontal();
            
            if(myTarget.showModuleInspector)
            {
                //EditorGUILayout.LabelField("This module will control the time of day.");
                serializedObj.UpdateIfRequiredOrScript ();
                EditorGUI.BeginChangeCheck();
                GUI.backgroundColor = categoryModuleColor;
                GUILayout.BeginVertical("",boxStyleModified);
                GUI.backgroundColor = Color.white;
                myTarget.showTimeControls = GUILayout.Toggle(myTarget.showTimeControls, "Time Controls", headerFoldout);
                    
                if(myTarget.showTimeControls)
                {   
                    EditorGUI.BeginChangeCheck();
                    GUILayout.Space(10); 
                    EditorGUILayout.LabelField("Time", headerStyle);
   

                    int secT,minT,hoursT,daysT,monthsT,yearsT = 0;

                    secT = EditorGUILayout.IntSlider("Second", myTarget.seconds,0,60);
                    minT = EditorGUILayout.IntSlider("Minute", myTarget.minutes,0,60);
                    hoursT = EditorGUILayout.IntSlider("Hour", myTarget.hours,0,24);
                    GUILayout.Space(10);
                    EditorGUILayout.LabelField("Date", headerStyle);
                    EditorGUILayout.PropertyField(calenderType);
                     if(myTarget.Settings.calenderType == EnviroTime.CalenderType.Custom)
                     {
                        daysT =  EditorGUILayout.IntSlider("Day", myTarget.days,1,myTarget.Settings.daysInMonth);
                        monthsT = EditorGUILayout.IntSlider("Month", myTarget.months,1,myTarget.Settings.monthsInYear);
                     }
                     else
                     {
                        daysT = EditorGUILayout.IntSlider("Day", myTarget.days,1,31);
                        monthsT = EditorGUILayout.IntSlider("Month", myTarget.months,1,12);
                     }

                    yearsT = EditorGUILayout.IntSlider("Year", myTarget.years,1,3000);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(target, "Time Changed");
                        myTarget.seconds = secT;
                        myTarget.minutes = minT; 
                        myTarget.hours = hoursT;
                        myTarget.days = daysT;
                        myTarget.months = monthsT;
                        myTarget.years = yearsT;
                        EditorUtility.SetDirty(myTarget);
                    }
                    if(myTarget.Settings.calenderType == EnviroTime.CalenderType.Custom)
                    {
                        GUILayout.Space(10);
                        EditorGUILayout.LabelField("Custom Calender Settings", headerStyle);
                        EditorGUILayout.PropertyField(daysInMonth);
                        EditorGUILayout.PropertyField(monthsInYear);
                        GUILayout.Space(10);
                        EditorGUILayout.PropertyField(customSunOffset); 
                        EditorGUILayout.PropertyField(customSunRotation);
                    }
                    GUILayout.Space(10);
                    EditorGUILayout.LabelField("Progression", headerStyle);
                    EditorGUILayout.PropertyField(simulate);
                    EditorGUILayout.PropertyField(cycleLengthInMinutes);
                    EditorGUILayout.PropertyField(dayLengthModifier);
                    EditorGUILayout.PropertyField(nightLengthModifier);  
                    GUILayout.Space(5);
                    if(EnviroManager.instance != null)
                       EnviroManager.instance.dayNightSwitch = EditorGUILayout.Slider("Day Night Switch",EnviroManager.instance.dayNightSwitch,0.2f,0.7f);                               
                    
                    Repaint();
                }  
                GUILayout.EndVertical();

                if(myTarget.showTimeControls)
                    GUILayout.Space(10);
                if(myTarget.Settings.calenderType == EnviroTime.CalenderType.Realistic)
                {
                    GUI.backgroundColor = categoryModuleColor;
                    GUILayout.BeginVertical("",boxStyleModified);
                    GUI.backgroundColor = Color.white;
                    myTarget.showLocationControls = GUILayout.Toggle(myTarget.showLocationControls, "Location Controls", headerFoldout);            
                    if(myTarget.showLocationControls)
                    {
                        EditorGUILayout.PropertyField(latitude);
                        EditorGUILayout.PropertyField(longitude);            
                        EditorGUILayout.PropertyField(utcOffset);
                    }  
                    GUILayout.EndVertical();
                }
                
                if(myTarget.showLocationControls)
                    GUILayout.Space(10);

                if(!Application.isPlaying) 
                    myTarget.UpdateModule();


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

                if(myTarget.showSaveLoad)
                    GUILayout.Space(10);

                ApplyChanges ();
            }
            GUILayout.EndVertical();

            if(myTarget.showModuleInspector)
             GUILayout.Space(20);
        }
    }
}
