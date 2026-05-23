#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class StringTheoryBuildAutomation
{
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
