namespace MidiPlayerTK
{
    /// <summary>@brief
    /// Cover fluid_inst_zone_t and fluid_preset_zone_t
    /// </summary>
    public class HiZone
    {
        /// <summary>@brief
        /// unique item id (see int note above)
        /// </summary>
        public int ItemId;

        // V2.89.0 removed (not used)  Instrument defined in this zone (only for preset zone)
        // public HiInstrument Instrument;

        //public string Name;
        //public fluid_sample_t sample;
        /// <summary>@brief
        /// Index to the sample (only for instrument zone)
        /// </summary>
        public int Index;
        public int KeyLo;
        public int KeyHi;
        public int VelLo;
        public int VelHi;
        //public fluid_gen_t[] gen;
        public HiGen[] gens; // gen in FS 2.3 

        public HiGen[] genE;

        public HiMod[] mods; /* List of modulators */


        public HiZone()
        {
            //sample = null;
            Index = -1;
            KeyLo = 0;
            KeyHi = 128;
            VelLo = 0;
            VelHi = 128;
        }
    }
}
