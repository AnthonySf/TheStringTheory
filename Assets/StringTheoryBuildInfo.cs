using System;
using UnityEngine;

public static class StringTheoryBuildInfo
{
    public const string Version = "1.0.23";
    public const string Channel = "release";
    public const int DiagnosticsSchemaVersion = 2;
    public const string DiagnosticsUploadEndpoint = "https://aged-cloud-17bd.anthonysfeir9.workers.dev";
    public const string DiagnosticsUploadEndpointKind = "raw-zip";

    public static string UnityApplicationVersion => string.IsNullOrWhiteSpace(Application.version)
        ? "(empty)"
        : Application.version;

    public static bool UnityVersionMatchesBuildInfo =>
        string.Equals(UnityApplicationVersion, Version, StringComparison.Ordinal);

    public static string DiagnosticVersionLabel =>
        UnityVersionMatchesBuildInfo
            ? Version
            : $"{Version} (Unity Application.version={UnityApplicationVersion})";
}
