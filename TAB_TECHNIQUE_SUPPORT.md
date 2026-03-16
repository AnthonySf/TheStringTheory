# Tabs Technique Support Audit

This file documents which guitar techniques are currently represented in the tab gameplay pipeline.

## Currently supported end-to-end

A technique is considered "end-to-end" when:
1. it is parsed from MusicXML,
2. it is attached to `NoteData`, and
3. it has a visible glyph in the tabs renderer.

Supported:
- Hammer-on (`H`)
- Pull-off (`P`)
- Slide (`/` or `\\` depending on direction)
- Bend (`^`)
- Vibrato (`~`)

## Key code locations

- Technique enum values: `Assets/GuitarGameplayModels.cs`
- MusicXML extraction logic: `Assets/MusicXmlLoader.cs` (`ParseTechniqueInfo`, `BuildGameplayNotes`)
- On-screen glyph mapping: `Assets/GuitarTabsRenderer.cs` (`GetTechniqueGlyph`)

## Not currently represented in `NoteTechnique`

Common tab techniques that do not currently have dedicated `NoteTechnique` values and are not rendered as dedicated technique glyphs:

- Palm mute
- Let ring
- Natural/artificial harmonics
- Trill
- Tapping
- Tremolo picking
- Ghost notes / dead-note ornaments beyond generic `X` note label
- Pre-bends / bend-release distinctions
- Slide-in / slide-out distinctions

Notes:
- The note label can show `X` for muted/dead notes, but this is not encoded as a dedicated technique state.
- Tie/slur semantics are used to merge durations and legato behavior, but ties are not shown as separate glyphs in tabs.
