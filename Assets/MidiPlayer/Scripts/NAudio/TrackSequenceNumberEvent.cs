using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MPTK.NAudio.Midi
{
    /// <summary>@brief
    /// Represents a MIDI track sequence number event event
    /// </summary>
    public class TrackSequenceNumberEvent : MetaEvent
    {
        private ushort sequenceNumber;

        /// <summary>@brief
        /// Creates a new track sequence number event
        /// </summary>
        public TrackSequenceNumberEvent(ushort sequenceNumber)
        {
            this.sequenceNumber = sequenceNumber;
        }

        /// <summary>@brief
        /// Reads a new track sequence number event from a MIDI stream
        /// </summary>
        /// <param name="br">The MIDI stream</param>
        /// <param name="length">the data length</param>
        public TrackSequenceNumberEvent(BinaryReader br, int length)
        {
            // TODO: there is a form of the TrackSequenceNumberEvent that
            // has a length of zero
            // TBN Change - process also length of 3 else return 0
            //if (length != 2)
            //{
            //    throw new FormatException("Invalid sequence number length");
            //}
            if (length == 2)
                sequenceNumber = (ushort)((br.ReadByte() << 8) + br.ReadByte());
            else
            {
                br.ReadBytes(/*length*/2);
                sequenceNumber = 0;
            }
        }

        /// <summary>@brief
        /// Creates a deep clone of this MIDI event.
        /// </summary>
        public override MidiEvent Clone()
        {
            return (TrackSequenceNumberEvent)MemberwiseClone();
        }

        /// <summary>@brief
        /// Describes this event
        /// </summary>
        /// <returns>String describing the event</returns>
        public override string ToString()
        {
            return String.Format("{0} {1}", base.ToString(), sequenceNumber);
        }

        /// <summary>@brief
        /// Calls base class export first, then exports the data 
        /// specific to this event
        /// <seealso cref="MidiEvent.Export">MidiEvent.Export</seealso>
        /// </summary>
        public override void Export(ref long absoluteTime, BinaryWriter writer)
        {
            base.Export(ref absoluteTime, writer);
            writer.Write((byte)((sequenceNumber >> 8) & 0xFF));
            writer.Write((byte)(sequenceNumber & 0xFF));
        }
    }
}
