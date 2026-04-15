using DemoMPTK;
using MidiPlayerTK;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UI;

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

    public class MidiChannelDisplay : MonoBehaviour
    {
        // UI Text element to display channel information
        public Text ChannelInfo;

        // Reference to the MidiFilePlayer prefab in the scene
        public MidiFilePlayer midiPlayer;

        // StringBuilder to accumulate channel information for display,
        // better than string concatenation in a loop which is more efficient and lower memory usage.
        private StringBuilder logChannelInfo;

        private void Awake()
        {
            // Search for an existing MidiFilePlayer prefab in the scene
            midiPlayer = FindFirstObjectByType<MidiFilePlayer>();

            if (midiPlayer == null)
                Debug.Log("No MidiFilePlayer Prefab found in the current Scene Hierarchy.");

            logChannelInfo = new StringBuilder(512);
        }

        public void Update()
        {
            if (Time.frameCount % 10 == 0)
            {
                // Update the channel display every 10 frames
                BuildInfoChannels();
                ChannelInfo.text = logChannelInfo.ToString();
            }
        }
        public void BuildInfoChannels()
        {
            logChannelInfo.Clear();

            for (int channel = 0; channel < midiPlayer.MPTK_Channels.Length; channel++)
            {
                // Display only channel with activity
                if (midiPlayer.MPTK_Channels[channel].NoteCount > 0)
                {
                    // Display channel number
                    logChannelInfo.Append($"Channel: {channel: 00}");

                    // Channel enabled?
                    logChannelInfo.Append($" Enable: {(midiPlayer.MPTK_Channels[channel].Enable ? "ON " : "OFF")}");

                    // Display preset, number and name
                    int presetNum = midiPlayer.MPTK_Channels[channel].PresetNum;
                    logChannelInfo.Append($" Preset: {presetNum: 000} {(midiPlayer.MPTK_Channels[channel].PresetName ?? "not set"),-20}");

                    // Sustain enabled?
                    bool sustain = midiPlayer.MPTK_Channels[channel].Controller(MPTKController.Sustain) < 64 ? false : true;
                    logChannelInfo.Append($" Sustain: {(sustain ? "ON " : "OFF")}");

                    // How many notes played on this channel?
                    logChannelInfo.Append($" Count: {midiPlayer.MPTK_Channels[channel].NoteCount,5}");
                    logChannelInfo.AppendLine();
                }
            }
        }
    }

}

