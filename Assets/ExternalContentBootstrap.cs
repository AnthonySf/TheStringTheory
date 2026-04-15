using System;
using System.IO;
using UnityEngine;

public static class ExternalContentBootstrap
{
    public static void EnsureRuntimeContentReady()
    {
        Debug.Log($"[ExternalContentBootstrap] Persistent root: {ExternalContentPaths.PersistentRoot}");

        EnsureDirectory(ExternalContentPaths.PersistentRoot);
        EnsureDirectory(ExternalContentPaths.PersistentLicensesDirectory);
        EnsureDirectory(ExternalContentPaths.PersistentToneLabDirectory);
        EnsureDirectory(ExternalContentPaths.PersistentToneLabPresetDirectory);
        EnsureDirectory(ExternalContentPaths.PersistentSongsDirectory);

        SyncRecursive(ExternalContentPaths.StreamingLegalDirectory, ExternalContentPaths.PersistentLicensesDirectory);
        CopyMissingRecursive(ExternalContentPaths.StreamingToneLabDirectory, ExternalContentPaths.PersistentToneLabDirectory);
        CopyMissingRecursive(ExternalContentPaths.StreamingSongsDirectory, ExternalContentPaths.PersistentSongsDirectory);
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
            string destinationFilePath = Path.Combine(destinationDirectory, fileName);

            if (!File.Exists(destinationFilePath))
            {
                File.Copy(sourceFilePath, destinationFilePath);
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

            File.Copy(sourceFilePath, destinationFilePath, true);
            Debug.Log($"[ExternalContentBootstrap] Synced legal file: {destinationFilePath}");
        }

        foreach (string sourceSubDirectory in Directory.GetDirectories(sourceDirectory))
        {
            string folderName = Path.GetFileName(sourceSubDirectory);
            string destinationSubDirectory = Path.Combine(destinationDirectory, folderName);
            SyncRecursive(sourceSubDirectory, destinationSubDirectory);
        }
    }

    private static bool FilesMatch(string sourceFilePath, string destinationFilePath)
    {
        FileInfo sourceInfo = new FileInfo(sourceFilePath);
        FileInfo destinationInfo = new FileInfo(destinationFilePath);

        if (sourceInfo.Length != destinationInfo.Length)
        {
            return false;
        }

        using FileStream sourceStream = File.OpenRead(sourceFilePath);
        using FileStream destinationStream = File.OpenRead(destinationFilePath);

        int sourceByte;
        while ((sourceByte = sourceStream.ReadByte()) != -1)
        {
            if (sourceByte != destinationStream.ReadByte())
            {
                return false;
            }
        }

        return true;
    }
}
