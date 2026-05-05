# How To Build

This public repo does not include the bundled third-party runtimes and packaged binaries.

Missing pieces you need locally:

- `Assets/MidiPlayer/`
  Maestro / Midi Player Tool Kit package.
- `Assets/Plugins/Managed/*.dll`
  AlphaTab and other managed runtime DLLs.
- `Assets/Plugins/x86_64/*.dll`
  detector/runtime DLLs such as ONNX Runtime, PortAudio, and the native detector bridge.
- `Assets/StreamingAssets/NotesReader/Models/basic_pitch_nmp.onnx`
  Basic Pitch model file.
- `Assets/StreamingAssets/NotesReader/guitar_ai2_continuous.exe`
  packaged detector worker.
- `Assets/StreamingAssets/ToneLab/dist/`
  packaged ToneLab runtime.
- `External/aubio/`
  aubio source used by the native detector build.
- `External/Rocksmith2014.NET/`
  local dependency source tree used if you want to build the Rocksmith PSARC importer yourself.

## Unity

Restore the missing folders above, then open the project in Unity.

## Native detector DLL

Build:

- `NativeNotesDetectorBridge/NativeNotesDetectorBridge.vcxproj`

Output expected by Unity:

- `Assets/Plugins/x86_64/NativeNotesDetectorBridgeNative_v6.dll`

The project expects `External/aubio/` to exist when building the detector.

If you ship a build that includes an aubio-linked detector, handle aubio's license and source requirements in that release package. The public repo does not bundle aubio.

## Detector worker

If you use the external detector worker path, place the packaged worker here:

- `Assets/StreamingAssets/NotesReader/guitar_ai2_continuous.exe`

and the model here:

- `Assets/StreamingAssets/NotesReader/Models/basic_pitch_nmp.onnx`

## ToneLab

Source files stay in:

- `Assets/StreamingAssets/ToneLab/`

If you want the packaged runtime in Unity, build it separately and place the output in:

- `Assets/StreamingAssets/ToneLab/dist/`

## Rocksmith PSARC importer

The repo includes our wrapper source here:

- `External/RocksmithImportTool/`

The game looks for the importer executable here:

- `Assets/StreamingAssets/RocksmithImport/RocksmithImportTool.exe`

If that executable is present, the game will detect `.psarc` files in the songs directory and import them on library refresh.

### End-user setup

If you already have a compatible `RocksmithImportTool.exe`, place it here:

- `Assets/StreamingAssets/RocksmithImport/RocksmithImportTool.exe`

No other repo changes are needed for basic PSARC importing.

### Build the importer locally

If you want to build the importer from source, first clone the dependency here:

- `External/Rocksmith2014.NET/`

Then build or publish:

- `External/RocksmithImportTool/RocksmithImportTool.csproj`

Recommended publish target:

- `dotnet publish External\\RocksmithImportTool\\RocksmithImportTool.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o Assets\\StreamingAssets\\RocksmithImport`

## Songs

Songs stay in:

- `Assets/StreamingAssets/Songs/`

Use your own files. The public repo should only ship royalty-free content.
