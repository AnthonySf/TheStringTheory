using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

public static class TheoryPackageCache
{
    public static bool TryCachePrimaryAudio(string packagePath, TheorySongManifest manifest, out string cachedPath, out string error)
    {
        cachedPath = string.Empty;
        error = string.Empty;
        TheoryAudioAsset asset = manifest?.audio?.FirstOrDefault(item => item != null && item.defaultForPlayback) ??
                                 manifest?.audio?.FirstOrDefault(item => item != null);
        string entry = !string.IsNullOrWhiteSpace(manifest?.primaryAudioEntry) ? manifest.primaryAudioEntry : asset?.entry;
        if (string.IsNullOrWhiteSpace(entry))
        {
            error = "Theory package has no primary audio entry.";
            return false;
        }

        return TryCacheEntry(packagePath, entry, out cachedPath, out error);
    }

    public static bool TryCacheCoverArt(string packagePath, TheorySongManifest manifest, out string cachedPath, out string error)
    {
        cachedPath = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(manifest?.coverArtEntry))
            return false;

        return TryCacheEntry(packagePath, manifest.coverArtEntry, out cachedPath, out error);
    }

    public static bool TryCacheEmbeddedStems(
        string packagePath,
        TheorySongManifest manifest,
        string sourceAudioPath,
        out StemCacheManifest stemManifest,
        out string error)
    {
        stemManifest = null;
        error = string.Empty;
        if (manifest?.stems == null || manifest.stems.Count == 0)
        {
            error = "Theory package has no embedded stems.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(sourceAudioPath) || !File.Exists(sourceAudioPath))
        {
            error = "Source audio was not found for the embedded stems.";
            return false;
        }

        List<TheoryStemAsset> packageStems = manifest.stems
            .Where(stem => stem != null &&
                           !string.IsNullOrWhiteSpace(stem.id) &&
                           !string.IsNullOrWhiteSpace(stem.entry))
            .OrderBy(stem => stem.id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (packageStems.Count == 0)
        {
            error = "Theory package stem manifest contains no usable stems.";
            return false;
        }

        long packageGeneratedAtUtcTicks = packageStems.Max(stem => stem.generatedAtUtcTicks);
        if (StemSeparationService.TryLoadValidManifest(sourceAudioPath, out StemCacheManifest existingManifest) &&
            IsMatchingEmbeddedStemCache(existingManifest, packageStems, packageGeneratedAtUtcTicks))
        {
            stemManifest = existingManifest;
            return true;
        }

        string cacheDirectory = StemSeparationService.GetCacheDirectory(sourceAudioPath);
        string tempDirectory = cacheDirectory + ".theory";
        try
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, true);
            Directory.CreateDirectory(tempDirectory);
            string stemsDirectory = Path.Combine(tempDirectory, TheoryPackageFormat.StemDirectory);
            Directory.CreateDirectory(stemsDirectory);

            List<StemCacheEntry> cachedStems = new List<StemCacheEntry>();
            for (int i = 0; i < packageStems.Count; i++)
            {
                TheoryStemAsset stem = packageStems[i];
                string extension = Path.GetExtension(stem.entry);
                string fileName = TheoryPackageFormat.SanitizeEntryFileName(stem.id, "stem") +
                                  (string.IsNullOrWhiteSpace(extension) ? ".ogg" : extension.ToLowerInvariant());
                string relativePath = Path.Combine(TheoryPackageFormat.StemDirectory, fileName).Replace('\\', '/');
                string destinationPath = Path.Combine(tempDirectory, relativePath);
                if (!TheoryPackageIO.TryExtractEntry(packagePath, stem.entry, destinationPath, out string extractError))
                {
                    error = extractError;
                    return false;
                }

                cachedStems.Add(new StemCacheEntry
                {
                    id = stem.id,
                    displayName = string.IsNullOrWhiteSpace(stem.displayName) ? StemSeparationService.FormatStemDisplayName(stem.id) : stem.displayName,
                    relativePath = relativePath
                });
            }

            FileInfo sourceInfo = new FileInfo(sourceAudioPath);
            StemCacheManifest cacheManifest = new StemCacheManifest
            {
                schemaVersion = 1,
                sourceAudioPath = Path.GetFullPath(sourceAudioPath),
                sourceAudioLastWriteUtcTicks = sourceInfo.LastWriteTimeUtc.Ticks,
                sourceAudioSizeBytes = sourceInfo.Length,
                provider = "theory-package",
                model = packageStems.FirstOrDefault(stem => !string.IsNullOrWhiteSpace(stem.model))?.model ?? string.Empty,
                generatedAtUtcTicks = packageGeneratedAtUtcTicks > 0 ? packageGeneratedAtUtcTicks : DateTime.UtcNow.Ticks,
                status = "ready",
                error = string.Empty,
                stems = cachedStems
                    .OrderBy(stem => stem.id, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            File.WriteAllText(Path.Combine(tempDirectory, StemSeparationService.ManifestFileName), UnityEngine.JsonUtility.ToJson(cacheManifest, true));

            if (Directory.Exists(cacheDirectory))
                Directory.Delete(cacheDirectory, true);
            Directory.Move(tempDirectory, cacheDirectory);
            stemManifest = cacheManifest;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                    Directory.Delete(tempDirectory, true);
            }
            catch
            {
            }
        }
    }

    private static bool IsMatchingEmbeddedStemCache(
        StemCacheManifest existingManifest,
        List<TheoryStemAsset> packageStems,
        long packageGeneratedAtUtcTicks)
    {
        if (existingManifest == null ||
            !string.Equals(existingManifest.provider, "theory-package", StringComparison.OrdinalIgnoreCase) ||
            existingManifest.stems == null ||
            packageStems == null ||
            existingManifest.stems.Count != packageStems.Count)
        {
            return false;
        }

        if (packageGeneratedAtUtcTicks > 0 && existingManifest.generatedAtUtcTicks != packageGeneratedAtUtcTicks)
            return false;

        List<string> existingIds = existingManifest.stems
            .Where(stem => stem != null && !string.IsNullOrWhiteSpace(stem.id))
            .Select(stem => stem.id.Trim())
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        List<string> packageIds = packageStems
            .Where(stem => stem != null && !string.IsNullOrWhiteSpace(stem.id))
            .Select(stem => stem.id.Trim())
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return existingIds.SequenceEqual(packageIds, StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryCacheEntry(string packagePath, string entryName, out string cachedPath, out string error)
    {
        cachedPath = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            error = "Theory package was not found.";
            return false;
        }

        string cacheDirectory = BuildPackageCacheDirectory(packagePath);
        string fileName = TheoryPackageFormat.SanitizeEntryFileName(Path.GetFileName(entryName), "entry");
        cachedPath = Path.Combine(cacheDirectory, fileName);
        if (File.Exists(cachedPath))
            return true;

        return TheoryPackageIO.TryExtractEntry(packagePath, entryName, cachedPath, out error);
    }

    private static string BuildPackageCacheDirectory(string packagePath)
    {
        FileInfo info = new FileInfo(packagePath);
        string safeName = TheoryPackageFormat.SanitizeEntryFileName(Path.GetFileNameWithoutExtension(packagePath), "song");
        string stamp = info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture);
        string size = info.Length.ToString(CultureInfo.InvariantCulture);
        return Path.Combine(ExternalContentPaths.PersistentRoot, TheoryPackageFormat.CacheDirectoryName, $"{safeName}_{stamp}_{size}");
    }
}
