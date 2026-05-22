param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+$')]
    [string]$AppId,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+$')]
    [string]$DepotId,

    [Parameter(Mandatory = $true)]
    [string]$SteamUsername,

    [Parameter(Mandatory = $true)]
    [string]$BuildRoot,

    [Parameter(Mandatory = $true)]
    [string]$SdkRoot,

    [string]$Description = "String Theory v1.0.19",

    [switch]$Preview
)

$ErrorActionPreference = "Stop"

function Resolve-RequiredPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label was not found: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function ConvertTo-VdfPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    return $Path.Replace('"', '\"')
}

$buildRootPath = Resolve-RequiredPath -Path $BuildRoot -Label "Build root"
$sdkRootPath = Resolve-RequiredPath -Path $SdkRoot -Label "Steamworks SDK root"
$contentBuilderPath = Resolve-RequiredPath -Path (Join-Path $sdkRootPath "tools\ContentBuilder") -Label "SteamPipe ContentBuilder"
$steamCmdPath = Resolve-RequiredPath -Path (Join-Path $contentBuilderPath "builder\steamcmd.exe") -Label "SteamCMD"
$gameExePath = Resolve-RequiredPath -Path (Join-Path $buildRootPath "StringTheory.exe") -Label "Game executable"

$scriptOutputRoot = Join-Path $contentBuilderPath "scripts\StringTheory"
$buildOutputRoot = Join-Path $contentBuilderPath "output\StringTheory"
New-Item -ItemType Directory -Force -Path $scriptOutputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $buildOutputRoot | Out-Null

$appBuildScriptPath = Join-Path $scriptOutputRoot "app_build_$AppId.vdf"
$previewValue = if ($Preview) { "1" } else { "0" }

$vdf = @"
"AppBuild"
{
    "AppID" "$AppId"
    "Desc" "$Description"
    "Preview" "$previewValue"
    "ContentRoot" "$(ConvertTo-VdfPath $buildRootPath)"
    "BuildOutput" "$(ConvertTo-VdfPath $buildOutputRoot)"

    "Depots"
    {
        "$DepotId"
        {
            "FileMapping"
            {
                "LocalPath" "*"
                "DepotPath" "."
                "Recursive" "1"
            }

            "FileExclusion" "StringTheory_BurstDebugInformation_DoNotShip\*"
            "FileExclusion" "*.pdb"
            "FileExclusion" "*.mdb"
        }
    }
}
"@

Set-Content -LiteralPath $appBuildScriptPath -Value $vdf -Encoding ASCII

Write-Host "SteamPipe app build script written:" -ForegroundColor Cyan
Write-Host "  $appBuildScriptPath"
Write-Host "Build root:" -ForegroundColor Cyan
Write-Host "  $buildRootPath"
Write-Host "Game executable:" -ForegroundColor Cyan
Write-Host "  $gameExePath"
Write-Host ""

if ($Preview) {
    Write-Host "Running SteamPipe preview. Nothing will be uploaded." -ForegroundColor Yellow
} else {
    Write-Host "Uploading build to SteamPipe." -ForegroundColor Yellow
    Write-Host "SteamCMD may prompt for your password and Steam Guard code in this terminal."
}

& $steamCmdPath +login $SteamUsername +run_app_build $appBuildScriptPath +quit
exit $LASTEXITCODE
