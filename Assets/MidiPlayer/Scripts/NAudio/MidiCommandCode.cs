namespace MPTK.NAudio.Midi 
{
    /// <summary>@brief
    /// MIDI command codes
    /// </summary>
    public enum MidiCommandCode : byte 
    {
        /// <summary>@briefNote Off</summary>
        NoteOff = 0x80,
        /// <summary>@briefNote On</summary>
        NoteOn = 0x90,
        /// <summary>@briefKey After-touch</summary>
        KeyAfterTouch = 0xA0,
        /// <summary>@briefControl change</summary>
        ControlChange = 0xB0,
        /// <summary>@briefPatch change</summary>
        PatchChange = 0xC0,
        /// <summary>@briefChannel after-touch</summary>
        ChannelAfterTouch = 0xD0,
        /// <summary>@briefPitch wheel change</summary>
        PitchWheelChange = 0xE0,
        /// <summary>@briefSysex message</summary>
        Sysex = 0xF0,
        /// <summary>@briefEox (comes at end of a sysex message)</summary>
        Eox = 0xF7,
        /// <summary>@briefTiming clock (used when synchronization is required)</summary>
        TimingClock = 0xF8,
        /// <summary>@briefStart sequence</summary>
        StartSequence = 0xFA,
        /// <summary>@briefContinue sequence</summary>
        ContinueSequence = 0xFB,
        /// <summary>@briefStop sequence</summary>
        StopSequence = 0xFC,
        /// <summary>@briefAuto-Sensing</summary>
        AutoSensing = 0xFE,
        /// <summary>@briefMeta-event</summary>
        MetaEvent = 0xFF,
    }
}
