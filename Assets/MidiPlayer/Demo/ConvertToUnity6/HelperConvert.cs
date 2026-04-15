#if UNITY_EDITOR
#if UNITY_6000_0_OR_NEWER
using MidiPlayerTK;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Trigger after package import or script compilation,
// so no (simple) way to detect Unity Package Manager installs/updates.
//public class MPTK_URPImportHook : AssetPostprocessor
//{
//    static void OnPostprocessAllAssets(
//        string[] importedAssets,
//        string[] deletedAssets,
//        string[] movedAssets,
//        string[] movedFromAssetPaths)
//    {
//        bool maestroImport = false;
//        foreach (string asset in importedAssets)
//        {
//            if (asset.Contains("MidiPlayer"))
//            {
//                // Delay to ensure scripts are compiled
//                maestroImport = true;
//                break;
//            }
//        }
//        if (maestroImport) EditorApplication.delayCall += ManualConversionMenu;
//    }

public class MPTK_Unity6_Helper
{
    [MenuItem(Constant.MENU_MAESTRO + "/Convert demo to Unity 6", false, 51)]
    static void ManualConversionMenu()
    {
        if (EditorUtility.DisplayDialog(
            "Convert to URP",
            "Unity 6000+ detected.\n\n" +
            "Demonstrations in this project are currently using the Built-in Render Pipeline.\n" +
            "Do you want to automatically convert them to the Universal Render Pipeline (URP)?",
            "Yes, convert",
            "No, keep Built-in"))
        {
            Debug.LogWarning("Within 'Render Pipeline Converter' select all 'converters' and click 'Initialize And Convert' *** irreversible changes ***");
            Debug.Log("More information here https://paxstellar.fr/news/");
            EditorApplication.ExecuteMenuItem("Window/Rendering/Render Pipeline Converter");
        }
        if (EditorUtility.DisplayDialog(
            "Select Input Manager",
            "Unity 6000+ detected.\n\n" +
            "Demonstrations in this project are currently using the old and new Input Manager.\n" +
            "Do you want to authorize both Input Manager?\n" +
            "Search for 'Configuration' then 'Active Input Handling' and select 'Both'",
            "Yes",
            "No"))
        {
            Debug.LogWarning("Within 'Project Settings' search for 'Configuration/Active Input Handling' and select 'Both'");
            Debug.Log("More information here https://paxstellar.fr/news/");
            EditorApplication.ExecuteMenuItem("Edit/Project Settings...");
        }
    }
}
#endif

#endif
