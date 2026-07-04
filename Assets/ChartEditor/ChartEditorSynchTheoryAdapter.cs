using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SynchTheory;
using UnityEngine;

public static class ChartEditorSynchTheoryAdapter
{
    private const double BeatEpsilon = 0.0001;

    public static bool TryBuildAudioData(AudioClip clip, out SynchTheoryAudioData audio, out string error)
    {
        audio = null;
        error = string.Empty;
        if (clip == null)
        {
            error = "Audio is not loaded yet.";
            return false;
        }

        if (clip.samples <= 0 || clip.frequency <= 0 || clip.channels <= 0)
        {
            error = "Audio clip has no readable sample data.";
            return false;
        }

        int channels = Mathf.Max(1, clip.channels);
        int totalFrames = Mathf.Max(1, clip.samples);
        float[] mono = new float[totalFrames];
        int chunkFrames = Mathf.Max(1024, Mathf.Min(totalFrames, clip.frequency * 8));
        for (int offsetFrame = 0; offsetFrame < totalFrames; offsetFrame += chunkFrames)
        {
            int frames = Mathf.Min(chunkFrames, totalFrames - offsetFrame);
            float[] buffer = new float[frames * channels];
            if (!clip.GetData(buffer, offsetFrame))
            {
                error = "Audio clip samples could not be read.";
                return false;
            }

            for (int frame = 0; frame < frames; frame++)
            {
                double sum = 0.0;
                int baseIndex = frame * channels;
                for (int channel = 0; channel < channels; channel++)
                    sum += buffer[baseIndex + channel];
                mono[offsetFrame + frame] = (float)(sum / channels);
            }
        }

        audio = new SynchTheoryAudioData
        {
            monoSamples = mono,
            sampleRate = clip.frequency
        };
        return true;
    }

    public static SynchTheoryScoreMap BuildScoreMap(ChartEditorProject project)
    {
        if (project == null)
            return null;

        project.EnsureDefaults();
        ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: false);
        List<ChartEditorBeatMarker> beatMarkers = ChartEditorTimingService.GetBeatMarkers(project);
        List<ChartEditorBeatMarker> anchors = ChartEditorTimingService.GetAnchors(project);

        SynchTheoryScoreMap score = new SynchTheoryScoreMap
        {
            durationSeconds = project.DurationSeconds,
            defaultTempoBpm = Math.Max(1.0, project.beatMap?.defaultTempoBpm ?? 120.0),
            beats = new List<SynchTheoryBeat>(),
            events = new List<SynchTheoryScoreEvent>(),
            anchors = new List<SynchTheoryAnchor>()
        };

        for (int i = 0; i < beatMarkers.Count; i++)
        {
            ChartEditorBeatMarker marker = beatMarkers[i];
            if (marker == null)
                continue;

            score.beats.Add(new SynchTheoryBeat
            {
                index = marker.index,
                beatPosition = marker.beatPosition,
                chartTimeSeconds = marker.audioTimeSeconds,
                audioTimeSeconds = marker.audioTimeSeconds,
                isDownbeat = marker.isDownbeat,
                isAnchor = marker.isAnchor,
                isGenerated = marker.generatedBySynchTheory,
                confidence = marker.synchTheoryConfidence
            });
        }

        for (int i = 0; i < anchors.Count; i++)
        {
            ChartEditorBeatMarker anchor = anchors[i];
            if (anchor == null)
                continue;

            score.anchors.Add(new SynchTheoryAnchor
            {
                id = anchor.id,
                beatPosition = anchor.beatPosition,
                audioTimeSeconds = anchor.audioTimeSeconds,
                locked = anchor.locked,
                label = anchor.label
            });
        }

        AddTrackEvents(project, score);
        return score;
    }

    public static int ApplyResult(ChartEditorProject project, SynchTheoryAlignmentResult result, bool moveContentWithBeatMap, out string summary)
    {
        summary = string.Empty;
        if (project == null || result == null || !result.success)
        {
            summary = result?.message ?? "SynchTheory result is empty.";
            return 0;
        }

        project.EnsureDefaults();
        ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: false);
        double startBeat = Math.Min(result.startBeat, result.endBeat);
        double endBeat = Math.Max(result.startBeat, result.endBeat);
        ChartEditorTimingService.ClearGeneratedSynchTheoryTiming(project, startBeat, endBeat);

        int applied = 0;
        List<ChartEditorBeatMarker> markers = project.beatMap.beatMarkers;
        foreach (SynchTheoryBeat beat in result.generatedBeats
                     .Where(beat => beat != null)
                     .OrderBy(beat => beat.beatPosition))
        {
            if (beat.beatPosition < startBeat - BeatEpsilon || beat.beatPosition > endBeat + BeatEpsilon)
                continue;

            ChartEditorBeatMarker manualAnchor = markers.FirstOrDefault(marker =>
                marker != null &&
                marker.isAnchor &&
                Math.Abs(marker.beatPosition - beat.beatPosition) <= BeatEpsilon);
            if (manualAnchor != null)
                continue;

            ChartEditorBeatMarker marker = markers.FirstOrDefault(candidate =>
                candidate != null &&
                !candidate.isAnchor &&
                Math.Abs(candidate.beatPosition - beat.beatPosition) <= BeatEpsilon);

            if (marker == null)
            {
                marker = new ChartEditorBeatMarker
                {
                    id = BuildGeneratedMarkerId(beat.beatPosition),
                    beatPosition = beat.beatPosition
                };
                markers.Add(marker);
            }

            marker.audioTimeSeconds = Math.Max(0.0, Math.Min(project.DurationSeconds, beat.audioTimeSeconds));
            marker.generatedBySynchTheory = true;
            marker.synchTheoryConfidence = Math.Max(0.0, Math.Min(1.0, beat.confidence));
            marker.synchTheorySource = "SynchTheory";
            marker.isAnchor = false;
            marker.locked = false;
            marker.label = string.Empty;
            applied++;
        }

        project.beatMap.beatMarkers = markers
            .Where(marker => marker != null)
            .OrderBy(marker => marker.beatPosition)
            .ThenBy(marker => marker.audioTimeSeconds)
            .ToList();

        // The generated markers were written directly into beatMap.beatMarkers;
        // without this, ApplyBeatMapToContent's EnsureBeatMap can hit the
        // same-frame cache and remap content against the pre-sync tempo
        // regions, silently discarding the alignment.
        ChartEditorTimingService.InvalidateBeatMapCache(project);
        if (moveContentWithBeatMap)
            ChartEditorTimingService.ApplyBeatMapToContent(project);
        else
            ChartEditorTimingService.PreviewBeatMapChange(project, moveContentWithBeatMap: false);

        project.dirty = true;
        summary = $"{applied} generated beat timings applied. Confidence {result.confidence.ToString("P0", CultureInfo.InvariantCulture)}.";
        return applied;
    }

    private static void AddTrackEvents(ChartEditorProject project, SynchTheoryScoreMap score)
    {
        if (project?.tracks == null)
            return;

        Dictionary<string, SynchTheoryScoreEvent> merged = new Dictionary<string, SynchTheoryScoreEvent>(StringComparer.Ordinal);
        for (int trackIndex = 0; trackIndex < project.tracks.Count; trackIndex++)
        {
            ChartEditorTrack track = project.tracks[trackIndex];
            if (track?.notes == null || track.notes.Count == 0)
                continue;

            double roleWeight = GetTrackRoleWeight(track.role);
            bool percussive = track.role == ChartEditorTrackRole.Drums;
            int[] stringPitches = track.tuning?.stringPitches;
            foreach (ChartEditorNote note in track.notes)
            {
                if (note == null)
                    continue;

                double beatPosition = note.usesBeatMapTiming
                    ? note.beatPosition
                    : ChartEditorTimingService.GetBeatPositionForAudioTime(project, note.timeSeconds);
                double chartTime = note.usesBeatMapTiming
                    ? ChartEditorTimingService.GetAudioTimeForBeat(project, beatPosition)
                    : note.timeSeconds;
                double duration = Math.Max(0.0, note.durationSeconds);
                double weight = roleWeight;
                if (!string.IsNullOrWhiteSpace(note.chordName) || note.chordId >= 0)
                    weight *= 1.25;
                if (note.accent)
                    weight *= 1.15;
                if (note.muted || note.palmMute || note.fretHandMute)
                    weight *= 0.85;

                string key = Math.Round(chartTime, 2).ToString("0.00", CultureInfo.InvariantCulture);
                if (!merged.TryGetValue(key, out SynchTheoryScoreEvent evt))
                {
                    evt = new SynchTheoryScoreEvent
                    {
                        id = note.id,
                        beatPosition = beatPosition,
                        chartTimeSeconds = chartTime,
                        durationSeconds = duration,
                        weight = weight,
                        pitchClass = ResolvePitchClass(stringPitches, note),
                        isPercussive = percussive
                    };
                    merged[key] = evt;
                }
                else
                {
                    evt.weight = Math.Min(3.0, evt.weight + weight * 0.55);
                    evt.durationSeconds = Math.Max(evt.durationSeconds, duration);
                    evt.beatPosition = Math.Min(evt.beatPosition, beatPosition);
                    if (evt.pitchClass < 0)
                        evt.pitchClass = ResolvePitchClass(stringPitches, note);
                    evt.isPercussive |= percussive;
                }
            }
        }

        score.events.AddRange(merged.Values.OrderBy(evt => evt.chartTimeSeconds));
    }

    private static double GetTrackRoleWeight(ChartEditorTrackRole role)
    {
        return role switch
        {
            ChartEditorTrackRole.LeadGuitar => 1.0,
            ChartEditorTrackRole.RhythmGuitar => 0.95,
            ChartEditorTrackRole.Bass => 0.82,
            ChartEditorTrackRole.Drums => 1.15,
            ChartEditorTrackRole.Piano => 0.70,
            ChartEditorTrackRole.Vocals => 0.50,
            _ => 0.75
        };
    }

    private static int ResolvePitchClass(int[] stringPitches, ChartEditorNote note)
    {
        if (note == null || stringPitches == null || stringPitches.Length == 0)
            return -1;

        int stringIndex = Mathf.Clamp(note.stringOrLane, 0, stringPitches.Length - 1);
        int midi = stringPitches[stringIndex] + Math.Max(0, note.fret);
        int pitchClass = midi % 12;
        return pitchClass < 0 ? pitchClass + 12 : pitchClass;
    }

    private static string BuildGeneratedMarkerId(double beatPosition)
    {
        return "synchtheory_" + Math.Round(beatPosition, 4).ToString("0.####", CultureInfo.InvariantCulture).Replace(".", "_");
    }
}
