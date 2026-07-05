using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class ChartEditorProjectStore
{
    public const string ProjectExtension = ".stchart.json";
    public const string ChartEditorSaveFolderName = "chart editor";

    private const string ProjectFolderName = "ChartEditorProjects";
    private const string ExportFolderPrefix = "song_";
    private const string LegacyTheorySaveFolderName = "theory";

    public static string ProjectsDirectory => Path.Combine(ExternalContentPaths.PersistentRoot, ProjectFolderName);

    public static bool SaveTheoryPackage(ChartEditorProject project, bool saveAs, out string packagePath, out string error)
    {
        packagePath = string.Empty;
        error = string.Empty;
        if (project == null)
        {
            error = "No chart editor project is loaded.";
            return false;
        }

        project.EnsureDefaults();
        ChartEditorToneScopeService.NormalizeProjectToneGroups(project);
        if (project.tracks == null || project.tracks.Count == 0)
        {
            error = "Project has no tracks to save.";
            return false;
        }

        try
        {
            ChartEditorRuntimeNoteSanitizer.SanitizeProjectNotes(project);
            ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: true);

            packagePath = !saveAs ? ResolveCurrentTheoryPackagePath(project, createDirectory: true) : string.Empty;
            if (string.IsNullOrWhiteSpace(packagePath))
                packagePath = BuildDefaultTheoryPackagePath(project);

            if (!TheoryChartEditorExporter.WriteProjectPackage(project, packagePath, out error))
                return false;

            project.sourceKind = ChartEditorSourceKind.TheoryPackage;
            project.sourcePath = packagePath;
            project.sourceFolder = Path.GetDirectoryName(packagePath) ?? string.Empty;
            project.savedProjectPath = string.Empty;
            project.dirty = false;
            SongLibraryService.ClearCache();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool SaveProject(ChartEditorProject project, out string savedPath, out string error)
    {
        savedPath = string.Empty;
        error = string.Empty;
        if (project == null)
        {
            error = "No chart editor project is loaded.";
            return false;
        }

        project.EnsureDefaults();
        ChartEditorToneScopeService.NormalizeProjectToneGroups(project);
        ChartEditorRuntimeNoteSanitizer.SanitizeProjectNotes(project);
        ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: true);
        try
        {
            Directory.CreateDirectory(ProjectsDirectory);
            string path = string.IsNullOrWhiteSpace(project.savedProjectPath)
                ? BuildAvailableFilePath(ProjectsDirectory, BuildProjectFileName(project), ProjectExtension)
                : project.savedProjectPath;

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            // The project is only clean once the file is actually on disk — a
            // failed or interrupted write must leave the editor knowing there
            // are unsaved changes.
            WriteTextFileAtomic(path, JsonUtility.ToJson(project, true));
            project.savedProjectPath = path;
            project.dirty = false;
            savedPath = path;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool CanSaveCurrentTheoryPackageInPlace(ChartEditorProject project)
    {
        return !string.IsNullOrWhiteSpace(ResolveCurrentTheoryPackagePath(project, createDirectory: false));
    }

    private static string ResolveCurrentTheoryPackagePath(ChartEditorProject project, bool createDirectory)
    {
        if (project == null ||
            project.sourceKind != ChartEditorSourceKind.TheoryPackage ||
            !TheoryPackageFormat.IsPackagePath(project.sourcePath))
        {
            return string.Empty;
        }

        string path = project.sourcePath;
        if (IsLegacyAutoSavePackagePath(path))
            return string.Empty;

        string directory = Path.GetDirectoryName(path);
        if (createDirectory && !string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        return path;
    }

    public static string GetTheoryPackageSaveDirectory(ChartEditorProject project, bool createDirectory = true)
    {
        string directory = ResolveChartEditorPackageDirectory(createDirectory);
        if (createDirectory && !string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        return directory;
    }

    private static string BuildDefaultTheoryPackagePath(ChartEditorProject project)
    {
        string directory = ResolveChartEditorPackageDirectory(createDirectories: true);
        Directory.CreateDirectory(directory);

        string baseName = BuildProjectFileName(project);
        return BuildAvailableFilePath(directory, baseName, TheoryPackageFormat.Extension);
    }

    private static string ResolveChartEditorPackageDirectory(bool createDirectories = true)
    {
        if (createDirectories)
            Directory.CreateDirectory(ExternalContentPaths.PersistentSongsDirectory);

        return Path.Combine(ExternalContentPaths.PersistentSongsDirectory, ChartEditorSaveFolderName);
    }

    private static bool IsLegacyAutoSavePackagePath(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            return false;

        string directory = Path.GetDirectoryName(packagePath);
        if (string.IsNullOrWhiteSpace(directory))
            return false;

        string songsRoot = Path.GetFullPath(ExternalContentPaths.PersistentSongsDirectory);
        string fullDirectory = Path.GetFullPath(directory);
        if (!IsSameOrChildPath(songsRoot, fullDirectory))
            return false;

        string sharedDirectory = Path.GetFullPath(ResolveChartEditorPackageDirectory(createDirectories: false));
        if (string.Equals(
                sharedDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                fullDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string folderName = Path.GetFileName(fullDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.Equals(folderName, LegacyTheorySaveFolderName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(folderName, ChartEditorSaveFolderName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameOrChildPath(string parentPath, string childPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(childPath))
            return false;

        string parent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string child = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(parent, child, StringComparison.OrdinalIgnoreCase) ||
               child.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               child.StartsWith(parent + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildAvailableFilePath(string directory, string baseName, string extension)
    {
        string safeBaseName = SanitizeFileName(baseName);
        string candidate = Path.Combine(directory, $"{safeBaseName}{extension}");
        if (!File.Exists(candidate))
            return candidate;

        for (int i = 2; i < 10000; i++)
        {
            candidate = Path.Combine(directory, $"{safeBaseName}_{i}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(directory, $"{safeBaseName}_{Guid.NewGuid():N}{extension}");
    }

    public static bool LoadProject(string path, out ChartEditorProject project, out string error)
    {
        project = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            error = "Project file was not found.";
            return false;
        }

        try
        {
            project = JsonUtility.FromJson<ChartEditorProject>(File.ReadAllText(path));
            if (project == null)
            {
                error = "Project file could not be parsed.";
                return false;
            }

            project.savedProjectPath = path;
            project.sourceKind = ChartEditorSourceKind.StringTheoryProject;
            project.EnsureDefaults();
            ChartEditorRuntimeNoteSanitizer.SanitizeProjectNotes(project);
            ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: true);
            project.dirty = false;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    // Project JSON writes go through a temp file + replace so a crash or full
    // disk mid-write can never truncate an existing save (.theory packages
    // already write this way in TheoryPackageIO).
    private static void WriteTextFileAtomic(string path, string contents)
    {
        string tempPath = path + ".tmp";
        if (File.Exists(tempPath))
            File.Delete(tempPath);

        File.WriteAllText(tempPath, contents);
        if (!File.Exists(path))
        {
            File.Move(tempPath, path);
            return;
        }

        string backupPath = path + ".bak";
        try
        {
            File.Replace(tempPath, path, backupPath, true);
        }
        catch (PlatformNotSupportedException)
        {
            File.Delete(path);
            File.Move(tempPath, path);
        }
        catch (IOException)
        {
            File.Delete(path);
            File.Move(tempPath, path);
        }

        try
        {
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
        catch
        {
            // A leftover .bak is harmless; never fail a successful save on it.
        }
    }

    public static bool ExportPlayableProject(ChartEditorProject project, out string exportDirectory, out string error)
    {
        return ExportPlayableProject(project, out exportDirectory, out _, out error);
    }

    public static bool ExportPlayableProject(ChartEditorProject project, out string exportDirectory, out string theoryPackagePath, out string error)
    {
        exportDirectory = string.Empty;
        theoryPackagePath = string.Empty;
        error = string.Empty;
        if (project == null)
        {
            error = "No chart editor project is loaded.";
            return false;
        }

        project.EnsureDefaults();
        ChartEditorToneScopeService.NormalizeProjectToneGroups(project);
        if (project.tracks == null || project.tracks.Count == 0)
        {
            error = "Project has no tracks to export.";
            return false;
        }

        try
        {
            ChartEditorRuntimeNoteSanitizer.SanitizeProjectNotes(project);
            ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: true);
            Directory.CreateDirectory(ExternalContentPaths.PersistentSongsDirectory);
            exportDirectory = Path.Combine(ExternalContentPaths.PersistentSongsDirectory, BuildExportDirectoryName(project));
            Directory.CreateDirectory(exportDirectory);

            WriteTextFileAtomic(Path.Combine(exportDirectory, "chart_project_export.stchart.json"), JsonUtility.ToJson(project, true));

            if (!TheoryChartEditorExporter.ExportProject(project, exportDirectory, out theoryPackagePath, out string theoryError))
            {
                error = $".theory package export failed: {theoryError}";
                return false;
            }

            SongLibraryService.ClearCache();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string BuildProjectFileName(ChartEditorProject project)
    {
        string title = project?.metadata?.title;
        string artist = project?.metadata?.artist;
        return SanitizeFileName($"{FirstNonEmpty(artist, "Unknown")}_{FirstNonEmpty(title, "Chart")}");
    }

    private static string BuildExportDirectoryName(ChartEditorProject project)
    {
        string id = string.IsNullOrWhiteSpace(project?.projectId) ? Guid.NewGuid().ToString("N") : project.projectId;
        return ExportFolderPrefix + BuildProjectFileName(project) + "_" + id.Substring(0, Math.Min(6, id.Length));
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "chart";

        char[] invalid = Path.GetInvalidFileNameChars();
        char[] chars = value.Trim().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (invalid.Contains(chars[i]) || char.IsWhiteSpace(chars[i]))
                chars[i] = '_';
        }

        string sanitized = new string(chars).Trim('_');
        while (sanitized.IndexOf("__", StringComparison.Ordinal) >= 0)
            sanitized = sanitized.Replace("__", "_");
        return string.IsNullOrWhiteSpace(sanitized) ? "chart" : sanitized;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return string.Empty;

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return string.Empty;
    }
}
