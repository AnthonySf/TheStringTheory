using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

internal static class Gp5Reader
{
    private const int BendPosition = 60;
    private const float BendSemitone = 50f;
    private static readonly Encoding GpEncoding;

    static Gp5Reader()
    {
        try
        {
            GpEncoding = Encoding.GetEncoding(1252);
        }
        catch
        {
            GpEncoding = Encoding.UTF8;
        }
    }

    public static Gp5Song Parse(string filePath)
    {
        using (FileStream stream = File.OpenRead(filePath))
        using (BinaryReader reader = new BinaryReader(stream, GpEncoding))
        {
            Gp5BinaryReader gp = new Gp5BinaryReader(reader);
            return gp.ReadSong(filePath);
        }
    }

    private sealed class Gp5BinaryReader
    {
        private readonly BinaryReader reader;
        private Version version = new Version(5, 0, 0);
        private readonly List<Gp5MidiChannel> channels = new List<Gp5MidiChannel>(64);
        private int currentMeasureIndex = -1;
        private int currentTrackIndex = -1;
        private int currentVoiceIndex = -1;
        private int currentBeatIndex = -1;

        public Gp5BinaryReader(BinaryReader reader)
        {
            this.reader = reader;
        }

        public Gp5Song ReadSong(string filePath)
        {
            try
            {
                Gp5Song song = new Gp5Song
                {
                    filePath = filePath
                };

                song.version = ReadByteSizeString(30);
                version = ParseVersion(song.version);

                ReadInfo(song);
                ReadLyrics();
                ReadRseMasterEffect();
                ReadPageSetup();
                song.tempoName = ReadIntByteSizeString();
                song.initialTempo = ReadInt32();
                if (version > new Version(5, 0, 0))
                    ReadBool();

                ReadSByte();
                ReadInt32();

                channels.Clear();
                channels.AddRange(ReadMidiChannels());
                ReadDirections();
                ReadInt32();

                int measureCount = ReadInt32();
                int trackCount = ReadInt32();

                List<Gp5MeasureHeader> headers = ReadMeasureHeaders(measureCount);
                song.measureHeaders.AddRange(headers);

                List<Gp5Track> tracks = ReadTracks(trackCount);
                song.tracks.AddRange(tracks);

                ReadMeasures(song);

                if (song.tempoChanges.All(change => Math.Abs(change.quarterPos) > 0.0001))
                {
                    song.tempoChanges.Insert(0, new Gp5TempoChange
                    {
                        quarterPos = 0.0,
                        bpm = Math.Max(1.0, song.initialTempo)
                    });
                }
                else if (song.tempoChanges.Count == 0)
                {
                    song.tempoChanges.Add(new Gp5TempoChange
                    {
                        quarterPos = 0.0,
                        bpm = Math.Max(1.0, song.initialTempo)
                    });
                }

                song.tempoChanges = song.tempoChanges
                    .OrderBy(change => change.quarterPos)
                    .ThenBy(change => change.bpm)
                    .ToList();

                return song;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    $"GP5 parse failed at stream position {reader.BaseStream.Position} " +
                    $"(measure={currentMeasureIndex}, track={currentTrackIndex}, voice={currentVoiceIndex}, beat={currentBeatIndex}).",
                    ex);
            }
        }

        private void ReadInfo(Gp5Song song)
        {
            song.title = ReadIntByteSizeString();
            song.subtitle = ReadIntByteSizeString();
            song.artist = ReadIntByteSizeString();
            song.album = ReadIntByteSizeString();
            song.words = ReadIntByteSizeString();
            song.music = ReadIntByteSizeString();
            song.copyright = ReadIntByteSizeString();
            song.tabbedBy = ReadIntByteSizeString();
            song.instructions = ReadIntByteSizeString();

            int noticeCount = ReadInt32();
            for (int i = 0; i < noticeCount; i++)
                ReadIntByteSizeString();
        }

        private void ReadLyrics()
        {
            ReadInt32();
            for (int i = 0; i < 5; i++)
            {
                ReadInt32();
                ReadIntSizeString();
            }
        }

        private void ReadRseMasterEffect()
        {
            if (version <= new Version(5, 0, 0))
                return;

            ReadInt32();
            ReadInt32();
            for (int i = 0; i < 11; i++)
                ReadSByte();
        }

        private void ReadPageSetup()
        {
            for (int i = 0; i < 7; i++)
                ReadInt32();

            ReadByte();
            ReadByte();

            for (int i = 0; i < 10; i++)
                ReadIntByteSizeString();
        }

        private List<Gp5MidiChannel> ReadMidiChannels()
        {
            List<Gp5MidiChannel> result = new List<Gp5MidiChannel>(64);
            for (int i = 0; i < 64; i++)
            {
                Gp5MidiChannel channel = new Gp5MidiChannel
                {
                    index = i,
                    instrument = ReadInt32(),
                    volume = ReadByte(),
                    balance = ReadByte(),
                    chorus = ReadByte(),
                    reverb = ReadByte(),
                    phaser = ReadByte(),
                    tremolo = ReadByte()
                };

                ReadByte();
                ReadByte();
                result.Add(channel);
            }

            return result;
        }

        private void ReadDirections()
        {
            for (int i = 0; i < 19; i++)
                ReadInt16();
        }

        private List<Gp5MeasureHeader> ReadMeasureHeaders(int measureCount)
        {
            List<Gp5MeasureHeader> headers = new List<Gp5MeasureHeader>(measureCount);
            Gp5MeasureHeader previous = null;
            double startQuarter = 0.0;
            int currentNumerator = 4;
            int currentDenominator = 4;

            for (int i = 0; i < measureCount; i++)
            {
                if (previous != null)
                    Skip(1);

                byte flags = ReadByte();
                Gp5MeasureHeader header = new Gp5MeasureHeader
                {
                    number = i + 1,
                    numerator = currentNumerator,
                    denominator = currentDenominator,
                    startQuarter = startQuarter
                };

                if ((flags & 0x01) != 0)
                    header.numerator = ReadSByte();
                if ((flags & 0x02) != 0)
                    header.denominator = ReadSByte();
                if ((flags & 0x04) != 0)
                    header.isRepeatOpen = true;
                if ((flags & 0x08) != 0)
                    header.repeatClose = ReadSByte();
                if ((flags & 0x20) != 0)
                    header.markerName = ReadMarkerName();
                if ((flags & 0x40) != 0)
                {
                    ReadSByte();
                    ReadSByte();
                }

                if ((flags & 0x10) != 0)
                    header.repeatAlternative = ReadByte();

                if ((flags & 0x03) != 0)
                {
                    ReadByte();
                    ReadByte();
                    ReadByte();
                    ReadByte();
                }

                if ((flags & 0x10) == 0)
                    Skip(1);

                ReadByte();

                header.hasDoubleBar = (flags & 0x80) != 0;
                header.lengthQuarter = ComputeMeasureLengthQuarter(header.numerator, header.denominator);
                headers.Add(header);

                startQuarter += header.lengthQuarter;
                currentNumerator = header.numerator;
                currentDenominator = header.denominator;
                previous = header;
            }

            return headers;
        }

        private List<Gp5Track> ReadTracks(int trackCount)
        {
            List<Gp5Track> tracks = new List<Gp5Track>(trackCount);
            for (int i = 0; i < trackCount; i++)
            {
                Gp5Track track = new Gp5Track
                {
                    index = i,
                    partId = $"gp5-track-{i}"
                };

                if (i == 0 || version == new Version(5, 0, 0))
                    Skip(1);

                byte flags1 = ReadByte();
                track.isPercussionTrack = (flags1 & 0x01) != 0;
                track.isVisible = (flags1 & 0x08) != 0;
                track.isSolo = (flags1 & 0x10) != 0;
                track.isMuted = (flags1 & 0x20) != 0;
                track.useRse = (flags1 & 0x40) != 0;
                track.name = ReadByteSizeString(40);

                int stringCount = ReadInt32();
                List<int> strings = new List<int>(stringCount);
                for (int stringIndex = 0; stringIndex < 7; stringIndex++)
                {
                    int tuning = ReadInt32();
                    if (stringIndex < stringCount)
                        strings.Add(tuning);
                }

                track.stringsHighToLow = strings.ToArray();
                track.port = ReadInt32();
                Gp5MidiChannel channel = ReadChannel();
                track.sourceMidiChannel = channel != null ? channel.index : -1;
                track.sourceMidiProgram = channel != null ? channel.instrument : -1;
                if (channel != null && channel.IsPercussion)
                    track.isPercussionTrack = true;

                track.fretCount = ReadInt32();
                track.capo = ReadInt32();
                ReadColor();

                ReadInt16();
                ReadByte();
                track.midiBank = ReadByte();
                ReadTrackRse();

                tracks.Add(track);
            }

            Skip(version == new Version(5, 0, 0) ? 2 : 1);
            return tracks;
        }

        private void ReadMeasures(Gp5Song song)
        {
            for (int measureIndex = 0; measureIndex < song.measureHeaders.Count; measureIndex++)
            {
                currentMeasureIndex = measureIndex;
                Gp5MeasureHeader header = song.measureHeaders[measureIndex];
                for (int trackIndex = 0; trackIndex < song.tracks.Count; trackIndex++)
                {
                    currentTrackIndex = trackIndex;
                    Gp5Track track = song.tracks[trackIndex];
                    for (int voiceIndex = 0; voiceIndex < 2; voiceIndex++)
                    {
                        currentVoiceIndex = voiceIndex;
                        double startQuarter = header.startQuarter;
                        int beatCount = ReadInt32();
                        for (int beatNumber = 0; beatNumber < beatCount; beatNumber++)
                        {
                            currentBeatIndex = beatNumber;
                            Gp5Beat beat = ReadBeat(track, measureIndex, voiceIndex, startQuarter);
                            if (beat != null)
                            {
                                track.beats.Add(beat);
                                startQuarter += beat.isEmpty ? 0.0 : beat.durationQuarter;
                                if (beat.tempoChangeBpm > 0)
                                {
                                    song.tempoChanges.Add(new Gp5TempoChange
                                    {
                                        quarterPos = beat.startQuarter,
                                        bpm = beat.tempoChangeBpm
                                    });
                                }
                            }
                        }
                    }

                    ReadOptionalByte();
                }
            }
        }

        private Gp5MidiChannel ReadChannel()
        {
            int channelIndex = ReadInt32() - 1;
            int effectChannelIndex = ReadInt32() - 1;
            if (channelIndex < 0 || channelIndex >= channels.Count)
                return null;

            Gp5MidiChannel source = channels[channelIndex];
            return new Gp5MidiChannel
            {
                index = channelIndex,
                effectChannelIndex = effectChannelIndex,
                instrument = source.instrument,
                volume = source.volume,
                balance = source.balance,
                chorus = source.chorus,
                reverb = source.reverb,
                phaser = source.phaser,
                tremolo = source.tremolo
            };
        }

        private string ReadMarkerName()
        {
            string marker = ReadIntByteSizeString();
            ReadColor();
            return marker;
        }

        private Gp5Beat ReadBeat(Gp5Track track, int measureIndex, int voiceIndex, double startQuarter)
        {
            byte flags = ReadByte();
            bool dotted = (flags & 0x01) != 0;
            bool hasChord = (flags & 0x02) != 0;
            bool hasText = (flags & 0x04) != 0;
            bool hasEffects = (flags & 0x08) != 0;
            bool hasMixTable = (flags & 0x10) != 0;
            bool hasTuplet = (flags & 0x20) != 0;
            bool hasStatus = (flags & 0x40) != 0;

            byte status = 0x01;
            if (hasStatus)
                status = ReadByte();

            sbyte durationValue = ReadSByte();
            int tupletValue = hasTuplet ? ReadInt32() : 1;
            double durationQuarter = DurationToQuarter(durationValue, dotted, tupletValue);

            if (hasChord)
                ReadChord();

            if (hasText)
                ReadIntByteSizeString();

            bool beatWideVibrato = false;
            bool noteVibrato = false;
            if (hasEffects)
                ReadBeatEffects(ref beatWideVibrato, ref noteVibrato);

            int tempoChangeBpm = -1;
            string tempoName = null;
            if (hasMixTable)
                ReadMixTableChange(ref tempoChangeBpm, ref tempoName);

            Gp5Beat beat = new Gp5Beat
            {
                measureIndex = measureIndex,
                voiceIndex = voiceIndex,
                startQuarter = startQuarter,
                durationQuarter = durationQuarter,
                isEmpty = status == 0x00,
                isRest = status == 0x02,
                beatWideVibrato = beatWideVibrato,
                noteVibrato = noteVibrato,
                tempoChangeBpm = tempoChangeBpm,
                tempoName = tempoName
            };

            ReadNotes(track, beat);

            short flags2 = ReadInt16();
            if ((flags2 & 0x0800) != 0)
                ReadByte();

            return beat;
        }

        private void ReadTrackRse()
        {
            ReadByte();
            for (int i = 0; i < 3; i++)
                ReadInt32();
            Skip(12);

            ReadRseInstrument();
            if (version > new Version(5, 0, 0))
            {
                for (int i = 0; i < 4; i++)
                    ReadSByte();
                ReadIntByteSizeString();
                ReadIntByteSizeString();
            }
        }

        private void ReadRseInstrument()
        {
            ReadInt32();
            ReadInt32();
            ReadInt32();
            if (version == new Version(5, 0, 0))
            {
                ReadInt16();
                Skip(1);
            }
            else
            {
                ReadInt32();
            }
        }

        private void ReadChord()
        {
            byte header = ReadByte();
            if (header == 0)
            {
                ReadIntByteSizeString();
                ReadInt32();
                for (int i = 0; i < 6; i++)
                    ReadInt32();
                return;
            }

            ReadBool();
            Skip(3);
            ReadByte();
            ReadByte();
            ReadByte();
            ReadInt32();
            ReadInt32();
            ReadBool();
            ReadByteSizeString(22);
            ReadByte();
            ReadByte();
            ReadByte();
            ReadInt32();
            for (int i = 0; i < 7; i++)
                ReadInt32();

            int barreCount = ReadByte();
            for (int i = 0; i < 5; i++)
                ReadByte();
            for (int i = 0; i < 5; i++)
                ReadByte();
            for (int i = 0; i < 5; i++)
                ReadByte();
            for (int i = 0; i < 7; i++)
                ReadBool();
            Skip(1);
            for (int i = 0; i < 7; i++)
                ReadSByte();
            ReadBool();

            int extraBarres = Math.Max(0, barreCount - 5);
            if (extraBarres > 0)
            {
                for (int i = 0; i < extraBarres * 3; i++)
                    ReadByte();
            }
        }

        private void ReadBeatEffects(ref bool beatWideVibrato, ref bool noteVibrato)
        {
            byte flags1 = ReadByte();
            beatWideVibrato = (flags1 & 0x02) != 0;
            noteVibrato = false;

            if ((flags1 & 0x20) != 0)
                ReadSByte();

            byte flags2 = ReadByte();
            if ((flags2 & 0x04) != 0)
                ReadTremoloBar();
            if ((flags1 & 0x40) != 0)
            {
                ReadSByte();
                ReadSByte();
            }

            if ((flags2 & 0x02) != 0)
                ReadSByte();
        }

        private void ReadTremoloBar()
        {
            ReadBend();
        }

        private void ReadMixTableChange(ref int tempoChangeBpm, ref string tempoName)
        {
            sbyte instrument = ReadSByte();
            ReadRseInstrument();
            if (version == new Version(5, 0, 0))
                Skip(1);

            sbyte volume = ReadSByte();
            sbyte balance = ReadSByte();
            sbyte chorus = ReadSByte();
            sbyte reverb = ReadSByte();
            sbyte phaser = ReadSByte();
            sbyte tremolo = ReadSByte();
            tempoName = ReadIntByteSizeString();
            int tempo = ReadInt32();

            if (volume >= 0)
                ReadSByte();
            if (balance >= 0)
                ReadSByte();
            if (chorus >= 0)
                ReadSByte();
            if (reverb >= 0)
                ReadSByte();
            if (phaser >= 0)
                ReadSByte();
            if (tremolo >= 0)
                ReadSByte();
            if (tempo >= 0)
            {
                ReadSByte();
                if (version > new Version(5, 0, 0))
                    ReadBool();

                tempoChangeBpm = tempo;
            }

            byte flags = ReadByte();
            ReadSByte();
            if (version > new Version(5, 0, 0))
            {
                ReadIntByteSizeString();
                ReadIntByteSizeString();
            }
        }

        private void ReadNotes(Gp5Track track, Gp5Beat beat)
        {
            byte stringFlags = ReadByte();
            for (int bitIndex = 6; bitIndex >= 0; bitIndex--)
            {
                int mask = 1 << bitIndex;
                if ((stringFlags & mask) == 0)
                    continue;

                int stringNumber = 7 - bitIndex;
                Gp5Note note = ReadNote(track, stringNumber);
                note.stringIdx = Math.Max(0, track.stringsHighToLow.Length - stringNumber);
                note.midi = ResolveMidi(track, note);
                beat.notes.Add(note);
            }
        }

        private Gp5Note ReadNote(Gp5Track track, int stringNumber)
        {
            byte flags = ReadByte();
            Gp5Note note = new Gp5Note
            {
                stringNumber = stringNumber,
                isHeavyAccentuated = (flags & 0x02) != 0,
                isGhost = (flags & 0x04) != 0,
                isAccentuated = (flags & 0x40) != 0,
                durationPercent = 1.0
            };

            byte noteType = 1;
            if ((flags & 0x20) != 0)
                noteType = ReadByte();

            if ((flags & 0x10) != 0)
                note.velocity = UnpackVelocity(ReadSByte());

            if ((flags & 0x20) != 0)
            {
                int fret = ReadSByte();
                note.isTie = noteType == 2;
                note.isDead = noteType == 3;
                note.fret = note.isTie ? GetLastFretOnString(track, stringNumber) : Mathf.Clamp(fret, 0, 99);
            }

            if ((flags & 0x80) != 0)
            {
                ReadSByte();
                ReadSByte();
            }

            if ((flags & 0x01) != 0)
                note.durationPercent = ReadDouble();

            byte flags2 = ReadByte();
            if ((flags & 0x08) != 0)
                ReadNoteEffects(note);

            if ((flags2 & 0x02) != 0)
            {
                // swap accidentals flag only, no payload
            }

            return note;
        }

        private void ReadNoteEffects(Gp5Note note)
        {
            byte flags1 = ReadByte();
            byte flags2 = ReadByte();

            note.isHammer = (flags1 & 0x02) != 0;
            note.letRing = (flags1 & 0x08) != 0;
            note.isStaccato = (flags2 & 0x01) != 0;
            note.isPalmMute = (flags2 & 0x02) != 0;
            note.isVibrato = (flags2 & 0x40) != 0;

            if ((flags1 & 0x01) != 0)
                note.bend = ReadBend();
            if ((flags1 & 0x10) != 0)
                ReadGrace();
            if ((flags2 & 0x04) != 0)
                ReadSByte();
            if ((flags2 & 0x08) != 0)
            {
                note.slideFlags = ReadByte();
                note.hasSlide = note.slideFlags != 0;
            }

            if ((flags2 & 0x10) != 0)
            {
                note.isHarmonic = true;
                ReadHarmonic();
            }

            if ((flags2 & 0x20) != 0)
                ReadTrill();
        }

        private Gp5BendEffect ReadBend()
        {
            Gp5BendEffect bend = new Gp5BendEffect
            {
                type = ReadSByte(),
                value = ReadInt32()
            };

            int pointCount = ReadInt32();
            for (int i = 0; i < pointCount; i++)
            {
                int rawPosition = ReadInt32();
                int rawValue = ReadInt32();
                bool vibrato = ReadBool();
                bend.points.Add(new Gp5BendPoint
                {
                    position = Mathf.RoundToInt(rawPosition * 12f / BendPosition),
                    value = rawValue / BendSemitone,
                    vibrato = vibrato
                });
            }

            return bend;
        }

        private void ReadGrace()
        {
            ReadSByte();
            ReadByte();
            ReadByte();
            ReadByte();
            ReadByte();
        }

        private void ReadHarmonic()
        {
            ReadSByte();
        }

        private void ReadTrill()
        {
            ReadSByte();
            ReadSByte();
        }

        private int ResolveMidi(Gp5Track track, Gp5Note note)
        {
            if (track.stringsHighToLow == null || track.stringsHighToLow.Length == 0)
                return note.fret;

            int tuningIndex = Mathf.Clamp(note.stringNumber - 1, 0, track.stringsHighToLow.Length - 1);
            return Mathf.Clamp(track.stringsHighToLow[tuningIndex] + note.fret, 0, 127);
        }

        private int GetLastFretOnString(Gp5Track track, int stringNumber)
        {
            for (int i = track.beats.Count - 1; i >= 0; i--)
            {
                Gp5Beat beat = track.beats[i];
                for (int noteIndex = beat.notes.Count - 1; noteIndex >= 0; noteIndex--)
                {
                    Gp5Note note = beat.notes[noteIndex];
                    if (note.stringNumber == stringNumber)
                        return note.fret;
                }
            }

            return 0;
        }

        private static double ComputeMeasureLengthQuarter(int numerator, int denominator)
        {
            if (denominator <= 0)
                denominator = 4;

            return numerator * (4.0 / denominator);
        }

        private static double DurationToQuarter(int durationValue, bool dotted, int tupletValue)
        {
            double quarter = 4.0 / Math.Pow(2.0, durationValue + 2);
            if (dotted)
                quarter *= 1.5;

            if (tupletValue > 1)
                quarter *= GetTupletTimes(tupletValue) / (double)tupletValue;

            return quarter;
        }

        private static int GetTupletTimes(int tupletValue)
        {
            switch (tupletValue)
            {
                case 3: return 2;
                case 5: return 4;
                case 6: return 4;
                case 7: return 4;
                case 9: return 8;
                case 10: return 8;
                case 11: return 8;
                case 12: return 8;
                case 13: return 8;
                default: return 1;
            }
        }

        private static Version ParseVersion(string versionText)
        {
            if (string.IsNullOrWhiteSpace(versionText))
                return new Version(5, 0, 0);

            int marker = versionText.LastIndexOf('v');
            if (marker >= 0 && Version.TryParse(versionText.Substring(marker + 1).Trim(), out Version parsed))
                return parsed;

            return new Version(5, 0, 0);
        }

        private static int UnpackVelocity(int dynamic)
        {
            return Mathf.Clamp(15 + (16 * dynamic) - 16, 1, 127);
        }

        private string ReadByteSizeString(int fieldLength)
        {
            int textLength = ReadByte();
            byte[] raw = reader.ReadBytes(fieldLength);
            int safeLength = Math.Min(textLength, raw.Length);
            return DecodeString(raw, safeLength);
        }

        private string ReadIntByteSizeString()
        {
            int totalLength = ReadInt32();
            if (totalLength <= 0)
                return string.Empty;

            int textLength = ReadByte();
            byte[] raw = reader.ReadBytes(Math.Max(0, totalLength - 1));
            int safeLength = Math.Min(textLength, raw.Length);
            return DecodeString(raw, safeLength);
        }

        private string ReadIntSizeString()
        {
            int textLength = ReadInt32();
            if (textLength <= 0)
                return string.Empty;

            byte[] raw = reader.ReadBytes(textLength);
            return DecodeString(raw, raw.Length);
        }

        private static string DecodeString(byte[] data, int length)
        {
            if (data == null || data.Length == 0 || length <= 0)
                return string.Empty;

            int safeLength = Math.Min(length, data.Length);
            string decoded = GpEncoding.GetString(data, 0, safeLength);
            return decoded.TrimEnd('\0', ' ');
        }

        private void ReadColor()
        {
            ReadByte();
            ReadByte();
            ReadByte();
            ReadByte();
        }

        private void Skip(int count)
        {
            if (count > 0)
                reader.ReadBytes(count);
        }

        private byte ReadByte() => reader.ReadByte();
        private byte ReadOptionalByte(byte fallback = 0)
        {
            if (reader.BaseStream.Position >= reader.BaseStream.Length)
                return fallback;

            return reader.ReadByte();
        }
        private bool ReadBool() => reader.ReadByte() != 0;
        private short ReadInt16() => reader.ReadInt16();
        private int ReadInt32() => reader.ReadInt32();
        private sbyte ReadSByte() => reader.ReadSByte();
        private double ReadDouble() => reader.ReadDouble();
    }
}
