//#define MPTK_PRO
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using MidiPlayerTK;
using UnityEditor;
using UnityEngine.Analytics;
using UnityEngine.UIElements;

namespace DemoMPTK
{
    public partial class TestMidiFilePlayerScripting : MonoBehaviour
    {
        // How to integrate 3D object in a canvas
        // https://www.youtube.com/watch?v=8yzpjkoE0YA

        public GameObject Renderer;
        MPTKStat midiStat;
        NoteViewerCircle[] notesViewerCircle;

        int lastNoteViewer;
        const int MAX_LINE_RENDERER = 30;
        public GameObject PrefabWithLineRenderer;
        public GameObject ShadowScreen;

        class NoteViewerCircle
        {
            static Color[] colors;

            public static float WidthLine { get => widthLine; set { widthLine = Mathf.Clamp(value, 0.001f, 5f); } }
            [SerializeField] private static float widthLine = 0.1f;

            public static float GrowingPhase { get => growingPhase; set { growingPhase = Mathf.Clamp(value, 0f, 2f); } }
            [SerializeField] private static float growingPhase;

            public static float FadingPhase { get => fadingPhase; set { fadingPhase = Mathf.Clamp(value, 0f, 2f); } }
            [SerializeField] private static float fadingPhase = 0.2f;

            public static float DistanceShadow { get => distanceShadow; set { distanceShadow = Mathf.Clamp(value, 0f, 1f); } }
            [SerializeField] private static float distanceShadow;

            int index;

            Color color;
            LineRenderer lineRenderer;
            GameObject ShadowScreen;

            MPTKEvent midiEvent;

            // Time when the line is added
            float timeStart;

            // Time until the line must growing (for transprency or any kind of properties you want to animate)
            float timeGrowing;

            // Time until the line stay stable (for transprency or any kind of properties you want to animate)
            float timeSteady;

            // Time until the line must fading (for transprency or any kind of properties you want to animate)
            float timeFading;

            Vector3 positionStart = new Vector3(0, 0, 0);
            Vector3 positionEnd = new Vector3(0, 0, 0);
            enum PHASE { GROWING, STEADY, FADING };
            PHASE phase;


            public NoteViewerCircle(GameObject goRenderer, GameObject WithLineRenderer, GameObject shadowScreen, int pIndex)
            {
                ShadowScreen = shadowScreen;
                index = pIndex;
                // Build channel color
                if (colors == null)
                {
                    colors = new Color[16];
                    // Fill the array with different colors (thanks to Copilot!).
                    colors[0] = Color.red;
                    colors[1] = Color.green;
                    colors[2] = Color.blue;
                    colors[3] = Color.yellow;
                    colors[4] = Color.cyan;
                    colors[5] = Color.magenta;
                    colors[6] = new Color(1, 0.5f, 0); // Orange
                    colors[7] = Color.white;
                    colors[8] = Color.black;
                    colors[9] = new Color(0.5f, 0, 1); // Violet
                    colors[10] = new Color(0, 1, 0.5f); // Turquoise
                    colors[11] = new Color(1, 0.5f, 0.5f); // Rose
                    colors[12] = new Color(0.5f, 1, 0.5f); // Vert clair
                    colors[13] = new Color(0.5f, 0.5f, 1); // Bleu clair
                    colors[14] = new Color(1, 1, 0); // Jaune clair
                    colors[15] = new Color(0, 1, 1); // Cyan clair
                }

                GameObject obj = Instantiate(WithLineRenderer) as GameObject;
                lineRenderer = obj.GetComponent<LineRenderer>();
                lineRenderer.gameObject.transform.SetParent(goRenderer.transform, false);
                //lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.widthMultiplier = widthLine;
                lineRenderer.enabled = false;
            }

            public void AssignMidi(MPTKEvent pmidiEvent, int noteMin, int noteMax)
            {
                midiEvent = pmidiEvent;

                phase = PHASE.GROWING;

                // Calculating time for each step
                timeStart = Time.time;
                // No growing phase for percussive instrument (channel = 9)
                timeGrowing = midiEvent.Channel == 9 ? Time.time : timeStart + growingPhase;
                timeSteady = timeGrowing + (midiEvent.Duration / 1000f);
                timeFading = timeSteady + fadingPhase;

                //Debug.Log($"{index:00} {Time.time} ADD NOTE timeAttack: {timeAttack} timeHold: {timeHold} timeRelease: {timeRelease}");

                // Calculating angle from note value:
                //  Build a normalized value [0,1] from the note value [0, 127]
                //  taking into account min and max note values (to fill the full circle)
                //      if value = min, normalized value is 0
                //      if value = max, normalized value is 1
                //  Apply a coefficient of 2*PI to get an angle in radians between 0 and 2*PI (full circle)
                //  Add PI (180-degree rotation)
                //  Avoid division by zero if min = max: return PI.
                float angleNoteValue = noteMax == noteMin ? Mathf.PI : (((midiEvent.Value - noteMin) / (float)(noteMax - noteMin)) * Mathf.PI * 2f) + MathF.PI;

                // Calculating length from velocity: Build a normalized value [0,1] from the note velocity [0, 127]
                float normalisedNoteVel = midiEvent.Velocity / 127f;

                //Debug.Log($"{index:00} {Time.time} angle: {angleNoteValue} length: {normalisedNoteVel}");

                // Start line from a inner small circle. Radius: 0.1
                positionStart.x = Mathf.Cos(angleNoteValue) * 0.1f;
                positionStart.y = Mathf.Sin(angleNoteValue) * 0.1f;
                lineRenderer.SetPosition(0, positionStart);

                // For setting a color start 
                //Line.startColor = Color.green;

                // End line to a large circle radius max 1.8 related to the velocity. Add inner radius circle.
                positionEnd.x = Mathf.Cos(angleNoteValue) * (normalisedNoteVel * 1.8f  + 0.1f);
                positionEnd.y = Mathf.Sin(angleNoteValue) * (normalisedNoteVel * 1.8f + 0.1f);
                lineRenderer.SetPosition(1, positionEnd);

                lineRenderer.widthMultiplier = widthLine;

                // For cyclic color: Color color = new Color(Mathf.Sin(Time.time), Mathf.Cos(Time.time), Mathf.Sin(Time.time));

                // Set a color related to the MIDI channel.
                // Idea for the next version: defined color related to the current instrument associated to the channel.
                color = colors[midiEvent.Channel];

                // If there os no growing time, start directly with no transparence (alpha = 1) else full transparency.
                color.a = timeStart == timeGrowing ? 1f : 0f;
                lineRenderer.material.SetColor("_Color", color);

                // Line is visible
                lineRenderer.enabled = true;
            }
            // Vector3 positionShadow;
            public void Update()
            {
                if (midiEvent != null && lineRenderer != null)
                {
                    //Debug.Log($"{index:00} {Time.time} note:{midiEvent.Value} phase: {phase} alpha: {color.a}");

                    // Move the cube with the shadows along the Z axis.
                    Vector3 positionShadow = ((RectTransform)ShadowScreen.transform).anchoredPosition3D;
                    positionShadow.z = Mathf.Lerp(128, 500, DistanceShadow);
                    ((RectTransform)ShadowScreen.transform).anchoredPosition3D = positionShadow;

                    switch (phase)
                    {
                        case PHASE.GROWING:
                            if (Time.time > timeGrowing)
                            {
                                phase = PHASE.STEADY;
                                color.a = 1; // go to steady phase, no transparency
                            }
                            else
                            {
                                // Make transparency change from 0 to 1 along the growing phase.
                                // If no growing phase (timeStart == timeGrowing) set no transparency (alpha = 1).
                                color.a = timeStart == timeGrowing ? 1f : (Time.time - timeStart) / (timeGrowing - timeStart);
                            }
                            lineRenderer.material.SetColor("_Color", color);
                            break;

                        case PHASE.STEADY:
                            if (Time.time > timeSteady)
                            {
                                phase = PHASE.FADING; // go to fadng phase
                            }
                            break;

                        case PHASE.FADING:
                            if (Time.time > timeFading)
                            {
                                //Debug.Log($"{index:00} {Time.time} END DISPLAY note:{midiEvent.Value}");
                                lineRenderer.enabled = false;
                                midiEvent = null; // No more MIDI event associated to this line renderer
                                color.a = 0; // Fading is over, set full transparency

                            }
                            else
                            {
                                // Make transparency fading from 1 to 0 along the fading phase.
                                // If no fading phase (timeSteady == timeGrowing) set full transparency (alpha = 0).
                                color.a = timeSteady == timeGrowing ? 0f : (timeFading - Time.time) / (timeFading - timeSteady);
                            }
                            lineRenderer.material.SetColor("_Color", color);
                            break;
                    }
                }
            }
        }
    }
}
