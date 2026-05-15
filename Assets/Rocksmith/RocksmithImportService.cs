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

        MigrateLegacyImports(songsDirectory);

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
        string legacyDirectory = Path.Combine(songsDirectory, BuildLegacyImportDirectoryName(psarcPath));
        TryMigrateLegacyImportDirectory(legacyDirectory, targetDirectory);
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
            if (IsImportedCacheDirectoryName(segments[i]))
                return true;
        }

        return false;
    }

    private static void CleanupOrphanedImports(string songsDirectory, string[] psarcFiles)
    {
        string[] importDirectories = Directory.GetDirectories(songsDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => IsImportedCacheDirectoryName(Path.GetFileName(path)))
            .ToArray();
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
        string baseName = Path.GetFileNameWithoutExtension(psarcPath) ?? "psarc";
        string sanitized = SanitizeFileName(baseName);
        string hash = ComputePathHash(psarcPath);
        return $"{RocksmithCachedSongFormat.ImportedFolderPrefix}{sanitized}_{hash}";
    }

    private static string BuildLegacyImportDirectoryName(string psarcPath)
    {
        string baseName = Path.GetFileNameWithoutExtension(psarcPath) ?? "psarc";
        string sanitized = SanitizeFileName(baseName);
        string hash = ComputePathHash(psarcPath);
        return $"{RocksmithCachedSongFormat.LegacyImportedFolderPrefix}{sanitized}_{hash}";
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "psarc";

        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        string sanitized = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "psarc" : sanitized;
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

    private static bool IsImportedCacheDirectoryName(string directoryName)
    {
        if (string.IsNullOrWhiteSpace(directoryName))
            return false;

        return directoryName.StartsWith(RocksmithCachedSongFormat.ImportedFolderPrefix, StringComparison.OrdinalIgnoreCase) ||
               directoryName.StartsWith(RocksmithCachedSongFormat.LegacyImportedFolderPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static void MigrateLegacyImports(string songsDirectory)
    {
        string[] legacyDirectories = Directory.GetDirectories(
            songsDirectory,
            $"{RocksmithCachedSongFormat.LegacyImportedFolderPrefix}*",
            SearchOption.TopDirectoryOnly);

        for (int i = 0; i < legacyDirectories.Length; i++)
        {
            string legacyDirectory = legacyDirectories[i];
            string legacyName = Path.GetFileName(legacyDirectory);
            if (string.IsNullOrWhiteSpace(legacyName) ||
                !legacyName.StartsWith(RocksmithCachedSongFormat.LegacyImportedFolderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string targetDirectory = Path.Combine(
                songsDirectory,
                RocksmithCachedSongFormat.ImportedFolderPrefix + legacyName.Substring(RocksmithCachedSongFormat.LegacyImportedFolderPrefix.Length));
            TryMigrateLegacyImportDirectory(legacyDirectory, targetDirectory);
        }
    }

    private static void TryMigrateLegacyImportDirectory(string legacyDirectory, string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(legacyDirectory) ||
            !Directory.Exists(legacyDirectory) ||
            string.Equals(legacyDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            if (Directory.Exists(targetDirectory))
            {
                TryNormalizeImportedDirectory(targetDirectory);
                TryDeleteOrphanedImportDirectory(legacyDirectory);
                return;
            }

            Directory.Move(legacyDirectory, targetDirectory);
            TryNormalizeImportedDirectory(targetDirectory);
            Debug.Log($"{ImportLogPrefix} Migrated legacy imported cache '{legacyDirectory}' to '{targetDirectory}'.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"{ImportLogPrefix} Failed to migrate legacy cache '{legacyDirectory}': {ex.Message}");
        }
    }

    private static void TryNormalizeImportedDirectory(string importDirectory)
    {
        try
        {
            string legacyContentDirectory = Path.Combine(importDirectory, RocksmithCachedSongFormat.LegacyContentDirectoryName);
            string contentDirectory = Path.Combine(importDirectory, RocksmithCachedSongFormat.ContentDirectoryName);
            if (Directory.Exists(legacyContentDirectory) && !Directory.Exists(contentDirectory))
                Directory.Move(legacyContentDirectory, contentDirectory);

            string manifestPath = Path.Combine(importDirectory, RocksmithCachedSongFormat.ManifestFileName);
            if (!File.Exists(manifestPath))
                return;

            RocksmithCachedSongManifest manifest = JsonUtility.FromJson<RocksmithCachedSongManifest>(File.ReadAllText(manifestPath));
            if (manifest == null)
                return;

            manifest.audioPath = RewriteStoredCachePath(manifest.audioPath);
            manifest.previewAudioPath = RewriteStoredCachePath(manifest.previewAudioPath);
            manifest.artworkPath = RewriteStoredCachePath(manifest.artworkPath);
            manifest.arrangements ??= new System.Collections.Generic.List<RocksmithCachedArrangementSummary>();
            for (int i = 0; i < manifest.arrangements.Count; i++)
            {
                if (manifest.arrangements[i] == null)
                    continue;

                manifest.arrangements[i].partFilePath = RewriteStoredCachePath(manifest.arrangements[i].partFilePath);
            }

            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"{ImportLogPrefix} Failed to normalize imported cache '{importDirectory}': {ex.Message}");
        }
    }

    private static string RewriteStoredCachePath(string storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath) || Path.IsPathRooted(storedPath))
            return storedPath;

        string normalized = storedPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        string legacyPrefix = RocksmithCachedSongFormat.LegacyContentDirectoryName + Path.DirectorySeparatorChar;
        if (normalized.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            return RocksmithCachedSongFormat.ContentDirectoryName + normalized.Substring(RocksmithCachedSongFormat.LegacyContentDirectoryName.Length);

        return normalized;
    }
}
