using System;
using System.Collections.Generic;
using MidiPlayerTK;
using UnityEngine;

public enum StringTheoryChordPreviewInstrument
{
    ElectricGuitar = 0,
    AcousticGuitar = 1,
    CleanGuitar = 2,
    Piano = 3,
    SynthPad = 4
}

public sealed class StringTheoryChordAudioPlayer : IDisposable
{
    private const int PreviewChannel = 2;
    private const int DefaultBank = -1;
    private readonly List<MPTKEvent> eventBuffer = new List<MPTKEvent>(8);
    private GameObject synthRoot;
    private MidiStreamPlayer midiStreamPlayer;
    private StringTheoryChordPreviewInstrument currentInstrument = (StringTheoryChordPreviewInstrument)(-1);
    private bool subscribedToPresetLoaded;

    public bool IsReady => midiStreamPlayer != null && midiStreamPlayer.MPTK_SoundFont.IsReady;

    public static string GetInstrumentLabel(StringTheoryChordPreviewInstrument instrument)
    {
        switch (instrument)
        {
            case StringTheoryChordPreviewInstrument.AcousticGuitar:
                return "Acoustic";
            case StringTheoryChordPreviewInstrument.CleanGuitar:
                return "Clean Guitar";
            case StringTheoryChordPreviewInstrument.Piano:
                return "Piano";
            case StringTheoryChordPreviewInstrument.SynthPad:
                return "Synth Pad";
            default:
                return "Electric";
        }
    }

    public static StringTheoryChordPreviewInstrument NormalizeInstrumentIndex(int index)
    {
        if (Enum.IsDefined(typeof(StringTheoryChordPreviewInstrument), index))
            return (StringTheoryChordPreviewInstrument)index;

        return StringTheoryChordPreviewInstrument.ElectricGuitar;
    }

    public void EnsureInitialized(Transform parent)
    {
        if (midiStreamPlayer != null)
            return;

        if (MidiPlayerGlobal.Instance == null)
        {
            GameObject globalRoot = new GameObject("MidiPlayerGlobal");
            if (parent != null)
                globalRoot.transform.SetParent(parent, false);
            globalRoot.AddComponent<MidiPlayerGlobal>();
        }

        synthRoot = new GameObject("StringTheoryChordAudioPlayer");
        if (parent != null)
            synthRoot.transform.SetParent(parent, false);

        midiStreamPlayer = synthRoot.AddComponent<MidiStreamPlayer>();
        midiStreamPlayer.MPTK_CorePlayer = true;
        midiStreamPlayer.MPTK_DirectSendToPlayer = true;
        midiStreamPlayer.MPTK_EnablePresetDrum = true;
        midiStreamPlayer.MPTK_ApplyRealTimeModulator = true;
        midiStreamPlayer.MPTK_LogEvents = false;
        midiStreamPlayer.MPTK_Volume = 0.82f;
        if (midiStreamPlayer.CoreAudioSource != null)
            midiStreamPlayer.CoreAudioSource.enabled = true;
        midiStreamPlayer.MPTK_StartMidiStream();
        midiStreamPlayer.MPTK_StartSynth();
        if (midiStreamPlayer.CoreAudioSource != null && !midiStreamPlayer.CoreAudioSource.isPlaying)
            midiStreamPlayer.CoreAudioSource.Play();

        SubscribeToPresetLoaded();
    }

    public void PlayChord(
        int[] midiNotes,
        StringTheoryChordPreviewInstrument instrument,
        float durationSeconds = 0.82f,
        int velocity = 96)
    {
        if (midiNotes == null || midiNotes.Length == 0 || midiStreamPlayer == null || !IsReady)
            return;

        ApplyInstrument(instrument);
        eventBuffer.Clear();
        int durationMs = Mathf.Max(60, Mathf.RoundToInt(Mathf.Clamp(durationSeconds, 0.08f, 3f) * 1000f));
        int clampedVelocity = Mathf.Clamp(velocity, 1, 127);
        for (int i = 0; i < midiNotes.Length; i++)
        {
            int midi = Mathf.Clamp(midiNotes[i], 0, 127);
            eventBuffer.Add(new MPTKEvent
            {
                Command = MPTKCommand.NoteOn,
                Channel = PreviewChannel,
                Value = midi,
                Velocity = clampedVelocity,
                Delay = 0,
                Duration = durationMs
            });
        }

        midiStreamPlayer.MPTK_PlayEvent(eventBuffer);
    }

    public void StopImmediately()
    {
        if (midiStreamPlayer != null && synthRoot != null && synthRoot.activeInHierarchy && Application.isPlaying)
            midiStreamPlayer.MPTK_ClearAllSound(false);
    }

    public void Dispose()
    {
        UnsubscribeFromPresetLoaded();
        StopImmediately();
        if (synthRoot != null)
            UnityEngine.Object.Destroy(synthRoot);

        synthRoot = null;
        midiStreamPlayer = null;
    }

    private void ApplyInstrument(StringTheoryChordPreviewInstrument instrument)
    {
        if (midiStreamPlayer == null || currentInstrument == instrument)
            return;

        int bank = ResolveBank();
        int preset = ResolvePreset(instrument);
        midiStreamPlayer.MPTK_Channels[PreviewChannel].ForcedBank = bank;
        midiStreamPlayer.MPTK_Channels[PreviewChannel].BankNum = bank;
        midiStreamPlayer.MPTK_Channels[PreviewChannel].ForcedPreset = preset;
        midiStreamPlayer.MPTK_Channels[PreviewChannel].PresetNum = preset;
        currentInstrument = instrument;
    }

    private static int ResolvePreset(StringTheoryChordPreviewInstrument instrument)
    {
        switch (instrument)
        {
            case StringTheoryChordPreviewInstrument.AcousticGuitar:
                return 25; // Acoustic Guitar (steel)
            case StringTheoryChordPreviewInstrument.CleanGuitar:
                return 27; // Electric Guitar (clean)
            case StringTheoryChordPreviewInstrument.Piano:
                return 0; // Acoustic Grand Piano
            case StringTheoryChordPreviewInstrument.SynthPad:
                return 88; // Pad 1
            default:
                return 29; // Overdriven Guitar
        }
    }

    private static int ResolveBank()
    {
        if (MidiPlayerGlobal.ImSFCurrent == null)
            return 0;

        return DefaultBank >= 0 ? DefaultBank : MidiPlayerGlobal.ImSFCurrent.DefaultBankNumber;
    }

    private void SubscribeToPresetLoaded()
    {
        if (subscribedToPresetLoaded || MidiPlayerGlobal.OnEventPresetLoaded == null)
            return;

        MidiPlayerGlobal.OnEventPresetLoaded.AddListener(HandlePresetLoaded);
        subscribedToPresetLoaded = true;
    }

    private void UnsubscribeFromPresetLoaded()
    {
        if (!subscribedToPresetLoaded || MidiPlayerGlobal.OnEventPresetLoaded == null)
            return;

        MidiPlayerGlobal.OnEventPresetLoaded.RemoveListener(HandlePresetLoaded);
        subscribedToPresetLoaded = false;
    }

    private void HandlePresetLoaded()
    {
        currentInstrument = (StringTheoryChordPreviewInstrument)(-1);
    }
}
