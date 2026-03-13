using System.IO;
using UnityEngine;

public static class ToneLabService
{
    public static bool EnsureToneLabRuntimeFiles()
    {
        ExternalContentBootstrap.EnsureRuntimeContentReady();

        if (!File.Exists(ExternalContentPaths.PersistentToneLabScriptPath))
        {
            Debug.LogWarning($"[ToneLabService] Tone Lab script not found at runtime path: {ExternalContentPaths.PersistentToneLabScriptPath}");
            return false;
        }

        return true;
    }

    public static string GetToneLabScriptPath()
    {
        return ExternalContentPaths.PersistentToneLabScriptPath;
    }
}
