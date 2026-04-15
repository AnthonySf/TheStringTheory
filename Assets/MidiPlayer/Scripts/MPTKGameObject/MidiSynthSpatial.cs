//#define MPTK_PRO

using UnityEngine;

#if MPTK_PRO && UNITY_ANDROID && UNITY_OBOE
using Oboe.Stream;
#endif

namespace MidiPlayerTK
{

    /// <summary> 
    /// Base class which contains all the stuff to build a Wave Table Synth.
    /// 
    /// Loads SoundFont and samples, process midi event, play voices, controllers, generators ...\n 
    /// This class is inherited by others class to build these prefabs: MidiStreamPlayer, MidiFilePlayer, MidiInReader.\n
    /// <b>It is not recommended to instanciate directly this class, rather add prefabs to the hierarchy of your scene. 
    /// and use attributes and methods from an instance of them in your script.</b> 
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
        //-------------------------------------------------------------------------
        //
        //              Attenuation on distance and Specialization
        // Distance Attenuation Parameters
        //
        // Maestro uses three parameters to control how an audio source is attenuated with distance.
        // These parameters map directly to Unity’s 3D audio behaviour but are presented in a simpler form.
        // MPTK_MinDistance
        //      Distance at which attenuation begins.
        //      When the listener is closer than this value, the audio plays at full volume(1.0).
        //
        //  MPTK_MaxDistance
        //      Distance at which attenuation reaches its minimum level.
        //      Beyond this distance, the volume remains constant and does not decrease further.
        //
        //  MPTK_MinSoundAttenuation
        //      Minimum volume applied at Max Distance.
        //      The value must be between 0.0 and 1.0.
        //-------------------------------------------------------------------------

        // Applies directional audio effects based on the relative orientation between the sound source and the listener (panning, front/back attenuation and filtering).
        [Header("---------------- MPTK 3D Audio (Pro) ----------------")]
        public bool enable3DOrientation = false;

        [SerializeField][HideInInspector] private bool distanceAttenuation;
        AnimationCurve customCurveAudioSource;
        [SerializeField][HideInInspector] private float minDistance, maxDistance;
        [SerializeField][HideInInspector] private float minSoundAttenuation;

        [Header("Pan Amount")]
        [Range(0f, 1f)] public float panAmount = 1f;

        [Tooltip("0 = Side stays neutral, 1 = Side follows the pan as much as the Mid. Recommended: 0.4")]
        [Range(0f, 1f)] public float sideFollow = 0.4f;

        [Header("Behind Attenuation Gain")]
        [Range(0.3f, 1f)] public float behindMinGain = 0.8f;

        [Range(0, 2)] public int cutoffPoleCount = 1;

        [Header("Front Low-Pass Cutoff (Hz)")]
        [Range(2000f, 20000f)] public float cutoffFrontHz = 16000f;

        [Header("Behind Low-Pass Cutoff (Hz)")]
        [Range(200f, 12000f)] public float cutoffBehindHz = 3000f;

        // --- Front / back transition tuning ---
        [Header("Front / Back transition")]
        [Range(0f, 0.5f)]
        public float frontDeadZone = 0.15f;   // Angular dead zone around the frontal plane (prevents audible switching)

        [Range(1f, 20f)]
        public float behindSmoothSpeed = 8f;  // Temporal smoothing speed (~0.1?0.15 s is a good range)

        // --- Response curves ---
        [Header("Front / Back response curves")]
        [Range(0.5f, 4f)]
        public float behindGainCurve = 1.2f;  // Gain reacts earlier and more gently

        [Range(0.5f, 6f)]
        public float behindLPFCurve = 2.6f;   // Low-pass reacts later and more progressively

        /// @ingroup synth_spatial_audio
        /// @name 3D Orientation and Attenuation
        /// @brief Real-time spatial audio processing based on listener orientation.
        /// @details
        /// These parameters control how the synthesizer audio reacts to the
        /// position and orientation of the AudioListener.
        ///
        /// Features include:
        /// - stereo panning based on listener direction
        /// - front/back filtering
        /// - distance attenuation
        ///
        /// The goal is to simulate directional sound perception and improve
        /// spatial immersion when MIDI instruments are placed in a 3D scene.

        /// @{


        /// <summary>@brief
        /// Enables orientation-based audio behavior (Pro).
        /// <remarks>
        /// When enabled, MPTK applies directional audio processing based on the
        /// relative orientation between the sound source and the AudioListener.
        ///
        /// This includes:
        /// - Smooth left/right stereo panning
        /// - Front/back volume attenuation
        /// - Subtle filtering when the sound source is behind the listener
        ///
        /// Note:
        /// Enabling orientation-based audio may slightly reduce perceived loudness.
        /// Adjust the Global Volume setting if needed.
        /// 
        /// This feature is implemented internally by MPTK and does not require
        /// any external spatializer plugin.
        ///
        /// The AudioListener used for orientation is defined by <c>MPTK_AudioListener</c>.
        /// By default, the first AudioListener found in the scene is used.
        /// You can override it from the inspector of the <c>MidiPlayerGlobal</c> prefab.
        ///
        /// @version 2.18.0 Pro
        /// </remarks>
        /// </summary>
        [HideInInspector]
        public bool MPTK_Orientation
        {
            get { return enable3DOrientation; }
            set { enable3DOrientation = value; }
        }

        /// <summary>@brief
        /// Returns the signed angle (in degrees) between the sound source and the AudioListener.
        /// 
        /// - 0°   = in front
        /// - +90° = right
        /// - -90° = left
        /// - ±180° = behind
        /// </summary>
        /// <remarks>
        /// Notes:
        /// - Convert to a 0–360° range with:
        ///   <c>MPTK_OrientationToListener &lt; 0f ? MPTK_OrientationToListener + 360f : MPTK_OrientationToListener</c>
        /// - When the source is exactly behind the listener, floating-point precision
        ///   may cause the signed angle to be either +180° or -180°.
        ///   After remapping to [0..360[, both cases consistently yield 180°.
        /// 
        /// @version 2.18.0 Pro
        /// </remarks>
        public float MPTK_OrientationToListener;

        /// <summary>@brief
        /// If true, the MIDI player will be automatically paused 
        /// when the distance from the listener exceeds MPTK_MaxDistance.
        /// @version 2.16.1
        /// </summary>
        [HideInInspector] public bool MPTK_PauseOnMaxDistance = true;
        

        /// <summary>@brief
        /// Enables Unity distance-based attenuation (min/max distance and custom rolloff curve).
        /// When enabled, MPTK configures the underlying AudioSource with:
        /// - minDistance
        /// - maxDistance
        /// - a custom rolloff curve
        /// - spatialBlend set to 3D (1.0)
        ///
        /// This is a convenience helper for Unity's built-in 3D audio attenuation.
        /// It does not manage external spatializer plugins.
        /// 
        /// When disabled, MPTK does not modify the AudioSource.
        /// Configure Unity 3D audio settings directly on the AudioSource if needed,
        /// <b>>especially when using an external spatializer plugin</b.        
        /// </summary>
        /// <remarks>
        /// See setup details:
        /// https://paxstellar.fr/midi-file-player-detailed-view-2/#Foldout-Spatialization-Parameters
        ///
        /// Notes:
        /// - Renamed from <c>MPTK_Spatialize</c> to <c>MPTK_DistanceAttenuation</c> in v2.18.0.
        /// - Can be combined with <c>MPTK_Orientation</c> (Pro) to apply angle-based panning and filtering.
        /// </remarks>
        [HideInInspector]
        public bool MPTK_DistanceAttenuation
        {
            get { return distanceAttenuation; }
            set
            {
                // Avoid call if no change
                if (distanceAttenuation != value /*|| !distanceAttenuationInitialized*/)
                {
                    //distanceAttenuationInitialized = true;
                    distanceAttenuation = value;
                    SetDistanceAttenuation();
                }
            }
        }

        /// <summary>@brief 
        /// When MPTK_DistanceAttenuation is enabled, the volume of the audio source depends on the distance between the audio source and the listener.
        /// Distance at which attenuation begins. When the listener is closer than this value, the audio plays at full volume(1.0). 
        /// </summary>
        [HideInInspector]
        public float MPTK_MinDistance
        {
            get
            {
                return minDistance;
            }
            set
            {
                try
                {
                    if (minDistance != value)
                    {
                        if (value < 0)
                            minDistance = 0;
                        else
                            minDistance = value;
                        if (value > maxDistance)
                            minDistance = maxDistance;
                        SetDistanceAttenuation();
                    }
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }
            }
        }

        /// <summary>@brief 
        /// When MPTK_DistanceAttenuation is enabled, the volume of the audio source depends on the distance between the audio source and the listener.
        /// Distance at which attenuation reaches its minimum level. Beyond this distance, the volume remains constant and does not decrease further
        /// </summary>
        [HideInInspector]
        public float MPTK_MaxDistance
        {
            get
            {
                return maxDistance;
            }
            set
            {
                try
                {
                    if (maxDistance != value)
                    {
                        if (value < 0)
                            maxDistance = 0;
                        else
                            maxDistance = value;
                        SetDistanceAttenuation();
                    }
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }
            }
        }

        /// <summary>@brief 
        /// If MPTK_DistanceAttenuation is enabled, the volume of the audio source depends on the distance between the audio source and the listener.
        /// Minimum volume applied at Max Distance
        /// </summary>
        [HideInInspector]
        public float MPTK_MinSoundAttenuation
        {
            get
            {
                return minSoundAttenuation;
            }
            set
            {
                try
                {
                    if (minSoundAttenuation != value)
                    {
                        minSoundAttenuation = value;
                        SetDistanceAttenuation();
                    }
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }
            }
        }

        /// @}

        protected void SetDistanceAttenuation()
        {
            if (MPTK_CorePlayer)
            {
                if (CoreAudioSource != null)
                {
                    ApplyDistanceAttenuationToAudioSource(CoreAudioSource);
#if MPTK_PRO
                    if (this is MidiSpatializer)
                        ApplyDistanceAttenuationForSpatializer();
#endif
                }
            }
            else
            {
                if (ActiveVoices != null)
                    for (int i = 0; i < ActiveVoices.Count; i++)
                    {
                        fluid_voice voice = ActiveVoices[i];
                        if (voice.VoiceAudio != null)
                        {
                            ApplyDistanceAttenuationToAudioSource(CoreAudioSource);
                        }
                    }
                if (FreeVoices != null)
                    for (int i = 0; i < FreeVoices.Count; i++)
                    {
                        fluid_voice voice = FreeVoices[i];
                        if (voice.VoiceAudio != null)
                        {
                            ApplyDistanceAttenuationToAudioSource(voice.VoiceAudio.Audiosource);
                        }
                    }
            }
        }

        private void ApplyDistanceAttenuationToAudioSource(AudioSource audioSource)
        {
            if (distanceAttenuation)
            {
                if (VerboseSpatialSynth)
                    Debug.Log($"ApplySpatialToAudioSource Distance:{distanceAttenuation} MaxDistance:{maxDistance:F2} MinAttenuation:{MPTK_MinSoundAttenuation:F2} CorePlayer:{MPTK_CorePlayer} {CoreAudioSource?.name}");

                customCurveAudioSource = new AnimationCurve(new Keyframe(minDistance, 1), new Keyframe(maxDistance, minSoundAttenuation));
                
                // Not working - remove 2.17.1
                //else
                //{
                //    Keyframe[] keys = customCurveAudioSource.keys;
                //    keys[0].time = minDistance;
                //    keys[1].time = maxDistance;
                //    keys[1].value = minSoundAttenuation;
                //    customCurveAudioSource.keys = keys;
                //}
                
                audioSource.rolloffMode = AudioRolloffMode.Custom;
                audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, customCurveAudioSource);
                audioSource.minDistance = minDistance;
                audioSource.maxDistance = maxDistance;
                audioSource.spatialBlend = 1f;
                // Disabled version 2.18.2 - related to the spatializer plugin selected in Project not for classical attenuation 3D sound.
                // audioSource.spatialize = true;
                // audioSource.spatializePostEffects = true;
                //audioSource.loop = true;
                //audioSource.volume = 1f;
                //if (!audioSource.isPlaying)
                //    audioSource.Play();
            }
            // Disabled version 2.18.2
            //else
            //{
            //    audioSource.spatialBlend = 0f;
            //    audioSource.spatialize = false;
            //    audioSource.spatializePostEffects = false;
            //}
        }
        // create a short empty clip
        private AudioClip CreateEmptyClip()
        {
            int sampleRate = 44100;
            int sampleCount = 10;
            int sampleChannel = 1;
            AudioClip myClip = AudioClip.Create("blank", sampleCount, sampleChannel, sampleRate, false);
            float[] samples = new float[sampleCount * sampleChannel];
            for (int i = 0; i < samples.Length; ++i)
            {
                samples[i] = 0f;
            }
            myClip.SetData(samples, 0);
            return myClip;
        }
    }
}
