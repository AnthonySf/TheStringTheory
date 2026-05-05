# How To Build

This repo is the public source tree. It does **not** include the private runtime packages, bundled DLLs, detector binaries, or local song content needed for a full working build.

## 1. Install the prerequisites

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
git clone <your-repo-url> GuitarProject
cd GuitarProject
```

## 3. Restore the missing local-only content

Copy these back into the project before opening Unity:

- `Assets/MidiPlayer/`  
  Maestro / Midi Player Tool Kit package.
- `Assets/Plugins/Managed/*.dll`  
  AlphaTab and other managed runtime DLLs.
- `Assets/Plugins/x86_64/*.dll`  
  Native runtime DLLs such as ONNX Runtime, PortAudio, and the detector bridge dependencies.
- `Assets/StreamingAssets/NotesReader/Models/basic_pitch_nmp.onnx`
- `Assets/StreamingAssets/NotesReader/guitar_ai2_continuous.exe`
- `Assets/StreamingAssets/ToneLab/dist/`
- `External/aubio/`  
  Required if you want to rebuild the native detector bridge.

Optional:

- `Assets/StreamingAssets/RocksmithImport/RocksmithImportTool.exe`  
  Needed only if you want the game to import `.psarc` files directly.
- `External/Rocksmith2014.NET/`  
  Needed only if you want to rebuild the PSARC importer from source.

## 4. Open the project in Unity

1. Open Unity Hub.
2. Add this folder as a project.
3. Open it with Unity Editor `6000.2.9f1`.
4. Let Unity finish the first import.

If everything is in place, the project should open and compile normally.

## 5. Quick verification from the command line

This checks the C# project side before you make a player build:

```powershell
dotnet build Assembly-CSharp.csproj -nologo
```

## 6. Optional: rebuild the native detector bridge

Do this only if you need the native detector DLL and do not already have it.

Project:

- `NativeNotesDetectorBridge/NativeNotesDetectorBridge.vcxproj`

Expected output:

- `Assets/Plugins/x86_64/NativeNotesDetectorBridgeNative_v6.dll`

Build command:

```powershell
msbuild NativeNotesDetectorBridge\NativeNotesDetectorBridge.vcxproj /p:Configuration=Release /p:Platform=x64
```

`External/aubio/` must exist for this build.

If you ship a build that includes the aubio-linked detector, you are responsible for aubio license/source compliance in that distribution.

## 7. Optional: enable PSARC importing

You do **not** need this for the default shipped extracted demo songs.  
You need it only if you want users or developers to drop `.psarc` files into the songs folder and import them.

### Easiest path

Drop a compatible importer executable here:

- `Assets/StreamingAssets/RocksmithImport/RocksmithImportTool.exe`

### Build the importer yourself

First clone the dependency source here:

- `External/Rocksmith2014.NET/`

Then publish the wrapper:

```powershell
dotnet publish External\RocksmithImportTool\RocksmithImportTool.csproj -c Release -r win-x64 --self-contained false -o Assets\StreamingAssets\RocksmithImport
```

## 8. Build the game

From Unity:

1. Open `File -> Build Profiles`.
2. Select the target platform.
3. Make sure the scenes and output folder are correct.
4. Press `Build` or `Build And Run`.

## 9. Where local content lives

- Shipped / startup-copied songs:
  - `Assets/StreamingAssets/Songs/`
- Runtime persistent songs:
  - `%USERPROFILE%\\AppData\\LocalLow\\StringTheory\\StringTheory\\Songs`

The public repo should only ship royalty-free content and user-rebuildable code. Local song folders and private binaries are intentionally not tracked.
