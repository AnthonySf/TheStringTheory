using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CabSimPedalSettings
{
    public float thump = 0.55f;
    public float presence = 0.42f;
    public float air = 0.38f;
    public float mix = 1f;
}

public sealed class CabSimPedalProcessor : IToneLabPedalProcessor
{
    private const int ImpulseLength = 160;

    private CabSimPedalSettings settings = new CabSimPedalSettings();
    private ToneLabFirConvolver[] convolvers = Array.Empty<ToneLabFirConvolver>();
    private float[] impulseResponse = Array.Empty<float>();
    private int preparedSampleRate;
    private int preparedChannels;

    public void Prepare(int sampleRate, int channels)
    {
        preparedSampleRate = Mathf.Max(1, sampleRate);
        preparedChannels = Mathf.Max(1, channels);
        convolvers = new ToneLabFirConvolver[preparedChannels];
        for (int i = 0; i < preparedChannels; i++)
            convolvers[i] = new ToneLabFirConvolver();

        RebuildImpulse();
        Reset();
    }

    public void Reset()
    {
        if (convolvers == null)
            return;

        for (int i = 0; i < convolvers.Length; i++)
            convolvers[i]?.Reset();
    }

    public void ApplySettings(object settingsObject)
    {
        settings = settingsObject as CabSimPedalSettings ?? new CabSimPedalSettings();
        settings.thump = Mathf.Clamp01(settings.thump);
        settings.presence = Mathf.Clamp01(settings.presence);
        settings.air = Mathf.Clamp01(settings.air);
        settings.mix = Mathf.Clamp01(settings.mix);
        RebuildImpulse();
    }

    public void Process(float[] data, int channels, int sampleRate)
    {
        if (data == null || preparedSampleRate <= 0 || preparedChannels != channels || convolvers == null)
            return;

        for (int frame = 0; frame < data.Length; frame += channels)
        {
            for (int channel = 0; channel < channels; channel++)
            {
                float dry = data[frame + channel];
                float wet = convolvers[channel].Process(dry);
                data[frame + channel] = (dry * (1f - settings.mix)) + (wet * settings.mix);
            }
        }
    }

    private void RebuildImpulse()
    {
        if (preparedSampleRate <= 0 || convolvers == null || convolvers.Length == 0)
            return;

        impulseResponse = BuildSpeakerImpulse(preparedSampleRate, settings);
        for (int i = 0; i < convolvers.Length; i++)
            convolvers[i].SetKernel(impulseResponse);
    }

    private static float[] BuildSpeakerImpulse(int sampleRate, CabSimPedalSettings settings)
    {
        float[] kernel = new float[ImpulseLength];
        ToneLabBiquadFilter highPass = new ToneLabBiquadFilter();
        ToneLabBiquadFilter lowPass = new ToneLabBiquadFilter();
        ToneLabBiquadFilter bodyPeak = new ToneLabBiquadFilter();
        ToneLabBiquadFilter upperMidPeak = new ToneLabBiquadFilter();
        ToneLabBiquadFilter fizzNotch = new ToneLabBiquadFilter();
        ToneLabBiquadFilter airShelf = new ToneLabBiquadFilter();

        float highPassHz = Mathf.Lerp(65f, 95f, 1f - settings.thump);
        float lowPassHz = Mathf.Lerp(3800f, 6900f, settings.air);
        float bodyGainDb = Mathf.Lerp(1.2f, 4.8f, settings.thump);
        float midGainDb = Mathf.Lerp(-2.5f, 2.0f, settings.presence);
        float fizzNotchDb = Mathf.Lerp(-6.5f, -2.0f, settings.air);
        float airGainDb = Mathf.Lerp(-5f, 1.2f, settings.air);

        highPass.SetHighPass(sampleRate, highPassHz, 0.707f);
        lowPass.SetLowPass(sampleRate, lowPassHz, 0.707f);
        bodyPeak.SetPeaking(sampleRate, 110f, 0.92f, bodyGainDb);
        upperMidPeak.SetPeaking(sampleRate, 1850f, 1.1f, midGainDb);
        fizzNotch.SetPeaking(sampleRate, 4200f, 1.25f, fizzNotchDb);
        airShelf.SetHighShelf(sampleRate, 3200f, 0.707f, airGainDb);

        for (int i = 0; i < kernel.Length; i++)
        {
            float sample = i == 0 ? 1f : 0f;
            sample = highPass.Process(sample);
            sample = bodyPeak.Process(sample);
            sample = upperMidPeak.Process(sample);
            sample = fizzNotch.Process(sample);
            sample = airShelf.Process(sample);
            sample = lowPass.Process(sample);

            float damping = Mathf.Exp(-i / Mathf.Lerp(36f, 52f, settings.air));
            kernel[i] = sample * damping;
        }

        if (kernel.Length > 18)
            kernel[18] += Mathf.Lerp(0.03f, 0.08f, settings.presence);
        if (kernel.Length > 33)
            kernel[33] += Mathf.Lerp(0.015f, 0.04f, settings.air);

        ToneLabPedalUtility.NormalizeKernel(kernel);
        return kernel;
    }
}

public sealed class CabSimPedalDescriptor : ToneLabPedalDescriptorBase<CabSimPedalSettings, CabSimPedalProcessor>
{
    private static readonly IReadOnlyList<ToneLabPedalParameterDefinition> parameterDefinitions =
        new[]
        {
            new ToneLabPedalParameterDefinition("thump", "Thump", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((CabSimPedalSettings)settingsObject).thump, (settingsObject, value) => ((CabSimPedalSettings)settingsObject).thump = value),
            new ToneLabPedalParameterDefinition("presence", "Presence", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((CabSimPedalSettings)settingsObject).presence, (settingsObject, value) => ((CabSimPedalSettings)settingsObject).presence = value),
            new ToneLabPedalParameterDefinition("air", "Air", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((CabSimPedalSettings)settingsObject).air, (settingsObject, value) => ((CabSimPedalSettings)settingsObject).air = value),
            new ToneLabPedalParameterDefinition("mix", "Mix", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((CabSimPedalSettings)settingsObject).mix, (settingsObject, value) => ((CabSimPedalSettings)settingsObject).mix = value)
        };

    public override UnityToneLabRuntime.ToneLabPedalType PedalType => UnityToneLabRuntime.ToneLabPedalType.CabSim;
    public override string DisplayName => "Cab Sim";
    public override string ShortName => "CAB";
    public override string Description => "Speaker and mic convolution stage that removes direct-input fizz and adds recorded cabinet character.";
    public override IReadOnlyList<ToneLabPedalParameterDefinition> Parameters => parameterDefinitions;

    protected override ToneLabPedalAppearance CreateAppearance()
    {
        return new ToneLabPedalAppearance
        {
            BodyColor = new Color(0.22f, 0.28f, 0.18f, 1f),
            FaceColor = new Color(0.65f, 0.74f, 0.52f, 1f),
            LabelStripColor = new Color(0.08f, 0.11f, 0.07f, 0.98f),
            TextColor = new Color(0.97f, 0.99f, 0.93f, 1f),
            SecondaryTextColor = new Color(0.18f, 0.24f, 0.13f, 0.95f),
            EdgeColor = new Color(0.12f, 0.17f, 0.10f, 1f),
            TopEdgeColor = new Color(0.87f, 0.94f, 0.76f, 1f),
            AccentColor = new Color(0.70f, 0.82f, 0.49f, 1f),
            LedOnColor = new Color(0.83f, 0.97f, 0.58f, 1f),
            KnobCount = 3,
            SliderCount = 1,
            DecorationStyle = ToneLabPedalDecorationStyle.Grille
        };
    }
}
