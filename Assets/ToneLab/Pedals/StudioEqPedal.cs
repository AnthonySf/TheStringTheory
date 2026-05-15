using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class StudioEqPedalSettings
{
    public float low_cut_hz = 75f;
    public float low_shelf_db = 0f;
    public float mid_db = 0f;
    public float high_shelf_db = 0f;
    public float high_cut_hz = 7600f;
}

public sealed class StudioEqPedalProcessor : IToneLabPedalProcessor
{
    private StudioEqPedalSettings settings = new StudioEqPedalSettings();
    private ToneLabBiquadFilter[] highPass = Array.Empty<ToneLabBiquadFilter>();
    private ToneLabBiquadFilter[] lowShelf = Array.Empty<ToneLabBiquadFilter>();
    private ToneLabBiquadFilter[] midPeak = Array.Empty<ToneLabBiquadFilter>();
    private ToneLabBiquadFilter[] highShelf = Array.Empty<ToneLabBiquadFilter>();
    private ToneLabBiquadFilter[] lowPass = Array.Empty<ToneLabBiquadFilter>();
    private int preparedSampleRate;
    private int preparedChannels;

    public void Prepare(int sampleRate, int channels)
    {
        preparedSampleRate = Mathf.Max(1, sampleRate);
        preparedChannels = Mathf.Max(1, channels);
        highPass = CreateBank(preparedChannels);
        lowShelf = CreateBank(preparedChannels);
        midPeak = CreateBank(preparedChannels);
        highShelf = CreateBank(preparedChannels);
        lowPass = CreateBank(preparedChannels);
        ReconfigureFilters();
        Reset();
    }

    public void Reset()
    {
        ResetBank(highPass);
        ResetBank(lowShelf);
        ResetBank(midPeak);
        ResetBank(highShelf);
        ResetBank(lowPass);
    }

    public void ApplySettings(object settingsObject)
    {
        settings = settingsObject as StudioEqPedalSettings ?? new StudioEqPedalSettings();
        settings.low_cut_hz = Mathf.Clamp(settings.low_cut_hz, 40f, 180f);
        settings.low_shelf_db = Mathf.Clamp(settings.low_shelf_db, -12f, 12f);
        settings.mid_db = Mathf.Clamp(settings.mid_db, -12f, 12f);
        settings.high_shelf_db = Mathf.Clamp(settings.high_shelf_db, -12f, 12f);
        settings.high_cut_hz = Mathf.Clamp(settings.high_cut_hz, 2600f, 12000f);
        ReconfigureFilters();
    }

    public void Process(float[] data, int channels, int sampleRate)
    {
        if (data == null || preparedSampleRate <= 0 || preparedChannels != channels)
            return;

        for (int frame = 0; frame < data.Length; frame += channels)
        {
            for (int channel = 0; channel < channels; channel++)
            {
                float sample = data[frame + channel];
                sample = highPass[channel].Process(sample);
                sample = lowShelf[channel].Process(sample);
                sample = midPeak[channel].Process(sample);
                sample = highShelf[channel].Process(sample);
                sample = lowPass[channel].Process(sample);
                data[frame + channel] = sample;
            }
        }
    }

    private void ReconfigureFilters()
    {
        if (preparedSampleRate <= 0 || preparedChannels <= 0)
            return;

        for (int channel = 0; channel < preparedChannels; channel++)
        {
            highPass[channel].SetHighPass(preparedSampleRate, settings.low_cut_hz, 0.707f);
            lowShelf[channel].SetLowShelf(preparedSampleRate, 180f, 0.707f, settings.low_shelf_db);
            midPeak[channel].SetPeaking(preparedSampleRate, 850f, 0.85f, settings.mid_db);
            highShelf[channel].SetHighShelf(preparedSampleRate, 3000f, 0.707f, settings.high_shelf_db);
            lowPass[channel].SetLowPass(preparedSampleRate, settings.high_cut_hz, 0.707f);
        }
    }

    private static ToneLabBiquadFilter[] CreateBank(int channels)
    {
        ToneLabBiquadFilter[] bank = new ToneLabBiquadFilter[channels];
        for (int i = 0; i < channels; i++)
            bank[i] = new ToneLabBiquadFilter();
        return bank;
    }

    private static void ResetBank(ToneLabBiquadFilter[] bank)
    {
        if (bank == null)
            return;

        for (int i = 0; i < bank.Length; i++)
            bank[i]?.Reset();
    }
}

public sealed class StudioEqPedalDescriptor : ToneLabPedalDescriptorBase<StudioEqPedalSettings, StudioEqPedalProcessor>
{
    private static readonly IReadOnlyList<ToneLabPedalParameterDefinition> parameterDefinitions =
        new[]
        {
            new ToneLabPedalParameterDefinition("low_cut_hz", "Low Cut", 40f, 180f, value => $"{value:F0} Hz", settingsObject => ((StudioEqPedalSettings)settingsObject).low_cut_hz, (settingsObject, value) => ((StudioEqPedalSettings)settingsObject).low_cut_hz = value),
            new ToneLabPedalParameterDefinition("low_shelf_db", "Low Shelf", -12f, 12f, value => $"{value:F1} dB", settingsObject => ((StudioEqPedalSettings)settingsObject).low_shelf_db, (settingsObject, value) => ((StudioEqPedalSettings)settingsObject).low_shelf_db = value),
            new ToneLabPedalParameterDefinition("mid_db", "Mid", -12f, 12f, value => $"{value:F1} dB", settingsObject => ((StudioEqPedalSettings)settingsObject).mid_db, (settingsObject, value) => ((StudioEqPedalSettings)settingsObject).mid_db = value),
            new ToneLabPedalParameterDefinition("high_shelf_db", "High Shelf", -12f, 12f, value => $"{value:F1} dB", settingsObject => ((StudioEqPedalSettings)settingsObject).high_shelf_db, (settingsObject, value) => ((StudioEqPedalSettings)settingsObject).high_shelf_db = value),
            new ToneLabPedalParameterDefinition("high_cut_hz", "High Cut", 2600f, 12000f, value => $"{value:F0} Hz", settingsObject => ((StudioEqPedalSettings)settingsObject).high_cut_hz, (settingsObject, value) => ((StudioEqPedalSettings)settingsObject).high_cut_hz = value)
        };

    public override UnityToneLabRuntime.ToneLabPedalType PedalType => UnityToneLabRuntime.ToneLabPedalType.StudioEq;
    public override string DisplayName => "Studio EQ";
    public override string ShortName => "EQ";
    public override string Description => "Final corrective shaping for low-cut, high-cut, and shelf balance before the room effects.";
    public override IReadOnlyList<ToneLabPedalParameterDefinition> Parameters => parameterDefinitions;

    protected override ToneLabPedalAppearance CreateAppearance()
    {
        return new ToneLabPedalAppearance
        {
            BodyColor = new Color(0.70f, 0.34f, 0.21f, 1f),
            FaceColor = new Color(0.95f, 0.66f, 0.38f, 1f),
            LabelStripColor = new Color(0.18f, 0.10f, 0.07f, 0.98f),
            TextColor = new Color(1f, 0.95f, 0.91f, 1f),
            SecondaryTextColor = new Color(0.32f, 0.16f, 0.11f, 0.95f),
            EdgeColor = new Color(0.42f, 0.18f, 0.11f, 1f),
            TopEdgeColor = new Color(1f, 0.83f, 0.58f, 1f),
            AccentColor = new Color(0.95f, 0.49f, 0.21f, 1f),
            LedOnColor = new Color(1f, 0.72f, 0.40f, 1f),
            KnobCount = 0,
            SliderCount = 5,
            DecorationStyle = ToneLabPedalDecorationStyle.SweepBars
        };
    }
}
