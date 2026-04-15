using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MidiPlayerTK
{

    /* Flags telling the polarity of a modulator.  Compare with SF2.01
       section 8.2. Note: The numbers of the bits are different!  (for
       example: in the flags of a SF modulator, the polarity bit is bit
       nr. 9) */
    public enum fluid_mod_flags : byte
    {
        FLUID_MOD_POSITIVE = 0,
        FLUID_MOD_NEGATIVE = 1,
        FLUID_MOD_UNIPOLAR = 0,
        FLUID_MOD_BIPOLAR = 2,
        FLUID_MOD_LINEAR = 0,
        FLUID_MOD_CONCAVE = 4,
        FLUID_MOD_CONVEX = 8,
        FLUID_MOD_SWITCH = 12,
        FLUID_MOD_GC = 0,
        FLUID_MOD_CC = 16,
        FLUID_MOD_SIN = 0x80,            /**< Custom non-standard sinus mapping function */
    }

    /* Flags telling the source of a modulator.  This corresponds to
     * SF2.01 section 8.2.1 */
    public enum fluid_mod_src : byte
    {
        FLUID_MOD_NONE = 0,
        FLUID_MOD_VELOCITY = 2,
        FLUID_MOD_KEY = 3,
        FLUID_MOD_KEYPRESSURE = 10,
        FLUID_MOD_CHANNELPRESSURE = 13,
        FLUID_MOD_PITCHWHEEL = 14,
        FLUID_MOD_PITCHWHEELSENS = 16
    }

    /// <summary>@brief
    /// Defined Modulator from fluid_mod_t
    /// </summary>
    public class HiMod
    {
        /* Maximum number of modulators in a voice */
        static public int FLUID_NUM_MOD = 64;

        public byte dest;
        public byte src1;
        public byte flags1;
        public byte src2;
        public byte flags2;
        public float amount;

        // Modulator structure read from SF
        public ushort SfSrc;        /* source modulator */
        public ushort SfAmtSrc;     /* second source controls amnt of first */
        public ushort SfTrans;      /* transform applied to source */


        /*
         * retrieves the initial value from the given source of the modulator
         */
        static float fluid_mod_get_source_value(byte mod_src, byte mod_flags, ref float range, fluid_voice voice)
        {
            MPTKChannel chan = voice.channel;
            float val;

            if ((mod_flags & (byte)fluid_mod_flags.FLUID_MOD_CC) != 0)
            {
                //val = fluid_channel_get_cc(chan, mod_src);
                val = chan.cc[mod_src];

                if (mod_src == (byte)MPTKController.PORTAMENTO_CTRL)
                {
                    // an invalid portamento fromkey should be treated as 0 when it's actually used for moulating
                    /* Macros interface to monophonic list variables 
                    #define INVALID_NOTE (255)
                    // Returns true when a note is a valid note 
                    #define fluid_channel_is_valid_note(n)    (n != INVALID_NOTE)*/
                    if (val == 255)
                    {
                        val = 0;
                    }
                }
            }
            else
            {
                switch (mod_src)
                {
                    case (byte)fluid_mod_src.FLUID_MOD_NONE:         /* SF 2.01 8.2.1 item 0: src enum=0 => value is 1 */
                        val = range;
                        break;

                    case (byte)fluid_mod_src.FLUID_MOD_VELOCITY:
                        val = voice.fluid_voice_get_actual_velocity();
                        break;

                    case (byte)fluid_mod_src.FLUID_MOD_KEY:
                        val = voice.fluid_voice_get_actual_key();
                        break;

                    case (byte)fluid_mod_src.FLUID_MOD_KEYPRESSURE:
                        //val = fluid_channel_get_key_pressure(chan, voice.key);
                        // Not processed by nAudio, so also not processed by Maestro Synth.
                        val = chan.key_pressure[voice.key];
                        break;

                    case (byte)fluid_mod_src.FLUID_MOD_CHANNELPRESSURE:
                        val = chan.channel_pressure;
                        break;

                    case (byte)fluid_mod_src.FLUID_MOD_PITCHWHEEL:
                        val = chan.pitch_bend;
                        range = 0x4000;
                        break;

                    case (byte)fluid_mod_src.FLUID_MOD_PITCHWHEELSENS:
                        val = chan.pitch_wheel_sensitivity;
                        break;

                    default:
                        Debug.Log($"Unknown modulator source '{mod_src}', disabling modulator.");
                        val = 0f;
                        break;
                }
            }

            return val;
        }

        /**
         * transforms the initial value retrieved by \c fluid_mod_get_source_value into [0.0;1.0]
         */
        static float fluid_mod_transform_source_value(float val, byte mod_flags, float range)
        {
            /* normalized value, i.e. usually in the range [0;1] */
            float val_norm = val / range;

            /* we could also only switch case the lower nibble of mod_flags, however
             * this would keep us from adding further mod types in the future
             *
             * instead just remove the flag(s) we already took care of
             */
            sbyte inv = ~(sbyte)fluid_mod_flags.FLUID_MOD_CC;
            mod_flags &= (byte)inv;

            switch (mod_flags/* & 0x0f*/)
            {
                case (byte)fluid_mod_flags.FLUID_MOD_LINEAR | (byte)fluid_mod_flags.FLUID_MOD_UNIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_POSITIVE: /* =0 */
                    val = val_norm;
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_LINEAR | (byte)fluid_mod_flags.FLUID_MOD_UNIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_NEGATIVE: /* =1 */
                    val = 1.0f - val_norm;
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_LINEAR | (byte)fluid_mod_flags.FLUID_MOD_BIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_POSITIVE: /* =2 */
                    val = -1.0f + 2.0f * val_norm;
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_LINEAR | (byte)fluid_mod_flags.FLUID_MOD_BIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_NEGATIVE: /* =3 */
                    val = 1.0f - 2.0f * val_norm;
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_CONCAVE | (byte)fluid_mod_flags.FLUID_MOD_UNIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_POSITIVE: /* =4 */
                    val = fluid_conv.fluid_concave(127 * (val_norm));
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_CONCAVE | (byte)fluid_mod_flags.FLUID_MOD_UNIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_NEGATIVE: /* =5 */
                    val = fluid_conv.fluid_concave(127 * (1.0f - val_norm));
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_CONCAVE | (byte)fluid_mod_flags.FLUID_MOD_BIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_POSITIVE: /* =6 */
                    val = (val_norm > 0.5f) ? fluid_conv.fluid_concave(127 * 2 * (val_norm - 0.5f))
                          : -fluid_conv.fluid_concave(127 * 2 * (0.5f - val_norm));
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_CONCAVE | (byte)fluid_mod_flags.FLUID_MOD_BIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_NEGATIVE: /* =7 */
                    val = (val_norm > 0.5f) ? -fluid_conv.fluid_concave(127 * 2 * (val_norm - 0.5f))
                          : fluid_conv.fluid_concave(127 * 2 * (0.5f - val_norm));
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_CONVEX | (byte)fluid_mod_flags.FLUID_MOD_UNIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_POSITIVE: /* =8 */
                    val = fluid_conv.fluid_convex(127 * (val_norm));
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_CONVEX | (byte)fluid_mod_flags.FLUID_MOD_UNIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_NEGATIVE: /* =9 */
                    val = fluid_conv.fluid_convex(127 * (1.0f - val_norm));
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_CONVEX | (byte)fluid_mod_flags.FLUID_MOD_BIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_POSITIVE: /* =10 */
                    val = (val_norm > 0.5f) ? fluid_conv.fluid_convex(127 * 2 * (val_norm - 0.5f))
                          : -fluid_conv.fluid_convex(127 * 2 * (0.5f - val_norm));
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_CONVEX | (byte)fluid_mod_flags.FLUID_MOD_BIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_NEGATIVE: /* =11 */
                    val = (val_norm > 0.5f) ? -fluid_conv.fluid_convex(127 * 2 * (val_norm - 0.5f))
                          : fluid_conv.fluid_convex(127 * 2 * (0.5f - val_norm));
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_SWITCH | (byte)fluid_mod_flags.FLUID_MOD_UNIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_POSITIVE: /* =12 */
                    val = (val_norm >= 0.5f) ? 1.0f : 0.0f;
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_SWITCH | (byte)fluid_mod_flags.FLUID_MOD_UNIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_NEGATIVE: /* =13 */
                    val = (val_norm >= 0.5f) ? 0.0f : 1.0f;
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_SWITCH | (byte)fluid_mod_flags.FLUID_MOD_BIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_POSITIVE: /* =14 */
                    val = (val_norm >= 0.5f) ? 1.0f : -1.0f;
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_SWITCH | (byte)fluid_mod_flags.FLUID_MOD_BIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_NEGATIVE: /* =15 */
                    val = (val_norm >= 0.5f) ? -1.0f : 1.0f;
                    break;

                /*
                 * MIDI CCs only have a resolution of 7 bits. The closer val_norm gets to 1,
                 * the less will be the resulting change of the sinus. When using this sin()
                 * for scaling the cutoff frequency, there will be no audible difference between
                 * MIDI CCs 118 to 127. To avoid this waste of CCs multiply with 0.87
                 * (at least for unipolar) which makes sin() never get to 1.0 but to 0.98 which
                 * is close enough.
                 */
                case (byte)fluid_mod_flags.FLUID_MOD_SIN | (byte)fluid_mod_flags.FLUID_MOD_UNIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_POSITIVE: /* custom sin(x) */
                    val = Mathf.Sin((Mathf.PI / 2.0f * 0.87f) * val_norm);
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_SIN | (byte)fluid_mod_flags.FLUID_MOD_UNIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_NEGATIVE: /* custom */
                    val = Mathf.Sin((Mathf.PI / 2.0f * 0.87f) * (1.0f - val_norm));
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_SIN | (byte)fluid_mod_flags.FLUID_MOD_BIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_POSITIVE: /* custom */
                    val = (val_norm > 0.5f) ? Mathf.Sin(Mathf.PI * (val_norm - 0.5f)) :
                        -Mathf.Sin(Mathf.PI * (0.5f - val_norm));
                    break;

                case (byte)fluid_mod_flags.FLUID_MOD_SIN | (byte)fluid_mod_flags.FLUID_MOD_BIPOLAR | (byte)fluid_mod_flags.FLUID_MOD_NEGATIVE: /* custom */
                    val = (val_norm > 0.5f) ? -Mathf.Sin(Mathf.PI * (val_norm - 0.5f))
                          : Mathf.Sin(Mathf.PI * (0.5f - val_norm));
                    break;

                default:
                    Debug.LogWarning($"Unknown modulator type '{mod_flags}', disabling modulator.");
                    val = 0f;
                    break;
            }

            return val;
        }


        /*
         * fluid_mod_get_value.
         * Computes and return modulator output following SF2.01
         * (See SoundFont Modulator Controller Model Chapter 9.5).
         *
         * Output = Transform(Amount * Map(primary source input) * Map(secondary source input))
         *
         * Notes:
         * 1)fluid_mod_get_value, ignores the Transform operator. The result is:
         *
         *   Output = Amount * Map(primary source input) * Map(secondary source input)
         *
         * 2)When primary source input (src1) is set to General Controller 'No Controller',
         *   output is forced to 0.
         *
         * 3)When secondary source input (src2) is set to General Controller 'No Controller',
         *   output is forced to +1.0 
         */
        public float fluid_mod_get_value(fluid_voice voice)
        {
            float v1 = 0.0f, v2 = 1.0f;
            /* The wording of the default modulators refers to a range of 127/128.
             * And the table in section 9.5.3 suggests, that this mapping should be applied
             * to all unipolar and bipolar mappings respectively.
             *
             * Thinking about this further, this is actually pretty clever, as this is properly
             * addresses MIDI Recommended Practice (RP-036) Default Pan Formula
             * "Since MIDI controller values range from 0 to 127, the exact center
             * of the range, 63.5, cannot be represented."
             *
             * When changing the overall range to 127/128 however, the "middle pan" value of 64
             * can be correctly represented.
             */
            float range1 = 128.0f, range2 = 128.0f;

            /* 'special treatment' for default controller
             *
             *  Reference: SF2.01 section 8.4.2
             *
             * The GM default controller 'vel-to-filter cut off' is not clearly defined: If implemented according to the specs, the filter
             * frequency jumps between vel=63 and vel=64.  To maintain compatibility with existing sound fonts, the implementation is
             * 'hardcoded', it is impossible to implement using only one modulator otherwise.
             *
             * I assume here, that the 'intention' of the paragraph is one octave (1200 cents) filter frequency shift between vel=127 and
             * vel=64.  'amount' is (-2400), at least as long as the controller is set to default.
             *
             * Further, the 'appearance' of the modulator (source enumerator, destination enumerator, flags etc) is different from that
             * described in section 8.4.2, but it matches the definition used in several SF2.1 sound fonts (where it is used only to turn it off).
             * */
            if (fluid_mod_test_identity(MidiSynth.default_vel2filter_mod))
            {
                // S. Christian Collins' mod, to stop forcing velocity based filtering
                /*
                    if (voice.vel < 64){
                      return (float) mod.amount / 2.0;
                    } else {
                      return (float) mod.amount * (127 - voice.vel) / 127;
                    }
                */
                return 0; // (float) mod.amount / 2.0;
            }
            // end S. Christian Collins' mod

            /* get the initial value of the first source */
            if (src1 > 0)
            {
                v1 = fluid_mod_get_source_value(src1, flags1, ref range1, voice);

                /* transform the input value */
                v1 = fluid_mod_transform_source_value(v1, flags1, range1);
            }
            /* When primary source input (src1) is set to General Controller 'No Controller',
               output is forced to 0.0
            */
            else
            {
                return 0f;
            }

            /* no need to go further */
            if (v1 == 0f)
            {
                return 0f;
            }

            /* get the second input source */
            if (src2 > 0)
            {
                v2 = fluid_mod_get_source_value(src2, flags2, ref range2, voice);

                /* transform the second input value */
                v2 = fluid_mod_transform_source_value(v2, flags2, range2);
            }
            /* When secondary source input (src2) is set to General Controller 'No Controller',
               output is forced to +1.0
            */
            else
            {
                v2 = 1f;
            }

            /* it's as simple as that: */
            return (float)amount * v1 * v2;
        }


        /**
         * Checks if modulators source other than CC source is invalid.
         *
         * @param mod, modulator.
         * @param src1_select, source input selection to check.
         *   1 to check src1 source.
         *   0 to check src2 source.
         * @return FALSE if selected modulator source other than cc is invalid, TRUE otherwise.
         *
         * (specs SF 2.01  7.4, 7.8, 8.2.1)
         */
        bool fluid_mod_check_non_cc_source(int src1_select)
        {
            byte flags, src;

            if (src1_select == 1)
            {
                flags = flags1;
                src = src1;
            }
            else
            {
                flags = flags2;
                src = src2;
            }

            return (((flags & (byte)fluid_mod_flags.FLUID_MOD_CC) != 0)  /* src is a CC */
                   /* SF2.01 section 8.2.1: Constant value */
                   || ((src == (byte)fluid_mod_src.FLUID_MOD_NONE)
                       || (src == (byte)fluid_mod_src.FLUID_MOD_VELOCITY)        /* Note-on velocity */
                       || (src == (byte)fluid_mod_src.FLUID_MOD_KEY)             /* Note-on key number */
                       || (src == (byte)fluid_mod_src.FLUID_MOD_KEYPRESSURE)     /* Poly pressure */
                       || (src == (byte)fluid_mod_src.FLUID_MOD_CHANNELPRESSURE) /* Channel pressure */
                       || (src == (byte)fluid_mod_src.FLUID_MOD_PITCHWHEEL)      /* Pitch wheel */
                       || (src == (byte)fluid_mod_src.FLUID_MOD_PITCHWHEELSENS)  /* Pitch wheel sensitivity */
                      ));
        }

        /**
         * Checks if modulator CC source is invalid (specs SF 2.01  7.4, 7.8, 8.2.1).
         *
         * @param mod, modulator.
         * @src1_select, source input selection:
         *   1 to check src1 source or
         *   0 to check src2 source.
         * @return FALSE if selected modulator's source CC is invalid, TRUE otherwise.
         */
        bool fluid_mod_check_cc_source(int src1_select)
        {
            byte flags, src;

            if (src1_select == 1)
            {
                flags = flags1;
                src = src1;
            }
            else
            {
                flags = flags2;
                src = src2;
            }

            return (((flags & (byte)fluid_mod_flags.FLUID_MOD_CC) == 0)  /* src is non CC */
                   || ((src != (byte)MPTKController.BankSelectMsb)
                       && (src != (byte)MPTKController.BankSelectLsb)
                       && (src != (byte)MPTKController.DATA_ENTRY_MSB)
                       && (src != (byte)MPTKController.DATA_ENTRY_LSB)
                       /* is src not NRPN_LSB, NRPN_MSB, RPN_LSB, RPN_MSB */
                       && ((src < (byte)MPTKController.NRPN_LSB) || ((byte)MPTKController.RPN_MSB < src))
                       /* is src not ALL_SOUND_OFF, ALL_CTRL_OFF, LOCAL_CONTROL, ALL_NOTES_OFF ? */
                       /* is src not OMNI_OFF, OMNI_ON, POLY_OFF, POLY_ON ? */
                       && (src < (byte)MPTKController.AllSoundOff)
                      /* CC lsb shouldn't allowed to modulate (spec SF 2.01 - 8.2.1)
                         However, as long fluidsynth will use only CC 7 bits resolution,
                         it is safe to ignore these SF recommendations on CC receive.
                         See explanations in fluid_synth_cc_LOCAL() */
                      /* uncomment next line to forbid CC lsb  */
                      /* && ((src < 32) || (63 < src)) */
                      ));
        }

        /**
         * Checks valid modulator sources (specs SF 2.01  7.4, 7.8, 8.2.1)
         *
         * @param mod, modulator.
         * @param name,if not NULL, pointer on a string displayed as a warning.
         * @return TRUE if modulator sources src1, src2 are valid, FALSE otherwise.
         */
       public bool fluid_mod_check_sources(string name)
        {
            /* checks valid non cc sources */
            if (!fluid_mod_check_non_cc_source(1)) /* check src1 */
            {
                Debug.LogWarning($"Invalid modulator, using non-CC source {name}.src1={src1}");
                return false;
            }

            /*
              When src1 is non CC source FLUID_MOD_NONE, the modulator is valid but
              the output of this modulator will be forced to 0 at synthesis time.
              Also this modulator cannot be used to overwrite a default modulator (as
              there is no default modulator with src1 source equal to FLUID_MOD_NONE).
              Consequently it is useful to return FALSE to indicate this modulator
              being useless. It will be removed later with others invalid modulators.
            */

            //if (fluid_mod_is_src1_none
            if (((flags1 & (byte)fluid_mod_flags.FLUID_MOD_CC) == 0) && (src1 == (byte)fluid_mod_src.FLUID_MOD_NONE))
            {
                Debug.LogWarning($"Modulator with source 1 none {name}.src1={src1}");
                return false;
            }

            if (!fluid_mod_check_non_cc_source(0)) /* check src2 */
            {
                Debug.LogWarning($"Invalid modulator, using non-CC source {name}.src2={src2}");
                return false;
            }

            /* checks valid cc sources */
            if (!fluid_mod_check_cc_source(1)) /* check src1 */
            {
                Debug.LogWarning($"Invalid modulator, using CC source {name}.src1={src1}");
                return false;
            }

            if (!fluid_mod_check_cc_source(0)) /* check src2 */
            {
                Debug.LogWarning($"Invalid modulator, using CC source {name}.src2={src2}");
                return false;
            }

            return true;
        }

        /**
         * Checks if two modulators are identical in sources, flags and destination.
         *
         * @param mod1 First modulator
         * @param mod2 Second modulator
         * @return TRUE if identical, FALSE otherwise
         *
         * SF2.01 section 9.5.1 page 69, 'bullet' 3 defines 'identical'.
         */

        public bool fluid_mod_test_identity(HiMod mod2)
        {
            return dest == mod2.dest
                   && src1 == mod2.src1
                   && src2 == mod2.src2
                   && flags1 == mod2.flags1
                   && flags2 == mod2.flags2;
        }

        public override string ToString()
        {
            return string.Format("Mod amount:{0} src1:{1} flags1:{2} src2:{3} flags2:{4} dest:{5}", this.amount, this.src1, this.flags1, this.src2, this.flags2, (fluid_gen_type)this.dest);
        }

        static public void DebugLog(string info, List<HiMod> mods)
        {
            foreach (HiMod mod in mods)
                Debug.Log(info + mod.ToString());
        }
        public void fluid_dump_modulator(float valMod)
        {
            //int src1 = mod.src1;
            //int dest = mod.dest;
            //int src2 = mod.src2;
            //int flags1 = mod.flags1;
            //int flags2 = mod.flags2;
            //float amount = (float)mod.amount;
            StringBuilder dump = new StringBuilder();

            dump.Append("Src: ");

            if ((flags1 & ((byte)fluid_mod_flags.FLUID_MOD_CC)) != 0)
            {
                dump.Append("MIDI CC=");
                dump.Append(src1.ToString("d3"));
            }
            else
            {
                switch (src1)
                {
                    case (byte)fluid_mod_src.FLUID_MOD_NONE:
                        dump.Append("None       ");
                        break;

                    case (byte)fluid_mod_src.FLUID_MOD_VELOCITY:
                        dump.Append("note-on vel");
                        break;

                    case (byte)fluid_mod_src.FLUID_MOD_KEY:
                        dump.Append("Key mod key");
                        break;

                    case (byte)fluid_mod_src.FLUID_MOD_KEYPRESSURE:
                        dump.Append("Poly press ");
                        break;

                    case (byte)fluid_mod_src.FLUID_MOD_CHANNELPRESSURE:
                        dump.Append("Chan press ");
                        break;

                    case (byte)fluid_mod_src.FLUID_MOD_PITCHWHEEL:
                        dump.Append("Pitch Wheel");
                        break;

                    case (byte)fluid_mod_src.FLUID_MOD_PITCHWHEELSENS:
                        dump.Append("Pitch Wheel sens");
                        break;

                    default:
                        dump.Append("unknown:");
                        dump.Append(src1.ToString());
                        break;
                } /* switch src1 */
            } /* if not CC */

            if ((flags1 & ((byte)fluid_mod_flags.FLUID_MOD_NEGATIVE)) != 0)
            {
                dump.Append(" - ");
            }
            else
            {
                dump.Append(" + ");
            }

            if ((flags1 & ((byte)fluid_mod_flags.FLUID_MOD_BIPOLAR)) != 0)
            {
                dump.Append("bipolar  ");
            }
            else
            {
                dump.Append("unipolar ");
            };

            dump.Append(" . ");

            switch (dest)
            {
                case (byte)fluid_gen_type.GEN_FILTERQ:
                    dump.Append("GEN_FILTERQ       ");
                    break;

                case (byte)fluid_gen_type.GEN_FILTERFC:
                    dump.Append("GEN_FILTERFC      ");
                    break;

                case (byte)fluid_gen_type.GEN_CUSTOM_FILTERQ: // Not used with MPTK
                    dump.Append("GEN_CUSTOM_FILTERQ");
                    break;

                case (byte)fluid_gen_type.GEN_CUSTOM_FILTERFC: // Not used with MPTK
                    dump.Append("GEN_CUSTOM_FILTERFC");
                    break;

                case (byte)fluid_gen_type.GEN_VIBLFOTOPITCH:
                    dump.Append("GEN_VIBLFOTOPITCH ");
                    break;

                case (byte)fluid_gen_type.GEN_MODENVTOPITCH:
                    dump.Append("GEN_MODENVTOPITCH ");
                    break;

                case (byte)fluid_gen_type.GEN_MODLFOTOPITCH:
                    dump.Append("GEN_MODLFOTOPITCH ");
                    break;

                case (byte)fluid_gen_type.GEN_CHORUSSEND:
                    dump.Append("GEN_CHORUSSEND    ");
                    break;

                case (byte)fluid_gen_type.GEN_REVERBSEND:
                    dump.Append("GEN_REVERBSEND    ");
                    break;

                case (byte)fluid_gen_type.GEN_PAN:
                    dump.Append("GEN_PAN           ");
                    break;

                case (byte)fluid_gen_type.GEN_CUSTOM_BALANCE: // Not used with MPTK
                    dump.Append("GEN_CUSTOM_BALANCE");
                    break;

                case (byte)fluid_gen_type.GEN_ATTENUATION:
                    dump.Append("GEN_ATTENUATION   ");
                    break;

                default:
                    dump.Append($"{((fluid_gen_type)dest),-13} ({dest})");
                    break;
            }; /* switch dest */

            dump.Append($" amount:{amount,-10:#######.##} flags:{flags1,-2:##} src2:{src2} flags2:{flags2,-2:##} incGenerator:{valMod,-10:#######.##}\n");
            Debug.Log(dump);
        }
    }
}
