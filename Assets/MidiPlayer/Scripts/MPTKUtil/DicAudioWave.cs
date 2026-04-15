using System.Collections.Generic;
using UnityEngine;


namespace MidiPlayerTK
{
    //! @cond NODOC

    /// <summary>
    /// HiSample cache which contains samples (core mode) 
    /// </summary>
    public class DicAudioWave
    { 
        private static Dictionary<string, HiSample> dicWave;
        public static void Init()
        {
            dicWave = new Dictionary<string, HiSample>();
        }

        public static bool Check()
        {
            return (dicWave == null || dicWave.Count == 0 ? false : true);
        }

        public static void Add(HiSample smpl)
        {
            HiSample c;
            try
            {
                if (dicWave!= null && !dicWave.TryGetValue(smpl.Name, out c))
                {
#if DEBUG_LOAD_WAVE
                    Debug.Log($"DicAudioWave.Add {dicWave.Count} {smpl.Name} {smpl.SampleRate} {smpl.End - smpl.Start}");
#endif
                    dicWave.Add(smpl.Name, smpl);
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }
        public static bool Exist(string name)
        {
            try
            {
                if (dicWave != null)
                {
                    HiSample c;
                    return dicWave.TryGetValue(name, out c);
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return false;
        }
        public static HiSample Get(string name)
        {
            try
            {
                HiSample c;
                dicWave.TryGetValue(name, out c);
                return c;
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return null;
        }
        public static HiSample GetWave(string name)
        {
            try
            {
                HiSample c;

                dicWave.TryGetValue(name, out c);
                return c;
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return null;
        }
    }
    //! @endcond
}
