#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MidiPlayerTK
{

    // http://trinary.tech/category/mec/mec-pro/

    /// <summary>@brief
    /// Window editor for the setup of MPTK
    /// </summary>

    // ensure class initializer is called whenever scripts recompile
    [ExecuteAlways, InitializeOnLoadAttribute]
    public class TestMidiEditorWindow : EditorWindow
    {
        MidiEditorLib MidiPlayerEditor;

        static private TestMidiEditorWindow window;

        // % (ctrl on Windows, cmd on macOS), # (shift), & (alt).
        //[MenuItem(Constant.MENU_MAESTRO + "/Test Midi Editor", false, 5)]
        static public void InitSequencer()
        {
            // Get existing open window or if none, make a new one:
            try
            {
                if (Application.isPlaying)
                    Debug.LogWarning("Mode MIDI Editor Playing is no possible when application is running");
                else
                {
                    window = ScriptableObject.CreateInstance(typeof(TestMidiEditorWindow)) as TestMidiEditorWindow;
                    window.Show();
                    window.titleContent = new GUIContent("MIDI Sequencer Window");
                    window.minSize = new Vector2(300, 200);
                    //Debug.Log($"Init name:{window.name} at {window.position}");
                }

            }
            catch (Exception /*ex*/)
            {
                //MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        private void Awake()
        {
            MidiPlayerEditor = new MidiEditorLib("TestMidiEditor", true, true);
        }

        private void OnEnable()
        {
            Debug.Log("OnEnable TestMidiEditorWindow ... Application.isPlaying:" + Application.isPlaying);
            EditorApplication.playModeStateChanged += LogPlayModeState;
        }


        private void LogPlayModeState(PlayModeStateChange state)
        {
            Debug.Log(">>> LogPlayModeState MidiSequencerWindow" + state);
            //if (state == PlayModeStateChange.EnteredPlayMode)
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                //if (MidiPlayerEditor != null) //strangely, this property can be null when window is close
                //    MidiPlayerEditor.DestroyMidiObject();
                Close(); // call OnDestroy
            }
            Debug.Log("<<< LogPlayModeState MidiSequencerWindow" + state);
        }

        void OnDestroy()
        {
            EditorApplication.playModeStateChanged -= LogPlayModeState;
            if (MidiPlayerEditor != null) //strangely, this property can be null when window is close
                MidiPlayerEditor.DestroyMidiObject();
            else
                Debug.LogWarning("MidiPlayerEditor is null");
        }

        void OnGUI()
        {
            try
            {
                MPTKGui.LoadSkinAndStyle();

                GUILayout.BeginHorizontal(GUILayout.Width(300));
                GUILayout.Label("MIDI", MPTKGui.styleBold, GUILayout.Width(150));
                if (GUILayout.Button("Play", GUILayout.Width(100)))
                {
                    // Select a MIDI from the MIDI DB (with exact name)
                    //midiFilePlayer.MPTK_MidiName = "Bach - Fugue"; 
                    //midiFilePlayer.MPTK_MidiName = "All Night Long";
                    MidiPlayerEditor.MidiPlayer.MPTK_MidiIndex = 11;
                    MidiPlayerEditor.MidiPlayer.OnEventNotesMidi.AddListener(MidiReadEvents);

                    // Play the MIDI file
                    MidiPlayerEditor.MidiPlayer.MPTK_Play();
                }
                if (GUILayout.Button("Stop", GUILayout.Width(100)))
                {
                    MidiPlayerEditor.MidiPlayer.MPTK_Stop();
                }
                GUILayout.EndHorizontal();
            }
            //catch (ExitGUIException) { }
            catch (Exception ex)
            {
                Debug.Log(ex.Message);
                //MidiPlayerGlobal.ErrorDetail(ex);
            }
        }
        public void MidiReadEvents(List<MPTKEvent> midiEvents)
        {
            foreach (MPTKEvent midiEvent in midiEvents)
            {
                switch (midiEvent.Command)
                {
                    case MPTKCommand.ControlChange:
                        Debug.LogFormat($"ControlChange Channel:{midiEvent.Channel} Value:{midiEvent.Value}");
                        break;

                    case MPTKCommand.NoteOn:
                        Debug.LogFormat($"NoteOn Channel:{midiEvent.Channel} {midiEvent.Value} Velocity:{midiEvent.Velocity} Duration:{midiEvent.Duration}");
                        break;

                    case MPTKCommand.MetaEvent:
                        switch (midiEvent.Meta)
                        {
                            case MPTKMeta.TextEvent:
                            case MPTKMeta.Lyric:
                            case MPTKMeta.Marker:
                            case MPTKMeta.Copyright:
                            case MPTKMeta.SequenceTrackName:
                                Debug.LogFormat($"MetaEvent Channel:{midiEvent.Channel} Meta:{midiEvent.Meta} Info:{midiEvent.Info}");
                                break;
                        }
                        break;
                }

            }
        }
        void OnInspectorUpdate()
        {
            // Repaint();
        }

    }
}
#endif
