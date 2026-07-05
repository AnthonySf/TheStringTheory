using System;
using System.Diagnostics;
using System.Text;
using Debug = UnityEngine.Debug;

// File/folder pickers for Windows player builds.
//
// In-process native dialogs (GetOpenFileName / SHBrowseForFolder) are not
// survivable inside the Unity player on every machine: on the main thread the
// dialog's message pump re-enters Unity's window procedure until the stack
// overflows, and on a helper thread the shell's COM machinery can still take
// the process down. Instead the standard Windows dialogs are shown by a tiny
// PowerShell child process and the chosen path is read from its stdout — a
// separate process cannot crash, deadlock, or stack-overflow the game.
public static class WindowsOutOfProcessDialogs
{
    private const string FileDialogScript =
        "[Console]::OutputEncoding=[Text.Encoding]::UTF8;" +
        "Add-Type -AssemblyName System.Windows.Forms;" +
        "$d=New-Object System.Windows.Forms.OpenFileDialog;" +
        "$d.Title=$env:ST_DLG_TITLE;" +
        "$d.Filter=$env:ST_DLG_FILTER;" +
        "if($env:ST_DLG_DIR){$d.InitialDirectory=$env:ST_DLG_DIR};" +
        "$d.RestoreDirectory=$true;" +
        "$d.Multiselect=$false;" +
        "if($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){[Console]::Out.Write($d.FileName)}";

    private const string FolderDialogScript =
        "[Console]::OutputEncoding=[Text.Encoding]::UTF8;" +
        "Add-Type -AssemblyName System.Windows.Forms;" +
        "$d=New-Object System.Windows.Forms.FolderBrowserDialog;" +
        "$d.Description=$env:ST_DLG_TITLE;" +
        "$d.ShowNewFolderButton=$true;" +
        "if($env:ST_DLG_DIR){$d.SelectedPath=$env:ST_DLG_DIR};" +
        "if($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){[Console]::Out.Write($d.SelectedPath)}";

    public static bool TryPickFile(string title, string winFormsFilter, string initialDirectory, out string path)
    {
        return TryRunDialog(FileDialogScript, title, winFormsFilter, initialDirectory, out path);
    }

    public static bool TryPickFolder(string title, string initialDirectory, out string path)
    {
        return TryRunDialog(FolderDialogScript, title, null, initialDirectory, out path);
    }

    private static bool TryRunDialog(string script, string title, string filter, string initialDirectory, out string path)
    {
        path = string.Empty;
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -STA -Command \"" + script + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            // Values travel through environment variables so titles, filters,
            // and paths never need to be escaped into the command line.
            startInfo.EnvironmentVariables["ST_DLG_TITLE"] = title ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(filter))
                startInfo.EnvironmentVariables["ST_DLG_FILTER"] = filter;
            if (!string.IsNullOrWhiteSpace(initialDirectory))
                startInfo.EnvironmentVariables["ST_DLG_DIR"] = initialDirectory;

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                    return false;

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                string selected = output?.Trim();
                if (string.IsNullOrWhiteSpace(selected))
                    return false;

                path = selected;
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FileDialog] Could not show the system dialog: {ex.Message}");
            return false;
        }
    }
}
