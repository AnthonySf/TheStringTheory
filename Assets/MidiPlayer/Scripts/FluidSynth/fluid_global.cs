#if UNITY_EDITOR
#else
using System.Diagnostics;
#endif

namespace MidiPlayerTK
{
    //    public enum fluid_log_level
    //    {
    //        FLUID_PANIC,   /**< The synth can't function correctly any more */
    //        FLUID_ERR,     /**< Serious error occurred */
    //        FLUID_WARN,    /**< Warning */
    //        FLUID_INFO,    /**< Verbose informational messages */
    //        FLUID_DBG,     /**< Debugging messages */
    //        LAST_LOG_LEVEL /**< @warning This symbol is not part of the public API and ABI stability guarantee and may change at any time! */
    //    }

    //    static public class fluid_global
    //    {
    //        public const int FLUID_OK = 0;
    //        public const int FLUID_FAILED = -1;

    //        static public void FLUID_LOG(fluid_log_level level, string fmt, params object[] list)
    //        {
    //#if UNITY_EDITOR
    //            if (level == fluid_log_level.FLUID_INFO)
    //                Debug.LogFormat(fmt, list);
    //            else if (level == fluid_log_level.FLUID_WARN)
    //                Debug.LogWarningFormat(fmt, list);
    //            else
    //                Debug.LogErrorFormat(fmt, list);
    //#else
    //            Debug.WriteLine(string.Format(fmt, list));
    //#endif
    //        }

    //    }
}
