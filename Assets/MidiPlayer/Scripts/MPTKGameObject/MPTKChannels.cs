using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace MidiPlayerTK
{
    /// @ingroup midi_channel_state
    /// <summary>
    /// Collection of MIDI channels managed by a MIDI synth.\n
    /// A standard MIDI synth uses 16 channels (0 to 15). Each channel keeps an independent state, including:
    ///     - Instrument (preset) and bank
    ///     - Volume
    ///     - Mute state (see Enable)
    ///     - Pitch bend and controller values
    ///
    /// MIDI messages target one channel at a time, allowing different instruments and settings to play simultaneously.
    /// In General MIDI, channel 9 (10 in 1-based MIDI notation) is conventionally used for percussion.
    ///
    /// @snippet TestMidiFilePlayerScripting.cs ExampleUsingChannelAPI_Full
    /// </summary>
    public class MPTKChannels : IEnumerable<MPTKChannel>
    {

        private List<MPTKChannel> Channels { get; set; }

        /// <summary>@brief 
        /// Number of channels in this collection. Usually 16 when reading a standard MIDI file.\n
        /// Higher values are possible for internal usage, but this is not MIDI-file compliant.
        /// </summary>
        public int Length { get { return Channels.Count; } }

        /// <summary>@brief 
        /// Enables or disables playback for all channels.
        /// @snippet TestMidiFilePlayerScripting.cs ExampleUsingChannelAPI_6
        /// </summary>
        public bool EnableAll
        {
            set
            {
                foreach (var c in Channels)
                    c.Enable = value;
            }
        }

        /// <summary>@brief 
        /// Sets the volume for all channels, from 0.0 to 1.0.
        /// </summary>
        public float VolumeAll
        {
            set
            {
                foreach (var c in Channels)
                    c.Volume = value;
            }
        }



        /// <summary>@brief 
        /// Indexer access to a channel in the synth.
        /// Channels are addressed from 0 to 15 in standard MIDI usage.
        /// @snippet TestMidiFilePlayerScripting.cs ExampleUsingChannelAPI_1
        /// </summary>
        /// <param name="channel">Zero-based channel index (typically 0 to 15).</param>
        /// <returns>The channel at the requested index, or null if out of range.</returns>
        public MPTKChannel this[int channel]
        {
            get
            {
                try
                {
                    return Channels[channel];
                }
                catch (Exception)
                {
                    Debug.LogError($"Error when trying access to MPTK_Channels, channel {channel}");
                    //if (Channels == null)
                    //    Debug.LogException(ex);
                }
                return null;
            }
            set
            {
                try
                {
                    Channels[channel] = value;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error when trying access to MPTK_Channels, channel {channel}");
                    if (Channels == null)
                        Debug.LogException(ex);
                }
            }
        }


        /// <summary>@brief
        /// Creates a channel collection for a synth.
        /// </summary>
        /// <param name="psynth">Target synth that owns these channels.</param>
        /// <param name="countChannel">Number of channels to create. Default is 16.</param>
        public MPTKChannels(MidiSynth psynth, int countChannel = 16)
        {
            if (psynth.VerboseChannel) Debug.Log($"Create {countChannel} channels for synth '{psynth.name}'");
            Channels = new List<MPTKChannel>();
            for (int i = 0; i < countChannel; i++)
                Channels.Add(new MPTKChannel(i, psynth));
        }

        /// <summary>@brief
        /// Parameterless constructor kept for serialization and code stripping preservation.
        /// </summary>
        [Preserve]
        public MPTKChannels()
        {
        }

        /// <summary>@brief 
        /// Enables channel-state reset when MIDI playback starts.\n
        /// If true (default), the channel list is reinitialized when the synth is reinitialized.
        /// Setting this to false can produce unexpected behavior with MidiFilePlayer, but may be useful with MidiStreamPlayer.
        /// @version 2.10.1
        /// </summary>
        public bool EnableResetChannel = true;


        /// <summary>@brief 
        /// Resets Maestro channel extension fields.
        ///     - LastBank is set to -1 (no last bank feature when reset)
        ///     - ForcedBank is disabled
        ///     - ForcedPreset is disabled
        ///     - Enable is set to true
        ///     - Volume is set to maximum (1)
        /// Other channel state, such as current preset, bank, and controllers, is not reset.
        /// @version 2.10.1
        /// </summary>
        /// <param name="channelNum">Channel index to reset. Use -1 (default) to reset all channels.</param>
        public void ResetExtension(int channelNum = -1)
        {
            //if (synth != null && synth.VerboseChannel) Debug.Log("Reset channel extension feature");

            if (channelNum < 0)
                foreach (MPTKChannel channel in Channels)
                {
                    channel.LastBank = -1; // V2.12.0 was 0
                    channel.ForcedBank = -1;
                    channel.ForcedPreset = -1;
                    // before 2.12.0 channel.LastBank = 0;
                    channel.LastPreset = 0;
                    channel.Enable = true;
                    channel.Volume = 1;
                }
            else if (channelNum < Channels.Count)
            {
                Channels[channelNum].LastBank = -1; // V2.12.0 was 0
                Channels[channelNum].ForcedBank = -1;
                Channels[channelNum].ForcedPreset = -1;
                // before 2.12.0  Channels[channelNum].LastBank = 0;
                Channels[channelNum].LastPreset = 0;
                Channels[channelNum].Enable = true;
                Channels[channelNum].Volume = 1;
            }
            else
                Debug.LogWarning($"MPTK_ResetChannels: channel number is incorrect, must be between 0 and {Channels.Count}");
        }

        public IEnumerator<MPTKChannel> GetEnumerator()
        {
            return Channels.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Channels.GetEnumerator();
        }
    }

    /// @ingroup midi_channel_state
    /// <summary>
    /// Runtime state for one MIDI channel in the synth.\n
    /// A channel contains independent settings used to render events on that channel:
    ///     - Current instrument (preset) and bank
    ///     - Volume
    ///     - Mute state (see Enable)
    ///     - Pitch bend and controller values
    ///
    /// Channels are zero-based in MPTK (0 to 15). In General MIDI, channel 9 is typically used for percussion.
    ///
    /// @snippet TestMidiFilePlayerScripting.cs ExampleUsingChannelAPI_One
    /// </summary>
    public class MPTKChannel
    {

        /* Field shift amounts for sfont_bank_prog bit field integer */
        //const int PROG_SHIFTVAL = 0;
        //const int BANK_SHIFTVAL = 8;
        //const int SFONT_SHIFTVAL = 22;

        //const int PROG_MASKVAL = 0x000000FF;  /* Bit 7 is used to indicate unset state */
        //const int BANK_MASKVAL = 0x003FFF00;
        //const int BANKLSB_MASKVAL = 0x00007F00;
        //const int BANKMSB_MASKVAL = 0x003F8000;
        //const uint SFONT_MASKVAL = 0xFFC00000;

        //public enum enumStyleBanq
        //{
        //    FLUID_BANK_STYLE_GM,  /**< GM style, bank = 0 always (CC0/MSB and CC32/LSB ignored) */
        //    FLUID_BANK_STYLE_GS, /**< GS style, bank = CC0/MSB (CC32/LSB ignored) */
        //    FLUID_BANK_STYLE_XG,  /**< XG style, bank = CC32/LSB (CC0/MSB ignored) */
        //    FLUID_BANK_STYLE_MMA, /**< MMA style bank = 128*MSB+LSB */
        //}
        //public enumStyleBanq StyleBanq = enumStyleBanq.FLUID_BANK_STYLE_GM;

        /// <summary>@brief
        /// Last preset used on this channel. Used when restoring after a forced preset.
        /// </summary>
        public int LastPreset;

        /// <summary>@brief
        /// Last bank used on this channel. Used when restoring after a forced bank.
        /// </summary>
        public int LastBank;

        /// <summary>@brief
        /// Zero-based channel number. Standard MIDI channels are 0 to 15; channel 9 is conventionally percussion.
        /// </summary>
        public int Channel { get { return channum; } }
        private int channum;

        private HiPreset hiPreset;
        public HiPreset HiPreset { get { return hiPreset; } }

        public byte[] key_pressure;               /**< MIDI polyphonic key pressure from [0;127] */
        public short channel_pressure;
        public short pitch_bend;
        public short pitch_wheel_sensitivity;

        // NRPN system 
        //int nrpn_select;
        // cached values of last MSB values of MSB/LSB controllers
        //byte bank_msb;

        // Maestro specifique
        private int forcedPreset; // forced preset for this channel
        private int forcedBank; // forced bank for this channel

        private int count; // countChannel of note-on for the channel
        private int banknum;
        // controller values
        public byte[] cc;
        private MidiSynth synth;
        //private ImSoundFont sfont;

        // the micro-tuning TO BE DONE ... one day
        //private fluid_tuning tuning;

        /* The values of the generators, set by NRPN messages, or by
         * fluid_synth_set_gen(), are cached in the channel so they can be
         * applied to future notes. They are copied to a voice's generators
         * in fluid_voice_init(), wihich calls fluid_gen_init().  */
        private double[] gens;

        /* By default, the NRPN values are relative to the values of the
         * generators set in the SoundFont. For example, if the NRPN
         * specifies an attack of 100 msec then 100 msec will be added to the
         * combined attack time of the sound font and the modulators.
         *
         * However, it is useful to be able to specify the generator value
         * absolutely, completely ignoring the generators of the sound font
         * and the values of modulators. The gen_abs field, is a boolean
         * flag indicating whether the NRPN value is absolute or not.
         */
        private bool[] gen_abs;

        public MPTKChannel(int channel, MidiSynth psynth)
        {
            channum = channel;
            synth = psynth;
            Enable = true;
            Volume = 1f;
            count = 0;
            forcedPreset = -1;
            forcedBank = -1;
            gens = new double[Enum.GetNames(typeof(fluid_gen_type)).Length];
            gen_abs = new bool[Enum.GetNames(typeof(fluid_gen_type)).Length];
            cc = new byte[128];
            key_pressure = new byte[128];
            hiPreset = null;
            //tuning = null;
            fluid_channel_init();
            fluid_channel_init_ctrl();

        }
        /// <summary>@brief 
        /// Builds a debug string that summarizes the channel state.\n
        /// Example output:
        /// @li Channel:2	Enabled	[Preset:18, Bank:0]		'Rock Organ'			Count:1		Volume:1
        /// @li Channel:4	Muted	[Preset:F44, Bank:0]	'Stereo Strings Trem'	Count:33	Volume:0,50
        /// </summary>
        /// <returns>Information string</returns>
        public override string ToString()
        {
            string info = "Channel:";

            string sPreset = "";
            if (ForcedPreset == -1)
            {
                // Preset not forced, get the preset defined on this channel by the Midi
                sPreset = PresetNum.ToString();
            }
            else
            {
                sPreset = $"F{ForcedPreset}";
            }

            string sMuted = Enable ? "Enabled" : "Muted";

            info += $"{Channel}\t";
            info += $"{sMuted}\t";
            info += $"[Preset:{sPreset}, ";
            info += $"Bank:{BankNum}]\t";
            info += $"'{PresetName}'\t";
            info += $"Count:{NoteCount}\t";
            info += $"Volume:{Volume:F2}\t";

            return info;
        }

        /// <summary>@brief 
        /// Enables (unmutes) or disables (mutes) this channel.
        /// All channels are unmuted when MIDI starts playing (MidiFilePlayer#MPTK_Play).\n
        /// To mute channels just before playback starts, use MidiFilePlayer#OnEventStartPlayMidi.
        /// 
        /// See demo: Assets\MidiPlayer\Demo\FreeMVP\MidiLoop.cs
        /// @snippet MidiLoop.cs ExampleFindPlayerAndAddListener
        /// 
        /// @snippet MidiLoop.cs ExampleChannelEnabled
        /// </summary>
        /// <returns>True if the channel is enabled.</returns>
        /// @snippet TestMidiFilePlayerScripting.cs ExampleUsingChannelAPI_7
        public bool Enable { get; set; }


        /// <summary>@brief 
        /// Gets or sets the number of NoteOn events played since MIDI start.
        /// You can also set it, for example to reset it to 0.
        /// </summary>
        public int NoteCount { get { return count; } set { count = value; } }

        /// <summary>@brief 
        /// Gets or sets channel volume, from 0.0 to 1.0.
        /// </summary>
        /// @snippet TestMidiFilePlayerScripting.cs ExampleUsingChannelAPI_5
        public float Volume { get; set; }

        private int prognum;

        /// <summary>@brief 
        /// Gets or sets the current preset number for this channel.\n
        /// Value must be between 0 and 127 (with a full GM SoundFont).
        /// Each MIDI channel can play a different preset and bank.\n
        /// You can inspect available banks and presets from Maestro / SoundFont Setup (right panel),
        /// then click the eye icon buttons.
        /// </summary>
        /// @snippet TestMidiStream.cs ExampleUsingChannelAPI_4
        public int PresetNum
        {
            get
            {
                return prognum;
            }
            set
            {
                //if (!HelperDemo.CheckSFExists())
                //{
                //    Debug.LogWarning($"MPTK_Channel[{Channel}].PresetNum - no SoundFont defined");
                //}
                //else
                if (value < 0 || value > 127)
                {
                    Debug.LogWarning($"MPTK_Channel[{Channel}].PresetNum out of range, must be between 0 and 127, found {value}");
                }
                else
                {
                    LastPreset = value;
                    fluid_synth_program_change(value);
                }
            }
        }

        /// <summary>@brief
        /// Gets or sets the current bank number for this channel.\n
        /// Each MIDI channel can play a different preset and bank.\n
        /// You can inspect available banks and presets from Maestro / SoundFont Setup (right panel),
        /// then click the eye icon buttons.
        /// </summary>
        /// @snippet TestMidiStream.cs ExampleUsingChannelAPI_4
        public int BankNum
        {
            get
            {
                return banknum;
            }
            set
            {
                if (value < 0 || value > 16383)
                {
                    Debug.LogWarning($"MPTK_Channel[{Channel}].BankNum out of range, must be between 0 and 16383, found {value}");
                }
                banknum = value;
            }
        }

        /// <summary>@brief 
        /// Gets the current preset name for this channel.\n
        /// Each MIDI channel can use a different preset.
        /// </summary>
        /// <returns>Preset name, or "no preset defined" if unavailable.</returns>
        /// @snippet TestMidiStream.cs ExampleUsingChannelAPI_3
        public string PresetName { get { return hiPreset != null ? hiPreset.Name : "no preset defined"; } }



        /// <summary>@brief 
        /// Gets or sets a forced preset for this channel.\n
        /// When set to a value >= 0, the channel always uses this preset, even if a Program Change is received.
        /// Set to -1 to disable this behavior.
        /// </summary>
        /// <returns>Preset index, or -1 when not forced.</returns>
        /// @snippet TestMidiFilePlayerScripting.cs ExampleUsingChannelAPI_2
        /// 
        public int ForcedPreset
        {
            get
            {
                return forcedPreset;
            }
            set
            {
                if (value >= 0)
                {
                    if (synth.VerboseChannel) Debug.Log($"ForcedPreset Channel:{Channel} Preset forced to:{value}");
                    forcedPreset = value;
                    fluid_synth_program_change(value);
                }
                else
                {
                    // If < 0  disable forced preset
                    forcedPreset = -1;
                    if (synth.VerboseChannel) Debug.Log($"ForcedPreset Channel:{Channel} Restore preset to:{LastPreset}");
                    fluid_synth_program_change(LastPreset);
                }
            }
        }

        /// <summary>@brief 
        /// Gets or sets a forced bank for this channel.\n
        /// When set to a value >= 0, the channel always uses this bank, even if a bank change message is received.
        /// Set to -1 to disable this behavior.
        /// </summary>
        /// <returns>Bank index, or -1 when not forced.</returns>
        /// @snippet TestMidiFilePlayerScripting.cs ExampleUsingChannelAPI_2
        public int ForcedBank
        {
            get
            {
                return forcedBank;
            }
            set
            {
                if (value >= 0)
                {
                    if (synth.VerboseChannel) Debug.Log($"ForcedBank Channel:{Channel} Force bank to:{value}");
                    forcedBank = value; // set to -1 to disable forced bank
                }
                else
                {
                    if (synth.VerboseChannel) Debug.Log($"ForcedBank Channel:{Channel} Restore bank to:{LastBank}");
                    // before 2.12.0 banknum = LastBank;
                    if (LastBank >= 0)
                        banknum = LastBank;
                    else
                    {
                        ImSoundFont sFont = synth.MPTK_SoundFont.SoundFont;
                        banknum = Channel == 9 ? sFont.DrumKitBankNumber : sFont.DefaultBankNumber;
                    }
                    forcedBank = -1;
                }
            }
        }

        private void fluid_channel_init()
        {
            prognum = 0;
            if (synth.MPTK_SoundFont.IsReady)
            {
                banknum = Channel == 9 ? synth.MPTK_SoundFont.SoundFont.DrumKitBankNumber : synth.MPTK_SoundFont.SoundFont.DefaultBankNumber;
                // Search default preset for the channel
                hiPreset = fluid_synth_find_preset(banknum, prognum);
            }
        }

        private void fluid_channel_init_ctrl()
        {
            /*
                @param is_all_ctrl_off if nonzero, only resets some controllers, according to
                https://www.midi.org/techspecs/rp15.php
                For MPTK: is_all_ctrl_off=0, all controllers will be reset
            */

            channel_pressure = 0;
            pitch_bend = 0x2000; // Range is 0x4000, pitch bend wheel starts in centered position
            pitch_wheel_sensitivity = 2; // two semi-tones 

            for (int i = 0; i < gens.Length; i++)
            {
                gens[i] = 0.0f;
                gen_abs[i] = false;
            }

            for (int i = 0; i < 128; i++)
            {
                cc[i] = 0;
                key_pressure[i] = 0;
            }

            //fluid_channel_clear_portamento(chan); /* Clear PTC receive */
            //chan->previous_cc_breath = 0;         /* Reset previous breath */
            /* Reset polyphonic key pressure on all voices */
            //for (i = 0; i < 128; i++)
            //{
            //    fluid_channel_set_key_pressure(chan, i, 0);
            //}

            /* Set RPN controllers to NULL state */
            cc[(int)MPTKController.RPN_LSB] = 127;
            cc[(int)MPTKController.RPN_MSB] = 127;

            /* Set NRPN controllers to NULL state */
            cc[(int)MPTKController.NRPN_LSB] = 127;
            cc[(int)MPTKController.NRPN_MSB] = 127;

            /* Expression (MSB & LSB) */
            cc[(int)MPTKController.Expression] = 127;
            cc[(int)MPTKController.EXPRESSION_LSB] = 127;

            /* Just like panning, a value of 64 indicates no change for sound ctrls */
            for (int i = (int)MPTKController.SOUND_CTRL1; i <= (int)MPTKController.SOUND_CTRL10; i++)
            {
                cc[i] = 64;
            }

            // Volume / initial attenuation (MSB & LSB) 
            cc[(int)MPTKController.VOLUME_MSB] = 100; // V2.88.2 before was 127
            cc[(int)MPTKController.VOLUME_LSB] = 0;

            // Pan (MSB & LSB) 
            cc[(int)MPTKController.Pan] = 64;
            cc[(int)MPTKController.PAN_LSB] = 0;

            cc[(int)MPTKController.BALANCE_MSB] = 64;
            cc[(int)MPTKController.BALANCE_LSB] = 0;

            /* Reverb */
            /* fluid_channel_set_cc (chan, EFFECTS_DEPTH1, 40); */
            /* Note: although XG standard specifies the default amount of reverb to
               be 40, most people preferred having it at zero.
               See https://lists.gnu.org/archive/html/fluid-dev/2009-07/msg00016.html */
        }

        /* was  fluid_channel_cc */
        /// <summary>@brief
        /// Reads or writes a controller value.
        /// <returns>Current value if reading, previous value if writing, or -1 on error.</returns>
        /// 
        /// @snippet TestMidiStream.cs ExampleAccessToControler
        /// </summary>
        /// <param name="numController">Controller to read or write</param>
        /// <param name="valueController">Value to set. Default is -1 to read only (no write).</param>
        public int Controller(MPTKController numController, int valueController = -1)
        {
            if ((int)numController < 0 || (int)numController > 127)
            {
                Debug.LogWarning($"MPTK_Channel[{Channel}].Controller out of range, must be between 0 and 127, found {numController} {(int)numController}");
                return -1;
            }
            if (valueController < 0)
            {
                return cc[(int)numController];
            }
            else
            {
                short previousValue = cc[(int)numController];
                cc[(int)numController] = (byte)valueController;

                if (synth.VerboseController || synth.VerboseChannel) Debug.Log($"MPTK_Channel[{Channel}].Controller Control:{numController} Value:{valueController} Previous:{previousValue}");

                switch (numController)
                {
                    case MPTKController.Sustain:
                        {
                            if (valueController < 64)
                            {
                                // printf("** sustain off\n");
                                // Send note-off to all active voices for this channel and if sustained
                                synth.fluid_synth_damp_voices(Channel);
                            }
                            else
                            {
                                // printf("** sustain on\n");
                                // cc[(int)numController] has been set to a valeur >= 64
                                // So future note-off will not be applied on this channel
                            }
                        }
                        break;

                    /* how the synthesizer interprets Bank Select messages. https://www.fluidsynth.org/api/settings_synth.html
                        GM: (General MIDI): This is the basic standard for MIDI files, ensuring that MIDI music will sound consistent across different GM-compatible synthesizers. 
                            It includes 128 standard instrument sounds plus some percussion sounds.
                        GS: (General Standard): Developed by Roland, GS extends the GM standard. It adds more instruments, effects (such as reverb and chorus), 
                            and other features for finer sound control. Specifically optimized for Roland synthesizers.

                        CC0: MSB
                        CC21: LSB

                        GM: ignores CC0 and CC32 messages.
                        GS: (default) CC0 becomes the bank number, CC32 is ignored.
                        MMA: bank is calculated as CC0*128+CC32.
                        XG: If CC0 is equal to 120, 126, or 127 then channel is set to drum and the bank number is set to 128 (CC32 is ignored).
                            Otherwise the channel is set to melodic and CC32 is the bank number. 

                        MPTK apply the GS bank number selection: CC0 becomes the bank number, CC32 is ignored.
                     */
                    case MPTKController.BankSelectMsb: // CC0
                        // ex value = 120 --> bank = 120
                        banknum = valueController & 0x7F;
                        LastBank = banknum;
                        // useless, we need to priorize bankselect before program change
                        // synth.fluid_synth_program_change(channum, prognum);
                        break;

                    case MPTKController.BankSelectLsb: // CC32
                        // v2.14.1: CC32 ignored (default is GS)
                        // ex value =0 and banknum=120 --> 15360
                        //banknum = banknum * 128 + (valueController & 0x7F);
                        //LastBank = banknum;
                        break;


                    case MPTKController.AllNotesOff:
                        synth.fluid_synth_noteoff(Channel, -1);
                        break;

                    case MPTKController.AllSoundOff:
                        synth.fluid_synth_soundoff(Channel);
                        break;

                    case MPTKController.ResetAllControllers:
                        fluid_channel_init_ctrl();
                        synth.fluid_synth_modulate_voices_all(Channel);
                        break;

                    case MPTKController.DATA_ENTRY_LSB: /* not allowed to modulate (spec SF 2.01 - 8.2.1) */
                        break;

                    case MPTKController.DATA_ENTRY_MSB: /* not allowed to modulate (spec SF 2.01 - 8.2.1) */
                        {
                            //int data = (valueController << 7) + cc[(int)MPTKController.DATA_ENTRY_LSB] ;

                            //if (chan->nrpn_active)   /* NRPN is active? */
                            //{
                            //    /* SontFont 2.01 NRPN Message (Sect. 9.6, p. 74)  */
                            //    if ((fluid_channel_get_cc(chan, NRPN_MSB) == 120)
                            //            && (fluid_channel_get_cc(chan, NRPN_LSB) < 100))
                            //    {
                            //        nrpn_select = chan->nrpn_select;

                            //        if (nrpn_select < GEN_LAST)
                            //        {
                            //            float val = fluid_gen_scale_nrpn(nrpn_select, data);
                            //            fluid_synth_set_gen_LOCAL(synth, channum, nrpn_select, val);
                            //        }

                            //        chan->nrpn_select = 0;  /* Reset to 0 */
                            //    }
                            //}
                            //else 
                            // if (fluid_channel_get_cc(chan, RPN_MSB) == 0)      /* RPN is active: MSB = 0? */
                            {
                                switch ((midi_rpn_event)cc[(int)MPTKController.RPN_LSB])
                                {
                                    case midi_rpn_event.RPN_PITCH_BEND_RANGE:    /* Set bend range in semitones */
                                        //fluid_channel_set_pitch_wheel_sensitivity(synth->channel[channum], value);
                                        pitch_wheel_sensitivity = (short)valueController;

                                        /* Update bend range */
                                        /* fluid_synth_update_pitch_wheel_sens_LOCAL(synth, channum);    
                                               fluid_synth_modulate_voices_LOCAL(synth, chan, 0, FLUID_MOD_PITCHWHEELSENS);
                                                    fluid_voice_t* voice;
                                                    int i;

                                                    for (i = 0; i < synth->polyphony; i++)
                                                    {
                                                        voice = synth->voice[i];

                                                        if (fluid_voice_get_channel(voice) == chan)
                                                        {
                                                            fluid_voice_modulate(voice, is_cc, ctrl);
                                                        }
                                                    }
                                        */
                                        break;

                                        //case RPN_CHANNEL_FINE_TUNE:   /* Fine tune is 14 bit over +/-1 semitone (+/- 100 cents, 8192 = center) */
                                        //    fluid_synth_set_gen_LOCAL(synth, channum, GEN_FINETUNE,
                                        //                              (float)(data - 8192) * (100.0f / 8192.0f));
                                        //    break;

                                        //case RPN_CHANNEL_COARSE_TUNE: /* Coarse tune is 7 bit and in semitones (64 is center) */
                                        //    fluid_synth_set_gen_LOCAL(synth, channum, GEN_COARSETUNE,
                                        //                              value - 64);
                                        //    break;

                                        //case RPN_TUNING_PROGRAM_CHANGE:
                                        //    fluid_channel_set_tuning_prog(chan, value);
                                        //    fluid_synth_activate_tuning(synth, channum,
                                        //                                fluid_channel_get_tuning_bank(chan),
                                        //                                value, TRUE);
                                        //    break;

                                        //case RPN_TUNING_BANK_SELECT:
                                        //    fluid_channel_set_tuning_bank(chan, value);
                                        //    break;

                                        //case RPN_MODULATION_DEPTH_RANGE:
                                        //    break;
                                }
                            }

                            break;
                        }
                    //case MPTKController.DATA_ENTRY_MSB:
                    //    {
                    //        //int data = (value << 7) + chan->cc[DATA_ENTRY_LSB];

                    ///* SontFont 2.01 NRPN Message (Sect. 9.6, p. 74)  */
                    //if ((chan->cc[NRPN_MSB] == 120) && (chan->cc[NRPN_LSB] < 100))
                    //{
                    //    float val = fluid_gen_scale_nrpn(chan->nrpn_select, data);
                    //    FLUID_LOG(FLUID_WARN, "%s: %d: Data = %d, value = %f", __FILE__, __LINE__, data, val);
                    //    fluid_synth_set_gen(chan->synth, chan->channum, chan->nrpn_select, val);
                    //}
                    //    break;
                    //}

                    //case MPTKController.NRPN_MSB:
                    //    cc[(int)MPTKController.NRPN_LSB] = 0;
                    //    nrpn_select = 0;
                    //    break;

                    //case MPTKController.NRPN_LSB:
                    //    /* SontFont 2.01 NRPN Message (Sect. 9.6, p. 74)  */
                    //    if (cc[(int)MPTKController.NRPN_MSB] == 120)
                    //    {
                    //        if (value == 100)
                    //        {
                    //            nrpn_select += 100;
                    //        }
                    //        else if (value == 101)
                    //        {
                    //            nrpn_select += 1000;
                    //        }
                    //        else if (value == 102)
                    //        {
                    //            nrpn_select += 10000;
                    //        }
                    //        else if (value < 100)
                    //        {
                    //            nrpn_select += value;
                    //            Debug.LogWarning(string.Format("NRPN Select = {0}", nrpn_select));
                    //        }
                    //    }
                    //    break;

                    //case MPTKController.RPN_MSB:
                    //    break;

                    //case MPTKController.RPN_LSB:
                    //    // erase any previously received NRPN message 
                    //    cc[(int)MPTKController.NRPN_MSB] = 0;
                    //    cc[(int)MPTKController.NRPN_LSB] = 0;
                    //    nrpn_select = 0;
                    //    break;

                    default:
                        if (synth.MPTK_ApplyRealTimeModulator)
                            synth.fluid_synth_modulate_voices(Channel, 1, (int)numController);
                        break;
                }
                return previousValue;
            }
        }


        /*
         * fluid_channel_pitch_bend
         */
        // Not recommended to use
        public void fluid_channel_pitch_bend(int val)
        {
            if (synth.VerboseChannel) Debug.LogFormat("PitchChange\tChannel:{0}\tValue:{1}", Channel, val);
            pitch_bend = (short)val;
            synth.fluid_synth_modulate_voices(Channel, 0, (int)fluid_mod_src.FLUID_MOD_PITCHWHEEL); //STRANGE
        }

        // Not recommended to use
        public HiPreset fluid_synth_find_preset(int banknum, int prognum)
        {
            if (synth.VerboseChannel) Debug.Log($"Find HiPreset for channel {Channel} bank:{banknum} preset:{prognum}");

            HiPreset preset_found = CheckBankAndPresetExist(banknum, prognum);
            if (preset_found != null)
                return preset_found;

            if (synth.VerboseChannel) Debug.LogWarning($"No HiPreset found");
            ImSoundFont sFont = synth.MPTK_SoundFont.SoundFont;
            // v2.9.0 try to find the same preset in the first bank
            if (banknum != 0)
            {
                banknum = 0;
                if (banknum >= 0 && banknum < sFont.Banks.Length &&
                   sFont.Banks[banknum] != null &&
                   sFont.Banks[banknum].defpresets != null &&
                   prognum < sFont.Banks[banknum].defpresets.Length &&
                   sFont.Banks[banknum].defpresets[prognum] != null)
                {
                    if (synth.VerboseVoice || synth.VerboseChannel) Debug.Log($"Select the HiPreset bank:{banknum} preset:{prognum}.");
                    return sFont.Banks[banknum].defpresets[prognum];
                }
            }

            // Not find, return the first available preset
            foreach (ImBank bank in sFont.Banks)
                if (bank != null)
                    foreach (HiPreset preset in bank.defpresets)
                        if (preset != null)
                        {
                            if (synth.VerboseVoice || synth.VerboseChannel) Debug.Log($"Select the HiPreset {preset.Num} in the bank {bank.BankNumber}.");
                            return preset;
                        }
            return null;
        }

        private HiPreset CheckBankAndPresetExist(int banknum, int prognum)
        {
            if (synth.MPTK_SoundFont.IsReady)
            {
                ImSoundFont sfont = synth.MPTK_SoundFont.SoundFont;
                if (sfont == null)
                {
                    Debug.LogWarning("Find preset: no soundfont defined");
                }
                else if (banknum >= 0 && banknum < sfont.Banks.Length && sfont.Banks[banknum] != null)
                {
                    if (sfont.Banks[banknum].defpresets != null && prognum < sfont.Banks[banknum].defpresets.Length && sfont.Banks[banknum].defpresets[prognum] != null)
                    {
                        return sfont.Banks[banknum].defpresets[prognum];
                    }
                    else
                        Debug.LogWarning($"Preset {prognum} not found in the bank {banknum} of the selected SoundFont.");
                }
                else
                    Debug.LogWarning($"Bank {banknum} not found in the selected SoundFont.");
            }
            else
                Debug.LogWarning("Find preset: no soundfont defined");
            return null;
        }

        // Not recommended to use
        public void fluid_synth_program_change(int preset)
        {
            if (synth.MPTK_SoundFont.IsReady)
            {
                int banknum;

                if (Channel != 9 || synth.MPTK_EnablePresetDrum == true) // V2.89.0
                {
                    if (ForcedPreset >= 0)
                        preset = ForcedPreset;

                    banknum = ForcedBank >= 0 ? ForcedBank : BankNum; //fluid_channel_get_banknum

                    prognum = preset; // fluid_channel_set_prognum
                    BankNum = banknum;

                    if (synth.VerboseVoice || synth.VerboseChannel) Debug.LogFormat("ProgramChange\tChannel:{0}\tBank:{1}\tPreset:{2}", Channel, banknum, preset);
                    hiPreset = fluid_synth_find_preset(banknum, preset);
                }
            }
        }
    }
}
