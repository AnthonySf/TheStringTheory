using System;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

namespace MidiPlayerTK
{
    /// <summary>@brief
    /// SoundFont adapted to Unity
    /// </summary>
    [Serializable]
    public partial class ImSoundFont
    {
        public string SoundFontName;
        public int DefaultBankNumber;
        public int DrumKitBankNumber;
        public const int MAXBANKPRESET = 129;
        public int LoadType;
        public int CompressionFormat;


        public string StrBankSelected;

        [XmlIgnore]
        public bool LiveSF = false;

        [XmlIgnore]
        public bool[] BankSelected;

        [XmlIgnore]
        public SFData HiSf;

        [XmlIgnore]
        public float[] SamplesData;

        /// <summary>@brief
        /// List  of banks of the sound font
        /// </summary>
        [XmlIgnore]
        public ImBank[] Banks;

        public ImSoundFont()
        {
            DefaultBankNumber = -1;
            DrumKitBankNumber = -1;
            BankSelected = new bool[MAXBANKPRESET];
            CompressionFormat = 0; // PCM 
            LoadType = 1; // CompressedInMemory
        }

        public int IndexInstrumentBank
        {
            get
            {
                if (DefaultBankNumber >= 0)
                    for (int b = 0; b < Banks.Length; b++)
                    {

                        if (Banks[b] != null && Banks[b].BankNumber == DefaultBankNumber)
                        {
                            return b;
                        }
                    }
                return 0;
            }
        }
        public int IndexDrumBank
        {
            get
            {
                if (DrumKitBankNumber >= 0)
                    for (int b = 0; b < Banks.Length; b++)
                    {

                        if (Banks[b] != null && Banks[b].BankNumber == DrumKitBankNumber)
                        {
                            return b;
                        }
                    }
                return 0;
            }
        }
        public void SelectAllBanks()
        {
            for (int b = 0; b < BankSelected.Length; b++)
                BankSelected[b] = true;
        }
        public void UnSelectAllBanks()
        {
            for (int b = 0; b < BankSelected.Length; b++)
                BankSelected[b] = false;
        }
        public void InverseSelectedBanks()
        {
            for (int b = 0; b < BankSelected.Length; b++)
                BankSelected[b] = !BankSelected[b];
        }

        public int FirstBank()
        {
            int ibank = 0;
            try
            {
                while (Banks[ibank] == null && ibank < Banks.Length) ibank++;
                if (ibank == Banks.Length) ibank = 0;
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return ibank;
        }
        public int LastBank()
        {
            int ibank = Banks.Length - 1;
            try
            {
                while (Banks[ibank] == null && ibank >= 0) ibank--;
                if (ibank < 0) ibank = 0;
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return ibank;
        }

        /// <summary>@brief
        /// Load a ImSoundFont from a TextAsset resource
        /// </summary>
        /// <param name="path"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static ImSoundFont LoadMPTKSoundFont(string path, string name)
        {
            ImSoundFont loaded = null;

            try
            {
                // Path to the XML soundfont file for this SF
                TextAsset sfxml = Resources.Load<TextAsset>(path + "/" + name);
                if (sfxml == null || sfxml.bytes.Length == 0)
                    Debug.LogWarningFormat("SoundFont {0} not found in Unity resource {1}", name, path);
                else
                {
                    if (sfxml != null && !string.IsNullOrEmpty(sfxml.text))
                    {
                        // Load XML description of the SF
                        var serializer = new XmlSerializer(typeof(ImSoundFont));
                        using (TextReader reader = new StringReader(sfxml.text))
                        {
                            loaded = serializer.Deserialize(reader) as ImSoundFont;
                        }

                        if (loaded == null)
                        {
                            Debug.LogWarningFormat("Error when reading SoundFont {0} from {1}", name, path);
                        }
                        else
                        {
                            //Debug.LogFormat("XML SF loaded {0} Bank instrument:{1} Bank drum:{2}", loaded.SoundFontName, loaded.DefaultBankNumber, loaded.DrumKitBankNumber);

                            // Load binary data of the SF
                            TextAsset sfbin = Resources.Load<TextAsset>(path + "/" + name + "_data");
                            if (sfbin == null)
                                Debug.LogWarningFormat("Error when reading SoundFont data {0} from {1}", name, path);
                            else
                            {
                                // Create sf from binaray data
                                SFLoad load = new SFLoad(sfbin.bytes, SFFile.SfSource.MPTK);
                                if (load == null || load.SfData == null)
                                    Debug.LogWarningFormat("Error when decoding SoundFont data {0} from {1}", name, path);
                                else
                                {
                                    loaded.HiSf = load.SfData;
                                    LoadBanks(loaded);
                                    //SFFile.DumpSFToFile(loaded.hisf, @"c:\temp\" + name + "_dump.txt");
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return loaded;
        }

        public static void LoadBanks(ImSoundFont imsf)
        {
            // BankSelected is XMLIgnore, build bank selected from string
            imsf.BankSelected = new bool[MAXBANKPRESET];
            for (int b = 0; b < imsf.BankSelected.Length; b++)
                imsf.BankSelected[b] = false;

            if (imsf.StrBankSelected != null)
            {
                string[] sbanks = imsf.StrBankSelected.Split(',');
                if (sbanks != null)
                    foreach (string sbank in sbanks)
                    {
                        if (!string.IsNullOrEmpty(sbank))
                        {
                            int ibank = Convert.ToInt32(sbank);
                            if (ibank >= 0 && ibank < MAXBANKPRESET)
                                imsf.BankSelected[ibank] = true;
                        }
                    }
            }

            // Build bank content
            imsf.Banks = new ImBank[MAXBANKPRESET];
            foreach (HiPreset p in imsf.HiSf.preset)
            {
                if (p != null)
                {
                    if (imsf.Banks[p.Bank] == null)
                    {
                        //Debug.Log($"Create bank {p.Bank}");
                        // New bank, create it
                        imsf.Banks[p.Bank] = new ImBank()
                        {
                            BankNumber = p.Bank,
                            defpresets = new HiPreset[MAXBANKPRESET]
                        };
                    }
                    //if (p.Bank == 24 || p.Bank == 25 || p.Bank == 26)
                    //    Debug.Log($"Add preset num:{p.Num} id:{p.ItemId} zone:{p.Zone.Length} to bank {p.Bank}");
                    
                    imsf.Banks[p.Bank].defpresets[p.Num] = p;
                }
            }
        }
    }
}
