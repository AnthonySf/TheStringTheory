# Tone Lab LV2 Plugins

Bundled LV2 effects are copied to the persistent Tone Lab plugin folder on first run.

Users can add compatible LV2 bundles by placing `.lv2` folders under:

```text
Application.persistentDataPath/ToneLab/LV2/
```

Current Windows bundled set:

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

Current macOS build-time bundled set:

- DPF Plugins 1.7 macOS universal LV2 bundles
- Release package: `DPF-Plugins-v1.7-macos-universal.dmg`
- Dragonfly Reverb 3.2.10 macOS universal LV2 bundles
- Release package: `dragonfly-reverb-3.2.10-macos-universal.dmg`
- Zam Plugins 4.5 macOS universal LV2 bundles
- Release package: `zam-plugins-4.5-macos-universal.dmg`
- GxPlugins.lv2 v1.0 macOS LV2 bundles
- Source-built from tag `v1.0` because upstream does not publish official macOS binaries

The macOS payload is staged by `Tools/Mac/stage-macos-lv2.sh` during the GitHub
macOS build. That script removes Windows LV2 binaries from the macOS build
workspace before Unity runs, so the macOS player does not ship Windows `.dll`
plugins. The generated `macos-universal` folder is intentionally git-ignored.
GxPlugins are built as DSP-only LV2 bundles for Tone Lab; the upstream plugin
GUI binaries are not built because Tone Lab uses its own pedal UI.

Tone Lab scans bundle metadata and saves presets by LV2 plugin URI, not by bundle folder name.
The preferred runtime path hosts LV2 plugins through the native lilv bridge. The managed
direct LV2 loader remains as a Windows-only fallback for simple audio/control plugins if
the native DLL is unavailable; macOS and Linux builds require the native host library. Tone
Lab exposes audio/control LV2 pedals as board pedals. The native bridge supports
audio/control ports, Atom event ports, host options, URID map/unmap, logging, bounded block
sizes, and worker scheduling; plugins that require unsupported real-time extensions are
rejected instead of being loaded unsafely.
