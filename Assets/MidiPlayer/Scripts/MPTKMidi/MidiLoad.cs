//#define MPTK_PRO
using MPTK.NAudio.Midi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace MidiPlayerTK
{

    /// @ingroup runtime_midi_loading
    /// <summary>
    /// Base class for loading a MIDI file and reading MIDI event in a MIDI sequencer. <b>It's not possible to instanciate directly this class.</b>\n
    /// Rather, use MidiFilePlayer to load a MIDI
    /// This class is used by MidiFilePlayer, MidiListPlayer, MidiFileWrite2, MidiFileLoader (see members MPTK_MidiLoaded of these classes).\n
    /// </summary>
    public partial class MidiLoad
    {
        //! @cond NODOC
        public MidiFile midifile;
        public bool EndMidiEvent;
        //public double QuarterPerMinuteValue;
        public string SequenceTrackName = "";
        public string ProgramName = "";
        public string TrackInstrumentName = "";
        public string TextEvent = "";
        public string Copyright = "";
        public bool endPlayAtLastNote;
        //! @endcond

        /// <summary>@brief
        /// List of MIDI events loaded and played by the MIDI sequencer.
        /// It's possible to modify directly the content but with unpredictable results!
        /// </summary>
        public List<MPTKEvent> MPTK_MidiEvents;


        #region Tempo and time

        /// <summary>@brief
        /// Read from the SetTempo event: The tempo is given in micro seconds per quarter beat. 
        /// To convert this to BPM we needs to use the following equation:BPM = 60,000,000/[tt tt tt]
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
        /// Real-time duration in millisecond of a MIDI tick. This value is updated when a tempo change is found.\n
        /// The pulse length is the minimum time in millisecond between two MIDI events.\n
        /// @note
        /// - Depends on the current tempo, the #MPTK_DeltaTicksPerQuarterNote and the Speed.\n
        /// - Formula: Pulse = (60000000 /  MPTK_CurrentTempo) / MPTK_DeltaTicksPerQuarterNote / 1000 / Speed
        /// </summary>
        public double MPTK_Pulse;

        public double MPTK_PulseLenght { get { Debug.LogWarning("MPTK_PulseLenght has been deprecated, please investigate MPTK_Pulse in place"); return 0d; } }

        /// <summary>@brief
        /// Initial tempo found in the MIDI. Available from MPTK_MidiLoaded attribut from MidiFilePLayer or MidiExternal.
        /// @code
        ///  if (midiPlayer.MPTK_MidiLoaded != null) 
        ///     infoMidi += $"Initial Tempo: {midiPlayer.MPTK_MidiLoaded.MPTK_InitialTempo:F2}\n";
        /// @endcode
        /// @note 
        ///     - Available only when a MIDI is loaded.
        /// </summary>
        public double MPTK_InitialTempo;

        /// <summary>@brief
        /// Get or change the current tempo played by the internal MIDI sequencer. Available from MPTK_MidiLoaded attribut from MidiFilePLayer or MidiExternal.
        /// @code
        ///  if (midiPlayer.MPTK_MidiLoaded != null) 
        ///     infoMidi += $"Current Tempo: {midiPlayer.MPTK_MidiLoaded.MPTK_CurrentTempo:F2}\n";
        /// @endcode
        /// @note 
        ///     - Available only when a MIDI is loaded.\n
        ///     - Warning: changing the current tempo when playing has no impact on the calculated duration of the MIDI.
        /// https://en.wikipedia.org/wiki/Tempo
        /// </summary>
        public double MPTK_CurrentTempo
        {
            get
            {
                return fluid_player_get_bpm();
            }
            set
            {
                if (value > 0)
                    fluid_player_set_bpm((int)value);
            }
        }

        public double Speed { get => speed; }

        /// <summary>@brief
        /// Real duration expressed in TimeSpan of the full midi from the first event (tick=0) to the last event.\n
        /// If #MPTK_KeepEndTrack is false, the MIDI events End Track are not considered to calculate this time.\n
        /// The tempo changes are taken into account if #MPTK_EnableChangeTempo is set to true before loading the MIDI.
        /// </summary>
        public TimeSpan MPTK_Duration;

        /// <summary>@brief
        /// Real duration expressed in milliseconds of the full midi from the first event (tick=0) to the last event.\n
        /// If #MPTK_KeepEndTrack is false, the MIDI events End Track are not considered to calculate this time.\n
        /// The tempo changes are taken into account if #MPTK_EnableChangeTempo is set to true before loading the MIDI.
        /// </summary>
        public float MPTK_DurationMS;

        /// <summary>@brief
        /// Should enable or disable change tempo from MIDI Events ? If disabled, the first tempo defined (or default to 120 BPM) will be used for all the MIDI played.
        /// </summary>
        public bool MPTK_EnableChangeTempo;


        #endregion

        // ------------------------------------------
        #region Position

        /// <summary>@brief
        /// Tick to start playing the MIDI.
        /// @snippet MidiLoop.cs ExampleMidiLoop
        /// </summary>
        public long MPTK_TickStart;

        /// <summary>@brief
        /// Tick to end playing the MIDI. All MIDI events with MIDI tick higher or equal are not played.
        /// @snippet MidiLoop.cs ExampleMidiLoop
        /// </summary>
        public long MPTK_TickEnd;            // stop the MIDI reading at this tick

        /// <summary>@brief
        /// Tick value of the last MIDI event found in the MIDI loaded.
        /// </summary>
        public long MPTK_TickLast;

        /// <summary>@brief
        /// Get the tick value of the last MIDI event played.\n
        /// Set the tick value of the next MIDI event to played.\n
        /// Unlike MPTK_TickPlayer, which is continuously updated, this property is only updated when a MIDI event is read from the sequencer.
        /// @details
        /// A MIDI tick is an easy way to identify a position in a song, independent of time, which can vary with tempo change events.\n
        /// The number of ticks per quarter note is constant throughout a MIDI, it's a property of the whole MIDI. see #MPTK_DeltaTicksPerQuarterNote.\n
        /// With a 4/4 time signature, the length of a bar is 4 * #MPTK_DeltaTicksPerQuarterNote.\n
        /// More info here https://paxstellar.fr/2020/09/11/midi-timing/
        /// @notes
        /// @li Works only when the MIDI sequencer is playing.
        /// @li It's not possible to set the current tick when MIDI is not playing. Instead, use the OnEventStartPlayMidi event to change the start position.
        /// @li See also MidiFilePlayer#MPTK_Position: set or get the position in milliseconds.
        /// </summary>
        public long MPTK_TickCurrent;

        /// <summary>@brief 
        /// This parameter is used when changing the playback position in a MIDI.\n
        /// By default (false), all events except note-on are played from the beginning of the MIDI to the new position, to put the synthesizer back in the right context (tempo, selected instruments, controller, ...).\n
        /// If set to true, the current playback position is set, but the previous context is retained. This can have undesirable (or funny!) effects on some MIDI, but it also makes it possible to change position much more quickly.\n
        /// It's a choice to be made according to your needs.\n
        /// @note
        /// - Value returns to false (default) each time a MIDI is loaded. You can load the MIDI with #MPTK_Load(), change this parameter and play the MIDI with #MPTK_Play().        /// </summary>
        public bool MPTK_RawSeek;

        /// <summary>@brief
        /// Get the real-time tick value of the MIDI player.\n
        /// Unlike MPTK_TickCurrent which is only updated when a MIDI event is read, this property is updated continuously while the MIDI thread is playing.
        /// @details
        /// More info about tick here https://paxstellar.fr/2020/09/11/midi-timing/
        /// @notes
        /// @li Works only when MIDI is playing.
        /// @li Can't be changed.
        /// @li Used by the MIDI Editor (pro) to display the sequencer position in real time.
        /// @li Available from MidiFilePlayer.MPTK_MidiLoaded
        /// @code
        ///  void Update()
        ///  {
        ///    if (mfPlayer != null && mfPlayer.MPTK_MidiLoaded != null)
        ///        TextMidiTime.text = $"Real-time MIDI sequencer position: {mfPlayer.MPTK_MidiLoaded.MPTK_TickPlayer}";
        ///  }
        /// @endcode
        /// </summary>
        public long MPTK_TickPlayer;


        /// <summary>@brief
        /// Tick position for the first note-on found.\n
        /// Most MIDI doesn't start playing a note immediately. There is often a delay.\n
        /// Use this attribute to know the tick position where the player will start playing a sound.\n
        /// See also #MPTK_PositionFirstNote
        /// </summary>
        public long MPTK_TickFirstNote;

        /// <summary>@brief
        /// Tick position for the last note-on found.\n
        /// There are often other MIDI events after the last note-on: for example, the track-end event.\n
        /// Use this attribute to know the tick position at which all sound will stop.\n
        /// See also #MPTK_PositionLastNote which returns the last MIDI tick.
        /// </summary>
        public long MPTK_TickLastNote;

        /// <summary>@brief
        /// Last MIDI event note-on found in MIDI.\n
        /// There are often other MIDI events after the last note-on: for example, the track-end event.\n
        /// Use this attribute to know the tick position time when all sound will stop.\n
        /// See also #MPTK_PositionLastNote which returns the last MIDI tick.
        /// @version v2.12.1
        /// </summary>
        public MPTKEvent MPTK_EventLastNote;

        /// <summary>@brief
        /// Real-time position in milliseconds for the first note-on found.\n
        /// Most MIDI doesn't start playing a note immediately. There is often a delay.\n
        /// Use this attribute to know the real time at which it will start.\n
        /// See also #MPTK_TickFirstNote
        /// </summary>
        public double MPTK_PositionFirstNote;

        /// <summary>@brief
        /// Real-time position in milliseconds for the last note-on found in the MIDI.\n
        /// There are often other MIDI events after the last note-on: for example, the track-end event.\n
        /// Use this attribute to know the real time when all sound will stop.\n
        /// See also #MPTK_DurationMS which gives the full duration of all MIDI events including track end, start and end control, ....\n
        /// See also #MPTK_TickLastNote
        /// </summary>
        public double MPTK_PositionLastNote;

        /// <summary>@brief
        /// Measure position for the last note-on found.\n
        /// There is often other MIDI events after the last note-on: for example event track-end.
        /// </summary>
        public long MPTK_MeasureLastNote;

        /// <summary>@brief
        /// Current MIDI event read when the MIDI sequencer is playing the MIDI. See #MPTK_TickCurrent.
        /// </summary>
        public MPTKEvent MPTK_LastEventPlayed;

        #endregion

        // ------------------------------------------
        #region TempoSignatureMap

        /// <summary>@brief
        /// List of all tempo changes found in the MIDI.
        /// </summary>
        public List<MPTKTempo> MPTK_TempoMap;

        /// <summary>@brief
        /// List of all signature changes found in the MIDI.
        /// </summary>
        public List<MPTKSignature> MPTK_SignMap;

        /// <summary>@brief
        /// Current index of the Tempo segment.
        /// </summary>
        public int MPTK_CurrentIndexTempoMap;

        /// <summary>@brief
        /// Current Tempo Map.
        /// </summary>
        public MPTKTempo MPTK_CurrentTempoMap;

        /// <summary>@brief
        /// Current index of the Signature segment.
        /// </summary>
        public int MPTK_CurrentIndexSignMap;

        /// <summary>@brief
        /// Current Signature segment.
        /// </summary>
        public MPTKSignature MPTK_CurrentSignMap;

        #endregion


        // ------------------------------------------
        #region TimeSignatureEvent

        /// <summary>@brief
        /// Updated from TimeSignature event when playing MIDI: The numerator counts the number of beats in a measure.\n
        /// For example a numerator of 4 means that each bar contains four beats.\n
        /// This is important to know because usually the first beat of each bar has extra emphasis.\n
        /// In MIDI the denominator value is stored in a special format. i.e. the real denominator = 2 ^ #MPTK_TimeSigNumerator\n
        /// https://paxstellar.fr/2020/09/11/midi-timing/
        /// </summary>
        public int MPTK_TimeSigNumerator;

        /// <summary>@brief
        /// Updated from TimeSignature event when playing MIDI: The denominator specifies the number of quarter notes in a beat.
        ///   - 2 represents a quarter-note,\n
        ///   - 3 represents an eighth-note, etc.\n
        /// https://paxstellar.fr/2020/09/11/midi-timing/
        /// </summary>
        public int MPTK_TimeSigDenominator;

        /// <summary>@brief
        /// Updated from TimeSignature event when playing MIDI: the numerator counts the number of beats in a measure.\n
        /// For example a numerator of 4 means that each bar contains four beats.\n
        /// @note 
        /// - This value is updated at each time signature meta event played.
        /// - This is important to know because usually the first beat of each bar has extra emphasis.
        /// https://paxstellar.fr/2020/09/11/midi-timing/
        /// </summary>
        public int MPTK_NumberBeatsMeasure;

        /// <summary>@brief
        /// Updated from TimeSignature event when playing MIDI: describe of what note value a beat is (ie, how many quarter notes there are in a beat).
        /// @note
        /// This value is updated at each time signature meta event played.\n
        /// Calculated with 2^MPTK_TimeSigDenominatorn
        /// -  1 for a whole-note
        /// -  2 for a half-note
        /// -  4 for a quarter-note
        /// -  8 for an eighth-note, etc.
        /// 
        /// Equal 2 Power TimeSigDenominator.\n
        /// https://paxstellar.fr/2020/09/11/midi-timing/
        /// </summary>
        public int MPTK_NumberQuarterBeat;

        /// <summary>@brief
        /// Updated from TimeSignature event when playing MIDI: The standard MIDI clock ticks every 24 times every quarter note (crotchet)\n
        /// So a #MPTK_TicksInMetronomeClick value of 24 would mean that the metronome clicks once every quarter note.\n
        /// A #MPTK_TicksInMetronomeClick value of 6 would mean that the metronome clicks once every 1/8th of a note (quaver).\n
        /// https://paxstellar.fr/2020/09/11/midi-timing/
        /// </summary>
        public int MPTK_TicksInMetronomeClick;

        /// <summary>@brief
        /// Updated from TimeSignature event when playing MIDI: This value specifies the number of 1/32nds of a note happen every MIDI quarter note.\n
        /// It is usually 8 which means that a quarter note happens every quarter note.\n
        /// https://paxstellar.fr/2020/09/11/midi-timing/
        /// </summary>
        public int MPTK_No32ndNotesInQuarterNote;
        #endregion

        // ------------------------------------------
        #region KeySignatureEvent

        /// <summary>@brief
        /// Updated from KeySignature event when playing MIDI: values between -7 and 7 and specifies the key signature in terms of number of flats (if negative) or sharps (if positive)
        /// https://www.recordingblogs.com/wiki/midi-key-signature-meta-message
        /// </summary>
        public int MPTK_KeySigSharpsFlats;

        /// <summary>@brief
        /// Updated from KeySignature event when playing MIDI: specifies the scale of the MIDI file.
        /// -  0 the scale is major.
        /// -  1 the scale is minor.
        /// https://www.recordingblogs.com/wiki/midi-key-signature-meta-message
        /// </summary>
        public int MPTK_KeySigMajorMinor;

        #endregion

        /// <summary>@brief
        /// Count of track in the MIDI file
        /// </summary>
        public int MPTK_TrackCount;

        /// <summary>@brief
        /// Processing time in millisecond for loading the MIDI file.
        /// </summary>
        public float MPTK_LoadTime;

        /// <summary>@brief
        /// If true display in console all midi events when a MIDI file is loaded. v2.9.0\n
        /// Set to true could increase greatly the load time. To be used only for debug.
        /// </summary>
        public bool MPTK_LogLoadEvents;

        /// If the value is true, text read from Text META (e.g. lyrics) will be read with UTF8 encoding. The default is false.\n
        /// The MIDI standard only allows ASCII characters for this META, but with this extension you will be able to read and display\n
        /// characters like Korean, Chinese, Japanese and even French accented letters ;-)
        /// @version 2.11.3
        public bool MPTK_ExtendedText;

        /// <summary>@brief
        /// A MIDI file is a kind of keyboard simulation: in general, a key pressed generates a 'note-on' and a key release generates a 'note-off'.\n
        /// But there is an other possibility in a MIDI file: create a 'note-on' with a velocity=0 wich must act as a 'midi-off'\n
        /// By default, MPTK create only one MPTK event with the command NoteOn and a duration.\n
        /// But in some cases, you could want to keep the note-off events if they exist in the MIDI file.\n
        /// Set to false if there is no need (could greatly increases the MIDI list events).\n
        /// Set to true to keep 'note-off' events.
        /// </summary>
        public bool MPTK_KeepNoteOff;

        /// <summary>
        /// @deprecated v2.9.0 rather use MPTK_KeepNoteOff
        /// </summary>
        public bool KeepNoteOff;

        /// <summary>@brief
        /// If set to true, the meta MIDI events 'End Track' are kept and the MIDI duration includes the 'End Track' events.\n
        /// Default value is false.
        /// </summary>
        public bool MPTK_KeepEndTrack;

        public bool ReadyToPlay;

        private long Quantization;
        //private long CurrentTick;
        private double speed = 1d;
        //private double LastTimeFromStartMS;

        public long TickSeek;           /* new position in tempo ticks (midi ticks) for seeking was seek_ticks*/
        public long TickFromTempoChange;          /* the number of ticks passed at the last tempo change was start_tick */
        //private int begin_msec;           /* the time (msec) of the beginning of the file */
        private int next_event;
        private int start_msec;           /* the start time of the last tempo change */
        private int cur_msec;             /* the current time */
        private int miditempo;            /* as indicated by MIDI SetTempo: n 24th of a usec per midi-clock. bravo! */
        //private double deltatime;         /* milliseconds per midi tick. depends on set-tempo */

        private DateTime timeStartLoad;

        public MidiLoad()
        {
            //ReadyToStarted = false;
            ReadyToPlay = false;

        }

        /// <summary>@brief
        /// Removes all information about the MIDI loaded
        /// </summary>
        public void MPTK_Clear()
        {
            InitMidiLoadAttributes();
        }
        /// <summary>
        /// Initialize MIDI attributs when loading a MIDI
        /// </summary>
        private void InitMidiLoadAttributes()
        {
            MPTK_LoadTime = 0;
            MPTK_Pulse = 0;
            MPTK_MidiEvents = null;
            MPTK_Duration = TimeSpan.Zero;
            MPTK_DurationMS = 0f;
            MPTK_TickLast = 0;
            MPTK_LastEventPlayed = null;
            MPTK_TickFirstNote = -1;
            MPTK_TickLastNote = -1;
            MPTK_EventLastNote = null;
            MPTK_MeasureLastNote = -1;
            MPTK_PositionFirstNote = 0;
            MPTK_PositionLastNote = 0;
            MPTK_NumberBeatsMeasure = 4; // Default value if there is no TimeSignatureEvent
            MPTK_NumberQuarterBeat = 0;
            MPTK_TimeSigNumerator = 0;
            MPTK_TimeSigDenominator = 0;
            MPTK_KeySigSharpsFlats = 0;
            MPTK_KeySigMajorMinor = 0;
            MPTK_TicksInMetronomeClick = 0;
            MPTK_No32ndNotesInQuarterNote = 0;
            MPTK_MicrosecondsPerQuarterNote = 0;
            MPTK_DeltaTicksPerQuarterNote = 0;
            MPTK_TrackCount = 0;
            MPTK_TickCurrent = 0;
            MPTK_CurrentIndexTempoMap = 0;
            MPTK_CurrentIndexSignMap = 0;
            ClearMetaText();

        }

        /// <summary>@brief
        /// Load Midi from midi MPTK referential (Unity resource). \n
        /// The index of the Midi file can be found in the windo "Midi File Setup". Display with menu MPTK / Midi File Setup\n
        /// If result is true, MPTK_MidiEvents contains list of MIDI events.
        /// @code
        /// public MidiLoad MidiLoaded;
        /// // .....
        /// MidiLoaded = new MidiLoad();
        /// MidiLoaded.MPTK_Load(14) // index for "Beattles - Michelle"
        /// Debug.Log("Duration:" + MidiLoaded.MPTK_Duration);
        /// @endcode
        /// </summary>
        /// <param name="index"></param>
        /// <param name="strict">If true will error on non-paired note events, default:false</param>
        /// <returns>true if loaded</returns>
        public bool MPTK_Load(int index, bool strict = false, bool extendedText = false)
        {
            bool ok = true;
            InitMidiLoadAttributes();
            try
            {
                if (MidiPlayerGlobal.CurrentMidiSet.MidiFiles != null && MidiPlayerGlobal.CurrentMidiSet.MidiFiles.Count > 0)
                {
                    if (index >= 0 && index < MidiPlayerGlobal.CurrentMidiSet.MidiFiles.Count)
                    {
                        timeStartLoad = DateTime.Now;
                        string midiname = MidiPlayerGlobal.CurrentMidiSet.MidiFiles[index];
                        TextAsset mididata = Resources.Load<TextAsset>(Path.Combine(MidiPlayerGlobal.MidiFilesDB, midiname));
                        midifile = new MidiFile(mididata.bytes, strict);
                        if (midifile != null)
                        {
                            MPTK_DeltaTicksPerQuarterNote = midifile.DeltaTicksPerQuarterNote;
                            MPTK_MidiEvents = ConvertNAudioEventsToMPTKEvents();
                            calculateTiming();
                            if (MPTK_LogLoadEvents) MPTK_DisplayMidiAttributes();
                        }
                    }
                    else
                    {
                        Debug.LogWarningFormat("MidiLoad - index {0} out of range ", index);
                        ok = false;
                    }
                }
                else
                {
                    Debug.LogWarningFormat("MidiLoad - index:{0} - {1}", index, MidiPlayerGlobal.ErrorNoMidiFile);
                    ok = false;
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
                ok = false;
            }
            return ok;
        }


        /// <summary>@brief
        /// Load Midi from an array of bytes. If result is true, MPTK_MidiEvents contains list of MIDI events.
        /// </summary>
        /// <param name="datamidi">byte arry midi</param>
        /// <param name="strict">If true will error on non-paired note events, default:false</param>
        /// <returns>true if loaded</returns>
        public bool MPTK_Load(byte[] datamidi, bool strict = false)
        {
            bool ok = true;
            InitMidiLoadAttributes();
            try
            {
                timeStartLoad = DateTime.Now;
                midifile = new MidiFile(datamidi, strict, extendedText: MPTK_ExtendedText);
                if (midifile != null)
                {
                    if (midifile.FileFormat == 0)
                    {
                        // V2.9.0 automatic migration from format 0 to format 1
                        midifile.Events.MidiFileType = 1;
                        midifile.Events.PrepareForExport();
                    }
                    MPTK_DeltaTicksPerQuarterNote = midifile.DeltaTicksPerQuarterNote;
                    MPTK_MidiEvents = ConvertNAudioEventsToMPTKEvents();
                    calculateTiming();
                    if (MPTK_LogLoadEvents) MPTK_DisplayMidiAttributes();
                }
                else
                    ok = false;
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
                ok = false;
            }
            return ok;
        }

        /// <summary>@brief
        /// Load Midi from a Midi file from Unity resources. The Midi file must be present in Unity MidiDB ressource folder.
        /// @code
        /// public MidiLoad MidiLoaded;
        /// // .....
        /// MidiLoaded = new MidiLoad();
        /// MidiLoaded.MPTK_Load("Beattles - Michelle")
        /// Debug.Log("Duration:" + MidiLoaded.MPTK_Duration);
        /// @endcode
        /// </summary>
        /// <param name="midiname">Midi file name without path and extension</param>
        /// <param name="strict">if true, check strict compliance with the Midi norm</param>
        /// <returns>true if loaded</returns>
        public bool MPTK_Load(string midiname, bool strict = false)
        {
            try
            {
                TextAsset mididata = Resources.Load<TextAsset>(Path.Combine(MidiPlayerGlobal.MidiFilesDB, midiname));
                if (mididata != null && mididata.bytes != null && mididata.bytes.Length > 0)
                    return MPTK_Load(mididata.bytes, strict);
                else
                    Debug.LogWarningFormat("MIDI {0} not loaded from folder {1}", midiname, MidiPlayerGlobal.MidiFilesDB);
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return false;
        }

        /// <summary>@brief
        /// Read the list of midi events available in the Midi from a ticks position to an end position.
        /// </summary>
        /// <param name="fromTicks">ticks start, default 0</param>
        /// <param name="toTicks">ticks end, default end of Midi file</param>
        /// <returns></returns>
        public List<MPTKEvent> MPTK_ReadMidiEvents(long fromTicks = 0, long toTicks = long.MaxValue)
        {
            List<MPTKEvent> midievents = new List<MPTKEvent>();
            try
            {
                if (MPTK_MidiEvents != null)
                {
                    foreach (MPTKEvent trackEvent in MPTK_MidiEvents)
                    {
                        //if (Quantization != 0)
                        //    mptkEvent.AbsoluteQuantize = ((mptkEvent.Event.AbsoluteTime + Quantization / 2) / Quantization) * Quantization;
                        //else
                        //    mptkEvent.AbsoluteQuantize = mptkEvent.Event.AbsoluteTime;

                        //Debug.Log("ReadMidiEvents - timeFromStartMS:" + Convert.ToInt32(timeFromStartMS) + " LastTimeFromStartMS:" + Convert.ToInt32(LastTimeFromStartMS) + " CurrentPulse:" + CurrentPulse + " AbsoluteQuantize:" + mptkEvent.AbsoluteQuantize);

                        //if (mptkEvent.AbsoluteQuantize >= fromTicks && mptkEvent.AbsoluteQuantize <= toTicks)
                        if (trackEvent.Tick >= fromTicks && trackEvent.Tick <= toTicks)
                        {
                            //MPTKEvent mptkEvent = ConvertTrackEventToMPTKEvent(mptkEvent);
                            //if (mptkEvent != null)
                            midievents.Add(trackEvent);
                        }
                        MPTK_TickCurrent = trackEvent.Tick;
                        MPTK_TickLast = trackEvent.Tick;

                        if (trackEvent.Tick > toTicks)
                            break;
                    }
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return midievents;
        }

        /// <summary>@brief
        /// @deprecated Convert the tick duration to a real time duration in millisecond regarding the current tempo.
        /// Please investigate the tempo map capabilities, see #MPTK_TempoMap.
        /// </summary>
        /// <param name="tick">duration in ticks</param>
        /// <returns>duration in milliseconds</returns>
        public double MPTK_ConvertTickToTime(long tick)
        {
            //return tick * MPTK_Pulse;
            Debug.LogWarning("MidiLoad.MPTK_ConvertTickToTime is deprecated and will be removed. Please investigate the tempo map capabilities, see MPTK_TempoMap.");
            return 0;
        }

        /// <summary>@brief
        /// @deprecated Convert a real time duration in millisecond to a number of tick regarding the current tempo.
        /// Please investigate the tempo map capabilities, see #MPTK_TempoMap.
        /// </summary>
        /// <param name="time">duration in milliseconds</param>
        /// <returns>duration in ticks</returns>
        public long MPTK_ConvertTimeToTick(double time)
        {
            //if (MPTK_Pulse != 0d)
            //    return Convert.ToInt64((time / MPTK_Pulse) + 0.5d);
            //else
            //    return 0;
            Debug.LogWarning("MidiLoad.MPTK_ConvertTimeToTick is deprecated and will be removed. Please investigate the tempo map capabilities, see MPTK_TempoMap.");
            return 0;
        }

        /// <summary>@brief
        /// Search for a Midi event from a time position expressed in millisecond.\n
        /// So time=19.3 and time=19.9 will find the same event.\n
        /// </summary>
        /// <param name="time">position in milliseconds</param>
        /// <returns>MPTKEvent or null</returns>
        public MPTKEvent MPTK_SearchEventFromTime(double time)
        {
            if (time < 0d || time >= MPTK_DurationMS)
                return null;
            else
            {
                //foreach (TrackMidiEvent mptkEvent in MPTK_MidiEvents)
                //    if (mptkEvent.RealTime >= time)
                //    {
                //        MPTKEvent mptkEvent = ConvertTrackEventToMPTKEvent(mptkEvent);
                //        if (mptkEvent != null)
                //            return mptkEvent;
                //    }
                int lowIndex = 0;
                int highIndex = MPTK_MidiEvents.Count - 1;
                int found = -1;
                long lTime = (long)time;
                while (found < 0)
                {
                    int middleIndex = (lowIndex + highIndex) / 2; // bug corrected in v2.9.0 Was low + high / 2 // ;-)
                    long middleTime = (long)MPTK_MidiEvents[middleIndex].RealTime;
                    if (lTime < middleTime)
                        highIndex = middleIndex;
                    else if (lTime > middleTime)
                        lowIndex = middleIndex;
                    else
                        found = middleIndex;
                    if (lowIndex == highIndex || lowIndex + 1 == highIndex)
                        // Found exact time or index adjacent
                        found = lowIndex;
                }

                if (found >= 0)
                {
                    MPTKEvent mptkEvent = MPTK_MidiEvents[found];
                    if (mptkEvent != null)
                        return mptkEvent;
                }

            }
            return null;
        }

        /// <summary>@brief
        /// Search a tick position in the current midi from a position in millisecond.\n
        /// Warning: this method loop on the whole midi to find the position. \n
        /// Could be CPU costly but this method take care of the tempo change in the Midi.\n
        /// Use MPTK_ConvertTimeToTick if there is no tempo change in the midi. 
        /// </summary>
        /// <param name="time">position in milliseconds</param>
        /// <returns>position in ticks</returns>
        public long MPTK_SearchTickFromTime(double time)
        {
            if (time <= 0d)
                return 0L;
            else if (time >= MPTK_DurationMS)
                return MPTK_TickLast;
            else
            {
                foreach (MPTKEvent trackEvent in MPTK_MidiEvents)
                    if (trackEvent.RealTime >= time)
                        return trackEvent.Tick;
            }
            return 0;
        }

        /// <summary>
        /// @deprecated MidiLoad.BeatPerMinute2QuarterPerMicroSecond is deprecated and will be removed. Please investigate MPTKEvent.BeatPerMinute2QuarterPerMicroSecond.
        /// </summary>
        public static int MPTK_BPM2MPQN(int bpm)
        {
            Debug.LogWarning("MidiLoad.BeatPerMinute2QuarterPerMicroSecond is deprecated and will be removed. Please investigate MPTKEvent.BeatPerMinute2QuarterPerMicroSecond in place");
            return 60000000 / bpm;
        }

        /// <summary>
        /// @deprecated MidiLoad.QuarterPerMicroSecond2BeatPerMinute is deprecated and will be removed. Please investigate MPTKEvent.QuarterPerMicroSecond2BeatPerMinute.
        /// </summary>
        public static int MPTK_MPQN2BPM(int microsecondsPerQuaterNote)
        {
            Debug.LogWarning("MidiLoad.QuarterPerMicroSecond2BeatPerMinute is deprecated and will be removed. Please investigate MPTKEvent.QuarterPerMicroSecond2BeatPerMinute in place");
            return 60000000 / microsecondsPerQuaterNote;
        }

        /// <summary>@brief
        /// https://en.wikipedia.org/wiki/Note_value
        /// </summary>
        /// <param name="mptkEvent"></param>
        /// <returns></returns>
        public MPTKEvent.EnumLength NoteLength(MPTKEvent mptkEvent)
        {
            if (mptkEvent.Length >= MPTK_DeltaTicksPerQuarterNote * 4)
                return MPTKEvent.EnumLength.Whole;
            else if (mptkEvent.Length >= MPTK_DeltaTicksPerQuarterNote * 2)
                return MPTKEvent.EnumLength.Half;
            else if (mptkEvent.Length >= MPTK_DeltaTicksPerQuarterNote)
                return MPTKEvent.EnumLength.Quarter;
            else if (mptkEvent.Length >= MPTK_DeltaTicksPerQuarterNote / 2)
                return MPTKEvent.EnumLength.Eighth;
            return MPTKEvent.EnumLength.Sixteenth;
        }

        /// <summary>@brief
        /// Display in console the attributes of the MIDI loaded - v2.9.0
        /// </summary>
        /// <param name="tmEvents"></param>
        public void MPTK_DisplayMidiAttributes()
        {
            if (MPTK_MidiEvents == null)
                Debug.LogWarning($"No MIDI loaded");
            else
            {
                if (string.IsNullOrEmpty(SequenceTrackName)) Debug.Log($"SequenceTrackName: {SequenceTrackName}");
                if (string.IsNullOrEmpty(ProgramName)) Debug.Log($"SequenceTrackName: {ProgramName}");
                if (string.IsNullOrEmpty(TrackInstrumentName)) Debug.Log($"SequenceTrackName: {TrackInstrumentName}");
                if (string.IsNullOrEmpty(TextEvent)) Debug.Log($"SequenceTrackName: {TextEvent}");
                if (string.IsNullOrEmpty(Copyright)) Debug.Log($"SequenceTrackName: {Copyright}");

                Debug.Log($"MPTK_DeltaTicksPerQuarterNote:\t\t{MPTK_DeltaTicksPerQuarterNote} ticks");
                //Debug.Log($"MPTK_TicksInMetronomeClick:\t\t{MPTK_TicksInMetronomeClick}");
                //Debug.Log($"MPTK_No32ndNotesInQuarterNote:\t\t{MPTK_No32ndNotesInQuarterNote}");
                Debug.Log($"MPTK_MicrosecondsPerQuarterNote:\t\t{MPTK_MicrosecondsPerQuarterNote}");
                Debug.Log($"MPTK_Pulse:\t\t{MPTK_Pulse} millisecond");

                Debug.Log($"MPTK_TrackCount:\t\t{MPTK_TrackCount}");
                Debug.Log($"MPTK_MidiEvents:\t\t{MPTK_MidiEvents.Count} events");

                Debug.Log($"MPTK_InitialTempo:\t\t{MPTK_InitialTempo} bpm\t\tTempo change:\t\t{MPTK_TempoMap.Count}");
                Debug.Log($"MPTK_DurationMS:\t\t{MPTK_DurationMS / 1000f} seconds \tMPTK_Duration:\t\t{MPTK_Duration}");

                Debug.Log($"MPTK_TickFirstNote:\t\t{MPTK_TickFirstNote} ticks \t\tMPTK_PositionFirstNote:\t{MPTK_PositionFirstNote / 1000f:F2} second {TimeSpan.FromMilliseconds(MPTK_PositionFirstNote)} ");
                Debug.Log($"MPTK_TickLastNote:\t\t{MPTK_TickLastNote} ticks \t\tMPTK_PositionLastNote:\t{MPTK_PositionLastNote / 1000f:F2} second {TimeSpan.FromMilliseconds(MPTK_PositionLastNote)}");

                Debug.Log($"MPTK_TickLast:\t\t{MPTK_TickLast} ticks");
                Debug.Log($"MPTK_NumberBeatsMeasure:\t\t{MPTK_NumberBeatsMeasure}");
                Debug.Log($"MPTK_NumberQuarterBeat:\t\t{MPTK_NumberQuarterBeat}");
                Debug.Log($"MPTK_TimeSigNumerator:\t\t{MPTK_TimeSigNumerator}");
                Debug.Log($"MPTK_TimeSigDenominator:\t\t{MPTK_TimeSigDenominator}");
                Debug.Log($"MPTK_KeySigSharpsFlats:\t\t{MPTK_KeySigSharpsFlats}");
                Debug.Log($"MPTK_KeySigMajorMinor:\t\t{MPTK_KeySigMajorMinor}");

                Debug.Log($"MPTK_LoadTime:\t\t{MPTK_LoadTime} millisecond");
            }
        }

        /// <summary>@brief
        /// Calculate all timing in the MIDI file:
        ///     - Tempo Map
        ///     - Signature Map
        ///     - For each events:
        ///         Realtime, 
        ///         Measure, 
        ///         Beat
        /// </summary>
        /// @snippet TestMidiFilePlayerScripting.cs Example_GUI_PreloadAndAlterMIDI
        public void MPTK_CalculateTiming()
        {
            if (MPTK_MidiEvents == null || MPTK_MidiEvents.Count == 0)
                Debug.LogWarning("MPTK_CalculateTiming: no MIDI events found");
            else
            {
                timeStartLoad = DateTime.Now;
                calculateTiming();
            }
        }

        private void calculateTiming()
        {

            if (MPTK_MidiEvents == null || MPTK_MidiEvents.Count == 0)
            {
                Debug.LogWarning("No MIDI event found");
            }
            else
            {
                MPTK_TempoMap = new List<MPTKTempo>();
                MPTKTempo.CalculateMap(MPTK_DeltaTicksPerQuarterNote, MPTK_MidiEvents, MPTK_TempoMap);
                MPTK_TempoMap = MPTK_TempoMap.OrderBy(o => o.FromTick).ToList();
                MPTK_CurrentTempoMap = MPTK_TempoMap[0];
                MPTK_MicrosecondsPerQuarterNote = MPTK_CurrentTempoMap.MicrosecondsPerQuarterNote;
                MPTK_InitialTempo = MPTKEvent.QuarterPerMicroSecond2BeatPerMinute(MPTK_MicrosecondsPerQuarterNote);
                fluid_player_set_midi_tempo(MPTK_MicrosecondsPerQuarterNote);

                MPTK_SignMap = new List<MPTKSignature>();
                MPTKSignature.CalculateMap(MPTK_DeltaTicksPerQuarterNote, MPTK_MidiEvents, MPTK_SignMap);
                MPTKSignature.CalculateMeasureBoundaries(MPTK_SignMap);
                MPTK_CurrentSignMap = MPTK_SignMap[0];

                if (MPTK_LogLoadEvents)
                    MPTK_TempoMap.ForEach(tempo => Debug.Log($"Tempo change From:{tempo.FromTick} To:{tempo.ToTick} MPQN:{tempo.MicrosecondsPerQuarterNote,10} BPM:{MPTKEvent.QuarterPerMicroSecond2BeatPerMinute(tempo.MicrosecondsPerQuarterNote),3} Pulse:{tempo.Pulse,2:F2} Cumul:{tempo.FromTime,6:F2}"));

                MPTK_TickFirstNote = -1;
                MPTK_TickLastNote = -1;
                MPTK_MeasureLastNote = -1;

                // By default get the last which could be not a note-on
                MPTK_EventLastNote = MPTK_MidiEvents.Last();

                MPTK_TrackCount = 0;
                int indexTempo = 0;
                int indexSign = 0;
                int indexEvent = 0;

                // Calculate
                // realtime of the vent in ms,
                // duration in ms,
                // MPTK_TrackCount,
                // beat and measure for each event
                // MPTK_TickFirstNote, MPTK_PositionFirstNote (realtime in ms)
                MPTK_MidiEvents.ForEach(midievent =>
                {
                    midievent.Index = indexEvent++;

                    indexTempo = MPTKTempo.FindSegment(MPTK_TempoMap, midievent.Tick, indexTempo);
                    MPTKTempo tempoMap = MPTK_TempoMap[indexTempo];
                    double newTime = tempoMap.FromTime + (midievent.Tick - tempoMap.FromTick) * tempoMap.Pulse;
                    midievent.RealTime = (float)newTime;

                    indexSign = MPTKSignature.FindSegment(MPTK_SignMap, midievent.Tick, indexSign);
                    MPTKSignature signMap = MPTK_SignMap[indexSign];
                    midievent.Measure = signMap.TickToMeasure(midievent.Tick);
                    midievent.Beat = signMap.CalculateBeat(midievent.Tick, midievent.Measure);
                    //Debug.Log($"Tick:{midievent.Tick} {midievent.Command} indexTempo:{indexTempo} RealTime:{midievent.RealTime} Measure:{midievent.Measure}.{midievent.Beat}");

                    if (MPTK_TrackCount <= midievent.Track)
                        MPTK_TrackCount = (int)midievent.Track + 1;

                    if (midievent.Command == MPTKCommand.NoteOn)
                    {
                        midievent.Duration = Convert.ToInt64(midievent.Length * tempoMap.Pulse);
                        if (MPTK_TickFirstNote == -1)
                        {
                            MPTK_TickFirstNote = midievent.Tick;
                            MPTK_PositionFirstNote = midievent.RealTime;
                        }

                        MPTK_EventLastNote = midievent;
                    }
                });

                //// Numerator: counts the number of beats in a measure. 
                //// For example a numerator of 4 means that each bar contains four beats. 
                //MPTK_TimeSigNumerator = timesig.Numerator;
                //// Denominator: number of quarter notes in a beat.0=ronde, 1=blanche, 2=quarter, 3=eighth, etc. 
                //MPTK_TimeSigDenominator = timesig.Denominator;
                MPTK_NumberBeatsMeasure = MPTK_SignMap[0].NumberBeatsMeasure;
                MPTK_NumberQuarterBeat = MPTK_SignMap[0].NumberQuarterBeat;

                MPTK_ComputeDuration();

                MPTK_LoadTime = (float)(DateTime.Now - timeStartLoad).TotalMilliseconds;
            }
        }

        /// <summary>@brief
        /// Find the last MIDI event note-on.
        /// @version 2.12.1
        /// </summary>
        /// <returns>return the last MPTKEvent note-on found or null if not found</returns>
        public MPTKEvent MPTK_FindLastNote()
        {
            if (MPTK_MidiEvents != null)
            {
                foreach (MPTKEvent midievent in MPTK_MidiEvents)
                {
                    if (midievent.Command == MPTKCommand.NoteOn)
                        return midievent;
                }
            }
            return null;
        }

        /// <summary>@brief
        /// Calculate MPTK_Duration, MPTK_DurationMS, MPTK_TickLast from the MIDI events list MPTK_MidiEvents.
        /// @details
        /// This is done automatically when loading a MIDI file but not when MIDI events are added by script. In this case you will have to call this method.
        /// </summary>
        public void MPTK_ComputeDuration()
        {
            if (MPTK_MidiEvents != null && MPTK_MidiEvents.Count > 0)
            {
                MPTKEvent lastEvent = MPTK_MidiEvents[MPTK_MidiEvents.Count - 1];
                MPTK_TickLast = lastEvent.Tick;

                MPTK_DurationMS = lastEvent.RealTime;
                //Debug.Log($"MPTK_ComputeDuration - RealTime last event {lastEvent.Command} {lastEvent.RealTime}");

                if (MPTK_EventLastNote != null)
                {
                    //Debug.Log($"MPTK_ComputeDuration - MPTK_EventLastNote != null");
                    // We could have a note-on playing after the last event ... or not!
                    float endLastNote = MPTK_EventLastNote.RealTime + MPTK_EventLastNote.Duration; // v2.12.1 now take into account the duration of the last note-on event. thx Izanol.
                    if (endLastNote > MPTK_DurationMS)
                    {
                        //Debug.Log($"MPTK_ComputeDuration - endLastNote supersede last event endLastNote:{endLastNote} / {MPTK_DurationMS} ");
                        MPTK_DurationMS = endLastNote;
                    }
                    MPTK_TickLastNote = MPTK_EventLastNote.Tick;
                    MPTK_PositionLastNote = MPTK_EventLastNote.RealTime;
                    MPTK_MeasureLastNote = MPTK_EventLastNote.Measure;
                }
                else
                {
                    //Debug.Log($"MPTK_ComputeDuration - MPTK_EventLastNote == null");
                    MPTK_TickLastNote = lastEvent.Tick;
                    MPTK_PositionLastNote = lastEvent.RealTime;
                    MPTK_MeasureLastNote = lastEvent.Measure;
                }
                MPTK_Duration = TimeSpan.FromMilliseconds(MPTK_DurationMS);
                //Debug.Log($"MPTK_ComputeDuration - MPTK_Duration:{MPTK_Duration} MPTK_DurationMS:{MPTK_DurationMS}");
            }
            else
            {
                MPTK_TickLast = 0;
                MPTK_DurationMS = 0;
                MPTK_Duration = TimeSpan.FromMilliseconds(0);
            }
        }

        // No doc until end of file
        //! @cond NODOC

        /// <summary>@brief
        /// Build OS path to the midi file
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        static public string BuildOSPath(string filename)
        {
            try
            {
                string pathMidiFolder = Path.Combine(Application.dataPath, MidiPlayerGlobal.PathToMidiFile);
                string pathfilename = Path.Combine(pathMidiFolder, filename + MidiPlayerGlobal.ExtensionMidiFile);
                return pathfilename;
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return null;
        }



        // to avoid to pass as parameters ....
        int indexLoadingTrack;
        private List<MPTKEvent> ConvertNAudioEventsToMPTKEvents()
        {
            try
            {
                // Transform NAudio MIDI events to MPTKEvents
                // ------------------------------------------

                indexLoadingTrack = 0;
                List<MPTKEvent> mptkEvents = new List<MPTKEvent>();
                foreach (IList<MidiEvent> nAudioTrack in midifile.Events)
                {
                    foreach (MidiEvent nAudioMidievent in nAudioTrack)
                    {
                        // V2.89.0 able to remove End Track event
                        if (nAudioMidievent.CommandCode == MidiCommandCode.MetaEvent && ((MetaEvent)nAudioMidievent).MetaEventType == MetaEventType.EndTrack && !MPTK_KeepEndTrack)
                        {
                            //Debug.Log($"Dont keep EndTrack:{indexTracks}  CommandCode:{nAudioMidievent.CommandCode} indexTempo:{indexTempo} AbsoluteTime:{nAudioMidievent.AbsoluteTime} DeltaTime:{nAudioMidievent.DeltaTime} RealTime:{newTime / 1000f:F2} Pulse:{(float)tempoMap[indexTempo].MicrosecondsPerQuarterNote / (float)midifile.DeltaTicksPerQuarterNote / 1000f}");
                        }
                        else
                        {
                            MPTKEvent mptkEvent = ConvertNAudioEventToMPTKEvent(nAudioMidievent);
                            if (mptkEvent != null)
                                mptkEvents.Add(mptkEvent);
                        }
                    }
                    indexLoadingTrack++; // will be the count of tracks
                }

                if (mptkEvents.Count == 0)
                    throw new Exception("No midi event found");

                // MIDI events in a MIDI file are ordered by track then by ticks.
                // We want a list ordered by ticks only.

                //System.Diagnostics.Stopwatch watch = null;
                //watch = new System.Diagnostics.Stopwatch(); // High resolution time
                //watch.Start();

                // Quick sort (realloc new list) < v2.14.1
                //mptkEvents = mptkEvents.OrderBy(o => o.Tick).ToList();
                // Then sort with priority of preset change, meta end (pre-sort by ticks is mandatory)
                //Sort(mptkEvents, 0, mptkEvents.Count - 1, new MidiEventComparer());

                // v2.14.1 - Sort by ticks with priority by bank change, preset change, note ... meta end the lower.
                mptkEvents.Sort((x, y) => x.Compare(y));

                //Debug.Log($"Stable sort time {watch.ElapsedMilliseconds} {watch.ElapsedTicks}");
                //watch.Stop();

                return mptkEvents;

                //return mptkEvents.OrderBy(o => o.Tick).ToList(); 
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return null;
        }
        private MPTKEvent ConvertNAudioEventToMPTKEvent(MidiEvent midiEvent)
        {
            MPTKEvent mptkEvent = null;
            switch (midiEvent.CommandCode)
            {
                case MidiCommandCode.NoteOn:
                    //if (((NoteOnEvent)mptkEvent.Event).OffEvent != null)
                    {

                        NoteOnEvent noteon = (NoteOnEvent)midiEvent;
                        //Debug.Log(string.Format("Track:{0} NoteNumber:{1,3:000} AbsoluteTime:{2,6:000000} NoteLength:{3,6:000000} OffDeltaTime:{4,6:000000} ", track, noteon.NoteNumber, noteon.AbsoluteTime, noteon.NoteLength, noteon.OffEvent.DeltaTime));
                        if (noteon.OffEvent != null)
                        // V2.88   if (noteon.Velocity != 0)
                        {
                            mptkEvent = new MPTKEvent()
                            {
                                Track = indexLoadingTrack,
                                Tick = midiEvent.AbsoluteTime,
                                Command = MPTKCommand.NoteOn,
                                Value = noteon.NoteNumber,
                                Channel = midiEvent.Channel - 1,
                                Velocity = noteon.Velocity,
                                //DurationTicks = noteon.NoteLength, // added v 3.89.5 // removed in v2.9.0
                                //Duration = Convert.ToInt64(noteon.NoteLength * MPTK_Pulse),
                                Length = noteon.NoteLength,
                            };
                            if (MPTK_LogLoadEvents) Debug.Log(BuildInfoTrack(mptkEvent) + $"NoteOn {mptkEvent.Value:000} {noteon.NoteName,-4}\tDuration:{noteon.NoteLength,5} ticks {mptkEvent.Duration:000} ms {NoteLength(mptkEvent)}\tVelocity:{noteon.Velocity,3}");
                        }
                        else // It's a noteon which means a noteoff
                        {
                            // V2.88 if (KeepNoteOff)
                            if (MPTK_KeepNoteOff)// && noteon.OffEvent == null && noteon.NoteLength!=0)
                            {
                                mptkEvent = new MPTKEvent()
                                {
                                    Track = indexLoadingTrack,
                                    Tick = midiEvent.AbsoluteTime,
                                    Command = MPTKCommand.NoteOff, // set a noteoff
                                    Value = noteon.NoteNumber,
                                    Channel = midiEvent.Channel - 1,
                                    Velocity = noteon.Velocity,
                                    //Duration = Convert.ToInt64(noteon.NoteLength * MPTK_Pulse),
                                    //Length = noteon.NoteLength,
                                };

                                if (MPTK_LogLoadEvents) Debug.Log(BuildInfoTrack(mptkEvent) + $"NoteOff {mptkEvent.Value:000}\t{noteon.NoteName,-4}\tFrom NoteOn");
                            }
                        }
                    }
                    break;

                case MidiCommandCode.NoteOff:
                    if (MPTK_KeepNoteOff)
                    {
                        NoteEvent noteoff = (NoteEvent)midiEvent;
                        //Debug.Log(string.Format("Track:{0} NoteNumber:{1,3:000} AbsoluteTime:{2,6:000000} NoteLength:{3,6:000000} OffDeltaTime:{4,6:000000} ", track, noteon.NoteNumber, noteon.AbsoluteTime, noteon.NoteLength, noteon.OffEvent.DeltaTime));
                        mptkEvent = new MPTKEvent()
                        {
                            Track = indexLoadingTrack,
                            Tick = midiEvent.AbsoluteTime,
                            Command = MPTKCommand.NoteOff,
                            Value = noteoff.NoteNumber,
                            Channel = midiEvent.Channel - 1,
                            Velocity = noteoff.Velocity,
                            Duration = 0,
                            Length = 0,
                        };

                        if (MPTK_LogLoadEvents) Debug.Log(BuildInfoTrack(mptkEvent) + $"NoteOff {mptkEvent.Value:000}\t{noteoff.NoteName,-4}\tFrom file");
                    }
                    break;

                case MidiCommandCode.PitchWheelChange:
                    PitchWheelChangeEvent pitch = (PitchWheelChangeEvent)midiEvent;
                    mptkEvent = new MPTKEvent()
                    {
                        Track = indexLoadingTrack,
                        Tick = midiEvent.AbsoluteTime,
                        Command = MPTKCommand.PitchWheelChange,
                        Channel = midiEvent.Channel - 1,
                        Value = pitch.Pitch,  // Pitch Wheel Value 0 is minimum, 0x2000 (8192) is default, 0x3FFF (16383) is maximum
                    };
                    if (MPTK_LogLoadEvents) Debug.Log(BuildInfoTrack(mptkEvent) + string.Format("PitchWheelChange {0}", pitch.Pitch));
                    break;

                case MidiCommandCode.ControlChange:
                    ControlChangeEvent controlchange = (ControlChangeEvent)midiEvent;
                    mptkEvent = new MPTKEvent()
                    {
                        Track = indexLoadingTrack,
                        Tick = midiEvent.AbsoluteTime,
                        Command = MPTKCommand.ControlChange,
                        Channel = midiEvent.Channel - 1,
                        Controller = (MPTKController)controlchange.Controller,
                        Value = controlchange.ControllerValue,

                    };

                    //if ((MPTKController)controlchange.Controller != MPTKController.Sustain)

                    // Other midi event
                    if (MPTK_LogLoadEvents) Debug.Log(BuildInfoTrack(mptkEvent) + $"Control 0x{mptkEvent.Controller:X}/{mptkEvent.Controller} {mptkEvent.Value}");

                    break;

                case MidiCommandCode.PatchChange:
                    PatchChangeEvent change = (PatchChangeEvent)midiEvent;
                    mptkEvent = new MPTKEvent()
                    {
                        Track = indexLoadingTrack,
                        Tick = midiEvent.AbsoluteTime,
                        Command = MPTKCommand.PatchChange,
                        Channel = midiEvent.Channel - 1,
                        Value = change.Patch,
                    };
                    if (MPTK_LogLoadEvents) Debug.Log(BuildInfoTrack(mptkEvent) + string.Format("Preset   {0,3:000} {1}", change.Patch, PatchChangeEvent.GetPatchName(change.Patch)));
                    break;

                case MidiCommandCode.MetaEvent:
                    MetaEvent meta = (MetaEvent)midiEvent;
                    mptkEvent = new MPTKEvent()
                    {
                        Track = indexLoadingTrack,
                        Tick = midiEvent.AbsoluteTime,
                        Command = MPTKCommand.MetaEvent,
                        Channel = midiEvent.Channel - 1,
                        Meta = (MPTKMeta)meta.MetaEventType,
                    };

                    switch (meta.MetaEventType)
                    {
                        case MetaEventType.EndTrack:
                            mptkEvent.Info = "End Track";
                            break;

                        case MetaEventType.KeySignature:
                            KeySignatureEvent keysig = (KeySignatureEvent)meta;
                            // will be set with the first KeySignature found (in general there is no more one KeySignature in a MIDI)
                            // then with each KeySignature found when playing the MIDI.
                            if (MPTK_KeySigSharpsFlats == 0) MPTK_KeySigSharpsFlats = keysig.SharpsFlats;
                            if (MPTK_KeySigMajorMinor == 0) MPTK_KeySigMajorMinor = keysig.MajorMinor;
                            mptkEvent.Value = MPTKEvent.BuildIntFromBytes((byte)MPTK_KeySigSharpsFlats, (byte)MPTK_KeySigMajorMinor, 0, 0);
                            mptkEvent.Info = $"SharpsFlats:{MPTK_KeySigSharpsFlats} MajorMinor:{MPTK_KeySigMajorMinor}";
                            break;

                        case MetaEventType.TimeSignature:
                            TimeSignatureEvent timesig = (TimeSignatureEvent)meta;
                            // will be set with the first TimeSignature found (in general there is no more one TimeSignature in a MIDI)
                            // then with each TimeSignature found when playing the MIDI.
                            if (MPTK_TimeSigNumerator == 0) MPTK_TimeSigNumerator = timesig.Numerator;
                            if (MPTK_TimeSigDenominator == 0) MPTK_TimeSigDenominator = timesig.Denominator;
                            if (MPTK_TicksInMetronomeClick == 0) MPTK_TicksInMetronomeClick = timesig.TicksInMetronomeClick;
                            if (MPTK_No32ndNotesInQuarterNote == 0) MPTK_No32ndNotesInQuarterNote = timesig.No32ndNotesInQuarterNote;
                            mptkEvent.Value = MPTKEvent.BuildIntFromBytes((byte)timesig.Numerator, (byte)timesig.Denominator, (byte)timesig.TicksInMetronomeClick, (byte)timesig.No32ndNotesInQuarterNote);
                            mptkEvent.Info = $"Numerator:{timesig.Numerator} Denominator:{timesig.Denominator}";
                            break;

                        case MetaEventType.SetTempo:
                            // removed with v2.10.1 - tempo event are keep in the list but are not played
                            // if (MPTK_EnableChangeTempo)
                            {
                                TempoEvent tempoEvent = (TempoEvent)meta;
                                int microsecondsPerQuarterNote = ((TempoEvent)meta).MicrosecondsPerQuarterNote;
                                if (microsecondsPerQuarterNote <= 0/* || tempo <= 0*/)
                                {
                                    // tempo = 120;
                                    microsecondsPerQuarterNote = MPTKEvent.BeatPerMinute2QuarterPerMicroSecond((int)120);
                                }

                                // Tempo change will be done in MidiFilePlayer
                                // Version 2.10.0 Removed - mptkEvent.Duration = tempo;
                                mptkEvent.Value = microsecondsPerQuarterNote;
                                if (MPTK_LogLoadEvents) Debug.Log(BuildInfoTrack(mptkEvent) + string.Format("Meta     {0,-15} 'Tempo:{1:F2} MicrosecondsPerQuarterNote:{2}'", meta.MetaEventType, tempoEvent.Tempo, tempoEvent.MicrosecondsPerQuarterNote));
                            }
                            break;

                        case MetaEventType.SequenceTrackName:
                            mptkEvent.Info = ((TextEvent)meta).Text;
                            if (!string.IsNullOrEmpty(SequenceTrackName)) SequenceTrackName += "\n";
                            SequenceTrackName += mptkEvent.Info;
                            break;

                        case MetaEventType.ProgramName:
                            mptkEvent.Info = ((TextEvent)meta).Text;
                            ProgramName += mptkEvent.Info + " ";
                            break;

                        case MetaEventType.TrackInstrumentName:
                            mptkEvent.Info = ((TextEvent)meta).Text;
                            if (!string.IsNullOrEmpty(TrackInstrumentName)) TrackInstrumentName += "\n";
                            TrackInstrumentName += mptkEvent.Info;
                            break;

                        case MetaEventType.TextEvent:
                            mptkEvent.Info = ((TextEvent)meta).Text;
                            TextEvent += mptkEvent.Info + " ";
                            break;

                        case MetaEventType.Copyright:
                            mptkEvent.Info = ((TextEvent)meta).Text;
                            Copyright += mptkEvent.Info + " ";
                            break;

                        case MetaEventType.Lyric: // lyric
                            mptkEvent.Info = ((TextEvent)meta).Text;
                            TextEvent += mptkEvent.Info + " ";
                            break;

                        case MetaEventType.Marker: // marker
                            mptkEvent.Info = ((TextEvent)meta).Text;
                            TextEvent += mptkEvent.Info + " ";
                            break;

                        default:
                            //case MetaEventType.CuePoint: // cue point
                            //case MetaEventType.DeviceName:
                            if (MPTK_LogLoadEvents) Debug.LogWarning($"This Meta MIDI event is not processed by Maestro: {midiEvent}");
                            break;
                    }

                    if (MPTK_LogLoadEvents && mptkEvent != null && !string.IsNullOrEmpty(mptkEvent.Info)) Debug.Log(BuildInfoTrack(mptkEvent) + string.Format("Meta     {0,-15} '{1}'", mptkEvent.Meta, mptkEvent.Info));
                    break;

                default:
                    // Other midi event
                    if (MPTK_LogLoadEvents) Debug.LogWarning($"This MIDI event is not processed by Maestro: {midiEvent}");
                    break;
            }
            return mptkEvent;
        }

        /// <summary>@brief
        /// In-place and stable implementation of MergeSort
        /// </summary>
        public static void Sort<T>(IList<T> list, int lowIndex, int highIndex, IComparer<T> comparer)
        {
            if (lowIndex >= highIndex)
                return;

            int midIndex = (lowIndex + highIndex) / 2;

            // Partition the list into two lists and Sort them recursively
            Sort(list, lowIndex, midIndex, comparer);
            Sort(list, midIndex + 1, highIndex, comparer);

            // Merge the two sorted lists
            int endLow = midIndex;
            int startHigh = midIndex + 1;

            while ((lowIndex <= endLow) && (startHigh <= highIndex))
            {
                // MRH, if use < 0 sort is not stable
                if (comparer.Compare(list[lowIndex], list[startHigh]) <= 0)
                {
                    lowIndex++;
                }
                else
                {
                    // list[lowIndex] > list[startHigh]
                    // The next element comes from the second list, 
                    // move the list[start_hi] element into the next position and shuffle all the other elements up.
                    T t = list[startHigh];

                    for (int k = startHigh - 1; k >= lowIndex; k--)
                    {
                        list[k + 1] = list[k];
                    }

                    list[lowIndex] = t;
                    lowIndex++;
                    endLow++;
                    startHigh++;
                }
            }
        }

        // Utility class for comparing MidiEvent objects -  DEPRECATED from 2.14.1.
        // Comparer moved to MPTKEvent class.
        //public class MidiEventComparer : IComparer<MPTKEvent>
        //{
        //    public int Compare(MPTKEvent x, MPTKEvent y)
        //    {
        //        long xTime = x.Tick;
        //        long yTime = y.Tick;

        //        if (xTime == yTime)
        //        {
        //            if (x.Command != MPTKCommand.ControlChange && x.Command != MPTKCommand.PatchChange && x.Command != MPTKCommand.NoteOn &&
        //                y.Command != MPTKCommand.ControlChange && y.Command != MPTKCommand.PatchChange && y.Command != MPTKCommand.NoteOn)
        //                yTime = Int64.MinValue;

        //            // sort meta events before note events, except end track
        //            if (x.Command == MPTKCommand.MetaEvent)
        //            {
        //                if (x.Meta == MPTKMeta.EndTrack)
        //                    xTime = Int64.MaxValue;
        //                else
        //                    xTime = Int64.MinValue;
        //            }
        //            if (y.Command == MPTKCommand.MetaEvent)
        //            {
        //                if (y.Meta == MPTKMeta.EndTrack)
        //                    yTime = Int64.MaxValue;
        //                else
        //                    yTime = Int64.MinValue;
        //            }
        //            Debug.Log($"{x.Tick:0000} x:{x.Command} y:{y.Command} {(xTime < yTime ? "x first" : "y first")}");

        //        }
        //        return xTime.CompareTo(yTime);
        //    }
        //}

        public void ClearMetaText()
        {
            SequenceTrackName = "";
            ProgramName = "";
            TrackInstrumentName = "";
            TextEvent = "";
            Copyright = "";
        }

        /// <summary>@brief
        /// Change speed to play. 1=normal speed
        /// </summary>
        /// <param name="speed"></param>
        public void ChangeSpeed(float speed)
        {
            try
            {
                if (speed > 0)
                {
                    //Debug.Log($"ChangeSpeed from {this.speed:F3} to {speed:F3}");
                    //double lastSpeed = speed;
                    this.speed = speed;
                    fluid_player_set_midi_tempo(miditempo);
                    // V2.88 : duration is no longer linked to speed
                    //MPTK_DurationMS = (float)((double)MPTK_DurationMS * lastSpeed / speed);
                    //MPTK_Duration = TimeSpan.FromMilliseconds(MPTK_DurationMS);
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        // Level of quantization : 
        //     -      0 = None 
        //     -      1 = Beat Note
        //     -      2 = Eighth Note
        //     -      3 = 16th Note
        //     -      4 = 32th Note
        //     -      5 = 64th Note
        //     -      6 = 128th Note
        public void ChangeQuantization(int level = 4)
        {
            try
            {
                if (level <= 0)
                {
                    //Debug.Log($"ChangeQuantization to 0");
                    Quantization = 0;
                }
                else
                {
                    // Quantization = MPTK_DeltaTicksPerQuarterNote / level;
                    // v2.16.0 Thanks to cihadturhan_unity
                    //Debug.Log($"ChangeQuantization to {MPTK_DeltaTicksPerQuarterNote / (1 << (level - 1)):F2} for level {level} - previous was: {Quantization:F2} - DeltaTicksPerQuarterNote:{MPTK_DeltaTicksPerQuarterNote}");
                    Quantization = MPTK_DeltaTicksPerQuarterNote / (1 << (level-1));
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        public void ResetMidiPlayer()
        {
            // Debug.Log("StartMidi core");
            //begin_msec = 0;
            start_msec = 0;
            TickFromTempoChange = 0;
            MPTK_TickPlayer = 0;
            TickSeek = -1;
            cur_msec = 0;
            next_event = 0;
            EndMidiEvent = false;
            MPTK_TickCurrent = 0;
            MPTK_LastEventPlayed = null;
        }
        public bool calculateTickPlayer(int msec)
        {
            bool beatChange = false;
            try
            {
                // Calculate tick player. All MIDI with tick bellow will be read
                cur_msec = msec;
                MPTK_TickPlayer = TickFromTempoChange + (long)(((double)(cur_msec - start_msec) / MPTK_Pulse) + 0.5d);

                // pre-test to avoid calling find tempo map at each cycle
                if (MPTK_TickPlayer < MPTK_CurrentTempoMap.FromTick ||
                    MPTK_TickPlayer > MPTK_CurrentTempoMap.ToTick)
                {
                    // Normally, a new index will be found
                    MPTK_CurrentIndexTempoMap = MPTKTempo.FindSegment(MPTK_TempoMap, MPTK_TickPlayer, MPTK_CurrentIndexTempoMap);
                    MPTK_CurrentTempoMap = MPTK_TempoMap[MPTK_CurrentIndexTempoMap];
                }

                // pre-test to avoid calling find tempo map at each cycle
                if (MPTK_TickPlayer < MPTK_CurrentSignMap.FromTick ||
                    MPTK_TickPlayer > MPTK_CurrentSignMap.ToTick)
                {
                    // Normally, a new index will be found
                    MPTK_CurrentIndexSignMap = MPTKSignature.FindSegment(MPTK_SignMap, MPTK_TickPlayer, MPTK_CurrentIndexSignMap);
                    MPTK_CurrentSignMap = MPTK_SignMap[MPTK_CurrentIndexSignMap];
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return beatChange;
        }
        /**
         * Set the tempo of a MIDI player.
         * MPQN: microsecondsPerQuaterNote = 60000000 / bpm
         * @param tempo Tempo to set playback speed from param1
         *
         */
        private void fluid_player_set_midi_tempo(int MPQN)
        {
            if (MPTK_DeltaTicksPerQuarterNote <= 0)
                Debug.LogWarning("DeltaTicksPerQuarterNote is not set, tempo can't be defined");
            else if (MPQN <= 0)
                Debug.LogWarning("MicrosecondsPerQuaterNote is not correct. Probably a weird tempo event has been found");
            else
            {
                miditempo = MPQN;
                MPTK_Pulse = (double)MPQN / MPTK_DeltaTicksPerQuarterNote / 1000f / speed; /* in milliseconds */
                //Debug.Log($"Apres tempo change tempo:{MPQN} BPM:{MPTKEvent.QuarterPerMicroSecond2BeatPerMinute(MPQN)} pulse:{MPTK_Pulse} start:{start_msec} ms start:{TickFromTempoChange} ticks DeltaTicksPerQuarterNote:{MPTK_DeltaTicksPerQuarterNote}");
                start_msec = cur_msec;
                TickFromTempoChange = MPTK_TickPlayer;
            }
        }

        /**
        * Seek in the currently playing file.
        * @param player MIDI player instance
        * @param ticks the position to seek to in the current file
        * @return #FLUID_FAILED if ticks is negative or after the latest tick of the file,
        *   #FLUID_OK otherwise
        * @since 2.0.0
        *
        * The actual seek is performed during the player_callback.
        */
        public void fluid_player_seek(long ticks)
        {
            // Include tick un parameter
            if (ticks > 0)
                TickSeek = ticks - 1;
            else
                TickSeek = 0;
            //Debug.Log($"fluid_player_seek: ticks:{ticks} TickSeek:{TickSeek}");
        }


        /**
      * Set the tempo of a MIDI player in beats per minute.
      * @param player MIDI player instance
      * @param bpm Tempo in beats per minute
      * @return Always returns #FLUID_OK
      */
        private void fluid_player_set_bpm(int bpm)
        {
            fluid_player_set_midi_tempo(60000000 / bpm);
        }

        /**
         * Get the tempo of a MIDI player in beats per minute.
         * @param player MIDI player instance
         * @return MIDI player tempo in BPM
         * @since 1.1.7
         */

        private int fluid_player_get_bpm()
        {
            // return 60000000 / miditempo;
            // v2.16.0 it’s floating point is something like 139.9xxx which is rounded to 139BPM.I changed my code as the following to fix this.
            return Mathf.RoundToInt(60000000f / miditempo);
        }

        // not possible.  QueueMidiEvents.Enqueue will send list of events by reference ...
        // and this list can be changed in this thread. We need to allocate a new list when new events are available.
        //public List<MPTKEvent> internalMidiEventsProcessed = new List<MPTKEvent>(10);

        // see previous version for comment and debugide
        public List<MPTKEvent> fluid_player_callback(int msec, int idSession)
        {
            List<MPTKEvent> midiEvents = null;

            //Debug.Log($"-------------- {msec} --- {next_event} --- TickPlayer:{MPTK_TickPlayer} --- TickCurrent:{MPTK_TickCurrent} --- TickSeek:{TickSeek} ---------------");

            try
            {
                if (next_event >= 0 && MPTK_MidiEvents != null /*v2.9.0*/)
                {
                    if (MPTK_TickEnd > 0 && MPTK_TickPlayer >= MPTK_TickEnd)
                    {
                        // Reach the tick end ? not confuse with the InnerLoop feature
                        //Debug.Log($"MPTK_TickEnd reached MPTK_TickEnd:{MPTK_TickEnd} MPTK_TickPlayer:{MPTK_TickPlayer} MPTK_TickCurrent:{MPTK_TickCurrent}");
                        EndMidiEvent = true;
                    }
                    else if (endPlayAtLastNote && MPTK_TickCurrent >= MPTK_EventLastNote.Tick)
                    {
                        // The last note played (MPTK_TickCurrent) was on the LastNote tick
                        //Debug.Log($" End Note on MPTK_LastEventPlayed.Tick:{MPTK_LastEventPlayed.Tick} MPTK_TickPlayer:{MPTK_TickPlayer} MPTK_TickCurrent:{MPTK_TickCurrent}");
                        EndMidiEvent = true;
                        return null;
                    }
                    else
                    {
                        long ticks;

                        // Need to seek?
                        if (TickSeek < 0)
                            // No, start search MIDI events from current player position
                            ticks = MPTK_TickPlayer;
                        else
                        {
                            // Yes, start search MIDI events from seek position
                            ticks = TickSeek;
                            if (ticks <= MPTK_TickCurrent)
                                // Search events from the start of the MIDI but not note-on will be excluded (apply tempo, controller, preset change ... from start)
                                next_event = 0;
                            //Debug.Log($"Start seek - next_event:{next_event} TickFromTempoChange:{TickFromTempoChange} cur_msec:{cur_msec} start_msec:{start_msec}");
                        }

                        //Debug.Log($"Search for event from next_event:{next_event} ticks:{ticks} MPTK_TickPlayer:{MPTK_TickPlayer} TickSeek:{TickSeek} TickFromTempoChange:{TickFromTempoChange} cur_msec:{cur_msec} start_msec:{start_msec}");

                        // Search all events from 
                        while (true)
                        {
                            if (next_event >= MPTK_MidiEvents.Count)
                            {
                                // No MIDI event remains in the list ... end playing.
                                //Debug.Log($"No MIDI event remains in the list. next_event:{next_event}");
                                next_event = -1;
                                break;
                            }

                            MPTKEvent mptkEvent = MPTK_MidiEvents[next_event];
                            // 2.9.0 remove stupid shift half quanta

                            long quantizedTick = Quantization != 0 ? (mptkEvent.Tick / Quantization) * Quantization : mptkEvent.Tick;
                            //Debug.Log($"MIDI events search. {Quantization} ticks:{mptkEvent.Tick} --> quantizedTick:{quantizedTick}");

                            if (quantizedTick >= ticks) // V2.872 replaced > by >=    
                            {
                                // MIDI events search is over
                                //Debug.Log($"   Search over, break the loop. {((midiEvents == null) ? "No event.": midiEvents.Count.ToString()+" events.")} Player ticks:{ticks}  Next tick was:{quantizedTick} TickSeek:{TickSeek}");
                                break;
                            }
                            //Debug.Log($"MIDI events search. Quantization:{Quantization} TicksPerQuarterNote:{MPTK_DeltaTicksPerQuarterNote} ticks:{mptkEvent.Tick} --> quantizedTick:{quantizedTick}");

                            // If not seeking get next events
                            // If seeking exclude note-on and all META which are not SetTempo (MetaEvent.SetTempo, Preset, Control ... are kept)
                            // to replace the MIDI synth in the same context.
                            if (TickSeek < 0
                                || (!MPTK_RawSeek && (mptkEvent.Command != MPTKCommand.NoteOn /*&& mptkEvent.Command != MPTKCommand.NoteOff*/ &&
                                  !(mptkEvent.Command == MPTKCommand.MetaEvent && mptkEvent.Meta != MPTKMeta.SetTempo))))
                            {
                                //Debug.Log($"   Ticks:{ticks} add event to list  mptkEvent:{mptkEvent}");
                                if (midiEvents == null) midiEvents = new List<MPTKEvent>();

                                if (mptkEvent.Command == MPTKCommand.MetaEvent)
                                {
                                    if (mptkEvent.Meta == MPTKMeta.SetTempo)
                                    {
                                        if (MPTK_EnableChangeTempo)
                                        {
                                            MPTK_MicrosecondsPerQuarterNote = mptkEvent.Value;
                                            // Update MPTK_Pulse
                                            fluid_player_set_midi_tempo(MPTK_MicrosecondsPerQuarterNote);
                                        }
                                    }
                                    else if (mptkEvent.Meta == MPTKMeta.TimeSignature)
                                    {
                                        MPTK_NumberBeatsMeasure = MPTKEvent.ExtractFromInt((uint)mptkEvent.Value, 0);
                                        MPTK_NumberQuarterBeat = MPTKEvent.ExtractFromInt((uint)mptkEvent.Value, 1);
                                        MPTK_TicksInMetronomeClick = MPTKEvent.ExtractFromInt((uint)mptkEvent.Value, 2);
                                        MPTK_No32ndNotesInQuarterNote = MPTKEvent.ExtractFromInt((uint)mptkEvent.Value, 3);
                                    }
                                    else if (mptkEvent.Meta == MPTKMeta.KeySignature)
                                    {
                                        MPTK_KeySigSharpsFlats = MPTKEvent.ExtractFromInt((uint)mptkEvent.Value, 0);
                                        MPTK_KeySigMajorMinor = MPTKEvent.ExtractFromInt((uint)mptkEvent.Value, 1);
                                    }
                                }
                                mptkEvent.IdSession = idSession;
                                midiEvents.Add(mptkEvent);
                            }
                            next_event++;
                            //Debug.Log($"next_event:{next_event} TickSeek:{TickSeek} quantizedTick:{quantizedTick}  MPTK_TickCurrent:{MPTK_TickCurrent}  TickCurrent:{MPTK_TickPlayer}");
                        }

                        if (TickSeek >= 0)
                        {
                            // Seek sequence over
                            TickFromTempoChange = TickSeek;
                            MPTK_TickPlayer = TickSeek;
                            start_msec = msec;
                            //Debug.Log($"Reset TickSeek start_msec:{start_msec} MPTK_TickPlayer:{MPTK_TickPlayer}  midiEvents.Count:{(midiEvents != null ? midiEvents.Count : "null")}");
                            TickSeek = -1;
                        }

                        if (next_event < 0)
                        {
                            EndMidiEvent = true;
                            //Debug.Log($"EndMidiEvent set to true");
                        }

                        if (midiEvents != null && midiEvents.Count > 0)
                        {
                            MPTK_LastEventPlayed = midiEvents.Last();
                            //Debug.Log($"{MPTK_LastEventPlayed.Command} {MPTK_LastEventPlayed.RealTime} {MPTK_LastEventPlayed.Tick} MPTK_TickPlayer:{MPTK_TickPlayer} MPTK_TickCurrent:{MPTK_TickCurrent}");
                            MPTK_TickCurrent = MPTK_LastEventPlayed.Tick;// mptkEvent.Event.AbsoluteTime;
                        }

                        //Debug.Log($"Loop Exit next_event:{next_event} TickSeek:{TickSeek}  MPTK_TickCurrent:{MPTK_TickCurrent}  MPTK_TickPlayer:{MPTK_TickPlayer}");
                        //if (midiEvents != null && midiEvents.Count > 0)
                        //    midiEvents.ForEach(midiEvent => Debug.Log($"   {midiEvent.ToString()}"));
                        //else
                        //    Debug.Log($"   no event found");
                    }
                }

            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return midiEvents;
        }


        //private static string BuildNoteName(MPTKEvent midievent)
        //{
        //    return (midievent.Channel != 9) ?
        //        $"{HelperNoteLabel.LabelFromMidi(midievent.Value)}":
        //        "Drum";

        //    //String.Format("{0}{1}", NoteNames[midievent.Value % 12], midievent.Value / 12) :
        //}


        // https://www.recordingblogs.com/wiki/midi-key-signature-meta-message
        //private void AnalyzeKeySignature(MetaEvent meta, MPTKEvent mptkEvent = null)
        //{
        //    KeySignatureEvent keysig = (KeySignatureEvent)meta;
        //    MPTK_KeySigSharpsFlats = keysig.SharpsFlats;
        //    MPTK_KeySigMajorMinor = keysig.MajorMinor;

        //    if (mptkEvent != null)
        //    {
        //        mptkEvent.Value = MPTKEvent.BuildIntFromBytes(MPTK_KeySigSharpsFlats, MPTK_KeySigMajorMinor); ;
        //        mptkEvent.Info = $"SharpsFlats:{MPTK_KeySigSharpsFlats} MajorMinor:{MPTK_KeySigMajorMinor}";
        //    }
        //}

        //private void AnalyzeTimeSignature(MetaEvent meta, MPTKEvent mptkEvent = null)
        //{
        //    TimeSignatureEvent timesig = (TimeSignatureEvent)meta;

        //    // Numerator: counts the number of beats in a measure. 
        //    // For example a numerator of 4 means that each bar contains four beats. 
        //    MPTK_TimeSigNumerator = timesig.Numerator;
        //    // Denominator: number of quarter notes in a beat.0=ronde, 1=blanche, 2=quarter, 3=eighth, etc. 
        //    MPTK_TimeSigDenominator = timesig.Denominator;
        //    MPTK_NumberBeatsMeasure = timesig.Numerator;
        //    MPTK_NumberQuarterBeat = System.Convert.ToInt32(Mathf.Pow(2f, timesig.Denominator));
        //    MPTK_TicksInMetronomeClick = timesig.TicksInMetronomeClick;
        //    MPTK_No32ndNotesInQuarterNote = timesig.No32ndNotesInQuarterNote;

        //    if (mptkEvent != null)
        //    {
        //        mptkEvent.Value = MPTKEvent.BuildIntFromBytes(timesig.Numerator, timesig.Denominator);
        //        mptkEvent.Info = $"Numerator:{timesig.Numerator} Denominator:{timesig.Denominator}";
        //    }
        //}

        private string BuildInfoTrack(MPTKEvent e)
        {
            return $"[I:{e.Index:00000} A:{e.Tick:00000} R:{e.RealTime / 1000f:F2}] [Track:{e.Track} Channel:{e.Channel,00}] ";
        }

        public void DebugTrack()
        {
            int itrck = 0;
            foreach (IList<MidiEvent> track in midifile.Events)
            {
                itrck++;
                foreach (MidiEvent midievent in track)
                {
                    string info = string.Format("Track:{0} Channel:{1,2:00} Command:{2} AbsoluteTime:{3:0000000} ", itrck, midievent.Channel, midievent.CommandCode, midievent.AbsoluteTime);
                    if (midievent.CommandCode == MidiCommandCode.NoteOn)
                    {
                        NoteOnEvent noteon = (NoteOnEvent)midievent;
                        if (noteon.OffEvent == null)
                            info += string.Format(" OffEvent null");
                        else
                            info += string.Format(" OffEvent.DeltaTimeChannel:{0:0000.00} ", noteon.OffEvent.DeltaTime);
                    }
                    Debug.Log(info);
                }
            }
        }

        //! @endcond
    }
}

