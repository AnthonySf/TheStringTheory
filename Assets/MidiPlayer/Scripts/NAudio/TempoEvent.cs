using System;
using System.IO;
using System.Text;

namespace MPTK.NAudio.Midi 
{
    /// <summary>@brief
    /// Represents a MIDI tempo event
    /// </summary>
    public class TempoEvent : MetaEvent 
    {
        private int microsecondsPerQuarterNote;
        
        /// <summary>@brief
        /// Reads a new tempo event from a MIDI stream
        /// </summary>
        /// <param name="br">The MIDI stream</param>
        /// <param name="length">the data length</param>
        public TempoEvent(BinaryReader br,int length) 
        {
            if(length != 3) 
            {
                throw new FormatException("Invalid tempo length");
            }
            microsecondsPerQuarterNote = (br.ReadByte() << 16) + (br.ReadByte() << 8) + br.ReadByte();
        }

        /// <summary>@brief
        /// Creates a new tempo event with specified settings
        /// </summary>
        /// <param name="microsecondsPerQuarterNote">Microseconds per quarter note</param>
        /// <param name="absoluteTime">Absolute time</param>
        public TempoEvent(int microsecondsPerQuarterNote, long absoluteTime)
            : base(MetaEventType.SetTempo,3,absoluteTime)
        {
            this.microsecondsPerQuarterNote = microsecondsPerQuarterNote;
        }

        /// <summary>@brief
        /// Creates a deep clone of this MIDI event.
        /// </summary>
        public override MidiEvent Clone()
        {
            return (TempoEvent)MemberwiseClone();
        }

        /// <summary>@brief
        /// Describes this tempo event
        /// </summary>
        /// <returns>String describing the tempo event</returns>
        public override string ToString() 
        {
            return String.Format("{0} {2}bpm ({1})",
                base.ToString(),
                microsecondsPerQuarterNote,
                (60000000 / microsecondsPerQuarterNote));
        }

        /// <summary>@brief
        /// Microseconds per quarter note
        /// </summary>
        public int MicrosecondsPerQuarterNote
        {
            get { return microsecondsPerQuarterNote; }
            set { microsecondsPerQuarterNote = value; }
        }

        /// <summary>@brief
        /// Tempo: quarter per minute
        /// </summary>
        public double Tempo
        {
            get { return (60000000.0/microsecondsPerQuarterNote); }
            set { microsecondsPerQuarterNote = (int) (60000000.0/value); }
        }

        /// <summary>@brief
        /// Calls base class export first, then exports the data 
        /// specific to this event
        /// <seealso cref="MidiEvent.Export">MidiEvent.Export</seealso>
        /// </summary>
        public override void Export(ref long absoluteTime, BinaryWriter writer)
        {
            base.Export(ref absoluteTime, writer);
            writer.Write((byte) ((microsecondsPerQuarterNote >> 16) & 0xFF));
            writer.Write((byte) ((microsecondsPerQuarterNote >> 8) & 0xFF));
            writer.Write((byte) (microsecondsPerQuarterNote & 0xFF));
        }
    }
}
