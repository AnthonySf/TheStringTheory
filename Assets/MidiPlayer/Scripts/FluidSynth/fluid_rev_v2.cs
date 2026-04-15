/******************************************************************************
 * FluidSynth - A Software Synthesizer
 *
 * Copyright (C) 2003  Peter Hanappe and others.
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
 *
 *  CHANGES
 *   - Adapted for Unity, Thierry Bachmann, March 2020
 *
 *                           FDN REVERB
 *
 * Freeverb used by fluidsynth (v.1.1.10 and previous) is based on
 * Schroeder-Moorer reverberator:
 * https://ccrma.stanford.edu/~jos/pasp/Freeverb.html
 *
 * This FDN reverberation is based on jot FDN reverberator.
 * https://ccrma.stanford.edu/~jos/Reverb/FDN_Late_Reverberation.html
 * Like Freeverb it is a late reverb which is convenient for Fluidsynth.
 *
 *
 *                                        .-------------------.
 *                      .-----------------|                   |
 *                      |              -  |      Feedback     |
 *                      |  .--------------|       Matrix      |
 *                      |  |              |___________________|
 *                      |  |                         /|\   /|\
 *                     \|/ |   .---------. .-------.  |  -  |   .------.
 *                   .->+ ---->| Delay 0 |-|L.P.F 0|--*-------->|      |-> out
 *      .---------.  |     |   |_________| |_______|        |   |      |  left
 *      |Tone     |  |     |       -           -            |   |Stereo|
 * In ->|corrector|--*     |       -           -            |   | unit |
 * mono |_________|  |    \|/  .---------. .-------.        |   |      |-> out
 *                    ---->+ ->| Delay 7 |-|L.P.F 7|--------*-->|      |  right
 *                             |_________| |_______|            |______|
 *                                          /|\ /|\              /|\ /|\
 *                                           |   |                |   |
 *                                roomsize --/   |       width  --/   |
 *                                    damp ------/       level  ------/
 *
 * It takes a monophonic input and produces a stereo output.
 *
 * The parameters are the same than for Freeverb.
 * Also the default response of these parameters are the same than for Freeverb:
 *  - roomsize (0 to 1): control the reverb time from 0.7 to 12.5 s.
 *    This reverberation time is ofen called T60DC.
 *
 *  - damp (0 to 1): controls the reverb time frequency dependency.
 *    This controls the reverb time for the frequency sample rate/2
 *
 *    When 0, the reverb time for high frequencies is the same as
 *    for DC frequency.
 *    When > 0, high frequencies have less reverb time than lower frequencies.
 *
 *  - width (0 to 100): controls the left/right output separation.
 *    When 0, there are no separation and the signal on left and right.
 *    output is the same. This sounds like a monophonic signal.
 *    When 100, the separation between left and right is maximum.
 *
 *  - level (0 to 1), controls the output level reverberation.
 *
 * This FDN reverb produces a better quality reverberation tail than Freeverb with
 * far less ringing by using modulated delay lines that help to cancel
 * the building of a lot of resonances in the reverberation tail even when
 * using only 8 delays lines (NBR_DELAYS = 8) (default).
 *
 * The frequency density (often called "modal density" is one property that
 * contributes to sound quality. Although 8 lines give good result, using 12 delays
 * lines brings the overall frequency density quality a bit higher.
 * This quality augmentation is noticeable particularly when using long reverb time
 * (roomsize = 1) on solo instrument with long release time. Of course the cpu load
 * augmentation is +50% relatively to 8 lines.
 *
 * As a general rule the reverberation tail quality is easier to perceive by ear
 * when using:
 * - percussive instruments (i.e piano and others).
 * - long reverb time (roomsize = 1).
 * - no damping (damp = 0).
 * - Using headphone. Avoid using loud speaker, you will be quickly misguided by the
 *   natural reverberation of the room in which you are.
 *
 * The cpu load for 8 lines is a bit lower than for freeverb (- 3%),
 * but higher for 12 lines (+ 41%).
 *
 *
 * The memory consumption is less than for freeverb
 * (see the results table below).
 *
 * Two macros are usable at compiler time:
 * - NBR_DELAYS: number of delay lines. 8 (default) or 12.
 * - ROOMSIZE_RESPONSE_LINEAR: allows to choose an alternate response of
 *   roomsize parameter.
 *   When this macro is not defined (the default), roomsize has the same
 *   response that Freeverb, that is:
 *   - roomsize (0 to 1) controls concave reverb time (0.7 to 12.5 s).
 *
 *   When this macro is defined, roomsize behaves linearly:
 *   - roomsize (0 to 1) controls reverb time linearly  (0.7 to 12.5 s).
 *   This linear response is convenient when using GUI controls.
 *
 * --------------------------------------------------------------------------
 * Compare table:
 * Note: the cpu load in % are relative each to other. These values are
 * given by the fluidsynth profile commands.
 * --------------------------------------------------------------------------
 * reverb    | NBR_DELAYS     | Performances    | memory size       | quality
 *           |                | (cpu_load: %)   | (bytes)(see note) |
 * ==========================================================================
 * freeverb  | 2 x 8 comb     |  0.670 %        | 204616            | ringing
 *           | 2 x 4 all-pass |                 |                   |
 * ----------|---------------------------------------------------------------
 *    FDN    | 8              |  0.650 %        | 112160            | far less
 * modulated |                |(feeverb - 3%)   | (55% freeverb)    | ringing
 *           |---------------------------------------------------------------
 *           | 12             |  0.942 %        | 168240            | best than
 *           |                |(freeverb + 41%) | (82 %freeverb)    | 8 lines
 *---------------------------------------------------------------------------
 *
 * Note:
 * Values in this column is the memory consumption for sample rate <= 44100Hz.
 * For sample rate > 44100Hz , multiply these values by (sample rate / 44100Hz).
 *
 *
 *----------------------------------------------------------------------------
 * 'Denormalise' method to avoid loss of performance.
 * --------------------------------------------------
 * According to music-dsp thread 'Denormalise', Pentium processors
 * have a hardware 'feature', that is of interest here, related to
 * numeric underflow.  We have a recursive filter. The output decays
 * exponentially, if the input stops.  So the numbers get smaller and
 * smaller... At some point, they reach 'denormal' level.  This will
 * lead to drastic spikes in the CPU load.  The effect was reproduced
 * with the reverb - sometimes the average load over 10 s doubles!!.
 *
 * The 'undenormalise' macro fixes the problem: As soon as the number
 * is close enough to denormal level, the macro forces the number to
 * 0.0f.  The original macro is:
 *
 * #define undenormalise(sample) if(((*(unsigned int*)&sample)&0x7f800000)==0) sample=0.0f
 *
 * This will zero out a number when it reaches the denormal level.
 * Advantage: Maximum dynamic range Disadvantage: We'll have to check
 * every sample, expensive.  The alternative macro comes from a later
 * mail from Jon Watte. It will zap a number before it reaches
 * denormal level. Jon suggests to run it once per block instead of
 * every sample.
 */

/* Denormalising part II:
 *
 * Another method fixes the problem cheaper: Use a small DC-offset in
 * the filter calculations.  Now the signals converge not against 0,
 * but against the offset.  The constant offset is invisible from the
 * outside world (i.e. it does not appear at the output.  There is a
 * very small turn-on transient response, which should not cause
 * problems.
 */

/*----------------------------------------------------------------------------
                        Configuration macros at compiler time.

 3 macros are usable at compiler time:
  - NBR_DELAYs: number of delay lines. 8 (default) or 12.
  - ROOMSIZE_RESPONSE_LINEAR: allows to choose an alternate response for
    roomsize parameter.
  - DENORMALISING enable denormalising handling.
-----------------------------------------------------------------------------*/
//#define INFOS_PRINT /* allows message to be printed on the console. */
#define DENORMALISING
using System;
using UnityEngine;

namespace MidiPlayerTK
{
    public class fluid_revmodel
    {
        int FLUID_BUFSIZE;

        /* Number of delay lines (must be only 8 or 12)
          8 is the default.
         12 produces a better quality but is +50% cpu expensive
        */
#if MPTK_REVERB_NBR_DELAYS_12
        const int NBR_DELAYS = 12; /* default or 12*/
#else
        const int NBR_DELAYS = 8; /* default or 12*/
#endif
        /* response curve of parameter roomsize  */
        /*
            The default response is the same as Freeverb:
            - roomsize (0 to 1) controls concave reverb time (0.7 to 12.5 s).

            when ROOMSIZE_RESPONSE_LINEAR is defined, the response is:
            - roomsize (0 to 1) controls reverb time linearly  (0.7 to 12.5 s).
        */
        //#define ROOMSIZE_RESPONSE_LINEAR

        /* DENORMALISING enable denormalising handling */

#if DENORMALISING
        const float DC_OFFSET = 1e-8f;
#else
        const float DC_OFFSET = 0f;
#endif

        /*----------------------------------------------------------------------------
         Initial internal reverb settings (at reverb creation time)
        -----------------------------------------------------------------------------*/
        /* SCALE_WET_WIDTH is a compensation weight factor to get an output
           amplitude (wet) rather independent of the width setting.
            0: the output amplitude is fully dependant on the width setting.
           >0: the output amplitude is less dependant on the width setting.
           With a SCALE_WET_WIDTH of 0.2 the output amplitude is rather
           independent of width setting (see fluid_revmodel_update()).
         */
        const float SCALE_WET_WIDTH = 0.2f;

        /* It is best to inject the input signal less ofen. This contributes to obtain
        a flatter response on comb filter. So the input gain is set to 0.1 rather 1.0. */
        const float FIXED_GAIN = 0.1f;/* input gain */

        /* SCALE_WET is adjusted to 5.0 to get internal output level equivalent to freeverb */
        const float SCALE_WET = 5.0f;/* scale output gain */

        /*----------------------------------------------------------------------------
         Internal FDN late reverb settings
        -----------------------------------------------------------------------------*/

        /*-- Reverberation time settings ----------------------------------
         MIN_DC_REV_TIME est defined egal to the minimum value of freeverb:
         MAX_DC_REV_TIME est defined egal to the maximum value of freeverb:
         T60DC is computed from gi and the longuest delay line in freeverb: L8 = 1617
         T60 = -3 * Li * T / log10(gi)
         T60 = -3 * Li *  / (log10(gi) * sr)

          - Li: length of comb filter delay line.
          - sr: sample rate.
          - gi: the feedback gain.

         The minimum value for freeverb correspond to gi = 0.7.
         with Mi = 1617, sr at 44100 Hz, and gi = 0.7 => MIN_DC_REV_TIME = 0.7 s

         The maximum value for freeverb correspond to gi = 0.98.
         with Mi = 1617, sr at 44100 Hz, and gi = 0.98 => MAX_DC_REV_TIME = 12.5 s
        */

        const float MIN_DC_REV_TIME = 0.7f; /* minimum T60DC reverb time: seconds */
        const float MAX_DC_REV_TIME = 12.5f;/* maximumm T60DC time in seconds */
        const float RANGE_REV_TIME = (MAX_DC_REV_TIME - MIN_DC_REV_TIME);

        /*-- Modulation related settings ----------------------------------*/
        /* For many instruments, the range for MOD_FREQ and MOD_DEPTH should be:

         MOD_DEPTH: [3..6] (in samples).
         MOD_FREQ: [0.5 ..2.0] (in Hz).

         Values below the lower limits are often not sufficient to cancel unwanted
         "ringing"(resonant frequency).
         Values above upper limits augment the unwanted "chorus".

         With NBR_DELAYS to 8:
          MOD_DEPTH must be >= 4 to cancel the unwanted "ringing".[4..6].
         With NBR_DELAYS to 12:
          MOD_DEPTH to 3 is sufficient to cancel the unwanted "ringing".[3..6]
        */
        /* modulation depth (samples)*/
        const int MOD_DEPTH = 4;
        /* modulation rate  (samples)*/
        const int MOD_RATE = 50;

        /* modulation frequency (Hz) */
        const float MOD_FREQ = 1f;
        /*
         Number of samples to add to the desired length of a delay line. This
         allow to take account of modulation interpolation.
         1 is sufficient with MOD_DEPTH equal to 4.
        */
        const int INTERP_SAMPLES_NBR = 1;

        /* phase offset between modulators waveform */
        const float MOD_PHASE = (360f / (float)NBR_DELAYS);

        const float FLUID_M_LN10 = 2.3025850929940456840179914546844f;

        public enum fluid_revmodel_set_t
        {
            FLUID_REVMODEL_SET_ROOMSIZE = 1 << 0,
            FLUID_REVMODEL_SET_DAMPING = 1 << 1,
            FLUID_REVMODEL_SET_WIDTH = 1 << 2,
            FLUID_REVMODEL_SET_LEVEL = 1 << 3,

            /** Value for fluid_revmodel_set() which sets all reverb parameters. */
            FLUID_REVMODEL_SET_ALL = FLUID_REVMODEL_SET_LEVEL
                                                  | FLUID_REVMODEL_SET_WIDTH
                                                  | FLUID_REVMODEL_SET_DAMPING
                                                  | FLUID_REVMODEL_SET_ROOMSIZE,
        }



        /*---------------------------------------------------------------------------*/
        /* The FDN late feed back matrix: A
                                    T
          A   = P  -  2 / N * u  * u
           N     N             N    N

          N: the matrix dimension (i.e NBR_DELAYS).
          P: permutation matrix.
          u: is a colomn vector of 1.

        */
        const float FDN_MATRIX_FACTOR = -2f / NBR_DELAYS;

        /*----------------------------------------------------------------------------
                     Internal FDN late structures and static functions
        -----------------------------------------------------------------------------*/


        /*-----------------------------------------------------------------------------
         Delay absorbent low pass filter
        -----------------------------------------------------------------------------*/
        public class fdn_delay_lpf
        {
            public float buffer;
            /* filter coefficients */
            public float b0, a1;
            /*-----------------------------------------------------------------------------
             Sets coefficients for delay absorbent low pass filter.
             @param lpf pointer on low pass filter structure.
             @param b0,a1 coefficients.
            -----------------------------------------------------------------------------*/
            public void set_fdn_delay_lpf(float pb0, float pa1)
            {
                b0 = pb0;
                a1 = pa1;
            }
        }

        /*-----------------------------------------------------------------------------
         Process delay absorbent low pass filter.
         @param mod_delay modulated delay line.
         @param in, input sample.
         @param out output sample.
        -----------------------------------------------------------------------------*/
        /*-----------------------------------------------------------------------------
         Delay line :
         The delay line is composed of the line plus an absorbent low pass filter
         to get frequency dependant reverb time.
        -----------------------------------------------------------------------------*/
        public class delay_line
        {
            public float[] line; /* buffer line */
            public int size;    /* effective internal size (in samples) */
                                /*-------------*/
            public int line_in;  /* line in position */
            public int line_out; /* line out position */
                                 /*-------------*/
            public fdn_delay_lpf damping; /* damping low pass filter */

            public delay_line()
            {
                damping = new fdn_delay_lpf();
            }

            /*-----------------------------------------------------------------------------
             Clears a delay line to DC_OFFSET float value.
             @param dl pointer on delay line structure
            -----------------------------------------------------------------------------*/
            public void clear_delay_line()
            {
                int i;

                for (i = 0; i < size; i++)
                {
                    line[i] = DC_OFFSET;
                }
            }

        }



        ///*-----------------------------------------------------------------------------
        // Push a sample val into the delay line
        //-----------------------------------------------------------------------------*/
        //#define push_in_delay_line(dl, val) \
        //{\
        //    dl->line[dl->line_in] = val;\
        //    /* Incrementation and circular motion if necessary */\
        //    if(++dl->line_in >= dl->size) dl->line_in -= dl->size;\
        //}\

        /*-----------------------------------------------------------------------------
         Modulator for modulated delay line
        -----------------------------------------------------------------------------*/

        /*-----------------------------------------------------------------------------
         Sinusoidal modulator
        -----------------------------------------------------------------------------*/
        /* modulator are integrated in modulated delay line */
        public class sinus_modulator
        {
            public float a1;          /* Coefficient: a1 = 2 * cos(w) */
            public float buffer1;     /* buffer1 */
            public float buffer2;     /* buffer2 */
            public float reset_buffer2;/* reset value of buffer2 */


            /*-----------------------------------------------------------------------------
             Sets the frequency of sinus oscillator.

             @param mod pointer on modulator structure.
             @param freq frequency of the oscillator in Hz.
             @param sample_rate sample rate on audio output in Hz.
             @param phase initial phase of the oscillator in degree (0 to 360).
            -----------------------------------------------------------------------------*/
            public void set_mod_frequency(float freq, float sample_rate, float phase)
            {
                float w = 2f * Mathf.PI * freq / sample_rate; /* initial angle */
                float a;

                a1 = 2 * Mathf.Cos(w);

                a = (2 * Mathf.PI / 360f) * phase;

                buffer2 = Mathf.Sin(a - w); /* y(n-1) = sin(-intial angle) */
                buffer1 = Mathf.Sin(a); /* y(n) = sin(initial phase) */
                reset_buffer2 = Mathf.Sin(Mathf.PI / 2f - w); /* reset value for PI/2 */
            }

            /*-----------------------------------------------------------------------------
             Gets current value of sinus modulator:
               y(n) = a1 . y(n-1)  -  y(n-2)
               out = a1 . buffer1  -  buffer2

             @param pointer on modulator structure.
             @return current value of the modulator sine wave.
            -----------------------------------------------------------------------------*/
            public float get_mod_sinus()
            {
                float outp;
                outp = a1 * buffer1 - buffer2;
                buffer2 = buffer1;

                if (outp >= 1f) /* reset in case of instability near PI/2 */
                {
                    outp = 1f; /* forces output to the right value */
                    buffer2 = reset_buffer2;
                }

                if (outp <= -1f) /* reset in case of instability near -PI/2 */
                {
                    outp = -1f; /* forces output to the right value */
                    buffer2 = -reset_buffer2;
                }

                buffer1 = outp;
                return outp;
            }
        }

        /*-----------------------------------------------------------------------------
         Modulated delay line. The line is composed of:
         - the delay line with its damping low pass filter.
         - the sinusoidal modulator.
         - center output position modulated by the modulator.
         - variable rate control of center output position.
         - first order All-Pass interpolator.
        -----------------------------------------------------------------------------*/
        public class mod_delay_line
        {
            /* delay line with damping low pass filter member */
            public delay_line dl; /* delayed line */
                                  /*---------------------------*/
                                  /* Sinusoidal modulator member */
            public sinus_modulator mod; /* sinus modulator */
                                        /*-------------------------*/
                                        /* center output position members */
            float center_pos_mod; /* center output position modulated by modulator */
            int mod_depth;   /* modulation depth (in samples) */
                             /*-------------------------*/
                             /* variable rate control of center output position */
            int index_rate;  /* index rate to know when to update center_pos_mod */
            int mod_rate;    /* rate at which center_pos_mod is updated */
                             /*-------------------------*/
                             /* first order All-Pass interpolator members */
            float frac_pos_mod; /* fractional position part between samples) */
                                /* previous value used when interpolating using fractional */
            float buffer;

            public mod_delay_line()
            {
                dl = new delay_line();
                mod = new sinus_modulator();
            }

            /*-----------------------------------------------------------------------------
             Modulated delay line initialization.

             Sets the length line ( alloc delay samples).
             Remark: the function sets the internal size accordling to the length delay_length.
             As the delay line is a modulated line, its internal size is augmented by mod_depth.
             The size is also augmented by INTERP_SAMPLES_NBR to take account of interpolation.

             @param mdl, pointer on modulated delay line.
             @param delay_length the length of the delay line in samples.
             @param mod_depth depth of the modulation in samples (amplitude of the sine wave).
             @param mod_rate the rate of the modulation in samples.
             @return FLUID_OK if success , FLUID_FAILED if memory error.

             Return FLUID_OK if success, FLUID_FAILED if memory error.
            -----------------------------------------------------------------------------*/
            public bool set_mod_delay_line(int delay_length, int pmod_depth, int pmod_rate)
            {
                /*-----------------------------------------------------------------------*/
                /* checks parameter */
                if (delay_length < 1)
                {
                    return false;
                }

                /* limits mod_depth to the requested delay length */
                if (pmod_depth >= delay_length)
                {
                    Debug.Log("fdn reverb: modulation depth has been limited");
                    pmod_depth = delay_length - 1;
                }

                mod_depth = pmod_depth;
                /*-----------------------------------------------------------------------
                 allocates delay_line and initialize members:
                   - line, size, line_in, line_out...
                */
                {
                    /* total size of the line:
                    size = INTERP_SAMPLES_NBR + mod_depth + delay_length */
                    dl.size = delay_length + pmod_depth + INTERP_SAMPLES_NBR;
                    dl.line = new float[dl.size];

                    dl.clear_delay_line(); /* clears the buffer */

                    /* Initializes line_in to the start of the buffer */
                    dl.line_in = 0;
                    /*  Initializes line_out index INTERP_SAMPLES_NBR samples after line_in */
                    /*  so that the delay between line_out and line_in is:
                        mod_depth + delay_length */
                    dl.line_out = dl.line_in + INTERP_SAMPLES_NBR;
                }

                /* Damping low pass filter -------------------*/
                dl.damping.buffer = 0;
                /*------------------------------------------------------------------------
                 Initializes modulation members:
                 - modulated center position: center_pos_mod
                 - index rate to know when to update center_pos_mod:index_rate
                 - modulation rate (the speed at which center_pos_mod is modulated: mod_rate
                 - interpolator member: buffer, frac_pos_mod
                 -------------------------------------------------------------------------*/
                /* Sets the modulation rate. This rate defines how often
                 the  center position (center_pos_mod ) is modulated .
                 The value is expressed in samples. The default value is 1 that means that
                 center_pos_mod is updated at every sample.
                 For example with a value of 2, the center position position will be
                 updated only one time every 2 samples only.
                */
                mod_rate = 1; /* default modulation rate: every one sample */

                if (pmod_rate > dl.size)
                {
                    Debug.Log("fdn reverb: modulation rate is out of range");
                }
                else
                {
                    mod_rate = pmod_rate;
                }

                /* Initializes the modulated center position (center_pos_mod) so that:
                    - the delay between line_out and center_pos_mod is mod_depth.
                    - the delay between center_pos_mod and line_in is delay_length.
                 */
                center_pos_mod = (float)INTERP_SAMPLES_NBR + mod_depth;

                /* index rate to control when to update center_pos_mod */
                /* Important: must be set to get center_pos_mod immediately used for the
                   reading of first sample (see get_mod_delay()) */
                index_rate = mod_rate;

                /* initializes 1st order All-Pass interpolator members */
                buffer = 0;       /* previous delay sample value */
                frac_pos_mod = 0; /* fractional position (between consecutives sample) */
                return true;
            }

            /*-----------------------------------------------------------------------------
             Return norminal delay length

             @param mdl, pointer on modulated delay line.
            -----------------------------------------------------------------------------*/
            public int get_mod_delay_line_length()
            {
                return (dl.size - mod_depth - INTERP_SAMPLES_NBR);
            }

            /*-----------------------------------------------------------------------------
             Reads the sample value out of the modulated delay line.
             @param mdl, pointer on modulated delay line.
             @return the sample value.
            -----------------------------------------------------------------------------*/
            public float get_mod_delay()
            {
                float out_index;  /* new modulated index position */
                int int_out_index; /* integer part of out_index */
                float outp; /* value to return */

                /* Checks if the modulator must be updated (every mod_rate samples). */
                /* Important: center_pos_mod must be used immediately for the
                   first sample. So, index_rate must be initialized
                   to mod_rate (set_mod_delay_line())  */

                if (++index_rate >= mod_rate)
                {
                    index_rate = 0;

                    /* out_index = center position (center_pos_mod) + sinus waweform */
                    out_index = center_pos_mod + mod.get_mod_sinus() * mod_depth;

                    /* extracts integer part in int_out_index */
                    if (out_index >= 0f)
                    {
                        int_out_index = (int)out_index; /* current integer part */

                        /* forces read index (line_out)  with integer modulation value  */
                        /* Boundary check and circular motion as needed */
                        if ((dl.line_out = int_out_index) >= dl.size)
                        {
                            dl.line_out -= dl.size;
                        }
                    }
                    else /* negative */
                    {
                        int_out_index = (int)(out_index - 1); /* previous integer part */
                                                              /* forces read index (line_out) with integer modulation value  */
                                                              /* circular motion as needed */
                        dl.line_out = int_out_index + dl.size;
                    }

                    /* extracts fractionnal part. (it will be used when interpolating
                      between line_out and line_out +1) and memorize it.
                      Memorizing is necessary for modulation rate above 1 */
                    frac_pos_mod = out_index - int_out_index;

                    /* updates center position (center_pos_mod) to the next position
                       specified by modulation rate */
                    if ((center_pos_mod += mod_rate) >= dl.size)
                    {
                        center_pos_mod -= dl.size;
                    }
                }

                /*  First order all-pass interpolation ----------------------------------*/
                /* https://ccrma.stanford.edu/~jos/pasp/First_Order_Allpass_Interpolation.html */
                /*  begins interpolation: read current sample */
                outp = dl.line[dl.line_out];

                /* updates line_out to the next sample.
                   Boundary check and circular motion as needed */
                if (++dl.line_out >= dl.size)
                {
                    dl.line_out -= dl.size;
                }

                /* Fractional interpolation between next sample (at next position) and
                   previous output added to current sample.
                */
                outp += frac_pos_mod * (dl.line[dl.line_out] - buffer);
                buffer = outp; /* memorizes current output */
                return outp;
            }
        }

        /*-----------------------------------------------------------------------------
 fluidsynth reverb structure
-----------------------------------------------------------------------------*/

        /* reverb parameters */
        float roomsize; /* acting on reverb time */
        float damp; /* acting on frequency dependent reverb time */
        float level, wet1, wet2; /* output level */
        float width; /* width stereo separation */

        /* fdn reverberation structure */
        public fluid_late late;


        public float Roomsize { get { return roomsize; } set { roomsize = Mathf.Clamp(value, 0f, 1f); fluid_revmodel_update(); } }
        public float Damp { get { return damp; } set { damp = Mathf.Clamp(value, 0f, 1f); fluid_revmodel_update(); } }
        public float Level { get { return level; } set { level = Mathf.Clamp(value, 0f, 1f); fluid_revmodel_update(); } }
        public float Width { get { return width; } set { width = Mathf.Clamp(value, 0f, 100f); fluid_revmodel_update(); } }


        /*-----------------------------------------------------------------------------
         Late structure
        -----------------------------------------------------------------------------*/
        public class fluid_late
        {
            public float samplerate;       /* sample rate */
                                           /*----- High pass tone corrector -------------------------------------*/
            public float tone_buffer;
            public float b1, b2;
            /*----- Modulated delay lines lines ----------------------------------*/
            public mod_delay_line[] mod_delay_lines;
            /*-----------------------------------------------------------------------*/
            /* Output coefficients for separate Left and right stereo outputs */
            public float[] out_left_gain; /* Left delay lines' output gains */
            public float[] out_right_gain;/* Right delay lines' output gains*/

            public fluid_late()
            {
                mod_delay_lines = new mod_delay_line[NBR_DELAYS];
                for (int i = 0; i < NBR_DELAYS; i++)
                    mod_delay_lines[i] = new mod_delay_line();
                out_left_gain = new float[NBR_DELAYS]; /* Left delay lines' output gains */
                out_right_gain = new float[NBR_DELAYS];/* Right delay lines' output gains*/
            }

            //  typedef struct _fluid_late   fluid_late;


            /*-----------------------------------------------------------------------------
             Updates Reverb time and absorbent filters coefficients from parameters:

             @param late pointer on late structure.
             @param roomsize (0 to 1): acting on reverb time.
             @param damping (0 to 1): acting on absorbent damping filter.

             Design formulas:
             https://ccrma.stanford.edu/~jos/Reverb/First_Order_Delay_Filter_Design.html
             https://ccrma.stanford.edu/~jos/Reverb/Tonal_Correction_Filter.html
            -----------------------------------------------------------------------------*/
            public void update_rev_time_damping(float proomsize, float pdamp)
            {
                int i;
                float sample_period = 1 / samplerate; /* Sampling period */
                int delay_length;               /* delay length */
                float dc_rev_time;       /* Reverb time at 0 Hz (in seconds) */

                float alpha, alpha2;

                /*--------------------------------------------
                     Computes dc_rev_time and alpha
                ----------------------------------------------*/
                {
                    float gi_tmp, ai_tmp;
#if ROOMSIZE_RESPONSE_LINEAR
                    /*   roomsize parameter behave linearly:
                     *   - roomsize (0 to 1) controls reverb time linearly  (0.7 to 10 s).
                     *   This linear response is convenient when using GUI controls.
                    */
                    /*-----------------------------------------
                          Computes dc_rev_time
                    ------------------------------------------*/
                    /* compute internal reverberation time versus roomsize parameter  */
                    dc_rev_time = MIN_DC_REV_TIME + RANGE_REV_TIME * proomsize;
                    delay_length = mod_delay_lines[NBR_DELAYS - 1].get_mod_delay_line_length();
                    /* computes gi_tmp from dc_rev_time using relation E2 */
                    gi_tmp = Mathf.Pow(10, -3 * delay_length * sample_period / dc_rev_time); /* E2 */
#else
                    /*   roomsize parameters have the same response that Freeverb, that is:
                     *   - roomsize (0 to 1) controls concave reverb time (0.7 to 10 s).
                    */
                    {
                        /*-----------------------------------------
                         Computes dc_rev_time
                        ------------------------------------------*/
                        float gi_min, gi_max;

                        /* values gi_min et gi_max are computed using E2 for the line with
                          maximum delay */
                        delay_length = mod_delay_lines[NBR_DELAYS - 1].get_mod_delay_line_length();
                        gi_max = Mathf.Pow(10, (-3 * delay_length / MAX_DC_REV_TIME) * sample_period); /* E2 */
                        gi_min = Mathf.Pow(10, (-3 * delay_length / MIN_DC_REV_TIME) * sample_period); /* E2 */
                                                                                                       /* gi = f(roomsize, gi_max, gi_min) */
                        gi_tmp = gi_min + proomsize * (gi_max - gi_min);
                        /* Computes T60DC from gi using inverse of relation E2.*/
                        dc_rev_time = -3 * FLUID_M_LN10 * delay_length * sample_period / Mathf.Log(gi_tmp);
                    }
#endif 
                    /*--------------------------------------------
                        Computes alpha
                    ----------------------------------------------*/
                    /* Computes alpha from damp,ai_tmp,gi_tmp using relation R */
                    /* - damp (0 to 1) controls concave reverb time for fs/2 frequency (T60DC to 0) */
                    ai_tmp = 1f * pdamp;

                    /* Preserve the square of R */
                    alpha2 = 1f / (1f - ai_tmp / ((20f / 80f) * Mathf.Log(gi_tmp)));

                    alpha = Mathf.Sqrt(alpha2); /* R */
                }

                /* updates tone corrector coefficients b1,b2 from alpha */
                {
                    /*
                     Beta = (1 - alpha)  / (1 + alpha)
                     b1 = 1/(1-beta)
                     b2 = beta * b1
                    */
                    float beta = (1f - alpha) / (1f + alpha);
                    b1 = 1f / (1f - beta);
                    b2 = beta * b1;
                    tone_buffer = 0f;
                }

                /* updates damping  coefficients of all lines (gi , ai) from dc_rev_time, alpha */
                for (i = 0; i < NBR_DELAYS; i++)
                {
                    float gi, ai;

                    /* delay length */
                    delay_length = mod_delay_lines[i].get_mod_delay_line_length();

                    /* iir low pass filter gain */
                    gi = Mathf.Pow(10f, -3f * delay_length * sample_period / dc_rev_time);

                    /* iir low pass filter feedback gain */
                    ai = (20f / 80f) * Mathf.Log(gi) * (1f - 1f / alpha2);

                    /* b0 = gi * (1 - ai),  a1 = - ai */
                    mod_delay_lines[i].dl.damping.set_fdn_delay_lpf(gi * (1f - ai), -ai);
                }
            }

            /*-----------------------------------------------------------------------------
             Updates stereo coefficients
             @param late pointer on late structure
             @param wet level integrated in stereo coefficients.
            -----------------------------------------------------------------------------*/
            public void update_stereo_coefficient(float wet1)
            {
                int i;
                float wet;

                for (i = 0; i < NBR_DELAYS; i++)
                {
                    /*  delay lines output gains vectors Left and Right

                                       L    R
                                   0 | 1    1|
                                   1 |-1    1|
                                   2 | 1   -1|
                                   3 |-1   -1|

                                   4 | 1    1|
                                   5 |-1    1|
                     stereo gain = 6 | 1   -1|
                                   7 |-1   -1|

                                   8 | 1    1|
                                   9 |-1    1|
                                   10| 1   -1|
                                   11|-1   -1|
                    */

                    /* for left line: 00,  ,02,  ,04,  ,06,  ,08,  ,10,  ,12,... left_gain = +1 */
                    /* for left line:   ,01,  ,03,  ,05,  ,07,  ,09,  ,11,...    left_gain = -1 */
                    wet = wet1;
                    if ((i & 1) != 0)
                    {
                        wet = -wet1;
                    }
                    out_left_gain[i] = wet;

                    /* for right line: 00,01,      ,04,05,     ,08,09,     ,12,13  right_gain = +1 */
                    /* for right line:      ,02 ,03,     ,06,07,     ,10,11,...    right_gain = -1 */
                    wet = wet1;
                    if ((i & 2) != 0)
                    {
                        wet = -wet1;
                    }
                    out_right_gain[i] = wet;
                }
            }

            /*-----------------------------------------------------------------------------
             Creates all modulated lines.
             @param late, pointer on the fnd late reverb to initialize.
             @param sample_rate, the audio sample rate.
             @return FLUID_OK if success, FLUID_FAILED otherwise.
            -----------------------------------------------------------------------------*/
            public bool create_mod_delay_lines(float sample_rate)
            {
                /* Delay lines length table (in samples) */
                int[] delay_length;
#if MPTK_REVERB_NBR_DELAYS_12
                delay_length = new int[] { 601, 691, 773, 839, 919, 997, 1061, 1093, 1129, 1151, 1171, 1187 };
#else
                delay_length = new int[] { 601, 691, 773, 839, 919, 997, 1061, /* v1.9 1093*/ 1129 };
#endif
                int i;

                /*
                  1)"modal density" is one property that contributes to the quality of the reverb tail.
                    The more is the modal density, the less are unwanted resonant frequencies
                    build during the decay time: modal density = total delay / sample rate.

                    Delay line's length given by static table delay_length[] is nominal
                    to get minimum modal density of 0.15 at sample rate 44100Hz.
                    Here we set length_factor to 2 to multiply this nominal modal
                    density by 2. This leads to a default modal density of 0.15 * 2 = 0.3 for
                    sample rate <= 44100.

                    For sample rate > 44100, length_factor is multiplied by
                    sample_rate / 44100. This ensures that the default modal density keeps inchanged.
                    (Without this compensation, the default modal density would be diminished for
                    new sample rate change above 44100Hz).

                  2)Modulated delay line contributes to diminish resonnant frequencies (often called "ringing").
                    Modulation depth (mod_depth) is set to nominal value of MOD_DEPTH at sample rate 44100Hz.
                    For sample rate > 44100, mod_depth is multiplied by sample_rate / 44100. This ensures
                    that the effect of modulated delay line keeps inchanged.
                */
                float length_factor = 2f;
                float mod_depth = MOD_DEPTH;
                if (sample_rate > 44100f)
                {
                    float sample_rate_factor = sample_rate / 44100f;
                    length_factor *= sample_rate_factor;
                    mod_depth *= sample_rate_factor;
                }
                //# ifdef INFOS_PRINT // allows message to be printed on the console.
                //                printf("length_factor:%f, mod_depth:%f\n", length_factor, mod_depth);
                //                /* Print: modal density and total memory bytes */
                //                {
                //                    int i;
                //                    int total_delay; /* total delay in samples */
                //                    for (i = 0, total_delay = 0; i < NBR_DELAYS; i++)
                //                    {
                //                        total_delay += length_factor * delay_length[i];
                //                    }

                //                    /* modal density and total memory bytes */
                //                    printf("modal density:%f, total memory:%d bytes\n",
                //                            total_delay / sample_rate, total_delay * sizeof(float));
                //                }
                //#endif

                for (i = 0; i < NBR_DELAYS; i++) /* for each delay line */
                {
                    /* allocate delay line and set local delay lines's parameters */
                    if (!mod_delay_lines[i].set_mod_delay_line((int)(delay_length[i] * length_factor), (int)mod_depth, MOD_RATE))
                    {
                        return false;
                    }

                    /* Sets local Modulators parameters: frequency and phase
                     Each modulateur are shifted of MOD_PHASE degree
                    */
                    mod_delay_lines[i].mod.set_mod_frequency(MOD_FREQ * MOD_RATE, samplerate, (float)(MOD_PHASE * i));
                }
                return true;
            }

            /*-----------------------------------------------------------------------------
             Creates the fdn reverb.
             @param late, pointer on the fnd late reverb to initialize.
             @param sample_rate the sample rate.
             @return FLUID_OK if success, FLUID_FAILED otherwise.
            -----------------------------------------------------------------------------*/
            public bool create_fluid_rev_late(float psample_rate)
            {
                //TBD FLUID_MEMSET(late, 0, sizeof(fluid_late));

                samplerate = psample_rate;

                /*--------------------------------------------------------------------------
                  First initialize the modulated delay lines
                */

                if (!create_mod_delay_lines(psample_rate))
                {
                    return false;
                }

                return true;
            }
        }
        /*
         Clears the delay lines.

         @param rev pointer on the reverb.
        */
        void fluid_revmodel_init()
        {
            int i;

            /* clears all the delay lines */
            for (i = 0; i < NBR_DELAYS; i++)
            {
                late.mod_delay_lines[i].dl.clear_delay_line();
            }
        }


        /*
         updates internal parameters.

         @param rev pointer on the reverb.
        */
        void fluid_revmodel_update()
        {
            /* Recalculate internal values after parameters change */

            /* The stereo amplitude equation (wet1 and wet2 below) have a
            tendency to produce high amplitude with high width values ( 1 < width < 100).
            This results in an unwanted noisy output clipped by the audio card.
            To avoid this dependency, we divide by (1 + width * SCALE_WET_WIDTH)
            Actually, with a SCALE_WET_WIDTH of 0.2, (regardless of level setting),
            the output amplitude (wet) seems rather independent of width setting */
            float wet = (level * SCALE_WET) / (1f + width * SCALE_WET_WIDTH);

            /* wet1 and wet2 are used by the stereo effect controlled by the width setting
            for producing a stereo ouptput from a monophonic reverb signal.
            Please see the note above about a side effect tendency */

            wet1 = wet * (width / 2f + 0.5f);
            wet2 = wet * ((1.0f - width) / 2f);

            /* integrates wet1 in stereo coefficient (this will save one multiply) */
            late.update_stereo_coefficient(wet1);

            if (wet1 > 0f)
            {
                wet2 /= wet1;
            }

            /* Reverberation time and damping */
            late.update_rev_time_damping(roomsize, damp);
        }

        /*----------------------------------------------------------------------------
                                    Reverb API
        -----------------------------------------------------------------------------*/

        /*
        * Creates a reverb. One created the reverb have no parameters set, so
        * fluid_revmodel_set() must be called at least one time after calling
        * new_fluid_revmodel().
        *
        * @param sample_rate sample rate in Hz.
        * @return pointer on the new reverb or NULL if memory error.
        * Reverb API.
        */
        public fluid_revmodel(float sample_rate, int bufsize)
        {
            FLUID_BUFSIZE = bufsize;
            late = new fluid_late();
            /* create fdn reverb */
            late.create_fluid_rev_late(sample_rate);
        }

        /*
        * free the reverb.
        * Note that while the reverb is used by calling any fluid_revmodel_processXXX()
        * function, calling delete_fluid_revmodel() isn't multi task safe because
        * delay line are freed. To deal properly with this issue follow the steps:
        *
        * 1) Stop reverb processing (i.e disable calling of any fluid_revmodel_processXXX().
        *    reverb functions.
        * 2) Delete the reverb by calling delete_fluid_revmodel().
        *
        * @param rev pointer on reverb to free.
        * Reverb API.
        */
        //void
        //delete_fluid_revmodel(fluid_revmodel_t* rev)
        //{
        //    fluid_return_if_fail(rev != NULL);
        //    delete_fluid_rev_late(&late);
        //    FLUID_FREE(rev);
        //}

        /*
        * Sets one or more reverb parameters. Note this must be called at least one
        * time after calling new_fluid_revmodel().
        *
        * Note that while the reverb is used by calling any fluid_revmodel_processXXX()
        * function, calling fluid_revmodel_set() could produce audible clics.
        * If this is a problem, optionally call fluid_revmodel_reset() before calling
        * fluid_revmodel_set().
        *
        * @param rev Reverb instance.
        * @param set One or more flags from #fluid_revmodel_set_t indicating what
        *   parameters to set (#FLUID_REVMODEL_SET_ALL to set all parameters).
        * @param roomsize Reverb room size.
        * @param damping Reverb damping.
        * @param width Reverb width.
        * @param level Reverb level.
        *
        * Reverb API.
        */
        public void fluid_revmodel_set(int set, float proomsize, float pdamping, float pwidth, float plevel)
        {
            /*-----------------------------------*/
            if ((set & (int)fluid_revmodel_set_t.FLUID_REVMODEL_SET_ROOMSIZE) != 0)
            {
                Roomsize = proomsize;
            }

            /*-----------------------------------*/
            if ((set & (int)fluid_revmodel_set_t.FLUID_REVMODEL_SET_DAMPING) != 0)
            {
                Damp = pdamping;
            }

            /*-----------------------------------*/
            if ((set & (int)fluid_revmodel_set_t.FLUID_REVMODEL_SET_WIDTH) != 0)
            {
                width = pwidth;
            }

            /*-----------------------------------*/
            if ((set & (int)fluid_revmodel_set_t.FLUID_REVMODEL_SET_LEVEL) != 0)
            {
                Level = plevel;
            }

            /* updates internal parameters */
            fluid_revmodel_update();
        }

        /*
        * Applies a sample rate change on the reverb.
        * Note that while the reverb is used by calling any fluid_revmodel_processXXX()
        * function, calling fluid_revmodel_samplerate_change() isn't multi task safe because
        * delay line are memory reallocated. To deal properly with this issue follow
        * the steps:
        * 1) Stop reverb processing (i.e disable calling of any fluid_revmodel_processXXX().
        *    reverb functions.
        * 2) Change sample rate by calling fluid_revmodel_samplerate_change().
        * 3) Restart reverb processing (i.e enabling calling of any fluid_revmodel_processXXX()
        *    reverb functions.
        *
        * Another solution is to substitute step (2):
        * 2.1) delete the reverb by calling delete_fluid_revmodel().
        * 2.2) create the reverb by calling new_fluid_revmodel().
        *
        * @param rev the reverb.
        * @param sample_rate new sample rate value.
        * @return FLUID_OK if success, FLUID_FAILED otherwise (memory error).
        * Reverb API.
        */
        bool fluid_revmodel_samplerate_change(float psample_rate)
        {
            late.samplerate = psample_rate; /* new sample rate value */

            /* free all delay lines */
            //TBD delete_fluid_rev_late(&late);

            /* create all delay lines */
            if (!late.create_mod_delay_lines(psample_rate))
            {
                return false; /* memory error */
            }

            /* updates damping filter coefficients according to sample rate change */
            late.update_rev_time_damping(roomsize, damp);

            return true;
        }

        /*
        * Damps the reverb by clearing the delay lines.
        * @param rev the reverb.
        *
        * Reverb API.
        */
        void fluid_revmodel_reset()
        {
            fluid_revmodel_init();
        }

        //        /*-----------------------------------------------------------------------------
        //        * fdn reverb process replace.
        //        * @param rev pointer on reverb.
        //        * @param in monophonic buffer input (FLUID_BUFSIZE sample).
        //        * @param left_out stereo left processed output (FLUID_BUFSIZE sample).
        //        * @param right_out stereo right processed output (FLUID_BUFSIZE sample).
        //        *
        //        * The processed reverb is replacing anything there in out.
        //        * Reverb API.
        //        -----------------------------------------------------------------------------*/
        //        void        fluid_revmodel_processreplace( const float*in,
        //                                  float* left_out, float* right_out)
        //    {
        //        int i, k;

        //        float xn;                   /* mono input x(n) */
        //        float out_tone_filter;      /* tone corrector output */
        //        float out_left, out_right;  /* output stereo Left  and Right  */
        //        float matrix_factor;        /* partial matrix computation */
        //        float delay_out_s;          /* sample */
        //        float delay_out[NBR_DELAYS]; /* Line output + damper output */

        //        for (k = 0; k<FLUID_BUFSIZE; k++)
        //        {
        //            /* stereo output */
        //            out_left = out_right = 0;

        //# ifdef DENORMALISING
        //            /* Input is adjusted by DC_OFFSET. */
        //            xn = (in[k]) * FIXED_GAIN + DC_OFFSET;
        //#else
        //        xn = (in[k]) * FIXED_GAIN;
        //#endif

        //    /*--------------------------------------------------------------------
        //     tone correction.
        //    */
        //    out_tone_filter = xn* late.b1 - late.b2* late.tone_buffer;
        //        late.tone_buffer = xn;
        //        xn = out_tone_filter;
        //        /*--------------------------------------------------------------------
        //         process  feedback delayed network:
        //          - xn is the input signal.
        //          - before inserting in the line input we first we get the delay lines
        //            output, filter them and compute output in delay_out[].
        //          - also matrix_factor is computed (to simplify further matrix product)
        //        ---------------------------------------------------------------------*/
        //        /* We begin with the modulated output delay line + damping filter */
        //        matrix_factor = 0;

        //        for(i = 0; i<NBR_DELAYS; i++)
        //        {
        //            mod_delay_line* mdl = &late.mod_delay_lines[i];
        //        /* get current modulated output */
        //        delay_out_s = get_mod_delay(mdl);

        //        /* process low pass damping filter (input:delay_out_s, output:delay_out_s) */
        //        process_damping_filter(delay_out_s, delay_out_s, mdl);
        //        ///* process low pass damping filter (input, output, delay) */
        //        //#define process_damping_filter(in,out,mod_delay) \
        //        //{\
        //        //    out = in * mod_delay->dl.damping.b0 - mod_delay->dl.damping.buffer* \
        //        //                                            mod_delay->dl.damping.a1;\
        //        //    mod_delay->dl.damping.buffer = out;\
        //        //}\

        //        /* Result in delay_out[], and matrix_factor.
        //           These will be of use later during input line process */
        //        delay_out[i] = delay_out_s;   /* result in delay_out[] */
        //            matrix_factor += delay_out_s; /* result in matrix_factor */

        //            /* Process stereo output */
        //            /* stereo left = left + out_left_gain * delay_out */
        //            out_left += late.out_left_gain[i] * delay_out_s;
        //    /* stereo right= right+ out_right_gain * delay_out */
        //    out_right += late.out_right_gain[i] * delay_out_s;
        //}

        //    /* now we process the input delay line.Each input is a combination of
        //       - xn: input signal
        //       - delay_out[] the output of a delay line given by a permutation matrix P
        //       - and matrix_factor.
        //      This computes: in_delay_line = xn + (delay_out[] * matrix A) with
        //      an algorithm equivalent but faster than using a product with matrix A.
        //    */
        //    /* matrix_factor = output sum * (-2.0)/N  */
        //    matrix_factor *= FDN_MATRIX_FACTOR;
        //        matrix_factor += xn; /* adds reverb input signal */

        //        for(i = 1; i<NBR_DELAYS; i++)
        //        {
        //            /* delay_in[i-1] = delay_out[i] + matrix_factor */
        //            delay_line* dl = &late.mod_delay_lines[i - 1].dl;
        //    push_in_delay_line(dl, delay_out[i] + matrix_factor);
        //}

        //        /* last line input (NB_DELAY-1) */
        //        /* delay_in[0] = delay_out[NB_DELAY -1] + matrix_factor */
        //        {
        //            delay_line* dl = &late.mod_delay_lines[NBR_DELAYS - 1].dl;
        //push_in_delay_line(dl, delay_out[0] + matrix_factor);
        //        }

        //        /*-------------------------------------------------------------------*/
        //#ifdef DENORMALISING
        //        /* Removes the DC offset */
        //        out_left -= DC_OFFSET;
        //        out_right -= DC_OFFSET;
        //#endif

        //        /* Calculates stereo output REPLACING anything already there: */
        //        /*
        //            left_out[k]  = out_left * wet1 + out_right * wet2;
        //            right_out[k] = out_right * wet1 + out_left * wet2;

        //            As wet1 is integrated in stereo coefficient wet 1 is now
        //            integrated in out_left and out_right we simplify previous
        //            relation by suppression of one multiply as this:

        //            left_out[k]  = out_left  + out_right * wet2;
        //            right_out[k] = out_right + out_left * wet2;
        //        */
        //        left_out[k]  = out_left  + out_right* wet2;
        //        right_out[k] = out_right + out_left* wet2;
        //    }
        //}

        private float[] delay_out = new float[NBR_DELAYS]; /* Line output + damper output */

        /*-----------------------------------------------------------------------------
        * fdn reverb process mix.
        * @param rev pointer on reverb.
        * @param in monophonic buffer input (FLUID_BUFSIZE samples).
        * @param left_out stereo left processed output (FLUID_BUFSIZE samples).
        * @param right_out stereo right processed output (FLUID_BUFSIZE samples).
        *
        * The processed reverb is mixed in out with samples already there in out.
        * Reverb API.
        -----------------------------------------------------------------------------*/
        public void fluid_revmodel_processmix(float[] inp, float[] left_out, float[] right_out)
        {
            int i, k;

            float xn;                   /* mono input x(n) */
            float out_tone_filter;      /* tone corrector output */
            float out_left, out_right;  /* output stereo Left  and Right  */
            float matrix_factor;        /* partial matrix term */
            float delay_out_s;          /* sample */
            //float[] delay_out; /* Line output + damper output */
            //delay_out = new float[NBR_DELAYS]; -- avoid realloc at each call
            Array.Clear(delay_out, 0, NBR_DELAYS);

            for (k = 0; k < FLUID_BUFSIZE; k++)
            {
                /* stereo output */
                out_left = out_right = 0;
#if DENORMALISING
                /* Input is adjusted by DC_OFFSET. */
                xn = (inp[k]) * FIXED_GAIN + DC_OFFSET;
#else
                xn = inp[k] * FIXED_GAIN;
#endif

                /*--------------------------------------------------------------------
                 tone correction
                */
                out_tone_filter = xn * late.b1 - late.b2 * late.tone_buffer;
                late.tone_buffer = xn;
                xn = out_tone_filter;
                /*--------------------------------------------------------------------
                 process feedback delayed network:
                  - xn is the input signal.
                  - before inserting in the line input we first we get the delay lines
                    output, filter them and compute output in local delay_out[].
                  - also matrix_factor is computed (to simplify further matrix product).
                ---------------------------------------------------------------------*/
                /* We begin with the modulated output delay line + damping filter */
                matrix_factor = 0;

                for (i = 0; i < NBR_DELAYS; i++)
                {
                    mod_delay_line mdl = late.mod_delay_lines[i];
                    /* get current modulated output */
                    delay_out_s = mdl.get_mod_delay();

                    /* process low pass damping filter (input:delay_out_s, output:delay_out_s) */
                    //process_damping_filter(delay_out_s, delay_out_s, mdl);
                    delay_out_s = delay_out_s * mdl.dl.damping.b0 - mdl.dl.damping.buffer * mdl.dl.damping.a1;
                    mdl.dl.damping.buffer = delay_out_s;

                    /* Result in delay_out[], and matrix_factor. These will be of use later during input line process */
                    delay_out[i] = delay_out_s;   /* result in delay_out[] */
                    matrix_factor += delay_out_s; /* result in matrix_factor */

                    /* Process stereo output */
                    /* stereo left = left + out_left_gain * delay_out */
                    out_left += late.out_left_gain[i] * delay_out_s;
                    /* stereo right= right+ out_right_gain * delay_out */
                    out_right += late.out_right_gain[i] * delay_out_s;
                }

                /* now we process the input delay line. Each input is a combination of:
                   - xn: input signal
                   - delay_out[] the output of a delay line given by a permutation matrix P
                   - and matrix_factor.
                  This computes: in_delay_line = xn + (delay_out[] * matrix A) with
                  an algorithm equivalent but faster than using a product with matrix A.
                */
                /* matrix_factor = output sum * (-2.0)/N  */
                matrix_factor *= FDN_MATRIX_FACTOR;
                matrix_factor += xn; /* adds reverb input signal */

                for (i = 1; i < NBR_DELAYS; i++)
                {
                    /* delay_in[i-1] = delay_out[i] + matrix_factor */
                    delay_line dl = late.mod_delay_lines[i - 1].dl;
                    //dl.push_in_delay_line(delay_out[i] + matrix_factor);
                    // Push a sample val into the delay line
                    dl.line[dl.line_in] = delay_out[i] + matrix_factor;
                    /* Incrementation and circular motion if necessary */
                    if (++dl.line_in >= dl.size) dl.line_in -= dl.size;

                }

                /* last line input (NB_DELAY-1) */
                /* delay_in[0] = delay_out[NB_DELAY -1] + matrix_factor */
                {
                    delay_line dl = late.mod_delay_lines[NBR_DELAYS - 1].dl;
                    //dl.push_in_delay_line(delay_out[0] + matrix_factor);
                    // Push a sample val into the delay line
                    dl.line[dl.line_in] = delay_out[0] + matrix_factor;
                    /* Incrementation and circular motion if necessary */
                    if (++dl.line_in >= dl.size) dl.line_in -= dl.size;
                }

                /*-------------------------------------------------------------------*/
#if DENORMALISING
                /* Removes the DC offset */
                out_left -= DC_OFFSET;
                out_right -= DC_OFFSET;
#endif
                /* Calculates stereo output MIXING anything already there: */
                /*
                    left_out[k]  += out_left * wet1 + out_right * wet2;
                    right_out[k] += out_right * wet1 + out_left * wet2;

                    As wet1 is integrated in stereo coefficient wet 1 is now
                    integrated in out_left and out_right we simplify previous
                    relation by suppression of one multiply as this:

                    left_out[k]  += out_left  + out_right * wet2;
                    right_out[k] += out_right + out_left * wet2;
                */
                left_out[k] += out_left + out_right * wet2;
                right_out[k] += out_right + out_left * wet2;
            }
        }
    }
}
