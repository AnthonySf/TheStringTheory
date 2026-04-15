/*
 * 
 * this script has been replaced by ScenesDemonstrationMono.cs and can be deleted
 * 

using MidiPlayerTK;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemoMPTK
{
    public class SceneHandler : MonoBehaviour
    {
        // Manage skin
        public CustomStyle myStyle;
        //public Button MsgScene;
        //private float widthColDescription = 500;
        private float widthColGo = 200;
        private float widthColVersion = 70;
        private float height = 70;
        private Vector2 scrollerWindow = Vector2.zero;

        public class Demonstrator
        {
            public string Title;
            public string Description;
            public string SceneName;
            public string ScripName;
            public string PrefabClass;
            public string Version;
            public bool Pro;
            public static List<Demonstrator> Demos;

            public static void Load()
            {
                try
                {
                    Demos = new List<Demonstrator>();
                    TextAsset mytxtData = Resources.Load<TextAsset>("DemosListForBuilder");
                    string text = System.Text.Encoding.UTF8.GetString(mytxtData.bytes);
                    text = text.Replace("\n", "");
                    text = text.Replace("\\n", "\n");
                    string[] listDemos = text.Split('\r');
                    if (listDemos != null)
                    {
                        foreach (string demo in listDemos)
                        {
                            string[] colmuns = demo.Split(';');
                            if (colmuns.Length >= 5)
                                Demos.Add(new Demonstrator()
                                {
                                    Title = colmuns[0],
                                    Description = colmuns[1],
                                    SceneName = colmuns[2],
                                    ScripName = colmuns[3],
                                    PrefabClass = colmuns[4],
                                    Version = colmuns[5],
                                    Pro = colmuns[5] == "Pro" ? true : false
                                });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log("Error loading demonstrator " + ex.Message);
                    throw;
                }
            }
        }
        // Use this for initialization
        void Start()
        {
            Demonstrator.Load();
        }

        void OnGUI()
        {
            if (myStyle == null) myStyle = new CustomStyle();

            scrollerWindow = GUILayout.BeginScrollView(scrollerWindow, false, false, GUILayout.Width(Screen.width));

            MainMenu.Display("Maestro MPTK Demonstration - Have a look to the demo scenes and the documentation!", myStyle);

                for (int index = 1; index < Demonstrator.Demos.Count; index++)
                {
                    Demonstrator demo = Demonstrator.Demos[index];
                    if (SceneUtility.GetBuildIndexByScenePath(demo.SceneName) >= 0)
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(demo.Title, myStyle.TextFieldMultiLineCentered, GUILayout.Width(widthColGo), GUILayout.Height(height)) ||
                            GUILayout.Button(demo.Version, myStyle.TextFieldMultiLineCentered, GUILayout.Width(widthColVersion), GUILayout.Height(height)) ||
                            GUILayout.Button(demo.Description, myStyle.TextFieldMultiLine, GUILayout.Height(height)))
                            SceneManager.LoadScene(SceneUtility.GetBuildIndexByScenePath(demo.SceneName), LoadSceneMode.Single);
                        GUILayout.EndHorizontal();
                    }
                }
            GUILayout.EndScrollView();
        }
    }
}

*/
