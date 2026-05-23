using System;
using System.IO;
using UnityEngine;

public static class ExternalContentBootstrap
{
    private static bool runtimeContentReady;
    private static bool toneLabContentReady;
    private static string toneLabContentReadyPath = string.Empty;
    private static bool toneLabEffectsDirectoryReady;
    private static string toneLabEffectsDirectoryReadyPath = string.Empty;
    private static bool songsDirectoryReady;
    private static string songsDirectoryReadyPath = string.Empty;

    public static void EnsureRuntimeContentReady()
    {
        ExternalContentPaths.EnsureUnityRootsCaptured();
        EnsureBaseRuntimeContentReady();
        EnsureToneLabContentReady();
        EnsureToneLabEffectsDirectoryReady();
        EnsureSongsDirectoryReady();
    }

    public static void EnsureToneLabRuntimeContentReady()
    {
        ExternalContentPaths.EnsureUnityRootsCaptured();
        EnsureBaseRuntimeContentReady();
        EnsureToneLabContentReady();
        EnsureToneLabEffectsDirectoryReady();
    }

    private static void EnsureBaseRuntimeContentReady()
    {
        if (!runtimeContentReady)
        {
            Debug.Log($"[ExternalContentBootstrap] Persistent root: {ExternalContentPaths.PersistentRoot}");

            EnsureDirectory(ExternalContentPaths.PersistentRoot);
            EnsureDirectory(ExternalContentPaths.PersistentLicensesDirectory);
            EnsureDirectory(ExternalContentPaths.PersistentStemSeparatorDirectory);
            EnsureDirectory(ExternalContentPaths.PersistentStemSeparatorModelCacheDirectory);

            SyncRecursive(ExternalContentPaths.StreamingLegalDirectory, ExternalContentPaths.PersistentLicensesDirectory);
            runtimeContentReady = true;
        }
    }

    private static void EnsureToneLabContentReady()
    {
        string toneLabPath = ExternalContentPaths.PersistentToneLabDirectory;
        if (toneLabContentReady &&
            string.Equals(toneLabContentReadyPath, toneLabPath, StringTheoryPlatform.PathComparison) &&
            Directory.Exists(toneLabPath))
        {
            return;
        }

        EnsureDirectory(ExternalContentPaths.PersistentToneLabDirectory);
        EnsureDirectory(ExternalContentPaths.PersistentToneLabPresetDirectory);
        CopyMissingRecursive(ExternalContentPaths.StreamingToneLabDirectory, ExternalContentPaths.PersistentToneLabDirectory);
        toneLabContentReady = true;
        toneLabContentReadyPath = toneLabPath;
    }

    public static void EnsureSongsDirectoryReady()
    {
        string songsPath = ExternalContentPaths.PersistentSongsDirectory;
        if (songsDirectoryReady &&
            string.Equals(songsDirectoryReadyPath, songsPath, StringTheoryPlatform.PathComparison) &&
            Directory.Exists(songsPath))
        {
            return;
        }

        EnsureDirectory(ExternalContentPaths.PersistentSongsDirectory);
        SyncSongContentRecursive(ExternalContentPaths.StreamingSongsDirectory, ExternalContentPaths.PersistentSongsDirectory);
        songsDirectoryReady = true;
        songsDirectoryReadyPath = songsPath;
    }

    public static void EnsureToneLabEffectsDirectoryReady()
    {
        string effectsPath = ExternalContentPaths.PersistentToneLabEffectsDirectory;
        if (toneLabEffectsDirectoryReady &&
            string.Equals(toneLabEffectsDirectoryReadyPath, effectsPath, StringTheoryPlatform.PathComparison) &&
            Directory.Exists(ExternalContentPaths.PersistentToneLabLv2Directory) &&
            Directory.Exists(ExternalContentPaths.PersistentToneLabNamDirectory))
        {
            return;
        }

        EnsureDirectory(ExternalContentPaths.PersistentToneLabEffectsDirectory);
        EnsureDirectory(ExternalContentPaths.PersistentToneLabLv2Directory);
        EnsureDirectory(ExternalContentPaths.PersistentToneLabNamDirectory);
        CopyMissingRecursive(
            Path.Combine(ExternalContentPaths.StreamingToneLabDirectory, ExternalContentPaths.ToneLabLv2FolderName),
            ExternalContentPaths.PersistentToneLabLv2Directory);
        CopyMissingRecursive(
            Path.Combine(ExternalContentPaths.StreamingToneLabDirectory, ExternalContentPaths.ToneLabNamFolderName),
            ExternalContentPaths.PersistentToneLabNamDirectory);
        toneLabEffectsDirectoryReady = true;
        toneLabEffectsDirectoryReadyPath = effectsPath;
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            Debug.Log($"[ExternalContentBootstrap] Created directory: {path}");
        }
    }

    private static void CopyMissingRecursive(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            Debug.LogWarning($"[ExternalContentBootstrap] Source directory missing, skipping copy: {sourceDirectory}");
            return;
        }

        EnsureDirectory(destinationDirectory);

        foreach (string sourceFilePath in Directory.GetFiles(sourceDirectory))
        {
            string fileName = Path.GetFileName(sourceFilePath);
            if (ShouldSkipRuntimeContentFile(fileName))
                continue;

            string destinationFilePath = Path.Combine(destinationDirectory, fileName);

            if (!File.Exists(destinationFilePath))
            {
                CopyFilePreservingTimestamp(sourceFilePath, destinationFilePath, overwrite: false);
                Debug.Log($"[ExternalContentBootstrap] Copied default file: {destinationFilePath}");
            }
        }

        foreach (string sourceSubDirectory in Directory.GetDirectories(sourceDirectory))
        {
            string folderName = Path.GetFileName(sourceSubDirectory);
            string destinationSubDirectory = Path.Combine(destinationDirectory, folderName);
            CopyMissingRecursive(sourceSubDirectory, destinationSubDirectory);
        }
    }

    private static void SyncRecursive(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            Debug.LogWarning($"[ExternalContentBootstrap] Source directory missing, skipping sync: {sourceDirectory}");
            return;
        }

        EnsureDirectory(destinationDirectory);

        foreach (string sourceFilePath in Directory.GetFiles(sourceDirectory))
        {
            string fileName = Path.GetFileName(sourceFilePath);
            string destinationFilePath = Path.Combine(destinationDirectory, fileName);
            bool shouldCopy = !File.Exists(destinationFilePath) || !FilesMatch(sourceFilePath, destinationFilePath);

            if (!shouldCopy)
            {
                continue;
            }

            CopyFilePreservingTimestamp(sourceFilePath, destinationFilePath, overwrite: true);
            Debug.Log($"[ExternalContentBootstrap] Synced legal file: {destinationFilePath}");
        }

        foreach (string sourceSubDirectory in Directory.GetDirectories(sourceDirectory))
        {
            string folderName = Path.GetFileName(sourceSubDirectory);
            string destinationSubDirectory = Path.Combine(destinationDirectory, folderName);
            SyncRecursive(sourceSubDirectory, destinationSubDirectory);
        }
    }

    private static bool ShouldSkipRuntimeContentFile(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName) &&
               fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase);
    }

    private static void SyncSongContentRecursive(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            Debug.LogWarning($"[ExternalContentBootstrap] Source directory missing, skipping song sync: {sourceDirectory}");
            return;
        }

        EnsureDirectory(destinationDirectory);

        foreach (string sourceFilePath in Directory.GetFiles(sourceDirectory))
        {
            string fileName = Path.GetFileName(sourceFilePath);
            if (ShouldSkipSongSyncFile(fileName))
                continue;

            string destinationFilePath = Path.Combine(destinationDirectory, fileName);
            bool shouldCopy = !File.Exists(destinationFilePath) || !FilesMatch(sourceFilePath, destinationFilePath);
            if (!shouldCopy)
                continue;

            CopyFilePreservingTimestamp(sourceFilePath, destinationFilePath, overwrite: true);
            Debug.Log($"[ExternalContentBootstrap] Synced song content file: {destinationFilePath}");
        }

        foreach (string sourceSubDirectory in Directory.GetDirectories(sourceDirectory))
        {
            string folderName = Path.GetFileName(sourceSubDirectory);
            string destinationSubDirectory = Path.Combine(destinationDirectory, folderName);
            SyncSongContentRecursive(sourceSubDirectory, destinationSubDirectory);
        }
    }

    private static bool ShouldSkipSongSyncFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        if (fileName.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
            return true;

        return fileName.Equals(ExternalContentPaths.SongMetadataFileName, System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool FilesMatch(string sourceFilePath, string destinationFilePath)
    {
        FileInfo sourceInfo = new FileInfo(sourceFilePath);
        FileInfo destinationInfo = new FileInfo(destinationFilePath);

        return sourceInfo.Length == destinationInfo.Length &&
               sourceInfo.LastWriteTimeUtc == destinationInfo.LastWriteTimeUtc;
    }

    private static void CopyFilePreservingTimestamp(string sourceFilePath, string destinationFilePath, bool overwrite)
    {
        File.Copy(sourceFilePath, destinationFilePath, overwrite);
        File.SetLastWriteTimeUtc(destinationFilePath, File.GetLastWriteTimeUtc(sourceFilePath));
    }
}
