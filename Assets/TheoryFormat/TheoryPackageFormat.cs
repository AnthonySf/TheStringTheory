using System;
using System.IO;

public static class TheoryPackageFormat
{
    public const int SchemaVersion = 2;
    public const string Extension = ".theory";
    public const string FormatId = "string-theory-song";
    public const string ManifestEntryName = "manifest.json";
    public const string ArrangementDirectory = "arrangements";
    public const string AudioDirectory = "audio";
    public const string StemDirectory = "stems";
    public const string AssetsDirectory = "assets";
    public const string EditorDirectory = "editor";
    public const string UserDirectory = "user";
    public const string EditorStateEntryName = "editor/editor-state.json";
    public const string ToneLabMappingsEntryName = "user/tone-lab-mappings.json";
    public const string CacheDirectoryName = "TheoryPackageCache";

    public static bool IsPackagePath(string path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildArrangementEntryName(string arrangementId)
    {
        string safeId = SanitizeEntryFileName(arrangementId, "arrangement");
        return $"{ArrangementDirectory}/{safeId}.json";
    }

    public static string BuildAudioEntryName(string fileName)
    {
        return $"{AudioDirectory}/{SanitizeEntryFileName(fileName, "audio")}";
    }

    public static string BuildStemEntryName(string stemId, string extension)
    {
        string safeId = SanitizeEntryFileName(stemId, "stem");
        string safeExtension = string.IsNullOrWhiteSpace(extension) ? ".ogg" : extension.Trim();
        if (!safeExtension.StartsWith(".", StringComparison.Ordinal))
            safeExtension = "." + safeExtension;
        return $"{StemDirectory}/{safeId}{safeExtension.ToLowerInvariant()}";
    }

    public static string BuildAssetEntryName(string fileName)
    {
        return $"{AssetsDirectory}/{SanitizeEntryFileName(fileName, "asset")}";
    }

    public static string SanitizeEntryFileName(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        string fileName = Path.GetFileName(value.Trim());
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = fallback;

        char[] invalid = Path.GetInvalidFileNameChars();
        char[] chars = fileName.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == '/' || chars[i] == '\\')
                chars[i] = '_';
        }

        string result = new string(chars).Trim('_', '.', ' ');
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }
}
