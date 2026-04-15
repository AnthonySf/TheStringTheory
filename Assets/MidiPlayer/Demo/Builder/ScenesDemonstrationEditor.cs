// Uncomment to check mode editor
// Comment to build
//#define CHECK_MODE_EDITOR

//#if CHECK_MODE_EDITOR
 
// Was just to learn UI Toolbox and check possibility to share script and UXML between mode Extension Editor and Runtime
// result: 
//  It's possible for UI Toolkit, just load UXML "manually" (AssetDatabase.LoadAssetAtPath)
//  but EditorSceneManager.OpenScene not available out of Editor folder
//  and not possible to create a build

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;

namespace MidiPlayerTK
{
    public class ScenesDemonstrationEditor : EditorWindow
    {
        static ScenesDemonstrationEditor window;
        Demonstrator loadedDemos;

        [MenuItem(Constant.MENU_MAESTRO + "/Load Demonstration &D", false, 50)]
        public static void ShowWindow()
        {
            window = GetWindow<ScenesDemonstrationEditor>(true, "Demonstration Loader - Version: " + Constant.version);
            if (window != null)
            {
                window.minSize = new Vector2(200, 300);
                window.Show();
            }
        }

        public void OnEnable()
        {
            loadedDemos = new Demonstrator();
            loadedDemos.LoadCSV();

            // Each editor window contains a root VisualElement object but empty.
            loadedDemos.Root = this.rootVisualElement;

            // Load the UXML document from Assets/MidiPlayer/Demo/Builder/Resources
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/MidiPlayer/Demo/Builder/Resources/ScenesDemonstration.uxml");
            // Clone the visual tree into the root.
            var uxml = visualTree.CloneTree();

            loadedDemos.Root.Add(uxml);

            /// Find all visual elements and apply content or enable callbacks.
            /// Works the same in Editor and Runtime modes.
            loadedDemos.FindVisualComponent();

            // Load the row template
            loadedDemos.RowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/MidiPlayer/Demo/Builder/Resources/OneRowDemo.uxml");

            int index = 1;
            foreach (Demonstrator demo in loadedDemos.Demos)
            {
                loadedDemos.AddRow(demo, index++, 0);
            }
        }
    }
}
#endif
