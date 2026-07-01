using System;
using System.Collections.Generic;

[Serializable]
public sealed class SongImporterManifest
{
    public string id;
    public string displayName;
    public string version;
    public int apiVersion = 1;
    public bool enabled = true;
    public int priority;
    public List<string> extensions = new List<string>();
    public List<SongImporterFolderSignature> folderSignatures = new List<SongImporterFolderSignature>();
    public List<SongImporterEntrypoint> entrypoints = new List<SongImporterEntrypoint>();
}

[Serializable]
public sealed class SongImporterFolderSignature
{
    public string id;
    public string displayName;
    public bool recursive;
    public List<string> requiredFiles = new List<string>();
    public List<string> anyFiles = new List<string>();
    public List<string> requiredFilePatterns = new List<string>();
    public List<string> anyFilePatterns = new List<string>();
}

[Serializable]
public sealed class SongImporterEntrypoint
{
    public string runtimeIdentifier;
    public string path;
    public string arguments;
}

public sealed class SongImporterDescriptor
{
    public string Id;
    public string DisplayName;
    public string Version;
    public int ApiVersion;
    public int Priority;
    public string DirectoryPath;
    public string ManifestPath;
    public List<string> Extensions = new List<string>();
    public List<SongImporterFolderSignature> FolderSignatures = new List<SongImporterFolderSignature>();
    public List<SongImporterEntrypoint> Entrypoints = new List<SongImporterEntrypoint>();
}

public sealed class SongImporterFolderMatch
{
    public SongImporterDescriptor importer;
    public SongImporterFolderSignature signature;
    public string folderPath;
}

public sealed class SongImporterConversionRequest
{
    public string importerId;
    public string sourcePath;
    public string outputDirectory;
    public string outputPackagePath;
    public bool overwriteExisting;
    public bool validatePackage = true;
    public bool requireAudio = true;
    public bool useLibrarySongsDirectory;
    public bool rejectChartEditorOutputDirectory;
}

public sealed class SongImporterConversionResult
{
    public SongImporterDescriptor importer;
    public string sourcePath;
    public string packagePath;
    public bool packageWasWritten;
    public List<string> warnings = new List<string>();
}
