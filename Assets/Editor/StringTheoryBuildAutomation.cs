#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;

public static class StringTheoryBuildAutomation
{
    [PostProcessBuild(1000)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target == BuildTarget.StandaloneOSX)
            FinalizeMacAppBundle(NormalizeMacAppOutputPath(pathToBuiltProject));

        StripImporterAddonsFromBuild(target, pathToBuiltProject);
    }

    // Importer add-ons are distributed separately and must never ship inside
    // a player build. The project keeps them under
    // Assets/StreamingAssets/Importers for editor use, which Unity copies
    // into every build — so the copy is deleted from the build output
    // automatically here.
    private static void StripImporterAddonsFromBuild(BuildTarget target, string pathToBuiltProject)
    {
        try
        {
            string streamingAssets = ResolveBuildStreamingAssetsPath(target, pathToBuiltProject);
            if (string.IsNullOrWhiteSpace(streamingAssets))
                return;

            string importers = Path.Combine(streamingAssets, "Importers");
            if (!Directory.Exists(importers))
                return;

            Directory.Delete(importers, recursive: true);
            UnityEngine.Debug.Log($"[Build] Stripped importer add-ons from the build output: {importers}");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[Build] Could not strip importer add-ons from the build output: {ex.Message}");
        }
    }

    private static string ResolveBuildStreamingAssetsPath(BuildTarget target, string pathToBuiltProject)
    {
        if (string.IsNullOrWhiteSpace(pathToBuiltProject))
            return null;

        if (target == BuildTarget.StandaloneOSX)
        {
            string appPath = NormalizeMacAppOutputPath(pathToBuiltProject);
            return Path.Combine(appPath, "Contents", "Resources", "Data", "StreamingAssets");
        }

        if (target == BuildTarget.StandaloneWindows || target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneLinux64)
        {
            string directory = Path.GetDirectoryName(pathToBuiltProject);
            if (string.IsNullOrWhiteSpace(directory))
                return null;

            string dataFolder = Path.GetFileNameWithoutExtension(pathToBuiltProject) + "_Data";
            return Path.Combine(directory, dataFolder, "StreamingAssets");
        }

        return null;
    }

    public static void BuildMacOS()
    {
        string outputPath = GetArgument("-buildOutput");
        if (string.IsNullOrWhiteSpace(outputPath))
            outputPath = GetArgument("-customBuildPath");
        if (string.IsNullOrWhiteSpace(outputPath))
            outputPath = Environment.GetEnvironmentVariable("BUILD_OUTPUT_PATH");
        if (string.IsNullOrWhiteSpace(outputPath))
            outputPath = "build/StringTheory-macOS/StringTheory.app";

        outputPath = NormalizeMacAppOutputPath(outputPath);

        string architectureArgument = GetArgument("-macOSArchitecture");
        if (string.IsNullOrWhiteSpace(architectureArgument))
            architectureArgument = Environment.GetEnvironmentVariable("MACOS_UNITY_ARCHITECTURE");

        OSArchitecture architecture = ParseMacArchitecture(architectureArgument);
        PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, (int)architecture);
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();

        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled scenes were found in EditorBuildSettings.");

        string parentDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(parentDirectory))
            Directory.CreateDirectory(parentDirectory);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.StandaloneOSX,
            targetGroup = BuildTargetGroup.Standalone,
            subtarget = (int)StandaloneBuildSubtarget.Player,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"macOS build failed with result {report.summary.result} and {report.summary.totalErrors} errors.");
        }

        FinalizeMacAppBundle(outputPath);
    }

    private static void FinalizeMacAppBundle(string appBundlePath)
    {
        PatchMacInfoPlist(appBundlePath);
        AdHocSignMacAppBundle(appBundlePath);
    }

    private static bool PatchMacInfoPlist(string appBundlePath)
    {
        string plistPath = Path.Combine(appBundlePath, "Contents", "Info.plist");
        if (!File.Exists(plistPath))
            throw new FileNotFoundException("macOS build completed but Info.plist was not found.", plistPath);

        XDocument document = XDocument.Load(plistPath, LoadOptions.PreserveWhitespace);
        XElement dict = document.Root?
            .Element("dict");
        if (dict == null)
            throw new InvalidOperationException($"macOS Info.plist has no root dict: {plistPath}");

        bool changed = SetPlistString(
            dict,
            "NSMicrophoneUsageDescription",
            "String Theory uses microphone input for live guitar note detection and Tone Lab monitoring.");

        if (changed)
            document.Save(plistPath);

        return changed;
    }

    private static bool SetPlistString(XElement dict, string key, string value)
    {
        XElement existingKey = dict
            .Elements("key")
            .FirstOrDefault(element => string.Equals(element.Value, key, StringComparison.Ordinal));
        if (existingKey != null)
        {
            XElement existingValue = existingKey.ElementsAfterSelf().FirstOrDefault();
            if (existingValue == null || existingValue.Name.LocalName != "string")
            {
                existingKey.AddAfterSelf(new XElement("string", value));
                return true;
            }

            if (string.Equals(existingValue.Value, value, StringComparison.Ordinal))
                return false;

            existingValue.Value = value;
            return true;
        }

        dict.Add(new XElement("key", key));
        dict.Add(new XElement("string", value));
        return true;
    }

    private static void AdHocSignMacAppBundle(string appBundlePath)
    {
        if (UnityEngine.Application.platform != UnityEngine.RuntimePlatform.OSXEditor)
            return;

        if (IsTruthy(Environment.GetEnvironmentVariable("STRINGTHEORY_SKIP_ADHOC_MAC_SIGNING")))
        {
            UnityEngine.Debug.Log("Skipping ad-hoc macOS signing because STRINGTHEORY_SKIP_ADHOC_MAC_SIGNING is set.");
            return;
        }

        if (!Directory.Exists(appBundlePath))
            throw new DirectoryNotFoundException($"macOS app bundle was not found for signing: {appBundlePath}");

        RunMacTool(
            "/usr/bin/xattr",
            $"-dr com.apple.quarantine {Quote(appBundlePath)}",
            allowFailure: true);

        RunMacTool(
            "/usr/bin/codesign",
            $"--force --deep --sign - --timestamp=none {Quote(appBundlePath)}",
            allowFailure: false);

        RunMacTool(
            "/usr/bin/codesign",
            $"--verify --deep --strict --verbose=2 {Quote(appBundlePath)}",
            allowFailure: false);
    }

    private static void RunMacTool(string executable, string arguments, bool allowFailure)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = Process.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException($"Failed to start {executable}.");

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 0 || allowFailure)
        {
            if (!string.IsNullOrWhiteSpace(output))
                UnityEngine.Debug.Log(output.Trim());
            if (!string.IsNullOrWhiteSpace(error))
                UnityEngine.Debug.Log(error.Trim());
            return;
        }

        throw new InvalidOperationException($"{executable} failed with exit code {process.ExitCode}: {error.Trim()} {output.Trim()}");
    }

    private static bool IsTruthy(string value)
    {
        string normalized = (value ?? string.Empty).Trim();
        return string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static string Quote(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }

    private static string NormalizeMacAppOutputPath(string outputPath)
    {
        outputPath = outputPath.Replace('\\', '/').Trim();
        if (outputPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            return outputPath;

        string buildName = GetArgument("-customBuildName");
        if (string.IsNullOrWhiteSpace(buildName))
            buildName = "StringTheory";

        return Path.Combine(outputPath, $"{buildName}.app").Replace('\\', '/');
    }

    private static OSArchitecture ParseMacArchitecture(string value)
    {
        string normalized = (value ?? string.Empty)
            .Trim()
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(";", string.Empty)
            .ToLowerInvariant();

        return normalized switch
        {
            "arm64" => OSArchitecture.ARM64,
            "x64" => OSArchitecture.x64,
            "x8664" => OSArchitecture.x64,
            "universal" => OSArchitecture.x64ARM64,
            "x64arm64" => OSArchitecture.x64ARM64,
            "arm64x64" => OSArchitecture.x64ARM64,
            _ => OSArchitecture.x64ARM64
        };
    }

    private static string GetArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return string.Empty;
    }
}
#endif
