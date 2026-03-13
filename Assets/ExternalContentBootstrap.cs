using System;
using System.IO;
using UnityEngine;

public static class ExternalContentBootstrap
{
    public static void EnsureRuntimeContentReady()
    {
        Debug.Log($"[ExternalContentBootstrap] Persistent root: {ExternalContentPaths.PersistentRoot}");

        EnsureDirectory(ExternalContentPaths.PersistentRoot);
        EnsureDirectory(ExternalContentPaths.PersistentToneLabDirectory);
        EnsureDirectory(ExternalContentPaths.PersistentSongsDirectory);

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
}
