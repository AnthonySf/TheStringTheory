#if UNITY_EDITOR
//#define MPTK_PRO
//#define DEBUG_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MidiPlayerTK
{

    /// <summary>@brief
    /// MIDI Editor
    ///     Features:
    ///         Create a MIDI sequence from scratch.
    ///         Load a MIDI sequence from Maestro MIDI DB or an external MIDI file.
    ///         Save a MIDI sequence to Maestro MIDI DB or to an external MIDI file.
    ///         Keep current edition safe even when compiling or relaunching Unity editor (serialize/deserialize).
    ///         Display MIDI events by channel with a piano roll view
    ///         Create/Modify properties of notes and presets change.
    ///         Mouse editing functions: drag & drop event, change length.
    ///         Editing with quantization (whole, half, quarter, ...).
    ///         Integrated MIDI player, playing available when editing (but not when running).
    ///         Looping on the whole sequence or between two points.
    ///         Lean mode for looping at end (immediately, when all voices are released or when all voices are finished).
    ///         
    ///     New features and issues corrected 
    ///          -- 2.9.1 --
    ///         Current playing position(vertical red line) not drawn when playing if not following the MIDI events.
    ///         Key Enter/Return validate value entered for a MIDI event.
    ///          -- 2.10.0 --
    ///         Display measure according to the time signature.
    ///         With following mode, the horizontal scroll is not working, now following is disabled when scroll is move.
    ///         Enhance the time banner drawing.
    ///         Go to next or previous measure.
    ///         Draw the time banner with real time in relation of tempo change MIDI events.
    ///         Investigate why midiPlayerGlobal.InitInstance() takes time occasionally: sometime Unity take more time to load the samples. Nothing can be done.
    ///         Hide/show channel
    ///         Disable/enable playing by channel.
    ///         Add tempo event section for modification.
    ///         Add text event edition + others meta.
    ///         Integrate new Inner loop (enhance accuracy, based on tick value)
    ///         
    ///     Bugs to be corrected for the next version
    ///     
    ///     In progress:
    ///                  
    ///     Backlog by priority order:
    ///         Copy/paste event? multi events?
    ///         Merging classes AreaUI and SectionAll?
    ///         Undo/Redo.
    ///         Add velocity edition with Piano roll UI.
    ///         Add control change edition with Piano roll UI.
    ///         Playing and editing in run mode, feasibility ?
    ///         Redesign color and texture like MIDI event, keyboard ... 
    ///         Specific MIDI events for integration with Unity.
    ///         Multi events selection, feasibility ?
    ///         Helper for building chords.
    ///         Helper for scale.
    ///         Helper for chords progression?
    ///         Percussion library integrated in MIDI DB?
    ///         Add effects section.
    ///         Partial import/insert of MIDI: select channels, select tick. Example: import only drum from a lib.
    ///         Manage MIDI tracks, useful?
    ///         Resizable panel, like between piano and note, useful?
    ///         Rhythm generator with Euclidean algo.
    ///         Send MIDI events to an external MIDI keyboard + MIDI beat clock?
    ///         Add others meta event than text event for modification
    ///         
    ///         
    /// </summary>
    //[ExecuteAlways, InitializeOnLoadAttribute]
    public partial class MidiEditorWindow : EditorWindow
    {
        static private MidiEditorWindow window;

        // % (ctrl on Windows, cmd on macOS), # (shift), & (alt).
#if MPTK_PRO
        [MenuItem(Constant.MENU_MAESTRO + "/Midi Editor &E", false, 12)]
#else
        [MenuItem(Constant.MENU_MAESTRO + "/Midi Editor [Pro] &E", false, 12)]
#endif
        public static void Init()
        {
#if MPTK_PRO
            try
            {
                MidiPlayerGlobal.InitPath();
                ToolsEditor.LoadMidiSet();
                ToolsEditor.CheckMidiSet();
                AssetDatabase.Refresh();


                if (MidiPlayerGlobal.CurrentMidiSet == null || MidiPlayerGlobal.CurrentMidiSet.ActiveSounFontInfo == null)
                    EditorUtility.DisplayDialog($"MIDI Editor", MidiPlayerGlobal.ErrorNoSoundFont, "OK");
                else if (MidiPlayerGlobal.CurrentMidiSet.ActiveSounFontInfo.PatchCount == 0)
                    EditorUtility.DisplayDialog($"MIDI Editor", MidiPlayerGlobal.ErrorNoPreset, "OK");
                else
                {
                    window = GetWindow<MidiEditorWindow>(false, "MIDI Editor " + Constant.version);
                    //window = GetWindowWithRect<MidiEditorWindow>(new Rect(0, 0, 180, 80),false, "MIDI Editor (beta) - Maestro " + Constant.version);
                    window.wantsMouseMove = true;
                    window.minSize = new Vector2(300, 350);
                }
            }
            catch (Exception /*ex*/)
            {
                //MidiPlayerGlobal.ErrorDetail(ex);
            }
#else
            PopupWindow.Show(new Rect(100, 100, 30, 18), new GetFullVersion());
#endif
        }
    }
}
#endif
