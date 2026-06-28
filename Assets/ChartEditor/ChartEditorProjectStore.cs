using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

public static class ChartEditorProjectStore
{
    public const string ProjectExtension = ".stchart.json";
    public const string ChartEditorSaveFolderName = "chart editor";

    private const string ProjectFolderName = "ChartEditorProjects";
    private const string ExportFolderPrefix = "chart_editor_";

    public static string ProjectsDirectory => Path.Combine(ExternalContentPaths.PersistentRoot, ProjectFolderName);

    public static bool SaveTheoryPackage(ChartEditorProject project, bool saveAs, out string packagePath, out string error)
    {
        packagePath = string.Empty;
        error = string.Empty;
        if (project == null)
        {
            error = "No chart editor project is loaded.";
            return false;
        }

        project.EnsureDefaults();
        if (project.tracks == null || project.tracks.Count == 0)
        {
            error = "Project has no tracks to save.";
            return false;
        }

        try
        {
            ChartEditorRuntimeNoteSanitizer.SanitizeProjectNotes(project);
            ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: true);

            packagePath = !saveAs ? ResolveCurrentTheoryPackagePath(project) : string.Empty;
            if (string.IsNullOrWhiteSpace(packagePath))
                packagePath = BuildDefaultTheoryPackagePath(project, forceNewFile: saveAs);

            if (!TheoryChartEditorExporter.WriteProjectPackage(project, packagePath, out error))
                return false;

            project.sourceKind = ChartEditorSourceKind.TheoryPackage;
            project.sourcePath = packagePath;
            project.sourceFolder = Path.GetDirectoryName(packagePath) ?? string.Empty;
            project.savedProjectPath = string.Empty;
            project.dirty = false;
            SongLibraryService.ClearCache();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool SaveProject(ChartEditorProject project, out string savedPath, out string error)
    {
        savedPath = string.Empty;
        error = string.Empty;
        if (project == null)
        {
            error = "No chart editor project is loaded.";
            return false;
        }

        project.EnsureDefaults();
        ChartEditorRuntimeNoteSanitizer.SanitizeProjectNotes(project);
        ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: true);
        try
        {
            Directory.CreateDirectory(ProjectsDirectory);
            string path = string.IsNullOrWhiteSpace(project.savedProjectPath)
                ? Path.Combine(ProjectsDirectory, $"{BuildProjectFileName(project)}{ProjectExtension}")
                : project.savedProjectPath;

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            project.savedProjectPath = path;
            project.dirty = false;
            File.WriteAllText(path, JsonUtility.ToJson(project, true));
            savedPath = path;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string ResolveCurrentTheoryPackagePath(ChartEditorProject project)
    {
        if (project == null ||
            project.sourceKind != ChartEditorSourceKind.TheoryPackage ||
            !TheoryPackageFormat.IsPackagePath(project.sourcePath))
        {
            return string.Empty;
        }

        string path = project.sourcePath;
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        return path;
    }

    private static string BuildDefaultTheoryPackagePath(ChartEditorProject project, bool forceNewFile)
    {
        string directory = ResolveChartEditorPackageDirectory(project);
        Directory.CreateDirectory(directory);

        string baseName = BuildProjectFileName(project);
        if (forceNewFile)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            return BuildAvailableFilePath(directory, $"{baseName}_{stamp}", TheoryPackageFormat.Extension);
        }

        return BuildAvailableFilePath(directory, baseName, TheoryPackageFormat.Extension);
    }

    private static string ResolveChartEditorPackageDirectory(ChartEditorProject project)
    {
        string songDirectory = ResolveLibrarySongDirectory(project);
        if (IsChartEditorSaveDirectory(songDirectory))
            return songDirectory;

        return Path.Combine(songDirectory, ChartEditorSaveFolderName);
    }

    private static string ResolveLibrarySongDirectory(ChartEditorProject project)
    {
        string candidate = ResolveExistingLibraryDirectory(project?.sourceFolder);
        if (!string.IsNullOrWhiteSpace(candidate))
            return candidate;

        string sourceDirectory = string.IsNullOrWhiteSpace(project?.sourcePath) ? string.Empty : Path.GetDirectoryName(project.sourcePath);
        candidate = ResolveExistingLibraryDirectory(sourceDirectory);
        if (!string.IsNullOrWhiteSpace(candidate))
            return candidate;

        Directory.CreateDirectory(ExternalContentPaths.PersistentSongsDirectory);
        string folderName = SanitizeFileName($"{FirstNonEmpty(project?.metadata?.artist, "Unknown")}_{FirstNonEmpty(project?.metadata?.title, "Chart")}");
        string fallback = Path.Combine(ExternalContentPaths.PersistentSongsDirectory, folderName);
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static string ResolveExistingLibraryDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return string.Empty;

        string fullDirectory = Path.GetFullPath(directory);
        string songsRoot = Path.GetFullPath(ExternalContentPaths.PersistentSongsDirectory);
        if (!IsSameOrChildPath(songsRoot, fullDirectory))
            return string.Empty;

        if (IsChartEditorSaveDirectory(fullDirectory))
            return fullDirectory;

        return fullDirectory;
    }

    private static bool IsChartEditorSaveDirectory(string directory)
    {
        return !string.IsNullOrWhiteSpace(directory) &&
               string.Equals(
                   Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                   ChartEditorSaveFolderName,
                   StringComparison.OrdinalIgnoreCase);
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
        string safeBaseName = SanitizeFileName(baseName);
        string candidate = Path.Combine(directory, $"{safeBaseName}{extension}");
        if (!File.Exists(candidate))
            return candidate;

        for (int i = 2; i < 10000; i++)
        {
            candidate = Path.Combine(directory, $"{safeBaseName}_{i}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(directory, $"{safeBaseName}_{Guid.NewGuid():N}{extension}");
    }

    public static bool LoadProject(string path, out ChartEditorProject project, out string error)
    {
        project = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            error = "Project file was not found.";
            return false;
        }

        try
        {
            project = JsonUtility.FromJson<ChartEditorProject>(File.ReadAllText(path));
            if (project == null)
            {
                error = "Project file could not be parsed.";
                return false;
            }

            project.savedProjectPath = path;
            project.sourceKind = ChartEditorSourceKind.StringTheoryProject;
            project.EnsureDefaults();
            ChartEditorRuntimeNoteSanitizer.SanitizeProjectNotes(project);
            ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: true);
            project.dirty = false;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool ExportPlayableProject(ChartEditorProject project, out string exportDirectory, out string error)
    {
        return ExportPlayableProject(project, out exportDirectory, out _, out error);
    }

    public static bool ExportPlayableProject(ChartEditorProject project, out string exportDirectory, out string theoryPackagePath, out string error)
    {
        exportDirectory = string.Empty;
        theoryPackagePath = string.Empty;
        error = string.Empty;
        if (project == null)
        {
            error = "No chart editor project is loaded.";
            return false;
        }

        project.EnsureDefaults();
        if (project.tracks == null || project.tracks.Count == 0)
        {
            error = "Project has no tracks to export.";
            return false;
        }

        try
        {
            ChartEditorRuntimeNoteSanitizer.SanitizeProjectNotes(project);
            ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: true);
            Directory.CreateDirectory(ExternalContentPaths.PersistentSongsDirectory);
            exportDirectory = Path.Combine(ExternalContentPaths.PersistentSongsDirectory, BuildExportDirectoryName(project));
            Directory.CreateDirectory(exportDirectory);

            string audioRelativePath = CopyAudioForExport(project, exportDirectory);
            string arrangementsDirectory = Path.Combine(exportDirectory, "arrangements");
            Directory.CreateDirectory(arrangementsDirectory);

            float duration = Mathf.Max(project.DurationSeconds, 0.1f);
            List<RocksmithCachedArrangementSummary> summaries = new List<RocksmithCachedArrangementSummary>();
            for (int i = 0; i < project.tracks.Count; i++)
            {
                ChartEditorTrack track = project.tracks[i];
                if (track == null)
                    continue;

                track.EnsureDefaults();
                string partId = SanitizeIdentifier(string.IsNullOrWhiteSpace(track.id) ? $"track_{i + 1}" : track.id);
                string partFileName = $"{partId}.rs2part.json";
                string partPath = Path.Combine(arrangementsDirectory, partFileName);
                RocksmithCachedArrangementPart part = BuildArrangementPart(project, track, partId, duration);
                File.WriteAllText(partPath, JsonUtility.ToJson(part, true));

                summaries.Add(new RocksmithCachedArrangementSummary
                {
                    partId = partId,
                    displayName = track.displayName ?? track.importedName ?? $"Track {i + 1}",
                    route = RouteForRole(track.role),
                    arrangementGroupId = partId,
                    arrangementDisplayName = track.displayName ?? track.importedName ?? $"Track {i + 1}",
                    difficultyLabel = "Full",
                    difficultyUiIndex = 3,
                    hasDifficultyVariants = false,
                    partFilePath = Path.Combine("arrangements", partFileName),
                    noteCount = track.notes?.Count ?? 0,
                    tabCount = track.notes?.Count ?? 0,
                    score = Mathf.Clamp(track.notes?.Count ?? 0, 0, 100000),
                    difficultyRating = EstimateDifficulty(track),
                    tuningPitches = track.tuning?.stringPitches != null ? (int[])track.tuning.stringPitches.Clone() : null,
                    tuningDisplayName = track.tuning?.displayName ?? string.Empty
                });
            }

            RocksmithCachedSongManifest manifest = new RocksmithCachedSongManifest
            {
                schemaVersion = RocksmithCachedSongFormat.SchemaVersion,
                sourcePsarcPath = project.sourceKind == ChartEditorSourceKind.Psarc ? project.sourcePath : string.Empty,
                sourcePsarcLastWriteUtcTicks = TryGetLastWriteTicks(project.sourcePath),
                importedAtUtcTicks = DateTime.UtcNow.Ticks,
                displayName = string.IsNullOrWhiteSpace(project.metadata?.title) ? "Edited Chart" : project.metadata.title.Trim(),
                artist = project.metadata?.artist ?? string.Empty,
                album = project.metadata?.album ?? string.Empty,
                subtitle = "Chart Editor",
                artworkPath = project.metadata?.coverImagePath ?? string.Empty,
                audioPath = audioRelativePath,
                previewAudioPath = audioRelativePath,
                durationSeconds = duration,
                difficultyRating = summaries.Count > 0 ? Mathf.Clamp(Mathf.RoundToInt((float)summaries.Average(summary => summary.difficultyRating)), 0, 5) : 0,
                toneDefinitionScanVersion = 1,
                toneDefinitionCount = 0,
                arrangements = summaries
            };

            File.WriteAllText(Path.Combine(exportDirectory, RocksmithCachedSongFormat.ManifestFileName), JsonUtility.ToJson(manifest, true));
            File.WriteAllText(Path.Combine(exportDirectory, "chart_project_export.stchart.json"), JsonUtility.ToJson(project, true));

            if (!TheoryChartEditorExporter.ExportProject(project, exportDirectory, out theoryPackagePath, out string theoryError))
            {
                error = $"Playable folder was exported, but .theory package export failed: {theoryError}";
                return false;
            }

            SongLibraryService.ClearCache();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static RocksmithCachedArrangementPart BuildArrangementPart(
        ChartEditorProject project,
        ChartEditorTrack track,
        string partId,
        float duration)
    {
        List<ChartEditorNote> orderedNotes = ChartEditorRuntimeNoteSanitizer.PrepareChartNotesForRuntime(track.notes?
            .Where(note => note != null)
            .OrderBy(note => note.timeSeconds)
            .ThenBy(note => note.stringOrLane)
            .ToList() ?? new List<ChartEditorNote>());

        RocksmithCachedArrangementPart part = new RocksmithCachedArrangementPart
        {
            schemaVersion = RocksmithCachedSongFormat.SchemaVersion,
            partId = partId,
            displayName = track.displayName ?? track.importedName ?? partId,
            route = RouteForRole(track.role),
            arrangementGroupId = partId,
            arrangementDisplayName = track.displayName ?? track.importedName ?? partId,
            difficultyLabel = "Full",
            difficultyUiIndex = 3,
            hasDifficultyVariants = false,
            durationSeconds = duration,
            difficultyRating = EstimateDifficulty(track),
            tuningPitches = track.tuning?.stringPitches != null ? (int[])track.tuning.stringPitches.Clone() : null,
            tuningDisplayName = track.tuning?.displayName ?? string.Empty,
            timing = new RocksmithCachedArrangementTimingData
            {
                averageTempoBpm = EstimateAverageTempo(project),
                capo = 0,
                ebeats = BuildEbeats(project, duration),
                sections = BuildCachedSections(project)
            },
            tones = BuildToneData(track),
            generatedPart = BuildGeneratedPart(track, partId),
            notes = new List<RocksmithCachedNoteData>(),
            arpeggioGuides = BuildArpeggioGuides(track),
            generatedNotes = BuildGeneratedNotes(track)
        };

        for (int i = 0; i < orderedNotes.Count; i++)
            part.notes.Add(ToCachedNote(orderedNotes[i], i));

        return part;
    }

    private static RocksmithCachedArrangementToneData BuildToneData(ChartEditorTrack track)
    {
        RocksmithCachedArrangementToneData result = new RocksmithCachedArrangementToneData
        {
            baseToneName = track?.tones?.baseToneName ?? string.Empty,
            changes = new List<RocksmithCachedToneChangeData>(),
            definitions = new List<RocksmithCachedToneDefinitionData>()
        };

        if (track?.tones?.changes != null)
        {
            for (int i = 0; i < track.tones.changes.Count; i++)
            {
                ChartEditorToneChange source = track.tones.changes[i];
                if (source == null)
                    continue;

                result.changes.Add(new RocksmithCachedToneChangeData
                {
                    timeSeconds = Mathf.Max(0f, source.timeSeconds),
                    toneName = source.toneName ?? string.Empty,
                    toneId = source.toneId
                });
            }
        }

        if (track?.tones?.definitions != null)
        {
            for (int i = 0; i < track.tones.definitions.Count; i++)
            {
                ChartEditorToneDefinition source = track.tones.definitions[i];
                if (source == null)
                    continue;

                result.definitions.Add(new RocksmithCachedToneDefinitionData
                {
                    name = source.name ?? string.Empty,
                    key = source.key ?? string.Empty,
                    rawJson = BuildTonePresetJson(source.preset, source.name, source.key),
                    preferredPresetName = FirstNonEmpty(source.fallback?.preferredPresetName, source.preset?.presetName, source.name, source.key),
                    fallbackSearchText = BuildToneFallbackSearchText(source.fallback?.searchText, source.name, source.key)
                });
            }
        }

        return result;
    }

    private static string BuildTonePresetJson(ChartEditorTonePresetData source, string fallbackName, string fallbackKey)
    {
        UnityToneLabRuntime.ToneLabPreset preset = ToUnityToneLabPreset(source, fallbackName, fallbackKey);
        return preset?.pedal_chain != null && preset.pedal_chain.Count > 0
            ? JsonUtility.ToJson(preset, false)
            : string.Empty;
    }

    private static UnityToneLabRuntime.ToneLabPreset ToUnityToneLabPreset(ChartEditorTonePresetData source, string fallbackName, string fallbackKey)
    {
        if (source == null)
            return null;

        UnityToneLabRuntime.ToneLabPreset preset = new UnityToneLabRuntime.ToneLabPreset
        {
            preset_id = string.IsNullOrWhiteSpace(source.presetId)
                ? BuildNeutralTonePresetId(fallbackName, fallbackKey)
                : source.presetId,
            preset_name = string.IsNullOrWhiteSpace(source.presetName)
                ? FirstNonEmpty(fallbackName, fallbackKey, "Tone")
                : source.presetName,
            input_gain_db = source.inputGainDb,
            output_gain_db = source.outputGainDb,
            pedal_chain = new List<UnityToneLabRuntime.ToneLabPedalSlot>()
        };

        if (source.pedalChain != null)
        {
            for (int i = 0; i < source.pedalChain.Count; i++)
            {
                ChartEditorTonePedalSlotData slot = source.pedalChain[i];
                if (slot == null)
                    continue;

                UnityToneLabRuntime.ToneLabPedalType pedalType = UnityToneLabRuntime.ToneLabPedalType.Amp;
                if (!string.IsNullOrWhiteSpace(slot.pedalType))
                    Enum.TryParse(slot.pedalType, ignoreCase: true, out pedalType);

                preset.pedal_chain.Add(new UnityToneLabRuntime.ToneLabPedalSlot
                {
                    pedal_instance_id = slot.instanceId ?? string.Empty,
                    pedal_type = pedalType,
                    descriptor_id = slot.descriptorId ?? string.Empty,
                    enabled = slot.enabled,
                    settings_json = slot.settingsJson ?? string.Empty
                });
            }
        }

        return preset;
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

    private static RocksmithCachedGeneratedPartInfo BuildGeneratedPart(ChartEditorTrack track, string partId)
    {
        string displayName = track?.displayName ?? track?.importedName ?? partId;
        string fallbackInstrument = InstrumentNameForRole(track?.role ?? ChartEditorTrackRole.LeadGuitar);
        bool fallbackIsDrum = track?.role == ChartEditorTrackRole.Drums;
        bool fallbackIsGuitarFamily = IsGuitarFamilyRole(track?.role ?? ChartEditorTrackRole.LeadGuitar);
        int fallbackProgram = DefaultMidiProgramForRole(track?.role ?? ChartEditorTrackRole.LeadGuitar);
        int fallbackChannel = fallbackIsDrum ? 9 : 0;
        ChartEditorGeneratedPartInfo source = HasGeneratedPartOverride(track) ? track.generatedPart : null;

        return new RocksmithCachedGeneratedPartInfo
        {
            partId = FirstNonEmpty(source?.partId, partId),
            displayName = FirstNonEmpty(source?.displayName, displayName),
            instrumentName = FirstNonEmpty(source?.instrumentName, fallbackInstrument),
            sourceMidiChannel = source != null ? source.sourceMidiChannel : fallbackChannel,
            sourceMidiProgram = source != null ? source.sourceMidiProgram : fallbackProgram,
            preferredBank = source != null ? source.preferredBank : -1,
            isDrum = source != null ? source.isDrum : fallbackIsDrum,
            isGuitarFamily = source != null ? source.isGuitarFamily : fallbackIsGuitarFamily,
            isExplicitHarmonicPart = source != null && source.isExplicitHarmonicPart
        };
    }

    private static bool HasGeneratedPartOverride(ChartEditorTrack track)
    {
        ChartEditorGeneratedPartInfo source = track?.generatedPart;
        return source != null &&
               (!string.IsNullOrWhiteSpace(source.partId) ||
                !string.IsNullOrWhiteSpace(source.displayName) ||
                !string.IsNullOrWhiteSpace(source.instrumentName) ||
                !string.IsNullOrWhiteSpace(track?.generatedPlaybackNoteFingerprint) ||
                (track?.generatedNotes?.Count ?? 0) > 0);
    }

    private static List<RocksmithCachedGeneratedNoteEvent> BuildGeneratedNotes(ChartEditorTrack track)
    {
        List<RocksmithCachedGeneratedNoteEvent> result = new List<RocksmithCachedGeneratedNoteEvent>();
        if (!ChartEditorGeneratedPlaybackIntegrity.CanReuseGeneratedPlayback(track))
            return result;

        for (int i = 0; i < track.generatedNotes.Count; i++)
        {
            ChartEditorGeneratedNoteEvent source = track.generatedNotes[i];
            if (source == null)
                continue;

            RocksmithCachedGeneratedNoteEvent note = new RocksmithCachedGeneratedNoteEvent
            {
                startTimeSeconds = Mathf.Max(0f, source.startTimeSeconds),
                durationSeconds = Mathf.Max(0f, source.durationSeconds),
                pitchPreRollSeconds = Mathf.Max(0f, source.pitchPreRollSeconds),
                midiNote = source.midiNote,
                velocity = source.velocity,
                channel = source.channel,
                partId = source.partId,
                partName = source.partName,
                techniqueVariant = source.techniqueVariant,
                legatoTransitionKind = source.legatoTransitionKind,
                attackVelocityScale = source.attackVelocityScale,
                vibratoDepthSemitones = source.vibratoDepthSemitones,
                vibratoRateHz = source.vibratoRateHz,
                vibratoDelayNormalized = source.vibratoDelayNormalized,
                vibratoFadeNormalized = source.vibratoFadeNormalized,
                pitchBendRangeSemitones = source.pitchBendRangeSemitones,
                pitchCurve = new List<RocksmithCachedGeneratedPitchPoint>()
            };

            if (source.pitchCurve != null)
            {
                for (int pointIndex = 0; pointIndex < source.pitchCurve.Count; pointIndex++)
                {
                    ChartEditorGeneratedPitchPoint point = source.pitchCurve[pointIndex];
                    if (point == null)
                        continue;

                    note.pitchCurve.Add(new RocksmithCachedGeneratedPitchPoint
                    {
                        normalizedTime = point.normalizedTime,
                        semitoneOffset = point.semitoneOffset
                    });
                }
            }

            result.Add(note);
        }

        return result;
    }

    private static List<RocksmithCachedArpeggioGuideData> BuildArpeggioGuides(ChartEditorTrack track)
    {
        List<RocksmithCachedArpeggioGuideData> guides = new List<RocksmithCachedArpeggioGuideData>();
        if (track?.arpeggioGuides == null)
            return guides;

        for (int i = 0; i < track.arpeggioGuides.Count; i++)
        {
            ChartEditorArpeggioGuide source = track.arpeggioGuides[i];
            if (source == null)
                continue;

            guides.Add(new RocksmithCachedArpeggioGuideData
            {
                id = source.id,
                startTime = Mathf.Max(0f, source.startTime),
                endTime = Mathf.Max(source.startTime, source.endTime),
                chordName = source.chordName ?? string.Empty,
                stringFrets = source.stringFrets != null ? (int[])source.stringFrets.Clone() : null
            });
        }

        return guides;
    }

    private static RocksmithCachedNoteData ToCachedNote(ChartEditorNote note, int index)
    {
        int noteId = note.sourceNoteId >= 0 ? note.sourceNoteId : index;
        NoteTechnique primaryTechnique = ResolvePrimaryTechnique(note);
        bool isHammerOn = IsEditorTechniqueEnabled(note, NoteTechnique.HammerOn);
        bool isPullOff = IsEditorTechniqueEnabled(note, NoteTechnique.PullOff);
        bool hasVibrato = IsEditorTechniqueEnabled(note, NoteTechnique.Vibrato);
        bool hasSpecificMute = note.palmMute || note.fretHandMute;
        bool palmMute = note.palmMute || (note.muted && !hasSpecificMute);
        bool muted = note.muted || note.palmMute || note.fretHandMute;
        RocksmithCachedNoteData cached = new RocksmithCachedNoteData
        {
            id = noteId,
            time = Mathf.Max(0f, (float)note.timeSeconds),
            duration = Mathf.Max(0f, (float)note.durationSeconds),
            stringIdx = Mathf.Clamp(note.stringOrLane, 0, 8),
            fret = Mathf.Max(0, note.fret),
            note = note.noteName ?? string.Empty,
            chordId = note.chordId,
            chordName = note.chordName ?? string.Empty,
            technique = Mathf.Clamp((int)primaryTechnique, 0, (int)NoteTechnique.Vibrato),
            slideTargetFret = note.slideTargetFret,
            bendStep = note.bendStep,
            bendVisualStartTime = note.bendVisualStartTime,
            bendVisualDuration = note.bendVisualDuration,
            bendPreBend = note.bendPreBend,
            bendRelease = note.bendRelease,
            isMuted = muted,
            isPalmMute = palmMute,
            isFretHandMute = note.fretHandMute,
            isHarmonic = note.harmonic,
            isAccent = note.accent,
            isTap = note.tap,
            isTremolo = note.tremolo,
            isPinchHarmonic = note.pinchHarmonic,
            isHammerOn = isHammerOn,
            isPullOff = isPullOff,
            isHopo = isHammerOn || isPullOff,
            hasVibrato = hasVibrato,
            vibratoStrength = note.vibratoStrength,
            maxBend = Mathf.Max(note.maxBend, note.bendStep),
            isLegato = note.legato || isHammerOn || isPullOff,
            requiresPluck = (isHammerOn || isPullOff) ? false : note.requiresPluck,
            linkedFromNoteId = note.linkedFromNoteId,
            bendPoints = new List<RocksmithCachedBendPointData>(),
            techniqueSegments = new List<RocksmithCachedTechniqueSegmentData>()
        };

        if (note.bendPoints != null)
        {
            for (int i = 0; i < note.bendPoints.Count; i++)
            {
                ChartEditorBendPoint point = note.bendPoints[i];
                if (point == null)
                    continue;

                cached.bendPoints.Add(new RocksmithCachedBendPointData
                {
                    timeSeconds = point.timeSeconds,
                    step = point.step
                });
            }
        }

        if (note.techniqueSegments != null)
        {
            for (int i = 0; i < note.techniqueSegments.Count; i++)
            {
                ChartEditorTechniqueSegment segment = note.techniqueSegments[i];
                if (segment == null)
                    continue;

                cached.techniqueSegments.Add(new RocksmithCachedTechniqueSegmentData
                {
                    type = Mathf.Clamp((int)segment.type, 0, (int)NoteTechniqueSegmentType.Vibrato),
                    startOffset = segment.startOffset,
                    endOffset = segment.endOffset,
                    startFret = segment.startFret,
                    endFret = segment.endFret,
                    startBend = segment.startBend,
                    endBend = segment.endBend
                });
            }
        }

        return cached;
    }

    private static NoteTechnique ResolvePrimaryTechnique(ChartEditorNote note)
    {
        if (note == null)
            return NoteTechnique.None;

        if (note.technique == NoteTechnique.HammerOn || note.technique == NoteTechnique.PullOff)
            return note.technique;
        if (IsEditorTechniqueEnabled(note, NoteTechnique.Slide))
            return NoteTechnique.Slide;
        if (IsEditorTechniqueEnabled(note, NoteTechnique.Bend))
            return NoteTechnique.Bend;
        if (IsEditorTechniqueEnabled(note, NoteTechnique.Vibrato))
            return NoteTechnique.Vibrato;
        return NoteTechnique.None;
    }

    private static bool IsEditorTechniqueEnabled(ChartEditorNote note, NoteTechnique technique)
    {
        if (note == null)
            return false;

        if (note.technique == technique)
            return true;

        switch (technique)
        {
            case NoteTechnique.Slide:
                return note.slideTargetFret >= 0 || HasEditorTechniqueSegment(note, NoteTechniqueSegmentType.Slide);
            case NoteTechnique.Bend:
                return Mathf.Abs(note.bendStep) > 0.01f ||
                       note.bendPreBend ||
                       note.bendRelease ||
                       HasEditorTechniqueSegment(note, NoteTechniqueSegmentType.Bend) ||
                       HasBendBearingEditorTechniqueSegment(note);
            case NoteTechnique.Vibrato:
                return HasEditorTechniqueSegment(note, NoteTechniqueSegmentType.Vibrato);
            default:
                return false;
        }
    }

    private static bool HasEditorTechniqueSegment(ChartEditorNote note, NoteTechniqueSegmentType type)
    {
        return note?.techniqueSegments != null &&
               note.techniqueSegments.Any(segment => segment != null && segment.type == type);
    }

    private static bool HasBendBearingEditorTechniqueSegment(ChartEditorNote note)
    {
        if (note?.techniqueSegments == null)
            return false;

        return note.techniqueSegments.Any(segment =>
            segment != null &&
            (segment.type == NoteTechniqueSegmentType.Bend ||
             segment.type == NoteTechniqueSegmentType.Sustain ||
             segment.type == NoteTechniqueSegmentType.Vibrato) &&
            (Mathf.Abs(segment.startBend) > 0.01f || Mathf.Abs(segment.endBend) > 0.01f));
    }

    private static List<RocksmithCachedSectionData> BuildCachedSections(ChartEditorProject project)
    {
        List<RocksmithCachedSectionData> sections = new List<RocksmithCachedSectionData>();
        if (project?.sections == null)
            return sections;

        List<ChartEditorSection> ordered = project.sections
            .Where(section => section != null)
            .OrderBy(section => section.startTimeSeconds)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            ChartEditorSection source = ordered[i];
            sections.Add(new RocksmithCachedSectionData
            {
                name = string.IsNullOrWhiteSpace(source.name) ? $"Section {i + 1}" : source.name.Trim(),
                number = (short)i,
                timeSeconds = Mathf.Max(0f, (float)source.startTimeSeconds)
            });
        }

        return sections;
    }

    private static float EstimateAverageTempo(ChartEditorProject project)
    {
        ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: false);
        List<ChartEditorTempoRegion> regions = ChartEditorTimingService.GetTempoRegions(project);
        if (regions.Count == 0)
            return Mathf.Max(1f, (float)(project?.beatMap?.defaultTempoBpm ?? 120.0));

        double weighted = 0.0;
        double total = 0.0;
        for (int i = 0; i < regions.Count; i++)
        {
            ChartEditorTempoRegion region = regions[i];
            double length = Math.Max(0.0, region.endAudioTimeSeconds - region.startAudioTimeSeconds);
            weighted += region.bpm * length;
            total += length;
        }

        return Mathf.Max(1f, (float)(total > 0.0001 ? weighted / total : regions[0].bpm));
    }

    private static List<RocksmithCachedEbeatData> BuildEbeats(ChartEditorProject project, float duration)
    {
        ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: false);
        List<RocksmithCachedEbeatData> ebeats = new List<RocksmithCachedEbeatData>();
        List<ChartEditorBeatMarker> markers = ChartEditorTimingService.GetBeatMarkers(project)
            .Where(marker => marker != null && marker.audioTimeSeconds <= duration + 0.001)
            .OrderBy(marker => marker.audioTimeSeconds)
            .ToList();

        if (markers.Count > 0)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                ChartEditorBeatMarker marker = markers[i];
                ebeats.Add(new RocksmithCachedEbeatData
                {
                    timeSeconds = Mathf.Max(0f, (float)marker.audioTimeSeconds),
                    measure = marker.isDownbeat ? (short)Mathf.Max(0, marker.barNumber - 1) : (short)-1
                });
            }

            return ebeats;
        }

        float beatSeconds = 0.5f;
        int count = Mathf.Max(1, Mathf.CeilToInt(duration / beatSeconds) + 1);
        for (int i = 0; i < count; i++)
        {
            ebeats.Add(new RocksmithCachedEbeatData
            {
                timeSeconds = i * beatSeconds,
                measure = (short)Mathf.Max(0, i / 4)
            });
        }

        return ebeats;
    }

    private static string CopyAudioForExport(ChartEditorProject project, string exportDirectory)
    {
        string sourcePath = project.audio?.sourcePath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return string.Empty;

        string extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".audio";

        string destinationFileName = $"audio{extension.ToLowerInvariant()}";
        string destinationPath = Path.Combine(exportDirectory, destinationFileName);
        if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
            File.Copy(sourcePath, destinationPath, true);
        return destinationFileName;
    }

    private static string BuildProjectFileName(ChartEditorProject project)
    {
        string title = project?.metadata?.title;
        string artist = project?.metadata?.artist;
        string prefix = SanitizeFileName($"{FirstNonEmpty(artist, "Unknown")}_{FirstNonEmpty(title, "Chart")}");
        string id = string.IsNullOrWhiteSpace(project?.projectId) ? Guid.NewGuid().ToString("N") : project.projectId;
        return $"{prefix}_{id.Substring(0, Math.Min(8, id.Length))}";
    }

    private static string BuildExportDirectoryName(ChartEditorProject project)
    {
        string id = string.IsNullOrWhiteSpace(project?.projectId) ? Guid.NewGuid().ToString("N") : project.projectId;
        return ExportFolderPrefix + BuildProjectFileName(project) + "_" + id.Substring(0, Math.Min(6, id.Length));
    }

    private static string RouteForRole(ChartEditorTrackRole role)
    {
        switch (role)
        {
            case ChartEditorTrackRole.Bass:
                return "Bass";
            case ChartEditorTrackRole.RhythmGuitar:
                return "Rhythm";
            case ChartEditorTrackRole.Drums:
                return "Drums";
            case ChartEditorTrackRole.Piano:
                return "Piano";
            case ChartEditorTrackRole.Vocals:
                return "Vocals";
            case ChartEditorTrackRole.Custom:
                return "Custom";
            default:
                return "Lead";
        }
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

    private static int EstimateDifficulty(ChartEditorTrack track)
    {
        int count = track?.notes?.Count ?? 0;
        if (count <= 0)
            return 0;
        if (count < 80)
            return 1;
        if (count < 180)
            return 2;
        if (count < 360)
            return 3;
        if (count < 700)
            return 4;
        return 5;
    }

    private static long TryGetLastWriteTicks(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return 0L;

        try
        {
            return File.GetLastWriteTimeUtc(path).Ticks;
        }
        catch
        {
            return 0L;
        }
    }

    private static string SanitizeIdentifier(string value)
    {
        string sanitized = SanitizeFileName(value);
        return string.IsNullOrWhiteSpace(sanitized) ? Guid.NewGuid().ToString("N") : sanitized;
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

        string sanitized = new string(chars).Trim('_');
        while (sanitized.IndexOf("__", StringComparison.Ordinal) >= 0)
            sanitized = sanitized.Replace("__", "_");
        return string.IsNullOrWhiteSpace(sanitized) ? "chart" : sanitized;
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
