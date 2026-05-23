#if UNITY_EDITOR
using System;
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
            PatchMacInfoPlist(NormalizeMacAppOutputPath(pathToBuiltProject));
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

        PatchMacInfoPlist(outputPath);
    }

    private static void PatchMacInfoPlist(string appBundlePath)
    {
        string plistPath = Path.Combine(appBundlePath, "Contents", "Info.plist");
        if (!File.Exists(plistPath))
            throw new FileNotFoundException("macOS build completed but Info.plist was not found.", plistPath);

        XDocument document = XDocument.Load(plistPath, LoadOptions.PreserveWhitespace);
        XElement dict = document.Root?
            .Element("dict");
        if (dict == null)
            throw new InvalidOperationException($"macOS Info.plist has no root dict: {plistPath}");

        SetPlistString(
            dict,
            "NSMicrophoneUsageDescription",
            "String Theory uses microphone input for live guitar note detection and Tone Lab monitoring.");

        document.Save(plistPath);
    }

    private static void SetPlistString(XElement dict, string key, string value)
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
            }
            else
            {
                existingValue.Value = value;
            }

            return;
        }

        dict.Add(new XElement("key", key));
        dict.Add(new XElement("string", value));
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
