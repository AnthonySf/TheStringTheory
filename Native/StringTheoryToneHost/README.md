# StringTheoryToneHost

Native audio bridge for Tone Lab external pedals.

Unity calls the exported C ABI in `StringTheoryToneHost.h`. The managed side already handles
discovery, preset serialization, UI, and safe bypass behavior when the DLL is absent.

Current implementation:

- loads `.nam` models with NeuralAmpModelerCore
- hosts LV2 plugins through lilv, with LV2/serd/sord/sratom/zix compiled into this DLL
- supports LV2 audio/control ports, Atom event ports, host options, URID map/unmap, logging, bounded block size, and worker scheduling
- process interleaved `float` audio in place
- exposes NAM parameters by ID: `input_trim_db`, `output_trim_db`, `mix`
- exposes LV2 input control ports by their LV2 symbols

The process callback avoids file IO and dynamic allocation. NAM and LV2 instances use
preallocated buffers sized from `max_block_frames`; model/plugin loading happens at
instance creation. The managed C# LV2 direct host remains as a fallback if this DLL is
missing or cannot instantiate a plugin.

Build:

```powershell
.\Native\StringTheoryToneHost\build-win64.ps1
```

Expected deployment path:

```text
Assets/Plugins/x86_64/StringTheoryToneHost.dll
```

Runtime content paths:

```text
Application.persistentDataPath/ToneLab/LV2/
Application.persistentDataPath/ToneLab/NAM/
```
