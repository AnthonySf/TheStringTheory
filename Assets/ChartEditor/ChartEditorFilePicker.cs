using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class ChartEditorFilePicker
{
    private const int MaxPathBuffer = 4096;
    private static string lastInputDirectory;

    public static bool TryPickChartFile(out string path)
    {
        return TryPickInputFile(
            "Select Chart File",
            "Chart Files",
            "*.theory;*.gp;*.gp3;*.gp4;*.gp5;*.gpx;*.musicxml;*.xml",
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

    public static bool TryPickPsarcFile(out string path)
    {
        return TryPickInputFile("Select PSARC File", "PSARC Files", "*.psarc", out path);
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
        RememberInputDirectory(path);
        return true;
#else
        bool picked = WindowsFolderPicker.TryPickFolder(title, resolvedInitialDirectory, out path);
        if (picked)
            RememberInputDirectory(path);
        return picked;
#endif
    }

    private static bool TryPickInputFile(string title, string filterName, string filterPattern, out string path)
    {
        bool picked = TryPickFile(title, filterName, filterPattern, ResolveInputInitialDirectory(), out path);
        if (picked)
            RememberInputDirectory(path);
        return picked;
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
        if (!string.IsNullOrWhiteSpace(lastInputDirectory) && Directory.Exists(lastInputDirectory))
            return lastInputDirectory;

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

    private static void RememberInputDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        string directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return;

        lastInputDirectory = directory;
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public StringBuilder lpstrFile;
        public int nMaxFile;
        public string lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int flagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

    private static bool TryPickWindowsFile(string title, string filterName, string filterPattern, string initialDirectory, out string path)
    {
        path = string.Empty;
        string filter = $"{filterName}\0{filterPattern}\0All Files\0*.*\0\0";
        OpenFileName ofn = new OpenFileName
        {
            lStructSize = Marshal.SizeOf(typeof(OpenFileName)),
            lpstrFilter = filter,
            lpstrFile = new StringBuilder(MaxPathBuffer),
            nMaxFile = MaxPathBuffer,
            lpstrInitialDir = initialDirectory,
            lpstrTitle = title,
            flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008
        };

        if (!GetOpenFileName(ofn))
            return false;

        path = ofn.lpstrFile.ToString();
        return !string.IsNullOrWhiteSpace(path);
    }
#endif
}
