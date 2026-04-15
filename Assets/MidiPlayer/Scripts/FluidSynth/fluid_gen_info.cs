using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace MidiPlayerTK
{
    public class fluid_gen_info
    {
        /// <summary>@brief
        /// Generator number
        /// </summary>
        public int num;

        /// <summary>@brief
        /// Does the generator need to be initialized (cfr. fluid_voice_init()) 
        /// </summary>
        public int init;

        /// <summary>@brief
        /// The scale to convert from NRPN (cfr. fluid_gen_map_nrpn())
        /// </summary>
        public int nrpn_scale;

        /// <summary>@brief
        /// The minimum value 
        /// </summary>
        public float min;

        /// <summary>@brief
        /// The maximum value 
        /// </summary>
        public float max;

        /// <summary>@brief
        /// The default value (cfr. fluid_gen_set_default_values())
        /// </summary>
        public float def;

        public bool RealTimeChange;
        public string Description;

        public static fluid_gen_info[] FluidGenInfo = new fluid_gen_info[]
        {
                                        // number/name                        init  scale      min        max       default  modifiable        See SFSpec21 $8.1.3 
            new fluid_gen_info((int) fluid_gen_type.GEN_STARTADDROFS,           1,     1,       0.0f,     1e10f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_ENDADDROFS,             1,     1,     -1e10f,      0.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_STARTLOOPADDROFS,       1,     1,     -1e10f,     1e10f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_ENDLOOPADDROFS,         1,     1,     -1e10f,     1e10f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_STARTADDRCOARSEOFS,     0,     1,       0.0f,     1e10f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_MODLFOTOPITCH,          1,     2,  -12000.0f,  12000.0f,       0.0f, true, "Modulation LFO to pitch"),
            new fluid_gen_info((int) fluid_gen_type.GEN_VIBLFOTOPITCH,          1,     2,  -12000.0f,  12000.0f,       0.0f, true, "Vibrato LFO to pitch"),
            new fluid_gen_info((int) fluid_gen_type.GEN_MODENVTOPITCH,          1,     2,  -12000.0f,  12000.0f,       0.0f, true, "Modulation envelope to pitch"),
            new fluid_gen_info((int) fluid_gen_type.GEN_FILTERFC,               1,     2,    1500.0f,  13500.0f,   13500.0f, true, "Filter Cutoff"),
            new fluid_gen_info((int) fluid_gen_type.GEN_FILTERQ,                1,     1,       0.0f,    9600.0f,      0.0f, true, "Filter Extrem Q"), // max is x 10 from the standrad to propose strange experimentation ;-)
            new fluid_gen_info((int) fluid_gen_type.GEN_MODLFOTOFILTERFC,       1,     2,  -12000.0f,  12000.0f,       0.0f, true, "Modulation LFO to filter cutoff"),
            new fluid_gen_info((int) fluid_gen_type.GEN_MODENVTOFILTERFC,       1,     2,  -12000.0f,  12000.0f,       0.0f, true, "Modulation envelope to filter cutoff"),
            new fluid_gen_info((int) fluid_gen_type.GEN_ENDADDRCOARSEOFS,       0,     1,     -1e10f,      0.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_MODLFOTOVOL,            1,     1,    -960.0f,    960.0f,       0.0f, true, "Modulation LFO to volume"),
            new fluid_gen_info((int) fluid_gen_type.GEN_UNUSED1,                0,     0,       0.0f,      0.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_CHORUSSEND,             1,     1,       0.0f,   1000.0f,       0.0f, true, "Chorus send amount"),
            new fluid_gen_info((int) fluid_gen_type.GEN_REVERBSEND,             1,     1,       0.0f,   1000.0f,       0.0f, true, "Reverb send amount"),
            new fluid_gen_info((int) fluid_gen_type.GEN_PAN,                    1,     1,    -500.0f,    500.0f,       0.0f, true, "Stereo panning"),
            new fluid_gen_info((int) fluid_gen_type.GEN_UNUSED2,                0,     0,       0.0f,      0.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_UNUSED3,                0,     0,       0.0f,      0.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_UNUSED4,                0,     0,       0.0f,      0.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_MODLFODELAY,            1,     2,  -12000.0f,   5000.0f,  -12000.0f, true, "Modulation LFO delay"),
            new fluid_gen_info((int) fluid_gen_type.GEN_MODLFOFREQ,             1,     4,  -16000.0f,   4500.0f,       0.0f, true, "Modulation LFO frequency"),
            new fluid_gen_info((int) fluid_gen_type.GEN_VIBLFODELAY,            1,     2,  -12000.0f,   5000.0f,  -12000.0f, true, "Vibrato LFO delay"),
            new fluid_gen_info((int) fluid_gen_type.GEN_VIBLFOFREQ,             1,     4,  -16000.0f,   4500.0f,       0.0f, true, "Vibrato LFO frequency"),
            new fluid_gen_info((int) fluid_gen_type.GEN_MODENVDELAY,            1,     2,  -12000.0f,   5000.0f,  -12000.0f, true, "Modulation envelope delay"),
            new fluid_gen_info((int) fluid_gen_type.GEN_MODENVATTACK,           1,     2,  -12000.0f,   8000.0f,  -12000.0f, true, "Modulation envelope attack"),
            new fluid_gen_info((int) fluid_gen_type.GEN_MODENVHOLD,             1,     2,  -12000.0f,   5000.0f,  -12000.0f, true, "Modulation envelope hold"),
            new fluid_gen_info((int) fluid_gen_type.GEN_MODENVDECAY,            1,     2,  -12000.0f,   8000.0f,  -12000.0f, true, "Modulation envelope decay"),
            new fluid_gen_info((int) fluid_gen_type.GEN_MODENVSUSTAIN,          0,     1,       0.0f,   1000.0f,       0.0f, true, "Modulation envelope sustain"),
            new fluid_gen_info((int) fluid_gen_type.GEN_MODENVRELEASE,          1,     2,  -12000.0f,   8000.0f,  -12000.0f, true, "Modulation envelope release"),
            new fluid_gen_info((int) fluid_gen_type.GEN_KEYTOMODENVHOLD,        0,     1,   -1200.0f,   1200.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_KEYTOMODENVDECAY,       0,     1,   -1200.0f,   1200.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_VOLENVDELAY,            1,     2,  -12000.0f,   5000.0f,  -12000.0f, true, "Volume envelope delay"),
            new fluid_gen_info((int) fluid_gen_type.GEN_VOLENVATTACK,           1,     2,  -12000.0f,   8000.0f,  -12000.0f, true, "Volume envelope attack"),
            new fluid_gen_info((int) fluid_gen_type.GEN_VOLENVHOLD,             1,     2,  -12000.0f,   5000.0f,  -12000.0f, true, "Volume envelope hold"),
            new fluid_gen_info((int) fluid_gen_type.GEN_VOLENVDECAY,            1,     2,  -12000.0f,   8000.0f,  -12000.0f, true, "Volume envelope decay"),
            new fluid_gen_info((int) fluid_gen_type.GEN_VOLENVSUSTAIN,          0,     1,       0.0f,   1440.0f,       0.0f, true, "Volume envelope sustain"),
            new fluid_gen_info((int) fluid_gen_type.GEN_VOLENVRELEASE,          1,     2,  -12000.0f,   8000.0f,  -12000.0f, true, "Volume envelope release"),
            new fluid_gen_info((int) fluid_gen_type.GEN_KEYTOVOLENVHOLD,        0,     1,   -1200.0f,   1200.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_KEYTOVOLENVDECAY,       0,     1,   -1200.0f,   1200.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_INSTRUMENT,             0,     0,       0.0f,      0.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_RESERVED1,              0,     0,       0.0f,      0.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_KEYRANGE,               0,     0,       0.0f,    127.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_VELRANGE,               0,     0,       0.0f,    127.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_STARTLOOPADDRCOARSEOFS, 0,     1,     -1e10f,     1e10f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_KEYNUM,                 1,     0,       0.0f,    127.0f,      -1.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_VELOCITY,               1,     1,       0.0f,    127.0f,      -1.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_ATTENUATION,            1,     1,       0.0f,   1440.0f,       0.0f, true, "Volume attenuation"),
            new fluid_gen_info((int) fluid_gen_type.GEN_RESERVED2,              0,     0,       0.0f,      0.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_ENDLOOPADDRCOARSEOFS,   0,     1,     -1e10f,     1e10f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_COARSETUNE,             0,     1,    -120.0f,    120.0f,       0.0f, true, "Coarse tuning"),
            new fluid_gen_info((int) fluid_gen_type.GEN_FINETUNE,               0,     1,     -99.0f,     99.0f,       0.0f, true, "Fine tuning"),
            new fluid_gen_info((int) fluid_gen_type.GEN_SAMPLEID,               0,     0,       0.0f,      0.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_SAMPLEMODE,             0,     0,       0.0f,      0.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_RESERVED3,              0,     0,       0.0f,      0.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_SCALETUNE,              0,     1,       0.0f,   1200.0f,     100.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_EXCLUSIVECLASS,         0,     0,       0.0f,      0.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_OVERRIDEROOTKEY,        1,     0,       0.0f,    127.0f,      -1.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_PITCH,                  1,     0,       0.0f,    127.0f,       0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_CUSTOM_BALANCE,         1,     0,    -960.0f,     960.0f,      0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_CUSTOM_FILTERFC,        1,     2,       0.0f,   22050.0f,      0.0f, false, ""),
            new fluid_gen_info((int) fluid_gen_type.GEN_CUSTOM_FILTERQ,         1,     1,       0.0f,     960.0f,      0.0f, false, ""),
     };

        [Preserve]
        public fluid_gen_info()
        {

        }

        public fluid_gen_info(int pnum, int pinit, int pscale, float pmin, float pmax, float pdef, bool preplace, string pinfo)
        {
            num = pnum;
            init = pinit;
            nrpn_scale = pscale;
            min = pmin;
            max = pmax;
            def = pdef;
            RealTimeChange = preplace;
            Description = pinfo;
        }


        /**
         * Set an array of generators to their default values.
         * @param gen Array of generators (should be #GEN_LAST in size).
         * @return Always returns 0
         */
        static public void fluid_gen_set_default_values(HiGen[] gens)
        {
            for (int i = 0; i < gens.Length-1 /* avoid GEN_LAST */; i++)
            {
                gens[i].flags = fluid_gen_flags.GEN_UNUSED;
                gens[i].Mod = 0.0f;
                //gens[i].nrpn = 0.0;
                gens[i].Val = FluidGenInfo[i].def;
            }
        }

        [Preserve]
        public void fluid_gen_set_default_values()
        {

        }
        //#define fluid_gen_set_mod(_gen, _val)  { (_gen)->mod = (double) (_val); }
        //#define fluid_gen_set_nrpn(_gen, _val) { (_gen)->nrpn = (double) (_val); }
    }

    /// <summary>@brief
    /// Build list of synth modifier available
    /// </summary>
    public class GenModifier
    {
        static public List<MPTKListItem> RealTimeGenerator;
        public MPTKModeGeneratorChange Mode;
        public float NormalizedVal;
        public float SoundFontVal;

        public static float DefaultNormalizedVal(fluid_gen_type genType)
        {
            int genId = (int)genType;
            if (genId >= 0)
                return Mathf.InverseLerp(fluid_gen_info.FluidGenInfo[genId].min, fluid_gen_info.FluidGenInfo[genId].max, fluid_gen_info.FluidGenInfo[genId].def);
            else
                return 0f;
        }

        public static void InitListGenerator(bool log = false)
        {
            if (RealTimeGenerator == null)
            {
                RealTimeGenerator = new List<MPTKListItem>();
                RealTimeGenerator.Add(new MPTKListItem() { Index = -1, Label = "None" });
                foreach (fluid_gen_info gen in fluid_gen_info.FluidGenInfo)
                {
                    if (gen.RealTimeChange)
                    {
                        RealTimeGenerator.Add(new MPTKListItem() { Index = gen.num, Label = gen.Description });
                        if (log)
                            Debug.Log($"{gen.num,2} {gen.Description,36} {gen.min,6} {gen.max,6} {GenModifier.DefaultNormalizedVal((fluid_gen_type)gen.num):F2}");
                    }
                }
            }
        }
    }

    /// <summary>@brief
    /// Mode for applying the generator change.
    /// </summary>
    public enum MPTKModeGeneratorChange
    {
        Restaure = 0,
        Override = 1,
        Reinforce = 2,
    }
}
