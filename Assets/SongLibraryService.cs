using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

public static class SongLibraryService
{
    private const string SongDefinitionFileName = "song.json";
    private const int SongLibraryCacheVersion = 9;

    [Serializable]
    private sealed class SongLibraryCacheManifest
    {
        public int version = SongLibraryCacheVersion;
        public long generatedAtUtcTicks;
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

    public static void UpdateCachedMetadataSummary(
        string songDirectory,
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
        EnsureSessionCacheLoadedForUpdates();

        bool updated = false;
        for (int i = 0; i < sessionCachedEntries.Count; i++)
        {
            SongLibraryEntry entry = sessionCachedEntries[i];
            if (entry == null || !string.Equals(NormalizeSongDirectoryKey(entry.SongDirectory), normalizedSongDirectory, StringComparison.OrdinalIgnoreCase))
                continue;

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

    public static List<SongLibraryEntry> GetAvailableSongs(bool forceRefresh = false)
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

        List<SongLibraryEntry> rebuiltEntries = ScanSongsDirectory();
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

    private static List<SongLibraryEntry> ScanSongsDirectory()
    {
        List<SongLibraryEntry> entries = new List<SongLibraryEntry>();

        if (!Directory.Exists(ExternalContentPaths.PersistentSongsDirectory))
        {
            Debug.LogWarning($"[SongLibraryService] Songs directory does not exist: {ExternalContentPaths.PersistentSongsDirectory}");
            return entries;
        }

        ArrangementCacheImportService.RefreshImports();

        string[] songDirectories = Directory.GetDirectories(ExternalContentPaths.PersistentSongsDirectory)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (int i = 0; i < songDirectories.Length; i++)
        {
            string songDirectory = songDirectories[i];
            if (TryBuildEntry(songDirectory, out SongLibraryEntry discovered))
            {
                PopulateCachedLibrarySummary(discovered);
                entries.Add(discovered);
            }
        }

        return entries;
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

        HashSet<string> seenSongDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < cachedEntries.Count; i++)
        {
            if (!TryNormalizeCachedEntry(cachedEntries[i], out SongLibraryEntry normalized))
                continue;

            if (!seenSongDirectories.Add(normalized.SongDirectory ?? string.Empty))
                continue;

            normalizedEntries.Add(normalized);
        }

        normalizedEntries.Sort((a, b) => string.Compare(a?.SongDirectory ?? string.Empty, b?.SongDirectory ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        return normalizedEntries;
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

        if (resolvedNotationKind == SongNotationSourceKind.ArrangementCache &&
            ArrangementCacheSongLoader.TryLoadManifest(clone.PrimaryNotationPath, out var arrangementCacheManifest))
        {
            if ((string.IsNullOrWhiteSpace(clone.Mp3Path) || !File.Exists(clone.Mp3Path)) &&
                !string.IsNullOrWhiteSpace(arrangementCacheManifest.audioPath) &&
                File.Exists(arrangementCacheManifest.audioPath))
            {
                clone.Mp3Path = arrangementCacheManifest.audioPath;
            }

            if ((string.IsNullOrWhiteSpace(clone.ArtworkPath) || !File.Exists(clone.ArtworkPath)) &&
                !string.IsNullOrWhiteSpace(arrangementCacheManifest.artworkPath) &&
                File.Exists(arrangementCacheManifest.artworkPath))
            {
                clone.ArtworkPath = arrangementCacheManifest.artworkPath;
            }

            if (string.IsNullOrWhiteSpace(clone.DifficultyDisplayLabel))
                clone.DifficultyDisplayLabel = ArrangementCacheMetadata.BuildDifficultySummary(arrangementCacheManifest);

            if (clone.DurationSeconds <= 0f)
                clone.DurationSeconds = Mathf.Max(0f, arrangementCacheManifest.durationSeconds);
        }

        normalized = clone;
        return true;
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

    private static bool TryBuildEntry(string songDirectory, out SongLibraryEntry entry)
    {
        entry = null;

        string mp3Path = FindFirstFile(songDirectory, "*.mp3")
                         ?? FindFirstFile(songDirectory, "*.wav")
                         ?? FindFirstFile(songDirectory, "*.ogg");
        string arrangementCacheManifestPath = ArrangementCacheSongLoader.FindManifestInDirectory(songDirectory);
        string arcadeChartPath = FindCloneHeroChartFile(songDirectory);
        string arcadeSongIniPath = Path.Combine(songDirectory, "song.ini");
        bool hasArcadeSongIni = File.Exists(arcadeSongIniPath);
        List<string> arcadeAudioPaths = FindCloneHeroAudioFiles(songDirectory);
        string gpPath = FindPreferredGpNotation(songDirectory);
        string xmlPath = FindFirstFile(songDirectory, "*.musicxml") ?? FindFirstFile(songDirectory, "*.xml");
        string artworkPath = FindArtworkFile(songDirectory);
        string primaryNotationPath = !string.IsNullOrWhiteSpace(arrangementCacheManifestPath)
            ? arrangementCacheManifestPath
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
                Debug.LogWarning($"[SongLibraryService] Skipping invalid song folder '{songDirectory}'. Required files: an extracted arrangement manifest, supported Guitar Pro/MusicXML notation, or Clone Hero notes.chart/notes.mid plus song.ini.");
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

        if (primaryNotationKind == SongNotationSourceKind.ArrangementCache &&
            ArrangementCacheSongLoader.TryLoadManifest(primaryNotationPath, out var arrangementCacheManifest))
        {
            string arrangementCacheMetadataPath = Path.Combine(songDirectory, ExternalContentPaths.SongMetadataFileName);
            string arrangementDifficultySummary = ArrangementCacheMetadata.BuildDifficultySummary(arrangementCacheManifest);
            entry = new SongLibraryEntry
            {
                SongId = Path.GetFileName(songDirectory),
                LibraryType = SongLibraryType.Guitar,
                DisplayName = string.IsNullOrWhiteSpace(arrangementCacheManifest.displayName) ? Path.GetFileName(songDirectory) : arrangementCacheManifest.displayName.Trim(),
                Artist = string.IsNullOrWhiteSpace(arrangementCacheManifest.artist) ? string.Empty : arrangementCacheManifest.artist.Trim(),
                Album = string.IsNullOrWhiteSpace(arrangementCacheManifest.album) ? string.Empty : arrangementCacheManifest.album.Trim(),
                Subtitle = BuildSubtitleDisplay(arrangementCacheManifest.artist, arrangementCacheManifest.subtitle),
                ArtworkPath = File.Exists(arrangementCacheManifest.artworkPath) ? arrangementCacheManifest.artworkPath : artworkPath,
                DifficultyRating = Mathf.Clamp(arrangementCacheManifest.difficultyRating, 0, 5),
                DifficultyDisplayLabel = arrangementDifficultySummary,
                SongDirectory = songDirectory,
                Mp3Path = !string.IsNullOrWhiteSpace(arrangementCacheManifest.audioPath) && File.Exists(arrangementCacheManifest.audioPath) ? arrangementCacheManifest.audioPath : mp3Path,
                PrimaryNotationPath = primaryNotationPath,
                PrimaryNotationKind = primaryNotationKind,
                GpPath = null,
                XmlPath = null,
                MetadataPath = arrangementCacheMetadataPath,
                DurationSeconds = Mathf.Max(0f, arrangementCacheManifest.durationSeconds),
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

        string[] preferredNames =
        {
            "song.ogg",
            "guitar.ogg",
            "rhythm.ogg",
            "bass.ogg",
            "drums.ogg",
            "drums_1.ogg",
            "drums_2.ogg",
            "drums_3.ogg",
            "drums_4.ogg",
            "keys.ogg",
            "crowd.ogg",
            "song.mp3",
            "guitar.mp3",
            "rhythm.mp3",
            "bass.mp3",
            "drums.mp3",
            "keys.mp3",
            "song.wav",
            "guitar.wav",
            "rhythm.wav",
            "bass.wav",
            "drums.wav",
            "keys.wav"
        };

        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < preferredNames.Length; i++)
        {
            string candidate = Path.Combine(directory, preferredNames[i]);
            if (File.Exists(candidate) && seen.Add(candidate))
                results.Add(candidate);
        }

        string[] patterns = { "*.ogg", "*.mp3", "*.wav" };
        for (int patternIndex = 0; patternIndex < patterns.Length; patternIndex++)
        {
            foreach (string candidate in Directory.GetFiles(directory, patterns[patternIndex]).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
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
            Debug.LogWarning($"[SongLibraryService] Failed to parse Clone Hero song.ini '{iniPath}': {ex.Message}");
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
            Debug.LogWarning($"[SongLibraryService] Failed to read display name from XML '{xmlPath}': {ex.Message}");
        }

        return null;
    }

    internal static string TryReadCreatorFromXml(string xmlPath)
    {
        if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
            return null;

        try
        {
            XmlDocument xml = new XmlDocument();
            xml.Load(xmlPath);

            XmlNode creatorNode = xml.SelectSingleNode("//identification/creator[@type='composer']")
                ?? xml.SelectSingleNode("//identification/creator")
                ?? xml.SelectSingleNode("//creator");

            if (creatorNode != null && !string.IsNullOrWhiteSpace(creatorNode.InnerText))
                return creatorNode.InnerText.Trim();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongLibraryService] Failed to read creator from XML '{xmlPath}': {ex.Message}");
        }

        return null;
    }

    private static SongLibraryEntry CloneEntry(SongLibraryEntry entry)
    {
        if (entry == null)
            return null;

        return new SongLibraryEntry
        {
            SongId = entry.SongId,
            LibraryType = entry.LibraryType,
            DisplayName = entry.DisplayName,
            Artist = entry.Artist,
            Album = entry.Album,
            Subtitle = entry.Subtitle,
            ArtworkPath = entry.ArtworkPath,
            DifficultyRating = entry.DifficultyRating,
            DifficultyDisplayLabel = entry.DifficultyDisplayLabel,
            SongDirectory = entry.SongDirectory,
            Mp3Path = entry.Mp3Path,
            PrimaryNotationPath = entry.PrimaryNotationPath,
            PrimaryNotationKind = entry.PrimaryNotationKind,
            GpPath = entry.GpPath,
            XmlPath = entry.XmlPath,
            MetadataPath = entry.MetadataPath,
            DurationSeconds = entry.DurationSeconds,
            MidiPath = entry.MidiPath,
            ArcadeChartPath = entry.ArcadeChartPath,
            ArcadeSongIniPath = entry.ArcadeSongIniPath,
            ArcadeDifficultySummary = entry.ArcadeDifficultySummary,
            ArcadeAudioPaths = entry.ArcadeAudioPaths != null ? new List<string>(entry.ArcadeAudioPaths) : new List<string>(),
            CachedFavoriteInLibrary = entry.CachedFavoriteInLibrary,
            CachedBestScoreValue = entry.CachedBestScoreValue,
            CachedBestScorePercent = entry.CachedBestScorePercent,
            CachedHeroBestScoreValue = entry.CachedHeroBestScoreValue,
            CachedHeroBestScorePercent = entry.CachedHeroBestScorePercent,
            CachedHeroBestHeartsRemaining = entry.CachedHeroBestHeartsRemaining,
            CachedHeroBestHeartsTotal = entry.CachedHeroBestHeartsTotal,
            CachedBestArcadeScoreValue = entry.CachedBestArcadeScoreValue
        };
    }
}
