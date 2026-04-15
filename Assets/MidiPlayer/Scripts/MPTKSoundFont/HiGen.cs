namespace MidiPlayerTK
{
    public enum fluid_gen_flags:byte
    {
        GEN_UNUSED,     /**< Generator value is not set */
        GEN_SET,        /**< Generator value is set at innstrument level*/
    }

    /// <summary>@brief
    /// Generator type. Value are defined in soundfont for each instruments and each note. 
    /// This default value can be overriden in real time. 
    /// See MPTKEvent class and https://paxstellar.fr/class-mptkevent#Generator-List
    /// </summary>
    public  enum fluid_gen_type  :byte
    {
        GEN_STARTADDROFS,       /**< Sample start address offset (0-32767) */
        GEN_ENDADDROFS,     /**< Sample end address offset (-32767-0) */
        GEN_STARTLOOPADDROFS,       /**< Sample loop start address offset (-32767-32767) */
        GEN_ENDLOOPADDROFS,     /**< Sample loop end address offset (-32767-32767) */
        GEN_STARTADDRCOARSEOFS, /**< Sample start address coarse offset (X 32768) */
        GEN_MODLFOTOPITCH,      /**< Modulation LFO to pitch */
        GEN_VIBLFOTOPITCH,      /**< Vibrato LFO to pitch */
        GEN_MODENVTOPITCH,      /**< Modulation envelope to pitch */
        GEN_FILTERFC,           /**< Filter cutoff */
        GEN_FILTERQ,            /**< Filter Q */
        GEN_MODLFOTOFILTERFC,       /**< Modulation LFO to filter cutoff */
        GEN_MODENVTOFILTERFC,       /**< Modulation envelope to filter cutoff */
        GEN_ENDADDRCOARSEOFS,       /**< Sample end address coarse offset (X 32768) 12 */
        GEN_MODLFOTOVOL,        /**< Modulation LFO to volume */
        GEN_UNUSED1,            /**< Unused */
        GEN_CHORUSSEND,     /**< Chorus send amount */
        GEN_REVERBSEND,     /**< Reverb send amount */
        GEN_PAN,            /**< Stereo panning */
        GEN_UNUSED2,            /**< Unused */
        GEN_UNUSED3,            /**< Unused */
        GEN_UNUSED4,            /**< Unused */
        GEN_MODLFODELAY,        /**< Modulation LFO delay */
        GEN_MODLFOFREQ,     /**< Modulation LFO frequency */
        GEN_VIBLFODELAY,        /**< Vibrato LFO delay */
        GEN_VIBLFOFREQ,     /**< Vibrato LFO frequency */
        GEN_MODENVDELAY,        /**< Modulation envelope delay */
        GEN_MODENVATTACK,       /**< Modulation envelope attack */
        GEN_MODENVHOLD,     /**< Modulation envelope hold */
        GEN_MODENVDECAY,        /**< Modulation envelope decay */
        GEN_MODENVSUSTAIN,      /**< Modulation envelope sustain */
        GEN_MODENVRELEASE,      /**< Modulation envelope release */
        GEN_KEYTOMODENVHOLD,        /**< Key to modulation envelope hold */
        GEN_KEYTOMODENVDECAY,       /**< Key to modulation envelope decay */
        GEN_VOLENVDELAY,        /**< Volume envelope delay */
        GEN_VOLENVATTACK,       /**< Volume envelope attack */
        GEN_VOLENVHOLD,     /**< Volume envelope hold */
        GEN_VOLENVDECAY,        /**< Volume envelope decay */
        GEN_VOLENVSUSTAIN,      /**< Volume envelope sustain */
        GEN_VOLENVRELEASE,      /**< Volume envelope release */
        GEN_KEYTOVOLENVHOLD,        /**< Key to volume envelope hold */
        GEN_KEYTOVOLENVDECAY,       /**< Key to volume envelope decay */
        GEN_INSTRUMENT,     /**< Instrument ID (shouldn't be set by user) */
        GEN_RESERVED1,      /**< Reserved */
        GEN_KEYRANGE,           /**< MIDI note range */
        GEN_VELRANGE,           /**< MIDI velocity range 44 */
        GEN_STARTLOOPADDRCOARSEOFS, /**< Sample start loop address coarse offset (X 32768) */
        GEN_KEYNUM,         /**< Fixed MIDI note number */
        GEN_VELOCITY,           /**< Fixed MIDI velocity value */
        GEN_ATTENUATION,        /**< Initial volume attenuation 48 */
        GEN_RESERVED2,      /**< Reserved */
        GEN_ENDLOOPADDRCOARSEOFS,   /**< Sample end loop address coarse offset (X 32768) */
        GEN_COARSETUNE,     /**< Coarse tuning */
        GEN_FINETUNE,           /**< Fine tuning */
        GEN_SAMPLEID,           /**< Sample ID (shouldn't be set by user) */
        GEN_SAMPLEMODE,     /**< Sample mode flags 54 */
        GEN_RESERVED3,      /**< Reserved */
        GEN_SCALETUNE,      /**< Scale tuning */
        GEN_EXCLUSIVECLASS,     /**< Exclusive class number */
        GEN_OVERRIDEROOTKEY,        /**< Sample root note override 58 */

        /**
             * Initial Pitch
             *
             * @note This is not "standard" SoundFont generator, because it is not
             * mentioned in the list of generators in the SF2 specifications.
             * It is used by FluidSynth internally to compute the nominal pitch of
             * a note on note-on event. By nature it shouldn't be allowed to be modulated,
             * however the specification defines a default modulator having "Initial Pitch"
             * as destination (cf. SF2.01 page 57 section 8.4.10 MIDI Pitch Wheel to Initial Pitch).
             * Thus it is impossible to cancel this default modulator, which would be required
             * to let the MIDI Pitch Wheel controller modulate a different generator.
             * In order to provide this flexibility, FluidSynth >= 2.1.0 uses a default modulator
             * "Pitch Wheel to Fine Tune", rather than Initial Pitch. The same "compromise" can
             * be found on the Audigy 2 ZS for instance.
             */
        GEN_PITCH,          /**< Pitch (NOTE: Not a real SoundFont generator) */

        // FS 2.3
        GEN_CUSTOM_BALANCE,          /**< Balance @note Not a real SoundFont generator */
        /* non-standard generator for an additional custom high- or low-pass filter */
        GEN_CUSTOM_FILTERFC,        /**< Custom filter cutoff frequency */
        GEN_CUSTOM_FILTERQ,     /**< Custom filter Q */

        GEN_LAST			/** 63 < @internal Value defines the count of generators (#fluid_gen_type)
                          @warning This symbol is not part of the public API and ABI
                          stability guarantee and may change at any time! */

    }

    public class HiGenAmount
    {               /* Generator amount structure */
        public ushort Uword;      /* unsigned 16 bit value */

        /// <summary>@brief
        /// Generator amount as a signed short
        /// </summary>
        public short Sword
        {
            get { return (short)Uword; }
            set { Uword = (ushort)value; }
        }

        /// <summary>@brief
        /// Low byte amount
        /// </summary>
        public byte Lo
        {
            get { return (byte)(Uword & 0x00FF); }
            set { Uword &= 0xFF00; Uword += value; }
        }

        /// <summary>@brief
        /// High byte amount
        /// </summary>
        public byte Hi
        {
            get { return (byte)((Uword & 0xFF00) >> 8); }
            set { Uword &= 0x00FF; Uword += (ushort)(value << 8); }
        }
    }

    /// <summary>@brief
    /// Defined generator from fluid_gen_t
    /// </summary>
    public class HiGen
    {
        // not used with ISerializable [NonSerialized]
        public fluid_gen_type type;
        public fluid_gen_flags flags; /**< Is the generator set or not (#fluid_gen_flags) */
        public float Val;          /**< The nominal value */
        public float Mod;          /**< Change by modulators */
        //public double nrpn;         /**< Change by NRPN messages - not used with MPTK */
        /// <summary>@brief
        /// generator value
        /// </summary>
        public HiGenAmount Amount;

        /// <summary>@brief
        ///  Set an array of generators to their initial value
        /// </summary>
        /// <param name="gens"></param>
        /// <param name="channel"></param>
        static public void fluid_gen_init(HiGen[] gens, MPTKChannel channel)
        {
            fluid_gen_info.fluid_gen_set_default_values(gens);

            //for (int i = 0; i < gens.Length; i++)
            //{
            //    gens[i].nrpn = channel.gens[i];

                ///* This is an extension to the SoundFont standard. More
                // * documentation is available at the fluid_synth_set_gen2()
                // * function. */
                //if (fluid_channel_get_gen_abs(channel, i))
                //{
                //    gen[i].flags = GEN_ABS_NRPN;
                //}
            //}
        }
        //public fluid_gen_t(int i)
        //{
        //    flags = fluid_gen_flags.GEN_UNUSED;
        //    mod = 0.0;
        //    nrpn = 0.0;
        //    val = fluid_gen_info_t.fluid_gen_info[i].def;
        //}
        public override string ToString()
        {
            return $"Gen {type} flags:{flags} val:{Val} mod:{Mod}";
        }
    }
}
