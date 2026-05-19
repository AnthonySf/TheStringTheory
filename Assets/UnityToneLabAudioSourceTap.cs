using System;
using UnityEngine;

[DisallowMultipleComponent]
internal sealed class UnityToneLabAudioSourceTap : MonoBehaviour
{
    private readonly object bufferLock = new object();
    private AudioSource source;
    private float[] ringBuffer = Array.Empty<float>();
    private int writeIndex;
    private int readIndex;
    private int bufferedSamples;
    private int channelCount = 2;
    private int overflowSamples;
    private int underrunSamples;
    private int peakBufferedSamples;
    private volatile bool captureEnabled;
    private volatile bool suppressOutput;
    private volatile float outputGain = 1f;

    public AudioSource Source => source;
    public int ChannelCount => Mathf.Max(1, channelCount);
    public int OverflowSamples => overflowSamples;
    public int UnderrunSamples => underrunSamples;
    public int PeakBufferedSamples => peakBufferedSamples;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
    }

    public void SetCaptureState(bool enabled, bool suppress)
    {
        captureEnabled = enabled;
        suppressOutput = enabled && suppress;
        if (!enabled)
            ResetCapturedAudio();
    }

    public void SetOutputGain(float gain)
    {
        outputGain = Mathf.Clamp(gain, 0f, 2f);
    }

    public void ResetCapturedAudio()
    {
        lock (bufferLock)
        {
            writeIndex = 0;
            readIndex = 0;
            bufferedSamples = 0;
            overflowSamples = 0;
            underrunSamples = 0;
            peakBufferedSamples = 0;
        }
    }

    public int Consume(float[] destination, int requestedSamples)
    {
        if (destination == null || requestedSamples <= 0)
            return 0;

        int copied = 0;
        lock (bufferLock)
        {
            int samplesToCopy = Mathf.Min(requestedSamples, bufferedSamples);
            for (int i = 0; i < samplesToCopy; i++)
            {
                destination[i] = ringBuffer[readIndex];
                readIndex = (readIndex + 1) % ringBuffer.Length;
            }

            bufferedSamples -= samplesToCopy;
            copied = samplesToCopy;
            if (samplesToCopy < requestedSamples)
                underrunSamples += requestedSamples - samplesToCopy;
        }

        if (copied < requestedSamples)
            Array.Clear(destination, copied, requestedSamples - copied);

        return copied;
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (!captureEnabled || data == null || data.Length == 0)
            return;

        channelCount = Mathf.Max(1, channels);
        AppendCapturedSamples(data, outputGain);
        if (suppressOutput)
            Array.Clear(data, 0, data.Length);
    }

    private void AppendCapturedSamples(float[] samples, float gain)
    {
        lock (bufferLock)
        {
            int requiredCapacity = Mathf.Max(samples.Length * 4, bufferedSamples + samples.Length + 2048);
            EnsureCapacity(requiredCapacity);
            for (int i = 0; i < samples.Length; i++)
            {
                ringBuffer[writeIndex] = samples[i] * gain;
                writeIndex = (writeIndex + 1) % ringBuffer.Length;
                if (bufferedSamples < ringBuffer.Length)
                {
                    bufferedSamples++;
                }
                else
                {
                    overflowSamples++;
                    readIndex = (readIndex + 1) % ringBuffer.Length;
                }
            }

            if (bufferedSamples > peakBufferedSamples)
                peakBufferedSamples = bufferedSamples;
        }
    }

    private void EnsureCapacity(int requiredCapacity)
    {
        if (ringBuffer != null && ringBuffer.Length >= requiredCapacity)
            return;

        float[] replacement = new float[requiredCapacity];
        for (int i = 0; i < bufferedSamples; i++)
        {
            int sourceIndex = ringBuffer != null && ringBuffer.Length > 0
                ? (readIndex + i) % ringBuffer.Length
                : 0;
            replacement[i] = ringBuffer != null && ringBuffer.Length > 0 ? ringBuffer[sourceIndex] : 0f;
        }

        ringBuffer = replacement;
        readIndex = 0;
        writeIndex = bufferedSamples;
    }
}
