//#define MPTK_PRO
using System.Collections.Generic;
using UnityEngine;

namespace MidiPlayerTK
{
    /// <summary>@brief
    /// Provides runtime access to SoundFont loading, caching,
    /// state inspection, and preset/bank exploration.
    /// @version 2.14
    /// @note
    /// - An instance of this class is automatically created for each MPTK prefab
    ///   (MidiFilePlayer, MidiStreamPlayer, etc.) loaded in the scene.
    /// - Access this API from MidiSynth.MPTK_SoundFont.
    /// - To enable detailed runtime logs, set MidiFilePlayer.VerboseSoundfont to true.
    /// </summary>
    /// @ingroup soundfont_management
    public partial class MPTKSoundFont
    {
        /// @ingroup soundfont_cache
        /// <summary>@brief
        /// Whether to reuse previously downloaded SoundFonts if available. Default is true.\n
        /// Folder: Application.persistentDataPath/"DownloadedSF"
        /// </summary>
        public bool LoadFromCache;

        /// @ingroup soundfont_cache
        /// <summary>@brief
        /// Whether to store the loaded SoundFont in a local cache. Default is true.\n
        /// Folder: Application.persistentDataPath/"DownloadedSF"
        /// </summary>
        public bool SaveToCache;

        /// @ingroup soundfont_cache
        /// <summary>@brief
        /// Whether to only download the SoundFont. When true, the SoundFont is not loaded in the MidiSynth. Default is false.
        /// @note
        /// - Set SaveToCache to true to save the SoundFont in the local cache.
        /// - Set LoadFromCache to false to force the SoundFont download.
        /// - Interesting only for external soundfont, useful to download a SoundFont in the background and load it later in many MidiSynths.
        /// </summary>
        public bool DownloadOnly;


        /// @ingroup soundfont_state
        /// <summary>@brief
        /// The default bank to use for instruments. Set to -1 to select the first bank.
        /// </summary>
        public int DefaultBank
        {
            get => sfLocal != null ? defaultBank : MidiPlayerGlobal.ImSFCurrent.DefaultBankNumber;
            set => defaultBank = value;
        }
        private int defaultBank;


        /// @ingroup soundfont_state
        /// <summary>@brief
        /// The bank to use for the drum kit. Set to -1 to select the last bank.
        /// </summary>
        public int DrumBank
        {
            get => sfLocal != null ? drumBank : MidiPlayerGlobal.ImSFCurrent.DrumKitBankNumber;
            set => drumBank = value;
        }
        private int drumBank;
        private MidiSynth synth;
        private ImSoundFont sfLocal = null;
        private string soundFontName = string.Empty;
        private bool isInternal = true; 
        private bool isDefault = true; // Default SoundFont loaded from MPTK

        /// @ingroup soundfont_state
        /// <summary>@brief
        /// True if the Soundfont is loaded from the MPTK resources (internal).\n
        /// False is the Soundfont is loaded from an external resources, local file or from an URL.
        /// </summary>
        public bool IsInternal { get => isInternal; }

        /// @ingroup soundfont_state
        /// <summary>@brief
        /// True if the Soundfont is loaded from the MPTK resources (internal)  and is the default (selected in "Soundfont Setup").
        /// </summary>
        public bool IsDefault { get => isDefault; }

        /// @ingroup soundfont_state
        /// <summary>@brief
        /// True if a Soundfont is available from internal or external.
        /// </summary>
        public bool IsReady { get => SoundFont != null; }

        /// @ingroup soundfont_state
        /// <summary>@brief
        /// When a Soundfont is ready, return an instance of #ImSoundfont else return null 
        /// </summary>
        public ImSoundFont SoundFont
        {
            get
            {
                if (sfLocal != null)
                    return sfLocal;
                else
                    return MidiPlayerGlobal.ImSFCurrent;
            }
        }

        /// @ingroup soundfont_state
        /// <summary>@brief
        /// Name of Soundfont. 
        /// </summary>
        public string SoundFontName
        {
            get
            {
                if (sfLocal != null)
                    return soundFontName;
                else
                    return MidiPlayerGlobal.MPTK_SoundFontName;
            }
        }

        public MPTKSoundFont(MidiSynth pSynth)
        {
            synth = pSynth;
            if (synth.VerboseSoundfont) Debug.Log($"Init MidiSynth Soundfont");
            DefaultBank = -1;
            DrumBank = -1;
            LoadFromCache = true;
            DownloadOnly = false;
            SaveToCache = true;
        }


        /// @ingroup soundfont_presets
        /// <summary>@brief
        /// This method change the default instrument drum bank and build the presets list associated. See #ListPreset.\n
        /// Note 1: this call doesn't change the current MIDI bank used to play an instrument, only the content of #ListPreset.\n
        /// Note 2: to apply the bank to all channels, the synth must be restarted: call MidiFilePlayer.MPTK_InitSynth.\n
        /// Note 3: to change the current bank, rather use #MidiSynth.MPTK_ChannelPresetChange\n
        /// </summary>
        /// <param name="bankNumber">Number of the SoundFont Bank to load for instrument.</param>
        /// <returns>true if bank has been found else false.</returns>
        /// @see BanksName
        /// @see BanksNumber
        /// @see PresetsName
        /// @see MidiSynth.MPTK_ChannelPresetChange
        public bool SelectBankInstrument(int bankNumber)
        {
            if (synth.VerboseSoundfont) Debug.Log($"SelectBankInstrument: {bankNumber} for {synth.name}");
            if (sfLocal != null)
            {
#if MPTK_PRO
                if (bankNumber >= 0 && bankNumber < sfLocal.Banks.Length)
                    if (sfLocal.Banks[bankNumber] != null)
                    {
                        sfLocal.DefaultBankNumber = bankNumber;
                        BuildPresetList(true);
                        return true;
                    }
                    else
                        Debug.LogWarningFormat("MPTK_SelectBankInstrument: bank {0} is not defined", bankNumber);
                else
                    Debug.LogWarningFormat("MPTK_SelectBankInstrument: bank {0} outside of range", bankNumber);
#endif
            }
            else
                return MidiPlayerGlobal.MPTK_SelectBankInstrument(bankNumber);
            return false;
        }

        /// @ingroup soundfont_presets
        /// <summary>@brief
        /// This method change the default instrument drum bank and build the presets list associated. See #ListPresetDrum.\n
        /// Note 1: this call doesn't change the current MIDI bank used to play a drum, only the content of #ListPresetDrum.\n
        /// Note 2: to apply the bank to all channels, the synth must be restarted: call MidixxxPlayer.MPTK_InitSynth.\n
        /// Note 3: to change the current bank, rather use MidiSynthPlayer.MPTK_ChannelPresetChange\n
        /// </summary>
        /// <param name="bankNumber">Number of the SoundFont Bank to load for drum.</param>
        /// <returns>true if bank has been found else false.</returns>
        /// @see PresetsDrumName
        /// @see PresetsDrumNumber
        /// @see MidiSynth.MPTK_ChannelPresetChange
        public bool SelectBankDrum(int bankNumber)
        {
            if (synth.VerboseSoundfont) Debug.Log($"SelectBankDrum: {bankNumber} for {synth.name}");
            if (sfLocal != null)
            {
#if MPTK_PRO
                if (bankNumber >= 0 && bankNumber < sfLocal.Banks.Length)
                    if (sfLocal.Banks[bankNumber] != null)
                    {
                        sfLocal.DrumKitBankNumber = bankNumber;
                        BuildPresetList(false);
                        return true;
                    }
                    else
                        Debug.LogWarningFormat("MPTK_SelectBankDrum: bank {0} is not defined", bankNumber);
                else
                    Debug.LogWarningFormat("MPTK_SelectBankDrum: bank {0} outside of range", bankNumber);
#endif
            }
            else
                return MidiPlayerGlobal.MPTK_SelectBankDrum(bankNumber);
            return false;
        }

        /// @ingroup soundfont_presets
        /// <summary>@brief
        /// List of banks name available with the format "<number> - Bank". Unlike preset, there is no bank name defined in a Soundfont.\n
        /// Index in the list is not the bank number (missing bank have been removed), use the same index in #BanksNumber to get the bank number.
        /// </summary>
        public List<string> BanksName
        {
            get => sfLocal != null ? banksName : MidiPlayerGlobal.MPTK_BanksName;
        }
        private List<string> banksName;

        /// @ingroup soundfont_presets
        /// <summary>@brief
        /// List of banks number available. 
        /// Index in the list is not the bank number (missing banks have been removed), use the same index in #BanksName to get the bank name.
        /// </summary>
        public List<int> BanksNumber
        {
            get => sfLocal != null ? banksNumber : MidiPlayerGlobal.MPTK_BanksNumber;
        }
        private List<int> banksNumber;


        /// @ingroup soundfont_presets
        /// <summary>@brief
        /// List of preset name available with the format "<number> - <name>".\n
        /// Index in the list is not the preset number (preset missing has been removed), use the same index in #PresetsNumber to get the preset number.
        /// </summary>
        public List<string> PresetsName
        {
            get => sfLocal != null ? presetsName : MidiPlayerGlobal.MPTK_PresetsName;
        }
        private List<string> presetsName;

        /// @ingroup soundfont_presets
        /// <summary>@brief
        /// List of preset number available.
        /// Use the same index in #PresetsName to get the preset name.
        /// </summary>
        public List<int> PresetsNumber
        {
            get => sfLocal != null ? presetsNumber : MidiPlayerGlobal.MPTK_PresetsNumber;
        }
        private List<int> presetsNumber;


        /// @ingroup soundfont_presets
        /// <summary>@brief
        /// List of preset name available with the format "<number> - <name>" but dedicated to drum bank.\n
        /// Index in the list is not the preset number (preset missing has been removed), use the same index in #PresetsDrumNumber to get the preset number.
        /// </summary>
        public List<string> PresetsDrumName
        {
            get => sfLocal != null ? presetsDrumName : MidiPlayerGlobal.MPTK_PresetsName;
        }
        private List<string> presetsDrumName;

        /// @ingroup soundfont_presets
        /// <summary>@brief
        /// List of preset number available but dedicated to drum bank.\n
        /// Use the same index in #PresetsDrumName to get the preset name.
        /// </summary>
        public List<int> PresetsDrumNumber
        {
            get => sfLocal != null ? presetsDrumNumber : MidiPlayerGlobal.MPTK_PresetsNumber;
        }
        private List<int> presetsDrumNumber;

        /// @ingroup soundfont_presets
        /// <summary>@brief
        /// Gets the list of banks available. It's a full and fixed list of 129 MPTKListItem with null value if a bank is missing.\n
        /// MPTKListItem.Index give the number of the bank\n
        /// Prefer using the #BanksName to get a list of bank available and #BanksNumber to get the bank number at same corresponding index.\n
        /// @note
        /// - The default bank can be changed with #SelectBankInstrument or #SelectBankDrum, the ListBank will be updated.
        /// - Legacy list based on MPTKListItem. Please investigate #BanksName or #BanksNumber for an easier use.
        /// </summary>
        public List<MPTKListItem> ListBank
        {
            get => sfLocal != null ? listBank : MidiPlayerGlobal.MPTK_ListBank;
            set => listBank = value;
        }
        private List<MPTKListItem> listBank;

        /// @ingroup soundfont_presets
        /// <summary>@brief
        /// List of presets for instrument for the default or selected bank. When using with preset change MIDI event, MPTKListItem.Index give the number of the preset\n
        /// @note
        /// - The default bank can be changed with #SelectBankInstrument, the ListPreset will be updated.
        /// - Legacy list based on MPTKListItem. Please investigate #PresetsName or #PresetsNumber for an easier use.
        /// </summary>
        public List<MPTKListItem> ListPreset
        {

            get => sfLocal != null ? listPresetInstrument : MidiPlayerGlobal.MPTK_ListPreset;
            set => listPresetInstrument = value;
        }
        private List<MPTKListItem> listPresetInstrument;

        /// @ingroup soundfont_presets
        /// <summary>@brief
        /// List of presets drum for the default or selected bank. When using with preset change MIDI event, MPTKListItem.Index give the number of the preset\n
        /// @note
        /// - The default bank can be changed with #SelectBankDrum, the ListPresetDrum will be updated.
        /// - Legacy list based on MPTKListItem. Please investigate #PresetsDrumName or #PresetsDrumNumber for an easier use.
        /// </summary>
        public List<MPTKListItem> ListPresetDrum
        {
            get => sfLocal != null ? listPresetDrum : MidiPlayerGlobal.MPTK_ListPresetDrum;
            set => listPresetDrum = value;
        }
        private List<MPTKListItem> listPresetDrum;
    }
}
