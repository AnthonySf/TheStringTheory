using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class NativeDetectorInputDevice
{
    public int index = -1;
    public string displayName = string.Empty;
    public string name = string.Empty;
    public string hostApiName = string.Empty;
    public int maxInputChannels;
    public float defaultSampleRate;
    public float defaultLowInputLatency;
}

[Serializable]
public sealed class NativeDetectorDeviceListPayload
{
    public int preferredDeviceIndex = -1;
    public NativeDetectorInputDevice[] devices = Array.Empty<NativeDetectorInputDevice>();
}

[Serializable]
public sealed class NativeDetectorRuntimeInfo
{
    public bool running;
    public string backendLabel = "Native C++ Detector";
    public int selectedInputDeviceIndex = -1;
    public string selectedInputDeviceDisplayName = string.Empty;
    public string selectedHostApiName = string.Empty;
    public string inputChannelMode = SharedAudioInputChannelModes.Input1;
    public int sampleRate = 22050;
    public int captureSampleRate;
    public int internalSampleRate = 22050;
    public string configuredResamplerMode = SharedAudioDetectorResamplerModes.Filtered;
    public string activeResamplerMode = "Direct";
    public bool resamplerToggleAvailable;
    public int hopSize = 512;
    public float captureSeconds = 0.3f;
    public float inputLevelNormalized;
    public string latestPacket = "--";
    public string statusText = "Native detector idle.";
    public string errorText = string.Empty;
}

public enum NativeDetectorResamplerMode
{
    Linear = 0,
    Filtered = 1
}

public sealed class NativeNotesDetectorBridge
{
    private const string NativeLibraryName = "NativeNotesDetectorBridgeNative_v6";
    private const int NativeBufferSize = 65536;
    private const int NativeInputChannelInput1 = 0;
    private const int NativeInputChannelInput2 = 1;
    private const int NativeInputChannelMonoMix = 2;

    private bool initialized;
    private bool presetStoreLoaded;
    private string lastStatus = "Native detector idle.";
    private string lastError = string.Empty;
    private NativeDetectorDeviceListPayload cachedDevices = new NativeDetectorDeviceListPayload();
    private NativeDetectorRuntimeInfo cachedRuntimeInfo = new NativeDetectorRuntimeInfo();
    private NativeDetectorPresetStore presetStore = new NativeDetectorPresetStore();
    private NativeDetectorSettingsData workingSettings = NativeDetectorSettingCatalog.CreateLevel2();
    private NativeDetectorResamplerMode preferredResamplerMode = NativeDetectorResamplerMode.Filtered;
    private string preferredInputChannelMode = SharedAudioInputChannelModes.Input1;

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NativeDetector_Initialize(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string modelPathUtf8,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dataDirectoryUtf8,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string reservedUtf8);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NativeDetector_Start(int inputDeviceIndex);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NativeDetector_StartWithInputChannel(int inputDeviceIndex, int inputChannelMode);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NativeDetector_Stop();

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NativeDetector_SetHintPayload([MarshalAs(UnmanagedType.LPUTF8Str)] string payloadUtf8);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NativeDetector_PollLatestPacket(StringBuilder destination, int capacity);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NativeDetector_GetStatus(StringBuilder destination, int capacity);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NativeDetector_GetLastError(StringBuilder destination, int capacity);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NativeDetector_IsRunning();

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NativeDetector_ListInputDevicesJson(StringBuilder destination, int capacity);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NativeDetector_GetRuntimeInfoJson(StringBuilder destination, int capacity);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NativeDetector_SetSettingsJson([MarshalAs(UnmanagedType.LPUTF8Str)] string settingsJsonUtf8);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NativeDetector_SetResamplerMode(int mode);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int NativeDetector_SetDebugLogPath([MarshalAs(UnmanagedType.LPUTF8Str)] string debugLogPathUtf8);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void NativeDetector_Shutdown();

    public string LastStatus => lastStatus;
    public string LastError => lastError;
    public NativeDetectorInputDevice[] InputDevices => cachedDevices?.devices ?? Array.Empty<NativeDetectorInputDevice>();
    public int PreferredInputDeviceIndex => cachedDevices != null ? cachedDevices.preferredDeviceIndex : -1;
    public NativeDetectorRuntimeInfo RuntimeInfo => cachedRuntimeInfo ?? new NativeDetectorRuntimeInfo();
    public string SelectedPresetId
    {
        get
        {
            EnsurePresetStoreLoaded();
            return presetStore.selectedPresetId;
        }
    }
    public string SelectedPresetLabel => NativeDetectorSettingCatalog.GetPresetLabel(SelectedPresetId);
    public int SelectedPresetIndex => NativeDetectorSettingCatalog.GetPresetIndex(SelectedPresetId);
    public IReadOnlyList<NativeDetectorPresetDescriptor> Presets => NativeDetectorSettingCatalog.PresetDescriptors;
    public NativeDetectorSettingsData WorkingSettings
    {
        get
        {
            EnsurePresetStoreLoaded();
            return NativeDetectorSettingCatalog.Clone(workingSettings);
        }
    }

    public bool Initialize()
    {
        EnsurePresetStoreLoaded();

        if (initialized)
        {
            ApplyWorkingSettingsToNative();
            ApplyPreferredResamplerModeToNative();
            return true;
        }

        try
        {
            string modelPath = Path.Combine(Application.streamingAssetsPath, "NotesReader", "Models", "basic_pitch_nmp.onnx");
            if (!File.Exists(modelPath))
            {
                lastError = $"Detector model not found: {modelPath}";
                lastStatus = lastError;
                return false;
            }

            int result = NativeDetector_Initialize(modelPath, Application.persistentDataPath, string.Empty);
            initialized = result != 0;
            if (initialized)
            {
                ApplyWorkingSettingsToNative();
                ApplyPreferredResamplerModeToNative();
            }
            RefreshNativeStatus();
            RefreshDevices();

            if (!initialized && string.IsNullOrWhiteSpace(lastError))
            {
                lastError = "Native detector initialization failed.";
                lastStatus = lastError;
            }

            return initialized;
        }
        catch (DllNotFoundException ex)
        {
            lastError = $"Native detector DLL not found: {ex.Message}";
            lastStatus = lastError;
            return false;
        }
        catch (EntryPointNotFoundException ex)
        {
            lastError = $"Native detector entry point missing: {ex.Message}";
            lastStatus = lastError;
            return false;
        }
        catch (Exception ex)
        {
            lastError = $"Native detector init failed: {ex.Message}";
            lastStatus = lastError;
            return false;
        }
    }

    public bool Start(int inputDeviceIndex = -1)
    {
        return Start(inputDeviceIndex, preferredInputChannelMode);
    }

    public bool Start(int inputDeviceIndex, string inputChannelMode)
    {
        if (!Initialize())
            return false;

        try
        {
            preferredInputChannelMode = SharedAudioInputChannelModes.Normalize(inputChannelMode);
            ApplyWorkingSettingsToNative();
            ApplyPreferredResamplerModeToNative();
            RefreshDevices();

            List<int> candidates = BuildInputDeviceStartCandidates(inputDeviceIndex);
            List<string> attemptErrors = new List<string>();
            int firstCandidate = candidates.Count > 0 ? candidates[0] : inputDeviceIndex;
            int nativeInputChannelMode = ToNativeInputChannelMode(preferredInputChannelMode);

            for (int i = 0; i < candidates.Count; i++)
            {
                int candidate = candidates[i];
                bool ok = StartNative(candidate, nativeInputChannelMode);
                RefreshNativeStatus();
                if (ok)
                {
                    if (i > 0)
                    {
                        Debug.LogWarning($"[NativeNotesDetector] Detector input '{DescribeInputDevice(firstCandidate)}' failed. Fell back to '{DescribeInputDevice(candidate)}'.");
                    }

                    return true;
                }

                string detail = string.IsNullOrWhiteSpace(lastError) ? lastStatus : lastError;
                attemptErrors.Add($"{DescribeInputDevice(candidate)} -> {detail}");
            }

            lastError = BuildStartFailureMessage(inputDeviceIndex, attemptErrors);
            lastStatus = lastError;
            Debug.LogWarning($"[NativeNotesDetector] {lastError}");
            return false;
        }
        catch (Exception ex)
        {
            lastError = $"Native detector start failed: {ex.Message}";
            lastStatus = lastError;
            return false;
        }
    }

    public void SetInputChannelMode(string inputChannelMode)
    {
        preferredInputChannelMode = SharedAudioInputChannelModes.Normalize(inputChannelMode);
    }

    private bool StartNative(int inputDeviceIndex, int inputChannelMode)
    {
        try
        {
            return NativeDetector_StartWithInputChannel(inputDeviceIndex, inputChannelMode) != 0;
        }
        catch (EntryPointNotFoundException)
        {
            if (inputChannelMode == NativeInputChannelInput1)
                return NativeDetector_Start(inputDeviceIndex) != 0;

            lastError = "Native detector DLL does not support input channel selection.";
            lastStatus = lastError;
            return false;
        }
    }

    private static int ToNativeInputChannelMode(string inputChannelMode)
    {
        switch (SharedAudioInputChannelModes.Normalize(inputChannelMode))
        {
            case SharedAudioInputChannelModes.Input2:
                return NativeInputChannelInput2;
            case SharedAudioInputChannelModes.MonoMix:
                return NativeInputChannelMonoMix;
            case SharedAudioInputChannelModes.Input1:
            default:
                return NativeInputChannelInput1;
        }
    }

    private List<int> BuildInputDeviceStartCandidates(int requestedInputDeviceIndex)
    {
        List<int> candidates = new List<int>();
        NativeDetectorInputDevice[] devices = InputDevices;

        if (requestedInputDeviceIndex >= 0)
            AddInputDeviceCandidate(candidates, requestedInputDeviceIndex);

        if (PreferredInputDeviceIndex == requestedInputDeviceIndex || IsLowLatencyInputHost(PreferredInputDeviceIndex))
            AddInputDeviceCandidate(candidates, PreferredInputDeviceIndex);

        AddInputDeviceCandidatesByHost(candidates, devices, "Core Audio");
        AddInputDeviceCandidatesByHost(candidates, devices, "CoreAudio");
        AddInputDeviceCandidatesByHost(candidates, devices, "WASAPI");
        AddInputDeviceCandidatesByHost(candidates, devices, "DirectSound");
        AddInputDeviceCandidatesByHost(candidates, devices, "MME");
        AddInputDeviceCandidatesByHost(candidates, devices, "Windows");
        AddInputDeviceCandidatesByHost(candidates, devices, string.Empty);

        if (candidates.Count == 0)
            candidates.Add(requestedInputDeviceIndex);

        return candidates;
    }

    private bool IsLowLatencyInputHost(int deviceIndex)
    {
        NativeDetectorInputDevice[] devices = InputDevices;
        for (int i = 0; i < devices.Length; i++)
        {
            NativeDetectorInputDevice device = devices[i];
            if (device == null || device.index != deviceIndex)
                continue;

            string host = device.hostApiName ?? string.Empty;
            return host.IndexOf("ASIO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   host.IndexOf("Core Audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   host.IndexOf("CoreAudio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   host.IndexOf("WASAPI", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        return false;
    }

    private void AddInputDeviceCandidatesByHost(List<int> candidates, NativeDetectorInputDevice[] devices, string hostName)
    {
        if (devices == null)
            return;

        for (int i = 0; i < devices.Length; i++)
        {
            NativeDetectorInputDevice device = devices[i];
            if (device == null)
                continue;

            string deviceHost = device.hostApiName ?? string.Empty;
            bool matches = string.IsNullOrEmpty(hostName)
                ? true
                : deviceHost.IndexOf(hostName, StringComparison.OrdinalIgnoreCase) >= 0;
            if (matches)
                AddInputDeviceCandidate(candidates, device.index);
        }
    }

    private void AddInputDeviceCandidate(List<int> candidates, int deviceIndex)
    {
        if (deviceIndex < 0 || candidates == null || candidates.Contains(deviceIndex))
            return;

        if (!HasInputDevice(deviceIndex))
            return;

        candidates.Add(deviceIndex);
    }

    private bool HasInputDevice(int deviceIndex)
    {
        NativeDetectorInputDevice[] devices = InputDevices;
        for (int i = 0; i < devices.Length; i++)
        {
            NativeDetectorInputDevice device = devices[i];
            if (device != null && device.index == deviceIndex)
                return true;
        }

        return false;
    }

    private string BuildStartFailureMessage(int requestedInputDeviceIndex, List<string> attemptErrors)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("Unable to open detector input stream on any available input.");
        builder.Append(" Requested: ");
        builder.Append(DescribeInputDevice(requestedInputDeviceIndex));

        if (attemptErrors != null && attemptErrors.Count > 0)
        {
            builder.Append(". Attempts: ");
            int limit = Math.Min(attemptErrors.Count, 4);
            for (int i = 0; i < limit; i++)
            {
                if (i > 0)
                    builder.Append(" | ");
                builder.Append(attemptErrors[i]);
            }

            if (attemptErrors.Count > limit)
            {
                builder.Append(" | ");
                builder.Append(attemptErrors.Count - limit);
                builder.Append(" more input(s) failed.");
            }
        }

        builder.Append(". Available inputs: ");
        builder.Append(BuildAvailableInputDeviceSummary());
        return builder.ToString();
    }

    private string BuildAvailableInputDeviceSummary()
    {
        NativeDetectorInputDevice[] devices = InputDevices;
        if (devices == null || devices.Length == 0)
            return "none";

        StringBuilder builder = new StringBuilder();
        int limit = Math.Min(devices.Length, 8);
        for (int i = 0; i < limit; i++)
        {
            if (i > 0)
                builder.Append(" | ");
            builder.Append(DescribeInputDevice(devices[i]));
        }

        if (devices.Length > limit)
        {
            builder.Append(" | ");
            builder.Append(devices.Length - limit);
            builder.Append(" more");
        }

        return builder.ToString();
    }

    private string DescribeInputDevice(int deviceIndex)
    {
        if (deviceIndex < 0)
            return "Automatic";

        NativeDetectorInputDevice[] devices = InputDevices;
        for (int i = 0; i < devices.Length; i++)
        {
            NativeDetectorInputDevice device = devices[i];
            if (device != null && device.index == deviceIndex)
                return DescribeInputDevice(device);
        }

        return $"Device {deviceIndex}";
    }

    private static string DescribeInputDevice(NativeDetectorInputDevice device)
    {
        if (device == null)
            return "Unknown";

        string label = string.IsNullOrWhiteSpace(device.displayName) ? device.name : device.displayName;
        if (string.IsNullOrWhiteSpace(label))
            label = $"Device {device.index}";

        if (!string.IsNullOrWhiteSpace(device.hostApiName))
            return $"{label} ({device.hostApiName})";

        return label;
    }

    public void Stop()
    {
        if (!initialized)
            return;

        try
        {
            NativeDetector_Stop();
            RefreshNativeStatus();
        }
        catch (Exception ex)
        {
            lastError = $"Native detector stop failed: {ex.Message}";
            lastStatus = lastError;
        }
    }

    public void Shutdown(bool forceNativeShutdown = false)
    {
        if (!initialized && !forceNativeShutdown)
            return;

        try
        {
            NativeDetector_Shutdown();
        }
        catch
        {
        }

        initialized = false;
    }

    public List<string> BuildPresetLabels()
    {
        return NativeDetectorSettingCatalog.BuildPresetLabels();
    }

    public List<NativeDetectorSettingSnapshot> BuildSettingSnapshots()
    {
        EnsurePresetStoreLoaded();
        return NativeDetectorSettingCatalog.BuildSettingSnapshots(workingSettings);
    }

    public void SelectPresetByIndex(int presetIndex)
    {
        EnsurePresetStoreLoaded();
        string presetId = NativeDetectorSettingCatalog.GetPresetIdAtIndex(presetIndex);
        presetStore.selectedPresetId = presetId;
        workingSettings = ResolveSettingsForPresetId(presetId);
        SavePresetStore();
        ApplyWorkingSettingsToNative();
        RefreshNativeStatus();
    }

    public void UpdateWorkingSetting(string key, float value)
    {
        EnsurePresetStoreLoaded();
        NativeDetectorSettingCatalog.ApplySettingValue(workingSettings, key, value);
        workingSettings = NativeDetectorSettingCatalog.Sanitize(workingSettings);
        ApplyWorkingSettingsToNative();
    }

    public void SaveWorkingSettingsToCustomPreset()
    {
        EnsurePresetStoreLoaded();
        presetStore.customSettings = NativeDetectorSettingCatalog.Clone(workingSettings);
        presetStore.selectedPresetId = NativeDetectorSettingCatalog.CustomPresetId;
        workingSettings = ResolveSettingsForPresetId(presetStore.selectedPresetId);
        SavePresetStore();
        ApplyWorkingSettingsToNative();
        RefreshNativeStatus();
    }

    public void RestoreSelectedPresetWorkingSettings()
    {
        EnsurePresetStoreLoaded();
        workingSettings = ResolveSettingsForPresetId(presetStore.selectedPresetId);
        ApplyWorkingSettingsToNative();
    }

    public bool SetHintPayload(string payload)
    {
        if (!initialized)
            return false;

        try
        {
            return NativeDetector_SetHintPayload(payload ?? string.Empty) != 0;
        }
        catch (Exception ex)
        {
            lastError = $"Native detector hint send failed: {ex.Message}";
            return false;
        }
    }

    public bool SetDebugLogPath(string debugLogPath)
    {
        if (!initialized)
            return false;

        try
        {
            return NativeDetector_SetDebugLogPath(debugLogPath ?? string.Empty) != 0;
        }
        catch (Exception ex)
        {
            lastError = $"Native detector debug log path set failed: {ex.Message}";
            lastStatus = lastError;
            return false;
        }
    }

    public string PollLatestPacket()
    {
        if (!initialized)
            return string.Empty;

        try
        {
            StringBuilder builder = new StringBuilder(NativeBufferSize);
            if (NativeDetector_PollLatestPacket(builder, builder.Capacity) != 0)
                return builder.ToString();
        }
        catch (Exception ex)
        {
            lastError = $"Native detector poll failed: {ex.Message}";
            lastStatus = lastError;
        }

        return string.Empty;
    }

    public bool IsRunning()
    {
        if (!initialized)
            return false;

        try
        {
            return NativeDetector_IsRunning() != 0;
        }
        catch
        {
            return false;
        }
    }

    public NativeDetectorDeviceListPayload RefreshDevices(bool forcePortAudioRescan = false)
    {
        bool restartAfterRescan = forcePortAudioRescan && IsRunning();
        int restartDeviceIndex = cachedRuntimeInfo?.selectedInputDeviceIndex ?? -1;
        string restartChannelMode = preferredInputChannelMode;
        if (forcePortAudioRescan)
        {
            Shutdown(forceNativeShutdown: true);
        }

        if (!Initialize())
            return cachedDevices;

        try
        {
            StringBuilder builder = new StringBuilder(NativeBufferSize);
            if (NativeDetector_ListInputDevicesJson(builder, builder.Capacity) != 0)
            {
                string json = builder.ToString();
                NativeDetectorDeviceListPayload parsed = JsonUtility.FromJson<NativeDetectorDeviceListPayload>(json);
                if (parsed != null)
                    cachedDevices = parsed;
            }
        }
        catch (Exception ex)
        {
            lastError = $"Native detector device query failed: {ex.Message}";
            lastStatus = lastError;
        }

        if (cachedDevices == null)
            cachedDevices = new NativeDetectorDeviceListPayload();

        if (restartAfterRescan)
            Start(restartDeviceIndex, restartChannelMode);

        return cachedDevices;
    }

    public NativeDetectorRuntimeInfo RefreshRuntimeInfo()
    {
        if (!initialized)
            return cachedRuntimeInfo;

        try
        {
            StringBuilder builder = new StringBuilder(NativeBufferSize);
            if (NativeDetector_GetRuntimeInfoJson(builder, builder.Capacity) != 0)
            {
                string json = builder.ToString();
                NativeDetectorRuntimeInfo parsed = JsonUtility.FromJson<NativeDetectorRuntimeInfo>(json);
                if (parsed != null)
                    cachedRuntimeInfo = parsed;
            }
        }
        catch (Exception ex)
        {
            lastError = $"Native detector runtime info query failed: {ex.Message}";
            lastStatus = lastError;
        }

        if (cachedRuntimeInfo == null)
            cachedRuntimeInfo = new NativeDetectorRuntimeInfo();

        return cachedRuntimeInfo;
    }

    public void RefreshNativeStatus()
    {
        if (!initialized)
            return;

        try
        {
            StringBuilder statusBuilder = new StringBuilder(NativeBufferSize);
            if (NativeDetector_GetStatus(statusBuilder, statusBuilder.Capacity) != 0)
                lastStatus = statusBuilder.ToString();

            StringBuilder errorBuilder = new StringBuilder(NativeBufferSize);
            if (NativeDetector_GetLastError(errorBuilder, errorBuilder.Capacity) != 0)
                lastError = errorBuilder.ToString();

            NativeDetectorRuntimeInfo info = RefreshRuntimeInfo();
            if (info != null)
            {
                if (!string.IsNullOrWhiteSpace(info.statusText))
                    lastStatus = info.statusText;
                if (!string.IsNullOrWhiteSpace(info.errorText))
                    lastError = info.errorText;
            }
        }
        catch (Exception ex)
        {
            lastError = $"Native detector status query failed: {ex.Message}";
            lastStatus = lastError;
        }
    }

    public void SetResamplerMode(NativeDetectorResamplerMode mode)
    {
        preferredResamplerMode = NormalizeResamplerMode(mode);

        if (!initialized)
            return;

        try
        {
            ApplyPreferredResamplerModeToNative();
            RefreshNativeStatus();
        }
        catch (Exception ex)
        {
            lastError = $"Native detector resampler update failed: {ex.Message}";
            lastStatus = lastError;
        }
    }

    public string GetInputDeviceDisplayName(int deviceIndex)
    {
        NativeDetectorInputDevice[] devices = InputDevices;
        for (int i = 0; i < devices.Length; i++)
        {
            NativeDetectorInputDevice device = devices[i];
            if (device != null && device.index == deviceIndex)
                return string.IsNullOrWhiteSpace(device.displayName) ? device.name : device.displayName;
        }

        return deviceIndex >= 0 ? $"Device {deviceIndex}" : "Auto";
    }

    private void EnsurePresetStoreLoaded()
    {
        if (presetStoreLoaded)
            return;

        presetStoreLoaded = true;
        presetStore = new NativeDetectorPresetStore();
        string presetStorePath = GetPresetStorePath();

        try
        {
            if (File.Exists(presetStorePath))
            {
                string json = File.ReadAllText(presetStorePath);
                NativeDetectorPresetStore parsed = JsonUtility.FromJson<NativeDetectorPresetStore>(json);
                if (parsed != null)
                    presetStore = parsed;
            }
        }
        catch (Exception ex)
        {
            lastError = $"Detector preset load failed: {ex.Message}";
        }

        if (presetStore == null)
            presetStore = new NativeDetectorPresetStore();

        if (string.IsNullOrWhiteSpace(presetStore.selectedPresetId))
            presetStore.selectedPresetId = NativeDetectorSettingCatalog.DefaultPresetId;

        if (presetStore.customSettings == null)
            presetStore.customSettings = NativeDetectorSettingCatalog.CreateLevel2();

        workingSettings = ResolveSettingsForPresetId(presetStore.selectedPresetId);
        SavePresetStore();
    }

    private NativeDetectorResamplerMode NormalizeResamplerMode(NativeDetectorResamplerMode mode)
    {
        return mode == NativeDetectorResamplerMode.Linear
            ? NativeDetectorResamplerMode.Linear
            : NativeDetectorResamplerMode.Filtered;
    }

    private void ApplyPreferredResamplerModeToNative()
    {
        if (!initialized)
            return;

        NativeDetector_SetResamplerMode((int)preferredResamplerMode);
    }

    private string GetPresetStorePath()
    {
        return Path.Combine(Application.persistentDataPath, "NotesDetector", "native_detector_presets.json");
    }

    private void SavePresetStore()
    {
        EnsurePresetStoreLoaded();

        try
        {
            string path = GetPresetStorePath();
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            presetStore.customSettings = NativeDetectorSettingCatalog.Sanitize(presetStore.customSettings);
            string json = JsonUtility.ToJson(presetStore, true);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            lastError = $"Detector preset save failed: {ex.Message}";
        }
    }

    private NativeDetectorSettingsData ResolveSettingsForPresetId(string presetId)
    {
        if (string.Equals(presetId, NativeDetectorSettingCatalog.CustomPresetId, StringComparison.OrdinalIgnoreCase))
            return NativeDetectorSettingCatalog.Sanitize(presetStore.customSettings);

        return NativeDetectorSettingCatalog.Sanitize(NativeDetectorSettingCatalog.CreatePresetById(presetId));
    }

    private void ApplyWorkingSettingsToNative()
    {
        EnsurePresetStoreLoaded();
        workingSettings = NativeDetectorSettingCatalog.Sanitize(workingSettings);

        if (!initialized)
            return;

        try
        {
            NativeDetector_SetSettingsJson(JsonUtility.ToJson(workingSettings));
        }
        catch (Exception ex)
        {
            lastError = $"Native detector settings apply failed: {ex.Message}";
            lastStatus = lastError;
        }
    }
}
