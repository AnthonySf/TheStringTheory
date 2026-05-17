using System;
using System.IO;
using UnityEngine;

public static class ExternalContentPaths
{
    [Serializable]
    private sealed class ExternalContentSettingsData
    {
        public int version = 1;
        public string songsDirectoryOverride = string.Empty;
    }

    public const string LegalFolderName = "Legal";
    public const string LicensesFolderName = "Licenses";
    public const string ToneLabFolderName = "ToneLab";
    public const string ToneLabDistFolderName = "dist";
    public const string ToneLabDistAppFolderName = "ToneLab";
    public const string AlphaTabRenderHelperFolderName = "AlphaTabRenderHelper";
    public const string AlphaTabRenderHelperExeFileName = "AlphaTabRenderHelper.exe";
    public const string AlphaTabRenderCacheFolderName = "AlphaTabRenderCache";
    public const string SongsFolderName = "Songs";
    public const string ToneLabScriptFileName = "ToneLab.py";
    public const string ToneLabExeFileName = "ToneLab.exe";
    public const string ToneLabConfigFileName = "tone.json";
    public const string AudioSettingsFileName = "audio_settings.json";
    public const string ToneLabPresetsFolderName = "Presets";
    public const string SongMetadataFileName = "metadata.json";
    public const string SongSaveDataFileName = "saveData.json";
    public const string SongLibraryCacheFileName = "song_library_cache.json";
    public const string ExternalContentSettingsFileName = "content_settings.json";

    private static bool externalContentSettingsLoaded;
    private static string songsDirectoryOverride = string.Empty;

    public static string StreamingRoot => Application.streamingAssetsPath;
    public static string PersistentRoot => Application.persistentDataPath;

    public static string StreamingLegalDirectory => Path.Combine(StreamingRoot, LegalFolderName);
    public static string PersistentLicensesDirectory => Path.Combine(PersistentRoot, LicensesFolderName);
    public static string StreamingToneLabDirectory => Path.Combine(StreamingRoot, ToneLabFolderName);
    public static string StreamingAlphaTabRenderHelperDirectory => Path.Combine(StreamingRoot, AlphaTabRenderHelperFolderName);
    public static string PersistentToneLabDirectory => Path.Combine(PersistentRoot, ToneLabFolderName);
    public static string PersistentToneLabPresetDirectory => Path.Combine(PersistentToneLabDirectory, ToneLabPresetsFolderName);
    public static string PersistentToneLabDistDirectory => Path.Combine(PersistentToneLabDirectory, ToneLabDistFolderName, ToneLabDistAppFolderName);
    public static string PersistentAlphaTabRenderCacheDirectory => Path.Combine(PersistentRoot, AlphaTabRenderCacheFolderName);
    public static string StreamingSongsDirectory => Path.Combine(StreamingRoot, SongsFolderName);
    public static string DefaultPersistentSongsDirectory => Path.Combine(PersistentRoot, SongsFolderName);
    public static string PersistentSongsDirectory => string.IsNullOrWhiteSpace(GetSongsDirectoryOverride()) ? DefaultPersistentSongsDirectory : GetSongsDirectoryOverride();
    public static string PersistentSongLibraryCachePath => Path.Combine(PersistentSongsDirectory, SongLibraryCacheFileName);

    public static string PersistentToneLabScriptPath => Path.Combine(PersistentToneLabDirectory, ToneLabScriptFileName);
    public static string PersistentToneLabExePath => Path.Combine(PersistentToneLabDistDirectory, ToneLabExeFileName);
    public static string StreamingAlphaTabRenderHelperExePath => Path.Combine(StreamingAlphaTabRenderHelperDirectory, AlphaTabRenderHelperExeFileName);
    public static string PersistentToneLabConfigPath => Path.Combine(PersistentToneLabDirectory, ToneLabConfigFileName);
    public static string PersistentAudioSettingsPath => Path.Combine(PersistentRoot, AudioSettingsFileName);
    public static string PersistentExternalContentSettingsPath => Path.Combine(PersistentRoot, ExternalContentSettingsFileName);

    public static string GetPersistentSongDirectory(string songId)
    {
        return Path.Combine(PersistentSongsDirectory, songId);
    }

    public static string GetSongMetadataPath(string songDirectory)
    {
        return Path.Combine(songDirectory, SongMetadataFileName);
    }

    public static string GetSongsDirectoryOverride()
    {
        EnsureExternalContentSettingsLoaded();
        return songsDirectoryOverride;
    }

    public static bool IsUsingDefaultSongsDirectory()
    {
        return string.IsNullOrWhiteSpace(GetSongsDirectoryOverride());
    }

    public static void SetSongsDirectoryOverride(string directoryPath)
    {
        EnsureExternalContentSettingsLoaded();
        songsDirectoryOverride = NormalizeSongsDirectoryOverride(directoryPath);
        SaveExternalContentSettings();
    }

    private static void EnsureExternalContentSettingsLoaded()
    {
        if (externalContentSettingsLoaded)
            return;

        externalContentSettingsLoaded = true;
        songsDirectoryOverride = string.Empty;

        string path = PersistentExternalContentSettingsPath;
        try
        {
            if (!File.Exists(path))
                return;

            ExternalContentSettingsData loaded = JsonUtility.FromJson<ExternalContentSettingsData>(File.ReadAllText(path));
            songsDirectoryOverride = NormalizeSongsDirectoryOverride(loaded?.songsDirectoryOverride);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ExternalContentPaths] Failed to load content settings '{path}': {ex.Message}");
            songsDirectoryOverride = string.Empty;
        }
    }

    private static void SaveExternalContentSettings()
    {
        string path = PersistentExternalContentSettingsPath;
        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            ExternalContentSettingsData data = new ExternalContentSettingsData
            {
                version = 1,
                songsDirectoryOverride = songsDirectoryOverride ?? string.Empty
            };

            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ExternalContentPaths] Failed to save content settings '{path}': {ex.Message}");
        }
    }

    private static string NormalizeSongsDirectoryOverride(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            return string.Empty;

        try
        {
            string normalized = Path.GetFullPath(directoryPath.Trim())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string defaultDirectory = DefaultPersistentSongsDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(normalized, defaultDirectory, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : normalized;
        }
        catch
        {
            return string.Empty;
        }
    }
}
