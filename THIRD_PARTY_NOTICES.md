## Third-Party Notices

This project bundles or depends on the following third-party components for the native notes detector path.

### aubio

- Upstream: https://github.com/aubio/aubio
- License family: GPL-3.0-or-later
- Vendored source location: [External/aubio](External/aubio)
- Repo license text: [licenses/AUBIO-GPL-3.0.txt](licenses/AUBIO-GPL-3.0.txt)
- Runtime copy location: `Application.persistentDataPath/Licenses/AUBIO-GPL-3.0.txt`

The native notes detector is currently built against vendored aubio source. The detector-side corresponding source is included in this repository under:

- [NativeNotesDetectorBridge](NativeNotesDetectorBridge)
- [External/aubio](External/aubio)

### ONNX Runtime

- Upstream: https://github.com/microsoft/onnxruntime
- License family: MIT
- Repo license text: [licenses/ONNXRUNTIME-MIT.txt](licenses/ONNXRUNTIME-MIT.txt)
- Runtime copy location: `Application.persistentDataPath/Licenses/ONNXRUNTIME-MIT.txt`
- Bundled runtime DLLs:
  - [Assets/Plugins/x86_64/onnxruntime.dll](Assets/Plugins/x86_64/onnxruntime.dll)
  - [Assets/Plugins/x86_64/onnxruntime_providers_shared.dll](Assets/Plugins/x86_64/onnxruntime_providers_shared.dll)

### PortAudio

- Upstream: https://www.portaudio.com/
- License family: permissive / MIT-style
- Repo license text: [licenses/PORTAUDIO-LICENSE.txt](licenses/PORTAUDIO-LICENSE.txt)
- Runtime copy location: `Application.persistentDataPath/Licenses/PORTAUDIO-LICENSE.txt`
- Bundled runtime DLL:
  - [Assets/Plugins/x86_64/libportaudio64bit-asio.dll](Assets/Plugins/x86_64/libportaudio64bit-asio.dll)

### Basic Pitch

- Upstream: https://github.com/spotify/basic-pitch
- License family: Apache-2.0
- Repo license text: [licenses/BASIC_PITCH-APACHE-2.0.txt](licenses/BASIC_PITCH-APACHE-2.0.txt)
- Runtime copy location: `Application.persistentDataPath/Licenses/BASIC_PITCH-APACHE-2.0.txt`
- Bundled model:
  - [Assets/StreamingAssets/NotesReader/Models/basic_pitch_nmp.onnx](Assets/StreamingAssets/NotesReader/Models/basic_pitch_nmp.onnx)

### AlphaTab

- Upstream: https://www.alphatab.net/
- Repository: https://github.com/CoderLine/alphaTab
- License family: MPL-2.0
- Bundled managed runtime DLL:
  - [Assets/Plugins/Managed/AlphaTab.dll](Assets/Plugins/Managed/AlphaTab.dll)

### AlphaSkia

- Upstream: https://github.com/CoderLine/alphaSkia
- License family: BSD-3-Clause
- Bundled managed runtime DLL:
  - [Assets/Plugins/Managed/AlphaSkia.dll](Assets/Plugins/Managed/AlphaSkia.dll)

## Notes

- The game bootstrap syncs the packaged legal files into `Application.persistentDataPath/Licenses` on startup so shipped builds expose the same notices outside the repo.
- This file is an engineering notice file, not legal advice.
- If you add Steamworks SDK integration later, re-review license compatibility before release.
