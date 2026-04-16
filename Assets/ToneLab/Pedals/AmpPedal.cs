using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class AmpPedalSettings
{
    public float gain_db = 24f;
    public float tone = 0.55f;
    public float presence = 0.48f;
    public float master_db = 0f;
    public float sag = 0.22f;
}

public sealed class AmpPedalProcessor : IToneLabPedalProcessor
{
    private AmpPedalSettings settings = new AmpPedalSettings();
    private ToneLabBiquadFilter[] inputHighPass = Array.Empty<ToneLabBiquadFilter>();
    private ToneLabBiquadFilter[] bodyPeak = Array.Empty<ToneLabBiquadFilter>();
    private ToneLabBiquadFilter[] postLowPass = Array.Empty<ToneLabBiquadFilter>();
    private ToneLabBiquadFilter[] presencePeak = Array.Empty<ToneLabBiquadFilter>();
    private float[] previousInput = Array.Empty<float>();
    private float supplyEnvelope;
    private int preparedSampleRate;
    private int preparedChannels;

    public void Prepare(int sampleRate, int channels)
    {
        preparedSampleRate = Mathf.Max(1, sampleRate);
        preparedChannels = Mathf.Max(1, channels);
        inputHighPass = CreateFilterBank(preparedChannels);
        bodyPeak = CreateFilterBank(preparedChannels);
        postLowPass = CreateFilterBank(preparedChannels);
        presencePeak = CreateFilterBank(preparedChannels);
        previousInput = new float[preparedChannels];
        ReconfigureFilters();
        Reset();
    }

    public void Reset()
    {
        ResetBank(inputHighPass);
        ResetBank(bodyPeak);
        ResetBank(postLowPass);
        ResetBank(presencePeak);
        if (previousInput.Length > 0)
            Array.Clear(previousInput, 0, previousInput.Length);
        supplyEnvelope = 0f;
    }

    public void ApplySettings(object settingsObject)
    {
        settings = settingsObject as AmpPedalSettings ?? new AmpPedalSettings();
        settings.gain_db = Mathf.Clamp(settings.gain_db, 0f, 42f);
        settings.tone = Mathf.Clamp01(settings.tone);
        settings.presence = Mathf.Clamp01(settings.presence);
        settings.master_db = Mathf.Clamp(settings.master_db, -12f, 12f);
        settings.sag = Mathf.Clamp01(settings.sag);
        ReconfigureFilters();
    }

    public void Process(float[] data, int channels, int sampleRate)
    {
        if (data == null || preparedSampleRate <= 0 || preparedChannels != channels)
            return;

        float normalizedGain = Mathf.InverseLerp(0f, 42f, settings.gain_db);
        float baseDrive = Mathf.Lerp(1.15f, 7.0f, normalizedGain);
        float master = ToneLabPedalUtility.DbToLinear(settings.master_db) * Mathf.Lerp(1.0f, 0.58f, normalizedGain);
        float supplyRelease = Mathf.Exp(-1f / Mathf.Max(1f, preparedSampleRate * 0.060f));

        for (int frame = 0; frame < data.Length; frame += channels)
        {
            float detector = 0f;
            for (int channel = 0; channel < channels; channel++)
                detector = Mathf.Max(detector, Mathf.Abs(data[frame + channel]));

            supplyEnvelope = Mathf.Max(detector, (supplyEnvelope * supplyRelease) + (detector * (1f - supplyRelease)));
            float sagCompensation = 1f - (settings.sag * 0.28f * Mathf.Clamp01(supplyEnvelope));
            float stageDrive = Mathf.Max(1.2f, baseDrive * sagCompensation);

            for (int channel = 0; channel < channels; channel++)
            {
                float dry = data[frame + channel];
                float filtered = inputHighPass[channel].Process(dry);

                float interpolated = 0.5f * (previousInput[channel] + filtered);
                float shapedMid = ShapeStage(interpolated, stageDrive, settings.presence);
                float shapedCurrent = ShapeStage(filtered, stageDrive, settings.presence);
                float shaped = 0.5f * (shapedMid + shapedCurrent);
                previousInput[channel] = filtered;

                shaped = bodyPeak[channel].Process(shaped);
                shaped = postLowPass[channel].Process(shaped);
                shaped = presencePeak[channel].Process(shaped);
                data[frame + channel] = Mathf.Clamp(shaped * master, -1f, 1f);
            }
        }
    }

    private void ReconfigureFilters()
    {
        if (preparedSampleRate <= 0 || preparedChannels <= 0)
            return;

        float toneLowPass = Mathf.Lerp(2800f, 6800f, settings.tone);
        float bodyGainDb = Mathf.Lerp(2.2f, -1.0f, settings.tone);
        float presenceGainDb = Mathf.Lerp(-0.5f, 3.2f, settings.presence);

        for (int channel = 0; channel < preparedChannels; channel++)
        {
            inputHighPass[channel].SetHighPass(preparedSampleRate, 60f, 0.707f);
            bodyPeak[channel].SetPeaking(preparedSampleRate, 190f, 0.85f, bodyGainDb);
            postLowPass[channel].SetLowPass(preparedSampleRate, toneLowPass, 0.707f);
            presencePeak[channel].SetPeaking(preparedSampleRate, 3200f, 0.75f, presenceGainDb);
        }
    }

    private static float ShapeStage(float sample, float drive, float presence)
    {
        float preGain = 1.08f + (presence * 0.10f);
        float stage1 = ToneLabPedalUtility.SoftClipAtan(sample * preGain, drive);
        float stage2Drive = 1.35f + (drive * 0.35f);
        float stage2 = ToneLabPedalUtility.SoftClipAtan(stage1 * (1.04f + (presence * 0.12f)), stage2Drive);
        float blend = Mathf.Lerp(stage1, stage2, 0.42f);
        return blend * 0.74f;
    }

    private static ToneLabBiquadFilter[] CreateFilterBank(int channels)
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

public sealed class AmpPedalDescriptor : ToneLabPedalDescriptorBase<AmpPedalSettings, AmpPedalProcessor>
{
    private static readonly IReadOnlyList<ToneLabPedalParameterDefinition> parameterDefinitions =
        new[]
        {
            new ToneLabPedalParameterDefinition("gain_db", "Gain", 0f, 42f, value => $"{value:F1} dB", settingsObject => ((AmpPedalSettings)settingsObject).gain_db, (settingsObject, value) => ((AmpPedalSettings)settingsObject).gain_db = value),
            new ToneLabPedalParameterDefinition("tone", "Tone", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((AmpPedalSettings)settingsObject).tone, (settingsObject, value) => ((AmpPedalSettings)settingsObject).tone = value),
            new ToneLabPedalParameterDefinition("presence", "Presence", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((AmpPedalSettings)settingsObject).presence, (settingsObject, value) => ((AmpPedalSettings)settingsObject).presence = value),
            new ToneLabPedalParameterDefinition("master_db", "Master", -12f, 12f, value => $"{value:F1} dB", settingsObject => ((AmpPedalSettings)settingsObject).master_db, (settingsObject, value) => ((AmpPedalSettings)settingsObject).master_db = value),
            new ToneLabPedalParameterDefinition("sag", "Sag", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((AmpPedalSettings)settingsObject).sag, (settingsObject, value) => ((AmpPedalSettings)settingsObject).sag = value)
        };

    public override UnityToneLabRuntime.ToneLabPedalType PedalType => UnityToneLabRuntime.ToneLabPedalType.Amp;
    public override string DisplayName => "Amp";
    public override string ShortName => "AMP";
    public override string Description => "Preamp and power-stage voicing with dynamic sag, tone shaping, and musical saturation.";
    public override IReadOnlyList<ToneLabPedalParameterDefinition> Parameters => parameterDefinitions;

    protected override ToneLabPedalAppearance CreateAppearance()
    {
        return new ToneLabPedalAppearance
        {
            BodyColor = new Color(0.32f, 0.21f, 0.17f, 1f),
            FaceColor = new Color(0.74f, 0.61f, 0.50f, 1f),
            LabelStripColor = new Color(0.08f, 0.06f, 0.05f, 0.98f),
            TextColor = new Color(1f, 0.96f, 0.92f, 1f),
            SecondaryTextColor = new Color(0.22f, 0.15f, 0.12f, 0.95f),
            EdgeColor = new Color(0.17f, 0.11f, 0.09f, 1f),
            TopEdgeColor = new Color(0.96f, 0.82f, 0.67f, 1f),
            AccentColor = new Color(0.86f, 0.56f, 0.31f, 1f),
            LedOnColor = new Color(1.00f, 0.72f, 0.34f, 1f),
            KnobCount = 4,
            SliderCount = 1,
            DecorationStyle = ToneLabPedalDecorationStyle.Grille
        };
    }
}
