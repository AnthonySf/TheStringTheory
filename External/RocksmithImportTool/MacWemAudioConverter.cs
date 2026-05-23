using System.Diagnostics;

internal static class MacWemAudioConverter
{
    private const string ToolDirectoryName = "Tools";
    private const string VgmstreamEnvironmentVariable = "STRINGTHEORY_VGMSTREAM_CLI";
    private const string FfmpegEnvironmentVariable = "STRINGTHEORY_FFMPEG";
    private const int ExternalDecoderTimeoutMilliseconds = 120_000;

    public static void ConvertWithFallback(string wemPath)
    {
        try
        {
            Convert(wemPath);
            return;
        }
        catch (Exception macDecoderException)
        {
            Console.Error.WriteLine($"[RocksmithImportTool] Bundled macOS WEM decoder failed for '{Path.GetFileName(wemPath)}': {macDecoderException.Message}");

            try
            {
                Rocksmith2014.Audio.Conversion.wemToOgg(wemPath);
                return;
            }
            catch (Exception legacyException)
            {
                throw new InvalidOperationException(
                    $"Failed to convert WEM audio '{Path.GetFileName(wemPath)}' on macOS. " +
                    $"Bundled decoder: {macDecoderException.Message} " +
                    $"Legacy converter: {legacyException.Message}",
                    legacyException);
            }
        }
    }

    private static void Convert(string wemPath)
    {
        string? vgmstreamPath = ResolveDecoderExecutable(VgmstreamEnvironmentVariable, "vgmstream-cli");
        string? ffmpegPath = ResolveDecoderExecutable(FfmpegEnvironmentVariable, "ffmpeg");

        if (string.IsNullOrWhiteSpace(vgmstreamPath))
            throw new FileNotFoundException($"macOS WEM decoder not found. Bundle '{ToolDirectoryName}/vgmstream-cli' next to RocksmithImportTool or set {VgmstreamEnvironmentVariable}.");

        if (string.IsNullOrWhiteSpace(ffmpegPath))
            throw new FileNotFoundException($"macOS ffmpeg not found. Bundle '{ToolDirectoryName}/ffmpeg' next to RocksmithImportTool or set {FfmpegEnvironmentVariable}.");

        string oggPath = Path.ChangeExtension(wemPath, ".ogg");
        string tempWavPath = Path.Combine(Path.GetTempPath(), $"StringTheoryImport_{Guid.NewGuid():N}.wav");

        try
        {
            ToolRunResult vgmstreamResult = RunDecoderTool(
                vgmstreamPath,
                new[] { "-o", tempWavPath, wemPath },
                Path.GetDirectoryName(wemPath));

            if (!vgmstreamResult.Succeeded || !IsUsableFile(tempWavPath))
            {
                throw new InvalidOperationException(
                    $"vgmstream-cli failed with exit code {vgmstreamResult.ExitCode}: {SummarizeToolOutput(vgmstreamResult.Output)}");
            }

            ToolRunResult ffmpegResult = RunDecoderTool(
                ffmpegPath,
                new[] { "-y", "-hide_banner", "-loglevel", "error", "-i", tempWavPath, "-c:a", "libvorbis", "-q:a", "5", oggPath },
                Path.GetDirectoryName(wemPath));

            if (!ffmpegResult.Succeeded && ffmpegResult.Output.Contains("Unknown encoder 'libvorbis'", StringComparison.OrdinalIgnoreCase))
            {
                ffmpegResult = RunDecoderTool(
                    ffmpegPath,
                    new[] { "-y", "-hide_banner", "-loglevel", "error", "-i", tempWavPath, "-c:a", "vorbis", "-strict", "experimental", "-q:a", "5", oggPath },
                    Path.GetDirectoryName(wemPath));
            }

            if (!ffmpegResult.Succeeded || !IsUsableFile(oggPath))
            {
                throw new InvalidOperationException(
                    $"ffmpeg failed with exit code {ffmpegResult.ExitCode}: {SummarizeToolOutput(ffmpegResult.Output)}");
            }
        }
        catch
        {
            TryDeleteFile(oggPath);
            throw;
        }
        finally
        {
            TryDeleteFile(tempWavPath);
        }
    }

    private static string? ResolveDecoderExecutable(string environmentVariable, string executableName)
    {
        string? configuredPath = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            string? explicitPath = ResolveExplicitOrPathExecutable(configuredPath.Trim());
            if (!string.IsNullOrWhiteSpace(explicitPath))
                return explicitPath;

            throw new FileNotFoundException($"{environmentVariable} is set but does not resolve to an executable file: {Path.GetFileName(configuredPath)}");
        }

        string bundledPath = Path.Combine(AppContext.BaseDirectory, ToolDirectoryName, executableName);
        if (File.Exists(bundledPath))
            return bundledPath;

        return FindExecutableOnPath(executableName);
    }

    private static string? ResolveExplicitOrPathExecutable(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        if (candidate.Contains(Path.DirectorySeparatorChar) ||
            (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar && candidate.Contains(Path.AltDirectorySeparatorChar)))
        {
            string path = ExpandConfiguredPath(candidate);
            return File.Exists(path) ? path : null;
        }

        return FindExecutableOnPath(candidate);
    }

    private static string ExpandConfiguredPath(string path)
    {
        string expanded = Environment.ExpandEnvironmentVariables(path);
        if (expanded == "~" || expanded.StartsWith("~/", StringComparison.Ordinal))
        {
            string? home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrWhiteSpace(home))
                expanded = Path.Combine(home, expanded.Length == 1 ? string.Empty : expanded.Substring(2));
        }

        return Path.GetFullPath(expanded);
    }

    private static string? FindExecutableOnPath(string executableName)
    {
        string? pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
            return null;

        foreach (string directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static ToolRunResult RunDecoderTool(string executablePath, IReadOnlyList<string> arguments, string? workingDirectory)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? AppContext.BaseDirectory : workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using Process process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException($"Failed to start {Path.GetFileName(executablePath)}.");

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(ExternalDecoderTimeoutMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // The process may have exited between WaitForExit and Kill.
            }

            throw new TimeoutException($"{Path.GetFileName(executablePath)} timed out after {ExternalDecoderTimeoutMilliseconds / 1000} seconds.");
        }

        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();
        return new ToolRunResult(process.ExitCode, string.Concat(output, error));
    }

    private static bool IsUsableFile(string path)
    {
        return File.Exists(path) && new FileInfo(path).Length > 0;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup for temporary conversion files.
        }
    }

    private static string SummarizeToolOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return "no output";

        string compact = output.Replace("\r", " ").Replace("\n", " ").Trim();
        return compact.Length <= 500 ? compact : compact.Substring(0, 500) + "...";
    }

    private readonly record struct ToolRunResult(int ExitCode, string Output)
    {
        public bool Succeeded => ExitCode == 0;
    }
}
