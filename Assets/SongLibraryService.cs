using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed class SongLibraryEntry
{
    public string SongId;
    public string SongDirectory;
    public string Mp3Path;
    public string XmlPath;
    public string MetadataPath;
    public string MidiPath;
}

public static class SongLibraryService
{
    public static bool TryGetFirstValidSong(out SongLibraryEntry entry)
    {
        entry = null;

        if (!Directory.Exists(ExternalContentPaths.PersistentSongsDirectory))
        {
            Debug.LogWarning($"[SongLibraryService] Songs directory does not exist: {ExternalContentPaths.PersistentSongsDirectory}");
            return false;
        }

        string[] songDirectories = Directory.GetDirectories(ExternalContentPaths.PersistentSongsDirectory)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (string songDirectory in songDirectories)
        {
            if (TryBuildEntry(songDirectory, out SongLibraryEntry discovered))
            {
                entry = discovered;
                return true;
            }
        }

        Debug.LogWarning($"[SongLibraryService] No valid song folders found in: {ExternalContentPaths.PersistentSongsDirectory}");
        return false;
    }

    private static bool TryBuildEntry(string songDirectory, out SongLibraryEntry entry)
    {
        entry = null;

        string mp3Path = FindFirstFile(songDirectory, "*.mp3");
        string xmlPath = FindFirstFile(songDirectory, "*.musicxml") ?? FindFirstFile(songDirectory, "*.xml");

        if (string.IsNullOrEmpty(mp3Path) || string.IsNullOrEmpty(xmlPath))
        {
            Debug.LogWarning($"[SongLibraryService] Skipping invalid song folder '{songDirectory}'. Required files: .mp3 and .xml/.musicxml.");
            return false;
        }

        entry = new SongLibraryEntry
        {
            SongId = Path.GetFileName(songDirectory),
            SongDirectory = songDirectory,
            Mp3Path = mp3Path,
            XmlPath = xmlPath,
            MetadataPath = Path.Combine(songDirectory, ExternalContentPaths.SongMetadataFileName),
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
}
