using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public static class StringTheoryPlatform
{
    public static bool IsWindows
    {
        get
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return true;
#else
            return Application.platform == RuntimePlatform.WindowsPlayer ||
                   Application.platform == RuntimePlatform.WindowsEditor;
#endif
        }
    }

    public static bool IsMacOS
    {
        get
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return true;
#else
            return Application.platform == RuntimePlatform.OSXPlayer ||
                   Application.platform == RuntimePlatform.OSXEditor;
#endif
        }
    }

    public static bool IsLinux
    {
        get
        {
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            return true;
#else
            return Application.platform == RuntimePlatform.LinuxPlayer ||
                   Application.platform == RuntimePlatform.LinuxEditor;
#endif
        }
    }

    public static StringComparison PathComparison => IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    public static StringComparer PathComparer => IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static string AlphaTabRenderHelperFileName => IsWindows ? "AlphaTabRenderHelper.exe" : "AlphaTabRenderHelper";
    public static string RocksmithImportToolFileName => IsWindows ? "RocksmithImportTool.exe" : "RocksmithImportTool";
    public static string StemSeparatorCommandFileName => IsWindows ? "StemSeparator.exe" : "StemSeparator";
    public static string StemSeparatorPythonFileName => IsWindows ? "python.exe" : "python3";
    public static string StemRuntimePackageFileName
    {
        get
        {
            if (IsMacOS)
                return "stem-separator-runtime-macos-universal.zip";
            if (IsLinux)
                return "stem-separator-runtime-linux-x64.zip";
            return "stem-separator-runtime-win-x64.zip";
        }
    }

    public static string ToneLabExecutableFileName => IsWindows ? "ToneLab.exe" : "ToneLab";
    public static string ToneLabMacAppBundleName => "ToneLab.app";

    public static string DotNetRuntimeIdentifier
    {
        get
        {
            Architecture architecture = RuntimeInformation.ProcessArchitecture;
            if (IsMacOS)
                return architecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
            if (IsLinux)
                return architecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
            return architecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
        }
    }

    public static string GetToneLabExecutablePath(string persistentToneLabDirectory, string distFolderName, string distAppFolderName)
    {
        if (IsMacOS)
        {
            return Path.Combine(
                persistentToneLabDirectory,
                distFolderName,
                ToneLabMacAppBundleName,
                "Contents",
                "MacOS",
                ToneLabExecutableFileName);
        }

        return Path.Combine(persistentToneLabDirectory, distFolderName, distAppFolderName, ToneLabExecutableFileName);
    }

    public static string GetStemSeparatorPythonPath(string runtimeRoot, string pythonFolderName)
    {
        if (IsWindows)
            return Path.Combine(runtimeRoot, pythonFolderName, StemSeparatorPythonFileName);

        return Path.Combine(runtimeRoot, pythonFolderName, "bin", StemSeparatorPythonFileName);
    }

    public static bool TryOpenFolder(string folderPath, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            error = "Folder path was empty.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(folderPath);

            if (IsWindows)
            {
                Process.Start("explorer.exe", folderPath.Replace('/', '\\'));
                return true;
            }

            if (IsMacOS)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = QuoteArgument(folderPath),
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                return true;
            }

            if (IsLinux)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = QuoteArgument(folderPath),
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                return true;
            }

            Application.OpenURL(new Uri(folderPath).AbsoluteUri);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            try
            {
                Application.OpenURL(new Uri(folderPath).AbsoluteUri);
                error = string.Empty;
                return true;
            }
            catch (Exception fallbackEx)
            {
                error = fallbackEx.Message;
                return false;
            }
        }
    }

    public static bool TryPickFolder(string title, string initialDirectory, out string selectedPath)
    {
        selectedPath = string.Empty;

#if UNITY_EDITOR
        string editorSelection = UnityEditor.EditorUtility.OpenFolderPanel(
            string.IsNullOrWhiteSpace(title) ? "Select Folder" : title,
            string.IsNullOrWhiteSpace(initialDirectory) ? string.Empty : initialDirectory,
            string.Empty);
        if (!string.IsNullOrWhiteSpace(editorSelection))
        {
            selectedPath = editorSelection;
            return true;
        }

        return false;
#else
        if (IsWindows)
            return WindowsFolderPicker.TryPickFolder(title, initialDirectory, out selectedPath);
        if (IsMacOS)
            return TryPickMacFolder(title, initialDirectory, out selectedPath);

        Debug.LogWarning("[Platform] Native folder picker is not implemented for this player platform yet.");
        return false;
#endif
    }

    private static bool TryPickMacFolder(string title, string initialDirectory, out string selectedPath)
    {
        selectedPath = string.Empty;
        try
        {
            string prompt = EscapeAppleScriptString(string.IsNullOrWhiteSpace(title) ? "Select Folder" : title);
            string script = $"POSIX path of (choose folder with prompt \"{prompt}\"";
            if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
                script += $" default location POSIX file \"{EscapeAppleScriptString(initialDirectory)}\"";
            script += ")";

            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = "-e " + QuoteArgument(script),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process == null)
                return false;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                return false;

            selectedPath = (output ?? string.Empty).Trim();
            if (selectedPath.EndsWith("/", StringComparison.Ordinal) && selectedPath.Length > 1)
                selectedPath = selectedPath.TrimEnd('/');

            return !string.IsNullOrWhiteSpace(selectedPath);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[Platform] macOS folder picker failed: {ex.Message}");
            selectedPath = string.Empty;
            return false;
        }
    }

    private static string EscapeAppleScriptString(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public static void TryEnsureExecutable(string filePath)
    {
        if (IsWindows || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        try
        {
            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x {QuoteArgument(filePath)}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[Platform] Failed to mark executable '{filePath}': {ex.Message}");
        }
    }

    public static bool IsPathInsideDirectory(string path, string directory)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directory))
            return false;

        string fullDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullPath = Path.GetFullPath(path);
        string directoryWithSeparator = fullDirectory + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(directoryWithSeparator, PathComparison) ||
               string.Equals(fullPath, fullDirectory, PathComparison);
    }

    public static string QuoteArgument(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }
}
