using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SocialPlatforms;

namespace MidiPlayerTK
{
    /// @ingroup runtime_global_config
    /// <summary>
    /// Singleton class to manage all global MPTK features.
    /// More information here: https://paxstellar.fr/midiplayerglobal/
    /// </summary>
    [HelpURL("https://paxstellar.fr/midiplayerglobal/")]
    public partial class MidiPlayerGlobal : MonoBehaviour
    {
        private static MidiPlayerGlobal instance;
        private MidiPlayerGlobal()
        {
            //Debug.Log("MidiPlayerGlobal: create Instance");
        }
        public static MidiPlayerGlobal Instance { get { return instance; } }

        public const string SoundfontsDB = "SoundfontsDB";
        public const string MidiFilesDB = "MidiDB";
        public const string SongFilesDB = "SongDB";
        public const string ExtensionMidiFile = ".bytes";
        public const string ExtensionSoundFileDot = ".txt";
        public const string ExtensionSoundFileFileData = "_data.bytes";
        public const string FilenameMidiSet = "MidiSet";
        public const string PathSF2 = "SoundFont";
        public const string PathToWave = "wave";
        public const string ErrorNoSoundFont = "No global SoundFont ready found. Load and choose a SoundFont from the Unity Editor menu 'Maestro/SoundFont Setup'";
        public const string ErrorNoPreset = "No Preset found in the SoundFont selected.\nHave you extracted them?\nCheck from the Unity Editor menu 'Maestro/SoundFont Setup'";
        public const string ErrorNoMidiFile = "No MIDI found in MIDI DB. Add MIDI file from Unity Editor menu 'Maestro/MIDI File Setup' or copy .mid files in MidiPlayer/Resources/MidiDB folder and load all with 'Maestro/MIDI File Setup'.";
        public const string HelpDefSoundFont = "Add or Select SoundFont from the Unity Editor menu 'Maestro' (Alt-f)";


        [HideInInspector]
        public string PathToResources;

        /// <summary>@brief
        /// This path could change depending your project. Change the path before any actions in MPTK. 
        /// </summary>
        static public string MPTK_PathToResources //= "MPTK/Resources/";
        {
            get
            {
                string path;
                //if (Instance == null)
                //    Debug.Log("MPTK_PathToResources: Instance==null");
                //else if (string.IsNullOrEmpty(Instance.PathToResources))
                //    Debug.Log("MPTK_PathToResources: PathToResources==null");

                if (instance != null && !string.IsNullOrEmpty(instance.PathToResources))
                {
                    path = instance.PathToResources + "/Resources/";
                    //Debug.Log($"MPTK_PathToResources: {path}");
                }
                else
                {
                    path = "MidiPlayer/Resources/";
                    //Debug.Log($"MPTK_PathToResources (default): {path}");
                }
                return path;
            }
        }

        /// <summary>@brief
        /// Folder where Soundfont downloaded are saved (cache). 
        /// @version 2.11.2 Pro
        /// </summary>
        static public string MPTK_PathSoundFontCache => Path.Combine(Application.persistentDataPath, "DownloadedSF");

        /// <summary>
        /// Current Soundfont name
        /// @version 2.15.0
        /// </summary>
        static public string MPTK_SoundFontName;


        // Initialized with InitPath()
        static public string PathToSoundfonts;
        static public string PathToMidiFile;
        static public string PathToMidiSet;


        /// <summary>@brief
        /// Downloading or reading time for the current SoundFont
        /// </summary>
        public static TimeSpan MPTK_TimeToDownloadSoundFont
        {
            get
            {
                return timeToDownloadSoundFont;
            }
        }

        /// <summary>@brief
        /// Loading time for the current SoundFont
        /// </summary>
        public static TimeSpan MPTK_TimeToLoadSoundFont
        {
            get
            {
                return timeToLoadSoundFont;
            }
        }

        /// <summary>@brief
        /// Loading time for the samples 
        /// </summary>
        public static TimeSpan MPTK_TimeToLoadWave
        {
            get
            {
                return timeToLoadWave;
            }
        }

        /// <summary>
        /// Loading status for SoundFont from local resources folder. For runtime loading status use #MPTK_SoundFontLoaded.
        //      - False at start.
        //      - True when a SF defined in the Maestro setup is ready.
        /// </summary>
        public static bool MPTK_SoundFontIsReady
        {
            get =>
                MidiPlayerGlobal.ImSFCurrent != null &&
                MidiPlayerGlobal.CurrentMidiSet != null &&
                MidiPlayerGlobal.CurrentMidiSet.ActiveSounFontInfo != null &&
                MidiPlayerGlobal.CurrentMidiSet.ActiveSounFontInfo.PatchCount > 0;
        }

        /// <summary>@brief
        /// Count of preset loaded
        /// </summary>
        public static int MPTK_CountPresetLoaded
        {
            get
            {
                int count = 0;
                if (ImSFCurrent != null)
                {
                    if (ImSFCurrent.DefaultBankNumber >= 0 && ImSFCurrent.DefaultBankNumber < ImSFCurrent.Banks.Length)
                        count = ImSFCurrent.Banks[ImSFCurrent.DefaultBankNumber].PatchCount;
                    if (ImSFCurrent.DrumKitBankNumber >= 0 && ImSFCurrent.DrumKitBankNumber < ImSFCurrent.Banks.Length)
                        count += ImSFCurrent.Banks[ImSFCurrent.DrumKitBankNumber].PatchCount;
                }
                return count;
            }
        }

        /// <summary>@brief
        /// Number of samples loaded
        /// </summary>
        public static int MPTK_CountWaveLoaded;

        /// <summary>@brief
        /// If true load soundfont at startup
        /// </summary>
        public static bool MPTK_LoadSoundFontAtStartup
        {
            get { return instance != null ? instance.LoadSoundFontAtStartup : false; }
            set
            {
                if (instance != null)
                    instance.LoadSoundFontAtStartup = value;
                else
                    Debug.LogWarning("MPTK_LoadSoundFontAtStartup: no MidiPlayerGlobal instance found");
            }
        }

      
        /// <summary>@brief
        /// If true load all waves when application is started else load when need when playing (default)
        /// Useless?
        /// </summary>
        public static bool VerboseGlobal
        {
            get { return instance != null ? instance.verboseGlobal : false; }
            set { if (instance != null) instance.verboseGlobal = value; }
        }

        /// <summary>@brief
        /// Find index of a Midi by name. Use the exact name defined in Unity resources folder MidiDB without any path or extension.\n
        /// Tips: Add Midi files to your project with the Unity menu MPTK or add it directly in the resource folder and open Midi File Setup to automatically integrate Midi in MPTK.
        /// </summary>
        /// <param name="name">name of the midi without path nor extension</param>
        /// <returns>-1 if not found else return the index of the midi.</returns>
        public static int MPTK_FindMidi(string name)
        {
            int index = -1;
            try
            {
                if (!string.IsNullOrEmpty(name))
                    if (CurrentMidiSet != null && CurrentMidiSet.MidiFiles != null)
                        index = CurrentMidiSet.MidiFiles.FindIndex(s => s == name);

            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return index;
        }

        /// <summary>@brief
        /// Calculate distance with the AudioListener.
        /// </summary>
        /// <param name="trf">Transform of the object to calculate the distance.</param>
        /// <returns></returns>
        public static float MPTK_DistanceToListener(Transform trf)
        {
            float distance = 0f;
            try
            {
                if (MPTK_AudioListener != null)
                {
                    distance = Vector3.Distance(MPTK_AudioListener.transform.position, trf.position);
                    //Debug.Log("MPTK_DistanceToListener:" + MPTK_AudioListener.name + " " + distance);
                }
                //else
                //    Debug.Log("MPTK_DistanceToListener: MPTK_AudioListener is null");
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }

            return distance;
        }

        /// <summary>@brief
        /// Event triggered at the end of the loading of the soundfont.\n
        /// Setting this callback function by script (AddListener) is not recommended. It's better to set callback function from the inspector.
        /// See here:https://mptkapi.paxstellar.com/d7/dc4/class_midi_player_t_k_1_1_midi_player_global.html#a64995b20027b35286c143f9f25a1cb6d
        /// @image html OnEventGlobal.png
        /// </summary>
        public static UnityEvent OnEventPresetLoaded
        {
            get { return Instance != null ? Instance.InstanceOnEventPresetLoaded : null; }
            set { Instance.InstanceOnEventPresetLoaded = value; }
        }

        /// <summary>@brief
        /// Loading status for SoundFont from external file or URL. For status from local resources folder use #MPTK_SoundFontIsReady.
        ///      - False at start.
        ///      - True when a SF downloaded or loaded from the cache is ready.
        /// </summary>
        public static bool MPTK_SoundFontLoaded = false;

        //! @cond NODOC

        public bool LoadSoundFontAtStartup = true;
        public bool verboseGlobal;
        public static TimeSpan timeToDownloadSoundFont = TimeSpan.Zero;
        public static TimeSpan timeToLoadSoundFont = TimeSpan.Zero;
        public static TimeSpan timeToLoadWave = TimeSpan.Zero;

        /// <summary>@brief
        /// Current SoundFont loaded
        /// </summary>
        public static ImSoundFont ImSFCurrent;

        /// <summary>@brief
        /// Event triggered when Soundfont is loaded
        /// </summary>
        public UnityEvent InstanceOnEventPresetLoaded = new UnityEvent();

        /// <summary>@brief
        /// Current Midi Set loaded
        /// </summary>
        public static MidiSet CurrentMidiSet;

        public static string WavePath;


        [SerializeField]
        [HideInInspector]
        public AudioListener audioListener;

        /// <summary>
        /// Specify which AudioListener is used.\n
        /// Distance and orientation calculation are based on this AudioListener.\n
        /// If not defined, the first AudioListener found in the scene will be used.\n
        /// @version 2.18.0 Pro
        /// </summary>
        public static AudioListener MPTK_AudioListener
        {
            get 
            {
                try
                {
                    if (Instance.audioListener == null)
                        // If no audio listener defined in the inspector, search one in the hierarchy
                        Instance.audioListener = FindFirstObjectByType<AudioListener>();
                    if (Instance.audioListener == null)
                    {
                        Debug.LogWarning("No audio listener found. Add one and only one AudioListener component to your hierarchy.");
                        //return;
                    }
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }

                try
                {
                    AudioListener[] listeners = Component.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
                    if (listeners != null && listeners.Length > 1)
                    {
                        Debug.LogWarning("More than one enabled audio listener found. Some unexpected behaviors could happen.");
                    }
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }

                //Debug.Log($"Get Instance.audioListener={Instance.audioListener}");
                return Instance.audioListener; 
            }

            set
            { 
                Instance.audioListener = value;
                Debug.Log($"Set Instance.audioListener={Instance.audioListener}");
            }
        }

        private static bool initialized = false;
        private static DateTime startInstance;


        // create path. Useful only in editor mode in conjonction of Application.dataPath. (for OS point of view)
        public static void InitPath()
        {
            PathToSoundfonts = MPTK_PathToResources + SoundfontsDB;
            PathToMidiFile = MPTK_PathToResources + MidiFilesDB;
            PathToMidiSet = MPTK_PathToResources + FilenameMidiSet + ExtensionSoundFileDot;
        }

        void Awake()
        {
            HelperNoteLabel.Init();

            if (verboseGlobal) Debug.Log("Awake MidiPlayerGlobal " + (instance == null ? "instance null" : "instance exist"));
            InitPath();
            if (instance != null && instance != this)
            {
                //Debug.Log("Awake MidiPlayerGlobal - remove previous instance");
                Destroy(gameObject);    // remove previous instance
            }
            else
            {
                //DontDestroyOnLoad(gameObject);
                InitInstance();
            }

        }

        public void InitInstance()
        {
            if (verboseGlobal)
            {
                startInstance = DateTime.Now;
                //Debug.Log("Awake MidiPlayerGlobal - InitThread");
            }

            instance = this;
            if (Application.isPlaying)
                Routine.RunCoroutine(instance.InitThread(), Segment.RealtimeUpdate);
            else
                Routine.RunCoroutine(instance.InitThread(), Segment.EditorUpdate);
        }

        void OnApplicationQuit()
        {
            //Debug.Log("MPTK ending after" + Time.time + " seconds");
        }

        //! @endcond

        /// <summary>@brief
        /// List of Soundfont(s) available. With the MPTK PRO, add or remove SoundFonts from the Unity editor menu "MPTK / SoundFont Setup" or Alt-F
        /// </summary>
        public static List<string> MPTK_ListSoundFont
        {
            get
            {
                if (CurrentMidiSet != null && CurrentMidiSet.SoundFonts != null)
                {
                    List<string> sfNames = new List<string>();
                    foreach (SoundFontInfo sfi in CurrentMidiSet.SoundFonts)
                        sfNames.Add(sfi.Name);
                    return sfNames;
                }
                else
                    return null;
            }
        }

        /// <summary>@brief
        /// List of MIDI files available. Add or remove MIDI from the Unity editor menu "MPTK / Midi File Setup" or Alt-M
        /// </summary>
        public static List<MPTKListItem> MPTK_ListMidi;

        /// <summary>@brief
        /// List of presets (instrument) for the default or selected bank.\n
        /// The default bank can be changed with #MPTK_SelectBankInstrument or with the popup "SoundFont Setup Alt-F" in the Unity editor.
        /// </summary>
        public static List<MPTKListItem> MPTK_ListPreset;

        public static string MPTK_PresetName(int patch)
        {

            if (MPTK_ListPreset != null && patch >= 0 && patch < MPTK_ListPreset.Count && MPTK_ListPreset[patch] != null)
                return MPTK_ListPreset[patch].Label;
            else
                return "";
        }

        /// <summary>@brief
        /// Gets the list of banks available.\n
        /// The default bank can be changed with #MPTK_SelectBankInstrument or #MPTK_SelectBankDrum or with the menu "MPTK / SoundFont" or Alt-F in the Unity editor.
        /// </summary>
        public static List<MPTKListItem> MPTK_ListBank;

        public static List<string> MPTK_BanksName;

        public static List<int> MPTK_BanksNumber;

        public static List<string> MPTK_PresetsName;
        public static List<int> MPTK_PresetsNumber;
        public static List<string> MPTK_PresetsNameDrum;
        public static List<int> MPTK_PresetsNumberDrum;

        /// <summary>@brief
        /// List of drum set for the default or selected bank.\n
        /// The default bank can be changed with #MPTK_SelectBankDrum or with the menu "MPTK / SoundFont" or Alt-F in the Unity editor.
        /// </summary>
        public static List<MPTKListItem> MPTK_ListPresetDrum;

        // <summary>
        // Get the list of presets available
        // </summary>
        // NOT USED removed with 2.89.0 public static List<MPTKListItem> MPTK_ListDrum;

        /// <summary>@brief
        /// Call by the first MidiPlayer awake
        /// </summary>
        private IEnumerator<float> InitThread()
        {
            if (!initialized)
            {
                //Debug.Log("MidiPlayerGlobal InitThread");
                initialized = true;
                ImSFCurrent = null;

                // Move to the MPTK_AudioListener getter with v2.18 
                //try
                //{
                //    if (MPTK_AudioListener == null)
                //        // If no audio listener defined in the inspector, search one in the hierarchy
                //        MPTK_AudioListener = FindFirstObjectByType<AudioListener>();
                //    if (MPTK_AudioListener == null)
                //    {
                //        Debug.LogWarning("No audio listener found. Add one and only one AudioListener component to your hierarchy.");
                //        //return;
                //    }
                //}
                //catch (System.Exception ex)
                //{
                //    MidiPlayerGlobal.ErrorDetail(ex);
                //}

                //try
                //{
                //    AudioListener[] listeners = Component.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
                //    if (listeners != null && listeners.Length > 1)
                //    {
                //        Debug.LogWarning("More than one enabled audio listener found. Some unexpected behaviors could happen.");
                //    }
                //}
                //catch (System.Exception ex)
                //{
                //    MidiPlayerGlobal.ErrorDetail(ex);
                //}

                try
                {
                    LoadMidiSetFromRsc();
                    if (verboseGlobal) Debug.Log($"{(DateTime.Now - startInstance).TotalSeconds:F3} MidiPlayerGlobal - LoadMidiSetFromRsc");

                    DicAudioWave.Init();
                    if (verboseGlobal) Debug.Log($"{(DateTime.Now - startInstance).TotalSeconds:F3} MidiPlayerGlobal - DicAudioWave.Init");

                    DicAudioClip.Init();
                    if (verboseGlobal) Debug.Log($"{(DateTime.Now - startInstance).TotalSeconds:F3} MidiPlayerGlobal - DicAudioClip.Init");
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }

                if (CurrentMidiSet == null)
                {
                    Debug.LogWarning(MidiPlayerGlobal.ErrorNoMidiFile);
                    yield return Routine.WaitForOneFrame;
                }
                else if (CurrentMidiSet.ActiveSounFontInfo == null)
                {
                    Debug.LogWarning(MidiPlayerGlobal.ErrorNoSoundFont);
                    yield return Routine.WaitForOneFrame;
                }
                if (verboseGlobal) Debug.Log($"{(DateTime.Now - startInstance).TotalSeconds:F3} MidiPlayerGlobal - WaitForOneFrame");

                BuildMidiList();
                if (verboseGlobal) Debug.Log($"{(DateTime.Now - startInstance).TotalSeconds:F3} MidiPlayerGlobal - BuildMidiList ");

                if (MPTK_LoadSoundFontAtStartup)
                    LoadCurrentSF();
                if (verboseGlobal) Debug.Log($"{(DateTime.Now - startInstance).TotalSeconds:F3} MidiPlayerGlobal - LoadCurrentSF ");
            }
        }

        /// <summary>@brief
        /// Search for each MidiFilePlayer and stop playing and stop the MIDI synth.
        /// </summary>
        public static void MPTK_Stop()
        {
            MidiSynth[] synths = FindObjectsByType<MidiSynth>(FindObjectsSortMode.None);
            if (synths != null)
            {
                foreach (MidiSynth synth in synths)
                {
                    if (synth is MidiFilePlayer)
                    {
                        MidiFilePlayer player = (MidiFilePlayer)synth;
                        if (player.MPTK_IsPlaying)
                        {
                            player.MPTK_Stop(); // stop and clear all sound
                        }
                    }
                    synth.MPTK_StopSynth();
                }
            }
        }

        /// <summary>@brief
        /// Stops all MIDI synthesizers and exits the application.
        /// </summary>
        public static void MPTK_Quit()
        {
            MPTK_Stop();
            Application.Quit();
        }

        //private static float startupdate = float.MinValue;

        /// <summary>@brief
        /// @warning MPTK_IsReady has been deprecated, please investigate MPTK_Pulse in place <midiSynth>.MPTK_SoundFont.IsReady
        /// @code
        ///    // Check that SoundFont is loaded for the midistream synth.
        ///    if (!midiStreamPlayer.MPTK_SoundFont.IsReady)
        ///       return;
        /// @endcode
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        public static bool MPTK_IsReady(float delay = 0.5f)
        {
            Debug.LogWarning("MPTK_IsReady has been deprecated, please investigate <midiSynth>.MPTK_SoundFont.IsReady in place.");
            return true;
        }


        /// <summary>@brief
        /// The default instrument and drum banks are defined with the popup "SoundFont Setup Alt-F" in the Unity editor.\n
        /// This method change the default instrument drum bank and build the presets list associated. See #MPTK_ListPreset.\n
        /// Note 1: this call doesn't change the current MIDI bank used to play an instrument, only the content of #MPTK_ListPreset.\n
        /// Note 2: to apply the bank to all channels, the synth must be restarted: call MidiFilePlayer.MPTK_InitSynth.\n
        /// Note 3: to change the current bank, rather use MidiFilePlayer.MPTK_ChannelPresetChange\n
        /// </summary>
        /// <param name="nbank">Number of the SoundFont Bank to load for instrument.</param>
        /// <returns>true if bank has been found else false.</returns>
        public static bool MPTK_SelectBankInstrument(int nbank)
        {
            if (ImSFCurrent != null && ImSFCurrent.Banks != null)
                if (nbank >= 0 && nbank < ImSFCurrent.Banks.Length)
                    if (ImSFCurrent.Banks[nbank] != null)
                    {
                        ImSFCurrent.DefaultBankNumber = nbank;
                        BuildPresetList(true);
                        return true;
                    }
                    else
                        Debug.LogWarningFormat("MPTK_SelectBankInstrument: bank {0} is not defined", nbank);
                else
                    Debug.LogWarningFormat("MPTK_SelectBankInstrument: bank {0} outside of range", nbank);
            return false;
        }

        /// <summary>@brief
        /// The default instrument and drum banks are defined with the popup "SoundFont Setup Alt-F" in the Unity editor.\n
        /// This method change the default instrument drum bank and the presets list associated. See #MPTK_ListPresetDrum.\n
        /// Note 1: this call doesn't change the current MIDI bank used to play a drum, only the content of #MPTK_ListPresetDrum.\n
        /// Note 2: to apply the bank to all channels, the synth must be restarted: call MidiFilePlayer.MPTK_InitSynth.\n
        /// Note 3: to change the current bank, rather use MidiFilePlayer.MPTK_ChannelPresetChange\n
        /// </summary>
        /// <param name="nbank">Number of the SoundFont Bank to load for drum.</param>
        /// <returns>true if bank has been found else false.</returns>
        public static bool MPTK_SelectBankDrum(int nbank)
        {
            if (ImSFCurrent != null && ImSFCurrent.Banks != null)
                if (nbank >= 0 && nbank < ImSFCurrent.Banks.Length)
                    if (ImSFCurrent.Banks[nbank] != null)
                    {
                        ImSFCurrent.DrumKitBankNumber = nbank;
                        BuildPresetList(false);
                        return true;
                        //BuildDrumList();
                    }
                    else
                        Debug.LogWarningFormat("MPTK_SelectBankDrum: bank {0} is not defined", nbank);
                else
                    Debug.LogWarningFormat("MPTK_SelectBankDrum: bank {0} outside of range", nbank);
            return false;
        }

        /// <summary>@brief
        /// Return the name of the preset (patch) from a bank and patch number.
        /// </summary>
        /// <param name="bank"></param>
        /// <param name="patch"></param>
        /// <returns>Name of the preset or empty if not found.</returns>
        public static string MPTK_GetPatchName(int bank, int patch)
        {
            string name = "";
            if (ImSFCurrent != null && ImSFCurrent.Banks != null)
                if (ImSFCurrent != null && ImSFCurrent.Banks != null)
                {
                    if (bank >= 0 && bank < ImSFCurrent.Banks.Length && ImSFCurrent.Banks[bank] != null)
                    {
                        if (ImSFCurrent.Banks[bank].defpresets != null)
                        {
                            if (patch >= 0 && patch < ImSFCurrent.Banks[bank].defpresets.Length && ImSFCurrent.Banks[bank].defpresets[patch] != null)
                                name = ImSFCurrent.Banks[bank].defpresets[patch].Name;
                        }
                    }
                }
            return name;
        }

        //! @cond NODOC

        /// <summary>@brief
        /// Loading of a SoundFont when playing using a thread
        /// </summary>
        /// <param name="restartPlayer"></param>
        /// <returns></returns>
        private static IEnumerator<float> LoadSoundFontThread(bool restartPlayer = true)
        {
            //if (MidiPlayerGlobal.ImSFCurrent != null)
            {
                //Debug.Log("Load MidiPlayerGlobal.ImSFCurrent: " + MidiPlayerGlobal.ImSFCurrent.SoundFontName);
                //Debug.Log("Load CurrentMidiSet.ActiveSounFontInfo: " + CurrentMidiSet.ActiveSounFontInfo.Name);

                MidiSynth[] synths = FindObjectsByType<MidiSynth>(FindObjectsSortMode.None);
                List<MidiFilePlayer> playerToRestart = new List<MidiFilePlayer>();
                MPTK_SoundFontLoaded = false;
                if (Application.isPlaying)
                {
                    if (synths != null)
                    {
                        foreach (MidiSynth synth in synths)
                        {
                            if (synth is MidiFilePlayer)
                            {
                                MidiFilePlayer player = (MidiFilePlayer)synth;
                                if (player.MPTK_IsPlaying)
                                {
                                    playerToRestart.Add(player);
                                    player.MPTK_Stop(); // stop and clear all sound
                                }
                            }
                            //synth.MPTK_ClearAllSound();
                            if (Application.isPlaying)
                                yield return Routine.WaitUntilDone(Routine.RunCoroutine(synth.ThreadWaitAllStop(), Segment.RealtimeUpdate), false);
                            else
                                yield return Routine.WaitUntilDone(Routine.RunCoroutine(synth.ThreadWaitAllStop(), Segment.EditorUpdate), false);
                            synth.MPTK_StopSynth();
                        }
                    }
                    DicAudioClip.Init();
                    DicAudioWave.Init();
                }
                LoadCurrentSF();

                if (Application.isPlaying)
                    if (synths != null)
                        foreach (MidiSynth synth in synths)
                            if (synths != null)
                                synth.MPTK_InitSynth();
                if (Application.isPlaying)
                    if (playerToRestart != null)
                        foreach (MidiFilePlayer synth in playerToRestart)
                            if (synths != null)
                                synth.MPTK_RePlay();

            }
        }

        /// <summary>@brief
        /// Loads a SF when editor
        /// </summary>
        private static void LoadSoundFont()
        {
            if (MidiPlayerGlobal.ImSFCurrent != null)
            {
                Debug.Log("Load MidiPlayerGlobal.ImSFCurrent: " + MidiPlayerGlobal.ImSFCurrent.SoundFontName);
                Debug.Log("Load CurrentMidiSet.ActiveSounFontInfo: " + CurrentMidiSet.ActiveSounFontInfo.Name);

                LoadCurrentSF();
                Debug.Log("Time To Load SoundFont: " + Math.Round(MidiPlayerGlobal.MPTK_TimeToLoadSoundFont.TotalSeconds, 3).ToString() + " second");
                if (Application.isPlaying)
                    Debug.Log("Time To Load Samples: " + Math.Round(MidiPlayerGlobal.MPTK_TimeToLoadWave.TotalSeconds, 3).ToString() + " second");
            }
        }

        /// <summary>@brief
        /// Core function to load a SF when playing or from editor from the Unity asset
        /// </summary>
        public static void LoadCurrentSF()
        {
            MPTK_SoundFontLoaded = false;
            DateTime start = DateTime.Now;
            // Load simplified soundfont
            try
            {
                if (CurrentMidiSet == null)
                {
                    Debug.Log(MidiPlayerGlobal.ErrorNoSoundFont);
                }
                else
                {
                    SoundFontInfo sfi = CurrentMidiSet.ActiveSounFontInfo;
                    if (sfi == null)
                        Debug.LogWarning(MidiPlayerGlobal.ErrorNoSoundFont);
                    else
                    {
                        if (VerboseGlobal)
                            Debug.Log("Start loading " + sfi.Name);

                        // Path to the soundfonts directory for this SF, start from resource folder
                        string pathToImSF = Path.Combine(SoundfontsDB + "/", sfi.Name);

                        WavePath = Path.Combine(pathToImSF + "/", PathToWave);
                        // Load all presets defined in the sf
                        ImSFCurrent = ImSoundFont.LoadMPTKSoundFont(pathToImSF, sfi.Name);
                        if (VerboseGlobal) Debug.Log($"{(DateTime.Now - start).TotalSeconds:F3} ImSoundFont.LoadMPTKSoundFont");

                        // Add
                        if (ImSFCurrent == null)
                        {
                            Debug.LogWarning("Error loading " + sfi.Name ?? "name not defined");
                        }
                        else
                        {
                            //Debug.Log("BuildBankList");
                            BuildBankList();
                            BuildPresetList(true);
                            BuildPresetList(false);

                            timeToLoadSoundFont = DateTime.Now - start;

                            //Debug.Log("End loading SoundFont " + timeToLoadSoundFont.TotalSeconds + " seconds");

                        }

                    }
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }

            if (ImSFCurrent == null)
            {
                Debug.LogWarning("SoundFont not loaded.");
                return;
            }

            if (VerboseGlobal) Debug.Log($"{(DateTime.Now - start).TotalSeconds:F3} SoundFont loaded");

            // Load samples only in run mode
            //if (Application.isPlaying) // check removes in v2.89.5
            {
                try
                {
                    MPTK_CountWaveLoaded = 0;
                    System.Diagnostics.Stopwatch watchLoadWave = new System.Diagnostics.Stopwatch(); // High resolution time
                    watchLoadWave.Start();

                    // Load audio clip is used only in the legacy mode (non core mode)
                    // Loading at startup is useful only with the legacy mode for better performance (avoid loading AudioClip at each note-on
                    // Add to DicAudioClip.
                    //if (MPTK_LoadWaveAtStartup)
                    //{
                    //    LoadAudioClip();
                    //    if (VerboseGlobal) Debug.Log($"Load DicAudioClip {(DateTime.Now - start).TotalSeconds:F3} second");
                    //}

#if !UNITY_WEBGL // WebGL doesn't support core mode, so no need to load AudioClip
                    // Load sample for core mode which are using DicAudioWave. Not used when live SF or external SF are used.
                    // Attention, on ne peut pas utiliser AudioClip et Resources en dehors du main thread unity.
                    // Il faut donc charger tous les echantillons avant.
                    // Add to DicAudioWave.
                    if (MPTK_LoadSoundFontAtStartup)
                    {
                        LoadWave();
                        if (VerboseGlobal) Debug.Log($"Load DicAudioWave {(DateTime.Now - start).TotalSeconds:F3} second");
                        if (Application.isPlaying && !DicAudioWave.Check())
                            Debug.LogError($"<color=red>No sample found in the SoundFont {CurrentMidiSet.ActiveSounFontInfo.Name}. Have you extracted samples from the Maestro 'SoundFont Setup' menu in Unity Editor?</color>");
                    }
#else
                    if (VerboseGlobal) Debug.Log($"Load DicAudioWave disabled with WebGL");
#endif
                    timeToLoadWave = watchLoadWave.Elapsed;
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }
            }

            if (ImSFCurrent != null)
            {
                MPTK_SoundFontName = ImSFCurrent.SoundFontName;
                MPTK_SoundFontLoaded = true;
            }

            try
            {
                if (OnEventPresetLoaded != null)
                    OnEventPresetLoaded.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError("OnEventPresetLoaded: exception detected. Check the callback code");
                Debug.LogException(ex);
            }
        }

        /// <summary>@brief
        /// Loads samples associated to a patch for High SF
        /// </summary>
        private static void LoadAudioClip()
        {
            try
            {
                float start = Time.realtimeSinceStartup;
                //Debug.LogWarning("LoadAudioClip - Load Sample for legacy mode");
                //int count = 0;
                if (ImSFCurrent != null)
                {
                    foreach (HiSample smpl in ImSFCurrent.HiSf.Samples)
                    {
                        if (smpl.Name != null)
                        {
                            if (!DicAudioClip.Exist(smpl.Name))
                            {
                                string path = WavePath + "/" + Path.GetFileNameWithoutExtension(smpl.Name);// + ".wav";
                                AudioClip ac = Resources.Load<AudioClip>(path);
                                if (ac != null)
                                {
                                    //Debug.Log("Sample load " + path);
                                    DicAudioClip.Add(smpl.Name, ac);

                                    MPTK_CountWaveLoaded++;
                                }
                                //else Debug.LogWarning("Sample " + smpl.WaveFile + " not found");
                            }
                        }
                    }
                }
                else Debug.Log("SoundFont not loaded ");
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        private static void LoadWave()
        {
            try
            {
                if (ImSFCurrent != null)
                {
                    foreach (HiSample hiSample in ImSFCurrent.HiSf.Samples)
                    {
                        if (hiSample.Name != null)
                        {
                            if (!DicAudioWave.Exist(hiSample.Name))
                            {
                                LoadWave(hiSample);
                                MPTK_CountWaveLoaded++;
                            }
                        }
                    }
                }
                else Debug.Log("SoundFont not loaded ");
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        public static void LoadWave(HiSample smpl)
        {
            DateTime start = DateTime.Now;

            //Debug.Log($">>-------------------- LoadCurrentSF - sample:{smpl.Name}");
            string path = WavePath + "/" + Path.GetFileNameWithoutExtension(smpl.Name);// + ".wav";
            AudioClip ac = Resources.Load<AudioClip>(path);
            //Debug.Log("<<-------------------- " + smpl.Name);
            if (ac != null && ac.loadType == AudioClipLoadType.CompressedInMemory)
            {
                float[] data = new float[ac.samples * ac.channels];
                if (ac.GetData(data, 0))
                {
                    //if ( smpl.SampleRate != (uint)ac.frequency)
                    //    Debug.Log($"{smpl.Name,-25} samples:{ac.samples} channels:{ac.channels} sampleRate:{smpl.SampleRate} frequency:{ac.frequency}");
                    smpl.SampleRate = (uint)ac.frequency;  // <- I added this and now it works as expected with vorbis
                    smpl.Data = data;
                    DicAudioWave.Add(smpl);
                    //Debug.Log($"{MPTK_CountWaveLoaded} {(DateTime.Now - start).TotalMilliseconds:F3} ms {path}");
                }
            }
            //else Debug.LogWarning("Sample " + smpl.Name + " not found");
        }

        /// <summary>@brief
        /// Builds list of presets found in the SoundFont
        /// </summary>
        static public void BuildBankList()
        {
            MPTK_ListBank = new List<MPTKListItem>();
            MPTK_BanksName = new List<string>();
            MPTK_BanksNumber = new List<int>();
            try
            {
                //Debug.Log(">>> Load Preset - b:" + ibank + " p:" + ipatch);
                if (ImSFCurrent != null && CurrentMidiSet != null)
                {
                    foreach (ImBank bank in ImSFCurrent.Banks)
                    {
                        if (bank != null)
                        {
                            MPTK_ListBank.Add(new MPTKListItem() { Index = bank.BankNumber, Label = "Bank " + bank.BankNumber });
                            MPTK_BanksName.Add(bank.BankNumber + " - Bank");
                            MPTK_BanksNumber.Add(bank.BankNumber);
                        }
                        else
                            MPTK_ListBank.Add(null);
                    }
                }
                else
                {
                    Debug.Log(MidiPlayerGlobal.ErrorNoSoundFont);
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        /// <summary>@brief
        /// Builds list of presets found in the default bank of the current SoundFont
        /// </summary>
        static public void BuildPresetList(bool forInstrument)
        {
            List<MPTKListItem> presets;
            List<string> presetsName;
            List<int> presetsNumber;


            if (forInstrument)
            {
                if (MPTK_ListPreset == null)
                    MPTK_ListPreset = new List<MPTKListItem>();
                else
                    MPTK_ListPreset.Clear();

                if (MPTK_PresetsName == null)
                    MPTK_PresetsName = new List<string>();
                else
                    MPTK_PresetsName.Clear();

                if (MPTK_PresetsNumber == null)
                    MPTK_PresetsNumber = new List<int>();
                else
                    MPTK_PresetsNumber.Clear();
                presets = MPTK_ListPreset;
                presetsName = MPTK_PresetsName;
                presetsNumber = MPTK_PresetsNumber;
            }
            else
            {
                if (MPTK_ListPresetDrum == null)
                    MPTK_ListPresetDrum = new List<MPTKListItem>();
                else
                    MPTK_ListPresetDrum.Clear();

                if (MPTK_PresetsNameDrum == null)
                    MPTK_PresetsNameDrum = new List<string>();
                else
                    MPTK_PresetsNameDrum.Clear();

                if (MPTK_PresetsNumberDrum == null)
                    MPTK_PresetsNumberDrum = new List<int>();
                else
                    MPTK_PresetsNumberDrum.Clear();

                presets = MPTK_ListPresetDrum;
                presetsName = MPTK_PresetsNameDrum;
                presetsNumber = MPTK_PresetsNumberDrum;
            }

            try
            {
                //Debug.Log(">>> Load Preset - b:" + ibank + " p:" + ipatch);
                if (ImSFCurrent != null)
                {
                    if ((forInstrument && ImSFCurrent.DefaultBankNumber >= 0 && ImSFCurrent.DefaultBankNumber < ImSFCurrent.Banks.Length) ||
                       (!forInstrument && ImSFCurrent.DrumKitBankNumber >= 0 && ImSFCurrent.DrumKitBankNumber < ImSFCurrent.Banks.Length))
                    {
                        int ibank = forInstrument ? ImSFCurrent.DefaultBankNumber : ImSFCurrent.DrumKitBankNumber;
                        if (ImSFCurrent.Banks[ibank] != null)
                        {
                            ImSFCurrent.Banks[ibank].PatchCount = 0;
                            for (int ipreset = 0; ipreset < ImSFCurrent.Banks[ibank].defpresets.Length; ipreset++)
                            {
                                HiPreset p = ImSFCurrent.Banks[ibank].defpresets[ipreset];
                                if (p != null)
                                {
                                    string label = $"{p.Num} - {p.Name}";
                                    presets.Add(new MPTKListItem() { Index = p.Num, Label = label, Position = presets.Count });
                                    presetsName.Add(label);
                                    presetsNumber.Add(p.Num);
                                    ImSFCurrent.Banks[ibank].PatchCount++;
                                }
                                //else
                                //    presets.Add(null);
                            }
                        }
                        else
                        {
                            Debug.LogWarningFormat("Default bank {0} for {1} is not defined.", ibank, forInstrument ? "Instrument" : "Drum");
                        }
                    }
                    else
                    {
                        Debug.LogWarningFormat("No default bank defined for {0}.", forInstrument ? "Instrument" : "Drum");
                    }

                    // Global count
                    //ImSFCurrent.PatchCount = 0;
                    foreach (ImBank bank in ImSFCurrent.Banks)
                    {
                        if (bank != null)
                        {
                            bank.PatchCount = 0;
                            foreach (HiPreset preset in bank.defpresets)
                            {
                                if (preset != null)
                                {
                                    // Bank count
                                    bank.PatchCount++;
                                }
                            }
                            //ImSFCurrent.PatchCount += bank.PatchCount;
                        }
                    }
                }
                else
                {
                    Debug.Log(MidiPlayerGlobal.ErrorNoSoundFont);
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }

            //if (forInstrument)
            //{
            //    MPTK_ListPreset = presets;
            //}
            //else
            //{
            //    MPTK_ListPresetDrum = presets;
            //}
        }

        public static void BuildMidiList()
        {
            MPTK_ListMidi = new List<MPTKListItem>();
            if (CurrentMidiSet != null && CurrentMidiSet.MidiFiles != null)
                foreach (string name in CurrentMidiSet.MidiFiles)
                    MPTK_ListMidi.Add(new MPTKListItem() { Index = MPTK_ListMidi.Count, Label = name, Position = MPTK_ListMidi.Count });
            //Debug.Log("Midi file list loaded: " + MPTK_ListMidi.Count);
        }

        private static void LoadMidiSetFromRsc()
        {
            try
            {
                TextAsset sf = Resources.Load<TextAsset>(MidiPlayerGlobal.FilenameMidiSet);
                if (sf == null)
                    Debug.LogWarning(MidiPlayerGlobal.ErrorNoMidiFile);
                else
                {
                    //UnityEngine.Debug.Log(sf.text);
                    CurrentMidiSet = MidiSet.LoadRsc(sf.text);
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        public void EndLoadingSF()
        {
            Debug.Log("End loading SF, MPTK is ready to play");

            //Debug.Log("List of presets available");
            //int i = 0;
            //foreach (string preset in MidiPlayerGlobal.MPTK_ListPreset)
            //    Debug.Log("   " + string.Format("[{0,3:000}] - {1}", i++, preset));
            //i = 0;
            //Debug.Log("List of drums available");
            //foreach (string drum in MidiPlayerGlobal.MPTK_ListDrum)
            //    Debug.Log("   " + string.Format("[{0,3:000}] - {1}", i++, drum));

            Debug.Log("Load statistique");
            Debug.Log("   Time To Load SoundFont: " + Math.Round(MidiPlayerGlobal.MPTK_TimeToLoadSoundFont.TotalSeconds, 3).ToString() + " second");
            Debug.Log("   Time To Load Samples: " + Math.Round(MidiPlayerGlobal.MPTK_TimeToLoadWave.TotalSeconds, 3).ToString() + " second");
            Debug.Log("   Presets Loaded: " + MidiPlayerGlobal.MPTK_CountPresetLoaded);
            Debug.Log("   Samples Loaded: " + MidiPlayerGlobal.MPTK_CountWaveLoaded);
        }

        public static void ErrorDetail(System.Exception ex)
        {
            string msg = ex.Message;
            //Debug.LogWarning("Maestro Error " + msg);
            Debug.LogException(ex);
            //if (ex.StackTrace != null) msg += ex.StackTrace;
            //if (ex.TargetSite != null) msg += " " + ex.TargetSite;
            //if (ex.InnerException != null) msg += " " + ex.InnerException;
            //if (ex.TargetSite != null) Debug.LogWarning("   TargetSite:" + ex.TargetSite);
            //if (ex.InnerException != null) Debug.LogWarning("   InnerException:" + ex.InnerException);

            // V2.89.0 Remove the full error stack because Unity 2019 do the job!
            //var st = new System.Diagnostics.StackTrace(ex, true);
            //if (st != null)
            //{
            //    var frames = st.GetFrames();
            //    if (frames != null)
            //    {
            //        foreach (var frame in frames)
            //        {
            //            if (frame.GetFileLineNumber() < 1)
            //                continue;
            //            Debug.LogWarning("   " + frame.GetFileName() + " " + frame.GetMethod().Name + " " + frame.GetFileLineNumber());
            //        }
            //    }
            //    else
            //        Debug.LogWarning("   " + ex.StackTrace ?? "");
            //}
            //else
            //    Debug.LogWarning("   " + ex.StackTrace ?? "");
        }
        //! @endcond
    }
}
