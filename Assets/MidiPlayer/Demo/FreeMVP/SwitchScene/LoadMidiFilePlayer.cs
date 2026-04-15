using MidiPlayerTK;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemoMVPSwitchScene
{

    public class LoadMidiFilePlayer : MonoBehaviour
    {

        /// <summary>
        // Access to the MidiFilePlayer from the static class and its singleton instance:
        // LoadMidiFilePlayer.Instance.midiFilePlayer.MPTK_Next();
        /// </summary>
        public MidiFilePlayer midiFilePlayer;

        /// <summary>
        /// Dynamically MidiFilePlayer loading or from a prefab in the hierarchy
        /// </summary>
        public bool dynamicMidiFilePlayerLoading;

        /// <summary>
        /// Singleton for this instance
        /// </summary>
        public static LoadMidiFilePlayer Instance;

        private void Awake()
        {
            // ----------- Mandatory for using the MidiFilePLayer from other gameobject ----------------
            // Create a singleton for this instance. Instance is static: reachable by all others classes
            // -----------------------------------------------------------------------------------------
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // ----------------- the most important ---------------------------------------------------
            // Don't destroy this gameobject (which holds the MidiFilePlayer when the scene is unloaded
            // ----------------------------------------------------------------------------------------
            DontDestroyOnLoad(gameObject);

            if (dynamicMidiFilePlayerLoading)
            {
                // With dynamic mode, the prefab MidiFilePlayer is created by script (uncheck in the inspector to test)
                Debug.Log("Dynamically add MidiFilePlayer components.");
                Debug.Log("Nevertheless, loading a MidiFilePlayer prefab from the hierarchy is also possible.");

                // MidiPlayerGlobal is a singleton: only one instance can be created. 
                if (MidiPlayerGlobal.Instance == null)
                    gameObject.AddComponent<MidiPlayerGlobal>();

                // When running, this component will be added to this gameObject. Set essential parameters.
                // Nevertheless loading a MidiFilePlayer prefab from the hierarchy is also possible.
                midiFilePlayer = gameObject.AddComponent<MidiFilePlayer>();
            }
            else
            {
                // The prefab MidiFilePlayer held by this gameobject is loaded from the scene (current demo)
                Debug.Log("Load MidiFilePlayer Prefab from hierarchy.");
                Debug.Log("Nevertheless, dynamically loading MidiFilePlayer components is also possible.");

                // Find it on the scene
                midiFilePlayer = FindFirstObjectByType<MidiFilePlayer>();
                if (midiFilePlayer == null)
                {
                    Debug.LogWarning("Can't find a MidiFilePlayer Prefab in the current Scene Hierarchy. Add it with the MPTK menu.");
                    return;
                }
            }

        }

        // Start is called before the first frame update
        void Start()
        {
            midiFilePlayer.MPTK_CorePlayer = true;
            midiFilePlayer.MPTK_DirectSendToPlayer = true;
            midiFilePlayer.MPTK_StartPlayAtFirstNote = true;
            midiFilePlayer.MPTK_MidiIndex = 4;
            midiFilePlayer.MPTK_Play();
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}
