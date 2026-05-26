using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

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

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_IsFormatSupported(
        ref StreamParameters inputParameters,
        ref StreamParameters outputParameters,
        double sampleRate);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_StartStream(IntPtr stream);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_StopStream(IntPtr stream);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_CloseStream(IntPtr stream);

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
