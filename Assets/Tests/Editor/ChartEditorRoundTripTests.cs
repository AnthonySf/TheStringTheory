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
    public void ExportWithoutEdits_ArrangementJsonIsStableAfterReload()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.SaveProject(project, out string projectPath, out string saveError), saveError);
        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out string firstExportDirectory, out string firstExportError), firstExportError);
        string firstSnapshot = ReadExportedArrangementSnapshot(firstExportDirectory);

        Assert.IsTrue(ChartEditorProjectStore.LoadProject(projectPath, out ChartEditorProject loaded, out string loadError), loadError);
        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(loaded, out string secondExportDirectory, out string secondExportError), secondExportError);
        string secondSnapshot = ReadExportedArrangementSnapshot(secondExportDirectory);

        Assert.AreEqual(firstExportDirectory, secondExportDirectory);
        Assert.AreEqual(firstSnapshot, secondSnapshot, "Exporting a loaded chart without edits must produce the same playable arrangement chart data.");
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
    public void ExportWithoutEdits_TheoryRuntimeMatchesCompatibilityExport()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        AttachFixtureCover(project, scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out string exportDirectory, out string packagePath, out string exportError), exportError);
        string manifestPath = Path.Combine(exportDirectory, RocksmithCachedSongFormat.ManifestFileName);
        Assert.IsTrue(File.Exists(manifestPath), "Export did not create the compatibility song manifest.");
        Assert.IsTrue(File.Exists(packagePath), "Export did not create the .theory package.");

        List<MusicXmlLoader.MusicXmlPartSummary> oldSummaries = SongNotationFacade.GetPartSummaries(manifestPath, SongNotationSourceKind.ArrangementCache);
        List<MusicXmlLoader.MusicXmlPartSummary> theorySummaries = SongNotationFacade.GetPartSummaries(packagePath, SongNotationSourceKind.TheoryPackage);
        CollectionAssert.AreEqual(BuildPartSummaryDigest(oldSummaries), BuildPartSummaryDigest(theorySummaries));

        for (int i = 0; i < oldSummaries.Count; i++)
        {
            CollectionAssert.AreEqual(
                BuildRuntimeNoteDigest(SongNotationFacade.LoadSong(manifestPath, SongNotationSourceKind.ArrangementCache, i)),
                BuildRuntimeNoteDigest(SongNotationFacade.LoadSong(packagePath, SongNotationSourceKind.TheoryPackage, i)),
                $"Runtime notes diverged for arrangement index {i}; note detection and 2D/3D visuals consume these values.");

            CollectionAssert.AreEqual(
                BuildArpeggioGuideDigest(SongNotationFacade.LoadArpeggioGuides(manifestPath, SongNotationSourceKind.ArrangementCache, i)),
                BuildArpeggioGuideDigest(SongNotationFacade.LoadArpeggioGuides(packagePath, SongNotationSourceKind.TheoryPackage, i)),
                $"Arpeggio guides diverged for arrangement index {i}.");
        }

        CollectionAssert.AreEqual(
            BuildGeneratedArrangementDigest(SongNotationFacade.LoadGeneratedArrangement(manifestPath, SongNotationSourceKind.ArrangementCache)),
            BuildGeneratedArrangementDigest(SongNotationFacade.LoadGeneratedArrangement(packagePath, SongNotationSourceKind.TheoryPackage)),
            "Generated playback arrangement metadata must match the compatibility export.");
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

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out string exportDirectory, out string packagePath, out string exportError), exportError);
        string manifestPath = Path.Combine(exportDirectory, RocksmithCachedSongFormat.ManifestFileName);
        Assert.IsTrue(File.Exists(manifestPath), "Export did not create the compatibility song manifest.");

        List<MusicXmlLoader.MusicXmlPartSummary> summaries = SongNotationFacade.GetPartSummaries(packagePath, SongNotationSourceKind.TheoryPackage);
        List<MusicXmlLoader.MusicXmlPartSummary> compatibilitySummaries = SongNotationFacade.GetPartSummaries(manifestPath, SongNotationSourceKind.ArrangementCache);
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
            MusicXmlLoader.MusicXmlPartSummary summary = summaries.First(candidate => candidate.PartId == sourceTrack.id);
            MusicXmlLoader.MusicXmlPartSummary compatibilitySummary = compatibilitySummaries.First(candidate => candidate.PartId == sourceTrack.id);
            CollectionAssert.AreEqual(
                BuildRuntimeNoteDigest(SongNotationFacade.LoadSong(manifestPath, SongNotationSourceKind.ArrangementCache, compatibilitySummary.Index)),
                BuildRuntimeNoteDigest(SongNotationFacade.LoadSong(packagePath, SongNotationSourceKind.TheoryPackage, summary.Index)),
                $".theory runtime notes diverged for explicitly tagged track '{sourceTrack.id}'.");
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
    public void ExportWithoutEdits_TheoryGeneratedDrumRoutesMatchCompatibilityExport()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        project.tracks.Add(CreateDrumTrack());
        AttachFixtureAudio(project, scope.RootPath);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out string exportDirectory, out string packagePath, out string exportError), exportError);
        string manifestPath = Path.Combine(exportDirectory, RocksmithCachedSongFormat.ManifestFileName);
        Assert.IsTrue(File.Exists(manifestPath), "Export did not create the compatibility song manifest.");
        Assert.IsTrue(File.Exists(packagePath), "Export did not create the .theory package.");

        GeneratedPlaybackArrangement compatibility = SongNotationFacade.LoadGeneratedArrangement(manifestPath, SongNotationSourceKind.ArrangementCache);
        GeneratedPlaybackArrangement theory = SongNotationFacade.LoadGeneratedArrangement(packagePath, SongNotationSourceKind.TheoryPackage);
        Assert.IsTrue(compatibility.channelAssignments.Any(channel => channel.isDrum), "Compatibility generated playback should preserve drum channel routes.");
        CollectionAssert.AreEqual(
            BuildGeneratedArrangementDigest(compatibility),
            BuildGeneratedArrangementDigest(theory),
            "Generated drum route metadata must match between compatibility and .theory exports.");
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
    public void ExportWithoutEdits_PreservesGeneratedPlaybackEventsInTheoryAndCompatibilityExport()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        AttachGeneratedPlaybackEvent(project.tracks[0]);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out string exportDirectory, out string packagePath, out string exportError), exportError);
        string manifestPath = Path.Combine(exportDirectory, RocksmithCachedSongFormat.ManifestFileName);
        Assert.IsTrue(File.Exists(manifestPath), "Export did not create the compatibility song manifest.");
        Assert.IsTrue(File.Exists(packagePath), "Export did not create the .theory package.");

        GeneratedPlaybackArrangement compatibility = SongNotationFacade.LoadGeneratedArrangement(manifestPath, SongNotationSourceKind.ArrangementCache);
        GeneratedPlaybackArrangement theory = SongNotationFacade.LoadGeneratedArrangement(packagePath, SongNotationSourceKind.TheoryPackage);
        Assert.AreEqual(1, compatibility.notes.Count, "Compatibility export should preserve the editor generated playback event.");
        Assert.AreEqual(1, theory.notes.Count, ".theory export should preserve the editor generated playback event.");
        CollectionAssert.AreEqual(
            BuildGeneratedArrangementDigest(compatibility),
            BuildGeneratedArrangementDigest(theory),
            "Generated playback events must match between compatibility and .theory exports.");
    }

    [Test]
    public void ExportWithoutEdits_PreservesToneChangesInTheoryAndCompatibilityExport()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        AttachToneData(project.tracks[0]);
        PrepareProjectForNoEditRoundTrip(project);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out string exportDirectory, out string packagePath, out string exportError), exportError);

        Dictionary<string, RocksmithCachedArrangementPart> compatibilityParts = LoadExportedPartsByRoute(exportDirectory);
        Assert.IsTrue(compatibilityParts.TryGetValue("lead", out RocksmithCachedArrangementPart compatibilityLead), "Compatibility export did not contain the lead arrangement.");
        Assert.IsTrue(TheorySongLoader.TryLoadArrangementByPartId(packagePath, "lead", out TheoryArrangementSummary theoryLeadSummary, out TheoryArrangementData theoryLead), "The .theory package did not contain the lead arrangement.");

        CollectionAssert.AreEqual(
            BuildEditorToneDigest(project.tracks[0].tones),
            BuildCachedToneDigest(compatibilityLead.tones),
            "Compatibility export should preserve chart editor tone changes and tone definitions.");
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
    public void ExportWithEditedNotes_InvalidatesPreservedGeneratedPlaybackEvents()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        ChartEditorProject project = CreateRoundTripProject(scope.RootPath);
        AttachFixtureAudio(project, scope.RootPath);
        AttachGeneratedPlaybackEvent(project.tracks[0]);
        PrepareProjectForNoEditRoundTrip(project);

        project.tracks[0].notes[0].timeSeconds += 0.125;
        project.dirty = true;

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out string exportDirectory, out string packagePath, out string exportError), exportError);
        string manifestPath = Path.Combine(exportDirectory, RocksmithCachedSongFormat.ManifestFileName);

        GeneratedPlaybackArrangement compatibility = SongNotationFacade.LoadGeneratedArrangement(manifestPath, SongNotationSourceKind.ArrangementCache);
        GeneratedPlaybackArrangement theory = SongNotationFacade.LoadGeneratedArrangement(packagePath, SongNotationSourceKind.TheoryPackage);
        Assert.AreEqual(0, compatibility.notes.Count, "Edited notes should not keep stale compatibility generated playback events.");
        Assert.AreEqual(0, theory.notes.Count, "Edited notes should not keep stale .theory generated playback events.");
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
    public void ImportCachedArrangementThenExport_PreservesTranslatedChartValues()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        string manifestPath = WriteCachedArrangementFixture(scope.RootPath);

        Assert.IsTrue(ChartEditorImportService.ImportArrangementManifest(manifestPath, out ChartEditorImportResult result, out string importError), importError);
        Assert.IsNotNull(result?.project, "Import did not return a chart editor project.");

        ChartEditorProject project = result.project;
        Assert.AreEqual(ChartEditorSourceKind.ArrangementCache, project.sourceKind);
        Assert.AreEqual("Import Translation Song", project.metadata.title);
        Assert.AreEqual("Unit Test Band", project.metadata.artist);
        Assert.AreEqual(1, project.tracks.Count, "Only the full difficulty arrangement should be imported for a track group.");
        Assert.AreEqual(2, project.sections.Count, "Cached arrangement sections should be imported into the editor.");
        Assert.IsTrue(project.beatMap.beatMarkers.Any(marker => marker != null && marker.isAnchor && Approximately(marker.audioTimeSeconds, 0.0)));
        Assert.IsTrue(project.beatMap.beatMarkers.Any(marker => marker != null && marker.isAnchor && Approximately(marker.audioTimeSeconds, 2.0)));

        ChartEditorTrack track = project.tracks[0];
        Assert.AreEqual("lead_full", track.id);
        Assert.AreEqual(ChartEditorTrackRole.LeadGuitar, track.role);
        Assert.AreEqual("E Standard", track.tuning.displayName);
        CollectionAssert.AreEqual(new[] { 40, 45, 50, 55, 59, 64 }, track.tuning.stringPitches);
        Assert.AreEqual(7, track.notes.Count);
        Assert.AreEqual(1, track.arpeggioGuides.Count);

        ChartEditorNote prebend = FindEditorNote(track, 501);
        Assert.AreEqual(NoteTechnique.Bend, prebend.technique);
        Assert.IsTrue(prebend.bendPreBend);
        Assert.AreEqual(2f, prebend.bendStep, 0.001f);
        Assert.AreEqual(2f, prebend.maxBend, 0.001f);
        ChartEditorTechniqueSegment prebendSustain = FindEditorSegment(prebend, NoteTechniqueSegmentType.Sustain, 0f, 0.9f);
        AssertSegment(prebendSustain, startFret: 8, endFret: 8, startBend: 2f, endBend: 2f);

        ChartEditorNote bendRelease = FindEditorNote(track, 502);
        Assert.AreEqual(NoteTechnique.Bend, bendRelease.technique);
        Assert.IsTrue(bendRelease.bendRelease);
        Assert.AreEqual(3, bendRelease.bendPoints.Count);
        ChartEditorTechniqueSegment bendUp = FindEditorSegment(bendRelease, NoteTechniqueSegmentType.Bend, 0f, 0.3f);
        AssertSegment(bendUp, startFret: 12, endFret: 12, startBend: 0f, endBend: 2f);
        ChartEditorTechniqueSegment bendDown = FindEditorSegment(bendRelease, NoteTechniqueSegmentType.Bend, 0.3f, 0.65f);
        AssertSegment(bendDown, startFret: 12, endFret: 12, startBend: 2f, endBend: 0f);

        ChartEditorNote slide = FindEditorNote(track, 503);
        Assert.AreEqual(NoteTechnique.Slide, slide.technique);
        Assert.AreEqual(10, slide.slideTargetFret);
        Assert.IsTrue(slide.harmonic);
        Assert.IsTrue(slide.accent);
        Assert.IsTrue(slide.tap);
        Assert.IsTrue(slide.tremolo);
        ChartEditorTechniqueSegment slideSegment = FindEditorSegment(slide, NoteTechniqueSegmentType.Slide, 0f, 0.75f);
        AssertSegment(slideSegment, startFret: 7, endFret: 10, startBend: 0f, endBend: 0f);

        ChartEditorNote hammerOn = FindEditorNote(track, 504);
        Assert.AreEqual(NoteTechnique.HammerOn, hammerOn.technique);
        Assert.IsTrue(hammerOn.legato);
        Assert.IsFalse(hammerOn.requiresPluck);

        ChartEditorNote pullOff = FindEditorNote(track, 505);
        Assert.AreEqual(NoteTechnique.PullOff, pullOff.technique);
        Assert.IsTrue(pullOff.legato);
        Assert.IsFalse(pullOff.requiresPluck);
        Assert.IsTrue(pullOff.muted);
        Assert.IsTrue(pullOff.fretHandMute);

        ChartEditorNote vibrato = FindEditorNote(track, 506);
        Assert.AreEqual(2, vibrato.vibratoStrength);
        Assert.IsTrue(vibrato.pinchHarmonic);
        ChartEditorTechniqueSegment vibratoSegment = FindEditorSegment(vibrato, NoteTechniqueSegmentType.Vibrato, 0f, 0.6f);
        AssertSegment(vibratoSegment, startFret: 15, endFret: 15, startBend: 0f, endBend: 0f);

        ChartEditorNote palmMute = FindEditorNote(track, 507);
        Assert.IsTrue(palmMute.muted);
        Assert.IsTrue(palmMute.palmMute);

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out string exportDirectory, out string exportError), exportError);
        RocksmithCachedArrangementPart exported = ReadSingleExportedArrangementPart(exportDirectory);

        Assert.AreEqual("lead_full", exported.partId);
        Assert.AreEqual("Lead Guitar", exported.displayName);
        Assert.AreEqual("Lead", exported.route);
        Assert.AreEqual("Full", exported.difficultyLabel);
        Assert.AreEqual("E Standard", exported.tuningDisplayName);
        CollectionAssert.AreEqual(new[] { 40, 45, 50, 55, 59, 64 }, exported.tuningPitches);
        Assert.AreEqual(7, exported.notes.Count);
        Assert.AreEqual(1, exported.arpeggioGuides.Count);

        RocksmithCachedNoteData exportedPrebend = FindCachedNote(exported, 501);
        Assert.AreEqual((int)NoteTechnique.Bend, exportedPrebend.technique);
        Assert.IsTrue(exportedPrebend.bendPreBend);
        Assert.AreEqual(2f, exportedPrebend.bendStep, 0.001f);
        RocksmithCachedTechniqueSegmentData exportedPrebendSustain = FindCachedSegment(exportedPrebend, NoteTechniqueSegmentType.Sustain, 0f, 0.9f);
        AssertSegment(exportedPrebendSustain, startFret: 8, endFret: 8, startBend: 2f, endBend: 2f);

        RocksmithCachedNoteData exportedBendRelease = FindCachedNote(exported, 502);
        Assert.AreEqual((int)NoteTechnique.Bend, exportedBendRelease.technique);
        Assert.IsTrue(exportedBendRelease.bendRelease);
        Assert.AreEqual(3, exportedBendRelease.bendPoints.Count);
        Assert.AreEqual(2f, exportedBendRelease.maxBend, 0.001f);
        AssertSegment(FindCachedSegment(exportedBendRelease, NoteTechniqueSegmentType.Bend, 0f, 0.3f), startFret: 12, endFret: 12, startBend: 0f, endBend: 2f);
        AssertSegment(FindCachedSegment(exportedBendRelease, NoteTechniqueSegmentType.Bend, 0.3f, 0.65f), startFret: 12, endFret: 12, startBend: 2f, endBend: 0f);

        RocksmithCachedNoteData exportedSlide = FindCachedNote(exported, 503);
        Assert.AreEqual((int)NoteTechnique.Slide, exportedSlide.technique);
        Assert.AreEqual(10, exportedSlide.slideTargetFret);
        Assert.IsTrue(exportedSlide.isHarmonic);
        Assert.IsTrue(exportedSlide.isAccent);
        Assert.IsTrue(exportedSlide.isTap);
        Assert.IsTrue(exportedSlide.isTremolo);
        AssertSegment(FindCachedSegment(exportedSlide, NoteTechniqueSegmentType.Slide, 0f, 0.75f), startFret: 7, endFret: 10, startBend: 0f, endBend: 0f);

        RocksmithCachedNoteData exportedHammerOn = FindCachedNote(exported, 504);
        Assert.AreEqual((int)NoteTechnique.HammerOn, exportedHammerOn.technique);
        Assert.IsTrue(exportedHammerOn.isHammerOn);
        Assert.IsTrue(exportedHammerOn.isHopo);
        Assert.IsTrue(exportedHammerOn.isLegato);
        Assert.IsFalse(exportedHammerOn.requiresPluck);

        RocksmithCachedNoteData exportedPullOff = FindCachedNote(exported, 505);
        Assert.AreEqual((int)NoteTechnique.PullOff, exportedPullOff.technique);
        Assert.IsTrue(exportedPullOff.isPullOff);
        Assert.IsTrue(exportedPullOff.isHopo);
        Assert.IsTrue(exportedPullOff.isLegato);
        Assert.IsFalse(exportedPullOff.requiresPluck);
        Assert.IsTrue(exportedPullOff.isMuted);
        Assert.IsTrue(exportedPullOff.isFretHandMute);

        RocksmithCachedNoteData exportedVibrato = FindCachedNote(exported, 506);
        Assert.AreEqual((int)NoteTechnique.Vibrato, exportedVibrato.technique);
        Assert.IsTrue(exportedVibrato.hasVibrato);
        Assert.IsTrue(exportedVibrato.isPinchHarmonic);
        Assert.AreEqual(2, exportedVibrato.vibratoStrength);
        AssertSegment(FindCachedSegment(exportedVibrato, NoteTechniqueSegmentType.Vibrato, 0f, 0.6f), startFret: 15, endFret: 15, startBend: 0f, endBend: 0f);

        RocksmithCachedNoteData exportedPalmMute = FindCachedNote(exported, 507);
        Assert.IsTrue(exportedPalmMute.isMuted);
        Assert.IsTrue(exportedPalmMute.isPalmMute);
    }

    [Test]
    public void ImportCachedDrumArrangementThenExport_PreservesLaneOrderAndNormalNoteFlags()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        string manifestPath = WriteCachedDrumArrangementFixture(scope.RootPath);

        Assert.IsTrue(ChartEditorImportService.ImportArrangementManifest(manifestPath, out ChartEditorImportResult result, out string importError), importError);
        ChartEditorProject project = result?.project;
        Assert.IsNotNull(project, "Import did not return a chart editor project.");
        Assert.AreEqual(1, project.tracks.Count);

        ChartEditorTrack track = project.tracks[0];
        Assert.AreEqual(ChartEditorTrackRole.Drums, track.role);
        CollectionAssert.AreEqual(
            new[]
            {
                "0:42:Hi-Hat:False:True",
                "1:49:Crash Cymbal:False:True",
                "2:38:Snare:False:True",
                "4:36:Kick:False:True"
            },
            track.notes
                .OrderBy(note => note.timeSeconds)
                .Select(note => $"{note.stringOrLane}:{note.fret}:{note.noteName}:{note.tap}:{note.requiresPluck}")
                .ToArray());

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out string exportDirectory, out string exportError), exportError);
        RocksmithCachedArrangementPart exported = ReadSingleExportedArrangementPart(exportDirectory);
        Assert.AreEqual("Drums", exported.route);
        Assert.IsTrue(exported.generatedPart.isDrum);
        CollectionAssert.AreEqual(
            new[]
            {
                "0:42:Hi-Hat:False:True",
                "1:49:Crash Cymbal:False:True",
                "2:38:Snare:False:True",
                "4:36:Kick:False:True"
            },
            exported.notes
                .OrderBy(note => note.time)
                .Select(note => $"{note.stringIdx}:{note.fret}:{note.note}:{note.isTap}:{note.requiresPluck}")
                .ToArray());
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
    public void ImportFolder_WithInvalidTheoryPackage_FallsBackToCachedArrangement()
    {
        using ExternalContentRootScope scope = new ExternalContentRootScope();
        string manifestPath = WriteCachedArrangementFixture(scope.RootPath);
        string songDirectory = Path.GetDirectoryName(manifestPath);
        File.WriteAllText(Path.Combine(songDirectory, "aaa_broken.theory"), "not a theory package");

        Assert.IsTrue(ChartEditorImportService.ImportFolder(songDirectory, out ChartEditorImportResult result, out string importError), importError);
        ChartEditorProject project = result?.project;
        Assert.IsNotNull(project, "Folder import should fall back to the valid cached arrangement manifest.");
        Assert.AreEqual(ChartEditorSourceKind.Folder, project.sourceKind);
        Assert.AreEqual(manifestPath, project.sourcePath);
        Assert.AreEqual("Import Translation Song", project.metadata.title);
        Assert.AreEqual(1, project.tracks.Count);
        Assert.AreEqual("lead_full", project.tracks[0].id);
    }

    [Test]
    public void SongLibrary_WithInvalidTheoryPackage_FallsBackToCachedArrangement()
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

        Assert.IsNotNull(entry, "Library scanner should not let a broken .theory file hide a valid cached arrangement.");
        Assert.AreEqual(SongNotationSourceKind.ArrangementCache, entry.PrimaryNotationKind);
        Assert.AreEqual(manifestPath, entry.PrimaryNotationPath);
        Assert.AreEqual("Import Translation Song", entry.DisplayName);
    }

    [Test]
    public void ImportNovemberRainThenExport_PreservesRealSongChartData()
    {
        string manifestPath = FindNovemberRainManifest();
        if (string.IsNullOrWhiteSpace(manifestPath))
            Assert.Ignore("November Rain cached song was not found in the local String Theory songs folder.");

        Dictionary<string, RocksmithCachedArrangementPart> sourcePartsByRoute = LoadFullDifficultyPartsByRoute(manifestPath);
        Assert.AreEqual(3, sourcePartsByRoute.Count, "November Rain should provide full bass, lead, and rhythm arrangements.");
        Assert.IsTrue(sourcePartsByRoute.ContainsKey("lead"));
        Assert.IsTrue(sourcePartsByRoute.ContainsKey("bass"));
        Assert.IsTrue(sourcePartsByRoute.ContainsKey("rhythm"));

        ArrangementTechniqueStats sourceLeadStats = BuildCachedStats(sourcePartsByRoute["lead"], normalizeSegments: true);
        Assert.Greater(sourceLeadStats.bendNotes, 0, "The real-song regression must include lead bends.");
        Assert.Greater(sourceLeadStats.slideNotes, 0, "The real-song regression must include lead slides.");
        Assert.Greater(sourceLeadStats.vibratoNotes, 0, "The real-song regression must include lead vibrato.");
        Assert.Greater(sourceLeadStats.hopoNotes, 0, "The real-song regression must include lead HO/PO notes.");

        using ExternalContentRootScope scope = new ExternalContentRootScope();
        Assert.IsTrue(ChartEditorImportService.ImportArrangementManifest(manifestPath, out ChartEditorImportResult result, out string importError), importError);
        ChartEditorProject project = result?.project;
        Assert.IsNotNull(project, "Import did not return a chart editor project.");
        Assert.AreEqual("November Rain", project.metadata.title);
        Assert.AreEqual("Guns N' Roses", project.metadata.artist);
        Assert.AreEqual(3, project.tracks.Count, "Only full-difficulty bass, lead, and rhythm tracks should be imported.");
        Assert.AreEqual(26, project.sections.Count, "November Rain sections should be imported from the real cached arrangement.");
        Assert.GreaterOrEqual(project.beatMap.beatMarkers.Count(marker => marker != null && marker.isAnchor), 100, "November Rain beat/downbeat anchors should be imported.");

        Dictionary<string, ChartEditorTrack> importedTracksByRoute = project.tracks
            .Where(track => track != null)
            .ToDictionary(track => RouteKeyForTrack(track), track => track, StringComparer.OrdinalIgnoreCase);
        CollectionAssert.AreEquivalent(sourcePartsByRoute.Keys.ToArray(), importedTracksByRoute.Keys.ToArray());

        foreach (KeyValuePair<string, RocksmithCachedArrangementPart> entry in sourcePartsByRoute)
        {
            string route = entry.Key;
            ChartEditorTrack track = importedTracksByRoute[route];
            ArrangementTechniqueStats sourceStats = BuildCachedStats(entry.Value, normalizeSegments: true);
            ArrangementTechniqueStats editorStats = BuildEditorStats(track);
            AssertSourceFlagsMatch(sourceStats, editorStats, $"November Rain import {route}");
            Assert.IsFalse(track.id.IndexOf("level-", StringComparison.OrdinalIgnoreCase) >= 0, $"Imported {route} track should be the full difficulty.");
        }

        Assert.IsTrue(ChartEditorProjectStore.ExportPlayableProject(project, out string exportDirectory, out string exportError), exportError);
        Dictionary<string, RocksmithCachedArrangementPart> exportedPartsByRoute = LoadExportedPartsByRoute(exportDirectory);
        CollectionAssert.AreEquivalent(sourcePartsByRoute.Keys.ToArray(), exportedPartsByRoute.Keys.ToArray());

        foreach (KeyValuePair<string, ChartEditorTrack> entry in importedTracksByRoute)
        {
            string route = entry.Key;
            ChartEditorTrack track = entry.Value;
            RocksmithCachedArrangementPart exportedPart = exportedPartsByRoute[route];
            ArrangementTechniqueStats editorStats = BuildEditorStats(track);
            ArrangementTechniqueStats exportedStats = BuildCachedStats(exportedPart, normalizeSegments: false);
            AssertStatsMatch(editorStats, exportedStats, $"November Rain export {route}");
            CollectionAssert.AreEqual(
                BuildEditorNoteDigest(track),
                BuildCachedNoteDigest(exportedPart),
                $"November Rain exported {route} note data should match the chart editor translated values.");
        }
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

    private static string ReadExportedArrangementSnapshot(string exportDirectory)
    {
        string arrangementsDirectory = Path.Combine(exportDirectory, "arrangements");
        Assert.IsTrue(Directory.Exists(arrangementsDirectory), "Export did not create an arrangements directory.");

        string[] files = Directory.GetFiles(arrangementsDirectory, "*.rs2part.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Greater(files.Length, 0, "Export did not create arrangement chart files.");

        return string.Join("\n---\n", files.Select(path => Path.GetFileName(path) + "\n" + File.ReadAllText(path)));
    }

    private static RocksmithCachedArrangementPart ReadSingleExportedArrangementPart(string exportDirectory)
    {
        string arrangementsDirectory = Path.Combine(exportDirectory, "arrangements");
        Assert.IsTrue(Directory.Exists(arrangementsDirectory), "Export did not create an arrangements directory.");

        string[] files = Directory.GetFiles(arrangementsDirectory, "*.rs2part.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.AreEqual(1, files.Length, "The import fixture should export exactly one full-difficulty arrangement.");

        RocksmithCachedArrangementPart part = JsonUtility.FromJson<RocksmithCachedArrangementPart>(File.ReadAllText(files[0]));
        Assert.IsNotNull(part, "Exported arrangement JSON could not be parsed.");
        return part;
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
        RocksmithCachedArrangementPart fullPart = CreateFullDifficultyCachedPart(eStandard);
        RocksmithCachedArrangementPart easyPart = CreateEasyDifficultyCachedPart(eStandard);

        string fullPartPath = Path.Combine(arrangementsDirectory, "lead_full.rs2part.json");
        string easyPartPath = Path.Combine(arrangementsDirectory, "lead_easy.rs2part.json");
        File.WriteAllText(fullPartPath, JsonUtility.ToJson(fullPart, true));
        File.WriteAllText(easyPartPath, JsonUtility.ToJson(easyPart, true));

        string audioPath = Path.Combine(sourceDirectory, "silence.ogg");
        File.WriteAllBytes(audioPath, Array.Empty<byte>());

        RocksmithCachedSongManifest manifest = new RocksmithCachedSongManifest
        {
            schemaVersion = RocksmithCachedSongFormat.SchemaVersion,
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
            arrangements = new List<RocksmithCachedArrangementSummary>
            {
                new RocksmithCachedArrangementSummary
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
                new RocksmithCachedArrangementSummary
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

        string manifestPath = Path.Combine(sourceDirectory, RocksmithCachedSongFormat.ManifestFileName);
        File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
        return manifestPath;
    }

    private static string WriteCachedDrumArrangementFixture(string rootPath)
    {
        string sourceDirectory = Path.Combine(rootPath, "drum_import_fixture");
        string arrangementsDirectory = Path.Combine(sourceDirectory, "arrangements");
        Directory.CreateDirectory(arrangementsDirectory);

        RocksmithCachedArrangementPart drumPart = CreateCachedDrumArrangementPart();
        string drumPartPath = Path.Combine(arrangementsDirectory, "drums_full.rs2part.json");
        File.WriteAllText(drumPartPath, JsonUtility.ToJson(drumPart, true));

        string audioPath = Path.Combine(sourceDirectory, "silence.ogg");
        File.WriteAllBytes(audioPath, Array.Empty<byte>());

        RocksmithCachedSongManifest manifest = new RocksmithCachedSongManifest
        {
            schemaVersion = RocksmithCachedSongFormat.SchemaVersion,
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
            arrangements = new List<RocksmithCachedArrangementSummary>
            {
                new RocksmithCachedArrangementSummary
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

        string manifestPath = Path.Combine(sourceDirectory, RocksmithCachedSongFormat.ManifestFileName);
        File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
        return manifestPath;
    }

    private static RocksmithCachedArrangementPart CreateCachedDrumArrangementPart()
    {
        return new RocksmithCachedArrangementPart
        {
            schemaVersion = RocksmithCachedSongFormat.SchemaVersion,
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
            generatedPart = new RocksmithCachedGeneratedPartInfo
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
            timing = new RocksmithCachedArrangementTimingData
            {
                averageTempoBpm = 120f,
                sections = new List<RocksmithCachedSectionData>
                {
                    new RocksmithCachedSectionData { name = "intro", number = 1, timeSeconds = 0f }
                },
                ebeats = BuildFixtureEbeats()
            },
            notes = new List<RocksmithCachedNoteData>
            {
                new RocksmithCachedNoteData { id = 601, time = 0.25f, duration = 0.10f, stringIdx = 0, fret = 42, note = "Hi-Hat", requiresPluck = true },
                new RocksmithCachedNoteData { id = 602, time = 0.50f, duration = 0.10f, stringIdx = 1, fret = 49, note = "Crash Cymbal", requiresPluck = true },
                new RocksmithCachedNoteData { id = 603, time = 0.75f, duration = 0.10f, stringIdx = 2, fret = 38, note = "Snare", requiresPluck = true },
                new RocksmithCachedNoteData { id = 604, time = 1.00f, duration = 0.10f, stringIdx = 4, fret = 36, note = "Kick", requiresPluck = true }
            }
        };
    }

    private static RocksmithCachedArrangementPart CreateFullDifficultyCachedPart(int[] tuningPitches)
    {
        return new RocksmithCachedArrangementPart
        {
            schemaVersion = RocksmithCachedSongFormat.SchemaVersion,
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
            generatedPart = new RocksmithCachedGeneratedPartInfo
            {
                partId = "lead_full",
                displayName = "Lead Guitar",
                instrumentName = "Guitar",
                sourceMidiChannel = 0,
                sourceMidiProgram = 29,
                preferredBank = -1,
                isGuitarFamily = true
            },
            timing = new RocksmithCachedArrangementTimingData
            {
                averageTempoBpm = 120f,
                sections = new List<RocksmithCachedSectionData>
                {
                    new RocksmithCachedSectionData { name = "intro", number = 1, timeSeconds = 0f },
                    new RocksmithCachedSectionData { name = "solo", number = 1, timeSeconds = 4f }
                },
                ebeats = BuildFixtureEbeats()
            },
            notes = new List<RocksmithCachedNoteData>
            {
                new RocksmithCachedNoteData
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
                new RocksmithCachedNoteData
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
                    bendPoints = new List<RocksmithCachedBendPointData>
                    {
                        new RocksmithCachedBendPointData { timeSeconds = 0f, step = 0f },
                        new RocksmithCachedBendPointData { timeSeconds = 0.3f, step = 2f },
                        new RocksmithCachedBendPointData { timeSeconds = 0.65f, step = 0f }
                    }
                },
                new RocksmithCachedNoteData
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
                    techniqueSegments = new List<RocksmithCachedTechniqueSegmentData>
                    {
                        new RocksmithCachedTechniqueSegmentData
                        {
                            type = (int)NoteTechniqueSegmentType.Slide,
                            startOffset = 0f,
                            endOffset = 0.75f,
                            startFret = 7,
                            endFret = 10
                        }
                    }
                },
                new RocksmithCachedNoteData
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
                new RocksmithCachedNoteData
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
                new RocksmithCachedNoteData
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
                new RocksmithCachedNoteData
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
            arpeggioGuides = new List<RocksmithCachedArpeggioGuideData>
            {
                new RocksmithCachedArpeggioGuideData
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

    private static RocksmithCachedArrangementPart CreateEasyDifficultyCachedPart(int[] tuningPitches)
    {
        RocksmithCachedArrangementPart part = CreateFullDifficultyCachedPart(tuningPitches);
        part.partId = "lead_easy";
        part.difficultyLabel = "1";
        part.difficultyUiIndex = 1;
        part.difficultyRating = 1;
        part.notes = new List<RocksmithCachedNoteData>
        {
            new RocksmithCachedNoteData
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
        part.arpeggioGuides = new List<RocksmithCachedArpeggioGuideData>();
        return part;
    }

    private static List<RocksmithCachedEbeatData> BuildFixtureEbeats()
    {
        List<RocksmithCachedEbeatData> ebeats = new List<RocksmithCachedEbeatData>();
        for (int i = 0; i <= 16; i++)
        {
            ebeats.Add(new RocksmithCachedEbeatData
            {
                timeSeconds = i * 0.5f,
                measure = (short)(i % 4 == 0 ? i / 4 : -1)
            });
        }

        return ebeats;
    }

    private static ChartEditorNote FindEditorNote(ChartEditorTrack track, int sourceNoteId)
    {
        ChartEditorNote note = track?.notes?.FirstOrDefault(candidate => candidate != null && candidate.sourceNoteId == sourceNoteId);
        Assert.IsNotNull(note, $"Missing imported editor note {sourceNoteId}.");
        return note;
    }

    private static RocksmithCachedNoteData FindCachedNote(RocksmithCachedArrangementPart part, int sourceNoteId)
    {
        RocksmithCachedNoteData note = part?.notes?.FirstOrDefault(candidate => candidate != null && candidate.id == sourceNoteId);
        Assert.IsNotNull(note, $"Missing exported cached note {sourceNoteId}.");
        return note;
    }

    private static ChartEditorTechniqueSegment FindEditorSegment(
        ChartEditorNote note,
        NoteTechniqueSegmentType type,
        float startOffset,
        float endOffset)
    {
        ChartEditorTechniqueSegment segment = note?.techniqueSegments?.FirstOrDefault(candidate =>
            candidate != null &&
            candidate.type == type &&
            Approximately(candidate.startOffset, startOffset) &&
            Approximately(candidate.endOffset, endOffset));
        Assert.IsNotNull(segment, $"Missing editor {type} segment {startOffset:0.###}-{endOffset:0.###} on note {note?.sourceNoteId}.");
        return segment;
    }

    private static RocksmithCachedTechniqueSegmentData FindCachedSegment(
        RocksmithCachedNoteData note,
        NoteTechniqueSegmentType type,
        float startOffset,
        float endOffset)
    {
        RocksmithCachedTechniqueSegmentData segment = note?.techniqueSegments?.FirstOrDefault(candidate =>
            candidate != null &&
            candidate.type == (int)type &&
            Approximately(candidate.startOffset, startOffset) &&
            Approximately(candidate.endOffset, endOffset));
        Assert.IsNotNull(segment, $"Missing exported {type} segment {startOffset:0.###}-{endOffset:0.###} on note {note?.id}.");
        return segment;
    }

    private static void AssertSegment(
        ChartEditorTechniqueSegment segment,
        int startFret,
        int endFret,
        float startBend,
        float endBend)
    {
        Assert.IsNotNull(segment);
        Assert.AreEqual(startFret, segment.startFret);
        Assert.AreEqual(endFret, segment.endFret);
        Assert.AreEqual(startBend, segment.startBend, 0.001f);
        Assert.AreEqual(endBend, segment.endBend, 0.001f);
    }

    private static void AssertSegment(
        RocksmithCachedTechniqueSegmentData segment,
        int startFret,
        int endFret,
        float startBend,
        float endBend)
    {
        Assert.IsNotNull(segment);
        Assert.AreEqual(startFret, segment.startFret);
        Assert.AreEqual(endFret, segment.endFret);
        Assert.AreEqual(startBend, segment.startBend, 0.001f);
        Assert.AreEqual(endBend, segment.endBend, 0.001f);
    }

    private static bool Approximately(double actual, double expected)
    {
        return Math.Abs(actual - expected) <= 0.001;
    }

    private static string FindNovemberRainManifest()
    {
        List<string> roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(Application.persistentDataPath))
        {
            roots.Add(Path.Combine(Application.persistentDataPath, "Songs3"));
            roots.Add(Path.Combine(Application.persistentDataPath, "songs"));
            roots.Add(Path.Combine(Application.persistentDataPath, "Songs"));
        }

        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localApplicationData))
        {
            string localLow = Path.GetFullPath(Path.Combine(localApplicationData, "..", "LocalLow", "StringTheory", "StringTheory"));
            roots.Add(Path.Combine(localLow, "Songs3"));
            roots.Add(Path.Combine(localLow, "songs"));
            roots.Add(Path.Combine(localLow, "Songs"));
        }

        foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                continue;

            foreach (string manifestPath in Directory.EnumerateFiles(root, RocksmithCachedSongFormat.ManifestFileName, SearchOption.AllDirectories))
            {
                if (manifestPath.IndexOf("November", StringComparison.OrdinalIgnoreCase) < 0 &&
                    manifestPath.IndexOf("Guns", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!RocksmithCachedSongLoader.TryLoadManifest(manifestPath, out RocksmithCachedSongManifest manifest) || manifest == null)
                    continue;

                if (string.Equals(manifest.displayName, "November Rain", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(manifest.artist, "Guns N' Roses", StringComparison.OrdinalIgnoreCase))
                {
                    return manifestPath;
                }
            }
        }

        return string.Empty;
    }

    private static Dictionary<string, RocksmithCachedArrangementPart> LoadFullDifficultyPartsByRoute(string manifestPath)
    {
        Assert.IsTrue(RocksmithCachedSongLoader.TryLoadManifest(manifestPath, out RocksmithCachedSongManifest manifest), "Could not load cached song manifest.");
        Assert.IsNotNull(manifest?.arrangements, "Cached song manifest has no arrangements.");

        Dictionary<string, RocksmithCachedArrangementPart> parts = new Dictionary<string, RocksmithCachedArrangementPart>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < manifest.arrangements.Count; i++)
        {
            RocksmithCachedArrangementSummary summary = manifest.arrangements[i];
            if (!IsFullDifficultySummary(summary))
                continue;

            Assert.IsTrue(RocksmithCachedSongLoader.TryLoadArrangementPart(manifestPath, i, out RocksmithCachedArrangementSummary loadedSummary, out RocksmithCachedArrangementPart part),
                $"Could not load full difficulty arrangement '{summary?.partId}'.");
            string route = RouteKey(loadedSummary?.route ?? summary?.route);
            Assert.IsFalse(string.IsNullOrWhiteSpace(route), $"Arrangement '{summary?.partId}' has no route.");
            Assert.IsFalse(parts.ContainsKey(route), $"Duplicate full difficulty route '{route}'.");
            parts[route] = part;
        }

        return parts;
    }

    private static Dictionary<string, RocksmithCachedArrangementPart> LoadExportedPartsByRoute(string exportDirectory)
    {
        string manifestPath = Path.Combine(exportDirectory, RocksmithCachedSongFormat.ManifestFileName);
        Assert.IsTrue(File.Exists(manifestPath), "Export did not create a song manifest.");
        RocksmithCachedSongManifest manifest = JsonUtility.FromJson<RocksmithCachedSongManifest>(File.ReadAllText(manifestPath));
        Assert.IsNotNull(manifest?.arrangements, "Exported song manifest has no arrangements.");

        Dictionary<string, RocksmithCachedArrangementPart> parts = new Dictionary<string, RocksmithCachedArrangementPart>(StringComparer.OrdinalIgnoreCase);
        foreach (RocksmithCachedArrangementSummary summary in manifest.arrangements.Where(summary => summary != null))
        {
            string route = RouteKey(summary.route);
            string partPath = Path.IsPathRooted(summary.partFilePath)
                ? summary.partFilePath
                : Path.Combine(exportDirectory, summary.partFilePath ?? string.Empty);
            Assert.IsTrue(File.Exists(partPath), $"Exported part file does not exist for route '{route}'.");
            RocksmithCachedArrangementPart part = JsonUtility.FromJson<RocksmithCachedArrangementPart>(File.ReadAllText(partPath));
            Assert.IsNotNull(part, $"Exported part file could not be parsed for route '{route}'.");
            Assert.IsFalse(parts.ContainsKey(route), $"Duplicate exported route '{route}'.");
            parts[route] = part;
        }

        return parts;
    }

    private static bool IsFullDifficultySummary(RocksmithCachedArrangementSummary summary)
    {
        if (summary == null)
            return false;

        return summary.difficultyUiIndex == 0 ||
               string.Equals(summary.difficultyLabel?.Trim(), "Full", StringComparison.OrdinalIgnoreCase);
    }

    private static string RouteKeyForTrack(ChartEditorTrack track)
    {
        if (track == null)
            return string.Empty;

        switch (track.role)
        {
            case ChartEditorTrackRole.Bass:
                return "bass";
            case ChartEditorTrackRole.RhythmGuitar:
                return "rhythm";
            case ChartEditorTrackRole.Drums:
                return "drums";
            case ChartEditorTrackRole.Piano:
                return "piano";
            case ChartEditorTrackRole.Vocals:
                return "vocals";
            default:
                return "lead";
        }
    }

    private static string RouteKey(string route)
    {
        string normalized = (route ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("bass"))
            return "bass";
        if (normalized.Contains("rhythm"))
            return "rhythm";
        if (normalized.Contains("drum"))
            return "drums";
        if (normalized.Contains("vocal"))
            return "vocals";
        if (normalized.Contains("lead"))
            return "lead";
        return normalized;
    }

    private static ArrangementTechniqueStats BuildCachedStats(RocksmithCachedArrangementPart part, bool normalizeSegments)
    {
        ArrangementTechniqueStats stats = new ArrangementTechniqueStats
        {
            noteCount = part?.notes?.Count ?? 0,
            arpeggioGuideCount = part?.arpeggioGuides?.Count ?? 0
        };

        if (part?.notes == null)
            return stats;

        foreach (RocksmithCachedNoteData note in part.notes.Where(note => note != null))
        {
            List<NoteTechniqueSegmentData> segments = GetCachedSegments(note, normalizeSegments);
            CountSegments(stats, segments);
            bool hasBendSegment = HasBendSegment(segments);
            bool hasSlideSegment = segments.Any(segment => segment.type == NoteTechniqueSegmentType.Slide);
            bool hasVibratoSegment = segments.Any(segment => segment.type == NoteTechniqueSegmentType.Vibrato);

            if (note.technique == (int)NoteTechnique.Bend || note.bendStep > 0.01f || note.bendPreBend || note.bendRelease || note.maxBend > 0.01f || hasBendSegment)
                stats.bendNotes++;
            if (note.technique == (int)NoteTechnique.Slide || note.slideTargetFret >= 0 || hasSlideSegment)
                stats.slideNotes++;
            if (note.technique == (int)NoteTechnique.Vibrato || note.hasVibrato || hasVibratoSegment)
                stats.vibratoNotes++;
            if (note.isHammerOn)
                stats.hammerOnNotes++;
            if (note.isPullOff)
                stats.pullOffNotes++;
            if (note.isHopo || note.isHammerOn || note.isPullOff)
                stats.hopoNotes++;
            if (note.isMuted)
                stats.mutedNotes++;
            if (note.isPalmMute)
                stats.palmMuteNotes++;
            if (note.isFretHandMute)
                stats.fretHandMuteNotes++;
            if (note.isHarmonic)
                stats.harmonicNotes++;
            if (note.isAccent)
                stats.accentNotes++;
            if (note.isTap)
                stats.tapNotes++;
            if (note.isTremolo)
                stats.tremoloNotes++;
            if (note.isPinchHarmonic)
                stats.pinchHarmonicNotes++;
            if (note.bendPreBend)
                stats.preBendNotes++;
            if (note.bendRelease)
                stats.bendReleaseNotes++;
            stats.bendPointCount += note.bendPoints?.Count ?? 0;
        }

        return stats;
    }

    private static ArrangementTechniqueStats BuildEditorStats(ChartEditorTrack track)
    {
        ArrangementTechniqueStats stats = new ArrangementTechniqueStats
        {
            noteCount = track?.notes?.Count ?? 0,
            arpeggioGuideCount = track?.arpeggioGuides?.Count ?? 0
        };

        if (track?.notes == null)
            return stats;

        foreach (ChartEditorNote note in track.notes.Where(note => note != null))
        {
            List<NoteTechniqueSegmentData> segments = GetEditorSegments(note);
            CountSegments(stats, segments);
            bool hasBendSegment = HasBendSegment(segments);
            bool hasSlideSegment = segments.Any(segment => segment.type == NoteTechniqueSegmentType.Slide);
            bool hasVibratoSegment = segments.Any(segment => segment.type == NoteTechniqueSegmentType.Vibrato);

            if (note.technique == NoteTechnique.Bend || note.bendStep > 0.01f || note.bendPreBend || note.bendRelease || note.maxBend > 0.01f || hasBendSegment)
                stats.bendNotes++;
            if (note.technique == NoteTechnique.Slide || note.slideTargetFret >= 0 || hasSlideSegment)
                stats.slideNotes++;
            if (note.technique == NoteTechnique.Vibrato || hasVibratoSegment)
                stats.vibratoNotes++;
            if (note.technique == NoteTechnique.HammerOn)
                stats.hammerOnNotes++;
            if (note.technique == NoteTechnique.PullOff)
                stats.pullOffNotes++;
            if (note.technique == NoteTechnique.HammerOn || note.technique == NoteTechnique.PullOff)
                stats.hopoNotes++;
            if (note.muted)
                stats.mutedNotes++;
            if (note.palmMute)
                stats.palmMuteNotes++;
            if (note.fretHandMute)
                stats.fretHandMuteNotes++;
            if (note.harmonic)
                stats.harmonicNotes++;
            if (note.accent)
                stats.accentNotes++;
            if (note.tap)
                stats.tapNotes++;
            if (note.tremolo)
                stats.tremoloNotes++;
            if (note.pinchHarmonic)
                stats.pinchHarmonicNotes++;
            if (note.bendPreBend)
                stats.preBendNotes++;
            if (note.bendRelease)
                stats.bendReleaseNotes++;
            stats.bendPointCount += note.bendPoints?.Count ?? 0;
        }

        return stats;
    }

    private static List<NoteTechniqueSegmentData> GetCachedSegments(RocksmithCachedNoteData note, bool normalizeSegments)
    {
        if (note == null)
            return new List<NoteTechniqueSegmentData>();

        if (normalizeSegments)
            return RocksmithCachedSongLoader.BuildNormalizedTechniqueSegments(note) ?? new List<NoteTechniqueSegmentData>();

        return note.techniqueSegments?
            .Where(segment => segment != null)
            .Select(segment => new NoteTechniqueSegmentData(
                (NoteTechniqueSegmentType)Mathf.Clamp(segment.type, 0, (int)NoteTechniqueSegmentType.Vibrato),
                segment.startOffset,
                segment.endOffset,
                segment.startFret,
                segment.endFret,
                segment.startBend,
                segment.endBend))
            .ToList() ?? new List<NoteTechniqueSegmentData>();
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

    private static void CountSegments(ArrangementTechniqueStats stats, List<NoteTechniqueSegmentData> segments)
    {
        stats.techniqueSegmentCount += segments?.Count ?? 0;
        if (segments == null)
            return;

        for (int i = 0; i < segments.Count; i++)
        {
            switch (segments[i].type)
            {
                case NoteTechniqueSegmentType.Bend:
                    stats.bendSegmentCount++;
                    break;
                case NoteTechniqueSegmentType.Slide:
                    stats.slideSegmentCount++;
                    break;
                case NoteTechniqueSegmentType.Sustain:
                    stats.sustainSegmentCount++;
                    break;
                case NoteTechniqueSegmentType.Vibrato:
                    stats.vibratoSegmentCount++;
                    break;
            }
        }
    }

    private static bool HasBendSegment(List<NoteTechniqueSegmentData> segments)
    {
        return segments != null && segments.Any(segment =>
            segment.type == NoteTechniqueSegmentType.Bend ||
            Mathf.Abs(segment.startBend) > 0.01f ||
            Mathf.Abs(segment.endBend) > 0.01f);
    }

    private static void AssertSourceFlagsMatch(ArrangementTechniqueStats expected, ArrangementTechniqueStats actual, string label)
    {
        Assert.AreEqual(expected.noteCount, actual.noteCount, $"{label}: note count mismatch.");
        Assert.AreEqual(expected.arpeggioGuideCount, actual.arpeggioGuideCount, $"{label}: arpeggio guide count mismatch.");
        Assert.AreEqual(expected.bendNotes, actual.bendNotes, $"{label}: bend note count mismatch.");
        Assert.AreEqual(expected.slideNotes, actual.slideNotes, $"{label}: slide note count mismatch.");
        Assert.AreEqual(expected.vibratoNotes, actual.vibratoNotes, $"{label}: vibrato note count mismatch.");
        Assert.AreEqual(expected.hammerOnNotes, actual.hammerOnNotes, $"{label}: hammer-on count mismatch.");
        Assert.AreEqual(expected.pullOffNotes, actual.pullOffNotes, $"{label}: pull-off count mismatch.");
        Assert.AreEqual(expected.hopoNotes, actual.hopoNotes, $"{label}: HO/PO count mismatch.");
        Assert.AreEqual(expected.mutedNotes, actual.mutedNotes, $"{label}: muted note count mismatch.");
        Assert.AreEqual(expected.palmMuteNotes, actual.palmMuteNotes, $"{label}: palm mute count mismatch.");
        Assert.AreEqual(expected.fretHandMuteNotes, actual.fretHandMuteNotes, $"{label}: fret-hand mute count mismatch.");
        Assert.AreEqual(expected.harmonicNotes, actual.harmonicNotes, $"{label}: harmonic count mismatch.");
        Assert.AreEqual(expected.accentNotes, actual.accentNotes, $"{label}: accent count mismatch.");
        Assert.AreEqual(expected.tapNotes, actual.tapNotes, $"{label}: tap count mismatch.");
        Assert.AreEqual(expected.tremoloNotes, actual.tremoloNotes, $"{label}: tremolo count mismatch.");
        Assert.AreEqual(expected.pinchHarmonicNotes, actual.pinchHarmonicNotes, $"{label}: pinch harmonic count mismatch.");
        Assert.AreEqual(expected.preBendNotes, actual.preBendNotes, $"{label}: prebend count mismatch.");
        Assert.AreEqual(expected.bendReleaseNotes, actual.bendReleaseNotes, $"{label}: bend release count mismatch.");
    }

    private static void AssertStatsMatch(ArrangementTechniqueStats expected, ArrangementTechniqueStats actual, string label)
    {
        AssertSourceFlagsMatch(expected, actual, label);
        Assert.AreEqual(expected.bendPointCount, actual.bendPointCount, $"{label}: bend point count mismatch.");
        Assert.AreEqual(expected.techniqueSegmentCount, actual.techniqueSegmentCount, $"{label}: technique segment count mismatch.");
        Assert.AreEqual(expected.bendSegmentCount, actual.bendSegmentCount, $"{label}: bend segment count mismatch.");
        Assert.AreEqual(expected.slideSegmentCount, actual.slideSegmentCount, $"{label}: slide segment count mismatch.");
        Assert.AreEqual(expected.sustainSegmentCount, actual.sustainSegmentCount, $"{label}: sustain segment count mismatch.");
        Assert.AreEqual(expected.vibratoSegmentCount, actual.vibratoSegmentCount, $"{label}: vibrato segment count mismatch.");
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

    private static List<string> BuildCachedToneDigest(RocksmithCachedArrangementToneData toneData)
    {
        List<string> digest = new List<string> { $"base|{toneData?.baseToneName ?? string.Empty}" };
        digest.AddRange((toneData?.changes ?? new List<RocksmithCachedToneChangeData>())
            .Where(change => change != null)
            .OrderBy(change => change.timeSeconds)
            .ThenBy(change => change.toneName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(change => string.Join("|",
                "change",
                FormatSeconds(change.timeSeconds),
                change.toneName ?? string.Empty,
                change.toneId)));
        digest.AddRange((toneData?.definitions ?? new List<RocksmithCachedToneDefinitionData>())
            .Where(definition => definition != null)
            .OrderBy(definition => definition.name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(definition => definition.key ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(definition => string.Join("|",
                "definition",
                definition.name ?? string.Empty,
                definition.key ?? string.Empty,
                UnityTonePresetDigest(ParseToneLabPresetForDigest(definition.rawJson)))));
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

    private static List<string> BuildCachedNoteDigest(RocksmithCachedArrangementPart part)
    {
        return part?.notes?
            .Where(note => note != null)
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
                note.technique,
                note.slideTargetFret,
                FormatSeconds(note.bendStep),
                FormatSeconds(note.bendVisualStartTime),
                FormatSeconds(note.bendVisualDuration),
                note.bendPreBend,
                note.bendRelease,
                note.isMuted,
                note.isPalmMute,
                note.isFretHandMute,
                note.isHarmonic,
                note.isAccent,
                note.isTap,
                note.isTremolo,
                note.isPinchHarmonic,
                note.vibratoStrength,
                note.isLegato,
                note.requiresPluck,
                note.linkedFromNoteId,
                BendPointDigest(note.bendPoints),
                SegmentDigest(GetCachedSegments(note, normalizeSegments: false))))
            .ToList() ?? new List<string>();
    }

    private static string BendPointDigest(IEnumerable<ChartEditorBendPoint> points)
    {
        return string.Join(",", points?
            .Where(point => point != null)
            .OrderBy(point => point.timeSeconds)
            .Select(point => $"{FormatSeconds(point.timeSeconds)}:{FormatSeconds(point.step)}") ?? Enumerable.Empty<string>());
    }

    private static string BendPointDigest(IEnumerable<RocksmithCachedBendPointData> points)
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

    private sealed class ArrangementTechniqueStats
    {
        public int noteCount;
        public int arpeggioGuideCount;
        public int techniqueSegmentCount;
        public int bendSegmentCount;
        public int slideSegmentCount;
        public int sustainSegmentCount;
        public int vibratoSegmentCount;
        public int bendNotes;
        public int slideNotes;
        public int vibratoNotes;
        public int hammerOnNotes;
        public int pullOffNotes;
        public int hopoNotes;
        public int mutedNotes;
        public int palmMuteNotes;
        public int fretHandMuteNotes;
        public int harmonicNotes;
        public int accentNotes;
        public int tapNotes;
        public int tremoloNotes;
        public int pinchHarmonicNotes;
        public int preBendNotes;
        public int bendReleaseNotes;
        public int bendPointCount;
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
