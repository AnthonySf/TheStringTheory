param(
    [string]$OutputPath = "Temp\private-unity-assets\StringTheoryReleaseAssets.zip"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Get-Location).Path
$resolvedOutputPath = Join-Path $repoRoot $OutputPath
$outputDirectory = Split-Path $resolvedOutputPath -Parent
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

if (Test-Path $resolvedOutputPath) {
    Remove-Item -LiteralPath $resolvedOutputPath -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$files = New-Object System.Collections.Generic.List[string]

$songRoot = "Assets\StreamingAssets\Songs"
if (Test-Path $songRoot) {
    Get-ChildItem -Path $songRoot -Recurse -File -Force | ForEach-Object {
        $files.Add($_.FullName)
    }
}

$songRootMeta = "$songRoot.meta"
if (Test-Path $songRootMeta) {
    $files.Add((Resolve-Path $songRootMeta).Path)
}

$resourceFiles = @(
    "Assets\Resources\Hero.png",
    "Assets\Resources\Hero.png.meta",
    "Assets\Resources\HeroOld.png",
    "Assets\Resources\HeroOld.png.meta",
    "Assets\Resources\Skullhead_Monochrome.png",
    "Assets\Resources\Skullhead_Monochrome.png.meta",
    "Assets\Resources\char2.png",
    "Assets\Resources\char2.png.meta",
    "Assets\Resources\char3.png",
    "Assets\Resources\char3.png.meta",
    "Assets\Resources\charOld.png",
    "Assets\Resources\charOld.png.meta",
    "Assets\Resources\filler2.png",
    "Assets\Resources\filler2.png.meta"
)

foreach ($path in $resourceFiles) {
    if (Test-Path $path) {
        $files.Add((Resolve-Path $path).Path)
    }
}

if ($files.Count -eq 0) {
    throw "No release assets were found to package."
}

$zip = [System.IO.Compression.ZipFile]::Open(
    $resolvedOutputPath,
    [System.IO.Compression.ZipArchiveMode]::Create)

try {
    foreach ($file in ($files | Sort-Object -Unique)) {
        $relativePath = $file.Substring($repoRoot.Length).TrimStart("\", "/") -replace "\\", "/"
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip,
            $file,
            $relativePath,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

$reader = [System.IO.Compression.ZipFile]::OpenRead($resolvedOutputPath)
try {
    $entryCount = $reader.Entries.Count
}
finally {
    $reader.Dispose()
}

$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $resolvedOutputPath).Hash.ToLowerInvariant()

Write-Host "Release asset package created:"
Write-Host "  $resolvedOutputPath"
Write-Host "Entries: $entryCount"
Write-Host "Bytes: $((Get-Item $resolvedOutputPath).Length)"
Write-Host "SHA256: $hash"
