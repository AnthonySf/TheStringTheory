using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.Scripting;

namespace MidiPlayerTK
{
    /// @ingroup runtime_catalogs
    /// <summary>@brief
    /// Contains the setting of MidiFilePlayer TK
    /// </summary>
    public class MidiSet
    {
        public List<SoundFontInfo> SoundFonts;
        public int IndexSelectedSF;
               
        public SoundFontInfo ActiveSounFontInfo
        {
            get
            {
                if (IndexSelectedSF >= 0 && IndexSelectedSF < SoundFonts.Count)
                    return SoundFonts[IndexSelectedSF];
                else
                    return null;
            }
        }
        public List<string> MidiFiles;

        [Preserve]
        public MidiSet()
        {
            SoundFonts = new List<SoundFontInfo>();
        }

        public void AddSoundFont(ImSoundFont imsf)
        {
            SoundFontInfo sfi = new SoundFontInfo();
            sfi.Name = imsf.SoundFontName;
            SoundFonts.Add(sfi);
        }

        public int LastIndexSoundFont()
        {
            return SoundFonts.Count - 1;
        }
        public void SetActiveSoundFont(int index)
        {
            try
            {
                if (index > -1 && index < SoundFonts.Count)
                {
                    IndexSelectedSF = index;
                    //Debug.Log("Select SoundFont index " + index);
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        /// <summary>@brief
        /// Save setting (edit mode)
        /// </summary>
        public void Save()
        {
            try
            {
                //Debug.Log("Save MidiSet " + ActiveSounFontInfo.DefaultBankNumber + " " + ActiveSounFontInfo.DrumKitBankNumber);
                var serializer = new XmlSerializer(typeof(MidiSet));
                string path = Application.dataPath + "/" + MidiPlayerGlobal.PathToMidiSet;
                using (var stream = new FileStream(path, FileMode.Create))
                {
                    serializer.Serialize(stream, this);
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        /// <summary>@brief
        /// Load setting (edit mode)
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static MidiSet Load(string path)
        {
            MidiSet loaded = null;

            try
            {
                if (File.Exists(path))
                {
                    var serializer = new XmlSerializer(typeof(MidiSet));
                    using (var stream = new FileStream(path, FileMode.Open))
                    {
                        loaded = serializer.Deserialize(stream) as MidiSet;
                    }
                }
                else
                    loaded = new MidiSet();
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }

            return loaded;
        }

        /// <summary>@brief
        /// Load setting (run mode)
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static MidiSet LoadRsc(string data)
        {
            MidiSet loaded = null;

            try
            {
                if (!string.IsNullOrEmpty(data))
                {
                    var serializer = new XmlSerializer(typeof(MidiSet));
                    using (TextReader reader = new StringReader(data))
                    {
                        loaded = serializer.Deserialize(reader) as MidiSet;
                    }
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }

            return loaded;
        }
    }
}
