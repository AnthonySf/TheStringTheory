
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace MidiPlayerTK
{
    /// @ingroup runtime_midi_loading
    /// <summary>
    /// @Warning - will be soon deprecated, please rather use MidiFilePlayer prefab which have the same features + eventually MIDI sequencer and MIDI synth!
    /// Exemple for just loading a MIDI with MidiFilePlayer:\n
    /// @snippet TestMidiFileLoad.cs Example TheMostSimpleDemoForMidiLoader
    /// </summary>
    [HelpURL("https://paxstellar.fr/prefab-midifileloader/")]
    public partial class MidiFileLoader : MonoBehaviour
    {

        [Preserve]
        public MidiFileLoader() { }

        /// <summary>@brief
        /// Midi name to load. Use the exact name defined in Unity resources folder MidiDB without any path or extension.
        /// Tips: Add Midi files to your project with the Unity menu MPTK or add it directly in the resource folder and open Midi File Setup to automatically integrate Midi in MPTK.
        /// </summary>
        public string MPTK_MidiName
        {
            get
            {
                //Debug.Log("MPTK_MidiName get " + midiNameToPlay);
                return midiNameToPlay;
            }
            set
            {
                Debug.LogWarning("MidiFileLoader is deprecated, please use MidiFilePlayer prefab");
                //Debug.Log("MPTK_MidiName set " + value);
                midiIndexToPlay = MidiPlayerGlobal.MPTK_FindMidi(value);
                //Debug.Log("MPTK_MidiName set index= " + midiIndexToPlay);
                midiNameToPlay = value;
            }
        }
        [SerializeField]
        [HideInInspector]
        private string midiNameToPlay;

        /// <summary>@brief
        /// Index Midi. Find the Index of Midi file from the popup in MidiFileLoader inspector.\n
        /// Tips: Add Midi files to your project with the Unity menu MPTK or add it directly in the resource folder and open Midi File Setup to automatically integrate Midi in MPTK.\n
        /// return -1 if not found
        /// @code
        /// midiFilePlayer.MPTK_MidiIndex = 1;
        /// @endcode
        /// </summary>
        /// <param name="index"></param>
        public int MPTK_MidiIndex
        {
            get
            {
                try
                {
                    //int index = MidiPlayerGlobal.MPTK_FindMidi(MPTK_MidiName);
                    //Debug.Log("MPTK_MidiIndex get " + midiIndexToPlay);
                    return midiIndexToPlay;
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }
                return -1;
            }
            set
            {
                try
                {
                    Debug.LogWarning("MidiFileLoader is deprecated, please use MidiFilePlayer prefab");
                    //Debug.Log("MPTK_MidiIndex set " + value);
                    if (value >= 0 && value < MidiPlayerGlobal.CurrentMidiSet.MidiFiles.Count)
                    {
                        MPTK_MidiName = MidiPlayerGlobal.CurrentMidiSet.MidiFiles[value];
                        // useless, set when set midi name : 
                        midiIndexToPlay = value;
                    }
                    else
                        Debug.LogWarning("MidiFilePlayer - Set MidiIndex value not valid : " + value);
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }
            }
        }

        [SerializeField]
        [HideInInspector]
        private int midiIndexToPlay;

        /// <summary>@brief
        /// If true display in console all midi events loaded. v2.9.0\n
        /// Sets to true could increase greatly the load time. To be used only for debug.
        /// </summary>
        public bool MPTK_LogLoadEvents;
        // Deprecated v2.9.0 public bool MPTK_LogEvents;

        /// <summary>@brief
        /// A MIDI file is a kind of keyboard simulation: in general, a key pressed generates a 'note-on' and a key release generates a 'note-off'.\n
        /// But there is an other possibility in a MIDI file: create a 'note-on' with a velocity=0 which must act as a 'midi-off'\n
        /// By default, MPTK create only one MPTK event with the command NoteOn and a duration.\n
        /// But in some cases, you could want to keep the note-off events if they exist in the MIDI file.\n
        /// Sets to false if there is no need (could greatly increases the MIDI list events).\n
        /// Sets to true to keep 'note-off' events.
        /// </summary>
        public bool MPTK_KeepNoteOff;

        /// <summary>@brief
        /// When set to true, meta MIDI event End Track are keep. Default is false.\n
        /// If set to true, the duration of the MIDI taken into account the End Track Event.
        /// </summary>
        public bool MPTK_KeepEndTrack;


        // Should accept change tempo from Midi Events ? 
        // remove after v2.88
        //public bool MPTK_EnableChangeTempo;

        /// <summary>@brief
        /// Initial tempo read in the Midi.
        /// </summary>
        public double MPTK_InitialTempo;

        /// <summary>@brief
        /// Duration of the midi. 
        /// </summary>
        public TimeSpan MPTK_Duration;

        // V2.88 removed Real Duration of the midi calculated with the midi change tempo events find inside the midi file.
        //public TimeSpan MPTK_RealDuration;

        /// <summary>@brief
        /// Duration (milliseconds) of the midi. 
        /// </summary>
        public float MPTK_DurationMS { get { try { if (midiLoaded != null) return midiLoaded.MPTK_DurationMS; } catch (System.Exception ex) { MidiPlayerGlobal.ErrorDetail(ex); } return 0f; } }


        /// <summary>@brief
        /// Last tick position in Midi: Time of the last midi event in sequence expressed in number of "ticks".\n
        /// #MPTK_TickLast / #MPTK_DeltaTicksPerQuarterNote equal the duration time of a quarter-note regardless the defined tempo.
        /// </summary>
        public long MPTK_TickLast;

        /// <summary>@brief
        /// From TimeSignature event: The numerator counts the number of beats in a measure.\n
        /// For example a numerator of 4 means that each bar contains four beats.\n
        /// This is important to know because usually the first beat of each bar has extra emphasis.\n
        /// https://paxstellar.fr/2020/09/11/midi-timing/
        /// </summary>
        public int MPTK_NumberBeatsMeasure;

        /// <summary>@brief
        /// From TimeSignature event: number of quarter notes in a beat.\n
        /// Equal 2 Power TimeSigDenominator.\n
        /// https://paxstellar.fr/2020/09/11/midi-timing/
        /// </summary>
        public int MPTK_NumberQuarterBeat;

        /// <summary>@brief
        /// From TimeSignature event: The numerator counts the number of beats in a measure.\n
        /// For example a numerator of 4 means that each bar contains four beats.\n
        /// This is important to know because usually the first beat of each bar has extra emphasis.\n
        /// In MIDI the denominator value is stored in a special format. i.e. the real denominator = 2 ^ MPTK_TimeSigNumerator\n
        /// https://paxstellar.fr/2020/09/11/midi-timing/
        /// </summary>
        public int MPTK_TimeSigNumerator;

        /// <summary>@brief
        /// From TimeSignature event: The denominator specifies the number of quarter notes in a beat.\n
        ///   2 represents a quarter-note,\n
        ///   3 represents an eighth-note, etc.\n
        /// https://paxstellar.fr/2020/09/11/midi-timing/
        /// </summary>
        public int MPTK_TimeSigDenominator;

        /// <summary>@brief
        /// From KeySignature event: Values between -7 and 7 and specifies the key signature in terms of number of flast (if negative) or sharps (if positive)
        /// https://www.recordingblogs.com/wiki/midi-key-signature-meta-message
        /// </summary>
        public int MPTK_KeySigSharpsFlats;

        /// <summary>@brief
        /// From KeySignature event: Specifies the scale of the MIDI file.
        /// @li 0 the scale is major.
        /// @li 1 the scale is minor.
        /// https://www.recordingblogs.com/wiki/midi-key-signature-meta-message
        /// </summary>
        public int MPTK_KeySigMajorMinor;

        /// <summary>@brief
        /// From TimeSignature event: The standard MIDI clock ticks every 24 times every quarter note (crotchet)\n
        /// So a MPTK_TicksInMetronomeClick value of 24 would mean that the metronome clicks once every quarter note.\n
        /// A MPTK_TicksInMetronomeClick value of 6 would mean that the metronome clicks once every 1/8th of a note (quaver).\n
        /// https://paxstellar.fr/2020/09/11/midi-timing/
        /// </summary>
        public int MPTK_TicksInMetronomeClick;

        /// <summary>@brief
        /// From TimeSignature event: This value specifies the number of 1/32nds of a note happen every MIDI quarter note.\n
        /// It is usually 8 which means that a quarter note happens every quarter note.\n
        /// https://paxstellar.fr/2020/09/11/midi-timing/
        /// </summary>
        public int MPTK_No32ndNotesInQuarterNote;

        /// <summary>@brief
        /// From the SetTempo event: The tempo is given in micro seconds per quarter beat. \n
        /// To convert this to BPM we needs to use the following equation:BPM = 60,000,000/[tt tt tt]\n
        /// Warning: this value can change during the playing when a change tempo event is find. \n
        /// https://paxstellar.fr/2020/09/11/midi-timing/
        /// </summary>
        public int MPTK_MicrosecondsPerQuarterNote;

        /// <summary>@brief
        /// Delta Ticks Per Beat Note (or DTPQN) represent the duration time in "ticks" which make up a quarter-note. \n
        /// For example, with 96 a duration of an eighth-note in the file would be 48.\n
        /// From a MIDI file, this value is found in the MIDI Header and remains constant for all the MIDI file.\n
        /// More info here https://paxstellar.fr/2020/09/11/midi-timing/\n
        /// </summary>
        public int MPTK_DeltaTicksPerQuarterNote;

        /// <summary>@brief
        /// Count of track read in the Midi file.\n
        /// Not to be confused with channel. A track can contains midi events for different channel.
        /// </summary>
        public int MPTK_TrackCount;

        private MidiLoad midiLoaded;

        /// <summary>@brief 
        /// Gets all the MPTK MIDI events available in the midi file. New v2.9.0\n
        /// </summary>
        public List<MPTKEvent> MPTK_MidiEvents
        {
            get
            {
                return midiLoaded != null ? midiLoaded.MPTK_MidiEvents : null;
            }
        }
        /// <summary>@brief
        /// Gets detailed information about the midi loaded. 
        /// </summary>
        public MidiLoad MPTK_MidiLoaded { get { return midiLoaded; } }

        void Awake()
        {
            //Debug.Log("Awake MidiFileLoader");
        }

        void Start()
        {
            //Debug.Log("Start MidiFileLoader");
        }


        /// <summary>@brief
        /// Loads the midi file defined with MPTK_MidiName or MPTK_MidiIndex or from a array of bytes.\n
        /// Look at MPTK_MidiLoaded for detailed information about the MIDI loaded.
        /// MPTK_MidiEvents will contains all MIDI events loaded.
        /// </summary>
        /// <param name="midiBytesToLoad">byte arry from a midi stream</param>
        /// <returns>true if loading succeed/returns>
        public bool MPTK_Load(byte[] midiBytesToLoad = null)
        {
            bool result = false;
            try
            {
                Debug.LogWarning("MidiFileLoader is deprecated, please use MidiFilePlayer prefab");

                // Load description of available soundfont
                //if (MidiPlayerGlobal.ImSFCurrent != null && MidiPlayerGlobal.CurrentMidiSet != null && MidiPlayerGlobal.CurrentMidiSet.MidiFiles != null && MidiPlayerGlobal.CurrentMidiSet.MidiFiles.Count > 0)
                {
                    if (string.IsNullOrEmpty(MPTK_MidiName))
                        MPTK_MidiName = MidiPlayerGlobal.CurrentMidiSet.MidiFiles[0];
                    int selectedMidi = MidiPlayerGlobal.CurrentMidiSet.MidiFiles.FindIndex(s => s == MPTK_MidiName);
                    if (selectedMidi < 0)
                    {
                        Debug.LogWarning("MidiFilePlayer - MidiFile " + MPTK_MidiName + " not found. Try with the first in list.");
                        selectedMidi = 0;
                        MPTK_MidiName = MidiPlayerGlobal.CurrentMidiSet.MidiFiles[0];
                    }

                    try
                    {
                        midiLoaded = new MidiLoad();

                        // No midi byte array, try to load from MidiFile from resource
                        if (midiBytesToLoad == null || midiBytesToLoad.Length == 0)
                        {
                            TextAsset mididata = Resources.Load<TextAsset>(System.IO.Path.Combine(MidiPlayerGlobal.MidiFilesDB, MPTK_MidiName));
                            midiBytesToLoad = mididata.bytes;
                        }

                        midiLoaded.MPTK_LogLoadEvents = MPTK_LogLoadEvents;
                        midiLoaded.MPTK_KeepNoteOff = MPTK_KeepNoteOff;
                        midiLoaded.MPTK_KeepEndTrack = MPTK_KeepEndTrack;
                        midiLoaded.MPTK_LogLoadEvents = MPTK_LogLoadEvents;
                        midiLoaded.MPTK_EnableChangeTempo = true;
                        if (!midiLoaded.MPTK_Load(midiBytesToLoad))
                            return false;
                        SetAttributes();
                        result = true;
                    }
                    catch (System.Exception ex)
                    {
                        MidiPlayerGlobal.ErrorDetail(ex);
                    }
                }
                //else
                //    Debug.LogWarning(MidiPlayerGlobal.ErrorNoMidiFile);
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return result;
        }

        private void SetAttributes()
        {
            if (midiLoaded != null)
            {
                MPTK_InitialTempo = midiLoaded.MPTK_InitialTempo;
                MPTK_Duration = midiLoaded.MPTK_Duration;
                MPTK_TickLast = midiLoaded.MPTK_TickLast;
                MPTK_NumberBeatsMeasure = midiLoaded.MPTK_NumberBeatsMeasure;
                MPTK_NumberQuarterBeat = midiLoaded.MPTK_NumberQuarterBeat;
                MPTK_TimeSigNumerator = midiLoaded.MPTK_TimeSigNumerator;
                MPTK_TimeSigDenominator = midiLoaded.MPTK_TimeSigDenominator;
                MPTK_KeySigSharpsFlats = midiLoaded.MPTK_KeySigSharpsFlats;
                MPTK_KeySigMajorMinor = midiLoaded.MPTK_KeySigMajorMinor;
                MPTK_TicksInMetronomeClick = midiLoaded.MPTK_TicksInMetronomeClick;
                MPTK_No32ndNotesInQuarterNote = midiLoaded.MPTK_No32ndNotesInQuarterNote;
                MPTK_MicrosecondsPerQuarterNote = midiLoaded.MPTK_MicrosecondsPerQuarterNote;
                MPTK_DeltaTicksPerQuarterNote = midiLoaded.MPTK_DeltaTicksPerQuarterNote;
                MPTK_TrackCount = midiLoaded.MPTK_TrackCount;
            }
        }
        /// <summary>@brief
        /// Read the list of midi events available in the Midi file from a ticks position to an end position into a List of MPTKEvent
        /// @snippet TestMidiFileLoad.cs Example TheMostSimpleDemoForMidiLoader
        /// See full example in TestMidiFileLoad.cs
        /// </summary>
        /// <param name="fromTicks">ticks start, default from start</param>
        /// <param name="toTicks">ticks end, default to the end</param>
        /// <returns></returns>
        public List<MPTKEvent> MPTK_ReadMidiEvents(long fromTicks = 0, long toTicks = long.MaxValue)
        {
            Debug.LogWarning("MidiFileLoader is deprecated, please use MidiFilePlayer prefab");
            if (midiLoaded == null)
            {
                NoMidiLoaded("MPTK_ReadMidiEvents");
                return null;
            }
            midiLoaded.MPTK_KeepNoteOff = MPTK_KeepNoteOff;
            midiLoaded.MPTK_KeepEndTrack = MPTK_KeepEndTrack;
            midiLoaded.MPTK_LogLoadEvents = MPTK_LogLoadEvents;
            midiLoaded.MPTK_EnableChangeTempo = true;
            return midiLoaded.MPTK_ReadMidiEvents(fromTicks, toTicks);
        }

        private void NoMidiLoaded(string action)
        {
            Debug.LogWarning(string.Format("No MIDI loaded, {0} canceled", action));
        }
        /// <summary>@brief
        /// Read next Midi from the list of midi defined in MPTK (see Unity menu Midi)
        /// </summary>
        public void MPTK_Next()
        {
            try
            {
                if (MidiPlayerGlobal.CurrentMidiSet.MidiFiles != null && MidiPlayerGlobal.CurrentMidiSet.MidiFiles.Count > 0)
                {
                    int selectedMidi = 0;
                    //Debug.Log("Next search " + MPTK_MidiName);
                    if (!string.IsNullOrEmpty(MPTK_MidiName))
                        selectedMidi = MidiPlayerGlobal.CurrentMidiSet.MidiFiles.FindIndex(s => s == MPTK_MidiName);
                    if (selectedMidi >= 0)
                    {
                        selectedMidi++;
                        if (selectedMidi >= MidiPlayerGlobal.CurrentMidiSet.MidiFiles.Count)
                            selectedMidi = 0;
                        MPTK_MidiName = MidiPlayerGlobal.CurrentMidiSet.MidiFiles[selectedMidi];
                        //Debug.Log("Next found " + MPTK_MidiName);
                    }
                }
                else
                    Debug.LogWarning(MidiPlayerGlobal.ErrorNoMidiFile);
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        /// <summary>@brief
        /// Read previous Midi from the list of midi defined in MPTK (see Unity menu Midi)
        /// </summary>
        public void MPTK_Previous()
        {
            try
            {
                if (MidiPlayerGlobal.CurrentMidiSet.MidiFiles != null && MidiPlayerGlobal.CurrentMidiSet.MidiFiles.Count > 0)
                {
                    int selectedMidi = 0;
                    if (!string.IsNullOrEmpty(MPTK_MidiName))
                        selectedMidi = MidiPlayerGlobal.CurrentMidiSet.MidiFiles.FindIndex(s => s == MPTK_MidiName);
                    if (selectedMidi >= 0)
                    {
                        selectedMidi--;
                        if (selectedMidi < 0)
                            selectedMidi = MidiPlayerGlobal.CurrentMidiSet.MidiFiles.Count - 1;
                        MPTK_MidiName = MidiPlayerGlobal.CurrentMidiSet.MidiFiles[selectedMidi];
                    }
                }
                else
                    Debug.LogWarning(MidiPlayerGlobal.ErrorNoMidiFile);
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        /// <summary>@brief
        /// Return note length as https://en.wikipedia.org/wiki/Note_value 
        /// </summary>
        /// <param name="note"></param>
        /// <returns>MPTKEvent.EnumLength</returns>
        public MPTKEvent.EnumLength MPTK_NoteLength(MPTKEvent note)
        {
            if (midiLoaded != null)
                return midiLoaded.NoteLength(note);
            else
                NoMidiLoaded("MPTK_NoteLength");
            return MPTKEvent.EnumLength.Sixteenth;
        }
    }
}

