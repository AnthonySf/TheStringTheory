# Tone Lab NAM Models

Place `.nam` files in the persistent NAM folder to expose them as Tone Lab amp pedals:

```text
Application.persistentDataPath/ToneLab/NAM/
```

Tone Lab scans this folder recursively. Each `.nam` file becomes a NAM pedal in the Library tab.

Bundled profiles can be described with `metadata.json` in this folder. The `profiles` list maps relative `.nam` paths to display names, short labels, descriptions, creators, licenses, and source URLs. User-added profiles still work without metadata, but they fall back to the file name and a generic description.

NAM DSP execution requires the native `StringTheoryToneHost` DLL with Neural Amp Modeler core support.
