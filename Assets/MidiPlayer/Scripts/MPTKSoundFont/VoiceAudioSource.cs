//#define DEBUGPERF
//#define DEBUGTIME
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidiPlayerTK
{
    // Used when the synth is in legacy mode (MPTK_LegacyAudioSource) and each sample are played by a dedicated AudioSource component.
    public class VoiceAudioSource : MonoBehaviour
    {
        public MidiSynth synth;
        public fluid_voice fluidvoice;
        public AudioSource Audiosource;
        public AudioLowPassFilter LowPassFilter;
        public AudioReverbFilter ReverbFilter;
        public AudioChorusFilter ChorusFilter;

        public void Awake()
        {
            //Debug.Log("Awake fluid_voice");
            if (Audiosource == null)
                Debug.LogWarning("No AudioSource attached to the VoiceAudioSource. Check VoiceAudioSource in your Hierarchy.");
        }

        public void Start()
        {
            //Debug.Log("Start fluid_voice");
        }

        // From fluid_voice_start() fluid_voice.cs  when legacy mode is used
        public void RunUnityThread()
        {
#if DEBUGPERF
            synth.DebugPerf("After Precalculate enveloppe");
#endif
            Routine.RunCoroutine(ThreadPlayNote(), Segment.RealtimeUpdate);
#if DEBUGPERF
            synth.DebugPerf("After RunCoroutine");
#endif
        }

#if DEBUGTIME
        public int countIteration;
        public double cumulDeltaTime;
        public double averageDeltaTime;
        public double cumulProcessTime;
        public double averageProcessTime;
        private double startProcessTime;
#endif

        protected IEnumerator<float> ThreadPlayNote()
        {
            //#if DEBUGPERF
            //            synth.DebugPerf("Before Audiosource.Play");
            //#endif
            // A single tick represents one hundred nanoseconds or one ten-millionth of a second.
            // There are 10,000 ticks in a millisecond, or 10 million ticks in a second. 
            //fluidvoice.LastTimeWrite = fluidvoice.TimeAtStart;

            if (Audiosource != null && Audiosource.gameObject.activeInHierarchy)
            {
                float last_pitch = -9999999, last_pan = -9999999;
                Audiosource.volume = fluidvoice.StartVolume;
                //Audiosource.loop = fluidvoice.IsLoop;
                Audiosource.loop = fluidvoice.samplemode == fluid_loop.FLUID_LOOP_UNTIL_RELEASE || fluidvoice.samplemode == fluid_loop.FLUID_LOOP_DURING_RELEASE;
                last_pan = -1;// Audiosource.panStereo = !synth.MPTK_EnablePanChange ? 0f : Mathf.Lerp(-1f, 1f, (fluidvoice.pan + 500f) / 1000f);
                last_pitch = -1; // Audiosource.pitch = Mathf.Pow(fluid_voice._ratioHalfTone, (fluidvoice.pitch - fluidvoice.root_pitch) / 100f);

                if (synth.VerboseVoice) Debug.LogFormat("   fluid_voice_start Audiosource volume:{0:0.000} loop:{1} pan:{2:0.000} pitch:{3:0.000} {4}",
                        Audiosource.volume, Audiosource.loop, Audiosource.panStereo, Audiosource.pitch, synth.MPTK_WeakDevice ? "[WEAK]" : "[FULL]");

                // Play take 0.3 ms
                yield return 0;
                
                // Guard against AudioSource being destroyed while this coroutine was waiting (thanks to Macias for pointing this out on https://discussions.unity.com/t/re-maestro-midi-player-tool-kit-good-news-for-your-rhythm-game/1707497)
                if (Audiosource != null) Audiosource.Play();

                //#if DEBUGPERF
                //                synth.DebugPerf("After Audiosource.Play");
                //#endif

#if DEBUGTIME
                cumulDeltaTime = 0d;
                countIteration = 0;
                averageDeltaTime = 0d;
                cumulProcessTime = 0d;
                averageProcessTime = 0d;
#endif

                while (fluidvoice.status != fluid_voice_status.FLUID_VOICE_OFF)
                {
#if DEBUGTIME
                    cumulDeltaTime += Time.realtimeSinceStartup * 1000d - LastTimeWrite;
                    countIteration++;
                    startProcessTime = Time.realtimeSinceStartup * 1000d;
#endif
                    if (Audiosource == null || !Audiosource.gameObject.activeInHierarchy)
                        break;
                    if (!fluidvoice.weakDevice)
                    {
                        // fluidvoice.pan is dynamically changed by fluid_voice_update_param (fluid_voice.cs)
                        if (last_pan != fluidvoice.pan)
                        {
                            last_pan = fluidvoice.pan;
                            //Audiosource.panStereo = !synth.MPTK_EnablePanChange ? 0f : Mathf.Lerp(-1f, 1f, (fluidvoice.pan + 500f) / 1000f);
                            if (synth.MPTK_EnablePanChange)
                                Audiosource.panStereo = fluidvoice.pan / 500f; ;
                            if (synth.VerboseCalcGen)
                                Debug.LogFormat($"CalcGen - EnablePanChange={synth.MPTK_EnablePanChange} last_pan={last_pan:0.00} new pan={fluidvoice.pan:0.00} --> panStereo={Audiosource.panStereo:0.00}");
                        }

                        // fluidvoice.pitch is dynamically changed by fluid_voice_update_param (fluid_voice.cs)
                        if (last_pitch != fluidvoice.pitch)
                        {
                            //Debug.Log("Pitch change");
                            last_pitch = fluidvoice.pitch;
                            Audiosource.pitch = Mathf.Pow(fluid_voice._ratioHalfTone, (fluidvoice.pitch - fluidvoice.root_pitch) / 100f);
                        }
                        fluid_voice_audiosource_write(DateTime.UtcNow.Ticks);
                    }
                    else
                        fluid_weakvoice_write(DateTime.UtcNow.Ticks);

#if DEBUGTIME
                    cumulProcessTime += Time.realtimeSinceStartup * 1000d - startProcessTime;
#endif

                    // Wait next iteration
                    yield return Routine.WaitForSeconds(0.010f); //0;
                }
            }

            try
            {
                //Debug.Log("Stop AudioSource " + Audiosource.clip.name + " vol:" + Audiosource.volume);
#if DEBUGTIME
                if (countIteration > 0)
                {
                    averageDeltaTime = cumulDeltaTime / countIteration;
                    averageProcessTime = cumulProcessTime / countIteration;
                }
#endif
                Audiosource.Stop();
            }
            catch (Exception)
            {
            }
        }

        /*
         * fluid_voice_write
         *
         * This is where it all happens. This function is called by the
         * synthesizer to generate the sound samples. The synthesizer passes
         * four audio buffers: left, right, reverb out, and chorus out.
         *
         * The biggest part of this function sets the correct values for all
         * the dsp parameters (all the control data boil down to only a few
         * dsp parameters). The dsp routine is #included in several places (fluid_dsp_core.c).
         */
        public void fluid_voice_audiosource_write(long ticks)
        {
            fluidvoice.NewTimeWrite = ticks;
            fluidvoice.DeltaTimeWrite = fluidvoice.NewTimeWrite - fluidvoice.LastTimeWrite;
            fluidvoice.LastTimeWrite = fluidvoice.NewTimeWrite;
            fluidvoice.TimeFromStart = fluidvoice.NewTimeWrite - fluidvoice.TimeAtStart;

            //Debug.Log(fluidvoice.DeltaTimeWrite / 10000f);
            if (fluidvoice.DeltaTimeWrite <= 0) return;

            //DebugEnveloppe("fluid_voice_write:" + status);
            //Debug.LogFormat("{0} TimeFromStart:{1:0.000000} DeltaTimeWrite:{2:0.000000} TimeAtStart:{3:0.000000} TimeAtEnd:{4:0.000000} {5}", 
            //   fluidvoice.IdVoice, fluidvoice.TimeFromStart/10000f, fluidvoice.DeltaTimeWrite / 10000f, fluidvoice.TimeAtStart / 10000f, fluidvoice.TimeAtEnd / 10000f, fluidvoice.modenv_section);

            if (fluidvoice.DurationTick >= 0 &&
                fluidvoice.volenv_section != fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE &&
                fluidvoice.TimeFromStart > fluidvoice.DurationTick)
            {
                //if (synth.VerboseEnveloppe) DebugEnveloppe("Over duration");
                fluidvoice.fluid_rvoice_noteoff();
            }

            //******************* volume env **********************
            //-----------------------------------------------------

            fluid_env_data env_data = fluidvoice.volenv_data[(int)fluidvoice.volenv_section];

            // skip to the next section of the envelope if necessary
            while (fluidvoice.volenv_count >= env_data.count)
            {
                // Next section
                fluidvoice.volenv_section++;
                env_data = fluidvoice.volenv_data[(int)fluidvoice.volenv_section];
                fluidvoice.volenv_count = 0;
                if (synth.VerboseEnvVolume) fluidvoice.DebugVolEnv("Next");
            }

            // calculate the envelope value and check for valid range 
            // x = env_data->coeff * voice->volenv_val + env_data->incr;

            float x = fluidvoice.volenv_val;
            if (env_data.incr > 0)
                x += ((float)fluidvoice.DeltaTimeWrite / (float)env_data.count);
            else if (env_data.incr < 0)
                x -= ((float)fluidvoice.DeltaTimeWrite / (float)env_data.count);

            //Debug.LogFormat("TimeFromStart:{0} ms Delta:{1} ms volenv_count:{2} ms count:{3} ms Ratio:{4:0.000000} modenv_val:{5,1:F3} coeff:{6} incr:{7} --> x:{8,1:F3} section:{9}",
            //   fluidvoice.TimeFromStart / fluid_voice.Nano100ToMilli, 
            //   fluidvoice.DeltaTimeWrite / fluid_voice.Nano100ToMilli,
            //   fluidvoice.volenv_count / fluid_voice.Nano100ToMilli, 
            //   env_data.count / fluid_voice.Nano100ToMilli,
            //   ((double)fluidvoice.DeltaTimeWrite / (double)env_data.count),
            //   fluidvoice.volenv_val, 
            //   env_data.coeff, 
            //   env_data.incr,
            //   x, 
            //   fluidvoice.volenv_section);

            if (x < env_data.min)
            {
                x = env_data.min;
                if (synth.VerboseEnvVolume) fluidvoice.DebugVolEnv("Min");
            }
            else if (x > env_data.max)
            {
                x = env_data.max;
                if (synth.VerboseEnvVolume) fluidvoice.DebugVolEnv("Max");
            }
            fluidvoice.volenv_val = x;
            fluidvoice.volenv_count += fluidvoice.DeltaTimeWrite;

            if (fluidvoice.volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED)
            {
                fluidvoice.fluid_voice_off();
                return;
            }

            //******************* modulation env **********************
            //---------------------------------------------------------
            ////// No mod env available with the audiosource solution

            //////if (synth.MPTK_ApplyRealTimeModulator)
            //////{
            //////    env_data = fluidvoice.modenv_data[(int)fluidvoice.modenv_section];

            //////    // skip to the next section of the envelope if necessary
            //////    while (fluidvoice.modenv_count >= env_data.count)
            //////    {
            //////        fluidvoice.modenv_section++;
            //////        env_data = fluidvoice.modenv_data[(int)fluidvoice.modenv_section];
            //////        fluidvoice.modenv_count = 0;
            //////        //Debug.LogFormat("Time:{0:0.000} Delta:{1:0.000} Count --> section:{2}  new count:{3}", TimeFromStartPlayNote, DeltaTimeWrite, (int)modenv_section, env_data.count);
            //////        if (synth.VerboseEnvModulation) fluidvoice.DebugModEnv("Next");
            //////    }

            //////    // calculate the envelope value and check for valid range
            //////    //Debug.LogFormat("Time:{0:0.000} Delta:{1:0.000} Calcul --> coeff:{2} modenv_val:{3:0.000} incr:{4} --> x:{5} section:{6}", TimeFromStartPlayNote, DeltaTimeWrite, env_data.coeff, modenv_val, env_data.incr, x, (int)modenv_section);
            //////    switch (env_data.incr)
            //////    {
            //////        case 1:
            //////            x = fluidvoice.modenv_val + ((float)fluidvoice.DeltaTimeWrite / (float)env_data.count);
            //////            break;
            //////        case -1:
            //////            x = fluidvoice.modenv_val - ((float)fluidvoice.DeltaTimeWrite / (float)env_data.count);
            //////            break;
            //////        default:
            //////            // Volume constant
            //////            x = fluidvoice.modenv_val;
            //////            break;
            //////    }

            //////    if (x < env_data.min)
            //////    {
            //////        x = env_data.min;
            //////        if (synth.VerboseEnvModulation) fluidvoice.DebugModEnv("Min");
            //////    }
            //////    else if (x > env_data.max)
            //////    {
            //////        x = env_data.max;
            //////        if (synth.VerboseEnvModulation) fluidvoice.DebugModEnv("Max");
            //////    }

            //////    fluidvoice.modenv_val = x;
            //////    fluidvoice.modenv_count += fluidvoice.DeltaTimeWrite;
            //////}

            //******************* modulation lfo **********************
            //---------------------------------------------------------
            ////// No LFO available with the audiosource solution

            //////if (synth.MPTK_ApplyModLfo)
            //////{
            //////    if (fluidvoice.TimeFromStart >= fluidvoice.modlfo_delay)
            //////    {
            //////        fluidvoice.modlfo_val += (fluidvoice.modlfo_incr * synth.LfoAmpFreq) / ((float)fluidvoice.DeltaTimeWrite / (float)fluid_voice.Nano100ToMilli);
            //////        //fluidvoice.DebugLFO("Apply modlfo_val");

            //////        if (fluidvoice.modlfo_val > 1f)
            //////        {
            //////            //fluidvoice.DebugLFO("delay modlfo_val > 1d");
            //////            //last_modlfo_val_supp_1 = TimeFromStartPlayNote;
            //////            fluidvoice.modlfo_incr = -fluidvoice.modlfo_incr;
            //////            fluidvoice.modlfo_val = 2f - fluidvoice.modlfo_val;
            //////        }
            //////        else if (fluidvoice.modlfo_val < -1f)
            //////        {
            //////            //fluidvoice.DebugLFO("modlfo_val < -1d");
            //////            fluidvoice.modlfo_incr = -fluidvoice.modlfo_incr;
            //////            fluidvoice.modlfo_val = -2f - fluidvoice.modlfo_val;
            //////        }
            //////        //DebugLFO("TimeFromStartPlayNote >= modlfo_delay");
            //////    }
            //////    //else DebugLFO("TimeFromStartPlayNote < modlfo_delay");
            //////}

            //******************* vibrato lfo **********************
            //------------------------------------------------------
            ////// No vibrato available with the audiosource solution (modulation)
            ////// as we can apply change directly on the sample play by the AudioSource
            //////if (synth.MPTK_ApplyVibLfo)
            //////{
            //////    if (fluidvoice.TimeFromStart >= fluidvoice.viblfo_delay)
            //////    {

            //////        fluidvoice.viblfo_val += (fluidvoice.viblfo_incr * synth.LfoVibFreq) / ((float)fluidvoice.DeltaTimeWrite / (float)fluid_voice.Nano100ToMilli);
            //////        //DebugVib("viblfo_delay");

            //////        if (fluidvoice.viblfo_val > 1f)
            //////        {
            //////            //DebugVib("viblfo_val > 1 freq:" + (TimeFromStartPlayNote - last_modvib_val_supp_1).ToString());
            //////            //last_modvib_val_supp_1 = TimeFromStartPlayNote;
            //////            fluidvoice.viblfo_incr = -fluidvoice.viblfo_incr;
            //////            fluidvoice.viblfo_val = 2f - fluidvoice.viblfo_val;
            //////        }
            //////        else if (fluidvoice.viblfo_val < -1f)
            //////        {
            //////            //DebugVib("viblfo_val < -1");
            //////            fluidvoice.viblfo_incr = -fluidvoice.viblfo_incr;
            //////            fluidvoice.viblfo_val = -2f - fluidvoice.viblfo_val;
            //////        }
            //////    }
            //////    //else DebugVib("TimeFromStartPlayNote < viblfo_delay");fluid_ct2hz_real
            //////    Audiosource.pitch = fluidvoice.pitch_audiosource * (1f + fluidvoice.viblfo_val * fluidvoice.viblfo_to_pitch * synth.LfoVibAmp / 2000f);
            //////    //Audiosource.pitch = (float)fluid_conv.fluid_ct2hz_real(pitch * (1d + viblfo_val * viblfo_to_pitch * synth.LfoVibAmp));
            //////}

            /******************* amplitude **********************/

            /* calculate final amplitude
             * - initial gain
             * - amplitude envelope
             */

            if (fluidvoice.volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY)
            {

                //if (synth.VerboseEnveloppe) DebugEnveloppe("volenv_section == FLUID_VOICE_ENVDELAY");
                // The volume amplitude is in delay phase. No sound is produced.
                return;
            }

            float amp;

            if (!synth.AdsrSimplified && fluidvoice.volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK)
            {
                // The envelope is in the attack section: ramp linearly to max value. A positive modlfo_to_vol should increase volume (negative attenuation).
                if (synth.MPTK_ApplyModLfo)
                    amp = (float)(fluid_conv.fluid_cb2amp(fluidvoice.attenuation) * fluid_conv.fluid_cb2amp(fluidvoice.modlfo_val * -fluidvoice.modlfo_to_vol) * fluidvoice.volenv_val) * synth.MPTK_Volume;
                else
                    amp = (float)(fluid_conv.fluid_cb2amp(fluidvoice.attenuation) * fluidvoice.volenv_val) * synth.MPTK_Volume;
            }
            else
            {
                if (fluidvoice.volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN ||
                    fluidvoice.volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE)
                {
                    if (fluidvoice.volenv_val <= 0f)
                    {
                        //if (synth.VerboseEnveloppe) DebugEnveloppe("amp <=" + synth.CutoffVolume);
                        fluidvoice.fluid_voice_off();
                        return;
                    }
                }

                //////if (synth.MPTK_ApplyModLfo)
                //////    amp = (float)(fluid_conv.fluid_atten2amp(fluidvoice.attenuation) * fluid_conv.fluid_cb2amp(960.0f * (1f - fluidvoice.volenv_val) + fluidvoice.modlfo_val * -fluidvoice.modlfo_to_vol)) * synth.MPTK_Volume;
                //////else
                amp = (float)fluid_conv.fluid_cb2amp(fluidvoice.attenuation) *
                      (float)fluid_conv.fluid_cb2amp(fluid_conv.FLUID_PEAK_ATTENUATION * (1f - fluidvoice.volenv_val)) *
                      synth.MPTK_Volume;

            }

            if (!Mathf.Approximately(Audiosource.volume, amp))
            {
                if (synth.DampVolume <= 0)
                    Audiosource.volume = amp;
                else
                {
                    float velocity = 0f;
                    Audiosource.volume = Mathf.SmoothDamp(Audiosource.volume, amp, ref velocity, synth.DampVolume * 0.001f);
                }
                if (synth.VerboseVolume) Debug.LogFormat("Volume [Id:{0}] {1} TimeFromStart:{2} Delta:{3} Att:{4,0:F2} modlfo_val:{5,0:F2} modlfo_to_vol:{6,0:F2} volenv_val:{7,0:F2} modenv_val:{8,0:F2} Amp:{9,0:F3} --> volume:{10,0:F3} {11}",
                        fluidvoice.id, "", fluidvoice.TimeFromStart / fluid_voice.Nano100ToMilli, fluidvoice.DeltaTimeWrite / fluid_voice.Nano100ToMilli,
                        fluidvoice.attenuation, fluidvoice.modlfo_val, fluidvoice.modlfo_to_vol, fluidvoice.volenv_val, fluidvoice.modenv_val, amp, Audiosource.volume, fluidvoice.volenv_section);
            }
            //Debug.LogFormat("Volume [Id:{0}] {1} TimeFromStart:{2} Delta:{3} Att:{4,0:F2} modlfo_val:{5,0:F2} modlfo_to_vol:{6,0:F2} volenv_val:{7,0:F2} modenv_val:{8,0:F2} Amp:{9,0:F3} --> volume:{10,0:F3} {11}",
            //    fluidvoice.IdVoice, "", fluidvoice.TimeFromStart / fluid_voice.Nano100ToMilli, fluidvoice.DeltaTimeWrite / fluid_voice.Nano100ToMilli,
            //    fluidvoice.attenuation, fluidvoice.modlfo_val, fluidvoice.modlfo_to_vol, fluidvoice.volenv_val, fluidvoice.modenv_val, amp, Audiosource.volume, fluidvoice.volenv_section);
            //else
            //    Debug.LogFormat("[{0,4}] {1} TimeDSP:{2:00000.000} Delta:{3:0.000} Att:{4,0:F2} modlfo_val:{5,0:F2} modlfo_to_vol:{6,0:F2} volenv_val:{7,0:F2} modenv_val:{8,0:F2} Amp:{9,0:F3} --> volume:{10,0:F3}",
            //        IdVoice, "", TimeFromStartPlayNote, DeltaTimeWrite,
            //        attenuation, modlfo_val, modlfo_to_vol, volenv_val, modenv_val, amp, Audiosource.volume);

            //if (synth.MPTK_ApplyFilter)
            //{
            //    if (LowPassFilter != null)
            //    {
            //        if (!LowPassFilter.enabled) LowPassFilter.enabled = true;
            //        calculateFilter();
            //    }
            //}
            //else if (LowPassFilter != null)
            //    if (LowPassFilter.enabled) LowPassFilter.enabled = false;

            //if (synth.MPTK_ApplyUnityReverb)
            //{
            //    if (ReverbFilter != null)
            //    {
            //        if (!ReverbFilter.enabled) ReverbFilter.enabled = true;
            //        if (synth.ReverbMix == 0f)
            //            ReverbFilter.dryLevel = Mathf.Lerp(0f, -10000f, fluidvoice.reverb_send);
            //        else
            //            ReverbFilter.dryLevel = Mathf.Lerp(0f, -10000f, synth.ReverbMix);
            //    }
            //}
            //else if (ReverbFilter != null)
            //    if (ReverbFilter.enabled) ReverbFilter.enabled = false;

            //if (synth.MPTK_ApplySFChorus)
            //{
            //    if (ChorusFilter != null)
            //    {
            //        if (!ChorusFilter.enabled) ChorusFilter.enabled = true;
            //        if (synth.ChorusMix == 0f)
            //            ChorusFilter.dryMix = fluidvoice.chorus_send;
            //        else
            //            ChorusFilter.dryMix = synth.ChorusMix;
            //    }
            //}
            //else if (ChorusFilter != null)
            //    if (ChorusFilter.enabled) ChorusFilter.enabled = false;
        }

        /// <summary>@brief
        ///  week device voice: take care only of duration and release time
        /// </summary>
        public void fluid_weakvoice_write(long ticks)
        {
            fluidvoice.DeltaTimeWrite = ticks - fluidvoice.LastTimeWrite;
            fluidvoice.LastTimeWrite = ticks;
            fluidvoice.TimeFromStart = fluidvoice.LastTimeWrite - fluidvoice.TimeAtStart;

            if (fluidvoice.DeltaTimeWrite <= 0) return;
            if (fluidvoice.volenv_section != fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE)
            {
                if (fluidvoice.DurationTick >= 0 && fluidvoice.TimeFromStart > fluidvoice.DurationTick)
                {
                    fluidvoice.fluid_rvoice_noteoff();
                }
                else
                    return;
            }

            if (Audiosource.volume <= 0f || synth.MPTK_ReleaseTimeMin <= 0)
            {
                fluidvoice.fluid_voice_off();
                return;
            }
            Audiosource.volume = Mathf.Lerp(Audiosource.volume, 0f, (float)(fluidvoice.volenv_count / synth.MPTK_ReleaseTimeMin));

            if (synth.VerboseEnvVolume)
                Debug.LogFormat("[Weak] Volume - [{0,4}] TimeFromStart:{1:0.000} Delta:{2:0.000} synth.ReleaseTimeMin:{3:0.000} volenv_count:{4:0.000} Volume:{5:0.0000}",
                    fluidvoice.id, fluidvoice.TimeFromStart / fluid_voice.Nano100ToMilli, fluidvoice.DeltaTimeWrite / fluid_voice.Nano100ToMilli, synth.MPTK_ReleaseTimeMin / fluid_voice.Nano100ToMilli, fluidvoice.volenv_count, Audiosource.volume);

            fluidvoice.volenv_count += fluidvoice.DeltaTimeWrite;

        }

        // private float calculateFilter()
        //{
        /*************** resonant filter ******************/

        /* calculate the frequency of the resonant filter in Hz */
        //float localfres = fluid_conv.fluid_ct2hz(
        //    fluidvoice.fres + fluidvoice.modlfo_val * fluidvoice.modlfo_to_fc * synth.LfoToFilterMod + fluidvoice.modenv_val * fluidvoice.modenv_to_fc * synth.FilterEnvelopeMod) + synth.FilterOffset;

        //if (synth.VerboseFilter)
        //    Debug.LogFormat("[{0,4}] {1} TimeDSP:{2:00000.000} Delta:{3:0.000} Fres:{4} modlfo_val:{5:0.000} modlfo_to_fc:{6:0.000} modenv_val:{7:0.000}  modenv_to_fc:{8:0.000} --> localfres:{9:0.000} q_lin:{10:0.000}",
        //        fluidvoice.IdVoice, "", fluidvoice.TimeFromStart, fluidvoice.DeltaTimeWrite,
        //        fluidvoice.fres, fluidvoice.modlfo_val, fluidvoice.modlfo_to_fc, fluidvoice.modenv_val, fluidvoice.modenv_to_fc, localfres, fluidvoice.q_lin * synth.FilterQMod);

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

        //if (fres > 0.45f)// * output_rate)
        //    fres = 0.45f;// * output_rate;
        //else if (fres < 5)
        //    fres = 5;

        /* if filter enabled and there is a significant frequency change.. */
        //if ((Math.Abs(localfres - fluidvoice.last_fres) > 0.01d))
        //{
        //    if (LowPassFilter != null)
        //    {
        //        LowPassFilter.cutoffFrequency = (float)localfres;
        //        LowPassFilter.lowpassResonanceQ = (float)(fluidvoice.q_lin * synth.FilterQMod);
        //    }
        //    /* The filter coefficients have to be recalculated (filter
        //    * parameters have changed). Recalculation for various reasons is
        //    * forced by setting last_fres to -1.  The flag filter_startup
        //    * indicates, that the DSP loop runs for the first time, in this
        //    * case, the filter is set directly, instead of smoothly fading
        //    * between old and new settings.
        //    *
        //    * Those equations from Robert Bristow-Johnson's `Cookbook
        //    * formulae for audio EQ biquad filter coefficients', obtained
        //    * from Harmony-central.com / Computer / Programming. They are
        //    * the result of the bilinear transform on an analogue filter
        //    * prototype. To quote, `BLT frequency warping has been taken
        //    * into account for both significant frequency relocation and for
        //    * bandwidth readjustment'. */

        //    //double omega = (2d * M_PI * (fres / 44100.0d));
        //    //double sin_coeff = Math.Sin(omega);
        //    //double cos_coeff = Math.Cos(omega);
        //    //double alpha_coeff = sin_coeff / (2.0d * q_lin);
        //    //double a0_inv = 1.0d / (1.0d + alpha_coeff);

        //    /* Calculate the filter coefficients. All coefficients are
        //     * normalized by a0. Think of `a1' as `a1/a0'.
        //     *
        //     * Here a couple of multiplications are saved by reusing common expressions.
        //     * The original equations should be:
        //     *  b0=(1.-cos_coeff)*a0_inv*0.5*filter_gain;
        //     *  b1=(1.-cos_coeff)*a0_inv*filter_gain;
        //     *  b2=(1.-cos_coeff)*a0_inv*0.5*filter_gain; */

        //    //double a1_temp = -2d * cos_coeff * a0_inv;
        //    //double a2_temp = (1d - alpha_coeff) * a0_inv;
        //    //double b1_temp = (1d - cos_coeff) * a0_inv * filter_gain;
        //    ///* both b0 -and- b2 */
        //    //double b02_temp = b1_temp * 0.5f;

        //    //if (filter_startup)
        //    //{
        //    //    /* The filter is calculated, because the voice was started up.
        //    //     * In this case set the filter coefficients without delay.
        //    //     */
        //    //    a1 = a1_temp;
        //    //    a2 = a2_temp;
        //    //    b02 = b02_temp;
        //    //    b1 = b1_temp;
        //    //    filter_coeff_incr_count = 0;
        //    //    filter_startup = false;
        //    //    //       printf("Setting initial filter coefficients.\n");
        //    //}
        //    //else
        //    //{

        //    //    /* The filter frequency is changed.  Calculate an increment
        //    //     * factor, so that the new setting is reached after one buffer
        //    //     * length. x_incr is added to the current value FLUID_BUFSIZE
        //    //     * times. The length is arbitrarily chosen. Longer than one
        //    //     * buffer will sacrifice some performance, though.  Note: If
        //    //     * the filter is still too 'grainy', then increase this number
        //    //     * at will.
        //    //     */

        //    //    a1_incr = (a1_temp - a1) / fluid_synth_t.FLUID_BUFSIZE;
        //    //    a2_incr = (a2_temp - a2) / fluid_synth_t.FLUID_BUFSIZE;
        //    //    b02_incr = (b02_temp - b02) / fluid_synth_t.FLUID_BUFSIZE;
        //    //    b1_incr = (b1_temp - b1) / fluid_synth_t.FLUID_BUFSIZE;
        //    //    /* Have to add the increments filter_coeff_incr_count times. */
        //    //    filter_coeff_incr_count = fluid_synth_t.FLUID_BUFSIZE;
        //    //}
        //    fluidvoice.last_fres = localfres;
        //}

        // return localfres;
        //   }


        ///* No interpolation. Just take the sample, which is closest to
        //  * the playback pointer.  Questionable quality, but very
        //  * efficient. */
        //int
        //fluid_dsp_float_interpolate_none()
        //{
        //    //fluid_phase_t dsp_phase = phase;
        //    //fluid_phase_t dsp_phase_incr, end_phase;
        //    //short int* dsp_data = sample->data;
        //    fluid_real_t* dsp_buf = dsp_buf;
        //    fluid_real_t dsp_amp = amp;
        //    fluid_real_t dsp_amp_incr = amp_incr;
        //    unsigned int dsp_i = 0;
        //    unsigned int dsp_phase_index;
        //    unsigned int end_index;
        //    int looping;

        //    /* Convert playback "speed" floating point value to phase index/fract */
        //    //fluid_phase_set_float(dsp_phase_incr, phase_incr);

        //    /* voice is currently looping? */
        //    looping = _SAMPLEMODE(voice) == FLUID_LOOP_DURING_RELEASE
        //      || (_SAMPLEMODE(voice) == FLUID_LOOP_UNTIL_RELEASE
        //      && volenv_section < FLUID_VOICE_ENVRELEASE);

        //    end_index = looping ? loopend - 1 : end;

        //    while (1)
        //    {
        //        dsp_phase_index = fluid_phase_index_round(dsp_phase);   /* round to nearest point */

        //        /* interpolate sequence of sample points */
        //        for (; dsp_i < FLUID_BUFSIZE && dsp_phase_index <= end_index; dsp_i++)
        //        {
        //            dsp_buf[dsp_i] = dsp_amp * dsp_data[dsp_phase_index];

        //            /* increment phase and amplitude */
        //            fluid_phase_incr(dsp_phase, dsp_phase_incr);
        //            dsp_phase_index = fluid_phase_index_round(dsp_phase);   /* round to nearest point */
        //            dsp_amp += dsp_amp_incr;
        //        }

        //        /* break out if not looping (buffer may not be full) */
        //        if (!looping) break;

        //        /* go back to loop start */
        //        if (dsp_phase_index > end_index)
        //        {
        //            fluid_phase_sub_int(dsp_phase, loopend - loopstart);
        //            has_looped = 1;
        //        }

        //        /* break out if filled buffer */
        //        if (dsp_i >= FLUID_BUFSIZE) break;
        //    }

        //    phase = dsp_phase;
        //    amp = dsp_amp;

        //    return (dsp_i);

        //}

        public IEnumerator<float> Release()
        {
            //fluid_voice_noteoff();
            //Debug.Log("Release " + IdVoice);
            fluidvoice.fluid_rvoice_noteoff(true);
            yield return 0;
        }



    }
}
