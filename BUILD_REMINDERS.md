# Build Reminders

Check this before every release build.

## Every Build

1. Bump the game version.
   - Update the Unity/player version and `StringTheoryBuildInfo` values through the existing version sync flow.
   - Confirm the version appears correctly in logs and diagnostics.

2. Confirm there are no unintended local changes.
   - Run `git status --short --branch`.
   - Do not commit or publish anything from `Signing/`, local certificates, passwords, `.alf`, `.ulf`, or other secrets.

3. Run a compile sanity check.
   - `dotnet build .\Assembly-CSharp-Editor.csproj -v:minimal --no-restore`

4. Check release-only private assets.
   - If bundled songs or private character art changed, rebuild:
     `powershell -ExecutionPolicy Bypass -File Tools\Mac\create-release-assets-package-windows.ps1`
   - Upload `Temp/private-unity-assets/StringTheoryReleaseAssets.zip` to the private `ci-private-assets` release.
   - Update `RELEASE_ASSETS_SHA256` in `.github/workflows/macos-build.yml`.

5. Check private CI assets.
   - `MidiPlayer.zip` must exist on the private `ci-private-assets` release.
   - `StringTheoryReleaseAssets.zip` must exist on the same release.
   - Hashes in the workflow must match the uploaded files.

6. Check platform-specific build inputs.
   - Windows local/Unity build should include Windows native plugins, LV2, NAM, songs, and private character art.
   - macOS GitHub build should stage native plugins, stem runtime, helper tools, LV2, NAM, songs, and private character art.

7. Confirm signing/notarization inputs for macOS.
   - Private repo Actions secrets must include:
     `MACOS_CERTIFICATE_P12`, `MACOS_CERTIFICATE_PASSWORD`, `APPLE_ID`,
     `APPLE_TEAM_ID`, and `APPLE_APP_SPECIFIC_PASSWORD`.
   - The Developer ID certificate should not be expired.

8. Check diagnostics endpoint configuration.
   - Cloudflare Worker secrets should include user-report and automatic-log Discord webhooks.
   - Do not commit raw webhook URLs.

9. Smoke test after building.
   - Launch game.
   - Verify audio input/output, Tone Lab monitoring, note detection, tuner, song start flow, song tone mapping, and one gameplay song.
   - For macOS, confirm the downloaded app opens without quarantine/damaged-app errors.

## Public Push

Before pushing private changes to the public repository:

1. Confirm no private assets or generated binaries are tracked unintentionally.
2. Run a secret scan over tracked files.
3. Confirm public-safe files only: source, docs, scripts, metadata, and allowed assets.
