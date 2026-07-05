using System;

public static class WindowsFolderPicker
{
    public static bool TryPickFolder(string title, string initialDirectory, out string selectedPath)
    {
        selectedPath = string.Empty;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // Shown by an out-of-process helper: in-process native dialogs crash
        // the Unity player (see WindowsOutOfProcessDialogs).
        string normalizedInitialDirectory = NormalizeInitialDirectory(initialDirectory);
        return WindowsOutOfProcessDialogs.TryPickFolder(
            string.IsNullOrWhiteSpace(title) ? "Select Folder" : title,
            normalizedInitialDirectory,
            out selectedPath);
#else
        return false;
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private static string NormalizeInitialDirectory(string initialDirectory)
    {
        if (string.IsNullOrWhiteSpace(initialDirectory))
            return string.Empty;

        try
        {
            return System.IO.Path.GetFullPath(initialDirectory.Trim());
        }
        catch
        {
            return string.Empty;
        }
    }
#endif
}
