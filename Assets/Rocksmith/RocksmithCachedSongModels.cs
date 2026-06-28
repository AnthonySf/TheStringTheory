using System;
using System.Collections.Generic;

public static class RocksmithCachedSongFormat
{
    public const int SchemaVersion = 20;
    public const string ManifestFileName = "song.rs2song.json";
    public const string ContentDirectoryName = "psarc_content";
    public const string ImportedFolderPrefix = "__psarc_";
    public const string LegacyContentDirectoryName = "rocksmith_content";
    public const string LegacyImportedFolderPrefix = "__rocksmith_";
}

[Serializable]
public sealed class RocksmithCachedSongManifest
{
    public int schemaVersion = RocksmithCachedSongFormat.SchemaVersion;
    public string sourcePsarcPath;
    public long sourcePsarcLastWriteUtcTicks;
    public long importedAtUtcTicks;
    public string displayName;
    public string artist;
    public string album;
    public string subtitle;
    public string artworkPath;
    public string audioPath;
    public string previewAudioPath;
    public float durationSeconds;
    public int difficultyRating;
    public int toneDefinitionScanVersion;
    public int toneDefinitionCount;
    public List<RocksmithCachedArrangementSummary> arrangements = new List<RocksmithCachedArrangementSummary>();
}

[Serializable]
public sealed class RocksmithCachedArrangementSummary
{
    public string partId;
    public string displayName;
    public string route;
    public string arrangementGroupId;
    public string arrangementDisplayName;
    public string difficultyLabel;
    public int difficultyUiIndex = -1;
    public bool hasDifficultyVariants;
    public string partFilePath;
    public int noteCount;
    public int tabCount;
    public int score;
    public int difficultyRating;
    public int[] tuningPitches;
    public string tuningDisplayName;
}

[Serializable]
public sealed class RocksmithCachedArrangementPart
{
    public int schemaVersion = RocksmithCachedSongFormat.SchemaVersion;
    public string partId;
    public string displayName;
    public string route;
    public string arrangementGroupId;
    public string arrangementDisplayName;
    public string difficultyLabel;
    public int difficultyUiIndex = -1;
    public bool hasDifficultyVariants;
    public float durationSeconds;
    public int difficultyRating;
    public int[] tuningPitches;
    public string tuningDisplayName;
    public RocksmithCachedArrangementTimingData timing = new RocksmithCachedArrangementTimingData();
    public RocksmithCachedArrangementToneData tones = new RocksmithCachedArrangementToneData();
    public RocksmithCachedGeneratedPartInfo generatedPart = new RocksmithCachedGeneratedPartInfo();
    public List<RocksmithCachedNoteData> notes = new List<RocksmithCachedNoteData>();
    public List<RocksmithCachedArpeggioGuideData> arpeggioGuides = new List<RocksmithCachedArpeggioGuideData>();
    public List<RocksmithCachedGeneratedNoteEvent> generatedNotes = new List<RocksmithCachedGeneratedNoteEvent>();
}

[Serializable]
public sealed class RocksmithCachedArrangementToneData
{
    public string baseToneName;
    public List<RocksmithCachedToneChangeData> changes = new List<RocksmithCachedToneChangeData>();
    public List<RocksmithCachedToneDefinitionData> definitions = new List<RocksmithCachedToneDefinitionData>();
}

[Serializable]
public sealed class RocksmithCachedToneChangeData
{
    public float timeSeconds;
    public string toneName;
    public int toneId = -1;
}

[Serializable]
public sealed class RocksmithCachedToneDefinitionData
{
    public string name;
    public string key;
    public string rawJson;
    public string preferredPresetName;
    public string fallbackSearchText;
}

[Serializable]
public sealed class RocksmithCachedGeneratedPartInfo
{
    public string partId;
    public string displayName;
    public string instrumentName;
    public int sourceMidiChannel = -1;
    public int sourceMidiProgram = 29;
    public int preferredBank = -1;
    public bool isDrum;
    public bool isGuitarFamily = true;
    public bool isExplicitHarmonicPart;
}

[Serializable]
public sealed class RocksmithCachedNoteData
{
    public int id;
    public float time;
    public float duration;
    public int stringIdx;
    public int fret;
    public string note;
    public int chordId;
    public string chordName;
    public int technique;
    public int slideTargetFret = -1;
    public float bendStep;
    public float bendVisualStartTime = -1f;
    public float bendVisualDuration;
    public bool bendPreBend;
    public bool bendRelease;
    public bool isMuted;
    public bool isPalmMute;
    public bool isFretHandMute;
    public bool isHarmonic;
    public bool isAccent;
    public bool isTap;
    public bool isTremolo;
    public bool isPinchHarmonic;
    public bool isHammerOn;
    public bool isPullOff;
    public bool isHopo;
    public bool hasVibrato;
    public int vibratoStrength;
    public float maxBend;
    public bool isLegato;
    public bool requiresPluck = true;
    public int linkedFromNoteId = -1;
    public List<RocksmithCachedBendPointData> bendPoints = new List<RocksmithCachedBendPointData>();
    public List<RocksmithCachedTechniqueSegmentData> techniqueSegments = new List<RocksmithCachedTechniqueSegmentData>();
}

[Serializable]
public sealed class RocksmithCachedBendPointData
{
    public float timeSeconds;
    public float step;
}

[Serializable]
public sealed class RocksmithCachedTechniqueSegmentData
{
    public int type;
    public float startOffset;
    public float endOffset;
    public int startFret;
    public int endFret;
    public float startBend;
    public float endBend;
}

[Serializable]
public sealed class RocksmithCachedArpeggioGuideData
{
    public int id;
    public float startTime;
    public float endTime;
    public string chordName;
    public int[] stringFrets;
}

[Serializable]
public sealed class RocksmithCachedGeneratedNoteEvent
{
    public float startTimeSeconds;
    public float durationSeconds;
    public float pitchPreRollSeconds;
    public int midiNote;
    public int velocity;
    public int channel;
    public string partId;
    public string partName;
    public int techniqueVariant;
    public int legatoTransitionKind;
    public float attackVelocityScale = 1f;
    public float vibratoDepthSemitones;
    public float vibratoRateHz;
    public float vibratoDelayNormalized;
    public float vibratoFadeNormalized;
    public int pitchBendRangeSemitones;
    public List<RocksmithCachedGeneratedPitchPoint> pitchCurve = new List<RocksmithCachedGeneratedPitchPoint>();
}

[Serializable]
public sealed class RocksmithCachedGeneratedPitchPoint
{
    public float normalizedTime;
    public float semitoneOffset;
}

[Serializable]
public sealed class RocksmithCachedArrangementTimingData
{
    public float averageTempoBpm = 120f;
    public int capo;
    public List<RocksmithCachedEbeatData> ebeats = new List<RocksmithCachedEbeatData>();
    public List<RocksmithCachedSectionData> sections = new List<RocksmithCachedSectionData>();
}

[Serializable]
public sealed class RocksmithCachedEbeatData
{
    public float timeSeconds;
    public short measure = -1;
}

[Serializable]
public sealed class RocksmithCachedSectionData
{
    public string name;
    public short number;
    public float timeSeconds;
}
