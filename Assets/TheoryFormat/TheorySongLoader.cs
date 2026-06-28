using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class TheorySongLoader
{
    private const string ChartEditorPackageDirectoryName = "chart editor";

    public static bool IsPackagePath(string filePath)
    {
        return TheoryPackageFormat.IsPackagePath(filePath);
    }

    public static bool TryLoadManifest(string packagePath, out TheorySongManifest manifest)
    {
        return TheoryPackageIO.TryReadManifest(packagePath, out manifest, out _);
    }

    public static List<MusicXmlLoader.MusicXmlPartSummary> GetPartSummaries(string packagePath)
    {
        if (!TheoryPackageIO.TryReadManifest(packagePath, out TheorySongManifest manifest, out _) ||
            manifest.arrangements == null)
        {
            return new List<MusicXmlLoader.MusicXmlPartSummary>();
        }

        List<MusicXmlLoader.MusicXmlPartSummary> summaries = new List<MusicXmlLoader.MusicXmlPartSummary>();
        for (int i = 0; i < manifest.arrangements.Count; i++)
        {
            TheoryArrangementSummary arrangement = manifest.arrangements[i];
            if (arrangement == null || string.IsNullOrWhiteSpace(arrangement.entry))
                continue;

            summaries.Add(new MusicXmlLoader.MusicXmlPartSummary
            {
                Index = i,
                PartId = arrangement.arrangementId ?? string.Empty,
                Name = string.IsNullOrWhiteSpace(arrangement.displayName) ? arrangement.route ?? $"Arrangement {i + 1}" : arrangement.displayName,
                InstrumentType = arrangement.instrumentType ?? string.Empty,
                Route = arrangement.route ?? string.Empty,
                GroupId = arrangement.groupId ?? arrangement.arrangementId ?? string.Empty,
                GroupDisplayName = string.IsNullOrWhiteSpace(arrangement.groupDisplayName)
                    ? (string.IsNullOrWhiteSpace(arrangement.route) ? arrangement.displayName : arrangement.route)
                    : arrangement.groupDisplayName,
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

    public static List<NoteData> LoadSong(string packagePath, int targetPartIndex = -1)
    {
        if (!TryLoadArrangement(packagePath, targetPartIndex, out _, out TheoryArrangementData arrangement))
            return new List<NoteData>();

        List<NoteData> notes = new List<NoteData>(arrangement.notes?.Count ?? 0);
        if (arrangement.notes == null)
            return notes;

        List<TheoryNoteData> ordered = arrangement.notes
            .Where(note => note != null)
            .OrderBy(note => note.time)
            .ThenBy(note => note.stringIndex)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            TheoryNoteData source = ordered[i];
            List<NoteTechniqueSegmentData> segments = BuildNormalizedTechniqueSegments(source);
            NoteTechnique primaryTechnique = ResolveRuntimePrimaryTechnique(source);
            notes.Add(new NoteData(
                source.id,
                source.time,
                source.duration,
                source.stringIndex,
                source.fret,
                source.noteName,
                source.chordId,
                primaryTechnique,
                source.slideTargetFret,
                source.bendStep,
                source.legato,
                source.requiresPluck,
                source.linkedFromNoteId,
                source.bendPreBend,
                source.bendRelease,
                source.bendVisualStartTime,
                source.bendVisualDuration,
                segments,
                source.muted,
                source.chordName));
        }

        NormalizeTheoryLegatoTransitions(notes);
        return notes;
    }

    public static List<ArpeggioGuideData> LoadArpeggioGuides(string packagePath, int targetPartIndex = -1)
    {
        if (!TryLoadArrangement(packagePath, targetPartIndex, out _, out TheoryArrangementData arrangement) ||
            arrangement.arpeggioGuides == null)
        {
            return new List<ArpeggioGuideData>();
        }

        List<ArpeggioGuideData> guides = new List<ArpeggioGuideData>(arrangement.arpeggioGuides.Count);
        for (int i = 0; i < arrangement.arpeggioGuides.Count; i++)
        {
            TheoryArpeggioGuideData source = arrangement.arpeggioGuides[i];
            if (source == null || source.stringFrets == null || source.stringFrets.Length == 0)
                continue;

            guides.Add(new ArpeggioGuideData
            {
                id = source.id,
                startTime = source.startTime,
                endTime = source.endTime,
                chordName = source.chordName,
                stringFrets = (int[])source.stringFrets.Clone()
            });
        }

        return guides;
    }

    public static GeneratedPlaybackArrangement LoadGeneratedArrangement(string packagePath)
    {
        if (!TheoryPackageIO.TryReadManifest(packagePath, out TheorySongManifest manifest, out _) ||
            manifest.arrangements == null)
        {
            return null;
        }

        GeneratedPlaybackArrangement result = new GeneratedPlaybackArrangement
        {
            sourcePath = packagePath,
            durationSeconds = Mathf.Max(0f, manifest.durationSeconds)
        };

        List<TheoryArrangementSummary> generatedSummaries = SelectGeneratedArrangementSummaries(manifest.arrangements);
        int nextChannel = 0;
        for (int i = 0; i < generatedSummaries.Count; i++)
        {
            TheoryArrangementSummary summary = generatedSummaries[i];
            if (!TheoryPackageIO.TryReadArrangement(packagePath, summary, out TheoryArrangementData arrangement, out _))
                continue;

            if (arrangement.generatedPart != null)
            {
                result.parts.Add(new GeneratedPlaybackPartInfo
                {
                    partId = arrangement.generatedPart.partId ?? arrangement.arrangementId ?? $"theory_part_{i}",
                    displayName = arrangement.generatedPart.displayName ?? arrangement.displayName,
                    instrumentName = arrangement.generatedPart.instrumentName ?? arrangement.route,
                    sourceMidiChannel = arrangement.generatedPart.sourceMidiChannel,
                    sourceMidiProgram = arrangement.generatedPart.sourceMidiProgram,
                    preferredBank = arrangement.generatedPart.preferredBank,
                    isDrum = arrangement.generatedPart.isDrum,
                    isGuitarFamily = arrangement.generatedPart.isGuitarFamily,
                    isExplicitHarmonicPart = arrangement.generatedPart.isExplicitHarmonicPart
                });
            }

            result.channelAssignments.Add(new GeneratedPlaybackChannelAssignment
            {
                channel = Mathf.Clamp(nextChannel, 0, 15),
                bank = -1,
                preset = arrangement.generatedPart != null ? arrangement.generatedPart.sourceMidiProgram : 29,
                isDrum = arrangement.generatedPart != null && arrangement.generatedPart.isDrum,
                label = arrangement.displayName,
                sourcePartId = arrangement.arrangementId,
                sourcePartName = arrangement.displayName,
                pitchBendRangeSemitones = GetMaxPitchBendRange(arrangement.generatedNotes)
            });

            if (arrangement.generatedNotes != null)
            {
                for (int noteIndex = 0; noteIndex < arrangement.generatedNotes.Count; noteIndex++)
                {
                    TheoryGeneratedNoteEvent source = arrangement.generatedNotes[noteIndex];
                    if (source == null)
                        continue;

                    GeneratedPlaybackNoteEvent note = new GeneratedPlaybackNoteEvent
                    {
                        startTimeSeconds = source.startTimeSeconds,
                        durationSeconds = source.durationSeconds,
                        pitchPreRollSeconds = source.pitchPreRollSeconds,
                        midiNote = source.midiNote,
                        velocity = source.velocity,
                        channel = Mathf.Clamp(nextChannel, 0, 15),
                        partId = source.partId ?? arrangement.arrangementId,
                        partName = source.partName ?? arrangement.displayName,
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
                        for (int pointIndex = 0; pointIndex < source.pitchCurve.Count; pointIndex++)
                        {
                            TheoryGeneratedPitchPoint point = source.pitchCurve[pointIndex];
                            if (point == null)
                                continue;

                            note.pitchCurve.Add(new GeneratedPlaybackPitchPoint
                            {
                                normalizedTime = point.normalizedTime,
                                semitoneOffset = point.semitoneOffset
                            });
                        }
                    }

                    result.notes.Add(note);
                }
            }

            nextChannel++;
            if (nextChannel == 9)
                nextChannel++;
        }

        if (result.durationSeconds <= 0.01f && result.notes.Count > 0)
            result.durationSeconds = result.notes.Max(note => note.EndTimeSeconds);

        return result;
    }

    public static string TryReadDisplayName(string packagePath)
    {
        return TheoryPackageIO.TryReadManifest(packagePath, out TheorySongManifest manifest, out _)
            ? manifest.title
            : null;
    }

    public static string TryReadArtist(string packagePath)
    {
        return TheoryPackageIO.TryReadManifest(packagePath, out TheorySongManifest manifest, out _)
            ? manifest.artist
            : null;
    }

    public static bool IsLoadablePackage(string packagePath)
    {
        return TheoryPackageIO.TryReadManifest(packagePath, out _, out _);
    }

    public static string FindPackageInDirectory(string directory, bool requireLoadable = false)
    {
        if (!Directory.Exists(directory))
            return null;

        List<string> candidates = new List<string>();
        AddPackageCandidates(directory, candidates);
        AddPackageCandidates(Path.Combine(directory, ChartEditorPackageDirectoryName), candidates);

        if (!requireLoadable)
            return OrderPackageCandidates(candidates).FirstOrDefault();

        return OrderPackageCandidates(candidates).FirstOrDefault(IsLoadablePackage);
    }

    private static void AddPackageCandidates(string directory, List<string> candidates)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory) || candidates == null)
            return;

        candidates.AddRange(Directory.GetFiles(directory, $"*{TheoryPackageFormat.Extension}", SearchOption.TopDirectoryOnly));
    }

    private static IEnumerable<string> OrderPackageCandidates(IEnumerable<string> candidates)
    {
        return (candidates ?? Enumerable.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(TryGetLastWriteTicks)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static long TryGetLastWriteTicks(string path)
    {
        try
        {
            return string.IsNullOrWhiteSpace(path) || !File.Exists(path)
                ? 0L
                : File.GetLastWriteTimeUtc(path).Ticks;
        }
        catch
        {
            return 0L;
        }
    }

    public static bool TryLoadArrangementByPartId(
        string packagePath,
        string arrangementId,
        out TheoryArrangementSummary summary,
        out TheoryArrangementData arrangement)
    {
        return TryLoadArrangementByPredicate(
            packagePath,
            candidate => string.Equals(candidate?.arrangementId ?? string.Empty, arrangementId ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            out summary,
            out arrangement);
    }

    public static bool TryLoadArrangementByGroupId(
        string packagePath,
        string groupId,
        out TheoryArrangementSummary summary,
        out TheoryArrangementData arrangement)
    {
        return TryLoadArrangementByPredicate(
            packagePath,
            candidate => string.Equals(candidate?.groupId ?? string.Empty, groupId ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            out summary,
            out arrangement);
    }

    private static bool TryLoadArrangementByPredicate(
        string packagePath,
        Func<TheoryArrangementSummary, bool> predicate,
        out TheoryArrangementSummary summary,
        out TheoryArrangementData arrangement)
    {
        summary = null;
        arrangement = null;
        if (predicate == null ||
            !TheoryPackageIO.TryReadManifest(packagePath, out TheorySongManifest manifest, out _) ||
            manifest.arrangements == null)
        {
            return false;
        }

        for (int i = 0; i < manifest.arrangements.Count; i++)
        {
            TheoryArrangementSummary candidate = manifest.arrangements[i];
            if (candidate == null || !predicate(candidate))
                continue;

            summary = candidate;
            return TheoryPackageIO.TryReadArrangement(packagePath, candidate, out arrangement, out _);
        }

        return false;
    }

    private static bool TryLoadArrangement(
        string packagePath,
        int targetPartIndex,
        out TheoryArrangementSummary summary,
        out TheoryArrangementData arrangement)
    {
        summary = null;
        arrangement = null;
        if (!TheoryPackageIO.TryReadManifest(packagePath, out TheorySongManifest manifest, out _) ||
            manifest.arrangements == null ||
            manifest.arrangements.Count == 0)
        {
            return false;
        }

        int index = ResolveArrangementIndex(manifest, targetPartIndex);
        if (index < 0 || index >= manifest.arrangements.Count)
            return false;

        summary = manifest.arrangements[index];
        return TheoryPackageIO.TryReadArrangement(packagePath, summary, out arrangement, out _);
    }

    private static int ResolveArrangementIndex(TheorySongManifest manifest, int targetPartIndex)
    {
        if (manifest?.arrangements == null || manifest.arrangements.Count == 0)
            return -1;

        if (targetPartIndex >= 0 && targetPartIndex < manifest.arrangements.Count)
            return targetPartIndex;

        if (!string.IsNullOrWhiteSpace(manifest.defaultArrangementId))
        {
            for (int i = 0; i < manifest.arrangements.Count; i++)
            {
                if (string.Equals(manifest.arrangements[i]?.arrangementId ?? string.Empty, manifest.defaultArrangementId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        int bestIndex = 0;
        int bestScore = int.MinValue;
        for (int i = 0; i < manifest.arrangements.Count; i++)
        {
            TheoryArrangementSummary arrangement = manifest.arrangements[i];
            int score = GetDefaultRoutePriority(arrangement?.route) + (arrangement?.score ?? 0);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static NoteTechnique ResolveRuntimePrimaryTechnique(TheoryNoteData source)
    {
        if (source == null)
            return NoteTechnique.None;
        if (source.hammerOn)
            return NoteTechnique.HammerOn;
        if (source.pullOff)
            return NoteTechnique.PullOff;
        return (NoteTechnique)Mathf.Clamp(source.primaryTechnique, (int)NoteTechnique.None, (int)NoteTechnique.Vibrato);
    }

    private static List<NoteTechniqueSegmentData> BuildNormalizedTechniqueSegments(TheoryNoteData source)
    {
        return TheoryTechniqueSegmentNormalizer.Build(source);
    }

    private static void NormalizeTheoryLegatoTransitions(List<NoteData> notes)
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

    private static List<TheoryArrangementSummary> SelectGeneratedArrangementSummaries(List<TheoryArrangementSummary> arrangements)
    {
        if (arrangements == null || arrangements.Count == 0)
            return new List<TheoryArrangementSummary>();

        return arrangements
            .GroupBy(arrangement => string.IsNullOrWhiteSpace(arrangement?.groupId) ? arrangement?.arrangementId ?? string.Empty : arrangement.groupId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(arrangement => arrangement?.difficultyUiIndex ?? int.MaxValue)
                .ThenByDescending(arrangement => arrangement?.score ?? 0)
                .FirstOrDefault())
            .Where(arrangement => arrangement != null)
            .ToList();
    }

    private static int GetMaxPitchBendRange(List<TheoryGeneratedNoteEvent> notes)
    {
        if (notes == null || notes.Count == 0)
            return 0;

        int max = 0;
        for (int i = 0; i < notes.Count; i++)
            max = Mathf.Max(max, notes[i]?.pitchBendRangeSemitones ?? 0);
        return max;
    }

    private static int GetDefaultRoutePriority(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return 0;

        switch (route.Trim().ToLowerInvariant())
        {
            case "lead": return 3000;
            case "rhythm": return 2500;
            case "bass": return 2200;
            case "drums": return 1800;
            case "vocals": return 1000;
            default: return 0;
        }
    }
}
