using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ChartEditorHighwayPreviewFrame
{
    public string trackId;
    public string trackName;
    public string tuningName;
    public ChartEditorTrackRole role;
    public int laneCount;
    public float songTime;
    public float songDurationSeconds;
    public List<NoteData> notes = new List<NoteData>();
    public List<ArpeggioGuideData> arpeggioGuides = new List<ArpeggioGuideData>();
}

public static class ChartEditorHighwayPreviewSnapshotBuilder
{
    public static ChartEditorHighwayPreviewFrame Build(ChartEditorProject project)
    {
        if (project == null)
            return null;

        project.EnsureDefaultsThrottled();
        ChartEditorTrack track = project.SelectedTrack;
        if (track == null || !IsSupportedPreviewTrack(track))
            return new ChartEditorHighwayPreviewFrame
            {
                trackId = track?.id ?? string.Empty,
                trackName = track != null ? FormatTrackName(track) : "No Track",
                tuningName = track?.tuning?.displayName ?? string.Empty,
                role = track?.role ?? ChartEditorTrackRole.Custom,
                laneCount = 0,
                songTime = Mathf.Max(0f, (float)project.cursorTimeSeconds),
                songDurationSeconds = Mathf.Max(0.1f, (float)ChartEditorTimingService.GetCachedDurationSeconds(project)),
                notes = new List<NoteData>(),
                arpeggioGuides = new List<ArpeggioGuideData>()
            };

        int laneCount = ResolveLaneCount(track);
        List<ChartEditorNote> orderedNotes = track.notes?
            .Where(note => note != null)
            .OrderBy(note => note.timeSeconds)
            .ThenBy(note => note.stringOrLane)
            .ToList() ?? new List<ChartEditorNote>();

        ChartEditorHighwayPreviewFrame frame = new ChartEditorHighwayPreviewFrame
        {
            trackId = track.id ?? string.Empty,
            trackName = FormatTrackName(track),
            tuningName = FirstNonEmpty(track.tuning?.displayName, track.role.ToString()),
            role = track.role,
            laneCount = laneCount,
            songTime = Mathf.Max(0f, (float)project.cursorTimeSeconds),
            songDurationSeconds = Mathf.Max(0.1f, project.DurationSeconds),
            notes = new List<NoteData>(orderedNotes.Count),
            arpeggioGuides = BuildArpeggioGuides(track)
        };

        for (int i = 0; i < orderedNotes.Count; i++)
            frame.notes.Add(ToNoteData(orderedNotes[i], i, laneCount, track.role));

        if (track.role != ChartEditorTrackRole.Drums)
        {
            ChartEditorRuntimeNoteSanitizer.TrimSameStringNoteOverlaps(frame.notes);
            // The gameplay loader rewrites linked hammer-on/pull-off pairs into
            // source-anchored transitions before the highway renderer sees them
            // (legato arc from the origin, destination box hidden). Apply the
            // same rewrite to this render-only list so the preview matches the
            // in-game look; the editable project notes keep their stored form.
            TheorySongLoader.NormalizeTheoryLegatoTransitions(frame.notes);
        }

        return frame;
    }

    private static List<ArpeggioGuideData> BuildArpeggioGuides(ChartEditorTrack track)
    {
        List<ArpeggioGuideData> guides = new List<ArpeggioGuideData>();
        if (track?.arpeggioGuides == null)
            return guides;

        for (int i = 0; i < track.arpeggioGuides.Count; i++)
        {
            ChartEditorArpeggioGuide source = track.arpeggioGuides[i];
            if (source == null)
                continue;

            guides.Add(new ArpeggioGuideData
            {
                id = source.id,
                startTime = Mathf.Max(0f, source.startTime),
                endTime = Mathf.Max(source.startTime, source.endTime),
                chordName = source.chordName ?? string.Empty,
                stringFrets = source.stringFrets != null ? (int[])source.stringFrets.Clone() : null
            });
        }

        return guides;
    }

    public static bool IsSupportedPreviewTrack(ChartEditorTrack track)
    {
        return track != null &&
               (track.role == ChartEditorTrackRole.LeadGuitar ||
                track.role == ChartEditorTrackRole.RhythmGuitar ||
                track.role == ChartEditorTrackRole.Bass ||
                track.role == ChartEditorTrackRole.Drums ||
                track.role == ChartEditorTrackRole.Custom);
    }

    public static int ResolveLaneCount(ChartEditorTrack track)
    {
        if (track?.role == ChartEditorTrackRole.Drums)
            return DrumLaneMapper.LaneCount;

        int defaultCount = track?.role == ChartEditorTrackRole.Bass ? 4 :
            track?.role == ChartEditorTrackRole.Piano ? 8 : 6;
        int count = defaultCount;
        if (track?.tuning?.stringPitches != null && track.tuning.stringPitches.Length > 0)
            count = Mathf.Clamp(track.tuning.stringPitches.Length, 1, 8);

        if (track?.notes != null)
        {
            for (int i = 0; i < track.notes.Count; i++)
            {
                ChartEditorNote note = track.notes[i];
                if (note != null)
                    count = Mathf.Max(count, note.stringOrLane + 1);
            }
        }

        return Mathf.Clamp(count, 1, 8);
    }

    private static NoteData ToNoteData(ChartEditorNote note, int fallbackIndex, int laneCount, ChartEditorTrackRole role)
    {
        int sourceId = note.sourceNoteId >= 0 ? note.sourceNoteId : StableId(note.id, fallbackIndex);
        bool isDrums = role == ChartEditorTrackRole.Drums;
        // Match the runtime mute split (TheorySongLoader.LoadSong): only
        // dead/x notes render muted; palm mutes are a separate technique.
        bool runtimeMuted = note.hasRuntimeMuted ? note.runtimeMuted : note.muted && !note.palmMute;
        bool runtimePalmMute = note.hasRuntimePalmMute ? note.runtimePalmMute : note.palmMute;
        if (runtimeMuted && note.palmMute && !note.fretHandMute)
        {
            runtimeMuted = false;
            runtimePalmMute = true;
        }
        List<NoteTechniqueSegmentData> segments = isDrums ? null : BuildPreviewTechniqueSegments(note);
        bool hammerOn = !isDrums && IsTechniqueEnabled(note, NoteTechnique.HammerOn);
        bool pullOff = !isDrums && IsTechniqueEnabled(note, NoteTechnique.PullOff);
        int lane = Mathf.Clamp(note.stringOrLane, 0, Math.Max(0, laneCount - 1));
        int fret = Mathf.Max(0, note.fret);
        if (isDrums)
        {
            if (fret >= 35 && fret <= 87)
                lane = Mathf.Clamp(DrumLaneMapper.MapGeneralMidiToLane(fret), 0, Math.Max(0, laneCount - 1));
            else if (DrumLaneMapper.TryResolveLaneFromLabel(note.noteName, out int labelLane))
                lane = Mathf.Clamp(labelLane, 0, Math.Max(0, laneCount - 1));

            fret = lane + 1;
        }

        return new NoteData(
            sourceId,
            Mathf.Max(0f, (float)note.timeSeconds),
            Mathf.Max(0f, (float)note.durationSeconds),
            lane,
            fret,
            note.noteName ?? string.Empty,
            note.chordId,
            isDrums ? NoteTechnique.None : ResolvePrimaryTechnique(note),
            isDrums ? -1 : note.slideTargetFret,
            isDrums ? 0f : note.bendStep,
            isDrums ? false : note.legato || hammerOn || pullOff,
            isDrums ? true : ((hammerOn || pullOff) ? false : note.requiresPluck),
            isDrums ? -1 : note.linkedFromNoteId,
            isDrums ? false : note.bendPreBend,
            isDrums ? false : note.bendRelease,
            isDrums ? -1f : note.bendVisualStartTime,
            isDrums ? 0f : note.bendVisualDuration,
            segments,
            isDrums ? false : runtimeMuted,
            note.chordName ?? string.Empty,
            palmMuted: !isDrums && runtimePalmMute);
    }

    private static List<NoteTechniqueSegmentData> BuildPreviewTechniqueSegments(ChartEditorNote note)
    {
        List<NoteTechniqueSegmentData> segments = null;
        if (note?.techniqueSegments != null && note.techniqueSegments.Count > 0)
        {
            segments = new List<NoteTechniqueSegmentData>(note.techniqueSegments.Count);
            for (int i = 0; i < note.techniqueSegments.Count; i++)
            {
                ChartEditorTechniqueSegment segment = note.techniqueSegments[i];
                if (segment == null)
                    continue;

                segments.Add(new NoteTechniqueSegmentData(
                    segment.type,
                    segment.startOffset,
                    segment.endOffset,
                    segment.startFret,
                    segment.endFret,
                    segment.startBend,
                    segment.endBend));
            }
        }

        if (note == null)
            return segments;

        NoteTechnique primaryTechnique = ResolvePrimaryTechnique(note);
        bool wantsBendVisual =
            primaryTechnique == NoteTechnique.Bend ||
            Mathf.Abs(note.bendStep) > 0.01f ||
            note.bendPreBend ||
            note.bendRelease;
        bool wantsNormalizedFlagSegment =
            primaryTechnique == NoteTechnique.Vibrato &&
            (segments == null || segments.Count == 0);
        if (!wantsBendVisual && !wantsNormalizedFlagSegment)
            return segments;

        PsarcCachedNoteData source = new PsarcCachedNoteData
        {
            id = note.sourceNoteId,
            time = Mathf.Max(0f, (float)note.timeSeconds),
            duration = Mathf.Max(0f, (float)note.durationSeconds),
            stringIdx = note.stringOrLane,
            fret = note.fret,
            technique = (int)primaryTechnique,
            slideTargetFret = note.slideTargetFret,
            bendStep = Mathf.Max(note.bendStep, note.maxBend),
            bendVisualStartTime = note.bendVisualStartTime,
            bendVisualDuration = note.bendVisualDuration,
            bendPreBend = note.bendPreBend,
            bendRelease = note.bendRelease,
            hasVibrato = primaryTechnique == NoteTechnique.Vibrato,
            maxBend = Mathf.Max(note.maxBend, note.bendStep),
            bendPoints = new List<PsarcCachedBendPointData>(),
            techniqueSegments = new List<PsarcCachedTechniqueSegmentData>()
        };

        if (note.bendPoints != null)
        {
            for (int i = 0; i < note.bendPoints.Count; i++)
            {
                ChartEditorBendPoint point = note.bendPoints[i];
                if (point == null)
                    continue;

                source.bendPoints.Add(new PsarcCachedBendPointData
                {
                    timeSeconds = point.timeSeconds,
                    step = point.step
                });
            }
        }

        if (segments != null)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                NoteTechniqueSegmentData segment = segments[i];
                source.techniqueSegments.Add(new PsarcCachedTechniqueSegmentData
                {
                    type = (int)segment.type,
                    startOffset = segment.startOffset,
                    endOffset = segment.endOffset,
                    startFret = segment.startFret,
                    endFret = segment.endFret,
                    startBend = segment.startBend,
                    endBend = segment.endBend
                });
            }
        }

        return PsarcTechniqueSegmentNormalizer.BuildNormalizedTechniqueSegments(source);
    }

    private static NoteTechnique ResolvePrimaryTechnique(ChartEditorNote note)
    {
        if (note == null)
            return NoteTechnique.None;

        if (note.technique == NoteTechnique.HammerOn || note.technique == NoteTechnique.PullOff)
            return note.technique;
        if (IsTechniqueEnabled(note, NoteTechnique.Slide))
            return NoteTechnique.Slide;
        if (IsTechniqueEnabled(note, NoteTechnique.Bend))
            return NoteTechnique.Bend;
        if (IsTechniqueEnabled(note, NoteTechnique.Vibrato))
            return NoteTechnique.Vibrato;
        return NoteTechnique.None;
    }

    private static bool IsTechniqueEnabled(ChartEditorNote note, NoteTechnique technique)
    {
        if (note == null)
            return false;

        if (note.technique == technique)
            return true;

        switch (technique)
        {
            case NoteTechnique.Slide:
                return note.slideTargetFret >= 0 || HasTechniqueSegment(note, NoteTechniqueSegmentType.Slide);
            case NoteTechnique.Bend:
                return Mathf.Abs(note.bendStep) > 0.01f ||
                       note.bendPreBend ||
                       note.bendRelease ||
                       HasTechniqueSegment(note, NoteTechniqueSegmentType.Bend) ||
                       HasBendBearingTechniqueSegment(note);
            case NoteTechnique.Vibrato:
                return HasTechniqueSegment(note, NoteTechniqueSegmentType.Vibrato);
            default:
                return false;
        }
    }

    private static bool HasTechniqueSegment(ChartEditorNote note, NoteTechniqueSegmentType type)
    {
        return note?.techniqueSegments != null &&
               note.techniqueSegments.Any(segment => segment != null && segment.type == type);
    }

    private static bool HasBendBearingTechniqueSegment(ChartEditorNote note)
    {
        if (note?.techniqueSegments == null)
            return false;

        return note.techniqueSegments.Any(segment =>
            segment != null &&
            (segment.type == NoteTechniqueSegmentType.Bend ||
             segment.type == NoteTechniqueSegmentType.Sustain ||
             segment.type == NoteTechniqueSegmentType.Vibrato) &&
            (Mathf.Abs(segment.startBend) > 0.01f || Mathf.Abs(segment.endBend) > 0.01f));
    }

    private static int StableId(string text, int fallback)
    {
        unchecked
        {
            int hash = 17;
            if (!string.IsNullOrWhiteSpace(text))
            {
                for (int i = 0; i < text.Length; i++)
                    hash = hash * 31 + text[i];
            }

            hash = hash * 31 + fallback;
            return hash == int.MinValue ? 0 : Math.Abs(hash);
        }
    }

    private static string FormatTrackName(ChartEditorTrack track)
    {
        if (track == null)
            return "Track";

        switch (track.role)
        {
            case ChartEditorTrackRole.LeadGuitar:
                return "Lead Guitar";
            case ChartEditorTrackRole.RhythmGuitar:
                return "Rhythm Guitar";
            case ChartEditorTrackRole.Bass:
                return "Bass";
            case ChartEditorTrackRole.Piano:
                return "Piano";
            case ChartEditorTrackRole.Drums:
                return "Drums";
            case ChartEditorTrackRole.Vocals:
                return "Vocals";
            default:
                return FirstNonEmpty(track.displayName, track.importedName, "Track");
        }
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return string.Empty;

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return string.Empty;
    }
}
