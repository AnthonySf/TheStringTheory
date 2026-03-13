using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEngine;

public sealed class SongLibraryEntry
{
    public string SongId;
    public string DisplayName;
    public string SongDirectory;
    public string Mp3Path;
    public string XmlPath;
    public string MetadataPath;
    public string MidiPath;
}

public static class SongLibraryService
{
    [Serializable]
    private sealed class SongFolderMetadata
    {
        public string songId;
        public string displayName;
    }

    public static bool TryGetFirstValidSong(out SongLibraryEntry entry)
    {
        List<SongLibraryEntry> songs = GetAvailableSongs();
        entry = songs.Count > 0 ? songs[0] : null;
        return entry != null;
    }

    public static List<SongLibraryEntry> GetAvailableSongs()
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

        foreach (string songDirectory in songDirectories)
        {
            if (TryBuildEntry(songDirectory, out SongLibraryEntry discovered))
                entries.Add(discovered);
        }

        if (entries.Count == 0)
            Debug.LogWarning($"[SongLibraryService] No valid song folders found in: {ExternalContentPaths.PersistentSongsDirectory}");

        return entries;
    }

    private static bool TryBuildEntry(string songDirectory, out SongLibraryEntry entry)
    {
        entry = null;

        string mp3Path = FindFirstFile(songDirectory, "*.mp3");
        string xmlPath = FindFirstFile(songDirectory, "*.musicxml") ?? FindFirstFile(songDirectory, "*.xml");

        if (string.IsNullOrEmpty(xmlPath))
        {
            Debug.LogWarning($"[SongLibraryService] Skipping invalid song folder '{songDirectory}'. Required files: .xml/.musicxml.");
            return false;
        }

        string metadataPath = Path.Combine(songDirectory, ExternalContentPaths.SongMetadataFileName);
        string displayName = ResolveDisplayName(songDirectory, xmlPath, metadataPath);

        entry = new SongLibraryEntry
        {
            SongId = Path.GetFileName(songDirectory),
            DisplayName = displayName,
            SongDirectory = songDirectory,
            Mp3Path = mp3Path,
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

    private static string ResolveDisplayName(string songDirectory, string xmlPath, string metadataPath)
    {
        string fallbackName = Path.GetFileName(songDirectory);

        string metadataName = TryReadDisplayNameFromMetadata(metadataPath);
        if (!string.IsNullOrWhiteSpace(metadataName))
            return metadataName.Trim();

        string xmlName = TryReadDisplayNameFromXml(xmlPath);
        if (!string.IsNullOrWhiteSpace(xmlName))
            return xmlName.Trim();

        return fallbackName;
    }

    private static string TryReadDisplayNameFromMetadata(string metadataPath)
    {
        if (string.IsNullOrEmpty(metadataPath) || !File.Exists(metadataPath))
            return null;

        try
        {
            string json = File.ReadAllText(metadataPath);
            SongFolderMetadata metadata = JsonUtility.FromJson<SongFolderMetadata>(json);
            return metadata != null ? metadata.displayName : null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongLibraryService] Failed to parse metadata '{metadataPath}': {ex.Message}");
            return null;
        }
    }

    private static string TryReadDisplayNameFromXml(string xmlPath)
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
}
