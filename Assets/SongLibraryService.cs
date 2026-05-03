using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEngine;

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
    public string SongDirectory;
    public string Mp3Path;
    public string PrimaryNotationPath;
    public SongNotationSourceKind PrimaryNotationKind;
    public string GpPath;
    public string XmlPath;
    public string MetadataPath;
    public string MidiPath;
    public string ArcadeChartPath;
    public string ArcadeSongIniPath;
    public List<string> ArcadeAudioPaths = new List<string>();
}

public static class SongLibraryService
{
    private const string SongDefinitionFileName = "song.json";

    private sealed class CachedSongEntry
    {
        public SongLibraryEntry Entry;
        public string Fingerprint;
    }

    private static readonly Dictionary<string, CachedSongEntry> cachedEntriesByDirectory = new Dictionary<string, CachedSongEntry>(StringComparer.OrdinalIgnoreCase);
    private static string cachedLibraryFingerprint = string.Empty;

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
        cachedEntriesByDirectory.Clear();
        cachedLibraryFingerprint = string.Empty;
    }

    public static List<SongLibraryEntry> GetAvailableSongs(bool forceRefresh = false)
    {
        List<SongLibraryEntry> entries = new List<SongLibraryEntry>();

        if (!Directory.Exists(ExternalContentPaths.PersistentSongsDirectory))
        {
            Debug.LogWarning($"[SongLibraryService] Songs directory does not exist: {ExternalContentPaths.PersistentSongsDirectory}");
            return entries;
        }

        string[] songDirectories = Directory.GetDirectories(ExternalContentPaths.PersistentSongsDirectory)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string libraryFingerprint = BuildLibraryFingerprint(songDirectories);
        if (!forceRefresh && string.Equals(cachedLibraryFingerprint, libraryFingerprint, StringComparison.Ordinal))
        {
            foreach (string songDirectory in songDirectories)
            {
                if (cachedEntriesByDirectory.TryGetValue(songDirectory, out CachedSongEntry cached) && cached?.Entry != null)
                    entries.Add(CloneEntry(cached.Entry));
            }

            return entries;
        }

        Dictionary<string, CachedSongEntry> nextCache = new Dictionary<string, CachedSongEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (string songDirectory in songDirectories)
        {
            string entryFingerprint = BuildSongFingerprint(songDirectory);
            if (!forceRefresh &&
                cachedEntriesByDirectory.TryGetValue(songDirectory, out CachedSongEntry cached) &&
                cached != null &&
                string.Equals(cached.Fingerprint, entryFingerprint, StringComparison.Ordinal) &&
                cached.Entry != null)
            {
                SongLibraryEntry cloned = CloneEntry(cached.Entry);
                entries.Add(cloned);
                nextCache[songDirectory] = new CachedSongEntry
                {
                    Entry = CloneEntry(cloned),
                    Fingerprint = entryFingerprint
                };
                continue;
            }

            if (TryBuildEntry(songDirectory, out SongLibraryEntry discovered))
            {
                entries.Add(discovered);
                nextCache[songDirectory] = new CachedSongEntry
                {
                    Entry = CloneEntry(discovered),
                    Fingerprint = entryFingerprint
                };
            }
        }

        cachedEntriesByDirectory.Clear();
        foreach (KeyValuePair<string, CachedSongEntry> pair in nextCache)
            cachedEntriesByDirectory[pair.Key] = pair.Value;
        cachedLibraryFingerprint = libraryFingerprint;

        if (entries.Count == 0)
            Debug.LogWarning($"[SongLibraryService] No valid song folders found in: {ExternalContentPaths.PersistentSongsDirectory}");

        return entries;
    }

    private static bool TryBuildEntry(string songDirectory, out SongLibraryEntry entry)
    {
        entry = null;

        string mp3Path = FindFirstFile(songDirectory, "*.mp3")
                         ?? FindFirstFile(songDirectory, "*.wav")
                         ?? FindFirstFile(songDirectory, "*.ogg");
        string arcadeChartPath = FindCloneHeroChartFile(songDirectory);
        string arcadeSongIniPath = Path.Combine(songDirectory, "song.ini");
        bool hasArcadeSongIni = File.Exists(arcadeSongIniPath);
        List<string> arcadeAudioPaths = FindCloneHeroAudioFiles(songDirectory);
        string gpPath = FindPreferredGpNotation(songDirectory);
        string xmlPath = FindFirstFile(songDirectory, "*.musicxml") ?? FindFirstFile(songDirectory, "*.xml");
        string artworkPath = FindArtworkFile(songDirectory);
        string primaryNotationPath = !string.IsNullOrWhiteSpace(gpPath) ? gpPath : xmlPath;
        SongNotationSourceKind primaryNotationKind = SongNotationSourceKind.None;
        if (!SongNotationFacade.TryDetectKind(primaryNotationPath, out primaryNotationKind))
            primaryNotationKind = SongNotationSourceKind.None;

        if (string.IsNullOrEmpty(primaryNotationPath) || primaryNotationKind == SongNotationSourceKind.None)
        {
            if (string.IsNullOrWhiteSpace(arcadeChartPath) || !hasArcadeSongIni)
            {
                Debug.LogWarning($"[SongLibraryService] Skipping invalid song folder '{songDirectory}'. Required files: supported Guitar Pro/MusicXML notation, or Clone Hero notes.chart/notes.mid plus song.ini.");
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
                MidiPath = Path.GetExtension(arcadeChartPath).Equals(".mid", StringComparison.OrdinalIgnoreCase) ||
                           Path.GetExtension(arcadeChartPath).Equals(".midi", StringComparison.OrdinalIgnoreCase)
                    ? arcadeChartPath
                    : null,
                ArcadeChartPath = arcadeChartPath,
                ArcadeSongIniPath = arcadeSongIniPath,
                ArcadeAudioPaths = arcadeAudioPaths
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
            SongDirectory = entry.SongDirectory,
            Mp3Path = entry.Mp3Path,
            PrimaryNotationPath = entry.PrimaryNotationPath,
            PrimaryNotationKind = entry.PrimaryNotationKind,
            GpPath = entry.GpPath,
            XmlPath = entry.XmlPath,
            MetadataPath = entry.MetadataPath,
            MidiPath = entry.MidiPath,
            ArcadeChartPath = entry.ArcadeChartPath,
            ArcadeSongIniPath = entry.ArcadeSongIniPath,
            ArcadeAudioPaths = entry.ArcadeAudioPaths != null ? new List<string>(entry.ArcadeAudioPaths) : new List<string>()
        };
    }

    private static string BuildLibraryFingerprint(IEnumerable<string> songDirectories)
    {
        List<string> tokens = new List<string>();
        foreach (string songDirectory in songDirectories)
            tokens.Add($"{songDirectory}|{BuildSongFingerprint(songDirectory)}");

        return string.Join("||", tokens);
    }

    private static string BuildSongFingerprint(string songDirectory)
    {
        if (string.IsNullOrEmpty(songDirectory) || !Directory.Exists(songDirectory))
            return string.Empty;

        List<string> tokens = new List<string>();
        foreach (string filePath in Directory.GetFiles(songDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            FileInfo info = new FileInfo(filePath);
            tokens.Add($"{Path.GetFileName(filePath)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}");
        }

        DirectoryInfo directoryInfo = new DirectoryInfo(songDirectory);
        tokens.Add($"DIR|{directoryInfo.LastWriteTimeUtc.Ticks}");
        return string.Join(";", tokens);
    }
}
