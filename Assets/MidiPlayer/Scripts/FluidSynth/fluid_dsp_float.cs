/* FluidSynth - A Software Synthesizer
 *
 * Copyright (C) 2003  Peter Hanappe and others.
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Library General Public License
 * as published by the Free Software Foundation; either version 2 of
 * the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Library General Public License for more details.
 *
 * You should have received a copy of the GNU Library General Public
 * License along with this library; if not, write to the Free
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA
 * 02111-1307, USA
 */

/* Purpose:
 *
 * Interpolates audio data (obtains values between the samples of the original
 * waveform data).
 *
 * Variables loaded from the voice structure (assigned in fluid_voice_write()):
 * - dsp_data: Pointer to the original waveform data
 * - dsp_phase: The position in the original waveform data.
 *              This has an integer and a fractional part (between samples).
 * - dsp_phase_incr: For each output sample, the position in the original
 *              waveform advances by dsp_phase_incr. This also has an integer
 *              part and a fractional part.
 *              If a sample is played at root pitch (no pitch change),
 *              dsp_phase_incr is integer=1 and fractional=0.
 * - dsp_amp: The current amplitude envelope value.
 * - dsp_amp_incr: The changing rate of the amplitude envelope.
 *
 * A couple of variables are used internally, their results are discarded:
 * - dsp_i: Index through the output buffer
 * - dsp_buf: Output buffer of floating point values (fluid_bufsize in length)
 */
using UnityEngine;

namespace MidiPlayerTK
{

    public class fluid_dsp_float
    {

        const int SINC_INTERP_ORDER = 7;    /* 7th order constant */

        const int FLUID_INTERP_BITS = 8;
        const uint FLUID_INTERP_BITS_MASK = 0xff000000;
        const int FLUID_INTERP_BITS_SHIFT = 24;
        const int FLUID_INTERP_MAX = 256;

        const float FLUID_FRACT_MAX = 4294967296f;

        /* Interpolation (find a value between two samples of the original waveform) */

        /* Linear interpolation table (2 coefficients centered on 1st) */
        static float[][] interp_coeff_linear;//[FLUID_INTERP_MAX,2];

        /* 4th order (cubic) interpolation table (4 coefficients centered on 2nd) */
        static float[][] interp_coeff;

        /* 7th order interpolation (7 coefficients centered on 3rd) */
        static float[][] sinc_table7;

        /* Initializes interpolation tables */
        public static void fluid_dsp_float_config()
        {
            //Debug.Log("Init DSP 32");
            int i, i2;
            float x, v;
            float i_shifted;

            interp_coeff_linear = new float[FLUID_INTERP_MAX][];//2
            interp_coeff = new float[FLUID_INTERP_MAX][];//4
            sinc_table7 = new float[FLUID_INTERP_MAX][];//7

            /* Initialize the coefficients for the interpolation. The math comes
             * from a mail, posted by Olli Niemitalo to the music-dsp mailing
             * list (I found it in the music-dsp archives
             * http://www.smartelectronix.com/musicdsp/).  */

            for (i = 0; i < FLUID_INTERP_MAX; i++)
            {
                x = (float)i / (float)FLUID_INTERP_MAX;

                interp_coeff_linear[i] = new float[2];
                interp_coeff_linear[i][0] = 1f - x;
                interp_coeff_linear[i][1] = x;

                interp_coeff[i] = new float[4];
                interp_coeff[i][0] = x * (-0.5f + x * (1f - 0.5f * x));
                interp_coeff[i][1] = 1f + x * x * (1.5f * x - 2.5f);
                interp_coeff[i][2] = x * (0.5f + x * (2f - 1f * x));
                interp_coeff[i][3] = 0.5f * x * x * (x - 1f);

                sinc_table7[i] = new float[7];
            }

            /* i: Offset in terms of whole samples */
            for (i = 0; i < SINC_INTERP_ORDER; i++)
            {
                /* i2: Offset in terms of fractional samples ('subsamples') */
                for (i2 = 0; i2 < FLUID_INTERP_MAX; i2++)
                {
                    /* center on middle of table */
                    i_shifted = (float)i - ((float)SINC_INTERP_ORDER / 2f) + (float)i2 / (float)FLUID_INTERP_MAX;

                    /* sinc(0) cannot be calculated straightforward (limit needed for 0/0) */
                    if (Mathf.Abs(i_shifted) > 0.000001f)
                    {
                        v = Mathf.Sin(i_shifted * fluid_voice.M_PI) / (fluid_voice.M_PI * i_shifted);
                        /* Hamming window */
                        v *= 0.5f * (1f + Mathf.Cos(2f * fluid_voice.M_PI * i_shifted / (float)SINC_INTERP_ORDER));
                    }
                    else
                        v = 1f;

                    sinc_table7[FLUID_INTERP_MAX - i2 - 1][i] = v;
                }
            }

            //#if 0
            //  for (i = 0; i < FLUID_INTERP_MAX; i++)
            //  {
            //    printf ("%d %0.3f %0.3f %0.3f %0.3f %0.3f %0.3f %0.3f\n",
            //	    i, sinc_table7[0,i], sinc_table7[1,i], sinc_table7[2,i],
            //	    sinc_table7[3,i], sinc_table7[4,i], sinc_table7[5,i], sinc_table7[6,i]);
            //  }
            //#endif

            //fluid_check_fpe("interpolation table calculation");
        }



        // No interpolation. Just take the sample, which is closest to the playback pointer.  
        //  Questionable quality, but very efficient. 
        public static int fluid_dsp_float_interpolate_none(fluid_voice voice)
        {
            ulong dsp_phase = voice.phase;
            ulong dsp_phase_incr;
            float[] dsp_data = voice.sample.Data;
            float[] dsp_buf = voice.dsp_buf;
            float dsp_amp = voice.amp;
            float dsp_amp_incr = voice.amp_incr;
            uint dsp_i = 0;
            uint dsp_phase_index;
            uint end_index;
            int fluid_bufsize = voice.synth.FLUID_BUFSIZE;

            //Debug.Log("fluid_dsp_float_interpolate_none");

            /* Convert playback "speed" floating point value to phase index/fract
               Sets the phase a to a phase increment given in b.
               For example, assume b is 0.9. After setting a to it, adding a to the playing pointer will advance it by 0.9 samples.
               #define fluid_phase_set_float(a, b)  (a) = (((unsigned long long)(b)) << 32) | (uint32) (((double)(b) - (int)(b)) * (double)FLUID_FRACT_MAX)
               fluid_phase_set_float (dsp_phase_incr, phase_incr);
            */
            dsp_phase_incr = (((ulong)voice.phase_incr) << 32) |
                (uint)(((double)(voice.phase_incr) - (int)(voice.phase_incr)) * (double)FLUID_FRACT_MAX);

            end_index = voice.is_looping ? (uint)voice.loopend - 1 : (uint)voice.end;

            while (true)
            {
                /*
                  Get the phase index with fractional rounding
                  #define fluid_phase_index_round(_x)  ((uint)(((_x) + 0x80000000) >> 32))
                  dsp_phase_index = fluid_phase_index_round(dsp_phase);	/* round to nearest point
                */

                dsp_phase_index = ((uint)((dsp_phase + 0x80000000) >> 32));

                //Debug.LogFormat("   fluid_dsp_float_interpolate_none dsp_i:{0} dsp_phase_index:{1}  end_index:{2} loopend:{3} end:{4} synth.fluid_bufsize:{5} sample.Data.Length:{6}",
                //    dsp_i, dsp_phase_index, end_index, loopend, end, synth.fluid_bufsize, sample.Data.Length);

                /* interpolate sequence of sample points */
                for (; dsp_i < fluid_bufsize && dsp_phase_index <= end_index; dsp_i++)
                {
                    //Debug.LogFormat("dsp_i:{0} phase_incr:{1,0:F7}  dsp_phase_index:{2} amp:{3,0:F7}  dsp_buf:{4,0:F7} phase:{5}",dsp_i, dsp_phase_incr, dsp_phase_index, dsp_amp, dsp_buf[dsp_i], dsp_phase);

                    dsp_buf[dsp_i] = dsp_amp * dsp_data[dsp_phase_index];

                    // increment phase and amplitude
                    // Advance a by a step of b (both are ulong).
                    // #define fluid_phase_incr(a, b)  a += b
                    // fluid_phase_incr (dsp_phase, dsp_phase_incr); //

                    dsp_phase += dsp_phase_incr;

                    //dsp_phase_index = fluid_phase_index_round(dsp_phase);	/* round to nearest point */
                    dsp_phase_index = ((uint)((dsp_phase + 0x80000000) >> 32));

                    dsp_amp += dsp_amp_incr;
                }

                /* break out if not looping (buffer may not be full) */
                if (!voice.is_looping) break;

                /* go back to loop start */
                if (dsp_phase_index > end_index)
                {
                    // Purpose:
                    // Subtract b samples from a.
                    // #define fluid_phase_sub_int(a, b)  ((a) -= (unsigned long long)(b) << 32)
                    // fluid_phase_sub_int (dsp_phase, loopend - loopstart);
                    dsp_phase -= ((ulong)(voice.loopend - voice.loopstart)) << 32;
                    voice.has_looped = true;
                    //Debug.LogFormat("   return to start:{0} end_index:{1} ", ((uint)((dsp_phase + 0x80000000) >> 32)), end_index);

                }

                /* break out if filled buffer */
                if (dsp_i >= fluid_bufsize) break;
            }

            voice.phase = dsp_phase;
            voice.amp = dsp_amp;

            return (int)dsp_i;
        }


        /* Straight line interpolation.
         * Returns number of samples processed (usually fluid_bufsize but could be
         * smaller if end of sample occurs).
         */
        public static int fluid_dsp_float_interpolate_linear(fluid_voice voice)
        {
            ulong dsp_phase = voice.phase;
            ulong dsp_phase_incr;
            float[] dsp_data = voice.sample.Data;
            float[] dsp_buf = voice.dsp_buf;
            float dsp_amp = voice.amp;
            float dsp_amp_incr = voice.amp_incr;
            uint dsp_i = 0;
            uint dsp_phase_index;
            uint end_index;
            float point;
            float[] coeffs;
            int fluid_bufsize = voice.synth.FLUID_BUFSIZE;

            //Debug.Log("fluid_dsp_float_interpolate_linear");

            /* Convert playback "speed" floating point value to phase index/fract 
               fluid_phase_set_float (dsp_phase_incr, voice->phase_incr);
             */
            dsp_phase_incr = (((ulong)voice.phase_incr) << 32) |
              (uint)(((double)(voice.phase_incr) - (int)(voice.phase_incr)) * (double)FLUID_FRACT_MAX);


            //Debug.LogFormat("looping:{0} ", looping);

            /* last index before 2nd interpolation point must be specially handled */
            end_index = (voice.is_looping ? (uint)voice.loopend - 1 : (uint)voice.end) - 1;

            /* 2nd interpolation point to use at end of loop or sample */
            point = voice.is_looping ? dsp_data[voice.loopstart] : dsp_data[voice.end];          /* duplicate end for samples no longer looping */

            while (true)
            {
                /* Purpose: Return the index and the fractional part, respectively. 
                #define fluid_phase_index(_x)  ((unsigned int)((_x) >> 32))
                dsp_phase_index = fluid_phase_index(dsp_phase);
                */
                dsp_phase_index = ((uint)((dsp_phase) >> 32));

                /* interpolate the sequence of sample points */
                for (; dsp_i < fluid_bufsize && dsp_phase_index <= end_index; dsp_i++)
                {

                    /*Purpose:
                    *Takes the fractional part of the argument phase and
                    * calculates the corresponding position in the interpolation table.
                    * The fractional position of the playing pointer is calculated with a quite high
                    * resolution(32 bits). It would be unpractical to keep a set of interpolation
                    * coefficients for each possible fractional part...
                    #define fluid_phase_fract(_x)  ((uint32)((_x) & 0xFFFFFFFF))
                    #define fluid_phase_fract_to_tablerow(_x) ((unsigned int) (fluid_phase_fract(_x) & FLUID_INTERP_BITS_MASK) >> FLUID_INTERP_BITS_SHIFT)
                    fluid_phase_fract_to_tablerow(dsp_phase)
                    */

                    uint tablerow = ((uint)(((uint)((dsp_phase) & 0xFFFFFFFF)) & FLUID_INTERP_BITS_MASK) >> FLUID_INTERP_BITS_SHIFT);

                    coeffs = interp_coeff_linear[tablerow];

                    dsp_buf[dsp_i] = dsp_amp * (coeffs[0] * dsp_data[dsp_phase_index] + coeffs[1] * dsp_data[dsp_phase_index + 1]);

                    /* increment phase and amplitude */
                    //fluid_phase_incr(dsp_phase, dsp_phase_incr);
                    dsp_phase += dsp_phase_incr;

                    //dsp_phase_index = fluid_phase_index(dsp_phase);
                    dsp_phase_index = ((uint)((dsp_phase) >> 32));

                    dsp_amp += dsp_amp_incr;
                }

                /* break out if buffer filled */
                if (dsp_i >= fluid_bufsize) break;

                end_index++;    /* we're now interpolating the last point */

                /* interpolate within last point */
                for (; dsp_phase_index <= end_index && dsp_i < fluid_bufsize; dsp_i++)
                {

                    /*Purpose:
                     *Takes the fractional part of the argument phase and
                     * calculates the corresponding position in the interpolation table.
                     * The fractional position of the playing pointer is calculated with a quite high
                     * resolution(32 bits). It would be unpractical to keep a set of interpolation
                     * coefficients for each possible fractional part...
                     #define fluid_phase_fract(_x)  ((uint32)((_x) & 0xFFFFFFFF))
                     #define fluid_phase_fract_to_tablerow(_x) ((unsigned int) (fluid_phase_fract(_x) & FLUID_INTERP_BITS_MASK) >> FLUID_INTERP_BITS_SHIFT)
                     */

                    //coeffs = interp_coeff_linear[fluid_phase_fract_to_tablerow(dsp_phase)];
                    uint tablerow = ((uint)(((uint)((dsp_phase) & 0xFFFFFFFF)) & FLUID_INTERP_BITS_MASK) >> FLUID_INTERP_BITS_SHIFT);
                    coeffs = interp_coeff_linear[tablerow];

                    dsp_buf[dsp_i] = dsp_amp * (coeffs[0] * dsp_data[dsp_phase_index] + coeffs[1] * point);

                    /* increment phase and amplitude */
                    //fluid_phase_incr(dsp_phase, dsp_phase_incr);
                    dsp_phase += dsp_phase_incr;

                    //dsp_phase_index = fluid_phase_index(dsp_phase);
                    dsp_phase_index = ((uint)((dsp_phase) >> 32));

                    dsp_amp += dsp_amp_incr;    /* increment amplitude */
                }

                if (!voice.is_looping) break;    /* break out if not looping (end of sample) */

                /* go back to loop start (if past */
                if (dsp_phase_index > end_index)
                {
                    /* Purpose: Subtract b samples from a.
                       #define fluid_phase_sub_int(a, b)  ((a) -= (unsigned long long)(b) << 32)
                       fluid_phase_sub_int(dsp_phase, voice.loopend - voice.loopstart);
                    */
                    //Debug.LogFormat("has_looped end_index:{0} ", end_index);
                    dsp_phase -= ((ulong)(voice.loopend - voice.loopstart)) << 32;
                    voice.has_looped = true;
                }

                /* break out if filled buffer */
                if (dsp_i >= fluid_bufsize) break;

                end_index--;    /* set end back to second to last sample point */
            }

            voice.phase = dsp_phase;
            voice.amp = dsp_amp;

            return (int)dsp_i;
        }

        /* 4th order (cubic) interpolation.
         * Returns number of samples processed (usually fluid_bufsize but could be
         * smaller if end of sample occurs).
         */
        public static int fluid_dsp_float_interpolate_4th_order(fluid_voice voice)
        {
            ulong dsp_phase = voice.phase;
            ulong dsp_phase_incr;//, end_phase;
            float[] dsp_data = voice.sample.Data;
            float[] dsp_buf = voice.dsp_buf;
            float dsp_amp = voice.amp;
            float dsp_amp_incr = voice.amp_incr;
            uint dsp_i = 0;
            uint dsp_phase_index;
            uint start_index, end_index;
            float start_point, end_point1, end_point2;
            float[] coeffs;
            int fluid_bufsize = voice.synth.FLUID_BUFSIZE;

            /* Convert playback "speed" floating point value to phase index/fract */
            //fluid_phase_set_float(dsp_phase_incr, voice.phase_incr);
            dsp_phase_incr = (((ulong)voice.phase_incr) << 32) |
             (uint)(((double)(voice.phase_incr) - (int)(voice.phase_incr)) * (double)FLUID_FRACT_MAX);

            /* last index before 4th interpolation point must be specially handled */
            end_index = (uint)(voice.is_looping ? voice.loopend - 1 : voice.end) - 2;

            if (voice.has_looped)  /* set start_index and start point if looped or not */
            {
                start_index = (uint)voice.loopstart;
                start_point = dsp_data[voice.loopend - 1]; /* last point in loop (wrap around) */
            }
            else
            {
                start_index = (uint)voice.start;
                start_point = dsp_data[voice.start];   /* just duplicate the point */
            }

            /* get points off the end (loop start if looping, duplicate point if end) */
            if (voice.is_looping)
            {
                end_point1 = dsp_data[voice.loopstart];
                end_point2 = dsp_data[voice.loopstart + 1];
            }
            else
            {
                end_point1 = dsp_data[voice.end];
                end_point2 = end_point1;
            }

            while (true)
            {
                //dsp_phase_index = fluid_phase_index(dsp_phase);
                dsp_phase_index = ((uint)((dsp_phase) >> 32));

                /* interpolate first sample point (start or loop start) if needed */
                for (; dsp_phase_index == start_index && dsp_i < fluid_bufsize; dsp_i++)
                {
                    uint tablerow = (((uint)((dsp_phase) & 0xFFFFFFFF)) & FLUID_INTERP_BITS_MASK) >> FLUID_INTERP_BITS_SHIFT;

                    coeffs = interp_coeff[tablerow];
                    dsp_buf[dsp_i] = dsp_amp * (coeffs[0] * start_point
                                + coeffs[1] * dsp_data[dsp_phase_index]
                                + coeffs[2] * dsp_data[dsp_phase_index + 1]
                                + coeffs[3] * dsp_data[dsp_phase_index + 2]);

                    /* increment phase and amplitude */
                    //fluid_phase_incr(dsp_phase, dsp_phase_incr);
                    dsp_phase += dsp_phase_incr;

                    //dsp_phase_index = fluid_phase_index(dsp_phase);
                    dsp_phase_index = ((uint)((dsp_phase) >> 32));

                    dsp_amp += dsp_amp_incr;
                }

                /* interpolate the sequence of sample points */
                for (; dsp_i < fluid_bufsize && dsp_phase_index <= end_index; dsp_i++)
                {
                    uint tablerow = ((uint)(((uint)((dsp_phase) & 0xFFFFFFFF)) & FLUID_INTERP_BITS_MASK) >> FLUID_INTERP_BITS_SHIFT);

                    coeffs = interp_coeff[tablerow];
                    dsp_buf[dsp_i] = dsp_amp * (coeffs[0] * dsp_data[dsp_phase_index - 1]
                        + coeffs[1] * dsp_data[dsp_phase_index]
                        + coeffs[2] * dsp_data[dsp_phase_index + 1]
                        + coeffs[3] * dsp_data[dsp_phase_index + 2]);

                    /* increment phase and amplitude */
                    //fluid_phase_incr(dsp_phase, dsp_phase_incr);
                    dsp_phase += dsp_phase_incr;

                    //dsp_phase_index = fluid_phase_index(dsp_phase);
                    dsp_phase_index = ((uint)((dsp_phase) >> 32));

                    dsp_amp += dsp_amp_incr;
                }

                /* break out if buffer filled */
                if (dsp_i >= fluid_bufsize) break;

                end_index++;    /* we're now interpolating the 2nd to last point */

                /* interpolate within 2nd to last point */
                for (; dsp_phase_index <= end_index && dsp_i < fluid_bufsize; dsp_i++)
                {
                    uint tablerow = ((uint)(((uint)((dsp_phase) & 0xFFFFFFFF)) & FLUID_INTERP_BITS_MASK) >> FLUID_INTERP_BITS_SHIFT);
                    coeffs = interp_coeff[tablerow];
                    dsp_buf[dsp_i] = dsp_amp * (coeffs[0] * dsp_data[dsp_phase_index - 1]
                                + coeffs[1] * dsp_data[dsp_phase_index]
                                + coeffs[2] * dsp_data[dsp_phase_index + 1]
                                + coeffs[3] * end_point1);

                    /* increment phase and amplitude */
                    //fluid_phase_incr(dsp_phase, dsp_phase_incr);
                    dsp_phase += dsp_phase_incr;

                    //dsp_phase_index = fluid_phase_index(dsp_phase);
                    dsp_phase_index = ((uint)((dsp_phase) >> 32));

                    dsp_amp += dsp_amp_incr;
                }

                end_index++;    /* we're now interpolating the last point */

                /* interpolate within the last point */
                for (; dsp_phase_index <= end_index && dsp_i < fluid_bufsize; dsp_i++)
                {
                    uint tablerow = ((uint)(((uint)((dsp_phase) & 0xFFFFFFFF)) & FLUID_INTERP_BITS_MASK) >> FLUID_INTERP_BITS_SHIFT);
                    coeffs = interp_coeff[tablerow];
                    dsp_buf[dsp_i] = dsp_amp * (coeffs[0] * dsp_data[dsp_phase_index - 1]
                                + coeffs[1] * dsp_data[dsp_phase_index]
                                + coeffs[2] * end_point1
                                + coeffs[3] * end_point2);

                    /* increment phase and amplitude */
                    //fluid_phase_incr(dsp_phase, dsp_phase_incr);
                    dsp_phase += dsp_phase_incr;

                    //dsp_phase_index = fluid_phase_index(dsp_phase);
                    dsp_phase_index = ((uint)((dsp_phase) >> 32));

                    dsp_amp += dsp_amp_incr;
                }

                if (!voice.is_looping) break;    /* break out if not looping (end of sample) */

                /* go back to loop start */
                if (dsp_phase_index > end_index)
                {
                    //fluid_phase_sub_int(dsp_phase, voice.loopend - voice.loopstart);
                    dsp_phase -= ((ulong)(voice.loopend - voice.loopstart)) << 32;

                    if (!voice.has_looped)
                    {
                        //Debug.LogFormat("has_looped end_index:{0} ", end_index);
                        voice.has_looped = true;
                        start_index = (uint)voice.loopstart;
                        start_point = dsp_data[voice.loopend - 1];
                    }
                }

                /* break out if filled buffer */
                if (dsp_i >= fluid_bufsize) break;

                end_index -= 2; /* set end back to third to last sample point */
            }

            voice.phase = dsp_phase;
            voice.amp = dsp_amp;

            return (int)dsp_i;
        }

        /* 7th order interpolation.
         * Returns number of samples processed (usually fluid_bufsize but could be
         * smaller if end of sample occurs).
         */
        public static int fluid_dsp_float_interpolate_7th_order(fluid_voice voice)
        {
            ulong dsp_phase = voice.phase;
            ulong dsp_phase_incr;//, end_phase;
            float[] dsp_data = voice.sample.Data;
            float[] dsp_buf = voice.dsp_buf;
            float dsp_amp = voice.amp;
            float dsp_amp_incr = voice.amp_incr;
            uint dsp_i = 0;
            uint dsp_phase_index;
            uint start_index, end_index;
            float start_points0;
            float start_points1;
            float start_points2;
            float end_points0;
            float end_points1;
            float end_points2;
            float[] coeffs;
            int fluid_bufsize = voice.synth.FLUID_BUFSIZE;

            /* Convert playback "speed" floating point value to phase index/fract
               fluid_phase_set_float(dsp_phase_incr, voice.phase_incr);
            */
            dsp_phase_incr = (((ulong)voice.phase_incr) << 32) |
                (uint)(((double)(voice.phase_incr) - (int)(voice.phase_incr)) * (double)FLUID_FRACT_MAX);

            /* add 1/2 sample to dsp_phase since 7th order interpolation is centered on
             * the 4th sample point 
              fluid_phase_incr(dsp_phase, (ulong)0x80000000);
            */
            dsp_phase += 0x80000000;

            /* last index before 7th interpolation point must be specially handled */
            end_index = (uint)(voice.is_looping ? voice.loopend - 1 : voice.end) - 3;

            if (voice.has_looped)  /* set start_index and start point if looped or not */
            {
                start_index = (uint)voice.loopstart;
                start_points0 = dsp_data[voice.loopend - 1];
                start_points1 = dsp_data[voice.loopend - 2];
                start_points2 = dsp_data[voice.loopend - 3];
            }
            else
            {
                start_index = (uint)voice.start;
                start_points0 = dsp_data[voice.start];   /* just duplicate the start point */
                start_points1 = start_points0;
                start_points2 = start_points0;
            }

            /* get the 3 points off the end (loop start if looping, duplicate point if end) */
            if (voice.is_looping)
            {
                end_points0 = dsp_data[voice.loopstart];
                end_points1 = dsp_data[voice.loopstart + 1];
                end_points2 = dsp_data[voice.loopstart + 2];
            }
            else
            {
                end_points0 = dsp_data[voice.end];
                end_points1 = end_points0;
                end_points2 = end_points0;
            }

            while (true)
            {
                //dsp_phase_index = fluid_phase_index(dsp_phase);
                dsp_phase_index = ((uint)((dsp_phase) >> 32));

                /* interpolate first sample point (start or loop start) if needed */
                for (; dsp_phase_index == start_index && dsp_i < fluid_bufsize; dsp_i++)
                {
                    uint tablerow = ((uint)(((uint)((dsp_phase) & 0xFFFFFFFF)) & FLUID_INTERP_BITS_MASK) >> FLUID_INTERP_BITS_SHIFT);

                    coeffs = sinc_table7[tablerow];

                    dsp_buf[dsp_i] = dsp_amp
                    * (coeffs[0] * (float)start_points2
                     + coeffs[1] * (float)start_points1
                     + coeffs[2] * (float)start_points0
                     + coeffs[3] * (float)dsp_data[dsp_phase_index]
                     + coeffs[4] * (float)dsp_data[dsp_phase_index + 1]
                     + coeffs[5] * (float)dsp_data[dsp_phase_index + 2]
                     + coeffs[6] * (float)dsp_data[dsp_phase_index + 3]);

                    /* increment phase and amplitude */
                    //fluid_phase_incr(dsp_phase, dsp_phase_incr);
                    dsp_phase += dsp_phase_incr;

                    //dsp_phase_index = fluid_phase_index(dsp_phase);
                    dsp_phase_index = ((uint)((dsp_phase) >> 32));

                    dsp_amp += dsp_amp_incr;
                }

                start_index++;

                /* interpolate 2nd to first sample point (start or loop start) if needed */
                for (; dsp_phase_index == start_index && dsp_i < fluid_bufsize; dsp_i++)
                {
                    uint tablerow = ((uint)(((uint)((dsp_phase) & 0xFFFFFFFF)) & FLUID_INTERP_BITS_MASK) >> FLUID_INTERP_BITS_SHIFT);
                    coeffs = sinc_table7[tablerow];

                    dsp_buf[dsp_i] = dsp_amp
                    * (coeffs[0] * (float)start_points1
                     + coeffs[1] * (float)start_points0
                     + coeffs[2] * (float)dsp_data[dsp_phase_index - 1]
                     + coeffs[3] * (float)dsp_data[dsp_phase_index]
                     + coeffs[4] * (float)dsp_data[dsp_phase_index + 1]
                     + coeffs[5] * (float)dsp_data[dsp_phase_index + 2]
                     + coeffs[6] * (float)dsp_data[dsp_phase_index + 3]);

                    /* increment phase and amplitude */
                    //fluid_phase_incr(dsp_phase, dsp_phase_incr);
                    dsp_phase += dsp_phase_incr;

                    //dsp_phase_index = fluid_phase_index(dsp_phase);
                    dsp_phase_index = ((uint)((dsp_phase) >> 32));

                    dsp_amp += dsp_amp_incr;
                }

                start_index++;

                /* interpolate 3rd to first sample point (start or loop start) if needed */
                for (; dsp_phase_index == start_index && dsp_i < fluid_bufsize; dsp_i++)
                {
                    uint tablerow = ((uint)(((uint)((dsp_phase) & 0xFFFFFFFF)) & FLUID_INTERP_BITS_MASK) >> FLUID_INTERP_BITS_SHIFT);
                    coeffs = sinc_table7[tablerow];

                    dsp_buf[dsp_i] = dsp_amp
                    * (coeffs[0] * (float)start_points0
                     + coeffs[1] * (float)dsp_data[dsp_phase_index - 2]
                     + coeffs[2] * (float)dsp_data[dsp_phase_index - 1]
                     + coeffs[3] * (float)dsp_data[dsp_phase_index]
                     + coeffs[4] * (float)dsp_data[dsp_phase_index + 1]
                     + coeffs[5] * (float)dsp_data[dsp_phase_index + 2]
                     + coeffs[6] * (float)dsp_data[dsp_phase_index + 3]);

                    /* increment phase and amplitude */
                    //fluid_phase_incr(dsp_phase, dsp_phase_incr);
                    dsp_phase += dsp_phase_incr;

                    //dsp_phase_index = fluid_phase_index(dsp_phase);
                    dsp_phase_index = ((uint)((dsp_phase) >> 32));

                    dsp_amp += dsp_amp_incr;
                }

                start_index -= 2;   /* set back to original start index */


                /* interpolate the sequence of sample points */
                for (; dsp_i < fluid_bufsize && dsp_phase_index <= end_index; dsp_i++)
                {
                    uint tablerow = ((uint)(((uint)((dsp_phase) & 0xFFFFFFFF)) & FLUID_INTERP_BITS_MASK) >> FLUID_INTERP_BITS_SHIFT);
                    coeffs = sinc_table7[tablerow];

                    dsp_buf[dsp_i] = dsp_amp
                               * (coeffs[0] * (float)dsp_data[dsp_phase_index - 3]
                                + coeffs[1] * (float)dsp_data[dsp_phase_index - 2]
                                + coeffs[2] * (float)dsp_data[dsp_phase_index - 1]
                                + coeffs[3] * (float)dsp_data[dsp_phase_index]
                                + coeffs[4] * (float)dsp_data[dsp_phase_index + 1]
                                + coeffs[5] * (float)dsp_data[dsp_phase_index + 2]
                                + coeffs[6] * (float)dsp_data[dsp_phase_index + 3]);

                    /* increment phase and amplitude */
                    //fluid_phase_incr(dsp_phase, dsp_phase_incr);
                    dsp_phase += dsp_phase_incr;

                    //dsp_phase_index = fluid_phase_index(dsp_phase);
                    dsp_phase_index = ((uint)((dsp_phase) >> 32));

                    dsp_amp += dsp_amp_incr;
                }

                /* break out if buffer filled */
                if (dsp_i >= fluid_bufsize) break;

                end_index++;    /* we're now interpolating the 3rd to last point */

                /* interpolate within 3rd to last point */
                for (; dsp_phase_index <= end_index && dsp_i < fluid_bufsize; dsp_i++)
                {
                    uint tablerow = ((uint)(((uint)((dsp_phase) & 0xFFFFFFFF)) & FLUID_INTERP_BITS_MASK) >> FLUID_INTERP_BITS_SHIFT);
                    coeffs = sinc_table7[tablerow];

                    dsp_buf[dsp_i] = dsp_amp
                    * (coeffs[0] * (float)dsp_data[dsp_phase_index - 3]
                     + coeffs[1] * (float)dsp_data[dsp_phase_index - 2]
                     + coeffs[2] * (float)dsp_data[dsp_phase_index - 1]
                     + coeffs[3] * (float)dsp_data[dsp_phase_index]
                     + coeffs[4] * (float)dsp_data[dsp_phase_index + 1]
                     + coeffs[5] * (float)dsp_data[dsp_phase_index + 2]
                     + coeffs[6] * (float)end_points0);

                    /* increment phase and amplitude */
                    //fluid_phase_incr(dsp_phase, dsp_phase_incr);
                    dsp_phase += dsp_phase_incr;

                    //dsp_phase_index = fluid_phase_index(dsp_phase);
                    dsp_phase_index = ((uint)((dsp_phase) >> 32));

                    dsp_amp += dsp_amp_incr;
                }

                end_index++;    /* we're now interpolating the 2nd to last point */

                /* interpolate within 2nd to last point */
                for (; dsp_phase_index <= end_index && dsp_i < fluid_bufsize; dsp_i++)
                {
                    uint tablerow = ((uint)(((uint)((dsp_phase) & 0xFFFFFFFF)) & FLUID_INTERP_BITS_MASK) >> FLUID_INTERP_BITS_SHIFT);
                    coeffs = sinc_table7[tablerow];

                    dsp_buf[dsp_i] = dsp_amp
                    * (coeffs[0] * (float)dsp_data[dsp_phase_index - 3]
                     + coeffs[1] * (float)dsp_data[dsp_phase_index - 2]
                     + coeffs[2] * (float)dsp_data[dsp_phase_index - 1]
                     + coeffs[3] * (float)dsp_data[dsp_phase_index]
                     + coeffs[4] * (float)dsp_data[dsp_phase_index + 1]
                     + coeffs[5] * (float)end_points0
                     + coeffs[6] * (float)end_points1);

                    /* increment phase and amplitude */
                    //fluid_phase_incr(dsp_phase, dsp_phase_incr);
                    dsp_phase += dsp_phase_incr;

                    //dsp_phase_index = fluid_phase_index(dsp_phase);
                    dsp_phase_index = ((uint)((dsp_phase) >> 32));

                    dsp_amp += dsp_amp_incr;
                }

                end_index++;    /* we're now interpolating the last point */

                /* interpolate within last point */
                for (; dsp_phase_index <= end_index && dsp_i < fluid_bufsize; dsp_i++)
                {
                    uint tablerow = ((uint)(((uint)((dsp_phase) & 0xFFFFFFFF)) & FLUID_INTERP_BITS_MASK) >> FLUID_INTERP_BITS_SHIFT);
                    coeffs = sinc_table7[tablerow];

                    dsp_buf[dsp_i] = dsp_amp
                    * (coeffs[0] * (float)dsp_data[dsp_phase_index - 3]
                     + coeffs[1] * (float)dsp_data[dsp_phase_index - 2]
                     + coeffs[2] * (float)dsp_data[dsp_phase_index - 1]
                     + coeffs[3] * (float)dsp_data[dsp_phase_index]
                     + coeffs[4] * (float)end_points0
                     + coeffs[5] * (float)end_points1
                     + coeffs[6] * (float)end_points2);

                    /* increment phase and amplitude */
                    //fluid_phase_incr(dsp_phase, dsp_phase_incr);
                    dsp_phase += dsp_phase_incr;

                    //dsp_phase_index = fluid_phase_index(dsp_phase);
                    dsp_phase_index = ((uint)((dsp_phase) >> 32));

                    dsp_amp += dsp_amp_incr;
                }

                if (!voice.is_looping) break;    /* break out if not looping (end of sample) */

                /* go back to loop start */
                if (dsp_phase_index > end_index)
                {
                    //fluid_phase_sub_int(dsp_phase, voice.loopend - voice.loopstart);
                    dsp_phase -= ((ulong)(voice.loopend - voice.loopstart)) << 32;

                    if (!voice.has_looped)
                    {
                        //Debug.LogFormat("has_looped end_index:{0} ", end_index);
                        voice.has_looped = true;
                        start_index = (uint)voice.loopstart;
                        start_points0 = dsp_data[voice.loopend - 1];
                        start_points1 = dsp_data[voice.loopend - 2];
                        start_points2 = dsp_data[voice.loopend - 3];
                    }
                }

                /* break out if filled buffer */
                if (dsp_i >= fluid_bufsize) break;

                end_index -= 3; /* set end back to 4th to last sample point */
            }

            /* sub 1/2 sample from dsp_phase since 7th order interpolation is centered on
             * the 4th sample point (correct back to real value) 
             * Subtract b from a (both are fluid_phase_t).
             * #define fluid_phase_decr(a, b)  a -= b           
             * fluid_phase_decr(dsp_phase, (ulong)0x80000000);
             */
            dsp_phase -= 0x80000000;
            voice.phase = dsp_phase;
            voice.amp = dsp_amp;

            return (int)dsp_i;
        }
    }
}
