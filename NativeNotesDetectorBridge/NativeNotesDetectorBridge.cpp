#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifdef _WIN32
#include <Windows.h>
#else
#include <dlfcn.h>
#include <strings.h>
#ifndef __cdecl
#define __cdecl
#endif
#endif

#ifdef _WIN32
#define ST_NATIVE_EXPORT __declspec(dllexport)
#else
#define ST_NATIVE_EXPORT __attribute__((visibility("default")))
#endif

#include <algorithm>
#include <array>
#include <atomic>
#include <chrono>
#include <cmath>
#include <complex>
#include <condition_variable>
#include <codecvt>
#include <cstdio>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <ctime>
#include <deque>
#include <filesystem>
#include <fstream>
#include <limits>
#include <locale>
#include <memory>
#include <mutex>
#include <queue>
#include <set>
#include <sstream>
#include <string>
#include <thread>
#include <utility>
#include <vector>

#include "third_party_onnxruntime_c_api.h"
#include "aubio.h"
#include "samplerate.h"

namespace
{
constexpr int kSampleRate = 22050;
constexpr int kHopSize = 512;
constexpr int kOnsetWindowSize = 1024;
constexpr int kPitchWindowSize = 2048;
constexpr int kCaptureSamples = 6615;
constexpr int kRingBufferSamples = kSampleRate * 8;
constexpr int kMaxAudioCallbackFrames = 65536;
constexpr int kModelFftHop = 256;
constexpr int kModelOverlapFrames = 30;
constexpr int kModelInputSamples = 43844;
constexpr int kModelOutputFrames = 172;
constexpr int kModelOutputPitches = 88;
constexpr int kModelTrimFrames = kModelOverlapFrames / 2;
constexpr int kModelAnnotationsFps = 86;
constexpr int kFastChordAnalysisWindowShortSamples = 4096;
constexpr int kFastChordAnalysisWindowLongSamples = 8192;
constexpr int kFastSingleAnalysisWindowShortSamples = 2048;
constexpr int kFastSingleAnalysisWindowLongSamples = 3072;
constexpr int kFastSingleAnalysisWindowFallbackSamples = 4096;
constexpr float kDebounceSeconds = 0.05f;
constexpr float kEventBroadcastSeconds = 0.14f;
constexpr float kContinuousRmsGate = 0.007f;
constexpr float kContinuousHoldSeconds = 0.10f;
constexpr int kContinuousMedianWindow = 5;
constexpr int kContinuousMinMidi = 36;
constexpr int kContinuousMaxMidi = 88;
constexpr float kUnitySyncAlpha = 0.20f;
constexpr double kUnitySyncSnapThresholdSeconds = 0.25;
constexpr float kHintRetentionSeconds = 2.0f;
constexpr int kHighStringMinMidi = 64;
constexpr float kHighStringRmsMultiplier = 0.50f;
constexpr int kHighStringBenefitMatchMaxDistance = 0;
constexpr float kOnsetExpectLookaheadSeconds = 0.120f;
constexpr int kMaxEventNotes = 6;
constexpr float kChordResultMergeSeconds = 0.050f;
constexpr double kVerifierScoreIntervalSeconds = 0.035;
constexpr double kVerifierOnsetEarlySeconds = 0.170;
constexpr double kVerifierOnsetLateSeconds = 0.260;
constexpr double kVerifierOnsetRetentionSeconds = 2.0;
constexpr double kVerifierSeekResetThresholdSeconds = 0.35;
constexpr int kVerifierMaxGroupsPerHop = 4;
constexpr int kBassRescuePrimaryWindowStartSamples = 640;
constexpr int kBassRescueSecondaryWindowStartSamples = 1280;
constexpr int kBassRescueAnalysisWindowSamples = 3072;
constexpr size_t kBassRescueMinExpectedOverlap = 2;
constexpr float kBassRescueNeighborRatio = 1.22f;
constexpr float kBassRescueSliceRmsRatio = 0.08f;
constexpr float kBassRescueAbsoluteAmplitudeFloor = 0.0065f;
constexpr float kBassRescueFundamentalWeight = 1.00f;
constexpr float kBassRescueSecondHarmonicWeight = 0.42f;
constexpr float kBassRescueThirdHarmonicWeight = 0.18f;
constexpr float kBassRescueOctaveSupportMultiplier = 1.10f;
constexpr float kBassRescueFundamentalRelaxedScale = 0.72f;
constexpr float kOnsetThreshold = 0.50f;
constexpr float kFrameThreshold = 0.30f;
constexpr int kMinimumNoteLengthFrames = 11;
constexpr int kMelodiaEnergyTolerance = 11;
constexpr float kFastChordStringBandHeadroom = 0.10f;
constexpr float kFastChordStringEnergyThreshold = 0.030f;
constexpr float kFastChordLegatoStringEnergyThreshold = 0.022f;
constexpr float kFastChordHarmonicStringEnergyThreshold = 0.020f;
constexpr float kFastChordBaseToleranceCents = 45.0f;
constexpr float kFastChordLowToleranceCents = 65.0f;
constexpr float kFastChordMotionToleranceCents = 95.0f;
constexpr float kFastChordPitchScoreThreshold = 0.0042f;
constexpr float kFastChordLowPitchScoreThreshold = 0.0034f;
constexpr float kFastChordSecondHarmonicWeight = 0.42f;
constexpr float kFastChordThirdHarmonicWeight = 0.18f;
constexpr float kFastChordNeighborRatioThreshold = 1.22f;
constexpr float kFastChordLowNeighborRatioThreshold = 1.12f;
constexpr float kPi = 3.14159265358979323846f;
constexpr float kYinThreshold = 0.18f;
constexpr int kDetectorInputChannelInput1 = 0;
constexpr int kDetectorInputChannelInput2 = 1;
constexpr int kDetectorInputChannelMonoMix = 2;

constexpr unsigned long kPaFloat32 = 0x00000001UL;
constexpr unsigned long kPaNoFlag = 0UL;
constexpr int kPaNoError = 0;
constexpr int kPaNoDevice = -1;

const std::array<const char*, 12> kNoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

struct DetectorSettings
{
    float chordLeniency = 0.90f;
    float continuousRmsGate = 0.007f;
    float continuousConfidenceGate = 0.73f;
    float continuousHoldSeconds = 0.10f;
    int highStringMinMidi = 64;
    float highStringRmsMultiplier = 0.50f;
    float highStringConfidenceMultiplier = 0.82f;
    int highStringBenefitMatchMaxDistance = 0;
    float onsetExpectLookaheadSeconds = 0.120f;
    float standardOnsetThreshold = 0.23f;
    float highStringOnsetThreshold = 0.14f;
    float expectExactBonus = 2.0f;
    float expectNearBonus1 = 0.55f;
    float expectNearBonus2 = 0.20f;
    int expectMaxDistanceAi = 1;
    int expectMaxDistanceContinuous = 1;
    float expectStrictConfidence = 0.88f;
    float chordScoreKeepRatio = 0.80f;
    float chordExpectedScoreKeepRatio = 0.70f;
};

enum class DetectorResamplerMode : int
{
    Linear = 0,
    Filtered = 1
};

DetectorResamplerMode NormalizeDetectorResamplerMode(int rawValue)
{
    return rawValue == static_cast<int>(DetectorResamplerMode::Linear)
        ? DetectorResamplerMode::Linear
        : DetectorResamplerMode::Filtered;
}

const char* GetDetectorResamplerModeLabel(DetectorResamplerMode mode)
{
    return mode == DetectorResamplerMode::Linear ? "Linear" : "Filtered";
}

const char* GetActiveDetectorResamplerModeLabel(DetectorResamplerMode mode, int captureSampleRate)
{
    if (captureSampleRate == kSampleRate)
        return "Direct";

    return GetDetectorResamplerModeLabel(mode);
}

int NormalizeDetectorInputChannelMode(int rawValue)
{
    switch (rawValue)
    {
    case kDetectorInputChannelInput2:
        return kDetectorInputChannelInput2;
    case kDetectorInputChannelMonoMix:
        return kDetectorInputChannelMonoMix;
    case kDetectorInputChannelInput1:
    default:
        return kDetectorInputChannelInput1;
    }
}

const char* GetDetectorInputChannelModeLabel(int mode)
{
    switch (NormalizeDetectorInputChannelMode(mode))
    {
    case kDetectorInputChannelInput2:
        return "Input 2";
    case kDetectorInputChannelMonoMix:
        return "Mono Mix";
    case kDetectorInputChannelInput1:
    default:
        return "Input 1";
    }
}

int RequiredDetectorInputChannels(int mode, int availableChannels)
{
    const int safeAvailable = std::max(1, availableChannels);
    const int normalized = NormalizeDetectorInputChannelMode(mode);
    if ((normalized == kDetectorInputChannelInput2 || normalized == kDetectorInputChannelMonoMix) && safeAvailable > 1)
        return 2;

    return 1;
}

float ReadDetectorInputSample(const float* input, int frame, int inputChannels, int sourceChannel)
{
    if (input == nullptr || frame < 0)
        return 0.0f;

    const int safeChannels = std::max(1, inputChannels);
    const int clampedChannel = std::clamp(sourceChannel, 0, safeChannels - 1);
    return input[(frame * safeChannels) + clampedChannel];
}

float SelectDetectorMonoSample(const float* input, int frame, int inputChannels, int mode)
{
    const int safeChannels = std::max(1, inputChannels);
    switch (NormalizeDetectorInputChannelMode(mode))
    {
    case kDetectorInputChannelInput2:
        return ReadDetectorInputSample(input, frame, safeChannels, safeChannels > 1 ? 1 : 0);
    case kDetectorInputChannelMonoMix:
        if (safeChannels <= 1)
            return ReadDetectorInputSample(input, frame, safeChannels, 0);
        return (ReadDetectorInputSample(input, frame, safeChannels, 0) +
                ReadDetectorInputSample(input, frame, safeChannels, 1)) * 0.5f;
    case kDetectorInputChannelInput1:
    default:
        return ReadDetectorInputSample(input, frame, safeChannels, 0);
    }
}

enum ExpectedHintNoteFlags : uint32_t
{
    ExpectedHintNoteFlagNone = 0,
    ExpectedHintNoteFlagLegato = 1u << 0,
    ExpectedHintNoteFlagBend = 1u << 1,
    ExpectedHintNoteFlagSlide = 1u << 2,
    ExpectedHintNoteFlagHarmonic = 1u << 3
};

DetectorSettings MakeTightDetectorSettings()
{
    return DetectorSettings{};
}

using Clock = std::chrono::steady_clock;

struct PaStream;
typedef double PaTime;
typedef unsigned long PaSampleFormat;
typedef unsigned long PaStreamFlags;
typedef unsigned long PaStreamCallbackFlags;

struct PaStreamCallbackTimeInfo
{
    PaTime inputBufferAdcTime;
    PaTime currentTime;
    PaTime outputBufferDacTime;
};

typedef int(__cdecl* PaStreamCallback)(
    const void* input,
    void* output,
    unsigned long frameCount,
    const PaStreamCallbackTimeInfo* timeInfo,
    PaStreamCallbackFlags statusFlags,
    void* userData);

struct PaStreamParameters
{
    int device;
    int channelCount;
    PaSampleFormat sampleFormat;
    PaTime suggestedLatency;
    void* hostApiSpecificStreamInfo;
};

struct PaDeviceInfoNative
{
    int structVersion;
    const char* name;
    int hostApi;
    int maxInputChannels;
    int maxOutputChannels;
    double defaultLowInputLatency;
    double defaultLowOutputLatency;
    double defaultHighInputLatency;
    double defaultHighOutputLatency;
    double defaultSampleRate;
};

struct PaHostApiInfoNative
{
    int structVersion;
    int type;
    const char* name;
    int deviceCount;
    int defaultInputDevice;
    int defaultOutputDevice;
};

struct NativeDeviceDescriptor
{
    int index = -1;
    std::string displayName;
    std::string name;
    std::string hostApiName;
    int maxInputChannels = 0;
    double defaultSampleRate = 0.0;
    double defaultLowInputLatency = 0.0;
};

struct ExpectedHintNoteSpec
{
    int midi = -1;
    int stringIndex = -1;
    int fret = -1;
    int openMidi = -1;
    uint32_t flags = ExpectedHintNoteFlagNone;
    int noteId = -1;
    int chordId = -1;
    double noteTime = -1.0;       // Unity song time
    double notePythonTime = -1.0; // Native audio time, filled after sync mapping.
};

struct ExpectedHintContext
{
    std::set<int> midiNotes;
    std::vector<ExpectedHintNoteSpec> expectedNotes;
    bool hasWindow = false;
    double windowStartTime = 0.0;      // Unity song time
    double windowEndTime = 0.0;        // Unity song time
    double windowStartPythonTime = 0.0;
    double windowEndPythonTime = 0.0;
};

int NormalizeInputSampleRate(double sampleRate);
std::vector<int> BuildInputSampleRateCandidates(const NativeDeviceDescriptor* selectedDevice);

struct HintWindow
{
    double startTime = 0.0;
    double endTime = 0.0;
    std::set<int> midiNotes;
    std::vector<ExpectedHintNoteSpec> expectedNotes;
    Clock::time_point createdAt = Clock::now();
};

struct CaptureTask
{
    int eventId = 0;
    uint64_t startFrame = 0;
    uint64_t readyFrame = 0;
    double onsetTime = 0.0;
};

struct FastChordTask
{
    int eventId = 0;
    uint64_t onsetFrame = 0;
    uint64_t readyFrame = 0;
    double onsetTime = 0.0;
    std::set<int> expectedMidiNotes;
    std::vector<ExpectedHintNoteSpec> expectedNotes;
};

struct FastSingleTask
{
    int eventId = 0;
    uint64_t onsetFrame = 0;
    uint64_t readyFrame = 0;
    double onsetTime = 0.0;
    ExpectedHintNoteSpec expectedNote;
    int analysisWindowSamples = 0;
    int attemptIndex = 0;
    bool proactive = false;
    double windowStartPythonTime = 0.0;
    double windowEndPythonTime = 0.0;
};

struct DeepResult
{
    int eventId = 0;
    double onsetTime = 0.0;
    std::set<int> eventNotes;
    std::set<int> expectedMidiNotes;
    std::string sourceTag;
};

struct NativeVerifierVerdict
{
    int noteId = -1;
    int chordId = -1;
    int midi = -1;
    bool hit = false;
    double noteTime = -1.0;
    double detectedSongTime = -1.0;
    float confidence = 0.0f;
    float centsError = 0.0f;
    std::string source;
};

struct VerifierExpectedGroup
{
    std::vector<ExpectedHintNoteSpec> expectedNotes;
    int chordId = -1;
    double noteTime = -1.0;
    double notePythonTime = -1.0;
    bool requiresOnset = false;
};

struct ConstraintChordNoteDebugResult
{
    ExpectedHintNoteSpec spec;
    float supportRatio = 0.0f;
    float supportThreshold = 0.0f;
    float fundamentalRatio = 0.0f;
    float neighborFundamentalMax = 0.0f;
    float noteScore = 0.0f;
    float noteScoreThreshold = 0.0f;
    float dominantPeakHz = 0.0f;
    float centsError = 0.0f;
    bool hit = false;
};

struct ConstraintChordEvaluationResult
{
    std::vector<ExpectedHintNoteSpec> expectedNotes;
    std::vector<ConstraintChordNoteDebugResult> noteResults;
    int hitCount = 0;
    int totalExpected = 0;
    int requiredHits = 0;
    float chordLeniency = 0.0f;
    bool accepted = false;
};

struct ConstraintSingleEvaluationResult
{
    ExpectedHintNoteSpec expectedNote;
    ConstraintChordNoteDebugResult noteResult;
    bool accepted = false;
};

struct OfflineSingleEvaluationResult
{
    bool accepted = false;
    bool highStringContext = false;
    bool onsetDetected = false;
    int detectedMidi = -1;
    int acceptedHopIndex = -1;
    int onsetHopIndex = -1;
    float lastMidiEstimate = -1.0f;
    float lastConfidence = 0.0f;
    float bestConfidence = 0.0f;
    float bestRms = 0.0f;
};

struct FastSingleWindowEvaluationResult
{
    ConstraintSingleEvaluationResult spectral;
    OfflineSingleEvaluationResult yin;
    bool accepted = false;
};

OfflineSingleEvaluationResult EvaluateOfflineSingleExpectedNote(
    const float* samples,
    int sampleCount,
    int expectedMidi,
    const DetectorSettings& settings);

struct NoteEventCandidate
{
    int midi = 0;
    float amplitude = 0.0f;
};

enum class ExpectedContextKind
{
    Normal,
    HighStringFocused,
    MixedChord
};

std::wstring Utf8ToWide(const char* text)
{
    if (text == nullptr || *text == '\0')
        return std::wstring();

#ifdef _WIN32
    const int required = MultiByteToWideChar(CP_UTF8, 0, text, -1, nullptr, 0);
    if (required <= 0)
        return std::wstring(); 

    std::wstring result(static_cast<size_t>(required) - 1, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, text, -1, result.data(), required);
    return result;
#else
    try
    {
        std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
        return converter.from_bytes(text);
    }
    catch (...)
    {
        std::string narrow(text);
        return std::wstring(narrow.begin(), narrow.end());
    }
#endif
}

std::string WideToUtf8(const std::wstring& text)
{
    if (text.empty())
        return std::string();

#ifdef _WIN32
    const int required = WideCharToMultiByte(CP_UTF8, 0, text.c_str(), -1, nullptr, 0, nullptr, nullptr);
    if (required <= 0)
        return std::string();

    std::string result(static_cast<size_t>(required) - 1, '\0');
    WideCharToMultiByte(CP_UTF8, 0, text.c_str(), -1, result.data(), required, nullptr, nullptr);
    return result;
#else
    try
    {
        std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
        return converter.to_bytes(text);
    }
    catch (...)
    {
        return std::string(text.begin(), text.end());
    }
#endif
}

bool CopyUtf8String(const std::string& source, char* destination, int capacity)
{
    if (destination == nullptr || capacity <= 0)
        return false;

    const size_t copyLength = std::min(source.size(), static_cast<size_t>(capacity - 1));
    if (copyLength > 0)
        memcpy(destination, source.data(), copyLength);
    destination[copyLength] = '\0';
    return true;
}

std::wstring GetCurrentModuleDirectory()
{
#ifdef _WIN32
    HMODULE module = nullptr;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        reinterpret_cast<LPCWSTR>(&GetCurrentModuleDirectory), &module))
    {
        return std::wstring();
    }

    std::wstring path(1024, L'\0');
    const DWORD length = GetModuleFileNameW(module, path.data(), static_cast<DWORD>(path.size()));
    if (length == 0)
        return std::wstring();

    path.resize(length);
    return std::filesystem::path(path).parent_path().wstring();
#else
    Dl_info info{};
    if (dladdr(reinterpret_cast<void*>(&GetCurrentModuleDirectory), &info) == 0 || info.dli_fname == nullptr)
        return std::wstring();

    std::filesystem::path path(info.dli_fname);
    return Utf8ToWide(path.parent_path().string().c_str());
#endif
}

#ifdef _WIN32
using DynamicLibraryHandle = HMODULE;
#else
using DynamicLibraryHandle = void*;
#endif

DynamicLibraryHandle LoadDynamicLibrary(const std::filesystem::path& path, std::wstring& error)
{
#ifdef _WIN32
    DynamicLibraryHandle handle = LoadLibraryW(path.wstring().c_str());
    if (handle == nullptr)
        error = L"Failed to load " + path.filename().wstring() + L" from the project plugins folder.";
    return handle;
#else
    dlerror();
    DynamicLibraryHandle handle = dlopen(path.string().c_str(), RTLD_NOW | RTLD_LOCAL);
    if (handle == nullptr)
    {
        const char* loadError = dlerror();
        error = L"Failed to load " + Utf8ToWide(path.filename().string().c_str()) + L" from the project plugins folder";
        if (loadError != nullptr && *loadError != '\0')
            error += L": " + Utf8ToWide(loadError);
        error += L".";
    }
    return handle;
#endif
}

void* GetDynamicLibrarySymbol(DynamicLibraryHandle handle, const char* name)
{
#ifdef _WIN32
    return reinterpret_cast<void*>(GetProcAddress(handle, name));
#else
    return dlsym(handle, name);
#endif
}

void CloseDynamicLibrary(DynamicLibraryHandle handle)
{
    if (handle == nullptr)
        return;

#ifdef _WIN32
    FreeLibrary(handle);
#else
    dlclose(handle);
#endif
}

std::filesystem::path PluginLibraryPath(const std::wstring& pluginDirectory, const wchar_t* windowsName, const char* macName, const char* linuxName)
{
#ifdef _WIN32
    return std::filesystem::path(pluginDirectory) / windowsName;
#elif defined(__APPLE__)
    return std::filesystem::path(WideToUtf8(pluginDirectory)) / macName;
#else
    return std::filesystem::path(WideToUtf8(pluginDirectory)) / linuxName;
#endif
}

std::string JsonEscape(const std::string& value)
{
    std::string result;
    result.reserve(value.size() + 16);
    for (char ch : value)
    {
        switch (ch)
        {
        case '\\': result += "\\\\"; break;
        case '"': result += "\\\""; break;
        case '\n': result += "\\n"; break;
        case '\r': result += "\\r"; break;
        case '\t': result += "\\t"; break;
        default: result += ch; break;
        }
    }
    return result;
}

template <typename TValue>
TValue ClampValue(TValue value, TValue minValue, TValue maxValue)
{
    return std::max(minValue, std::min(maxValue, value));
}

bool TryExtractJsonNumber(const std::string& json, const char* key, double& outValue)
{
    if (key == nullptr || *key == '\0')
        return false;

    const std::string pattern = std::string("\"") + key + "\":";
    const size_t keyPos = json.find(pattern);
    if (keyPos == std::string::npos)
        return false;

    const size_t valueStart = keyPos + pattern.size();
    char* endPtr = nullptr;
    const double parsedValue = std::strtod(json.c_str() + valueStart, &endPtr);
    if (endPtr == json.c_str() + valueStart)
        return false;

    outValue = parsedValue;
    return true;
}

bool TryExtractJsonInt(const std::string& json, const char* key, int& outValue)
{
    double parsedValue = 0.0;
    if (!TryExtractJsonNumber(json, key, parsedValue))
        return false;

    outValue = static_cast<int>(std::lround(parsedValue));
    return true;
}

float ComputeRms(const float* data, int count)
{
    if (data == nullptr || count <= 0)
        return 0.0f;

    double accum = 0.0;
    for (int i = 0; i < count; ++i)
        accum += static_cast<double>(data[i]) * static_cast<double>(data[i]);

    return static_cast<float>(std::sqrt(accum / static_cast<double>(count)));
}

int HostPriority(const std::string& hostApiName)
{
    if (hostApiName.find("ASIO") != std::string::npos || hostApiName.find("asio") != std::string::npos)
        return 0;
    if (hostApiName.find("Core Audio") != std::string::npos || hostApiName.find("CoreAudio") != std::string::npos ||
        hostApiName.find("core audio") != std::string::npos || hostApiName.find("coreaudio") != std::string::npos)
        return 0;
    if (hostApiName.find("WASAPI") != std::string::npos || hostApiName.find("Wasapi") != std::string::npos || hostApiName.find("wasapi") != std::string::npos)
        return 1;
    return 999;
}

int NoteNameToMidi(const std::string& noteName)
{
    if (noteName.empty())
        return -1;

    std::string upper;
    upper.reserve(noteName.size());
    for (char ch : noteName)
    {
        if (ch >= 'a' && ch <= 'z')
            upper.push_back(static_cast<char>(ch - 32));
        else
            upper.push_back(ch);
    }

    std::string pitch;
    std::string octaveText;
    if (upper.size() >= 3 && upper[1] == '#')
    {
        pitch = upper.substr(0, 2);
        octaveText = upper.substr(2);
    }
    else
    {
        pitch = upper.substr(0, 1);
        octaveText = upper.substr(1);
    }

    int semitone = -1;
    for (int i = 0; i < static_cast<int>(kNoteNames.size()); ++i)
    {
        if (pitch == kNoteNames[static_cast<size_t>(i)])
        {
            semitone = i;
            break;
        }
    }

    if (semitone < 0)
        return -1;

    try
    {
        const int octave = std::stoi(octaveText);
        return semitone + (octave + 1) * 12;
    }
    catch (...)
    {
        return -1;
    }
}

std::string SanitizeHintNoteToken(std::string token)
{
    auto isTrimByte = [](unsigned char ch)
    {
        return ch <= 32 || ch == 127;
    };

    while (!token.empty())
    {
        const unsigned char ch = static_cast<unsigned char>(token.front());
        if (!isTrimByte(ch) && ch != 0xEF && ch != 0xBB && ch != 0xBF)
            break;
        token.erase(token.begin());
    }

    while (!token.empty() && isTrimByte(static_cast<unsigned char>(token.back())))
        token.pop_back();

    const size_t firstUseful = token.find_first_of("ABCDEFGabcdefg0123456789-");
    if (firstUseful != std::string::npos && firstUseful > 0)
        token.erase(0, firstUseful);

    return token;
}

int ParseHintTokenToMidi(const std::string& rawToken)
{
    std::string token = SanitizeHintNoteToken(rawToken);
    if (token.empty())
        return -1;

    int midi = NoteNameToMidi(token);
    if (midi >= 0)
        return midi;

    try
    {
        size_t parsedLength = 0;
        const int numericMidi = std::stoi(token, &parsedLength);
        if (parsedLength == token.size() && numericMidi >= 0 && numericMidi <= 127)
            return numericMidi;
    }
    catch (...)
    {
    }

    return -1;
}

bool ExpectedHintNoteSpecsEqual(const ExpectedHintNoteSpec& left, const ExpectedHintNoteSpec& right)
{
    return left.midi == right.midi &&
        left.stringIndex == right.stringIndex &&
        left.fret == right.fret &&
        left.openMidi == right.openMidi &&
        left.flags == right.flags &&
        left.noteId == right.noteId &&
        left.chordId == right.chordId &&
        std::abs(left.noteTime - right.noteTime) <= 0.0005;
}

void AppendUniqueExpectedNotes(std::vector<ExpectedHintNoteSpec>& destination, const std::vector<ExpectedHintNoteSpec>& source)
{
    if (source.empty())
    {
        std::vector<ExpectedHintNoteSpec> deduped;
        deduped.reserve(destination.size());
        for (const ExpectedHintNoteSpec& spec : destination)
        {
            bool exists = false;
            for (const ExpectedHintNoteSpec& existing : deduped)
            {
                if (ExpectedHintNoteSpecsEqual(existing, spec))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
                deduped.push_back(spec);
        }

        destination.swap(deduped);
        return;
    }

    for (const ExpectedHintNoteSpec& spec : source)
    {
        bool exists = false;
        for (const ExpectedHintNoteSpec& existing : destination)
        {
            if (ExpectedHintNoteSpecsEqual(existing, spec))
            {
                exists = true;
                break;
            }
        }

        if (!exists)
            destination.push_back(spec);
    }
}

std::vector<ExpectedHintNoteSpec> ParseExpectedHintNoteSpecsCsv(const std::string& csv)
{
    std::vector<ExpectedHintNoteSpec> result;
    std::string current;
    for (size_t i = 0; i <= csv.size(); ++i)
    {
        if (i == csv.size() || csv[i] == ',')
        {
            if (!current.empty())
            {
                std::vector<std::string> fields;
                std::string field;
                for (size_t fieldIndex = 0; fieldIndex <= current.size(); ++fieldIndex)
                {
                    if (fieldIndex == current.size() || current[fieldIndex] == '~')
                    {
                        fields.push_back(field);
                        field.clear();
                    }
                    else if (current[fieldIndex] != ' ' && current[fieldIndex] != '\t')
                    {
                        field.push_back(current[fieldIndex]);
                    }
                }

                if (fields.size() >= 5)
                {
                    try
                    {
                        ExpectedHintNoteSpec spec;
                        spec.midi = ParseHintTokenToMidi(fields[0]);
                        spec.stringIndex = std::stoi(fields[1]);
                        spec.fret = std::stoi(fields[2]);
                        spec.openMidi = ParseHintTokenToMidi(fields[3]);
                        spec.flags = static_cast<uint32_t>(std::stoul(fields[4]));
                        if (fields.size() >= 6)
                            spec.noteId = std::stoi(fields[5]);
                        if (fields.size() >= 7)
                            spec.chordId = std::stoi(fields[6]);
                        if (fields.size() >= 8)
                            spec.noteTime = std::stod(fields[7]);
                        if (spec.midi >= 0 && spec.stringIndex >= 0 && spec.openMidi >= 0)
                            result.push_back(spec);
                    }
                    catch (...)
                    {
                    }
                }
            }

            current.clear();
        }
        else if (csv[i] != ' ' && csv[i] != '\t')
        {
            current.push_back(csv[i]);
        }
    }

    AppendUniqueExpectedNotes(result, {});
    return result;
}

std::string MidiToNoteName(int midi)
{
    const int note = ((midi % 12) + 12) % 12;
    const int octave = (midi / 12) - 1;
    std::ostringstream builder;
    builder << kNoteNames[static_cast<size_t>(note)] << octave;
    return builder.str();
}

int SemitoneDistance(int midiA, int midiB)
{
    return std::abs(midiA - midiB);
}

float MidiToFrequencyHz(int midi)
{
    return 440.0f * std::pow(2.0f, static_cast<float>(midi - 69) / 12.0f);
}

float ComputeWindowRms(const std::vector<float>& audio, int startSample, int sampleCount)
{
    if (sampleCount <= 0 || startSample < 0 || startSample >= static_cast<int>(audio.size()))
        return 0.0f;

    const int available = std::min(sampleCount, static_cast<int>(audio.size()) - startSample);
    if (available <= 0)
        return 0.0f;

    double energy = 0.0;
    for (int i = 0; i < available; ++i)
    {
        const double sample = audio[static_cast<size_t>(startSample + i)];
        energy += sample * sample;
    }

    return static_cast<float>(std::sqrt(energy / static_cast<double>(available)));
}

float ComputeGoertzelAmplitude(const std::vector<float>& audio, int startSample, int sampleCount, float frequencyHz)
{
    if (!std::isfinite(frequencyHz) || frequencyHz <= 0.0f || frequencyHz >= (static_cast<float>(kSampleRate) * 0.5f) - 5.0f)
        return 0.0f;

    if (sampleCount <= 0 || startSample < 0 || startSample >= static_cast<int>(audio.size()))
        return 0.0f;

    const int available = std::min(sampleCount, static_cast<int>(audio.size()) - startSample);
    if (available < 256)
        return 0.0f;

    const double omega = (2.0 * kPi * static_cast<double>(frequencyHz)) / static_cast<double>(kSampleRate);
    const double coeff = 2.0 * std::cos(omega);
    double q0 = 0.0;
    double q1 = 0.0;
    double q2 = 0.0;
    double windowSum = 0.0;

    for (int i = 0; i < available; ++i)
    {
        const double phase = available > 1 ? (2.0 * kPi * static_cast<double>(i) / static_cast<double>(available - 1)) : 0.0;
        const double window = 0.5 - (0.5 * std::cos(phase));
        const double sample = static_cast<double>(audio[static_cast<size_t>(startSample + i)]) * window;
        q0 = (coeff * q1) - q2 + sample;
        q2 = q1;
        q1 = q0;
        windowSum += window;
    }

    const double power = std::max(0.0, (q1 * q1) + (q2 * q2) - (coeff * q1 * q2));
    const double magnitude = std::sqrt(power);
    if (windowSum <= std::numeric_limits<double>::epsilon())
        return 0.0f;

    return static_cast<float>((2.0 * magnitude) / windowSum);
}

float ComputeHarmonicSalience(
    const std::vector<float>& audio,
    int startSample,
    int sampleCount,
    int midi,
    float* outFundamentalAmplitude = nullptr)
{
    const float f0 = MidiToFrequencyHz(midi);
    if (!std::isfinite(f0) || f0 <= 0.0f)
    {
        if (outFundamentalAmplitude != nullptr)
            *outFundamentalAmplitude = 0.0f;
        return 0.0f;
    }

    const float fundamentalAmplitude = ComputeGoertzelAmplitude(audio, startSample, sampleCount, f0);
    if (outFundamentalAmplitude != nullptr)
        *outFundamentalAmplitude = fundamentalAmplitude;

    float salience = kBassRescueFundamentalWeight * fundamentalAmplitude;

    const float secondHarmonicFrequency = f0 * 2.0f;
    if (secondHarmonicFrequency < (static_cast<float>(kSampleRate) * 0.5f) - 5.0f)
        salience += kBassRescueSecondHarmonicWeight * ComputeGoertzelAmplitude(audio, startSample, sampleCount, secondHarmonicFrequency);

    const float thirdHarmonicFrequency = f0 * 3.0f;
    if (thirdHarmonicFrequency < (static_cast<float>(kSampleRate) * 0.5f) - 5.0f)
        salience += kBassRescueThirdHarmonicWeight * ComputeGoertzelAmplitude(audio, startSample, sampleCount, thirdHarmonicFrequency);

    return salience;
}

size_t CountSetOverlap(const std::set<int>& left, const std::set<int>& right)
{
    if (left.empty() || right.empty())
        return 0;

    const std::set<int>& smaller = left.size() <= right.size() ? left : right;
    const std::set<int>& larger = left.size() <= right.size() ? right : left;
    size_t overlap = 0;
    for (int value : smaller)
    {
        if (larger.find(value) != larger.end())
            ++overlap;
    }

    return overlap;
}

ExpectedContextKind GetExpectedContextKind(const std::set<int>& expectedMidi, const DetectorSettings& settings)
{
    if (expectedMidi.empty())
        return ExpectedContextKind::Normal;

    size_t highCount = 0;
    for (int midi : expectedMidi)
    {
        if (midi >= settings.highStringMinMidi)
            ++highCount;
    }

    if (highCount == 0)
        return ExpectedContextKind::Normal;

    if (highCount == expectedMidi.size())
        return ExpectedContextKind::HighStringFocused;

    return ExpectedContextKind::MixedChord;
}

bool IsHighStringContext(const std::set<int>& expectedMidi, const DetectorSettings& settings)
{
    return GetExpectedContextKind(expectedMidi, settings) == ExpectedContextKind::HighStringFocused;
}

DetectorSettings ClampDetectorSettings(const DetectorSettings& source)
{
    DetectorSettings clamped = source;
    clamped.chordLeniency = ClampValue(clamped.chordLeniency, 0.34f, 1.0f);
    clamped.continuousRmsGate = ClampValue(clamped.continuousRmsGate, 0.001f, 0.05f);
    clamped.continuousConfidenceGate = ClampValue(clamped.continuousConfidenceGate, 0.0f, 0.99f);
    clamped.continuousHoldSeconds = ClampValue(clamped.continuousHoldSeconds, 0.01f, 0.50f);
    clamped.highStringMinMidi = ClampValue(clamped.highStringMinMidi, 36, 96);
    clamped.highStringRmsMultiplier = ClampValue(clamped.highStringRmsMultiplier, 0.10f, 1.50f);
    clamped.highStringConfidenceMultiplier = ClampValue(clamped.highStringConfidenceMultiplier, 0.10f, 1.00f);
    clamped.highStringBenefitMatchMaxDistance = ClampValue(clamped.highStringBenefitMatchMaxDistance, 0, 2);
    clamped.onsetExpectLookaheadSeconds = ClampValue(clamped.onsetExpectLookaheadSeconds, 0.0f, 0.40f);
    clamped.standardOnsetThreshold = ClampValue(clamped.standardOnsetThreshold, 0.01f, 1.0f);
    clamped.highStringOnsetThreshold = ClampValue(clamped.highStringOnsetThreshold, 0.01f, 1.0f);
    clamped.expectExactBonus = ClampValue(clamped.expectExactBonus, 0.0f, 5.0f);
    clamped.expectNearBonus1 = ClampValue(clamped.expectNearBonus1, 0.0f, 3.0f);
    clamped.expectNearBonus2 = ClampValue(clamped.expectNearBonus2, 0.0f, 3.0f);
    clamped.expectMaxDistanceAi = ClampValue(clamped.expectMaxDistanceAi, 0, 4);
    clamped.expectMaxDistanceContinuous = ClampValue(clamped.expectMaxDistanceContinuous, 0, 4);
    clamped.expectStrictConfidence = ClampValue(clamped.expectStrictConfidence, 0.0f, 0.99f);
    clamped.chordScoreKeepRatio = ClampValue(clamped.chordScoreKeepRatio, 0.0f, 0.99f);
    clamped.chordExpectedScoreKeepRatio = ClampValue(clamped.chordExpectedScoreKeepRatio, 0.0f, 0.99f);
    return clamped;
}

DetectorSettings ParseDetectorSettingsJson(const std::string& json, const DetectorSettings& fallback)
{
    DetectorSettings settings = fallback;
    double numberValue = 0.0;
    int intValue = 0;

    if (TryExtractJsonNumber(json, "chordLeniency", numberValue)) settings.chordLeniency = static_cast<float>(numberValue);
    if (TryExtractJsonNumber(json, "continuousRmsGate", numberValue)) settings.continuousRmsGate = static_cast<float>(numberValue);
    if (TryExtractJsonNumber(json, "continuousConfidenceGate", numberValue)) settings.continuousConfidenceGate = static_cast<float>(numberValue);
    if (TryExtractJsonNumber(json, "continuousHoldSeconds", numberValue)) settings.continuousHoldSeconds = static_cast<float>(numberValue);
    if (TryExtractJsonInt(json, "highStringMinMidi", intValue)) settings.highStringMinMidi = intValue;
    if (TryExtractJsonNumber(json, "highStringRmsMultiplier", numberValue)) settings.highStringRmsMultiplier = static_cast<float>(numberValue);
    if (TryExtractJsonNumber(json, "highStringConfidenceMultiplier", numberValue)) settings.highStringConfidenceMultiplier = static_cast<float>(numberValue);
    if (TryExtractJsonInt(json, "highStringBenefitMatchMaxDistance", intValue)) settings.highStringBenefitMatchMaxDistance = intValue;
    if (TryExtractJsonNumber(json, "onsetExpectLookaheadSeconds", numberValue)) settings.onsetExpectLookaheadSeconds = static_cast<float>(numberValue);
    if (TryExtractJsonNumber(json, "standardOnsetThreshold", numberValue)) settings.standardOnsetThreshold = static_cast<float>(numberValue);
    if (TryExtractJsonNumber(json, "highStringOnsetThreshold", numberValue)) settings.highStringOnsetThreshold = static_cast<float>(numberValue);
    if (TryExtractJsonNumber(json, "expectExactBonus", numberValue)) settings.expectExactBonus = static_cast<float>(numberValue);
    if (TryExtractJsonNumber(json, "expectNearBonus1", numberValue)) settings.expectNearBonus1 = static_cast<float>(numberValue);
    if (TryExtractJsonNumber(json, "expectNearBonus2", numberValue)) settings.expectNearBonus2 = static_cast<float>(numberValue);
    if (TryExtractJsonInt(json, "expectMaxDistanceAi", intValue)) settings.expectMaxDistanceAi = intValue;
    if (TryExtractJsonInt(json, "expectMaxDistanceContinuous", intValue)) settings.expectMaxDistanceContinuous = intValue;
    if (TryExtractJsonNumber(json, "expectStrictConfidence", numberValue)) settings.expectStrictConfidence = static_cast<float>(numberValue);
    if (TryExtractJsonNumber(json, "chordScoreKeepRatio", numberValue)) settings.chordScoreKeepRatio = static_cast<float>(numberValue);
    if (TryExtractJsonNumber(json, "chordExpectedScoreKeepRatio", numberValue)) settings.chordExpectedScoreKeepRatio = static_cast<float>(numberValue);

    return ClampDetectorSettings(settings);
}

template <typename TValue>
std::string JoinMidiNotes(const TValue& notes)
{
    std::vector<std::string> noteNames;
    noteNames.reserve(std::distance(notes.begin(), notes.end()));
    for (int midi : notes)
        noteNames.push_back(MidiToNoteName(midi));

    std::sort(noteNames.begin(), noteNames.end());

    std::ostringstream builder;
    bool first = true;
    for (const std::string& noteName : noteNames)
    {
        if (!first)
            builder << ',';
        first = false;
        builder << noteName;
    }
    return first ? std::string("--") : builder.str();
}

int CompareIgnoreCase(const std::string& left, const std::string& right)
{
#ifdef _WIN32
    return _stricmp(left.c_str(), right.c_str());
#else
    return strcasecmp(left.c_str(), right.c_str());
#endif
}

std::string MergeEventSourceTags(const std::string& left, const std::string& right)
{
    if (left.empty())
        return right;
    if (right.empty())
        return left;
    if (CompareIgnoreCase(left, right) == 0)
        return left;
    return left + "+" + right;
}

template <typename T>
void EraseAt(std::deque<T>& items, size_t index)
{
    items.erase(items.begin() + static_cast<typename std::deque<T>::difference_type>(index));
}

void FFT(std::vector<std::complex<float>>& data)
{
    const size_t n = data.size();
    size_t j = 0;
    for (size_t i = 1; i < n; ++i)
    {
        size_t bit = n >> 1;
        while (j & bit)
        {
            j ^= bit;
            bit >>= 1;
        }
        j ^= bit;
        if (i < j)
            std::swap(data[i], data[j]);
    }

    for (size_t len = 2; len <= n; len <<= 1)
    {
        const float angle = -2.0f * kPi / static_cast<float>(len);
        const std::complex<float> wlen(std::cos(angle), std::sin(angle));
        for (size_t i = 0; i < n; i += len)
        {
            std::complex<float> w(1.0f, 0.0f);
            const size_t half = len >> 1;
            for (size_t jHalf = 0; jHalf < half; ++jHalf)
            {
                const std::complex<float> u = data[i + jHalf];
                const std::complex<float> v = data[i + jHalf + half] * w;
                data[i + jHalf] = u + v;
                data[i + jHalf + half] = u - v;
                w *= wlen;
            }
        }
    }
}

std::mutex g_debugLogMutex;
std::wstring g_debugLogPath;

std::string BuildDebugLogTimestamp()
{
    using SystemClock = std::chrono::system_clock;
    const auto now = SystemClock::now();
    const std::time_t tt = SystemClock::to_time_t(now);
    std::tm localTime{};
#ifdef _WIN32
    localtime_s(&localTime, &tt);
#else
    localtime_r(&tt, &localTime);
#endif
    const auto millis = std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()) % 1000;

    char buffer[64];
    std::snprintf(
        buffer,
        sizeof(buffer),
        "%02d:%02d:%02d.%03d",
        localTime.tm_hour,
        localTime.tm_min,
        localTime.tm_sec,
        static_cast<int>(millis.count()));
    return std::string(buffer);
}

void AppendDebugLogLine(const std::string& line)
{
    std::lock_guard<std::mutex> lock(g_debugLogMutex);
    if (g_debugLogPath.empty())
        return;

    try
    {
        std::ofstream stream(std::filesystem::path(g_debugLogPath), std::ios::out | std::ios::app | std::ios::binary);
        if (!stream.is_open())
            return;

        stream << "[" << BuildDebugLogTimestamp() << "] [native] " << line << "\r\n";
    }
    catch (...)
    {
    }
}

void SetDebugLogPathInternal(const std::wstring& path)
{
    std::lock_guard<std::mutex> lock(g_debugLogMutex);
    g_debugLogPath = path;
}

float CentsToFrequencyFactor(float cents)
{
    return std::pow(2.0f, cents / 1200.0f);
}

float FrequencyDeltaCents(float detectedHz, float expectedHz)
{
    if (!std::isfinite(detectedHz) || detectedHz <= 0.0f || !std::isfinite(expectedHz) || expectedHz <= 0.0f)
        return std::numeric_limits<float>::infinity();

    const float rawCents = 1200.0f * std::log2(detectedHz / expectedHz);
    const float foldedCents = rawCents - (std::round(rawCents / 1200.0f) * 1200.0f);
    return std::abs(foldedCents);
}

void BuildMagnitudeSpectrum(
    const std::vector<float>& audio,
    int sampleCount,
    std::vector<float>& magnitudes,
    float& binHz)
{
    if (sampleCount <= 0 || audio.empty())
    {
        magnitudes.clear();
        binHz = 0.0f;
        return;
    }

    int fftSize = 1;
    while (fftSize < sampleCount)
        fftSize <<= 1;

    std::vector<std::complex<float>> fftData(static_cast<size_t>(fftSize), std::complex<float>(0.0f, 0.0f));
    const int available = std::min(sampleCount, static_cast<int>(audio.size()));
    for (int i = 0; i < available; ++i)
    {
        const float phase = available > 1 ? (2.0f * kPi * static_cast<float>(i) / static_cast<float>(available - 1)) : 0.0f;
        const float window = 0.5f - (0.5f * std::cos(phase));
        fftData[static_cast<size_t>(i)] = std::complex<float>(audio[static_cast<size_t>(i)] * window, 0.0f);
    }

    FFT(fftData);

    const size_t halfBins = static_cast<size_t>((fftSize >> 1) + 1);
    magnitudes.resize(halfBins);
    for (size_t i = 0; i < halfBins; ++i)
        magnitudes[i] = std::abs(fftData[i]);

    binHz = static_cast<float>(kSampleRate) / static_cast<float>(fftSize);
}

float ComputeMagnitudeTotalEnergy(const std::vector<float>& magnitudes)
{
    double totalEnergy = 0.0;
    for (float magnitude : magnitudes)
        totalEnergy += static_cast<double>(magnitude) * static_cast<double>(magnitude);

    return static_cast<float>(totalEnergy);
}

float ComputeBandEnergyRatio(
    const std::vector<float>& magnitudes,
    float binHz,
    float lowHz,
    float highHz,
    float totalEnergy)
{
    if (magnitudes.empty() || !std::isfinite(binHz) || binHz <= 0.0f || !std::isfinite(lowHz) || !std::isfinite(highHz) || totalEnergy <= 1e-9f)
        return 0.0f;

    const int maxBin = static_cast<int>(magnitudes.size()) - 1;
    const int lowBin = std::max(0, static_cast<int>(std::floor(lowHz / binHz)));
    const int highBin = std::min(maxBin, static_cast<int>(std::ceil(highHz / binHz)));
    if (highBin < lowBin)
        return 0.0f;

    double bandEnergy = 0.0;
    for (int bin = lowBin; bin <= highBin; ++bin)
        bandEnergy += static_cast<double>(magnitudes[static_cast<size_t>(bin)]) * static_cast<double>(magnitudes[static_cast<size_t>(bin)]);

    return static_cast<float>(bandEnergy / static_cast<double>(totalEnergy));
}

float FindPeakFrequencyInBand(
    const std::vector<float>& magnitudes,
    float binHz,
    float lowHz,
    float highHz)
{
    if (magnitudes.empty() || !std::isfinite(binHz) || binHz <= 0.0f || !std::isfinite(lowHz) || !std::isfinite(highHz))
        return -1.0f;

    const int maxBin = static_cast<int>(magnitudes.size()) - 1;
    const int lowBin = std::max(0, static_cast<int>(std::floor(lowHz / binHz)));
    const int highBin = std::min(maxBin, static_cast<int>(std::ceil(highHz / binHz)));
    if (highBin < lowBin)
        return -1.0f;

    int bestBin = -1;
    float bestMagnitude = -1.0f;
    for (int bin = lowBin; bin <= highBin; ++bin)
    {
        const float magnitude = magnitudes[static_cast<size_t>(bin)];
        if (magnitude > bestMagnitude)
        {
            bestMagnitude = magnitude;
            bestBin = bin;
        }
    }

    if (bestBin < 0)
        return -1.0f;

    float delta = 0.0f;
    if (bestBin > lowBin && bestBin < highBin)
    {
        const float left = magnitudes[static_cast<size_t>(bestBin - 1)];
        const float center = magnitudes[static_cast<size_t>(bestBin)];
        const float right = magnitudes[static_cast<size_t>(bestBin + 1)];
        const float denominator = (left - (2.0f * center) + right);
        if (std::isfinite(denominator) && std::abs(denominator) > 1e-12f)
        {
            delta = 0.5f * (left - right) / denominator;
            delta = ClampValue(delta, -1.0f, 1.0f);
        }
    }

    return (static_cast<float>(bestBin) + delta) * binHz;
}

float ComputeExpectedNoteBandScore(
    const std::vector<float>& magnitudes,
    float binHz,
    float totalEnergy,
    int midi,
    float toleranceCents)
{
    const float expectedHz = MidiToFrequencyHz(midi);
    if (!std::isfinite(expectedHz) || expectedHz <= 0.0f)
        return 0.0f;

    const float frequencyFactor = CentsToFrequencyFactor(std::max(1.0f, toleranceCents));
    const float lowHz = expectedHz / frequencyFactor;
    const float highHz = expectedHz * frequencyFactor;

    float score = ComputeBandEnergyRatio(magnitudes, binHz, lowHz, highHz, totalEnergy);

    const float secondHarmonic = expectedHz * 2.0f;
    if (secondHarmonic < (static_cast<float>(kSampleRate) * 0.5f) - 5.0f)
    {
        score += kFastChordSecondHarmonicWeight * ComputeBandEnergyRatio(
            magnitudes,
            binHz,
            secondHarmonic / frequencyFactor,
            secondHarmonic * frequencyFactor,
            totalEnergy);
    }

    const float thirdHarmonic = expectedHz * 3.0f;
    if (thirdHarmonic < (static_cast<float>(kSampleRate) * 0.5f) - 5.0f)
    {
        score += kFastChordThirdHarmonicWeight * ComputeBandEnergyRatio(
            magnitudes,
            binHz,
            thirdHarmonic / frequencyFactor,
            thirdHarmonic * frequencyFactor,
            totalEnergy);
    }

    return score;
}

float FindExpectedNotePeakFrequency(
    const std::vector<float>& magnitudes,
    float binHz,
    float totalEnergy,
    int midi,
    float toleranceCents)
{
    const float expectedHz = MidiToFrequencyHz(midi);
    if (!std::isfinite(expectedHz) || expectedHz <= 0.0f)
        return -1.0f;

    const float frequencyFactor = CentsToFrequencyFactor(std::max(1.0f, toleranceCents));
    float bestPeakHz = -1.0f;
    float bestScore = -1.0f;
    constexpr float harmonicWeights[] = { 1.0f, kFastChordSecondHarmonicWeight, kFastChordThirdHarmonicWeight };

    for (int harmonicIndex = 0; harmonicIndex < 3; ++harmonicIndex)
    {
        const float harmonicHz = expectedHz * static_cast<float>(harmonicIndex + 1);
        if (harmonicHz <= 0.0f || harmonicHz >= (static_cast<float>(kSampleRate) * 0.5f) - 5.0f)
            continue;

        const float lowHz = harmonicHz / frequencyFactor;
        const float highHz = harmonicHz * frequencyFactor;
        const float bandRatio = ComputeBandEnergyRatio(magnitudes, binHz, lowHz, highHz, totalEnergy);
        const float peakHz = FindPeakFrequencyInBand(magnitudes, binHz, lowHz, highHz);
        if (peakHz <= 0.0f)
            continue;

        const float score = bandRatio * harmonicWeights[harmonicIndex];
        if (score > bestScore)
        {
            bestScore = score;
            bestPeakHz = peakHz;
        }
    }

    return bestPeakHz;
}

float ComputeConstraintSupportBandRatio(
    const std::vector<float>& magnitudes,
    float binHz,
    float totalEnergy,
    const ExpectedHintNoteSpec& spec,
    float* lowHzOut = nullptr,
    float* highHzOut = nullptr)
{
    if (spec.openMidi < 0 || spec.midi < 0)
        return 0.0f;

    const int lowerMidi = spec.openMidi;
    const int upperMidi = spec.openMidi + 24;
    const float lowHz = MidiToFrequencyHz(lowerMidi) * (1.0f - kFastChordStringBandHeadroom);
    const float highHz = MidiToFrequencyHz(upperMidi) * (1.0f + kFastChordStringBandHeadroom);
    if (lowHzOut != nullptr)
        *lowHzOut = lowHz;
    if (highHzOut != nullptr)
        *highHzOut = highHz;
    return ComputeBandEnergyRatio(magnitudes, binHz, lowHz, highHz, totalEnergy);
}

float ComputeConstraintSupportThreshold(const ExpectedHintNoteSpec& spec)
{
    const bool isLegato = (spec.flags & ExpectedHintNoteFlagLegato) != 0;
    const bool isHarmonic = (spec.flags & ExpectedHintNoteFlagHarmonic) != 0;
    const bool isMotion = (spec.flags & (ExpectedHintNoteFlagBend | ExpectedHintNoteFlagSlide)) != 0;

    float threshold = isHarmonic
        ? kFastChordHarmonicStringEnergyThreshold
        : (isLegato ? kFastChordLegatoStringEnergyThreshold : kFastChordStringEnergyThreshold);

    // Full strums regularly bury the upper strings under low-string energy.
    // Relax the minimum band-energy requirement there, but keep lower strings stricter.
    if (spec.midi >= 59)
        threshold = std::min(threshold, 0.014f);
    else if (spec.midi >= 55)
        threshold = std::min(threshold, 0.016f);
    else if (spec.midi >= 50)
        threshold = std::min(threshold, 0.018f);

    if (isMotion)
        threshold *= 0.92f;

    return threshold;
}

float ComputeConstraintPitchScoreThreshold(const ExpectedHintNoteSpec& spec)
{
    const bool isMotion = (spec.flags & (ExpectedHintNoteFlagBend | ExpectedHintNoteFlagSlide)) != 0;

    float threshold = spec.midi <= 52
        ? kFastChordLowPitchScoreThreshold
        : kFastChordPitchScoreThreshold;

    if (spec.midi >= 59)
        threshold *= 0.72f;
    else if (spec.midi >= 55)
        threshold *= 0.82f;

    if (isMotion)
        threshold *= 0.75f;

    return threshold;
}

bool ScoreExpectedConstraintNote(
    const std::vector<float>& magnitudes,
    float binHz,
    float totalEnergy,
    const ExpectedHintNoteSpec& spec)
{
    const bool isMotion = (spec.flags & (ExpectedHintNoteFlagBend | ExpectedHintNoteFlagSlide)) != 0;
    const bool isHarmonic = (spec.flags & ExpectedHintNoteFlagHarmonic) != 0;

    float supportLowHz = 0.0f;
    float supportHighHz = 0.0f;
    const float supportBandRatio = ComputeConstraintSupportBandRatio(
        magnitudes,
        binHz,
        totalEnergy,
        spec,
        &supportLowHz,
        &supportHighHz);
    const float supportThreshold = ComputeConstraintSupportThreshold(spec);
    if (supportBandRatio < supportThreshold)
        return false;

    if (isHarmonic)
        return true;

    const float toleranceCents = isMotion
        ? kFastChordMotionToleranceCents
        : (spec.midi <= 52 ? kFastChordLowToleranceCents : kFastChordBaseToleranceCents);
    const float expectedHz = MidiToFrequencyHz(spec.midi);
    const float dominantBandPeakHz = FindExpectedNotePeakFrequency(magnitudes, binHz, totalEnergy, spec.midi, toleranceCents);
    if (dominantBandPeakHz <= 0.0f)
        return false;

    if (FrequencyDeltaCents(dominantBandPeakHz, expectedHz) > toleranceCents)
        return false;

    const float noteScore = ComputeExpectedNoteBandScore(magnitudes, binHz, totalEnergy, spec.midi, toleranceCents);
    if (noteScore < ComputeConstraintPitchScoreThreshold(spec))
        return false;

    if (spec.midi <= 52)
    {
        const float frequencyFactor = CentsToFrequencyFactor(std::max(1.0f, toleranceCents));
        const float fundamentalRatio = ComputeBandEnergyRatio(
            magnitudes,
            binHz,
            expectedHz / frequencyFactor,
            expectedHz * frequencyFactor,
            totalEnergy);
        const float minimumFundamentalRatio = spec.midi <= 45 ? 0.020f : 0.015f;
        if (fundamentalRatio < minimumFundamentalRatio)
            return false;
    }

    return true;
}

ConstraintChordEvaluationResult EvaluateExpectedChordConstraintWindow(
    const std::vector<float>& audioWindow,
    const std::vector<ExpectedHintNoteSpec>& expectedNotes,
    const DetectorSettings& settings)
{
    ConstraintChordEvaluationResult result;
    result.chordLeniency = settings.chordLeniency;

    result.expectedNotes.reserve(expectedNotes.size());
    for (const ExpectedHintNoteSpec& spec : expectedNotes)
    {
        if (spec.midi < 0 || spec.stringIndex < 0 || spec.openMidi < 0)
            continue;

        bool alreadyAdded = false;
        for (const ExpectedHintNoteSpec& existing : result.expectedNotes)
        {
            if (ExpectedHintNoteSpecsEqual(existing, spec))
            {
                alreadyAdded = true;
                break;
            }
        }

        if (!alreadyAdded)
            result.expectedNotes.push_back(spec);
    }

    result.totalExpected = static_cast<int>(result.expectedNotes.size());
    if (result.totalExpected < 2)
        return result;

    std::vector<float> magnitudes;
    float binHz = 0.0f;
    BuildMagnitudeSpectrum(audioWindow, static_cast<int>(audioWindow.size()), magnitudes, binHz);
    const float totalEnergy = ComputeMagnitudeTotalEnergy(magnitudes);
    if (magnitudes.empty() || totalEnergy <= 1e-9f)
        return result;

    result.noteResults.reserve(result.expectedNotes.size());
    for (const ExpectedHintNoteSpec& expectedNote : result.expectedNotes)
    {
        ConstraintChordNoteDebugResult noteResult;
        noteResult.spec = expectedNote;

        float supportLowHz = 0.0f;
        float supportHighHz = 0.0f;
        noteResult.supportRatio = ComputeConstraintSupportBandRatio(magnitudes, binHz, totalEnergy, expectedNote, &supportLowHz, &supportHighHz);
        noteResult.supportThreshold = ComputeConstraintSupportThreshold(expectedNote);
        const bool isMotion = (expectedNote.flags & (ExpectedHintNoteFlagBend | ExpectedHintNoteFlagSlide)) != 0;
        const bool isHarmonic = (expectedNote.flags & ExpectedHintNoteFlagHarmonic) != 0;
        const float toleranceCents = isMotion
            ? kFastChordMotionToleranceCents
            : (expectedNote.midi <= 52 ? kFastChordLowToleranceCents : kFastChordBaseToleranceCents);
        const float expectedHz = MidiToFrequencyHz(expectedNote.midi);
        const float frequencyFactor = CentsToFrequencyFactor(std::max(1.0f, toleranceCents));
        noteResult.fundamentalRatio = ComputeBandEnergyRatio(
            magnitudes,
            binHz,
            expectedHz / frequencyFactor,
            expectedHz * frequencyFactor,
            totalEnergy);
        for (int offset : { -2, -1, 1, 2 })
        {
            const int neighborMidi = expectedNote.midi + offset;
            if (neighborMidi < kContinuousMinMidi || neighborMidi > kContinuousMaxMidi)
                continue;

            const float neighborHz = MidiToFrequencyHz(neighborMidi);
            noteResult.neighborFundamentalMax = std::max(
                noteResult.neighborFundamentalMax,
                ComputeBandEnergyRatio(
                    magnitudes,
                    binHz,
                    neighborHz / frequencyFactor,
                    neighborHz * frequencyFactor,
                    totalEnergy));
        }
        noteResult.noteScore = isHarmonic
            ? 0.0f
            : ComputeExpectedNoteBandScore(magnitudes, binHz, totalEnergy, expectedNote.midi, toleranceCents);
        noteResult.noteScoreThreshold = isHarmonic
            ? 0.0f
            : ComputeConstraintPitchScoreThreshold(expectedNote);
        noteResult.dominantPeakHz = FindExpectedNotePeakFrequency(
            magnitudes,
            binHz,
            totalEnergy,
            expectedNote.midi,
            toleranceCents);
        noteResult.centsError = FrequencyDeltaCents(noteResult.dominantPeakHz, expectedHz);

        if (noteResult.supportRatio < noteResult.supportThreshold)
        {
            noteResult.hit = false;
        }
        else if (isHarmonic)
        {
            noteResult.hit = true;
        }
        else
        {
            const bool hasExpectedPeak = noteResult.dominantPeakHz > 0.0f && noteResult.centsError <= toleranceCents;
            const bool hasPitchSupport = noteResult.noteScore >= noteResult.noteScoreThreshold;
            const bool lowStringFundamentalOk = expectedNote.midi > 52 ||
                (expectedNote.midi <= 45 ? noteResult.fundamentalRatio >= 0.020f : noteResult.fundamentalRatio >= 0.015f);

            noteResult.hit = hasExpectedPeak && hasPitchSupport && lowStringFundamentalOk;
        }

        if (noteResult.hit)
            result.hitCount++;

        result.noteResults.push_back(noteResult);
    }

    result.requiredHits = result.totalExpected <= 2
        ? result.totalExpected
        : std::max(2, static_cast<int>(std::ceil(static_cast<double>(result.totalExpected) * ClampValue(settings.chordLeniency, 0.34f, 1.0f))));
    result.accepted = result.hitCount >= result.requiredHits;
    return result;
}

ConstraintSingleEvaluationResult EvaluateExpectedSingleConstraintWindow(
    const std::vector<float>& audioWindow,
    const ExpectedHintNoteSpec& expectedNote,
    const DetectorSettings&)
{
    ConstraintSingleEvaluationResult result;
    result.expectedNote = expectedNote;
    result.noteResult.spec = expectedNote;

    if (expectedNote.midi < 0 || expectedNote.stringIndex < 0 || expectedNote.openMidi < 0)
        return result;

    std::vector<float> magnitudes;
    float binHz = 0.0f;
    BuildMagnitudeSpectrum(audioWindow, static_cast<int>(audioWindow.size()), magnitudes, binHz);
    const float totalEnergy = ComputeMagnitudeTotalEnergy(magnitudes);
    if (magnitudes.empty() || totalEnergy <= 1e-9f)
        return result;

    float supportLowHz = 0.0f;
    float supportHighHz = 0.0f;
    result.noteResult.supportRatio = ComputeConstraintSupportBandRatio(magnitudes, binHz, totalEnergy, expectedNote, &supportLowHz, &supportHighHz);
    result.noteResult.supportThreshold = ComputeConstraintSupportThreshold(expectedNote);
    const bool isMotion = (expectedNote.flags & (ExpectedHintNoteFlagBend | ExpectedHintNoteFlagSlide)) != 0;
    const bool isHarmonic = (expectedNote.flags & ExpectedHintNoteFlagHarmonic) != 0;
    const float toleranceCents = isMotion
        ? kFastChordMotionToleranceCents
        : (expectedNote.midi <= 52 ? kFastChordLowToleranceCents : kFastChordBaseToleranceCents);
    const float expectedHz = MidiToFrequencyHz(expectedNote.midi);
    const float frequencyFactor = CentsToFrequencyFactor(std::max(1.0f, toleranceCents));
    result.noteResult.fundamentalRatio = ComputeBandEnergyRatio(
        magnitudes,
        binHz,
        expectedHz / frequencyFactor,
        expectedHz * frequencyFactor,
        totalEnergy);

    for (int offset : { -2, -1, 1, 2 })
    {
        const int neighborMidi = expectedNote.midi + offset;
        if (neighborMidi < kContinuousMinMidi || neighborMidi > kContinuousMaxMidi)
            continue;

        const float neighborHz = MidiToFrequencyHz(neighborMidi);
        result.noteResult.neighborFundamentalMax = std::max(
            result.noteResult.neighborFundamentalMax,
            ComputeBandEnergyRatio(
                magnitudes,
                binHz,
                neighborHz / frequencyFactor,
                neighborHz * frequencyFactor,
                totalEnergy));
    }

    result.noteResult.noteScore = isHarmonic
        ? 0.0f
        : ComputeExpectedNoteBandScore(magnitudes, binHz, totalEnergy, expectedNote.midi, toleranceCents);
    result.noteResult.noteScoreThreshold = isHarmonic
        ? 0.0f
        : ComputeConstraintPitchScoreThreshold(expectedNote);
    result.noteResult.dominantPeakHz = FindExpectedNotePeakFrequency(
        magnitudes,
        binHz,
        totalEnergy,
        expectedNote.midi,
        toleranceCents);
    result.noteResult.centsError = FrequencyDeltaCents(result.noteResult.dominantPeakHz, expectedHz);

    if (result.noteResult.supportRatio < result.noteResult.supportThreshold)
    {
        result.noteResult.hit = false;
    }
    else if (isHarmonic)
    {
        result.noteResult.hit = true;
    }
    else
    {
        const bool hasExpectedPeak = result.noteResult.dominantPeakHz > 0.0f && result.noteResult.centsError <= toleranceCents;
        const bool hasPitchSupport = result.noteResult.noteScore >= result.noteResult.noteScoreThreshold;
        const bool lowStringFundamentalOk = expectedNote.midi > 52 ||
            (expectedNote.midi <= 45 ? result.noteResult.fundamentalRatio >= 0.020f : result.noteResult.fundamentalRatio >= 0.015f);
        result.noteResult.hit = hasExpectedPeak && hasPitchSupport && lowStringFundamentalOk;
    }

    result.accepted = result.noteResult.hit;
    return result;
}

int GetFastSinglePrimaryAnalysisWindowSamples(const ExpectedHintNoteSpec& expectedNote)
{
    if (expectedNote.midi <= 45)
        return kFastSingleAnalysisWindowLongSamples;

    if (expectedNote.midi >= 59 || expectedNote.openMidi >= 59)
        return kFastSingleAnalysisWindowLongSamples;

    return kFastSingleAnalysisWindowShortSamples;
}

int GetFastSingleFallbackAnalysisWindowSamples(const ExpectedHintNoteSpec& expectedNote)
{
    if (expectedNote.midi <= 45)
        return std::max(kFastSingleAnalysisWindowLongSamples, kFastSingleAnalysisWindowFallbackSamples);

    if (expectedNote.midi >= 59 || expectedNote.openMidi >= 59)
        return kFastSingleAnalysisWindowFallbackSamples;

    return std::max(kFastSingleAnalysisWindowLongSamples, kFastSingleAnalysisWindowFallbackSamples);
}

FastSingleWindowEvaluationResult EvaluateFastSingleWindow(
    const std::vector<float>& audioWindow,
    const ExpectedHintNoteSpec& expectedNote,
    const DetectorSettings& settings)
{
    FastSingleWindowEvaluationResult result;
    result.spectral = EvaluateExpectedSingleConstraintWindow(audioWindow, expectedNote, settings);
    result.yin = EvaluateOfflineSingleExpectedNote(
        audioWindow.empty() ? nullptr : audioWindow.data(),
        static_cast<int>(audioWindow.size()),
        expectedNote.midi,
        settings);
    // Keep the spectral single-note constraint only as a diagnostic/helper signal.
    // Let the monophonic detector own actual fast single-note acceptance.
    // Chord-style band checks are too easy to fool with upper harmonics from
    // the wrong string on a mono guitar input.
    result.accepted = result.yin.accepted;
    return result;
}

void BuildRecentWindowFromSamples(
    const float* samples,
    int sampleCount,
    int endSampleExclusive,
    int windowSampleCount,
    std::vector<float>& destination)
{
    destination.assign(static_cast<size_t>(std::max(0, windowSampleCount)), 0.0f);
    if (samples == nullptr || sampleCount <= 0 || windowSampleCount <= 0)
        return;

    const int clampedEnd = ClampValue(endSampleExclusive, 0, sampleCount);
    const int available = std::min(windowSampleCount, clampedEnd);
    if (available <= 0)
        return;

    const int sourceStart = clampedEnd - available;
    const int destinationStart = windowSampleCount - available;
    memcpy(
        destination.data() + static_cast<size_t>(destinationStart),
        samples + static_cast<size_t>(sourceStart),
        static_cast<size_t>(available) * sizeof(float));
}

struct OfflineAubioContext
{
    fvec_t* hopInput = nullptr;
    fvec_t* onsetOutput = nullptr;
    fvec_t* pitchOutput = nullptr;
    aubio_onset_t* onset = nullptr;
    aubio_pitch_t* pitch = nullptr;

    ~OfflineAubioContext()
    {
        if (pitch != nullptr)
            del_aubio_pitch(pitch);
        if (onset != nullptr)
            del_aubio_onset(onset);
        if (pitchOutput != nullptr)
            del_fvec(pitchOutput);
        if (onsetOutput != nullptr)
            del_fvec(onsetOutput);
        if (hopInput != nullptr)
            del_fvec(hopInput);
    }

    bool Initialize()
    {
        hopInput = new_fvec(static_cast<uint_t>(kHopSize));
        onsetOutput = new_fvec(1);
        pitchOutput = new_fvec(1);
        onset = new_aubio_onset(const_cast<char_t*>("hfc"), static_cast<uint_t>(kOnsetWindowSize), static_cast<uint_t>(kHopSize), static_cast<uint_t>(kSampleRate));
        pitch = new_aubio_pitch(const_cast<char_t*>("yinfast"), static_cast<uint_t>(kPitchWindowSize), static_cast<uint_t>(kHopSize), static_cast<uint_t>(kSampleRate));
        if (hopInput == nullptr || onsetOutput == nullptr || pitchOutput == nullptr || onset == nullptr || pitch == nullptr)
            return false;

        aubio_pitch_set_unit(pitch, const_cast<char_t*>("midi"));
        aubio_pitch_set_tolerance(pitch, 0.82f);
        return true;
    }
};

OfflineSingleEvaluationResult EvaluateOfflineSingleExpectedNote(
    const float* samples,
    int sampleCount,
    int expectedMidi,
    const DetectorSettings& settings)
{
    OfflineSingleEvaluationResult result;
    if (samples == nullptr || sampleCount <= 0 || expectedMidi < 0)
        return result;

    OfflineAubioContext aubio;
    if (!aubio.Initialize())
        return result;

    const std::set<int> expectedMidiSet = { expectedMidi };
    const ExpectedContextKind expectedContext = GetExpectedContextKind(expectedMidiSet, settings);
    result.highStringContext = expectedContext == ExpectedContextKind::HighStringFocused;
    const float rmsGate = result.highStringContext ? (settings.continuousRmsGate * settings.highStringRmsMultiplier) : settings.continuousRmsGate;
    const float relaxedConfidenceGate = std::min(
        settings.continuousConfidenceGate,
        result.highStringContext ? (settings.continuousConfidenceGate * settings.highStringConfidenceMultiplier) : settings.continuousConfidenceGate);

    std::deque<int> recentPitchMidi;
    int stableMidi = -1;
    int stableCount = 0;
    double lastContinuousTime = -999.0;
    std::set<int> currentActiveNotes;
    std::vector<float> hop(static_cast<size_t>(kHopSize), 0.0f);
    const int totalHops = static_cast<int>(std::ceil(static_cast<double>(sampleCount) / static_cast<double>(kHopSize)));

    for (int hopIndex = 0; hopIndex < totalHops; ++hopIndex)
    {
        const int startSample = hopIndex * kHopSize;
        std::fill(hop.begin(), hop.end(), 0.0f);
        const int available = std::max(0, std::min(kHopSize, sampleCount - startSample));
        if (available > 0)
        {
            memcpy(hop.data(), samples + static_cast<size_t>(startSample), static_cast<size_t>(available) * sizeof(float));
        }

        const double currentTime = static_cast<double>(startSample + kHopSize) / static_cast<double>(kSampleRate);
        aubio_onset_set_threshold(aubio.onset, result.highStringContext ? settings.highStringOnsetThreshold : settings.standardOnsetThreshold);
        memcpy(aubio.hopInput->data, hop.data(), static_cast<size_t>(kHopSize) * sizeof(float));
        aubio_onset_do(aubio.onset, aubio.hopInput, aubio.onsetOutput);
        if (aubio.onsetOutput->data[0] > 0.0f)
        {
            result.onsetDetected = true;
            if (result.onsetHopIndex < 0)
                result.onsetHopIndex = hopIndex;
        }

        const float rms = ComputeRms(hop.data(), static_cast<int>(hop.size()));
        result.bestRms = std::max(result.bestRms, rms);
        if (!std::isfinite(rms) || rms < rmsGate)
        {
            recentPitchMidi.clear();
            stableMidi = -1;
            stableCount = 0;
            if ((currentTime - lastContinuousTime) > settings.continuousHoldSeconds)
                currentActiveNotes.clear();
            continue;
        }

        memcpy(aubio.hopInput->data, hop.data(), static_cast<size_t>(kHopSize) * sizeof(float));
        aubio_pitch_do(aubio.pitch, aubio.hopInput, aubio.pitchOutput);
        const float midiEstimate = aubio.pitchOutput->data[0];
        const float confidence = aubio_pitch_get_confidence(aubio.pitch);
        result.lastMidiEstimate = midiEstimate;
        result.lastConfidence = confidence;
        result.bestConfidence = std::max(result.bestConfidence, confidence);
        if (!std::isfinite(midiEstimate) ||
            !std::isfinite(confidence) ||
            midiEstimate < static_cast<float>(kContinuousMinMidi) ||
            midiEstimate > static_cast<float>(kContinuousMaxMidi))
        {
            if ((currentTime - lastContinuousTime) > settings.continuousHoldSeconds)
                currentActiveNotes.clear();
            continue;
        }

        const int candidateMidi = static_cast<int>(std::round(midiEstimate));
        bool accepted = confidence >= settings.continuousConfidenceGate;
        if (!accepted && result.highStringContext && confidence >= relaxedConfidenceGate)
        {
            const int bestDistance = SemitoneDistance(candidateMidi, expectedMidi);
            if (bestDistance <= settings.highStringBenefitMatchMaxDistance)
                accepted = true;
        }

        if (!accepted)
        {
            if ((currentTime - lastContinuousTime) > settings.continuousHoldSeconds)
                currentActiveNotes.clear();
            continue;
        }

        if (SemitoneDistance(candidateMidi, expectedMidi) > settings.expectMaxDistanceContinuous &&
            confidence < settings.expectStrictConfidence)
        {
            continue;
        }

        recentPitchMidi.push_back(candidateMidi);
        while (recentPitchMidi.size() > static_cast<size_t>(kContinuousMedianWindow))
            recentPitchMidi.pop_front();

        int mostCommonMidi = candidateMidi;
        int bestCount = 0;
        for (int value : recentPitchMidi)
        {
            const int count = static_cast<int>(std::count(recentPitchMidi.begin(), recentPitchMidi.end(), value));
            if (count > bestCount)
            {
                bestCount = count;
                mostCommonMidi = value;
            }
        }

        if (mostCommonMidi == stableMidi)
            ++stableCount;
        else
        {
            stableMidi = mostCommonMidi;
            stableCount = 1;
        }

        if (stableCount >= 1)
        {
            currentActiveNotes.clear();
            currentActiveNotes.insert(stableMidi);
            lastContinuousTime = currentTime;
        }

        if (result.detectedMidi < 0 && !currentActiveNotes.empty())
            result.detectedMidi = *currentActiveNotes.begin();

        if (!result.accepted && currentActiveNotes.find(expectedMidi) != currentActiveNotes.end())
        {
            result.accepted = true;
            result.acceptedHopIndex = hopIndex;
            result.detectedMidi = expectedMidi;
        }
    }

    if (!result.accepted && result.detectedMidi < 0 && stableMidi >= 0)
        result.detectedMidi = stableMidi;

    return result;
}

class PortAudioRuntime
{
public:
    ~PortAudioRuntime()
    {
        Shutdown();
    }

    bool Initialize(const std::wstring& pluginDirectory, std::wstring& error);
    void Shutdown();
    std::vector<NativeDeviceDescriptor> EnumerateInputDevices() const;
    int GetPreferredInputDeviceIndex(const std::vector<NativeDeviceDescriptor>& devices) const;
    bool OpenInputStream(int deviceIndex, int sampleRate, int inputChannelCount, unsigned long framesPerBuffer, double suggestedLatency, PaStreamCallback callback, void* userData, PaStream*& stream, std::wstring& error) const;
    void CloseStream(PaStream*& stream) const;

private:
    template <typename TFunction>
    void load_(TFunction& target, const char* name)
    {
        target = reinterpret_cast<TFunction>(GetDynamicLibrarySymbol(dll_, name));
    }

    DynamicLibraryHandle dll_ = nullptr;

    int(__cdecl* Pa_Initialize)() = nullptr;
    int(__cdecl* Pa_Terminate)() = nullptr;
    const char* (__cdecl* Pa_GetErrorText)(int) = nullptr;
    int(__cdecl* Pa_GetDeviceCount)() = nullptr;
    const void* (__cdecl* Pa_GetDeviceInfo)(int) = nullptr;
    const void* (__cdecl* Pa_GetHostApiInfo)(int) = nullptr;
    int(__cdecl* Pa_GetDefaultInputDevice)() = nullptr;
    int(__cdecl* Pa_OpenStream)(PaStream**, const PaStreamParameters*, const PaStreamParameters*, double, unsigned long, unsigned long, PaStreamCallback, void*) = nullptr;
    int(__cdecl* Pa_StartStream)(PaStream*) = nullptr;
    int(__cdecl* Pa_StopStream)(PaStream*) = nullptr;
    int(__cdecl* Pa_CloseStream)(PaStream*) = nullptr;
};

bool PortAudioRuntime::Initialize(const std::wstring& pluginDirectory, std::wstring& error)
{
    if (dll_ != nullptr)
    {
        error.clear();
        return true;
    }

    std::wstring loadError;
    std::filesystem::path dllPath = PluginLibraryPath(pluginDirectory, L"libportaudio64bit-asio.dll", "libportaudio.dylib", "libportaudio.so");
    dll_ = LoadDynamicLibrary(dllPath, loadError);
    if (dll_ == nullptr)
    {
        error = loadError;
        return false;
    }

    load_(Pa_Initialize, "Pa_Initialize");
    load_(Pa_Terminate, "Pa_Terminate");
    load_(Pa_GetErrorText, "Pa_GetErrorText");
    load_(Pa_GetDeviceCount, "Pa_GetDeviceCount");
    load_(Pa_GetDeviceInfo, "Pa_GetDeviceInfo");
    load_(Pa_GetHostApiInfo, "Pa_GetHostApiInfo");
    load_(Pa_GetDefaultInputDevice, "Pa_GetDefaultInputDevice");
    load_(Pa_OpenStream, "Pa_OpenStream");
    load_(Pa_StartStream, "Pa_StartStream");
    load_(Pa_StopStream, "Pa_StopStream");
    load_(Pa_CloseStream, "Pa_CloseStream");

    if (!Pa_Initialize || !Pa_Terminate || !Pa_GetErrorText || !Pa_GetDeviceCount || !Pa_GetDeviceInfo || !Pa_GetHostApiInfo ||
        !Pa_GetDefaultInputDevice || !Pa_OpenStream || !Pa_StartStream || !Pa_StopStream || !Pa_CloseStream)
    {
        error = L"PortAudio DLL is missing one or more required exports.";
        Shutdown();
        return false;
    }

    const int initResult = Pa_Initialize();
    if (initResult != kPaNoError)
    {
        error = Utf8ToWide(Pa_GetErrorText(initResult));
        Shutdown();
        return false;
    }

    error.clear();
    return true;
}

void PortAudioRuntime::Shutdown()
{
    if (dll_ == nullptr)
        return;

    if (Pa_Terminate)
        Pa_Terminate();

    CloseDynamicLibrary(dll_);
    dll_ = nullptr;
}

std::vector<NativeDeviceDescriptor> PortAudioRuntime::EnumerateInputDevices() const
{
    std::vector<NativeDeviceDescriptor> devices;
    if (dll_ == nullptr || Pa_GetDeviceCount == nullptr)
        return devices;

    const int count = Pa_GetDeviceCount();
    for (int i = 0; i < count; ++i)
    {
        const PaDeviceInfoNative* deviceInfo = reinterpret_cast<const PaDeviceInfoNative*>(Pa_GetDeviceInfo(i));
        if (deviceInfo == nullptr || deviceInfo->maxInputChannels <= 0)
            continue;

        std::string hostApiName;
        const PaHostApiInfoNative* hostApiInfo = reinterpret_cast<const PaHostApiInfoNative*>(Pa_GetHostApiInfo(deviceInfo->hostApi));
        if (hostApiInfo != nullptr && hostApiInfo->name != nullptr)
            hostApiName = hostApiInfo->name;

        NativeDeviceDescriptor descriptor;
        descriptor.index = i;
        descriptor.name = deviceInfo->name != nullptr ? deviceInfo->name : ("Device " + std::to_string(i));
        descriptor.displayName = std::to_string(i) + ": " + descriptor.name;
        descriptor.hostApiName = hostApiName;
        descriptor.maxInputChannels = deviceInfo->maxInputChannels;
        descriptor.defaultSampleRate = deviceInfo->defaultSampleRate;
        descriptor.defaultLowInputLatency = deviceInfo->defaultLowInputLatency;
        devices.push_back(std::move(descriptor));
    }

    std::sort(devices.begin(), devices.end(), [](const NativeDeviceDescriptor& a, const NativeDeviceDescriptor& b)
    {
        const int priorityCompare = HostPriority(a.hostApiName) - HostPriority(b.hostApiName);
        if (priorityCompare != 0)
            return priorityCompare < 0;
        return a.index < b.index;
    });

    return devices;
}

int PortAudioRuntime::GetPreferredInputDeviceIndex(const std::vector<NativeDeviceDescriptor>& devices) const
{
    if (devices.empty())
        return -1;

    const int defaultIndex = Pa_GetDefaultInputDevice ? Pa_GetDefaultInputDevice() : kPaNoDevice;
    if (defaultIndex != kPaNoDevice)
    {
        for (const NativeDeviceDescriptor& device : devices)
        {
            if (device.index == defaultIndex)
                return device.index;
        }
    }

    return devices.front().index;
}

bool PortAudioRuntime::OpenInputStream(
    int deviceIndex,
    int sampleRate,
    int inputChannelCount,
    unsigned long framesPerBuffer,
    double suggestedLatency,
    PaStreamCallback callback,
    void* userData,
    PaStream*& stream,
    std::wstring& error) const
{
    stream = nullptr;

    PaStreamParameters inputParameters{};
    inputParameters.device = deviceIndex;
    inputParameters.channelCount = std::max(1, inputChannelCount);
    inputParameters.sampleFormat = kPaFloat32;
    inputParameters.suggestedLatency = suggestedLatency;
    inputParameters.hostApiSpecificStreamInfo = nullptr;

    const int openResult = Pa_OpenStream(
        &stream,
        &inputParameters,
        nullptr,
        static_cast<double>(sampleRate),
        framesPerBuffer,
        kPaNoFlag,
        callback,
        userData);

    if (openResult != kPaNoError || stream == nullptr)
    {
        error = Utf8ToWide(Pa_GetErrorText(openResult));
        stream = nullptr;
        return false;
    }

    const int startResult = Pa_StartStream(stream);
    if (startResult != kPaNoError)
    {
        error = Utf8ToWide(Pa_GetErrorText(startResult));
        Pa_CloseStream(stream);
        stream = nullptr;
        return false;
    }

    error.clear();
    return true;
}

void PortAudioRuntime::CloseStream(PaStream*& stream) const
{
    if (stream == nullptr)
        return;

    Pa_StopStream(stream);
    Pa_CloseStream(stream);
    stream = nullptr;
}

class OrtRuntime
{
public:
    ~OrtRuntime()
    {
        Shutdown();
    }

    bool Initialize(const std::wstring& pluginDirectory, const std::wstring& modelPath, std::wstring& error);
    void Shutdown();
    bool RunNoteAndOnsetInference(const std::vector<float>& audio, std::vector<float>& note, std::vector<float>& onset, std::wstring& error);

private:
    bool check_(OrtStatus* status, std::wstring& error, const wchar_t* context);
    void releaseValue_(OrtValue* value);

    DynamicLibraryHandle dll_ = nullptr;
    const OrtApi* api_ = nullptr;
    OrtEnv* env_ = nullptr;
    OrtSessionOptions* sessionOptions_ = nullptr;
    OrtSession* session_ = nullptr;
    OrtMemoryInfo* cpuMemoryInfo_ = nullptr;
    OrtStatus* (ORT_API_CALL* appendCpuProvider_)(OrtSessionOptions*, int) = nullptr;
};

bool OrtRuntime::Initialize(const std::wstring& pluginDirectory, const std::wstring& modelPath, std::wstring& error)
{
    if (api_ != nullptr && session_ != nullptr)
    {
        error.clear();
        return true;
    }

    std::wstring loadError;
    const std::filesystem::path ortDllPath = PluginLibraryPath(pluginDirectory, L"onnxruntime.dll", "libonnxruntime.dylib", "libonnxruntime.so");
    dll_ = LoadDynamicLibrary(ortDllPath, loadError);
    if (dll_ == nullptr)
    {
        error = loadError;
        return false;
    }

    const auto getApiBase = reinterpret_cast<const OrtApiBase* (ORT_API_CALL*)(void)>(GetDynamicLibrarySymbol(dll_, "OrtGetApiBase"));
    appendCpuProvider_ = reinterpret_cast<OrtStatus* (ORT_API_CALL*)(OrtSessionOptions*, int)>(GetDynamicLibrarySymbol(dll_, "OrtSessionOptionsAppendExecutionProvider_CPU"));
    if (getApiBase == nullptr)
    {
        error = L"onnxruntime.dll is missing OrtGetApiBase.";
        Shutdown();
        return false;
    }

    const OrtApiBase* apiBase = getApiBase();
    api_ = apiBase != nullptr ? apiBase->GetApi(ORT_API_VERSION) : nullptr;
    if (api_ == nullptr)
    {
        error = L"Failed to acquire ONNX Runtime C API.";
        Shutdown();
        return false;
    }

    if (!check_(api_->CreateEnv(ORT_LOGGING_LEVEL_WARNING, "NativeNotesDetector", &env_), error, L"CreateEnv"))
        return false;
    if (!check_(api_->CreateSessionOptions(&sessionOptions_), error, L"CreateSessionOptions"))
        return false;

    api_->SetIntraOpNumThreads(sessionOptions_, 1);
    api_->SetInterOpNumThreads(sessionOptions_, 1);
    api_->SetSessionGraphOptimizationLevel(sessionOptions_, ORT_ENABLE_ALL);
    if (appendCpuProvider_ != nullptr)
    {
        OrtStatus* providerStatus = appendCpuProvider_(sessionOptions_, 1);
        if (providerStatus != nullptr)
            api_->ReleaseStatus(providerStatus);
    }

#ifdef _WIN32
    const ORTCHAR_T* modelPathForOrt = modelPath.c_str();
#else
    const std::string modelPathUtf8 = WideToUtf8(modelPath);
    const ORTCHAR_T* modelPathForOrt = modelPathUtf8.c_str();
#endif

    if (!check_(api_->CreateSession(env_, modelPathForOrt, sessionOptions_, &session_), error, L"CreateSession"))
        return false;
    if (!check_(api_->CreateCpuMemoryInfo(OrtArenaAllocator, OrtMemTypeDefault, &cpuMemoryInfo_), error, L"CreateCpuMemoryInfo"))
        return false;

    error.clear();
    return true;
}

void OrtRuntime::Shutdown()
{
    if (api_ != nullptr)
    {
        if (cpuMemoryInfo_ != nullptr)
            api_->ReleaseMemoryInfo(cpuMemoryInfo_);
        if (session_ != nullptr)
            api_->ReleaseSession(session_);
        if (sessionOptions_ != nullptr)
            api_->ReleaseSessionOptions(sessionOptions_);
        if (env_ != nullptr)
            api_->ReleaseEnv(env_);
    }

    cpuMemoryInfo_ = nullptr;
    session_ = nullptr;
    sessionOptions_ = nullptr;
    env_ = nullptr;
    api_ = nullptr;

    if (dll_ != nullptr)
    {
        CloseDynamicLibrary(dll_);
        dll_ = nullptr;
    }
}

bool OrtRuntime::RunNoteAndOnsetInference(const std::vector<float>& audio, std::vector<float>& note, std::vector<float>& onset, std::wstring& error)
{
    if (api_ == nullptr || session_ == nullptr || cpuMemoryInfo_ == nullptr)
    {
        error = L"ONNX Runtime is not initialized.";
        return false;
    }

    const char* inputNames[] = { "serving_default_input_2:0" };
    const char* outputNames[] = { "StatefulPartitionedCall:1", "StatefulPartitionedCall:2" };
    const int overlapLength = kModelOverlapFrames * kModelFftHop;
    const int overlapPrefix = overlapLength / 2;
    const int windowHopSize = kModelInputSamples - overlapLength;
    const int unwrappedFramesPerWindow = kModelOutputFrames - kModelOverlapFrames;
    const int originalLength = static_cast<int>(audio.size());

    std::vector<float> paddedAudio(static_cast<size_t>(overlapPrefix + originalLength), 0.0f);
    if (originalLength > 0)
        memcpy(paddedAudio.data() + overlapPrefix, audio.data(), static_cast<size_t>(originalLength) * sizeof(float));

    std::vector<float> batchedNote;
    std::vector<float> batchedOnset;
    batchedNote.reserve(static_cast<size_t>(kModelOutputFrames * kModelOutputPitches * 2));
    batchedOnset.reserve(static_cast<size_t>(kModelOutputFrames * kModelOutputPitches * 2));

    auto runSingleWindow = [&](const float* windowData) -> bool
    {
        const int64_t inputShape[] = { 1, kModelInputSamples, 1 };
        OrtValue* inputTensor = nullptr;
        if (!check_(api_->CreateTensorWithDataAsOrtValue(
            cpuMemoryInfo_,
            const_cast<float*>(windowData),
            static_cast<size_t>(kModelInputSamples) * sizeof(float),
            inputShape,
            3,
            ONNX_TENSOR_ELEMENT_DATA_TYPE_FLOAT,
            &inputTensor), error, L"CreateTensorWithDataAsOrtValue"))
        {
            return false;
        }

        OrtValue* outputValues[2] = { nullptr, nullptr };
        const OrtValue* inputValues[] = { inputTensor };
        const bool runOk = check_(api_->Run(
            session_,
            nullptr,
            inputNames,
            inputValues,
            1,
            outputNames,
            2,
            outputValues), error, L"Run");

        api_->ReleaseValue(inputTensor);
        if (!runOk)
        {
            releaseValue_(outputValues[0]);
            releaseValue_(outputValues[1]);
            return false;
        }

        float* noteData = nullptr;
        float* onsetData = nullptr;
        if (!check_(api_->GetTensorMutableData(outputValues[0], reinterpret_cast<void**>(&noteData)), error, L"GetTensorMutableData(note)") ||
            !check_(api_->GetTensorMutableData(outputValues[1], reinterpret_cast<void**>(&onsetData)), error, L"GetTensorMutableData(onset)"))
        {
            releaseValue_(outputValues[0]);
            releaseValue_(outputValues[1]);
            return false;
        }

        batchedNote.insert(batchedNote.end(), noteData, noteData + (kModelOutputFrames * kModelOutputPitches));
        batchedOnset.insert(batchedOnset.end(), onsetData, onsetData + (kModelOutputFrames * kModelOutputPitches));

        releaseValue_(outputValues[0]);
        releaseValue_(outputValues[1]);
        return true;
    };

    std::vector<float> window(static_cast<size_t>(kModelInputSamples), 0.0f);
    bool ranAnyWindow = false;
    for (int start = 0; start < static_cast<int>(paddedAudio.size()); start += windowHopSize)
    {
        std::fill(window.begin(), window.end(), 0.0f);
        const int copySamples = std::min(kModelInputSamples, static_cast<int>(paddedAudio.size()) - start);
        if (copySamples > 0)
            memcpy(window.data(), paddedAudio.data() + start, static_cast<size_t>(copySamples) * sizeof(float));

        if (!runSingleWindow(window.data()))
            return false;
        ranAnyWindow = true;
    }

    if (!ranAnyWindow)
    {
        error = L"No audio windows were generated for ONNX inference.";
        return false;
    }

    const int batchCount = static_cast<int>(batchedNote.size() / static_cast<size_t>(kModelOutputFrames * kModelOutputPitches));
    const int trimFrames = kModelTrimFrames;
    const int outputFramesOriginal = static_cast<int>(std::floor(
        static_cast<double>(originalLength) * static_cast<double>(kModelAnnotationsFps) / static_cast<double>(kSampleRate)));

    const int unwrappedFramesAvailable = batchCount * unwrappedFramesPerWindow;
    const int framesToKeep = std::max(0, std::min(outputFramesOriginal, unwrappedFramesAvailable));

    note.clear();
    onset.clear();
    note.reserve(static_cast<size_t>(framesToKeep * kModelOutputPitches));
    onset.reserve(static_cast<size_t>(framesToKeep * kModelOutputPitches));

    for (int batch = 0; batch < batchCount && static_cast<int>(note.size() / kModelOutputPitches) < framesToKeep; ++batch)
    {
        for (int frame = trimFrames; frame < (kModelOutputFrames - trimFrames) && static_cast<int>(note.size() / kModelOutputPitches) < framesToKeep; ++frame)
        {
            const size_t sourceIndex = static_cast<size_t>((batch * kModelOutputFrames + frame) * kModelOutputPitches);
            note.insert(note.end(), batchedNote.begin() + static_cast<std::ptrdiff_t>(sourceIndex), batchedNote.begin() + static_cast<std::ptrdiff_t>(sourceIndex + kModelOutputPitches));
            onset.insert(onset.end(), batchedOnset.begin() + static_cast<std::ptrdiff_t>(sourceIndex), batchedOnset.begin() + static_cast<std::ptrdiff_t>(sourceIndex + kModelOutputPitches));
        }
    }

    error.clear();
    return true;
}

bool OrtRuntime::check_(OrtStatus* status, std::wstring& error, const wchar_t* context)
{
    if (status == nullptr)
        return true;

    const char* message = api_ != nullptr ? api_->GetErrorMessage(status) : nullptr;
    std::wstring wideMessage = Utf8ToWide(message != nullptr ? message : "unknown ONNX Runtime error");
    api_->ReleaseStatus(status);
    error = std::wstring(context) + L": " + wideMessage;
    return false;
}

void OrtRuntime::releaseValue_(OrtValue* value)
{
    if (value != nullptr && api_ != nullptr)
        api_->ReleaseValue(value);
}

class HintState
{
public:
    void SetSync(double unitySongTime, double pythonAudioTime);
    void ClearWindows();
    void AddHintWindow(double startTime, double endTime, const std::set<int>& midiNotes, const std::vector<ExpectedHintNoteSpec>& expectedNotes = {});
    std::set<int> GetExpectedNotesForPythonTime(double pythonAudioTime, double* unitySongTime);
    std::set<int> GetExpectedNotesNearPythonTime(double pythonAudioTime, double lookaheadSeconds, double* unitySongTime);
    ExpectedHintContext GetExpectedContextForPythonTime(double pythonAudioTime, double* unitySongTime);
    ExpectedHintContext GetExpectedContextNearPythonTime(double pythonAudioTime, double lookaheadSeconds, double* unitySongTime);
    void ParsePayload(const std::string& payload, double pythonAudioTime);
    void Prune();

private:
    static std::vector<std::string> split_(const std::string& text, char delimiter);
    static std::set<int> parseMidiSet_(const std::string& csv);
    static std::vector<ExpectedHintNoteSpec> parseExpectedNoteSpecs_(const std::string& csv);
    static bool noteSpecsEqual_(const ExpectedHintNoteSpec& left, const ExpectedHintNoteSpec& right);
    static void appendUniqueExpectedNotes_(std::vector<ExpectedHintNoteSpec>& destination, const std::vector<ExpectedHintNoteSpec>& source);
    void pruneLocked_();

    mutable std::mutex mutex_;
    mutable std::deque<HintWindow> windows_;
    double offset_ = 0.0;
    bool hasOffset_ = false;
    double lastUnityTime_ = 0.0;
    double lastPythonTimeAtSync_ = 0.0;
};

void HintState::SetSync(double unitySongTime, double pythonAudioTime)
{
    std::lock_guard<std::mutex> lock(mutex_);
    const double newOffset = pythonAudioTime - unitySongTime;
    if (!hasOffset_)
    {
        offset_ = newOffset;
        hasOffset_ = true;
    }
    else if (std::abs(newOffset - offset_) > kUnitySyncSnapThresholdSeconds)
    {
        offset_ = newOffset;
    }
    else
    {
        offset_ = (1.0 - kUnitySyncAlpha) * offset_ + kUnitySyncAlpha * newOffset;
    }
    lastUnityTime_ = unitySongTime;
    lastPythonTimeAtSync_ = pythonAudioTime;
}

void HintState::AddHintWindow(double startTime, double endTime, const std::set<int>& midiNotes, const std::vector<ExpectedHintNoteSpec>& expectedNotes)
{
    if (midiNotes.empty() && expectedNotes.empty())
        return;

    HintWindow window;
    window.startTime = std::min(startTime, endTime);
    window.endTime = std::max(startTime, endTime);
    window.midiNotes = midiNotes;
    window.expectedNotes = expectedNotes;

    std::lock_guard<std::mutex> lock(mutex_);
    for (ExpectedHintNoteSpec& expectedNote : window.expectedNotes)
    {
        if (expectedNote.midi >= 0)
            window.midiNotes.insert(expectedNote.midi);
        if (hasOffset_ && expectedNote.noteTime >= 0.0)
            expectedNote.notePythonTime = expectedNote.noteTime + offset_;
    }
    window.createdAt = Clock::now();

    windows_.push_back(std::move(window));
    pruneLocked_();
}

void HintState::ClearWindows()
{
    std::lock_guard<std::mutex> lock(mutex_);
    windows_.clear();
}

std::set<int> HintState::GetExpectedNotesForPythonTime(double pythonAudioTime, double* unitySongTime)
{
    return GetExpectedContextForPythonTime(pythonAudioTime, unitySongTime).midiNotes;
}

ExpectedHintContext HintState::GetExpectedContextForPythonTime(double pythonAudioTime, double* unitySongTime)
{
    std::lock_guard<std::mutex> lock(mutex_);
    pruneLocked_();
    if (!hasOffset_)
    {
        if (unitySongTime != nullptr)
            *unitySongTime = -1.0;
        return {};
    }

    const double unityTime = pythonAudioTime - offset_;
    if (unitySongTime != nullptr)
        *unitySongTime = unityTime;

    ExpectedHintContext result;
    for (const HintWindow& window : windows_)
    {
        if (window.startTime <= unityTime && unityTime <= window.endTime)
        {
            result.midiNotes.insert(window.midiNotes.begin(), window.midiNotes.end());
            appendUniqueExpectedNotes_(result.expectedNotes, window.expectedNotes);
            if (!result.hasWindow)
            {
                result.hasWindow = true;
                result.windowStartTime = window.startTime;
                result.windowEndTime = window.endTime;
                result.windowStartPythonTime = window.startTime + offset_;
                result.windowEndPythonTime = window.endTime + offset_;
            }
            else
            {
                result.windowStartTime = std::min(result.windowStartTime, window.startTime);
                result.windowEndTime = std::max(result.windowEndTime, window.endTime);
                result.windowStartPythonTime = std::min(result.windowStartPythonTime, window.startTime + offset_);
                result.windowEndPythonTime = std::max(result.windowEndPythonTime, window.endTime + offset_);
            }
        }
    }
    return result;
}

std::set<int> HintState::GetExpectedNotesNearPythonTime(double pythonAudioTime, double lookaheadSeconds, double* unitySongTime)
{
    return GetExpectedContextNearPythonTime(pythonAudioTime, lookaheadSeconds, unitySongTime).midiNotes;
}

ExpectedHintContext HintState::GetExpectedContextNearPythonTime(double pythonAudioTime, double lookaheadSeconds, double* unitySongTime)
{
    std::lock_guard<std::mutex> lock(mutex_);
    pruneLocked_();
    if (!hasOffset_)
    {
        if (unitySongTime != nullptr)
            *unitySongTime = -1.0;
        return {};
    }

    const double unityTime = pythonAudioTime - offset_;
    if (unitySongTime != nullptr)
        *unitySongTime = unityTime;

    ExpectedHintContext currentResult;
    ExpectedHintContext futureResult;
    const double futureUnityTime = unityTime + lookaheadSeconds;
    for (const HintWindow& window : windows_)
    {
        if (window.startTime <= unityTime && unityTime <= window.endTime)
        {
            currentResult.midiNotes.insert(window.midiNotes.begin(), window.midiNotes.end());
            appendUniqueExpectedNotes_(currentResult.expectedNotes, window.expectedNotes);
            if (!currentResult.hasWindow)
            {
                currentResult.hasWindow = true;
                currentResult.windowStartTime = window.startTime;
                currentResult.windowEndTime = window.endTime;
                currentResult.windowStartPythonTime = window.startTime + offset_;
                currentResult.windowEndPythonTime = window.endTime + offset_;
            }
            else
            {
                currentResult.windowStartTime = std::min(currentResult.windowStartTime, window.startTime);
                currentResult.windowEndTime = std::max(currentResult.windowEndTime, window.endTime);
                currentResult.windowStartPythonTime = std::min(currentResult.windowStartPythonTime, window.startTime + offset_);
                currentResult.windowEndPythonTime = std::max(currentResult.windowEndPythonTime, window.endTime + offset_);
            }
        }
        else if (window.startTime <= futureUnityTime && futureUnityTime <= window.endTime)
        {
            futureResult.midiNotes.insert(window.midiNotes.begin(), window.midiNotes.end());
            appendUniqueExpectedNotes_(futureResult.expectedNotes, window.expectedNotes);
            if (!futureResult.hasWindow)
            {
                futureResult.hasWindow = true;
                futureResult.windowStartTime = window.startTime;
                futureResult.windowEndTime = window.endTime;
                futureResult.windowStartPythonTime = window.startTime + offset_;
                futureResult.windowEndPythonTime = window.endTime + offset_;
            }
            else
            {
                futureResult.windowStartTime = std::min(futureResult.windowStartTime, window.startTime);
                futureResult.windowEndTime = std::max(futureResult.windowEndTime, window.endTime);
                futureResult.windowStartPythonTime = std::min(futureResult.windowStartPythonTime, window.startTime + offset_);
                futureResult.windowEndPythonTime = std::max(futureResult.windowEndPythonTime, window.endTime + offset_);
            }
        }
    }

    if (!currentResult.midiNotes.empty() || !currentResult.expectedNotes.empty())
        return currentResult;

    return futureResult;
}

void HintState::ParsePayload(const std::string& payload, double pythonAudioTime)
{
    if (payload.empty())
        return;

    std::vector<std::string> parts = split_(payload, '|');
    if (parts.empty())
        return;

    std::string command = parts[0];
    for (char& ch : command)
        ch = static_cast<char>(::toupper(static_cast<unsigned char>(ch)));

    if ((command == "SYNC" || command == "TIME") && parts.size() >= 2)
    {
        try
        {
            SetSync(std::stod(parts.back()), pythonAudioTime);
        }
        catch (...)
        {
        }
        return;
    }

    if (command == "CLEAR" || command == "SYNCCLEAR" || command == "TIMECLEAR")
    {
        if (parts.size() >= 2)
        {
            try
            {
                SetSync(std::stod(parts.back()), pythonAudioTime);
            }
            catch (...)
            {
            }
        }

        ClearWindows();
        return;
    }

    if ((command == "HINTCLEAR" || command == "EXPECTCLEAR") && parts.size() >= 2)
    {
        double currentSongTime = 0.0;
        try
        {
            currentSongTime = std::stod(parts[1]);
        }
        catch (...)
        {
            return;
        }

        SetSync(currentSongTime, pythonAudioTime);
        ClearWindows();

        for (size_t i = 2; i < parts.size(); ++i)
        {
            if (parts[i].empty())
                continue;

            std::vector<std::string> fields = split_(parts[i], ':');
            if (fields.size() >= 3)
            {
                try
                {
                    AddHintWindow(
                        std::stod(fields[0]),
                        std::stod(fields[1]),
                        parseMidiSet_(fields[2]),
                        fields.size() >= 4 ? parseExpectedNoteSpecs_(fields[3]) : std::vector<ExpectedHintNoteSpec>{});
                }
                catch (...)
                {
                }
            }
            else
            {
                AddHintWindow(currentSongTime - 0.07, currentSongTime + 0.22, parseMidiSet_(parts[i]));
            }
        }
        return;
    }

    if ((command == "HINT" || command == "EXPECT") && parts.size() >= 2)
    {
        double currentSongTime = 0.0;
        try
        {
            currentSongTime = std::stod(parts[1]);
        }
        catch (...)
        {
            return;
        }

        SetSync(currentSongTime, pythonAudioTime);

        for (size_t i = 2; i < parts.size(); ++i)
        {
            if (parts[i].empty())
                continue;

            std::vector<std::string> fields = split_(parts[i], ':');
            if (fields.size() >= 3)
            {
                try
                {
                    AddHintWindow(
                        std::stod(fields[0]),
                        std::stod(fields[1]),
                        parseMidiSet_(fields[2]),
                        fields.size() >= 4 ? parseExpectedNoteSpecs_(fields[3]) : std::vector<ExpectedHintNoteSpec>{});
                }
                catch (...)
                {
                }
            }
            else
            {
                AddHintWindow(currentSongTime - 0.07, currentSongTime + 0.22, parseMidiSet_(parts[i]));
            }
        }
    }
}

void HintState::Prune()
{
    std::lock_guard<std::mutex> lock(mutex_);
    pruneLocked_();
}

std::vector<std::string> HintState::split_(const std::string& text, char delimiter)
{
    std::vector<std::string> parts;
    std::string current;
    for (char ch : text)
    {
        if (ch == delimiter)
        {
            parts.push_back(current);
            current.clear();
        }
        else
        {
            current.push_back(ch);
        }
    }
    parts.push_back(current);
    return parts;
}

std::set<int> HintState::parseMidiSet_(const std::string& csv)
{
    std::set<int> result;
    std::string current;
    for (size_t i = 0; i <= csv.size(); ++i)
    {
        if (i == csv.size() || csv[i] == ',')
        {
            if (!current.empty())
            {
                const int midi = ParseHintTokenToMidi(current);
                if (midi >= 0)
                    result.insert(midi);
            }
            current.clear();
        }
        else if (csv[i] != ' ' && csv[i] != '\t')
        {
            current.push_back(csv[i]);
        }
    }
    return result;
}

std::vector<ExpectedHintNoteSpec> HintState::parseExpectedNoteSpecs_(const std::string& csv)
{
    return ParseExpectedHintNoteSpecsCsv(csv);
}

bool HintState::noteSpecsEqual_(const ExpectedHintNoteSpec& left, const ExpectedHintNoteSpec& right)
{
    return ExpectedHintNoteSpecsEqual(left, right);
}

void HintState::appendUniqueExpectedNotes_(std::vector<ExpectedHintNoteSpec>& destination, const std::vector<ExpectedHintNoteSpec>& source)
{
    AppendUniqueExpectedNotes(destination, source);
}

void HintState::pruneLocked_()
{
    const auto now = Clock::now();
    while (!windows_.empty())
    {
        const double age = std::chrono::duration<double>(now - windows_.front().createdAt).count();
        if (age <= kHintRetentionSeconds)
            break;
        windows_.pop_front();
    }
}

class NativeDetectorEngine
{
public:
    NativeDetectorEngine();
    ~NativeDetectorEngine();

    bool Initialize(const std::wstring& modelPath, const std::wstring& dataDirectory, std::wstring& error);
    bool Start(int inputDeviceIndex, int inputChannelMode, std::wstring& error);
    bool StartSharedInput(int inputDeviceIndex, int sampleRate, int inputChannelCount, int inputChannelMode, int maxBlockFrames, const std::string& sourceLabel, const std::string& hostApiName, std::wstring& error);
    bool SubmitSharedInput(const float* input, int frameCount, int inputChannelCount, int sampleRate, int inputChannelMode);
    void Stop();
    void Shutdown();
    void SetHintPayload(const std::string& payload);
    std::string PollLatestPacket() const;
    std::string PollVerifierVerdictsJson();
    std::string GetStatusLine() const;
    std::wstring GetLastError() const;
    bool IsRunning() const;
    std::string ListInputDevicesJson() const;
    std::string GetRuntimeInfoJson() const;
    void ApplySettingsJson(const std::string& settingsJson);
    void SetResamplerMode(int mode);

private:
    static int __cdecl PortAudioCallback_(const void* input, void* output, unsigned long frameCount, const PaStreamCallbackTimeInfo*, PaStreamCallbackFlags, void* userData);
    int onAudio_(const float* input, int frameCount);
    void FastLoop_();
    void DeepLoop_();
    void pumpDeepResults_(double currentTime);
    void maybeDispatchFastChordTasks_(uint64_t availableFrames, double currentTime);
    void maybeDispatchFastSingleTasks_(uint64_t availableFrames, double currentTime, const std::set<int>& currentActiveNotes);
    void maybeRunExpectedNoteVerifier_(uint64_t availableFrames, double currentTime, const DetectorSettings& settings, const std::deque<double>& onsetTimes);
    void maybeDispatchCaptureTasks_(uint64_t availableFrames);
    bool scoreExpectedChordConstraint_(const std::vector<float>& audioWindow, const std::vector<ExpectedHintNoteSpec>& expectedNotes, const DetectorSettings& settings, const char* sourceTag) const;
    bool tryScoreFastExpectedChord_(uint64_t endFrameExclusive, const std::vector<ExpectedHintNoteSpec>& expectedNotes, const DetectorSettings& settings) const;
    bool scoreExpectedSingleConstraint_(const std::vector<float>& audioWindow, const ExpectedHintNoteSpec& expectedNote, const DetectorSettings& settings, const char* sourceTag) const;
    bool tryScoreFastExpectedSingle_(uint64_t endFrameExclusive, const ExpectedHintNoteSpec& expectedNote, const DetectorSettings& settings) const;
    void publishFastChordEvent_(int eventId, double onsetTime, double currentTime, const std::set<int>& expectedMidi);
    void publishFastSingleEvent_(int eventId, double onsetTime, double currentTime, int expectedMidi);
    void publishVerifierVerdicts_(const VerifierExpectedGroup& group, const ConstraintChordEvaluationResult& evaluation, double currentTime, const char* sourceTag);
    void publishVerifierVerdict_(const ExpectedHintNoteSpec& spec, const ConstraintChordNoteDebugResult& noteResult, double currentTime, const char* sourceTag);
    std::vector<VerifierExpectedGroup> buildVerifierGroups_(const ExpectedHintContext& context) const;
    bool verifierGroupHasOnset_(const VerifierExpectedGroup& group, const std::deque<double>& onsetTimes) const;
    void resetVerifierStateLocked_();
    bool initializeAubio_(std::wstring& error);
    void shutdownAubio_();
    void updateContinuousNotes_(const std::vector<float>& hop, double currentTime, std::deque<int>& recentPitchMidi, int& stableMidi, int& stableCount, double& lastContinuousTime, std::set<int>& currentActiveNotes);
    bool detectOnset_(const std::vector<float>& hop, const std::set<int>& expectedMidi);
    bool detectPitchYin_(const std::vector<float>& hop, float& midiOut, float& confidenceOut);
    std::vector<NoteEventCandidate> decodeBasicPitch_(const std::vector<float>& noteOutput, const std::vector<float>& onsetOutput) const;
    void inferOnsets_(std::vector<float>& onsets, const std::vector<float>& frames, int nFrames) const;
    std::vector<NoteEventCandidate> outputToNotesPolyphonic_(const std::vector<float>& frames, const std::vector<float>& onsets, int nFrames) const;
    std::set<int> scoreAiCandidates_(const std::vector<NoteEventCandidate>& candidates, const std::set<int>& expectedMidi) const;
    std::set<int> applyLowestExpectedBassRescue_(const std::vector<float>& audio, const std::set<int>& expectedMidi, const std::set<int>& selectedMidi) const;
    void buildLatestPacket_(double currentTime, const std::set<int>& currentActiveNotes);
    void readRecentWindow_(uint64_t endFrameExclusive, std::vector<float>& destination, int windowSize) const;
    void readRange_(uint64_t startFrame, std::vector<float>& destination, int count) const;
    void readAbsoluteRange_(uint64_t startFrame, float* destination, int count) const;
    double GetCurrentAudioTime() const;
    void stopLocked_();
    void resetStateLocked_();
    void resetResamplerState_();
    bool prepareFilteredResamplerLocked_(std::wstring& warning, int inputBlockFramesCapacity = kHopSize);
    uint64_t writeLinearResampledFrames_(const float* input, int frameCount, uint64_t startFrame);
    uint64_t writeFilteredResampledFrames_(const float* input, int frameCount, uint64_t startFrame);
    void updateStatusLocked_();
    void setError_(const std::wstring& error);
    bool isKnownDeviceIndex_(int index) const;
    const NativeDeviceDescriptor* findDevice_(int index) const;
    DetectorSettings getSettingsSnapshot_() const;

    mutable std::mutex controlMutex_;
    mutable std::mutex stateMutex_;
    mutable std::mutex dataMutex_;
    mutable std::mutex deepMutex_;
    mutable std::mutex resultMutex_;
    mutable std::mutex captureMutex_;
    std::condition_variable dataCondition_;
    std::condition_variable deepCondition_;

    PortAudioRuntime portAudio_;
    OrtRuntime ortRuntime_;
    HintState hintState_;

    bool initialized_ = false;
    std::wstring pluginDirectory_;
    std::wstring modelPath_;
    std::vector<NativeDeviceDescriptor> inputDevices_;
    int preferredDeviceIndex_ = -1;
    int selectedDeviceIndex_ = -1;
    std::string selectedDeviceDisplayName_;
    std::string selectedHostApiName_;
    int selectedInputChannelMode_ = kDetectorInputChannelInput1;
    int activeInputChannelCount_ = 1;
    int activeInputSampleRate_ = kSampleRate;
    std::atomic<bool> sharedInputMode_{ false };
    std::atomic<int> activeSharedInputCallbacks_{ 0 };
    double resampleSourceCursor_ = 0.0;
    DetectorResamplerMode configuredResamplerMode_ = DetectorResamplerMode::Filtered;
    DetectorResamplerMode activeResamplerMode_ = DetectorResamplerMode::Filtered;
    SRC_STATE* filteredResamplerState_ = nullptr;
    std::vector<float> filteredResamplerOutputScratch_;
    std::vector<float> filteredResamplerSilentInputScratch_;
    std::vector<float> inputMonoScratch_;

    PaStream* stream_ = nullptr;
    std::atomic<bool> running_{ false };
    std::thread fastThread_;
    std::thread deepThread_;
    aubio_onset_t* aubioOnset_ = nullptr;
    aubio_pitch_t* aubioPitch_ = nullptr;
    fvec_t* aubioHopInput_ = nullptr;
    fvec_t* aubioOnsetOutput_ = nullptr;
    fvec_t* aubioPitchOutput_ = nullptr;

    std::vector<float> ringBuffer_;
    std::atomic<uint64_t> totalFramesWritten_{ 0 };
    std::atomic<float> smoothedInputLevel_{ 0.0f };

    std::deque<CaptureTask> captures_;
    std::deque<FastChordTask> fastChordTasks_;
    std::deque<FastSingleTask> fastSingleTasks_;
    std::queue<std::pair<CaptureTask, std::vector<float>>> deepTasks_;
    std::queue<DeepResult> deepResults_;
    std::deque<NativeVerifierVerdict> verifierVerdicts_;
    std::set<int> verifierResolvedNoteIds_;

    std::string latestPacket_;
    std::string statusLine_;
    std::wstring lastError_;
    DetectorSettings settings_ = MakeTightDetectorSettings();

    int broadcastEventId_ = 0;
    double broadcastEventOnsetTime_ = 0.0;
    double broadcastEventUntil_ = 0.0;
    std::set<int> broadcastEventNotes_;
    std::string broadcastEventSource_;
    std::set<int> broadcastExpectedNotes_;
    std::set<int> fastChordActiveNotes_;
    double fastChordActiveUntil_ = 0.0;
    bool verifierEnabled_ = true;
    double verifierLastScoreTime_ = -999.0;
    double verifierLastUnitySongTime_ = -999.0;
};

NativeDetectorEngine::NativeDetectorEngine()
    : ringBuffer_(kRingBufferSamples, 0.0f)
{
    statusLine_ = "Native detector idle.";
    latestPacket_ = "--";
}

NativeDetectorEngine::~NativeDetectorEngine()
{
    Shutdown();
}

bool NativeDetectorEngine::Initialize(const std::wstring& modelPath, const std::wstring& dataDirectory, std::wstring& error)
{
    std::lock_guard<std::mutex> lock(controlMutex_);
    if (initialized_)
    {
        error.clear();
        return true;
    }

    pluginDirectory_ = GetCurrentModuleDirectory();
    modelPath_ = modelPath;
    if (pluginDirectory_.empty())
    {
        error = L"Failed to resolve the native detector plugin directory.";
        setError_(error);
        return false;
    }

    if (!std::filesystem::exists(modelPath_))
    {
        error = L"Basic Pitch ONNX model is missing from StreamingAssets.";
        setError_(error);
        return false;
    }

    if (!portAudio_.Initialize(pluginDirectory_, error))
    {
        setError_(error);
        return false;
    }

    inputDevices_ = portAudio_.EnumerateInputDevices();
    preferredDeviceIndex_ = portAudio_.GetPreferredInputDeviceIndex(inputDevices_);
    if (!ortRuntime_.Initialize(pluginDirectory_, modelPath_, error))
    {
        setError_(error);
        return false;
    }

    if (!initializeAubio_(error))
    {
        setError_(error);
        return false;
    }

    initialized_ = true;
    updateStatusLocked_();
    error.clear();
    return true;
}

bool NativeDetectorEngine::initializeAubio_(std::wstring& error)
{
    shutdownAubio_();

    aubioHopInput_ = new_fvec(static_cast<uint_t>(kHopSize));
    aubioOnsetOutput_ = new_fvec(1);
    aubioPitchOutput_ = new_fvec(1);
    aubioOnset_ = new_aubio_onset(const_cast<char_t*>("hfc"), static_cast<uint_t>(kOnsetWindowSize), static_cast<uint_t>(kHopSize), static_cast<uint_t>(kSampleRate));
    aubioPitch_ = new_aubio_pitch(const_cast<char_t*>("yinfast"), static_cast<uint_t>(kPitchWindowSize), static_cast<uint_t>(kHopSize), static_cast<uint_t>(kSampleRate));

    if (aubioHopInput_ == nullptr || aubioOnsetOutput_ == nullptr || aubioPitchOutput_ == nullptr || aubioOnset_ == nullptr || aubioPitch_ == nullptr)
    {
        shutdownAubio_();
        error = L"Failed to initialize aubio onset/pitch objects.";
        return false;
    }

    aubio_onset_set_threshold(aubioOnset_, 0.20f);
    aubio_pitch_set_unit(aubioPitch_, const_cast<char_t*>("midi"));
    aubio_pitch_set_tolerance(aubioPitch_, 0.82f);

    error.clear();
    return true;
}

void NativeDetectorEngine::shutdownAubio_()
{
    if (aubioPitch_ != nullptr)
        del_aubio_pitch(aubioPitch_);
    if (aubioOnset_ != nullptr)
        del_aubio_onset(aubioOnset_);
    if (aubioPitchOutput_ != nullptr)
        del_fvec(aubioPitchOutput_);
    if (aubioOnsetOutput_ != nullptr)
        del_fvec(aubioOnsetOutput_);
    if (aubioHopInput_ != nullptr)
        del_fvec(aubioHopInput_);

    aubioPitch_ = nullptr;
    aubioOnset_ = nullptr;
    aubioPitchOutput_ = nullptr;
    aubioOnsetOutput_ = nullptr;
    aubioHopInput_ = nullptr;
}

bool NativeDetectorEngine::Start(int inputDeviceIndex, int inputChannelMode, std::wstring& error)
{
    std::lock_guard<std::mutex> lock(controlMutex_);
    if (!initialized_)
    {
        error = L"Native detector is not initialized.";
        setError_(error);
        return false;
    }

    stopLocked_();
    resetStateLocked_();

    sharedInputMode_.store(false, std::memory_order_release);

    if (inputDevices_.empty())
        inputDevices_ = portAudio_.EnumerateInputDevices();

    int resolvedDeviceIndex = inputDeviceIndex;
    if (resolvedDeviceIndex < 0)
        resolvedDeviceIndex = preferredDeviceIndex_;
    if (!isKnownDeviceIndex_(resolvedDeviceIndex))
        resolvedDeviceIndex = preferredDeviceIndex_;
    if (!isKnownDeviceIndex_(resolvedDeviceIndex) && !inputDevices_.empty())
        resolvedDeviceIndex = inputDevices_.front().index;

    selectedDeviceIndex_ = resolvedDeviceIndex;
    const NativeDeviceDescriptor* selected = findDevice_(selectedDeviceIndex_);
    selectedDeviceDisplayName_ = selected != nullptr ? selected->displayName : "Default input";
    selectedHostApiName_ = selected != nullptr ? selected->hostApiName : std::string();
    selectedInputChannelMode_ = NormalizeDetectorInputChannelMode(inputChannelMode);
    activeInputChannelCount_ = RequiredDetectorInputChannels(
        selectedInputChannelMode_,
        selected != nullptr ? selected->maxInputChannels : 1);
    inputMonoScratch_.assign(static_cast<size_t>(kMaxAudioCallbackFrames), 0.0f);

    running_.store(true, std::memory_order_release);
    fastThread_ = std::thread(&NativeDetectorEngine::FastLoop_, this);
    deepThread_ = std::thread(&NativeDetectorEngine::DeepLoop_, this);

    const double suggestedLatency = selected != nullptr && selected->defaultLowInputLatency > 0.0
        ? selected->defaultLowInputLatency
        : 0.008;

    std::vector<int> sampleRateCandidates = BuildInputSampleRateCandidates(selected);
    std::vector<std::wstring> sampleRateErrors;
    for (const int sampleRateCandidate : sampleRateCandidates)
    {
        std::wstring attemptError;
        if (portAudio_.OpenInputStream(
            selectedDeviceIndex_,
            sampleRateCandidate,
            activeInputChannelCount_,
            static_cast<unsigned long>(kHopSize),
            suggestedLatency,
            &NativeDetectorEngine::PortAudioCallback_,
            this,
            stream_,
            attemptError))
        {
            activeInputSampleRate_ = sampleRateCandidate;
            resampleSourceCursor_ = 0.0;
            error.clear();
            break;
        }

        std::wostringstream attemptBuilder;
        attemptBuilder << sampleRateCandidate << L" Hz: " << attemptError;
        sampleRateErrors.push_back(attemptBuilder.str());
    }

    if (stream_ == nullptr)
    {
        running_.store(false, std::memory_order_release);
        dataCondition_.notify_all();
        deepCondition_.notify_all();
        if (fastThread_.joinable())
            fastThread_.join();
        if (deepThread_.joinable())
            deepThread_.join();
        if (!sampleRateErrors.empty())
        {
            std::wostringstream errorBuilder;
            errorBuilder << L"Unable to open detector input stream. ";
            for (size_t i = 0; i < sampleRateErrors.size(); ++i)
            {
                if (i > 0)
                    errorBuilder << L" | ";
                errorBuilder << sampleRateErrors[i];
            }
            error = errorBuilder.str();
        }
        setError_(error);
        return false;
    }

    std::wstring resamplerWarning;
    activeResamplerMode_ = configuredResamplerMode_;
    if (activeInputSampleRate_ != kSampleRate && configuredResamplerMode_ == DetectorResamplerMode::Filtered)
    {
        if (!prepareFilteredResamplerLocked_(resamplerWarning, kMaxAudioCallbackFrames))
            activeResamplerMode_ = DetectorResamplerMode::Linear;
    }

    updateStatusLocked_();
    setError_(resamplerWarning);
    error.clear();
    return true;
}

bool NativeDetectorEngine::StartSharedInput(
    int inputDeviceIndex,
    int sampleRate,
    int inputChannelCount,
    int inputChannelMode,
    int maxBlockFrames,
    const std::string& sourceLabel,
    const std::string& hostApiName,
    std::wstring& error)
{
    std::lock_guard<std::mutex> lock(controlMutex_);
    if (!initialized_)
    {
        error = L"Native detector is not initialized.";
        setError_(error);
        return false;
    }

    const int normalizedSampleRate = NormalizeInputSampleRate(static_cast<double>(sampleRate));
    if (normalizedSampleRate <= 0)
    {
        error = L"Shared detector input sample rate is invalid.";
        setError_(error);
        return false;
    }

    stopLocked_();
    resetStateLocked_();

    selectedDeviceIndex_ = inputDeviceIndex;
    selectedDeviceDisplayName_ = !sourceLabel.empty() ? sourceLabel : "Tone Lab shared input";
    selectedHostApiName_ = !hostApiName.empty() ? hostApiName : "Shared Tone Lab capture";
    selectedInputChannelMode_ = NormalizeDetectorInputChannelMode(inputChannelMode);
    activeInputChannelCount_ = std::clamp(inputChannelCount, 1, 64);
    activeInputSampleRate_ = normalizedSampleRate;
    sharedInputMode_.store(true, std::memory_order_release);

    const int safeMaxBlockFrames = std::clamp(maxBlockFrames, kHopSize, 65536);
    inputMonoScratch_.assign(static_cast<size_t>(safeMaxBlockFrames), 0.0f);

    std::wstring resamplerWarning;
    activeResamplerMode_ = configuredResamplerMode_;
    if (activeInputSampleRate_ != kSampleRate && configuredResamplerMode_ == DetectorResamplerMode::Filtered)
    {
        if (!prepareFilteredResamplerLocked_(resamplerWarning, safeMaxBlockFrames))
            activeResamplerMode_ = DetectorResamplerMode::Linear;
    }

    running_.store(true, std::memory_order_release);
    fastThread_ = std::thread(&NativeDetectorEngine::FastLoop_, this);
    deepThread_ = std::thread(&NativeDetectorEngine::DeepLoop_, this);

    updateStatusLocked_();
    setError_(resamplerWarning);
    error.clear();
    return true;
}

bool NativeDetectorEngine::SubmitSharedInput(const float* input, int frameCount, int inputChannelCount, int sampleRate, int inputChannelMode)
{
    if (frameCount <= 0)
        return true;

    if (!running_.load(std::memory_order_acquire) || !sharedInputMode_.load(std::memory_order_acquire))
        return false;

    activeSharedInputCallbacks_.fetch_add(1, std::memory_order_acq_rel);
    struct SharedInputCallbackGuard
    {
        std::atomic<int>& counter;
        ~SharedInputCallbackGuard()
        {
            counter.fetch_sub(1, std::memory_order_acq_rel);
        }
    } callbackGuard{ activeSharedInputCallbacks_ };

    if (!running_.load(std::memory_order_acquire) || !sharedInputMode_.load(std::memory_order_acquire))
        return false;

    const int normalizedSampleRate = NormalizeInputSampleRate(static_cast<double>(sampleRate));
    const int normalizedChannelMode = NormalizeDetectorInputChannelMode(inputChannelMode);
    const int safeInputChannels = std::clamp(inputChannelCount, 1, 64);
    if (normalizedSampleRate != activeInputSampleRate_ ||
        safeInputChannels != activeInputChannelCount_ ||
        normalizedChannelMode != selectedInputChannelMode_)
    {
        return false;
    }

    return onAudio_(input, frameCount) == 0;
}

void NativeDetectorEngine::Stop()
{
    std::lock_guard<std::mutex> lock(controlMutex_);
    stopLocked_();
}

void NativeDetectorEngine::Shutdown()
{
    std::lock_guard<std::mutex> lock(controlMutex_);
    stopLocked_();
    ortRuntime_.Shutdown();
    portAudio_.Shutdown();
    shutdownAubio_();
    initialized_ = false;
}

void NativeDetectorEngine::SetHintPayload(const std::string& payload)
{
    const double currentTime = GetCurrentAudioTime();
    const std::string verifierToken = "|VERIFIER=";
    const size_t verifierTokenIndex = payload.find(verifierToken);
    if (verifierTokenIndex != std::string::npos)
    {
        const size_t valueIndex = verifierTokenIndex + verifierToken.size();
        const bool enabled = valueIndex < payload.size() && payload[valueIndex] != '0';
        std::lock_guard<std::mutex> lock(stateMutex_);
        if (verifierEnabled_ != enabled)
        {
            verifierEnabled_ = enabled;
            resetVerifierStateLocked_();
        }
    }

    double unitySongTime = -1.0;
    const size_t firstPipe = payload.find('|');
    if (firstPipe != std::string::npos)
    {
        const size_t secondPipe = payload.find('|', firstPipe + 1);
        const std::string timeToken = payload.substr(firstPipe + 1, secondPipe == std::string::npos ? std::string::npos : secondPipe - firstPipe - 1);
        try
        {
            unitySongTime = std::stod(timeToken);
        }
        catch (...)
        {
            unitySongTime = -1.0;
        }
    }

    if (unitySongTime >= 0.0)
    {
        std::lock_guard<std::mutex> lock(stateMutex_);
        if (verifierLastUnitySongTime_ >= 0.0 &&
            (unitySongTime + kVerifierSeekResetThresholdSeconds < verifierLastUnitySongTime_ ||
                std::abs(unitySongTime - verifierLastUnitySongTime_) > kHintRetentionSeconds + kVerifierSeekResetThresholdSeconds))
        {
            resetVerifierStateLocked_();
        }

        verifierLastUnitySongTime_ = unitySongTime;
    }

    hintState_.ParsePayload(payload, currentTime);
}

std::string NativeDetectorEngine::PollLatestPacket() const
{
    std::lock_guard<std::mutex> lock(stateMutex_);
    return latestPacket_;
}

std::string NativeDetectorEngine::PollVerifierVerdictsJson()
{
    std::deque<NativeVerifierVerdict> verdicts;
    {
        std::lock_guard<std::mutex> lock(stateMutex_);
        verdicts.swap(verifierVerdicts_);
    }

    std::ostringstream builder;
    builder << "{\"verdicts\":[";
    for (size_t i = 0; i < verdicts.size(); ++i)
    {
        const NativeVerifierVerdict& verdict = verdicts[i];
        if (i > 0)
            builder << ',';

        builder << "{\"noteId\":" << verdict.noteId
            << ",\"chordId\":" << verdict.chordId
            << ",\"midi\":" << verdict.midi
            << ",\"hit\":" << (verdict.hit ? "true" : "false")
            << ",\"noteTime\":" << verdict.noteTime
            << ",\"detectedSongTime\":" << verdict.detectedSongTime
            << ",\"confidence\":" << verdict.confidence
            << ",\"centsError\":" << verdict.centsError
            << ",\"source\":\"" << JsonEscape(verdict.source)
            << "\"}";
    }
    builder << "]}";
    return builder.str();
}

std::string NativeDetectorEngine::GetStatusLine() const
{
    std::lock_guard<std::mutex> lock(stateMutex_);
    return statusLine_;
}

std::wstring NativeDetectorEngine::GetLastError() const
{
    std::lock_guard<std::mutex> lock(stateMutex_);
    return lastError_;
}

bool NativeDetectorEngine::IsRunning() const
{
    return running_.load(std::memory_order_acquire);
}

std::string NativeDetectorEngine::ListInputDevicesJson() const
{
    std::lock_guard<std::mutex> lock(controlMutex_);
    std::ostringstream builder;
    builder << "{\"preferredDeviceIndex\":" << preferredDeviceIndex_ << ",\"devices\":[";
    for (size_t i = 0; i < inputDevices_.size(); ++i)
    {
        const NativeDeviceDescriptor& device = inputDevices_[i];
        if (i > 0)
            builder << ',';
        builder << "{\"index\":" << device.index
            << ",\"displayName\":\"" << JsonEscape(device.displayName)
            << "\",\"name\":\"" << JsonEscape(device.name)
            << "\",\"hostApiName\":\"" << JsonEscape(device.hostApiName)
            << "\",\"maxInputChannels\":" << device.maxInputChannels
            << ",\"defaultSampleRate\":" << device.defaultSampleRate
            << ",\"defaultLowInputLatency\":" << device.defaultLowInputLatency
            << "}";
    }
    builder << "]}";
    return builder.str();
}

DetectorSettings NativeDetectorEngine::getSettingsSnapshot_() const
{
    std::lock_guard<std::mutex> lock(stateMutex_);
    return settings_;
}

std::string NativeDetectorEngine::GetRuntimeInfoJson() const
{
    std::lock_guard<std::mutex> lock(stateMutex_);
    const bool running = running_.load(std::memory_order_acquire);
    std::ostringstream builder;
    builder << "{"
        << "\"running\":" << (running ? "true" : "false")
        << ",\"backendLabel\":\"" << (sharedInputMode_.load(std::memory_order_acquire) ? "Shared Tone Lab Capture" : "Native C++ Detector") << "\""
        << ",\"selectedInputDeviceIndex\":" << selectedDeviceIndex_
        << ",\"selectedInputDeviceDisplayName\":\"" << JsonEscape(selectedDeviceDisplayName_)
        << "\",\"selectedHostApiName\":\"" << JsonEscape(selectedHostApiName_)
        << "\",\"inputChannelMode\":\"" << GetDetectorInputChannelModeLabel(selectedInputChannelMode_)
        << "\",\"sampleRate\":" << kSampleRate
        << ",\"captureSampleRate\":" << (running ? activeInputSampleRate_ : 0)
        << ",\"internalSampleRate\":" << kSampleRate
        << ",\"configuredResamplerMode\":\"" << GetDetectorResamplerModeLabel(configuredResamplerMode_)
        << "\",\"activeResamplerMode\":\"" << GetActiveDetectorResamplerModeLabel(activeResamplerMode_, running ? activeInputSampleRate_ : kSampleRate)
        << "\",\"resamplerToggleAvailable\":" << ((running && activeInputSampleRate_ != kSampleRate) ? "true" : "false")
        << ",\"hopSize\":" << kHopSize
        << ",\"captureSeconds\":0.3"
        << ",\"inputLevelNormalized\":" << smoothedInputLevel_.load(std::memory_order_relaxed)
        << ",\"latestPacket\":\"" << JsonEscape(latestPacket_)
        << "\",\"statusText\":\"" << JsonEscape(statusLine_)
        << "\",\"errorText\":\"" << JsonEscape(WideToUtf8(lastError_))
        << "\"}";
    return builder.str();
}

void NativeDetectorEngine::ApplySettingsJson(const std::string& settingsJson)
{
    std::lock_guard<std::mutex> lock(stateMutex_);
    settings_ = ParseDetectorSettingsJson(settingsJson, settings_);
}

void NativeDetectorEngine::SetResamplerMode(int mode)
{
    std::lock_guard<std::mutex> lock(controlMutex_);
    configuredResamplerMode_ = NormalizeDetectorResamplerMode(mode);
    if (!running_.load(std::memory_order_acquire))
        activeResamplerMode_ = configuredResamplerMode_;
    updateStatusLocked_();
}

int __cdecl NativeDetectorEngine::PortAudioCallback_(const void* input, void* output, unsigned long frameCount, const PaStreamCallbackTimeInfo*, PaStreamCallbackFlags, void* userData)
{
    if (output != nullptr)
        memset(output, 0, static_cast<size_t>(frameCount) * sizeof(float));

    NativeDetectorEngine* self = static_cast<NativeDetectorEngine*>(userData);
    return self != nullptr ? self->onAudio_(static_cast<const float*>(input), static_cast<int>(frameCount)) : 0;
}

int NativeDetectorEngine::onAudio_(const float* input, int frameCount)
{
    if (!running_.load(std::memory_order_acquire) || frameCount <= 0)
        return 0;

    const float* monoInput = input;
    if (activeInputChannelCount_ > 1)
    {
        const int scratchFrames = static_cast<int>(std::min<size_t>(
            inputMonoScratch_.size(),
            static_cast<size_t>(std::numeric_limits<int>::max())));
        if (scratchFrames <= 0)
            return 0;
        if (frameCount > scratchFrames)
            frameCount = scratchFrames;

        float* scratch = inputMonoScratch_.data();
        for (int i = 0; i < frameCount; ++i)
            scratch[i] = SelectDetectorMonoSample(input, i, activeInputChannelCount_, selectedInputChannelMode_);
        monoInput = scratch;
    }

    const uint64_t startFrame = totalFramesWritten_.load(std::memory_order_relaxed);
    uint64_t framesWritten = 0;
    if (activeInputSampleRate_ == kSampleRate)
    {
        for (int i = 0; i < frameCount; ++i)
        {
            const float sample = monoInput != nullptr ? monoInput[i] : 0.0f;
            ringBuffer_[static_cast<size_t>((startFrame + static_cast<uint64_t>(i)) % ringBuffer_.size())] = sample;
        }

        framesWritten = static_cast<uint64_t>(frameCount);
    }
    else if (activeResamplerMode_ == DetectorResamplerMode::Linear)
    {
        framesWritten = writeLinearResampledFrames_(monoInput, frameCount, startFrame);
    }
    else
    {
        framesWritten = writeFilteredResampledFrames_(monoInput, frameCount, startFrame);
    }

    const float rms = monoInput != nullptr ? ComputeRms(monoInput, frameCount) : 0.0f;
    const float previousLevel = smoothedInputLevel_.load(std::memory_order_relaxed);
    const float smoothed = std::clamp(previousLevel * 0.85f + rms * 7.5f * 0.15f, 0.0f, 1.0f);
    smoothedInputLevel_.store(smoothed, std::memory_order_relaxed);

    totalFramesWritten_.store(startFrame + framesWritten, std::memory_order_release);
    dataCondition_.notify_one();
    return 0;
}

void NativeDetectorEngine::readRecentWindow_(uint64_t endFrameExclusive, std::vector<float>& destination, int windowSize) const
{
    std::fill(destination.begin(), destination.end(), 0.0f);
    const int available = static_cast<int>(std::min<uint64_t>(endFrameExclusive, static_cast<uint64_t>(windowSize)));
    if (available <= 0)
        return;

    const uint64_t startFrame = endFrameExclusive - static_cast<uint64_t>(available);
    readAbsoluteRange_(startFrame, destination.data() + (windowSize - available), available);
}

void NativeDetectorEngine::readRange_(uint64_t startFrame, std::vector<float>& destination, int count) const
{
    if (static_cast<int>(destination.size()) < count)
        destination.resize(static_cast<size_t>(count));
    readAbsoluteRange_(startFrame, destination.data(), count);
}

void NativeDetectorEngine::readAbsoluteRange_(uint64_t startFrame, float* destination, int count) const
{
    const uint64_t available = totalFramesWritten_.load(std::memory_order_acquire);
    const uint64_t oldestFrame = available > static_cast<uint64_t>(ringBuffer_.size())
        ? available - static_cast<uint64_t>(ringBuffer_.size())
        : 0;

    for (int i = 0; i < count; ++i)
    {
        const uint64_t frame = startFrame + static_cast<uint64_t>(i);
        if (frame < oldestFrame || frame >= available)
            destination[i] = 0.0f;
        else
            destination[i] = ringBuffer_[static_cast<size_t>(frame % ringBuffer_.size())];
    }
}

double NativeDetectorEngine::GetCurrentAudioTime() const
{
    return static_cast<double>(totalFramesWritten_.load(std::memory_order_acquire)) / static_cast<double>(kSampleRate);
}

void NativeDetectorEngine::stopLocked_()
{
    const bool wasSharedInput = sharedInputMode_.load(std::memory_order_acquire);
    running_.store(false, std::memory_order_release);
    dataCondition_.notify_all();
    deepCondition_.notify_all();

    if (wasSharedInput)
    {
        for (int spin = 0; activeSharedInputCallbacks_.load(std::memory_order_acquire) > 0; ++spin)
        {
            if (spin < 32)
                std::this_thread::yield();
            else
                std::this_thread::sleep_for(std::chrono::microseconds(100));
        }
    }

    portAudio_.CloseStream(stream_);

    if (fastThread_.joinable())
        fastThread_.join();
    if (deepThread_.joinable())
        deepThread_.join();

    {
        std::lock_guard<std::mutex> lock(deepMutex_);
        while (!deepTasks_.empty())
            deepTasks_.pop();
    }

    {
        std::lock_guard<std::mutex> lock(resultMutex_);
        while (!deepResults_.empty())
            deepResults_.pop();
    }

    {
        std::lock_guard<std::mutex> lock(captureMutex_);
        captures_.clear();
        fastChordTasks_.clear();
        fastSingleTasks_.clear();
    }

    resetResamplerState_();
    sharedInputMode_.store(false, std::memory_order_release);
    updateStatusLocked_();
}

void NativeDetectorEngine::resetStateLocked_()
{
    std::fill(ringBuffer_.begin(), ringBuffer_.end(), 0.0f);
    totalFramesWritten_.store(0, std::memory_order_release);
    smoothedInputLevel_.store(0.0f, std::memory_order_relaxed);
    activeInputSampleRate_ = kSampleRate;
    activeInputChannelCount_ = 1;
    sharedInputMode_.store(false, std::memory_order_release);
    resampleSourceCursor_ = 0.0;
    activeResamplerMode_ = configuredResamplerMode_;
    resetResamplerState_();
    {
        std::lock_guard<std::mutex> lock(captureMutex_);
        captures_.clear();
        fastChordTasks_.clear();
        fastSingleTasks_.clear();
    }
    {
        std::lock_guard<std::mutex> lock(stateMutex_);
        latestPacket_ = "--";
        broadcastEventNotes_.clear();
        broadcastEventSource_.clear();
        broadcastEventId_ = 0;
        broadcastEventOnsetTime_ = 0.0;
        broadcastEventUntil_ = 0.0;
        broadcastExpectedNotes_.clear();
        fastChordActiveNotes_.clear();
        fastChordActiveUntil_ = 0.0;
        resetVerifierStateLocked_();
    }
}

void NativeDetectorEngine::resetVerifierStateLocked_()
{
    verifierVerdicts_.clear();
    verifierResolvedNoteIds_.clear();
    verifierLastScoreTime_ = -999.0;
    verifierLastUnitySongTime_ = -999.0;
}

std::string EscapeJsonString(const std::string& value)
{
    std::ostringstream builder;
    for (char ch : value)
    {
        switch (ch)
        {
        case '\\':
            builder << "\\\\";
            break;
        case '"':
            builder << "\\\"";
            break;
        case '\r':
            builder << "\\r";
            break;
        case '\n':
            builder << "\\n";
            break;
        case '\t':
            builder << "\\t";
            break;
        default:
            builder << ch;
            break;
        }
    }

    return builder.str();
}

int NormalizeInputSampleRate(double sampleRate)
{
    if (!std::isfinite(sampleRate))
        return -1;

    const int rounded = static_cast<int>(std::lround(sampleRate));
    return rounded >= 8000 && rounded <= 192000 ? rounded : -1;
}

std::vector<int> BuildInputSampleRateCandidates(const NativeDeviceDescriptor* selectedDevice)
{
    std::vector<int> candidates;
    candidates.reserve(4);
    candidates.push_back(kSampleRate);

    if (selectedDevice != nullptr)
    {
        const int defaultRate = NormalizeInputSampleRate(selectedDevice->defaultSampleRate);
        if (defaultRate > 0 && std::find(candidates.begin(), candidates.end(), defaultRate) == candidates.end())
            candidates.push_back(defaultRate);
    }

    for (const int fallbackRate : { 48000, 44100 })
    {
        if (std::find(candidates.begin(), candidates.end(), fallbackRate) == candidates.end())
            candidates.push_back(fallbackRate);
    }

    return candidates;
}

void NativeDetectorEngine::resetResamplerState_()
{
    if (filteredResamplerState_ != nullptr)
        filteredResamplerState_ = src_delete(filteredResamplerState_);

    filteredResamplerOutputScratch_.clear();
    filteredResamplerSilentInputScratch_.clear();
}

bool NativeDetectorEngine::prepareFilteredResamplerLocked_(std::wstring& warning, int inputBlockFramesCapacity)
{
    warning.clear();
    resetResamplerState_();

    if (activeInputSampleRate_ == kSampleRate)
        return true;

    int errorCode = 0;
    filteredResamplerState_ = src_new(SRC_SINC_BEST_QUALITY, 1, &errorCode);
    if (filteredResamplerState_ == nullptr)
    {
        warning = L"Filtered resampler unavailable";
        const char* errorText = src_strerror(errorCode);
        if (errorText != nullptr && *errorText != '\0')
        {
            warning += L" (";
            warning += Utf8ToWide(errorText);
            warning += L")";
        }
        warning += L"; using linear.";
        return false;
    }

    src_reset(filteredResamplerState_);

    const double ratio = static_cast<double>(kSampleRate) / static_cast<double>(std::max(1, activeInputSampleRate_));
    const int safeInputBlockFramesCapacity = std::max(kHopSize, inputBlockFramesCapacity);
    const size_t outputCapacity = static_cast<size_t>(std::ceil(static_cast<double>(safeInputBlockFramesCapacity) * std::max(1.0, ratio))) + 512u;
    filteredResamplerOutputScratch_.assign(std::max<size_t>(outputCapacity, static_cast<size_t>(kHopSize)), 0.0f);
    filteredResamplerSilentInputScratch_.assign(static_cast<size_t>(safeInputBlockFramesCapacity), 0.0f);
    return true;
}

uint64_t NativeDetectorEngine::writeLinearResampledFrames_(const float* input, int frameCount, uint64_t startFrame)
{
    uint64_t framesWritten = 0;
    const double sourceStep = static_cast<double>(activeInputSampleRate_) / static_cast<double>(kSampleRate);
    double sourceCursor = resampleSourceCursor_;
    while (sourceCursor < static_cast<double>(frameCount))
    {
        const int sampleIndex = static_cast<int>(std::floor(sourceCursor));
        const int nextSampleIndex = std::min(sampleIndex + 1, frameCount - 1);
        const float sampleA = input != nullptr ? input[sampleIndex] : 0.0f;
        const float sampleB = input != nullptr ? input[nextSampleIndex] : sampleA;
        const float fraction = static_cast<float>(sourceCursor - static_cast<double>(sampleIndex));
        const float sample = sampleA + ((sampleB - sampleA) * fraction);
        ringBuffer_[static_cast<size_t>((startFrame + framesWritten) % ringBuffer_.size())] = sample;
        ++framesWritten;
        sourceCursor += sourceStep;
    }

    resampleSourceCursor_ = sourceCursor - static_cast<double>(frameCount);
    return framesWritten;
}

uint64_t NativeDetectorEngine::writeFilteredResampledFrames_(const float* input, int frameCount, uint64_t startFrame)
{
    if (filteredResamplerState_ == nullptr)
        return writeLinearResampledFrames_(input, frameCount, startFrame);

    const double ratio = static_cast<double>(kSampleRate) / static_cast<double>(std::max(1, activeInputSampleRate_));
    const size_t minimumOutputCapacity = static_cast<size_t>(std::ceil(static_cast<double>(frameCount) * std::max(1.0, ratio))) + 512u;
    if (filteredResamplerOutputScratch_.size() < minimumOutputCapacity)
        return writeLinearResampledFrames_(input, frameCount, startFrame);

    const float* inputSamples = input;
    if (inputSamples == nullptr)
    {
        if (filteredResamplerSilentInputScratch_.size() < static_cast<size_t>(frameCount))
            return writeLinearResampledFrames_(input, frameCount, startFrame);
        std::fill(
            filteredResamplerSilentInputScratch_.begin(),
            filteredResamplerSilentInputScratch_.begin() + frameCount,
            0.0f);
        inputSamples = filteredResamplerSilentInputScratch_.data();
    }

    SRC_DATA data{};
    data.data_in = inputSamples;
    data.input_frames = frameCount;
    data.data_out = filteredResamplerOutputScratch_.data();
    data.output_frames = static_cast<long>(filteredResamplerOutputScratch_.size());
    data.end_of_input = 0;
    data.src_ratio = ratio;

    const int errorCode = src_process(filteredResamplerState_, &data);
    if (errorCode != 0)
    {
        std::wstring message = L"Filtered resampler processing failed";
        const char* errorText = src_strerror(errorCode);
        if (errorText != nullptr && *errorText != '\0')
        {
            message += L" (";
            message += Utf8ToWide(errorText);
            message += L")";
        }
        message += L"; using linear.";
        setError_(message);
        return writeLinearResampledFrames_(input, frameCount, startFrame);
    }

    const uint64_t framesWritten = static_cast<uint64_t>(std::max<long>(0, data.output_frames_gen));
    for (uint64_t i = 0; i < framesWritten; ++i)
        ringBuffer_[static_cast<size_t>((startFrame + i) % ringBuffer_.size())] = filteredResamplerOutputScratch_[static_cast<size_t>(i)];

    resampleSourceCursor_ = 0.0;
    return framesWritten;
}

void NativeDetectorEngine::updateStatusLocked_()
{
    std::ostringstream builder;
    if (running_.load(std::memory_order_acquire))
    {
        builder << (sharedInputMode_.load(std::memory_order_acquire) ? "Running on shared Tone Lab input " : "Running on ")
            << (selectedDeviceDisplayName_.empty() ? "default input" : selectedDeviceDisplayName_)
            << "  •  " << GetDetectorInputChannelModeLabel(selectedInputChannelMode_)
            << "  •  " << kSampleRate << " Hz"
            << "  •  Hop " << kHopSize
            << "  •  HFC onset"
            << "  •  Adaptive high-string tuning";
        if (!selectedHostApiName_.empty())
            builder << "  •  " << selectedHostApiName_;
    }
    else
    {
        builder << "Native detector idle.";
    }

    std::lock_guard<std::mutex> stateLock(stateMutex_);
    statusLine_ = builder.str();
}

void NativeDetectorEngine::setError_(const std::wstring& error)
{
    std::lock_guard<std::mutex> lock(stateMutex_);
    lastError_ = error;
}

bool NativeDetectorEngine::isKnownDeviceIndex_(int index) const
{
    if (index < 0)
        return false;
    return std::any_of(inputDevices_.begin(), inputDevices_.end(), [&](const NativeDeviceDescriptor& device)
    {
        return device.index == index;
    });
}

const NativeDeviceDescriptor* NativeDetectorEngine::findDevice_(int index) const
{
    for (const NativeDeviceDescriptor& device : inputDevices_)
    {
        if (device.index == index)
            return &device;
    }
    return nullptr;
}

void NativeDetectorEngine::FastLoop_()
{
    std::vector<float> hop(kHopSize, 0.0f);
    std::deque<int> recentPitchMidi;
    std::set<int> currentActiveNotes;
    double lastContinuousTime = -999.0;
    int stableMidi = -1;
    int stableCount = 0;
    double lastOnsetTime = -999.0;
    int pluckCounter = 0;
    uint64_t processedFrames = 0;
    ExpectedHintNoteSpec lastProactiveScheduledNote;
    bool hasLastProactiveScheduledNote = false;
    double lastProactiveScheduledUntil = -999.0;
    int lastFastSingleScheduledMidi = -1;
    double lastFastSingleScheduledWindowStartPython = -999.0;
    double lastFastSingleScheduledWindowEndPython = -999.0;
    std::deque<double> verifierOnsetTimes;

    auto scheduleFastSingleTask = [&](int eventId, uint64_t onsetFrame, double onsetTime, const ExpectedHintNoteSpec& expectedNote, bool proactive, double windowStartPythonTime, double windowEndPythonTime)
    {
        if (eventId <= 0 || expectedNote.midi < 0)
            return;

        FastSingleTask fastTask;
        fastTask.eventId = eventId;
        fastTask.onsetFrame = onsetFrame;
        fastTask.analysisWindowSamples = GetFastSinglePrimaryAnalysisWindowSamples(expectedNote);
        fastTask.readyFrame = onsetFrame + static_cast<uint64_t>(fastTask.analysisWindowSamples);
        fastTask.onsetTime = onsetTime;
        fastTask.expectedNote = expectedNote;
        fastTask.attemptIndex = 0;
        fastTask.proactive = proactive;
        fastTask.windowStartPythonTime = windowStartPythonTime;
        fastTask.windowEndPythonTime = windowEndPythonTime;

        {
            std::lock_guard<std::mutex> captureLock(captureMutex_);
            fastSingleTasks_.push_back(std::move(fastTask));
        }

        lastFastSingleScheduledMidi = expectedNote.midi;
        lastFastSingleScheduledWindowStartPython = windowStartPythonTime;
        lastFastSingleScheduledWindowEndPython = windowEndPythonTime;

        std::ostringstream fastLog;
        fastLog << "FAST_SINGLE_SCHEDULE"
            << " eventId=" << eventId
            << " onsetTime=" << onsetTime
            << " midi=" << expectedNote.midi
            << " string=" << expectedNote.stringIndex
            << " fret=" << expectedNote.fret
            << " proactive=" << (proactive ? 1 : 0)
            << " onsetFrame=" << onsetFrame
            << " readyFrame=" << (onsetFrame + static_cast<uint64_t>(GetFastSinglePrimaryAnalysisWindowSamples(expectedNote)))
            << " windowSamples=" << GetFastSinglePrimaryAnalysisWindowSamples(expectedNote)
            << " hintStart=" << windowStartPythonTime
            << " hintEnd=" << windowEndPythonTime;
        AppendDebugLogLine(fastLog.str());
    };

    while (running_.load(std::memory_order_acquire))
    {
        uint64_t availableFrames = totalFramesWritten_.load(std::memory_order_acquire);
        if (availableFrames < processedFrames + static_cast<uint64_t>(kHopSize))
        {
            std::unique_lock<std::mutex> waitLock(dataMutex_);
            dataCondition_.wait_for(waitLock, std::chrono::milliseconds(10));
            continue;
        }

        while (running_.load(std::memory_order_acquire) && availableFrames >= processedFrames + static_cast<uint64_t>(kHopSize))
        {
            readRange_(processedFrames, hop, kHopSize);
            processedFrames += static_cast<uint64_t>(kHopSize);
            const double currentTime = static_cast<double>(processedFrames) / static_cast<double>(kSampleRate);

            pumpDeepResults_(currentTime);
            maybeDispatchCaptureTasks_(availableFrames);
            maybeDispatchFastChordTasks_(availableFrames, currentTime);
            updateContinuousNotes_(hop, currentTime, recentPitchMidi, stableMidi, stableCount, lastContinuousTime, currentActiveNotes);
            maybeDispatchFastSingleTasks_(availableFrames, currentTime, currentActiveNotes);

            const DetectorSettings settings = getSettingsSnapshot_();
            double onsetUnityTime = -1.0;
            const ExpectedHintContext expectedOnsetContext = hintState_.GetExpectedContextNearPythonTime(currentTime, settings.onsetExpectLookaheadSeconds, &onsetUnityTime);
            const bool onsetDetected = detectOnset_(hop, expectedOnsetContext.midiNotes);
            if (onsetDetected && (currentTime - lastOnsetTime) > kDebounceSeconds)
            {
                lastOnsetTime = currentTime;
                verifierOnsetTimes.push_back(currentTime);
                while (!verifierOnsetTimes.empty() && currentTime - verifierOnsetTimes.front() > kVerifierOnsetRetentionSeconds)
                    verifierOnsetTimes.pop_front();
                ++pluckCounter;

                if (expectedOnsetContext.expectedNotes.size() >= 2)
                {
                    int lowestExpectedMidi = std::numeric_limits<int>::max();
                    for (const ExpectedHintNoteSpec& spec : expectedOnsetContext.expectedNotes)
                    {
                        if (spec.midi >= 0)
                            lowestExpectedMidi = std::min(lowestExpectedMidi, spec.midi);
                    }

                    if (lowestExpectedMidi != std::numeric_limits<int>::max())
                    {
                        const int analysisWindowSamples = lowestExpectedMidi <= 40
                            ? kFastChordAnalysisWindowLongSamples
                            : kFastChordAnalysisWindowShortSamples;

                        FastChordTask fastTask;
                        fastTask.eventId = pluckCounter;
                        fastTask.onsetFrame = processedFrames - static_cast<uint64_t>(kHopSize);
                        fastTask.readyFrame = fastTask.onsetFrame + static_cast<uint64_t>(analysisWindowSamples);
                        fastTask.onsetTime = currentTime;
                        fastTask.expectedMidiNotes = expectedOnsetContext.midiNotes;
                        fastTask.expectedNotes = expectedOnsetContext.expectedNotes;

                        {
                            std::lock_guard<std::mutex> captureLock(captureMutex_);
                            fastChordTasks_.push_back(std::move(fastTask));
                        }

                        std::ostringstream fastLog;
                        fastLog << "FAST_CHORD_SCHEDULE"
                            << " eventId=" << pluckCounter
                            << " onsetTime=" << currentTime
                            << " expectedCount=" << expectedOnsetContext.expectedNotes.size()
                            << " onsetFrame=" << (processedFrames - static_cast<uint64_t>(kHopSize))
                            << " readyFrame=" << (processedFrames - static_cast<uint64_t>(kHopSize) + static_cast<uint64_t>(analysisWindowSamples))
                            << " windowSamples=" << analysisWindowSamples;
                        AppendDebugLogLine(fastLog.str());
                    }
                }
                else if (expectedOnsetContext.expectedNotes.size() == 1)
                {
                    const ExpectedHintNoteSpec& expectedNote = expectedOnsetContext.expectedNotes.front();
                    if (expectedNote.midi >= 0)
                    {
                        scheduleFastSingleTask(
                            pluckCounter,
                            processedFrames - static_cast<uint64_t>(kHopSize),
                            currentTime,
                            expectedNote,
                            false,
                            expectedOnsetContext.windowStartPythonTime,
                            expectedOnsetContext.windowEndPythonTime);
                    }
                }

                CaptureTask task;
                task.eventId = pluckCounter;
                task.startFrame = processedFrames - static_cast<uint64_t>(kHopSize);
                task.readyFrame = task.startFrame + static_cast<uint64_t>(kCaptureSamples);
                task.onsetTime = currentTime;

                {
                    std::lock_guard<std::mutex> captureLock(captureMutex_);
                    captures_.push_back(task);
                }
            }

            double activeUnityTime = -1.0;
            const ExpectedHintContext activeSingleContext = hintState_.GetExpectedContextForPythonTime(currentTime, &activeUnityTime);
            if (activeSingleContext.expectedNotes.size() == 1 &&
                activeSingleContext.hasWindow &&
                activeSingleContext.windowStartPythonTime > 0.0 &&
                activeSingleContext.windowEndPythonTime >= currentTime)
            {
                const ExpectedHintNoteSpec& expectedNote = activeSingleContext.expectedNotes.front();
                const bool sameProactiveNote =
                    hasLastProactiveScheduledNote &&
                    ExpectedHintNoteSpecsEqual(lastProactiveScheduledNote, expectedNote) &&
                    currentTime <= lastProactiveScheduledUntil;

                int bestActiveDistance = std::numeric_limits<int>::max();
                for (int activeMidi : currentActiveNotes)
                    bestActiveDistance = std::min(bestActiveDistance, SemitoneDistance(activeMidi, expectedNote.midi));

                const bool conflictingActivePitch =
                    !currentActiveNotes.empty() &&
                    bestActiveDistance > std::max(1, settings.expectMaxDistanceContinuous);

                if (!sameProactiveNote && !conflictingActivePitch)
                {
                    ++pluckCounter;
                    const uint64_t startFrame = static_cast<uint64_t>(std::max(0.0, std::floor(activeSingleContext.windowStartPythonTime * static_cast<double>(kSampleRate))));
                    scheduleFastSingleTask(
                        pluckCounter,
                        startFrame,
                        activeSingleContext.windowStartPythonTime,
                        expectedNote,
                        true,
                        activeSingleContext.windowStartPythonTime,
                        activeSingleContext.windowEndPythonTime);
                    lastProactiveScheduledNote = expectedNote;
                    hasLastProactiveScheduledNote = true;
                    lastProactiveScheduledUntil = activeSingleContext.windowEndPythonTime;
                }
            }
            else if (activeSingleContext.expectedNotes.empty() || !activeSingleContext.hasWindow)
            {
                hasLastProactiveScheduledNote = false;
                lastProactiveScheduledUntil = -999.0;
            }

            while (!verifierOnsetTimes.empty() && currentTime - verifierOnsetTimes.front() > kVerifierOnsetRetentionSeconds)
                verifierOnsetTimes.pop_front();
            maybeRunExpectedNoteVerifier_(availableFrames, currentTime, settings, verifierOnsetTimes);

            buildLatestPacket_(currentTime, currentActiveNotes);
            availableFrames = totalFramesWritten_.load(std::memory_order_acquire);
        }
    }
}

void NativeDetectorEngine::DeepLoop_()
{
    while (true)
    {
        CaptureTask task;
        std::vector<float> audio;

        {
            std::unique_lock<std::mutex> lock(deepMutex_);
            deepCondition_.wait(lock, [&] { return !running_.load(std::memory_order_acquire) || !deepTasks_.empty(); });
            if (!running_.load(std::memory_order_acquire) && deepTasks_.empty())
                break;

            task = std::move(deepTasks_.front().first);
            audio = std::move(deepTasks_.front().second);
            deepTasks_.pop();
        }

        std::vector<float> noteOutput;
        std::vector<float> onsetOutput;
        std::wstring ortError;
        if (!ortRuntime_.RunNoteAndOnsetInference(audio, noteOutput, onsetOutput, ortError))
        {
            setError_(ortError);
            continue;
        }

        std::vector<NoteEventCandidate> decodedCandidates = decodeBasicPitch_(noteOutput, onsetOutput);

        double unityTime = -1.0;
        const ExpectedHintContext expectedContext = hintState_.GetExpectedContextForPythonTime(task.onsetTime, &unityTime);
        const DetectorSettings settings = getSettingsSnapshot_();
        std::set<int> selectedMidi = scoreAiCandidates_(decodedCandidates, expectedContext.midiNotes);
        selectedMidi = applyLowestExpectedBassRescue_(audio, expectedContext.midiNotes, selectedMidi);

        if (expectedContext.expectedNotes.size() >= 2)
        {
            if (scoreExpectedChordConstraint_(audio, expectedContext.expectedNotes, settings, "deep"))
                selectedMidi = expectedContext.midiNotes;
            else
                selectedMidi.clear();
        }

        if (selectedMidi.empty())
            continue;

        DeepResult result;
        result.eventId = task.eventId;
        result.onsetTime = task.onsetTime;
        result.eventNotes = selectedMidi;
        result.expectedMidiNotes = expectedContext.midiNotes;
        result.sourceTag = "ai";

        {
            std::lock_guard<std::mutex> resultLock(resultMutex_);
            deepResults_.push(std::move(result));
        }
    }
}

void NativeDetectorEngine::pumpDeepResults_(double currentTime)
{
    std::queue<DeepResult> localResults;
    {
        std::lock_guard<std::mutex> lock(resultMutex_);
        std::swap(localResults, deepResults_);
    }

    while (!localResults.empty())
    {
        const DeepResult& result = localResults.front();
        if (result.eventNotes.empty())
        {
            localResults.pop();
            continue;
        }

        {
            std::lock_guard<std::mutex> lock(stateMutex_);
            const bool closeEnoughToMerge = broadcastEventId_ > 0 &&
                !broadcastEventNotes_.empty() &&
                !result.eventNotes.empty() &&
                std::abs(result.onsetTime - broadcastEventOnsetTime_) <= kChordResultMergeSeconds;
            const size_t expectedOverlap = CountSetOverlap(broadcastExpectedNotes_, result.expectedMidiNotes);
            const size_t minExpectedSize = std::min(broadcastExpectedNotes_.size(), result.expectedMidiNotes.size());
            const bool mergeableChordExpectation = minExpectedSize >= 2 &&
                expectedOverlap > 0 &&
                (expectedOverlap * 2) >= minExpectedSize;

            if (closeEnoughToMerge && mergeableChordExpectation)
            {
                broadcastEventOnsetTime_ = std::min(broadcastEventOnsetTime_, result.onsetTime);
                broadcastEventUntil_ = currentTime + kEventBroadcastSeconds;
                broadcastEventNotes_.insert(result.eventNotes.begin(), result.eventNotes.end());
                broadcastExpectedNotes_.insert(result.expectedMidiNotes.begin(), result.expectedMidiNotes.end());
                broadcastEventSource_ = MergeEventSourceTags(broadcastEventSource_, result.sourceTag);
            }
            else
            {
                broadcastEventId_ = result.eventId;
                broadcastEventOnsetTime_ = result.onsetTime;
                broadcastEventUntil_ = currentTime + kEventBroadcastSeconds;
                broadcastEventNotes_ = result.eventNotes;
                broadcastExpectedNotes_ = result.expectedMidiNotes;
                broadcastEventSource_ = result.sourceTag;
            }
        }
        localResults.pop();
    }
}

void NativeDetectorEngine::maybeDispatchCaptureTasks_(uint64_t availableFrames)
{
    std::deque<CaptureTask> readyTasks;
    {
        std::lock_guard<std::mutex> lock(captureMutex_);
        for (size_t i = 0; i < captures_.size();)
        {
            if (captures_[i].readyFrame <= availableFrames)
            {
                readyTasks.push_back(captures_[i]);
                EraseAt(captures_, i);
            }
            else
            {
                ++i;
            }
        }
    }

    while (!readyTasks.empty())
    {
        const CaptureTask task = readyTasks.front();
        readyTasks.pop_front();

        std::vector<float> audio(static_cast<size_t>(kCaptureSamples), 0.0f);
        readAbsoluteRange_(task.startFrame, audio.data(), kCaptureSamples);

        {
            std::lock_guard<std::mutex> lock(deepMutex_);
            deepTasks_.push(std::make_pair(task, std::move(audio)));
        }
        deepCondition_.notify_one();
    }
}

void NativeDetectorEngine::maybeDispatchFastChordTasks_(uint64_t availableFrames, double currentTime)
{
    std::deque<FastChordTask> readyTasks;
    {
        std::lock_guard<std::mutex> lock(captureMutex_);
        while (!fastChordTasks_.empty() && fastChordTasks_.front().readyFrame <= availableFrames)
        {
            readyTasks.push_back(std::move(fastChordTasks_.front()));
            fastChordTasks_.pop_front();
        }
    }

    while (!readyTasks.empty())
    {
        const FastChordTask task = readyTasks.front();
        readyTasks.pop_front();

        if (task.expectedNotes.size() < 2 || task.expectedMidiNotes.empty())
            continue;

        int lowestExpectedMidi = std::numeric_limits<int>::max();
        for (const ExpectedHintNoteSpec& spec : task.expectedNotes)
        {
            if (spec.midi >= 0)
                lowestExpectedMidi = std::min(lowestExpectedMidi, spec.midi);
        }

        if (lowestExpectedMidi == std::numeric_limits<int>::max())
            continue;

        const DetectorSettings settings = getSettingsSnapshot_();
        const int analysisWindowSamples = lowestExpectedMidi <= 40
            ? kFastChordAnalysisWindowLongSamples
            : kFastChordAnalysisWindowShortSamples;

        std::vector<float> audioWindow(static_cast<size_t>(analysisWindowSamples), 0.0f);
        readAbsoluteRange_(task.onsetFrame, audioWindow.data(), analysisWindowSamples);

        const bool fastChordAccepted = scoreExpectedChordConstraint_(audioWindow, task.expectedNotes, settings, "fast");
        std::ostringstream fastLog;
        fastLog << "FAST_CHORD_ATTEMPT"
            << " eventId=" << task.eventId
            << " onsetTime=" << task.onsetTime
            << " dispatchTime=" << currentTime
            << " expectedCount=" << task.expectedNotes.size()
            << " accepted=" << (fastChordAccepted ? 1 : 0);
        AppendDebugLogLine(fastLog.str());

        if (fastChordAccepted)
            publishFastChordEvent_(task.eventId, task.onsetTime, currentTime, task.expectedMidiNotes);
    }
}

void NativeDetectorEngine::maybeDispatchFastSingleTasks_(uint64_t availableFrames, double currentTime, const std::set<int>& currentActiveNotes)
{
    std::deque<FastSingleTask> readyTasks;
    {
        std::lock_guard<std::mutex> lock(captureMutex_);
        while (!fastSingleTasks_.empty() && fastSingleTasks_.front().readyFrame <= availableFrames)
        {
            readyTasks.push_back(std::move(fastSingleTasks_.front()));
            fastSingleTasks_.pop_front();
        }
    }

    while (!readyTasks.empty())
    {
        const FastSingleTask task = readyTasks.front();
        readyTasks.pop_front();

        if (task.expectedNote.midi < 0)
            continue;

        const DetectorSettings settings = getSettingsSnapshot_();
        const int analysisWindowSamples = task.analysisWindowSamples > 0
            ? task.analysisWindowSamples
            : GetFastSinglePrimaryAnalysisWindowSamples(task.expectedNote);

        std::vector<float> audioWindow(static_cast<size_t>(analysisWindowSamples), 0.0f);
        readAbsoluteRange_(task.onsetFrame, audioWindow.data(), analysisWindowSamples);

        const FastSingleWindowEvaluationResult evaluation = EvaluateFastSingleWindow(audioWindow, task.expectedNote, settings);
        bool fastSingleAccepted = evaluation.accepted;
        int conflictingActiveDistance = std::numeric_limits<int>::max();
        for (int activeMidi : currentActiveNotes)
            conflictingActiveDistance = std::min(conflictingActiveDistance, SemitoneDistance(activeMidi, task.expectedNote.midi));

        const bool hasConflictingContinuousPitch =
            !currentActiveNotes.empty() &&
            conflictingActiveDistance > std::max(1, settings.expectMaxDistanceContinuous) &&
            !evaluation.yin.accepted;
        if (fastSingleAccepted && hasConflictingContinuousPitch)
            fastSingleAccepted = false;

        std::ostringstream fastLog;
        fastLog << "FAST_SINGLE_ATTEMPT"
            << " eventId=" << task.eventId
            << " onsetTime=" << task.onsetTime
            << " dispatchTime=" << currentTime
            << " midi=" << task.expectedNote.midi
            << " string=" << task.expectedNote.stringIndex
            << " fret=" << task.expectedNote.fret
            << " proactive=" << (task.proactive ? 1 : 0)
            << " attemptIndex=" << task.attemptIndex
            << " windowSamples=" << analysisWindowSamples
            << " spectralAccepted=" << (evaluation.spectral.accepted ? 1 : 0)
            << " yinAccepted=" << (evaluation.yin.accepted ? 1 : 0)
            << " yinDetectedMidi=" << evaluation.yin.detectedMidi
            << " yinAcceptedHopIndex=" << evaluation.yin.acceptedHopIndex
            << " yinOnsetHopIndex=" << evaluation.yin.onsetHopIndex
            << " yinBestConfidence=" << evaluation.yin.bestConfidence
            << " conflictingActiveDistance=" << (currentActiveNotes.empty() ? -1 : conflictingActiveDistance)
            << " conflictingContinuous=" << (hasConflictingContinuousPitch ? 1 : 0)
            << " accepted=" << (fastSingleAccepted ? 1 : 0);
        AppendDebugLogLine(fastLog.str());

        scoreExpectedSingleConstraint_(audioWindow, task.expectedNote, settings, "fast-single");

        if (fastSingleAccepted)
        {
            publishFastSingleEvent_(task.eventId, task.onsetTime, currentTime, task.expectedNote.midi);
            continue;
        }

        if (task.attemptIndex <= 0)
        {
            const int fallbackWindowSamples = GetFastSingleFallbackAnalysisWindowSamples(task.expectedNote);
            if (fallbackWindowSamples > analysisWindowSamples)
            {
                FastSingleTask retryTask = task;
                retryTask.analysisWindowSamples = fallbackWindowSamples;
                retryTask.readyFrame = task.onsetFrame + static_cast<uint64_t>(fallbackWindowSamples);
                retryTask.attemptIndex = task.attemptIndex + 1;

                std::lock_guard<std::mutex> captureLock(captureMutex_);
                fastSingleTasks_.push_back(std::move(retryTask));
            }
        }
    }
}

bool NativeDetectorEngine::tryScoreFastExpectedChord_(
    uint64_t endFrameExclusive,
    const std::vector<ExpectedHintNoteSpec>& expectedNotes,
    const DetectorSettings& settings) const
{
    int lowestExpectedMidi = std::numeric_limits<int>::max();
    for (const ExpectedHintNoteSpec& spec : expectedNotes)
    {
        if (spec.midi >= 0)
            lowestExpectedMidi = std::min(lowestExpectedMidi, spec.midi);
    }

    if (lowestExpectedMidi == std::numeric_limits<int>::max())
        return false;

    const int analysisWindowSamples = lowestExpectedMidi <= 40
        ? kFastChordAnalysisWindowLongSamples
        : kFastChordAnalysisWindowShortSamples;

    std::vector<float> audioWindow(static_cast<size_t>(analysisWindowSamples), 0.0f);
    readRecentWindow_(endFrameExclusive, audioWindow, analysisWindowSamples);
    return scoreExpectedChordConstraint_(audioWindow, expectedNotes, settings, "fast");
}

bool NativeDetectorEngine::tryScoreFastExpectedSingle_(
    uint64_t endFrameExclusive,
    const ExpectedHintNoteSpec& expectedNote,
    const DetectorSettings& settings) const
{
    if (expectedNote.midi < 0)
        return false;

    const int analysisWindowSamples = expectedNote.midi <= 45
        ? kFastSingleAnalysisWindowLongSamples
        : kFastSingleAnalysisWindowShortSamples;

    std::vector<float> audioWindow(static_cast<size_t>(analysisWindowSamples), 0.0f);
    readRecentWindow_(endFrameExclusive, audioWindow, analysisWindowSamples);
    return scoreExpectedSingleConstraint_(audioWindow, expectedNote, settings, "fast-single");
}

bool NativeDetectorEngine::scoreExpectedChordConstraint_(
    const std::vector<float>& audioWindow,
    const std::vector<ExpectedHintNoteSpec>& expectedNotes,
    const DetectorSettings& settings,
    const char* sourceTag) const
{
    const ConstraintChordEvaluationResult evaluation = EvaluateExpectedChordConstraintWindow(audioWindow, expectedNotes, settings);
    for (const ConstraintChordNoteDebugResult& noteResult : evaluation.noteResults)
    {
        std::ostringstream noteLog;
        noteLog << "CHORD_NOTE"
            << " source=" << (sourceTag != nullptr ? sourceTag : "unknown")
            << " midi=" << noteResult.spec.midi
            << " string=" << noteResult.spec.stringIndex
            << " fret=" << noteResult.spec.fret
            << " openMidi=" << noteResult.spec.openMidi
            << " flags=" << noteResult.spec.flags
            << " supportRatio=" << noteResult.supportRatio
            << " supportThreshold=" << noteResult.supportThreshold
            << " fundamentalRatio=" << noteResult.fundamentalRatio
            << " neighborFundamentalMax=" << noteResult.neighborFundamentalMax
            << " noteScore=" << noteResult.noteScore
            << " noteScoreThreshold=" << noteResult.noteScoreThreshold
            << " peakHz=" << noteResult.dominantPeakHz
            << " centsError=" << noteResult.centsError
            << " hit=" << (noteResult.hit ? 1 : 0);
        AppendDebugLogLine(noteLog.str());
    }

    std::ostringstream summaryLog;
    summaryLog << "CHORD_SCORE"
        << " source=" << (sourceTag != nullptr ? sourceTag : "unknown")
        << " hitCount=" << evaluation.hitCount
        << " totalExpected=" << evaluation.totalExpected
        << " requiredHits=" << evaluation.requiredHits
        << " chordLeniency=" << evaluation.chordLeniency
        << " accepted=" << (evaluation.accepted ? 1 : 0);
    AppendDebugLogLine(summaryLog.str());

    return evaluation.accepted;
}

bool NativeDetectorEngine::scoreExpectedSingleConstraint_(
    const std::vector<float>& audioWindow,
    const ExpectedHintNoteSpec& expectedNote,
    const DetectorSettings& settings,
    const char* sourceTag) const
{
    const ConstraintSingleEvaluationResult evaluation = EvaluateExpectedSingleConstraintWindow(audioWindow, expectedNote, settings);
    const ConstraintChordNoteDebugResult& noteResult = evaluation.noteResult;

    std::ostringstream noteLog;
    noteLog << "SINGLE_NOTE"
        << " source=" << (sourceTag != nullptr ? sourceTag : "unknown")
        << " midi=" << noteResult.spec.midi
        << " string=" << noteResult.spec.stringIndex
        << " fret=" << noteResult.spec.fret
        << " openMidi=" << noteResult.spec.openMidi
        << " flags=" << noteResult.spec.flags
        << " supportRatio=" << noteResult.supportRatio
        << " supportThreshold=" << noteResult.supportThreshold
        << " fundamentalRatio=" << noteResult.fundamentalRatio
        << " neighborFundamentalMax=" << noteResult.neighborFundamentalMax
        << " noteScore=" << noteResult.noteScore
        << " noteScoreThreshold=" << noteResult.noteScoreThreshold
        << " peakHz=" << noteResult.dominantPeakHz
        << " centsError=" << noteResult.centsError
        << " hit=" << (noteResult.hit ? 1 : 0);
    AppendDebugLogLine(noteLog.str());

    std::ostringstream summaryLog;
    summaryLog << "SINGLE_SCORE"
        << " source=" << (sourceTag != nullptr ? sourceTag : "unknown")
        << " midi=" << expectedNote.midi
        << " accepted=" << (evaluation.accepted ? 1 : 0);
    AppendDebugLogLine(summaryLog.str());

    return evaluation.accepted;
}

std::vector<VerifierExpectedGroup> NativeDetectorEngine::buildVerifierGroups_(const ExpectedHintContext& context) const
{
    std::vector<VerifierExpectedGroup> groups;
    if (!context.hasWindow || context.expectedNotes.empty())
        return groups;

    for (const ExpectedHintNoteSpec& spec : context.expectedNotes)
    {
        if (spec.noteId < 0 || spec.midi < 0 || spec.stringIndex < 0 || spec.openMidi < 0)
            continue;

        const bool chordGroup = spec.chordId >= 0;
        VerifierExpectedGroup* group = nullptr;
        for (VerifierExpectedGroup& existing : groups)
        {
            if ((chordGroup && existing.chordId == spec.chordId) ||
                (!chordGroup && existing.chordId < 0 && existing.expectedNotes.size() == 1 && existing.expectedNotes.front().noteId == spec.noteId))
            {
                group = &existing;
                break;
            }
        }

        if (group == nullptr)
        {
            VerifierExpectedGroup newGroup;
            newGroup.chordId = spec.chordId;
            newGroup.noteTime = spec.noteTime >= 0.0 ? spec.noteTime : context.windowStartTime;
            newGroup.notePythonTime = spec.notePythonTime >= 0.0 ? spec.notePythonTime : context.windowStartPythonTime;
            groups.push_back(std::move(newGroup));
            group = &groups.back();
        }

        bool duplicate = false;
        for (const ExpectedHintNoteSpec& existing : group->expectedNotes)
        {
            if (ExpectedHintNoteSpecsEqual(existing, spec))
            {
                duplicate = true;
                break;
            }
        }

        if (!duplicate)
            group->expectedNotes.push_back(spec);

        if (spec.noteTime >= 0.0)
            group->noteTime = group->noteTime < 0.0 ? spec.noteTime : std::min(group->noteTime, spec.noteTime);
        if (spec.notePythonTime >= 0.0)
            group->notePythonTime = group->notePythonTime < 0.0 ? spec.notePythonTime : std::min(group->notePythonTime, spec.notePythonTime);
        if ((spec.flags & ExpectedHintNoteFlagLegato) == 0)
            group->requiresOnset = true;
    }

    groups.erase(
        std::remove_if(groups.begin(), groups.end(), [](const VerifierExpectedGroup& group)
        {
            return group.expectedNotes.empty();
        }),
        groups.end());

    return groups;
}

bool NativeDetectorEngine::verifierGroupHasOnset_(const VerifierExpectedGroup& group, const std::deque<double>& onsetTimes) const
{
    if (!group.requiresOnset)
        return true;

    if (group.notePythonTime < 0.0)
        return false;

    const double windowStart = group.notePythonTime - kVerifierOnsetEarlySeconds;
    const double windowEnd = group.notePythonTime + kVerifierOnsetLateSeconds;
    for (double onsetTime : onsetTimes)
    {
        if (onsetTime >= windowStart && onsetTime <= windowEnd)
            return true;
    }

    return false;
}

void NativeDetectorEngine::publishVerifierVerdict_(
    const ExpectedHintNoteSpec& spec,
    const ConstraintChordNoteDebugResult& noteResult,
    double currentTime,
    const char* sourceTag)
{
    if (spec.noteId < 0 || spec.midi < 0 || !noteResult.hit)
        return;

    std::lock_guard<std::mutex> lock(stateMutex_);
    if (!verifierResolvedNoteIds_.insert(spec.noteId).second)
        return;

    NativeVerifierVerdict verdict;
    verdict.noteId = spec.noteId;
    verdict.chordId = spec.chordId;
    verdict.midi = spec.midi;
    verdict.hit = true;
    verdict.noteTime = spec.noteTime;
    verdict.detectedSongTime = spec.noteTime >= 0.0 ? spec.noteTime : currentTime;
    verdict.confidence = noteResult.noteScore;
    verdict.centsError = noteResult.centsError;
    verdict.source = sourceTag != nullptr ? sourceTag : "verifier-v2";
    verifierVerdicts_.push_back(verdict);
    while (verifierVerdicts_.size() > 128)
        verifierVerdicts_.pop_front();

    std::ostringstream log;
    log << "VERIFIER_HIT"
        << " source=" << verdict.source
        << " noteId=" << verdict.noteId
        << " chordId=" << verdict.chordId
        << " midi=" << verdict.midi
        << " noteTime=" << verdict.noteTime
        << " currentTime=" << currentTime
        << " score=" << verdict.confidence
        << " centsError=" << verdict.centsError;
    AppendDebugLogLine(log.str());
}

void NativeDetectorEngine::publishVerifierVerdicts_(
    const VerifierExpectedGroup&,
    const ConstraintChordEvaluationResult& evaluation,
    double currentTime,
    const char* sourceTag)
{
    for (const ConstraintChordNoteDebugResult& noteResult : evaluation.noteResults)
    {
        if (noteResult.hit)
            publishVerifierVerdict_(noteResult.spec, noteResult, currentTime, sourceTag);
    }
}

void NativeDetectorEngine::maybeRunExpectedNoteVerifier_(
    uint64_t,
    double currentTime,
    const DetectorSettings& settings,
    const std::deque<double>& onsetTimes)
{
    {
        std::lock_guard<std::mutex> lock(stateMutex_);
        if (!verifierEnabled_)
            return;

        if (currentTime - verifierLastScoreTime_ < kVerifierScoreIntervalSeconds)
            return;
        verifierLastScoreTime_ = currentTime;
    }

    double unityTime = -1.0;
    const ExpectedHintContext context = hintState_.GetExpectedContextForPythonTime(currentTime, &unityTime);
    std::vector<VerifierExpectedGroup> groups = buildVerifierGroups_(context);
    if (groups.empty())
        return;

    std::sort(groups.begin(), groups.end(), [currentTime](const VerifierExpectedGroup& left, const VerifierExpectedGroup& right)
    {
        const double leftDistance = left.notePythonTime >= 0.0 ? std::abs(left.notePythonTime - currentTime) : std::numeric_limits<double>::max();
        const double rightDistance = right.notePythonTime >= 0.0 ? std::abs(right.notePythonTime - currentTime) : std::numeric_limits<double>::max();
        return leftDistance < rightDistance;
    });

    const int groupLimit = std::min<int>(static_cast<int>(groups.size()), kVerifierMaxGroupsPerHop);
    for (int groupIndex = 0; groupIndex < groupLimit; ++groupIndex)
    {
        const VerifierExpectedGroup& group = groups[static_cast<size_t>(groupIndex)];
        if (group.expectedNotes.empty())
            continue;

        bool allNotesAlreadyPublished = true;
        {
            std::lock_guard<std::mutex> lock(stateMutex_);
            for (const ExpectedHintNoteSpec& spec : group.expectedNotes)
            {
                if (spec.noteId >= 0 && verifierResolvedNoteIds_.find(spec.noteId) == verifierResolvedNoteIds_.end())
                {
                    allNotesAlreadyPublished = false;
                    break;
                }
            }
        }
        if (allNotesAlreadyPublished)
            continue;

        if (group.requiresOnset &&
            group.notePythonTime >= 0.0 &&
            currentTime > group.notePythonTime + kVerifierOnsetLateSeconds)
        {
            continue;
        }

        const bool hasOnset = verifierGroupHasOnset_(group, onsetTimes);
        const bool presenceOnlyChordWindow =
            group.expectedNotes.size() >= 2 &&
            group.notePythonTime >= 0.0 &&
            currentTime >= group.notePythonTime - 0.020 &&
            currentTime <= group.notePythonTime + kVerifierOnsetLateSeconds;
        if (group.requiresOnset && !hasOnset && !presenceOnlyChordWindow)
            continue;

        int lowestExpectedMidi = std::numeric_limits<int>::max();
        for (const ExpectedHintNoteSpec& spec : group.expectedNotes)
        {
            if (spec.midi >= 0)
                lowestExpectedMidi = std::min(lowestExpectedMidi, spec.midi);
        }

        if (lowestExpectedMidi == std::numeric_limits<int>::max())
            continue;

        const int analysisWindowSamples = group.expectedNotes.size() >= 2
            ? (lowestExpectedMidi <= 40 ? kFastChordAnalysisWindowLongSamples : kFastChordAnalysisWindowShortSamples)
            : GetFastSinglePrimaryAnalysisWindowSamples(group.expectedNotes.front());
        const uint64_t endFrame = static_cast<uint64_t>(std::max(0.0, std::floor(currentTime * static_cast<double>(kSampleRate))));
        std::vector<float> audioWindow(static_cast<size_t>(analysisWindowSamples), 0.0f);
        readRecentWindow_(endFrame, audioWindow, analysisWindowSamples);

        if (group.expectedNotes.size() >= 2)
        {
            ConstraintChordEvaluationResult evaluation = EvaluateExpectedChordConstraintWindow(audioWindow, group.expectedNotes, settings);
            if (evaluation.accepted)
                publishVerifierVerdicts_(group, evaluation, currentTime, "verifier-v2");
            continue;
        }

        ConstraintSingleEvaluationResult evaluation = EvaluateExpectedSingleConstraintWindow(audioWindow, group.expectedNotes.front(), settings);
        if (evaluation.accepted)
            publishVerifierVerdict_(group.expectedNotes.front(), evaluation.noteResult, currentTime, "verifier-v2");
    }
}

void NativeDetectorEngine::publishFastChordEvent_(
    int eventId,
    double onsetTime,
    double currentTime,
    const std::set<int>& expectedMidi)
{
    if (eventId <= 0 || expectedMidi.empty())
        return;

    std::lock_guard<std::mutex> lock(stateMutex_);
    const bool mergeableExisting =
        broadcastEventId_ > 0 &&
        !broadcastExpectedNotes_.empty() &&
        !broadcastEventNotes_.empty() &&
        std::abs(onsetTime - broadcastEventOnsetTime_) <= kChordResultMergeSeconds &&
        CountSetOverlap(broadcastExpectedNotes_, expectedMidi) > 0;

    if (mergeableExisting)
    {
        broadcastEventOnsetTime_ = std::min(broadcastEventOnsetTime_, onsetTime);
        broadcastEventUntil_ = std::max(broadcastEventUntil_, currentTime + kEventBroadcastSeconds);
        broadcastEventNotes_.insert(expectedMidi.begin(), expectedMidi.end());
        broadcastExpectedNotes_.insert(expectedMidi.begin(), expectedMidi.end());
        broadcastEventSource_ = MergeEventSourceTags(broadcastEventSource_, "fast-chord");
    }
    else
    {
        broadcastEventId_ = eventId;
        broadcastEventOnsetTime_ = onsetTime;
        broadcastEventUntil_ = currentTime + kEventBroadcastSeconds;
        broadcastEventNotes_ = expectedMidi;
        broadcastExpectedNotes_ = expectedMidi;
        broadcastEventSource_ = "fast-chord";
    }

    fastChordActiveNotes_ = expectedMidi;
    fastChordActiveUntil_ = currentTime + kEventBroadcastSeconds;
}

void NativeDetectorEngine::publishFastSingleEvent_(
    int eventId,
    double onsetTime,
    double currentTime,
    int expectedMidi)
{
    if (eventId <= 0 || expectedMidi < 0)
        return;

    const std::set<int> expectedMidiSet = { expectedMidi };

    std::lock_guard<std::mutex> lock(stateMutex_);
    const bool mergeableExisting =
        broadcastEventId_ > 0 &&
        !broadcastExpectedNotes_.empty() &&
        !broadcastEventNotes_.empty() &&
        std::abs(onsetTime - broadcastEventOnsetTime_) <= kChordResultMergeSeconds &&
        CountSetOverlap(broadcastExpectedNotes_, expectedMidiSet) > 0;

    if (mergeableExisting)
    {
        broadcastEventOnsetTime_ = std::min(broadcastEventOnsetTime_, onsetTime);
        broadcastEventUntil_ = std::max(broadcastEventUntil_, currentTime + kEventBroadcastSeconds);
        broadcastEventNotes_.insert(expectedMidi);
        broadcastExpectedNotes_.insert(expectedMidi);
        broadcastEventSource_ = MergeEventSourceTags(broadcastEventSource_, "fast-single");
    }
    else
    {
        broadcastEventId_ = eventId;
        broadcastEventOnsetTime_ = onsetTime;
        broadcastEventUntil_ = currentTime + kEventBroadcastSeconds;
        broadcastEventNotes_ = expectedMidiSet;
        broadcastExpectedNotes_ = expectedMidiSet;
        broadcastEventSource_ = "fast-single";
    }
}

void NativeDetectorEngine::updateContinuousNotes_(
    const std::vector<float>& hop,
    double currentTime,
    std::deque<int>& recentPitchMidi,
    int& stableMidi,
    int& stableCount,
    double& lastContinuousTime,
    std::set<int>& currentActiveNotes)
{
    const DetectorSettings settings = getSettingsSnapshot_();
    double unityTime = -1.0;
    const std::set<int> expectedMidi = hintState_.GetExpectedNotesForPythonTime(currentTime, &unityTime);
    const ExpectedContextKind expectedContext = GetExpectedContextKind(expectedMidi, settings);
    const bool highStringContext = expectedContext == ExpectedContextKind::HighStringFocused;
    const float rmsGate = highStringContext ? (settings.continuousRmsGate * settings.highStringRmsMultiplier) : settings.continuousRmsGate;
    const float relaxedConfidenceGate = std::min(
        settings.continuousConfidenceGate,
        highStringContext ? (settings.continuousConfidenceGate * settings.highStringConfidenceMultiplier) : settings.continuousConfidenceGate);

    const float rms = ComputeRms(hop.data(), static_cast<int>(hop.size()));
    if (!std::isfinite(rms) || rms < rmsGate)
    {
        recentPitchMidi.clear();
        stableMidi = -1;
        stableCount = 0;
        if ((currentTime - lastContinuousTime) > settings.continuousHoldSeconds)
            currentActiveNotes.clear();
        return;
    }

    float midiEstimate = -1.0f;
    float confidence = 0.0f;
    if (!detectPitchYin_(hop, midiEstimate, confidence) ||
        !std::isfinite(midiEstimate) ||
        midiEstimate < static_cast<float>(kContinuousMinMidi) ||
        midiEstimate > static_cast<float>(kContinuousMaxMidi))
    {
        if ((currentTime - lastContinuousTime) > settings.continuousHoldSeconds)
            currentActiveNotes.clear();
        return;
    }

    const int candidateMidi = static_cast<int>(std::round(midiEstimate));
    bool accepted = confidence >= settings.continuousConfidenceGate;
    if (!accepted && highStringContext && confidence >= relaxedConfidenceGate)
    {
        int bestDistance = std::numeric_limits<int>::max();
        for (int expected : expectedMidi)
            bestDistance = std::min(bestDistance, SemitoneDistance(candidateMidi, expected));
        if (bestDistance <= settings.highStringBenefitMatchMaxDistance)
            accepted = true;
    }

    if (!accepted)
    {
        if ((currentTime - lastContinuousTime) > settings.continuousHoldSeconds)
            currentActiveNotes.clear();
        return;
    }

    if (!expectedMidi.empty())
    {
        int bestDistance = std::numeric_limits<int>::max();
        for (int expected : expectedMidi)
            bestDistance = std::min(bestDistance, SemitoneDistance(candidateMidi, expected));
        if (bestDistance > settings.expectMaxDistanceContinuous && confidence < settings.expectStrictConfidence)
            return;
    }

    recentPitchMidi.push_back(candidateMidi);
    while (recentPitchMidi.size() > static_cast<size_t>(kContinuousMedianWindow))
        recentPitchMidi.pop_front();

    int mostCommonMidi = candidateMidi;
    int bestCount = 0;
    for (int value : recentPitchMidi)
    {
        const int count = static_cast<int>(std::count(recentPitchMidi.begin(), recentPitchMidi.end(), value));
        if (count > bestCount)
        {
            bestCount = count;
            mostCommonMidi = value;
        }
    }

    if (mostCommonMidi == stableMidi)
        ++stableCount;
    else
    {
        stableMidi = mostCommonMidi;
        stableCount = 1;
    }

    if (stableCount >= 1)
    {
        currentActiveNotes.clear();
        currentActiveNotes.insert(stableMidi);
        lastContinuousTime = currentTime;
    }
}

bool NativeDetectorEngine::detectOnset_(const std::vector<float>& hop, const std::set<int>& expectedMidi)
{
    if (aubioOnset_ == nullptr || aubioHopInput_ == nullptr || aubioOnsetOutput_ == nullptr || hop.size() < static_cast<size_t>(kHopSize))
        return false;

    const DetectorSettings settings = getSettingsSnapshot_();
    const ExpectedContextKind expectedContext = GetExpectedContextKind(expectedMidi, settings);
    aubio_onset_set_threshold(aubioOnset_, expectedContext == ExpectedContextKind::HighStringFocused ? settings.highStringOnsetThreshold : settings.standardOnsetThreshold);
    memcpy(aubioHopInput_->data, hop.data(), static_cast<size_t>(kHopSize) * sizeof(float));
    aubio_onset_do(aubioOnset_, aubioHopInput_, aubioOnsetOutput_);
    return aubioOnsetOutput_->data[0] > 0.0f;
}

bool NativeDetectorEngine::detectPitchYin_(const std::vector<float>& hop, float& midiOut, float& confidenceOut)
{
    if (aubioPitch_ == nullptr || aubioHopInput_ == nullptr || aubioPitchOutput_ == nullptr || hop.size() < static_cast<size_t>(kHopSize))
        return false;

    memcpy(aubioHopInput_->data, hop.data(), static_cast<size_t>(kHopSize) * sizeof(float));
    aubio_pitch_do(aubioPitch_, aubioHopInput_, aubioPitchOutput_);
    midiOut = aubioPitchOutput_->data[0];
    confidenceOut = aubio_pitch_get_confidence(aubioPitch_);
    return std::isfinite(midiOut) && std::isfinite(confidenceOut) && midiOut > 0.0f;
}

std::vector<NoteEventCandidate> NativeDetectorEngine::decodeBasicPitch_(const std::vector<float>& noteOutput, const std::vector<float>& onsetOutput) const
{
    if (noteOutput.empty() ||
        onsetOutput.empty() ||
        noteOutput.size() != onsetOutput.size() ||
        (noteOutput.size() % static_cast<size_t>(kModelOutputPitches)) != 0)
    {
        return {};
    }

    const int effectiveFrames = static_cast<int>(noteOutput.size() / static_cast<size_t>(kModelOutputPitches));
    if (effectiveFrames <= 0)
        return {};

    std::vector<float> frames = noteOutput;
    std::vector<float> onsets = onsetOutput;
    std::vector<float> inferredOnsets = onsets;
    inferOnsets_(inferredOnsets, frames, effectiveFrames);
    return outputToNotesPolyphonic_(frames, inferredOnsets, effectiveFrames);
}

void NativeDetectorEngine::inferOnsets_(std::vector<float>& onsets, const std::vector<float>& frames, int nFrames) const
{
    std::vector<float> frameDiff(static_cast<size_t>(nFrames * kModelOutputPitches), 0.0f);
    for (int diff = 1; diff <= 2; ++diff)
    {
        for (int time = diff; time < nFrames; ++time)
        {
            for (int pitch = 0; pitch < kModelOutputPitches; ++pitch)
            {
                const size_t index = static_cast<size_t>(time * kModelOutputPitches + pitch);
                const size_t previousIndex = static_cast<size_t>((time - diff) * kModelOutputPitches + pitch);
                const float delta = frames[index] - frames[previousIndex];
                if (diff == 1 || delta < frameDiff[index])
                    frameDiff[index] = delta;
            }
        }
    }

    float maxOnset = 0.0f;
    float maxDiff = 0.0f;
    for (float value : onsets)
        maxOnset = std::max(maxOnset, value);
    for (float& value : frameDiff)
    {
        if (value < 0.0f)
            value = 0.0f;
        maxDiff = std::max(maxDiff, value);
    }

    for (int time = 0; time < std::min(2, nFrames); ++time)
    {
        for (int pitch = 0; pitch < kModelOutputPitches; ++pitch)
            frameDiff[static_cast<size_t>(time * kModelOutputPitches + pitch)] = 0.0f;
    }

    if (maxDiff > std::numeric_limits<float>::epsilon())
    {
        const float scale = maxOnset / maxDiff;
        for (int i = 0; i < static_cast<int>(frameDiff.size()); ++i)
            onsets[static_cast<size_t>(i)] = std::max(onsets[static_cast<size_t>(i)], frameDiff[static_cast<size_t>(i)] * scale);
    }
}

std::vector<NoteEventCandidate> NativeDetectorEngine::outputToNotesPolyphonic_(const std::vector<float>& frames, const std::vector<float>& onsets, int nFrames) const
{
    struct OnsetPeak { int time = 0; int pitch = 0; };
    std::vector<float> remainingEnergy = frames;
    std::vector<OnsetPeak> peaks;

    for (int pitch = 0; pitch < kModelOutputPitches; ++pitch)
    {
        for (int time = 1; time < nFrames - 1; ++time)
        {
            const float value = onsets[static_cast<size_t>(time * kModelOutputPitches + pitch)];
            const float previous = onsets[static_cast<size_t>((time - 1) * kModelOutputPitches + pitch)];
            const float next = onsets[static_cast<size_t>((time + 1) * kModelOutputPitches + pitch)];
            if (value >= kOnsetThreshold && value > previous && value > next)
                peaks.push_back({ time, pitch });
        }
    }

    std::sort(peaks.begin(), peaks.end(), [](const OnsetPeak& a, const OnsetPeak& b)
    {
        if (a.time != b.time)
            return a.time > b.time;
        return a.pitch > b.pitch;
    });

    std::vector<NoteEventCandidate> events;
    auto clearEnergyRange = [&](int startFrame, int endFrame, int pitch)
    {
        for (int time = startFrame; time < endFrame; ++time)
        {
            for (int neighbor = std::max(0, pitch - 1); neighbor <= std::min(kModelOutputPitches - 1, pitch + 1); ++neighbor)
                remainingEnergy[static_cast<size_t>(time * kModelOutputPitches + neighbor)] = 0.0f;
        }
    };

    for (const OnsetPeak& peak : peaks)
    {
        if (peak.time >= nFrames - 1)
            continue;

        int time = peak.time + 1;
        int silentFrames = 0;
        while (time < nFrames - 1 && silentFrames < kMelodiaEnergyTolerance)
        {
            if (remainingEnergy[static_cast<size_t>(time * kModelOutputPitches + peak.pitch)] < kFrameThreshold)
                ++silentFrames;
            else
                silentFrames = 0;
            ++time;
        }

        time -= silentFrames;
        if ((time - peak.time) <= kMinimumNoteLengthFrames)
            continue;

        float amplitudeSum = 0.0f;
        for (int frame = peak.time; frame < time; ++frame)
            amplitudeSum += frames[static_cast<size_t>(frame * kModelOutputPitches + peak.pitch)];
        const float amplitude = amplitudeSum / static_cast<float>(time - peak.time);

        clearEnergyRange(peak.time, time, peak.pitch);
        events.push_back({ peak.pitch + 21, amplitude });
    }

    while (true)
    {
        float bestValue = kFrameThreshold;
        int bestTime = -1;
        int bestPitch = -1;
        for (int time = 0; time < nFrames; ++time)
        {
            for (int pitch = 0; pitch < kModelOutputPitches; ++pitch)
            {
                const float value = remainingEnergy[static_cast<size_t>(time * kModelOutputPitches + pitch)];
                if (value > bestValue)
                {
                    bestValue = value;
                    bestTime = time;
                    bestPitch = pitch;
                }
            }
        }

        if (bestTime < 0 || bestPitch < 0)
            break;

        remainingEnergy[static_cast<size_t>(bestTime * kModelOutputPitches + bestPitch)] = 0.0f;

        int endFrame = bestTime + 1;
        int silentFrames = 0;
        while (endFrame < nFrames - 1 && silentFrames < kMelodiaEnergyTolerance)
        {
            if (remainingEnergy[static_cast<size_t>(endFrame * kModelOutputPitches + bestPitch)] < kFrameThreshold)
                ++silentFrames;
            else
                silentFrames = 0;

            remainingEnergy[static_cast<size_t>(endFrame * kModelOutputPitches + bestPitch)] = 0.0f;
            if (bestPitch > 0)
                remainingEnergy[static_cast<size_t>(endFrame * kModelOutputPitches + bestPitch - 1)] = 0.0f;
            if (bestPitch < kModelOutputPitches - 1)
                remainingEnergy[static_cast<size_t>(endFrame * kModelOutputPitches + bestPitch + 1)] = 0.0f;
            ++endFrame;
        }
        endFrame = endFrame - 1 - silentFrames;

        int startFrame = bestTime - 1;
        silentFrames = 0;
        while (startFrame > 0 && silentFrames < kMelodiaEnergyTolerance)
        {
            if (remainingEnergy[static_cast<size_t>(startFrame * kModelOutputPitches + bestPitch)] < kFrameThreshold)
                ++silentFrames;
            else
                silentFrames = 0;

            remainingEnergy[static_cast<size_t>(startFrame * kModelOutputPitches + bestPitch)] = 0.0f;
            if (bestPitch > 0)
                remainingEnergy[static_cast<size_t>(startFrame * kModelOutputPitches + bestPitch - 1)] = 0.0f;
            if (bestPitch < kModelOutputPitches - 1)
                remainingEnergy[static_cast<size_t>(startFrame * kModelOutputPitches + bestPitch + 1)] = 0.0f;
            --startFrame;
        }
        startFrame += silentFrames + 1;

        if ((endFrame - startFrame) <= kMinimumNoteLengthFrames)
            continue;

        float amplitudeSum = 0.0f;
        for (int frame = startFrame; frame < endFrame; ++frame)
            amplitudeSum += frames[static_cast<size_t>(frame * kModelOutputPitches + bestPitch)];
        const float amplitude = amplitudeSum / static_cast<float>(endFrame - startFrame);
        events.push_back({ bestPitch + 21, amplitude });
    }

    return events;
}

std::set<int> NativeDetectorEngine::scoreAiCandidates_(const std::vector<NoteEventCandidate>& candidates, const std::set<int>& expectedMidi) const
{
    if (candidates.empty())
        return {};

    const DetectorSettings settings = getSettingsSnapshot_();
    const ExpectedContextKind expectedContext = GetExpectedContextKind(expectedMidi, settings);
    const bool mixedChordContext = expectedContext == ExpectedContextKind::MixedChord;
    const int lowestExpectedMidi = expectedMidi.empty() ? -1 : *expectedMidi.begin();

    struct ScoredCandidate
    {
        int midi = 0;
        float relativeAmplitude = 0.0f;
        float score = 0.0f;
        int distance = -1;
        bool hasDistance = false;
    };

    float maxAmplitude = 0.0f;
    for (const NoteEventCandidate& candidate : candidates)
        maxAmplitude = std::max(maxAmplitude, candidate.amplitude);
    if (maxAmplitude <= std::numeric_limits<float>::epsilon())
        maxAmplitude = 1.0f;

    std::vector<ScoredCandidate> scored;
    for (const NoteEventCandidate& candidate : candidates)
    {
        ScoredCandidate scoredCandidate;
        scoredCandidate.midi = candidate.midi;
        scoredCandidate.relativeAmplitude = candidate.amplitude / maxAmplitude;
        scoredCandidate.score = scoredCandidate.relativeAmplitude;

        if (!expectedMidi.empty())
        {
            int bestDistance = std::numeric_limits<int>::max();
            for (int expected : expectedMidi)
                bestDistance = std::min(bestDistance, SemitoneDistance(candidate.midi, expected));
            if (bestDistance < std::numeric_limits<int>::max())
            {
                scoredCandidate.distance = bestDistance;
                scoredCandidate.hasDistance = true;
                if (bestDistance == 0)
                    scoredCandidate.score += settings.expectExactBonus;
                else if (bestDistance == 1)
                    scoredCandidate.score += settings.expectNearBonus1;
                else if (bestDistance <= settings.expectMaxDistanceAi)
                    scoredCandidate.score += settings.expectNearBonus2;
            }
        }

        auto existing = std::find_if(scored.begin(), scored.end(), [&](const ScoredCandidate& current) { return current.midi == scoredCandidate.midi; });
        if (existing == scored.end())
            scored.push_back(scoredCandidate);
        else if (scoredCandidate.score > existing->score)
            *existing = scoredCandidate;
    }

    std::sort(scored.begin(), scored.end(), [](const ScoredCandidate& a, const ScoredCandidate& b)
    {
        if (a.score != b.score)
            return a.score > b.score;
        if (a.relativeAmplitude != b.relativeAmplitude)
            return a.relativeAmplitude > b.relativeAmplitude;
        return MidiToNoteName(a.midi) < MidiToNoteName(b.midi);
    });

    if (scored.empty())
        return {};

    const float bestScore = scored.front().score;
    bool expectedExactPresent = false;
    for (const ScoredCandidate& candidate : scored)
    {
        if (candidate.hasDistance && candidate.distance == 0)
        {
            expectedExactPresent = true;
            break;
        }
    }

    const float keepRatio = expectedExactPresent ? settings.chordExpectedScoreKeepRatio : settings.chordScoreKeepRatio;
    std::set<int> primarySelected;
    for (const ScoredCandidate& candidate : scored)
    {
        if (primarySelected.size() >= static_cast<size_t>(kMaxEventNotes))
            break;
        if (candidate.score < bestScore * keepRatio)
            continue;
        if (!expectedMidi.empty() && candidate.hasDistance && candidate.distance > settings.expectMaxDistanceAi && candidate.relativeAmplitude < 0.95f)
            continue;
        primarySelected.insert(candidate.midi);
    }

    std::vector<int> orderedSelected;
    orderedSelected.reserve(static_cast<size_t>(kMaxEventNotes));
    auto appendSelectedMidi = [&](int midi)
    {
        if (midi < 0)
            return;
        if (std::find(orderedSelected.begin(), orderedSelected.end(), midi) != orderedSelected.end())
            return;
        if (orderedSelected.size() >= static_cast<size_t>(kMaxEventNotes))
            return;
        orderedSelected.push_back(midi);
    };

    if (!expectedMidi.empty())
    {
        for (int expected : expectedMidi)
        {
            const bool isLowestExpected = mixedChordContext && expected == lowestExpectedMidi;
            const ScoredCandidate* bestCoverageCandidate = nullptr;
            float bestCoverageScore = -std::numeric_limits<float>::infinity();

            for (const ScoredCandidate& candidate : scored)
            {
                const int distance = SemitoneDistance(candidate.midi, expected);
                if (distance > settings.expectMaxDistanceAi)
                    continue;

                const float minimumAmplitude = isLowestExpected ? 0.10f : 0.16f;
                if (candidate.relativeAmplitude < minimumAmplitude)
                    continue;

                float coverageScore = candidate.relativeAmplitude;
                if (distance == 0)
                    coverageScore += isLowestExpected ? 0.85f : 0.65f;
                else if (distance == 1)
                    coverageScore += 0.15f;
                else
                    coverageScore += 0.05f;

                coverageScore -= static_cast<float>(distance) * 0.22f;

                if (isLowestExpected && candidate.midi <= expected)
                    coverageScore += 0.10f;

                if (coverageScore > bestCoverageScore)
                {
                    bestCoverageScore = coverageScore;
                    bestCoverageCandidate = &candidate;
                }
            }

            if (bestCoverageCandidate == nullptr)
                continue;

            const float minimumCoverageScore = isLowestExpected ? 0.30f : 0.42f;
            if (bestCoverageScore < minimumCoverageScore)
                continue;

            appendSelectedMidi(bestCoverageCandidate->midi);
        }
    }

    for (const ScoredCandidate& candidate : scored)
    {
        if (primarySelected.find(candidate.midi) == primarySelected.end())
            continue;
        appendSelectedMidi(candidate.midi);
    }

    if (!orderedSelected.empty())
        return std::set<int>(orderedSelected.begin(), orderedSelected.end());

    std::set<int> selected;
    for (const ScoredCandidate& candidate : scored)
    {
        if (candidate.relativeAmplitude > 0.40f)
            selected.insert(candidate.midi);
        if (selected.size() >= static_cast<size_t>(kMaxEventNotes))
            break;
    }
    return selected;
}

std::set<int> NativeDetectorEngine::applyLowestExpectedBassRescue_(const std::vector<float>& audio, const std::set<int>& expectedMidi, const std::set<int>& selectedMidi) const
{
    if (audio.empty() || expectedMidi.size() < 2 || selectedMidi.empty())
        return selectedMidi;

    const int lowestExpectedMidi = *expectedMidi.begin();
    if (selectedMidi.find(lowestExpectedMidi) != selectedMidi.end())
        return selectedMidi;

    const DetectorSettings settings = getSettingsSnapshot_();
    if (lowestExpectedMidi >= settings.highStringMinMidi)
        return selectedMidi;

    const size_t expectedOverlap = CountSetOverlap(expectedMidi, selectedMidi);
    if (expectedOverlap < kBassRescueMinExpectedOverlap)
        return selectedMidi;

    const float targetFrequency = MidiToFrequencyHz(lowestExpectedMidi);
    if (!std::isfinite(targetFrequency) || targetFrequency < 60.0f || targetFrequency > 220.0f)
        return selectedMidi;

    bool hasOctaveSupport = false;
    const int lowestPitchClass = ((lowestExpectedMidi % 12) + 12) % 12;
    for (int midi : selectedMidi)
    {
        if (midi <= lowestExpectedMidi)
            continue;
        if (midi > lowestExpectedMidi + 24)
            continue;
        if ((((midi % 12) + 12) % 12) == lowestPitchClass)
        {
            hasOctaveSupport = true;
            break;
        }
    }

    const std::array<int, 2> windowStarts = { kBassRescuePrimaryWindowStartSamples, kBassRescueSecondaryWindowStartSamples };
    bool rescued = false;

    for (int windowStart : windowStarts)
    {
        const float sliceRms = ComputeWindowRms(audio, windowStart, kBassRescueAnalysisWindowSamples);
        if (!std::isfinite(sliceRms) || sliceRms <= std::numeric_limits<float>::epsilon())
            continue;

        float targetFundamentalAmplitude = 0.0f;
        float targetSalience = ComputeHarmonicSalience(
            audio,
            windowStart,
            kBassRescueAnalysisWindowSamples,
            lowestExpectedMidi,
            &targetFundamentalAmplitude);
        if (!std::isfinite(targetSalience) || !std::isfinite(targetFundamentalAmplitude))
            continue;

        float strongestNeighborFundamentalAmplitude = 0.0f;
        float strongestNeighborSalience = 0.0f;
        for (int offset : { -2, -1, 1, 2 })
        {
            const int neighborMidi = lowestExpectedMidi + offset;
            if (neighborMidi < kContinuousMinMidi || neighborMidi > kContinuousMaxMidi)
                continue;

            float neighborFundamentalAmplitude = 0.0f;
            const float neighborSalience = ComputeHarmonicSalience(
                audio,
                windowStart,
                kBassRescueAnalysisWindowSamples,
                neighborMidi,
                &neighborFundamentalAmplitude);
            strongestNeighborFundamentalAmplitude = std::max(strongestNeighborFundamentalAmplitude, neighborFundamentalAmplitude);
            strongestNeighborSalience = std::max(strongestNeighborSalience, neighborSalience);
        }

        if (hasOctaveSupport)
            targetSalience *= kBassRescueOctaveSupportMultiplier;

        const float minimumFundamentalAmplitude = std::max(
            kBassRescueAbsoluteAmplitudeFloor,
            sliceRms * kBassRescueSliceRmsRatio * (hasOctaveSupport ? kBassRescueFundamentalRelaxedScale : 1.0f));
        const float neighborFundamentalThreshold = strongestNeighborFundamentalAmplitude * (hasOctaveSupport ? 1.08f : 1.15f);
        const float neighborSalienceThreshold = strongestNeighborSalience * (hasOctaveSupport ? 1.08f : kBassRescueNeighborRatio);

        if (targetFundamentalAmplitude >= minimumFundamentalAmplitude &&
            targetFundamentalAmplitude >= neighborFundamentalThreshold &&
            targetSalience >= neighborSalienceThreshold)
        {
            rescued = true;
            break;
        }
    }

    if (!rescued)
        return selectedMidi;

    std::set<int> rescuedMidi = selectedMidi;
    rescuedMidi.insert(lowestExpectedMidi);
    return rescuedMidi;
}

void NativeDetectorEngine::buildLatestPacket_(double currentTime, const std::set<int>& currentActiveNotes)
{
    std::set<int> broadcastNotes;
    std::set<int> fastChordNotes;
    int broadcastId = 0;
    double broadcastOnsetTime = 0.0;
    double broadcastUntil = 0.0;
    std::string broadcastSource;
    double fastChordUntil = 0.0;
    {
        std::lock_guard<std::mutex> lock(stateMutex_);
        broadcastNotes = broadcastEventNotes_;
        broadcastId = broadcastEventId_;
        broadcastOnsetTime = broadcastEventOnsetTime_;
        broadcastUntil = broadcastEventUntil_;
        broadcastSource = broadcastEventSource_;
        fastChordNotes = fastChordActiveNotes_;
        fastChordUntil = fastChordActiveUntil_;
    }

    int eventIdToSend = 0;
    double eventAgeToSend = 0.0;
    std::set<int> eventNotesToSend;
    if (!broadcastNotes.empty() && currentTime <= broadcastUntil && broadcastId > 0)
    {
        eventIdToSend = broadcastId;
        eventAgeToSend = std::max(0.0, currentTime - broadcastOnsetTime);
        eventNotesToSend = broadcastNotes;
    }

    std::set<int> activeNotesToSend = currentActiveNotes;
    if (!fastChordNotes.empty() && currentTime <= fastChordUntil)
        activeNotesToSend.insert(fastChordNotes.begin(), fastChordNotes.end());

    std::ostringstream packet;
    packet << "A|" << JoinMidiNotes(activeNotesToSend)
        << "|" << eventIdToSend
        << "|" << eventAgeToSend
        << "|" << JoinMidiNotes(eventNotesToSend)
        << "|" << smoothedInputLevel_.load(std::memory_order_relaxed)
        << "|" << (broadcastSource.empty() ? std::string("--") : broadcastSource);

    std::lock_guard<std::mutex> lock(stateMutex_);
    latestPacket_ = packet.str();
}

std::mutex g_bridgeMutex;
std::shared_ptr<NativeDetectorEngine> g_detector;
}

extern "C"
{
ST_NATIVE_EXPORT int NativeDetector_Initialize(const char* modelPathUtf8, const char* dataDirectoryUtf8, const char*)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        g_detector = std::make_shared<NativeDetectorEngine>();

    std::wstring error;
    return g_detector->Initialize(Utf8ToWide(modelPathUtf8), Utf8ToWide(dataDirectoryUtf8), error) ? 1 : 0;
}

ST_NATIVE_EXPORT int NativeDetector_Start(int inputDeviceIndex)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return 0;

    std::wstring error;
    return g_detector->Start(inputDeviceIndex, kDetectorInputChannelInput1, error) ? 1 : 0;
}

ST_NATIVE_EXPORT int NativeDetector_StartWithInputChannel(int inputDeviceIndex, int inputChannelMode)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return 0;

    std::wstring error;
    return g_detector->Start(inputDeviceIndex, NormalizeDetectorInputChannelMode(inputChannelMode), error) ? 1 : 0;
}

ST_NATIVE_EXPORT int NativeDetector_StartSharedInput(
    int inputDeviceIndex,
    int sampleRate,
    int inputChannelCount,
    int inputChannelMode,
    int maxBlockFrames,
    const char* sourceLabelUtf8,
    const char* hostApiNameUtf8)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return 0;

    std::wstring error;
    return g_detector->StartSharedInput(
        inputDeviceIndex,
        sampleRate,
        inputChannelCount,
        NormalizeDetectorInputChannelMode(inputChannelMode),
        maxBlockFrames,
        sourceLabelUtf8 != nullptr ? sourceLabelUtf8 : "",
        hostApiNameUtf8 != nullptr ? hostApiNameUtf8 : "",
        error) ? 1 : 0;
}

ST_NATIVE_EXPORT int NativeDetector_SubmitSharedInputPcmFloat(
    const float* samples,
    int frameCount,
    int inputChannelCount,
    int sampleRate,
    int inputChannelMode)
{
    std::shared_ptr<NativeDetectorEngine> detector;
    {
        std::lock_guard<std::mutex> lock(g_bridgeMutex);
        detector = g_detector;
    }

    if (!detector)
        return 0;

    return detector->SubmitSharedInput(
        samples,
        frameCount,
        inputChannelCount,
        sampleRate,
        NormalizeDetectorInputChannelMode(inputChannelMode)) ? 1 : 0;
}

ST_NATIVE_EXPORT int NativeDetector_Stop()
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return 1;
    g_detector->Stop();
    return 1;
}

ST_NATIVE_EXPORT int NativeDetector_SetHintPayload(const char* payloadUtf8)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return 0;
    g_detector->SetHintPayload(payloadUtf8 != nullptr ? payloadUtf8 : "");
    return 1;
}

ST_NATIVE_EXPORT int NativeDetector_SetSettingsJson(const char* settingsJsonUtf8)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return 0;
    g_detector->ApplySettingsJson(settingsJsonUtf8 != nullptr ? settingsJsonUtf8 : "");
    return 1;
}

ST_NATIVE_EXPORT int NativeDetector_SetResamplerMode(int mode)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return 0;
    g_detector->SetResamplerMode(mode);
    return 1;
}

ST_NATIVE_EXPORT int NativeDetector_SetDebugLogPath(const char* debugLogPathUtf8)
{
    SetDebugLogPathInternal(Utf8ToWide(debugLogPathUtf8 != nullptr ? debugLogPathUtf8 : ""));
    AppendDebugLogLine("DEBUG_LOG_PATH_SET");
    return 1;
}

ST_NATIVE_EXPORT int NativeDetector_PollLatestPacket(char* destination, int capacity)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return CopyUtf8String("--", destination, capacity) ? 1 : 0;
    return CopyUtf8String(g_detector->PollLatestPacket(), destination, capacity) ? 1 : 0;
}

ST_NATIVE_EXPORT int NativeDetector_PollVerifierVerdictsJson(char* destination, int capacity)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return CopyUtf8String("{\"verdicts\":[]}", destination, capacity) ? 1 : 0;
    return CopyUtf8String(g_detector->PollVerifierVerdictsJson(), destination, capacity) ? 1 : 0;
}

ST_NATIVE_EXPORT int NativeDetector_GetStatus(char* destination, int capacity)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return CopyUtf8String("Native detector idle.", destination, capacity) ? 1 : 0;
    return CopyUtf8String(g_detector->GetStatusLine(), destination, capacity) ? 1 : 0;
}

ST_NATIVE_EXPORT int NativeDetector_GetLastError(char* destination, int capacity)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return CopyUtf8String("", destination, capacity) ? 1 : 0;
    return CopyUtf8String(WideToUtf8(g_detector->GetLastError()), destination, capacity) ? 1 : 0;
}

ST_NATIVE_EXPORT int NativeDetector_IsRunning()
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    return (g_detector && g_detector->IsRunning()) ? 1 : 0;
}

ST_NATIVE_EXPORT int NativeDetector_ListInputDevicesJson(char* destination, int capacity)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return CopyUtf8String("{\"preferredDeviceIndex\":-1,\"devices\":[]}", destination, capacity) ? 1 : 0;
    return CopyUtf8String(g_detector->ListInputDevicesJson(), destination, capacity) ? 1 : 0;
}

ST_NATIVE_EXPORT int NativeDetector_GetRuntimeInfoJson(char* destination, int capacity)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return CopyUtf8String("{\"running\":false,\"backendLabel\":\"Native C++ Detector\",\"selectedInputDeviceIndex\":-1,\"selectedInputDeviceDisplayName\":\"\",\"selectedHostApiName\":\"\",\"inputChannelMode\":\"Input 1\",\"sampleRate\":22050,\"captureSampleRate\":0,\"internalSampleRate\":22050,\"configuredResamplerMode\":\"Filtered\",\"activeResamplerMode\":\"Direct\",\"resamplerToggleAvailable\":false,\"hopSize\":512,\"captureSeconds\":0.3,\"inputLevelNormalized\":0,\"latestPacket\":\"--\",\"statusText\":\"Native detector idle.\",\"errorText\":\"\"}", destination, capacity) ? 1 : 0;
    return CopyUtf8String(g_detector->GetRuntimeInfoJson(), destination, capacity) ? 1 : 0;
}

ST_NATIVE_EXPORT int NativeDetector_DebugEvaluatePcmFloat(
    const float* samples,
    int sampleCount,
    const char* expectedNoteSpecsUtf8,
    const char* settingsJsonUtf8,
    char* destination,
    int capacity)
{
    if (destination == nullptr || capacity <= 0)
        return 0;

    const std::vector<ExpectedHintNoteSpec> expectedNotes = ParseExpectedHintNoteSpecsCsv(expectedNoteSpecsUtf8 != nullptr ? expectedNoteSpecsUtf8 : "");
    DetectorSettings settings = MakeTightDetectorSettings();
    if (settingsJsonUtf8 != nullptr && *settingsJsonUtf8 != '\0')
        settings = ParseDetectorSettingsJson(settingsJsonUtf8, settings);

    std::ostringstream json;
    json << "{\"sampleRate\":" << kSampleRate
        << ",\"hopSize\":" << kHopSize
        << ",\"revision\":2"
        << ",\"sampleCount\":" << std::max(0, sampleCount)
        << ",\"expectedCount\":" << expectedNotes.size();

    if (expectedNotes.empty() || samples == nullptr || sampleCount <= 0)
    {
        json << ",\"mode\":\"invalid\""
            << ",\"accepted\":false"
            << ",\"error\":\"Missing samples or expected notes.\"}";
        return CopyUtf8String(json.str(), destination, capacity) ? 1 : 0;
    }

    if (expectedNotes.size() >= 2)
    {
        int lowestExpectedMidi = std::numeric_limits<int>::max();
        for (const ExpectedHintNoteSpec& spec : expectedNotes)
        {
            if (spec.midi >= 0)
                lowestExpectedMidi = std::min(lowestExpectedMidi, spec.midi);
        }

        const int analysisWindowSamples = lowestExpectedMidi <= 40
            ? kFastChordAnalysisWindowLongSamples
            : kFastChordAnalysisWindowShortSamples;

        ConstraintChordEvaluationResult bestResult;
        int bestEndSampleExclusive = 0;
        int firstAcceptedEndSampleExclusive = -1;
        bool hasBest = false;
        std::vector<float> audioWindow;

        for (int endSampleExclusive = kHopSize; endSampleExclusive <= sampleCount; endSampleExclusive += kHopSize)
        {
            BuildRecentWindowFromSamples(samples, sampleCount, endSampleExclusive, analysisWindowSamples, audioWindow);
            const ConstraintChordEvaluationResult currentResult = EvaluateExpectedChordConstraintWindow(audioWindow, expectedNotes, settings);

            if (!hasBest ||
                (currentResult.accepted && !bestResult.accepted) ||
                (currentResult.accepted == bestResult.accepted && currentResult.hitCount > bestResult.hitCount))
            {
                bestResult = currentResult;
                bestEndSampleExclusive = endSampleExclusive;
                hasBest = true;
            }

            if (currentResult.accepted && firstAcceptedEndSampleExclusive < 0)
                firstAcceptedEndSampleExclusive = endSampleExclusive;
        }

        json << ",\"mode\":\"chord\""
            << ",\"accepted\":" << (bestResult.accepted ? "true" : "false")
            << ",\"analysisWindowSamples\":" << analysisWindowSamples
            << ",\"bestEndSampleExclusive\":" << bestEndSampleExclusive
            << ",\"bestEndTimeSeconds\":" << (static_cast<double>(bestEndSampleExclusive) / static_cast<double>(kSampleRate))
            << ",\"firstAcceptedEndSampleExclusive\":" << firstAcceptedEndSampleExclusive
            << ",\"firstAcceptedTimeSeconds\":";
        if (firstAcceptedEndSampleExclusive >= 0)
            json << (static_cast<double>(firstAcceptedEndSampleExclusive) / static_cast<double>(kSampleRate));
        else
            json << -1;

        json << ",\"hitCount\":" << bestResult.hitCount
            << ",\"totalExpected\":" << bestResult.totalExpected
            << ",\"requiredHits\":" << bestResult.requiredHits
            << ",\"chordLeniency\":" << bestResult.chordLeniency
            << ",\"notes\":[";

        for (size_t i = 0; i < bestResult.noteResults.size(); ++i)
        {
            const ConstraintChordNoteDebugResult& note = bestResult.noteResults[i];
            if (i > 0)
                json << ',';
            json << "{\"midi\":" << note.spec.midi
                << ",\"stringIndex\":" << note.spec.stringIndex
                << ",\"fret\":" << note.spec.fret
                << ",\"openMidi\":" << note.spec.openMidi
                << ",\"flags\":" << note.spec.flags
                << ",\"supportRatio\":" << note.supportRatio
                << ",\"supportThreshold\":" << note.supportThreshold
                << ",\"fundamentalRatio\":" << note.fundamentalRatio
                << ",\"neighborFundamentalMax\":" << note.neighborFundamentalMax
                << ",\"noteScore\":" << note.noteScore
                << ",\"noteScoreThreshold\":" << note.noteScoreThreshold
                << ",\"dominantPeakHz\":" << note.dominantPeakHz
                << ",\"centsError\":" << note.centsError
                << ",\"hit\":" << (note.hit ? "true" : "false")
                << "}";
        }

        json << "]}";
        return CopyUtf8String(json.str(), destination, capacity) ? 1 : 0;
    }

    const ExpectedHintNoteSpec& expectedNote = expectedNotes[0];
    const int primaryWindowSamples = GetFastSinglePrimaryAnalysisWindowSamples(expectedNote);
    const int fallbackWindowSamples = GetFastSingleFallbackAnalysisWindowSamples(expectedNote);
    const OfflineSingleEvaluationResult continuousResult = EvaluateOfflineSingleExpectedNote(samples, sampleCount, expectedNote.midi, settings);
    FastSingleWindowEvaluationResult bestPrimaryFastResult;
    FastSingleWindowEvaluationResult bestFallbackFastResult;
    bool hasPrimaryFastResult = false;
    bool hasFallbackFastResult = false;
    bool fastAccepted = false;
    int fastAcceptedEndSampleExclusive = -1;
    std::string fastAcceptedSource = "none";
    std::vector<float> audioWindow;

    for (int endSampleExclusive = kHopSize; endSampleExclusive <= sampleCount; endSampleExclusive += kHopSize)
    {
        BuildRecentWindowFromSamples(samples, sampleCount, endSampleExclusive, primaryWindowSamples, audioWindow);
        const FastSingleWindowEvaluationResult primaryResult = EvaluateFastSingleWindow(audioWindow, expectedNote, settings);
        if (!hasPrimaryFastResult || (primaryResult.accepted && !bestPrimaryFastResult.accepted))
        {
            bestPrimaryFastResult = primaryResult;
            hasPrimaryFastResult = true;
        }

        if (!fastAccepted && primaryResult.accepted)
        {
            fastAccepted = true;
            fastAcceptedEndSampleExclusive = endSampleExclusive;
            fastAcceptedSource = primaryResult.spectral.accepted ? "yin+spectral-support" : "yin";
        }

        if (fallbackWindowSamples > primaryWindowSamples)
        {
            BuildRecentWindowFromSamples(samples, sampleCount, endSampleExclusive, fallbackWindowSamples, audioWindow);
            const FastSingleWindowEvaluationResult fallbackResult = EvaluateFastSingleWindow(audioWindow, expectedNote, settings);
            if (!hasFallbackFastResult || (fallbackResult.accepted && !bestFallbackFastResult.accepted))
            {
                bestFallbackFastResult = fallbackResult;
                hasFallbackFastResult = true;
            }

            if (!fastAccepted && fallbackResult.accepted)
            {
                fastAccepted = true;
                fastAcceptedEndSampleExclusive = endSampleExclusive;
                fastAcceptedSource = fallbackResult.spectral.accepted ? "yin+spectral-support" : "yin";
            }
        }
    }

    const bool fastRejectedByContinuousConflict =
        continuousResult.detectedMidi >= 0 &&
        SemitoneDistance(continuousResult.detectedMidi, expectedNote.midi) > std::max(1, settings.expectMaxDistanceContinuous) &&
        fastAcceptedSource == "spectral";
    if (fastRejectedByContinuousConflict)
    {
        fastAccepted = false;
        fastAcceptedEndSampleExclusive = -1;
        fastAcceptedSource = "conflict";
    }

    json << ",\"mode\":\"single\""
        << ",\"accepted\":" << (fastAccepted ? "true" : "false")
        << ",\"expectedMidi\":" << expectedNote.midi
        << ",\"primaryWindowSamples\":" << primaryWindowSamples
        << ",\"fallbackWindowSamples\":" << fallbackWindowSamples
        << ",\"fastAcceptedEndSampleExclusive\":" << fastAcceptedEndSampleExclusive
        << ",\"fastAcceptedTimeSeconds\":";
    if (fastAcceptedEndSampleExclusive >= 0)
        json << (static_cast<double>(fastAcceptedEndSampleExclusive) / static_cast<double>(kSampleRate));
    else
        json << -1;
    json << ",\"fastAcceptedSource\":\"" << fastAcceptedSource << "\""
        << ",\"fastRejectedByContinuousConflict\":" << (fastRejectedByContinuousConflict ? "true" : "false")
        << ",\"continuousAccepted\":" << (continuousResult.accepted ? "true" : "false")
        << ",\"continuousDetectedMidi\":" << continuousResult.detectedMidi
        << ",\"acceptedHopIndex\":" << continuousResult.acceptedHopIndex
        << ",\"onsetHopIndex\":" << continuousResult.onsetHopIndex
        << ",\"highStringContext\":" << (continuousResult.highStringContext ? "true" : "false")
        << ",\"onsetDetected\":" << (continuousResult.onsetDetected ? "true" : "false")
        << ",\"lastMidiEstimate\":" << continuousResult.lastMidiEstimate
        << ",\"lastConfidence\":" << continuousResult.lastConfidence
        << ",\"bestConfidence\":" << continuousResult.bestConfidence
        << ",\"bestRms\":" << continuousResult.bestRms;
    if (hasPrimaryFastResult)
    {
        json << ",\"primaryFast\":{"
            << "\"accepted\":" << (bestPrimaryFastResult.accepted ? "true" : "false")
            << ",\"spectralAccepted\":" << (bestPrimaryFastResult.spectral.accepted ? "true" : "false")
            << ",\"yinAccepted\":" << (bestPrimaryFastResult.yin.accepted ? "true" : "false")
            << ",\"yinDetectedMidi\":" << bestPrimaryFastResult.yin.detectedMidi
            << ",\"yinAcceptedHopIndex\":" << bestPrimaryFastResult.yin.acceptedHopIndex
            << ",\"centsError\":" << bestPrimaryFastResult.spectral.noteResult.centsError
            << ",\"dominantPeakHz\":" << bestPrimaryFastResult.spectral.noteResult.dominantPeakHz
            << "}";
    }
    if (hasFallbackFastResult)
    {
        json << ",\"fallbackFast\":{"
            << "\"accepted\":" << (bestFallbackFastResult.accepted ? "true" : "false")
            << ",\"spectralAccepted\":" << (bestFallbackFastResult.spectral.accepted ? "true" : "false")
            << ",\"yinAccepted\":" << (bestFallbackFastResult.yin.accepted ? "true" : "false")
            << ",\"yinDetectedMidi\":" << bestFallbackFastResult.yin.detectedMidi
            << ",\"yinAcceptedHopIndex\":" << bestFallbackFastResult.yin.acceptedHopIndex
            << ",\"centsError\":" << bestFallbackFastResult.spectral.noteResult.centsError
            << ",\"dominantPeakHz\":" << bestFallbackFastResult.spectral.noteResult.dominantPeakHz
            << "}";
    }
    json
        << "}";
    return CopyUtf8String(json.str(), destination, capacity) ? 1 : 0;
}

ST_NATIVE_EXPORT void NativeDetector_Shutdown()
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return;
    g_detector->Shutdown();
    g_detector.reset();
}
}
