//#define MPTK_PRO
//#define DEBUGPERF
//#define DEBUGTIME

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace MidiPlayerTK
{
    /* for fluid_voice_add_mod */
    public enum fluid_voice_addorover_mod
    {
        FLUID_VOICE_OVERWRITE,
        FLUID_VOICE_ADD,
        FLUID_VOICE_DEFAULT
    }

    public enum fluid_voice_status
    {
        FLUID_VOICE_CLEAN,
        FLUID_VOICE_ON,
        FLUID_VOICE_SUSTAINED,
        FLUID_VOICE_OFF
    }

    public partial class fluid_voice
    {
        /// <summary>@brief
        /// Time at start of the voice in tick (UTCNow.Ticks)\n
        /// A single tick represents one hundred nanoseconds or one ten-millionth of a second.
        /// There are 10,000 ticks in a millisecond, or 10 million ticks in a second.         
        /// </summary>
        public long TimeAtStart;

        // Set only if VerboseSynth is true
        public long TimeFromStart;
        public long TimeAtEnd;
        public long NewTimeWrite;
        public long LastTimeWrite;

        /// <summary>@brief
        /// Delay in ms between call to fluid_voice_write
        /// </summary>
        public long DeltaTimeWrite;

        public MidiSynth synth;
        public MPTKEvent MptkEvent;
        public int IndexActive;
        public long LatenceTick;

        /// <summary>@brief
        /// Legacy mode, mix fluid_voice and a AudioSource
        /// </summary>
        public VoiceAudioSource VoiceAudio;

        //public string SampleName;
        public float StartVolume;
        //public bool IsLoop;

        static public int LastId;
        public int IdSession;
        public bool Reused;

        /// <summary>@brief
        ///  MPTK specific - Note duration in tick. Set to -1 to indefinitely.
        /// A single tick represents one hundred nanoseconds or one ten-millionth of a second.
        /// There are 10,000 ticks in a millisecond, or 10 million ticks in a second.         
        /// </summary>
        public long DurationTick;

        /// <summary>@brief
        /// Duration of the note in millisecond, only for information
        /// </summary>
        public long Duration;

        public const uint Nano100ToMilli = 10000;

        // min vol envelope release (to stop clicks) in SoundFont timecents : ~16ms 
        public const int NO_CHANNEL = 0xff;

        /* used for filter turn off optimization - if filter cutoff is above the
           specified value and filter q is below the other value, turn filter off */
        public const float FLUID_MAX_AUDIBLE_FILTER_FC = 19000.0f;
        public const float FLUID_MIN_AUDIBLE_FILTER_Q = 1.2f;

        /* Smallest amplitude that can be perceived (full scale is +/- 0.5)
         * 16 bits => 96+4=100 dB dynamic range => 0.00001
         * 0.00001 * 2 is approximately 0.00003 :)
         */
        public const float FLUID_NOISE_FLOOR = 0.00003f;

        /* these should be the absolute minimum that FluidSynth can deal with */
        public const int FLUID_MIN_LOOP_SIZE = 2;
        public const int FLUID_MIN_LOOP_PAD = 0;

        /* min vol envelope release (to stop clicks) in SoundFont timecents */
        public const float FLUID_MIN_VOLENVRELEASE = -7200.0f;/* ~16ms */

        public const float M_PI = 3.1415926535897932384626433832795f;
        public const double M_PId = 3.1415926535897932384626433832795d;


        public int id;                      /* the id is incremented for every new noteon. it's used for noteoff's  */
        public fluid_voice_status status;
        public int chan;             /* the channel number, quick access for channel messages */
        public int key;              /* the key, quick acces for noteoff */
        public int vel;              /* the velocity */
        public MPTKChannel channel;
        public HiSample sample;

        public int mod_count;
        public List<HiMod> mods; //[FLUID_NUM_MOD];
        public HiGen[] gen; //[GEN_LAST];

        public float output_rate;        /* the sample rate of the synthesizer */

        public float pitch;              /* the pitch in midicents */
        public float attenuation;        /* the attenuation in centibels */
        public float root_pitch;

        /* master gain (dupe in rvoice) and not yet used */
        // fluid_real_t synth_gain;

        public float pan;
        private float amp_left;
        private float amp_right;

        // Not yet used bool has_noteoff; /* Flag set when noteoff has been sent */

        #region fluid_rvoice_dsp_t
        /* -----------------------------------------------------------------------------------
         * rvoice parameters needed for dsp interpolation
         * FS: struct fluid_rvoice_dsp_t
         */
        public bool is_looping; // moved from local fluid_rvoice_write
        public bool has_looped; /* Flag that is set as soon as the first loop is completed. */
        fluid_interp interp_method; // MPTK used at synth level
        public fluid_loop samplemode; // Set at SF instrument level. 0: no loop, 1: loop when released

        public byte check_sample_sanity_flag;   /* Flag that initiates, that sample-related parameters have to be checked. */
        // To be closer FS
        const byte FLUID_SAMPLESANITY_CHECK = 1 << 0;
        const byte FLUID_SAMPLESANITY_STARTUP = 1 << 1;

        // Duplicate from fluid_voice_t
        // fluid_sample_t* sample;

        /* sample and loop start and end points (offset in sample memory).  */
        public int start;
        public int end;
        public int loopstart;
        public int loopend;    /* Note: first point following the loop (superimposed on loopstart) */

        /* Stuff needed for portamento calculations */
        public float pitchoffset;        /* the portamento range in midicents */
        public float pitchinc;           /* the portamento increment in midicents */

        // duplicate from fluid_voice.c ?
        // fluid_real_t pitch;              /* the pitch in midicents */

        public float root_pitch_hz;

        // duplicate from fluid_voice.c ?
        // fluid_real_t output_rate;

        // duplicate from fluid_voice.c ?
        // fluid_real_t attenuation;        /* the attenuation in centibels */

        float prev_attenuation;   /* the previous attenuation in centibels used by fluid_rvoice_multi_retrigger_attack() */
        public float min_attenuation_cB; /* Estimate on the smallest possible attenuation during the lifetime of the voice */

        /* master gain (dupe in fluid_voice) and not yet used */
        // fluid_real_t synth_gain;

        //public uint start_time;

        public float amp;                /* current linear amplitude */
        public float amp_incr;      /* amplitude increment value */

        /* fluid_phase_t
        * Purpose:
        * Playing pointer for voice playback
        *
        * When a sample is played back at a different pitch, the playing pointer in the
        * source sample will not advance exactly one sample per output sample.
        * This playing pointer is implemented using fluid_phase_t.
        * It is a 64 bit number. The higher 32 bits contain the 'index' (number of
        * the current sample), the lower 32 bits the fractional part.
          it's a uint64 with FS */
        public ulong phase;             /* the phase of the sample wave */

        /* Temporary variables used in fluid_voice_write() */

        public float phase_incr;    /* the phase increment for the next 64 samples */

        #endregion

        public float[] dsp_buf;      /* buffer to store interpolated sample data to */

        /* -----------------------------------------------------------------------------------
         * rvoice ticks-based parameters
         * These parameters must be updated even if the voice is currently quiet.
         * FS:  attribut fluid_rvoice_t voice->rvoice in fluid_voice.c with:
         *          fluid_rvoice_envlfo_t envlfo;
         *          fluid_rvoice_dsp_t dsp;
         *          fluid_iir_filter_t resonant_filter; // IIR resonant dsp filter 
         *          fluid_iir_filter_t resonant_custom_filter; // optional custom/general-purpose IIR resonant filter 
         *          fluid_rvoice_buffers_t buffers;
         *          
         *      with struct fluid_rvoice_envlfo_t for modenv,volenv, modlfo ... 
         *      see bellow:
         */

        /* Note-off minimum length */
        long ticks;
        // not yet used long noteoff_ticks;


        /* fluid_adsr_env_t volenv */
        public fluid_env_data[] volenv_data; // volume enveloppe [FLUID_VOICE_ENVLAST];
        public long volenv_count; // Count time since the start of the section
        public float volenv_val;
        public fluid_voice_envelope_index volenv_section; //Current section in the enveloppe

        //public float amplitude_that_reaches_noise_floor_nonloop;
        //public float amplitude_that_reaches_noise_floor_loop;

        /* fluid_adsr_env_t modenv */
        public fluid_env_data[] modenv_data;  // modulation enveloppe [FLUID_VOICE_ENVLAST];
        public long modenv_count;
        public fluid_voice_envelope_index modenv_section;
        public float modenv_val;         /* the value of the modulation envelope */
        /* from fluid_rvoice_envlfo_t */
        public float modenv_to_fc;
        public float modenv_to_pitch;

        /* fluid_lfo_t modlfo */
        public float modlfo_val;          /* the value of the modulation LFO */
        public uint modlfo_delay;       /* the delay of the lfo in samples */
        public float modlfo_incr;         /* the lfo frequency is converted to a per-buffer increment */
        /* from fluid_rvoice_envlfo_t */
        public float modlfo_to_fc;
        public float modlfo_to_pitch;
        public float modlfo_to_vol;

        /* fluid_lfo_t viblfo */
        public float viblfo_val;        /* the value of the vibrato LFO */
        public long viblfo_delay;      /* the delay of the lfo in samples */
        public float viblfo_incr;       /* the lfo frequency is converted to a per-buffer increment */
        /* from fluid_rvoice_envlfo_t */
        public float viblfo_to_pitch;

        public float q_dB; // from GEN_FILTERQ, Q factor in centibels
        public float fres; // from GEN_FILTERFC

        /* reverb */
        //public float reverb_send;
        float reverb_send;

        /* chorus */
        //public float chorus_send;
        float chorus_send;

        public fluid_iir_filter resonant_filter;
        //fluid_iir_filter resonant_custom_filter; /* optional custom/general-purpose IIR resonant filter */


        /* interpolation method, as in fluid_interp in fluidsynth.h */
        // move to synth
        //public fluid_interp interp_method;

        /* for debugging */
        //public int debug;
        //TBC double ref;

        // To avoid realloc every frame
        static private StringBuilder logSynthInfo;
        public static string HeaderVoiceInfo()
        {
            return "ID:[session event] TCV:[track channel value] TT:[time_second tick] MB:[measure beat]" +
                   "DD:[duration_second duration_ticks] VS:[volume ADSR_section] PR:[preset_num preset_name sample_name]";
        }
        public override string ToString()
        {
            try
            {
                if (logSynthInfo == null)
                    logSynthInfo = new StringBuilder(256);
                else
                    logSynthInfo.Clear();

                logSynthInfo.Append($"ID:[{MptkEvent.IdSession} {MptkEvent.Index}] TCV:[{MptkEvent.Track:00} {MptkEvent.Channel:00} {MptkEvent.Value:000}] TT:[{MptkEvent.RealTime / 1000f:F3} {MptkEvent.Tick,-7:0000000}] ");
                logSynthInfo.Append($"MB[{MptkEvent.Measure}/{MptkEvent.Beat}] DD:[{MptkEvent.Duration / 1000f:F2} {MptkEvent.Length}] VS:[{volenv_val:F2} {volenv_section}] ");
                logSynthInfo.AppendLine($"PR:['{synth.Channels[MptkEvent.Channel].HiPreset.Num} {synth.Channels[MptkEvent.Channel].HiPreset.Name}' '{sample.Name}']");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                logSynthInfo.Append(ex.ToString());
            }
            return logSynthInfo.ToString();
        }

        static int[] list_of_generators_to_initialize =
             {
                (int)fluid_gen_type.GEN_STARTADDROFS,                    /* SF2.01 page 48 #0  - Unity load wave from wave file, no real time change possible on wave attribute */
                (int)fluid_gen_type.GEN_ENDADDROFS,                      /*                #1  - Unity load wave from wave file, no real time change possible on wave attribute */
                (int)fluid_gen_type.GEN_STARTLOOPADDROFS,                /*                #2  - Unity load wave from wave file, no real time change possible on wave attribute */
                (int)fluid_gen_type.GEN_ENDLOOPADDROFS,                  /*                #3  - Unity load wave from wave file, no real time change possible on wave attribute */
                /* (int)fluid_gen_type.GEN_STARTADDRCOARSEOFS see comment below [1]        #4  - Unity load wave from wave file, no real time change possible on wave attribute */
                (int)fluid_gen_type.GEN_MODLFOTOPITCH,                   /*                #5   */
                (int)fluid_gen_type.GEN_VIBLFOTOPITCH,                   /*                #6   */
                (int)fluid_gen_type.GEN_MODENVTOPITCH,                   /*                #7   */
                (int)fluid_gen_type.GEN_FILTERFC,                        /*                #8   */
                (int)fluid_gen_type.GEN_FILTERQ,                         /*                #9   */
                (int)fluid_gen_type.GEN_MODLFOTOFILTERFC,                /*                #10  */
                (int)fluid_gen_type.GEN_MODENVTOFILTERFC,                /*                #11  */
                /* (int)fluid_gen_type.GEN_ENDADDRCOARSEOFS [1]                            #12  - Unity load wave from wave file, no real time change possible on wave attribute */
                (int)fluid_gen_type.GEN_MODLFOTOVOL,                     /*                #13  */
                /* not defined                                         #14  */
                (int)fluid_gen_type.GEN_CHORUSSEND,                      /*                #15  */
                (int)fluid_gen_type.GEN_REVERBSEND,                      /*                #16  */
                (int)fluid_gen_type.GEN_PAN,                             /*                #17  */
                /* not defined                                         #18  */
                /* not defined                                         #19  */
                /* not defined                                         #20  */
                (int)fluid_gen_type.GEN_MODLFODELAY,                     /*                #21  */
                (int)fluid_gen_type.GEN_MODLFOFREQ,                      /*                #22  */
                (int)fluid_gen_type.GEN_VIBLFODELAY,                     /*                #23  */
                (int)fluid_gen_type.GEN_VIBLFOFREQ,                      /*                #24  */
                (int)fluid_gen_type.GEN_MODENVDELAY,                     /*                #25  */
                (int)fluid_gen_type.GEN_MODENVATTACK,                    /*                #26  */
                (int)fluid_gen_type.GEN_MODENVHOLD,                      /*                #27  */
                (int)fluid_gen_type.GEN_MODENVDECAY,                     /*                #28  */
                /* (int)fluid_gen_type.GEN_MODENVSUSTAIN [1]                               #29  */
                (int)fluid_gen_type.GEN_MODENVRELEASE,                   /*                #30  */
                /* (int)fluid_gen_type.GEN_KEYTOMODENVHOLD [1]                             #31  */
                /* (int)fluid_gen_type.GEN_KEYTOMODENVDECAY [1]                            #32  */
                (int)fluid_gen_type.GEN_VOLENVDELAY,                     /*                #33  */
                (int)fluid_gen_type.GEN_VOLENVATTACK,                    /*                #34  */
                (int)fluid_gen_type.GEN_VOLENVHOLD,                      /*                #35  */
                (int)fluid_gen_type.GEN_VOLENVDECAY,                     /*                #36  */
                /* (int)fluid_gen_type.GEN_VOLENVSUSTAIN [1]                               #37  */
                (int)fluid_gen_type.GEN_VOLENVRELEASE,                   /*                #38  */
                /* (int)fluid_gen_type.GEN_KEYTOVOLENVHOLD [1]                             #39  */
                /* (int)fluid_gen_type.GEN_KEYTOVOLENVDECAY [1]                            #40  */
                /* (int)fluid_gen_type.GEN_STARTLOOPADDRCOARSEOFS [1]                      #45 - Unity load wave from wave file, no real time change possible on wave attribute */
                (int)fluid_gen_type.GEN_KEYNUM,                          /*                #46  */
                (int)fluid_gen_type.GEN_VELOCITY,                        /*                #47  */
                (int)fluid_gen_type.GEN_ATTENUATION,                     /*                #48  */
                /* (int)fluid_gen_type.GEN_ENDLOOPADDRCOARSEOFS [1]                        #50  - Unity load wave from wave file, no real time change possible on wave attribute */
                /* (int)fluid_gen_type.GEN_COARSETUNE           [1]                        #51  */
                /* (int)fluid_gen_type.GEN_FINETUNE             [1]                        #52  */
                (int)fluid_gen_type.GEN_OVERRIDEROOTKEY,                 /*                #58  */
                (int)fluid_gen_type.GEN_PITCH,                           /*                ---  */
                (int)fluid_gen_type.GEN_CUSTOM_BALANCE,  /*  Not used with MPTK              ---  */
                (int)fluid_gen_type.GEN_CUSTOM_FILTERFC, /*  Not used with MPTK              ---  */
                (int)fluid_gen_type.GEN_CUSTOM_FILTERQ   /*  Not used with MPTK              ---  */
             };

        static int[] list_of_non_core_generators_to_initialize =
        {
                (int)fluid_gen_type.GEN_STARTADDROFS,                    /* SF2.01 page 48 #0  - Unity load wave from wave file, no real time change possible on wave attribute */
                (int)fluid_gen_type.GEN_ENDADDROFS,                      /*                #1  - Unity load wave from wave file, no real time change possible on wave attribute */
                //(int)fluid_gen_type.GEN_STARTLOOPADDROFS,                /*                #2  - Unity load wave from wave file, no real time change possible on wave attribute */
                //(int)fluid_gen_type.GEN_ENDLOOPADDROFS,                  /*                #3  - Unity load wave from wave file, no real time change possible on wave attribute */
                /* (int)fluid_gen_type.GEN_STARTADDRCOARSEOFS see comment below [1]        #4  - Unity load wave from wave file, no real time change possible on wave attribute */
                (int)fluid_gen_type.GEN_MODLFOTOPITCH,                   /*                #5   */
                (int)fluid_gen_type.GEN_VIBLFOTOPITCH,                   /*                #6   */
                (int)fluid_gen_type.GEN_MODENVTOPITCH,                   /*                #7   */
                //(int)fluid_gen_type.GEN_FILTERFC,                        /*                #8   */
                //(int)fluid_gen_type.GEN_FILTERQ,                         /*                #9   */
                //(int)fluid_gen_type.GEN_MODLFOTOFILTERFC,                /*                #10  */
                //(int)fluid_gen_type.GEN_MODENVTOFILTERFC,                /*                #11  */
                /* (int)fluid_gen_type.GEN_ENDADDRCOARSEOFS [1]                            #12  - Unity load wave from wave file, no real time change possible on wave attribute */
                (int)fluid_gen_type.GEN_MODLFOTOVOL,                     /*                #13  */
                /* not defined                                         #14  */
                //(int)fluid_gen_type.GEN_CHORUSSEND,                      /*                #15  */
                //(int)fluid_gen_type.GEN_REVERBSEND,                      /*                #16  */
                (int)fluid_gen_type.GEN_PAN,                             /*                #17  */
                /* not defined                                         #18  */
                /* not defined                                         #19  */
                /* not defined                                         #20  */
                (int)fluid_gen_type.GEN_MODLFODELAY,                     /*                #21  */
                //(int)fluid_gen_type.GEN_MODLFOFREQ,                      /*                #22  */
                (int)fluid_gen_type.GEN_VIBLFODELAY,                     /*                #23  */
                //(int)fluid_gen_type.GEN_VIBLFOFREQ,                      /*                #24  */
                (int)fluid_gen_type.GEN_MODENVDELAY,                     /*                #25  */
                (int)fluid_gen_type.GEN_MODENVATTACK,                    /*                #26  */
                (int)fluid_gen_type.GEN_MODENVHOLD,                      /*                #27  */
                (int)fluid_gen_type.GEN_MODENVDECAY,                     /*                #28  */
                /* (int)fluid_gen_type.GEN_MODENVSUSTAIN [1]                               #29  */
                (int)fluid_gen_type.GEN_MODENVRELEASE,                   /*                #30  */
                /* (int)fluid_gen_type.GEN_KEYTOMODENVHOLD [1]                             #31  */
                /* (int)fluid_gen_type.GEN_KEYTOMODENVDECAY [1]                            #32  */
                (int)fluid_gen_type.GEN_VOLENVDELAY,                     /*                #33  */
                (int)fluid_gen_type.GEN_VOLENVATTACK,                    /*                #34  */
                (int)fluid_gen_type.GEN_VOLENVHOLD,                      /*                #35  */
                (int)fluid_gen_type.GEN_VOLENVDECAY,                     /*                #36  */
                /* (int)fluid_gen_type.GEN_VOLENVSUSTAIN [1]                               #37  */
                (int)fluid_gen_type.GEN_VOLENVRELEASE,                   /*                #38  */
                /* (int)fluid_gen_type.GEN_KEYTOVOLENVHOLD [1]                             #39  */
                /* (int)fluid_gen_type.GEN_KEYTOVOLENVDECAY [1]                            #40  */
                /* (int)fluid_gen_type.GEN_STARTLOOPADDRCOARSEOFS [1]                      #45 - Unity load wave from wave file, no real time change possible on wave attribute */
                (int)fluid_gen_type.GEN_KEYNUM,                          /*                #46  */
                (int)fluid_gen_type.GEN_VELOCITY,                        /*                #47  */
                (int)fluid_gen_type.GEN_ATTENUATION,                     /*                #48  */
                /* (int)fluid_gen_type.GEN_ENDLOOPADDRCOARSEOFS [1]                        #50  - Unity load wave from wave file, no real time change possible on wave attribute */
                /* (int)fluid_gen_type.GEN_COARSETUNE           [1]                        #51  */
                /* (int)fluid_gen_type.GEN_FINETUNE             [1]                        #52  */
                (int)fluid_gen_type.GEN_OVERRIDEROOTKEY,                 /*                #58  */
                (int)fluid_gen_type.GEN_PITCH,                           /*                ---  */
                (int)fluid_gen_type.GEN_CUSTOM_BALANCE,  /*  Not used with MPTK              ---  */
                //(int)fluid_gen_type.GEN_CUSTOM_FILTERFC, /*  Not used with MPTK              ---  */
                //(int)fluid_gen_type.GEN_CUSTOM_FILTERQ   /*  Not used with MPTK              ---  */
             };
        static int[] list_of_weakgenerators_to_initialize =
     {
                (int)fluid_gen_type.GEN_STARTADDROFS,                    /* SF2.01 page 48 #0  - Unity load wave from wave file, no real time change possible on wave attribute */
                (int)fluid_gen_type.GEN_ENDADDROFS,                      /*                #1  - Unity load wave from wave file, no real time change possible on wave attribute */
                (int)fluid_gen_type.GEN_STARTLOOPADDROFS,                /*                #2  - Unity load wave from wave file, no real time change possible on wave attribute */
                (int)fluid_gen_type.GEN_ENDLOOPADDROFS,                  /*                #3  - Unity load wave from wave file, no real time change possible on wave attribute */
                /* (int)fluid_gen_type.GEN_STARTADDRCOARSEOFS see comment below [1]        #4  - Unity load wave from wave file, no real time change possible on wave attribute */
                //(int)fluid_gen_type.GEN_MODLFOTOPITCH,                   /*                #5   */
                //(int)fluid_gen_type.GEN_VIBLFOTOPITCH,                   /*                #6   */
                //(int)fluid_gen_type.GEN_MODENVTOPITCH,                   /*                #7   */
                //(int)fluid_gen_type.GEN_FILTERFC,                        /*                #8   */
                //(int)fluid_gen_type.GEN_FILTERQ,                         /*                #9   */
                //(int)fluid_gen_type.GEN_MODLFOTOFILTERFC,                /*                #10  */
                //(int)fluid_gen_type.GEN_MODENVTOFILTERFC,                /*                #11  */
                /* (int)fluid_gen_type.GEN_ENDADDRCOARSEOFS [1]                            #12  - Unity load wave from wave file, no real time change possible on wave attribute */
                //(int)fluid_gen_type.GEN_MODLFOTOVOL,                     /*                #13  */
                /* not defined                                         #14  */
                //(int)fluid_gen_type.GEN_CHORUSSEND,                      /*                #15  */
                //(int)fluid_gen_type.GEN_REVERBSEND,                      /*                #16  */
                (int)fluid_gen_type.GEN_PAN,                             /*                #17  */
                /* not defined                                         #18  */
                /* not defined                                         #19  */
                /* not defined                                         #20  */
                //(int)fluid_gen_type.GEN_MODLFODELAY,                     /*                #21  */
                //(int)fluid_gen_type.GEN_MODLFOFREQ,                      /*                #22  */
                //(int)fluid_gen_type.GEN_VIBLFODELAY,                     /*                #23  */
                //(int)fluid_gen_type.GEN_VIBLFOFREQ,                      /*                #24  */
                //(int)fluid_gen_type.GEN_MODENVDELAY,                     /*                #25  */
                //(int)fluid_gen_type.GEN_MODENVATTACK,                    /*                #26  */
                //(int)fluid_gen_type.GEN_MODENVHOLD,                      /*                #27  */
                //(int)fluid_gen_type.GEN_MODENVDECAY,                     /*                #28  */
                /* (int)fluid_gen_type.GEN_MODENVSUSTAIN [1]                               #29  */
                //(int)fluid_gen_type.GEN_MODENVRELEASE,                   /*                #30  */
                /* (int)fluid_gen_type.GEN_KEYTOMODENVHOLD [1]                             #31  */
                /* (int)fluid_gen_type.GEN_KEYTOMODENVDECAY [1]                            #32  */
                //(int)fluid_gen_type.GEN_VOLENVDELAY,                     /*                #33  */
                //(int)fluid_gen_type.GEN_VOLENVATTACK,                    /*                #34  */
                //(int)fluid_gen_type.GEN_VOLENVHOLD,                      /*                #35  */
                //(int)fluid_gen_type.GEN_VOLENVDECAY,                     /*                #36  */
                /* (int)fluid_gen_type.GEN_VOLENVSUSTAIN [1]                               #37  */
                //(int)fluid_gen_type.GEN_VOLENVRELEASE,                   /*                #38  */
                /* (int)fluid_gen_type.GEN_KEYTOVOLENVHOLD [1]                             #39  */
                /* (int)fluid_gen_type.GEN_KEYTOVOLENVDECAY [1]                            #40  */
                /* (int)fluid_gen_type.GEN_STARTLOOPADDRCOARSEOFS [1]                      #45 - Unity load wave from wave file, no real time change possible on wave attribute */
                (int)fluid_gen_type.GEN_KEYNUM,                          /*                #46  */
                (int)fluid_gen_type.GEN_VELOCITY,                        /*                #47  */
                (int)fluid_gen_type.GEN_ATTENUATION,                     /*                #48  */
                /* (int)fluid_gen_type.GEN_ENDLOOPADDRCOARSEOFS [1]                        #50  - Unity load wave from wave file, no real time change possible on wave attribute */
                /* (int)fluid_gen_type.GEN_COARSETUNE           [1]                        #51  */
                /* (int)fluid_gen_type.GEN_FINETUNE             [1]                        #52  */
                (int)fluid_gen_type.GEN_OVERRIDEROOTKEY,                 /*                #58  */
                (int)fluid_gen_type.GEN_PITCH,                           /*                ---  */
             };


        public const float _ratioHalfTone = 1.0594630943592952645618252949463f;

        public bool weakDevice;

        static public long TicksToMilli(long pTicks)
        {
            return (long)(pTicks / Nano100ToMilli);
        }

        static public float TicksToMilliF(long pTicks)
        {
            return (float)pTicks / (float)Nano100ToMilli;
        }

        public fluid_voice(MidiSynth pSynth)
        {
            synth = pSynth;
            IdSession = -1;
            id = LastId++;
            //Debug.Log("New  fluid_voice " + IdVoice);
            //Audiosource.PlayOneShot(new AudioClip(), 0);

            weakDevice = synth.MPTK_CorePlayer ? false : synth.MPTK_WeakDevice;

            gen = new HiGen[(byte)fluid_gen_type.GEN_LAST];
            for (int i = 0; i < gen.Length; i++)
            {
                gen[i] = new HiGen();
                gen[i].type = (fluid_gen_type)i;
                gen[i].flags = fluid_gen_flags.GEN_UNUSED;
            }

            status = fluid_voice_status.FLUID_VOICE_CLEAN;
            chan = NO_CHANNEL;
            key = 0;
            vel = 0;
            channel = null;
            sample = null;
            output_rate = synth.OutputRate;
#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
            dsp_buf = new float[synth.FLUID_MAX_BUFSIZE]; // buffer are length variable
#else
            dsp_buf = new float[synth.FLUID_BUFSIZE];
#endif

            modenv_data = new fluid_env_data[Enum.GetNames(typeof(fluid_voice_envelope_index)).Length];
            for (int i = 0; i < modenv_data.Length; i++)
                modenv_data[i] = new fluid_env_data();

            volenv_data = new fluid_env_data[Enum.GetNames(typeof(fluid_voice_envelope_index)).Length];
            for (int i = 0; i < volenv_data.Length; i++)
                volenv_data[i] = new fluid_env_data();

            // The 'sustain' and 'finished' segments of the volume / modulation envelope are constant. 
            // They are never affected by any modulator or generator. 
            // Therefore it is enough to initialize them once during the lifetime of the synth.

            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].count = 0xffffffff; // infiny until note off or duration is over
            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].coeff = 1;
            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].incr = 0;          // Volume remmains constant during sustain phase
            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].min = -1;          // not used for sustain (constant volume)
            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].max = 2; //1;     // not used for sustain (constant volume)

            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].count = 0xffffffff;
            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].coeff = 0;
            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].incr = 0;
            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].min = -1;
            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].max = 1;

            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].count = 0xffffffff;
            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].coeff = 1;
            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].incr = 0;
            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].min = -1;
            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].max = 2; //1; fluidsythn original value=2

            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].count = 0xffffffff;
            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].coeff = 0;
            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].incr = 0;
            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].min = -1;
            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].max = 1;

            if (synth.dsp_64)
                resonant_filter = new fluid_iir_filter_64(synth.FLUID_BUFSIZE);
            else
                resonant_filter = new fluid_iir_filter_32(synth.FLUID_BUFSIZE);
            // High pass filter useless: resonant_filter.fluid_iir_filter_init(fluid_iir_filter_type.FLUID_IIR_HIGHPASS, fluid_iir_filter_flags.FLUID_IIR_NOFLAGS);
            resonant_filter.fluid_iir_filter_init(fluid_iir_filter_type.FLUID_IIR_LOWPASS, fluid_iir_filter_flags.FLUID_IIR_NOFLAGS);
            //resonant_custom_filter.fluid_iir_filter_init(fluid_iir_filter_type.FLUID_IIR_DISABLED, fluid_iir_filter_flags.FLUID_IIR_NOFLAGS);

        }

        /* fluid_voice_init
         *
         * Initialize the synthesis process
         * inst_zone, the Instrument Zone contains the sample, Keyrange,Velrange
         * of the voice.
         * When playing legato (n1,n2) in mono mode, n2 will use n1 voices
         * as far as n2 still enters in Keyrange,Velrange of n1.
         * 
         * Defined default voice value. Called also when a voice is reused.
         */
        public void fluid_voice_init(
            MPTKChannel pchannel, int pkey, int pvel)
        /* not used from FS:
            // For legato mode (not in MPTK)  
            fluid_zone_range_t *inst_zone_range 

            // MPTK id already defined when voice is allocated
            // but with FS incremented when fluid_synth_release_voice_on_same_note_LOCAL is call ?
            unsigned int id

            // the number of audio samples since the start 
            unsigned int start_time = synth.ticks_since_start;    

            //  voice->dsp.synth_gain mainly used for amplitude_that_reaches_noise_floor
            float gain
        */

        {
            key = pkey;
            vel = pvel;
            channel = pchannel;
            chan = channel.Channel;
            mod_count = 0;
            // not yet used has_noteoff = false;

            #region From C:\Devel\fluidsynth-2.3.1\src\rvoice\fluid_rvoice.c

            // UPDATE_RVOICE0(fluid_rvoice_reset);
            // --------------------------------------------------------

            has_looped = false; // from voice->dsp.has_looped - Will be set during voice_write when the 2nd loop point is reached 
            ticks = 0; // from voice->envlfo.ticks
            // not yet used noteoff_ticks = 0;
            amp = 0.0f; /* The last value of the volume envelope, used to calculate the volume increment during processing */

            /* legato initialization */
            pitchoffset = 0f; /* portamento initialization */
            pitchinc = 0f;

            /* fluid_adsr_env_reset(envlfo.modenv) mod env initialization */
            modenv_count = 0;
            modenv_section = (fluid_voice_envelope_index)0;
            modenv_val = 0;

            /* fluid_adsr_env_reset(envlfo.volenv) vol env initialization */
            volenv_count = 0;
            volenv_section = (fluid_voice_envelope_index)0;
            volenv_val = 0;

            /* fluid_lfo_reset(envlfo.viblfo) vib lfo */
            viblfo_val = 0; /* Fixme: See mod lfo */

            /* fluid_lfo_reset(envlfo.modlfo) mod lfo */
            modlfo_val = 0;/* Fixme: Retrieve from any other existing voice on this channel to keep LFOs in unison? */

            /* Clear sample history in filter */
            resonant_filter.fluid_iir_filter_reset();

            check_sample_sanity_flag |= FLUID_SAMPLESANITY_STARTUP;

            #endregion

            /* removed from FS, with MPTK it's a global setting for all voices
                i = fluid_channel_get_interp_method(channel);
                UPDATE_RVOICE_I1(fluid_rvoice_set_interp_method, i);
            */

            /* Set all the generators to their default value, according to SF
             * 2.01 section 8.1.3 (page 48). The value of NRPN messages are
             * copied from the channel to the voice's generators. The sound font
             * loader overwrites them. The generator values are later converted
             * into voice parameters in
             * fluid_voice_calculate_runtime_synthesis_parameters.  */
            HiGen.fluid_gen_init(gen, channel);

            //UPDATE_RVOICE_I1(fluid_rvoice_set_samplemode, _SAMPLEMODE(voice));
            //#define _SAMPLEMODE(voice) ((int)(voice)->gen[GEN_SAMPLEMODE].val)
            samplemode = (fluid_loop)gen[(int)fluid_gen_type.GEN_SAMPLEMODE].Val;

            //gain=

#if DEBUGPERF
            synth.DebugPerf("After fluid_voice_init voice:");
#endif
        }


        /**
         * Adds a modulator to the voice if the modulator has valid sources.
         *
         * @param voice Voice instance.
         * @param mod Modulator info (copied).
         * @param mode Determines how to handle an existing identical modulator.
         *   #FLUID_VOICE_ADD to add (offset) the modulator amounts,
         *   #FLUID_VOICE_OVERWRITE to replace the modulator,
         *   #FLUID_VOICE_DEFAULT when adding a default modulator - no duplicate should
         *   exist so don't check.
         *   fluid_defpreset_noteon
         *      fluid_synth_alloc_voice_LOCAL
         *          fluid_voice_init - Initialize the synthesis process
         *          add the default modulators to the synthesis process.
         *      Add generators at instrument level
         *      fluid_defpreset_noteon_add_mod_to_voice  - Adds instrument zone modulators 
         *      Add generators at preset level 
         *      fluid_defpreset_noteon_add_mod_to_voice - Adds preset zone modulators
         *      fluid_synth_start_voice - add the synthesis process to the synthesis loop
         */
        public void fluid_voice_add_mod(HiMod mod, fluid_voice_addorover_mod mode)
        {
            /* Ignore the modulator if its sources inputs are invalid */
            if (mod.fluid_mod_check_sources("api fluid_voice_add_mod mod"))
            {
                fluid_voice_add_mod_local(mod, mode, HiMod.FLUID_NUM_MOD);
            }
        }

        /**
         * Adds a modulator to the voice.
         * local version of fluid_voice_add_mod function. Called at noteon time.
         * @param voice, mod, mode, same as for fluid_voice_add_mod() (see above).
         * @param check_limit_count is the modulator number limit to handle with existing
         *   identical modulator(i.e mode FLUID_VOICE_OVERWRITE, FLUID_VOICE_ADD).
         *   - When FLUID_NUM_MOD, all the voices modulators (since the previous call)
         *     are checked for identity.
         *   - When check_count_limit is below the actual number of voices modulators
         *   (voice->mod_count), this will restrict identity check to this number,
         *   This is useful when we know by advance that there is no duplicate with
         *   modulators at index above this limit. This avoid wasting cpu cycles at noteon.
         */
        public void fluid_voice_add_mod_local(HiMod mod, fluid_voice_addorover_mod mode, int check_limit_count)
        {
            int i;

            /* check_limit_count cannot be above voice->mod_count */
            if (check_limit_count > mods.Count)
            {
                check_limit_count = mods.Count;
            }

            if (mode == fluid_voice_addorover_mod.FLUID_VOICE_ADD)
            {

                /* if identical modulator exists, add them */
                for (i = 0; i < check_limit_count; i++)
                {
                    if (mods[i].fluid_mod_test_identity(mod))
                    {
                        //		printf("Adding modulator...\n");
                        mods[i].amount += mod.amount;
                        return;
                    }
                }
            }
            else if (mode == fluid_voice_addorover_mod.FLUID_VOICE_OVERWRITE)
            {

                /* if identical modulator exists, replace it (only the amount has to be changed) */
                for (i = 0; i < check_limit_count; i++)
                {
                    if (mods[i].fluid_mod_test_identity(mod))
                    {
                        //		printf("Replacing modulator...amount is %f\n",mod->amount);
                        mods[i].amount = mod.amount;
                        return;
                    }
                }
            }

            /* Add a new modulator (No existing modulator to add / overwrite).
               Also, default modulators (FLUID_VOICE_DEFAULT) are added without
               checking, if the same modulator already exists. */
            if (mod_count < HiMod.FLUID_NUM_MOD)
            {
                //fluid_mod_clone(&voice->mod[voice->mod_count++], mod);
                mods.Add(new HiMod()
                {
                    amount = mod.amount,
                    dest = mod.dest,
                    flags1 = mod.flags1,
                    flags2 = mod.flags2,
                    src1 = mod.src1,
                    src2 = mod.src2,
                });
            }
            else
            {
                Debug.LogWarning($"Voice {id} has more modulators than supported, ignoring.");
            }
        }

        public void fluid_voice_start()
        {
            // Find the exclusive class of this voice. If set, kill all voices that match the exclusive class 
            // and are younger than the first voice process created by this noteon event.
            if (synth.MPTK_KillByExclusiveClass)
                synth.fluid_synth_kill_by_exclusive_class(this);

            // The maximum volume of the loop is calculated and cached once for each sample with its nominal loop settings. 
            // This happens, when the sample is used for the first time.

            fluid_voice_calculate_runtime_synthesis_parameters();
#if DEBUGPERF
            synth.DebugPerf("After synthesis_parameters:");
#endif
            if (!weakDevice)
            {
                if (synth.VerboseEnvVolume)
                    for (int i = 0; i < (int)fluid_voice_envelope_index.FLUID_VOICE_ENVLAST; i++)
                        Debug.LogFormat("Volume Env. {0} {1,24} {2}", i, (fluid_voice_envelope_index)i, volenv_data[i].ToString());

                if (synth.VerboseEnvModulation)
                    for (int i = 0; i < (int)fluid_voice_envelope_index.FLUID_VOICE_ENVLAST; i++)
                        Debug.LogFormat("Modulation Env. {0} {1,24} {2}", i, (fluid_voice_envelope_index)i, modenv_data[i].ToString());

                // precalculation disabled, may cause issue on iOS and low output rate - V2.89.0 
                // if (!synth.AdsrSimplified)
                //{
                // Precalculate env. volume
                //fluid_env_data env_data = volenv_data[(int)volenv_section];
                //while (env_data.count <= 0d && (int)volenv_section < volenv_data.Length)
                //{
                //    float lastmax = env_data.max; ;
                //    volenv_section++;
                //    env_data = volenv_data[(int)volenv_section];
                //    //volenv_count = 0d;
                //    volenv_val = lastmax;
                //    if (synth.VerboseEnvVolume) Debug.LogFormat("Volume Precalculate Env. Count -. section:{0}  new count:{1} volenv_val:{2}", (int)volenv_section, env_data.count, volenv_val);
                //}

                //if (synth.VerboseEnvVolume)
                //    Debug.LogFormat("After precalc. volenv_section {0} ", volenv_section);


                //// Precalculate env. modulation
                //env_data = modenv_data[(int)modenv_section];
                //while (env_data.count <= 0d && (int)modenv_section < modenv_data.Length)
                //{
                //    float lastmax = env_data.max;
                //    modenv_section++;
                //    env_data = modenv_data[(int)modenv_section];
                //    modenv_count = 0;
                //    modenv_val = lastmax;
                //    if (synth.VerboseEnvModulation) Debug.LogFormat("Modulation Precalculate Env. Count -. section:{0}  new count:{1} modenv_val:{2}", (int)modenv_section, env_data.count, modenv_val);
                //}
                //}
                if (synth.AdsrSimplified)
                {
                    volenv_section = fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY;
                    volenv_val = 1;
                    volenv_count = volenv_data[(int)volenv_section].count;

                    modenv_section = fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY;
                    modenv_val = 1;
                    modenv_count = modenv_data[(int)modenv_section].count;
                }
            }
            else
                volenv_val = 1;


            StartVolume = synth.MPTK_Volume * (float)(fluid_conv.fluid_cb2amp(attenuation) * fluid_conv.fluid_cb2amp(960.0f * (1f - volenv_val)));
            //IsLoop = ((gen[(int)fluid_gen_type.GEN_SAMPLEMODE].Val == (double)fluid_loop.FLUID_LOOP_UNTIL_RELEASE) || (gen[(int)fluid_gen_type.GEN_SAMPLEMODE].Val == (double)fluid_loop.FLUID_LOOP_DURING_RELEASE));

            // Force setting of the phase at the first DSP loop run This cannot be done earlier, because it depends on modulators.
            check_sample_sanity_flag = FLUID_SAMPLESANITY_STARTUP;

            // Voice with status FLUID_VOICE_ON are played in background when CorePlayer is enabled
            status = fluid_voice_status.FLUID_VOICE_ON;
            LatenceTick = -1L;

            // A single tick represents one hundred nanoseconds or one ten-millionth of a second.
            // There are 10,000 ticks in a millisecond, or 10 million ticks in a second. 
            // Substrack 2 milliseconds to avoid waiting in fluid_voice_write (root cause not found ... )
            TimeAtStart = DateTime.UtcNow.Ticks - 20000;
            if (MptkEvent.Delay > 0)
                TimeAtStart += MptkEvent.Delay * fluid_voice.Nano100ToMilli;
            TimeAtEnd = DurationTick > -1 ? TimeAtStart + DurationTick : long.MaxValue;
            LastTimeWrite = TimeAtStart;
            //time = 0.0;

            if (VoiceAudio != null)
                // Play sound with an AudioSource component (legacy mode)
                VoiceAudio.RunUnityThread();
        }

        /// <summary>@brief
        /// in this function we calculate the values of all the parameters. the parameters are converted to their most useful unit for the DSP algorithm, 
        /// for example, number of samples instead of timecents.
        /// Some parameters keep their "perceptual" unit and conversion will be done in the DSP function.
        /// This is the case, for example, for the pitch since it is modulated by the controllers in cents.
        /// </summary>
        void fluid_voice_calculate_runtime_synthesis_parameters()
        {
            // When the voice is made ready for the synthesis process, a lot of voice-internal parameters have to be calculated.
            // At this point, the sound font has already set the -nominal- value for all generators (excluding GEN_PITCH). 
            // Most generators can be modulated - they include a nominal value and an offset (which changes with velocity, note number, channel parameters like
            // aftertouch, mod wheel...) 
            // Now this offset will be calculated as follows:
            //  - Process each modulator once.
            //  - Calculate its output value.
            //  - Find the target generator.
            //  - Add the output value to the modulation value of the generator.
            // Note: The generators have been initialized with fluid_gen_set_default_values.

            //foreach (HiMod m in mods) Debug.Log(m.ToString());

            /* From FS 2.3
              for (i = 0; i < voice->mod_count; i++)
                {
                    fluid_mod_t *mod = &voice->mod[i];
                    fluid_real_t modval = fluid_mod_get_value(mod, voice);
                    int dest_gen_index = m.dest;
                    fluid_gen_t *dest_gen = &voice->gen[dest_gen_index];
                    dest_gen->mod += modval;
        
                    fluid_dump_modulator(mod);
                }
             */

            foreach (HiMod mod in mods)
            {
                //if (mod.dest == (int)fluid_gen_type.GEN_ATTENUATION)
                //    Debug.Log("GEN_ATTENUATION");

                float valMod = mod.fluid_mod_get_value(this);

                if (synth.VerboseCalcMod) mod.fluid_dump_modulator(valMod);

                gen[mod.dest].Mod += valMod;
            }

            /* Now the generators are initialized, nominal and modulation value.
             * The voice parameters (which depend on generators) are calculated
             * with fluid_voice_update_param. Processing the list of generator
             * changes will calculate each voice parameter once.
             *
             * Note [1]: Some voice parameters depend on several generators. For
             * example, the pitch depends on GEN_COARSETUNE, GEN_FINETUNE and
             * GEN_PITCH.  voice.pitch.  Unnecessary recalculation is avoided
             * by removing all but one generator from the list of voice
             * parameters.  Same with GEN_XXX and GEN_XXXCOARSE: the
             * initialisation list contains only GEN_XXX.
             */

            // Calculate the voice parameter(s) dependent on each generator.
            int[] list_of_generators;
            if (!weakDevice)
                if (synth.MPTK_CorePlayer)
                    list_of_generators = list_of_generators_to_initialize;
                else
                    list_of_generators = list_of_non_core_generators_to_initialize;
            else
                list_of_generators = list_of_weakgenerators_to_initialize;

            if (synth.VerboseCalcGen)
            {
                Debug.Log("list_of_generators_to_initialize:");
                for (int n = 0; n < list_of_generators.Length; n++)
                {
                    int g = list_of_generators[n];
                    Debug.Log($"  ({g,-2:00}) {(fluid_gen_type)g,-25} {(int)gen[g].flags} val:{gen[g].Val,10:F2} mod:{gen[g].Mod,10:F2}");
                }
                Debug.Log("fluid_voice_update_param:");
            }
            foreach (int igen in list_of_generators)
                try
                {
                    fluid_voice_update_param(igen);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Channel:{chan} Sample:{sample.Name} Generator:[{igen} {(fluid_gen_type)igen}] {ex.Message}");
                }

            /* Start portamento if enabled TO BE DONE
            {
                // fromkey note comes from "GetFromKeyPortamentoLegato()" detector.
                // When fromkey is set to ValidNote , portamento is started 
                //  Return fromkey portamento 
                int fromkey = voice->channel->synth->fromkey_portamento;

                if (fluid_channel_is_valid_note(fromkey))
                {
                    // Send portamento parameters to the voice dsp 
                    fluid_voice_update_portamento(voice, fromkey, fluid_voice_get_actual_key(voice));
                }
            } */

            // Make an estimate on how loud this voice can get at any time (attenuation). */
            min_attenuation_cB = fluid_voice_get_lower_boundary_for_attenuation();
        }


        /*
         * fluid_voice_get_lower_boundary_for_attenuation
         *
         * Purpose:
         *
         * A lower boundary for the attenuation (as in 'the minimum
         * attenuation of this voice, with volume pedals, modulators
         * etc. resulting in minimum attenuation, cannot fall below x cB) is
         * calculated.  This has to be called during fluid_voice_init, after
         * all modulators have been run on the voice once.  Also,
         * voice.attenuation has to be initialized.
         */
        float fluid_voice_get_lower_boundary_for_attenuation()
        {
            float possible_att_reduction_cB = 0;
            float lower_bound;

            foreach (HiMod m in mods)
            {
                // Modulator has attenuation as target and can change over time? 
                if (m.dest == (int)fluid_gen_type.GEN_ATTENUATION)
                {
                    if (((m.flags1 & (byte)fluid_mod_flags.FLUID_MOD_CC) > 0 || (m.flags2 & (byte)fluid_mod_flags.FLUID_MOD_CC) > 0) ||
                         (m.src1 == (byte)fluid_mod_src.FLUID_MOD_CHANNELPRESSURE) || (m.src1 == (byte)fluid_mod_src.FLUID_MOD_KEYPRESSURE) ||
                         (m.src1 == (byte)fluid_mod_src.FLUID_MOD_PITCHWHEEL) || (m.src2 == (byte)fluid_mod_src.FLUID_MOD_CHANNELPRESSURE) ||
                         (m.src2 == (byte)fluid_mod_src.FLUID_MOD_KEYPRESSURE) || (m.src2 == (byte)fluid_mod_src.FLUID_MOD_PITCHWHEEL))
                    {

                        float current_val = m.fluid_mod_get_value(this);
                        float min_val = Mathf.Abs(m.amount);

                        if ((m.flags1 & (byte)fluid_mod_flags.FLUID_MOD_BIPOLAR) > 0 ||
                            (m.flags2 & (byte)fluid_mod_flags.FLUID_MOD_BIPOLAR) > 0 ||
                            (m.amount < 0))
                        {
                            min_val = -min_val; /* min_val = - |amount|*/
                        }
                        else
                        {
                            /* No negative value possible. But still, the minimum contribution is 0. */
                            min_val = 0f;
                        }

                        /* For example:
                         * - current_val=100
                         * - min_val=-4000
                         * - possible_att_reduction_cB += 4100
                         */
                        if (synth.VerboseCalcMod) Debug.Log($"fluid_voice_get_lower_boundary_for_attenuation {m.dest} current_val:{current_val} min_val: {min_val}");
                        if (current_val > min_val)
                        {
                            possible_att_reduction_cB += (current_val - min_val);
                        }
                    }
                }
            }

            lower_bound = attenuation - possible_att_reduction_cB;

            // Same values with FS2.3
            // C60-100 possible_att_reduction_cB 44.25, lower_bound 57.52
            // C60-30  possible_att_reduction_cB:44,24804 lower_bound:220,5466 
            if (synth.VerboseCalcMod) Debug.Log($"fluid_voice_get_lower_boundary_for_attenuation possible_att_reduction_cB:{possible_att_reduction_cB} lower_bound:{lower_bound} ");

            /* SF2.01 specs do not allow negative attenuation */
            if (lower_bound < 0f)
            {
                lower_bound = 0f;
            }
            return lower_bound;
        }
        /// <summary>@brief
        /// The value of a generator (gen) has changed.  (The different generators are listed in fluidsynth.h, or in SF2.01 page 48-49). Now the dependent 'voice' parameters are calculated.
        /// fluid_voice_update_param can be called during the setup of the  voice (to calculate the initial value for a voice parameter), or
        /// during its operation (a generator has been changed due to real-time parameter modifications like pitch-bend).
        /// Note: The generator holds three values: The base value .val, an offset caused by modulators .mod, and an offset caused by the
        /// NRPN system. _GEN(voice, generator_enumerator) returns the sum of all three.
        /// From fluid_midi_send_event NOTE_ON -. synth_noteon -. fluid_voice_start -. fluid_voice_calculate_runtime_synthesis_parameters
        /// From fluid_midi_send_event CONTROL_CHANGE -. fluid_synth_cc -. fluid_channel_cc Default      -. fluid_synth_modulate_voices     -. fluid_voice_modulate
        /// From fluid_midi_send_event CONTROL_CHANGE -. fluid_synth_cc -. fluid_channel_cc ALL_CTRL_OFF -. fluid_synth_modulate_voices_all -. fluid_voice_modulate_all
        /// </summary>
        /// <param name="igen"></param>
        public void fluid_voice_update_param(int igen)
        {
            //Debug.Log("fluid_voice_update_param " + (fluid_gen_type)igen);
            float genVal = CalculateGeneratorValue(igen);
            string header = null;
            if (synth.VerboseCalcGen || synth.VerboseCalcVolADSR || synth.VerboseCalcModADSR)
            {
                header = $"Calc ({igen,2:00}) {(fluid_gen_type)igen,-25}";
            }
            switch (igen)
            {
                case (int)fluid_gen_type.GEN_PAN:
                    /* range checking is done in the fluid_pan function: range from -500 to 500 */
                    pan = genVal;
                    // Init with default volume channel value
                    amp_left = channel.Volume;
                    amp_right = channel.Volume;

                    if (synth.MPTK_EnablePanChange)
                    {
                        if (synth.MPTK_CorePlayer)
                        {
                            float left = fluid_conv.fluid_pan(pan, true); // from 0 to 1
                            float right = fluid_conv.fluid_pan(pan, false); // from 0 to 1 
                            amp_left *= left;
                            amp_right *= right;
                            if (synth.VerboseCalcGen)
                                Debug.LogFormat($"{header} EnablePanChange={synth.MPTK_EnablePanChange} synth.gain={synth.gain:0.00} pan={pan:0.00} amp_left={left:0.00} amp_right={right:0.00} mptkChannel.volume={channel.Volume}");
                        }
                        else
                        {
                            // Not useful! 
                            // Pan change is handle in VoiceAudioSource.cs
                            //if (VoiceAudio != null && VoiceAudio.Audiosource != null)
                            //    // Values range from -1.0 to 1.0.
                            //    // -1.0 = full left
                            //    //  0.0 = center
                            //    //  1.0 = full right
                            //    VoiceAudio.Audiosource.panStereo = pan / 500f; // from pan -500 to 500 to audio source pan -1 to 1
                            //if (synth.VerboseCalcGen)
                            //    Debug.LogFormat($"{header} EnablePanChange={synth.MPTK_EnablePanChange} pan={pan:0.00} --> panStereo={VoiceAudio.Audiosource.panStereo:0.00}");
                        }
                    }
                    break;

                case (int)fluid_gen_type.GEN_ATTENUATION:
                    // Range: SF2.01 section 8.1.3 # 48 Motivation for range checking:OHPiano.SF2 sets initial attenuation to a whooping -96 dB
                    attenuation = genVal < 0.0f ? 0.0f : genVal > 1440.0f ? 1440.0f : genVal;
                    if (synth.VerboseCalcGen) Debug.LogFormat($"{header} Val={gen[igen].Val} Mod={gen[igen].Mod} attenuation:{genVal}");
                    break;

                // The pitch is calculated from the current note 
                case (int)fluid_gen_type.GEN_PITCH:
                case (int)fluid_gen_type.GEN_COARSETUNE:
                case (int)fluid_gen_type.GEN_FINETUNE:
                    // The testing for allowed range is done in 'fluid_ct2hz' 
                    pitch = CalculateGeneratorValue((int)fluid_gen_type.GEN_PITCH) +
                            CalculateGeneratorValue((int)fluid_gen_type.GEN_COARSETUNE) * 100f +
                            CalculateGeneratorValue((int)fluid_gen_type.GEN_FINETUNE);

                    if (synth.VerboseCalcGen) Debug.LogFormat($"{header} pitch={pitch:0.00}");
                    break;

                case (int)fluid_gen_type.GEN_REVERBSEND:
                    {
                        /* The generator unit is 'tenths of a percent'. */
                        float send = genVal / 1000f;
                        reverb_send = send < 0f ? 0f : send > 1f ? 1f : send;
                        if (synth.VerboseCalcGen)
                            Debug.LogFormat($"{header} genVal={genVal} reverb_send={send} amp_reverb={reverb_send}");
                        //Debug.LogFormat($"genVal={genVal} reverb_send={send} amp_reverb={reverb_send}");
                    }
                    break;

                case (int)fluid_gen_type.GEN_CHORUSSEND:
                    {
                        /* The generator unit is 'tenths of a percent'. */
                        float send = genVal / 1000f;
                        chorus_send = send < 0f ? 0f : send > 1f ? 1f : send;
                        if (synth.VerboseCalcGen)
                            Debug.LogFormat($"{header} chorus_send={send} amp_chorus={chorus_send}");
                    }
                    break;

                case (int)fluid_gen_type.GEN_OVERRIDEROOTKEY:
                    {
                        float x = 0;
                        float val = gen[(int)fluid_gen_type.GEN_OVERRIDEROOTKEY].Val;
                        // This is a non-realtime parameter. Therefore the .mod part of the generator can be neglected.
                        // NOTE: origpitch sets MIDI root note while pitchadj is a fine tuning amount which offsets the original rate.  
                        // This means that the fine tuning is inverted with respect to the root note (so subtract it, not add).
                        if (val > -1)
                        {
                            //FIXME: use flag instead of -1
                            root_pitch = val * 100.0f - sample.PitchAdj;
                        }
                        else
                        {
                            root_pitch = sample.OrigPitch * 100.0f - sample.PitchAdj;
                        }

                        if (synth.MPTK_CorePlayer)
                        {
                            /*was root_pitch*/
                            x = (float)(fluid_conv.fluid_ct2hz(root_pitch) * ((double)output_rate / (double)sample.SampleRate));
                            //Debug.Log($"{sample.Name} output_rate:{output_rate} SampleRate:{sample.SampleRate}");
                        }

                        // set  voice->dsp.root_pitch_hz
                        //UPDATE_RVOICE_R1(fluid_rvoice_set_root_pitch_hz, x);
                        root_pitch_hz = x;

                        /* voice->pitch depends on voice->root_pitch, so calculate voice->pitch now */
                        //fluid_voice_calculate_gen_pitch(voice);
                        //     voice->gen[GEN_PITCH].val = fluid_voice_calculate_pitch(voice, fluid_voice_get_actual_key(voice));
                        gen[(int)fluid_gen_type.GEN_PITCH].Val = fluid_voice_calculate_pitch(fluid_voice_get_actual_key());
                        if (synth.VerboseCalcGen)
                            Debug.LogFormat($"{header} key:{fluid_voice_get_actual_key()} pitch.val:{gen[(int)fluid_gen_type.GEN_PITCH].Val} root_pitch:{root_pitch} PitchAdj:{sample.PitchAdj}");
                    }
                    break;

                case (int)fluid_gen_type.GEN_FILTERFC:
                    // The resonance frequency is converted from absolute cents to midicents .val and .mod are both used, this permits real-time
                    // modulation.  The allowed range is tested in the 'fluid_ct2hz' function [PH,20021214]
                    fres = genVal;
                    resonant_filter.fluid_iir_filter_set_fres(fres);
                    if (synth.VerboseCalcGen) Debug.LogFormat($"{header} fres:{fres}");
                    break;

                case (int)fluid_gen_type.GEN_FILTERQ:
                    // The generator contains 'centibels' (1/10 dB) => divide by 10 to obtain dB
                    // removes with FS 2.3 q_dB = genVal / 10f;
                    q_dB = genVal;
                    //// SF 2.01 page 59: The SoundFont specs ask for a gain reduction equal to half the height of the resonance peak (Q).  For example, for a 10 dB
                    ////  resonance peak, the gain is reduced by 5 dB.  This is done by multiplying the total gain with sqrt(1/Q).  `Sqrt' divides dB by 2 
                    //// (100 lin = 40 dB, 10 lin = 20 dB, 3.16 lin = 10 dB etc)
                    ////  The gain is later factored into the 'b' coefficients  (numerator of the filter equation).  This gain factor depends
                    ////  only on Q, so this is the right place to calculate it.
                    //filter_gain = 1f / Mathf.Sqrt(q_lin);

                    //// The synthesis loop will have to recalculate the filter coefficients. */
                    //last_fres = -1f;
#if MPTK_PRO
                    resonant_filter.fluid_iir_filter_set_q(q_dB, synth.MPTK_EffectSoundFont.FilterQModOffset);
                    if (synth.VerboseCalcGen) Debug.LogFormat($"{header} q:{q_dB} MPTK_SFFilterQModOffset:{synth.MPTK_EffectSoundFont.FilterQModOffset}");
#else
                    resonant_filter.fluid_iir_filter_set_q(q_dB,0);
                    if (synth.VerboseCalcGen) Debug.LogFormat($"{header} q:{q_dB}");
#endif
                    break;

                case (int)fluid_gen_type.GEN_MODLFOTOPITCH:
                    modlfo_to_pitch = genVal < -12000f ? -12000f : genVal > 12000f ? 12000f : genVal;
                    if (synth.VerboseCalcGen) Debug.LogFormat($"{header} {modlfo_to_pitch}");
                    break;

                case (int)fluid_gen_type.GEN_MODLFOTOVOL:
                    modlfo_to_vol = genVal < -960f ? -960f : genVal > 960f ? 960f : genVal;
                    if (synth.VerboseCalcGen) Debug.LogFormat($"{header} {modlfo_to_vol}");
                    break;

                case (int)fluid_gen_type.GEN_MODLFOTOFILTERFC:
                    modlfo_to_fc = genVal < -12000f ? -12000f : genVal > 12000f ? 12000f : genVal;
                    if (synth.VerboseCalcGen) Debug.LogFormat($"{header} {modlfo_to_fc}");
                    break;

                case (int)fluid_gen_type.GEN_MODLFODELAY:
                    {
                        float x = genVal < -12000f ? -12000f : genVal > 5000f ? 5000f : genVal;
                        modlfo_delay = (uint)(output_rate * fluid_conv.fluid_tc2sec_delay(x));
                        if (synth.VerboseCalcGen) Debug.LogFormat($"{header} time:{fluid_conv.fluid_tc2sec_delay(x):F4} s.");
                    }
                    break;

                case (int)fluid_gen_type.GEN_MODLFOFREQ:
                    {
                        //the frequency is converted into a delta value, per buffer of FLUID_BUFSIZE samples.
                        //the delay into a sample delay
                        float x = genVal < -16000.0f ? -16000.0f : genVal > 4500.0f ? 4500.0f : genVal;
                        modlfo_incr = (4.0f * synth.FLUID_BUFSIZE * (float)fluid_conv.fluid_ct2hz_real(x) / output_rate);
                        //modlfo_incr = (4.0f * synth.FLUID_BUFSIZE * (float)fluid_conv.fluid_act2hz(x) / output_rate);
                        if (synth.VerboseCalcGen)
                            Debug.LogFormat($"{header} freq:{(float)fluid_conv.fluid_ct2hz_real(x):F4}");
                    }
                    break;

                case (int)fluid_gen_type.GEN_VIBLFOFREQ:
                    {
                        // the frequency is converted into a delta value, per buffer of FLUID_BUFSIZE samples
                        // the delay into a sample delay
                        float x = genVal < -16000.0f ? -16000.0f : genVal > 4500.0f ? 4500.0f : genVal;
                        viblfo_incr = (4.0f * synth.FLUID_BUFSIZE * (float)fluid_conv.fluid_ct2hz_real(x) / output_rate);
                        //viblfo_incr = (4.0f * synth.FLUID_BUFSIZE * (float)fluid_conv.fluid_act2hz(x) / output_rate);
                        if (synth.VerboseCalcGen) Debug.LogFormat($"{header} freq:{(float)fluid_conv.fluid_ct2hz_real(x):F4}");
                    }
                    break;

                case (int)fluid_gen_type.GEN_VIBLFODELAY:
                    {
                        float x = genVal < -12000f ? -12000f : genVal > 5000f ? 5000f : genVal;
                        viblfo_delay = (uint)(output_rate * fluid_conv.fluid_tc2sec_delay(x));
                        if (synth.VerboseCalcGen) Debug.LogFormat($"{header} time:{fluid_conv.fluid_tc2sec_delay(x):F4} s.");
                    }
                    break;

                case (int)fluid_gen_type.GEN_VIBLFOTOPITCH:
                    viblfo_to_pitch = genVal < -12000f ? -12000f : genVal > 12000f ? 12000f : genVal;
                    if (synth.VerboseCalcGen) Debug.LogFormat($"{header} {viblfo_to_pitch:F4}");
                    break;

                case (int)fluid_gen_type.GEN_KEYNUM:
                    {
                        /* GEN_KEYNUM: SF2.01 page 46, item 46
                         *
                         * If this generator is active, it forces the key number to its
                         * value.  Non-realtime controller.
                         *
                         * There is a flag, which should indicate, whether a generator is
                         * enabled or not.  But here we rely on the default value of -1.
                         */

                        /* 2017-09-02: do not change the voice's key here, otherwise it will
                         * never be released on a noteoff event
                         */

                        // GEN_KEYNUM: SF2.01 page 46, item 46
                        // If this generator is active, it forces the key number to its value.  Non-realtime controller.
                        // There is a flag, which should indicate, whether a generator is enabled or not.  But here we rely on the default value of -1.
                        //int x = Convert.ToInt32(genVal);
                        //if (x >= 0) key = x;
                    }
                    break;

                case (int)fluid_gen_type.GEN_VELOCITY:
                    {
                        // GEN_VELOCITY: SF2.01 page 46, item 47
                        // If this generator is active, it forces the velocity to its value. Non-realtime controller.
                        // There is a flag, which should indicate, whether a generator is enabled or not. But here we rely on the default value of -1. 

                        // Commented with FS 2.3
                        //int x = Convert.ToInt32(genVal);
                        //if (x >= 0) vel = x;

                        //Debug.Log(string.Format("fluid_voice_update_param {0} vel={1} ", (fluid_gen_type)igen, x));

                    }
                    break;

                case (int)fluid_gen_type.GEN_MODENVTOPITCH:
                    modenv_to_pitch = genVal < -12000.0f ? -12000.0f : genVal > 12000.0f ? 12000.0f : genVal;
                    if (synth.VerboseCalcGen) Debug.LogFormat($"{header} {modenv_to_pitch}");
                    break;

                case (int)fluid_gen_type.GEN_MODENVTOFILTERFC:
                    // Range: SF2.01 section 8.1.3 # 1
                    // Motivation for range checking:Filter is reported to make funny noises now 
                    modenv_to_fc = genVal < -12000.0f ? -12000.0f : genVal > 12000.0f ? 12000.0f : genVal;
                    if (synth.VerboseCalcGen) Debug.LogFormat($"{header} {modenv_to_fc}");
                    break;

                // sample start and ends points
                // Range checking is initiated via the check_sample_sanity flag, because it is impossible to check here:
                // During the voice setup, all modulators are processed, while the voice is inactive. Therefore, illegal settings may
                // occur during the setup (for example: First move the loop end point ahead of the loop start point => invalid, then move the loop start point forward => valid again.
                // Unity adaptation: wave are played from a wave file not from a global data buffer. It's not possible de change these
                // value after importing the SoudFont. Only loop address are taken in account whrn importing the SF
                case (int)fluid_gen_type.GEN_STARTADDROFS:              /* SF2.01 section 8.1.3 # 0 */
                case (int)fluid_gen_type.GEN_STARTADDRCOARSEOFS:        /* SF2.01 section 8.1.3 # 4 */
                    if (sample != null)
                    {
                        start = (int)(sample.Start
                            + (int)gen[(int)fluid_gen_type.GEN_STARTADDROFS].Val + gen[(int)fluid_gen_type.GEN_STARTADDROFS].Mod /*+ gens[(int)fluid_gen_type.GEN_STARTADDROFS].nrpn*/
                            + 32768 * (int)gen[(int)fluid_gen_type.GEN_STARTADDRCOARSEOFS].Val + gen[(int)fluid_gen_type.GEN_STARTADDRCOARSEOFS].Mod /*+ gens[(int)fluid_gen_type.GEN_STARTADDRCOARSEOFS].nrpn*/);
                        if (sample.Data != null && start >= sample.Data.Length) start = sample.Data.Length - 1;
                        check_sample_sanity_flag |= FLUID_SAMPLESANITY_CHECK;
                        //if (synth.VerboseCalcGen) Debug.LogFormat("Calc {0} start={1} val={2:0.00} mod={3:0.00}", (fluid_gen_type)igen, start, gens[igen].Val, gens[igen].Mod);
                    }
                    break;
                case (int)fluid_gen_type.GEN_ENDADDROFS:                 /* SF2.01 section 8.1.3 # 1 */
                case (int)fluid_gen_type.GEN_ENDADDRCOARSEOFS:           /* SF2.01 section 8.1.3 # 12 */
                    if (sample != null)
                    {
                        end = (int)(sample.End - 1
                            + (int)gen[(int)fluid_gen_type.GEN_ENDADDROFS].Val + gen[(int)fluid_gen_type.GEN_ENDADDROFS].Mod /*+ gens[(int)fluid_gen_type.GEN_ENDADDROFS].nrpn*/
                            + 32768 * (int)gen[(int)fluid_gen_type.GEN_ENDADDRCOARSEOFS].Val + gen[(int)fluid_gen_type.GEN_ENDADDRCOARSEOFS].Mod /*+ gens[(int)fluid_gen_type.GEN_ENDADDRCOARSEOFS].nrpn*/);
                        if (sample.Data != null && end >= sample.Data.Length) end = sample.Data.Length - 1;
                        check_sample_sanity_flag |= FLUID_SAMPLESANITY_CHECK;
                        //if (synth.VerboseCalcGen) Debug.LogFormat("Calc {0} end={1} val={2:0.00} mod={3:0.00}", (fluid_gen_type)igen, end, gens[igen].Val, gens[igen].Mod);
                    }
                    break;
                case (int)fluid_gen_type.GEN_STARTLOOPADDROFS:           /* SF2.01 section 8.1.3 # 2 */
                case (int)fluid_gen_type.GEN_STARTLOOPADDRCOARSEOFS:     /* SF2.01 section 8.1.3 # 45 */
                    if (sample != null)
                    {
                        loopstart =
                            (int)(sample.LoopStart +
                            (int)gen[(int)fluid_gen_type.GEN_STARTLOOPADDROFS].Val +
                            gen[(int)fluid_gen_type.GEN_STARTLOOPADDROFS].Mod +
                            //gens[(int)fluid_gen_type.GEN_STARTLOOPADDROFS].nrpn +
                            32768 * (int)gen[(int)fluid_gen_type.GEN_STARTLOOPADDRCOARSEOFS].Val +
                            gen[(int)fluid_gen_type.GEN_STARTLOOPADDRCOARSEOFS].Mod /*+ gens[(int)fluid_gen_type.GEN_STARTLOOPADDRCOARSEOFS].nrpn*/);
                        if (loopstart >= sample.Data.Length) loopstart = sample.Data.Length - 1;
                        check_sample_sanity_flag |= FLUID_SAMPLESANITY_CHECK;
                        //if (synth.VerboseCalcGen) Debug.LogFormat("Calc {0} loopstart={1} val={2:0.00} mod={3:0.00}", (fluid_gen_type)igen, loopstart, gens[igen].Val, gens[igen].Mod);
                    }
                    break;

                case (int)fluid_gen_type.GEN_ENDLOOPADDROFS:             /* SF2.01 section 8.1.3 # 3 */
                case (int)fluid_gen_type.GEN_ENDLOOPADDRCOARSEOFS:       /* SF2.01 section 8.1.3 # 50 */
                    if (sample != null)
                    {
                        loopend =
                            (int)(sample.LoopEnd +
                            (int)gen[(int)fluid_gen_type.GEN_ENDLOOPADDROFS].Val +
                            gen[(int)fluid_gen_type.GEN_ENDLOOPADDROFS].Mod
                            //+ gens[(int)fluid_gen_type.GEN_ENDLOOPADDROFS].nrpn
                            + 32768 * (int)gen[(int)fluid_gen_type.GEN_ENDLOOPADDRCOARSEOFS].Val +
                            gen[(int)fluid_gen_type.GEN_ENDLOOPADDRCOARSEOFS].Mod
                            /*+ gens[(int)fluid_gen_type.GEN_ENDLOOPADDRCOARSEOFS].nrpn*/);
                        if (loopend >= sample.Data.Length) loopend = sample.Data.Length - 1;
                        check_sample_sanity_flag |= FLUID_SAMPLESANITY_CHECK;
                        //if (synth.VerboseCalcGen) Debug.LogFormat("Calc {0} loopend={1} val={2:0.00} mod={3:0.00}", (fluid_gen_type)igen, loopend, gens[igen].Val, gens[igen].Mod);
                    }
                    break;

                //
                // Volume ADSR
                // -----------

                // - delay and hold times are converted to absolute number of samples
                // - sustain is converted to its absolute value
                // - attack, decay and release are converted to their increment per sample
                case (int)fluid_gen_type.GEN_VOLENVDELAY:                /* SF2.01 section 8.1.3 # 33 */
                    {
                        float x = genVal < -12000f ? -12000f : genVal > 5000f ? 5000f : genVal;

                        uint count = synth.MPTK_CorePlayer ?
                                        // NUM_BUFFERS_DELAY
                                        (uint)(output_rate * fluid_conv.fluid_tc2sec_delay(x) / synth.FLUID_BUFSIZE) :
                                        (uint)(Nano100ToMilli * fluid_conv.fluid_tc2sec_delay(x) * 1000f);
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].count = count;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].coeff = 0f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].incr = 0f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].min = -1f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].max = 1f;
                        if (synth.VerboseCalcVolADSR)
                        {
                            float delaySec = (float)fluid_conv.fluid_tc2sec_delay(x);
                            Debug.Log($"{header} count:{count,-5} time:{delaySec:F4} s. ");
                        }
                    }
                    break;

                case (int)fluid_gen_type.GEN_VOLENVATTACK:               /* SF2.01 section 8.1.3 # 34 */
                    {
                        float x = genVal < -12000f ? -12000f : genVal > 8000f ? 8000f : genVal;

                        uint count = synth.MPTK_CorePlayer ?
                                        // NUM_BUFFERS_ATTACK
                                        1 + (uint)(output_rate * fluid_conv.fluid_tc2sec_attack(x) / synth.FLUID_BUFSIZE) :
                                        (uint)(Nano100ToMilli * fluid_conv.fluid_tc2sec_attack(x) * 1000f);

                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].count = count;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].coeff = 1f;
                        float incr = count != 0 ? 1f / count : 0f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].incr = incr;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].min = -1f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].max = 1f;
                        if (synth.VerboseCalcVolADSR)
                        {
                            float attackSec = (float)fluid_conv.fluid_tc2sec_attack(x);
                            Debug.Log($"{header} count:{count,-5} time:{attackSec:F4} s.");
                        }
                    }
                    break;

                case (int)fluid_gen_type.GEN_VOLENVHOLD:                 /* SF2.01 section 8.1.3 # 35 */
                case (int)fluid_gen_type.GEN_KEYTOVOLENVHOLD:            /* SF2.01 section 8.1.3 # 39 */
                    {
                        uint count = synth.MPTK_CorePlayer ?
                            calculate_hold_decay_buffers((int)fluid_gen_type.GEN_VOLENVHOLD, (int)fluid_gen_type.GEN_KEYTOVOLENVHOLD, false) : /* 0 means: hold */
                            (uint)(Nano100ToMilli * calculate_hold_decay_ms((int)fluid_gen_type.GEN_VOLENVHOLD, (int)fluid_gen_type.GEN_KEYTOVOLENVHOLD, false)); /* 0 means: hold */

                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].count = count;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].coeff = 1f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].incr = 0f; // Volume stay stable during hold phase
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].min = -1f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].max = 2f;// was 1 with 2.05;
                        if (synth.VerboseCalcVolADSR)
                        {
                            float holdSec = calculate_hold_decay_buffers_seconds(
                                (int)fluid_gen_type.GEN_VOLENVHOLD,
                                (int)fluid_gen_type.GEN_KEYTOVOLENVHOLD, false);
                            Debug.Log($"{header} count:{count,-5} time:{holdSec:F4} s.");
                        }
                    }
                    break;

                case (int)fluid_gen_type.GEN_VOLENVDECAY:               /* SF2.01 section 8.1.3 # 36 */
                case (int)fluid_gen_type.GEN_VOLENVSUSTAIN:             /* SF2.01 section 8.1.3 # 37 */
                case (int)fluid_gen_type.GEN_KEYTOVOLENVDECAY:          /* SF2.01 section 8.1.3 # 40 */
                    {
                        // x = 1.0f - 0.001f * fluid_voice_gen_value(voice, GEN_VOLENVSUSTAIN);
                        float genv = CalculateGeneratorValue((int)fluid_gen_type.GEN_VOLENVSUSTAIN);
                        float x = 1f - 0.001f * genv;
                        x = x < 0f ? 0f : x > 1f ? 1f : x;

                        uint count = synth.MPTK_CorePlayer ?
                            calculate_hold_decay_buffers((int)fluid_gen_type.GEN_VOLENVDECAY, (int)fluid_gen_type.GEN_KEYTOVOLENVDECAY, true)
                            :
                            (uint)(Nano100ToMilli * calculate_hold_decay_ms((int)fluid_gen_type.GEN_VOLENVDECAY, (int)fluid_gen_type.GEN_KEYTOVOLENVDECAY, true));
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].count = count;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].coeff = 1f;
                        float incr = count != 0 ? -1f / count : 0f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].incr = incr; ;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].min = x; // Value to reach 
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].max = 2f;// was 1 with 2.05;

                        if (synth.VerboseCalcVolADSR)
                        {
                            float decaySec = calculate_hold_decay_buffers_seconds((int)fluid_gen_type.GEN_VOLENVDECAY, (int)fluid_gen_type.GEN_KEYTOVOLENVDECAY, true);
                            //Debug.Log(string.Format("Calc {0} y={1:F7} count={2} incr={3:F7}", (fluid_gen_type)igen, y,
                            //    volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].count,
                            //    volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].incr));
                            Debug.Log($"{header} count:{count,-5} time:{decaySec:F4} s. x:{x}");
                        }
                    }
                    break;

                case (int)fluid_gen_type.GEN_VOLENVRELEASE:             /* SF2.01 section 8.1.3 # 38 */
                    {
                        float x = genVal < FLUID_MIN_VOLENVRELEASE ? FLUID_MIN_VOLENVRELEASE : genVal > 8000.0f ? 8000.0f : genVal;
                        uint count;
                        if (synth.MPTK_CorePlayer)
                        {
                            //NUM_BUFFERS_RELEASE
                            count = 1 + (uint)(output_rate * fluid_conv.fluid_tc2sec_release(x) * synth.MPTK_ReleaseTimeMod / synth.FLUID_BUFSIZE);
                        }
                        else
                        {
                            //uint count = 1 + (uint)(output_rate * fluid_conv.fluid_tc2sec_release(x) / fluid_synth_t.FLUID_BUFSIZE);
                            uint rt = (uint)(Nano100ToMilli * fluid_conv.fluid_tc2sec_release(x) * 1000f);
                            count = rt < synth.MPTK_ReleaseTimeMin ? synth.MPTK_ReleaseTimeMin : rt;
                        }
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].count = count;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].coeff = 1f;
                        float incr = count != 0 ? -1f / count : 0f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].incr = incr;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].min = 0f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].max = 1f;
                        if (synth.VerboseCalcVolADSR)
                        {
                            float releaseSec = (float)fluid_conv.fluid_tc2sec_release(x);
                            Debug.Log($"{header} count={count,-5} time:{releaseSec:F4} s.");
                        }

                    }
                    break;

                //
                // Modulation envelope
                //
                // - delay and hold times are converted to absolute number of samples
                // - sustain is converted to its absolute value
                // - attack, decay and release are converted to their increment per sample
                case (int)fluid_gen_type.GEN_MODENVDELAY:                /* SF2.01 section 8.1.3 # 33 */
                    {
                        float x = genVal < -12000f ? -12000f : genVal > 5000f ? 5000f : genVal;

                        uint count = synth.MPTK_CorePlayer ?
                                        // NUM_BUFFERS_DELAY
                                        (uint)(output_rate * fluid_conv.fluid_tc2sec_delay(x) / synth.FLUID_BUFSIZE) :
                                        (uint)(Nano100ToMilli * fluid_conv.fluid_tc2sec_delay(x) * 1000f);
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].count = count;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].coeff = 0f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].incr = 0f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].min = -1f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].max = 1f;
                        if (synth.VerboseCalcModADSR)
                            Debug.Log(string.Format($"{header} count:{count,-5} time:{fluid_conv.fluid_tc2sec_delay(x):F4} s."));

                    }
                    break;

                case (int)fluid_gen_type.GEN_MODENVATTACK:               /* SF2.01 section 8.1.3 # 34 */
                    {
                        float x = genVal < -12000f ? -12000f : genVal > 8000f ? 8000f : genVal;

                        uint count = synth.MPTK_CorePlayer ?
                                        // NUM_BUFFERS_ATTACK
                                        1 + (uint)(output_rate * fluid_conv.fluid_tc2sec_attack(x) / synth.FLUID_BUFSIZE) :
                                        (uint)(Nano100ToMilli * fluid_conv.fluid_tc2sec_attack(x) * 1000f);

                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].count = count;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].coeff = 1f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].incr = count != 0 ? 1f / count : 0f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].min = -1f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].max = 1f;
                        if (synth.VerboseCalcModADSR)
                            Debug.Log(string.Format($"{header} count:{count,-5} time:{fluid_conv.fluid_tc2sec_attack(x):F4} s."));
                    }
                    break;

                case (int)fluid_gen_type.GEN_MODENVHOLD:                 /* SF2.01 section 8.1.3 # 35 */
                case (int)fluid_gen_type.GEN_KEYTOMODENVHOLD:            /* SF2.01 section 8.1.3 # 39 */
                    {
                        uint count = synth.MPTK_CorePlayer ?
                            calculate_hold_decay_buffers((int)fluid_gen_type.GEN_MODENVHOLD, (int)fluid_gen_type.GEN_KEYTOMODENVHOLD, false) : /* 0 means: hold */
                            (uint)(Nano100ToMilli * calculate_hold_decay_ms((int)fluid_gen_type.GEN_MODENVHOLD, (int)fluid_gen_type.GEN_KEYTOMODENVHOLD, false)); /* 0 means: hold */

                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].count = count;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].coeff = 1f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].incr = 0f; // Volume stay stable during hold phase
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].min = -1f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].max = 2f;// was 1 with 2.05;
                        if (synth.VerboseCalcModADSR)
                        {
                            float delayHold = calculate_hold_decay_buffers_seconds((int)fluid_gen_type.GEN_MODENVHOLD, (int)fluid_gen_type.GEN_KEYTOMODENVHOLD, false);
                            Debug.Log(string.Format($"{header} count:{count,-5} time:{delayHold:F4} s."));
                        }
                    }
                    break;

                case (int)fluid_gen_type.GEN_MODENVDECAY:               /* SF2.01 section 8.1.3 # 36 */
                case (int)fluid_gen_type.GEN_MODENVSUSTAIN:             /* SF2.01 section 8.1.3 # 37 */
                case (int)fluid_gen_type.GEN_KEYTOMODENVDECAY:          /* SF2.01 section 8.1.3 # 40 */
                    {
                        uint count = synth.MPTK_CorePlayer ?
                            calculate_hold_decay_buffers(
                                (int)fluid_gen_type.GEN_MODENVDECAY,
                                (int)fluid_gen_type.GEN_KEYTOMODENVDECAY, true) : /* 1 for decay */
                            (uint)(Nano100ToMilli * calculate_hold_decay_ms((int)fluid_gen_type.GEN_MODENVDECAY, (int)fluid_gen_type.GEN_KEYTOMODENVDECAY, true)); /* 1 for decay */

                        float x = 1f - 0.001f * CalculateGeneratorValue((int)fluid_gen_type.GEN_MODENVSUSTAIN);
                        x = x < 0f ? 0f : x > 1f ? 1f : x;

                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].count = count;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].coeff = 1f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].incr = count != 0f ? -1f / count : 0f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].min = x; // Value to reach 
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].max = 2f;// was 1 with 2.05;

                        if (synth.VerboseCalcModADSR)
                        {
                            float decaySec = calculate_hold_decay_buffers_seconds(
                                (int)fluid_gen_type.GEN_MODENVDECAY,
                                (int)fluid_gen_type.GEN_KEYTOVOLENVDECAY, true);
                            Debug.Log(string.Format($"{header} count:{count,-5} time:{decaySec:F4} s. x:{x}"));
                        }
                    }
                    break;

                case (int)fluid_gen_type.GEN_MODENVRELEASE:             /* SF2.01 section 8.1.3 # 30 */
                    {
                        float x = genVal < -12000f ? -12000f : genVal > 8000.0f ? 8000.0f : genVal;
                        uint count;
                        if (synth.MPTK_CorePlayer)
                        {
                            //NUM_BUFFERS_RELEASE
                            count = 1 + (uint)(output_rate * fluid_conv.fluid_tc2sec_release(x) / synth.FLUID_BUFSIZE);
                        }
                        else
                        {
                            //uint count = 1 + (uint)(output_rate * fluid_conv.fluid_tc2sec_release(x) / fluid_synth_t.FLUID_BUFSIZE);
                            uint rt = (uint)(Nano100ToMilli * fluid_conv.fluid_tc2sec_release(x) * 1000f);
                            count = rt < synth.MPTK_ReleaseTimeMin ? synth.MPTK_ReleaseTimeMin : rt;
                        }
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].count = count;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].coeff = 1f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].incr = count != 0 ? -1f / count : 0f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].min = 0f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].max = 2f;
                        if (synth.VerboseCalcModADSR)
                            Debug.Log(string.Format($"{header} count:{count,-5} time:{fluid_conv.fluid_tc2sec_release(x):F4} s."));
                    }
                    break;

                default:
                    break;
            }
        }


        /**
         * Return the effective MIDI key of the playing voice.
         *
         * @param voice Voice instance
         * @return The MIDI key this voice is playing at
         *
         * If the voice was started from an instrument which uses a fixed key generator, it returns that.
         * Otherwise returns the same value as \c fluid_voice_get_key.
         *
         * @note The result of this function is only valid if the voice is playing.
         *
         * @since 1.1.7
         */
        public int fluid_voice_get_actual_key()
        {
            //float x = fluid_voice_gen_value(voice, GEN_KEYNUM);
            float x = gen[(int)fluid_gen_type.GEN_KEYNUM].Val + gen[(int)fluid_gen_type.GEN_KEYNUM].Mod; //+ gens[igen].nrpn 

            if (x >= 0)
            {
                return (int)x;
            }
            else
            {
                return fluid_voice_get_key();
            }
        }

        /**
         * Return the MIDI key from the starting noteon event.
         *
         * @param voice Voice instance
         * @return The MIDI key of the noteon event that originally turned on this voice
         *
         * @note The result of this function is only valid if the voice is playing.
         *
         * @since 1.1.7
         */
        int fluid_voice_get_key()
        {
            return key;
        }


        /* Useful to return the nominal pitch of a key */
        /* The nominal pitch is dependent of voice->root_pitch,tuning, and
           GEN_SCALETUNE generator.
           This is useful to set the value of GEN_PITCH generator on noteOn.
           This is useful to get the beginning/ending pitch for portamento.
        */
        float fluid_voice_calculate_pitch(int key)
        {
            //fluid_tuning_t* tuning;
            float pitch;

            /* Now the nominal pitch of the key is returned.
             * Note about SCALETUNE: SF2.01 8.1.3 says, that this generator is a
             * non-realtime parameter. So we don't allow modulation (as opposed
             * to fluid_voice_gen_value(voice, GEN_SCALETUNE) When the scale tuning is varied,
             * one key remains fixed. Here C3 (MIDI number 60) is used.
             */
            //if (fluid_channel_has_tuning(voice->channel))
            //{
            //    tuning = fluid_channel_get_tuning(voice->channel);
            //    x = fluid_tuning_get_pitch(tuning, (int)(voice->root_pitch / 100.0f));
            //    pitch = voice->gen[GEN_SCALETUNE].val / 100.0f * (fluid_tuning_get_pitch(tuning, key) - x) + x;
            //}
            //else
            {
                pitch = gen[(int)fluid_gen_type.GEN_SCALETUNE].Val * (key - root_pitch / 100.0f) + root_pitch;
            }

            return pitch;
        }

        private float CalculateGeneratorValue(int igen)
        {
            float genVal = gen[igen].Val;
#if MPTK_PRO
            if (MptkEvent != null && MptkEvent.GensModifier != null && MptkEvent.GensModifier[igen] != null)
            {
                switch (MptkEvent.GensModifier[igen].Mode)
                {
                    case MPTKModeGeneratorChange.Override:
                        genVal = MptkEvent.GensModifier[igen].SoundFontVal;
                        break;
                    case MPTKModeGeneratorChange.Reinforce:
                        genVal += MptkEvent.GensModifier[igen].SoundFontVal;
                        break;
                    case MPTKModeGeneratorChange.Restaure:
                        // the soundfont value is not modified
                        break;
                }
                if (synth.VerboseCalcGen)
                {
                    Debug.Log($"Genrator {(fluid_gen_type)igen} modified from {gen[igen].Val} to {genVal} {MptkEvent.GensModifier[igen].Mode}");
                }
            }
#endif
            return genVal + gen[igen].Mod; //+ gens[igen].nrpn
        }


        /**
         * Compute the pitch for a key after applying Fluidsynth's tuning functionality
         * and channel coarse/fine tunings.
         * @param chan fluid_channel_t
         * @param key MIDI note number (0-127)
         * @return the pitch of the key
         */
        float fluid_channel_get_key_pitch(int key)
        {
            //if (midiChannel.tuning != null)
            //{
            //    return fluid_tuning_get_pitch(chan->tuning, key)
            //        + 100.0f * fluid_channel_get_gen(chan, GEN_COARSETUNE)
            //        + fluid_channel_get_gen(chan, GEN_FINETUNE);
            //}
            //else
            {
                return key * 100.0f;
            }
        }


        /**
         * Return the effective MIDI velocity of the playing voice.
         *
         * @param voice Voice instance
         * @return The MIDI velocity this voice is playing at
         *
         * If the voice was started from an instrument which uses a fixed velocity generator, it returns
         * that. Otherwise it returns the same value as \c fluid_voice_get_velocity.
         *
         * @note The result of this function is only valid if the voice is playing.
         *
         * @since 1.1.7
         */
        public int fluid_voice_get_actual_velocity()
        {
            //fluid_real_t x = fluid_voice_gen_value(voice, GEN_VELOCITY);
            float x = gen[(int)fluid_gen_type.GEN_VELOCITY].Val + gen[(int)fluid_gen_type.GEN_VELOCITY].Mod; //+ gens[igen].nrpn // fluid_voice_gen_value(voice, GEN_KEYNUM);

            if (x > 0f)
            {
                return (int)x;
            }
            else
            {
                // return fluid_voice_get_velocity(voice);
                return vel;
            }
        }


        float calculate_hold_decay_buffers_seconds(int gen_base, int gen_key2base, bool is_decay)
        {
            /* Purpose:
             *
             * Returns the number of DSP loops, that correspond to the hold
             * (is_decay=0) or decay (is_decay=1) time.
             * gen_base=GEN_VOLENVHOLD, GEN_VOLENVDECAY, GEN_MODENVHOLD,
             * GEN_MODENVDECAY gen_key2base=GEN_KEYTOVOLENVHOLD,
             * GEN_KEYTOVOLENVDECAY, GEN_KEYTOMODENVHOLD, GEN_KEYTOMODENVDECAY
             */

            /* SF2.01 section 8.4.3 # 31, 32, 39, 40
             * GEN_KEYTOxxxENVxxx uses key 60 as 'origin'.
             * The unit of the generator is timecents per key number.
             * If KEYTOxxxENVxxx is 100, a key one octave over key 60 (72)
             * will cause (60-72)*100=-1200 timecents of time variation.
             * The time is cut in half.
             */
            float keysteps = 60f - fluid_channel_get_key_pitch(fluid_voice_get_actual_key()) / 100f;
            float timecents = CalculateGeneratorValue(gen_base) + CalculateGeneratorValue(gen_key2base) * keysteps;

            /* Range checking */
            if (is_decay)
            {
                /* SF 2.01 section 8.1.3 # 28, 36 */
                if (timecents > 8000f)
                {
                    timecents = 8000f;
                }
            }
            else
            {
                /* SF 2.01 section 8.1.3 # 27, 35 */
                if (timecents > 5000f)
                {
                    timecents = 5000f;
                }
                /* SF 2.01 section 8.1.2 # 27, 35:
                 * The most negative number indicates no hold time
                 */
                if (timecents <= -32768f)
                {
                    return 0;
                }
            }
            /* SF 2.01 section 8.1.3 # 27, 28, 35, 36 */
            if (timecents < -12000f)
            {
                timecents = -12000f;
            }

            //seconds = fluid_conv.fluid_tc2sec(timecents);
            return Mathf.Pow(2f, timecents / 1200f);
            /* Each DSP loop processes FLUID_BUFSIZE samples. */
        }

        /*
         * calculate_hold_decay_buffers
         */
        uint calculate_hold_decay_buffers(int gen_base, int gen_key2base, bool is_decay)
        {
            /* round to next full number of buffers */
            return (uint)((output_rate * calculate_hold_decay_buffers_seconds(gen_base, gen_key2base, is_decay)) / synth.FLUID_BUFSIZE + 0.5f);
        }

        /// <summary>@brief
        /// Returns the number of DSP loops, that correspond to the hold (is_decay=0) or decay (is_decay=1) time.
        /// gen_base=GEN_VOLENVHOLD, GEN_VOLENVDECAY, GEN_MODENVHOLD, GEN_MODENVDECAY gen_key2base=GEN_KEYTOVOLENVHOLD, GEN_KEYTOVOLENVDECAY, GEN_KEYTOMODENVHOLD, GEN_KEYTOMODENVDECAY
        /// </summary>
        /// <param name="gen_base"></param>
        /// <param name="gen_key2base"></param>
        /// <param name="is_decay"></param>
        /// <returns></returns>
        float calculate_hold_decay_ms(int gen_base, int gen_key2base, bool is_decay)
        {
            // SF2.01 section 8.4.3 # 31, 32, 39, 40
            // GEN_KEYTOxxxENVxxx uses key 60 as 'origin'.
            // The unit of the generator is timecents per key number.
            // If KEYTOxxxENVxxx is 100, a key one octave over key 60 (72) will cause (60-72)*100=-1200 timecents of time variation. The time is cut in half.
            float timecents = CalculateGeneratorValue(gen_base) + CalculateGeneratorValue(gen_key2base) * (60f - key);

            // Range checking 
            if (is_decay)
            {
                // SF 2.01 section 8.1.3 # 28, 36 
                if (timecents > 8000f)
                {
                    timecents = 8000f;
                }
            }
            else
            {
                // SF 2.01 section 8.1.3 # 27, 35 
                if (timecents > 5000f)
                {
                    timecents = 5000f;
                }
                // SF 2.01 section 8.1.2 # 27, 35: The most negative number indicates no hold time
                if (timecents <= -32768f)
                {
                    return 0f;
                }
            }
            // SF 2.01 section 8.1.3 # 27, 28, 35, 36 
            if (timecents < -12000f)
            {
                timecents = -12000f;
            }

            //fluid_conv.fluid_tc2sec(timecents);
            float seconds = Mathf.Pow(2f, timecents / 1200f);

            // Each DSP loop processes FLUID_BUFSIZE samples. Round to next full number of buffers 
            //buffers = Convert.ToInt32((output_rate * seconds) / (double)fluid_synth_t.FLUID_BUFSIZE + 0.5);

            return seconds * 1000f;
        }

        const int NBR_BIT_BY_VAR_LN2 = 5; /* for 32 bits variables */
        const int NBR_BIT_BY_VAR = 1 << NBR_BIT_BY_VAR_LN2;
        const int NBR_BIT_BY_VAR_ANDMASK = NBR_BIT_BY_VAR - 1;
        const int SIZE_UPDATED_GEN_BIT = (((byte)fluid_gen_type.GEN_LAST + NBR_BIT_BY_VAR_ANDMASK) / NBR_BIT_BY_VAR);

        /**
         * fluid_voice_modulate
         *
         * In this implementation, I want to make sure that all controllers
         * are event based: the parameter values of the DSP algorithm should
         * only be updates when a controller event arrived and not at every
         * iteration of the audio cycle (which would probably be feasible if
         * the synth was made in silicon).
         *
         * The update is done in three steps:
         *
         * - first, we look for all the modulators that have the changed
         * controller as a source. This will yield a list of generators that
         * will be changed because of the controller event.
         *
         * - For every changed generator, calculate its new value. This is the
         * sum of its original value plus the values of al the attached
         * modulators.
         *
         * - For every changed generator, convert its value to the correct
         * unit of the corresponding DSP parameter
         *
         * @fn int fluid_voice_modulate(fluid_voice_t* voice, int cc, int ctrl, int val)
         * @param voice the synthesis voice
         * @param cc flag to distinguish between a continous control and a channel control (pitch bend, ...)
         * @param ctrl the control number
         * */
        public void fluid_voice_modulate(int cc, int ctrl)
        {
            //if (synth.VerboseVoice)
            //{
            //    fluid_global.FLUID_LOG(fluid_log_level.FLUID_INFO, "Chan={0}, CC={1}, Src={2}", channel.channum, cc, pctrl);
            //}
            for (int i = 0; i < mods.Count; i++)
            {
                HiMod mod = mods[i];

                /* Clears registered bits table of updated generators
                 * SIZE_UPDATED_GEN_BIT=2 */
                // feature not kept from FS 2.3
                // uint[] updated_gen_bit = new uint[SIZE_UPDATED_GEN_BIT];

                /* step 1: find all the modulators that have the changed controller
                   as input source. When ctrl is -1 all modulators destination
                   are updated */

                /* fluid_mod_has_source
                (((m.src1 == ctrl) && ((m.flags1 & FLUID_MOD_CC) != 0) && (cc != 0))
                || ((m.src1 == ctrl) && ((m.flags1 & FLUID_MOD_CC) == 0) && (cc == 0)))
            ||
                (((m.src2 == ctrl) && ((m.flags2 & FLUID_MOD_CC) != 0) && (cc != 0))
                || ((m.src2 == ctrl) && ((m.flags2 & FLUID_MOD_CC) == 0) && (cc == 0)))
                 */

                if ((((mod.src1 == ctrl) && ((mod.flags1 & (byte)fluid_mod_flags.FLUID_MOD_CC) != 0) && (cc != 0)) ||
                    (((mod.src1 == ctrl) && ((mod.flags1 & (byte)fluid_mod_flags.FLUID_MOD_CC) == 0) && (cc == 0))))
                    ||
                    (((mod.src2 == ctrl) && ((mod.flags2 & (byte)fluid_mod_flags.FLUID_MOD_CC) != 0) && (cc != 0)) ||
                    (((mod.src2 == ctrl) && ((mod.flags2 & (byte)fluid_mod_flags.FLUID_MOD_CC) == 0) && (cc == 0)))))
                {

                    int gen = mod.dest; //fluid_mod_get_dest

                    // Skip if this generator has already been updated 
                    // feature not kept from FS 2.3
                    //if ((updated_gen_bit[igen >> NBR_BIT_BY_VAR_LN2] & (1 << (igen & NBR_BIT_BY_VAR_ANDMASK)))!=0)
                    {

                        float modval = 0.0f;

                        // step 2: for every changed modulator, calculate the modulation value of its associated generator
                        for (int k = 0; k < mods.Count; k++)
                        {
                            HiMod modk = mods[k];
                            if (modk.dest == gen) //fluid_mod_has_dest(mod, gen)((mod).dest == gen)
                            {
                                modval += modk.fluid_mod_get_value(this);
                            }
                        }

                        this.gen[gen].Mod = modval; //fluid_gen_set_mod(_gen, _val)  { (_gen).mod = (double)(_val); }

                        // step 3: now that we have the new value of the generator, recalculate the parameter values that are derived from the generator */
                        if (synth.VerboseController) Debug.Log($"Modulate Generator Channel:{channel.Channel} Controller:{(fluid_mod_src)ctrl} Continue={cc} Gen:{(fluid_gen_type)gen} Value:{modval:F2}");

                        fluid_voice_update_param(gen);

                        // set the bit that indicates this generator is updated 
                        // feature not kept from FS 2.3
                        //updated_gen_bit[igen >> NBR_BIT_BY_VAR_LN2] |= (uint)(1 << (igen & NBR_BIT_BY_VAR_ANDMASK));

                    }
                }
            }
        }

        /// <summary>@brief
        /// Update all the modulators. This function is called after a ALL_CTRL_OFF MIDI message has been received (CC 121). 
        /// </summary>
        /// <param name="voice"></param>
        /// <returns></returns>
        public void fluid_voice_modulate_all()
        {
            //fluid_mod_t* mod;
            //int i, k, gen;
            //float modval;

            //Loop through all the modulators.
            //    FIXME: we should loop through the set of generators instead of the set of modulators. We risk to call 'fluid_voice_update_param'
            //    several times for the same generator if several modulators have that generator as destination. It's not an error, just a wast of
            //    energy (think polution, global warming, unhappy musicians, ...) 

            foreach (HiMod m in mods)
            {
                gen[m.dest].Mod += m.fluid_mod_get_value(this);
                int igen = m.dest; //fluid_mod_get_dest
                float modval = 0.0f;
                // Accumulate the modulation values of all the modulators with destination generator 'gen'
                foreach (HiMod m1 in mods)
                {
                    if (m1.dest == igen) //fluid_mod_has_dest(mod, gen)((mod).dest == gen)
                    {
                        modval += m1.fluid_mod_get_value(this);
                    }
                }
                gen[igen].Mod = modval; //fluid_gen_set_mod(_gen, _val)  { (_gen).mod = (double)(_val); }

                // Update the parameter values that are depend on the generator 'gen'
                fluid_voice_update_param(igen);
            }
        }

        /* Purpose:
         *
         * Make sure, that sample start / end point and loop points are in
         * proper order. When starting up, calculate the initial phase.
         */
        void fluid_rvoice_check_sample_sanity()
        {
            //Debug.Log("fluid_rvoice_check_sample_sanity");
            int min_index_nonloop = (int)sample.Start;
            int max_index_nonloop = (int)sample.End;

            /* make sure we have enough samples surrounding the loop */
            int min_index_loop = (int)sample.Start + FLUID_MIN_LOOP_PAD;
            /* 'end' is last valid sample, loopend can be + 1 */
            int max_index_loop = (int)sample.End - FLUID_MIN_LOOP_PAD + 1;

            //if (check_sample_sanity_flag == 0)
            //{
            //    return;
            //}

            //Debug.LogFormat("Sample from {0} to {1}", sample.Start, sample.End);
            //Debug.LogFormat("Sample loop from {0} {1}", sample.LoopStart, sample.LoopEnd);
            //Debug.LogFormat("Playback from {0} to {1}", start, end);
            //Debug.LogFormat("Playback loop from {0} to {1}", loopstart, loopend);

            /* Keep the start point within the sample data */
            if (start < min_index_nonloop)
                start = min_index_nonloop;
            else if (start > max_index_nonloop)
                start = max_index_nonloop;

            /* Keep the end point within the sample data */
            if (end < min_index_nonloop)
                end = min_index_nonloop;
            else if (end > max_index_nonloop)
                end = max_index_nonloop;

            /* Keep start and end point in the right order */
            if (start > end)
            {
                int temp = start;
                start = end;
                end = temp;
                /*FLUID_LOG(FLUID_DBG, "Loop / sample sanity check: Changing order of start / end points!"); */
            }

            /* Zero length? */
            if (start == end)
            {
                fluid_voice_off();
                return;
            }


            //if (IsLoop)
            if (samplemode == fluid_loop.FLUID_LOOP_UNTIL_RELEASE || samplemode == fluid_loop.FLUID_LOOP_DURING_RELEASE)
            {
                /* Keep the loop start point within the sample data */
                if (loopstart < min_index_loop)
                    loopstart = min_index_loop;
                else if (loopstart > max_index_loop)
                    loopstart = max_index_loop;

                /* Keep the loop end point within the sample data */
                if (loopend < min_index_loop)
                    loopend = min_index_loop;
                else if (loopend > max_index_loop)
                    loopend = max_index_loop;

                /* Keep loop start and end point in the right order */
                if (loopstart > loopend)
                {
                    int temp = loopstart;
                    loopstart = loopend;
                    loopend = temp;
                    /*FLUID_LOG(FLUID_DBG, "Loop / sample sanity check: Changing order of loop points!"); */
                }

                /* Loop too short? Then don't loop. */
                if (loopend < loopstart + FLUID_MIN_LOOP_SIZE)
                {
                    samplemode = fluid_loop.FLUID_UNLOOPED;
                }

                /* The loop points may have changed. Obtain a new estimate for the loop volume. */
                /* Is the voice loop within the sample loop? */
                // TBD from FS
                if (loopstart >= (int)sample.LoopStart &&
                    loopend <= (int)sample.LoopEnd)
                {
                    /* Is there a valid peak amplitude available for the loop? */
                    //if (sample.amplitude_that_reaches_noise_floor_is_valid)
                    //{
                    //    amplitude_that_reaches_noise_floor_loop = sample.amplitude_that_reaches_noise_floor /*/ synth.gain*/;
                    //}
                    //else
                    //{
                    //    /* Worst case */
                    //    amplitude_that_reaches_noise_floor_loop = amplitude_that_reaches_noise_floor_nonloop;
                    //}
                    //Debug.LogFormat("amplitude_that_reaches_noise_floor_loop:{0}" , amplitude_that_reaches_noise_floor_loop);
                }

            } /* if sample mode is looped */

            /* Run startup specific code (only once, when the voice is started)
        #define FLUID_SAMPLESANITY_STARTUP (1 << 1) 
            */
            if ((check_sample_sanity_flag & FLUID_SAMPLESANITY_STARTUP) != 0)
            {
                if (max_index_loop - min_index_loop < FLUID_MIN_LOOP_SIZE)
                {
                    if (samplemode == fluid_loop.FLUID_LOOP_UNTIL_RELEASE ||
                        samplemode == fluid_loop.FLUID_LOOP_DURING_RELEASE)
                    {
                        samplemode = fluid_loop.FLUID_UNLOOPED;
                    }
                }

                // Set the initial phase of the voice (using the result from the start offset modulators). 
                //#define fluid_phase_set_int(a, b)    ((a) = ((unsigned long long)(b)) << 32)
                //fluid_phase_set_int(phase, start);
                phase = ((ulong)start) << 32;
            } /* if startup */

            // Is this voice run in loop mode, or does it run straight to the end of the waveform data?
            if ((samplemode == fluid_loop.FLUID_LOOP_UNTIL_RELEASE &&
                 volenv_section < fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE) ||
                 samplemode == fluid_loop.FLUID_LOOP_DURING_RELEASE)
            {
                /* Yes, it will loop as soon as it reaches the loop point.  In
                 * this case we must prevent, that the playback pointer (phase)
                 * happens to end up beyond the 2nd loop point, because the
                 * point has moved.  The DSP algorithm is unable to cope with
                 * that situation.  So if the phase is beyond the 2nd loop
                 * point, set it to the start of the loop. No way to avoid some
                 * noise here.  Note: If the sample pointer ends up -before the
                 * first loop point- instead, then the DSP loop will just play
                 * the sample, enter the loop and proceed as expected => no
                 * actions required.

                  Purpose: Return the index and the fractional part, respectively. 
        #define fluid_phase_index(_x) ((uint)((_x) >> 32))
                  int index_in_sample = fluid_phase_index(phase);
                */

                int index_in_sample = ((int)phase) >> 32;
                if (index_in_sample >= loopend)
                {
                    /* FLUID_LOG(FLUID_DBG, "Loop / sample sanity check: Phase after 2nd loop point!"); 
                       #define fluid_phase_set_int(a, b)    ((a) = ((unsigned long long)(b)) << 32)
                       fluid_phase_set_int(phase, loopstart);
                    */
                    phase = ((ulong)loopstart) << 32;
                }
            }
            /*    FLUID_LOG(FLUID_DBG, "Loop / sample sanity check: Sample from %i to %i, loop from %i to %i", start, end, loopstart, loopend); */
            // Sample sanity has been assured. Don't check again, until some sample parameter is changed by modulation. 
            check_sample_sanity_flag = 0;

            //Debug.LogFormat("Sane? playback loop from {0} to {1}", loopstart , loopend );
        }

        /*
         * fluid_voice_write - called from OnAudioFilterRead for each voices
         *
         * This is where it all happens. This function is called by the
         * synthesizer to generate the sound samples. The synthesizer passes
         * four audio buffers: left, right, reverb out, and chorus out.
         *
         * The biggest part of this function sets the correct values for all
         * the dsp parameters (all the control data boil down to only a few
         * dsp parameters). The dsp routine is #included in several places (fluid_dsp_core.c).
         */
        public int fluid_rvoice_write(
            long onAudioFilterTicks,
            float[] dsp_left_buf, float[] dsp_right_buf,
            float[] dsp_reverb_buf, float[] dsp_chorus_buf)
        {
            float target_amp;    /* target amplitude */
            int count;

            fluid_env_data env_data;
            float x;
#if MPTK_PRO
            ticks = PauseApply(onAudioFilterTicks);
            if (ticks < 0) return 0;
            //ticks = onAudioFilterTicks - deltaPause;
#else
            ticks = onAudioFilterTicks;
#endif

            if (ticks < TimeAtStart)
            {
                //Debug.Log($"Not yet {ticks} {TimeAtStart} {(TimeAtStart - ticks) / 10000f:F3} ms"); 
                return 0;
            }

            if (synth.VerboseSynth || synth.VerboseOverload)
            {
                NewTimeWrite = ticks;
                DeltaTimeWrite = NewTimeWrite - LastTimeWrite;
                LastTimeWrite = NewTimeWrite;
                TimeFromStart = NewTimeWrite - TimeAtStart;
            }
            //Debug.Log($"fluid_rvoice_write {id} {sample.Name} {ticks}");
            //Debug.Log(this.sample.Name);
            Array.Clear(dsp_buf, 0, synth.FLUID_BUFSIZE);

            // Check time for the note-off (no note_off with MPTK)
            // A tick represents = 100 nanoseconds or one ten-millionth of a second. There are 10,000 ticks in a millisecond, or 10 million ticks in a second. 
            if (DurationTick >= 0 && ticks > TimeAtEnd)
            {
                // No note_off in attack phase (sure ?) or already in release phase
                if (volenv_section > fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK && volenv_section < fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE)
                {
                    // V2.89.0 The old soundcards would play any sample that didn't have loop points as a one-shot.  
                    // In other words, once the sample was triggered by a NoteOn event it would be played once through to the end and could not be interrupted by a NoteOff event.  
                    // This was used for drum samples for the most part.  I have several old MIDI files that take advantage of this.  
                    // In one extreme example, nearly every NoteOn for the drum part is immediately followed by a NoteOff on the next tick.
                    // if (synth.keepPlayingNonLooped == false || samplemode == fluid_loop.FLUID_LOOP_UNTIL_RELEASE || samplemode == fluid_loop.FLUID_LOOP_DURING_RELEASE)
                    if (synth.keepPlayingNonLooped && samplemode == fluid_loop.FLUID_UNLOOPED)
                    {
                        if (synth.VerboseSpecialNoteOff) //Ignore note-off
                        {
                            string title = $"Ignore note-off Channel:{chan} key:{key} ADSR:{volenv_section} Ignore:{synth.keepPlayingNonLooped} DurationTick:{DurationTick} Name:{sample.Name}";
                            DebugVolEnv(title);
                        }
                    }
                    else
                    {
                        if (synth.VerboseEnvVolume) DebugVolEnv("Over Time, send note-off");
                        fluid_rvoice_noteoff();
                    }
                }
            }

            /******************* sample **********************/

            /* Range checking for sample- and loop-related parameters 
             * Initial phase is calculated here*/
            if (check_sample_sanity_flag != 0)
                fluid_rvoice_check_sample_sanity();

            /******************* vol env **********************/

            env_data = volenv_data[(int)volenv_section];

            /* skip to the next section of the envelope if necessary */
            while (volenv_count >= env_data.count)
            {
                volenv_section++;
                env_data = volenv_data[(int)volenv_section];
                volenv_count = 0;
                if (synth.VerboseEnvVolume) DebugVolEnv("Next");
            }


            /* calculate the envelope value and check for valid range */
            x = env_data.coeff * volenv_val + env_data.incr;
            //Debug.LogFormat("t:{0} calc --> coeff:{1} * volenv_val:{2:F7} + incr:{3:F10} --> x:{4:F7} section:{5} ({6})",
            //     (System.DateTime.UtcNow.Ticks - TimeAtStart) / fluid_voice.Nano100ToMilli, 
            //    env_data.coeff, volenv_val, env_data.incr, x, volenv_section, (int)volenv_section);

            if (x < env_data.min)
            {
                x = env_data.min;
                volenv_section++;
                volenv_count = 0;
                if (synth.VerboseEnvVolume) DebugVolEnv("Min");
            }
            else if (x > env_data.max)
            {
                x = env_data.max;
                volenv_section++;
                volenv_count = 0;
                if (synth.VerboseEnvVolume) DebugVolEnv("Max");
            }

            volenv_val = x;
            volenv_count++;

            if (volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED)
            {
                //fluid_profile(FLUID_PROF_VOICE_RELEASE, ref);
                fluid_voice_off();
                return 0;
            }

            //fluid_check_fpe("voice_write vol env");

            /******************* mod env **********************/
            if (synth.MPTK_ApplyRealTimeModulator)
            {

                env_data = modenv_data[(int)modenv_section];

                /* skip to the next section of the envelope if necessary */
                while (modenv_count >= env_data.count)
                {
                    modenv_section++;
                    env_data = modenv_data[(int)modenv_section];
                    modenv_count = 0;
                    if (synth.VerboseEnvModulation) DebugModEnv("Next");
                }

                /* calculate the envelope value and check for valid range */
                x = env_data.coeff * modenv_val + env_data.incr;

                if (x < env_data.min)
                {
                    x = env_data.min;
                    modenv_section++;
                    modenv_count = 0;
                    if (synth.VerboseEnvModulation) DebugModEnv("Min");
                }
                else if (x > env_data.max)
                {
                    x = env_data.max;
                    modenv_section++;
                    modenv_count = 0;
                    if (synth.VerboseEnvModulation) DebugModEnv("Max");
                }

                modenv_val = x;
                modenv_count++;
                //fluid_check_fpe("voice_write mod env");
            }

            /******************* mod lfo **********************/
            // was FluidTicks
            if (synth.MPTK_ApplyModLfo && ticks >= modlfo_delay)
            {
                modlfo_val += modlfo_incr;

                if (modlfo_val > 1f)
                {
                    //debug_lfo(voice, "modlfo_val > 1");
                    modlfo_incr = -modlfo_incr;
                    modlfo_val = 2f - modlfo_val;
                }
                else if (modlfo_val < -1f)
                {
                    //debug_lfo(voice, "modlfo_val <-1");
                    modlfo_incr = -modlfo_incr;
                    modlfo_val = -2f - modlfo_val;
                }
                //DebugLFO("TimeFromStartPlayNote >= modlfo_delay");
            }
            //else DebugLFO("TimeFromStartPlayNote < modlfo_delay");

            //fluid_check_fpe("voice_write mod LFO");

            /******************* vib lfo **********************/

            if (synth.MPTK_ApplyVibLfo && ticks >= viblfo_delay)
            {
                viblfo_val += viblfo_incr;
                //DebugVib("viblfo_delay");

                if (viblfo_val > 1f)
                {
                    //DebugVib("viblfo_val > 1 freq:" + (TimeFromStartPlayNote - last_modvib_val_supp_1).ToString());
                    viblfo_incr = -viblfo_incr;
                    viblfo_val = 2f - viblfo_val;
                }
                else if (viblfo_val < -1f)
                {
                    //DebugVib("viblfo_val < -1");
                    viblfo_incr = -viblfo_incr;
                    viblfo_val = -2f - viblfo_val;
                }
            }
            //else DebugVib("TimeFromStartPlayNote < viblfo_delay");fluid_ct2hz_real

            // fluid_check_fpe("voice_write Vib LFO");

            /******************* amplitude **********************/

            /* calculate final amplitude
             * - initial gain
             * - amplitude envelope
             */

            if (volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY)
                goto post_process;  /* The volume amplitude is in hold phase. No sound is produced. */

            float amp_max = 0f;

            if (volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK)
            {
                /* the envelope is in the attack section: ramp linearly to max value.
                 * A positive modlfo_to_vol should increase volume (negative attenuation).
                 */
                // FS 1.0    t  arget_amp = fluid_atten2amp(voice->attenuation)     * fluid_cb2amp(voice->modlfo_val                        * -voice->modlfo_to_vol)
                //                            * voice->volenv_val;
                // FS 2.3      target_amp = fluid_cb2amp   (voice->dsp.attenuation) * fluid_cb2amp(fluid_lfo_get_val(&voice->envlfo.modlfo) * -voice->envlfo.modlfo_to_vol)
                //                            * fluid_adsr_env_get_val(&voice->envlfo.volenv);
                //             see C:\Devel\fluidsynth-2.3.1\src\rvoice\fluid_rvoice.c line 46
                // MPTK 2.11.2 target_amp = fluid_conv.fluid_atten2amp(attenuation) * fluid_conv.fluid_cb2amp(modlfo_val * -modlfo_to_vol) * volenv_val;
                // MPTK 2.11.3 update related to FS 2.3
                target_amp = (float)fluid_conv.fluid_cb2amp(attenuation) * (float)fluid_conv.fluid_cb2amp(modlfo_val * -modlfo_to_vol) * volenv_val;
                //Debug.Log($"FLUID_VOICE_ENVATTACK target_amp {target_amp} attenuation:{attenuation} min:{min_attenuation_cB}");
                if (synth.VerboseVolume) DebugVolume($"amp_max:no_max target_amp:{target_amp:F5} amp:{amp:F5} CutOff:no_cutoff");
            }
            else
            {
                // FS 1.0       target_amp = fluid_atten2amp(voice->attenuation) * fluid_cb2amp(960.0f * (1.0f - voice->volenv_val) + voice->modlfo_val * -voice->modlfo_to_vol);
                // FS 2.3       target_amp = fluid_cb2amp(voice->dsp.attenuation) * fluid_cb2amp(FLUID_PEAK_ATTENUATION * (1.0f - fluid_adsr_env_get_val(&voice->envlfo.volenv))
                //                            + fluid_lfo_get_val(&voice->envlfo.modlfo) * -voice->envlfo.modlfo_to_vol);
                //              see C:\Devel\fluidsynth-2.3.1\src\rvoice\fluid_rvoice.c line 56
                // MPTK 2.11.2  target_amp = fluid_conv.fluid_atten2amp(attenuation) * fluid_conv.fluid_cb2amp(960f * (1f - volenv_val) + modlfo_val * -modlfo_to_vol);
                // MPTK 2.11.3 update related to FS 2.3
                target_amp = (float)fluid_conv.fluid_cb2amp(attenuation) * (float)fluid_conv.fluid_cb2amp(fluid_conv.FLUID_PEAK_ATTENUATION * (1f - volenv_val) + modlfo_val * -modlfo_to_vol);
                //Debug.Log($"FLUID_VOICE_ENVxxxxx target_amp {target_amp} {attenuation}");


                //fprintf(stdout, "target_amp:'%f' attenuation:'%f' volenv_val:'%f' fluid_atten2amp:'%f' fluid_cb2amp:'%f'  \n", 
                //	target_amp,
                //	attenuation, 
                //	volenv_val,
                //	fluid_atten2amp(attenuation),
                //	fluid_cb2amp(960.0f * (1.0f - volenv_val), modlfo_val * -modlfo_to_vol));

                /* We turn off a voice, if the volume has dropped low enough. */

                /* A voice can be turned off, when an estimate for the volume
                 * (upper bound) falls below that volume, that will drop the
                 * sample below the noise floor.
                 */

                /* If the loop amplitude is known, we can use it if the voice loop is within
                 * the sample loop
                 */

                /* Is the playing pointer already in the loop? */
                //if (has_looped)
                //    amplitude_that_reaches_noise_floor = amplitude_that_reaches_noise_floor_loop;
                //else
                //    amplitude_that_reaches_noise_floor = amplitude_that_reaches_noise_floor_nonloop;
                //amplitude_that_reaches_noise_floor = 0.1f;

                /* attenuation_min is a lower boundary for the attenuation
                 * now and in the future (possibly 0 in the worst case).  Now the
                 * amplitude of sample and volenv cannot exceed amp_max (since
                 * volenv_val can only drop):
                 */
                // SF 2.3      amp_max = fluid_cb2amp(voice->dsp.min_attenuation_cB) * fluid_adsr_env_get_val(&voice->envlfo.volenv);
                // MPTK 2.11.2 amp_max = fluid_conv.fluid_atten2amp(min_attenuation_cB) * volenv_val;
                // MPTK 2.11.3 update related to FS 2.3
                amp_max = (float)fluid_conv.fluid_cb2amp(min_attenuation_cB) * volenv_val;

                //Debug.LogFormat($"t:{(DateTime.UtcNow.Ticks - TimeAtStart) / fluid_voice.Nano100ToMilli} "+
                //    $"calc amp_max:{amp_max:F7} volenv_val:{volenv_val:F7} section:{volenv_section} ({(int)volenv_section})");

                /* And if amp_max is already smaller than the known amplitude,
                 * which will attenuate the sample below the noise floor, then we
                 * can safely turn off the voice. Duh. */
                if (amp_max < synth.MPTK_CutOffVolume)
                {
                    if (synth.VerboseVolume) DebugVolEnv($"CutOff {amp_max:F4} < {synth.MPTK_CutOffVolume:F4}");
                    fluid_voice_off();
                    goto post_process;
                }
                if (synth.VerboseVolume) DebugVolume($"amp_max:{amp_max:F5} target_amp:{target_amp:F5} amp:{amp:F5} CutOff:{synth.MPTK_CutOffVolume:F6}");
            }

#if MPTK_PRO
            if (pauseRampCurrent > 0)
                // A pause or resume action is in progress
                target_amp = SmoothAmp(target_amp);
#endif

            /* Volume increment to go from amp to target_amp in FLUID_BUFSIZE steps */
            amp_incr = (target_amp - amp) / synth.FLUID_BUFSIZE;
            //Debug.Log($"{volenv_section} {target_amp} {amp}");

            //fluid_check_fpe("voice_write amplitude calculation");

            /* no volume and not changing? - No need to process 
                removed in 2.9.1 - not sure of its utility
             */
            //if ((amp == 0f) && (amp_incr == 0f))
            //{
            //    //if (synth.VerboseVolume)
            //        Debug.Log($"no volume and not changing? - No need to process amp={amp} amp_incr={amp_incr}");
            //    goto post_process;
            //}
            /* Calculate the number of samples, that the DSP loop advances
             * through the original waveform with each step in the output
             * buffer. It is the ratio between the frequencies of original
             * waveform and output waveform.*/
            /*
            voice->dsp.phase_incr =
                    fluid_ct2hz_real(voice->dsp.pitch + voice->dsp.pitchoffset +
                         fluid_lfo_get_val(&voice->envlfo.modlfo) * voice->envlfo.modlfo_to_pitch +
                         fluid_lfo_get_val(&voice->envlfo.viblfo) * voice->envlfo.viblfo_to_pitch +
                         modenv_val * voice->envlfo.modenv_to_pitch) / voice->dsp.root_pitch_hz;

             */
            phase_incr = (float)fluid_conv.fluid_ct2hz_real(
                pitch + modlfo_val * modlfo_to_pitch
                + viblfo_val * viblfo_to_pitch
                + modenv_val * modenv_to_pitch) / root_pitch_hz;

            //fluid_check_fpe("voice_write phase calculation");

            /* if phase_incr is not advancing, set it to the minimum fraction value (prevent stuckage) */
            if (phase_incr == 0) phase_incr = 1;

            /* voice is currently looping? (used in the DSP) set once at voice init */
            is_looping =
                (samplemode == fluid_loop.FLUID_LOOP_DURING_RELEASE ||
                (samplemode == fluid_loop.FLUID_LOOP_UNTIL_RELEASE &&
                volenv_section < fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE));

            //Debug.Log($"samplemode:{samplemode} volenv_section:{volenv_section} is_looping:{is_looping} ");

            /*********************** run the dsp chain ************************
             * The sample is mixed with the output buffer.
             * The buffer has to be filled from 0 to FLUID_BUFSIZE-1.
             * Depending on the position in the loop and the loop size, this
             * may require several runs. */

            count = 0;
            switch (synth.InterpolationMethod)
            {
                case fluid_interp.None:
                    if (synth.dsp_64)
                        count = fluid_dsp_float_64.fluid_dsp_float_interpolate_none(this);
                    else
                        count = fluid_dsp_float.fluid_dsp_float_interpolate_none(this);
                    break;
                case fluid_interp.Linear:
                default:
                    if (synth.dsp_64)
                        count = fluid_dsp_float_64.fluid_dsp_float_interpolate_linear(this);
                    else
                        count = fluid_dsp_float.fluid_dsp_float_interpolate_linear(this);
                    break;
                case fluid_interp.Cubic:
                    if (synth.dsp_64)
                        count = fluid_dsp_float_64.fluid_dsp_float_interpolate_4th_order(this);
                    else
                        count = fluid_dsp_float.fluid_dsp_float_interpolate_4th_order(this);
                    break;
                case fluid_interp.Order7:
                    if (synth.dsp_64)
                        count = fluid_dsp_float_64.fluid_dsp_float_interpolate_7th_order(this);
                    else
                        count = fluid_dsp_float.fluid_dsp_float_interpolate_7th_order(this);

                    break;
            }

            /*************** resonant filter ******************/
            if (synth.MPTK_EffectSoundFont.EnableFilter)
            {
#if MPTK_PRO                
                resonant_filter.fluid_iir_filter_calc(output_rate, modlfo_val * modlfo_to_fc + modenv_val * modenv_to_fc, synth.MPTK_EffectSoundFont.FilterFreqOffset);
#else
                resonant_filter.fluid_iir_filter_calc(output_rate, modlfo_val * modlfo_to_fc + modenv_val * modenv_to_fc, 0);
#endif
                resonant_filter.fluid_iir_filter_apply(dsp_buf, count);
            }

            /* additional custom filter - only uses the fixed modulator, no lfos... */
            //        resonant_custom_filter. fluid_iir_filter_calc(output_rate, 0);
            //        resonant_custom_filter. fluid_iir_filter_apply(dsp_buf, count);

            if (count > 0)
                fluid_voice_effects(count, dsp_left_buf, dsp_right_buf, dsp_reverb_buf, dsp_chorus_buf);
            else
                Debug.Log("Count <= 0 in fluid_rvoice_write");
            /* turn off voice if short count (sample ended and not looping) */
            if (count < synth.FLUID_BUFSIZE)
            {
                //fluid_profile(FLUID_PROF_VOICE_RELEASE, ref);
                //Debug.Log($"Count: {count} in fluid_rvoice_write fluid_voice_off");
                fluid_voice_off();
            }

        post_process:
            // was FluidTicks
            ticks += (uint)synth.FLUID_BUFSIZE;
            //fluid_check_fpe("voice_write postprocess");
            return 0;
        }

#if DEBUGTIME
        public int countIteration;
        public double cumulDeltaTime;
        public double averageDeltaTime;
        public double cumulProcessTime;
        public double averageProcessTime;
        private double startProcessTime;
#endif

        /* Purpose:
         *
         * - mixes the processed sample to left and right output using the pan setting
         * - sends the processed sample to chorus and reverb
         */
        void fluid_voice_effects(int count, float[] dsp_left_buf, float[] dsp_right_buf, float[] dsp_reverb_buf, float[] dsp_chorus_buf)
        {
            int dsp_i;
            float v;

            /* pan (Copy the signal to the left and right output buffer) The voice
            * panning generator has a range of -500 .. 500.  
            * If it is centered, it's close to 0. amp_left and amp_right are then the
            * same, and we can save one multiplication per voice and sample.
            */
            if (!synth.MPTK_EnablePanChange || (-0.5f < pan) && (pan < 0.5f))
            {
                /* The voice is centered. Use amp_left twice (with mptkChannel.volume). */
                for (dsp_i = 0; dsp_i < count; dsp_i++)
                {
                    v = amp_left * dsp_buf[dsp_i];
                    dsp_left_buf[dsp_i] += v;
                    dsp_right_buf[dsp_i] += v;
                    //if (dsp_i < 50)Debug.LogFormat("dsp_i:{0} amp_left:{1,0:F7}  dsp_buf[dsp_i]:{2,0:F7} dsp_left_buf[dsp_i]:{3,0:F7}", dsp_i, amp_left, dsp_buf[dsp_i], dsp_left_buf[dsp_i]);
                }
            }
            else    /* The voice is not centered. Stereo samples have one side zero. */
            {
                if (amp_left != 0f)
                {
                    for (dsp_i = 0; dsp_i < count; dsp_i++)
                    {
                        dsp_left_buf[dsp_i] += amp_left * dsp_buf[dsp_i];
                        //if (dsp_i < 50) Debug.LogFormat("dsp_i:{0} amp_left:{1,0:F7}  dsp_buf[dsp_i]:{2,0:F7} dsp_left_buf[dsp_i]:{3,0:F7}", dsp_i, amp_left, dsp_buf[dsp_i], dsp_left_buf[dsp_i]);
                    }
                }

                if (amp_right != 0f)
                {
                    for (dsp_i = 0; dsp_i < count; dsp_i++)
                        dsp_right_buf[dsp_i] += amp_right * dsp_buf[dsp_i];
                }
            }

#if MPTK_PRO
            /* reverb send. Buffer may be NULL. */
            float levelReverb = reverb_send + synth.MPTK_EffectSoundFont.ReverbAmplify;
            /* chorus send. Buffer may be NULL. */
            float levelChorus = chorus_send + synth.MPTK_EffectSoundFont.ChorusAmplify;
#else
            /* reverb send. Buffer may be NULL. */
            float levelReverb = reverb_send;
            /* chorus send. Buffer may be NULL. */
            float levelChorus = chorus_send;
#endif
            if (levelReverb > 1f)
                levelReverb = 1f;
            if (levelChorus > 1f)
                levelChorus = 1f;

            if (dsp_reverb_buf != null && levelReverb > 0f)
            {
                for (dsp_i = 0; dsp_i < count; dsp_i++)
                    dsp_reverb_buf[dsp_i] += levelReverb * dsp_buf[dsp_i];
            }

            //Debug.Log("amp_chorus:" + amp_chorus + " MPTK_ChorusAmplify:" + synth.MPTK_ChorusAmplify + " --> " + levelChorus));

            if (dsp_chorus_buf != null && levelChorus > 0f)
            {
                for (dsp_i = 0; dsp_i < count; dsp_i++)
                    dsp_chorus_buf[dsp_i] += levelChorus * dsp_buf[dsp_i];
            }
        }

        public IEnumerator<float> Release()
        {
            //Debug.Log("Release " + IdVoice);
            fluid_rvoice_noteoff(true);
            yield return 0;
        }

        /// <summary>@brief
        /// Move phase enveloppe to release //FS fluid_rvoice_noteoff_LOCAL
        /// </summary>
        public void fluid_rvoice_noteoff(bool force = false
                    /*,[CallerMemberName] string nomAppelant = "",
                    [CallerFilePath] string filePath = "",
                    [CallerLineNumber] int lineNumber = 0*/)
        {
            //Debug.Log($"fluid_rvoice_noteoff: {filePath}/{lineNumber} {nomAppelant}");
            if (status == fluid_voice_status.FLUID_VOICE_ON || status == fluid_voice_status.FLUID_VOICE_SUSTAINED)
            {
                if (!weakDevice)
                {
                    if (!force && channel != null && channel.Controller(MPTKController.Sustain) >= 64)
                    {
                        if (synth.VerboseController)
                            if (status != fluid_voice_status.FLUID_VOICE_SUSTAINED) Debug.Log($"fluid_voice_noteoff Channel:{chan} key:{key} set status to FLUID_VOICE_SUSTAINED");
                        status = fluid_voice_status.FLUID_VOICE_SUSTAINED;
                    }
                    else
                    {
                        if (synth.VerboseVoice) Debug.Log($"fluid_voice_noteoff {id} Duration:{TimeSpan.FromTicks(DateTime.UtcNow.Ticks - TimeAtStart).TotalSeconds:F2} Name:{sample.Name}");
                        if (volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK)
                        {
                            // A voice is turned off during the attack section of the volume envelope.  
                            // The attack section ramps up linearly with amplitude. 
                            // The other sections use logarithmic scaling. 
                            // Calculate new volenv_val to achieve equivalent amplitude during the release phase for seamless volume transition.

                            //if (synth.VerboseEnvVolume) DebugVolEnv("noteoff ATTACK");
                            if (volenv_val > 0)
                            {
                                float env_value;
                                if (synth.MPTK_ApplyModLfo)
                                {
                                    /* FS 2.3
                                        fluid_real_t lfo = voice->envlfo.modlfo.val * -voice->envlfo.modlfo_to_vol;
                                        fluid_real_t amp = voice->envlfo.volenv.val * fluid_cb2amp(lfo);
                                        fluid_real_t env_value = - (((-200.f / FLUID_M_LN10) * FLUID_LOGF(amp) - lfo) / FLUID_PEAK_ATTENUATION - 1);
                                        fluid_clip(env_value, 0.0f, 1.0f);
                                        voice->envlfo.volenv= env_value;
                                    */
                                    float lfo = modlfo_val * -modlfo_to_vol;
                                    float amp = volenv_val * (float)fluid_conv.fluid_cb2amp(lfo);
                                    env_value = -(((-200f / fluid_conv.M_LN10) * Mathf.Log(amp) - lfo) / fluid_conv.FLUID_PEAK_ATTENUATION - 1);
                                    // before 2.14.1
                                    // float lfo = modlfo_val * -modlfo_to_vol;
                                    // float vol = volenv_val * Mathf.Pow(10f, lfo / -200f);
                                    // env_value = -((-200f * Mathf.Log(vol) / Mathf.Log(10f) - lfo) / fluid_conv.FLUID_PEAK_ATTENUATION - 1f);
                                }
                                else
                                {
                                    env_value = -(((-200f / fluid_conv.M_LN10) * Mathf.Log(amp)) / fluid_conv.FLUID_PEAK_ATTENUATION - 1);
                                    //env_value = Convert.ToInt64(-((-200f * Mathf.Log(volenv_val) / Mathf.Log(10f)) / fluid_conv.FLUID_PEAK_ATTENUATION - 1f));
                                }
                                volenv_val = env_value > 1 ? 1 : env_value < 0 ? 0 : env_value;
                            }
                            if (synth.VerboseEnvVolume) DebugVolEnv("noteoff ATTACK");
                        }
                        volenv_section = fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE;
                        volenv_count = 0;
                        if (synth.VerboseEnvVolume) DebugVolEnv("noteoff");

                        modenv_section = fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE;
                        modenv_count = 0;
                        if (synth.VerboseEnvModulation) DebugModEnv("noteoff");
                    }
                }
                else
                {
                    volenv_section = fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE;
                    volenv_count = 0;
                }
            }
        }


        /*
         * fluid_voice_kill_excl
         *
         * Percussion sounds can be mutually exclusive: for example, a 'closed
         * hihat' sound will terminate an 'open hihat' sound ringing at the
         * same time. This behaviour is modeled using 'exclusive classes',
         * turning on a voice with an exclusive class other than 0 will kill
         * all other voices having that exclusive class within the same preset
         * or channel.  fluid_voice_kill_excl gets called, when 'voice' is to
         * be killed for that reason.
         */
        public int fluid_voice_kill_excl()
        {
            if (!(status == fluid_voice_status.FLUID_VOICE_ON) || status == fluid_voice_status.FLUID_VOICE_SUSTAINED)
            {
                return 0;
            }

            // Turn off the exclusive class information for this voice, so that it doesn't get killed twice fluid_voice_gen_set(voice, GEN_EXCLUSIVECLASS, 0);
            gen[(int)fluid_gen_type.GEN_EXCLUSIVECLASS].Val = 0f;
            gen[(int)fluid_gen_type.GEN_EXCLUSIVECLASS].flags = fluid_gen_flags.GEN_SET;

            if (synth.VerboseSpecialNoteOff) DebugKillByExclusive($"Release sample with class {gen[(int)fluid_gen_type.GEN_EXCLUSIVECLASS].Val}");

            /* If the voice is not yet in release state, put it into release state */
            if (volenv_section != fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE)
            {
                volenv_section = fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE;
                volenv_count = 0;
                modenv_section = fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE;
                modenv_count = 0;
            }

            // Speed up the volume envelope 
            // The value was found through listening tests with hi-hat samples. 
            //fluid_voice_gen_set(voice, GEN_VOLENVRELEASE, -200);
            gen[(int)fluid_gen_type.GEN_VOLENVRELEASE].Val = -200f;
            gen[(int)fluid_gen_type.GEN_VOLENVRELEASE].flags = fluid_gen_flags.GEN_SET;
            fluid_voice_update_param((int)fluid_gen_type.GEN_VOLENVRELEASE);

            // Speed up the modulation envelope 
            //fluid_voice_gen_set(voice, GEN_MODENVRELEASE, -200);
            gen[(int)fluid_gen_type.GEN_MODENVRELEASE].Val = -200f;
            gen[(int)fluid_gen_type.GEN_MODENVRELEASE].flags = fluid_gen_flags.GEN_SET;
            fluid_voice_update_param((int)fluid_gen_type.GEN_MODENVRELEASE);

            return 0;
        }

        /*
        * fluid_voice_off
        *
        * Purpose:
        * Turns off a voice, meaning that it is not processed
        * anymore by the DSP loop.
        */
        public void fluid_voice_off()
        {
            chan = NO_CHANNEL;
            volenv_section = fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED;
            if (synth.VerboseVoice) DebugSynth("fluid_voice_off");
            volenv_count = 0;
            if (VoiceAudio != null && VoiceAudio.Audiosource != null)
            {
                VoiceAudio.Audiosource.volume = 0;
            }
            modenv_section = fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED;
            modenv_count = 0;
            status = fluid_voice_status.FLUID_VOICE_OFF;
        }

        public void DebugKillByExclusive(string info)
        {
            Debug.LogFormat($"KillByExclusive - [{id,4}] TimeFromStart:{TicksToMilli(ticks - TimeAtStart)} ms Delta:{TicksToMilliF(DeltaTimeWrite):F2} ms {sample.Name} {info}");
        }

        public void DebugSynth(string info)
        {
            Debug.LogFormat($"Synth - [{id,4}] TimeFromStart:{TicksToMilli(ticks - TimeAtStart)} ms Delta:{TicksToMilliF(DeltaTimeWrite):F2} ms {info}");
        }

        public void DebugOverload(string info)
        {
            Debug.LogFormat($"Overload - [{id,4}] TimeFromStart:{TicksToMilli(ticks - TimeAtStart)} ms Delta:{TicksToMilliF(DeltaTimeWrite):F2} ms {info}");
        }

        public void DebugVolume(string info)
        {
            Debug.LogFormat($"Volume - [{id,4}] TimeFromStart:{TicksToMilli(ticks - TimeAtStart)} ms Delta:{TicksToMilliF(DeltaTimeWrite):F2} ms {volenv_section} {info}");
        }

        public void DebugVolEnv(string info)
        {
            Debug.LogFormat($"VolEnv - [{id,4}] {info,-25} " +
                $"TimeFromStart:{TicksToMilli(ticks - TimeAtStart)} ms Delta:{TicksToMilliF(DeltaTimeWrite):F2} ms " +
                $"section:{volenv_section} volenv_val:{volenv_val:F3} " +
                $"volenv_count:{volenv_data[(int)volenv_section].count} incr:{volenv_data[(int)volenv_section].incr:F4} coeff:{volenv_data[(int)volenv_section].coeff:F2}");
        }

        public void DebugModEnv(string info)
        {
            Debug.LogFormat("ModEnv - [{0,4}] {1,-15} TimeFromStart:{2:0.000} Delta:{3:0.000} section:{4} modenv_val:{5:0.000} modenv_count:{6} incr:{7:0.0000}",
           id, info, TicksToMilli(ticks - TimeAtStart), TicksToMilliF(DeltaTimeWrite),
           modenv_section, modenv_val, modenv_data[(int)modenv_section].count, modenv_data[(int)modenv_section].incr);
        }

        public void DebugLFO(string info)
        {
            Debug.LogFormat("[{0,4}] {1,-15} TimeFromStart:{2:00000.000} Delta:{3:0.000} modlfo_delay:{4} modlfo_incr:{5:0.000} modlfo_val:{6:0.000} modlfo_to_vol:{7:0.000}",
               id, info, TicksToMilli(ticks - TimeAtStart), TicksToMilliF(DeltaTimeWrite), modlfo_delay, modlfo_incr, modlfo_val, modlfo_to_vol);
        }
        public void DebugVib(string info)
        {
            Debug.LogFormat("[{0,4}] {1,-15} TimeFromStart:{2:00000.000} Delta:{3:0.000} viblfo_delay:{4} viblfo_incr:{5:0.000} viblfo_val:{6:0.000} viblfo_to_pitch:{7:0.000} -. pitch mod:{8:0.000}",
               id, info, TicksToMilli(ticks - TimeAtStart), TicksToMilliF(DeltaTimeWrite), viblfo_delay, viblfo_incr, viblfo_val, viblfo_to_pitch, (float)(1d + viblfo_val * viblfo_to_pitch / 1000d));
        }
    }
}
