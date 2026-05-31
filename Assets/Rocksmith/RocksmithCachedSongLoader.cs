using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

public static class RocksmithCachedSongLoader
{
    private const int MinimumSupportedSchemaVersion = 10;
    private static readonly Dictionary<string, CachedManifestEntry> manifestCache = new Dictionary<string, CachedManifestEntry>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, CachedPartEntry> partCache = new Dictionary<string, CachedPartEntry>(StringComparer.OrdinalIgnoreCase);

    private sealed class CachedManifestEntry
    {
        public long ticks;
        public RocksmithCachedSongManifest manifest;
    }

    private sealed class CachedPartEntry
    {
        public long ticks;
        public RocksmithCachedArrangementPart part;
    }

    public static bool IsRocksmithManifestPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        string fileName = Path.GetFileName(filePath);
        return string.Equals(fileName, RocksmithCachedSongFormat.ManifestFileName, StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".rs2song.json", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryLoadManifest(string manifestPath, out RocksmithCachedSongManifest manifest)
    {
        manifest = null;
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
            return false;

        long ticks = File.GetLastWriteTimeUtc(manifestPath).Ticks;
        if (manifestCache.TryGetValue(manifestPath, out CachedManifestEntry cached) &&
            cached != null &&
            cached.ticks == ticks &&
            cached.manifest != null)
        {
            manifest = cached.manifest;
            return true;
        }

        try
        {
            string json = File.ReadAllText(manifestPath);
            RocksmithCachedSongManifest loaded = JsonUtility.FromJson<RocksmithCachedSongManifest>(json);
            if (loaded == null || !IsSupportedSchemaVersion(loaded.schemaVersion))
                return false;

            NormalizeManifest(manifestPath, loaded);
            manifestCache[manifestPath] = new CachedManifestEntry
            {
                ticks = ticks,
                manifest = loaded
            };
            manifest = loaded;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ImportedSongCache] Failed to parse manifest '{manifestPath}': {ex.Message}");
            return false;
        }
    }

    public static List<MusicXmlLoader.MusicXmlPartSummary> GetPartSummaries(string manifestPath)
    {
        if (!TryLoadManifest(manifestPath, out RocksmithCachedSongManifest manifest))
            return new List<MusicXmlLoader.MusicXmlPartSummary>();

        List<MusicXmlLoader.MusicXmlPartSummary> summaries = new List<MusicXmlLoader.MusicXmlPartSummary>();
        if (manifest.arrangements == null)
            return summaries;

        for (int i = 0; i < manifest.arrangements.Count; i++)
        {
            RocksmithCachedArrangementSummary arrangement = manifest.arrangements[i];
            if (arrangement == null || string.IsNullOrWhiteSpace(arrangement.partFilePath))
                continue;

            summaries.Add(new MusicXmlLoader.MusicXmlPartSummary
            {
                Index = i,
                PartId = arrangement.partId ?? string.Empty,
                Name = string.IsNullOrWhiteSpace(arrangement.displayName) ? arrangement.route ?? $"Arrangement {i + 1}" : arrangement.displayName,
                GroupId = arrangement.arrangementGroupId ?? arrangement.partId ?? string.Empty,
                GroupDisplayName = string.IsNullOrWhiteSpace(arrangement.arrangementDisplayName)
                    ? (string.IsNullOrWhiteSpace(arrangement.route) ? arrangement.displayName : arrangement.route)
                    : arrangement.arrangementDisplayName,
                DifficultyLabel = arrangement.difficultyLabel ?? string.Empty,
                DifficultyUiIndex = arrangement.difficultyUiIndex,
                HasDifficultyVariants = arrangement.hasDifficultyVariants,
                NoteCount = Mathf.Max(0, arrangement.noteCount),
                TabCount = Mathf.Max(0, arrangement.tabCount),
                Score = arrangement.score,
                StringTuningPitches = arrangement.tuningPitches != null ? (int[])arrangement.tuningPitches.Clone() : null,
                TuningDisplayName = string.IsNullOrWhiteSpace(arrangement.tuningDisplayName)
                    ? StringTuningUtils.FormatTuningDisplayName(arrangement.tuningPitches)
                    : arrangement.tuningDisplayName
            });
        }

        return summaries;
    }

    public static List<NoteData> LoadSong(string manifestPath, int targetPartIndex = -1)
    {
        if (!TryLoadManifest(manifestPath, out RocksmithCachedSongManifest manifest))
            return new List<NoteData>();

        int chosenIndex = ResolveArrangementIndex(manifest, targetPartIndex);
        if (chosenIndex < 0 || manifest.arrangements == null || chosenIndex >= manifest.arrangements.Count)
            return new List<NoteData>();

        RocksmithCachedArrangementSummary summary = manifest.arrangements[chosenIndex];
        if (!TryLoadPart(summary?.partFilePath, out RocksmithCachedArrangementPart part))
            return new List<NoteData>();

        List<NoteData> notes = new List<NoteData>(part.notes?.Count ?? 0);
        if (part.notes == null)
            return notes;

        for (int i = 0; i < part.notes.Count; i++)
        {
            RocksmithCachedNoteData source = part.notes[i];
            List<NoteTechniqueSegmentData> segments = null;
            if (source.techniqueSegments != null && source.techniqueSegments.Count > 0)
            {
                segments = new List<NoteTechniqueSegmentData>(source.techniqueSegments.Count);
                for (int segmentIndex = 0; segmentIndex < source.techniqueSegments.Count; segmentIndex++)
                {
                    RocksmithCachedTechniqueSegmentData segment = source.techniqueSegments[segmentIndex];
                    segments.Add(new NoteTechniqueSegmentData(
                        (NoteTechniqueSegmentType)Mathf.Clamp(segment.type, 0, (int)NoteTechniqueSegmentType.Vibrato),
                        segment.startOffset,
                        segment.endOffset,
                        segment.startFret,
                        segment.endFret,
                        segment.startBend,
                        segment.endBend));
                }
            }

            segments = NormalizeRocksmithTechniqueSegments(source, segments);

            notes.Add(new NoteData(
                source.id,
                source.time,
                source.duration,
                source.stringIdx,
                source.fret,
                source.note,
                source.chordId,
                (NoteTechnique)Mathf.Clamp(source.technique, 0, (int)NoteTechnique.Vibrato),
                source.slideTargetFret,
                source.bendStep,
                source.isLegato,
                source.requiresPluck,
                source.linkedFromNoteId,
                source.bendPreBend,
                source.bendRelease,
                source.bendVisualStartTime,
                source.bendVisualDuration,
                segments,
                source.isMuted,
                source.chordName));
        }

        NormalizeRocksmithLegatoTransitions(notes);
        return notes;
    }

    public static bool TryLoadArrangementPart(
        string manifestPath,
        int targetPartIndex,
        out RocksmithCachedArrangementSummary summary,
        out RocksmithCachedArrangementPart part)
    {
        summary = null;
        part = null;

        if (!TryLoadManifest(manifestPath, out RocksmithCachedSongManifest manifest))
            return false;

        int chosenIndex = ResolveArrangementIndex(manifest, targetPartIndex);
        if (chosenIndex < 0 || manifest.arrangements == null || chosenIndex >= manifest.arrangements.Count)
            return false;

        summary = manifest.arrangements[chosenIndex];
        return TryLoadPart(summary?.partFilePath, out part);
    }

    public static List<ArpeggioGuideData> LoadArpeggioGuides(string manifestPath, int targetPartIndex = -1)
    {
        if (!TryLoadManifest(manifestPath, out RocksmithCachedSongManifest manifest))
            return new List<ArpeggioGuideData>();

        int chosenIndex = ResolveArrangementIndex(manifest, targetPartIndex);
        if (chosenIndex < 0 || manifest.arrangements == null || chosenIndex >= manifest.arrangements.Count)
            return new List<ArpeggioGuideData>();

        RocksmithCachedArrangementSummary summary = manifest.arrangements[chosenIndex];
        if (!TryLoadPart(summary?.partFilePath, out RocksmithCachedArrangementPart part) || part.arpeggioGuides == null)
            return new List<ArpeggioGuideData>();

        List<ArpeggioGuideData> guides = new List<ArpeggioGuideData>(part.arpeggioGuides.Count);
        for (int i = 0; i < part.arpeggioGuides.Count; i++)
        {
            RocksmithCachedArpeggioGuideData source = part.arpeggioGuides[i];
            if (source == null || source.stringFrets == null || source.stringFrets.Length == 0)
                continue;

            int[] clonedFrets = new int[source.stringFrets.Length];
            Array.Copy(source.stringFrets, clonedFrets, source.stringFrets.Length);
            guides.Add(new ArpeggioGuideData
            {
                id = source.id,
                startTime = source.startTime,
                endTime = source.endTime,
                chordName = source.chordName,
                stringFrets = clonedFrets
            });
        }

        return guides;
    }

    private static List<NoteTechniqueSegmentData> NormalizeRocksmithTechniqueSegments(
        RocksmithCachedNoteData source,
        List<NoteTechniqueSegmentData> segments)
    {
        bool wantsBendVisual =
            source != null &&
            ((NoteTechnique)Mathf.Clamp(source.technique, 0, (int)NoteTechnique.Vibrato) == NoteTechnique.Bend ||
             source.bendStep > 0.01f ||
             source.bendPreBend ||
             source.bendRelease);
        if (!wantsBendVisual)
            return RemoveFlatSustainUnderExpressiveSegments(segments);

        if (source.bendPoints != null && source.bendPoints.Count > 0)
            return RemoveFlatSustainUnderExpressiveSegments(BuildBendTechniqueSegmentsFromPoints(source, segments));

        if (HasRenderableBendTechniqueSegments(segments))
            return RemoveFlatSustainUnderExpressiveSegments(segments);

        return RemoveFlatSustainUnderExpressiveSegments(BuildFallbackBendTechniqueSegments(source, segments));
    }

    private static bool HasRenderableBendTechniqueSegments(List<NoteTechniqueSegmentData> segments)
    {
        if (segments == null || segments.Count == 0)
            return false;

        for (int i = 0; i < segments.Count; i++)
        {
            NoteTechniqueSegmentData segment = segments[i];
            if (segment.endOffset <= segment.startOffset + 0.0001f)
                continue;

            if (segment.type == NoteTechniqueSegmentType.Bend)
            {
                if (Mathf.Abs(segment.endBend - segment.startBend) > 0.01f)
                    return true;
            }
            else if ((segment.type == NoteTechniqueSegmentType.Sustain || segment.type == NoteTechniqueSegmentType.Vibrato) &&
                     (Mathf.Abs(segment.startBend) > 0.01f || Mathf.Abs(segment.endBend) > 0.01f))
            {
                return true;
            }
        }

        return false;
    }

    private static List<NoteTechniqueSegmentData> BuildFallbackBendTechniqueSegments(
        RocksmithCachedNoteData source,
        List<NoteTechniqueSegmentData> existingSegments)
    {
        List<NoteTechniqueSegmentData> result = new List<NoteTechniqueSegmentData>();
        if (existingSegments != null)
        {
            for (int i = 0; i < existingSegments.Count; i++)
            {
                NoteTechniqueSegmentData segment = existingSegments[i];
                if (segment.type == NoteTechniqueSegmentType.Slide && segment.endOffset > segment.startOffset + 0.0001f)
                    result.Add(segment);
            }
        }

        float duration = Mathf.Max(source.duration, source.bendVisualDuration, 0.12f);
        float bend = Mathf.Max(0.5f, source.bendStep);
        int fret = source.fret;

        if (source.bendRelease)
        {
            if (source.bendPreBend)
            {
                result.Add(new NoteTechniqueSegmentData(
                    NoteTechniqueSegmentType.Bend,
                    0f,
                    duration,
                    fret,
                    fret,
                    bend,
                    0f));
                return result;
            }

            float bendUpEnd = Mathf.Clamp(duration * 0.45f, 0.08f, Mathf.Max(0.08f, duration - 0.08f));
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Bend,
                0f,
                bendUpEnd,
                fret,
                fret,
                0f,
                bend));
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Bend,
                bendUpEnd,
                duration,
                fret,
                fret,
                bend,
                0f));
            return result;
        }

        if (source.bendPreBend)
        {
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Sustain,
                0f,
                duration,
                fret,
                fret,
                bend,
                bend));
            return result;
        }

        float riseEnd = Mathf.Clamp(duration * 0.24f, 0.08f, duration);
        if (duration > riseEnd + 0.04f)
        {
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Bend,
                0f,
                riseEnd,
                fret,
                fret,
                0f,
                bend));
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Sustain,
                riseEnd,
                duration,
                fret,
                fret,
                bend,
                bend));
        }
        else
        {
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Bend,
                0f,
                duration,
                fret,
                fret,
                0f,
                bend));
        }

        return result;
    }

    private static List<NoteTechniqueSegmentData> BuildBendTechniqueSegmentsFromPoints(
        RocksmithCachedNoteData source,
        List<NoteTechniqueSegmentData> existingSegments)
    {
        List<NoteTechniqueSegmentData> result = new List<NoteTechniqueSegmentData>();
        if (existingSegments != null)
        {
            for (int i = 0; i < existingSegments.Count; i++)
            {
                NoteTechniqueSegmentData segment = existingSegments[i];
                if (segment.endOffset <= segment.startOffset + 0.0001f)
                    continue;

                if (segment.type == NoteTechniqueSegmentType.Bend)
                    continue;

                if ((segment.type == NoteTechniqueSegmentType.Sustain || segment.type == NoteTechniqueSegmentType.Vibrato) &&
                    (Mathf.Abs(segment.startBend) > 0.01f || Mathf.Abs(segment.endBend) > 0.01f))
                {
                    continue;
                }

                result.Add(segment);
            }
        }

        List<RocksmithCachedBendPointData> points = source.bendPoints
            .Where(point => point != null)
            .OrderBy(point => point.timeSeconds)
            .ToList();
        if (points.Count == 0)
            return BuildFallbackBendTechniqueSegments(source, result);

        float duration = Mathf.Max(source.duration, source.bendVisualDuration, 0.12f);
        int fret = source.fret;
        bool startsWithPreBend = source.bendPreBend ||
                                 (points[0].timeSeconds <= 0.001f && Mathf.Abs(points[0].step) > 0.01f);

        RocksmithCachedBendPointData firstPoint = points[0];
        float firstPointTime = Mathf.Clamp(firstPoint.timeSeconds, 0f, duration);
        if (firstPointTime > 0.0001f && Mathf.Abs(firstPoint.step) > 0.01f)
        {
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Bend,
                0f,
                firstPointTime,
                fret,
                fret,
                startsWithPreBend ? firstPoint.step : 0f,
                firstPoint.step));
        }

        for (int i = 1; i < points.Count; i++)
        {
            RocksmithCachedBendPointData previous = points[i - 1];
            RocksmithCachedBendPointData current = points[i];
            float startOffset = Mathf.Clamp(previous.timeSeconds, 0f, duration);
            float endOffset = Mathf.Clamp(current.timeSeconds, 0f, duration);
            if (endOffset <= startOffset + 0.0001f)
                continue;

            NoteTechniqueSegmentType segmentType =
                Mathf.Abs(current.step - previous.step) <= 0.01f
                    ? NoteTechniqueSegmentType.Sustain
                    : NoteTechniqueSegmentType.Bend;

            result.Add(new NoteTechniqueSegmentData(
                segmentType,
                startOffset,
                endOffset,
                fret,
                fret,
                previous.step,
                current.step));
        }

        RocksmithCachedBendPointData lastPoint = points[points.Count - 1];
        float lastPointTime = Mathf.Clamp(lastPoint.timeSeconds, 0f, duration);
        if (duration > lastPointTime + 0.0001f && Mathf.Abs(lastPoint.step) > 0.01f)
        {
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Sustain,
                lastPointTime,
                duration,
                fret,
                fret,
                lastPoint.step,
                lastPoint.step));
        }

        return result;
    }

    private static List<NoteTechniqueSegmentData> RemoveFlatSustainUnderExpressiveSegments(List<NoteTechniqueSegmentData> segments)
    {
        if (segments == null || segments.Count == 0)
            return segments;

        List<(float start, float end)> expressiveSpans = null;
        for (int i = 0; i < segments.Count; i++)
        {
            NoteTechniqueSegmentData segment = segments[i];
            if (segment.endOffset <= segment.startOffset + 0.0001f)
                continue;

            bool isExpressive = false;
            if (segment.type == NoteTechniqueSegmentType.Vibrato)
            {
                isExpressive = true;
            }
            else if (segment.type == NoteTechniqueSegmentType.Slide)
            {
                isExpressive = segment.startFret != segment.endFret;
            }
            else if (segment.type == NoteTechniqueSegmentType.Bend)
            {
                isExpressive =
                    Mathf.Abs(segment.endBend - segment.startBend) > 0.01f ||
                    Mathf.Abs(segment.startBend) > 0.01f ||
                    Mathf.Abs(segment.endBend) > 0.01f;
            }
            else if (segment.type == NoteTechniqueSegmentType.Sustain)
            {
                isExpressive =
                    Mathf.Abs(segment.startBend) > 0.01f ||
                    Mathf.Abs(segment.endBend) > 0.01f;
            }

            if (isExpressive)
            {
                expressiveSpans ??= new List<(float start, float end)>();
                expressiveSpans.Add((segment.startOffset, segment.endOffset));
            }
        }

        if (expressiveSpans == null || expressiveSpans.Count == 0)
            return segments;

        expressiveSpans.Sort((left, right) => left.start.CompareTo(right.start));
        List<(float start, float end)> mergedExpressiveSpans = new List<(float start, float end)>(expressiveSpans.Count);
        for (int i = 0; i < expressiveSpans.Count; i++)
        {
            (float start, float end) span = expressiveSpans[i];
            if (mergedExpressiveSpans.Count == 0)
            {
                mergedExpressiveSpans.Add(span);
                continue;
            }

            (float currentStart, float currentEnd) = mergedExpressiveSpans[mergedExpressiveSpans.Count - 1];
            if (span.start <= currentEnd + 0.0001f)
            {
                mergedExpressiveSpans[mergedExpressiveSpans.Count - 1] = (currentStart, Mathf.Max(currentEnd, span.end));
            }
            else
            {
                mergedExpressiveSpans.Add(span);
            }
        }

        List<NoteTechniqueSegmentData> filtered = new List<NoteTechniqueSegmentData>(segments.Count + mergedExpressiveSpans.Count);
        for (int i = 0; i < segments.Count; i++)
        {
            NoteTechniqueSegmentData segment = segments[i];
            bool isFlatSustain =
                segment.type == NoteTechniqueSegmentType.Sustain &&
                Mathf.Abs(segment.startBend) <= 0.01f &&
                Mathf.Abs(segment.endBend) <= 0.01f &&
                segment.endOffset > segment.startOffset + 0.0001f;

            if (!isFlatSustain)
            {
                filtered.Add(segment);
                continue;
            }

            float cursor = segment.startOffset;
            bool emittedSplit = false;
            for (int spanIndex = 0; spanIndex < mergedExpressiveSpans.Count; spanIndex++)
            {
                (float spanStart, float spanEnd) = mergedExpressiveSpans[spanIndex];
                if (spanEnd <= cursor + 0.0001f)
                    continue;
                if (spanStart >= segment.endOffset - 0.0001f)
                    break;

                if (spanStart > cursor + 0.0001f)
                {
                    filtered.Add(new NoteTechniqueSegmentData(
                        NoteTechniqueSegmentType.Sustain,
                        cursor,
                        Mathf.Min(spanStart, segment.endOffset),
                        segment.startFret,
                        segment.endFret,
                        0f,
                        0f));
                    emittedSplit = true;
                }

                cursor = Mathf.Max(cursor, spanEnd);
                if (cursor >= segment.endOffset - 0.0001f)
                {
                    emittedSplit = true;
                    break;
                }
            }

            if (cursor < segment.endOffset - 0.0001f)
            {
                filtered.Add(new NoteTechniqueSegmentData(
                    NoteTechniqueSegmentType.Sustain,
                    cursor,
                    segment.endOffset,
                    segment.startFret,
                    segment.endFret,
                    0f,
                    0f));
                emittedSplit = true;
            }

            if (!emittedSplit)
            {
                filtered.Add(segment);
            }
        }

        return filtered;
    }

    private static void NormalizeRocksmithLegatoTransitions(List<NoteData> notes)
    {
        if (notes == null || notes.Count == 0)
            return;

        Dictionary<int, int> noteIndexById = new Dictionary<int, int>(notes.Count);
        for (int i = 0; i < notes.Count; i++)
            noteIndexById[notes[i].id] = i;

        for (int i = 0; i < notes.Count; i++)
        {
            NoteData destination = notes[i];
            if (!destination.isLegato || destination.linkedFromNoteId < 0)
                continue;

            if (!noteIndexById.TryGetValue(destination.linkedFromNoteId, out int originIndex))
                continue;

            NoteData origin = notes[originIndex];
            if (origin.stringIdx != destination.stringIdx)
                continue;

            float transitionDuration = Mathf.Max(0.05f, destination.time - origin.time);

            if (destination.technique == NoteTechnique.Slide)
            {
                if (origin.fret != destination.fret)
                {
                    NoteTechnique inferredLegato = destination.fret >= origin.fret
                        ? NoteTechnique.HammerOn
                        : NoteTechnique.PullOff;
                    if (origin.technique == NoteTechnique.None)
                        origin.technique = inferredLegato;
                    if (origin.slideTargetFret < 0)
                        origin.slideTargetFret = destination.fret;
                    origin.duration = Mathf.Max(origin.duration, transitionDuration);
                    notes[originIndex] = origin;
                }
                else
                {
                    destination.isLegato = false;
                    destination.requiresPluck = true;
                    destination.linkedFromNoteId = -1;
                    notes[i] = destination;
                }

                continue;
            }

            NoteTechnique inferredTechnique = destination.technique;
            if (inferredTechnique == NoteTechnique.None && origin.fret != destination.fret)
            {
                inferredTechnique = destination.fret >= origin.fret
                    ? NoteTechnique.HammerOn
                    : NoteTechnique.PullOff;
            }

            if (inferredTechnique == NoteTechnique.HammerOn || inferredTechnique == NoteTechnique.PullOff)
            {
                if (origin.technique == NoteTechnique.None)
                    origin.technique = inferredTechnique;
                if (origin.slideTargetFret < 0)
                    origin.slideTargetFret = destination.fret;
                origin.duration = Mathf.Max(origin.duration, transitionDuration);
                destination.technique = NoteTechnique.None;
                notes[originIndex] = origin;
                notes[i] = destination;
            }
        }
    }

    public static GeneratedPlaybackArrangement LoadGeneratedArrangement(string manifestPath)
    {
        if (!TryLoadManifest(manifestPath, out RocksmithCachedSongManifest manifest))
            return null;

        GeneratedPlaybackArrangement arrangement = new GeneratedPlaybackArrangement
        {
            sourcePath = manifestPath,
            durationSeconds = Mathf.Max(0f, manifest.durationSeconds)
        };

        if (manifest.arrangements == null)
            return arrangement;

        List<RocksmithCachedArrangementSummary> generatedSummaries = SelectGeneratedArrangementSummaries(manifest.arrangements);
        int nextChannel = 0;
        for (int i = 0; i < generatedSummaries.Count; i++)
        {
            RocksmithCachedArrangementSummary summary = generatedSummaries[i];
            if (!TryLoadPart(summary?.partFilePath, out RocksmithCachedArrangementPart part))
                continue;

            if (part.generatedPart != null)
            {
                arrangement.parts.Add(new GeneratedPlaybackPartInfo
                {
                    partId = part.generatedPart.partId ?? part.partId ?? $"rs_part_{i}",
                    displayName = part.generatedPart.displayName ?? part.displayName,
                    instrumentName = part.generatedPart.instrumentName ?? part.route,
                    sourceMidiChannel = part.generatedPart.sourceMidiChannel,
                    sourceMidiProgram = part.generatedPart.sourceMidiProgram,
                    preferredBank = part.generatedPart.preferredBank,
                    isDrum = part.generatedPart.isDrum,
                    isGuitarFamily = part.generatedPart.isGuitarFamily,
                    isExplicitHarmonicPart = part.generatedPart.isExplicitHarmonicPart
                });
            }

            arrangement.channelAssignments.Add(new GeneratedPlaybackChannelAssignment
            {
                channel = Mathf.Clamp(nextChannel, 0, 15),
                bank = -1,
                preset = part.generatedPart != null ? part.generatedPart.sourceMidiProgram : 29,
                isDrum = false,
                label = part.displayName,
                sourcePartId = part.partId,
                sourcePartName = part.displayName,
                pitchBendRangeSemitones = GetMaxPitchBendRange(part.generatedNotes)
            });

            if (part.generatedNotes != null)
            {
                for (int noteIndex = 0; noteIndex < part.generatedNotes.Count; noteIndex++)
                {
                    RocksmithCachedGeneratedNoteEvent source = part.generatedNotes[noteIndex];
                    GeneratedPlaybackNoteEvent generatedNote = new GeneratedPlaybackNoteEvent
                    {
                        startTimeSeconds = source.startTimeSeconds,
                        durationSeconds = source.durationSeconds,
                        pitchPreRollSeconds = source.pitchPreRollSeconds,
                        midiNote = source.midiNote,
                        velocity = source.velocity,
                        channel = Mathf.Clamp(nextChannel, 0, 15),
                        partId = source.partId ?? part.partId,
                        partName = source.partName ?? part.displayName,
                        techniqueVariant = (GeneratedTechniqueVariant)Mathf.Clamp(source.techniqueVariant, 0, (int)GeneratedTechniqueVariant.Harmonic),
                        legatoTransitionKind = (GeneratedLegatoTransitionKind)Mathf.Clamp(source.legatoTransitionKind, 0, (int)GeneratedLegatoTransitionKind.PullOff),
                        attackVelocityScale = source.attackVelocityScale,
                        vibratoDepthSemitones = source.vibratoDepthSemitones,
                        vibratoRateHz = source.vibratoRateHz,
                        vibratoDelayNormalized = source.vibratoDelayNormalized,
                        vibratoFadeNormalized = source.vibratoFadeNormalized,
                        pitchBendRangeSemitones = source.pitchBendRangeSemitones
                    };

                    if (source.pitchCurve != null)
                    {
                        for (int curveIndex = 0; curveIndex < source.pitchCurve.Count; curveIndex++)
                        {
                            RocksmithCachedGeneratedPitchPoint point = source.pitchCurve[curveIndex];
                            generatedNote.pitchCurve.Add(new GeneratedPlaybackPitchPoint
                            {
                                normalizedTime = point.normalizedTime,
                                semitoneOffset = point.semitoneOffset
                            });
                        }
                    }

                    arrangement.notes.Add(generatedNote);
                }
            }

            nextChannel++;
            if (nextChannel == 9)
                nextChannel++;
        }

        if (arrangement.durationSeconds <= 0.01f && arrangement.notes.Count > 0)
            arrangement.durationSeconds = arrangement.notes.Max(note => note.EndTimeSeconds);

        return arrangement;
    }

    public static string TryReadDisplayName(string manifestPath)
    {
        return TryLoadManifest(manifestPath, out RocksmithCachedSongManifest manifest)
            ? manifest.displayName
            : null;
    }

    public static string TryReadArtist(string manifestPath)
    {
        return TryLoadManifest(manifestPath, out RocksmithCachedSongManifest manifest)
            ? manifest.artist
            : null;
    }

    public static string FindManifestInDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return null;

        string exact = Path.Combine(directory, RocksmithCachedSongFormat.ManifestFileName);
        if (File.Exists(exact))
            return exact;

        return Directory.GetFiles(directory, "*.rs2song.json")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static void NormalizeManifest(string manifestPath, RocksmithCachedSongManifest manifest)
    {
        manifest.arrangements ??= new List<RocksmithCachedArrangementSummary>();
        string manifestDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;

        manifest.audioPath = ResolveStoredPath(manifestDirectory, manifest.audioPath);
        manifest.previewAudioPath = ResolveStoredPath(manifestDirectory, manifest.previewAudioPath);
        manifest.artworkPath = ResolveStoredPath(manifestDirectory, manifest.artworkPath);
        if (manifest.durationSeconds < 0f)
            manifest.durationSeconds = 0f;
        manifest.difficultyRating = Mathf.Clamp(manifest.difficultyRating, 0, 5);

        for (int i = 0; i < manifest.arrangements.Count; i++)
        {
            RocksmithCachedArrangementSummary arrangement = manifest.arrangements[i];
            if (arrangement == null)
                continue;

            arrangement.partFilePath = ResolveStoredPath(manifestDirectory, arrangement.partFilePath);
            arrangement.arrangementGroupId = string.IsNullOrWhiteSpace(arrangement.arrangementGroupId)
                ? arrangement.partId ?? string.Empty
                : arrangement.arrangementGroupId;
            arrangement.arrangementDisplayName = string.IsNullOrWhiteSpace(arrangement.arrangementDisplayName)
                ? (!string.IsNullOrWhiteSpace(arrangement.route) ? arrangement.route : arrangement.displayName)
                : arrangement.arrangementDisplayName;
            arrangement.difficultyLabel = NormalizeDifficultyLabel(arrangement.difficultyLabel, arrangement.difficultyUiIndex);
            arrangement.difficultyUiIndex = NormalizeDifficultyUiIndex(arrangement.difficultyUiIndex, arrangement.difficultyLabel);
            arrangement.tuningDisplayName = string.IsNullOrWhiteSpace(arrangement.tuningDisplayName)
                ? StringTuningUtils.FormatTuningDisplayName(arrangement.tuningPitches)
                : arrangement.tuningDisplayName;
            arrangement.difficultyRating = Mathf.Clamp(arrangement.difficultyRating, 0, 5);
        }

        manifest.arrangements = manifest.arrangements
            .OrderByDescending(arrangement => GetDefaultRoutePriority(arrangement?.route))
            .ThenBy(arrangement => arrangement?.arrangementDisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(arrangement => arrangement?.difficultyUiIndex ?? int.MaxValue)
            .ToList();
    }

    private static bool TryLoadPart(string partFilePath, out RocksmithCachedArrangementPart part)
    {
        part = null;
        if (string.IsNullOrWhiteSpace(partFilePath) || !File.Exists(partFilePath))
            return false;

        long ticks = File.GetLastWriteTimeUtc(partFilePath).Ticks;
        if (partCache.TryGetValue(partFilePath, out CachedPartEntry cached) &&
            cached != null &&
            cached.ticks == ticks &&
            cached.part != null)
        {
            part = cached.part;
            return true;
        }

        try
        {
            string json = File.ReadAllText(partFilePath);
            RocksmithCachedArrangementPart loaded = JsonUtility.FromJson<RocksmithCachedArrangementPart>(json);
            if (loaded == null || !IsSupportedSchemaVersion(loaded.schemaVersion))
                return false;

            loaded.notes ??= new List<RocksmithCachedNoteData>();
            loaded.arpeggioGuides ??= new List<RocksmithCachedArpeggioGuideData>();
            loaded.generatedNotes ??= new List<RocksmithCachedGeneratedNoteEvent>();
            loaded.timing ??= new RocksmithCachedArrangementTimingData();
            loaded.tones ??= new RocksmithCachedArrangementToneData();
            loaded.tones.changes ??= new List<RocksmithCachedToneChangeData>();
            loaded.tones.definitions ??= new List<RocksmithCachedToneDefinitionData>();
            loaded.timing.ebeats ??= new List<RocksmithCachedEbeatData>();
            loaded.timing.sections ??= new List<RocksmithCachedSectionData>();
            if (loaded.timing.averageTempoBpm <= 0.01f)
                loaded.timing.averageTempoBpm = 120f;
            if (loaded.timing.capo < 0)
                loaded.timing.capo = 0;
            loaded.timing.ebeats = loaded.timing.ebeats
                .Where(ebeat => ebeat != null)
                .OrderBy(ebeat => ebeat.timeSeconds)
                .ToList();
            loaded.timing.sections = loaded.timing.sections
                .Where(section => section != null && section.timeSeconds >= 0f)
                .OrderBy(section => section.timeSeconds)
                .ThenBy(section => section.number)
                .ToList();
            if (loaded.generatedPart == null)
                loaded.generatedPart = new RocksmithCachedGeneratedPartInfo();
            partCache[partFilePath] = new CachedPartEntry
            {
                ticks = ticks,
                part = loaded
            };
            part = loaded;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ImportedSongCache] Failed to parse part '{partFilePath}': {ex.Message}");
            return false;
        }
    }

    public static bool TryLoadArrangementPartByPartId(
        string manifestPath,
        string partId,
        out RocksmithCachedArrangementSummary summary,
        out RocksmithCachedArrangementPart part)
    {
        summary = null;
        part = null;

        if (string.IsNullOrWhiteSpace(partId) ||
            !TryLoadManifest(manifestPath, out RocksmithCachedSongManifest manifest) ||
            manifest?.arrangements == null)
        {
            return false;
        }

        for (int i = 0; i < manifest.arrangements.Count; i++)
        {
            RocksmithCachedArrangementSummary candidate = manifest.arrangements[i];
            if (candidate == null ||
                !string.Equals(candidate.partId ?? string.Empty, partId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            summary = candidate;
            return TryLoadPart(candidate.partFilePath, out part);
        }

        return false;
    }

    public static bool TryLoadArrangementPartByGroupId(
        string manifestPath,
        string arrangementGroupId,
        out RocksmithCachedArrangementSummary summary,
        out RocksmithCachedArrangementPart part)
    {
        summary = null;
        part = null;

        if (string.IsNullOrWhiteSpace(arrangementGroupId) ||
            !TryLoadManifest(manifestPath, out RocksmithCachedSongManifest manifest) ||
            manifest?.arrangements == null)
        {
            return false;
        }

        RocksmithCachedArrangementSummary best = manifest.arrangements
            .Where(candidate => candidate != null &&
                                string.Equals(candidate.arrangementGroupId ?? candidate.partId ?? string.Empty, arrangementGroupId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.difficultyUiIndex < 0 ? int.MaxValue : candidate.difficultyUiIndex)
            .ThenByDescending(candidate => candidate.noteCount)
            .FirstOrDefault();

        if (best == null)
            return false;

        summary = best;
        return TryLoadPart(best.partFilePath, out part);
    }

    private static int ResolveArrangementIndex(RocksmithCachedSongManifest manifest, int targetPartIndex)
    {
        if (manifest?.arrangements == null || manifest.arrangements.Count == 0)
            return -1;

        if (targetPartIndex >= 0 && targetPartIndex < manifest.arrangements.Count)
            return targetPartIndex;

        int bestIndex = 0;
        int bestScore = int.MinValue;
        for (int i = 0; i < manifest.arrangements.Count; i++)
        {
            RocksmithCachedArrangementSummary arrangement = manifest.arrangements[i];
            int score = arrangement != null
                ? GetDefaultRoutePriority(arrangement.route) + arrangement.score
                : int.MinValue;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static bool IsSupportedSchemaVersion(int schemaVersion)
    {
        return schemaVersion >= MinimumSupportedSchemaVersion &&
               schemaVersion <= RocksmithCachedSongFormat.SchemaVersion;
    }

    private static List<RocksmithCachedArrangementSummary> SelectGeneratedArrangementSummaries(List<RocksmithCachedArrangementSummary> arrangements)
    {
        if (arrangements == null || arrangements.Count == 0)
            return new List<RocksmithCachedArrangementSummary>();

        return arrangements
            .GroupBy(arrangement => string.IsNullOrWhiteSpace(arrangement?.arrangementGroupId) ? arrangement?.partId ?? string.Empty : arrangement.arrangementGroupId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(arrangement => arrangement?.difficultyUiIndex ?? int.MaxValue)
                .ThenByDescending(arrangement => arrangement?.score ?? 0)
                .FirstOrDefault())
            .Where(arrangement => arrangement != null)
            .ToList();
    }

    private static string NormalizeDifficultyLabel(string difficultyLabel, int difficultyUiIndex)
    {
        if (!string.IsNullOrWhiteSpace(difficultyLabel))
            return difficultyLabel.Trim();

        if (difficultyUiIndex == 0)
            return "Full";
        if (difficultyUiIndex > 0)
            return difficultyUiIndex.ToString(CultureInfo.InvariantCulture);
        return string.Empty;
    }

    private static int NormalizeDifficultyUiIndex(int difficultyUiIndex, string difficultyLabel)
    {
        if (difficultyUiIndex >= 0)
            return difficultyUiIndex;

        if (string.IsNullOrWhiteSpace(difficultyLabel))
            return -1;

        string normalized = difficultyLabel.Trim();
        if (string.Equals(normalized, "Full", StringComparison.OrdinalIgnoreCase))
            return 0;

        return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? Math.Max(0, parsed)
            : -1;
    }

    private static int GetDefaultRoutePriority(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return 0;

        if (route.IndexOf("lead", StringComparison.OrdinalIgnoreCase) >= 0)
            return 4000;
        if (route.IndexOf("combo", StringComparison.OrdinalIgnoreCase) >= 0)
            return 3000;
        if (route.IndexOf("rhythm", StringComparison.OrdinalIgnoreCase) >= 0)
            return 2000;
        if (route.IndexOf("bass", StringComparison.OrdinalIgnoreCase) >= 0)
            return 1000;
        return 0;
    }

    private static string ResolveStoredPath(string baseDirectory, string storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            return storedPath;

        if (Path.IsPathRooted(storedPath))
            return storedPath;

        string resolved = Path.GetFullPath(Path.Combine(baseDirectory ?? string.Empty, storedPath));
        if (File.Exists(resolved) || Directory.Exists(resolved))
            return resolved;

        string remapped = TryRemapLegacyStoredPath(baseDirectory, storedPath);
        if (!string.IsNullOrWhiteSpace(remapped))
            return remapped;

        return resolved;
    }

    private static string TryRemapLegacyStoredPath(string baseDirectory, string storedPath)
    {
        string normalized = storedPath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        string legacyPrefix = RocksmithCachedSongFormat.LegacyContentDirectoryName + Path.DirectorySeparatorChar;
        string currentPrefix = RocksmithCachedSongFormat.ContentDirectoryName + Path.DirectorySeparatorChar;

        if (normalized.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string remapped = RocksmithCachedSongFormat.ContentDirectoryName + normalized.Substring(RocksmithCachedSongFormat.LegacyContentDirectoryName.Length);
            return Path.GetFullPath(Path.Combine(baseDirectory ?? string.Empty, remapped));
        }

        if (normalized.StartsWith(currentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string remapped = RocksmithCachedSongFormat.LegacyContentDirectoryName + normalized.Substring(RocksmithCachedSongFormat.ContentDirectoryName.Length);
            return Path.GetFullPath(Path.Combine(baseDirectory ?? string.Empty, remapped));
        }

        return null;
    }

    private static int GetMaxPitchBendRange(List<RocksmithCachedGeneratedNoteEvent> notes)
    {
        if (notes == null || notes.Count == 0)
            return 0;

        int range = 0;
        for (int i = 0; i < notes.Count; i++)
            range = Mathf.Max(range, notes[i].pitchBendRangeSemitones);
        return range;
    }
}
