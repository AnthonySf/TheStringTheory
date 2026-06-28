using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ChartEditorTimingService
{
    private const double DefaultTempoBpm = 120.0;
    private const double MinTempoBpm = 20.0;
    private const double MaxTempoBpm = 360.0;
    private const double MinAnchorGapSeconds = 0.02;
    private const double BeatEpsilon = 0.0001;

    public static void EnsureDefaultSyncPoints(ChartEditorProject project)
    {
        EnsureBeatMap(project, attachContentToBeatMap: true);
    }

    public static void EnsureBeatMap(ChartEditorProject project, bool attachContentToBeatMap)
    {
        if (project == null)
            return;

        project.EnsureDefaults();
        project.beatMap.defaultTempoBpm = ClampTempo(project.beatMap.defaultTempoBpm <= 0.001 ? DefaultTempoBpm : project.beatMap.defaultTempoBpm);

        if (!project.beatMap.timeSignatures.Any(change => change != null))
        {
            project.beatMap.timeSignatures.Add(new ChartEditorTimeSignatureChange
            {
                beatPosition = 0.0,
                numerator = 4,
                denominator = 4
            });
        }

        bool hasAnchors = project.beatMap.beatMarkers.Any(marker => marker != null && marker.isAnchor);
        bool hasLegacySyncPoints = project.syncPoints?.Any(point => point != null) == true;
        if (!hasAnchors && hasLegacySyncPoints)
            SeedAnchors(project);

        RebuildBeatMap(project);
        if (attachContentToBeatMap)
            AttachContentToBeatMap(project);
        SyncLegacySyncPointsFromBeatMap(project);
    }

    public static void ApplySyncPoints(ChartEditorProject project)
    {
        ApplyBeatMapToContent(project);
    }

    public static void ApplyBeatMapToContent(ChartEditorProject project)
    {
        if (project == null)
            return;

        EnsureBeatMap(project, attachContentToBeatMap: false);

        if (project.tracks != null)
        {
            for (int trackIndex = 0; trackIndex < project.tracks.Count; trackIndex++)
            {
                List<ChartEditorNote> notes = project.tracks[trackIndex]?.notes;
                if (notes == null)
                    continue;

                for (int noteIndex = 0; noteIndex < notes.Count; noteIndex++)
                {
                    ChartEditorNote note = notes[noteIndex];
                    if (note == null || !note.usesBeatMapTiming)
                        continue;

                    double start = GetAudioTimeForBeat(project, note.beatPosition);
                    double durationBeats = note.durationBeats;
                    if (durationBeats <= BeatEpsilon && note.durationSeconds > BeatEpsilon)
                        durationBeats = Math.Max(0.0, GetBeatPositionForAudioTime(project, note.timeSeconds + note.durationSeconds) - note.beatPosition);

                    double end = GetAudioTimeForBeat(project, note.beatPosition + Math.Max(0.0, durationBeats));
                    note.chartTimeSeconds = Math.Max(0.0, start);
                    note.timeSeconds = Math.Max(0.0, start);
                    note.durationBeats = Math.Max(0.0, durationBeats);
                    note.durationSeconds = Math.Max(0.0, end - start);
                }

                notes.Sort((a, b) => (a?.timeSeconds ?? 0.0).CompareTo(b?.timeSeconds ?? 0.0));
            }
        }

        if (project.sections != null)
        {
            for (int sectionIndex = 0; sectionIndex < project.sections.Count; sectionIndex++)
            {
                ChartEditorSection section = project.sections[sectionIndex];
                if (section == null || !section.usesBeatMapTiming)
                    continue;

                double start = GetAudioTimeForBeat(project, section.startBeatPosition);
                double end = GetAudioTimeForBeat(project, Math.Max(section.startBeatPosition + BeatEpsilon, section.endBeatPosition));
                section.chartStartTimeSeconds = Math.Max(0.0, start);
                section.chartEndTimeSeconds = Math.Max(section.chartStartTimeSeconds + 0.05, end);
                section.startTimeSeconds = section.chartStartTimeSeconds;
                section.endTimeSeconds = section.chartEndTimeSeconds;
            }

            NormalizeSections(project);
        }

        SyncLegacySyncPointsFromBeatMap(project);
        project.dirty = true;
    }

    public static void AttachContentToBeatMap(ChartEditorProject project)
    {
        if (project == null)
            return;

        EnsureBeatMap(project, attachContentToBeatMap: false);

        if (project.tracks != null)
        {
            foreach (ChartEditorTrack track in project.tracks)
            {
                if (track?.notes == null)
                    continue;

                foreach (ChartEditorNote note in track.notes)
                    UpdateNoteBeatTiming(project, note);
            }
        }

        if (project.sections != null)
        {
            foreach (ChartEditorSection section in project.sections)
                UpdateSectionBeatTiming(project, section);
        }
    }

    public static void UpdateNoteBeatTiming(ChartEditorProject project, ChartEditorNote note)
    {
        if (project == null || note == null)
            return;

        note.usesBeatMapTiming = true;
        note.beatPosition = Math.Max(0.0, GetBeatPositionForAudioTime(project, note.timeSeconds));
        double endBeat = Math.Max(note.beatPosition, GetBeatPositionForAudioTime(project, note.timeSeconds + Math.Max(0.0, note.durationSeconds)));
        note.durationBeats = Math.Max(0.0, endBeat - note.beatPosition);
        note.chartTimeSeconds = note.timeSeconds;
    }

    public static void UpdateSectionBeatTiming(ChartEditorProject project, ChartEditorSection section)
    {
        if (project == null || section == null)
            return;

        section.usesBeatMapTiming = true;
        section.startBeatPosition = Math.Max(0.0, GetBeatPositionForAudioTime(project, section.startTimeSeconds));
        section.endBeatPosition = Math.Max(section.startBeatPosition + BeatEpsilon, GetBeatPositionForAudioTime(project, section.endTimeSeconds));
        section.chartStartTimeSeconds = section.startTimeSeconds;
        section.chartEndTimeSeconds = section.endTimeSeconds;
    }

    public static ChartEditorBeatMarker AddAnchorAtAudioTime(ChartEditorProject project, double audioTimeSeconds, bool moveContentWithBeatMap = true)
    {
        if (project == null)
            return null;

        EnsureBeatMap(project, attachContentToBeatMap: true);
        audioTimeSeconds = Math.Max(0.0, Math.Min(project.DurationSeconds, audioTimeSeconds));
        double beatPosition = Math.Max(0.0, Math.Round(GetBeatPositionForAudioTime(project, audioTimeSeconds)));

        ChartEditorBeatMarker marker = FindBeatMarker(project, beatPosition);
        if (marker != null && marker.isAnchor)
            return marker;

        if (marker == null)
        {
            marker = new ChartEditorBeatMarker
            {
                id = Guid.NewGuid().ToString("N"),
                beatPosition = beatPosition
            };
            project.beatMap.beatMarkers.Add(marker);
        }

        marker.isAnchor = true;
        marker.generatedBySynchTheory = false;
        marker.synchTheorySource = string.Empty;
        marker.audioTimeSeconds = audioTimeSeconds;
        marker.label = string.IsNullOrWhiteSpace(marker.label) ? FormatAnchorLabel(project, marker) : marker.label;
        ClearGeneratedSynchTheoryTimingForManualAnchorEdit(project, marker.beatPosition);
        RebuildBeatMap(project);
        FinishBeatMapEdit(project, moveContentWithBeatMap);
        return FindBeatMarker(project, beatPosition, anchorsOnly: true);
    }

    public static ChartEditorBeatMarker AddAnchorAtBeat(ChartEditorProject project, double beatPosition, double audioTimeSeconds, bool moveContentWithBeatMap = true)
    {
        if (project == null)
            return null;

        EnsureBeatMap(project, attachContentToBeatMap: true);
        beatPosition = Math.Max(0.0, beatPosition);
        audioTimeSeconds = Math.Max(0.0, Math.Min(project.DurationSeconds, audioTimeSeconds));

        ChartEditorBeatMarker marker = FindBeatMarker(project, beatPosition);
        if (marker == null)
        {
            marker = new ChartEditorBeatMarker
            {
                id = Guid.NewGuid().ToString("N"),
                beatPosition = beatPosition
            };
            project.beatMap.beatMarkers.Add(marker);
        }

        marker.isAnchor = true;
        marker.generatedBySynchTheory = false;
        marker.synchTheorySource = string.Empty;
        marker.audioTimeSeconds = audioTimeSeconds;
        marker.label = string.IsNullOrWhiteSpace(marker.label) ? FormatAnchorLabel(project, marker) : marker.label;
        ClearGeneratedSynchTheoryTimingForManualAnchorEdit(project, marker.beatPosition);
        RebuildBeatMap(project);
        FinishBeatMapEdit(project, moveContentWithBeatMap);
        return FindBeatMarker(project, beatPosition, anchorsOnly: true);
    }

    public static ChartEditorBeatMarker MoveBeatMarkerAsAnchor(ChartEditorProject project, ChartEditorBeatMarker marker, double audioTimeSeconds)
    {
        if (project == null || marker == null)
            return null;

        ChartEditorBeatMarker anchor = AddAnchorAtBeat(project, marker.beatPosition, marker.audioTimeSeconds);
        if (anchor == null || anchor.locked)
            return anchor;

        MoveAnchor(project, anchor, audioTimeSeconds);
        return FindAnchorById(project, anchor.id) ?? FindBeatMarker(project, anchor.beatPosition, anchorsOnly: true);
    }

    public static void MoveAnchor(ChartEditorProject project, ChartEditorBeatMarker anchor, double audioTimeSeconds, bool moveContentWithBeatMap = true)
    {
        if (project == null || anchor == null)
            return;

        EnsureBeatMap(project, attachContentToBeatMap: true);
        ChartEditorBeatMarker liveAnchor = FindAnchorById(project, anchor.id) ?? FindBeatMarker(project, anchor.beatPosition, anchorsOnly: true);
        if (liveAnchor == null)
            return;

        List<ChartEditorBeatMarker> anchors = GetAnchors(project);
        int index = anchors.FindIndex(candidate => string.Equals(candidate.id, liveAnchor.id, StringComparison.OrdinalIgnoreCase));
        double min = 0.0;
        double max = project.DurationSeconds;
        if (index > 0)
            min = anchors[index - 1].audioTimeSeconds + MinAnchorGapSeconds;
        if (index >= 0 && index + 1 < anchors.Count)
            max = anchors[index + 1].audioTimeSeconds - MinAnchorGapSeconds;

        liveAnchor.audioTimeSeconds = Math.Max(min, Math.Min(max, audioTimeSeconds));
        liveAnchor.isAnchor = true;
        liveAnchor.generatedBySynchTheory = false;
        liveAnchor.synchTheorySource = string.Empty;
        ClearGeneratedSynchTheoryTimingForManualAnchorEdit(project, liveAnchor.beatPosition);
        RebuildBeatMap(project);
        if (moveContentWithBeatMap)
            ApplyBeatMapToContent(project);
        else
        {
            AttachContentToBeatMap(project);
            SyncLegacySyncPointsFromBeatMap(project);
            project.dirty = true;
        }
    }

    public static void PreviewBeatMapChange(ChartEditorProject project, bool moveContentWithBeatMap = false)
    {
        if (project == null)
            return;

        if (moveContentWithBeatMap)
        {
            ApplyBeatMapToContent(project);
        }
        else
        {
            RebuildBeatMap(project);
            AttachContentToBeatMap(project);
            SyncLegacySyncPointsFromBeatMap(project);
            project.dirty = true;
        }
    }

    public static void RemoveAnchor(ChartEditorProject project, ChartEditorBeatMarker anchor)
    {
        if (project == null || anchor == null)
            return;

        EnsureBeatMap(project, attachContentToBeatMap: false);
        ChartEditorBeatMarker liveAnchor = FindAnchorById(project, anchor.id) ?? FindBeatMarker(project, anchor.beatPosition, anchorsOnly: true);
        if (liveAnchor == null)
            return;

        liveAnchor.isAnchor = false;
        liveAnchor.locked = false;
        liveAnchor.label = string.Empty;
        RebuildBeatMap(project);
        SyncLegacySyncPointsFromBeatMap(project);
        ApplyBeatMapToContent(project);
    }

    public static double GetAudioTimeForBeat(ChartEditorProject project, double beatPosition)
    {
        if (project?.beatMap == null)
            return Math.Max(0.0, beatPosition * 60.0 / DefaultTempoBpm);

        beatPosition = Math.Max(0.0, beatPosition);
        List<ChartEditorTempoRegion> regions = GetTempoRegions(project);
        if (regions.Count == 0)
            return beatPosition * 60.0 / ClampTempo(project.beatMap.defaultTempoBpm);

        ChartEditorTempoRegion first = regions[0];
        if (beatPosition <= first.startBeat)
            return Math.Max(0.0, first.startAudioTimeSeconds + ((beatPosition - first.startBeat) * 60.0 / ClampTempo(first.bpm)));

        for (int i = 0; i < regions.Count; i++)
        {
            ChartEditorTempoRegion region = regions[i];
            if (beatPosition > region.endBeat && i + 1 < regions.Count)
                continue;

            double beatSpan = Math.Max(BeatEpsilon, region.endBeat - region.startBeat);
            double t = Math.Max(0.0, Math.Min(1.0, (beatPosition - region.startBeat) / beatSpan));
            return Mathf.Lerp((float)region.startAudioTimeSeconds, (float)region.endAudioTimeSeconds, (float)t);
        }

        ChartEditorTempoRegion last = regions[regions.Count - 1];
        return Math.Max(0.0, last.endAudioTimeSeconds + ((beatPosition - last.endBeat) * 60.0 / ClampTempo(last.bpm)));
    }

    public static double GetBeatPositionForAudioTime(ChartEditorProject project, double audioTimeSeconds)
    {
        if (project?.beatMap == null)
            return Math.Max(0.0, audioTimeSeconds * DefaultTempoBpm / 60.0);

        audioTimeSeconds = Math.Max(0.0, audioTimeSeconds);
        List<ChartEditorTempoRegion> regions = GetTempoRegions(project);
        if (regions.Count == 0)
            return audioTimeSeconds * ClampTempo(project.beatMap.defaultTempoBpm) / 60.0;

        ChartEditorTempoRegion first = regions[0];
        if (audioTimeSeconds <= first.startAudioTimeSeconds)
            return Math.Max(0.0, first.startBeat + ((audioTimeSeconds - first.startAudioTimeSeconds) * ClampTempo(first.bpm) / 60.0));

        for (int i = 0; i < regions.Count; i++)
        {
            ChartEditorTempoRegion region = regions[i];
            if (audioTimeSeconds > region.endAudioTimeSeconds && i + 1 < regions.Count)
                continue;

            double timeSpan = Math.Max(MinAnchorGapSeconds, region.endAudioTimeSeconds - region.startAudioTimeSeconds);
            double t = Math.Max(0.0, Math.Min(1.0, (audioTimeSeconds - region.startAudioTimeSeconds) / timeSpan));
            return Mathf.Lerp((float)region.startBeat, (float)region.endBeat, (float)t);
        }

        ChartEditorTempoRegion last = regions[regions.Count - 1];
        return Math.Max(0.0, last.endBeat + ((audioTimeSeconds - last.endAudioTimeSeconds) * ClampTempo(last.bpm) / 60.0));
    }

    public static double GetTempoAtBeat(ChartEditorProject project, double beatPosition)
    {
        List<ChartEditorTempoRegion> regions = GetTempoRegions(project);
        if (regions.Count == 0)
            return ClampTempo(project?.beatMap?.defaultTempoBpm ?? DefaultTempoBpm);

        for (int i = 0; i < regions.Count; i++)
        {
            ChartEditorTempoRegion region = regions[i];
            if (beatPosition <= region.endBeat || i == regions.Count - 1)
                return ClampTempo(region.bpm);
        }

        return ClampTempo(regions[regions.Count - 1].bpm);
    }

    public static void MoveEntireProject(ChartEditorProject project, double deltaSeconds)
    {
        if (project == null || Math.Abs(deltaSeconds) < 0.000001)
            return;

        ShiftProjectContent(project, _ => true, deltaSeconds);
    }

    public static void MoveEverythingAfter(ChartEditorProject project, double cursorSeconds, double deltaSeconds)
    {
        if (project == null || Math.Abs(deltaSeconds) < 0.000001)
            return;

        ShiftProjectContent(project, time => time >= cursorSeconds, deltaSeconds);
    }

    public static void StretchRegion(ChartEditorProject project, double startSeconds, double endSeconds, double scale)
    {
        if (project == null || endSeconds <= startSeconds + 0.0001 || Math.Abs(scale - 1.0) < 0.000001)
            return;

        scale = Math.Max(0.05, scale);

        double Transform(double time)
        {
            if (time < startSeconds || time > endSeconds)
                return time;
            return startSeconds + ((time - startSeconds) * scale);
        }

        if (project.tracks != null)
        {
            foreach (ChartEditorTrack track in project.tracks)
            {
                if (track?.notes == null)
                    continue;

                foreach (ChartEditorNote note in track.notes)
                {
                    if (note == null || note.timeSeconds < startSeconds || note.timeSeconds > endSeconds)
                        continue;

                    double oldEnd = note.timeSeconds + Math.Max(0.0, note.durationSeconds);
                    note.timeSeconds = Math.Max(0.0, Transform(note.timeSeconds));
                    note.chartTimeSeconds = Math.Max(0.0, Transform(note.chartTimeSeconds));
                    double newEnd = Transform(oldEnd);
                    note.durationSeconds = Math.Max(0.0, newEnd - note.timeSeconds);
                    UpdateNoteBeatTiming(project, note);
                }

                track.notes.Sort((a, b) => (a?.timeSeconds ?? 0.0).CompareTo(b?.timeSeconds ?? 0.0));
            }
        }

        if (project.sections != null)
        {
            foreach (ChartEditorSection section in project.sections)
            {
                if (section == null || section.startTimeSeconds < startSeconds || section.startTimeSeconds > endSeconds)
                    continue;

                section.startTimeSeconds = Math.Max(0.0, Transform(section.startTimeSeconds));
                section.endTimeSeconds = Math.Max(section.startTimeSeconds + 0.05, Transform(section.endTimeSeconds));
                section.chartStartTimeSeconds = Math.Max(0.0, Transform(section.chartStartTimeSeconds));
                section.chartEndTimeSeconds = Math.Max(section.chartStartTimeSeconds + 0.05, Transform(section.chartEndTimeSeconds));
                section.userEdited = true;
                UpdateSectionBeatTiming(project, section);
            }

            NormalizeSections(project);
        }

        RebuildBeatMap(project);
        SyncLegacySyncPointsFromBeatMap(project);
        project.dirty = true;
    }

    public static void SetDefaultTempo(ChartEditorProject project, double bpm)
    {
        if (project == null)
            return;

        project.EnsureDefaults();
        project.beatMap.defaultTempoBpm = ClampTempo(bpm);
        ChartEditorBeatMarker origin = FindBeatMarker(project, 0.0);
        if (origin != null)
            origin.bpm = project.beatMap.defaultTempoBpm;
        RebuildBeatMap(project);
        ApplyBeatMapToContent(project);
    }

    public static bool SetTempoRegionBpmAtBeat(ChartEditorProject project, double beatPosition, double bpm)
    {
        if (project == null)
            return false;

        EnsureBeatMap(project, attachContentToBeatMap: true);
        bpm = ClampTempo(bpm);
        List<ChartEditorTempoRegion> regions = GetTempoRegions(project);
        if (regions.Count == 0)
        {
            SetDefaultTempo(project, bpm);
            return true;
        }

        ChartEditorTempoRegion region = regions
            .FirstOrDefault(candidate => candidate != null &&
                                         beatPosition >= candidate.startBeat - BeatEpsilon &&
                                         beatPosition <= candidate.endBeat + BeatEpsilon) ??
                                         regions[regions.Count - 1];
        if (region == null)
            return false;

        ChartEditorBeatMarker startMarker = FindBeatMarker(project, region.startBeat);
        ChartEditorBeatMarker startAnchor = FindBeatMarker(project, region.startBeat, anchorsOnly: true);
        ChartEditorBeatMarker endAnchor = FindBeatMarker(project, region.endBeat, anchorsOnly: true);
        if (startAnchor == null)
        {
            if (startMarker != null)
                startMarker.bpm = bpm;
            SetDefaultTempo(project, bpm);
            return true;
        }

        if (endAnchor == null || string.Equals(startAnchor.id, endAnchor.id, StringComparison.OrdinalIgnoreCase))
        {
            startAnchor.bpm = bpm;
            if (Math.Abs(startAnchor.beatPosition) <= BeatEpsilon)
                project.beatMap.defaultTempoBpm = bpm;
            RebuildBeatMap(project);
            ApplyBeatMapToContent(project);
            return true;
        }

        if (endAnchor.locked)
            return false;

        double beatSpan = Math.Max(BeatEpsilon, endAnchor.beatPosition - startAnchor.beatPosition);
        double requestedEndAudio = startAnchor.audioTimeSeconds + beatSpan * 60.0 / bpm;
        MoveAnchor(project, endAnchor, requestedEndAudio);
        return true;
    }

    public static bool CanUseTrailingTempoProbe(ChartEditorProject project, double beatPosition, bool markerIsAnchor)
    {
        if (project == null || markerIsAnchor || beatPosition <= BeatEpsilon)
            return false;

        EnsureBeatMap(project, attachContentToBeatMap: false);
        return !GetAnchors(project).Any(anchor => anchor != null && anchor.beatPosition > beatPosition + BeatEpsilon);
    }

    public static bool MoveTrailingBeatAsTempoProbe(ChartEditorProject project, double beatPosition, double audioTimeSeconds, bool moveContentWithBeatMap)
    {
        if (project == null || beatPosition <= BeatEpsilon)
            return false;

        EnsureBeatMap(project, attachContentToBeatMap: false);
        if (GetAnchors(project).Any(anchor => anchor != null && anchor.beatPosition > beatPosition + BeatEpsilon))
            return false;

        ChartEditorBeatMarker start = project.beatMap.beatMarkers
            .Where(marker => marker != null &&
                             marker.beatPosition < beatPosition - BeatEpsilon &&
                             (marker.isAnchor || Math.Abs(marker.beatPosition) <= BeatEpsilon))
            .OrderByDescending(marker => marker.beatPosition)
            .FirstOrDefault();
        if (start == null)
            return false;

        audioTimeSeconds = Math.Max(start.audioTimeSeconds + MinAnchorGapSeconds, Math.Min(project.DurationSeconds, audioTimeSeconds));
        double beatSpan = Math.Max(BeatEpsilon, beatPosition - start.beatPosition);
        double timeSpan = Math.Max(MinAnchorGapSeconds, audioTimeSeconds - start.audioTimeSeconds);
        double bpm = ClampTempo(beatSpan * 60.0 / timeSpan);

        start.bpm = bpm;
        if (Math.Abs(start.beatPosition) <= BeatEpsilon)
            project.beatMap.defaultTempoBpm = bpm;
        ClearGeneratedSynchTheoryTimingAfterBeat(project, start.beatPosition);

        if (moveContentWithBeatMap)
            ApplyBeatMapToContent(project);
        else
        {
            RebuildBeatMap(project);
            AttachContentToBeatMap(project);
            SyncLegacySyncPointsFromBeatMap(project);
            project.dirty = true;
        }

        return true;
    }

    public static void SetTimeSignatureAtBeat(ChartEditorProject project, double beatPosition, int numerator, int denominator)
    {
        if (project == null)
            return;

        EnsureBeatMap(project, attachContentToBeatMap: false);
        beatPosition = Math.Max(0.0, Math.Round(beatPosition, 4));
        numerator = Math.Max(1, numerator);
        denominator = Mathf.Clamp(denominator <= 0 ? 4 : denominator, 1, 64);

        project.beatMap.timeSignatures.RemoveAll(change => change == null || Math.Abs(change.beatPosition - beatPosition) <= BeatEpsilon);
        project.beatMap.timeSignatures.Add(new ChartEditorTimeSignatureChange
        {
            beatPosition = beatPosition,
            numerator = numerator,
            denominator = denominator
        });
        project.beatMap.timeSignatures = project.beatMap.timeSignatures
            .Where(change => change != null)
            .OrderBy(change => change.beatPosition)
            .ToList();

        RebuildBeatMap(project);
        SyncLegacySyncPointsFromBeatMap(project);
        project.dirty = true;
    }

    public static ChartEditorBeatMarker GetNearestBeatMarker(ChartEditorProject project, double audioTimeSeconds)
    {
        EnsureBeatMap(project, attachContentToBeatMap: false);
        return project?.beatMap?.beatMarkers?
            .Where(marker => marker != null)
            .OrderBy(marker => Math.Abs(marker.audioTimeSeconds - audioTimeSeconds))
            .FirstOrDefault();
    }

    public static ChartEditorTimeSignatureChange GetTimeSignatureAtBeat(ChartEditorProject project, double beatPosition)
    {
        EnsureBeatMap(project, attachContentToBeatMap: false);
        return ResolveTimeSignature(project?.beatMap, beatPosition);
    }

    public static void NormalizeSections(ChartEditorProject project)
    {
        if (project?.sections == null)
            return;

        project.sections = project.sections
            .Where(section => section != null)
            .OrderBy(section => section.startTimeSeconds)
            .ToList();

        double duration = Math.Max(0.1, project.DurationSeconds);
        for (int i = 0; i < project.sections.Count; i++)
        {
            ChartEditorSection section = project.sections[i];
            double nextStart = i + 1 < project.sections.Count
                ? project.sections[i + 1].startTimeSeconds
                : Math.Max(duration, section.endTimeSeconds);
            section.endTimeSeconds = Math.Max(section.startTimeSeconds + 0.05, Math.Max(section.endTimeSeconds, nextStart));
            section.chartEndTimeSeconds = Math.Max(section.chartStartTimeSeconds + 0.05, section.chartEndTimeSeconds);
        }
    }

    public static double MapChartTimeToAudioTime(double chartTimeSeconds, List<ChartEditorSyncPoint> points)
    {
        if (points == null || points.Count == 0)
            return Math.Max(0.0, chartTimeSeconds);

        if (points.Count == 1)
            return Math.Max(0.0, chartTimeSeconds + (points[0].audioTimeSeconds - points[0].chartTimeSeconds));

        chartTimeSeconds = Math.Max(0.0, chartTimeSeconds);
        List<ChartEditorSyncPoint> ordered = points
            .Where(point => point != null)
            .OrderBy(point => point.chartTimeSeconds)
            .ToList();
        if (ordered.Count == 0)
            return chartTimeSeconds;
        if (chartTimeSeconds <= ordered[0].chartTimeSeconds)
            return Math.Max(0.0, chartTimeSeconds + (ordered[0].audioTimeSeconds - ordered[0].chartTimeSeconds));

        for (int i = 0; i < ordered.Count - 1; i++)
        {
            ChartEditorSyncPoint a = ordered[i];
            ChartEditorSyncPoint b = ordered[i + 1];
            if (chartTimeSeconds > b.chartTimeSeconds)
                continue;

            double range = Math.Max(0.000001, b.chartTimeSeconds - a.chartTimeSeconds);
            double t = Mathf.Clamp01((float)((chartTimeSeconds - a.chartTimeSeconds) / range));
            return Mathf.Lerp((float)a.audioTimeSeconds, (float)b.audioTimeSeconds, (float)t);
        }

        ChartEditorSyncPoint last = ordered[ordered.Count - 1];
        return Math.Max(0.0, chartTimeSeconds + (last.audioTimeSeconds - last.chartTimeSeconds));
    }

    public static List<ChartEditorBeatMarker> GetBeatMarkers(ChartEditorProject project)
    {
        EnsureBeatMap(project, attachContentToBeatMap: false);
        return project?.beatMap?.beatMarkers?
            .Where(marker => marker != null)
            .OrderBy(marker => marker.beatPosition)
            .ToList() ?? new List<ChartEditorBeatMarker>();
    }

    public static List<ChartEditorBeatMarker> GetAnchors(ChartEditorProject project)
    {
        return project?.beatMap?.beatMarkers?
            .Where(marker => marker != null && marker.isAnchor)
            .OrderBy(marker => marker.beatPosition)
            .ToList() ?? new List<ChartEditorBeatMarker>();
    }

    public static List<ChartEditorTempoRegion> GetTempoRegions(ChartEditorProject project)
    {
        return project?.beatMap?.tempoRegions?
            .Where(region => region != null)
            .OrderBy(region => region.startBeat)
            .ToList() ?? new List<ChartEditorTempoRegion>();
    }

    public static void SyncLegacySyncPointsFromBeatMap(ChartEditorProject project)
    {
        if (project?.beatMap == null)
            return;

        List<ChartEditorBeatMarker> anchors = GetAnchors(project);
        project.syncPoints = anchors.Select((anchor, index) => new ChartEditorSyncPoint
        {
            id = anchor.id,
            name = string.IsNullOrWhiteSpace(anchor.label) ? (index == 0 ? "Start" : $"Anchor {index + 1}") : anchor.label,
            chartTimeSeconds = GetAudioTimeForBeat(project, anchor.beatPosition),
            audioTimeSeconds = anchor.audioTimeSeconds,
            linkedSectionId = anchor.linkedSectionId,
            locked = anchor.locked
        }).ToList();
    }

    private static void SeedAnchors(ChartEditorProject project)
    {
        project.beatMap.beatMarkers.Clear();
        double defaultBpm = ClampTempo(project.beatMap.defaultTempoBpm);
        double beatSeconds = 60.0 / defaultBpm;

        List<ChartEditorSyncPoint> legacyPoints = project.syncPoints?
            .Where(point => point != null)
            .OrderBy(point => point.chartTimeSeconds)
            .ToList() ?? new List<ChartEditorSyncPoint>();

        if (legacyPoints.Count > 0)
        {
            for (int i = 0; i < legacyPoints.Count; i++)
            {
                ChartEditorSyncPoint point = legacyPoints[i];
                project.beatMap.beatMarkers.Add(new ChartEditorBeatMarker
                {
                    id = string.IsNullOrWhiteSpace(point.id) ? Guid.NewGuid().ToString("N") : point.id,
                    beatPosition = Math.Max(0.0, point.chartTimeSeconds / beatSeconds),
                    audioTimeSeconds = Math.Max(0.0, point.audioTimeSeconds),
                    isAnchor = true,
                    locked = point.locked,
                    label = string.IsNullOrWhiteSpace(point.name) ? (i == 0 ? "Start" : $"Anchor {i + 1}") : point.name,
                    linkedSectionId = point.linkedSectionId
                });
            }
            return;
        }

        project.beatMap.beatMarkers.Add(new ChartEditorBeatMarker
        {
            id = StableBeatMapId("beat", 0.0),
            beatPosition = 0.0,
            audioTimeSeconds = 0.0,
            isAnchor = false,
            label = string.Empty,
            bpm = defaultBpm
        });
    }

    private static void RebuildBeatMap(ChartEditorProject project)
    {
        if (project?.beatMap == null)
            return;

        ChartEditorBeatMap beatMap = project.beatMap;
        beatMap.EnsureDefaults();
        List<ChartEditorBeatMarker> anchors = beatMap.beatMarkers
            .Where(marker => marker != null && marker.isAnchor)
            .OrderBy(marker => marker.beatPosition)
            .ToList();

        ChartEditorBeatMarker origin = beatMap.beatMarkers
            .Where(marker => marker != null && Math.Abs(marker.beatPosition) <= BeatEpsilon)
            .OrderByDescending(marker => marker.isAnchor)
            .ThenByDescending(marker => marker.generatedBySynchTheory)
            .FirstOrDefault();

        if (origin == null)
        {
            double originAudio = 0.0;
            if (anchors.Count > 0)
                originAudio = Math.Max(0.0, anchors[0].audioTimeSeconds - anchors[0].beatPosition * 60.0 / ClampTempo(beatMap.defaultTempoBpm));

            origin = new ChartEditorBeatMarker
            {
                id = StableBeatMapId("beat", 0.0),
                beatPosition = 0.0,
                audioTimeSeconds = originAudio,
                isAnchor = false,
                label = string.Empty,
                bpm = ClampTempo(beatMap.defaultTempoBpm)
            };
        }

        List<ChartEditorBeatMarker> timingControls = beatMap.beatMarkers
            .Where(marker => marker != null &&
                             (marker.isAnchor || marker.generatedBySynchTheory) &&
                             Math.Abs(marker.beatPosition) > BeatEpsilon)
            .OrderBy(marker => marker.beatPosition)
            .ToList();
        timingControls.Insert(0, origin);

        beatMap.tempoRegions = BuildTempoRegions(project, timingControls);
        double maxBeat = ResolveRequiredBeatCount(project, timingControls, beatMap.tempoRegions);
        int markerCount = Mathf.Max(1, Mathf.CeilToInt((float)maxBeat) + 2);

        Dictionary<string, ChartEditorBeatMarker> anchorByRoundedBeat = timingControls
            .GroupBy(anchor => BeatKey(anchor.beatPosition), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        List<ChartEditorBeatMarker> rebuilt = new List<ChartEditorBeatMarker>(markerCount);
        for (int i = 0; i < markerCount; i++)
        {
            double beat = i;
            string key = BeatKey(beat);
            ChartEditorBeatMarker marker = anchorByRoundedBeat.TryGetValue(key, out ChartEditorBeatMarker anchor)
                ? anchor
                : new ChartEditorBeatMarker { id = StableBeatMapId("beat", beat) };

            marker.index = i;
            marker.beatPosition = beat;
            if (!marker.isAnchor && !marker.generatedBySynchTheory)
                marker.audioTimeSeconds = GetAudioTimeForBeatInternal(beatMap.tempoRegions, beat, beatMap.defaultTempoBpm);
            ApplyBarInfo(beatMap, marker);
            rebuilt.Add(marker);
        }

        foreach (ChartEditorBeatMarker anchor in timingControls)
        {
            if (Math.Abs(anchor.beatPosition - Math.Round(anchor.beatPosition)) <= BeatEpsilon)
                continue;

            anchor.index = Mathf.RoundToInt((float)anchor.beatPosition);
            anchor.audioTimeSeconds = Math.Max(0.0, anchor.audioTimeSeconds);
            ApplyBarInfo(beatMap, anchor);
            rebuilt.Add(anchor);
        }

        beatMap.beatMarkers = rebuilt
            .Where(marker => marker != null && marker.audioTimeSeconds <= project.DurationSeconds + 4.0)
            .OrderBy(marker => marker.beatPosition)
            .ThenBy(marker => marker.audioTimeSeconds)
            .ToList();
    }

    public static int ClearGeneratedSynchTheoryTiming(ChartEditorProject project, double? startBeat = null, double? endBeat = null)
    {
        if (project?.beatMap?.beatMarkers == null)
            return 0;

        double minBeat = startBeat ?? double.NegativeInfinity;
        double maxBeat = endBeat ?? double.PositiveInfinity;
        int removed = project.beatMap.beatMarkers.RemoveAll(marker =>
            marker != null &&
            marker.generatedBySynchTheory &&
            !marker.isAnchor &&
            marker.beatPosition >= minBeat - BeatEpsilon &&
            marker.beatPosition <= maxBeat + BeatEpsilon);

        if (removed > 0)
        {
            RebuildBeatMap(project);
            ApplyBeatMapToContent(project);
        }

        return removed;
    }

    private static int ClearGeneratedSynchTheoryTimingForManualAnchorEdit(ChartEditorProject project, double beatPosition)
    {
        if (project?.beatMap?.beatMarkers == null)
            return 0;

        beatPosition = Math.Max(0.0, beatPosition);
        List<ChartEditorBeatMarker> surroundingAnchors = project.beatMap.beatMarkers
            .Where(marker => marker != null &&
                             marker.isAnchor &&
                             Math.Abs(marker.beatPosition - beatPosition) > BeatEpsilon)
            .OrderBy(marker => marker.beatPosition)
            .ToList();

        ChartEditorBeatMarker previousAnchor = surroundingAnchors
            .Where(marker => marker.beatPosition < beatPosition - BeatEpsilon)
            .LastOrDefault();
        ChartEditorBeatMarker nextAnchor = surroundingAnchors
            .FirstOrDefault(marker => marker.beatPosition > beatPosition + BeatEpsilon);

        double lowerBeat = previousAnchor != null ? previousAnchor.beatPosition : 0.0;
        double upperBeat = nextAnchor != null ? nextAnchor.beatPosition : double.PositiveInfinity;

        return project.beatMap.beatMarkers.RemoveAll(marker =>
            marker != null &&
            marker.generatedBySynchTheory &&
            !marker.isAnchor &&
            marker.beatPosition > lowerBeat + BeatEpsilon &&
            marker.beatPosition < upperBeat - BeatEpsilon);
    }

    private static int ClearGeneratedSynchTheoryTimingAfterBeat(ChartEditorProject project, double beatPosition)
    {
        if (project?.beatMap?.beatMarkers == null)
            return 0;

        double lowerBeat = Math.Max(0.0, beatPosition);
        return project.beatMap.beatMarkers.RemoveAll(marker =>
            marker != null &&
            marker.generatedBySynchTheory &&
            !marker.isAnchor &&
            marker.beatPosition > lowerBeat + BeatEpsilon);
    }

    private static void FinishBeatMapEdit(ChartEditorProject project, bool moveContentWithBeatMap)
    {
        if (moveContentWithBeatMap)
        {
            ApplyBeatMapToContent(project);
        }
        else
        {
            AttachContentToBeatMap(project);
            SyncLegacySyncPointsFromBeatMap(project);
            project.dirty = true;
        }
    }

    private static List<ChartEditorTempoRegion> BuildTempoRegions(ChartEditorProject project, List<ChartEditorBeatMarker> anchors)
    {
        List<ChartEditorTempoRegion> regions = new List<ChartEditorTempoRegion>();
        double fallbackBpm = ClampTempo(project.beatMap.defaultTempoBpm);
        for (int i = 0; i < anchors.Count; i++)
        {
            ChartEditorBeatMarker start = anchors[i];
            ChartEditorBeatMarker end = i + 1 < anchors.Count ? anchors[i + 1] : null;
            double startBeat = Math.Max(0.0, start.beatPosition);
            double startAudio = Math.Max(0.0, start.audioTimeSeconds);
            double endBeat;
            double endAudio;
            double bpm;
            double explicitBpm = start.bpm > 0.001 ? ClampTempo(start.bpm) : 0.0;

            if (end != null && end.beatPosition > startBeat + BeatEpsilon && end.audioTimeSeconds > startAudio + MinAnchorGapSeconds)
            {
                endBeat = end.beatPosition;
                endAudio = end.audioTimeSeconds;
                bpm = ClampTempo((endBeat - startBeat) * 60.0 / Math.Max(MinAnchorGapSeconds, endAudio - startAudio));
            }
            else
            {
                bpm = explicitBpm > 0.0
                    ? explicitBpm
                    : i > 0 && regions.Count > 0
                        ? ClampTempo(regions[regions.Count - 1].bpm)
                        : fallbackBpm;
                endAudio = Math.Max(project.DurationSeconds, startAudio + 60.0 / bpm);
                endBeat = startBeat + Math.Max(1.0, (endAudio - startAudio) * bpm / 60.0);
            }

            regions.Add(new ChartEditorTempoRegion
            {
                id = StableBeatMapId("tempo", startBeat, endBeat),
                startBeat = startBeat,
                endBeat = Math.Max(startBeat + BeatEpsilon, endBeat),
                startAudioTimeSeconds = startAudio,
                endAudioTimeSeconds = Math.Max(startAudio + MinAnchorGapSeconds, endAudio),
                bpm = bpm
            });
        }

        return regions;
    }

    private static double ResolveRequiredBeatCount(ChartEditorProject project, List<ChartEditorBeatMarker> anchors, List<ChartEditorTempoRegion> regions)
    {
        double maxBeat = Math.Max(4.0, GetBeatPositionForAudioTimeInternal(regions, project.DurationSeconds, project.beatMap.defaultTempoBpm));
        foreach (ChartEditorBeatMarker anchor in anchors)
            maxBeat = Math.Max(maxBeat, anchor.beatPosition);

        if (project.tracks != null)
        {
            foreach (ChartEditorTrack track in project.tracks)
            {
                if (track?.notes == null)
                    continue;

                foreach (ChartEditorNote note in track.notes)
                {
                    if (note == null)
                        continue;

                    maxBeat = Math.Max(maxBeat, note.beatPosition + Math.Max(0.0, note.durationBeats));
                }
            }
        }

        if (project.sections != null)
        {
            foreach (ChartEditorSection section in project.sections)
            {
                if (section == null)
                    continue;

                maxBeat = Math.Max(maxBeat, section.endBeatPosition);
            }
        }

        return maxBeat;
    }

    private static void ApplyBarInfo(ChartEditorBeatMap beatMap, ChartEditorBeatMarker marker)
    {
        if (marker == null)
            return;

        List<ChartEditorTimeSignatureChange> changes = beatMap?.timeSignatures?
            .Where(change => change != null)
            .OrderBy(change => change.beatPosition)
            .ToList() ?? new List<ChartEditorTimeSignatureChange>();
        if (changes.Count == 0 || changes[0].beatPosition > BeatEpsilon)
            changes.Insert(0, new ChartEditorTimeSignatureChange { beatPosition = 0.0, numerator = 4, denominator = 4 });

        int barNumber = 1;
        ChartEditorTimeSignatureChange active = changes[0];
        double segmentStartBeat = Math.Max(0.0, active.beatPosition);

        for (int i = 1; i < changes.Count && changes[i].beatPosition <= marker.beatPosition + BeatEpsilon; i++)
        {
            double measureLength = GetMeasureLengthInQuarterBeats(active);
            if (measureLength > BeatEpsilon)
                barNumber += Mathf.Max(0, Mathf.FloorToInt((float)((changes[i].beatPosition - segmentStartBeat) / measureLength)));

            active = changes[i];
            segmentStartBeat = Math.Max(0.0, active.beatPosition);
        }

        double activeMeasureLength = GetMeasureLengthInQuarterBeats(active);
        double beatUnit = GetBeatUnitInQuarterBeats(active);
        double offset = Math.Max(0.0, marker.beatPosition - segmentStartBeat);
        int barsIntoSegment = activeMeasureLength > BeatEpsilon ? Mathf.Max(0, Mathf.FloorToInt((float)(offset / activeMeasureLength))) : 0;
        double beatRemainder = activeMeasureLength > BeatEpsilon ? offset - barsIntoSegment * activeMeasureLength : 0.0;

        marker.barNumber = Math.Max(1, barNumber + barsIntoSegment);
        marker.beatInBar = Mathf.Clamp(Mathf.FloorToInt((float)(beatRemainder / Math.Max(BeatEpsilon, beatUnit))) + 1, 1, Math.Max(1, active.numerator));
        marker.isDownbeat = beatRemainder <= BeatEpsilon || activeMeasureLength - beatRemainder <= BeatEpsilon;
    }

    private static ChartEditorTimeSignatureChange ResolveTimeSignature(ChartEditorBeatMap beatMap, double beatPosition)
    {
        ChartEditorTimeSignatureChange resolved = beatMap?.timeSignatures?
            .Where(change => change != null)
            .OrderBy(change => change.beatPosition)
            .LastOrDefault(change => change.beatPosition <= beatPosition + BeatEpsilon);
        if (resolved != null)
            return resolved;

        return new ChartEditorTimeSignatureChange { beatPosition = 0.0, numerator = 4, denominator = 4 };
    }

    private static double GetMeasureLengthInQuarterBeats(ChartEditorTimeSignatureChange signature)
    {
        signature ??= new ChartEditorTimeSignatureChange { numerator = 4, denominator = 4 };
        return Math.Max(BeatEpsilon, Math.Max(1, signature.numerator) * GetBeatUnitInQuarterBeats(signature));
    }

    private static double GetBeatUnitInQuarterBeats(ChartEditorTimeSignatureChange signature)
    {
        signature ??= new ChartEditorTimeSignatureChange { numerator = 4, denominator = 4 };
        return 4.0 / Math.Max(1, signature.denominator);
    }

    private static ChartEditorBeatMarker FindBeatMarker(ChartEditorProject project, double beatPosition, bool anchorsOnly = false)
    {
        string key = BeatKey(beatPosition);
        return project?.beatMap?.beatMarkers?
            .FirstOrDefault(marker => marker != null &&
                                      (!anchorsOnly || marker.isAnchor) &&
                                      string.Equals(BeatKey(marker.beatPosition), key, StringComparison.OrdinalIgnoreCase));
    }

    private static ChartEditorBeatMarker FindAnchorById(ChartEditorProject project, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return project?.beatMap?.beatMarkers?
            .FirstOrDefault(marker => marker != null &&
                                      marker.isAnchor &&
                                      string.Equals(marker.id, id, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountAnchors(ChartEditorProject project)
    {
        return project?.beatMap?.beatMarkers?.Count(marker => marker != null && marker.isAnchor) ?? 0;
    }

    private static string FormatAnchorLabel(ChartEditorProject project, ChartEditorBeatMarker marker)
    {
        int count = Math.Max(1, CountAnchors(project));
        if (marker != null && Math.Abs(marker.beatPosition) <= BeatEpsilon)
            return "Start";

        return $"Anchor {count}";
    }

    private static void ShiftProjectContent(ChartEditorProject project, Func<double, bool> predicate, double deltaSeconds)
    {
        if (project.tracks != null)
        {
            foreach (ChartEditorTrack track in project.tracks)
            {
                if (track?.notes == null)
                    continue;

                foreach (ChartEditorNote note in track.notes)
                {
                    if (note == null || !predicate(note.timeSeconds))
                        continue;

                    note.timeSeconds = Math.Max(0.0, note.timeSeconds + deltaSeconds);
                    note.chartTimeSeconds = Math.Max(0.0, note.chartTimeSeconds + deltaSeconds);
                    UpdateNoteBeatTiming(project, note);
                }

                track.notes.Sort((a, b) => (a?.timeSeconds ?? 0.0).CompareTo(b?.timeSeconds ?? 0.0));
            }
        }

        if (project.sections != null)
        {
            foreach (ChartEditorSection section in project.sections)
            {
                if (section == null || !predicate(section.startTimeSeconds))
                    continue;

                section.startTimeSeconds = Math.Max(0.0, section.startTimeSeconds + deltaSeconds);
                section.endTimeSeconds = Math.Max(section.startTimeSeconds + 0.05, section.endTimeSeconds + deltaSeconds);
                section.chartStartTimeSeconds = Math.Max(0.0, section.chartStartTimeSeconds + deltaSeconds);
                section.chartEndTimeSeconds = Math.Max(section.chartStartTimeSeconds + 0.05, section.chartEndTimeSeconds + deltaSeconds);
                section.userEdited = true;
                UpdateSectionBeatTiming(project, section);
            }

            NormalizeSections(project);
        }

        if (project.beatMap?.beatMarkers != null)
        {
            foreach (ChartEditorBeatMarker anchor in project.beatMap.beatMarkers.Where(marker => marker != null && marker.isAnchor))
            {
                if (anchor.locked || !predicate(anchor.audioTimeSeconds))
                    continue;

                anchor.audioTimeSeconds = Math.Max(0.0, anchor.audioTimeSeconds + deltaSeconds);
            }
        }

        project.cursorTimeSeconds = Math.Max(0.0, project.cursorTimeSeconds + deltaSeconds);
        RebuildBeatMap(project);
        SyncLegacySyncPointsFromBeatMap(project);
        project.dirty = true;
    }

    private static double GetAudioTimeForBeatInternal(List<ChartEditorTempoRegion> regions, double beatPosition, double defaultBpm)
    {
        if (regions == null || regions.Count == 0)
            return Math.Max(0.0, beatPosition * 60.0 / ClampTempo(defaultBpm));

        beatPosition = Math.Max(0.0, beatPosition);
        ChartEditorTempoRegion first = regions[0];
        if (beatPosition <= first.startBeat)
            return Math.Max(0.0, first.startAudioTimeSeconds + ((beatPosition - first.startBeat) * 60.0 / ClampTempo(first.bpm)));

        for (int i = 0; i < regions.Count; i++)
        {
            ChartEditorTempoRegion region = regions[i];
            if (beatPosition > region.endBeat && i + 1 < regions.Count)
                continue;

            double beatSpan = Math.Max(BeatEpsilon, region.endBeat - region.startBeat);
            double t = Math.Max(0.0, Math.Min(1.0, (beatPosition - region.startBeat) / beatSpan));
            return Mathf.Lerp((float)region.startAudioTimeSeconds, (float)region.endAudioTimeSeconds, (float)t);
        }

        ChartEditorTempoRegion last = regions[regions.Count - 1];
        return Math.Max(0.0, last.endAudioTimeSeconds + ((beatPosition - last.endBeat) * 60.0 / ClampTempo(last.bpm)));
    }

    private static double GetBeatPositionForAudioTimeInternal(List<ChartEditorTempoRegion> regions, double audioTimeSeconds, double defaultBpm)
    {
        if (regions == null || regions.Count == 0)
            return Math.Max(0.0, audioTimeSeconds * ClampTempo(defaultBpm) / 60.0);

        audioTimeSeconds = Math.Max(0.0, audioTimeSeconds);
        ChartEditorTempoRegion first = regions[0];
        if (audioTimeSeconds <= first.startAudioTimeSeconds)
            return Math.Max(0.0, first.startBeat + ((audioTimeSeconds - first.startAudioTimeSeconds) * ClampTempo(first.bpm) / 60.0));

        for (int i = 0; i < regions.Count; i++)
        {
            ChartEditorTempoRegion region = regions[i];
            if (audioTimeSeconds > region.endAudioTimeSeconds && i + 1 < regions.Count)
                continue;

            double timeSpan = Math.Max(MinAnchorGapSeconds, region.endAudioTimeSeconds - region.startAudioTimeSeconds);
            double t = Math.Max(0.0, Math.Min(1.0, (audioTimeSeconds - region.startAudioTimeSeconds) / timeSpan));
            return Mathf.Lerp((float)region.startBeat, (float)region.endBeat, (float)t);
        }

        ChartEditorTempoRegion last = regions[regions.Count - 1];
        return Math.Max(0.0, last.endBeat + ((audioTimeSeconds - last.endAudioTimeSeconds) * ClampTempo(last.bpm) / 60.0));
    }

    private static double ClampTempo(double bpm)
    {
        return Math.Max(MinTempoBpm, Math.Min(MaxTempoBpm, bpm));
    }

    private static string BeatKey(double beatPosition)
    {
        return Math.Round(beatPosition, 4).ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string StableBeatMapId(string prefix, params double[] beats)
    {
        string[] keys = beats?
            .Select(beat => BeatKey(beat).Replace(".", "_"))
            .ToArray() ?? Array.Empty<string>();
        return prefix + "_" + string.Join("_", keys);
    }
}
