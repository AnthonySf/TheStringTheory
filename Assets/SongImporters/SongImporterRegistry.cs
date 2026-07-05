using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

public static class SongImporterRegistry
{
    public const int SupportedApiVersion = 1;
    public const string ManifestFileName = "importer.json";

    private const string ImportWorkFolderName = "ImporterWork";
    private const string ImportFailureLogFolderName = "ImporterLogs";
    private const int ImporterProcessTimeoutMilliseconds = 60 * 60 * 1000;
    private static readonly object cacheLock = new object();
    private static List<SongImporterDescriptor> cachedImporters;

    public static void ClearCache()
    {
        lock (cacheLock)
            cachedImporters = null;
    }

    public static List<SongImporterDescriptor> GetAvailableImporters(bool forceRefresh = false)
    {
        lock (cacheLock)
        {
            if (!forceRefresh && cachedImporters != null)
                return CloneDescriptors(cachedImporters);

            cachedImporters = DiscoverImportersUnchecked();
            return CloneDescriptors(cachedImporters);
        }
    }

    public static List<string> GetSupportedExtensions(bool forceRefresh = false)
    {
        return GetAvailableImporters(forceRefresh)
            .SelectMany(importer => importer.Extensions ?? new List<string>())
            .Select(NormalizeExtension)
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<SongImporterFolderMatch> GetMatchingFolderImporters(string folderPath)
    {
        List<SongImporterFolderMatch> matches = new List<SongImporterFolderMatch>();
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return matches;

        foreach (SongImporterDescriptor importer in GetAvailableImporters())
        {
            if (importer?.FolderSignatures == null || importer.FolderSignatures.Count == 0)
                continue;
            if (!TryResolveEntrypoint(importer, out _, out _, out _))
                continue;

            for (int i = 0; i < importer.FolderSignatures.Count; i++)
            {
                SongImporterFolderSignature signature = importer.FolderSignatures[i];
                if (!FolderMatchesSignature(folderPath, signature))
                    continue;

                matches.Add(new SongImporterFolderMatch
                {
                    importer = importer,
                    signature = CloneFolderSignature(signature),
                    folderPath = Path.GetFullPath(folderPath)
                });
            }
        }

        return matches
            .OrderByDescending(match => match.importer?.Priority ?? 0)
            .ThenBy(match => match.importer?.DisplayName ?? match.importer?.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.signature?.displayName ?? match.signature?.id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool TryGetImporterForSource(string sourcePath, out SongImporterDescriptor importer)
    {
        importer = null;
        if (!string.IsNullOrWhiteSpace(sourcePath) && Directory.Exists(sourcePath))
        {
            SongImporterFolderMatch folderMatch = GetMatchingFolderImporters(sourcePath).FirstOrDefault();
            importer = folderMatch?.importer;
            return importer != null;
        }

        string extension = NormalizeExtension(Path.GetExtension(sourcePath));
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        importer = GetAvailableImporters()
            .Where(candidate => candidate?.Extensions != null &&
                                candidate.Extensions.Any(importerExtension => string.Equals(NormalizeExtension(importerExtension), extension, StringComparison.OrdinalIgnoreCase)) &&
                                TryResolveEntrypoint(candidate, out _, out _, out _))
            .OrderByDescending(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.DisplayName ?? candidate.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        return importer != null;
    }

    public static bool TryGetImporterById(string importerId, out SongImporterDescriptor importer)
    {
        importer = null;
        if (string.IsNullOrWhiteSpace(importerId))
            return false;

        importer = GetAvailableImporters()
            .FirstOrDefault(candidate => string.Equals(candidate?.Id ?? string.Empty, importerId, StringComparison.OrdinalIgnoreCase));
        return importer != null;
    }

    public static bool ImporterHasUsableEntrypoint(SongImporterDescriptor importer)
    {
        return TryResolveEntrypoint(importer, out _, out _, out _);
    }

    /// <summary>
    /// Computes a portable, format-agnostic identity for an importer source: a digest of the
    /// relative file names and sizes (for folders) or the file name and size (for single files).
    /// It survives moves and copies, requires no knowledge of the source format, and changes
    /// whenever the content actually changes. Importer tools may pre-fill a richer fingerprint
    /// in the .theory manifest; the stamped value is only set when the importer left it empty.
    /// </summary>
    public static string ComputeSourceContentFingerprint(string sourcePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return string.Empty;

            List<string> entries = new List<string>();
            if (File.Exists(sourcePath))
            {
                FileInfo info = new FileInfo(sourcePath);
                entries.Add(info.Name.ToLowerInvariant() + "|" + info.Length);
            }
            else if (Directory.Exists(sourcePath))
            {
                string root = Path.GetFullPath(sourcePath);
                foreach (string filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (IsGeneratedTheoryPackageInDirectorySource(filePath) ||
                        filePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string relativePath = Path.GetRelativePath(root, filePath)
                        .Replace('\\', '/')
                        .ToLowerInvariant();
                    entries.Add(relativePath + "|" + new FileInfo(filePath).Length);
                }
            }
            else
            {
                return string.Empty;
            }

            if (entries.Count == 0)
                return string.Empty;

            entries.Sort(StringComparer.Ordinal);
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", entries)));
                StringBuilder builder = new StringBuilder("v1:", 3 + hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    public static HashSet<string> GetInstalledImporterCacheFolderNames(bool forceRefresh = false)
    {
        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (SongImporterDescriptor importer in GetAvailableImporters(forceRefresh))
        {
            if (importer?.CacheFolderNames == null)
                continue;

            for (int i = 0; i < importer.CacheFolderNames.Count; i++)
            {
                string name = importer.CacheFolderNames[i];
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name.Trim());
            }
        }

        return names;
    }

    public static bool ConvertSourceToTheoryPackage(
        SongImporterConversionRequest request,
        out SongImporterConversionResult result,
        out string error)
    {
        result = new SongImporterConversionResult();
        error = string.Empty;

        if (request == null)
        {
            error = "Importer conversion request is missing.";
            return false;
        }

        string sourcePath = request.sourcePath?.Trim();
        bool sourceIsFile = !string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath);
        bool sourceIsDirectory = !string.IsNullOrWhiteSpace(sourcePath) && Directory.Exists(sourcePath);
        if (string.IsNullOrWhiteSpace(sourcePath) || (!sourceIsFile && !sourceIsDirectory))
        {
            error = "Importer source file or folder was not found.";
            return false;
        }

        SongImporterDescriptor importer;
        if (!string.IsNullOrWhiteSpace(request.importerId))
        {
            if (!TryGetImporterById(request.importerId, out importer))
            {
                error = $"Importer '{request.importerId}' is not available.";
                return false;
            }

            if (sourceIsFile)
            {
                string sourceExtension = NormalizeExtension(Path.GetExtension(sourcePath));
                if (importer.Extensions == null ||
                    !importer.Extensions.Any(extension => string.Equals(NormalizeExtension(extension), sourceExtension, StringComparison.OrdinalIgnoreCase)))
                {
                    error = $"Importer '{importer.DisplayName}' does not support '{sourceExtension}'.";
                    return false;
                }
            }
            else if (!ImporterMatchesFolder(importer, sourcePath))
            {
                error = $"Importer '{importer.DisplayName}' does not support this folder source.";
                return false;
            }
        }
        else if (!TryGetImporterForSource(sourcePath, out importer))
        {
            error = sourceIsDirectory
                ? "No importer is available for this folder source."
                : $"No importer is available for '{Path.GetExtension(sourcePath)}'.";
            return false;
        }

        if (!TryResolveEntrypoint(importer, out string executablePath, out string argumentsTemplate, out string entrypointError))
        {
            error = entrypointError;
            return false;
        }

        string outputPackagePath = ResolveOutputPackagePath(sourcePath, request);
        if (string.IsNullOrWhiteSpace(outputPackagePath))
        {
            error = "Importer output package path could not be resolved.";
            return false;
        }

        if (request.rejectChartEditorOutputDirectory && IsInChartEditorSaveDirectory(outputPackagePath))
        {
            error = "Library .theory conversion cannot write into the chart editor save folder.";
            return false;
        }

        if (File.Exists(outputPackagePath) && !request.overwriteExisting)
        {
            error = $"Importer output already exists: {outputPackagePath}";
            return false;
        }

        string workDirectory = BuildWorkDirectory(importer, sourcePath);
        try
        {
            if (Directory.Exists(workDirectory))
                Directory.Delete(workDirectory, recursive: true);
            Directory.CreateDirectory(workDirectory);
            string outputDirectory = Path.GetDirectoryName(outputPackagePath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            string arguments = BuildArguments(
                argumentsTemplate,
                sourcePath,
                outputPackagePath,
                workDirectory,
                importer.DirectoryPath);

            if (!RunImporterProcess(executablePath, arguments, importer.DirectoryPath, out string processOutput))
            {
                string logPath = WriteFailureLog(importer, sourcePath, executablePath, arguments, processOutput);
                error = $"Importer '{importer.DisplayName}' failed. Failure log: {logPath}";
                return false;
            }

            if (!File.Exists(outputPackagePath))
            {
                string logPath = WriteFailureLog(importer, sourcePath, executablePath, arguments, "Importer completed but did not create the expected .theory file.");
                error = $"Importer '{importer.DisplayName}' did not produce a .theory package. Failure log: {logPath}";
                return false;
            }

            if (!StampImporterProvenance(outputPackagePath, importer, sourcePath, out string stampError))
            {
                error = stampError;
                return false;
            }

            if (request.validatePackage &&
                !ChartEditorTheoryConversionService.ValidateTheoryPackage(outputPackagePath, null, request.requireAudio, result.warnings, out error))
            {
                return false;
            }

            result.importer = importer;
            result.sourcePath = Path.GetFullPath(sourcePath);
            result.packagePath = Path.GetFullPath(outputPackagePath);
            result.packageWasWritten = true;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
        }
    }

    private static List<SongImporterDescriptor> DiscoverImportersUnchecked()
    {
        List<SongImporterDescriptor> result = new List<SongImporterDescriptor>();
        HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string root in GetImporterRoots())
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                continue;

            foreach (string manifestPath in EnumerateImporterManifests(root))
            {
                if (!TryReadManifest(manifestPath, out SongImporterDescriptor descriptor, out string error))
                {
                    Debug.LogWarning($"[SongImporter] Skipping importer manifest '{manifestPath}': {error}");
                    continue;
                }

                if (seenIds.Contains(descriptor.Id))
                    continue;

                result.Add(descriptor);
                seenIds.Add(descriptor.Id);
            }
        }

        return result
            .OrderByDescending(importer => importer.Priority)
            .ThenBy(importer => importer.DisplayName ?? importer.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> GetImporterRoots()
    {
        yield return ExternalContentPaths.StreamingImportersDirectory;
        yield return ExternalContentPaths.PersistentImportersDirectory;
    }

    private static IEnumerable<string> EnumerateImporterManifests(string root)
    {
        try
        {
            return Directory.GetFiles(root, ManifestFileName, SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongImporter] Failed to inspect importer folder '{root}': {ex.Message}");
            return Array.Empty<string>();
        }
    }

    private static bool TryReadManifest(string manifestPath, out SongImporterDescriptor descriptor, out string error)
    {
        descriptor = null;
        error = string.Empty;
        try
        {
            SongImporterManifest manifest = JsonUtility.FromJson<SongImporterManifest>(File.ReadAllText(manifestPath));
            if (manifest == null)
            {
                error = "manifest could not be parsed.";
                return false;
            }

            if (!manifest.enabled)
            {
                error = "importer is disabled.";
                return false;
            }

            if (manifest.apiVersion <= 0 || manifest.apiVersion > SupportedApiVersion)
            {
                error = $"unsupported importer API version {manifest.apiVersion}.";
                return false;
            }

            string id = manifest.id?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                error = "importer id is missing.";
                return false;
            }

            List<string> extensions = (manifest.extensions ?? new List<string>())
                .Select(NormalizeExtension)
                .Where(extension => !string.IsNullOrWhiteSpace(extension))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<SongImporterFolderSignature> folderSignatures = (manifest.folderSignatures ?? new List<SongImporterFolderSignature>())
                .Where(IsUsableFolderSignature)
                .Select(CloneFolderSignature)
                .ToList();

            if (extensions.Count == 0 && folderSignatures.Count == 0)
            {
                error = "importer has no supported file extensions or folder signatures.";
                return false;
            }

            List<SongImporterEntrypoint> entrypoints = (manifest.entrypoints ?? new List<SongImporterEntrypoint>())
                .Where(entrypoint => entrypoint != null && !string.IsNullOrWhiteSpace(entrypoint.path))
                .ToList();
            if (entrypoints.Count == 0)
            {
                error = "importer has no usable entrypoint.";
                return false;
            }

            List<string> cacheFolderNames = (manifest.cacheFolderNames ?? new List<string>())
                .Select(name => name?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name) &&
                               name.IndexOf('/') < 0 &&
                               name.IndexOf('\\') < 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            descriptor = new SongImporterDescriptor
            {
                Id = id,
                DisplayName = string.IsNullOrWhiteSpace(manifest.displayName) ? id : manifest.displayName.Trim(),
                Version = manifest.version ?? string.Empty,
                ApiVersion = manifest.apiVersion,
                Priority = manifest.priority,
                DirectoryPath = Path.GetDirectoryName(manifestPath) ?? string.Empty,
                ManifestPath = manifestPath,
                Extensions = extensions,
                FolderSignatures = folderSignatures,
                Entrypoints = entrypoints,
                CacheFolderNames = cacheFolderNames
            };
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool ImporterMatchesFolder(SongImporterDescriptor importer, string folderPath)
    {
        if (importer?.FolderSignatures == null || importer.FolderSignatures.Count == 0)
            return false;

        for (int i = 0; i < importer.FolderSignatures.Count; i++)
        {
            if (FolderMatchesSignature(folderPath, importer.FolderSignatures[i]))
                return true;
        }

        return false;
    }

    private static bool IsUsableFolderSignature(SongImporterFolderSignature signature)
    {
        if (signature == null)
            return false;

        return HasAnyValue(signature.requiredFiles) ||
               HasAnyValue(signature.anyFiles) ||
               HasAnyValue(signature.requiredFilePatterns) ||
               HasAnyValue(signature.anyFilePatterns);
    }

    private static bool FolderMatchesSignature(string folderPath, SongImporterFolderSignature signature)
    {
        if (string.IsNullOrWhiteSpace(folderPath) ||
            !Directory.Exists(folderPath) ||
            !IsUsableFolderSignature(signature))
        {
            return false;
        }

        if (!AllExactFilesExist(folderPath, signature.requiredFiles))
            return false;
        if (!AllFilePatternsMatch(folderPath, signature.requiredFilePatterns, signature.recursive))
            return false;

        bool hasAnyCriteria = HasAnyValue(signature.anyFiles) || HasAnyValue(signature.anyFilePatterns);
        if (hasAnyCriteria &&
            !AnyExactFileExists(folderPath, signature.anyFiles) &&
            !AnyFilePatternMatches(folderPath, signature.anyFilePatterns, signature.recursive))
        {
            return false;
        }

        return true;
    }

    private static bool AllExactFilesExist(string folderPath, List<string> relativePaths)
    {
        if (!HasAnyValue(relativePaths))
            return true;

        for (int i = 0; i < relativePaths.Count; i++)
        {
            string relativePath = NormalizeRelativeSignaturePath(relativePaths[i]);
            if (string.IsNullOrWhiteSpace(relativePath))
                continue;
            if (!File.Exists(Path.Combine(folderPath, relativePath)))
                return false;
        }

        return true;
    }

    private static bool AnyExactFileExists(string folderPath, List<string> relativePaths)
    {
        if (!HasAnyValue(relativePaths))
            return false;

        for (int i = 0; i < relativePaths.Count; i++)
        {
            string relativePath = NormalizeRelativeSignaturePath(relativePaths[i]);
            if (string.IsNullOrWhiteSpace(relativePath))
                continue;
            if (File.Exists(Path.Combine(folderPath, relativePath)))
                return true;
        }

        return false;
    }

    private static bool AllFilePatternsMatch(string folderPath, List<string> patterns, bool recursive)
    {
        if (!HasAnyValue(patterns))
            return true;

        for (int i = 0; i < patterns.Count; i++)
        {
            if (!FilePatternMatches(folderPath, patterns[i], recursive))
                return false;
        }

        return true;
    }

    private static bool AnyFilePatternMatches(string folderPath, List<string> patterns, bool recursive)
    {
        if (!HasAnyValue(patterns))
            return false;

        for (int i = 0; i < patterns.Count; i++)
        {
            if (FilePatternMatches(folderPath, patterns[i], recursive))
                return true;
        }

        return false;
    }

    private static bool FilePatternMatches(string folderPath, string pattern, bool recursive)
    {
        string normalizedPattern = NormalizeRelativeSignaturePath(pattern);
        if (string.IsNullOrWhiteSpace(normalizedPattern))
            return false;

        try
        {
            string relativeDirectory = Path.GetDirectoryName(normalizedPattern);
            string filePattern = Path.GetFileName(normalizedPattern);
            if (string.IsNullOrWhiteSpace(filePattern))
                return false;

            string searchRoot = string.IsNullOrWhiteSpace(relativeDirectory)
                ? folderPath
                : Path.Combine(folderPath, relativeDirectory);
            if (!Directory.Exists(searchRoot))
                return false;

            SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.EnumerateFiles(searchRoot, filePattern, searchOption).Any();
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeRelativeSignaturePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string normalized = path.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        return Path.IsPathRooted(normalized) ? string.Empty : normalized;
    }

    private static bool HasAnyValue(List<string> values)
    {
        return values != null && values.Any(value => !string.IsNullOrWhiteSpace(value));
    }

    private static SongImporterFolderSignature CloneFolderSignature(SongImporterFolderSignature source)
    {
        if (source == null)
            return null;

        return new SongImporterFolderSignature
        {
            id = source.id ?? string.Empty,
            displayName = source.displayName ?? string.Empty,
            recursive = source.recursive,
            requiredFiles = new List<string>(source.requiredFiles ?? new List<string>()),
            anyFiles = new List<string>(source.anyFiles ?? new List<string>()),
            requiredFilePatterns = new List<string>(source.requiredFilePatterns ?? new List<string>()),
            anyFilePatterns = new List<string>(source.anyFilePatterns ?? new List<string>())
        };
    }

    private static bool TryResolveEntrypoint(
        SongImporterDescriptor importer,
        out string executablePath,
        out string arguments,
        out string error)
    {
        executablePath = string.Empty;
        arguments = string.Empty;
        error = string.Empty;
        if (importer?.Entrypoints == null || importer.Entrypoints.Count == 0)
        {
            error = $"Importer '{importer?.DisplayName ?? "Unknown"}' has no entrypoint.";
            return false;
        }

        string runtimeIdentifier = StringTheoryPlatform.DotNetRuntimeIdentifier;
        SongImporterEntrypoint entrypoint = importer.Entrypoints.FirstOrDefault(candidate =>
            string.Equals(candidate.runtimeIdentifier ?? string.Empty, runtimeIdentifier, StringComparison.OrdinalIgnoreCase));
        entrypoint ??= importer.Entrypoints.FirstOrDefault(candidate => string.IsNullOrWhiteSpace(candidate.runtimeIdentifier) ||
                                                                        string.Equals(candidate.runtimeIdentifier, "any", StringComparison.OrdinalIgnoreCase) ||
                                                                        string.Equals(candidate.runtimeIdentifier, "*", StringComparison.Ordinal));
        if (entrypoint == null)
        {
            error = $"Importer '{importer.DisplayName}' has no entrypoint for {runtimeIdentifier}.";
            return false;
        }

        executablePath = ResolveImporterPath(importer.DirectoryPath, entrypoint.path);
        arguments = string.IsNullOrWhiteSpace(entrypoint.arguments)
            ? "import-theory --source {sourcePath} --output {outputTheoryPath} --work {workDirectory}"
            : entrypoint.arguments;

        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            error = $"Importer executable was not found: {executablePath}";
            return false;
        }

        return true;
    }

    private static string ResolveImporterPath(string importerDirectory, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string replaced = path
            .Replace("{streamingRoot}", ExternalContentPaths.StreamingRoot)
            .Replace("{persistentRoot}", ExternalContentPaths.PersistentRoot)
            .Replace("{importerDirectory}", importerDirectory ?? string.Empty);
        if (Path.IsPathRooted(replaced))
            return Path.GetFullPath(replaced);

        return Path.GetFullPath(Path.Combine(importerDirectory ?? string.Empty, replaced));
    }

    private static string BuildArguments(
        string template,
        string sourcePath,
        string outputTheoryPath,
        string workDirectory,
        string importerDirectory)
    {
        return (template ?? string.Empty)
            .Replace("{sourcePath}", StringTheoryPlatform.QuoteArgument(Path.GetFullPath(sourcePath)))
            .Replace("{outputTheoryPath}", StringTheoryPlatform.QuoteArgument(Path.GetFullPath(outputTheoryPath)))
            .Replace("{workDirectory}", StringTheoryPlatform.QuoteArgument(Path.GetFullPath(workDirectory)))
            .Replace("{importerDirectory}", StringTheoryPlatform.QuoteArgument(Path.GetFullPath(importerDirectory ?? string.Empty)));
    }

    private static bool RunImporterProcess(string executablePath, string arguments, string workingDirectory, out string processOutput)
    {
        StringBuilder output = new StringBuilder();
        try
        {
            StringTheoryPlatform.TryEnsureExecutable(executablePath);
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments ?? string.Empty,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Path.GetDirectoryName(executablePath)
                    : workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = startInfo, EnableRaisingEvents = true })
            {
                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        output.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        output.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                bool exited = process.WaitForExit(ImporterProcessTimeoutMilliseconds);
                if (!exited)
                {
                    output.AppendLine($"Importer timed out after {ImporterProcessTimeoutMilliseconds / 60000} minute(s).");
                    TryKillImporterProcess(process, output);
                    processOutput = output.ToString();
                    return false;
                }

                process.WaitForExit();
                processOutput = output.ToString();
                return process.ExitCode == 0;
            }
        }
        catch (Exception ex)
        {
            processOutput = output.AppendLine(ex.ToString()).ToString();
            return false;
        }
    }

    private static void TryKillImporterProcess(Process process, StringBuilder output)
    {
        if (process == null)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill();
        }
        catch (Exception ex)
        {
            output?.AppendLine($"Failed to stop timed-out importer process: {ex.Message}");
            return;
        }

        try
        {
            process.WaitForExit(5000);
        }
        catch
        {
        }
    }

    private static string ResolveOutputPackagePath(string sourcePath, SongImporterConversionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.outputPackagePath))
        {
            string explicitPath = request.outputPackagePath.Trim();
            if (string.IsNullOrWhiteSpace(Path.GetExtension(explicitPath)))
                explicitPath += TheoryPackageFormat.Extension;
            return Path.GetFullPath(explicitPath);
        }

        string outputDirectory = request.outputDirectory;
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            outputDirectory = request.useLibrarySongsDirectory
                ? ExternalContentPaths.PersistentSongsDirectory
                : Path.GetDirectoryName(sourcePath);
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
            return string.Empty;

        string baseName = TheoryPackageFormat.SanitizeEntryFileName(GetSourceDisplayName(sourcePath), "imported-song");
        string candidate = Path.Combine(outputDirectory, $"{baseName}{TheoryPackageFormat.Extension}");
        return request.overwriteExisting
            ? Path.GetFullPath(candidate)
            : BuildAvailableFilePath(outputDirectory, baseName, TheoryPackageFormat.Extension);
    }

    private static bool StampImporterProvenance(
        string packagePath,
        SongImporterDescriptor importer,
        string sourcePath,
        out string error)
    {
        error = string.Empty;
        if (!TheoryPackageIO.TryReadManifest(packagePath, out TheorySongManifest manifest, out error))
            return false;

        DateTime now = DateTime.UtcNow;
        manifest.provenance ??= new TheoryImportProvenance();
        if (string.IsNullOrWhiteSpace(manifest.provenance.sourceType))
            manifest.provenance.sourceType = importer.Id;
        if (string.IsNullOrWhiteSpace(manifest.provenance.sourceDisplayName))
            manifest.provenance.sourceDisplayName = GetSourceDisplayName(sourcePath);
        manifest.provenance.sourcePath = Path.GetFullPath(sourcePath);
        manifest.provenance.sourceLastWriteUtcTicks = TryGetLastWriteUtcTicks(sourcePath);
        manifest.provenance.sourceSizeBytes = TryGetFileSize(sourcePath);
        if (string.IsNullOrWhiteSpace(manifest.provenance.sourceContentFingerprint))
            manifest.provenance.sourceContentFingerprint = ComputeSourceContentFingerprint(sourcePath);
        if (manifest.provenance.importedAtUtcTicks <= 0L)
            manifest.provenance.importedAtUtcTicks = now.Ticks;
        if (string.IsNullOrWhiteSpace(manifest.provenance.converterName))
            manifest.provenance.converterName = importer.DisplayName;
        if (string.IsNullOrWhiteSpace(manifest.provenance.converterVersion))
            manifest.provenance.converterVersion = importer.Version ?? string.Empty;
        if (manifest.createdAtUtcTicks <= 0L)
            manifest.createdAtUtcTicks = now.Ticks;
        manifest.modifiedAtUtcTicks = now.Ticks;

        return TheoryPackageIO.TryRewriteManifest(packagePath, manifest, out error);
    }

    private static string BuildWorkDirectory(SongImporterDescriptor importer, string sourcePath)
    {
        string safeImporter = TheoryPackageFormat.SanitizeEntryFileName(importer?.Id ?? "importer", "importer");
        string safeSource = TheoryPackageFormat.SanitizeEntryFileName(GetSourceDisplayName(sourcePath), "source");
        return Path.Combine(
            ExternalContentPaths.PersistentRoot,
            ImportWorkFolderName,
            $"{safeImporter}_{safeSource}_{Guid.NewGuid():N}");
    }

    private static bool IsInChartEditorSaveDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string chartEditorDirectory = Path.GetFullPath(Path.Combine(
            ExternalContentPaths.PersistentSongsDirectory,
            ChartEditorProjectStore.ChartEditorSaveFolderName));
        string fullPath = Path.GetFullPath(path);
        return string.Equals(fullPath, chartEditorDirectory, StringTheoryPlatform.PathComparison) ||
               fullPath.StartsWith(chartEditorDirectory + Path.DirectorySeparatorChar, StringTheoryPlatform.PathComparison) ||
               fullPath.StartsWith(chartEditorDirectory + Path.AltDirectorySeparatorChar, StringTheoryPlatform.PathComparison);
    }

    private static string BuildAvailableFilePath(string directory, string baseName, string extension)
    {
        Directory.CreateDirectory(directory);
        string normalizedExtension = string.IsNullOrWhiteSpace(extension) ? string.Empty : extension;
        if (!string.IsNullOrWhiteSpace(normalizedExtension) && !normalizedExtension.StartsWith(".", StringComparison.Ordinal))
            normalizedExtension = "." + normalizedExtension;

        string candidate = Path.Combine(directory, $"{baseName}{normalizedExtension}");
        if (!File.Exists(candidate))
            return Path.GetFullPath(candidate);

        for (int i = 2; i < 10000; i++)
        {
            candidate = Path.Combine(directory, $"{baseName} ({i}){normalizedExtension}");
            if (!File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return Path.GetFullPath(Path.Combine(directory, $"{baseName}_{Guid.NewGuid():N}{normalizedExtension}"));
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        string trimmed = extension.Trim().ToLowerInvariant();
        return trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed : "." + trimmed;
    }

    private static List<SongImporterDescriptor> CloneDescriptors(List<SongImporterDescriptor> source)
    {
        return (source ?? new List<SongImporterDescriptor>())
            .Select(CloneDescriptor)
            .Where(importer => importer != null)
            .ToList();
    }

    private static SongImporterDescriptor CloneDescriptor(SongImporterDescriptor source)
    {
        if (source == null)
            return null;

        return new SongImporterDescriptor
        {
            Id = source.Id,
            DisplayName = source.DisplayName,
            Version = source.Version,
            ApiVersion = source.ApiVersion,
            Priority = source.Priority,
            DirectoryPath = source.DirectoryPath,
            ManifestPath = source.ManifestPath,
            Extensions = new List<string>(source.Extensions ?? new List<string>()),
            FolderSignatures = (source.FolderSignatures ?? new List<SongImporterFolderSignature>())
                .Select(CloneFolderSignature)
                .Where(signature => signature != null)
                .ToList(),
            Entrypoints = new List<SongImporterEntrypoint>(source.Entrypoints ?? new List<SongImporterEntrypoint>()),
            CacheFolderNames = new List<string>(source.CacheFolderNames ?? new List<string>())
        };
    }

    private static string WriteFailureLog(
        SongImporterDescriptor importer,
        string sourcePath,
        string executablePath,
        string arguments,
        string details)
    {
        try
        {
            string directory = Path.Combine(ExternalContentPaths.PersistentRoot, ImportFailureLogFolderName);
            Directory.CreateDirectory(directory);
            string fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{TheoryPackageFormat.SanitizeEntryFileName(importer?.Id ?? "importer", "importer")}.log";
            string path = Path.Combine(directory, fileName);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Importer: {importer?.DisplayName ?? importer?.Id ?? "Unknown"}");
            builder.AppendLine($"ImporterId: {importer?.Id ?? string.Empty}");
            builder.AppendLine($"SourcePath: {sourcePath}");
            builder.AppendLine($"ExecutablePath: {executablePath}");
            builder.AppendLine($"Arguments: {arguments}");
            builder.AppendLine();
            builder.AppendLine(details ?? string.Empty);
            File.WriteAllText(path, builder.ToString());
            return path;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static long TryGetLastWriteUtcTicks(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return 0L;
            if (File.Exists(path))
                return File.GetLastWriteTimeUtc(path).Ticks;
            if (!Directory.Exists(path))
                return 0L;

            long result = Directory.GetLastWriteTimeUtc(path).Ticks;
            foreach (string filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (IsGeneratedTheoryPackageInDirectorySource(filePath))
                    continue;

                result = Math.Max(result, File.GetLastWriteTimeUtc(filePath).Ticks);
            }

            return result;
        }
        catch
        {
            return 0L;
        }
    }

    private static long TryGetFileSize(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return 0L;
            if (File.Exists(path))
                return new FileInfo(path).Length;
            if (!Directory.Exists(path))
                return 0L;

            long result = 0L;
            foreach (string filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (IsGeneratedTheoryPackageInDirectorySource(filePath))
                    continue;

                result += new FileInfo(filePath).Length;
            }

            return result;
        }
        catch
        {
            return 0L;
        }
    }

    private static bool IsGeneratedTheoryPackageInDirectorySource(string filePath)
    {
        return string.Equals(Path.GetExtension(filePath), TheoryPackageFormat.Extension, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSourceDisplayName(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            return string.Empty;

        string trimmed = sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (Directory.Exists(trimmed))
            return Path.GetFileName(trimmed);

        return Path.GetFileNameWithoutExtension(trimmed);
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongImporter] Failed to remove importer work folder '{directory}': {ex.Message}");
        }
    }
}
