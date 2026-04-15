//#define MPTK_PRO

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Analytics;


namespace MidiPlayerTK
{
    /// <summary> 
    /// Base class wich contains all the stuff to build a Wave Table Synth.
    /// 
    /// Load SoundFont and samples, process midi event, play voices, controllers, generators ...\n 
    /// This class is inherited by others class to build these prefabs: MidiStreamPlayer, MidiFilePlayer, MidiInReader.\n
    /// <b>It is not recommended to instanciate directly this class, rather add prefabs to the hierarchy of your scene. 
    /// and use attributs and methods from an instance of them in your script.</b> 
    /// Example:
    ///     - midiFilePlayer.MPTK_ChorusDelay = 0.2
    ///     - midiStreamPlayer.MPTK_InitSynth()
    /// </summary>
#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
    public partial class MidiSynth : MonoBehaviour, IMixerProcessor
    {
#else
    //[ExecuteAlways]
    public partial class MidiSynth : MonoBehaviour
    {
#endif
        public void fluid_defpreset_noteon(MPTKEvent note)
        {
            //if (note.Tag != null && note.Tag.GetType() == typeof(long))
            //    StatUILatencyLAST = (float)(DateTime.UtcNow.Ticks - (long)note.Tag) / (float)fluid_voice.Nano100ToMilli;

            HiSample hiSample;
            fluid_voice voice;
            List<HiMod> mod_list = new List<HiMod>();

            // v2.15 - restaure intial value before transpose
            if (MPTK_Transpose != 0 && note.Channel != MPTK_TransExcludedChannel)
                note.TransposeValue(MPTK_Transpose);

            //int vel = note.Velocity;
            HiPreset defpreset;

            //DebugPerf("Begin synth_noteon:");
            Channels[note.Channel].NoteCount++;

            if (!Channels[note.Channel].Enable)
            {
                if (MPTK_LogWave)
                    Debug.LogFormat("Channel {0} disabled, cancel playing note: {1}", note.Channel, note.Value);
                return;
            }

            // Use the preset defined in the channel
            defpreset = Channels[note.Channel].HiPreset;
            if (defpreset == null)
            {
                if (MPTK_LogWave)
                    Debug.LogWarningFormat("No preset associated to this channel {0}, set first preset, note: {1}", note.Channel, note.Value);
                // before v2.11 fluid_synth_program_change(note.Channel, 0);
                Channels[note.Channel].fluid_synth_program_change(0);
                defpreset = Channels[note.Channel].HiPreset;
                if (defpreset == null)
                {
                    Debug.LogWarningFormat("No preset associated to this channel {0}, cancel playing note: {1}", note.Channel, note.Value);
                    return;
                }
            }

            // If the same note is hit twice on the same channel, then the older voice process is advanced to the release stage.  
            if (MPTK_ReleaseSameNote)
                fluid_synth_release_voice_on_same_note(note.Channel, note.Value);

            //ImSoundFont sfont = MidiPlayerGlobal.ImSFCurrent;
            ImSoundFont soundFont = MPTK_SoundFont.SoundFont;
            note.Voices = new List<fluid_voice>();

            HiZone global_preset_zone = defpreset.GlobalZone;

            // run thru all the zones of this preset 
            foreach (HiZone preset_zone in defpreset.Zone)
            {
                // See v2.3 fluid_defsont.c line 919
                // ---------------------------------

                // check if the note falls into the key and velocity range of this preset 
                if ((preset_zone.KeyLo <= note.Value) &&
                    (preset_zone.KeyHi >= note.Value) &&
                    (preset_zone.VelLo <= note.Velocity) &&
                    (preset_zone.VelHi >= note.Velocity))
                {
                    if (preset_zone.Index >= 0)
                    {
                        HiInstrument inst = soundFont.HiSf.inst[preset_zone.Index];
                        HiZone global_inst_zone = inst.GlobalZone;


                        if (global_preset_zone != null && global_preset_zone.genE == null)
                        {
                            // Build just once at the first use of this zone
                            //Debug.Log($"Build gen global_preset_zone {global_preset_zone.Index}");
                            global_preset_zone.genE = new HiGen[(int)fluid_gen_type.GEN_LAST];
                            foreach (HiGen gen in global_preset_zone.gens)
                                global_preset_zone.genE[(byte)gen.type] = gen;
                        }

                        if (global_inst_zone != null && global_inst_zone.genE == null)
                        {
                            // Build just once at the first use of this zone
                            //Debug.Log($"Build gen global_inst_zone {global_inst_zone.Index}");
                            global_inst_zone.genE = new HiGen[(int)fluid_gen_type.GEN_LAST];
                            foreach (HiGen gen in global_inst_zone.gens)
                                global_inst_zone.genE[(byte)gen.type] = gen;
                        }

                        // run thru all the zones of this instrument that could start a voice
                        foreach (HiZone voice_zone in inst.Zone)
                        {
                            if (voice_zone.Index < 0 || voice_zone.Index >= soundFont.HiSf.Samples.Length)
                                continue;

                            // make sure this instrument zone has a valid sample
                            hiSample = soundFont.HiSf.Samples[voice_zone.Index];
                            if (hiSample == null)
                                continue;

                            // check if the note falls into the key and velocity range of this instrument
                            // Line 939
                            if ((voice_zone.KeyLo <= note.Value) &&
                                (voice_zone.KeyHi >= note.Value) &&
                                (voice_zone.VelLo <= note.Velocity) &&
                                (voice_zone.VelHi >= note.Velocity))
                            {
                                // Just to keep naming comptibility with FS
                                // voice_zone is a inst_zone with a range
                                HiZone inst_zone = voice_zone;

                                voice = fluid_synth_alloc_voice_LOCAL(hiSample, note);
#if LOG_PERF_NOTEON
                                DebugPerf("After fluid_synth_alloc_voice:");
#endif
                                if (voice == null) return;

                                //voice.MptkEvent = note; // V2.10, note set in fluid_synth_alloc_voice_LOCAL parameter, replace  note.Channel, note.IdSession, key, vel);
                                note.Voices.Add(voice);
                                //voice.Duration = note.Duration; // only for information, not used

                                // V2.82: can be set to -1 
                                // Calculate the real duration in tick
                                if (midiLoaded != null)
                                    voice.DurationTick = note.Duration >= 0 ? (long)(((double)(note.Duration * fluid_voice.Nano100ToMilli)) / midiLoaded.Speed) : -1;
                                else
                                    // No midi loaded. Synth used as a realtime player without MIDI loaded
                                    voice.DurationTick = note.Duration >= 0 ? (long)(((double)(note.Duration * fluid_voice.Nano100ToMilli))) : -1;

                                //if (!voice.Reused) // For v2.12.2 try to optimize
                                {
                                    // Instrument level - Generator

                                    if (inst_zone.genE == null)
                                    {
                                        // Build just once at the first use of this zone
                                        //Debug.Log($"Build gen inst_zone {inst_zone.Index}");
                                        inst_zone.genE = new HiGen[(int)fluid_gen_type.GEN_LAST];
                                        foreach (HiGen gen in inst_zone.gens)
                                            inst_zone.genE[(byte)gen.type] = gen;
                                    }

                                    for (int i = 0; i < (int)fluid_gen_type.GEN_LAST; i++)
                                    {
                                        HiGen inst_zone_genE = inst_zone.genE[i];

                                        /* SF 2.01 section 9.4 'bullet' 4:
                                         *
                                         * A generator in a local instrument zone supersedes a
                                         * global instrument zone generator.  Both cases supersede
                                         * the default generator -> voice_gen_set */
                                        //if (i == 52)
                                        //    Debug.Log("FINETUNE");

                                        if (inst_zone_genE != null && inst_zone_genE.flags != fluid_gen_flags.GEN_UNUSED)
                                        {
                                            //fluid_voice_gen_set(voice, i, inst_zone->gen[i].val);
                                            voice.gen[i].Val = inst_zone_genE.Val;
                                            voice.gen[i].flags = fluid_gen_flags.GEN_SET;
                                            if (i == (int)fluid_gen_type.GEN_SAMPLEMODE)
                                                voice.samplemode = (fluid_loop)voice.gen[i].Val;
                                        }
                                        else if ((global_inst_zone != null) && global_inst_zone.genE[i] != null && (global_inst_zone.genE[i].flags != fluid_gen_flags.GEN_UNUSED))
                                        {
                                            //fluid_voice_gen_set(voice, i, global_inst_zone->gen[i].val);
                                            voice.gen[i].Val = global_inst_zone.genE[i].Val;
                                            voice.gen[i].flags = fluid_gen_flags.GEN_SET;
                                            if (i == (int)fluid_gen_type.GEN_SAMPLEMODE)
                                                voice.samplemode = (fluid_loop)voice.gen[i].Val;
                                        }
                                        else
                                        {
                                            /* The generator has not been defined in this instrument.
                                             * Do nothing, leave it at the default.
                                             */
                                        }

                                    } /* for all generators */


                                    //// Global zone

                                    //// SF 2.01 section 9.4 'bullet' 4:
                                    //// A generator in a local instrument zone supersedes a
                                    //// global instrument zone generator.  Both cases supersede
                                    //// the default generator -> voice_gen_set

                                    /* Adds instrument zone modulators (global and local) to the voice.*/
                                    fluid_defpreset_noteon_add_mod_to_voice(voice,
                                                        /* global instrument modulators */
                                                        global_inst_zone != null ? global_inst_zone.mods : null,
                                                        inst_zone.mods, /* local instrument modulators */
                                                        fluid_voice_addorover_mod.FLUID_VOICE_OVERWRITE); /* mode */


                                    /* Preset level, generators */

                                    if (preset_zone.genE == null)
                                    {
                                        // Build just once at the first use of this zone
                                        preset_zone.genE = new HiGen[(int)fluid_gen_type.GEN_LAST];
                                        foreach (HiGen gen in preset_zone.gens)
                                            preset_zone.genE[(byte)gen.type] = gen;
                                    }

                                    for (int i = 0; i < (int)fluid_gen_type.GEN_LAST; i++)
                                    {

                                        /* SF 2.01 section 8.5 page 58: If some generators are
                                         encountered at preset level, they should be ignored.
                                         However this check is not necessary when the soundfont
                                         loader has ignored invalid preset generators.
                                         Actually load_pgen()has ignored these invalid preset
                                         generators:
                                           GEN_STARTADDROFS,      GEN_ENDADDROFS,
                                           GEN_STARTLOOPADDROFS,  GEN_ENDLOOPADDROFS,
                                           GEN_STARTADDRCOARSEOFS,GEN_ENDADDRCOARSEOFS,
                                           GEN_STARTLOOPADDRCOARSEOFS,
                                           GEN_KEYNUM, GEN_VELOCITY,
                                           GEN_ENDLOOPADDRCOARSEOFS,
                                           GEN_SAMPLEMODE, GEN_EXCLUSIVECLASS,GEN_OVERRIDEROOTKEY
                                        */

                                        /* SF 2.01 section 9.4 'bullet' 9: A generator in a
                                         * local preset zone supersedes a global preset zone
                                         * generator.  The effect is -added- to the destination
                                         * summing node -> voice_gen_incr */

                                        HiGen preset_zone_genE = preset_zone.genE[i];

                                        if (preset_zone_genE != null && preset_zone_genE.flags != fluid_gen_flags.GEN_UNUSED)
                                        {
                                            //fluid_voice_gen_incr(voice, i, inst_zone->gen[i].val);
                                            voice.gen[i].Val += preset_zone_genE.Val;
                                            voice.gen[i].flags = fluid_gen_flags.GEN_SET;
                                        }
                                        else if ((global_preset_zone != null) && global_preset_zone.genE[i] != null && global_preset_zone.genE[i].flags != fluid_gen_flags.GEN_UNUSED)
                                        {
                                            //fluid_voice_gen_incr(voice, i, global_preset_zone->gen[i].val);
                                            voice.gen[i].Val += global_preset_zone.genE[i].Val;
                                            voice.gen[i].flags = fluid_gen_flags.GEN_SET;
                                        }
                                        else
                                        {
                                            /* The generator has not been defined in this preset
                                             * Do nothing, leave it unchanged.
                                             */
                                        }
                                    } /* for all generators */

                                    /* Adds preset zone modulators (global and local) to the voice.*/
                                    fluid_defpreset_noteon_add_mod_to_voice(voice,
                                                        /* global preset modulators */
                                                        global_preset_zone != null ? global_preset_zone.mods : null,
                                                        preset_zone.mods, /* local preset modulators */
                                                        fluid_voice_addorover_mod.FLUID_VOICE_ADD); /* mode */

                                }

#if LOG_PERF_NOTEON
                                DebugPerf("After genmod init:");
#endif
                                /* Start the new voice */
                                voice.fluid_voice_start();

#if LOG_PERF_NOTEON
                                DebugPerf("After fluid_voice_start:");
#endif

                                if (MPTK_LogWave)
                                {
                                    sLogSampleUse.Clear();
                                    sLogSampleUse.Append($"Channel:{note.Channel:00} Bank:{Channels[note.Channel].BankNum:000} Preset:{Channels[note.Channel].PresetNum:000} ");
                                    sLogSampleUse.Append($"{defpreset.Name,-21} Key:{note.Value,-3}({HelperNoteLabel.LabelFromMidi(note.Value),-3}) ");
                                    sLogSampleUse.Append($"Transp.:{(note.OriginalValue == -1 ? 0 : note.Value - note.OriginalValue),-3} ");
                                    sLogSampleUse.Append($"Vel.:{note.Velocity,-3} ");
                                    sLogSampleUse.Append(note.Duration >= 0 ? $"Duration:{note.Duration,6} ms {voice.DurationTick,9} ticks " : "Inf.  ");
                                    sLogSampleUse.Append($"Instr:{inst.Name,-21} Sample:{soundFont.HiSf.Samples[voice_zone.Index].Name,-21} ");
                                    sLogSampleUse.Append($"Atten:{fluid_conv.fluid_cb2amp(voice.attenuation):F2} Pan:{voice.pan:F2}");
                                    //,Channels[note.Channel].cc[(int)MPTKController.VOLUME_MSB]  {12}
                                    Debug.Log(sLogSampleUse);
                                }

                                if (VerboseGenerator)
                                    foreach (HiGen gen in voice.gen)
                                        if (gen != null && gen.flags > 0)
                                            Debug.LogFormat("Gen Id:{1,-50}\t{0}\tValue:{2:0.00}\tMod:{3:0.00}\tflags:{4,-50}", (int)gen.type, gen.type, gen.Val, gen.Mod, gen.flags);

                                /* Store the ID of the first voice that was created by this noteon event.
                                 * Exclusive class may only terminate older voices.
                                 * That avoids killing voices, which have just been created.
                                 * (a noteon event can create several voice processes with the same exclusive
                                 * class - for example when using stereo samples)
                                 */
                            }
                            if (playOnlyFirstWave && note.Voices.Count > 0)
                                return;
                        }
                    }

                }
            }
#if LOG_PERF_NOTEON
            DebugPerf("After synth_noteon:");
#endif
            if (MPTK_LogWave && note.Voices.Count == 0)
                Debug.LogFormat("NoteOn [{0:00} {1:000} {2:000}]\t{3,-21}\tKey:{4,-3}\tVel:{5,-3}\tDuration:{6:0.000}\tInstr:{7,-21}",
                note.Channel, Channels[note.Channel].BankNum, Channels[note.Channel].PresetNum, defpreset.Name, note.Value, note.Velocity, note.Duration, "*** no wave found ***");
        }


        /*
         * Adds global and local modulators list to the voice. This is done in 2 steps:
         * - Step 1: Local modulators replace identical global modulators.
         * - Step 2: global + local modulators are added to the voice using mode.
         *
         * Instrument zone list (local/global) must be added using FLUID_VOICE_OVERWRITE.
         * Preset zone list (local/global) must be added using FLUID_VOICE_ADD.
         *
         * @param voice voice instance.
         * @param global_mod global list of modulators.
         * @param local_mod local list of modulators.
         * @param mode Determines how to handle an existing identical modulator.
         *   #FLUID_VOICE_ADD to add (offset) the modulator amounts,
         *   #FLUID_VOICE_OVERWRITE to replace the modulator,
        */
        void fluid_defpreset_noteon_add_mod_to_voice(fluid_voice voice,
                                                HiMod[] global_mod, HiMod[] local_mod,
                                                fluid_voice_addorover_mod mode)
        {
            /* list for 'sorting' global/local modulators */
            List<HiMod> mod_list = new List<HiMod>();
            //int mod_list_count;

            /* identity_limit_count is the modulator upper limit number to handle with
             * existing identical modulators.
             * When identity_limit_count is below the actual number of modulators, this
             * will restrict identity check to this upper limit,
             * This is useful when we know by advance that there is no duplicate with
             * modulators at index above this limit. This avoid wasting cpu cycles at
             * noteon.
             */
            //int identity_limit_count;

            /* Step 1: Local modulators replace identical global modulators. */
            //mod_list_count = 0;

            if (local_mod != null)
                foreach (HiMod l_mod in local_mod)
                {
                    /* As modulators number in local_mod list was limited to FLUID_NUM_MOD at
                       soundfont loading time (fluid_limit_mod_list()), here we don't need
                       to check if mod_list is full.
                     */
                    mod_list.Add(l_mod);
                    //mod_list_count++;
                }

            /* global (instrument zone/preset zone), modulators.
             * Replace modulators with the same definition in the global list:
             * (Instrument zone: SF 2.01 page 69, 'bullet' 8)
             * (Preset zone:     SF 2.01 page 69, second-last bullet).
             *
             * mod_list contains local modulators. Now we know that there
             * is no global modulator identical to another global modulator (this has
             * been checked at soundfont loading time). So global modulators
             * are only checked against local modulators number.
             */

            /* Restrict identity check to the number of local modulators */
            // identity_limit_count = mod_list.Count;

            if (global_mod != null)
            {
                foreach (HiMod g_mod in global_mod)
                {
                    /* 'Identical' global modulators are ignored.
                     *  SF2.01 section 9.5.1
                     *  page 69, 'bullet' 3 defines 'identical'.  */
                    bool Found = false;
                    foreach (HiMod x_mod in mod_list)
                    {
                        if (x_mod.fluid_mod_test_identity(g_mod))
                        {
                            Found = true;
                            break;
                        }
                    }

                    /* Finally add the new modulator to the list. */
                    if (!Found)
                    {
                        /* Although local_mod and global_mod lists was limited to
                           FLUID_NUM_MOD at soundfont loading time, it is possible that
                           local + global modulators exceeds FLUID_NUM_MOD.
                           So, checks if mod_list_count reaches the limit.
                        */
                        if (/*mod_list_count*/ mod_list.Count >= HiMod.FLUID_NUM_MOD)
                        {
                            /* mod_list is full, we silently forget this modulator and
                               next global modulators. When mod_list will be added to the
                               voice, a warning will be displayed if the voice list is full.
                               (see fluid_voice_add_mod_local()).
                            */
                            break;
                        }

                        mod_list.Add(g_mod);
                        //mod_list_count++;
                    }
                }
            }

            /* Step 2: global + local modulators are added to the voice using mode. */

            /*
             * mod_list contains local and global modulators, we know that:
             * - there is no global modulator identical to another global modulator,
             * - there is no local modulator identical to another local modulator,
             * So these local/global modulators are only checked against
             * actual number of voice modulators.
             */

            /* Restrict identity check to the actual number of voice modulators */
            /* Actual number of voice modulators : defaults + [instruments] */
            //identity_limit_count = voice.mod_count;

            foreach (HiMod mod in mod_list)
            {

                /* in mode FLUID_VOICE_OVERWRITE disabled instruments modulators CANNOT be skipped. */
                /* in mode FLUID_VOICE_ADD disabled preset modulators can be skipped. */

                if ((mode == fluid_voice_addorover_mod.FLUID_VOICE_OVERWRITE) || (mod.amount != 0))
                {
                    /* Instrument modulators -supersede- existing (default) modulators.
                       SF 2.01 page 69, 'bullet' 6 */

                    /* Preset modulators -add- to existing instrument modulators.
                       SF2.01 page 70 first bullet on page */
                    voice.fluid_voice_add_mod_local(mod, mode, voice.mods.Count);
                }
            }
        }
    }
}
