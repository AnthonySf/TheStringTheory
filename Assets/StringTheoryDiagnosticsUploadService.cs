using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public static class StringTheoryDiagnosticsUploadService
{
    private const int StateVersion = 1;
    private const string StateFileName = "diagnostics_upload_state.json";
    private const string ReportsFolderName = "Reports";
    private const long MaxTextFileBytes = 8L * 1024L * 1024L;
    private const long MaxDiscordWebhookPackageBytes = 9L * 1024L * 1024L;
    private static readonly Regex WindowsUserPathRegex = new Regex(@"([A-Za-z]:\\Users\\)[^\\/\r\n]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MacUserPathRegex = new Regex(@"(/Users/)[^/\r\n]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LinuxUserPathRegex = new Regex(@"(/home/)[^/\r\n]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static DiagnosticsUploadState state;
    private static string lastStatus = string.Empty;
    private static bool initialized;
    private static bool automaticUploadInProgress;
    private static bool userUploadInProgress;

    public static bool AutomaticUploadsEnabled
    {
        get
        {
            EnsureInitialized();
            return state != null && state.automaticUploadsEnabled;
        }
    }

    public static bool ShouldShowStartupConsentPrompt
    {
        get
        {
            EnsureInitialized();
            return state != null && !state.automaticUploadsEnabled && !state.startupPromptSeen;
        }
    }

    public static bool HasUploadEndpoint => !string.IsNullOrWhiteSpace(StringTheoryBuildInfo.DiagnosticsUploadEndpoint);

    public static bool IsUserUploadInProgress => userUploadInProgress;

    public static string LastStatus
    {
        get
        {
            EnsureInitialized();
            return string.IsNullOrWhiteSpace(lastStatus) ? state?.lastUploadStatus ?? string.Empty : lastStatus;
        }
    }

    public static void Initialize()
    {
        EnsureInitialized();
    }

    public static void SetAutomaticUploadsConsent(bool enabled)
    {
        EnsureInitialized();
        state.startupPromptSeen = true;
        state.automaticUploadsEnabled = enabled;
        state.updatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        SaveState();
        lastStatus = enabled
            ? "Automatic diagnostic uploads are enabled."
            : "Automatic diagnostic uploads are disabled.";
        StringTheoryDiagnostics.WriteLine("DiagnosticsUpload", lastStatus);
    }

    public static void MarkStartupPromptSeen()
    {
        EnsureInitialized();
        state.startupPromptSeen = true;
        state.updatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        SaveState();
    }

    public static IEnumerator UploadPreviousSessionIfNeeded()
    {
        EnsureInitialized();
        if (!AutomaticUploadsEnabled || automaticUploadInProgress)
            yield break;

        string previousSessionPath = StringTheoryDiagnostics.PreviousSessionLogPath;
        if (!File.Exists(previousSessionPath))
            yield break;

        string sessionKey = BuildFileFingerprint(previousSessionPath);
        if (string.Equals(state.lastUploadedPreviousSessionKey, sessionKey, StringComparison.Ordinal))
            yield break;

        automaticUploadInProgress = true;
        yield return UploadPackage(
            new DiagnosticsUploadRequest
            {
                uploadKind = "automatic-previous-session",
                reason = "startup-previous-session",
                sessionKey = sessionKey,
                includeCurrentSession = false,
                includePreviousSession = true,
                userInitiated = false
            },
            result =>
            {
                if (result.success || result.savedLocallyOnly)
                    state.lastUploadedPreviousSessionKey = sessionKey;
            });
        automaticUploadInProgress = false;
    }

    public static IEnumerator UploadCurrentSessionIfNeeded(string reason, float delaySeconds)
    {
        EnsureInitialized();
        if (!AutomaticUploadsEnabled || automaticUploadInProgress)
            yield break;

        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);

        string sessionKey = StringTheoryDiagnostics.SessionId;
        if (string.Equals(state.lastUploadedCurrentSessionKey, sessionKey, StringComparison.Ordinal))
            yield break;

        automaticUploadInProgress = true;
        yield return UploadPackage(
            new DiagnosticsUploadRequest
            {
                uploadKind = "automatic-current-session",
                reason = string.IsNullOrWhiteSpace(reason) ? "current-session" : reason,
                sessionKey = sessionKey,
                includeCurrentSession = true,
                includePreviousSession = false,
                userInitiated = false
            },
            result =>
            {
                if (result.success || result.savedLocallyOnly)
                    state.lastUploadedCurrentSessionKey = sessionKey;
            });
        automaticUploadInProgress = false;
    }

    public static IEnumerator SendUserBugReport(string description, Action<DiagnosticsUploadResult> onComplete)
    {
        EnsureInitialized();
        if (userUploadInProgress)
        {
            onComplete?.Invoke(new DiagnosticsUploadResult
            {
                success = false,
                message = "A bug report upload is already running."
            });
            yield break;
        }

        userUploadInProgress = true;
        DiagnosticsUploadResult uploadResult = null;
        DiagnosticsUploadRequest request = new DiagnosticsUploadRequest
        {
            uploadKind = "user-bug-report",
            reason = "user-bug-report",
            sessionKey = StringTheoryDiagnostics.SessionId,
            includeCurrentSession = true,
            includePreviousSession = true,
            userInitiated = true,
            description = description ?? string.Empty
        };

        yield return UploadPackage(
            request,
            result => uploadResult = result);
        if (uploadResult != null && uploadResult.success)
            MarkIncludedSessionsUploaded(request);
        userUploadInProgress = false;
        onComplete?.Invoke(uploadResult);
    }

    private static IEnumerator UploadPackage(DiagnosticsUploadRequest request, Action<DiagnosticsUploadResult> onComplete)
    {
        DiagnosticsUploadResult result = null;
        yield return CreatePackageAsync(request, value => result = value);
        if (result == null)
        {
            result = new DiagnosticsUploadResult
            {
                packageCreated = false,
                success = false,
                message = "Diagnostics package creation did not complete."
            };
        }

        if (!result.packageCreated)
        {
            SetLastStatus(result.message);
            onComplete?.Invoke(result);
            yield break;
        }

        if (!HasUploadEndpoint)
        {
            result.savedLocallyOnly = true;
            result.success = false;
            result.message = $"Diagnostics package saved locally. Upload endpoint is not configured yet: {result.packagePath}";
            SetLastStatus(result.message);
            onComplete?.Invoke(result);
            yield break;
        }

        string uploadEndpoint = StringTheoryBuildInfo.DiagnosticsUploadEndpoint.Trim();
        if (ShouldUseDiscordWebhook(uploadEndpoint))
        {
            byte[] packageBytes = null;
            string readError = null;
            yield return ReadPackageBytesAsync(result.packagePath, (bytes, error) =>
            {
                packageBytes = bytes;
                readError = error;
            });

            if (!string.IsNullOrEmpty(readError))
            {
                result.success = false;
                result.message = $"Diagnostics package was created but could not be read for upload: {readError}";
                SetLastStatus(result.message);
                onComplete?.Invoke(result);
                yield break;
            }

            if (packageBytes.LongLength > MaxDiscordWebhookPackageBytes)
            {
                result.success = false;
                result.message = $"Diagnostics package is too large for the Discord upload endpoint ({packageBytes.LongLength / 1024f / 1024f:F1} MB).";
                SetLastStatus(result.message);
                onComplete?.Invoke(result);
                yield break;
            }

            WWWForm form = new WWWForm();
            string packageFileName = Path.GetFileName(result.packagePath);
            form.AddField("payload_json", BuildDiscordWebhookPayloadJson(request, packageFileName));
            form.AddBinaryData("files[0]", packageBytes, packageFileName, "application/zip");

            using (UnityWebRequest webRequest = UnityWebRequest.Post(AppendDiscordWaitQuery(uploadEndpoint), form))
            {
                webRequest.timeout = 25;
                AddCommonHeaders(webRequest, request);

                yield return webRequest.SendWebRequest();

                bool ok = webRequest.result == UnityWebRequest.Result.Success &&
                          webRequest.responseCode >= 200 &&
                          webRequest.responseCode < 300;
                result.success = ok;
                result.httpStatusCode = webRequest.responseCode;
                result.message = ok
                    ? $"Diagnostics uploaded ({request.uploadKind})."
                    : $"Diagnostics upload failed ({webRequest.responseCode}): {webRequest.error ?? webRequest.downloadHandler?.text ?? "unknown error"}";
            }

            SetLastStatus(result.message);
            onComplete?.Invoke(result);
            yield break;
        }

        using (UnityWebRequest webRequest = new UnityWebRequest(uploadEndpoint, UnityWebRequest.kHttpVerbPOST))
        {
            webRequest.uploadHandler = new UploadHandlerFile(result.packagePath);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.timeout = 25;
            webRequest.SetRequestHeader("Content-Type", "application/zip");
            AddCommonHeaders(webRequest, request);

            yield return webRequest.SendWebRequest();

            bool ok = webRequest.result == UnityWebRequest.Result.Success &&
                      webRequest.responseCode >= 200 &&
                      webRequest.responseCode < 300;
            result.success = ok;
            result.httpStatusCode = webRequest.responseCode;
            result.message = ok
                ? $"Diagnostics uploaded ({request.uploadKind})."
                : $"Diagnostics upload failed ({webRequest.responseCode}): {webRequest.error ?? webRequest.downloadHandler?.text ?? "unknown error"}";
        }

        SetLastStatus(result.message);
        onComplete?.Invoke(result);
    }

    private static IEnumerator ReadPackageBytesAsync(string packagePath, Action<byte[], string> onComplete)
    {
        Task<byte[]> task = Task.Run(() => File.ReadAllBytes(packagePath));
        while (!task.IsCompleted)
            yield return null;

        if (task.IsFaulted)
        {
            string message = task.Exception?.GetBaseException()?.Message ?? "unknown error";
            onComplete?.Invoke(null, message);
            yield break;
        }

        onComplete?.Invoke(task.Result, null);
    }

    private static void AddCommonHeaders(UnityWebRequest webRequest, DiagnosticsUploadRequest request)
    {
        webRequest.SetRequestHeader("X-StringTheory-Version", StringTheoryBuildInfo.Version);
        webRequest.SetRequestHeader("X-StringTheory-Channel", StringTheoryBuildInfo.Channel);
        webRequest.SetRequestHeader("X-StringTheory-Install-Id", state.installId);
        webRequest.SetRequestHeader("X-StringTheory-Session-Id", StringTheoryDiagnostics.SessionId);
        webRequest.SetRequestHeader("X-StringTheory-Upload-Kind", request.uploadKind ?? string.Empty);
        webRequest.SetRequestHeader("X-StringTheory-User-Initiated", request.userInitiated ? "true" : "false");
    }

    private static bool ShouldUseDiscordWebhook(string endpoint)
    {
        string kind = StringTheoryBuildInfo.DiagnosticsUploadEndpointKind ?? string.Empty;
        if (string.Equals(kind, "discord-webhook", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(kind, "raw-zip", StringComparison.OrdinalIgnoreCase))
            return false;

        return endpoint.IndexOf("discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase) >= 0 ||
               endpoint.IndexOf("discordapp.com/api/webhooks/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string AppendDiscordWaitQuery(string endpoint)
    {
        if (endpoint.IndexOf("wait=", StringComparison.OrdinalIgnoreCase) >= 0)
            return endpoint;

        return endpoint.Contains("?") ? endpoint + "&wait=true" : endpoint + "?wait=true";
    }

    private static string BuildDiscordWebhookPayloadJson(DiagnosticsUploadRequest request, string packageFileName)
    {
        string title = request.userInitiated ? "User bug report" : "Automatic diagnostics";
        string description = request.userInitiated && !string.IsNullOrWhiteSpace(request.description)
            ? request.description.Trim()
            : request.reason ?? string.Empty;
        description = TruncateForDiscord(description, 1500);

        StringBuilder builder = new StringBuilder();
        builder.Append('{');
        AppendJsonProperty(builder, "username", "String Theory Diagnostics");
        builder.Append(',');
        AppendJsonProperty(builder, "content", request.userInitiated ? "New user bug report" : "New automatic diagnostics package");
        builder.Append(",\"allowed_mentions\":{\"parse\":[]}");
        builder.Append(",\"embeds\":[{");
        AppendJsonProperty(builder, "title", title);
        builder.Append(',');
        AppendJsonProperty(builder, "description", string.IsNullOrWhiteSpace(description) ? "(no description)" : description);
        builder.Append(",\"color\":3447003");
        builder.Append(",\"fields\":[");
        AppendDiscordField(builder, "Version", StringTheoryBuildInfo.Version, true);
        builder.Append(',');
        AppendDiscordField(builder, "Kind", request.uploadKind ?? string.Empty, true);
        builder.Append(',');
        AppendDiscordField(builder, "Platform", Application.platform.ToString(), true);
        builder.Append(',');
        AppendDiscordField(builder, "Install ID", GetShortInstallId(), true);
        builder.Append(',');
        AppendDiscordField(builder, "Session", StringTheoryDiagnostics.SessionId, false);
        builder.Append("]}]");
        builder.Append(",\"attachments\":[{\"id\":0,");
        AppendJsonProperty(builder, "filename", packageFileName);
        builder.Append(',');
        AppendJsonProperty(builder, "description", "String Theory diagnostics package");
        builder.Append("}]}");
        return builder.ToString();
    }

    private static void AppendDiscordField(StringBuilder builder, string name, string value, bool inline)
    {
        builder.Append('{');
        AppendJsonProperty(builder, "name", name);
        builder.Append(',');
        AppendJsonProperty(builder, "value", string.IsNullOrWhiteSpace(value) ? "--" : value);
        builder.Append(",\"inline\":");
        builder.Append(inline ? "true" : "false");
        builder.Append('}');
    }

    private static void AppendJsonProperty(StringBuilder builder, string name, string value)
    {
        builder.Append('"');
        builder.Append(EscapeJson(name));
        builder.Append("\":\"");
        builder.Append(EscapeJson(value ?? string.Empty));
        builder.Append('"');
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        StringBuilder builder = new StringBuilder(value.Length + 8);
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < ' ')
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }

    private static string TruncateForDiscord(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value ?? string.Empty;

        return value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
    }

    private static IEnumerator CreatePackageAsync(DiagnosticsUploadRequest request, Action<DiagnosticsUploadResult> onComplete)
    {
        DiagnosticsPackageBuildSpec spec;
        try
        {
            spec = BuildPackageBuildSpec(request);
        }
        catch (Exception ex)
        {
            onComplete?.Invoke(new DiagnosticsUploadResult
            {
                packageCreated = false,
                success = false,
                message = $"Failed to prepare diagnostics package: {ex.Message}"
            });
            yield break;
        }

        Task<DiagnosticsUploadResult> task = Task.Run(() => CreatePackage(spec));
        while (!task.IsCompleted)
            yield return null;

        if (task.IsFaulted)
        {
            string message = task.Exception?.GetBaseException()?.Message ?? "unknown error";
            onComplete?.Invoke(new DiagnosticsUploadResult
            {
                packageCreated = false,
                success = false,
                message = $"Failed to create diagnostics package: {message}"
            });
            yield break;
        }

        onComplete?.Invoke(task.Result);
    }

    private static DiagnosticsPackageBuildSpec BuildPackageBuildSpec(DiagnosticsUploadRequest request)
    {
        StringTheoryDiagnostics.Flush();
        EnsureInitialized();

        string reportsDirectory = Path.Combine(StringTheoryDiagnostics.DiagnosticsDirectory, ReportsFolderName);
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string safeKind = SanitizeFileName(request.uploadKind);
        string shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
        string fileName = $"StringTheory-{StringTheoryBuildInfo.Version}-{GetShortInstallId()}-{safeKind}-{timestamp}-{shortId}.zip";

        return new DiagnosticsPackageBuildSpec
        {
            reportsDirectory = reportsDirectory,
            packagePath = Path.Combine(reportsDirectory, fileName),
            manifestJson = JsonUtility.ToJson(BuildManifest(request), true),
            readmeText = BuildReadme(request),
            redaction = new DiagnosticsRedactionContext
            {
                persistentDataPath = Application.persistentDataPath ?? string.Empty,
                streamingAssetsPath = Application.streamingAssetsPath ?? string.Empty,
                dataPath = Application.dataPath ?? string.Empty
            },
            textFiles = new List<DiagnosticsPackageTextFile>
            {
                new DiagnosticsPackageTextFile("logs/player.log", StringTheoryDiagnostics.ConsoleLogPath, true),
                new DiagnosticsPackageTextFile("logs/current_session.log", StringTheoryDiagnostics.CurrentSessionLogPath, request.includeCurrentSession),
                new DiagnosticsPackageTextFile("logs/previous_session.log", StringTheoryDiagnostics.PreviousSessionLogPath, request.includePreviousSession),
                new DiagnosticsPackageTextFile("logs/native_detector.log", StringTheoryDiagnostics.NativeDetectorLogPath, request.includeCurrentSession),
                new DiagnosticsPackageTextFile("logs/previous_native_detector.log", StringTheoryDiagnostics.PreviousNativeDetectorLogPath, request.includePreviousSession),
                new DiagnosticsPackageTextFile("logs/latest_diagnostics_snapshot.txt", StringTheoryDiagnostics.LatestSnapshotPath, true),
                new DiagnosticsPackageTextFile("settings/audio_settings.json", ExternalContentPaths.PersistentAudioSettingsPath, true),
                new DiagnosticsPackageTextFile("settings/tone_lab_settings.json", ExternalContentPaths.PersistentToneLabConfigPath, true),
                new DiagnosticsPackageTextFile("settings/runtime_settings_metadata.json", Path.Combine(ExternalContentPaths.PersistentRoot, "runtime_settings_metadata.json"), true),
                new DiagnosticsPackageTextFile("settings/content_settings.json", ExternalContentPaths.PersistentExternalContentSettingsPath, true)
            }
        };
    }

    private static DiagnosticsUploadResult CreatePackage(DiagnosticsPackageBuildSpec spec)
    {
        try
        {
            Directory.CreateDirectory(spec.reportsDirectory);

            using (FileStream zipStream = new FileStream(spec.packagePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                AddTextEntry(archive, "manifest.json", spec.manifestJson);
                AddTextEntry(archive, "README.txt", spec.readmeText);
                foreach (DiagnosticsPackageTextFile file in spec.textFiles)
                    AddOptionalTextFile(archive, file.entryName, file.path, file.include, spec.redaction);
            }

            return new DiagnosticsUploadResult
            {
                packageCreated = true,
                packagePath = spec.packagePath,
                message = $"Diagnostics package created: {spec.packagePath}"
            };
        }
        catch (Exception ex)
        {
            return new DiagnosticsUploadResult
            {
                packageCreated = false,
                success = false,
                message = $"Failed to create diagnostics package: {ex.Message}"
            };
        }
    }

    private static DiagnosticsUploadManifest BuildManifest(DiagnosticsUploadRequest request)
    {
        return new DiagnosticsUploadManifest
        {
            schemaVersion = StringTheoryBuildInfo.DiagnosticsSchemaVersion,
            uploadKind = request.uploadKind ?? string.Empty,
            reason = request.reason ?? string.Empty,
            userInitiated = request.userInitiated,
            description = request.description ?? string.Empty,
            createdUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            installId = state.installId,
            anonymousUserId = state.installId,
            shortAnonymousUserId = GetShortInstallId(),
            sessionId = StringTheoryDiagnostics.SessionId,
            sessionKey = request.sessionKey ?? string.Empty,
            gameVersion = StringTheoryBuildInfo.Version,
            buildChannel = StringTheoryBuildInfo.Channel,
            unityApplicationVersion = StringTheoryBuildInfo.UnityApplicationVersion,
            unityVersion = Application.unityVersion,
            productName = Application.productName,
            platform = Application.platform.ToString(),
            operatingSystem = SystemInfo.operatingSystem,
            processorType = SystemInfo.processorType,
            processorCount = SystemInfo.processorCount,
            graphicsDeviceName = SystemInfo.graphicsDeviceName,
            systemMemoryMB = SystemInfo.systemMemorySize,
            uploadEndpointConfigured = HasUploadEndpoint
        };
    }

    private static string BuildReadme(DiagnosticsUploadRequest request)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("String Theory diagnostics package");
        builder.AppendLine($"Version: {StringTheoryBuildInfo.Version}");
        builder.AppendLine($"Kind: {request.uploadKind}");
        builder.AppendLine($"Anonymous user ID: {GetShortInstallId()}");
        builder.AppendLine($"Session ID: {StringTheoryDiagnostics.SessionId}");
        builder.AppendLine($"User initiated: {request.userInitiated}");
        builder.AppendLine("No audio recordings are included.");
        builder.AppendLine("Text files are redacted for common local user path prefixes.");
        if (!string.IsNullOrWhiteSpace(request.description))
        {
            builder.AppendLine();
            builder.AppendLine("User description:");
            builder.AppendLine(request.description.Trim());
        }

        return builder.ToString();
    }

    private static void AddOptionalTextFile(ZipArchive archive, string entryName, string path, bool include, DiagnosticsRedactionContext redaction)
    {
        if (!include || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            FileInfo info = new FileInfo(path);
            if (info.Length > MaxTextFileBytes)
            {
                AddTextEntry(archive, entryName + ".skipped.txt", $"Skipped because the file is larger than {MaxTextFileBytes} bytes. Path: {RedactSensitiveText(path, redaction)}");
                return;
            }

            string text;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                text = reader.ReadToEnd();
            }

            AddTextEntry(archive, entryName, RedactSensitiveText(text, redaction));
        }
        catch (Exception ex)
        {
            AddTextEntry(archive, entryName + ".error.txt", $"Failed to include file: {ex.Message}");
        }
    }

    private static string RedactSensitiveText(string value, DiagnosticsRedactionContext redaction)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        string redacted = value;
        redacted = ReplacePath(redacted, redaction?.persistentDataPath, "<persistentDataPath>");
        redacted = ReplacePath(redacted, redaction?.streamingAssetsPath, "<streamingAssetsPath>");
        redacted = ReplacePath(redacted, redaction?.dataPath, "<dataPath>");
        redacted = WindowsUserPathRegex.Replace(redacted, "$1<user>");
        redacted = MacUserPathRegex.Replace(redacted, "$1<user>");
        redacted = LinuxUserPathRegex.Replace(redacted, "$1<user>");
        return redacted;
    }

    private static string ReplacePath(string text, string path, string token)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(path))
            return text;

        return text.Replace(path, token).Replace(path.Replace('\\', '/'), token);
    }

    private static void AddTextEntry(ZipArchive archive, string entryName, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Fastest);
        using (Stream stream = entry.Open())
        using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
            writer.Write(content ?? string.Empty);
    }

    private static string BuildFileFingerprint(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return string.Empty;

        FileInfo info = new FileInfo(path);
        return $"{Path.GetFileName(path)}:{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }

    private static void SetLastStatus(string status)
    {
        EnsureInitialized();
        lastStatus = status ?? string.Empty;
        state.lastUploadStatus = lastStatus;
        state.lastUploadUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        state.updatedUtc = state.lastUploadUtc;
        SaveState();
        StringTheoryDiagnostics.WriteLine("DiagnosticsUpload", lastStatus);
        if (!string.IsNullOrWhiteSpace(lastStatus))
            Debug.Log($"[DiagnosticsUpload] {lastStatus}");
    }

    private static void MarkIncludedSessionsUploaded(DiagnosticsUploadRequest request)
    {
        EnsureInitialized();
        bool changed = false;
        if (request.includeCurrentSession)
        {
            string currentSessionKey = StringTheoryDiagnostics.SessionId;
            if (!string.IsNullOrWhiteSpace(currentSessionKey) &&
                !string.Equals(state.lastUploadedCurrentSessionKey, currentSessionKey, StringComparison.Ordinal))
            {
                state.lastUploadedCurrentSessionKey = currentSessionKey;
                changed = true;
            }
        }

        if (request.includePreviousSession)
        {
            string previousSessionPath = StringTheoryDiagnostics.PreviousSessionLogPath;
            if (File.Exists(previousSessionPath))
            {
                string previousSessionKey = BuildFileFingerprint(previousSessionPath);
                if (!string.IsNullOrWhiteSpace(previousSessionKey) &&
                    !string.Equals(state.lastUploadedPreviousSessionKey, previousSessionKey, StringComparison.Ordinal))
                {
                    state.lastUploadedPreviousSessionKey = previousSessionKey;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            state.updatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            SaveState();
        }
    }

    private static string GetShortInstallId()
    {
        EnsureInitialized();
        string installId = state?.installId ?? string.Empty;
        if (installId.Length <= 12)
            return string.IsNullOrWhiteSpace(installId) ? "unknown" : installId;

        return installId.Substring(0, 12);
    }

    private static void EnsureInitialized()
    {
        if (initialized)
            return;

        StringTheoryDiagnostics.EnsureInitialized("diagnostics-upload");
        state = LoadState();
        if (string.IsNullOrWhiteSpace(state.installId))
            state.installId = Guid.NewGuid().ToString("N");
        state.version = StateVersion;
        state.updatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        initialized = true;
        SaveState();
        lastStatus = state.lastUploadStatus ?? string.Empty;
    }

    private static DiagnosticsUploadState LoadState()
    {
        string path = GetStatePath();
        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                DiagnosticsUploadState loaded = JsonUtility.FromJson<DiagnosticsUploadState>(json);
                if (loaded != null)
                    return loaded;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DiagnosticsUpload] Failed to load upload state: {ex.Message}");
        }

        return new DiagnosticsUploadState
        {
            version = StateVersion,
            installId = Guid.NewGuid().ToString("N")
        };
    }

    private static void SaveState()
    {
        try
        {
            string path = GetStatePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(state, true), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DiagnosticsUpload] Failed to save upload state: {ex.Message}");
        }
    }

    private static string GetStatePath()
    {
        return Path.Combine(StringTheoryDiagnostics.DiagnosticsDirectory, StateFileName);
    }

    private static string SanitizeFileName(string value)
    {
        string text = string.IsNullOrWhiteSpace(value) ? "diagnostics" : value.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
            text = text.Replace(invalid, '-');
        return text.Replace(' ', '-');
    }

    private sealed class DiagnosticsPackageBuildSpec
    {
        public string reportsDirectory = string.Empty;
        public string packagePath = string.Empty;
        public string manifestJson = string.Empty;
        public string readmeText = string.Empty;
        public DiagnosticsRedactionContext redaction;
        public List<DiagnosticsPackageTextFile> textFiles = new List<DiagnosticsPackageTextFile>();
    }

    private sealed class DiagnosticsPackageTextFile
    {
        public readonly string entryName;
        public readonly string path;
        public readonly bool include;

        public DiagnosticsPackageTextFile(string entryName, string path, bool include)
        {
            this.entryName = entryName ?? string.Empty;
            this.path = path ?? string.Empty;
            this.include = include;
        }
    }

    private sealed class DiagnosticsRedactionContext
    {
        public string persistentDataPath = string.Empty;
        public string streamingAssetsPath = string.Empty;
        public string dataPath = string.Empty;
    }

    [Serializable]
    private sealed class DiagnosticsUploadState
    {
        public int version = StateVersion;
        public string installId = string.Empty;
        public bool startupPromptSeen;
        public bool automaticUploadsEnabled;
        public string lastUploadedCurrentSessionKey = string.Empty;
        public string lastUploadedPreviousSessionKey = string.Empty;
        public string lastUploadUtc = string.Empty;
        public string lastUploadStatus = string.Empty;
        public string updatedUtc = string.Empty;
    }

    private sealed class DiagnosticsUploadRequest
    {
        public string uploadKind = string.Empty;
        public string reason = string.Empty;
        public string sessionKey = string.Empty;
        public bool includeCurrentSession;
        public bool includePreviousSession;
        public bool userInitiated;
        public string description = string.Empty;
    }

    [Serializable]
    private sealed class DiagnosticsUploadManifest
    {
        public int schemaVersion;
        public string uploadKind;
        public string reason;
        public bool userInitiated;
        public string description;
        public string createdUtc;
        public string installId;
        public string anonymousUserId;
        public string shortAnonymousUserId;
        public string sessionId;
        public string sessionKey;
        public string gameVersion;
        public string buildChannel;
        public string unityApplicationVersion;
        public string unityVersion;
        public string productName;
        public string platform;
        public string operatingSystem;
        public string processorType;
        public int processorCount;
        public string graphicsDeviceName;
        public int systemMemoryMB;
        public bool uploadEndpointConfigured;
    }
}

public sealed class DiagnosticsUploadResult
{
    public bool packageCreated;
    public bool success;
    public bool savedLocallyOnly;
    public long httpStatusCode;
    public string packagePath = string.Empty;
    public string message = string.Empty;
}
