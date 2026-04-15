#if UNITY_EDITOR
//#define MPTK_PRO
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace MidiPlayerTK
{

    [InitializeOnLoad]
    class NoErrorValidator
    {
        static public bool CantChangeAudioConfiguration;
        static NoErrorValidator()
        {
            //Debug.Log("NoErrorValidator");  
            //CompilationPipeline.assemblyCompilationStarted += CompileStarted;
            CompilationPipeline.compilationStarted += CompileStarted;
            CompilationPipeline.assemblyCompilationFinished += CompileFinish;
#if xxxxxUNITY_IOS
        // Now always enabled but without any garantee!
        Debug.Log("Platform iOS selected, change audio configuration is disabled.");
        CantChangeAudioConfiguration = true;
#else
            // Always false, keep it for compatibility ?
            CantChangeAudioConfiguration = false;
#endif
        }

        private static void CompileStarted(object obj)
        {
            Debug.Log("Compilation Started ...");
            // Better to let Unity doing what is set in Edit / Preferences / Script Changes when playing
            //if (EditorApplication.isPlaying)
            //{
            //    Debug.Log("Stop Playing...");
            //    EditorApplication.isPlaying = false;
            //}
            // in case of a call back has been set, it's mandatory to unset it to avoid crash
#if MPTK_PRO
            MidiKeyboard.MPTK_UnsetRealTimeRead();
#endif
            Routine.KillCoroutines();

            //#if UNITY_IOS
            //        Debug.Log("Platform iOS selectedInFilterList, change audio configuration is disabled.");
            //#endif
#if NET_LEGACY
        Debug.LogWarning(".NET 2.0 is selected, .NET 4.x API compatibility level is recommended.");
#endif
        }

        static private void CompileFinish(string s, CompilerMessage[] compilerMessages)
        {
            Debug.Log($"Compilation {s} Finished, error: " + compilerMessages.Count(m => m.type == CompilerMessageType.Error));
            // not working CleanPackage.CleaningPackages(null);
            //if (compilerMessages.Count(m => m.type == CompilerMessageType.Error) > 0)
            //EditorApplication.Exit(-1);
            //Debug.Log("compilerMessages:" + compilerMessages.Count(m => m.type == CompilerMessageType.Error));
        }
    }

#if EVENT_NOT_WORKING_AS_NEEDED
    // No working because compilation error prevent to trigger event.
    // Using CompileFinish is not working because new asset are not yet loaded ;-)
    // So, I still don't have a solution for cleaning when upgrading versions... that's sad.
    public class CleanPackage
    {
        //[MenuItem(Constant.MENU_MAESTRO + "/Cleaning MPTK Packages", false, 53)]
        static public void CleaningPackages(MenuCommand menuCommand)
        {
            CleanDelete(Path.Combine(Application.dataPath, "MidiPlayer/Scripts/MPTKGameObject/Pro/MPTKEffectSoundFont.cs"));
            CleanDelete(Path.Combine(Application.dataPath, "MidiPlayer/Scripts/MPTKGameObject/Pro/MPTKEffectUnity.cs"));
        }

        static void CleanDelete(string fileToDelete)
        {
            if (File.Exists(fileToDelete))
            {
                Debug.Log($"MPTK Cleaning - Delete {fileToDelete}");
                File.Delete(fileToDelete);
            }
            else
                Debug.Log($"MPTK Cleaning - Not found {fileToDelete}");
        }
    }

    [InitializeOnLoad]
    public class PackageImportHandler : MonoBehaviour
    {
        static PackageImportHandler()
        {
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private static void OnProjectChanged()
        {
            Debug.Log("Project changed! This could indicate a package import.");
            CleanPackage.CleaningPackages(null);
        }
    }
    class MyAllPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            // Call when a new asset has been found, at compilation even with compilation error    
            Debug.Log($"OnPostprocessAllAssets didDomainReload:{didDomainReload} {importedAssets.Length}");
            //if (didDomainReload || importedAssets.Length > 0)
            //    CleanPackage.CleaningPackages(null);
            //foreach (string str in importedAssets)
            //{
            //    Debug.Log("Reimported Asset: " + str);
            //}
            //foreach (string str in deletedAssets)
            //{
            //    Debug.Log("Deleted Asset: " + str);
            //}

            //for (int i = 0; i < movedAssets.Length; i++)
            //{
            //    Debug.Log("Moved Asset: " + movedAssets[i] + " from: " + movedFromAssetPaths[i]);
            //}

            //if (didDomainReload)
            //{
            //    Debug.Log("Domain has been reloaded");
            //}
        }
    }
#endif //EVENT_NOT_WORKING_AS_NEEDED
}
#endif // UNITY_EDITOR
