using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

public static class RocksmithImportService
{
    private const string ImportToolFolderName = "RocksmithImport";
    private const string ImportToolExecutableName = "RocksmithImportTool.exe";
    private const string ImportLogPrefix = "[PsarcImport]";
    private static bool missingToolLogged;
    private static bool unsupportedPlatformLogged;

    public static void RefreshImports()
    {
        if (!IsSupportedRuntimePlatform())
        {
            if (!unsupportedPlatformLogged)
            {
                Debug.LogWarning($"{ImportLogPrefix} PSARC import is only available on Windows because it depends on a local Windows importer executable.");
                unsupportedPlatformLogged = true;
            }

            return;
        }

        unsupportedPlatformLogged = false;
        string songsDirectory = ExternalContentPaths.PersistentSongsDirectory;
        if (!Directory.Exists(songsDirectory))
            return;

        string[] psarcFiles = Directory.GetFiles(songsDirectory, "*.psarc", SearchOption.AllDirectories)
            .Where(path => !IsInsideImportedCache(path, songsDirectory))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        CleanupOrphanedImports(songsDirectory, psarcFiles);

        if (psarcFiles.Length == 0)
            return;

        string importToolPath = GetImportToolPath();
        if (!File.Exists(importToolPath))
        {
            if (!missingToolLogged)
            {
                Debug.LogWarning($"{ImportLogPrefix} Import tool not found at '{importToolPath}'. Drop 'RocksmithImportTool.exe' into that folder to enable PSARC song import.");
                missingToolLogged = true;
            }

            return;
        }

        missingToolLogged = false;
        for (int i = 0; i < psarcFiles.Length; i++)
            RefreshImportForFile(psarcFiles[i], songsDirectory, importToolPath);
    }

    private static bool IsSupportedRuntimePlatform()
    {
        RuntimePlatform platform = Application.platform;
        return platform == RuntimePlatform.WindowsEditor || platform == RuntimePlatform.WindowsPlayer;
    }

    public static string GetImportToolPath()
    {
        return Path.Combine(ExternalContentPaths.StreamingRoot, ImportToolFolderName, ImportToolExecutableName);
    }

    private static void RefreshImportForFile(string psarcPath, string songsDirectory, string importToolPath)
    {
        string targetDirectory = Path.Combine(songsDirectory, BuildImportDirectoryName(psarcPath));
        string manifestPath = Path.Combine(targetDirectory, RocksmithCachedSongFormat.ManifestFileName);

        if (IsImportUpToDate(psarcPath, manifestPath))
            return;

        Directory.CreateDirectory(targetDirectory);
        if (!RunImporter(importToolPath, psarcPath, targetDirectory, out string processOutput))
        {
            Debug.LogWarning($"{ImportLogPrefix} Failed to import '{psarcPath}'.\n{processOutput}");
            return;
        }

        if (!File.Exists(manifestPath))
        {
            Debug.LogWarning($"{ImportLogPrefix} Importer completed for '{psarcPath}' but did not produce '{manifestPath}'.");
            return;
        }

        Debug.Log($"{ImportLogPrefix} Imported '{Path.GetFileName(psarcPath)}' into '{targetDirectory}'.");
    }

    private static bool IsImportUpToDate(string psarcPath, string manifestPath)
    {
        if (!File.Exists(psarcPath) || !File.Exists(manifestPath))
            return false;

        if (!RocksmithCachedSongLoader.TryLoadManifest(manifestPath, out RocksmithCachedSongManifest manifest) || manifest == null)
            return false;

        long currentTicks = File.GetLastWriteTimeUtc(psarcPath).Ticks;
        if (!string.Equals(Path.GetFullPath(psarcPath), Path.GetFullPath(manifest.sourcePsarcPath ?? string.Empty), StringComparison.OrdinalIgnoreCase))
            return false;

        if (manifest.sourcePsarcLastWriteUtcTicks != currentTicks)
            return false;

        if (manifest.arrangements == null || manifest.arrangements.Count == 0)
            return false;

        if (string.IsNullOrWhiteSpace(manifest.audioPath) || !File.Exists(manifest.audioPath))
            return false;

        for (int i = 0; i < manifest.arrangements.Count; i++)
        {
            RocksmithCachedArrangementSummary arrangement = manifest.arrangements[i];
            if (arrangement == null || string.IsNullOrWhiteSpace(arrangement.partFilePath) || !File.Exists(arrangement.partFilePath))
                return false;
        }

        return true;
    }

    private static bool RunImporter(string importToolPath, string psarcPath, string targetDirectory, out string processOutput)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = importToolPath,
            Arguments = $"import {Quote(psarcPath)} {Quote(targetDirectory)}",
            WorkingDirectory = Path.GetDirectoryName(importToolPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using Process process = new Process { StartInfo = startInfo };
        process.Start();
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        processOutput = string.Join(Environment.NewLine, new[] { stdout, stderr }.Where(text => !string.IsNullOrWhiteSpace(text)));
        return process.ExitCode == 0;
    }

    private static bool IsInsideImportedCache(string filePath, string songsDirectory)
    {
        string relativeDirectory = Path.GetRelativePath(songsDirectory, Path.GetDirectoryName(filePath) ?? songsDirectory);
        if (string.IsNullOrWhiteSpace(relativeDirectory) || string.Equals(relativeDirectory, ".", StringComparison.Ordinal))
            return false;

        string[] segments = relativeDirectory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i].StartsWith(RocksmithCachedSongFormat.ImportedFolderPrefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void CleanupOrphanedImports(string songsDirectory, string[] psarcFiles)
    {
        string[] importDirectories = Directory.GetDirectories(songsDirectory, $"{RocksmithCachedSongFormat.ImportedFolderPrefix}*", SearchOption.TopDirectoryOnly);
        if (importDirectories == null || importDirectories.Length == 0)
            return;

        var knownSources = psarcFiles
            .Select(path => Path.GetFullPath(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < importDirectories.Length; i++)
        {
            string manifestPath = Path.Combine(importDirectories[i], RocksmithCachedSongFormat.ManifestFileName);
            if (!RocksmithCachedSongLoader.TryLoadManifest(manifestPath, out RocksmithCachedSongManifest manifest) || manifest == null)
                continue;

            string sourcePath = manifest.sourcePsarcPath;
            if (!string.IsNullOrWhiteSpace(sourcePath) && knownSources.Contains(Path.GetFullPath(sourcePath)) && File.Exists(sourcePath))
                continue;

            TryDeleteOrphanedImportDirectory(importDirectories[i]);
        }
    }

    private static void TryDeleteOrphanedImportDirectory(string directory)
    {
        try
        {
            string metadataPath = Path.Combine(directory, ExternalContentPaths.SongMetadataFileName);
            if (File.Exists(metadataPath))
                File.Delete(metadataPath);

            Directory.Delete(directory, true);
            Debug.Log($"{ImportLogPrefix} Removed orphaned imported cache '{directory}'.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"{ImportLogPrefix} Failed to remove orphaned cache '{directory}': {ex.Message}");
        }
    }

    private static string BuildImportDirectoryName(string psarcPath)
    {
        string baseName = Path.GetFileNameWithoutExtension(psarcPath) ?? "rocksmith";
        string sanitized = SanitizeFileName(baseName);
        string hash = ComputePathHash(psarcPath);
        return $"{RocksmithCachedSongFormat.ImportedFolderPrefix}{sanitized}_{hash}";
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "rocksmith";

        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        string sanitized = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "rocksmith" : sanitized;
    }

    private static string ComputePathHash(string path)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToLowerInvariant());
        byte[] hashBytes;
        using (SHA1 sha1 = SHA1.Create())
            hashBytes = sha1.ComputeHash(bytes);
        return BitConverter.ToString(hashBytes, 0, 4).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string Quote(string path)
    {
        return $"\"{path}\"";
    }
}
