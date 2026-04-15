
#if UNITY_ANDROID && UNITY_OBOE
using Oboe.Stream;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;


namespace MidiPlayerTK
{
    /// @ingroup runtime_debug_tools
    public class SynthInfo
    {
        private StringBuilder logSynthInfo;

#if DEBUG_HISTO_SYNTH
        class AudioHistoric
        {
            public int DeltaOnAudio;
            public int SizeBuffOnAudio;
            public int TimeSynthProcess;
        }
        public int SizeBuffOnAudio { set { audioHistoric[histoCurrent].SizeBuffOnAudio = value; } }
        public int DeltaOnAudio { set { audioHistoric[histoCurrent].DeltaOnAudio = value; } }
        public int TimeSynthProcess { set { audioHistoric[histoCurrent].TimeSynthProcess = value; } }

#if DEBUG_HISTO_SYNTH
        int MaxLengthHisto;
        AudioHistoric[] audioHistoric;
        int histoCurrent;
        int minDeltaOnAudio;
        int maxDeltaOnAudio;
        int minSizeBuff;
        int maxSizeBuff;
        int countInf64;
        int countNot64;
#endif

#if UNITY_ANDROID && UNITY_OBOE
        int minOboeLatency;
        int maxOboeLatency;
#endif
        public void NextHistoPosition()
        {
            if (++histoCurrent >= audioHistoric.Length) histoCurrent = 0;
            if (audioHistoric[histoCurrent] == null) audioHistoric[histoCurrent] = new AudioHistoric();
        }

        public void ClearHistoMinMax()
        {
#if DEBUG_HISTO_SYNTH
            minDeltaOnAudio = 99999;
            maxDeltaOnAudio = 0;
            minSizeBuff = 99999;
            maxSizeBuff = 0;
#endif

#if UNITY_ANDROID && UNITY_OBOE
            minOboeLatency = 99999;
            maxOboeLatency = 0;
#endif

        }
#endif

        public SynthInfo()
        {
            logSynthInfo = new StringBuilder(256);

#if DEBUG_HISTO_SYNTH
            histoCurrent = 0;
            MaxLengthHisto = 21;
            audioHistoric = new AudioHistoric[MaxLengthHisto];
            ClearHistoMinMax();
#endif
        }

        /// <summary>
        /// Builds a string with performance and information about the MIDI reader and the MIDI synthesizer.
        /// </summary>
        /// <param name="synth"></param>
        /// <returns>Contains all results</returns>
        public StringBuilder MPTK_BuildInfoSynth(MidiSynth synth)
        {
            try
            {
                if (synth != null)
                {
                    logSynthInfo.Clear();
                    logSynthInfo.Append($"<b>Maestro Mode</b> {(synth.MPTK_CorePlayer ? "'Core'" : "'AudioSource'")}");
                    logSynthInfo.Append($" '{(synth.AudioEngine != null ? synth.AudioEngine : "No Audio Engine found")}'");

                    if (synth.AudioThreadMidi)
                        logSynthInfo.Append($" 'MidiOnAudioThread'");
                    else
                        logSynthInfo.Append($" 'MidiMptkThread'   Prio:{synth.ThreadMidiPriorityReal} Wait:{synth.MPTK_ThreadMidiWait}");
                    logSynthInfo.Append($" {synth.OutputRate} Hz Buffer:{synth.DspBufferSize}");
                    logSynthInfo.Append("          <b>Time in milliseconds</b>");

                    logSynthInfo.Append($"\n<b>Process MIDI</b>  Delta:{(int)Math.Round(synth.StatDeltaThreadMidiMS),-4}");
                    // Available only when symbol DEBUG_PERF_MIDI is defined
                    if (synth.StatDeltaThreadMidiMA != null && synth.StatDeltaThreadMidiMA.Count > 0 && synth.StatDeltaThreadMidiMIN < double.MaxValue)
                    {
                        logSynthInfo.Append($"    Mini:{(synth.StatDeltaThreadMidiMIN < double.MaxValue ? (int)Math.Round(synth.StatDeltaThreadMidiMIN) : 0),-4}");
                        logSynthInfo.Append($"   Maxi:{(int)Math.Round(synth.StatDeltaThreadMidiMAX, 2),-4}");
                        logSynthInfo.Append($"   Average:{(int)Math.Round(synth.StatDeltaThreadMidiAVG, 2),-4}");
                        logSynthInfo.Append($"\n              Read:{Math.Round(synth.StatReadMidiMS, 2),-4}");
                        logSynthInfo.Append($"     Treat:{Math.Round(synth.StatProcessMidiMS, 2),-4}");
                        logSynthInfo.Append($"  Maxi:{Math.Round(synth.StatProcessMidiMAX, 2),-4}");

                        // Calculate time between two group of MIDI events processed. There is no MIDI event to play most of the time.
                        logSynthInfo.Append($"   TimeBetweenMidi:{Math.Round(synth.StatDeltaTimeMidi, 0),-4}");
                    }

                    logSynthInfo.Append($"\n<b>Voice Stats</b>   Played:{synth.MPTK_StatVoicePlayed,-4}   Active:{synth.MPTK_StatVoiceCountActive,-3}");

                    logSynthInfo.Append($"  Reused:{synth.MPTK_StatVoiceCountReused,-3}  Ratio:{Mathf.RoundToInt(synth.MPTK_StatVoiceRatioReused),-2}%");
                    logSynthInfo.Append($"   In Cache:{synth.MPTK_StatVoiceCountFree,-3} Free:{synth.FreeVoices?.Count,-3}"); 

#if DEBUG_STATUS_STAT
                    /* FLUID_VOICE_CLEAN FLUID_VOICE_ON FLUID_VOICE_SUSTAINED FLUID_VOICE_OFF*/
                    logSynthInfo.Append($"\n              ");
                    //logSynthInfo.Append($"Clean:{synth.StatusStat[(int)fluid_voice_status.FLUID_VOICE_CLEAN],-2}");
                    logSynthInfo.Append($"On:{synth.StatusStat[(int)fluid_voice_status.FLUID_VOICE_ON],-10}");
                    logSynthInfo.Append($" Sustain:{synth.StatusStat[(int)fluid_voice_status.FLUID_VOICE_SUSTAINED],-2}");
                    //logSynthInfo.Append($" Off:{synth.StatusStat[(int)fluid_voice_status.FLUID_VOICE_OFF],-2}");

                    logSynthInfo.Append($"  Enveloppe:  Delay:{synth.EnveloppeStat[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY],-2}");
                    logSynthInfo.Append($" Attack:{synth.EnveloppeStat[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK],-2}");
                    logSynthInfo.Append($" Hold:{synth.EnveloppeStat[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD],-2}");
                    logSynthInfo.Append($" Dec:{synth.EnveloppeStat[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY],-2}");
                    logSynthInfo.Append($" Sus:{synth.EnveloppeStat[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN],-2}");
                    logSynthInfo.Append($" Rel:{synth.EnveloppeStat[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE],-2}");
                    //logSynthInfo.Append($" Finish:{synth.EnveloppeStat[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED],-2}");
#endif

                    logSynthInfo.Append($"\n<b>Process Synth</b> Delta:{Math.Round(synth.DeltaTimeAudioCall, 2),5}   Time:{Math.Round(synth.StatAudioFilterReadMS, 2),5}");
                    // Available only when symbol DEBUG_PERF_AUDIO is defined 
                    if (synth.StatAudioFilterReadMA != null)
                    {
                        logSynthInfo.Append($"  Mini:{(synth.StatAudioFilterReadMIN < double.MaxValue ? Math.Round(synth.StatAudioFilterReadMIN, 2) : 0),-5}");
                        logSynthInfo.Append($"  Maxi:{Math.Round(synth.StatAudioFilterReadMAX, 2),-5}");
                        logSynthInfo.Append($"  Average:{Math.Round(synth.StatAudioFilterReadAVG, 2),-5}");
                    }


#if UNITY_ANDROID && UNITY_OBOE
                    if (synth.oboeAudioStream != null)
                    {
                        ResultWithValue<double> dLatency = synth.oboeAudioStream.CalculateLatencyMillis();
                        int latency = (int)Math.Round(dLatency.Value, 0);
                        if (latency > maxOboeLatency) maxOboeLatency = latency;
                        if (latency < minOboeLatency) minOboeLatency = latency;
                        logSynthInfo.Append($"\n<b>Info Oboe   </b>                Latency:{latency,-2}");
                        logSynthInfo.Append($"  Mini:{minOboeLatency,-3}");
                        logSynthInfo.Append($"    Maxi:{maxOboeLatency,-3}");
                        int xRun = synth.oboeAudioStream.GetXRunCount().Value;
                        if (xRun < 10)
                            logSynthInfo.Append($"    XRun:{xRun}");
                        else
                            logSynthInfo.Append($"    XRun:<color=red>{xRun}</color>");
                    }
#else
                    logSynthInfo.Append($"\n<b>DSP Load (%)</b>                Load:{((int)Math.Round(synth.StatDspLoadPCT)),-3}");
                    // Available only when symbol DEBUG_PERF_AUDIO is defined
                    if (synth.StatDspLoadMAX != 0f)
                    {
                        logSynthInfo.Append($"    Mini:{(int)Math.Round(synth.StatDspLoadMIN),-3}");
                        logSynthInfo.Append($"    Maxi:{(int)Math.Round(synth.StatDspLoadMAX),-3}");
                        logSynthInfo.Append($"    Average:{(int)Math.Round(synth.StatDspLoadAVG),-3}");
                    }
                    if (synth.StatDspLoadPCT >= 100f)
                        logSynthInfo.Append($"\t<color=red>\tDSP Load over 100%</color>");
                    else if (synth.StatDspLoadPCT >= synth.MaxDspLoad)
                        logSynthInfo.Append($"\t<color=orange>\tDSP Load over {synth.MaxDspLoad}%</color>");
#endif


#if DEBUG_HISTO_SYNTH
                    countNot64 = 0;
                    countInf64 = 0;
                    for (int i = 0; i < MaxLengthHisto; i++)
                    {
                        if (audioHistoric != null && audioHistoric[i] != null)
                        {
                            if (audioHistoric[i].DeltaOnAudio < minDeltaOnAudio) minDeltaOnAudio = audioHistoric[i].DeltaOnAudio;
                            if (audioHistoric[i].DeltaOnAudio > maxDeltaOnAudio) maxDeltaOnAudio = audioHistoric[i].DeltaOnAudio;
                            if (audioHistoric[i].SizeBuffOnAudio < 64) countInf64++;
                            if (audioHistoric[i].SizeBuffOnAudio % 64 != 0) countNot64++;
                            if (audioHistoric[i].SizeBuffOnAudio < minSizeBuff) minSizeBuff = audioHistoric[i].SizeBuffOnAudio;
                            if (audioHistoric[i].SizeBuffOnAudio > maxSizeBuff) maxSizeBuff = audioHistoric[i].SizeBuffOnAudio;
                        }
                    }
                    logSynthInfo.AppendLine($"\n<b>OnAudio Historic</b> BufferSize:[{minSizeBuff:000}-{maxSizeBuff:000}] LowerThan64:{countInf64,-2} NotModulo64:{countNot64,-2} over {MaxLengthHisto} frames");
                    logSynthInfo.AppendLine($"                 DeltaCall: [{minDeltaOnAudio:0000}-{maxDeltaOnAudio:0000}] historic:[BufferSize TimeWithPrevAudioCall TimeForSynthProcess] <b>100=1ms</b>");
                    int countElt = 0;
                    for (int i = 0; i < MaxLengthHisto; i++)
                    {
                        if (audioHistoric != null && audioHistoric[i] != null)
                        {
                            if (countElt++ >= 7) { logSynthInfo.AppendLine(""); countElt = 1; }
                            string info = $"[{audioHistoric[i].SizeBuffOnAudio:000} {audioHistoric[i].DeltaOnAudio:0000} {audioHistoric[i].TimeSynthProcess:0000}]";
                            // Alert if buffer size is not a multiple of 64 or if time to process exceed delta time betweeb each audio call
                            if (audioHistoric[i].SizeBuffOnAudio < 64 || audioHistoric[i].SizeBuffOnAudio % 64 != 0 || audioHistoric[i].TimeSynthProcess >= audioHistoric[i].DeltaOnAudio)
                                logSynthInfo.Append($"<color=orange>{info}</color>");
                            else if (i == histoCurrent)
                                logSynthInfo.Append($"<color=green><b>{info}</b></color>");
                            else
                                logSynthInfo.Append(info);
                        }
                    }
#endif
                    logSynthInfo.AppendLine("");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                logSynthInfo.Append(ex.ToString());
            }
            return logSynthInfo;
        }
    }
}
