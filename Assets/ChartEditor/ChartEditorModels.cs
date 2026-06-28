using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public enum ChartEditorTrackRole
{
    LeadGuitar,
    RhythmGuitar,
    Bass,
    Drums,
    Piano,
    Vocals,
    Custom
}

public enum ChartEditorSourceKind
{
    Empty,
    GuitarPro,
    MusicXml,
    ArrangementCache,
    Psarc,
    Folder,
    TheoryPackage,
    StringTheoryProject
}

[Serializable]
public sealed class ChartEditorProject
{
    public const int CurrentSchemaVersion = 2;

    public int schemaVersion = CurrentSchemaVersion;
    public string projectId;
    public ChartEditorSourceKind sourceKind;
    public string sourcePath;
    public string sourceFolder;
    public string savedProjectPath;
    public ChartEditorSongMetadata metadata = new ChartEditorSongMetadata();
    public ChartEditorAudioInfo audio = new ChartEditorAudioInfo();
    public List<ChartEditorTrack> tracks = new List<ChartEditorTrack>();
    public List<ChartEditorSection> sections = new List<ChartEditorSection>();
    public ChartEditorBeatMap beatMap = new ChartEditorBeatMap();
    public List<ChartEditorSyncPoint> syncPoints = new List<ChartEditorSyncPoint>();
    public ChartEditorProjectSettings settings = new ChartEditorProjectSettings();
    public string selectedTrackId;
    public double cursorTimeSeconds;
    public bool dirty;

    public float DurationSeconds
    {
        get
        {
            double max = Math.Max(0.0, audio?.durationSeconds ?? 0.0);
            if (tracks != null)
            {
                for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
                {
                    List<ChartEditorNote> notes = tracks[trackIndex]?.notes;
                    if (notes == null)
                        continue;

                    for (int noteIndex = 0; noteIndex < notes.Count; noteIndex++)
                    {
                        ChartEditorNote note = notes[noteIndex];
                        if (note != null)
                            max = Math.Max(max, note.timeSeconds + Math.Max(0.0, note.durationSeconds));
                    }
                }
            }

            if (sections != null)
            {
                for (int i = 0; i < sections.Count; i++)
                {
                    ChartEditorSection section = sections[i];
                    if (section != null)
                        max = Math.Max(max, section.endTimeSeconds);
                }
            }

            return Mathf.Max(0.1f, (float)max);
        }
    }

    public ChartEditorTrack SelectedTrack
    {
        get
        {
            if (tracks == null || tracks.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(selectedTrackId))
            {
                for (int i = 0; i < tracks.Count; i++)
                {
                    ChartEditorTrack track = tracks[i];
                    if (track != null &&
                        string.Equals(track.id ?? string.Empty, selectedTrackId, StringComparison.OrdinalIgnoreCase))
                    {
                        return track;
                    }
                }
            }

            selectedTrackId = tracks[0]?.id;
            return tracks[0];
        }
    }

    public void EnsureDefaults()
    {
        schemaVersion = CurrentSchemaVersion;
        if (string.IsNullOrWhiteSpace(projectId))
            projectId = Guid.NewGuid().ToString("N");
        metadata ??= new ChartEditorSongMetadata();
        audio ??= new ChartEditorAudioInfo();
        tracks ??= new List<ChartEditorTrack>();
        sections ??= new List<ChartEditorSection>();
        beatMap ??= new ChartEditorBeatMap();
        syncPoints ??= new List<ChartEditorSyncPoint>();
        settings ??= new ChartEditorProjectSettings();

        for (int i = 0; i < tracks.Count; i++)
            tracks[i]?.EnsureDefaults();
        for (int i = 0; i < sections.Count; i++)
            sections[i]?.EnsureDefaults();
        beatMap.EnsureDefaults();
        for (int i = 0; i < syncPoints.Count; i++)
            syncPoints[i]?.EnsureDefaults();

        if (string.IsNullOrWhiteSpace(selectedTrackId) && tracks.Count > 0)
            selectedTrackId = tracks[0]?.id;
    }
}

[Serializable]
public sealed class ChartEditorSongMetadata
{
    public string title;
    public string artist;
    public string album;
    public string genre;
    public string year;
    public string coverImagePath;
    public double previewStartTimeSeconds;
    public string defaultArrangementId;
}

[Serializable]
public sealed class ChartEditorAudioInfo
{
    public string sourcePath;
    public string displayName;
    public string extension;
    public double durationSeconds;
}

[Serializable]
public sealed class ChartEditorProjectSettings
{
    public double snapSeconds = 0.05;
    public bool snapEnabled = true;
    public double largeNudgeSeconds = 0.1;
    public double smallNudgeSeconds = 0.01;
    public bool showBeatGrid = true;
    public bool metronomeEnabled;
    public bool noteClapsEnabled;
    public float playbackSpeed = 1f;
}

[Serializable]
public sealed class ChartEditorTrack
{
    public string id;
    public string importedName;
    public string displayName;
    public ChartEditorTrackRole role;
    public ChartEditorTuningInfo tuning = new ChartEditorTuningInfo();
    public string colorHex;
    public bool visible = true;
    public bool muted;
    public bool solo;
    public ChartEditorGeneratedPartInfo generatedPart = new ChartEditorGeneratedPartInfo();
    public ChartEditorToneData tones = new ChartEditorToneData();
    public List<ChartEditorNote> notes = new List<ChartEditorNote>();
    public List<ChartEditorArpeggioGuide> arpeggioGuides = new List<ChartEditorArpeggioGuide>();
    public List<ChartEditorGeneratedNoteEvent> generatedNotes = new List<ChartEditorGeneratedNoteEvent>();
    public string generatedPlaybackNoteFingerprint;

    public void EnsureDefaults()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = string.IsNullOrWhiteSpace(importedName) ? "Track" : importedName;
        tuning ??= new ChartEditorTuningInfo();
        generatedPart ??= new ChartEditorGeneratedPartInfo();
        tones ??= new ChartEditorToneData();
        tones.EnsureDefaults();
        notes ??= new List<ChartEditorNote>();
        arpeggioGuides ??= new List<ChartEditorArpeggioGuide>();
        generatedNotes ??= new List<ChartEditorGeneratedNoteEvent>();
        for (int i = 0; i < generatedNotes.Count; i++)
            generatedNotes[i]?.EnsureDefaults();
        for (int i = 0; i < notes.Count; i++)
        {
            ChartEditorNote note = notes[i];
            note?.EnsureDefaults();
        }

        HashSet<string> seenNoteIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < notes.Count; i++)
        {
            ChartEditorNote note = notes[i];
            if (note == null)
                continue;

            while (string.IsNullOrWhiteSpace(note.id) || seenNoteIds.Contains(note.id))
                note.id = Guid.NewGuid().ToString("N");
            seenNoteIds.Add(note.id);
        }
    }
}

[Serializable]
public sealed class ChartEditorToneData
{
    public string baseToneName;
    public List<ChartEditorToneChange> changes = new List<ChartEditorToneChange>();
    public List<ChartEditorToneDefinition> definitions = new List<ChartEditorToneDefinition>();

    public void EnsureDefaults()
    {
        changes ??= new List<ChartEditorToneChange>();
        definitions ??= new List<ChartEditorToneDefinition>();
        for (int i = 0; i < definitions.Count; i++)
            definitions[i]?.EnsureDefaults();
    }
}

[Serializable]
public sealed class ChartEditorToneChange
{
    public float timeSeconds;
    public string toneName;
    public int toneId = -1;
}

[Serializable]
public sealed class ChartEditorToneDefinition
{
    public string name;
    public string key;
    public ChartEditorTonePresetData preset = new ChartEditorTonePresetData();
    public ChartEditorToneFallbackData fallback = new ChartEditorToneFallbackData();

    public void EnsureDefaults()
    {
        preset ??= new ChartEditorTonePresetData();
        preset.EnsureDefaults();
        fallback ??= new ChartEditorToneFallbackData();
    }
}

[Serializable]
public sealed class ChartEditorTonePresetData
{
    public string presetId;
    public string presetName;
    public float inputGainDb;
    public float outputGainDb;
    public List<ChartEditorTonePedalSlotData> pedalChain = new List<ChartEditorTonePedalSlotData>();

    public void EnsureDefaults()
    {
        pedalChain ??= new List<ChartEditorTonePedalSlotData>();
    }
}

[Serializable]
public sealed class ChartEditorTonePedalSlotData
{
    public string instanceId;
    public string pedalType;
    public string descriptorId;
    public bool enabled = true;
    public string settingsJson;
}

[Serializable]
public sealed class ChartEditorToneFallbackData
{
    public string preferredPresetName;
    public string searchText;
}

[Serializable]
public sealed class ChartEditorGeneratedPartInfo
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
public sealed class ChartEditorGeneratedNoteEvent
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
    public List<ChartEditorGeneratedPitchPoint> pitchCurve = new List<ChartEditorGeneratedPitchPoint>();

    public void EnsureDefaults()
    {
        pitchCurve ??= new List<ChartEditorGeneratedPitchPoint>();
    }
}

[Serializable]
public sealed class ChartEditorGeneratedPitchPoint
{
    public float normalizedTime;
    public float semitoneOffset;
}

[Serializable]
public sealed class ChartEditorArpeggioGuide
{
    public int id;
    public float startTime;
    public float endTime;
    public string chordName;
    public int[] stringFrets;
}

[Serializable]
public sealed class ChartEditorTuningInfo
{
    public string displayName;
    public int[] stringPitches;
}

[Serializable]
public sealed class ChartEditorNote
{
    public string id;
    public int sourceNoteId = -1;
    public double chartTimeSeconds;
    public double timeSeconds;
    public double durationSeconds;
    public double beatPosition;
    public double durationBeats;
    public bool usesBeatMapTiming = true;
    public int stringOrLane;
    public int fret;
    public int velocity = 95;
    public string noteName;
    public int chordId = -1;
    public string chordName;
    public NoteTechnique technique;
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
    public int vibratoStrength;
    public float maxBend;
    public bool legato;
    public bool requiresPluck = true;
    public int linkedFromNoteId = -1;
    public List<ChartEditorBendPoint> bendPoints = new List<ChartEditorBendPoint>();
    public List<ChartEditorTechniqueSegment> techniqueSegments = new List<ChartEditorTechniqueSegment>();

    public double EndTimeSeconds
    {
        get
        {
            double duration = Math.Max(0.0, durationSeconds);
            if (techniqueSegments != null)
            {
                for (int i = 0; i < techniqueSegments.Count; i++)
                {
                    ChartEditorTechniqueSegment segment = techniqueSegments[i];
                    if (segment == null)
                        continue;

                    duration = Math.Max(duration, Math.Max(0.0, Math.Max(segment.startOffset, segment.endOffset)));
                }
            }

            return timeSeconds + duration;
        }
    }

    public void EnsureDefaults()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = Guid.NewGuid().ToString("N");
        bendPoints ??= new List<ChartEditorBendPoint>();
        techniqueSegments ??= new List<ChartEditorTechniqueSegment>();
        muted = muted || palmMute || fretHandMute;
    }
}

[Serializable]
public sealed class ChartEditorBendPoint
{
    public float timeSeconds;
    public float step;
}

[Serializable]
public sealed class ChartEditorTechniqueSegment
{
    public NoteTechniqueSegmentType type;
    public float startOffset;
    public float endOffset;
    public int startFret;
    public int endFret;
    public float startBend;
    public float endBend;
}

[Serializable]
public sealed class ChartEditorSection
{
    public string id;
    public string name;
    public double chartStartTimeSeconds;
    public double chartEndTimeSeconds;
    public double startTimeSeconds;
    public double endTimeSeconds;
    public double startBeatPosition;
    public double endBeatPosition;
    public bool usesBeatMapTiming = true;
    public string optionalType;
    public string colorHex;
    public bool userEdited;

    public void EnsureDefaults()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(name))
            name = "Section";
        if (endTimeSeconds <= startTimeSeconds)
            endTimeSeconds = startTimeSeconds + 0.05;
        if (chartEndTimeSeconds <= chartStartTimeSeconds)
            chartEndTimeSeconds = chartStartTimeSeconds + Math.Max(0.05, endTimeSeconds - startTimeSeconds);
    }
}

[Serializable]
public sealed class ChartEditorBeatMap
{
    public double defaultTempoBpm = 120.0;
    public List<ChartEditorBeatMarker> beatMarkers = new List<ChartEditorBeatMarker>();
    public List<ChartEditorTempoRegion> tempoRegions = new List<ChartEditorTempoRegion>();
    public List<ChartEditorTimeSignatureChange> timeSignatures = new List<ChartEditorTimeSignatureChange>();

    public void EnsureDefaults()
    {
        if (defaultTempoBpm <= 0.001)
            defaultTempoBpm = 120.0;
        beatMarkers ??= new List<ChartEditorBeatMarker>();
        tempoRegions ??= new List<ChartEditorTempoRegion>();
        timeSignatures ??= new List<ChartEditorTimeSignatureChange>();
        if (timeSignatures.Count == 0)
        {
            timeSignatures.Add(new ChartEditorTimeSignatureChange
            {
                beatPosition = 0.0,
                numerator = 4,
                denominator = 4
            });
        }

        for (int i = 0; i < beatMarkers.Count; i++)
            beatMarkers[i]?.EnsureDefaults();
        for (int i = 0; i < tempoRegions.Count; i++)
            tempoRegions[i]?.EnsureDefaults();
        for (int i = 0; i < timeSignatures.Count; i++)
            timeSignatures[i]?.EnsureDefaults();
    }
}

[Serializable]
public sealed class ChartEditorBeatMarker
{
    public string id;
    public int index;
    public double beatPosition;
    public double audioTimeSeconds;
    public int barNumber;
    public int beatInBar;
    public bool isAnchor;
    public bool isDownbeat;
    public bool locked;
    public string label;
    public string linkedSectionId;
    public double bpm;
    public bool generatedBySynchTheory;
    public double synchTheoryConfidence;
    public string synchTheorySource;

    public void EnsureDefaults()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = Guid.NewGuid().ToString("N");
    }
}

[Serializable]
public sealed class ChartEditorTempoRegion
{
    public string id;
    public double startBeat;
    public double endBeat;
    public double startAudioTimeSeconds;
    public double endAudioTimeSeconds;
    public double bpm;

    public void EnsureDefaults()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = Guid.NewGuid().ToString("N");
        bpm = Math.Max(1.0, bpm);
    }
}

[Serializable]
public sealed class ChartEditorTimeSignatureChange
{
    public double beatPosition;
    public int numerator = 4;
    public int denominator = 4;

    public void EnsureDefaults()
    {
        if (numerator <= 0)
            numerator = 4;
        if (denominator <= 0)
            denominator = 4;
    }
}

[Serializable]
public sealed class ChartEditorSyncPoint
{
    public string id;
    public string name;
    public double chartTimeSeconds;
    public double audioTimeSeconds;
    public string linkedSectionId;
    public bool locked;

    public void EnsureDefaults()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(name))
            name = "Anchor";
    }
}

public static class ChartEditorGeneratedPlaybackIntegrity
{
    public static string ComputeNoteFingerprint(ChartEditorTrack track)
    {
        return ComputeNoteFingerprint(track?.notes);
    }

    public static string ComputeNoteFingerprint(IEnumerable<ChartEditorNote> notes)
    {
        StringBuilder builder = new StringBuilder();
        foreach (ChartEditorNote note in (notes ?? Enumerable.Empty<ChartEditorNote>())
                     .Where(note => note != null)
                     .OrderBy(note => note.sourceNoteId)
                     .ThenBy(note => note.timeSeconds)
                     .ThenBy(note => note.stringOrLane)
                     .ThenBy(note => note.fret))
        {
            builder.Append(note.sourceNoteId).Append('|')
                .Append(Format(note.timeSeconds)).Append('|')
                .Append(Format(note.durationSeconds)).Append('|')
                .Append(note.stringOrLane).Append('|')
                .Append(note.fret).Append('|')
                .Append(note.noteName ?? string.Empty).Append('|')
                .Append(note.chordId).Append('|')
                .Append(note.chordName ?? string.Empty).Append('|')
                .Append((int)note.technique).Append('|')
                .Append(note.slideTargetFret).Append('|')
                .Append(Format(note.bendStep)).Append('|')
                .Append(Format(note.bendVisualStartTime)).Append('|')
                .Append(Format(note.bendVisualDuration)).Append('|')
                .Append(note.bendPreBend).Append('|')
                .Append(note.bendRelease).Append('|')
                .Append(note.muted).Append('|')
                .Append(note.palmMute).Append('|')
                .Append(note.fretHandMute).Append('|')
                .Append(note.harmonic).Append('|')
                .Append(note.accent).Append('|')
                .Append(note.tap).Append('|')
                .Append(note.tremolo).Append('|')
                .Append(note.pinchHarmonic).Append('|')
                .Append(note.vibratoStrength).Append('|')
                .Append(note.maxBend.ToString("0.####", CultureInfo.InvariantCulture)).Append('|')
                .Append(note.legato).Append('|')
                .Append(note.requiresPluck).Append('|')
                .Append(note.linkedFromNoteId).Append('|')
                .Append(BendPointDigest(note.bendPoints)).Append('|')
                .Append(SegmentDigest(note.techniqueSegments))
                .AppendLine();
        }

        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
        StringBuilder hex = new StringBuilder(hash.Length * 2);
        for (int i = 0; i < hash.Length; i++)
            hex.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
        return hex.ToString();
    }

    public static bool CanReuseGeneratedPlayback(ChartEditorTrack track)
    {
        return track?.generatedNotes != null &&
               track.generatedNotes.Count > 0 &&
               !string.IsNullOrWhiteSpace(track.generatedPlaybackNoteFingerprint) &&
               string.Equals(
                   track.generatedPlaybackNoteFingerprint,
                   ComputeNoteFingerprint(track),
                   StringComparison.Ordinal);
    }

    private static string BendPointDigest(IEnumerable<ChartEditorBendPoint> points)
    {
        return string.Join(",", (points ?? Enumerable.Empty<ChartEditorBendPoint>())
            .Where(point => point != null)
            .OrderBy(point => point.timeSeconds)
            .Select(point => $"{Format(point.timeSeconds)}:{Format(point.step)}"));
    }

    private static string SegmentDigest(IEnumerable<ChartEditorTechniqueSegment> segments)
    {
        return string.Join(",", (segments ?? Enumerable.Empty<ChartEditorTechniqueSegment>())
            .Where(segment => segment != null)
            .OrderBy(segment => segment.startOffset)
            .ThenBy(segment => segment.endOffset)
            .ThenBy(segment => segment.type)
            .Select(segment => string.Join(":",
                (int)segment.type,
                Format(segment.startOffset),
                Format(segment.endOffset),
                segment.startFret,
                segment.endFret,
                Format(segment.startBend),
                Format(segment.endBend))));
    }

    private static string Format(double value)
    {
        return Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
    }
}

[Serializable]
public sealed class ChartEditorImportResult
{
    public ChartEditorProject project;
    public List<string> warnings = new List<string>();
}
