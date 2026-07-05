using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEngine;

[Serializable]
public sealed class SongLibraryEntry
{
    public string SongId;
    public SongLibraryType LibraryType;
    public string DisplayName;
    public string Artist;
    public string Album;
    public string Subtitle;
    public string ArtworkPath;
    public int DifficultyRating;
    public string DifficultyDisplayLabel;
    public string SongDirectory;
    public string Mp3Path;
    public string PrimaryNotationPath;
    public SongNotationSourceKind PrimaryNotationKind;
    public string GpPath;
    public string XmlPath;
    public string MetadataPath;
    public float DurationSeconds;
    public string MidiPath;
    public string ArcadeChartPath;
    public string ArcadeSongIniPath;
    public string ArcadeDifficultySummary;
    public List<string> ArcadeAudioPaths = new List<string>();
    public bool CachedFavoriteInLibrary;
    public int CachedBestScoreValue;
    public float CachedBestScorePercent;
    public int CachedHeroBestScoreValue;
    public float CachedHeroBestScorePercent;
    public int CachedHeroBestHeartsRemaining;
    public int CachedHeroBestHeartsTotal;
    public int CachedBestArcadeScoreValue;
}

public sealed class SongLibraryImportCandidate
{
    public string ImporterId;
    public string SourcePath;
    public string SourceDirectory;
    public string DisplayName;
    public string Subtitle;
    public string SourceKindLabel;
    public string AudioPath;
    public SongNotationSourceKind NotationKind;
}

public static class SongLibraryService
{
    private const string SongDefinitionFileName = "song.json";
    // v12: raw GP/MusicXML entries covered by a .theory conversion are filtered from
    // the listing at scan time, so caches written by older versions must be rebuilt.
    private const int SongLibraryCacheVersion = 12;
    private const int MaxSongDirectoryDiscoveryDepth = 12;
    private const string LegacyTheoryPackageFolderName = "theory";
    private static readonly string[] SupportedAudioExtensions = { ".ogg", ".mp3", ".wav", ".flac", ".m4a", ".aiff", ".aif" };
    private static readonly string[] PrimaryAudioExtensions = { ".mp3", ".wav", ".ogg", ".flac", ".m4a", ".aiff", ".aif" };
    private static readonly string[] SupportedAudioPatterns = SupportedAudioExtensions
        .Select(extension => "*" + extension)
        .ToArray();
    private static readonly string[] PrimaryAudioPatterns = PrimaryAudioExtensions
        .Select(extension => "*" + extension)
        .ToArray();

    [Serializable]
    private sealed class SongLibraryCacheManifest
    {
        public int version = SongLibraryCacheVersion;
        public long generatedAtUtcTicks;
        public string librarySignature;
        public List<SongLibraryEntry> entries = new List<SongLibraryEntry>();
    }

    private static readonly List<SongLibraryEntry> sessionCachedEntries = new List<SongLibraryEntry>();
    private static bool sessionCacheLoaded;

    [Serializable]
    private sealed class SongFolderMetadata
    {
        public string songId;
        public string displayName;
        public string artist;
        public string album;
        public string subtitle;
        public int difficulty;
    }

    private sealed class CloneHeroFolderMetadata
    {
        public string name;
        public string artist;
        public string album;
        public string charter;
        public string year;
        public int difficulty;
        public int songLengthMilliseconds;
    }

    [Serializable]
    private sealed class CachedSongMetadataPayload
    {
        public bool favoriteInLibrary;
        public int bestScoreValue;
        public float bestScorePercent;
        public int bestArcadeScoreValue;
        public List<CachedTrackScorePayload> trackScores = new List<CachedTrackScorePayload>();
        public List<CachedArcadeScorePayload> arcadeScores = new List<CachedArcadeScorePayload>();
    }

    [Serializable]
    private sealed class CachedTrackScorePayload
    {
        public int bestScoreValue;
        public float bestScorePercent;
        public int heroBestScoreValue;
        public float heroBestScorePercent;
        public int heroBestHeartsRemaining;
        public int heroBestHeartsTotal;
    }

    [Serializable]
    private sealed class CachedArcadeScorePayload
    {
        public int bestScoreValue;
    }

    public static string GetDifficultyLabel(int difficultyRating)
    {
        switch (Mathf.Clamp(difficultyRating, 0, 5))
        {
            case 1: return "Beginner";
            case 2: return "Novice";
            case 3: return "Standard";
            case 4: return "Advanced";
            case 5: return "Master";
            default: return "Unknown";
        }
    }

    public static bool TryGetFirstValidSong(out SongLibraryEntry entry)
    {
        List<SongLibraryEntry> songs = GetAvailableSongs();
        entry = songs.Count > 0 ? songs[0] : null;
        return entry != null;
    }

    public static void ClearCache()
    {
        sessionCachedEntries.Clear();
        sessionCacheLoaded = false;
    }

    public static List<SongLibraryImportCandidate> DiscoverPendingTheoryConversionCandidates()
    {
        List<SongLibraryImportCandidate> candidates = new List<SongLibraryImportCandidate>();
        string songsDirectory = ExternalContentPaths.PersistentSongsDirectory;
        if (string.IsNullOrWhiteSpace(songsDirectory) || !Directory.Exists(songsDirectory))
            return candidates;

        HashSet<string> seenSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> cachedLegacySourceKeys = ReadCachedLegacySourceKeysUnchecked();
        ConvertedTheorySourceIndex convertedTheorySources = DiscoverConvertedTheorySourceStamps(songsDirectory);

        AddExternalImporterCandidates(songsDirectory, convertedTheorySources, seenSources, candidates);
        AddNotationImportCandidates(songsDirectory, cachedLegacySourceKeys, convertedTheorySources, seenSources, candidates);

        candidates.Sort((a, b) =>
        {
            int nameCompare = string.Compare(a?.DisplayName ?? string.Empty, b?.DisplayName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            return nameCompare != 0
                ? nameCompare
                : string.Compare(a?.SourcePath ?? string.Empty, b?.SourcePath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        });
        return candidates;
    }

    public static List<SongLibraryImportCandidate> DiscoverExistingRawNotationTheoryConversionCandidates()
    {
        List<SongLibraryImportCandidate> candidates = new List<SongLibraryImportCandidate>();
        string songsDirectory = ExternalContentPaths.PersistentSongsDirectory;
        if (string.IsNullOrWhiteSpace(songsDirectory) || !Directory.Exists(songsDirectory))
            return candidates;

        HashSet<string> seenSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ConvertedTheorySourceIndex convertedTheorySources = DiscoverConvertedTheorySourceStamps(songsDirectory);
        AddNotationImportCandidates(
            songsDirectory,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            convertedTheorySources,
            seenSources,
            candidates,
            skipCachedLegacySources: false);

        candidates.Sort((a, b) =>
        {
            int nameCompare = string.Compare(a?.DisplayName ?? string.Empty, b?.DisplayName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            return nameCompare != 0
                ? nameCompare
                : string.Compare(a?.SourcePath ?? string.Empty, b?.SourcePath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        });
        return candidates;
    }

    public static int ConvertImportCandidatesToTheoryPackages(
        IReadOnlyList<SongLibraryImportCandidate> candidates,
        Action<int, int, string> progress,
        out List<string> errors)
    {
        errors = new List<string>();
        if (candidates == null || candidates.Count == 0)
        {
            progress?.Invoke(0, 0, string.Empty);
            return 0;
        }

        int convertedCount = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            SongLibraryImportCandidate candidate = candidates[i];
            string displayName = string.IsNullOrWhiteSpace(candidate?.DisplayName)
                ? Path.GetFileNameWithoutExtension(candidate?.SourcePath ?? string.Empty)
                : candidate.DisplayName;
            progress?.Invoke(i + 1, candidates.Count, displayName);

            if (candidate == null || string.IsNullOrWhiteSpace(candidate.SourcePath))
            {
                errors.Add("Skipped an empty library import candidate.");
                continue;
            }

            string conversionError;
            bool converted;
            if (!string.IsNullOrWhiteSpace(candidate.ImporterId))
            {
                converted = SongImporterRegistry.ConvertSourceToTheoryPackage(
                    new SongImporterConversionRequest
                    {
                        importerId = candidate.ImporterId,
                        sourcePath = candidate.SourcePath,
                        overwriteExisting = false,
                        validatePackage = true,
                        requireAudio = true,
                        useLibrarySongsDirectory = true,
                        rejectChartEditorOutputDirectory = true
                    },
                    out _,
                    out conversionError);
            }
            else
            {
                converted = ChartEditorTheoryConversionService.ConvertLibrarySourceToTheoryPackage(
                    new ChartEditorTheoryConversionRequest
                    {
                        sourcePath = candidate.SourcePath,
                        audioPath = candidate.AudioPath,
                        overwriteExisting = false,
                        validatePackage = true,
                        requireAudio = true
                    },
                    out _,
                    out conversionError);
            }

            if (converted)
            {
                convertedCount++;
            }
            else
            {
                errors.Add($"{displayName}: {conversionError}");
            }
        }

        progress?.Invoke(candidates.Count, candidates.Count, string.Empty);
        return convertedCount;
    }

    public static void UpdateCachedMetadataSummary(
        string songDirectory,
        string primaryNotationPath,
        bool favoriteInLibrary,
        int bestScoreValue,
        float bestScorePercent,
        int heroBestScoreValue,
        float heroBestScorePercent,
        int heroBestHeartsRemaining,
        int heroBestHeartsTotal,
        int bestArcadeScoreValue)
    {
        if (string.IsNullOrWhiteSpace(songDirectory))
            return;

        string normalizedSongDirectory = NormalizeSongDirectoryKey(songDirectory);
        string normalizedPrimaryNotationPath = NormalizeSongDirectoryKey(primaryNotationPath);
        EnsureSessionCacheLoadedForUpdates();

        bool updated = false;
        for (int i = 0; i < sessionCachedEntries.Count; i++)
        {
            SongLibraryEntry entry = sessionCachedEntries[i];
            if (entry == null ||
                !string.Equals(NormalizeSongDirectoryKey(entry.SongDirectory), normalizedSongDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(normalizedPrimaryNotationPath) &&
                !string.Equals(NormalizeSongDirectoryKey(entry.PrimaryNotationPath), normalizedPrimaryNotationPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ApplyCachedMetadataSummary(
                entry,
                favoriteInLibrary,
                bestScoreValue,
                bestScorePercent,
                heroBestScoreValue,
                heroBestScorePercent,
                heroBestHeartsRemaining,
                heroBestHeartsTotal,
                bestArcadeScoreValue);
            updated = true;
        }

        if (updated)
            SaveCacheManifest(sessionCachedEntries);
    }

    private static string NormalizeSongDirectoryKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string normalized = path.Replace('\\', '/').Trim();
        while (normalized.EndsWith("/", StringComparison.Ordinal))
            normalized = normalized.Substring(0, normalized.Length - 1);
        return normalized;
    }

    private sealed class ConvertedTheorySourceStamp
    {
        public long LastWriteUtcTicks;
        public long SizeBytes;
    }

    private static void AddExternalImporterCandidates(
        string songsDirectory,
        ConvertedTheorySourceIndex convertedTheorySources,
        HashSet<string> seenSources,
        List<SongLibraryImportCandidate> candidates)
    {
        List<SongImporterDescriptor> importers = SongImporterRegistry.GetAvailableImporters(forceRefresh: true);
        if (importers.Count == 0)
            return;

        HashSet<string> importerCacheFolderNames = SongImporterRegistry.GetInstalledImporterCacheFolderNames();

        foreach (SongImporterDescriptor importer in importers)
        {
            if (importer == null || !SongImporterRegistry.ImporterHasUsableEntrypoint(importer))
                continue;

            if (importer.Extensions != null && importer.Extensions.Count > 0)
            {
                for (int extensionIndex = 0; extensionIndex < importer.Extensions.Count; extensionIndex++)
                {
                    string extension = NormalizeImporterExtension(importer.Extensions[extensionIndex]);
                    if (string.IsNullOrWhiteSpace(extension))
                        continue;

                    foreach (string sourcePath in EnumerateFilesSafe(songsDirectory, $"*{extension}", SearchOption.AllDirectories))
                    {
                        if (string.IsNullOrWhiteSpace(sourcePath) ||
                            IsPathInsideImportedCacheDirectory(songsDirectory, sourcePath, importerCacheFolderNames) ||
                            SourceHasCurrentTheoryConversion(sourcePath, convertedTheorySources) ||
                            !seenSources.Add(NormalizeFullPathKey(sourcePath)))
                        {
                            continue;
                        }

                        string directory = Path.GetDirectoryName(sourcePath) ?? songsDirectory;
                        candidates.Add(new SongLibraryImportCandidate
                        {
                            ImporterId = importer.Id,
                            SourcePath = Path.GetFullPath(sourcePath),
                            SourceDirectory = directory,
                            DisplayName = Path.GetFileNameWithoutExtension(sourcePath),
                            Subtitle = BuildImportCandidateSubtitle(importer.DisplayName, directory, songsDirectory),
                            SourceKindLabel = importer.DisplayName,
                            AudioPath = string.Empty,
                            NotationKind = SongNotationSourceKind.None
                        });
                    }
                }
            }
        }

        foreach (string folderPath in EnumerateDirectoriesSafe(songsDirectory, includeRoot: true))
        {
            string sourceKey = NormalizeFullPathKey(folderPath);
            if (string.IsNullOrWhiteSpace(sourceKey) ||
                IsPathInsideImportedCacheDirectory(songsDirectory, folderPath, importerCacheFolderNames, allowLegacyUnpackedCacheRoot: true))
            {
                continue;
            }

            List<SongImporterFolderMatch> folderMatches = SongImporterRegistry.GetMatchingFolderImporters(folderPath);
            if (folderMatches.Count == 0)
                continue;

            if (LegacyUnpackedCacheIsCoveredByPackedSource(songsDirectory, folderPath, convertedTheorySources) ||
                DirectoryDirectlyContainsLoadableTheoryPackage(folderPath) ||
                SourceHasCurrentTheoryConversion(folderPath, convertedTheorySources) ||
                !seenSources.Add(sourceKey))
            {
                continue;
            }

            SongImporterFolderMatch match = folderMatches[0];
            SongImporterDescriptor importer = match.importer;
            if (importer == null)
                continue;

            string sourceLabel = string.IsNullOrWhiteSpace(match.signature?.displayName)
                ? importer.DisplayName
                : match.signature.displayName.Trim();
            candidates.Add(new SongLibraryImportCandidate
            {
                ImporterId = importer.Id,
                SourcePath = Path.GetFullPath(folderPath),
                SourceDirectory = folderPath,
                DisplayName = PsarcCachedSongFormat.StripImportedFolderDecorations(
                    Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))),
                Subtitle = BuildImportCandidateSubtitle(sourceLabel, folderPath, songsDirectory),
                SourceKindLabel = sourceLabel,
                AudioPath = string.Empty,
                NotationKind = SongNotationSourceKind.None
            });
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string directory, bool includeRoot)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return Enumerable.Empty<string>();

        try
        {
            List<string> result = new List<string>();
            if (includeRoot)
                result.Add(Path.GetFullPath(directory));

            result.AddRange(Directory.GetDirectories(directory, "*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongLibraryService] Failed to inspect folders under '{directory}': {ex.Message}");
            return Enumerable.Empty<string>();
        }
    }

    private static void AddNotationImportCandidates(
        string songsDirectory,
        HashSet<string> cachedLegacySourceKeys,
        ConvertedTheorySourceIndex convertedTheorySources,
        HashSet<string> seenSources,
        List<SongLibraryImportCandidate> candidates,
        bool skipCachedLegacySources = true)
    {
        List<string> songDirectories = DiscoverSongDirectories(songsDirectory);
        for (int i = 0; i < songDirectories.Count; i++)
        {
            string songDirectory = songDirectories[i];
            if (!string.IsNullOrWhiteSpace(TheorySongLoader.FindPackageInDirectory(songDirectory, requireLoadable: true)))
                continue;

            if (!TryBuildEntry(songDirectory, out SongLibraryEntry entry) ||
                entry == null ||
                entry.LibraryType != SongLibraryType.Guitar ||
                string.IsNullOrWhiteSpace(entry.PrimaryNotationPath))
            {
                continue;
            }

            if (entry.PrimaryNotationKind != SongNotationSourceKind.Gp5 &&
                entry.PrimaryNotationKind != SongNotationSourceKind.MusicXml)
            {
                continue;
            }

            string sourceKey = NormalizeFullPathKey(entry.PrimaryNotationPath);
            if (string.IsNullOrWhiteSpace(sourceKey) ||
                (skipCachedLegacySources && cachedLegacySourceKeys.Contains(sourceKey)) ||
                SourceHasCurrentTheoryConversion(entry.PrimaryNotationPath, convertedTheorySources) ||
                !seenSources.Add(sourceKey))
            {
                continue;
            }

            candidates.Add(new SongLibraryImportCandidate
            {
                SourcePath = Path.GetFullPath(entry.PrimaryNotationPath),
                SourceDirectory = songDirectory,
                DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                    ? Path.GetFileNameWithoutExtension(entry.PrimaryNotationPath)
                    : entry.DisplayName,
                Subtitle = BuildImportCandidateSubtitle(GetNotationKindLabel(entry.PrimaryNotationKind), songDirectory, songsDirectory, entry.Artist),
                SourceKindLabel = GetNotationKindLabel(entry.PrimaryNotationKind),
                AudioPath = entry.Mp3Path,
                NotationKind = entry.PrimaryNotationKind
            });
        }
    }

    private static HashSet<string> ReadCachedLegacySourceKeysUnchecked()
    {
        HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string cachePath = ExternalContentPaths.PersistentSongLibraryCachePath;
        if (string.IsNullOrWhiteSpace(cachePath) || !File.Exists(cachePath))
            return keys;

        try
        {
            SongLibraryCacheManifest manifest = JsonUtility.FromJson<SongLibraryCacheManifest>(File.ReadAllText(cachePath));
            if (manifest?.entries == null)
                return keys;

            for (int i = 0; i < manifest.entries.Count; i++)
            {
                SongLibraryEntry entry = manifest.entries[i];
                if (entry == null || entry.LibraryType != SongLibraryType.Guitar)
                    continue;

                if (entry.PrimaryNotationKind == SongNotationSourceKind.Gp5 ||
                    entry.PrimaryNotationKind == SongNotationSourceKind.MusicXml)
                {
                    string key = NormalizeFullPathKey(entry.PrimaryNotationPath);
                    if (!string.IsNullOrWhiteSpace(key))
                        keys.Add(key);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongLibraryService] Failed to inspect existing song library cache for import candidates: {ex.Message}");
        }

        return keys;
    }

    private sealed class ConvertedTheorySourceIndex
    {
        public readonly Dictionary<string, List<ConvertedTheorySourceStamp>> StampsBySourcePath =
            new Dictionary<string, List<ConvertedTheorySourceStamp>>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> StampFingerprints = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> ContentFingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildSourceStampFingerprint(long sizeBytes, long lastWriteUtcTicks)
    {
        return sizeBytes > 0L && lastWriteUtcTicks > 0L
            ? sizeBytes.ToString() + ":" + lastWriteUtcTicks.ToString()
            : string.Empty;
    }

    private static ConvertedTheorySourceIndex DiscoverConvertedTheorySourceStamps(string songsDirectory)
    {
        ConvertedTheorySourceIndex index = new ConvertedTheorySourceIndex();
        foreach (string packagePath in EnumerateFilesSafe(songsDirectory, $"*{TheoryPackageFormat.Extension}", SearchOption.AllDirectories))
        {
            if (!TheoryPackageIO.TryReadManifest(packagePath, out TheorySongManifest manifest, out _))
                continue;

            TheoryImportProvenance provenance = manifest?.provenance;
            if (provenance == null)
                continue;

            long stampTicks = Math.Max(0L, provenance.sourceLastWriteUtcTicks);
            long stampSize = Math.Max(0L, provenance.sourceSizeBytes);

            string fingerprint = BuildSourceStampFingerprint(stampSize, stampTicks);
            if (!string.IsNullOrWhiteSpace(fingerprint))
                index.StampFingerprints.Add(fingerprint);

            if (!string.IsNullOrWhiteSpace(provenance.sourceContentFingerprint))
                index.ContentFingerprints.Add(provenance.sourceContentFingerprint.Trim());

            string key = NormalizeFullPathKey(provenance.sourcePath);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (!index.StampsBySourcePath.TryGetValue(key, out List<ConvertedTheorySourceStamp> sourceStamps))
            {
                sourceStamps = new List<ConvertedTheorySourceStamp>();
                index.StampsBySourcePath[key] = sourceStamps;
            }

            sourceStamps.Add(new ConvertedTheorySourceStamp
            {
                LastWriteUtcTicks = stampTicks,
                SizeBytes = stampSize
            });
        }

        return index;
    }

    private static bool SourceHasCurrentTheoryConversion(
        string sourcePath,
        ConvertedTheorySourceIndex convertedTheorySources)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || convertedTheorySources == null)
            return false;

        string key = NormalizeFullPathKey(sourcePath);
        if (string.IsNullOrWhiteSpace(key))
            return false;

        bool hasPathStamps = convertedTheorySources.StampsBySourcePath.TryGetValue(key, out List<ConvertedTheorySourceStamp> stamps);
        if (!hasPathStamps &&
            convertedTheorySources.StampFingerprints.Count == 0 &&
            convertedTheorySources.ContentFingerprints.Count == 0)
        {
            return false;
        }

        long lastWriteUtcTicks = TryGetLastWriteUtcTicks(sourcePath);
        long sizeBytes = TryGetFileSize(sourcePath);

        if (hasPathStamps)
        {
            for (int i = 0; i < stamps.Count; i++)
            {
                ConvertedTheorySourceStamp stamp = stamps[i];
                if (stamp == null)
                    continue;

                bool timestampMatches = stamp.LastWriteUtcTicks <= 0L || lastWriteUtcTicks <= 0L || stamp.LastWriteUtcTicks == lastWriteUtcTicks;
                bool sizeMatches = stamp.SizeBytes <= 0L || sizeBytes <= 0L || stamp.SizeBytes == sizeBytes;
                if (timestampMatches && sizeMatches)
                    return true;
            }
        }

        // Path-independent fallbacks: a previous conversion stamped an identity for this
        // exact content, so the source was already imported even if it has been moved or
        // copied to a different location since. The content fingerprint (relative file
        // names + sizes) survives copies that reset timestamps; the size+timestamp stamp
        // covers packages imported before content fingerprints existed.
        if (convertedTheorySources.ContentFingerprints.Count > 0)
        {
            string contentFingerprint = SongImporterRegistry.ComputeSourceContentFingerprint(sourcePath);
            if (!string.IsNullOrWhiteSpace(contentFingerprint) &&
                convertedTheorySources.ContentFingerprints.Contains(contentFingerprint))
            {
                return true;
            }
        }

        string sourceFingerprint = BuildSourceStampFingerprint(sizeBytes, lastWriteUtcTicks);
        return !string.IsNullOrWhiteSpace(sourceFingerprint) &&
               convertedTheorySources.StampFingerprints.Contains(sourceFingerprint);
    }

    private static string BuildImportCandidateSubtitle(string kindLabel, string sourceDirectory, string songsDirectory, string artist = null)
    {
        List<string> parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(kindLabel))
            parts.Add(kindLabel.Trim());
        if (!string.IsNullOrWhiteSpace(artist))
            parts.Add(artist.Trim());

        string relative = GetRelativePath(songsDirectory, sourceDirectory);
        if (!string.IsNullOrWhiteSpace(relative) && !string.Equals(relative, ".", StringComparison.Ordinal))
            parts.Add(relative.Replace('\\', '/'));

        return string.Join("  |  ", parts);
    }

    private static string GetNotationKindLabel(SongNotationSourceKind kind)
    {
        switch (kind)
        {
            case SongNotationSourceKind.Gp5:
                return "Guitar Pro";
            case SongNotationSourceKind.MusicXml:
                return "MusicXML";
            case SongNotationSourceKind.TheoryPackage:
                return ".theory";
            default:
                return "Source";
        }
    }

    private static string NormalizeImporterExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        string trimmed = extension.Trim().ToLowerInvariant();
        return trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed : "." + trimmed;
    }

    private static IEnumerable<string> EnumerateFilesSafe(string directory, string pattern, SearchOption searchOption)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return Enumerable.Empty<string>();

        try
        {
            return Directory.GetFiles(directory, pattern, searchOption)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongLibraryService] Failed to inspect files under '{directory}': {ex.Message}");
            return Enumerable.Empty<string>();
        }
    }

    // Legacy unpacked psarc caches ("__psarc_*" folders created by the pre-.theory
    // pipeline) are importable folder sources, but they are skipped when their packed
    // .psarc is still in the library folder (it gets offered itself) or was already
    // converted — otherwise the popup would propose the same song twice.
    private static bool LegacyUnpackedCacheIsCoveredByPackedSource(
        string songsDirectory,
        string folderPath,
        ConvertedTheorySourceIndex convertedTheorySources)
    {
        string folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!PsarcCachedSongFormat.IsImportedFolderName(folderName))
            return false;

        try
        {
            string manifestPath = Path.Combine(folderPath, PsarcCachedSongFormat.ManifestFileName);
            if (!File.Exists(manifestPath))
                return false;

            PsarcCachedSongManifest cacheManifest = JsonUtility.FromJson<PsarcCachedSongManifest>(File.ReadAllText(manifestPath));
            string packedSourcePath = cacheManifest?.sourcePsarcPath?.Trim();
            if (string.IsNullOrWhiteSpace(packedSourcePath))
                return false;

            if (File.Exists(packedSourcePath) && PathIsUnderDirectory(songsDirectory, packedSourcePath))
                return true;

            return SourceHasCurrentTheoryConversion(packedSourcePath, convertedTheorySources);
        }
        catch
        {
            return false;
        }
    }

    private static bool PathIsUnderDirectory(string directory, string path)
    {
        try
        {
            string parent = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string child = Path.GetFullPath(path);
            return child.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                   child.StartsWith(parent + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool DirectoryDirectlyContainsLoadableTheoryPackage(string directory)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(directory) &&
                   Directory.Exists(directory) &&
                   !string.IsNullOrWhiteSpace(TheorySongLoader.FindPackageInDirectory(directory, requireLoadable: true));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPathInsideImportedCacheDirectory(string songsDirectory, string path, HashSet<string> importerCacheFolderNames, bool allowLegacyUnpackedCacheRoot = false)
    {
        if (string.IsNullOrWhiteSpace(songsDirectory) || string.IsNullOrWhiteSpace(path))
            return false;

        string directory = File.Exists(path) ? Path.GetDirectoryName(path) : path;
        string relativeDirectory = GetRelativePath(songsDirectory, directory);
        if (string.IsNullOrWhiteSpace(relativeDirectory) || string.Equals(relativeDirectory, ".", StringComparison.Ordinal))
            return false;

        string[] segments = relativeDirectory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (int i = 0; i < segments.Length; i++)
        {
            string segment = segments[i];
            if (PsarcCachedSongFormat.IsImportedFolderName(segment))
            {
                // Legacy unpacked psarc caches are themselves importable folder sources,
                // so the candidate folder itself may be allowed through; anything nested
                // inside one is still internal content.
                if (!allowLegacyUnpackedCacheRoot || i < segments.Length - 1)
                    return true;
            }
            else if (importerCacheFolderNames != null && importerCacheFolderNames.Contains(segment))
            {
                return true;
            }
        }

        // The chart editor's managed save folder is game-owned content; sources dropped
        // there are never proposed for library import (the registry symmetrically refuses
        // to write conversion output into it).
        return string.Equals(
            segments[0],
            ChartEditorProjectStore.ChartEditorSaveFolderName,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFullPathKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            return NormalizeSongDirectoryKey(Path.GetFullPath(path));
        }
        catch
        {
            return NormalizeSongDirectoryKey(path);
        }
    }

    private static long TryGetLastWriteUtcTicks(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return 0L;
            if (File.Exists(path))
                return File.GetLastWriteTimeUtc(path).Ticks;
            if (!Directory.Exists(path))
                return 0L;

            long result = Directory.GetLastWriteTimeUtc(path).Ticks;
            foreach (string filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (IsGeneratedTheoryPackageInDirectorySource(filePath))
                    continue;

                result = Math.Max(result, File.GetLastWriteTimeUtc(filePath).Ticks);
            }

            return result;
        }
        catch
        {
            return 0L;
        }
    }

    private static long TryGetFileSize(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return 0L;
            if (File.Exists(path))
                return new FileInfo(path).Length;
            if (!Directory.Exists(path))
                return 0L;

            long result = 0L;
            foreach (string filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (IsGeneratedTheoryPackageInDirectorySource(filePath))
                    continue;

                result += new FileInfo(filePath).Length;
            }

            return result;
        }
        catch
        {
            return 0L;
        }
    }

    private static bool IsGeneratedTheoryPackageInDirectorySource(string filePath)
    {
        return string.Equals(Path.GetExtension(filePath), TheoryPackageFormat.Extension, StringComparison.OrdinalIgnoreCase);
    }

    public static List<SongLibraryEntry> GetAvailableSongs(
        bool forceRefresh = false,
        bool refreshImports = false,
        Action<float, string> progress = null)
    {
        if (!forceRefresh)
        {
            if (sessionCacheLoaded)
                return CloneEntries(sessionCachedEntries);

            if (TryLoadCacheManifest(out List<SongLibraryEntry> cachedEntries))
            {
                ReplaceSessionCache(cachedEntries);
                return CloneEntries(sessionCachedEntries);
            }
        }

        List<SongLibraryEntry> rebuiltEntries = ScanSongsDirectory(refreshImports, progress);
        ReplaceSessionCache(rebuiltEntries);
        SaveCacheManifest(sessionCachedEntries);

        if (sessionCachedEntries.Count == 0)
            Debug.LogWarning($"[SongLibraryService] No valid song folders found in: {ExternalContentPaths.PersistentSongsDirectory}");

        return CloneEntries(sessionCachedEntries);
    }

    private static void ReplaceSessionCache(IReadOnlyList<SongLibraryEntry> entries)
    {
        sessionCachedEntries.Clear();
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                SongLibraryEntry entry = CloneEntry(entries[i]);
                if (entry != null)
                    sessionCachedEntries.Add(entry);
            }
        }

        sessionCacheLoaded = true;
    }

    private static void EnsureSessionCacheLoadedForUpdates()
    {
        if (sessionCacheLoaded)
            return;

        if (TryLoadCacheManifest(out List<SongLibraryEntry> cachedEntries))
            ReplaceSessionCache(cachedEntries);
    }

    private static List<SongLibraryEntry> CloneEntries(IReadOnlyList<SongLibraryEntry> entries)
    {
        List<SongLibraryEntry> clones = new List<SongLibraryEntry>();
        if (entries == null)
            return clones;

        for (int i = 0; i < entries.Count; i++)
        {
            SongLibraryEntry clone = CloneEntry(entries[i]);
            if (clone != null)
                clones.Add(clone);
        }

        return clones;
    }

    private static List<SongLibraryEntry> ScanSongsDirectory(bool refreshImports, Action<float, string> progress)
    {
        List<SongLibraryEntry> entries = new List<SongLibraryEntry>();

        if (!Directory.Exists(ExternalContentPaths.PersistentSongsDirectory))
        {
            Debug.LogWarning($"[SongLibraryService] Songs directory does not exist: {ExternalContentPaths.PersistentSongsDirectory}");
            return entries;
        }

        if (refreshImports)
            progress?.Invoke(5f, "Scanning songs...");

        List<string> songDirectories = DiscoverSongDirectories(ExternalContentPaths.PersistentSongsDirectory);

        if (songDirectories.Count == 0)
        {
            progress?.Invoke(100f, "Library refresh complete.");
            return entries;
        }

        for (int i = 0; i < songDirectories.Count; i++)
        {
            string songDirectory = songDirectories[i];
            List<SongLibraryEntry> discoveredEntries = BuildEntriesForDirectory(songDirectory);
            for (int entryIndex = 0; entryIndex < discoveredEntries.Count; entryIndex++)
            {
                SongLibraryEntry discovered = discoveredEntries[entryIndex];
                PopulateCachedLibrarySummary(discovered);
                entries.Add(discovered);
            }

            float scanRatio = (i + 1) / (float)songDirectories.Count;
            float baseProgress = 0f;
            float remainingProgress = 100f;
            progress?.Invoke(
                baseProgress + (scanRatio * remainingProgress),
                $"Scanning songs... {i + 1}/{songDirectories.Count}");
        }

        RemoveRawNotationEntriesCoveredByConversions(entries);

        progress?.Invoke(100f, "Library refresh complete.");
        return entries;
    }

    // A raw GP/MusicXML song whose notation was already converted to a .theory package
    // (anywhere in the library) is represented by that package, so the raw entry is
    // hidden from the library list. This uses the same identity checks as import
    // candidate suppression (source path + stamps, then content fingerprint), so
    // deleting the converted package or editing the raw source makes the raw entry
    // reappear on the next rescan — the library cache signature covers both events.
    private static void RemoveRawNotationEntriesCoveredByConversions(List<SongLibraryEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            return;

        bool hasRawNotationEntries = entries.Any(IsRawNotationLibraryEntry);
        if (!hasRawNotationEntries)
            return;

        ConvertedTheorySourceIndex convertedTheorySources = DiscoverConvertedTheorySourceStamps(ExternalContentPaths.PersistentSongsDirectory);
        if (convertedTheorySources.StampsBySourcePath.Count == 0 &&
            convertedTheorySources.StampFingerprints.Count == 0 &&
            convertedTheorySources.ContentFingerprints.Count == 0)
        {
            return;
        }

        entries.RemoveAll(entry =>
            IsRawNotationLibraryEntry(entry) &&
            SourceHasCurrentTheoryConversion(entry.PrimaryNotationPath, convertedTheorySources));
    }

    private static bool IsRawNotationLibraryEntry(SongLibraryEntry entry)
    {
        return entry != null &&
               entry.LibraryType == SongLibraryType.Guitar &&
               (entry.PrimaryNotationKind == SongNotationSourceKind.Gp5 ||
                entry.PrimaryNotationKind == SongNotationSourceKind.MusicXml) &&
               !string.IsNullOrWhiteSpace(entry.PrimaryNotationPath);
    }

    private static bool TryLoadCacheManifest(out List<SongLibraryEntry> entries)
    {
        entries = new List<SongLibraryEntry>();
        string cachePath = ExternalContentPaths.PersistentSongLibraryCachePath;
        if (string.IsNullOrWhiteSpace(cachePath) || !File.Exists(cachePath))
            return false;

        try
        {
            string json = File.ReadAllText(cachePath);
            SongLibraryCacheManifest manifest = JsonUtility.FromJson<SongLibraryCacheManifest>(json);
            if (manifest == null || manifest.version != SongLibraryCacheVersion || manifest.entries == null)
                return false;

            string currentSignature = BuildSongsDirectorySignature();
            if (!string.Equals(manifest.librarySignature ?? string.Empty, currentSignature, StringComparison.Ordinal))
                return false;

            entries = NormalizeCachedEntries(manifest.entries);
            if (entries.Count == 0 && SongsDirectoryHasAnySubdirectories())
                return false;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongLibraryService] Failed to load song library cache '{cachePath}': {ex.Message}");
            return false;
        }
    }

    private static bool SongsDirectoryHasAnySubdirectories()
    {
        string songsDirectory = ExternalContentPaths.PersistentSongsDirectory;
        return Directory.Exists(songsDirectory) &&
               Directory.GetDirectories(songsDirectory).Length > 0;
    }

    private static List<string> DiscoverSongDirectories(string songsDirectory)
    {
        List<string> discovered = new List<string>();
        if (string.IsNullOrWhiteSpace(songsDirectory) || !Directory.Exists(songsDirectory))
            return discovered;

        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (DirectoryContainsSongEntryPoint(songsDirectory))
        {
            string fullRootPath = Path.GetFullPath(songsDirectory);
            if (seen.Add(fullRootPath))
                discovered.Add(fullRootPath);
        }

        foreach (string directory in EnumerateChildDirectoriesSafe(songsDirectory))
            DiscoverSongDirectoriesRecursive(directory, depth: 1, discovered, seen);

        discovered.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));
        return discovered;
    }

    private static void DiscoverSongDirectoriesRecursive(
        string directory,
        int depth,
        List<string> discovered,
        HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(directory) ||
            discovered == null ||
            seen == null ||
            depth > MaxSongDirectoryDiscoveryDepth ||
            !Directory.Exists(directory) ||
            ShouldSkipLibraryDiscoveryDirectory(directory))
        {
            return;
        }

        if (DirectoryContainsSongEntryPoint(directory))
        {
            string fullPath = Path.GetFullPath(directory);
            if (seen.Add(fullPath))
                discovered.Add(fullPath);
            return;
        }

        foreach (string childDirectory in EnumerateChildDirectoriesSafe(directory))
            DiscoverSongDirectoriesRecursive(childDirectory, depth + 1, discovered, seen);
    }

    private static IEnumerable<string> EnumerateChildDirectoriesSafe(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return Enumerable.Empty<string>();

        try
        {
            return Directory.GetDirectories(directory)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongLibraryService] Failed to inspect song subfolders in '{directory}': {ex.Message}");
            return Enumerable.Empty<string>();
        }
    }

    private static bool ShouldSkipLibraryDiscoveryDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return true;

        // Legacy unpacked psarc caches and their content folders are conversion
        // sources, never playable song folders; stray notation files inside them
        // must not surface as bogus library entries. (Conversion candidate scanning
        // is unaffected — it enumerates directories independently.)
        string folderName = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (PsarcCachedSongFormat.IsImportedFolderName(folderName) ||
            string.Equals(folderName, PsarcCachedSongFormat.ContentDirectoryName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(folderName, PsarcCachedSongFormat.LegacyContentDirectoryName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            FileAttributes attributes = File.GetAttributes(directory);
            return (attributes & FileAttributes.System) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool DirectoryContainsSongEntryPoint(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return false;

        if (!string.IsNullOrWhiteSpace(TheorySongLoader.FindPackageInDirectory(directory, requireLoadable: true)))
            return true;

        if (!string.IsNullOrWhiteSpace(FindPreferredGpNotation(directory)))
            return true;

        if (!string.IsNullOrWhiteSpace(FindFirstFile(directory, "*.musicxml")) ||
            !string.IsNullOrWhiteSpace(FindFirstFile(directory, "*.xml")))
        {
            return true;
        }

        string arcadeChartPath = FindCloneHeroChartFile(directory);
        return !string.IsNullOrWhiteSpace(arcadeChartPath) &&
               File.Exists(Path.Combine(directory, "song.ini"));
    }

    private static void SaveCacheManifest(IReadOnlyList<SongLibraryEntry> entries)
    {
        string cachePath = ExternalContentPaths.PersistentSongLibraryCachePath;
        try
        {
            string directory = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            SongLibraryCacheManifest manifest = new SongLibraryCacheManifest
            {
                version = SongLibraryCacheVersion,
                generatedAtUtcTicks = DateTime.UtcNow.Ticks,
                librarySignature = BuildSongsDirectorySignature(),
                entries = CloneEntries(entries)
            };

            File.WriteAllText(cachePath, JsonUtility.ToJson(manifest, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongLibraryService] Failed to save song library cache '{cachePath}': {ex.Message}");
        }
    }

    private static List<SongLibraryEntry> NormalizeCachedEntries(IReadOnlyList<SongLibraryEntry> cachedEntries)
    {
        List<SongLibraryEntry> normalizedEntries = new List<SongLibraryEntry>();
        if (cachedEntries == null)
            return normalizedEntries;

        HashSet<string> seenSongKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < cachedEntries.Count; i++)
        {
            if (!TryNormalizeCachedEntry(cachedEntries[i], out SongLibraryEntry normalized))
                continue;

            string identityKey = BuildSongEntryIdentityKey(normalized);
            if (!seenSongKeys.Add(identityKey))
                continue;

            normalizedEntries.Add(normalized);
        }

        normalizedEntries.Sort((a, b) => string.Compare(BuildSongEntryIdentityKey(a), BuildSongEntryIdentityKey(b), StringComparison.OrdinalIgnoreCase));
        return normalizedEntries;
    }

    private static string BuildSongEntryIdentityKey(SongLibraryEntry entry)
    {
        if (entry == null)
            return string.Empty;

        string path = !string.IsNullOrWhiteSpace(entry.PrimaryNotationPath)
            ? entry.PrimaryNotationPath
            : !string.IsNullOrWhiteSpace(entry.ArcadeChartPath)
                ? entry.ArcadeChartPath
                : entry.SongDirectory;

        return NormalizeSongDirectoryKey(path);
    }

    private static bool TryNormalizeCachedEntry(SongLibraryEntry entry, out SongLibraryEntry normalized)
    {
        normalized = null;
        if (entry == null || string.IsNullOrWhiteSpace(entry.SongDirectory) || !Directory.Exists(entry.SongDirectory))
            return false;

        SongLibraryEntry clone = CloneEntry(entry);
        NormalizeCachedSummary(clone);
        clone.ArtworkPath = File.Exists(clone.ArtworkPath) ? clone.ArtworkPath : null;

        if (clone.LibraryType == SongLibraryType.Arcade)
        {
            if (string.IsNullOrWhiteSpace(clone.ArcadeChartPath) || !File.Exists(clone.ArcadeChartPath))
                return false;
            if (string.IsNullOrWhiteSpace(clone.ArcadeSongIniPath) || !File.Exists(clone.ArcadeSongIniPath))
                return false;
            if (string.IsNullOrWhiteSpace(clone.ArcadeDifficultySummary))
                clone.ArcadeDifficultySummary = BuildArcadeDifficultySummary(clone.ArcadeChartPath);

            List<string> audioPaths = new List<string>();
            if (clone.ArcadeAudioPaths != null)
            {
                HashSet<string> seenAudioPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < clone.ArcadeAudioPaths.Count; i++)
                {
                    string audioPath = clone.ArcadeAudioPaths[i];
                    if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
                        continue;

                    if (seenAudioPaths.Add(audioPath))
                        audioPaths.Add(audioPath);
                }
            }

            clone.ArcadeAudioPaths = audioPaths;
            if (string.IsNullOrWhiteSpace(clone.Mp3Path) || !File.Exists(clone.Mp3Path))
                clone.Mp3Path = audioPaths.FirstOrDefault();
            clone.PrimaryNotationPath = string.Empty;
            clone.PrimaryNotationKind = SongNotationSourceKind.None;
            clone.GpPath = null;
            clone.XmlPath = null;
            clone.MidiPath = File.Exists(clone.MidiPath) ? clone.MidiPath : null;
            normalized = clone;
            return true;
        }

        string resolvedNotationPath = clone.PrimaryNotationPath;
        if (string.IsNullOrWhiteSpace(resolvedNotationPath) || !File.Exists(resolvedNotationPath))
        {
            if (!string.IsNullOrWhiteSpace(clone.GpPath) && File.Exists(clone.GpPath))
                resolvedNotationPath = clone.GpPath;
            else if (!string.IsNullOrWhiteSpace(clone.XmlPath) && File.Exists(clone.XmlPath))
                resolvedNotationPath = clone.XmlPath;
            else
                return false;
        }

        SongNotationSourceKind resolvedNotationKind = clone.PrimaryNotationKind;
        if (resolvedNotationKind == SongNotationSourceKind.None &&
            !SongNotationFacade.TryDetectKind(resolvedNotationPath, out resolvedNotationKind))
            return false;

        clone.PrimaryNotationPath = resolvedNotationPath;
        clone.PrimaryNotationKind = resolvedNotationKind;
        clone.GpPath = !string.IsNullOrWhiteSpace(clone.GpPath) && File.Exists(clone.GpPath) ? clone.GpPath : null;
        clone.XmlPath = !string.IsNullOrWhiteSpace(clone.XmlPath) && File.Exists(clone.XmlPath) ? clone.XmlPath : null;
        clone.Mp3Path = !string.IsNullOrWhiteSpace(clone.Mp3Path) && File.Exists(clone.Mp3Path) ? clone.Mp3Path : null;
        clone.MidiPath = !string.IsNullOrWhiteSpace(clone.MidiPath) && File.Exists(clone.MidiPath) ? clone.MidiPath : null;

        if (resolvedNotationKind == SongNotationSourceKind.TheoryPackage &&
            TheorySongLoader.TryLoadManifest(clone.PrimaryNotationPath, out TheorySongManifest theoryManifest))
        {
            if ((string.IsNullOrWhiteSpace(clone.Mp3Path) || !File.Exists(clone.Mp3Path)) &&
                TheoryPackageCache.TryCachePrimaryAudio(clone.PrimaryNotationPath, theoryManifest, out string cachedAudioPath, out _))
            {
                clone.Mp3Path = cachedAudioPath;
            }

            if (!string.IsNullOrWhiteSpace(clone.Mp3Path) && File.Exists(clone.Mp3Path))
                TheoryPackageCache.TryCacheEmbeddedStems(clone.PrimaryNotationPath, theoryManifest, clone.Mp3Path, out _, out _);

            if ((string.IsNullOrWhiteSpace(clone.ArtworkPath) || !File.Exists(clone.ArtworkPath)) &&
                TheoryPackageCache.TryCacheCoverArt(clone.PrimaryNotationPath, theoryManifest, out string cachedCoverPath, out _))
            {
                clone.ArtworkPath = cachedCoverPath;
            }

            if (string.IsNullOrWhiteSpace(clone.DisplayName))
                clone.DisplayName = string.IsNullOrWhiteSpace(theoryManifest.title) ? Path.GetFileNameWithoutExtension(clone.PrimaryNotationPath) : theoryManifest.title.Trim();
            if (string.IsNullOrWhiteSpace(clone.Artist))
                clone.Artist = theoryManifest.artist ?? string.Empty;
            if (string.IsNullOrWhiteSpace(clone.Album))
                clone.Album = theoryManifest.album ?? string.Empty;
            if (string.IsNullOrWhiteSpace(clone.DifficultyDisplayLabel))
                clone.DifficultyDisplayLabel = BuildTheoryDifficultySummary(theoryManifest);
            if (clone.DurationSeconds <= 0.01f)
                clone.DurationSeconds = Mathf.Max(0f, theoryManifest.durationSeconds);

            normalized = clone;
            return true;
        }

        normalized = clone;
        return true;
    }

    private static string BuildTheoryDifficultySummary(TheorySongManifest manifest)
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

    private static string BuildSongsDirectorySignature()
    {
        string songsDirectory = ExternalContentPaths.PersistentSongsDirectory;
        if (string.IsNullOrWhiteSpace(songsDirectory) || !Directory.Exists(songsDirectory))
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        List<string> songDirectories = DiscoverSongDirectories(songsDirectory);

        for (int i = 0; i < songDirectories.Count; i++)
        {
            string directory = songDirectories[i];
            builder.Append("dir:");
            builder.Append(GetRelativePath(songsDirectory, directory).Replace('\\', '/'));
            builder.Append('|');

            AppendDirectorySignatureFiles(builder, directory);
        }

        return builder.ToString();
    }

    private static void AppendDirectorySignatureFiles(StringBuilder builder, string directory)
    {
        if (builder == null || string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return;

        try
        {
            string[] files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => !ShouldIgnoreLibrarySignatureFile(Path.GetFileName(path)))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            for (int i = 0; i < files.Length; i++)
                AppendFileSignature(builder, directory, files[i]);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongLibraryService] Failed to inspect song folder signature '{directory}': {ex.Message}");
        }

        string chartEditorDirectory = Path.Combine(directory, ChartEditorProjectStore.ChartEditorSaveFolderName);
        if (Directory.Exists(chartEditorDirectory))
            AppendLibrarySignatureFilesInDirectory(builder, directory, chartEditorDirectory);

        string legacyTheoryDirectory = Path.Combine(directory, LegacyTheoryPackageFolderName);
        if (Directory.Exists(legacyTheoryDirectory))
            AppendLibrarySignatureFilesInDirectory(builder, directory, legacyTheoryDirectory);

    }

    private static void AppendLibrarySignatureFilesInDirectory(StringBuilder builder, string rootDirectory, string directory)
    {
        if (builder == null || string.IsNullOrWhiteSpace(rootDirectory) || string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return;

        try
        {
            string[] files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => !ShouldIgnoreLibrarySignatureFile(Path.GetFileName(path)))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            for (int i = 0; i < files.Length; i++)
                AppendFileSignature(builder, rootDirectory, files[i]);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongLibraryService] Failed to inspect nested song folder signature '{directory}': {ex.Message}");
        }
    }

    private static void AppendFileSignature(StringBuilder builder, string directory, string filePath)
    {
        if (builder == null || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        try
        {
            FileInfo fileInfo = new FileInfo(filePath);
            string relativePath = GetRelativePath(directory, filePath);
            builder.Append(relativePath.Replace('\\', '/'));
            builder.Append(':');
            builder.Append(fileInfo.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(fileInfo.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture));
            builder.Append('|');
        }
        catch
        {
        }
    }

    private static string GetRelativePath(string rootPath, string filePath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(filePath))
            return Path.GetFileName(filePath) ?? string.Empty;

        try
        {
            return Path.GetRelativePath(rootPath, filePath);
        }
        catch
        {
            return Path.GetFileName(filePath) ?? string.Empty;
        }
    }

    private static bool ShouldIgnoreLibrarySignatureFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return true;

        if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            return true;

        return fileName.Equals(Path.GetFileName(ExternalContentPaths.PersistentSongLibraryCachePath), StringComparison.OrdinalIgnoreCase);
    }

    private static void PopulateCachedLibrarySummary(SongLibraryEntry entry)
    {
        if (entry == null)
            return;

        ApplyCachedMetadataSummary(entry, ReadCachedMetadataSummary(entry));
    }

    private static CachedMetadataSummaryValue ReadCachedMetadataSummary(SongLibraryEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.MetadataPath) || !File.Exists(entry.MetadataPath))
            return default;

        try
        {
            string json = File.ReadAllText(entry.MetadataPath);
            CachedSongMetadataPayload payload = JsonUtility.FromJson<CachedSongMetadataPayload>(json);
            if (payload == null)
                return default;

            int bestScoreValue = Mathf.Max(0, payload.bestScoreValue);
            float bestScorePercent = Mathf.Clamp(payload.bestScorePercent, 0f, 100f);
            if (payload.trackScores != null)
            {
                for (int i = 0; i < payload.trackScores.Count; i++)
                {
                    CachedTrackScorePayload trackScore = payload.trackScores[i];
                    if (trackScore == null)
                        continue;

                    bestScoreValue = Mathf.Max(bestScoreValue, Mathf.Max(0, trackScore.bestScoreValue));
                    bestScorePercent = Mathf.Max(bestScorePercent, Mathf.Clamp(trackScore.bestScorePercent, 0f, 100f));
                }
            }

            int bestArcadeScoreValue = Mathf.Max(0, payload.bestArcadeScoreValue);
            if (payload.arcadeScores != null)
            {
                for (int i = 0; i < payload.arcadeScores.Count; i++)
                {
                    CachedArcadeScorePayload arcadeScore = payload.arcadeScores[i];
                    if (arcadeScore == null)
                        continue;

                    bestArcadeScoreValue = Mathf.Max(bestArcadeScoreValue, Mathf.Max(0, arcadeScore.bestScoreValue));
                }
            }

            HeroSummaryValue heroSummary = default;
            if (payload.trackScores != null)
            {
                for (int i = 0; i < payload.trackScores.Count; i++)
                {
                    CachedTrackScorePayload trackScore = payload.trackScores[i];
                    if (trackScore == null)
                        continue;

                    if (ShouldReplaceHeroBest(
                            heroSummary.scoreValue,
                            heroSummary.percent,
                            heroSummary.heartsRemaining,
                            heroSummary.heartsTotal,
                            trackScore.heroBestScoreValue,
                            trackScore.heroBestScorePercent,
                            trackScore.heroBestHeartsRemaining,
                            trackScore.heroBestHeartsTotal))
                    {
                        heroSummary = new HeroSummaryValue(
                            trackScore.heroBestScoreValue,
                            trackScore.heroBestScorePercent,
                            trackScore.heroBestHeartsRemaining,
                            trackScore.heroBestHeartsTotal);
                    }
                }
            }

            return new CachedMetadataSummaryValue(
                payload.favoriteInLibrary,
                bestScoreValue,
                bestScorePercent,
                heroSummary.scoreValue,
                heroSummary.percent,
                heroSummary.heartsRemaining,
                heroSummary.heartsTotal,
                bestArcadeScoreValue);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongLibraryService] Failed to read metadata summary '{entry.MetadataPath}': {ex.Message}");
            return default;
        }
    }

    private static void ApplyCachedMetadataSummary(SongLibraryEntry entry, CachedMetadataSummaryValue summary)
    {
        ApplyCachedMetadataSummary(
            entry,
            summary.favoriteInLibrary,
            summary.bestScoreValue,
            summary.bestScorePercent,
            summary.heroBestScoreValue,
            summary.heroBestScorePercent,
            summary.heroBestHeartsRemaining,
            summary.heroBestHeartsTotal,
            summary.bestArcadeScoreValue);
    }

    private static void ApplyCachedMetadataSummary(
        SongLibraryEntry entry,
        bool favoriteInLibrary,
        int bestScoreValue,
        float bestScorePercent,
        int heroBestScoreValue,
        float heroBestScorePercent,
        int heroBestHeartsRemaining,
        int heroBestHeartsTotal,
        int bestArcadeScoreValue)
    {
        if (entry == null)
            return;

        entry.CachedFavoriteInLibrary = favoriteInLibrary;
        entry.CachedBestScoreValue = Mathf.Max(0, bestScoreValue);
        entry.CachedBestScorePercent = Mathf.Clamp(bestScorePercent, 0f, 100f);
        entry.CachedHeroBestScoreValue = Mathf.Max(0, heroBestScoreValue);
        entry.CachedHeroBestScorePercent = Mathf.Clamp(heroBestScorePercent, 0f, 100f);
        entry.CachedHeroBestHeartsRemaining = Mathf.Max(0, heroBestHeartsRemaining);
        entry.CachedHeroBestHeartsTotal = Mathf.Max(0, heroBestHeartsTotal);
        entry.CachedBestArcadeScoreValue = Mathf.Max(0, bestArcadeScoreValue);
    }

    private static void NormalizeCachedSummary(SongLibraryEntry entry)
    {
        if (entry == null)
            return;

        entry.CachedBestScoreValue = Mathf.Max(0, entry.CachedBestScoreValue);
        entry.CachedBestScorePercent = Mathf.Clamp(entry.CachedBestScorePercent, 0f, 100f);
        entry.CachedHeroBestScoreValue = Mathf.Max(0, entry.CachedHeroBestScoreValue);
        entry.CachedHeroBestScorePercent = Mathf.Clamp(entry.CachedHeroBestScorePercent, 0f, 100f);
        entry.CachedHeroBestHeartsRemaining = Mathf.Max(0, entry.CachedHeroBestHeartsRemaining);
        entry.CachedHeroBestHeartsTotal = Mathf.Max(0, entry.CachedHeroBestHeartsTotal);
        entry.CachedBestArcadeScoreValue = Mathf.Max(0, entry.CachedBestArcadeScoreValue);
    }

    private readonly struct HeroSummaryValue
    {
        public readonly int scoreValue;
        public readonly float percent;
        public readonly int heartsRemaining;
        public readonly int heartsTotal;

        public HeroSummaryValue(int scoreValue, float percent, int heartsRemaining, int heartsTotal)
        {
            this.scoreValue = Mathf.Max(0, scoreValue);
            this.percent = Mathf.Clamp(percent, 0f, 100f);
            this.heartsRemaining = Mathf.Max(0, heartsRemaining);
            this.heartsTotal = Mathf.Max(0, heartsTotal);
        }

        public bool IsAvailable => heartsTotal > 0;
    }

    private readonly struct CachedMetadataSummaryValue
    {
        public readonly bool favoriteInLibrary;
        public readonly int bestScoreValue;
        public readonly float bestScorePercent;
        public readonly int heroBestScoreValue;
        public readonly float heroBestScorePercent;
        public readonly int heroBestHeartsRemaining;
        public readonly int heroBestHeartsTotal;
        public readonly int bestArcadeScoreValue;

        public CachedMetadataSummaryValue(
            bool favoriteInLibrary,
            int bestScoreValue,
            float bestScorePercent,
            int heroBestScoreValue,
            float heroBestScorePercent,
            int heroBestHeartsRemaining,
            int heroBestHeartsTotal,
            int bestArcadeScoreValue)
        {
            this.favoriteInLibrary = favoriteInLibrary;
            this.bestScoreValue = Mathf.Max(0, bestScoreValue);
            this.bestScorePercent = Mathf.Clamp(bestScorePercent, 0f, 100f);
            this.heroBestScoreValue = Mathf.Max(0, heroBestScoreValue);
            this.heroBestScorePercent = Mathf.Clamp(heroBestScorePercent, 0f, 100f);
            this.heroBestHeartsRemaining = Mathf.Max(0, heroBestHeartsRemaining);
            this.heroBestHeartsTotal = Mathf.Max(0, heroBestHeartsTotal);
            this.bestArcadeScoreValue = Mathf.Max(0, bestArcadeScoreValue);
        }
    }

    private static bool ShouldReplaceHeroBest(
        int existingScoreValue,
        float existingPercent,
        int existingHeartsRemaining,
        int existingHeartsTotal,
        int candidateScoreValue,
        float candidatePercent,
        int candidateHeartsRemaining,
        int candidateHeartsTotal)
    {
        HeroSummaryValue existing = new HeroSummaryValue(existingScoreValue, existingPercent, existingHeartsRemaining, existingHeartsTotal);
        HeroSummaryValue candidate = new HeroSummaryValue(candidateScoreValue, candidatePercent, candidateHeartsRemaining, candidateHeartsTotal);
        if (!candidate.IsAvailable)
            return false;

        if (!existing.IsAvailable)
            return true;

        if (candidate.scoreValue > existing.scoreValue)
            return true;

        if (candidate.scoreValue < existing.scoreValue)
            return false;

        if (candidate.heartsTotal > 0 && existing.heartsTotal > 0)
        {
            if (candidate.heartsTotal < existing.heartsTotal)
                return true;

            if (candidate.heartsTotal > existing.heartsTotal)
                return false;
        }

        if (candidate.percent > existing.percent + 0.01f)
            return true;

        if (candidate.percent < existing.percent - 0.01f)
            return false;

        if (candidate.heartsRemaining > existing.heartsRemaining)
            return true;

        if (candidate.heartsRemaining < existing.heartsRemaining)
            return false;

        return false;
    }

    private static List<SongLibraryEntry> BuildEntriesForDirectory(string songDirectory)
    {
        List<SongLibraryEntry> entries = new List<SongLibraryEntry>();
        if (string.IsNullOrWhiteSpace(songDirectory) || !Directory.Exists(songDirectory))
            return entries;

        List<string> theoryPackagePaths = TheorySongLoader.FindPackagesInDirectory(songDirectory, requireLoadable: true);
        if (theoryPackagePaths.Count > 0)
        {
            for (int i = 0; i < theoryPackagePaths.Count; i++)
            {
                if (TryBuildEntry(songDirectory, out SongLibraryEntry theoryEntry, theoryPackagePaths[i]))
                    entries.Add(theoryEntry);
            }

            return entries;
        }

        if (TryBuildEntry(songDirectory, out SongLibraryEntry entry))
            entries.Add(entry);

        return entries;
    }

    private static bool TryBuildEntry(string songDirectory, out SongLibraryEntry entry, string preferredTheoryPackagePath = null)
    {
        entry = null;

        string mp3Path = FindPreferredAudioFile(songDirectory);
        string theoryPackagePath = !string.IsNullOrWhiteSpace(preferredTheoryPackagePath)
            ? preferredTheoryPackagePath
            : TheorySongLoader.FindPackageInDirectory(songDirectory, requireLoadable: true);
        string arcadeChartPath = FindCloneHeroChartFile(songDirectory);
        string arcadeSongIniPath = Path.Combine(songDirectory, "song.ini");
        bool hasArcadeSongIni = File.Exists(arcadeSongIniPath);
        List<string> arcadeAudioPaths = FindCloneHeroAudioFiles(songDirectory);
        string gpPath = FindPreferredGpNotation(songDirectory);
        string xmlPath = FindFirstFile(songDirectory, "*.musicxml") ?? FindFirstFile(songDirectory, "*.xml");
        string artworkPath = FindArtworkFile(songDirectory);
        string primaryNotationPath = !string.IsNullOrWhiteSpace(theoryPackagePath)
            ? theoryPackagePath
            : !string.IsNullOrWhiteSpace(gpPath)
                ? gpPath
                : xmlPath;
        SongNotationSourceKind primaryNotationKind = SongNotationSourceKind.None;
        if (!SongNotationFacade.TryDetectKind(primaryNotationPath, out primaryNotationKind))
            primaryNotationKind = SongNotationSourceKind.None;

        if (string.IsNullOrEmpty(primaryNotationPath) || primaryNotationKind == SongNotationSourceKind.None)
        {
            if (string.IsNullOrWhiteSpace(arcadeChartPath) || !hasArcadeSongIni)
            {
                Debug.LogWarning($"[SongLibraryService] Skipping invalid song folder '{songDirectory}'. Required files: a .theory package, supported Guitar Pro/MusicXML notation, or five-lane notes.chart/notes.mid plus song.ini.");
                return false;
            }

            CloneHeroFolderMetadata cloneHeroMetadata = TryReadCloneHeroFolderMetadata(arcadeSongIniPath);
            string arcadeDisplayName = ResolveCloneHeroDisplayName(songDirectory, cloneHeroMetadata);
            string arcadeArtist = string.IsNullOrWhiteSpace(cloneHeroMetadata?.artist) ? "Unknown Artist" : cloneHeroMetadata.artist.Trim();
            string arcadeAlbum = string.IsNullOrWhiteSpace(cloneHeroMetadata?.album) ? string.Empty : cloneHeroMetadata.album.Trim();
            string arcadeSubtitle = BuildCloneHeroSubtitle(arcadeArtist, cloneHeroMetadata);
            string arcadeMetadataPath = Path.Combine(songDirectory, ExternalContentPaths.SongMetadataFileName);

            entry = new SongLibraryEntry
            {
                SongId = Path.GetFileName(songDirectory),
                LibraryType = SongLibraryType.Arcade,
                DisplayName = arcadeDisplayName,
                Artist = arcadeArtist,
                Album = arcadeAlbum,
                Subtitle = arcadeSubtitle,
                ArtworkPath = artworkPath,
                DifficultyRating = cloneHeroMetadata != null ? Mathf.Clamp(cloneHeroMetadata.difficulty, 0, 5) : 0,
                SongDirectory = songDirectory,
                Mp3Path = arcadeAudioPaths.FirstOrDefault() ?? mp3Path,
                PrimaryNotationPath = string.Empty,
                PrimaryNotationKind = SongNotationSourceKind.None,
                GpPath = null,
                XmlPath = null,
                MetadataPath = arcadeMetadataPath,
                DurationSeconds = ResolveArcadeSongDurationSeconds(cloneHeroMetadata, arcadeChartPath),
                MidiPath = Path.GetExtension(arcadeChartPath).Equals(".mid", StringComparison.OrdinalIgnoreCase) ||
                           Path.GetExtension(arcadeChartPath).Equals(".midi", StringComparison.OrdinalIgnoreCase)
                    ? arcadeChartPath
                    : null,
                ArcadeChartPath = arcadeChartPath,
                ArcadeSongIniPath = arcadeSongIniPath,
                ArcadeDifficultySummary = BuildArcadeDifficultySummary(arcadeChartPath),
                ArcadeAudioPaths = arcadeAudioPaths
            };

            return true;
        }

        if (primaryNotationKind == SongNotationSourceKind.TheoryPackage &&
            TheorySongLoader.TryLoadManifest(primaryNotationPath, out TheorySongManifest theoryManifest))
        {
            string theoryMetadataPath = BuildTheoryMetadataPath(primaryNotationPath, songDirectory);
            string cachedAudioPath = mp3Path;
            if (TheoryPackageCache.TryCachePrimaryAudio(primaryNotationPath, theoryManifest, out string extractedAudioPath, out string audioCacheError))
                cachedAudioPath = extractedAudioPath;
            else if (!string.IsNullOrWhiteSpace(audioCacheError))
                Debug.LogWarning($"[SongLibraryService] Could not cache .theory audio for '{primaryNotationPath}': {audioCacheError}");

            if (!string.IsNullOrWhiteSpace(cachedAudioPath) && File.Exists(cachedAudioPath))
                TheoryPackageCache.TryCacheEmbeddedStems(primaryNotationPath, theoryManifest, cachedAudioPath, out _, out _);

            string cachedCoverPath = artworkPath;
            if (TheoryPackageCache.TryCacheCoverArt(primaryNotationPath, theoryManifest, out string extractedCoverPath, out _))
                cachedCoverPath = extractedCoverPath;

            entry = new SongLibraryEntry
            {
                SongId = Path.GetFileNameWithoutExtension(primaryNotationPath),
                LibraryType = SongLibraryType.Guitar,
                DisplayName = string.IsNullOrWhiteSpace(theoryManifest.title) ? Path.GetFileNameWithoutExtension(primaryNotationPath) : theoryManifest.title.Trim(),
                Artist = string.IsNullOrWhiteSpace(theoryManifest.artist) ? string.Empty : theoryManifest.artist.Trim(),
                Album = string.IsNullOrWhiteSpace(theoryManifest.album) ? string.Empty : theoryManifest.album.Trim(),
                Subtitle = BuildSubtitleDisplay(theoryManifest.artist, theoryManifest.subtitle),
                ArtworkPath = cachedCoverPath,
                DifficultyRating = Mathf.Clamp(theoryManifest.difficultyRating, 0, 5),
                DifficultyDisplayLabel = BuildTheoryDifficultySummary(theoryManifest),
                SongDirectory = songDirectory,
                Mp3Path = !string.IsNullOrWhiteSpace(cachedAudioPath) && File.Exists(cachedAudioPath) ? cachedAudioPath : null,
                PrimaryNotationPath = primaryNotationPath,
                PrimaryNotationKind = primaryNotationKind,
                GpPath = null,
                XmlPath = null,
                MetadataPath = theoryMetadataPath,
                DurationSeconds = Mathf.Max(0f, theoryManifest.durationSeconds),
                MidiPath = null
            };

            return true;
        }

        string metadataPath = Path.Combine(songDirectory, ExternalContentPaths.SongMetadataFileName);
        string definitionPath = Path.Combine(songDirectory, SongDefinitionFileName);
        SongFolderMetadata importedDefinition = TryReadSongFolderMetadata(definitionPath) ?? TryReadSongFolderMetadata(metadataPath);
        string displayName = ResolveDisplayName(songDirectory, primaryNotationPath, primaryNotationKind, importedDefinition, xmlPath);
        string artist = ResolveArtist(primaryNotationPath, primaryNotationKind, importedDefinition, xmlPath);
        string album = ResolveAlbum(importedDefinition);
        string subtitle = BuildSubtitleDisplay(artist, importedDefinition?.subtitle);

        entry = new SongLibraryEntry
        {
            SongId = Path.GetFileName(songDirectory),
            LibraryType = SongLibraryType.Guitar,
            DisplayName = displayName,
            Artist = artist,
            Album = album,
            Subtitle = subtitle,
            ArtworkPath = artworkPath,
            DifficultyRating = importedDefinition != null ? Mathf.Clamp(importedDefinition.difficulty, 0, 5) : 0,
            SongDirectory = songDirectory,
            Mp3Path = mp3Path,
            PrimaryNotationPath = primaryNotationPath,
            PrimaryNotationKind = primaryNotationKind,
            GpPath = gpPath,
            XmlPath = xmlPath,
            MetadataPath = metadataPath,
            DurationSeconds = ResolveGuitarSongDurationSeconds(primaryNotationPath, primaryNotationKind),
            MidiPath = FindFirstFile(songDirectory, "*.mid") ?? FindFirstFile(songDirectory, "*.midi")
        };

        return true;
    }

    private static string BuildTheoryMetadataPath(string packagePath, string fallbackDirectory)
    {
        string packageDirectory = string.IsNullOrWhiteSpace(packagePath) ? string.Empty : Path.GetDirectoryName(packagePath);
        string packageName = string.IsNullOrWhiteSpace(packagePath) ? string.Empty : Path.GetFileNameWithoutExtension(packagePath);
        if (!string.IsNullOrWhiteSpace(packageDirectory) && !string.IsNullOrWhiteSpace(packageName))
            return Path.Combine(packageDirectory, $"{packageName}.metadata.json");

        return Path.Combine(fallbackDirectory, ExternalContentPaths.SongMetadataFileName);
    }

    private static string FindFirstFile(string directory, string pattern)
    {
        if (!Directory.Exists(directory))
            return null;

        return Directory.GetFiles(directory, pattern)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static float ResolveArcadeSongDurationSeconds(CloneHeroFolderMetadata metadata, string chartPath)
    {
        if (metadata != null && metadata.songLengthMilliseconds > 0)
            return metadata.songLengthMilliseconds / 1000f;

        if (string.IsNullOrWhiteSpace(chartPath) || !File.Exists(chartPath))
            return 0f;

        try
        {
            List<ArcadeArrangementSummary> summaries = ArcadeCloneHeroLoader.GetArrangementSummaries(chartPath);
            ArcadeArrangementSummary summary = summaries.FirstOrDefault();
            if (summary == null)
                return 0f;

            ArcadeDifficulty difficulty = ArcadeCloneHeroLoader.GetBestDefaultDifficulty(summary.Difficulties);
            ArcadeChartData chartData = ArcadeCloneHeroLoader.Load(chartPath, summary.ArrangementId, difficulty);
            return Mathf.Max(0f, chartData?.DurationSeconds ?? 0f);
        }
        catch
        {
            return 0f;
        }
    }

    private static float ResolveGuitarSongDurationSeconds(string notationPath, SongNotationSourceKind notationKind)
    {
        if (string.IsNullOrWhiteSpace(notationPath) || notationKind == SongNotationSourceKind.None || !File.Exists(notationPath))
            return 0f;

        try
        {
            GeneratedPlaybackArrangement arrangement = SongNotationFacade.LoadGeneratedArrangement(notationPath, notationKind);
            if (arrangement != null && arrangement.durationSeconds > 0.01f)
                return arrangement.durationSeconds;
        }
        catch
        {
        }

        try
        {
            List<NoteData> notes = SongNotationFacade.LoadSong(notationPath, notationKind);
            if (notes != null && notes.Count > 0)
                return Mathf.Max(0f, notes.Max(note => note.time + Mathf.Max(0.05f, note.duration)));
        }
        catch
        {
        }

        return 0f;
    }

    private static string FindCloneHeroChartFile(string directory)
    {
        if (!Directory.Exists(directory))
            return null;

        string[] preferredNames =
        {
            "notes.chart",
            "notes.mid",
            "notes.midi"
        };

        for (int i = 0; i < preferredNames.Length; i++)
        {
            string candidate = Path.Combine(directory, preferredNames[i]);
            if (File.Exists(candidate))
                return candidate;
        }

        return FindFirstFile(directory, "*.chart") ?? FindFirstFile(directory, "*.mid") ?? FindFirstFile(directory, "*.midi");
    }

    private static List<string> FindCloneHeroAudioFiles(string directory)
    {
        List<string> results = new List<string>();
        if (!Directory.Exists(directory))
            return results;

        string[] preferredStems =
        {
            "song",
            "guitar",
            "rhythm",
            "bass",
            "drums",
            "drums_1",
            "drums_2",
            "drums_3",
            "drums_4",
            "keys",
            "crowd"
        };

        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int stemIndex = 0; stemIndex < preferredStems.Length; stemIndex++)
        {
            for (int extensionIndex = 0; extensionIndex < SupportedAudioExtensions.Length; extensionIndex++)
            {
                string candidate = Path.Combine(directory, preferredStems[stemIndex] + SupportedAudioExtensions[extensionIndex]);
                if (File.Exists(candidate) && seen.Add(candidate))
                    results.Add(candidate);
            }
        }

        for (int patternIndex = 0; patternIndex < SupportedAudioPatterns.Length; patternIndex++)
        {
            foreach (string candidate in Directory.GetFiles(directory, SupportedAudioPatterns[patternIndex]).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string fileName = Path.GetFileName(candidate);
                if (IsCloneHeroIgnoredMediaFile(fileName))
                    continue;

                if (seen.Add(candidate))
                    results.Add(candidate);
            }
        }

        return results;
    }

    private static string FindPreferredAudioFile(string directory)
    {
        if (!Directory.Exists(directory))
            return null;

        string[] preferredStems =
        {
            "song",
            "audio",
            "music",
            "backing",
            "guitar"
        };

        for (int stemIndex = 0; stemIndex < preferredStems.Length; stemIndex++)
        {
            for (int extensionIndex = 0; extensionIndex < PrimaryAudioExtensions.Length; extensionIndex++)
            {
                string candidate = Path.Combine(directory, preferredStems[stemIndex] + PrimaryAudioExtensions[extensionIndex]);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        for (int patternIndex = 0; patternIndex < PrimaryAudioPatterns.Length; patternIndex++)
        {
            string candidate = Directory.GetFiles(directory, PrimaryAudioPatterns[patternIndex])
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsCloneHeroIgnoredMediaFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        string lower = fileName.ToLowerInvariant();
        return lower.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
               lower.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ||
               lower.EndsWith(".avi", StringComparison.OrdinalIgnoreCase) ||
               lower.EndsWith(".mov", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindPreferredGpNotation(string directory)
    {
        if (!Directory.Exists(directory))
            return null;

        string[] patterns =
        {
            "*.gp8",
            "*.gp5",
            "*.gp4",
            "*.gp3",
            "*.gpx",
            "*.gp"
        };

        for (int i = 0; i < patterns.Length; i++)
        {
            string candidate = FindFirstFile(directory, patterns[i]);
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            if (SongNotationFacade.TryDetectKind(candidate, out SongNotationSourceKind detectedKind) &&
                detectedKind != SongNotationSourceKind.None)
            {
                return candidate;
            }
        }

        return null;
    }

    private static string FindArtworkFile(string directory)
    {
        if (!Directory.Exists(directory))
            return null;

        string[] preferredNames =
        {
            "cover.jpg",
            "cover.jpeg",
            "cover.png",
            "folder.jpg",
            "folder.jpeg",
            "folder.png",
            "artwork.jpg",
            "artwork.jpeg",
            "artwork.png"
        };

        for (int i = 0; i < preferredNames.Length; i++)
        {
            string candidate = Path.Combine(directory, preferredNames[i]);
            if (File.Exists(candidate))
                return candidate;
        }

        string[] patterns = { "*.jpg", "*.jpeg", "*.png" };
        for (int i = 0; i < patterns.Length; i++)
        {
            string candidate = FindFirstFile(directory, patterns[i]);
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return null;
    }

    private static string BuildArcadeDifficultySummary(string chartPath)
    {
        if (string.IsNullOrWhiteSpace(chartPath) || !File.Exists(chartPath))
            return string.Empty;

        List<ArcadeArrangementSummary> arrangements = ArcadeCloneHeroLoader.GetArrangementSummaries(chartPath);
        if (arrangements == null || arrangements.Count == 0)
            return string.Empty;

        HashSet<ArcadeDifficulty> available = new HashSet<ArcadeDifficulty>();
        for (int i = 0; i < arrangements.Count; i++)
        {
            List<ArcadeDifficulty> difficulties = arrangements[i]?.Difficulties;
            if (difficulties == null)
                continue;

            for (int j = 0; j < difficulties.Count; j++)
                available.Add(difficulties[j]);
        }

        if (available.Count == 0)
            return string.Empty;

        List<ArcadeDifficulty> ordered = new List<ArcadeDifficulty>
        {
            ArcadeDifficulty.Expert,
            ArcadeDifficulty.Hard,
            ArcadeDifficulty.Medium,
            ArcadeDifficulty.Easy
        };

        return string.Concat(ordered
            .Where(available.Contains)
            .Select(ArcadeCloneHeroLoader.GetDifficultyLabel));
    }

    private static string ResolveDisplayName(string songDirectory, string notationPath, SongNotationSourceKind notationKind, SongFolderMetadata importedDefinition, string xmlFallbackPath)
    {
        string fallbackName = Path.GetFileName(songDirectory);

        if (!string.IsNullOrWhiteSpace(importedDefinition?.displayName))
            return importedDefinition.displayName.Trim();

        string notationName = SongNotationFacade.TryReadDisplayName(notationPath, notationKind);
        if (!string.IsNullOrWhiteSpace(notationName))
            return notationName.Trim();

        string xmlName = TryReadDisplayNameFromXml(xmlFallbackPath);
        if (!string.IsNullOrWhiteSpace(xmlName))
            return xmlName.Trim();

        return fallbackName;
    }

    private static SongFolderMetadata TryReadSongFolderMetadata(string metadataPath)
    {
        if (string.IsNullOrEmpty(metadataPath) || !File.Exists(metadataPath))
            return null;

        try
        {
            string json = File.ReadAllText(metadataPath);
            return JsonUtility.FromJson<SongFolderMetadata>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongLibraryService] Failed to parse metadata '{metadataPath}': {ex.Message}");
            return null;
        }
    }

    private static CloneHeroFolderMetadata TryReadCloneHeroFolderMetadata(string iniPath)
    {
        if (string.IsNullOrEmpty(iniPath) || !File.Exists(iniPath))
            return null;

        CloneHeroFolderMetadata metadata = new CloneHeroFolderMetadata();
        try
        {
            foreach (string rawLine in File.ReadAllLines(iniPath))
            {
                string line = rawLine?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                int equals = line.IndexOf('=');
                if (equals <= 0)
                    continue;

                string key = line.Substring(0, equals).Trim().ToLowerInvariant();
                string value = line.Substring(equals + 1).Trim().Trim('"');
                switch (key)
                {
                    case "name":
                        metadata.name = value;
                        break;
                    case "artist":
                        metadata.artist = value;
                        break;
                    case "album":
                        metadata.album = value;
                        break;
                    case "charter":
                        metadata.charter = value;
                        break;
                    case "year":
                        metadata.year = value;
                        break;
                    case "diff_guitar":
                    case "diff_band":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedDifficulty))
                            metadata.difficulty = Mathf.Clamp(parsedDifficulty, 0, 5);
                        break;
                    case "song_length":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSongLength))
                            metadata.songLengthMilliseconds = Mathf.Max(0, parsedSongLength);
                        break;
                }
            }

            return metadata;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongLibraryService] Failed to parse rhythm song.ini '{iniPath}': {ex.Message}");
            return null;
        }
    }

    private static string ResolveCloneHeroDisplayName(string songDirectory, CloneHeroFolderMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata?.name))
            return metadata.name.Trim();

        return Path.GetFileName(songDirectory);
    }

    private static string BuildCloneHeroSubtitle(string artist, CloneHeroFolderMetadata metadata)
    {
        List<string> parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(artist))
            parts.Add(artist.Trim());
        if (!string.IsNullOrWhiteSpace(metadata?.charter))
            parts.Add($"Chart by {metadata.charter.Trim()}");

        return string.Join("  |  ", parts);
    }

    private static string ResolveArtist(string notationPath, SongNotationSourceKind notationKind, SongFolderMetadata importedDefinition, string xmlFallbackPath)
    {
        if (!string.IsNullOrWhiteSpace(importedDefinition?.artist))
            return importedDefinition.artist.Trim();

        if (!string.IsNullOrWhiteSpace(importedDefinition?.subtitle))
            return importedDefinition.subtitle.Trim();

        string notationCreator = SongNotationFacade.TryReadCreator(notationPath, notationKind);
        if (!string.IsNullOrWhiteSpace(notationCreator))
            return notationCreator.Trim();

        string xmlCreator = TryReadCreatorFromXml(xmlFallbackPath);
        return string.IsNullOrWhiteSpace(xmlCreator) ? string.Empty : xmlCreator.Trim();
    }

    private static string ResolveAlbum(SongFolderMetadata importedDefinition)
    {
        if (!string.IsNullOrWhiteSpace(importedDefinition?.album))
            return importedDefinition.album.Trim();

        return string.Empty;
    }

    private static string BuildSubtitleDisplay(string artist, string legacySubtitle)
    {
        if (!string.IsNullOrWhiteSpace(artist))
            return artist.Trim();

        if (!string.IsNullOrWhiteSpace(legacySubtitle))
            return legacySubtitle.Trim();

        return string.Empty;
    }

    internal static string TryReadDisplayNameFromXml(string xmlPath)
    {
        if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
            return null;

        try
        {
            XmlDocument xml = new XmlDocument();
            xml.Load(xmlPath);
            string[] candidates = { "//work-title", "//movement-title", "//credit-words" };

            for (int i = 0; i < candidates.Length; i++)
            {
                XmlNode node = xml.SelectSingleNode(candidates[i]);
                if (node != null && !string.IsNullOrWhiteSpace(node.InnerText))
                    return node.InnerText;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongLibraryService] Failed to read display name f