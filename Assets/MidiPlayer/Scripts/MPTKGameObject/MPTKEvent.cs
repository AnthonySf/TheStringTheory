using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidiPlayerTK
{

    /// @ingroup midi_event_object
    /// <summary>@brief
    /// Represents a MIDI event used throughout MPTK.
    /// This class is central to script-based MIDI workflows in components such as
    /// MidiStreamPlayer, MidiFilePlayer, MidiFileLoader, and MPTKWriter.
    ///
    /// The key property is Command. The meaning of other fields (for example Value)
    /// depends on the selected MIDI command type.
    ///
    /// Typical use cases:
    /// - play/stop notes,
    /// - change instrument (patch/preset),
    /// - send control changes,
    /// - handle meta events.
    ///
    /// Related components:
    /// - MidiFileLoader: reads MIDI events from files,
    /// - MidiFilePlayer: processes MIDI events from the internal sequencer,
    /// - MPTKWriter: generates MIDI content programmatically,
    /// - MidiStreamPlayer: plays MIDI events in real time.
    ///
    /// More information:
    /// - https://paxstellar.fr/class-mptkevent
    /// - https://mptkapi.paxstellar.com/d9/d50/class_midi_player_t_k_1_1_m_p_t_k_event.html
    ///
    /// Example with MidiStreamPlayer:
    /// @code
    /// 
    /// // Find a MidiStreamPlayer Prefab from the scene
    /// MidiStreamPlayer midiStreamPlayer = FindFirstObjectByType<MidiStreamPlayer>();
    /// midiStreamPlayer.MPTK_StartMidiStream();
    /// 
    /// // Change instrument to Marimba for channel 0
    /// MPTKEvent PatchChange = new MPTKEvent() {
    ///        Command = MPTKCommand.PatchChange,
    ///        Value = 12, // usually Marimba, depending on the selected SoundFont
    ///        Channel = 0 }; // Instruments are assigned per channel (0 to 15).
    ///                       // At a given time, up to 16 channel instruments can be active.
    /// midiStreamPlayer.MPTK_PlayEvent(PatchChange);    
    ///
    /// // Play a C4 during one second with the Marimba instrument
    /// MPTKEvent NotePlaying = new MPTKEvent() {
    ///        Command = MPTKCommand.NoteOn,
    ///        Value = 60, // C4
    ///        Channel = 0,
    ///        Duration = 1000, // one second
    ///        Velocity = 100 };
    /// midiStreamPlayer.MPTK_PlayEvent(NotePlaying);    
    /// @endcode
    /// </summary>
    public partial class MPTKEvent : ICloneable
    {
        /// @name Event Type and Message Data
        /// @brief Core MIDI event identity and associated message data.
        /// @details
        /// This section defines what the event is (`Command`) and the data fields
        /// interpreted from that command (`Controller`, `Meta`, `Info`, `Value`).
        /// It is the primary model used by playback, serialization, and filtering logic.

        /// @{

        /// <summary>@brief
        /// MIDI command type for this event.
        /// See #MPTKCommand (NoteOn, ControlChange, PatchChange, etc.).
        /// </summary>
        public MPTKCommand Command;

        /// <summary>@brief
        /// Controller code when #Command is #MPTKCommand.ControlChange
        /// (for example modulation, pan, bank select).
        /// The associated controller value is stored in #Value.
        /// </summary>
        public MPTKController Controller;

        /// <summary>@brief
        /// Meta-event type when #Command is #MPTKCommand.MetaEvent
        /// (for example Lyric, TimeSignature, SetTempo).
        /// Related meta message data is stored in other fields (such as #Value or #Info).
        /// </summary>
        public MPTKMeta Meta;

        /// <summary>@brief
        /// Text message data for meta events (for example TextEvent or Lyric).
        /// </summary>
        public string Info;

        /// <summary>@brief
        /// Numeric message data whose meaning depends on #Command.
        ///! <ul>
        ///! <li>#Command = #MPTKCommand.NoteOn
        ///!     <ul>
        ///!       <li> #Value contains the MIDI note number (Middle C is 60 / C4).\n
        ///!         See: http://www.music.mcgill.ca/~ich/classes/mumt306/StandardMIDIfileformat.html#BMA1_3
        ///        </li>
        ///!     </ul>
        ///! </li>
        ///! <li>#Command = #MPTKCommand.ControlChange
        ///!     <ul>
        ///!       <li> #Value contains controller value, see #MPTKController</li>
        ///!     </ul>
        ///! </li>
        ///! <li>#Command = #MPTKCommand.PatchChange
        ///!     <ul>
        ///!        <li>  #Value contains patch/preset/instrument number.\n
        ///!                Use the active SoundFont to resolve instrument names.\n
        ///!                If your SoundFont follows the General MIDI (GM) map, the patch map is similar to:\n
        ///                 http://www.music.mcgill.ca/~ich/classes/mumt306/StandardMIDIfileformat.html#BMA1_4    
        ///!        </li>
        ///!     </ul>
        ///! </li>
        ///! <li>#Command = #MPTKCommand.MetaEvent and #Meta equal:
        ///!     <ul>
        ///!        <li>  #MPTKMeta.SetTempo</li>
        ///!        <ul>
        ///!            <li>  #Value contains microseconds per quarter note</li>
        ///!        </ul>
        ///!        <li>  #MPTKMeta.TimeSignature. See #MPTKMeta.TimeSignature</li>
        ///!        <li>  #MPTKMeta.KeySignature. See #MPTKMeta.KeySignature</li>
        ///!     </ul>
        ///! </li>
        ///! </ul>
        /// </summary>
        public int Value;

        /// @}



        /// @name Note Playback Attributes
        /// @brief Parameters used to play and shape notes.
        /// @details
        /// These members describe how a note should be rendered:
        /// channel routing, velocity, note duration, and optional start delay.
        /// They are mainly used when `Command` is `NoteOn`/`NoteOff`.

        /// @{

        /// <summary>@brief
        /// Track index of this event in the source MIDI file.
        /// Track 0 is the first track.
        /// This value is informational and does not affect playback behavior.
        /// </summary>
        public long Track;

        /// <summary>@brief
        /// MIDI channel from 0 to 15 (channel 9 is typically used for drums).
        /// </summary>
        public int Channel;

        /// <summary>@brief
        /// Note velocity (0 to 127) for #MPTKCommand.NoteOn and #MPTKCommand.NoteOff.
        /// </summary>
        public int Velocity;

        /// <summary>@brief
        /// Note duration in milliseconds when #Command = #MPTKCommand.NoteOn.
        /// Set to -1 for an indefinitely sustained note (until explicit note-off).
        /// @version 2.10.0 
        /// @note
        /// Previous non-note usages were removed:
        ///    - SetTempo no longer stores tempo in this field,
        ///    - TimeSignature no longer stores denominator in this field,
        ///    - KeySignature no longer stores major/minor flag in this field.
        /// </summary>
        public long Duration;

        /// <summary>@brief
        /// Delay in milliseconds before playing a note.
        /// Used only for #MPTKCommand.NoteOn and only in Core mode.
        /// </summary>
        public long Delay;

        /// <summary>@brief
        /// Duration in MIDI ticks when #Command = #MPTKCommand.NoteOn.
        /// @details
        /// Tick duration is converted to milliseconds (#Duration) when a MIDI file is loaded.
        /// @note
        /// Maestro playback uses #Duration in milliseconds rather than this tick field.
        /// https://en.wikipedia.org/wiki/Note_value
        /// </summary>
        public int Length;

        /// @}

        /// @name Musical Position and Timing
        /// @brief Temporal position of the event in musical and real time.
        /// @details
        /// These values provide timeline context:
        /// tick-based position, measure/beat, index in the source stream,
        /// and real-time timing useful for diagnostics and synchronization.
        /// @{

        /// <summary>@brief
        /// Event time in MIDI ticks (fraction of a beat) from the beginning of the MIDI file.
        /// This value is independent of tempo or playback speed.
        /// Not used by MidiStreamPlayer or MidiInReader (real-time sources).
        /// </summary>
        public long Tick;

        /// <summary>@brief
        /// Measure (bar) where this event occurs.
        /// Computed from time-signature events when a MIDI file is loaded.
        /// By default the time signature is 4/4.
        /// </summary>
        public int Measure;

        /// <summary>@brief
        /// Beat index inside the measure for this event.
        /// Present for all events, but musically most relevant for note events.
        /// Computed from time-signature events when a MIDI file is loaded.
        /// By default the time signature is 4/4.
        /// </summary>
        public int Beat;

        /// <summary>@brief
        /// Original index of this event in the loaded MIDI event list.
        /// </summary>
        public int Index;

        /// <summary>@brief
        /// UTC timestamp (DateTime ticks) captured when this event instance is created.
        /// Divide by 10,000 to convert to milliseconds.
        /// Replaces the former TickTime field.
        /// </summary>
        public long CreateTime;

        /// <summary>@brief
        /// Absolute event time in milliseconds from the start of the MIDI sequence.
        /// Accounts for tempo changes, but not for MidiFilePlayer.MPTK_Speed.
        /// Not used by MidiStreamPlayer or MidiInReader (real-time sources).
        /// </summary>
        public float RealTime;

        /// <summary>@brief
        /// Elapsed system time (DateTime.UtcNow.Ticks) since this event was created.
        /// Mainly useful for latency diagnostics.
        /// One DateTime tick equals 100 nanoseconds.
        /// @note Disabled by default. Define DEBUG_PERF_AUDIO in MidiSynth for debug-only use.
        /// </summary>
        public long LatenceTime { get { return DateTime.UtcNow.Ticks - CreateTime; } }

        /// <summary>@brief
        /// Elapsed time in milliseconds since this event was created.
        /// Mainly useful for latency diagnostics.
        /// @note Disabled by default. Define DEBUG_PERF_AUDIO in MidiSynth for debug-only use.
        /// </summary>
        public long LatenceTimeMillis { get { return LatenceTime / fluid_voice.Nano100ToMilli; } }

        /// @}


        /// @name Value Transposition
        /// @brief Utilities to transpose and restore note values safely.
        /// @details
        /// The original value is cached once, then reused so repeated calls
        /// apply transposition from the same base note instead of accumulating drift.
        /// @{

        // initial value before transpose
        private int notTransposedValue;

        /// <summary>@brief
        /// Original (untransposed) value captured before calling #TransposeValue.
        /// </summary>
        public int OriginalValue { get { return notTransposedValue; } }

        /// <summary>@brief
        /// Applies a transposition offset to #Value.
        /// The first call stores the original value, then each call restores and reapplies
        /// the requested transpose amount from that original value.
        /// </summary>
        /// <param name="transpose">Semitone offset to apply.</param>
        public void TransposeValue(int transpose)
        {
            if (notTransposedValue == -1)
                // Must be set only at the first note-on played to get the initial value
                notTransposedValue = Value;
            // Restaure initial value
            Value = notTransposedValue;
            // Apply
            Value += transpose;
        }
        /// <summary>@brief
        /// Restores #Value to the original untransposed value if available.
        /// </summary>
        public void ResetTransposeValue()
        {
            if (notTransposedValue > -1)
                // Restaure initial value
                Value = notTransposedValue;
        }
        /// @}


        /// <summary>@brief
        /// Musical note-length categories for note duration: Whole, Half, Quarter, Eighth, Sixteenth.
        /// See https://en.wikipedia.org/wiki/Note_value
        /// </summary>
        public enum EnumLength { Whole, Half, Quarter, Eighth, Sixteenth }

        /// @name Source and Runtime Context
        /// @brief Runtime metadata attached to the event during playback.
        /// @details
        /// Includes source/session identifiers, user tag, associated voices,
        /// and playback state (`IsOver`) for voice lifecycle tracking.
        /// @{

        /// <summary>@brief
        /// Origin of the message.
        /// Contains MIDI input source ID for incoming MIDI events, otherwise 0.
        /// </summary>
        public uint Source;

        /// <summary>@brief
        /// Session identifier associated with this event.
        /// With MidiFilePlayer, this ID is unique per played MIDI session and is used by
        /// MPTK_ClearAllSound to stop only voices from that session when switching tracks.
        /// It can also be used in other components such as MidiStreamPlayer for custom grouping,
        /// but avoid overriding it while MidiFilePlayer manages playback.
        /// </summary>
        public int IdSession;


        /// <summary>@brief
        /// Free-form application tag associated with this event.
        /// </summary>
        public object Tag;

        /// <summary>@brief
        /// Voices associated with a NoteOn event.
        /// Multiple voices can exist for one note (for example layered samples).
        /// </summary>
        public List<fluid_voice> Voices;

        /// <summary>@brief
        /// Indicates whether this event has finished playing (all associated voices are OFF).
        /// </summary>
        public bool IsOver
        {
            get
            {
                if (Voices != null)
                {
                    foreach (fluid_voice voice in Voices)
                        if (voice.status != fluid_voice_status.FLUID_VOICE_OFF)
                            return false;
                }
                // All voices are off or empty
                return true;
            }
        }

        /// @}

        /// @name Constructors, Clone and Message Conversion
        /// @brief Creation, duplication, and MIDI packed-message conversion.
        /// @details
        /// This section contains constructors for default and packed MIDI input,
        /// cloning support, and conversion to/from compact MIDI data representation.
        /// @{

        /// <summary>@brief
        /// Initializes a new MPTKEvent with default NoteOn-oriented values.
        /// Defaults:
        /// - Command = NoteOn
        /// - Duration = -1 (infinite)
        /// - Channel = 0
        /// - Delay = 0
        /// - Velocity = 127
        /// - IdSession = -1
        /// </summary>
        public MPTKEvent()
        {
            Command = MPTKCommand.NoteOn;
            Duration = -1;
            Channel = 0;
            Delay = 0;
            Velocity = 127; // max
            IdSession = -1;
            CreateTime = DateTime.UtcNow.Ticks;
            notTransposedValue = -1;
        }

        /// <summary>@brief
        /// Dupplicate MIDI message.
        /// </summary>
        public virtual object Clone()
        {
            return this.MemberwiseClone();
        }

        /// <summary>@brief
        /// Creates an MPTKEvent from a packed MIDI input message.
        /// </summary>
        /// <param name="data">Packed MIDI message data.</param>
        public MPTKEvent(ulong data)
        {
            Source = (uint)(data & 0xffffffffUL);
            Command = (MPTKCommand)((data >> 32) & 0xFF);
            if (Command < MPTKCommand.Sysex)
            {
                Channel = (int)Command & 0xF;
                Command = (MPTKCommand)((int)Command & 0xF0);
            }
            byte data1 = (byte)((data >> 40) & 0xff);
            byte data2 = (byte)((data >> 48) & 0xff);

            if (Command == MPTKCommand.NoteOn && data2 == 0)
                Command = MPTKCommand.NoteOff;

            //if ((int)Command != 0xFE)
            //    Debug.Log($"{data >> 32:X}");

            switch (Command)
            {
                case MPTKCommand.NoteOn:
                    Value = data1; // Key
                    Velocity = data2;
                    Duration = -1; // no duration are defined in MIDI flux
                    break;
                case MPTKCommand.NoteOff:
                    Value = data1; // Key
                    Velocity = data2;
                    break;
                case MPTKCommand.KeyAfterTouch:
                    Value = data1; // Key
                    Velocity = data2;
                    break;
                case MPTKCommand.ControlChange:
                    Controller = (MPTKController)data1;
                    Value = data2;
                    break;
                case MPTKCommand.PatchChange:
                    Value = data1;
                    break;
                case MPTKCommand.ChannelAfterTouch:
                    Value = data1;
                    break;
                case MPTKCommand.PitchWheelChange:
                    Value = data2 << 7 | data1; // Pitch-bend is transmitted with 14-bit precision. 
                    break;
                case MPTKCommand.TimingClock:
                case MPTKCommand.StartSequence:
                case MPTKCommand.ContinueSequence:
                case MPTKCommand.StopSequence:
                case MPTKCommand.AutoSensing:
                    // no value
                    break;

            }
        }

        /// <summary>@brief
        /// Builds a packed MIDI message from this MPTKEvent.
        /// Example: 0x00403C90 for NoteOn (90h), note 3Ch, velocity 40h.
        /// </summary>
        /// <returns>Packed MIDI message as an unsigned 64-bit value.</returns>
        public ulong ToData()
        {
            ulong data = (ulong)Command | ((ulong)Channel & 0xF);
            switch (Command)
            {
                case MPTKCommand.NoteOn:
                    data |= (ulong)Value << 8 | (ulong)Velocity << 16;
                    break;
                case MPTKCommand.NoteOff:
                    data |= (ulong)Value << 8 | (ulong)Velocity << 16;
                    break;
                case MPTKCommand.KeyAfterTouch:
                    data |= (ulong)Value << 8 | (ulong)Velocity << 16;
                    break;
                case MPTKCommand.ControlChange:
                    data |= (ulong)Controller << 8 | (ulong)Value << 16;
                    break;
                case MPTKCommand.PatchChange:
                    data |= (ulong)Value << 8;
                    break;
                case MPTKCommand.ChannelAfterTouch:
                    data |= (ulong)Value << 8;
                    break;
                case MPTKCommand.PitchWheelChange:
                    // The pitch bender is measured by a fourteen bit value. Center (no pitch change) is 2000H. 
                    // Two data after the command code 
                    //  1) the least significant 7 bits. 
                    //  2) the most significant 7 bits.
                    data |= ((ulong)Value & 0x7F) << 8 | ((ulong)Value & 0x7F00) << 16;
                    break;
                case MPTKCommand.TimingClock:
                case MPTKCommand.StartSequence:
                case MPTKCommand.ContinueSequence:
                case MPTKCommand.StopSequence:
                case MPTKCommand.AutoSensing:
                    data = (ulong)Command;
                    break;

            }
            return data;
        }
        /// @}

        /// @name Tempo and Byte Conversion Helpers
        /// @brief Static helpers for tempo conversion and byte packing.
        /// @details
        /// Utility methods convert BPM to microseconds-per-quarter-note (and reverse),
        /// and provide low-level byte extraction/packing used by meta event data.
        /// @{

        /// <summary>@brief
        /// Converts beats per minute (BPM) to microseconds per quarter note.
        /// Example: BPM=120 gives 500000 microseconds per quarter note.
        /// </summary>
        /// <param name="bpm">Beats per minute (assuming one beat = quarter note).</param>
        /// <returns>60000000 / bpm, or 500000 when bpm &lt;= 0.</returns>
        public static int BeatPerMinute2QuarterPerMicroSecond(double bpm)
        {
            return bpm > 0 ? (int)(60000000d / bpm) : 500000;
        }

        /// <summary>@brief
        /// Converts microseconds per quarter note to beats per minute (BPM).
        /// Example: 500000 microseconds per quarter note gives BPM=120.
        /// </summary>
        /// <param name="microsecondsPerQuaterNote">Microseconds per quarter note.</param>
        /// <returns>60000000 / microsecondsPerQuaterNote, or 120 when input &lt;= 0.</returns>
        public static double QuarterPerMicroSecond2BeatPerMinute(int microsecondsPerQuaterNote)
        {
            return microsecondsPerQuaterNote > 0 ? 60000000d / microsecondsPerQuaterNote : 120;
        }

        /// @}

        /// @name Debug String Helpers
        /// @brief Human-readable formatting helpers for logs and diagnostics.
        /// @details
        /// Produces compact or detailed string representations of `MPTKEvent`
        /// to simplify debugging, tracing, and inspection during runtime.
        /// @{

        public override string ToString()
        {
            return ConvertToString(false);
        }
        public string ToStringBrief()
        {
            return ConvertToString(true);
        }

        /// <summary>@brief
        /// Builds a string description of this MIDI event.
        /// Since v2.83, returned strings no longer contain trailing end-of-line markers.
        /// For improved alignment in Debug.Log, enable a monospace font in the Unity console settings.
        /// </summary>
        /// <returns>Formatted event description.</returns>
        public string ConvertToString(bool brief)
        {
            string result = "";

            string command = "unknown";
            switch (Command)
            {
                case MPTKCommand.NoteOn: command = "NoteOn"; break;
                case MPTKCommand.NoteOff: command = "NoteOff"; break;
                case MPTKCommand.PatchChange: command = "Preset"; break;
                case MPTKCommand.ControlChange: command = $"Ctrl {Controller}"; break;
                case MPTKCommand.KeyAfterTouch: command = "KeyTouch"; break;
                case MPTKCommand.ChannelAfterTouch: command = "ChannelTouch"; break;
                case MPTKCommand.PitchWheelChange: command = "PitchWheel"; break;
                case MPTKCommand.MetaEvent:
                    try
                    {
                        switch (Meta)
                        {
                            case MPTKMeta.KeySignature: command = $"KeySign"; break;
                            case MPTKMeta.TimeSignature: command = $"TimeSign"; break;
                            case MPTKMeta.SetTempo: command = $"Tempo"; break;
                            default: command = $"{Meta}"; break;
                        }
                    }
                    catch { command = $"{Meta} error value:{Value}"; }
                    break;

                case MPTKCommand.TimingClock:
                case MPTKCommand.StartSequence:
                case MPTKCommand.ContinueSequence:
                case MPTKCommand.StopSequence:
                case MPTKCommand.AutoSensing: command += $"Command:{Command}"; break;
                default: command += $"Command:{Command}"; break;
            }

            string position = "";
            if (!brief)
            {
                position = $"T:{Track,-2:00} ";
                if (Command == MPTKCommand.NoteOn || Command == MPTKCommand.NoteOff || Command == MPTKCommand.KeyAfterTouch || Command == MPTKCommand.ControlChange ||
                    Command == MPTKCommand.PatchChange || Command == MPTKCommand.ChannelAfterTouch || Command == MPTKCommand.PitchWheelChange)
                    position += $"C:{Channel,-2:00} ";
                else
                    position += "     ";
                position += $"{RealTime / 1000f:F3} s. {Tick,-7:0000000} t. M/B:{Measure}/{Beat}";
            }
            else if (Command == MPTKCommand.NoteOn || Command == MPTKCommand.NoteOff || Command == MPTKCommand.KeyAfterTouch || Command == MPTKCommand.ControlChange ||
                    Command == MPTKCommand.PatchChange || Command == MPTKCommand.ChannelAfterTouch || Command == MPTKCommand.PitchWheelChange)
                position += $"C:{Channel,-2:00} ";

            command = $"{command,-20} {position}";

            switch (Command)
            {
                case MPTKCommand.NoteOn:
                    string sDuration = "";
                    if (!brief)
                        if (Duration < 0)
                            sDuration = "Duration:Inf.    ";
                        else
                            sDuration = $"Duration:{Duration / 1000f:F2} s. {Length} t.";
                    string strNote = brief ? "" : $"/{HelperNoteLabel.LabelFromMidi(Value)}";
                    result += $"{command} Note:{Value,3:000}{strNote,-4} Velocity:{Velocity:000} {sDuration}";
                    break;
                case MPTKCommand.NoteOff:
                    result += $"{command} Note:{Value,3:000} Velocity:{Velocity:000}";
                    break;
                case MPTKCommand.PatchChange:
                    result += $"{command} Value:{Value,-3:000}";
                    break;
                case MPTKCommand.ControlChange:
                    result += $"{command} Value:{Value,-3:000}";
                    break;
                case MPTKCommand.KeyAfterTouch:
                    result += $"{command} Not processed by Maestro Synth";
                    break;
                case MPTKCommand.ChannelAfterTouch:
                    result += $"{command} Not processed by Maestro Synth";
                    break;
                case MPTKCommand.PitchWheelChange:
                    result += $"{command} Value:{Value,-3:000}";
                    break;
                case MPTKCommand.MetaEvent:
                    try
                    {
                        switch (Meta)
                        {
                            case MPTKMeta.KeySignature: result = $"{command} SharpsFlats:{MPTKEvent.ExtractFromInt((uint)Value, 0)} MajorMinor:{MPTKEvent.ExtractFromInt((uint)Value, 1)}"; break;
                            case MPTKMeta.TimeSignature: result = $"{command} Numerator:{MPTKEvent.ExtractFromInt((uint)Value, 0)} Denominator:{MPTKEvent.ExtractFromInt((uint)Value, 1)}"; break;
                            case MPTKMeta.SetTempo: result = $"{command} Microseconds:{Value} Tempo:{60000000 / Value:F2}"; break;
                            default:
                                string sinfo = Info ?? "";
                                result = $"{command} {sinfo}"; break;
                        }
                    }
                    catch { result = $"{command} {Meta} error value:{Value}"; }
                    break;

                case MPTKCommand.TimingClock:
                case MPTKCommand.StartSequence:
                case MPTKCommand.ContinueSequence:
                case MPTKCommand.StopSequence:
                case MPTKCommand.AutoSensing:
                    result += $"{Command}";
                    break;
                default:
                    result += $"{Command} Value:{Value} Duration:{Duration,6} Velocity:{Velocity,3} source:{Source}";
                    break;
            }
            return result;
        }
        /// @}

        // @cond NODOC

        public int Compare(MPTKEvent y)
        {
            int tickComparison = Tick.CompareTo(y.Tick);

            // If Ticks are equal, apply MIDI event type priority, incredible ChatGPT help!
            if (tickComparison == 0)
            {
                // Helper function to get priority (lower number = higher priority)
                int GetTypePriority(MPTKEvent midi) => midi.Command switch
                {
                    MPTKCommand.ControlChange => 1,
                    MPTKCommand.PatchChange => 2,
                    MPTKCommand.MetaEvent => (midi.Meta == MPTKMeta.EndTrack ? 99 : 50),
                    _ => 50  // All other types get middle priority
                };

                return GetTypePriority(this).CompareTo(GetTypePriority(y));
            }
            return tickComparison;
        }

        // Packs four bytes into one integer.
        // <param name="b1">Byte 0 (least significant).</param>
        // <param name="b2">Byte 1.</param>
        // <param name="b3">Byte 2.</param>
        // <param name="b4">Byte 3 (most significant).</param>
        // <returns>(b4 << 24) | (b3 << 16) | (b2 << 8) | b1</returns>
        static public int BuildIntFromBytes(byte b1, byte b2, byte b3, byte b4)
        {
            return (b4 << 24) | (b3 << 16) | (b2 << 8) | b1;
        }

        // Extracts byte position <c>n</c> from an integer.
        // <param name="v">Value built with #BuildIntFromBytes.</param>
        // <param name="n">Byte position from 0 (least significant) to 3 (most significant).</param>
        // <returns>(v >> (8*n)) & 0xFF</returns>
        static public byte ExtractFromInt(uint v, int n)
        {
            return (byte)((v >> (8 * n)) & 0xFF);
        }


        // @endcond

    }
}

