using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ChartEditorRoundTripTests
{
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    [Test]
    public void SaveLoadSaveWithoutEdits_WritesIdenticalProjectJson()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.SaveProject(project, out string firstPath, out string firstError), firstError);
        string firstJson = File.ReadAllText(firstPath);

        Assert.IsTrue(ChartEditorProjectStore.LoadProject(firstPath, out ChartEditorProject loaded, out string loadError), loadError);
        Assert.IsTrue(ChartEditorProjectStore.SaveProject(loaded, out string secondPath, out string secondError), secondError);
        string secondJson = File.ReadAllText(secondPath);

        Assert.AreEqual(firstPath, secondPath);
        Assert.AreEqual(firstJson, secondJson, "Loading and saving a chart editor project without edits must not rewrite chart data.");
    }

    [Test]
    public void ExportWithoutEdits_TheoryPackageIsStableAfterReload()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.SaveProject(project, out string projectPath, out string saveError), saveError);
        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out string firstExportDirectory, out string firstPackagePath, out string firstExportError), firstExportError);
        string firstSnapshot = ReadTheoryPackageSnapshot(firstPackagePath);

        Assert.IsTrue(ChartEditorProjectStore.LoadProject(projectPath, out ChartEditorProject loaded, out string loadError), loadError);
        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(loaded, out string secondExportDirectory, out string secondPackagePath, out string secondExportError), secondExportError);
        string secondSnapshot = ReadTheoryPackageSnapshot(secondPackagePath);

        Assert.AreEqual(firstExportDirectory, secondExportDirectory);
        Assert.AreEqual(firstPackagePath, secondPackagePath);
        Assert.AreEqual(firstSnapshot, secondSnapshot, "Exporting a loaded chart without edits must produce the same .theory package chart data.");
    }

    [Test]
    public void SaveTheoryPackage_DefaultsToSharedChartEditorFolderUnderSongs()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.SaveTheoryPackage(project, saveAs: false, out string packagePath, out string saveError), saveError);

        string packageDirectory = Path.GetDirectoryName(packagePath);
        string expectedDirectory = Path.Combine(ExternalContentPaths.PersistentSongsDirectory, ChartEditorProjectStore.ChartEditorSaveFolderName);
        Assert.IsTrue(File.Exists(packagePath), "Saving a .theory package should create the package file.");
        Assert.AreEqual(Path.GetFullPath(expectedDirectory), Path.GetFullPath(packageDirectory ?? string.Empty));
        Assert.AreEqual("Unit_Test_Band_Round_Trip_Song.theory", Path.GetFileName(packagePath));
        Assert.AreEqual(packagePath, project.sourcePath);
    }

    [Test]
    public void SaveTheoryPackage_SaveAsUsesNumberedConflictSuffix()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.SaveTheoryPackage(project, saveAs: false, out string firstPackagePath, out string firstError), firstError);
        Assert.IsTrue(ChartEditorProjectStore.SaveTheoryPackage(project, saveAs: true, out string secondPackagePath, out string secondError), secondError);

        string expectedDirectory = Path.Combine(ExternalContentPaths.PersistentSongsDirectory, ChartEditorProjectStore.ChartEditorSaveFolderName);
        Assert.AreEqual(Path.GetFullPath(expectedDirectory), Path.GetFullPath(Path.GetDirectoryName(firstPackagePath) ?? string.Empty));
        Assert.AreEqual(Path.GetFullPath(expectedDirectory), Path.GetFullPath(Path.GetDirectoryName(secondPackagePath) ?? string.Empty));
        Assert.AreEqual("Unit_Test_Band_Round_Trip_Song.theory", Path.GetFileName(firstPackagePath));
        Assert.AreEqual("Unit_Test_Band_Round_Trip_Song_2.theory", Path.GetFileName(secondPackagePath));
        Assert.AreEqual(secondPackagePath, project.sourcePath);
    }

    [Test]
    public void SaveTheoryPackage_RelocatesLegacyPerSongTheoryFolderToSharedChartEditorFolder()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        string legacyDirectory = Path.Combine(ExternalContentPaths.PersistentSongsDirectory, "Old Song", "theory");
        Directory.CreateDirectory(legacyDirectory);

        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);
        Assert.IsTrue(TheoryChartEditorExporter.ExportProject(project, legacyDirectory, out string legacyPackagePath, out string exportError), exportError);

        project.sourceKind = ChartEditorSourceKind.TheoryPackage;
        project.sourcePath = legacyPackagePath;
        project.sourceFolder = legacyDirectory;
        Assert.IsTrue(ChartEditorProjectStore.SaveTheoryPackage(project, saveAs: false, out string savedPackagePath, out string saveError), saveError);

        string expectedDirectory = Path.Combine(ExternalContentPaths.PersistentSongsDirectory, ChartEditorProjectStore.ChartEditorSaveFolderName);
        Assert.AreEqual(Path.GetFullPath(expectedDirectory), Path.GetFullPath(Path.GetDirectoryName(savedPackagePath) ?? string.Empty));
        Assert.AreNotEqual(Path.GetFullPath(legacyPackagePath), Path.GetFullPath(savedPackagePath));
    }

    [Test]
    public void LibraryScanner_FindsMultipleTheoryPackagesInSharedChartEditorFolder()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject first = CreateRoundTripProject(scope.RootPath);
        PrepareProjectForNoEditRoundTrip(first);
        Assert.IsTrue(ChartEditorProjectStore.SaveTheoryPackage(first, saveAs: false, out string firstPackagePath, out string firstError), firstError);

        ChartEditorProject second = CreateRoundTripProject(scope.RootPath);
        second.projectId = "chart_editor_second_project";
        second.metadata.title = "Second Saved Song";
        PrepareProjectForNoEditRoundTrip(second);
        Assert.IsTrue(ChartEditorProjectStore.SaveTheoryPackage(second, saveAs: false, out string secondPackagePath, out string secondError), secondError);

        string expectedDirectory = Path.Combine(ExternalContentPaths.PersistentSongsDirectory, ChartEditorProjectStore.ChartEditorSaveFolderName);
        Assert.AreEqual(Path.GetFullPath(expectedDirectory), Path.GetFullPath(Path.GetDirectoryName(firstPackagePath) ?? string.Empty));
        Assert.AreEqual(Path.GetFullPath(expectedDirectory), Path.GetFullPath(Path.GetDirectoryName(secondPackagePath) ?? string.Empty));

        SongLibraryService.ClearCache();
        List<SongLibraryEntry> librarySongs = SongLibraryService.GetAvailableSongs(forceRefresh: true, refreshImports: false);
        HashSet<string> discoveredPackages = new HashSet<string>(
            librarySongs
                .Where(song => song?.PrimaryNotationKind == SongNotationSourceKind.TheoryPackage)
                .Select(song => song.PrimaryNotationPath),
            StringComparer.OrdinalIgnoreCase);

        Assert.IsTrue(discoveredPackages.Contains(firstPackagePath), "The shared chart editor save folder should expose the first .theory package.");
        Assert.IsTrue(discoveredPackages.Contains(secondPackagePath), "The shared chart editor save folder should expose the second .theory package.");
    }

    [Test]
    public void ExportWithoutEdits_TheoryPackageLoadsThroughRuntimeAndLibrary()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        AttachFixtureCover(project, scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out string exportDirectory, out string packagePath, out string exportError), exportError);
        Assert.IsTrue(File.Exists(packagePath), "Export did not create a .theory package.");
        Assert.IsTrue(packagePath.EndsWith(TheoryPackageFormat.Extension, StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("Unit_Test_Band_Round_Trip_Song.theory", Path.GetFileName(packagePath));

        Assert.IsTrue(SongNotationFacade.TryDetectKind(packagePath, out SongNotationSourceKind kind));
        Assert.AreEqual(SongNotationSourceKind.TheoryPackage, kind);
        Assert.IsTrue(TheorySongLoader.TryLoadManifest(packagePath, out TheorySongManifest manifest));
        Assert.AreEqual(project.metadata.title, manifest.title);
        Assert.AreEqual(project.metadata.artist, manifest.artist);
        Assert.AreEqual(project.tracks.Count, manifest.arrangements.Count);

        List<MusicXmlLoader.MusicXmlPartSummary> summaries = SongNotationFacade.GetPartSummaries(packagePath, kind);
        Assert.AreEqual(project.tracks.Count, summaries.Count);
        List<NoteData> runtimeNotes = SongNotationFacade.LoadSong(packagePath, kind, 0);
        Assert.AreEqual(project.tracks[0].notes.Count, runtimeNotes.Count);
        List<ArpeggioGuideData> arpeggioGuides = SongNotationFacade.LoadArpeggioGuides(packagePath, kind, 0);
        Assert.AreEqual(project.tracks[0].arpeggioGuides.Count, arpeggioGuides.Count);
        Assert.IsTrue(TheoryPackageCache.TryCachePrimaryAudio(packagePath, manifest, out string cachedAudioPath, out string audioCacheError), audioCacheError);
        Assert.IsTrue(File.Exists(cachedAudioPath), "Library playback needs a cached real audio file path.");
        Assert.IsTrue(TheoryPackageCache.TryCacheCoverArt(packagePath, manifest, out string cachedCoverPath, out string coverCacheError), coverCacheError);
        Assert.IsTrue(File.Exists(cachedCoverPath), "Library display needs a cached real cover-art file path.");

        SongLibraryService.ClearCache();
        List<SongLibraryEntry> librarySongs = SongLibraryService.GetAvailableSongs(forceRefresh: true, refreshImports: false);
        SongLibraryEntry entry = librarySongs.FirstOrDefault(song =>
            song != null &&
            string.Equals(song.PrimaryNotationPath, packagePath, StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(entry, "The library scanner did not discover the exported .theory package.");
        Assert.AreEqual(SongNotationSourceKind.TheoryPackage, entry.PrimaryNotationKind);
        Assert.AreEqual(project.metadata.title, entry.DisplayName);
        Assert.IsTrue(File.Exists(entry.Mp3Path), "The .theory library entry should expose cached audio for Unity playback.");
        Assert.IsTrue(File.Exists(entry.ArtworkPath), "The .theory library entry should expose cached cover art for the library UI.");
        Assert.AreEqual(exportDirectory, Path.GetDirectoryName(packagePath));
    }

    [Test]
    public void SongNotationFacade_DetectsGp8AsGuitarPro()
    {
        string path = Path.Combine(Path.GetTempPath(), $"string_theory_gp8_detection_{Guid.NewGuid():N}.gp8");
        try
        {
            File.WriteAllBytes(path, Array.Empty<byte>());

            Assert.IsTrue(SongNotationFacade.TryDetectKind(path, out SongNotationSourceKind kind));
            Assert.AreEqual(SongNotationSourceKind.Gp5, kind);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Test]
    public void LibraryScanner_FindsTheoryPackagesInsideNestedGroupingFolders()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        string nestedSongDirectory = Path.Combine(ExternalContentPaths.PersistentSongsDirectory, "My Favorites", "Nested Song");
        Directory.CreateDirectory(nestedSongDirectory);

        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        project.sourceFolder = nestedSongDirectory;
        project.sourcePath = Path.Combine(nestedSongDirectory, "source.gp");
        AttachFixtureAudio(project, scope.RootPath);
        AttachFixtureCover(project, scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(TheoryChartEditorExporter.ExportProject(project, nestedSongDirectory, out string packagePath, out string exportError), exportError);
        Assert.AreEqual(nestedSongDirectory, Path.GetDirectoryName(packagePath));

        SongLibraryService.ClearCache();
        List<SongLibraryEntry> librarySongs = SongLibraryService.GetAvailableSongs(forceRefresh: true, refreshImports: false);
        SongLibraryEntry entry = librarySongs.FirstOrDefault(song =>
            song != null &&
            string.Equals(song.PrimaryNotationPath, packagePath, StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(entry, "The library scanner should discover .theory packages below arbitrary grouping folders.");
        Assert.AreEqual(Path.GetFullPath(nestedSongDirectory), Path.GetFullPath(entry.SongDirectory));
        Assert.AreEqual(SongNotationSourceKind.TheoryPackage, entry.PrimaryNotationKind);
    }

    [Test]
    public void ExportWithoutEdits_TheoryExplicitInstrumentTagsDriveRuntimeAndEditorImport()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);

        project.tracks[0].displayName = "Part A";
        project.tracks[0].importedName = "Part A";
        project.tracks[1].displayName = "Part B";
        project.tracks[1].importedName = "Part B";
        project.tracks.Add(CreatePianoTrack());
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string packagePath, out string exportError), exportError);

        List<MusicXmlLoader.MusicXmlPartSummary> summaries = SongNotationFacade.GetPartSummaries(packagePath, SongNotationSourceKind.TheoryPackage);
        Assert.AreEqual(3, summaries.Count);
        AssertTheorySummaryTag(summaries, "lead", "Lead", "guitar");
        AssertTheorySummaryTag(summaries, "bass", "Bass", "bass");
        AssertTheorySummaryTag(summaries, "piano", "Piano", "piano");

        CollectionAssert.AreEqual(
            BuildRuntimeNoteDigest(SongNotationFacade.LoadSong(packagePath, SongNotationSourceKind.TheoryPackage, summaries.First(summary => summary.PartId == "lead").Index)),
            BuildRuntimeNoteDigest(SongNotationFacade.LoadSong(packagePath, SongNotationSourceKind.TheoryPackage)),
            "Default .theory runtime load should use the manifest default arrangement, not a display-name guess.");

        foreach (ChartEditorTrack sourceTrack in project.tracks)
        {
            Assert.IsTrue(
                TheorySongLoader.TryLoadArrangementByPartId(packagePath, sourceTrack.id, out _, out TheoryArrangementData arrangement),
                $".theory package did not contain arrangement '{sourceTrack.id}'.");
            Assert.AreEqual(sourceTrack.notes.Count, arrangement.notes.Count, $".theory arrangement '{sourceTrack.id}' lost note data.");
        }

        GeneratedPlaybackArrangement generated = SongNotationFacade.LoadGeneratedArrangement(packagePath, SongNotationSourceKind.TheoryPackage);
        GeneratedPlaybackPartInfo pianoPart = generated.parts.FirstOrDefault(part => part.partId == "piano");
        Assert.IsNotNull(pianoPart, "Generated playback metadata should include future non-guitar tagged parts.");
        Assert.AreEqual("Piano", pianoPart.instrumentName);
        Assert.AreEqual(0, pianoPart.sourceMidiProgram);
        Assert.IsFalse(pianoPart.isGuitarFamily);

        SongLibraryService.ClearCache();
        List<SongLibraryEntry> librarySongs = SongLibraryService.GetAvailableSongs(forceRefresh: true, refreshImports: false);
        SongLibraryEntry entry = librarySongs.FirstOrDefault(song =>
            song != null &&
            string.Equals(song.PrimaryNotationPath, packagePath, StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(entry, "The library scanner did not discover the tagged .theory package.");
        Assert.AreEqual(SongNotationSourceKind.TheoryPackage, entry.PrimaryNotationKind);
        CollectionAssert.AreEqual(
            BuildPartSummaryDigest(summaries),
            BuildPartSummaryDigest(SongNotationFacade.GetPartSummaries(entry.PrimaryNotationPath, entry.PrimaryNotationKind)),
            "Library .theory summaries should expose the same explicit arrangement tags used by the game.");

        Assert.IsTrue(ChartEditorImportService.ImportTheoryPackage(packagePath, out ChartEditorImportResult importResult, out string importError), importError);
        ChartEditorProject imported = importResult?.project;
        Assert.IsNotNull(imported, "Importing the .theory package did not return an editor project.");
        CollectionAssert.AreEquivalent(
            new[] { ChartEditorTrackRole.LeadGuitar, ChartEditorTrackRole.Bass, ChartEditorTrackRole.Piano },
            imported.tracks.Select(track => track.role).ToArray(),
            "Editor import should restore roles from .theory route/instrument tags even when names are generic.");
    }

    [Test]
    public void ExportWithoutEdits_TheoryPackageResolvesAlphaTabSource()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string packagePath, out string exportError), exportError);

        AlphaTabResolvedSourceData resolved = AlphaTabSourceResolver.Resolve(packagePath, 0);
        Assert.AreEqual(Path.GetFullPath(packagePath), resolved.logicalNotationPath);
        Assert.AreEqual(0, resolved.logicalTrackIndex);
        Assert.AreEqual(0, resolved.resolvedTrackIndex);
        Assert.IsTrue(File.Exists(resolved.resolvedNotationPath), "The .theory package should resolve to an AlphaTab-compatible source file.");
        StringAssert.EndsWith(".alphatex", resolved.resolvedNotationPath);
        Assert.IsTrue(File.Exists($"{resolved.resolvedNotationPath}.timing.json"), "The resolved AlphaTab source should include the timing sidecar used by cursor/feedback rendering.");
    }

    [Test]
    public void ExportWithoutEdits_TheoryGeneratedDrumRoutesPreserveDrumChannels()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        project.tracks.Add(CreateDrumTrack());
        AttachFixtureAudio(project, scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string packagePath, out string exportError), exportError);
        Assert.IsTrue(File.Exists(packagePath), "Export did not create the .theory package.");

        GeneratedPlaybackArrangement theory = SongNotationFacade.LoadGeneratedArrangement(packagePath, SongNotationSourceKind.TheoryPackage);
        Assert.IsTrue(theory.channelAssignments.Any(channel => channel.isDrum), ".theory generated playback should preserve drum channel routes.");
        Assert.IsTrue(theory.parts.Any(part => part != null && part.isDrum && string.Equals(part.partId, "drums", StringComparison.OrdinalIgnoreCase)),
            ".theory generated playback metadata should include the drum part.");
    }

    [Test]
    public void ExportImportDrumTheoryPackage_PreservesLaneOrderAndNormalNoteFlags()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        project.tracks.Add(CreateDrumTrack());
        AttachFixtureAudio(project, scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string packagePath, out string exportError), exportError);
        Assert.IsTrue(TheorySongLoader.TryLoadArrangementByPartId(packagePath, "drums", out TheoryArrangementSummary drumSummary, out TheoryArrangementData drumArrangement), "The .theory package did not contain the drum arrangement.");
        Assert.AreEqual("drums", drumSummary.instrumentType);
        Assert.AreEqual("Drums", drumSummary.route);

        CollectionAssert.AreEqual(
            new[]
            {
                "0:42:Hi-Hat:False:True",
                "1:49:Crash Cymbal:False:True",
                "2:38:Snare:False:True",
                "4:36:Kick:False:True"
            },
            drumArrangement.notes
                .OrderBy(note => note.time)
                .Select(note => $"{note.stringIndex}:{note.fret}:{note.noteName}:{note.tap}:{note.requiresPluck}")
                .ToArray());

        MusicXmlLoader.MusicXmlPartSummary runtimeSummary = SongNotationFacade
            .GetPartSummaries(packagePath, SongNotationSourceKind.TheoryPackage)
            .First(summary => string.Equals(summary.PartId, "drums", StringComparison.OrdinalIgnoreCase));
        List<NoteData> runtimeNotes = SongNotationFacade.LoadSong(packagePath, SongNotationSourceKind.TheoryPackage, runtimeSummary.Index);
        CollectionAssert.AreEqual(
            new[]
            {
                "0:42:Hi-Hat:True",
                "1:49:Crash Cymbal:True",
                "2:38:Snare:True",
                "4:36:Kick:True"
            },
            runtimeNotes
                .OrderBy(note => note.time)
                .Select(note => $"{note.stringIdx}:{note.fret}:{note.note}:{note.requiresPluck}")
                .ToArray());

        Assert.IsTrue(ChartEditorImportService.ImportTheoryPackage(packagePath, out ChartEditorImportResult importResult, out string importError), importError);
        ChartEditorTrack importedDrums = importResult.project.tracks.First(track => string.Equals(track.id, "drums", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(ChartEditorTrackRole.Drums, importedDrums.role);
        CollectionAssert.AreEqual(
            new[]
            {
                "0:42:Hi-Hat:False:True",
                "1:49:Crash Cymbal:False:True",
                "2:38:Snare:False:True",
                "4:36:Kick:False:True"
            },
            importedDrums.notes
                .OrderBy(note => note.timeSeconds)
                .Select(note => $"{note.stringOrLane}:{note.fret}:{note.noteName}:{note.tap}:{note.requiresPluck}")
                .ToArray());
    }

    [Test]
    public void DrumLaneMapper_UsesGameplayLaneOrder()
    {
        CollectionAssert.AreEqual(
            new[] { "Hat", "Crash", "Snare", "T1", "Kick", "T2", "Floor", "Ride" },
            Enumerable.Range(0, DrumLaneMapper.LaneCount)
                .Select(DrumLaneMapper.GetLaneLabel)
                .ToArray());

        Assert.AreEqual(DrumLaneMapper.HiHatLane, DrumLaneMapper.MapGeneralMidiToLane(42));
        Assert.AreEqual(DrumLaneMapper.CrashLane, DrumLaneMapper.MapGeneralMidiToLane(49));
        Assert.AreEqual(DrumLaneMapper.SnareLane, DrumLaneMapper.MapGeneralMidiToLane(38));
        Assert.AreEqual(DrumLaneMapper.KickLane, DrumLaneMapper.MapGeneralMidiToLane(36));
    }

    [Test]
    public void HighwayPreview_DrumTrack_IsSupportedAndUsesDrumLanePreviewColumns()
    {
        ChartEditorTrack drums = CreateDrumTrack();
        ChartEditorProject project = new ChartEditorProject
        {
            tracks = new List<ChartEditorTrack> { drums },
            selectedTrackId = drums.id,
            cursorTimeSeconds = 0.5
        };
        project.EnsureDefaults();

        Assert.IsTrue(ChartEditorHighwayPreviewSnapshotBuilder.IsSupportedPreviewTrack(drums));
        ChartEditorHighwayPreviewFrame frame = ChartEditorHighwayPreviewSnapshotBuilder.Build(project);

        Assert.AreEqual(DrumLaneMapper.LaneCount, frame.laneCount);
        CollectionAssert.AreEqual(
            new[]
            {
                "0:1:Hi-Hat:True",
                "1:2:Crash Cymbal:True",
                "2:3:Snare:True",
                "4:5:Kick:True"
            },
            frame.notes
                .OrderBy(note => note.time)
                .Select(note => $"{note.stringIdx}:{note.fret}:{note.note}:{note.requiresPluck}")
                .ToArray());
    }

    [Test]
    public void DrumNoteSanitizer_ClearsGuitarTechniqueState()
    {
        ChartEditorNote note = new ChartEditorNote
        {
            technique = NoteTechnique.Bend,
            slideTargetFret = 9,
            bendStep = 2f,
            bendVisualStartTime = 0.1f,
            bendVisualDuration = 0.4f,
            bendPreBend = true,
            bendRelease = true,
            muted = true,
            palmMute = true,
            fretHandMute = true,
            harmonic = true,
            accent = true,
            tap = true,
            tremolo = true,
            pinchHarmonic = true,
            vibratoStrength = 2,
            maxBend = 2f,
            legato = true,
            requiresPluck = false,
            linkedFromNoteId = 42,
            bendPoints = new List<ChartEditorBendPoint> { new ChartEditorBendPoint { timeSeconds = 0.1f, step = 2f } },
            techniqueSegments = new List<ChartEditorTechniqueSegment>
            {
                new ChartEditorTechniqueSegment { type = NoteTechniqueSegmentType.Vibrato, startOffset = 0f, endOffset = 0.3f }
            }
        };

        Assert.IsTrue(ChartEditorDrumNoteSanitizer.Sanitize(note));
        Assert.AreEqual(NoteTechnique.None, note.technique);
        Assert.AreEqual(-1, note.slideTargetFret);
        Assert.AreEqual(0f, note.bendStep);
        Assert.AreEqual(-1f, note.bendVisualStartTime);
        Assert.AreEqual(0f, note.bendVisualDuration);
        Assert.IsFalse(note.bendPreBend);
        Assert.IsFalse(note.bendRelease);
        Assert.IsFalse(note.muted);
        Assert.IsFalse(note.palmMute);
        Assert.IsFalse(note.fretHandMute);
        Assert.IsFalse(note.harmonic);
        Assert.IsFalse(note.accent);
        Assert.IsFalse(note.tap);
        Assert.IsFalse(note.tremolo);
        Assert.IsFalse(note.pinchHarmonic);
        Assert.AreEqual(0, note.vibratoStrength);
        Assert.AreEqual(0f, note.maxBend);
        Assert.IsFalse(note.legato);
        Assert.IsTrue(note.requiresPluck);
        Assert.AreEqual(-1, note.linkedFromNoteId);
        Assert.AreEqual(0, note.bendPoints.Count);
        Assert.AreEqual(0, note.techniqueSegments.Count);
    }

    [Test]
    public void ImportTheoryDrumNote_MapsMidiFretToEditorLaneWhenSourceLaneIsCollapsed()
    {
        MethodInfo method = typeof(ChartEditorImportService).GetMethod("FromTheoryNoteData", StaticPrivate);
        Assert.IsNotNull(method, "Could not find chart editor theory-note import method.");

        TheoryNoteData source = new TheoryNoteData
        {
            id = 77,
            time = 1.0f,
            duration = 0.1f,
            stringIndex = 0,
            fret = 49,
            noteName = "Crash Cymbal",
            requiresPluck = false,
            legato = true,
            linkedFromNoteId = 12
        };

        // Reflection does not apply C# default parameter values, so the
        // optional preserveRuntimeFields argument must be passed explicitly.
        ChartEditorNote note = (ChartEditorNote)method.Invoke(null, new object[] { source, 0, true, false });

        Assert.AreEqual(DrumLaneMapper.CrashLane, note.stringOrLane);
        Assert.AreEqual(49, note.fret);
        Assert.IsTrue(note.requiresPluck);
        Assert.IsFalse(note.legato);
        Assert.AreEqual(-1, note.linkedFromNoteId);
    }

    [Test]
    public void ExportWithoutEdits_PreservesGeneratedPlaybackEventsInTheoryExport()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        AttachGeneratedPlaybackEvent(project.tracks[0]);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string packagePath, out string exportError), exportError);
        Assert.IsTrue(File.Exists(packagePath), "Export did not create the .theory package.");

        GeneratedPlaybackArrangement theory = SongNotationFacade.LoadGeneratedArrangement(packagePath, SongNotationSourceKind.TheoryPackage);
        Assert.AreEqual(1, theory.notes.Count, ".theory export should preserve the editor generated playback event.");
        Assert.AreEqual(68, theory.notes[0].midiNote);
        Assert.AreEqual(0, theory.notes[0].channel);
        Assert.AreEqual("lead", theory.notes[0].partId);
    }

    [Test]
    public void ExportWithoutEdits_PreservesToneChangesInTheoryExport()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        AttachToneData(project.tracks[0]);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string packagePath, out string exportError), exportError);

        Assert.IsTrue(TheorySongLoader.TryLoadArrangementByPartId(packagePath, "lead", out TheoryArrangementSummary theoryLeadSummary, out TheoryArrangementData theoryLead), "The .theory package did not contain the lead arrangement.");

        CollectionAssert.AreEqual(
            BuildEditorToneDigest(project.tracks[0].tones),
            BuildTheoryToneDigest(theoryLead.tones),
            ".theory export should preserve chart editor tone changes and tone definitions.");

        string theoryArrangementJson = ReadPackageEntry(packagePath, theoryLeadSummary.entry);
        string theoryManifestJson = ReadPackageEntry(packagePath, TheoryPackageFormat.ManifestEntryName);
        string theoryEditorStateJson = ReadPackageEntry(packagePath, TheoryPackageFormat.EditorStateEntryName);
        AssertTheoryPackageJsonIsNeutral(theoryManifestJson);
        AssertTheoryPackageJsonIsNeutral(theoryArrangementJson);
        AssertTheoryPackageJsonIsNeutral(theoryEditorStateJson);
    }

    [Test]
    public void ExportToneChanges_CanonicalizesStaleToneNameWhenToneIdResolvesDefinition()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        AttachToneData(project.tracks[0]);
        project.tracks[0].tones.changes[0].toneName = "Old Clean Label";
        project.tracks[0].tones.changes[0].toneId = 0;
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string packagePath, out string exportError), exportError);
        Assert.IsTrue(TheorySongLoader.TryLoadArrangementByPartId(packagePath, "lead", out _, out TheoryArrangementData theoryLead), "The .theory package did not contain the lead arrangement.");

        TheoryToneChangeData exportedChange = theoryLead.tones.changes.FirstOrDefault(change => Math.Abs(change.timeSeconds - 0.5f) < 0.001f);
        Assert.IsNotNull(exportedChange, "Expected the first tone change to export.");
        Assert.AreEqual("Clean Intro", exportedChange.toneName, "Exported tone changes must use a definition name the game can resolve.");
        Assert.AreEqual(0, exportedChange.toneId, "Export should not discard the source tone id.");
    }

    [Test]
    public void ExportDifficultyVariants_SharesToneDataWithinArrangementGroup()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        ChartEditorTrack fullLead = project.tracks[0];
        fullLead.arrangementGroupId = "lead";
        fullLead.difficultyLabel = "Full";
        fullLead.difficultyUiIndex = 0;
        AttachToneData(fullLead);

        ChartEditorTrack easyLead = JsonUtility.FromJson<ChartEditorTrack>(JsonUtility.ToJson(fullLead));
        easyLead.id = "lead_easy";
        easyLead.displayName = "Lead Guitar Easy";
        easyLead.arrangementGroupId = "lead";
        easyLead.difficultyLabel = "1";
        easyLead.difficultyUiIndex = 1;
        AttachAlternateToneData(easyLead);
        project.tracks.Insert(1, easyLead);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string packagePath, out string exportError), exportError);
        Assert.IsTrue(TheorySongLoader.TryLoadArrangementByPartId(packagePath, "lead", out _, out TheoryArrangementData exportedFull), "The .theory package did not contain the full lead arrangement.");
        Assert.IsTrue(TheorySongLoader.TryLoadArrangementByPartId(packagePath, "lead_easy", out _, out TheoryArrangementData exportedEasy), "The .theory package did not contain the easy lead arrangement.");

        CollectionAssert.AreEqual(
            BuildTheoryToneDigest(exportedFull.tones),
            BuildTheoryToneDigest(exportedEasy.tones),
            "Difficulty variants in the same arrangement group should share one tone map.");
        Assert.AreEqual("Clean Intro", exportedEasy.tones.baseToneName);
    }

    [Test]
    public void LoadArrangementByGroupId_PrefersFullDifficultyWhenManifestOrderDiffers()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        ChartEditorTrack fullLead = project.tracks[0];
        fullLead.id = "lead_full";
        fullLead.arrangementGroupId = "lead";
        fullLead.difficultyLabel = "Full";
        fullLead.difficultyUiIndex = 0;

        ChartEditorTrack easyLead = JsonUtility.FromJson<ChartEditorTrack>(JsonUtility.ToJson(fullLead));
        easyLead.id = "lead_easy";
        easyLead.displayName = "Lead Guitar Easy";
        easyLead.arrangementGroupId = "lead";
        easyLead.difficultyLabel = "1";
        easyLead.difficultyUiIndex = 1;
        project.tracks.Clear();
        project.tracks.Add(easyLead);
        project.tracks.Add(fullLead);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string packagePath, out string exportError), exportError);
        Assert.IsTrue(TheorySongLoader.TryLoadArrangementByGroupId(packagePath, "lead", out TheoryArrangementSummary summary, out _), "The .theory package did not contain the lead group.");

        Assert.AreEqual("lead_full", summary.arrangementId);
    }

    private static void AssertTheoryPackageJsonIsNeutral(string json)
    {
        StringAssert.DoesNotContain("rawJson", json);
        StringAssert.DoesNotContain("GearList", json);
        StringAssert.DoesNotContain("Rocksmith", json);
        StringAssert.DoesNotContain("rocksmith", json);
        StringAssert.DoesNotContain("Slop", json);
        StringAssert.DoesNotContain("slop", json);
        StringAssert.DoesNotContain("PSARC", json);
        StringAssert.DoesNotContain("psarc", json);
    }

    [Test]
    public void ImportExportedTheoryPackage_PreservesToneChanges()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        AttachToneData(project.tracks[0]);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string packagePath, out string exportError), exportError);
        Assert.IsTrue(ChartEditorImportService.ImportTheoryPackage(packagePath, out ChartEditorImportResult importResult, out string importError), importError);
        ChartEditorProject imported = importResult?.project;
        Assert.IsNotNull(imported, "Importing the .theory package did not return a chart editor project.");

        ChartEditorTrack importedLead = imported.tracks.First(track => track.id == "lead");
        CollectionAssert.AreEqual(
            BuildEditorToneDigest(project.tracks[0].tones),
            BuildEditorToneDigest(importedLead.tones),
            "Opening a .theory package in the editor should restore tone changes and tone definitions.");

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(imported, out _, out string reexportedPackagePath, out string reexportError), reexportError);
        Assert.IsTrue(TheorySongLoader.TryLoadArrangementByPartId(reexportedPackagePath, "lead", out _, out TheoryArrangementData reexportedLead), "Re-exported .theory package did not contain the lead arrangement.");
        CollectionAssert.AreEqual(
            BuildEditorToneDigest(project.tracks[0].tones),
            BuildTheoryToneDigest(reexportedLead.tones),
            "Re-exporting an imported .theory package should preserve tone changes and tone definitions.");
    }

    [Test]
    public void ImportTheoryPackage_AppliesEmbeddedToneLabMappingsToEditorTones()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        AttachToneData(project.tracks[0]);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string packagePath, out string exportError), exportError);
        TheoryToneLabMappingState mappingState = new TheoryToneLabMappingState
        {
            mappings = new List<TheoryToneLabPresetMappingData>
            {
                new TheoryToneLabPresetMappingData
                {
                    arrangementId = "lead",
                    toneName = "Clean Intro",
                    presetId = "mapped_clean",
                    presetSnapshot = CreateTheoryTonePreset("mapped_clean", "Mapped Clean", "Amp", "mapped-amp")
                }
            }
        };
        Assert.IsTrue(TheoryPackageIO.TryWriteToneLabMappings(packagePath, mappingState, out string mappingWriteError), mappingWriteError);

        Assert.IsTrue(ChartEditorImportService.ImportTheoryPackage(packagePath, out ChartEditorImportResult importResult, out string importError), importError);
        ChartEditorTrack importedLead = importResult.project.tracks.First(track => track.id == "lead");
        ChartEditorToneDefinition importedClean = importedLead.tones.definitions.FirstOrDefault(definition => definition.name == "Clean Intro");

        Assert.IsNotNull(importedClean, "Expected the mapped tone definition to exist after import.");
        Assert.AreEqual("mapped_clean", importedClean.preset.presetId);
        Assert.AreEqual("Mapped Clean", importedClean.preset.presetName);
        Assert.AreEqual("mapped-amp", importedClean.preset.pedalChain[0].descriptorId);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(importResult.project, out _, out string reexportedPackagePath, out string reexportError), reexportError);
        Assert.IsTrue(TheoryPackageIO.TryReadToneLabMappings(reexportedPackagePath, out TheoryToneLabMappingState reexportedMappings, out string mappingReadError), mappingReadError);
        TheoryToneLabPresetMappingData reexportedMapping = reexportedMappings.mappings.FirstOrDefault(mapping => mapping.arrangementId == "lead" && mapping.toneName == "Clean Intro");
        Assert.IsNotNull(reexportedMapping, "Re-export should write the effective editor tone mapping back into the .theory package.");
        Assert.AreEqual("mapped_clean", reexportedMapping.presetId);
        Assert.AreEqual("Mapped Clean", reexportedMapping.presetSnapshot.presetName);
    }

    [Test]
    public void ExportWithEditedNotes_RegeneratesGeneratedPlaybackFromEditedChartNotes()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        AttachGeneratedPlaybackEvent(project.tracks[0]);
        PrepareProjectForNoEditRoundTrip(project);

        project.tracks[0].notes[0].timeSeconds += 0.125;
        project.dirty = true;

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string packagePath, out string exportError), exportError);

        GeneratedPlaybackArrangement theory = SongNotationFacade.LoadGeneratedArrangement(packagePath, SongNotationSourceKind.TheoryPackage);
        Assert.Greater(theory.notes.Count, 0, "Edited notes should produce regenerated .theory generated playback events.");
        Assert.IsTrue(theory.notes.Any(note => Math.Abs(note.startTimeSeconds - (float)project.tracks[0].notes[0].timeSeconds) <= 0.001f),
            ".theory generated playback should reflect the edited chart note timing.");
    }

    [Test]
    public void TheoryPackage_EmbeddedStemsHydrateRuntimeStemCache()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string packagePath, out string exportError), exportError);
        Assert.IsTrue(TheorySongLoader.TryLoadManifest(packagePath, out TheorySongManifest manifest));
        Assert.IsTrue(TheoryPackageCache.TryCachePrimaryAudio(packagePath, manifest, out string cachedAudioPath, out string audioError), audioError);

        StemCacheManifest generatedManifest = WriteGeneratedStemCache(cachedAudioPath);
        Assert.IsTrue(StemSeparationService.TryLoadValidManifest(cachedAudioPath, out StemCacheManifest loadedGeneratedManifest));
        Assert.AreEqual(2, loadedGeneratedManifest.stems.Count);

        Assert.IsTrue(TheoryPackageIO.TryEmbedStemCache(packagePath, generatedManifest, cachedAudioPath, out string embedError), embedError);
        Assert.IsTrue(TheorySongLoader.TryLoadManifest(packagePath, out TheorySongManifest updatedManifest));
        Assert.AreEqual(2, updatedManifest.stems.Count);
        CollectionAssert.AreEqual(new[] { "bass", "guitar" }, updatedManifest.stems.Select(stem => stem.id).OrderBy(id => id).ToArray());
        Assert.IsTrue(updatedManifest.stems.All(stem => stem.entry.StartsWith($"{TheoryPackageFormat.StemDirectory}/", StringComparison.Ordinal)));
        AssertPackageEntryExists(packagePath, TheoryPackageFormat.BuildStemEntryName("guitar", ".ogg"));
        AssertPackageEntryExists(packagePath, TheoryPackageFormat.BuildStemEntryName("bass", ".ogg"));

        string cacheDirectory = StemSeparationService.GetCacheDirectory(cachedAudioPath);
        Directory.Delete(cacheDirectory, true);
        Assert.IsFalse(StemSeparationService.TryLoadValidManifest(cachedAudioPath, out _), "The loose generated cache should be gone before testing package hydration.");

        Assert.IsTrue(TheoryPackageCache.TryCacheEmbeddedStems(packagePath, updatedManifest, cachedAudioPath, out StemCacheManifest embeddedManifest, out string cacheError), cacheError);
        Assert.AreEqual("theory-package", embeddedManifest.provider);
        Assert.AreEqual(2, embeddedManifest.stems.Count);
        foreach (StemCacheEntry stem in embeddedManifest.stems)
        {
            string stemPath = StemSeparationService.ResolveStemPath(cachedAudioPath, stem);
            Assert.IsTrue(File.Exists(stemPath), $"Embedded stem '{stem.id}' was not hydrated into the runtime stem cache.");
        }
    }

    [Test]
    public void ImportExportedTheoryPackage_PreservesEmbeddedStems()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string packagePath, out string exportError), exportError);
        Assert.IsTrue(TheorySongLoader.TryLoadManifest(packagePath, out TheorySongManifest manifest));
        Assert.IsTrue(TheoryPackageCache.TryCachePrimaryAudio(packagePath, manifest, out string cachedAudioPath, out string audioError), audioError);
        StemCacheManifest generatedManifest = WriteGeneratedStemCache(cachedAudioPath);
        Assert.IsTrue(TheoryPackageIO.TryEmbedStemCache(packagePath, generatedManifest, cachedAudioPath, out string embedError), embedError);

        Assert.IsTrue(ChartEditorImportService.ImportTheoryPackage(packagePath, out ChartEditorImportResult importResult, out string importError), importError);
        Assert.IsNotNull(importResult?.project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(importResult.project, out _, out string reexportedPackagePath, out string reexportError), reexportError);
        Assert.IsTrue(TheorySongLoader.TryLoadManifest(reexportedPackagePath, out TheorySongManifest reexportedManifest));
        Assert.AreEqual(2, reexportedManifest.stems.Count, "Re-exporting an imported .theory package must preserve embedded stems.");
        AssertPackageEntryExists(reexportedPackagePath, TheoryPackageFormat.BuildStemEntryName("guitar", ".ogg"));
        AssertPackageEntryExists(reexportedPackagePath, TheoryPackageFormat.BuildStemEntryName("bass", ".ogg"));

        Assert.IsTrue(TheoryPackageCache.TryCachePrimaryAudio(reexportedPackagePath, reexportedManifest, out string reexportedAudioPath, out string reexportedAudioError), reexportedAudioError);
        Assert.IsTrue(TheoryPackageCache.TryCacheEmbeddedStems(reexportedPackagePath, reexportedManifest, reexportedAudioPath, out StemCacheManifest embeddedManifest, out string cacheError), cacheError);
        CollectionAssert.AreEqual(new[] { "bass", "guitar" }, embeddedManifest.stems.Select(stem => stem.id).OrderBy(id => id).ToArray());
    }

    [Test]
    public void ReexportExistingTheoryPackage_PreservesEmbeddedStemsFromTargetPackage()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsFalse(TheoryPackageFormat.IsPackagePath(project.sourcePath), "Fixture project should represent a non-.theory source.");
        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string packagePath, out string exportError), exportError);
        Assert.IsTrue(TheorySongLoader.TryLoadManifest(packagePath, out TheorySongManifest manifest));
        Assert.IsTrue(TheoryPackageCache.TryCachePrimaryAudio(packagePath, manifest, out string cachedAudioPath, out string audioError), audioError);
        StemCacheManifest generatedManifest = WriteGeneratedStemCache(cachedAudioPath);
        Assert.IsTrue(TheoryPackageIO.TryEmbedStemCache(packagePath, generatedManifest, cachedAudioPath, out string embedError), embedError);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string reexportedPackagePath, out string reexportError), reexportError);
        Assert.AreEqual(packagePath, reexportedPackagePath);
        Assert.IsTrue(TheorySongLoader.TryLoadManifest(reexportedPackagePath, out TheorySongManifest reexportedManifest));
        Assert.AreEqual(2, reexportedManifest.stems.Count, "Re-exporting an existing target .theory package must preserve embedded stems even when the editor project source is not .theory.");
        AssertPackageEntryExists(reexportedPackagePath, TheoryPackageFormat.BuildStemEntryName("guitar", ".ogg"));
        AssertPackageEntryExists(reexportedPackagePath, TheoryPackageFormat.BuildStemEntryName("bass", ".ogg"));
    }

    [Test]
    public void ImportExportedTheoryPackage_PreservesEditorOnlyChartValues()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out _, out string packagePath, out string exportError), exportError);
        Assert.IsTrue(ChartEditorImportService.ImportTheoryPackage(packagePath, out ChartEditorImportResult result, out string importError), importError);
        ChartEditorProject imported = result?.project;
        Assert.IsNotNull(imported, "Importing the package did not return a project.");
        Assert.AreEqual(ChartEditorSourceKind.TheoryPackage, imported.sourceKind);
        Assert.AreEqual(project.metadata.title, imported.metadata.title);
        Assert.AreEqual(project.metadata.artist, imported.metadata.artist);
        Assert.AreEqual(project.tracks.Count, imported.tracks.Count);
        Assert.AreEqual(project.beatMap.beatMarkers.Count, imported.beatMap.beatMarkers.Count);
        Assert.AreEqual(project.settings.snapSeconds, imported.settings.snapSeconds);

        ChartEditorTrack sourceLead = project.tracks.First(track => track.id == "lead");
        ChartEditorTrack importedLead = imported.tracks.First(track => track.id == "lead");
        CollectionAssert.AreEqual(BuildEditorNoteDigest(sourceLead), BuildEditorNoteDigest(importedLead));

        ChartEditorNote sourceBend = sourceLead.notes.First(note => note.id == "lead_bend_release");
        ChartEditorNote importedBend = importedLead.notes.First(note => note.id == "note_102");
        Assert.AreEqual(sourceBend.techniqueSegments.Count, importedBend.techniqueSegments.Count);
        Assert.IsTrue(importedBend.bendRelease, "Editor-only bend-release flag should survive .theory export/import.");
        Assert.AreEqual(2f, importedBend.maxBend, 0.001f);
        Assert.IsTrue(File.Exists(imported.audio.sourcePath), "Opening a .theory package should cache its embedded audio for editing/playback.");
    }

    [Test]
    public void HeadlessTheoryConversion_FromImporterBackedCacheFolder_WritesValidatedPackageWithDifficultiesAndPlayback()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        string manifestPath = WriteCachedArrangementFixture(scope.RootPath);
        string sourceDirectory = Path.GetDirectoryName(manifestPath);
        string outputPath = Path.Combine(scope.RootPath, "converted.theory");

        Assert.IsTrue(ChartEditorTheoryConversionService.ConvertToTheoryPackage(new ChartEditorTheoryConversionRequest
        {
            sourcePath = sourceDirectory,
            outputPackagePath = outputPath,
            overwriteExisting = true,
            validatePackage = true,
            requireAudio = true
        }, out ChartEditorTheoryConversionResult result, out string conversionError), conversionError);

        Assert.IsTrue(File.Exists(result.packagePath), "Headless conversion did not write a .theory package.");
        Assert.IsTrue(result.packageWasWritten);
        Assert.AreEqual(ChartEditorSourceKind.ExternalImporter, result.sourceKind);
        Assert.AreEqual(SongNotationSourceKind.TheoryPackage, result.sourceNotationKind);
        Assert.AreEqual(2, result.project.tracks.Count, "Headless conversion should keep every difficulty variant.");

        Assert.IsTrue(TheorySongLoader.TryLoadManifest(result.packagePath, out TheorySongManifest manifest));
        Assert.AreEqual(2, manifest.arrangements.Count);
        Assert.IsFalse(string.IsNullOrWhiteSpace(manifest.primaryAudioEntry));
        Assert.IsTrue(TheoryPackageIO.EntryExists(result.packagePath, manifest.primaryAudioEntry), "Converted package should embed playback audio.");

        TheoryArrangementSummary easySummary = manifest.arrangements.FirstOrDefault(summary => summary.arrangementId == "lead_easy");
        TheoryArrangementSummary fullSummary = manifest.arrangements.FirstOrDefault(summary => summary.arrangementId == "lead_full");
        Assert.IsNotNull(easySummary, "Converted package lost the easy difficulty.");
        Assert.IsNotNull(fullSummary, "Converted package lost the full difficulty.");
        Assert.AreEqual("lead", easySummary.groupId);
        Assert.AreEqual("1", easySummary.difficultyLabel);
        Assert.AreEqual(1, easySummary.difficultyUiIndex);
        Assert.IsTrue(easySummary.hasDifficultyVariants);
        Assert.AreEqual("lead", fullSummary.groupId);
        Assert.AreEqual("Full", fullSummary.difficultyLabel);
        Assert.AreEqual(0, fullSummary.difficultyUiIndex);
        Assert.IsTrue(fullSummary.hasDifficultyVariants);

        Assert.IsTrue(TheoryPackageIO.TryReadArrangement(result.packagePath, easySummary, out TheoryArrangementData easyArrangement, out string easyError), easyError);
        Assert.IsTrue(TheoryPackageIO.TryReadArrangement(result.packagePath, fullSummary, out TheoryArrangementData fullArrangement, out string fullError), fullError);
        Assert.AreEqual(1, easyArrangement.notes.Count);
        Assert.AreEqual(7, fullArrangement.notes.Count);
        Assert.Greater(easyArrangement.generatedNotes.Count, 0, "Converted easy difficulty should include generated playback events.");
        Assert.Greater(fullArrangement.generatedNotes.Count, 0, "Converted full difficulty should include generated playback events.");

        GeneratedPlaybackArrangement generated = SongNotationFacade.LoadGeneratedArrangement(result.packagePath, SongNotationSourceKind.TheoryPackage);
        Assert.IsNotNull(generated);
        Assert.Greater(generated.notes.Count, 0, "Runtime generated playback loader should read the converted package.");
    }

    [Test]
    public void LibraryTheoryConversion_DefaultsToSongsRootNotChartEditorFolder()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        Directory.CreateDirectory(ExternalContentPaths.PersistentSongsDirectory);
        string manifestPath = WriteCachedArrangementFixture(ExternalContentPaths.PersistentSongsDirectory);
        string sourceDirectory = Path.GetDirectoryName(manifestPath);

        Assert.IsTrue(ChartEditorTheoryConversionService.ConvertLibrarySourceToTheoryPackage(new ChartEditorTheoryConversionRequest
        {
            sourcePath = sourceDirectory,
            validatePackage = true,
            requireAudio = true
        }, out ChartEditorTheoryConversionResult result, out string conversionError), conversionError);

        string songsRoot = Path.GetFullPath(ExternalContentPaths.PersistentSongsDirectory);
        string packageDirectory = Path.GetFullPath(Path.GetDirectoryName(result.packagePath) ?? string.Empty);
        sourceDirectory = Path.GetFullPath(sourceDirectory ?? string.Empty);
        string chartEditorDirectory = Path.GetFullPath(Path.Combine(ExternalContentPaths.PersistentSongsDirectory, ChartEditorProjectStore.ChartEditorSaveFolderName));

        Assert.AreEqual(songsRoot, packageDirectory, "Library conversion should write the .theory package directly under the user's Songs folder.");
        Assert.AreNotEqual(sourceDirectory, packageDirectory, "Library conversion should not hide the .theory package in the imported song's source subfolder.");
        Assert.IsFalse(packageDirectory.StartsWith(chartEditorDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(string.Equals(packageDirectory, chartEditorDirectory, StringComparison.OrdinalIgnoreCase));

        SongLibraryService.ClearCache();
        List<SongLibraryEntry> librarySongs = SongLibraryService.GetAvailableSongs(forceRefresh: true, refreshImports: false);
        Assert.IsTrue(
            librarySongs.Any(entry => entry != null &&
                                      string.Equals(Path.GetFullPath(entry.PrimaryNotationPath ?? string.Empty), Path.GetFullPath(result.packagePath), StringComparison.OrdinalIgnoreCase)),
            "Library scanner should discover .theory packages written directly under the user's Songs folder.");
    }

    [Test]
    public void LibraryTheoryConversion_RejectsChartEditorOutputFolder()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        string manifestPath = WriteCachedArrangementFixture(scope.RootPath);
        string sourceDirectory = Path.GetDirectoryName(manifestPath);
        string chartEditorDirectory = Path.Combine(ExternalContentPaths.PersistentSongsDirectory, ChartEditorProjectStore.ChartEditorSaveFolderName);

        Assert.IsFalse(ChartEditorTheoryConversionService.ConvertLibrarySourceToTheoryPackage(new ChartEditorTheoryConversionRequest
        {
            sourcePath = sourceDirectory,
            outputDirectory = chartEditorDirectory,
            validatePackage = true,
            requireAudio = true
        }, out _, out string conversionError), "Library conversion should not write into the chart editor save folder.");

        StringAssert.Contains("chart editor", conversionError.ToLowerInvariant());
        Assert.IsFalse(Directory.Exists(chartEditorDirectory) &&
                       Directory.GetFiles(chartEditorDirectory, $"*{TheoryPackageFormat.Extension}", SearchOption.AllDirectories).Length > 0,
            "Rejected library conversion should not leave .theory packages in the chart editor folder.");
    }

    [Test]
    public void HeadlessTheoryConversion_ExistingTheoryWithoutOutput_ReturnsExistingValidatedPackage()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);
        Assert.IsTrue(TheoryChartEditorExporter.ExportProject(project, scope.RootPath, out string packagePath, out string exportError), exportError);

        Assert.IsTrue(ChartEditorTheoryConversionService.ConvertToTheoryPackage(new ChartEditorTheoryConversionRequest
        {
            sourcePath = packagePath,
            validatePackage = true,
            requireAudio = true
        }, out ChartEditorTheoryConversionResult result, out string conversionError), conversionError);

        Assert.AreEqual(Path.GetFullPath(packagePath), result.packagePath);
        Assert.IsTrue(result.sourceAlreadyTheoryPackage);
        Assert.IsFalse(result.packageWasWritten);
        Assert.AreEqual(ChartEditorSourceKind.TheoryPackage, result.sourceKind);
        Assert.AreEqual(SongNotationSourceKind.TheoryPackage, result.sourceNotationKind);
    }

    [Test]
    public void RuntimeNormalizers_ConvertStaticBentBendSegmentsToBentSustain()
    {
        TheoryNoteData theoryNote = new TheoryNoteData
        {
            id = 901,
            duration = 1f,
            fret = 8,
            primaryTechnique = (int)NoteTechnique.Bend,
            bendStep = 2f,
            techniqueSegments = new List<TheoryTechniqueSegmentData>
            {
                new TheoryTechniqueSegmentData
                {
                    type = (int)NoteTechniqueSegmentType.Bend,
                    startOffset = 0f,
                    endOffset = 1f,
                    startFret = 8,
                    endFret = 8,
                    startBend = 2f,
                    endBend = 2f
                }
            }
        };

        List<NoteTechniqueSegmentData> theorySegments = TheoryTechniqueSegmentNormalizer.Build(theoryNote);
        Assert.IsFalse(theorySegments.Any(segment => segment.type == NoteTechniqueSegmentType.Bend));
        AssertRuntimeSegment(FindRuntimeSegment(theorySegments, NoteTechniqueSegmentType.Sustain, 0f, 1f), 8, 8, 2f, 2f);

        PsarcCachedNoteData cachedNote = new PsarcCachedNoteData
        {
            id = 902,
            duration = 1f,
            fret = 8,
            technique = (int)NoteTechnique.Bend,
            bendStep = 2f,
            techniqueSegments = new List<PsarcCachedTechniqueSegmentData>
            {
                new PsarcCachedTechniqueSegmentData
                {
                    type = (int)NoteTechniqueSegmentType.Bend,
                    startOffset = 0f,
                    endOffset = 1f,
                    startFret = 8,
                    endFret = 8,
                    startBend = 2f,
                    endBend = 2f
                }
            }
        };

        List<NoteTechniqueSegmentData> cachedSegments = PsarcTechniqueSegmentNormalizer.BuildNormalizedTechniqueSegments(cachedNote);
        Assert.IsFalse(cachedSegments.Any(segment => segment.type == NoteTechniqueSegmentType.Bend));
        AssertRuntimeSegment(FindRuntimeSegment(cachedSegments, NoteTechniqueSegmentType.Sustain, 0f, 1f), 8, 8, 2f, 2f);
    }

    [Test]
    public void ImportMusicXml_UsesInstrumentMetadataForGenericPartNames()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        string xmlPath = WriteInstrumentTaggedMusicXmlFixture(scope.RootPath);
        string audioPath = Path.Combine(scope.RootPath, "silence.ogg");
        File.WriteAllBytes(audioPath, new byte[] { 0x4f, 0x67, 0x67, 0x53 });

        List<MusicXmlLoader.MusicXmlPartSummary> summaries = SongNotationFacade.GetPartSummaries(xmlPath, SongNotationSourceKind.MusicXml);
        Assert.AreEqual(4, summaries.Count);
        AssertTheorySummaryTag(summaries, "P1", "Lead", "guitar");
        AssertTheorySummaryTag(summaries, "P2", "Bass", "bass");
        AssertTheorySummaryTag(summaries, "P3", "Piano", "piano");
        AssertTheorySummaryTag(summaries, "P4", "Drums", "drums");

        Assert.IsTrue(ChartEditorImportService.ImportChartAndAudio(xmlPath, audioPath, out ChartEditorImportResult result, out string importError), importError);
        ChartEditorProject project = result?.project;
        Assert.IsNotNull(project, "MusicXML import did not return a chart editor project.");
        Dictionary<string, ChartEditorTrack> tracksById = project.tracks.ToDictionary(track => track.id, track => track, StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual(ChartEditorTrackRole.LeadGuitar, tracksById["P1"].role);
        Assert.AreEqual(ChartEditorTrackRole.Bass, tracksById["P2"].role);
        Assert.AreEqual(ChartEditorTrackRole.Piano, tracksById["P3"].role);
        Assert.AreEqual(ChartEditorTrackRole.Drums, tracksById["P4"].role);
        CollectionAssert.AreEqual(new[] { 40, 45, 50, 55, 59, 64 }, tracksById["P1"].tuning.stringPitches);
        CollectionAssert.AreEqual(new[] { 28, 33, 38, 43 }, tracksById["P2"].tuning.stringPitches);
        Assert.AreEqual(1, tracksById["P4"].notes.Count);
        Assert.AreEqual(0, tracksById["P4"].notes[0].stringOrLane);
        Assert.AreEqual(42, tracksById["P4"].notes[0].fret);
        Assert.AreEqual("Hi-Hat", tracksById["P4"].notes[0].noteName);
        Assert.IsFalse(tracksById["P4"].notes[0].tap);
        Assert.IsTrue(tracksById["P4"].notes[0].requiresPluck);
    }

    [Test]
    public void ValidationWarnsWhenInstrumentTagConflictsWithStringEvidence()
    {
        ChartEditorProject project = new ChartEditorProject
        {
            audio = new ChartEditorAudioInfo { sourcePath = "missing.ogg" },
            beatMap = new ChartEditorBeatMap
            {
                beatMarkers = new List<ChartEditorBeatMarker>
                {
                    new ChartEditorBeatMarker { id = "a", audioTimeSeconds = 0.0, beatPosition = 0.0, isAnchor = true },
                    new ChartEditorBeatMarker { id = "b", audioTimeSeconds = 1.0, beatPosition = 2.0, isAnchor = true }
                }
            },
            tracks = new List<ChartEditorTrack>
            {
                new ChartEditorTrack
                {
                    id = "wrong_bass",
                    displayName = "Six String Tagged Bass",
                    role = ChartEditorTrackRole.Bass,
                    tuning = new ChartEditorTuningInfo
                    {
                        displayName = "E Standard",
                        stringPitches = new[] { 40, 45, 50, 55, 59, 64 }
                    },
                    notes = new List<ChartEditorNote>
                    {
                        new ChartEditorNote { id = "n1", timeSeconds = 0.5, stringOrLane = 5, fret = 3 }
                    }
                }
            }
        };

        List<string> warnings = ChartEditorValidationService.BuildWarnings(project);
        Assert.IsTrue(
            warnings.Any(warning => warning.IndexOf("tagged Bass", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                    warning.IndexOf("6 string lanes", StringComparison.OrdinalIgnoreCase) >= 0),
            "A bass tag on six-string data should warn instead of silently changing routing semantics.");
    }

    [Test]
    public void ImportFolder_WithInvalidTheoryPackage_UsesImporterBackedCacheFolder()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        string manifestPath = WriteCachedArrangementFixture(scope.RootPath);
        string songDirectory = Path.GetDirectoryName(manifestPath);
        File.WriteAllText(Path.Combine(songDirectory, "aaa_broken.theory"), "not a theory package");

        Assert.IsTrue(ChartEditorImportService.ImportFolder(songDirectory, out ChartEditorImportResult result, out string importError), importError);
        ChartEditorProject project = result?.project;
        Assert.IsNotNull(project, "Folder import should use the importer-backed cache folder after ignoring the broken .theory package.");
        Assert.AreEqual(ChartEditorSourceKind.ExternalImporter, project.sourceKind);
        Assert.AreEqual(Path.GetFullPath(songDirectory), project.sourcePath);
        Assert.AreEqual("Import Translation Song", project.metadata.title);
        Assert.AreEqual(2, project.tracks.Count);
        Assert.IsTrue(project.tracks.Any(track => track != null && track.id == "lead_full"));
        Assert.IsTrue(project.tracks.Any(track => track != null && track.id == "lead_easy"));
    }

    [Test]
    public void SongLibrary_WithInvalidTheoryPackage_ExposesCacheFolderOnlyAsImportCandidate()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        string manifestPath = WriteCachedArrangementFixture(ExternalContentPaths.PersistentSongsDirectory);
        string songDirectory = Path.GetDirectoryName(manifestPath);
        File.WriteAllText(Path.Combine(songDirectory, "aaa_broken.theory"), "not a theory package");

        SongLibraryService.ClearCache();
        List<SongLibraryEntry> songs = SongLibraryService.GetAvailableSongs(forceRefresh: true, refreshImports: false);
        SongLibraryEntry entry = songs.FirstOrDefault(song =>
            song != null &&
            string.Equals(song.SongDirectory, songDirectory, StringComparison.OrdinalIgnoreCase));

        Assert.IsNull(entry, "Library scanner should not live-load importer-backed cache folders as playable songs.");

        List<SongLibraryImportCandidate> candidates = SongLibraryService.DiscoverPendingTheoryConversionCandidates();
        Assert.IsTrue(
            candidates.Any(candidate =>
                candidate != null &&
                string.Equals(Path.GetFullPath(candidate.SourcePath), Path.GetFullPath(songDirectory), StringComparison.OrdinalIgnoreCase)),
            "Importer-backed cache folders should appear as pending conversion candidates.");
    }

    [Test]
    public void SongLibrary_RawNotationImportCandidate_UsesSupportedNonMp3Audio()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        string songDirectory = Path.Combine(ExternalContentPaths.PersistentSongsDirectory, "Flac Audio Song");
        Directory.CreateDirectory(songDirectory);
        string xmlPath = WriteInstrumentTaggedMusicXmlFixture(songDirectory);
        string audioPath = Path.Combine(songDirectory, "song.flac");
        File.WriteAllBytes(audioPath, new byte[] { 0x66, 0x4c, 0x61, 0x43 });

        SongLibraryService.ClearCache();
        List<SongLibraryImportCandidate> candidates = SongLibraryService.DiscoverPendingTheoryConversionCandidates();
        SongLibraryImportCandidate candidate = candidates.FirstOrDefault(item =>
            item != null &&
            string.Equals(Path.GetFullPath(item.SourcePath), Path.GetFullPath(xmlPath), StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(candidate, "Raw MusicXML song should be offered for .theory conversion.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(candidate.AudioPath), "Raw notation conversion should include a paired audio path.");
        Assert.AreEqual(Path.GetFullPath(audioPath), Path.GetFullPath(candidate.AudioPath), "Raw notation conversion should pair every supported editor audio type, not only MP3/WAV/OGG.");
    }

    private static void PrepareProjectForNoEditRoundTrip(ChartEditorProject project)
    {
        project.EnsureDefaults();
        ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: true);
        project.dirty = false;
    }

    private static void AttachFixtureAudio(ChartEditorProject project, string rootPath)
    {
        string audioPath = Path.Combine(rootPath, "silence.ogg");
        File.WriteAllBytes(audioPath, new byte[] { 0x4f, 0x67, 0x67, 0x53 });
        project.audio.sourcePath = audioPath;
        project.audio.displayName = Path.GetFileName(audioPath);
        project.audio.extension = ".ogg";
    }

    private static void AttachFixtureCover(ChartEditorProject project, string rootPath)
    {
        string coverPath = Path.Combine(rootPath, "cover.png");
        byte[] pngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
        File.WriteAllBytes(coverPath, pngBytes);
        project.metadata.coverImagePath = coverPath;
    }

    private static void AttachGeneratedPlaybackEvent(ChartEditorTrack track)
    {
        track.generatedPart = new ChartEditorGeneratedPartInfo
        {
            partId = track.id,
            displayName = track.displayName,
            instrumentName = "Guitar",
            sourceMidiChannel = 0,
            sourceMidiProgram = 29,
            preferredBank = -1,
            isGuitarFamily = true
        };
        track.generatedNotes = new List<ChartEditorGeneratedNoteEvent>
        {
            new ChartEditorGeneratedNoteEvent
            {
                startTimeSeconds = 1.2f,
                durationSeconds = 0.9f,
                pitchPreRollSeconds = 0.05f,
                midiNote = 68,
                velocity = 96,
                channel = 0,
                partId = track.id,
                partName = track.displayName,
                techniqueVariant = (int)GeneratedTechniqueVariant.Normal,
                legatoTransitionKind = (int)GeneratedLegatoTransitionKind.None,
                attackVelocityScale = 0.85f,
                vibratoDepthSemitones = 0.1f,
                vibratoRateHz = 5.5f,
                vibratoDelayNormalized = 0.2f,
                vibratoFadeNormalized = 0.3f,
                pitchBendRangeSemitones = 2,
                pitchCurve = new List<ChartEditorGeneratedPitchPoint>
                {
                    new ChartEditorGeneratedPitchPoint { normalizedTime = 0f, semitoneOffset = 0f },
                    new ChartEditorGeneratedPitchPoint { normalizedTime = 0.5f, semitoneOffset = 1f },
                    new ChartEditorGeneratedPitchPoint { normalizedTime = 1f, semitoneOffset = 2f }
                }
            }
        };
        track.generatedPlaybackNoteFingerprint = ChartEditorGeneratedPlaybackIntegrity.ComputeNoteFingerprint(track);
    }

    private static void AttachToneData(ChartEditorTrack track)
    {
        track.tones = new ChartEditorToneData
        {
            baseToneName = "Clean Intro",
            changes = new List<ChartEditorToneChange>
            {
                new ChartEditorToneChange
                {
                    timeSeconds = 0.5f,
                    toneName = "Clean Intro",
                    toneId = 1
                },
                new ChartEditorToneChange
                {
                    timeSeconds = 2.25f,
                    toneName = "Solo Lead",
                    toneId = 2
                }
            },
            definitions = new List<ChartEditorToneDefinition>
            {
                new ChartEditorToneDefinition
                {
                    name = "Clean Intro",
                    key = "clean_intro",
                    preset = CreateTonePreset("tone_clean_intro", "Clean Intro", UnityToneLabRuntime.ToneLabPedalType.Amp, "clean-amp")
                },
                new ChartEditorToneDefinition
                {
                    name = "Solo Lead",
                    key = "solo_lead",
                    preset = CreateTonePreset("tone_solo_lead", "Solo Lead", UnityToneLabRuntime.ToneLabPedalType.Distortion, "lead-drive")
                }
            }
        };
    }

    private static void AttachAlternateToneData(ChartEditorTrack track)
    {
        track.tones = new ChartEditorToneData
        {
            baseToneName = "Easy Placeholder",
            changes = new List<ChartEditorToneChange>
            {
                new ChartEditorToneChange
                {
                    timeSeconds = 0.25f,
                    toneName = "Easy Placeholder",
                    toneId = 0
                }
            },
            definitions = new List<ChartEditorToneDefinition>
            {
                new ChartEditorToneDefinition
                {
                    name = "Easy Placeholder",
                    key = "easy_placeholder",
                    preset = CreateTonePreset("tone_easy_placeholder", "Easy Placeholder", UnityToneLabRuntime.ToneLabPedalType.Amp, "easy-amp")
                }
            }
        };
    }

    private static void AssertTheorySummaryTag(
        IEnumerable<MusicXmlLoader.MusicXmlPartSummary> summaries,
        string partId,
        string expectedRoute,
        string expectedInstrumentType)
    {
        MusicXmlLoader.MusicXmlPartSummary summary = summaries.FirstOrDefault(candidate =>
            candidate != null &&
            string.Equals(candidate.PartId, partId, StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(summary, $"Missing .theory summary for part '{partId}'.");
        Assert.AreEqual(expectedRoute, summary.Route);
        Assert.AreEqual(expectedInstrumentType, summary.InstrumentType);
    }

    private static ChartEditorTonePresetData CreateTonePreset(
        string presetId,
        string presetName,
        UnityToneLabRuntime.ToneLabPedalType pedalType,
        string descriptorId)
    {
        return new ChartEditorTonePresetData
        {
            presetId = presetId,
            presetName = presetName,
            inputGainDb = -1.5f,
            outputGainDb = 2.25f,
            pedalChain = new List<ChartEditorTonePedalSlotData>
            {
                new ChartEditorTonePedalSlotData
                {
                    instanceId = $"{presetId}_slot",
                    pedalType = pedalType.ToString(),
                    descriptorId = descriptorId,
                    enabled = true,
                    settingsJson = "{\"level\":0.75}"
                }
            }
        };
    }

    private static TheoryTonePresetData CreateTheoryTonePreset(
        string presetId,
        string presetName,
        string pedalType,
        string descriptorId)
    {
        return new TheoryTonePresetData
        {
            presetId = presetId,
            presetName = presetName,
            inputGainDb = -2f,
            outputGainDb = 1.25f,
            pedalChain = new List<TheoryTonePedalSlotData>
            {
                new TheoryTonePedalSlotData
                {
                    instanceId = $"{presetId}_slot",
                    pedalType = pedalType,
                    descriptorId = descriptorId,
                    enabled = true,
                    settingsJson = "{\"level\":0.55}"
                }
            }
        };
    }

    private static StemCacheManifest WriteGeneratedStemCache(string sourceAudioPath)
    {
        string cacheDirectory = StemSeparationService.GetCacheDirectory(sourceAudioPath);
        if (Directory.Exists(cacheDirectory))
            Directory.Delete(cacheDirectory, true);

        string stemsDirectory = Path.Combine(cacheDirectory, "stems");
        Directory.CreateDirectory(stemsDirectory);
        File.WriteAllBytes(Path.Combine(stemsDirectory, "guitar.ogg"), new byte[] { 0x4f, 0x67, 0x67, 0x53, 0x01 });
        File.WriteAllBytes(Path.Combine(stemsDirectory, "bass.ogg"), new byte[] { 0x4f, 0x67, 0x67, 0x53, 0x02 });

        FileInfo sourceInfo = new FileInfo(sourceAudioPath);
        StemCacheManifest manifest = new StemCacheManifest
        {
            schemaVersion = 1,
            sourceAudioPath = Path.GetFullPath(sourceAudioPath),
            sourceAudioLastWriteUtcTicks = sourceInfo.LastWriteTimeUtc.Ticks,
            sourceAudioSizeBytes = sourceInfo.Length,
            provider = "demucs",
            model = StemSeparationService.DefaultDemucsModel,
            generatedAtUtcTicks = 987654321,
            status = "ready",
            error = string.Empty,
            stems = new List<StemCacheEntry>
            {
                new StemCacheEntry
                {
                    id = "guitar",
                    displayName = "Guitar",
                    relativePath = "stems/guitar.ogg"
                },
                new StemCacheEntry
                {
                    id = "bass",
                    displayName = "Bass",
                    relativePath = "stems/bass.ogg"
                }
            }
        };

        File.WriteAllText(Path.Combine(cacheDirectory, StemSeparationService.ManifestFileName), JsonUtility.ToJson(manifest, true));
        return manifest;
    }

    private static void AssertPackageEntryExists(string packagePath, string entryName)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        Assert.IsNotNull(archive.GetEntry(entryName), $"Package entry '{entryName}' was not found.");
    }

    private static string ReadPackageEntry(string packagePath, string entryName)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        ZipArchiveEntry entry = archive.GetEntry(entryName);
        Assert.IsNotNull(entry, $"Package entry '{entryName}' was not found.");
        using Stream stream = entry.Open();
        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static ChartEditorProject CreateRoundTripProject(string rootPath)
    {
        ChartEditorProject project = new ChartEditorProject
        {
            projectId = "chart_editor_roundtrip_project",
            sourceKind = ChartEditorSourceKind.StringTheoryProject,
            sourcePath = "unit-test-source",
            sourceFolder = rootPath,
            savedProjectPath = Path.Combine(rootPath, "ChartEditorProjects", "roundtrip.stchart.json"),
            metadata = new ChartEditorSongMetadata
            {
                title = "Round Trip Song",
                artist = "Unit Test Band",
                album = "Regression Suite",
                genre = "Rock",
                year = "2026"
            },
            audio = new ChartEditorAudioInfo
            {
                displayName = "silence.ogg",
                extension = ".ogg",
                durationSeconds = 8.0
            },
            settings = new ChartEditorProjectSettings
            {
                snapEnabled = true,
                snapSeconds = 0.01,
                playbackSpeed = 1f,
                showBeatGrid = true
            },
            beatMap = new ChartEditorBeatMap
            {
                defaultTempoBpm = 120.0,
                timeSignatures = new List<ChartEditorTimeSignatureChange>
                {
                    new ChartEditorTimeSignatureChange { beatPosition = 0.0, numerator = 4, denominator = 4 }
                },
                beatMarkers = new List<ChartEditorBeatMarker>
                {
                    new ChartEditorBeatMarker { id = "anchor_start", beatPosition = 0.0, audioTimeSeconds = 0.0, isAnchor = true, label = "Start" },
                    new ChartEditorBeatMarker { id = "anchor_drift", beatPosition = 4.0, audioTimeSeconds = 2.1, isAnchor = true, label = "Drift" },
                    new ChartEditorBeatMarker { id = "anchor_solo", beatPosition = 8.0, audioTimeSeconds = 4.3, isAnchor = true, label = "Solo" }
                }
            },
            sections = new List<ChartEditorSection>
            {
                new ChartEditorSection
                {
                    id = "section_intro",
                    name = "intro",
                    startTimeSeconds = 0.0,
                    endTimeSeconds = 2.1,
                    chartStartTimeSeconds = 0.0,
                    chartEndTimeSeconds = 2.1,
                    colorHex = "#7C4DFF",
                    userEdited = true
                },
                new ChartEditorSection
                {
                    id = "section_solo",
                    name = "solo",
                    startTimeSeconds = 2.1,
                    endTimeSeconds = 4.3,
                    chartStartTimeSeconds = 2.1,
                    chartEndTimeSeconds = 4.3,
                    colorHex = "#42A5F5",
                    userEdited = true
                }
            },
            tracks = new List<ChartEditorTrack>
            {
                CreateLeadTrack(),
                CreateBassTrack()
            },
            selectedTrackId = "lead"
        };

        return project;
    }

    private static ChartEditorTrack CreateLeadTrack()
    {
        return new ChartEditorTrack
        {
            id = "lead",
            importedName = "Lead Guitar",
            displayName = "Lead Guitar",
            role = ChartEditorTrackRole.LeadGuitar,
            colorHex = "#9B5CFF",
            tuning = new ChartEditorTuningInfo
            {
                displayName = "E Standard",
                stringPitches = new[] { 40, 45, 50, 55, 59, 64 }
            },
            notes = new List<ChartEditorNote>
            {
                new ChartEditorNote
                {
                    id = "lead_hammer",
                    sourceNoteId = 100,
                    timeSeconds = 0.50,
                    chartTimeSeconds = 0.50,
                    durationSeconds = 0.18,
                    stringOrLane = 1,
                    fret = 5,
                    noteName = "D",
                    technique = NoteTechnique.HammerOn,
                    legato = true,
                    requiresPluck = false
                },
                new ChartEditorNote
                {
                    id = "lead_prebend_sustain",
                    sourceNoteId = 101,
                    timeSeconds = 1.20,
                    chartTimeSeconds = 1.20,
                    durationSeconds = 0.90,
                    stringOrLane = 3,
                    fret = 8,
                    noteName = "Bb",
                    technique = NoteTechnique.Bend,
                    bendStep = 2f,
                    maxBend = 2f,
                    bendPreBend = true,
                    requiresPluck = true,
                    techniqueSegments = new List<ChartEditorTechniqueSegment>
                    {
                        new ChartEditorTechniqueSegment
                        {
                            type = NoteTechniqueSegmentType.Sustain,
                            startOffset = 0f,
                            endOffset = 0.90f,
                            startFret = 8,
                            endFret = 8,
                            startBend = 2f,
                            endBend = 2f
                        }
                    }
                },
                new ChartEditorNote
                {
                    id = "lead_bend_release",
                    sourceNoteId = 102,
                    timeSeconds = 2.45,
                    chartTimeSeconds = 2.45,
                    durationSeconds = 1.30,
                    stringOrLane = 4,
                    fret = 12,
                    noteName = "E",
                    technique = NoteTechnique.Bend,
                    bendStep = 2f,
                    maxBend = 2f,
                    bendRelease = true,
                    techniqueSegments = new List<ChartEditorTechniqueSegment>
                    {
                        new ChartEditorTechniqueSegment
                        {
                            type = NoteTechniqueSegmentType.Sustain,
                            startOffset = 0f,
                            endOffset = 1.30f,
                            startFret = 12,
                            endFret = 12
                        },
                        new ChartEditorTechniqueSegment
                        {
                            type = NoteTechniqueSegmentType.Bend,
                            startOffset = 0.12f,
                            endOffset = 0.42f,
                            startFret = 12,
                            endFret = 12,
                            startBend = 0f,
                            endBend = 2f
                        },
                        new ChartEditorTechniqueSegment
                        {
                            type = NoteTechniqueSegmentType.Bend,
                            startOffset = 0.42f,
                            endOffset = 0.74f,
                            startFret = 12,
                            endFret = 12,
                            startBend = 2f,
                            endBend = 0f
                        }
                    }
                },
                new ChartEditorNote
                {
                    id = "lead_slide_flags",
                    sourceNoteId = 103,
                    timeSeconds = 4.60,
                    chartTimeSeconds = 4.60,
                    durationSeconds = 0.75,
                    stringOrLane = 2,
                    fret = 7,
                    noteName = "D",
                    technique = NoteTechnique.Slide,
                    slideTargetFret = 10,
                    harmonic = true,
                    accent = true,
                    tap = true,
                    tremolo = true,
                    techniqueSegments = new List<ChartEditorTechniqueSegment>
                    {
                        new ChartEditorTechniqueSegment
                        {
                            type = NoteTechniqueSegmentType.Slide,
                            startOffset = 0f,
                            endOffset = 0.75f,
                            startFret = 7,
                            endFret = 10
                        }
                    }
                },
                new ChartEditorNote
                {
                    id = "lead_pinch_vibrato",
                    sourceNoteId = 104,
                    timeSeconds = 5.80,
                    chartTimeSeconds = 5.80,
                    durationSeconds = 0.90,
                    stringOrLane = 0,
                    fret = 3,
                    noteName = "G",
                    technique = NoteTechnique.Vibrato,
                    pinchHarmonic = true,
                    vibratoStrength = 2,
                    techniqueSegments = new List<ChartEditorTechniqueSegment>
                    {
                        new ChartEditorTechniqueSegment
                        {
                            type = NoteTechniqueSegmentType.Vibrato,
                            startOffset = 0f,
                            endOffset = 0.90f,
                            startFret = 3,
                            endFret = 3
                        }
                    }
                }
            },
            arpeggioGuides = new List<ChartEditorArpeggioGuide>
            {
                new ChartEditorArpeggioGuide
                {
                    id = 1,
                    startTime = 1.0f,
                    endTime = 2.0f,
                    chordName = "G",
                    stringFrets = new[] { 3, 2, 0, 0, 0, 3 }
                }
            }
        };
    }

    private static ChartEditorTrack CreateBassTrack()
    {
        return new ChartEditorTrack
        {
            id = "bass",
            importedName = "Bass",
            displayName = "Bass",
            role = ChartEditorTrackRole.Bass,
            colorHex = "#35D07F",
            tuning = new ChartEditorTuningInfo
            {
                displayName = "E Standard Bass",
                stringPitches = new[] { 28, 33, 38, 43 }
            },
            notes = new List<ChartEditorNote>
            {
                new ChartEditorNote
                {
                    id = "bass_palm",
                    sourceNoteId = 200,
                    timeSeconds = 0.35,
                    chartTimeSeconds = 0.35,
                    durationSeconds = 0.30,
                    stringOrLane = 0,
                    fret = 3,
                    noteName = "G",
                    palmMute = true,
                    muted = true
                },
                new ChartEditorNote
                {
                    id = "bass_fhm",
                    sourceNoteId = 201,
                    timeSeconds = 1.70,
                    chartTimeSeconds = 1.70,
                    durationSeconds = 0.55,
                    stringOrLane = 2,
                    fret = 5,
                    noteName = "C",
                    fretHandMute = true,
                    muted = true
                }
            }
        };
    }

    private static ChartEditorTrack CreateDrumTrack()
    {
        return new ChartEditorTrack
        {
            id = "drums",
            importedName = "Drums",
            displayName = "Drums",
            role = ChartEditorTrackRole.Drums,
            colorHex = "#FFB020",
            notes = new List<ChartEditorNote>
            {
                new ChartEditorNote
                {
                    id = "drum_hihat",
                    sourceNoteId = 300,
                    timeSeconds = 0.25,
                    chartTimeSeconds = 0.25,
                    durationSeconds = 0.10,
                    stringOrLane = 0,
                    fret = 42,
                    noteName = "Hi-Hat"
                },
                new ChartEditorNote
                {
                    id = "drum_crash",
                    sourceNoteId = 301,
                    timeSeconds = 0.50,
                    chartTimeSeconds = 0.50,
                    durationSeconds = 0.10,
                    stringOrLane = 1,
                    fret = 49,
                    noteName = "Crash Cymbal"
                },
                new ChartEditorNote
                {
                    id = "drum_snare",
                    sourceNoteId = 302,
                    timeSeconds = 0.75,
                    chartTimeSeconds = 0.75,
                    durationSeconds = 0.10,
                    stringOrLane = 2,
                    fret = 38,
                    noteName = "Snare"
                },
                new ChartEditorNote
                {
                    id = "drum_kick",
                    sourceNoteId = 303,
                    timeSeconds = 1.00,
                    chartTimeSeconds = 1.00,
                    durationSeconds = 0.10,
                    stringOrLane = 4,
                    fret = 36,
                    noteName = "Kick"
                }
            }
        };
    }

    private static ChartEditorTrack CreatePianoTrack()
    {
        return new ChartEditorTrack
        {
            id = "piano",
            importedName = "Part C",
            displayName = "Part C",
            role = ChartEditorTrackRole.Piano,
            colorHex = "#D7E2FF",
            notes = new List<ChartEditorNote>
            {
                new ChartEditorNote
                {
                    id = "piano_c4",
                    sourceNoteId = 400,
                    timeSeconds = 0.65,
                    chartTimeSeconds = 0.65,
                    durationSeconds = 0.50,
                    stringOrLane = 0,
                    fret = 60,
                    noteName = "C4"
                },
                new ChartEditorNote
                {
                    id = "piano_e4",
                    sourceNoteId = 401,
                    timeSeconds = 1.15,
                    chartTimeSeconds = 1.15,
                    durationSeconds = 0.45,
                    stringOrLane = 1,
                    fret = 64,
                    noteName = "E4"
                }
            }
        };
    }

    private static string ReadTheoryPackageSnapshot(string packagePath)
    {
        Assert.IsTrue(File.Exists(packagePath), "Export did not create a .theory package.");

        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        string[] jsonEntries = archive.Entries
            .Where(entry => entry != null && entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.FullName)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Greater(jsonEntries.Length, 0, "The .theory package did not contain chart JSON.");

        List<string> snapshot = new List<string>();
        for (int i = 0; i < jsonEntries.Length; i++)
            snapshot.Add(jsonEntries[i] + "\n" + ReadPackageEntry(packagePath, jsonEntries[i]));

        return string.Join("\n---\n", snapshot);
    }

    private static string WriteInstrumentTaggedMusicXmlFixture(string rootPath)
    {
        string path = Path.Combine(rootPath, "instrument_tags.musicxml");
        File.WriteAllText(path, @"<?xml version=""1.0"" encoding=""UTF-8""?>
<score-partwise version=""3.1"">
  <work><work-title>Instrument Tags</work-title></work>
  <identification><creator type=""composer"">Unit Test Band</creator></identification>
  <part-list>
    <score-part id=""P1"">
      <part-name>Part A</part-name>
      <score-instrument id=""P1-I1"">
        <instrument-name>Electric Guitar</instrument-name>
        <instrument-sound>pluck.guitar.electric</instrument-sound>
      </score-instrument>
      <midi-instrument id=""P1-I1""><midi-channel>1</midi-channel><midi-program>30</midi-program></midi-instrument>
    </score-part>
    <score-part id=""P2"">
      <part-name>Part B</part-name>
      <score-instrument id=""P2-I1"">
        <instrument-name>Electric Bass</instrument-name>
        <instrument-sound>pluck.bass.electric</instrument-sound>
      </score-instrument>
      <midi-instrument id=""P2-I1""><midi-channel>2</midi-channel><midi-program>34</midi-program></midi-instrument>
    </score-part>
    <score-part id=""P3"">
      <part-name>Part C</part-name>
      <score-instrument id=""P3-I1"">
        <instrument-name>Piano</instrument-name>
        <instrument-sound>keyboard.piano</instrument-sound>
      </score-instrument>
      <midi-instrument id=""P3-I1""><midi-channel>3</midi-channel><midi-program>1</midi-program></midi-instrument>
    </score-part>
    <score-part id=""P4"">
      <part-name>Part D</part-name>
      <score-instrument id=""P4-I1"">
        <instrument-name>Drumset</instrument-name>
        <instrument-sound>drum-set.standard</instrument-sound>
      </score-instrument>
      <midi-instrument id=""P4-I1""><midi-channel>10</midi-channel><midi-program>1</midi-program><midi-unpitched>42</midi-unpitched></midi-instrument>
    </score-part>
  </part-list>
  <part id=""P1"">
    <measure number=""1"">
      <attributes>
        <divisions>1</divisions>
        <time><beats>4</beats><beat-type>4</beat-type></time>
        <staff-details>
          <staff-lines>6</staff-lines>
          <staff-tuning line=""1""><tuning-step>E</tuning-step><tuning-octave>2</tuning-octave></staff-tuning>
          <staff-tuning line=""2""><tuning-step>A</tuning-step><tuning-octave>2</tuning-octave></staff-tuning>
          <staff-tuning line=""3""><tuning-step>D</tuning-step><tuning-octave>3</tuning-octave></staff-tuning>
          <staff-tuning line=""4""><tuning-step>G</tuning-step><tuning-octave>3</tuning-octave></staff-tuning>
          <staff-tuning line=""5""><tuning-step>B</tuning-step><tuning-octave>3</tuning-octave></staff-tuning>
          <staff-tuning line=""6""><tuning-step>E</tuning-step><tuning-octave>4</tuning-octave></staff-tuning>
        </staff-details>
      </attributes>
      <note><pitch><step>G</step><octave>2</octave></pitch><duration>1</duration><type>quarter</type><notations><technical><string>1</string><fret>3</fret></technical></notations></note>
    </measure>
  </part>
  <part id=""P2"">
    <measure number=""1"">
      <attributes>
        <divisions>1</divisions>
        <time><beats>4</beats><beat-type>4</beat-type></time>
        <staff-details>
          <staff-lines>4</staff-lines>
          <staff-tuning line=""1""><tuning-step>E</tuning-step><tuning-octave>1</tuning-octave></staff-tuning>
          <staff-tuning line=""2""><tuning-step>A</tuning-step><tuning-octave>1</tuning-octave></staff-tuning>
          <staff-tuning line=""3""><tuning-step>D</tuning-step><tuning-octave>2</tuning-octave></staff-tuning>
          <staff-tuning line=""4""><tuning-step>G</tuning-step><tuning-octave>2</tuning-octave></staff-tuning>
        </staff-details>
      </attributes>
      <note><pitch><step>G</step><octave>1</octave></pitch><duration>1</duration><type>quarter</type><notations><technical><string>1</string><fret>3</fret></technical></notations></note>
    </measure>
  </part>
  <part id=""P3"">
    <measure number=""1"">
      <attributes><divisions>1</divisions><time><beats>4</beats><beat-type>4</beat-type></time></attributes>
      <note><pitch><step>C</step><octave>4</octave></pitch><duration>1</duration><type>quarter</type></note>
    </measure>
  </part>
  <part id=""P4"">
    <measure number=""1"">
      <attributes><divisions>1</divisions><time><beats>4</beats><beat-type>4</beat-type></time></attributes>
      <note><unpitched><display-step>C</display-step><display-octave>5</display-octave></unpitched><duration>1</duration><type>quarter</type><instrument id=""P4-I1"" /></note>
    </measure>
  </part>
</score-partwise>");
        return path;
    }

    private static string WriteCachedArrangementFixture(string rootPath)
    {
        string sourceDirectory = Path.Combine(rootPath, "import_fixture");
        string arrangementsDirectory = Path.Combine(sourceDirectory, "arrangements");
        Directory.CreateDirectory(arrangementsDirectory);

        int[] eStandard = { 40, 45, 50, 55, 59, 64 };
        PsarcCachedArrangementPart fullPart = CreateFullDifficultyCachedPart(eStandard);
        PsarcCachedArrangementPart easyPart = CreateEasyDifficultyCachedPart(eStandard);

        string fullPartPath = Path.Combine(arrangementsDirectory, "lead_full.rs2part.json");
        string easyPartPath = Path.Combine(arrangementsDirectory, "lead_easy.rs2part.json");
        File.WriteAllText(fullPartPath, JsonUtility.ToJson(fullPart, true));
        File.WriteAllText(easyPartPath, JsonUtility.ToJson(easyPart, true));

        string audioPath = Path.Combine(sourceDirectory, "silence.ogg");
        File.WriteAllBytes(audioPath, Array.Empty<byte>());

        PsarcCachedSongManifest manifest = new PsarcCachedSongManifest
        {
            schemaVersion = PsarcCachedSongFormat.SchemaVersion,
            sourcePsarcPath = string.Empty,
            sourcePsarcLastWriteUtcTicks = 0,
            importedAtUtcTicks = 123456789,
            displayName = "Import Translation Song",
            artist = "Unit Test Band",
            album = "Regression Suite",
            audioPath = audioPath,
            previewAudioPath = audioPath,
            durationSeconds = 8f,
            difficultyRating = 4,
            arrangements = new List<PsarcCachedArrangementSummary>
            {
                new PsarcCachedArrangementSummary
                {
                    partId = "lead_easy",
                    displayName = "Lead Guitar",
                    route = "Lead",
                    arrangementGroupId = "lead",
                    arrangementDisplayName = "Lead Guitar",
                    difficultyLabel = "1",
                    difficultyUiIndex = 1,
                    hasDifficultyVariants = true,
                    partFilePath = Path.Combine("arrangements", "lead_easy.rs2part.json"),
                    noteCount = easyPart.notes.Count,
                    tabCount = easyPart.notes.Count,
                    score = easyPart.notes.Count,
                    difficultyRating = 1,
                    tuningPitches = (int[])eStandard.Clone(),
                    tuningDisplayName = "E Standard"
                },
                new PsarcCachedArrangementSummary
                {
                    partId = "lead_full",
                    displayName = "Lead Guitar",
                    route = "Lead",
                    arrangementGroupId = "lead",
                    arrangementDisplayName = "Lead Guitar",
                    difficultyLabel = "Full",
                    difficultyUiIndex = 0,
                    hasDifficultyVariants = true,
                    partFilePath = Path.Combine("arrangements", "lead_full.rs2part.json"),
                    noteCount = fullPart.notes.Count,
                    tabCount = fullPart.notes.Count,
                    score = fullPart.notes.Count,
                    difficultyRating = 4,
                    tuningPitches = (int[])eStandard.Clone(),
                    tuningDisplayName = "E Standard"
                }
            }
        };

        string manifestPath = Path.Combine(sourceDirectory, PsarcCachedSongFormat.ManifestFileName);
        File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
        return manifestPath;
    }

    private static string WriteCachedDrumArrangementFixture(string rootPath)
    {
        string sourceDirectory = Path.Combine(rootPath, "drum_import_fixture");
        string arrangementsDirectory = Path.Combine(sourceDirectory, "arrangements");
        Directory.CreateDirectory(arrangementsDirectory);

        PsarcCachedArrangementPart drumPart = CreateCachedDrumArrangementPart();
        string drumPartPath = Path.Combine(arrangementsDirectory, "drums_full.rs2part.json");
        File.WriteAllText(drumPartPath, JsonUtility.ToJson(drumPart, true));

        string audioPath = Path.Combine(sourceDirectory, "silence.ogg");
        File.WriteAllBytes(audioPath, Array.Empty<byte>());

        PsarcCachedSongManifest manifest = new PsarcCachedSongManifest
        {
            schemaVersion = PsarcCachedSongFormat.SchemaVersion,
            sourcePsarcPath = Path.Combine(sourceDirectory, "source.psarc"),
            sourcePsarcLastWriteUtcTicks = 123456789,
            importedAtUtcTicks = 123456790,
            displayName = "Drum Import Song",
            artist = "Unit Test Band",
            album = "Regression Suite",
            audioPath = audioPath,
            previewAudioPath = audioPath,
            durationSeconds = 8f,
            difficultyRating = 4,
            arrangements = new List<PsarcCachedArrangementSummary>
            {
                new PsarcCachedArrangementSummary
                {
                    partId = "drums_full",
                    displayName = "Drums",
                    route = "Drums",
                    arrangementGroupId = "drums",
                    arrangementDisplayName = "Drums",
                    difficultyLabel = "Full",
                    difficultyUiIndex = 0,
                    hasDifficultyVariants = false,
                    partFilePath = Path.Combine("arrangements", "drums_full.rs2part.json"),
                    noteCount = drumPart.notes.Count,
                    tabCount = drumPart.notes.Count,
                    score = drumPart.notes.Count,
                    difficultyRating = 4
                }
            }
        };

        string manifestPath = Path.Combine(sourceDirectory, PsarcCachedSongFormat.ManifestFileName);
        File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
        return manifestPath;
    }

    private static PsarcCachedArrangementPart CreateCachedDrumArrangementPart()
    {
        return new PsarcCachedArrangementPart
        {
            schemaVersion = PsarcCachedSongFormat.SchemaVersion,
            partId = "drums_full",
            displayName = "Drums",
            route = "Drums",
            arrangementGroupId = "drums",
            arrangementDisplayName = "Drums",
            difficultyLabel = "Full",
            difficultyUiIndex = 0,
            hasDifficultyVariants = false,
            durationSeconds = 8f,
            difficultyRating = 4,
            generatedPart = new PsarcCachedGeneratedPartInfo
            {
                partId = "drums_full",
                displayName = "Drums",
                instrumentName = "Drums",
                sourceMidiChannel = 9,
                sourceMidiProgram = 0,
                preferredBank = -1,
                isDrum = true,
                isGuitarFamily = false
            },
            timing = new PsarcCachedArrangementTimingData
            {
                averageTempoBpm = 120f,
                sections = new List<PsarcCachedSectionData>
                {
                    new PsarcCachedSectionData { name = "intro", number = 1, timeSeconds = 0f }
                },
                ebeats = BuildFixtureEbeats()
            },
            notes = new List<PsarcCachedNoteData>
            {
                new PsarcCachedNoteData { id = 601, time = 0.25f, duration = 0.10f, stringIdx = 0, fret = 42, note = "Hi-Hat", requiresPluck = true },
                new PsarcCachedNoteData { id = 602, time = 0.50f, duration = 0.10f, stringIdx = 1, fret = 49, note = "Crash Cymbal", requiresPluck = true },
                new PsarcCachedNoteData { id = 603, time = 0.75f, duration = 0.10f, stringIdx = 2, fret = 38, note = "Snare", requiresPluck = true },
                new PsarcCachedNoteData { id = 604, time = 1.00f, duration = 0.10f, stringIdx = 4, fret = 36, note = "Kick", requiresPluck = true }
            }
        };
    }

    private static PsarcCachedArrangementPart CreateFullDifficultyCachedPart(int[] tuningPitches)
    {
        return new PsarcCachedArrangementPart
        {
            schemaVersion = PsarcCachedSongFormat.SchemaVersion,
            partId = "lead_full",
            displayName = "Lead Guitar",
            route = "Lead",
            arrangementGroupId = "lead",
            arrangementDisplayName = "Lead Guitar",
            difficultyLabel = "Full",
            difficultyUiIndex = 0,
            hasDifficultyVariants = true,
            durationSeconds = 8f,
            difficultyRating = 4,
            tuningPitches = (int[])tuningPitches.Clone(),
            tuningDisplayName = "E Standard",
            generatedPart = new PsarcCachedGeneratedPartInfo
            {
                partId = "lead_full",
                displayName = "Lead Guitar",
                instrumentName = "Guitar",
                sourceMidiChannel = 0,
                sourceMidiProgram = 29,
                preferredBank = -1,
                isGuitarFamily = true
            },
            timing = new PsarcCachedArrangementTimingData
            {
                averageTempoBpm = 120f,
                sections = new List<PsarcCachedSectionData>
                {
                    new PsarcCachedSectionData { name = "intro", number = 1, timeSeconds = 0f },
                    new PsarcCachedSectionData { name = "solo", number = 1, timeSeconds = 4f }
                },
                ebeats = BuildFixtureEbeats()
            },
            notes = new List<PsarcCachedNoteData>
            {
                new PsarcCachedNoteData
                {
                    id = 501,
                    time = 1.2f,
                    duration = 0.9f,
                    stringIdx = 4,
                    fret = 8,
                    note = "Bb",
                    technique = (int)NoteTechnique.Bend,
                    bendStep = 2f,
                    maxBend = 2f,
                    bendPreBend = true,
                    requiresPluck = true
                },
                new PsarcCachedNoteData
                {
                    id = 502,
                    time = 2.5f,
                    duration = 1.1f,
                    stringIdx = 4,
                    fret = 12,
                    note = "E",
                    technique = (int)NoteTechnique.Bend,
                    bendStep = 2f,
                    maxBend = 2f,
                    bendRelease = true,
                    requiresPluck = true,
                    bendPoints = new List<PsarcCachedBendPointData>
                    {
                        new PsarcCachedBendPointData { timeSeconds = 0f, step = 0f },
                        new PsarcCachedBendPointData { timeSeconds = 0.3f, step = 2f },
                        new PsarcCachedBendPointData { timeSeconds = 0.65f, step = 0f }
                    }
                },
                new PsarcCachedNoteData
                {
                    id = 503,
                    time = 4.0f,
                    duration = 0.75f,
                    stringIdx = 2,
                    fret = 7,
                    note = "D",
                    technique = (int)NoteTechnique.Slide,
                    slideTargetFret = 10,
                    isHarmonic = true,
                    isAccent = true,
                    isTap = true,
                    isTremolo = true,
                    requiresPluck = true,
                    techniqueSegments = new List<PsarcCachedTechniqueSegmentData>
                    {
                        new PsarcCachedTechniqueSegmentData
                        {
                            type = (int)NoteTechniqueSegmentType.Slide,
                            startOffset = 0f,
                            endOffset = 0.75f,
                            startFret = 7,
                            endFret = 10
                        }
                    }
                },
                new PsarcCachedNoteData
                {
                    id = 504,
                    time = 5.0f,
                    duration = 0.2f,
                    stringIdx = 1,
                    fret = 5,
                    note = "D",
                    technique = (int)NoteTechnique.HammerOn,
                    isHammerOn = true,
                    isHopo = true,
                    isLegato = true,
                    requiresPluck = false
                },
                new PsarcCachedNoteData
                {
                    id = 505,
                    time = 5.5f,
                    duration = 0.2f,
                    stringIdx = 1,
                    fret = 3,
                    note = "C",
                    technique = (int)NoteTechnique.PullOff,
                    isPullOff = true,
                    isHopo = true,
                    isLegato = true,
                    requiresPluck = false,
                    isMuted = true,
                    isFretHandMute = true
                },
                new PsarcCachedNoteData
                {
                    id = 506,
                    time = 6.0f,
                    duration = 0.6f,
                    stringIdx = 0,
                    fret = 15,
                    note = "G",
                    technique = (int)NoteTechnique.None,
                    hasVibrato = true,
                    vibratoStrength = 2,
                    isPinchHarmonic = true,
                    requiresPluck = true
                },
                new PsarcCachedNoteData
                {
                    id = 507,
                    time = 6.8f,
                    duration = 0.3f,
                    stringIdx = 3,
                    fret = 9,
                    note = "E",
                    technique = (int)NoteTechnique.None,
                    isMuted = true,
                    isPalmMute = true,
                    requiresPluck = true
                }
            },
            arpeggioGuides = new List<PsarcCachedArpeggioGuideData>
            {
                new PsarcCachedArpeggioGuideData
                {
                    id = 701,
                    startTime = 1.0f,
                    endTime = 2.2f,
                    chordName = "G",
                    stringFrets = new[] { 3, 2, 0, 0, 0, 3 }
                }
            }
        };
    }

    private static PsarcCachedArrangementPart CreateEasyDifficultyCachedPart(int[] tuningPitches)
    {
        PsarcCachedArrangementPart part = CreateFullDifficultyCachedPart(tuningPitches);
        part.partId = "lead_easy";
        part.difficultyLabel = "1";
        part.difficultyUiIndex = 1;
        part.difficultyRating = 1;
        part.notes = new List<PsarcCachedNoteData>
        {
            new PsarcCachedNoteData
            {
                id = 401,
                time = 1.2f,
                duration = 0.1f,
                stringIdx = 4,
                fret = 8,
                note = "Bb",
                requiresPluck = true
            }
        };
        part.arpeggioGuides = new List<PsarcCachedArpeggioGuideData>();
        return part;
    }

    private static List<PsarcCachedEbeatData> BuildFixtureEbeats()
    {
        List<PsarcCachedEbeatData> ebeats = new List<PsarcCachedEbeatData>();
        for (int i = 0; i <= 16; i++)
        {
            ebeats.Add(new PsarcCachedEbeatData
            {
                timeSeconds = i * 0.5f,
                measure = (short)(i % 4 == 0 ? i / 4 : -1)
            });
        }

        return ebeats;
    }

    private static NoteTechniqueSegmentData FindRuntimeSegment(
        List<NoteTechniqueSegmentData> segments,
        NoteTechniqueSegmentType type,
        float startOffset,
        float endOffset)
    {
        NoteTechniqueSegmentData? segment = segments?.FirstOrDefault(candidate =>
            candidate.type == type &&
            Approximately(candidate.startOffset, startOffset) &&
            Approximately(candidate.endOffset, endOffset));
        Assert.IsTrue(segment.HasValue, $"Missing runtime {type} segment {startOffset:0.###}-{endOffset:0.###}.");
        return segment.Value;
    }

    private static void AssertRuntimeSegment(
        NoteTechniqueSegmentData segment,
        int startFret,
        int endFret,
        float startBend,
        float endBend)
    {
        Assert.AreEqual(startFret, segment.startFret);
        Assert.AreEqual(endFret, segment.endFret);
        Assert.AreEqual(startBend, segment.startBend, 0.001f);
        Assert.AreEqual(endBend, segment.endBend, 0.001f);
    }

    private static bool Approximately(double actual, double expected)
    {
        return Math.Abs(actual - expected) <= 0.001;
    }

    private static List<NoteTechniqueSegmentData> GetEditorSegments(ChartEditorNote note)
    {
        return note?.techniqueSegments?
            .Where(segment => segment != null)
            .Select(segment => new NoteTechniqueSegmentData(
                segment.type,
                segment.startOffset,
                segment.endOffset,
                segment.startFret,
                segment.endFret,
                segment.startBend,
                segment.endBend))
            .ToList() ?? new List<NoteTechniqueSegmentData>();
    }

    private static List<string> BuildPartSummaryDigest(IEnumerable<MusicXmlLoader.MusicXmlPartSummary> summaries)
    {
        return summaries?
            .Where(summary => summary != null)
            .OrderBy(summary => summary.Index)
            .Select(summary => string.Join("|",
                summary.Index,
                summary.PartId ?? string.Empty,
                summary.Name ?? string.Empty,
                summary.InstrumentType ?? string.Empty,
                summary.Route ?? string.Empty,
                summary.GroupId ?? string.Empty,
                summary.GroupDisplayName ?? string.Empty,
                summary.DifficultyLabel ?? string.Empty,
                summary.DifficultyUiIndex,
                summary.HasDifficultyVariants,
                summary.NoteCount,
                summary.TabCount,
                summary.Score,
                IntArrayDigest(summary.StringTuningPitches),
                summary.TuningDisplayName ?? string.Empty))
            .ToList() ?? new List<string>();
    }

    private static List<string> BuildRuntimeNoteDigest(IEnumerable<NoteData> notes)
    {
        return notes?
            .OrderBy(note => note.time)
            .ThenBy(note => note.stringIdx)
            .ThenBy(note => note.fret)
            .ThenBy(note => note.id)
            .Select(note => string.Join("|",
                note.id,
                FormatSeconds(note.time),
                FormatSeconds(note.duration),
                note.stringIdx,
                note.fret,
                note.note ?? string.Empty,
                note.chordId,
                note.chordName ?? string.Empty,
                (int)note.technique,
                note.slideTargetFret,
                FormatSeconds(note.bendStep),
                FormatSeconds(note.bendVisualStartTime),
                FormatSeconds(note.bendVisualDuration),
                note.bendPreBend,
                note.bendRelease,
                note.isMuted,
                note.isLegato,
                note.requiresPluck,
                note.linkedFromNoteId,
                SegmentDigest(note.techniqueSegments)))
            .ToList() ?? new List<string>();
    }

    private static List<string> BuildArpeggioGuideDigest(IEnumerable<ArpeggioGuideData> guides)
    {
        return guides?
            .Where(guide => guide != null)
            .OrderBy(guide => guide.startTime)
            .ThenBy(guide => guide.id)
            .Select(guide => string.Join("|",
                guide.id,
                FormatSeconds(guide.startTime),
                FormatSeconds(guide.endTime),
                guide.chordName ?? string.Empty,
                IntArrayDigest(guide.stringFrets)))
            .ToList() ?? new List<string>();
    }

    private static List<string> BuildGeneratedArrangementDigest(GeneratedPlaybackArrangement arrangement)
    {
        List<string> digest = new List<string>();
        if (arrangement == null)
            return digest;

        digest.Add($"duration|{FormatSeconds(arrangement.durationSeconds)}");
        digest.AddRange((arrangement.parts ?? new List<GeneratedPlaybackPartInfo>())
            .OrderBy(part => part.partId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(part => string.Join("|",
                "part",
                part.partId ?? string.Empty,
                part.displayName ?? string.Empty,
                part.instrumentName ?? string.Empty,
                part.sourceMidiChannel,
                part.sourceMidiProgram,
                part.preferredBank,
                part.isDrum,
                part.isGuitarFamily,
                part.isExplicitHarmonicPart)));
        digest.AddRange((arrangement.channelAssignments ?? new List<GeneratedPlaybackChannelAssignment>())
            .OrderBy(channel => channel.channel)
            .ThenBy(channel => channel.sourcePartId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(channel => string.Join("|",
                "channel",
                channel.channel,
                channel.bank,
                channel.preset,
                channel.isDrum,
                channel.label ?? string.Empty,
                channel.sourcePartId ?? string.Empty,
                channel.sourcePartName ?? string.Empty,
                channel.pitchBendRangeSemitones)));
        digest.AddRange((arrangement.notes ?? new List<GeneratedPlaybackNoteEvent>())
            .OrderBy(note => note.startTimeSeconds)
            .ThenBy(note => note.channel)
            .ThenBy(note => note.midiNote)
            .Select(note => string.Join("|",
                "note",
                FormatSeconds(note.startTimeSeconds),
                FormatSeconds(note.durationSeconds),
                FormatSeconds(note.pitchPreRollSeconds),
                note.midiNote,
                note.velocity,
                note.channel,
                note.partId ?? string.Empty,
                note.partName ?? string.Empty,
                (int)note.techniqueVariant,
                (int)note.legatoTransitionKind,
                FormatSeconds(note.attackVelocityScale),
                FormatSeconds(note.vibratoDepthSemitones),
                FormatSeconds(note.vibratoRateHz),
                FormatSeconds(note.vibratoDelayNormalized),
                FormatSeconds(note.vibratoFadeNormalized),
                note.pitchBendRangeSemitones,
                PitchCurveDigest(note.pitchCurve))));
        return digest;
    }

    private static List<string> BuildEditorToneDigest(ChartEditorToneData toneData)
    {
        List<string> digest = new List<string> { $"base|{toneData?.baseToneName ?? string.Empty}" };
        digest.AddRange((toneData?.changes ?? new List<ChartEditorToneChange>())
            .Where(change => change != null)
            .OrderBy(change => change.timeSeconds)
            .ThenBy(change => change.toneName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(change => string.Join("|",
                "change",
                FormatSeconds(change.timeSeconds),
                change.toneName ?? string.Empty,
                change.toneId)));
        digest.AddRange((toneData?.definitions ?? new List<ChartEditorToneDefinition>())
            .Where(definition => definition != null)
            .OrderBy(definition => definition.name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(definition => definition.key ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(definition => string.Join("|",
                "definition",
                definition.name ?? string.Empty,
                definition.key ?? string.Empty,
                EditorTonePresetDigest(definition.preset),
                EditorToneFallbackDigest(definition.fallback))));
        return digest;
    }

    private static List<string> BuildTheoryToneDigest(TheoryToneData toneData)
    {
        List<string> digest = new List<string> { $"base|{toneData?.baseToneName ?? string.Empty}" };
        digest.AddRange((toneData?.changes ?? new List<TheoryToneChangeData>())
            .Where(change => change != null)
            .OrderBy(change => change.timeSeconds)
            .ThenBy(change => change.toneName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(change => string.Join("|",
                "change",
                FormatSeconds(change.timeSeconds),
                change.toneName ?? string.Empty,
                change.toneId)));
        digest.AddRange((toneData?.definitions ?? new List<TheoryToneDefinitionData>())
            .Where(definition => definition != null)
            .OrderBy(definition => definition.name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(definition => definition.key ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(definition => string.Join("|",
                "definition",
                definition.name ?? string.Empty,
                definition.key ?? string.Empty,
                TheoryTonePresetDigest(definition.preset),
                TheoryToneFallbackDigest(definition.fallback))));
        return digest;
    }

    private static string EditorTonePresetDigest(ChartEditorTonePresetData preset)
    {
        return string.Join("|",
            preset?.presetId ?? string.Empty,
            preset?.presetName ?? string.Empty,
            FormatSeconds(preset?.inputGainDb ?? 0f),
            FormatSeconds(preset?.outputGainDb ?? 0f),
            string.Join(",", (preset?.pedalChain ?? new List<ChartEditorTonePedalSlotData>())
                .Where(slot => slot != null)
                .OrderBy(slot => slot.instanceId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(slot => string.Join(":",
                    slot.instanceId ?? string.Empty,
                    slot.pedalType ?? string.Empty,
                    slot.descriptorId ?? string.Empty,
                    slot.enabled,
                    slot.settingsJson ?? string.Empty))));
    }

    private static string TheoryTonePresetDigest(TheoryTonePresetData preset)
    {
        return string.Join("|",
            preset?.presetId ?? string.Empty,
            preset?.presetName ?? string.Empty,
            FormatSeconds(preset?.inputGainDb ?? 0f),
            FormatSeconds(preset?.outputGainDb ?? 0f),
            string.Join(",", (preset?.pedalChain ?? new List<TheoryTonePedalSlotData>())
                .Where(slot => slot != null)
                .OrderBy(slot => slot.instanceId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(slot => string.Join(":",
                    slot.instanceId ?? string.Empty,
                    slot.pedalType ?? string.Empty,
                    slot.descriptorId ?? string.Empty,
                    slot.enabled,
                    slot.settingsJson ?? string.Empty))));
    }

    private static string EditorToneFallbackDigest(ChartEditorToneFallbackData fallback)
    {
        return string.Join("|",
            fallback?.preferredPresetName ?? string.Empty,
            fallback?.searchText ?? string.Empty);
    }

    private static string TheoryToneFallbackDigest(TheoryToneFallbackData fallback)
    {
        return string.Join("|",
            fallback?.preferredPresetName ?? string.Empty,
            fallback?.searchText ?? string.Empty);
    }

    private static string UnityTonePresetDigest(UnityToneLabRuntime.ToneLabPreset preset)
    {
        return string.Join("|",
            preset?.preset_id ?? string.Empty,
            preset?.preset_name ?? string.Empty,
            FormatSeconds(preset?.input_gain_db ?? 0f),
            FormatSeconds(preset?.output_gain_db ?? 0f),
            string.Join(",", (preset?.pedal_chain ?? new List<UnityToneLabRuntime.ToneLabPedalSlot>())
                .Where(slot => slot != null)
                .OrderBy(slot => slot.pedal_instance_id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(slot => string.Join(":",
                    slot.pedal_instance_id ?? string.Empty,
                    slot.pedal_type,
                    slot.descriptor_id ?? string.Empty,
                    slot.enabled,
                    slot.settings_json ?? string.Empty))));
    }

    private static UnityToneLabRuntime.ToneLabPreset ParseToneLabPresetForDigest(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonUtility.FromJson<UnityToneLabRuntime.ToneLabPreset>(json);
    }

    private static List<string> BuildEditorNoteDigest(ChartEditorTrack track)
    {
        return track?.notes?
            .Where(note => note != null)
            .OrderBy(note => note.timeSeconds)
            .ThenBy(note => note.stringOrLane)
            .ThenBy(note => note.fret)
            .ThenBy(note => note.sourceNoteId)
            .Select(note => string.Join("|",
                note.sourceNoteId,
                FormatSeconds(note.timeSeconds),
                FormatSeconds(note.durationSeconds),
                note.stringOrLane,
                note.fret,
                note.noteName ?? string.Empty,
                note.chordId,
                note.chordName ?? string.Empty,
                (int)note.technique,
                note.slideTargetFret,
                FormatSeconds(note.bendStep),
                FormatSeconds(note.bendVisualStartTime),
                FormatSeconds(note.bendVisualDuration),
                note.bendPreBend,
                note.bendRelease,
                note.muted,
                note.palmMute,
                note.fretHandMute,
                note.harmonic,
                note.accent,
                note.tap,
                note.tremolo,
                note.pinchHarmonic,
                note.vibratoStrength,
                note.legato || note.technique == NoteTechnique.HammerOn || note.technique == NoteTechnique.PullOff,
                (note.technique == NoteTechnique.HammerOn || note.technique == NoteTechnique.PullOff) ? false : note.requiresPluck,
                note.linkedFromNoteId,
                BendPointDigest(note.bendPoints),
                SegmentDigest(GetEditorSegments(note))))
            .ToList() ?? new List<string>();
    }

    private static string BendPointDigest(IEnumerable<ChartEditorBendPoint> points)
    {
        return string.Join(",", points?
            .Where(point => point != null)
            .OrderBy(point => point.timeSeconds)
            .Select(point => $"{FormatSeconds(point.timeSeconds)}:{FormatSeconds(point.step)}") ?? Enumerable.Empty<string>());
    }

    private static string IntArrayDigest(IEnumerable<int> values)
    {
        return string.Join(",", values ?? Enumerable.Empty<int>());
    }

    private static string PitchCurveDigest(IEnumerable<GeneratedPlaybackPitchPoint> points)
    {
        return string.Join(",", points?
            .Where(point => point != null)
            .OrderBy(point => point.normalizedTime)
            .Select(point => $"{FormatSeconds(point.normalizedTime)}:{FormatSeconds(point.semitoneOffset)}") ?? Enumerable.Empty<string>());
    }

    private static string SegmentDigest(IEnumerable<NoteTechniqueSegmentData> segments)
    {
        return string.Join(",", segments?
            .OrderBy(segment => segment.startOffset)
            .ThenBy(segment => segment.endOffset)
            .ThenBy(segment => segment.type)
            .Select(segment => string.Join(":",
                (int)segment.type,
                FormatSeconds(segment.startOffset),
                FormatSeconds(segment.endOffset),
                segment.startFret,
                segment.endFret,
                FormatSeconds(segment.startBend),
                FormatSeconds(segment.endBend))) ?? Enumerable.Empty<string>());
    }

    private static string FormatSeconds(double value)
    {
        return Math.Round(value, 4).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class ExternalContentRootScope : IDisposable
    {
        private readonly string originalStreamingRoot;
        private readonly string originalPersistentRoot;
        private readonly bool originalSettingsLoaded;
        private readonly string originalSongsDirectoryOverride;
        private readonly string originalToneLabEffectsDirectoryOverride;

        public ExternalContentRootScope()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "StringTheoryChartEditorTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);

            originalStreamingRoot = GetStaticField<string>("cachedStreamingRoot");
            originalPersistentRoot = GetStaticField<string>("cachedPersistentRoot");
            originalSettingsLoaded = GetStaticField<bool>("externalContentSettingsLoaded");
            originalSongsDirectoryOverride = GetStaticField<string>("songsDirectoryOverride");
            originalToneLabEffectsDirectoryOverride = GetStaticField<string>("toneLabEffectsDirectoryOverride");

            SetStaticField("cachedStreamingRoot", Application.streamingAssetsPath);
            SetStaticField("cachedPersistentRoot", RootPath);
            SetStaticField("externalContentSettingsLoaded", true);
            SetStaticField("songsDirectoryOverride", string.Empty);
            SetStaticField("toneLabEffectsDirectoryOverride", string.Empty);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            SetStaticField("cachedStreamingRoot", originalStreamingRoot);
            SetStaticField("cachedPersistentRoot", originalPersistentRoot);
            SetStaticField("externalContentSettingsLoaded", originalSettingsLoaded);
            SetStaticField("songsDirectoryOverride", originalSongsDirectoryOverride);
            SetStaticField("toneLabEffectsDirectoryOverride", originalToneLabEffectsDirectoryOverride);

            try
            {
                if (Directory.Exists(RootPath))
                    Directory.Delete(RootPath, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; a failed delete should not hide the test result.
            }
        }
    }

    private static T GetStaticField<T>(string name)
    {
        FieldInfo field = typeof(ExternalContentPaths).GetField(name, StaticPrivate);
        Assert.IsNotNull(field, $"Missing ExternalContentPaths.{name}.");
        object value = field.GetValue(null);
        return value is T typed ? typed : default;
    }

    private static void SetStaticField<T>(string name, T value)
    {
        FieldInfo field = typeof(ExternalContentPaths).GetField(name, StaticPrivate);
        Assert.IsNotNull(field, $"Missing ExternalContentPaths.{name}.");
        field.SetValue(null, value);
    }
}
