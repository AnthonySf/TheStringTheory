$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$msbuild = Join-Path ${env:ProgramFiles} "Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
if (!(Test-Path $msbuild)) {
    $msbuild = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
}

if (!(Test-Path $msbuild)) {
    throw "MSBuild.exe was not found. Install Visual Studio 2022 with the Desktop development with C++ workload."
}

$project = Join-Path $PSScriptRoot "StringTheoryToneHost.vcxproj"
& $msbuild $project /m /p:Configuration=Release /p:Platform=x64
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with exit code $LASTEXITCODE."
}

$dll = Join-Path $repoRoot "Assets\Plugins\x86_64\StringTheoryToneHost.dll"
if (!(Test-Path $dll)) {
    throw "Build completed but $dll was not produced."
}

Write-Host "Built $dll"
