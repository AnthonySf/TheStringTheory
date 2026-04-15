using System;
using UnityEngine;

namespace MidiPlayerTK
{

    public class fluid_conv
    {

        /*
         Attenuation range in centibels.
         Attenuation range is the dynamic range of the volume envelope generator
         from 0 to the end of attack segment.
         fluidsynth is a 24 bit synth, it could (should??) be 144 dB of attenuation.
         However the spec makes no distinction between 16 or 24 bit synths, so use
         96 dB here.

         Note about usefulness of 24 bits:
         1)Even fluidsynth is a 24 bit synth, this format is only relevant if
         the sample format coming from the soundfont is 24 bits and the audio sample format
         choosen by the application (audio.sample.format) is not 16 bits.

         2)When the sample soundfont is 16 bits, the internal 24 bits number have
         16 bits msb and lsb to 0. Consequently, at the DAC output, the dynamic range of
         this 24 bit sample is reduced to the the dynamic of a 16 bits sample (ie 90 db)
         even if this sample is produced by the audio driver using an audio sample format
         compatible for a 24 bit DAC.

         3)When the audio sample format settings is 16 bits (audio.sample.format), the
         audio driver will make use of a 16 bit DAC, and the dynamic will be reduced to 96 dB
         even if the initial sample comes from a 24 bits soundfont.

         In both cases (2) or (3), the real dynamic range is only 96 dB.

         Other consideration for FLUID_NOISE_FLOOR related to case (1),(2,3):
         - for case (1), FLUID_NOISE_FLOOR should be the noise floor for 24 bits (i.e -138 dB).
         - for case (2) or (3), FLUID_NOISE_FLOOR should be the noise floor for 16 bits (i.e -90 dB).
         */
        static public float FLUID_PEAK_ATTENUATION = 960.0f;

        public const int FLUID_CENTS_HZ_SIZE = 1200;
        public const int FLUID_VEL_CB_SIZE = 128;
        public const int FLUID_CB_AMP_SIZE = 1441; // MPTK 2.11.2 961; // FS 2.3 1441

        // REMOVED WITH MPTK 2.11.3 (related to FS 2.3) public const int FLUID_ATTEN_AMP_SIZE = 1441; 
        public const int FLUID_PAN_SIZE = 1002;

        public const float M_LN10 = 2.3025850929940456840179914546844f; // Mathf.Log(10f);
        public const float M_LN2 = 0.69314718055994530941723212145818f;


        /* EMU 8k/10k don't follow spec in regards to volume attenuation.
         * This factor is used in the equation pow (10.0, cb / FLUID_ATTEN_POWER_FACTOR).
         * By the standard this should be -200.0. */
        /* 07/11/2008 modified by S. Christian Collins for increased velocity sensitivity.  Now it equals the response of EMU10K1 programming.*/
        // REMOVED WITH MPTK 2.11.3 (related to FS 2.3)  public const float FLUID_ATTEN_POWER_FACTOR = -531.509f; // too much issue was -200f; (some notes at low velocity are not played) 	/* v2.9.0 was (-531.509)*/

        /*
         * Look at this source for the generated tab 
         * -----------------------------------------
         * fluidsynth-git\src\gentables\gen_conv.c
         * */

        /* conversion tables */
        // With FS, tables are build from C generated with this source: C:\Devel\fluidsynth-git\src\gentables\gen_conv.c
        // With MPTK 2.11.3 change float to double
        public static double[] fluid_ct2hz_tab = new double[FLUID_CENTS_HZ_SIZE];
        public static double[] fluid_cb2amp_tab = new double[FLUID_CB_AMP_SIZE];

        // REMOVED WITH MPTK 2.11.3 (related to FS 2.3) public static float[] fluid_atten2amp_tab = new float[FLUID_ATTEN_AMP_SIZE];
        // public static float[] fluid_posbp_tab = new float[128];

        public static float[] fluid_concave_tab = new float[128];
        public static float[] fluid_convex_tab = new float[128];
        public static float[] fluid_pan_tab = new float[FLUID_PAN_SIZE];

        /*
         * void fluid_synth_init
         *
         * Does all the initialization for this module.
         */
        public static void fluid_conversion_config()
        {
            int i;
            float x;

            /* FS 2.3 TBD also in fluid_ct2hz_real
               6,875 is just a factor that we already multiply into the lookup table to save
                that multiplication in fluid_ct2hz_real() 
                6.875 Hz because 440Hz / 2^6
                fluid_ct2hz_tab[i] = 6.875L * powl(2.0L, i / 1200.0L);
            */
            for (i = 0; i < FLUID_CENTS_HZ_SIZE; i++)
                fluid_ct2hz_tab[i] = 6.875d * Math.Pow(2d, i / 1200d);

            /* FS 2.3  ok
             * fluid_cb2amp_tab[i] = powl(10.0L, i / -200.0L);
             * centibels to amplitude conversion
             * Note: SF2.01 section 8.1.3: Initial attenuation range is
             * between 0 and 144 dB. Therefore a negative attenuation is
             * not allowed.
             */
            for (i = 0; i < FLUID_CB_AMP_SIZE; i++)
                fluid_cb2amp_tab[i] = Math.Pow(10d, i / -200d);


            /* REMOVED WITH MPTK 2.11.3 (related to FS 2.3) 
             * FS 2.3 fluid_atten2amp_tab seems not yet used
             * NOTE: EMU8k and EMU10k devices don't conform to the SoundFont
             * specification in regards to volume attenuation.  The below calculation
             * is an approx. equation for generating a table equivelant to the
             * cb_to_amp_table[] in tables.c of the TiMidity++ source, which I'm told
             * was generated from device testing.  By the spec this should be centibels.
             
            for (i = 0; i < FLUID_ATTEN_AMP_SIZE; i++)
            {
                fluid_atten2amp_tab[i] = Mathf.Pow(10f, i / FLUID_ATTEN_POWER_FACTOR);
            }
            */

            /* initialize the conversion tables (see fluid_mod.c
               fluid_mod_get_value cases 4 and 8) */

            /* concave unipolar positive transform curve */
            fluid_concave_tab[0] = 0.0f;
            fluid_concave_tab[FLUID_VEL_CB_SIZE - 1] = 1f;

            /* convex unipolar positive transform curve */
            fluid_convex_tab[0] = 0;
            fluid_convex_tab[FLUID_VEL_CB_SIZE - 1] = 1f;
            //x = Mathf.Log10(128f / 127f);

            /* There seems to be an error in the specs. The equations are
               implemented according to the pictures on SF2.01 page 73. */

            for (i = 1; i < FLUID_VEL_CB_SIZE - 1; i++)
            {
                // MPTK 2.11.2
                // float oldx = (float)(-200f / FLUID_PEAK_ATTENUATION) * Mathf.Log((i * i) / (float)((FLUID_VEL_CB_SIZE - 1) * (FLUID_VEL_CB_SIZE - 1))) / Mathf.Log(10f);
                // FS 2.3    x = (-200.0L * 2 / FLUID_PEAK_ATTENUATION) *logl(i / (FLUID_VEL_CB_SIZE - 1.0L)) / M_LN10; 
                // MPTK 2.11.3
                x = (float)(-200f * 2f / FLUID_PEAK_ATTENUATION) * Mathf.Log(i / (float)(FLUID_VEL_CB_SIZE - 1f)) / M_LN10;
                // Debug.Log($"{i} oldx:{oldx} newx:{x}");
                // diff is tiny:
                //   1 oldx:0,8765849  newx:0,8765849
                //  97 oldx:0,04876332 newx:0,04876333
                // 126 oldx:0,00143049 newx:0,001430489

                fluid_convex_tab[i] = 1f - x;
                fluid_concave_tab[(FLUID_VEL_CB_SIZE - 1) - i] = (float)x;
                //Debug.Log($"{i} convex:{fluid_convex_tab[i]} concave:{fluid_concave_tab[i]}");
            }

            /* initialize the pan conversion table */
            /* FS 2.3 ok with MPTK
            x = M_PI / 2.0L / (FLUID_PAN_SIZE - 1.0L);
            for (i = 0; i < FLUID_PAN_SIZE; i++)
            {
                fluid_pan_tab[i] = sinl(i * x);
            }
            */
            x = (float)Math.PI / 2f / (FLUID_PAN_SIZE - 1.0f);
            for (i = 0; i < FLUID_PAN_SIZE; i++)
                fluid_pan_tab[i] = (float)Math.Sin(i * x);
        }

        /*
        * Converts absolute cents to Hertz
        * 
        * As per sfspec section 9.3:
        * 
        * ABSOLUTE CENTS - An absolute logarithmic measure of frequency based on a
        * reference of MIDI key number scaled by 100.
        * A cent is 1/1200 of an octave [which is the twelve hundredth root of two],
        * and value 6900 is 440 Hz (A-440).
        * 
        * Implemented below basically is the following:
        *   440 * 2^((cents-6900)/1200)
        * = 440 * 2^((int)((cents-6900)/1200)) * 2^(((int)cents-6900)%1200))
        * = 2^((int)((cents-6900)/1200)) * (440 * 2^(((int)cents-6900)%1200)))
        *                                  ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
        *                           This second factor is stored in the lookup table.
        *
        * The first factor can be implemented with a fast shift when the exponent
        * is always an int. This is the case when using 440/2^6 Hz rather than 440Hz
        * reference.
        */
        private const int mask_hz= 4 * 8 - 1; // 31 with sizeof(int)=4
        public static double fluid_ct2hz_real(double cents)
        {
            if (cents < 0)
            {
                return fluid_act2hz(cents);
            }
            else
            {
                int mult, fac, rem;
                int icents = (int)cents;
                icents += 300;

                // don't use stdlib div() here, it turned out have poor performance
                fac = icents / 1200;
                rem = icents % 1200;

                // Think of "mult" as the factor that we multiply (440/2^6)Hz with,
                // or in other words mult is the "first factor" of the above
                // functions comment.
                //
                // Assuming sizeof(uint)==4 this will give us a maximum range of
                // 32 * 1200cents - 300cents == 38100 cents == 29,527,900,160 Hz
                // which is much more than ever needed. For bigger values, just
                // safely wrap around (the & is just a replacement for the quick
                // modulo operation % 32).
                mult = 1 << (fac & mask_hz);
                //Debug.Log($" *** fluid_act2hz  int cents:{cents} mult:{mult}");
                // don't use ldexp() either (poor performance)
                return mult * fluid_ct2hz_tab[rem];
            }
        }

        public static double fluid_ct2hz(double cents)
        {
            /* Filter fc limit: SF2.01 page 48 # 8 */
            if (cents >= 13500d)
                cents = 13500d;             /* 20 kHz */
            else if (cents < 1500d)
                cents = 1500d;              /* 20 Hz */
            return fluid_ct2hz_real(cents);
        }

        /*
         * fluid_cb2amp
         *
         * in: a value between 0 and 1440, 0 is no attenuation
         * out: a value between 1 and 0
         */
        public static double fluid_cb2amp(double cb)
        {
            /*
             * cb: an attenuation in 'centibels' (1/10 dB)
             * SF2.01 page 49 # 48 limits it to 144 dB.
             * 96 dB is reasonable for 16 bit systems, 144 would make sense for 24 bit.
             */
            int icb = (int)cb;
            /* minimum attenuation: 0 dB */
            if (icb < 0) return 1d;
            if (icb >= FLUID_CB_AMP_SIZE) return 0d;
            return fluid_cb2amp_tab[icb];
        }

        /*
         * fluid_atten2amp REMOVED WITH MPTK 2.11.3 (related to FS 2.3)
         *
         * in: a value between 0 and 1440, 0 is no attenuation
         * out: a value between 1 and 0
         *
         * Note: Volume attenuation is supposed to be centibels but EMU8k/10k don't
         * follow this.  Thats the reason for separate fluid_cb2amp and fluid_atten2amp.
        public static float fluid_atten2amp(float atten)
        {
            if (atten < 0) return 1f;
            else if (atten >= FLUID_ATTEN_AMP_SIZE) return 0f;
            else return fluid_atten2amp_tab[(int)atten];
        }
         */

        public static double fluid_tc2sec_delay(double tc)
        {
            /* SF2.01 section 8.1.2 items 21, 23, 25, 33
             * SF2.01 section 8.1.3 items 21, 23, 25, 33
             * The most negative number indicates a delay of 0. Range is limited
             * from -12000 to 5000 */
            if (tc <= -32768d) return 0d;
            if (tc < -12000d) tc = -12000d;
            if (tc > 5000d) tc = 5000d;
            return Math.Pow(2d, tc / 1200d);
        }

        public static double fluid_tc2sec_attack(double tc)
        {
            /* SF2.01 section 8.1.2 items 26, 34
             * SF2.01 section 8.1.3 items 26, 34
             * The most negative number indicates a delay of 0
             * Range is limited from -12000 to 8000 */
            if (tc <= -32768d) return 0d;
            if (tc < -12000d) tc = -12000d;
            if (tc > 8000d) tc = 8000d;
            return Math.Pow(2d, tc / 1200d);
        }

        //public static float fluid_tc2msec(float tc)
        //{
        //   return = Math.Pow(2d, tc / 1200d) * 1000d;
        //}

        public static double fluid_tc2sec_release(double tc)
        {
            /* SF2.01 section 8.1.2 items 30, 38
             * SF2.01 section 8.1.3 items 30, 38
             * No 'most negative number' rule here!
             * Range is limited from -12000 to 8000 */
            if (tc <= -32768d) return 0d;
            if (tc < -12000d) tc = -12000d;
            if (tc > 8000d) tc = 8000d;
            return Math.Pow(2d, tc / 1200d);
        }


        public static double fluid_act2hz(double c)
        {
            //return 8.176d * Math.Pow(2d, c / 1200d);
            // do not use FLUID_POW, otherwise the unit tests will fail when compiled in single precision
            return 8.1757989156437073336828122976032719176391831357d * Math.Pow(2d, c / 1200d);

        }

        /*
         * fluid_hz2ct
         *
         * Convert from Hertz to cents
         */
        // Removed with FS 2.3
        // public static float fluid_hz2ct(float f)
        // {
        //     return 6900f + 1200f * Mathf.Log10(f / 440f) / M_LN2;
        // }

        public static float fluid_pan(float c, bool left)
        {
            if (left) c = -c;

            if (c <= -500f)
                return 0f;
            else if (c >= 500f)
                return 1f;
            else
                return fluid_pan_tab[(int)(c + 500f)];
        }

        /*
         * Return the amount of attenuation based on the balance for the specified
         * channel. If balance is negative (turned toward left channel, only the right
         * channel is attenuated. If balance is positive, only the left channel is
         * attenuated.
         *
         * @params balance left/right balance, range [-960;960] in absolute centibels
         * @return amount of attenuation [0.0;1.0]
         */
        //public static float fluid_balance(float balance, bool left)
        //{
        //    /* This is the most common case */
        //    if (balance == 0)
        //    {
        //        return 1.0f;
        //    }

        //    if ((left && balance < 0) || (!left && balance > 0))
        //    {
        //        return 1.0f;
        //    }

        //    if (balance < 0)
        //    {
        //        balance = -balance;
        //    }

        //    return fluid_cb2amp(balance);
        //}

        /*
         * fluid_concave
         */
        public static float fluid_concave(float val)
        {
            int ival = (int)val;
            if (val < 0f)
                return 0f;
            else if (ival >= FLUID_VEL_CB_SIZE - 1)
                return 1f;
            return fluid_concave_tab[ival] + (fluid_concave_tab[ival + 1] - fluid_concave_tab[ival]) * (val - ival);

            //return fluid_concave_tab[(int)val];
        }

        public static float fluid_convex(float val)
        {
            int ival = (int)val;
            if (val < 0f)
                return 0f;
            else if (ival >= FLUID_VEL_CB_SIZE - 1)
                return fluid_convex_tab[FLUID_VEL_CB_SIZE - 1];
            //return fluid_convex_tab[(int)val];
            return fluid_convex_tab[ival] + (fluid_convex_tab[ival + 1] - fluid_convex_tab[ival]) * (val - ival);
        }
    }
}
