using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class ChartEditorFilePicker
{
    private const int MaxPathBuffer = 4096;

    public static bool TryPickChartFile(out string path)
    {
        return TryPickInputFile(
            "Select Chart File",
            "Chart Files",
            "*.theory;*.gp;*.gp3;*.gp4;*.gp5;*.gp8;*.gpx;*.musicxml;*.xml",
            out path);
    }

    public static bool TryPickNotationFile(out string path)
    {
        return TryPickInputFile(
            "Select Chart File",
            "GP / MusicXML Files",
            "*.gp;*.gp3;*.gp4;*.gp5;*.gp8;*.gpx;*.musicxml;*.xml",
            out path);
    }

    public static bool TryPickTheoryPackageFile(out string path)
    {
        return TryPickInputFile("Open Theory Package", "Theory Packages", "*.theory", out path);
    }

    public static bool TryPickAudioFile(out string path)
    {
        return TryPickInputFile(
            "Select Audio File",
            "Audio Files",
            "*.mp3;*.wav;*.ogg;*.flac;*.m4a;*.aiff;*.aif",
            out path);
    }

    public static bool TryPickImageFile(out string path)
    {
        return TryPickInputFile(
            "Select Song Image",
            "Image Files",
            "*.png;*.jpg;*.jpeg",
            out path);
    }

    public static bool TryPickImporterSourceFile(SongImporterDescriptor importer, out string path)
    {
        string label = string.IsNullOrWhiteSpace(importer?.DisplayName)
            ? "Importer Files"
            : $"{importer.DisplayName} Files";
        string patterns = BuildExtensionPattern(importer?.Extensions);
        return TryPickInputFile($"Select {label}", label, patterns, out path);
    }

    public static bool TryPickImporterSourceFolder(
        SongImporterDescriptor importer,
        SongImporterFolderSignature signature,
        out string path)
    {
        string label = BuildImporterFolderLabel(importer, signature);
        return TryPickFolder($"Select {label}", ResolveInputInitialDirectory(), out path);
    }

    public static bool TryPickProjectFile(out string path)
    {
        return TryPickFile(
            "Open Chart Editor Project",
            "String Theory Chart Projects",
            "*.stchart.json",
            ResolveInitialDirectory(ChartEditorProjectStore.ProjectsDirectory, createDirectory: true),
            out path);
    }

    public static bool TryPickFolder(string title, string initialDirectory, out string path)
    {
        path = string.Empty;
        string resolvedInitialDirectory = ResolveInitialDirectory(initialDirectory, createDirectory: true);
#if UNITY_EDITOR
        string selected = EditorUtility.OpenFolderPanel(title, resolvedInitialDirectory, string.Empty);
        if (string.IsNullOrWhiteSpace(selected))
            return false;

        path = selected;
        return true;
#else
        return WindowsFolderPicker.TryPickFolder(title, resolvedInitialDirectory, out path);
#endif
    }

    private static bool TryPickInputFile(string title, string filterName, string filterPattern, out string path)
    {
        return TryPickFile(title, filterName, filterPattern, ResolveInputInitialDirectory(), out path);
    }

    private static string BuildExtensionPattern(IReadOnlyList<string> extensions)
    {
        List<string> patterns = (extensions ?? Array.Empty<string>())
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(extension => extension.Trim())
            .Select(extension => extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension)
            .Select(extension => "*" + extension)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return patterns.Count == 0 ? "*.*" : string.Join(";", patterns);
    }

    private static string BuildImporterFolderLabel(
        SongImporterDescriptor importer,
        SongImporterFolderSignature signature)
    {
        if (!string.IsNullOrWhiteSpace(signature?.displayName))
            return $"{signature.displayName.Trim()} Folder";

        if (!string.IsNullOrWhiteSpace(importer?.DisplayName))
            return $"{importer.DisplayName.Trim()} Folder";

        return "Importer Folder";
    }

    private static bool TryPickFile(string title, string filterName, string filterPattern, string initialDirectory, out string path)
    {
        path = string.Empty;
        string resolvedInitialDirectory = ResolveInitialDirectory(initialDirectory, createDirectory: false);
#if UNITY_EDITOR
        string editorPattern = filterPattern.Replace("*.", string.Empty).Replace("*", string.Empty).Replace(";", ",");
        string[] filters =
        {
            filterName, editorPattern,
            "All Files", "*"
        };
        string selected = EditorUtility.OpenFilePanelWithFilters(title, resolvedInitialDirectory, filters);
        if (string.IsNullOrWhiteSpace(selected))
            return false;

        path = selected;
        return true;
#elif UNITY_STANDALONE_WIN
        return TryPickWindowsFile(title, filterName, filterPattern, resolvedInitialDirectory, out path);
#else
        Debug.LogWarning("[ChartEditor] File picker is not implemented for this platform.");
        return false;
#endif
    }

    private static string ResolveInputInitialDirectory()
    {
        // Always start in the songs folder: reopening wherever the user last
        // browsed (possibly sessions ago) made the pickers feel random.
        return ResolveInitialDirectory(ExternalContentPaths.PersistentSongsDirectory, createDirectory: true);
    }

    private static string ResolveInitialDirectory(string directory, bool createDirectory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            directory = ExternalContentPaths.PersistentSongsDirectory;

        try
        {
            if (createDirectory)
                Directory.CreateDirectory(directory);

            if (Directory.Exists(directory))
                return Path.GetFullPath(directory);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ChartEditor] Could not prepare file picker folder '{directory}': {ex.Message}");
        }

        return string.Empty;
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private static bool TryPickWindowsFile(string title, string filterName, string filterPattern, string initialDirectory, out string path)
    {
        // Shown by an out-of-process helper: in-process native dialogs crash
        // the Unity player (see WindowsOutOfProcessDialogs).
        string filter = $"{filterName}|{filterPattern}|All Files|*.*";
        return WindowsOutOfProcessDialogs.TryPickFile(title, filter, initialDirectory, out path);
    }
#endif
}
