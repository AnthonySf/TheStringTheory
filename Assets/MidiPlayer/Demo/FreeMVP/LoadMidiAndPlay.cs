using MidiPlayerTK;
using UnityEngine;

namespace DemoMVP
{
    //! [LoadMidiAndPlay]
    public class LoadMidiAndPlay : MonoBehaviour
    {
        // This demo enables you to load a MIDI file before playing it, modify some MIDI events, and then play the modified MIDI.
        // It also demonstrates how to change the tempo using three methods:
        //  1) From the MidiFilePlayer inspector, under the "Show MIDI Parameters" tab, adjust the speed (default is 1).
        //  2) Programmatically, at any time when the MIDI is playing, change the tempo using midiFilePlayer.MPTK_Tempo = bpm (bpm must be > 0).
        //  3) Programmatically, load the MIDI, modify the MIDI tempo events, and then play.
        //
        // This demo showcases the third method.
        // In your Unity scene:
        //     - Add a MidiFilePlayer prefab to your scene.
        //     - In the MidiFilePlayer inspector:
        //         - Select the MIDI file you wish to load.
        //         - Uncheck "Automatic MIDI Start".
        //     - Attach this script to an existing GameObject (whether empty or not).

        // MPTK component for playing a MIDI file. 
        // You can set it in the inspector or let this script find it automatically.
        public MidiFilePlayer midiFilePlayer;

        private void Awake()
        {
            // Find a MidiFilePlayer added to the scene or set it directly in the inspector.
            if (midiFilePlayer == null)
                midiFilePlayer = FindFirstObjectByType<MidiFilePlayer>();
        }

        void Start()
        {
            if (midiFilePlayer == null)
            {
                Debug.LogWarning("No MidiFilePlayer Prefab found in the current Scene Hierarchy. See 'Maestro / Add Prefab' in the menu.");
            }
            else
            {
                // Index of the MIDI file from the MIDI database (find it using 'Midi File Setup' from the Maestro menu).
                // Optionally, the MIDI file to load can also be defined in the inspector. Uncomment to select the MIDI programmatically.
                // midiFilePlayer.MPTK_MidiIndex = 0;

                // Load the MIDI without playing it.
                MidiLoad midiloaded = midiFilePlayer.MPTK_Load();

                if (midiloaded != null)
                {
                    Debug.Log($"Duration: {midiloaded.MPTK_Duration.TotalSeconds} seconds, Initial Tempo: {midiloaded.MPTK_InitialTempo}, MIDI Event Count: {midiloaded.MPTK_ReadMidiEvents().Count}");

                    foreach (MPTKEvent mptkEvent in midiloaded.MPTK_MidiEvents)
                    {
                        if (mptkEvent.Command == MPTKCommand.MetaEvent && mptkEvent.Meta == MPTKMeta.SetTempo)
                        {
                            // The value contains Microseconds Per Beat, convert it to BPM for clarity.
                            double bpm = MPTKEvent.QuarterPerMicroSecond2BeatPerMinute(mptkEvent.Value);
                            // Double the tempo and convert back to Microseconds Per Beat.
                            mptkEvent.Value = MPTKEvent.BeatPerMinute2QuarterPerMicroSecond(bpm * 2);
                            Debug.Log($"   Tempo doubled at tick position {mptkEvent.Tick} and {mptkEvent.RealTime / 1000f:F2} seconds. New tempo: {MPTKEvent.QuarterPerMicroSecond2BeatPerMinute(mptkEvent.Value)} BPM");
                        }
                    }

                    // Start playback.
                    midiFilePlayer.MPTK_Play(alreadyLoaded: true);
                }
            }
        }
    }
    //! [LoadMidiAndPlay]
}
