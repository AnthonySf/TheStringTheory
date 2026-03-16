using System.IO;
using UnityEngine;

public static class ToneLabService
{
    public static bool EnsureToneLabRuntimeFiles()
    {
        ExternalContentBootstrap.EnsureRuntimeContentReady();

        bool hasScript = File.Exists(ExternalContentPaths.PersistentToneLabScriptPath);
        bool hasExe = File.Exists(ExternalContentPaths.PersistentToneLabExePath);

        if (!hasScript)
            Debug.LogWarning($"[ToneLabService] Tone Lab script not found at runtime path: {ExternalContentPaths.PersistentToneLabScriptPath}");

        if (!hasExe)
            Debug.LogWarning($"[ToneLabService] Tone Lab executable not found at runtime path: {ExternalContentPaths.PersistentToneLabExePath}");

        return hasScript && hasExe;
    }

    public static string GetToneLabExecutablePath()
    {
        return ExternalContentPaths.PersistentToneLabExePath;
    }
}
