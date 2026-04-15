//#define MPTK_PRO
#if UNITY_EDITOR
using System;
using UnityEngine;

namespace MidiPlayerTK
{
    public class MidiEditorLib
    {
        public MidiFileEditorPlayer MidiPlayer;
        private GameObject goSequencer;
        private string nameComponent;
        private bool logSoundFontLoaded;
        private bool logDebug;
        DateTime startLoad;

        public MidiEditorLib(string _nameComponent = null, bool _logSoundFontLoaded = false, bool _logDebug = false)
        {
            nameComponent = _nameComponent != null ? _nameComponent : "MidiSequencerEditor";
            logSoundFontLoaded = _logSoundFontLoaded;
            logDebug = _logDebug;
            //Debug.Log(">>> Awake MidiEditorWindow ..." + nameSequencer + " Application.isPlaying:" + Application.isPlaying);
            LoadPlayer();
        }

        // Seems AudioSource stop playing sometime in editor mode
        public void PlayAudioSource()
        {
            //Debug.Log($"PlayAudioSource isPlaying:{MidiPlayer.CoreAudioSource.isPlaying}");
            if (!MidiPlayer.CoreAudioSource.isPlaying)
                MidiPlayer.CoreAudioSource.Play();
        }
        private void LoadPlayer()
        {
            startLoad = DateTime.Now;
            if (logDebug) Debug.Log(">>> Load Editor Player ..." + nameComponent + " Application.isPlaying:" + Application.isPlaying);

            GameObject oldGo = GameObject.Find(nameComponent);
            if (oldGo != null)
            {
                if (logDebug) Debug.Log($"{(DateTime.Now - startLoad).TotalSeconds:F3} Delete previous " + nameComponent);
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(oldGo);
                else
                    UnityEngine.Object.DestroyImmediate(oldGo, true);
            }

            goSequencer = new GameObject();
            goSequencer.name = nameComponent;
            goSequencer.hideFlags = HideFlags.DontSave;

            MidiPlayer = goSequencer.AddComponent<MidiFileEditorPlayer>();
            MidiPlayer.CoreAudioSource.enabled = true;
            //MidiPlayer.VerboseSynth = true;
            //MidiPlayer.MPTK_IndexSynthBuffSize = 3;

            MidiPlayerGlobal midiPlayerGlobal;
            GameObject goMidiGlobal = GameObject.Find("MidiPlayerGlobal");
            if (goMidiGlobal == null)
            {
                if (logDebug) Debug.Log($"{(DateTime.Now - startLoad).TotalSeconds:F3} Not found MidiPlayerGlobal");
                if (logDebug) Debug.Log("     ... create a Midi Global");
                GameObject objectMidiGlobal = new GameObject();
                objectMidiGlobal.hideFlags = HideFlags.DontSave;
                objectMidiGlobal.name = "MidiPlayerGlobal";
                midiPlayerGlobal = objectMidiGlobal.gameObject.AddComponent<MidiPlayerGlobal>();
            }
            else
            {
                if (logDebug)
                {
                    Debug.Log($"{(DateTime.Now - startLoad).TotalSeconds:F3} Found MidiPlayerGlobal");
                    Transform parent = goMidiGlobal.transform.parent;
                    if (parent == null)
                        Debug.Log("     ... parent is null");
                    else
                        Debug.Log("     ... parent is " + parent.name);
                }
                midiPlayerGlobal = goMidiGlobal.GetComponent<MidiPlayerGlobal>();
                if (logDebug && midiPlayerGlobal == null)
                    Debug.LogWarning("     ... midiPlayerGlobal is null");
            }
            MidiPlayerGlobal.VerboseGlobal = logDebug;

            MidiPlayerGlobal.InitPath();
            ToolsEditor.LoadMidiSet();
            if (logDebug) Debug.Log($"{(DateTime.Now - startLoad).TotalSeconds:F3} Load MidiPlayerGlobal instance");
            midiPlayerGlobal.InitInstance();
            if (logSoundFontLoaded)
                MidiPlayerGlobal.OnEventPresetLoaded.AddListener(SoundFontIsReadyEvent);
            //MidiPlayerGlobal.MPTK_LoadLiveSF("file://" + MidiPlayerGlobal.CurrentMidiSet.ActiveSounFontInfo.SF2Path, -1, -1, false);
            if (logDebug) Debug.Log($"{(DateTime.Now - startLoad).TotalSeconds:F3} LoadCurrentSF");
            MidiPlayerGlobal.LoadCurrentSF();

            MidiPlayer.MPTK_CorePlayer = true;
#if MPTK_PRO
            MidiPlayer.MPTK_InnerLoop = new MPTKInnerLoop();
#endif

            // Effect instance not yet available
            //MidiPlayer.MPTK_EffectSoundFont.EnableChorus = false;
            //MidiPlayer.MPTK_EffectSoundFont.EnableFilter = false;
            //MidiPlayer.MPTK_EffectSoundFont.EnableReverb = false;
            //MidiPlayer.MPTK_EffectUnity.EnableChorus = false;
            //MidiPlayer.MPTK_EffectUnity.EnableReverb = false;

            //MidiPlayer.MPTK_LogEvents = true;
            //MidiPlayer.MPTK_LogWave = true;
            MidiPlayer.MPTK_EnablePresetDrum = false; // was true version < 2.14.1
            MidiPlayer.MPTK_StartPlayAtFirstNote = true;
            MidiPlayer.MPTK_DirectSendToPlayer = true;
            MidiPlayer.MPTK_AutoCleanVoiceLimit = 100;
            MidiPlayer.MPTK_AutoCleanVoiceTime = 10000;
            // Need an audio clip in editor mode to avoid message "Only custom filters can be played. Please add a custom filter or an audioclip to the audiosource"
            if (logDebug) Debug.Log($"{(DateTime.Now - startLoad).TotalSeconds:F3} CoreAudioSource");
            MidiPlayer.CoreAudioSource.clip = Create();
            MidiPlayer.CoreAudioSource.Play();

            if (logDebug) Debug.Log($"{(DateTime.Now - startLoad).TotalSeconds:F3} <<< Load Editor Player ..." + nameComponent);
        }

        public void SoundFontIsReadyEvent()
        {
            Debug.LogFormat("Loaded SF '{0}', MPTK is ready to play", MidiPlayerGlobal.ImSFCurrent.SoundFontName);
            Debug.Log("   Time To Load SoundFont: " + Math.Round(MidiPlayerGlobal.MPTK_TimeToLoadSoundFont.TotalSeconds, 3).ToString() + " second");
            Debug.Log("   Time To Load Samples: " + Math.Round(MidiPlayerGlobal.MPTK_TimeToLoadWave.TotalSeconds, 3).ToString() + " second");
            Debug.Log("   Presets Loaded: " + MidiPlayerGlobal.MPTK_CountPresetLoaded);
            Debug.Log("   Samples Loaded: " + MidiPlayerGlobal.MPTK_CountWaveLoaded);
        }

        // create a short empty clip (or a sin wave for testing purpose)
        private AudioClip Create()
        {
            int sampleRate = 44100;
            int sampleCount = 10;
            int sampleChannel = 1;
            //float frequency = 440;
            AudioClip myClip = AudioClip.Create("blank", sampleCount, sampleChannel, sampleRate, false);
            float[] samples = new float[sampleCount * sampleChannel];
            for (int i = 0; i < samples.Length; ++i)
            {
                samples[i] = 0f; // Mathf.Sin(2 * Mathf.PI * frequency * i / samplerate);
            }
            myClip.SetData(samples, 0);
            return myClip;
        }

        public void DestroyMidiObject()
        {
            if (logDebug) Debug.Log(">>> DestroyMidiObject ... Application.isPlaying:" + Application.isPlaying);

            if (goSequencer != null)
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(goSequencer);
                else
                    UnityEngine.Object.DestroyImmediate(goSequencer, true);
            MidiPlayer = null;
            goSequencer = null;

            GameObject instanceHome = GameObject.Find(Routine.InstanceName);
            //Debug.Log("instanceHome: " + instanceHome ?? "null");
            if (instanceHome != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(instanceHome);
                else
                    UnityEngine.Object.DestroyImmediate(instanceHome, true);
            }

            if (logDebug) Debug.Log("<<< DestroyMidiObject ... " + Application.isPlaying);
        }
    }
}
#endif
