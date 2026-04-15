using System;
using System.Collections.Generic;
using System.Linq;

public static class GeneratedPlaybackArrangementFilter
{
    public static GeneratedPlaybackArrangement CreateFiltered(GeneratedPlaybackArrangement source, IReadOnlyCollection<string> enabledPartIds, bool useAllParts)
    {
        if (source == null)
            return null;

        HashSet<string> enabledSet = useAllParts || enabledPartIds == null
            ? null
            : new HashSet<string>(enabledPartIds.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);

        List<GeneratedPlaybackPartInfo> filteredParts = (source.parts ?? new List<GeneratedPlaybackPartInfo>())
            .Select(ClonePart)
            .ToList();

        List<GeneratedPlaybackNoteEvent> filteredNotes = (source.notes ?? new List<GeneratedPlaybackNoteEvent>())
            .Where(note => enabledSet == null || enabledSet.Contains(note.partId ?? string.Empty))
            .Select(CloneNote)
            .OrderBy(note => note.startTimeSeconds)
            .ThenBy(note => note.channel)
            .ThenBy(note => note.midiNote)
            .ToList();

        HashSet<int> usedChannels = new HashSet<int>(filteredNotes.Select(note => note.channel));
        List<GeneratedPlaybackChannelAssignment> filteredChannels = (source.channelAssignments ?? new List<GeneratedPlaybackChannelAssignment>())
            .Where(route => usedChannels.Contains(route.channel))
            .Select(CloneChannel)
            .OrderBy(route => route.channel)
            .ToList();

        float filteredDuration = filteredNotes.Count > 0
            ? filteredNotes.Max(note => note.EndTimeSeconds)
            : source.durationSeconds;

        return new GeneratedPlaybackArrangement
        {
            sourcePath = source.sourcePath,
            durationSeconds = filteredDuration,
            parts = filteredParts,
            channelAssignments = filteredChannels,
            notes = filteredNotes
        };
    }

    private static GeneratedPlaybackPartInfo ClonePart(GeneratedPlaybackPartInfo source)
    {
        return new GeneratedPlaybackPartInfo
        {
            partId = source.partId,
            displayName = source.displayName,
            instrumentName = source.instrumentName,
            sourceMidiChannel = source.sourceMidiChannel,
            sourceMidiProgram = source.sourceMidiProgram,
            preferredBank = source.preferredBank,
            isDrum = source.isDrum,
            isGuitarFamily = source.isGuitarFamily,
            isExplicitHarmonicPart = source.isExplicitHarmonicPart
        };
    }

    private static GeneratedPlaybackChannelAssignment CloneChannel(GeneratedPlaybackChannelAssignment source)
    {
        return new GeneratedPlaybackChannelAssignment
        {
            channel = source.channel,
            bank = source.bank,
            preset = source.preset,
            isDrum = source.isDrum,
            label = source.label,
            sourcePartId = source.sourcePartId,
            sourcePartName = source.sourcePartName,
            pitchBendRangeSemitones = source.pitchBendRangeSemitones
        };
    }

    private static GeneratedPlaybackNoteEvent CloneNote(GeneratedPlaybackNoteEvent source)
    {
        return new GeneratedPlaybackNoteEvent
        {
            startTimeSeconds = source.startTimeSeconds,
            durationSeconds = source.durationSeconds,
            midiNote = source.midiNote,
            velocity = source.velocity,
            channel = source.channel,
            partId = source.partId,
            partName = source.partName,
            techniqueVariant = source.techniqueVariant,
            pitchBendRangeSemitones = source.pitchBendRangeSemitones,
            pitchCurve = source.pitchCurve != null
                ? source.pitchCurve.Select(point => new GeneratedPlaybackPitchPoint
                {
                    normalizedTime = point.normalizedTime,
                    semitoneOffset = point.semitoneOffset
                }).ToList()
                : new List<GeneratedPlaybackPitchPoint>()
        };
    }
}
