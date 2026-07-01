using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;

public sealed class TheoryPackageWriteRequest
{
    public TheorySongManifest manifest;
    public List<TheoryArrangementData> arrangements = new List<TheoryArrangementData>();
    public TheoryEditorState editorState;
    public string primaryAudioSourcePath;
    public string coverArtSourcePath;
    public string preservedPackageSourcePath;
    public List<string> preservedEntryNames = new List<string>();
}

public static class TheoryPackageIO
{
    public static bool TryReadManifest(string packagePath, out TheorySongManifest manifest, out string error)
    {
        manifest = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            error = "Theory package was not found.";
            return false;
        }

        try
        {
            string json = ReadTextEntry(packagePath, TheoryPackageFormat.ManifestEntryName);
            manifest = JsonUtility.FromJson<TheorySongManifest>(json);
            if (manifest == null)
            {
                error = "Theory package manifest could not be parsed.";
                return false;
            }

            manifest.EnsureDefaults();
            if (!string.Equals(manifest.formatId, TheoryPackageFormat.FormatId, StringComparison.Ordinal))
            {
                error = "Theory package manifest has an unsupported format id.";
                manifest = null;
                return false;
            }

            if (manifest.schemaVersion <= 0 || manifest.schemaVersion > TheoryPackageFormat.SchemaVersion)
            {
                error = $"Theory package schema version {manifest.schemaVersion} is not supported.";
                manifest = null;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Debug.LogWarning($"[TheoryPackage] Failed to read manifest '{packagePath}': {ex.Message}");
            return false;
        }
    }

    public static bool TryReadArrangement(
        string packagePath,
        TheoryArrangementSummary summary,
        out TheoryArrangementData arrangement,
        out string error)
    {
        arrangement = null;
        error = string.Empty;
        if (summary == null || string.IsNullOrWhiteSpace(summary.entry))
        {
            error = "Arrangement entry is missing.";
            return false;
        }

        try
        {
            string json = ReadTextEntry(packagePath, summary.entry);
            arrangement = JsonUtility.FromJson<TheoryArrangementData>(json);
            if (arrangement == null)
            {
                error = $"Arrangement '{summary.entry}' could not be parsed.";
                return false;
            }

            arrangement.EnsureDefaults();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Debug.LogWarning($"[TheoryPackage] Failed to read arrangement '{summary.entry}' from '{packagePath}': {ex.Message}");
            return false;
        }
    }

    public static bool TryReadEditorState(string packagePath, out TheoryEditorState editorState, out string error)
    {
        editorState = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            error = "Theory package was not found.";
            return false;
        }

        try
        {
            using (ZipArchive archive = ZipFile.OpenRead(packagePath))
            {
                ZipArchiveEntry entry = archive.GetEntry(NormalizeEntryName(TheoryPackageFormat.EditorStateEntryName));
                if (entry == null)
                    return false;

                using (Stream stream = entry.Open())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    editorState = JsonUtility.FromJson<TheoryEditorState>(json);
                }
            }

            if (editorState == null)
            {
                error = "Theory package editor state could not be parsed.";
                return false;
            }

            editorState.EnsureDefaults();
            if (editorState.schemaVersion <= 0 || editorState.schemaVersion > TheoryPackageFormat.SchemaVersion)
            {
                error = $"Theory package editor state schema version {editorState.schemaVersion} is not supported.";
                editorState = null;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Debug.LogWarning($"[TheoryPackage] Failed to read editor state '{packagePath}': {ex.Message}");
            return false;
        }
    }

    public static bool TryReadToneLabMappings(string packagePath, out TheoryToneLabMappingState mappingState, out string error)
    {
        mappingState = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            error = "Theory package was not found.";
            return false;
        }

        try
        {
            using (ZipArchive archive = ZipFile.OpenRead(packagePath))
            {
                ZipArchiveEntry entry = archive.GetEntry(NormalizeEntryName(TheoryPackageFormat.ToneLabMappingsEntryName));
                if (entry == null)
                    return false;

                using (Stream stream = entry.Open())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    mappingState = JsonUtility.FromJson<TheoryToneLabMappingState>(json);
                }
            }

            if (mappingState == null)
            {
                error = "Theory package Tone Lab mappings could not be parsed.";
                return false;
            }

            mappingState.EnsureDefaults();
            if (mappingState.schemaVersion <= 0 || mappingState.schemaVersion > TheoryPackageFormat.SchemaVersion)
            {
                error = $"Theory package Tone Lab mappings schema version {mappingState.schemaVersion} is not supported.";
                mappingState = null;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Debug.LogWarning($"[TheoryPackage] Failed to read Tone Lab mappings '{packagePath}': {ex.Message}");
            return false;
        }
    }

    public static bool WritePackage(string packagePath, TheoryPackageWriteRequest request, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            error = "Theory package path is empty.";
            return false;
        }

        if (request?.manifest == null)
        {
            error = "Theory package manifest is missing.";
            return false;
        }

        try
        {
            request.manifest.EnsureDefaults();
            request.arrangements ??= new List<TheoryArrangementData>();
            string directory = Path.GetDirectoryName(packagePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string tempPath = packagePath + ".tmp";
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                HashSet<string> writtenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                WriteTextEntry(archive, TheoryPackageFormat.ManifestEntryName, JsonUtility.ToJson(request.manifest, true));
                writtenEntries.Add(NormalizeEntryName(TheoryPackageFormat.ManifestEntryName));

                for (int i = 0; i < request.arrangements.Count; i++)
                {
                    TheoryArrangementData arrangement = request.arrangements[i];
                    if (arrangement == null)
                        continue;

                    arrangement.EnsureDefaults();
                    string entryName = FindArrangementEntry(request.manifest, arrangement);
                    if (string.IsNullOrWhiteSpace(entryName))
                        entryName = TheoryPackageFormat.BuildArrangementEntryName(arrangement.arrangementId);

                    WriteTextEntry(archive, entryName, JsonUtility.ToJson(arrangement, true));
                    writtenEntries.Add(NormalizeEntryName(entryName));
                }

                if (request.editorState != null)
                {
                    WriteTextEntry(archive, TheoryPackageFormat.EditorStateEntryName, JsonUtility.ToJson(request.editorState, true));
                    writtenEntries.Add(NormalizeEntryName(TheoryPackageFormat.EditorStateEntryName));
                }

                if (!string.IsNullOrWhiteSpace(request.primaryAudioSourcePath) && File.Exists(request.primaryAudioSourcePath))
                {
                    string audioEntry = request.manifest.primaryAudioEntry;
                    if (string.IsNullOrWhiteSpace(audioEntry))
                        audioEntry = request.manifest.audio?.FirstOrDefault(asset => asset != null && asset.defaultForPlayback)?.entry;
                    if (!string.IsNullOrWhiteSpace(audioEntry))
                    {
                        WriteFileEntry(archive, audioEntry, request.primaryAudioSourcePath);
                        writtenEntries.Add(NormalizeEntryName(audioEntry));
                    }
                }

                if (!string.IsNullOrWhiteSpace(request.coverArtSourcePath) &&
                    File.Exists(request.coverArtSourcePath) &&
                    !string.IsNullOrWhiteSpace(request.manifest.coverArtEntry))
                {
                    WriteFileEntry(archive, request.manifest.coverArtEntry, request.coverArtSourcePath);
                    writtenEntries.Add(NormalizeEntryName(request.manifest.coverArtEntry));
                }

                CopyPreservedPackageEntries(
                    archive,
                    request.preservedPackageSourcePath,
                    request.preservedEntryNames,
                    writtenEntries);
            }

            ReplacePackageFile(tempPath, packagePath);
            return true;
        }
        catch (Exception ex)
        {
            TryDeleteFile(packagePath + ".tmp");
            error = ex.Message;
            Debug.LogWarning($"[TheoryPackage] Failed to write package '{packagePath}': {ex.Message}");
            return false;
        }
    }

    public static bool TryWriteToneLabMappings(string packagePath, TheoryToneLabMappingState mappingState, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            error = "Theory package was not found.";
            return false;
        }

        if (mappingState == null)
        {
            error = "Theory package Tone Lab mappings are missing.";
            return false;
        }

        string tempPath = packagePath + ".tmp";
        if (File.Exists(tempPath))
            File.Delete(tempPath);

        try
        {
            mappingState.EnsureDefaults();
            if (mappingState.modifiedAtUtcTicks <= 0L)
                mappingState.modifiedAtUtcTicks = DateTime.UtcNow.Ticks;

            string mappingEntryName = NormalizeEntryName(TheoryPackageFormat.ToneLabMappingsEntryName);
            using (ZipArchive sourceArchive = ZipFile.OpenRead(packagePath))
            using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (ZipArchive destinationArchive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (ZipArchiveEntry sourceEntry in sourceArchive.Entries)
                {
                    string entryName = NormalizeEntryName(sourceEntry.FullName);
                    if (string.IsNullOrWhiteSpace(entryName) ||
                        string.Equals(entryName, mappingEntryName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    CopyEntry(sourceEntry, destinationArchive, entryName);
                }

                WriteTextEntry(destinationArchive, mappingEntryName, JsonUtility.ToJson(mappingState, true));
            }

            ReplacePackageFile(tempPath, packagePath);
            return true;
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempPath);
            error = ex.Message;
            Debug.LogWarning($"[TheoryPackage] Failed to write Tone Lab mappings in '{packagePath}': {ex.Message}");
            return false;
        }
    }

    public static bool TryEmbedStemCache(
        string packagePath,
        StemCacheManifest stemManifest,
        string sourceAudioPath,
        out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            error = "Theory package was not found.";
            return false;
        }

        if (stemManifest?.stems == null || stemManifest.stems.Count == 0)
        {
            error = "No generated stems are available to embed.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(sourceAudioPath) || !File.Exists(sourceAudioPath))
        {
            error = "Source audio was not found for the generated stems.";
            return false;
        }

        if (!TryReadManifest(packagePath, out TheorySongManifest manifest, out error))
            return false;

        List<StemCacheEntry> sourceStems = stemManifest.stems
            .Where(stem => stem != null && !string.IsNullOrWhiteSpace(stem.id) && !string.IsNullOrWhiteSpace(stem.relativePath))
            .OrderBy(stem => stem.id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (sourceStems.Count == 0)
        {
            error = "Generated stem manifest contains no usable stems.";
            return false;
        }

        List<TheoryStemAsset> embeddedStems = new List<TheoryStemAsset>();
        Dictionary<string, string> sourcePathsByEntry = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sourceStems.Count; i++)
        {
            StemCacheEntry stem = sourceStems[i];
            string sourceStemPath = StemSeparationService.ResolveStemPath(sourceAudioPath, stem);
            if (string.IsNullOrWhiteSpace(sourceStemPath) || !File.Exists(sourceStemPath))
            {
                error = $"Generated stem file is missing: {sourceStemPath}";
                return false;
            }

            string extension = Path.GetExtension(sourceStemPath);
            string entryName = TheoryPackageFormat.BuildStemEntryName(stem.id, extension);
            FileInfo stemInfo = new FileInfo(sourceStemPath);
            embeddedStems.Add(new TheoryStemAsset
            {
                id = stem.id,
                displayName = string.IsNullOrWhiteSpace(stem.displayName) ? StemSeparationService.FormatStemDisplayName(stem.id) : stem.displayName,
                entry = entryName,
                contentType = BuildAudioContentType(sourceStemPath),
                sourceSizeBytes = stemInfo.Length,
                provider = string.IsNullOrWhiteSpace(stemManifest.provider) ? "demucs" : stemManifest.provider,
                model = stemManifest.model ?? string.Empty,
                generatedAtUtcTicks = stemManifest.generatedAtUtcTicks
            });
            sourcePathsByEntry[NormalizeEntryName(entryName)] = sourceStemPath;
        }

        HashSet<string> oldStemEntries = new HashSet<string>(
            (manifest.stems ?? new List<TheoryStemAsset>())
            .Where(stem => stem != null && !string.IsNullOrWhiteSpace(stem.entry))
            .Select(stem => NormalizeEntryName(stem.entry)),
            StringComparer.OrdinalIgnoreCase);

        manifest.stems = embeddedStems;
        manifest.modifiedAtUtcTicks = DateTime.UtcNow.Ticks;

        string tempPath = packagePath + ".tmp";
        if (File.Exists(tempPath))
            File.Delete(tempPath);

        try
        {
            using (ZipArchive sourceArchive = ZipFile.OpenRead(packagePath))
            using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (ZipArchive destinationArchive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                HashSet<string> newStemEntries = new HashSet<string>(sourcePathsByEntry.Keys, StringComparer.OrdinalIgnoreCase);
                foreach (ZipArchiveEntry sourceEntry in sourceArchive.Entries)
                {
                    string entryName = NormalizeEntryName(sourceEntry.FullName);
                    if (string.IsNullOrWhiteSpace(entryName) ||
                        string.Equals(entryName, TheoryPackageFormat.ManifestEntryName, StringComparison.OrdinalIgnoreCase) ||
                        oldStemEntries.Contains(entryName) ||
                        newStemEntries.Contains(entryName))
                    {
                        continue;
                    }

                    CopyEntry(sourceEntry, destinationArchive, entryName);
                }

                WriteTextEntry(destinationArchive, TheoryPackageFormat.ManifestEntryName, JsonUtility.ToJson(manifest, true));
                foreach (KeyValuePair<string, string> stemEntry in sourcePathsByEntry)
                    WriteFileEntry(destinationArchive, stemEntry.Key, stemEntry.Value);
            }

            ReplacePackageFile(tempPath, packagePath);
            return true;
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempPath);
            error = ex.Message;
            Debug.LogWarning($"[TheoryPackage] Failed to embed stems in '{packagePath}': {ex.Message}");
            return false;
        }
    }

    public static bool EntryExists(string packagePath, string entryName)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath) || string.IsNullOrWhiteSpace(entryName))
            return false;

        try
        {
            using (ZipArchive archive = ZipFile.OpenRead(packagePath))
            {
                ZipArchiveEntry entry = archive.GetEntry(NormalizeEntryName(entryName));
                return entry != null && !string.IsNullOrEmpty(entry.Name);
            }
        }
        catch
        {
            return false;
        }
    }

    public static bool TryExtractEntry(string packagePath, string entryName, string destinationPath, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(packagePath) || string.IsNullOrWhiteSpace(entryName) || string.IsNullOrWhiteSpace(destinationPath))
        {
            error = "Theory package extract request is incomplete.";
            return false;
        }

        try
        {
            using (ZipArchive archive = ZipFile.OpenRead(packagePath))
            {
                ZipArchiveEntry entry = archive.GetEntry(NormalizeEntryName(entryName));
                if (entry == null)
                {
                    error = $"Theory package entry '{entryName}' was not found.";
                    return false;
                }

                string directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                entry.ExtractToFile(destinationPath, true);
                return true;
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Debug.LogWarning($"[TheoryPackage] Failed to extract '{entryName}' from '{packagePath}': {ex.Message}");
            return false;
        }
    }

    public static bool TryReadTextEntry(string packagePath, string entryName, out string text, out string error)
    {
        text = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(packagePath) || string.IsNullOrWhiteSpace(entryName))
        {
            error = "Theory package text entry request is incomplete.";
            return false;
        }

        try
        {
            text = ReadTextEntry(packagePath, entryName);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Debug.LogWarning($"[TheoryPackage] Failed to read '{entryName}' from '{packagePath}': {ex.Message}");
            return false;
        }
    }

    public static bool TryRewriteManifest(string packagePath, TheorySongManifest manifest, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            error = "Theory package was not found.";
            return false;
        }

        if (manifest == null)
        {
            error = "Theory package manifest is missing.";
            return false;
        }

        string tempPath = packagePath + ".tmp";
        if (File.Exists(tempPath))
            File.Delete(tempPath);

        try
        {
            manifest.EnsureDefaults();
            using (ZipArchive sourceArchive = ZipFile.OpenRead(packagePath))
            using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (ZipArchive destinationArchive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (ZipArchiveEntry sourceEntry in sourceArchive.Entries)
                {
                    string entryName = NormalizeEntryName(sourceEntry.FullName);
                    if (string.IsNullOrWhiteSpace(entryName) ||
                        string.Equals(entryName, TheoryPackageFormat.ManifestEntryName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    CopyEntry(sourceEntry, destinationArchive, entryName);
                }

                WriteTextEntry(destinationArchive, TheoryPackageFormat.ManifestEntryName, JsonUtility.ToJson(manifest, true));
            }

            ReplacePackageFile(tempPath, packagePath);
            return true;
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempPath);
            error = ex.Message;
            Debug.LogWarning($"[TheoryPackage] Failed to rewrite manifest in '{packagePath}': {ex.Message}");
            return false;
        }
    }

    private static string ReadTextEntry(string packagePath, string entryName)
    {
        using (ZipArchive archive = ZipFile.OpenRead(packagePath))
        {
            ZipArchiveEntry entry = archive.GetEntry(NormalizeEntryName(entryName));
            if (entry == null)
                throw new FileNotFoundException($"Package entry '{entryName}' was not found.", entryName);

            using (Stream stream = entry.Open())
            using (StreamReader reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }
    }

    private static void WriteTextEntry(ZipArchive archive, string entryName, string text)
    {
        ZipArchiveEntry entry = archive.CreateEntry(NormalizeEntryName(entryName), System.IO.Compression.CompressionLevel.Optimal);
        using (Stream stream = entry.Open())
        using (StreamWriter writer = new StreamWriter(stream))
            writer.Write(text ?? string.Empty);
    }

    private static void WriteFileEntry(ZipArchive archive, string entryName, string sourcePath)
    {
        ZipArchiveEntry entry = archive.CreateEntry(NormalizeEntryName(entryName), System.IO.Compression.CompressionLevel.Fastest);
        using (Stream input = File.OpenRead(sourcePath))
        using (Stream output = entry.Open())
            input.CopyTo(output);
    }

    private static void CopyEntry(ZipArchiveEntry sourceEntry, ZipArchive archive, string entryName)
    {
        if (sourceEntry == null || string.IsNullOrWhiteSpace(entryName) || string.IsNullOrEmpty(sourceEntry.Name))
            return;

        ZipArchiveEntry entry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
        using (Stream input = sourceEntry.Open())
        using (Stream output = entry.Open())
            input.CopyTo(output);
    }

    private static void CopyPreservedPackageEntries(
        ZipArchive destinationArchive,
        string sourcePackagePath,
        IEnumerable<string> entryNames,
        HashSet<string> writtenEntries)
    {
        if (destinationArchive == null ||
            string.IsNullOrWhiteSpace(sourcePackagePath) ||
            !File.Exists(sourcePackagePath) ||
            entryNames == null)
        {
            return;
        }

        using (ZipArchive sourceArchive = ZipFile.OpenRead(sourcePackagePath))
        {
            foreach (string rawEntryName in entryNames)
            {
                string entryName = NormalizeEntryName(rawEntryName);
                if (string.IsNullOrWhiteSpace(entryName) ||
                    writtenEntries.Contains(entryName))
                {
                    continue;
                }

                ZipArchiveEntry sourceEntry = sourceArchive.GetEntry(entryName);
                if (sourceEntry == null || string.IsNullOrEmpty(sourceEntry.Name))
                    continue;

                CopyEntry(sourceEntry, destinationArchive, entryName);
                writtenEntries.Add(entryName);
            }
        }
    }

    private static void ReplacePackageFile(string tempPath, string packagePath)
    {
        if (string.IsNullOrWhiteSpace(tempPath) || string.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentException("Package replacement paths are incomplete.");

        if (!File.Exists(tempPath))
            throw new FileNotFoundException("Temporary package file was not created.", tempPath);

        if (!File.Exists(packagePath))
        {
            File.Move(tempPath, packagePath);
            return;
        }

        string backupPath = packagePath + ".bak";
        TryDeleteFile(backupPath);

        try
        {
            File.Replace(tempPath, packagePath, backupPath, true);
            TryDeleteFile(backupPath);
        }
        catch (PlatformNotSupportedException)
        {
            ReplacePackageFileWithBackupMove(tempPath, packagePath, backupPath);
        }
        catch (IOException)
        {
            ReplacePackageFileWithBackupMove(tempPath, packagePath, backupPath);
        }
    }

    private static void ReplacePackageFileWithBackupMove(string tempPath, string packagePath, string backupPath)
    {
        File.Move(packagePath, backupPath);
        try
        {
            File.Move(tempPath, packagePath);
            TryDeleteFile(backupPath);
        }
        catch
        {
            if (!File.Exists(packagePath) && File.Exists(backupPath))
                File.Move(backupPath, packagePath);
            throw;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static string BuildAudioContentType(string path)
    {
        string extension = Path.GetExtension(path)?.ToLowerInvariant();
        switch (extension)
        {
            case ".ogg": return "audio/ogg";
            case ".wav": return "audio/wav";
            case ".mp3": return "audio/mpeg";
            case ".flac": return "audio/flac";
            default: return "application/octet-stream";
        }
    }

    private static string FindArrangementEntry(TheorySongManifest manifest, TheoryArrangementData arrangement)
    {
        if (manifest?.arrangements == null || arrangement == null)
            return string.Empty;

        for (int i = 0; i < manifest.arrangements.Count; i++)
        {
            TheoryArrangementSummary summary = manifest.arrangements[i];
            if (summary == null)
                continue;

            if (string.Equals(summary.arrangementId ?? string.Empty, arrangement.arrangementId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                return summary.entry;
        }

        return string.Empty;
    }

    private static string NormalizeEntryName(string entryName)
    {
        return (entryName ?? string.Empty).Replace('\\', '/').TrimStart('/');
    }
}
