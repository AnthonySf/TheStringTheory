using System;
using System.Collections.Generic;
using System.Linq;
using AlphaTab.Model;
using UnityEngine;

internal static class AlphaTabGpBandPlaybackLoader
{
    private const float BendRouteReleaseTailSeconds = 0.025f;
    private const int DefaultGuitarCleanPreset = 27;
    private const int DefaultGuitarLeadPreset = 29;
    private const int DefaultGuitarAcousticPreset = 25;
    private const int DefaultGuitarMutePreset = 28;
    private const int DefaultGuitarHarmonicPreset = 31;

    private readonly struct RouteKey : IEquatable<RouteKey>
    {
        public readonly string partId;
        public readonly bool isDrum;
        public readonly int bank;
        public readonly int preset;
        public readonly int pitchBendRangeSemitones;

        public RouteKey(string partId, bool isDrum, int bank, int preset, int pitchBendRangeSemitones)
        {
            this.partId = partId ?? string.Empty;
            this.isDrum = isDrum;
            this.bank = bank;
            this.preset = preset;
            this.pitchBendRangeSemitones = pitchBendRangeSemitones;
        }

        public bool Equals(RouteKey other)
        {
            return string.Equals(partId, other.partId, StringComparison.OrdinalIgnoreCase) &&
                   isDrum == other.isDrum &&
                   bank == other.bank &&
                   preset == other.preset &&
                   pitchBendRangeSemitones == other.pitchBendRangeSemitones;
        }

        public override bool Equals(object obj)
        {
            return obj is RouteKey other && Equals(other);
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

    private sealed class RouteDescriptor
    {
        public int channel;
        public int bank;
        public int preset;
        public bool isDrum;
        public string label;
        public string sourcePartId;
        public string sourcePartName;
        public int pitchBendRangeSemitones;
    }

    private sealed class RouteLaneState
    {
        public RouteDescriptor descriptor;
        public float occupiedUntilSeconds;
    }

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

        for (int trackIndex = 0; trackIndex < data.tracks.Count; trackIndex++)
        {
            AlphaTabGpTrackContext track = data.tracks[trackIndex];
            bool isGuitarFamily = IsGuitarTrack(track);
            parts.Add(new GeneratedPlaybackPartInfo
            {
                partId = track.partId,
                displayName = track.name,
                instrumentName = string.IsNullOrWhiteSpace(track.shortName) ? track.name : track.shortName,
                sourceMidiChannel = track.usedChannels.Count > 0 ? track.usedChannels.Min() : 0,
                sourceMidiProgram = track.midiProgram,
                preferredBank = track.isPercussion ? 128 : 0,
                isDrum = track.isPercussion,
                isGuitarFamily = isGuitarFamily,
                isExplicitHarmonicPart = false
            });
        }

        int harmonicPreset = ResolveHarmonicPreset(data.tracks);
        List<RouteDescriptor> routes = new List<RouteDescriptor>();
        Dictionary<RouteKey, RouteDescriptor> sharedRoutes = new Dictionary<RouteKey, RouteDescriptor>();
        Dictionary<RouteKey, List<RouteLaneState>> pitchRouteLanes = new Dictionary<RouteKey, List<RouteLaneState>>();
        HashSet<int> usedChannels = new HashSet<int> { 9 };

        foreach (AlphaTabGpMatchedNote matched in data.matchedNotes.OrderBy(note => note.startTick).ThenBy(note => note.trackIndex).ThenBy(note => note.stringIdx))
        {
            if (matched.trackIndex < 0 || matched.trackIndex >= data.tracks.Count)
                continue;

            AlphaTabGpTrackContext track = data.tracks[matched.trackIndex];
            float startSeconds = (float)AlphaTabGpLoader.TickToSeconds(matched.startTick, data.tempoPoints, data.midiDivision);
            float endSeconds = (float)AlphaTabGpLoader.TickToSeconds(matched.endTick, data.tempoPoints, data.midiDivision);
            float durationSeconds = Mathf.Max(0.03f, endSeconds - startSeconds);
            Note source = matched.sourceNote;

            GeneratedTechniqueVariant techniqueVariant = ResolveTechniqueVariant(source);
            List<GeneratedPlaybackPitchPoint> pitchCurve = AlphaTabGpLoader.BuildPitchCurve(source);
            float pitchPreRollSeconds = 0f;
            if (pitchCurve != null && pitchCurve.Count > 1 && Mathf.Abs(pitchCurve[0].semitoneOffset) > 0.01f)
                pitchPreRollSeconds = 0.06f;

            int midiNote = matched.midiNote;
            AlphaTabGpLoader.TryNormalizePreBendAttackPitch(ref midiNote, ref pitchPreRollSeconds, pitchCurve);
            GeneratedPlaybackNoteEvent noteEvent = new GeneratedPlaybackNoteEvent
            {
                startTimeSeconds = startSeconds,
                durationSeconds = durationSeconds,
                pitchPreRollSeconds = pitchPreRollSeconds,
                midiNote = midiNote,
                velocity = Mathf.Clamp(matched.velocity, 1, 127),
                partId = matched.partId,
                partName = matched.partName,
                techniqueVariant = techniqueVariant,
                pitchCurve = pitchCurve
            };

            if (source != null && (source.Vibrato != VibratoType.None || source.Beat.Vibrato != VibratoType.None))
            {
                noteEvent.vibratoDepthSemitones = IsGuitarTrack(track)
                    ? (noteEvent.pitchCurve.Count > 1 ? 0.16f : 0.28f)
                    : (noteEvent.pitchCurve.Count > 1 ? 0.12f : 0.24f);
                noteEvent.vibratoRateHz = IsGuitarTrack(track) ? 5.6f : 5.8f;
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

            int pitchBendRangeSemitones = AlphaTabGpLoader.CalculatePitchCurveRange(noteEvent.pitchCurve);
            if (noteEvent.HasVibrato)
                pitchBendRangeSemitones = Mathf.Max(2, pitchBendRangeSemitones, Mathf.CeilToInt(noteEvent.vibratoDepthSemitones));
            noteEvent.pitchBendRangeSemitones = pitchBendRangeSemitones;

            bool wantsLeadPreset = ShouldUseLeadPreset(track, source, pitchBendRangeSemitones);
            int preset = ResolvePresetForNote(track, techniqueVariant, harmonicPreset, wantsLeadPreset);
            int bank = track.isPercussion ? 128 : 0;
            RouteKey routeKey = new RouteKey(track.partId, track.isPercussion, bank, preset, pitchBendRangeSemitones);
            RouteDescriptor route = pitchBendRangeSemitones > 0 && !track.isPercussion
                ? ResolvePitchRouteDescriptor(routeKey, track, techniqueVariant, preset, startSeconds, durationSeconds, noteEvent.pitchPreRollSeconds, usedChannels, routes, pitchRouteLanes)
                : ResolveSharedRouteDescriptor(routeKey, track, techniqueVariant, preset, usedChannels, routes, sharedRoutes);
            if (route == null)
                continue;

            noteEvent.channel = route.channel;
            notes.Add(noteEvent);
        }

        for (int i = 0; i < routes.Count; i++)
        {
            RouteDescriptor route = routes[i];
            channels.Add(new GeneratedPlaybackChannelAssignment
            {
                channel = route.channel,
                bank = route.bank,
                preset = route.preset,
                isDrum = route.isDrum,
                label = route.label,
                sourcePartId = route.sourcePartId,
                sourcePartName = route.sourcePartName,
                pitchBendRangeSemitones = route.pitchBendRangeSemitones
            });
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

    private static RouteDescriptor ResolveSharedRouteDescriptor(
        RouteKey routeKey,
        AlphaTabGpTrackContext track,
        GeneratedTechniqueVariant techniqueVariant,
        int preset,
        HashSet<int> usedChannels,
        List<RouteDescriptor> routes,
        Dictionary<RouteKey, RouteDescriptor> sharedRoutes)
    {
        if (sharedRoutes.TryGetValue(routeKey, out RouteDescriptor route))
            return route;

            int channel = AllocateVirtualChannel(track.isPercussion, usedChannels);
            if (channel < 0)
                return null;

        route = CreateRouteDescriptor(channel, track, techniqueVariant, preset, routeKey.pitchBendRangeSemitones, laneIndex: 0);
        sharedRoutes[routeKey] = route;
        routes.Add(route);
        return route;
    }

    private static RouteDescriptor ResolvePitchRouteDescriptor(
        RouteKey routeKey,
        AlphaTabGpTrackContext track,
        GeneratedTechniqueVariant techniqueVariant,
        int preset,
        float startTimeSeconds,
        float noteDurationSeconds,
        float pitchPreRollSeconds,
        HashSet<int> usedChannels,
        List<RouteDescriptor> routes,
        Dictionary<RouteKey, List<RouteLaneState>> pitchRouteLanes)
    {
        if (!pitchRouteLanes.TryGetValue(routeKey, out List<RouteLaneState> lanes))
        {
            lanes = new List<RouteLaneState>();
            pitchRouteLanes[routeKey] = lanes;
        }

        float occupancyStart = startTimeSeconds - Mathf.Max(0f, pitchPreRollSeconds);
        float occupancyEnd = startTimeSeconds + noteDurationSeconds + BendRouteReleaseTailSeconds;

        for (int i = 0; i < lanes.Count; i++)
        {
            RouteLaneState lane = lanes[i];
            if (lane.occupiedUntilSeconds <= occupancyStart + 0.0005f)
            {
                lane.occupiedUntilSeconds = occupancyEnd;
                return lane.descriptor;
            }
        }

        int channel = AllocateVirtualChannel(track.isPercussion, usedChannels);
        RouteDescriptor descriptor = CreateRouteDescriptor(channel, track, techniqueVariant, preset, routeKey.pitchBendRangeSemitones, lanes.Count);
        routes.Add(descriptor);
        lanes.Add(new RouteLaneState
        {
            descriptor = descriptor,
            occupiedUntilSeconds = occupancyEnd
        });
        return descriptor;
    }

    private static RouteDescriptor CreateRouteDescriptor(
        int channel,
        AlphaTabGpTrackContext track,
        GeneratedTechniqueVariant techniqueVariant,
        int preset,
        int pitchBendRangeSemitones,
        int laneIndex)
    {
        string label = BuildRouteLabel(track, techniqueVariant, preset, pitchBendRangeSemitones);
        if (laneIndex > 0)
            label = $"{label} {laneIndex + 1}";

        return new RouteDescriptor
        {
            channel = channel,
            bank = track.isPercussion ? 128 : 0,
            preset = preset,
            isDrum = track.isPercussion,
            label = label,
            sourcePartId = track.partId,
            sourcePartName = track.name,
            pitchBendRangeSemitones = pitchBendRangeSemitones
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

    private static int ResolveHarmonicPreset(IEnumerable<AlphaTabGpTrackContext> tracks)
    {
        AlphaTabGpTrackContext harmonicTrack = tracks.FirstOrDefault(track =>
            IsGuitarTrack(track) &&
            track.midiProgram >= 24 &&
            track.midiProgram <= 31 &&
            ((track.name ?? string.Empty).IndexOf("harm", StringComparison.OrdinalIgnoreCase) >= 0 ||
             (track.shortName ?? string.Empty).IndexOf("harm", StringComparison.OrdinalIgnoreCase) >= 0));
        if (harmonicTrack != null)
            return harmonicTrack.midiProgram;

        return DefaultGuitarHarmonicPreset;
    }

    private static int ResolvePresetForNote(AlphaTabGpTrackContext track, GeneratedTechniqueVariant techniqueVariant, int harmonicPreset, bool preferLeadPreset)
    {
        int sourcePreset = Mathf.Clamp(track.midiProgram, 0, 127);
        if (track.isPercussion)
            return sourcePreset;

        bool isGuitarTrack = IsGuitarTrack(track);
        if (!isGuitarTrack)
            return sourcePreset;

        switch (techniqueVariant)
        {
            case GeneratedTechniqueVariant.PalmMute:
            case GeneratedTechniqueVariant.StraightMute:
                return DefaultGuitarMutePreset;
            case GeneratedTechniqueVariant.Harmonic:
                return harmonicPreset;
        }

        if (IsGuitarProgram(sourcePreset))
        {
            if (preferLeadPreset && (sourcePreset == DefaultGuitarCleanPreset || sourcePreset == DefaultGuitarAcousticPreset))
                return DefaultGuitarLeadPreset;

            return sourcePreset;
        }

        string searchableName = $"{track.name} {track.shortName}".ToLowerInvariant();
        if (searchableName.Contains("acoustic"))
            return DefaultGuitarAcousticPreset;
        if (preferLeadPreset || searchableName.Contains("lead") || searchableName.Contains("solo") || searchableName.Contains("slash"))
            return DefaultGuitarLeadPreset;
        if (searchableName.Contains("mute"))
            return DefaultGuitarMutePreset;

        return DefaultGuitarCleanPreset;
    }

    private static bool ShouldUseLeadPreset(AlphaTabGpTrackContext track, Note source, int pitchBendRangeSemitones)
    {
        string searchableName = $"{track.name} {track.shortName}".ToLowerInvariant();
        if (searchableName.Contains("lead") || searchableName.Contains("solo") || searchableName.Contains("slash"))
            return true;

        if (pitchBendRangeSemitones > 0)
            return true;

        if (source == null)
            return false;

        return source.SlideOrigin != null ||
               source.SlideTarget != null ||
               source.IsHammerPullDestination ||
               source.Vibrato != VibratoType.None ||
               source.Beat.Vibrato != VibratoType.None;
    }

    private static string BuildRouteLabel(AlphaTabGpTrackContext track, GeneratedTechniqueVariant techniqueVariant, int preset, int pitchBendRangeSemitones)
    {
        if (track.isPercussion)
            return "Drums";

        if (pitchBendRangeSemitones > 0)
            return $"{track.name} Bend";
        if (techniqueVariant == GeneratedTechniqueVariant.Harmonic)
            return $"{track.name} Harmonic";
        if (techniqueVariant == GeneratedTechniqueVariant.PalmMute)
            return $"{track.name} Palm Mute";
        if (techniqueVariant == GeneratedTechniqueVariant.StraightMute)
            return $"{track.name} Mute";

        return $"{track.name} ({preset})";
    }

    private static int AllocateVirtualChannel(bool isDrum, HashSet<int> usedChannels)
    {
        if (isDrum)
        {
            usedChannels.Add(9);
            return 9;
        }

        int channel = 0;
        while (usedChannels.Contains(channel) || channel == 9)
            channel++;

        usedChannels.Add(channel);
        return channel;
    }

    private static bool IsGuitarTrack(AlphaTabGpTrackContext track)
    {
        if (track == null || track.isPercussion)
            return false;

        return IsGuitarTrackName(track.name) || IsGuitarTrackName(track.shortName) || IsGuitarProgram(track.midiProgram);
    }

    private static bool IsGuitarTrackName(string name)
    {
        string lower = (name ?? string.Empty).ToLowerInvariant();
        return lower.Contains("guitar") || lower.Contains("slash") || lower.Contains("solo") || lower.Contains("lead");
    }

    private static bool IsGuitarProgram(int preset)
    {
        return preset >= 24 && preset <= 31;
    }
}
