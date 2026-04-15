using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MidiPlayerTK;

namespace DemoMVP
{
    public class SimpleSpatialStream : MonoBehaviour
    {
        // To be set with the inspector
        public MidiStreamPlayer midiStreamPlayer;

        // Delay between two notes in seconds
        public float Delay;

        // Current time, reset to 0 when note is played
        public float Last;

        // Note to play
        [Range(0, 127)]
        public int Note;

        // Max position of the gameobject 
        [Range(-10, 10)]
        public int MaxPositionX;
        [Range(-10, 10)]
        public int MaxPositionY;
        [Range(-10, 10)]
        public int MaxPositionZ;

        // Preset 
        [Range(0, 127)]
        public int Preset;

        // The current note playing
        private MPTKEvent midiEvent;

        // Update is called once per frame
        void Update()
        {
            // Play a note after a delay of 'Delay' seconds
            Last += Time.deltaTime;
            if (Last > Delay)
            {
                Last = 0f;

                // Stop the note 
                if (midiEvent != null)
                    midiStreamPlayer.MPTK_StopEvent(midiEvent);

                // Build a note with infinite duration ...
                midiEvent = new MPTKEvent() { Command = MPTKCommand.NoteOn, Value = Note };
                // ... and play it
                midiStreamPlayer.MPTK_PlayEvent(midiEvent);
            }

            // Change preset if needed. We can always use channel 0 because each MidiStreamPlayer uses
            // an independent synthesizer, like separate keyboards connected with MIDI cables.
            // Changing the preset on one player does not affect the others.
            if (midiStreamPlayer.MPTK_Channels[0].PresetNum != Preset)
                midiStreamPlayer.MPTK_Channels[0].PresetNum = Preset;

            // Move the GameObject (sphere) on each axis from -StartPos to StartPos.
            if (MaxPositionX != 0) transform.position = new Vector3(Mathf.Sin(Time.realtimeSinceStartup) * MaxPositionX, transform.position.y, transform.position.z);
            if (MaxPositionY != 0) transform.position = new Vector3(transform.position.x, Mathf.Sin(Time.realtimeSinceStartup) * MaxPositionY, transform.position.z);
            if (MaxPositionZ != 0) transform.position = new Vector3(transform.position.x, transform.position.y, Mathf.Sin(Time.realtimeSinceStartup) * MaxPositionZ);
        }
    }
}
