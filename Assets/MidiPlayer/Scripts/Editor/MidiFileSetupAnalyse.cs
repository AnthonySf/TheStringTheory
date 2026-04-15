#if UNITY_EDITOR
using MPTK.NAudio.Midi;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace MidiPlayerTK
{

    /// <summary>@brief
    /// Window editor for the setup of MPTK
    /// </summary>
    public partial class MidiFileSetupWindow : EditorWindow
    {
        //static private List<string> infoEvents;
        static private List<List<MidiEvent>> trackMidiEvents;
        static public int PageToDisplay = 0;
        static public int PageCount = 0;
        const int MAXLINEPAGE = 300;
        static bool withMeta = true, withNoteOn = true, withNoteOff = true, withPatchChange = true, withPitchWheelChange = true;
        static bool withControlChange = true, withAfterTouch = true, withOthers = true;

        static private void ReadRawMidiEvents()
        {
            if (IndexEditItem >= 0 && IndexEditItem < MidiPlayerGlobal.CurrentMidiSet.MidiFiles.Count)
            {
                PageToDisplay = 0;
                PageCount = 0;
                scrollPosAnalyze = Vector2.zero;
                try
                {
                    string midifileName = MidiPlayerGlobal.CurrentMidiSet.MidiFiles[IndexEditItem];
                    //infoEvents = MidiScan.GeneralInfoNAudio(midifile, withNoteOn, withNoteOff, withControlChange, withPatchChange, withAfterTouch, withMeta, withOthers);
                    trackMidiEvents = MidiScan.GetEventFromRawMIDI(midifileName, withNoteOn, withNoteOff, withPitchWheelChange, withControlChange, withPatchChange, withAfterTouch, withMeta, withOthers);
                    PageCount = MidiScan.CountMidiEvents / MAXLINEPAGE + 1;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(ex);
                }

                //if (withDisplayStat)
                //    DisplayStat();
            }

        }

        private void ShowMidiRawCommand()
        {
            try
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("All", MPTKGui.Button))
                {
                    withMeta = withNoteOn = withNoteOff = withPitchWheelChange = withControlChange = withPatchChange = withAfterTouch = withOthers = true;
                    ReadRawMidiEvents();
                }

                if (GUILayout.Button("None", MPTKGui.Button))
                {
                    withMeta = withNoteOn = withNoteOff = withControlChange = withPitchWheelChange = withPatchChange = withAfterTouch = withOthers = false;
                    ReadRawMidiEvents();
                }

                bool filter = GUILayout.Toggle(withNoteOn, "Note On", MPTKGui.styleToggle);
                if (filter != withNoteOn) { withNoteOn = filter; ReadRawMidiEvents(); }
                filter = GUILayout.Toggle(withNoteOff, "Note Off", MPTKGui.styleToggle);
                if (filter != withNoteOff) { withNoteOff = filter; ReadRawMidiEvents(); }
                filter = GUILayout.Toggle(withPitchWheelChange, "Pitch Wheel", MPTKGui.styleToggle);
                if (filter != withPitchWheelChange) { withPitchWheelChange = filter; ReadRawMidiEvents(); }
                filter = GUILayout.Toggle(withControlChange, "Control", MPTKGui.styleToggle);
                if (filter != withControlChange) { withControlChange = filter; ReadRawMidiEvents(); }
                filter = GUILayout.Toggle(withPatchChange, "Preset", MPTKGui.styleToggle);
                if (filter != withPatchChange) { withPatchChange = filter; ReadRawMidiEvents(); }
                filter = GUILayout.Toggle(withMeta, "Meta", MPTKGui.styleToggle);
                if (filter != withMeta) { withMeta = filter; ReadRawMidiEvents(); }
                filter = GUILayout.Toggle(withAfterTouch, "Touch", MPTKGui.styleToggle);
                if (filter != withAfterTouch) { withAfterTouch = filter; ReadRawMidiEvents(); }

                filter = GUILayout.Toggle(withOthers, "Others", MPTKGui.styleToggle);
                if (filter != withOthers) { withOthers = filter; ReadRawMidiEvents(); }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{ex}");
            }
            finally
            {
                GUILayout.EndHorizontal();
            }

            try
            {
                // Display settings -- Third line --
                GUILayout.BeginHorizontal();
                float alignHorizontal = 24;

                if (PageToDisplay < 0) PageToDisplay = 0;

                // Change page
                if (GUILayout.Button(MPTKGui.IconFirst, MPTKGui.Button)) { PageToDisplay = 0; scrollPosAnalyze = Vector2.zero; }
                if (GUILayout.Button(MPTKGui.IconPrevious, MPTKGui.Button)) { PageToDisplay--; scrollPosAnalyze.y = 9999; Repaint(); }
                GUILayout.Label($"Page {(PageToDisplay + 1)} / {PageCount} - MIDI events:{MidiScan.CountMidiEvents}", MPTKGui.styleLabelCenter, GUILayout.Height(alignHorizontal));
                if (GUILayout.Button(MPTKGui.IconNext, MPTKGui.Button)) { PageToDisplay++; scrollPosAnalyze = Vector2.zero; }
                if (GUILayout.Button(MPTKGui.IconLast, MPTKGui.Button)) { PageToDisplay = PageCount; scrollPosAnalyze = Vector2.zero; }
                PageToDisplay = Mathf.Clamp(PageToDisplay, 0, PageCount);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{ex}");
            }
            finally
            {
                GUILayout.EndHorizontal();
            }

        }

        private void ShowMidiRawEvent(float startX, float width, float height, float nextAreaY)
        {
            if (trackMidiEvents != null)
            {
                try
                {
                    // Begin area MIDI events list
                    // --------------------------
                    // Why +30 ? Any idea !
                    GUILayout.BeginArea(new Rect(startX, nextAreaY, width, height - nextAreaY + 30), MPTKGui.stylePanelGrayLight);
                    //Debug.Log($"{ height - nextAreaY + 30}");
                    scrollPosAnalyze = GUILayout.BeginScrollView(scrollPosAnalyze);

                    if (PageToDisplay == 0)
                    {
                        List<string> infos = MidiScan.RawScanLegend();
                        foreach (string info in infos)
                            GUILayout.Label(info, MPTKGui.styleLabelFontCourier);
                    }
                    // Foreach MIDI events on the current page
                    // ---------------------------------------
                    int track = 0;
                    int line = 0;
                    foreach (List<MidiEvent> midiEvents in trackMidiEvents)
                    {
                        foreach (MidiEvent midievent in midiEvents)
                        {
                            if (line >= PageToDisplay * MAXLINEPAGE && line < (PageToDisplay + 1) * MAXLINEPAGE)
                            {
                                string sEvent = MidiScan.ConvertnAudioEventToString(midievent, track);
                                if (sEvent != null)
                                    GUILayout.Label(sEvent, MPTKGui.styleLabelFontCourier);
                            }
                            line++;
                        }
                        track++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{ex}");
                }
                finally
                {
                    GUILayout.EndScrollView();
                    GUILayout.EndArea();
                }
            }
        }
    }
}
#endif
