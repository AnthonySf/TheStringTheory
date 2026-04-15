//#define MPTK_PRO
//#define DEBUG_MULTI
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using MidiPlayerTK;

namespace DemoMPTK
{
    public class TestMidiStream : MonoBehaviour
    {
        // MIDI standard is limited to 16 channels.
        // With MidiStream it's possible to extend channels number until 256
        // Warning: it's not possible to create MIDI file with channels number over 16.
        public const int MAX_CHANNEL = 32;

        // MPTK component able to play a stream of midi events
        // Add a MidiStreamPlayer Prefab to your game object and defined midiStreamPlayer in the inspector with this prefab.
        public MidiStreamPlayer midiStreamPlayer;

        public Vector2 scale = new Vector2(1f, 1f);
        public Vector2 pivotPoint; // =new Vector2(0, 0);

        public bool FoldOutLooping;
        public bool FoldOutChord;
        public bool FoldOutPauseResume;
        public bool FoldOutRealTimeMidiChange;
        public bool FoldOutRealTimeVoiceChange;
        public bool FoldOutEffectSoundFontDisplay;
        public bool FoldOutEffectUnityDisplay;
        public bool FoldOutProgressionGuide;

        public float widthIndent = 2.5f;

        [Range(0.05f, 10f)]
        public float LoopDelay = 1;

        [Range(-10f, 100f)]
        public float CurrentDuration;

        [Range(0f, 10f)]
        public float CurrentDelay = 0;

        [Header("Realtime voice parameters mode")]
        public bool RealtimeRelatif;

        [Header("")]
        public bool RandomNote, RandomDuration, RandomDelay;
        public bool DrumKit = false;
        public bool ChordPlay = false;
        public int ArpeggioPlayChord = 0;
        public int DelayPlayScale = 200;
        public int DelayPlayProgression = 500;
        public bool ChordLibPlay = false;
        public bool ScaleLibPlay = false;
        public bool ProgressionLibPlay = false;
        public int CurrentChord;

        [Range(0f, 5000f)]
        public float TransitionDurationPauseResumeMS = 200f;

        [Range(0, 127)]
        public int StartLoopingNote = 50;

        [Range(0, 127)]
        public int EndLoopingNote = 60;

        [Range(0, 127)]
        public int CurrentVelocity = 100;

        [Range(0, 16)]
        public int StreamChannel = 0;

        [Range(0, 16)]
        public int DrumChannel = 9; // by convention the channel 10 is used for playing drum (value = 9 because channel start from channel 0 in Maestro)

        [Range(0, 127)]
        public int CurrentNote;

        [Range(0, 127)]
        public int StartLoopPreset = 0;

        [Range(0, 127)]
        public int EndLoopPreset = 127;

        [Range(0, 127)]
        public int CurrentPreset;

        [Range(0, 127)]
        public int CurrentBank;

        [Range(0, 127)]
        public int CurrentBankDrum;

        [Range(0, 127)]
        public int CurrentPatchDrum;

        [Range(0, 127)]
        public int PanChange = 64;

        [Range(0, 127)]
        public int ModulationChange;

        [Range(0, 1)]
        public float PitchChange = DEFAULT_PITCH;

        [Range(0, 127)]
        public int ExpressionChange = 127; // default value

        [Range(0, 127)]
        public int AttenuationChange = 100; // default value

        const float DEFAULT_PITCH = 0.5f; // 8192;

        [Range(0, 24)]
        public int PitchSensibility = 2;

        private float currentVelocityPitch;
        private float LastTimePitchChange;

        public int CountNoteToPlay = 1;
        public int CountNoteChord = 3;
        public int DegreeChord = 1;
        public int CurrentScale = 0;
        public int CurrentProgression = 0;

        /// <summary>@brief
        /// Current note playing
        /// </summary>
        private MPTKEvent NotePlaying;

        private float LastTimeChange;

        /// <summary>@brief
        /// Popup to select an instrument
        /// </summary>
        private PopupListItem PopPatchInstrument;
        private PopupListItem PopBankInstrument;
        private PopupListItem PopPatchDrum;
        private PopupListItem PopBankDrum;

        // Popup to select a realtime generator
        private PopupListItem[] PopGenerator;
        private int[] indexGenerator;
        private string[] labelGenerator;
        private float[] valueGenerator;
        private const int nbrGenerator = 4;

        // Manage skin
        public CustomStyle myStyle;

        private Vector2 scrollerWindow = Vector2.zero;
        //private int buttonLargeWidth = 500;
        private int buttonWidth = 250;
        private int buttonSmallWidth = 166;
        private int buttonTinyWidth = 100;
        private float spaceVertical = 0;
        private float widthFirstCol = 100;
        public bool IsplayingLoopNotes;
        public bool IsplayingLoopPresets;

#if MPTK_PRO
        MPTKChordBuilder ChordPlaying;
        MPTKChordBuilder ChordLibPlaying;
        List<MPTKChordBuilder> ProgressionPlaying;
        MPTKChordBuilder ProgressionHeldChord;
        int ProgressionHeldStep = -1;
        string ProgressionHeldPresetId;
        Coroutine ProgressionTask;
#endif

        private void Awake()
        {
            if (midiStreamPlayer != null)
            {
                // Warning: depending on the starting orders of the GameObjects, 
                //          this event may be missed if MidiStreamPlayer prefab start method is started before TestMidiStream.
                //          It's recommended to define this event in the prefab MidiStreamPlayer
                //          from the Unity editor inspector. See "On Event Synth Awake".tab. 
                // StartLoadingSynth will be called just before the initialization of the synthesizer.
                midiStreamPlayer.OnEventSynthAwake.AddListener(StartLoadingSynth);

                // EndLoadingSynth will be called when the synthesizer is ready.
                midiStreamPlayer.OnEventSynthStarted.AddListener(EndLoadingSynth);
            }
            else
                Debug.LogWarning("midiStreamPlayer is not defined. Check in Unity editor inspector of this gameComponent");
        }

        // Use this for initialization
        void Start()
        {
            //Debug.Log(Application.consoleLogPath);
            // Warning: when defined by script, this event is not triggered at first load of MPTK 
            // because MidiPlayerGlobal is loaded before any other gamecomponent
            // To be done in Start event (not Awake)
            MidiPlayerGlobal.OnEventPresetLoaded.AddListener(EndLoadingSF);

            // Define popup to display to select preset and bank
            PopBankInstrument = new PopupListItem() { Title = "Select A Bank", OnSelect = PopupBankPatchChanged, Tag = "BANK_INST", ColCount = 5, ColWidth = 150, };
            PopPatchInstrument = new PopupListItem() { Title = "Select A Patch", OnSelect = PopupBankPatchChanged, Tag = "PATCH_INST", ColCount = 5, ColWidth = 150, };
            PopBankDrum = new PopupListItem() { Title = "Select A Bank", OnSelect = PopupBankPatchChanged, Tag = "BANK_DRUM", ColCount = 5, ColWidth = 150, };
            PopPatchDrum = new PopupListItem() { Title = "Select A Patch", OnSelect = PopupBankPatchChanged, Tag = "PATCH_DRUM", ColCount = 5, ColWidth = 150, };

            GenModifier.InitListGenerator();
            indexGenerator = new int[nbrGenerator];
            labelGenerator = new string[nbrGenerator];
            valueGenerator = new float[nbrGenerator];
            PopGenerator = new PopupListItem[nbrGenerator];
            for (int i = 0; i < nbrGenerator; i++)
            {
                indexGenerator[i] = GenModifier.RealTimeGenerator[0].Index;
                labelGenerator[i] = GenModifier.RealTimeGenerator[0].Label;
                if (indexGenerator[i] >= 0)
                    valueGenerator[i] = RealtimeRelatif ? 0f : GenModifier.DefaultNormalizedVal((fluid_gen_type)indexGenerator[i]) * 100f;
                PopGenerator[i] = new PopupListItem() { Title = "Select A Generator", OnSelect = PopupGeneratorChanged, Tag = i, ColCount = 3, ColWidth = 250, };
            }
            LastTimeChange = Time.realtimeSinceStartup;
            CurrentNote = StartLoopingNote;
            LastTimeChange = -9999999f;
            PitchChange = DEFAULT_PITCH;
            CountNoteToPlay = 1;

        }

        // disabled
        void xxxOnApplicationFocus(bool hasFocus)
        {
            Debug.Log("TestMidiStream OnApplicationFocus " + hasFocus);
            if (!hasFocus)
            {
                midiStreamPlayer.MPTK_StopSynth();
                ///midiStreamPlayer.CoreAudioSource.enabled = false;
                midiStreamPlayer.CoreAudioSource.Stop();
            }
            else
            {
                //midiStreamPlayer.CoreAudioSource.enabled = true;
                midiStreamPlayer.CoreAudioSource.Play();
                midiStreamPlayer.MPTK_InitSynth();
            }
        }

        /// <summary>@brief
        /// The call of this method is defined in MidiPlayerGlobal from the Unity editor inspector. 
        /// The method is called when SoundFont is loaded. We use it only to statistics purpose.
        /// </summary>
        public void EndLoadingSF()
        {
            Debug.Log("End loading SoundFont. Statistics: ");

            //Debug.Log("List of presets available");
            //foreach (MPTKListItem preset in MidiPlayerGlobal.MPTK_ListPreset)
            //    Debug.Log($"   [{preset.Index,3:000}] - {preset.Label}");

            Debug.Log("   Time To Load SoundFont: " + Math.Round(MidiPlayerGlobal.MPTK_TimeToLoadSoundFont.TotalSeconds, 3).ToString() + " second");
            Debug.Log("   Time To Load Samples: " + Math.Round(MidiPlayerGlobal.MPTK_TimeToLoadWave.TotalSeconds, 3).ToString() + " second");
            Debug.Log("   Presets Loaded: " + MidiPlayerGlobal.MPTK_CountPresetLoaded);
            Debug.Log("   Samples Loaded: " + MidiPlayerGlobal.MPTK_CountWaveLoaded);
        }

        public void StartLoadingSynth(string name)
        {
            Debug.Log($"Start loading Synth {name}");
        }

        //! [ExampleOnEventEndLoadingSynth]

        /// <summary>@brief
        /// This method is called when the synthesizer is ready if you have defined OnEventSynthStarted or defined the event from the inspector in Unity.
        /// This is a good place to set certain synth parameters as defined by channel. 
        /// /// </summary>
        /// <param name="name"></param>
        public void EndLoadingSynth(string name)
        {
            Debug.Log($"EndLoadingSynth - Synth {name} loaded, now change bank and preset");

            if (MAX_CHANNEL != 16)
                // MIDI standard is limited to 16 channels.
                // With MidiStream it's possible to extend channels number until 256.
                // Warning: it's not possible to create MIDI file with channels number over 16.
                midiStreamPlayer.MPTK_InitSynth(MAX_CHANNEL);

            // Mandatory for updating UI list but not for playing sample.
            // The default instrument and drum banks are defined with the popup "SoundFont Setup Alt-F" in the Unity editor.
            // This method can be used by script to change the instrument bank and build presets available for it: MPTK_ListPreset.
            midiStreamPlayer.MPTK_SoundFont.SelectBankInstrument(CurrentBank);

            // Don't forget to initialize your MidiStreamPlayer variable, see link below:
            // https://paxstellar.fr/api-mptk-v2/#DefinedVariablePrefab

            // Channel 0: set Piano (if SoundFont is GeneralUser GS v1.471)
            // Define bank with CurrentBank (value defined in inspector to 0).
            midiStreamPlayer.MPTK_PlayEvent(new MPTKEvent() { Command = MPTKCommand.ControlChange, Controller = MPTKController.BankSelectMsb, Value = CurrentBank, Channel = StreamChannel, });
            Debug.LogFormat($"   Bank '{CurrentBank}' defined on channel {StreamChannel}");

            //! [ExampleUsingChannelAPI_3]
            // Defined preset with CurrentPreset (value defined in inspector to 0).
            midiStreamPlayer.MPTK_Channels[StreamChannel].PresetNum = CurrentPreset;
            Debug.LogFormat($"   Preset '{midiStreamPlayer.MPTK_Channels[StreamChannel].PresetName}' defined on channel {StreamChannel}");
            //! [ExampleUsingChannelAPI_3]

            // Playing a preset from another bank in the channel 1

            // Channel 1: set Laser Gun (if SoundFont is GeneralUser GS v1.471)
            // TBD int channel = 1, bank = 2, preset = 127;
            //midiStreamPlayer.MPTK_PlayEvent(new MPTKEvent() { Command = MPTKCommand.ControlChange, Controller = MPTKController.BankSelectMsb, Value = bank, Channel = channel, });
            //midiStreamPlayer.MPTK_ChannelPresetChange(channel, preset);
            //// MPTK_GetPatchName mandatory for getting the patch nane when the bank is not the default bank.
            // Before v2.10.1 Debug.LogFormat($"   Preset '{MidiPlayerGlobal.MPTK_GetPatchName(bank, preset)}' defined on channel {channel} and bank {bank}");
        }

        //! [ExampleOnEventEndLoadingSynth]

        [Header("Test MPTK_ChannelPresetChange for changing preset")]
        public bool Test_MPTK_ChannelPresetChange = false;

        /// <summary>@brief
        /// Two method are available for changing preset and bank : 
        ///         MPTK_ChannelPresetChange(channel, preset, bank)
        ///     or standard MIDI 
        ///         // change bank
        ///         MPTK_PlayEvent(new MPTKEvent() { Command = MPTKCommand.ControlChange, Controller = MPTKController.BankSelectMsb, Value = index, Channel = StreamChannel, });
        ///         // change preset in the current bank
        ///         MPTK_PlayEvent(new MPTKEvent() { Command = MPTKCommand.PatchChange, Value = index, Channel = StreamChannel, });
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="index"></param>
        /// <param name="indexList"></param>
        private void PopupBankPatchChanged(object tag, int index, int indexList)
        {
            //Debug.Log($"Bank or Patch Change {tag} {index} {indexList}");

            switch ((string)tag)
            {
                case "BANK_INST":
                    CurrentBank = index;
                    // This method build the preset list for the selected bank.
                    // This call doesn't change the MIDI bank used to play an instrument.
                    midiStreamPlayer.MPTK_SoundFont.SelectBankInstrument(index);
                    if (Test_MPTK_ChannelPresetChange)
                    {
                        // Before v2.10.1
                        // Change the bank number but not the preset, we need to retrieve the current preset for this channel
                        // int currentPresetInst = midiStreamPlayer.MPTK_ChannelPresetGetIndex(StreamChannel);
                        // Change the bank but not the preset. Return false if the preset is not found.
                        // ret = midiStreamPlayer.MPTK_ChannelPresetChange(StreamChannel, currentPresetInst, index);

                        // From v2.10.1
                        midiStreamPlayer.MPTK_Channels[StreamChannel].BankNum = index;
                    }
                    else
                        // Change bank with the standard MIDI message
                        midiStreamPlayer.MPTK_PlayEvent(new MPTKEvent() { Command = MPTKCommand.ControlChange, Controller = MPTKController.BankSelectMsb, Value = index, Channel = StreamChannel, });

                    Debug.Log($"Instrument Bank change - channel:{StreamChannel} bank:{midiStreamPlayer.MPTK_Channels[StreamChannel].BankNum} preset:{midiStreamPlayer.MPTK_Channels[StreamChannel].PresetNum}");
                    break;

                case "PATCH_INST":
                    CurrentPreset = index;
                    if (Test_MPTK_ChannelPresetChange)
                    {
                        // Before v2.10.1
                        // Change the preset number but not the bank. Return false if the preset is not found.
                        // ret = midiStreamPlayer.MPTK_ChannelPresetChange(StreamChannel, index);

                        // From v2.10.1
                        midiStreamPlayer.MPTK_Channels[StreamChannel].PresetNum = index;
                    }
                    else
                        midiStreamPlayer.MPTK_PlayEvent(new MPTKEvent() { Command = MPTKCommand.PatchChange, Value = index, Channel = StreamChannel, });

                    Debug.Log($"Instrument Preset change - channel:{StreamChannel} bank:{midiStreamPlayer.MPTK_Channels[StreamChannel].BankNum} preset:{midiStreamPlayer.MPTK_Channels[StreamChannel].PresetNum}");
                    break;

                case "BANK_DRUM":
                    // This method build the preset list for the selected bank.
                    // This call doesn't change the MIDI bank used to play an instrument.
                    CurrentBankDrum = index;
                    midiStreamPlayer.MPTK_SoundFont.SelectBankDrum(index);
                    if (Test_MPTK_ChannelPresetChange)
                        // From v2.10.1
                        midiStreamPlayer.MPTK_Channels[DrumChannel].BankNum = index;
                    else
                        // Change bank with the standard MIDI message
                        midiStreamPlayer.MPTK_PlayEvent(new MPTKEvent() { Command = MPTKCommand.ControlChange, Controller = MPTKController.BankSelectMsb, Value = index, Channel = DrumChannel, });

                    Debug.Log($"Drum Bank change - channel:{DrumChannel} bank:{midiStreamPlayer.MPTK_Channels[DrumChannel].BankNum} preset:{midiStreamPlayer.MPTK_Channels[DrumChannel].PresetNum}");
                    break;

                case "PATCH_DRUM":
                    CurrentPatchDrum = index;
                    if (Test_MPTK_ChannelPresetChange)
                        // From v2.10.1
                        midiStreamPlayer.MPTK_Channels[DrumChannel].PresetNum = index;
                    else
                        midiStreamPlayer.MPTK_PlayEvent(new MPTKEvent() { Command = MPTKCommand.PatchChange, Value = index, Channel = DrumChannel });

                    Debug.Log($"Drum Preset change - channel:{DrumChannel} bank:{midiStreamPlayer.MPTK_Channels[DrumChannel].BankNum} preset:{midiStreamPlayer.MPTK_Channels[DrumChannel].PresetNum}");
                    break;
            }
        }

        private void PopupGeneratorChanged(object tag, int index, int indexList)
        {
            int iGenerator = Convert.ToInt32(tag);
            indexGenerator[iGenerator] = index;
            labelGenerator[iGenerator] = GenModifier.RealTimeGenerator[indexList].Label;
            valueGenerator[iGenerator] = RealtimeRelatif ? 0f : GenModifier.DefaultNormalizedVal((fluid_gen_type)indexGenerator[iGenerator]) * 100f;
            Debug.Log($"indexList:{indexList} indexGenerator:{indexGenerator[iGenerator]} valueGenerator:{valueGenerator[iGenerator]} {labelGenerator[iGenerator]}");
        }

        void OnGUI()
        {

            Vector3 scale = HelperDemo.GUIScale();

            if (myStyle == null) { myStyle = new CustomStyle(); HelperDemo.myStyle = myStyle; }

            // midiStreamPlayer must be defined with the inspector of this gameObject 
            // Otherwise  you could use : midiStreamPlayer fp = FindFirstObjectByType<MidiStreamPlayer>(); in the Start() method
            if (midiStreamPlayer != null)
            {

                // +25 to avoid useless HScroll
                scrollerWindow = GUILayout.BeginScrollView(scrollerWindow, false, false, GUILayout.Width(Screen.width / scale.x), GUILayout.Height(Screen.height / scale.y));

                HelperDemo.GUI_Horizontal(HelperDemo.Zone.INIT);
                HelperDemo.GUI_Vertical(HelperDemo.Zone.INIT);


                // If need, display the popup  before any other UI to avoid trigger it hidden
                //if (HelperDemo.CheckSFExists() || midiStreamPlayer.sfLocal != null)
                if (midiStreamPlayer.MPTK_SoundFont.SoundFont != null)
                {
                    {
                        PopBankInstrument.Draw(midiStreamPlayer.MPTK_SoundFont.ListBank, CurrentBank, myStyle);
                        PopPatchInstrument.Draw(midiStreamPlayer.MPTK_SoundFont.ListPreset, CurrentPreset, myStyle);
                        PopBankDrum.Draw(midiStreamPlayer.MPTK_SoundFont.ListBank, CurrentBankDrum, myStyle);
                        PopPatchDrum.Draw(midiStreamPlayer.MPTK_SoundFont.ListPresetDrum, CurrentPatchDrum, myStyle);
                    }
                    //else
                    //{
                    //    PopBankInstrument.Draw(MidiPlayerGlobal.MPTK_ListBank, CurrentBank, myStyle);
                    //    PopPatchInstrument.Draw(MidiPlayerGlobal.MPTK_ListPreset, CurrentPreset, myStyle);
                    //    PopBankDrum.Draw(MidiPlayerGlobal.MPTK_ListBank, MidiPlayerGlobal.ImSFCurrent.DrumKitBankNumber, myStyle);
                    //    PopPatchDrum.Draw(MidiPlayerGlobal.MPTK_ListPresetDrum, CurrentPatchDrum, myStyle);

                    //}
                    for (int i = 0; i < nbrGenerator; i++)
                        PopGenerator[i].Draw(GenModifier.RealTimeGenerator, indexGenerator[i], myStyle);

                    MainMenu.Display("Test MIDI Stream - A very simple Generated Music Stream ", myStyle, Screen.width / scale.x, "https://paxstellar.fr/midi-file-player-detailed-view-2-2/");

                    // Display soundfont available and select a new one
                    GUISelectSoundFont.Display(scrollerWindow, myStyle, Screen.width / scale.x);

                    HelperDemo.GUI_Vertical(HelperDemo.Zone.BEGIN, myStyle.BacgDemosMedium);

                    try
                    {
                        //
                        // Select bank & Patch for Instrument
                        // ----------------------------------
                        OnGUI_SelectBankAndPatchForInstrument();

                        //
                        // Select bank & Patch for Drum
                        // ----------------------------
                        OnGUI_SelectBankAndPatchForDrum();

                        //
                        // Change bank or preset with free value with MPTK_ChannelPresetChange
                        // -------------------------------------------------------------------
                        OnGUI_FreeBankAndPreset();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Error in  Select bank & Patch {ex}");
                        HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);
                    }
                    finally
                    {
                        HelperDemo.GUI_Vertical(HelperDemo.Zone.END);
                    }
                }
                GUILayout.Space(spaceVertical);

                //
                // Display info and synth stats
                // ----------------------------
                HelperDemo.GUI_Vertical(HelperDemo.Zone.BEGIN, myStyle.BacgDemosMedium);
                HelperDemo.DisplayInfoSynth(midiStreamPlayer, 500, myStyle);

                GUILayout.Space(spaceVertical);

                HelperDemo.GUI_Vertical(HelperDemo.Zone.BEGIN, myStyle.BacgDemosMedium); // global

                //
                // Play one note 
                // --------------
                OnGUI_PlayNote();

                //
                // Pause / resume voices
                // --------------
                FoldOutPauseResume = GUILayout.Toggle(FoldOutPauseResume, "Pause and Resume Voices (Pro)");
                if (FoldOutPauseResume)
                    OnGUI_PauseResumeVoices();

                //
                // Play note loop and preset loop
                // ------------------------------
                FoldOutLooping = GUILayout.Toggle(FoldOutLooping, "Looping and Random");
                if (FoldOutLooping)
                    OnGUI_LoopingAndRandom();
#if DEBUG_MULTI
                                HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN);
                                GUILayout.Label(" ", myStyle.TitleLabel3, GUILayout.Width(widthFirstCol));
                                CountNoteToPlay = (int)Slider("Play Multiple Notes", CountNoteToPlay, 1, 200, false, 70);
                                HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);
                                GUILayout.Space(spaceVertical);
#endif

                //
                // Build chord and scale (Pro)
                // ---------------------------
                FoldOutChord = GUILayout.Toggle(FoldOutChord, "Build Chords, Scales and Progression (Pro)");
                if (FoldOutChord)
                    OnGUI_BuildChordScaleProgression();

                //
                // Change value from Midi Command
                // ------------------------------
                FoldOutRealTimeMidiChange = GUILayout.Toggle(FoldOutRealTimeMidiChange, "MIDI Controller and Pitch Change");
                if (FoldOutRealTimeMidiChange)
                    OnGUI_MidiControllerChange();

                //
                // Change value from Generator Synth
                // ---------------------------------
                FoldOutRealTimeVoiceChange = GUILayout.Toggle(FoldOutRealTimeVoiceChange, "Real-time Voice Parameters Change (Pro)");
                if (FoldOutRealTimeVoiceChange)
                    OnGUI_RealTimeVoiceParametersChange();

                //
                // Enable Unity effects
                // ------------------------
                FoldOutEffectSoundFontDisplay = GUILayout.Toggle(FoldOutEffectSoundFontDisplay, "Enable SoundFont Effects");
                if (FoldOutEffectSoundFontDisplay)
                    HelperDemo.GUI_EffectSoundFont(widthIndent, midiStreamPlayer.MPTK_EffectSoundFont);

                //
                // Enable Unity effects
                // ------------------------
                FoldOutEffectUnityDisplay = GUILayout.Toggle(FoldOutEffectUnityDisplay, "Enable Unity Effects");
                if (FoldOutEffectUnityDisplay)
#if MPTK_PRO
                    HelperDemo.GUI_EffectUnity(widthIndent, midiStreamPlayer.MPTK_EffectUnity);
#else
                    HelperDemo.GUI_EffectUnity(widthIndent);
#endif

                GUILayout.Space(spaceVertical);
                HelperDemo.GUI_Vertical(HelperDemo.Zone.END); // global

                // Display footer
                // --------------
                if (Application.isEditor)
                {
                    HelperDemo.GUI_Vertical(HelperDemo.Zone.BEGIN, myStyle.BacgDemosMedium);
                    HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN);
                    if (!string.IsNullOrEmpty(Application.consoleLogPath))
                    {
                        //if (GUILayout.Button("Clear Log ")) Debug.ClearDeveloperConsole();
                        if (GUILayout.Button("Open Folder " + System.IO.Path.GetDirectoryName(Application.consoleLogPath))) Application.OpenURL("file://" + System.IO.Path.GetDirectoryName(Application.consoleLogPath));
                        if (GUILayout.Button("Open Log File")) Application.OpenURL("file://" + Application.consoleLogPath);
                    }
                    //else
                    //    GUILayout.Label("current platform does not support log files");
                    HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

                    GUILayout.Label("Go to your Hierarchy, select GameObject MidiStreamPlayer: inspector contains a lot of parameters to control the sound.", myStyle.TitleLabel2);
                    HelperDemo.GUI_Vertical(HelperDemo.Zone.END);
                }
                else
                {
                    // blues en C minor: C,D#,F,F#,G,A#
                    HelperDemo.GUI_Vertical(HelperDemo.Zone.BEGIN, myStyle.BacgDemosLight);
                    GUILayout.Label("Play blues notes C minor: C, D#, F, F#, G, A# from numeric keys on your top keyboard. Change preset with arrow up/down keys.", myStyle.TitleLabel2);
                    HelperDemo.GUI_Vertical(HelperDemo.Zone.END);
                }

                HelperDemo.GUI_Horizontal(HelperDemo.Zone.CLEAN);
                HelperDemo.GUI_Vertical(HelperDemo.Zone.CLEAN);
                GUILayout.EndScrollView();
            }
            else
            {
                GUILayout.Space(spaceVertical);
                GUILayout.Label("MidiStreamPlayer not defined, check hierarchy.", myStyle.TitleLabel3);
            }

        }

        private void CloseAllPopup()
        {
            PopBankInstrument.Show = false;
            PopPatchInstrument.Show = false;
            PopBankDrum.Show = false;
            PopPatchDrum.Show = false;
        }

        private void OnGUI_SelectBankAndPatchForInstrument()
        {
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN);
            GUILayout.Label("Instrument", myStyle.TitleLabel3, GUILayout.Width(widthFirstCol));

            // Open the popup to select a bank
            if (GUILayout.Button(MidiPlayerGlobal.ImSFCurrent.DefaultBankNumber + " - Bank", GUILayout.Width(buttonWidth)))
            {
                PopPatchInstrument.Show = false;
                PopBankDrum.Show = false;
                PopPatchDrum.Show = false;
                PopBankInstrument.Show = !PopBankInstrument.Show;
            }
            PopBankInstrument.PositionWithScroll(ref scrollerWindow);

            // Open the popup to select an instrument
            if (GUILayout.Button(CurrentPreset.ToString() + " - " + MidiPlayerGlobal.MPTK_GetPatchName(MidiPlayerGlobal.ImSFCurrent.DefaultBankNumber, CurrentPreset), GUILayout.Width(buttonWidth)))
            {
                PopBankInstrument.Show = false;
                PopBankDrum.Show = false;
                PopPatchDrum.Show = false;
                PopPatchInstrument.Show = !PopPatchInstrument.Show;
            }
            // disable, list too long
            //PopPatchInstrument.PositionWithScroll(ref scrollerWindow);

            int channel = (int)HelperDemo.GUI_Slider("Channel", StreamChannel, 0, MAX_CHANNEL - 1, alignCaptionRight: true, enableButton: true, widthLabelValue: 20, widthCaption: 70, widthSlider: 100);
            if (channel != StreamChannel)
            {
                StreamChannel = channel;
                Debug.Log($"Change to channel:{StreamChannel}");
                Debug.Log($"        bank:   {midiStreamPlayer.MPTK_Channels[StreamChannel].BankNum}");
                Debug.Log($"        preset: {midiStreamPlayer.MPTK_Channels[StreamChannel].PresetNum}");
                Debug.Log($"        name:   '{midiStreamPlayer.MPTK_Channels[StreamChannel].PresetName}'");
            }
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);
        }

        private void OnGUI_SelectBankAndPatchForDrum()
        {
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN);
            GUILayout.Label("Drum", myStyle.TitleLabel3, GUILayout.Width(widthFirstCol));

            // Open the popup to select a bank for drum
            if (GUILayout.Button(MidiPlayerGlobal.ImSFCurrent.DrumKitBankNumber + " - Bank", GUILayout.Width(buttonWidth)))
            {
                PopBankInstrument.Show = false;
                PopPatchInstrument.Show = false;
                PopPatchDrum.Show = false;
                PopBankDrum.Show = !PopBankDrum.Show;
            }
            PopBankDrum.PositionWithScroll(ref scrollerWindow);

            // Open the popup to select an instrument for drum
            if (GUILayout.Button(
                CurrentPatchDrum.ToString() + " - " +
                MidiPlayerGlobal.MPTK_GetPatchName(MidiPlayerGlobal.ImSFCurrent.DrumKitBankNumber, CurrentPatchDrum),
                GUILayout.Width(buttonWidth)))
            {
                PopBankInstrument.Show = false;
                PopPatchInstrument.Show = false;
                PopBankDrum.Show = false;
                PopPatchDrum.Show = !PopPatchDrum.Show;
            }
            PopPatchDrum.PositionWithScroll(ref scrollerWindow);

            GUILayout.Label(" ", GUILayout.Width(11));

            bool newDrumKit = GUILayout.Toggle(DrumKit, "Activate Drum Mode   ");
            if (newDrumKit != DrumKit)
            {
                DrumKit = newDrumKit;
                // Set channel to dedicated drum channel 9 
                StreamChannel = DrumKit ? 9 : 0;
            }

            midiStreamPlayer.MPTK_LogEvents = GUILayout.Toggle(midiStreamPlayer.MPTK_LogEvents, "Log MIDI Events");
            GUILayout.FlexibleSpace();

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);
        }


        private void OnGUI_FreeBankAndPreset()
        {
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, style: null, GUILayout.Width(350));

            // Change bank or preset with free value with MPTK_ChannelPresetChange
            // -------------------------------------------------------------------

            // Before v2.10.1

            //// Select any value in the range 0 and 16383 for the bank 
            //int bank = (int)Slider("Free Bank", midiStreamPlayer.MPTK_ChannelBankGetIndex(StreamChannel), 0, 128 * 128 - 1, alignright: false, wiLab: 80, wiSlider: 200, wiLabelValue: 100);
            //// Select any value in the range 0 and 127 for the preset
            //int prst = (int)Slider("Free Preset", midiStreamPlayer.MPTK_ChannelPresetGetIndex(StreamChannel), 0, 127, alignright: false, wiLab: 80, wiSlider: 200);
            //// If user made change for bank or preset ...
            //if (bank != midiStreamPlayer.MPTK_ChannelBankGetIndex(StreamChannel) ||
            //    prst != midiStreamPlayer.MPTK_ChannelPresetGetIndex(StreamChannel))
            //{
            //    // ... apply the change to the MidiStreamPlayer for the current channel.
            //    // If the bank or the preset doestn't exist 
            //    //      - the method returns false 
            //    //      - the bank and preset are still registered in the channel
            //    //      - when a note-on is received on this channel, the first preset of the first bank is used to play (usually piano).
            //    bool ret = midiStreamPlayer.MPTK_ChannelPresetChange(StreamChannel, prst, bank);

            //    // Read the current bank, preset and preset name selected
            //    int newbank = midiStreamPlayer.MPTK_ChannelBankGetIndex(StreamChannel);
            //    int newpreset = midiStreamPlayer.MPTK_ChannelPresetGetIndex(StreamChannel);
            //    string newname = midiStreamPlayer.MPTK_ChannelPresetGetName(StreamChannel);
            //    Debug.Log($"MPTK_ChannelPresetChange result:{ret} bank:{newbank} preset:{newpreset} '{newname}'");
            //}

            // After v2.10.1
            //! [ExampleUsingChannelAPI_4]

            // Select any value in the range 0 and 16383 for the bank 
            int newBank = (int)HelperDemo.GUI_Slider("Free Bank", midiStreamPlayer.MPTK_Channels[StreamChannel].BankNum, 0, 128 * 128 - 1, alignCaptionRight: false, widthCaption: 100, widthSlider: 115, widthLabelValue: 30);
            // Select any value in the range 0 and 127 for the preset
            int newPreset = (int)HelperDemo.GUI_Slider("Free Preset", midiStreamPlayer.MPTK_Channels[StreamChannel].PresetNum, 0, 127, alignCaptionRight: true, widthCaption: 100, widthSlider: 115, widthLabelValue: 30);
            // If user made change for bank or preset ...
            if (newBank != midiStreamPlayer.MPTK_Channels[StreamChannel].BankNum ||
                newPreset != midiStreamPlayer.MPTK_Channels[StreamChannel].PresetNum)
            {
                // ... apply the change to the MidiStreamPlayer for the current channel.
                // If the bank or the preset doestn't exist 
                //      - the method returns false 
                //      - the bank and preset are still registered in the channel
                //      - when a note-on is received on this channel, the first preset of the first bank is used to play (usually piano).
                midiStreamPlayer.MPTK_Channels[StreamChannel].BankNum = newBank;
                if (midiStreamPlayer.MPTK_Channels[StreamChannel].BankNum != newBank)
                    Debug.Log($"Bank {newBank} not set ");
                else
                    Debug.Log($"Bank set to:{newBank}");

                midiStreamPlayer.MPTK_Channels[StreamChannel].PresetNum = newPreset;
                if (midiStreamPlayer.MPTK_Channels[StreamChannel].PresetNum != newPreset)
                    Debug.Log($"Preset {newPreset} not set ");
                else
                    Debug.Log($"Preset set to:{newPreset}");

                // Read the current preset name 
                Debug.Log($"Current preset name  '{midiStreamPlayer.MPTK_Channels[StreamChannel].PresetName}'");
            }
            //! [ExampleUsingChannelAPI_4]


            //GUILayout.Label("If preset not found, The first found is selected i", myStyle.TitleLabel3);
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);
        }
        private void OnGUI_RealTimeVoiceParametersChange()
        {
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, myStyle.BacgDemosLight);

            HelperDemo.GUI_Indent(widthIndent);

            HelperDemo.GUI_Vertical(HelperDemo.Zone.BEGIN, myStyle.BacgDemosLight, GUILayout.Width(widthIndent), GUILayout.ExpandWidth(false));

            //GUILayout.Label("Real Time Voice Parameters Change [Available with MPTK Pro].", myStyle.TitleLabel3);
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN);
            if (GUILayout.Button("Play indefinitely", myStyle.BtStandard, GUILayout.Width(buttonSmallWidth)))
            {
                CurrentDuration = -1;
                // Stop current note if playing
                MaestroStopOneNote();
                // Play one note 
                MaestroPlayOneNote();
            }
            if (GUILayout.Button("Stop", myStyle.BtStandard, GUILayout.Width(buttonSmallWidth)))
            {
                MaestroStopOneNote();
            }
            if (GUILayout.Button("Restore default value", myStyle.BtStandard, GUILayout.Width(buttonSmallWidth)))
            {
                for (int i = 0; i < nbrGenerator; i++)
                {
                    if (indexGenerator[i] >= 0)
                        valueGenerator[i] = RealtimeRelatif ? 0f : GenModifier.DefaultNormalizedVal((fluid_gen_type)indexGenerator[i]) * 100f;
                }
#if MPTK_PRO
                for (int i = 0; i < nbrGenerator; i++)
                    if (NotePlaying != null)
                    {
                        NotePlaying.ModifySynthParameter((fluid_gen_type)indexGenerator[i], 0f, MPTKModeGeneratorChange.Restaure);
                    }
#endif
            }
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);


            float gene;
            for (int i = 0; i < nbrGenerator; i += 2) // 2 generators per line
            {
                HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, style: null, GUILayout.Width(650));

                for (int j = 0; j < 2; j++) // 2 generators per line
                {
                    int numGenerator = i + j;
                    // Open the popup to select an instrument
                    if (GUILayout.Button(indexGenerator[numGenerator] + " - " + labelGenerator[numGenerator], GUILayout.Width(buttonWidth)))
                        PopGenerator[numGenerator].Show = !PopGenerator[numGenerator].Show;

                    // Get real time value
                    gene = HelperDemo.GUI_Slider("Value", valueGenerator[numGenerator], RealtimeRelatif ? -100f : 0f, 100f, true, true, 50f, 80f);
                    if (indexGenerator[numGenerator] >= 0)
                    {
#if MPTK_PRO
                        if (NotePlaying != null)
                        {
                            float currentValue = NotePlaying.GetSynthParameterCurrentValue((fluid_gen_type)indexGenerator[numGenerator]);
                            if (currentValue >= 0 && valueGenerator[numGenerator] != gene)
                                Debug.Log($"{numGenerator} {indexGenerator[numGenerator]} {labelGenerator[numGenerator]} {currentValue}");
                        }

                        // If value is different then applied to the current note playing
                        if (valueGenerator[numGenerator] != gene && NotePlaying != null)
                        {
                            NotePlaying.ModifySynthParameter(
                                (fluid_gen_type)indexGenerator[numGenerator],
                                valueGenerator[numGenerator] / 100f,
                                RealtimeRelatif ? MPTKModeGeneratorChange.Reinforce : MPTKModeGeneratorChange.Override);

                            //if ((fluid_gen_type)indexGenerator[numGenerator] == fluid_gen_type.GEN_VOLENVATTACK)
                            //{
                            //Debug.Log($"GEN_VOLENVATTACK default {NotePlaying.GetSynthParameterDefaultValue(fluid_gen_type.GEN_VOLENVATTACK)} {valueGenerator[numGenerator]}");
                            //}
                        }
                        //MPTKEvent mptkEvent = new MPTKEvent()
                        //{
                        //    Command = MPTKCommand.NoteOn, // midi command
                        //    Value = 50, // from 0 to 127, 48 for C3, 60 for C4, ...
                        //    Channel = 0, // from 0 to 15, 9 reserved for drum
                        //    Duration = -1, // note duration in millisecond, -1 to play indefinitely, MPTK_StopEvent to stop
                        //    Velocity = 100, // from 0 to 127, sound can vary depending on the velocity
                        //    Delay = 0, // delay in millisecond before playing the note
                        //};
                        //mptkEvent.ModifySynthParameter(fluid_gen_type.GEN_FILTERFC, 0.5f, MPTKModeGeneratorChange.Override);
                        //midiStreamPlayer.MPTK_PlayEvent(mptkEvent);   


#endif
                        valueGenerator[numGenerator] = gene;
                    }
                    GUILayout.Label(" ", myStyle.TitleLabel3, GUILayout.Width(60));
                }

                HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

            }
#if MPTK_PRO
#else
            GUILayout.Label("Available with Maestro MPTK Pro.", myStyle.TitleLabel3);
#endif

            HelperDemo.GUI_Vertical(HelperDemo.Zone.END);

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);
        }

        private void OnGUI_PlayNote()
        {
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, style: null, GUILayout.Width(350));
            float heightButton = 30;
            if (GUILayout.Button("Play", myStyle.BtStandard, GUILayout.Width(buttonSmallWidth), GUILayout.Height(heightButton)))
            {
                // Stop current note if playing
                // Was stupid! No need.
                // If MPTK_ReleaseSameNote is true (default value)
                // Playing the same noe on the same channel will stop the previous note
                // MaestroStopOneNote();

                // Play one note 
                MaestroPlayOneNote();
            }

            if (GUILayout.Button("Stop", myStyle.BtStandard, GUILayout.Width(buttonSmallWidth), GUILayout.Height(heightButton)))
            {
                MaestroStopOneNote();
#if MPTK_PRO
                MaestroStopChord();
#endif
            }

            if (GUILayout.Button("Clear", myStyle.BtStandard, GUILayout.Width(buttonSmallWidth), GUILayout.Height(heightButton)))
            {
                midiStreamPlayer.MPTK_ClearAllSound(true);
            }

            if (GUILayout.Button("Reset", myStyle.BtStandard, GUILayout.Width(buttonSmallWidth), GUILayout.Height(heightButton)))
            {
                midiStreamPlayer.MPTK_InitSynth();
                StreamChannel = 0; DrumChannel = 9;
                CurrentPreset = CurrentPatchDrum = CurrentBank = 0;
                CurrentBankDrum = 128; // only for GS compliant soundfont
                CurrentDuration = 1; CurrentDelay = 0; CurrentNote = 50; CurrentVelocity = 100;
                LoopDelay = 1; StartLoopingNote = 50; EndLoopingNote = 60; StartLoopPreset = 0; EndLoopPreset = 127;
                RealtimeRelatif = FoldOutLooping = FoldOutChord = FoldOutRealTimeMidiChange = FoldOutRealTimeVoiceChange = false;
                FoldOutEffectUnityDisplay = FoldOutEffectSoundFontDisplay = FoldOutProgressionGuide = false;
                RandomNote = RandomDelay = RandomDuration = DrumKit = ChordPlay = ChordLibPlay = ScaleLibPlay = ProgressionLibPlay = false;
                widthIndent = 2.5f;
                CountNoteToPlay = 1; CountNoteChord = 3; DegreeChord = 1; CurrentScale = CurrentProgression = 0; CurrentChord = ArpeggioPlayChord = 0; DelayPlayScale = 200; DelayPlayProgression = 500;
                PanChange = 64; ModulationChange = 0; PitchChange = DEFAULT_PITCH; ExpressionChange = 127; AttenuationChange = 100; PitchSensibility = 2;
#if MPTK_PRO
                StopHeldProgressionChord();
#endif
                CurrentProgression = 0;
            }

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, style: null, GUILayout.Width(500));

            //if (GUILayout.Button("Test", myStyle.BtStandard, GUILayout.Width(buttonWidth * 0.666f)))
            //{
            //    //midiStreamPlayer.MPTK_KillByExclusiveClass = false;

            //    NotePlaying = new MPTKEvent() { Command = MPTKCommand.NoteOn, Value = 36, Channel = 9, Duration = 1000, Velocity = 10, };// Bass_drum channel 9
            //    midiStreamPlayer.MPTK_PlayEvent(NotePlaying);

            //    NotePlaying = new MPTKEvent() { Command = MPTKCommand.NoteOn, Value = 42, Channel = 9, Duration = 1000, Velocity = 80, };// Closed Hihat channel 9 
            //    midiStreamPlayer.MPTK_PlayEvent(NotePlaying);
            //}

            CurrentNote = (int)HelperDemo.GUI_Slider($"Note {HelperNoteLabel.LabelFromMidi(CurrentNote)}", CurrentNote, 0, 127, widthCaption: 70);
            CurrentVelocity = (int)HelperDemo.GUI_Slider("Velocity", (int)CurrentVelocity, 0f, 127f, alignCaptionRight: true, widthCaption: 70);
            CurrentDuration = HelperDemo.GUI_Slider("Duration", CurrentDuration, -1f, 10f, alignCaptionRight: true, valueButton: 0.1f, widthCaption: 70);
            CurrentDelay = HelperDemo.GUI_Slider("Delay", CurrentDelay, 0f, 5f, alignCaptionRight: true, valueButton: 0.1f, widthCaption: 50);

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);
        }

        private void OnGUI_PauseResumeVoices()
        {
            float heightButton = 30;

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, myStyle.BacgDemosLight);
            HelperDemo.GUI_Indent(widthIndent);
            HelperDemo.GUI_Vertical(HelperDemo.Zone.BEGIN, myStyle.BacgDemosLight, GUILayout.Width(widthIndent), GUILayout.ExpandWidth(false));

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, style: null, GUILayout.Width(500));
#if MPTK_PRO
            if (GUILayout.Button("Pause", myStyle.BtStandard, GUILayout.Width(buttonTinyWidth), GUILayout.Height(heightButton)))
                midiStreamPlayer.MPTK_PauseVoices(TransitionDurationPauseResumeMS);

            if (GUILayout.Button("Resume", myStyle.BtStandard, GUILayout.Width(buttonTinyWidth), GUILayout.Height(heightButton)))
                midiStreamPlayer.MPTK_ResumeVoices(TransitionDurationPauseResumeMS);

            TransitionDurationPauseResumeMS = Mathf.RoundToInt(HelperDemo.GUI_Slider("Transition Duration (ms)", Mathf.RoundToInt(TransitionDurationPauseResumeMS),
                0f, 5000f, true, valueButton: 10, widthCaption: 200, widthLabelValue: 50));
#else
            
            GUI.enabled = false;
            GUILayout.Button("Pause (MPTK Pro)", myStyle.BtStandard, GUILayout.Width(buttonSmallWidth), GUILayout.Height(heightButton));
            GUILayout.Button("Resume (MPTK Pro)", myStyle.BtStandard, GUILayout.Width(buttonSmallWidth), GUILayout.Height(heightButton));
            GUI.enabled = true;
#endif

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

            HelperDemo.GUI_Vertical(HelperDemo.Zone.END);
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);
        }


        private void OnGUI_LoopingAndRandom()
        {
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, myStyle.BacgDemosLight);

            HelperDemo.GUI_Indent(widthIndent);

            HelperDemo.GUI_Vertical(HelperDemo.Zone.BEGIN, myStyle.BacgDemosLight, GUILayout.Width(widthIndent), GUILayout.ExpandWidth(false));

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, style: null, GUILayout.Width(500));
            //GUILayout.Label("Loop on Notes and Presets", myStyle.TitleLabel3, GUILayout.Width(220));
            LoopDelay = HelperDemo.GUI_Slider("Loop Delay (s)", LoopDelay, 0.01f, 10f, valueButton: 0.1f, widthCaption: 120, widthSlider: 100);
            GUILayout.Space(20f);
            RandomNote = GUILayout.Toggle(RandomNote, "Random Notes", GUILayout.Width(120));
            RandomDuration = GUILayout.Toggle(RandomDuration, "Random Duration", GUILayout.Width(120));
            RandomDelay = GUILayout.Toggle(RandomDelay, "Random Delay", GUILayout.Width(120));

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, style: null, GUILayout.Width(350));
            GUILayout.Label("Loop Notes", myStyle.TitleLabel3, GUILayout.Width(widthFirstCol));
            if (GUILayout.Button("Start / Stop", IsplayingLoopNotes ? myStyle.BtSelected : myStyle.BtStandard, GUILayout.Width(buttonSmallWidth))) IsplayingLoopNotes = !IsplayingLoopNotes;
            StartLoopingNote = (int)HelperDemo.GUI_Slider("From", StartLoopingNote, 0, 127, true);
            EndLoopingNote = (int)HelperDemo.GUI_Slider("To", EndLoopingNote, 0, 127, true);
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, style: null, GUILayout.Width(350));
            GUILayout.Label("Loop Presets", myStyle.TitleLabel3, GUILayout.Width(widthFirstCol));
            if (GUILayout.Button("Start / Stop", IsplayingLoopPresets ? myStyle.BtSelected : myStyle.BtStandard, GUILayout.Width(buttonSmallWidth))) IsplayingLoopPresets = !IsplayingLoopPresets;
            StartLoopPreset = (int)HelperDemo.GUI_Slider("From", StartLoopPreset, 0, 127, true);
            EndLoopPreset = (int)HelperDemo.GUI_Slider("To", EndLoopPreset, 0, 127, true);
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

            HelperDemo.GUI_Vertical(HelperDemo.Zone.END);

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);
        }


        private void OnGUI_MidiControllerChange()
        {
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, myStyle.BacgDemosLight);

            HelperDemo.GUI_Indent(widthIndent);

            HelperDemo.GUI_Vertical(HelperDemo.Zone.BEGIN, style: myStyle.BacgDemosLight);

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, style: null, GUILayout.Width(350));
            // Change pitch (automatic return to center as a physical keyboard!)
            // 0 is the lowest bend positions(default is 2 semitones), 
            // 0.5 centered value, the sounding notes aren't being transposed up or down,
            // 1 is the highest pitch bend position (default is 2 semitones)
            float pitchChange = HelperDemo.GUI_Slider("Pitch Change", PitchChange, 0, 1, false);
            if (pitchChange != PitchChange)
            {
                LastTimePitchChange = Time.realtimeSinceStartup;
                PitchChange = pitchChange;
#if MPTK_PRO
                midiStreamPlayer.MPTK_PlayPitchWheelChange(StreamChannel, PitchChange);
#else
                Debug.Log("Pitch change: Pro only");
#endif
            }

            // midi pitch sensitivity change for all notes on the channel.
            // Pitch change sensitivity from 0 to 24 semitones up and down. Default value 2.
            // Example: 4, means semitones range is from -4 to 4 when MPTK_PlayPitchWheelChange change from 0 to 1.
            int pitchSensi = (int)HelperDemo.GUI_Slider("Sensibility", PitchSensibility, 0, 24, true);
            if (pitchSensi != PitchSensibility)
            {
                PitchSensibility = pitchSensi;
#if MPTK_PRO
                midiStreamPlayer.MPTK_PlayPitchWheelSensitivity(StreamChannel, PitchSensibility);
#else
                Debug.Log("Pitch sensitivity change: Pro only");
#endif
            }

            midiStreamPlayer.MPTK_Transpose = (int)HelperDemo.GUI_Slider("Transpose", midiStreamPlayer.MPTK_Transpose, -24, 24, true);

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, style: null, GUILayout.Width(350));

            // Change volume
            midiStreamPlayer.MPTK_Volume = HelperDemo.GUI_Slider("Global Volume", midiStreamPlayer.MPTK_Volume, 0f, Constant.MAX_VOLUME);

            // Change velocity of the note: what force is applied on the key. Change volume and sound of the note.
            //CurrentVelocity = (int)Slider("Velocity", (int)CurrentVelocity, 0f, 127f, true);

            // Change left / right stereo
            int panChange = (int)HelperDemo.GUI_Slider("Panoramic", PanChange, 0, 127, true);
            if (panChange != PanChange)
            {
                PanChange = panChange;
                midiStreamPlayer.MPTK_PlayEvent(new MPTKEvent()
                {
                    Command = MPTKCommand.ControlChange,
                    Controller = MPTKController.Pan,
                    Value = PanChange,
                    Channel = StreamChannel
                });
            }

            // Sustain pedal control (CC64).
            // The Sustain Pedal CC64 is one of the most common MIDI CC messages, used to hold played notes while the sustain pedal is active/depressed.
            //      Values of 0-63 indicate OFF.
            //      Values of 64-127 indicate ON.
            // https://anotherproducer.com/online-tools-for-musicians/midi-cc-list/
            //! [ExampleAccessToControler]
            GUILayout.Label("Sustain switch", myStyle.LabelRight, GUILayout.Width(120), GUILayout.Height(25));

            // Is sustain enabled on the current channel?
            bool sustain = midiStreamPlayer.MPTK_Channels[StreamChannel].Controller(MPTKController.Sustain) < 64 ? false : true;

            // Switch sustain state
            if (GUILayout.Button(sustain ? "Sustain On" : "Sustain Off", sustain ? myStyle.BtSelected : myStyle.BtStandard, GUILayout.Width(buttonTinyWidth)))
                // Send a MIDI event control change for sustain
                midiStreamPlayer.MPTK_PlayEvent(new MPTKEvent()
                {
                    Command = MPTKCommand.ControlChange,
                    Controller = MPTKController.Sustain,
                    Value = sustain ? 0 : 64, // enable / disable sustain
                    Channel = StreamChannel
                });
            //! [ExampleAccessToControler]

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

            // Change modulation. Often applied a vibrato, this effect is defined in the SoundFont 
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, style: null, GUILayout.Width(350));
            int modChange = (int)HelperDemo.GUI_Slider("Modulation", ModulationChange, 0, 127);
            if (modChange != ModulationChange)
            {
                ModulationChange = modChange;
                midiStreamPlayer.MPTK_PlayEvent(new MPTKEvent()
                {
                    Command = MPTKCommand.ControlChange,
                    Controller = MPTKController.Modulation,
                    Value = ModulationChange,
                    Channel = StreamChannel
                });
            }

            // Change modulation. Often applied volume, this effect is defined in the SoundFont 
            int expChange = (int)HelperDemo.GUI_Slider("Expression", ExpressionChange, 0, 127, true);
            if (expChange != ExpressionChange)
            {
                ExpressionChange = expChange;
                midiStreamPlayer.MPTK_PlayEvent(new MPTKEvent()
                {
                    Command = MPTKCommand.ControlChange,
                    Controller = MPTKController.Expression,
                    Value = ExpressionChange,
                    Channel = StreamChannel
                });
            }

            // Change modulation. Often applied volume, this effect is defined in the SoundFont 
            int expAttenuation = (int)HelperDemo.GUI_Slider("Attenuation", AttenuationChange, 0, 127, true);
            if (expAttenuation != AttenuationChange)
            {
                AttenuationChange = expAttenuation;
                midiStreamPlayer.MPTK_PlayEvent(new MPTKEvent()
                {
                    Command = MPTKCommand.ControlChange,
                    Controller = MPTKController.VOLUME_MSB,
                    Value = AttenuationChange,
                    Channel = StreamChannel
                });
            }
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);



            HelperDemo.GUI_Vertical(HelperDemo.Zone.END);

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);
        }

        /// <summary>
        /// Build Chord And Scale
        /// </summary>
        private void OnGUI_BuildChordScaleProgression()
        {
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, myStyle.BacgDemosLight);

            HelperDemo.GUI_Indent(widthIndent);

            HelperDemo.GUI_Vertical(HelperDemo.Zone.BEGIN, myStyle.BacgDemosLight, GUILayout.Width(widthIndent), GUILayout.ExpandWidth(false));
#if MPTK_PRO
#else
            GUILayout.Label("Available with Maestro MPTK Pro.", myStyle.TitleLabel3);
#endif

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN);

            if (!ChordPlay && !ChordLibPlay && !ScaleLibPlay && !ProgressionLibPlay) ChordPlay = true;

            ChordPlay = GUILayout.Toggle(ChordPlay, "Play Chord From Degree", GUILayout.Width(170));
            if (ChordPlay) { ChordLibPlay = false; ScaleLibPlay = false; ProgressionLibPlay = false; }

            ChordLibPlay = GUILayout.Toggle(ChordLibPlay, "Play Chord From Lib", GUILayout.Width(170));
            if (ChordLibPlay) { ChordPlay = false; ScaleLibPlay = false; ProgressionLibPlay = false; }

            ScaleLibPlay = GUILayout.Toggle(ScaleLibPlay, "Play Range From Lib", GUILayout.Width(170));
            if (ScaleLibPlay) { ChordPlay = false; ChordLibPlay = false; ProgressionLibPlay = false; }

            ProgressionLibPlay = GUILayout.Toggle(ProgressionLibPlay, "Play Progression From Lib", GUILayout.Width(190));
            if (ProgressionLibPlay) { ChordPlay = false; ChordLibPlay = false; ScaleLibPlay = false; }

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

            // Build a chord from degree
            if (ChordPlay)
                OnGUI_BuildChordFromDegree();

            // Build a chord from a library
            if (ChordLibPlay)
                OnGUI_BuildChordFromLibrary();

            if (ScaleLibPlay)
                OnGUI_BuildScaleFromLib();

            if (ProgressionLibPlay)
                OnGUI_BuildProgressionFromLib();

            HelperDemo.GUI_Vertical(HelperDemo.Zone.END);

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

        }

        /// <summary>
        /// Build Chord From degree
        /// </summary>
        private void OnGUI_BuildChordFromDegree()
        {
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, style: null, GUILayout.Width(600));

            int countNote = (int)HelperDemo.GUI_Slider("Count", CountNoteChord, 2, 50, alignCaptionRight: false, widthCaption: 100, widthSlider: 100, widthLabelValue: 30);
            //bool alignright = false, float wiLab = 100, float wiSlider = 100, float wiLabelValue = 30)
            if (countNote != CountNoteChord)
            {
                CountNoteChord = countNote;
                MaestroPlay(true);
            }

            int degreeChord = (int)HelperDemo.GUI_Slider("Degree", DegreeChord, 1, 7, alignCaptionRight: true, widthSlider: 100, widthCaption: 100, widthLabelValue: 30);
            if (degreeChord != DegreeChord)
            {
                DegreeChord = degreeChord;
                MaestroPlay(true);
            }

            ArpeggioPlayChord = (int)HelperDemo.GUI_Slider("Arpeggio (ms)", ArpeggioPlayChord, 0, 500, alignCaptionRight: true, widthSlider: 100, widthCaption: 100, widthLabelValue: 30);

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

            OnGUI_PlayScale();
        }

        /// <summary>
        /// Build Chord From Library
        /// </summary>
        private void OnGUI_BuildChordFromLibrary()
        {
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN);
#if MPTK_PRO

            int chord = (int)HelperDemo.GUI_Slider("Scale", CurrentChord, 0, MPTKChordLib.Chords.Count - 1, alignCaptionRight: false, widthSlider: 100, widthCaption: 100, widthLabelValue: 30);
            if (chord != CurrentChord)
            {
                CurrentChord = chord;
                MaestroPlay(true);
            }
            GUILayout.Label($"{MPTKChordLib.Chords[CurrentChord].Name}", myStyle.TitleLabel3);
            if (GUILayout.Button("Play", GUILayout.Width(50)))
                MaestroPlay(true);
#endif
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);
        }

        private void OnGUI_BuildScaleFromLib()
        {
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN);
            GUILayout.Label("From Lib", myStyle.TitleLabel3, GUILayout.Width(widthFirstCol));
            // Apply a delay on each chord notes
            DelayPlayScale = (int)HelperDemo.GUI_Slider("Delay (ms)", DelayPlayScale, 100, 1000, false, true, 70);
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);
            OnGUI_PlayScale();
        }

        private void OnGUI_BuildProgressionFromLib()
        {
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN);
#if MPTK_PRO
            if (MPTKChordProgressionLib.Presets.Count == 0)
            {
                GUILayout.Label("No progression preset found.", myStyle.TitleLabel3);
                HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);
                return;
            }

            int progression = (int)HelperDemo.GUI_Slider("Progression", CurrentProgression, 0, MPTKChordProgressionLib.Presets.Count - 1, 
                    alignCaptionRight: false, widthSlider: 100, widthCaption: 100, widthLabelValue: 30);
            if (progression != CurrentProgression)
            {
                CurrentProgression = progression;
                MaestroPlay(true);
            }
            MPTKChordProgressionPreset currentPreset = MPTKChordProgressionLib.Presets[CurrentProgression];

            GUILayout.Label($"   {currentPreset.Name}", myStyle.TitleLabel3, GUILayout.Width(150));

            if (GUILayout.Button("Play One Progression", GUILayout.Width(160)))
            {
                MaestroPlay(true);
            }
            if (ProgressionTask != null)
            {
                if (GUILayout.Button("Stop Progression", GUILayout.Width(160)))
                {
                    StopProgressionTask();
                }
            }


#else
            GUILayout.Label("Available with Maestro MPTK Pro.", myStyle.TitleLabel3);
#endif
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

#if MPTK_PRO
            if (MPTKChordProgressionLib.Presets.Count == 0)
                return;

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN);
            int tonic = (int)HelperDemo.GUI_Slider($"Tonic {HelperNoteLabel.LabelFromMidi(CurrentNote)}", CurrentNote, 0, 127, 
                    alignCaptionRight: false, widthCaption: 100, widthSlider: 100, widthLabelValue: 30);
            if (tonic != CurrentNote)
            {
                CurrentNote = tonic;
                MaestroPlay(true);
            }

            DelayPlayProgression = (int)HelperDemo.GUI_Slider("Delay Chords (ms)", DelayPlayProgression, 50, 1000, 
                    alignCaptionRight: true, widthCaption: 105, widthSlider: 100, widthLabelValue: 40);
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

            OnGUI_PlayProgressionChordsHold(currentPreset);

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN);
            GUILayout.Label($"Mode: {currentPreset.Mode}", myStyle.TitleLabel3, GUILayout.Width(135));
            GUILayout.Label($"Genre: {currentPreset.Genre}", myStyle.TitleLabel3, GUILayout.Width(150));
            GUILayout.Label($"Harmony: {currentPreset.HarmonyFlavor}", myStyle.TitleLabel3, GUILayout.Width(160));
            string moods = currentPreset.MoodTags != null ? string.Join(" / ", currentPreset.MoodTags) : string.Empty;
            GUILayout.Label($"Mood: {moods}", myStyle.TitleLabel3, GUILayout.Width(250));
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN);
            string degrees = currentPreset.Degrees != null ? string.Join(" - ", currentPreset.Degrees) : string.Empty;
            //GUILayout.Label($"Degrees: {degrees}", myStyle.TitleLabel3, GUILayout.Width(200));
            GUILayout.Label($"Intensity: {currentPreset.Intensity}/5", myStyle.TitleLabel3, GUILayout.Width(135));
            GUILayout.Label($"Harmonic Tension: {currentPreset.HarmonicTension}/5", myStyle.TitleLabel3, GUILayout.Width(150));
            GUILayout.Label($"Example: {currentPreset.Example}", myStyle.TitleLabel3, GUILayout.Width(200));
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

            FoldOutProgressionGuide = GUILayout.Toggle(FoldOutProgressionGuide, "Musical Guide (Progression)");
            if (FoldOutProgressionGuide)
            {
                List<string> harmonyGuide = BuildHarmonyGuide(currentPreset);

                // Darker panel + slight right offset for better readability.
                HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN, myStyle.BacgDemosMedium);
                GUILayout.Space(14f);
                HelperDemo.GUI_Vertical(HelperDemo.Zone.BEGIN, null, GUILayout.Width(700));
                GUILayout.Space(4f);

                for (int i = 0; i < harmonyGuide.Count; i++)
                    GUILayout.Label(harmonyGuide[i], myStyle.TitleLabel3, GUILayout.Width(682));

                HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN);
                GUILayout.Label("Learn more:", myStyle.TitleLabel3, GUILayout.Width(95));
                if (GUILayout.Button("Harmony Lessons", myStyle.BtStandard, GUILayout.Width(170)))
                    Application.OpenURL("https://www.musictheory.net/lessons");
                if (GUILayout.Button("Roman Numerals Guide", myStyle.BtStandard, GUILayout.Width(200)))
                    Application.OpenURL("https://viva.pressbooks.pub/openmusictheorycopy/chapter/roman-numerals/");
                HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

                GUILayout.Space(4f);
                HelperDemo.GUI_Vertical(HelperDemo.Zone.END);
                HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);
            }


#endif
        }

#if MPTK_PRO
        private static List<string> BuildHarmonyGuide(MPTKChordProgressionPreset preset)
        {
            string degreePath = preset.Degrees != null && preset.Degrees.Length > 0 ? string.Join(" -> ", preset.Degrees) : "(no degree sequence)";
            List<string> lines = new List<string>
            {
                "Think in motion: tension and release, not fixed formulas.",
                "Chord color:",
                "   uppercase roman numerals (I, IV, V) suggest major color,",
                "   lowercase roman numerals (i, iv, v) suggest minor color.",
                "Harmonic function: tonic = rest, pre-dominant = setup, dominant = pull.",
                $"Chords for this progression: {degreePath}"
            };

            switch (preset.Mode)
            {
                case MPTKTonalMode.Major:
                    lines.Add("Mode - Major: some chords feel settled, others prepare motion, and others create a clear pull toward resolution.");
                    break;
                case MPTKTonalMode.Minor:
                    lines.Add("Mode - Minor: listen to the contrast between darker home color and the chord that creates the strongest return.");
                    break;
                case MPTKTonalMode.Modal:
                    lines.Add("Mode - Modal: focus on characteristic color tones more than classical dominant-to-tonic cadence.");
                    break;
            }

            switch (preset.HarmonyFlavor)
            {
                case MPTKHarmonyFlavor.Diatonic:
                    lines.Add("Harmony - Diatonic: most chords come from one scale, so contrast is created mainly by function (rest, setup, pull) and chord order.");
                    break;
                case MPTKHarmonyFlavor.Borrowed:
                    lines.Add("Harmony - Borrowed-chord: when both IV and iv appear, hear iv as a color-shift from parallel minor.");
                    break;
                case MPTKHarmonyFlavor.JazzExtended:
                    lines.Add("Harmony - Jazz-extended: 7th chords shape direction through voice-leading; listen to top-note and 3rd/7th movement.");
                    break;
                case MPTKHarmonyFlavor.Functional:
                    lines.Add("Harmony - Functional: identify where dominant tension appears, then where resolution actually lands.");
                    break;
                case MPTKHarmonyFlavor.Modal:
                    lines.Add("Harmony - Phrygian: emphasize i <-> bII contrast; this semitone color is often the main expressive identity.");
                    break;
            }

            lines.Add("Try this workflow: hold each chord in order, choose your strongest tension point, then replay the full progression.");
            return lines;
        }

        private void OnGUI_PlayProgressionChordsHold(MPTKChordProgressionPreset preset)
        {
            List<MPTKResolvedChordStep> steps = MPTKChordProgressionLib.ResolveDegrees(preset.Degrees, CurrentNote);
            if (steps == null || steps.Count == 0)
            {
                StopHeldProgressionChord();
                return;
            }

            EventType evt = Event.current.type;
            bool canControlAudio =
                evt == EventType.Repaint ||
                evt == EventType.MouseDown ||
                evt == EventType.MouseUp;

            bool oneHeld = false;
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN);
            GUILayout.Label("Hold chord", myStyle.TitleLabel3, GUILayout.Width(135));
            for (int i = 0; i < steps.Count; i++)
            {
                // Get progression step name (I, V, vi, ...) from degree or use degree number if not defined
                string token = preset.Degrees != null && i < preset.Degrees.Length ? preset.Degrees[i] : (i + 1).ToString();

                bool isPlayingThis = ProgressionHeldChord != null && ProgressionHeldStep == i && string.Equals(ProgressionHeldPresetId, preset.Id, StringComparison.Ordinal);
                GUIStyle style = isPlayingThis ? myStyle.BtSelected : myStyle.BtStandard;
                bool held = GUILayout.RepeatButton(token, style, GUILayout.Width(70));
                if (held)
                {
                    oneHeld = true;
                    if (canControlAudio && !isPlayingThis)
                        StartHeldProgressionChord(preset, steps[i], i);
                }
            }
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);

            if (canControlAudio && !oneHeld)
                StopHeldProgressionChord();
        }
#endif


        /// <summary>@brief
        /// Common UI for building and playing a chord or a scale from the library of scale
        /// See in GUI "Play Chord From Degree" and "Play Range From Lib"
        /// </summary>
        private void OnGUI_PlayScale()
        {
            HelperDemo.GUI_Horizontal(HelperDemo.Zone.BEGIN);
#if MPTK_PRO
            int scale = (int)HelperDemo.GUI_Slider("Scale", CurrentScale, 0, MPTKScaleLib.RangeCount - 1, alignCaptionRight: false, widthSlider: 100, widthCaption: 100, widthLabelValue: 30);

            if (scale != CurrentScale)
            {
                CurrentScale = scale;
                // Select the range from a list of range. See file Resources/GeneratorTemplate/GammeDefinition.csv 
                midiStreamPlayer.MPTK_ScaleSelected = (MPTKScaleName)CurrentScale;
                MaestroPlay(true);
            }

            GUILayout.Label(midiStreamPlayer.MPTK_ScaleName, myStyle.TitleLabel3);

            // Button play for playing the current scale
            if (GUILayout.Button("Play", GUILayout.Width(50)))
                MaestroPlay(true);
#else
            GUILayout.Label("Available with Maestro MPTK Pro.", myStyle.TitleLabel3);
#endif

            HelperDemo.GUI_Horizontal(HelperDemo.Zone.END);
        }


        //! [ExampleFullMidiStream]

        private MPTKEvent[] eventsMidi;

        // blues en C minor: C,D#,F,F#,G,A# http://patrick.murris.com/musique/gammes_piano.htm?base=3&scale=0%2C3%2C5%2C6%2C7%2C10&octaves=1
        // for playing from the keyboard
        private int[] keysToNote = { 60, 63, 65, 66, 67, 70, 72, 75, 77 };

        // Update is called once per frame
        void Update()
        {

            // Check that SoundFont is loaded
            if (!midiStreamPlayer.MPTK_SoundFont.IsReady)
                return;

            //
            // Play note from the keyboard
            // ---------------------------

            // Better in Start(), here only for demo clarity
            if (eventsMidi == null)
                // Can play simultanesously 10 notes from keyboard
                eventsMidi = new MPTKEvent[10];

            // Check if key 1 to 9 is down (top alpha keyboard)
            for (int key = 0; key < 9; key++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + key))
                {
                    // Create a new note and play
                    eventsMidi[key] = new MPTKEvent()
                    {
                        Command = MPTKCommand.NoteOn,
                        Channel = StreamChannel, // From 0 to 15
                        Duration = -1, // Infinite, note-off when key is released, see bellow.
                        Value = keysToNote[key], // blues en C minor: C,D#,F,F#,G,A# http://patrick.murris.com/musique/gammes_piano.htm?base=3&scale=0%2C3%2C5%2C6%2C7%2C10&octaves=1
                        Velocity = 100
                    };

                    // Send the note-on MIDI event to the MIDI synth
                    midiStreamPlayer.MPTK_PlayEvent(eventsMidi[key]);
                }

                // If the note is active and the corresponding key is released then stop the note
                if (eventsMidi[key] != null && Input.GetKeyUp(KeyCode.Alpha1 + key))
                {
                    midiStreamPlayer.MPTK_StopEvent(eventsMidi[key]);
                    eventsMidi[key] = null;
                }
            }

            //
            // Change preset with arrow keys
            // -----------------------------
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (Input.GetKeyDown(KeyCode.DownArrow)) CurrentPreset--;
                if (Input.GetKeyDown(KeyCode.UpArrow)) CurrentPreset++;
                CurrentPreset = Mathf.Clamp(CurrentPreset, 0, 127);

                // Send the patch (other name for preset) change MIDI event to the MIDI synth
                midiStreamPlayer.MPTK_PlayEvent(new MPTKEvent()
                {
                    Command = MPTKCommand.PatchChange,
                    Value = CurrentPreset,   // From 0 to 127
                    Channel = StreamChannel, // From 0 to 15
                });
            }

#if MPTK_PRO
            //
            // Change pitch
            // ------------
            if (PitchChange != DEFAULT_PITCH)
            {
                // If user change the pitch, wait 1/2 second before returning to median value
                if (Time.realtimeSinceStartup - LastTimePitchChange > 0.5f)
                {
                    PitchChange = Mathf.SmoothDamp(PitchChange, DEFAULT_PITCH, ref currentVelocityPitch, 0.5f, 10000, Time.unscaledDeltaTime);
                    if (Mathf.Abs(PitchChange - DEFAULT_PITCH) < 0.001f)
                        PitchChange = DEFAULT_PITCH;

                    //Debug.Log("DEFAULT_PITCH " + DEFAULT_PITCH + " " + PitchChange + " " + currentVelocityPitch);
                    midiStreamPlayer.MPTK_PlayPitchWheelChange(StreamChannel, PitchChange);
                }
            }
#endif
            //
            // Loop playing on notes and presets
            // ---------------------------------
            if (midiStreamPlayer != null && (IsplayingLoopPresets || IsplayingLoopNotes))
            {
                float time = Time.realtimeSinceStartup - LastTimeChange;
                if (time > LoopDelay)
                {
                    // It's time to generate some notes ;-)
                    LastTimeChange = Time.realtimeSinceStartup;

                    for (int indexNote = 0; indexNote < CountNoteToPlay; indexNote++)
                    {
                        // Loop on preset
                        if (IsplayingLoopPresets)
                        {
                            if (++CurrentPreset > EndLoopPreset) CurrentPreset = StartLoopPreset;
                            if (CurrentPreset < StartLoopPreset) CurrentPreset = StartLoopPreset;

                            midiStreamPlayer.MPTK_PlayEvent(new MPTKEvent()
                            {
                                Command = MPTKCommand.PatchChange,
                                Value = CurrentPreset,
                                Channel = StreamChannel,
                            });
                        }

                        // Loop on note
                        if (IsplayingLoopNotes)
                        {
                            if (++CurrentNote > EndLoopingNote) CurrentNote = StartLoopingNote;
                            if (CurrentNote < StartLoopingNote) CurrentNote = StartLoopingNote;
                        }

                        // Play note or chord or scale without stopping the current (useful for performance test)
                        MaestroPlay(false);

                    }
                }
            }
        }

        /// <summary>@brief
        /// Play music depending the parameters set
        /// </summary>
        /// <param name="stopCurrent">stop current note playing</param>
        void MaestroPlay(bool stopCurrent)
        {
            if (RandomNote)
            {
                if (StartLoopingNote >= EndLoopingNote)
                    CurrentNote = StartLoopingNote;
                else
                    CurrentNote = UnityEngine.Random.Range(StartLoopingNote, EndLoopingNote);
            }
            if (RandomDuration)
            {
                CurrentDuration = UnityEngine.Random.Range(0.1f, 2f);
                if (!RandomDelay)
                    LoopDelay = CurrentDuration;
            }
            if (RandomDelay)
                LoopDelay = UnityEngine.Random.Range(0.01f, 2f);

#if MPTK_PRO
            if (FoldOutChord && (ChordPlay || ChordLibPlay || ScaleLibPlay || ProgressionLibPlay))
            {
                if (RandomNote)
                {
                    CountNoteChord = UnityEngine.Random.Range(3, 50);
                    DegreeChord = UnityEngine.Random.Range(1, 8);
                    CurrentChord = UnityEngine.Random.Range(StartLoopingNote, EndLoopingNote);
                    if (MPTKChordProgressionLib.Presets.Count > 0)
                        CurrentProgression = UnityEngine.Random.Range(0, MPTKChordProgressionLib.Presets.Count);
                }

                if (stopCurrent)
                    MaestroStopChord();

                if (ChordPlay)
                    MaestroPlayOneChord();

                if (ChordLibPlay)
                    MaestroPlayOneChordFromLib();

                if (ScaleLibPlay)
                    MaestroPlayScale();

                if (ProgressionLibPlay)
                    MaestroPlayProgressionFromLib();
            }
            else
#endif
            {
                if (stopCurrent)
                    MaestroStopOneNote();
                MaestroPlayOneNote();
            }
        }


#if MPTK_PRO
        /// <summary>@brief
        /// Play note from a scale
        /// </summary>
        private void MaestroPlayScale()
        {
            // get the current scale selected
            MPTKScaleLib scale = MPTKScaleLib.CreateScale((MPTKScaleName)CurrentScale, true);
            for (int ecart = 0; ecart < scale.Count; ecart++)
            {
                NotePlaying = new MPTKEvent()
                {
                    Command = MPTKCommand.NoteOn, // midi command
                    Value = CurrentNote + scale[ecart], // from 0 to 127, 48 for C4, 60 for C5, ...
                    Channel = StreamChannel, // from 0 to 15, 9 reserved for drum
                    Duration = DelayPlayScale, // note duration in millisecond, -1 to play indefinitely, MPTK_StopEvent to stop
                    Velocity = CurrentVelocity, // from 0 to 127, sound can vary depending on the velocity
                    Delay = ecart * DelayPlayScale, // delay in millisecond before playing the note. Use it yo play chord with Arpeggio
                };
                midiStreamPlayer.MPTK_PlayEvent(NotePlaying);
            }
        }

        private void MaestroPlayOneChord()
        {
            // Start playing a new chord and save in ChordPlaying to stop it later
            ChordPlaying = new MPTKChordBuilder(true)
            {
                // Parameters to build the chord
                Tonic = CurrentNote,
                Count = CountNoteChord,
                Degree = DegreeChord,

                // Midi Parameters how to play the chord
                Channel = StreamChannel,
                Arpeggio = ArpeggioPlayChord, // delay in milliseconds between each notes of the chord
                Duration = Convert.ToInt64(CurrentDuration * 1000f), // millisecond, -1 to play indefinitely
                Velocity = CurrentVelocity, // Sound can vary depending on the velocity
                Delay = Convert.ToInt64(CurrentDelay * 1000f),
            };
            //Debug.Log(DegreeChord);
            midiStreamPlayer.MPTK_PlayChordFromScale(ChordPlaying);
        }
        private void MaestroPlayOneChordFromLib()
        {
            // Start playing a new chord and save in ChordLibPlaying to stop it later
            ChordLibPlaying = new MPTKChordBuilder(true)
            {
                // Parameters to build the chord
                Tonic = CurrentNote,
                FromLib = CurrentChord,

                // Midi Parameters how to play the chord
                Channel = StreamChannel,
                Arpeggio = ArpeggioPlayChord, // delay in milliseconds between each notes of the chord
                Duration = Convert.ToInt64(CurrentDuration * 1000f), // millisecond, -1 to play indefinitely
                Velocity = CurrentVelocity, // Sound can vary depending on the velocity
                Delay = Convert.ToInt64(CurrentDelay * 1000f),
            };
            //Debug.Log(DegreeChord);
            midiStreamPlayer.MPTK_PlayChordFromLib(ChordLibPlaying);
        }

        private void MaestroPlayProgressionFromLib()
        {
            if (MPTKChordProgressionLib.Presets.Count == 0)
                return;

            CurrentProgression = Mathf.Clamp(CurrentProgression, 0, MPTKChordProgressionLib.Presets.Count - 1);
            MPTKChordProgressionPreset preset = MPTKChordProgressionLib.Presets[CurrentProgression];
            StopHeldProgressionChord();
            StopProgressionTask();
            ProgressionTask = StartCoroutine(PlayProgressionTask(preset));
        }

        private IEnumerator PlayProgressionTask(MPTKChordProgressionPreset preset)
        {
            ProgressionPlaying = new List<MPTKChordBuilder>();

            long duration = Convert.ToInt64(CurrentDuration * 1000f);
            float initialDelay = Mathf.Max(0f, CurrentDelay);

            if (initialDelay > 0f)
                yield return new WaitForSecondsRealtime(initialDelay);

            // Build chords list from progression
            List<MPTKResolvedChordStep> steps = MPTKChordProgressionLib.ResolveDegrees(preset.Degrees, CurrentNote);
            for (int i = 0; i < steps.Count; i++)
            {
                MPTKChordBuilder chord = new MPTKChordBuilder(true)
                {
                    Tonic = steps[i].TonicMidi,
                    FromLib = (int)steps[i].ChordName,
                    Channel = StreamChannel,
                    Velocity = CurrentVelocity,
                    Duration = duration,
                    Delay = 0,
                    Arpeggio = 0,
                };

                ProgressionPlaying.Add(chord);
            }

            // Play progression sequentially.
            foreach (MPTKChordBuilder chord in ProgressionPlaying)
            {
                midiStreamPlayer.MPTK_PlayChordFromLib(chord);
                yield return new WaitForSecondsRealtime(DelayPlayProgression / 1000f);
            }
            ProgressionTask = null;
        }

        private void StopProgressionTask()
        {
            if (ProgressionTask != null)
            {
                StopCoroutine(ProgressionTask);
                ProgressionTask = null;
            }
        }

        /// <summary>
        /// Build the chord from the progressio step and play it.
        /// </summary>
        /// <param name="preset"></param>
        /// <param name="step"></param>
        /// <param name="stepIndex"></param>
        private void StartHeldProgressionChord(MPTKChordProgressionPreset preset, MPTKResolvedChordStep step, int stepIndex)
        {
            StopHeldProgressionChord();

            ProgressionHeldChord = new MPTKChordBuilder(log:true)
            {
                Tonic = step.TonicMidi,
                FromLib = (int)step.ChordName,
                Channel = StreamChannel,
                Velocity = CurrentVelocity,
                Duration = -1,
                Delay = 0,
                Arpeggio = 0,
            };

            ProgressionHeldStep = stepIndex;
            ProgressionHeldPresetId = preset.Id;
            midiStreamPlayer.MPTK_PlayChordFromLib(ProgressionHeldChord);
        }

        private void StopHeldProgressionChord()
        {
            if (ProgressionHeldChord != null)
                midiStreamPlayer.MPTK_StopChord(ProgressionHeldChord);

            ProgressionHeldChord = null;
            ProgressionHeldStep = -1;
            ProgressionHeldPresetId = null;
        }

        private void MaestroStopChord()
        {
            StopProgressionTask();
            StopHeldProgressionChord();

            if (ChordPlaying != null)
                midiStreamPlayer.MPTK_StopChord(ChordPlaying);

            if (ChordLibPlaying != null)
                midiStreamPlayer.MPTK_StopChord(ChordLibPlaying);

            if (ProgressionPlaying != null)
                foreach (MPTKChordBuilder chord in ProgressionPlaying)
                    if (chord != null)
                        midiStreamPlayer.MPTK_StopChord(chord);

            ProgressionPlaying = null;

        }
#else
        private void PlayScale() { }
        private void PlayOneChord() { }
        private void PlayOneChordFromLib() { }
        private void StopChord() { }
#endif
        //! [ExampleMPTK_PlayEvent]
        /// <summary>@brief
        /// Send the note to the player. Notes are plays in a thread, so call returns immediately.
        /// The note is stopped automatically after the Duration defined.
        /// </summary>
        private void MaestroPlayOneNote()
        {
            //Debug.Log($"{StreamChannel} {midiStreamPlayer.MPTK_ChannelPresetGetName(StreamChannel)}");
            // Start playing a new note
            NotePlaying = new MPTKEvent()
            {
                Command = MPTKCommand.NoteOn,
                Value = CurrentNote, // note to played, ex 60=C5. Use the method from class HelperNoteLabel to convert to string
                Channel = StreamChannel,
                Duration = Convert.ToInt64(CurrentDuration * 1000f), // millisecond, -1 to play indefinitely
                Velocity = CurrentVelocity, // Sound can vary depending on the velocity
                Delay = Convert.ToInt64(CurrentDelay * 1000f),
            };

#if MPTK_PRO
            // Applied to the current note playing all the real time generators defined
            for (int i = 0; i < nbrGenerator; i++)
                if (indexGenerator[i] >= 0)
                    NotePlaying.ModifySynthParameter((fluid_gen_type)indexGenerator[i], valueGenerator[i] / 100f, MPTKModeGeneratorChange.Override);
#endif
            midiStreamPlayer.MPTK_PlayEvent(NotePlaying);
        }
        //! [ExampleMPTK_PlayEvent]

        private void MaestroStopOneNote()
        {
            if (NotePlaying != null)
            {
                //Debug.Log("Stop note");
                // Stop the note (method to simulate a real human on a keyboard: duration is not known when a note is triggered.
                midiStreamPlayer.MPTK_StopEvent(NotePlaying);
                NotePlaying = null;
            }
        }
        //! [ExampleFullMidiStream]

    }
}
