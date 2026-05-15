using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ReverbPedalSettings
{
    public float room_size = 0.45f;
    public float damping = 0.35f;
    public float wet = 0.22f;
    public float dry = 0.95f;
    public float width = 1f;
    public float freeze = 0f;
}

public sealed class ReverbPedalProcessor : IToneLabPedalProcessor
{
    private sealed class CombFilter
    {
        private readonly float[] buffer;
        private int index;
        private float feedback;
        private float damp1;
        private float damp2;
        private float filterStore;

        public CombFilter(int length)
        {
            buffer = new float[Mathf.Max(1, length)];
        }

        public void Reset()
        {
            Array.Clear(buffer, 0, buffer.Length);
            index = 0;
            filterStore = 0f;
        }

        public void SetFeedback(float value)
        {
            feedback = value;
        }

        public void SetDamping(float value)
        {
            damp1 = value;
            damp2 = 1f - value;
        }

        public float Process(float input)
        {
            float output = buffer[index];
            filterStore = (output * damp2) + (filterStore * damp1);
            buffer[index] = input + (filterStore * feedback);
            index++;
            if (index >= buffer.Length)
                index = 0;
            return output;
        }
    }

    private sealed class AllPassFilter
    {
        private readonly float[] buffer;
        private int index;
        private readonly float feedback;

        public AllPassFilter(int length, float feedback)
        {
            buffer = new float[Mathf.Max(1, length)];
            this.feedback = feedback;
        }

        public void Reset()
        {
            Array.Clear(buffer, 0, buffer.Length);
            index = 0;
        }

        public float Process(float input)
        {
            float buffered = buffer[index];
            float output = -input + buffered;
            buffer[index] = input + (buffered * feedback);
            index++;
            if (index >= buffer.Length)
                index = 0;
            return output;
        }
    }

    private ReverbPedalSettings settings = new ReverbPedalSettings();
    private CombFilter[] combLeft;
    private CombFilter[] combRight;
    private AllPassFilter[] allPassLeft;
    private AllPassFilter[] allPassRight;
    private int preparedSampleRate;

    public void Prepare(int sampleRate, int channels)
    {
        preparedSampleRate = Mathf.Max(1, sampleRate);
        int[] combTunings = ScaleTunings(new[] { 1116, 1188, 1277, 1356 }, preparedSampleRate);
        int[] allPassTunings = ScaleTunings(new[] { 556, 441 }, preparedSampleRate);

        combLeft = new CombFilter[combTunings.Length];
        combRight = new CombFilter[combTunings.Length];
        for (int i = 0; i < combTunings.Length; i++)
        {
            combLeft[i] = new CombFilter(combTunings[i]);
            combRight[i] = new CombFilter(combTunings[i] + Mathf.RoundToInt(23f * preparedSampleRate / 44100f));
        }

        allPassLeft = new AllPassFilter[allPassTunings.Length];
        allPassRight = new AllPassFilter[allPassTunings.Length];
        for (int i = 0; i < allPassTunings.Length; i++)
        {
            allPassLeft[i] = new AllPassFilter(allPassTunings[i], 0.5f);
            allPassRight[i] = new AllPassFilter(allPassTunings[i] + Mathf.RoundToInt(23f * preparedSampleRate / 44100f), 0.5f);
        }

        Reset();
    }

    public void Reset()
    {
        if (combLeft != null)
        {
            for (int i = 0; i < combLeft.Length; i++)
            {
                combLeft[i].Reset();
                combRight[i].Reset();
            }
        }

        if (allPassLeft != null)
        {
            for (int i = 0; i < allPassLeft.Length; i++)
            {
                allPassLeft[i].Reset();
                allPassRight[i].Reset();
            }
        }
    }

    public void ApplySettings(object settingsObject)
    {
        settings = settingsObject as ReverbPedalSettings ?? new ReverbPedalSettings();
        settings.room_size = Mathf.Clamp01(settings.room_size);
        settings.damping = Mathf.Clamp01(settings.damping);
        settings.wet = Mathf.Clamp01(settings.wet);
        settings.dry = Mathf.Clamp01(settings.dry);
        settings.width = Mathf.Clamp01(settings.width);
        settings.freeze = Mathf.Clamp01(settings.freeze);
    }

    public void Process(float[] data, int channels, int sampleRate)
    {
        if (data == null || combLeft == null || preparedSampleRate <= 0)
            return;

        float room = Mathf.Lerp(0.45f, 0.93f, settings.room_size);
        bool freezeMode = settings.freeze >= 0.5f;
        float wet1 = settings.wet * ((settings.width * 0.5f) + 0.5f);
        float wet2 = settings.wet * ((1f - settings.width) * 0.5f);
        float feedback = freezeMode ? 0.995f : room;
        float inputGain = freezeMode ? 0f : 0.23f;

        for (int i = 0; i < combLeft.Length; i++)
        {
            combLeft[i].SetFeedback(feedback);
            combRight[i].SetFeedback(feedback);
            combLeft[i].SetDamping(settings.damping);
            combRight[i].SetDamping(settings.damping);
        }

        for (int frame = 0; frame < data.Length; frame += channels)
        {
            float inL = data[frame];
            float inR = channels > 1 ? data[frame + 1] : inL;
            float input = ((inL + inR) * 0.5f) * inputGain;

            float outL = 0f;
            float outR = 0f;
            for (int i = 0; i < combLeft.Length; i++)
            {
                outL += combLeft[i].Process(input);
                outR += combRight[i].Process(input);
            }

            for (int i = 0; i < allPassLeft.Length; i++)
            {
                outL = allPassLeft[i].Process(outL);
                outR = allPassRight[i].Process(outR);
            }

            data[frame] = (inL * settings.dry) + (outL * wet1) + (outR * wet2);
            if (channels > 1)
                data[frame + 1] = (inR * settings.dry) + (outR * wet1) + (outL * wet2);
        }
    }

    private static int[] ScaleTunings(int[] baseTunings, int sampleRate)
    {
        int[] scaled = new int[baseTunings.Length];
        float scale = sampleRate / 44100f;
        for (int i = 0; i < baseTunings.Length; i++)
            scaled[i] = Mathf.Max(1, Mathf.RoundToInt(baseTunings[i] * scale));
        return scaled;
    }
}

public sealed class ReverbPedalDescriptor : ToneLabPedalDescriptorBase<ReverbPedalSettings, ReverbPedalProcessor>
{
    private static readonly IReadOnlyList<ToneLabPedalParameterDefinition> parameterDefinitions =
        new[]
        {
            new ToneLabPedalParameterDefinition("room_size", "Room Size", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((ReverbPedalSettings)settingsObject).room_size, (settingsObject, value) => ((ReverbPedalSettings)settingsObject).room_size = value),
            new ToneLabPedalParameterDefinition("damping", "Damping", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((ReverbPedalSettings)settingsObject).damping, (settingsObject, value) => ((ReverbPedalSettings)settingsObject).damping = value),
            new ToneLabPedalParameterDefinition("wet", "Wet", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((ReverbPedalSettings)settingsObject).wet, (settingsObject, value) => ((ReverbPedalSettings)settingsObject).wet = value),
            new ToneLabPedalParameterDefinition("dry", "Dry", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((ReverbPedalSettings)settingsObject).dry, (settingsObject, value) => ((ReverbPedalSettings)settingsObject).dry = value),
            new ToneLabPedalParameterDefinition("width", "Width", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((ReverbPedalSettings)settingsObject).width, (settingsObject, value) => ((ReverbPedalSettings)settingsObject).width = value),
            new ToneLabPedalParameterDefinition("freeze", "Freeze", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((ReverbPedalSettings)settingsObject).freeze, (settingsObject, value) => ((ReverbPedalSettings)settingsObject).freeze = value)
        };

    public override UnityToneLabRuntime.ToneLabPedalType PedalType => UnityToneLabRuntime.ToneLabPedalType.Reverb;
    public override string DisplayName => "Reverb";
    public override string ShortName => "RV";
    public override string Description => "Room simulation that sits near the end of the signal path.";
    public override IReadOnlyList<ToneLabPedalParameterDefinition> Parameters => parameterDefinitions;

    protected override ToneLabPedalAppearance CreateAppearance()
    {
        return new ToneLabPedalAppearance
        {
            BodyColor = new Color(0.85f, 0.52f, 0.38f, 1f),
            FaceColor = new Color(0.95f, 0.79f, 0.68f, 1f),
            LabelStripColor = new Color(0.22f, 0.12f, 0.11f, 0.98f),
            TextColor = new Color(1.00f, 0.96f, 0.94f, 1f),
            SecondaryTextColor = new Color(0.39f, 0.19f, 0.16f, 0.95f),
            EdgeColor = new Color(0.42f, 0.18f, 0.14f, 1f),
            TopEdgeColor = new Color(0.99f, 0.87f, 0.78f, 1f),
            AccentColor = new Color(0.96f, 0.58f, 0.44f, 1f),
            LedOnColor = new Color(0.99f, 0.72f, 0.52f, 1f),
            KnobCount = 2,
            SliderCount = 0,
            DecorationStyle = ToneLabPedalDecorationStyle.SparkBars
        };
    }
}
