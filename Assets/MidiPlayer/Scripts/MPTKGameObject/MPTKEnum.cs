using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidiPlayerTK
{
    /// @ingroup midi_event_enums
    /// @name MIDI Event Model Enums
    /// @{

    /// <summary>@brief
    /// MIDI command codes. Defines the action performed by the message: note on/off, patch change, and more.\n
    /// Depending on the selected command, specific MPTKEvent properties must be set (Value, Channel, Velocity, etc.).\n
    /// </summary>
    public enum MPTKCommand : byte
    {
        /// <summary>@brief
        /// Note Off.\n
        /// Stops the note identified by Value on the specified Channel.\n
        ///      - MPTKEvent#Value contains the note number to stop (60 = C5).\n
        ///      - MPTKEvent#Channel contains the MIDI channel (0 to 15).\n
        /// </summary>
        NoteOff = 0x80,

        /// <summary>@brief
        /// Note On.\n
        ///      - MPTKEvent#Value contains the note number to play (60 = C5).\n
        ///      - MPTKEvent#Duration contains the note duration in milliseconds (-1 for infinite).\n
        ///      - MPTKEvent#Channel contains the MIDI channel (0 to 15).\n
        ///      - MPTKEvent#Velocity contains the velocity (0 to 127).\n
        /// </summary>
        NoteOn = 0x90,

        /// <summary>@brief
        /// Key After-touch (polyphonic key pressure).\n
        /// Not processed by NAudio and therefore not processed by Maestro Synth.\n
        /// Reserved for future support.
        /// </summary>
        KeyAfterTouch = 0xA0, // FS KEY_PRESSURE


        /// <summary>@brief
        /// Control Change.\n
        ///      - MPTKEvent.Controller contains the controller number to change. See #MPTKController (Modulation, Pan, Bank Select, ...).\n
        ///      - MPTKEvent.Value contains the controller value (0 to 127).
        /// </summary>
        ControlChange = 0xB0,

        /// <summary>@brief
        /// Patch Change.\n
        ///      - MPTKEvent.Value contains the patch/preset/instrument number to select (0 to 127). 
        /// </summary>
        PatchChange = 0xC0,

        /// <summary>@brief
        /// Channel after-touch (channel pressure).\n
        /// Not processed by Maestro Synth.\n
        /// </summary>
        ChannelAfterTouch = 0xD0,

        /// <summary>@brief
        /// Pitch Wheel Change.\n
        /// MPTKEvent.Value contains the pitch wheel value (0 to 16383).\n
        /// Higher values transpose pitch up, and lower values transpose pitch down.\n
        /// The default sensitivity is 2 semitones. Maximum bend is therefore two semitones up or down\n
        /// from the original pitch (a 4-semitone span from minimum to maximum bend).
        ///     - 0 is the lowest bend position (default: -2 semitones), 
        ///     - 8192 (0x2000) is the center value (no transposition),
        ///     - 16383 (0x3FFF) is the highest bend position (default: +2 semitones)
        /// </summary>
        PitchWheelChange = 0xE0,

        /// <summary>@brief
        /// SysEx message. Not processed by Maestro Synth.\n
        /// </summary>
        Sysex = 0xF0,

        /// <summary>@brief
        /// End of Exclusive (EOX), marks the end of a SysEx message. Not processed by Maestro Synth.
        /// </summary>
        Eox = 0xF7,

        /// <summary>@brief
        /// Timing clock.\n
        /// Used when synchronization is required.
        /// </summary>
        TimingClock = 0xF8,

        /// <summary>@brief
        /// Start sequence\n
        /// </summary>
        StartSequence = 0xFA,

        /// <summary>@brief
        /// Continue sequence\n
        /// </summary>
        ContinueSequence = 0xFB,

        /// <summary>@brief
        /// Stop sequence.\n
        /// </summary>
        StopSequence = 0xFC,

        /// <summary>@brief
        /// Active Sensing.\n
        /// </summary>
        AutoSensing = 0xFE,

        /// <summary>@brief
        /// Meta events provide optional MIDI information; none are mandatory.\n
        /// In MPTKEvent, the MPTKEvent#Meta attribute defines the meta event type. See #MPTKMeta (TextEvent, Lyric, TimeSignature, ...).\n
        ///     - if MPTKEvent#Meta = #MPTKMeta.SetTempo, MPTKEvent#Value contains the new microseconds-per-quarter-note value. Use MPTKEvent.QuarterPerMicroSecond2BeatPerMinute() to convert to BPM.
        ///     - if MPTKEvent#Meta = #MPTKMeta.TimeSignature, MPTKEvent#Value contains four bytes from least significant to most significant. Use MPTKEvent.ExtractFromInt().
        ///         -# Numerator (number of beats in a bar), 
        ///         -# Denominator (beat unit: 1 means 2, 2 means 4 (quarter), 3 means 8 (eighth), 4 means 16, ...)
        ///         -# TicksInMetronomeClick, usually 24 (MIDI clocks per metronome click)
        ///         -# No32ndNotesInQuarterNote, usually 8 (number of 32nd notes per MIDI quarter note)
        ///     - if MPTKEvent#Meta = #MPTKMeta.KeySignature, MPTKEvent#Value contains two bytes from least significant to most significant. Use MPTKEvent.ExtractFromInt().
        ///         -# SharpsFlats (number of sharps/flats) 
        ///         -# MajorMinor flag (0 the scale is major, 1 the scale is minor).
        ///     - for other types (TextEvent, ...), MPTKEvent#Info contains textual information.
        /// </summary>
        MetaEvent = 0xFF,
    }

    /// <summary>@brief
    /// MIDI controller list.\n
    /// Each MIDI CC operates at 7-bit resolution, meaning it has 128 possible values. The values start at 0 and go to 127.\n
    /// Some instruments can receive higher resolution data for their MIDI control assignments. These high res assignments are defined by combining two separate CCs,\n
    /// one being the Most Significant Byte (MSB), and one being the Least Significant Byte (LSB).\n
    /// Most instruments receive only the MSB (default 7-bit resolution).
    /// More information: https://www.presetpatch.com/midi-cc-list.aspx
    /// </summary>
    public enum MPTKController : byte
    {
        /// <summary>@brief
        /// Bank Select (MSB)
        /// </summary>
        BankSelectMsb = 0,

        /// <summary>@brief
        /// Modulation (MSB)
        /// </summary>
        Modulation = 1,

        /// <summary>@brief
        /// Breath Controller
        /// </summary>
        BreathController = 2,

        /// <summary>@brief
        /// Foot controller (MSB)
        /// </summary>
        FootController = 4,

        PORTAMENTO_TIME_MSB = 0x05,

        DATA_ENTRY_MSB = 6,

        /// <summary>@brief
        /// Channel volume (named MainVolume before v2.88.2)
        /// </summary>
        VOLUME_MSB = 7,

        BALANCE_MSB = 8,

        /// <summary>@brief Pan MSB</summary>
        Pan = 10, //0xA

        /// <summary>@brief Expression (EXPRESSION_MSB)</summary>
        Expression = 11, // 0xB

        EFFECTS1_MSB = 12, //0x0C,
        EFFECTS2_MSB = 13, //0x0D,

        GPC1_MSB = 16, //0x10, /* general purpose controller */
        GPC2_MSB = 17, //0x11,
        GPC3_MSB = 18, //0x12,
        GPC4_MSB = 19, // 0x13,

        /// <summary>@brief Bank Select LSB.\n
        /// MPTK bank style is FLUID_BANK_STYLE_GS (see FluidSynth): bank = CC0/MSB (CC32/LSB ignored).
        /// </summary>
        BankSelectLsb = 32, // 0x20

        MODULATION_WHEEL_LSB = 33, // 0x21,
        BREATH_LSB = 34, // 0x22,
        FOOT_LSB = 36, // 0x24,
        PORTAMENTO_TIME_LSB = 37, // 0x25,


        DATA_ENTRY_LSB = 38, // 0x26,

        VOLUME_LSB = 39, // 0x27,

        BALANCE_LSB = 40, // 0x28,

        PAN_LSB = 42, //0x2A,

        EXPRESSION_LSB = 43, //0x2B,

        EFFECTS1_LSB = 44, //0x2C,
        EFFECTS2_LSB = 45, // 0x2D,
        GPC1_LSB = 48, // 0x30,
        GPC2_LSB = 49, // 0x31,
        GPC3_LSB = 50, // 0x32,
        GPC4_LSB = 51, // 0x33,

        /// <summary>@brief Sustain</summary>
        Sustain = 64, // 0x40

        /// <summary>@brief Portamento On/Off - not yet implemented </summary>
        Portamento = 65, // 0x41

        /// <summary>@brief Sostenuto On/Off - not yet implemented </summary>
        Sostenuto = 66, // 0x42

        /// <summary>@brief Soft Pedal On/Off - not yet implemented </summary>
        SoftPedal = 67, // 0x43

        /// <summary>@brief Legato Footswitch - not yet implemented </summary>
        LegatoFootswitch = 68, // 0x44

        HOLD2_SWITCH = 69, // 0x45,

        SOUND_CTRL1 = 70, // 0x46,
        SOUND_CTRL2 = 71, // 0x47,
        SOUND_CTRL3 = 72, // 0x48,
        SOUND_CTRL4 = 73, // 0x49,
        SOUND_CTRL5 = 74, // 0x4A,
        SOUND_CTRL6 = 75, // 0x4B,
        SOUND_CTRL7 = 76, // 0x4C,
        SOUND_CTRL8 = 77, // 0x4D,
        SOUND_CTRL9 = 78, // 0x4E,
        SOUND_CTRL10 = 79, // 0x4F,

        GPC5 = 80, // 0x50,
        GPC6 = 81, // 0x51,
        GPC7 = 82, // 0x52,
        GPC8 = 83, // 0x53,

        PORTAMENTO_CTRL = 84, // 0x54, 

        EFFECTS_DEPTH1 = 91, // 0x5B,
        EFFECTS_DEPTH2 = 92, // 0x5C,
        EFFECTS_DEPTH3 = 93, // 0x5D,
        EFFECTS_DEPTH4 = 94, // 0x5E,
        EFFECTS_DEPTH5 = 95, // 0x5F,

        DATA_ENTRY_INCR = 96, // 0x60,
        DATA_ENTRY_DECR = 97, // 0x61,

        /// <summary>@brief
        /// Non-Registered Parameter Number LSB\n
        /// http://www.philrees.co.uk/nrpnq.htm
        /// </summary>
        NRPN_LSB = 98, // 0x62,

        /// <summary>@brief
        /// Non-Registered Parameter Number MSB\n
        /// http://www.philrees.co.uk/nrpnq.htm
        /// </summary>
        NRPN_MSB = 99, // 0x63,

        /// <summary>@brief
        /// Registered Parameter Number LSB\n
        /// http://www.philrees.co.uk/nrpnq.htm
        /// </summary>
        RPN_LSB = 100, // 0x64,

        /// <summary>@brief
        /// Registered Parameter Number MSB\n
        /// http://www.philrees.co.uk/nrpnq.htm
        /// </summary>
        RPN_MSB = 101, // 0x65,

        /// <summary>@brief All sound off (ALL_SOUND_OFF)</summary>
        AllSoundOff = 120, // 0x78,

        /// <summary>@brief Reset all controllers (ALL_CTRL_OFF)</summary>
        ResetAllControllers = 121, // 0x79

        LOCAL_CONTROL = 122, // 0x7A,

        /// <summary>@brief All notes off (ALL_NOTES_OFF)</summary>
        AllNotesOff = 123, // 0x7B

        OMNI_OFF = 124, // 0x7C,
        OMNI_ON = 125, // 0x7D,
        POLY_OFF = 126, // 0x7E,
        POLY_ON = 127, // 0x7F
    }


    /// <summary>@brief
    /// General MIDI RPN event numbers (LSB, MSB = 0).
    /// Parameter numbers are configured in two steps:\n
    /// first select the parameter, then send the data entry value.\n
    /// For example, to set pitch bend sensitivity to 12 semitones, send the following controller MIDI messages:\n
    ///     - MPTKEvent#Controller=RPN_MSB (101) MPTKEvent#Value=0
    ///     - MPTKEvent#Controller=RPN_LSB (100) MPTKEvent#Value=midi_rpn_event.RPN_PITCH_BEND_RANGE
    ///     - MPTKEvent#Controller=DATA_ENTRY_MSB (6) MPTKEvent#Value=12
    ///     - MPTKEvent#Controller=DATA_ENTRY_LSB (38) MPTKEvent#Value=0
    /// https://www.2writers.com/Eddie/TutNrpn.htm
    /// </summary>
    public enum midi_rpn_event
    {
        /// <summary>@brief
        /// Change pitch bend sensitivity
        /// </summary>
        RPN_PITCH_BEND_RANGE = 0x00,

        RPN_CHANNEL_FINE_TUNE = 0x01,
        RPN_CHANNEL_COARSE_TUNE = 0x02,
        RPN_TUNING_PROGRAM_CHANGE = 0x03,
        RPN_TUNING_BANK_SELECT = 0x04,
        RPN_MODULATION_DEPTH_RANGE = 0x05
    }

    /// <summary>@brief
    /// MIDI meta event type. Meta events are optional MIDI information; none are mandatory.\n
    /// In MPTKEvent, the MPTKEvent.Meta attribute defines the type of meta event. 
    /// </summary>
    public enum MPTKMeta : byte
    {
        /// <summary>@brief Track sequence number</summary>
        TrackSequenceNumber = 0x00,
        /// <summary>@brief Text event</summary>
        TextEvent = 0x01,
        /// <summary>@brief Copyright</summary>
        Copyright = 0x02,
        /// <summary>@brief Sequence track name</summary>
        SequenceTrackName = 0x03,
        /// <summary>@brief Track instrument name</summary>
        TrackInstrumentName = 0x04,
        /// <summary>@brief Lyric</summary>
        Lyric = 0x05,
        /// <summary>@brief Marker</summary>
        Marker = 0x06,
        /// <summary>@brief Cue point</summary>
        CuePoint = 0x07,
        /// <summary>@brief Program (patch) name</summary>
        ProgramName = 0x08,
        /// <summary>@brief Device (port) name</summary>
        DeviceName = 0x09,
        /// <summary>@brief MIDI channel (not part of the official SMF meta event set)</summary>
        MidiChannel = 0x20,

        /// <summary>@brief MIDI port (not part of the official SMF meta event set)</summary>
        MidiPort = 0x21,

        /// <summary>@brief End track</summary>
        EndTrack = 0x2F,

        /// <summary>@brief Set tempo
        /// MPTKEvent.Value contains the new microseconds-per-quarter-note value.
        /// @deprecated Since 2.10.0, tempo is no longer stored in MPTKEvent.Duration. Use MPTKEvent.QuarterPerMicroSecond2BeatPerMinute() for BPM conversion.
        /// </summary>
        SetTempo = 0x51,

        /// <summary>@brief SMPTE offset</summary>
        SmpteOffset = 0x54,

        /// <summary>@brief Time signature
        /// MPTKEvent.Value contains four bytes, from least significant to most significant. See MPTKEvent.ExtractFromInt():
        ///    -# Numerator (number of beats in a bar), 
        ///    -# Denominator (beat unit: 1 means 2, 2 means 4 (quarter), 3 means 8 (eighth), 4 means 16, ...)
        ///    -# TicksInMetronomeClick, usually 24 (MIDI clocks per metronome click)
        ///    -# No32ndNotesInQuarterNote, usually 8 (number of 32nd notes per MIDI quarter note)
        /// @deprecated Since 2.10.0, all fields are merged into MPTKEvent.Value.
        /// </summary>
        TimeSignature = 0x58,

        /// <summary>@brief Key signature
        /// MPTKEvent.Value contains two bytes, from least significant to most significant. See MPTKEvent.ExtractFromInt().
        ///     -# SharpsFlats (number of sharps/flats) 
        ///     -# MajorMinor flag (0 the scale is major, 1 the scale is minor).
        /// @deprecated Since 2.10.0, MPTKEvent.Duration no longer contains MajorMinor.
        /// </summary>
        KeySignature = 0x59,

        /// <summary>@brief Sequencer specific</summary>
        SequencerSpecific = 0x7F,
    }
    /// @}

    /// @name Playback Lifecycle Enums
    /// @{
    [System.Serializable]
    public enum EventEndMidiEnum
    {
        MidiEnd,
        ApiStop,
        Replay,
        Next,
        Previous,
        MidiErr,
        Loop
    }
    /// @}

    /// @name Load Status Enums
    /// @{

    /// <summary>@brief
    /// Status of the last MIDI file load operation.
    /// @li      -1: midi file is loading
    /// @li       0: success, MIDI file loaded
    /// @li       1: error, no MIDI file found
    /// @li       2: error, not a MIDI file (size too short)
    /// @li       3: error, not a MIDI file (MThd signature not found)
    /// @li       4: error, network error or site not found.
    /// </summary>
    [System.Serializable]
    public enum LoadingStatusMidiEnum
    {
        /// <summary>@brief
        /// -1: MIDI file is loading.
        /// </summary>
        NotYetDefined = -1,

        /// <summary>@brief
        /// 0: success, MIDI file loaded.
        /// </summary>
        Success = 0,

        /// <summary>@brief
        /// 1: error, no MIDI file found.
        /// </summary>
        NotFound = 1,

        /// <summary>@brief
        /// 2: error, not a MIDI file (size too short).
        /// </summary>
        TooShortSize = 2,

        /// <summary>@brief
        /// 3: error, not a MIDI file (MThd signature not found).
        /// </summary>
        NoMThdSignature = 3,

        /// <summary>@brief
        /// 4: error, network error or site not found.
        /// </summary>
        NetworkError = 4,

        /// <summary>@brief
        /// 5: error, MIDI file is corrupted (error detected while loading MIDI events).
        /// </summary>
        MidiFileInvalid = 5,

        /// <summary>@brief
        /// 6: SoundFont not loaded.
        /// </summary>
        SoundFontNotLoaded = 6,

        /// <summary>@brief
        /// 7: error, already playing.
        /// </summary>
        AlreadyPlaying = 7,

        /// <summary>@brief
        /// 8: error, MPTK_MidiName must start with file://, http://, or https:// (MidiExternalPlayer only).
        /// </summary>
        MidiNameInvalid = 8,

        /// <summary>@brief
        /// 9: error, set MPTK_MidiName by script or in the Inspector with a MIDI URL/path before playing.
        /// </summary>
        MidiNameNotDefined = 9,

        /// <summary>@brief
        /// 10: error, Read 0 byte from the MIDI file.
        /// </summary>
        MidiFileEmpty = 10,
    }

    /// <summary>@brief
    /// Status of the last SoundFont load operation.
    /// </summary>
    [System.Serializable]
    public enum LoadingStatusSoundFontEnum
    {
        /// <summary>@brief
        /// -1: SoundFont is loading.
        /// </summary>
        InProgress = -1,

        /// <summary>@brief
        /// 0: success, SoundFont loaded.
        /// </summary>
        Success = 0,

        /// <summary>@brief
        /// 1: error, no SoundFont found.
        /// </summary>
        //NotFound = 1,

        /// <summary>@brief
        /// 2: error, not a SoundFont (size too short).
        /// </summary>
        //TooShortSize = 2,

        /// <summary>@brief
        /// 3: error, not a SoundFont (RIFF signature not found).
        /// </summary>
        NoRIFFSignature = 3,

        /// <summary>@brief
        /// 4: error, network error or site not found.
        /// </summary>
        NetworkError = 4,

        /// <summary>@brief
        /// 5: error, SoundFont corrupted, error detected when loading the SoundFont.
        /// </summary>
        //MidiFileInvalid = 5,

        /// <summary>@brief
        /// 6: SoundFont not loaded.
        /// </summary>
        SoundFontNotLoaded = 6,

        /// <summary>@brief
        /// 7: error, already playing.
        /// </summary>
        ///AlreadyPlaying = 7,

        /// <summary>@brief
        /// 8: error, URL must start with file:// or http:// or https://
        /// </summary>
        InvalidURL = 8,

        ///// <summary>@brief
        ///// 9: error,  Set MPTK_MidiName by script or in the inspector with Midi Url/path before playing.
        ///// </summary>
        //MidiNameNotDefined = 9,

        ///// <summary>@brief
        ///// 10: error, Read 0 byte from the SoundFont file.
        ///// </summary>
        SoundFontEmpty = 10,

        ///// <summary>@brief
        ///// 100: Not yet SF loaded.
        ///// </summary>
        Unknown = 100,
    }
    /// @}
}

