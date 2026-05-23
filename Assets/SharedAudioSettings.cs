using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable]
public sealed class SharedAudioSettings
{
    public int version = 6;
    public string inputDeviceName = string.Empty;
    public string outputDeviceName = string.Empty;
    public int monitoringBufferSize = 128;
    public float guitarVolumePercent = 100f;
    public float songVolumePercent = 100f;
    public string detectorResamplerMode = SharedAudioDetectorResamplerModes.Filtered;
    public SharedAudioAdvancedSettings advanced = new SharedAudioAdvancedSettings();
}

[Serializable]
public sealed class SharedAudioAdvancedSettings
{
    public bool betaEnabled;
    public string backendMode = SharedAudioBackendModes.Auto;
    public bool allowFallback = true;
    public string inputDeviceName = string.Empty;
    public string outputDeviceName = string.Empty;
    public int sampleRate;
    public int bufferSize;
    public bool unifiedOutputEnabled;
    public bool unityRecorderCaptureEnabled;
}

public static class SharedAudioBackendModes
{
    public const string Auto = "Auto";
    public const string Wasapi = "WASAPI";
    public const string Asio = "ASIO";
    public const string CoreAudio = "CoreAudio";

    public static string Normalize(string value)
    {
        if (string.Equals(value, Wasapi, StringComparison.OrdinalIgnoreCase))
            return Wasapi;
        if (string.Equals(value, Asio, StringComparison.OrdinalIgnoreCase))
            return Asio;
        if (string.Equals(value, CoreAudio, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "Core Audio", StringComparison.OrdinalIgnoreCase))
        {
            return CoreAudio;
        }

        return Auto;
    }

    public static List<string> GetChoicesForCurrentPlatform()
    {
        if (StringTheoryPlatform.IsMacOS)
            return new List<string> { Auto, CoreAudio };

        return new List<string> { Auto, Wasapi, Asio };
    }

    public static string NormalizeForCurrentPlatform(string value)
    {
        string normalized = Normalize(value);
        return IsSupportedOnCurrentPlatform(normalized) ? normalized : Auto;
    }

    public static bool IsSupportedOnCurrentPlatform(string normalizedValue)
    {
        normalizedValue = Normalize(normalizedValue);
        if (string.Equals(normalizedValue, Auto, StringComparison.Ordinal))
            return true;
        if (StringTheoryPlatform.IsMacOS)
            return string.Equals(normalizedValue, CoreAudio, StringComparison.Ordinal);

        return string.Equals(normalizedValue, Wasapi, StringComparison.Ordinal) ||
               string.Equals(normalizedValue, Asio, StringComparison.Ordinal);
    }

    public static string NormalizeHostApiLabel(string hostApiName)
    {
        if (string.IsNullOrWhiteSpace(hostApiName))
            return "Unknown";

        if (hostApiName.IndexOf("ASIO", StringComparison.OrdinalIgnoreCase) >= 0)
            return Asio;

        if (hostApiName.IndexOf("WASAPI", StringComparison.OrdinalIgnoreCase) >= 0)
            return Wasapi;

        if (hostApiName.IndexOf("Core Audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
            hostApiName.IndexOf("CoreAudio", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return CoreAudio;
        }

        return hostApiName.Trim();
    }

    public static int GetHostPriority(string hostApiName)
    {
        string normalized = NormalizeHostApiLabel(hostApiName);
        if (string.Equals(normalized, Asio, StringComparison.Ordinal))
            return 0;
        if (string.Equals(normalized, CoreAudio, StringComparison.Ordinal))
            return 0;
        if (string.Equals(normalized, Wasapi, StringComparison.Ordinal))
            return 1;
        return 2;
    }
}

public static class SharedAudioSampleRateOptions
{
    public const int Auto = 0;

    public static readonly int[] SupportedRates =
    {
        Auto,
        44100,
        48000,
        96000
    };

    public static int Normalize(int value)
    {
        for (int i = 0; i < SupportedRates.Length; i++)
        {
            if (SupportedRates[i] == value)
                return value;
        }

        return Auto;
    }

    public static string ToLabel(int value)
    {
        return value <= 0 ? "Auto" : $"{value} Hz";
    }
}

public static class SharedAudioDetectorResamplerModes
{
    public const string Filtered = "Filtered";
    public const string Linear = "Linear";

    public static string Normalize(string value)
    {
        return string.Equals(value, Linear, StringComparison.OrdinalIgnoreCase)
            ? Linear
            : Filtered;
    }

    public static string Toggle(string currentValue)
    {
        return string.Equals(Normalize(currentValue), Filtered, StringComparison.OrdinalIgnoreCase)
            ? Linear
            : Filtered;
    }
}

public static class SharedAudioSettingsUtility
{
    private static readonly Regex LeadingIndexRegex = new Regex(@"^\d+\s*:\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static SharedAudioSettings Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new SharedAudioSettings();

        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                SharedAudioSettings loaded = JsonUtility.FromJson<SharedAudioSettings>(json);
                if (loaded != null)
                    return loaded;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SharedAudioSettings] Failed to load '{path}': {ex.Message}");
        }

        return new SharedAudioSettings();
    }

    public static void Save(string path, SharedAudioSettings settings)
    {
        if (string.IsNullOrWhiteSpace(path) || settings == null)
            return;

        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, JsonUtility.ToJson(settings, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SharedAudioSettings] Failed to save '{path}': {ex.Message}");
        }
    }

    public static string NormalizeDeviceKey(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return string.Empty;

        string normalized = LeadingIndexRegex.Replace(deviceName.Trim(), string.Empty);
        normalized = WhitespaceRegex.Replace(normalized, " ");
        return normalized.Trim().ToLowerInvariant();
    }

    public static string NormalizeStoredDeviceName(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return string.Empty;

        return WhitespaceRegex.Replace(deviceName.Trim(), " ");
    }

    public static SharedAudioAdvancedSettings CloneAdvancedSettings(SharedAudioAdvancedSettings source)
    {
        if (source == null)
            return new SharedAudioAdvancedSettings();

        return new SharedAudioAdvancedSettings
        {
            betaEnabled = source.betaEnabled,
            backendMode = SharedAudioBackendModes.NormalizeForCurrentPlatform(source.backendMode),
            allowFallback = source.allowFallback,
            inputDeviceName = NormalizeStoredDeviceName(source.inputDeviceName),
            outputDeviceName = NormalizeStoredDeviceName(source.outputDeviceName),
            sampleRate = SharedAudioSampleRateOptions.Normalize(source.sampleRate),
            bufferSize = source.bufferSize,
            unifiedOutputEnabled = source.unifiedOutputEnabled,
            unityRecorderCaptureEnabled = source.unityRecorderCaptureEnabled
        };
    }
}
