using MidiPlayerTK;
using System.Collections.Generic;
using UnityEngine;

namespace DemoMVP
{

    /// <summary>@brief
    /// Load a MIDI and restart playing between two bar positions (not using MPTKInnerLoop). 
    /// Icing on the cake: filtering notes played by channel. 
    /// 
    /// As usual with a MVP demo, focus is on the essentials: no value check, no error catch, no optimization, limited functions ...
    /// 
    /// </summary>
    public class MidiLoop : MonoBehaviour
    {
        [Header("A MidiFilePlayer prefab must exist in the hierarchy")]
        /// <summary>@brief
        /// MPTK component able to play a Midi file from your list of Midi file. This PreFab must be present in your scene.
        /// </summary>
        public MidiFilePlayer midiFilePlayer;

        [Header("Set MIDI bar start and stop")]

        /// <summary>@brief
        /// Bar to start playing. Change value in the Inspector.
        /// </summary>
        public int StartBar;

        /// <summary>@brief
        /// Bar where to restart playing. Change value in the Inspector.
        /// </summary>
        public int EndBar;

        /// <summary>@brief
        /// Play only notes from this channel. -1 for playing all channels. Change value in the Inspector.
        /// </summary>
        [Header("Play only this channel if greater or equal to 0")]
        [Range(-1, 15)]
        public int ChannelSelected;

        //! [ExampleFindPlayerAndAddListener]

        // Start is called before the first frame update
        void Start()
        {
            // Find existing MidiFilePlayer in the scene hierarchy
            // ---------------------------------------------------

            midiFilePlayer = FindFirstObjectByType<MidiFilePlayer>();
            if (midiFilePlayer == null)
            {
                Debug.LogWarning("Can't find a MidiFilePlayer Prefab in the current Scene Hierarchy. Add it with the Maestro menu.");
                return;
            }
            midiFilePlayer.MPTK_PlayOnStart = false;

            // Set Listeners 
            // -------------

            // triggered when MIDI starts playing (Indeed, will be triggered at every restart)
            midiFilePlayer.OnEventStartPlayMidi.AddListener(StartPlay);

            // triggered when MIDI ends playing (Indeed, will be triggered at every end of restart)
            midiFilePlayer.OnEventEndPlayMidi.AddListener(EndPlay);

            // triggered every time a group of MIDI events are ready to be played by the MIDI synth.
            midiFilePlayer.OnEventNotesMidi.AddListener(MidiReadEvents);

            LoadAndPlay();
        }
        //! [ExampleFindPlayerAndAddListener]

        //! [ExampleChannelEnabled]

        /// <summary>@brief
        /// Start playing MIDI: MIDI File is loaded and Midi Synth is initialized, but so far any MIDI event has been read.
        /// This is the right time to defined some specific behaviors. 
        /// </summary>
        /// <param name="midiname"></param>
        public void StartPlay(string midiname)
        {

            // Enable or disable MIDI channel. it's not possible before because channel are not yet allocated
            for (int channel = 0; channel < midiFilePlayer.MPTK_Channels.Length; channel++)
                // Enable only ChannelSelected or all channels if ChannelSelected equal -1 
                if (channel == ChannelSelected || ChannelSelected == -1)
                    // v2.10.1 midiFilePlayer.MPTK_ChannelEnableSet(channel, true);
                    midiFilePlayer.MPTK_Channels[channel].Enable= true;
                else
                    // Disable this channel
                    // v2.10.1 midiFilePlayer.MPTK_ChannelEnableSet(channel, false);
                    midiFilePlayer.MPTK_Channels[channel].Enable = false;

            Debug.Log($"<color=green>Start at tick:{midiFilePlayer.MPTK_TickCurrent}</color>" +
                $" MPTK_DeltaTicksPerQuarterNote:{midiFilePlayer.MPTK_MidiLoaded.MPTK_DeltaTicksPerQuarterNote}" +
                $" MPTK_NumberBeatsMeasure:{midiFilePlayer.MPTK_MidiLoaded.MPTK_NumberBeatsMeasure}" +
                $" MPTK_NumberQuarterBeat:{midiFilePlayer.MPTK_MidiLoaded.MPTK_NumberQuarterBeat}");
        }

        //! [ExampleChannelEnabled]

        /// <summary>
        /// End or restart playing MIDI detected.
        /// </summary>
        /// <param name="midiname"></param>
        /// <param name="reason"></param>
        public void EndPlay(string midiname, EventEndMidiEnum reason)
        {
            Debug.Log($"<color=red>Replay at tick {ConvertBarToTick(EndBar)}</color> {reason}");
        }

        /// <summary>@brief
        /// Triggered by the listener when midi notes are available from MidiFilePlayer. 
        /// warning: when the events are reveived it's too late to stop playing them, they are already in the pipeline of the synth.
        /// Maestro MPTK Pro is able to 
        /// </summary>
        public void MidiReadEvents(List<MPTKEvent> midiEvents)
        {
            // warning: when the events are reveived with OnEventNotesMidi it's too late to stop playing them, 
            // because they are already in the pipeline of the MIDI synth or even already played!
            // Rather use OnMidiEvent (from Maestro pro) for processing MIDI events before they are sent to the synth. 
            // https://mptkapi.paxstellar.com/d3/d1d/class_midi_player_t_k_1_1_midi_synth.html#a1ff7a431b64a01a3ce800351461e2241

            Debug.Log($"OnEventNotesMidi: midiEvents.Count:{midiEvents.Count}  MPTK_TickCurrent:{midiFilePlayer.MPTK_TickCurrent}");
            midiEvents.ForEach(midiEvent =>
            {
                if (midiEvent.Command == MPTKCommand.NoteOn)
                    Debug.Log(midiEvent.ToString());
            });
        }

        //! [ExampleMidiLoop]
        // Full source code in MidiLoop.cs
        void LoadAndPlay()
        {
            // Preload the MIDI file to be able to set MIDI attributes before playing
            midiFilePlayer.MPTK_Load();
            SetLoopingMode();
            midiFilePlayer.MPTK_Play(alreadyLoaded: true);
        }

        private void SetLoopingMode()
        {
            // Set start / end position by bar
            midiFilePlayer.MPTK_MidiLoaded.MPTK_TickStart = ConvertBarToTick(StartBar);
            midiFilePlayer.MPTK_MidiLoaded.MPTK_TickEnd = ConvertBarToTick(EndBar);
            midiFilePlayer.MPTK_ModeStopVoice = MidiFilePlayer.ModeStopPlay.StopWhenAllVoicesReleased;
            midiFilePlayer.MPTK_MidiAutoRestart = true;
        }

        // Convert a bar number (musical score concept) to a tick position (MIDI concept).\n
        long ConvertBarToTick(int bar)
        {
            // was MPTK_NumberQuarterBeat, replaced by MPTK_NumberBeatsMeasure 
            return (long)(bar * midiFilePlayer.MPTK_MidiLoaded.MPTK_NumberBeatsMeasure * midiFilePlayer.MPTK_MidiLoaded.MPTK_DeltaTicksPerQuarterNote);
        }
        //! [ExampleMidiLoop]

        // Update is called once per frame
        void Update()
        {
            // Set start / end position (update for change)
            midiFilePlayer.MPTK_MidiLoaded.MPTK_TickStart = ConvertBarToTick(StartBar);
            midiFilePlayer.MPTK_MidiLoaded.MPTK_TickEnd = ConvertBarToTick(EndBar);
        }
    }
}
