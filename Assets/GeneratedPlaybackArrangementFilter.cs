using System;
using System.Collections.Generic;
using System.Linq;

public static class GeneratedPlaybackArrangementFilter
{
    private const float BendRouteReleaseTailSeconds = 0.025f;

    private readonly struct ChannelRouteKey : IEquatable<ChannelRouteKey>
    {
        public readonly string partId;
        public readonly bool isDrum;
        public readonly int bank;
        public readonly int preset;
        public readonly int pitchBendRangeSemitones;

        public ChannelRouteKey(string partId, bool isDrum, int bank, int preset, int pitchBendRangeSemitones)
        {
            this.partId = partId ?? string.Empty;
            this.isDrum = isDrum;
            this.bank = bank;
            this.preset = preset;
            this.pitchBendRangeSemitones = pitchBendRangeSemitones;
        }

        public bool Equals(ChannelRouteKey other)
        {
            return string.Equals(partId, other.partId, StringComparison.OrdinalIgnoreCase) &&
                   isDrum == other.isDrum &&
                   bank == other.bank &&
                   preset == other.preset &&
                   pitchBendRangeSemitones == other.pitchBendRangeSemitones;
        }

        public override bool Equals(object obj)
        {
            return obj is ChannelRouteKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(partId ?? string.Empty);
                hash = (hash * 397) ^ (isDrum ? 1 : 0);
                hash = (hash * 397) ^ bank;
                hash = (hash * 397) ^ preset;
                hash = (hash * 397) ^ pitchBendRangeSemitones;
                return hash;
            }
        }
    }

    private sealed class ChannelLaneState
    {
        public GeneratedPlaybackChannelAssignment route;
        public float occupiedUntilSeconds;
    }

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

        List<GeneratedPlaybackChannelAssignment> filteredChannels = ReassignPlaybackChannels(source, filteredNotes);

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

    private static List<GeneratedPlaybackChannelAssignment> ReassignPlaybackChannels(
        GeneratedPlaybackArrangement source,
        List<GeneratedPlaybackNoteEvent> filteredNotes)
    {
        if (filteredNotes == null || filteredNotes.Count == 0)
            return new List<GeneratedPlaybackChannelAssignment>();

        List<GeneratedPlaybackChannelAssignment> sourceRoutes = (source.channelAssignments ?? new List<GeneratedPlaybackChannelAssignment>())
            .Where(route => route != null)
            .Select(CloneChannel)
            .ToList();

        Dictionary<ChannelRouteKey, GeneratedPlaybackChannelAssignment> sharedRoutes = new Dictionary<ChannelRouteKey, GeneratedPlaybackChannelAssignment>();
        Dictionary<ChannelRouteKey, List<ChannelLaneState>> pitchRoutes = new Dictionary<ChannelRouteKey, List<ChannelLaneState>>();
        HashSet<int> usedActualChannels = new HashSet<int> { 9 };
        List<GeneratedPlaybackChannelAssignment> allocatedRoutes = new List<GeneratedPlaybackChannelAssignment>();

        filteredNotes.Sort((a, b) =>
        {
            int cmp = a.startTimeSeconds.CompareTo(b.startTimeSeconds);
            if (cmp != 0)
                return cmp;
            cmp = a.channel.CompareTo(b.channel);
            if (cmp != 0)
                return cmp;
            return a.midiNote.CompareTo(b.midiNote);
        });

        foreach (GeneratedPlaybackNoteEvent note in filteredNotes)
        {
            GeneratedPlaybackChannelAssignment sourceRoute = ResolveSourceRoute(note, sourceRoutes);
            ChannelRouteKey key = new ChannelRouteKey(
                sourceRoute.sourcePartId ?? note.partId,
                sourceRoute.isDrum,
                sourceRoute.bank,
                sourceRoute.preset,
                sourceRoute.pitchBendRangeSemitones);

            GeneratedPlaybackChannelAssignment mappedRoute = sourceRoute.isDrum
                ? ResolveDrumRoute(sharedRoutes, key, sourceRoute, allocatedRoutes)
                : sourceRoute.pitchBendRangeSemitones > 0
                    ? ResolvePitchRoute(pitchRoutes, key, sourceRoute, note, usedActualChannels, allocatedRoutes)
                    : ResolveSharedRoute(sharedRoutes, key, sourceRoute, usedActualChannels, allocatedRoutes);

            note.channel = mappedRoute.channel;
        }

        return allocatedRoutes
            .OrderBy(route => route.channel)
            .ThenBy(route => route.label ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static GeneratedPlaybackChannelAssignment ResolveSourceRoute(
        GeneratedPlaybackNoteEvent note,
        List<GeneratedPlaybackChannelAssignment> sourceRoutes)
    {
        if (sourceRoutes != null && sourceRoutes.Count > 0)
        {
            GeneratedPlaybackChannelAssignment byPartId = sourceRoutes.FirstOrDefault(route =>
                route.channel == note.channel &&
                !string.IsNullOrWhiteSpace(note.partId) &&
                string.Equals(route.sourcePartId ?? string.Empty, note.partId, StringComparison.OrdinalIgnoreCase));
            if (byPartId != null)
                return byPartId;

            GeneratedPlaybackChannelAssignment byPartName = sourceRoutes.FirstOrDefault(route =>
                route.channel == note.channel &&
                !string.IsNullOrWhiteSpace(note.partName) &&
                string.Equals(route.sourcePartName ?? string.Empty, note.partName, StringComparison.OrdinalIgnoreCase));
            if (byPartName != null)
                return byPartName;

            GeneratedPlaybackChannelAssignment byChannel = sourceRoutes.FirstOrDefault(route => route.channel == note.channel);
            if (byChannel != null)
                return byChannel;
        }

        return new GeneratedPlaybackChannelAssignment
        {
            channel = note.channel,
            bank = -1,
            preset = 0,
            isDrum = false,
            label = note.partName,
            sourcePartId = note.partId,
            sourcePartName = note.partName,
            pitchBendRangeSemitones = note.pitchBendRangeSemitones
        };
    }

    private static GeneratedPlaybackChannelAssignment ResolveDrumRoute(
        Dictionary<ChannelRouteKey, GeneratedPlaybackChannelAssignment> sharedRoutes,
        ChannelRouteKey key,
        GeneratedPlaybackChannelAssignment template,
        List<GeneratedPlaybackChannelAssignment> allocatedRoutes)
    {
        if (sharedRoutes.TryGetValue(key, out GeneratedPlaybackChannelAssignment existing))
            return existing;

        GeneratedPlaybackChannelAssignment created = CloneChannel(template);
        created.channel = 9;
        sharedRoutes[key] = created;
        allocatedRoutes.Add(created);
        return created;
    }

    private static GeneratedPlaybackChannelAssignment ResolveSharedRoute(
        Dictionary<ChannelRouteKey, GeneratedPlaybackChannelAssignment> sharedRoutes,
        ChannelRouteKey key,
        GeneratedPlaybackChannelAssignment template,
        HashSet<int> usedActualChannels,
        List<GeneratedPlaybackChannelAssignment> allocatedRoutes)
    {
        if (sharedRoutes.TryGetValue(key, out GeneratedPlaybackChannelAssignment existing))
            return existing;

        GeneratedPlaybackChannelAssignment created = CloneChannel(template);
        created.channel = AllocateActualChannel(usedActualChannels);
        sharedRoutes[key] = created;
        allocatedRoutes.Add(created);
        return created;
    }

    private static GeneratedPlaybackChannelAssignment ResolvePitchRoute(
        Dictionary<ChannelRouteKey, List<ChannelLaneState>> pitchRoutes,
        ChannelRouteKey key,
        GeneratedPlaybackChannelAssignment template,
        GeneratedPlaybackNoteEvent note,
        HashSet<int> usedActualChannels,
        List<GeneratedPlaybackChannelAssignment> allocatedRoutes)
    {
        if (!pitchRoutes.TryGetValue(key, out List<ChannelLaneState> lanes))
        {
            lanes = new List<ChannelLaneState>();
            pitchRoutes[key] = lanes;
        }

        float occupancyStart = note.startTimeSeconds - Math.Max(0f, note.pitchPreRollSeconds);
        float occupancyEnd = note.EndTimeSeconds + BendRouteReleaseTailSeconds;
        for (int i = 0; i < lanes.Count; i++)
        {
            ChannelLaneState lane = lanes[i];
            if (lane.occupiedUntilSeconds <= occupancyStart + 0.0005f)
            {
                lane.occupiedUntilSeconds = occupancyEnd;
                return lane.route;
            }
        }

        int channel = AllocateActualChannel(usedActualChannels, allowFailure: true);
        if (channel >= 0)
        {
            GeneratedPlaybackChannelAssignment created = CloneChannel(template);
            created.channel = channel;
            allocatedRoutes.Add(created);
            lanes.Add(new ChannelLaneState
            {
                route = created,
                occupiedUntilSeconds = occupancyEnd
            });
            return created;
        }

        if (lanes.Count > 0)
        {
            ChannelLaneState fallbackLane = lanes.OrderBy(lane => lane.occupiedUntilSeconds).First();
            fallbackLane.occupiedUntilSeconds = occupancyEnd;
            return fallbackLane.route;
        }

        GeneratedPlaybackChannelAssignment forced = CloneChannel(template);
        forced.channel = AllocateActualChannel(usedActualChannels);
        allocatedRoutes.Add(forced);
        lanes.Add(new ChannelLaneState
        {
            route = forced,
            occupiedUntilSeconds = occupancyEnd
        });
        return forced;
    }

    private static int AllocateActualChannel(HashSet<int> usedActualChannels, bool allowFailure = false)
    {
        for (int channel = 0; channel < 16; channel++)
        {
            if (channel == 9 || usedActualChannels.Contains(channel))
                continue;

            usedActualChannels.Add(channel);
            return channel;
        }

        return allowFailure ? -1 : 0;
    }

    private static GeneratedPlaybackNoteEvent CloneNote(GeneratedPlaybackNoteEvent source)
    {
        return new GeneratedPlaybackNoteEvent
        {
            startTimeSeconds = source.startTimeSeconds,
            durationSeconds = source.durationSeconds,
            pitchPreRollSeconds = source.pitchPreRollSeconds,
            midiNote = source.midiNote,
            velocity = source.velocity,
            channel = source.channel,
            partId = source.partId,
            partName = source.partName,
            techniqueVariant = source.techniqueVariant,
            legatoTransitionKind = source.legatoTransitionKind,
            attackVelocityScale = source.attackVelocityScale,
            vibratoDepthSemitones = source.vibratoDepthSemitones,
            vibratoRateHz = source.vibratoRateHz,
            vibratoDelayNormalized = source.vibratoDelayNormalized,
            vibratoFadeNormalized = source.vibratoFadeNormalized,
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
