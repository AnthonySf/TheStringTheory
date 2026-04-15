//#define MPTK_PRO
using System;
using UnityEngine;

namespace MidiPlayerTK
{
    /// <summary>
    /// A SoundFont contains parameters for three effect types: low-pass filter, reverb, and chorus.\n
    /// These parameters can be specific to each instrument and even each voice.\n
    /// Maestro MPTK SoundFont effects are based on FluidSynth effect modules.
    /// To provide more flexibility than the raw SoundFont defaults, Maestro can increase or decrease effect impact from the inspector or by script.
    /// To summarize:
    ///     - Effects are applied individually to each voice, while defaults remain defined in the SoundFont.
    ///     - Maestro parameters can be adjusted to increase or decrease the default values set in the SoundFont.
    ///     - These adjustments are applied across the prefab, but the audible result still depends on preset-level SoundFont settings.
    ///     - Please note that these effects require additional CPU resources.
    /// See more details here: https://paxstellar.fr/sound-effects/
    /// @version Maestro Pro 
    /// @note
    ///     - Effect modules are available only in Maestro MPTK Pro.
    ///     - By default, these effects are disabled in Maestro. 
    ///     - To enable them, adjust settings in the prefab inspector (Synth Parameters / SoundFont Effect) or by script.
    ///     - For better sound quality, enabling the low-pass filter is often useful.
    /// @ingroup soundfont_effects
    /// @code
    /// // Find an MPTK prefab (also works for MidiStreamPlayer, MidiExternalPlayer, and all classes that inherit from MidiSynth).
    /// MidiFilePlayer fp = FindFirstObjectByType<MidiFilePlayer>();
    /// fp.MPTK_EffectSoundFont.EnableFilter = true;
    /// fp.MPTK_EffectSoundFont.FilterFreqOffset = 500;
    /// @endcode
    /// </summary>
    public partial class MPTKEffectSoundFont : ScriptableObject
    {
        /// <summary>@brief
        /// Applies the SoundFont low-pass filter.\n 
        /// This FluidSynth-based effect is processed independently for each voice and increases CPU usage.
        /// @version Maestro Pro 
        /// @code
        /// midiFilePlayer.MPTK_EffectSoundFont.EnableFilter = true;
        /// @endcode
        /// </summary>
        public bool EnableFilter { get => applySFFilter; set => applySFFilter = value; }

        /// <summary>
        /// Applies the SoundFont reverb effect.\n
        /// This FluidSynth-based effect is processed independently for each voice and increases CPU usage. 
        /// @version Maestro Pro 
        /// @code
        /// midiFilePlayer.MPTK_EffectSoundFont.EnableReverb = true;
        /// @endcode
        /// </summary>
        public bool EnableReverb { get => applySFReverb; set => applySFReverb = value; }

        /// <summary>
        /// Applies the SoundFont chorus effect.\n
        /// This FluidSynth-based effect is processed independently for each voice and slightly increases CPU usage. 
        /// @version Maestro Pro 
        /// @code
        /// midiFilePlayer.MPTK_EffectSoundFont.EnableChorus = true;
        /// @endcode
        /// </summary>
        public bool EnableChorus { get => applySFChorus; set => applySFChorus = value; }

        [HideInInspector, SerializeField]
        private bool applySFReverb, applySFChorus, applySFFilter = true;

        //! @cond NODOC

        private MidiSynth synth;

        public void Init(MidiSynth psynth)
        {
            synth = psynth;
            ///* Effects audio buffers */
            /* allocate the reverb module */
            fx_reverb = new float[psynth.FLUID_BUFSIZE];  // FLUID_MAX_BUFSIZE not supported, each frame must have the same length
            reverb = new fluid_revmodel(psynth.OutputRate, psynth.FLUID_BUFSIZE); // FLUID_MAX_BUFSIZE not supported, each frame must have the same length

            fx_chorus = new float[psynth.FLUID_BUFSIZE]; // FLUID_MAX_BUFSIZE not supported, each frame must have the same length
            /* allocate the chorus module */
            chorus = new fluid_chorus(psynth.OutputRate, psynth.FLUID_BUFSIZE); // FLUID_MAX_BUFSIZE not supported, each frame must have the same length
#if MPTK_PRO
            SetParamSfReverb();
            SetParamSfChorus();
#else
            reverb.fluid_revmodel_set(0xFF,
                MidiSynth.FLUID_REVERB_DEFAULT_ROOMSIZE, MidiSynth.FLUID_REVERB_DEFAULT_DAMP, 
                MidiSynth.FLUID_REVERB_DEFAULT_WIDTH, MidiSynth.FLUID_REVERB_DEFAULT_LEVEL);
            chorus.fluid_chorus_set((int)fluid_chorus.fluid_chorus_set_t.FLUID_CHORUS_SET_ALL,
                MidiSynth.FLUID_CHORUS_DEFAULT_N, MidiSynth.FLUID_CHORUS_DEFAULT_LEVEL,
                MidiSynth.FLUID_CHORUS_DEFAULT_SPEED, MidiSynth.FLUID_CHORUS_DEFAULT_DEPTH, 
                fluid_chorus.FLUID_CHORUS_DEFAULT_TYPE, MidiSynth.FLUID_CHORUS_DEFAULT_WIDTH);
#endif
        }

        fluid_revmodel reverb;
        private float[] fx_reverb;
        fluid_chorus chorus;
        private float[] fx_chorus;


        public void PrepareBufferEffect(out float[] reverb_buf, out float[] chorus_buf)
        {
            // Set up the reverb / chorus buffers only, when the effect is enabled on synth level.
            // Nonexisting buffers are detected in theDSP loop. 
            // Not sending the reverb / chorus signal saves some time in that case.
            if (EnableReverb)
            {
                Array.Clear(fx_reverb, 0, synth.FLUID_BUFSIZE);
                reverb_buf = fx_reverb;
            }
            else
                reverb_buf = null;

            if (EnableChorus)
            {
                Array.Clear(fx_chorus, 0, synth.FLUID_BUFSIZE);
                chorus_buf = fx_chorus;
            }
            else
                chorus_buf = null;
        }

        public void ProcessEffect(float[] reverb_buf, float[] chorus_buf, float[] left_buf, float[] right_buf)
        {
            /* send to reverb */
            if (EnableReverb && reverb_buf != null)
            {
                reverb.fluid_revmodel_processmix(reverb_buf, left_buf, right_buf);
            }

            /* send to chorus */
            if (EnableChorus && chorus_buf != null)
            {
                chorus.fluid_chorus_processmix(chorus_buf, left_buf, right_buf);
            }
        }

        //! @endcond

    }
}


