param(
    [string]$OutputDir = "Signing",
    [string]$CommonName = "String Theory Developer ID",
    [string]$EmailAddress = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$resolvedOutputDir = Join-Path (Get-Location) $OutputDir
New-Item -ItemType Directory -Force -Path $resolvedOutputDir | Out-Null

$safeName = ($CommonName -replace '[^a-zA-Z0-9._-]+', '-').Trim('-')
if ([string]::IsNullOrWhiteSpace($safeName)) {
    $safeName = "StringTheoryDeveloperID"
}

$infPath = Join-Path $resolvedOutputDir "$safeName.inf"
$csrPath = Join-Path $resolvedOutputDir "$safeName.csr"

if ((Test-Path $csrPath) -and -not $Force) {
    throw "CSR already exists: $csrPath. Use -Force to overwrite it."
}

$subjectParts = @("CN=$CommonName")
if (-not [string]::IsNullOrWhiteSpace($EmailAddress)) {
    $subjectParts += "E=$EmailAddress"
}

$subject = $subjectParts -join ", "
$inf = @"
[Version]
Signature="`$Windows NT`$"

[NewRequest]
Subject = "$subject"
KeySpec = 1
KeyLength = 2048
Exportable = TRUE
MachineKeySet = FALSE
ProviderName = "Microsoft Enhanced RSA and AES Cryptographic Provider"
KeyAlgorithm = RSA
HashAlgorithm = SHA256
RequestType = PKCS10
KeyUsage = 0xa0
"@

Set-Content -Path $infPath -Value $inf -Encoding ASCII

if (Test-Path $csrPath) {
    Remove-Item -LiteralPath $csrPath -Force
}

& certreq.exe -q -new $infPath $csrPath
if ($LASTEXITCODE -ne 0) {
    throw "certreq.exe failed with exit code $LASTEXITCODE"
}

(Get-Content -Raw -Path $csrPath).
    Replace("BEGIN NEW CERTIFICATE REQUEST", "BEGIN CERTIFICATE REQUEST").
    Replace("END NEW CERTIFICATE REQUEST", "END CERTIFICATE REQUEST") |
    Set-Content -Path $csrPath -Encoding ASCII

Write-Host ""
Write-Host "Developer ID CSR created:"
Write-Host "  $csrPath"
Write-Host ""
Write-Host "Upload this CSR in Apple Developer > Certificates > + > Developer ID Application."
Write-Host "After downloading Apple's .cer file, run:"
Write-Host "  powershell -ExecutionPolicy Bypass -File Tools\Mac\export-developer-id-p12-windows.ps1 -CertificatePath <downloaded.cer> -P12Password <password>"
