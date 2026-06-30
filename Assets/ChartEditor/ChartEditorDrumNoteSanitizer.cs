using System.Collections.Generic;

public static class ChartEditorDrumNoteSanitizer
{
    public static bool Sanitize(ChartEditorNote note)
    {
        if (note == null)
            return false;

        bool changed =
            note.technique != NoteTechnique.None ||
            note.slideTargetFret != -1 ||
            note.bendStep != 0f ||
            note.bendVisualStartTime != -1f ||
            note.bendVisualDuration != 0f ||
            note.bendPreBend ||
            note.bendRelease ||
            note.muted ||
            note.palmMute ||
            note.fretHandMute ||
            note.harmonic ||
            note.accent ||
            note.tap ||
            note.tremolo ||
            note.pinchHarmonic ||
            note.vibratoStrength != 0 ||
            note.maxBend != 0f ||
            note.legato ||
            !note.requiresPluck ||
            note.linkedFromNoteId != -1 ||
            (note.bendPoints != null && note.bendPoints.Count > 0) ||
            (note.techniqueSegments != null && note.techniqueSegments.Count > 0);

        note.technique = NoteTechnique.None;
        note.slideTargetFret = -1;
        note.bendStep = 0f;
        note.bendVisualStartTime = -1f;
        note.bendVisualDuration = 0f;
        note.bendPreBend = false;
        note.bendRelease = false;
        note.muted = false;
        note.palmMute = false;
        note.fretHandMute = false;
        note.harmonic = false;
        note.accent = false;
        note.tap = false;
        note.tremolo = false;
        note.pinchHarmonic = false;
        note.vibratoStrength = 0;
        note.maxBend = 0f;
        note.legato = false;
        note.requiresPluck = true;
        note.linkedFromNoteId = -1;
        note.bendPoints ??= new List<ChartEditorBendPoint>();
        note.bendPoints.Clear();
        note.techniqueSegments ??= new List<ChartEditorTechniqueSegment>();
        note.techniqueSegments.Clear();

        return changed;
    }
}
