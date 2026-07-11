# Importers

Importers can be added to convert different file formats into `.theory`
packages. This document provides an overview of how importers work.

## Table of Contents

- [How it works](#how-it-works)
- [Importer folder structure](#importer-folder-structure)
- [Manifest format (`importer.json`)](#manifest-format-importerjson)
  - [Fields](#fields)
  - [Entrypoints](#entrypoints)
    - [Runtime identifier matching](#runtime-identifier-matching)
    - [Placeholders](#placeholders)
  - [Folder signatures](#folder-signatures)
- [Binary contract](#binary-contract)
  - [Output validation](#output-validation)
- [The `.theory` package format](#the-theory-package-format)
  - [ZIP structure](#zip-structure)
  - [`manifest.json`](#manifestjson)
  - [Arrangement JSON schema](#arrangement-json-schema)
  - [Note data](#note-data)
    - [BendPoint](#bendpoint)
    - [TechniqueSegment](#techniquesegment)
  - [Timing data](#timing-data)
  - [Tone data](#tone-data)
  - [Generated notes (playback events)](#generated-notes-playback-events)
  - [Audio embedding](#audio-embedding)
  - [Provenance](#provenance)
- [Key source files](#key-source-files)

## How it works

Importers are discovered and registered by
[`SongImporterRegistry`](Assets/SongImporters/SongImporterRegistry.cs). When a
library is refreshed, the following occurs:

1.  The game scans the Songs directory for files matching the extensions handled
    by custom importers.
2.  A UI is shown with the discovered candidates for the user to select.
3.  Once the user click on "Convert", the importer registry spawns the add-on
    binary following the command template in the `importer.json` file.
4.  The resulting `.theory` file is validated and metadata is written to the manifest.
5.  The game loads the file into the library.


## Importer folder structure

Each importer is a folder containing an `importer.json` manifest and the
binary it describes:

```
Importers/
  my-importer/
    importer.json
    my-importer.exe          (win-x64)
    my-importer              (osx-x64 / osx-arm64)
    helper.dll               (runtime dependencies, optional)
```

The folder name is arbitrary but should match the add-on id for clarity.

## Manifest format (`importer.json`)

The manifest is a JSON file that deserializes into [`SongImporterManifest`](Assets/SongImporters/SongImporterRegistry.cs).

```json
{
  "id": "my-importer",
  "displayName": "My Importer",
  "version": "1.0.0",
  "apiVersion": 1,
  "enabled": true,
  "priority": 100,
  "extensions": [".myfmt", ".altfmt"],
  "folderSignatures": [
    {
      "id": "my-folder-format",
      "displayName": "My Folder Format",
      "recursive": false,
      "requiredFiles": ["metadata.json"],
      "anyFiles": ["song.abc"],
      "requiredFilePatterns": ["*.xyz"],
      "anyFilePatterns": ["notes/*.txt"]
    }
  ],
  "entrypoints": [
    {
      "runtimeIdentifier": "win-x64",
      "path": "my-importer.exe",
      "arguments": "--source {sourcePath} --output {outputTheoryPath} --work {workDirectory}"
    }
  ],
  "cacheFolderNames": ["__myfmt_cache"]
}
```

### Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `id` | string | Yes | Unique identifier. |
| `displayName` | string | No | Human-readable name. Falls back to `id`. |
| `version` | string | No | Importer version string. |
| `apiVersion` | int | Yes | Must be `1` (the current supported version). |
| `enabled` | bool | No | If `false`, the add-on is skipped. Default `true`. |
| `priority` | int | No | Higher values are preferred when multiple add-ons match the same extension. Default `0`. |
| `extensions` | string[] | No* | File extensions this add-on handles (e.g. `[".psarc"]`). At least one of `extensions` or `folderSignatures` must be provided. |
| `folderSignatures` | [FolderSignature](#folder-signatures)[] | No* | Descriptions of folder layouts this add-on can process. |
| `entrypoints` | [Entrypoint](#entrypoints)[] | Yes | Platform-specific executables. At least one required. |
| `cacheFolderNames` | string[] | No | Folder names (single directory name, no path separators) that the library scanner should skip when scanning for new candidates. Prevents re-scanning of already-imported cache directories. |

*\*At least one of `extensions` or `folderSignatures` is required.*

### Entrypoints

Each entrypoint defines how to run the add-on on a specific platform.

| Field | Type | Required | Description |
|---|---|---|---|
| `runtimeIdentifier` | string | No | Platform RID. Matched against `StringTheoryPlatform.DotNetRuntimeIdentifier`. Falls back to `"any"`, `"*"`, or empty. See [Runtime identifier matching](#runtime-identifier-matching). |
| `path` | string | Yes | Path to the executable, relative to the add-on folder. Supports `{streamingRoot}`, `{persistentRoot}`, `{importerDirectory}` [argument placeholders](#placeholders). |
| `arguments` | string | No | CLI template. If omitted, defaults to `import-theory --source {sourcePath} --output {outputTheoryPath} --work {workDirectory}`. Supports [argument placeholders](#placeholders). |

#### Runtime identifier matching

The game uses `StringTheoryPlatform.DotNetRuntimeIdentifier` (e.g. `win-x64`,
`osx-x64`, `osx-arm64`) to pick the right entrypoint. The match order:

1. Exact match (`"win-x64"`)
2. Fallback to `"any"` or `"*"` or empty `runtimeIdentifier`

#### Placeholders

When constructing the command line, these placeholders are replaced in the
`arguments` template:

| Placeholder | Value |
|---|---|
| `{sourcePath}` | Full path to the input file or directory (quoted) |
| `{outputTheoryPath}` | Full path where the output `.theory` file must be written (quoted) |
| `{workDirectory}` | Full path to a temporary work directory (created fresh, cleaned up after; quoted) |
| `{importerDirectory}` | Full path to the add-on's own folder (quoted) |

### Folder signatures

Folder signatures let an importer declare that it can import a directory
(rather than a single file) based on the files inside.

```json
{
  "id": "my-folder-format",
  "displayName": "My Folder Format",
  "recursive": false,
  "requiredFiles": ["manifest.xml"],
  "anyFiles": ["audio.ogg"],
  "requiredFilePatterns": ["*.notes"],
  "anyFilePatterns": ["parts/*.xml"]
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `id` | string | Yes | Internal identifier for this signature. |
| `displayName` | string | No | Human-readable label shown in the UI. |
| `recursive` | bool | No | Whether file patterns search subdirectories. Default `false`. |
| `requiredFiles` | string[] | No | All of these files must exist (exact relative paths). |
| `anyFiles` | string[] | No | At least one of these files must exist. |
| `requiredFilePatterns` | string[] | No | All of these glob patterns must match at least one file. |
| `anyFilePatterns` | string[] | No | At least one of these glob patterns must match a file. |

A signature is considered usable if at least one of `requiredFiles`,
`anyFiles`, `requiredFilePatterns`, or `anyFilePatterns` is non-empty.

## Binary contract

The game spawns the add-on executable as a child process, captures its stdout
and stderr, waits for it to finish, and validates the output `.theory` file.
Non-zero exit codes indicate an import failure and the stdout+stderr is logged.
The import process must complete within 60 minutes otherwise the process tree is
killed.

### Output validation

After a successful exit, the game validates the output meets the following
conditions.

- `manifest.json` must parse and have `formatId == "string-theory-song"`
- `schemaVersion` must be in range (currently 1-2)
- At least one arrangement must exist
- If `requireAudio` is true (default), an audio entry must exist inside the ZIP
- Arrangement IDs must be unique
- `defaultArrangementId` must reference a valid arrangement
- Each arrangement entry must exist as a JSON file inside the ZIP
- Each arrangement with notes must have `generatedNotes` (playback events)
- `noteCount` must match the actual exported notes

## The `.theory` package format

The `.theory` file is a standard **ZIP archive**. The game uses
`System.IO.Compression.ZipArchive` to read and write it. At minimum it must
contain a manifest, audio, and at least one arrangement.

### ZIP structure

```
output.theory (ZIP archive)
  manifest.json                          -- TheorySongManifest (required)
  arrangements/lead.json                 -- TheoryArrangementData (one per arrangement, required)
  arrangements/rhythm.json
  audio/song.ogg                         -- Audio file (required)
  audio/song_preview.ogg                 -- Optional preview audio
  assets/cover.png                       -- Optional cover art
  editor/editor-state.json               -- Optional chart editor state
  user/tone-lab-mappings.json            -- Optional tone lab preset mappings
```

### manifest.json

```json
{
  "formatId": "string-theory-song",
  "schemaVersion": 2,
  "packageId": "abc123def456",
  "createdAtUtcTicks": 638000000000000000,
  "modifiedAtUtcTicks": 638000000000000000,
  "title": "Song Title",
  "artist": "Artist Name",
  "album": "Album Name",
  "subtitle": "",
  "genre": "Rock",
  "year": "2024",
  "defaultArrangementId": "lead::1",
  "primaryAudioEntry": "audio/song.ogg",
  "coverArtEntry": "assets/cover.png",
  "durationSeconds": 240.5,
  "difficultyRating": 4,
  "provenance": {
    "sourceType": "com.example.my-importer",
    "sourceDisplayName": "song.mysong",
    "sourcePath": "C:/Songs/song.mysong",
    "sourceLastWriteUtcTicks": 638000000000000000,
    "sourceSizeBytes": 5000000,
    "sourceContentFingerprint": "v1:a1b2c3d4e5...",
    "importedAtUtcTicks": 638000000000000000,
    "converterName": "My Importer",
    "converterVersion": "1.0.0"
  },
  "audio": [
    {
      "id": "primary",
      "entry": "audio/song.ogg",
      "displayName": "Primary",
      "role": "primary",
      "contentType": "audio/ogg",
      "sourceSizeBytes": 1234567,
      "defaultForPlayback": true
    }
  ],
  "stems": [],
  "arrangements": [
    {
      "arrangementId": "lead::1",
      "displayName": "Lead",
      "instrumentType": "guitar",
      "route": "Lead",
      "groupId": "lead",
      "groupDisplayName": "Lead",
      "difficultyLabel": "Full",
      "difficultyUiIndex": 0,
      "hasDifficultyVariants": false,
      "entry": "arrangements/lead::1.json",
      "noteCount": 500,
      "tabCount": 0,
      "score": 100,
      "difficultyRating": 4,
      "preserveImportedRuntimeNotes": false,
      "tuningPitches": [64, 59, 55, 50, 45, 40],
      "tuningDisplayName": "E Standard"
    }
  ]
}
```

**Requirements:**

- `formatId` must be exactly `"string-theory-song"`
- `schemaVersion` must be between 1 and the current version (2)
- `defaultArrangementId` must match an arrangement in the `arrangements` list
- Audio files are referenced by ZIP entry path via `primaryAudioEntry` and each
  `audio[].entry` field
- The `arrangements` list must have at least one entry
- The `entry` field in each arrangement summary must match a file inside the
  ZIP under `arrangements/`

### Arrangement JSON schema

Each arrangement file at `arrangements/<arrangementId>.json`:

```json
{
  "schemaVersion": 2,
  "arrangementId": "lead::1",
  "displayName": "Lead",
  "instrumentType": "guitar",
  "route": "Lead",
  "durationSeconds": 240.5,
  "difficultyRating": 4,
  "preserveImportedRuntimeNotes": false,
  "tuningPitches": [64, 59, 55, 50, 45, 40],
  "tuningDisplayName": "E Standard",
  "timing": {
    "averageTempoBpm": 120.0,
    "capo": 0,
    "beats": [
      { "timeSeconds": 0.0, "measure": 0 },
      { "timeSeconds": 0.5, "measure": 0 }
    ],
    "sections": [
      { "name": "Intro", "number": 1, "timeSeconds": 0.0 },
      { "name": "Verse", "number": 2, "timeSeconds": 32.0 }
    ]
  },
  "tones": {
    "baseToneName": "Clean",
    "changes": [
      { "timeSeconds": 0.0, "toneName": "Clean", "toneId": 0 }
    ],
    "definitions": [
      {
        "name": "Clean",
        "key": "clean_01",
        "rawToneEntry": "<JSON string of the source tone data>",
        "preferredPresetName": "",
        "fallbackSearchText": "clean",
        "preset": {
          "presetId": "abcdef1234567890",
          "presetName": "Clean",
          "inputGainDb": 13.0,
          "outputGainDb": 7.5,
          "pedalChain": []
        },
        "fallback": {
          "preferredPresetName": "",
          "searchText": ""
        }
      }
    ]
  },
  "generatedPart": {
    "partId": "lead::1",
    "displayName": "Lead",
    "instrumentName": "guitar",
    "sourceMidiChannel": 0,
    "sourceMidiProgram": 29,
    "isDrum": false,
    "isGuitarFamily": true
  },
  "notes": [
    {
      "id": 1,
      "time": 1.0,
      "duration": 0.5,
      "stringIndex": 0,
      "fret": 5,
      "noteName": "A",
      "chordId": -1,
      "primaryTechnique": 0,
      "slideTargetFret": -1,
      "bendStep": 0.0,
      "bendPreBend": false,
      "bendRelease": false,
      "muted": false,
      "palmMute": false,
      "fretHandMute": false,
      "hammerOn": false,
      "pullOff": false,
      "hopo": false,
      "vibrato": false,
      "harmonic": false,
      "accent": false,
      "tap": false,
      "tremolo": false,
      "pinchHarmonic": false,
      "maxBend": 0.0,
      "legato": false,
      "requiresPluck": true,
      "linkedFromNoteId": -1,
      "bendPoints": [],
      "techniqueSegments": []
    }
  ],
  "generatedNotes": [
    {
      "startTimeSeconds": 1.0,
      "durationSeconds": 0.5,
      "midiNote": 69,
      "velocity": 100,
      "channel": 0,
      "partId": "lead::1",
      "techniqueVariant": 0,
      "legatoTransitionKind": 0,
      "attackVelocityScale": 1.0,
      "pitchCurve": [
        { "normalizedTime": 0.0, "semitoneOffset": 0.0 }
      ]
    }
  ]
}
```

**`generatedNotes` is required** if the arrangement has notes. These are MIDI-
based playback events that the game uses for audio output. Without them,
validation will fail.

### Note data

Each note has:

| Field | Type | Description |
|---|---|---|
| `id` | int | Sequential note identifier |
| `time` | float | Start time in seconds |
| `duration` | float | Sustain/release time in seconds |
| `stringIndex` | int | Guitar string (0 = high E, 5 = low E) |
| `fret` | int | Fret number |
| `noteName` | string | Note name (e.g. "A", "C#") |
| `chordId` | int | Chord template identifier, -1 if not a chord |
| `chordName` | string | Chord display name |
| `primaryTechnique` | int | 0=None, 1=HammerOn, 2=PullOff, 3=Slide, 4=Bend, 5=Vibrato |
| `slideTargetFret` | int | Slide destination fret, -1 if no slide |
| `bendStep` | float | Maximum bend amount in semitones |
| `muted` | bool | Note is muted (palm mute or fret hand mute) |
| `palmMute` | bool | Note is palm muted |
| `fretHandMute` | bool | Note is fret hand muted |
| `hammerOn` | bool | Note is a hammer-on |
| `pullOff` | bool | Note is a pull-off |
| `hopo` | bool | Note is a hammer-on/pull-off (general) |
| `vibrato` | bool | Note has vibrato |
| `harmonic` | bool | Note is a harmonic (natural or pinch) |
| `accent` | bool | Note is accented |
| `tap` | bool | Note is a tap |
| `tremolo` | bool | Note has tremolo picking |
| `pinchHarmonic` | bool | Note is a pinch harmonic |
| `maxBend` | float | Maximum bend value from source |
| `legato` | bool | Note is linked from a previous note (no pluck) |
| `requiresPluck` | bool | Whether this note needs a pluck event |
| `linkedFromNoteId` | int | ID of the note this legato note is linked from, -1 if none |
| `bendPoints` | [BendPoint](#bendpoint)[] | Bend curve points |
| `techniqueSegments` | [TechniqueSegment](#techniquesegment)[] | Visual technique segments (type, offsets, fret/bend data) |

#### BendPoint

| Field | Type | Description |
|---|---|---|
| `timeSeconds` | float | Time offset from note start in seconds |
| `step` | float | Bend amount in semitones (positive = bend up) |

#### TechniqueSegment

| Field | Type | Description |
|---|---|---|
| `type` | int | See segment types below |
| `startOffset` | float | Start offset from note start, normalized 0-1 |
| `endOffset` | float | End offset from note start, normalized 0-1 |
| `startFret` | int | Start fret (for slides), -1 if unused |
| `endFret` | int | End fret (for slides), -1 if unused |
| `startBend` | float | Start bend amount in semitones |
| `endBend` | float | End bend amount in semitones |

**Segment types:**

| Type | Value |
|---|---|
| 0 | Slide |
| 1 | Bend |
| 2 | Sustain |
| 3 | Vibrato (bend-driven or generic) |

### Timing data

```json
{
  "averageTempoBpm": 120.0,
  "capo": 0,
  "beats": [
    { "timeSeconds": 0.0, "measure": 0 }
  ],
  "sections": [
    { "name": "Intro", "number": 1, "timeSeconds": 0.0 }
  ]
}
```

- `beats` are ebeats (extremity beats used by Rocksmith-style highways)
- `sections` are named song sections for navigation

### Tone data

Each arrangement has tone definitions with optional ToneLab presets.

```json
{
  "baseToneName": "Clean",
  "changes": [
    { "timeSeconds": 0.0, "toneName": "Clean", "toneId": 0 }
  ],
  "definitions": [
    {
      "name": "Clean",
      "key": "clean_01",
      "rawToneEntry": "<raw JSON from source>",
      "preferredPresetName": "",
      "fallbackSearchText": "clean",
      "preset": {
        "presetId": "abcdef",
        "presetName": "Clean",
        "inputGainDb": 13.0,
        "outputGainDb": 7.5,
        "pedalChain": [
          {
            "instanceId": "guid",
            "pedalType": "Amp",
            "descriptorId": "amp",
            "enabled": true,
            "settingsJson": "{}"
          }
        ]
      },
      "fallback": {
        "preferredPresetName": "",
        "searchText": ""
      }
    }
  ]
}
```

- `rawToneEntry` stores the source format's tone data as a JSON string (for
  reference and future re-processing)
- `preset` stores a ToneLab-compatible preset with pedal chain
- `fallback` provides search hints if no matching ToneLab preset is found
- `key` is a unique key for the tone definition within the arrangement

### Generated notes (playback events)

Each generated note event represents a MIDI-like playback event:

```json
{
  "startTimeSeconds": 1.0,
  "durationSeconds": 0.5,
  "pitchPreRollSeconds": 0.0,
  "midiNote": 69,
  "velocity": 100,
  "channel": 0,
  "partId": "lead::1",
  "partName": "Lead",
  "techniqueVariant": 0,
  "legatoTransitionKind": 0,
  "attackVelocityScale": 1.0,
  "vibratoDepthSemitones": 0.0,
  "vibratoRateHz": 0.0,
  "vibratoDelayNormalized": 0.0,
  "vibratoFadeNormalized": 0.0,
  "pitchBendRangeSemitones": 0,
  "pitchCurve": [
    { "normalizedTime": 0.0, "semitoneOffset": 0.0 }
  ]
}
```

**Technique variants:**

| Value | Meaning |
|---|---|
| 0 | Normal (picked) |
| 1 | Palm mute |
| 2 | Fret hand mute |
| 3 | Harmonic |

**Legato transition kinds:**

| Value | Meaning |
|---|---|
| 0 | None (pick attack) |
| 1 | Slide |
| 2 | Hammer-on |
| 3 | Pull-off |

### Audio embedding

Audio files are stored inside the ZIP under the `audio/` directory. The
supported formats are those that Unity can play: **OGG Vorbis** (recommended),
WAV, MP3, FLAC.

The manifest's `audio` array lists each audio file:

```json
"audio": [
  {
    "id": "primary",
    "entry": "audio/song.ogg",
    "displayName": "Primary",
    "role": "primary",
    "contentType": "audio/ogg",
    "sourceSizeBytes": 1234567,
    "defaultForPlayback": true
  }
]
```

- Include a `role: "preview"` entry for short preview clips used in the song
  library UI if desired
- Compression before embedding is recommended to reduce package size

### Provenance

After a successful import, the game stamps provenance into the manifest. If
your add-on pre-fills the `provenance` object, the game respects those values
and only fills in missing fields:

```json
"provenance": {
  "sourceType": "com.example.my-importer",
  "sourceDisplayName": "source.mysong",
  "sourcePath": "C:/Songs/source.mysong",
  "sourceLastWriteUtcTicks": 638000000000000000,
  "sourceSizeBytes": 5000000,
  "sourceContentFingerprint": "v1:a1b2c3d4e5...",
  "importedAtUtcTicks": 638000000000000000,
  "converterName": "My Importer",
  "converterVersion": "1.0.0"
}
```

If you leave `sourceContentFingerprint` empty, the game computes a format-
agnostic fingerprint based on file names and sizes.

## Key source files

| File | Purpose |
|---|---|
| `Assets/SongImporters/SongImporterRegistry.cs` | Add-on discovery, process spawning, validation, provenance stamping |
| `Assets/SongImporters/SongImporterModels.cs` | Data models: `SongImporterManifest`, `SongImporterEntrypoint`, `SongImporterFolderSignature` |
| `Assets/TheoryFormat/TheoryPackageFormat.cs` | `.theory` format constants (extension, entry names, schema version) |
| `Assets/TheoryFormat/TheorySongModels.cs` | All theory package data models (manifest, arrangement, notes, tones, generated notes) |
| `Assets/TheoryFormat/TheoryPackageIO.cs` | Read/write `.theory` packages (ZIP), manifest and arrangement deserialization |
| `Assets/ExternalContentPaths.cs` | Importer directory paths (`StreamingImportersDirectory`, `PersistentImportersDirectory`) |
| `Assets/SongLibraryService.cs` | Library discovery of import candidates |
| `Assets/ChartEditor/ChartEditorImportService.cs` | Chart editor import workflow |
