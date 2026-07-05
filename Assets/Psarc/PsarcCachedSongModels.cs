using System;
using System.Collections.Generic;

public static class PsarcCachedSongFormat
{
    public const int SchemaVersion = 20;
    public const string ManifestFileName = "song.rs2song.json";
    public const string ContentDirectoryName = "psarc_content";
    public const string ImportedFolderPrefix = "__psarc_";
    public const string LegacyContentDirectoryName = "rocksmith_content";
    public const string LegacyImportedFolderPrefix = "__rocksmith_";

    public static bool IsImportedFolderName(string folderName)
    {
        return !string.IsNullOrWhiteSpace(folderName) &&
               (folderName.StartsWith(ImportedFolderPrefix, StringComparison.OrdinalIgnoreCase) ||
                folderName.StartsWith(LegacyImportedFolderPrefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Turns a legacy unpacked-cache folder name like "__psarc_Some_Song_v5_DD_p_d8ff5397"
    /// into a readable display name ("Some_Song_v5_DD_p") by removing the game-generated
    /// prefix and trailing hash. Names without the prefix are returned unchanged.
    /// </summary>
    public static string StripImportedFolderDecorations(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return folderName ?? string.Empty;

        string name = folderName.Trim();
        if (name.StartsWith(ImportedFolderPrefix, StringComparison.OrdinalIgnoreCase))
            name = name.Substring(ImportedFolderPrefix.Length);
        else if (name.StartsWith(LegacyImportedFolderPrefix, StringComparison.OrdinalIgnoreCase))
            name = name.Substring(LegacyImportedFolderPrefix.Length);
        else
            return name;

        int separatorIndex = name.LastIndexOf('_');
        if (separatorIndex > 0)
        {
            string suffix = name.Substring(separatorIndex + 1);
            bool looksLikeHash = suffix.Length >= 6;
            for (int i = 0; looksLikeHash && i < suffix.Length; i++)
            {
                if (!Uri.IsHexDigit(suffix[i]))
                    looksLikeHash = false;
            }

            if (looksLikeHash)
                name = name.Substring(0, separatorIndex);
        }

        return string.IsNullOrWhiteSpace(name) ? folderName.Trim() : name;
    }
}

[Serializable]
public sealed class PsarcCachedSongManifest
{
    public int schemaVersion = PsarcCachedSongFormat.SchemaVersion;
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
    public List<PsarcCachedArrangementSummary> arrangements = new List<PsarcCachedArrangementSummary>();
}

[Serializable]
public sealed class PsarcCachedArrangementSummary
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
public sealed class PsarcCachedArrangementPart
{
    public int schemaVersion = PsarcCachedSongFormat.SchemaVersion;
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
    public PsarcCachedArrangementTimingData timing = new PsarcCachedArrangementTimingData();
    public PsarcCachedArrangementToneData tones = new PsarcCachedArrangementToneData();
    public PsarcCachedGeneratedPartInfo generatedPart = new PsarcCachedGeneratedPartInfo();
    public List<PsarcCachedNoteData> notes = new List<PsarcCachedNoteData>();
    public List<PsarcCachedArpeggioGuideData> arpeggioGuides = new List<PsarcCachedArpeggioGuideData>();
    public List<PsarcCachedGeneratedNoteEvent> generatedNotes = new List<PsarcCachedGeneratedNoteEvent>();
}

[Serializable]
public sealed class PsarcCachedArrangementToneData
{
    public string baseToneName;
    public List<PsarcCachedToneChangeData> changes = new List<PsarcCachedToneChangeData>();
    public List<PsarcCachedToneDefinitionData> definitions = new List<PsarcCachedToneDefinitionData>();
}

[Serializable]
public sealed class PsarcCachedToneChangeData
{
    public float timeSeconds;
    public string toneName;
    public int toneId = -1;
}

[Serializable]
public sealed class PsarcCachedToneDefinitionData
{
    public string name;
    public string key;
    public string rawJson;
    public string preferredPresetName;
    public string fallbackSearchText;
}

[Serializable]
public sealed class PsarcCachedGeneratedPartInfo
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
public sealed class PsarcCachedNoteData
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
    public List<PsarcCachedBendPointData> bendPoints = new List<PsarcCachedBendPointData>();
    public List<PsarcCachedTechniqueSegmentData> techniqueSegments = new List<PsarcCachedTechniqueSegmentData>();
}

[Serializable]
public sealed class PsarcCachedBendPointData
{
    public float timeSeconds;
    public float step;
}

[Serializable]
public sealed class PsarcCachedTechniqueSegmentData
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
public sealed class PsarcCachedArpeggioGuideData
{
    public int id;
    public float startTime;
    public float endTime;
    public string chordName;
    public int[] stringFrets;
}

[Serializable]
public sealed class PsarcCachedGeneratedNoteEvent
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
    public List<PsarcCachedGeneratedPitchPoint> pitchCurve = new List<PsarcCachedGeneratedPitchPoint>();
}

[Serializable]
public sealed class PsarcCachedGeneratedPitchPoint
{
    public float normalizedTime;
    public float semitoneOffset;
}

[Serializable]
public sealed class PsarcCachedArrangementTimingData
{
    public float averageTempoBpm = 120f;
    public int capo;
    public List<PsarcCachedEbeatData> ebeats = new List<PsarcCachedEbeatData>();
    public List<PsarcCachedSectionData> sections = new List<PsarcCachedSectionData>();
}

[Serializable]
public sealed class PsarcCachedEbeatData
{
    public float timeSeconds;
    public short measure = -1;
}

[Serializable]
public sealed class PsarcCachedSectionData
{
    public string name;
    public short number;
    public float timeSeconds;
}
