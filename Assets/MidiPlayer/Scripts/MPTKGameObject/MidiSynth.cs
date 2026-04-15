//#define MPTK_PRO
//#define DEBUG_GC

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
using Oboe.Stream;
#endif

namespace MidiPlayerTK
{
    public enum fluid_loop
    {
        FLUID_UNLOOPED = 0,
        FLUID_LOOP_DURING_RELEASE = 1,
        FLUID_NOTUSED = 2,
        FLUID_LOOP_UNTIL_RELEASE = 3
    }

    public enum fluid_synth_status
    {
        FLUID_SYNTH_CLEAN,
        FLUID_SYNTH_PLAYING,
        FLUID_SYNTH_QUIET,
        FLUID_SYNTH_STOPPED
    }

    // Flags to choose the interpolation method 
    public enum fluid_interp
    {
        None, // no interpolation: Fastest, but questionable audio quality
        Linear, // Straight-line interpolation: A bit slower, reasonable audio quality
        Cubic, // Fourth-order interpolation: Requires 50 % of the whole DSP processing time, good quality 
        Order7,
    }
    /// @ingroup synth_engine_runtime
    /// <summary>@brief
    /// Core sample-based SoundFont synthesizer (base class).
    /// </summary>
    /// <remarks>
    /// Loads a SoundFont and its samples, processes incoming MIDI events, and renders voices (controllers, generators, etc.).
    /// The synthesis engine is a C# adaptation of FluidSynth tailored for Unity audio integration.
    /// This class is the common base used by the MPTK prefabs:
    /// <see cref="MidiStreamPlayer"/>, <see cref="MidiFilePlayer"/>, and <see cref="MidiInReader"/>.
    ///
    /// Do not instantiate <see cref="MidiSynth"/> directly. Instead, add one of the MPTK prefabs to your scene and access its
    /// properties and methods from your scripts.
    ///
    /// <para>Examples:</para>
    /// <code>
    /// midiFilePlayer.MPTK_ChorusDelay = 0.2f;
    /// midiStreamPlayer.MPTK_InitSynth();
    /// </code>
    /// </remarks>
#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
    public partial class MidiSynth : MonoBehaviour, IMixerProcessor
    {
#else
    //[ExecuteAlways]
    public partial class MidiSynth : MonoBehaviour
    {
#endif
        // @cond NODOC

        public const float FLUID_REVERB_DEFAULT_ROOMSIZE = 0.5f;    /**< Default reverb room size */
        public const float FLUID_REVERB_DEFAULT_DAMP = 0.3f;     /**< Default reverb damping */
        public const float FLUID_REVERB_DEFAULT_WIDTH = 0.8f; /**< Default reverb width */
        public const float FLUID_REVERB_DEFAULT_LEVEL = 0.7f;/**< Default reverb level */

        public const int FLUID_CHORUS_DEFAULT_N = 3;                       /**< Default chorus voice count */
        public const float FLUID_CHORUS_DEFAULT_LEVEL = 0.6f;              /**< Default chorus level */
        public const float FLUID_CHORUS_DEFAULT_SPEED = 0.2f;               /**< Default chorus speed */
        public const float FLUID_CHORUS_DEFAULT_DEPTH = 4.25f;               /**< Default chorus depth */
        public const float FLUID_CHORUS_DEFAULT_WIDTH = 10f;               /**< Default chorus width gradually stereo effect*/

        [HideInInspector, NonSerialized]
        public AudioSource CoreAudioSource;

        /// <summary>@brief
        /// Time in milliseconds since playback started.
        /// </summary>
        protected double timeMidiFromStartPlay = 0d;

        //[HideInInspector]
        public bool audioThreadMidi = false;

        public bool AudioThreadMidi
        {
            get => audioThreadMidi;
            set => audioThreadMidi = value;
        }


        [HideInInspector]
        public int priorityThreadMidi = 0;

        public int ThreadMidiPriorityReal
        {
            get
            {
                // Try reading real value
                if (midiThread != null)
                    return midiThread.Priority - System.Threading.ThreadPriority.Normal;
                return -1;
            }
        }
        [HideInInspector] // defined in custom inspector
        [Range(1, 100)]
        public int DevicePerformance = 40;

        /// <summary>@brief
        /// Time in milliseconds of the current MIDI playback position.
        /// </summary>
        protected double lastTimeMidi = 0d;

        public System.Diagnostics.Stopwatch watchPerfMidi;

        [HideInInspector] // defined in custom inspector
        [Range(0, 100)]
        public float MaxDspLoad = 40f;

        // has the synth module been initialized? 
        private static int lastIdSynth;
        [NonSerialized]
        public int IdSynth;
        [NonSerialized]
        public int IdSession;


        [HideInInspector, NonSerialized]
        public int FLUID_BUFSIZE = 64;
#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
        [HideInInspector, NonSerialized]
        public int FLUID_MAX_BUFSIZE = 2048;
        public int AudioBufferLenght => oboeAudioStream.BufferSizeInFrames;
        public int AudioNumBuffers => oboeAudioStream.FramesPerCallback;
        public string AudioEngine => oboeAudioStream.UsesAAudio ? "AAudio" : "OpenSLES";
#else
        [HideInInspector, NonSerialized]
        public int FLUID_MAX_BUFSIZE = 64;
        public int AudioBufferLenght; // value from AudioSettings with FMOD
        public int AudioNumBuffers; // value from AudioSettings with FMOD
        public string AudioEngine = "FMOD";
#endif

        //[NonSerialized]
        public float OutputRate;

        [HideInInspector, NonSerialized]
        public int DspBufferSize;


        ///// <summary>@brief
        ///// Enable the reset Maestro channel extension information when MIDI start playing. Default is true.
        ///// - ForcedBank is disable
        ///// - ForcedPreset is disable
        ///// - Enable is set to true
        ///// - Volume is set to max (1)
        ///// </summary>
        //[Tooltip("When a MIDI is starting playing and if true all Maestro channel extension information are restored to default value: enable, volume, forced instrument")]
        //public bool MPTK_ResetChannel = true;

        //[HideInInspector]
        /// <summary>@brief
        /// Presets are often made of two or more samples—typically left and right channels. Enable this to play only the first sample found.
        /// </summary>
        public bool playOnlyFirstWave;

        // Set to be true when playing from MidiFileWriter
        public bool playNoteOff = false;

        // @endcond

        /* GM and GS  soundfonts are the most popular soundfonts. I was not able to find MMA and XG soundfont for testing.
           The default bank style for MPTK is GS (127 banks, CC32 ignored) which is compliant with GM (one bank, CC0 and CC32 ignored, also there is no bank change)
 /// <summary>@brief
 /// GM(General MIDI) : A standard SoundFont that provides 128 basic instrument sounds for consistent playback across different devices.
 /// GS(General Standard): An extended MIDI standard by Roland, offering additional instruments, effects, and enhanced control.
 /// MMA(Musical Instrument Digital Interface Manufacturers Association): High-quality, versatile SoundFont designed for a wide range of software and hardware compatibility.
 /// XG(Extended General MIDI): Yamaha's expanded MIDI standard, providing more instrument sounds, effects, and detailed control over MIDI performance.
 /// </summary>
                public enum StyleBank
                {
                    FLUID_BANK_STYLE_GM,  // GM style, bank = 0 always (CC0/MSB and CC32/LSB ignored)
                    FLUID_BANK_STYLE_GS,  // GS style, bank = CC0/MSB (CC32/LSB ignored) - Default
                    FLUID_BANK_STYLE_XG,  // XG style, bank = CC32/LSB (CC0/MSB ignored) 
                    FLUID_BANK_STYLE_MMA, // MMA style bank = 128*MSB+LSB 
                }
                static public string[] StyleBankLabel = { "GM - General MIDI", "GS - General Standard", "MMA - Musical Instrument Digital Interface Manufacturers Association", "XG - Extended General MIDI"};
                public StyleBank MPTK_StyleBank;
        */
        // @endcond

        /// @name SoundFont Management
        /// @brief Runtime loading, caching, and access to SoundFont banks and presets.
        ///
        /// @details
        /// This section provides the API used to manage SoundFonts in Maestro MPTK.
        ///
        /// A SoundFont defines the instruments, banks, presets, and samples used by the
        /// synthesizer to render MIDI events. MPTK allows SoundFonts to be loaded and
        /// switched dynamically while the application is running.
        ///
        /// SoundFonts can be loaded from:
        /// - the internal MPTK SoundFont database
        /// - a local file (file://)
        /// - a remote resource (http:// or https://)
        ///
        /// When loading from external sources, SoundFonts can optionally be downloaded,
        /// cached locally, or reused from the cache to reduce loading time.
        ///
        /// This API also provides access to the structure of the loaded SoundFont,
        /// including the list of available banks and presets for instruments and drums.
        /// These lists can be used to build user interfaces or to select banks before
        /// applying MIDI preset changes.
        ///
        /// Additionally, callbacks are available to monitor download progress and to be
        /// notified when a SoundFont has finished loading.
        ///
        /// @note
        /// Changing banks or presets through this API updates the available preset lists
        /// but does not directly change the currently active instrument in the synthesizer.
        /// To change instruments during playback, use the MIDI channel API
        /// (see MidiStreamPlayer.MPTK_Channels for the MIDI Stream prefab).        
        /// @{

        /// <summary>@brief
        /// At runtime, SoundFonts can be loaded dynamically from the local file system or directly from the web.
        /// This avoids embedding a SoundFont in your build, which is ideal for in-app purchases or downloadable content.
        /// Legacy mode still allows loading SoundFonts from the internal MPTK database.
        /// Maestro MPTK also lets you assign different SoundFonts to different MIDI players (MidiFilePlayer, MidiStreamPlayer, ...), enabling flexible, per-player audio rendering.
        /// @version 2.15
        /// An instance of this class is automatically created when the MPTK prefab is loaded in the scene. See MPTKSoundFont class for more details.
        /// @note
        /// @code
        /// @endcode
        /// </summary>
        [HideInInspector, NonSerialized]
        public MPTKSoundFont MPTK_SoundFont = null;

        /// @}

        /// @name Synthesizer Initialization And Life Cycle
        /// @brief Initialization and lifecycle of the MIDI synthesizer.
        /// @details
        /// This section contains methods and parameters related to the creation
        /// and initialization of the MIDI synthesizer. These functions configure
        /// the audio engine, load SoundFonts, and prepare the synth for playback.
        ///
        /// In most cases initialization is handled automatically by the prefab,
        /// but advanced users may control it directly from scripts.

        /// @{

        /// <summary>@brief
        /// Initializes the synthesizer:
        /// - channel information (instrument and bank selection, pitch, volume, ...)
        /// - voice cache
        /// - synth modulators.
        /// This method is called by MidiFilePlayer during MPTK_Play().
        /// In some cases you may need to call MPTK_InitSynth to restore the synth's initial state (mainly with MidiStreamPlayer).
        /// </summary>
        /// <param name="channelCount">Number of channels to create. Default is 16. Other values are experimental.</param>
        /// <param name="preserveChannelInfo">If true, channel information is not reset. Default is false, which reinitializes channel information.</param>
        public void MPTK_InitSynth(int channelCount = 16, bool preserveChannelInfo = false, bool preserveActivVoice = true)
        {
            if (VerboseSynth) Debug.Log($"MPTK_InitSynth. IdSynth:{IdSynth}, Name:{name}, Channels:{channelCount}, preserveChannelInfo:{preserveChannelInfo}, state was {state}");
            fluid_voice.LastId = 0;

            if (channelCount > 256)
                channelCount = 256;

            if (channelCount < 16)
                channelCount = 16;

            // no more experimental with 2.12.2 - Debug.Log($"MPTK_InitSynth. Experimental feature activated, {channelCount} channels are used");

            // v2.10.1 possibility to not reset channels information.
            if (Channels == null || Channels.EnableResetChannel || Channels.Length != channelCount || !preserveChannelInfo)
            {
                Channels = new MPTKChannels(this, channelCount);
            }

            if (ActiveVoices == null)
                ActiveVoices = new List<fluid_voice>();

            if (!preserveActivVoice)
                // Needed when another soundfont is loaded
                ActiveVoices.Clear();

            FreeVoices = new List<fluid_voice>();

            QueueSynthCommand = new Queue<SynthCommand>();
            QueueMidiEvents = new Queue<List<MPTKEvent>>();

            if (state != fluid_synth_status.FLUID_SYNTH_PLAYING)
                MPTK_InitModulators();

            MPTK_ResetStat();
            state = fluid_synth_status.FLUID_SYNTH_PLAYING;

            if (!MPTK_CorePlayer && !handleManageCacheVoice.IsRunning)
                // To be run only one time
                handleManageCacheVoice = Routine.RunCoroutine(RoutineManageCacheVoice(), Segment.RealtimeUpdate);
            //else Debug.Log(handleManageCacheVoice.IsRunning);

            /* TU Synth 
            MPTK_PlayDirectEvent(new MPTKEvent() { Command= MPTKCommand.PatchChange, Value=18 });
            MPTK_PlayDirectEvent(new MPTKEvent() { Command= MPTKCommand.NoteOn, Value=60, Duration=-1 });
            MPTK_PlayDirectEvent(new MPTKEvent() { Command= MPTKCommand.NoteOn, Value=62, Duration=-1 });
            MPTK_PlayDirectEvent(new MPTKEvent() { Command= MPTKCommand.NoteOn, Value=64, Duration=-1 });
            MPTK_PlayDirectEvent(new MPTKEvent() { Command= MPTKCommand.NoteOn, Value=66, Duration=-1 });
            MPTK_PlayDirectEvent(new MPTKEvent() { Command= MPTKCommand.NoteOn, Value=68, Duration=-1 });
            MPTK_PlayDirectEvent(new MPTKEvent() { Command= MPTKCommand.NoteOn, Value=70, Duration=-1 });
            MPTK_PlayDirectEvent(new MPTKEvent() { Command= MPTKCommand.NoteOn, Value=72, Duration=-1 });
            MPTK_PlayDirectEvent(new MPTKEvent() { Command= MPTKCommand.NoteOn, Value=74, Duration=-1 });
            CoreAudioSource.Play();
            */
        }

        /// <summary>@brief
        /// Initializes the default MIDI modulators for the synthesizer, mapping standard MIDI inputs to corresponding
        /// sound synthesis parameters according to SoundFont 2.01 specifications.
        /// </summary>
        /// <remarks>This method configures modulation sources and destinations so that MIDI events such
        /// as velocity, controller changes, channel pressure, and pitch wheel movements influence sound characteristics
        /// including attenuation, filter cutoff, vibrato depth, pan position, reverb, chorus, and pitch. It ensures
        /// that the synthesizer responds to MIDI input in a manner consistent with SoundFont standards, enabling
        /// expressive and dynamic sound generation. Call this method before processing MIDI events to guarantee correct
        /// modulator setup.</remarks>
        public void MPTK_InitModulators()
        {
            if (VerboseSynth) Debug.Log($"MPTK_InitModulators. IdSynth:{IdSynth}, Channels:{Channels.Length}, state was {state} {fluid_conv.FLUID_PEAK_ATTENUATION}");
            fluid_conv.fluid_conversion_config();

            //TBC fluid_dsp_float_config();
            //fluid_sys_config();
            //init_dither(); // pour fluid_synth_write_s16 ?

            /* SF2.01 page 53 section 8.4.1: MIDI Note-On Velocity to Initial Attenuation
             * */
            fluid_mod_set_source1(default_vel2att_mod, /* The modulator we are programming here */
                (int)fluid_mod_src.FLUID_MOD_VELOCITY,    /* Source. VELOCITY corresponds to 'index=2'. */
                (int)fluid_mod_flags.FLUID_MOD_GC           /* Not a MIDI continuous controller */
                | (int)fluid_mod_flags.FLUID_MOD_CONCAVE    /* Curve shape. Corresponds to 'type=1' */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR   /* Polarity. Corresponds to 'P=0' */
                | (int)fluid_mod_flags.FLUID_MOD_NEGATIVE   /* Direction. Corresponds to 'D=1' */
            );
            fluid_mod_set_source2(default_vel2att_mod, 0, 0); /* No 2nd source */
            fluid_mod_set_dest(default_vel2att_mod, (int)fluid_gen_type.GEN_ATTENUATION);  /* Target: Initial attenuation */
            fluid_mod_set_amount(default_vel2att_mod, fluid_conv.FLUID_PEAK_ATTENUATION);          /* Modulation amount: 960 */

            /* SF2.01 page 53 section 8.4.2: MIDI Note-On Velocity to Filter Cutoff
             * Have to make a design decision here. The specs don't make any sense this way or another.
             * One sound font, 'Kingston Piano', which has been praised for its quality, tries to
             * override this modulator with an amount of 0 and positive polarity (instead of what
             * the specs say, D=1) for the secondary source.
             * So if we change the polarity to 'positive', one of the best free sound fonts works...
             */

            fluid_mod_set_source1(default_vel2filter_mod, (int)fluid_mod_src.FLUID_MOD_VELOCITY, /* Index=2 */
                (int)fluid_mod_flags.FLUID_MOD_GC                        /* CC=0 */
                | (int)fluid_mod_flags.FLUID_MOD_LINEAR                  /* type=0 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                /* P=0 */
                | (int)fluid_mod_flags.FLUID_MOD_NEGATIVE                /* D=1 */
            );
            fluid_mod_set_source2(default_vel2filter_mod, (int)fluid_mod_src.FLUID_MOD_VELOCITY, /* Index=2 */
                (int)fluid_mod_flags.FLUID_MOD_GC                                 /* CC=0 */
                | (int)fluid_mod_flags.FLUID_MOD_SWITCH                           /* type=3 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                         /* P=0 */
                // do not remove       | FLUID_MOD_NEGATIVE                         /* D=1 */
                | (int)fluid_mod_flags.FLUID_MOD_POSITIVE                         /* D=0 */
            );
            fluid_mod_set_dest(default_vel2filter_mod, (int)fluid_gen_type.GEN_FILTERFC);        /* Target: Initial filter cutoff */
            fluid_mod_set_amount(default_vel2filter_mod, -2400);

            /* SF2.01 page 53 section 8.4.3: MIDI Channel pressure to Vibrato LFO pitch depth
             * */
            fluid_mod_set_source1(default_at2viblfo_mod, (int)fluid_mod_src.FLUID_MOD_CHANNELPRESSURE, /* Index=13 */
                (int)fluid_mod_flags.FLUID_MOD_GC                        /* CC=0 */
                | (int)fluid_mod_flags.FLUID_MOD_LINEAR                  /* type=0 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                /* P=0 */
                | (int)fluid_mod_flags.FLUID_MOD_POSITIVE                /* D=0 */
            );
            fluid_mod_set_source2(default_at2viblfo_mod, 0, 0); /* no second source */
            fluid_mod_set_dest(default_at2viblfo_mod, (int)fluid_gen_type.GEN_VIBLFOTOPITCH);        /* Target: Vib. LFO => pitch */
            fluid_mod_set_amount(default_at2viblfo_mod, 50);


            /* SF2.01 page 53 section 8.4.4: Mod wheel (Controller 1) to Vibrato LFO pitch depth
             * */
            fluid_mod_set_source1(default_mod2viblfo_mod, (int)MPTKController.Modulation, /* Index=1 */
                (int)fluid_mod_flags.FLUID_MOD_CC                        /* CC=1 */
                | (int)fluid_mod_flags.FLUID_MOD_LINEAR                  /* type=0 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                /* P=0 */
                | (int)fluid_mod_flags.FLUID_MOD_POSITIVE                /* D=0 */
            );
            fluid_mod_set_source2(default_mod2viblfo_mod, 0, 0); /* no second source */
            fluid_mod_set_dest(default_mod2viblfo_mod, (int)fluid_gen_type.GEN_VIBLFOTOPITCH);        /* Target: Vib. LFO => pitch */
            fluid_mod_set_amount(default_mod2viblfo_mod, 50);


            /* SF2.01 page 55 section 8.4.5: MIDI continuous controller 7 to initial attenuation
             */
            fluid_mod_set_source1(default_att_mod, (int)MPTKController.VOLUME_MSB,                     /* index=7 */
                (int)fluid_mod_flags.FLUID_MOD_CC                              /* CC=1 */
                | (int)fluid_mod_flags.FLUID_MOD_CONCAVE                       /* type=1 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                      /* P=0 */
                | (int)fluid_mod_flags.FLUID_MOD_NEGATIVE                      /* D=1 */
            );
            fluid_mod_set_source2(default_att_mod, 0, 0);                 /* No second source */
            fluid_mod_set_dest(default_att_mod, (int)fluid_gen_type.GEN_ATTENUATION);         /* Target: Initial attenuation */
            fluid_mod_set_amount(default_att_mod, fluid_conv.FLUID_PEAK_ATTENUATION);                 /* Amount: 960 */


            /* SF2.01 page 55 section 8.4.6 MIDI continuous controller 10 to Pan Position
             * */
            fluid_mod_set_source1(default_pan_mod, (int)MPTKController.Pan,                    /* index=10 */
                (int)fluid_mod_flags.FLUID_MOD_CC                              /* CC=1 */
                | (int)fluid_mod_flags.FLUID_MOD_LINEAR                        /* type=0 */
                | (int)fluid_mod_flags.FLUID_MOD_BIPOLAR                       /* P=1 */
                | (int)fluid_mod_flags.FLUID_MOD_POSITIVE                      /* D=0 */
            );
            fluid_mod_set_source2(default_pan_mod, 0, 0);                 /* No second source */
            fluid_mod_set_dest(default_pan_mod, (int)fluid_gen_type.GEN_PAN);

            // Target: pan - Amount: 500. The SF specs $8.4.6, p. 55 syas: "Amount = 1000 tenths of a percent". 
            // The center value (64) corresponds to 50%, so it follows that amount = 50% x 1000/% = 500. 
            fluid_mod_set_amount(default_pan_mod, 500.0f);


            /* SF2.01 page 55 section 8.4.7: MIDI continuous controller 11 to initial attenuation
             * */
            fluid_mod_set_source1(default_expr_mod, (int)MPTKController.Expression,                     /* index=11 */
                (int)fluid_mod_flags.FLUID_MOD_CC                              /* CC=1 */
                | (int)fluid_mod_flags.FLUID_MOD_CONCAVE                       /* type=1 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                      /* P=0 */
                | (int)fluid_mod_flags.FLUID_MOD_NEGATIVE                      /* D=1 */
            );
            fluid_mod_set_source2(default_expr_mod, 0, 0);                 /* No second source */
            fluid_mod_set_dest(default_expr_mod, (int)fluid_gen_type.GEN_ATTENUATION);         /* Target: Initial attenuation */
            fluid_mod_set_amount(default_expr_mod, fluid_conv.FLUID_PEAK_ATTENUATION);                 /* Amount: 960 */


            /* SF2.01 page 55 section 8.4.8: MIDI continuous controller 91 to Reverb send
             * */
            fluid_mod_set_source1(default_reverb_mod, (int)MPTKController.EFFECTS_DEPTH1,                 /* index=91 */
                (int)fluid_mod_flags.FLUID_MOD_CC                              /* CC=1 */
                | (int)fluid_mod_flags.FLUID_MOD_LINEAR                        /* type=0 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                      /* P=0 */
                | (int)fluid_mod_flags.FLUID_MOD_POSITIVE                      /* D=0 */
            );
            fluid_mod_set_source2(default_reverb_mod, 0, 0);              /* No second source */
            fluid_mod_set_dest(default_reverb_mod, (int)fluid_gen_type.GEN_REVERBSEND);       /* Target: Reverb send */
            fluid_mod_set_amount(default_reverb_mod, 200);                /* Amount: 200 ('tenths of a percent') */


            /* SF2.01 page 55 section 8.4.9: MIDI continuous controller 93 to Reverb send
             * */
            fluid_mod_set_source1(default_chorus_mod, (int)MPTKController.EFFECTS_DEPTH3,                 /* index=93 */
                (int)fluid_mod_flags.FLUID_MOD_CC                              /* CC=1 */
                | (int)fluid_mod_flags.FLUID_MOD_LINEAR                        /* type=0 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                      /* P=0 */
                | (int)fluid_mod_flags.FLUID_MOD_POSITIVE                      /* D=0 */
            );
            fluid_mod_set_source2(default_chorus_mod, 0, 0);              /* No second source */
            fluid_mod_set_dest(default_chorus_mod, (int)fluid_gen_type.GEN_CHORUSSEND);       /* Target: Chorus */
            fluid_mod_set_amount(default_chorus_mod, 200);                /* Amount: 200 ('tenths of a percent') */


            /* SF2.01 page 57 section 8.4.10 MIDI Pitch Wheel to Initial Pitch ... */
            /* Initial Pitch is not a "standard" generator, because it isn't mentioned in the
               list of generators in the SF2 specifications. That's why destination Initial Pitch
               is replaced here by fine tune generator.
             */
            fluid_mod_set_source1(default_pitch_bend_mod, (int)fluid_mod_src.FLUID_MOD_PITCHWHEEL, /* Index=14 */
                (int)fluid_mod_flags.FLUID_MOD_GC                              /* CC =0 */
                | (int)fluid_mod_flags.FLUID_MOD_LINEAR                        /* type=0 */
                | (int)fluid_mod_flags.FLUID_MOD_BIPOLAR                       /* P=1 */
                | (int)fluid_mod_flags.FLUID_MOD_POSITIVE                      /* D=0 */
            );
            fluid_mod_set_source2(default_pitch_bend_mod, (int)fluid_mod_src.FLUID_MOD_PITCHWHEELSENS,  /* Index = 16 */
                (int)fluid_mod_flags.FLUID_MOD_GC                                        /* CC=0 */
                | (int)fluid_mod_flags.FLUID_MOD_LINEAR                                  /* type=0 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                                /* P=0 */
                | (int)fluid_mod_flags.FLUID_MOD_POSITIVE                                /* D=0 */
            );
            fluid_mod_set_dest(default_pitch_bend_mod,/* (int)fluid_gen_type.GEN_PITCH replace with FS 2.3*/  (int)fluid_gen_type.GEN_FINETUNE);                 /* Destination: Initial pitch */
            fluid_mod_set_amount(default_pitch_bend_mod, 12700.0f);                 /* Amount: 12700 cents */
        }

        /// <summary>@brief
        /// Gets or sets the initial volume level of the synthesizer at application startup. This property allows for a
        /// gradual increase in volume to prevent abrupt or unusual sounds when the application begins.
        /// </summary>
        /// <remarks>Set this property to 1 for immediate full volume at startup. Valid values range from
        /// 0.001 to 1. Using a lower value can help avoid unexpected audio artifacts during initialization.</remarks>
        [Range(0.001f, 1f)]
        [Header("Lean volume startup")]
        [Tooltip("Lean startup of the volume of the synth is useful to avoid weird sound at the beginning of the application. Set to 1 for an immediate full volume at startup.")]
        public float MPTK_LeanSynthStarting = 0.05f;

        /// <summary>@brief
        /// Starts the MIDI sequencer: each MIDI event is read and played on a dedicated thread.
        /// This thread is started automatically by the MidiFilePlayer, MidiListPlayer, and MidiExternalPlayer prefabs.
        /// @note Available only when MPTK_CorePlayer is true. No effect in Legacy mode.
        /// </summary>
        public void MPTK_StartSequencerMidi()
        {
            if (MPTK_CorePlayer)
            {
                if (!AudioThreadMidi)
                {
                    if (VerboseSynth) Debug.Log($"MPTK_StartSequencerMidi {this.name} {((midiThread == null ? "Null" : "Alive:" + midiThread.IsAlive))}");
                    if (midiThread != null)
                    {
                        //Debug.LogWarning($"MPTK_StartSequencerMidi {this.name} already alive {midiThread.IsAlive}");
                        if (!midiThread.IsAlive)
                            midiThread.Start();
                    }
                    else
                    {
                        if (midiThread == null)
                        {
                            midiThread = new Thread(ThreadMidiPlayer);
                        }

                        if (midiThread != null && !midiThread.IsAlive)
                        {
                            midiThread.Name = "MidiThread_" + IdSynth.ToString();
                            midiThread.Priority = MPTK_ThreadMidiPriority + System.Threading.ThreadPriority.Normal;
                            midiThread.Start();
                            if (VerboseSynth) Debug.Log($"MPTK_StartSequencerMidi Started {this.name} {IdSynth} ManagedThreadId:{midiThread.ManagedThreadId}");
                        }
                        else if (VerboseSynth) Debug.Log($"MPTK_StartSequencerMidi: Alive  {this.name} {IdSynth} ManagedThreadId:{midiThread.ManagedThreadId} Priority:{midiThread.Priority}");
                    }
                }
                else if (VerboseSynth) Debug.Log($"MPTK_StartSequencerMidi - MIDI MPTK Thread disabled, MIDI reader from Audio Thread");
            }
        }

        /// <summary>@brief
        /// Starts processing samples by the synth and the MIDI sequencer.
        /// Useful only after calling MPTK_StopSynth when using MidiStreamPlayer.
        /// @version 2.11.2
        /// </summary>
        public void MPTK_StartSynth()
        {
            state = fluid_synth_status.FLUID_SYNTH_PLAYING;
        }

        /// <summary>@brief
        /// Stops processing samples by the synth and the MIDI sequencer.
        /// See also MPTK_StartSynth.
        /// </summary>
        public void MPTK_StopSynth()
        {
            if (VerboseSynth) Debug.Log($"MPTK_StopSynth. IdSynth:{IdSynth}, state was {state}");
            state = fluid_synth_status.FLUID_SYNTH_STOPPED;
        }


        /// <summary>@brief
        /// Clears all sound by sending NoteOff messages.
        /// This can take a few seconds because sample release times must play out.
        /// @code
        /// if (GUILayout.Button("Clear"))
        ///     midiStreamPlayer.MPTK_ClearAllSound(true);
        /// @endcode
        /// </summary>
        /// <param name="destroyAudioSource">Useful only in non-core mode.</param>
        /// <param name="_idSession">Clear only samples playing in this session; -1 clears all (default).</param>
        public void MPTK_ClearAllSound(bool destroyAudioSource = false, int _idSession = -1)
        {
            if (Application.isPlaying)
                Routine.RunCoroutine(ThreadClearAllSound(true), Segment.RealtimeUpdate);
            else
                Routine.RunCoroutine(ThreadClearAllSound(true), Segment.EditorUpdate);
        }

        /// @}

        /// @name Synthesizer Settings
        /// @brief Settings of the MIDI synthesizer.
        /// @details
        /// This section contains methods and parameters related the audio engine configuration.

        /// @{

        /// <summary>@brief
        /// Accept preset changes on Drum Channel 9 (percussion).
        /// Enabling this can produce unexpected results with MIDI files that are not standard-compliant.
        /// If disabled, preset changes on channel 9 are ignored (preset 0 remains in use).
        /// @note
        /// - Bank 128 and preset 0 are generally used for drum kits.
        /// @attention
        /// - Set to false in MidiFilePlayer.
        /// - Set to true in MidiStreamPlayer, where all MIDI events are generated by your script.
        /// </summary>
        [HideInInspector]
        public bool MPTK_EnablePresetDrum;

        /// <summary>@brief
        /// If the same note is hit twice on the same channel, the older voice is advanced to the release stage.
        /// This is the default MIDI processing behavior.
        /// </summary>
        [HideInInspector]
        public bool MPTK_ReleaseSameNote = true;

        /// <summary>@brief
        /// Finds the exclusive class of this voice. When enabled, stops all voices that share the exclusive class
        /// and were created after the first voice for this note-on event.
        /// </summary>
        [HideInInspector]
        public bool MPTK_KillByExclusiveClass = true;

        /// <summary>@brief
        /// When the value is true, NoteOff and Duration for non-looped samples are ignored and the samples play through to the end.
        /// </summary>
        public bool MPTK_KeepPlayingNonLooped
        {
            get { return keepPlayingNonLooped; }
            set { keepPlayingNonLooped = value; }
        }

        /// <summary>@brief
        /// When a note is stopped with a note-off or when its duration is over, it continues to play for a short time depending on the instrument.
        /// This parameter multiplies the default release time defined in the SoundFont for each instrument.
        /// Recommended values are between 0.1 and 10. Default is 1 (no change to the release time).
        /// Performance note: the longer the release, the more CPU is used after note-on, because many samples may play simultaneously.
        /// </summary>
        [Tooltip("Modify the default value of the release time")]
        [Range(0.1f, 10f)]
        public float MPTK_ReleaseTimeMod = 1f;

        /// <summary>@brief
        /// When the amplitude of a sample falls below this value, playback stops.
        /// Increasing the value can improve performance when many samples play concurrently, but may stop samples too early and reduce quality.
        /// Remember: amplitude ranges between 0 and 1.
        /// @version 2.9.1 // was 0.03
        /// </summary>
        [Range(0.000001f, 0.5f)]
        [Tooltip("Sample is stopped when amplitude is below this value")]
        public float MPTK_CutOffVolume = 0.0001f; // replace amplitude_that_reaches_noise_floor 
        /// <summary>@brief
        /// Experimental feature: modify the Fluidsynth constant FLUID_PEAK_ATTENUATION to change how velocity affects the voice attenuation.
        /// The default value is 960 (96 Db)
        /// Other values may produce unwanted results, use with care!
        /// </summary>
        [Tooltip("Velocity attenuation")]
        public float MPTK_VelocityAttenuation
        {
            get
            {
                return fluid_conv.FLUID_PEAK_ATTENUATION;
            }
            set
            {
                if (value >= 100f && value <= 2440f)
                {
                    fluid_conv.FLUID_PEAK_ATTENUATION = value;
                    if (state == fluid_synth_status.FLUID_SYNTH_PLAYING)
                        MPTK_InitModulators();
                }
                else
                    Debug.LogWarning("MPTK_VelocityAttenuation must be between 10 and 2440, default is 960");
            }
        }

        
        /// <summary>@brief
        /// Gradually ramps the synth volume at startup to avoid artifacts.
        /// Controls how quickly the audio source reaches full volume.
        /// Set to 1 for an immediate full level at start.
        /// </summary>
        /// <summary>@brief
        /// Apply real-time modulator effects defined in the SoundFont: pitch bend, control change, envelope modulation.
        /// </summary>
        [HideInInspector] // defined in custom inspector
        public bool MPTK_ApplyRealTimeModulator;

        /// <summary>@brief
        /// Apply LFO effect defined in the SoundFont
        /// </summary>
        [HideInInspector] // defined in custom inspector
        public bool MPTK_ApplyModLfo;

        /// <summary>@brief
        /// Apply vibrato effect defined in the SoundFont
        /// </summary>
        [HideInInspector] // defined in custom inspector
        public bool MPTK_ApplyVibLfo;

        /// <summary>@brief
        /// If true (default) then MIDI events are sent automatically to the MIDI player.
        /// Sets to false if you want to process events without playing sound.
        /// OnEventNotesMidi Unity Event can be used to process each note.
        /// </summary>
        [HideInInspector]
        public bool MPTK_DirectSendToPlayer;

        /// <summary>@brief
        /// Enable tempo changes from the MIDI file during playback.
        /// If disabled, only the first tempo change in the MIDI file is applied (or 120 BPM if none is found).
        /// Disable this when you want to control tempo from your script.
        /// </summary>
        [HideInInspector]
        public bool MPTK_EnableChangeTempo;

        /// <summary>@brief
        /// Apply pan changes from MIDI events in the SoundFont.
        /// Pan is disabled when spatialization is activated.
        /// </summary>
        [HideInInspector]
        public bool MPTK_EnablePanChange;

        /// <summary>@brief
        /// Sets the global volume between 0 and 1 for the current MPTK Synth.
        /// @note:
        /// - Unlock the maximum value to 10 (could be useful with Oboe) when the Unity script symbol MPTK_UNLOCK_VOLUME is defined (experimental).
        /// - Unity 2022/2023: https://docs.unity3d.com/2022.3/Documentation/Manual/CustomScriptingSymbols.html
        /// - Unity 6: https://docs.unity3d.com/6000.0/Documentation/Manual/custom-scripting-symbols.html
        /// </summary>
        [HideInInspector]
        public float MPTK_Volume
        {
            get { return volumeGlobal; }
            set
            {
                if (value >= 0f && value <= Constant.MAX_VOLUME)
                    volumeGlobal = value;
                else
                    Debug.LogWarning($"Set Volume value {value} not valid, must be between 0 and {Constant.MAX_VOLUME}");
            }
        }

        [SerializeField]
        [HideInInspector]
        private float volumeGlobal = 0.5f;

        // @cond NODOC
        [HideInInspector]
        protected float volumeStartStop = 1f;
        // @endcond

        /// <summary>@brief
        /// Optimize playback for low-power devices (for example, budget smartphones). Applies only in AudioSource mode (MPTK_CorePlayer = false).
        /// Enabling this may cause imperfect MIDI interpretation and lower sound quality.
        /// </summary>
        [Tooltip("Apply only with AudioSource mode (no Core Audio)")]
        public bool MPTK_WeakDevice;

        /// <summary>@brief
        /// [Only when CorePlayer = false] Defines a minimum release time at note-off in 100-nanosecond units.
        /// The 50 ms default is a good tradeoff; shorter values can produce unpleasant artifacts. No effect when MPTK_CorePlayer is true.
        /// </summary>
        [Range(0, 5000000)]
        [Tooltip("Apply only with AudioSource mode (no Core Audio)")]
        public uint MPTK_ReleaseTimeMin = 500000;

        // @cond NODOC
        [Range(0, 100)]
        [Tooltip("Smooth Volume Change (legacy mode only)")]
        public int DampVolume = 0; // default value=5
                                   // @endcond

        /// <summary>@brief
        /// Transposes notes from -24 to +24 semitones.
        /// </summary>
        [HideInInspector]
        public int MPTK_Transpose
        {
            get { return transpose; }
            set { if (value >= -24 && value <= 24) transpose = value; else Debug.LogWarning("Set Transpose value not valid : " + value); }
        }

        /// <summary>@brief
        /// Transposition applies to all channels except this one. Set to -1 to apply to every channel. V2.89.0
        /// Default is 9 because drums are generally on channel 9.
        /// </summary>
        [HideInInspector]
        public int MPTK_TransExcludedChannel
        {
            get { return transExcludedChannel; }
            set { if (value < 16 && value >= -1) transExcludedChannel = value; else Debug.LogWarning("Set Transpose Excluded Channel value not valid : " + value); }
        }

        [HideInInspector]
        public bool MPTK_Dsp64
        {
            get { return dsp_64; }
            set
            {
                if (value != dsp_64)
                {
                    dsp_64 = value;
                    if (FreeVoices != null)
                        FreeVoices.Clear();
                    if (dsp_64)
                        fluid_dsp_float_64.fluid_dsp_float_config();
                    else
                        fluid_dsp_float.fluid_dsp_float_config();
                }
            }
        }

        // @cond NODOC
        [HideInInspector]
        public bool dsp_64;
        // @endcond

        /// @}

        
        /// @name Synthesizer Performance
        /// @{

        /// <summary>@brief
        /// The MIDI thread priority controls how often the thread is scheduled by the CPU.
        /// The default value is 0 (normal); you can increase it to 1 or 2 (higher) on weaker hardware for more stable MIDI reading.
        /// @note A higher priority thread might consume all CPU time. It's recommended to set MPTK_ThreadMidiWait higher than 5 ms.
        /// </summary>
        public int MPTK_ThreadMidiPriority
        {
            get
            {
                return priorityThreadMidi;
            }
            set
            {
                priorityThreadMidi = value;
                // Try setting value
                if (midiThread != null)
                    midiThread.Priority = priorityThreadMidi + System.Threading.ThreadPriority.Normal;
                //Debug.Log($"Set PriorityThreadMidi: {priorityThreadMidi}");
            }
        }
        /// <summary>@brief
        /// Delay (in milliseconds) that the MIDI sequencing thread waits between processing MIDI event batches.
        /// </summary>
        [HideInInspector] // defined in custom inspector
        [Range(1, 30)]
        public int MPTK_ThreadMidiWait = 10;
        /// <summary>@brief
        /// Enable voice buffering to improve runtime performance.
        /// </summary>
        /// <remarks>
        /// When enabled:
        /// <list type="bullet">
        /// <item><description>Voices whose state becomes <c>OFF</c> are moved from the Active list to the Free list.</description></item>
        /// <item><description>The Free list can be cleaned automatically (see <see cref="MPTK_AutoCleanVoiceLimit"/> and <see cref="MPTK_AutoCleanVoiceTime"/>).</description></item>
        /// </list>
        /// When disabled, voices whose state becomes <c>OFF</c> are removed directly from the Active list.
        /// </remarks>
        [Header("---------------- Buffering ----------------")]
        [Tooltip("Enable bufferring Voice to enhance performance.")]
        public bool MPTK_AutoBuffer = true;

        /// <summary>@brief
        /// Removes free voices older than MPTK_AutoCleanVoiceTime once their count exceeds MPTK_AutoCleanVoiceLimit.
        /// </summary>
        [Tooltip("Auto Clean Voice Greater Than")]
        [Range(0, 1000)]
        public int MPTK_AutoCleanVoiceLimit;

        [Tooltip("Auto Clean Voice Older Than (millisecond)")]
        [Range(1000, 100000)]
        public float MPTK_AutoCleanVoiceTime;

        /// @}



        /// @name Audio Effects
        /// @brief Integration of SoundFont and Unity audio effects applied to the synthesizer.
        /// @details
        /// 
        /// - A SoundFont contains parameters to apply three kinds of effects: low-pass filter, reverb, chorus. These parameters can be specifics for each instruments and even each voices.
        /// - Unlike SoundFont effects which operate at the instrument level, Unity audio effects are applied to the entire output of the MIDI synthesizer.
        ///
        /// Maestro integrates selected Unity effects such as Reverb and Chorus.
        /// These effects can be configured from the inspector or controlled
        /// directly from scripts at runtime.


        /// @{


        /// <summary>@brief
        /// A SoundFont defines parameters for three kinds of effects: low-pass filter, reverb, and chorus.
        /// These parameters can be specific to each instrument and even each voice.
        /// Maestro MPTK effects are based on FluidSynth algorithmic effect modules.
        /// Maestro Pro can increase or decrease the impact of these effects (from the inspector or by script).
        /// To summarize:
        /// - Effects are applied per voice but are statically defined in the SoundFont.
        /// - Maestro parameters can raise or lower the default values set in the SoundFont.
        /// - Adjustments apply to the whole prefab; the audible result depends on the initial SoundFont preset.
        /// - These effects require additional CPU resources.
        /// See more detailed information at https://paxstellar.fr/sound-effects/
        /// @note
        /// - By default, only the low-pass filter effect is enabled in Maestro.
        /// - Enable other effects from the prefab inspector (Synth Parameters / SoundFont Effect) or by script.
        /// - The low-pass filter often improves sound quality and is enabled by default.
        /// @code
        /// // Find a MPTK Prefab, will work also for MidiStreamPlayer, MidiExternalPlayer ... all classes which inherit from MidiSynth.
        /// MidiFilePlayer fp = FindFirstObjectByType<MidiFilePlayer>();
        /// fp.MPTK_EffectSoundFont.EnableFilter = true;
        /// fp.MPTK_EffectSoundFont.FilterFreqOffset = 500;
        /// @endcode
        /// </summary>
        [SerializeField]
        [HideInInspector]
        public MPTKEffectSoundFont MPTK_EffectSoundFont;

        /// @}




        // @cond NODOC

        [Header("---------------- DSP Statistics ----------------")]
        public float StatDspLoadPCT;
        public float StatDspLoadMIN;
        public float StatDspLoadMAX;
        public float StatDspLoadAVG;
        public int StatDspChannelCount;
        public int GcCollectionCout;
        public long AllocatedBytesForCurrentThread;

#if DEBUG_PERF_AUDIO

        //public float StatUILatencyLAST;
        public MovingAverage StatSynthLatency;
        public float StatSynthLatencyLAST;
        public float StatSynthLatencyAVG;
        public float StatSynthLatencyMIN;
        public float StatSynthLatencyMAX;
#endif

        //public float StatDspLoadLongAVG;
        public MovingAverage StatDspLoadMA;

        [Header("---------------- MIDI Sequencer Statistics ----------------")]

        /// <summary>@brief
        /// Delta time in milliseconds between calls of the MIDI sequencer
        /// </summary>
        public double StatDeltaThreadMidiMS = 0d;
        public double StatDeltaThreadMidiMAX;
        public double StatDeltaThreadMidiMIN;
        public float StatDeltaThreadMidiAVG;
        public MovingAverage StatDeltaThreadMidiMA;

        /// <summary>@brief
        /// Time to read MIDI Events
        /// </summary>
        public float StatReadMidiMS;
        private double lasttimeMidiFromStartPlay;
        public double StatDeltaTimeMidi = 0;

        /// <summary>@brief
        /// Time to enqueue MIDI events to the Unity thread
        /// </summary>
        public float StatEnqueueMidiMS;

        /// <summary>@brief
        /// Time to process MIDI event (create voice)
        /// </summary>
        public float StatProcessMidiMS;
        public float StatProcessMidiMAX;

        [Header("---------------- MIDI Synth Statistics ----------------")]

        /// <summary>@brief
        /// Delta time in milliseconds between call to the MIDI Synth (OnAudioFilterRead).
        /// This value is constant during playing. Directly related to the buffer size and the synth rate values.
        /// </summary>
        public double DeltaTimeAudioCall;

        /// <summary>@brief
        /// Time in milliseconds for the whole MIDI Synth processing (OnAudioFilterRead)
        /// </summary>
        public double StatAudioFilterReadMS;
        public double StatAudioFilterReadMAX;
        public double StatAudioFilterReadMIN;
        public float StatAudioFilterReadAVG;
        public MovingAverage StatAudioFilterReadMA;

        /// <summary>@brief
        /// Time to process samples in active list of voices: apply effect, WriteAllSamples, copy data to audio buffer.
        /// </summary>
        public float StatSampleWriteMS;
        public float StatSampleWriteAVG;
        public MovingAverage StatSampleWriteMA;
        /// <summary>@brief
        /// Time to process active and free voices
        /// </summary>
        public float StatProcessListMS;
        public float StatProcessListAVG;
        public MovingAverage StatProcessListMA;

#if DEBUG_PERF_AUDIO
        private System.Diagnostics.Stopwatch watchPerfAudio = new System.Diagnostics.Stopwatch(); // High resolution time
#endif

#if DEBUG_STATUS_STAT 
        [Header("Voice Status Count Clean / On / Sustain / Off / Release")]
 /// <summary>@brief
 /// Voice status and volume envelope count.
 /// </summary>
        public int[] StatusStat;
        public int[] EnveloppeStat;
#endif
        public SynthInfo synthInfo = new SynthInfo();


        /// <summary>@brief
        /// Time in milliseconds for the last OnAudioFilter
        /// </summary>
        protected double lastTimePlayCore = 0d;

        private System.Diagnostics.Stopwatch watchSynth = new System.Diagnostics.Stopwatch();

        protected System.Diagnostics.Stopwatch watchMidi = new System.Diagnostics.Stopwatch();
        protected System.Diagnostics.Stopwatch pauseMidi = new System.Diagnostics.Stopwatch();
        // removed 2.9.0 and replaced with timeMidiFromStartPlay
        // protected long EllapseMidi;
        /*private*/
        public Thread midiThread;

        // Set to true to force clearing list of free voices
        private bool needClearingFreeVoices;

        private StringBuilder sLogSampleUse;

        [SerializeField]
        [HideInInspector]
        // When the option is on, NoteOff and Duration for non-looped samples are ignored and the samples play through to the end.
        public bool keepPlayingNonLooped /* V2.89.0 */;

#if LOG_PERF_NOTEON
        private float perf_time_cumul;
        private List<string> perfs;
        private System.Diagnostics.Stopwatch watchPerfNoteOn = new System.Diagnostics.Stopwatch(); // High resolution time
#endif

        [HideInInspector]
        public bool mPTK_CorePlayer;

        // @endcond

        /// @name Audio Settings
        /// @{

        /// <summary>@brief
        /// If true, the synth rate and buffer size are set automatically by Unity according to the hardware capability. - V2.89.0 -
        /// See Unity menu "Edit / Project Settings..." to choose between best latency and best performance.
        /// If false, you can set rate and buffer size manually, at the risk of degraded audio quality (experimental).
        /// </summary>
        [HideInInspector]

        /// <summary>@brief
        /// If true, MIDI events are read and played from a dedicated thread.
        /// If false, MidiSynth uses AudioSource GameObjects to play sound.
        /// This property must be set from the inspector before running the application.
        /// Default is true.
        /// Warning: the non-core mode player (MPTK_CorePlayer = false) will be removed in the next major version (V3).
        /// </summary>
        public bool MPTK_CorePlayer
        {
            get =>
#if UNITY_WEBGL
                false;
#else
            mPTK_CorePlayer;
#endif
            set => mPTK_CorePlayer = value;
        }


        /// <summary>@brief
        /// Use the audio settings provided by Unity instead of custom values.
        /// </summary>
        public bool MPTK_AudioSettingFromUnity;

        /// <summary>@brief
        /// Gets or sets the current synth rate (only if MPTK_EnableFreeSynthRate is true).
        /// </summary>
        public int MPTK_SynthRate
        {
            get { return (int)OutputRate; }
            set
            {
                int valueClamped = Mathf.Clamp(value, 12000, 96000);
                if (OutputRate != value)
                {
                    OutputRate = valueClamped;
                    Debug.Log($"New OutputRate: {OutputRate} ac.sampleRate:{OutputRate}");

#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
                    InitOboe();
#else
                    // Get current configuration
                    AudioConfiguration ac = AudioSettings.GetConfiguration();
                    if (ac.sampleRate != valueClamped)
                    {
                        if (VerboseSynth) Debug.Log($"Change Sample Rate from {ac.sampleRate} to {valueClamped}");
                        ac.sampleRate = valueClamped;
                        ResetAudio(ac);
                    }
#endif
                    if (CoreAudioSource != null)
                    {
                        //Debug.Log($"Create instance for effect.");

                        if (MPTK_EffectSoundFont == null)
                        {
                            // Instance is deserialized when MidiSynth is loaded, but not at the first load
                            //Debug.Log($"Create instance for MPTK_EffectSoundFont. <b>Set default setting in {this.name} Inspector / Synth Parameters / SoundFont Effect Parameters</b>");
                            MPTK_EffectSoundFont = ScriptableObject.CreateInstance<MPTKEffectSoundFont>();
#if MPTK_PRO
                            MPTK_EffectSoundFont.DefaultAll();
#endif
                        }
                        MPTK_EffectSoundFont.Init(this);
#if MPTK_PRO
                        InitEffectUnity();
#endif
                    }
                    if (ActiveVoices != null)
                        for (int i = 0; i < ActiveVoices.Count; i++)
                            ActiveVoices[i].output_rate = OutputRate; // was ac.sampleRate
                    needClearingFreeVoices = true;
                }
            }
        }

        /// <summary>@brief
        /// Apply new audio setting: rate or buffer size.
        /// It is recommended to use the audio setting from Unity.
        /// </summary>
        /// <param name="ac"></param>
        private static void ResetAudio(AudioConfiguration ac)
        {
            AudioSettings.Reset(ac);
            // V2.89.0 All audioSource are stopped after a reset, need to restart MPTK audiosource. What about others AudioSource ?
            foreach (AudioSource audioSource in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                //Debug.Log($"before {audioSource.name} {audioSource.isPlaying} {audioSource.mute}");
                // Need to restart audiosource after an audio reset. Unluckily Unity generate a warning message...
                // V2.12 - now restart all audio source ...
                //if (audioSource.name.Contains("VoiceAudio"))
                // Avoid restartind audiosource out of the Maestro component (your app!)
                audioSource.Play();
            }
        }

        /// <summary>@brief
        /// Allow direct setting of the Synth Rate
        /// </summary>
        [HideInInspector]
        public bool MPTK_EnableFreeSynthRate = false;

        /// <summary>@brief
        /// Gets or sets the synth output sample rate. -1: default, 0: 24000, 1: 36000, 2: 48000, 3: 60000, 4: 72000, 5: 84000, 6: 96000.
        /// It's better to stop playback before changing on the fly to avoid noise.
        /// </summary>
        public int MPTK_IndexSynthRate
        {
            get { return indexSynthRate; }
            set
            {
                indexSynthRate = value;
                if (VerboseSynth) Debug.Log("MPTK_ChangeSynthRate " + indexSynthRate);
                if (indexSynthRate < 0)
                {
#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
                    // Set a default value
                    MPTK_SynthRate = 0;
#else
                    // No change
                    OnAudioConfigurationChanged(false);
#endif
                }
                else
                {
                    if (indexSynthRate > 6) indexSynthRate = 6;
                    MPTK_SynthRate = 24000 + (indexSynthRate * 12000);
                }
            }
        }

        [SerializeField]
        [HideInInspector]
        private int indexSynthRate = -1;

        private int[] tabDspBufferSize = new int[] { 64, 128, 256, 512, 1024, 2048 };

        /// <summary>@brief
        /// Gets or sets synth buffer size: -1: default, 0: 64, 1: 128, 2: 256, 3: 512, 4: 1024, 5: 2048.
        /// The change is global for all prefabs. Stop playback on all prefabs before changing on the fly to avoid noise or crashes.
        /// </summary>
        public int MPTK_IndexSynthBuffSize
        {
            get { return indexBuffSize; }
            set
            {
                indexBuffSize = value;
                if (VerboseSynth) Debug.Log("MPTK_IndexSynthBuffSize " + indexBuffSize);
                if (indexBuffSize < 0 || indexBuffSize >= tabDspBufferSize.Length)
                {
#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
                    MPTK_IndexSynthBuffSize = 1; //128
#else
                    // No change
                    OnAudioConfigurationChanged(false);
#endif
                }
                else
                {
#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
                    if (indexBuffSize > 5) indexBuffSize = 5;
                    DspBufferSize = tabDspBufferSize[indexBuffSize];
                    InitOboe();
#else
                    DspBufferSize = tabDspBufferSize[indexBuffSize];
                    AudioConfiguration ac = AudioSettings.GetConfiguration();
                    if (VerboseSynth) Debug.Log($"Change Buffer Size from {ac.dspBufferSize} to {DspBufferSize}");
                    ac.dspBufferSize = DspBufferSize;
                    ResetAudio(ac);
#endif
                }
            }
        }

        /// @}

        [SerializeField]
        [HideInInspector]
        private int indexBuffSize = -1;


        /// @name MIDI Spatial Mapper
        /// @brief Spatial routing system used by the MidiSpatializer prefab.
        /// @details
        /// The MIDI Spatial Mapper distributes incoming MIDI events from the main
        /// MIDI reader to multiple MidiSynth instances in the scene. This allows
        /// instruments or tracks to be rendered from different spatial positions.
        ///
        /// Two routing modes are available:
        /// - Channel mode: one spatial synth per MIDI channel.
        /// - Track mode: one spatial synth per MIDI track.
        ///
        /// This system is typically used with the MidiSpatializer prefab to create
        /// immersive 3D audio scenes.
        /// @version Maestro Pro

        /// @{

        /// <summary>@brief
        /// Contains each Midi Synth for each channel or track when the prefab MidiSpatializer is used and IsMidiChannelSpace=true.
        /// Warning: only one MidiSpatializer can be used in a hierarchy.
        /// </summary>
        static public List<MidiFilePlayer> SpatialSynths;

        private int spatialSynthIndex = -1;

        /// <summary>@brief
        /// Index of the MidiSynth for the dedicated Channel or Track when the prefab MidiSpatializer is used.
        /// If MPTK_ModeSpatializer = Channel then represent the playing channel.
        /// If MPTK_ModeSpatializer = Track then represent the playing track.
        /// The value is -1 for the Midi reader because no voice is played.
        /// </summary>
        public int MPTK_SpatialSynthIndex
        {
            get
            {
                return spatialSynthIndex;
            }
        }
        /// @}

        /// @name Log & Debug
        /// @{

        /// <summary>@brief
        /// Log MIDI events (v2.9.0 moved from MidiFilePlayer)
        /// </summary>
        [HideInInspector]
        public bool MPTK_LogEvents;

        /// <summary>@brief
        /// Log for each sample to be played
        /// </summary>
        [HideInInspector]
        public bool MPTK_LogWave;

        /// <summary>@brief
        /// Log information about active voices
        /// </summary>
        public void MPTK_DebugActiveVoice()
        {
            if (ActiveVoices != null && ActiveVoices.Count > 0)
            {
                Debug.Log("Active voice found");
                Debug.Log(fluid_voice.HeaderVoiceInfo());
                for (int i = 0; i < ActiveVoices.Count; i++)
                    Debug.Log(ActiveVoices[i].ToString());
            }
            else
                Debug.Log("No active voice found");
        }

        /// <summary>@brief
        /// Log information about free voices
        /// </summary>
        public void MPTK_DebugFreeVoice()
        {
            if (FreeVoices != null && FreeVoices.Count > 0)
            {
                Debug.Log("Free voice found");
                Debug.Log(fluid_voice.HeaderVoiceInfo());
                for (int i = 0; i < FreeVoices.Count; i++)
                    Debug.Log(FreeVoices[i].ToString());
            }
            else
                Debug.Log("No free voice found");
        }

        /// @}

        [Header("---------------- Voice Statistics ----------------")]

        /// @name Voice Statistics
        /// @{

        /// <summary>@brief
        /// Count of active voices (playing, excluding voices in release).
        /// Sound may still be audible from voices in release even when this count is 0. Read-only.
        /// </summary>
        public int MPTK_StatVoiceCountPlaying;

        /// <summary>@brief
        /// Gets the number of active voices currently in the sustained state.
        /// </summary>
        /// <remarks>A sustained voice typically indicates that a note is being held, such as when a
        /// sustain pedal is engaged or a note-on event has not yet been released. This property can be used to monitor
        /// sustained notes for performance analysis or MIDI event handling.</remarks>
        public int MPTK_StatVoiceCountSustained
        {
            get
            {
                int count = 0;
                if (ActiveVoices != null)
                {
                    foreach (fluid_voice voice in ActiveVoices)
                        if (voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED)
                            count++;
                }
                return count;
            }
        }

        /// <summary>@brief
        /// Count of the active voices (playing and releasing) - Readonly
        /// </summary>
        public int MPTK_StatVoiceCountActive;

        /// <summary>@brief
        /// Count of the voices reused - Readonly
        /// </summary>
        public int MPTK_StatVoiceCountReused;

        /// <summary>@brief
        /// Count of free voices available for reuse.
        /// Voices older than AutoCleanVoiceTime are removed only when the count exceeds AutoCleanVoiceLimit. Read-only.
        /// </summary>
        public int MPTK_StatVoiceCountFree;

        /// <summary>@brief
        /// Percentage of voices reused during the synth lifetime. 0: no reuse, 100: all voices reused (unattainable in practice).
        /// </summary>
        public int MPTK_StatVoiceRatioReused;

        /// <summary>@brief
        /// Count of voice played since the start of the synth
        /// </summary>
        public int MPTK_StatVoicePlayed;

        /// <summary>@brief
        /// Reset voices statistics
        /// </summary>
        public void MPTK_ResetStat()
        {
            if (VerboseSynth) Debug.Log("MPTK_ResetStat");
            MPTK_StatVoicePlayed = 0;
            MPTK_StatVoiceCountReused = 0;
            MPTK_StatVoiceRatioReused = 0;
            //        lastTimePlayCore = 0d;
            StatDspLoadPCT = 0f;
            StatDspLoadMIN = float.MaxValue;
            StatDspLoadMAX = 0f;

            if (Channels != null)
                foreach (MPTKChannel mptkChannel in Channels)
                    mptkChannel.NoteCount = 0;
#if DEBUG_HISTO_SYNTH
            synthInfo.ClearHistoMinMax();
#endif

#if DEBUG_PERF_AUDIO
            StatSynthLatency = new MovingAverage();
            StatSynthLatencyAVG = 0f;
            StatSynthLatencyMIN = float.MaxValue;
            StatSynthLatencyMAX = 0f;

            StatDspLoadMA = new MovingAverage();
            StatAudioFilterReadMIN = double.MaxValue;
            StatAudioFilterReadMAX = 0;
            StatAudioFilterReadMA = new MovingAverage();
            StatSampleWriteMA = new MovingAverage();
            StatProcessListMA = new MovingAverage();
#endif
#if DEBUG_PERF_MIDI
            StatDeltaThreadMidiMIN = double.MaxValue;
            StatDeltaThreadMidiMAX = 0;
            StatDeltaThreadMidiMA = new MovingAverage();
            StatProcessMidiMAX = 0f; ;
            lasttimeMidiFromStartPlay = 0;
            watchPerfMidi = new System.Diagnostics.Stopwatch();
#endif
        }

        /// @}

        // @cond NODOC

        public MidiLoad midiLoaded;
        protected bool sequencerPause = false;
        protected double SynthElapsedMilli;
        protected float timeToPauseMilliSeconde = -1f;

        [SerializeField]
        [HideInInspector]
        protected bool playPause = false;
        /// <summary>@brief
        /// Distance to the listener.
        /// Calculated only if MPTK_PauseOnDistance = true
        /// </summary>
        [HideInInspector]
        public float distanceToListener;

        [SerializeField]
        [HideInInspector]
        public int transpose = 0, transExcludedChannel = 9;

        // removed 2.10.1 public mptk_channel[] Channels and properties included in;
        //      public MptkChannel[] Channels;
        public MPTKChannels Channels;
        public List<fluid_voice> ActiveVoices;

        public List<fluid_voice> FreeVoices;
        protected Queue<SynthCommand> QueueSynthCommand;
        protected Queue<List<MPTKEvent>> QueueMidiEvents;

        public class SynthCommand
        {
            public enum enCmd { StartEvent, StopEvent, ClearAllVoices, NoteOffAll }
            public enCmd Command;
            public int IdSession;
            public MPTKEvent MidiEvent;
        }

        [HideInInspector]
        public fluid_interp InterpolationMethod = fluid_interp.Linear;

        [HideInInspector]
        public float gain = 1f;

        [Header("---------------- Enable Debug Log ----------------")]

        // Set from the inspector 
        public bool VerboseSynth;
        public bool VerboseSoundfont;
        public bool VerboseSample;
        public bool VerboseOverload;
        public bool VerboseVoice;
        public bool VerboseChannel;
        public bool VerboseGenerator;
        public bool VerboseCalcGen;
        public bool VerboseCalcMod;
        public bool VerboseCalcVolADSR;
        public bool VerboseCalcModADSR;
        public bool VerboseController;
        public bool VerboseEnvVolume;
        public bool VerboseSpecialNoteOff;
        public bool VerboseEnvModulation;
        public bool VerboseFilter;
        public bool VerboseVolume;
        public bool VerboseSpatialSynth;
        public bool VerboseChord;


        public fluid_synth_status MPTK_SynthState { get => state; set => state = value; }

        private fluid_synth_status state = fluid_synth_status.FLUID_SYNTH_CLEAN;

        [Header("---------------- Apply only with AudioSource mode (Core Audio unchecked) ----------------")]
        public VoiceAudioSource AudiosourceTemplate;

        [Tooltip("Apply only with AudioSource mode (no Core Audio)")]
        public bool AdsrSimplified;

        // @endcond

        /// @name Synthesizer Events
        /// @brief Events raised by the MIDI synthesizer during audio processing.
        /// @details
        /// These callbacks allow user scripts to interact with the MIDI synthesizer
        /// during playback. Events may be triggered from the audio processing thread,
        /// so Unity API calls are generally not allowed except for Debug.Log.
        ///
        /// Typical uses include:
        /// - reacting to audio frame timing
        /// - intercepting MIDI events before playback
        /// - tracking musical structure such as beats or measures
        ///
        /// Developers should keep event handlers lightweight to avoid affecting
        /// audio timing accuracy.
        
        /// @{

        //[Header("Events associated to the synth")]
        [HideInInspector]
        /// <summary>@brief
        /// Unity event fired during the synthesizer's Awake. The GameObject component name is passed as a parameter.
        /// Setting this callback via script (AddListener) is not recommended; prefer setting it from the inspector.
        /// @image html SetOnEventSynth.png
        /// Example script (still recommended to set the callback from the inspector).
        /// @code
        /// ...
        /// midiStreamPlayer.OnEventSynthAwake.AddListener(StartLoadingSynth);
        /// ...
        /// public void StartLoadingSynth(string name)
        /// {
        /// Debug.LogFormat("Synth {0} loading", name);
        /// }
        /// @endcode
        /// </summary>
        public EventSynthClass OnEventSynthAwake;

        [HideInInspector]
        /// <summary>@brief
        /// Unity event fired when the synthesizer starts. The GameObject component name is passed as a parameter.
        /// Setting this callback via script (AddListener) is not recommended; prefer setting it from the inspector.
        /// @image html SetOnEventSynth.png
        /// Example script (callback still recommended from the inspector).
        /// @snippet TestMidiStream.cs ExampleOnEventEndLoadingSynth
        /// </summary>
        public EventSynthClass OnEventSynthStarted;

        /// @}

        private float[] left_buf;
        private float[] right_buf;

        //int cur;                           /** the current sample in the audio buffers to be output */
        //int dither_index;		/* current index in random dither value buffer: fluid_synth_(write_s16|dither_s16) */


        //fluid_tuning_t[][] tuning;           /** 128 banks of 128 programs for the tunings */
        //fluid_tuning_t cur_tuning;         /** current tuning in the iteration */

        // The midi router. Could be done nicer.
        //Indicates, whether the audio thread is currently running.Note: This simple scheme does -not- provide 100 % protection against thread problems, for example from MIDI thread and shell thread
        //fluid_mutex_t busy;
        //fluid_midi_router_t* midi_router;


        //default modulators SF2.01 page 52 ff:
        //There is a set of predefined default modulators. They have to be explicitly overridden by the sound font in order to turn them off.

        private static HiMod default_vel2att_mod = new HiMod();        /* SF2.01 section 8.4.1  */
        public static HiMod default_vel2filter_mod = new HiMod();     /* SF2.01 section 8.4.2  */
        private static HiMod default_at2viblfo_mod = new HiMod();      /* SF2.01 section 8.4.3  */
        private static HiMod default_mod2viblfo_mod = new HiMod();     /* SF2.01 section 8.4.4  */
        private static HiMod default_att_mod = new HiMod();            /* SF2.01 section 8.4.5  */
        private static HiMod default_pan_mod = new HiMod();            /* SF2.01 section 8.4.6  */
        private static HiMod default_expr_mod = new HiMod();           /* SF2.01 section 8.4.7  */
        private static HiMod default_reverb_mod = new HiMod();         /* SF2.01 section 8.4.8  */
        private static HiMod default_chorus_mod = new HiMod();         /* SF2.01 section 8.4.9  */
        private static HiMod default_pitch_bend_mod = new HiMod();     /* SF2.01 section 8.4.10 */

        // @cond NODOC

        [HideInInspector]
        public bool showMidiInfo;
        [HideInInspector]
        public bool showSynthParameter;
        [HideInInspector]
        public bool showSpatialization;
        [HideInInspector]
        public bool showUnitySynthParameter;
        [HideInInspector]
        public bool showUnityPerformanceParameter;
        [HideInInspector]
        public bool showExperimentalFeature;
        [HideInInspector]
        public bool showMidiPerformanceParameter;
        [HideInInspector]
        public bool showSoundFontEffect;
        [HideInInspector]
        public bool showVerbose;
        [HideInInspector]
        public bool showUnitySynthEffect;
        [HideInInspector]
        public bool showMidiParameter;
        [HideInInspector]
        public bool showMidiPlaying;
        [HideInInspector]
        public bool showSynthEvents;
        [HideInInspector]
        public bool showEvents;
        [HideInInspector]
        public bool showDefault;

        // @endcond

        /* reverb presets */
        //        static fluid_revmodel_presets_t revmodel_preset[] = {
        //	/* name */    /* roomsize */ /* damp */ /* width */ /* level */
        //	{ "Test 1",          0.2f,      0.0f,       0.5f,       0.9f },
        //    { "Test 2",          0.4f,      0.2f,       0.5f,       0.8f },
        //    { "Test 3",          0.6f,      0.4f,       0.5f,       0.7f },
        //    { "Test 4",          0.8f,      0.7f,       0.5f,       0.6f },
        //    { "Test 5",          0.8f,      1.0f,       0.5f,       0.5f },
        //    { NULL, 0.0f, 0.0f, 0.0f, 0.0f }
        //};

        // From fluid_sys.c - fluid_utime() returns the time in micro seconds. this time should only be used to measure duration(relative times). 
        //double fluid_utime()
        //{
        //    //fprintf(stderr, "fluid_cpu_frequency:%f fluid_utime:%f\n", fluid_cpu_frequency, rdtsc() / fluid_cpu_frequency);

        //    //return (rdtsc() / fluid_cpu_frequency);
        //    return AudioSettings.dspTime;
        //}

        // returns the current time in milliseconds. This time should only be used in relative time measurements.
        //int fluid_curtime()
        //{
        //    // replace GetTickCount() :Retrieves the number of milliseconds that have elapsed since the system was started, up to 49.7 days.
        //    return System.Environment.TickCount;
        //}

        public void Awake()
        {
            // for test
            //Time.timeScale = 0;
#if DEBUG_STATUS_STAT
            StatusStat = new int[(int)fluid_voice_status.FLUID_VOICE_OFF + 1];
            EnveloppeStat = new int[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED + 1];
#endif

            IdSynth = lastIdSynth++;
            sLogSampleUse = new StringBuilder(255);

            if (VerboseSynth) Debug.Log($"Awake MidiSynth IdSynth:{IdSynth}");
            MPTK_SoundFont = new MPTKSoundFont(this);

            try
            {
                if (OnEventSynthAwake != null)
                    OnEventSynthAwake.Invoke(this.name);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("OnEventSynthAwake: exception detected. Check the callback code");
                Debug.LogException(ex);
            }

            MidiPlayerGlobal.InitPath();

            // V2.83 Move these init from Start to Awake
            if (!MPTK_CorePlayer && AudiosourceTemplate == null)
            {
                if (VerboseSynth) Debug.LogWarningFormat("AudiosourceTemplate not defined in the {0} inspector, search one", this.name);
                AudiosourceTemplate = FindFirstObjectByType<VoiceAudioSource>();
                //if (AudiosourceTemplate == null)
                //{
                //    Debug.LogErrorFormat("No VoiceAudioSource template found for the audiosource synth {0}", this.name);
                //}
            }

            // V2.83 Move these init from Start to Awake
            if (MPTK_CorePlayer)
            {
                if (CoreAudioSource == null)
                {
                    CoreAudioSource = GetComponent<AudioSource>();
                    if (CoreAudioSource == null)
                    {
                        Debug.LogErrorFormat("No AudioSource defined in the MPTK prefab '{0}'", this.name);
                    }
                }
            }
            else
            {
                // No need of the AudioSource 
                CoreAudioSource = GetComponent<AudioSource>();
                if (CoreAudioSource != null)
                {
                    CoreAudioSource.enabled = false;
                }
            }
        }

        public void Start()
        {
            if (OnEventSynthAwake == null) OnEventSynthAwake = new EventSynthClass();
            if (OnEventSynthStarted == null) OnEventSynthStarted = new EventSynthClass();

            left_buf = new float[FLUID_MAX_BUFSIZE];
            right_buf = new float[FLUID_MAX_BUFSIZE];

            if (VerboseSynth) Debug.Log($"Start MidiSynth IdSynth:{IdSynth}");
            try
            {
#if MPTK_PRO
                BuildSpatialSynth();
#endif
                if (Application.isPlaying)
                    Routine.RunCoroutine(ThreadLeanStartAudio(CoreAudioSource).CancelWith(gameObject), Segment.FixedUpdate);
                else
                    Routine.RunCoroutine(ThreadLeanStartAudio(CoreAudioSource), Segment.EditorUpdate);

                AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
                if (!MPTK_AudioSettingFromUnity)
                {
                    if (VerboseSynth) Debug.Log($"Set specific rate and dsp size indexBuffSize:{indexBuffSize}");
                    // Set synth rate
                    if (MPTK_EnableFreeSynthRate)
                        // Set free rate
                        MPTK_SynthRate = (int)OutputRate;
                    else
                        // Set rate from a list
                        MPTK_IndexSynthRate = indexSynthRate;

                    // Set buffer size
                    MPTK_IndexSynthBuffSize = indexBuffSize;
                }
                if (dsp_64)
                    fluid_dsp_float_64.fluid_dsp_float_config();
                else
                    fluid_dsp_float.fluid_dsp_float_config();

                GetInfoAudio();

                /* as soon as the synth is created it starts playing. */
                // Too soon state = fluid_synth_status.FLUID_SYNTH_PLAYING;

                if (MPTK_CorePlayer)
                {
                    if (CoreAudioSource != null)
                    {
                        // Special init for core mode

                        //Debug.Log($"Create instance for effect.");
                        if (MPTK_EffectSoundFont == null)
                        {
                            // Instance is deserialized when MidiSynth is loaded, but not at the first load
                            //Debug.Log($"Create instance for MPTK_EffectSoundFont. <b>Set default setting in {this.name} Inspector / Synth Parameters / SoundFont Effect Parameters</b>");
                            MPTK_EffectSoundFont = ScriptableObject.CreateInstance<MPTKEffectSoundFont>();
#if MPTK_PRO
                            MPTK_EffectSoundFont.DefaultAll();
#endif
                        }
                        MPTK_EffectSoundFont.Init(this);
#if MPTK_PRO
                        InitEffectUnity();
#endif
                    }
                }
                else
                {
                    // Special init for legacymode

                }

                //cur = FLUID_BUFSIZE;
                //dither_index = 0;

                /* FIXME */
                //start = (uint)(DateTime.UtcNow.Ticks / fluid_voice.Nano100ToMilli); // milliseconds:  fluid_curtime();

#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
                InitOboe();
#endif

                try
                {
                    if (OnEventSynthStarted != null)
                        OnEventSynthStarted.Invoke(this.name);

                }
                catch (Exception ex)
                {
                    Debug.LogError("OnEventSynthStarted: exception detected. Check the callback code");
                    Debug.LogException(ex);
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }

            if (distanceAttenuation)
                // At start, apply change on the AudioSource only when distance attenuation is enabled.
                // Keep unchanged the AudioSource parameters defined in the inspector.
                SetDistanceAttenuation();
        }



        public IEnumerator<float> ThreadLeanStartAudio(AudioSource audioSource)
        {
            float increment = Mathf.Clamp(MPTK_LeanSynthStarting, 0.001f, 1f);
            if (increment >= 1f)
                audioSource.volume = 1f;
            else
            {
                audioSource.volume = 0f;
                while (audioSource.volume < 1f)
                {
                    audioSource.volume += increment;
                    //Debug.Log(audioSource.volume);
                    yield return 0;// Routine.WaitForSeconds(.01f);
                }
            }
            yield return 0;
        }

        /// @name Synthesizer Events
        /// @brief Events raised by the MIDI synthesizer during audio processing.
        /// @details
        /// These callbacks allow user scripts to interact with the MIDI synthesizer
        /// during playback. Events may be triggered from the audio processing thread,
        /// so Unity API calls are generally not allowed except for Debug.Log.
        ///
        /// Typical uses include:
        /// - reacting to audio frame timing
        /// - intercepting MIDI events before playback
        /// - tracking musical structure such as beats or measures
        ///
        /// Developers should keep event handlers lightweight to avoid affecting
        /// audio timing accuracy.
        
        /// @{

        /// <summary>@brief
        /// Gets current audio configuration
        /// </summary>
        /// <param name="deviceWasChanged"></param>
        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
            // see InitOboe method
#else
            AudioConfiguration GetConfiguration = AudioSettings.GetConfiguration();
            OutputRate = GetConfiguration.sampleRate;
            DspBufferSize = GetConfiguration.dspBufferSize;
            if (VerboseSynth) // OnAudioConfigurationChanged
            {
                Debug.Log("OnAudioConfigurationChanged - " + (deviceWasChanged ? "Device was changed" : "Reset was called"));
                Debug.Log("   dspBufferSize:" + DspBufferSize);
                Debug.Log("   OutputRate:" + OutputRate);
            }
#endif
        }

        /// @}

        private void GetInfoAudio()
        {
#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
            // see InitOboe method
#else
            // Two methods
            AudioSettings.GetDSPBufferSize(out AudioBufferLenght, out AudioNumBuffers);
            AudioConfiguration ac = AudioSettings.GetConfiguration();
            OutputRate = ac.sampleRate;
            DspBufferSize = ac.dspBufferSize;
            if (VerboseSynth) LogInfoAudio();

            if (ac.dspBufferSize % FLUID_BUFSIZE != 0)
            {
                Debug.LogError($"DspBufferSize length is {ac.dspBufferSize}. It must be a multiple of {FLUID_BUFSIZE}.");
                Debug.LogError($"Try to change the 'DSP Buffer Size' in the Unity Editor menu 'Edit / Project Settings / Audio'");
            }

#endif
        }

        public void LogInfoAudio()
        {
#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
            Debug.Log($"------InfoAudio Oboe---{name}---");
            Debug.Log($"    MPTKRate:{MPTK_SynthRate} Oboe  SampleRate:{oboeAudioStream.SampleRate}");
            Debug.Log($"    DspBufferSize:{DspBufferSize}");
            Debug.Log($"    ChannelCount:{oboeAudioStream.ChannelCount}");
            Debug.Log($"    BufferCapacityInFrames:{oboeAudioStream.BufferCapacityInFrames}");
            Debug.Log($"    BufferSizeInFrames:{oboeAudioStream.BufferSizeInFrames}"); // https://developer.android.com/ndk/reference/group/audio#aaudiostream_setbuffersizeinframes
            Debug.Log($"    FramesPerCallback:{oboeAudioStream.FramesPerCallback}");
            Debug.Log($"    FramesPerBurst:{oboeAudioStream.FramesPerBurst}");
            Debug.Log($"    BytesPerFrame:{oboeAudioStream.BytesPerFrame}");
            Debug.Log($"    PerformanceMode:{oboeAudioStream.PerformanceMode}");
            Debug.Log($"    SampleRateConversionQuality:{oboeAudioStream.SampleRateConversionQuality}");
            Debug.Log($"    Audio Format:{oboeAudioStream.Format}");
            Debug.Log($"    SessionId:{oboeAudioStream.SessionId}");
            Debug.Log($"    Usage:{oboeAudioStream.Usage}");
            Debug.Log($"    AudioApi:{oboeAudioStream.AudioApi}");
            Debug.Log("---------------------");
#else
            AudioConfiguration ac = AudioSettings.GetConfiguration();
            Debug.Log($"------InfoAudio FMOD---{name}---");
            Debug.Log("  " + (MPTK_CorePlayer ? "Core Player Activated" : "AudioSource Player Activated"));
            Debug.Log("  bufferLenght:" + AudioBufferLenght + " 2nd method: " + ac.dspBufferSize);
            Debug.Log("  numBuffers:" + AudioNumBuffers);
            Debug.Log("  outputSampleRate:" + AudioSettings.outputSampleRate + " 2nd method: " + ac.sampleRate);
            Debug.Log("  speakerMode:" + AudioSettings.speakerMode);
            Debug.Log("---------------------");
#endif
        }

        private CoroutineHandle handleManageCacheVoice;

        /// <summary>@brief
        /// Waits until all notes are off.
        /// This may take a few seconds because samples must complete their release time.
        /// The method exits after a 3-second timeout.
        /// *** Use this method only as a coroutine. ***
        /// @code
        /// // Call this method with: StartCoroutine(NextPreviousWithWait(false));
        /// // See TestMidiFilePlayerScripting.cs
        /// public IEnumerator NextPreviousWithWait(bool next)
        /// {
        /// midiFilePlayer.MPTK_Stop();
        /// yield return midiFilePlayer.MPTK_WaitAllNotesOff(midiFilePlayer.IdSession);
        /// if (next)
        /// midiFilePlayer.MPTK_Next();
        /// else
        /// midiFilePlayer.MPTK_Previous();
        /// CurrentIndexPlaying = midiFilePlayer.MPTK_MidiIndex;
        /// yield return 0;
        /// }
        /// @endcode
        /// </summary>
        /// <param name="_idSession">Clear only samples playing in this session; -1 clears all.</param>
        /// <returns></returns>
        public IEnumerator MPTK_WaitAllNotesOff(int _idSession = -1) // V2.84: new param idsession and CoRoutine compatible
        {
            //Debug.Log($"<<< {DateTime.Now} MPTK_WaitAllNotesOff {_idSession}");
            int count = 999;
            DateTime start = DateTime.Now;
            //Debug.Log($"ThreadWaitAllStop " + start);
            if (ActiveVoices != null)
            {
                while (count != 0 && (DateTime.Now - start).TotalMilliseconds < 3000d)
                {
                    count = 0;
                    foreach (fluid_voice voice in ActiveVoices)
                        if (voice != null &&
                           (_idSession == -1 || voice.IdSession == _idSession) &&
                           (voice.status == fluid_voice_status.FLUID_VOICE_ON || voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED))
                            count++;
                    //Debug.LogFormat("   ThreadReleaseAll\t{0}\t{1}\t{2}/{3}", start, (DateTime.Now - start).TotalMilliseconds, count, ActiveVoices.Count);
                    yield return new WaitForSeconds(.1f);
                }
            }
            //Debug.Log($"<<< {DateTime.Now} MPTK_WaitAllNotesOff {_idSession}");
            yield return 0;
        }

        /// @name MIDI Direct Play
        /// @brief Send a single MIDI event directly to the synthesizer (immediate, synchronous).
        /// @details
        /// This section provides low-level methods to play or stop one MIDI event without using the MIDI sequencer.
        /// The event is processed immediately by the synth and the method returns only after the event has been handled.
        /// 
        /// Supported commands include:
        /// - NoteOn (Velocity 0 is treated as NoteOff)
        /// - NoteOff (optional handling via playNoteOff)
        /// - ControlChange (applied only when MPTK_ApplyRealTimeModulator is enabled)
        /// - PatchChange (with drum channel rules depending on MPTK_EnablePresetDrum)
        /// - PitchWheelChange
        /// - MetaEvent (TextEvent may trigger action parsing in Pro builds)
        /// 
        /// Typical use cases:
        /// - trigger one-shot notes or SFX
        /// - real-time modulation (CC, pitch bend)

        /// @warning
        /// This method should be used sparingly, method returns only after the event has been fully handled.
        /// For sending a large number of MIDI events, it is recommended to use the
        /// MidiStreamPlayer prefab with #MPTK_PlayEvent. In this case events are queued
        /// and processed efficiently by the internal MIDI event stack.

        /// @{

        /// <summary>@brief
        /// Plays a single MIDI event immediately.
        /// Processing is synchronous: the method returns only after the MIDI event has been handled by the synth.
        /// @snippet TestMidiFilePlayerScripting.cs Example_OnBeatEvent
        /// </summary>
        /// <param name="midiEvent"></param>
        public void MPTK_PlayDirectEvent(MPTKEvent midiEvent, bool playNoteOff = true)
        {
            //Debug.Log($">>> PlayEvent IdSynth:'{this.IdSynth}'");

            try
            {
                if (MPTK_SoundFont.SoundFont == null)
                {
                    Debug.Log("No SoundFont selected for MPTK_PlayNote ");
                    return;
                }

#if LOG_PERF_NOTEON
                DebugPerf("-----> Init perf:", 0);
#endif
                if (MPTK_LogEvents && midiEvent != null)
                    Debug.Log(/*state.ToString() + " " + */midiEvent.ToString());

                switch (midiEvent.Command)
                {
                    case MPTKCommand.NoteOn:
                        if (midiEvent.Velocity != 0)
                        {
#if DEBUGNOTE
                            numberNote++;
                            if (numberNote < startNote || numberNote > startNote + countNote - 1) return;
#endif
                            //if (note.Channel==4)
                            fluid_defpreset_noteon(midiEvent);
                        }
                        else
                        {
                            //Debug.Log("PlayEvent: NoteOn velocity=0 " + midiEvent.Value);
                            fluid_synth_noteoff(midiEvent.Channel, midiEvent.Value);
                        }
                        break;

                    case MPTKCommand.NoteOff:
                        if (playNoteOff)
                            fluid_synth_noteoff(midiEvent.Channel, midiEvent.Value);
                        break;

                    case MPTKCommand.KeyAfterTouch:
                        // Not processed by nAudio
                        //Channels[midiEvent.Channel].key_pressure[key] = midiEvent.Value;
                        break;

                    case MPTKCommand.ControlChange:
                        //if (midiEvent.Controller == MPTKController.Modulation) Debug.Log("midiEvent.Controller Modulation " + midiEvent.Value);
                        if (MPTK_ApplyRealTimeModulator)
                            //Channels[midiEvent.Channel].fluid_channel_cc(midiEvent.Controller, midiEvent.Value); // replace of fluid_synth_cc(note.Channel, note.Controller, (int)note.Value);
                            Channels[midiEvent.Channel].Controller(midiEvent.Controller, midiEvent.Value); // replace of fluid_synth_cc(note.Channel, note.Controller, (int)note.Value);
                        break;

                    case MPTKCommand.PatchChange:
                        if (midiEvent.Channel != 9 || MPTK_EnablePresetDrum == true)
                        {
                            Channels[midiEvent.Channel].LastPreset = midiEvent.Value;
                            // before v2.11 fluid_synth_program_change(midiEvent.Channel, midiEvent.Value);
                            Channels[midiEvent.Channel].fluid_synth_program_change(midiEvent.Value);
                        }
                        break;

                    case MPTKCommand.PitchWheelChange:
                        fluid_synth_pitch_bend(midiEvent.Channel, midiEvent.Value);
                        break;
                    case MPTKCommand.MetaEvent:
#if MPTK_PRO
                        if (midiEvent.Meta == MPTKMeta.TextEvent)
                        {
                            AnalyseActionMeta(midiEvent);
                        }
#endif
                        break;
                }
#if LOG_PERF_NOTEON
                DebugPerf("<---- ClosePerf perf:", 2);
#endif
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            //Debug.Log($"<<< PlayEvent IdSynth:'{this.IdSynth}'");
        }

        /// <summary>@brief
        /// Stops immediately the MIDI event played with @MPTK_PlayDirectEvent.
        /// Processing is synchronous: the method returns after all voices for the notes have been handled by the synth.
        /// </summary>
        /// <param name="midiEvent"></param>
        public void MPTK_StopDirectEvent(MPTKEvent midiEvent)
        {
            StopEvent(midiEvent);
        }


        /// @}


        public IEnumerator<float> ThreadClearAllSound(bool destroyAudioSource = false, int _idSession = -1)
        {
#if DEBUGNOTE
            numberNote = -1;
#endif
            MPTK_ResetStat();
            //Debug.Log($" >>> {DateTime.Now} ThreadClearAllSound:{_idSession} destroyAudioSource:{destroyAudioSource}");
            if (MPTK_CorePlayer)
            {
                if (SpatialSynths != null && spatialSynthIndex == -1) // apply only for the MidiSynth reader
                {
                    //Debug.Log($"For Spatial ");
                    foreach (MidiFilePlayer mfp in SpatialSynths)
                        if (mfp.QueueSynthCommand != null)
                            mfp.QueueSynthCommand.Enqueue(new SynthCommand() { Command = SynthCommand.enCmd.NoteOffAll, IdSession = _idSession });
                    // Could be gard to synch all synth ! prefer a robust solution ...
                    // V2.84 yield return Timing.WaitForSeconds(0.5f);
                }
                else
                {
                    if (QueueSynthCommand != null)
                        QueueSynthCommand.Enqueue(new SynthCommand() { Command = SynthCommand.enCmd.NoteOffAll, IdSession = _idSession });
                    // V2.84 yield return Timing.WaitUntilDone(Timing.RunCoroutine(ThreadWaitAllStop()), false);
                }
            }
            else
            {
                if (ActiveVoices != null)
                {
                    for (int i = 0; i < ActiveVoices.Count; i++)
                    {
                        fluid_voice voice = ActiveVoices[i];
                        //Debug.LogFormat($"Release Active id:{voice.id} / {ActiveVoices.Count} {voice.VoiceAudio.name} {voice.status} {(voice.VoiceAudio.Audiosource.clip != null ? voice.VoiceAudio.Audiosource.clip.name : "no clip")}");
                        if (voice != null && (voice.status == fluid_voice_status.FLUID_VOICE_ON || voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED))
                        {
                            //voice.fluid_rvoice_noteoff(true);
                            Routine.RunCoroutine(voice.Release(), Segment.RealtimeUpdate);
                            //yield return Routine.WaitUntilDone(Routine.RunCoroutine(voice.Release(), Segment.RealtimeUpdate));
                        }
                    }
                }
            }
            if (destroyAudioSource)
            {
                if (!MPTK_CorePlayer)
                    Routine.RunCoroutine(ThreadDestroyAllVoice(), Segment.RealtimeUpdate);
                else
                    yield return Routine.WaitUntilDone(Routine.RunCoroutine(ThreadDestroyAllVoice(), Segment.RealtimeUpdate), false);
            }

            //Debug.Log($" <<< {DateTime.Now} ThreadClearAllSound {_idSession}");

            yield return 0;
        }

        public IEnumerator<float> ThreadVoiceAudioSourceRemove(float wait, int _idSession = -1)
        {
            //Debug.Log($" >>> {DateTime.Now} ThreadClearAllSound:{_idSession} ");
            DateTime dateTime = DateTime.Now;
            if (wait <= 0f)
                wait = 2000f;
            while (true)
            {
                VoiceAudioSource[] voiceAudioSources = FindObjectsByType<VoiceAudioSource>(FindObjectsSortMode.InstanceID);
                //Debug.Log($"    MPTK_Stop {(DateTime.Now - dateTime).TotalMilliseconds} {voiceAudioSources.Length}");
                if (voiceAudioSources.Length == 0)
                    break;
                if ((DateTime.Now - dateTime).TotalMilliseconds > wait)
                    break;
                yield return Routine.WaitForSeconds(0.1f);

            }


            //Debug.Log($" <<< {DateTime.Now} ThreadClearAllSound {_idSession}");

            yield return 0;
        }



        // Nothing to document after this line
        // @cond NODOC

        public IEnumerator<float> ThreadWaitAllStop(int _idSession = -1)
        {
            int count = 999;
            DateTime start = DateTime.Now;
            //Debug.Log($"ThreadWaitAllStop " + start);
            if (ActiveVoices != null)
            {
                while (count != 0 && (DateTime.Now - start).TotalMilliseconds < 3000d)
                {
                    count = 0;
                    foreach (fluid_voice voice in ActiveVoices)
                        if (voice != null &&
                           (_idSession == -1 || voice.IdSession == _idSession) &&
                           (voice.status == fluid_voice_status.FLUID_VOICE_ON || voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED))
                            count++;
                    //Debug.LogFormat("   ThreadReleaseAll\t{0}\t{1}\t{2}/{3}", start, (DateTime.Now - start).TotalMilliseconds, count, ActiveVoices.Count);
                    yield return Routine.WaitForSeconds(0.2f);
                }
            }
            //Debug.Log($"ThreadWaitAllStop end - {DateTime.Now} count:{count}");
            yield return 0;

        }

        /// Remove AudioSource not playing
        /// </summary>
        protected IEnumerator<float> ThreadDestroyAllVoice()
        {
            //Debug.Log(">>> ThreadDestroyAllVoice");
            try
            {
                if (MPTK_CorePlayer)
                {
                    if (QueueSynthCommand != null)
                        // ActiveVoices and FreVoices clear
                        QueueSynthCommand.Enqueue(new SynthCommand() { Command = SynthCommand.enCmd.ClearAllVoices });
                }
                else
                {
                    // Search voice audio source but not template which is inactive
                    VoiceAudioSource[] voiceAudioSources = FindObjectsByType<VoiceAudioSource>(FindObjectsSortMode.InstanceID);
                    //Debug.Log($"    ThreadDestroyAllVoice - {voiceAudioSources.Length} AudioSource found");

                    if (voiceAudioSources != null)
                        foreach (VoiceAudioSource voice in voiceAudioSources)
                        {
                            //Debug.Log($"    Destroy id:{voice.name} {(voice.Audiosource.clip != null ? voice.Audiosource.clip.name : "no clip")}");
                            if (voice.name != "VoiceAudioSourceTemplate") // Test in case template gas been enabled by error!
                                try
                                {
                                    // Delete voice audio source and its audio source 
                                    Destroy(voice.gameObject);
                                }
                                catch (System.Exception ex)
                                {
                                    MidiPlayerGlobal.ErrorDetail(ex);
                                }
                        }
                    if (ActiveVoices != null) ActiveVoices.Clear();
                    if (FreeVoices != null) FreeVoices.Clear();
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            yield return 0;
            //Debug.Log("<<< ThreadDestroyAllVoice");

        }

        private void killVoices(List<fluid_voice> voices)
        {
            if (voices != null)
                for (int i = 0; i < voices.Count; i++)
                {
                    try
                    {
                        fluid_voice voice = voices[i];
                        if (voice != null && voice.VoiceAudio != null)
                        {
                            //Debug.Log($"Destroy id:{voice.id} {voice.VoiceAudio.name} {(voice.VoiceAudio.Audiosource.clip != null ? voice.VoiceAudio.Audiosource.clip.name : "no clip")}");
                            // Delete all audio source but not template
                            if (voice.VoiceAudio.name.StartsWith("VoiceAudioId_"))
                                Destroy(voice.VoiceAudio.gameObject);
                        }
                        //else Debug.Log($"Destroy not done id:{voice?.id} name:{voice.VoiceAudio?.name}");

                    }
                    catch (System.Exception ex)
                    {
                        MidiPlayerGlobal.ErrorDetail(ex);
                    }
                }
            voices.Clear();
        }

        /// @name Synthesizer Events
        /// @brief Events raised by the MIDI synthesizer during audio processing.
        /// @details
        /// These callbacks allow user scripts to interact with the MIDI synthesizer
        /// during playback. Events may be triggered from the audio processing thread,
        /// so Unity API calls are generally not allowed except for Debug.Log.
        ///
        /// Typical uses include:
        /// - reacting to audio frame timing
        /// - intercepting MIDI events before playback
        /// - tracking musical structure such as beats or measures
        ///
        /// Developers should keep event handlers lightweight to avoid affecting
        /// audio timing accuracy.
        
        /// @{

        void OnApplicationQuit()
        {
            //Debug.Log("MidiSynth OnApplicationQuit " + Time.time + " seconds");
            state = fluid_synth_status.FLUID_SYNTH_STOPPED;
        }

        private void OnApplicationPause(bool pause)
        {
            //Debug.Log("MidiSynth OnApplicationPause " + pause);
        }

        /// @}

        protected void ResetMidiPlayer()
        {
            //Debug.Log("ResetMidi");

            timeMidiFromStartPlay = 0d;
            lastTimeMidi = 0d;
            watchMidi.Reset();
            watchMidi.Start();
            if (midiLoaded != null) midiLoaded.ResetMidiPlayer();
        }

        // removed from 2.10.1 protected void ResetChannels()
        //{
        //    if (MPTK_ResetChannel)
        //    {
        //        Channels.MPTK_ResetMaestroChannelsExtension();
        //    }
        //}

        // @endcond


        /*
         * fluid_mod_set_source1
         */
        void fluid_mod_set_source1(HiMod mod, int src, int flags)
        {
            mod.src1 = (byte)src;
            mod.flags1 = (byte)flags;
        }

        /*
         * fluid_mod_set_source2
         */
        void fluid_mod_set_source2(HiMod mod, int src, int flags)
        {
            mod.src2 = (byte)src;
            mod.flags2 = (byte)flags;
        }

        /*
         * fluid_mod_set_dest
         */
        void fluid_mod_set_dest(HiMod mod, int dest)
        {
            mod.dest = (byte)dest;
        }

        /*
         * fluid_mod_set_amount
         */
        void fluid_mod_set_amount(HiMod mod, float amount)
        {
            mod.amount = amount;
        }

        // @cond NODOC
        // No doc, deprecated methods

        /// <summary>@brief
        /// Allocate a synthesis voice. This function is called by a SoundFont's preset in response to a note-on event.
        /// The returned voice comes with default modulators installed(velocity-to-attenuation, velocity to filter, ...)
        /// Note: A single note-on event may create any number of voices, when the preset is layered. Typically 1 (mono) or 2 (stereo).
        /// </summary>
        /// <param name="hiSample">the hisample of the zone selected for key/vel/instrument/preset</param>
        /// <param name="chan"></param>
        /// <param name="_idSession"></param>
        /// <param name="key"></param>
        /// <param name="vel"></param>
        /// <returns></returns>
        public fluid_voice fluid_synth_alloc_voice_LOCAL(HiSample hiSample, MPTKEvent mptkEvent)
        {
            fluid_voice voice = null;
            MPTK_StatVoicePlayed++;

            /*   fluid_mutex_lock(synth.busy); /\* Don't interfere with the audio thread *\/ */
            /*   fluid_mutex_unlock(synth.busy); */

            // check if there's an available free voice with same sample and same session
            //if (true)
            for (int indexVoice = 0; indexVoice < FreeVoices.Count;)
            {
                fluid_voice freeVoice = FreeVoices[indexVoice];
                if (freeVoice.sample.Name == hiSample.Name /* v2.10.1 useless? freevoice is cleared when a new MIDI is played && _idSession == freeVoice.IdSession*/)
                {
                    voice = freeVoice;
                    voice.Reused = true;
#if MPTK_PRO 
                    voice.ResetPause();
#endif
                    FreeVoices.RemoveAt(indexVoice);
                    MPTK_StatVoiceCountReused++;
                    if (VerboseVoice) Debug.Log($"Voice {voice.id} - Reuse - Sample:'{hiSample.Name}'");
                    break;
                }
                indexVoice++;
            }
            //else
            //    Debug.Log($"Cache disabled");

#if LOG_PERF_NOTEON
            DebugPerf("After find existing voice:");
#endif
            // No existing voice found, instantiate a new one
            if (voice == null)
            {
                if (VerboseVoice) Debug.Log($"Voice idSession:{mptkEvent.IdSession} idVoice:{fluid_voice.LastId} - Create - Sample:'{hiSample.Name}' Rate:{hiSample.SampleRate} hz");

                voice = new fluid_voice(this);
                voice.IdSession = mptkEvent.IdSession;
                voice.Reused = false;

                if (MPTK_CorePlayer)
                {
                    // Play voice with OnAudioFilterRead
                    // --------------------------------------

                    if (MidiPlayerGlobal.ImSFCurrent != null && MidiPlayerGlobal.ImSFCurrent.LiveSF)
                    {
                        // each voice have a pointer to the full samples available in the SF
                        hiSample.Data = MidiPlayerGlobal.ImSFCurrent.SamplesData;
                        // TBD hiSample.Data = sfLocal == null ? MidiPlayerGlobal.ImSFCurrent.SamplesData : sfLocal.SamplesData;
                        voice.sample = hiSample;
                    }
                    else if (/*MPTK_SoundFont.SoundFont != null && */MPTK_SoundFont.SoundFont.LiveSF)
                    {
                        hiSample.Data = MPTK_SoundFont.SoundFont.SamplesData;
                        // TBD hiSample.Data = sfLocal == null ? MidiPlayerGlobal.ImSFCurrent.SamplesData : sfLocal.SamplesData;
                        voice.sample = hiSample;
                    }
                    else
                    {
                        // Search sample from the dictionary loaded from the resource wave
                        voice.VoiceAudio = null;
                        //if (MidiPlayerGlobal.MPTK_LoadWaveAtStartup)
                        {
#if MPTK_PRO
                            voice.sample = MPTK_SoundFont.DicAudioWaveLocal == null ?
                                DicAudioWave.GetWave(hiSample.Name) :
                                MPTK_SoundFont.DicAudioWaveLocal.GetWave(hiSample.Name);
#else
                            voice.sample = DicAudioWave.GetWave(hiSample.Name);
#endif
                            if (voice.sample == null)
                            {
                                Debug.Log($"<color=red>Sample {hiSample.Name} not loaded. Have you extracted samples from 'SoundFont Setup' menu?</color>");
                                // Not possible using Unity API outside the Unity thread
                                //MidiPlayerGlobal.LoadWave(hiSample);
                                //voice.sample = DicAudioWave.GetWave(hiSample.Name);
                            }

                        }
                        //else - non, on ne peut pas utiliser AudioClip et Resources endehors du main thread d'unity
                        //{
                        //    string path = MidiPlayerGlobal.WavePath + "/" + System.IO.Path.GetFileNameWithoutExtension(hiSample.Name);// + ".wav";
                        //    AudioClip ac = Resources.Load<AudioClip>(path);
                        //    if (ac != null)
                        //    {
                        //        float[] data = new float[ac.samples * ac.channels];
                        //        if (ac.GetData(data, 0))
                        //        {
                        //            //Debug.Log(smpl.Name + " " +ac.samples * ac.channels);
                        //            hiSample.Data = data;
                        //        }
                        //    }
                        //}
                    }

                    if (voice.sample == null)
                    {
                        Debug.LogWarningFormat("fluid_synth_alloc_voice - Clip {0} data not loaded", hiSample.Name);
                        return null;
                    }
                    // Debug.LogFormat("fluid_synth_alloc_voice - load wave from dict. {0} Length:{1} SynthSampleRate:{2}", hiSample.Name, voice.sample.Data.Length, sample_rate);
                }
                else
                {
                    // Play each voice with a dedicated AudioSource (legacy mode)
                    // ----------------------------------------------------------
                    AudioClip clip = null;
                    if (MidiPlayerGlobal.ImSFCurrent != null && MidiPlayerGlobal.ImSFCurrent.LiveSF)
                    {
                        // each voice have a pointer to the full samples available in the SF
                        hiSample.Data = MidiPlayerGlobal.ImSFCurrent.SamplesData;
                        // TBD when sample comes from the SamplesData, we need to extract the sample related to the voice and create an AudioClip ....
                        voice.sample = hiSample;
                    }
                    else if (/*MPTK_SoundFont.SoundFont != null && */MPTK_SoundFont.SoundFont.LiveSF)
                    {
                        hiSample.Data = MPTK_SoundFont.SoundFont.SamplesData;
                        // TBD when sample comes from the SamplesData, we need to extract the sample related to the voice and create an AudioClip ....
                        voice.sample = hiSample;
                    }
                    else
                    {
                        clip = DicAudioClip.Get(hiSample.Name);
                        if (clip == null)
                        {
                            string path = MidiPlayerGlobal.WavePath + "/" + System.IO.Path.GetFileNameWithoutExtension(hiSample.Name);
                            AudioClip ac = Resources.Load<AudioClip>(path);
                            if (ac != null)
                            {
                                //Debug.Log("Wave load " + path);
                                DicAudioClip.Add(hiSample.Name, ac);
                            }
                            clip = DicAudioClip.Get(hiSample.Name);
                            if (clip == null /*|| clip.loadState != AudioDataLoadState.Loaded*/)
                            {
                                if (VerboseVoice) Debug.LogWarningFormat("fluid_synth_alloc_voice - Clip {0} not found", hiSample.Name);
                                return null;
                            }
                            // v2.16.0 seems clip is not loaded until associated to the audio source
                            //else if (clip.loadState != AudioDataLoadState.Loaded)
                            //{
                            //    if (VerboseVoice) Debug.LogWarningFormat("fluid_synth_alloc_voice - Clip {0} not ready to play {1}", hiSample.Name, clip.loadState);
                            //    return null;
                            //}
                        }
                        if (VerboseVoice) Debug.Log($"Voice idSession:{mptkEvent.IdSession} idVoice:{fluid_voice.LastId} - Clip created - name:'{clip.name}' loadState:{clip.loadState}");
                        voice.sample = hiSample;
                    }

                    if (clip == null)
                    {
                        Debug.LogWarningFormat("fluid_synth_alloc_voice - Clip {0} data not loaded - not compliant with WebGL", hiSample.Name);
                    }
                    else
                    {

                        voice.VoiceAudio = Instantiate<VoiceAudioSource>(AudiosourceTemplate);
                        voice.VoiceAudio.gameObject.SetActive(true);
                        voice.VoiceAudio.fluidvoice = voice;
                        voice.VoiceAudio.synth = this;
                        voice.VoiceAudio.transform.position = AudiosourceTemplate.transform.position;
                        voice.VoiceAudio.transform.SetParent(AudiosourceTemplate.transform.parent);
                        voice.VoiceAudio.name = "VoiceAudioId_" + voice.id;
                        voice.VoiceAudio.Audiosource.clip = clip;
                        //voice.VoiceAudio.Audiosource.loop
                        // seems to have no effect, issue open with Unity
                        voice.VoiceAudio.hideFlags = VerboseVoice ? HideFlags.None : HideFlags.HideInHierarchy;

                        if (VerboseVoice) Debug.Log($"Voice idSession:{mptkEvent.IdSession} idVoice:{fluid_voice.LastId} - AudioSource Instanciated - name:'{clip.name}' loadState:{clip.loadState}");
                    }
                }

                //AddDefaultMod(voice);

#if LOG_PERF_NOTEON
                DebugPerf("After instanciate voice:");
#endif
            }
            //else if (VerboseVoice) Debug.Log($"Voice idSession:{_idSession} idVoice:{fluid_voice.LastId} - Reuse - Sample:'{hiSample.Name}' Rate:{hiSample.SampleRate} hz");

            voice.MptkEvent = mptkEvent;
            mptkEvent.Voices.Add(voice);
            voice.Duration = mptkEvent.Duration; // only for information, not used

            // Remove 2.17
            ////// Apply change on each voice
            ////if (MPTK_CorePlayer)
            ////{
            //// // Done with ThreadCorePlay in MidiFilePlayer
            ////}
            ////else
            ////{
            //// // Legacy mode
            //// if (voice.VoiceAudio != null)
            //// voice.VoiceAudio.Audiosource.spatialBlend = MPTK_Spatialize ? 1f : 0f;

            //// // Now done in a coroutine for legacy mode
            //// //MoveVoiceToFree();
            //// //if (MPTK_AutoBuffer)
            //// // AutoCleanVoice(DateTime.UtcNow.Ticks);
            ////}

            int chan = mptkEvent.Channel;

            if (chan < 0 || chan >= Channels.Length)
            {
                Debug.LogFormat("Channel out of range chan:{0}", chan);
                chan = 0;
            }

            MPTKChannel channel = Channels[chan];

            // Defined default voice value. Called also when a voice is reused.
            voice.fluid_voice_init(channel, mptkEvent.Value, mptkEvent.Velocity);

#if LOG_PERF_NOTEON
            DebugPerf("After fluid_voice_init:");
#endif

            // pourquoi reinit mod si voice depuis le cache ? ... a tester
            AddDefaultMod(voice);
#if LOG_PERF_NOTEON
            DebugPerf("After AddDefaultMod:");
#endif


            ActiveVoices.Add(voice);
            voice.IndexActive = ActiveVoices.Count - 1;

            MPTK_StatVoiceCountActive = ActiveVoices.Count;
            MPTK_StatVoiceCountFree = FreeVoices.Count;
            MPTK_StatVoiceRatioReused = MPTK_StatVoicePlayed > 0 ? (MPTK_StatVoiceCountReused * 100) / MPTK_StatVoicePlayed : 0;
            return voice;
        }

        /* From fluid_synth_alloc_voice_LOCAL
            add the default modulators to the synthesis process. 
            custom_breath2att_modulator is not a default modulator specified in SF
            it is intended to replace default_vel2att_mod for this channel on demand using
            API fluid_synth_set_breath_mode() or shell command setbreathmode for this channel.
        */
        private static void AddDefaultMod(fluid_voice voice)
        {
            /* add the default modulators to the synthesis process. */
            voice.mods = new List<HiMod>();
            voice.fluid_voice_add_mod(MidiSynth.default_vel2att_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT);    /* SF2.01 $8.4.1  */
            voice.fluid_voice_add_mod(MidiSynth.default_vel2filter_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT); /* SF2.01 $8.4.2  */
            voice.fluid_voice_add_mod(MidiSynth.default_at2viblfo_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT);  /* SF2.01 $8.4.3  */
            voice.fluid_voice_add_mod(MidiSynth.default_mod2viblfo_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT); /* SF2.01 $8.4.4  */
            voice.fluid_voice_add_mod(MidiSynth.default_att_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT);        /* SF2.01 $8.4.5  */
            voice.fluid_voice_add_mod(MidiSynth.default_pan_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT);        /* SF2.01 $8.4.6  */
            voice.fluid_voice_add_mod(MidiSynth.default_expr_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT);       /* SF2.01 $8.4.7  */
            voice.fluid_voice_add_mod(MidiSynth.default_reverb_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT);     /* SF2.01 $8.4.8  */
            voice.fluid_voice_add_mod(MidiSynth.default_chorus_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT);     /* SF2.01 $8.4.9  */
            voice.fluid_voice_add_mod(MidiSynth.default_pitch_bend_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT); /* SF2.01 $8.4.10 */
        }

        public void fluid_synth_kill_by_exclusive_class(fluid_voice new_voice)
        {
            //fluid_synth_t* synth
            /** Kill all voices on a given channel, which belong into
                excl_class.  This function is called by a SoundFont's preset in
                response to a noteon event.  If one noteon event results in
                several voice processes (stereo samples), ignore_ID must name
                the voice ID of the first generated voice (so that it is not
                stopped). The first voice uses ignore_ID=-1, which will
                terminate all voices on a channel belonging into the exclusive
                class excl_class.
            */

            //int i;
            int excl_class = (int)new_voice.gen[(int)fluid_gen_type.GEN_EXCLUSIVECLASS].Val;
            /* Check if the voice belongs to an exclusive class. In that case, previous notes from the same class are released. */

            /* Excl. class 0: No exclusive class */
            if (excl_class == 0)
            {
                return;
            }

            //if (VerboseKillByExclusive) new_voice.DebugKillByExclusive($"Check existing voice with class {excl_class}");

            //  FLUID_LOG(FLUID_INFO, "Voice belongs to exclusive class (class=%d, ignore_id=%d)", excl_class, ignore_ID);

            /* Kill all notes on the same channel with the same exclusive class */

            for (int i = 0; i < ActiveVoices.Count; i++)
            {
                fluid_voice voice = ActiveVoices[i];
                /* Existing voice does not play? Leave it alone. */
                if (!(voice.status == fluid_voice_status.FLUID_VOICE_ON) || voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED)
                {
                    continue;
                }

                /* An exclusive class is valid for a whole channel (or preset). Is the voice on a different channel? Leave it alone. */
                if (voice.chan != new_voice.chan)
                {
                    continue;
                }

                /* Existing voice has a different (or no) exclusive class? Leave it alone. */
                if ((int)voice.gen[(int)fluid_gen_type.GEN_EXCLUSIVECLASS].Val != excl_class)
                {
                    continue;
                }

                /* Existing voice is a voice process belonging to this note-on event (for example: stereo sample)?  Leave it alone. */
                if (voice.id == new_voice.id)
                {
                    if (VerboseSpecialNoteOff) voice.DebugKillByExclusive("voice.IdVoice == new_voice.IdVoice");
                    continue;
                }

                //    FLUID_LOG(FLUID_INFO, "Releasing previous voice of exclusive class (class=%d, id=%d)",
                //     (int)_GEN(existing_voice, GEN_EXCLUSIVECLASS), (int)fluid_voice_get_id(existing_voice));
                //Debug.Log($"{voice.key} {voice.SampleName} ");

                voice.fluid_voice_kill_excl();
            }
        }

        // If the same note is hit twice on the same channel, then the older voice process is advanced to the release stage.  
        // Using a mechanical MIDI controller, the only way this can happen is when the sustain pedal is held.
        // In this case the behaviour implemented here is natural for many instruments.  
        // Note: One noteon event can trigger several voice processes, for example a stereo sample.  Don't release those...
        void fluid_synth_release_voice_on_same_note(int chan, int key)
        {
            for (int i = 0; i < ActiveVoices.Count; i++)
            {
                fluid_voice voice = ActiveVoices[i];
                if (voice.chan == chan && voice.key == key)
                //&& (fluid_voice_get_id(voice) != synth->noteid))
                {
                    //if (ForceVoiceOff)
                    //voice.fluid_voice_off();
                    //else
                    voice.fluid_rvoice_noteoff(true);
                    if (VerboseSpecialNoteOff) Debug.Log($"Voice {voice.id} - Same note, send note-off {chan} key:{key} {voice.volenv_section} DurationTick:{voice.DurationTick} Name:{voice.sample.Name}");
                }
            }
        }

        // fluid_synth_all_notes_off in fluidsynth
        // V2.89.0 not used, replaced with fluid_synth_noteoff(pchan, -1) 
        //public void fluid_synth_allnotesoff()
        //{
        //    for (int i = 0; i < ActiveVoices.Count; i++)
        //    {
        //        fluid_voice voice = ActiveVoices[i];
        //        if (voice.status == fluid_voice_status.FLUID_VOICE_ON &&
        //            voice.volenv_section < fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE)
        //        {
        //            //Debug.Log($"fluid_synth_noteoff Channel:{pchan} key:{pkey} Isloop:{voice.IsLoop} Ignore:{keepPlayingNonLooped} Name:{voice.sample.Name}");
        //            voice.fluid_voice_noteoff();
        //        }
        //    }
        //}

        // if pkey == -1 equivalent to fluid_synth_all_notes_off in fluidsynth
        public void fluid_synth_noteoff(int pchan, int pkey)
        {
            for (int i = 0; i < ActiveVoices.Count; i++)
            {
                fluid_voice voice = ActiveVoices[i];
                // A voice is 'ON', if it has not yet received a noteoff event. Sending a noteoff event will advance the envelopes to  section 5 (release). 
                //#define _ON(voice)  ((voice)->status == FLUID_VOICE_ON && (voice)->volenv_section < FLUID_VOICE_ENVRELEASE)
                if (voice.status == fluid_voice_status.FLUID_VOICE_ON &&
                    voice.volenv_section < fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE &&
                    voice.chan == pchan &&
                    //(voice.IsLoop || !keepPlayingNonLooped) && // V2.89.0
                    (!keepPlayingNonLooped || voice.samplemode == fluid_loop.FLUID_LOOP_UNTIL_RELEASE || voice.samplemode == fluid_loop.FLUID_LOOP_DURING_RELEASE) &&
                    (pkey == -1 || voice.key == pkey))
                {
                    //Debug.Log($"fluid_synth_noteoff Channel:{pchan} key:{pkey} Isloop:{voice.IsLoop} Ignore:{keepPlayingNonLooped} Name:{voice.sample.Name}");
                    voice.fluid_rvoice_noteoff();
                }
            }
        }

        public void fluid_synth_soundoff(int pchan)
        {
            for (int i = 0; i < ActiveVoices.Count; i++)
            {
                fluid_voice voice = ActiveVoices[i];
                // A voice is 'ON', if it has not yet received a noteoff event. Sending a noteoff event will advance the envelopes to  section 5 (release). 
                //#define _ON(voice)  ((voice)->status == FLUID_VOICE_ON && (voice)->volenv_section < FLUID_VOICE_ENVRELEASE)
                if (voice.status == fluid_voice_status.FLUID_VOICE_ON &&
                    voice.volenv_section < fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE &&
                    voice.chan == pchan)
                {
                    //fluid_global.FLUID_LOG(fluid_log_level.FLUID_INFO, "noteoff chan:{0} key:{1} vel:{2} time{3}", voice.chan, voice.key, voice.vel, (fluid_curtime() - start) / 1000.0f);
                    voice.fluid_voice_off();
                }
            }
        }

        /*
         * fluid_synth_damp_voices - clear voices sustained (controller < 64)
         */
        public void fluid_synth_damp_voices(int pchan)
        {
            for (int i = 0; i < ActiveVoices.Count; i++)
            {
                fluid_voice voice = ActiveVoices[i];
                //#define _SUSTAINED(voice)  ((voice)->status == FLUID_VOICE_SUSTAINED)
                if (voice.chan == pchan && voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED)
                    voice.fluid_rvoice_noteoff(true);
            }
        }

        /*
         * fluid_synth_cc - call directly
         */
        public void fluid_synth_cc(int chan, MPTKController num, int val)
        {
            /*   fluid_mutex_lock(busy); /\* Don't interfere with the audio thread *\/ */
            /*   fluid_mutex_unlock(busy); */

            /* check the ranges of the arguments */
            //if ((chan < 0) || (chan >= midi_channels))
            //{
            //    FLUID_LOG(FLUID_WARN, "Channel out of range");
            //    return FLUID_FAILED;
            //}
            //if ((num < 0) || (num >= 128))
            //{
            //    FLUID_LOG(FLUID_WARN, "Ctrl out of range");
            //    return FLUID_FAILED;
            //}
            //if ((val < 0) || (val >= 128))
            //{
            //    FLUID_LOG(FLUID_WARN, "Value out of range");
            //    return FLUID_FAILED;
            //}

            /* set the controller value in the channel */
            //Channels[chan].fluid_channel_cc(num, val);
            Channels[chan].Controller(num, val);
        }

        /// <summary>@brief
        /// Instructs all active voices on this channel to update their synthesis parameters after a control change.
        /// </summary>
        /// <param name="chan"></param>
        /// <param name="is_cc"></param>
        /// <param name="ctrl"></param>
        public void fluid_synth_modulate_voices(int chan, int is_cc, int ctrl)
        {
            for (int i = 0; i < ActiveVoices.Count; i++)
            {
                fluid_voice voice = ActiveVoices[i];
                if (voice.chan == chan && voice.status != fluid_voice_status.FLUID_VOICE_OFF)
                    voice.fluid_voice_modulate(is_cc, ctrl);
            }
        }

        /// <summary>@brief
        /// Instructs all voices on this channel to refresh their synthesis parameters after an All Controllers Off message (all controllers reset to default).
        /// </summary>
        /// <param name="chan"></param>
        public void fluid_synth_modulate_voices_all(int chan)
        {
            for (int i = 0; i < ActiveVoices.Count; i++)
            {
                fluid_voice voice = ActiveVoices[i];
                if (voice.chan == chan)
                    voice.fluid_voice_modulate_all();
            }
        }

        /*
         * fluid_synth_program_change
         */
        //public void fluid_synth_program_change(int pchan, int preset)
        //{
        //    MptkChannel channel;
        //    HiPreset hiPreset;
        //    int banknum;

        //    if (pchan != 9 || MPTK_EnablePresetDrum == true) // V2.89.0
        //    {
        //        if (Channels[pchan].ForcedPreset >= 0)
        //            preset = Channels[pchan].ForcedPreset;

        //        channel = Channels[pchan];

        //        banknum = Channels[pchan].ForcedBank >= 0 ? Channels[pchan].ForcedBank : channel.BankNum; //fluid_channel_get_banknum

        //        channel.prognum = preset; // fluid_channel_set_prognum
        //        channel.BankNum = banknum;

        //        if (VerboseVoice) Debug.LogFormat("ProgramChange\tChannel:{0}\tBank:{1}\tPreset:{2}", pchan, banknum, preset);
        //        hiPreset = fluid_synth_find_preset(banknum, preset);
        //        channel.hiPreset = hiPreset; // fluid_channel_set_preset
        //    }
        //}

        /*
         * fluid_synth_pitch_bend
         */
        void fluid_synth_pitch_bend(int chan, int val)
        {
            if (MPTK_ApplyRealTimeModulator)
            {
                /*   fluid_mutex_lock(busy); /\* Don't interfere with the audio thread *\/ */
                /*   fluid_mutex_unlock(busy); */

                /* check the ranges of the arguments */
                if (chan < 0 || chan >= Channels.Length)
                {
                    Debug.LogFormat("Channel out of range chan:{0}", chan);
                    return;
                }

                /* set the pitch-bend value in the channel */
                Channels[chan].fluid_channel_pitch_bend(val);
            }
        }

        /// <summary>@brief
        /// Plays a list of MIDI events
        /// </summary>
        /// <param name="midievents">List of MIDI events to play</param>
        /// <param name="playNoteOff"></param>
        protected void PlayEvents(List<MPTKEvent> midievents, bool playNoteOff = true)
        {
            if (!MPTK_SoundFont.IsReady)
                return;

            if (midievents != null)
            {
                foreach (MPTKEvent note in midievents)
                {
                    MPTK_PlayDirectEvent(note, playNoteOff);
                }
            }
        }
#if DEBUGNOTE
        public int numberNote = -1;
        public int startNote;
        public int countNote;
#endif
        protected void StopEvent(MPTKEvent midievent)
        {
            try
            {
                if (midievent != null && midievent.Voices != null)
                {
                    for (int i = 0; i < midievent.Voices.Count; i++)
                    {
                        fluid_voice voice = midievent.Voices[i];
                        if (voice.volenv_section != fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE &&
                            voice.status != fluid_voice_status.FLUID_VOICE_OFF)
                            voice.fluid_rvoice_noteoff();
                    }
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
        public unsafe void OnAudioData(AudioStream audioStream, void* dataArray, int numFrames)
        {
#else
        private void OnAudioFilterRead(float[] data, int channels)
        {
#if DEBUG_HISTO_SYNTH
            synthInfo.NextHistoPosition();
            synthInfo.SizeBuffOnAudio = data.Length;
#endif
            // data.Length == DspBufferSize * channels (so, in general the double
            // Debug.Log($"OnAudioFilterRead IdSynth:{IdSynth} length:{data.Length} channels:{channels} DspBufferSize:{DspBufferSize} state:{state}");
            //Debug.Log($"OnAudioFilterRead GetAllocatedBytesForCurrentThread:{GC.GetAllocatedBytesForCurrentThread()} CollectionCount:{GC.CollectionCount(0)}");
#endif
            // Not implemented GC.WaitForFullGCApproach(22); GC.WaitForFullGCComplete(25);
            // No effect GC.WaitForPendingFinalizers();

            if (state == fluid_synth_status.FLUID_SYNTH_PLAYING)
            {
                if (AudioThreadMidi)
                    PlayMidi();

                //This uses the Unity specific float method we added to get the buffer
                if (state == fluid_synth_status.FLUID_SYNTH_PLAYING)
                {
                    //Debug.Log($"{Thread.CurrentThread.ManagedThreadId} {Thread.CurrentThread.Name,15} {Thread.CurrentThread.IsThreadPoolThread} {Thread.CurrentThread.GetApartmentState()} {Thread.CurrentThread.Priority}");

                    long ticks = DateTime.UtcNow.Ticks;

#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
                    //SynthElapsedMilli = ticks / 10000D;
                    //DeltaTimeAudioCall = (watchSynth.ElapsedTicks) / (System.Diagnostics.Stopwatch.Frequency / 1000d);
                    //watchSynth.Reset();
                    //watchSynth.Start();

                    if (lastTimePlayCore == 0d)
                    {
                        lastTimePlayCore = ticks / 10000D;
                        return;
                    }
                    watchSynth.Reset();
                    watchSynth.Start();

                    SynthElapsedMilli = ticks / 10000D;
                    DeltaTimeAudioCall = SynthElapsedMilli - lastTimePlayCore;
                    lastTimePlayCore = SynthElapsedMilli;

#else
                    if (lastTimePlayCore == 0d)
                    {
                        lastTimePlayCore = AudioSettings.dspTime * 1000d;
                        return;
                    }
                    watchSynth.Reset();
                    watchSynth.Start();

                    SynthElapsedMilli = AudioSettings.dspTime * 1000d;
                    DeltaTimeAudioCall = SynthElapsedMilli - lastTimePlayCore;
                    lastTimePlayCore = SynthElapsedMilli;
#endif

                    //Debug.Log($"SynthElapsedMilli: {SynthElapsedMilli} lastTimePlayCore: {lastTimePlayCore} DeltaTimeAudioCall:{DeltaTimeAudioCall} ");

#if MPTK_PRO
                    StartFrame();
#endif
                    float vol = MPTK_Volume * volumeStartStop;

                    lock (this)
                    {
                        ProcessQueueCommand();

#if DEBUG_PERF_AUDIO
                        watchPerfAudio.Reset();
                        watchPerfAudio.Start();
#endif
                        ManageCacheVoice(ticks);

#if DEBUG_PERF_AUDIO
                        //GcCollectionCoutSynth = 0;
                        //for (int i = 0; i <= GC.MaxGeneration; i++)
                        //    GcCollectionCoutSynth += GC.CollectionCount(i);

                        watchPerfAudio.Stop();
                        StatProcessListMS = (float)watchPerfAudio.ElapsedTicks / ((float)System.Diagnostics.Stopwatch.Frequency / 1000f);
                        StatProcessListMA.Add(Convert.ToInt32(StatProcessListMS * 1000f));
                        StatProcessListAVG = StatProcessListMA.Average / 1000f;

                        watchPerfAudio.Reset();
                        watchPerfAudio.Start();
#endif
#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
                        WriteAndroidSamples(dataArray, ticks, numFrames);
#else
                        // Not implemented
                        //GC.TryStartNoGCRegion(240 * 1024 * 1024);
                        int block = 0;
                        float[] reverb_buf = null;
                        float[] chorus_buf = null;



                        //if (ActiveVoices.Count > 0) removed 2.14.1 - cause abrupt reverb/chorus off when all voices are off
                        //{
                        while (block < DspBufferSize)
                        {
                            Array.Clear(left_buf, 0, FLUID_BUFSIZE);
                            Array.Clear(right_buf, 0, FLUID_BUFSIZE);
#if MPTK_PRO
                            MPTK_EffectSoundFont.PrepareBufferEffect(out reverb_buf, out chorus_buf);
#endif
                            WriteAllSamples(ticks, reverb_buf, chorus_buf);
                            //Debug.Log("   block:" + block + " j start:" + ((block + 0) * 2) + " j end:" + ((block + FLUID_BUFSIZE-1) * 2) + " data.Length:" + data.Length );
#if MPTK_PRO
                            MPTK_EffectSoundFont.ProcessEffect(reverb_buf, chorus_buf, left_buf, right_buf);
#endif

#if DEBUG_TRY_CATCH_AUDIODATA
                                try
                                {
#endif
                            for (int i = 0; i < FLUID_BUFSIZE; i++)
                            {
                                int j = (block + i) << 1;
                                data[j] = left_buf[i] * vol;
                                data[j + 1] = right_buf[i] * vol;
                            }
#if DEBUG_TRY_CATCH_AUDIODATA
                                }
                                catch (Exception ex)
                                {
                                    Debug.Log($"DspSize:{DspBufferSize} data:{data.Length} channels:{channels} FLUIDBUF:{FLUID_BUFSIZE} left_buf:{left_buf.Length} {block} ElapsedMilli:{SynthElapsedMilli:F2} Delta:{DeltaTimeAudioCall:F2}");
                                    Debug.LogError(ex);
                                }
#endif

#if DEBUG_AUDIO_DATA_LOOP
                                Debug.Log($"DspSize:{DspBufferSize} data:{data.Length} channels:{channels} FLUIDBUF:{FLUID_BUFSIZE} left_buf:{left_buf.Length} {block} ElapsedMilli:{SynthElapsedMilli:F2} Delta:{DeltaTimeAudioCall:F2}");
#endif


                            block += FLUID_BUFSIZE;
                        }
                        //}

#if MPTK_PRO
                        if (enable3DOrientation)
                            ApplyMPTK3DSound(data);
#endif

                        // Not implemented
                        //GC.EndNoGCRegion();
#endif

                        // Calculate time for processing all samples (watchSynth is reset at start of OnAudioFilterRead)
                        StatAudioFilterReadMS = ((double)watchSynth.ElapsedTicks) / (System.Diagnostics.Stopwatch.Frequency / 1000d);
                        // DeltaTimeAudioCall is normally a constant related to the synth frequency.
                        // But it's not always the case, so DSP loading is an approximation.
                        // At the start of the audio callback but after MIDI processing if Audio Thread is enabled, DeltaTimeAudioCall set to:
                        // watchSynth.ElapsedTicks / (System.Diagnostics.Stopwatch.Frequency / 1000d);
                        // 
                        // Build a load ratio
                        StatDspLoadPCT = (float)(DeltaTimeAudioCall > 0d ? (StatAudioFilterReadMS * 100d) / DeltaTimeAudioCall : 0d);

#if DEBUG_HISTO_SYNTH
                        synthInfo.DeltaOnAudio = Convert.ToInt32(DeltaTimeAudioCall * 100f);
                        synthInfo.TimeSynthProcess = Convert.ToInt32(StatAudioFilterReadMS * 100f);
#endif

#if DEBUG_PERF_AUDIO
                        StatAudioFilterReadMA.Add(Convert.ToInt32(StatAudioFilterReadMS * 1000f));
                        if (StatAudioFilterReadMS > StatAudioFilterReadMAX) StatAudioFilterReadMAX = StatAudioFilterReadMS;
                        if (StatAudioFilterReadMS < StatAudioFilterReadMIN) StatAudioFilterReadMIN = StatAudioFilterReadMS;
                        StatAudioFilterReadAVG = StatAudioFilterReadMA.Average / 1000f;

                        watchPerfAudio.Stop();
                        StatSampleWriteMS = (float)watchPerfAudio.ElapsedTicks / ((float)System.Diagnostics.Stopwatch.Frequency / 1000f);
                        StatSampleWriteMA.Add(Convert.ToInt32(StatSampleWriteMS * 1000f));
                        StatSampleWriteAVG = StatSampleWriteMA.Average / 1000f;

                        StatDspLoadMA.Add(Convert.ToInt32(StatDspLoadPCT * 1000f));
                        if (StatDspLoadPCT > StatDspLoadMAX) StatDspLoadMAX = StatDspLoadPCT;
                        if (StatDspLoadPCT < StatDspLoadMIN) StatDspLoadMIN = StatDspLoadPCT;
                        StatDspLoadAVG = StatDspLoadMA.Average / 1000f;
#endif
                    }
                }
            }
        }

        private void WriteAllSamples(long ticks, float[] reverb_buf, float[] chorus_buf)
        {
            int countPlaying = 0;
            for (int i = 0; i < ActiveVoices.Count; i++)
            {
                fluid_voice voice = ActiveVoices[i];
                if (voice != null) // v2.9.0
                {
                    //Debug.Log("voice.TimeAtStart :" + voice.TimeAtStart + " System.DateTime.UtcNow.Ticks:" + System.DateTime.UtcNow.Ticks);
                    try
                    {
#if DEBUG_PERF_AUDIO
                        if (voice.LatenceTick < 0)
                        {
                            voice.LatenceTick = voice.MptkEvent.LatenceTime;
                            StatSynthLatency.Add(Convert.ToInt32(voice.LatenceTick));
                            StatSynthLatencyLAST = (float)voice.LatenceTick / (float)fluid_voice.Nano100ToMilli;
                            if (StatSynthLatencyLAST > StatSynthLatencyMAX) StatSynthLatencyMAX = StatSynthLatencyLAST;
                            if (StatSynthLatencyLAST < StatSynthLatencyMIN) StatSynthLatencyMIN = StatSynthLatencyLAST;
                            StatSynthLatencyAVG = (float)StatSynthLatency.Average / (float)fluid_voice.Nano100ToMilli;
                        }
#endif
                        voice.fluid_rvoice_write(ticks, left_buf, right_buf, reverb_buf, chorus_buf);

                        if (voice.volenv_section <= fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN)
                            countPlaying++;


                        //if (voice.TimeAtStart <= ticks &&
                        //   (voice.status == fluid_voice_status.FLUID_VOICE_ON || voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED))
                        //{
                        //    voice.fluid_rvoice_write(ticks, left_buf, right_buf, reverb_buf, chorus_buf);
                        //    if (voice.volenv_section <= fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN)
                        //        countPlaying++;
                        //    //Debug.Log($"{voice.status} {voice.volenv_section}"); 
                        //}

                    }
                    catch (Exception ex)
                    {
                        if (VerboseSynth) Debug.LogWarning(ex.Message); // WriteAllSamples
                    }
                }
            }
            // Decrease the risk of reading the value outof the thread when counting is in progress
            MPTK_StatVoiceCountPlaying = countPlaying;

        }

        private void ProcessQueueCommand()
        {
            try
            {
                //if (QueueSynthCommand != null)
                while (QueueSynthCommand.Count > 0)
                {
                    SynthCommand action = QueueSynthCommand.Dequeue();
                    if (action != null)
                    {
                        switch (action.Command)
                        {
                            case SynthCommand.enCmd.StartEvent:
                                MPTK_PlayDirectEvent(action.MidiEvent);
                                break;
                            case SynthCommand.enCmd.StopEvent:
                                StopEvent(action.MidiEvent);
                                break;
                            case SynthCommand.enCmd.ClearAllVoices:
                                ActiveVoices.Clear();
                                FreeVoices.Clear();
                                break;
                            case SynthCommand.enCmd.NoteOffAll:
                                //Debug.Log($"NoteOffAll");
                                for (int i = 0; i < ActiveVoices.Count; i++)
                                {
                                    fluid_voice voice = ActiveVoices[i];
                                    if ((voice.IdSession == action.IdSession || action.IdSession == -1) &&
                                        (voice.status == fluid_voice_status.FLUID_VOICE_ON || voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED))
                                    {
                                        //Debug.Log($"ReleaseAll {voice.IdVoice}/{ActiveVoices.Count} IsSession:{voice.IdSession}");
                                        voice.fluid_rvoice_noteoff(true);
                                    }
                                }
                                break;
                        }
                    }
                    else
                        Debug.LogWarning($"OnAudioFilterRead/ProcessQueueCommand SynthCommand null");
                }
            }
            catch (Exception ex)
            {
                if (VerboseSynth) Debug.LogWarning(ex.Message); // ProcessQueueCommand
            }
        }


        private IEnumerator<float> RoutineManageCacheVoice()
        {
            if (VerboseSynth) Debug.Log($"Start RoutineManageCacheVoice {IdSynth} {ActiveVoices?.Count} {FreeVoices?.Count}");
            while (state == fluid_synth_status.FLUID_SYNTH_PLAYING)
            {
                long ticks = System.DateTime.UtcNow.Ticks;
                ManageCacheVoice(ticks);
                yield return Routine.WaitForSeconds(0.2f);
            }
            if (VerboseSynth) Debug.Log($"End RoutineManageCacheVoice {IdSynth} {ActiveVoices?.Count} {FreeVoices?.Count}");
        }

        private void ManageCacheVoice(long ticks)
        {
            MoveVoiceToFree();
            if (!MPTK_CorePlayer && MPTK_AutoBuffer)
                AutoCleanVoice(ticks);
            MPTK_StatVoiceCountActive = ActiveVoices.Count;
            MPTK_StatVoiceCountFree = FreeVoices.Count;
        }


        private void MoveVoiceToFree()
        {
#if DEBUG_STATUS_STAT
            Array.Clear(StatusStat, 0, StatusStat.Length);
            Array.Clear(EnveloppeStat, 0, EnveloppeStat.Length);
#endif

#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
            // Overload is disabled with Oboe
#else
            bool firstToKill = false;
#endif
            int countActive = ActiveVoices.Count;
            for (int indexVoice = 0; indexVoice < ActiveVoices.Count;)
            {
                try
                {
                    fluid_voice voice = ActiveVoices[indexVoice];
#if DEBUG_STATUS_STAT
                    /*  FLUID_VOICE_CLEAN = 0
                        FLUID_VOICE_ON = 1
                        FLUID_VOICE_SUSTAINED = 2
                        FLUID_VOICE_OFF = 3 */
                    StatusStat[(int)voice.status]++;

                    /*  FLUID_VOICE_ENVDELAY = 0
                        FLUID_VOICE_ENVATTACK = 1
                        FLUID_VOICE_ENVHOLD = 2
                        FLUID_VOICE_ENVDECAY = 3
                        FLUID_VOICE_ENVSUSTAIN = 4
                        FLUID_VOICE_ENVRELEASE = 5
                        FLUID_VOICE_ENVFINISHED = 6 */
                    EnveloppeStat[(int)voice.volenv_section]++;
#endif

#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
                    // Overload is disabled with Oboe
#else
                    if (StatDspLoadPCT > MaxDspLoad)
                    {
                        if (VerboseOverload) voice.DebugOverload($"OverLoad: DeltaTimeAudioCall:{DeltaTimeAudioCall:F2} StatAudioFilterReadMS:{StatAudioFilterReadMS:F2} ActiveVoice:{MPTK_StatVoiceCountActive}");

                        // Check if there is voice which are sustained: MIDI message ControlChange with Sustain (64)
                        if (voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED)
                        {
                            if (VerboseOverload) voice.DebugOverload("Send noteoff");
                            voice.fluid_rvoice_noteoff(true);
                        }

                        if (voice.volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE)
                        {
                            if (VerboseOverload) voice.DebugOverload("Reduce release time");
                            // reduce release time
                            float count = voice.volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].count;
                            count *= (float)DevicePerformance / 100f;
                            //if (indexVoice == 0) Debug.Log(voice.volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].count + " --> " + count);
                            voice.volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].count = (uint)count;
                        }

                        if (!firstToKill && DevicePerformance <= 25) // V2.82 Try to stop one older voice (the first in the list of active voice)
                        {
                            if (voice.volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD ||
                                voice.volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN)
                            {
                                firstToKill = true;
                                if (VerboseOverload) voice.DebugOverload("Send noteoff");
                                voice.fluid_rvoice_noteoff(true);
                            }
                        }
                    }
#endif
                    if (voice.status == fluid_voice_status.FLUID_VOICE_OFF)
                    {
                        if (VerboseVoice) Debug.Log($"Voice {voice.id} - Voice Off, move to FreeVoices");

                        ActiveVoices.RemoveAt(indexVoice);
                        if (MPTK_AutoBuffer)
                            FreeVoices.Add(voice);
                    }
                    else
                    {
                        indexVoice++;
                    }

                }
                catch (Exception ex)
                {
                    if (VerboseSynth) Debug.LogWarning(ex.Message); // MoveVoiceToFree
                }
            }

#if LOG_STATUS_STAT
            if (StatDspLoadPCT > MaxDspLoad)
            {
                Debug.Log(Math.Round(SynthElapsedMilli, 2) +

                    " deltaTimeCore:" + Math.Round(DeltaTimeAudioCall, 2) +
                    " timeToProcessAudio:" + Math.Round(StatAudioFilterReadMS, 2) +
                    " dspLoad:" + Math.Round(StatDspLoadPCT, 2) +
                    " Active:" + countActive +
                    //" Sustained:" + countSustained +
                    " Clean:" + StatusStat[(int)fluid_voice_status.FLUID_VOICE_CLEAN] +
                    " On:" + StatusStat[(int)fluid_voice_status.FLUID_VOICE_ON] +
                    " Sust:" + StatusStat[(int)fluid_voice_status.FLUID_VOICE_SUSTAINED] +
                    " Off:" + StatusStat[(int)fluid_voice_status.FLUID_VOICE_OFF]);
            }
#endif
        }
        public void DebugVoice()
        {
            foreach (fluid_voice v in ActiveVoices)
            {
                Debug.LogFormat("", v.LastTimeWrite);
            }
        }

        private void AutoCleanVoice(long ticks)
        {
            for (int indexVoice = 0; indexVoice < FreeVoices.Count;)
            {
                try
                {
                    if (FreeVoices.Count > MPTK_AutoCleanVoiceLimit || needClearingFreeVoices)
                    {
                        fluid_voice voice = FreeVoices[indexVoice];
                        // Is it an older voice ?
                        //if ((Time.realtimeSinceStartup * 1000d - v.TimeAtStart) > AutoCleanVoiceTime)
                        if (((ticks - voice.TimeAtStart) / fluid_voice.Nano100ToMilli) > MPTK_AutoCleanVoiceTime || needClearingFreeVoices)
                        {
                            if (VerboseVoice) Debug.Log($"AutoCleanVoice:{FreeVoices.Count} id:{voice.id} start:{(ticks - voice.TimeAtStart) / fluid_voice.Nano100ToMilli}");
                            FreeVoices.RemoveAt(indexVoice);
                            if (voice.VoiceAudio != null) Destroy(voice.VoiceAudio.gameObject);
                        }
                        else
                            indexVoice++;
                    }
                    else
                        break;
                }
                catch (Exception ex)
                {
                    if (VerboseSynth) Debug.LogWarning(ex.Message); // AutoCleanVoice
                }
            }
            needClearingFreeVoices = false;
        }

        private void ThreadMidiPlayer()
        {
            if (VerboseSynth) Debug.Log($"START ThreadMidiPlayer IdSynth:{IdSynth} state:{state} ManagedThreadId:{Thread.CurrentThread.ManagedThreadId}");

            if (MPTK_SpatialSynthIndex < 0)
            {
                while (state == fluid_synth_status.FLUID_SYNTH_PLAYING)
                {
                    //Debug.Log($"midiThread.IsAlive:{midiThread.IsAlive} ThreadState:{midiThread.ThreadState}");

                    if (MPTK_ThreadMidiWait > 0)
                        System.Threading.Thread.Sleep(MPTK_ThreadMidiWait);

                    PlayMidi();
                }
            }
            if (VerboseSynth) Debug.Log($"STOP ThreadMidiPlayer IdSynth:{IdSynth} state:{state} ManagedThreadId:{Thread.CurrentThread.ManagedThreadId}");

            try
            {
                midiThread.Abort();
            }
            catch (Exception ex)
            {
                if (VerboseSynth) Debug.LogWarning($"STOP ThreadMidiPlayer IdSynth:{IdSynth} state:{state} ManagedThreadId:{Thread.CurrentThread.ManagedThreadId} {ex}");
            }
            finally { midiThread = null; }
        }

        private void PlayMidi()
        {
            double nowMs = 0d;
            if (AudioThreadMidi)
            {
                //Debug.Log($"{Thread.CurrentThread.ManagedThreadId} {Thread.CurrentThread.Name,15} {Thread.CurrentThread.IsThreadPoolThread} {Thread.CurrentThread.GetApartmentState()} {Thread.CurrentThread.Priority}");
                nowMs = (double)watchMidi.ElapsedTicks / ((double)System.Diagnostics.Stopwatch.Frequency / 1000d);
            }
            else
            {
                //Debug.Log($"{Thread.CurrentThread.ManagedThreadId} {Thread.CurrentThread.Name,15} {Thread.CurrentThread.IsThreadPoolThread} {Thread.CurrentThread.GetApartmentState()} {Thread.CurrentThread.Priority}");
                nowMs = AudioSettings.dspTime * 1000d;
            }

            //Debug.Log($"PlayMidi -  watchMidi:{Math.Round(nowMs, 2):F2} lastTimeMidi:{Math.Round(lastTimeMidi, 2):F2} timeMidiFromStartPlay:{Math.Round(timeMidiFromStartPlay, 2):F2}  StatDeltaThreadMidiMS:{Math.Round(StatDeltaThreadMidiMS, 2):F2}");

            if (lastTimeMidi <= 0d)
            {
                lastTimeMidi = nowMs;
                return;
            }
            double delta = nowMs - lastTimeMidi;
            if (delta < MPTK_ThreadMidiWait) // Why thre was delta > 100d, seems not useful.
            {
                //Debug.Log($"PlayMidi - time cancel - watchMidi:{Math.Round(nowMs, 2)} lastTimeMidi:{Math.Round(lastTimeMidi, 2)} timeMidiFromStartPlay:{Math.Round(timeMidiFromStartPlay, 2)}  delta:{Math.Round(delta, 2)}");
                return;
            }
            StatDeltaThreadMidiMS = delta;
            //Debug.Log($"PlayMidi -  watchMidi:{Math.Round(nowMs, 2):F2} lastTimeMidi:{Math.Round(lastTimeMidi, 2):F2} timeMidiFromStartPlay:{Math.Round(timeMidiFromStartPlay, 2):F2}  StatDeltaThreadMidiMS:{Math.Round(StatDeltaThreadMidiMS, 2):F2}");
            lastTimeMidi = nowMs;

#if DEBUG_PERF_MIDI
            if (StatDeltaThreadMidiMS > 0d && StatDeltaThreadMidiMS < 1000d)
            {
                if (StatDeltaThreadMidiMS > StatDeltaThreadMidiMAX)
                    StatDeltaThreadMidiMAX = StatDeltaThreadMidiMS;
                //else
                //    StatDeltaThreadMidiMAX = (StatDeltaThreadMidiMAX * 999d + StatDeltaThreadMidiMS) / 1000d; // smooth regression

                if (StatDeltaThreadMidiMS < StatDeltaThreadMidiMIN)
                    StatDeltaThreadMidiMIN = StatDeltaThreadMidiMS;
                //else
                //    StatDeltaThreadMidiMIN = (StatDeltaThreadMidiMIN * 999d + StatDeltaThreadMidiMS) / 1000d; // smooth regression

                if (StatDeltaThreadMidiMA == null)
                    StatDeltaThreadMidiMA = new MovingAverage();

                StatDeltaThreadMidiMA.Add(Convert.ToInt32(StatDeltaThreadMidiMS * 1000d));
                StatDeltaThreadMidiAVG = StatDeltaThreadMidiMA.Average / 1000f;

            }
#endif
#if DEBUG_GC
            GcCollectionCout = 0;
            for (int i = 0; i <= GC.MaxGeneration; i++)
                GcCollectionCout += GC.CollectionCount(i);
            AllocatedBytesForCurrentThread = GC.GetAllocatedBytesForCurrentThread();
#endif
            if (midiLoaded != null && midiLoaded.MPTK_MidiEvents != null)
            {
                if (!sequencerPause)
                {
                    if (midiLoaded.ReadyToPlay && !midiLoaded.EndMidiEvent)
                    {
                        lock (this)
                        {
                            timeMidiFromStartPlay += StatDeltaThreadMidiMS;
#if DEBUG_PERF_MIDI
                            watchPerfMidi.Reset();
                            watchPerfMidi.Start();
#endif
                            midiLoaded.calculateTickPlayer((int)timeMidiFromStartPlay);
#if MPTK_PRO
                            // Return false in case of error or stop playing detected
                            if (!CheckBeatEvent((int)timeMidiFromStartPlay))
                                return;

                            if (!midiLoaded.CheckInnerLoop(((MidiFilePlayer)this).MPTK_InnerLoop))
                                return;
#endif
                            // Read midi events until this time
                            List<MPTKEvent> midievents = midiLoaded.fluid_player_callback((int)timeMidiFromStartPlay, IdSession);

#if DEBUG_PERF_MIDI
                            StatReadMidiMS = (float)watchPerfMidi.ElapsedTicks / ((float)System.Diagnostics.Stopwatch.Frequency / 1000f);
                            watchPerfMidi.Reset();
                            watchPerfMidi.Start();
#endif

                            // Play notes read from the midi file
                            if (midievents != null && midievents.Count > 0)
                            {
#if DEBUG_PERF_MIDI
                                // Calculate time between two group of MIDI events processed. There is no MIDI event to play most of the time.
                                StatDeltaTimeMidi = timeMidiFromStartPlay - lasttimeMidiFromStartPlay;
                                //if (StatDeltaTimeMidi < -3 || StatDeltaTimeMidi > 3 || StatProcessMidiMS >= 0.5f)
                                //Debug.Log($"EllapseMidi:{timeMidiFromStartPlay / 1000f:F2} delta:{StatDeltaTimeMidi:F2} PreviousTimeProcess:{StatProcessMidiMS:F2}");
                                lasttimeMidiFromStartPlay = timeMidiFromStartPlay;
#endif

                                //lock (this) // V2.83 - there is already a lock around PlayMidi()
                                {
                                    QueueMidiEvents.Enqueue(midievents);
                                }

#if DEBUG_PERF_MIDI
                                StatEnqueueMidiMS = (float)watchPerfMidi.ElapsedTicks / ((float)System.Diagnostics.Stopwatch.Frequency / 1000f);
                                watchPerfMidi.Reset();
                                watchPerfMidi.Start();
#endif

                                if (MPTK_DirectSendToPlayer)
                                {
                                    foreach (MPTKEvent midiEvent in midievents)
                                    {
                                        try
                                        {
#if MPTK_PRO
                                            if (StartMidiEvent(midiEvent))
                                                if (SpatialSynths != null)
                                                {
                                                    PlaySpatialEvent(midiEvent);
                                                }
                                                else
#endif
                                                    MPTK_PlayDirectEvent(midiEvent, playNoteOff);
                                        }
                                        catch (System.Exception ex)
                                        {
                                            MidiPlayerGlobal.ErrorDetail(ex);
                                        }
                                        //catch (Exception ex)
                                        //{
                                        //    Debug.Log($"ThreadMidiPlayer IdSynth:'{this.IdSynth}' {ex.Message}");
                                        //}
                                    }
#if DEBUG_PERF_MIDI
                                    StatProcessMidiMS = (float)watchPerfMidi.ElapsedTicks / ((float)System.Diagnostics.Stopwatch.Frequency / 1000f);
                                    if (StatProcessMidiMS > StatProcessMidiMAX)
                                        StatProcessMidiMAX = StatProcessMidiMS;
                                    //else
                                    //    StatProcessMidiMAX = (StatProcessMidiMAX * 9f + StatProcessMidiMS) / 10f; // smooth regression 
                                    watchPerfMidi.Reset();
#endif
                                }
                            }
                        }
                    }
                }
                else
                {
                    //Debug.Log(lastTimeMidi + " " + timeToPauseMilliSeconde + " " + pauseMidi.ElapsedMilliseconds);
                    if (timeToPauseMilliSeconde > -1f)
                    {
                        if (pauseMidi.ElapsedMilliseconds > timeToPauseMilliSeconde)
                        {
                            if (timeMidiFromStartPlay <= 0d) watchMidi.Reset(); // V2.82
                            watchMidi.Start();
                            pauseMidi.Stop();
                            //Debug.Log("Pause ended: " + lastTimeMidi + " " + timeToPauseMilliSeconde + " pauseMidi:" + pauseMidi.ElapsedMilliseconds + " watchMidi:" + watchMidi.ElapsedMilliseconds);
                            playPause = false;
                            sequencerPause = false; // V2.82
                        }
                    }
                }
            }
        }

        // @endcond


#if LOG_PERF_NOTEON
        float perf_time_last;
        public void DebugPerf(string info, int mode = 1)
        {
            // Init
            if (mode == 0)
            {
                watchPerfNoteOn.Reset();
                watchPerfNoteOn.Start();
                perfs = new List<string>();
                perf_time_cumul = 0;
                perf_time_last = 0;
            }

            if (perfs != null)
            {
                //Debug.Log(watchPerfNoteOn.ElapsedTicks+ " " + System.Diagnostics.Stopwatch .IsHighResolution+ " " + System.Diagnostics.Stopwatch.Frequency);
                float now = (float)watchPerfNoteOn.ElapsedTicks / ((float)System.Diagnostics.Stopwatch.Frequency / 1000f);
                perf_time_cumul = now;
                float delta = now - perf_time_last;
                perf_time_last = now;
                string perf = string.Format("{0,-30} \t\t delta:{1:F6} ms \t cumul:{2:F6} ms ", info, delta, perf_time_cumul);
                perfs.Add(perf);
            }

            // Close
            if (mode == 2)
            {
                foreach (string perf in perfs)
                    Debug.Log(perf);
                //Debug.Log(perfs.Last());
            }
        }
#endif
    }
}
