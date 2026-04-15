using System.Collections.Generic;
using UnityEngine;

namespace MidiPlayerTK
{
    //! @cond NODOC

    /// <summary>
    /// Audioclip cache contains samples (legacy mode) 
    /// </summary>
    public class DicAudioClip
    {
        private static Dictionary<string, AudioClip> dicSamples;
        public static void Init()
        {
            dicSamples = new Dictionary<string, AudioClip>();
        }
        public static void Add(string name, AudioClip clip)
        {
            AudioClip c;
            try
            {
#if DEBUG_LOAD_WAVE
                Debug.Log($"DicAudioClip.Add {name} {clip.length} {clip.frequency} {clip.name}");
#endif
                if (!dicSamples.TryGetValue(name, out c))
                {
                    dicSamples.Add(name, clip);
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
                AudioClip c;
                return dicSamples.TryGetValue(name, out c);
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return false;
        }
        public static AudioClip Get(string name)
        {
            try
            {
                AudioClip c;
                dicSamples.TryGetValue(name, out c);
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
