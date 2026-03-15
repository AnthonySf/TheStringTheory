using System.IO;
using UnityEngine;

public static class ExternalContentPaths
{
    public const string ToneLabFolderName = "ToneLab";
    public const string ToneLabDistFolderName = "dist";
    public const string ToneLabDistAppFolderName = "ToneLab";
    public const string SongsFolderName = "Songs";
    public const string ToneLabScriptFileName = "ToneLab.py";
    public const string ToneLabExeFileName = "ToneLab.exe";
    public const string ToneLabConfigFileName = "tone.json";
    public const string SongMetadataFileName = "metadata.json";
    public const string SongSaveDataFileName = "saveData.json";

    public static string StreamingRoot => Application.streamingAssetsPath;
    public static string PersistentRoot => Application.persistentDataPath;

    public static string StreamingToneLabDirectory => Path.Combine(StreamingRoot, ToneLabFolderName);
    public static string PersistentToneLabDirectory => Path.Combine(PersistentRoot, ToneLabFolderName);
    public static string PersistentToneLabDistDirectory => Path.Combine(PersistentToneLabDirectory, ToneLabDistFolderName, ToneLabDistAppFolderName);
    public static string StreamingSongsDirectory => Path.Combine(StreamingRoot, SongsFolderName);
    public static string PersistentSongsDirectory => Path.Combine(PersistentRoot, SongsFolderName);

    public static string PersistentToneLabScriptPath => Path.Combine(PersistentToneLabDirectory, ToneLabScriptFileName);
    public static string PersistentToneLabExePath => Path.Combine(PersistentToneLabDistDirectory, ToneLabExeFileName);
    public static string PersistentToneLabConfigPath => Path.Combine(PersistentToneLabDirectory, ToneLabConfigFileName);

    public static string GetPersistentSongDirectory(string songId)
    {
        return Path.Combine(PersistentSongsDirectory, songId);
    }

    public static string GetSongMetadataPath(string songDirectory)
    {
        return Path.Combine(songDirectory, SongMetadataFileName);
    }
}
