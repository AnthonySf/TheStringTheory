using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

[Serializable]
public sealed class StemCacheManifest
{
    public int schemaVersion = 1;
    public string sourceAudioPath;
    public long sourceAudioLastWriteUtcTicks;
    public long sourceAudioSizeBytes;
    public string provider = "demucs";
    public string model = StemSeparationService.DefaultDemucsModel;
    public long generatedAtUtcTicks;
    public string status = "ready";
    public string error = string.Empty;
    public List<StemCacheEntry> stems = new List<StemCacheEntry>();
}

[Serializable]
public sealed class StemCacheEntry
{
    public string id;
    public string displayName;
    public string relativePath;
}

public enum StemGenerationState
{
    Idle = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3
}

public sealed class StemGenerationStatus
{
    public StemGenerationState state = StemGenerationState.Idle;
    public string message = string.Empty;
    public string error = string.Empty;
    public bool consumed;
}

[Serializable]
public sealed class StemRuntimeInstallManifest
{
    public int schemaVersion = 1;
    public string sourcePath = string.Empty;
    public long sourceLastWriteUtcTicks;
    public long sourceSizeBytes;
    public long generatedAtUtcTicks;
}

public enum StemRuntimeInstallState
{
    Idle = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3
}

public sealed class StemRuntimeInstallStatus
{
    public StemRuntimeInstallState state = StemRuntimeInstallState.Idle;
    public string message = string.Empty;
    public string error = string.Empty;
    public float progressPercent;
    public bool consumed;
}

public static class StemSeparationService
{
    public const string DefaultDemucsModel = "htdemucs_6s";
    public const string ManifestFileName = "stems.manifest.json";

    private static readonly object Gate = new object();
    private static readonly object RuntimeInstallLogGate = new object();
    private static readonly Dictionary<string, StemGenerationStatus> StatusByKey = new Dictionary<string, StemGenerationStatus>(StringComparer.OrdinalIgnoreCase);
    private static StemRuntimeInstallStatus RuntimeInstallStatus = new StemRuntimeInstallStatus();
    private static readonly string[] PreferredStemOrder = { "guitar", "bass", "drums", "vocals", "piano", "other" };
    private const int CopyBufferSize = 1024 * 1024;
    private const string RuntimeInstallLogFileName = "stem-generator-install.log";
    private const string WindowsPythonEmbedUrl = "https://www.python.org/ftp/python/3.11.9/python-3.11.9-embed-amd64.zip";
    private const string PipZipAppUrl = "https://bootstrap.pypa.io/pip/pip.pyz";
    private const string AntlrRuntimeSourceUrl = "https://files.pythonhosted.org/packages/3e/38/7859ff46355f76f8d19459005ca000b6e7012f2f1ca597746cbcd1fbfe5e/antlr4-python3-runtime-4.9.3.tar.gz";
    private const string JuliusSourceUrl = "https://files.pythonhosted.org/packages/a1/19/c9e1596b5572c786b93428d0904280e964c930fae7e6c9368ed9e1b63922/julius-0.2.7.tar.gz";
    private const string DoraSearchSourceUrl = "https://files.pythonhosted.org/packages/source/d/dora-search/dora_search-0.1.12.tar.gz";
    private const string DemucsSourceUrl = "https://files.pythonhosted.org/packages/source/d/demucs/demucs-4.0.1.tar.gz";
    private const string PytorchCpuIndexUrl = "https://download.pytorch.org/whl/cpu";
    private const string StemRuntimePackageTools = "setuptools==75.8.0 wheel==0.45.1 packaging==24.2";
    private const string StemRuntimeDependencyPackages = "torch==2.5.1+cpu torchaudio==2.5.1+cpu soundfile==0.13.1 einops==0.8.2 lameenc==1.8.2 openunmix==1.3.0 pyyaml==6.0.3 tqdm==4.67.3 omegaconf==2.3.0 retrying==1.4.2 submitit==1.5.4 treetable==0.2.6";
    private const string StemSeparatorWorkFolderName = "work";
    private const string StemSeparatorTempFolderName = "temp";
    private const string OggEncoderScriptFileName = "encode_stem_to_ogg.py";
    private const string OggEncoderPythonScript =
        "import sys\n" +
        "import soundfile as sf\n" +
        "src, dst = sys.argv[1], sys.argv[2]\n" +
        "with sf.SoundFile(src, 'r') as infile:\n" +
        "    with sf.SoundFile(dst, 'w', samplerate=infile.samplerate, channels=infile.channels, format='OGG', subtype='VORBIS') as outfile:\n" +
        "        for block in infile.blocks(blocksize=262144, dtype='float32', always_2d=True):\n" +
        "            outfile.write(block)\n";
    private const int InstallerProcessTimeoutMilliseconds = 45 * 60 * 1000;

    public static string RuntimeInstallLogPath => Path.Combine(ExternalContentPaths.PersistentStemSeparatorDirectory, RuntimeInstallLogFileName);

    public static string GetCacheDirectory(string sourceAudioPath)
    {
        string normalized = NormalizePath(sourceAudioPath);
        string hash = ComputeStableHash(normalized);
        return Path.Combine(ExternalContentPaths.PersistentStemCacheDirectory, hash);
    }

    public static string GetManifestPath(string sourceAudioPath)
    {
        return Path.Combine(GetCacheDirectory(sourceAudioPath), ManifestFileName);
    }

    public static bool TryLoadValidManifest(string sourceAudioPath, out StemCacheManifest manifest)
    {
        manifest = null;
        if (string.IsNullOrWhiteSpace(sourceAudioPath) || !File.Exists(sourceAudioPath))
            return false;

        string manifestPath = GetManifestPath(sourceAudioPath);
        if (!File.Exists(manifestPath))
            return false;

        try
        {
            StemCacheManifest loaded = JsonUtility.FromJson<StemCacheManifest>(File.ReadAllText(manifestPath));
            if (!IsManifestValidForSource(sourceAudioPath, loaded))
                return false;

            string cacheDirectory = GetCacheDirectory(sourceAudioPath);
            loaded.stems = (loaded.stems ?? new List<StemCacheEntry>())
                .Where(stem => stem != null &&
                               !string.IsNullOrWhiteSpace(stem.id) &&
                               !string.IsNullOrWhiteSpace(stem.relativePath) &&
                               File.Exists(Path.Combine(cacheDirectory, stem.relativePath)))
                .OrderBy(stem => GetStemOrder(stem.id))
                .ThenBy(stem => stem.id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (loaded.stems.Count == 0)
                return false;

            manifest = loaded;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StemSeparation] Failed to load stem manifest '{manifestPath}': {ex.Message}");
            return false;
        }
    }

    public static StemGenerationStatus GetStatus(string sourceAudioPath)
    {
        string key = NormalizePath(sourceAudioPath);
        if (string.IsNullOrWhiteSpace(key))
            return new StemGenerationStatus();

        lock (Gate)
        {
            if (!StatusByKey.TryGetValue(key, out StemGenerationStatus status) || status == null)
                return new StemGenerationStatus();

            return new StemGenerationStatus
            {
                state = status.state,
                message = status.message,
                error = status.error,
                consumed = status.consumed
            };
        }
    }

    public static void MarkStatusConsumed(string sourceAudioPath)
    {
        string key = NormalizePath(sourceAudioPath);
        if (string.IsNullOrWhiteSpace(key))
            return;

        lock (Gate)
        {
            if (StatusByKey.TryGetValue(key, out StemGenerationStatus status) && status != null)
                status.consumed = true;
        }
    }

    public static bool IsManagedRuntimeReady()
    {
        return TryResolveManagedRuntimeRoot(out _);
    }

    public static bool IsManagedRuntimeInstallAvailable()
    {
        return TryResolveRuntimeInstallSource(out _, out _) || CanInstallRuntimeOnline();
    }

    public static StemRuntimeInstallStatus GetRuntimeInstallStatus()
    {
        lock (Gate)
        {
            return new StemRuntimeInstallStatus
            {
                state = RuntimeInstallStatus.state,
                message = RuntimeInstallStatus.message,
                error = RuntimeInstallStatus.error,
                progressPercent = RuntimeInstallStatus.progressPercent,
                consumed = RuntimeInstallStatus.consumed
            };
        }
    }

    public static void MarkRuntimeInstallStatusConsumed()
    {
        lock (Gate)
            RuntimeInstallStatus.consumed = true;
    }

    public static bool StartManagedRuntimeInstall(out string error)
    {
        error = string.Empty;
        ExternalContentPaths.EnsureUnityRootsCaptured();
        lock (Gate)
        {
            if (RuntimeInstallStatus.state == StemRuntimeInstallState.Running)
            {
                error = "Stem generator installation is already running.";
                return false;
            }

            RuntimeInstallStatus = new StemRuntimeInstallStatus
            {
                state = StemRuntimeInstallState.Running,
                message = "Starting stem generator install...",
                error = string.Empty,
                progressPercent = 0f,
                consumed = false
            };
        }

        WriteRuntimeInstallLog("Install requested.");
        Thread installThread = new Thread(RunManagedRuntimeInstallTask)
        {
            IsBackground = true,
            Name = "StemGeneratorInstall"
        };
        installThread.Start();
        WriteRuntimeInstallLog("Install worker thread scheduled.");
        return true;
    }

    public static bool StartDemucsGeneration(string sourceAudioPath, out string error)
    {
        error = string.Empty;
        ExternalContentPaths.EnsureUnityRootsCaptured();
        if (string.IsNullOrWhiteSpace(sourceAudioPath) || !File.Exists(sourceAudioPath))
        {
            error = "No source audio file is available for this song.";
            return false;
        }

        if (!IsManagedRuntimeReady() && IsManagedRuntimeInstallAvailable())
        {
            error = "Install the stem generator before generating stems.";
            return false;
        }

        string key = NormalizePath(sourceAudioPath);
        lock (Gate)
        {
            if (StatusByKey.TryGetValue(key, out StemGenerationStatus existing) &&
                existing != null &&
                existing.state == StemGenerationState.Running)
            {
                error = "Stem generation is already running for this song.";
                return false;
            }

            StatusByKey[key] = new StemGenerationStatus
            {
                state = StemGenerationState.Running,
                message = "Preparing Demucs stem generation...",
                error = string.Empty,
                consumed = false
            };
        }

        Task.Run(() => GenerateWithDemucs(sourceAudioPath, key));
        return true;
    }

    public static string ResolveStemPath(string sourceAudioPath, StemCacheEntry stem)
    {
        if (stem == null || string.IsNullOrWhiteSpace(stem.relativePath))
            return string.Empty;

        return Path.Combine(GetCacheDirectory(sourceAudioPath), stem.relativePath);
    }

    public static string FormatStemDisplayName(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "Stem";

        string normalized = id.Trim();
        if (normalized.Length == 1)
            return normalized.ToUpperInvariant();

        return char.ToUpperInvariant(normalized[0]) + normalized.Substring(1);
    }

    private static void InstallManagedRuntime()
    {
        string runtimeDirectory = ExternalContentPaths.PersistentStemSeparatorRuntimeDirectory;
        string tempDirectory = runtimeDirectory + ".installing";

        try
        {
            WriteRuntimeInstallLog($"Worker started. runtime='{runtimeDirectory}', temp='{tempDirectory}'");
            UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, "Checking stem generator install... 0%", string.Empty, 0f);

            if (TryResolveManagedRuntimeRoot(out _))
            {
                WriteRuntimeInstallLog("Runtime already available.");
                UpdateRuntimeInstallStatus(StemRuntimeInstallState.Succeeded, "Stem generator already installed.", string.Empty, 100f);
                return;
            }

            UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, "Preparing install folder... 0%", string.Empty, 0f);

            if (Directory.Exists(tempDirectory))
            {
                WriteRuntimeInstallLog($"Deleting previous partial install: {tempDirectory}");
                UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, "Cleaning previous install attempt... 0%", string.Empty, 0f);
                Directory.Delete(tempDirectory, true);
            }
            Directory.CreateDirectory(tempDirectory);
            WriteRuntimeInstallLog("Install folder ready.");

            string sourcePath;
            if (TryResolveRuntimeInstallSource(out sourcePath, out RuntimeInstallSourceKind sourceKind))
            {
                WriteRuntimeInstallLog($"Installing from local {sourceKind}: {sourcePath}");
                UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, "Installing from local package... 0%", string.Empty, 0f);
                if (sourceKind == RuntimeInstallSourceKind.Zip)
                    ExtractRuntimeZip(sourcePath, tempDirectory);
                else
                    CopyRuntimeDirectory(sourcePath, tempDirectory);
            }
            else
            {
                if (!CanInstallRuntimeOnline())
                    throw new FileNotFoundException($"Stem generator package not found. Add '{ExternalContentPaths.StemSeparatorPackageFileName}' to '{ExternalContentPaths.StreamingStemSeparatorDirectory}'.");

                sourcePath = "online";
                WriteRuntimeInstallLog("No local package found. Starting online install.");
                UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, "Starting online install... 0%", string.Empty, 0f);
                InstallRuntimeFromOnline(tempDirectory);
            }

            UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, "Finalizing stem generator install... 98%", string.Empty, 98f);
            string runtimeRoot = ResolveRuntimeRoot(tempDirectory);
            if (string.IsNullOrWhiteSpace(runtimeRoot))
                throw new InvalidOperationException("Installed stem generator package did not contain StemSeparator.exe or python.exe.");

            WriteRuntimeInstallLog($"Resolved runtime root: {runtimeRoot}");
            WriteRuntimeInstallManifest(runtimeRoot, sourcePath);

            if (Directory.Exists(runtimeDirectory))
            {
                WriteRuntimeInstallLog($"Replacing existing runtime: {runtimeDirectory}");
                Directory.Delete(runtimeDirectory, true);
            }

            if (string.Equals(Path.GetFullPath(runtimeRoot), Path.GetFullPath(tempDirectory), StringComparison.OrdinalIgnoreCase))
            {
                Directory.Move(tempDirectory, runtimeDirectory);
            }
            else
            {
                Directory.Move(runtimeRoot, runtimeDirectory);
                if (Directory.Exists(tempDirectory))
                    Directory.Delete(tempDirectory, true);
            }

            WriteRuntimeInstallLog("Install completed.");
            UpdateRuntimeInstallStatus(StemRuntimeInstallState.Succeeded, "Stem generator installed.", string.Empty, 100f);
        }
        catch (Exception ex)
        {
            WriteRuntimeInstallLog($"Install failed: {ex}");
            try
            {
                if (Directory.Exists(tempDirectory))
                    Directory.Delete(tempDirectory, true);
            }
            catch
            {
            }

            UpdateRuntimeInstallStatus(StemRuntimeInstallState.Failed, "Stem generator install failed.", ex.Message, 0f);
            Debug.LogWarning($"[StemSeparation] Stem generator install failed: {ex.Message}");
        }
    }

    private static void RunManagedRuntimeInstallTask()
    {
        try
        {
            InstallManagedRuntime();
        }
        catch (Exception ex)
        {
            WriteRuntimeInstallLog($"Unhandled install task failure: {ex}");
            UpdateRuntimeInstallStatus(StemRuntimeInstallState.Failed, "Stem generator install failed.", ex.Message, 0f);
        }
    }

    private static void ExtractRuntimeZip(string zipPath, string destinationDirectory)
    {
        long totalBytes = 0;
        using (ZipArchive archive = ZipFile.OpenRead(zipPath))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!IsZipDirectory(entry))
                    totalBytes += Math.Max(0L, entry.Length);
            }

            long copiedBytes = 0;
            string destinationRoot = Path.GetFullPath(destinationDirectory);
            if (!destinationRoot.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !destinationRoot.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                destinationRoot += Path.DirectorySeparatorChar;
            }

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string targetPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                if (!targetPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Unsafe path in stem generator package: {entry.FullName}");

                if (IsZipDirectory(entry))
                {
                    Directory.CreateDirectory(targetPath);
                    continue;
                }

                string directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                using (Stream input = entry.Open())
                using (FileStream output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    CopyStreamWithProgress(input, output, totalBytes, ref copiedBytes, "Installing stem generator");
                }
            }
        }
    }

    private static void CopyRuntimeDirectory(string sourceDirectory, string destinationDirectory)
    {
        string[] files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        long totalBytes = files.Sum(path => new FileInfo(path).Length);
        long copiedBytes = 0;

        for (int i = 0; i < files.Length; i++)
        {
            string sourceFile = files[i];
            string relativePath = GetRelativePath(sourceDirectory, sourceFile);
            string destinationFile = Path.Combine(destinationDirectory, relativePath);
            string directory = Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            using (FileStream input = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (FileStream output = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                CopyStreamWithProgress(input, output, totalBytes, ref copiedBytes, "Installing stem generator");
            }
        }
    }

    private static void InstallRuntimeFromOnline(string destinationDirectory)
    {
        if (!CanInstallRuntimeOnline())
            throw new PlatformNotSupportedException("Online stem generator installation is only supported on Windows.");

        WriteRuntimeInstallLog("Online install selected.");
        UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, "Preparing online install... 0%", string.Empty, 0f);

        string downloadsDirectory = Path.Combine(destinationDirectory, "_downloads");
        string pythonDirectory = Path.Combine(destinationDirectory, ExternalContentPaths.StemSeparatorPythonFolderName);
        Directory.CreateDirectory(downloadsDirectory);
        Directory.CreateDirectory(pythonDirectory);
        WriteRuntimeInstallLog($"Download directory: {downloadsDirectory}");
        WriteRuntimeInstallLog($"Python directory: {pythonDirectory}");

        string pythonZipPath = Path.Combine(downloadsDirectory, "python-embed-amd64.zip");
        string pipZipAppPath = Path.Combine(downloadsDirectory, "pip.pyz");
        string antlrRuntimeSourceArchivePath = Path.Combine(downloadsDirectory, "antlr4-python3-runtime-4.9.3.tar.gz");
        string juliusSourceArchivePath = Path.Combine(downloadsDirectory, "julius-0.2.7.tar.gz");
        string doraSourceArchivePath = Path.Combine(downloadsDirectory, "dora_search-0.1.12.tar.gz");
        string demucsSourceArchivePath = Path.Combine(downloadsDirectory, "demucs-4.0.1.tar.gz");

        DownloadFileWithProgress(WindowsPythonEmbedUrl, pythonZipPath, 0f, 16f, "Downloading Python");
        WriteRuntimeInstallLog("Python download finished.");
        UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, "Extracting Python... 18%", string.Empty, 18f);
        ZipFile.ExtractToDirectory(pythonZipPath, pythonDirectory);
        WriteRuntimeInstallLog("Python extracted.");
        PrepareEmbeddedPythonForPip(pythonDirectory);
        WriteRuntimeInstallLog("Embedded Python prepared for pip.");

        DownloadFileWithProgress(PipZipAppUrl, pipZipAppPath, 20f, 28f, "Downloading package installer");
        WriteRuntimeInstallLog("Package installer download finished.");

        string pythonExe = Path.Combine(pythonDirectory, ExternalContentPaths.StemSeparatorPythonExeFileName);
        RunInstallerProcess(
            pythonExe,
            $"{Quote(pipZipAppPath)} install --no-warn-script-location --disable-pip-version-check --no-input --no-cache-dir --prefer-binary --upgrade {StemRuntimePackageTools}",
            pythonDirectory,
            28f,
            36f,
            "Installing Python package tools");

        RunInstallerProcess(
            pythonExe,
            "-c \"import setuptools, setuptools.build_meta, wheel; print('python package tools ready')\"",
            pythonDirectory,
            36f,
            38f,
            "Verifying Python package tools");

        DownloadFileWithProgress(AntlrRuntimeSourceUrl, antlrRuntimeSourceArchivePath, 38f, 39f, "Downloading ANTLR runtime");
        InstallSourcePackageFromTarGz(pythonExe, pythonDirectory, antlrRuntimeSourceArchivePath, downloadsDirectory, "antlr4-python3-runtime-4.9.3", 39f, 40f, "ANTLR runtime");

        RunInstallerProcess(
            pythonExe,
            $"{Quote(pipZipAppPath)} install --no-warn-script-location --disable-pip-version-check --no-input --no-cache-dir --prefer-binary --no-build-isolation --upgrade --extra-index-url {Quote(PytorchCpuIndexUrl)} {StemRuntimeDependencyPackages}",
            pythonDirectory,
            40f,
            80f,
            "Installing stem generator dependencies");

        DownloadFileWithProgress(JuliusSourceUrl, juliusSourceArchivePath, 80f, 82f, "Downloading Julius");
        InstallSourcePackageFromTarGz(pythonExe, pythonDirectory, juliusSourceArchivePath, downloadsDirectory, "julius-0.2.7", 82f, 84f, "Julius");

        DownloadFileWithProgress(DoraSearchSourceUrl, doraSourceArchivePath, 84f, 86f, "Downloading Dora");
        InstallSourcePackageFromTarGz(pythonExe, pythonDirectory, doraSourceArchivePath, downloadsDirectory, "dora_search-0.1.12", 86f, 88f, "Dora");

        DownloadFileWithProgress(DemucsSourceUrl, demucsSourceArchivePath, 88f, 90f, "Downloading Demucs");
        InstallSourcePackageFromTarGz(pythonExe, pythonDirectory, demucsSourceArchivePath, downloadsDirectory, "demucs-4.0.1", 90f, 94f, "Demucs");

        RunInstallerProcess(
            pythonExe,
            "-c \"import antlr4, demucs, julius, torch, torchaudio, soundfile; print('stem generator ready')\"",
            pythonDirectory,
            94f,
            98f,
            "Verifying stem generator");

        try
        {
            Directory.Delete(downloadsDirectory, true);
        }
        catch
        {
        }

        UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, "Stem generator install complete... 100%", string.Empty, 100f);
    }

    private static void InstallSourcePackageFromTarGz(
        string pythonExe,
        string pythonDirectory,
        string archivePath,
        string downloadsDirectory,
        string sourceDirectoryName,
        float startPercent,
        float endPercent,
        string label)
    {
        string sourceRoot = Path.Combine(downloadsDirectory, "src");
        string packageDirectory = Path.Combine(sourceRoot, sourceDirectoryName);
        if (Directory.Exists(packageDirectory))
            Directory.Delete(packageDirectory, true);
        Directory.CreateDirectory(sourceRoot);

        float extractEndPercent = Mathf.Min(endPercent, startPercent + 1f);
        RunInstallerProcess(
            pythonExe,
            $"-c \"import sys, tarfile; tarfile.open(sys.argv[1], 'r:gz').extractall(sys.argv[2])\" {Quote(archivePath)} {Quote(sourceRoot)}",
            pythonDirectory,
            startPercent,
            extractEndPercent,
            $"Extracting {label}");

        if (!Directory.Exists(packageDirectory))
            throw new DirectoryNotFoundException($"{label} source package did not extract to {packageDirectory}");

        string setupPath = Path.Combine(packageDirectory, "setup.py");
        if (!File.Exists(setupPath))
            throw new FileNotFoundException($"{label} source package did not contain setup.py.", setupPath);

        RunInstallerProcess(
            pythonExe,
            $"{Quote(setupPath)} install",
            packageDirectory,
            extractEndPercent,
            endPercent,
            $"Installing {label}");
    }

    private static void DownloadFileWithProgress(string url, string targetPath, float startPercent, float endPercent, string label)
    {
        WriteRuntimeInstallLog($"{label}: connecting to {url}");
        UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, $"{label}: connecting... {startPercent:F0}%", string.Empty, startPercent);

        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.UserAgent = "GuitarProjectStemInstaller/1.0";
        request.AllowAutoRedirect = true;
        request.Timeout = 30000;
        request.ReadWriteTimeout = 30000;

        using (WebResponse response = request.GetResponse())
        using (Stream input = response.GetResponseStream())
        using (FileStream output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            if (input == null)
                throw new IOException($"No response body while downloading {url}");

            long totalBytes = response.ContentLength;
            WriteRuntimeInstallLog($"{label}: response received. contentLength={totalBytes}");
            UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, $"{label}: downloading... {startPercent:F0}%", string.Empty, startPercent);
            long copiedBytes = 0;
            byte[] buffer = new byte[CopyBufferSize];
            int read;
            DateTime lastLogTime = DateTime.UtcNow;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                copiedBytes += read;
                float fraction = totalBytes > 0
                    ? Mathf.Clamp01(copiedBytes / (float)totalBytes)
                    : Mathf.Clamp01(copiedBytes / (20f * 1024f * 1024f));
                float progress = Mathf.Lerp(startPercent, endPercent, fraction);
                UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, $"{label}... {progress:F0}%", string.Empty, progress);
                if ((DateTime.UtcNow - lastLogTime).TotalSeconds >= 2.0)
                {
                    WriteRuntimeInstallLog($"{label}: downloaded {copiedBytes} / {totalBytes} bytes.");
                    lastLogTime = DateTime.UtcNow;
                }
            }
        }

        WriteRuntimeInstallLog($"{label}: saved to {targetPath}");
    }

    private static void PrepareEmbeddedPythonForPip(string pythonDirectory)
    {
        Directory.CreateDirectory(Path.Combine(pythonDirectory, "Lib", "site-packages"));
        string pthPath = Directory.GetFiles(pythonDirectory, "python*._pth").FirstOrDefault();
        if (string.IsNullOrWhiteSpace(pthPath) || !File.Exists(pthPath))
            return;

        List<string> lines = File.ReadAllLines(pthPath).ToList();
        bool hasSitePackages = lines.Any(line => string.Equals(line.Trim(), "Lib\\site-packages", StringComparison.OrdinalIgnoreCase));
        bool hasImportSite = false;
        for (int i = 0; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].Trim(), "#import site", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = "import site";
                hasImportSite = true;
            }
            else if (string.Equals(lines[i].Trim(), "import site", StringComparison.OrdinalIgnoreCase))
            {
                hasImportSite = true;
            }
        }

        if (!hasSitePackages)
            lines.Insert(Math.Max(0, lines.Count - 1), "Lib\\site-packages");
        if (!hasImportSite)
            lines.Add("import site");

        File.WriteAllLines(pthPath, lines);
    }

    private static void RunInstallerProcess(string fileName, string arguments, string workingDirectory, float startPercent, float endPercent, string label)
    {
        List<string> recentLines = new List<string>();
        object linesGate = new object();
        float[] currentProgress = { startPercent };
        UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, $"{label}... {startPercent:F0}%", string.Empty, startPercent);
        WriteRuntimeInstallLog($"{label}: launching {fileName} {arguments}");

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
        startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        startInfo.EnvironmentVariables["PYTHONNOUSERSITE"] = "1";
        startInfo.EnvironmentVariables["PIP_DISABLE_PIP_VERSION_CHECK"] = "1";
        startInfo.EnvironmentVariables["PIP_NO_INPUT"] = "1";
        PrependProcessPath(startInfo, workingDirectory);

        using (Process process = new Process())
        {
            process.StartInfo = startInfo;
            process.OutputDataReceived += (_, e) => CaptureInstallerLine(e.Data, recentLines, linesGate, currentProgress, endPercent, label);
            process.ErrorDataReceived += (_, e) => CaptureInstallerLine(e.Data, recentLines, linesGate, currentProgress, endPercent, label);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            DateTime startedAt = DateTime.UtcNow;
            DateTime lastHeartbeatAt = startedAt;
            while (!process.WaitForExit(1000))
            {
                double elapsedMilliseconds = Math.Max(0.0, (DateTime.UtcNow - startedAt).TotalMilliseconds);
                if (elapsedMilliseconds >= InstallerProcessTimeoutMilliseconds)
                {
                    TryKillProcess(process);
                    WriteRuntimeInstallLog($"{label}: timed out after {elapsedMilliseconds / 1000.0:F0}s");
                    throw new TimeoutException($"{label} timed out. Check your network connection and try again.");
                }

                if ((DateTime.UtcNow - lastHeartbeatAt).TotalSeconds >= 10.0)
                {
                    float heartbeatProgress;
                    lock (linesGate)
                    {
                        float timeFraction = Mathf.Clamp01((float)(elapsedMilliseconds / InstallerProcessTimeoutMilliseconds));
                        float predictedProgress = Mathf.Lerp(startPercent, endPercent - 0.2f, timeFraction * 0.85f);
                        float previousProgress = currentProgress.Length > 0 ? currentProgress[0] : startPercent;
                        heartbeatProgress = Mathf.Max(previousProgress, predictedProgress);
                        if (currentProgress.Length > 0)
                            currentProgress[0] = heartbeatProgress;
                    }

                    UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, $"{label}... {heartbeatProgress:F0}%", string.Empty, heartbeatProgress);
                    WriteRuntimeInstallLog($"{label}: still running after {elapsedMilliseconds / 1000.0:F0}s.");
                    lastHeartbeatAt = DateTime.UtcNow;
                }
            }
            WriteRuntimeInstallLog($"{label}: exited with code {process.ExitCode}");

            if (process.ExitCode != 0)
            {
                string outputTail;
                lock (linesGate)
                    outputTail = string.Join(" | ", recentLines);

                throw new InvalidOperationException($"{label} failed with code {process.ExitCode}: {outputTail}");
            }
        }

        UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, $"{label}... {endPercent:F0}%", string.Empty, endPercent);
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (process != null && !process.HasExited)
                process.Kill();
        }
        catch
        {
        }
    }

    private static void CaptureInstallerLine(string line, List<string> recentLines, object linesGate, float[] currentProgress, float endPercent, string label)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        string trimmed = line.Trim();
        float progress;
        lock (linesGate)
        {
            recentLines.Add(trimmed);
            while (recentLines.Count > 8)
                recentLines.RemoveAt(0);

            float previousProgress = currentProgress != null && currentProgress.Length > 0 ? currentProgress[0] : 0f;
            progress = Mathf.Min(endPercent - 0.2f, previousProgress + Mathf.Max(0.1f, (endPercent - previousProgress) * 0.04f));
            if (currentProgress != null && currentProgress.Length > 0)
                currentProgress[0] = progress;
        }

        WriteRuntimeInstallLog($"{label}: {trimmed}");
        UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, $"{label}... {progress:F0}%", string.Empty, progress);
    }

    private static void CopyStreamWithProgress(Stream input, Stream output, long totalBytes, ref long copiedBytes, string label)
    {
        byte[] buffer = new byte[CopyBufferSize];
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, read);
            copiedBytes += read;
            float progress = totalBytes > 0 ? Mathf.Clamp01(copiedBytes / (float)totalBytes) * 100f : 100f;
            UpdateRuntimeInstallStatus(StemRuntimeInstallState.Running, $"{label}... {progress:F0}%", string.Empty, progress);
        }
    }

    private static void WriteRuntimeInstallManifest(string runtimeRoot, string sourcePath)
    {
        FileInfo sourceInfo = File.Exists(sourcePath) ? new FileInfo(sourcePath) : null;
        StemRuntimeInstallManifest manifest = new StemRuntimeInstallManifest
        {
            schemaVersion = 1,
            sourcePath = NormalizePath(sourcePath),
            sourceLastWriteUtcTicks = sourceInfo != null ? sourceInfo.LastWriteTimeUtc.Ticks : 0L,
            sourceSizeBytes = sourceInfo != null ? sourceInfo.Length : 0L,
            generatedAtUtcTicks = DateTime.UtcNow.Ticks
        };

        File.WriteAllText(Path.Combine(runtimeRoot, ExternalContentPaths.StemSeparatorInstallManifestFileName), JsonUtility.ToJson(manifest, true));
    }

    private static void GenerateWithDemucs(string sourceAudioPath, string key)
    {
        string cacheDirectory = GetCacheDirectory(sourceAudioPath);
        string tempCacheDirectory = cacheDirectory + ".generating";
        string workDirectory = Path.Combine(
            ExternalContentPaths.PersistentStemSeparatorDirectory,
            StemSeparatorWorkFolderName,
            "guitarproject_stems_" + Guid.NewGuid().ToString("N"));

        try
        {
            UpdateStatus(key, StemGenerationState.Running, "Running Demucs. This can take several minutes.", string.Empty);

            if (Directory.Exists(tempCacheDirectory))
                Directory.Delete(tempCacheDirectory, true);
            Directory.CreateDirectory(tempCacheDirectory);
            Directory.CreateDirectory(workDirectory);

            string demucsOutput = Path.Combine(workDirectory, "demucs");
            string resultDirectory = RunDemucs(sourceAudioPath, demucsOutput, key);
            List<StemCacheEntry> stems = CopyProducedStems(resultDirectory, tempCacheDirectory, workDirectory, key);
            if (stems.Count == 0)
                throw new InvalidOperationException("Demucs completed but did not produce any supported stems.");

            FileInfo sourceInfo = new FileInfo(sourceAudioPath);
            StemCacheManifest manifest = new StemCacheManifest
            {
                schemaVersion = 1,
                sourceAudioPath = NormalizePath(sourceAudioPath),
                sourceAudioLastWriteUtcTicks = sourceInfo.LastWriteTimeUtc.Ticks,
                sourceAudioSizeBytes = sourceInfo.Length,
                provider = "demucs",
                model = DefaultDemucsModel,
                generatedAtUtcTicks = DateTime.UtcNow.Ticks,
                status = "ready",
                error = string.Empty,
                stems = stems
            };

            File.WriteAllText(Path.Combine(tempCacheDirectory, ManifestFileName), JsonUtility.ToJson(manifest, true));

            if (Directory.Exists(cacheDirectory))
                Directory.Delete(cacheDirectory, true);
            Directory.Move(tempCacheDirectory, cacheDirectory);

            UpdateStatus(key, StemGenerationState.Succeeded, $"Generated {stems.Count} stems.", string.Empty);
        }
        catch (Exception ex)
        {
            try
            {
                if (Directory.Exists(tempCacheDirectory))
                    Directory.Delete(tempCacheDirectory, true);
            }
            catch
            {
            }

            UpdateStatus(key, StemGenerationState.Failed, "Stem generation failed.", ex.Message);
            Debug.LogWarning($"[StemSeparation] Stem generation failed for '{sourceAudioPath}': {ex.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(workDirectory))
                    Directory.Delete(workDirectory, true);
            }
            catch
            {
            }
        }
    }

    private static bool TryBuildManagedDemucsCommand(string sourceAudioPath, string demucsOutputDirectory, out DemucsCommand command)
    {
        command = default;
        if (!TryResolveManagedRuntimeRoot(out string runtimeRoot))
            return false;

        string separatorExe = Path.Combine(runtimeRoot, ExternalContentPaths.StemSeparatorExeFileName);
        if (File.Exists(separatorExe))
        {
            command = new DemucsCommand(
                separatorExe,
                $"-n {Quote(DefaultDemucsModel)} -o {Quote(demucsOutputDirectory)} {Quote(sourceAudioPath)}",
                runtimeRoot);
            return true;
        }

        string pythonExe = ResolvePythonExecutable(runtimeRoot);
        if (File.Exists(pythonExe))
        {
            command = new DemucsCommand(
                pythonExe,
                $"-m demucs -n {Quote(DefaultDemucsModel)} -o {Quote(demucsOutputDirectory)} {Quote(sourceAudioPath)}",
                runtimeRoot);
            return true;
        }

        return false;
    }

    private static bool TryResolveManagedRuntimeRoot(out string runtimeRoot)
    {
        runtimeRoot = string.Empty;
        string persistentRoot = ExternalContentPaths.PersistentStemSeparatorRuntimeDirectory;
        if (HasRuntimeCommandAtRoot(persistentRoot))
        {
            runtimeRoot = persistentRoot;
            return true;
        }

        string streamingRoot = ExternalContentPaths.StreamingStemSeparatorRuntimeDirectory;
        if (HasRuntimeCommandAtRoot(streamingRoot))
        {
            runtimeRoot = streamingRoot;
            return true;
        }

        return false;
    }

    private static bool TryResolveRuntimeInstallSource(out string sourcePath, out RuntimeInstallSourceKind sourceKind)
    {
        string persistentPackage = ExternalContentPaths.PersistentStemSeparatorPackagePath;
        if (File.Exists(persistentPackage))
        {
            sourcePath = persistentPackage;
            sourceKind = RuntimeInstallSourceKind.Zip;
            return true;
        }

        string streamingPackage = ExternalContentPaths.StreamingStemSeparatorPackagePath;
        if (File.Exists(streamingPackage))
        {
            sourcePath = streamingPackage;
            sourceKind = RuntimeInstallSourceKind.Zip;
            return true;
        }

        string streamingRuntime = ExternalContentPaths.StreamingStemSeparatorRuntimeDirectory;
        if (HasRuntimeCommandAtRoot(streamingRuntime))
        {
            sourcePath = streamingRuntime;
            sourceKind = RuntimeInstallSourceKind.Directory;
            return true;
        }

        sourcePath = string.Empty;
        sourceKind = RuntimeInstallSourceKind.Zip;
        return false;
    }

    private static bool CanInstallRuntimeOnline()
    {
        return Environment.OSVersion.Platform == PlatformID.Win32NT;
    }

    private static string ResolveRuntimeRoot(string directory)
    {
        if (HasRuntimeCommandAtRoot(directory))
            return directory;

        if (!Directory.Exists(directory))
            return string.Empty;

        foreach (string child in Directory.GetDirectories(directory, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path.Length)
                     .ThenBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (HasRuntimeCommandAtRoot(child))
                return child;
        }

        return string.Empty;
    }

    private static bool HasRuntimeCommandAtRoot(string runtimeRoot)
    {
        if (string.IsNullOrWhiteSpace(runtimeRoot) || !Directory.Exists(runtimeRoot))
            return false;

        if (File.Exists(Path.Combine(runtimeRoot, ExternalContentPaths.StemSeparatorExeFileName)))
            return true;

        return File.Exists(ResolvePythonExecutable(runtimeRoot));
    }

    private static string ResolvePythonExecutable(string runtimeRoot)
    {
        string nestedPython = Path.Combine(runtimeRoot, ExternalContentPaths.StemSeparatorPythonFolderName, ExternalContentPaths.StemSeparatorPythonExeFileName);
        if (File.Exists(nestedPython))
            return nestedPython;

        return Path.Combine(runtimeRoot, ExternalContentPaths.StemSeparatorPythonExeFileName);
    }

    private static string RunDemucs(string sourceAudioPath, string demucsOutputDirectory, string key)
    {
        Directory.CreateDirectory(demucsOutputDirectory);
        List<string> errors = new List<string>();
        List<DemucsCommand> commands = new List<DemucsCommand>();
        if (TryBuildManagedDemucsCommand(sourceAudioPath, demucsOutputDirectory, out DemucsCommand managedCommand))
            commands.Add(managedCommand);

        commands.AddRange(new[]
        {
            new DemucsCommand("python", $"-m demucs -n {Quote(DefaultDemucsModel)} -o {Quote(demucsOutputDirectory)} {Quote(sourceAudioPath)}", string.Empty),
            new DemucsCommand("py", $"-3 -m demucs -n {Quote(DefaultDemucsModel)} -o {Quote(demucsOutputDirectory)} {Quote(sourceAudioPath)}", string.Empty),
            new DemucsCommand("python3", $"-m demucs -n {Quote(DefaultDemucsModel)} -o {Quote(demucsOutputDirectory)} {Quote(sourceAudioPath)}", string.Empty)
        });

        for (int i = 0; i < commands.Count; i++)
        {
            DemucsCommand command = commands[i];
            try
            {
                UpdateStatus(key, StemGenerationState.Running, $"Running Demucs with {Path.GetFileName(command.executable)}...", string.Empty);
                int exitCode = RunProcess(command.executable, command.arguments, command.workingDirectory, key, out string outputTail);
                if (exitCode == 0)
                    return ResolveDemucsResultDirectory(demucsOutputDirectory);

                errors.Add($"{command.executable} exited with code {exitCode}: {outputTail}");
            }
            catch (Exception ex)
            {
                errors.Add($"{command.executable}: {ex.Message}");
            }
        }

        throw new InvalidOperationException(
            "Could not run Demucs. Install the stem generator package or install Demucs manually with `pip install demucs`. " +
            string.Join(" | ", errors));
    }

    private static int RunProcess(string fileName, string arguments, string workingDirectory, string key, out string outputTail)
    {
        List<string> recentLines = new List<string>();
        object linesGate = new object();
        Directory.CreateDirectory(ExternalContentPaths.PersistentStemSeparatorModelCacheDirectory);

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = !string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory)
                ? workingDirectory
                : Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
        startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        startInfo.EnvironmentVariables["TORCH_HOME"] = ExternalContentPaths.PersistentStemSeparatorModelCacheDirectory;
        startInfo.EnvironmentVariables["XDG_CACHE_HOME"] = ExternalContentPaths.PersistentStemSeparatorModelCacheDirectory;
        string processTempDirectory = GetStemSeparatorTempDirectory();
        startInfo.EnvironmentVariables["TEMP"] = processTempDirectory;
        startInfo.EnvironmentVariables["TMP"] = processTempDirectory;
        startInfo.EnvironmentVariables["TMPDIR"] = processTempDirectory;
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            PrependProcessPath(startInfo, workingDirectory);
            PrependProcessPath(startInfo, Path.Combine(workingDirectory, "bin"));
            PrependProcessPath(startInfo, Path.Combine(workingDirectory, "Library", "bin"));
        }

        using (Process process = new Process())
        {
            process.StartInfo = startInfo;
            process.OutputDataReceived += (_, e) => CaptureProcessLine(e.Data, key, recentLines, linesGate);
            process.ErrorDataReceived += (_, e) => CaptureProcessLine(e.Data, key, recentLines, linesGate);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            lock (linesGate)
                outputTail = string.Join(" | ", recentLines);

            return process.ExitCode;
        }
    }

    private static string GetStemSeparatorTempDirectory()
    {
        string tempDirectory = Path.Combine(ExternalContentPaths.PersistentStemSeparatorDirectory, StemSeparatorTempFolderName);
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static void PrependProcessPath(ProcessStartInfo startInfo, string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return;

        string pathKey = "PATH";
        foreach (string key in startInfo.EnvironmentVariables.Keys)
        {
            if (string.Equals(key, "PATH", StringComparison.OrdinalIgnoreCase))
            {
                pathKey = key;
                break;
            }
        }

        string existing = startInfo.EnvironmentVariables[pathKey] ?? string.Empty;
        startInfo.EnvironmentVariables[pathKey] = string.IsNullOrWhiteSpace(existing)
            ? directory
            : directory + Path.PathSeparator + existing;
    }

    private static void CaptureProcessLine(string line, string key, List<string> recentLines, object linesGate)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        string trimmed = SanitizeProcessStatusLine(line);
        if (string.IsNullOrWhiteSpace(trimmed))
            return;

        lock (linesGate)
        {
            recentLines.Add(trimmed);
            while (recentLines.Count > 8)
                recentLines.RemoveAt(0);
        }

        UpdateStatus(key, StemGenerationState.Running, trimmed, string.Empty);
    }

    private static string SanitizeProcessStatusLine(string line)
    {
        string cleaned = Regex.Replace(line ?? string.Empty, @"\x1B\[[0-?]*[ -/]*[@-~]", string.Empty);
        cleaned = cleaned.Replace('\r', ' ').Replace('\n', ' ').Trim();
        Match progressMatch = Regex.Match(cleaned, @"(?<percent>\d{1,3})%\|.*?(?<current>\d+(?:\.\d+)?)/(?<total>\d+(?:\.\d+)?)", RegexOptions.CultureInvariant);
        if (progressMatch.Success)
        {
            string percent = progressMatch.Groups["percent"].Value;
            string current = progressMatch.Groups["current"].Value;
            string total = progressMatch.Groups["total"].Value;
            return $"Generating stems... {percent}% ({current}/{total})";
        }

        cleaned = Regex.Replace(cleaned, @"[^\u0020-\u007E]", string.Empty);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        if (cleaned.Length > 180)
            cleaned = cleaned.Substring(0, 180).TrimEnd() + "...";
        return cleaned;
    }

    private static string ResolveDemucsResultDirectory(string demucsOutputDirectory)
    {
        string modelDirectory = Path.Combine(demucsOutputDirectory, DefaultDemucsModel);
        if (!Directory.Exists(modelDirectory))
            throw new DirectoryNotFoundException($"Demucs output folder was not found: {modelDirectory}");

        string[] candidates = Directory.GetDirectories(modelDirectory);
        if (candidates.Length == 0)
            throw new DirectoryNotFoundException($"Demucs produced no track folder under: {modelDirectory}");

        return candidates
            .OrderByDescending(path => Directory.GetFiles(path, "*.wav").Length)
            .First();
    }

    private static List<StemCacheEntry> CopyProducedStems(string resultDirectory, string cacheDirectory, string workDirectory, string key)
    {
        string stemsDirectory = Path.Combine(cacheDirectory, "stems");
        Directory.CreateDirectory(stemsDirectory);

        List<StemCacheEntry> stems = new List<StemCacheEntry>();
        foreach (string wavPath in Directory.GetFiles(resultDirectory, "*.wav"))
        {
            string stemId = Path.GetFileNameWithoutExtension(wavPath)?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(stemId))
                continue;

            string outputFileName;
            string outputPath;
            UpdateStatus(key, StemGenerationState.Running, $"Encoding {FormatStemDisplayName(stemId)} stem...", string.Empty);
            string oggFileName = stemId + ".ogg";
            string oggPath = Path.Combine(stemsDirectory, oggFileName);
            if (TryEncodeWavStemToOgg(wavPath, oggPath, workDirectory, key, out string encodeError))
            {
                outputFileName = oggFileName;
                outputPath = oggPath;
            }
            else
            {
                outputFileName = stemId + ".wav";
                outputPath = Path.Combine(stemsDirectory, outputFileName);
                UpdateStatus(key, StemGenerationState.Running, $"Keeping {FormatStemDisplayName(stemId)} stem as WAV fallback...", string.Empty);
                Debug.LogWarning($"[StemSeparation] OGG encoding failed for '{wavPath}'. Keeping WAV fallback. {encodeError}");
                File.Copy(wavPath, outputPath, true);
            }

            stems.Add(new StemCacheEntry
            {
                id = stemId,
                displayName = FormatStemDisplayName(stemId),
                relativePath = Path.Combine("stems", outputFileName).Replace('\\', '/')
            });
        }

        return stems
            .OrderBy(stem => GetStemOrder(stem.id))
            .ThenBy(stem => stem.id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryEncodeWavStemToOgg(string wavPath, string outputPath, string workDirectory, string key, out string error)
    {
        error = string.Empty;
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        string scriptPath = Path.Combine(workDirectory, OggEncoderScriptFileName);
        File.WriteAllText(scriptPath, OggEncoderPythonScript, Encoding.UTF8);

        List<string> errors = new List<string>();
        foreach (DemucsCommand command in BuildOggEncoderCommands(scriptPath, wavPath, outputPath))
        {
            try
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                int exitCode = RunProcess(command.executable, command.arguments, command.workingDirectory, key, out string outputTail);
                if (exitCode == 0 && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
                    return true;

                errors.Add($"{command.executable} exited with code {exitCode}: {outputTail}");
            }
            catch (Exception ex)
            {
                errors.Add($"{command.executable}: {ex.Message}");
            }
        }

        try
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
        catch
        {
        }

        error = string.Join(" | ", errors);
        return false;
    }

    private static List<DemucsCommand> BuildOggEncoderCommands(string scriptPath, string wavPath, string outputPath)
    {
        List<DemucsCommand> commands = new List<DemucsCommand>();
        if (TryResolveManagedRuntimeRoot(out string runtimeRoot))
        {
            string pythonExe = ResolvePythonExecutable(runtimeRoot);
            if (File.Exists(pythonExe))
            {
                commands.Add(new DemucsCommand(
                    pythonExe,
                    $"{Quote(scriptPath)} {Quote(wavPath)} {Quote(outputPath)}",
                    runtimeRoot));
            }
        }

        commands.AddRange(new[]
        {
            new DemucsCommand("python", $"{Quote(scriptPath)} {Quote(wavPath)} {Quote(outputPath)}", string.Empty),
            new DemucsCommand("py", $"-3 {Quote(scriptPath)} {Quote(wavPath)} {Quote(outputPath)}", string.Empty),
            new DemucsCommand("python3", $"{Quote(scriptPath)} {Quote(wavPath)} {Quote(outputPath)}", string.Empty)
        });
        return commands;
    }

    private static bool IsManifestValidForSource(string sourceAudioPath, StemCacheManifest manifest)
    {
        if (manifest == null || manifest.stems == null || manifest.stems.Count == 0)
            return false;

        FileInfo info = new FileInfo(sourceAudioPath);
        return string.Equals(NormalizePath(sourceAudioPath), NormalizePath(manifest.sourceAudioPath), StringComparison.OrdinalIgnoreCase) &&
               manifest.sourceAudioLastWriteUtcTicks == info.LastWriteTimeUtc.Ticks &&
               manifest.sourceAudioSizeBytes == info.Length;
    }

    private static void UpdateStatus(string key, StemGenerationState state, string message, string error)
    {
        lock (Gate)
        {
            if (!StatusByKey.TryGetValue(key, out StemGenerationStatus status) || status == null)
            {
                status = new StemGenerationStatus();
                StatusByKey[key] = status;
            }

            status.state = state;
            status.message = message ?? string.Empty;
            status.error = error ?? string.Empty;
            status.consumed = false;
        }
    }

    private static void UpdateRuntimeInstallStatus(StemRuntimeInstallState state, string message, string error, float progressPercent)
    {
        lock (Gate)
        {
            RuntimeInstallStatus.state = state;
            RuntimeInstallStatus.message = message ?? string.Empty;
            RuntimeInstallStatus.error = error ?? string.Empty;
            RuntimeInstallStatus.progressPercent = Mathf.Clamp(progressPercent, 0f, 100f);
            RuntimeInstallStatus.consumed = false;
        }
    }

    private static void WriteRuntimeInstallLog(string message)
    {
        try
        {
            lock (RuntimeInstallLogGate)
            {
                Directory.CreateDirectory(ExternalContentPaths.PersistentStemSeparatorDirectory);
                File.AppendAllText(
                    RuntimeInstallLogPath,
                    $"[{DateTime.UtcNow:O}] {message ?? string.Empty}{Environment.NewLine}");
            }
        }
        catch
        {
        }
    }

    private static bool IsZipDirectory(ZipArchiveEntry entry)
    {
        return entry == null || string.IsNullOrEmpty(entry.Name);
    }

    private static string GetRelativePath(string rootDirectory, string filePath)
    {
        string root = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string path = Path.GetFullPath(filePath);
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return Path.GetFileName(filePath);

        string relative = path.Substring(root.Length)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return relative;
    }

    private static int GetStemOrder(string id)
    {
        for (int i = 0; i < PreferredStemOrder.Length; i++)
        {
            if (string.Equals(PreferredStemOrder[i], id, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return PreferredStemOrder.Length;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch
        {
            return path.Trim();
        }
    }

    private static string ComputeStableHash(string value)
    {
        using (SHA1 sha1 = SHA1.Create())
        {
            byte[] bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }
    }

    private static string Quote(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }

    private enum RuntimeInstallSourceKind
    {
        Zip = 0,
        Directory = 1
    }

    private struct DemucsCommand
    {
        public readonly string executable;
        public readonly string arguments;
        public readonly string workingDirectory;

        public DemucsCommand(string executable, string arguments, string workingDirectory)
        {
            this.executable = executable;
            this.arguments = arguments;
            this.workingDirectory = workingDirectory;
        }
    }
}
