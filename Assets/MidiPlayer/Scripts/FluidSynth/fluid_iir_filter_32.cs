/* FluidSynth - A Software Synthesizer
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
 */

/**
  CHANGES
    - Adapted for Unity, Thierry Bachmann, March 2020

 * Applies a low- or high-pass filter with variable cutoff frequency and quality factor
 * for a given biquad transfer function:
 *          b0 + b1*z^-1 + b2*z^-2
 *  H(z) = ------------------------
 *          a0 + a1*z^-1 + a2*z^-2
 *
 * Also modifies filter state accordingly.
 * @param iir_filter Filter parameter
 * @param dsp_buf Pointer to the synthesized audio data
 * @param count Count of samples in dsp_buf
 */
/*
 * Variable description:
 * - dsp_a1, dsp_a2: Filter coefficients for the the previously filtered output signal
 * - dsp_b0, dsp_b1, dsp_b2: Filter coefficients for input signal
 * - coefficients normalized to a0
 *
 * A couple of variables are used internally, their results are discarded:
 * - dsp_i: Index through the output buffer
 * - dsp_centernode: delay line for the IIR filter
 * - dsp_hist1: same
 * - dsp_hist2: same
 */

using UnityEngine;

namespace MidiPlayerTK
{
    public class fluid_iir_filter_32: fluid_iir_filter
    {
        fluid_iir_filter_type type; /* specifies the type of this filter */
        fluid_iir_filter_flags flags; /* additional flags to customize this filter */

        /* filter coefficients */
        /* The coefficients are normalized to a0. */
        /* b0 and b2 are identical => b02 */
        float b02;              /* b0 / a0 */
        float b1;              /* b1 / a0 */
        float a1;              /* a0 / a0 */
        float a2;              /* a1 / a0 */

        float b02_incr;
        float b1_incr;
        float a1_incr;
        float a2_incr;
        int filter_coeff_incr_count;
        bool compensate_incr;        /* Flag: If set, must compensate history */
        float hist1, hist2;      /* Sample history for the IIR filter */
        bool filter_startup;             /* Flag: If set, the filter will be set directly.
					   Else it changes smoothly. */

        float fres;              /* the resonance frequency, in cents (not absolute cents) */
        float last_fres;         /* Current resonance frequency of the IIR filter */
        /* Serves as a flag: A deviation between fres and last_fres */
        /* indicates, that the filter has to be recalculated. */
        float q_lin;             /* the q-factor on a linear scale */
        float filter_gain;       /* Gain correction factor, depends on q */

        int FLUID_BUFSIZE;

        public fluid_iir_filter_32(int bufsize)
        {
            //Debug.Log("Init iir filter 32");
            FLUID_BUFSIZE = bufsize;
        }

        public override void fluid_iir_filter_apply(float[] dsp_buf, int count)
        {
            //Debug.Log($"fluid_iir_filter_32 {count} {q_lin}");
            // MPTK Specific, check effect disabled done above
            if (/*type == fluid_iir_filter_type.FLUID_IIR_DISABLED ||*/ q_lin == 0)
            {
                return;
            }
            else
            {
                /* IIR filter sample history */
                float dsp_hist1 = hist1;
                float dsp_hist2 = hist2;

                /* IIR filter coefficients */
                float dsp_a1 = a1;
                float dsp_a2 = a2;
                float dsp_b02 = b02;
                float dsp_b1 = b1;
                int dsp_filter_coeff_incr_count = filter_coeff_incr_count;

                float dsp_centernode;
                int dsp_i;

                /* filter (implement the voice filter according to SoundFont standard) */

                /* Check for denormal number (too close to zero). */
                if (Mathf.Abs(dsp_hist1) < 1e-20f)
                    dsp_hist1 = 0.0f;    /* FIXME JMG - Is this even needed? */

                /* Two versions of the filter loop. One, while the filter is
                * changing towards its new setting. The other, if the filter
                * doesn't change.
                */


                if (dsp_filter_coeff_incr_count > 0)
                {
                    float dsp_a1_incr = a1_incr;
                    float dsp_a2_incr = a2_incr;
                    float dsp_b02_incr = b02_incr;
                    float dsp_b1_incr = b1_incr;
                    //Debug.Log($"{FLUID_BUFSIZE} {dsp_filter_coeff_incr_count} a1_incr:{a1_incr:F6} a2_incr:{a2_incr:F6} b02_incr:{b02_incr:F6} b1_incr:{b1_incr:F6}");

                    /* Increment is added to each filter coefficient filter_coeff_incr_count times. */
                    for (dsp_i = 0; dsp_i < count; dsp_i++)
                    {
                        /* The filter is implemented in Direct-II form. */
                        dsp_centernode = dsp_buf[dsp_i] - dsp_a1 * dsp_hist1 - dsp_a2 * dsp_hist2;
                        dsp_buf[dsp_i] = dsp_b02 * (dsp_centernode + dsp_hist2) + dsp_b1 * dsp_hist1;
                        dsp_hist2 = dsp_hist1;
                        dsp_hist1 = dsp_centernode;

                        if (dsp_filter_coeff_incr_count-- > 0)
                        {
                            float old_b02 = dsp_b02;
                            dsp_a1 += dsp_a1_incr;
                            dsp_a2 += dsp_a2_incr;
                            dsp_b02 += dsp_b02_incr;
                            dsp_b1 += dsp_b1_incr;

                            /* Compensate history to avoid the filter going havoc with large frequency changes */
                            if (compensate_incr && (Mathf.Abs(dsp_b02) > 0.001f))
                            {
                                float compensate = old_b02 / dsp_b02;
                                dsp_hist1 *= compensate;
                                dsp_hist2 *= compensate;
                            }
                        }
                    } /* for dsp_i */
                }
                else /* The filter parameters are constant.  This is duplicated to save time. */
                {
                    //Debug.Log($"{FLUID_BUFSIZE} {dsp_filter_coeff_incr_count} a1:{a1:F6} a2:{a2:F6} b02:{b02:F6} dsp_b1:{dsp_b1:F6}");

                    for (dsp_i = 0; dsp_i < count; dsp_i++)
                    {
                        /* The filter is implemented in Direct-II form. */
                        dsp_centernode = dsp_buf[dsp_i] - dsp_a1 * dsp_hist1 - dsp_a2 * dsp_hist2;
                        dsp_buf[dsp_i] = dsp_b02 * (dsp_centernode + dsp_hist2) + dsp_b1 * dsp_hist1;
                        dsp_hist2 = dsp_hist1;
                        dsp_hist1 = dsp_centernode;
                    }
                }

                hist1 = dsp_hist1;
                hist2 = dsp_hist2;
                a1 = dsp_a1;
                a2 = dsp_a2;
                b02 = dsp_b02;
                b1 = dsp_b1;
                filter_coeff_incr_count = dsp_filter_coeff_incr_count;
            }
        }

        /* Macro for declaring an rvoice event function (#fluid_rvoice_function_t). The functions may only access
         * those params that were previously set in fluid_voice.c
         */
        //#define DECLARE_FLUID_RVOICE_FUNCTION(name) void name(void* obj, const fluid_rvoice_param_t param[MAX_EVENT_PARAMS])

        public override void fluid_iir_filter_init(fluid_iir_filter_type ptype, fluid_iir_filter_flags pflags)
        {
            type = ptype;
            flags = pflags;

            if (type != fluid_iir_filter_type.FLUID_IIR_DISABLED)
            {
                fluid_iir_filter_reset();
            }
        }

        public override void fluid_iir_filter_reset()
        {
            hist1 = 0;
            hist2 = 0;
            last_fres = -1f;
            q_lin = 0;
            filter_startup = true;
        }

        public override void fluid_iir_filter_set_fres(float pfres)
        {
            //Debug.Log($"fluid_iir_filter_set_fres pfres:{pfres}");

            fres = pfres;
            last_fres = -1f;
        }

        public override void fluid_iir_filter_set_q(float pq, float offset /*MPTK Specific*/)
        {
            float q = pq;
            //Debug.Log(pq + " " + offset);

            // MPTK: flags=FLUID_IIR_NOFLAGS -> 0
            //Debug.Log($"fluid_iir_filter_set_q q:{q} {flags}");

            if ((((int)flags & (int)fluid_iir_filter_flags.FLUID_IIR_Q_ZERO_OFF) != 0) && q <= 0f)
            {
                q = 0;
            }
            else if (((int)flags & (int)fluid_iir_filter_flags.FLUID_IIR_Q_LINEAR) != 0)
            {
                /* q is linear (only for user-defined filter)
                 * increase to avoid Q being somewhere between zero and one,
                 * which results in some strange amplified lowpass signal
                 */
                q++;
            }
            else
            {
                // Default with MPTK (and fluidsynth)
                q = fluid_iir_filter_q_from_dB(q, offset);
                //Debug.Log($"fluid_iir_filter_q_from_dB voice.q_dB:{pq} offset:{offset} -> q:{q}");
            }

            q_lin = q;
            filter_gain = 1f;

            if (((int)flags & (int)fluid_iir_filter_flags.FLUID_IIR_NO_GAIN_AMP) == 0)
            {
                /* SF 2.01 page 59:
                 *
                 *  The SoundFont specs ask for a gain reduction equal to half the
                 *  height of the resonance peak (Q).  For example, for a 10 dB
                 *  resonance peak, the gain is reduced by 5 dB.  This is done by
                 *  multiplying the total gain with sqrt(1/Q).  `Sqrt' divides dB
                 *  by 2 (100 lin = 40 dB, 10 lin = 20 dB, 3.16 lin = 10 dB etc)
                 *  The gain is later factored into the 'b' coefficients
                 *  (numerator of the filter equation).  This gain factor depends
                 *  only on Q, so this is the right place to calculate it.
                 */
                filter_gain /= Mathf.Sqrt(q);
                //Debug.Log($"fluid_iir_filter_set_q filter_gain:{filter_gain}");
            }

            /* The synthesis loop will have to recalculate the filter coefficients. */
            last_fres = -1f;
        }

        private static float fluid_iir_filter_q_from_dB(float q_dB, float offset /*MPTK specific*/)
        {
            /* The generator contains 'centibels' (1/10 dB) => divide by 10 to
             * obtain dB */
            q_dB /= 10f;

            /* Range: SF2.01 section 8.1.3 # 8 (convert from cB to dB => /10) */
            q_dB = Mathf.Clamp(q_dB + offset, 0f, 96f);

            /* Short version: Modify the Q definition in a way, that a Q of 0
             * dB leads to no resonance hump in the freq. response.
             *
             * Long version: From SF2.01, page 39, item 9 (initialFilterQ):
             * "The gain at the cutoff frequency may be less than zero when
             * zero is specified".  Assume q_dB=0 / q_lin=1: If we would leave
             * q as it is, then this results in a 3 dB hump slightly below
             * fc. At fc, the gain is exactly the DC gain (0 dB).  What is
             * (probably) meant here is that the filter does not show a
             * resonance hump for q_dB=0. In this case, the corresponding
             * q_lin is 1/sqrt(2)=0.707.  The filter should have 3 dB of
             * attenuation at fc now.  In this case Q_dB is the height of the
             * resonance peak not over the DC gain, but over the frequency
             * response of a non-resonant filter.  This idea is implemented as
             * follows: */
            q_dB -= 3.01f;

            /* The 'sound font' Q is defined in dB. The filter needs a linear
               q. Convert. */
            //Debug.Log($"fluid_iir_filter_q_from_dB q_dB:{q_dB} --> {Mathf.Pow(10f, q_dB / 20f)}");
            return Mathf.Pow(10f, q_dB / 20f);
        }

        private void fluid_iir_filter_calculate_coefficients(int transition_samples, float output_rate)
        {
            /* FLUID_IIR_Q_LINEAR may switch the filter off by setting Q==0 */
            if (q_lin == 0)
            {
                return;
            }
            else
            {
                /*
                 * Those equations from Robert Bristow-Johnson's `Cookbook
                 * formulae for audio EQ biquad filter coefficients', obtained
                 * from Harmony-central.com / Computer / Programming. They are
                 * the result of the bilinear transform on an analogue filter
                 * prototype. To quote, `BLT frequency warping has been taken
                 * into account for both significant frequency relocation and for
                 * bandwidth readjustment'. */

                // Don't forget the freq can change also with the modulator (case for piano)
                // So, this calculation will be done for each buffer (64 values)
                // -------------------------------------------------------------------------------

                float omega = (2f * Mathf.PI) * (last_fres / output_rate);
                float sin_coeff = Mathf.Sin(omega);
                float cos_coeff = Mathf.Cos(omega);
                float alpha_coeff = sin_coeff / (2f * q_lin);
                float a0_inv = 1f / (1f + alpha_coeff);

                /* Calculate the filter coefficients. All coefficients are
                 * normalized by a0. Think of `a1' as `a1/a0'.
                 *
                 * Here a couple of multiplications are saved by reusing common expressions.
                 * The original equations should be:
                 *  b0=(1.-cos_coeff)*a0_inv*0.5*filter_gain;
                 *  b1=(1.-cos_coeff)*a0_inv*filter_gain;
                 *  b2=(1.-cos_coeff)*a0_inv*0.5*filter_gain; */

                /* "a" coeffs are same for all 3 available filter types */
                float a1_temp = -2f * cos_coeff * a0_inv;
                float a2_temp = (1f - alpha_coeff) * a0_inv;

                float b02_temp, b1_temp;

                switch (type)
                {
                    case fluid_iir_filter_type.FLUID_IIR_HIGHPASS:
                        //Debug.Log("fluid_iir_filter_calculate_coefficients FLUID_IIR_HIGHPASS");
                        b1_temp = (1f + cos_coeff) * a0_inv * filter_gain;
                        /* both b0 -and- b2 */
                        b02_temp = b1_temp * 0.5f;
                        b1_temp *= -1f;
                        break;

                    case fluid_iir_filter_type.FLUID_IIR_LOWPASS:
                        b1_temp = (1f - cos_coeff) * a0_inv * filter_gain;
                        /* both b0 -and- b2 */
                        b02_temp = b1_temp * 0.5f;
                        //Debug.Log("fluid_iir_filter_calculate_coefficients FLUID_IIR_LOWPASS");
                        break;

                    default:
                        /* filter disabled, should never get here */
                        return;
                }

                compensate_incr = false;

                if (filter_startup || (transition_samples == 0))
                {
                    /* The filter is calculated, because the voice was started up.
                     * In this case set the filter coefficients without delay.
                     */
                    //Debug.Log("Setting initial filter coefficients.\n");
                    a1 = a1_temp;
                    a2 = a2_temp;
                    b02 = b02_temp;
                    b1 = b1_temp;
                    filter_coeff_incr_count = 0;
                    filter_startup = false;
                }
                else
                {
                    //Debug.Log($"Setting transition filter coefficients {transition_samples}.\n");

                    /* The filter frequency is changed.  Calculate an increment
                     * factor, so that the new setting is reached after one buffer
                     * length. x_incr is added to the current value FLUID_BUFSIZE
                     * times. The length is arbitrarily chosen. Longer than one
                     * buffer will sacrifice some performance, though.  Note: If
                     * the filter is still too 'grainy', then increase this number
                     * at will.
                     */

                    a1_incr = (a1_temp - a1) / transition_samples;
                    a2_incr = (a2_temp - a2) / transition_samples;
                    b02_incr = (b02_temp - b02) / transition_samples;
                    b1_incr = (b1_temp - b1) / transition_samples;

                    if (Mathf.Abs(b02) > 0.0001f)
                    {
                        float quota = b02_temp / b02;
                        compensate_incr = quota < 0.5f || quota > 2f;
                    }

                    /* Have to add the increments filter_coeff_incr_count times. */
                    filter_coeff_incr_count = transition_samples;
                }
            }
        }

        public override void fluid_iir_filter_calc(float output_rate, float fres_mod, float offset /*MPTK specific*/)
        {
            float fres;

            /* calculate the frequency of the resonant filter in Hz */
            fres = (float)fluid_conv.fluid_ct2hz(this.fres + fres_mod);
            //Debug.Log($"fluid_iir_filter_calc fres: {fres}  fres_mod: {fres_mod}  offset: {offset}");
            fres += offset; //offset: MPTK specific

            /* FIXME - Still potential for a click during turn on, can we interpolate
               between 20khz cutoff and 0 Q? */

            /* I removed the optimization of turning the filter off when the
             * resonance frequence is above the maximum frequency. Instead, the
             * filter frequency is set to a maximum of 0.45 times the sampling
             * rate. For a 44100 kHz sampling rate, this amounts to 19845
             * Hz. The reason is that there were problems with anti-aliasing when the
             * synthesizer was run at lower sampling rates. Thanks to Stephan
             * Tassart for pointing me to this bug. By turning the filter on and
             * clipping the maximum filter frequency at 0.45*srate, the filter
             * is used as an anti-aliasing filter. */

            if (fres > 0.45f * output_rate)
            {
                fres = 0.45f * output_rate;
            }
            else if (fres < 5f)
            {
                fres = 5f;
            }

            /* if filter enabled and there is a significant frequency change.. */
            if (type != fluid_iir_filter_type.FLUID_IIR_DISABLED && Mathf.Abs(fres - last_fres) > 0.01f)
            {
                /* The filter coefficients have to be recalculated (filter
                 * parameters have changed). Recalculation for various reasons is
                 * forced by setting last_fres to -1.  The flag filter_startup
                 * indicates, that the DSP loop runs for the first time, in this
                 * case, the filter is set directly, instead of smoothly fading
                 * between old and new settings. */
                last_fres = fres;
                fluid_iir_filter_calculate_coefficients(FLUID_BUFSIZE, output_rate);
            }
        }
    }
}
