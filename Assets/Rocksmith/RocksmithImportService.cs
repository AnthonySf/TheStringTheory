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
    private const string ImportLogPrefix = "[PsarcImport]";
    private const string ImportFailureLogFolderName = "RocksmithImportLogs";
    private static bool missingToolLogged;
    private static bool unsupportedPlatformLogged;

    public static void RefreshImports(Action<int, int, string> progress = null)
    {
        if (!IsSupportedRuntimePlatform())
        {
            if (!unsupportedPlatformLogged)
            {
                Debug.LogWarning($"{ImportLogPrefix} PSARC import requires a bundled importer executable for this platform.");
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
        {
            progress?.Invoke(0, 0, string.Empty);
            return;
        }

        string importToolPath = GetImportToolPath();
        if (!File.Exists(importToolPath))
        {
            if (!missingToolLogged)
            {
                string logPath = WriteImportFailureLog(
                    psarcPath: null,
                    importToolPath: importToolPath,
                    targetDirectory: null,
                    stagingDirectory: null,
                    reason: "Import tool not found",
                    details: $"Expected importer executable at '{importToolPath}'.");
                Debug.LogWarning($"{ImportLogPrefix} Import tool not found at '{importToolPath}'. Bundle the {StringTheoryPlatform.DotNetRuntimeIdentifier} Rocksmith import helper under '{ExternalContentPaths.StreamingRocksmithImportDirectory}' to enable PSARC song import. Failure log: {logPath}");
                missingToolLogged = true;
            }

            return;
        }

        missingToolLogged = false;
        for (int i = 0; i < psarcFiles.Length; i++)
        {
            progress?.Invoke(i, psarcFiles.Length, Path.GetFileName(psarcFiles[i]));
            RefreshImportForFile(psarcFiles[i], songsDirectory, importToolPath);
        }

        progress?.Invoke(psarcFiles.Length, psarcFiles.Length, string.Empty);
    }

    public static bool RefreshImportForPsarc(string psarcPath, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(psarcPath))
        {
            error = "PSARC path was empty.";
            return false;
        }

        if (!IsSupportedRuntimePlatform())
        {
            error = "PSARC import refresh is not available because this platform's importer executable is not enabled.";
            return false;
        }

        string normalizedPsarcPath = Path.GetFullPath(psarcPath);
        if (!File.Exists(normalizedPsarcPath))
        {
            error = $"PSARC file was not found: {normalizedPsarcPath}";
            return false;
        }

        string songsDirectory = ExternalContentPaths.PersistentSongsDirectory;
        string importToolPath = GetImportToolPath();
        if (!File.Exists(importToolPath))
        {
            error = $"Import tool was not found: {importToolPath}";
            return false;
        }

        try
        {
            Directory.CreateDirectory(songsDirectory);
            RefreshImportForFile(normalizedPsarcPath, songsDirectory, importToolPath);
            string manifestPath = Path.Combine(songsDirectory, BuildImportDirectoryName(normalizedPsarcPath), RocksmithCachedSongFormat.ManifestFileName);
            return File.Exists(manifestPath);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string GetImportedManifestPathForPsarc(string psarcPath)
    {
        if (string.IsNullOrWhiteSpace(psarcPath))
            return string.Empty;

        string normalizedPsarcPath = Path.GetFullPath(psarcPath);
        return Path.Combine(
            ExternalContentPaths.PersistentSongsDirectory,
            BuildImportDirectoryName(normalizedPsarcPath),
            RocksmithCachedSongFormat.ManifestFileName);
    }

    public static bool IsPsarcImportUpToDate(string psarcPath)
    {
        if (string.IsNullOrWhiteSpace(psarcPath))
            return false;

        string manifestPath = GetImportedManifestPathForPsarc(psarcPath);
        return IsImportUpToDate(psarcPath, manifestPath);
    }

    private static bool IsSupportedRuntimePlatform()
    {
        RuntimePlatform platform = Application.platform;
        return platform == RuntimePlatform.WindowsEditor ||
               platform == RuntimePlatform.WindowsPlayer ||
               platform == RuntimePlatform.OSXEditor ||
               platform == RuntimePlatform.OSXPlayer;
    }

    public static string GetImportToolPath()
    {
        return ExternalContentPaths.StreamingRocksmithImportToolPath;
    }

    private static void RefreshImportForFile(string psarcPath, string songsDirectory, string importToolPath)
    {
        string targetDirectory = Path.Combine(songsDirectory, BuildImportDirectoryName(psarcPath));
        string legacyDirectory = Path.Combine(songsDirectory, BuildLegacyImportDirectoryName(psarcPath));
        string stagingDirectory = targetDirectory + ".importing";
        TryMigrateLegacyImportDirectory(legacyDirectory, targetDirectory);
        string manifestPath = Path.Combine(targetDirectory, RocksmithCachedSongFormat.ManifestFileName);

        if (IsImportUpToDate(psarcPath, manifestPath))
            return;

        if (!TryDeleteDirectory(stagingDirectory, out string stagingDeleteError))
        {
            string logPath = WriteImportFailureLog(
                psarcPath,
                importToolPath,
                targetDirectory,
                stagingDirectory,
                "Failed to prepare staging directory",
                stagingDeleteError);
            Debug.LogWarning($"{ImportLogPrefix} Failed to prepare staging import directory '{stagingDirectory}': {stagingDeleteError}\nFailure log: {logPath}");
            return;
        }

        Directory.CreateDirectory(stagingDirectory);
        if (!RunImporter(importToolPath, psarcPath, stagingDirectory, out string processOutput))
        {
            TryDeleteDirectory(stagingDirectory, out _);
            string logPath = WriteImportFailureLog(
                psarcPath,
                importToolPath,
                targetDirectory,
                stagingDirectory,
                "Importer process failed",
                processOutput);
            Debug.LogWarning($"{ImportLogPrefix} Failed to import '{psarcPath}'.\n{processOutput}\nFailure log: {logPath}");
            return;
        }

        string stagedManifestPath = Path.Combine(stagingDirectory, RocksmithCachedSongFormat.ManifestFileName);
        if (!File.Exists(stagedManifestPath))
        {
            TryDeleteDirectory(stagingDirectory, out _);
            string logPath = WriteImportFailureLog(
                psarcPath,
                importToolPath,
                targetDirectory,
                stagingDirectory,
                "Importer produced no manifest",
                $"Expected manifest at '{stagedManifestPath}', but it was not created.");
            Debug.LogWarning($"{ImportLogPrefix} Importer completed for '{psarcPath}' but did not produce '{stagedManifestPath}'.\nFailure log: {logPath}");
            return;
        }

        string metadataPath = Path.Combine(targetDirectory, ExternalContentPaths.SongMetadataFileName);
        string preservedMetadataJson = TryReadTextFile(metadataPath);
        if (!TryReplaceImportedDirectory(stagingDirectory, targetDirectory, preservedMetadataJson, out string replacementError))
        {
            TryDeleteDirectory(stagingDirectory, out _);
            string logPath = WriteImportFailureLog(
                psarcPath,
                importToolPath,
                targetDirectory,
                stagingDirectory,
                "Failed to finalize import",
                replacementError);
            Debug.LogWarning($"{ImportLogPrefix} Failed to finalize import for '{psarcPath}': {replacementError}\nFailure log: {logPath}");
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

        if (manifest.schemaVersion < RocksmithCachedSongFormat.SchemaVersion)
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

            if (!RocksmithCachedSongLoader.TryLoadArrangementPart(manifestPath, i, out _, out RocksmithCachedArrangementPart loadedPart) ||
                loadedPart == null ||
                loadedPart.timing == null ||
                loadedPart.timing.ebeats == null ||
                loadedPart.timing.ebeats.Count == 0)
            {
                return false;
            }

            if (manifest.schemaVersion >= 19 &&
                manifest.toneDefinitionScanVersion < 1)
            {
                return false;
            }

            if (manifest.schemaVersion >= 19 &&
                manifest.toneDefinitionCount > 0 &&
                !HasUsableToneDefinitions(loadedPart))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasUsableToneDefinitions(RocksmithCachedArrangementPart part)
    {
        if (part?.tones?.definitions == null || part.tones.definitions.Count == 0)
            return false;

        for (int i = 0; i < part.tones.definitions.Count; i++)
        {
            string rawJson = part.tones.definitions[i]?.rawJson;
            if (!string.IsNullOrWhiteSpace(rawJson) &&
                rawJson.IndexOf("\"GearList\"", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool RunImporter(string importToolPath, string psarcPath, string targetDirectory, out string processOutput)
    {
        try
        {
            StringTheoryPlatform.TryEnsureExecutable(importToolPath);
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
        catch (Exception ex)
        {
            processOutput = ex.ToString();
            return false;
        }
    }

    private static bool TryReplaceImportedDirectory(string stagingDirectory, string targetDirectory, string preservedMetadataJson, out string error)
    {
        error = string.Empty;

        if (!TryDeleteDirectory(targetDirectory, out error))
            return false;

        try
        {
            Directory.Move(stagingDirectory, targetDirectory);
            if (!string.IsNullOrWhiteSpace(preservedMetadataJson))
                File.WriteAllText(Path.Combine(targetDirectory, ExternalContentPaths.SongMetadataFileName), preservedMetadataJson);

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryDeleteDirectory(string directory, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return true;

        try
        {
            Directory.Delete(directory, true);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string TryReadTextFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            return File.ReadAllText(path);
        }
        catch
        {
            return null;
        }
    }

    private static string WriteImportFailureLog(
        string psarcPath,
        string importToolPath,
        string targetDirectory,
        string stagingDirectory,
        string reason,
        string details)
    {
        try
        {
            string logsRoot = Path.Combine(ExternalContentPaths.PersistentRoot, ImportFailureLogFolderName);
            Directory.CreateDirectory(logsRoot);

            string baseName = string.IsNullOrWhiteSpace(psarcPath)
                ? "psarc_import"
                : SanitizeFileName(Path.GetFileNameWithoutExtension(psarcPath));
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string logPath = Path.Combine(logsRoot, $"{baseName}-{timestamp}.log");

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("StringTheory PSARC Import Failure");
            builder.AppendLine($"TimestampUtc: {DateTime.UtcNow:O}");
            builder.AppendLine($"Platform: {Application.platform}");
            builder.AppendLine($"Reason: {reason ?? "Unknown"}");
            builder.AppendLine($"PsarcPath: {psarcPath ?? "(none)"}");
            builder.AppendLine($"ImportToolPath: {importToolPath ?? "(none)"}");
            builder.AppendLine($"TargetDirectory: {targetDirectory ?? "(none)"}");
            builder.AppendLine($"StagingDirectory: {stagingDirectory ?? "(none)"}");
            builder.AppendLine($"PersistentRoot: {ExternalContentPaths.PersistentRoot}");
            builder.AppendLine();
            builder.AppendLine("Details:");
            builder.AppendLine(string.IsNullOrWhiteSpace(details) ? "(none)" : details);

            File.WriteAllText(logPath, builder.ToString());
            return logPath;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"{ImportLogPrefix} Failed to write import failure log: {ex.Message}");
            return "(failed to write log)";
        }
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
