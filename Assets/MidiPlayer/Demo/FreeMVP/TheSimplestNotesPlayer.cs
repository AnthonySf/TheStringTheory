using UnityEngine;
using MidiPlayerTK;

namespace DemoMVP
{
    /// <summary>
    /// Functionality Demonstrated:
    /// - Plays a single C5 note when the space key is pressed.
    /// - Stops the note when the space key is released.
    /// 
    /// How to Use:
    /// 1. Add an empty GameObject to your Unity Scene and attach this script to the GameObject.
    /// 2. Add a MidiStreamPlayer prefab to your scene (right click on the Hierarchy Tab, menu Maestro)
    /// 3. Run the scene and press the space key to play the C5 note.
    /// 
    /// Documentation References:
    /// - MIDI Stream Player: https://paxstellar.fr/midi-file-player-detailed-view-2-2/
    /// - MPTKEvent: https://mptkapi.paxstellar.com/d9/d50/class_midi_player_t_k_1_1_m_p_t_k_event.html
    /// 
    /// A Minimum Viable Product (MVP) that focuses on the essentials of the Maestro API functionality. 
    /// This script demonstrates only a few basic functions to help users get started with the API.
    /// 
    /// Features and Approach:
    /// - Error handling is minimal, the user interface is basic, and manipulations in Unity are minimized.
    /// - Prefabs like `MidiFilePlayer` and `MidiStreamPlayer` are essential components of Maestro. 
    ///   While this demo creates these prefabs via script, it is recommended to add them in the Unity editor 
    ///   for real projects to take full advantage of the Inspector's parameters.
    /// 
    /// </summary>
    public class TheSimplestNotesPlayer : MonoBehaviour
    {
        // MidiStreamPlayer is a class that can play MIDI events such as notes, chords, patch changes, and effects.
        private MidiStreamPlayer midiStreamPlayer;

        // MPTKEvent is a class that describes MIDI events such as notes to play.
        private MPTKEvent mptkEvent;

        private void Awake()
        {
            // Look for an existing MidiStreamPlayer prefab in the scene
            midiStreamPlayer = FindFirstObjectByType<MidiStreamPlayer>();

            if (midiStreamPlayer == null)
            {
                // If no MidiStreamPlayer prefab is found, create it dynamically
                Debug.Log("No MidiStreamPlayer Prefab found in the current Scene Hierarchy.");
                Debug.Log("A new MidiStreamPlayer prefab will be created via script. For production, add it manually to the scene!");

                // Create an empty GameObject to hold the Maestro-related prefab
                GameObject go = new GameObject("HoldsMaestroPrefab");

                // Add the MidiPlayerGlobal component to manage the SoundFont. This is a singleton, so only one instance will be created.
                go.AddComponent<MidiPlayerGlobal>();

                // Add the MidiStreamPlayer prefab to the GameObject
                midiStreamPlayer = go.AddComponent<MidiStreamPlayer>();

                // *** Configure essential parameters for the player ***

                // Enable the internal core player for smooth playback
                midiStreamPlayer.MPTK_CorePlayer = true;

                // Enable logging of MIDI events in the Unity Console
                // Use a monospace font in the Console for better readability
                midiStreamPlayer.MPTK_LogEvents = true;

                // Ensure that MIDI events are sent directly to the player for playback
                midiStreamPlayer.MPTK_DirectSendToPlayer = true;
            }

            Debug.Log("Press the <Space> key to play a note.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                // Create an MPTKEvent to describe the note to be played
                // Value = 60 corresponds to the C5 note. Duration is set to -1 for infinite playback.
                mptkEvent = new MPTKEvent() { Value = 60 };

                // Start playing the C5 note
                midiStreamPlayer.MPTK_PlayEvent(mptkEvent);
            }

            if (Input.GetKeyUp(KeyCode.Space))
            {
                // Stop playing the C5 note
                midiStreamPlayer.MPTK_StopEvent(mptkEvent);
            }
        }
    }
}
