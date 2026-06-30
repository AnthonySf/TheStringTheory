using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlphaTab;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Importer;
using AlphaTab.Midi;
using AlphaTab.Model;
using UnityEngine;

internal sealed class AlphaTabGpTempoPoint
{
    public long tick;
    public double bpm;
}

internal sealed class AlphaTabGpTrackContext
{
    public int index;
    public string partId;
    public string name;
    public string shortName;
    public bool isPercussion;
    public int midiProgram;
    public int[] stringTuningPitches;
    public string tuningDisplayName;
    public readonly HashSet<int> usedChannels = new HashSet<int>();
}

internal sealed class AlphaTabGpMatchedNote
{
    public int trackIndex;
    public string partId;
    public string partName;
    public Note sourceNote;
    public long startTick;
    public long endTick;
    public int midiNote;
    public int velocity;
    public int channel;
    public int stringIdx;
    public int fret;
    public bool isStringed;
}

internal sealed class AlphaTabGpScoreData
{
    public string filePath;
    public Score score;
    public double midiDivision;
    public List<AlphaTabGpTempoPoint> tempoPoints = new List<AlphaTabGpTempoPoint>();
    public List<AlphaTabGpTrackContext> tracks = new List<AlphaTabGpTrackContext>();
    public List<AlphaTabGpMatchedNote> matchedNotes = new List<AlphaTabGpMatchedNote>();
    public Dictionary<int, List<AlphaTabGpMatchedNote>> matchedNotesByTrack = new Dictionary<int, List<AlphaTabGpMatchedNote>>();
}

internal static class AlphaTabGpScoreCache
{
    private static readonly Dictionary<string, AlphaTabGpScoreData> CachedScoresByPath = new Dictionary<string, AlphaTabGpScoreData>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, long> CachedTicksByPath = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    private static readonly int[] GuitarStringBasePitches = { 40, 45, 50, 55, 59, 64 };
    private static bool loggedImporterSettings;

    private sealed class SourceCandidate
    {
        public int trackIndex;
        public string partId;
        public string partName;
        public Note note;
        public long startTick;
        public int midiNote;
        public int stringIdx;
        public int fret;
        public bool isStringed;
        public bool matched;
        public long matchedEndTick;
        public int matchedVelocity;
        public int matchedChannel;
    }

    private sealed class MidiNoteSpan
    {
        public int trackIndex;
        public int channel;
        public int midiNote;
        public int velocity;
        public long startTick;
        public long endTick;
    }

    private readonly struct MatchKey : IEquatable<MatchKey>
    {
        public readonly int trackIndex;
        public readonly long startTick;
        public readonly int midiNote;

        public MatchKey(int trackIndex, long startTick, int midiNote)
        {
            this.trackIndex = trackIndex;
            this.startTick = startTick;
            this.midiNote = midiNote;
        }

        public bool Equals(MatchKey other)
        {
            return trackIndex == other.trackIndex &&
                   startTick == other.startTick &&
                   midiNote == other.midiNote;
        }

        public override bool Equals(object obj)
        {
            return obj is MatchKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = trackIndex;
                hash = (hash * 397) ^ startTick.GetHashCode();
                hash = (hash * 397) ^ midiNote;
                return hash;
            }
        }
    }

    public static AlphaTabGpScoreData GetOrLoad(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        long ticks = File.GetLastWriteTimeUtc(filePath).Ticks;
        if (CachedScoresByPath.TryGetValue(filePath, out AlphaTabGpScoreData cached) &&
            cached != null &&
            CachedTicksByPath.TryGetValue(filePath, out long cachedTicks) &&
            cachedTicks == ticks)
        {
            return cached;
        }

        try
        {
            AlphaTabGpScoreData loaded = LoadScoreData(filePath);
            CachedScoresByPath[filePath] = loaded;
            CachedTicksByPath[filePath] = ticks;
            return loaded;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AlphaTabGp] Failed to parse '{filePath}': {ex}");
            return null;
        }
    }

    private static AlphaTabGpScoreData LoadScoreData(string filePath)
    {
        Settings settings = CreateSettings();
        Score score = ScoreLoader.LoadScoreFromBytes(new Uint8Array(File.ReadAllBytes(filePath)), settings);

        MidiFile midiFile = new MidiFile();
        AlphaSynthMidiFileHandler midiHandler = new AlphaSynthMidiFileHandler(midiFile, true);
        MidiFileGenerator generator = new MidiFileGenerator(score, settings, midiHandler);
        generator.Generate();

        AlphaTabGpScoreData data = new AlphaTabGpScoreData
        {
            filePath = filePath,
            score = score,
            midiDivision = Math.Max(1.0, midiFile.Division)
        };

        data.tracks = BuildTrackContexts(score);
        data.tempoPoints = BuildTempoPoints(score, midiFile);
        data.matchedNotes = BuildMatchedNotes(data.tracks, score, midiFile);
        data.matchedNotesByTrack = data.matchedNotes
            .GroupBy(note => note.trackIndex)
            .ToDictionary(group => group.Key, group => group.OrderBy(note => note.startTick).ThenBy(note => note.stringIdx).ToList());

        return data;
    }

    private static Settings CreateSettings()
    {
        if (!loggedImporterSettings)
        {
            loggedImporterSettings = true;
            Debug.Log("[AlphaTabGp] Using default AlphaTab importer settings (no forced codepage override).");
        }

        return new Settings();
    }

    private static List<AlphaTabGpTrackContext> BuildTrackContexts(Score score)
    {
        List<AlphaTabGpTrackContext> tracks = new List<AlphaTabGpTrackContext>(score.Tracks.Count);
        for (int trackIndex = 0; trackIndex < score.Tracks.Count; trackIndex++)
        {
            Track track = score.Tracks[trackIndex];
            PlaybackInformation playback = track.PlaybackInfo;
            bool isPercussion = (track.PercussionArticulations != null && track.PercussionArticulations.Count > 0) ||
                                Mathf.RoundToInt((float)playback.PrimaryChannel) == 9 ||
                                Mathf.RoundToInt((float)playback.SecondaryChannel) == 9 ||
                                (track.Name ?? string.Empty).IndexOf("drum", StringComparison.OrdinalIgnoreCase) >= 0;

            tracks.Add(new AlphaTabGpTrackContext
            {
                index = trackIndex,
                partId = $"gp5-track-{trackIndex}",
                name = string.IsNullOrWhiteSpace(track.Name) ? $"Track {trackIndex + 1}" : track.Name,
                shortName = track.ShortName,
                isPercussion = isPercussion,
                midiProgram = Mathf.Clamp(Mathf.RoundToInt((float)playback.Program), 0, 127),
                stringTuningPitches = ExtractTrackTuningPitches(track),
                tuningDisplayName = ExtractTrackTuningDisplayName(track)
            });
        }

        return tracks;
    }

    private static List<AlphaTabGpTempoPoint> BuildTempoPoints(Score score, MidiFile midiFile)
    {
        List<AlphaTabGpTempoPoint> tempoPoints = midiFile.Events
            .OfType<TempoChangeEvent>()
            .Select(tempo => new AlphaTabGpTempoPoint
            {
                tick = Mathf.Max(0, Mathf.RoundToInt((float)tempo.Tick)),
                bpm = Math.Max(1.0, tempo.BeatsPerMinute)
            })
            .OrderBy(point => point.tick)
            .ToList();

        if (tempoPoints.Count == 0 || tempoPoints[0].tick != 0)
        {
            tempoPoints.Insert(0, new AlphaTabGpTempoPoint
            {
                tick = 0,
                bpm = Math.Max(1.0, score.Tempo)
            });
        }

        return tempoPoints;
    }

    private static List<AlphaTabGpMatchedNote> BuildMatchedNotes(List<AlphaTabGpTrackContext> tracks, Score score, MidiFile midiFile)
    {
        List<SourceCandidate> sourceCandidates = BuildSourceCandidates(tracks, score);
        Dictionary<MatchKey, Queue<SourceCandidate>> exactBuckets = BuildExactBuckets(sourceCandidates);
        Dictionary<(int trackIndex, long startTick), List<SourceCandidate>> startBuckets = BuildStartBuckets(sourceCandidates);

        foreach (MidiNoteSpan midiNote in BuildMidiNotes(midiFile))
        {
            SourceCandidate matchedCandidate = TryMatchCandidate(midiNote, exactBuckets, startBuckets);
            if (matchedCandidate == null)
                continue;

            matchedCandidate.matched = true;
            matchedCandidate.matchedEndTick = Math.Max(midiNote.endTick, midiNote.startTick + 1);
            matchedCandidate.matchedVelocity = midiNote.velocity;
            matchedCandidate.matchedChannel = midiNote.channel;

            if (midiNote.trackIndex >= 0 && midiNote.trackIndex < tracks.Count)
                tracks[midiNote.trackIndex].usedChannels.Add(midiNote.channel);
        }

        List<AlphaTabGpMatchedNote> result = new List<AlphaTabGpMatchedNote>(sourceCandidates.Count);
        for (int i = 0; i < sourceCandidates.Count; i++)
        {
            SourceCandidate candidate = sourceCandidates[i];
            long fallbackEndTick = ComputeFallbackEndTick(candidate.note);
            long endTick = candidate.matched ? candidate.matchedEndTick : fallbackEndTick;
            if (endTick <= candidate.startTick)
                endTick = candidate.startTick + 1;

            int fallbackChannel = ResolveFallbackChannel(score.Tracks[candidate.trackIndex]);
            result.Add(new AlphaTabGpMatchedNote
            {
                trackIndex = candidate.trackIndex,
                partId = candidate.partId,
                partName = candidate.partName,
                sourceNote = candidate.note,
                startTick = candidate.startTick,
                endTick = endTick,
                midiNote = candidate.midiNote,
                velocity = candidate.matched ? candidate.matchedVelocity : 96,
                channel = candidate.matched ? candidate.matchedChannel : fallbackChannel,
                stringIdx = candidate.stringIdx,
                fret = candidate.fret,
                isStringed = candidate.isStringed
            });

            tracks[candidate.trackIndex].usedChannels.Add(candidate.matched ? candidate.matchedChannel : fallbackChannel);
        }

        return result
            .OrderBy(note => note.trackIndex)
            .ThenBy(note => note.startTick)
            .ThenBy(note => note.stringIdx)
            .ToList();
    }

    private static List<SourceCandidate> BuildSourceCandidates(List<AlphaTabGpTrackContext> tracks, Score score)
    {
        List<SourceCandidate> candidates = new List<SourceCandidate>();
        for (int trackIndex = 0; trackIndex < score.Tracks.Count; trackIndex++)
        {
            Track track = score.Tracks[trackIndex];
            AlphaTabGpTrackContext trackContext = tracks[trackIndex];

            foreach (Staff staff in track.Staves)
            {
                foreach (Bar bar in staff.Bars)
                {
                    foreach (Voice voice in bar.Voices)
                    {
                        foreach (Beat beat in voice.Beats)
                        {
                            if (beat == null || beat.IsRest || beat.Notes == null || beat.Notes.Count == 0)
                                continue;

                            foreach (Note note in beat.Notes)
                            {
                                if (note == null || note.IsTieDestination)
                                    continue;

                                int midiNote = ResolveSoundingMidi(note);
                                int stringIdx;
                                int fret;
                                bool isStringed;
                                if (trackContext.isPercussion)
                                {
                                    stringIdx = MapPercussionMidiToLane(midiNote);
                                    fret = midiNote;
                                    isStringed = false;
                                }
                                else
                                {
                                    isStringed = TryResolveStringPosition(note, out stringIdx, out fret);
                                    if (!isStringed)
                                        ResolveFallbackStringPosition(trackContext.stringTuningPitches, midiNote, out stringIdx, out fret);
                                }

                                candidates.Add(new SourceCandidate
                                {
                                    trackIndex = trackIndex,
                                    partId = trackContext.partId,
                                    partName = trackContext.name,
                                    note = note,
                                    startTick = RoundTick(note.Beat.AbsolutePlaybackStart),
                                    midiNote = midiNote,
                                    stringIdx = stringIdx,
                                    fret = fret,
                                    isStringed = isStringed
                                });
                            }
                        }
                    }
                }
            }
        }

        return candidates
            .OrderBy(candidate => candidate.trackIndex)
            .ThenBy(candidate => candidate.startTick)
            .ThenBy(candidate => candidate.midiNote)
            .ThenBy(candidate => candidate.stringIdx)
            .ToList();
    }

    private static int[] ExtractTrackTuningPitches(Track track)
    {
        if (track?.Staves == null)
            return null;

        Staff stringedStaff = track.Staves.FirstOrDefault(staff => staff != null && staff.IsStringed && staff.Tuning != null && staff.Tuning.Count > 0);
        if (stringedStaff == null)
            return null;

        return stringedStaff.Tuning.Select(value => Mathf.RoundToInt((float)value)).ToArray();
    }

    private static string ExtractTrackTuningDisplayName(Track track)
    {
        if (track?.Staves != null)
        {
            Staff stringedStaff = track.Staves.FirstOrDefault(staff => staff != null && staff.IsStringed);
            if (stringedStaff?.StringTuning != null && !string.IsNullOrWhiteSpace(stringedStaff.StringTuning.Name))
                return stringedStaff.StringTuning.Name;

            if (!string.IsNullOrWhiteSpace(stringedStaff?.TuningName))
                return stringedStaff.TuningName;
        }

        return StringTuningUtils.FormatTuningDisplayName(ExtractTrackTuningPitches(track));
    }

    private static Dictionary<MatchKey, Queue<SourceCandidate>> BuildExactBuckets(List<SourceCandidate> candidates)
    {
        Dictionary<MatchKey, Queue<SourceCandidate>> buckets = new Dictionary<MatchKey, Queue<SourceCandidate>>();
        for (int i = 0; i < candidates.Count; i++)
        {
            SourceCandidate candidate = candidates[i];
            MatchKey key = new MatchKey(candidate.trackIndex, candidate.startTick, candidate.midiNote);
            if (!buckets.TryGetValue(key, out Queue<SourceCandidate> queue))
            {
                queue = new Queue<SourceCandidate>();
                buckets[key] = queue;
            }

            queue.Enqueue(candidate);
        }

        return buckets;
    }

    private static Dictionary<(int trackIndex, long startTick), List<SourceCandidate>> BuildStartBuckets(List<SourceCandidate> candidates)
    {
        Dictionary<(int trackIndex, long startTick), List<SourceCandidate>> buckets = new Dictionary<(int trackIndex, long startTick), List<SourceCandidate>>();
        for (int i = 0; i < candidates.Count; i++)
        {
            SourceCandidate candidate = candidates[i];
            (int, long) key = (candidate.trackIndex, candidate.startTick);
            if (!buckets.TryGetValue(key, out List<SourceCandidate> list))
            {
                list = new List<SourceCandidate>();
                buckets[key] = list;
            }

            list.Add(candidate);
        }

        return buckets;
    }

    private static SourceCandidate TryMatchCandidate(
        MidiNoteSpan midiNote,
        Dictionary<MatchKey, Queue<SourceCandidate>> exactBuckets,
        Dictionary<(int trackIndex, long startTick), List<SourceCandidate>> startBuckets)
    {
        MatchKey exactKey = new MatchKey(midiNote.trackIndex, midiNote.startTick, midiNote.midiNote);
        if (exactBuckets.TryGetValue(exactKey, out Queue<SourceCandidate> exactQueue))
        {
            while (exactQueue.Count > 0)
            {
                SourceCandidate candidate = exactQueue.Dequeue();
                if (!candidate.matched)
                    return candidate;
            }
        }

        if (startBuckets.TryGetValue((midiNote.trackIndex, midiNote.startTick), out List<SourceCandidate> sameTickCandidates))
        {
            for (int i = 0; i < sameTickCandidates.Count; i++)
            {
                if (!sameTickCandidates[i].matched)
                    return sameTickCandidates[i];
            }
        }

        return null;
    }

    private static IEnumerable<MidiNoteSpan> BuildMidiNotes(MidiFile midiFile)
    {
        Dictionary<(int trackIndex, int channel, int midiNote), Queue<MidiNoteSpan>> activeNotes =
            new Dictionary<(int trackIndex, int channel, int midiNote), Queue<MidiNoteSpan>>();
        List<MidiNoteSpan> completed = new List<MidiNoteSpan>();
        long lastTick = 0;

        foreach (MidiEvent midiEvent in midiFile.Events.OrderBy(e => e.Tick))
        {
            lastTick = Math.Max(lastTick, RoundTick(midiEvent.Tick));
            if (!(midiEvent is NoteEvent noteEvent))
                continue;

            int trackIndex = Mathf.RoundToInt((float)noteEvent.Track);
            int channel = Mathf.RoundToInt((float)noteEvent.Channel);
            int midiNote = Mathf.RoundToInt((float)noteEvent.NoteKey);
            int velocity = Mathf.RoundToInt((float)noteEvent.NoteVelocity);
            var key = (trackIndex, channel, midiNote);

            bool isNoteOn = noteEvent.Type == MidiEventType.NoteOn && velocity > 0;
            bool isNoteOff = noteEvent.Type == MidiEventType.NoteOff ||
                             (noteEvent.Type == MidiEventType.NoteOn && velocity <= 0);

            if (isNoteOn)
            {
                if (!activeNotes.TryGetValue(key, out Queue<MidiNoteSpan> queue))
                {
                    queue = new Queue<MidiNoteSpan>();
                    activeNotes[key] = queue;
                }

                queue.Enqueue(new MidiNoteSpan
                {
                    trackIndex = trackIndex,
                    channel = channel,
                    midiNote = midiNote,
                    velocity = velocity,
                    startTick = RoundTick(noteEvent.Tick)
                });
            }
            else if (isNoteOff &&
                     activeNotes.TryGetValue(key, out Queue<MidiNoteSpan> queue) &&
                     queue.Count > 0)
            {
                MidiNoteSpan note = queue.Dequeue();
                note.endTick = Math.Max(RoundTick(noteEvent.Tick), note.startTick + 1);
                completed.Add(note);
            }
        }

        foreach (KeyValuePair<(int trackIndex, int channel, int midiNote), Queue<MidiNoteSpan>> pair in activeNotes)
        {
            Queue<MidiNoteSpan> queue = pair.Value;
            while (queue.Count > 0)
            {
                MidiNoteSpan note = queue.Dequeue();
                note.endTick = Math.Max(lastTick, note.startTick + 1);
                completed.Add(note);
            }
        }

        return completed
            .OrderBy(note => note.trackIndex)
            .ThenBy(note => note.startTick)
            .ThenBy(note => note.midiNote);
    }

    private static long ComputeFallbackEndTick(Note note)
    {
        long endTick = ComputeNoteEndTick(note);

        Note current = note;
        HashSet<Note> guard = new HashSet<Note>();
        while (current != null && current.TieDestination != null && guard.Add(current))
        {
            current = current.TieDestination;
            endTick = Math.Max(endTick, ComputeNoteEndTick(current));
        }

        if (note.LetRingDestination != null)
            endTick = Math.Max(endTick, ComputeNoteEndTick(note.LetRingDestination));

        return endTick;
    }

    private static long ComputeNoteEndTick(Note note)
    {
        if (note == null || note.Beat == null)
            return 1L;

        double startTick = SanitizeFinite(note.Beat.AbsolutePlaybackStart, 0d);
        double playbackDuration = SanitizeFinite(note.Beat.PlaybackDuration, 0d);
        double durationFactor = SanitizeDurationFactor(note.DurationPercent);
        double computedEndTick = startTick + (playbackDuration * durationFactor);

        long roundedStartTick = RoundTick(startTick);
        long roundedEndTick = RoundTick(computedEndTick, roundedStartTick + 1L);
        if (roundedEndTick <= roundedStartTick)
            return roundedStartTick + 1L;

        return roundedEndTick;
    }

    private static double SanitizeFinite(double value, double fallback)
    {
        return double.IsNaN(value) || double.IsInfinity(value) ? fallback : value;
    }

    private static double SanitizeDurationFactor(double durationFactor)
    {
        if (double.IsNaN(durationFactor) || double.IsInfinity(durationFactor) || durationFactor <= 0.0001)
            return 1d;

        return durationFactor;
    }

    private static bool TryResolveStringPosition(Note note, out int stringIdx, out int fret)
    {
        stringIdx = 0;
        fret = Mathf.Max(0, Mathf.RoundToInt((float)note.Fret));
        if (!note.IsStringed || note.String <= 0)
            return false;

        stringIdx = Mathf.Max(0, Mathf.RoundToInt((float)note.String) - 1);
        return true;
    }

    private static void ResolveFallbackStringPosition(int[] tuningPitches, int midiNote, out int stringIdx, out int fret)
    {
        if (TryMapMidiToGuitar(tuningPitches, midiNote, out stringIdx, out fret))
            return;

        stringIdx = 0;
        int[] resolvedTuning = tuningPitches != null && tuningPitches.Length > 0 ? tuningPitches : GuitarStringBasePitches;
        fret = Mathf.Max(0, midiNote - resolvedTuning[0]);
    }

    private static int MapPercussionMidiToLane(int midiNote)
    {
        switch (midiNote)
        {
            case 35:
            case 36:
                return 4;
            case 37:
            case 38:
            case 39:
            case 40:
                return 2;
            case 42:
            case 44:
            case 46:
                return 0;
            case 48:
            case 50:
                return 3;
            case 45:
            case 47:
                return 5;
            case 41:
            case 43:
                return 6;
            case 49:
            case 52:
            case 55:
            case 57:
                return 1;
            case 51:
            case 53:
            case 59:
                return 7;
            case 54:
            case 56:
            case 58:
            case 60:
            case 61:
            case 62:
            case 63:
            case 64:
            case 65:
            case 66:
            case 67:
            case 68:
            case 69:
            case 70:
            case 71:
            case 72:
            case 73:
            case 74:
            case 75:
            case 76:
            case 77:
            case 78:
            case 79:
            case 80:
            case 81:
            case 82:
            case 83:
            case 84:
            case 85:
            case 86:
            case 87:
                return 7;
            default:
                return 7;
        }
    }

    private static bool TryMapMidiToGuitar(int[] tuningPitches, int midiPitch, out int stringIdx, out int fret)
    {
        stringIdx = -1;
        fret = -1;
        int bestString = -1;
        int bestFret = int.MaxValue;
        int[] resolvedTuning = tuningPitches != null && tuningPitches.Length > 0 ? tuningPitches : GuitarStringBasePitches;

        for (int s = 0; s < resolvedTuning.Length; s++)
        {
            int candidateFret = midiPitch - resolvedTuning[s];
            if (candidateFret < 0 || candidateFret > 24)
                continue;

            if (candidateFret < bestFret)
            {
                bestFret = candidateFret;
                bestString = s;
            }
        }

        if (bestString < 0)
            return false;

        stringIdx = bestString;
        fret = bestFret;
        return true;
    }

    private static int ResolveSoundingMidi(Note note)
    {
        double pitch = note.RealValue;
        if (note.IsHarmonic && note.HarmonicPitch > 0.01)
            pitch = note.HarmonicPitch;
        else if (pitch <= 0.01 && note.RealValueWithoutHarmonic > 0.01)
            pitch = note.RealValueWithoutHarmonic;

        return Mathf.Clamp(Mathf.RoundToInt((float)pitch), 0, 127);
    }

    private static int ResolveFallbackChannel(Track track)
    {
        int primary = Mathf.RoundToInt((float)track.PlaybackInfo.PrimaryChannel);
        if (primary >= 0 && primary <= 15)
            return primary;

        int secondary = Mathf.RoundToInt((float)track.PlaybackInfo.SecondaryChannel);
        if (secondary >= 0 && secondary <= 15)
            return secondary;

        return 0;
    }

    private static long RoundTick(double tick, long fallback = 0L)
    {
        if (double.IsNaN(tick) || double.IsInfinity(tick))
            return Math.Max(0L, fallback);

        double rounded = Math.Round(tick);
        if (rounded <= 0d)
            return 0L;

        if (rounded >= long.MaxValue)
            return long.MaxValue;

        return Math.Max(0L, Convert.ToInt64(rounded));
    }
}
