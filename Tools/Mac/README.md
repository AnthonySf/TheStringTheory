# macOS Developer ID Signing

String Theory can produce either an ad-hoc signed macOS build or a Developer ID signed and notarized build.

The GitHub Actions workflow keeps the old ad-hoc path when signing secrets are missing. When the secrets below are configured, the workflow signs the Unity app with Developer ID, submits it to Apple notarization, staples the ticket, verifies Gatekeeper acceptance, and creates the final zip.

## Create the certificate request on Windows

Run this from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\Mac\create-developer-id-csr-windows.ps1
```

Upload `Signing\String-Theory-Developer-ID.csr` at:

`Apple Developer > Certificates, IDs & Profiles > Certificates > + > Developer ID Application`

Download the generated `.cer` file from Apple.

## Create the GitHub certificate secret

Run this from the repository root, replacing the certificate path and password:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\Mac\export-developer-id-p12-windows.ps1 `
  -CertificatePath C:\Path\To\developerID_application.cer `
  -P12Password "use-a-strong-password"
```

The export script also installs Apple's public Developer ID certificate chain into the current user's Windows certificate stores. If Windows refuses `certreq -accept` because of Apple-specific certificate extensions, the script falls back to `certutil -addstore` plus `certutil -repairstore` and still exports the matching certificate/private key pair.

Add these GitHub Actions secrets:

```text
MACOS_CERTIFICATE_P12          Contents of Signing\String-Theory-Developer-ID.p12.base64
MACOS_CERTIFICATE_PASSWORD     The P12Password used above
APPLE_ID                       Apple ID email used for notarization
APPLE_TEAM_ID                  Apple Developer Team ID
APPLE_APP_SPECIFIC_PASSWORD    App-specific password from account.apple.com
```

Never commit files from `Signing\`.

## Private release assets

The macOS workflow also expects `StringTheoryReleaseAssets.zip` on the private `ci-private-assets` release. This package contains ignored release-only content that should match the local Windows release build, such as bundled songs and private character art.

The zip must preserve repository-relative paths, for example:

```text
Assets/StreamingAssets/Songs/...
Assets/Resources/Hero.png
Assets/Resources/Hero.png.meta
```

After changing bundled songs or private character art, rebuild and re-upload `StringTheoryReleaseAssets.zip`, then update `RELEASE_ASSETS_SHA256` in `.github/workflows/macos-build.yml`.

On Windows, rebuild the package from the repository root with:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\Mac\create-release-assets-package-windows.ps1
```
