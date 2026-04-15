/* FluidSynth - A Software Synthesizer
/* FluidSynth - A Software Synthesizer
 *
 * Copyright (C) 2003  Peter Hanappe, Markus Nentwig and others.
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public License
 * as published by the Free Software Foundation; either version 2.1 of
 * the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free
 * Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
 * 02110-1301, USA
 */

/*
  based on a chorus implementation made by Juergen Mueller And Sundry Contributors in 1998

  CHANGES

  - Adapted for fluidsynth, Peter Hanappe, March 2002

  - Variable delay line implementation using bandlimited
    interpolation, code reorganization: Markus Nentwig May 2002

  - Complete rewrite using lfo computed on the fly, first order all-pass
    interpolator and adding stereo unit: Jean-Jacques Ceresa, Jul 2019

  - Adapted for Unity, Thierry Bachmann, March 2020
 */


/*
 * 	Chorus effect.
 *
 * Flow diagram scheme for n delays ( 1 <= n <= MAX_CHORUS ):
 *
 *                                                       ________
 *                  direct signal (not implemented) >-. |        |
 *                 _________                            |        |
 * mono           |         |                           |        |
 * input ---+---. | delay 1 |-------------------------. | Stereo |--. right
 *          |     |_________|                           |        |     output
 *          |        /|\                                | Unit   |
 *          :         |                                 |        |
 *          : +-----------------+                       |(width) |
 *          : | Delay control 1 |<-+                    |        |
 *          : +-----------------+  |                    |        |--. left
 *          |      _________       |                    |        |     output
 *          |     |         |      |                    |        |
 *          +---. | delay n |-------------------------. |        |
 *                |_________|      |                    |        |
 *                   /|\           |                    |________|
 *                    |            |  +--------------+      /|\
 *            +-----------------+  |  |mod depth (ms)|       |
 *            | Delay control n |<-*--|lfo speed (Hz)|     gain-out
 *            +-----------------+     +--------------+
 *
 *
 * The delay i is controlled by a sine or triangle modulation i ( 1 <= i <= n).
 *
 * The chorus unit process a monophonic input signal and produces stereo output
 * controlled by WIDTH macro.
 * Actually WIDTH is fixed to maximum value. But in the future, we could add a
 * setting (e.g "synth.width") allowing the user to get a gradually stereo
 * effect from minimum (monophonic) to maximum stereo effect.
 *
 * Delays lines are implemented using only one line for all chorus blocks.
 * Each chorus block has it own lfo (sinus/triangle). Each lfo are out of phase
 * to produce uncorrelated signal at the output of the delay line (this simulates
 * the presence of individual line for each block). Each lfo modulates the length
 * of the line using a depth modulation value and lfo frequency value common to
 * all lfos.
 *
 * LFO modulators are computed on the fly, instead of using lfo lookup table.
 * The advantages are:
 * - Avoiding a lost of 608272 memory bytes when lfo speed is low (0.3Hz).
 * - Allows to diminish the lfo speed lower limit to 0.1Hz instead of 0.3Hz.
 *   A speed of 0.1 is interesting for  Using a lookuptable for 0.1Hz
 *   would require too much memory (1824816 bytes).
 * - Interpolation make use of first order all-pass interpolator instead of
 *   bandlimited interpolation.
 * - Although lfo modulator is computed on the fly, cpu load is lower than
 *   using lfo lookup table with bandlimited interpolator.
 */
using UnityEngine;

namespace MidiPlayerTK
{
    /*-----------------------------------------------------------------------------
     Sinusoidal modulator
    -----------------------------------------------------------------------------*/
    /* modulator */
    public class sinus_modulator
    {
        public float a1;          /* Coefficient: a1 = 2 * cos(w) */
        public float buffer1;     /* buffer1 */
        public float buffer2;     /* buffer2 */
        public float reset_buffer2;/* reset value of buffer2 */
    }

    /*-----------------------------------------------------------------------------
     Triangle modulator
    -----------------------------------------------------------------------------*/
    public class triang_modulator
    {
        public float freq;       /* Osc. Frequency (in Hertz) */
        public float val;         /* internal current value */
        public float inc;         /* increment value */
    }

    /*-----------------------------------------------------------------------------
     modulator
    -----------------------------------------------------------------------------*/
    public class modulator
    {
        public int line_out; /* current line out position for this modulator */
        public sinus_modulator sinus; /* sinus lfo */
        public triang_modulator triang; /* triangle lfo */
                                        /* first order All-Pass interpolator members */
        public float frac_pos_mod; /* fractional position part between samples */
                                   /* previous value used when interpolating using fractional */
        public float buffer;

        public modulator()
        {
            sinus = new sinus_modulator();
            triang = new triang_modulator();
        }
    }

    /* Private data for SKEL file */
    public class fluid_chorus
    {

        /*-------------------------------------------------------------------------------------
     Private
   --------------------------------------------------------------------------------------*/
        // #define DEBUG_PRINT // allows message to be printed on the console.

        const int MAX_CHORUS = 99;  /* number maximum of block */
        const int MAX_LEVEL = 10; /* max output level */
        const float MIN_SPEED_HZ = 0.1f; /* min lfo frequency (Hz) */
        const int MAX_SPEED_HZ = 5; /* max lfo frequency (Hz) */

        /* WIDTH [0..10] value define a stereo separation between left and right.
         When 0, the output is monophonic. When > 0 , the output is stereophonic.
         Actually WIDTH is fixed to maximum value. But in the future we could add a setting to
         allow a gradually stereo effect from minimum (monophonic) to maximum stereo effect.
        */
        //const int WIDTH = 10; now a setting in MPTK

        /* SCALE_WET_WIDTH is a compensation weight factor to get an output
           amplitude (wet) rather independent of the width setting.
            0: the output amplitude is fully dependant on the width setting.
           >0: the output amplitude is less dependant on the width setting.
           With a SCALE_WET_WIDTH of 0.2 the output amplitude is rather
           independent of width setting (see fluid_chorus_set()).
         */
        const float SCALE_WET_WIDTH = 0.2f;
        const float SCALE_WET = 1.0f;

        const int MAX_SAMPLES = 2048;/* delay length in sample (46.4 ms at sample rate: 44100Hz).*/
        const int LOW_MOD_DEPTH = 176;    /* low mod_depth/2 in samples */
        const float HIGH_MOD_DEPTH = MAX_SAMPLES / 2; /* high mod_depth in sample */
        const float RANGE_MOD_DEPTH = (HIGH_MOD_DEPTH - LOW_MOD_DEPTH);

        /* Important min max values for MOD_RATE */
        /* mod rate define the rate at which the modulator is updated. Examples
           50: the modulator is updated every 50 samples (less cpu cycles expensive).
           1: the modulator is updated every sample (more cpu cycles expensive).
        */
        /* MOD_RATE acceptable for max lfo speed (5Hz) and max modulation depth (46.6 ms) */
        const int LOW_MOD_RATE = 5; /* MOD_RATE acceptable for low modulation depth (8 ms) */
        const int HIGH_MOD_RATE = 4;/* MOD_RATE acceptable for max modulation depth (46.6 ms) */
                                    /* and max lfo speed (5 Hz) */
        const int RANGE_MOD_RATE = (HIGH_MOD_RATE - LOW_MOD_RATE);

        /* some chorus cpu_load measurement dependant of modulation rate: mod_rate
         (number of chorus blocks: 2)

         No stero unit:
         mod_rate | chorus cpu load(%) | one voice cpu load (%)
         ----------------------------------------------------
         50       | 0.204              |
         5        | 0.256              |  0.169
         1        | 0.417              |

         With stero unit:
         mod_rate | chorus cpu load(%) | one voice cpu load (%)
         ----------------------------------------------------
         50       | 0.220              |
         5        | 0.274              |  0.169
         1        | 0.465              |

        */

        /*
         Number of samples to add to the desired length of the delay line. This
         allows to take account of rounding error interpolation when using large
         modulation depth.
         1 is sufficient for max modulation depth (46.6 ms) and max lfo speed (5 Hz).
        */
        //const int INTERP_SAMPLES_NBR 0
        const int INTERP_SAMPLES_NBR = 1;

        public const fluid_chorus_mod FLUID_CHORUS_DEFAULT_TYPE = fluid_chorus_mod.FLUID_CHORUS_MOD_SINE;  /**< Default chorus waveform type */


        fluid_chorus_mod type;
        float depth_ms;
        float level;
        float speed_Hz;
        int number_blocks;
        float sample_rate;

        /* width control: 0 monophonic, > 0 more stereophonic */
        float width;
        float wet1, wet2;

        float[] line; /* buffer line */
        int size;    /* effective internal size (in samples) */

        int line_in;  /* line in position */

        /* center output position members */
        float center_pos_mod; /* center output position modulated by modulator */
        int mod_depth;   /* modulation depth (in samples) */

        /* variable rate control of center output position */
        int index_rate;  /* index rate to know when to update center_pos_mod */
        int mod_rate;    /* rate at which center_pos_mod is updated */

        int FLUID_BUFSIZE;

        /**
         * Chorus modulation waveform type.
         */
        public enum fluid_chorus_mod
        {
            FLUID_CHORUS_MOD_SINE = 0,            /**< Sine wave chorus modulation */
            FLUID_CHORUS_MOD_TRIANGLE = 1         /**< Triangle wave chorus modulation */
        }

        /** Flags for fluid_chorus_set() */
        public enum fluid_chorus_set_t
        {
            FLUID_CHORUS_SET_NR = 1 << 0,
            FLUID_CHORUS_SET_LEVEL = 1 << 1,
            FLUID_CHORUS_SET_SPEED = 1 << 2,
            FLUID_CHORUS_SET_DEPTH = 1 << 3,
            FLUID_CHORUS_SET_TYPE = 1 << 4,

            /** Value for fluid_chorus_set() which sets all chorus parameters. */
            FLUID_CHORUS_SET_ALL = FLUID_CHORUS_SET_NR
                                       | FLUID_CHORUS_SET_LEVEL
                                       | FLUID_CHORUS_SET_SPEED
                                       | FLUID_CHORUS_SET_DEPTH
                                       | FLUID_CHORUS_SET_TYPE,
        }


        /* modulator member */
        modulator[] mod; /* sinus/triangle modulator */

        /*-----------------------------------------------------------------------------
      API
    ------------------------------------------------------------------------------*/
        /**
         * Create the chorus unit.
         * @sample_rate audio sample rate in Hz.
         * @return pointer on chorus unit.
         */

        public fluid_chorus(float psample_rate, int bufsize)
        {
            FLUID_BUFSIZE = bufsize;
            sample_rate = psample_rate;
            mod = new modulator[MAX_CHORUS];
            for (int i = 0; i < MAX_CHORUS; i++)
                mod[i] = new modulator();

            //# ifdef DEBUG_PRINT
            //            printf("fluid_chorus_t:{0} bytes\n", sizeof(fluid_chorus_t));
            //            printf("float:{0} bytes\n", sizeof(float));
            //#endif

            //# ifdef DEBUG_PRINT
            //            printf("NEW_MOD\n");
            //#endif

            new_mod_delay_line(MAX_SAMPLES);
        }

        /*-----------------------------------------------------------------------------
         Sets the frequency of sinus oscillator.

         @param mod pointer on modulator structure.
         @param freq frequency of the oscillator in Hz.
         @param sample_rate sample rate on audio output in Hz.
         @param phase initial phase of the oscillator in degree (0 to 360).
        -----------------------------------------------------------------------------*/
        void set_sinus_frequency(sinus_modulator mod, float freq, float sample_rate, float phase)
        {
            float w = 2f * Mathf.PI * freq / sample_rate; /* initial angle */
            float a;

            mod.a1 = 2f * Mathf.Cos(w);

            a = (2f * Mathf.PI / 360f) * phase;

            mod.buffer2 = Mathf.Sin(a - w); /* y(n-1) = sin(-intial angle) */
            mod.buffer1 = Mathf.Sin(a); /* y(n) = sin(initial phase) */
            mod.reset_buffer2 = Mathf.Sin(Mathf.PI / 2f - w); /* reset value for PI/2 */
        }

        /*-----------------------------------------------------------------------------
         Gets current value of sinus modulator:
           y(n) = a1 . y(n-1)  -  y(n-2)
           out = a1 . buffer1  -  buffer2

         @param pointer on modulator structure.
         @return current value of the modulator sine wave.
        -----------------------------------------------------------------------------*/
        float get_mod_sinus(sinus_modulator mod)
        {
            float outp;
            outp = mod.a1 * mod.buffer1 - mod.buffer2;
            mod.buffer2 = mod.buffer1;

            if (outp >= 1.0f) /* reset in case of instability near PI/2 */
            {
                outp = 1.0f; /* forces output to the right value */
                mod.buffer2 = mod.reset_buffer2;
            }

            if (outp <= -1.0f) /* reset in case of instability near -PI/2 */
            {
                outp = -1.0f; /* forces output to the right value */
                mod.buffer2 = -mod.reset_buffer2;
            }

            mod.buffer1 = outp;
            return outp;
        }

        /*-----------------------------------------------------------------------------
         Set the frequency of triangular oscillator
         The frequency is converted in a slope value.
         The initial value is set according to frac_phase which is a position
         in the period relative to the beginning of the period.
         For example: 0 is the beginning of the period, 1/4 is at 1/4 of the period
         relative to the beginning.
        -----------------------------------------------------------------------------*/
        void set_triangle_frequency(triang_modulator mod, float freq, float sample_rate, float frac_phase)
        {
            float ns_period; /* period in numbers of sample */

            if (freq <= 0f)
            {
                freq = 0.5f;
            }

            mod.freq = freq;

            ns_period = sample_rate / freq;

            /* the slope of a triangular osc (0 up to +1 down to -1 up to 0....) is equivalent
            to the slope of a saw osc (0 . +4) */
            mod.inc = 4f / ns_period; /* positive slope */

            /* The initial value and the sign of the slope depend of initial phase:
              initial value = = (ns_period * frac_phase) * slope
            */
            mod.val = ns_period * frac_phase * mod.inc;

            if (1f <= mod.val && mod.val < 3f)
            {
                mod.val = 2f - mod.val; /*  1.0 down to -1.0 */
                mod.inc = -mod.inc; /* negative slope */
            }
            else if (3f <= mod.val)
            {
                mod.val = mod.val - 4f; /*  -1.0 up to +1.0. */
            }

            /* else val < 1.0 */
        }

        /*-----------------------------------------------------------------------------
           Get current value of triangular oscillator
               y(n) = y(n-1) + dy
        -----------------------------------------------------------------------------*/
        float get_mod_triang(triang_modulator mod)
        {
            mod.val = mod.val + mod.inc;

            if (mod.val >= 1f)
            {
                mod.inc = -mod.inc;
                return 1f;
            }

            if (mod.val <= -1f)
            {
                mod.inc = -mod.inc;
                return -1f;
            }

            return mod.val;
        }
        /*-----------------------------------------------------------------------------
         Reads the sample value out of the modulated delay line.
         @param mdl, pointer on modulated delay line.
         @return the sample value.
        -----------------------------------------------------------------------------*/
        float get_mod_delay(modulator mod)
        {
            float out_index;  /* new modulated index position */
            int int_out_index; /* integer part of out_index */
            float outp; /* value to return */

            /* Checks if the modulator must be updated (every mod_rate samples). */
            /* Important: center_pos_mod must be used immediately for the
               first sample. So, mdl.index_rate must be initialized
               to mdl.mod_rate (new_mod_delay_line())  */

            if (index_rate >= mod_rate)
            {
                /* out_index = center position (center_pos_mod) + sinus waweform */
                if (type == fluid_chorus_mod.FLUID_CHORUS_MOD_SINE)
                {
                    out_index = center_pos_mod + get_mod_sinus(mod.sinus) * mod_depth;
                }
                else
                {
                    out_index = center_pos_mod + get_mod_triang(mod.triang) * mod_depth;
                }

                /* extracts integer part in int_out_index */
                if (out_index >= 0f)
                {
                    int_out_index = (int)out_index; /* current integer part */

                    /* forces read index (line_out)  with integer modulation value  */
                    /* Boundary check and circular motion as needed */
                    if ((mod.line_out = int_out_index) >= size)
                    {
                        mod.line_out -= size;
                    }
                }
                else /* negative */
                {
                    int_out_index = (int)(out_index - 1); /* previous integer part */
                                                          /* forces read index (line_out) with integer modulation value  */
                                                          /* circular motion as needed */
                    mod.line_out = int_out_index + size;
                }

                /* extracts fractionnal part. (it will be used when interpolating
                  between line_out and line_out +1) and memorize it.
                  Memorizing is necessary for modulation rate above 1 */
                mod.frac_pos_mod = out_index - int_out_index;
            }

            /*  First order all-pass interpolation ----------------------------------*/
            /* https://ccrma.stanford.edu/~jos/pasp/First_Order_Allpass_Interpolation.html */
            /*  begins interpolation: read current sample */
            outp = line[mod.line_out];

            /* updates line_out to the next sample.
               Boundary check and circular motion as needed */
            if (++mod.line_out >= size)
            {
                mod.line_out -= size;
            }

            /* Fractional interpolation between next sample (at next position) and
               previous output added to current sample.
            */
            outp += mod.frac_pos_mod * (line[mod.line_out] - mod.buffer);
            mod.buffer = outp; /* memorizes current output */
            return outp;
        }

        /*-----------------------------------------------------------------------------
         Initialize : mod_rate, center_pos_mod,  and index rate

         center_pos_mod is initialized so that the delay between center_pos_mod and
         line_in is: mod_depth + INTERP_SAMPLES_NBR.
        -----------------------------------------------------------------------------*/
        void set_center_position()
        {
            int center;

            /* Sets the modulation rate. This rate defines how often
             the  center position (center_pos_mod ) is modulated .
             The value is expressed in samples. The default value is 1 that means that
             center_pos_mod is updated at every sample.
             For example with a value of 2, the center position position will be
             updated only one time every 2 samples only.
            */
            mod_rate = LOW_MOD_RATE; /* default modulation rate */

            /* compensate mod rate for high modulation depth */
            if (mod_depth > LOW_MOD_DEPTH)
            {
                int delta_mod_depth = (mod_depth - LOW_MOD_DEPTH);
                mod_rate += (int)((delta_mod_depth * RANGE_MOD_RATE) / RANGE_MOD_DEPTH);
            }

            /* Initializes the modulated center position (center_pos_mod) so that:
                - the delay between center_pos_mod and line_in is:
                  mod_depth + INTERP_SAMPLES_NBR.
            */
            center = line_in - (INTERP_SAMPLES_NBR + mod_depth);

            if (center < 0)
            {
                center += size;
            }

            center_pos_mod = (float)center;

            /* index rate to control when to update center_pos_mod */
            /* Important: must be set to get center_pos_mod immediately used for the
               reading of first sample (see get_mod_delay()) */
            index_rate = mod_rate;
        }

        /*-----------------------------------------------------------------------------
         Modulated delay line initialization.

         Sets the length line ( alloc delay samples).
         Remark: the function sets the internal size accordling to the length delay_length.
         The size is augmented by INTERP_SAMPLES_NBR to take account of interpolation.

         @param chorus, pointer chorus unit.
         @param delay_length the length of the delay line in samples.
         @return FLUID_OK if success , FLUID_FAILED if memory error.

         Return FLUID_OK if success, FLUID_FAILED if memory error.
        -----------------------------------------------------------------------------*/
        bool new_mod_delay_line(int delay_length)
        {
            /*-----------------------------------------------------------------------*/
            /* checks parameter */
            if (delay_length < 1)
            {
                return false;
            }

            mod_depth = 0;
            /*-----------------------------------------------------------------------
             allocates delay_line and initialize members: - line, size, line_in...
            */
            /* total size of the line:  size = INTERP_SAMPLES_NBR + delay_length */
            size = delay_length + INTERP_SAMPLES_NBR;
            line = new float[size];


            /* clears the buffer:
             - delay line
             - interpolator member: buffer, frac_pos_mod
            */
            fluid_chorus_reset();

            /* Initializes line_in to the start of the buffer */
            line_in = 0;
            /*------------------------------------------------------------------------
             Initializes modulation members:
             - modulation rate (the speed at which center_pos_mod is modulated: mod_rate
             - modulated center position: center_pos_mod
             - index rate to know when to update center_pos_mod:index_rate
             -------------------------------------------------------------------------*/
            /* Initializes the modulated center position:
               mod_rate, center_pos_mod,  and index rate
            */
            set_center_position();
            return true;
        }

        /**
     * Clear the internal delay line and associate filter.
     * @param chorus pointer on chorus unit returned by new_fluid_chorus().
     */
        void fluid_chorus_reset()
        {
            int i;
            //uint u;

            /* reset delay line */
            for (i = 0; i < size; i++)
            {
                line[i] = 0;
            }

            /* reset modulators's allpass filter */
            foreach (modulator m in mod)
            {
                /* initializes 1st order All-Pass interpolator members */
                m.buffer = 0;       /* previous delay sample value */
                m.frac_pos_mod = 0; /* fractional position (between consecutives sample) */
            }
        }

        /**
         * Set one or more chorus parameters.
         * @param chorus Chorus instance
         * @param set Flags indicating which chorus parameters to set (#fluid_chorus_set_t)
         * @param nr Chorus voice count (0-99, CPU time consumption proportional to
         *   this value)
         * @param level Chorus level (0.0-10.0)
         * @param speed Chorus speed in Hz (0.1-5.0)
         * @param depth_ms Chorus depth (max value depends on synth sample rate,
         *   0.0-21.0 is safe for sample rate values up to 96KHz)
         * @param type Chorus waveform type (#fluid_chorus_mod)
         */
        public void fluid_chorus_set(int set, int nr, float plevel, float speed, float pdepth_ms, fluid_chorus_mod ptype, float pwidth)
        {
            int i;

            if ((set & (int)fluid_chorus_set_t.FLUID_CHORUS_SET_NR) != 0) /* number of block */
            {
                number_blocks = nr;
            }

            if ((set & (int)fluid_chorus_set_t.FLUID_CHORUS_SET_LEVEL) != 0) /* output level */
            {
                level = plevel;
            }

            if ((set & (int)fluid_chorus_set_t.FLUID_CHORUS_SET_SPEED) != 0) /* lfo frequency (in Hz) */
            {
                speed_Hz = speed;
            }

            if ((set & (int)fluid_chorus_set_t.FLUID_CHORUS_SET_DEPTH) != 0) /* modulation depth (in ms) */
            {
                depth_ms = pdepth_ms;
            }

            if ((set & (int)fluid_chorus_set_t.FLUID_CHORUS_SET_TYPE) != 0) /* lfo shape (sinus, triangle) */
            {
                type = ptype;
            }

            /* check min , max parameters */
            if (number_blocks < 0)
            {
                Debug.Log("chorus: number blocks must be >=0! Setting value to 0.");
                number_blocks = 0;
            }
            else if (number_blocks > MAX_CHORUS)
            {
                Debug.LogFormat("chorus: number blocks larger than max. allowed! Setting value to {0}.", MAX_CHORUS);
                number_blocks = MAX_CHORUS;
            }

            if (speed_Hz < MIN_SPEED_HZ)
            {
                Debug.LogFormat("chorus: speed is too low (min {0})! Setting value to min.", MIN_SPEED_HZ);
                speed_Hz = MIN_SPEED_HZ;
            }
            else if (speed_Hz > MAX_SPEED_HZ)
            {
                Debug.LogFormat("chorus: speed must be below {0} Hz! Setting value to max.", (double)MAX_SPEED_HZ);
                speed_Hz = MAX_SPEED_HZ;
            }

            if (depth_ms < 0f)
            {
                Debug.Log("chorus: depth must be positive! Setting value to 0.");
                depth_ms = 0f;
            }

            if (level < 0f)
            {
                Debug.Log("chorus: level must be positive! Setting value to 0.");
                level = 0f;
            }
            else if (level > MAX_LEVEL)
            {
                Debug.Log("chorus: level must be < 10. A reasonable level is << 1! Setting it to 0.1.");
                level = 0.1f;
            }

            /* initialize modulation depth (peak to peak) (in samples). convert modulation depth in ms to s*/
            mod_depth = (int)(depth_ms / 1000.0 * sample_rate);

            if (mod_depth > MAX_SAMPLES)
            {
                //Debug.LogFormat("chorus: Too high depth. Setting it to max ({0}).", MAX_SAMPLES);
                mod_depth = MAX_SAMPLES;
                // set depth to maximum to avoid spamming console with above warning
                depth_ms = (mod_depth * 1000) / sample_rate;
            }

            /* amplitude is peak to peek / 2 */
            mod_depth /= 2;

            //# ifdef DEBUG_PRINT
            //            Debug.LogFormat("depth_ms:%f, depth_samples/2:{0}\n", depth_ms, mod_depth);
            //#endif
            /* Initializes the modulated center position:
               mod_rate, center_pos_mod,  and index rate.
            */
            /* must be called before set_xxxx_frequency() */
            set_center_position();

            //# ifdef DEBUG_PRINT
            //            Debug.LogFormat("mod_rate:{0}", mod_rate);
            //#endif

            /* initialize modulator frequency */
            for (i = 0; i < number_blocks; i++)
            {
                set_sinus_frequency(mod[i].sinus,
                                    speed_Hz * mod_rate,
                                    sample_rate,
                                    /* phase offset between modulators waveform */
                                    (float)((360f / (float)number_blocks) * i));

                set_triangle_frequency(mod[i].triang,
                                       speed_Hz * mod_rate,
                                       sample_rate,
                                       /* phase offset between modulators waveform */
                                       (float)i / number_blocks);
            }

            //# ifdef DEBUG_PRINT
            //            Debug.LogFormat("lfo type:{0}\n", type);
            //            Debug.LogFormat("speed_Hz:%f\n", speed_Hz);
            //#endif

            //# ifdef DEBUG_PRINT

            //            if (type == fluid_chorus_mod.FLUID_CHORUS_MOD_SINE)
            //            {
            //                printf("lfo: sinus\n");
            //            }
            //            else
            //            {
            //                printf("lfo: triangle\n");
            //            }

            //            printf("nr:{0}\n", number_blocks);
            //#endif

            /* Recalculate internal values after parameters change */

            /*
             Note:
             Actually WIDTH is fixed to maximum value. But in the future we could add a setting
             "synth.width" to allow a gradually stereo effect from minimum (monophonic) to
             maximum stereo effect.
             If this setting will be added, remove the following instruction.
            */
            width = pwidth; // set as a setting in MPTK

            /* The stereo amplitude equation (wet1 and wet2 below) have a
             tendency to produce high amplitude with high width values ( 1 < width < 10).
             This results in an unwanted noisy output clipped by the audio card.
             To avoid this dependency, we divide by (1 + width * SCALE_WET_WIDTH)
             Actually, with a SCALE_WET_WIDTH of 0.2, (regardless of level setting),
             the output amplitude (wet) seems rather independent of width setting */

            float wet = level;// * SCALE_WET;

            /* wet1 and wet2 are used by the stereo effect controlled by the width setting
            for producing a stereo ouptput from a monophonic chorus signal.
            Please see the note above about a side effect tendency */

            if (number_blocks > 1)
            {
                wet = wet / (1.0f + width * SCALE_WET_WIDTH);
                wet1 = wet * (width / 2.0f + 0.5f);
                wet2 = wet * ((1.0f - width) / 2.0f);
                //# ifdef DEBUG_PRINT
                //                    printf("width:%f\n", width);

                //                    if (width > 0)
                //                    {
                //                        printf("nr > 1, width > 0 => out stereo\n");
                //                    }
                //                    else
                //                    {
                //                        printf("nr > 1, width:0 =>out mono\n");
                //                    }

                //#endif
            }
            else
            {
                /* only one chorus block */
                if (width == 0f)
                {
                    /* wet1 and wet2 should make stereo output monomophic */
                    wet1 = wet2 = wet;
                }
                else
                {
                    /* for width > 0, wet1 and wet2 should make stereo output stereo
                       with only one block. This will only possible by inverting
                       the unique signal on each left and right output.
                       Note however that with only one block, it isn't possible to
                       have a graduate width effect */
                    wet1 = wet;
                    wet2 = -wet; /* inversion */
                }

                //# ifdef DEBUG_PRINT
                //                    printf("width:%f\n", width);

                //                    if (width != 0)
                //                    {
                //                        printf("one block, width > 0 => out stereo\n");
                //                    }
                //                    else
                //                    {
                //                        printf("one block,  width:0 => out mono\n");
                //                    }

                //#endif
            }
        }

        /**
         * Process chorus by mixing the result in output buffer.
         * @param chorus pointer on chorus unit returned by new_fluid_chorus().
         * @param in, pointer on monophonic input buffer of FLUID_BUFSIZE samples.
         * @param left_out, right_out, pointers on stereo output buffers of FLUID_BUFSIZE samples.
         */
        public void fluid_chorus_processmix(float[] inp, float[] left_out, float[] right_out)
        {
            int sample_index;
            int i;
            float d_out0;               /* output stereo Left and Right  */
            float d_out1;               /* output stereo Left and Right  */

            /* foreach sample, process output sample then input sample */
            for (sample_index = 0; sample_index < FLUID_BUFSIZE; sample_index++)
            {
                float outp = 0; /* block output */

                d_out0 = d_out1 = 0f; /* clear stereo unit input */

                //#if 0
                //        /* Debug: Listen to the chorus signal only */
                //        left_out[sample_index] = 0;
                //        right_out[sample_index] = 0;
                //#endif

                ++index_rate; /* modulator rate */

                /* foreach chorus block, process output sample */
                for (i = 0; i < number_blocks; i++)
                {
                    /* get sample from the output of modulated delay line */
                    outp = get_mod_delay(mod[i]);

                    /* accumulate out into stereo unit input */
                    if ((i & 1) == 1)
                        d_out1 += outp;
                    else
                        d_out0 += outp;
                }

                /* update modulator index rate and output center position */
                if (index_rate >= mod_rate)
                {
                    index_rate = 0; /* clear modulator index rate */

                    /* updates center position (center_pos_mod) to the next position
                       specified by modulation rate */
                    if ((center_pos_mod += mod_rate) >= size)
                    {
                        center_pos_mod -= size;
                    }
                }

                /* Adjust stereo input level in case of number_blocks odd:
                   In those case, d_out[1] level is lower than d_out[0], so we need to
                   add out value to d_out[1] to have d_out[0] and d_out[1] balanced.
                */
                if ((i & 1) == 1 && i > 2)  // i = 3,5,7...
                {
                    d_out1 += outp;
                }

                /* process stereo unit */
                /* Add the chorus stereo unit d_out to left and right output */
                left_out[sample_index] += d_out0 * wet1 + d_out1 * wet2;
                right_out[sample_index] += d_out1 * wet1 + d_out0 * wet2;

                /* Write the current input sample into the circular buffer */
                /*-----------------------------------------------------------------------------
                Push a sample val into the delay line
                -----------------------------------------------------------------------------*/
                //#define push_in_delay_line(dl, val) \
                //{\
                //    dl.line[dl.line_in] = val;\
                //    /* Incrementation and circular motion if necessary */\
                //    if(++dl.line_in >= dl.size) dl.line_in -= dl.size;\
                //}
                //push_in_delay_line(chorus in[sample_index]);

                line[line_in] = inp[sample_index];
                /* Incrementation and circular motion if necessary */
                if (++line_in >= size) line_in -= size;
            }
        }
    }
}
