using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MidiPlayerTK;

namespace DemoMVP
{
    /// <summary>
    /// This is intended to be the "Hello, World!" equivalent for Maestro.
    /// 
    /// This code assumes that the Scene contains:
    ///  * A MidiStreamPlayer added via the Inspector.
    /// 
    /// This code initializes MPTK engine and creates the objects necessary to play a note when the spacebar is
    /// pressed, stopping the note when the spacebar is released.
    /// </summary>
    public class TheSimplestMidiGenerator : MonoBehaviour
    {
        // This class is able to play MIDI event: play note, play chord, patch change, apply effect, ... see doc!
        // https://mptkapi.paxstellar.com/d9/d1e/class_midi_player_t_k_1_1_midi_stream_player.html
        public MidiStreamPlayer midiStreamPlayer;   // Initialized at Start() or could be set in the Inspector

        // Description of the MIDI event which will hold the description of the note to played and 
        // information about the samples when playing.
        // https://mptkapi.paxstellar.com/d9/d50/class_midi_player_t_k_1_1_m_p_t_k_event.html
        private MPTKEvent mptkEvent;

        // Start is called before the first frame update
        void Start()
        {
            Debug.Log("Start: dynamically load MidiStreamPlayer prefab from the hierarchy.");
            midiStreamPlayer = FindFirstObjectByType<MidiStreamPlayer>();
            if (midiStreamPlayer == null)
                Debug.LogWarning("Can't find a MidiStreamPlayer Prefab in the current scene hierarchy. You can add it with the Maestro menu in Unity editor.");
            else
                Debug.Log("<color=green>Use key <Space> to play a note.</color>");
        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                // Assign our "Hello, World!" (using MPTKEvent's defaults value, so duration = -1 for an infinite note playing
                // Value = 60 for playing a C5 (HelperNoteLabel class could be your friend)
                mptkEvent = new MPTKEvent() { Value = 60 };

                // Start playing our "Hello, World!" note C5
                midiStreamPlayer.MPTK_PlayEvent(mptkEvent);
            }
            
            if (Input.GetKeyUp(KeyCode.Space))
            {
                // Stop playing our "Hello, World!" note C5
                midiStreamPlayer.MPTK_StopEvent(mptkEvent);
            }
        }
    }
}
