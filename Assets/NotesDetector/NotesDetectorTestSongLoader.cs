using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class NotesDetectorTestCatalogDefinition
{
    public string catalogTitle = "Detector Tests";
    public float leadInSeconds = 1.8f;
    public float gapBetweenTargetsSeconds = 0.75f;
    public float singleNoteDurationSeconds = 0.95f;
    public float chordDurationSeconds = 1.35f;
    public List<NotesDetectorTestCategoryDefinition> categories = new List<NotesDetectorTestCategoryDefinition>();
}

[Serializable]
public sealed class NotesDetectorTestCategoryDefinition
{
    public string id;
    public string title;
    public string description;
    public List<NotesDetectorTestDefinition> tests = new List<NotesDetectorTestDefinition>();
}

[Serializable]
public sealed class NotesDetectorTestDefinition
{
    public string id;
    public string label;
    public string displayName;
    public string description;
    public float leadInSeconds = -1f;
    public float gapBetweenTargetsSeconds = -1f;
    public float singleNoteDurationSeconds = -1f;
    public float chordDurationSeconds = -1f;
    public List<NotesDetectorTestTargetDefinition> targets = new List<NotesDetectorTestTargetDefinition>();
}

[Serializable]
public sealed class NotesDetectorTestTargetDefinition
{
    public string label;
    public string chordName;
    public float durationSeconds = -1f;
    public float gapAfterSeconds = -1f;
    public List<NotesDetectorTestNoteDefinition> notes = new List<NotesDetectorTestNoteDefinition>();
}

[Serializable]
public sealed class NotesDetectorTestNoteDefinition
{
    public int stringIndex;
    public int fret;
    public string technique;
    public int slideTargetFret = -1;
    public float bendStep;
    public bool preBend;
    public bool release;
    public bool muted;
    public bool legato;
    public bool requiresPluck = true;
    public float durationSeconds = -1f;
    public List<NotesDetectorTestTechniqueSegmentDefinition> segments = new List<NotesDetectorTestTechniqueSegmentDefinition>();
}

[Serializable]
public sealed class NotesDetectorTestTechniqueSegmentDefinition
{
    public string type;
    public float startOffset;
    public float endOffset;
    public int startFret = SegmentUnsetFret;
    public int endFret = SegmentUnsetFret;
    public float startBend;
    public float endBend;

    public const int SegmentUnsetFret = -999;
}

public sealed class LoadedNotesDetectorTestCatalogEntry
{
    public string Id;
    public string CategoryId;
    public string CategoryTitle;
    public string Label;
    public string Description;
    public string DisplayName;
}

public sealed class LoadedNotesDetectorTestCatalog
{
    public string Title;
    public List<LoadedNotesDetectorTestCatalogEntry> Tests = new List<LoadedNotesDetectorTestCatalogEntry>();
}

public sealed class LoadedNotesDetectorTestSong
{
    public string TestId;
    public string CategoryTitle;
    public string DisplayName;
    public string Description;
    public List<NoteData> Notes = new List<NoteData>();
}

public static class NotesDetectorTestSongLoader
{
    private static readonly string[] PitchNames =
    {
        "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
    };

    public static bool TryLoadCatalog(string filePath, out LoadedNotesDetectorTestCatalog catalog, out string error)
    {
        catalog = null;
        error = string.Empty;

        if (!TryReadCatalogDefinition(filePath, out NotesDetectorTestCatalogDefinition definition, out error))
            return false;

        catalog = BuildCatalog(definition);
        if (catalog.Tests == null || catalog.Tests.Count == 0)
        {
            error = "Detector test catalog did not contain any playable tests.";
            catalog = null;
            return false;
        }

        return true;
    }

    public static bool TryLoadSong(string filePath, string testId, out LoadedNotesDetectorTestSong song, out string error)
    {
        song = null;
        error = string.Empty;

        if (!TryReadCatalogDefinition(filePath, out NotesDetectorTestCatalogDefinition definition, out error))
            return false;

        if (!TryFindTestDefinition(definition, testId, out NotesDetectorTestCategoryDefinition category, out NotesDetectorTestDefinition testDefinition))
        {
            error = string.IsNullOrWhiteSpace(testId)
                ? "Detector test catalog did not contain any tests."
                : $"Detector test '{testId}' was not found in the catalog.";
            return false;
        }

        song = BuildSong(definition, category, testDefinition);
        if (song.Notes == null || song.Notes.Count == 0)
        {
            error = $"Detector test '{song.DisplayName}' did not contain any playable notes.";
            song = null;
            return false;
        }

        return true;
    }

    private static bool TryReadCatalogDefinition(string filePath, out NotesDetectorTestCatalogDefinition definition, out string error)
    {
        definition = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            error = "Detector test catalog path was empty.";
            return false;
        }

        if (!File.Exists(filePath))
        {
            error = $"Detector test catalog file was not found: {filePath}";
            return false;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            definition = JsonUtility.FromJson<NotesDetectorTestCatalogDefinition>(json);
            if (definition == null)
            {
                error = "Detector test catalog JSON could not be parsed.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Detector test catalog load failed: {ex.Message}";
            definition = null;
            return false;
        }
    }

    private static LoadedNotesDetectorTestCatalog BuildCatalog(NotesDetectorTestCatalogDefinition definition)
    {
        LoadedNotesDetectorTestCatalog catalog = new LoadedNotesDetectorTestCatalog
        {
            Title = string.IsNullOrWhiteSpace(definition.catalogTitle)
                ? "Detector Tests"
                : definition.catalogTitle.Trim(),
            Tests = new List<LoadedNotesDetectorTestCatalogEntry>()
        };

        List<NotesDetectorTestCategoryDefinition> categories = definition.categories ?? new List<NotesDetectorTestCategoryDefinition>();
        for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
        {
            NotesDetectorTestCategoryDefinition category = categories[categoryIndex];
            if (category == null)
                continue;

            string categoryId = ResolveIdentifier(category.id, category.title, $"category-{categoryIndex + 1}");
            string categoryTitle = string.IsNullOrWhiteSpace(category.title) ? "Other" : category.title.Trim();
            List<NotesDetectorTestDefinition> tests = category.tests ?? new List<NotesDetectorTestDefinition>();
            for (int testIndex = 0; testIndex < tests.Count; testIndex++)
            {
                NotesDetectorTestDefinition test = tests[testIndex];
                if (test == null || test.targets == null || test.targets.Count == 0)
                    continue;

                string testId = ResolveIdentifier(test.id, test.label, $"{categoryId}-test-{testIndex + 1}");
                string label = string.IsNullOrWhiteSpace(test.label) ? $"Test {catalog.Tests.Count + 1}" : test.label.Trim();
                string displayName = string.IsNullOrWhiteSpace(test.displayName) ? label : test.displayName.Trim();
                string description = string.IsNullOrWhiteSpace(test.description)
                    ? (string.IsNullOrWhiteSpace(category.description) ? string.Empty : category.description.Trim())
                    : test.description.Trim();

                catalog.Tests.Add(new LoadedNotesDetectorTestCatalogEntry
                {
                    Id = testId,
                    CategoryId = categoryId,
                    CategoryTitle = categoryTitle,
                    Label = label,
                    Description = description,
                    DisplayName = displayName
                });
            }
        }

        return catalog;
    }

    private static bool TryFindTestDefinition(
        NotesDetectorTestCatalogDefinition definition,
        string testId,
        out NotesDetectorTestCategoryDefinition resolvedCategory,
        out NotesDetectorTestDefinition resolvedTest)
    {
        resolvedCategory = null;
        resolvedTest = null;

        List<NotesDetectorTestCategoryDefinition> categories = definition.categories ?? new List<NotesDetectorTestCategoryDefinition>();
        string desiredId = string.IsNullOrWhiteSpace(testId) ? string.Empty : testId.Trim();

        for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
        {
            NotesDetectorTestCategoryDefinition category = categories[categoryIndex];
            if (category == null)
                continue;

            string categoryId = ResolveIdentifier(category.id, category.title, $"category-{categoryIndex + 1}");
            List<NotesDetectorTestDefinition> tests = category.tests ?? new List<NotesDetectorTestDefinition>();
            for (int testIndex = 0; testIndex < tests.Count; testIndex++)
            {
                NotesDetectorTestDefinition test = tests[testIndex];
                if (test == null || test.targets == null || test.targets.Count == 0)
                    continue;

                string resolvedId = ResolveIdentifier(test.id, test.label, $"{categoryId}-test-{testIndex + 1}");
                if (string.IsNullOrWhiteSpace(desiredId))
                {
                    resolvedCategory = category;
                    resolvedTest = test;
                    return true;
                }

                if (!string.Equals(resolvedId, desiredId, StringComparison.OrdinalIgnoreCase))
                    continue;

                resolvedCategory = category;
                resolvedTest = test;
                return true;
            }
        }

        return false;
    }

    private static LoadedNotesDetectorTestSong BuildSong(
        NotesDetectorTestCatalogDefinition catalogDefinition,
        NotesDetectorTestCategoryDefinition categoryDefinition,
        NotesDetectorTestDefinition testDefinition)
    {
        LoadedNotesDetectorTestSong song = new LoadedNotesDetectorTestSong
        {
            TestId = ResolveIdentifier(testDefinition.id, testDefinition.label, "detector-test"),
            CategoryTitle = string.IsNullOrWhiteSpace(categoryDefinition?.title) ? "Detector Test" : categoryDefinition.title.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(testDefinition.displayName)
                ? (string.IsNullOrWhiteSpace(testDefinition.label) ? "Detection Check" : testDefinition.label.Trim())
                : testDefinition.displayName.Trim(),
            Description = string.IsNullOrWhiteSpace(testDefinition.description) ? string.Empty : testDefinition.description.Trim(),
            Notes = new List<NoteData>()
        };

        float currentTime = Mathf.Max(0.25f, ResolvePositiveValue(testDefinition.leadInSeconds, catalogDefinition.leadInSeconds, 1.8f));
        float gapBetweenTargets = Mathf.Max(0.10f, ResolvePositiveValue(testDefinition.gapBetweenTargetsSeconds, catalogDefinition.gapBetweenTargetsSeconds, 0.75f));
        float singleDuration = Mathf.Max(0.18f, ResolvePositiveValue(testDefinition.singleNoteDurationSeconds, catalogDefinition.singleNoteDurationSeconds, 0.95f));
        float chordDuration = Mathf.Max(0.20f, ResolvePositiveValue(testDefinition.chordDurationSeconds, catalogDefinition.chordDurationSeconds, 1.35f));

        int nextNoteId = 0;
        int nextChordId = 0;
        Dictionary<int, int> previousNoteIdByString = new Dictionary<int, int>();

        List<NotesDetectorTestTargetDefinition> targets = testDefinition.targets ?? new List<NotesDetectorTestTargetDefinition>();
        for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
        {
            NotesDetectorTestTargetDefinition target = targets[targetIndex];
            if (target == null || target.notes == null || target.notes.Count == 0)
                continue;

            List<NotesDetectorTestNoteDefinition> orderedNotes = target.notes
                .Where(note => note != null)
                .Where(note => note.stringIndex >= 0 && note.stringIndex < StringTuningUtils.StandardGuitarTuning.Length)
                .Where(note => note.fret >= 0)
                .OrderBy(note => note.stringIndex)
                .ThenBy(note => note.fret)
                .ToList();

            if (orderedNotes.Count == 0)
                continue;

            float targetDuration = target.durationSeconds > 0f
                ? target.durationSeconds
                : (orderedNotes.Count > 1 ? chordDuration : singleDuration);
            int chordId = orderedNotes.Count > 1 ? nextChordId++ : -1;
            string chordName = string.IsNullOrWhiteSpace(target.chordName) ? null : target.chordName.Trim();

            for (int noteIndex = 0; noteIndex < orderedNotes.Count; noteIndex++)
            {
                NotesDetectorTestNoteDefinition sourceNote = orderedNotes[noteIndex];
                float noteDuration = sourceNote.durationSeconds > 0f ? sourceNote.durationSeconds : targetDuration;
                int midi = StringTuningUtils.StandardGuitarTuning[sourceNote.stringIndex] + sourceNote.fret;
                string noteName = PitchNames[Mod(midi, 12)];
                NoteTechnique technique = ParseTechnique(sourceNote.technique);
                bool inferredLegato = sourceNote.legato || technique == NoteTechnique.HammerOn || technique == NoteTechnique.PullOff;
                bool pluckRequired = sourceNote.requiresPluck;
                if (technique == NoteTechnique.HammerOn || technique == NoteTechnique.PullOff)
                    pluckRequired = false;

                int linkedFrom = -1;
                if ((!pluckRequired || inferredLegato || technique == NoteTechnique.Slide) &&
                    previousNoteIdByString.TryGetValue(sourceNote.stringIndex, out int previousNoteId))
                {
                    linkedFrom = previousNoteId;
                }

                List<NoteTechniqueSegmentData> techniqueSegments = BuildTechniqueSegments(sourceNote, technique, noteDuration);
                NoteData noteData = new NoteData(
                    nextNoteId,
                    currentTime,
                    noteDuration,
                    sourceNote.stringIndex,
                    sourceNote.fret,
                    noteName,
                    chordId,
                    technique,
                    sourceNote.slideTargetFret,
                    sourceNote.bendStep,
                    inferredLegato,
                    pluckRequired,
                    linkedFrom,
                    sourceNote.preBend,
                    sourceNote.release,
                    ResolveBendVisualStartTime(sourceNote, techniqueSegments),
                    ResolveBendVisualDuration(techniqueSegments),
                    techniqueSegments,
                    sourceNote.muted,
                    chordName);

                song.Notes.Add(noteData);
                previousNoteIdByString[sourceNote.stringIndex] = nextNoteId;
                nextNoteId++;
            }

            float gapAfterTarget = target.gapAfterSeconds > 0f ? target.gapAfterSeconds : gapBetweenTargets;
            currentTime += targetDuration + gapAfterTarget;
        }

        return song;
    }

    private static List<NoteTechniqueSegmentData> BuildTechniqueSegments(
        NotesDetectorTestNoteDefinition sourceNote,
        NoteTechnique technique,
        float noteDuration)
    {
        List<NoteTechniqueSegmentData> explicitSegments = BuildExplicitTechniqueSegments(sourceNote, noteDuration);
        if (explicitSegments != null && explicitSegments.Count > 0)
            return explicitSegments;

        List<NoteTechniqueSegmentData> fallbackSegments = new List<NoteTechniqueSegmentData>();
        if (technique == NoteTechnique.Slide && sourceNote.slideTargetFret >= 0)
        {
            float slideStart = Mathf.Clamp(noteDuration * 0.14f, 0f, noteDuration * 0.60f);
            float slideEnd = Mathf.Clamp(noteDuration * 0.82f, slideStart + 0.05f, noteDuration);
            fallbackSegments.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Slide,
                slideStart,
                slideEnd,
                sourceNote.fret,
                sourceNote.slideTargetFret,
                0f,
                0f));
        }

        if (technique == NoteTechnique.Bend || sourceNote.bendStep > 0.01f || sourceNote.preBend || sourceNote.release)
        {
            if (sourceNote.preBend && sourceNote.release)
            {
                fallbackSegments.Add(new NoteTechniqueSegmentData(
                    NoteTechniqueSegmentType.Bend,
                    0f,
                    Mathf.Clamp(noteDuration * 0.85f, 0.12f, noteDuration),
                    sourceNote.fret,
                    sourceNote.fret,
                    sourceNote.bendStep,
                    0f));
            }
            else if (sourceNote.preBend)
            {
                fallbackSegments.Add(new NoteTechniqueSegmentData(
                    NoteTechniqueSegmentType.Bend,
                    0f,
                    Mathf.Clamp(noteDuration * 0.85f, 0.12f, noteDuration),
                    sourceNote.fret,
                    sourceNote.fret,
                    sourceNote.bendStep,
                    sourceNote.bendStep));
            }
            else if (sourceNote.release)
            {
                float bendUpEnd = Mathf.Clamp(noteDuration * 0.42f, 0.10f, noteDuration * 0.65f);
                float bendReleaseEnd = Mathf.Clamp(noteDuration * 0.88f, bendUpEnd + 0.10f, noteDuration);
                fallbackSegments.Add(new NoteTechniqueSegmentData(
                    NoteTechniqueSegmentType.Bend,
                    0f,
                    bendUpEnd,
                    sourceNote.fret,
                    sourceNote.fret,
                    0f,
                    sourceNote.bendStep));
                fallbackSegments.Add(new NoteTechniqueSegmentData(
                    NoteTechniqueSegmentType.Bend,
                    bendUpEnd,
                    bendReleaseEnd,
                    sourceNote.fret,
                    sourceNote.fret,
                    sourceNote.bendStep,
                    0f));
            }
            else
            {
                fallbackSegments.Add(new NoteTechniqueSegmentData(
                    NoteTechniqueSegmentType.Bend,
                    0f,
                    Mathf.Clamp(noteDuration * 0.80f, 0.10f, noteDuration),
                    sourceNote.fret,
                    sourceNote.fret,
                    0f,
                    sourceNote.bendStep));
            }
        }

        if (technique == NoteTechnique.Vibrato)
        {
            fallbackSegments.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Vibrato,
                0f,
                Mathf.Max(0.08f, noteDuration),
                sourceNote.fret,
                sourceNote.fret,
                0f,
                0f));
        }

        return fallbackSegments.Count > 0 ? fallbackSegments : null;
    }

    private static List<NoteTechniqueSegmentData> BuildExplicitTechniqueSegments(
        NotesDetectorTestNoteDefinition sourceNote,
        float noteDuration)
    {
        if (sourceNote == null || sourceNote.segments == null || sourceNote.segments.Count == 0)
            return null;

        List<NoteTechniqueSegmentData> segments = new List<NoteTechniqueSegmentData>();
        for (int segmentIndex = 0; segmentIndex < sourceNote.segments.Count; segmentIndex++)
        {
            NotesDetectorTestTechniqueSegmentDefinition sourceSegment = sourceNote.segments[segmentIndex];
            if (sourceSegment == null)
                continue;

            NoteTechniqueSegmentType segmentType = ParseTechniqueSegmentType(sourceSegment.type);
            float startOffset = Mathf.Clamp(sourceSegment.startOffset, 0f, noteDuration);
            float endOffset = Mathf.Clamp(sourceSegment.endOffset, startOffset, noteDuration);
            if (endOffset <= startOffset + 0.0001f)
                continue;

            int startFret = sourceSegment.startFret != NotesDetectorTestTechniqueSegmentDefinition.SegmentUnsetFret
                ? Mathf.Max(0, sourceSegment.startFret)
                : Mathf.Max(0, sourceNote.fret);
            int endFret = sourceSegment.endFret != NotesDetectorTestTechniqueSegmentDefinition.SegmentUnsetFret
                ? Mathf.Max(0, sourceSegment.endFret)
                : (sourceNote.slideTargetFret >= 0 ? sourceNote.slideTargetFret : Mathf.Max(0, sourceNote.fret));

            segments.Add(new NoteTechniqueSegmentData(
                segmentType,
                startOffset,
                endOffset,
                startFret,
                endFret,
                sourceSegment.startBend,
                sourceSegment.endBend));
        }

        return segments.Count > 0 ? segments : null;
    }

    private static float ResolveBendVisualStartTime(
        NotesDetectorTestNoteDefinition sourceNote,
        List<NoteTechniqueSegmentData> techniqueSegments)
    {
        if (sourceNote == null)
            return -1f;

        if (techniqueSegments == null || techniqueSegments.Count == 0)
            return -1f;

        for (int i = 0; i < techniqueSegments.Count; i++)
        {
            NoteTechniqueSegmentData segment = techniqueSegments[i];
            if (segment.type == NoteTechniqueSegmentType.Bend)
                return segment.startOffset;
        }

        return -1f;
    }

    private static float ResolveBendVisualDuration(List<NoteTechniqueSegmentData> techniqueSegments)
    {
        if (techniqueSegments == null || techniqueSegments.Count == 0)
            return 0f;

        float start = -1f;
        float end = -1f;
        for (int i = 0; i < techniqueSegments.Count; i++)
        {
            NoteTechniqueSegmentData segment = techniqueSegments[i];
            if (segment.type != NoteTechniqueSegmentType.Bend)
                continue;

            if (start < 0f || segment.startOffset < start)
                start = segment.startOffset;
            if (segment.endOffset > end)
                end = segment.endOffset;
        }

        return start >= 0f && end > start ? end - start : 0f;
    }

    private static string ResolveIdentifier(string explicitId, string fallbackLabel, string generatedFallback)
    {
        string candidate = !string.IsNullOrWhiteSpace(explicitId)
            ? explicitId.Trim()
            : (!string.IsNullOrWhiteSpace(fallbackLabel) ? fallbackLabel.Trim() : generatedFallback);

        candidate = candidate.ToLowerInvariant();
        char[] buffer = candidate.ToCharArray();
        for (int i = 0; i < buffer.Length; i++)
        {
            char c = buffer[i];
            buffer[i] = char.IsLetterOrDigit(c) ? c : '-';
        }

        string normalized = new string(buffer);
        while (normalized.Contains("--"))
            normalized = normalized.Replace("--", "-");

        normalized = normalized.Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? generatedFallback : normalized;
    }

    private static float ResolvePositiveValue(float primary, float secondary, float fallback)
    {
        if (primary > 0f)
            return primary;
        if (secondary > 0f)
            return secondary;
        return fallback;
    }

    private static NoteTechnique ParseTechnique(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return NoteTechnique.None;

        switch (rawValue.Trim().ToLowerInvariant())
        {
            case "hammeron":
            case "hammer-on":
            case "ho":
                return NoteTechnique.HammerOn;
            case "pulloff":
            case "pull-off":
            case "po":
                return NoteTechnique.PullOff;
            case "slide":
                return NoteTechnique.Slide;
            case "bend":
                return NoteTechnique.Bend;
            case "vibrato":
                return NoteTechnique.Vibrato;
            default:
                return NoteTechnique.None;
        }
    }

    private static NoteTechniqueSegmentType ParseTechniqueSegmentType(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return NoteTechniqueSegmentType.Sustain;

        switch (rawValue.Trim().ToLowerInvariant())
        {
            case "slide":
                return NoteTechniqueSegmentType.Slide;
            case "bend":
                return NoteTechniqueSegmentType.Bend;
            case "vibrato":
                return NoteTechniqueSegmentType.Vibrato;
            case "sustain":
            default:
                return NoteTechniqueSegmentType.Sustain;
        }
    }

    private static int Mod(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
