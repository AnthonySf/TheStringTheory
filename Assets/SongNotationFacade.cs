using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class SongNotationFacade
{
    public static bool TryDetectKind(string filePath, out SongNotationSourceKind kind)
    {
        kind = SongNotationSourceKind.None;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        if (ArrangementCacheSongLoader.IsManifestPath(filePath))
        {
            kind = SongNotationSourceKind.ArrangementCache;
            return true;
        }

        string extension = Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
        if (extension == TheoryPackageFormat.Extension)
        {
            kind = SongNotationSourceKind.TheoryPackage;
            return true;
        }

        if (extension == ".musicxml" || extension == ".xml")
        {
            kind = SongNotationSourceKind.MusicXml;
            return true;
        }

        if (extension == ".gp5" ||
            extension == ".gp4" ||
            extension == ".gp3" ||
            extension == ".gpx" ||
            extension == ".gp")
        {
            kind = SongNotationSourceKind.Gp5;
            return true;
        }

        return false;
    }

    public static List<MusicXmlLoader.MusicXmlPartSummary> GetPartSummaries(string filePath, SongNotationSourceKind kind)
    {
        switch (kind)
        {
            case SongNotationSourceKind.TheoryPackage:
                return TheorySongLoader.GetPartSummaries(filePath);
            case SongNotationSourceKind.ArrangementCache:
                return ArrangementCacheSongLoader.GetPartSummaries(filePath);
            case SongNotationSourceKind.Gp5:
                return AlphaTabGpLoader.GetPartSummaries(filePath);
            case SongNotationSourceKind.MusicXml:
                return MusicXmlLoader.GetPartSummaries(filePath);
            default:
                return new List<MusicXmlLoader.MusicXmlPartSummary>();
        }
    }

    public static List<NoteData> LoadSong(string filePath, SongNotationSourceKind kind, int targetPartIndex = -1)
    {
        switch (kind)
        {
            case SongNotationSourceKind.TheoryPackage:
                return TheorySongLoader.LoadSong(filePath, targetPartIndex);
            case SongNotationSourceKind.ArrangementCache:
                return ArrangementCacheSongLoader.LoadSong(filePath, targetPartIndex);
            case SongNotationSourceKind.Gp5:
                return AlphaTabGpLoader.LoadSong(filePath, targetPartIndex);
            case SongNotationSourceKind.MusicXml:
                return MusicXmlLoader.LoadMusicXmlSong(filePath, targetPartIndex);
            default:
                return null;
        }
    }

    public static List<ArpeggioGuideData> LoadArpeggioGuides(string filePath, SongNotationSourceKind kind, int targetPartIndex = -1)
    {
        switch (kind)
        {
            case SongNotationSourceKind.TheoryPackage:
                return TheorySongLoader.LoadArpeggioGuides(filePath, targetPartIndex);
            case SongNotationSourceKind.ArrangementCache:
                return ArrangementCacheSongLoader.LoadArpeggioGuides(filePath, targetPartIndex);
            default:
                return new List<ArpeggioGuideData>();
        }
    }

    public static GeneratedPlaybackArrangement LoadGeneratedArrangement(string filePath, SongNotationSourceKind kind)
    {
        switch (kind)
        {
            case SongNotationSourceKind.TheoryPackage:
                return TheorySongLoader.LoadGeneratedArrangement(filePath);
            case SongNotationSourceKind.ArrangementCache:
                return ArrangementCacheSongLoader.LoadGeneratedArrangement(filePath);
            case SongNotationSourceKind.Gp5:
                return AlphaTabGpBandPlaybackLoader.LoadArrangement(filePath);
            case SongNotationSourceKind.MusicXml:
                return MusicXmlBandPlaybackLoader.LoadArrangement(filePath);
            default:
                return null;
        }
    }

    public static string TryReadDisplayName(string filePath, SongNotationSourceKind kind)
    {
        switch (kind)
        {
            case SongNotationSourceKind.TheoryPackage:
                return TheorySongLoader.TryReadDisplayName(filePath);
            case SongNotationSourceKind.ArrangementCache:
                return ArrangementCacheSongLoader.TryReadDisplayName(filePath);
            case SongNotationSourceKind.Gp5:
                return AlphaTabGpLoader.TryReadTitle(filePath);
            case SongNotationSourceKind.MusicXml:
                return SongLibraryService.TryReadDisplayNameFromXml(filePath);
            default:
                return null;
        }
    }

    public static string TryReadCreator(string filePath, SongNotationSourceKind kind)
    {
        switch (kind)
        {
            case SongNotationSourceKind.TheoryPackage:
                return TheorySongLoader.TryReadArtist(filePath);
            case SongNotationSourceKind.ArrangementCache:
                return ArrangementCacheSongLoader.TryReadArtist(filePath);
            case SongNotationSourceKind.Gp5:
                return AlphaTabGpLoader.TryReadArtist(filePath);
            case SongNotationSourceKind.MusicXml:
                return SongLibraryService.TryReadCreatorFromXml(filePath);
            default:
                return null;
        }
    }
}

public static class ArrangementCacheSongLoader
{
    public static bool IsManifestPath(string filePath)
    {
        return RocksmithCachedSongLoader.IsRocksmithManifestPath(filePath);
    }

    public static bool TryLoadManifest(string manifestPath, out RocksmithCachedSongManifest manifest)
    {
        return RocksmithCachedSongLoader.TryLoadManifest(manifestPath, out manifest);
    }

    public static bool TryLoadArrangementPartByPartId(
        string manifestPath,
        string partId,
        out RocksmithCachedArrangementSummary summary,
        out RocksmithCachedArrangementPart part)
    {
        return RocksmithCachedSongLoader.TryLoadArrangementPartByPartId(manifestPath, partId, out summary, out part);
    }

    public static bool TryLoadArrangementPartByGroupId(
        string manifestPath,
        string arrangementGroupId,
        out RocksmithCachedArrangementSummary summary,
        out RocksmithCachedArrangementPart part)
    {
        return RocksmithCachedSongLoader.TryLoadArrangementPartByGroupId(manifestPath, arrangementGroupId, out summary, out part);
    }

    public static List<MusicXmlLoader.MusicXmlPartSummary> GetPartSummaries(string manifestPath)
    {
        return RocksmithCachedSongLoader.GetPartSummaries(manifestPath);
    }

    public static List<NoteData> LoadSong(string manifestPath, int targetPartIndex = -1)
    {
        return RocksmithCachedSongLoader.LoadSong(manifestPath, targetPartIndex);
    }

    public static List<ArpeggioGuideData> LoadArpeggioGuides(string manifestPath, int targetPartIndex = -1)
    {
        return RocksmithCachedSongLoader.LoadArpeggioGuides(manifestPath, targetPartIndex);
    }

    public static GeneratedPlaybackArrangement LoadGeneratedArrangement(string manifestPath)
    {
        return RocksmithCachedSongLoader.LoadGeneratedArrangement(manifestPath);
    }

    public static string TryReadDisplayName(string manifestPath)
    {
        return RocksmithCachedSongLoader.TryReadDisplayName(manifestPath);
    }

    public static string TryReadArtist(string manifestPath)
    {
        return RocksmithCachedSongLoader.TryReadArtist(manifestPath);
    }

    public static string FindManifestInDirectory(string directory)
    {
        return RocksmithCachedSongLoader.FindManifestInDirectory(directory);
    }
}

public static class ArrangementCacheImportService
{
    public static void RefreshImports(Action<int, int, string> progress = null)
    {
        RocksmithImportService.RefreshImports(progress);
    }
}

public static class ArrangementCacheMetadata
{
    public static string BuildDifficultySummary(RocksmithCachedSongManifest manifest)
    {
        if (manifest?.arrangements == null || manifest.arrangements.Count == 0)
            return string.Empty;

        bool hasMultiple = manifest.arrangements.Any(arrangement => arrangement != null && arrangement.hasDifficultyVariants);
        if (!hasMultiple)
            return string.Empty;

        HashSet<string> labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < manifest.arrangements.Count; i++)
        {
            string label = manifest.arrangements[i]?.difficultyLabel;
            if (!string.IsNullOrWhiteSpace(label))
                labels.Add(label.Trim().ToUpperInvariant());
        }

        if (labels.Count == 0)
            return string.Empty;

        string[] ordered = { "X", "H", "M", "E" };
        return string.Concat(ordered.Where(labels.Contains));
    }
}
