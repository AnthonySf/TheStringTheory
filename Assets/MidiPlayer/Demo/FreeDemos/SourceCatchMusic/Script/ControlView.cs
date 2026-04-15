using MidiPlayerTK;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MPTKDemoCatchMusic
{
    /// <summary>
    /// Describe a Unity GameObject dedicated to instrument change.
    ///     - created when a MIDI event is read from the MidiFilePlayer (see MusicView.cs), 
    ///     - moves along the X-axis,
    ///     - plays the MIDI event when reach the end of the area, 
    ///     - then destroy.
    /// </summary>
    public class ControlView : MonoBehaviour
    {
        public MPTKEvent instrumentChange;
        public MidiStreamPlayer midiStreamPlayer;
        public bool played = false;
        public Material MatPlayed;
        public float zOriginal;

        void Update()
        {
            // The midi event is played with a MidiStreamPlayer when position X < -45 (falling)
            if (!played && transform.position.x < -45f)
            {
                played = true;

                // Random instrument change when the original position is modified by a collider.
                // If original z is not the same, the value will be changed.
                // Not good for the ears ... but funny.
                int delta = (int)(zOriginal - transform.position.z);
                instrumentChange.Value += delta;

                // Now play the instrument change with a MidiStreamPlayer prefab
                midiStreamPlayer.MPTK_PlayEvent(instrumentChange);

                gameObject.GetComponent<Renderer>().material = MatPlayed;
            }
            if (transform.position.y < -30f)
            {
                Destroy(this.gameObject);
            }
        }

        void FixedUpdate()
        {
            // Move the note along the X axis
            float translation = Time.fixedDeltaTime * MusicView.Speed;
            transform.Translate(-translation, 0, 0);
        }
    }
}
