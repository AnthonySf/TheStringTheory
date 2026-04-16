using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Gp5BandPlaybackLoader
{
    private sealed class TempoEvent
    {
        public double quarterPos;
        public double bpm;
    }

    public static GeneratedPlaybackArrangement LoadArrangement(string filePath)
    {
        Gp5Song song = Gp5Loader.GetParsedSong(filePath);
        if (song == null)
            return null;

        List<GeneratedPlaybackPartInfo> parts = new List<GeneratedPlaybackPartInfo>(song.tracks.Count);
        List<GeneratedPlaybackChannelAssignment> channels = new List<GeneratedPlaybackChannelAssignment>();
        List<GeneratedPlaybackNoteEvent> notes = new List<GeneratedPlaybackNoteEvent>();
        List<TempoEvent> tempoMap = song.tempoChanges
            .OrderBy(change => change.quarterPos)
            .Select(change => new TempoEvent { quarterPos = change.quarterPos, bpm = Math.Max(1.0, change.bpm) })
            .ToList();

        HashSet<int> usedChannels = new HashSet<int>();

        for (int trackIndex = 0; trackIndex < song.tracks.Count; trackIndex++)
        {
            Gp5Track track = song.tracks[trackIndex];
            int channel = AllocateChannel(track, usedChannels);

            channels.Add(new GeneratedPlaybackChannelAssignment
            {
                channel = channel,
                bank = track.isPercussionTrack ? 128 : track.midiBank,
                preset = Mathf.Clamp(track.sourceMidiProgram, 0, 127),
                isDrum = track.isPercussionTrack,
                label = string.IsNullOrWhiteSpace(track.name) ? $"Track {trackIndex + 1}" : track.name,
                sourcePartId = track.partId,
                sourcePartName = track.name,
                pitchBendRangeSemitones = 2
            });

            parts.Add(new GeneratedPlaybackPartInfo
            {
                partId = track.partId,
                displayName = string.IsNullOrWhiteSpace(track.name) ? $"Track {trackIndex + 1}" : track.name,
                instrumentName = string.IsNullOrWhiteSpace(track.name) ? "Track" : track.name,
                sourceMidiChannel = track.sourceMidiChannel,
                sourceMidiProgram = track.sourceMidiProgram,
                preferredBank = track.isPercussionTrack ? 128 : track.midiBank,
                isDrum = track.isPercussionTrack,
                isGuitarFamily = IsGuitarTrackName(track.name),
                isExplicitHarmonicPart = false
            });

            foreach (Gp5Beat beat in track.beats.Where(beat => !beat.isRest))
            {
                float startSeconds = (float)QuarterToSeconds(beat.startQuarter, tempoMap);
                float beatDurationSeconds = (float)Math.Max(0.03, QuarterToSeconds(beat.startQuarter + beat.durationQuarter, tempoMap) - QuarterToSeconds(beat.startQuarter, tempoMap));
                foreach (Gp5Note note in beat.notes)
                {
                    ResolveTechniqueVariant(note, out GeneratedTechniqueVariant techniqueVariant, out int bendRange);
                    GeneratedPlaybackNoteEvent noteEvent = new GeneratedPlaybackNoteEvent
                    {
                        startTimeSeconds = startSeconds,
                        durationSeconds = Mathf.Max(0.03f, (float)(beatDurationSeconds * Math.Max(0.05, note.durationPercent))),
                        midiNote = Mathf.Clamp(note.midi, 0, 127),
                        velocity = Mathf.Clamp(note.velocity, 1, 127),
                        channel = channel,
                        partId = track.partId,
                        partName = track.name,
                        techniqueVariant = techniqueVariant,
                        pitchBendRangeSemitones = bendRange,
                        pitchCurve = BuildPitchCurve(note.bend)
                    };

                    if (noteEvent.pitchCurve.Count > 1)
                        noteEvent.pitchPreRollSeconds = 0.06f;

                    if (beat.noteVibrato || beat.beatWideVibrato || note.isVibrato)
                    {
                        noteEvent.vibratoDepthSemitones = noteEvent.pitchCurve.Count > 1 ? 0.12f : 0.18f;
                        noteEvent.vibratoRateHz = 5.8f;
                        noteEvent.vibratoDelayNormalized = 0.3f;
                        noteEvent.vibratoFadeNormalized = 0.2f;
                    }

                    if (note.isHammer)
                        noteEvent.attackVelocityScale = 0.72f;
                    else if (note.hasSlide)
                        noteEvent.attackVelocityScale = 0.78f;

                    notes.Add(noteEvent);
                }
            }
        }

        return new GeneratedPlaybackArrangement
        {
            sourcePath = filePath,
            durationSeconds = notes.Count > 0 ? notes.Max(note => note.EndTimeSeconds) : 0f,
            parts = parts,
            channelAssignments = channels.OrderBy(channel => channel.channel).ToList(),
            notes = notes.OrderBy(note => note.startTimeSeconds).ThenBy(note => note.channel).ThenBy(note => note.midiNote).ToList()
        };
    }

    private static int AllocateChannel(Gp5Track track, HashSet<int> usedChannels)
    {
        if (track.isPercussionTrack)
        {
            usedChannels.Add(9);
            return 9;
        }

        int preferred = track.sourceMidiChannel >= 0 ? track.sourceMidiChannel % 16 : 0;
        if (preferred == 9)
            preferred = 0;

        if (!usedChannels.Contains(preferred))
        {
            usedChannels.Add(preferred);
            return preferred;
        }

        for (int i = 0; i < 16; i++)
        {
            if (i == 9 || usedChannels.Contains(i))
                continue;

            usedChannels.Add(i);
            return i;
        }

        return preferred;
    }

    private static void ResolveTechniqueVariant(Gp5Note note, out GeneratedTechniqueVariant variant, out int bendRange)
    {
        bendRange = 2;
        if (note.isDead)
        {
            variant = GeneratedTechniqueVariant.StraightMute;
            return;
        }

        if (note.isPalmMute)
        {
            variant = GeneratedTechniqueVariant.PalmMute;
            return;
        }

        if (note.isHarmonic)
        {
            variant = GeneratedTechniqueVariant.Harmonic;
            return;
        }

        variant = GeneratedTechniqueVariant.Normal;
    }

    private static List<GeneratedPlaybackPitchPoint> BuildPitchCurve(Gp5BendEffect bend)
    {
        List<GeneratedPlaybackPitchPoint> curve = new List<GeneratedPlaybackPitchPoint>();
        if (bend == null || bend.points == null || bend.points.Count == 0)
        {
            curve.Add(new GeneratedPlaybackPitchPoint { normalizedTime = 0f, semitoneOffset = 0f });
            curve.Add(new GeneratedPlaybackPitchPoint { normalizedTime = 1f, semitoneOffset = 0f });
            return curve;
        }

        for (int i = 0; i < bend.points.Count; i++)
        {
            Gp5BendPoint point = bend.points[i];
            curve.Add(new GeneratedPlaybackPitchPoint
            {
                normalizedTime = Mathf.Clamp01(point.position / 12f),
                semitoneOffset = point.value
            });
        }

        if (curve[0].normalizedTime > 0f)
        {
            curve.Insert(0, new GeneratedPlaybackPitchPoint
            {
                normalizedTime = 0f,
                semitoneOffset = curve[0].semitoneOffset
            });
        }

        if (curve[curve.Count - 1].normalizedTime < 1f)
        {
            curve.Add(new GeneratedPlaybackPitchPoint
            {
                normalizedTime = 1f,
                semitoneOffset = curve[curve.Count - 1].semitoneOffset
            });
        }

        return curve;
    }

    private static bool IsGuitarTrackName(string name)
    {
        string lower = (name ?? string.Empty).ToLowerInvariant();
        return lower.Contains("guitar") || lower.Contains("slash") || lower.Contains("solo") || lower.Contains("lead");
    }

    private static double QuarterToSeconds(double quarterPos, List<TempoEvent> tempoMap)
    {
        if (tempoMap == null || tempoMap.Count == 0)
            return quarterPos * 0.5;

        double seconds = 0.0;
        TempoEvent current = tempoMap[0];
        for (int i = 1; i < tempoMap.Count; i++)
        {
            TempoEvent next = tempoMap[i];
            if (quarterPos <= next.quarterPos)
            {
                seconds += (quarterPos - current.quarterPos) * (60.0 / current.bpm);
                return seconds;
            }

            seconds += (next.quarterPos - current.quarterPos) * (60.0 / current.bpm);
            current = next;
        }

        seconds += (quarterPos - current.quarterPos) * (60.0 / current.bpm);
        return seconds;
    }
}
