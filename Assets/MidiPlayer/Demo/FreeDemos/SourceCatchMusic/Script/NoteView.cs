using MidiPlayerTK;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>@brief
/// Demo CatchMusic
/// </summary>
namespace MPTKDemoCatchMusic
{

    /// <summary>
    /// Describe a Unity GameObject dedicated to MIDI note.
    ///     - created when a MIDI event is read from the MidiFilePlayer (see MusicView.cs), 
    ///     - moves along the X-axis,
    ///     - plays the MIDI event when reach the end of the area, 
    ///     - then destroy.
    /// </summary>
    public class NoteView : MonoBehaviour
    {
        public static bool FirstNotePlayed = false;
        public MPTKEvent noteOn;
        public MidiStreamPlayer midiStreamPlayer;
        public bool played = false;
        public Material MatPlayed;
        public float zOriginal;

        public void Update()
        {
            // The midi event is played with a MidiStreamPlayer when position X < -45 (falling)
            if (!played && transform.position.x < -45f)
            {
                played = true;

                // Random instrument change when the original position is modified by a collider.
                // If original z is not the same, the note value will be changed.
                // Not good for the ears ... but funny.
                int delta = (int)(zOriginal - transform.position.z);
                //Debug.Log($"Note:{note.Value} Z:{transform.position.z:F1} DeltaZ:{delta} Travel Time:{note.LatenceTimeMillis} ms");

                //! [Example PlayNote]
                noteOn.Value += delta; // change the original note
                // Now play the note with a MidiStreamPlayer prefab
                midiStreamPlayer.MPTK_PlayEvent(noteOn);
                //! [Example PlayNote]

                FirstNotePlayed = true;

                gameObject.GetComponent<Renderer>().material = MatPlayed;// .color = Color.red;
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
