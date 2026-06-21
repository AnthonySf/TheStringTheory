using System;
using System.Collections.Generic;
using UnityEngine;

public enum StringTheoryMetronomeSound
{
    Drums = 0,
    Click = 1,
    Woodblock = 2,
    Hat = 3
}

public sealed class StringTheoryMetronome : MonoBehaviour
{
    private const int SourcePoolSize = 8;
    private const double ScheduleAheadSeconds = 0.18d;
    private const double MinimumSecondsPerBeat = 0.20d;
    private const double MaximumSecondsPerBeat = 4.00d;

    private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
    private readonly AudioSource[] sources = new AudioSource[SourcePoolSize];
    private int sourceCursor;
    private double nextBeatDspTime;
    private double secondsPerBeat = 0.5d;
    private int beatIndex;
    private int beatsPerBar = 4;
    private float volume = 0.78f;
    private StringTheoryMetronomeSound sound = StringTheoryMetronomeSound.Drums;
    private bool running;

    public bool IsRunning => running;
    public double SecondsPerBeat => secondsPerBeat;
    public StringTheoryMetronomeSound Sound => sound;

    public static string GetSoundLabel(StringTheoryMetronomeSound sound)
    {
        switch (sound)
        {
            case StringTheoryMetronomeSound.Click:
                return "Click";
            case StringTheoryMetronomeSound.Woodblock:
                return "Woodblock";
            case StringTheoryMetronomeSound.Hat:
                return "Hi-Hat";
            default:
                return "Drums";
        }
    }

    public static StringTheoryMetronomeSound NormalizeSoundIndex(int index)
    {
        if (Enum.IsDefined(typeof(StringTheoryMetronomeSound), index))
            return (StringTheoryMetronomeSound)index;

        return StringTheoryMetronomeSound.Drums;
    }

    public void StartMetronome(
        double startDspTime,
        double secondsPerBeat,
        StringTheoryMetronomeSound sound,
        int beatsPerBar = 4,
        float volume = 0.78f,
        int initialBeatIndex = 0)
    {
        EnsureSources();
        this.secondsPerBeat = Math.Max(MinimumSecondsPerBeat, Math.Min(MaximumSecondsPerBeat, secondsPerBeat));
        this.sound = sound;
        this.beatsPerBar = Mathf.Clamp(beatsPerBar, 1, 16);
        this.volume = Mathf.Clamp01(volume);
        beatIndex = Math.Max(0, initialBeatIndex);
        nextBeatDspTime = Math.Max(AudioSettings.dspTime + 0.01d, startDspTime);
        running = true;
        ScheduleDueBeats();
    }

    public void Reconfigure(double secondsPerBeat, StringTheoryMetronomeSound sound, int beatsPerBar = 4, float volume = 0.78f)
    {
        this.secondsPerBeat = Math.Max(MinimumSecondsPerBeat, Math.Min(MaximumSecondsPerBeat, secondsPerBeat));
        this.sound = sound;
        this.beatsPerBar = Mathf.Clamp(beatsPerBar, 1, 16);
        this.volume = Mathf.Clamp01(volume);
    }

    public void StopMetronome()
    {
        running = false;
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != null)
                sources[i].Stop();
        }
    }

    private void Update()
    {
        if (running)
            ScheduleDueBeats();
    }

    private void EnsureSources()
    {
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != null)
                continue;

            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.priority = 32;
            sources[i] = source;
        }
    }

    private void ScheduleDueBeats()
    {
        double now = AudioSettings.dspTime;
        while (running && nextBeatDspTime <= now + ScheduleAheadSeconds)
        {
            bool accent = beatsPerBar <= 1 || beatIndex % beatsPerBar == 0;
            AudioSource source = sources[sourceCursor];
            sourceCursor = (sourceCursor + 1) % sources.Length;
            source.clip = GetClip(sound, accent);
            source.volume = accent ? volume : volume * 0.72f;
            source.Stop();
            source.PlayScheduled(nextBeatDspTime);

            nextBeatDspTime += secondsPerBeat;
            beatIndex++;
        }
    }

    private AudioClip GetClip(StringTheoryMetronomeSound sound, bool accent)
    {
        int sampleRate = Mathf.Clamp(AudioSettings.outputSampleRate, 22050, 96000);
        string key = $"{sound}:{accent}:{sampleRate}";
        if (clipCache.TryGetValue(key, out AudioClip cached) && cached != null)
            return cached;

        float duration = sound == StringTheoryMetronomeSound.Drums && accent ? 0.13f : 0.085f;
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * duration));
        float[] samples = new float[sampleCount];
        switch (sound)
        {
            case StringTheoryMetronomeSound.Click:
                FillClick(samples, sampleRate, accent ? 2050f : 1380f, accent ? 0.95f : 0.72f);
                break;
            case StringTheoryMetronomeSound.Woodblock:
                FillWoodblock(samples, sampleRate, accent ? 980f : 760f, accent ? 0.92f : 0.74f);
                break;
            case StringTheoryMetronomeSound.Hat:
                FillNoiseHat(samples, sampleRate, accent ? 0.95f : 0.70f);
                break;
            default:
                if (accent)
                    FillKick(samples, sampleRate, 0.98f);
                else
                    FillNoiseHat(samples, sampleRate, 0.74f);
                break;
        }

        AudioClip clip = AudioClip.Create($"Metronome_{key}", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        clipCache[key] = clip;
        return clip;
    }

    private static void FillClick(float[] samples, int sampleRate, float frequency, float gain)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 65f);
            float transient = i < sampleRate * 0.0025f ? 0.28f : 0f;
            samples[i] = Mathf.Clamp((Mathf.Sin(t * frequency * Mathf.PI * 2f) * envelope * gain) + transient, -1f, 1f);
        }
    }

    private static void FillWoodblock(float[] samples, int sampleRate, float frequency, float gain)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 42f);
            float tone = Mathf.Sign(Mathf.Sin(t * frequency * Mathf.PI * 2f)) * 0.70f;
            float overtone = Mathf.Sin(t * frequency * 1.72f * Mathf.PI * 2f) * 0.30f;
            samples[i] = Mathf.Clamp((tone + overtone) * envelope * gain, -1f, 1f);
        }
    }

    private static void FillKick(float[] samples, int sampleRate, float gain)
    {
        uint seed = 2166136261u;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)sampleRate;
            float pitch = Mathf.Lerp(118f, 52f, Mathf.Clamp01(t / 0.11f));
            float envelope = Mathf.Exp(-t * 26f);
            float body = Mathf.Sin(t * pitch * Mathf.PI * 2f) * envelope;
            float noise = (NextNoise(ref seed) * 2f - 1f) * Mathf.Exp(-t * 90f) * 0.12f;
            samples[i] = Mathf.Clamp((body + noise) * gain, -1f, 1f);
        }
    }

    private static void FillNoiseHat(float[] samples, int sampleRate, float gain)
    {
        uint seed = 362436069u;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 58f);
            float noise = (NextNoise(ref seed) * 2f - 1f);
            float shimmer = Mathf.Sin(t * 7800f * Mathf.PI * 2f) * 0.22f;
            samples[i] = Mathf.Clamp((noise * 0.56f + shimmer) * envelope * gain, -1f, 1f);
        }
    }

    private static float NextNoise(ref uint seed)
    {
        seed ^= seed << 13;
        seed ^= seed >> 17;
        seed ^= seed << 5;
        return (seed & 0x00FFFFFF) / 16777215f;
    }

    private void OnDestroy()
    {
        StopMetronome();
        foreach (AudioClip clip in clipCache.Values)
        {
            if (clip != null)
                Destroy(clip);
        }

        clipCache.Clear();
    }
}
