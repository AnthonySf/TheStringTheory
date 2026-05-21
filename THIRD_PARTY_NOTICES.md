## Third-Party Notices

This public repo is source-first. It does not include the third-party runtimes, DLLs, models, or packaged tools needed for every feature.

If you restore those pieces locally or ship them in a release, keep their upstream licenses with that build.

### AlphaTab

- Upstream: https://github.com/CoderLine/alphaTab
- License: MPL-2.0

### AlphaSkia

- Upstream: https://github.com/CoderLine/alphaSkia
- License: BSD-3-Clause

### ONNX Runtime

- Upstream: https://github.com/microsoft/onnxruntime
- License: MIT
- Local license text: [licenses/ONNXRUNTIME-MIT.txt](licenses/ONNXRUNTIME-MIT.txt)

### PortAudio

- Upstream: https://www.portaudio.com/
- License: permissive / MIT-style
- Local license text: [licenses/PORTAUDIO-LICENSE.txt](licenses/PORTAUDIO-LICENSE.txt)

### Basic Pitch

- Upstream: https://github.com/spotify/basic-pitch
- License: Apache-2.0
- Local license text: [licenses/BASIC_PITCH-APACHE-2.0.txt](licenses/BASIC_PITCH-APACHE-2.0.txt)

### GxPlugins.lv2

- Upstream: https://github.com/brummer10/GxPlugins.lv2
- Bundled package: GxPlugins v1.0 Windows x64
- License: GPL-compatible Guitarix/LV2 plugin licenses as declared in each LV2 bundle metadata

### Dragonfly Reverb

- Upstream: https://github.com/michaelwillis/dragonfly-reverb
- Bundled package: Dragonfly Reverb 3.2.10 Windows x64 LV2 bundles
- License: GPL-3.0 as declared in the LV2 bundle metadata

### Zam Plugins

- Upstream: https://github.com/zamaudio/zam-plugins
- Bundled package: Zam Plugins 4.5 Windows x64 LV2 bundles
- License: GPL-2.0-or-later as declared in the LV2 bundle metadata

### DPF Plugins

- Upstream: https://github.com/DISTRHO/DPF-Plugins
- Bundled package: DPF Plugins 1.7 Windows x64 LV2 audio-effect subset
- License: per-plugin licenses as declared in the LV2 bundle metadata

### LV2 / lilv

- LV2 upstream: https://gitlab.com/lv2/lv2
- lilv upstream: https://gitlab.com/lv2/lilv
- serd upstream: https://gitlab.com/drobilla/serd
- sord upstream: https://gitlab.com/drobilla/sord
- sratom upstream: https://gitlab.com/lv2/sratom
- zix upstream: https://gitlab.com/drobilla/zix
- License: ISC / 0BSD-style LV2 stack licenses as declared upstream
- Purpose: native LV2 host runtime for Tone Lab external pedals

### NeuralAmpModelerCore

- Upstream: https://github.com/sdatkinson/NeuralAmpModelerCore
- License: MIT
- Purpose: native NAM model loading and DSP for Tone Lab NAM pedals

### Eigen

- Upstream: https://gitlab.com/libeigen/eigen
- License: MPL-2.0
- Purpose: NeuralAmpModelerCore math dependency

### nlohmann/json

- Upstream: https://github.com/nlohmann/json
- License: MIT
- Purpose: NeuralAmpModelerCore `.nam` JSON parsing dependency

### AudioDSPTools

- Upstream: https://github.com/sdatkinson/AudioDSPTools
- License: MIT
- Purpose: NeuralAmpModelerCore dependency source vendored with the NAM core tree

Notes:

- aubio is not bundled in this public repo
- this file is an engineering notice file, not legal advice
