using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable]
public sealed class SharedAudioSettings
{
    public int version = 4;
    public string inputDeviceName = string.Empty;
    public string outputDeviceName = string.Empty;
    public int monitoringBufferSize = 128;
    public float guitarVolumePercent = 100f;
    public float songVolumePercent = 100f;
    public string detectorResamplerMode = SharedAudioDetectorResamplerModes.Filtered;
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
}
