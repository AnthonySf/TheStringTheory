using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public static class StringTheoryDiagnostics
{
    private const string DiagnosticsFolderName = "Diagnostics";
    private const string CurrentSessionFileName = "current_session.log";
    private const string PreviousSessionFileName = "previous_session.log";
    private const string NativeDetectorLogFileName = "native_detector.log";
    private const string PreviousNativeDetectorLogFileName = "previous_native_detector.log";
    private const string LatestSnapshotFileName = "latest_diagnostics_snapshot.txt";
    private const long MaxSessionLogBytes = 4L * 1024L * 1024L;
    private static readonly object LockObject = new object();
    private static readonly Regex WindowsUserPathRegex = new Regex(@"([A-Za-z]:\\Users\\)[^\\/\r\n]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MacUserPathRegex = new Regex(@"(/Users/)[^/\r\n]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LinuxUserPathRegex = new Regex(@"(/home/)[^/\r\n]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static bool initialized;
    private static StreamWriter writer;
    private static string diagnosticsDirectory = string.Empty;
    private static string currentSessionLogPath = string.Empty;
    private static string latestSnapshotPath = string.Empty;
    private static string sessionId = string.Empty;
    private static string redactionPersistentDataPath = string.Empty;
    private static string redactionStreamingAssetsPath = string.Empty;
    private static string redactionDataPath = string.Empty;
    private static long sessionLogBytesWritten;
    private static bool sessionLogTruncated;

    public static string DiagnosticsDirectory
    {
        get
        {
            EnsureInitialized("directory-query");
            return diagnosticsDirectory;
        }
    }

    public static string CurrentSessionLogPath
    {
        get
        {
            EnsureInitialized("current-log-query");
            return currentSessionLogPath;
        }
    }

    public static string LatestSnapshotPath
    {
        get
        {
            EnsureInitialized("snapshot-path-query");
            return latestSnapshotPath;
        }
    }

    public static string PreviousSessionLogPath
    {
        get
        {
            EnsureInitialized("previous-log-query");
            return Path.Combine(diagnosticsDirectory, PreviousSessionFileName);
        }
    }

    public static string NativeDetectorLogPath
    {
        get
        {
            EnsureInitialized("native-log-query");
            return Path.Combine(diagnosticsDirectory, NativeDetectorLogFileName);
        }
    }

    public static string PreviousNativeDetectorLogPath
    {
        get
        {
            EnsureInitialized("previous-native-log-query");
            return Path.Combine(diagnosticsDirectory, PreviousNativeDetectorLogFileName);
        }
    }

    public static string ConsoleLogPath => GetConsoleLogPath();

    public static string SessionId
    {
        get
        {
            EnsureInitialized("session-id-query");
            return sessionId;
        }
    }

    public static void EnsureInitialized(string reason)
    {
        if (initialized)
            return;

        lock (LockObject)
        {
            if (initialized)
                return;

            redactionPersistentDataPath = Application.persistentDataPath ?? string.Empty;
            redactionStreamingAssetsPath = Application.streamingAssetsPath ?? string.Empty;
            redactionDataPath = Application.dataPath ?? string.Empty;
            diagnosticsDirectory = Path.Combine(redactionPersistentDataPath, DiagnosticsFolderName);
            currentSessionLogPath = Path.Combine(diagnosticsDirectory, CurrentSessionFileName);
            latestSnapshotPath = Path.Combine(diagnosticsDirectory, LatestSnapshotFileName);
            sessionId = Guid.NewGuid().ToString("N");

            try
            {
                Directory.CreateDirectory(diagnosticsDirectory);
                string previousSessionPath = Path.Combine(diagnosticsDirectory, PreviousSessionFileName);
                if (File.Exists(currentSessionLogPath))
                    File.Copy(currentSessionLogPath, previousSessionPath, overwrite: true);
                string nativeDetectorLogPath = Path.Combine(diagnosticsDirectory, NativeDetectorLogFileName);
                string previousNativeDetectorLogPath = Path.Combine(diagnosticsDirectory, PreviousNativeDetectorLogFileName);
                if (File.Exists(nativeDetectorLogPath))
                    File.Copy(nativeDetectorLogPath, previousNativeDetectorLogPath, overwrite: true);
                File.WriteAllText(nativeDetectorLogPath, BuildNativeDetectorLogHeader(reason), Encoding.UTF8);

                writer = new StreamWriter(new FileStream(currentSessionLogPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
                {
                    AutoFlush = false
                };
                sessionLogBytesWritten = 0L;
                sessionLogTruncated = false;
                initialized = true;
                Application.logMessageReceivedThreaded += HandleUnityLogMessage;
                WriteRawLine("============================================================");
                WriteRawLine($"String Theory diagnostic session started | sessionId={sessionId} | {BuildVersionLogLabel()} | reason={reason} | utc={DateTime.UtcNow:O} | local={DateTime.Now:O}");
                WriteRawLine($"diagnosticsDirectory={RedactSensitiveText(diagnosticsDirectory)}");
                WriteRawLine($"currentSessionLog={RedactSensitiveText(currentSessionLogPath)}");
                WriteRawLine("============================================================");
                Flush();
                Debug.Log($"[Diagnostics] Session log started | {BuildVersionLogLabel()} | path={RedactSensitiveText(currentSessionLogPath)}");
            }
            catch (Exception ex)
            {
                initialized = true;
                writer = null;
                Debug.LogWarning($"[Diagnostics] Failed to start persistent diagnostics log: {ex.Message}");
            }
        }
    }

    public static void Shutdown(string reason)
    {
        lock (LockObject)
        {
            if (!initialized)
                return;

            WriteRawLine($"String Theory diagnostic session ending | reason={reason} | utc={DateTime.UtcNow:O}");
            Application.logMessageReceivedThreaded -= HandleUnityLogMessage;
            try
            {
                writer?.Flush();
                writer?.Dispose();
            }
            catch
            {
            }

            writer = null;
            initialized = false;
        }
    }

    public static void Flush()
    {
        lock (LockObject)
        {
            try
            {
                writer?.Flush();
            }
            catch
            {
            }
        }
    }

    public static void LogSnapshot(string label, string content)
    {
        EnsureInitialized($"snapshot-{label}");
        string safeLabel = string.IsNullOrWhiteSpace(label) ? "snapshot" : label.Trim();
        string snapshot = BuildSnapshotBlock(safeLabel, content);
        WriteRaw(snapshot);

        try
        {
            File.WriteAllText(latestSnapshotPath, snapshot, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Diagnostics] Failed to write latest diagnostics snapshot: {ex.Message}");
        }

        Flush();
        Debug.Log($"[Diagnostics] Snapshot '{safeLabel}' written | bytes={Encoding.UTF8.GetByteCount(snapshot)}");
    }

    public static void WriteLine(string category, string message)
    {
        EnsureInitialized($"write-{category}");
        WriteRawLine($"[{DateTime.Now:HH:mm:ss.fff}] [{NormalizeCategory(category)}] {RedactSensitiveText(message)}");
    }

    public static string BuildEnvironmentSnapshot()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Environment");
        builder.AppendLine($"  utc: {DateTime.UtcNow:O}");
        builder.AppendLine($"  local: {DateTime.Now:O}");
        builder.AppendLine($"  app: {Application.productName}");
        builder.AppendLine($"  stringTheoryVersion: {StringTheoryBuildInfo.Version}");
        builder.AppendLine($"  buildChannel: {StringTheoryBuildInfo.Channel}");
        builder.AppendLine($"  diagnosticsSchemaVersion: {StringTheoryBuildInfo.DiagnosticsSchemaVersion}");
        builder.AppendLine($"  unityApplicationVersion: {StringTheoryBuildInfo.UnityApplicationVersion}");
        builder.AppendLine($"  unityVersionMatchesBuildInfo: {StringTheoryBuildInfo.UnityVersionMatchesBuildInfo}");
        builder.AppendLine($"  unityVersion: {Application.unityVersion}");
        builder.AppendLine($"  company: {Application.companyName}");
        builder.AppendLine($"  platform: {Application.platform}");
        builder.AppendLine($"  installMode: {(Application.isEditor ? "Editor" : "Player")}");
        builder.AppendLine($"  processId: {GetProcessId()}");
        builder.AppendLine($"  targetFrameRate: {Application.targetFrameRate}");
        builder.AppendLine($"  runInBackground: {Application.runInBackground}");
        builder.AppendLine($"  persistentDataPath: {RedactSensitiveText(Application.persistentDataPath)}");
        builder.AppendLine($"  streamingAssetsPath: {RedactSensitiveText(Application.streamingAssetsPath)}");
        builder.AppendLine($"  dataPath: {RedactSensitiveText(Application.dataPath)}");
        builder.AppendLine($"  consoleLogPath: {RedactSensitiveText(GetConsoleLogPath())}");

        builder.AppendLine("System");
        builder.AppendLine($"  os: {SystemInfo.operatingSystem}");
        builder.AppendLine($"  osFamily: {SystemInfo.operatingSystemFamily}");
        builder.AppendLine($"  cpu: {SystemInfo.processorType}");
        builder.AppendLine($"  cpuCount: {SystemInfo.processorCount}");
        builder.AppendLine($"  cpuFrequencyMHz: {SystemInfo.processorFrequency}");
        builder.AppendLine($"  systemMemoryMB: {SystemInfo.systemMemorySize}");
        builder.AppendLine($"  deviceModel: {SystemInfo.deviceModel}");
        builder.AppendLine($"  deviceType: {SystemInfo.deviceType}");

        builder.AppendLine("Graphics");
        builder.AppendLine($"  gpuName: {SystemInfo.graphicsDeviceName}");
        builder.AppendLine($"  gpuVendor: {SystemInfo.graphicsDeviceVendor}");
        builder.AppendLine($"  gpuType: {SystemInfo.graphicsDeviceType}");
        builder.AppendLine($"  gpuVersion: {SystemInfo.graphicsDeviceVersion}");
        builder.AppendLine($"  gpuMemoryMB: {SystemInfo.graphicsMemorySize}");
        builder.AppendLine($"  graphicsMultiThreaded: {SystemInfo.graphicsMultiThreaded}");
        builder.AppendLine($"  shaderLevel: {SystemInfo.graphicsShaderLevel}");
        builder.AppendLine($"  maxTextureSize: {SystemInfo.maxTextureSize}");
        builder.AppendLine($"  screen: {Screen.width}x{Screen.height}@{Screen.currentResolution.refreshRateRatio.value:0.###}Hz");
        builder.AppendLine($"  fullscreen: {Screen.fullScreen}");
        builder.AppendLine($"  fullscreenMode: {Screen.fullScreenMode}");
        builder.AppendLine($"  dpi: {Screen.dpi:0.##}");
        builder.AppendLine($"  qualityLevel: {QualitySettings.GetQualityLevel()}");
        builder.AppendLine($"  vSyncCount: {QualitySettings.vSyncCount}");

        builder.AppendLine("UnityAudio");
        AppendUnityAudioSnapshot(builder);

        builder.AppendLine("Files");
        AppendFileSnapshot(builder, "playerLog", GetConsoleLogPath());
        AppendFileSnapshot(builder, "audioSettings", ExternalContentPaths.PersistentAudioSettingsPath);
        AppendFileSnapshot(builder, "toneLabSettings", ExternalContentPaths.PersistentToneLabConfigPath);
        AppendFileSnapshot(builder, "runtimeSettings", Path.Combine(ExternalContentPaths.PersistentRoot, "runtime_settings_metadata.json"));
        AppendFileSnapshot(builder, "contentSettings", ExternalContentPaths.PersistentExternalContentSettingsPath);
        AppendFileSnapshot(builder, "nativeDetectorLog", Path.Combine(DiagnosticsDirectory, NativeDetectorLogFileName));
        return builder.ToString().TrimEnd();
    }

    public static string RedactSensitiveText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        string redacted = value;
        redacted = ReplacePath(redacted, redactionPersistentDataPath, "<persistentDataPath>");
        redacted = ReplacePath(redacted, redactionStreamingAssetsPath, "<streamingAssetsPath>");
        redacted = ReplacePath(redacted, redactionDataPath, "<dataPath>");
        redacted = WindowsUserPathRegex.Replace(redacted, "$1<user>");
        redacted = MacUserPathRegex.Replace(redacted, "$1<user>");
        redacted = LinuxUserPathRegex.Replace(redacted, "$1<user>");
        return redacted;
    }

    private static string BuildSnapshotBlock(string label, string content)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("============================================================");
        builder.AppendLine($"Diagnostics snapshot: {label}");
        builder.AppendLine(BuildVersionLogLabel());
        builder.AppendLine($"utc: {DateTime.UtcNow:O}");
        builder.AppendLine("------------------------------------------------------------");
        if (!string.IsNullOrWhiteSpace(content))
            builder.AppendLine(RedactSensitiveText(content.Trim()));
        else
            builder.AppendLine("(empty)");
        builder.AppendLine("============================================================");
        return builder.ToString();
    }

    private static string BuildNativeDetectorLogHeader(string reason)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("============================================================");
        builder.AppendLine($"String Theory native detector log | {BuildVersionLogLabel()} | reason={reason} | utc={DateTime.UtcNow:O} | local={DateTime.Now:O}");
        builder.AppendLine("============================================================");
        return builder.ToString();
    }

    private static string BuildVersionLogLabel()
    {
        return $"version={StringTheoryBuildInfo.DiagnosticVersionLabel} | channel={StringTheoryBuildInfo.Channel} | diagnosticsSchemaVersion={StringTheoryBuildInfo.DiagnosticsSchemaVersion}";
    }

    private static void HandleUnityLogMessage(string condition, string stackTrace, LogType type)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        WriteRawLine($"[{timestamp}] [{type}] {RedactSensitiveText(condition)}");
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
        {
            string safeStack = RedactSensitiveText(stackTrace);
            if (!string.IsNullOrWhiteSpace(safeStack))
                WriteRawLine(safeStack.TrimEnd());
            Flush();
        }
    }

    private static void AppendUnityAudioSnapshot(StringBuilder builder)
    {
        try
        {
            AudioSettings.GetDSPBufferSize(out int dspBufferLength, out int dspBufferCount);
            AudioConfiguration configuration = AudioSettings.GetConfiguration();
            builder.AppendLine($"  outputSampleRate: {AudioSettings.outputSampleRate}");
            builder.AppendLine($"  speakerMode: {AudioSettings.speakerMode}");
            builder.AppendLine($"  driverCapabilities: {AudioSettings.driverCapabilities}");
            builder.AppendLine($"  dspBufferLength: {dspBufferLength}");
            builder.AppendLine($"  dspBufferCount: {dspBufferCount}");
            builder.AppendLine($"  configSampleRate: {configuration.sampleRate}");
            builder.AppendLine($"  configDspBufferSize: {configuration.dspBufferSize}");
            builder.AppendLine($"  configSpeakerMode: {configuration.speakerMode}");
            builder.AppendLine($"  configRealVoices: {configuration.numRealVoices}");
            builder.AppendLine($"  configVirtualVoices: {configuration.numVirtualVoices}");
            builder.AppendLine($"  unityMicrophones: {string.Join("; ", Microphone.devices ?? Array.Empty<string>())}");
        }
        catch (Exception ex)
        {
            builder.AppendLine($"  error: {ex.Message}");
        }
    }

    private static void AppendFileSnapshot(StringBuilder builder, string label, string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                builder.AppendLine($"  {label}: (none)");
                return;
            }

            FileInfo info = new FileInfo(path);
            if (!info.Exists)
            {
                builder.AppendLine($"  {label}: missing | path={RedactSensitiveText(path)}");
                return;
            }

            builder.AppendLine($"  {label}: exists | sizeBytes={info.Length} | lastWriteUtc={info.LastWriteTimeUtc:O} | path={RedactSensitiveText(path)}");
        }
        catch (Exception ex)
        {
            builder.AppendLine($"  {label}: error={ex.Message} | path={RedactSensitiveText(path)}");
        }
    }

    private static string ReplacePath(string text, string path, string token)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(path))
            return text;

        return text.Replace(path, token).Replace(path.Replace('\\', '/'), token);
    }

    private static void WriteRawLine(string line)
    {
        WriteRaw((line ?? string.Empty) + Environment.NewLine);
    }

    private static void WriteRaw(string text)
    {
        lock (LockObject)
        {
            try
            {
                if (writer == null)
                    return;

                text ??= string.Empty;
                long byteCount = Encoding.UTF8.GetByteCount(text);
                if (sessionLogBytesWritten + byteCount > MaxSessionLogBytes)
                {
                    if (!sessionLogTruncated)
                    {
                        string marker = $"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] Session diagnostics log reached {MaxSessionLogBytes} bytes and will stop recording extra lines for this session.{Environment.NewLine}";
                        writer.Write(marker);
                        sessionLogBytesWritten += Encoding.UTF8.GetByteCount(marker);
                        sessionLogTruncated = true;
                        writer.Flush();
                    }

                    return;
                }

                writer.Write(text);
                sessionLogBytesWritten += byteCount;
            }
            catch
            {
            }
        }
    }

    private static string NormalizeCategory(string category)
    {
        return string.IsNullOrWhiteSpace(category) ? "diagnostics" : category.Trim();
    }

    private static int GetProcessId()
    {
        try
        {
            return System.Diagnostics.Process.GetCurrentProcess().Id;
        }
        catch
        {
            return -1;
        }
    }

    private static string GetConsoleLogPath()
    {
        try
        {
            return Application.consoleLogPath;
        }
        catch
        {
            return string.Empty;
        }
    }
}
