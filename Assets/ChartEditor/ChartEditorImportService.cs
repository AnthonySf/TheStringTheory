using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

public static class ChartEditorImportService
{
    private static readonly HashSet<string> ChartExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".theory", ".gp", ".gp3", ".gp4", ".gp5", ".gpx", ".musicxml", ".xml"
    };

    private static readonly HashSet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".ogg", ".flac", ".m4a", ".aiff", ".aif"
    };

    public static bool IsSupportedChartPath(string path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               ChartExtensions.Contains(Path.GetExtension(path) ?? string.Empty);
    }

    public static bool IsSupportedAudioPath(string path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               AudioExtensions.Contains(Path.GetExtension(path) ?? string.Empty);
    }

    public static bool ImportChartAndAudio(string chartPath, string audioPath, out ChartEditorImportResult result, out string error)
    {
        result = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(chartPath) || !File.Exists(chartPath))
        {
            error = "Chart file was not found.";
            return false;
        }

        if (!SongNotationFacade.TryDetectKind(chartPath, out SongNotationSourceKind kind) || kind == SongNotationSourceKind.None)
        {
            error = $"Unsupported chart file: {Path.GetExtension(chartPath)}";
            return false;
        }

        if (kind == SongNotationSourceKind.TheoryPackage)
            return ImportTheoryPackage(chartPath, out result, out error);

        result = ImportNotationProject(chartPath, kind, audioPath, ChartEditorSourceKindFromNotation(kind));
        return true;
    }

    public static bool ImportTheoryPackage(string packagePath, out ChartEditorImportResult result, out string error)
    {
        result = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            error = "Theory package was not found.";
            return false;
        }

        if (!TheoryPackageIO.TryReadManifest(packagePath, out TheorySongManifest manifest, out error))
            return false;

        string audioPath = string.Empty;
        string audioWarning = string.Empty;
        if (!TheoryPackageCache.TryCachePrimaryAudio(packagePath, manifest, out audioPath, out audioWarning))
            audioPath = string.Empty;

        string coverPath = string.Empty;
        TheoryPackageCache.TryCacheCoverArt(packagePath, manifest, out coverPath, out _);

        ChartEditorProject project = CreateBaseProject(packagePath, ChartEditorSourceKind.TheoryPackage);
        project.sourceFolder = Path.GetDirectoryName(packagePath) ?? string.Empty;
        project.metadata.title = FirstNonEmpty(manifest.title, Path.GetFileNameWithoutExtension(packagePath));
        project.metadata.artist = manifest.artist ?? string.Empty;
        project.metadata.album = manifest.album ?? string.Empty;
        project.metadata.genre = manifest.genre ?? string.Empty;
        project.metadata.year = manifest.year ?? string.Empty;
        project.metadata.coverImagePath = coverPath;
        project.metadata.defaultArrangementId = manifest.defaultArrangementId ?? string.Empty;
        project.audio = BuildAudioInfo(audioPath, manifest.durationSeconds);

        List<MusicXmlLoader.MusicXmlPartSummary> selectedSummaries = SelectFullDifficultySummaries(TheorySongLoader.GetPartSummaries(packagePath));
        for (int i = 0; i < selectedSummaries.Count; i++)
        {
            MusicXmlLoader.MusicXmlPartSummary summary = selectedSummaries[i];
            if (summary == null || summary.Index < 0 || summary.Index >= (manifest.arrangements?.Count ?? 0))
                continue;

            TheoryArrangementSummary arrangementSummary = manifest.arrangements[summary.Index];
            if (!TheoryPackageIO.TryReadArrangement(packagePath, arrangementSummary, out TheoryArrangementData arrangement, out _))
                continue;

            project.tracks.Add(BuildTrack(summary, arrangement));
        }

        ImportTheoryTiming(packagePath, manifest, project);
        ApplyTheoryEditorState(packagePath, project);
        FinishProject(project);

        result = new ChartEditorImportResult { project = project };
        if (!string.IsNullOrWhiteSpace(audioWarning))
            result.warnings.Add(audioWarning);
        result.warnings.AddRange(ChartEditorValidationService.BuildWarnings(project));
        return true;
    }

    public static bool ImportPsarc(string psarcPath, out ChartEditorImportResult result, out string error)
    {
        result = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(psarcPath) || !File.Exists(psarcPath))
        {
            error = "PSARC file was not found.";
            return false;
        }

        if (!RocksmithImportService.RefreshImportForPsarc(psarcPath, out error))
            return false;

        string manifestPath = RocksmithImportService.GetImportedManifestPathForPsarc(psarcPath);
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            error = "PSARC import completed without a usable song manifest.";
            return false;
        }

        if (!ImportArrangementManifest(manifestPath, out result, out error))
            return false;

        result.project.sourceKind = ChartEditorSourceKind.Psarc;
        result.project.sourcePath = psarcPath;
        result.warnings.Add("PSARC was unpacked through the existing library importer; the editor is using the extracted manifest.");
        return true;
    }

    public static bool ImportFolder(string folderPath, out ChartEditorImportResult result, out string error)
    {
        result = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            error = "Folder was not found.";
            return false;
        }

        string theoryPackagePath = TheorySongLoader.FindPackageInDirectory(folderPath, requireLoadable: true);
        if (!string.IsNullOrWhiteSpace(theoryPackagePath) && File.Exists(theoryPackagePath))
        {
            bool imported = ImportTheoryPackage(theoryPackagePath, out result, out error);
            if (imported)
                result.project.sourceFolder = folderPath;

            return imported;
        }

        string manifestPath = ArrangementCacheSongLoader.FindManifestInDirectory(folderPath);
        if (!string.IsNullOrWhiteSpace(manifestPath) && File.Exists(manifestPath))
        {
            bool imported = ImportArrangementManifest(manifestPath, out result, out error);
            if (imported)
            {
                result.project.sourceKind = ChartEditorSourceKind.Folder;
                result.project.sourceFolder = folderPath;
            }

            return imported;
        }

        string chartPath = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(path => IsSupportedChartPath(path) &&
                           (!TheoryPackageFormat.IsPackagePath(path) || TheorySongLoader.IsLoadablePackage(path)))
            .OrderBy(GetChartPreference)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        string audioPath = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(IsSupportedAudioPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(chartPath))
        {
            error = "No supported chart or Rocksmith manifest was found in the folder.";
            return false;
        }

        if (!ImportChartAndAudio(chartPath, audioPath, out result, out error))
            return false;

        result.project.sourceKind = ChartEditorSourceKind.Folder;
        result.project.sourceFolder = folderPath;
        if (string.IsNullOrWhiteSpace(audioPath))
            result.warnings.Add("No audio file was found in the folder. The project can still be edited, but export should include audio before gameplay.");
        return true;
    }

    public static bool ImportArrangementManifest(string manifestPath, out ChartEditorImportResult result, out string error)
    {
        result = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            error = "Arrangement manifest was not found.";
            return false;
        }

        if (!RocksmithCachedSongLoader.TryLoadManifest(manifestPath, out RocksmithCachedSongManifest manifest) || manifest == null)
        {
            error = "Arrangement manifest could not be read.";
            return false;
        }

        ChartEditorProject project = CreateBaseProject(manifestPath, ChartEditorSourceKind.ArrangementCache);
        project.sourceFolder = Path.GetDirectoryName(manifestPath) ?? string.Empty;
        project.metadata.title = FirstNonEmpty(manifest.displayName, Path.GetFileName(project.sourceFolder));
        project.metadata.artist = manifest.artist ?? string.Empty;
        project.metadata.album = manifest.album ?? string.Empty;
        project.metadata.coverImagePath = manifest.artworkPath ?? string.Empty;
        project.audio = BuildAudioInfo(manifest.audioPath, manifest.durationSeconds);

        List<MusicXmlLoader.MusicXmlPartSummary> summaries = SelectFullDifficultySummaries(ArrangementCacheSongLoader.GetPartSummaries(manifestPath));
        for (int i = 0; i < summaries.Count; i++)
        {
            MusicXmlLoader.MusicXmlPartSummary summary = summaries[i];
            if (summary == null)
                continue;

            if (RocksmithCachedSongLoader.TryLoadArrangementPart(manifestPath, summary.Index, out _, out RocksmithCachedArrangementPart part) &&
                part != null)
            {
                project.tracks.Add(BuildTrack(summary, part));
            }
            else
            {
                List<NoteData> notes = ArrangementCacheSongLoader.LoadSong(manifestPath, summary.Index) ?? new List<NoteData>();
                project.tracks.Add(BuildTrack(summary, notes));
            }
        }

        ImportArrangementSections(manifestPath, project);
        ImportArrangementBeatMap(manifestPath, project);
        FinishProject(project);
        result = new ChartEditorImportResult { project = project };
        result.warnings.AddRange(ChartEditorValidationService.BuildWarnings(project));
        return true;
    }

    private static ChartEditorImportResult ImportNotationProject(
        string notationPath,
        SongNotationSourceKind kind,
        string audioPath,
        ChartEditorSourceKind sourceKind)
    {
        ChartEditorProject project = CreateBaseProject(notationPath, sourceKind);
        project.sourceFolder = Path.GetDirectoryName(notationPath) ?? string.Empty;
        project.metadata.title = FirstNonEmpty(
            SongNotationFacade.TryReadDisplayName(notationPath, kind),
            Path.GetFileNameWithoutExtension(notationPath));
        project.metadata.artist = SongNotationFacade.TryReadCreator(notationPath, kind) ?? string.Empty;
        project.audio = BuildAudioInfo(audioPath, 0f);

        List<MusicXmlLoader.MusicXmlPartSummary> summaries = SelectFullDifficultySummaries(SongNotationFacade.GetPartSummaries(notationPath, kind));
        if (summaries == null || summaries.Count == 0)
        {
            summaries = new List<MusicXmlLoader.MusicXmlPartSummary>
            {
                new MusicXmlLoader.MusicXmlPartSummary
                {
                    Index = -1,
                    PartId = "track_1",
                    Name = "Track",
                    GroupId = "track_1",
                    GroupDisplayName = "Track"
                }
            };
        }

        for (int i = 0; i < summaries.Count; i++)
        {
            MusicXmlLoader.MusicXmlPartSummary summary = summaries[i];
            int partIndex = summary?.Index ?? i;
            List<NoteData> notes = SongNotationFacade.LoadSong(notationPath, kind, partIndex) ?? new List<NoteData>();
            project.tracks.Add(BuildTrack(summary, notes));
        }

        if (kind == SongNotationSourceKind.Gp5)
        {
            ImportGpSections(notationPath, project);
            ImportGpBeatMap(notationPath, project);
        }
        else if (kind == SongNotationSourceKind.MusicXml)
        {
            ImportMusicXmlSections(notationPath, project);
            ImportMusicXmlBeatMap(notationPath, project);
        }

        FinishProject(project);
        ChartEditorImportResult result = new ChartEditorImportResult { project = project };
        result.warnings.AddRange(ChartEditorValidationService.BuildWarnings(project));
        if (!string.IsNullOrWhiteSpace(audioPath) && !IsSupportedAudioPath(audioPath))
            result.warnings.Add($"Audio extension '{Path.GetExtension(audioPath)}' is referenced but may not decode on every Unity platform.");
        return result;
    }

    private static ChartEditorProject CreateBaseProject(string sourcePath, ChartEditorSourceKind sourceKind)
    {
        return new ChartEditorProject
        {
            projectId = Guid.NewGuid().ToString("N"),
            sourceKind = sourceKind,
            sourcePath = sourcePath ?? string.Empty,
            metadata = new ChartEditorSongMetadata(),
            audio = new ChartEditorAudioInfo(),
            tracks = new List<ChartEditorTrack>(),
            sections = new List<ChartEditorSection>(),
            beatMap = new ChartEditorBeatMap(),
            syncPoints = new List<ChartEditorSyncPoint>()
        };
    }

    private static void FinishProject(ChartEditorProject project)
    {
        project.EnsureDefaults();
        KeepFullDifficultyTracksOnly(project);
        if (project.tracks.Count > 0)
            project.selectedTrackId = project.tracks[0].id;

        if (project.sections.Count == 0)
            BuildFallbackSections(project);

        ChartEditorTimingService.NormalizeSections(project);
        ChartEditorRuntimeNoteSanitizer.SanitizeProjectNotes(project);
        ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: true);
        RefreshGeneratedPlaybackFingerprints(project);
        project.dirty = false;
    }

    private static void RefreshGeneratedPlaybackFingerprints(ChartEditorProject project)
    {
        if (project?.tracks == null)
            return;

        for (int i = 0; i < project.tracks.Count; i++)
        {
            ChartEditorTrack track = project.tracks[i];
            if ((track?.generatedNotes?.Count ?? 0) == 0)
                continue;

            track.generatedPlaybackNoteFingerprint = ChartEditorGeneratedPlaybackIntegrity.ComputeNoteFingerprint(track);
        }
    }

    public static void KeepFullDifficultyTracksOnly(ChartEditorProject project)
    {
        if (project?.tracks == null || project.tracks.Count <= 1)
            return;

        project.tracks = project.tracks
            .Where(track => track != null)
            .GroupBy(GetFullDifficultyTrackGroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(track => track.notes?.Count ?? 0)
                .ThenBy(track => track.displayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .First())
            .Where(track => track != null)
            .ToList();

        if (!project.tracks.Any(track => string.Equals(track.id, project.selectedTrackId, StringComparison.OrdinalIgnoreCase)))
            project.selectedTrackId = project.tracks.Count > 0 ? project.tracks[0].id : string.Empty;
    }

    private static List<MusicXmlLoader.MusicXmlPartSummary> SelectFullDifficultySummaries(List<MusicXmlLoader.MusicXmlPartSummary> summaries)
    {
        summaries ??= new List<MusicXmlLoader.MusicXmlPartSummary>();
        if (summaries.Count <= 1)
            return summaries.Where(summary => summary != null).ToList();

        return summaries
            .Where(summary => summary != null)
            .GroupBy(GetFullDifficultySummaryGroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(summary => IsFullDifficultySummary(summary) ? 0 : 1)
                .ThenBy(summary => summary.DifficultyUiIndex < 0 ? int.MaxValue : summary.DifficultyUiIndex)
                .ThenByDescending(summary => summary.NoteCount)
                .ThenByDescending(summary => summary.TabCount)
                .ThenByDescending(summary => summary.Score)
                .First())
            .Where(summary => summary != null)
            .ToList();
    }

    private static bool IsFullDifficultySummary(MusicXmlLoader.MusicXmlPartSummary summary)
    {
        if (summary == null)
            return false;

        return summary.DifficultyUiIndex == 0 ||
               string.Equals(summary.DifficultyLabel?.Trim(), "Full", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetFullDifficultySummaryGroupKey(MusicXmlLoader.MusicXmlPartSummary summary)
    {
        if (summary == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(summary.GroupId))
            return summary.GroupId.Trim();

        if (!string.IsNullOrWhiteSpace(summary.PartId))
            return summary.PartId.Trim();

        return $"{NormalizeTrackKey(summary.GroupDisplayName)}|{NormalizeTrackKey(summary.Name)}|{NormalizeTrackKey(summary.TuningDisplayName)}";
    }

    private static string GetFullDifficultyTrackGroupKey(ChartEditorTrack track)
    {
        if (track == null)
            return string.Empty;

        return $"{track.role}|{NormalizeTrackKey(track.displayName)}|{NormalizeTrackKey(track.tuning?.displayName)}";
    }

    private static string NormalizeTrackKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    private static ChartEditorTrack BuildTrack(MusicXmlLoader.MusicXmlPartSummary summary, List<NoteData> sourceNotes)
    {
        string importedName = FirstNonEmpty(summary?.GroupDisplayName, summary?.Name, "Track");
        ChartEditorTrackRole role = ResolveRole(summary, summary?.Route, summary?.InstrumentType, summary?.GroupDisplayName, summary?.Name);
        ChartEditorTrack track = new ChartEditorTrack
        {
            id = FirstNonEmpty(summary?.PartId, summary?.GroupId, Guid.NewGuid().ToString("N")),
            importedName = importedName,
            displayName = importedName,
            role = role,
            colorHex = ColorForRole(role),
            tuning = new ChartEditorTuningInfo
            {
                displayName = summary?.TuningDisplayName ?? string.Empty,
                stringPitches = summary?.StringTuningPitches != null ? (int[])summary.StringTuningPitches.Clone() : null
            },
            notes = new List<ChartEditorNote>(),
            arpeggioGuides = new List<ChartEditorArpeggioGuide>()
        };

        sourceNotes ??= new List<NoteData>();
        for (int i = 0; i < sourceNotes.Count; i++)
            track.notes.Add(FromNoteData(sourceNotes[i], i));

        track.notes = track.notes
            .OrderBy(note => note.timeSeconds)
            .ThenBy(note => note.stringOrLane)
            .ToList();
        track.EnsureDefaults();
        return track;
    }

    private static ChartEditorTrack BuildTrack(MusicXmlLoader.MusicXmlPartSummary summary, RocksmithCachedArrangementPart part)
    {
        string importedName = FirstNonEmpty(summary?.GroupDisplayName, summary?.Name, part?.arrangementDisplayName, part?.displayName, "Track");
        ChartEditorTrackRole role = ResolveRole(
            summary,
            part?.route,
            summary?.Route,
            summary?.InstrumentType,
            summary?.GroupDisplayName,
            summary?.Name,
            part?.arrangementDisplayName,
            part?.displayName);
        ChartEditorTrack track = new ChartEditorTrack
        {
            id = FirstNonEmpty(summary?.PartId, part?.partId, summary?.GroupId, part?.arrangementGroupId, Guid.NewGuid().ToString("N")),
            importedName = importedName,
            displayName = importedName,
            role = role,
            colorHex = ColorForRole(role),
            tuning = new ChartEditorTuningInfo
            {
                displayName = FirstNonEmpty(summary?.TuningDisplayName, part?.tuningDisplayName),
                stringPitches = summary?.StringTuningPitches != null
                    ? (int[])summary.StringTuningPitches.Clone()
                    : part?.tuningPitches != null ? (int[])part.tuningPitches.Clone() : null
            },
            generatedPart = FromCachedGeneratedPart(part?.generatedPart, part?.partId, importedName, role),
            tones = FromCachedToneData(part?.tones, part?.route ?? summary?.Name ?? summary?.GroupDisplayName),
            notes = new List<ChartEditorNote>(),
            arpeggioGuides = new List<ChartEditorArpeggioGuide>(),
            generatedNotes = FromCachedGeneratedNotes(part?.generatedNotes)
        };

        List<RocksmithCachedNoteData> sourceNotes = part?.notes ?? new List<RocksmithCachedNoteData>();
        for (int i = 0; i < sourceNotes.Count; i++)
            track.notes.Add(FromCachedNoteData(sourceNotes[i], i));

        if (part?.arpeggioGuides != null)
        {
            for (int i = 0; i < part.arpeggioGuides.Count; i++)
            {
                RocksmithCachedArpeggioGuideData guide = part.arpeggioGuides[i];
                if (guide == null)
                    continue;

                track.arpeggioGuides.Add(new ChartEditorArpeggioGuide
                {
                    id = guide.id,
                    startTime = guide.startTime,
                    endTime = guide.endTime,
                    chordName = guide.chordName ?? string.Empty,
                    stringFrets = guide.stringFrets != null ? (int[])guide.stringFrets.Clone() : null
                });
            }
        }

        track.notes = track.notes
            .Where(note => note != null)
            .OrderBy(note => note.timeSeconds)
            .ThenBy(note => note.stringOrLane)
            .ToList();
        track.EnsureDefaults();
        if (track.generatedNotes.Count > 0)
            track.generatedPlaybackNoteFingerprint = ChartEditorGeneratedPlaybackIntegrity.ComputeNoteFingerprint(track);
        return track;
    }

    private static ChartEditorTrack BuildTrack(MusicXmlLoader.MusicXmlPartSummary summary, TheoryArrangementData arrangement)
    {
        string importedName = FirstNonEmpty(summary?.GroupDisplayName, summary?.Name, arrangement?.groupDisplayName, arrangement?.displayName, "Track");
        ChartEditorTrackRole role = ResolveRole(
            summary,
            arrangement?.route,
            arrangement?.instrumentType,
            summary?.Route,
            summary?.InstrumentType,
            importedName,
            arrangement?.groupDisplayName,
            arrangement?.displayName);
        ChartEditorTrack track = new ChartEditorTrack
        {
            id = FirstNonEmpty(summary?.PartId, arrangement?.arrangementId, summary?.GroupId, Guid.NewGuid().ToString("N")),
            importedName = importedName,
            displayName = importedName,
            role = role,
            colorHex = ColorForRole(role),
            tuning = new ChartEditorTuningInfo
            {
                displayName = FirstNonEmpty(summary?.TuningDisplayName, arrangement?.tuningDisplayName),
                stringPitches = summary?.StringTuningPitches != null
                    ? (int[])summary.StringTuningPitches.Clone()
                    : arrangement?.tuningPitches != null ? (int[])arrangement.tuningPitches.Clone() : null
            },
            generatedPart = FromTheoryGeneratedPart(arrangement?.generatedPart, arrangement?.arrangementId, importedName, role),
            tones = FromTheoryToneData(arrangement?.tones),
            notes = new List<ChartEditorNote>(),
            arpeggioGuides = new List<ChartEditorArpeggioGuide>(),
            generatedNotes = FromTheoryGeneratedNotes(arrangement?.generatedNotes)
        };

        List<TheoryNoteData> sourceNotes = arrangement?.notes ?? new List<TheoryNoteData>();
        for (int i = 0; i < sourceNotes.Count; i++)
            track.notes.Add(FromTheoryNoteData(sourceNotes[i], i));

        if (arrangement?.arpeggioGuides != null)
        {
            for (int i = 0; i < arrangement.arpeggioGuides.Count; i++)
            {
                TheoryArpeggioGuideData guide = arrangement.arpeggioGuides[i];
                if (guide == null)
                    continue;

                track.arpeggioGuides.Add(new ChartEditorArpeggioGuide
                {
                    id = guide.id,
                    startTime = guide.startTime,
                    endTime = guide.endTime,
                    chordName = guide.chordName ?? string.Empty,
                    stringFrets = guide.stringFrets != null ? (int[])guide.stringFrets.Clone() : null
                });
            }
        }

        track.notes = track.notes
            .Where(note => note != null)
            .OrderBy(note => note.timeSeconds)
            .ThenBy(note => note.stringOrLane)
            .ToList();
        track.EnsureDefaults();
        if (track.generatedNotes.Count > 0)
            track.generatedPlaybackNoteFingerprint = ChartEditorGeneratedPlaybackIntegrity.ComputeNoteFingerprint(track);
        return track;
    }

    private static ChartEditorToneData FromCachedToneData(RocksmithCachedArrangementToneData source, string arrangementRoute)
    {
        ChartEditorToneData result = new ChartEditorToneData
        {
            baseToneName = source?.baseToneName ?? string.Empty,
            changes = new List<ChartEditorToneChange>(),
            definitions = new List<ChartEditorToneDefinition>()
        };

        if (source?.changes != null)
        {
            for (int i = 0; i < source.changes.Count; i++)
            {
                RocksmithCachedToneChangeData change = source.changes[i];
                if (change == null)
                    continue;

                result.changes.Add(new ChartEditorToneChange
                {
                    timeSeconds = Mathf.Max(0f, change.timeSeconds),
                    toneName = change.toneName ?? string.Empty,
                    toneId = change.toneId
                });
            }
        }

        if (source?.definitions != null)
        {
            for (int i = 0; i < source.definitions.Count; i++)
            {
                RocksmithCachedToneDefinitionData definition = source.definitions[i];
                if (definition == null)
                    continue;

                result.definitions.Add(new ChartEditorToneDefinition
                {
                    name = definition.name ?? string.Empty,
                    key = definition.key ?? string.Empty,
                    preset = FromCachedToneDefinition(definition, arrangementRoute),
                    fallback = FromCachedToneFallback(definition, arrangementRoute)
                });
            }
        }

        result.EnsureDefaults();
        return result;
    }

    private static ChartEditorToneData FromTheoryToneData(TheoryToneData source)
    {
        ChartEditorToneData result = new ChartEditorToneData
        {
            baseToneName = source?.baseToneName ?? string.Empty,
            changes = new List<ChartEditorToneChange>(),
            definitions = new List<ChartEditorToneDefinition>()
        };

        if (source?.changes != null)
        {
            for (int i = 0; i < source.changes.Count; i++)
            {
                TheoryToneChangeData change = source.changes[i];
                if (change == null)
                    continue;

                result.changes.Add(new ChartEditorToneChange
                {
                    timeSeconds = Mathf.Max(0f, change.timeSeconds),
                    toneName = change.toneName ?? string.Empty,
                    toneId = change.toneId
                });
            }
        }

        if (source?.definitions != null)
        {
            for (int i = 0; i < source.definitions.Count; i++)
            {
                TheoryToneDefinitionData definition = source.definitions[i];
                if (definition == null)
                    continue;

                result.definitions.Add(new ChartEditorToneDefinition
                {
                    name = definition.name ?? string.Empty,
                    key = definition.key ?? string.Empty,
                    preset = FromTheoryTonePreset(definition.preset),
                    fallback = FromTheoryToneFallback(definition.fallback)
                });
            }
        }

        result.EnsureDefaults();
        return result;
    }

    private static ChartEditorTonePresetData FromCachedToneDefinition(RocksmithCachedToneDefinitionData source, string arrangementRoute)
    {
        if (source == null)
            return new ChartEditorTonePresetData();

        if (TryParseToneLabPreset(source.rawJson, out UnityToneLabRuntime.ToneLabPreset serializedPreset))
            return FromUnityToneLabPreset(serializedPreset, source.name, source.key);

        if (RocksmithTonePresetBuilder.TryBuildPreset(source.name, arrangementRoute, source.rawJson, out UnityToneLabRuntime.ToneLabPreset convertedPreset))
            return FromUnityToneLabPreset(convertedPreset, source.name, source.key);

        return new ChartEditorTonePresetData
        {
            presetId = BuildNeutralTonePresetId(source.name, source.key),
            presetName = source.name ?? source.key ?? string.Empty
        };
    }

    private static ChartEditorToneFallbackData FromCachedToneFallback(RocksmithCachedToneDefinitionData source, string arrangementRoute)
    {
        if (source == null)
            return new ChartEditorToneFallbackData();

        return new ChartEditorToneFallbackData
        {
            preferredPresetName = FirstNonEmpty(source.preferredPresetName, source.name, source.key),
            searchText = BuildToneFallbackSearchText(
                source.fallbackSearchText,
                source.name,
                source.key,
                arrangementRoute)
        };
    }

    private static ChartEditorToneFallbackData FromTheoryToneFallback(TheoryToneFallbackData source)
    {
        return new ChartEditorToneFallbackData
        {
            preferredPresetName = source?.preferredPresetName ?? string.Empty,
            searchText = source?.searchText ?? string.Empty
        };
    }

    private static ChartEditorTonePresetData FromTheoryTonePreset(TheoryTonePresetData source)
    {
        ChartEditorTonePresetData result = new ChartEditorTonePresetData
        {
            presetId = source?.presetId ?? string.Empty,
            presetName = source?.presetName ?? string.Empty,
            inputGainDb = source?.inputGainDb ?? 0f,
            outputGainDb = source?.outputGainDb ?? 0f,
            pedalChain = new List<ChartEditorTonePedalSlotData>()
        };

        if (source?.pedalChain != null)
        {
            for (int i = 0; i < source.pedalChain.Count; i++)
            {
                TheoryTonePedalSlotData slot = source.pedalChain[i];
                if (slot == null)
                    continue;

                result.pedalChain.Add(new ChartEditorTonePedalSlotData
                {
                    instanceId = slot.instanceId ?? string.Empty,
                    pedalType = slot.pedalType ?? string.Empty,
                    descriptorId = slot.descriptorId ?? string.Empty,
                    enabled = slot.enabled,
                    settingsJson = slot.settingsJson ?? string.Empty
                });
            }
        }

        result.EnsureDefaults();
        return result;
    }

    private static ChartEditorTonePresetData FromUnityToneLabPreset(UnityToneLabRuntime.ToneLabPreset source, string fallbackName, string fallbackKey)
    {
        ChartEditorTonePresetData result = new ChartEditorTonePresetData
        {
            presetId = RocksmithTonePresetBuilder.IsGeneratedPresetId(source?.preset_id)
                ? BuildNeutralTonePresetId(fallbackName, fallbackKey)
                : FirstNonEmpty(source?.preset_id, BuildNeutralTonePresetId(fallbackName, fallbackKey)),
            presetName = FirstNonEmpty(source?.preset_name, fallbackName, fallbackKey),
            inputGainDb = source?.input_gain_db ?? 0f,
            outputGainDb = source?.output_gain_db ?? 0f,
            pedalChain = new List<ChartEditorTonePedalSlotData>()
        };

        if (source?.pedal_chain != null)
        {
            for (int i = 0; i < source.pedal_chain.Count; i++)
            {
                UnityToneLabRuntime.ToneLabPedalSlot slot = source.pedal_chain[i];
                if (slot == null)
                    continue;

                result.pedalChain.Add(new ChartEditorTonePedalSlotData
                {
                    instanceId = slot.pedal_instance_id ?? string.Empty,
                    pedalType = slot.pedal_type.ToString(),
                    descriptorId = slot.descriptor_id ?? string.Empty,
                    enabled = slot.enabled,
                    settingsJson = slot.settings_json ?? string.Empty
                });
            }
        }

        result.EnsureDefaults();
        return result;
    }

    private static bool TryParseToneLabPreset(string json, out UnityToneLabRuntime.ToneLabPreset preset)
    {
        preset = null;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            UnityToneLabRuntime.ToneLabPreset parsed = JsonUtility.FromJson<UnityToneLabRuntime.ToneLabPreset>(json);
            if (parsed == null || parsed.pedal_chain == null || parsed.pedal_chain.Count == 0)
                return false;

            preset = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildNeutralTonePresetId(string name, string key)
    {
        string seed = FirstNonEmpty(key, name, "tone");
        char[] chars = seed.Trim().ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        }

        string normalized = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "tone" : $"tone_{normalized}";
    }

    private static string BuildToneFallbackSearchText(params string[] values)
    {
        if (values == null)
            return string.Empty;

        List<string> tokens = new List<string>();
        for (int i = 0; i < values.Length; i++)
        {
            string value = values[i];
            if (string.IsNullOrWhiteSpace(value))
                continue;

            string trimmed = value.Trim();
            if (tokens.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
                continue;

            tokens.Add(trimmed);
        }

        return string.Join(" ", tokens);
    }

    private static ChartEditorGeneratedPartInfo FromCachedGeneratedPart(
        RocksmithCachedGeneratedPartInfo source,
        string fallbackPartId,
        string fallbackName,
        ChartEditorTrackRole role)
    {
        if (source == null)
            return CreateDefaultGeneratedPart(fallbackPartId, fallbackName, role);

        return new ChartEditorGeneratedPartInfo
        {
            partId = source.partId ?? fallbackPartId,
            displayName = source.displayName ?? fallbackName,
            instrumentName = source.instrumentName ?? InstrumentNameForRole(role),
            sourceMidiChannel = source.sourceMidiChannel,
            sourceMidiProgram = source.sourceMidiProgram,
            preferredBank = source.preferredBank,
            isDrum = source.isDrum,
            isGuitarFamily = source.isGuitarFamily,
            isExplicitHarmonicPart = source.isExplicitHarmonicPart
        };
    }

    private static ChartEditorGeneratedPartInfo FromTheoryGeneratedPart(
        TheoryGeneratedPartInfo source,
        string fallbackPartId,
        string fallbackName,
        ChartEditorTrackRole role)
    {
        if (source == null)
            return CreateDefaultGeneratedPart(fallbackPartId, fallbackName, role);

        return new ChartEditorGeneratedPartInfo
        {
            partId = source.partId ?? fallbackPartId,
            displayName = source.displayName ?? fallbackName,
            instrumentName = source.instrumentName ?? InstrumentNameForRole(role),
            sourceMidiChannel = source.sourceMidiChannel,
            sourceMidiProgram = source.sourceMidiProgram,
            preferredBank = source.preferredBank,
            isDrum = source.isDrum,
            isGuitarFamily = source.isGuitarFamily,
            isExplicitHarmonicPart = source.isExplicitHarmonicPart
        };
    }

    private static ChartEditorGeneratedPartInfo CreateDefaultGeneratedPart(
        string fallbackPartId,
        string fallbackName,
        ChartEditorTrackRole role)
    {
        return new ChartEditorGeneratedPartInfo
        {
            partId = fallbackPartId ?? string.Empty,
            displayName = fallbackName ?? string.Empty,
            instrumentName = InstrumentNameForRole(role),
            sourceMidiChannel = role == ChartEditorTrackRole.Drums ? 9 : 0,
            sourceMidiProgram = DefaultMidiProgramForRole(role),
            preferredBank = -1,
            isDrum = role == ChartEditorTrackRole.Drums,
            isGuitarFamily = IsGuitarFamilyRole(role)
        };
    }

    private static string InstrumentNameForRole(ChartEditorTrackRole role)
    {
        switch (role)
        {
            case ChartEditorTrackRole.Bass:
                return "Bass";
            case ChartEditorTrackRole.Drums:
                return "Drums";
            case ChartEditorTrackRole.Piano:
                return "Piano";
            case ChartEditorTrackRole.Vocals:
                return "Vocals";
            default:
                return "Guitar";
        }
    }

    private static int DefaultMidiProgramForRole(ChartEditorTrackRole role)
    {
        switch (role)
        {
            case ChartEditorTrackRole.Bass:
                return 33;
            case ChartEditorTrackRole.Piano:
                return 0;
            default:
                return 29;
        }
    }

    private static bool IsGuitarFamilyRole(ChartEditorTrackRole role)
    {
        return role == ChartEditorTrackRole.LeadGuitar ||
               role == ChartEditorTrackRole.RhythmGuitar ||
               role == ChartEditorTrackRole.Bass ||
               role == ChartEditorTrackRole.Custom;
    }

    private static List<ChartEditorGeneratedNoteEvent> FromCachedGeneratedNotes(List<RocksmithCachedGeneratedNoteEvent> source)
    {
        List<ChartEditorGeneratedNoteEvent> result = new List<ChartEditorGeneratedNoteEvent>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            RocksmithCachedGeneratedNoteEvent note = source[i];
            if (note == null)
                continue;

            result.Add(CloneGeneratedNote(
                note.startTimeSeconds,
                note.durationSeconds,
                note.pitchPreRollSeconds,
                note.midiNote,
                note.velocity,
                note.channel,
                note.partId,
                note.partName,
                note.techniqueVariant,
                note.legatoTransitionKind,
                note.attackVelocityScale,
                note.vibratoDepthSemitones,
                note.vibratoRateHz,
                note.vibratoDelayNormalized,
                note.vibratoFadeNormalized,
                note.pitchBendRangeSemitones,
                note.pitchCurve?.Select(point => point == null
                    ? null
                    : new ChartEditorGeneratedPitchPoint
                    {
                        normalizedTime = point.normalizedTime,
                        semitoneOffset = point.semitoneOffset
                    })));
        }

        return result;
    }

    private static List<ChartEditorGeneratedNoteEvent> FromTheoryGeneratedNotes(List<TheoryGeneratedNoteEvent> source)
    {
        List<ChartEditorGeneratedNoteEvent> result = new List<ChartEditorGeneratedNoteEvent>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            TheoryGeneratedNoteEvent note = source[i];
            if (note == null)
                continue;

            result.Add(CloneGeneratedNote(
                note.startTimeSeconds,
                note.durationSeconds,
                note.pitchPreRollSeconds,
                note.midiNote,
                note.velocity,
                note.channel,
                note.partId,
                note.partName,
                note.techniqueVariant,
                note.legatoTransitionKind,
                note.attackVelocityScale,
                note.vibratoDepthSemitones,
                note.vibratoRateHz,
                note.vibratoDelayNormalized,
                note.vibratoFadeNormalized,
                note.pitchBendRangeSemitones,
                note.pitchCurve?.Select(point => point == null
                    ? null
                    : new ChartEditorGeneratedPitchPoint
                    {
                        normalizedTime = point.normalizedTime,
                        semitoneOffset = point.semitoneOffset
                    })));
        }

        return result;
    }

    private static ChartEditorGeneratedNoteEvent CloneGeneratedNote(
        float startTimeSeconds,
        float durationSeconds,
        float pitchPreRollSeconds,
        int midiNote,
        int velocity,
        int channel,
        string partId,
        string partName,
        int techniqueVariant,
        int legatoTransitionKind,
        float attackVelocityScale,
        float vibratoDepthSemitones,
        float vibratoRateHz,
        float vibratoDelayNormalized,
        float vibratoFadeNormalized,
        int pitchBendRangeSemitones,
        IEnumerable<ChartEditorGeneratedPitchPoint> pitchCurve)
    {
        ChartEditorGeneratedNoteEvent result = new ChartEditorGeneratedNoteEvent
        {
            startTimeSeconds = startTimeSeconds,
            durationSeconds = durationSeconds,
            pitchPreRollSeconds = pitchPreRollSeconds,
            midiNote = midiNote,
            velocity = velocity,
            channel = channel,
            partId = partId,
            partName = partName,
            techniqueVariant = techniqueVariant,
            legatoTransitionKind = legatoTransitionKind,
            attackVelocityScale = attackVelocityScale,
            vibratoDepthSemitones = vibratoDepthSemitones,
            vibratoRateHz = vibratoRateHz,
            vibratoDelayNormalized = vibratoDelayNormalized,
            vibratoFadeNormalized = vibratoFadeNormalized,
            pitchBendRangeSemitones = pitchBendRangeSemitones,
            pitchCurve = pitchCurve?
                .Where(point => point != null)
                .Select(point => new ChartEditorGeneratedPitchPoint
                {
                    normalizedTime = point.normalizedTime,
                    semitoneOffset = point.semitoneOffset
                })
                .ToList() ?? new List<ChartEditorGeneratedPitchPoint>()
        };
        result.EnsureDefaults();
        return result;
    }

    private static ChartEditorNote FromNoteData(NoteData source, int fallbackIndex)
    {
        ChartEditorNote note = new ChartEditorNote
        {
            id = source.id >= 0 ? $"note_{source.id}" : Guid.NewGuid().ToString("N"),
            sourceNoteId = source.id,
            chartTimeSeconds = Math.Max(0.0, source.time),
            timeSeconds = Math.Max(0.0, source.time),
            durationSeconds = Math.Max(0.0, source.duration),
            stringOrLane = Mathf.Clamp(source.stringIdx, 0, 7),
            fret = Mathf.Max(0, source.fret),
            velocity = 95,
            noteName = source.note ?? string.Empty,
            chordId = source.chordId,
            chordName = source.chordName ?? string.Empty,
            technique = source.technique,
            slideTargetFret = source.slideTargetFret,
            bendStep = source.bendStep,
            bendVisualStartTime = source.bendVisualStartTime,
            bendVisualDuration = source.bendVisualDuration,
            bendPreBend = source.bendPreBend,
            bendRelease = source.bendRelease,
            muted = source.isMuted,
            palmMute = source.isMuted,
            fretHandMute = false,
            maxBend = Mathf.Max(0f, source.bendStep),
            legato = source.isLegato,
            requiresPluck = source.requiresPluck,
            linkedFromNoteId = source.linkedFromNoteId,
            bendPoints = new List<ChartEditorBendPoint>(),
            techniqueSegments = new List<ChartEditorTechniqueSegment>()
        };

        if (source.techniqueSegments != null)
        {
            for (int i = 0; i < source.techniqueSegments.Count; i++)
            {
                NoteTechniqueSegmentData segment = source.techniqueSegments[i];
                note.techniqueSegments.Add(new ChartEditorTechniqueSegment
                {
                    type = segment.type,
                    startOffset = segment.startOffset,
                    endOffset = segment.endOffset,
                    startFret = segment.startFret,
                    endFret = segment.endFret,
                    startBend = segment.startBend,
                    endBend = segment.endBend
                });
            }
        }

        if (string.IsNullOrWhiteSpace(note.id))
            note.id = $"note_{fallbackIndex}";
        note.EnsureDefaults();
        return note;
    }

    private static ChartEditorNote FromCachedNoteData(RocksmithCachedNoteData source, int fallbackIndex)
    {
        if (source == null)
            return null;

        bool palmMute = source.isPalmMute;
        bool fretHandMute = source.isFretHandMute;
        bool muted = source.isMuted || palmMute || fretHandMute;
        ChartEditorNote note = new ChartEditorNote
        {
            id = source.id >= 0 ? $"note_{source.id}" : Guid.NewGuid().ToString("N"),
            sourceNoteId = source.id,
            chartTimeSeconds = Math.Max(0.0, source.time),
            timeSeconds = Math.Max(0.0, source.time),
            durationSeconds = Math.Max(0.0, source.duration),
            stringOrLane = Mathf.Clamp(source.stringIdx, 0, 7),
            fret = Mathf.Max(0, source.fret),
            velocity = 95,
            noteName = source.note ?? string.Empty,
            chordId = source.chordId,
            chordName = source.chordName ?? string.Empty,
            technique = ResolveCachedPrimaryTechnique(source),
            slideTargetFret = source.slideTargetFret,
            bendStep = source.bendStep,
            bendVisualStartTime = source.bendVisualStartTime,
            bendVisualDuration = source.bendVisualDuration,
            bendPreBend = source.bendPreBend,
            bendRelease = source.bendRelease,
            muted = muted,
            palmMute = palmMute || (muted && !fretHandMute && !palmMute),
            fretHandMute = fretHandMute,
            harmonic = source.isHarmonic,
            accent = source.isAccent,
            tap = source.isTap,
            tremolo = source.isTremolo,
            pinchHarmonic = source.isPinchHarmonic,
            vibratoStrength = source.vibratoStrength,
            maxBend = Mathf.Max(source.maxBend, source.bendStep),
            legato = source.isLegato,
            requiresPluck = source.requiresPluck,
            linkedFromNoteId = source.linkedFromNoteId,
            bendPoints = new List<ChartEditorBendPoint>(),
            techniqueSegments = new List<ChartEditorTechniqueSegment>()
        };

        if (source.bendPoints != null)
        {
            for (int i = 0; i < source.bendPoints.Count; i++)
            {
                RocksmithCachedBendPointData point = source.bendPoints[i];
                if (point == null)
                    continue;

                note.bendPoints.Add(new ChartEditorBendPoint
                {
                    timeSeconds = point.timeSeconds,
                    step = point.step
                });
            }
        }

        List<NoteTechniqueSegmentData> normalizedSegments = RocksmithCachedSongLoader.BuildNormalizedTechniqueSegments(source);
        if (normalizedSegments != null)
        {
            for (int i = 0; i < normalizedSegments.Count; i++)
            {
                NoteTechniqueSegmentData segment = normalizedSegments[i];
                note.techniqueSegments.Add(new ChartEditorTechniqueSegment
                {
                    type = segment.type,
                    startOffset = segment.startOffset,
                    endOffset = segment.endOffset,
                    startFret = segment.startFret,
                    endFret = segment.endFret,
                    startBend = segment.startBend,
                    endBend = segment.endBend
                });
            }
        }

        if (string.IsNullOrWhiteSpace(note.id))
            note.id = $"note_{fallbackIndex}";
        note.EnsureDefaults();
        return note;
    }

    private static ChartEditorNote FromTheoryNoteData(TheoryNoteData source, int fallbackIndex)
    {
        if (source == null)
            return null;

        bool muted = source.muted || source.palmMute || source.fretHandMute;
        ChartEditorNote note = new ChartEditorNote
        {
            id = source.id >= 0 ? $"note_{source.id}" : Guid.NewGuid().ToString("N"),
            sourceNoteId = source.id,
            chartTimeSeconds = Math.Max(0.0, source.time),
            timeSeconds = Math.Max(0.0, source.time),
            durationSeconds = Math.Max(0.0, source.duration),
            stringOrLane = Mathf.Clamp(source.stringIndex, 0, 7),
            fret = Mathf.Max(0, source.fret),
            velocity = 95,
            noteName = source.noteName ?? string.Empty,
            chordId = source.chordId,
            chordName = source.chordName ?? string.Empty,
            technique = ResolveTheoryPrimaryTechnique(source),
            slideTargetFret = source.slideTargetFret,
            bendStep = source.bendStep,
            bendVisualStartTime = source.bendVisualStartTime,
            bendVisualDuration = source.bendVisualDuration,
            bendPreBend = source.bendPreBend,
            bendRelease = source.bendRelease,
            muted = muted,
            palmMute = source.palmMute || (muted && !source.fretHandMute && !source.palmMute),
            fretHandMute = source.fretHandMute,
            harmonic = source.harmonic,
            accent = source.accent,
            tap = source.tap,
            tremolo = source.tremolo,
            pinchHarmonic = source.pinchHarmonic,
            vibratoStrength = source.vibratoStrength,
            maxBend = Mathf.Max(source.maxBend, source.bendStep),
            legato = source.legato || source.hammerOn || source.pullOff || source.hopo,
            requiresPluck = source.requiresPluck,
            linkedFromNoteId = source.linkedFromNoteId,
            bendPoints = new List<ChartEditorBendPoint>(),
            techniqueSegments = new List<ChartEditorTechniqueSegment>()
        };

        if (source.bendPoints != null)
        {
            for (int i = 0; i < source.bendPoints.Count; i++)
            {
                TheoryBendPointData point = source.bendPoints[i];
                if (point == null)
                    continue;

                note.bendPoints.Add(new ChartEditorBendPoint
                {
                    timeSeconds = point.timeSeconds,
                    step = point.step
                });
            }
        }

        if (source.techniqueSegments != null)
        {
            for (int i = 0; i < source.techniqueSegments.Count; i++)
            {
                TheoryTechniqueSegmentData segment = source.techniqueSegments[i];
                if (segment == null)
                    continue;

                note.techniqueSegments.Add(new ChartEditorTechniqueSegment
                {
                    type = (NoteTechniqueSegmentType)Mathf.Clamp(segment.type, 0, (int)NoteTechniqueSegmentType.Vibrato),
                    startOffset = segment.startOffset,
                    endOffset = segment.endOffset,
                    startFret = segment.startFret,
                    endFret = segment.endFret,
                    startBend = segment.startBend,
                    endBend = segment.endBend
                });
            }
        }

        if (string.IsNullOrWhiteSpace(note.id))
            note.id = $"note_{fallbackIndex}";
        note.EnsureDefaults();
        return note;
    }

    private static NoteTechnique ResolveCachedPrimaryTechnique(RocksmithCachedNoteData source)
    {
        if (source == null)
            return NoteTechnique.None;
        if (source.isHammerOn)
            return NoteTechnique.HammerOn;
        if (source.isPullOff)
            return NoteTechnique.PullOff;
        return (NoteTechnique)Mathf.Clamp(source.technique, (int)NoteTechnique.None, (int)NoteTechnique.Vibrato);
    }

    private static NoteTechnique ResolveTheoryPrimaryTechnique(TheoryNoteData source)
    {
        if (source == null)
            return NoteTechnique.None;
        if (source.hammerOn)
            return NoteTechnique.HammerOn;
        if (source.pullOff)
            return NoteTechnique.PullOff;
        return (NoteTechnique)Mathf.Clamp(source.primaryTechnique, (int)NoteTechnique.None, (int)NoteTechnique.Vibrato);
    }

    private static void ImportTheoryTiming(string packagePath, TheorySongManifest manifest, ChartEditorProject project)
    {
        if (project == null || manifest?.arrangements == null)
            return;

        TheoryArrangementData timingArrangement = null;
        for (int i = 0; i < manifest.arrangements.Count; i++)
        {
            TheoryArrangementSummary summary = manifest.arrangements[i];
            if (summary == null)
                continue;

            if (!string.IsNullOrWhiteSpace(manifest.defaultArrangementId) &&
                !string.Equals(summary.arrangementId ?? string.Empty, manifest.defaultArrangementId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TheoryPackageIO.TryReadArrangement(packagePath, summary, out timingArrangement, out _) && timingArrangement?.timing != null)
                break;
        }

        if (timingArrangement == null)
        {
            for (int i = 0; i < manifest.arrangements.Count; i++)
            {
                TheoryArrangementSummary summary = manifest.arrangements[i];
                if (TheoryPackageIO.TryReadArrangement(packagePath, summary, out timingArrangement, out _) && timingArrangement?.timing != null)
                    break;
            }
        }

        TheoryTimingData timing = timingArrangement?.timing;
        if (timing == null)
            return;

        project.beatMap.defaultTempoBpm = Math.Max(1.0, timing.averageTempoBpm);

        if (timing.sections != null && timing.sections.Count > 0)
        {
            BuildSectionsFromStartMarkers(
                project,
                timing.sections
                    .Where(section => section != null && section.timeSeconds >= 0f)
                    .Select((section, index) => new SectionMarker(section.name, section.timeSeconds, index)));
        }

        List<TheoryBeatData> beats = timing.beats?
            .Where(beat => beat != null && beat.timeSeconds >= 0f)
            .OrderBy(beat => beat.timeSeconds)
            .ToList();
        if (beats == null || beats.Count == 0 || project.beatMap == null)
            return;

        BuildImportedTempoAnchors(
            project,
            beats.Select(beat => Math.Max(0.0, (double)beat.timeSeconds)).ToList(),
            project.beatMap.defaultTempoBpm,
            "theory_beat");

        project.beatMap.timeSignatures.Clear();
        project.beatMap.timeSignatures.Add(new ChartEditorTimeSignatureChange
        {
            beatPosition = 0.0,
            numerator = 4,
            denominator = 4
        });
    }

    private static void ApplyTheoryEditorState(string packagePath, ChartEditorProject project)
    {
        if (project == null ||
            !TheoryPackageIO.TryReadEditorState(packagePath, out TheoryEditorState state, out _) ||
            state == null)
        {
            return;
        }

        project.cursorTimeSeconds = Math.Max(0.0, state.cursorTimeSeconds);
        project.selectedTrackId = state.selectedArrangementId ?? project.selectedTrackId;
        project.settings.snapSeconds = Math.Max(0.001, state.snapSeconds);
        project.settings.snapEnabled = state.snapEnabled;
        project.settings.largeNudgeSeconds = Math.Max(0.001, state.largeNudgeSeconds);
        project.settings.smallNudgeSeconds = Math.Max(0.001, state.smallNudgeSeconds);
        project.settings.showBeatGrid = state.showBeatGrid;
        project.settings.metronomeEnabled = state.metronomeEnabled;
        project.settings.noteClapsEnabled = state.noteClapsEnabled;
        project.settings.playbackSpeed = Mathf.Clamp(state.playbackSpeed <= 0f ? 1f : state.playbackSpeed, 0.25f, 2f);

        if (state.beatMarkers != null && state.beatMarkers.Count > 0 && project.beatMap != null)
        {
            project.beatMap.beatMarkers.Clear();
            List<TheoryEditorBeatMarker> markers = state.beatMarkers
                .Where(marker => marker != null)
                .OrderBy(marker => marker.beatPosition)
                .ToList();

            for (int i = 0; i < markers.Count; i++)
            {
                TheoryEditorBeatMarker marker = markers[i];
                project.beatMap.beatMarkers.Add(new ChartEditorBeatMarker
                {
                    id = string.IsNullOrWhiteSpace(marker.id) ? Guid.NewGuid().ToString("N") : marker.id,
                    index = i,
                    beatPosition = Math.Max(0.0, marker.beatPosition),
                    audioTimeSeconds = Math.Max(0.0, marker.audioTimeSeconds),
                    barNumber = Math.Max(0, marker.barNumber),
                    isDownbeat = marker.isDownbeat,
                    isAnchor = marker.isAnchor,
                    label = marker.label ?? string.Empty,
                    bpm = Math.Max(0.0, marker.bpm),
                    generatedBySynchTheory = marker.generatedBySynchTheory,
                    synchTheoryConfidence = Math.Max(0.0, marker.synchTheoryConfidence),
                    synchTheorySource = marker.synchTheorySource ?? string.Empty
                });
            }

            project.beatMap.timeSignatures.Clear();
            int lastNumerator = -1;
            int lastDenominator = -1;
            foreach (TheoryEditorBeatMarker marker in markers)
            {
                int numerator = Math.Max(1, marker.timeSignatureNumerator);
                int denominator = Math.Max(1, marker.timeSignatureDenominator);
                if (numerator == lastNumerator && denominator == lastDenominator)
                    continue;

                project.beatMap.timeSignatures.Add(new ChartEditorTimeSignatureChange
                {
                    beatPosition = Math.Max(0.0, marker.beatPosition),
                    numerator = numerator,
                    denominator = denominator
                });
                lastNumerator = numerator;
                lastDenominator = denominator;
            }

            if (project.beatMap.timeSignatures.Count == 0)
            {
                project.beatMap.timeSignatures.Add(new ChartEditorTimeSignatureChange
                {
                    beatPosition = 0.0,
                    numerator = 4,
                    denominator = 4
                });
            }
        }

        if (state.syncPoints != null)
        {
            project.syncPoints.Clear();
            for (int i = 0; i < state.syncPoints.Count; i++)
            {
                TheoryEditorSyncPoint point = state.syncPoints[i];
                if (point == null)
                    continue;

                project.syncPoints.Add(new ChartEditorSyncPoint
                {
                    id = string.IsNullOrWhiteSpace(point.id) ? Guid.NewGuid().ToString("N") : point.id,
                    chartTimeSeconds = Math.Max(0.0, point.chartTimeSeconds),
                    audioTimeSeconds = Math.Max(0.0, point.audioTimeSeconds),
                    name = point.label ?? string.Empty
                });
            }
        }
    }

    private static void ImportArrangementSections(string manifestPath, ChartEditorProject project)
    {
        if (!RocksmithCachedSongLoader.TryLoadManifest(manifestPath, out RocksmithCachedSongManifest manifest) ||
            manifest?.arrangements == null)
        {
            return;
        }

        RocksmithCachedArrangementPart partWithSections = null;
        for (int i = 0; i < manifest.arrangements.Count; i++)
        {
            if (RocksmithCachedSongLoader.TryLoadArrangementPart(manifestPath, i, out _, out RocksmithCachedArrangementPart part) &&
                part?.timing?.sections != null &&
                part.timing.sections.Count > 0)
            {
                partWithSections = part;
                break;
            }
        }

        if (partWithSections?.timing?.sections == null)
            return;

        BuildSectionsFromStartMarkers(
            project,
            partWithSections.timing.sections
                .Where(section => section != null && section.timeSeconds >= 0f)
                .Select((section, index) => new SectionMarker(section.name, section.timeSeconds, index)));
    }

    private static void ImportArrangementBeatMap(string manifestPath, ChartEditorProject project)
    {
        if (project?.beatMap == null ||
            !RocksmithCachedSongLoader.TryLoadManifest(manifestPath, out RocksmithCachedSongManifest manifest) ||
            manifest?.arrangements == null)
        {
            return;
        }

        RocksmithCachedArrangementPart partWithTiming = null;
        for (int i = 0; i < manifest.arrangements.Count; i++)
        {
            if (RocksmithCachedSongLoader.TryLoadArrangementPart(manifestPath, i, out _, out RocksmithCachedArrangementPart part) &&
                part?.timing?.ebeats != null &&
                part.timing.ebeats.Count > 0)
            {
                partWithTiming = part;
                break;
            }
        }

        List<RocksmithCachedEbeatData> ebeats = partWithTiming?.timing?.ebeats?
            .Where(ebeat => ebeat != null && ebeat.timeSeconds >= 0f)
            .OrderBy(ebeat => ebeat.timeSeconds)
            .ToList();
        if (ebeats == null || ebeats.Count == 0)
            return;

        project.beatMap.defaultTempoBpm = Math.Max(1.0, partWithTiming.timing.averageTempoBpm);
        BuildImportedTempoAnchors(
            project,
            ebeats.Select(ebeat => Math.Max(0.0, (double)ebeat.timeSeconds)).ToList(),
            project.beatMap.defaultTempoBpm,
            "imported_anchor");

        project.beatMap.timeSignatures.Clear();
        project.beatMap.timeSignatures.Add(new ChartEditorTimeSignatureChange
        {
            beatPosition = 0.0,
            numerator = 4,
            denominator = 4
        });
    }

    private static void ImportGpSections(string path, ChartEditorProject project)
    {
        Gp5Song song = Gp5Loader.GetParsedSong(path);
        if (song?.measureHeaders == null || song.measureHeaders.Count == 0)
            return;

        List<GpTempoPoint> tempoMap = BuildGpTempoMap(song);
        BuildSectionsFromStartMarkers(
            project,
            song.measureHeaders
                .Where(header => header != null && !string.IsNullOrWhiteSpace(header.markerName))
                .Select((header, index) => new SectionMarker(
                    header.markerName,
                    (float)GpQuarterToSeconds(header.startQuarter, tempoMap),
                    index)));
    }

    private static void ImportGpBeatMap(string path, ChartEditorProject project)
    {
        try
        {
            Gp5Song song = Gp5Loader.GetParsedSong(path);
            if (song == null || project?.beatMap == null)
                return;

            List<GpTempoPoint> tempoMap = BuildGpTempoMap(song);
            if (tempoMap.Count > 0)
                project.beatMap.defaultTempoBpm = Math.Max(1.0, tempoMap[0].bpm);

            project.beatMap.beatMarkers.Clear();
            foreach (GpTempoPoint tempoPoint in tempoMap
                         .GroupBy(point => Math.Round(point.quarterPos, 4))
                         .Select(group => group.First())
                         .OrderBy(point => point.quarterPos))
            {
                double beatPosition = Math.Max(0.0, tempoPoint.quarterPos);
                bool tempoChangeAnchor = beatPosition > 0.0001;
                project.beatMap.beatMarkers.Add(new ChartEditorBeatMarker
                {
                    id = Guid.NewGuid().ToString("N"),
                    beatPosition = beatPosition,
                    audioTimeSeconds = Math.Max(0.0, GpQuarterToSeconds(beatPosition, tempoMap)),
                    isAnchor = tempoChangeAnchor,
                    label = beatPosition <= 0.0001 ? "Start" : $"{tempoPoint.bpm:0.###} BPM",
                    bpm = Math.Max(1.0, tempoPoint.bpm)
                });
            }

            if (!project.beatMap.beatMarkers.Any(marker => marker != null && Math.Abs(marker.beatPosition) <= 0.0001))
            {
                project.beatMap.beatMarkers.Insert(0, new ChartEditorBeatMarker
                {
                    id = Guid.NewGuid().ToString("N"),
                    beatPosition = 0.0,
                    audioTimeSeconds = 0.0,
                    isAnchor = false,
                    label = string.Empty,
                    bpm = Math.Max(1.0, project.beatMap.defaultTempoBpm)
                });
            }

            project.beatMap.timeSignatures.Clear();
            int lastNumerator = -1;
            int lastDenominator = -1;
            if (song.measureHeaders != null)
            {
                foreach (Gp5MeasureHeader header in song.measureHeaders.OrderBy(header => header.startQuarter))
                {
                    if (header == null)
                        continue;

                    int numerator = Math.Max(1, header.numerator);
                    int denominator = Math.Max(1, header.denominator);
                    if (numerator == lastNumerator && denominator == lastDenominator)
                        continue;

                    project.beatMap.timeSignatures.Add(new ChartEditorTimeSignatureChange
                    {
                        beatPosition = Math.Max(0.0, header.startQuarter),
                        numerator = numerator,
                        denominator = denominator
                    });
                    lastNumerator = numerator;
                    lastDenominator = denominator;
                }
            }

            if (project.beatMap.timeSignatures.Count == 0)
            {
                project.beatMap.timeSignatures.Add(new ChartEditorTimeSignatureChange
                {
                    beatPosition = 0.0,
                    numerator = 4,
                    denominator = 4
                });
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ChartEditor] Failed to import GP beat map from '{path}': {ex.Message}");
        }
    }

    private static void ImportMusicXmlSections(string path, ChartEditorProject project)
    {
        try
        {
            XDocument doc = XDocument.Load(path);
            XElement firstPart = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "part");
            if (firstPart == null)
                return;

            List<SectionMarker> markers = new List<SectionMarker>();
            double currentQuarter = 0.0;
            double seconds = 0.0;
            double bpm = 120.0;
            int divisions = 1;
            int index = 0;

            foreach (XElement measure in firstPart.Elements().Where(e => e.Name.LocalName == "measure"))
            {
                foreach (XElement direction in measure.Elements().Where(e => e.Name.LocalName == "direction"))
                {
                    string words = direction.Descendants().FirstOrDefault(e => e.Name.LocalName == "rehearsal")?.Value;
                    words = FirstNonEmpty(words, direction.Descendants().FirstOrDefault(e => e.Name.LocalName == "words")?.Value);
                    if (!string.IsNullOrWhiteSpace(words))
                        markers.Add(new SectionMarker(words.Trim(), (float)seconds, index++));

                    string soundTempo = direction.Descendants().FirstOrDefault(e => e.Name.LocalName == "sound")?.Attribute("tempo")?.Value;
                    if (double.TryParse(soundTempo, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedTempo) && parsedTempo > 1.0)
                        bpm = parsedTempo;
                }

                XElement attributes = measure.Elements().FirstOrDefault(e => e.Name.LocalName == "attributes");
                string divisionsText = attributes?.Elements().FirstOrDefault(e => e.Name.LocalName == "divisions")?.Value;
                if (int.TryParse(divisionsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedDivisions) && parsedDivisions > 0)
                    divisions = parsedDivisions;

                foreach (XElement note in measure.Elements().Where(e => e.Name.LocalName == "note"))
                {
                    if (note.Elements().Any(e => e.Name.LocalName == "chord"))
                        continue;

                    string durationText = note.Elements().FirstOrDefault(e => e.Name.LocalName == "duration")?.Value;
                    if (!double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out double durationDivisions))
                        continue;

                    double durationQuarter = durationDivisions / Math.Max(1, divisions);
                    currentQuarter += durationQuarter;
                    seconds += durationQuarter * (60.0 / Math.Max(1.0, bpm));
                }
            }

            BuildSectionsFromStartMarkers(project, markers);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ChartEditor] Failed to import MusicXML sections from '{path}': {ex.Message}");
        }
    }

    private static void ImportMusicXmlBeatMap(string path, ChartEditorProject project)
    {
        try
        {
            if (project?.beatMap == null)
                return;

            XDocument doc = XDocument.Load(path);
            XElement firstPart = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "part");
            if (firstPart == null)
                return;

            project.beatMap.beatMarkers.Clear();
            project.beatMap.timeSignatures.Clear();

            double currentQuarter = 0.0;
            double seconds = 0.0;
            double bpm = 120.0;
            double initialBpm = 120.0;
            bool initialBpmSet = false;
            int divisions = 1;
            int numerator = 4;
            int denominator = 4;
            int lastNumerator = -1;
            int lastDenominator = -1;

            void AddTempoAnchor(double beat, double audio, double tempo, string label)
            {
                if (project.beatMap.beatMarkers.Any(marker => marker != null && Math.Abs(marker.beatPosition - beat) <= 0.0001))
                    return;

                bool tempoChangeAnchor = beat > 0.0001;
                project.beatMap.beatMarkers.Add(new ChartEditorBeatMarker
                {
                    id = Guid.NewGuid().ToString("N"),
                    beatPosition = Math.Max(0.0, beat),
                    audioTimeSeconds = Math.Max(0.0, audio),
                    isAnchor = tempoChangeAnchor,
                    label = tempoChangeAnchor ? label : string.Empty,
                    bpm = Math.Max(1.0, tempo)
                });
            }

            void AddSignatureIfChanged()
            {
                if (numerator == lastNumerator && denominator == lastDenominator)
                    return;

                project.beatMap.timeSignatures.Add(new ChartEditorTimeSignatureChange
                {
                    beatPosition = Math.Max(0.0, currentQuarter),
                    numerator = Math.Max(1, numerator),
                    denominator = Math.Max(1, denominator)
                });
                lastNumerator = numerator;
                lastDenominator = denominator;
            }

            AddTempoAnchor(0.0, 0.0, bpm, "Start");
            AddSignatureIfChanged();

            foreach (XElement measure in firstPart.Elements().Where(e => e.Name.LocalName == "measure"))
            {
                XElement attributes = measure.Elements().FirstOrDefault(e => e.Name.LocalName == "attributes");
                string divisionsText = attributes?.Elements().FirstOrDefault(e => e.Name.LocalName == "divisions")?.Value;
                if (int.TryParse(divisionsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedDivisions) && parsedDivisions > 0)
                    divisions = parsedDivisions;

                XElement time = attributes?.Elements().FirstOrDefault(e => e.Name.LocalName == "time");
                if (time != null)
                {
                    string beatsText = time.Elements().FirstOrDefault(e => e.Name.LocalName == "beats")?.Value;
                    string beatTypeText = time.Elements().FirstOrDefault(e => e.Name.LocalName == "beat-type")?.Value;
                    if (int.TryParse(beatsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedNumerator) && parsedNumerator > 0)
                        numerator = parsedNumerator;
                    if (int.TryParse(beatTypeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedDenominator) && parsedDenominator > 0)
                        denominator = parsedDenominator;
                    AddSignatureIfChanged();
                }

                foreach (XElement child in measure.Elements())
                {
                    if (child.Name.LocalName == "direction")
                    {
                        string soundTempo = child.Descendants().FirstOrDefault(e => e.Name.LocalName == "sound")?.Attribute("tempo")?.Value;
                        if (double.TryParse(soundTempo, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedTempo) && parsedTempo > 1.0)
                        {
                            bpm = parsedTempo;
                            if (!initialBpmSet)
                            {
                                initialBpm = bpm;
                                initialBpmSet = true;
                            }

                            if (currentQuarter > 0.0001)
                                AddTempoAnchor(currentQuarter, seconds, bpm, currentQuarter <= 0.0001 ? "Start" : $"{bpm:0.###} BPM");
                        }
                    }
                    else if (child.Name.LocalName == "note")
                    {
                        if (child.Elements().Any(e => e.Name.LocalName == "chord"))
                            continue;

                        string durationText = child.Elements().FirstOrDefault(e => e.Name.LocalName == "duration")?.Value;
                        if (!double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out double durationDivisions))
                            continue;

                        double durationQuarter = durationDivisions / Math.Max(1, divisions);
                        currentQuarter += durationQuarter;
                        seconds += durationQuarter * (60.0 / Math.Max(1.0, bpm));
                    }
                }
            }

            if (project.beatMap.beatMarkers.Count == 0)
                AddTempoAnchor(0.0, 0.0, bpm, "Start");
            if (project.beatMap.timeSignatures.Count == 0)
                project.beatMap.timeSignatures.Add(new ChartEditorTimeSignatureChange { beatPosition = 0.0, numerator = 4, denominator = 4 });
            project.beatMap.defaultTempoBpm = Math.Max(1.0, initialBpm);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ChartEditor] Failed to import MusicXML beat map from '{path}': {ex.Message}");
        }
    }

    private static void BuildFallbackSections(ChartEditorProject project)
    {
        float duration = project.DurationSeconds;
        project.sections.Add(new ChartEditorSection
        {
            id = Guid.NewGuid().ToString("N"),
            name = "Full Song",
            chartStartTimeSeconds = 0.0,
            chartEndTimeSeconds = duration,
            startTimeSeconds = 0.0,
            endTimeSeconds = duration,
            userEdited = false
        });
    }

    private static void BuildSectionsFromStartMarkers(ChartEditorProject project, IEnumerable<SectionMarker> sourceMarkers)
    {
        List<SectionMarker> markers = sourceMarkers?
            .Where(marker => !string.IsNullOrWhiteSpace(marker.name) && marker.timeSeconds >= 0f)
            .OrderBy(marker => marker.timeSeconds)
            .ToList() ?? new List<SectionMarker>();
        if (markers.Count == 0)
            return;

        project.sections.Clear();
        float duration = project.DurationSeconds;
        for (int i = 0; i < markers.Count; i++)
        {
            SectionMarker marker = markers[i];
            float start = Mathf.Max(0f, marker.timeSeconds);
            float end = i + 1 < markers.Count ? Mathf.Max(start + 0.05f, markers[i + 1].timeSeconds) : Mathf.Max(start + 0.05f, duration);
            project.sections.Add(new ChartEditorSection
            {
                id = Guid.NewGuid().ToString("N"),
                name = NormalizeSectionName(marker.name, marker.index),
                chartStartTimeSeconds = start,
                chartEndTimeSeconds = end,
                startTimeSeconds = start,
                endTimeSeconds = end,
                userEdited = false
            });
        }
    }

    private static void BuildImportedTempoAnchors(
        ChartEditorProject project,
        IReadOnlyList<double> beatTimes,
        double fallbackBpm,
        string idPrefix)
    {
        if (project?.beatMap == null || beatTimes == null || beatTimes.Count == 0)
            return;

        List<double> orderedTimes = beatTimes
            .Where(time => time >= 0.0)
            .OrderBy(time => time)
            .ToList();
        if (orderedTimes.Count == 0)
            return;

        double firstInterval = orderedTimes.Count > 1
            ? Math.Max(0.001, orderedTimes[1] - orderedTimes[0])
            : 60.0 / Math.Max(1.0, fallbackBpm);
        project.beatMap.defaultTempoBpm = Math.Max(1.0, 60.0 / firstInterval);
        project.beatMap.beatMarkers.Clear();

        void AddImportedBeatMarker(int beatIndex, double audioTime, double bpm, bool isAnchor, string label)
        {
            beatIndex = Math.Max(0, beatIndex);
            if (project.beatMap.beatMarkers.Any(marker => marker != null && Math.Abs(marker.beatPosition - beatIndex) <= 0.0001))
                return;

            project.beatMap.beatMarkers.Add(new ChartEditorBeatMarker
            {
                id = beatIndex == 0 ? $"{idPrefix}_start" : $"{idPrefix}_tempo_{beatIndex}",
                index = beatIndex,
                beatPosition = beatIndex,
                audioTimeSeconds = Math.Max(0.0, audioTime),
                isAnchor = isAnchor,
                isDownbeat = beatIndex == 0,
                label = isAnchor ? label : string.Empty,
                bpm = Math.Max(1.0, bpm)
            });
        }

        AddImportedBeatMarker(0, orderedTimes[0], project.beatMap.defaultTempoBpm, false, string.Empty);
        double activeInterval = firstInterval;
        for (int beatIndex = 1; beatIndex + 1 < orderedTimes.Count; beatIndex++)
        {
            double nextInterval = Math.Max(0.001, orderedTimes[beatIndex + 1] - orderedTimes[beatIndex]);
            double relativeChange = Math.Abs(nextInterval - activeInterval) / Math.Max(0.001, activeInterval);
            if (relativeChange < 0.03)
                continue;

            double nextBpm = 60.0 / nextInterval;
            AddImportedBeatMarker(beatIndex, orderedTimes[beatIndex], nextBpm, true, $"{nextBpm:0.###} BPM");
            activeInterval = nextInterval;
        }

        project.beatMap.beatMarkers = project.beatMap.beatMarkers
            .OrderBy(marker => marker.beatPosition)
            .ToList();
    }

    private static ChartEditorAudioInfo BuildAudioInfo(string path, float durationSeconds)
    {
        return new ChartEditorAudioInfo
        {
            sourcePath = path ?? string.Empty,
            displayName = string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFileName(path),
            extension = string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetExtension(path)?.ToLowerInvariant() ?? string.Empty,
            durationSeconds = Math.Max(0.0, durationSeconds)
        };
    }

    private static ChartEditorSourceKind ChartEditorSourceKindFromNotation(SongNotationSourceKind kind)
    {
        switch (kind)
        {
            case SongNotationSourceKind.MusicXml:
                return ChartEditorSourceKind.MusicXml;
            case SongNotationSourceKind.Gp5:
                return ChartEditorSourceKind.GuitarPro;
            case SongNotationSourceKind.ArrangementCache:
                return ChartEditorSourceKind.ArrangementCache;
            case SongNotationSourceKind.TheoryPackage:
                return ChartEditorSourceKind.TheoryPackage;
            default:
                return ChartEditorSourceKind.Empty;
        }
    }

    private static ChartEditorTrackRole GuessRole(string name)
    {
        if (TryGuessRole(name, out ChartEditorTrackRole role))
            return role;
        return ChartEditorTrackRole.Custom;
    }

    private static ChartEditorTrackRole ResolveRole(MusicXmlLoader.MusicXmlPartSummary summary, params string[] orderedHints)
    {
        if (orderedHints != null)
        {
            for (int i = 0; i < orderedHints.Length; i++)
            {
                if (TryGuessRole(orderedHints[i], out ChartEditorTrackRole role))
                    return role;
            }
        }

        if (summary?.StringTuningPitches != null &&
            summary.StringTuningPitches.Length > 0 &&
            summary.StringTuningPitches.Length <= 4)
        {
            return ChartEditorTrackRole.Bass;
        }

        return ChartEditorTrackRole.Custom;
    }

    private static bool TryGuessRole(string name, out ChartEditorTrackRole role)
    {
        string normalized = (name ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            role = ChartEditorTrackRole.Custom;
            return false;
        }

        if (normalized.Contains("bass"))
        {
            role = ChartEditorTrackRole.Bass;
            return true;
        }
        if (normalized.Contains("drum") || normalized.Contains("percussion"))
        {
            role = ChartEditorTrackRole.Drums;
            return true;
        }
        if (normalized.Contains("piano") ||
            normalized.Contains("keyboard") ||
            normalized.Contains("keys") ||
            normalized.Contains("synth"))
        {
            role = ChartEditorTrackRole.Piano;
            return true;
        }
        if (normalized.Contains("vocal") || normalized.Contains("voice") || normalized.Contains("lyric"))
        {
            role = ChartEditorTrackRole.Vocals;
            return true;
        }
        if (normalized.Contains("rhythm"))
        {
            role = ChartEditorTrackRole.RhythmGuitar;
            return true;
        }
        if (normalized.Contains("lead") || normalized.Contains("guitar"))
        {
            role = ChartEditorTrackRole.LeadGuitar;
            return true;
        }
        if (normalized.Contains("custom"))
        {
            role = ChartEditorTrackRole.Custom;
            return true;
        }

        role = ChartEditorTrackRole.Custom;
        return false;
    }

    private static string ColorForRole(ChartEditorTrackRole role)
    {
        switch (role)
        {
            case ChartEditorTrackRole.LeadGuitar:
                return "#9B6BFF";
            case ChartEditorTrackRole.RhythmGuitar:
                return "#4AA6FF";
            case ChartEditorTrackRole.Bass:
                return "#53D37D";
            case ChartEditorTrackRole.Drums:
                return "#F59A45";
            case ChartEditorTrackRole.Piano:
                return "#D7E2FF";
            case ChartEditorTrackRole.Vocals:
                return "#F36BC3";
            default:
                return "#D4DAE8";
        }
    }

    private static string NormalizeSectionName(string rawName, int index)
    {
        string name = string.IsNullOrWhiteSpace(rawName) ? $"Section {index + 1}" : rawName.Trim();
        name = name.Replace('_', ' ').Replace('-', ' ');
        while (name.IndexOf("  ", StringComparison.Ordinal) >= 0)
            name = name.Replace("  ", " ");
        return string.IsNullOrWhiteSpace(name) ? $"Section {index + 1}" : name;
    }

    private static int GetChartPreference(string path)
    {
        string extension = Path.GetExtension(path)?.ToLowerInvariant() ?? string.Empty;
        switch (extension)
        {
            case ".theory":
                return 0;
            case ".gp":
                return 1;
            case ".gpx":
                return 2;
            case ".gp5":
                return 3;
            case ".musicxml":
                return 4;
            case ".xml":
                return 5;
            default:
                return 10;
        }
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return string.Empty;

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return string.Empty;
    }

    private static List<GpTempoPoint> BuildGpTempoMap(Gp5Song song)
    {
        List<GpTempoPoint> tempoMap = new List<GpTempoPoint>();
        if (song?.tempoChanges != null)
        {
            for (int i = 0; i < song.tempoChanges.Count; i++)
            {
                Gp5TempoChange change = song.tempoChanges[i];
                if (change != null)
                    tempoMap.Add(new GpTempoPoint(Math.Max(0.0, change.quarterPos), Math.Max(1.0, change.bpm)));
            }
        }

        tempoMap = tempoMap.OrderBy(point => point.quarterPos).ToList();
        if (tempoMap.Count == 0 || tempoMap[0].quarterPos > 0.0001)
            tempoMap.Insert(0, new GpTempoPoint(0.0, Math.Max(1.0, song?.initialTempo ?? 120)));
        return tempoMap;
    }

    private static double GpQuarterToSeconds(double quarterPos, List<GpTempoPoint> tempoMap)
    {
        if (tempoMap == null || tempoMap.Count == 0)
            return Math.Max(0.0, quarterPos) * 0.5;

        double targetQuarter = Math.Max(0.0, quarterPos);
        double seconds = 0.0;
        GpTempoPoint current = tempoMap[0];
        for (int i = 1; i < tempoMap.Count; i++)
        {
            GpTempoPoint next = tempoMap[i];
            if (targetQuarter <= next.quarterPos)
            {
                seconds += Math.Max(0.0, targetQuarter - current.quarterPos) * (60.0 / Math.Max(1.0, current.bpm));
                return seconds;
            }

            seconds += Math.Max(0.0, next.quarterPos - current.quarterPos) * (60.0 / Math.Max(1.0, current.bpm));
            current = next;
        }

        seconds += Math.Max(0.0, targetQuarter - current.quarterPos) * (60.0 / Math.Max(1.0, current.bpm));
        return seconds;
    }

    private readonly struct GpTempoPoint
    {
        public readonly double quarterPos;
        public readonly double bpm;

        public GpTempoPoint(double quarterPos, double bpm)
        {
            this.quarterPos = quarterPos;
            this.bpm = bpm;
        }
    }

    private readonly struct SectionMarker
    {
        public readonly string name;
        public readonly float timeSeconds;
        public readonly int index;

        public SectionMarker(string name, float timeSeconds, int index)
        {
            this.name = name;
            this.timeSeconds = timeSeconds;
            this.index = index;
        }
    }
}

public static class ChartEditorValidationService
{
    public static List<string> BuildWarnings(ChartEditorProject project)
    {
        List<string> warnings = new List<string>();
        if (project == null)
        {
            warnings.Add("No project is loaded.");
            return warnings;
        }

        if (project.tracks == null || project.tracks.Count == 0)
        {
            warnings.Add("No playable tracks were imported.");
        }
        else if (!project.tracks.Any(track => track != null && track.notes != null && track.notes.Count > 0))
        {
            warnings.Add("Imported tracks contain no notes.");
        }
        else
        {
            AddRoleTuningWarnings(project.tracks, warnings);
        }

        if (project.audio == null || string.IsNullOrWhiteSpace(project.audio.sourcePath) || !File.Exists(project.audio.sourcePath))
            warnings.Add("No usable audio file is attached.");

        if (project.sections == null || project.sections.Count == 0)
            warnings.Add("No sections were imported. Add sections before exporting if you want practice timeline markers.");

        if (ChartEditorTimingService.GetAnchors(project).Count < 2)
            warnings.Add("Only one anchor is available. Add anchors on beat markers to correct tempo drift.");

        if (project.sections != null)
        {
            List<ChartEditorSection> ordered = project.sections
                .Where(section => section != null)
                .OrderBy(section => section.startTimeSeconds)
                .ToList();
            for (int i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].startTimeSeconds < ordered[i - 1].endTimeSeconds - 0.001)
                {
                    warnings.Add("Some sections overlap. The game timeline will use section start times in order.");
                    break;
                }
            }
        }

        return warnings;
    }

    private static void AddRoleTuningWarnings(List<ChartEditorTrack> tracks, List<string> warnings)
    {
        if (tracks == null || warnings == null)
            return;

        for (int i = 0; i < tracks.Count; i++)
        {
            ChartEditorTrack track = tracks[i];
            if (track == null)
                continue;

            int laneEvidence = ResolveStringLaneEvidence(track);
            string name = string.IsNullOrWhiteSpace(track.displayName)
                ? string.IsNullOrWhiteSpace(track.importedName) ? $"Track {i + 1}" : track.importedName
                : track.displayName;

            if (track.role == ChartEditorTrackRole.Bass && laneEvidence >= 5)
            {
                warnings.Add($"Track '{name}' is tagged Bass but has {laneEvidence} string lanes. Notes remain playable, but bass routing, stems, and generated tone defaults will follow the Bass tag.");
            }
            else if ((track.role == ChartEditorTrackRole.LeadGuitar || track.role == ChartEditorTrackRole.RhythmGuitar) &&
                     laneEvidence > 0 &&
                     laneEvidence <= 4)
            {
                warnings.Add($"Track '{name}' is tagged Guitar but has {laneEvidence} string lanes. Notes remain playable, but guitar routing, stems, and generated tone defaults will follow the Guitar tag.");
            }
        }
    }

    private static int ResolveStringLaneEvidence(ChartEditorTrack track)
    {
        int evidence = track?.tuning?.stringPitches != null ? track.tuning.stringPitches.Length : 0;
        if (track?.notes != null)
        {
            for (int i = 0; i < track.notes.Count; i++)
            {
                ChartEditorNote note = track.notes[i];
                if (note != null)
                    evidence = Math.Max(evidence, note.stringOrLane + 1);
            }
        }

        return evidence;
    }
}
