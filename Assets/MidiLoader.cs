using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class MidiLoader : MonoBehaviour
{
    // Standard guitar tuning: E2 A2 D3 G3 B3 E4
    private static readonly int[] stringBasePitches = { 40, 45, 50, 55, 59, 64 };
    private static readonly string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private struct TempoEvent
    {
        public long tick;
        public int microsecondsPerQuarter;

        public TempoEvent(long tick, int mpqn)
        {
            this.tick = tick;
            this.microsecondsPerQuarter = mpqn;
        }
    }

    public static List<NoteData> LoadMidiSong(string filePath, int targetTrackIndex)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("MIDI file not found: " + filePath);
            return null;
        }

        byte[] data = File.ReadAllBytes(filePath);

        int formatType;
        int numTracks;
        int timeDivision;
        List<byte[]> trackChunks = new List<byte[]>();

        using (MemoryStream ms = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(ms))
        {
            string chunkID = new string(reader.ReadChars(4));
            if (chunkID != "MThd")
            {
                Debug.LogError("Invalid MIDI header");
                return null;
            }

            int headerSize = SwapEndian(reader.ReadInt32());
            formatType = SwapEndian(reader.ReadUInt16());
            numTracks = SwapEndian(reader.ReadUInt16());
            timeDivision = SwapEndian(reader.ReadUInt16());

            // Skip any extra header bytes if header size > 6
            if (headerSize > 6)
                reader.ReadBytes(headerSize - 6);

            for (int i = 0; i < numTracks; i++)
            {
                if (ms.Position + 8 > ms.Length)
                    break;

                string trackID = new string(reader.ReadChars(4));
                if (trackID != "MTrk")
                {
                    Debug.LogError($"Expected MTrk, got {trackID} on track {i}");
                    return null;
                }

                int trackSize = SwapEndian(reader.ReadInt32());
                byte[] trackData = reader.ReadBytes(trackSize);
                trackChunks.Add(trackData);
            }
        }

        if (trackChunks.Count == 0)
        {
            Debug.LogError("No MIDI tracks found");
            return null;
        }

        if ((timeDivision & 0x8000) != 0)
        {
            Debug.LogError("SMPTE time division MIDI files are not supported.");
            return null;
        }

        // 1) Build global tempo map from all tracks
        List<TempoEvent> tempoMap = BuildTempoMap(trackChunks, timeDivision);

        // 2) Parse note tracks using that tempo map
        int bestTrackIndex = -1;
        int maxValidNotes = -1;
        List<NoteData> chosenNotes = new List<NoteData>();

        for (int i = 0; i < trackChunks.Count; i++)
        {
            string trackName;
            int totalNotes;
            int validNotes;

            List<NoteData> parsed = ParseTrackNotes(trackChunks[i], timeDivision, tempoMap, out trackName, out totalNotes, out validNotes);

            Debug.Log($"Track {i}: '{trackName}' | total note-ons={totalNotes} | mapped guitar notes={validNotes}");

            if (i == targetTrackIndex)
            {
                chosenNotes = parsed;
            }

            if (validNotes > maxValidNotes)
            {
                maxValidNotes = validNotes;
                bestTrackIndex = i;
            }
        }

        if (targetTrackIndex == -1)
        {
            if (bestTrackIndex == -1)
            {
                Debug.LogWarning("No suitable note track found.");
                return new List<NoteData>();
            }

            string bestTrackName;
            int totalNotes;
            int validNotes;

            chosenNotes = ParseTrackNotes(trackChunks[bestTrackIndex], timeDivision, tempoMap, out bestTrackName, out totalNotes, out validNotes);
            Debug.Log($"Auto-selected best track: {bestTrackIndex} ('{bestTrackName}')");
        }

        chosenNotes = chosenNotes.OrderBy(n => n.time).ToList();

        // Helpful debug output
        for (int i = 0; i < Mathf.Min(20, chosenNotes.Count); i++)
        {
            float delta = (i == 0) ? chosenNotes[i].time : (chosenNotes[i].time - chosenNotes[i - 1].time);
            Debug.Log($"Note {i}: t={chosenNotes[i].time:F3}s Δ={delta:F3}s string={chosenNotes[i].stringIdx} fret={chosenNotes[i].fret} note={chosenNotes[i].note}");
        }

        return chosenNotes;
    }

    private static List<TempoEvent> BuildTempoMap(List<byte[]> trackChunks, int timeDivision)
    {
        List<TempoEvent> tempos = new List<TempoEvent>();
        tempos.Add(new TempoEvent(0, 500000)); // Default MIDI tempo = 120 BPM

        for (int trackIndex = 0; trackIndex < trackChunks.Count; trackIndex++)
        {
            using (MemoryStream ms = new MemoryStream(trackChunks[trackIndex]))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                long currentTick = 0;
                byte lastStatus = 0;

                while (ms.Position < ms.Length)
                {
                    int deltaTime = ReadVariableLength(reader);
                    currentTick += deltaTime;

                    if (ms.Position >= ms.Length)
                        break;

                    byte status = reader.ReadByte();

                    if (status < 0x80)
                    {
                        status = lastStatus;
                        ms.Position--;
                    }
                    else
                    {
                        lastStatus = status;
                    }

                    int eventType = status & 0xF0;

                    if (status == 0xFF) // Meta
                    {
                        int metaType = reader.ReadByte();
                        int metaLen = ReadVariableLength(reader);
                        byte[] metaData = reader.ReadBytes(metaLen);

                        if (metaType == 0x51 && metaLen >= 3)
                        {
                            int mpqn = (metaData[0] << 16) | (metaData[1] << 8) | metaData[2];
                            tempos.Add(new TempoEvent(currentTick, mpqn));
                        }
                    }
                    else if (status == 0xF0 || status == 0xF7) // SysEx
                    {
                        int len = ReadVariableLength(reader);
                        reader.ReadBytes(len);
                    }
                    else if (eventType == 0x80 || eventType == 0x90 || eventType == 0xA0 || eventType == 0xB0 || eventType == 0xE0)
                    {
                        reader.ReadByte();
                        reader.ReadByte();
                    }
                    else if (eventType == 0xC0 || eventType == 0xD0)
                    {
                        reader.ReadByte();
                    }
                    else
                    {
                        Debug.LogWarning($"Unknown MIDI event in tempo scan: 0x{status:X2}");
                        break;
                    }
                }
            }
        }

        tempos = tempos
            .OrderBy(t => t.tick)
            .ThenBy(t => t.microsecondsPerQuarter)
            .ToList();

        // Remove duplicate tick entries, keeping the last one at that tick
        Dictionary<long, TempoEvent> dedup = new Dictionary<long, TempoEvent>();
        foreach (var t in tempos)
            dedup[t.tick] = t;

        List<TempoEvent> result = dedup.Values.OrderBy(t => t.tick).ToList();

        foreach (var t in result)
        {
            double bpm = 60000000.0 / t.microsecondsPerQuarter;
            Debug.Log($"Tempo Map: tick={t.tick} -> {bpm:F2} BPM");
        }

        return result;
    }

    private static List<NoteData> ParseTrackNotes(
        byte[] trackData,
        int timeDivision,
        List<TempoEvent> tempoMap,
        out string trackName,
        out int totalNotes,
        out int validNotes)
    {
        List<NoteData> notes = new List<NoteData>();
        trackName = "Untitled";
        totalNotes = 0;
        validNotes = 0;

        using (MemoryStream ms = new MemoryStream(trackData))
        using (BinaryReader reader = new BinaryReader(ms))
        {
            long currentTick = 0;
            byte lastStatus = 0;

            while (ms.Position < ms.Length)
            {
                int deltaTime = ReadVariableLength(reader);
                currentTick += deltaTime;

                if (ms.Position >= ms.Length)
                    break;

                byte status = reader.ReadByte();

                if (status < 0x80)
                {
                    status = lastStatus;
                    ms.Position--;
                }
                else
                {
                    lastStatus = status;
                }

                int eventType = status & 0xF0;

                if (eventType == 0x90) // Note On
                {
                    int noteNum = reader.ReadByte();
                    int velocity = reader.ReadByte();

                    if (velocity > 0)
                    {
                        totalNotes++;

                        var mapped = MapNoteToGuitar(noteNum);
                        if (mapped.HasValue)
                        {
                            double timeInSeconds = TickToSeconds(currentTick, tempoMap, timeDivision);
                            notes.Add(new NoteData((float)timeInSeconds, mapped.Value.Key, mapped.Value.Value, GetNoteName(noteNum)));
                            validNotes++;
                        }
                    }
                }
                else if (eventType == 0x80) // Note Off
                {
                    reader.ReadByte();
                    reader.ReadByte();
                }
                else if (eventType == 0xA0 || eventType == 0xB0 || eventType == 0xE0)
                {
                    reader.ReadByte();
                    reader.ReadByte();
                }
                else if (eventType == 0xC0 || eventType == 0xD0)
                {
                    reader.ReadByte();
                }
                else if (status == 0xFF) // Meta
                {
                    int metaType = reader.ReadByte();
                    int metaLen = ReadVariableLength(reader);
                    byte[] metaData = reader.ReadBytes(metaLen);

                    if (metaType == 0x03)
                    {
                        trackName = Encoding.ASCII.GetString(metaData).Trim();
                    }
                }
                else if (status == 0xF0 || status == 0xF7) // SysEx
                {
                    int len = ReadVariableLength(reader);
                    reader.ReadBytes(len);
                }
                else
                {
                    Debug.LogWarning($"Unknown MIDI event in note parse: 0x{status:X2}");
                    break;
                }
            }
        }

        return notes;
    }

    private static double TickToSeconds(long targetTick, List<TempoEvent> tempoMap, int timeDivision)
    {
        double totalSeconds = 0.0;

        long previousTick = 0;
        int currentMpqn = 500000; // default 120 BPM

        for (int i = 0; i < tempoMap.Count; i++)
        {
            TempoEvent t = tempoMap[i];

            if (t.tick > targetTick)
                break;

            long deltaTicks = t.tick - previousTick;
            totalSeconds += (deltaTicks * currentMpqn) / 1000000.0 / timeDivision;

            previousTick = t.tick;
            currentMpqn = t.microsecondsPerQuarter;
        }

        long remainingTicks = targetTick - previousTick;
        totalSeconds += (remainingTicks * currentMpqn) / 1000000.0 / timeDivision;

        return totalSeconds;
    }

    private static string GetNoteName(int midiNote)
    {
        return noteNames[midiNote % 12];
    }

    private static ushort SwapEndian(ushort val)
    {
        return (ushort)((val << 8) | (val >> 8));
    }

    private static int SwapEndian(int val)
    {
        uint u = (uint)val;
        return (int)((u << 24) | ((u & 0xFF00) << 8) | ((u >> 8) & 0xFF00) | (u >> 24));
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

    private static KeyValuePair<int, int>? MapNoteToGuitar(int midiPitch)
    {
        if (midiPitch < 28 || midiPitch > 90)
            return null;

        int bestString = -1;
        int bestFret = 100;

        for (int s = 0; s < 6; s++)
        {
            int fret = midiPitch - stringBasePitches[s];

            if (fret >= 0 && fret <= 22)
            {
                if (fret < bestFret)
                {
                    bestFret = fret;
                    bestString = s;
                }
            }
        }

        if (bestString != -1)
            return new KeyValuePair<int, int>(bestString, bestFret);

        return null;
    }
}