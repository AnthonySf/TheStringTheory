//#define MPTK_PRO
#define DEBUG_START_MIDIx
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidiPlayerTK
{

    /// <summary>
    /// Plays a MIDI file from the MidiDB. This class must be used with the prefab MidiFilePlayer.\n
    /// See "Midi File Setup" in the Unity menu MPTK for adding MIDI in MidiDB.\n
    /// @attention 
    ///     - MidiFilePlayer inherits from class MidiSynth. For clarity, only MidiFilePlayer attributes are provided here.\n 
    ///       Look at the class MidiSynth to discover all attributes available.
    ///     - #MPTK_MidiLoaded is an important attribute for MidiFilePlayer class.\n 
    ///       It contains an instance of the class MidiLoad with all detailed information about the MIDI loaded and/or which are playing. \n
    ///       Check that #MPTK_MidiLoaded is not null before using these attributes.
    /// 
    /// There is no need to writing a script. For a simple usage, all the job can be done in the prefab inspector.\n
    /// For more information see here https://paxstellar.fr/midi-file-player-detailed-view-2/\n
    /// But for specific interactions, this class can be useful. Some use cases:\n
    ///     - changing the current MIDI playing: #MPTK_MidiName or #MPTK_MidiIndex
    ///     - changing the speed of the MIDI: #MPTK_Speed   
    ///     - know the duration: #MPTK_Duration
    ///     - know the current real time: #MPTK_RealTime
    ///     - apply filter, reverb, chorus effects in relation with the gameplay
    ///     - triggering action when MIDI start: #OnEventStartPlayMidi
    ///     - triggering action when MIDI end: #OnEventEndPlayMidi
    ///     - triggering action according MIDI events: #OnEventNotesMidi
    ///     - triggering action for each synth frame: #OnAudioFrameStart
    ///     - change on the fly current MIDI event: #OnMidiEvent (pro)
    ///     - force a preset on a channel: #MPTK_ChannelForcedPresetSet from the inherited MidiSynth class.
    ///     - get the current preset name for a channel: #MPTK_ChannelPresetGetName from the inherited MidiSynth class.
    ///     - mute a channel: #MPTK_ChannelEnableSet from the inherited MidiSynth class.
    ///     - ...
    /// 
    /// @code
    /// 
    /// // This example randomly select a MIDI to play.
    /// using MidiPlayerTK; // Add a reference to the MPTK namespace at the top of your script
    /// using UnityEngine;        
    ///  
    /// public class YourClass : MonoBehaviour
    /// {
    ///     // See TestMidiFilePlayerScripting.cs for a more detailed usage of this class.
    ///     public void RandomPlay()
    ///     {
    ///         // Need a reference to the prefab MidiFilePlayer that you have added in your scene hierarchy.
    ///         MidiFilePlayer midiFilePlayer = FindFirstObjectByType<MidiFilePlayer>();
    ///
    ///         // Random select for the Midi
    ///         midiFilePlayer.MPTK_MidiIndex = UnityEngine.Random.Range(0, MidiPlayerGlobal.MPTK_ListMidi.Count);
    /// 
    ///         // Play! How to make more simple?
    ///         midiFilePlayer.MPTK_Play();
    ///     }
    /// }
    /// 
    /// @endcode
    /// </summary>
    /// @ingroup midifileplayer_management
    //[RequireComponent(typeof(MidiPlayerGlobal))] -- not possible. It's a singleton object, all gameobjects will be merge at run time
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(AudioReverbFilter))]
    [RequireComponent(typeof(AudioChorusFilter))]
    [HelpURL("https://paxstellar.fr/midi-file-player-detailed-view-2/")]
    public partial class MidiFilePlayer : MidiSynth
    {
        /// @name MIDI Channels
        /// @{

        /// <summary>@brief
        /// Description and list of MIDI Channels associated to the MIDI synth.\n
        /// Each MIDI synth has 16 channels that carry all the relevant MIDI information.
        ///     - Current instrument / bank
        ///     - Volume
        ///     - Mute / Unmute (see Enable)
        ///     - Pitch bend ...
        ///     
        /// They serve to distinguish between instruments and provide independent control over each one. \n
        /// By transmitting MIDI messages on their respective channels, you can alter the instrument, volume, pitch, and other parameters. \n
        /// Within the Maestro Midi Player Toolkit, MIDI channels are designated numerically from 0 to 15. Notably, channel 9 is set aside specifically for drum sounds.
        /// 
        /// @snippet TestMidiFilePlayerScripting.cs ExampleUsingChannelAPI_Full
        /// </summary>
        /// @ingroup midifileplayer_channels
        public MPTKChannels MPTK_Channels
        {
            get { return Channels; }
        }
        /// @}

        #region Play and Stop MIDI
        /// @name Play and Stop MIDI
        /// @{

        /// <summary>@brief 
        /// Select a MIDI from the MIDIDB to play by its name.\n
        /// Use the exact name as seen in the MIDI setup windows (Unity menu MPTK/ without any path or extension.\n
        /// Tips: Add MIDI files to your project with the Unity menu MPTK.
        /// @code
        /// // Play the MIDI "Albinoni - Adagio"
        /// midiFilePlayer.MPTK_MidiName = "Albinoni - Adagio";
        /// midiFilePlayer.MPTK_Play();
        /// @endcode
        /// </summary>
        /// @ingroup midifileplayer_selection
        virtual public string MPTK_MidiName
        {
            get
            {
                return midiNameToPlay;
            }
            set
            {
                midiIndexToPlay = MidiPlayerGlobal.MPTK_FindMidi(value);
                midiNameToPlay = value;
            }
        }
        [SerializeField]
        [HideInInspector]
        protected string midiNameToPlay;

        /// <summary>@brief 
        /// Select a MIDI file to play by its Index from the MIDIDB.\n
        /// The Index of a MIDI file is displayed in the popup from the MidiFilePlayer inspector and in the window "Midi File Setup" from the MPTK menu in the editor.\n
        /// @code
        /// // Play the MIDI index 33
        /// midiFilePlayer.MPTK_MidiIndex = 33;
        /// midiFilePlayer.MPTK_Play();
        /// @endcode        
        /// </summary>
        /// <param name="index">Index of the MIDI, start from 0</param>
        /// @ingroup midifileplayer_selection
        public int MPTK_MidiIndex
        {
            get
            {
                return midiIndexToPlay;
            }
            set
            {
                /// @code
                /// midiFilePlayer.MPTK_MidiIndex = 1;
                /// @endcode
                try
                {
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

        [Header("Attributes below applies on MIDI Player")]
        /// <summary>@brief 
        /// If the value is true, MIDI playing will begin at the first note found in the MIDI. 
        /// @details
        /// Obviously, all previous events are processed, but at the same tick as the first note-on.\n
        /// Often, the first note is not set at the beginning of the MIDI file (which is tick 0), alSo there is a delay before playing the first note.\n
        /// This setting is useful to start playing sound immediately. Works also when looping.\n
        /// @note
        ///     - Does not affect the value of #MPTK_Duration that remains the same. 
        ///     - If enabled, there will be a difference between the real time of the MIDI and the theoretical duration.
        /// </summary>
        //[HideInInspector]
        /// @ingroup midifileplayer_playback_controls
        public bool MPTK_StartPlayAtFirstNote;

        /// <summary>@brief 
        /// By default, the end of a MIDI file is not the last note. It is the last MIDI event.\n
        /// If this value is true, MIDI playback will stop at the last note found in the MIDI file \n
        /// and the OnEventEndPlay will be triggered at the last note.
        /// @note
        ///     - Does not affect the value of #MPTK_Duration, which remains the same.
        ///     - If enabled, MIDI playback could stop before #MPTK_Duration. See below how get this real duration.
        ///     - Get #MPTK_PositionLastNote for the duration at the last note.
        /// @code
        ///   // Build string with MIDI duration. 
        ///   TimeSpan tsDuration;
        ///   if (midiFilePlayer.MPTK_StopPlayOnLastNote)
        ///      tsDuration = TimeSpan.FromMilliseconds(midiFilePlayer.midiLoaded.MPTK_PositionLastNote);
        ///   else
        ///      tsDuration = midiFilePlayer.MPTK_Duration;
        ///   string sRealDuration = $"{tsDuration.Hours:00}:{tsDuration.Minutes:00}:{tsDuration.Seconds:00}:{tsDuration.Milliseconds:000}";
        /// @endcode
        /// </summary>
        //[HideInInspector]
        /// @ingroup midifileplayer_playback_controls
        public bool MPTK_StopPlayOnLastNote;

        /// <summary>
        /// Defined the behavior of the MIDI player when playback is stopped with #MPTK_Stop or restarted when the last MIDI events is reached and #MPTK_MidiAutoRestart is set to true.
        /// See also #MPTK_ModeStopVoice
        /// @version 2.9.1
        /// @note 
        ///     - The time at which #OnEventEndPlayMidi is triggered is linked to this parameter.
        ///     - No impact on #MPTK_Duration, remains the same including the duration of the last note.
        /// </summary>
        public enum ModeStopPlay
        {
            /// <summary>
            /// Stops immediately (a short delay could occur, depending on the Unity FPS).
            /// </summary>
            StopNoWaiting,

            /// <summary>
            /// Stops when all notes enter the release phase (when a note-off occurs or the duration is reached).
            /// </summary>
            StopWhenAllVoicesReleased,

            /// <summary>
            /// Stops when all notes are in the ended phase (after the release phase). This delay is dependent of the instrument.
            /// A drum has a short release phase, a piano a medium delay, a tubular bells a very long delay. 
            /// </summary>
            StopWhenAllVoicesEnded,
        }
        static public string[] ModeStopPlayLabel = { "Stop No Waiting", "Stop When All Voices Are Released", "Stop When All Voices Are Ended" };

        /// <summary>@brief 
        /// Defined the behavior of the MIDI player when playback is stopped with #MPTK_Stop or restarted when the last MIDI events is reached and #MPTK_MidiAutoRestart is set to true.\n
        /// Code example:
        /// @code
        /// midiFilePlayer.MPTK_ModeStopVoice = MidiFilePlayer.ModeStopPlay.StopWhenAllVoicesReleased;
        /// @endcode
        /// @version 2.9.1
        /// @note 
        ///     - The time at which #OnEventEndPlayMidi is triggered is linked to this parameter.
        ///     - No impact on #MPTK_Duration, remains the same including the duration of the last note.
        ///     
        /// </summary>
        //[HideInInspector]
        /// @ingroup midifileplayer_playback_controls
        public ModeStopPlay MPTK_ModeStopVoice;


        [SerializeField]
        [HideInInspector]
        private int midiIndexToPlay;

        /// <summary>@brief 
        /// Whether the MIDI playback starts when the application starts?
        /// </summary>
        //[HideInInspector]
        /// @ingroup midifileplayer_playback_controls
        public bool MPTK_PlayOnStart { get { return playOnStart; } set { playOnStart = value; } }

        /// <summary>@brief 
        /// When the value is true, the current MIDI playing is restarted when it reaches the end of the MIDI file or #MPTK_MidiLoaded.MPTK_TickEnd.\n
        /// @note
        ///     - The MIDI file is not reloaded, the restart is quite immediate.
        ///     - The restart is processed by the main Unity thread, also one Unity frame or more is needed to restart the playing of the MIDI.
        ///     - #MPTK_MidiLoaded.MPTK_TickStart and #MPTK_MidiLoaded.MPTK_TickEnd are useful to defined start and end playing position.
        ///     - Better looping accuracy can be done with #MPTK_InnerLoop (pro).
        ///     - #MPTK_ModeStopVoice must be consider to define more precisely when the MIDI will be restarted.
        /// @snippet MidiLoop.c ExampleMidiLoop
        /// </summary>
        [HideInInspector]
        /// @ingroup midifileplayer_playback_controls
        public bool MPTK_MidiAutoRestart { get { return midiAutoRestart; } set { midiAutoRestart = value; } }

        [SerializeField]
        [HideInInspector]
        private float speed = 1f;

        /// <summary>@brief 
        /// Is MIDI file playing is paused ?
        /// </summary>
        /// @ingroup midifileplayer_playback_controls
        public bool MPTK_IsPaused { get { return playPause; } }

        /// <summary>@brief 
        /// Is MIDI file is playing ?
        /// </summary>
        /// @ingroup midifileplayer_playback_controls
        public bool MPTK_IsPlaying { get { return midiIsPlaying; } }


        /// <summary>@brief
        /// Plays the midi file defined with #MPTK_MidiName or #MPTK_MidiIndex.
        /// In the most part of the case, just call midiFilePlayer.MPTK_Play() in your script.
        /// But sometimes, you want to apply some changes on the MIDI file before playing it.
        /// The script example below describes how to load a MIDI file, apply some changes and play it.
        /// Thank to the parameter 'alreadyLoaded'. When true, the MIDI has been already loaded with #MPTK_Load()
        /// @note
        ///     - No effect when already playing, resume pause if playing. 
        ///     - corrected v2.17.1 - When MIDI player is paused and not yet playing, 
        ///       the player just resume pause and not playing ... now, resume pause and play!
        /// </summary>
        /// @snippet LoadMidiAndPlay.cs LoadMidiAndPlay
        /// <param name="alreadyLoaded">true: the MIDI has already been loaded (see #MPTK_Load() v2.9.0</param>
        /// @ingroup midifileplayer_playback_controls
        public virtual void MPTK_Play(bool alreadyLoaded = false)
        {
            try
            {
                //Debug.Log($"MPTK_Play");

                // V2.82 removed from here
                //MPTK_InitSynth();
                //MPTK_StartSequencerMidi();

                if (MPTK_SoundFont.IsReady)
                {
                    // V2.82 playPause = false; UnPause if paused
                    if (MPTK_IsPaused)
                        MPTK_UnPause();
                    // else - corrected v2.17.1 - remove else
                    // start playing even if player was pausd because unpause do nothing if the player was not running before pause 
                    if (!MPTK_IsPlaying)
                    {
                        // V2.82 moved here
                        MPTK_InitSynth();
                        MPTK_StartSequencerMidi();

                        if (!alreadyLoaded)
                        {

                            // Load description of available soundfont
                            if (MidiPlayerGlobal.ImSFCurrent != null && MidiPlayerGlobal.CurrentMidiSet != null && MidiPlayerGlobal.CurrentMidiSet.MidiFiles != null && MidiPlayerGlobal.CurrentMidiSet.MidiFiles.Count > 0)
                            {
                                //if (VerboseSynth) Debug.Log(MPTK_MidiName);
                                if (string.IsNullOrEmpty(MPTK_MidiName))
                                    MPTK_MidiName = MidiPlayerGlobal.CurrentMidiSet.MidiFiles[0];
                                int selectedMidi = MidiPlayerGlobal.CurrentMidiSet.MidiFiles.FindIndex(s => s == MPTK_MidiName);
                                if (selectedMidi < 0)
                                {
                                    Debug.LogWarning($"MidiFilePlayer - MidiFile '{MPTK_MidiName}' not found. Trying with the first in list.");
                                    selectedMidi = 0;
                                    MPTK_MidiName = MidiPlayerGlobal.CurrentMidiSet.MidiFiles[0];
                                }
                            }
                            else
                                Debug.LogWarning(MidiPlayerGlobal.ErrorNoMidiFile);
                        }
                        if (MPTK_CorePlayer)
                        {
                            //Debug.Log($"MPTK_Play ThreadCorePlay");
                            if (Application.isPlaying)
                                Routine.RunCoroutine(ThreadCorePlay(alreadyLoaded: alreadyLoaded).CancelWith(gameObject), Segment.RealtimeUpdate);
                            else
                                Routine.RunCoroutine(ThreadCorePlay(alreadyLoaded: alreadyLoaded), Segment.EditorUpdate);
                        }
                        else
                        {
                            //Debug.Log($"MPTK_Play ThreadLegacyPlay");
                            Routine.RunCoroutine(ThreadLegacyPlay(alreadyLoaded: alreadyLoaded).CancelWith(gameObject), Segment.RealtimeUpdate);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }
        /// <summary>@brief
        /// Stops MIDI playback and cancels all sounds. This operation is performed in background, so MIDI may really stop after this method returns.
        /// </summary>
        /// @ingroup midifileplayer_playback_controls
        public void MPTK_Stop()
        {
            MPTK_Stop(stopAllSound: true, wait: -1f);
        }
        /// <summary>@brief
        /// Stops MIDI playback and cancels all sounds. This operation is performed in background, so MIDI may really stop after this method returns.
        /// </summary>
        /// <param name="stopAllSound">Set to true to stop all sounds (default), otherwise currently playing notes will continue until they finish.</param>
        /// <param name="wait">With Legacy mode and if greater than -1 (v2.16), waits until MIDI playback is fully stopped or the specified wait time (in milliseconds) is reached. Otherwise, returns immediately.</param>
        /// @ingroup midifileplayer_playback_controls
        public void MPTK_Stop(bool stopAllSound = true, float wait = -1f)
        {
            //Debug.Log($">>> MPTK_Stop stopAllSound:{stopAllSound} wait:{wait}");

            if (midiLoaded != null)
            {
                midiLoaded.ReadyToPlay = false;
                midiIsPlaying = false;
                playPause = false;
                stopMidi = true;
            }

            if (stopAllSound)
            {
                Routine.RunCoroutine(ThreadClearAllSound(true, IdSession), Application.isPlaying ? Segment.RealtimeUpdate : Segment.EditorUpdate);
                if (!MPTK_CorePlayer)
                    Routine.RunCoroutine(ThreadVoiceAudioSourceRemove(wait, IdSession), Application.isPlaying ? Segment.RealtimeUpdate : Segment.EditorUpdate);
            }
            //Debug.Log($"<<< MPTK_Stop");
        }

        /// <summary>@brief 
        /// Restart playing of the current midi file
        /// </summary>
        /// @ingroup midifileplayer_playback_controls
        public void MPTK_RePlay()
        {
            try
            {
                //Debug.Log($"MPTK_RePlay {Application.isPlaying} midiIsPlaying={midiIsPlaying}");
                //if (Application.isPlaying) // v2.89.5 avoid replay from editor mode // check removed v.289.6, play like a charm!
                {
                    playPause = false;
                    try
                    {
                        if (midiIsPlaying)
                        {
                            ThreadClearAllSound(true, IdSession);
                            replayMidi = true;
                        }
                        else
                            MPTK_Play();
                    }
                    catch (Exception ex)
                    {
                        throw new MaestroException($"MPTK_RePlay {ex}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        /// <summary>@brief 
        /// Should the MIDI playing must be paused when the application lost the focus?
        /// </summary>
        [HideInInspector]
        /// @ingroup midifileplayer_playback_controls
        public bool MPTK_PauseOnFocusLoss;

        /// <summary>@brief 
        /// Pause the current playing. Use MPTK_UnPause to continue playing.
        /// </summary>
        /// <param name="timeToPauseMS">time to pause in milliseconds. default or < 0 : indefinitely</param>
        /// @ingroup midifileplayer_playback_controls
        public void MPTK_Pause(float timeToPauseMS = -1f)
        {
            try
            {
                //Debug.Log($"MPTK_Pause {MPTK_IsSpatialSynthMaster} {MPTK_SpatialSynthIndex}");
                if (MPTK_CorePlayer && timeToPauseMS > 0f)
                {
                    // Pause with time limit. The timer pauseMidi is used to un pause the MIDI after the delay
                    pauseMidi.Reset();
                    pauseMidi.Start();
                }

                timeToPauseMilliSeconde = timeToPauseMS;
                watchMidi.Stop();
                playPause = true;
                if (Application.isPlaying)
                    Routine.RunCoroutine(ThreadClearAllSound(), Segment.RealtimeUpdate);
                else
                    Routine.RunCoroutine(ThreadClearAllSound(), Segment.EditorUpdate);
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        /// <summary>@brief 
        /// UnPause the current playing when MidiPlayer is paused with MPTK_Pause.
        /// </summary>
        /// @ingroup midifileplayer_playback_controls
        public void MPTK_UnPause()
        {
            try
            {
                //Debug.Log($"MPTK_UnPause {MPTK_IsSpatialSynthMaster} {MPTK_SpatialSynthIndex}");
                pauseWhenFocusLost = false;
                if (MPTK_CorePlayer)
                {
                    if (timeMidiFromStartPlay <= 0d) watchMidi.Reset(); // V2.82
                    watchMidi.Start();
                    playPause = false;
                }
                else
                {
                    playPause = false;
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }


        /// <summary>@brief 
        /// Plays next MIDI from the list of midi defined in MPTK (see Unity menu Midi)
        /// </summary>
        /// @ingroup midifileplayer_playback_controls
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
                        nextMidi = true;
                        MPTK_RePlay();
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
        /// Plays previous MIDI from the list of midi defined in MPTK (see Unity menu Midi)
        /// </summary>
        /// @ingroup midifileplayer_playback_controls
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
                        prevMidi = true;
                        MPTK_RePlay();
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


        /// @}
        #endregion

        #region Position and Duration
        /// @name Position and Duration
        /// @{


        /// <summary>@brief 
        /// Gets or sets the current position in the MIDI in milliseconds.\n
        /// When a new position is set, the corresponding tick in the MIDI event list is searched for by comparing it with the MPTKEvent.RealTime value, and the current MIDI playback is moved to this new tick position.\n
        /// @note
        ///     - The search does not take #MPTK_Speed into account.
        ///     - Works only when the MIDI is playing.
        ///     - You can't set the position before or just after MPTK_Play() because MPTK_Play() reset the position at the start.
        ///     - Rather, set the position when the event OnEventStartPlayMidi() is triggered. See example below.
        ///     - Look also the properties #MPTK_TickCurrent to change the tick by MIDI ticks.
        ///     - When the MIDI is playing look at the inspector of the MidiFilePlayer prefab to read (or change) the current position in seconds and find the position you want.
        ///     - see also :
        ///         - #MPTK_Load  
        ///         - MidiLoad.MPTK_SearchTickFromTime
        ///         - #MPTK_RawSeek.
        /// @par
        /// @details
        /// Here, more information about Midi Timing https://paxstellar.fr/2020/09/11/midi-timing/\n
        /// @code
        /// public MidiFilePlayer midiFilePlayer;
        /// void Start()
        /// {
        ///    // Find the prefab MidiFilePlayer in your scene
        ///    midiFilePlayer = FindFirstObjectByType<MidiFilePlayer>();
        ///    // Event trigger when midi file start playing
        ///    midiFilePlayer.OnEventStartPlayMidi.AddListener(info => StartPlay("Event set by script"));
        ///    // beginning playing
        ///    midiFilePlayer.MPTK_Play();
        /// }
        /// 
        /// // Method executed when the MIDI file start playing
        /// public void StartPlay(string name)
        /// {
        ///     // The MIDI will start playing at 5 seconds from the beginning of the MIDI
        ///     midiFilePlayer.MPTK_Position = 5000;
        /// }
        /// 
        /// void Update()
        /// {
        ///     if ('condition from your application is true')
        ///         // The MIDI will continue playing at 10 seconds from the beginning of the MIDI
        ///         midiFilePlayer.MPTK_Position = 10000;
        /// }
        /// @endcode
        /// </summary>
        /// @ingroup midifileplayer_positioning
        public double MPTK_Position
        {
            get
            {
                // V2.88 return midiLoaded != null ? midiLoaded.MPTK_ConvertTickToTime(MPTK_TickCurrent) : 0;
                return MPTK_LastEventPlayed != null ? MPTK_LastEventPlayed.RealTime : 0;
            }
            set
            {
                try
                {
                    if (midiLoaded != null)
                    {
                        midiLoaded.fluid_player_seek((int)midiLoaded.MPTK_SearchTickFromTime(value));
                    }
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }
            }
        }

        //! @cond NODOC
        /// <summary>@brief 
        /// Value updated only when playing in Unity (for inspector refresh)
        /// </summary>
        public string durationEditorModeOnly;
        //! @endcond

        /// <summary>@brief 
        /// Real duration expressed in TimeSpan of the full midi from the first event (tick=0) to the last event.\n
        /// If #MPTK_KeepEndTrack is false, the MIDI events End Track are not considered to calculate this time.\n
        /// The tempo changes are taken into account if #MPTK_EnableChangeTempo is set to true before loading the MIDI.
        /// </summary>
        /// @ingroup midifileplayer_positioning
        public TimeSpan MPTK_Duration { get { try { if (midiLoaded != null) return midiLoaded.MPTK_Duration; } catch (System.Exception ex) { MidiPlayerGlobal.ErrorDetail(ex); } return TimeSpan.Zero; } }

        /// <summary>@brief 
        /// Real duration expressed in milliseconds of the full MIDI from the first event (tick=0) to the last event.\n
        /// If #MPTK_KeepEndTrack is false, the MIDI events End Track are not considered to calculate this time.\n
        /// The tempo changes are taken into account if #MPTK_EnableChangeTempo is set to true before loading the MIDI.
        /// </summary>
        /// @ingroup midifileplayer_positioning
        public float MPTK_DurationMS { get { try { if (midiLoaded != null) return midiLoaded.MPTK_DurationMS; } catch (System.Exception ex) { MidiPlayerGlobal.ErrorDetail(ex); } return 0f; } }

        /// <summary>@brief 
        /// Last tick tick in Midi: it's the value of the tick for the last MIDI event in sequence expressed in number of "ticks".\n
        /// </summary>
        /// @ingroup midifileplayer_positioning
        public long MPTK_TickLast { get { return midiLoaded != null ? midiLoaded.MPTK_TickLast : 0; } }

        /// <summary>@brief 
        /// Tick tick for the first note-on found.\n
        /// Most MIDI don't start playing a note immediately. There is often a delay.\n
        /// Use this attribute to known the tick tick where the will start to play a sound.\n
        /// See also #MPTK_PositionFirstNote
        /// </summary>
        /// @ingroup midifileplayer_positioning
        public long MPTK_TickFirstNote { get { return midiLoaded != null ? midiLoaded.MPTK_TickFirstNote : 0; } }

        /// <summary>@brief 
        /// Tick tick for the last note-on found.\n
        /// There is often other MIDI events after the last note-on: for example event track-end.\n
        /// Use this attribute to known the tick tick time when all sound will be stop.\n
        /// See also the #MPTK_PositionLastNote which provides the last time of the MIDI.
        /// </summary>
        /// @ingroup midifileplayer_positioning
        public long MPTK_TickLastNote { get { return midiLoaded != null ? midiLoaded.MPTK_TickLastNote : 0; } }

        /// <summary>@brief 
        /// Real time tick in milliseconds for the first note-on found.\n
        /// Most MIDI don't start playing a note immediately. There is often a delay.\n
        /// Use this attribute to known the real time which it will start.\n
        /// See also #MPTK_TickFirstNote
        /// </summary>
        /// @ingroup midifileplayer_positioning
        public double MPTK_PositionFirstNote { get { return midiLoaded != null ? midiLoaded.MPTK_PositionFirstNote : 0; } }

        /// <summary>@brief 
        /// Real time tick in milliseconds for the last note-on found in the MIDI.\n
        /// There is often other MIDI events after the last note-on: for example event track-end.\n
        /// Use this attribute to known the real time when all sound will be stop.\n
        /// See also the #MPTK_DurationMS which provides the full time of all MIDI events including track-end, control at the beginning and at the end, ....\n
        /// See also #MPTK_TickLastNote
        /// </summary>
        /// @ingroup midifileplayer_positioning
        public double MPTK_PositionLastNote { get { return midiLoaded != null ? midiLoaded.MPTK_PositionLastNote : 0; } }


        /// <summary>@brief 
        /// Count of track read in the MIDI file
        /// </summary>
        /// @ingroup midifileplayer_positioning
        public int MPTK_TrackCount { get { return midiLoaded != null ? midiLoaded.MPTK_TrackCount : 0; } }


        /// <summary>@brief 
        /// Gets the tick value of the last MIDI event played.\n
        /// Sets the tick value of the next MIDI event to played.\n
        /// @details
        /// MIDI tick is an easy way to identify a position in a song independently of the time which could vary with tempo change event.\n
        /// The count of ticks by quarter is constant all along a MIDI, it's a properties of the whole MIDI. see #MPTK_DeltaTicksPerQuarterNote.\n
        /// With a time signature of 4/4 the ticks length of a bar is 4 * #MPTK_DeltaTicksPerQuarterNote.\n
        /// Here, more information about Midi Timing https://paxstellar.fr/2020/09/11/midi-timing/\n
        /// @note
        ///     - works only when the MIDI is playing.\n
        ///     - you can't set the tick before or just after MPTK_Play() because MPTK_Play() reset the tick at the start.\n
        ///     - rather, set  the tick when the event OnEventStartPlayMidi() is triggereed. See example below.\n
        ///     - look also the properties #MPTK_Position to change the tick by milliseconds.\n
        ///     - when the MIDI is playing look at the inspector of the MidiFilePlayer prefab to read (or change) the current tick and find the tick you want.\n
        ///     - see also #MPTK_RawSeek to change way current tick position is changed.
        ///     - look also MidiLoad.MPTK_TickPlayer to get the real-time tick value from the MIDI player.
        /// @par
        /// See example:\n
        /// @code
        /// public MidiFilePlayer midiFilePlayer;
        /// void Start()
        /// {
        ///    // Find the prefab MidiFilePlayer in your scene
        ///    midiFilePlayer = FindFirstObjectByType<MidiFilePlayer>();
        ///    // Event trigger when midi file start playing
        ///    midiFilePlayer.OnEventStartPlayMidi.AddListener(info => StartPlay("Event set by script"));
        ///    // beginning playing
        ///    midiFilePlayer.MPTK_Play();
        /// }
        /// 
        /// // Method executed when the MIDI file start playing
        /// public void StartPlay(string name)
        /// {
        ///     // The MIDI will start playing at tick 10000 
        ///     midiFilePlayer.MPTK_TickCurrent = 10000;
        /// }
        /// 
        /// void Update()
        /// {
        ///     if ('condition from your application is true')
        ///         // The MIDI will continue playing at ticks 20000
        ///         midiFilePlayer.MPTK_TickCurrent = 20000;
        /// }
        /// @endcode
        /// </summary>
        /// @ingroup midifileplayer_positioning
        public long MPTK_TickCurrent
        {
            get
            {
                return midiLoaded != null ? midiLoaded.MPTK_TickCurrent : 0;
            }
            set
            {
                try
                {
                    if (midiLoaded != null)
                    {
                        //Debug.Log("Set MPTK_TickCurrent:" + value);

                        long tick = value;
                        if (tick < 0) tick = 0;
                        if (tick > MPTK_TickLast) tick = MPTK_TickLast;
                        //MPTK_Position = miditoplay.MPTK_ConvertTickToTime(tick);
                        midiLoaded.fluid_player_seek((int)tick);
                    }
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }
            }
        }

        /// <summary>@brief 
        /// This parameter controls how the playback position is adjusted within a MIDI file.
        /// By default (false), when the playback position is changed, all events except "note-on" are replayed from the start of the MIDI file up to the new position.
        /// This ensures the synthesizer is correctly updated with the appropriate context (tempo, selected instruments, controllers, etc.).\n
        /// If set to true, the playback position is updated directly; consequently, the current context is preserved. \n
        /// While this approach can lead to unintended (or amusing!) effects in some MIDI files, it allows for much faster position changes.
        /// @version 2.12.0
        /// 
        /// @note
        ///     - This value resets to false (default) each time a MIDI file is loaded. You can use #MPTK_Load() to load the MIDI, adjust this parameter, and then play the MIDI using #MPTK_Play().
        /// 
        /// </summary>
        /// @ingroup midifileplayer_positioning
        public bool MPTK_RawSeek
        {
            get
            {
                return midiLoaded != null ? midiLoaded.MPTK_RawSeek : rawSeek;
            }
            set
            {
                rawSeek = value;
                if (midiLoaded != null)
                    // Apply if a MIDI is loaded
                    midiLoaded.MPTK_RawSeek = rawSeek;
            }
        }

        /// <summary>@brief 
        /// Last MIDI event played by the MIDI sequencer
        /// </summary>
        /// @ingroup midifileplayer_positioning
        public MPTKEvent MPTK_LastEventPlayed
        {
            get
            {
                return midiLoaded?.MPTK_LastEventPlayed;
            }
        }

        /// <summary>@brief
        /// Length in milliseconds of a MIDI tick. The pulse length is the minimum time in milliseconds between two MIDI events.\n
        /// It's like a definition of graphical resolution but for MIDI: the MIDI sequencer will not be able to play two separate MIDI events in a time below this value.\n
        /// @note
        ///     - Depends on the current tempo (#MPTK_CurrentTempo) and the #MPTK_DeltaTicksPerQuarterNote and Speed.
        ///     - Read Only.
        ///     - Formula: PulseLength = MPTK_CurrentTempo / MPTK_DeltaTicksPerQuarterNote / 1000 / Speed
        /// 
        /// </summary>
        /// @ingroup midifileplayer_positioning
        public double MPTK_Pulse { get { try { if (midiLoaded != null) return midiLoaded.MPTK_Pulse; } catch (System.Exception ex) { MidiPlayerGlobal.ErrorDetail(ex); } return 0d; } }

        public double MPTK_PulseLenght { get { Debug.LogWarning("MPTK_PulseLenght has been deprecated, please investigate MPTK_Pulse in place"); return 0d; } }

        //! @cond NODOC
        /// <summary>@brief 
        /// Updated only when playing in Unity (for inspector refresh)
        /// </summary>
        public string playTimeEditorModeOnly;
        //! @endcond

        /// <summary>@brief 
        /// Real time in TimeSpan format from the beginning of play. It's an access to the MIDI timer used by the MIDI sequencer.
        /// @note
        /// #MPTK_Tempo or #MPTK_Speed change have no direct impact on this value.
        /// </summary>
        /// @ingroup midifileplayer_positioning
        public TimeSpan MPTK_PlayTime { get { try { return TimeSpan.FromMilliseconds(timeMidiFromStartPlay); } catch (System.Exception ex) { MidiPlayerGlobal.ErrorDetail(ex); } return TimeSpan.Zero; } }

        /// <summary>@brief 
        /// Real time in milliseconds from the beginning of play. It's an access to the MIDI timer used by the MIDI sequencer.
        /// @note
        /// #MPTK_Tempo or #MPTK_Speed change have no direct impact on this value.
        /// </summary>
        /// @ingroup midifileplayer_positioning
        public double MPTK_RealTime { get { return timeMidiFromStartPlay; } }

        /// @}
        #endregion

        #region MIDI Events
        /// @name MIDI Events
        /// @{

        /// <summary>@brief 
        /// Method triggered for each MIDI event (or group of MIDI events) ready to be played by the MIDI synth. All these events are on same MIDI tick\n.
        /// The callback method is able to directly interacts with Unity gameObject (same thread).\n
        /// A List<MPTKEvent> is passed to the delegate.
        /// @note
        /// It's not possible to alter playing music by modifying note properties (pitch, velocity, ....) in the callback.
        /// @par
        /// @code
        /// 
        /// using MidiPlayerTK; // Add a reference to the MPTK namespace at the top of your script
        /// using UnityEngine;        
        ///  
        /// public class YourClass : MonoBehaviour
        /// {
        ///     
        ///     MidiFilePlayer midiFilePlayer;
        /// 
        ///     void Start()
        ///     {
        ///         // Get a reference to the prefab MidiFilePlayer from the hierarchy in the scene
        ///         midiFilePlayer = FindFirstObjectByType<MidiFilePlayer>(); 
        ///          
        ///         // Add a listener on the MIDI File Player.
        ///         // NotesToPlay will be called for each new group of notes read by the MIDI sequencer from the MIDI file.
        ///         midiFilePlayer.OnEventNotesMidi.AddListener(NotesToPlay);
        ///     }
        /// 
        ///     // This method will be called by the MIDI sequencer just before the notes
        ///     // are playing by the MIDI synthesizer (if 'Send To Synth' is enabled)
        ///     public void NotesToPlay(List<MPTKEvent> mptkEvents)
        ///     {
        ///         Debug.Log("Received " + mptkEvents.Count + " MIDI Events");
        ///         // Loop on each MIDI events
        ///         foreach (MPTKEvent mptkEvent in mptkEvents)
        ///         {
        ///             // Log if event is a note on
        ///             if (mptkEvent.Command == MPTKCommand.NoteOn)
        ///                 Debug.Log($"Note on Time:{mptkEvent.RealTime} millisecond  Note:{mptkEvent.Value}  Duration:{mptkEvent.Duration} millisecond  Velocity:{mptkEvent.Velocity}");
        ///                 
        ///             // Uncomment to display all MIDI events
        ///             // Debug.Log(mptkEvent.ToString());
        ///         }
        ///     }
        /// }
        /// 
        /// @endcode
        /// </summary>
        //[HideInInspector]
        /// @ingroup midifileplayer_events
        public EventNotesMidiClass OnEventNotesMidi;


        /// <summary>@brief 
        /// Define the Unity event to be triggered at the start of Midi playback.\n
        /// At this moment, the MIDI file is loaded, the MIDI synth is initialised, but no MIDI event has been read yet.\n
        /// This is the right time to defined some specific behaviors. 
        /// @code
        /// 
        /// using MidiPlayerTK; // Add a reference to the MPTK namespace at the top of your script
        /// using UnityEngine;        
        ///  
        /// public class YourClass : MonoBehaviour
        /// {
        ///     MidiFilePlayer midiFilePlayer;
        /// 
        ///     void Start()
        ///     {
        ///         // Get a reference to the prefab MidiFilePlayer from the hierarchy in the scene
        ///         midiFilePlayer = FindFirstObjectByType<MidiFilePlayer>(); 
        ///          
        ///         // Add a listener on the MIDI File Player.
        ///         // NotesToPlay will be called for each new group of notes read by the MIDI sequencer from the MIDI file.
        ///         midiFilePlayer.OnEventStartPlayMidi.AddListener(StartPlay);
        ///     }
        /// 
        ///     /// <summary>
        ///     /// Start playing: MIDI File is loaded, Midi Synth is initialized, but so far any MIDI event has been read.
        ///     /// This is the right time to defined some specific behaviors. 
        ///     /// </summary>
        ///     /// <param name="midiname"></param>
        ///     public void StartPlay(string midiname)
        ///     {
        ///         Debug.LogFormat($"Start playing midi {midiname}");
        ///         
        ///         // Disable MIDI channel 9 (generally drums)
        ///         midiFilePlayer.MPTK_ChannelEnableSet(9, false);
        ///         
        ///         // Set start tick
        ///         midiFilePlayer.MPTK_TickCurrent = 500;        
        ///     }
        /// } 
        /// 
        /// @endcode
        /// </summary>
        //[HideInInspector]
        /// @ingroup midifileplayer_events
        public EventStartMidiClass OnEventStartPlayMidi;

        /// <summary>@brief 
        /// Specify the Unity event that is triggered when the end of the MIDI list of events is reached.
        /// @note
        ///     - This event is triggered even if the note is still in play. In some cases this may cause unpleasant behavior.\n
        ///       #MPTK_ModeStopVoice defined the behavior of the MIDI player when playback is stopped or restarted.\n
        ///       A good practice is to defined #MPTK_ModeStopVoice = ModeStopPlay.StopWhenAllVoicesReleased.
        ///     - By default, the end of playback of a MIDI file is not the last note. It is the last MIDI event.\n
        ///       Sets #MPTK_StopPlayOnLastNote to true to fire this event on the last note.
        ///     - Set #MPTK_KeepEndTrack or #MPTK_KeepNoteOff to true when loading the MIDI file to synchronise the end of playback with the real end of the MIDI file.
        /// @code
        /// 
        /// using MidiPlayerTK; // Add a reference to the MPTK namespace at the top of your script
        /// using UnityEngine;        
        ///  
        /// public class YourClass : MonoBehaviour
        /// {
        ///     MidiFilePlayer midiFilePlayer;
        /// 
        ///     void Start()
        ///     {
        ///         // Get a reference to the prefab MidiFilePlayer from the hierarchy in the scene
        ///         midiFilePlayer = FindFirstObjectByType<MidiFilePlayer>(); 
        ///          
        ///         // Add a listener on the MIDI File Player.
        ///         // NotesToPlay will be called for each new group of notes read by the MIDI sequencer from the MIDI file.
        ///         midiFilePlayer.OnEventEndPlayMidi.AddListener(EndPlay);
        ///         midiFilePlayer.MPTK_ModeStopVoice = MidiFilePlayer.ModeStopPlay.StopWhenAllVoicesReleased;
        ///     }
        /// 
        ///     public void EndPlay(string midiname, EventEndMidiEnum reason)
        ///     {
        ///         Debug.LogFormat($"End playing midi {midiname} reason:{reason}");
        ///     }
        /// }
        /// 
        /// @endcode
        /// </summary>
        // [HideInInspector]
        /// @ingroup midifileplayer_events
        public EventEndMidiClass OnEventEndPlayMidi;

        /// @}
        #endregion


        [SerializeField]
        //[HideInInspector]
        private bool playOnStart = false,
            replayMidi = false, stopMidi = false,
            midiIsPlaying = false, midiAutoRestart = false,
            keepNoteOff = false, keepEndTrack = false, needDelayToStart = false,
            needDelayToStop = false, /* V2.84 */
            rawSeek = false;

        //private float delayToStopMilliseconds = 100f;

        private float timeRampUpSecond = 0f;
        private float delayRampUpSecond = 0f;

        private float timeRampDnSecond = 0f;
        private float delayRampDnSecond = 0f;

        [SerializeField]
        [HideInInspector]
        public bool nextMidi = false, prevMidi = false;

        //[SerializeField]
        //[HideInInspector]
        //protected bool playPause = false;

        [Range(0, 100)]
        private float delayMilliSeconde = 15f;  // only with AudioSource mode (non core)

        private double lastMidiTimePlayAS = 0d;
        protected double timeAtStartMidi = 0d;

        [HideInInspector]
        protected bool distancePause;

        new void Awake()
        {
            if (VerboseSynth) Debug.Log($"Awake MidiFilePlayer  midiIsPlaying:{midiIsPlaying} isPlaying:{Application.isPlaying} isEditor:{Application.isEditor}");
            AwakeMidiFilePlayer();
        }

        protected void AwakeMidiFilePlayer()
        {
            //Debug.Log("AwakeMidiFilePlayer MidiFilePlayer midiIsPlaying:" + midiIsPlaying);
            midiIsPlaying = false;
#if MPTK_PRO
            if (MPTK_InnerLoop == null)
                MPTK_InnerLoop = new MPTKInnerLoop();
#endif
            base.Awake();
        }

        new void Start()
        {
            if (VerboseSynth) Debug.Log($"Start MidiFilePlayer {this.name} isPlaying:{Application.isPlaying} midiIsPlaying:{midiIsPlaying} MPTK_PlayOnStart:{MPTK_PlayOnStart}");
            //if (Application.isPlaying && name == "MidiSequencerEditor")
            //    DestroyImmediate(this.gameObject, true);
            StartMidiFilePlayer();
        }

        protected void StartMidiFilePlayer()
        {
            //Debug.Log("StartMidiFilePlayer MidiFilePlayer midiIsPlaying:" + midiIsPlaying + " MPTK_PlayOnStart:" + MPTK_PlayOnStart);

            // V2.10.1 always init the synth at start
            // to be evaluated MPTK_InitSynth();

            // V2.11.0 already instanciated by Unity
            //if (OnEventStartPlayMidi == null) OnEventStartPlayMidi = new EventStartMidiClass();
            //if (OnEventNotesMidi == null) OnEventNotesMidi = new EventNotesMidiClass();
            //if (OnEventEndPlayMidi == null) OnEventEndPlayMidi = new EventEndMidiClass();

            base.Start();
            try
            {
                //Debug.Log("   midiIsPlaying:" + midiIsPlaying + " MPTK_PlayOnStart:" + MPTK_PlayOnStart);
                if (MPTK_PlayOnStart)
                {
                    Routine.RunCoroutine(TheadPlayIfReady(), Segment.RealtimeUpdate);
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        private void OnDestroy()
        {
            //Debug.Log("OnDestroy " + Time.time + " seconds");
            // MPTK_Stop(); this call launch a coroutine, not a good idea when scene is closing!
            // Extract of MPTK_Stop:
            if (midiLoaded != null)
            {
                midiLoaded.ReadyToPlay = false;
                midiIsPlaying = false;
                playPause = false;
                stopMidi = true;
            }
            MPTK_StopSynth();
        }

        void OnApplicationQuit()
        {
            //Debug.Log("OnApplicationQuit " + Time.time + " seconds");
            MPTK_Stop();
            MPTK_StopSynth();
        }

        private void OnApplicationPause(bool pause)
        {
            //Debug.Log("MidiFilePlayer OnApplicationPause " + pause);
            if (pause && MPTK_PauseOnFocusLoss)
            {
                if (watchMidi.IsRunning)
                    watchMidi.Stop();
            }
            else
            {
                if (!watchMidi.IsRunning)
                    watchMidi.Start();
            }
        }

        private bool pauseWhenFocusLost = false;
        void OnApplicationFocus(bool hasFocus)
        {
            //Debug.Log("MidiFilePlayer OnApplicationFocus " + hasFocus);
            if (MPTK_PauseOnFocusLoss)
            {
                if (!hasFocus)
                {
                    if (!MPTK_IsPaused)
                    {
                        // focus lost, need to be paused 
                        pauseWhenFocusLost = true;
                        MPTK_Pause();
                    }
                }
                else
                {
                    // unpause only if paused from a focus lost
                    if (pauseWhenFocusLost)
                        MPTK_UnPause();
                }
            }
        }

        protected IEnumerator<float> TheadPlayIfReady()
        {
            while (!MPTK_SoundFont.IsReady)
                yield return Routine.WaitForSeconds(0.2f);

            // Wait a few of millisecond to let app to start (useful when play on start)
            yield return Routine.WaitForSeconds(0.2f);

            MPTK_Play();
        }

        #region MIDI Properties
        /// @name MIDI Properties

        /// <summary>@brief 
        /// Percentage of the default playback speed. Range  0.1 (10% of the current BPM) to 10 (1000%). Default is 1 for normal speed.\n
        /// Speed also applied to the duration of the sample played at voice level (often multiple voices are played for one note).
        /// @note:
        ///     - MPTK_Pulse is modified, formula for pulse (60000000 /  MPTK_CurrentTempo) / MPTK_DeltaTicksPerQuarterNote / 1000 / Speed
        ///     - Release time (time after the note-off) remains unchanged, so some traffic jam could occur for the MIDI synth.
        ///     - No MPTKEvent attributes are modified. Duration and RealTime remain unchanged.
        ///     - Unlock the range from 0.0001 to 100 when the Unity script symbol MPTK_UNLOCK_SPEED is defined (experimental). 
        ///        - Unity 2022/2023: https://docs.unity3d.com/2022.3/Documentation/Manual/CustomScriptingSymbols.html
        ///        - Unity 6: https://docs.unity3d.com/6000.0/Documentation/Manual/custom-scripting-symbols.html
        /// </summary>
        /// @ingroup midifileplayer_positioning
        public float MPTK_Speed
        {
            get
            {
                //Debug.Log("get speed " + speed );
                return speed;
            }
            set
            {
                try
                {
                    if (value != speed)
                    {
                        //Debug.Log("set speed " + value);
                        if (value >= Constant.MIN_SPEED && value <= Constant.MAX_SPEED)
                        {
                            speed = value;
                            if (midiLoaded != null)
                                midiLoaded.ChangeSpeed(speed);
                        }
                        else
                            Debug.LogWarning("MidiFilePlayer - Set Speed value not valid : " + value);
                    }
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }
            }
        }

        /// <summary>@brief 
        /// @deprecated with 2.10.0 MPTK_Loop is deprecated. Please investigate #MPTK_MidiRestart or #MPTK_InnerLoop (Pro) for a better looping accuracy.
        /// </summary>
        [HideInInspector]
        public bool MPTK_Loop
        {
            get { Debug.LogWarning("MPTK_Loop is deprecated. Please investigate MPTK_MidiAutoRestart."); return false; }
            set { Debug.LogWarning("MPTK_Loop is deprecated. Please investigate MPTK_MidiAutoRestart."); }
        }


        /// <summary>@brief 
        /// Gets or sets the current tempo played by the internal MIDI sequencer (independent from MPTK_Speed). \n
        /// Return QuarterPerMinuteValue similar to BPM (Beat Per Measure).\n
        /// @note
        ///     - Can be handle only when the MIDI is playing.
        ///     - Changing the current tempo when playing has no impact on the calculated duration of the MIDI.
        /// </summary>
        public double MPTK_Tempo
        {
            get
            {
                if (midiLoaded != null) return midiLoaded.MPTK_CurrentTempo; else return 0d;
            }
            set
            {
                if (midiLoaded != null) midiLoaded.MPTK_CurrentTempo = value;
            }
        }

        /// <summary>@brief 
        /// Gets sequence track name if defined in the MIDI file with  MIDI MetaEventType = SequenceTrackName\n
        /// See detail here https://ccrma.stanford.edu/~craig/14q/midifile/MidiFileFormat.html \n
        /// Can be used only when the MIDI is playing.
        /// </summary>
        public string MPTK_SequenceTrackName { get { return midiLoaded != null ? midiLoaded.SequenceTrackName : ""; } }

        /// <summary>@brief 
        /// Gets Program track name if defined in the MIDI file with  MIDI MetaEventType = ProgramName\n
        /// See detail here https://ccrma.stanford.edu/~craig/14q/midifile/MidiFileFormat.html \n
        /// Can be used only when the MIDI is playing.
        /// </summary>
        public string MPTK_ProgramName { get { return midiLoaded != null ? midiLoaded.ProgramName : ""; } }

        /// <summary>@brief 
        /// Gets Instrument track name if defined in the MIDI file with  MIDI MetaEventType = TrackInstrumentName\n
        /// See detail here https://ccrma.stanford.edu/~craig/14q/midifile/MidiFileFormat.html \n
        /// Can be used only when the MIDI is playing.
        /// </summary>
        public string MPTK_TrackInstrumentName { get { return midiLoaded != null ? midiLoaded.TrackInstrumentName : ""; } }

        /// <summary>@brief 
        /// Gets Text if defined in the MIDI file with  MIDI MetaEventType = TextEvent\n
        /// See detail here https://ccrma.stanford.edu/~craig/14q/midifile/MidiFileFormat.html \n
        /// Can be used only when the MIDI is playing.
        /// </summary>
        public string MPTK_TextEvent { get { return midiLoaded != null ? midiLoaded.TextEvent : ""; } }

        /// <summary>@brief 
        /// Gets Copyright if defined in the MIDI file with  MIDI MetaEventType = Copyright\n
        /// See detail here https://ccrma.stanford.edu/~craig/14q/midifile/MidiFileFormat.html \n
        /// Can be used only when the MIDI is playing.
        /// </summary>
        public string MPTK_Copyright { get { return midiLoaded != null ? midiLoaded.Copyright : ""; } }



        /// <summary>@brief 
        /// Force all notes to return to their original values before transposing.\n 
        /// Useful when looping on a MIDI with a transpose value different than 0. 
        /// When returning to 0 (no transpose) the note value can be reset to their original value.
        /// @version V2.14.0
        /// </summary>
        public void MPTK_ResetTranspose()
        {
            if (midiLoaded == null || midiLoaded.MPTK_MidiEvents == null)
                Debug.LogWarning("MidiFilePlayer - No MIDI loaded - MPTK_ResetTranpose canceled ");
            else
                foreach (MPTKEvent mPTKEvent in midiLoaded.MPTK_MidiEvents)
                    if (mPTKEvent.Command == MPTKCommand.NoteOn)
                        mPTKEvent.ResetTransposeValue();
        }
        /// <summary>@brief 
        /// Gets a list of all the MPTK MIDI events that are available in the MIDI.\n
        /// @snippet TestMidiFilePlayerScripting.cs Example_GUI_PreloadAndAlterMIDI
        /// </summary>
        public List<MPTKEvent> MPTK_MidiEvents
        {
            get
            {
                return midiLoaded != null ? midiLoaded.MPTK_MidiEvents : null;
            }
        }
        /// <summary>@brief 
        /// Level of quantization : 
        ///     -      0 = None 
        ///     -      1 = Beat Note
        ///     -      2 = Eighth Note
        ///     -      3 = 16th Note
        ///     -      4 = 32th Note
        ///     -      5 = 64th Note
        ///     -      6 = 128th Note
        /// </summary>
        public int MPTK_Quantization
        {
            get { return quantization; }
            set
            {
                try
                {
                    if (value >= 0 && value <= 6)
                    {
                        quantization = value;
                        midiLoaded.ChangeQuantization(quantization);
                    }
                    else
                        Debug.LogWarning("MidiFilePlayer - Set Quantization value not valid : " + value);
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }
            }
        }
        [SerializeField]
        [HideInInspector]
        private int quantization = 0;

        /// <summary>@brief 
        /// Delta Ticks Per Beat Note. Indicate the duration time in "ticks" which make up a quarter-note.\n 
        /// For instance, if 96, then a duration of an eighth-note in the file would be 48.\n
        /// More info here https://paxstellar.fr/2020/09/11/midi-timing/\n
        /// @code
        /// Move forward one quarter
        /// midiFilePlayer.MPTK_TickCurrent = midiFilePlayer.MPTK_TickCurrent + midiFilePlayer.MPTK_DeltaTicksPerQuarterNote;
        /// @endcode
        /// </summary>
        public int MPTK_DeltaTicksPerQuarterNote
        {
            get
            {
                int DeltaTicksPerQuarterNote = 0;
                try
                {
                    DeltaTicksPerQuarterNote = midiLoaded?.MPTK_DeltaTicksPerQuarterNote ?? 0;
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }
                return DeltaTicksPerQuarterNote;
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
            return MPTKEvent.EnumLength.Sixteenth;
        }

        /// @}
        #endregion

        #region MIDI Loading and Reading

        /// @name MIDI Loading and Reading
        /// @{

        /// <summary>@brief 
        /// Loads the midi file defined with MPTK_MidiName or MPTK_MidiIndex. It's an optional action before playing a midi file with MPTK_Play()\n
        /// Use this method to get all MIDI events before start playing.
        /// @note
        ///     - Logs are displayed in case of error.
        ///     - Look at MPTK_StatusLastMidiLoaded for load status information.\n
        ///     - Look at MPTK_MidiLoaded for detailed information about the MIDI loaded.\n
        /// @snippet LoadMidiAndPlay.cs LoadMidiAndPlay
        /// </summary>        
        /// <returns>MidiLoad to access all the properties of the midi loaded, null in case of error look at MPTK_StatusLastMidiLoaded </returns>
        /// @ingroup midifileplayer_loading
        public MidiLoad MPTK_Load()
        {

            if (string.IsNullOrEmpty(MPTK_MidiName))
            {
                MPTK_StatusLastMidiLoaded = LoadingStatusMidiEnum.MidiNameInvalid;
                Debug.LogWarning("MPTK_Load: midi name not defined");
                return null;
            }

            TextAsset mididata = Resources.Load<TextAsset>(System.IO.Path.Combine(MidiPlayerGlobal.MidiFilesDB, MPTK_MidiName));
            if (mididata == null || mididata.bytes == null || mididata.bytes.Length == 0)
            {
                MPTK_StatusLastMidiLoaded = LoadingStatusMidiEnum.MidiFileInvalid;
                Debug.LogWarning("MPTK_Load: error when loading midi " + MPTK_MidiName);
                return null;
            }

            try
            {
                midiLoaded = new MidiLoad();

                midiLoaded.MPTK_KeepNoteOff = MPTK_KeepNoteOff;
                midiLoaded.MPTK_KeepEndTrack = MPTK_KeepEndTrack;
                midiLoaded.MPTK_LogLoadEvents = MPTK_LogLoadEvents;
                midiLoaded.MPTK_EnableChangeTempo = MPTK_EnableChangeTempo;
                midiLoaded.MPTK_Load(mididata.bytes);
            }
            catch (System.Exception ex)
            {
                MPTK_StatusLastMidiLoaded = LoadingStatusMidiEnum.MidiFileInvalid;
                MidiPlayerGlobal.ErrorDetail(ex);
                return null;
            }
            return midiLoaded;
        }

        /// <summary>@brief 
        /// Read the list of midi events available in the MIDI from a ticks tick to an end tick.
        /// @snippet TestMidiFilePlayerScripting.cs Example TheMostSimpleDemoForMidiPlayer
        /// </summary>
        /// <param name="fromTicks">ticks start, default 0</param>
        /// <param name="toTicks">ticks end, default end of MIDI file</param>
        /// <returns></returns>
        /// @ingroup midifileplayer_loading
        public List<MPTKEvent> MPTK_ReadMidiEvents(long fromTicks = 0, long toTicks = long.MaxValue)
        {
            if (midiLoaded == null)
            {
                Debug.LogWarning("MidiFilePlayer - No MIDI loaded - MPTK_ReadMidiEvents canceled ");
                return null;
            }
            midiLoaded.MPTK_KeepNoteOff = MPTK_KeepNoteOff;
            midiLoaded.MPTK_KeepEndTrack = MPTK_KeepEndTrack;
            midiLoaded.MPTK_LogLoadEvents = MPTK_LogLoadEvents;
            midiLoaded.MPTK_EnableChangeTempo = true;
            return midiLoaded.MPTK_ReadMidiEvents(fromTicks, toTicks);
        }

        /// <summary>@brief 
        /// Gets detailed information about MIDI playback. This readonly property is available only when a MIDI has been loaded.
        /// </summary>
        /// @ingroup midifileplayer_loading
        public MidiLoad MPTK_MidiLoaded { get { return midiLoaded; } }



        /// <summary>@brief
        /// MIDI list events must be sorted by ticks before playing. It's mandatory if the list is modified. 
        /// @version 2.0.0 - Maestro Pro 
        /// @snippet TestMidiFilePlayerScripting.cs Example_GUI_PreloadAndAlterMIDI
        /// </summary>
        /// @ingroup midifileplayer_loading
        public void MPTK_SortEvents()
        {
            if (MPTK_MidiEvents != null)
                MPTK_MidiEvents.Sort((e1, e2) =>
                {
                    return e1.Compare(e2);
                });
            else
                Debug.LogWarning("MPTK_SortEvents: No MIDI loaded");
        }

        /// <summary>@brief 
        /// A MIDI file is a kind of keyboard simulation: in general, a key pressed generates a 'note-on' and a key release generates a 'note-off'.\n
        /// But there is an other possibility in a MIDI file: create a 'note-on' with a velocity=0 which must act as a 'midi-off'\n
        /// By default, MPTK create only one MPTK event with the command NoteOn and a duration.\n
        /// But in some cases, you could want to keep the note-off events if they exist in the MIDI file.\n
        /// Sets to false if there is no need (could greatly increases the MIDI list events).\n
        /// Sets to true to keep 'note-off' events.
        /// </summary>
        public bool MPTK_KeepNoteOff
        {
            get { return keepNoteOff; }
            set { keepNoteOff = value; }
        }

        /// <summary>@brief 
        /// When set to true, meta MIDI event End Track are keep. Default is false.\n
        /// If set to true, the End Track Event are taken into account for calculate the full duration of the MIDI.\n
        /// See #MPTK_DurationMS.
        /// </summary>
        public bool MPTK_KeepEndTrack
        {
            get { return keepEndTrack; }
            set { keepEndTrack = value; }
        }
        /// <summary>@brief
        /// If the value is true, text read from Text META (e.g. lyrics) will be read with UTF8 encoding. The default is false.\n
        /// The MIDI standard only allows ASCII characters for this META, but with this extension you will be able to read and display\n
        /// characters like Korean, Chinese, Japanese and even French accented letters ;-)
        /// @version 2.11.3
        /// </summary>
        public bool MPTK_ExtendedText { get; set; }

        /// <summary>@brief
        /// If true display in console all midi events when a MIDI file is loaded.\n
        /// @note Set to true will increase greatly the MIDI load time. To be used only for debug purpose.
        /// @version 2.10.0
        /// </summary>
        [Tooltip("Warning: enabled will increase greatly the MIDI load time. To be used only for debug purpose.")]
        public bool MPTK_LogLoadEvents;

        /// <summary>@brief 
        /// Status of the last midi loaded. The status is updated in a coroutine, so the status can change at each frame.
        /// </summary>
        [HideInInspector]
        /// @ingroup midifileplayer_loading
        public LoadingStatusMidiEnum MPTK_StatusLastMidiLoaded;

        /// <summary>@brief 
        /// Contains the error from the web request when loading MIDI from an URL
        /// </summary>
        [HideInInspector]
        public string MPTK_WebRequestError;
        /// @}
#endregion

        //protected IEnumerator<float> TestFrameDelay()
        //{
        //    double deltaTime = 0;
        //    do
        //    {
        //        deltaTime = (Time.realtimeSinceStartup - lastTimePlay) * 1000d;
        //        timeFromStartPlay += deltaTime;
        //        Debug.Log("   deltaTime:" + Math.Round(deltaTime, 3));

        //        lastTimePlay = Time.realtimeSinceStartup;

        //        if (stopMidi)
        //        {
        //            break;
        //        }

        //        if (delayMilliSeconde > 0)
        //            yield return Timing.WaitForSeconds(delayMilliSeconde / 1000F);
        //        else
        //            yield return -1;

        //    }
        //    while (true);
        //}

        //! @cond NODOC

        /// <summary>@brief 
        /// Read and play MIDI event from the Unity Main Thread
        /// </summary>
        /// <param name="midiBytesToPlay"></param>
        /// <returns></returns>
        /*protected */
        public IEnumerator<float> ThreadLegacyPlay(byte[] midiBytesToPlay = null, string midiName = "", float fromPosition = 0, float toPosition = 0, bool alreadyLoaded = false)
        {
            double deltaTime = 0;
            midiIsPlaying = true;
            stopMidi = false;
            replayMidi = false;
            bool first = true;
            string currentMidiName = midiName != null ? midiName : "";
            //Debug.Log("Start play");
            if (alreadyLoaded)
            {
                //Debug.Log("Start playing same loaded MIDI");
            }
            else
            {
                try
                {
                    midiLoaded = new MidiLoad();

                    // No midi byte array, try to load from MidiFilesDN from resource
                    if (midiBytesToPlay == null || midiBytesToPlay.Length == 0)
                    {
                        currentMidiName = MPTK_MidiName;
                        TextAsset mididata = Resources.Load<TextAsset>(System.IO.Path.Combine(MidiPlayerGlobal.MidiFilesDB, currentMidiName));
                        midiBytesToPlay = mididata.bytes;
                    }

                    midiLoaded.MPTK_KeepNoteOff = MPTK_KeepNoteOff;
                    midiLoaded.MPTK_KeepEndTrack = MPTK_KeepEndTrack;
                    midiLoaded.MPTK_LogLoadEvents = MPTK_LogLoadEvents;
                    midiLoaded.MPTK_EnableChangeTempo = MPTK_EnableChangeTempo;
                    midiLoaded.MPTK_ExtendedText = MPTK_ExtendedText;
                    midiLoaded.MPTK_Load(midiBytesToPlay);
#if MPTK_PRO
                    MPTK_InnerLoop.Clear();
#endif
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }
            }
            if (midiLoaded != null && midiLoaded.MPTK_MidiEvents != null && midiLoaded.MPTK_MidiEvents.Count != 0)
            {
                // Clear all sound from a previous midi - v2.71 wait until all notes are stopped
                // V2.84 yield return Timing.WaitUntilDone(Timing.RunCoroutine(ThreadClearAllSound(true)), false);
                //Timing.RunCoroutine(ThreadClearAllSound(true));
                // V2.84
                if (Application.isPlaying)
                    Routine.RunCoroutine(ThreadClearAllSound(true, IdSession), Segment.RealtimeUpdate);
                else
                    Routine.RunCoroutine(ThreadClearAllSound(true, IdSession), Segment.EditorUpdate);

                try
                {
                    midiLoaded.ChangeSpeed(MPTK_Speed);
                    midiLoaded.ChangeQuantization(MPTK_Quantization);

                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }

                lastMidiTimePlayAS = Time.realtimeSinceStartup;
                timeMidiFromStartPlay = fromPosition;

                MPTK_ResetStat();

                timeAtStartMidi = (System.DateTime.UtcNow.Ticks / 10000D);
                ResetMidiPlayer();
                if (MPTK_Channels.EnableResetChannel)
                    MPTK_Channels.ResetExtension();

                // Call Event StartPlayMidi
                try
                {
                    // TO BE TESTED !!!!

                    if (SpatialSynths != null)
                        // Send to the channel synth
                        foreach (MidiFilePlayer mfp in SpatialSynths)
                            if (mfp.OnEventStartPlayMidi != null)
                                mfp.OnEventStartPlayMidi.Invoke(currentMidiName);

                    if (OnEventStartPlayMidi != null) // v 2.10.0
                        OnEventStartPlayMidi.Invoke(currentMidiName);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("OnEventStartPlayMidi: exception detected. Check the callback code");
                    Debug.LogException(ex);
                }

                if (midiLoaded.MPTK_TickStart > 0)
                    midiLoaded.TickSeek = midiLoaded.MPTK_TickStart;
                else if (fromPosition > 0)
                    MPTK_Position = fromPosition;
                else if (MPTK_StartPlayAtFirstNote && midiLoaded.MPTK_TickFirstNote > 0)
                    midiLoaded.TickSeek = midiLoaded.MPTK_TickFirstNote;

                //
                // Read and play MIDI event from the Unity Main Thread
                // --------------------------------------------------
                do
                {
                    midiLoaded.MPTK_KeepNoteOff = MPTK_KeepNoteOff;
                    midiLoaded.MPTK_KeepEndTrack = MPTK_KeepEndTrack;
                    midiLoaded.MPTK_LogLoadEvents = MPTK_LogLoadEvents;
                    midiLoaded.MPTK_EnableChangeTempo = MPTK_EnableChangeTempo;
                    midiLoaded.endPlayAtLastNote = MPTK_StopPlayOnLastNote;

                    if (MPTK_DistanceAttenuation)
                    {
                        distanceToListener = MidiPlayerGlobal.MPTK_DistanceToListener(this.transform);
                        if (distanceToListener > MPTK_MaxDistance && MPTK_PauseOnMaxDistance)
                        {
                            yield return -1;
                            continue;
                        }
                    }

                    if (playPause)
                    {
                        deltaTime = (Time.realtimeSinceStartup - lastMidiTimePlayAS) * 1000d;
                        lastMidiTimePlayAS = Time.realtimeSinceStartup;
                        //Debug.Log("pause " + timeToPauseMilliSeconde+ " " + deltaTime);
                        yield return Routine.WaitForSeconds(0.2f);
                        if (midiLoaded.EndMidiEvent || replayMidi || stopMidi)
                        {
                            break;
                        }
                        if (timeToPauseMilliSeconde > -1f)
                        {
                            timeToPauseMilliSeconde -= (float)deltaTime;
                            if (timeToPauseMilliSeconde <= 0f)
                                playPause = false;
                        }
                        continue;
                    }

                    if (!first)
                    {
                        deltaTime = (Time.realtimeSinceStartup - lastMidiTimePlayAS) * 1000d;

                        if (deltaTime < delayMilliSeconde)
                        {
                            yield return -1;
                            continue;
                        }
                        timeMidiFromStartPlay += deltaTime;
                    }
                    else
                    {
                        timeMidiFromStartPlay = fromPosition;
                        first = false;
                    }

                    lastMidiTimePlayAS = Time.realtimeSinceStartup;

                    //Debug.Log("---------------- " /*+ timeFromStartPlay */+ "   deltaTime:" + Math.Round(deltaTime, 3) /*+ "   " + System.DateTime.UtcNow.Millisecond*/);
                    midiLoaded.calculateTickPlayer((int)timeMidiFromStartPlay);
                    bool ok = true;
#if MPTK_PRO
                    ok = CheckBeatEvent((int)timeMidiFromStartPlay);
                    ok = midiLoaded.CheckInnerLoop(((MidiFilePlayer)this).MPTK_InnerLoop);
                    //if (!CheckBeatEvent((int)timeMidiFromStartPlay))
                    //    return;
                    //if (!midiLoaded.CheckInnerLoop(((MidiFilePlayer)this).MPTK_InnerLoop))
                    //return;
#endif
                    // Read midi events until this time
                    List<MPTKEvent> midievents = null;
                    if (ok) midievents = midiLoaded.fluid_player_callback((int)timeMidiFromStartPlay, IdSession);

                    if (midiLoaded.EndMidiEvent || replayMidi || stopMidi /*|| (toPosition > 0 && toPosition > fromPosition && MPTK_Position > toPosition)*/)
                    {
                        break;
                    }

                    // Play notes read from the midi file
                    if (midievents != null && midievents.Count > 0)
                    {
                        // Call event with these midi events
                        try
                        {
                            if (OnEventNotesMidi != null)
                                OnEventNotesMidi.Invoke(midievents);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError("OnEventNotesMidi: exception detected. Check the callback code");
                            Debug.LogException(ex);
                        }

                        float beforePLay = Time.realtimeSinceStartup;
                        //Debug.Log("---------------- play count:" + midievents.Count);
                        if (MPTK_DirectSendToPlayer)
                        {
                            foreach (MPTKEvent midievent in midievents)
                            {
                                MPTK_PlayDirectEvent(midievent, false);
                            }
                        }
                        //Debug.Log("---------------- played count:" + midievents.Count + " Start:" + timeFromStartPlay + " Delta:" + Math.Round(deltaTime, 3) + " Elapsed:" + Math.Round((Time.realtimeSinceStartup - beforePLay) * 1000f,3));
                    }

                    if (Application.isEditor)
                    {
                        TimeSpan times = TimeSpan.FromMilliseconds(MPTK_Position);
                        playTimeEditorModeOnly = string.Format("{0:00}:{1:00}:{2:00}:{3:000}", times.Hours, times.Minutes, times.Seconds, times.Milliseconds);
                        durationEditorModeOnly = string.Format("{0:00}:{1:00}:{2:00}:{3:000}", MPTK_Duration.Hours, MPTK_Duration.Minutes, MPTK_Duration.Seconds, MPTK_Duration.Milliseconds);
                    }

                    if (delayMilliSeconde > 0)
                        yield return Routine.WaitForSeconds(delayMilliSeconde / 1000F);
                    else
                        yield return -1;


                }
                while (true);
            }
            else
                Debug.LogWarning("MidiFilePlayer/ThreadPlay - MIDI Load error");

            midiIsPlaying = false;

            try
            {
                EventEndMidiEnum reason = EventEndMidiEnum.MidiEnd;
                if (nextMidi)
                {
                    reason = EventEndMidiEnum.Next;
                    nextMidi = false;
                }
                else if (prevMidi)
                {
                    reason = EventEndMidiEnum.Previous;
                    prevMidi = false;
                }
                else if (stopMidi)
                    reason = EventEndMidiEnum.ApiStop;
                else if (replayMidi)
                    reason = EventEndMidiEnum.Replay;

                try
                {
                    OnEventEndPlayMidi.Invoke(currentMidiName, reason);
                }
                catch (Exception ex)
                {
                    Debug.LogError("OnEventEndPlayMidi: exception detected. Check the callback code");
                    Debug.LogException(ex);
                }

                if ((MPTK_MidiAutoRestart || replayMidi) && !stopMidi)
                    MPTK_Play(alreadyLoaded);
                //stopMidiToPlay = false;
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            //Debug.Log("Stop play");
        }

        public IEnumerator<float> ThreadCorePlay(byte[] midiBytesToPlay = null, float fromPosition = 0, float toPosition = 0, bool alreadyLoaded = false)
        {
            StartPlaying();
            string currentMidiName = MPTK_MidiName;
            if (alreadyLoaded)
            {
                //Debug.Log("Start playing same loaded MIDI");
            }
            else
            {
                //Debug.Log("Start play " + fromPosition + " " + toPosition);
                try
                {

                    // No midi byte array, try to load from MidiFilesDB from resource
                    if (midiBytesToPlay == null || midiBytesToPlay.Length == 0)
                    {
                        TextAsset mididata = Resources.Load<TextAsset>(System.IO.Path.Combine(MidiPlayerGlobal.MidiFilesDB, currentMidiName));
                        if (mididata != null)
                            midiBytesToPlay = mididata.bytes;
                    }

                    if (midiBytesToPlay != null && midiBytesToPlay.Length != 0)
                    {
                        midiLoaded = new MidiLoad();
                        midiLoaded.MPTK_KeepNoteOff = MPTK_KeepNoteOff;
                        midiLoaded.MPTK_KeepEndTrack = MPTK_KeepEndTrack;
                        midiLoaded.MPTK_LogLoadEvents = MPTK_LogLoadEvents;
                        midiLoaded.MPTK_EnableChangeTempo = MPTK_EnableChangeTempo;
                        midiLoaded.MPTK_ExtendedText = MPTK_ExtendedText;
                        if (!midiLoaded.MPTK_Load(midiBytesToPlay))
                            midiLoaded = null;
#if MPTK_PRO
                        MPTK_InnerLoop.Clear();
#endif
                    }
#if DEBUG_START_MIDI
                Debug.Log("After load midi " + (double)watchStartMidi.ElapsedTicks / ((double)System.Diagnostics.Stopwatch.Frequency / 1000d));
#endif
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }
            }
            if (Application.isPlaying)
                Routine.RunCoroutine(ThreadInternalMidiPlaying(currentMidiName, fromPosition, toPosition, alreadyLoaded).CancelWith(gameObject), Segment.RealtimeUpdate);
            else
                Routine.RunCoroutine(ThreadInternalMidiPlaying(currentMidiName, fromPosition, toPosition, alreadyLoaded), Segment.EditorUpdate);
            yield return 0;
        }

        protected void StartPlaying()
        {
#if DEBUG_START_MIDI
            System.Diagnostics.Stopwatch watchStartMidi = new System.Diagnostics.Stopwatch();
            watchStartMidi.Start();
#endif
            midiIsPlaying = true;
            stopMidi = false;
            replayMidi = false;
            needDelayToStop = false;
        }

        // if timePosition is true, fromPosition and toPosition are used (if > 0)
        // else fromTick and toTick are used (if > 0)
        // MIDI must be already loaded
        protected IEnumerator<float> ThreadInternalMidiPlaying(string currentMidiName, float fromPosition = 0, float toPosition = 0, bool alreadyLoaded = false)
        {
            if (midiLoaded != null && midiLoaded.MPTK_MidiEvents != null && midiLoaded.MPTK_MidiEvents.Count != 0)
            {
                // Clear all sound from a previous midi - v2.71 wait until all notes are stopped
                // V2.84 yield return Timing.WaitUntilDone(Timing.RunCoroutine(ThreadClearAllSound(true)), false);
                //Timing.RunCoroutine(ThreadClearAllSound(true));
                // V2.84
                if (Application.isPlaying)
                    Routine.RunCoroutine(ThreadClearAllSound(true, IdSession), Segment.RealtimeUpdate);
                else
                    Routine.RunCoroutine(ThreadClearAllSound(true, IdSession), Segment.EditorUpdate);

#if DEBUG_START_MIDI
                Debug.Log("After clear sound " +(double)watchStartMidi.ElapsedTicks / ((double)System.Diagnostics.Stopwatch.Frequency / 1000d));
#endif
                try
                {
                    midiLoaded.ChangeSpeed(MPTK_Speed);
                    midiLoaded.ChangeQuantization(MPTK_Quantization);

                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }

                //SetSpatialization();
                //SetAttenuationOnDistance();

                MPTK_ResetStat();
                timeAtStartMidi = (System.DateTime.UtcNow.Ticks / 10000D);
                ResetMidiPlayer();
                if (MPTK_Channels.EnableResetChannel)
                    MPTK_Channels.ResetExtension();

                do
                {
                    if (midiLoaded.MPTK_TickStart > 0)
                        midiLoaded.TickSeek = midiLoaded.MPTK_TickStart;
                    else if (fromPosition > 0)
                        MPTK_Position = fromPosition;
                    else if (MPTK_StartPlayAtFirstNote && midiLoaded.MPTK_TickFirstNote > 0)
                        midiLoaded.TickSeek = midiLoaded.MPTK_TickFirstNote;

                    // Call Event StartPlayMidi - v2.71 move after the do
                    try
                    {
                        if (SpatialSynths != null)
                            // Send to the channel synth
                            foreach (MidiFilePlayer mfp in SpatialSynths)
                                if (mfp.OnEventStartPlayMidi != null)
                                    mfp.OnEventStartPlayMidi.Invoke(currentMidiName);

                        if (OnEventStartPlayMidi != null)
                            OnEventStartPlayMidi.Invoke(currentMidiName);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError("OnEventStartPlayMidi: exception detected. Check the callback code");
                        Debug.LogException(ex);
                    }

                    volumeStartStop = 1f;
                    IdSession++;
                    midiLoaded.ReadyToPlay = true;


#if DEBUG_START_MIDI
                    Debug.Log("Just before playing " + (double)watchStartMidi.ElapsedTicks / ((double)System.Diagnostics.Stopwatch.Frequency / 1000d));
#endif
                    do
                    {
                        midiLoaded.MPTK_KeepNoteOff = MPTK_KeepNoteOff;
                        midiLoaded.MPTK_KeepEndTrack = MPTK_KeepEndTrack;
                        midiLoaded.MPTK_LogLoadEvents = MPTK_LogLoadEvents;
                        midiLoaded.MPTK_EnableChangeTempo = MPTK_EnableChangeTempo;
                        midiLoaded.endPlayAtLastNote = MPTK_StopPlayOnLastNote;

                        if (MPTK_DistanceAttenuation)
                        {
                            distanceToListener = MidiPlayerGlobal.MPTK_DistanceToListener(this.transform);
                            bool pause = distanceToListener > MPTK_MaxDistance;
                            if (pause != distancePause)
                            {
                                if (MPTK_PauseOnMaxDistance)
                                {
                                    distancePause = pause;
                                    if (distancePause)
                                    {
                                        watchMidi.Stop();
                                    }
                                    else
                                        watchMidi.Start();
                                }
                            }
                        }


                        if (needDelayToStart && delayRampUpSecond > 0f)
                        {
                            float pct = (timeRampUpSecond - Time.realtimeSinceStartup) / delayRampUpSecond;
                            //Debug.Log($"{DateTime.UtcNow.ToLongTimeString()} {timeAtNeedToStopSecond - Time.realtimeSinceStartup} {delayNeedToStopSecond} {pct}");
                            if (pct > 0f)
                                volumeStartStop = 1 - pct; // pct start at 1 and go to 0, we need start to 0 to 1
                            else
                            {
                                needDelayToStart = false;
                            }
                        }

                        if (needDelayToStop)
                        {
                            float pct = (timeRampDnSecond - Time.realtimeSinceStartup) / delayRampDnSecond;
                            //Debug.Log($"{DateTime.UtcNow.ToLongTimeString()} {timeAtNeedToStopSecond - Time.realtimeSinceStartup} {delayNeedToStopSecond} {pct}");
                            if (pct > 0f)
                                volumeStartStop = pct; // pct start at 1 and go to 0
                            else
                            {
                                MPTK_Stop();
                            }
                        }

                        if (playPause || distancePause)
                        {
                            //Debug.Log("paused");
                            midiLoaded.ReadyToPlay = false;
                            sequencerPause = true;
                        }
                        else
                        {
                            midiLoaded.ReadyToPlay = true;
                            sequencerPause = false;
                        }

                        // 2.9.0 wait end of all notes before stop the MIDI player
                        if (midiLoaded.EndMidiEvent)
                        {
                            if (MPTK_ModeStopVoice == ModeStopPlay.StopNoWaiting ||
                                // 2.17.2 don't wait notes sustained
                                (MPTK_ModeStopVoice == ModeStopPlay.StopWhenAllVoicesReleased && MPTK_StatVoiceCountPlaying == MPTK_StatVoiceCountSustained) ||
                                (MPTK_ModeStopVoice == ModeStopPlay.StopWhenAllVoicesEnded && MPTK_StatVoiceCountActive <= 0))
                            {
                                //Debug.Log($"EndMidiEvent MPTK_ModeStopVoice:{MPTK_ModeStopVoice} MPTK_StatVoiceCountPlaying:{MPTK_StatVoiceCountPlaying} MPTK_StatVoiceCountActive:{MPTK_StatVoiceCountActive}");
                                DequeueMidiEvents();
                                midiLoaded.ReadyToPlay = false;
                                break;
                            }
                        }

                        if (replayMidi || stopMidi)
                        {
                            midiLoaded.ReadyToPlay = false;
                            break;
                        }

                        //if (timePosition && toPosition > 0 && toPosition > fromPosition && MPTK_Position > toPosition)
                        //{
                        //    midiLoaded.ReadyToPlay = false;
                        //    break;
                        //}

                        //if (!timePosition && toTick > 0 && toTick > fromTick && MPTK_TickCurrent > toTick)
                        //{
                        //    midiLoaded.ReadyToPlay = false;
                        //    break;
                        //}

                        DequeueMidiEvents();

                        if (Application.isEditor)
                        {
                            TimeSpan times = TimeSpan.FromMilliseconds(MPTK_Position);
                            playTimeEditorModeOnly = string.Format("{0:00}:{1:00}:{2:00}:{3:000}", times.Hours, times.Minutes, times.Seconds, times.Milliseconds);
                            durationEditorModeOnly = string.Format("{0:00}:{1:00}:{2:00}:{3:000}", MPTK_Duration.Hours, MPTK_Duration.Minutes, MPTK_Duration.Seconds, MPTK_Duration.Milliseconds);
                        }
                        yield return Routine.WaitForSeconds(delayMilliSeconde / 1000F);
                    }
                    while (true);

                    yield return Routine.WaitForSeconds(delayMilliSeconde / 1000F);
                    if (MPTK_MidiAutoRestart)
                    {
                        midiLoaded.EndMidiEvent = false;
                        midiLoaded.ClearMetaText();
                        //Debug.Log($"MPTK_TickCurrent:{MPTK_TickCurrent} TickSeek:{midiLoaded.TickSeek} TickFromTempoChange:{midiLoaded.TickFromTempoChange} MPTK_TickPlayer:{midiLoaded.MPTK_TickPlayer}");
                        ResetMidiPlayer();

                        try
                        {
                            if (OnEventEndPlayMidi != null)
                                OnEventEndPlayMidi.Invoke(currentMidiName, EventEndMidiEnum.Loop);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("OnEventEndPlayMidi: exception detected. Check the callback code");
                            Debug.LogException(ex);
                        }
                    }
                }
                while (MPTK_MidiAutoRestart && !stopMidi && !replayMidi);
            }
            else
                Debug.LogWarning("MidiFilePlayer/ThreadPlay - MIDI Load error");


            midiIsPlaying = false;
            try
            {
                EventEndMidiEnum reason = EventEndMidiEnum.MidiEnd;
                if (midiLoaded == null)
                {
                    reason = EventEndMidiEnum.MidiErr;
                    MPTK_StatusLastMidiLoaded = LoadingStatusMidiEnum.MidiFileInvalid;
                }
                else if (nextMidi)
                {
                    reason = EventEndMidiEnum.Next;
                    nextMidi = false;
                }
                else if (prevMidi)
                {
                    reason = EventEndMidiEnum.Previous;
                    prevMidi = false;
                }
                else if (stopMidi)
                    reason = EventEndMidiEnum.ApiStop;
                else if (replayMidi)
                    reason = EventEndMidiEnum.Replay;

                if (SpatialSynths != null)
                    // Send to the channel synth
                    foreach (MidiFilePlayer mfp in SpatialSynths)
                        try
                        {
                            if (mfp.OnEventEndPlayMidi != null)
                                mfp.OnEventEndPlayMidi.Invoke(currentMidiName, reason);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("OnEventEndPlayMidi: exception detected. Check the callback code");
                            Debug.LogException(ex);
                        }
                try
                {
                    if (OnEventEndPlayMidi != null)
                        OnEventEndPlayMidi.Invoke(currentMidiName, reason);
                }
                catch (Exception ex)
                {
                    Debug.LogError("OnEventEndPlayMidi: exception detected. Check the callback code");
                    Debug.LogException(ex);
                }

                if (replayMidi && !stopMidi) MPTK_Play(alreadyLoaded);
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        private void DequeueMidiEvents()
        {
            try
            {
                while (QueueMidiEvents != null && QueueMidiEvents.Count > 0)
                {
                    List<MPTKEvent> midievents = QueueMidiEvents.Dequeue();
                    if (midievents != null && midievents.Count > 0)
                    {
                        //Debug.Log(midievents[midievents.Count - 1].Tick);
#if MPTK_PRO
                        if (this is MidiSpatializer)
                        {
                            SpatialSendEvents(midievents);
                        }
                        else
#endif
                            // Send to the midi reader
                            if (OnEventNotesMidi != null)
                            {
                                try
                                {
                                    //Debug.Log($"OnEventNotesMidi tick:{midievents[midievents.Count - 1].Tick}");
                                    OnEventNotesMidi.Invoke(midievents);
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogError("OnEventNotesMidi: exception detected. Check your callback function.");
                                    Debug.LogException(ex);
                                }
                            }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        //! @endcond
    }
}

