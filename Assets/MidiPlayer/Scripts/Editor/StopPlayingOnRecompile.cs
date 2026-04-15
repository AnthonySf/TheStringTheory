#if UNITY_EDITOR
// Copyright Cape Guy Ltd. 2018. http://capeguy.co.uk.
// Provided under the terms of the MIT license -
// http://opensource.org/licenses/MIT. Cape Guy accepts
// no responsibility for any damages, financial or otherwise,
// incurred as a result of using this code.

using UnityEditor;
using UnityEngine;

/// <summary>@brief
/// Prevents script compilation and reload while in play mode.
/// The editor will show a the spinning reload icon if there are unapplied changes but will not actually
/// apply them until playmode is exited.
/// Note: Script compile errors will not be shown while in play mode.
/// Derived from the instructions here:
/// https://support.unity3d.com/hc/en-us/articles/210452343-How-to-stop-automatic-assembly-compilation-from-script
/// </summary>
//[InitializeOnLoad]
//public class DisableScripReloadInPlayMode
//{
//    static DisableScripReloadInPlayMode()
//    {
//        EditorApplication.playModeStateChanged
//            += OnPlayModeStateChanged;
//    }

//    static void OnPlayModeStateChanged(PlayModeStateChange stateChange)
//    {
//        switch (stateChange)
//        {
//            case (PlayModeStateChange.EnteredPlayMode):
//                {
//                    EditorApplication.LockReloadAssemblies();
//                    Debug.Log("Assembly Reload locked as entering play mode");
//                    break;
//                }
//            case (PlayModeStateChange.ExitingPlayMode):
//                {
//                    Debug.Log("Assembly Reload unlocked as exiting play mode");
//                    EditorApplication.UnlockReloadAssemblies();
//                    break;
//                }
//        }
//    }

//}





//using UnityEditor;
//using UnityEngine;

//[InitializeOnLoad]
//public class StopPlayingOnRecompile
//{

//    static StopPlayingOnRecompile()
//    {
//        //Since InitializeOnLoad is called when unity starts AND every time you hit play, we will unsubscribe and resubscribe to avoid duplicates.
//        //Might not be needed to do since EditorApplication.update might be cleared on every InitializeOnLoad call?
//        EditorApplication.update -= StopPlayingIfRecompiling;
//        EditorApplication.update += StopPlayingIfRecompiling;
//    }

//    static void StopPlayingIfRecompiling()
//    {
//        Debug.Log("StopPlayingIfRecompiling");

//        if (EditorApplication.isCompiling && EditorApplication.isPlaying)
//        {
//            Debug.Log("Force stop playing");
//            EditorApplication.isPlaying = false;
//        }
//    }
//}
#endif
