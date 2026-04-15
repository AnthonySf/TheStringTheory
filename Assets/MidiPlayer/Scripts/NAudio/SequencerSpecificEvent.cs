using System;
using System.IO;
using System.Text;

namespace MPTK.NAudio.Midi
{
    /// <summary>@brief
    /// Represents a Sequencer Specific event
    /// </summary>
    public class SequencerSpecificEvent : MetaEvent
    {
        private byte[] data;

        /// <summary>@brief
        /// Reads a new sequencer specific event from a MIDI stream
        /// </summary>
        /// <param name="br">The MIDI stream</param>
        /// <param name="length">The data length</param>
        public SequencerSpecificEvent(BinaryReader br, int length)
        {
            this.data = br.ReadBytes(length);
        }

        /// <summary>@brief
        /// Creates a new Sequencer Specific event
        /// </summary>
        /// <param name="data">The sequencer specific data</param>
        /// <param name="absoluteTime">Absolute time of this event</param>
        public SequencerSpecificEvent(byte[] data, long absoluteTime)
            : base(MetaEventType.SequencerSpecific, data.Length, absoluteTime)
        {
            this.data = data;
        }

        /// <summary>@brief
        /// Creates a deep clone of this MIDI event.
        /// </summary>
        public override MidiEvent Clone()
        {
            return new SequencerSpecificEvent((byte[])data.Clone(), AbsoluteTime);
        }

        /// <summary>@brief
        /// The contents of this sequencer specific
        /// </summary>
        public byte[] Data
        {
            get
            {
                return this.data;
            }
            set
            {
                this.data = value;
                // this.metaDataLength = this.data.Length; // TBN
            }
        }

        /// <summary>@brief
        /// Describes this MIDI text event
        /// </summary>
        /// <returns>A string describing this event</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(base.ToString());
            sb.Append(" ");
            foreach (var b in data)
            {
                sb.AppendFormat("{0:X2} ", b);
            }
            sb.Length--;
            return sb.ToString();
        }

        /// <summary>@brief
        /// Calls base class export first, then exports the data 
        /// specific to this event
        /// <seealso cref="MidiEvent.Export">MidiEvent.Export</seealso>
        /// </summary>
        public override void Export(ref long absoluteTime, BinaryWriter writer)
        {
            base.Export(ref absoluteTime, writer);
            writer.Write(data);
        }
    }
}
