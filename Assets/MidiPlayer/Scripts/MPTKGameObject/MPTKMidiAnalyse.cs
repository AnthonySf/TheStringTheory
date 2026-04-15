using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

namespace MidiPlayerTK
{
    /// @ingroup midi_tempo_signature
    // Contains some information about the count of MIDI events.
    // @version Maestro 1.9.0
    // @note beta
    public partial class MPTKStat
    {
        public int CountAll;
        public int CountNote;
        public int CountPreset;
        public int NoteMin;
        public int NoteMax;
        public int VelocityMin;
        public int VelocityMax;
        public int CountChannel;

        // Using dictionnary would be better than array but the purpose here is further
        // to demonstrate how computing statistics from a MIDI file in editor mode.
        int[,] CountByNoteByChannel = new int[16, 128];
        int[] CountNotesByChannel = new int[16];

        [Preserve]
        public MPTKStat() { }

        /// <summary>
        /// Calculate statistics from a list of MIDI events.\n
        /// </summary>
        /// <param name="midiEvents"></param>
        public void Calculate(List<MPTKEvent> midiEvents)
        {
            try
            {
                NoteMin = 127;
                NoteMax = 0;
                VelocityMin = 127;
                VelocityMax = 0;
                CountAll = 0;
                CountNote = 0;
                CountPreset = 0;
                CountChannel = 0;
                // Calculate notes count by channel
                foreach (MPTKEvent mptkEvent in midiEvents)
                {
                    CountAll++;
                    // In editor mode, only the basic structure of MIDI is available (not MPTKEvent)
                    if (mptkEvent.Command == MPTKCommand.NoteOn)
                    {
                        try
                        {
                            CountNote++;
                            CountByNoteByChannel[mptkEvent.Channel, mptkEvent.Value]++;
                            if (mptkEvent.Value < NoteMin) NoteMin = mptkEvent.Value;
                            if (mptkEvent.Value > NoteMax) NoteMax = mptkEvent.Value;
                            if (mptkEvent.Velocity < VelocityMin) VelocityMin = mptkEvent.Velocity;
                            if (mptkEvent.Velocity > VelocityMax) VelocityMax = mptkEvent.Velocity;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning(ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex);
            }

            try
            {
                // Display notes count and calculate count by channel
                for (int channel = 0; channel < 16; channel++)
                    for (int note = 0; note < 128; note++)
                        if (CountByNoteByChannel[channel, note] > 0)
                        {
                            CountNotesByChannel[channel]++;
                        }
                for (int channel = 0; channel < 16; channel++)
                    if (CountNotesByChannel[channel] > 0)
                        CountChannel++;
            }
            catch (Exception ex) { Debug.LogWarning(ex); }


        }
    }

    /// @ingroup midi_tempo_signature
    /// <summary>
    /// Contains information about the tempo change.\n
    /// @version Maestro 2.9.0
    /// @note 
    /// @li The tempo map is automaticalled build when a MIDI file is loaded from the MIDI DB, from an external MIDI or from a MIDI Writer instance.
    /// @li The tempo map must be run by script on your MIDI events when created with MidiFileWriter2 with:\n
    ///     #MPTK_CalculateTempoMap 
    /// @li Each segments defined the tick start/end and the start real time of the segment and the pulse (duration in milliseconds of a MIDI tick) which is constant all along the segment.

    /// </summary>
    public class MPTKTempo
    {
        /// <summary>@brief
        /// This information is mandatory to calculate start/end measure and beat.
        /// </summary>
        public static int DeltaTicksPerQuarterNote;

        /// <summary>@brief
        /// Index of this segment if added to a list of tempo map with #MPTK_CalculateTempoMap
        /// </summary>
        public int Index;

        /// <summary>@brief
        /// Tick start of this segment
        /// </summary>
        public long FromTick;

        /// <summary>@brief
        /// Tick end of this segment
        /// </summary>
        public long ToTick;

        /// <summary>@brief
        /// Exact time in milliseconds to reach this tempo or signature change
        /// </summary>
        public double FromTime;

        /// <summary>@brief
        /// Duration in milliseconds of a MIDI tick in this segment. The pulse length is the minimum time in milliseconds between two MIDI events.\n
        /// @note
        /// @li Depends on the current tempo, the #MPTK_DeltaTicksPerQuarterNote (but not the Speed).
        /// @li Formula: Pulse = (60000000 /  MPTK_CurrentTempo) / MPTK_DeltaTicksPerQuarterNote / 1000
        /// </summary>
        public double Pulse;

        /// <summary>@brief
        /// BPM = 60000000 / MicrosecondsPerQuarterNote
        /// </summary>
        public int MicrosecondsPerQuarterNote;

        /// <summary>
        /// Creates a tempo segment with default value
        /// </summary>
        /// <param name="index"></param>
        /// <param name="fromTick">default 0</param>
        /// <param name="toTick">default ong.MaxValue</param>
        /// <param name="fromTime">default 0</param>
        /// <param name="pulse">default 0</param>
        /// <param name="microsecondsPerQuarterNote">default 0</param>
        [Preserve]
        public MPTKTempo(int index, long fromTick = 0, long toTick = long.MaxValue, double fromTime = 0d,
            double pulse = 0d, int microsecondsPerQuarterNote = 0)
        {
            Index = index;
            FromTick = fromTick;
            ToTick = toTick;
            FromTime = fromTime;
            Pulse = pulse;
            MicrosecondsPerQuarterNote = microsecondsPerQuarterNote;
        }

        /// <summary>@brief
        /// Find an index in the tempo change list from a tick position.
        /// @warning
        ///     - To avoid confusion with search by time, make sure the tickSearch parameter is a long (or int).
        /// @note
        ///     - If tick is a border between two segments, the upper segment is returned.
        /// @snippet MidiEditorProWindow.cs ExampleFindTempoMap 
        /// </summary>
        /// <param name="tempoMap">List of tempo map build with MPTK_CalculateTempoMap </param>
        /// <param name="tickSearch">search from this tick value</param>
        /// <param name="fromIndex">search from this index position in the list (for optimization)</param>
        /// <returns>index of the segment in tempoMap</returns>
        public static int FindSegment(List<MPTKTempo> tempoMap, long tickSearch, int fromIndex = 0)
        {
            if (tempoMap == null || tempoMap.Count == 0)
                return 0;

            int indexTempo = fromIndex;

            while (indexTempo < tempoMap.Count)
            {
                if (tickSearch >= tempoMap[indexTempo].FromTick && tickSearch < tempoMap[indexTempo].ToTick)
                    break;
                indexTempo++;
            }

            if (indexTempo >= tempoMap.Count)
                indexTempo = 0;

            return indexTempo;
        }

        /// <summary>@brief
        /// Find an index in the tempo change list from a time position in milliseconds.
        /// @warning
        ///     - To avoid confusion with search by ticks, make sure the timeSearch parameter is a float.
        /// @note
        ///     - V2.12.0.1 Thanks to cihadturhan_unity for fixing the bug. See here: https://forum.unity.com/threads/maestro-midi-player-tool-kit-good-news-for-your-rhythm-game.526741/page-15#post-9865116
        ///     - If time is a border between two segments, the lower segment is returned.
        /// @snippet MidiEditorProWindow.cs ExampleFindTempoMap 
        /// </summary>
        /// <param name="tempoMap">List of tempo map build with MPTK_CalculateTempoMap </param>
        /// <param name="timeSearch">search from this time in milliseconds</param>
        /// <param name="fromIndex">search from this index position in the TempoMap list (for optimization)</param>
        /// <returns>index of the segment in tempoMap</returns>

        public static int FindSegment(List<MPTKTempo> tempoMap, float timeSearch, int fromIndex = 0)
        {
            if (tempoMap == null || tempoMap.Count == 0)
                return 0;

            int indexTempo = fromIndex;

            while (indexTempo < tempoMap.Count)
            {
                // calculate ToTime
                double ToTime = tempoMap[indexTempo].FromTime +
                    (tempoMap[indexTempo].ToTick - tempoMap[indexTempo].FromTick) * tempoMap[indexTempo].Pulse;
                //Debug.Log($"   timeSearch:{timeSearch} indexTempo:{indexTempo} FromTime:{tempoMap[indexTempo].FromTime} ToTime:{ToTime}");
                if (timeSearch < ToTime)
                {
                    break;
                }
                indexTempo++;
            }

            if (indexTempo >= tempoMap.Count)
                indexTempo = tempoMap.Count - 1;

            return indexTempo;
        }



        /// <summary>@brief
        /// Realtime in milliseconds for this tick in this segment
        /// @snippet MidiEditorProWindow.cs ExampleFindTempoMap 
        /// </summary>
        /// <param name="tick"></param>
        /// <returns></returns>
        public double CalculateTime(long tick)
        {
            return FromTime + (tick - FromTick) * Pulse;
        }

        public long CalculatelTick(float time)
        {
            return FromTick + (long)((time - FromTime) / Pulse + 0.5d);
        }

        /// <summary>@brief
        /// Creates a tempo map from a MIDI events list with tempo change.  
        /// An allocated tempo map must be defined in parameter but the content will be cleared.
        /// @note A default tempo segment will be added at tick 0 with BPM = 120
        /// @version 2.10.0
        /// @snippet TestMidiGenerator.cs ExampleCalculateMaps
        /// </summary>
        /// <param name="deltaTicksPerQuarterNote"></param>
        /// <param name="mptkEvents"></param>
        /// <param name="temposMap"></param>
        public static void CalculateMap(int deltaTicksPerQuarterNote, List<MPTKEvent> mptkEvents, List<MPTKTempo> temposMap)
        {
            DeltaTicksPerQuarterNote = deltaTicksPerQuarterNote;
            temposMap.Clear();

            // Create a tempo at start (some MIDI have no tempo defined), set to 120 by default (500 000 microseconds)
            temposMap.Add(new MPTKTempo(index: 0, microsecondsPerQuarterNote: MPTKEvent.BeatPerMinute2QuarterPerMicroSecond(120),
                pulse: (double)MPTKEvent.BeatPerMinute2QuarterPerMicroSecond(120) / (double)DeltaTicksPerQuarterNote / 1000d));
            MPTKTempo previousTempo = temposMap.Last();

            mptkEvents.ForEach(mptkEvent =>
            {
                if (mptkEvent.Command == MPTKCommand.MetaEvent && mptkEvent.Meta == MPTKMeta.SetTempo)
                {
                    int microsecondsPerQuarterNote = mptkEvent.Value;
                    if (microsecondsPerQuarterNote <= 0)
                    {
                        Debug.LogWarning($"SetTempo with incorrect MicrosecondsPerQuarterNote at position {mptkEvent.Tick} . Force tempo to 120.");
                        microsecondsPerQuarterNote = MPTKEvent.BeatPerMinute2QuarterPerMicroSecond((int)120);
                    }
                    double pulse = ((double)microsecondsPerQuarterNote / (double)DeltaTicksPerQuarterNote) / 1000d;

                    if (previousTempo.FromTick == mptkEvent.Tick)
                    {
                        // Same tick as previous, update it
                        previousTempo.Pulse = pulse;
                        previousTempo.MicrosecondsPerQuarterNote = microsecondsPerQuarterNote;
                    }
                    else
                    {
                        previousTempo = temposMap.Last();
                        // Add new tempo segment
                        temposMap.Add(new MPTKTempo(
                            index: temposMap.Count,
                            fromTick: mptkEvent.Tick,
                            pulse: pulse,
                            fromTime: previousTempo.FromTime + (mptkEvent.Tick - previousTempo.FromTick) * previousTempo.Pulse,
                            microsecondsPerQuarterNote: microsecondsPerQuarterNote
                        ));
                        previousTempo.ToTick = mptkEvent.Tick;
                    }
                }
            });
        }
        /// <summary>@brief
        /// String description of this segment
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string toTick = ToTick == long.MaxValue ? "End" : ToTick.ToString();
            return $"   {Index} Tick:{FromTick,-7:000000} to {toTick,-7:000000}  Pulse(ms):{Pulse:F2}  Time(ms) from:{FromTime,10:F2} BPM:{(int)(60000000f / MicrosecondsPerQuarterNote)} Quarter(ms):{(int)(MicrosecondsPerQuarterNote / 1000f)}";
        }
    }

    /// @ingroup midi_tempo_signature
    /// <summary>
    /// Contains information about time signature changes in a MIDI file.
    /// @version Maestro 2.10.0
    /// @note 
    /// @li The signature map is automatically built when a MIDI file is loaded from the MIDI DB, from an external MIDI, or from a MIDI Writer instance.
    /// @li The signature map must be processed by script on your MIDI events when created with MidiFileWriter2 using:
    ///     #MPTK_CalculateMap and #MPTK_CalculateMeasureBoundaries
    /// @li Each segment define the tick start/end and the measure start/end for the time signature.
    /// </summary>
    public class MPTKSignature
    {
        /// <summary>@brief
        /// This information is mandatory to calculate start/end measure and beat.
        /// </summary>
        public static int DeltaTicksPerQuarterNote;

        /// <summary>@brief
        /// Index of this segment if added to a list of tempo map with #MPTK_CalculateTempoMap
        /// </summary>
        public int Index;

        /// <summary>@brief
        /// Tick start of this segment
        /// </summary>
        public long FromTick;

        /// <summary>@brief
        /// Tick end of this segment
        /// </summary>
        public long ToTick;

        /// <summary>@brief
        /// From TimeSignature event: The numerator counts the number of beats in a mesure.\n
        /// For example a numerator of 4 means that each bar contains four beats.\n
        /// This is important knowing this value because usually the first beat of each bar has extra emphasis.\n
        /// https://paxstellar.fr/2020/09/11/midi-timing/
        /// </summary>
        public int NumberBeatsMeasure;

        /// <summary>@brief
        /// From TimeSignature event: Describes of what note value a beat is.
        /// @note
        /// Calculated with 2^MPTK_TimeSigDenominatorn
        /// @li  1 for a whole-note
        /// @li  2 for a half-note
        /// @li  4 for a quarter-note
        /// @li  8 for an eighth-note, etc.
        /// 
        /// Equal 2 Power TimeSigDenominator from the MIDI signature message.\n
        /// https://paxstellar.fr/2020/09/11/midi-timing/
        /// https://majicdesigns.github.io/MD_MIDIFile/page_timing.html
        /// </summary>
        public int NumberQuarterBeat;

        /// <summary>@brief
        /// Start measure of this segment of event
        /// </summary>
        public int FromMeasure;

        /// <summary>@brief
        /// End measure of this segment of event
        /// </summary>
        public int ToMeasure;

        /// <summary>
        /// Creates a signtaure segment with default value
        /// </summary>
        /// <param name="index"></param>
        /// <param name="fromTick">default 0</param>
        /// <param name="toTick">default ong.MaxValue</param>
        /// <param name="numberBeatsMeasure">default 4</param>
        /// <param name="numberQuarterBeat">default 4</param>
        [Preserve]
        public MPTKSignature(int index, long fromTick = 0, long toTick = long.MaxValue, int numberBeatsMeasure = 4, int numberQuarterBeat = 4)
        {
            Index = index;
            FromTick = fromTick;
            ToTick = toTick;
            NumberBeatsMeasure = numberBeatsMeasure;
            NumberQuarterBeat = numberQuarterBeat;
        }

        /// <summary>@brief
        /// Search a measure from a tick in this segment map.
        /// @snippet MidiEditorProWindow.cs ExampleFindNextMeasure 
        /// </summary>
        /// <param name="tick"></param>
        /// <returns></returns>
        public int TickToMeasure(long tick)
        {
            int measure = int.MaxValue;
            if (tick < long.MaxValue)
                measure = FromMeasure + (int)((tick - FromTick) / (float)DeltaTicksPerQuarterNote / (NumberBeatsMeasure * 4 / NumberQuarterBeat));
            //Debug.Log($"MPTK_TickToMeasure tick:{tick} --> measure:{measure}");

            return measure;
        }

        /// <summary>@brief
        /// Search a tick position from a measure tempo map.
        /// @note #MPTK_CalculateMeasureBoundaries must be applied to the tempo map before this call.
        /// @snippet MidiEditorProWindow.cs ExampleFindNextMeasure 
        /// </summary>
        /// <param name="temposMap">List of tempo maps</param>
        /// <param name="measure">measure to search (start at 1)</param>
        /// <returns>tick position found, value will be between FromTick and ToTick.</returns>
        public static long MeasureToTick(List<MPTKSignature> temposMap, int measure)
        {
            long tick = -1;
            //long index = -1;
            foreach (MPTKSignature tempoMap in temposMap)
                if (measure >= tempoMap.FromMeasure && measure <= tempoMap.ToMeasure)
                {
                    //Debug.Log(tempoMap.ToString());
                    tick = tempoMap.FromTick + (measure - tempoMap.FromMeasure) * (tempoMap.NumberBeatsMeasure * 4 / tempoMap.NumberQuarterBeat) * DeltaTicksPerQuarterNote;
                    //index = tempoMap.Index;
                    break;
                }
            //Debug.Log($"MPTK_MeasureToTick temposMap:{temposMap.Count} measure:{measure} --> tick:{tick}");
            return tick;

        }

        /// <summary>@brief
        /// Calculate beat for this tick and measure position in this segment.
        /// @snippet MidiEditorProWindow.cs ExampleFindNextMeasure 
        /// </summary>
        /// <param name="tick">tick to search in this segemnt map</param>
        /// <param name="measure">measure to search in this segemnt map</param>
        /// <returns>Beat position (between 1 and NumberBeatsMeasure)</returns>
        public int CalculateBeat(long tick, int measure)
        {
            int beat = 0;
            if (tick < long.MaxValue && DeltaTicksPerQuarterNote > 0)
            {
                if (NumberBeatsMeasure > 0)
                {
                    // calculate beat count from the start of this measure = 
                    // beats count from the start of this segment for tick  - beats count before this measure in this segment
                    beat = (int)((tick - FromTick) / (float)DeltaTicksPerQuarterNote) + 1 - (measure - FromMeasure) * (NumberBeatsMeasure * 4 / NumberQuarterBeat);
                }
            }
            else
                beat = int.MaxValue;
            //Debug.Log(this.ToString());
            return beat;
        }

        /// <summary>@brief
        /// Creates a signature map from a MIDI events list with time signature.  
        /// @li an allocated tempo map must be defined in parameter but the content will be cleared.
        /// @li a default time signature 4/4 is created if no time signature event found
        /// @version 2.10.0
        /// @snippet TestMidiGenerator.cs ExampleCalculateMaps
        /// </summary>
        /// <param name="deltaTicksPerQuarterNote"></param>
        /// <param name="mptkEvents"></param>
        /// <param name="signaturesMap"></param>
        public static void CalculateMap(int deltaTicksPerQuarterNote, List<MPTKEvent> mptkEvents, List<MPTKSignature> signaturesMap)
        {
            DeltaTicksPerQuarterNote = deltaTicksPerQuarterNote;
            signaturesMap.Clear();
            mptkEvents.ForEach(mptkEvent =>
            {
                if (mptkEvent.Command == MPTKCommand.MetaEvent && mptkEvent.Meta == MPTKMeta.TimeSignature)
                {
                    MPTKSignature sign = new MPTKSignature(
                        index: signaturesMap.Count,
                        fromTick: mptkEvent.Tick,
                        numberBeatsMeasure: MPTKEvent.ExtractFromInt((uint)mptkEvent.Value, 0),
                        numberQuarterBeat: System.Convert.ToInt32(Mathf.Pow(2f, MPTKEvent.ExtractFromInt((uint)mptkEvent.Value, 1))));
                    signaturesMap.Add(sign);
                    if (signaturesMap.Count >= 2)
                        signaturesMap[signaturesMap.Count - 2].ToTick = mptkEvent.Tick;
                }
            });

            if (signaturesMap.Count == 0)
                signaturesMap.Add(new MPTKSignature(index: 0));
        }

        /// <summary>@brief
        /// Calculate FromMeasure and ToMeasure for all segments in the signature map.
        /// @snippet TestMidiGenerator.cs ExampleCalculateMaps
        /// </summary>
        /// <param name="signaturesMap"></param>
        public static void CalculateMeasureBoundaries(List<MPTKSignature> signaturesMap)
        {
            signaturesMap.ForEach(signMap =>
            {
                int numberBeatsMeasure = signMap.NumberBeatsMeasure * 4 / signMap.NumberQuarterBeat;
                if (signMap.Index == 0)
                {
                    // TBD try [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    signMap.FromMeasure = (int)(signMap.FromTick / (float)DeltaTicksPerQuarterNote / numberBeatsMeasure) + 1;
                    if (signMap.ToTick == long.MaxValue)
                        signMap.ToMeasure = int.MaxValue;
                    else
                        signMap.ToMeasure = (int)(signMap.ToTick / (float)DeltaTicksPerQuarterNote / numberBeatsMeasure);
                }
                else
                {
                    signMap.FromMeasure = signaturesMap[signMap.Index - 1].ToMeasure + 1;
                    if (signMap.ToTick == long.MaxValue)
                        signMap.ToMeasure = int.MaxValue;
                    else
                        signMap.ToMeasure = signMap.FromMeasure + (int)((signMap.ToTick - signMap.FromTick) / (float)DeltaTicksPerQuarterNote / numberBeatsMeasure) - 1;
                }
                //Debug.Log("MPTK_CalculateMeasureBoundaries " + tempoMap.ToString());
            });
        }

        /// <summary>@brief
        /// Find a signature change from a tick position in the tempo map.
        /// @snippet MidiEditorProWindow.cs ExampleFindNextMeasure 
        /// </summary>
        /// <param name="signMap">List of tempo map build with MPTK_CalculateTempoMap </param>
        /// <param name="tickSearch">search from this tick value</param>
        /// <param name="fromIndex">search from this index position in the list (for optimazation)</param>
        /// <returns></returns>
        public static int FindSegment(List<MPTKSignature> signMap, long tickSearch, int fromIndex = 0)
        {
            if (signMap == null || signMap.Count == 0)
                return 0;

            int indexTempo = fromIndex;

            while (indexTempo < signMap.Count)
            {
                if (tickSearch >= signMap[indexTempo].FromTick && tickSearch < signMap[indexTempo].ToTick)
                    break;
                indexTempo++;
            }

            if (indexTempo >= signMap.Count)
                indexTempo = 0;

            return indexTempo;
        }

        /// <summary>@brief
        /// String description of this segment
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string toTick = ToTick == long.MaxValue ? "End" : ToTick.ToString();
            string toBar = ToMeasure == int.MaxValue ? "End" : ToMeasure.ToString();
            return $"   {Index} Tick:{FromTick,-7:000000} to {toTick,-7:000000} Bar: {FromMeasure} to {toBar} Beats/Measure:{NumberBeatsMeasure}  Quarter/Quarter:{NumberQuarterBeat}";
        }
    }
}
