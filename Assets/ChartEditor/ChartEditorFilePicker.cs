using System;
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
        return TryPickFile(
            "Select Chart File",
            "Chart Files",
            "*.theory;*.gp;*.gp3;*.gp4;*.gp5;*.gpx;*.musicxml;*.xml",
            out path);
    }

    public static bool TryPickTheoryPackageFile(out string path)
    {
        return TryPickFile("Open Theory Package", "Theory Packages", "*.theory", out path);
    }

    public static bool TryPickAudioFile(out string path)
    {
        return TryPickFile(
            "Select Audio File",
            "Audio Files",
            "*.mp3;*.wav;*.ogg;*.flac;*.m4a;*.aiff;*.aif",
            out path);
    }

    public static bool TryPickPsarcFile(out string path)
    {
        return TryPickFile("Select PSARC File", "PSARC Files", "*.psarc", out path);
    }

    public static bool TryPickProjectFile(out string path)
    {
        return TryPickFile("Open Chart Editor Project", "String Theory Chart Projects", "*.stchart.json", out path);
    }

    public static bool TryPickFolder(string title, string initialDirectory, out string path)
    {
        path = string.Empty;
#if UNITY_EDITOR
        string selected = EditorUtility.OpenFolderPanel(title, initialDirectory ?? string.Empty, string.Empty);
        if (string.IsNullOrWhiteSpace(selected))
            return false;

        path = selected;
        return true;
#else
        return WindowsFolderPicker.TryPickFolder(title, initialDirectory, out path);
#endif
    }

    private static bool TryPickFile(string title, string filterName, string filterPattern, out string path)
    {
        path = string.Empty;
#if UNITY_EDITOR
        string editorPattern = filterPattern.Replace("*.", string.Empty).Replace("*", string.Empty).Replace(";", ",");
        string[] filters =
        {
            filterName, editorPattern,
            "All Files", "*"
        };
        string selected = EditorUtility.OpenFilePanelWithFilters(title, string.Empty, filters);
        if (string.IsNullOrWhiteSpace(selected))
            return false;

        path = selected;
        return true;
#elif UNITY_STANDALONE_WIN
        return TryPickWindowsFile(title, filterName, filterPattern, out path);
#else
        Debug.LogWarning("[ChartEditor] File picker is not implemented for this platform.");
        return false;
#endif
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

    private static bool TryPickWindowsFile(string title, string filterName, string filterPattern, out string path)
    {
        path = string.Empty;
        string filter = $"{filterName}\0{filterPattern}\0All Files\0*.*\0\0";
        OpenFileName ofn = new OpenFileName
        {
            lStructSize = Marshal.SizeOf(typeof(OpenFileName)),
            lpstrFilter = filter,
            lpstrFile = new StringBuilder(MaxPathBuffer),
            nMaxFile = MaxPathBuffer,
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
