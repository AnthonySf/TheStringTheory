# How To Build

This repo is the public source tree. A fresh clone is missing a few local-only pieces. The steps below are the shortest path to get the project opening, compiling, and building on Windows.

This project currently targets **Unity 6 / 6000.2.9f1**. Opening it in a different Unity version may trigger upgrade prompts or version mismatch issues, so use that exact editor version unless you are intentionally updating the project.

## 1. Install the base tools

Install these first:

- Unity Hub  
  https://unity.com/en/unity-hub
- Unity Editor `6000.2.9f1` through Unity Hub
- .NET 9 SDK  
  https://dotnet.microsoft.com/download/dotnet/9.0
- Visual Studio Community 2022  
  https://visualstudio.microsoft.com/vs/community/

In Visual Studio Installer, enable these workloads:

- `Game development with Unity`
- `.NET desktop development`
- `Desktop development with C++`

## 2. Clone the repo

```powershell
git clone ...
cd GuitarProject
```

## 3. Restore the required files

These steps are required if you want the public repo clone to open and compile cleanly.

### A. Restore the managed DLLs used by Guitar Pro parsing

Run this PowerShell block from the repo root:

```powershell
$depsRoot = Join-Path $PWD ".build-deps"
New-Item -ItemType Directory -Force $depsRoot, "Assets\Plugins\Managed" | Out-Null

$packages = @(
    @{
        Url = "https://www.nuget.org/api/v2/package/AlphaTab/1.6.0-alpha.1444"
        Zip = "AlphaTab.zip"
        Dll = "lib/netstandard2.0/AlphaTab.dll"
    },
    @{
        Url = "https://www.nuget.org/api/v2/package/AlphaSkia/3.3.135"
        Zip = "AlphaSkia.zip"
        Dll = "lib/netstandard2.0/AlphaSkia.dll"
    },
    @{
        Url = "https://www.nuget.org/api/v2/package/System.Drawing.Common/9.0.5"
        Zip = "System.Drawing.Common.zip"
        Dll = "lib/netstandard2.0/System.Drawing.Common.dll"
    },
    @{
        Url = "https://www.nuget.org/api/v2/package/Microsoft.Win32.SystemEvents/9.0.5"
        Zip = "Microsoft.Win32.SystemEvents.zip"
        Dll = "lib/netstandard2.0/Microsoft.Win32.SystemEvents.dll"
    }
)

foreach ($pkg in $packages) {
    $zipPath = Join-Path $depsRoot $pkg.Zip
    $extractPath = Join-Path $depsRoot ([System.IO.Path]::GetFileNameWithoutExtension($pkg.Zip))

    Invoke-WebRequest $pkg.Url -OutFile $zipPath
    Remove-Item $extractPath -Recurse -Force -ErrorAction SilentlyContinue
    Expand-Archive $zipPath $extractPath -Force
    Copy-Item (Join-Path $extractPath $pkg.Dll) "Assets\Plugins\Managed\" -Force
}
```

After this step, these files should exist:

- `Assets/Plugins/Managed/AlphaTab.dll`
- `Assets/Plugins/Managed/AlphaSkia.dll`
- `Assets/Plugins/Managed/System.Drawing.Common.dll`
- `Assets/Plugins/Managed/Microsoft.Win32.SystemEvents.dll`

### B. Restore the MIDI package used by generated playback

This project uses the Unity Asset Store package `Maestro - Midi Player Tool Kit - Free`.

Asset Store page:

- https://assetstore.unity.com/packages/tools/audio/midi-tool-kit-free-107994

Install it like this:

1. Open Unity Hub.
2. Add this repo folder as a project.
3. Open the project with Unity `6000.2.9f1`.
4. If Unity shows compile errors on first open, ignore them for now.
5. In Unity, open `Window -> Package Manager`.
6. Switch the package source to `My Assets`.
7. Find `Maestro - Midi Player Tool Kit - Free`.
8. Download it, then import it into this project.

After import, this folder should exist:

- `Assets/MidiPlayer/`

Private CI restores this package from a private release asset. Public source builders must install it from the Asset Store because the package contents are not redistributed in this repo.

## 4. Open the project in Unity

Once the DLLs and MIDI package are in place:

1. Open Unity Hub.
2. Open this folder with Unity `6000.2.9f1`.
3. Let Unity finish importing.

## 5. Verify the C# side

From the repo root:

```powershell
dotnet build Assembly-CSharp.csproj -nologo
```

If that passes, the main C# project is in good shape.

## 6. Build the player

From Unity:

1. Open `File -> Build Profiles`.
2. Select the target platform.
3. Check the output path.
4. Press `Build` or `Build And Run`.

## 7. Optional runtime features

These are not required to open or build the project, but they are still supported game features.

### A. Live note detection

If you want live note detection to work, add these runtime files:

- `Assets/Plugins/x86_64/NativeNotesDetectorBridgeNative_v6.dll`
- `Assets/Plugins/x86_64/onnxruntime.dll`
- `Assets/Plugins/x86_64/onnxruntime_providers_shared.dll`
- `Assets/Plugins/x86_64/libportaudio64bit-asio.dll`
- `Assets/StreamingAssets/NotesReader/Models/basic_pitch_nmp.onnx`

Recommended setup:

1. Clone the detector resampler source used by the native bridge:

   ```powershell
   git clone https://github.com/libsndfile/libsamplerate External/libsamplerate
   ```

2. Build the native bridge:

   ```powershell
   msbuild NativeNotesDetectorBridge\NativeNotesDetectorBridge.vcxproj /p:Configuration=Release /p:Platform=x64
   Copy-Item NativeNotesDetectorBridge\build\Release\NativeNotesDetectorBridgeNative_v6.dll Assets\Plugins\x86_64\ -Force
   ```

   The filtered detector resampler is compiled directly into `NativeNotesDetectorBridgeNative_v6.dll`, so there is no extra resampler DLL to ship with the final build.

3. Download ONNX Runtime `1.19.2` for Windows x64 from the official release page:

   - https://github.com/microsoft/onnxruntime/releases/tag/v1.19.2

   Copy these files into `Assets/Plugins/x86_64/`:

   - `onnxruntime.dll`
   - `onnxruntime_providers_shared.dll`

4. Install `basic-pitch` and copy the ONNX model:

   ```powershell
   py -3.9 -m pip install basic-pitch==0.4.0
   py -3.9 -c "from pathlib import Path; from shutil import copyfile; from basic_pitch import ICASSP_2022_MODEL_PATH; dst = Path(r'Assets/StreamingAssets/NotesReader/Models/basic_pitch_nmp.onnx'); dst.parent.mkdir(parents=True, exist_ok=True); copyfile(ICASSP_2022_MODEL_PATH, dst); print(dst)"
   ```

5. Place `libportaudio64bit-asio.dll` into `Assets/Plugins/x86_64/`.

### B. PSARC import

If you want the library to import `.psarc` files on refresh, place this executable here:

- `Assets/StreamingAssets/RocksmithImport/RocksmithImportTool.exe`

To build it locally:

```powershell
git clone https://github.com/iminashi/Rocksmith2014.NET External/Rocksmith2014.NET
dotnet publish External\RocksmithImportTool\RocksmithImportTool.csproj -c Release -r win-x64 --self-contained true -o Assets\StreamingAssets\RocksmithImport
```

### C. AlphaTab tabs rendering

If you want the `Tabs (AlphaTab)` and `3D + Tabs` render modes to work, publish the Windows helper into `StreamingAssets`:

```powershell
dotnet publish Tools\AlphaTabRenderHelper\AlphaTabRenderHelper.csproj -c Release -r win-x64 --self-contained true -o Assets\StreamingAssets\AlphaTabRenderHelper
```

After publish, this file should exist:

- `Assets/StreamingAssets/AlphaTabRenderHelper/AlphaTabRenderHelper.exe`

The helper renders Guitar Pro files to tab images plus timing metadata. It is isolated from the main Unity renderers on purpose, so those modes fall back cleanly when the helper or GP source is unavailable.

### D. Song content

The public repo does not ship the local song library.

If you want songs available in the game, place them under:

- `Assets/StreamingAssets/Songs/`
