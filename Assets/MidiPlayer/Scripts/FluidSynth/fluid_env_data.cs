using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MidiPlayerTK
{

    /*
     * envelope data
     */
    public class fluid_env_data
    {
        public uint count;
        public float coeff;
        public float incr;
        public float min;
        public float max;
        public override string ToString()
        {
            return string.Format("count:{0} coeff:{1} incr:{2} min:{3} max:{4}", count, coeff, incr, min, max);
        }
    }

    /* Indices for envelope tables */
    public enum fluid_voice_envelope_index
    {
        FLUID_VOICE_ENVDELAY,
        FLUID_VOICE_ENVATTACK,
        FLUID_VOICE_ENVHOLD,
        FLUID_VOICE_ENVDECAY,
        FLUID_VOICE_ENVSUSTAIN,
        FLUID_VOICE_ENVRELEASE,
        FLUID_VOICE_ENVFINISHED,
        FLUID_VOICE_ENVLAST
    }
}
