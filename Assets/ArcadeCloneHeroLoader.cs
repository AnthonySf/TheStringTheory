using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public static class ArcadeCloneHeroLoader
{
    private const int DefaultResolution = 192;
    private const float NaturalHopoBeatFraction = 1f / 3f;
    private static readonly ArcadeDifficulty[] DifficultyOrder =
    {
        ArcadeDifficulty.Expert,
        ArcadeDifficulty.Hard,
        ArcadeDifficulty.Medium,
        ArcadeDifficulty.Easy
    };

    private static readonly ArcadeInstrument[] InstrumentOrder =
    {
        ArcadeInstrument.Guitar,
        ArcadeInstrument.Bass,
        ArcadeInstrument.Rhythm,
        ArcadeInstrument.CoopGuitar,
        ArcadeInstrument.Keys,
        ArcadeInstrument.Drums
    };

    private sealed class RawChartNote
    {
        public long tick;
        public int lane;
        public long sustainTicks;
        public bool isOpen;
        public bool isForced;
        public bool isTap;
    }

    private sealed class RawChartNoteGroup
    {
        public long tick;
        public int chordId;
        public List<RawChartNote> notes = new List<RawChartNote>();
    }

    private readonly struct TempoPoint
    {
        public readonly long tick;
        public readonly double bpm;

        public TempoPoint(long tick, double bpm)
        {
            this.tick = tick;
            this.bpm = bpm;
        }
    }

    private sealed class MidiTrackData
    {
        public string name;
        public List<MidiNoteEvent> notes = new List<MidiNoteEvent>();
    }

    private readonly struct MidiNoteEvent
    {
        public readonly long startTick;
        public readonly long endTick;
        public readonly int pitch;

        public MidiNoteEvent(long startTick, long endTick, int pitch)
        {
            this.startTick = startTick;
            this.endTick = endTick;
            this.pitch = pitch;
        }
    }

    public static ArcadeChartData Load(string chartPath, string arrangementId, ArcadeDifficulty difficulty)
    {
        ArcadeChartData empty = new ArcadeChartData
        {
            SourcePath = chartPath,
            LaneCount = 5
        };

        if (string.IsNullOrWhiteSpace(chartPath) || !File.Exists(chartPath))
        {
            Debug.LogWarning($"[ArcadeCloneHeroLoader] Chart file not found: {chartPath}");
            return empty;
        }

        string extension = Path.GetExtension(chartPath)?.ToLowerInvariant();
        try
        {
            if (extension == ".chart")
                return LoadChartFile(chartPath, arrangementId, difficulty);

            if (extension == ".mid" || extension == ".midi")
                return LoadMidiFile(chartPath, arrangementId, difficulty);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ArcadeCloneHeroLoader] Failed to load '{chartPath}': {ex.Message}");
        }

        return empty;
    }

    public static List<ArcadeArrangementSummary> GetArrangementSummaries(string chartPath)
    {
        if (string.IsNullOrWhiteSpace(chartPath) || !File.Exists(chartPath))
            return new List<ArcadeArrangementSummary>();

        string extension = Path.GetExtension(chartPath)?.ToLowerInvariant();
        try
        {
            if (extension == ".chart")
                return GetChartArrangementSummaries(chartPath);

            if (extension == ".mid" || extension == ".midi")
                return GetMidiArrangementSummaries(chartPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ArcadeCloneHeroLoader] Failed to inspect '{chartPath}': {ex.Message}");
        }

        return new List<ArcadeArrangementSummary>();
    }

    public static ArcadeDifficulty GetBestDefaultDifficulty(IReadOnlyList<ArcadeDifficulty> difficulties)
    {
        if (difficulties == null || difficulties.Count == 0)
            return ArcadeDifficulty.Expert;

        for (int i = 0; i < DifficultyOrder.Length; i++)
        {
            if (difficulties.Contains(DifficultyOrder[i]))
                return DifficultyOrder[i];
        }

        return difficulties[0];
    }

    public static string GetDifficultyLabel(ArcadeDifficulty difficulty)
    {
        switch (difficulty)
        {
            case ArcadeDifficulty.Expert:
                return "X";
            case ArcadeDifficulty.Hard:
                return "H";
            case ArcadeDifficulty.Medium:
                return "M";
            default:
                return "E";
        }
    }

    public static string SerializeDifficulty(ArcadeDifficulty difficulty)
    {
        return difficulty.ToString();
    }

    public static ArcadeDifficulty ParseDifficulty(string value, ArcadeDifficulty fallback)
    {
        return Enum.TryParse(value, true, out ArcadeDifficulty parsed) ? parsed : fallback;
    }

    public static string GetArrangementDisplayName(ArcadeInstrument instrument)
    {
        switch (instrument)
        {
            case ArcadeInstrument.Bass:
                return "Bass";
            case ArcadeInstrument.Rhythm:
                return "Rhythm Guitar";
            case ArcadeInstrument.CoopGuitar:
                return "Co-op Guitar";
            case ArcadeInstrument.Keys:
                return "Keys";
            case ArcadeInstrument.Drums:
                return "Drums";
            default:
                return "Lead Guitar";
        }
    }

    public static string GetArrangementId(ArcadeInstrument instrument)
    {
        return instrument.ToString();
    }

    private static ArcadeChartData LoadChartFile(string chartPath, string arrangementId, ArcadeDifficulty difficulty)
    {
        Dictionary<string, List<string>> sections = ReadChartSections(chartPath);
        int resolution = ParseChartResolution(sections);
        List<TempoPoint> tempos = ParseChartTempoMap(sections);
        List<ArcadeArrangementSummary> summaries = BuildChartSummaries(sections);
        ArcadeInstrument instrument = ResolveInstrument(arrangementId, summaries);
        string sectionName = GetChartSectionName(difficulty, instrument);
        List<RawChartNote> rawNotes = sections.TryGetValue(sectionName, out List<string> lines)
            ? ParseChartNotes(lines)
            : new List<RawChartNote>();

        List<ArcadeNoteData> notes = ConvertRawNotes(rawNotes, resolution, tempos);
        float duration = notes.Count > 0 ? notes.Max(note => note.time + Mathf.Max(0.05f, note.duration)) : 0f;
        return new ArcadeChartData
        {
            SourcePath = chartPath,
            LaneCount = 5,
            Arrangements = summaries,
            Notes = notes,
            DurationSeconds = duration
        };
    }

    private static List<ArcadeArrangementSummary> GetChartArrangementSummaries(string chartPath)
    {
        return BuildChartSummaries(ReadChartSections(chartPath));
    }

    private static Dictionary<string, List<string>> ReadChartSections(string chartPath)
    {
        Dictionary<string, List<string>> sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string currentSection = null;
        bool inBody = false;

        foreach (string rawLine in File.ReadAllLines(chartPath))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//", StringComparison.Ordinal))
                continue;

            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                currentSection = line.Substring(1, line.Length - 2).Trim();
                if (!sections.ContainsKey(currentSection))
                    sections[currentSection] = new List<string>();
                inBody = false;
                continue;
            }

            if (line == "{")
            {
                inBody = true;
                continue;
            }

            if (line == "}")
            {
                inBody = false;
                currentSection = null;
                continue;
            }

            if (currentSection != null && inBody)
                sections[currentSection].Add(line);
        }

        return sections;
    }

    private static int ParseChartResolution(Dictionary<string, List<string>> sections)
    {
        if (!sections.TryGetValue("Song", out List<string> songLines))
            return DefaultResolution;

        foreach (string line in songLines)
        {
            if (!TryParseKeyValue(line, out string key, out string value))
                continue;

            if (key.Equals("Resolution", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int resolution))
            {
                return Mathf.Max(1, resolution);
            }
        }

        return DefaultResolution;
    }

    private static List<TempoPoint> ParseChartTempoMap(Dictionary<string, List<string>> sections)
    {
        List<TempoPoint> tempos = new List<TempoPoint> { new TempoPoint(0, 120.0) };
        if (!sections.TryGetValue("SyncTrack", out List<string> lines))
            return tempos;

        foreach (string line in lines)
        {
            string[] parts = line.Split(new[] { ' ', '\t', '=' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                continue;

            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long tick))
                continue;

            if (!parts[1].Equals("B", StringComparison.OrdinalIgnoreCase))
                continue;

            if (double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double rawBpm))
                tempos.Add(new TempoPoint(tick, Math.Max(1.0, rawBpm / 1000.0)));
        }

        return tempos.OrderBy(point => point.tick).ToList();
    }

    private static bool TryParseKeyValue(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        int equals = line.IndexOf('=');
        if (equals <= 0)
            return false;

        key = line.Substring(0, equals).Trim();
        value = line.Substring(equals + 1).Trim().Trim('"');
        return !string.IsNullOrWhiteSpace(key);
    }

    private static List<ArcadeArrangementSummary> BuildChartSummaries(Dictionary<string, List<string>> sections)
    {
        List<ArcadeArrangementSummary> summaries = new List<ArcadeArrangementSummary>();
        for (int i = 0; i < InstrumentOrder.Length; i++)
        {
            ArcadeInstrument instrument = InstrumentOrder[i];
            List<ArcadeDifficulty> difficulties = new List<ArcadeDifficulty>();
            for (int d = 0; d < DifficultyOrder.Length; d++)
            {
                ArcadeDifficulty difficulty = DifficultyOrder[d];
                if (sections.ContainsKey(GetChartSectionName(difficulty, instrument)) &&
                    SectionHasPlayableNotes(sections[GetChartSectionName(difficulty, instrument)]))
                {
                    difficulties.Add(difficulty);
                }
            }

            if (difficulties.Count == 0)
                continue;

            summaries.Add(new ArcadeArrangementSummary
            {
                ArrangementId = GetArrangementId(instrument),
                DisplayName = GetArrangementDisplayName(instrument),
                Instrument = instrument,
                Difficulties = difficulties
            });
        }

        return summaries;
    }

    private static bool SectionHasPlayableNotes(List<string> lines)
    {
        if (lines == null)
            return false;

        for (int i = 0; i < lines.Count; i++)
        {
            string[] parts = lines[i].Split(new[] { ' ', '\t', '=' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && parts[1].Equals("N", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int lane) &&
                    (lane >= 0 && lane <= 5))
                    return true;
            }
        }

        return false;
    }

    private static string GetChartSectionName(ArcadeDifficulty difficulty, ArcadeInstrument instrument)
    {
        return $"{GetChartDifficultyPrefix(difficulty)}{GetChartInstrumentSuffix(instrument)}";
    }

    private static string GetChartDifficultyPrefix(ArcadeDifficulty difficulty)
    {
        switch (difficulty)
        {
            case ArcadeDifficulty.Expert:
                return "Expert";
            case ArcadeDifficulty.Hard:
                return "Hard";
            case ArcadeDifficulty.Medium:
                return "Medium";
            default:
                return "Easy";
        }
    }

    private static string GetChartInstrumentSuffix(ArcadeInstrument instrument)
    {
        switch (instrument)
        {
            case ArcadeInstrument.Bass:
                return "DoubleBass";
            case ArcadeInstrument.Rhythm:
                return "DoubleRhythm";
            case ArcadeInstrument.CoopGuitar:
                return "DoubleGuitar";
            case ArcadeInstrument.Keys:
                return "Keyboard";
            case ArcadeInstrument.Drums:
                return "Drums";
            default:
                return "Single";
        }
    }

    private static List<RawChartNote> ParseChartNotes(List<string> lines)
    {
        List<RawChartNote> notes = new List<RawChartNote>();
        HashSet<long> forcedTicks = new HashSet<long>();
        HashSet<long> tapTicks = new HashSet<long>();
        if (lines == null)
            return notes;

        foreach (string line in lines)
        {
            string[] parts = line.Split(new[] { ' ', '\t', '=' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4 || !parts[1].Equals("N", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long tick))
                continue;

            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int lane))
                continue;

            long sustain = 0;
            if (parts.Length >= 4)
                long.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out sustain);

            if (lane >= 0 && lane <= 4)
            {
                notes.Add(new RawChartNote { tick = tick, lane = lane, sustainTicks = Math.Max(0, sustain) });
            }
            else if (lane == 5)
            {
                notes.Add(new RawChartNote { tick = tick, lane = -1, sustainTicks = Math.Max(0, sustain), isOpen = true });
            }
            else if (lane == 6)
            {
                forcedTicks.Add(tick);
            }
            else if (lane == 7)
            {
                tapTicks.Add(tick);
            }
        }

        for (int i = 0; i < notes.Count; i++)
        {
            RawChartNote note = notes[i];
            note.isForced = forcedTicks.Contains(note.tick);
            note.isTap = tapTicks.Contains(note.tick);
        }

        return notes.OrderBy(note => note.tick).ThenBy(note => note.lane).ToList();
    }

    private static List<ArcadeNoteData> ConvertRawNotes(List<RawChartNote> rawNotes, int resolution, List<TempoPoint> tempos)
    {
        List<ArcadeNoteData> notes = new List<ArcadeNoteData>();
        if (rawNotes == null || rawNotes.Count == 0)
            return notes;

        List<RawChartNoteGroup> groups = BuildRawNoteGroups(rawNotes);
        RawChartNoteGroup previousGroup = null;
        int nextId = 0;

        foreach (RawChartNoteGroup group in groups)
        {
            bool isTap = group.notes.Any(note => note.isTap);
            bool isForced = group.notes.Any(note => note.isForced);
            bool naturalHopo = IsNaturalHopoGroup(group, previousGroup, resolution);
            bool isHopo = !isTap && (isForced ? !naturalHopo : naturalHopo);

            foreach (RawChartNote raw in group.notes)
            {
                float start = (float)TicksToSeconds(raw.tick, resolution, tempos);
                float end = (float)TicksToSeconds(raw.tick + raw.sustainTicks, resolution, tempos);
                float duration = Mathf.Max(0f, end - start);
                float sustainBeats = resolution > 0 ? Mathf.Max(0f, raw.sustainTicks / (float)resolution) : 0f;
                notes.Add(new ArcadeNoteData(nextId++, start, duration, sustainBeats, raw.lane, raw.isOpen, isHopo, isTap, group.chordId));
            }

            previousGroup = group;
        }

        return notes;
    }

    private static List<RawChartNoteGroup> BuildRawNoteGroups(List<RawChartNote> rawNotes)
    {
        List<RawChartNoteGroup> groups = new List<RawChartNoteGroup>();
        int nextChordId = 0;

        foreach (RawChartNote raw in rawNotes.OrderBy(note => note.tick).ThenBy(note => note.lane))
        {
            RawChartNoteGroup group = groups.Count > 0 ? groups[groups.Count - 1] : null;
            if (group == null || Math.Abs(raw.tick - group.tick) > 2)
            {
                group = new RawChartNoteGroup
                {
                    tick = raw.tick,
                    chordId = nextChordId++
                };
                groups.Add(group);
            }

            group.notes.Add(raw);
        }

        for (int i = 0; i < groups.Count; i++)
            NormalizeImpossibleOpenChord(groups[i]);

        return groups;
    }

    private static void NormalizeImpossibleOpenChord(RawChartNoteGroup group)
    {
        if (group == null || group.notes == null || group.notes.Count == 0)
            return;

        bool hasOpen = group.notes.Any(note => note.isOpen);
        bool hasFretted = group.notes.Any(note => !note.isOpen && note.lane >= 0 && note.lane <= 4);
        if (!hasOpen || !hasFretted)
            return;

        group.notes = group.notes.Where(note => !note.isOpen).ToList();
    }

    private static bool IsNaturalHopoGroup(RawChartNoteGroup current, RawChartNoteGroup previous, int resolution)
    {
        if (current == null || previous == null || current.notes.Count != 1 || previous.notes.Count != 1)
            return false;

        long maxGapTicks = Math.Max(1, (long)Math.Round(Mathf.Max(1, resolution) * NaturalHopoBeatFraction));
        if (current.tick - previous.tick > maxGapTicks)
            return false;

        RawChartNote currentNote = current.notes[0];
        RawChartNote previousNote = previous.notes[0];
        return currentNote.lane != previousNote.lane || currentNote.isOpen != previousNote.isOpen;
    }

    private static double TicksToSeconds(long tick, int resolution, List<TempoPoint> tempos)
    {
        if (tick <= 0)
            return 0.0;

        List<TempoPoint> orderedTempos = tempos != null && tempos.Count > 0
            ? tempos.OrderBy(point => point.tick).ToList()
            : new List<TempoPoint> { new TempoPoint(0, 120.0) };

        double seconds = 0.0;
        long previousTick = 0;
        double currentBpm = orderedTempos[0].bpm;

        for (int i = 1; i < orderedTempos.Count; i++)
        {
            TempoPoint point = orderedTempos[i];
            if (point.tick >= tick)
                break;

            seconds += TicksToSecondsDelta(point.tick - previousTick, resolution, currentBpm);
            previousTick = point.tick;
            currentBpm = point.bpm;
        }

        seconds += TicksToSecondsDelta(tick - previousTick, resolution, currentBpm);
        return seconds;
    }

    private static double TicksToSecondsDelta(long ticks, int resolution, double bpm)
    {
        return Math.Max(0L, ticks) * (60.0 / Math.Max(1.0, bpm)) / Math.Max(1, resolution);
    }

    private static ArcadeChartData LoadMidiFile(string chartPath, string arrangementId, ArcadeDifficulty difficulty)
    {
        MidiParseResult parsed = ParseMidi(chartPath);
        List<ArcadeArrangementSummary> summaries = BuildMidiSummaries(parsed);
        ArcadeInstrument instrument = ResolveInstrument(arrangementId, summaries);
        MidiTrackData track = FindMidiTrackForInstrument(parsed.tracks, instrument);
        List<ArcadeNoteData> notes = track != null
            ? ConvertMidiNotes(track.notes, parsed.timeDivision, parsed.tempos, difficulty)
            : new List<ArcadeNoteData>();

        float duration = notes.Count > 0 ? notes.Max(note => note.time + Mathf.Max(0.05f, note.duration)) : 0f;
        return new ArcadeChartData
        {
            SourcePath = chartPath,
            LaneCount = 5,
            Arrangements = summaries,
            Notes = notes,
            DurationSeconds = duration
        };
    }

    private static List<ArcadeArrangementSummary> GetMidiArrangementSummaries(string chartPath)
    {
        return BuildMidiSummaries(ParseMidi(chartPath));
    }

    private sealed class MidiParseResult
    {
        public int timeDivision = DefaultResolution;
        public List<TempoPoint> tempos = new List<TempoPoint> { new TempoPoint(0, 120.0) };
        public List<MidiTrackData> tracks = new List<MidiTrackData>();
    }

    private static MidiParseResult ParseMidi(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        MidiParseResult result = new MidiParseResult();
        List<byte[]> chunks = new List<byte[]>();

        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            string header = ReadAscii(reader, 4);
            if (header != "MThd")
                throw new InvalidDataException("Invalid MIDI header.");

            int headerLength = ReadInt32BE(reader);
            reader.ReadUInt16BigEndian();
            int trackCount = reader.ReadUInt16BigEndian();
            result.timeDivision = reader.ReadUInt16BigEndian();
            if (headerLength > 6)
                reader.ReadBytes(headerLength - 6);

            for (int i = 0; i < trackCount; i++)
            {
                if (stream.Position + 8 > stream.Length)
                    break;

                string chunkId = ReadAscii(reader, 4);
                int length = ReadInt32BE(reader);
                if (chunkId != "MTrk")
                {
                    reader.ReadBytes(length);
                    continue;
                }

                chunks.Add(reader.ReadBytes(length));
            }
        }

        if ((result.timeDivision & 0x8000) != 0)
            throw new NotSupportedException("SMPTE MIDI timing is not supported for Clone Hero charts.");

        result.tempos = BuildMidiTempoMap(chunks, result.timeDivision);
        for (int i = 0; i < chunks.Count; i++)
            result.tracks.Add(ParseMidiTrack(chunks[i]));

        return result;
    }

    private static List<TempoPoint> BuildMidiTempoMap(List<byte[]> chunks, int timeDivision)
    {
        List<TempoPoint> tempos = new List<TempoPoint> { new TempoPoint(0, 120.0) };
        foreach (byte[] chunk in chunks)
        {
            using (MemoryStream stream = new MemoryStream(chunk))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                long tick = 0;
                byte runningStatus = 0;
                while (stream.Position < stream.Length)
                {
                    tick += ReadVariableLength(reader);
                    if (stream.Position >= stream.Length)
                        break;

                    byte status = reader.ReadByte();
                    if (status < 0x80)
                    {
                        stream.Position--;
                        status = runningStatus;
                    }
                    else
                    {
                        runningStatus = status;
                    }

                    if (status == 0xFF)
                    {
                        int metaType = reader.ReadByte();
                        int length = ReadVariableLength(reader);
                        byte[] meta = reader.ReadBytes(length);
                        if (metaType == 0x51 && meta.Length >= 3)
                        {
                            int mpqn = (meta[0] << 16) | (meta[1] << 8) | meta[2];
                            double bpm = 60000000.0 / Math.Max(1, mpqn);
                            tempos.Add(new TempoPoint(tick, bpm));
                        }
                    }
                    else
                    {
                        SkipMidiEventData(reader, status);
                    }
                }
            }
        }

        return tempos.OrderBy(point => point.tick).ToList();
    }

    private static MidiTrackData ParseMidiTrack(byte[] chunk)
    {
        MidiTrackData track = new MidiTrackData();
        Dictionary<int, Queue<long>> activeNotes = new Dictionary<int, Queue<long>>();
        using (MemoryStream stream = new MemoryStream(chunk))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            long tick = 0;
            byte runningStatus = 0;
            while (stream.Position < stream.Length)
            {
                tick += ReadVariableLength(reader);
                if (stream.Position >= stream.Length)
                    break;

                byte status = reader.ReadByte();
                if (status < 0x80)
                {
                    stream.Position--;
                    status = runningStatus;
                }
                else
                {
                    runningStatus = status;
                }

                if (status == 0xFF)
                {
                    int metaType = reader.ReadByte();
                    int length = ReadVariableLength(reader);
                    byte[] meta = reader.ReadBytes(length);
                    if ((metaType == 0x03 || metaType == 0x04) && string.IsNullOrWhiteSpace(track.name))
                        track.name = DecodeMidiText(meta);
                    continue;
                }

                int eventType = status & 0xF0;
                if (eventType == 0x90 || eventType == 0x80)
                {
                    int pitch = reader.ReadByte();
                    int velocity = reader.ReadByte();
                    bool noteOn = eventType == 0x90 && velocity > 0;
                    if (noteOn)
                    {
                        if (!activeNotes.TryGetValue(pitch, out Queue<long> starts))
                        {
                            starts = new Queue<long>();
                            activeNotes[pitch] = starts;
                        }
                        starts.Enqueue(tick);
                    }
                    else if (activeNotes.TryGetValue(pitch, out Queue<long> starts) && starts.Count > 0)
                    {
                        long start = starts.Dequeue();
                        track.notes.Add(new MidiNoteEvent(start, Math.Max(start, tick), pitch));
                    }
                }
                else
                {
                    SkipMidiEventData(reader, status);
                }
            }
        }

        track.notes = track.notes.OrderBy(note => note.startTick).ThenBy(note => note.pitch).ToList();
        return track;
    }

    private static List<ArcadeArrangementSummary> BuildMidiSummaries(MidiParseResult parsed)
    {
        List<ArcadeArrangementSummary> summaries = new List<ArcadeArrangementSummary>();
        if (parsed == null || parsed.tracks == null)
            return summaries;

        for (int i = 0; i < InstrumentOrder.Length; i++)
        {
            ArcadeInstrument instrument = InstrumentOrder[i];
            MidiTrackData track = FindMidiTrackForInstrument(parsed.tracks, instrument);
            if (track == null)
                continue;

            List<ArcadeDifficulty> difficulties = new List<ArcadeDifficulty>();
            for (int d = 0; d < DifficultyOrder.Length; d++)
            {
                ArcadeDifficulty difficulty = DifficultyOrder[d];
                if (track.notes.Any(note => TryMapMidiPitchToLane(note.pitch, difficulty, out _, out _)))
                    difficulties.Add(difficulty);
            }

            if (difficulties.Count <= 0)
                continue;

            summaries.Add(new ArcadeArrangementSummary
            {
                ArrangementId = GetArrangementId(instrument),
                DisplayName = GetArrangementDisplayName(instrument),
                Instrument = instrument,
                Difficulties = difficulties
            });
        }

        return summaries;
    }

    private static List<ArcadeNoteData> ConvertMidiNotes(List<MidiNoteEvent> source, int resolution, List<TempoPoint> tempos, ArcadeDifficulty difficulty)
    {
        List<RawChartNote> raw = new List<RawChartNote>();
        HashSet<long> forcedTicks = new HashSet<long>();
        HashSet<long> tapTicks = new HashSet<long>();
        foreach (MidiNoteEvent note in source)
        {
            if (!TryMapMidiPitchToLane(note.pitch, difficulty, out int lane, out bool isOpen))
            {
                if (TryMapMidiPitchToMarker(note.pitch, difficulty, out ArcadeMidiMarker marker))
                {
                    if (marker == ArcadeMidiMarker.Forced)
                        forcedTicks.Add(note.startTick);
                    else if (marker == ArcadeMidiMarker.Tap)
                        tapTicks.Add(note.startTick);
                }

                continue;
            }

            raw.Add(new RawChartNote
            {
                tick = note.startTick,
                sustainTicks = Math.Max(0, note.endTick - note.startTick),
                lane = lane,
                isOpen = isOpen
            });
        }

        for (int i = 0; i < raw.Count; i++)
        {
            RawChartNote note = raw[i];
            note.isForced = forcedTicks.Contains(note.tick);
            note.isTap = tapTicks.Contains(note.tick);
        }

        return ConvertRawNotes(raw, resolution, tempos);
    }

    private enum ArcadeMidiMarker
    {
        Forced,
        Tap
    }

    private static bool TryMapMidiPitchToLane(int pitch, ArcadeDifficulty difficulty, out int lane, out bool isOpen)
    {
        isOpen = false;
        int basePitch;
        switch (difficulty)
        {
            case ArcadeDifficulty.Expert:
                basePitch = 96;
                break;
            case ArcadeDifficulty.Hard:
                basePitch = 84;
                break;
            case ArcadeDifficulty.Medium:
                basePitch = 72;
                break;
            default:
                basePitch = 60;
                break;
        }

        int offset = pitch - basePitch;
        if (offset >= 0 && offset <= 4)
        {
            lane = offset;
            return true;
        }

        if (offset == 5)
        {
            lane = -1;
            isOpen = true;
            return true;
        }

        lane = -1;
        return false;
    }

    private static bool TryMapMidiPitchToMarker(int pitch, ArcadeDifficulty difficulty, out ArcadeMidiMarker marker)
    {
        marker = ArcadeMidiMarker.Forced;
        int basePitch;
        switch (difficulty)
        {
            case ArcadeDifficulty.Expert:
                basePitch = 96;
                break;
            case ArcadeDifficulty.Hard:
                basePitch = 84;
                break;
            case ArcadeDifficulty.Medium:
                basePitch = 72;
                break;
            default:
                basePitch = 60;
                break;
        }

        int offset = pitch - basePitch;
        if (offset == 6)
        {
            marker = ArcadeMidiMarker.Forced;
            return true;
        }

        if (offset == 7)
        {
            marker = ArcadeMidiMarker.Tap;
            return true;
        }

        return false;
    }

    private static MidiTrackData FindMidiTrackForInstrument(List<MidiTrackData> tracks, ArcadeInstrument instrument)
    {
        if (tracks == null)
            return null;

        string[] tokens = GetMidiTrackNameTokens(instrument);
        return tracks.FirstOrDefault(track =>
        {
            string name = (track?.name ?? string.Empty).ToUpperInvariant();
            return tokens.Any(token => name.Contains(token));
        });
    }

    private static string[] GetMidiTrackNameTokens(ArcadeInstrument instrument)
    {
        switch (instrument)
        {
            case ArcadeInstrument.Bass:
                return new[] { "PART BASS" };
            case ArcadeInstrument.Rhythm:
                return new[] { "PART RHYTHM" };
            case ArcadeInstrument.CoopGuitar:
                return new[] { "PART GUITAR COOP", "PART COOP", "PART GUITAR CO-OP" };
            case ArcadeInstrument.Keys:
                return new[] { "PART KEYS", "PART REAL_KEYS_X", "PART REAL_KEYS" };
            case ArcadeInstrument.Drums:
                return new[] { "PART DRUMS" };
            default:
                return new[] { "PART GUITAR" };
        }
    }

    private static ArcadeInstrument ResolveInstrument(string arrangementId, List<ArcadeArrangementSummary> summaries)
    {
        if (!string.IsNullOrWhiteSpace(arrangementId) &&
            Enum.TryParse(arrangementId, true, out ArcadeInstrument parsed))
        {
            return parsed;
        }

        if (summaries != null && summaries.Count > 0)
            return summaries[0].Instrument;

        return ArcadeInstrument.Guitar;
    }

    private static void SkipMidiEventData(BinaryReader reader, byte status)
    {
        int eventType = status & 0xF0;
        if (status == 0xF0 || status == 0xF7)
        {
            int length = ReadVariableLength(reader);
            reader.ReadBytes(length);
            return;
        }

        if (eventType == 0x80 || eventType == 0x90 || eventType == 0xA0 || eventType == 0xB0 || eventType == 0xE0)
        {
            reader.ReadByte();
            reader.ReadByte();
            return;
        }

        if (eventType == 0xC0 || eventType == 0xD0)
            reader.ReadByte();
    }

    private static int ReadVariableLength(BinaryReader reader)
    {
        int value = 0;
        byte b;
        do
        {
            b = reader.ReadByte();
            value = (value << 7) | (b & 0x7F);
        }
        while ((b & 0x80) != 0);

        return value;
    }

    private static string ReadAscii(BinaryReader reader, int count)
    {
        return Encoding.ASCII.GetString(reader.ReadBytes(count));
    }

    private static int ReadInt32BE(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }

    private static ushort ReadUInt16BigEndian(this BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(2);
        return (ushort)((bytes[0] << 8) | bytes[1]);
    }

    private static string DecodeMidiText(byte[] data)
    {
        if (data == null || data.Length == 0)
            return string.Empty;

        return Encoding.UTF8.GetString(data).Trim('\0', ' ', '\t', '\r', '\n');
    }
}
