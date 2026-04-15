#if UNITY_EDITOR
#define SHOWDEFAULT
using UnityEditor;
using UnityEngine;

namespace MidiPlayerTK
{
    /// <summary>@brief
    /// Inspector for the midi global player component
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MidiStreamPlayer))]
    public class MidiStreamPlayerEditor : Editor
    {
        private static MidiStreamPlayer instance;
        private MidiCommonEditor commonEditor;

        void OnEnable()
        {
            try
            {
                instance = (MidiStreamPlayer)target;
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
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        public override void OnInspectorGUI()
        {
            try
            {
                GUI.changed = false;
                GUI.color = Color.white;
                if (commonEditor == null) commonEditor = ScriptableObject.CreateInstance<MidiCommonEditor>();

                //mDebug.Log(Event.current.type);

                commonEditor.DrawCaption(instance, "Midi Stream Player - Create your music with your algo.", "https://paxstellar.fr/midi-file-player-detailed-view-2-2/", "d9/d1e/class_midi_player_t_k_1_1_midi_stream_player.html#details");
                commonEditor.AllPrefab(instance);
                commonEditor.SpacialParameters(instance);
                commonEditor.SynthParameters(instance, serializedObject);
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

    }

}
#endif
