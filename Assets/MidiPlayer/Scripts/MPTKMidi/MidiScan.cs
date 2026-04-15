using MPTK.NAudio.Midi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MidiPlayerTK
{
    /// @ingroup runtime_midi_loading
    /// <summary>@brief
    /// Scan a midifile and return information
    /// </summary>
    public class MidiScan
    {
        /// <summary>@brief
        /// Return information about a midifile : patch change, copyright, ...
        /// </summary>
        /// <param name="pathfilename"></param>
        /// <param name="Info"></param>
        static public List<string> GeneralInfoMPTKEvent(string pathfilename, bool withNoteOn, bool withNoteOff, bool withControlChange, bool withPatchChange, bool withAfterTouch, bool withMeta, bool withOthers)
        {
            List<string> Info = new List<string>();
            try
            {
                MidiLoad midifile = new MidiLoad();
                midifile.MPTK_KeepNoteOff = withNoteOff;
                midifile.MPTK_EnableChangeTempo = true;
                midifile.MPTK_Load(pathfilename);
                if (midifile != null)
                {
                    Info.Add(string.Format("Format: {0}", midifile.midifile.FileFormat));
                    Info.Add(string.Format("Tracks: {0}", midifile.midifile.Tracks));
                    Info.Add(string.Format("Events count: {0}", midifile.MPTK_MidiEvents.Count()));
                    Info.Add(string.Format("Duration: {0} ({1} seconds) {2} Ticks", midifile.MPTK_Duration, midifile.MPTK_Duration.TotalSeconds, midifile.MPTK_TickLast));
                    Info.Add($"Track Count: {midifile.MPTK_TrackCount}");
                    Info.Add(string.Format("Initial Value"));
                    Info.Add(string.Format("   Tempo: {0,0:F2} BPM", midifile.MPTK_InitialTempo));
                    Info.Add(string.Format("   Beats in a measure: {0}", midifile.MPTK_NumberBeatsMeasure));
                    Info.Add(string.Format("   Quarters count in a beat:{0}", midifile.MPTK_NumberQuarterBeat));
                    Info.Add(string.Format("   Ticks per Quarter Note: {0}", midifile.midifile.DeltaTicksPerQuarterNote));
                    Info.Add("");

                    if (withNoteOn || withNoteOff || withControlChange || withPatchChange || withAfterTouch || withMeta || withOthers)
                    {
                        Info.Add("Legend MIDI event");
                        Info.Add("I: Event Index");
                        Info.Add("A: Absolute time in ticks");
                        Info.Add("D: Delta time in ticks from the last event");
                        Info.Add("R: Real time in seconds of the event with tempo change taken into account");
                        Info.Add("T: MIDI Track of this event");
                        Info.Add("C: MIDI Channel of this event");
                        Info.Add("");
                        Info.Add("*** Raw scan of the MIDI file ***");
                        Info.Add("");

                        foreach (MPTKEvent mptkEvent in midifile.MPTK_MidiEvents)
                        {
                            switch (mptkEvent.Command)
                            {
                                case MPTKCommand.NoteOn:
                                    if (withNoteOn) Info.Add(BuildInfoTrack(mptkEvent) +
                                        string.Format("NoteOn {0,3} ({1,3}) Len:{2,3} Vel:{3,3}", HelperNoteLabel.LabelFromMidi(mptkEvent.Value), mptkEvent.Value, mptkEvent.Length, mptkEvent.Velocity));
                                    break;
                                case MPTKCommand.NoteOff:
                                    if (withNoteOff) Info.Add(BuildInfoTrack(mptkEvent) +
                                        string.Format("NoteOff {0,3} ({1,3}) Vel:{2,3}", HelperNoteLabel.LabelFromMidi(mptkEvent.Value), mptkEvent.Value, mptkEvent.Velocity));
                                    break;
                                case MPTKCommand.PitchWheelChange:
                                    if (withOthers) Info.Add(BuildInfoTrack(mptkEvent) +
                                        string.Format("PitchWheelChange {0,3}", mptkEvent.Value));
                                    break;
                                case MPTKCommand.KeyAfterTouch:
                                    if (withAfterTouch) Info.Add(BuildInfoTrack(mptkEvent) +
                                        $"KeyAfterTouch {HelperNoteLabel.LabelFromMidi(mptkEvent.Value)} ({mptkEvent.Value}) Pressure:{mptkEvent.Velocity}");
                                    break;
                                case MPTKCommand.ChannelAfterTouch:
                                    if (withAfterTouch) Info.Add(BuildInfoTrack(mptkEvent) +
                                        $"ChannelAfterTouch Pressure:{mptkEvent.Value}");
                                    break;
                                case MPTKCommand.ControlChange:
                                    if (withControlChange) Info.Add(BuildInfoTrack(mptkEvent) +
                                        $"ControlChange  0x{mptkEvent.Controller:X}/{mptkEvent.Controller} {mptkEvent.Value}");
                                    break;
                                case MPTKCommand.PatchChange:
                                    if (withPatchChange) Info.Add(BuildInfoTrack(mptkEvent) +
                                        $"PresetChange {mptkEvent.Value} {PatchChangeEvent.GetPatchName(mptkEvent.Value)}");
                                    break;
                                case MPTKCommand.MetaEvent:
                                    if (withMeta)
                                    {
                                        switch (mptkEvent.Meta)
                                        {
                                            case MPTKMeta.SetTempo:
                                                Info.Add(BuildInfoTrack(mptkEvent) + $"SetTempo MicrosecondsPerQuarterNote:{mptkEvent.Value} BPM:{60000000 / mptkEvent.Value:F2}");
                                                break;

                                            case MPTKMeta.TimeSignature:
                                                // More info here https://paxstellar.fr/2020/09/11/midi-timing/
                                                // Numerator: counts the number of beats in a measure. 
                                                // For example a numerator of 4 means that each bar contains four beats. 
                                                // Denominator: number of quarter notes in a beat.0=ronde, 1=blanche, 2=quarter, 3=eighth, etc. 
                                                // Set default value
                                                Info.Add(BuildInfoTrack(mptkEvent) + "TimeSignature " +
                                                    $"Numerator (beats per measure):{MPTKEvent.ExtractFromInt((uint)mptkEvent.Value,0)} " +
                                                    $"Denominator:{MPTKEvent.ExtractFromInt((uint)mptkEvent.Value,1)} - " +
                                                    $"Quarter per quarter:{Convert.ToInt32(Mathf.Pow(2, MPTKEvent.ExtractFromInt((uint)mptkEvent.Value,1)))}");
                                                break;

                                            case MPTKMeta.KeySignature:
                                                Info.Add(BuildInfoTrack(mptkEvent) + "KeySignature " +
                                                    $"SharpsFlats:{MPTKEvent.ExtractFromInt((uint)mptkEvent.Value,0)} " +
                                                    $"MajorMinor:{MPTKEvent.ExtractFromInt((uint)mptkEvent.Value,1)}");
                                                break;

                                            default:
                                                Info.Add(BuildInfoTrack(mptkEvent) + mptkEvent.Meta.ToString() + mptkEvent.Info);
                                                break;
                                        }
                                    }
                                    break;

                                default:
                                    // Other midi event
                                    if (withOthers)
                                        Info.Add(BuildInfoTrack(mptkEvent) + $" {mptkEvent.Command}");
                                    break;
                            }
                        }
                    }
                    //else DebugMidiSorted(midifile.MidiSorted);
                }
                else
                {
                    Info.Add("Error reading midi file");
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return Info;
        }

        /// <summary>@brief
        /// Return information about a midifile : patch change, copyright, ...
        /// </summary>
        /// <param name="pathfilename"></param>
        /// <param name="Info"></param>
        //static public List<string> GeneralInfoNAudio(string pathfilename, bool withNoteOn, bool withNoteOff, bool withControlChange, bool withPatchChange, bool withAfterTouch, bool withMeta, bool withOthers)
        //{
        //    List<string> Info = new List<string>();
        //    try
        //    {
        //        List<TrackMidiEvent> midiEvents = GetEventFromRawMIDI(pathfilename);
        //        RawScanLegend(Info);

        //        if (withNoteOn || withNoteOff || withControlChange || withPatchChange || withAfterTouch || withMeta || withOthers)
        //            foreach (MidiEvent nAudioMidievent in midiEvents)
        //                Info.Add(ConvertnAudioEventToString(nAudioMidievent, withNoteOn, withNoteOff, withControlChange, withPatchChange, withAfterTouch, withMeta, withOthers));
        //    }
        //    catch (System.Exception ex)
        //    {
        //        Debug.LogWarning(ex);
        //    }
        //    return Info;
        //}
        static public int CountMidiEvents;

        public static List<List<MidiEvent>> GetEventFromRawMIDI(string pathfilename, bool withNoteOn, bool withNoteOff, bool withPitchWheelChange, bool withControlChange, bool withPatchChange, bool withAfterTouch, bool withMeta, bool withOthers)
        {
            List<List<MidiEvent>> midiEvents = new List<List<MidiEvent>>();
            CountMidiEvents = 0;
            try
            {
                TextAsset mididata = Resources.Load<TextAsset>(Path.Combine(MidiPlayerGlobal.MidiFilesDB, pathfilename));
                if (mididata != null && mididata.bytes != null && mididata.bytes.Length > 0)
                {
                    MidiFile midifile = new MidiFile(mididata.bytes, false);
                    int indexTrack = 0;
                    foreach (IList<MidiEvent> track in midifile.Events)
                    {
                        midiEvents.Add(new List<MidiEvent>());
                        //midiEvents[indexTrack].AddRange(track);
                        foreach (MidiEvent nAudioMidievent in track)
                        {
                            if ((withNoteOn && nAudioMidievent.CommandCode == MidiCommandCode.NoteOn) ||
                                (withNoteOff && nAudioMidievent.CommandCode == MidiCommandCode.NoteOff) ||
                                (withPitchWheelChange && nAudioMidievent.CommandCode == MidiCommandCode.PitchWheelChange) ||
                                (withAfterTouch && nAudioMidievent.CommandCode == MidiCommandCode.KeyAfterTouch) ||
                                (withAfterTouch && nAudioMidievent.CommandCode == MidiCommandCode.ChannelAfterTouch) ||
                                (withControlChange && nAudioMidievent.CommandCode == MidiCommandCode.ControlChange) ||
                                (withPatchChange && nAudioMidievent.CommandCode == MidiCommandCode.PatchChange) ||
                                (withMeta && nAudioMidievent.CommandCode == MidiCommandCode.MetaEvent) ||
                                (withOthers && (uint)nAudioMidievent.CommandCode >= 0xF0 && (uint)nAudioMidievent.CommandCode < 0xFF)) // Sysex, Eox, TimingClock, ... and not Meta
                            {
                                midiEvents[indexTrack].Add(nAudioMidievent);
                                CountMidiEvents++;
                            }
                        }
                        indexTrack++;
                    }
                }
                else
                    Debug.LogWarningFormat("Midi {0} not loaded from folder {1}", pathfilename, MidiPlayerGlobal.MidiFilesDB);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            return midiEvents;
        }

        public static List<string> RawScanLegend()
        {
            List<string> infos = new List<string>();
            infos.Add("*** Raw scan of the MIDI file ***");
            infos.Add("In a MIDI File, events are stored by tracks not by time position like with Maestro.");
            infos.Add("Useful to check the content of a MIDI file before any Maestro processing.");
            infos.Add("");
            infos.Add("MIDI event meaning:");
            infos.Add("A: Absolute time in ticks.");
            infos.Add("D: Delta time in ticks from the last event in the same track.");
            infos.Add("T: MIDI Track of this event.");
            infos.Add("C: MIDI Channel of this event (only for MIDI event channel based).");
            infos.Add("");
            return infos;
        }

        public static string ConvertnAudioEventToString(MidiEvent nAudioMidievent, int track)
        {
            string infoevent = null;
            switch (nAudioMidievent.CommandCode)
            {
                case MidiCommandCode.NoteOn:
                    NoteOnEvent noteon = (NoteOnEvent)nAudioMidievent;
                    if (noteon.OffEvent != null)
                        infoevent = BuildInfoTrack(nAudioMidievent, track) + string.Format("NoteOn  {0,3} ({1,03}) Len:{2,3} Vel:{3,3}", noteon.NoteName, noteon.NoteNumber, noteon.NoteLength, noteon.Velocity);
                    else
                        // It's a noteoff
                        infoevent = BuildInfoTrack(nAudioMidievent, track) + string.Format("NoteOff {0,3} ({1,03}) Len:{2,3} Vel:{3,3} (from noteon)", noteon.NoteName, noteon.NoteNumber, noteon.NoteLength, noteon.Velocity);
                    break;
                case MidiCommandCode.NoteOff:
                    NoteEvent noteoff = (NoteEvent)nAudioMidievent;
                    infoevent = BuildInfoTrack(nAudioMidievent, track) + string.Format("NoteOff {0,3} ({1,03}) Vel:{2,3}", noteoff.NoteName, noteoff.NoteNumber, noteoff.Velocity);
                    break;
                case MidiCommandCode.PitchWheelChange:
                    PitchWheelChangeEvent aftertouch = (PitchWheelChangeEvent)nAudioMidievent;
                    infoevent = BuildInfoTrack(nAudioMidievent, track) + string.Format("PitchWheelChange {0,03}", aftertouch.Pitch);
                    break;
                case MidiCommandCode.KeyAfterTouch:
                    NoteEvent keyaftertouch = (NoteEvent)nAudioMidievent;
                    infoevent = BuildInfoTrack(nAudioMidievent, track) + $"KeyAfterTouch {keyaftertouch.NoteName} ({keyaftertouch.NoteNumber}) Pressure:{keyaftertouch.Velocity}";
                    break;
                case MidiCommandCode.ChannelAfterTouch:
                    ChannelAfterTouchEvent channelaftertouch = (ChannelAfterTouchEvent)nAudioMidievent;
                    infoevent = BuildInfoTrack(nAudioMidievent, track) + $"ChannelAfterTouch Pressure:{channelaftertouch.AfterTouchPressure}";
                    break;
                case MidiCommandCode.ControlChange:
                    ControlChangeEvent controlchange = (ControlChangeEvent)nAudioMidievent;
                    infoevent = BuildInfoTrack(nAudioMidievent, track) + $"ControlChange 0x{(MPTKController)controlchange.Controller:X}/{(MPTKController)controlchange.Controller} {controlchange.ControllerValue}";
                    break;
                case MidiCommandCode.PatchChange:
                    PatchChangeEvent change = (PatchChangeEvent)nAudioMidievent;
                    infoevent = BuildInfoTrack(nAudioMidievent, track) + $"PresetChange {change.Patch} {PatchChangeEvent.GetPatchName(change.Patch)}";
                    break;
                case MidiCommandCode.MetaEvent:
                    MetaEvent meta = (MetaEvent)nAudioMidievent;
                    switch (meta.MetaEventType)
                    {
                        case MetaEventType.SetTempo:
                            TempoEvent tempo = (TempoEvent)meta;
                            infoevent = BuildInfoTrack(nAudioMidievent, track) + string.Format("SetTempo Tempo:{0} MicrosecondsPerQuarterNote:{1}", Math.Round(tempo.Tempo, 0), tempo.MicrosecondsPerQuarterNote);
                            //tempo.Tempo
                            break;

                        case MetaEventType.TimeSignature:
                            // More info here https://paxstellar.fr/2020/09/11/midi-timing/
                            TimeSignatureEvent timesig = (TimeSignatureEvent)meta;
                            // Numerator: counts the number of beats in a measure. 
                            // For example a numerator of 4 means that each bar contains four beats. 

                            // Denominator: number of quarter notes in a beat.0=ronde, 1=blanche, 2=quarter, 3=eighth, etc. 
                            // Set default value
                            infoevent = BuildInfoTrack(nAudioMidievent, track) + "TimeSignature " +
                                $"Numerator (beats per measure):{timesig.Numerator} " +
                                $"Denominator:{timesig.Denominator} - " +
                                $"Beat per quarter:{Convert.ToInt32(Mathf.Pow(2, timesig.Denominator))} - Ticks/Click:{timesig.TicksInMetronomeClick} - No32:{timesig.No32ndNotesInQuarterNote}";
                            break;

                        case MetaEventType.KeySignature:
                            KeySignatureEvent keysig = (KeySignatureEvent)meta;
                            infoevent = BuildInfoTrack(nAudioMidievent, track) + "KeySignature " +
                                $"SharpsFlats:{keysig.SharpsFlats} " +
                                $"MajorMinor:{keysig.MajorMinor}";
                            break;

                        default:
                            string text = meta is TextEvent ? " '" + ((TextEvent)meta).Text + "'" : "";
                            infoevent = BuildInfoTrack(nAudioMidievent, track) + meta.MetaEventType.ToString() + text;
                            break;
                    }
                    break;

                default:
                    // Other midi event
                    infoevent = BuildInfoTrack(nAudioMidievent, track) + string.Format("{0} ({1})", nAudioMidievent.CommandCode, (int)nAudioMidievent.CommandCode);
                    break;
            }
            return infoevent;
        }

        /// <summary>
        /// For Maestro format
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private static string BuildInfoTrack(MPTKEvent e)
        {
            return $"[I:{e.Index:00000} A:{e.Tick:00000} R:{e.RealTime / 1000f:F2}] [T:{e.Track:00} C:{e.Channel:00}] ";
        }

        /// <summary>
        /// For NAudio format
        /// </summary>
        /// <param name="e"></param>
        /// <param name="track"></param>
        /// <returns></returns>
        private static string BuildInfoTrack(MidiEvent e, int track)
        {
            switch (e.CommandCode)
            {
                case MidiCommandCode.NoteOff:
                case MidiCommandCode.NoteOn:
                case MidiCommandCode.KeyAfterTouch:
                case MidiCommandCode.ControlChange:
                case MidiCommandCode.PatchChange:
                case MidiCommandCode.ChannelAfterTouch:
                case MidiCommandCode.PitchWheelChange:
                    return $"A:{e.AbsoluteTime:00000} D:{e.DeltaTime:00000} T:{track:00} C:{e.Channel:00} ";
                default:
                    return $"A:{e.AbsoluteTime:00000} D:{e.DeltaTime:00000} T:{track:00}      ";
            }
        }
    }
}

