#if UNITY_EDITOR
#define SHOWDEFAULT
using System;
using UnityEditor;
using UnityEngine;

namespace MidiPlayerTK
{
    /// <summary>@brief
    /// Inspector for the midi global player component
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MidiFileLoader))]
    public class MidiFileLoaderEditor : Editor
    {
        private static MidiFileLoader instance;
        //private SelectMidiWindow winSelectMidi;
        private MidiCommonEditor commonEditor;


#if SHOWDEFAULT
        private static bool showDefault;
#endif

        void OnEnable()
        {
            try
            {
                instance = (MidiFileLoader)target;

                if (!Application.isPlaying)
                {
                    // Load description of available soundfont
                    if (MidiPlayerGlobal.CurrentMidiSet == null || MidiPlayerGlobal.CurrentMidiSet.ActiveSounFontInfo == null)
                    {
                        MidiPlayerGlobal.InitPath();
                        ToolsEditor.LoadMidiSet();
                        ToolsEditor.CheckMidiSet();
                    }
                }

                if (SelectMidiWindow.winSelectMidi != null)
                {
                    //Debug.Log("OnEnable winSelectMidi " + winSelectMidi.Title);
                    SelectMidiWindow.winSelectMidi.SelectedIndexMidi = instance.MPTK_MidiIndex;
                    SelectMidiWindow.winSelectMidi.Repaint();
                    SelectMidiWindow.winSelectMidi.Focus();
                }

                //EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        private void EditorApplication_playModeStateChanged(PlayModeStateChange obj)
        {
            //Debug.Log("EditorApplication_playModeStateChanged " + obj.ToString());
        }

        private void OnDisable()
        {
            try
            {
                if (SelectMidiWindow.winSelectMidi != null)
                {
                    //Debug.Log("OnDisable winSelectMidi " + winSelectMidi.Title);
                    SelectMidiWindow.winSelectMidi.Close();
                }
            }
            catch (Exception)
            {
            }
        }

        public void InitWinSelectMidi(int selected, Action<object, int> select)
        {
            // Get existing open window or if none, make a new one:
            try
            {
                SelectMidiWindow.winSelectMidi = EditorWindow.GetWindow<SelectMidiWindow>(true, "Select a MIDI File");
                SelectMidiWindow.winSelectMidi.OnSelect = select;
                SelectMidiWindow.winSelectMidi.SelectedIndexMidi = selected;
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        private void MidiChanged(object tag, int midiindex)
        {
            //Debug.Log("MidiChanged " + midiindex + " for " + tag);
            instance.MPTK_MidiIndex = midiindex;
            //MidiCommonEditor.SetSceneChangedIfNeed(instance, true);
        }
       
        public override void OnInspectorGUI()
        {
            try
            {
                GUI.changed = false;
                GUI.color = Color.white;

                if (commonEditor == null) commonEditor = ScriptableObject.CreateInstance<MidiCommonEditor>();

                if (MidiPlayerGlobal.CurrentMidiSet != null || MidiPlayerGlobal.CurrentMidiSet.MidiFiles == null || MidiPlayerGlobal.CurrentMidiSet.MidiFiles.Count == 0)
                {
                    commonEditor.DrawCaption(null, "MIDI File Loader - Load a MIDI and read the MIDI message.", "https://paxstellar.fr/prefab-midifileloader/", "df/d2e/class_midi_player_t_k_1_1_midi_file_loader.html#details");
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent("Select MIDI ", "Select MIDI File to load"), GUILayout.Width(150));

                    if (GUILayout.Button(new GUIContent(instance.MPTK_MidiIndex + " - " + instance.MPTK_MidiName, "Selected MIDI File to load"), GUILayout.Height(30)))
                        InitWinSelectMidi(instance.MPTK_MidiIndex, MidiChanged);
                    EditorGUILayout.EndHorizontal();

                    instance.MPTK_KeepNoteOff = EditorGUILayout.Toggle(new GUIContent("Keep MIDI NoteOff", "Keep MIDI NoteOff and NoteOn with Velocity=0 (need to restart the playing MIDI)"), instance.MPTK_KeepNoteOff);
                    instance.MPTK_KeepEndTrack = EditorGUILayout.Toggle(new GUIContent("Keep MIDI EndTrack", "When set to true, meta MIDI event End Track are keep and these MIDI events are taken into account for calculate the full duration of the MIDI."), instance.MPTK_KeepEndTrack);
                    instance.MPTK_LogLoadEvents = EditorGUILayout.Toggle(new GUIContent("Log MIDI Events", "Log information about each MIDI events read."), instance.MPTK_LogLoadEvents);
                    if (instance.MPTK_LogLoadEvents)
                        EditorGUILayout.LabelField("Setting 'Log MIDI Events' to true could increase greatly the load time. To be used only for debug.", MPTKGui.styleAlertRed);

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(new GUIContent("Load", "")))
                    {
                        instance.MPTK_Load();
                        instance.MPTK_MidiLoaded.MPTK_DisplayMidiAttributes();
                    }
                    if (GUILayout.Button(new GUIContent("Previous", "")))
                    {
                        instance.MPTK_Previous();
                        instance.MPTK_Load();
                        instance.MPTK_MidiLoaded.MPTK_DisplayMidiAttributes();
                    }
                    if (GUILayout.Button(new GUIContent("Next", "")))
                    {
                        instance.MPTK_Next(); EditorGUILayout.EndHorizontal();
                        instance.MPTK_Load();
                        instance.MPTK_MidiLoaded.MPTK_DisplayMidiAttributes();
                    }

                    EditorGUILayout.EndHorizontal();

#if SHOWDEFAULT
                    showDefault = EditorGUILayout.Foldout(showDefault, "Show default editor");
                    if (showDefault)
                    {
                        EditorGUI.indentLevel++;
                        // Sometime, catch an error                        
                        try { DrawDefaultInspector(); } catch { }
                        EditorGUI.indentLevel--;
                    }
#endif

                }
                else
                {
                    MidiCommonEditor.ErrorNoMidiFile();
                }

                MidiCommonEditor.SetSceneChangedIfNeed(instance, GUI.changed);
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }


    }
}
#endif
