#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public sealed class StringTheoryBuildVersionSync : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    static StringTheoryBuildVersionSync()
    {
        SyncPlayerSettingsVersion();
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        SyncPlayerSettingsVersion();
    }

    private static void SyncPlayerSettingsVersion()
    {
        if (string.Equals(PlayerSettings.bundleVersion, StringTheoryBuildInfo.Version, StringComparison.Ordinal))
            return;

        string previousVersion = PlayerSettings.bundleVersion;
        PlayerSettings.bundleVersion = StringTheoryBuildInfo.Version;
        Debug.Log($"[BuildVersion] Synced PlayerSettings.bundleVersion from '{previousVersion}' to '{StringTheoryBuildInfo.Version}'.");
    }
}
#endif
