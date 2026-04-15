/*
 * 
 * this script has been replaced by ScenesDemonstrationEditor.cs and can be deleted
 * 
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MidiPlayerTK
{
    /// <summary>@brief
    /// Window editor for selecting a demo in editor mode
    /// </summary>
    public class DemoLoaderWindow : EditorWindow
    {
        static DemoLoaderWindow window;
        Vector2 scrollPosSoundFont = Vector2.zero;
        List<MPTKGui.StyleItem> ListDemos;
        // Will be recalculated at each iteration
        float totalLineHeight;
        static Demonstrator loadedDemos;

        //[MenuItem(Constant.MENU_MAESTRO + "Load Demonstration &D", false, 50)] // The MenuItem's are sorted in increasing order and if you add more then 10 between two items (so, create at 10, 30, 50,...    ), an Separator-Line is drawn before the menuitem.
        public static void Init()
        {
            // Get existing open window or if none, make a new one:
            try
            {
                window = EditorWindow.GetWindow<DemoLoaderWindow>(true, "Demonstration Loader - Version: " + Constant.version);
                if (window != null)
                {
                    window.minSize = new Vector2(200, 300);
                    window.Show();
                    //Demonstrator.Load();
                    loadedDemos = new Demonstrator();
                    loadedDemos.LoadCSV();
                }
            }
            catch (System.Exception ex)
            {
                Debug.Log(ex.ToString());
            }
        }

        /// <summary>@brief
        /// Reload data
        /// </summary>
        private void OnFocus()
        {
            // Load description of available soundfont
            try
            {
                Init();
            }
            catch (Exception ex)
            {
                Debug.Log(ex.ToString());
            }
        }

        void OnGUI()
        {
            try
            {
                if (window == null) Init();
                MPTKGui.LoadSkinAndStyle();
                GUI.skin = MPTKGui.MaestroSkin;

                GUI.Box(new Rect(0, 0, window.position.width, window.position.height), "", EditorStyles.helpBox);
                ShowListDemos(0, 0, window.position.size.x - 5, window.position.size.y - 5);

            }
            catch (ExitGUIException) { }
            catch (Exception ex)
            {
                Debug.Log(ex.ToString());
            }
        }

        /// <summary>@brief
        /// Display, add, remove Soundfont
        /// </summary>
        private void ShowListDemos(float startX, float startY, float width, float height)
        {
            try
            {
                if (ListDemos == null)
                {
                    ListDemos = new List<MPTKGui.StyleItem>();
                    ListDemos.Add(new MPTKGui.StyleItem() { Width = 180, Caption = "Load" });
                    ListDemos.Add(new MPTKGui.StyleItem() { Width = 50, Caption = "Version" });
                    ListDemos.Add(new MPTKGui.StyleItem() { Width = 360, Caption = "Description" });
                    ListDemos.Add(new MPTKGui.StyleItem() { Width = 180, Caption = "Scene Name" });
                    ListDemos.Add(new MPTKGui.StyleItem() { Width = 180, Caption = "Main Scripts" });
                    ListDemos.Add(new MPTKGui.StyleItem() { Width = 130, Caption = "Class or Prefab" });
                }

                // Title bar
                GUILayout.BeginHorizontal();
                foreach (MPTKGui.StyleItem column in ListDemos)
                {
                    GUILayout.Label(column.Caption, MPTKGui.styleListTitle, GUILayout.Width(column.Width));
                }
                GUILayout.EndHorizontal();

                // Content
                Rect listVisibleRect = new Rect(startX, startY, width, height);
                Rect listContentRect = new Rect(0, 0, width - 35, totalLineHeight);
                //Debug.Log(totalLineHeight);
                scrollPosSoundFont = GUI.BeginScrollView(listVisibleRect, scrollPosSoundFont, listContentRect, false, true);
                totalLineHeight = 100; // required to start at 100 to have the full list
                // Loop on each demo (pass first, it's the title)
                //for (int i = 1; i < Demonstrator.Demos.Count; i++)
                for (int i = 1; i < loadedDemos.Demos.Count; i++)
                {
                    //Demonstrator sf = Demonstrator.Demos[i];
                    Demonstrator sf = loadedDemos.Demos[i];

                    // Description columun is the higher cel
                    float lineHeight = MPTKGui.styleListRowLeft.CalcHeight(new GUIContent(sf.Description), ListDemos[2].Width);
                    totalLineHeight += lineHeight;

                    GUILayout.BeginHorizontal();

                    // title + load
                    if (GUILayout.Button($"{i} - {sf.Title}", MPTKGui.Button, GUILayout.Width(ListDemos[0].Width), GUILayout.Height(lineHeight)))
                    {
                        if (Application.isPlaying)
                            EditorUtility.DisplayDialog("Load a Scene", "Can't load a scene when running", "ok");
                        else
                        {
                            if (sf.Version == "Dev")
                            {
                                EditorUtility.DisplayDialog("Not Yet Available", "Design in progress ...", "ok");
                            }
                            else
                            {
                                // Build path to the Unity scene
                                string freepro = sf.Version == "Free" ? "FreeDemos" : "ProDemos";
                                string scenePath = $"Assets/MidiPlayer/Demo/{freepro}/{sf.SceneName}.unity";
                                try
                                {
                                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                                    EditorSceneManager.OpenScene(scenePath);
                                }
                                catch (Exception ex)
                                {
                                    Debug.Log(ex.ToString());
                                    PopupWindow.Show(new Rect(100, 100, 180, 18), new GetFullVersion());
                                }
                            }
                        }
                    }
                    GUILayout.Label(sf.Version, MPTKGui.styleListRowCenter, GUILayout.Width(ListDemos[1].Width), GUILayout.Height(lineHeight));
                    GUILayout.Label(sf.Description, MPTKGui.styleListRowLeft, GUILayout.Width(ListDemos[2].Width), GUILayout.Height(lineHeight));
                    GUILayout.Label(sf.SceneName, MPTKGui.styleListRowLeft, GUILayout.Width(ListDemos[3].Width), GUILayout.Height(lineHeight));
                    GUILayout.Label(sf.ScripName, MPTKGui.styleListRowLeft, GUILayout.Width(ListDemos[4].Width), GUILayout.Height(lineHeight));
                    GUILayout.Label(sf.PrefabClass, MPTKGui.styleListRowLeft, GUILayout.Width(ListDemos[5].Width), GUILayout.Height(lineHeight));

                    GUILayout.EndHorizontal();
                }
                GUI.EndScrollView();
            }
            catch (ExitGUIException) { }
            catch (Exception ex)
            {
                Debug.Log(ex.ToString());
            }
        }

        //class Demonstrator
        //{
        //    public string Title;
        //    public string Description;
        //    public string SceneName;
        //    public string ScripName;
        //    public string PrefabClass;
        //    public string Version;
        //    public bool Pro;
        //    public static List<Demonstrator> Demos;

        //    public static void Load()
        //    {
        //        try
        //        {
        //            Demos = new List<Demonstrator>();
        //            //TextAsset mytxtData = Resources.Load<TextAsset>("DemosList");
        //            //string text = System.Text.Encoding.UTF8.GetString(mytxtData.bytes);
        //            //text = text.Replace("\n", "");
        //            //text = text.Replace("\\n", "\n");
        //            string filename = Application.dataPath + "/MidiPlayer/DemosList.csv";
        //            //Debug.Log($"Load  {filename}");
        //            string text = ToolsEditor.ReadTextFile(filename);
        //            string[] listDemos = text.Split('\n');
        //            if (listDemos != null)
        //            {
        //                foreach (string demo in listDemos)
        //                {
        //                    string[] colmuns = demo.Split(';');
        //                    if (colmuns.Length >= 5)
        //                        Demos.Add(new Demonstrator()
        //                        {
        //                            Title = colmuns[0].Replace("\\n", "\n"),
        //                            Description = colmuns[1].Replace("\\n", "\n"),
        //                            SceneName = colmuns[2],
        //                            ScripName = colmuns[3].Replace("\\n", "\n"),
        //                            PrefabClass = colmuns[4].Replace("\\n", "\n"),
        //                            Version = colmuns[5],
        //                            Pro = colmuns[5] == "Pro" ? true : false
        //                        });
        //                }
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Debug.Log("Error loading demonstrator " + ex.Message);
        //            throw;
        //        }
        //    }
        //}
    }
}
*/
