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
    [CustomEditor(typeof(MidiFilePlayer))]
    public class MidiFilePlayerEditor : Editor
    {
        private SerializedProperty CustomEventListNotesEvent;
        private SerializedProperty CustomEventStartPlayMidi;
        private SerializedProperty CustomEventEndPlayMidi;

        private static MidiFilePlayer instance;

        //static public SelectMidiWindow winSelectMidi;

        private MidiCommonEditor commonEditor;

        public void ClearLog()
        {
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEditor.Editor));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            method.Invoke(new object(), null);
        }

        void OnEnable()
        {
            try
            {
                instance = (MidiFilePlayer)target;
                CustomEventStartPlayMidi = serializedObject.FindProperty("OnEventStartPlayMidi");
                CustomEventListNotesEvent = serializedObject.FindProperty("OnEventNotesMidi");
                CustomEventEndPlayMidi = serializedObject.FindProperty("OnEventEndPlayMidi");

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

                //if (winSelectMidi != null)
                //{
                //    //Debug.Log("OnEnable winSelectMidi " + winSelectMidi.Title);
                //    winSelectMidi.SelectedIndexMidi = instance.MPTK_MidiIndex;
                //    winSelectMidi.Repaint();
                //    winSelectMidi.Focus();
                //}

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
                    //SelectMidiWindow.winSelectMidi.Close();
                    //SelectMidiWindow.winSelectMidi = null;
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
            //if (instance.midiFilter != null)
            //    instance.midiFilter.MidiLoad(midiindex);
            instance.MPTK_MidiIndex = midiindex;
            instance.MPTK_RePlay();
            MidiCommonEditor.SetSceneChangedIfNeed(instance, true);
        }

        public override void OnInspectorGUI()
        {
            try
            {
                //DrawHeader();
                GUI.changed = false;
                GUI.color = Color.white;
                if (commonEditor == null) commonEditor = ScriptableObject.CreateInstance<MidiCommonEditor>();

                Event e = Event.current;

                commonEditor.DrawCaption(instance, "Midi File Player - Play MIDI from the internal MIDI DB.", "https://paxstellar.fr/midi-file-player-detailed-view-2/", "d7/deb/class_midi_player_t_k_1_1_midi_file_player.html#details");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Select MIDI ", "Select Midi File to play"), GUILayout.Width(150));

                if (MidiPlayerGlobal.CurrentMidiSet.MidiFiles != null && MidiPlayerGlobal.CurrentMidiSet.MidiFiles.Count > 0)
                {
                    if (GUILayout.Button(new GUIContent(instance.MPTK_MidiIndex + " - " + instance.MPTK_MidiName, "Selected MIDI File to play"), GUILayout.Height(30)))
                        InitWinSelectMidi(instance.MPTK_MidiIndex, MidiChanged);
                }
                else
                {
                    EditorGUILayout.LabelField(MidiPlayerGlobal.ErrorNoMidiFile);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();

                commonEditor.AllPrefab(instance);
                commonEditor.MidiFileParameters(instance);
                commonEditor.SpacialParameters(instance);
                commonEditor.SynthParameters(instance, serializedObject);
                instance.showEvents = MidiCommonEditor.DrawFoldoutAndHelp(instance.showEvents, "Show Unity Events", "https://paxstellar.fr/midi-file-player-detailed-view-2/#Foldout-Events");
                if (instance.showEvents)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(CustomEventStartPlayMidi);
                    EditorGUILayout.PropertyField(CustomEventListNotesEvent);
                    EditorGUILayout.PropertyField(CustomEventEndPlayMidi);
                    serializedObject.ApplyModifiedProperties();
                    EditorGUI.indentLevel--;
                }
                commonEditor.MidiFileInfo(instance);

#if SHOWDEFAULT
                instance.showDefault = EditorGUILayout.Foldout(instance.showDefault, "Show default editor");
                if (instance.showDefault)
                {
                    EditorGUI.indentLevel++;
                    commonEditor.DrawAlertOnDefault();
                    // Sometime, catch an error                        
                    try { DrawDefaultInspector(); } catch { }
                    EditorGUI.indentLevel--;
                }
#endif
                MidiCommonEditor.SetSceneChangedIfNeed(instance, GUI.changed);
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        //protected override void OnHeaderGUI()
        //{
        //    EditorGUILayout.LabelField("for test", myStyle.LabelAlert);
        //    //custom GUI code here
        //}
    }
}
#endif
