#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace MidiPlayerTK
{

    /// <summary>@brief
    /// Window editor for the setup of MPTK,  
    /// </summary>
    public partial class MidiFileSetupWindow : EditorWindow
    {
        const int MAX_EVENT_PAGE = 250;
        const int WIDTH_BUTTON_PLAYER = 100;
        const int HEIGHT_PLAYER_CMD = 32;
        //const int HEIGHT_SETTING_DISPLAY = 60;
        const int HEIGHT_TITLE = 25;
        const int AREA_SPACE = 4;

        int SelectedEvent = 0;
        List<MPTKEvent> AllMidiEvents;
        List<List<MPTKEvent>> MidiEventsLoaded;
        MPTKEvent LastEventPlayed;
        int CurrentPage = 0;
        bool FollowEvent = true;
        List<MPTKGui.StyleItem> ColumnEvents;
        Vector2 ScrollerMidiPlayer = Vector2.zero;

        MPTKGui.PopupList PopupDisplayTime;
        int DisplayTime;
        List<MPTKGui.StyleItem> PopupFiltersDisplay;
        List<MPTKGui.StyleItem> PopupFiltersTrack;
        List<MPTKGui.StyleItem> PopupFiltersChannel;
        List<MPTKGui.StyleItem> PopupFiltersCommand;

        private void InitPlayer()
        {
            midiPlayerLib.MidiPlayer.MPTK_Volume = 0.5f;
            midiPlayerLib.MidiPlayer.MPTK_Speed = 1f;
        }

        private void InitGUI()
        {
            if (PopupFiltersDisplay == null)
            {
                PopupFiltersDisplay = new List<MPTKGui.StyleItem>
                {
                    new MPTKGui.StyleItem("Ticks", true),
                    new MPTKGui.StyleItem("Seconds", true),
                    new MPTKGui.StyleItem("hh:mm:ss:mmm", true)
                };
                PopupFiltersDisplay[DisplayTime].Selected = true;
            }

            if (PopupFiltersTrack == null)
            {
                // Track column has a dynamic popup to select tracks
                PopupFiltersTrack = new List<MPTKGui.StyleItem>();
                for (int track = 0; track < midiPlayerLib.MidiPlayer.MPTK_TrackCount; track++)
                    PopupFiltersTrack.Add(new MPTKGui.StyleItem($"Tracks {track}", true, true));
                //ColumnEvents[4].ItemPopupContent = PopupFiltersTrack;
                //// Force refresh of the popup
                //ColumnEvents[4].ItemPopup = null;
                ColumnEvents = null;

            }

            if (PopupFiltersChannel == null)
            {
                PopupFiltersChannel = new List<MPTKGui.StyleItem>();
                for (int channel = 0; channel < 16; channel++)
                    PopupFiltersChannel.Add(new MPTKGui.StyleItem($"Channel {channel}", true, true));
            }

            if (PopupFiltersCommand == null)
            {
                PopupFiltersCommand = new List<MPTKGui.StyleItem>
                {
                    new MPTKGui.StyleItem("Note On", true,true),
                    new MPTKGui.StyleItem("Note Off", true,true),
                    new MPTKGui.StyleItem("Control Change", true, true),
                    new MPTKGui.StyleItem("Preset Change", true, true),
                    new MPTKGui.StyleItem("Meta", true, true),
                    new MPTKGui.StyleItem("Touch", true, true),
                    new MPTKGui.StyleItem("Others", true, true)
                };
            }

            if (ColumnEvents == null)
            {
                ColumnEvents = new List<MPTKGui.StyleItem>
                {
                    new MPTKGui.StyleItem() { Width = 60, Caption = "Index", Tooltip = "Unique index of the MIDI event. Some index are missing because by default, MPTK removes NotOff Events." },
                    new MPTKGui.StyleItem() { Width = 70, Caption = "Tick", Tooltip = "Time in MIDI Tick (part of a Quarter) of the Event since the start of playing the midi file. This time is independent of the Tempo or Speed." },
                    new MPTKGui.StyleItem() { Width = 70, Caption = "Time (s)", Hidden = true, Tooltip = "Real time in seconds of this event from the start of the midi depending the tempo change." },
                    new MPTKGui.StyleItem() { Width = 120, Caption = "Time", Hidden = true, Tooltip = "Real time in seconds of this event from the start of the midi depending the tempo change." },
                    new MPTKGui.StyleItem() { Width = 70, Caption = "Track{*}", Offset = 25, ItemPopupContent = PopupFiltersTrack, ItemPopupWidth = 100, Tooltip = "Track index of the event in the midi. It's just a cool way to regroup MIDI events in a ... track. There is any impact on music played. Track 0 is the first track read from the midi file." },
                    new MPTKGui.StyleItem() { Width = 70, Caption = "Channel{*}", Offset = 25, ItemPopupContent = PopupFiltersChannel, ItemPopupWidth = 100, Tooltip = "MIDI channel fom 0 to 15 (9 for drum). Only one instrument can be selected at a time for a channel." },
                    new MPTKGui.StyleItem() { Width = 200, Caption = "Command {Count}", Offset = 10, ItemPopupContent = PopupFiltersCommand, Tooltip = "MIDI Command. Defined the MIDI action to be done by the synthesizer." },
                    new MPTKGui.StyleItem() { Width = 180, Caption = "Value", Tooltip = "Contains a value in relation with the Command: Note pitch, instrument selected, control change value, text for Meta command. " },
                    new MPTKGui.StyleItem() { Width = 70, Caption = "Velocity", Tooltip = "Velocity between 0 and 127." },
                    new MPTKGui.StyleItem() { Width = 50, Caption = "Duration", Tooltip = "Duration of the note in ticks." }, //9
                    new MPTKGui.StyleItem() { Width = 70, Caption = "Duration (s)", Hidden = true, Tooltip = "Duration of the note in second." }, //10
                    new MPTKGui.StyleItem() { Width = 120, Caption = "Duration", Hidden = true, Tooltip = "Duration of the note." }, //11
                    new MPTKGui.StyleItem() { Width = 120, Caption = "Measure", Tooltip = "Measure/Beat" } //12
                };
            }
        }

        private void EnableAllChannelFilter()
        {
            if (PopupFiltersChannel != null)
                foreach (MPTKGui.StyleItem item in PopupFiltersChannel)
                    item.Selected = true;
        }

        private void LoadMidiFileSelected(int indexEditItem, bool forceChannelEnabled = false)
        {
            try
            {
                midiPlayerLib.MidiPlayer.MPTK_Stop();
                MidiEventsLoaded = null;
                if (indexEditItem >= 0 && indexEditItem < MidiPlayerGlobal.CurrentMidiSet.MidiFiles.Count)
                {
                    midiPlayerLib.MidiPlayer.MPTK_StartPlayAtFirstNote = false; // was true
                    midiPlayerLib.MidiPlayer.MPTK_EnableChangeTempo = true;

                    midiPlayerLib.MidiPlayer.MPTK_ApplyRealTimeModulator = true;
                    midiPlayerLib.MidiPlayer.MPTK_ApplyModLfo = true;
                    midiPlayerLib.MidiPlayer.MPTK_ApplyVibLfo = true;
                    midiPlayerLib.MidiPlayer.MPTK_ReleaseSameNote = true;
                    midiPlayerLib.MidiPlayer.MPTK_KillByExclusiveClass = true;
                    midiPlayerLib.MidiPlayer.MPTK_EnablePanChange = true;
                    midiPlayerLib.MidiPlayer.MPTK_KeepPlayingNonLooped = true;
                    midiPlayerLib.MidiPlayer.MPTK_KeepEndTrack = false; // was true before 2.12.1

                    midiPlayerLib.MidiPlayer.MPTK_MidiIndex = indexEditItem;
                    midiPlayerLib.MidiPlayer.MPTK_KeepNoteOff = false; // was true
                    //MidiPlayerEditor.MidiPlayer.OnEventStartPlayMidi.AddListener(StartPlay);
                    if (midiPlayerLib.MidiPlayer.OnEventNotesMidi == null)
                        midiPlayerLib.MidiPlayer.OnEventNotesMidi = new EventNotesMidiClass();
                    midiPlayerLib.MidiPlayer.OnEventNotesMidi.AddListener(MidiReadEvents);
                    if (forceChannelEnabled)
                    {
                        // Useless, channel are unmuted at start
                        //if (MidiPlayerEditor != null && MidiPlayerEditor.MidiPlayer != null)
                        //    MidiPlayerEditor.MidiPlayer.MPTK_ChannelEnableSet(-1, true);
                        EnableAllChannelFilter();
                    }

                    //Debug.Log("Start Midi " + midiname + " Duration: " + MidiPlayerEditor.MidiPlayer.MPTK_Duration.TotalSeconds + " seconds");
                    midiPlayerLib.MidiPlayer.MPTK_Load(); ;
                    AllMidiEvents = midiPlayerLib.MidiPlayer.MPTK_ReadMidiEvents();
                    MidiEventsLoaded = new List<List<MPTKEvent>>();
                    List<MPTKEvent> pageMidi = null;
                    SelectedEvent = 0;
                    CurrentPage = 0;
                    // Load each MIDI events by page
                    foreach (MPTKEvent midiEvent in AllMidiEvents)
                    {
                        if (pageMidi == null || pageMidi.Count >= MAX_EVENT_PAGE)
                        {
                            pageMidi = new List<MPTKEvent>();
                            MidiEventsLoaded.Add(pageMidi);
                        }
                        pageMidi.Add(midiEvent);
                    }

                    // Force rebuild tracks popup at next OnGUI
                    PopupFiltersTrack = null;
                    InitGUI();

                }
            }
            catch (Exception ex)
            {
                throw new MaestroException($"LoadMidiFileSelected error.{ex.Message}");
            }
        }
        private void PlayMidiFileSelected()
        {
            midiPlayerLib.PlayAudioSource();
            midiPlayerLib.MidiPlayer.MPTK_Play();
        }

        /// <summary>@brief
        /// Event fired by MidiFilePlayer when midi notes are available. 
        /// Set by Unity Editor in MidiFilePlayer Inspector or by script with OnEventNotesMidi.
        /// </summary>
        public void MidiReadEvents(List<MPTKEvent> midiEvents)
        {
            try
            {
                //List<MPTKEvent> eventsOrdered = events.OrderBy(o => o.Value).ToList();
                LastEventPlayed = midiEvents[midiEvents.Count - 1];
                if (FollowEvent)
                {
                    // Find page with this event
                    if (LastEventPlayed.Tick < MidiEventsLoaded[CurrentPage][0].Tick ||
                        LastEventPlayed.Tick > MidiEventsLoaded[CurrentPage][MidiEventsLoaded[CurrentPage].Count - 1].Tick)
                    {
                        // Last pLayed events is on another page
                        for (int page = 0; page < MidiEventsLoaded.Count; page++)
                        {
                            List<MPTKEvent> eventsPage = MidiEventsLoaded[page];
                            if (LastEventPlayed.Tick >= eventsPage[0].Tick && LastEventPlayed.Tick <= eventsPage[eventsPage.Count - 1].Tick)
                            {
                                //Debug.Log($"change to page {page}");
                                CurrentPage = page;
                                break;
                            }
                        }
                    }
                    //Debug.Log($"{scrollerMidiPlayer}");
                    window.Repaint();
                }
            }
            catch (Exception ex)
            {
                throw new MaestroException($"MidiReadEvents.{ex.Message}");
            }
        }


        private void ShowMidiPlayerCommand()
        {
            try // Command ----- Second line -----
            {
                GUILayout.BeginHorizontal();

                AutoMidiPlay = GUILayout.Toggle(AutoMidiPlay, "Auto Playing", MPTKGui.styleToggle, GUILayout.Width(97));

                midiPlayerLib.MidiPlayer.MPTK_MidiAutoRestart = GUILayout.Toggle(midiPlayerLib.MidiPlayer.MPTK_MidiAutoRestart, "Restart MIDI", MPTKGui.styleToggle, GUILayout.Width(100)); // Vertical alignment with MIDI name

                GUIStyle styleButtonPlayAndPause = midiPlayerLib.MidiPlayer.MPTK_IsPlaying && !midiPlayerLib.MidiPlayer.MPTK_IsPaused ? MPTKGui.ButtonHighLight : MPTKGui.Button;
                if (GUILayout.Button("Play", styleButtonPlayAndPause, GUILayout.Width(WIDTH_BUTTON_PLAYER)))
                {
                    if (!midiPlayerLib.MidiPlayer.MPTK_CorePlayer)
                    {
                        EditorUtility.DisplayDialog("MIDI Player ", "Can't play MIDI in non core mode. WebGL build enabled?", "Ok");
                    }
                    else
                    {
                        LoadMidiFileSelected(IndexEditItem, true);
                        PlayMidiFileSelected();
                    }
                }

                styleButtonPlayAndPause = midiPlayerLib.MidiPlayer.MPTK_IsPaused ? MPTKGui.ButtonHighLight : MPTKGui.Button;
                if (GUILayout.Button("Pause", styleButtonPlayAndPause, GUILayout.Width(WIDTH_BUTTON_PLAYER)))
                    if (midiPlayerLib.MidiPlayer.MPTK_IsPaused)
                        midiPlayerLib.MidiPlayer.MPTK_UnPause();
                    else
                        midiPlayerLib.MidiPlayer.MPTK_Pause();

                // Disabled, seems not possible in editor mode
                if (GUILayout.Button("RePlay", MPTKGui.Button, GUILayout.Width(WIDTH_BUTTON_PLAYER)))
                {
                    EnableAllChannelFilter();
                    midiPlayerLib.MidiPlayer.MPTK_RePlay();
                }

                if (GUILayout.Button("Stop", MPTKGui.Button, GUILayout.Width(WIDTH_BUTTON_PLAYER)))
                    midiPlayerLib.MidiPlayer.MPTK_Stop();

                GUILayout.FlexibleSpace();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{ex}");
            }
            finally
            {
                GUILayout.EndHorizontal();
            }

            try // Sliders ----- 3rd line -----
            {
                GUILayout.BeginHorizontal();
                FollowEvent = GUILayout.Toggle(FollowEvent, "Follow", MPTKGui.styleToggle, GUILayout.ExpandWidth(false)/*, GUILayout.Height(alignHorizontal)*/);

                if (DisplayTime == 0)
                {


                    long tickCurrent = midiPlayerLib.MidiPlayer.MPTK_IsPlaying ? midiPlayerLib.MidiPlayer.MPTK_TickCurrent : 0;
                    GUILayout.Label($"Tick: {tickCurrent:000000} / {midiPlayerLib.MidiPlayer.MPTK_TickLast:000000}", MPTKGui.Label, GUILayout.Width(160));
                    long tick = (long)GUILayout.HorizontalSlider((float)tickCurrent, 0f, (float)midiPlayerLib.MidiPlayer.MPTK_TickLast, MPTKGui.HorizontalSlider, MPTKGui.HorizontalThumb, GUILayout.MinWidth(160));
                    if (tick != tickCurrent)
                    {
                        if (Event.current.type == EventType.Used)
                        {
                            //Debug.Log("New tick " + midiFilePlayer.MPTK_TickCurrent + " --> " + tick + " " + Event.current.type);
                            midiPlayerLib.MidiPlayer.MPTK_TickCurrent = tick;
                        }
                    }
                }
                else
                {
                    double currentPosition = midiPlayerLib.MidiPlayer.MPTK_IsPlaying ? Math.Round(midiPlayerLib.MidiPlayer.MPTK_Position / 1000d, 3) : 0;
                    double lastPosition = midiPlayerLib.MidiPlayer.MPTK_PositionLastNote / 1000d;
                    if (DisplayTime == 1)
                    {
                        GUILayout.Label($"Time: {currentPosition:F2} / {lastPosition:F2} sec.", MPTKGui.Label, GUILayout.Width(180));
                    }
                    else if (DisplayTime == 2)
                    {
                        TimeSpan timePos = TimeSpan.FromSeconds(currentPosition);
                        string playTime = string.Format("{0:00}:{1:00}:{2:00}:{3:000}", timePos.Hours, timePos.Minutes, timePos.Seconds, timePos.Milliseconds);
                        TimeSpan lastPos = TimeSpan.FromSeconds(lastPosition);
                        string lastTime = string.Format("{0:00}:{1:00}:{2:00}:{3:000}", lastPos.Hours, lastPos.Minutes, lastPos.Seconds, lastPos.Milliseconds);
                        GUILayout.Label($"Time: {playTime} / {lastTime}", MPTKGui.Label, GUILayout.Width(220));
                    }

                    double newPosition = Math.Round(GUILayout.HorizontalSlider((float)currentPosition, 0f, (float)midiPlayerLib.MidiPlayer.MPTK_Duration.TotalSeconds/*, GUILayout.Width(150)*/,
                            MPTKGui.HorizontalSlider, MPTKGui.HorizontalThumb), 2);

                    if (newPosition != currentPosition)
                    {
                        if (Event.current.type == EventType.Used)
                        {
                            //Debug.Log("New position " + currentPosition + " --> " + newPosition + " " + Event.current.type);
                            midiPlayerLib.MidiPlayer.MPTK_Position = newPosition * 1000d;
                        }
                    }
                }

                float volume = midiPlayerLib.MidiPlayer.MPTK_Volume;
                // Button to restore Volume to 0.5 with label style
                if (GUILayout.Button("Volume: " + volume.ToString("F2"), MPTKGui.Label, GUILayout.Width(80))) volume = 0.5f;
                midiPlayerLib.MidiPlayer.MPTK_Volume = GUILayout.HorizontalSlider(volume, 0f, Constant.MAX_VOLUME, MPTKGui.HorizontalSlider, MPTKGui.HorizontalThumb, GUILayout.Width(100));

                float speed = midiPlayerLib.MidiPlayer.MPTK_Speed;
                // Button to restore speed to 1 with label style
                if (GUILayout.Button("Speed: " + speed.ToString("F2"), MPTKGui.Label, GUILayout.Width(80))) speed = 1f;
                midiPlayerLib.MidiPlayer.MPTK_Speed = GUILayout.HorizontalSlider(speed, Constant.MIN_SPEED, Constant.MAX_SPEED, MPTKGui.HorizontalSlider, MPTKGui.HorizontalThumb, GUILayout.Width(100));

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

                // Select display time format
                MPTKGui.ComboBox(ref PopupDisplayTime, "Display Time: {Label}", PopupFiltersDisplay, false,
                       delegate (int index)
                       {
                           DisplayTime = index;
                           ColumnEvents[1].Hidden = ColumnEvents[2].Hidden = ColumnEvents[3].Hidden = true;
                           ColumnEvents[9].Hidden = ColumnEvents[10].Hidden = ColumnEvents[11].Hidden = true;
                           if (DisplayTime == 0)
                           {
                               ColumnEvents[1].Hidden = false;
                               ColumnEvents[9].Hidden = false;
                           }
                           else if (DisplayTime == 1)
                           {
                               ColumnEvents[2].Hidden = false;
                               ColumnEvents[10].Hidden = false;
                           }
                           else if (DisplayTime == 2)
                           {
                               ColumnEvents[3].Hidden = false;
                               ColumnEvents[11].Hidden = false;
                           }
                       }
                       , null);


                // Change page
                if (MidiEventsLoaded != null)
                {
                    if (GUILayout.Button(MPTKGui.IconFirst, MPTKGui.Button)) CurrentPage = 0;
                    if (GUILayout.Button(MPTKGui.IconPrevious, MPTKGui.Button)) CurrentPage--;
                    GUILayout.Label($"Page {CurrentPage} / {MidiEventsLoaded.Count - 1}", MPTKGui.styleLabelCenter, GUILayout.Height(alignHorizontal));
                    if (GUILayout.Button(MPTKGui.IconNext, MPTKGui.Button)) CurrentPage++;
                    if (GUILayout.Button(MPTKGui.IconLast, MPTKGui.Button)) CurrentPage = MidiEventsLoaded.Count - 1;
                    CurrentPage = Mathf.Clamp(CurrentPage, 0, MidiEventsLoaded.Count - 1);
                }
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

        private void ShowMidiPlayerEvent(float startX, float width, float height, float nextAreaY)
        {
            try // Begin area title list
            {
                GUILayout.BeginArea(new Rect(startX, nextAreaY, width, HEIGHT_TITLE), MPTKGui.styleListTitle);
                nextAreaY += HEIGHT_TITLE - 1; // -1 to overlay edge
                GUILayout.BeginHorizontal();
                GUIStyle style = MPTKGui.LabelListNormal; // get a ref to the style
                style.contentOffset = new Vector2(-ScrollerMidiPlayer.x, style.contentOffset.y);
                foreach (MPTKGui.StyleItem column in ColumnEvents)
                    //try
                    //{
                    if (!column.Hidden)
                        if (column.ItemPopupContent == null)
                            GUILayout.Label(new GUIContent(column.Caption, column.Tooltip), style, GUILayout.Width(column.Width));
                        else
                            MPTKGui.ComboBox(ref column.ItemPopup, column.Caption, column.ItemPopupContent, true,
                                delegate (int index)
                                {
                                    // Debug.Log($"{index} {column.Caption}");
                                    if (column.Caption.Contains("Channel"))
                                    {
                                        for (int channel = 0; channel < 16; channel++)
                                            if (index == -2)
                                                midiPlayerLib.MidiPlayer.MPTK_Channels[channel].Enable = false;
                                            else if (index == -1)
                                                midiPlayerLib.MidiPlayer.MPTK_Channels[channel].Enable = true;
                                            else
                                                midiPlayerLib.MidiPlayer.MPTK_Channels[channel].Enable = column.ItemPopupContent[channel].Selected;
                                    }
                                    if (column.Caption.Contains("Track"))
                                    {
                                        //Debug.Log($"{index} {column.Caption}");
                                    }
                                    Repaint();
                                },
                                style, column.ItemPopupWidth, GUILayout.Width(column.Width));


                style.contentOffset = Vector2.zero;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{ex}");
            }
            finally
            {
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }

            if (MidiEventsLoaded != null)
            {
                try // Begin area MIDI events list
                {
                    // Why +30 ? Any idea !
                    GUILayout.BeginArea(new Rect(startX, nextAreaY, width, height - nextAreaY + 30), MPTKGui.stylePanelGrayLight);

                    ScrollerMidiPlayer = GUILayout.BeginScrollView(ScrollerMidiPlayer);

                    // Foreach MIDI events on the current page
                    int lineIndex = 0;
                    foreach (MPTKEvent mptkEvent in MidiEventsLoaded[CurrentPage])
                    {
                        // Filters MIDI events
                        switch (mptkEvent.Command)
                        {
                            case MPTKCommand.NoteOn: if (!PopupFiltersCommand[0].Selected) continue; break;
                            case MPTKCommand.NoteOff: if (!PopupFiltersCommand[1].Selected) continue; break;
                            case MPTKCommand.ControlChange: if (!PopupFiltersCommand[2].Selected) continue; break;
                            case MPTKCommand.PatchChange: if (!PopupFiltersCommand[3].Selected) continue; break;
                            case MPTKCommand.MetaEvent: if (!PopupFiltersCommand[4].Selected) continue; break;
                            case MPTKCommand.ChannelAfterTouch:
                            case MPTKCommand.KeyAfterTouch: if (!PopupFiltersCommand[5].Selected) continue; break;
                            default: if (!PopupFiltersCommand[6].Selected) continue; break;
                        }
                        // Filters channel
                        if (!PopupFiltersChannel[mptkEvent.Channel].Selected) continue;

                        // Filters track
                        if (!PopupFiltersTrack[(int)mptkEvent.Track].Selected) continue;

                        GUIStyle styleRow;
                        bool isPlayed;
                        if (FollowEvent && LastEventPlayed != null && mptkEvent.Tick == LastEventPlayed.Tick)
                        {
                            styleRow = MPTKGui.LabelListPlayed;
                            isPlayed = true;
                        }
                        else if (mptkEvent.Index == SelectedEvent)
                        {
                            styleRow = MPTKGui.LabelListSelected;
                            isPlayed = false;
                        }
                        else
                        {
                            styleRow = null; // Default column style
                            isPlayed = false;
                        }
                        try
                        {
                            GUILayout.BeginHorizontal();
                            int index = mptkEvent.Index;
                            string text = "";
                            int column = 0;
                            AddCell(mptkEvent, ColumnEvents[column++], mptkEvent.Index.ToString(), styleRow);
                            AddCell(mptkEvent, ColumnEvents[column++], mptkEvent.Tick.ToString(), styleRow);

                            AddCell(mptkEvent, ColumnEvents[column++], mptkEvent.RealTime, styleRow);
                            AddCell(mptkEvent, ColumnEvents[column++], mptkEvent.RealTime, styleRow);
                            AddCell(mptkEvent, ColumnEvents[column++], mptkEvent.Track.ToString(), styleRow);
                            AddCell(mptkEvent, ColumnEvents[column++], mptkEvent.Channel.ToString(), styleRow);

                            bool displayMoreColumns = false;
                            // Command column
                            switch (mptkEvent.Command)
                            {
                                case MPTKCommand.ControlChange:
                                    text = "CC - " + mptkEvent.Controller.ToString();
                                    break;
                                case MPTKCommand.MetaEvent:
                                    text = "Meta - " + mptkEvent.Meta.ToString();
                                    break;
                                default:
                                    text = mptkEvent.Command.ToString();
                                    break;
                            }
                            AddCell(mptkEvent, ColumnEvents[column++], text, styleRow); // Command column

                            // Value column to display depend on command
                            switch (mptkEvent.Command)
                            {
                                case MPTKCommand.NoteOn:
                                    displayMoreColumns = true; // Display velocity and duration
                                    text = mptkEvent.Value.ToString("000") + " - " + HelperNoteLabel.LabelFromMidi(mptkEvent.Value);
                                    break;
                                case MPTKCommand.NoteOff:
                                    text = mptkEvent.Value.ToString("000") + " - " + HelperNoteLabel.LabelFromMidi(mptkEvent.Value);
                                    break;
                                case MPTKCommand.PatchChange:
                                    string name = "unk";
                                    int bank = 0;
                                    try
                                    {
                                        // name at runtime will be midiPlayerLib.MidiPlayer.MPTK_Channels[mptkEvent.Channel].PresetName; 
                                        // or midiPlayerLib.MidiPlayer.MPTK_SoundFont.PresetName(mptkEvent.Value);
                                        bank = mptkEvent.Channel == 9 ? MidiPlayerGlobal.ImSFCurrent.DrumKitBankNumber : MidiPlayerGlobal.ImSFCurrent.DefaultBankNumber;
                                        name = MidiPlayerGlobal.MPTK_GetPatchName(bank, mptkEvent.Value);
                                    }
                                    catch { }
                                    if (mptkEvent.Channel == 9) name += " (drum)";
                                    text = mptkEvent.Value.ToString("000") + " - " + name;
                                    break;
                                case MPTKCommand.MetaEvent:
                                    switch (mptkEvent.Meta)
                                    {
                                        case MPTKMeta.KeySignature: text = $"SharpsFlats:{MPTKEvent.ExtractFromInt((uint)mptkEvent.Value, 0)} MajorMinor:{MPTKEvent.ExtractFromInt((uint)mptkEvent.Value, 1)}"; break;
                                        case MPTKMeta.TimeSignature: text = $"Numerator:{MPTKEvent.ExtractFromInt((uint)mptkEvent.Value, 0)} Denominator:{MPTKEvent.ExtractFromInt((uint)mptkEvent.Value, 1)}"; break;
                                        case MPTKMeta.SetTempo: text = $"µseconds:{mptkEvent.Value} Tempo:{MPTKEvent.QuarterPerMicroSecond2BeatPerMinute(mptkEvent.Value):F0}"; break;
                                        default: text = mptkEvent.Info ?? ""; break;
                                    }
                                    if (text.Length > 60) text = text.Substring(0, 60);
                                    break;
                                default:
                                    text = mptkEvent.Value.ToString();
                                    break;
                            }
                            AddCell(mptkEvent, ColumnEvents[column++], text, styleRow); // Value column
                            AddCell(mptkEvent, ColumnEvents[column++], displayMoreColumns ? mptkEvent.Velocity.ToString() : "", styleRow);
                            AddCell(mptkEvent, ColumnEvents[column++], displayMoreColumns ? mptkEvent.Length.ToString() : "", styleRow);
                            AddCell(mptkEvent, ColumnEvents[column++], displayMoreColumns ? mptkEvent.Duration : -1, styleRow);
                            AddCell(mptkEvent, ColumnEvents[column++], displayMoreColumns ? mptkEvent.Duration : -1, styleRow); // 11 time hh:mm:ss

                            AddCell(mptkEvent, ColumnEvents[column++], $"{mptkEvent.Measure}/{mptkEvent.Beat}", styleRow);

                            // make events played visible in the schroll
                            lineIndex++;

                            if (Event.current.type == EventType.Repaint && !midiPlayerLib.MidiPlayer.MPTK_IsPaused && midiPlayerLib.MidiPlayer.MPTK_IsPlaying)
                            {
                                Rect lastEventDraw = GUILayoutUtility.GetLastRect();
                                if (isPlayed && FollowEvent)
                                {
                                    if (lineIndex > 10)
                                        lastEventDraw.y += 5 * 15; // 5 lines from the bottom
                                    else
                                        lastEventDraw.y = 0; // let the event displayed from the top
                                    lastEventDraw.x = ScrollerMidiPlayer.x; // don't move the horizontal scroll
                                    GUI.ScrollTo(lastEventDraw);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }
                        finally
                        {
                            GUILayout.EndHorizontal();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                finally
                {
                    GUILayout.EndScrollView();
                    GUILayout.EndArea();
                }
            }
        }

        private void AddCell(MPTKEvent midiEvent, MPTKGui.StyleItem item, float timeMs, GUIStyle styleRow)
        {
            if (!item.Hidden)
            {
                string text = "0";
                if (timeMs < 0)
                    text = "";
                else if (timeMs > 0)
                {
                    if (DisplayTime == 1)
                        text = (timeMs / 1000f).ToString("F2");
                    else if (DisplayTime == 2)
                    {
                        TimeSpan timePos = TimeSpan.FromMilliseconds(timeMs);
                        text = string.Format("{0:00}:{1:00}:{2:00}:{3:000}", timePos.Hours, timePos.Minutes, timePos.Seconds, timePos.Milliseconds);
                    }
                }
                AddCell(midiEvent, item, text, styleRow);
            }
        }

        private void AddCell(MPTKEvent midiEvent, MPTKGui.StyleItem item, string text, GUIStyle styleRow = null)
        {
            if (!item.Hidden)
            {
                GUIStyle style = styleRow == null ? item.Style : styleRow;
                // Align content of the column 
                if (item.Offset != 0) { style.contentOffset = item.OffsetV;/* Debug.Log($"{ item.Caption} {item.Offset}");*/ }
                ;
                GUILayout.Label(text, style, GUILayout.Width(item.Width));
                if (item.Offset != 0) style.contentOffset = Vector2.zero;

                // User select a line ?
                if (Event.current.type == EventType.MouseDown)
                    if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    {
                        //Debug.Log($"{mptkEvent} XXX {window.position.x + GUILayoutUtility.GetLastRect().x}");
                        SelectedEvent = midiEvent.Index;
                        midiPlayerLib.MidiPlayer.MPTK_TickCurrent = midiEvent.Tick;
                        window.Repaint();
                    }
            }
        }
    }
}
#endif
