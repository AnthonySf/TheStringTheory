using System;
using System.Collections.Generic;
using System.Linq;
using AlphaTab.Model;
using UnityEngine;

internal static class AlphaTabGpBandPlaybackLoader
{
    public static GeneratedPlaybackArrangement LoadArrangement(string filePath)
    {
        AlphaTabGpScoreData data = AlphaTabGpScoreCache.GetOrLoad(filePath);
        if (data == null || data.tracks.Count == 0)
            return null;

        List<GeneratedPlaybackPartInfo> parts = new List<GeneratedPlaybackPartInfo>(data.tracks.Count);
        List<GeneratedPlaybackChannelAssignment> channels = new List<GeneratedPlaybackChannelAssignment>();
        List<GeneratedPlaybackNoteEvent> notes = new List<GeneratedPlaybackNoteEvent>(data.matchedNotes.Count);
        Dictionary<Note, AlphaTabGpMatchedNote> matchedBySource = data.matchedNotes
            .Where(note => note.sourceNote != null)
            .GroupBy(note => note.sourceNote)
            .ToDictionary(group => group.Key, group => group.First());

        HashSet<int> emittedChannels = new HashSet<int>();
        for (int trackIndex = 0; trackIndex < data.tracks.Count; trackIndex++)
        {
            AlphaTabGpTrackContext track = data.tracks[trackIndex];
            parts.Add(new GeneratedPlaybackPartInfo
            {
                partId = track.partId,
                displayName = track.name,
                instrumentName = string.IsNullOrWhiteSpace(track.shortName) ? track.name : track.shortName,
                sourceMidiChannel = track.usedChannels.Count > 0 ? track.usedChannels.Min() : 0,
                sourceMidiProgram = track.midiProgram,
                preferredBank = track.isPercussion ? 128 : 0,
                isDrum = track.isPercussion,
                isGuitarFamily = IsGuitarTrackName(track.name),
                isExplicitHarmonicPart = false
            });

            foreach (int channel in track.usedChannels.OrderBy(value => value))
            {
                if (!emittedChannels.Add(channel))
                    continue;

                channels.Add(new GeneratedPlaybackChannelAssignment
                {
                    channel = channel,
                    bank = track.isPercussion ? 128 : 0,
                    preset = track.midiProgram,
                    isDrum = track.isPercussion,
                    label = track.name,
                    sourcePartId = track.partId,
                    sourcePartName = track.name,
                    pitchBendRangeSemitones = 2
                });
            }
        }

        foreach (AlphaTabGpMatchedNote matched in data.matchedNotes.OrderBy(note => note.startTick).ThenBy(note => note.trackIndex).ThenBy(note => note.stringIdx))
        {
            float startSeconds = (float)AlphaTabGpLoader.TickToSeconds(matched.startTick, data.tempoPoints, data.midiDivision);
            float endSeconds = (float)AlphaTabGpLoader.TickToSeconds(matched.endTick, data.tempoPoints, data.midiDivision);
            float durationSeconds = Mathf.Max(0.03f, endSeconds - startSeconds);
            Note source = matched.sourceNote;

            GeneratedPlaybackNoteEvent noteEvent = new GeneratedPlaybackNoteEvent
            {
                startTimeSeconds = startSeconds,
                durationSeconds = durationSeconds,
                midiNote = matched.midiNote,
                velocity = Mathf.Clamp(matched.velocity, 1, 127),
                channel = matched.channel,
                partId = matched.partId,
                partName = matched.partName,
                techniqueVariant = ResolveTechniqueVariant(source),
                pitchCurve = AlphaTabGpLoader.BuildPitchCurve(source)
            };

            noteEvent.pitchBendRangeSemitones = Mathf.Max(2, AlphaTabGpLoader.CalculatePitchCurveRange(noteEvent.pitchCurve));
            if (noteEvent.pitchCurve != null && noteEvent.pitchCurve.Count > 1 && Mathf.Abs(noteEvent.pitchCurve[0].semitoneOffset) > 0.01f)
                noteEvent.pitchPreRollSeconds = 0.06f;

            AlphaTabGpLoader.TryNormalizePreBendAttackPitch(ref noteEvent.midiNote, ref noteEvent.pitchPreRollSeconds, noteEvent.pitchCurve);

            if (source != null && (source.Vibrato != VibratoType.None || source.Beat.Vibrato != VibratoType.None))
            {
                noteEvent.vibratoDepthSemitones = noteEvent.pitchCurve.Count > 1 ? 0.12f : 0.24f;
                noteEvent.vibratoRateHz = 5.8f;
                noteEvent.vibratoDelayNormalized = AlphaTabGpLoader.ResolveVibratoDelayNormalized(source, noteEvent.pitchCurve);
                noteEvent.vibratoFadeNormalized = 0.20f;
                noteEvent.pitchBendRangeSemitones = Mathf.Max(noteEvent.pitchBendRangeSemitones, Mathf.CeilToInt(noteEvent.vibratoDepthSemitones));
            }

            if (source != null && source.IsHammerPullDestination && source.HammerPullOrigin != null)
            {
                AlphaTabGpMatchedNote origin = FindMatched(source.HammerPullOrigin, matchedBySource);
                if (origin != null)
                {
                    GeneratedLegatoTransitionKind kind = matched.fret >= origin.fret
                        ? GeneratedLegatoTransitionKind.HammerOn
                        : GeneratedLegatoTransitionKind.PullOff;
                    AlphaTabGpLoader.ApplyLegatoTransition(noteEvent, origin, kind);
                }
            }
            else if (source != null && source.SlideOrigin != null)
            {
                AlphaTabGpMatchedNote origin = FindMatched(source.SlideOrigin, matchedBySource);
                if (origin != null)
                    AlphaTabGpLoader.ApplyLegatoTransition(noteEvent, origin, GeneratedLegatoTransitionKind.Slide);
            }

            notes.Add(noteEvent);
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

    private static AlphaTabGpMatchedNote FindMatched(Note source, Dictionary<Note, AlphaTabGpMatchedNote> matchedBySource)
    {
        if (source == null)
            return null;

        matchedBySource.TryGetValue(source, out AlphaTabGpMatchedNote matched);
        return matched;
    }

    private static GeneratedTechniqueVariant ResolveTechniqueVariant(Note source)
    {
        if (source == null)
            return GeneratedTechniqueVariant.Normal;

        if (source.IsDead)
            return GeneratedTechniqueVariant.StraightMute;
        if (source.IsPalmMute)
            return GeneratedTechniqueVariant.PalmMute;
        if (source.IsHarmonic)
            return GeneratedTechniqueVariant.Harmonic;

        return GeneratedTechniqueVariant.Normal;
    }

    private static bool IsGuitarTrackName(string name)
    {
        string lower = (name ?? string.Empty).ToLowerInvariant();
        return lower.Contains("guitar") || lower.Contains("slash") || lower.Contains("solo") || lower.Contains("lead");
    }
}
