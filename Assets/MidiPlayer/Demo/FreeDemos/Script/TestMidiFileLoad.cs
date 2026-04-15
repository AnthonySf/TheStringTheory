//#define MPTK_PRO
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Events;
using MidiPlayerTK;
using UnityEngine.UIElements;

namespace DemoMPTK
{
    /// <summary>@brief
    /// MPTK Demo component able to load a MIDI file from your list of MIDI file or from a folder and displaying the list of events.
    /// </summary>
    public class TestMidiFileLoad : MonoBehaviour
    {
        /// <summary>@brief
        /// </summary>

        // Manage skin
        public CustomStyle myStyle;

        // This PreFab must be present in your scene.
        public MidiFilePlayer MidiLoader;

        public int MidiIndex = 0;
        public long StartTicks = 0;
        public long EndTicks = 0;
        public int PageToDisplay = 0;
        public string PathMidiFile;

        private Vector2 scrollerWindow = Vector2.zero;
        private int buttonWidth = 250;
        private PopupListItem PopMidi;

        private List<string> infoEvents;
        private Vector2 scrollPos = Vector2.zero;
        private GUIStyle butCentered;
        private GUIStyle labCentered;

        const int MAXLINEPAGE = 100;

        private void Awake()
        {
            MidiPlayerGlobal.LoadCurrentSF();
            MidiLoader = FindFirstObjectByType<MidiFilePlayer>();

            if (MidiLoader == null)
            {
                Debug.LogError("Can't find a MidiFilePlayer Prefab in the current Scene Hierarchy. Add it with the Maestro menu.");
                return;
            }
            MidiLoader.MPTK_PlayOnStart = false;
            MidiLoader.MPTK_DirectSendToPlayer = false;
        }

        private void Start()
        {

            PopMidi = new PopupListItem()
            {
                Title = "Select A MIDI File",
                OnSelect = MidiChanged,
                Tag = "NEWMIDI",
                ColCount = 3,
                ColWidth = 250,
            };
            MidiChanged(null, MidiIndex, 0);
        }

        private void MidiChanged(object tag, int midiindex, int indexList)
        {
            MidiIndex = midiindex;
            MidiLoader.MPTK_MidiIndex = midiindex;

            Debug.Log($"MidiChanged Index: {MidiLoader.MPTK_MidiIndex} '{MidiLoader.MPTK_MidiName}'");

            MidiLoader.MPTK_Load();
            StartTicks = 0;
            EndTicks = MidiLoader.MPTK_TickLast;
            PageToDisplay = 0;
            scrollPos = new Vector2(0, 0);
            infoEvents = new List<string>();
        }

        //! [Example TheMostSimpleDemoForMidiLoader]
        /// <summary>@brief
        /// Load and display MIDI events in a few line of code.
        /// </summary>
        private void TheMostSimpleDemoForMidiLoader()
        {
            // A MidiFileLoader prefab must be added to the hierarchy with the editor (see menu MPTK)
            MidiFilePlayer mfp = FindFirstObjectByType<MidiFilePlayer>();
            if (mfp == null)
            {
                Debug.LogWarning("Can't find a MidiFilePlayer Prefab in the current Scene Hierarchy. Add it with the Maestro menu.");
                return;
            }

            // Index of the midi in the MidiDB (find it with 'Midi File Setup' from the menu MPTK)
            mfp.MPTK_MidiIndex = MidiIndex;

            // Open and load the Midi
            mfp.MPTK_Load();

            // Loop on each MIDI events
            foreach (MPTKEvent mptkEvent in mfp.MPTK_MidiEvents)
            {
                // Log if event is a note on
                if (mptkEvent.Command == MPTKCommand.NoteOn)
                    Debug.Log($"Note On at {mptkEvent.RealTime} millisecond  Channel:{mptkEvent.Channel} Note:{mptkEvent.Value}  Duration:{mptkEvent.Duration} millisecond  Velocity:{mptkEvent.Velocity}");
                else if (mptkEvent.Command == MPTKCommand.PatchChange)
                    Debug.Log($"Preset Change at {mptkEvent.RealTime} millisecond  Channel:{mptkEvent.Channel}  Preset:{mptkEvent.Value}");
                else if (mptkEvent.Command == MPTKCommand.ControlChange)
                {
                    if (mptkEvent.Controller == MPTKController.BankSelectMsb)
                        Debug.Log($"Bank Change at {mptkEvent.RealTime} millisecond  Channel:{mptkEvent.Channel}  Bank:{mptkEvent.Value}");
                }
                // Uncomment to display all MIDI events
                //Debug.Log(mptkEvent.ToString());
            }
        }
        //! [Example TheMostSimpleDemoForMidiLoader]

        void OnGUI()
        {
            HelperDemo.GUIScale();

            // Set custom Style. Good for background color 3E619800
            if (myStyle == null)
                myStyle = new CustomStyle();

            if (butCentered == null)
            {
                butCentered = new GUIStyle("Button");
                butCentered.alignment = TextAnchor.MiddleCenter;
                butCentered.fontSize = 16;
            }

            if (labCentered == null)
            {
                labCentered = new GUIStyle("Label");
                labCentered.alignment = TextAnchor.MiddleCenter;
                labCentered.fontSize = 16;
            }

            if (MidiLoader != null)
            {
                scrollerWindow = GUILayout.BeginScrollView(scrollerWindow, false, false, GUILayout.Width(Screen.width));

                // Display popup in first to avoid activate other layout behind
                PopMidi.Draw(MidiPlayerGlobal.MPTK_ListMidi, MidiIndex, myStyle);

                MainMenu.Display("Test MIDI File Loader - Demonstrate how to use the MPTK API to load a MIDI file", myStyle, Screen.width);

                //
                // Left column: MIDI action and info
                // ---------------------------------

                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(myStyle.BacgDemosLight, GUILayout.Width(450));

                GUILayout.BeginHorizontal();
                GUILayout.Label("Select and load a MIDI File from the internal MidiDB. Add you MIDI from the Unity editor menu MPTK (ALT-M):", GUILayout.Width(350));
                // Open the popup to select a midi
                if (GUILayout.Button("Load MIDI from MidiDB", GUILayout.Width(160), GUILayout.Height(40)))
                    PopMidi.Show = !PopMidi.Show;
                PopMidi.PositionWithScroll(ref scrollerWindow);
                GUILayout.EndHorizontal();

                // Select a midi from a local file on the desktop
                GUILayout.Label("  Or", GUILayout.Width(50));
                GUILayout.Label("From a local MIDI file (Maestro Pro Only). Enter a full path:", GUILayout.Width(450));
                GUILayout.BeginHorizontal();
                PathMidiFile = GUILayout.TextField(PathMidiFile, GUILayout.Width(350));
                if (GUILayout.Button(new GUIContent("Load External MIDI File", ""), GUILayout.Width(160)))
                {
#if MPTK_PRO
                    // Load a MIDI from a local file
                    // -----------------------------
                    if (MidiLoader.MPTK_Load(PathMidiFile) != null)
                    {
                        StartTicks = 0;
                        EndTicks = MidiLoader.MPTK_TickLast;
                        PageToDisplay = 0;
                        scrollPos = new Vector2(0, 0);
                        infoEvents = new List<string>();
                    }
#else
                    Debug.LogWarning("Loading an external MIDI File is a Maestro Pro function");
#endif
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(20);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button($"Read MIDI Events from ticks {StartTicks} to ticks {EndTicks}", GUILayout.Height(40)))
                {
                    // Read MIDI events between two value of ticks
                    // -------------------------------------------
                    infoEvents = new List<string>();
                    List<MPTKEvent> mptkEvents = MidiLoader.MPTK_ReadMidiEvents(StartTicks, EndTicks);
                    foreach (MPTKEvent mptkEvent in mptkEvents)
                    {
                        // Create a string description of the MIDI event
                        infoEvents.Add(mptkEvent.ToString());
                    }
                }
                GUILayout.EndHorizontal();

                //string midiname = "no midi defined";
                //if (MidiIndex >= 0 && MidiPlayerGlobal.MPTK_ListMidi != null && MidiIndex < MidiPlayerGlobal.MPTK_ListMidi.Count)
                //    midiname = MidiPlayerGlobal.MPTK_ListMidi[MidiIndex].Label;
                GUILayout.Label("Current MIDI file: " + MidiLoader.MPTK_MidiName, myStyle.TitleLabel3);

                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Read MIDI Events from: " + StartTicks, myStyle.TitleLabel3, GUILayout.Width(220));
                StartTicks = (long)GUILayout.HorizontalSlider((float)StartTicks, 0f, (float)MidiLoader.MPTK_TickLast, GUILayout.Width(buttonWidth));
                GUILayout.EndHorizontal();

                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Read MIDI Events to: " + EndTicks, myStyle.TitleLabel3, GUILayout.Width(220));
                EndTicks = (long)GUILayout.HorizontalSlider((float)EndTicks, 0f, (float)MidiLoader.MPTK_TickLast, GUILayout.Width(buttonWidth));
                GUILayout.EndHorizontal();

                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Duration in seconds: ", myStyle.TitleLabel3, GUILayout.Width(220));
                GUILayout.Label(MidiLoader.MPTK_Duration.TotalSeconds.ToString(), myStyle.TitleLabel3);
                GUILayout.EndHorizontal();

                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Quarter per Measure: ", myStyle.TitleLabel3, GUILayout.Width(220));
                GUILayout.Label(MidiLoader.MPTK_MidiLoaded.MPTK_NumberBeatsMeasure.ToString());
                GUILayout.EndHorizontal();

                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Beat per Quarter: ", myStyle.TitleLabel3, GUILayout.Width(220));
                GUILayout.Label(MidiLoader.MPTK_MidiLoaded.MPTK_NumberQuarterBeat.ToString(), myStyle.TitleLabel3);
                GUILayout.EndHorizontal();

                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                GUILayout.Label("MPTK_InitialTempo: ", myStyle.TitleLabel3, GUILayout.Width(220));
                GUILayout.Label(Convert.ToInt32(MidiLoader.MPTK_MidiLoaded.MPTK_InitialTempo).ToString(), myStyle.TitleLabel3);
                GUILayout.EndHorizontal();

                GUILayout.Space(20);

                GUILayout.BeginHorizontal();
                // This short piece of script demonstrate how it is simple to load a MIDI from the MidiDB
                GUILayout.Label("The simplest MIDI file loader. See C# script TestMidiFileLoad.cs and TheMostSimpleDemoForMidiLoader() method:", GUILayout.Width(380));
                if (GUILayout.Button(new GUIContent($"Load MIDI File index {MidiIndex}", ""), GUILayout.Width(160), GUILayout.Height(40)))
                {
                    TheMostSimpleDemoForMidiLoader();
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(20);


                GUILayout.Label("This class can be used only to load a MIDI file and read events. There is no MIDI sequencer and no MIDI Synthesizer. Rather, used the prefab MidiFilePlayer to play a MIDI file.", myStyle.TitleLabel3);

                // End left column
                GUILayout.EndVertical();

                if (infoEvents != null && infoEvents.Count > 0)
                {
                    GUILayout.BeginVertical(myStyle.BacgDemosLight, GUILayout.Width(1050));

                    //
                    // Right Column: midi infomation, lyrics, ...
                    // ------------------------------------------
                    GUILayout.BeginHorizontal(myStyle.BacgDemosLight);

                    if (GUILayout.Button("<<", butCentered, GUILayout.Width(40), GUILayout.Height(40))) PageToDisplay = 0;
                    if (GUILayout.Button("<", butCentered, GUILayout.Width(40), GUILayout.Height(40))) PageToDisplay--;
                    GUILayout.Label("page " + (PageToDisplay + 1).ToString() + " / " + (infoEvents.Count / MAXLINEPAGE + 1).ToString(), labCentered, GUILayout.Width(150), GUILayout.Height(40));
                    if (GUILayout.Button(">", butCentered, GUILayout.Width(40), GUILayout.Height(40))) PageToDisplay++;
                    if (GUILayout.Button(">>", butCentered, GUILayout.Width(40), GUILayout.Height(40))) PageToDisplay = infoEvents.Count / MAXLINEPAGE;

                    GUILayout.EndHorizontal();

                    if (PageToDisplay < 0) PageToDisplay = 0;
                    if (PageToDisplay * MAXLINEPAGE > infoEvents.Count) PageToDisplay = infoEvents.Count / MAXLINEPAGE;

                    string infoToDisplay = "";
                    for (int i = PageToDisplay * MAXLINEPAGE; i < (PageToDisplay + 1) * MAXLINEPAGE; i++)
                        // display each string description of the MIDI event
                        if (i < infoEvents.Count)
                            infoToDisplay += infoEvents[i] + "\n";

                    //GUILayout.BeginHorizontal();

                    scrollPos = GUILayout.BeginScrollView(scrollPos, false, false);//, GUILayout.Height(heightLyrics));
                    GUILayout.Label(infoToDisplay, myStyle.TextFieldMultiCourier);
                    GUILayout.EndScrollView();

                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();

                GUILayout.EndScrollView();

            }
        }
    }
}
