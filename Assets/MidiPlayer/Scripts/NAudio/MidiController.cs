using System;

namespace MPTK.NAudio.Midi
{
    /// <summary>@brief
    /// MidiController enumeration
    /// http://www.midi.org/techspecs/midimessages.php#3
    /// </summary>
    public enum MidiController : byte 
    {
        /// <summary>@briefBank Select (MSB)</summary>
        BankSelect = 0,
        /// <summary>@briefModulation (MSB)</summary>
        Modulation = 1,
        /// <summary>@briefBreath Controller</summary>
        BreathController = 2,
        /// <summary>@briefFoot controller (MSB)</summary>
        FootController = 4,
        /// <summary>@briefMain volume</summary>
        MainVolume = 7,
        /// <summary>@briefPan</summary>
        Pan = 10,
        /// <summary>@briefExpression</summary>
        Expression = 11,
        /// <summary>@briefBank Select LSB</summary>
        BankSelectLsb = 32,
        /// <summary>@briefSustain</summary>
        Sustain = 64,
        /// <summary>@briefPortamento On/Off</summary>
        Portamento = 65,
        /// <summary>@briefSostenuto On/Off</summary>
        Sostenuto = 66,
        /// <summary>@briefSoft Pedal On/Off</summary>
        SoftPedal = 67,
        /// <summary>@briefLegato Footswitch</summary>
        LegatoFootswitch = 68,
        /// <summary>@briefReset all controllers</summary>
        ResetAllControllers = 121,
        /// <summary>@briefAll notes off</summary>
        AllNotesOff = 123,
    }
}
