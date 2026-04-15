using MidiPlayerTK;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemoMVPSwitchScene
{

    public class SwitchSceneController : MonoBehaviour
    {

        private void Awake()
        {
            Debug.Log($"Awake: SwitchSceneController Active Scene {SceneManager.GetActiveScene().name} ");
            // Add listeners to be called when a scene is loaded or changed
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Listener for sceneLoaded
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"OnSceneLoaded '{scene.name}'");
        }

        private void OnActiveSceneChanged(Scene prevscene, Scene scene)
        {
            Debug.Log($"OnActiveSceneChanged '{prevscene.name}' to '{scene.name}'");
        }

        // Start is called before the first frame update
        void Start()
        {
        }
        private void OnDestroy()
        {
            Debug.Log("OnDestroy: SwitchSceneController");

            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;

        }
        // **** Don't forget to add the scene SwitchSceneChild to your "Scenes in Build" in the build settings ****
        // Linked to UI button on the scene
        public void LoadSceneChild()
        {
            Debug.Log("LoadSceneChild: <b>the MidiFilePlayer is still playing</b>");
            Debug.LogWarning("When running in Unity Editor, possible some 'glitch' with the MIDI Player when switching.");
            Debug.LogWarning("Luckiliy, nothing weird detected from a built application, thanks to IL2CPP.");
            SceneManager.LoadScene("SwitchSceneChild", LoadSceneMode.Single);
        }

        // **** Don't forget to add the scene SwitchScene to your "Scenes in Build" in the build settings ****
        // Linked to UI button on the scene
        public void LoadSceneHome()
        {
            Debug.Log("LoadSceneHome: <b>the MidiFilePlayer is still playing</b>");
            Debug.LogWarning("When running in Unity Editor, possible some 'glitch' with the MIDI Player when switching.");
            Debug.LogWarning("Luckiliy, nothing weird detected from a built application, thanks to IL2CPP.");
            SceneManager.LoadScene("SwitchScene", LoadSceneMode.Single);
        }

        // Update is called once per frame
        void Update()
        {
        }
    }
}
