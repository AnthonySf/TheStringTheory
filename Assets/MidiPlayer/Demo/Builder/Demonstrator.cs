using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

using UnityEngine.UIElements;

namespace MidiPlayerTK
{
    public class Demonstrator
    {
        static Color ColorBack = new Color(0.5f, 0.5f, 0.5f);
        static Color ColorContentList = new Color(0.7f, 0.7f, 0.7f);
        static Color ColorHover = new Color(0.75f, 0.75f, 0.75f);
        static Color ColorHeaderList = new Color(0.5f, 0.5f, 0.5f);


        public string Title;
        public string Description;
        public string SceneName;
        public string ScripName;
        public string PrefabClass;
        public string Version;
        public bool Pro;
        public List<Demonstrator> Demos;
        public VisualElement Root { get => root; set => root = value; }
        public VisualTreeAsset RowTemplate { get => rowTemplate; set => rowTemplate = value; }

        private VisualElement root;
        private ScrollView scrollView;
        private VisualTreeAsset rowTemplate;

        public Demonstrator()
        {

        }

        public void LoadCSV()
        {
            try
            {
                Demos = new List<Demonstrator>();
                // CSV from Assets/MidiPlayer/Demo/Builder/Resources/DemosListCommon.csv
                TextAsset mytxtData = Resources.Load<TextAsset>("DemosListCommon");
                string text = System.Text.Encoding.UTF8.GetString(mytxtData.bytes);
                text = text.Replace("\n", "");
                text = text.Replace("\r", "");
                text = text.Replace("<br>", "\n");
                string[] listDemos = text.Split("ENDLINE");
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
                    //Debug.Log($"Found {listDemos.Length} lines in csv, {Demos.Count} demos");
                }
                //else  Debug.Log($"Demos CSV not loaded");
            }
            catch (Exception ex)
            {
                Debug.Log("Error loading demonstrator " + ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Find all visual elements and apply content or enable callbacks.
        /// Works the same in Editor and Runtime modes.
        /// </summary>
        public void FindVisualComponent()
        {

            GroupBox gbHeader = Root.Q<GroupBox>("gbHeader");
            gbHeader.style.backgroundColor = ColorBack;
            gbHeader.style.color = Color.black;

            //GroupBox gbHeaderList = Root.Q<GroupBox>("gbHeaderList");
            //gbHeaderList.style.backgroundColor = ColorHeaderList;
            //gbHeaderList.style.color = Color.black;
            //gbHeaderList.style.unityFontStyleAndWeight = FontStyle.Bold;

            scrollView = Root.Q<ScrollView>("scrollView");
            scrollView.style.backgroundColor = ColorContentList;
            scrollView.style.color = Color.black;
            Root.Q<Label>("labTitle").text = "Midi Player Tool Kit Demonstration Loader - V" + Constant.version + " - " + Constant.releaseDate;
#if UNITY_WEBGL
            string title = "Welcome to the first WebGL demo of Maestro MPTK! Please note that due to WebGL constraints,\n";
            title += "timing precision and audio quality might be slightly affected.";
#else
            string title = "";
#endif
            title += "\nAll sources are available, see the Script column to find them in Assets/MidiPlayer/Demo folders";

            Root.Q<Label>("labTitle2").text = title;
            Root.Q<Button>("btQuit").RegisterCallback<ClickEvent>(evt => { MidiPlayerGlobal.MPTK_Quit(); });
            Root.Q<Button>("btWebSite").RegisterCallback<ClickEvent>(evt => { Application.OpenURL("https://paxstellar.fr/"); });
        }

        /* TBD
            Unfortunately, Unity UI Toolkit does not currently support accessing USS variables directly from C# scripts. 
            The recommended way to change styles dynamically at runtime is to define multiple classes in your USS file and switch between them in your C# script1.
            https://docs.unity3d.com/2020.1/Documentation/Manual/UIE-USS.html
            .myGroupBox { background-color: #FF0000; }
            .myGroupBox.blue { background-color: #0000FF; }
            In this example, a GroupBox with the myGroupBox class will have a red background, and a GroupBox with both the myGroupBox and blue classes will have a blue background.
            You can then add or remove the blue class in your C# script to change the background color of the
            // To change the background color to blue
            groupBox.AddToClassList("blue");
            // To change the background color back to red
            groupBox.RemoveFromClassList("blue"); 
        */
        public void AddRow(Demonstrator demo, int index, int selected)
        {
            var row = rowTemplate.CloneTree();
            scrollView.Add(row);

            row.Q<Label>("labTitle").text = demo.Title;
            row.Q<Label>("labVersion").text = demo.Version;
            row.Q<Label>("labDescription").text = demo.Description;
            row.Q<Label>("labSceneName").text = demo.SceneName;
            row.Q<Label>("labMainScripts").text = demo.ScripName;
            row.Q<Label>("LabClass").text = demo.PrefabClass;

            GroupBox gbOneRow = row.Q<GroupBox>("gbOneRow");
            gbOneRow.style.width = 1500; // to limit some refresh issue when hscrolling

            if (index == 1)
            {
                row.Q<Label>("labHeadRow").text = "";
                gbOneRow.style.backgroundColor = ColorHeaderList;
                gbOneRow.style.backgroundColor = ColorHeaderList;
                gbOneRow.style.color = Color.black;
                gbOneRow.style.unityFontStyleAndWeight = FontStyle.Bold;
                gbOneRow.style.fontSize = 16;
                gbOneRow.style.borderTopWidth = 1;
                gbOneRow.style.borderTopColor = Color.black;
            }
            else
            {
                row.Q<Label>("labHeadRow").text = (index - 1).ToString();
                gbOneRow.style.fontSize = 14;

                var backgroundcolorSaved = gbOneRow.style.backgroundColor;
                gbOneRow.RegisterCallback<MouseEnterEvent>(evt => { gbOneRow.style.backgroundColor = ColorHover; });
                gbOneRow.RegisterCallback<MouseLeaveEvent>(evt => { gbOneRow.style.backgroundColor = backgroundcolorSaved; });

                // Add a click event listener to the button.
                gbOneRow.RegisterCallback<ClickEvent>(evt =>
                {
                    //Debug.Log($"click on row {demo.SceneName} {((VisualElement)evt.currentTarget).name}");

                    if (Application.isPlaying)
                        SceneManager.LoadScene(SceneUtility.GetBuildIndexByScenePath(demo.SceneName), LoadSceneMode.Single);
                    else
                    {
#if UNITY_EDITOR //NOT_AVAILABLE_OUT_OF_EDITOR_FOLDER
                        string freepro = demo.Version == "Free" ? "FreeDemos" : "ProDemos";
                        string scenePath = $"Assets/MidiPlayer/Demo/{freepro}/{demo.SceneName}.unity";
                        try
                        {
                            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                                EditorSceneManager.OpenScene(scenePath);
                        }
                        catch (Exception ex)
                        {
                            Debug.Log(ex.ToString());
                            //PopupWindow.Show(new Rect(100, 100, 180, 18), new GetFullVersion());
                        }
#endif
                    }
                });
            }
        }
    }
}
