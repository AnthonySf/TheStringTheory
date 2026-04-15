using DemoMPTK;
using MidiPlayerTK;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DemoMVP
{
    /// <summary>@brief
    /// This Minimum Viable Product focuses on the essentials of a Maestro feature. 
    /// Only a few functions are presented. Links to the documentation are provided for further exploration.
    /// As a result, error handling is intentionally minimal, the user interface is lightweight, and Unity setup is reduced. 
    /// 
    /// The goal is to learn how to use the Maestro API, then progress by building more complex applications.
    /// Maestro is based on prefabs (MidiFilePlayer, MidiStreamPlayer, ...) that must be added to your Unity scene hierarchy.
    /// In these demos, prefabs are created by script to reduce editor manipulations. 
    /// In production, it is recommended to create prefabs in the Unity Editor to take advantage of the Inspector and its many accessible parameters.
    /// 
    /// Functionality Demonstrated:
    /// - Enable or disable channels when a MIDI is playing. 
    /// - Keep only the drum channel playing.
    /// - Playback of a script-defined list of notes.
    ///
    /// How to Use:
    /// 1. Add a MidiFilePlayer prefab to your scene (right click on the Hierarchy Tab, menu Maestro)
    /// 2. Add an empty GameObject to your Unity Scene and attach this script to the GameObject.
    /// 3. Run the scene and change the value of HanfChannel in the inspector
    /// 
    /// Documentation References:
    /// - MIDI File Player: https://paxstellar.fr/midi-file-player-detailed-view-2/
    /// - MPTKChannel: https://mptkapi.paxstellar.com/da/dc1/class_midi_player_t_k_1_1_m_p_t_k_channel.html
    /// 
    /// </summary>

    public class MidiChannelWithExtraNotes : MonoBehaviour
    {
        // Change from false to true to hang
        public bool HangChannel;

        // Channel to play extra notes when HangChannel is et to true
        public int ChannelScriptedNotes = 8;

        // Channel to enable as solo
        public int ChannelDrum = 9;

        // For display note count by channel
        public int[] NoteCountByChannel;

        private bool lastHangChannel;


        [Serializable]
        public class ScriptedNote { public int Value; public long Delay; public long Duration; }
        public List<ScriptedNote> ExtraNotes;// = new List<ScriptedNote>();

        public MidiFilePlayer midiPlayer;

        private void Awake()
        {
            // Search for an existing MidiFilePlayer prefab in the scene
            midiPlayer = FindFirstObjectByType<MidiFilePlayer>();

            if (midiPlayer == null)
                Debug.Log("No MidiFilePlayer Prefab found in the current Scene Hierarchy.");

            // ----------- just for display in the inspector the count of notes -------------------

            // triggered when MIDI starts playing (Indeed, will be triggered at every restart)
            midiPlayer.OnEventStartPlayMidi.AddListener(info => { NoteCountByChannel = new int[16]; });

            // triggered every time a group of MIDI events are ready to be played by the MIDI synth.
            midiPlayer.OnEventNotesMidi.AddListener(midiEvents =>
            {
                midiEvents.ForEach(midiEvent =>
                {
                    if (midiEvent.Command == MPTKCommand.NoteOn && midiEvent.Channel >= 0 && midiEvent.Channel < 16)
                        // Count only if channel is enabled
                        if (midiPlayer.MPTK_Channels[midiEvent.Channel].Enable)
                            NoteCountByChannel[midiEvent.Channel]++;
                });
            });
        }

        public void Start()
        {
            // Commented to be able to defined list of notes from the inspector 
            //ExtraNotes = new List<ScriptedNote>
            //{
            //    new ScriptedNote() { value = 60, delay = 0, duration = 500 }, 
            //    new ScriptedNote() { value = 62, delay = 100, duration = 500 },
            //    new ScriptedNote() { value = 67, delay = 200, duration = 500 },
            //    new ScriptedNote() { value = 58, delay = 300, duration = 500 },
            //};

            lastHangChannel = HangChannel;
        }

        public void Update()
        {
            // Avoid changing channels properties at every frame
            if (lastHangChannel != HangChannel)
            {
                lastHangChannel = HangChannel;
                if (HangChannel)
                    HangChannels();
                else
                    FreeChannels();
            }

            // Display notes count by channel
        }
        public void FreeChannels()
        {
            // All channels enabled to play
            midiPlayer.MPTK_Channels.EnableAll = true;
        }

        public void HangChannels()
        {
            ChannelScriptedNotes= Mathf.Clamp(ChannelScriptedNotes, 0, 15);

            // Channel change and extra notes can be applied only if the MIDI is playing
            if (midiPlayer != null && midiPlayer.MPTK_IsPlaying)
            {
                // Send the notes to the synth before deactivating the channel,
                // otherwise they will not be played!
                if (ExtraNotes != null)
                    foreach (ScriptedNote note in ExtraNotes)
                        if (note.Value > 0)
                            midiPlayer.MPTK_PlayDirectEvent(new MPTKEvent()
                            {
                                Channel = ChannelScriptedNotes,
                                Command = MPTKCommand.NoteOn,
                                Value = note.Value,
                                Delay = note.Delay,
                                Duration = note.Duration
                            });

                ChannelDrum = Mathf.Clamp(ChannelDrum, 0, 15);

                for (int i = 0; i < 16; i++)
                {
                    if (i != ChannelDrum)
                    {
                        // Disable all channels but not drum: new notes read are not send to the MIDI synth.
                        midiPlayer.MPTK_Channels[i].Enable = false;

                        // Send not-off all channels but not drum. Note-off has no effect on channel sustained.
                        // So, current notes already playing on the ChannelToSustain continue playing.
                        midiPlayer.MPTK_PlayDirectEvent(new MPTKEvent() { Channel = i, Command = MPTKCommand.NoteOff });
                    }
                }
            }
        }
    }
}

