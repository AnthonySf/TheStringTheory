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

    /**
     * Specifies the type of filter to use for the custom IIR filter
     */
    public enum fluid_iir_filter_type
    {
        FLUID_IIR_DISABLED = 0, /**< Custom IIR filter is not operating */
        FLUID_IIR_LOWPASS, /**< Custom IIR filter is operating as low-pass filter */
        FLUID_IIR_HIGHPASS, /**< Custom IIR filter is operating as high-pass filter */
        FLUID_IIR_LAST /**< @internal Value defines the count of filter types (#fluid_iir_filter_type) @warning This symbol is not part of the public API and ABI stability guarantee and may change at any time! */
    };

    /**
     * Specifies optional settings to use for the custom IIR filter. Can be bitwise ORed.
     */
    public enum fluid_iir_filter_flags
    {
        FLUID_IIR_NOFLAGS = 0,
        FLUID_IIR_Q_LINEAR = 1 << 0, /**< The Soundfont spec requires the filter Q to be interpreted in dB. If this flag is set the filter Q is instead assumed to be in a linear range */
        FLUID_IIR_Q_ZERO_OFF = 1 << 1, /**< If this flag the filter is switched off if Q == 0 (prior to any transformation) */
        FLUID_IIR_NO_GAIN_AMP = 1 << 2 /**< The Soundfont spec requires to correct the gain of the filter depending on the filter's Q. If this flag is set the filter gain will not be corrected. */
    };

    public abstract class fluid_iir_filter
    {


        public abstract void fluid_iir_filter_apply(float[] dsp_buf, int count);

        public abstract void fluid_iir_filter_init(fluid_iir_filter_type ptype, fluid_iir_filter_flags pflags);

        public abstract void fluid_iir_filter_reset();

        public abstract void fluid_iir_filter_set_fres(float pfres);


        public abstract void fluid_iir_filter_set_q(float pq, float offset /*MPTK Specific*/);

        public abstract void fluid_iir_filter_calc(float output_rate, float fres_mod, float offset /*MPTK specific*/);

    }
}
