#if UNITY_EDITOR
//using DemoMPTK;
using System;
using UnityEditor;
using UnityEngine;

namespace MidiPlayerTK
{

    /// <summary>@brief
    /// Window editor for the setup of MPTK
    /// </summary>
    public class MenuShortcut : EditorWindow
    {

        // Add a menu item to create MidiFilePlayer GameObjects.
        // Priority 1 ensures it is grouped with the other menu items of the same kind
        // and propagated to the hierarchy dropdown and hierarch context menus.
        [MenuItem("GameObject/Maestro/Add Prefab MidiFilePlayer", false, 10)]
        [MenuItem(Constant.MENU_MAESTRO + "/Add Prefab/MidiFilePlayer", false, 30)]
        static void CreateMidiFilePlayerGameObject(MenuCommand menuCommand)
        {
            CreatePrefab(menuCommand, "MidiFilePlayer", "Assets/MidiPlayer/Prefab/MidiFilePlayer.prefab");
        }

        // Add a menu item to create MidiStreamPlayer GameObjects.
        // Priority 1 ensures it is grouped with the other menu items of the same kind
        // and propagated to the hierarchy dropdown and hierarch context menus.
        [MenuItem("GameObject/Maestro/Add Prefab MidiStreamPlayer", false, 11)]
        [MenuItem(Constant.MENU_MAESTRO + "/Add Prefab/MidiStreamPlayer", false, 31)]
        static void CreateMidiStreamPlayerGameObject(MenuCommand menuCommand)
        {
            CreatePrefab(menuCommand, "MidiStreamPlayer", "Assets/MidiPlayer/Prefab/MidiStreamPlayer.prefab");
        }

        // Deprecated with 2.10.1 - use MidiFile Player in place Add a menu item to create MidiStreamPlayer GameObjects.
        [MenuItem("GameObject/Maestro/Add Prefab MidiFileLoader", false, 12)]
        [MenuItem(Constant.MENU_MAESTRO + "/Add Prefab/MidiFileLoader", false, 32)]
        static void CreateMidiFileLoaderGameObject(MenuCommand menuCommand)
        {
            // Deprecated with 2.10.1 - CreatePrefab(menuCommand, "MidiFileLoader", "Assets/MidiPlayer/Prefab/MidiFileLoader.prefab");
            Debug.LogWarning("Maestro 2.10.1 - MidiFileLoader prefab is deprecated and will be removed. Please consider using MidiFilePlayer");
            Debug.LogWarning("Look the demo TestMidiFileLoad.cs which is using MidiFilePlayer MPTK_Load() method");
            
        }

        // Add a menu item to create MidiStreamPlayer GameObjects.
        // Priority 1 ensures it is grouped with the other menu items of the same kind
        // and propagated to the hierarchy dropdown and hierarch context menus.
        [MenuItem("GameObject/Maestro/Pro/Add Prefab MidiExternalPlay", false, 13)]
        [MenuItem(Constant.MENU_MAESTRO + "/Add Prefab/MidiExternalPlay [Pro]", false, 33)]
        static void CreateMidiExternalPlayGameObject(MenuCommand menuCommand)
        {
            CreatePrefab(menuCommand, "MidiExternalPlay", "Assets/MidiPlayer/Prefab/Pro/MidiExternalPlay.prefab");
        }
        // Add a menu item to create MidiStreamPlayer GameObjects.
        // Priority 1 ensures it is grouped with the other menu items of the same kind
        // and propagated to the hierarchy dropdown and hierarch context menus.
        [MenuItem("GameObject/Maestro/Pro/Add Prefab MidiInReader", false, 14)]
        [MenuItem(Constant.MENU_MAESTRO + "/Add Prefab/MidiInReader [Pro]", false, 34)]
        static void CreateMidiInReaderGameObject(MenuCommand menuCommand)
        {
            CreatePrefab(menuCommand, "MidiInReader", "Assets/MidiPlayer/Prefab/Pro/MidiInReader.prefab");
        }

        // Add a menu item to create MidiStreamPlayer GameObjects.
        // Priority 1 ensures it is grouped with the other menu items of the same kind
        // and propagated to the hierarchy dropdown and hierarch context menus.
        [MenuItem("GameObject/Maestro/Pro/Add Prefab MidiListPlayer", false, 15)]
        [MenuItem(Constant.MENU_MAESTRO + "/Add Prefab/MidiListPlayer [Pro]", false, 35)]
        static void CreateMidiListPlayerGameObject(MenuCommand menuCommand)
        {
            CreatePrefab(menuCommand, "MidiListPlayer", "Assets/MidiPlayer/Prefab/Pro/MidiListPlayer.prefab");
        }
        // Add a menu item to create MidiStreamPlayer GameObjects.
        // Priority 1 ensures it is grouped with the other menu items of the same kind
        // and propagated to the hierarchy dropdown and hierarch context menus.
        [MenuItem("GameObject/Maestro/Pro/Add Prefab MidiSpatializer", false, 16)]
        [MenuItem(Constant.MENU_MAESTRO + "/Add Prefab/MidiSpatializer [Pro]", false, 36)]
        static void CreateMidiSpatializerGameObject(MenuCommand menuCommand)
        {
            CreatePrefab(menuCommand, "MidiSpatializer", "Assets/MidiPlayer/Prefab/Pro/MidiSpatializer.prefab");
        }

        static void CreatePrefab(MenuCommand menuCommand, string prefabName, string prefabPath)
        {
            UnityEngine.Object prefab = AssetDatabase.LoadAssetAtPath(prefabPath, typeof(GameObject));
            if (prefab == null)
            {
                try
                {
                    PopupWindow.Show(new Rect(200, 200, 180, 18), new GetFullVersion());
                }
                catch (Exception)
                {

                }
                Debug.LogWarning($"Prefab {prefabName} not found or not the Pro version.");
            }
            else
            {
                // before v2.10.0 GameObject go = PrefabUtility.InstantiateAttachedAsset(prefab) as GameObject;
                GameObject go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                go.name = prefabName;
                // Ensure it gets reparented if this was a context click (otherwise does nothing)
                GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
                // Register the creation in the undo system
                Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
                Selection.activeObject = go;
            }
        }
    }
}
#endif
