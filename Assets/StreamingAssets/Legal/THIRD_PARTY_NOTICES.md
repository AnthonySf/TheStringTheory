## Third-Party Notices

This project depends on third-party components, some of which are bundled in this public source repo.

If you restore those runtimes locally or ship them in a build, include their upstream licenses with that build.

### AlphaTab

- Upstream: https://github.com/CoderLine/alphaTab
- License: MPL-2.0

### AlphaSkia

- Upstream: https://github.com/CoderLine/alphaSkia
- License: BSD-3-Clause

### ONNX Runtime

- Upstream: https://github.com/microsoft/onnxruntime
- License: MIT

### PortAudio

- Upstream: https://www.portaudio.com/
- License: permissive / MIT-style

### Basic Pitch

- Upstream: https://github.com/spotify/basic-pitch
- License: Apache-2.0

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

### Bundled NAM Profiles

- Sources:
  - https://www.tone3000.com/tones/engl-powerball-ii-5770
  - https://www.tone3000.com/tones/plexi-lore-1863
  - https://www.tone3000.com/tones/two-rock-studio-signature-ox-box-2x12-two-rock-cab-v2-5367
- Attribution file: `Assets/StreamingAssets/Legal/NAM_PROFILES-CC-BY.txt`
- License: CC BY 4.0 as declared on the source pages
- Purpose: default Tone Lab NAM amp pedals for new players

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
