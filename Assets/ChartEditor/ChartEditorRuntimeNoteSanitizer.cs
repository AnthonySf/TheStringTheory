using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ChartEditorRuntimeNoteSanitizer
{
    public static void SanitizeProjectNotes(ChartEditorProject project)
    {
        if (project?.tracks == null)
            return;

        for (int i = 0; i < project.tracks.Count; i++)
            SanitizeTrackNotes(project.tracks[i]);
    }

    public static void SanitizeTrackNotes(ChartEditorTrack track)
    {
        if (track == null)
            return;

        track.notes = PrepareChartNotesForRuntime(track.notes, !track.preserveImportedRuntimeNotes)
            .OrderBy(note => note.timeSeconds)
            .ThenBy(note => note.stringOrLane)
            .ToList();
        track.EnsureDefaults();
    }

    public static List<ChartEditorNote> PrepareChartNotesForRuntime(
        IEnumerable<ChartEditorNote> sourceNotes,
        bool trimSameStringOverlaps = true)
    {
        List<ChartEditorNote> notes = sourceNotes?
            .Where(note => note != null)
            .Select(CloneNote)
            .ToList() ?? new List<ChartEditorNote>();

        if (trimSameStringOverlaps)
            TrimSameStringChartNoteOverlaps(notes);
        return notes;
    }

    public static void TrimSameStringNoteOverlaps(List<NoteData> notes)
    {
        if (notes == null || notes.Count == 0)
            return;

        var indexedNotes = notes
            .Select((note, index) => new { note, index })
            .GroupBy(entry => entry.note.stringIdx);

        foreach (var stringGroup in indexedNotes)
        {
            List<(NoteData note, int index)> ordered = stringGroup
                .OrderBy(entry => entry.note.time)
                .ThenBy(entry => entry.note.fret)
                .Select(entry => (entry.note, entry.index))
                .ToList();

            for (int i = 0; i + 1 < ordered.Count; i++)
            {
                NoteData current = ordered[i].note;
                NoteData next = ordered[i + 1].note;
                if (next.time <= current.time + 0.0001f)
                    continue;

                float maxOffset = Mathf.Max(0f, next.time - current.time);
                if (GetVisualNoteEndOffset(current) <= maxOffset + 0.0005f)
                    continue;

                notes[ordered[i].index] = TrimNoteDataToMaxOffset(current, maxOffset);
            }
        }
    }

    private static void TrimSameStringChartNoteOverlaps(List<ChartEditorNote> notes)
    {
        if (notes == null || notes.Count == 0)
            return;

        var indexedNotes = notes
            .Select((note, index) => new { note, index })
            .GroupBy(entry => entry.note.stringOrLane);

        foreach (var stringGroup in indexedNotes)
        {
            List<(ChartEditorNote note, int index)> ordered = stringGroup
                .OrderBy(entry => entry.note.timeSeconds)
                .ThenBy(entry => entry.note.fret)
                .Select(entry => (entry.note, entry.index))
                .ToList();

            for (int i = 0; i + 1 < ordered.Count; i++)
            {
                ChartEditorNote current = ordered[i].note;
                ChartEditorNote next = ordered[i + 1].note;
                if (next.timeSeconds <= current.timeSeconds + 0.0001)
                    continue;

                double maxOffset = System.Math.Max(0.0, next.timeSeconds - current.timeSeconds);
                if (GetChartNoteVisualEndOffset(current) <= maxOffset + 0.0005)
                    continue;

                TrimChartNoteToMaxOffset(current, maxOffset);
            }
        }
    }

    private static float GetVisualNoteEndOffset(NoteData note)
    {
        float endOffset = Mathf.Max(0f, note.duration);
        if (note.techniqueSegments != null)
        {
            for (int i = 0; i < note.techniqueSegments.Count; i++)
                endOffset = Mathf.Max(endOffset, Mathf.Max(note.techniqueSegments[i].startOffset, note.techniqueSegments[i].endOffset));
        }

        return endOffset;
    }

    private static NoteData TrimNoteDataToMaxOffset(NoteData note, float maxOffset)
    {
        maxOffset = Mathf.Max(0f, maxOffset);
        note.duration = Mathf.Min(Mathf.Max(0f, note.duration), maxOffset);
        if (note.bendVisualDuration > 0f)
            note.bendVisualDuration = Mathf.Min(note.bendVisualDuration, maxOffset);

        if (note.techniqueSegments == null || note.techniqueSegments.Count == 0)
            return note;

        List<NoteTechniqueSegmentData> trimmedSegments = new List<NoteTechniqueSegmentData>(note.techniqueSegments.Count);
        for (int i = 0; i < note.techniqueSegments.Count; i++)
        {
            NoteTechniqueSegmentData segment = note.techniqueSegments[i];
            float start = Mathf.Clamp(segment.startOffset, 0f, maxOffset);
            float end = Mathf.Clamp(segment.endOffset, 0f, maxOffset);
            if (end <= start + 0.0001f)
                continue;

            trimmedSegments.Add(new NoteTechniqueSegmentData(
                segment.type,
                start,
                end,
                segment.startFret,
                segment.endFret,
                segment.startBend,
                segment.endBend));
        }

        note.techniqueSegments = trimmedSegments.Count > 0 ? trimmedSegments : null;
        return note;
    }

    private static double GetChartNoteVisualEndOffset(ChartEditorNote note)
    {
        double endOffset = System.Math.Max(0.0, note.durationSeconds);
        if (note.techniqueSegments != null)
        {
            for (int i = 0; i < note.techniqueSegments.Count; i++)
            {
                ChartEditorTechniqueSegment segment = note.techniqueSegments[i];
                if (segment != null)
                    endOffset = System.Math.Max(endOffset, System.Math.Max(segment.startOffset, segment.endOffset));
            }
        }

        return endOffset;
    }

    private static void TrimChartNoteToMaxOffset(ChartEditorNote note, double maxOffset)
    {
        maxOffset = System.Math.Max(0.0, maxOffset);
        note.durationSeconds = System.Math.Min(System.Math.Max(0.0, note.durationSeconds), maxOffset);
        if (note.bendVisualDuration > 0f)
            note.bendVisualDuration = Mathf.Min(note.bendVisualDuration, (float)maxOffset);

        if (note.bendPoints != null)
            note.bendPoints = note.bendPoints.Where(point => point != null && point.timeSeconds <= maxOffset + 0.0005).ToList();

        if (note.techniqueSegments == null || note.techniqueSegments.Count == 0)
            return;

        List<ChartEditorTechniqueSegment> trimmedSegments = new List<ChartEditorTechniqueSegment>(note.techniqueSegments.Count);
        for (int i = 0; i < note.techniqueSegments.Count; i++)
        {
            ChartEditorTechniqueSegment segment = note.techniqueSegments[i];
            if (segment == null)
                continue;

            float start = Mathf.Clamp(segment.startOffset, 0f, (float)maxOffset);
            float end = Mathf.Clamp(segment.endOffset, 0f, (float)maxOffset);
            if (end <= start + 0.0001f)
                continue;

            trimmedSegments.Add(new ChartEditorTechniqueSegment
            {
                type = segment.type,
                startOffset = start,
                endOffset = end,
                startFret = segment.startFret,
                endFret = segment.endFret,
                startBend = segment.startBend,
                endBend = segment.endBend
            });
        }

        note.techniqueSegments = trimmedSegments;
    }

    private static ChartEditorNote CloneNote(ChartEditorNote source)
    {
        return new ChartEditorNote
        {
            id = source.id,
            sourceNoteId = source.sourceNoteId,
            chartTimeSeconds = source.chartTimeSeconds,
            timeSeconds = source.timeSeconds,
            durationSeconds = source.durationSeconds,
            beatPosition = source.beatPosition,
            durationBeats = source.durationBeats,
            usesBeatMapTiming = source.usesBeatMapTiming,
            stringOrLane = source.stringOrLane,
            fret = source.fret,
            velocity = source.velocity,
            noteName = source.noteName,
            chordId = source.chordId,
            chordName = source.chordName,
            technique = source.technique,
            slideTargetFret = source.slideTargetFret,
            bendStep = source.bendStep,
            bendVisualStartTime = source.bendVisualStartTime,
            bendVisualDuration = source.bendVisualDuration,
            bendPreBend = source.bendPreBend,
            bendRelease = source.bendRelease,
            muted = source.muted,
            palmMute = source.palmMute,
            fretHandMute = source.fretHandMute,
            harmonic = source.harmonic,
            accent = source.accent,
            tap = source.tap,
            tremolo = source.tremolo,
            pinchHarmonic = source.pinchHarmonic,
            vibratoStrength = source.vibratoStrength,
            maxBend = source.maxBend,
            legato = source.legato,
            requiresPluck = source.requiresPluck,
            linkedFromNoteId = source.linkedFromNoteId,
            bendPoints = source.bendPoints?
                .Where(point => point != null)
                .Select(point => new ChartEditorBendPoint { timeSeconds = point.timeSeconds, step = point.step })
                .ToList() ?? new List<ChartEditorBendPoint>(),
            techniqueSegments = source.techniqueSegments?
                .Where(segment => segment != null)
                .Select(segment => new ChartEditorTechniqueSegment
                {
                    type = segment.type,
                    startOffset = segment.startOffset,
                    endOffset = segment.endOffset,
                    startFret = segment.startFret,
                    endFret = segment.endFret,
                    startBend = segment.startBend,
                    endBend = segment.endBend
                })
                .ToList() ?? new List<ChartEditorTechniqueSegment>()
        };
    }
}
