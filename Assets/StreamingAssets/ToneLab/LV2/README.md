# Tone Lab LV2 Plugins

Bundled LV2 effects are copied to the persistent Tone Lab plugin folder on first run.

Users can add compatible LV2 bundles by placing `.lv2` folders under:

```text
Application.persistentDataPath/ToneLab/LV2/
```

Current bundled set:

- GxPlugins.lv2 v1.0 Windows x64
- Upstream: https://github.com/brummer10/GxPlugins.lv2
- Release package: `gxplugins_1.0_win64.7z`
- Dragonfly Reverb 3.2.10 Windows x64 LV2 bundles
- Upstream: https://github.com/michaelwillis/dragonfly-reverb
- Release package: `dragonfly-reverb-3.2.10-win64.zip`
- Zam Plugins 4.5 Windows x64 LV2 bundles
- Upstream: https://github.com/zamaudio/zam-plugins
- Release package: `zam-plugins-4.5-win64.zip`
- DPF Plugins 1.7 Windows x64 LV2 audio-effect subset
- Upstream: https://github.com/DISTRHO/DPF-Plugins
- Release package: `DPF-Plugins-v1.7-win64.zip`
- Official LV2 specification bundles used by the lilv host

Tone Lab scans bundle metadata and saves presets by LV2 plugin URI, not by bundle folder name.
The preferred runtime path hosts LV2 plugins through the native lilv bridge. The managed
direct LV2 loader remains as a fallback for simple audio/control plugins if the native DLL
is unavailable. Tone Lab exposes audio/control LV2 pedals as board pedals. The native bridge
supports audio/control ports, Atom event ports, host options, URID map/unmap, logging, bounded
block sizes, and worker scheduling; plugins that require unsupported real-time extensions are
rejected instead of being loaded unsafely.
