using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;

namespace Enviro
{
    [CustomEditor(typeof(Enviro.EnviroEffectRemovalZone))]
    public class EnviroEffectRemovalZoneEditor : Editor {

        GUIStyle boxStyle;
        GUIStyle boxStyleModified;
        GUIStyle wrapStyle;
        GUIStyle wrapStyle2;
        GUIStyle clearStyle;

        Enviro.EnviroEffectRemovalZone myTarget;

        private Color boxColor1;

        SerializedObject serializedObj;

        private SerializedProperty type, density, radius, stretch, feather, size;

        void OnEnable()
        {
            myTarget = (Enviro.EnviroEffectRemovalZone)target;
            serializedObj = new SerializedObject (myTarget);
            boxColor1 = new Color(0.95f, 0.95f, 0.95f,1f);
            type = serializedObj.FindProperty("type"); 
            density = serializedObj.FindProperty("density"); 
            radius = serializedObj.FindProperty("radius"); 
            stretch = serializedObj.FindProperty("stretch"); 
            feather = serializedObj.FindProperty("feather"); 
            size = serializedObj.FindProperty("size"); 
        }

        public override void OnInspectorGUI ()
        {
            
            //Set up the box style
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box);
                boxStyle.normal.textColor = GUI.skin.label.normal.textColor;
                boxStyle.fontStyle = FontStyle.Bold;
                boxStyle.alignment = TextAnchor.UpperLeft;
            }

            if (boxStyleModified == null)
            {
                boxStyleModified = new GUIStyle(EditorStyles.helpBox);
                boxStyleModified.normal.textColor = GUI.skin.label.normal.textColor;
                boxStyleModified.fontStyle = FontStyle.Bold;
                boxStyleModified.fontSize = 11;
                boxStyleModified.alignment = TextAnchor.UpperLeft;
            }

            //Setup the wrap style
            if (wrapStyle == null)
            {
                wrapStyle = new GUIStyle(GUI.skin.label);
                wrapStyle.fontStyle = FontStyle.Bold;
                wrapStyle.wordWrap = true;
            }

            if (wrapStyle2 == null)
            {
                wrapStyle2 = new GUIStyle(GUI.skin.label);
                wrapStyle2.fontStyle = FontStyle.Normal;
                wrapStyle2.wordWrap = true;
            }

            if (clearStyle == null) {
                clearStyle = new GUIStyle(GUI.skin.label);
                clearStyle.normal.textColor = GUI.skin.label.normal.textColor;
                clearStyle.fontStyle = FontStyle.Bold;
                clearStyle.alignment = TextAnchor.UpperRight;
            }


            GUILayout.BeginVertical(" Enviro - Effect Removal Zone", boxStyle);
            GUILayout.Space(30);
            GUI.backgroundColor = boxColor1;
            GUILayout.BeginVertical("Information", boxStyleModified);
            GUI.backgroundColor = Color.white;
            GUILayout.Space(20);
            EditorGUILayout.LabelField("Use this component to hide fog and weather particles for example for indoor areas.", wrapStyle2);
            GUILayout.EndVertical();
            GUI.backgroundColor = boxColor1;
            GUILayout.BeginVertical("", boxStyleModified);
            GUI.backgroundColor = Color.white;
            GUILayout.Space(20);
            ///////
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(type);
            GUILayout.Space(5);
            EditorGUILayout.PropertyField(density);
            EditorGUILayout.PropertyField(feather);
            GUILayout.Space(5);
            if(myTarget.type == Enviro.EnviroEffectRemovalZone.Mode.Spherical)
            {   
                EditorGUILayout.PropertyField(radius);
                EditorGUILayout.PropertyField(stretch);
            }   
            else
            {
                EditorGUILayout.PropertyField(size);
            }
            
            if (EditorGUI.EndChangeCheck ()) 
            {
			    serializedObj.ApplyModifiedProperties ();
		    }
          
            ///////
            GUILayout.EndVertical();
            
            // END
            EditorGUILayout.EndVertical ();
        }
    }
}