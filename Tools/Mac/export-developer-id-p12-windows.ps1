param(
    [Parameter(Mandatory = $true)]
    [string]$CertificatePath,

    [Parameter(Mandatory = $true)]
    [string]$P12Password,

    [string]$OutputDir = "Signing",
    [string]$CommonName = "String Theory Developer ID"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $CertificatePath)) {
    throw "Certificate file not found: $CertificatePath"
}

$resolvedOutputDir = Join-Path (Get-Location) $OutputDir
New-Item -ItemType Directory -Force -Path $resolvedOutputDir | Out-Null

function Import-AppleCertificateChain {
    param([string]$TargetDir)

    $chainDir = Join-Path $TargetDir "AppleChain"
    New-Item -ItemType Directory -Force -Path $chainDir | Out-Null

    $rootPath = Join-Path $chainDir "AppleIncRootCertificate.cer"
    $developerIdPath = Join-Path $chainDir "DeveloperIDCA.cer"

    if (-not (Test-Path $rootPath)) {
        Invoke-WebRequest `
            -Uri "https://www.apple.com/appleca/AppleIncRootCertificate.cer" `
            -OutFile $rootPath
    }

    if (-not (Test-Path $developerIdPath)) {
        Invoke-WebRequest `
            -Uri "https://www.apple.com/certificateauthority/DeveloperIDCA.cer" `
            -OutFile $developerIdPath
    }

    Import-Certificate -FilePath $rootPath -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
    Import-Certificate -FilePath $developerIdPath -CertStoreLocation Cert:\CurrentUser\CA | Out-Null
}

Import-AppleCertificateChain -TargetDir $resolvedOutputDir

Write-Host "Accepting Apple certificate and binding it to the matching private key..."

$downloadedCert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($CertificatePath)
$thumbprint = $downloadedCert.Thumbprint
$serialNumber = $downloadedCert.SerialNumber

& certreq.exe -accept $CertificatePath
if ($LASTEXITCODE -ne 0) {
    Write-Warning "certreq.exe -accept failed with exit code $LASTEXITCODE. Falling back to addstore/repairstore."

    & certutil.exe -user -addstore My $CertificatePath
    if ($LASTEXITCODE -ne 0) {
        throw "certutil.exe -addstore failed with exit code $LASTEXITCODE"
    }

    & certutil.exe -user -repairstore My $serialNumber
    if ($LASTEXITCODE -ne 0) {
        throw "certutil.exe -repairstore failed with exit code $LASTEXITCODE"
    }
}

$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Thumbprint -eq $thumbprint -and
        $_.HasPrivateKey
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($null -eq $cert) {
    $cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -like "*CN=$CommonName*" -and
        $_.HasPrivateKey -and
        $_.EnhancedKeyUsageList.FriendlyName -contains "Code Signing"
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1
}

if ($null -eq $cert) {
    $cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object {
            $_.Subject -like "*CN=$CommonName*" -and
            $_.HasPrivateKey
        } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
}

if ($null -eq $cert) {
    throw "No installed certificate with a private key was found for CN=$CommonName."
}

$safeName = ($CommonName -replace '[^a-zA-Z0-9._-]+', '-').Trim('-')
if ([string]::IsNullOrWhiteSpace($safeName)) {
    $safeName = "StringTheoryDeveloperID"
}

$p12Path = Join-Path $resolvedOutputDir "$safeName.p12"
$base64Path = "$p12Path.base64"
$securePassword = ConvertTo-SecureString -String $P12Password -Force -AsPlainText

Export-PfxCertificate `
    -Cert $cert `
    -FilePath $p12Path `
    -Password $securePassword `
    -ChainOption BuildChain | Out-Null

[Convert]::ToBase64String([IO.File]::ReadAllBytes($p12Path)) |
    Set-Content -Path $base64Path -Encoding ASCII

Write-Host ""
Write-Host "Developer ID .p12 created:"
Write-Host "  $p12Path"
Write-Host ""
Write-Host "GitHub secret values:"
Write-Host "  MACOS_CERTIFICATE_P12      = contents of $base64Path"
Write-Host "  MACOS_CERTIFICATE_PASSWORD = the P12Password you provided"
Write-Host ""
Write-Host "Do not commit or share the .p12, .base64, or password."
