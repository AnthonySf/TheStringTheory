using System;
using System.Collections.Generic;

public enum SongPlaybackAudioMode
{
    Generated = 0,
    Mp3 = 1,
    Muted = 2
}

public enum GeneratedTechniqueVariant
{
    Normal = 0,
    PalmMute = 1,
    StraightMute = 2,
    Harmonic = 3
}

public enum GeneratedLegatoTransitionKind
{
    None = 0,
    Slide = 1,
    HammerOn = 2,
    PullOff = 3
}

[Serializable]
public sealed class GeneratedPlaybackPitchPoint
{
    public float normalizedTime;
    public float semitoneOffset;
}

[Serializable]
public sealed class GeneratedPlaybackPartInfo
{
    public string partId;
    public string displayName;
    public string instrumentName;
    public int sourceMidiChannel = -1;
    public int sourceMidiProgram = -1;
    public int preferredBank = -1;
    public bool isDrum;
    public bool isGuitarFamily;
    public bool isExplicitHarmonicPart;
}

[Serializable]
public sealed class GeneratedPlaybackChannelAssignment
{
    public int channel;
    public int bank = -1;
    public int preset;
    public bool isDrum;
    public string label;
    public string sourcePartId;
    public string sourcePartName;
    public int pitchBendRangeSemitones;
}

[Serializable]
public sealed class GeneratedPlaybackNoteEvent
{
    public float startTimeSeconds;
    public float durationSeconds;
    public float pitchPreRollSeconds;
    public int midiNote;
    public int velocity;
    public int channel;
    public string partId;
    public string partName;
    public GeneratedTechniqueVariant techniqueVariant;
    public GeneratedLegatoTransitionKind legatoTransitionKind;
    public float attackVelocityScale = 1f;
    public float vibratoDepthSemitones;
    public float vibratoRateHz;
    public float vibratoDelayNormalized;
    public float vibratoFadeNormalized;
    public int pitchBendRangeSemitones;
    public List<GeneratedPlaybackPitchPoint> pitchCurve = new List<GeneratedPlaybackPitchPoint>();

    public float EndTimeSeconds => startTimeSeconds + durationSeconds;
    public bool HasPitchCurve => pitchCurve != null && pitchCurve.Count > 1 && pitchBendRangeSemitones > 0;
    public bool HasVibrato => vibratoDepthSemitones > 0.01f && vibratoRateHz > 0.01f;
    public float PitchStartSemitoneOffset => pitchCurve != null && pitchCurve.Count > 0 ? pitchCurve[0].semitoneOffset : 0f;
}

[Serializable]
public sealed class GeneratedPlaybackArrangement
{
    public string sourcePath;
    public float durationSeconds;
    public List<GeneratedPlaybackPartInfo> parts = new List<GeneratedPlaybackPartInfo>();
    public List<GeneratedPlaybackChannelAssignment> channelAssignments = new List<GeneratedPlaybackChannelAssignment>();
    public List<GeneratedPlaybackNoteEvent> notes = new List<GeneratedPlaybackNoteEvent>();

    public bool IsValid => parts != null && parts.Count > 0;
}
