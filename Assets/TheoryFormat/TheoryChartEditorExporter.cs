using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

public static class TheoryChartEditorExporter
{
    public static bool ExportProject(ChartEditorProject project, string exportDirectory, out string packagePath, out string error)
    {
        packagePath = string.Empty;
        error = string.Empty;
        if (project == null)
        {
            error = "No chart editor project is loaded.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(exportDirectory))
        {
            error = "Export directory is empty.";
            return false;
        }

        try
        {
            bool preserveExistingGeneratedPlayback = !project.dirty;
            project.EnsureDefaults();
            ChartEditorRuntimeNoteSanitizer.SanitizeProjectNotes(project);
            ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: true);

            Directory.CreateDirectory(exportDirectory);
            packagePath = Path.Combine(exportDirectory, $"{BuildPackageFileName(project)}{TheoryPackageFormat.Extension}");
            TheoryPackageWriteRequest request = BuildWriteRequest(project, packagePath, preserveExistingGeneratedPlayback);
            return TheoryPackageIO.WritePackage(packagePath, request, out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool WriteProjectPackage(ChartEditorProject project, string packagePath, out string error)
    {
        error = string.Empty;
        if (project == null)
        {
            error = "No chart editor project is loaded.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(packagePath))
        {
            error = "Theory package path is empty.";
            return false;
        }

        try
        {
            bool preserveExistingGeneratedPlayback = !project.dirty;
            project.EnsureDefaults();
            ChartEditorRuntimeNoteSanitizer.SanitizeProjectNotes(project);
            ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: true);

            string directory = Path.GetDirectoryName(packagePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            TheoryPackageWriteRequest request = BuildWriteRequest(project, packagePath, preserveExistingGeneratedPlayback);
            return TheoryPackageIO.WritePackage(packagePath, request, out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static TheoryPackageWriteRequest BuildWriteRequest(
        ChartEditorProject project,
        string packagePath,
        bool preserveExistingGeneratedPlayback)
    {
        ChartEditorToneScopeService.NormalizeProjectToneGroups(project);
        bool preserveGeneratedPlaybackSet =
            preserveExistingGeneratedPlayback &&
            ProjectHasExistingGeneratedPlayback(project);
        float duration = Mathf.Max(project.DurationSeconds, 0.1f);
        List<TheoryArrangementData> arrangements = new List<TheoryArrangementData>();
        List<TheoryArrangementSummary> summaries = new List<TheoryArrangementSummary>();
        Dictionary<string, int> variantCounts = BuildVariantCounts(project);

        for (int i = 0; i < project.tracks.Count; i++)
        {
            ChartEditorTrack track = project.tracks[i];
            if (track == null)
                continue;

            track.EnsureDefaults();
            string arrangementId = SanitizeIdentifier(string.IsNullOrWhiteSpace(track.id) ? $"track_{i + 1}" : track.id);
            string groupId = BuildArrangementGroupId(track, arrangementId);
            bool hasDifficultyVariants = variantCounts.TryGetValue(groupId, out int variantCount) && variantCount > 1;
            TheoryArrangementData arrangement = BuildArrangement(
                project,
                track,
                arrangementId,
                groupId,
                hasDifficultyVariants,
                duration,
                preserveGeneratedPlaybackSet);
            string entry = TheoryPackageFormat.BuildArrangementEntryName(arrangementId);
            arrangements.Add(arrangement);
            summaries.Add(new TheoryArrangementSummary
            {
                arrangementId = arrangementId,
                displayName = arrangement.displayName,
                instrumentType = arrangement.instrumentType,
                route = arrangement.route,
                groupId = arrangement.groupId,
                groupDisplayName = arrangement.groupDisplayName,
                difficultyLabel = arrangement.difficultyLabel,
                difficultyUiIndex = arrangement.difficultyUiIndex,
                hasDifficultyVariants = arrangement.hasDifficultyVariants,
                entry = entry,
                noteCount = arrangement.notes?.Count ?? 0,
                tabCount = ResolveArrangementTabCount(track, arrangement),
                score = ResolveArrangementScore(track, arrangement),
                difficultyRating = arrangement.difficultyRating,
                tuningPitches = arrangement.tuningPitches != null ? (int[])arrangement.tuningPitches.Clone() : null,
                tuningDisplayName = arrangement.tuningDisplayName
            });
        }

        string audioEntry = BuildAudioEntry(project.audio?.sourcePath);
        string coverEntry = BuildCoverEntry(project.metadata?.coverImagePath);
        List<TheoryStemAsset> preservedStems = LoadPreservedStems(project, packagePath, out string preservedPackageSourcePath);
        List<string> preservedEntryNames = preservedStems
            .Where(stem => stem != null && !string.IsNullOrWhiteSpace(stem.entry))
            .Select(stem => stem.entry)
            .ToList();
        string toneLabMappingsSourcePath = FindPreservedPackageEntrySourcePath(project, packagePath, TheoryPackageFormat.ToneLabMappingsEntryName);
        if (!string.IsNullOrWhiteSpace(toneLabMappingsSourcePath) &&
            (string.IsNullOrWhiteSpace(preservedPackageSourcePath) ||
             string.Equals(Path.GetFullPath(preservedPackageSourcePath), Path.GetFullPath(toneLabMappingsSourcePath), StringComparison.OrdinalIgnoreCase)))
        {
            preservedPackageSourcePath = toneLabMappingsSourcePath;
            if (!preservedEntryNames.Any(entry => string.Equals(entry, TheoryPackageFormat.ToneLabMappingsEntryName, StringComparison.OrdinalIgnoreCase)))
                preservedEntryNames.Add(TheoryPackageFormat.ToneLabMappingsEntryName);
        }

        // Never silently strip embedded audio/cover on save: if the cached
        // source file no longer exists (e.g. the TheoryPackageCache extraction
        // was cleaned up), carry the existing package's entries forward
        // instead of writing a package without audio.
        TheoryAudioAsset preservedAudioAsset = null;
        if (string.IsNullOrWhiteSpace(audioEntry) || string.IsNullOrWhiteSpace(coverEntry))
        {
            List<string> preserveCandidates = new List<string>();
            AddPreservedPackageCandidate(preserveCandidates, project.sourcePath);
            AddPreservedPackageCandidate(preserveCandidates, packagePath);
            foreach (string candidate in preserveCandidates)
            {
                bool candidateUsable = string.IsNullOrWhiteSpace(preservedPackageSourcePath) ||
                                       string.Equals(Path.GetFullPath(preservedPackageSourcePath), Path.GetFullPath(candidate), StringComparison.OrdinalIgnoreCase);
                if (!candidateUsable ||
                    !TheoryPackageIO.TryReadManifest(candidate, out TheorySongManifest sourceManifest, out _) ||
                    sourceManifest == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(audioEntry) &&
                    !string.IsNullOrWhiteSpace(sourceManifest.primaryAudioEntry) &&
                    TheoryPackageIO.EntryExists(candidate, sourceManifest.primaryAudioEntry))
                {
                    audioEntry = sourceManifest.primaryAudioEntry;
                    preservedAudioAsset = sourceManifest.audio?.FirstOrDefault(asset =>
                        asset != null && string.Equals(asset.entry, sourceManifest.primaryAudioEntry, StringComparison.OrdinalIgnoreCase));
                    preservedPackageSourcePath = candidate;
                    if (!preservedEntryNames.Any(entry => string.Equals(entry, audioEntry, StringComparison.OrdinalIgnoreCase)))
                        preservedEntryNames.Add(audioEntry);
                    Debug.LogWarning("[ChartEditor] Audio source file is missing; preserving the embedded audio from the existing package instead of dropping it.");
                }

                if (string.IsNullOrWhiteSpace(coverEntry) &&
                    !string.IsNullOrWhiteSpace(sourceManifest.coverArtEntry) &&
                    TheoryPackageIO.EntryExists(candidate, sourceManifest.coverArtEntry))
                {
                    coverEntry = sourceManifest.coverArtEntry;
                    preservedPackageSourcePath = candidate;
                    if (!preservedEntryNames.Any(entry => string.Equals(entry, coverEntry, StringComparison.OrdinalIgnoreCase)))
                        preservedEntryNames.Add(coverEntry);
                }

                if (!string.IsNullOrWhiteSpace(audioEntry) && !string.IsNullOrWhiteSpace(coverEntry))
                    break;
            }
        }

        DateTime now = DateTime.UtcNow;

        // Deterministic timestamps so a no-edit re-export produces an
        // identical package: creation time is carried from the existing
        // package, and modified time is the project's last save time (not the
        // moment the export button was pressed).
        long createdTicks = now.Ticks;
        if (TheoryPackageIO.TryReadManifest(packagePath, out TheorySongManifest priorManifest, out _) &&
            priorManifest != null &&
            priorManifest.createdAtUtcTicks > 0)
        {
            createdTicks = priorManifest.createdAtUtcTicks;
        }

        long modifiedTicks = ResolveDeterministicModifiedTicks(project);

        TheorySongManifest manifest = new TheorySongManifest
        {
            formatId = TheoryPackageFormat.FormatId,
            schemaVersion = TheoryPackageFormat.SchemaVersion,
            packageId = string.IsNullOrWhiteSpace(project.projectId) ? Guid.NewGuid().ToString("N") : project.projectId,
            createdAtUtcTicks = createdTicks,
            modifiedAtUtcTicks = modifiedTicks,
            title = string.IsNullOrWhiteSpace(project.metadata?.title) ? "Edited Chart" : project.metadata.title.Trim(),
            artist = project.metadata?.artist ?? string.Empty,
            album = project.metadata?.album ?? string.Empty,
            subtitle = "Chart Editor",
            genre = project.metadata?.genre ?? string.Empty,
            year = project.metadata?.year ?? string.Empty,
            defaultArrangementId = ResolveDefaultArrangementId(project, summaries),
            primaryAudioEntry = audioEntry,
            coverArtEntry = coverEntry,
            durationSeconds = duration,
            difficultyRating = summaries.Count > 0 ? Mathf.Clamp(Mathf.RoundToInt((float)summaries.Average(summary => summary.difficultyRating)), 0, 5) : 0,
            provenance = new TheoryImportProvenance
            {
                sourceType = "chart-editor",
                sourceDisplayName = project.metadata?.title ?? string.Empty,
                sourcePath = project.sourcePath ?? string.Empty,
                sourceLastWriteUtcTicks = TryGetLastWriteUtcTicks(project.sourcePath),
                sourceSizeBytes = TryGetFileSize(project.sourcePath),
                importedAtUtcTicks = modifiedTicks,
                converterName = "String Theory Chart Editor",
                converterVersion = TheoryPackageFormat.SchemaVersion.ToString()
            },
            arrangements = summaries,
            audio = new List<TheoryAudioAsset>(),
            stems = preservedStems
        };

        if (!string.IsNullOrWhiteSpace(audioEntry))
        {
            manifest.audio.Add(preservedAudioAsset != null
                ? new TheoryAudioAsset
                {
                    id = string.IsNullOrWhiteSpace(preservedAudioAsset.id) ? "full" : preservedAudioAsset.id,
                    entry = audioEntry,
                    displayName = preservedAudioAsset.displayName ?? string.Empty,
                    role = string.IsNullOrWhiteSpace(preservedAudioAsset.role) ? "full" : preservedAudioAsset.role,
                    contentType = preservedAudioAsset.contentType ?? string.Empty,
                    sourceSizeBytes = preservedAudioAsset.sourceSizeBytes,
                    defaultForPlayback = true
                }
                : new TheoryAudioAsset
                {
                    id = "full",
                    entry = audioEntry,
                    displayName = string.IsNullOrWhiteSpace(project.audio?.displayName)
                        ? Path.GetFileName(project.audio?.sourcePath)
                        : project.audio.displayName,
                    role = "full",
                    contentType = BuildAudioContentType(project.audio?.sourcePath),
                    sourceSizeBytes = TryGetFileSize(project.audio?.sourcePath),
                    defaultForPlayback = true
                });
        }

        return new TheoryPackageWriteRequest
        {
            manifest = manifest,
            arrangements = arrangements,
            editorState = BuildEditorState(project),
            toneLabMappingState = BuildToneLabMappingState(project),
            primaryAudioSourcePath = project.audio?.sourcePath,
            coverArtSourcePath = project.metadata?.coverImagePath,
            preservedPackageSourcePath = preservedPackageSourcePath,
            preservedEntryNames = preservedEntryNames
        };
    }

    private static List<TheoryStemAsset> LoadPreservedStems(ChartEditorProject project, string packagePath, out string packageSourcePath)
    {
        packageSourcePath = string.Empty;
        List<string> candidates = new List<string>();
        AddPreservedPackageCandidate(candidates, project?.sourcePath);
        AddPreservedPackageCandidate(candidates, packagePath);

        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            string sourcePath = candidates[candidateIndex];
            if (!TheoryPackageIO.TryReadManifest(sourcePath, out TheorySongManifest sourceManifest, out _) ||
                sourceManifest?.stems == null ||
                sourceManifest.stems.Count == 0)
            {
                continue;
            }

            List<TheoryStemAsset> stems = new List<TheoryStemAsset>();
            for (int i = 0; i < sourceManifest.stems.Count; i++)
            {
                TheoryStemAsset stem = sourceManifest.stems[i];
                if (stem == null ||
                    string.IsNullOrWhiteSpace(stem.id) ||
                    string.IsNullOrWhiteSpace(stem.entry) ||
                    !TheoryPackageIO.EntryExists(sourcePath, stem.entry))
                {
                    continue;
                }

                stems.Add(new TheoryStemAsset
                {
                    id = stem.id,
                    displayName = stem.displayName ?? string.Empty,
                    entry = stem.entry,
                    contentType = stem.contentType ?? string.Empty,
                    sourceSizeBytes = stem.sourceSizeBytes,
                    provider = string.IsNullOrWhiteSpace(stem.provider) ? "demucs" : stem.provider,
                    model = stem.model ?? string.Empty,
                    generatedAtUtcTicks = stem.generatedAtUtcTicks
                });
            }

            if (stems.Count > 0)
            {
                packageSourcePath = sourcePath;
                return stems;
            }
        }

        return new List<TheoryStemAsset>();
    }

    private static void AddPreservedPackageCandidate(List<string> candidates, string path)
    {
        if (candidates == null ||
            !TheoryPackageFormat.IsPackagePath(path) ||
            !File.Exists(path))
        {
            return;
        }

        string fullPath = Path.GetFullPath(path);
        if (candidates.Any(candidate => string.Equals(Path.GetFullPath(candidate), fullPath, StringComparison.OrdinalIgnoreCase)))
            return;

        candidates.Add(path);
    }

    private static string FindPreservedPackageEntrySourcePath(ChartEditorProject project, string packagePath, string entryName)
    {
        List<string> candidates = new List<string>();
        AddPreservedPackageCandidate(candidates, project?.sourcePath);
        AddPreservedPackageCandidate(candidates, packagePath);

        for (int i = 0; i < candidates.Count; i++)
        {
            string candidate = candidates[i];
            if (TheoryPackageIO.EntryExists(candidate, entryName))
                return candidate;
        }

        return string.Empty;
    }

    private static Dictionary<string, int> BuildVariantCounts(ChartEditorProject project)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (project?.tracks == null)
            return counts;

        for (int i = 0; i < project.tracks.Count; i++)
        {
            ChartEditorTrack track = project.tracks[i];
            if (track == null)
                continue;

            string arrangementId = SanitizeIdentifier(string.IsNullOrWhiteSpace(track.id) ? $"track_{i + 1}" : track.id);
            string groupId = BuildArrangementGroupId(track, arrangementId);
            counts.TryGetValue(groupId, out int count);
            counts[groupId] = count + 1;
        }

        return counts;
    }

    private static bool ProjectHasExistingGeneratedPlayback(ChartEditorProject project)
    {
        return project?.tracks != null &&
               project.tracks.Any(track => track?.generatedNotes != null && track.generatedNotes.Count > 0);
    }

    private static string BuildArrangementGroupId(ChartEditorTrack track, string arrangementId)
    {
        string groupId = FirstNonEmpty(track?.arrangementGroupId, arrangementId);
        return SanitizeIdentifier(groupId);
    }

    private static int ResolveArrangementScore(ChartEditorTrack track, TheoryArrangementData arrangement)
    {
        if (track != null && track.importedSelectionScore >= 0)
            return Mathf.Max(0, track.importedSelectionScore);

        return Mathf.Max(0, arrangement?.notes?.Count ?? 0);
    }

    private static int ResolveArrangementTabCount(ChartEditorTrack track, TheoryArrangementData arrangement)
    {
        if (track != null && track.importedTabCount >= 0)
            return Mathf.Max(0, track.importedTabCount);

        return Mathf.Max(0, arrangement?.notes?.Count ?? 0);
    }

    private static string ResolveDefaultArrangementId(ChartEditorProject project, List<TheoryArrangementSummary> summaries)
    {
        if (summaries == null || summaries.Count == 0)
            return string.Empty;

        string explicitDefault = project?.metadata?.defaultArrangementId;
        if (!string.IsNullOrWhiteSpace(explicitDefault) &&
            summaries.Any(summary => string.Equals(summary?.arrangementId ?? string.Empty, explicitDefault, StringComparison.OrdinalIgnoreCase)))
        {
            return explicitDefault;
        }

        string selectedTrackId = project?.selectedTrackId;
        if (!string.IsNullOrWhiteSpace(selectedTrackId))
        {
            string selectedArrangementId = SanitizeIdentifier(selectedTrackId);
            if (summaries.Any(summary => string.Equals(summary?.arrangementId ?? string.Empty, selectedArrangementId, StringComparison.OrdinalIgnoreCase)))
                return selectedArrangementId;
        }

        return summaries
            .OrderBy(summary => NormalizeDifficultyUiIndex(summary?.difficultyUiIndex ?? -1, summary?.difficultyLabel))
            .ThenByDescending(summary => summary?.score ?? 0)
            .FirstOrDefault()?.arrangementId ?? summaries[0].arrangementId;
    }

    private static TheoryArrangementData BuildArrangement(
        ChartEditorProject project,
        ChartEditorTrack track,
        string arrangementId,
        string groupId,
        bool hasDifficultyVariants,
        float duration,
        bool preserveExistingGeneratedPlayback)
    {
        List<ChartEditorNote> orderedNotes = ChartEditorRuntimeNoteSanitizer.PrepareChartNotesForRuntime(track.notes?
            .Where(note => note != null)
            .OrderBy(note => note.timeSeconds)
            .ThenBy(note => note.stringOrLane)
            .ToList() ?? new List<ChartEditorNote>(), !track.preserveImportedRuntimeNotes);

        TheoryArrangementData arrangement = new TheoryArrangementData
        {
            schemaVersion = TheoryPackageFormat.SchemaVersion,
            arrangementId = arrangementId,
            displayName = track.displayName ?? track.importedName ?? arrangementId,
            instrumentType = FirstNonEmpty(track.arrangementInstrumentType, InstrumentTypeForRole(track.role)),
            route = FirstNonEmpty(track.arrangementRoute, RouteForRole(track.role)),
            groupId = groupId,
            groupDisplayName = FirstNonEmpty(track.arrangementGroupDisplayName, track.displayName, track.importedName, groupId),
            difficultyLabel = NormalizeDifficultyLabel(track.difficultyLabel, track.difficultyUiIndex),
            difficultyUiIndex = NormalizeDifficultyUiIndex(track.difficultyUiIndex, track.difficultyLabel),
            hasDifficultyVariants = hasDifficultyVariants,
            durationSeconds = duration,
            difficultyRating = EstimateDifficulty(track),
            preserveImportedRuntimeNotes = track.preserveImportedRuntimeNotes,
            tuningPitches = track.tuning?.stringPitches != null ? (int[])track.tuning.stringPitches.Clone() : null,
            tuningDisplayName = track.tuning?.displayName ?? string.Empty,
            timing = new TheoryTimingData
            {
                averageTempoBpm = EstimateAverageTempo(project),
                capo = 0,
                beats = BuildBeats(project, duration),
                sections = BuildSections(project)
            },
            tones = BuildToneData(track),
            generatedPart = BuildGeneratedPart(track, arrangementId),
            notes = new List<TheoryNoteData>(),
            arpeggioGuides = BuildArpeggioGuides(track),
            generatedChannels = BuildGeneratedChannels(track),
            generatedNotes = BuildGeneratedNotes(track, arrangementId, preserveExistingGeneratedPlayback)
        };

        // Editor-added notes (sourceNoteId = -1) must not fall back to their
        // list index: imported notes already own the sequential loader ids
        // 0..N-1, so an index id would collide with an imported note and
        // corrupt legato links and scoring lookups keyed by note id.
        int nextGeneratedNoteId = orderedNotes.Count;
        for (int i = 0; i < orderedNotes.Count; i++)
        {
            ChartEditorNote candidate = orderedNotes[i];
            if (candidate != null && candidate.sourceNoteId >= nextGeneratedNoteId)
                nextGeneratedNoteId = candidate.sourceNoteId + 1;
        }

        for (int i = 0; i < orderedNotes.Count; i++)
        {
            ChartEditorNote note = orderedNotes[i];
            int noteId = note.sourceNoteId >= 0 ? note.sourceNoteId : nextGeneratedNoteId++;
            arrangement.notes.Add(ToTheoryNote(note, noteId));
        }

        return arrangement;
    }

    private static TheoryToneData BuildToneData(ChartEditorTrack track)
    {
        TheoryToneData result = new TheoryToneData
        {
            baseToneName = track?.tones?.baseToneName ?? string.Empty,
            changes = new List<TheoryToneChangeData>(),
            definitions = new List<TheoryToneDefinitionData>()
        };

        if (track?.tones?.changes != null)
        {
            for (int i = 0; i < track.tones.changes.Count; i++)
            {
                ChartEditorToneChange source = track.tones.changes[i];
                if (source == null)
                    continue;

                ChartEditorToneDefinition definitionByName = FindToneDefinition(track, source.toneName);
                ChartEditorToneDefinition definition = definitionByName ?? ResolveToneDefinitionForChange(track, source);
                result.changes.Add(new TheoryToneChangeData
                {
                    timeSeconds = Mathf.Max(0f, source.timeSeconds),
                    toneName = definitionByName != null
                        ? source.toneName ?? string.Empty
                        : FirstNonEmpty(definition?.name, source.toneName, definition?.key),
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

                result.definitions.Add(new TheoryToneDefinitionData
                {
                    name = source.name ?? string.Empty,
                    key = source.key ?? string.Empty,
                    preset = ToTheoryTonePreset(source.preset),
                    fallback = ToTheoryToneFallback(source.fallback)
                });
            }
        }

        result.EnsureDefaults();
        return result;
    }

    private static ChartEditorToneDefinition ResolveToneDefinitionForChange(ChartEditorTrack track, ChartEditorToneChange change)
    {
        if (track?.tones?.definitions == null || track.tones.definitions.Count == 0 || change == null)
            return null;

        ChartEditorToneDefinition byName = FindToneDefinition(track, change.toneName);
        if (byName != null)
            return byName;

        if (change.toneId >= 0 && change.toneId < track.tones.definitions.Count)
            return track.tones.definitions[change.toneId];

        return null;
    }

    private static ChartEditorToneDefinition FindToneDefinition(ChartEditorTrack track, string toneName)
    {
        if (track?.tones?.definitions == null || string.IsNullOrWhiteSpace(toneName))
            return null;

        string normalized = toneName.Trim();
        return track.tones.definitions.FirstOrDefault(definition =>
            definition != null &&
            (string.Equals(definition.name ?? string.Empty, normalized, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(definition.key ?? string.Empty, normalized, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(definition.preset?.presetName ?? string.Empty, normalized, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(definition.preset?.presetId ?? string.Empty, normalized, StringComparison.OrdinalIgnoreCase)));
    }

    private static TheoryTonePresetData ToTheoryTonePreset(ChartEditorTonePresetData source)
    {
        TheoryTonePresetData result = new TheoryTonePresetData
        {
            presetId = source?.presetId ?? string.Empty,
            presetName = source?.presetName ?? string.Empty,
            inputGainDb = source?.inputGainDb ?? 0f,
            outputGainDb = source?.outputGainDb ?? 0f,
            pedalChain = new List<TheoryTonePedalSlotData>()
        };

        if (source?.pedalChain != null)
        {
            for (int i = 0; i < source.pedalChain.Count; i++)
            {
                ChartEditorTonePedalSlotData slot = source.pedalChain[i];
                if (slot == null)
                    continue;

                result.pedalChain.Add(new TheoryTonePedalSlotData
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

    private static TheoryToneFallbackData ToTheoryToneFallback(ChartEditorToneFallbackData source)
    {
        return new TheoryToneFallbackData
        {
            preferredPresetName = source?.preferredPresetName ?? string.Empty,
            searchText = source?.searchText ?? string.Empty
        };
    }

    private static TheoryToneLabMappingState BuildToneLabMappingState(ChartEditorProject project)
    {
        TheoryToneLabMappingState state = new TheoryToneLabMappingState
        {
            schemaVersion = TheoryPackageFormat.SchemaVersion,
            modifiedAtUtcTicks = ResolveDeterministicModifiedTicks(project),
            mappings = new List<TheoryToneLabPresetMappingData>()
        };

        if (project?.tracks == null)
        {
            state.EnsureDefaults();
            return state;
        }

        Dictionary<string, TheoryToneLabPresetMappingData> mappings = new Dictionary<string, TheoryToneLabPresetMappingData>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < project.tracks.Count; i++)
        {
            ChartEditorTrack track = project.tracks[i];
            if (track?.tones?.definitions == null)
                continue;

            string arrangementId = SanitizeIdentifier(string.IsNullOrWhiteSpace(track.id) ? $"track_{i + 1}" : track.id);
            string groupId = BuildArrangementGroupId(track, arrangementId);
            if (string.IsNullOrWhiteSpace(groupId))
                continue;

            for (int definitionIndex = 0; definitionIndex < track.tones.definitions.Count; definitionIndex++)
            {
                ChartEditorToneDefinition definition = track.tones.definitions[definitionIndex];
                if (definition == null || string.IsNullOrWhiteSpace(definition.name) || !HasUsableTonePreset(definition.preset))
                    continue;

                string toneName = definition.name.Trim();
                TheoryTonePresetData presetSnapshot = ToTheoryTonePreset(definition.preset);
                string key = $"{groupId}\n{toneName}";
                mappings[key] = new TheoryToneLabPresetMappingData
                {
                    arrangementId = groupId,
                    toneName = toneName,
                    presetId = FirstNonEmpty(definition.preset.presetId, definition.key, definition.name),
                    presetSnapshot = presetSnapshot
                };
            }
        }

        state.mappings = mappings.Values
            .OrderBy(mapping => mapping.arrangementId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mapping => mapping.toneName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        state.EnsureDefaults();
        return state;
    }

    private static bool HasUsableTonePreset(ChartEditorTonePresetData preset)
    {
        return preset?.pedalChain != null && preset.pedalChain.Count > 0;
    }

    private static TheoryGeneratedPartInfo BuildGeneratedPart(ChartEditorTrack track, string arrangementId)
    {
        string displayName = track?.displayName ?? track?.importedName ?? arrangementId;
        string fallbackInstrument = InstrumentNameForRole(track?.role ?? ChartEditorTrackRole.LeadGuitar);
        bool fallbackIsDrum = track?.role == ChartEditorTrackRole.Drums;
        bool fallbackIsGuitarFamily = IsGuitarFamilyRole(track?.role ?? ChartEditorTrackRole.LeadGuitar);
        int fallbackProgram = DefaultMidiProgramForRole(track?.role ?? ChartEditorTrackRole.LeadGuitar);
        int fallbackChannel = fallbackIsDrum ? 9 : 0;
        ChartEditorGeneratedPartInfo source = HasGeneratedPartOverride(track) ? track.generatedPart : null;

        return new TheoryGeneratedPartInfo
        {
            partId = FirstNonEmpty(source?.partId, arrangementId),
            displayName = !string.IsNullOrWhiteSpace(source?.displayName) ? source.displayName : FirstNonEmpty(displayName),
            instrumentName = !string.IsNullOrWhiteSpace(source?.instrumentName) ? source.instrumentName : fallbackInstrument,
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

    private static List<TheoryGeneratedChannelAssignment> BuildGeneratedChannels(ChartEditorTrack track)
    {
        List<TheoryGeneratedChannelAssignment> result = new List<TheoryGeneratedChannelAssignment>();
        if (track?.generatedChannels == null || track.generatedChannels.Count == 0)
            return result;

        HashSet<string> seenRoutes = new HashSet<string>(StringComparer.Ordinal);
        foreach (ChartEditorGeneratedChannelAssignment source in track.generatedChannels
                     .Where(channel => channel != null)
                     .OrderBy(channel => channel.channel)
                     .ThenBy(channel => channel.sourcePartId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(channel => channel.sourcePartName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(channel => channel.label ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            string routeKey = string.Join("\n",
                source.channel.ToString(CultureInfo.InvariantCulture),
                source.bank.ToString(CultureInfo.InvariantCulture),
                source.preset.ToString(CultureInfo.InvariantCulture),
                source.isDrum.ToString(),
                source.label ?? string.Empty,
                source.sourcePartId ?? string.Empty,
                source.sourcePartName ?? string.Empty,
                Mathf.Max(0, source.pitchBendRangeSemitones).ToString(CultureInfo.InvariantCulture));
            if (!seenRoutes.Add(routeKey))
                continue;

            result.Add(new TheoryGeneratedChannelAssignment
            {
                channel = source.channel,
                bank = source.bank,
                preset = source.preset,
                isDrum = source.isDrum,
                label = source.label,
                sourcePartId = source.sourcePartId,
                sourcePartName = source.sourcePartName,
                pitchBendRangeSemitones = Mathf.Max(0, source.pitchBendRangeSemitones)
            });
        }

        return result;
    }

    private static List<TheoryGeneratedNoteEvent> BuildGeneratedNotes(
        ChartEditorTrack track,
        string arrangementId,
        bool preserveExistingGeneratedPlayback)
    {
        List<TheoryGeneratedNoteEvent> result = new List<TheoryGeneratedNoteEvent>();
        bool hasExistingGeneratedPlayback = track?.generatedNotes != null && track.generatedNotes.Count > 0;
        List<ChartEditorGeneratedNoteEvent> sourceNotes;
        if (preserveExistingGeneratedPlayback)
        {
            sourceNotes = hasExistingGeneratedPlayback
                ? track.generatedNotes
                : new List<ChartEditorGeneratedNoteEvent>();
        }
        else
        {
            sourceNotes = ChartEditorGeneratedPlaybackIntegrity.CanReuseGeneratedPlayback(track)
                ? track.generatedNotes
                : ChartEditorGeneratedPlaybackBuilder.BuildFromChartNotes(track, arrangementId);
        }

        for (int i = 0; i < sourceNotes.Count; i++)
        {
            ChartEditorGeneratedNoteEvent source = sourceNotes[i];
            if (source == null)
                continue;

            TheoryGeneratedNoteEvent note = new TheoryGeneratedNoteEvent
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
                pitchCurve = new List<TheoryGeneratedPitchPoint>()
            };

            if (source.pitchCurve != null)
            {
                for (int pointIndex = 0; pointIndex < source.pitchCurve.Count; pointIndex++)
                {
                    ChartEditorGeneratedPitchPoint point = source.pitchCurve[pointIndex];
                    if (point == null)
                        continue;

                    note.pitchCurve.Add(new TheoryGeneratedPitchPoint
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

    private static TheoryNoteData ToTheoryNote(ChartEditorNote note, int noteId)
    {
        NoteTechnique primaryTechnique = ResolvePrimaryTechnique(note);
        bool isHammerOn = IsEditorTechniqueEnabled(note, NoteTechnique.HammerOn);
        bool isPullOff = IsEditorTechniqueEnabled(note, NoteTechnique.PullOff);
        bool hasVibrato = IsEditorTechniqueEnabled(note, NoteTechnique.Vibrato);
        // Palm mute is exported verbatim: the old fallback that promoted any
        // muted note to a palm mute turned dead/x notes into PM notes on the
        // first save (imports now set palmMute properly, so the guess is
        // never needed).
        bool palmMute = note.palmMute;
        bool muted = note.muted || note.palmMute || note.fretHandMute;
        bool runtimeMuted = note.hasRuntimeMuted ? note.runtimeMuted : note.muted;
        bool runtimePalmMute = note.hasRuntimePalmMute ? note.runtimePalmMute : note.palmMute;

        TheoryNoteData result = new TheoryNoteData
        {
            id = noteId,
            time = Mathf.Max(0f, (float)note.timeSeconds),
            duration = Mathf.Max(0f, (float)note.durationSeconds),
            stringIndex = Mathf.Clamp(note.stringOrLane, 0, 7),
            fret = Mathf.Max(0, note.fret),
            noteName = note.noteName ?? string.Empty,
            chordId = note.chordId,
            chordName = note.chordName ?? string.Empty,
            primaryTechnique = Mathf.Clamp((int)primaryTechnique, 0, (int)NoteTechnique.Vibrato),
            slideTargetFret = note.slideTargetFret,
            bendStep = note.bendStep,
            bendVisualStartTime = note.bendVisualStartTime,
            bendVisualDuration = note.bendVisualDuration,
            bendPreBend = note.bendPreBend,
            bendRelease = note.bendRelease,
            muted = muted,
            palmMute = palmMute,
            fretHandMute = note.fretHandMute,
            hasRuntimeMuted = true,
            runtimeMuted = runtimeMuted,
            hasRuntimePalmMute = true,
            runtimePalmMute = runtimePalmMute,
            harmonic = note.harmonic,
            accent = note.accent,
            tap = note.tap,
            tremolo = note.tremolo,
            pinchHarmonic = note.pinchHarmonic,
            hammerOn = isHammerOn,
            pullOff = isPullOff,
            hopo = isHammerOn || isPullOff,
            vibrato = hasVibrato,
            vibratoStrength = note.vibratoStrength,
            maxBend = Mathf.Max(note.maxBend, note.bendStep),
            legato = note.legato,
            requiresPluck = note.requiresPluck,
            linkedFromNoteId = note.linkedFromNoteId,
            bendPoints = new List<TheoryBendPointData>(),
            techniqueSegments = new List<TheoryTechniqueSegmentData>()
        };

        if (note.bendPoints != null)
        {
            for (int i = 0; i < note.bendPoints.Count; i++)
            {
                ChartEditorBendPoint point = note.bendPoints[i];
                if (point == null)
                    continue;

                result.bendPoints.Add(new TheoryBendPointData
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

                result.techniqueSegments.Add(new TheoryTechniqueSegmentData
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

        return result;
    }

    private static TheoryEditorState BuildEditorState(ChartEditorProject project)
    {
        TheoryEditorState state = new TheoryEditorState
        {
            schemaVersion = TheoryPackageFormat.SchemaVersion,
            selectedArrangementId = project.selectedTrackId ?? string.Empty,
            cursorTimeSeconds = project.cursorTimeSeconds,
            snapSeconds = project.settings?.snapSeconds ?? 0.05,
            snapEnabled = project.settings == null || project.settings.snapEnabled,
            largeNudgeSeconds = project.settings?.largeNudgeSeconds ?? 0.1,
            smallNudgeSeconds = project.settings?.smallNudgeSeconds ?? 0.01,
            showBeatGrid = project.settings == null || project.settings.showBeatGrid,
            metronomeEnabled = project.settings?.metronomeEnabled ?? false,
            noteClapsEnabled = project.settings?.noteClapsEnabled ?? false,
            playbackSpeed = project.settings?.playbackSpeed ?? 1f,
            beatMarkers = new List<TheoryEditorBeatMarker>(),
            syncPoints = new List<TheoryEditorSyncPoint>()
        };

        if (project.beatMap?.beatMarkers != null)
        {
            List<ChartEditorBeatMarker> markers = project.beatMap.beatMarkers
                .Where(marker => marker != null)
                .OrderBy(marker => marker.beatPosition)
                .ToList();
            for (int i = 0; i < markers.Count; i++)
            {
                ChartEditorBeatMarker marker = markers[i];
                ChartEditorTimeSignatureChange signature = GetTimeSignatureAtBeat(project, marker.beatPosition);
                state.beatMarkers.Add(new TheoryEditorBeatMarker
                {
                    id = marker.id,
                    beatPosition = marker.beatPosition,
                    audioTimeSeconds = marker.audioTimeSeconds,
                    barNumber = marker.barNumber,
                    isDownbeat = marker.isDownbeat,
                    isAnchor = marker.isAnchor,
                    label = marker.label ?? string.Empty,
                    // Prefer the marker's own bpm: for the last anchor the
                    // region lookup returns the PREVIOUS region's tempo, which
                    // would drop the trailing tempo set via probe drags and
                    // corrupt the grid after reload.
                    bpm = marker.bpm > 0.0 ? marker.bpm : GetTempoAtBeat(project, marker.beatPosition),
                    timeSignatureNumerator = signature?.numerator ?? 4,
                    timeSignatureDenominator = signature?.denominator ?? 4,
                    generatedBySynchTheory = marker.generatedBySynchTheory,
                    synchTheoryConfidence = marker.synchTheoryConfidence,
                    synchTheorySource = marker.synchTheorySource ?? string.Empty,
                    locked = marker.locked,
                    linkedSectionId = marker.linkedSectionId ?? string.Empty
                });
            }
        }

        if (project.syncPoints != null)
        {
            for (int i = 0; i < project.syncPoints.Count; i++)
            {
                ChartEditorSyncPoint point = project.syncPoints[i];
                if (point == null)
                    continue;

                state.syncPoints.Add(new TheoryEditorSyncPoint
                {
                    id = point.id,
                    chartTimeSeconds = point.chartTimeSeconds,
                    audioTimeSeconds = point.audioTimeSeconds,
                    label = point.name,
                    locked = point.locked,
                    linkedSectionId = point.linkedSectionId ?? string.Empty
                });
            }
        }

        return state;
    }

    private static List<TheoryBeatData> BuildBeats(ChartEditorProject project, float duration)
    {
        ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: false);
        List<TheoryBeatData> beats = new List<TheoryBeatData>();
        List<ChartEditorBeatMarker> markers = ChartEditorTimingService.GetBeatMarkers(project)
            .Where(marker => marker != null && marker.audioTimeSeconds <= duration + 0.001)
            .OrderBy(marker => marker.audioTimeSeconds)
            .ToList();

        if (markers.Count > 0)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                ChartEditorBeatMarker marker = markers[i];
                beats.Add(new TheoryBeatData
                {
                    timeSeconds = Mathf.Max(0f, (float)marker.audioTimeSeconds),
                    measure = marker.isDownbeat ? (short)Mathf.Max(0, marker.barNumber - 1) : (short)-1
                });
            }

            return beats;
        }

        float beatSeconds = 0.5f;
        int count = Mathf.Max(1, Mathf.CeilToInt(duration / beatSeconds) + 1);
        for (int i = 0; i < count; i++)
        {
            beats.Add(new TheoryBeatData
            {
                timeSeconds = i * beatSeconds,
                measure = (short)Mathf.Max(0, i / 4)
            });
        }

        return beats;
    }

    private static List<TheorySectionData> BuildSections(ChartEditorProject project)
    {
        List<TheorySectionData> sections = new List<TheorySectionData>();
        if (project?.sections == null)
            return sections;

        List<ChartEditorSection> ordered = project.sections
            .Where(section => section != null)
            .OrderBy(section => section.startTimeSeconds)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            ChartEditorSection source = ordered[i];
            sections.Add(new TheorySectionData
            {
                name = string.IsNullOrWhiteSpace(source.name) ? $"Section {i + 1}" : source.name.Trim(),
                number = (short)i,
                timeSeconds = Mathf.Max(0f, (float)source.startTimeSeconds)
            });
        }

        return sections;
    }

    private static List<TheoryArpeggioGuideData> BuildArpeggioGuides(ChartEditorTrack track)
    {
        List<TheoryArpeggioGuideData> guides = new List<TheoryArpeggioGuideData>();
        if (track?.arpeggioGuides == null)
            return guides;

        for (int i = 0; i < track.arpeggioGuides.Count; i++)
        {
            ChartEditorArpeggioGuide source = track.arpeggioGuides[i];
            if (source == null)
                continue;

            guides.Add(new TheoryArpeggioGuideData
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

    private static NoteTechnique ResolvePrimaryTechnique(ChartEditorNote note)
    {
        if (note == null)
            return NoteTechnique.None;
        if (note.technique != NoteTechnique.None)
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

    private static double GetTempoAtBeat(ChartEditorProject project, double beatPosition)
    {
        List<ChartEditorTempoRegion> regions = ChartEditorTimingService.GetTempoRegions(project);
        for (int i = 0; i < regions.Count; i++)
        {
            ChartEditorTempoRegion region = regions[i];
            if (beatPosition >= region.startBeat && beatPosition <= region.endBeat)
                return region.bpm;
        }

        return Math.Max(1.0, project?.beatMap?.defaultTempoBpm ?? 120.0);
    }

    private static ChartEditorTimeSignatureChange GetTimeSignatureAtBeat(ChartEditorProject project, double beatPosition)
    {
        if (project?.beatMap?.timeSignatures == null || project.beatMap.timeSignatures.Count == 0)
            return null;

        return project.beatMap.timeSignatures
            .Where(signature => signature != null && signature.beatPosition <= beatPosition + 0.0001)
            .OrderByDescending(signature => signature.beatPosition)
            .FirstOrDefault();
    }

    private static string BuildAudioEntry(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return string.Empty;

        string extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".audio";
        return TheoryPackageFormat.BuildAudioEntryName($"full{extension.ToLowerInvariant()}");
    }

    private static string BuildCoverEntry(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return string.Empty;

        string extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".image";
        return TheoryPackageFormat.BuildAssetEntryName($"cover{extension.ToLowerInvariant()}");
    }

    private static string BuildAudioContentType(string sourcePath)
    {
        string extension = Path.GetExtension(sourcePath)?.ToLowerInvariant() ?? string.Empty;
        switch (extension)
        {
            case ".ogg": return "audio/ogg";
            case ".mp3": return "audio/mpeg";
            case ".wav": return "audio/wav";
            case ".flac": return "audio/flac";
            case ".m4a": return "audio/mp4";
            case ".aif":
            case ".aiff": return "audio/aiff";
            default: return "application/octet-stream";
        }
    }

    private static long TryGetFileSize(string path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? new FileInfo(path).Length : 0L;
        }
        catch
        {
            return 0L;
        }
    }

    // Exports must be reproducible: a no-edit re-export should produce an
    // identical package, so "modified" derives from the project's last save
    // time rather than the wall clock at export.
    private static long ResolveDeterministicModifiedTicks(ChartEditorProject project)
    {
        long ticks = TryGetLastWriteUtcTicks(project?.savedProjectPath);
        return ticks > 0 ? ticks : DateTime.UtcNow.Ticks;
    }

    private static long TryGetLastWriteUtcTicks(string path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0L;
        }
        catch
        {
            return 0L;
        }
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

    private static string InstrumentTypeForRole(ChartEditorTrackRole role)
    {
        switch (role)
        {
            case ChartEditorTrackRole.Bass:
                return "bass";
            case ChartEditorTrackRole.Drums:
                return "drums";
            case ChartEditorTrackRole.Piano:
                return "piano";
            case ChartEditorTrackRole.Vocals:
                return "vocals";
            default:
                return "guitar";
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

    private static string BuildPackageFileName(ChartEditorProject project)
    {
        string title = project?.metadata?.title;
        string artist = project?.metadata?.artist;
        return SanitizeFileName($"{FirstNonEmpty(artist, "Unknown")}_{FirstNonEmpty(title, "Chart")}");
    }

    private static string NormalizeDifficultyLabel(string difficultyLabel, int difficultyUiIndex)
    {
        if (!string.IsNullOrWhiteSpace(difficultyLabel))
            return difficultyLabel.Trim();
        if (difficultyUiIndex == 0)
            return "Full";
        if (difficultyUiIndex > 0)
            return difficultyUiIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return "Full";
    }

    private static int NormalizeDifficultyUiIndex(int difficultyUiIndex, string difficultyLabel)
    {
        if (difficultyUiIndex >= 0)
            return difficultyUiIndex;
        if (string.Equals(difficultyLabel?.Trim(), "Full", StringComparison.OrdinalIgnoreCase))
            return 0;
        return int.TryParse(difficultyLabel, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsed)
            ? Mathf.Max(0, parsed)
            : 0;
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
