using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MidiPlayerTK
{
    /// <summary>
    /// Some useful methods to get the label of a note.
    /// </summary>
    public class HelperNoteLabel
    {
        public int Midi;
        public string Label;
        public bool Sharp = false;
        public bool IsNoteC = false;
        public string Drum = "";

        static List<HelperNoteLabel> ListNote;
        static List<HelperNoteLabel> ListEcart;
        static public float _ratioHalfTone = 0.0594630943592952645618252949463f;

        /// <summary>@brief
        /// Is this note is a sharp ?
        /// </summary>
        /// <param name="midiValue"></param>
        /// <returns>true if the note is a sharp</returns>
        static public bool IsSharp(int midiValue)
        {
            try
            {
                if (ListNote == null) Init();
                if (midiValue < 0 || midiValue >= ListNote.Count)
                    return false;
                else
                    return ListNote[midiValue].Sharp;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"IsSharp {ex}");
            }
            return false;
        }

        /// <summary>@brief
        /// Get the note number (0=C, 11=B) from the MIDI value (0 to 127). v2.86.\n
        /// http://www.music.mcgill.ca/~ich/classes/mumt306/StandardMIDIfileformat.html#BMA1_3
        /// @li         0 -->  0 (D-1 / C)
        /// @li         1 -->  1 (D-1# / C#)
        /// @li             ...
        /// @li        60 -->  0 (DO / C)
        /// @li        61 -->  1 (DO# / C#)
        /// @li        62 -->  2 (Re / D)
        /// @li        63 -->  3 (Re# / D#)
        /// @li        64 -->  4 (Mi / E)
        /// @li        65 -->  5 (Fa / F)
        /// @li        66 -->  6 (Fa# / F#)
        /// @li        67 -->  7 (Sol / G)
        /// @li        68 -->  8 (Sol# / G#)
        /// @li        69 -->  9 (La / A)
        /// @li        70 --> 10 (La# / A#)
        /// @li        71 --> 11 (Si / B)
        /// @li        72 -->  0 (Do / C)
        /// @li             ...
        /// @li       126 -->  6 (FA# / F#)
        /// @li       127 -->  7 (SOL / G)
        /// 
        /// </summary>
        /// <param name="midiValue">Value of the midi event note-on from 0 to 127</param>
        /// @version Modification
        ///     - return was Note number from 0 (C) to 11 (B) 
        ///     - now return from -1 (C) to 11 (B) 
        /// <returns>Note number from 0 (C) to 11 (B) </returns>
        static public int NoteNumber(int midiValue)
        {
            return midiValue % 12;
        }

        /// <summary>
        /// Get the octave number from the MIDI value (0 to 127) based on Middle C = C4. v2.86.\n
        /// http://www.music.mcgill.ca/~ich/classes/mumt306/StandardMIDIfileformat.html#BMA1_3
        /// </summary>
        /// <param name="midiValue">Value of the midi event note-on </param>
        /// <returns>octave number between -1 and 9</returns>
        static public int OctaveNumber(int midiValue)
        {
            if (midiValue <= 11)
                return -1;
            else
                return (midiValue / 12) - 1;
        }

        /// <summary>@brief
        /// @deprecated replaced by LabelFromMidi
        /// Get the label of the note from a MIDI value. C4 standard. v2.86.\n
        /// Maestro follows the General MIDI standard based on Middle C: C4 for a value of 60.\n
        /// http://www.music.mcgill.ca/~ich/classes/mumt306/StandardMIDIfileformat.html#BMA1_3\n
        /// </summary>
        /// <param name="midiValue">Note value between 0 (C-1) and 127 (G9). 60 will return C4</param>
        /// <returns></returns>
        static public string LabelC4FromMidi(int midiValue)
        {
            Debug.LogWarning("Maestro 2.12.2 - LabelC4FromMidi is deprecated and will be removed. Please consider using LabelFromMidi");

            //if (midiValue < 12)
            //{
            //    return LabelFromMidi(midiValue).Replace("0", "-1");
            //}
            return LabelFromMidi(midiValue /*- 12*/);
        }

        /// <summary>@brief
        /// Get the label of the note from a MIDI value.\n
        /// Maestro Synth follows the MIDI standard based on Middle C = C4.\n
        /// http://www.music.mcgill.ca/~ich/classes/mumt306/StandardMIDIfileformat.html#BMA1_3\n
        /// Now this method is based on Middle C = C4, so 60 return "C4".\n
        /// @version 1.12.2 Modification
        ///     - now return "C4" for a value of 60 (was a "C5")
        ///     - LabelC4FromMidi is deprecated.
        /// </summary>
        /// <param name="midiValue">Note value between 0 (C-1) and 127 (G9). 60 will return C4</param>
        /// <returns></returns>
        static public string LabelFromMidi(int midiValue)
        {
            try
            {
                if (ListNote == null) Init();
                if (midiValue < 0 || midiValue >= ListNote.Count)
                    return "xx";
                else
                    return ListNote[midiValue].Label;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"LabelFromMidi {ex}");
            }
            return "xx";
        }

        /// <summary>@brief
        /// Get the label of the note (C, C#, ... E) from a value (0 to 11)
        /// </summary>
        /// <param name="valueNote"></param>
        /// <returns></returns>
        static public string LabelFromEcart(int valueNote)
        {
            try
            {
                if (ListEcart == null) Init();
                if (valueNote < 0 || valueNote >= 12)
                    return "xx";
                else
                    return ListEcart[valueNote].Label;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"LabelFromEcart {ex}");
            }
            return "xx";
        }

        /// <summary>@brief
        /// Get the name of the note from a MIDI value. C5 standard.\n
        /// In GM standard MIDI files, channel 9 (10 if start at channem 1) is reserved for percussion instruments only.\n
        /// Notes recorded on channel 10/17 always produce percussion sounds. Each distinct note number specifies a unique percussive instrument, rather than the sound's pitch.\n
        /// https://en.wikipedia.org/wiki/General_MIDI\n
        /// </summary>
        /// <param name="midiValue">Note value between 27 (D2#) and 87 (D7#) return the label of a percussive instrument else return an empty string.</param>
        /// <returns></returns>
        static public string LabelPercussion(int midiValue)
        {
            try
            {
                if (ListNote == null) Init();
                if (midiValue < 0 || midiValue > 127)
                    return "";
                else
                {
                    return ListNote[midiValue].Drum;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"LabelPercussion {ex}");
            }
            return "";
        }

        static public void Init()
        {
            try
            {
                ListEcart = new List<HelperNoteLabel>();
                ListEcart.Add(new HelperNoteLabel() { Label = "C", Midi = 0, IsNoteC = true, });
                ListEcart.Add(new HelperNoteLabel() { Label = "C#", Midi = 1, Sharp = true, });
                ListEcart.Add(new HelperNoteLabel() { Label = "D", Midi = 2, });
                ListEcart.Add(new HelperNoteLabel() { Label = "D#", Midi = 3, Sharp = true, });
                ListEcart.Add(new HelperNoteLabel() { Label = "E", Midi = 4, });
                ListEcart.Add(new HelperNoteLabel() { Label = "F", Midi = 5, });
                ListEcart.Add(new HelperNoteLabel() { Label = "F#", Midi = 6, Sharp = true, });
                ListEcart.Add(new HelperNoteLabel() { Label = "G", Midi = 7, });
                ListEcart.Add(new HelperNoteLabel() { Label = "G#", Midi = 8, Sharp = true, });
                ListEcart.Add(new HelperNoteLabel() { Label = "A", Midi = 9, });
                ListEcart.Add(new HelperNoteLabel() { Label = "A#", Midi = 10, Sharp = true, });
                ListEcart.Add(new HelperNoteLabel() { Label = "B", Midi = 11, });

                ListNote = new List<HelperNoteLabel>();
                ListNote.Add(new HelperNoteLabel() { Label = "C-1", Midi = 0, IsNoteC = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "C-1#", Midi = 1, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "D-1", Midi = 2, });
                ListNote.Add(new HelperNoteLabel() { Label = "D-1#", Midi = 3, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "E-1", Midi = 4, });
                ListNote.Add(new HelperNoteLabel() { Label = "F-1", Midi = 5, });
                ListNote.Add(new HelperNoteLabel() { Label = "F-1#", Midi = 6, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "G-1", Midi = 7, });
                ListNote.Add(new HelperNoteLabel() { Label = "G-1#", Midi = 8, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "A-1", Midi = 9, });
                ListNote.Add(new HelperNoteLabel() { Label = "A-1#", Midi = 10, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "B-1", Midi = 11, });
                ListNote.Add(new HelperNoteLabel() { Label = "C0", Midi = 12, IsNoteC = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "C0#", Midi = 13, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "D0", Midi = 14, });
                ListNote.Add(new HelperNoteLabel() { Label = "D0#", Midi = 15, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "E0", Midi = 16, });
                ListNote.Add(new HelperNoteLabel() { Label = "F0", Midi = 17, });
                ListNote.Add(new HelperNoteLabel() { Label = "F0#", Midi = 18, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "G0", Midi = 19, });
                ListNote.Add(new HelperNoteLabel() { Label = "G0#", Midi = 20, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "A0", Midi = 21, });
                ListNote.Add(new HelperNoteLabel() { Label = "A0#", Midi = 22, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "B0", Midi = 23, });
                ListNote.Add(new HelperNoteLabel() { Label = "C1", Midi = 24, IsNoteC = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "C1#", Midi = 25, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "D1", Midi = 26, });
                ListNote.Add(new HelperNoteLabel() { Label = "D1#", Midi = 27, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "E1", Midi = 28, });
                ListNote.Add(new HelperNoteLabel() { Label = "F1", Midi = 29, });
                ListNote.Add(new HelperNoteLabel() { Label = "F1#", Midi = 30, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "G1", Midi = 31, });
                ListNote.Add(new HelperNoteLabel() { Label = "G1#", Midi = 32, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "A1", Midi = 33, });
                ListNote.Add(new HelperNoteLabel() { Label = "A1#", Midi = 34, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "B1", Midi = 35, });
                ListNote.Add(new HelperNoteLabel() { Label = "C2", Midi = 36, IsNoteC = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "C2#", Midi = 37, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "D2", Midi = 38, });


                ListNote.Add(new HelperNoteLabel() { Label = "D2#", Midi = 39, Sharp = true, Drum = "Hand Clap", });
                ListNote.Add(new HelperNoteLabel() { Label = "E2", Midi = 40, Drum = "Electric Snare", });
                ListNote.Add(new HelperNoteLabel() { Label = "F2", Midi = 41, Drum = "Low Floor Tom", });
                ListNote.Add(new HelperNoteLabel() { Label = "F2#", Midi = 42, Sharp = true, Drum = "Closed Hi Hat", });
                ListNote.Add(new HelperNoteLabel() { Label = "G2", Midi = 43, Drum = "High Floor Tom", });
                ListNote.Add(new HelperNoteLabel() { Label = "G2#", Midi = 44, Sharp = true, Drum = "Pedal Hi Hat", });
                ListNote.Add(new HelperNoteLabel() { Label = "A2", Midi = 45, Drum = "Low Tom", });
                ListNote.Add(new HelperNoteLabel() { Label = "A2#", Midi = 46, Sharp = true, Drum = "Open Hi Hat", });
                ListNote.Add(new HelperNoteLabel() { Label = "B2", Midi = 47, Drum = "Low-Mid Tom", });
                ListNote.Add(new HelperNoteLabel() { Label = "C3", Midi = 48, IsNoteC = true, Drum = "High Mid Tom", });
                ListNote.Add(new HelperNoteLabel() { Label = "C3#", Midi = 49, Sharp = true, Drum = "Crash Cymbal 1", });
                ListNote.Add(new HelperNoteLabel() { Label = "D3", Midi = 50, Drum = "High Tom", });
                ListNote.Add(new HelperNoteLabel() { Label = "D3#", Midi = 51, Sharp = true, Drum = "Ride Cymbal 1", });
                ListNote.Add(new HelperNoteLabel() { Label = "E3", Midi = 52, Drum = "Chinese Cymbal", });
                ListNote.Add(new HelperNoteLabel() { Label = "F3", Midi = 53, Drum = "Ride Bell", });
                ListNote.Add(new HelperNoteLabel() { Label = "F3#", Midi = 54, Sharp = true, Drum = "Tambourine", });
                ListNote.Add(new HelperNoteLabel() { Label = "G3", Midi = 55, Drum = "Splash Cymbal", });
                ListNote.Add(new HelperNoteLabel() { Label = "G3#", Midi = 56, Sharp = true, Drum = "Cowbell", });
                ListNote.Add(new HelperNoteLabel() { Label = "A3", Midi = 57, Drum = "Crash Cymbal 2", });
                ListNote.Add(new HelperNoteLabel() { Label = "A3#", Midi = 58, Sharp = true, Drum = "Vibraslap", });
                ListNote.Add(new HelperNoteLabel() { Label = "B3", Midi = 59, Drum = "Ride Cymbal 2", });
                ListNote.Add(new HelperNoteLabel() { Label = "C4", Midi = 60, IsNoteC = true, Drum = "Hi Bongo", });
                ListNote.Add(new HelperNoteLabel() { Label = "C4#", Midi = 61, Sharp = true, Drum = "Low Bongo", });
                ListNote.Add(new HelperNoteLabel() { Label = "D4", Midi = 62, Drum = "Mute Hi Conga", });
                ListNote.Add(new HelperNoteLabel() { Label = "D4#", Midi = 63, Sharp = true, Drum = "Open Hi Conga", });
                ListNote.Add(new HelperNoteLabel() { Label = "E4", Midi = 64, Drum = "Low Conga", });
                ListNote.Add(new HelperNoteLabel() { Label = "F4", Midi = 65, Drum = "High Timbale", });
                ListNote.Add(new HelperNoteLabel() { Label = "F4#", Midi = 66, Sharp = true, Drum = "Low Timbale", });
                ListNote.Add(new HelperNoteLabel() { Label = "G4", Midi = 67, Drum = "High Agogo", });
                ListNote.Add(new HelperNoteLabel() { Label = "G4#", Midi = 68, Sharp = true, Drum = "Low Agogo", });
                ListNote.Add(new HelperNoteLabel() { Label = "A4", Midi = 69, Drum = "Cabasa", });
                ListNote.Add(new HelperNoteLabel() { Label = "A4#", Midi = 70, Sharp = true, Drum = "Maracas", });
                ListNote.Add(new HelperNoteLabel() { Label = "B4", Midi = 71, Drum = "Short Whistle", });
                ListNote.Add(new HelperNoteLabel() { Label = "C5", Midi = 72, IsNoteC = true, Drum = "Long Whistle", });
                ListNote.Add(new HelperNoteLabel() { Label = "C5#", Midi = 73, Sharp = true, Drum = "Short Guiro", });
                ListNote.Add(new HelperNoteLabel() { Label = "D5", Midi = 74, Drum = "Long Guiro", });
                ListNote.Add(new HelperNoteLabel() { Label = "D5#", Midi = 75, Sharp = true, Drum = "Claves", });
                ListNote.Add(new HelperNoteLabel() { Label = "E5", Midi = 76, Drum = "Hi Wood Block", });
                ListNote.Add(new HelperNoteLabel() { Label = "F5", Midi = 77, Drum = "Low Wood Block", });
                ListNote.Add(new HelperNoteLabel() { Label = "F5#", Midi = 78, Sharp = true, Drum = "Mute Cuica", });
                ListNote.Add(new HelperNoteLabel() { Label = "G5", Midi = 79, Drum = "Open Cuica", });
                ListNote.Add(new HelperNoteLabel() { Label = "G5#", Midi = 80, Sharp = true, Drum = "Mute Triangle", });
                ListNote.Add(new HelperNoteLabel() { Label = "A5", Midi = 81, Drum = "Open Triangle", });
                ListNote.Add(new HelperNoteLabel() { Label = "A5#", Midi = 82, Sharp = true, Drum = "Shaker", });
                ListNote.Add(new HelperNoteLabel() { Label = "B5", Midi = 83, Drum = "Jingle Bell", });
                ListNote.Add(new HelperNoteLabel() { Label = "C6", Midi = 84, IsNoteC = true, Drum = "Belltree", });
                ListNote.Add(new HelperNoteLabel() { Label = "C6#", Midi = 85, Sharp = true, Drum = "Castanets", });
                ListNote.Add(new HelperNoteLabel() { Label = "D6", Midi = 86, Drum = "Mute Surdo", });
                ListNote.Add(new HelperNoteLabel() { Label = "D6#", Midi = 87, Sharp = true, Drum = "Open Surdo", });

                ListNote.Add(new HelperNoteLabel() { Label = "E6", Midi = 88, Drum = "Hi Wood Block", });
                ListNote.Add(new HelperNoteLabel() { Label = "F6", Midi = 89, Drum = "Low Wood Block", });
                ListNote.Add(new HelperNoteLabel() { Label = "F6#", Midi = 90, Sharp = true, Drum = "Mute Cuica", });
                ListNote.Add(new HelperNoteLabel() { Label = "G6", Midi = 91, Drum = "Open Cuica", });
                ListNote.Add(new HelperNoteLabel() { Label = "G6#", Midi = 92, Sharp = true, Drum = "Mute Triangle", });
                ListNote.Add(new HelperNoteLabel() { Label = "A6", Midi = 93, Drum = "Open Triangle", });
                ListNote.Add(new HelperNoteLabel() { Label = "A6#", Midi = 94, Sharp = true, Drum = "Shaker", });
                ListNote.Add(new HelperNoteLabel() { Label = "B6", Midi = 95, Drum = "Jingle Bell", });
                ListNote.Add(new HelperNoteLabel() { Label = "C7", Midi = 96, IsNoteC = true, Drum = "Belltree", });
                ListNote.Add(new HelperNoteLabel() { Label = "C7#", Midi = 97, Sharp = true, Drum = "Castanets", });
                ListNote.Add(new HelperNoteLabel() { Label = "D7", Midi = 98, Drum = "Mute Surdo", });
                ListNote.Add(new HelperNoteLabel() { Label = "D7#", Midi = 99, Sharp = true, Drum = "Open Surdo", });

                ListNote.Add(new HelperNoteLabel() { Label = "E7", Midi = 100, });
                ListNote.Add(new HelperNoteLabel() { Label = "F7", Midi = 101, });
                ListNote.Add(new HelperNoteLabel() { Label = "F7#", Midi = 102, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "G7", Midi = 103, });
                ListNote.Add(new HelperNoteLabel() { Label = "G7#", Midi = 104, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "A7", Midi = 105, });
                ListNote.Add(new HelperNoteLabel() { Label = "A7#", Midi = 106, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "B7", Midi = 107, });
                ListNote.Add(new HelperNoteLabel() { Label = "C8", Midi = 108, IsNoteC = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "C8#", Midi = 109, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "D8", Midi = 110, });
                ListNote.Add(new HelperNoteLabel() { Label = "D8#", Midi = 111, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "E8", Midi = 112, });
                ListNote.Add(new HelperNoteLabel() { Label = "F8", Midi = 113, });
                ListNote.Add(new HelperNoteLabel() { Label = "F8#", Midi = 114, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "G8", Midi = 115, });
                ListNote.Add(new HelperNoteLabel() { Label = "G8#", Midi = 116, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "A8", Midi = 117, });
                ListNote.Add(new HelperNoteLabel() { Label = "A8#", Midi = 118, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "B8", Midi = 119, });
                ListNote.Add(new HelperNoteLabel() { Label = "C9", Midi = 120, IsNoteC = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "C9#", Midi = 121, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "D9", Midi = 122, });
                ListNote.Add(new HelperNoteLabel() { Label = "D9#", Midi = 123, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "E9", Midi = 124, });
                ListNote.Add(new HelperNoteLabel() { Label = "F9", Midi = 125, });
                ListNote.Add(new HelperNoteLabel() { Label = "F9#", Midi = 126, Sharp = true, });
                ListNote.Add(new HelperNoteLabel() { Label = "G9", Midi = 127, });
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"LabelPercussione {ex}");
            }
            // For test
            //ListNote[60].Ratio = 1f; // C3
            //ListNote[60].Frequence = 261.626f; // C3

            //foreach (HelperNote hn in ListNote)
            //{
            //    hn.Ratio = Mathf.Pow(_ratioHalfTone, hn.Midi);
            //    hn.Frequence = ListNote[48].Frequence * hn.Ratio;
            //    //Debug.Log("Position:" + hn.Position +" Hauteur:" + hn.Hauteur +" Label:" + hn.Label +" Ratio:" + hn.Ratio +" Frequence:" + hn.Frequence);
            //}
        }
    }

    public class HelperNoteRatio
    {
        public int Delta;
        public float Ratio;
        static float[] Ratios;
        static public float _ratioHalfTone = 1.0594630943592952645618252949463f;
        static public float _rationCents = 1.0005777895f;

        /// <summary>@brief
        /// 
        /// </summary>
        /// <param name="delta">from -60 to 60</param>
        /// <returns></returns>
        static public float Get(int delta, int finetune)
        {
            try
            {
                return Mathf.Pow(_ratioHalfTone, (float)delta + (float)finetune / 100f);
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return 0f;
        }
        static public void Init()
        {
            try
            {
                Ratios = new float[120];
                for (int index = 0; index < 120; index++)
                {
                    Ratios[index] = Mathf.Pow(_ratioHalfTone, index - 60);
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }
    }
}
