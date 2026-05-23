using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class AudioPathRegressionTests
{
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void UnityTestRunnerReference_IsPresentForDiscovery()
    {
        Assert.IsNotNull(typeof(LogAssert));
    }

    [Test]
    public void SharedAudioSettingsNormalization_CoversBackendRatesResamplerAndDeviceKeys()
    {
        Assert.AreEqual(SharedAudioBackendModes.Auto, SharedAudioBackendModes.Normalize(null));
        Assert.AreEqual(SharedAudioBackendModes.Auto, SharedAudioBackendModes.Normalize(""));
        Assert.AreEqual(SharedAudioBackendModes.CoreAudio, SharedAudioBackendModes.Normalize("CoreAudio"));
        Assert.AreEqual(SharedAudioBackendModes.Asio, SharedAudioBackendModes.Normalize("asio"));
        Assert.AreEqual(SharedAudioBackendModes.Wasapi, SharedAudioBackendModes.Normalize("wasapi"));
        Assert.AreEqual(SharedAudioBackendModes.CoreAudio, SharedAudioBackendModes.NormalizeHostApiLabel("Core Audio"));
        Assert.AreEqual(0, SharedAudioBackendModes.GetHostPriority("Core Audio"));

        Assert.AreEqual(0, SharedAudioSampleRateOptions.Normalize(-1));
        Assert.AreEqual(0, SharedAudioSampleRateOptions.Normalize(12345));
        Assert.AreEqual(0, SharedAudioSampleRateOptions.Normalize(192000));
        Assert.AreEqual(44100, SharedAudioSampleRateOptions.Normalize(44100));
        Assert.AreEqual(48000, SharedAudioSampleRateOptions.Normalize(48000));
        Assert.AreEqual(96000, SharedAudioSampleRateOptions.Normalize(96000));

        Assert.AreEqual(SharedAudioDetectorResamplerModes.Filtered, SharedAudioDetectorResamplerModes.Normalize(null));
        Assert.AreEqual(SharedAudioDetectorResamplerModes.Filtered, SharedAudioDetectorResamplerModes.Normalize("junk"));
        Assert.AreEqual(SharedAudioDetectorResamplerModes.Linear, SharedAudioDetectorResamplerModes.Normalize("linear"));
        Assert.AreEqual(SharedAudioDetectorResamplerModes.Linear, SharedAudioDetectorResamplerModes.Toggle(SharedAudioDetectorResamplerModes.Filtered));
        Assert.AreEqual(SharedAudioDetectorResamplerModes.Filtered, SharedAudioDetectorResamplerModes.Toggle(SharedAudioDetectorResamplerModes.Linear));

        Assert.AreEqual("focusrite usb asio", SharedAudioSettingsUtility.NormalizeDeviceKey("003:  Focusrite   USB  ASIO "));
        Assert.AreEqual("realtek speakers", SharedAudioSettingsUtility.NormalizeDeviceKey("Realtek   Speakers"));
        Assert.AreEqual("Focusrite USB ASIO", SharedAudioSettingsUtility.NormalizeStoredDeviceName("  Focusrite   USB   ASIO  "));

        SharedAudioAdvancedSettings clone = SharedAudioSettingsUtility.CloneAdvancedSettings(new SharedAudioAdvancedSettings
        {
            betaEnabled = true,
            backendMode = "asio",
            allowFallback = false,
            inputDeviceName = "  2: Focusrite   Instrument  ",
            outputDeviceName = "  Focusrite   Output  ",
            sampleRate = 96000,
            bufferSize = 777,
            unifiedOutputEnabled = true,
            unityRecorderCaptureEnabled = true
        });

        Assert.IsTrue(clone.betaEnabled);
        Assert.AreEqual(SharedAudioBackendModes.Asio, clone.backendMode);
        Assert.IsFalse(clone.allowFallback);
        Assert.AreEqual("2: Focusrite Instrument", clone.inputDeviceName);
        Assert.AreEqual("Focusrite Output", clone.outputDeviceName);
        Assert.AreEqual(96000, clone.sampleRate);
        Assert.AreEqual(777, clone.bufferSize);
        Assert.IsTrue(clone.unifiedOutputEnabled);
        Assert.IsTrue(clone.unityRecorderCaptureEnabled);
    }

    [Test]
    public void SharedMonitoringLatencyLabels_RoundTripAndClampWeirdValues()
    {
        CollectionAssert.AreEqual(
            new[] { "Ultra Low (64)", "Low (128)", "Safe (256)" },
            UnityToneLabRuntime.SharedMonitoringLatencyOptions);

        Assert.AreEqual("Driver", UnityToneLabRuntime.GetSharedMonitoringLatencyLabel(-999));
        Assert.AreEqual("Driver", UnityToneLabRuntime.GetSharedMonitoringLatencyLabel(0));
        Assert.AreEqual("Ultra Low (64)", UnityToneLabRuntime.GetSharedMonitoringLatencyLabel(64));
        Assert.AreEqual("Low (128)", UnityToneLabRuntime.GetSharedMonitoringLatencyLabel(65));
        Assert.AreEqual("Low (128)", UnityToneLabRuntime.GetSharedMonitoringLatencyLabel(128));
        Assert.AreEqual("Safe (256)", UnityToneLabRuntime.GetSharedMonitoringLatencyLabel(256));
        Assert.AreEqual("Low (128)", UnityToneLabRuntime.GetSharedMonitoringLatencyLabel(2048));

        Assert.AreEqual(128, UnityToneLabRuntime.ParseSharedMonitoringLatencyBufferSize("Driver"));
        Assert.AreEqual(64, UnityToneLabRuntime.ParseSharedMonitoringLatencyBufferSize("Ultra Low (64)"));
        Assert.AreEqual(128, UnityToneLabRuntime.ParseSharedMonitoringLatencyBufferSize("Low (128)"));
        Assert.AreEqual(256, UnityToneLabRuntime.ParseSharedMonitoringLatencyBufferSize("Safe (256)"));
        Assert.AreEqual(128, UnityToneLabRuntime.ParseSharedMonitoringLatencyBufferSize("Unknown"));

        Assert.AreEqual(128, InvokeGuitarBridgeStatic<int>("NormalizeSharedMonitoringBufferSize", -1));
        Assert.AreEqual(128, InvokeGuitarBridgeStatic<int>("NormalizeSharedMonitoringBufferSize", 0));
        Assert.AreEqual(64, InvokeGuitarBridgeStatic<int>("NormalizeSharedMonitoringBufferSize", 64));
        Assert.AreEqual(128, InvokeGuitarBridgeStatic<int>("NormalizeSharedMonitoringBufferSize", 999));
        Assert.AreEqual(256, InvokeGuitarBridgeStatic<int>("NormalizeSharedMonitoringBufferSize", 256));
    }

    [Test]
    public void SharedAudioAdvancedSettingsNormalization_CoversInvalidAndAutomaticValues()
    {
        SharedAudioAdvancedSettings weird = new SharedAudioAdvancedSettings
        {
            betaEnabled = true,
            backendMode = "bad backend",
            allowFallback = false,
            inputDeviceName = "Automatic Input",
            outputDeviceName = "No output devices found",
            sampleRate = 12345,
            bufferSize = -256,
            unifiedOutputEnabled = true,
            unityRecorderCaptureEnabled = true
        };

        SharedAudioAdvancedSettings normalized = NormalizeAdvancedSettings(weird, fallbackBufferSize: 256, out bool changed);
        Assert.IsTrue(changed);
        Assert.IsTrue(normalized.betaEnabled);
        Assert.AreEqual(SharedAudioBackendModes.Auto, normalized.backendMode);
        Assert.IsFalse(normalized.allowFallback);
        Assert.AreEqual(string.Empty, normalized.inputDeviceName);
        Assert.AreEqual(string.Empty, normalized.outputDeviceName);
        Assert.AreEqual(0, normalized.sampleRate);
        Assert.AreEqual(256, normalized.bufferSize);
        Assert.IsTrue(normalized.unifiedOutputEnabled);
        Assert.IsTrue(normalized.unityRecorderCaptureEnabled);

        SharedAudioAdvancedSettings driverManaged = new SharedAudioAdvancedSettings
        {
            backendMode = SharedAudioBackendModes.Asio,
            inputDeviceName = "Focusrite USB [ASIO] (#7)",
            outputDeviceName = "Focusrite USB [ASIO] (#8)",
            sampleRate = 48000,
            bufferSize = 0
        };
        SharedAudioAdvancedSettings normalizedDriver = NormalizeAdvancedSettings(driverManaged, fallbackBufferSize: 64, out _);
        Assert.AreEqual(64, normalizedDriver.bufferSize, "Shared settings intentionally normalize Driver/0 to the current fallback latency. ASIO route candidates still add raw 0 separately.");
        Assert.AreEqual("Focusrite USB [ASIO] (#7)", normalizedDriver.inputDeviceName);
        Assert.AreEqual("Focusrite USB [ASIO] (#8)", normalizedDriver.outputDeviceName);

        SharedAudioAdvancedSettings nullNormalized = NormalizeAdvancedSettings(null, fallbackBufferSize: 999, out bool nullChanged);
        Assert.IsTrue(nullChanged);
        Assert.AreEqual(128, nullNormalized.bufferSize);
        Assert.AreEqual(SharedAudioBackendModes.Auto, nullNormalized.backendMode);
    }

    [Test]
    public void AdvancedDeviceNameHelpers_HandleDecoratedAsioWasapiAndLegacyLabels()
    {
        Assert.AreEqual(string.Empty, InvokeGuitarBridgeStatic<string>("NormalizeSharedAudioStoredSelection", "Automatic"));
        Assert.AreEqual(string.Empty, InvokeGuitarBridgeStatic<string>("NormalizeSharedAudioStoredSelection", "System Default Output"));
        Assert.AreEqual("Focusrite USB [ASIO] (#7)", InvokeGuitarBridgeStatic<string>("NormalizeSharedAudioStoredSelection", "  Focusrite   USB [ASIO] (#7)  "));

        Assert.AreEqual("Focusrite USB", InvokeGuitarBridgeStatic<string>("StripAdvancedAudioDeviceDecorations", "Focusrite USB [ASIO] (#7)"));
        Assert.AreEqual("Realtek Mic", InvokeGuitarBridgeStatic<string>("StripAdvancedAudioDeviceDecorations", "Realtek Mic [WASAPI] (#12)"));
        Assert.AreEqual("3: Legacy Device", InvokeGuitarBridgeStatic<string>("StripAdvancedAudioDeviceDecorations", "3: Legacy Device [MME] (#3)"));

        IReadOnlyList<string> choices = new[] { "Automatic", "3: Focusrite USB", "Realtek HD Audio", "4: Duplicate Name" };
        Assert.AreEqual("3: Focusrite USB", InvokeGuitarBridgeStatic<string>("ResolveChoiceByNormalizedName", choices, "Focusrite USB"));
        Assert.AreEqual("Realtek HD Audio", InvokeGuitarBridgeStatic<string>("ResolveChoiceByNormalizedName", choices, "000: Realtek   HD Audio"));
        Assert.AreEqual("4: Duplicate Name", InvokeGuitarBridgeStatic<string>("ResolveChoiceByNormalizedName", choices, "Duplicate Name"));
        Assert.AreEqual(string.Empty, InvokeGuitarBridgeStatic<string>("ResolveChoiceByNormalizedName", choices, "Missing"));
    }

    [Test]
    public void PortAudioDeviceResolution_CoversLegacyNamesDecoratedLabelsAndBackendFilters()
    {
        object asioInput = Device(7, "7: Focusrite USB ASIO", "Focusrite USB ASIO", "ASIO", 2, 0, 48000);
        object asioOutput = Device(8, "8: Focusrite USB ASIO", "Focusrite USB ASIO", "ASIO", 0, 2, 48000);
        object wasapiInput = Device(9, "9: Realtek HD Audio", "Realtek HD Audio", "Windows WASAPI", 2, 0, 44100);
        Array devices = DeviceArray(asioInput, asioOutput, wasapiInput);

        Assert.AreSame(asioInput, InvokeRuntimeStatic("ResolvePortAudioDevice", "7: Focusrite USB ASIO", devices));
        Assert.AreSame(asioInput, InvokeRuntimeStatic("ResolvePortAudioDevice", "Focusrite USB ASIO", devices));
        Assert.AreSame(wasapiInput, InvokeRuntimeStatic("ResolvePortAudioDevice", "000: Realtek HD Audio", devices));
        Assert.IsNull(InvokeRuntimeStatic("ResolvePortAudioDevice", "Missing Interface", devices));

        Assert.AreEqual("Focusrite USB ASIO [ASIO] (#7)", InvokeRuntimeStatic<string>("BuildAdvancedDeviceChoiceLabel", asioInput));
        Assert.AreEqual("Realtek HD Audio [WASAPI] (#9)", InvokeRuntimeStatic<string>("BuildAdvancedDeviceChoiceLabel", wasapiInput));
        Assert.AreEqual("focusrite usb asio [asio]", InvokeRuntimeStatic<string>("BuildAdvancedDeviceMatchKey", asioInput));
        Assert.AreEqual("focusrite usb asio [asio]", InvokeRuntimeStatic<string>("BuildAdvancedDeviceMatchKey", "Focusrite USB ASIO [ASIO] (#7)"));

        Assert.IsTrue(InvokeRuntimeStatic<bool>("MatchesBackendMode", "Steinberg ASIO", SharedAudioBackendModes.Asio));
        Assert.IsTrue(InvokeRuntimeStatic<bool>("MatchesBackendMode", "Windows WASAPI", SharedAudioBackendModes.Wasapi));
        Assert.IsTrue(InvokeRuntimeStatic<bool>("MatchesBackendMode", "Core Audio", SharedAudioBackendModes.CoreAudio));
        Assert.IsTrue(InvokeRuntimeStatic<bool>("MatchesBackendMode", "Windows WASAPI", SharedAudioBackendModes.Auto));
        Assert.IsFalse(InvokeRuntimeStatic<bool>("MatchesBackendMode", "Windows WASAPI", SharedAudioBackendModes.Asio));

        List<string> asioChoices = new List<string> { "Automatic" };
        using (RuntimeFixture fixture = RuntimeFixture.Create())
        {
            InvokeRuntimeInstance(fixture.Runtime, "AppendAdvancedDeviceChoices", asioChoices, devices, true, SharedAudioBackendModes.Asio);
        }

        CollectionAssert.AreEqual(new[] { "Automatic", "Focusrite USB ASIO [ASIO] (#7)" }, asioChoices);
    }

    [Test]
    public void PreferredPortAudioDevices_PrioritizeAsioThenWasapiThenOtherHosts()
    {
        object mme = Device(1, "1: Old MME Mic", "Old MME Mic", "MME", 2, 2, 44100);
        object wasapi = Device(2, "2: Realtek WASAPI", "Realtek WASAPI", "Windows WASAPI", 2, 2, 48000);
        object asio = Device(3, "3: Focusrite ASIO", "Focusrite ASIO", "ASIO", 2, 2, 48000);
        Array devices = DeviceArray(mme, wasapi, asio);

        IList preferredInputs = (IList)InvokePortAudioStatic("GetPreferredInputDevices", devices);
        IList preferredOutputs = (IList)InvokePortAudioStatic("GetPreferredOutputDevices", devices);

        Assert.AreEqual(3, GetDeviceIndex(preferredInputs[0]));
        Assert.AreEqual(2, GetDeviceIndex(preferredInputs[1]));
        Assert.AreEqual(3, GetDeviceIndex(preferredOutputs[0]));
        Assert.AreEqual(2, GetDeviceIndex(preferredOutputs[1]));
    }

    [Test]
    public void BufferAndSampleRateCandidateBuilders_CoverAsioDriverManagedAndFallbackCases()
    {
        object asioInput = Device(1, "1: ASIO Input", "ASIO Input", "ASIO", 2, 0, 96000);
        object asioOutput = Device(2, "2: ASIO Output", "ASIO Output", "ASIO", 0, 2, 48000);
        object wasapiInput = Device(3, "3: WASAPI Input", "WASAPI Input", "Windows WASAPI", 2, 0, 44100);
        object wasapiOutput = Device(4, "4: WASAPI Output", "WASAPI Output", "Windows WASAPI", 0, 2, 44100);

        CollectionAssert.AreEqual(
            new[] { 128, 0, 256, 64 },
            InvokeIntListRuntimeStatic("BuildBufferSizeCandidates", 512, true, asioInput, asioOutput));

        CollectionAssert.AreEqual(
            new[] { 64, 0 },
            InvokeIntListRuntimeStatic("BuildBufferSizeCandidates", 64, false, asioInput, asioOutput));

        CollectionAssert.AreEqual(
            new[] { 128, 256, 64 },
            InvokeIntListRuntimeStatic("BuildBufferSizeCandidates", 999, true, wasapiInput, wasapiOutput));

        CollectionAssert.AreEqual(
            new[] { 128 },
            InvokeIntListRuntimeStatic("BuildBufferSizeCandidates", -1, false, wasapiInput, wasapiOutput));

        CollectionAssert.AreEqual(
            new[] { 44100 },
            InvokeIntListRuntimeStatic("BuildSampleRateCandidates", 96000, asioInput, asioOutput, true, 44100));

        CollectionAssert.AreEqual(
            new[] { 96000, 48000, 44100 },
            InvokeIntListRuntimeStatic("BuildSampleRateCandidates", 96000, asioInput, asioOutput, true, 0));

        CollectionAssert.AreEqual(
            new[] { 48000 },
            InvokeIntListRuntimeStatic("BuildSampleRateCandidates", 0, null, null, false, 0));
    }

    [Test]
    public void AdvancedRoutePlans_SimulateAsioWasapiFallbackAndExplicitSelection()
    {
        object asioInput = Device(1, "1: Focusrite Inst", "Focusrite Inst", "ASIO", 2, 0, 48000);
        object asioOutput = Device(2, "2: Focusrite Out", "Focusrite Out", "ASIO", 0, 2, 48000);
        object wasapiInput = Device(3, "3: Realtek Mic", "Realtek Mic", "Windows WASAPI", 2, 0, 44100);
        object wasapiOutput = Device(4, "4: Realtek Speakers", "Realtek Speakers", "Windows WASAPI", 0, 2, 44100);
        object mmeInput = Device(5, "5: MME Mic", "MME Mic", "MME", 1, 0, 22050);
        object mmeOutput = Device(6, "6: MME Speakers", "MME Speakers", "MME", 0, 2, 22050);
        Array allDevices = DeviceArray(mmeInput, wasapiInput, asioOutput, asioInput, mmeOutput, wasapiOutput);

        using (RuntimeFixture fixture = RuntimeFixture.Create())
        {
            SetRuntimeRoutingFields(fixture.Runtime, allDevices, SharedAudioBackendModes.Auto, allowFallback: true, input: string.Empty, output: string.Empty, sampleRate: 0, bufferSize: 999);
            IList autoPlans = (IList)InvokeRuntimeInstance(fixture.Runtime, "BuildAdvancedRoutePlans");
            Assert.Greater(autoPlans.Count, 0);
            Assert.AreEqual(SharedAudioBackendModes.Asio, NormalizeHost(GetDeviceHost(GetPlanDevice(autoPlans[0], "InputDevice"))));
            Assert.AreEqual(SharedAudioBackendModes.Asio, NormalizeHost(GetDeviceHost(GetPlanDevice(autoPlans[0], "OutputDevice"))));
            Assert.IsTrue(autoPlans.Cast<object>().Any(plan => GetPlanInt(plan, "BufferSize") == 0), "ASIO plans should include driver-managed buffer 0 as a fallback candidate.");
            AssertAllPlansUseSameInputAndOutputHost(autoPlans);

            SetRuntimeRoutingFields(
                fixture.Runtime,
                allDevices,
                SharedAudioBackendModes.Wasapi,
                allowFallback: false,
                input: "Realtek Mic [WASAPI] (#3)",
                output: "Realtek Speakers [WASAPI] (#4)",
                sampleRate: 44100,
                bufferSize: 64);
            IList wasapiPlans = (IList)InvokeRuntimeInstance(fixture.Runtime, "BuildAdvancedRoutePlans");
            Assert.Greater(wasapiPlans.Count, 0);
            Assert.IsTrue(wasapiPlans.Cast<object>().All(plan => NormalizeHost(GetDeviceHost(GetPlanDevice(plan, "InputDevice"))) == SharedAudioBackendModes.Wasapi));
            Assert.IsTrue(wasapiPlans.Cast<object>().All(plan => NormalizeHost(GetDeviceHost(GetPlanDevice(plan, "OutputDevice"))) == SharedAudioBackendModes.Wasapi));
            Assert.IsTrue(wasapiPlans.Cast<object>().All(plan => GetPlanInt(plan, "BufferSize") == 64));
            Assert.IsTrue(wasapiPlans.Cast<object>().All(plan => GetPlanInt(plan, "SampleRate") == 44100));
        }
    }

    [Test]
    public void PortAudioChannelCopying_HandlesMonoStereoTruncatedAndNullBuffers()
    {
        float[] monoInput = { 0.25f, -0.5f, 0.75f };
        float[] stereoProcess = Enumerable.Repeat(99f, 6).ToArray();
        InvokeRuntimeStatic("FillProcessBufferFromPortAudioInput", monoInput, 1, 2, 3, stereoProcess, stereoProcess.Length);
        CollectionAssert.AreEqual(new[] { 0.25f, 0.25f, -0.5f, -0.5f, 0.75f, 0.75f }, stereoProcess);

        float[] stereoInput = { 0.1f, 0.2f, 0.3f };
        float[] quadProcess = Enumerable.Repeat(9f, 8).ToArray();
        InvokeRuntimeStatic("FillProcessBufferFromPortAudioInput", stereoInput, 2, 4, 2, quadProcess, quadProcess.Length);
        CollectionAssert.AreEqual(new[] { 0.1f, 0.2f, 0.2f, 0.2f, 0.3f, 0.3f, 0.3f, 0.3f }, quadProcess);

        float[] cleared = { 1f, 2f, 3f, 4f };
        InvokeRuntimeStatic("FillProcessBufferFromPortAudioInput", null, 2, 2, 2, cleared, cleared.Length);
        CollectionAssert.AreEqual(new[] { 0f, 0f, 0f, 0f }, cleared);

        float[] monoProcessed = { -0.1f, 0.5f, 0.9f };
        float[] stereoOutput = Enumerable.Repeat(4f, 6).ToArray();
        InvokeRuntimeStatic("FillPortAudioOutputBufferFromProcessedAudio", monoProcessed, 1, 3, stereoOutput, 2);
        CollectionAssert.AreEqual(new[] { -0.1f, -0.1f, 0.5f, 0.5f, 0.9f, 0.9f }, stereoOutput);

        float[] stereoProcessed = { 0.1f, 0.2f, 0.3f, 0.4f };
        float[] quadOutput = Enumerable.Repeat(8f, 8).ToArray();
        InvokeRuntimeStatic("FillPortAudioOutputBufferFromProcessedAudio", stereoProcessed, 2, 2, quadOutput, 4);
        CollectionAssert.AreEqual(new[] { 0.1f, 0.2f, 0.1f, 0.2f, 0.3f, 0.4f, 0.3f, 0.4f }, quadOutput);
    }

    [Test]
    public void NativeDetectorInputCandidates_PrioritizeRequestedPreferredAndWindowsFallbackHosts()
    {
        NativeNotesDetectorBridge bridge = new NativeNotesDetectorBridge();
        NativeDetectorDeviceListPayload payload = new NativeDetectorDeviceListPayload
        {
            preferredDeviceIndex = 7,
            devices = new[]
            {
                DetectorDevice(1, "ASIO Instrument", "ASIO"),
                DetectorDevice(2, "WASAPI Mic", "Windows WASAPI"),
                DetectorDevice(3, "DirectSound Mic", "DirectSound"),
                DetectorDevice(4, "MME Mic", "MME"),
                DetectorDevice(5, "Windows Mic", "Windows Audio"),
                DetectorDevice(6, "Unknown Host Mic", ""),
                DetectorDevice(7, "Preferred WASAPI", "WASAPI")
            }
        };

        SetField(bridge, "cachedDevices", payload);

        CollectionAssert.AreEqual(
            new[] { 1, 7, 2, 3, 4, 5, 6 },
            InvokeIntListInstance(bridge, "BuildInputDeviceStartCandidates", 1));

        CollectionAssert.AreEqual(
            new[] { 7, 2, 3, 4, 5, 1, 6 },
            InvokeIntListInstance(bridge, "BuildInputDeviceStartCandidates", -1));

        CollectionAssert.AreEqual(
            new[] { 7, 2, 3, 4, 5, 1, 6 },
            InvokeIntListInstance(bridge, "BuildInputDeviceStartCandidates", 99));

        string summary = (string)InvokeInstance(bridge, "BuildAvailableInputDeviceSummary");
        StringAssert.Contains("Preferred WASAPI", summary);
        StringAssert.Contains("ASIO Instrument", summary);
    }

    [Test]
    public void NativeDetectorInputCandidates_IncludeCoreAudioBeforeGenericFallback()
    {
        NativeNotesDetectorBridge bridge = new NativeNotesDetectorBridge();
        NativeDetectorDeviceListPayload payload = new NativeDetectorDeviceListPayload
        {
            preferredDeviceIndex = -1,
            devices = new[]
            {
                DetectorDevice(4, "Built-in Microphone", "Core Audio"),
                DetectorDevice(5, "Generic USB Input", "")
            }
        };

        SetField(bridge, "cachedDevices", payload);

        CollectionAssert.AreEqual(
            new[] { 4, 5 },
            InvokeIntListInstance(bridge, "BuildInputDeviceStartCandidates", -1));
    }

    [Test]
    public void ToneLabPedalProcessors_ProcessSyntheticAudioAcrossExtremeBuffersWithoutExploding()
    {
        int[] sampleRates = { 22050, 44100, 48000, 96000 };
        int[] channelCounts = { 1, 2 };
        int[] frameCounts = { 1, 31, 64, 127, 128, 255, 1024 };

        foreach (IToneLabPedalDescriptor descriptor in ToneLabPedalRegistry.AllDescriptors)
        {
            object[] settingVariants =
            {
                descriptor.CreateDefaultSettingsObject(),
                CreatePedalSettingsVariant(descriptor, valueSelector: parameter => parameter.MinimumValue),
                CreatePedalSettingsVariant(descriptor, valueSelector: parameter => parameter.MaximumValue),
                CreatePedalSettingsVariant(descriptor, valueSelector: parameter => (parameter.MinimumValue + parameter.MaximumValue) * 0.5f)
            };

            foreach (object settings in settingVariants)
            {
                string serialized = descriptor.SerializeSettingsObject(settings);
                Assert.IsNotEmpty(serialized, $"{descriptor.DisplayName} should serialize settings.");
                object roundTrippedSettings = descriptor.DeserializeSettingsObject(serialized);
                Assert.IsNotNull(roundTrippedSettings, $"{descriptor.DisplayName} should deserialize settings.");

                foreach (int sampleRate in sampleRates)
                {
                    foreach (int channels in channelCounts)
                    {
                        foreach (int frameCount in frameCounts)
                        {
                            IToneLabPedalProcessor processor = descriptor.CreateProcessor();
                            float[] buffer = CreateSyntheticGuitarBuffer(frameCount, channels, sampleRate, 82.4069f, 0.18f);
                            processor.Prepare(sampleRate, channels);
                            processor.Reset();
                            processor.ApplySettings(roundTrippedSettings);
                            Assert.DoesNotThrow(
                                () => processor.Process(buffer, channels, sampleRate),
                                $"{descriptor.DisplayName} failed for {sampleRate} Hz, {channels} channel(s), {frameCount} frame(s).");
                            AssertFiniteAndReasonable(buffer, $"{descriptor.DisplayName} {sampleRate} Hz {channels}ch {frameCount}f");
                        }
                    }
                }
            }
        }
    }

    [Test]
    public void ToneLabParameterDefinitions_ClampAndFormatExtremeValues()
    {
        foreach (IToneLabPedalDescriptor descriptor in ToneLabPedalRegistry.AllDescriptors)
        {
            object settings = descriptor.CreateDefaultSettingsObject();
            Assert.IsNotNull(descriptor.Appearance, $"{descriptor.DisplayName} should expose an appearance.");

            foreach (ToneLabPedalParameterDefinition parameter in descriptor.Parameters)
            {
                parameter.SetValue(settings, parameter.MinimumValue - 100000f);
                Assert.AreEqual(parameter.MinimumValue, parameter.GetValue(settings), 0.0001f, $"{descriptor.DisplayName}.{parameter.ParameterId} should clamp to min.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(parameter.Formatter(parameter.MinimumValue)), $"{descriptor.DisplayName}.{parameter.ParameterId} min formatter should return text.");

                parameter.SetValue(settings, parameter.MaximumValue + 100000f);
                Assert.AreEqual(parameter.MaximumValue, parameter.GetValue(settings), 0.0001f, $"{descriptor.DisplayName}.{parameter.ParameterId} should clamp to max.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(parameter.Formatter(parameter.MaximumValue)), $"{descriptor.DisplayName}.{parameter.ParameterId} max formatter should return text.");
            }
        }
    }

    private static object CreatePedalSettingsVariant(IToneLabPedalDescriptor descriptor, Func<ToneLabPedalParameterDefinition, float> valueSelector)
    {
        object settings = descriptor.CreateDefaultSettingsObject();
        foreach (ToneLabPedalParameterDefinition parameter in descriptor.Parameters)
            parameter.SetValue(settings, valueSelector(parameter));
        return settings;
    }

    private static float[] CreateSyntheticGuitarBuffer(int frames, int channels, int sampleRate, float frequencyHz, float amplitude)
    {
        float[] data = new float[Mathf.Max(0, frames) * Mathf.Max(1, channels)];
        int safeChannels = Mathf.Max(1, channels);
        int safeSampleRate = Mathf.Max(1, sampleRate);
        for (int frame = 0; frame < frames; frame++)
        {
            float t = frame / (float)safeSampleRate;
            float fundamental = Mathf.Sin(2f * Mathf.PI * frequencyHz * t);
            float second = 0.45f * Mathf.Sin(2f * Mathf.PI * frequencyHz * 2f * t);
            float pickTransient = Mathf.Exp(-frame / Mathf.Max(1f, safeSampleRate * 0.015f)) * 0.35f;
            float sample = amplitude * (fundamental + second + pickTransient);
            for (int channel = 0; channel < safeChannels; channel++)
            {
                float channelScale = channel == 0 ? 1f : 0.82f;
                data[(frame * safeChannels) + channel] = sample * channelScale;
            }
        }

        return data;
    }

    private static void AssertFiniteAndReasonable(float[] buffer, string context)
    {
        Assert.IsNotNull(buffer, context);
        float peak = 0f;
        for (int i = 0; i < buffer.Length; i++)
        {
            float sample = buffer[i];
            Assert.IsFalse(float.IsNaN(sample), $"{context} produced NaN at sample {i}.");
            Assert.IsFalse(float.IsInfinity(sample), $"{context} produced Infinity at sample {i}.");
            peak = Mathf.Max(peak, Mathf.Abs(sample));
        }

        Assert.Less(peak, 1000000f, $"{context} produced runaway audio.");
    }

    private static SharedAudioAdvancedSettings NormalizeAdvancedSettings(SharedAudioAdvancedSettings source, int fallbackBufferSize, out bool changed)
    {
        object[] args = { source, fallbackBufferSize, false };
        SharedAudioAdvancedSettings result = (SharedAudioAdvancedSettings)InvokeGuitarBridgeStatic("NormalizeSharedAudioAdvancedSettings", args);
        changed = (bool)args[2];
        return result;
    }

    private static NativeDetectorInputDevice DetectorDevice(int index, string name, string hostApi)
    {
        return new NativeDetectorInputDevice
        {
            index = index,
            displayName = name,
            name = name,
            hostApiName = hostApi,
            maxInputChannels = 2,
            defaultSampleRate = 48000f
        };
    }

    private static void SetRuntimeRoutingFields(UnityToneLabRuntime runtime, Array allDevices, string backendMode, bool allowFallback, string input, string output, int sampleRate, int bufferSize)
    {
        SetField(runtime, "settings", new UnityToneLabRuntime.ToneLabSettings
        {
            input_device_name = input,
            output_device_name = output,
            monitoring_buffer_size = bufferSize
        });
        SetField(runtime, "settingsLoaded", true);
        SetField(runtime, "portAudioAllDevices", allDevices);
        SetField(runtime, "advancedRoutingOptions", new UnityToneLabRuntime.AdvancedRoutingOptions
        {
            betaEnabled = true,
            backendMode = backendMode,
            allowFallback = allowFallback,
            preferredInputDeviceName = input,
            preferredOutputDeviceName = output,
            sampleRate = sampleRate,
            bufferSize = bufferSize
        });
    }

    private static void AssertAllPlansUseSameInputAndOutputHost(IList plans)
    {
        foreach (object plan in plans)
        {
            string inputHost = NormalizeHost(GetDeviceHost(GetPlanDevice(plan, "InputDevice")));
            string outputHost = NormalizeHost(GetDeviceHost(GetPlanDevice(plan, "OutputDevice")));
            Assert.AreEqual(inputHost, outputHost, "PortAudio route plans should not pair ASIO input with WASAPI/MME output.");
        }
    }

    private static object GetPlanDevice(object plan, string fieldName)
    {
        return GetField(plan, fieldName);
    }

    private static int GetPlanInt(object plan, string fieldName)
    {
        return Convert.ToInt32(GetField(plan, fieldName));
    }

    private static int GetDeviceIndex(object device)
    {
        return Convert.ToInt32(GetProperty(device, "Index"));
    }

    private static string GetDeviceHost(object device)
    {
        return (string)GetProperty(device, "HostApiName");
    }

    private static string NormalizeHost(string hostApi)
    {
        return InvokeRuntimeStatic<string>("GetNormalizedHostApiLabel", hostApi);
    }

    private static object Device(int index, string displayName, string name, string hostApiName, int maxInputChannels, int maxOutputChannels, double defaultSampleRate)
    {
        object device = Activator.CreateInstance(DeviceDescriptorType, true);
        SetProperty(device, "Index", index);
        SetProperty(device, "DisplayName", displayName);
        SetProperty(device, "Name", name);
        SetProperty(device, "HostApiName", hostApiName);
        SetProperty(device, "MaxInputChannels", maxInputChannels);
        SetProperty(device, "MaxOutputChannels", maxOutputChannels);
        SetProperty(device, "DefaultSampleRate", defaultSampleRate);
        SetProperty(device, "DefaultLowInputLatency", 0.001d);
        SetProperty(device, "DefaultLowOutputLatency", 0.001d);
        return device;
    }

    private static Array DeviceArray(params object[] devices)
    {
        Array array = Array.CreateInstance(DeviceDescriptorType, devices.Length);
        for (int i = 0; i < devices.Length; i++)
            array.SetValue(devices[i], i);
        return array;
    }

    private static Type DeviceDescriptorType
    {
        get
        {
            Type type = typeof(UnityToneLabRuntime).Assembly.GetType("ToneLabPortAudio+DeviceDescriptor", throwOnError: true);
            Assert.IsNotNull(type);
            return type;
        }
    }

    private static object InvokeRuntimeStatic(string methodName, params object[] args)
    {
        return InvokeStatic(typeof(UnityToneLabRuntime), methodName, args);
    }

    private static T InvokeRuntimeStatic<T>(string methodName, params object[] args)
    {
        return (T)InvokeRuntimeStatic(methodName, args);
    }

    private static object InvokePortAudioStatic(string methodName, params object[] args)
    {
        Type type = typeof(UnityToneLabRuntime).Assembly.GetType("ToneLabPortAudio", throwOnError: true);
        return InvokeStatic(type, methodName, args);
    }

    private static object InvokeGuitarBridgeStatic(string methodName, params object[] args)
    {
        return InvokeStatic(typeof(GuitarBridgeServer), methodName, args);
    }

    private static T InvokeGuitarBridgeStatic<T>(string methodName, params object[] args)
    {
        return (T)InvokeGuitarBridgeStatic(methodName, args);
    }

    private static object InvokeStatic(Type ownerType, string methodName, params object[] args)
    {
        MethodInfo method = FindMethod(ownerType, methodName, StaticPrivate, args);
        Assert.IsNotNull(method, $"Missing static method {ownerType.Name}.{methodName}");
        return method.Invoke(null, args);
    }

    private static object InvokeRuntimeInstance(UnityToneLabRuntime runtime, string methodName, params object[] args)
    {
        return InvokeInstance(runtime, methodName, args);
    }

    private static object InvokeInstance(object target, string methodName, params object[] args)
    {
        MethodInfo method = FindMethod(target.GetType(), methodName, InstancePrivate, args);
        Assert.IsNotNull(method, $"Missing instance method {target.GetType().Name}.{methodName}");
        return method.Invoke(target, args);
    }

    private static MethodInfo FindMethod(Type ownerType, string methodName, BindingFlags flags, object[] args)
    {
        object[] safeArgs = args ?? Array.Empty<object>();
        return ownerType
            .GetMethods(flags)
            .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
            .Where(method => MethodParametersMatch(method.GetParameters(), safeArgs))
            .FirstOrDefault();
    }

    private static bool MethodParametersMatch(ParameterInfo[] parameters, object[] args)
    {
        if (parameters.Length != args.Length)
            return false;

        for (int i = 0; i < parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            if (parameterType.IsByRef)
                parameterType = parameterType.GetElementType();

            object arg = args[i];
            if (arg == null)
            {
                if (parameterType != null && parameterType.IsValueType)
                    return false;
                continue;
            }

            if (parameterType == null || !parameterType.IsAssignableFrom(arg.GetType()))
                return false;
        }

        return true;
    }

    private static List<int> InvokeIntListRuntimeStatic(string methodName, params object[] args)
    {
        return ToIntList(InvokeRuntimeStatic(methodName, args));
    }

    private static List<int> InvokeIntListInstance(object target, string methodName, params object[] args)
    {
        return ToIntList(InvokeInstance(target, methodName, args));
    }

    private static List<int> ToIntList(object value)
    {
        IEnumerable enumerable = value as IEnumerable;
        Assert.IsNotNull(enumerable);
        List<int> result = new List<int>();
        foreach (object item in enumerable)
            result.Add(Convert.ToInt32(item));
        return result;
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceAny);
        Assert.IsNotNull(field, $"Missing field {target.GetType().Name}.{fieldName}");
        return field.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceAny);
        Assert.IsNotNull(field, $"Missing field {target.GetType().Name}.{fieldName}");
        field.SetValue(target, value);
    }

    private static object GetProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceAny);
        Assert.IsNotNull(property, $"Missing property {target.GetType().Name}.{propertyName}");
        return property.GetValue(target);
    }

    private static void SetProperty(object target, string propertyName, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceAny);
        Assert.IsNotNull(property, $"Missing property {target.GetType().Name}.{propertyName}");
        property.SetValue(target, value);
    }

    private sealed class RuntimeFixture : IDisposable
    {
        public GameObject GameObject { get; private set; }
        public UnityToneLabRuntime Runtime { get; private set; }

        public static RuntimeFixture Create()
        {
            GameObject gameObject = new GameObject("AudioPathRegressionTests_Runtime");
            gameObject.SetActive(false);
            return new RuntimeFixture
            {
                GameObject = gameObject,
                Runtime = gameObject.AddComponent<UnityToneLabRuntime>()
            };
        }

        public void Dispose()
        {
            if (Runtime != null)
                SetField(Runtime, "settingsDirty", false);
            if (GameObject != null)
                UnityEngine.Object.DestroyImmediate(GameObject);
            GameObject = null;
            Runtime = null;
        }
    }
}
