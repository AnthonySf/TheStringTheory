using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MidiPlayerTK;

namespace DemoMVP
{
    /// <summary>@brief
    /// This demo is able to load a MIDI file only by script.\n
    /// There is nothing to create in the Unity editor, just add this script to a GameObject in your scene and run!\n
    /// You could also:\n 
    ///     Add a MidiFilePlayer prefab in your hierarchy\n
    ///     Create this attribute in your class:\n
    ///         public MidiFilePlayer midiFilePlayer;\n
    ///     In the Unity Start()\n
    ///         midiFilePlayer = FindFirstObjectByType<MidiFilePlayer>();\n
    /// </summary>
    public class TheSimplestMidiLoader : MonoBehaviour
    {
        MidiFilePlayer midiFilePlayer;

        private void Awake()
        {
            Debug.Log("Awake: dynamically add MidiFilePlayer component (only for loading a MIDI file)");

            // MidiPlayerGlobal is a singleton: only one instance can be created. 
            if (MidiPlayerGlobal.Instance == null)
                gameObject.AddComponent<MidiPlayerGlobal>();

            // When running, this component will be added to this gameObject
            midiFilePlayer = gameObject.AddComponent<MidiFilePlayer>();
            midiFilePlayer.MPTK_PlayOnStart = false;
        }

        public void Start()
        {
            Debug.Log("Start: select a MIDI file and load MIDI events.");

            // Select a MIDI from the MIDI DB (with exact name)
            midiFilePlayer.MPTK_MidiName = "Bach - Fugue";

            // Load the MIDI file
            // v2.10.1 - Migration from MidiFileLoader to MidiFilePlayer
            //   - MidiFileLoader.MPTK_Load was returning a boolean
            //   - MidiFilePlayer.MPTK_Load now return an instance of MidiLoad
            MidiLoad midiLoaded = midiFilePlayer.MPTK_Load();
            if (midiLoaded != null)
            {
                // MIDI loaded
                Debug.Log($"Loading '{midiFilePlayer.MPTK_MidiName}', MIDI events count:{midiLoaded.MPTK_MidiEvents.Count}");
            }
            else
                Debug.Log($"Loading '{midiFilePlayer.MPTK_MidiName}' - Error");
        }
    }
}
