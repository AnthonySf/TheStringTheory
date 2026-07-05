# macOS Build Notes

This repo now has macOS-aware managed code paths and native CMake scaffolding, but the
final dylibs must be produced on macOS.

## Unity setup

1. Install the Unity macOS standalone build support module for the Unity editor version used by the project.
2. Create a macOS build profile in Unity.
3. Confirm `ProjectSettings/ProjectSettings.asset` keeps a non-empty microphone usage description.
4. Build Intel + Apple Silicon as either a universal build or separate depots.

## Build without owning a Mac

Use the manual GitHub Actions workflow `.github/workflows/macos-build.yml`.

1. Push the repo to GitHub.
2. Open GitHub Actions -> `macOS build` -> `Run workflow`.
3. Leave `build_unity_player` off to build only the native `.dylib` files.
4. Turn `build_unity_player` on after adding Unity license secrets:
   - `UNITY_LICENSE`
   - `UNITY_EMAIL`
   - `UNITY_PASSWORD`

The native-plugin job uses a GitHub-hosted macOS runner and uploads
`macos-native-plugins.tgz`. The Unity-player job downloads those plugins and uploads a
`StringTheory-macOS` build artifact. The Unity-player job uses
`StringTheoryBuildAutomation.BuildMacOS` and sets the macOS player architecture to
`x64ARM64`. It also prepares CI-only streaming assets that are intentionally ignored by
Git: the Basic Pitch ONNX model and both `osx-arm64` and `osx-x64` AlphaTab helper
publishes.

## Native audio and detector

Build all required macOS native plugins on a Mac:

```bash
bash Tools/Mac/build-macos-native-plugins.sh
```

This builds and copies:

- `libNativeNotesDetectorBridgeNative_v6.dylib`
- `libStringTheoryToneHost.dylib`
- `libportaudio.dylib` built with CoreAudio support
- `libonnxruntime.dylib`
- `libonnxruntime_providers_shared.dylib` if required by the ONNX Runtime package used

The script defaults to a universal `x86_64;arm64` build. For Apple Silicon only:

```bash
MACOS_ARCHS=arm64 bash Tools/Mac/build-macos-native-plugins.sh
```

The managed detector still imports `NativeNotesDetectorBridgeNative_v6`; on macOS Unity
should resolve that to `libNativeNotesDetectorBridgeNative_v6.dylib`.

## Tone Lab native host

If you need to build only the native Tone Lab host on a Mac:

```bash
bash Native/StringTheoryToneHost/build-macos.sh
```

This copies `libStringTheoryToneHost.dylib` into `Assets/Plugins/macOS`.

LV2 bundles must be macOS-native bundles. Windows `.dll` LV2 bundles are skipped by the
macOS scanner. Put macOS LV2 bundles under `Assets/StreamingAssets/ToneLab/LV2`, preferably
under a platform folder such as `macos-universal`.

## Helper tools

Publish helper tools on macOS before building the Unity player:

```bash
dotnet publish Tools/AlphaTabRenderHelper/AlphaTabRenderHelper.csproj -c Release -r osx-arm64 --self-contained true
dotnet publish Tools/AlphaTabRenderHelper/AlphaTabRenderHelper.csproj -c Release -r osx-x64 --self-contained true
```

For Intel builds, use `osx-x64`. For a universal Steam depot, publish both architectures
or build universal app/tool wrappers. The game looks for architecture-specific AlphaTab
helpers under `Assets/StreamingAssets/AlphaTabRenderHelper/osx-arm64` and
`Assets/StreamingAssets/AlphaTabRenderHelper/osx-x64`.

Importer add-ons are separate downloads that are not part of this repository;
their macOS support depends on each add-on shipping macOS-native binaries.

## Stem separation

The online stem runtime installer is still Windows-only. For macOS, provide a packaged
runtime zip named `stem-separator-runtime-macos-universal.zip` with:

- `StemSeparator`
- `python/bin/python3`
- any required Python packages and native dependencies

The app will mark packaged command files executable on first use.

## Manual validation required

These must be checked on a real Mac:

- Unity build starts and requests microphone permission.
- Tone Lab detects CoreAudio devices.
- Live monitoring produces output through CoreAudio.
- Native note detector lists CoreAudio input devices and accepts notes.
- `libStringTheoryToneHost.dylib` loads and NAM/LV2 effects process audio.
- Steam launch options point at the `.app` bundle and the macOS depot contains executable files with execute bits.
