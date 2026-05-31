using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

internal static class ToneLabPortAudio
{
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
    private const string DllName = "portaudio";
#else
    private const string DllName = "libportaudio64bit-asio";
#endif
    private const int PaNoError = 0;
    private const int PaNoDevice = -1;
    private const ulong PaFloat32 = 0x00000001;
    private const uint PaInputUnderflow = 0x00000001;
    private const uint PaInputOverflow = 0x00000002;
    private const uint PaOutputUnderflow = 0x00000004;
    private const uint PaOutputOverflow = 0x00000008;
    private const uint PaPrimingOutput = 0x00000010;

    private static bool initialized;
    private static bool initializationAttempted;
    private static string initializationError = string.Empty;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int StreamCallback(IntPtr input, IntPtr output, uint frameCount, IntPtr timeInfo, uint statusFlags, IntPtr userData);

    [StructLayout(LayoutKind.Sequential)]
    private struct PaDeviceInfoNative
    {
        public int structVersion;
        public IntPtr name;
        public int hostApi;
        public int maxInputChannels;
        public int maxOutputChannels;
        public double defaultLowInputLatency;
        public double defaultLowOutputLatency;
        public double defaultHighInputLatency;
        public double defaultHighOutputLatency;
        public double defaultSampleRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PaHostApiInfoNative
    {
        public int structVersion;
        public int type;
        public IntPtr name;
        public int deviceCount;
        public int defaultInputDevice;
        public int defaultOutputDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StreamInfoNative
    {
        public int structVersion;
        public double inputLatency;
        public double outputLatency;
        public double sampleRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct StreamParameters
    {
        public int device;
        public int channelCount;
        public ulong sampleFormat;
        public double suggestedLatency;
        public IntPtr hostApiSpecificStreamInfo;
    }

    internal sealed class DeviceDescriptor
    {
        public int Index { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string HostApiName { get; set; } = string.Empty;
        public int MaxInputChannels { get; set; }
        public int MaxOutputChannels { get; set; }
        public double DefaultSampleRate { get; set; }
        public double DefaultLowInputLatency { get; set; }
        public double DefaultLowOutputLatency { get; set; }
    }

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_Initialize();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_Terminate();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr Pa_GetErrorText(int errorCode);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_GetDeviceCount();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr Pa_GetDeviceInfo(int device);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr Pa_GetHostApiInfo(int hostApi);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_GetDefaultOutputDevice();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_OpenStream(
        out IntPtr stream,
        ref StreamParameters inputParameters,
        ref StreamParameters outputParameters,
        double sampleRate,
        uint framesPerBuffer,
        ulong streamFlags,
        StreamCallback streamCallback,
        IntPtr userData);

    [DllImport(DllName, EntryPoint = "Pa_OpenStream", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_OpenStreamRaw(
        out IntPtr stream,
        IntPtr inputParameters,
        IntPtr outputParameters,
        double sampleRate,
        uint framesPerBuffer,
        ulong streamFlags,
        StreamCallback streamCallback,
        IntPtr userData);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_IsFormatSupported(
        ref StreamParameters inputParameters,
        ref StreamParameters outputParameters,
        double sampleRate);

    [DllImport(DllName, EntryPoint = "Pa_IsFormatSupported", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_IsFormatSupportedRaw(
        IntPtr inputParameters,
        IntPtr outputParameters,
        double sampleRate);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_StartStream(IntPtr stream);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_StopStream(IntPtr stream);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_CloseStream(IntPtr stream);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr Pa_GetStreamInfo(IntPtr stream);

    internal static bool TryEnsureInitialized(out string error)
    {
        if (initialized)
        {
            error = string.Empty;
            return true;
        }

        if (initializationAttempted)
        {
            error = initializationError;
            return false;
        }

        initializationAttempted = true;
        try
        {
            int result = Pa_Initialize();
            if (result == PaNoError)
            {
                initialized = true;
                initializationError = string.Empty;
                error = string.Empty;
                return true;
            }

            initializationError = $"PortAudio init failed: {GetErrorText(result)}";
            error = initializationError;
            return false;
        }
        catch (Exception ex)
        {
            initializationError = $"PortAudio unavailable: {ex.Message}";
            error = initializationError;
            return false;
        }
    }

    internal static void Shutdown()
    {
        if (!initialized)
            return;

        try
        {
            Pa_Terminate();
        }
        catch
        {
            // Ignore shutdown failures.
        }

        initialized = false;
        initializationAttempted = false;
        initializationError = string.Empty;
    }

    internal static IReadOnlyList<DeviceDescriptor> EnumerateDevices()
    {
        if (!TryEnsureInitialized(out _))
            return Array.Empty<DeviceDescriptor>();

        int count = Pa_GetDeviceCount();
        if (count <= 0)
            return Array.Empty<DeviceDescriptor>();

        List<DeviceDescriptor> devices = new List<DeviceDescriptor>(count);
        for (int i = 0; i < count; i++)
        {
            IntPtr deviceInfoPtr = Pa_GetDeviceInfo(i);
            if (deviceInfoPtr == IntPtr.Zero)
                continue;

            PaDeviceInfoNative deviceInfo = Marshal.PtrToStructure<PaDeviceInfoNative>(deviceInfoPtr);
            string deviceName = Marshal.PtrToStringAnsi(deviceInfo.name) ?? $"Device {i}";
            string hostApiName = string.Empty;
            IntPtr hostApiPtr = Pa_GetHostApiInfo(deviceInfo.hostApi);
            if (hostApiPtr != IntPtr.Zero)
            {
                PaHostApiInfoNative hostApiInfo = Marshal.PtrToStructure<PaHostApiInfoNative>(hostApiPtr);
                hostApiName = Marshal.PtrToStringAnsi(hostApiInfo.name) ?? string.Empty;
            }

            devices.Add(new DeviceDescriptor
            {
                Index = i,
                DisplayName = $"{i}: {deviceName}",
                Name = deviceName,
                HostApiName = hostApiName,
                MaxInputChannels = deviceInfo.maxInputChannels,
                MaxOutputChannels = deviceInfo.maxOutputChannels,
                DefaultSampleRate = deviceInfo.defaultSampleRate,
                DefaultLowInputLatency = deviceInfo.defaultLowInputLatency,
                DefaultLowOutputLatency = deviceInfo.defaultLowOutputLatency
            });
        }

        return devices;
    }

    internal static IReadOnlyList<DeviceDescriptor> GetPreferredInputDevices(IReadOnlyList<DeviceDescriptor> devices)
    {
        return GetPreferredDevices(devices, descriptor => descriptor.MaxInputChannels > 0);
    }

    internal static IReadOnlyList<DeviceDescriptor> GetPreferredOutputDevices(IReadOnlyList<DeviceDescriptor> devices)
    {
        return GetPreferredDevices(devices, descriptor => descriptor.MaxOutputChannels > 0);
    }

    internal static string GetDefaultOutputDisplayName(IReadOnlyList<DeviceDescriptor> devices)
    {
        if (!TryEnsureInitialized(out _) || devices == null || devices.Count == 0)
            return string.Empty;

        int defaultOutputDevice = Pa_GetDefaultOutputDevice();
        if (defaultOutputDevice == PaNoDevice)
            return string.Empty;

        DeviceDescriptor descriptor = devices.FirstOrDefault(candidate => candidate.Index == defaultOutputDevice);
        return descriptor?.DisplayName ?? string.Empty;
    }

    internal static string GetErrorText(int errorCode)
    {
        try
        {
            IntPtr errorPtr = Pa_GetErrorText(errorCode);
            if (errorPtr != IntPtr.Zero)
            {
                string text = Marshal.PtrToStringAnsi(errorPtr);
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }
        catch
        {
            // Fall back to numeric code.
        }

        return $"PortAudio error {errorCode}";
    }

    private static bool TryReadStreamInfo(IntPtr stream, out StreamInfoNative info)
    {
        info = default;
        if (stream == IntPtr.Zero)
            return false;

        IntPtr infoPtr = Pa_GetStreamInfo(stream);
        if (infoPtr == IntPtr.Zero)
            return false;

        info = Marshal.PtrToStructure<StreamInfoNative>(infoPtr);
        return !double.IsNaN(info.sampleRate) && !double.IsInfinity(info.sampleRate) && info.sampleRate > 0.0;
    }

    private static void RecordStatusFlags(
        uint statusFlags,
        ref int aggregateStatusFlags,
        ref long inputUnderflowCount,
        ref long inputOverflowCount,
        ref long outputUnderflowCount,
        ref long outputOverflowCount,
        ref long primingOutputCount)
    {
        if (statusFlags == 0)
            return;

        int currentFlags = Volatile.Read(ref aggregateStatusFlags);
        Volatile.Write(ref aggregateStatusFlags, currentFlags | unchecked((int)statusFlags));
        if ((statusFlags & PaInputUnderflow) != 0)
            Interlocked.Increment(ref inputUnderflowCount);
        if ((statusFlags & PaInputOverflow) != 0)
            Interlocked.Increment(ref inputOverflowCount);
        if ((statusFlags & PaOutputUnderflow) != 0)
            Interlocked.Increment(ref outputUnderflowCount);
        if ((statusFlags & PaOutputOverflow) != 0)
            Interlocked.Increment(ref outputOverflowCount);
        if ((statusFlags & PaPrimingOutput) != 0)
            Interlocked.Increment(ref primingOutputCount);
    }

    private static void AppendStatusFlagSummary(
        StringBuilder builder,
        string prefix,
        int aggregateStatusFlags,
        long inputUnderflows,
        long inputOverflows,
        long outputUnderflows,
        long outputOverflows,
        long primingOutputs)
    {
        builder.Append(", ");
        builder.Append(prefix);
        builder.Append("StatusFlags=0x");
        builder.Append(aggregateStatusFlags.ToString("X", System.Globalization.CultureInfo.InvariantCulture));
        builder.Append(", ");
        builder.Append(prefix);
        builder.Append("InputUnderflows=");
        builder.Append(inputUnderflows);
        builder.Append(", ");
        builder.Append(prefix);
        builder.Append("InputOverflows=");
        builder.Append(inputOverflows);
        builder.Append(", ");
        builder.Append(prefix);
        builder.Append("OutputUnderflows=");
        builder.Append(outputUnderflows);
        builder.Append(", ");
        builder.Append(prefix);
        builder.Append("OutputOverflows=");
        builder.Append(outputOverflows);
        builder.Append(", ");
        builder.Append(prefix);
        builder.Append("PrimingOutputs=");
        builder.Append(primingOutputs);
    }

    internal sealed class DuplexStream : IDisposable
    {
        private const int DriverManagedCallbackFrameCapacity = 8192;
        private readonly StreamCallback callback;
        private readonly Action<float[], int, int, int, float[]> processBlock;
        private IntPtr stream = IntPtr.Zero;
        private float[] inputBuffer = Array.Empty<float>();
        private float[] outputBuffer = Array.Empty<float>();
        private int inputChannels;
        private int outputChannels;
        private long callbackCount;
        private long totalFramesProcessed;
        private int lastFrameCount;
        private int callbackStatusFlags;
        private long inputUnderflowFlagCount;
        private long inputOverflowFlagCount;
        private long outputUnderflowFlagCount;
        private long outputOverflowFlagCount;
        private long primingOutputFlagCount;
        private double actualSampleRate;
        private double actualInputLatency;
        private double actualOutputLatency;

        public DuplexStream(Action<float[], int, int, int, float[]> processBlock)
        {
            this.processBlock = processBlock;
            callback = StreamCallbackInternal;
        }

        public bool IsRunning => stream != IntPtr.Zero;

        public bool Start(
            int inputDeviceIndex,
            int outputDeviceIndex,
            int requestedInputChannels,
            int requestedOutputChannels,
            int sampleRate,
            uint framesPerBuffer,
            double inputLatency,
            double outputLatency,
            string routeDescription,
            out string error)
        {
            error = string.Empty;
            Stop();
            ResetStatusFlagCounters();

            if (!TryEnsureInitialized(out error))
                return false;

            inputChannels = Math.Max(1, requestedInputChannels);
            outputChannels = Math.Max(1, requestedOutputChannels);
            int callbackFrameCapacity = framesPerBuffer > 0 && framesPerBuffer <= int.MaxValue
                ? Math.Max((int)framesPerBuffer, 256)
                : DriverManagedCallbackFrameCapacity;
            EnsureBufferCapacity(ref inputBuffer, callbackFrameCapacity * inputChannels);
            EnsureBufferCapacity(ref outputBuffer, callbackFrameCapacity * outputChannels);

            StreamParameters inputParameters = new StreamParameters
            {
                device = inputDeviceIndex,
                channelCount = inputChannels,
                sampleFormat = PaFloat32,
                suggestedLatency = inputLatency,
                hostApiSpecificStreamInfo = IntPtr.Zero
            };

            StreamParameters outputParameters = new StreamParameters
            {
                device = outputDeviceIndex,
                channelCount = outputChannels,
                sampleFormat = PaFloat32,
                suggestedLatency = outputLatency,
                hostApiSpecificStreamInfo = IntPtr.Zero
            };

            int supportResult = Pa_IsFormatSupported(ref inputParameters, ref outputParameters, sampleRate);
            if (supportResult != PaNoError)
            {
                error = BuildRouteFailureMessage(
                    "PortAudio route unsupported",
                    supportResult,
                    inputDeviceIndex,
                    outputDeviceIndex,
                    inputChannels,
                    outputChannels,
                    sampleRate,
                    framesPerBuffer,
                    inputLatency,
                    outputLatency,
                    routeDescription);
                return false;
            }

            int openResult = Pa_OpenStream(
                out stream,
                ref inputParameters,
                ref outputParameters,
                sampleRate,
                framesPerBuffer,
                0,
                callback,
                IntPtr.Zero);

            if (openResult != PaNoError || stream == IntPtr.Zero)
            {
                error = BuildRouteFailureMessage(
                    "PortAudio open failed",
                    openResult,
                    inputDeviceIndex,
                    outputDeviceIndex,
                    inputChannels,
                    outputChannels,
                    sampleRate,
                    framesPerBuffer,
                    inputLatency,
                    outputLatency,
                    routeDescription);
                stream = IntPtr.Zero;
                return false;
            }

            int startResult = Pa_StartStream(stream);
            if (startResult != PaNoError)
            {
                error = BuildRouteFailureMessage(
                    "PortAudio start failed",
                    startResult,
                    inputDeviceIndex,
                    outputDeviceIndex,
                    inputChannels,
                    outputChannels,
                    sampleRate,
                    framesPerBuffer,
                    inputLatency,
                    outputLatency,
                    routeDescription);
                Stop();
                return false;
            }

            CacheActualStreamInfo();
            return true;
        }

        public void Stop()
        {
            if (stream == IntPtr.Zero)
                return;

            try
            {
                Pa_StopStream(stream);
            }
            catch
            {
                // Ignore stop failures.
            }

            try
            {
                Pa_CloseStream(stream);
            }
            catch
            {
                // Ignore close failures.
            }

            stream = IntPtr.Zero;
            Volatile.Write(ref callbackCount, 0L);
            Volatile.Write(ref totalFramesProcessed, 0L);
            Volatile.Write(ref lastFrameCount, 0);
            ResetStatusFlagCounters();
        }

        public void Dispose()
        {
            Stop();
        }

        private int StreamCallbackInternal(IntPtr input, IntPtr output, uint frameCount, IntPtr timeInfo, uint statusFlags, IntPtr userData)
        {
            try
            {
                int totalFrames = checked((int)frameCount);
                Interlocked.Increment(ref callbackCount);
                Interlocked.Add(ref totalFramesProcessed, totalFrames);
                Volatile.Write(ref lastFrameCount, totalFrames);
                RecordStatusFlags(statusFlags, ref callbackStatusFlags, ref inputUnderflowFlagCount, ref inputOverflowFlagCount, ref outputUnderflowFlagCount, ref outputOverflowFlagCount, ref primingOutputFlagCount);
                int inputFrameCapacity = inputBuffer.Length / Math.Max(1, inputChannels);
                int outputFrameCapacity = outputBuffer.Length / Math.Max(1, outputChannels);
                int callbackFrameCapacity = Math.Max(1, Math.Min(inputFrameCapacity, outputFrameCapacity));
                int processedFrames = 0;

                while (processedFrames < totalFrames)
                {
                    int chunkFrames = Math.Min(callbackFrameCapacity, totalFrames - processedFrames);
                    int inputSampleCount = checked(chunkFrames * inputChannels);
                    int outputSampleCount = checked(chunkFrames * outputChannels);
                    int inputOffsetSamples = checked(processedFrames * inputChannels);
                    int outputOffsetSamples = checked(processedFrames * outputChannels);

                    if (input != IntPtr.Zero)
                    {
                        IntPtr inputChunk = IntPtr.Add(input, inputOffsetSamples * sizeof(float));
                        Marshal.Copy(inputChunk, inputBuffer, 0, inputSampleCount);
                    }
                    else
                    {
                        Array.Clear(inputBuffer, 0, inputSampleCount);
                    }

                    Array.Clear(outputBuffer, 0, outputSampleCount);
                    processBlock?.Invoke(inputBuffer, inputChannels, outputChannels, chunkFrames, outputBuffer);
                    if (output != IntPtr.Zero)
                    {
                        IntPtr outputChunk = IntPtr.Add(output, outputOffsetSamples * sizeof(float));
                        Marshal.Copy(outputBuffer, 0, outputChunk, outputSampleCount);
                    }

                    processedFrames += chunkFrames;
                }
            }
            catch
            {
                if (output != IntPtr.Zero)
                {
                    try
                    {
                        int totalOutputSamples = checked((int)frameCount * outputChannels);
                        int outputSampleCapacity = outputBuffer.Length;
                        int writtenSamples = 0;
                        while (writtenSamples < totalOutputSamples && outputSampleCapacity > 0)
                        {
                            int sampleCount = Math.Min(outputSampleCapacity, totalOutputSamples - writtenSamples);
                            Array.Clear(outputBuffer, 0, sampleCount);
                            IntPtr outputChunk = IntPtr.Add(output, writtenSamples * sizeof(float));
                            Marshal.Copy(outputBuffer, 0, outputChunk, sampleCount);
                            writtenSamples += sampleCount;
                        }
                    }
                    catch
                    {
                        // Keep the PortAudio callback alive even if emergency silence fails.
                    }
                }
            }

            return 0;
        }

        public string GetDiagnosticSummary()
        {
            if (stream == IntPtr.Zero)
                return "duplexStream=stopped";

            StringBuilder builder = new StringBuilder();
            builder.Append("duplexStream=running");
            builder.Append(", callbacks=");
            builder.Append(Volatile.Read(ref callbackCount));
            builder.Append(", frames=");
            builder.Append(Volatile.Read(ref totalFramesProcessed));
            builder.Append(", lastFrames=");
            builder.Append(Volatile.Read(ref lastFrameCount));
            AppendStatusFlagSummary(
                builder,
                "callback",
                Volatile.Read(ref callbackStatusFlags),
                Volatile.Read(ref inputUnderflowFlagCount),
                Volatile.Read(ref inputOverflowFlagCount),
                Volatile.Read(ref outputUnderflowFlagCount),
                Volatile.Read(ref outputOverflowFlagCount),
                Volatile.Read(ref primingOutputFlagCount));
            builder.Append(", actualSampleRate=");
            builder.Append(actualSampleRate.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", actualInputLatency=");
            builder.Append(actualInputLatency.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", actualOutputLatency=");
            builder.Append(actualOutputLatency.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private void CacheActualStreamInfo()
        {
            actualSampleRate = 0.0;
            actualInputLatency = 0.0;
            actualOutputLatency = 0.0;
            if (TryReadStreamInfo(stream, out StreamInfoNative info))
            {
                actualSampleRate = info.sampleRate;
                actualInputLatency = info.inputLatency;
                actualOutputLatency = info.outputLatency;
            }
        }

        private void ResetStatusFlagCounters()
        {
            Volatile.Write(ref callbackStatusFlags, 0);
            Volatile.Write(ref inputUnderflowFlagCount, 0L);
            Volatile.Write(ref inputOverflowFlagCount, 0L);
            Volatile.Write(ref outputUnderflowFlagCount, 0L);
            Volatile.Write(ref outputOverflowFlagCount, 0L);
            Volatile.Write(ref primingOutputFlagCount, 0L);
        }

        private static void EnsureBufferCapacity(ref float[] buffer, int requiredLength)
        {
            if (buffer == null || buffer.Length < requiredLength)
                buffer = new float[requiredLength];
        }

        private static string BuildRouteFailureMessage(
            string prefix,
            int portAudioResult,
            int inputDeviceIndex,
            int outputDeviceIndex,
            int inputChannelCount,
            int outputChannelCount,
            int sampleRate,
            uint framesPerBuffer,
            double inputLatency,
            double outputLatency,
            string routeDescription)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(prefix);
            builder.Append(": ");
            builder.Append(GetErrorText(portAudioResult));
            builder.Append(" | route=");
            builder.Append(string.IsNullOrWhiteSpace(routeDescription)
                ? $"input #{inputDeviceIndex} -> output #{outputDeviceIndex}"
                : routeDescription.Trim());
            builder.Append(" | inputDeviceIndex=");
            builder.Append(inputDeviceIndex);
            builder.Append(", outputDeviceIndex=");
            builder.Append(outputDeviceIndex);
            builder.Append(", inputChannels=");
            builder.Append(inputChannelCount);
            builder.Append(", outputChannels=");
            builder.Append(outputChannelCount);
            builder.Append(", sampleRate=");
            builder.Append(sampleRate);
            builder.Append(", framesPerBuffer=");
            builder.Append(framesPerBuffer);
            builder.Append(", inputLatency=");
            builder.Append(inputLatency.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", outputLatency=");
            builder.Append(outputLatency.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    internal sealed class SplitStream : IDisposable
    {
        private const int DriverManagedCallbackFrameCapacity = 8192;
        private const int DriverManagedInitialRetainFrames = 512;
        private const int MinimumRingFrames = 2048;
        private const int MaximumRingFrames = 16384;
        private readonly StreamCallback inputCallback;
        private readonly StreamCallback outputCallback;
        private readonly Action<float[], int, int, int, float[]> processBlock;
        private IntPtr inputStream = IntPtr.Zero;
        private IntPtr outputStream = IntPtr.Zero;
        private float[] inputBuffer = Array.Empty<float>();
        private float[] processedOutputBuffer = Array.Empty<float>();
        private float[] outputBuffer = Array.Empty<float>();
        private long[] outputRing = Array.Empty<long>();
        private int outputRingMask;
        private int maxBufferedFrames;
        private int inputChannels;
        private int outputChannels;
        private long outputWriteIndex;
        private long outputReadIndex;
        private long inputCallbackCount;
        private long outputCallbackCount;
        private long inputFramesProcessed;
        private long outputFramesProcessed;
        private long outputUnderflowCount;
        private long outputCatchUpCount;
        private int inputCallbackStatusFlags;
        private int outputCallbackStatusFlags;
        private long inputCallbackUnderflowFlagCount;
        private long inputCallbackOverflowFlagCount;
        private long inputCallbackOutputUnderflowFlagCount;
        private long inputCallbackOutputOverflowFlagCount;
        private long inputCallbackPrimingOutputFlagCount;
        private long outputCallbackInputUnderflowFlagCount;
        private long outputCallbackInputOverflowFlagCount;
        private long outputCallbackUnderflowFlagCount;
        private long outputCallbackOverflowFlagCount;
        private long outputCallbackPrimingOutputFlagCount;
        private int lastInputFrameCount;
        private int lastOutputFrameCount;
        private int largestObservedBufferedFrames;
        private double actualInputSampleRate;
        private double actualOutputSampleRate;
        private double actualInputLatency;
        private double actualOutputLatency;

        public SplitStream(Action<float[], int, int, int, float[]> processBlock)
        {
            this.processBlock = processBlock;
            inputCallback = InputCallbackInternal;
            outputCallback = OutputCallbackInternal;
        }

        public bool IsRunning => inputStream != IntPtr.Zero || outputStream != IntPtr.Zero;

        public bool Start(
            int inputDeviceIndex,
            int outputDeviceIndex,
            int requestedInputChannels,
            int requestedOutputChannels,
            int sampleRate,
            uint framesPerBuffer,
            double inputLatency,
            double outputLatency,
            string routeDescription,
            out string error)
        {
            error = string.Empty;
            Stop();
            ResetStatusFlagCounters();

            if (!TryEnsureInitialized(out error))
                return false;

            inputChannels = Math.Max(1, requestedInputChannels);
            outputChannels = Math.Max(1, requestedOutputChannels);
            int callbackFrameCapacity = framesPerBuffer > 0 && framesPerBuffer <= int.MaxValue
                ? Math.Max((int)framesPerBuffer, 256)
                : DriverManagedCallbackFrameCapacity;
            EnsureBufferCapacity(ref inputBuffer, callbackFrameCapacity * inputChannels);
            EnsureBufferCapacity(ref processedOutputBuffer, callbackFrameCapacity * outputChannels);
            EnsureBufferCapacity(ref outputBuffer, callbackFrameCapacity * outputChannels);
            EnsureOutputRingCapacity(callbackFrameCapacity, framesPerBuffer);

            StreamParameters inputParameters = new StreamParameters
            {
                device = inputDeviceIndex,
                channelCount = inputChannels,
                sampleFormat = PaFloat32,
                suggestedLatency = inputLatency,
                hostApiSpecificStreamInfo = IntPtr.Zero
            };

            StreamParameters outputParameters = new StreamParameters
            {
                device = outputDeviceIndex,
                channelCount = outputChannels,
                sampleFormat = PaFloat32,
                suggestedLatency = outputLatency,
                hostApiSpecificStreamInfo = IntPtr.Zero
            };

            int inputSupport = CheckSingleDirectionFormatSupported(ref inputParameters, input: true, sampleRate);
            if (inputSupport != PaNoError)
            {
                error = BuildSplitRouteFailureMessage(
                    "PortAudio split input unsupported",
                    inputSupport,
                    inputDeviceIndex,
                    outputDeviceIndex,
                    inputChannels,
                    outputChannels,
                    sampleRate,
                    framesPerBuffer,
                    inputLatency,
                    outputLatency,
                    routeDescription);
                return false;
            }

            int outputSupport = CheckSingleDirectionFormatSupported(ref outputParameters, input: false, sampleRate);
            if (outputSupport != PaNoError)
            {
                error = BuildSplitRouteFailureMessage(
                    "PortAudio split output unsupported",
                    outputSupport,
                    inputDeviceIndex,
                    outputDeviceIndex,
                    inputChannels,
                    outputChannels,
                    sampleRate,
                    framesPerBuffer,
                    inputLatency,
                    outputLatency,
                    routeDescription);
                return false;
            }

            int openInput = OpenSingleDirectionStream(
                ref inputStream,
                ref inputParameters,
                input: true,
                sampleRate,
                framesPerBuffer,
                inputCallback);
            if (openInput != PaNoError || inputStream == IntPtr.Zero)
            {
                error = BuildSplitRouteFailureMessage(
                    "PortAudio split input open failed",
                    openInput,
                    inputDeviceIndex,
                    outputDeviceIndex,
                    inputChannels,
                    outputChannels,
                    sampleRate,
                    framesPerBuffer,
                    inputLatency,
                    outputLatency,
                    routeDescription);
                CloseStream(ref inputStream);
                return false;
            }

            int openOutput = OpenSingleDirectionStream(
                ref outputStream,
                ref outputParameters,
                input: false,
                sampleRate,
                framesPerBuffer,
                outputCallback);
            if (openOutput != PaNoError || outputStream == IntPtr.Zero)
            {
                error = BuildSplitRouteFailureMessage(
                    "PortAudio split output open failed",
                    openOutput,
                    inputDeviceIndex,
                    outputDeviceIndex,
                    inputChannels,
                    outputChannels,
                    sampleRate,
                    framesPerBuffer,
                    inputLatency,
                    outputLatency,
                    routeDescription);
                CloseStream(ref inputStream);
                CloseStream(ref outputStream);
                return false;
            }

            if (!TryValidateActualSplitSampleRates(inputStream, outputStream, sampleRate, out string actualSampleRateError))
            {
                error = BuildSplitRouteFailureMessage(
                    "PortAudio split actual sample rate mismatch",
                    actualSampleRateError,
                    inputDeviceIndex,
                    outputDeviceIndex,
                    inputChannels,
                    outputChannels,
                    sampleRate,
                    framesPerBuffer,
                    inputLatency,
                    outputLatency,
                    routeDescription);
                CloseStream(ref inputStream);
                CloseStream(ref outputStream);
                return false;
            }

            ResetOutputRing();
            int startInput = Pa_StartStream(inputStream);
            if (startInput != PaNoError)
            {
                error = BuildSplitRouteFailureMessage(
                    "PortAudio split input start failed",
                    startInput,
                    inputDeviceIndex,
                    outputDeviceIndex,
                    inputChannels,
                    outputChannels,
                    sampleRate,
                    framesPerBuffer,
                    inputLatency,
                    outputLatency,
                    routeDescription);
                Stop();
                return false;
            }

            int startOutput = Pa_StartStream(outputStream);
            if (startOutput != PaNoError)
            {
                error = BuildSplitRouteFailureMessage(
                    "PortAudio split output start failed",
                    startOutput,
                    inputDeviceIndex,
                    outputDeviceIndex,
                    inputChannels,
                    outputChannels,
                    sampleRate,
                    framesPerBuffer,
                    inputLatency,
                    outputLatency,
                    routeDescription);
                Stop();
                return false;
            }

            CacheActualSplitStreamInfo();
            return true;
        }

        public void Stop()
        {
            CloseStream(ref outputStream);
            CloseStream(ref inputStream);
            ResetOutputRing();
        }

        public void Dispose()
        {
            Stop();
        }

        private int InputCallbackInternal(IntPtr input, IntPtr output, uint frameCount, IntPtr timeInfo, uint statusFlags, IntPtr userData)
        {
            try
            {
                int totalFrames = checked((int)frameCount);
                Interlocked.Increment(ref inputCallbackCount);
                Interlocked.Add(ref inputFramesProcessed, totalFrames);
                Volatile.Write(ref lastInputFrameCount, totalFrames);
                RecordStatusFlags(statusFlags, ref inputCallbackStatusFlags, ref inputCallbackUnderflowFlagCount, ref inputCallbackOverflowFlagCount, ref inputCallbackOutputUnderflowFlagCount, ref inputCallbackOutputOverflowFlagCount, ref inputCallbackPrimingOutputFlagCount);
                UpdateMaxBufferedFramesForCallback(totalFrames);
                int inputFrameCapacity = inputBuffer.Length / Math.Max(1, inputChannels);
                int outputFrameCapacity = processedOutputBuffer.Length / Math.Max(1, outputChannels);
                int callbackFrameCapacity = Math.Max(1, Math.Min(inputFrameCapacity, outputFrameCapacity));
                int processedFrames = 0;

                while (processedFrames < totalFrames)
                {
                    int chunkFrames = Math.Min(callbackFrameCapacity, totalFrames - processedFrames);
                    int inputSampleCount = checked(chunkFrames * inputChannels);
                    int outputSampleCount = checked(chunkFrames * outputChannels);
                    int inputOffsetSamples = checked(processedFrames * inputChannels);

                    if (input != IntPtr.Zero)
                    {
                        IntPtr inputChunk = IntPtr.Add(input, inputOffsetSamples * sizeof(float));
                        Marshal.Copy(inputChunk, inputBuffer, 0, inputSampleCount);
                    }
                    else
                    {
                        Array.Clear(inputBuffer, 0, inputSampleCount);
                    }

                    Array.Clear(processedOutputBuffer, 0, outputSampleCount);
                    processBlock?.Invoke(inputBuffer, inputChannels, outputChannels, chunkFrames, processedOutputBuffer);
                    PushProcessedOutput(processedOutputBuffer, outputChannels, chunkFrames);
                    processedFrames += chunkFrames;
                }
            }
            catch
            {
                // Keep PortAudio alive; the output stream will emit silence if no fresh frames arrive.
            }

            return 0;
        }

        private int OutputCallbackInternal(IntPtr input, IntPtr output, uint frameCount, IntPtr timeInfo, uint statusFlags, IntPtr userData)
        {
            try
            {
                if (output == IntPtr.Zero)
                    return 0;

                int totalFrames = checked((int)frameCount);
                Interlocked.Increment(ref outputCallbackCount);
                Interlocked.Add(ref outputFramesProcessed, totalFrames);
                Volatile.Write(ref lastOutputFrameCount, totalFrames);
                RecordStatusFlags(statusFlags, ref outputCallbackStatusFlags, ref outputCallbackInputUnderflowFlagCount, ref outputCallbackInputOverflowFlagCount, ref outputCallbackUnderflowFlagCount, ref outputCallbackOverflowFlagCount, ref outputCallbackPrimingOutputFlagCount);
                UpdateMaxBufferedFramesForCallback(totalFrames);
                int outputFrameCapacity = outputBuffer.Length / Math.Max(1, outputChannels);
                int callbackFrameCapacity = Math.Max(1, outputFrameCapacity);
                int processedFrames = 0;

                while (processedFrames < totalFrames)
                {
                    int chunkFrames = Math.Min(callbackFrameCapacity, totalFrames - processedFrames);
                    int outputSampleCount = checked(chunkFrames * outputChannels);
                    int outputOffsetSamples = checked(processedFrames * outputChannels);

                    PullOutput(outputBuffer, outputChannels, chunkFrames);
                    IntPtr outputChunk = IntPtr.Add(output, outputOffsetSamples * sizeof(float));
                    Marshal.Copy(outputBuffer, 0, outputChunk, outputSampleCount);
                    processedFrames += chunkFrames;
                }
            }
            catch
            {
                if (output != IntPtr.Zero)
                    WriteEmergencySilence(output, frameCount);
            }

            return 0;
        }

        private void PushProcessedOutput(float[] data, int channels, int frameCount)
        {
            if (data == null || outputRing.Length == 0 || frameCount <= 0)
                return;

            int safeChannels = Math.Max(1, channels);
            long w = Volatile.Read(ref outputWriteIndex);
            for (int frame = 0; frame < frameCount; frame++)
            {
                int frameStart = frame * safeChannels;
                if (frameStart >= data.Length)
                    break;

                float left = Sanitize(data[frameStart]);
                float right = safeChannels > 1 && frameStart + 1 < data.Length
                    ? Sanitize(data[frameStart + 1])
                    : left;
                int slot = (int)((w + frame) & outputRingMask);
                Volatile.Write(ref outputRing[slot], PackStereo(left, right));
            }

            Volatile.Write(ref outputWriteIndex, w + frameCount);
        }

        private void PullOutput(float[] destination, int channels, int frameCount)
        {
            if (destination == null)
                return;

            int safeChannels = Math.Max(1, channels);
            int sampleCount = Math.Min(destination.Length, frameCount * safeChannels);
            Array.Clear(destination, 0, sampleCount);
            if (outputRing.Length == 0 || frameCount <= 0)
                return;

            long r = Volatile.Read(ref outputReadIndex);
            long w = Volatile.Read(ref outputWriteIndex);
            if (w < r)
                r = w;

            int capacity = outputRing.Length;
            long available = w - r;
            if (available > capacity)
            {
                r = w - capacity;
                available = capacity;
                Interlocked.Increment(ref outputCatchUpCount);
            }

            int bufferedLimit = Math.Max(1, Volatile.Read(ref maxBufferedFrames));
            if (available > bufferedLimit)
            {
                r = w - bufferedLimit;
                available = bufferedLimit;
                Interlocked.Increment(ref outputCatchUpCount);
            }
            UpdateLargestObservedBufferedFrames((int)Math.Min(int.MaxValue, Math.Max(0, available)));

            int pullCount = Math.Min(frameCount, (int)Math.Max(0, available));
            if (pullCount < frameCount)
                Interlocked.Increment(ref outputUnderflowCount);
            for (int frame = 0; frame < pullCount; frame++)
            {
                int slot = (int)((r + frame) & outputRingMask);
                UnpackStereo(Volatile.Read(ref outputRing[slot]), out float left, out float right);
                int destinationStart = frame * safeChannels;
                if (destinationStart >= sampleCount)
                    break;

                if (safeChannels == 1)
                {
                    destination[destinationStart] = Sanitize((left + right) * 0.5f);
                    continue;
                }

                for (int channel = 0; channel < safeChannels; channel++)
                {
                    int index = destinationStart + channel;
                    if (index >= sampleCount)
                        break;

                    destination[index] = Sanitize((channel & 1) == 0 ? left : right);
                }
            }

            Volatile.Write(ref outputReadIndex, r + Math.Min(frameCount, (int)Math.Max(0, available)));
        }

        private void EnsureOutputRingCapacity(int callbackFrameCapacity, uint framesPerBuffer)
        {
            long requestedFrames = Math.Max(MinimumRingFrames, (long)Math.Max(1, callbackFrameCapacity) * 8L);
            int capacity = NextPowerOfTwo((int)Math.Min(MaximumRingFrames, requestedFrames));
            if (outputRing == null || outputRing.Length != capacity)
                outputRing = new long[capacity];
            outputRingMask = capacity - 1;

            int requestedBuffer = framesPerBuffer > 0 && framesPerBuffer <= int.MaxValue
                ? Math.Max((int)framesPerBuffer, 1)
                : DriverManagedInitialRetainFrames;
            // Keep the ring capacity generous for driver-managed callback spikes, but
            // cap the retained queue close to the active callback size so split mode
            // corrects clock drift by dropping old frames before it becomes audible
            // monitoring latency.
            long targetFrames = Math.Max(128L, Math.Max((long)requestedBuffer * 4L, (long)callbackFrameCapacity * 2L));
            if (framesPerBuffer == 0)
                targetFrames = DriverManagedInitialRetainFrames;
            Volatile.Write(ref maxBufferedFrames, Math.Max(1, (int)Math.Min(capacity - 1L, targetFrames)));
            ResetOutputRing();
        }

        private void UpdateMaxBufferedFramesForCallback(int frameCount)
        {
            if (frameCount <= 0 || outputRing == null || outputRing.Length == 0)
                return;

            int capacityLimit = Math.Max(1, outputRing.Length - 1);
            long targetFrames = Math.Max(DriverManagedInitialRetainFrames, (long)frameCount * 2L);
            int target = Math.Max(1, (int)Math.Min(capacityLimit, targetFrames));
            int current = Math.Max(1, Volatile.Read(ref maxBufferedFrames));
            if (target > current)
                Volatile.Write(ref maxBufferedFrames, target);
        }

        private void UpdateLargestObservedBufferedFrames(int availableFrames)
        {
            if (availableFrames <= 0)
                return;

            int current = Volatile.Read(ref largestObservedBufferedFrames);
            while (availableFrames > current)
            {
                int previous = Interlocked.CompareExchange(ref largestObservedBufferedFrames, availableFrames, current);
                if (previous == current)
                    return;
                current = previous;
            }
        }

        private void ResetOutputRing()
        {
            Volatile.Write(ref outputWriteIndex, 0);
            Volatile.Write(ref outputReadIndex, 0);
            Volatile.Write(ref inputCallbackCount, 0L);
            Volatile.Write(ref outputCallbackCount, 0L);
            Volatile.Write(ref inputFramesProcessed, 0L);
            Volatile.Write(ref outputFramesProcessed, 0L);
            Volatile.Write(ref outputUnderflowCount, 0L);
            Volatile.Write(ref outputCatchUpCount, 0L);
            ResetStatusFlagCounters();
            Volatile.Write(ref lastInputFrameCount, 0);
            Volatile.Write(ref lastOutputFrameCount, 0);
            Volatile.Write(ref largestObservedBufferedFrames, 0);
            if (outputRing == null)
                return;

            for (int i = 0; i < outputRing.Length; i++)
                Volatile.Write(ref outputRing[i], 0L);
        }

        private void ResetStatusFlagCounters()
        {
            Volatile.Write(ref inputCallbackStatusFlags, 0);
            Volatile.Write(ref outputCallbackStatusFlags, 0);
            Volatile.Write(ref inputCallbackUnderflowFlagCount, 0L);
            Volatile.Write(ref inputCallbackOverflowFlagCount, 0L);
            Volatile.Write(ref inputCallbackOutputUnderflowFlagCount, 0L);
            Volatile.Write(ref inputCallbackOutputOverflowFlagCount, 0L);
            Volatile.Write(ref inputCallbackPrimingOutputFlagCount, 0L);
            Volatile.Write(ref outputCallbackInputUnderflowFlagCount, 0L);
            Volatile.Write(ref outputCallbackInputOverflowFlagCount, 0L);
            Volatile.Write(ref outputCallbackUnderflowFlagCount, 0L);
            Volatile.Write(ref outputCallbackOverflowFlagCount, 0L);
            Volatile.Write(ref outputCallbackPrimingOutputFlagCount, 0L);
        }

        public string GetDiagnosticSummary()
        {
            if (inputStream == IntPtr.Zero && outputStream == IntPtr.Zero)
                return "splitStream=stopped";

            long read = Volatile.Read(ref outputReadIndex);
            long write = Volatile.Read(ref outputWriteIndex);
            long queued = Math.Max(0L, write - read);
            int capacity = outputRing != null ? outputRing.Length : 0;
            StringBuilder builder = new StringBuilder();
            builder.Append("splitStream=");
            builder.Append(inputStream != IntPtr.Zero && outputStream != IntPtr.Zero ? "running" : "partial");
            builder.Append(", inputCallbacks=");
            builder.Append(Volatile.Read(ref inputCallbackCount));
            builder.Append(", outputCallbacks=");
            builder.Append(Volatile.Read(ref outputCallbackCount));
            builder.Append(", inputFrames=");
            builder.Append(Volatile.Read(ref inputFramesProcessed));
            builder.Append(", outputFrames=");
            builder.Append(Volatile.Read(ref outputFramesProcessed));
            builder.Append(", lastInputFrames=");
            builder.Append(Volatile.Read(ref lastInputFrameCount));
            builder.Append(", lastOutputFrames=");
            builder.Append(Volatile.Read(ref lastOutputFrameCount));
            builder.Append(", queuedFrames=");
            builder.Append(Math.Min(queued, int.MaxValue));
            builder.Append(", maxQueuedFrames=");
            builder.Append(Volatile.Read(ref largestObservedBufferedFrames));
            builder.Append(", maxBufferedFrames=");
            builder.Append(Volatile.Read(ref maxBufferedFrames));
            builder.Append(", ringCapacity=");
            builder.Append(capacity);
            builder.Append(", underflows=");
            builder.Append(Volatile.Read(ref outputUnderflowCount));
            builder.Append(", catchUps=");
            builder.Append(Volatile.Read(ref outputCatchUpCount));
            AppendStatusFlagSummary(
                builder,
                "inputCallback",
                Volatile.Read(ref inputCallbackStatusFlags),
                Volatile.Read(ref inputCallbackUnderflowFlagCount),
                Volatile.Read(ref inputCallbackOverflowFlagCount),
                Volatile.Read(ref inputCallbackOutputUnderflowFlagCount),
                Volatile.Read(ref inputCallbackOutputOverflowFlagCount),
                Volatile.Read(ref inputCallbackPrimingOutputFlagCount));
            AppendStatusFlagSummary(
                builder,
                "outputCallback",
                Volatile.Read(ref outputCallbackStatusFlags),
                Volatile.Read(ref outputCallbackInputUnderflowFlagCount),
                Volatile.Read(ref outputCallbackInputOverflowFlagCount),
                Volatile.Read(ref outputCallbackUnderflowFlagCount),
                Volatile.Read(ref outputCallbackOverflowFlagCount),
                Volatile.Read(ref outputCallbackPrimingOutputFlagCount));
            builder.Append(", actualInputSampleRate=");
            builder.Append(actualInputSampleRate.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", actualOutputSampleRate=");
            builder.Append(actualOutputSampleRate.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", actualInputLatency=");
            builder.Append(actualInputLatency.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", actualOutputLatency=");
            builder.Append(actualOutputLatency.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private void CacheActualSplitStreamInfo()
        {
            actualInputSampleRate = 0.0;
            actualOutputSampleRate = 0.0;
            actualInputLatency = 0.0;
            actualOutputLatency = 0.0;
            if (TryReadStreamInfo(inputStream, out StreamInfoNative inputInfo))
            {
                actualInputSampleRate = inputInfo.sampleRate;
                actualInputLatency = inputInfo.inputLatency;
            }

            if (TryReadStreamInfo(outputStream, out StreamInfoNative outputInfo))
            {
                actualOutputSampleRate = outputInfo.sampleRate;
                actualOutputLatency = outputInfo.outputLatency;
            }
        }

        private static int CheckSingleDirectionFormatSupported(ref StreamParameters parameters, bool input, int sampleRate)
        {
            IntPtr parametersPtr = IntPtr.Zero;
            try
            {
                parametersPtr = AllocStreamParameters(ref parameters);
                return input
                    ? Pa_IsFormatSupportedRaw(parametersPtr, IntPtr.Zero, sampleRate)
                    : Pa_IsFormatSupportedRaw(IntPtr.Zero, parametersPtr, sampleRate);
            }
            finally
            {
                if (parametersPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(parametersPtr);
            }
        }

        private static int OpenSingleDirectionStream(
            ref IntPtr stream,
            ref StreamParameters parameters,
            bool input,
            int sampleRate,
            uint framesPerBuffer,
            StreamCallback callback)
        {
            IntPtr parametersPtr = IntPtr.Zero;
            try
            {
                parametersPtr = AllocStreamParameters(ref parameters);
                return input
                    ? Pa_OpenStreamRaw(out stream, parametersPtr, IntPtr.Zero, sampleRate, framesPerBuffer, 0, callback, IntPtr.Zero)
                    : Pa_OpenStreamRaw(out stream, IntPtr.Zero, parametersPtr, sampleRate, framesPerBuffer, 0, callback, IntPtr.Zero);
            }
            finally
            {
                if (parametersPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(parametersPtr);
            }
        }

        private static bool TryValidateActualSplitSampleRates(IntPtr inputStream, IntPtr outputStream, int requestedSampleRate, out string error)
        {
            error = string.Empty;
            if (!TryGetStreamSampleRate(inputStream, out double inputSampleRate))
            {
                error = "could not read actual input stream sample rate";
                return false;
            }

            if (!TryGetStreamSampleRate(outputStream, out double outputSampleRate))
            {
                error = "could not read actual output stream sample rate";
                return false;
            }

            if (requestedSampleRate > 0 && (Math.Abs(inputSampleRate - requestedSampleRate) > 0.5 || Math.Abs(outputSampleRate - requestedSampleRate) > 0.5))
            {
                error = $"requested {requestedSampleRate} Hz, input opened at {inputSampleRate:0.###} Hz, output opened at {outputSampleRate:0.###} Hz";
                return false;
            }

            if (Math.Abs(inputSampleRate - outputSampleRate) > 0.5)
            {
                error = $"input opened at {inputSampleRate:0.###} Hz, output opened at {outputSampleRate:0.###} Hz";
                return false;
            }

            return true;
        }

        private static bool TryGetStreamSampleRate(IntPtr stream, out double sampleRate)
        {
            sampleRate = 0.0;
            if (!TryReadStreamInfo(stream, out StreamInfoNative info))
                return false;

            sampleRate = info.sampleRate;
            return true;
        }

        private static IntPtr AllocStreamParameters(ref StreamParameters parameters)
        {
            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<StreamParameters>());
            Marshal.StructureToPtr(parameters, ptr, false);
            return ptr;
        }

        private static void CloseStream(ref IntPtr stream)
        {
            if (stream == IntPtr.Zero)
                return;

            try
            {
                Pa_StopStream(stream);
            }
            catch
            {
                // Ignore stop failures.
            }

            try
            {
                Pa_CloseStream(stream);
            }
            catch
            {
                // Ignore close failures.
            }

            stream = IntPtr.Zero;
        }

        private void WriteEmergencySilence(IntPtr output, uint frameCount)
        {
            try
            {
                int totalOutputSamples = checked((int)frameCount * outputChannels);
                int outputSampleCapacity = outputBuffer.Length;
                int writtenSamples = 0;
                while (writtenSamples < totalOutputSamples && outputSampleCapacity > 0)
                {
                    int sampleCount = Math.Min(outputSampleCapacity, totalOutputSamples - writtenSamples);
                    Array.Clear(outputBuffer, 0, sampleCount);
                    IntPtr outputChunk = IntPtr.Add(output, writtenSamples * sizeof(float));
                    Marshal.Copy(outputBuffer, 0, outputChunk, sampleCount);
                    writtenSamples += sampleCount;
                }
            }
            catch
            {
                // Keep the PortAudio callback alive even if emergency silence fails.
            }
        }

        private static void EnsureBufferCapacity(ref float[] buffer, int requiredLength)
        {
            if (buffer == null || buffer.Length < requiredLength)
                buffer = new float[requiredLength];
        }

        private static int NextPowerOfTwo(int value)
        {
            int result = 1;
            int target = Math.Max(1, value);
            while (result < target && result < MaximumRingFrames)
                result <<= 1;
            return Math.Min(result, MaximumRingFrames);
        }

        private static float Sanitize(float sample)
        {
            if (float.IsNaN(sample) || float.IsInfinity(sample))
                return 0f;
            if (sample > 1f)
                return 1f;
            if (sample < -1f)
                return -1f;
            return sample;
        }

        private static long PackStereo(float left, float right)
        {
            unchecked
            {
                uint lo = (uint)FloatToInt32Bits(left);
                uint hi = (uint)FloatToInt32Bits(right);
                return (long)((ulong)lo | ((ulong)hi << 32));
            }
        }

        private static void UnpackStereo(long packed, out float left, out float right)
        {
            unchecked
            {
                uint lo = (uint)((ulong)packed & 0xffffffffUL);
                uint hi = (uint)(((ulong)packed >> 32) & 0xffffffffUL);
                left = Int32BitsToFloat((int)lo);
                right = Int32BitsToFloat((int)hi);
            }
        }

        private static int FloatToInt32Bits(float value)
        {
            FloatIntUnion union = new FloatIntUnion { FloatValue = value };
            return union.IntValue;
        }

        private static float Int32BitsToFloat(int value)
        {
            FloatIntUnion union = new FloatIntUnion { IntValue = value };
            return union.FloatValue;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatIntUnion
        {
            [FieldOffset(0)] public float FloatValue;
            [FieldOffset(0)] public int IntValue;
        }

        private static string BuildSplitRouteFailureMessage(
            string prefix,
            int portAudioResult,
            int inputDeviceIndex,
            int outputDeviceIndex,
            int inputChannelCount,
            int outputChannelCount,
            int sampleRate,
            uint framesPerBuffer,
            double inputLatency,
            double outputLatency,
            string routeDescription)
        {
            return BuildSplitRouteFailureMessage(
                prefix,
                GetErrorText(portAudioResult),
                inputDeviceIndex,
                outputDeviceIndex,
                inputChannelCount,
                outputChannelCount,
                sampleRate,
                framesPerBuffer,
                inputLatency,
                outputLatency,
                routeDescription);
        }

        private static string BuildSplitRouteFailureMessage(
            string prefix,
            string detail,
            int inputDeviceIndex,
            int outputDeviceIndex,
            int inputChannelCount,
            int outputChannelCount,
            int sampleRate,
            uint framesPerBuffer,
            double inputLatency,
            double outputLatency,
            string routeDescription)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(prefix);
            if (!string.IsNullOrWhiteSpace(detail))
            {
                builder.Append(": ");
                builder.Append(detail.Trim());
            }
            builder.Append(" | route=");
            builder.Append(string.IsNullOrWhiteSpace(routeDescription)
                ? $"input #{inputDeviceIndex} -> output #{outputDeviceIndex}"
                : routeDescription.Trim());
            builder.Append(" | inputDeviceIndex=");
            builder.Append(inputDeviceIndex);
            builder.Append(", outputDeviceIndex=");
            builder.Append(outputDeviceIndex);
            builder.Append(", inputChannels=");
            builder.Append(inputChannelCount);
            builder.Append(", outputChannels=");
            builder.Append(outputChannelCount);
            builder.Append(", sampleRate=");
            builder.Append(sampleRate);
            builder.Append(", framesPerBuffer=");
            builder.Append(framesPerBuffer);
            builder.Append(", inputLatency=");
            builder.Append(inputLatency.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", outputLatency=");
            builder.Append(outputLatency.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    private static IReadOnlyList<DeviceDescriptor> GetPreferredDevices(IReadOnlyList<DeviceDescriptor> devices, Func<DeviceDescriptor, bool> predicate)
    {
        if (devices == null || devices.Count == 0)
            return Array.Empty<DeviceDescriptor>();

        List<DeviceDescriptor> filtered = devices.Where(predicate).ToList();
        if (filtered.Count == 0)
            return filtered;

        List<DeviceDescriptor> preferred = filtered
            .Where(device => IsPreferredHost(device.HostApiName))
            .OrderBy(device => GetHostPriority(device.HostApiName))
            .ThenBy(device => device.Index)
            .ToList();

        if (preferred.Count > 0)
            return preferred;

        return filtered.OrderBy(device => device.Index).ToList();
    }

    private static bool IsPreferredHost(string hostApiName)
    {
        return GetHostPriority(hostApiName) < int.MaxValue;
    }

    private static int GetHostPriority(string hostApiName)
    {
        if (string.IsNullOrWhiteSpace(hostApiName))
            return int.MaxValue;

        int priority = SharedAudioBackendModes.GetHostPriority(hostApiName);
        return priority >= 2 ? int.MaxValue : priority;
    }
}
