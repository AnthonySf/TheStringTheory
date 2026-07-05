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
        ".theory", ".gp", ".gp3", ".gp4", ".gp5", ".gp8", ".gpx", ".musicxml", ".xml"
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

    public static bool TryReadNewProjectChartInfo(
        string chartPath,
        out SongNotationSourceKind kind,
        out List<MusicXmlLoader.MusicXmlPartSummary> summaries,
        out string title,
        out string artist,
        out string error)
    {
        kind = SongNotationSourceKind.None;
        summaries = new List<MusicXmlLoader.MusicXmlPartSummary>();
        title = string.Empty;
        artist = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(chartPath) || !File.Exists(chartPath))
        {
            error = "Chart file was not found.";
            return false;
        }

        if (!SongNotationFacade.TryDetectKind(chartPath, out kind) || kind == SongNotationSourceKind.None)
        {
            error = $"Unsupported chart file: {Path.GetExtension(chartPath)}";
            return false;
        }

        if (kind == SongNotationSourceKind.TheoryPackage)
        {
            error = "Use Open .theory Package for existing theory files.";
            return false;
        }

        summaries = GetNotationSummariesForImport(chartPath, kind);
        title = FirstNonEmpty(SongNotationFacade.TryReadDisplayName(chartPath, kind), Path.GetFileNameWithoutExtension(chartPath));
        artist = SongNotationFacade.TryReadCreator(chartPath, kind) ?? string.Empty;
        return true;
    }

    public static bool CreateNewProject(ChartEditorNewProjectRequest request, out ChartEditorImportResult result, out string error)
    {
        result = null;
        error = string.Empty;
        request ??= new ChartEditorNewProjectRequest();

        string chartPath = request.chartPath?.Trim() ?? string.Empty;
        string audioPath = request.audioPath?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(audioPath) && !File.Exists(audioPath))
        {
            error = "Audio file was not found.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(chartPath))
        {
            result = CreateBlankProject(request);
            return true;
        }

        if (!File.Exists(chartPath))
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
        {
            if (!ImportTheoryPackage(chartPath, out result, out error))
                return false;

            ApplyNewProjectMetadata(result.project, request, Path.GetFileNameWithoutExtension(chartPath));
            result.project.dirty = true;
            return true;
        }

        if (request.selectedPartIndices != null && request.selectedPartIndices.Count == 0)
        {
            error = "Select at least one arrangement or remove the chart file.";
            return false;
        }

        if (request.selectedPartIndices != null && request.selectedPartIndices.Count > 0)
        {
            HashSet<int> selected = new HashSet<int>(request.selectedPartIndices);
            if (!GetNotationSummariesForImport(chartPath, kind).Any(summary => summary != null && selected.Contains(summary.Index)))
            {
                error = "Selected arrangements were not found in the chart file.";
                return false;
            }
        }

        result = ImportNotationProject(
            chartPath,
            kind,
            audioPath,
            ChartEditorSourceKindFromNotation(kind),
            request.selectedPartIndices,
            request);
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

        List<MusicXmlLoader.MusicXmlPartSummary> selectedSummaries = OrderImportedSummaries(TheorySongLoader.GetPartSummaries(packagePath));
        for (int i = 0; i < selectedSummaries.Count; i++)
        {
            MusicXmlLoader.MusicXmlPartSummary summary = selectedSummaries[i];
            if (summary == null || summary.Index < 0 || summary.Index >= (manifest.arrangements?.Count ?? 0))
                continue;

            TheoryArrangementSummary arrangementSummary = manifest.arrangements[summary.Index];
            if (!TheoryPackageIO.TryReadArrangement(packagePath, arrangementSummary, out TheoryArrangementData arrangement, out _))
                continue;

            project.tracks.Add(BuildTrack(summary, arrangement, packagePath));
        }

        ApplyTheoryToneLabMappings(project, packagePath);
        ImportTheoryTiming(packagePath, manifest, project);
        ApplyTheoryEditorState(packagePath, project);
        FinishProject(project);

        result = new ChartEditorImportResult { project = project };
        if (!string.IsNullOrWhiteSpace(audioWarning))
            result.warnings.Add(audioWarning);
        result.warnings.AddRange(ChartEditorValidationService.BuildWarnings(project));
        return true;
    }

    public static bool ImportExternalImporterSource(
        string sourcePath,
        out ChartEditorImportResult result,
        out string error,
        string importerId = null)
    {
        result = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(sourcePath) || (!File.Exists(sourcePath) && !Directory.Exists(sourcePath)))
        {
            error = "Importer source file or folder was not found.";
            return false;
        }

        string importDirectory = Path.Combine(ExternalContentPaths.PersistentRoot, "ChartEditorImports");
        if (!SongImporterRegistry.ConvertSourceToTheoryPackage(
                new SongImporterConversionRequest
                {
                    importerId = importerId,
                    sourcePath = sourcePath,
                    outputDirectory = importDirectory,
                    overwriteExisting = false,
                    validatePackage = true,
                    requireAudio = true
                },
                out SongImporterConversionResult conversionResult,
                out error))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(conversionResult?.packagePath) || !File.Exists(conversionResult.packagePath))
        {
            error = "Importer completed without a usable .theory package.";
            return false;
        }

        if (!ImportTheoryPackage(conversionResult.packagePath, out result, out error))
            return false;

        result.project.sourceKind = ChartEditorSourceKind.ExternalImporter;
        result.project.sourcePath = Path.GetFullPath(sourcePath);
        result.project.sourceFolder = Directory.Exists(sourcePath)
            ? Path.GetFullPath(sourcePath)
            : Path.GetDirectoryName(sourcePath) ?? string.Empty;
        result.project.dirty = true;
        if (conversionResult.warnings != null)
            result.warnings.AddRange(conversionResult.warnings.Where(warning => !string.IsNullOrWhiteSpace(warning)));
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

        if (SongImporterRegistry.TryGetImporterForSource(folderPath, out SongImporterDescriptor folderImporter))
            return ImportExternalImporterSource(folderPath, out result, out error, folderImporter?.Id);

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
            error = "No supported chart or importer-backed folder source was found in the folder.";
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

    private static ChartEditorImportResult ImportNotationProject(
        string notationPath,
        SongNotationSourceKind kind,
        string audioPath,
        ChartEditorSourceKind sourceKind,
        IReadOnlyCollection<int> selectedPartIndices = null,
        ChartEditorNewProjectRequest metadataOverride = null)
    {
        ChartEditorProject project = CreateBaseProject(notationPath, sourceKind);
        project.sourceFolder = Path.GetDirectoryName(notationPath) ?? string.Empty;
        project.metadata.title = FirstNonEmpty(
            SongNotationFacade.TryReadDisplayName(notationPath, kind),
            Path.GetFileNameWithoutExtension(notationPath));
        project.metadata.artist = SongNotationFacade.TryReadCreator(notationPath, kind) ?? string.Empty;
        ApplyNewProjectMetadata(project, metadataOverride, project.metadata.title);
        project.audio = BuildAudioInfo(audioPath, 0f);

        List<MusicXmlLoader.MusicXmlPartSummary> summaries = GetNotationSummariesForImport(notationPath, kind);
        if (selectedPartIndices != null && selectedPartIndices.Count > 0)
        {
            HashSet<int> selected = new HashSet<int>(selectedPartIndices);
            summaries = summaries
                .Where(summary => summary != null && selected.Contains(summary.Index))
                .ToList();
        }

        GeneratedPlaybackArrangement generatedArrangement = SongNotationFacade.LoadGeneratedArrangement(notationPath, kind);
        for (int i = 0; i < summaries.Count; i++)
        {
            MusicXmlLoader.MusicXmlPartSummary summary = summaries[i];
            int partIndex = summary?.Index ?? i;
            List<NoteData> notes = SongNotationFacade.LoadSong(notationPath, kind, partIndex) ?? new List<NoteData>();
            project.tracks.Add(BuildTrack(summary, notes, generatedArrangement));
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

    private static ChartEditorImportResult CreateBlankProject(ChartEditorNewProjectRequest request)
    {
        ChartEditorProject project = CreateBaseProject(string.Empty, ChartEditorSourceKind.Empty);
        ApplyNewProjectMetadata(project, request, "Untitled Song");
        project.audio = BuildAudioInfo(request?.audioPath?.Trim(), 0f);
        project.tracks.Add(CreateDefaultBlankTrack());
        FinishProject(project);

        ChartEditorImportResult result = new ChartEditorImportResult { project = project };
        result.warnings.AddRange(ChartEditorValidationService.BuildWarnings(project));
        if (!string.IsNullOrWhiteSpace(request?.audioPath) && !IsSupportedAudioPath(request.audioPath))
            result.warnings.Add($"Audio extension '{Path.GetExtension(request.audioPath)}' is referenced but may not decode on every Unity platform.");
        return result;
    }

    private static ChartEditorTrack CreateDefaultBlankTrack()
    {
        ChartEditorTrack track = new ChartEditorTrack
        {
            id = "lead",
            importedName = "Lead Guitar",
            displayName = "Lead Guitar",
            role = ChartEditorTrackRole.LeadGuitar,
            arrangementGroupId = "lead",
            arrangementGroupDisplayName = "Lead Guitar",
            arrangementRoute = "Lead",
            arrangementInstrumentType = "guitar",
            difficultyLabel = "Full",
            difficultyUiIndex = 0,
            hasDifficultyVariants = false,
            colorHex = ColorForRole(ChartEditorTrackRole.LeadGuitar),
            tuning = new ChartEditorTuningInfo
            {
                displayName = "Standard E",
                stringPitches = new[] { 40, 45, 50, 55, 59, 64 }
            },
            generatedPart = FromGeneratedPlaybackPart(null, "lead", "Lead Guitar", ChartEditorTrackRole.LeadGuitar),
            notes = new List<ChartEditorNote>(),
            arpeggioGuides = new List<ChartEditorArpeggioGuide>(),
            generatedChannels = new List<ChartEditorGeneratedChannelAssignment>(),
            generatedNotes = new List<ChartEditorGeneratedNoteEvent>()
        };
        track.EnsureDefaults();
        return track;
    }

    private static void ApplyNewProjectMetadata(
        ChartEditorProject project,
        ChartEditorNewProjectRequest request,
        string fallbackTitle)
    {
        if (project?.metadata == null || request == null)
            return;

        project.metadata.title = FirstNonEmpty(request.title, project.metadata.title, fallbackTitle, "Untitled Song");
        project.metadata.artist = request.artist?.Trim() ?? string.Empty;
        project.metadata.album = request.album?.Trim() ?? string.Empty;
        project.metadata.genre = request.genre?.Trim() ?? string.Empty;
        project.metadata.year = request.year?.Trim() ?? string.Empty;
        project.metadata.coverImagePath = request.coverImagePath?.Trim() ?? string.Empty;
        project.metadata.previewStartTimeSeconds = Math.Max(0.0, request.previewStartTimeSeconds);
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
        ChartEditorToneScopeService.NormalizeProjectToneGroups(project);
        bool hasValidSelection = !string.IsNullOrWhiteSpace(project.selectedTrackId) &&
                                 project.tracks.Any(track => track != null &&
                                                             string.Equals(track.id, project.selectedTrackId, StringComparison.OrdinalIgnoreCase));
        if (!hasValidSelection && project.tracks.Count > 0)
            project.selectedTrackId = SelectDefaultImportedTrack(project)?.id ?? project.tracks[0].id;

        if (project.sections.Count == 0)
            BuildFallbackSections(project);

        ChartEditorTimingService.NormalizeSections(project);
        ChartEditorRuntimeNoteSanitizer.SanitizeProjectNotes(project);
        ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: true);
        RefreshGeneratedPlaybackFingerprints(project);
        project.dirty = false;
    }

    private static void ApplyTheoryToneLabMappings(ChartEditorProject project, string packagePath)
    {
        if (project?.tracks == null ||
            string.IsNullOrWhiteSpace(packagePath) ||
            !TheoryPackageIO.TryReadToneLabMappings(packagePath, out TheoryToneLabMappingState mappingState, out _) ||
            mappingState?.mappings == null ||
            mappingState.mappings.Count == 0)
        {
            return;
        }

        for (int i = 0; i < mappingState.mappings.Count; i++)
        {
            TheoryToneLabPresetMappingData mapping = mappingState.mappings[i];
            if (mapping == null ||
                string.IsNullOrWhiteSpace(mapping.arrangementId) ||
                string.IsNullOrWhiteSpace(mapping.toneName))
            {
                continue;
            }

            ChartEditorTonePresetData preset = FromTheoryTonePreset(mapping.presetSnapshot);
            if (!HasUsableTonePreset(preset))
                continue;

            foreach (ChartEditorTrack track in project.tracks.Where(track => ToneMappingAppliesToTrack(track, mapping.arrangementId)))
                ApplyTheoryToneLabMapping(track, mapping.toneName, mapping.presetId, preset);
        }
    }

    private static bool ToneMappingAppliesToTrack(ChartEditorTrack track, string arrangementId)
    {
        if (track == null || string.IsNullOrWhiteSpace(arrangementId))
            return false;

        string normalizedArrangement = arrangementId.Trim();
        return string.Equals(track.arrangementGroupId ?? string.Empty, normalizedArrangement, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(track.id ?? string.Empty, normalizedArrangement, StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyTheoryToneLabMapping(
        ChartEditorTrack track,
        string toneName,
        string presetId,
        ChartEditorTonePresetData preset)
    {
        if (track == null || string.IsNullOrWhiteSpace(toneName) || !HasUsableTonePreset(preset))
            return;

        track.tones ??= new ChartEditorToneData();
        track.tones.EnsureDefaults();
        string resolvedToneName = toneName.Trim();
        ChartEditorToneDefinition definition = FindToneDefinition(track.tones, resolvedToneName);
        bool createdDefinition = false;
        if (definition == null)
        {
            definition = new ChartEditorToneDefinition
            {
                name = resolvedToneName,
                key = BuildNeutralTonePresetId(resolvedToneName, presetId)
            };
            track.tones.definitions.Add(definition);
            createdDefinition = true;
        }

        if (string.IsNullOrWhiteSpace(definition.name))
            definition.name = resolvedToneName;
        if (string.IsNullOrWhiteSpace(definition.key))
            definition.key = BuildNeutralTonePresetId(definition.name, presetId);

        definition.preset = preset;
        if (string.IsNullOrWhiteSpace(definition.preset.presetId))
            definition.preset.presetId = BuildNeutralTonePresetId(definition.name, presetId);
        if (string.IsNullOrWhiteSpace(definition.preset.presetName))
            definition.preset.presetName = definition.name;
        definition.fallback ??= new ChartEditorToneFallbackData();
        if (createdDefinition)
        {
            definition.fallback.preferredPresetName = FirstNonEmpty(definition.fallback.preferredPresetName, definition.preset.presetName, definition.name);
            definition.fallback.searchText = FirstNonEmpty(definition.fallback.searchText, definition.name, definition.key);
        }
    }

    private static ChartEditorToneDefinition FindToneDefinition(ChartEditorToneData tones, string toneName)
    {
        if (tones?.definitions == null || string.IsNullOrWhiteSpace(toneName))
            return null;

        string normalized = toneName.Trim();
        return tones.definitions.FirstOrDefault(definition =>
            definition != null &&
            (string.Equals(definition.name ?? string.Empty, normalized, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(definition.key ?? string.Empty, normalized, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(definition.preset?.presetName ?? string.Empty, normalized, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(definition.preset?.presetId ?? string.Empty, normalized, StringComparison.OrdinalIgnoreCase)));
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

    private static List<MusicXmlLoader.MusicXmlPartSummary> OrderImportedSummaries(List<MusicXmlLoader.MusicXmlPartSummary> summaries)
    {
        return (summaries ?? new List<MusicXmlLoader.MusicXmlPartSummary>())
            .Where(summary => summary != null)
            .OrderBy(summary => GetFullDifficultySummaryGroupKey(summary), StringComparer.OrdinalIgnoreCase)
            .ThenBy(ResolveDifficultyUiIndex)
            .ThenByDescending(summary => summary.NoteCount)
            .ThenBy(summary => summary.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<MusicXmlLoader.MusicXmlPartSummary> GetNotationSummariesForImport(
        string notationPath,
        SongNotationSourceKind kind)
    {
        List<MusicXmlLoader.MusicXmlPartSummary> summaries = OrderImportedSummaries(
            SongNotationFacade.GetPartSummaries(notationPath, kind));
        if (summaries != null && summaries.Count > 0)
            return summaries;

        return new List<MusicXmlLoader.MusicXmlPartSummary>
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

    private static ChartEditorTrack SelectDefaultImportedTrack(ChartEditorProject project)
    {
        return project?.tracks?
            .Where(track => track != null)
            .OrderBy(track => string.IsNullOrWhiteSpace(track.arrangementGroupId) ? track.id ?? string.Empty : track.arrangementGroupId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(ResolveDifficultyUiIndex)
            .ThenByDescending(track => track.notes?.Count ?? 0)
            .FirstOrDefault();
    }

    private static int ResolveDifficultyUiIndex(MusicXmlLoader.MusicXmlPartSummary summary)
    {
        if (summary == null)
            return int.MaxValue;
        if (summary.DifficultyUiIndex >= 0)
            return summary.DifficultyUiIndex;
        if (string.Equals(summary.DifficultyLabel?.Trim(), "Full", StringComparison.OrdinalIgnoreCase))
            return 0;
        return int.TryParse(summary.DifficultyLabel, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? Mathf.Max(0, parsed)
            : int.MaxValue;
    }

    private static int ResolveDifficultyUiIndex(ChartEditorTrack track)
    {
        if (track == null)
            return int.MaxValue;
        if (track.difficultyUiIndex >= 0)
            return track.difficultyUiIndex;
        if (string.Equals(track.difficultyLabel?.Trim(), "Full", StringComparison.OrdinalIgnoreCase))
            return 0;
        return int.TryParse(track.difficultyLabel, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? Mathf.Max(0, parsed)
            : int.MaxValue;
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

    private static string NormalizeTrackKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    private static ChartEditorTrack BuildTrack(
        MusicXmlLoader.MusicXmlPartSummary summary,
        List<NoteData> sourceNotes,
        GeneratedPlaybackArrangement generatedArrangement = null)
    {
        string importedName = FirstNonEmpty(summary?.GroupDisplayName, summary?.Name, "Track");
        ChartEditorTrackRole role = ResolveRole(summary, summary?.Route, summary?.InstrumentType, summary?.GroupDisplayName, summary?.Name);
        GeneratedPlaybackPartInfo generatedPart = ResolveGeneratedPlaybackPart(generatedArrangement, summary);
        ChartEditorTrack track = new ChartEditorTrack
        {
            id = FirstNonEmpty(summary?.PartId, summary?.GroupId, Guid.NewGuid().ToString("N")),
            importedName = importedName,
            displayName = importedName,
            role = role,
            arrangementGroupId = FirstNonEmpty(summary?.GroupId, summary?.PartId),
            arrangementGroupDisplayName = FirstNonEmpty(summary?.GroupDisplayName, summary?.Name, importedName),
            arrangementRoute = summary?.Route ?? string.Empty,
            arrangementInstrumentType = summary?.InstrumentType ?? string.Empty,
            difficultyLabel = NormalizeDifficultyLabel(summary?.DifficultyLabel, summary?.DifficultyUiIndex ?? -1),
            difficultyUiIndex = NormalizeDifficultyUiIndex(summary?.DifficultyUiIndex ?? -1, summary?.DifficultyLabel),
            hasDifficultyVariants = summary?.HasDifficultyVariants ?? false,
            importedSelectionScore = summary?.Score ?? -1,
            importedTabCount = summary?.TabCount ?? -1,
            preserveImportedRuntimeNotes = true,
            colorHex = ColorForRole(role),
            tuning = new ChartEditorTuningInfo
            {
                displayName = summary?.TuningDisplayName ?? string.Empty,
                stringPitches = summary?.StringTuningPitches != null ? (int[])summary.StringTuningPitches.Clone() : null
            },
            generatedPart = FromGeneratedPlaybackPart(generatedPart, FirstNonEmpty(summary?.PartId, summary?.GroupId), importedName, role),
            notes = new List<ChartEditorNote>(),
            arpeggioGuides = new List<ChartEditorArpeggioGuide>(),
            generatedChannels = FromGeneratedPlaybackChannels(generatedArrangement, summary, generatedPart),
            generatedNotes = FromGeneratedPlaybackNotes(generatedArrangement, summary, generatedPart)
        };

        sourceNotes ??= new List<NoteData>();
        for (int i = 0; i < sourceNotes.Count; i++)
            track.notes.Add(FromNoteData(sourceNotes[i], i, role == ChartEditorTrackRole.Drums));

        track.notes = track.notes
            .OrderBy(note => note.timeSeconds)
            .ThenBy(note => note.stringOrLane)
            .ToList();
        track.EnsureDefaults();
        return track;
    }

    private static ChartEditorTrack BuildTrack(MusicXmlLoader.MusicXmlPartSummary summary, PsarcCachedArrangementPart part)
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
            arrangementGroupId = FirstNonEmpty(summary?.GroupId, part?.arrangementGroupId, summary?.PartId, part?.partId),
            arrangementGroupDisplayName = FirstNonEmpty(summary?.GroupDisplayName, part?.arrangementDisplayName, summary?.Name, importedName),
            arrangementRoute = FirstNonEmpty(part?.route, summary?.Route),
            arrangementInstrumentType = summary?.InstrumentType ?? string.Empty,
            difficultyLabel = NormalizeDifficultyLabel(FirstNonEmpty(summary?.DifficultyLabel, part?.difficultyLabel), summary?.DifficultyUiIndex ?? part?.difficultyUiIndex ?? -1),
            difficultyUiIndex = NormalizeDifficultyUiIndex(summary?.DifficultyUiIndex ?? part?.difficultyUiIndex ?? -1, FirstNonEmpty(summary?.DifficultyLabel, part?.difficultyLabel)),
            hasDifficultyVariants = (summary?.HasDifficultyVariants ?? false) || (part?.hasDifficultyVariants ?? false),
            importedSelectionScore = summary?.Score ?? -1,
            importedTabCount = summary?.TabCount ?? -1,
            preserveImportedRuntimeNotes = true,
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
            generatedChannels = new List<ChartEditorGeneratedChannelAssignment>(),
            generatedNotes = FromCachedGeneratedNotes(part?.generatedNotes)
        };

        List<PsarcCachedNoteData> sourceNotes = part?.notes ?? new List<PsarcCachedNoteData>();
        for (int i = 0; i < sourceNotes.Count; i++)
            track.notes.Add(FromCachedNoteData(sourceNotes[i], i, role == ChartEditorTrackRole.Drums));

        if (part?.arpeggioGuides != null)
        {
            for (int i = 0; i < part.arpeggioGuides.Count; i++)
            {
                PsarcCachedArpeggioGuideData guide = part.arpeggioGuides[i];
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

    private static ChartEditorTrack BuildTrack(MusicXmlLoader.MusicXmlPartSummary summary, TheoryArrangementData arrangement, string packagePath = null)
    {
        string importedName = FirstNonEmpty(summary?.GroupDisplayName, summary?.Name, arrangement?.groupDisplayName, arrangement?.displayName, "Track");
        string arrangementRoute = FirstNonEmpty(arrangement?.route, summary?.Route);
        ChartEditorTrackRole role = ResolveRole(
            summary,
            arrangementRoute,
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
            arrangementGroupId = FirstNonEmpty(summary?.GroupId, arrangement?.groupId, arrangement?.arrangementId),
            arrangementGroupDisplayName = FirstNonEmpty(summary?.GroupDisplayName, arrangement?.groupDisplayName, summary?.Name, importedName),
            arrangementRoute = FirstNonEmpty(arrangement?.route, summary?.Route),
            arrangementInstrumentType = FirstNonEmpty(arrangement?.instrumentType, summary?.InstrumentType),
            difficultyLabel = NormalizeDifficultyLabel(FirstNonEmpty(summary?.DifficultyLabel, arrangement?.difficultyLabel), summary?.DifficultyUiIndex ?? arrangement?.difficultyUiIndex ?? -1),
            difficultyUiIndex = NormalizeDifficultyUiIndex(summary?.DifficultyUiIndex ?? arrangement?.difficultyUiIndex ?? -1, FirstNonEmpty(summary?.DifficultyLabel, arrangement?.difficultyLabel)),
            hasDifficultyVariants = (summary?.HasDifficultyVariants ?? false) || (arrangement?.hasDifficultyVariants ?? false),
            importedSelectionScore = summary?.Score ?? -1,
            importedTabCount = summary?.TabCount ?? -1,
            preserveImportedRuntimeNotes = arrangement?.preserveImportedRuntimeNotes ?? false,
            colorHex = ColorForRole(role),
            tuning = new ChartEditorTuningInfo
            {
                displayName = FirstNonEmpty(summary?.TuningDisplayName, arrangement?.tuningDisplayName),
                stringPitches = summary?.StringTuningPitches != null
                    ? (int[])summary.StringTuningPitches.Clone()
                    : arrangement?.tuningPitches != null ? (int[])arrangement.tuningPitches.Clone() : null
            },
            generatedPart = FromTheoryGeneratedPart(arrangement?.generatedPart, arrangement?.arrangementId, importedName, role),
            tones = FromTheoryToneData(arrangement?.tones, packagePath, arrangementRoute),
            notes = new List<ChartEditorNote>(),
            arpeggioGuides = new List<ChartEditorArpeggioGuide>(),
            generatedChannels = FromTheoryGeneratedChannels(arrangement?.generatedChannels),
            generatedNotes = FromTheoryGeneratedNotes(arrangement?.generatedNotes)
        };

        List<TheoryNoteData> sourceNotes = arrangement?.notes ?? new List<TheoryNoteData>();
        for (int i = 0; i < sourceNotes.Count; i++)
            track.notes.Add(FromTheoryNoteData(
                sourceNotes[i],
                i,
                role == ChartEditorTrackRole.Drums,
                arrangement?.preserveImportedRuntimeNotes ?? false));

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

    private static ChartEditorToneData FromCachedToneData(PsarcCachedArrangementToneData source, string arrangementRoute)
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
                PsarcCachedToneChangeData change = source.changes[i];
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
                PsarcCachedToneDefinitionData definition = source.definitions[i];
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

    private static ChartEditorToneData FromTheoryToneData(TheoryToneData source, string packagePath = null, string arrangementRoute = null)
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
                    preset = FromTheoryToneDefinitionPreset(definition, packagePath, arrangementRoute),
                    fallback = FromTheoryToneDefinitionFallback(definition, arrangementRoute)
                });
            }
        }

        result.EnsureDefaults();
        return result;
    }

    private static ChartEditorTonePresetData FromTheoryToneDefinitionPreset(
        TheoryToneDefinitionData source,
        string packagePath,
        string arrangementRoute)
    {
        ChartEditorTonePresetData preset = FromTheoryTonePreset(source?.preset);
        if (HasUsableTonePreset(preset))
            return preset;

        string rawToneJson = ReadTheoryRawToneJson(packagePath, source?.rawToneEntry);
        if (!string.IsNullOrWhiteSpace(rawToneJson))
        {
            if (TryParseToneLabPreset(rawToneJson, out UnityToneLabRuntime.ToneLabPreset serializedPreset))
                return FromUnityToneLabPreset(serializedPreset, source?.name, source?.key);

            if (PsarcTonePresetBuilder.TryBuildPreset(source?.name, arrangementRoute, rawToneJson, out UnityToneLabRuntime.ToneLabPreset convertedPreset))
                return FromUnityToneLabPreset(convertedPreset, source?.name, source?.key);
        }

        return preset;
    }

    private static ChartEditorToneFallbackData FromTheoryToneDefinitionFallback(
        TheoryToneDefinitionData source,
        string arrangementRoute)
    {
        ChartEditorToneFallbackData fallback = FromTheoryToneFallback(source?.fallback);
        if (!string.IsNullOrWhiteSpace(fallback.preferredPresetName) ||
            !string.IsNullOrWhiteSpace(fallback.searchText))
        {
            return fallback;
        }

        // Definitions carrying a full pedal chain are self-sufficient —
        // synthesizing a fallback for them breaks round-trip fidelity of
        // editor-authored tones. The guess only helps external packages whose
        // preset could not be reconstructed.
        if (source?.preset?.pedalChain != null && source.preset.pedalChain.Count > 0)
            return fallback;

        return new ChartEditorToneFallbackData
        {
            preferredPresetName = FirstNonEmpty(source?.preferredPresetName, source?.preset?.presetName, source?.name, source?.key),
            searchText = BuildToneFallbackSearchText(source?.fallbackSearchText, source?.name, source?.key, arrangementRoute)
        };
    }

    private static bool HasUsableTonePreset(ChartEditorTonePresetData preset)
    {
        return preset != null &&
               preset.pedalChain != null &&
               preset.pedalChain.Count > 0;
    }

    private static string ReadTheoryRawToneJson(string packagePath, string rawToneEntry)
    {
        if (string.IsNullOrWhiteSpace(packagePath) ||
            string.IsNullOrWhiteSpace(rawToneEntry) ||
            !TheoryPackageIO.TryReadTextEntry(packagePath, rawToneEntry, out string rawJson, out _))
        {
            return string.Empty;
        }

        return rawJson ?? string.Empty;
    }

    private static ChartEditorTonePresetData FromCachedToneDefinition(PsarcCachedToneDefinitionData source, string arrangementRoute)
    {
        if (source == null)
            return new ChartEditorTonePresetData();

        if (TryParseToneLabPreset(source.rawJson, out UnityToneLabRuntime.ToneLabPreset serializedPreset))
            return FromUnityToneLabPreset(serializedPreset, source.name, source.key);

        if (PsarcTonePresetBuilder.TryBuildPreset(source.name, arrangementRoute, source.rawJson, out UnityToneLabRuntime.ToneLabPreset convertedPreset))
            return FromUnityToneLabPreset(convertedPreset, source.name, source.key);

        return new ChartEditorTonePresetData
        {
            presetId = BuildNeutralTonePresetId(source.name, source.key),
            presetName = source.name ?? source.key ?? string.Empty
        };
    }

    private static ChartEditorToneFallbackData FromCachedToneFallback(PsarcCachedToneDefinitionData source, string arrangementRoute)
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
            presetId = PsarcTonePresetBuilder.IsGeneratedPresetId(source?.preset_id)
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
        PsarcCachedGeneratedPartInfo source,
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

    private static ChartEditorGeneratedPartInfo FromGeneratedPlaybackPart(
        GeneratedPlaybackPartInfo source,
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

    private static List<ChartEditorGeneratedNoteEvent> FromCachedGeneratedNotes(List<PsarcCachedGeneratedNoteEvent> source)
    {
        List<ChartEditorGeneratedNoteEvent> result = new List<ChartEditorGeneratedNoteEvent>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            PsarcCachedGeneratedNoteEvent note = source[i];
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

    private static List<ChartEditorGeneratedChannelAssignment> FromTheoryGeneratedChannels(List<TheoryGeneratedChannelAssignment> source)
    {
        List<ChartEditorGeneratedChannelAssignment> result = new List<ChartEditorGeneratedChannelAssignment>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            TheoryGeneratedChannelAssignment channel = source[i];
            if (channel == null)
                continue;

            result.Add(new ChartEditorGeneratedChannelAssignment
            {
                channel = channel.channel,
                bank = channel.bank,
                preset = channel.preset,
                isDrum = channel.isDrum,
                label = channel.label ?? string.Empty,
                sourcePartId = channel.sourcePartId ?? string.Empty,
                sourcePartName = channel.sourcePartName ?? string.Empty,
                pitchBendRangeSemitones = Mathf.Max(0, channel.pitchBendRangeSemitones)
            });
        }

        return result;
    }

    private static GeneratedPlaybackPartInfo ResolveGeneratedPlaybackPart(
        GeneratedPlaybackArrangement arrangement,
        MusicXmlLoader.MusicXmlPartSummary summary)
    {
        if (arrangement?.parts == null || arrangement.parts.Count == 0)
            return null;

        HashSet<string> candidateIds = BuildGeneratedPlaybackCandidateIds(summary, null);
        GeneratedPlaybackPartInfo byId = arrangement.parts.FirstOrDefault(part =>
            part != null && candidateIds.Contains(part.partId ?? string.Empty));
        if (byId != null)
            return byId;

        string displayName = FirstNonEmpty(summary?.GroupDisplayName, summary?.Name);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            GeneratedPlaybackPartInfo byName = arrangement.parts.FirstOrDefault(part =>
                part != null &&
                (string.Equals(part.displayName ?? string.Empty, displayName, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(part.instrumentName ?? string.Empty, displayName, StringComparison.OrdinalIgnoreCase)));
            if (byName != null)
                return byName;
        }

        return arrangement.parts.Count == 1 ? arrangement.parts[0] : null;
    }

    private static List<ChartEditorGeneratedNoteEvent> FromGeneratedPlaybackNotes(
        GeneratedPlaybackArrangement arrangement,
        MusicXmlLoader.MusicXmlPartSummary summary,
        GeneratedPlaybackPartInfo generatedPart)
    {
        List<ChartEditorGeneratedNoteEvent> result = new List<ChartEditorGeneratedNoteEvent>();
        if (arrangement?.notes == null || arrangement.notes.Count == 0)
            return result;

        HashSet<string> candidateIds = BuildGeneratedPlaybackCandidateIds(summary, generatedPart);
        HashSet<int> candidateChannels = BuildGeneratedPlaybackCandidateChannels(arrangement, candidateIds);
        bool includeAll = arrangement.parts != null && arrangement.parts.Count == 1 && candidateChannels.Count == 0;
        bool hasCandidateIds = candidateIds.Count > 0;

        for (int i = 0; i < arrangement.notes.Count; i++)
        {
            GeneratedPlaybackNoteEvent note = arrangement.notes[i];
            if (note == null)
                continue;

            bool idMatch = candidateIds.Contains(note.partId ?? string.Empty);
            bool channelMatch = (!hasCandidateIds || string.IsNullOrWhiteSpace(note.partId)) &&
                                candidateChannels.Contains(note.channel);
            if (!includeAll && !idMatch && !channelMatch)
                continue;

            result.Add(CloneGeneratedNote(
                note.startTimeSeconds,
                note.durationSeconds,
                note.pitchPreRollSeconds,
                note.midiNote,
                note.velocity,
                note.channel,
                string.IsNullOrWhiteSpace(note.partId) ? generatedPart?.partId : note.partId,
                string.IsNullOrWhiteSpace(note.partName) ? generatedPart?.displayName : note.partName,
                (int)note.techniqueVariant,
                (int)note.legatoTransitionKind,
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

    private static List<ChartEditorGeneratedChannelAssignment> FromGeneratedPlaybackChannels(
        GeneratedPlaybackArrangement arrangement,
        MusicXmlLoader.MusicXmlPartSummary summary,
        GeneratedPlaybackPartInfo generatedPart)
    {
        List<ChartEditorGeneratedChannelAssignment> result = new List<ChartEditorGeneratedChannelAssignment>();
        if (arrangement?.channelAssignments == null || arrangement.channelAssignments.Count == 0)
            return result;

        HashSet<string> candidateIds = BuildGeneratedPlaybackCandidateIds(summary, generatedPart);
        bool includeAll = arrangement.parts != null && arrangement.parts.Count == 1;
        foreach (GeneratedPlaybackChannelAssignment channel in arrangement.channelAssignments
                     .Where(channel => channel != null)
                     .OrderBy(channel => channel.channel))
        {
            bool hasSourceIdentity = !string.IsNullOrWhiteSpace(channel.sourcePartId) ||
                                     !string.IsNullOrWhiteSpace(channel.sourcePartName);
            bool idMatch = candidateIds.Contains(channel.sourcePartId ?? string.Empty) ||
                           candidateIds.Contains(channel.sourcePartName ?? string.Empty) ||
                           (!hasSourceIdentity && candidateIds.Contains(channel.label ?? string.Empty));
            if (!includeAll && !idMatch)
                continue;

            result.Add(new ChartEditorGeneratedChannelAssignment
            {
                channel = channel.channel,
                bank = channel.bank,
                preset = channel.preset,
                isDrum = channel.isDrum,
                label = channel.label ?? string.Empty,
                sourcePartId = channel.sourcePartId ?? string.Empty,
                sourcePartName = channel.sourcePartName ?? string.Empty,
                pitchBendRangeSemitones = Mathf.Max(0, channel.pitchBendRangeSemitones)
            });
        }

        return result;
    }

    private static HashSet<string> BuildGeneratedPlaybackCandidateIds(
        MusicXmlLoader.MusicXmlPartSummary summary,
        GeneratedPlaybackPartInfo generatedPart)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddCandidateId(ids, summary?.PartId);
        AddCandidateId(ids, summary?.GroupId);
        AddCandidateId(ids, summary?.Name);
        AddCandidateId(ids, summary?.GroupDisplayName);
        AddCandidateId(ids, generatedPart?.partId);
        AddCandidateId(ids, generatedPart?.displayName);
        return ids;
    }

    private static HashSet<int> BuildGeneratedPlaybackCandidateChannels(
        GeneratedPlaybackArrangement arrangement,
        HashSet<string> candidateIds)
    {
        HashSet<int> channels = new HashSet<int>();
        if (arrangement?.channelAssignments == null || candidateIds == null || candidateIds.Count == 0)
            return channels;

        for (int i = 0; i < arrangement.channelAssignments.Count; i++)
        {
            GeneratedPlaybackChannelAssignment channel = arrangement.channelAssignments[i];
            if (channel == null)
                continue;

            bool hasSourceIdentity = !string.IsNullOrWhiteSpace(channel.sourcePartId) ||
                                     !string.IsNullOrWhiteSpace(channel.sourcePartName);
            if (candidateIds.Contains(channel.sourcePartId ?? string.Empty) ||
                candidateIds.Contains(channel.sourcePartName ?? string.Empty) ||
                (!hasSourceIdentity && candidateIds.Contains(channel.label ?? string.Empty)))
            {
                channels.Add(channel.channel);
            }
        }

        return channels;
    }

    private static void AddCandidateId(HashSet<string> ids, string value)
    {
        if (ids == null || string.IsNullOrWhiteSpace(value))
            return;

        ids.Add(value.Trim());
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

    private static ChartEditorNote FromNoteData(NoteData source, int fallbackIndex, bool drumTrack = false)
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
            palmMute = source.isPalmMute,
            fretHandMute = false,
            hasRuntimeMuted = true,
            runtimeMuted = source.isMuted,
            hasRuntimePalmMute = true,
            runtimePalmMute = source.isPalmMute,
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
        if (drumTrack)
            NormalizeDrumEditorNote(note);
        note.EnsureDefaults();
        return note;
    }

    private static ChartEditorNote FromCachedNoteData(PsarcCachedNoteData source, int fallbackIndex, bool drumTrack = false)
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
            hasRuntimeMuted = true,
            runtimeMuted = source.isMuted,
            hasRuntimePalmMute = true,
            runtimePalmMute = false,
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
                PsarcCachedBendPointData point = source.bendPoints[i];
                if (point == null)
                    continue;

                note.bendPoints.Add(new ChartEditorBendPoint
                {
                    timeSeconds = point.timeSeconds,
                    step = point.step
                });
            }
        }

        List<NoteTechniqueSegmentData> normalizedSegments = PsarcTechniqueSegmentNormalizer.BuildNormalizedTechniqueSegments(source);
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
        if (drumTrack)
            NormalizeDrumEditorNote(note);
        note.EnsureDefaults();
        return note;
    }

    private static ChartEditorNote FromTheoryNoteData(
        TheoryNoteData source,
        int fallbackIndex,
        bool drumTrack = false,
        bool preserveRuntimeFields = false)
    {
        if (source == null)
            return null;

        bool runtimeMuted = source.hasRuntimeMuted
            ? source.runtimeMuted
            : source.muted && !source.palmMute;
        bool runtimePalmMute = source.hasRuntimePalmMute
            ? source.runtimePalmMute
            : source.palmMute;
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
            technique = ResolveTheoryPrimaryTechnique(source, preserveRuntimeFields),
            slideTargetFret = source.slideTargetFret,
            bendStep = source.bendStep,
            bendVisualStartTime = source.bendVisualStartTime,
            bendVisualDuration = source.bendVisualDuration,
            bendPreBend = source.bendPreBend,
            bendRelease = source.bendRelease,
            muted = muted,
            palmMute = source.palmMute || (muted && !source.fretHandMute && !source.palmMute),
            fretHandMute = source.fretHandMute,
            hasRuntimeMuted = true,
            runtimeMuted = runtimeMuted,
            hasRuntimePalmMute = true,
            runtimePalmMute = runtimePalmMute,
            harmonic = source.harmonic,
            accent = source.accent,
            tap = source.tap,
            tremolo = source.tremolo,
            pinchHarmonic = source.pinchHarmonic,
            vibratoStrength = source.vibratoStrength,
            maxBend = Mathf.Max(source.maxBend, source.bendStep),
            legato = preserveRuntimeFields
                ? source.legato
                : source.legato || source.hammerOn || source.pullOff || source.hopo,
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
        if (drumTrack)
            NormalizeDrumEditorNote(note);
        note.EnsureDefaults();
        return note;
    }

    private static void NormalizeDrumEditorNote(ChartEditorNote note)
    {
        if (note == null)
            return;

        if (note.fret >= 35 && note.fret <= 87)
        {
            note.stringOrLane = DrumLaneMapper.MapGeneralMidiToLane(note.fret);
            if (string.IsNullOrWhiteSpace(note.noteName))
                note.noteName = DrumLaneMapper.GetGeneralMidiDrumName(note.fret);
        }
        else if (DrumLaneMapper.TryResolveLaneFromLabel(note.noteName, out int labelLane))
        {
            note.stringOrLane = labelLane;
        }
        else
        {
            note.stringOrLane = Mathf.Clamp(note.stringOrLane, 0, DrumLaneMapper.LaneCount - 1);
        }

        note.stringOrLane = Mathf.Clamp(note.stringOrLane, 0, DrumLaneMapper.LaneCount - 1);
        ChartEditorDrumNoteSanitizer.Sanitize(note);
    }

    private static NoteTechnique ResolveCachedPrimaryTechnique(PsarcCachedNoteData source)
    {
        if (source == null)
            return NoteTechnique.None;
        if (source.isHammerOn)
            return NoteTechnique.HammerOn;
        if (source.isPullOff)
            return NoteTechnique.PullOff;
        return (NoteTechnique)Mathf.Clamp(source.technique, (int)NoteTechnique.None, (int)NoteTechnique.Vibrato);
    }

    private static NoteTechnique ResolveTheoryPrimaryTechnique(TheoryNoteData source, bool preserveRuntimeFields = false)
    {
        if (source == null)
            return NoteTechnique.None;
        if (preserveRuntimeFields)
            return (NoteTechnique)Mathf.Clamp(source.primaryTechnique, (int)NoteTechnique.None, (int)NoteTechnique.Vibrato);
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
        project.beatMap.timeSignatures.Add(DeriveImportedTimeSignature(beats));
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
                    synchTheorySource = marker.synchTheorySource ?? string.Empty,
                    locked = marker.locked,
                    linkedSectionId = marker.linkedSectionId ?? string.Empty
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
                    name = point.label ?? string.Empty,
                    locked = point.locked,
                    linkedSectionId = point.linkedSectionId ?? string.Empty
                });
            }
        }
    }

    private static void ImportGpSections(string path, ChartEditorProject project)
    {
        if (!AlphaTabGpTimelineLoader.TryLoadTimeline(path, out AlphaTabGpTimelineData timeline) ||
            timeline?.sections == null ||
            timeline.sections.Count == 0)
        {
            return;
        }

        BuildSectionsFromStartMarkers(
            project,
            timeline.sections.Select(section => new SectionMarker(section.name, section.timeSeconds, section.index)));
    }

    private static void ImportGpBeatMap(string path, ChartEditorProject project)
    {
        try
        {
            if (!AlphaTabGpTimelineLoader.TryLoadTimeline(path, out AlphaTabGpTimelineData timeline) ||
                timeline == null ||
                project?.beatMap == null)
            {
                return;
            }

            project.beatMap.defaultTempoBpm = Math.Max(1.0, timeline.defaultTempoBpm);

            project.beatMap.beatMarkers.Clear();
            foreach (AlphaTabGpTimelineTempoChange tempoPoint in timeline.tempoChanges
                         .GroupBy(point => Math.Round(point.beatPosition, 4))
                         .Select(group => group.First())
                         .OrderBy(point => point.beatPosition))
            {
                double beatPosition = Math.Max(0.0, tempoPoint.beatPosition);
                bool tempoChangeAnchor = beatPosition > 0.0001;
                project.beatMap.beatMarkers.Add(new ChartEditorBeatMarker
                {
                    id = Guid.NewGuid().ToString("N"),
                    beatPosition = beatPosition,
                    audioTimeSeconds = Math.Max(0.0, tempoPoint.timeSeconds),
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
            if (timeline.timeSignatures != null)
            {
                foreach (AlphaTabGpTimelineTimeSignatureChange signature in timeline.timeSignatures.OrderBy(signature => signature.beatPosition))
                {
                    if (signature == null)
                        continue;

                    int numerator = Math.Max(1, signature.numerator);
                    int denominator = Math.Max(1, signature.denominator);
                    if (numerator == lastNumerator && denominator == lastDenominator)
                        continue;

                    project.beatMap.timeSignatures.Add(new ChartEditorTimeSignatureChange
                    {
                        beatPosition = Math.Max(0.0, signature.beatPosition),
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

                foreach (XElement child in measure.Elements())
                {
                    if (child.Name.LocalName == "note")
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
                    else if (child.Name.LocalName == "backup" || child.Name.LocalName == "forward")
                    {
                        string durationText = child.Elements().FirstOrDefault(e => e.Name.LocalName == "duration")?.Value;
                        if (!double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out double moveDivisions))
                            continue;

                        double moveQuarter = (moveDivisions / Math.Max(1, divisions)) * (child.Name.LocalName == "backup" ? -1.0 : 1.0);
                        currentQuarter = Math.Max(0.0, currentQuarter + moveQuarter);
                        seconds = Math.Max(0.0, seconds + moveQuarter * (60.0 / Math.Max(1.0, bpm)));
                    }
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
                            {
                                AddTempoAnchor(currentQuarter, seconds, bpm, $"{bpm:0.###} BPM");
                            }
                            else
                            {
                                // Tempo declared before the first note (the
                                // standard MusicXML layout): update the beat-0
                                // marker instead of leaving the grid at the
                                // hard-coded 120 BPM default.
                                ChartEditorBeatMarker origin = project.beatMap.beatMarkers
                                    .FirstOrDefault(marker => marker != null && Math.Abs(marker.beatPosition) <= 0.0001);
                                if (origin != null)
                                    origin.bpm = Math.Max(1.0, bpm);
                            }
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
                    else if (child.Name.LocalName == "backup" || child.Name.LocalName == "forward")
                    {
                        // Multi-voice/multi-staff measures rewind the cursor
                        // with <backup>; ignoring it double-counts every such
                        // measure and drifts the whole beat grid.
                        string durationText = child.Elements().FirstOrDefault(e => e.Name.LocalName == "duration")?.Value;
                        if (!double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out double moveDivisions))
                            continue;

                        double moveQuarter = (moveDivisions / Math.Max(1, divisions)) * (child.Name.LocalName == "backup" ? -1.0 : 1.0);
                        currentQuarter = Math.Max(0.0, currentQuarter + moveQuarter);
                        seconds = Math.Max(0.0, seconds + moveQuarter * (60.0 / Math.Max(1.0, bpm)));
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

    private static int BuildImportedTempoAnchors(
        ChartEditorProject project,
        IReadOnlyList<double> beatTimes,
        double fallbackBpm,
        string idPrefix)
    {
        if (project?.beatMap == null || beatTimes == null || beatTimes.Count == 0)
            return 0;

        List<double> orderedTimes = beatTimes
            .Where(time => time >= 0.0)
            .OrderBy(time => time)
            .ToList();
        if (orderedTimes.Count == 0)
            return 0;

        double firstInterval = orderedTimes.Count > 1
            ? Math.Max(0.001, orderedTimes[1] - orderedTimes[0])
            : 60.0 / Math.Max(1.0, fallbackBpm);
        project.beatMap.defaultTempoBpm = Math.Max(1.0, 60.0 / firstInterval);
        project.beatMap.beatMarkers.Clear();

        // Back-extend the grid to audio zero at the first measured tempo so
        // notes before the first detected beat (intro/pickup) keep real beat
        // positions instead of collapsing onto beat 0.
        int leadInBeats = orderedTimes[0] > 0.01
            ? Math.Max(0, (int)Math.Ceiling(orderedTimes[0] / firstInterval - 0.0001))
            : 0;
        double originAudio = Math.Max(0.0, orderedTimes[0] - leadInBeats * firstInterval);

        project.beatMap.beatMarkers.Add(new ChartEditorBeatMarker
        {
            id = $"{idPrefix}_start",
            index = 0,
            beatPosition = 0.0,
            audioTimeSeconds = originAudio,
            isAnchor = false,
            isDownbeat = true,
            label = string.Empty,
            bpm = project.beatMap.defaultTempoBpm
        });

        // Keep the measured grid faithfully: every detected beat becomes a
        // generated timing control (like SynchTheory output). The old sparse
        // 3%-threshold anchors let wobbly live-performance tempo drift off
        // the real beats between anchors.
        for (int beatIndex = 0; beatIndex < orderedTimes.Count; beatIndex++)
        {
            int beat = leadInBeats + beatIndex;
            if (beat <= 0)
                continue;

            project.beatMap.beatMarkers.Add(new ChartEditorBeatMarker
            {
                id = $"{idPrefix}_beat_{beat}",
                index = beat,
                beatPosition = beat,
                audioTimeSeconds = Math.Max(0.0, orderedTimes[beatIndex]),
                isAnchor = false,
                generatedBySynchTheory = true,
                synchTheoryConfidence = 1.0,
                synchTheorySource = idPrefix,
                // Locked: probe drags and manual anchor edits must not delete
                // an imported measured grid (see the Clear* guards).
                locked = true,
                label = string.Empty
            });
        }

        project.beatMap.beatMarkers = project.beatMap.beatMarkers
            .OrderBy(marker => marker.beatPosition)
            .ToList();
        return leadInBeats;
    }

    private static ChartEditorTimeSignatureChange DeriveImportedTimeSignature(List<TheoryBeatData> beats)
    {
        // Pick the dominant beats-per-measure from the measured beat data so
        // 3/4 or 6/8 material stops being force-labeled 4/4. Partial first and
        // last measures are excluded from the vote.
        Dictionary<int, int> votes = new Dictionary<int, int>();
        if (beats != null)
        {
            int runMeasure = int.MinValue;
            int runLength = 0;
            bool firstRun = true;
            void Vote(bool isFinalRun)
            {
                // Runs of length 1 also arise from downbeat-only measure
                // numbering conventions — never vote a 1/4 signature off them.
                if (runLength >= 2 && runLength <= 32 && !firstRun && !isFinalRun)
                {
                    votes.TryGetValue(runLength, out int count);
                    votes[runLength] = count + 1;
                }
            }

            for (int i = 0; i < beats.Count; i++)
            {
                short measure = beats[i]?.measure ?? -1;
                if (measure < 0)
                    continue;

                if (measure != runMeasure)
                {
                    Vote(false);
                    if (runMeasure != int.MinValue)
                        firstRun = false;
                    runMeasure = measure;
                    runLength = 0;
                }

                runLength++;
            }
        }

        int numerator = 4;
        int best = 0;
        foreach (KeyValuePair<int, int> vote in votes)
        {
            if (vote.Value > best || (vote.Value == best && vote.Key > numerator))
            {
                best = vote.Value;
                numerator = vote.Key;
            }
        }

        return new ChartEditorTimeSignatureChange
        {
            beatPosition = 0.0,
            numerator = numerator,
            denominator = 4
        };
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
            case SongNotationSourceKind.TheoryPackage:
                return ChartEditorSourceKind.TheoryPackage;
            default:
                return ChartEditorSourceKind.Empty;
        }
    }

    private static string NormalizeDifficultyLabel(string difficultyLabel, int difficultyUiIndex)
    {
        if (!string.IsNullOrWhiteSpace(difficultyLabel))
            return difficultyLabel.Trim();
        if (difficultyUiIndex == 0)
            return "Full";
        if (difficultyUiIndex > 0)
            return difficultyUiIndex.ToString(CultureInfo.InvariantCulture);
        return "Full";
    }

    private static int NormalizeDifficultyUiIndex(int difficultyUiIndex, string difficultyLabel)
    {
        if (difficultyUiIndex >= 0)
            return difficultyUiIndex;

        string label = difficultyLabel?.Trim();
        if (string.Equals(label, "Full", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (int.TryParse(label, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            return Mathf.Max(0, parsed);
        return 0;
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
            case ".gp8":
                return 3;
            case ".gp5":
                return 4;
            case ".musicxml":
                return 5;
            case ".xml":
                return 6;
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

public static class ChartEditorTheoryConversionService
{
    public static bool ConvertLibrarySourceToTheoryPackage(
        string sourcePath,
        string audioPath,
        out ChartEditorTheoryConversionResult result,
        out string error)
    {
        return ConvertLibrarySourceToTheoryPackage(new ChartEditorTheoryConversionRequest
        {
            sourcePath = sourcePath,
            audioPath = audioPath
        }, out result, out error);
    }

    public static bool ConvertLibrarySourceToTheoryPackage(
        ChartEditorTheoryConversionRequest request,
        out ChartEditorTheoryConversionResult result,
        out string error)
    {
        if (request == null)
        {
            result = null;
            error = "Theory conversion request is missing.";
            return false;
        }

        ChartEditorTheoryConversionRequest libraryRequest = new ChartEditorTheoryConversionRequest
        {
            sourcePath = request.sourcePath,
            audioPath = request.audioPath,
            outputDirectory = request.outputDirectory,
            outputPackagePath = request.outputPackagePath,
            overwriteExisting = request.overwriteExisting,
            validatePackage = request.validatePackage,
            requireAudio = request.requireAudio,
            returnExistingTheoryPackage = request.returnExistingTheoryPackage,
            useLibrarySongsDirectory = true,
            rejectChartEditorOutputDirectory = true
        };

        return ConvertToTheoryPackage(libraryRequest, out result, out error);
    }

    public static bool ConvertToTheoryPackage(
        string sourcePath,
        string audioPath,
        string outputPackagePath,
        out ChartEditorTheoryConversionResult result,
        out string error)
    {
        return ConvertToTheoryPackage(new ChartEditorTheoryConversionRequest
        {
            sourcePath = sourcePath,
            audioPath = audioPath,
            outputPackagePath = outputPackagePath
        }, out result, out error);
    }

    public static bool ConvertToTheoryPackage(
        ChartEditorTheoryConversionRequest request,
        out ChartEditorTheoryConversionResult result,
        out string error)
    {
        result = new ChartEditorTheoryConversionResult();
        error = string.Empty;

        if (request == null)
        {
            error = "Theory conversion request is missing.";
            return false;
        }

        string sourcePath = request.sourcePath?.Trim();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            error = "Theory conversion source path is empty.";
            return false;
        }

        bool sourceIsFile = File.Exists(sourcePath);
        bool sourceIsDirectory = Directory.Exists(sourcePath);
        if (!sourceIsFile && !sourceIsDirectory)
        {
            error = "Theory conversion source was not found.";
            return false;
        }

        bool sourceIsTheoryPackage = sourceIsFile && TheoryPackageFormat.IsPackagePath(sourcePath);
        if (sourceIsTheoryPackage &&
            request.returnExistingTheoryPackage &&
            string.IsNullOrWhiteSpace(request.outputPackagePath) &&
            string.IsNullOrWhiteSpace(request.outputDirectory))
        {
            result.packagePath = Path.GetFullPath(sourcePath);
            result.sourceKind = ChartEditorSourceKind.TheoryPackage;
            result.sourceNotationKind = SongNotationSourceKind.TheoryPackage;
            result.sourceAlreadyTheoryPackage = true;
            result.packageWasWritten = false;
            if (request.validatePackage &&
                !ValidateTheoryPackage(result.packagePath, null, request.requireAudio, result.warnings, out error))
            {
                return false;
            }

            return true;
        }

        if ((sourceIsFile || sourceIsDirectory) && SongImporterRegistry.TryGetImporterForSource(sourcePath, out _))
            return ConvertExternalImporterSourceToTheoryPackage(request, sourcePath, out result, out error);

        if (!ImportSource(request, sourceIsFile, sourceIsDirectory, out ChartEditorImportResult importResult, out error))
            return false;

        ChartEditorProject project = importResult?.project;
        if (project == null)
        {
            error = "Theory conversion import did not return a project.";
            return false;
        }

        project.EnsureDefaults();
        result.project = project;
        result.sourceKind = project.sourceKind;
        result.sourceNotationKind = ResolveSourceNotationKind(sourcePath, sourceIsFile, project.sourceKind);
        if (importResult?.warnings != null)
            result.warnings.AddRange(importResult.warnings.Where(warning => !string.IsNullOrWhiteSpace(warning)));

        if (project.tracks == null || project.tracks.Count == 0)
        {
            error = "Theory conversion source contains no playable tracks.";
            return false;
        }

        if (request.requireAudio && !HasUsableAudio(project))
        {
            error = "Theory conversion requires audio, but the imported source has no usable audio file.";
            return false;
        }

        string packagePath = ResolveOutputPackagePath(project, sourcePath, sourceIsDirectory, request);
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            error = "Theory conversion output path could not be resolved.";
            return false;
        }

        if (request.rejectChartEditorOutputDirectory && IsInChartEditorSaveDirectory(packagePath))
        {
            error = "Library .theory conversion cannot write into the chart editor save folder.";
            return false;
        }

        if (File.Exists(packagePath) && !request.overwriteExisting)
        {
            error = $"Theory conversion output already exists: {packagePath}";
            return false;
        }

        if (!TheoryChartEditorExporter.WriteProjectPackage(project, packagePath, out error))
            return false;

        result.packagePath = Path.GetFullPath(packagePath);
        result.packageWasWritten = true;
        result.sourceAlreadyTheoryPackage = sourceIsTheoryPackage;

        if (request.validatePackage &&
            !ValidateTheoryPackage(result.packagePath, project, request.requireAudio, result.warnings, out error))
        {
            return false;
        }

        return true;
    }

    public static bool ValidateTheoryPackage(
        string packagePath,
        ChartEditorProject sourceProject,
        bool requireAudio,
        List<string> warnings,
        out string error)
    {
        error = string.Empty;
        warnings ??= new List<string>();

        if (!TheoryPackageIO.TryReadManifest(packagePath, out TheorySongManifest manifest, out error))
            return false;

        if (manifest.arrangements == null || manifest.arrangements.Count == 0)
        {
            error = "Converted .theory package has no arrangements.";
            return false;
        }

        if (requireAudio)
        {
            string audioEntry = FirstNonEmpty(
                manifest.primaryAudioEntry,
                manifest.audio?.FirstOrDefault(asset => asset != null && asset.defaultForPlayback)?.entry);
            if (string.IsNullOrWhiteSpace(audioEntry) || !TheoryPackageIO.EntryExists(packagePath, audioEntry))
            {
                error = "Converted .theory package is missing embedded playback audio.";
                return false;
            }
        }

        List<ChartEditorTrack> sourceTracks = sourceProject?.tracks?
            .Where(track => track != null)
            .ToList();
        if (sourceTracks != null && manifest.arrangements.Count != sourceTracks.Count)
        {
            error = $"Converted .theory package has {manifest.arrangements.Count} arrangements, expected {sourceTracks.Count}.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(manifest.defaultArrangementId) &&
            !manifest.arrangements.Any(summary => string.Equals(summary?.arrangementId ?? string.Empty, manifest.defaultArrangementId, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Converted .theory package default arrangement id does not reference an exported arrangement.";
            return false;
        }

        HashSet<string> arrangementIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int playableArrangementCount = 0;
        for (int i = 0; i < manifest.arrangements.Count; i++)
        {
            TheoryArrangementSummary summary = manifest.arrangements[i];
            if (summary == null)
            {
                error = $"Converted .theory package has a missing arrangement summary at index {i}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(summary.arrangementId))
            {
                error = $"Converted .theory package has an arrangement with no id at index {i}.";
                return false;
            }

            if (!arrangementIds.Add(summary.arrangementId))
            {
                error = $"Converted .theory package has duplicate arrangement id '{summary.arrangementId}'.";
                return false;
            }

            if (!TheoryPackageIO.TryReadArrangement(packagePath, summary, out TheoryArrangementData arrangement, out error))
                return false;

            int exportedNoteCount = arrangement.notes?.Count ?? 0;
            if (exportedNoteCount > 0)
            {
                playableArrangementCount++;
                if ((arrangement.generatedNotes?.Count ?? 0) == 0)
                {
                    error = $"Converted .theory package is missing generated playback events for '{summary.arrangementId}'.";
                    return false;
                }
            }

            if (summary.noteCount != exportedNoteCount)
            {
                error = $"Converted .theory package note count mismatch for '{summary.arrangementId}'.";
                return false;
            }

            ChartEditorTrack sourceTrack = sourceTracks != null && i < sourceTracks.Count ? sourceTracks[i] : null;
            if (sourceTrack != null)
            {
                int expectedNoteCount = ChartEditorRuntimeNoteSanitizer
                    .PrepareChartNotesForRuntime(sourceTrack.notes, !sourceTrack.preserveImportedRuntimeNotes)
                    .Count;
                if (expectedNoteCount != exportedNoteCount)
                {
                    error = $"Converted .theory package lost notes for '{summary.arrangementId}' ({exportedNoteCount}/{expectedNoteCount}).";
                    return false;
                }

                bool expectedDifficultyVariants = HasDifficultyVariants(sourceTracks, sourceTrack);
                if (expectedDifficultyVariants && !summary.hasDifficultyVariants)
                {
                    error = $"Converted .theory package lost difficulty variant metadata for '{summary.arrangementId}'.";
                    return false;
                }
            }

            if (arrangement.timing == null || arrangement.timing.beats == null || arrangement.timing.beats.Count == 0)
                warnings.Add($"Arrangement '{summary.arrangementId}' has no beat map; gameplay will use fallback timing.");
        }

        if (playableArrangementCount == 0)
        {
            error = "Converted .theory package has no arrangements with notes.";
            return false;
        }

        return true;
    }

    private static bool ImportSource(
        ChartEditorTheoryConversionRequest request,
        bool sourceIsFile,
        bool sourceIsDirectory,
        out ChartEditorImportResult importResult,
        out string error)
    {
        importResult = null;
        error = string.Empty;
        string sourcePath = request.sourcePath?.Trim();

        if (sourceIsDirectory)
            return ChartEditorImportService.ImportFolder(sourcePath, out importResult, out error);

        if (!sourceIsFile)
        {
            error = "Theory conversion source was not found.";
            return false;
        }

        if (!SongNotationFacade.TryDetectKind(sourcePath, out SongNotationSourceKind kind) ||
            kind == SongNotationSourceKind.None)
        {
            string extension = Path.GetExtension(sourcePath) ?? string.Empty;
            error = $"Unsupported conversion source: {extension}";
            return false;
        }

        return ChartEditorImportService.ImportChartAndAudio(sourcePath, request.audioPath, out importResult, out error);
    }

    private static SongNotationSourceKind ResolveSourceNotationKind(string sourcePath, bool sourceIsFile, ChartEditorSourceKind sourceKind)
    {
        if (sourceKind == ChartEditorSourceKind.ExternalImporter)
            return SongNotationSourceKind.TheoryPackage;

        if (sourceIsFile && SongNotationFacade.TryDetectKind(sourcePath, out SongNotationSourceKind kind))
            return kind;

        return SongNotationSourceKind.None;
    }

    private static bool ConvertExternalImporterSourceToTheoryPackage(
        ChartEditorTheoryConversionRequest request,
        string sourcePath,
        out ChartEditorTheoryConversionResult result,
        out string error)
    {
        result = new ChartEditorTheoryConversionResult();
        if (!SongImporterRegistry.ConvertSourceToTheoryPackage(
                new SongImporterConversionRequest
                {
                    sourcePath = sourcePath,
                    outputDirectory = request.outputDirectory,
                    outputPackagePath = request.outputPackagePath,
                    overwriteExisting = request.overwriteExisting,
                    validatePackage = request.validatePackage,
                    requireAudio = request.requireAudio,
                    useLibrarySongsDirectory = request.useLibrarySongsDirectory,
                    rejectChartEditorOutputDirectory = request.rejectChartEditorOutputDirectory
                },
                out SongImporterConversionResult importerResult,
                out error))
        {
            return false;
        }

        result.packagePath = importerResult.packagePath;
        result.sourceKind = ChartEditorSourceKind.ExternalImporter;
        result.sourceNotationKind = SongNotationSourceKind.TheoryPackage;
        result.sourceAlreadyTheoryPackage = false;
        result.packageWasWritten = importerResult.packageWasWritten;
        if (importerResult.warnings != null)
            result.warnings.AddRange(importerResult.warnings.Where(warning => !string.IsNullOrWhiteSpace(warning)));

        if (!string.IsNullOrWhiteSpace(result.packagePath) && File.Exists(result.packagePath))
        {
            if (ChartEditorImportService.ImportTheoryPackage(result.packagePath, out ChartEditorImportResult importResult, out string importError))
            {
                result.project = importResult?.project;
                if (importResult?.warnings != null)
                    result.warnings.AddRange(importResult.warnings.Where(warning => !string.IsNullOrWhiteSpace(warning)));
            }
            else if (request.validatePackage)
            {
                error = $"Converted .theory package could not be opened in the chart editor: {importError}";
                return false;
            }
            else if (!string.IsNullOrWhiteSpace(importError))
            {
                result.warnings.Add(importError);
            }
        }

        return true;
    }

    private static bool HasUsableAudio(ChartEditorProject project)
    {
        string audioPath = project?.audio?.sourcePath;
        return !string.IsNullOrWhiteSpace(audioPath) && File.Exists(audioPath);
    }

    private static string ResolveOutputPackagePath(
        ChartEditorProject project,
        string sourcePath,
        bool sourceIsDirectory,
        ChartEditorTheoryConversionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.outputPackagePath))
        {
            string path = request.outputPackagePath.Trim();
            if (string.IsNullOrWhiteSpace(Path.GetExtension(path)))
                path += TheoryPackageFormat.Extension;
            return Path.GetFullPath(path);
        }

        string outputDirectory = request.outputDirectory;
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            outputDirectory = request.useLibrarySongsDirectory
                ? ExternalContentPaths.PersistentSongsDirectory
                : (sourceIsDirectory
                    ? sourcePath
                    : Path.GetDirectoryName(sourcePath));
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
            outputDirectory = Directory.GetCurrentDirectory();

        outputDirectory = Path.GetFullPath(outputDirectory);
        string baseName = BuildPackageBaseName(project);
        string candidate = Path.Combine(outputDirectory, $"{baseName}{TheoryPackageFormat.Extension}");
        return request.overwriteExisting
            ? candidate
            : BuildAvailableFilePath(outputDirectory, baseName, TheoryPackageFormat.Extension);
    }

    private static bool IsInChartEditorSaveDirectory(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            return false;

        string directory = Path.GetDirectoryName(Path.GetFullPath(packagePath));
        if (string.IsNullOrWhiteSpace(directory))
            return false;

        string chartEditorDirectory = Path.GetFullPath(Path.Combine(
            ExternalContentPaths.PersistentSongsDirectory,
            ChartEditorProjectStore.ChartEditorSaveFolderName));
        return IsSameOrChildPath(chartEditorDirectory, directory);
    }

    private static bool IsSameOrChildPath(string parentPath, string childPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(childPath))
            return false;

        string parent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string child = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(parent, child, StringComparison.OrdinalIgnoreCase) ||
               child.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               child.StartsWith(parent + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildAvailableFilePath(string directory, string baseName, string extension)
    {
        string candidate = Path.Combine(directory, $"{baseName}{extension}");
        if (!File.Exists(candidate))
            return candidate;

        for (int i = 2; i < 10000; i++)
        {
            candidate = Path.Combine(directory, $"{baseName}_{i}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(directory, $"{baseName}_{Guid.NewGuid():N}{extension}");
    }

    private static bool HasDifficultyVariants(List<ChartEditorTrack> tracks, ChartEditorTrack sourceTrack)
    {
        if (tracks == null || sourceTrack == null)
            return false;

        string groupId = FirstNonEmpty(sourceTrack.arrangementGroupId, sourceTrack.id);
        if (string.IsNullOrWhiteSpace(groupId))
            return false;

        return tracks.Count(track =>
        {
            string candidateGroupId = FirstNonEmpty(track?.arrangementGroupId, track?.id);
            return string.Equals(candidateGroupId, groupId, StringComparison.OrdinalIgnoreCase);
        }) > 1;
    }

    private static string BuildPackageBaseName(ChartEditorProject project)
    {
        string artist = project?.metadata?.artist;
        string title = project?.metadata?.title;
        return SanitizeFileName($"{FirstNonEmpty(artist, "Unknown")}_{FirstNonEmpty(title, "Chart")}");
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "chart";

        char[] invalid = Path.GetInvalidFileNameChars();
        char[] chars = value.Trim().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (invalid.Contains(chars[i]) || char.IsWhiteSpace(chars[i]))
                chars[i] = '_';
        }

        string sanitized = new string(chars);
        while (sanitized.Contains("__"))
            sanitized = sanitized.Replace("__", "_");
        return sanitized.Trim('_');
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
                                                                            