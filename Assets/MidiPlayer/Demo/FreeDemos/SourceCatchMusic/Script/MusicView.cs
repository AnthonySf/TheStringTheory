using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MidiPlayerTK;
using System;
using UnityEngine.Events;
using DemoMPTK;

namespace MPTKDemoCatchMusic
{
    /// <summary>
    /// MAin class for CatchMusic demo/
    ///     - Start MidiFilePlayer ad defined OnEventNotesMidi to catch each MIDI events (without playing them).
    ///     - Create Unity GameObject for each MIDI events (only NoteOn and PatchChange for instrument).
    /// </summary>
    public class MusicView : MonoBehaviour
    {

        public static float Speed = 15f;
        public Camera Cam;

        /// <summary>
        /// A prefab MidiFilePlayer must exists in the Unity Editor Hierarchy and must be associated in the MusicView GameObject inspector.
        /// In ths demo, the MidiFilePlayer is muted (Send MIDI ti the Synth disabled). 
        /// It is only used to receive MIDI events thanks to the MIDI sequencer and the event defined with OnEventNotesMidi (see bellow).
        /// </summary>
        public MidiFilePlayer midiFilePlayer;

        /// <summary>
        /// A prefab MidiStreamPlayer must exists in the Unity Editor Hierarchy and must be associated in the MusicView GameObject inspector.
        /// A reference to the MidiStreamPlayer is associated to each NoteView and ControlView GameObject created for each MIDI event.
        /// When the GameObject reach the end of the area, the MIDI event is played with MidiStreamPlayer.
        /// </summary>
        public MidiStreamPlayer midiStreamPlayer;

        public static Color ButtonColor = new Color(.7f, .9f, .7f, 1f);

        /// <summary>
        /// Hold a GameObject created for each MIDI note received from the MidiFilePlayer sequencer. Created with Instantiate<NoteView>().
        /// with:
        ///     - note associated
        ///     - MidiStreamPlayer for playing the note by the MIDI synth.
        ///     - some graphical information and current position
        /// </summary>
        public NoteView NoteDisplay;

        /// <summary>
        /// Hold a GameObject created for each MIDI controller received from the MidiFilePlayer sequencer. Created with Instantiate<NoteView>().
        /// with:
        ///     - Note associated
        ///     - MidiStreamPlayer for playing the note by the MIDI synth.
        ///     - some graphical information and current position
        /// </summary>
        public ControlView ControlDisplay;

        public Collide Collider;
        public GameObject Plane;
        public float minZ, maxZ, minX, maxX;
        public float LastTimeCollider;
        public float DelayCollider = 5;
        public float FirstDelayCollider = 20;
        public Material MatNewNote;
        public Material MatNewController;

        // Count GameObject for each z position in the plan. Useful to stack them.
        int[] countZ;

        public void EndLoadingSF()
        {
            Debug.Log("End loading SF, MPTK is ready to play");
            Debug.Log("   Time To Load SoundFont: " + Math.Round(MidiPlayerGlobal.MPTK_TimeToLoadSoundFont.TotalSeconds, 3).ToString() + " second");
            Debug.Log("   Time To Load Samples: " + Math.Round(MidiPlayerGlobal.MPTK_TimeToLoadWave.TotalSeconds, 3).ToString() + " second");
            Debug.Log("   Presets Loaded: " + MidiPlayerGlobal.MPTK_CountPresetLoaded);
            Debug.Log("   Samples Loaded: " + MidiPlayerGlobal.MPTK_CountWaveLoaded);
        }

        void Start()
        {
            // Default size of a Unity Plan
            float planSize = 10f;

            minZ = Plane.transform.localPosition.z - Plane.transform.localScale.z * planSize / 2f;
            maxZ = Plane.transform.localPosition.z + Plane.transform.localScale.z * planSize / 2f;

            minX = Plane.transform.localPosition.x - Plane.transform.localScale.x * planSize / 2f;
            maxX = Plane.transform.localPosition.x + Plane.transform.localScale.x * planSize / 2f;

            if (midiFilePlayer != null)
            {
                // No listener defined. Set now by script. NotesToPlay will be called for each new notes read from Midi file
                Debug.Log("MusicView: Maestro Event MidiFilePlayer.OnEventNotesMidi set by script (see MusicView.cs). Setting with the inspector is also possible.");
                midiFilePlayer.OnEventNotesMidi.AddListener(NotesToPlay);
            }
            else
                Debug.Log("No MidiFilePlayer prefab detected. Add it to your Hierarchy and defined it in MusicView inspector.");

        }

        /// <summary>@brief
        /// Call when a group of MIDI events is ready for playing from the the midi reader.
        /// Play events are delayed until they "fall out". See NoteView.cs and MControlView.cs
        /// </summary>
        /// <param name="notes"></param>
        public void NotesToPlay(List<MPTKEvent> notes)
        {
            // Count GameObject for each z position in the plan. Useful to stack them.
            countZ = new int[Convert.ToInt32(maxZ - minZ) + 1];

            //Debug.Log(midiFilePlayer.MPTK_PlayTime.ToString() + " count:" + notes.Count);
            foreach (MPTKEvent mptkEvent in notes)
            {
                switch (mptkEvent.Command)
                {
                    case MPTKCommand.NoteOn:
                        //Debug.Log($"NoteOn Channel:{note.Channel}  Preset index:{midiStreamPlayer.MPTK_ChannelPresetGetIndex(note.Channel)}  Preset name:{midiStreamPlayer.MPTK_ChannelPresetGetName(note.Channel)}");
                        if (mptkEvent.Value > 40 && mptkEvent.Value < 100)// && note.Channel==1)
                        {
                            // Z position is set depending the note value:mptkEvent.Value
                            float z = Mathf.Lerp(minZ, maxZ, (mptkEvent.Value - 40) / 60f);
                            countZ[Convert.ToInt32(z - minZ)]++;
                            // Y position is set depending the count of object at the z position
                            Vector3 position = new Vector3(maxX, 2 + countZ[Convert.ToInt32(z - minZ)] * 4f, z);
                            // Instantiate a GameObject to represent this midi event in the 3D world
                            NoteView noteview = Instantiate<NoteView>(NoteDisplay, position, Quaternion.identity);
                            noteview.gameObject.SetActive(true);
                            noteview.hideFlags = HideFlags.HideInHierarchy;
                            noteview.midiStreamPlayer = midiStreamPlayer;
                            noteview.noteOn = mptkEvent; // the midi event is attached to the gameobjet, will be played more later
                            noteview.gameObject.GetComponent<Renderer>().material = MatNewNote;
                            // See noteview.cs: update() move the note along the plan until they fall out, then they are played
                            noteview.zOriginal = position.z;

                            if (!NoteView.FirstNotePlayed)
                                PlaySound();
                        }
                        break;

                    case MPTKCommand.PatchChange:
                        {
                            //Debug.Log($"PatchChange Channel:{note.Channel}  Preset index:{note.Value}");
                            // Z position is set depending the note value:mptkEvent.Value
                            float z = Mathf.Lerp(minZ, maxZ, mptkEvent.Value / 127f);
                            // Y position is set depending the count of objects at the z position
                            countZ[Convert.ToInt32(z - minZ)]++;
                            Vector3 position = new Vector3(maxX, 8f + countZ[Convert.ToInt32(z - minZ)] * 4f, z);
                            // Instantiate a GameObject to represent this midi event in the 3D world
                            ControlView patchview = Instantiate<ControlView>(ControlDisplay, position, Quaternion.identity);
                            patchview.gameObject.SetActive(true);
                            patchview.hideFlags = HideFlags.HideInHierarchy;
                            patchview.midiStreamPlayer = midiStreamPlayer;
                            patchview.instrumentChange = mptkEvent; // the midi event is attached to the gameobjet, will be played more later
                            patchview.gameObject.GetComponent<Renderer>().material = MatNewController;
                            patchview.zOriginal = position.z;
                        }
                        break;
                }
            }
        }

        private void PlaySound()
        {
            // Some sound for waiting the notes, will be disabled at the fist note played ...
            //! [Example PlayNote]
            midiStreamPlayer.MPTK_PlayEvent
            (
                new MPTKEvent()
                {
                    Channel = 9,
                    Duration = 999999,
                    Value = 48,
                    Velocity = 100
                }
            );
            //! [Example PlayNote]
        }

        /// <summary>
        /// Quick UI
        /// </summary>
        void OnGUI()
        {
            int startx = 5;
            int starty = 90;
            int maxwidth = Screen.width;

            if (midiFilePlayer != null)
            {
                GUILayout.BeginArea(new Rect(startx, starty, maxwidth, 200));

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("Previous", ""), GUILayout.Width(150)))
                {
                    Clear();
                    midiFilePlayer.MPTK_Previous();
                }
                if (GUILayout.Button(new GUIContent("Next", ""), GUILayout.Width(150)))
                {
                    Clear();
                    midiFilePlayer.MPTK_Next();
                }
                if (GUILayout.Button(new GUIContent("Clear", ""), GUILayout.Width(150)))
                    Clear();
                GUILayout.EndHorizontal();
                GUILayout.Label("MIDI '" + midiFilePlayer.MPTK_MidiName + (midiFilePlayer.MPTK_IsPlaying ? "' is playing" : " is not playing"));
                GUILayout.BeginHorizontal();
                GUILayout.Label("MIDI Position :", GUILayout.Width(100));
                double currentposition = Math.Round(midiFilePlayer.MPTK_Position / 1000d, 2);
                double newposition = Math.Round(GUILayout.HorizontalSlider((float)currentposition, 0f, (float)midiFilePlayer.MPTK_DurationMS / 1000f, GUILayout.Width(200)), 2);
                if (newposition != currentposition)
                    midiFilePlayer.MPTK_Position = newposition * 1000d;
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Speed Music :", GUILayout.Width(100));
                float speed = GUILayout.HorizontalSlider(midiFilePlayer.MPTK_Speed, 0.1f, 5f, GUILayout.Width(200));
                if (speed != midiFilePlayer.MPTK_Speed) midiFilePlayer.MPTK_Speed = speed;
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Speed Note :", GUILayout.Width(100));
                Speed = GUILayout.HorizontalSlider(Speed, 5f, 20f, GUILayout.Width(200));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Camera Y:", GUILayout.Width(100));
                float y = GUILayout.HorizontalSlider(Cam.transform.position.y, 50f, 150f, GUILayout.Width(200));
                if (y != Cam.transform.position.y)
                    Cam.transform.Translate(new Vector3(0, y - Cam.transform.position.y, 0), Space.World);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Camera X:", GUILayout.Width(100));
                float x = GUILayout.HorizontalSlider(Cam.transform.position.x, -50f, 50f, GUILayout.Width(200));
                if (x != Cam.transform.position.x)
                    Cam.transform.Translate(new Vector3(x - Cam.transform.position.x, 0, 0), Space.World);
                GUILayout.EndHorizontal();

                GUILayout.Label("Be careful with the notes traffic jam!!!");

                GUILayout.EndArea();
            }
        }

        /// <summary>@brief
        /// Remove all GameObject Note on the screen
        /// </summary>
        public void Clear()
        {
            NoteView[] components = GameObject.FindObjectsByType<NoteView>(FindObjectsSortMode.None);
            foreach (NoteView noteview in components)
            {
                if (noteview.enabled)
                    //Debug.Log("destroy " + ut.name);
                    DestroyImmediate(noteview.gameObject);
            }
        }

        void Update()
        {
            if (midiFilePlayer != null && midiFilePlayer.MPTK_IsPlaying)
            {
                // Generates random collisions that unfortunately change the music ... for fun!
                float time = Time.realtimeSinceStartup - LastTimeCollider;
                if (time > DelayCollider + FirstDelayCollider)
                {
                    FirstDelayCollider = 0;
                    LastTimeCollider = Time.realtimeSinceStartup;

                    float zone = 10;
                    Vector3 position = new Vector3(UnityEngine.Random.Range(minX + zone, maxX - zone), -5, UnityEngine.Random.Range(minZ + zone, maxZ - zone));
                    // Instantiate collider (sphere) to interact with note and patch change.
                    Collide n = Instantiate<Collide>(Collider, position, Quaternion.identity);
                    n.gameObject.SetActive(true);
                    n.hideFlags = HideFlags.HideInHierarchy;
                }
            }
        }
    }
}
