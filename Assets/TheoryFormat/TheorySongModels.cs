using System;
using System.Collections.Generic;

[Serializable]
public sealed class TheorySongManifest
{
    public string formatId = TheoryPackageFormat.FormatId;
    public int schemaVersion = TheoryPackageFormat.SchemaVersion;
    public string packageId;
    public long createdAtUtcTicks;
    public long modifiedAtUtcTicks;
    public string title;
    public string artist;
    public string album;
    public string subtitle;
    public string genre;
    public string year;
    public string defaultArrangementId;
    public string primaryAudioEntry;
    public string coverArtEntry;
    public float durationSeconds;
    public int difficultyRating;
    public TheoryImportProvenance provenance = new TheoryImportProvenance();
    public List<TheoryAudioAsset> audio = new List<TheoryAudioAsset>();
    public List<TheoryStemAsset> stems = new List<TheoryStemAsset>();
    public List<TheoryArrangementSummary> arrangements = new List<TheoryArrangementSummary>();

    public void EnsureDefaults()
    {
        formatId = string.IsNullOrWhiteSpace(formatId) ? TheoryPackageFormat.FormatId : formatId;
        schemaVersion = schemaVersion <= 0 ? TheoryPackageFormat.SchemaVersion : schemaVersion;
        if (string.IsNullOrWhiteSpace(packageId))
            packageId = Guid.NewGuid().ToString("N");
        provenance ??= new TheoryImportProvenance();
        audio ??= new List<TheoryAudioAsset>();
        stems ??= new List<TheoryStemAsset>();
        arrangements ??= new List<TheoryArrangementSummary>();
    }
}

[Serializable]
public sealed class TheoryImportProvenance
{
    public string sourceType;
    public string sourceDisplayName;
    public string sourcePath;
    public long sourceLastWriteUtcTicks;
    public long sourceSizeBytes;
    public long importedAtUtcTicks;
    public string converterName;
    public string converterVersion;
}

[Serializable]
public sealed class TheoryAudioAsset
{
    public string id;
    public string entry;
    public string displayName;
    public string role;
    public string contentType;
    public long sourceSizeBytes;
    public bool defaultForPlayback = true;
}

[Serializable]
public sealed class TheoryStemAsset
{
    public string id;
    public string displayName;
    public string entry;
    public string contentType;
    public long sourceSizeBytes;
    public string provider = "demucs";
    public string model;
    public long generatedAtUtcTicks;
}

[Serializable]
public sealed class TheoryArrangementSummary
{
    public string arrangementId;
    public string displayName;
    public string instrumentType;
    public string route;
    public string groupId;
    public string groupDisplayName;
    public string difficultyLabel;
    public int difficultyUiIndex = -1;
    public bool hasDifficultyVariants;
    public string entry;
    public int noteCount;
    public int tabCount;
    public int score;
    public int difficultyRating;
    public int[] tuningPitches;
    public string tuningDisplayName;
}

[Serializable]
public sealed class TheoryArrangementData
{
    public int schemaVersion = TheoryPackageFormat.SchemaVersion;
    public string arrangementId;
    public string displayName;
    public string instrumentType;
    public string route;
    public string groupId;
    public string groupDisplayName;
    public string difficultyLabel;
    public int difficultyUiIndex = -1;
    public bool hasDifficultyVariants;
    public float durationSeconds;
    public int difficultyRating;
    public int[] tuningPitches;
    public string tuningDisplayName;
    public TheoryTimingData timing = new TheoryTimingData();
    public TheoryToneData tones = new TheoryToneData();
    public TheoryGeneratedPartInfo generatedPart = new TheoryGeneratedPartInfo();
    public List<TheoryNoteData> notes = new List<TheoryNoteData>();
    public List<TheoryArpeggioGuideData> arpeggioGuides = new List<TheoryArpeggioGuideData>();
    public List<TheoryGeneratedNoteEvent> generatedNotes = new List<TheoryGeneratedNoteEvent>();

    public void EnsureDefaults()
    {
        timing ??= new TheoryTimingData();
        tones ??= new TheoryToneData();
        tones.EnsureDefaults();
        generatedPart ??= new TheoryGeneratedPartInfo();
        notes ??= new List<TheoryNoteData>();
        arpeggioGuides ??= new List<TheoryArpeggioGuideData>();
        generatedNotes ??= new List<TheoryGeneratedNoteEvent>();
    }
}

[Serializable]
public sealed class TheoryToneData
{
    public string baseToneName;
    public List<TheoryToneChangeData> changes = new List<TheoryToneChangeData>();
    public List<TheoryToneDefinitionData> definitions = new List<TheoryToneDefinitionData>();

    public void EnsureDefaults()
    {
        changes ??= new List<TheoryToneChangeData>();
        definitions ??= new List<TheoryToneDefinitionData>();
        for (int i = 0; i < definitions.Count; i++)
            definitions[i]?.EnsureDefaults();
    }
}

[Serializable]
public sealed class TheoryToneChangeData
{
    public float timeSeconds;
    public string toneName;
    public int toneId = -1;
}

[Serializable]
public sealed class TheoryToneDefinitionData
{
    public string name;
    public string key;
    public TheoryTonePresetData preset = new TheoryTonePresetData();
    public TheoryToneFallbackData fallback = new TheoryToneFallbackData();

    public void EnsureDefaults()
    {
        preset ??= new TheoryTonePresetData();
        preset.EnsureDefaults();
        fallback ??= new TheoryToneFallbackData();
    }
}

[Serializable]
public sealed class TheoryTonePresetData
{
    public string presetId;
    public string presetName;
    public float inputGainDb;
    public float outputGainDb;
    public List<TheoryTonePedalSlotData> pedalChain = new List<TheoryTonePedalSlotData>();

    public void EnsureDefaults()
    {
        pedalChain ??= new List<TheoryTonePedalSlotData>();
    }
}

[Serializable]
public sealed class TheoryTonePedalSlotData
{
    public string instanceId;
    public string pedalType;
    public string descriptorId;
    public bool enabled = true;
    public string settingsJson;
}

[Serializable]
public sealed class TheoryToneFallbackData
{
    public string preferredPresetName;
    public string searchText;
}

[Serializable]
public sealed class TheoryTimingData
{
    public float averageTempoBpm = 120f;
    public int capo;
    public List<TheoryBeatData> beats = new List<TheoryBeatData>();
    public List<TheorySectionData> sections = new List<TheorySectionData>();
}

[Serializable]
public sealed class TheoryBeatData
{
    public float timeSeconds;
    public short measure = -1;
}

[Serializable]
public sealed class TheorySectionData
{
    public string name;
    public short number;
    public float timeSeconds;
}

[Serializable]
public sealed class TheoryNoteData
{
    public int id;
    public float time;
    public float duration;
    public int stringIndex;
    public int fret;
    public string noteName;
    public int chordId = -1;
    public string chordName;
    public int primaryTechnique;
    public int slideTargetFret = -1;
    public float bendStep;
    public float bendVisualStartTime = -1f;
    public float bendVisualDuration;
    public bool bendPreBend;
    public bool bendRelease;
    public bool muted;
    public bool palmMute;
    public bool fretHandMute;
    public bool harmonic;
    public bool accent;
    public bool tap;
    public bool tremolo;
    public bool pinchHarmonic;
    public bool hammerOn;
    public bool pullOff;
    public bool hopo;
    public bool vibrato;
    public int vibratoStrength;
    public float maxBend;
    public bool legato;
    public bool requiresPluck = true;
    public int linkedFromNoteId = -1;
    public List<TheoryBendPointData> bendPoints = new List<TheoryBendPointData>();
    public List<TheoryTechniqueSegmentData> techniqueSegments = new List<TheoryTechniqueSegmentData>();
}

[Serializable]
public sealed class TheoryBendPointData
{
    public float timeSeconds;
    public float step;
}

[Serializable]
public sealed class TheoryTechniqueSegmentData
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
public sealed class TheoryArpeggioGuideData
{
    public int id;
    public float startTime;
    public float endTime;
    public string chordName;
    public int[] stringFrets;
}

[Serializable]
public sealed class TheoryGeneratedPartInfo
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
public sealed class TheoryGeneratedNoteEvent
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
    public List<TheoryGeneratedPitchPoint> pitchCurve = new List<TheoryGeneratedPitchPoint>();
}

[Serializable]
public sealed class TheoryGeneratedPitchPoint
{
    public float normalizedTime;
    public float semitoneOffset;
}

[Serializable]
public sealed class TheoryEditorState
{
    public int schemaVersion = TheoryPackageFormat.SchemaVersion;
    public string selectedArrangementId;
    public double cursorTimeSeconds;
    public double snapSeconds = 0.05;
    public bool snapEnabled = true;
    public double largeNudgeSeconds = 0.1;
    public double smallNudgeSeconds = 0.01;
    public bool showBeatGrid = true;
    public bool metronomeEnabled;
    public bool noteClapsEnabled;
    public float playbackSpeed = 1f;
    public List<TheoryEditorBeatMarker> beatMarkers = new List<TheoryEditorBeatMarker>();
    public List<TheoryEditorSyncPoint> syncPoints = new List<TheoryEditorSyncPoint>();

    public void EnsureDefaults()
    {
        schemaVersion = schemaVersion <= 0 ? TheoryPackageFormat.SchemaVersion : schemaVersion;
        beatMarkers ??= new List<TheoryEditorBeatMarker>();
        syncPoints ??= new List<TheoryEditorSyncPoint>();
    }
}

[Serializable]
public sealed class TheoryEditorBeatMarker
{
    public string id;
    public double beatPosition;
    public double audioTimeSeconds;
    public int barNumber;
    public bool isDownbeat;
    public bool isAnchor;
    public string label;
    public double bpm;
    public int timeSignatureNumerator = 4;
    public int timeSignatureDenominator = 4;
    public bool generatedBySynchTheory;
    public double synchTheoryConfidence;
    public string synchTheorySource;
}

[Serializable]
public sealed class TheoryEditorSyncPoint
{
    public string id;
    public double chartTimeSeconds;
    public double audioTimeSeconds;
    public string label;
}
