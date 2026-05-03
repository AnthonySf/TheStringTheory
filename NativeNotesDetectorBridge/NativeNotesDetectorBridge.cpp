#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <Windows.h>

#include <algorithm>
#include <array>
#include <atomic>
#include <chrono>
#include <cmath>
#include <complex>
#include <condition_variable>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <deque>
#include <filesystem>
#include <limits>
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

namespace
{
constexpr int kSampleRate = 22050;
constexpr int kHopSize = 512;
constexpr int kOnsetWindowSize = 1024;
constexpr int kPitchWindowSize = 2048;
constexpr int kCaptureSamples = 6615;
constexpr int kRingBufferSamples = kSampleRate * 8;
constexpr int kModelFftHop = 256;
constexpr int kModelOverlapFrames = 30;
constexpr int kModelInputSamples = 43844;
constexpr int kModelOutputFrames = 172;
constexpr int kModelOutputPitches = 88;
constexpr int kModelTrimFrames = kModelOverlapFrames / 2;
constexpr int kModelAnnotationsFps = 86;
constexpr float kDebounceSeconds = 0.05f;
constexpr float kEventBroadcastSeconds = 0.14f;
constexpr float kContinuousRmsGate = 0.007f;
constexpr float kContinuousHoldSeconds = 0.10f;
constexpr int kContinuousMedianWindow = 5;
constexpr int kContinuousMinMidi = 36;
constexpr int kContinuousMaxMidi = 88;
constexpr float kUnitySyncAlpha = 0.20f;
constexpr float kHintRetentionSeconds = 2.0f;
constexpr int kHighStringMinMidi = 64;
constexpr float kHighStringRmsMultiplier = 0.50f;
constexpr int kHighStringBenefitMatchMaxDistance = 0;
constexpr float kOnsetExpectLookaheadSeconds = 0.120f;
constexpr int kMaxEventNotes = 6;
constexpr float kChordResultMergeSeconds = 0.050f;
constexpr float kOnsetThreshold = 0.50f;
constexpr float kFrameThreshold = 0.30f;
constexpr int kMinimumNoteLengthFrames = 11;
constexpr int kMelodiaEnergyTolerance = 11;
constexpr float kPi = 3.14159265358979323846f;
constexpr float kYinThreshold = 0.18f;

constexpr unsigned long kPaFloat32 = 0x00000001UL;
constexpr unsigned long kPaNoFlag = 0UL;
constexpr int kPaNoError = 0;
constexpr int kPaNoDevice = -1;

const std::array<const char*, 12> kNoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

struct DetectorSettings
{
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

struct HintWindow
{
    double startTime = 0.0;
    double endTime = 0.0;
    std::set<int> midiNotes;
    Clock::time_point createdAt = Clock::now();
};

struct CaptureTask
{
    int eventId = 0;
    uint64_t startFrame = 0;
    uint64_t readyFrame = 0;
    double onsetTime = 0.0;
};

struct DeepResult
{
    int eventId = 0;
    double onsetTime = 0.0;
    std::set<int> eventNotes;
    std::set<int> expectedMidiNotes;
};

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

    const int required = MultiByteToWideChar(CP_UTF8, 0, text, -1, nullptr, 0);
    if (required <= 0)
        return std::wstring(); 

    std::wstring result(static_cast<size_t>(required) - 1, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, text, -1, result.data(), required);
    return result;
}

std::string WideToUtf8(const std::wstring& text)
{
    if (text.empty())
        return std::string();

    const int required = WideCharToMultiByte(CP_UTF8, 0, text.c_str(), -1, nullptr, 0, nullptr, nullptr);
    if (required <= 0)
        return std::string();

    std::string result(static_cast<size_t>(required) - 1, '\0');
    WideCharToMultiByte(CP_UTF8, 0, text.c_str(), -1, result.data(), required, nullptr, nullptr);
    return result;
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

template <typename T>
void EraseAt(std::deque<T>& items, size_t index)
{
    items.erase(items.begin() + static_cast<std::deque<T>::difference_type>(index));
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
    bool OpenInputStream(int deviceIndex, int sampleRate, unsigned long framesPerBuffer, double suggestedLatency, PaStreamCallback callback, void* userData, PaStream*& stream, std::wstring& error) const;
    void CloseStream(PaStream*& stream) const;

private:
    template <typename TFunction>
    void load_(TFunction& target, const char* name)
    {
        target = reinterpret_cast<TFunction>(GetProcAddress(dll_, name));
    }

    HMODULE dll_ = nullptr;

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

    std::filesystem::path dllPath = std::filesystem::path(pluginDirectory) / L"libportaudio64bit-asio.dll";
    dll_ = LoadLibraryW(dllPath.c_str());
    if (dll_ == nullptr)
    {
        error = L"Failed to load libportaudio64bit-asio.dll from the project plugins folder.";
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

    FreeLibrary(dll_);
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
    inputParameters.channelCount = 1;
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

    HMODULE dll_ = nullptr;
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

    const std::filesystem::path ortDllPath = std::filesystem::path(pluginDirectory) / L"onnxruntime.dll";
    dll_ = LoadLibraryW(ortDllPath.c_str());
    if (dll_ == nullptr)
    {
        error = L"Failed to load onnxruntime.dll from the project plugins folder.";
        return false;
    }

    const auto getApiBase = reinterpret_cast<const OrtApiBase* (ORT_API_CALL*)(void)>(GetProcAddress(dll_, "OrtGetApiBase"));
    appendCpuProvider_ = reinterpret_cast<OrtStatus* (ORT_API_CALL*)(OrtSessionOptions*, int)>(GetProcAddress(dll_, "OrtSessionOptionsAppendExecutionProvider_CPU"));
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

    if (!check_(api_->CreateSession(env_, modelPath.c_str(), sessionOptions_, &session_), error, L"CreateSession"))
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
        FreeLibrary(dll_);
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
    void AddHintWindow(double startTime, double endTime, const std::set<int>& midiNotes);
    std::set<int> GetExpectedNotesForPythonTime(double pythonAudioTime, double* unitySongTime);
    std::set<int> GetExpectedNotesNearPythonTime(double pythonAudioTime, double lookaheadSeconds, double* unitySongTime);
    void ParsePayload(const std::string& payload, double pythonAudioTime);
    void Prune();

private:
    static std::vector<std::string> split_(const std::string& text, char delimiter);
    static std::set<int> parseMidiSet_(const std::string& csv);
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
    else
    {
        offset_ = (1.0 - kUnitySyncAlpha) * offset_ + kUnitySyncAlpha * newOffset;
    }
    lastUnityTime_ = unitySongTime;
    lastPythonTimeAtSync_ = pythonAudioTime;
}

void HintState::AddHintWindow(double startTime, double endTime, const std::set<int>& midiNotes)
{
    if (midiNotes.empty())
        return;

    HintWindow window;
    window.startTime = std::min(startTime, endTime);
    window.endTime = std::max(startTime, endTime);
    window.midiNotes = midiNotes;
    window.createdAt = Clock::now();

    std::lock_guard<std::mutex> lock(mutex_);
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

    std::set<int> result;
    for (const HintWindow& window : windows_)
    {
        if (window.startTime <= unityTime && unityTime <= window.endTime)
            result.insert(window.midiNotes.begin(), window.midiNotes.end());
    }
    return result;
}

std::set<int> HintState::GetExpectedNotesNearPythonTime(double pythonAudioTime, double lookaheadSeconds, double* unitySongTime)
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

    std::set<int> result;
    const double futureUnityTime = unityTime + lookaheadSeconds;
    for (const HintWindow& window : windows_)
    {
        if (window.startTime <= unityTime && unityTime <= window.endTime)
            result.insert(window.midiNotes.begin(), window.midiNotes.end());
        else if (window.startTime <= futureUnityTime && futureUnityTime <= window.endTime)
            result.insert(window.midiNotes.begin(), window.midiNotes.end());
    }
    return result;
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

        if (parts.size() == 3)
        {
            AddHintWindow(currentSongTime - 0.07, currentSongTime + 0.22, parseMidiSet_(parts[2]));
            return;
        }

        for (size_t i = 2; i < parts.size(); ++i)
        {
            if (parts[i].empty())
                continue;

            std::vector<std::string> fields = split_(parts[i], ':');
            if (fields.size() >= 3)
            {
                try
                {
                    AddHintWindow(std::stod(fields[0]), std::stod(fields[1]), parseMidiSet_(fields[2]));
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

        if (parts.size() == 3)
        {
            AddHintWindow(currentSongTime - 0.07, currentSongTime + 0.22, parseMidiSet_(parts[2]));
            return;
        }

        for (size_t i = 2; i < parts.size(); ++i)
        {
            if (parts[i].empty())
                continue;

            std::vector<std::string> fields = split_(parts[i], ':');
            if (fields.size() >= 3)
            {
                try
                {
                    AddHintWindow(std::stod(fields[0]), std::stod(fields[1]), parseMidiSet_(fields[2]));
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
                const int midi = NoteNameToMidi(current);
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

    bool Initialize(const std::wstring& modelPath, std::wstring& error);
    bool Start(int inputDeviceIndex, std::wstring& error);
    void Stop();
    void Shutdown();
    void SetHintPayload(const std::string& payload);
    std::string PollLatestPacket() const;
    std::string GetStatusLine() const;
    std::wstring GetLastError() const;
    bool IsRunning() const;
    std::string ListInputDevicesJson() const;
    std::string GetRuntimeInfoJson() const;
    void ApplySettingsJson(const std::string& settingsJson);

private:
    static int __cdecl PortAudioCallback_(const void* input, void* output, unsigned long frameCount, const PaStreamCallbackTimeInfo*, PaStreamCallbackFlags, void* userData);
    int onAudio_(const float* input, int frameCount);
    void FastLoop_();
    void DeepLoop_();
    void pumpDeepResults_(double currentTime);
    void maybeDispatchCaptureTasks_(uint64_t availableFrames);
    bool initializeAubio_(std::wstring& error);
    void shutdownAubio_();
    void updateContinuousNotes_(const std::vector<float>& hop, double currentTime, std::deque<int>& recentPitchMidi, int& stableMidi, int& stableCount, double& lastContinuousTime, std::set<int>& currentActiveNotes);
    bool detectOnset_(const std::vector<float>& hop, const std::set<int>& expectedMidi);
    bool detectPitchYin_(const std::vector<float>& hop, float& midiOut, float& confidenceOut);
    std::vector<NoteEventCandidate> decodeBasicPitch_(const std::vector<float>& noteOutput, const std::vector<float>& onsetOutput) const;
    void inferOnsets_(std::vector<float>& onsets, const std::vector<float>& frames, int nFrames) const;
    std::vector<NoteEventCandidate> outputToNotesPolyphonic_(const std::vector<float>& frames, const std::vector<float>& onsets, int nFrames) const;
    std::set<int> scoreAiCandidates_(const std::vector<NoteEventCandidate>& candidates, const std::set<int>& expectedMidi) const;
    void buildLatestPacket_(double currentTime, const std::set<int>& currentActiveNotes);
    void readRecentWindow_(uint64_t endFrameExclusive, std::vector<float>& destination, int windowSize) const;
    void readRange_(uint64_t startFrame, std::vector<float>& destination, int count) const;
    void readAbsoluteRange_(uint64_t startFrame, float* destination, int count) const;
    double GetCurrentAudioTime() const;
    void stopLocked_();
    void resetStateLocked_();
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
    std::queue<std::pair<CaptureTask, std::vector<float>>> deepTasks_;
    std::queue<DeepResult> deepResults_;

    std::string latestPacket_;
    std::string statusLine_;
    std::wstring lastError_;
    DetectorSettings settings_ = MakeTightDetectorSettings();

    int broadcastEventId_ = 0;
    double broadcastEventOnsetTime_ = 0.0;
    double broadcastEventUntil_ = 0.0;
    std::set<int> broadcastEventNotes_;
    std::set<int> broadcastExpectedNotes_;
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

bool NativeDetectorEngine::Initialize(const std::wstring& modelPath, std::wstring& error)
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

bool NativeDetectorEngine::Start(int inputDeviceIndex, std::wstring& error)
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

    running_.store(true, std::memory_order_release);
    fastThread_ = std::thread(&NativeDetectorEngine::FastLoop_, this);
    deepThread_ = std::thread(&NativeDetectorEngine::DeepLoop_, this);

    const double suggestedLatency = selected != nullptr && selected->defaultLowInputLatency > 0.0
        ? selected->defaultLowInputLatency
        : 0.008;

    if (!portAudio_.OpenInputStream(
        selectedDeviceIndex_,
        kSampleRate,
        static_cast<unsigned long>(kHopSize),
        suggestedLatency,
        &NativeDetectorEngine::PortAudioCallback_,
        this,
        stream_,
        error))
    {
        running_.store(false, std::memory_order_release);
        dataCondition_.notify_all();
        deepCondition_.notify_all();
        if (fastThread_.joinable())
            fastThread_.join();
        if (deepThread_.joinable())
            deepThread_.join();
        setError_(error);
        return false;
    }

    updateStatusLocked_();
    error.clear();
    return true;
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
    hintState_.ParsePayload(payload, currentTime);
}

std::string NativeDetectorEngine::PollLatestPacket() const
{
    std::lock_guard<std::mutex> lock(stateMutex_);
    return latestPacket_;
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
    std::ostringstream builder;
    builder << "{"
        << "\"running\":" << (running_.load(std::memory_order_acquire) ? "true" : "false")
        << ",\"backendLabel\":\"Native C++ Detector\""
        << ",\"selectedInputDeviceIndex\":" << selectedDeviceIndex_
        << ",\"selectedInputDeviceDisplayName\":\"" << JsonEscape(selectedDeviceDisplayName_)
        << "\",\"selectedHostApiName\":\"" << JsonEscape(selectedHostApiName_)
        << "\",\"sampleRate\":" << kSampleRate
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

    const uint64_t startFrame = totalFramesWritten_.load(std::memory_order_relaxed);
    for (int i = 0; i < frameCount; ++i)
    {
        const float sample = input != nullptr ? input[i] : 0.0f;
        ringBuffer_[static_cast<size_t>((startFrame + static_cast<uint64_t>(i)) % ringBuffer_.size())] = sample;
    }

    const float rms = input != nullptr ? ComputeRms(input, frameCount) : 0.0f;
    const float previousLevel = smoothedInputLevel_.load(std::memory_order_relaxed);
    const float smoothed = std::clamp(previousLevel * 0.85f + rms * 7.5f * 0.15f, 0.0f, 1.0f);
    smoothedInputLevel_.store(smoothed, std::memory_order_relaxed);

    totalFramesWritten_.store(startFrame + static_cast<uint64_t>(frameCount), std::memory_order_release);
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
    running_.store(false, std::memory_order_release);
    dataCondition_.notify_all();
    deepCondition_.notify_all();

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
    }

    updateStatusLocked_();
}

void NativeDetectorEngine::resetStateLocked_()
{
    std::fill(ringBuffer_.begin(), ringBuffer_.end(), 0.0f);
    totalFramesWritten_.store(0, std::memory_order_release);
    smoothedInputLevel_.store(0.0f, std::memory_order_relaxed);
    {
        std::lock_guard<std::mutex> lock(stateMutex_);
        latestPacket_ = "--";
        broadcastEventNotes_.clear();
        broadcastEventId_ = 0;
        broadcastEventOnsetTime_ = 0.0;
        broadcastEventUntil_ = 0.0;
        broadcastExpectedNotes_.clear();
    }
}

void NativeDetectorEngine::updateStatusLocked_()
{
    std::ostringstream builder;
    if (running_.load(std::memory_order_acquire))
    {
        builder << "Running on " << (selectedDeviceDisplayName_.empty() ? "default input" : selectedDeviceDisplayName_)
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

            updateContinuousNotes_(hop, currentTime, recentPitchMidi, stableMidi, stableCount, lastContinuousTime, currentActiveNotes);

            const DetectorSettings settings = getSettingsSnapshot_();
            double onsetUnityTime = -1.0;
            const std::set<int> expectedForOnsetWindow = hintState_.GetExpectedNotesNearPythonTime(currentTime, settings.onsetExpectLookaheadSeconds, &onsetUnityTime);
            const bool onsetDetected = detectOnset_(hop, expectedForOnsetWindow);
            if (onsetDetected && (currentTime - lastOnsetTime) > kDebounceSeconds)
            {
                lastOnsetTime = currentTime;
                ++pluckCounter;

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
        const std::set<int> expectedMidi = hintState_.GetExpectedNotesForPythonTime(task.onsetTime, &unityTime);
        const std::set<int> selectedMidi = scoreAiCandidates_(decodedCandidates, expectedMidi);

        DeepResult result;
        result.eventId = task.eventId;
        result.onsetTime = task.onsetTime;
        result.eventNotes = selectedMidi;
        result.expectedMidiNotes = expectedMidi;

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
            }
            else
            {
                broadcastEventId_ = result.eventId;
                broadcastEventOnsetTime_ = result.onsetTime;
                broadcastEventUntil_ = currentTime + kEventBroadcastSeconds;
                broadcastEventNotes_ = result.eventNotes;
                broadcastExpectedNotes_ = result.expectedMidiNotes;
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

void NativeDetectorEngine::buildLatestPacket_(double currentTime, const std::set<int>& currentActiveNotes)
{
    std::set<int> broadcastNotes;
    int broadcastId = 0;
    double broadcastOnsetTime = 0.0;
    double broadcastUntil = 0.0;
    {
        std::lock_guard<std::mutex> lock(stateMutex_);
        broadcastNotes = broadcastEventNotes_;
        broadcastId = broadcastEventId_;
        broadcastOnsetTime = broadcastEventOnsetTime_;
        broadcastUntil = broadcastEventUntil_;
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

    std::ostringstream packet;
    packet << "A|" << JoinMidiNotes(currentActiveNotes)
        << "|" << eventIdToSend
        << "|" << eventAgeToSend
        << "|" << JoinMidiNotes(eventNotesToSend)
        << "|" << smoothedInputLevel_.load(std::memory_order_relaxed);

    std::lock_guard<std::mutex> lock(stateMutex_);
    latestPacket_ = packet.str();
}

std::mutex g_bridgeMutex;
std::unique_ptr<NativeDetectorEngine> g_detector;
}

extern "C"
{
__declspec(dllexport) int NativeDetector_Initialize(const char* modelPathUtf8, const char*, const char*)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        g_detector = std::make_unique<NativeDetectorEngine>();

    std::wstring error;
    return g_detector->Initialize(Utf8ToWide(modelPathUtf8), error) ? 1 : 0;
}

__declspec(dllexport) int NativeDetector_Start(int inputDeviceIndex)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return 0;

    std::wstring error;
    return g_detector->Start(inputDeviceIndex, error) ? 1 : 0;
}

__declspec(dllexport) int NativeDetector_Stop()
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return 1;
    g_detector->Stop();
    return 1;
}

__declspec(dllexport) int NativeDetector_SetHintPayload(const char* payloadUtf8)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return 0;
    g_detector->SetHintPayload(payloadUtf8 != nullptr ? payloadUtf8 : "");
    return 1;
}

__declspec(dllexport) int NativeDetector_SetSettingsJson(const char* settingsJsonUtf8)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return 0;
    g_detector->ApplySettingsJson(settingsJsonUtf8 != nullptr ? settingsJsonUtf8 : "");
    return 1;
}

__declspec(dllexport) int NativeDetector_PollLatestPacket(char* destination, int capacity)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return CopyUtf8String("--", destination, capacity) ? 1 : 0;
    return CopyUtf8String(g_detector->PollLatestPacket(), destination, capacity) ? 1 : 0;
}

__declspec(dllexport) int NativeDetector_GetStatus(char* destination, int capacity)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return CopyUtf8String("Native detector idle.", destination, capacity) ? 1 : 0;
    return CopyUtf8String(g_detector->GetStatusLine(), destination, capacity) ? 1 : 0;
}

__declspec(dllexport) int NativeDetector_GetLastError(char* destination, int capacity)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return CopyUtf8String("", destination, capacity) ? 1 : 0;
    return CopyUtf8String(WideToUtf8(g_detector->GetLastError()), destination, capacity) ? 1 : 0;
}

__declspec(dllexport) int NativeDetector_IsRunning()
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    return (g_detector && g_detector->IsRunning()) ? 1 : 0;
}

__declspec(dllexport) int NativeDetector_ListInputDevicesJson(char* destination, int capacity)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return CopyUtf8String("{\"preferredDeviceIndex\":-1,\"devices\":[]}", destination, capacity) ? 1 : 0;
    return CopyUtf8String(g_detector->ListInputDevicesJson(), destination, capacity) ? 1 : 0;
}

__declspec(dllexport) int NativeDetector_GetRuntimeInfoJson(char* destination, int capacity)
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return CopyUtf8String("{\"running\":false,\"backendLabel\":\"Native C++ Detector\",\"selectedInputDeviceIndex\":-1,\"selectedInputDeviceDisplayName\":\"\",\"selectedHostApiName\":\"\",\"sampleRate\":22050,\"hopSize\":512,\"captureSeconds\":0.3,\"inputLevelNormalized\":0,\"latestPacket\":\"--\",\"statusText\":\"Native detector idle.\",\"errorText\":\"\"}", destination, capacity) ? 1 : 0;
    return CopyUtf8String(g_detector->GetRuntimeInfoJson(), destination, capacity) ? 1 : 0;
}

__declspec(dllexport) void NativeDetector_Shutdown()
{
    std::lock_guard<std::mutex> lock(g_bridgeMutex);
    if (!g_detector)
        return;
    g_detector->Shutdown();
    g_detector.reset();
}
}
