using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MidiPlayerTK
{
    public class ScenesDemonstrationMono : MonoBehaviour
    {
        private Demonstrator loadedDemos;

        void Start()
        {
            loadedDemos = new Demonstrator();
            loadedDemos.LoadCSV();

            // Load Assets/MidiPlayer/Demo/Builder/Resources/ScenesDemonstration.uxml,
            // defined in the "UI Document" component added to the SceneHandler GameObject.
            loadedDemos.Root = GetComponent<UIDocument>().rootVisualElement;

            // The SceneHandler GameObject can hold only one "UI Document" component.
            // Need to load the template UXML manually.
            loadedDemos.RowTemplate = Resources.Load<VisualTreeAsset>("OneRowDemo");

            /// Find all visual elements and apply content or enable callbacks.
            /// Works the same in Editor and Runtime modes.
            loadedDemos.FindVisualComponent();


            int index = 1;
            foreach (Demonstrator demo in loadedDemos.Demos)
            {
                // Keep only scenes defined in Build Settings, plus the first one
                // (which must be the demo scene itself).
                if (index == 1 || SceneUtility.GetBuildIndexByScenePath(demo.SceneName) >= 0)
                    loadedDemos.AddRow(demo, index++, 0);
            }
        }
    }
}
