using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class Gp5Loader
{
    private const int BendTypePreBend = 4;
    private const int BendTypePreBendRelease = 5;

    private sealed class CachedSong
    {
        public Gp5Song song;
        public long ticks;
    }

    private static readonly Dictionary<string, CachedSong> cachedSongsByPath = new Dictionary<string, CachedSong>(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public static string BuildPartId(int trackIndex)
    {
        return $"gp5-track-{trackIndex}";
    }

    public static string TryReadTitle(string filePath)
    {
        Gp5Song song = GetParsedSong(filePath);
        return song != null ? song.title : null;
    }

    public static string TryReadArtist(string filePath)
    {
        Gp5Song song = GetParsedSong(filePath);
        return song != null ? song.artist : null;
    }

    public static List<MusicXmlLoader.MusicXmlPartSummary> GetPartSummaries(string filePath)
    {
        Gp5Song song = GetParsedSong(filePath);
        if (song == null)
            return new List<MusicXmlLoader.MusicXmlPartSummary>();

        List<MusicXmlLoader.MusicXmlPartSummary> summaries = new List<MusicXmlLoader.MusicXmlPartSummary>(song.tracks.Count);
        for (int i = 0; i < song.tracks.Count; i++)
        {
            Gp5Track track = song.tracks[i];
            int noteCount = track.beats.Sum(beat => beat.isRest ? 0 : beat.notes.Count);
            int tabCount = track.beats.Sum(beat => beat.notes.Count(note => note.stringNumber > 0 && note.fret >= 0));
            int score = noteCount + (tabCount * 20) + ScoreTrackName(track.name, track.isPercussionTrack);
            summaries.Add(new MusicXmlLoader.MusicXmlPartSummary
            {
                Index = i,
                PartId = track.partId,
                Name = string.IsNullOrWhiteSpace(track.name) ? $"Track {i + 1}" : track.name,
                InstrumentType = InferGpInstrumentType(track),
                Route = InferGpRoute(track),
                NoteCount = noteCount,
                TabCount = tabCount,
                Score = score,
                StringTuningPitches = GetTrackTuningPitches(track),
                TuningDisplayName = StringTuningUtils.FormatTuningDisplayName(GetTrackTuningPitches(track))
            });
        }

        return summaries;
    }

    public static List<NoteData> LoadSong(string filePath, int targetPartIndex = -1)
    {
        Gp5Song song = GetParsedSong(filePath);
        if (song == null || song.tracks.Count == 0)
            return new List<NoteData>();

        List<MusicXmlLoader.MusicXmlPartSummary> summaries = GetPartSummaries(filePath);
        int chosenTrackIndex = (targetPartIndex >= 0 && targetPartIndex < song.tracks.Count)
            ? targetPartIndex
            : ChooseBestTrack(summaries);

        Gp5Track track = song.tracks[chosenTrackIndex];
        List<ParsedGpNote> parsedNotes = BuildParsedNotes(track);
        List<TempoEvent> tempoMap = BuildTempoMap(song);
        return BuildGameplayNotes(parsedNotes, tempoMap);
    }

    internal static Gp5Song GetParsedSong(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        long ticks = File.GetLastWriteTimeUtc(filePath).Ticks;
        if (cachedSongsByPath.TryGetValue(filePath, out CachedSong cached) &&
            cached != null &&
            cached.song != null &&
            cached.ticks == ticks)
        {
            return cached.song;
        }

        try
        {
            Gp5Song song = Gp5Reader.Parse(filePath);
            cachedSongsByPath[filePath] = new CachedSong
            {
                song = song,
                ticks = ticks
            };
            return song;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Gp5Loader] Failed to parse '{filePath}': {ex}");
            return null;
        }
    }

    private sealed class TempoEvent
    {
        public double quarterPos;
        public double bpm;
    }

    private sealed class ParsedGpNote
    {
        public double quarterPos;
        public double durationQuarter;
        public int midi;
        public int stringIdx;
        public int fret;
        public string note;
        public bool slideStart;
        public bool hammerStart;
        public bool pullStart;
        public bool vibrato;
        public float bendStep;
        public bool bendPreBend;
        public bool bendRelease;
        public bool isMuted;
        public readonly List<ParsedTechniqueSegment> techniqueSegments = new List<ParsedTechniqueSegment>();
    }

    private sealed class ParsedTechniqueSegment
    {
        public NoteTechniqueSegmentType type;
        public double startQuarter;
        public double endQuarter;
        public int startFret;
        public int endFret;
        public float startBend;
        public float endBend;
    }

    private static List<TempoEvent> BuildTempoMap(Gp5Song song)
    {
        return song.tempoChanges
            .OrderBy(change => change.quarterPos)
            .Select(change => new TempoEvent
            {
                quarterPos = change.quarterPos,
                bpm = Math.Max(1.0, change.bpm)
            })
            .ToList();
    }

    private static List<ParsedGpNote> BuildParsedNotes(Gp5Track track)
    {
        List<ParsedGpNote> parsed = new List<ParsedGpNote>();
        List<Gp5Beat> beats = track.beats
            .Where(beat => !beat.isRest && beat.notes.Count > 0)
            .OrderBy(beat => beat.startQuarter)
            .ThenBy(beat => beat.voiceIndex)
            .ToList();

        foreach (Gp5Beat beat in beats)
        {
            foreach (Gp5Note note in beat.notes.OrderBy(n => n.stringIdx))
            {
                int stringIdx = track.isPercussionTrack ? MapPercussionMidiToLane(note.midi) : note.stringIdx;
                int fret = track.isPercussionTrack ? note.midi : note.fret;
                ParsedGpNote parsedNote = new ParsedGpNote
                {
                    quarterPos = beat.startQuarter,
                    durationQuarter = Math.Max(0.03125, beat.durationQuarter * Math.Max(0.05, note.durationPercent)),
                    midi = note.midi,
                    stringIdx = stringIdx,
                    fret = fret,
                    note = GetNoteName(note.midi),
                    vibrato = beat.noteVibrato || beat.beatWideVibrato || note.isVibrato,
                    isMuted = note.isDead
                };

                if (note.bend != null && note.bend.points.Count > 0)
                    ApplyBendSegments(parsedNote, note.bend);

                parsed.Add(parsedNote);
            }
        }

        LinkLegatoTransitions(parsed, beats);
        return parsed.OrderBy(note => note.quarterPos).ThenBy(note => note.stringIdx).ToList();
    }

    private static int MapPercussionMidiToLane(int midiNote)
    {
        return DrumLaneMapper.MapGeneralMidiToLane(midiNote);
    }

    private static void LinkLegatoTransitions(List<ParsedGpNote> parsed, List<Gp5Beat> beats)
    {
        foreach (Gp5Beat beat in beats)
        {
            List<Gp5Note> ordered = beat.notes.OrderBy(note => note.stringIdx).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                Gp5Note current = ordered[i];
                if (!current.isHammer && !current.hasSlide)
                    continue;

                ParsedGpNote source = parsed.FirstOrDefault(note =>
                    Math.Abs(note.quarterPos - beat.startQuarter) < 0.0001 &&
                    note.stringIdx == current.stringIdx &&
                    note.fret == current.fret);
                if (source == null)
                    continue;

                Gp5Beat nextBeat = beats.FirstOrDefault(candidate =>
                    candidate.startQuarter > beat.startQuarter &&
                    candidate.notes.Any(note => note.stringIdx == current.stringIdx));
                if (nextBeat == null)
                    continue;

                Gp5Note destinationNote = nextBeat.notes
                    .FirstOrDefault(note => note.stringIdx == current.stringIdx);
                if (destinationNote == null)
                    continue;

                ParsedGpNote destination = parsed.FirstOrDefault(note =>
                    Math.Abs(note.quarterPos - nextBeat.startQuarter) < 0.0001 &&
                    note.stringIdx == destinationNote.stringIdx &&
                    note.fret == destinationNote.fret);
                if (destination == null)
                    continue;

                if (current.hasSlide)
                {
                    source.slideStart = true;
                    source.techniqueSegments.Add(new ParsedTechniqueSegment
                    {
                        type = NoteTechniqueSegmentType.Slide,
                        startQuarter = source.quarterPos,
                        endQuarter = destination.quarterPos,
                        startFret = source.fret,
                        endFret = destination.fret
                    });
                }
                else if (current.isHammer)
                {
                    if (destination.fret >= source.fret)
                        source.hammerStart = true;
                    else
                        source.pullStart = true;
                }
            }
        }
    }

    private static void ApplyBendSegments(ParsedGpNote parsedNote, Gp5BendEffect bend)
    {
        float maxBend = 0f;
        bool preBendType = IsPreBendType(bend.type);
        if (bend.points.Count == 1)
        {
            float value = bend.points[0].value;
            float startBend = preBendType ? value : 0f;
            parsedNote.techniqueSegments.Add(new ParsedTechniqueSegment
            {
                type = ResolveBendSegmentType(startBend, value),
                startQuarter = parsedNote.quarterPos,
                endQuarter = parsedNote.quarterPos + parsedNote.durationQuarter,
                startFret = parsedNote.fret,
                endFret = parsedNote.fret,
                startBend = startBend,
                endBend = value
            });
            maxBend = Mathf.Max(maxBend, Mathf.Abs(value));
        }
        else
        {
            for (int i = 0; i < bend.points.Count - 1; i++)
            {
                Gp5BendPoint current = bend.points[i];
                Gp5BendPoint next = bend.points[i + 1];
                double startQuarter = parsedNote.quarterPos + (parsedNote.durationQuarter * (current.position / 12.0));
                double endQuarter = parsedNote.quarterPos + (parsedNote.durationQuarter * (next.position / 12.0));
                ParsedTechniqueSegment segment = new ParsedTechniqueSegment
                {
                    type = ResolveBendSegmentType(current.value, next.value),
                    startQuarter = startQuarter,
                    endQuarter = Math.Max(startQuarter + 0.015625, endQuarter),
                    startFret = parsedNote.fret,
                    endFret = parsedNote.fret,
                    startBend = current.value,
                    endBend = next.value
                };
                parsedNote.techniqueSegments.Add(segment);
                maxBend = Mathf.Max(maxBend, Mathf.Abs(segment.startBend), Mathf.Abs(segment.endBend));
            }
        }

        parsedNote.bendStep = maxBend;
        parsedNote.bendPreBend = preBendType;
        parsedNote.bendRelease = parsedNote.techniqueSegments.Any(segment => segment.endBend < segment.startBend - 0.01f);
    }

    private static bool IsPreBendType(int bendType)
    {
        return bendType == BendTypePreBend || bendType == BendTypePreBendRelease;
    }

    private static NoteTechniqueSegmentType ResolveBendSegmentType(float startBend, float endBend)
    {
        return Mathf.Abs(endBend - startBend) <= 0.01f
            ? NoteTechniqueSegmentType.Sustain
            : NoteTechniqueSegmentType.Bend;
    }

    private static List<NoteData> BuildGameplayNotes(List<ParsedGpNote> parsed, List<TempoEvent> tempoMap)
    {
        List<NoteData> result = new List<NoteData>(parsed.Count);
        Dictionary<double, int> chordMap = new Dictionary<double, int>();
        int nextChordId = 0;

        for (int i = 0; i < parsed.Count; i++)
        {
            ParsedGpNote note = parsed[i];
            float startSeconds = (float)QuarterToSeconds(note.quarterPos, tempoMap);
            float durationSeconds = (float)Math.Max(0.0, QuarterToSeconds(note.quarterPos + note.durationQuarter, tempoMap) - QuarterToSeconds(note.quarterPos, tempoMap));
            List<NoteTechniqueSegmentData> techniqueSegments = ConvertTechniqueSegmentsToGameplay(note.techniqueSegments, note.quarterPos, tempoMap);
            bool hasBendSegments = HasBendTechniqueSegments(techniqueSegments);
            float bendVisualStart = hasBendSegments ? startSeconds + GetTechniqueSegmentVisualStart(techniqueSegments) : -1f;
            float bendVisualDuration = hasBendSegments ? GetTechniqueSegmentVisualDuration(techniqueSegments) : 0f;

            double chordKey = Math.Round(note.quarterPos, 6);
            if (!chordMap.TryGetValue(chordKey, out int chordId))
            {
                chordId = nextChordId++;
                chordMap[chordKey] = chordId;
            }

            NoteTechnique technique = NoteTechnique.None;
            if (hasBendSegments || note.bendStep > 0.01f || note.bendPreBend || note.bendRelease)
                technique = NoteTechnique.Bend;
            else if (note.vibrato)
                technique = NoteTechnique.Vibrato;

            result.Add(new NoteData(
                i,
                startSeconds,
                durationSeconds,
                note.stringIdx,
                note.fret,
                note.note,
                chordId,
                technique,
                -1,
                note.bendStep,
                false,
                true,
                -1,
                note.bendPreBend,
                note.bendRelease,
                bendVisualStart,
                bendVisualDuration,
                techniqueSegments,
                note.isMuted));
        }

        for (int i = 0; i < parsed.Count; i++)
        {
            ParsedGpNote current = parsed[i];
            int nextIndex = FindNextLinkedNoteIndex(parsed, i);
            if (nextIndex < 0)
                continue;

            NoteData start = result[i];
            NoteData dest = result[nextIndex];
            if (current.slideStart)
            {
                start.technique = NoteTechnique.Slide;
                start.slideTargetFret = dest.fret;
                start.duration = Mathf.Max(start.duration, Mathf.Max(0.05f, dest.time - start.time));
            }
            else if (current.hammerStart)
            {
                start.technique = NoteTechnique.HammerOn;
            }
            else if (current.pullStart)
            {
                start.technique = NoteTechnique.PullOff;
            }

            result[i] = start;
        }

        return result;
    }

    private static bool HasBendTechniqueSegments(List<NoteTechniqueSegmentData> techniqueSegments)
    {
        if (techniqueSegments == null || techniqueSegments.Count == 0)
            return false;

        for (int i = 0; i < techniqueSegments.Count; i++)
        {
            NoteTechniqueSegmentData segment = techniqueSegments[i];
            if (segment.type == NoteTechniqueSegmentType.Bend)
                return true;

            if ((segment.type == NoteTechniqueSegmentType.Sustain ||
                 segment.type == NoteTechniqueSegmentType.Vibrato) &&
                (Mathf.Abs(segment.startBend) > 0.01f ||
                 Mathf.Abs(segment.endBend) > 0.01f))
            {
                return true;
            }
        }

        return false;
    }

    private static List<NoteTechniqueSegmentData> ConvertTechniqueSegmentsToGameplay(List<ParsedTechniqueSegment> techniqueSegments, double noteQuarterStart, List<TempoEvent> tempoMap)
    {
        List<NoteTechniqueSegmentData> result = new List<NoteTechniqueSegmentData>();
        if (techniqueSegments == null || techniqueSegments.Count == 0)
            return result;

        for (int i = 0; i < techniqueSegments.Count; i++)
        {
            ParsedTechniqueSegment segment = techniqueSegments[i];
            float startOffset = (float)Math.Max(0.0, QuarterToSeconds(segment.startQuarter, tempoMap) - QuarterToSeconds(noteQuarterStart, tempoMap));
            float endOffset = (float)Math.Max(startOffset, QuarterToSeconds(segment.endQuarter, tempoMap) - QuarterToSeconds(noteQuarterStart, tempoMap));
            result.Add(new NoteTechniqueSegmentData(
                segment.type,
                startOffset,
                endOffset,
                segment.startFret,
                segment.endFret,
                segment.startBend,
                segment.endBend));
        }

        return result;
    }

    private static float GetTechniqueSegmentVisualStart(List<NoteTechniqueSegmentData> techniqueSegments)
    {
        if (techniqueSegments == null || techniqueSegments.Count == 0)
            return -1f;

        float start = float.PositiveInfinity;
        for (int i = 0; i < techniqueSegments.Count; i++)
        {
            NoteTechniqueSegmentData segment = techniqueSegments[i];
            if (segment.type != NoteTechniqueSegmentType.Bend &&
                segment.type != NoteTechniqueSegmentType.Sustain &&
                segment.type != NoteTechniqueSegmentType.Vibrato)
                continue;

            start = Mathf.Min(start, segment.startOffset);
        }

        return float.IsPositiveInfinity(start) ? -1f : start;
    }

    private static float GetTechniqueSegmentVisualDuration(List<NoteTechniqueSegmentData> techniqueSegments)
    {
        if (techniqueSegments == null || techniqueSegments.Count == 0)
            return 0f;

        float start = float.PositiveInfinity;
        float end = 0f;
        for (int i = 0; i < techniqueSegments.Count; i++)
        {
            NoteTechniqueSegmentData segment = techniqueSegments[i];
            if (segment.type != NoteTechniqueSegmentType.Bend &&
                segment.type != NoteTechniqueSegmentType.Sustain &&
                segment.type != NoteTechniqueSegmentType.Vibrato)
                continue;

            start = Mathf.Min(start, segment.startOffset);
            end = Mathf.Max(end, segment.endOffset);
        }

        if (float.IsPositiveInfinity(start))
            return 0f;

        return Mathf.Max(0f, end - start);
    }

    private static int FindNextLinkedNoteIndex(List<ParsedGpNote> parsed, int currentIndex)
    {
        ParsedGpNote current = parsed[currentIndex];
        for (int i = currentIndex + 1; i < parsed.Count; i++)
        {
            ParsedGpNote candidate = parsed[i];
            if (candidate.stringIdx != current.stringIdx)
                continue;

            if (candidate.quarterPos <= current.quarterPos)
                continue;

            return i;
        }

        return -1;
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

    private static int ChooseBestTrack(List<MusicXmlLoader.MusicXmlPartSummary> summaries)
    {
        if (summaries == null || summaries.Count == 0)
            return 0;

        MusicXmlLoader.MusicXmlPartSummary summary = summaries
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.NoteCount)
            .First();
        return Mathf.Clamp(summary.Index, 0, summaries.Count - 1);
    }

    private static int ScoreTrackName(string trackName, bool isPercussion)
    {
        if (isPercussion)
            return -500;

        string lower = (trackName ?? string.Empty).ToLowerInvariant();
        int score = 0;
        if (lower.Contains("guitar")) score += 500;
        if (lower.Contains("lead")) score += 200;
        if (lower.Contains("solo")) score += 180;
        if (lower.Contains("slash")) score += 220;
        if (lower.Contains("rhythm")) score += 120;
        if (lower.Contains("bass")) score -= 250;
        if (lower.Contains("drum")) score -= 500;
        if (lower.Contains("voice")) score -= 200;
        if (lower.Contains("vocal")) score -= 200;
        if (lower.Contains("piano")) score -= 100;
        return score;
    }

    private static string InferGpInstrumentType(Gp5Track track)
    {
        if (track == null)
            return string.Empty;

        string text = track.name ?? string.Empty;
        if (track.isPercussionTrack || ContainsAny(text, "drum", "percussion", "kit"))
            return "drums";
        if (ContainsAny(text, "bass"))
            return "bass";
        if (ContainsAny(text, "piano", "keyboard", "keys", "synth"))
            return "piano";
        if (ContainsAny(text, "vocal", "voice", "lyric", "choir"))
            return "vocals";
        if (ContainsAny(text, "guitar", "lead", "rhythm", "solo", "slash"))
            return "guitar";

        int[] tuning = GetTrackTuningPitches(track);
        if (tuning != null && tuning.Length > 0)
            return tuning.Length <= 4 ? "bass" : "guitar";
        if (track.sourceMidiProgram >= 32 && track.sourceMidiProgram <= 39)
            return "bass";
        if (track.sourceMidiProgram >= 24 && track.sourceMidiProgram <= 31)
            return "guitar";
        if (track.sourceMidiProgram >= 0 && track.sourceMidiProgram <= 7)
            return "piano";
        if (track.sourceMidiProgram == 52 || track.sourceMidiProgram == 53)
            return "vocals";

        return string.Empty;
    }

    private static string InferGpRoute(Gp5Track track)
    {
        string instrumentType = InferGpInstrumentType(track);
        if (string.Equals(instrumentType, "drums", StringComparison.OrdinalIgnoreCase))
            return "Drums";
        if (string.Equals(instrumentType, "bass", StringComparison.OrdinalIgnoreCase))
            return "Bass";
        if (string.Equals(instrumentType, "piano", StringComparison.OrdinalIgnoreCase))
            return "Piano";
        if (string.Equals(instrumentType, "vocals", StringComparison.OrdinalIgnoreCase))
            return "Vocals";
        if (string.Equals(instrumentType, "guitar", StringComparison.OrdinalIgnoreCase))
            return ContainsAny(track?.name, "rhythm") ? "Rhythm" : "Lead";

        return string.Empty;
    }

    private static int[] GetTrackTuningPitches(Gp5Track track)
    {
        return track?.stringsHighToLow != null && track.stringsHighToLow.Length > 0
            ? track.stringsHighToLow.Reverse().ToArray()
            : null;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(text) || needles == null)
            return false;

        for (int i = 0; i < needles.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(needles[i]) &&
                text.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetNoteName(int midi)
    {
        int pitchClass = ((midi % 12) + 12) % 12;
        int octave = (midi / 12) - 1;
        return $"{NoteNames[pitchClass]}{octave}";
    }
}
