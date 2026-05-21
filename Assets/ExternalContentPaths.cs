using System;
using System.IO;
using UnityEngine;

public static class ExternalContentPaths
{
    [Serializable]
    private sealed class ExternalContentSettingsData
    {
        public int version = 2;
        public string songsDirectoryOverride = string.Empty;
        public string toneLabEffectsDirectoryOverride = string.Empty;
    }

    public const string LegalFolderName = "Legal";
    public const string LicensesFolderName = "Licenses";
    public const string ToneLabFolderName = "ToneLab";
    public const string ToneLabDistFolderName = "dist";
    public const string ToneLabDistAppFolderName = "ToneLab";
    public const string AlphaTabRenderHelperFolderName = "AlphaTabRenderHelper";
    public const string AlphaTabRenderHelperExeFileName = "AlphaTabRenderHelper.exe";
    public const string AlphaTabRenderCacheFolderName = "AlphaTabRenderCache";
    public const string StemCacheFolderName = "StemCache";
    public const string StemSeparatorFolderName = "StemSeparator";
    public const string StemSeparatorRuntimeFolderName = "runtime";
    public const string StemSeparatorPackageFileName = "stem-separator-runtime-win-x64.zip";
    public const string StemSeparatorExeFileName = "StemSeparator.exe";
    public const string StemSeparatorPythonExeFileName = "python.exe";
    public const string StemSeparatorPythonFolderName = "python";
    public const string StemSeparatorInstallManifestFileName = "stem-separator-runtime.manifest.json";
    public const string StemSeparatorModelCacheFolderName = "model-cache";
    public const string SongsFolderName = "Songs";
    public const string ToneLabScriptFileName = "ToneLab.py";
    public const string ToneLabExeFileName = "ToneLab.exe";
    public const string ToneLabConfigFileName = "tone.json";
    public const string AudioSettingsFileName = "audio_settings.json";
    public const string ToneLabPresetsFolderName = "Presets";
    public const string ToneLabLv2FolderName = "LV2";
    public const string ToneLabNamFolderName = "NAM";
    public const string SongMetadataFileName = "metadata.json";
    public const string SongSaveDataFileName = "saveData.json";
    public const string SongLibraryCacheFileName = "song_library_cache.json";
    public const string ExternalContentSettingsFileName = "content_settings.json";

    private static bool externalContentSettingsLoaded;
    private static string songsDirectoryOverride = string.Empty;
    private static string toneLabEffectsDirectoryOverride = string.Empty;
    private static string cachedStreamingRoot = string.Empty;
    private static string cachedPersistentRoot = string.Empty;

    public static string StreamingRoot
    {
        get
        {
            EnsureUnityRootsCaptured();
            return cachedStreamingRoot;
        }
    }

    public static string PersistentRoot
    {
        get
        {
            EnsureUnityRootsCaptured();
            return cachedPersistentRoot;
        }
    }

    public static void EnsureUnityRootsCaptured()
    {
        if (string.IsNullOrWhiteSpace(cachedStreamingRoot))
            cachedStreamingRoot = Application.streamingAssetsPath;
        if (string.IsNullOrWhiteSpace(cachedPersistentRoot))
            cachedPersistentRoot = Application.persistentDataPath;
    }

    public static string StreamingLegalDirectory => Path.Combine(StreamingRoot, LegalFolderName);
    public static string PersistentLicensesDirectory => Path.Combine(PersistentRoot, LicensesFolderName);
    public static string StreamingToneLabDirectory => Path.Combine(StreamingRoot, ToneLabFolderName);
    public static string StreamingAlphaTabRenderHelperDirectory => Path.Combine(StreamingRoot, AlphaTabRenderHelperFolderName);
    public static string StreamingStemSeparatorDirectory => Path.Combine(StreamingRoot, StemSeparatorFolderName);
    public static string StreamingStemSeparatorRuntimeDirectory => Path.Combine(StreamingStemSeparatorDirectory, StemSeparatorRuntimeFolderName);
    public static string StreamingStemSeparatorPackagePath => Path.Combine(StreamingStemSeparatorDirectory, StemSeparatorPackageFileName);
    public static string PersistentToneLabDirectory => Path.Combine(PersistentRoot, ToneLabFolderName);
    public static string PersistentToneLabPresetDirectory => Path.Combine(PersistentToneLabDirectory, ToneLabPresetsFolderName);
    public static string DefaultPersistentToneLabEffectsDirectory => PersistentToneLabDirectory;
    public static string PersistentToneLabEffectsDirectory => string.IsNullOrWhiteSpace(GetToneLabEffectsDirectoryOverride()) ? DefaultPersistentToneLabEffectsDirectory : GetToneLabEffectsDirectoryOverride();
    public static string PersistentToneLabLv2Directory => Path.Combine(PersistentToneLabEffectsDirectory, ToneLabLv2FolderName);
    public static string PersistentToneLabNamDirectory => Path.Combine(PersistentToneLabEffectsDirectory, ToneLabNamFolderName);
    public static string PersistentToneLabDistDirectory => Path.Combine(PersistentToneLabDirectory, ToneLabDistFolderName, ToneLabDistAppFolderName);
    public static string PersistentAlphaTabRenderCacheDirectory => Path.Combine(PersistentRoot, AlphaTabRenderCacheFolderName);
    public static string PersistentStemCacheDirectory => Path.Combine(PersistentRoot, StemCacheFolderName);
    public static string PersistentStemSeparatorDirectory => Path.Combine(PersistentRoot, StemSeparatorFolderName);
    public static string PersistentStemSeparatorRuntimeDirectory => Path.Combine(PersistentStemSeparatorDirectory, StemSeparatorRuntimeFolderName);
    public static string PersistentStemSeparatorPackagePath => Path.Combine(PersistentStemSeparatorDirectory, StemSeparatorPackageFileName);
    public static string PersistentStemSeparatorModelCacheDirectory => Path.Combine(PersistentStemSeparatorDirectory, StemSeparatorModelCacheFolderName);
    public static string StreamingSongsDirectory => Path.Combine(StreamingRoot, SongsFolderName);
    public static string DefaultPersistentSongsDirectory => Path.Combine(PersistentRoot, SongsFolderName);
    public static string PersistentSongsDirectory => string.IsNullOrWhiteSpace(GetSongsDirectoryOverride()) ? DefaultPersistentSongsDirectory : GetSongsDirectoryOverride();
    public static string PersistentSongLibraryCachePath => Path.Combine(PersistentSongsDirectory, SongLibraryCacheFileName);

    public static string PersistentToneLabScriptPath => Path.Combine(PersistentToneLabDirectory, ToneLabScriptFileName);
    public static string PersistentToneLabExePath => Path.Combine(PersistentToneLabDistDirectory, ToneLabExeFileName);
    public static string StreamingAlphaTabRenderHelperExePath => Path.Combine(StreamingAlphaTabRenderHelperDirectory, AlphaTabRenderHelperExeFileName);
    public static string PersistentStemSeparatorExePath => Path.Combine(PersistentStemSeparatorRuntimeDirectory, StemSeparatorExeFileName);
    public static string PersistentStemSeparatorPythonExePath => Path.Combine(PersistentStemSeparatorRuntimeDirectory, StemSeparatorPythonFolderName, StemSeparatorPythonExeFileName);
    public static string PersistentStemSeparatorInstallManifestPath => Path.Combine(PersistentStemSeparatorRuntimeDirectory, StemSeparatorInstallManifestFileName);
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

    public static string GetToneLabEffectsDirectoryOverride()
    {
        EnsureExternalContentSettingsLoaded();
        return toneLabEffectsDirectoryOverride;
    }

    public static bool IsUsingDefaultToneLabEffectsDirectory()
    {
        return string.IsNullOrWhiteSpace(GetToneLabEffectsDirectoryOverride());
    }

    public static void SetToneLabEffectsDirectoryOverride(string directoryPath)
    {
        EnsureExternalContentSettingsLoaded();
        toneLabEffectsDirectoryOverride = NormalizeToneLabEffectsDirectoryOverride(directoryPath);
        SaveExternalContentSettings();
    }

    private static void EnsureExternalContentSettingsLoaded()
    {
        if (externalContentSettingsLoaded)
            return;

        externalContentSettingsLoaded = true;
        songsDirectoryOverride = string.Empty;
        toneLabEffectsDirectoryOverride = string.Empty;

        string path = PersistentExternalContentSettingsPath;
        try
        {
            if (!File.Exists(path))
                return;

            ExternalContentSettingsData loaded = JsonUtility.FromJson<ExternalContentSettingsData>(File.ReadAllText(path));
            songsDirectoryOverride = NormalizeSongsDirectoryOverride(loaded?.songsDirectoryOverride);
            toneLabEffectsDirectoryOverride = NormalizeToneLabEffectsDirectoryOverride(loaded?.toneLabEffectsDirectoryOverride);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ExternalContentPaths] Failed to load content settings '{path}': {ex.Message}");
            songsDirectoryOverride = string.Empty;
            toneLabEffectsDirectoryOverride = string.Empty;
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
                version = 2,
                songsDirectoryOverride = songsDirectoryOverride ?? string.Empty,
                toneLabEffectsDirectoryOverride = toneLabEffectsDirectoryOverride ?? string.Empty
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

    private static string NormalizeToneLabEffectsDirectoryOverride(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            return string.Empty;

        try
        {
            string normalized = Path.GetFullPath(directoryPath.Trim())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string defaultDirectory = DefaultPersistentToneLabEffectsDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
