using System;

namespace MPTK.NAudio.Midi
{
    /// <summary>@brief
    /// MIDI MetaEvent Type
    /// </summary>
    public enum MetaEventType : byte 
    {
        /// <summary>@briefTrack sequence number</summary>
        TrackSequenceNumber = 0x00,
        /// <summary>@briefText event</summary>
        TextEvent = 0x01,
        /// <summary>@briefCopyright</summary>
        Copyright = 0x02,
        /// <summary>@briefSequence track name</summary>
        SequenceTrackName = 0x03,
        /// <summary>@briefTrack instrument name</summary>
        TrackInstrumentName = 0x04,
        /// <summary>@briefLyric</summary>
        Lyric = 0x05,
        /// <summary>@briefMarker</summary>
        Marker = 0x06,
        /// <summary>@briefCue point</summary>
        CuePoint = 0x07,
        /// <summary>@briefProgram (patch) name</summary>
        ProgramName = 0x08,
        /// <summary>@briefDevice (port) name</summary>
        DeviceName = 0x09,
        /// <summary>@briefMIDI Channel (not official?)</summary>
        MidiChannel = 0x20,
        /// <summary>@briefMIDI Port (not official?)</summary>
        MidiPort = 0x21,
        /// <summary>@briefEnd track</summary>
        EndTrack = 0x2F,
        /// <summary>@briefSet tempo</summary>
        SetTempo = 0x51,
        /// <summary>@briefSMPTE offset</summary>
        SmpteOffset = 0x54,
        /// <summary>@briefTime signature</summary>
        TimeSignature = 0x58,
        /// <summary>@briefKey signature</summary>
        KeySignature = 0x59,
        /// <summary>@briefSequencer specific</summary>
        SequencerSpecific = 0x7F,
    }
}
