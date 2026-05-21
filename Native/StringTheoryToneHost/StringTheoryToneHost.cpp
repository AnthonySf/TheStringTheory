#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include "StringTheoryToneHost.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstring>
#include <cstdarg>
#include <cstdio>
#include <condition_variable>
#include <filesystem>
#include <memory>
#include <mutex>
#include <new>
#include <queue>
#include <string>
#include <thread>
#include <unordered_map>
#include <unordered_set>
#include <vector>

#ifdef _WIN32
#include <Windows.h>
#endif

#include "NAM/get_dsp.h"

extern "C"
{
#include "lilv/lilv.h"
#include "lv2/atom/atom.h"
#include "lv2/buf-size/buf-size.h"
#include "lv2/core/lv2.h"
#include "lv2/log/log.h"
#include "lv2/options/options.h"
#include "lv2/parameters/parameters.h"
#include "lv2/urid/urid.h"
#include "lv2/worker/worker.h"
}

namespace
{
constexpr int kApiVersion = 3;
constexpr int kDefaultMaxBlockFrames = 2048;

float db_to_linear(float decibels)
{
    return std::pow(10.0f, decibels / 20.0f);
}

float clamp01(float value)
{
    return std::clamp(value, 0.0f, 1.0f);
}

bool equals_parameter(const char* parameter_id, const char* expected)
{
    return parameter_id != nullptr && expected != nullptr && std::strcmp(parameter_id, expected) == 0;
}

std::filesystem::path path_from_utf8(const char* value)
{
    if (value == nullptr || value[0] == '\0')
        return {};

#ifdef _WIN32
    int wideLength = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value, -1, nullptr, 0);
    if (wideLength <= 1)
        return std::filesystem::path(value);

    std::wstring wide(static_cast<size_t>(wideLength), L'\0');
    MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value, -1, wide.data(), wideLength);
    if (!wide.empty() && wide.back() == L'\0')
        wide.pop_back();
    return std::filesystem::path(wide);
#else
    return std::filesystem::path(value);
#endif
}

class ProcessorInstance
{
public:
    virtual ~ProcessorInstance() = default;
    virtual void reset() = 0;
    virtual void set_parameter(const char* parameter_id, float value) = 0;
    virtual bool process_interleaved(float* interleaved_audio, int frames, int channels) = 0;
};

std::string utf8_from_path(const std::filesystem::path& path)
{
#ifdef _WIN32
    std::wstring wide = path.wstring();
    if (wide.empty())
        return {};

    int length = WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), -1, nullptr, 0, nullptr, nullptr);
    if (length <= 1)
        return path.string();

    std::string result(static_cast<size_t>(length), '\0');
    WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), -1, result.data(), length, nullptr, nullptr);
    if (!result.empty() && result.back() == '\0')
        result.pop_back();
    return result;
#else
    return path.string();
#endif
}

std::string normalize_lv2_search_path(const char* value)
{
    if (value == nullptr || value[0] == '\0')
        return {};

    return value;
}

std::vector<std::filesystem::path> split_lv2_search_path(const std::string& search_path)
{
    std::vector<std::filesystem::path> roots;
    size_t start = 0;
    while (start <= search_path.size())
    {
        size_t end = search_path.find(';', start);
        std::string token = search_path.substr(start, end == std::string::npos ? std::string::npos : end - start);
        if (!token.empty())
        {
            std::filesystem::path path = path_from_utf8(token.c_str());
            if (!path.empty())
                roots.push_back(path);
        }

        if (end == std::string::npos)
            break;
        start = end + 1;
    }

    return roots;
}

class Lv2WorldContext
{
public:
    explicit Lv2WorldContext(std::string search_path)
        : searchPath(std::move(search_path)),
          world(lilv_world_new())
    {
        if (!world)
            return;

        audioPort = lilv_new_uri(world, LILV_URI_AUDIO_PORT);
        controlPort = lilv_new_uri(world, LILV_URI_CONTROL_PORT);
        inputPort = lilv_new_uri(world, LILV_URI_INPUT_PORT);
        outputPort = lilv_new_uri(world, LILV_URI_OUTPUT_PORT);
        atomPort = lilv_new_uri(world, LV2_ATOM__AtomPort);
        optionalConnection = lilv_new_uri(world, LV2_CORE__connectionOptional);

        load_bundles();
        plugins = lilv_world_get_all_plugins(world);
        valid = plugins != nullptr;
    }

    ~Lv2WorldContext()
    {
        lilv_node_free(optionalConnection);
        lilv_node_free(atomPort);
        lilv_node_free(outputPort);
        lilv_node_free(inputPort);
        lilv_node_free(controlPort);
        lilv_node_free(audioPort);
        lilv_world_free(world);
    }

    Lv2WorldContext(const Lv2WorldContext&) = delete;
    Lv2WorldContext& operator=(const Lv2WorldContext&) = delete;

    const std::string& get_search_path() const { return searchPath; }
    bool is_valid() const { return valid; }

    const LilvPlugin* find_plugin(const char* plugin_uri)
    {
        if (!valid || plugin_uri == nullptr || plugin_uri[0] == '\0')
            return nullptr;

        LilvNode* uri = lilv_new_uri(world, plugin_uri);
        if (!uri)
            return nullptr;

        const LilvPlugin* plugin = lilv_plugins_get_by_uri(plugins, uri);
        lilv_node_free(uri);
        return plugin;
    }

    LilvWorld* get_world() const { return world; }
    const LilvNode* get_audio_port_node() const { return audioPort; }
    const LilvNode* get_control_port_node() const { return controlPort; }
    const LilvNode* get_input_port_node() const { return inputPort; }
    const LilvNode* get_output_port_node() const { return outputPort; }
    const LilvNode* get_atom_port_node() const { return atomPort; }
    const LilvNode* get_optional_connection_node() const { return optionalConnection; }

    std::mutex instantiateMutex;

private:
    void load_bundles()
    {
        std::vector<std::filesystem::path> roots = split_lv2_search_path(searchPath);
        for (const std::filesystem::path& root : roots)
        {
            try
            {
                if (!std::filesystem::exists(root))
                    continue;

                if (std::filesystem::is_directory(root) && root.extension() == ".lv2")
                {
                    load_bundle(root);
                    continue;
                }

                if (!std::filesystem::is_directory(root))
                    continue;

                for (const auto& entry : std::filesystem::recursive_directory_iterator(root))
                {
                    if (entry.is_directory() && entry.path().extension() == ".lv2")
                        load_bundle(entry.path());
                }
            }
            catch (...)
            {
            }
        }
    }

    void load_bundle(const std::filesystem::path& bundle_path)
    {
        std::filesystem::path normalized = bundle_path;
        normalized += std::filesystem::path::preferred_separator;
        std::string path = utf8_from_path(normalized);
        if (path.empty())
            return;

        LilvNode* bundleUri = lilv_new_file_uri(world, nullptr, path.c_str());
        if (!bundleUri)
            return;

        lilv_world_load_bundle(world, bundleUri);
        lilv_node_free(bundleUri);
    }

    std::string searchPath;
    LilvWorld* world = nullptr;
    LilvNode* audioPort = nullptr;
    LilvNode* controlPort = nullptr;
    LilvNode* inputPort = nullptr;
    LilvNode* outputPort = nullptr;
    LilvNode* atomPort = nullptr;
    LilvNode* optionalConnection = nullptr;
    const LilvPlugins* plugins = nullptr;
    bool valid = false;
};

std::mutex g_lv2_context_mutex;
std::shared_ptr<Lv2WorldContext> g_lv2_context;

std::shared_ptr<Lv2WorldContext> get_lv2_context(const char* lv2_search_path)
{
    std::string normalized = normalize_lv2_search_path(lv2_search_path);
    std::lock_guard<std::mutex> lock(g_lv2_context_mutex);
    if (!g_lv2_context || g_lv2_context->get_search_path() != normalized)
        g_lv2_context = std::make_shared<Lv2WorldContext>(normalized);

    return g_lv2_context;
}

class Lv2UridMapper
{
public:
    Lv2UridMapper()
    {
        uriToId.reserve(64);
        idToUri.emplace_back();
    }

    LV2_URID map(const char* uri)
    {
        if (uri == nullptr || uri[0] == '\0')
            return 0;

        std::lock_guard<std::mutex> lock(mutex);
        auto existing = uriToId.find(uri);
        if (existing != uriToId.end())
            return existing->second;

        LV2_URID id = static_cast<LV2_URID>(idToUri.size());
        idToUri.emplace_back(uri);
        uriToId.emplace(idToUri.back(), id);
        return id;
    }

    const char* unmap(LV2_URID id)
    {
        std::lock_guard<std::mutex> lock(mutex);
        if (id == 0 || id >= idToUri.size())
            return nullptr;

        return idToUri[id].c_str();
    }

    static LV2_URID map_callback(LV2_URID_Map_Handle handle, const char* uri)
    {
        return handle ? static_cast<Lv2UridMapper*>(handle)->map(uri) : 0;
    }

    static const char* unmap_callback(LV2_URID_Unmap_Handle handle, LV2_URID urid)
    {
        return handle ? static_cast<Lv2UridMapper*>(handle)->unmap(urid) : nullptr;
    }

private:
    std::mutex mutex;
    std::unordered_map<std::string, LV2_URID> uriToId;
    std::vector<std::string> idToUri;
};

int lv2_log_vprintf_callback(LV2_Log_Handle, LV2_URID, const char* fmt, va_list args)
{
    if (!fmt)
        return 0;

#ifdef _WIN32
    return _vscprintf(fmt, args);
#else
    va_list copy;
    va_copy(copy, args);
    int result = std::vsnprintf(nullptr, 0, fmt, copy);
    va_end(copy);
    return result;
#endif
}

int lv2_log_printf_callback(LV2_Log_Handle handle, LV2_URID type, const char* fmt, ...)
{
    va_list args;
    va_start(args, fmt);
    int result = lv2_log_vprintf_callback(handle, type, fmt, args);
    va_end(args);
    return result;
}

bool feature_uri_supported(const char* uri)
{
    if (uri == nullptr)
        return false;

    static const std::unordered_set<std::string> supported = {
        LV2_URID__map,
        LV2_URID__unmap,
        LV2_LOG__log,
        LV2_OPTIONS__options,
        LV2_BUF_SIZE__boundedBlockLength,
        LV2_CORE__hardRTCapable,
        LV2_WORKER__schedule,
    };

    return supported.find(uri) != supported.end();
}

bool plugin_required_features_supported(const LilvPlugin* plugin)
{
    LilvNodes* requiredFeatures = lilv_plugin_get_required_features(plugin);
    if (!requiredFeatures)
        return true;

    bool supported = true;
    for (LilvIter* iter = lilv_nodes_begin(requiredFeatures);
         !lilv_nodes_is_end(requiredFeatures, iter);
         iter = lilv_nodes_next(requiredFeatures, iter))
    {
        const LilvNode* node = lilv_nodes_get(requiredFeatures, iter);
        if (!node || !lilv_node_is_uri(node) || !feature_uri_supported(lilv_node_as_uri(node)))
        {
            supported = false;
            break;
        }
    }

    lilv_nodes_free(requiredFeatures);
    return supported;
}

void lv2_instance_connect_port(LilvInstance* instance, uint32_t port_index, void* data_location)
{
    if (instance && instance->lv2_descriptor && instance->lv2_descriptor->connect_port)
        instance->lv2_descriptor->connect_port(instance->lv2_handle, port_index, data_location);
}

void lv2_instance_activate(LilvInstance* instance)
{
    if (instance && instance->lv2_descriptor && instance->lv2_descriptor->activate)
        instance->lv2_descriptor->activate(instance->lv2_handle);
}

void lv2_instance_run(LilvInstance* instance, uint32_t sample_count)
{
    if (instance && instance->lv2_descriptor && instance->lv2_descriptor->run)
        instance->lv2_descriptor->run(instance->lv2_handle, sample_count);
}

void lv2_instance_deactivate(LilvInstance* instance)
{
    if (instance && instance->lv2_descriptor && instance->lv2_descriptor->deactivate)
        instance->lv2_descriptor->deactivate(instance->lv2_handle);
}

struct Lv2ControlPort
{
    uint32_t portIndex = 0;
    std::string symbol;
    std::unique_ptr<std::atomic<float>> requestedValue;
    float processValue = 0.0f;
};

struct Lv2AtomPort
{
    uint32_t portIndex = 0;
    std::vector<uint8_t> buffer;
};

struct Lv2WorkerMessage
{
    std::vector<uint8_t> data;
};

class Lv2ProcessorInstance final : public ProcessorInstance
{
public:
    Lv2ProcessorInstance(
        std::shared_ptr<Lv2WorldContext> context,
        const LilvPlugin* plugin,
        int sample_rate,
        int channels,
        int max_block_frames)
        : worldContext(std::move(context)),
          plugin(plugin),
          sampleRate(std::max(1, sample_rate)),
          channelCount(std::max(1, channels)),
          maxBlockFrames(std::max(1, max_block_frames))
    {
        if (!worldContext || !worldContext->is_valid() || !plugin || !plugin_required_features_supported(plugin))
            return;

        mapFeatureData.handle = &uridMapper;
        mapFeatureData.map = &Lv2UridMapper::map_callback;
        unmapFeatureData.handle = &uridMapper;
        unmapFeatureData.unmap = &Lv2UridMapper::unmap_callback;
        logFeatureData.handle = nullptr;
        logFeatureData.printf = &lv2_log_printf_callback;
        logFeatureData.vprintf = &lv2_log_vprintf_callback;

        atomIntUrid = uridMapper.map(LV2_ATOM__Int);
        atomFloatUrid = uridMapper.map(LV2_ATOM__Float);
        maxBlockLengthUrid = uridMapper.map(LV2_BUF_SIZE__maxBlockLength);
        minBlockLengthUrid = uridMapper.map(LV2_BUF_SIZE__minBlockLength);
        nominalBlockLengthUrid = uridMapper.map(LV2_BUF_SIZE__nominalBlockLength);
        sampleRateUrid = uridMapper.map(LV2_PARAMETERS__sampleRate);

        maxBlockOptionValue = this->maxBlockFrames;
        minBlockOptionValue = 1;
        nominalBlockOptionValue = std::min(512, this->maxBlockFrames);
        sampleRateOptionValue = static_cast<float>(this->sampleRate);

        options[0] = {LV2_OPTIONS_INSTANCE, 0, maxBlockLengthUrid, sizeof(int32_t), atomIntUrid, &maxBlockOptionValue};
        options[1] = {LV2_OPTIONS_INSTANCE, 0, minBlockLengthUrid, sizeof(int32_t), atomIntUrid, &minBlockOptionValue};
        options[2] = {LV2_OPTIONS_INSTANCE, 0, nominalBlockLengthUrid, sizeof(int32_t), atomIntUrid, &nominalBlockOptionValue};
        options[3] = {LV2_OPTIONS_INSTANCE, 0, sampleRateUrid, sizeof(float), atomFloatUrid, &sampleRateOptionValue};
        options[4] = {LV2_OPTIONS_INSTANCE, 0, 0, 0, 0, nullptr};

        featureMap = {LV2_URID__map, &mapFeatureData};
        featureUnmap = {LV2_URID__unmap, &unmapFeatureData};
        featureLog = {LV2_LOG__log, &logFeatureData};
        featureOptions = {LV2_OPTIONS__options, options};
        featureBoundedBlock = {LV2_BUF_SIZE__boundedBlockLength, nullptr};
        workerScheduleData.handle = this;
        workerScheduleData.schedule_work = &Lv2ProcessorInstance::schedule_work_callback;
        featureWorkerSchedule = {LV2_WORKER__schedule, &workerScheduleData};
        features[0] = &featureMap;
        features[1] = &featureUnmap;
        features[2] = &featureLog;
        features[3] = &featureOptions;
        features[4] = &featureBoundedBlock;
        features[5] = &featureWorkerSchedule;
        features[6] = nullptr;

        if (!scan_ports())
            return;

        resize_buffers(this->maxBlockFrames);

        {
            std::lock_guard<std::mutex> lock(worldContext->instantiateMutex);
            instance = lilv_plugin_instantiate(plugin, static_cast<double>(this->sampleRate), features);
        }

        if (!instance)
            return;

        workerInterface = static_cast<const LV2_Worker_Interface*>(
            instance->lv2_descriptor->extension_data
                ? instance->lv2_descriptor->extension_data(LV2_WORKER__interface)
                : nullptr);
        if (workerInterface)
            start_worker_thread();

        connect_ports();
        lv2_instance_activate(instance);
        valid = true;
    }

    ~Lv2ProcessorInstance() override
    {
        stop_worker_thread();

        if (instance)
        {
            lv2_instance_deactivate(instance);
            lilv_instance_free(instance);
            instance = nullptr;
        }
    }

    bool is_valid() const { return valid; }

    void reset() override
    {
        if (!instance)
            return;

        lv2_instance_deactivate(instance);
        lv2_instance_activate(instance);
    }

    void set_parameter(const char* parameter_id, float value) override
    {
        if (parameter_id == nullptr || parameter_id[0] == '\0')
            return;

        auto found = controlBySymbol.find(parameter_id);
        if (found == controlBySymbol.end())
            return;

        controlPorts[found->second].requestedValue->store(value, std::memory_order_relaxed);
    }

    bool process_interleaved(float* interleaved_audio, int frames, int channels) override
    {
        if (!valid || !instance || interleaved_audio == nullptr || frames <= 0 || channels <= 0)
            return false;

        int processedFrames = 0;
        while (processedFrames < frames)
        {
            int chunkFrames = std::min(maxBlockFrames, frames - processedFrames);
            resize_buffers(chunkFrames);
            fill_audio_inputs(interleaved_audio, processedFrames, chunkFrames, channels);
            update_control_values();
            clear_audio_outputs(chunkFrames);
            reset_atom_ports(atomInputPorts);
            reset_atom_ports(atomOutputPorts);
            deliver_worker_responses();

            try
            {
                lv2_instance_run(instance, static_cast<uint32_t>(chunkFrames));
                if (workerInterface && workerInterface->end_run)
                    workerInterface->end_run(instance->lv2_handle);
            }
            catch (...)
            {
                return false;
            }

            write_audio_outputs(interleaved_audio, processedFrames, chunkFrames, channels);
            processedFrames += chunkFrames;
        }

        return true;
    }

private:
    bool scan_ports()
    {
        uint32_t portCount = lilv_plugin_get_num_ports(plugin);
        for (uint32_t index = 0; index < portCount; ++index)
        {
            const LilvPort* port = lilv_plugin_get_port_by_index(plugin, index);
            if (!port)
                continue;

            bool isAudio = lilv_port_is_a(plugin, port, worldContext->get_audio_port_node());
            bool isControl = lilv_port_is_a(plugin, port, worldContext->get_control_port_node());
            bool isAtom = lilv_port_is_a(plugin, port, worldContext->get_atom_port_node());
            bool isInput = lilv_port_is_a(plugin, port, worldContext->get_input_port_node());
            bool isOutput = lilv_port_is_a(plugin, port, worldContext->get_output_port_node());
            bool optional = lilv_port_has_property(plugin, port, worldContext->get_optional_connection_node());

            if (isAudio && isInput)
            {
                audioInputPorts.push_back(index);
                continue;
            }

            if (isAudio && isOutput)
            {
                audioOutputPorts.push_back(index);
                continue;
            }

            if (isControl && isInput)
            {
                const LilvNode* symbolNode = lilv_port_get_symbol(plugin, port);
                if (!symbolNode)
                    return false;

                float defaultValue = 0.0f;
                LilvNode* def = nullptr;
                LilvNode* min = nullptr;
                LilvNode* max = nullptr;
                lilv_port_get_range(plugin, port, &def, &min, &max);
                if (def && (lilv_node_is_float(def) || lilv_node_is_int(def)))
                    defaultValue = lilv_node_as_float(def);
                lilv_node_free(def);
                lilv_node_free(min);
                lilv_node_free(max);

                Lv2ControlPort control;
                control.portIndex = index;
                control.symbol = lilv_node_as_string(symbolNode);
                control.requestedValue = std::make_unique<std::atomic<float>>(defaultValue);
                control.processValue = defaultValue;
                controlBySymbol[control.symbol] = controlPorts.size();
                controlPorts.push_back(std::move(control));
                continue;
            }

            if (isControl && isOutput)
            {
                outputControlPorts.push_back(index);
                outputControlValues.push_back(0.0f);
                continue;
            }

            if (isAtom && isInput)
            {
                Lv2AtomPort atomPort;
                atomPort.portIndex = index;
                atomPort.buffer.resize(kAtomSequenceBufferBytes, 0);
                atomInputPorts.push_back(std::move(atomPort));
                continue;
            }

            if (isAtom && isOutput)
            {
                Lv2AtomPort atomPort;
                atomPort.portIndex = index;
                atomPort.buffer.resize(kAtomSequenceBufferBytes, 0);
                atomOutputPorts.push_back(std::move(atomPort));
                continue;
            }

            if (!optional)
                return false;
        }

        return !audioInputPorts.empty() && !audioOutputPorts.empty();
    }

    void resize_buffers(int frames)
    {
        int safeFrames = std::max(1, frames);
        if (static_cast<int>(audioInputBuffers.size()) != static_cast<int>(audioInputPorts.size()))
            audioInputBuffers.resize(audioInputPorts.size());
        if (static_cast<int>(audioOutputBuffers.size()) != static_cast<int>(audioOutputPorts.size()))
            audioOutputBuffers.resize(audioOutputPorts.size());

        for (std::vector<float>& buffer : audioInputBuffers)
        {
            if (static_cast<int>(buffer.size()) < safeFrames)
                buffer.assign(static_cast<size_t>(safeFrames), 0.0f);
        }

        for (std::vector<float>& buffer : audioOutputBuffers)
        {
            if (static_cast<int>(buffer.size()) < safeFrames)
                buffer.assign(static_cast<size_t>(safeFrames), 0.0f);
        }
    }

    void connect_ports()
    {
        for (size_t i = 0; i < audioInputPorts.size(); ++i)
            lv2_instance_connect_port(instance, audioInputPorts[i], audioInputBuffers[i].data());

        for (size_t i = 0; i < audioOutputPorts.size(); ++i)
            lv2_instance_connect_port(instance, audioOutputPorts[i], audioOutputBuffers[i].data());

        for (Lv2ControlPort& control : controlPorts)
            lv2_instance_connect_port(instance, control.portIndex, &control.processValue);

        for (size_t i = 0; i < outputControlPorts.size(); ++i)
            lv2_instance_connect_port(instance, outputControlPorts[i], &outputControlValues[i]);

        reset_atom_ports(atomInputPorts);
        reset_atom_ports(atomOutputPorts);

        for (Lv2AtomPort& atomPort : atomInputPorts)
            lv2_instance_connect_port(instance, atomPort.portIndex, atomPort.buffer.data());

        for (Lv2AtomPort& atomPort : atomOutputPorts)
            lv2_instance_connect_port(instance, atomPort.portIndex, atomPort.buffer.data());
    }

    void fill_audio_inputs(const float* interleaved_audio, int frame_offset, int frames, int source_channels)
    {
        if (audioInputBuffers.size() == 1)
        {
            float* mono = audioInputBuffers[0].data();
            for (int frame = 0; frame < frames; ++frame)
            {
                int sourceIndex = (frame_offset + frame) * source_channels;
                float sum = 0.0f;
                for (int channel = 0; channel < source_channels; ++channel)
                    sum += interleaved_audio[sourceIndex + channel];
                mono[frame] = sum / static_cast<float>(source_channels);
            }
            return;
        }

        for (size_t input = 0; input < audioInputBuffers.size(); ++input)
        {
            float* destination = audioInputBuffers[input].data();
            int sourceChannel = std::min(static_cast<int>(input), source_channels - 1);
            for (int frame = 0; frame < frames; ++frame)
            {
                int sourceIndex = ((frame_offset + frame) * source_channels) + sourceChannel;
                destination[frame] = interleaved_audio[sourceIndex];
            }
        }
    }

    void update_control_values()
    {
        for (Lv2ControlPort& control : controlPorts)
            control.processValue = control.requestedValue->load(std::memory_order_relaxed);
    }

    void clear_audio_outputs(int frames)
    {
        for (std::vector<float>& buffer : audioOutputBuffers)
            std::fill(buffer.begin(), buffer.begin() + frames, 0.0f);
    }

    void write_audio_outputs(float* interleaved_audio, int frame_offset, int frames, int destination_channels)
    {
        if (audioOutputBuffers.size() == 1)
        {
            const float* mono = audioOutputBuffers[0].data();
            for (int frame = 0; frame < frames; ++frame)
            {
                int destinationIndex = (frame_offset + frame) * destination_channels;
                for (int channel = 0; channel < destination_channels; ++channel)
                    interleaved_audio[destinationIndex + channel] = mono[frame];
            }
            return;
        }

        for (int frame = 0; frame < frames; ++frame)
        {
            int destinationIndex = (frame_offset + frame) * destination_channels;
            for (int channel = 0; channel < destination_channels; ++channel)
            {
                int outputChannel = std::min(channel, static_cast<int>(audioOutputBuffers.size()) - 1);
                interleaved_audio[destinationIndex + channel] = audioOutputBuffers[outputChannel][frame];
            }
        }
    }

    static LV2_Worker_Status schedule_work_callback(
        LV2_Worker_Schedule_Handle handle,
        uint32_t size,
        const void* data)
    {
        Lv2ProcessorInstance* self = static_cast<Lv2ProcessorInstance*>(handle);
        return self ? self->schedule_work(size, data) : LV2_WORKER_ERR_UNKNOWN;
    }

    static LV2_Worker_Status worker_respond_callback(
        LV2_Worker_Respond_Handle handle,
        uint32_t size,
        const void* data)
    {
        Lv2ProcessorInstance* self = static_cast<Lv2ProcessorInstance*>(handle);
        return self ? self->enqueue_worker_response(size, data) : LV2_WORKER_ERR_UNKNOWN;
    }

    LV2_Worker_Status schedule_work(uint32_t size, const void* data)
    {
        if (!workerInterface || !workerInterface->work || !data)
            return LV2_WORKER_ERR_UNKNOWN;

        Lv2WorkerMessage message;
        const uint8_t* bytes = static_cast<const uint8_t*>(data);
        message.data.assign(bytes, bytes + size);

        {
            std::lock_guard<std::mutex> lock(workerMutex);
            if (stopWorker)
                return LV2_WORKER_ERR_UNKNOWN;
            workerQueue.push(std::move(message));
        }

        workerCondition.notify_one();
        return LV2_WORKER_SUCCESS;
    }

    LV2_Worker_Status enqueue_worker_response(uint32_t size, const void* data)
    {
        if (!data)
            return LV2_WORKER_ERR_UNKNOWN;

        Lv2WorkerMessage message;
        const uint8_t* bytes = static_cast<const uint8_t*>(data);
        message.data.assign(bytes, bytes + size);

        std::lock_guard<std::mutex> lock(workerResponseMutex);
        workerResponses.push(std::move(message));
        return LV2_WORKER_SUCCESS;
    }

    void start_worker_thread()
    {
        workerThread = std::thread([this] { worker_thread_loop(); });
    }

    void stop_worker_thread()
    {
        {
            std::lock_guard<std::mutex> lock(workerMutex);
            stopWorker = true;
        }
        workerCondition.notify_all();
        if (workerThread.joinable())
            workerThread.join();
    }

    void worker_thread_loop()
    {
        for (;;)
        {
            Lv2WorkerMessage message;
            {
                std::unique_lock<std::mutex> lock(workerMutex);
                workerCondition.wait(lock, [this] { return stopWorker || !workerQueue.empty(); });
                if (stopWorker && workerQueue.empty())
                    return;

                message = std::move(workerQueue.front());
                workerQueue.pop();
            }

            if (workerInterface && workerInterface->work && instance)
            {
                workerInterface->work(
                    instance->lv2_handle,
                    &Lv2ProcessorInstance::worker_respond_callback,
                    this,
                    static_cast<uint32_t>(message.data.size()),
                    message.data.data());
            }
        }
    }

    void deliver_worker_responses()
    {
        if (!workerInterface || !workerInterface->work_response || !instance)
            return;

        for (;;)
        {
            Lv2WorkerMessage message;
            {
                std::lock_guard<std::mutex> lock(workerResponseMutex);
                if (workerResponses.empty())
                    break;

                message = std::move(workerResponses.front());
                workerResponses.pop();
            }

            workerInterface->work_response(
                instance->lv2_handle,
                static_cast<uint32_t>(message.data.size()),
                message.data.data());
        }
    }

    void reset_atom_ports(std::vector<Lv2AtomPort>& atomPorts)
    {
        LV2_URID sequenceType = uridMapper.map(LV2_ATOM__Sequence);
        for (Lv2AtomPort& atomPort : atomPorts)
        {
            if (atomPort.buffer.size() < sizeof(LV2_Atom_Sequence))
                atomPort.buffer.resize(kAtomSequenceBufferBytes, 0);

            std::fill(atomPort.buffer.begin(), atomPort.buffer.end(), 0);
            LV2_Atom_Sequence* sequence = reinterpret_cast<LV2_Atom_Sequence*>(atomPort.buffer.data());
            sequence->atom.size = sizeof(LV2_Atom_Sequence_Body);
            sequence->atom.type = sequenceType;
            sequence->body.unit = 0;
            sequence->body.pad = 0;
        }
    }

    std::shared_ptr<Lv2WorldContext> worldContext;
    const LilvPlugin* plugin = nullptr;
    LilvInstance* instance = nullptr;
    int sampleRate;
    int channelCount;
    int maxBlockFrames;
    bool valid = false;

    Lv2UridMapper uridMapper;
    LV2_URID_Map mapFeatureData {};
    LV2_URID_Unmap unmapFeatureData {};
    LV2_Log_Log logFeatureData {};
    LV2_Feature featureMap {};
    LV2_Feature featureUnmap {};
    LV2_Feature featureLog {};
    LV2_Feature featureOptions {};
    LV2_Feature featureBoundedBlock {};
    LV2_Feature featureWorkerSchedule {};
    const LV2_Feature* features[7] {};
    LV2_Options_Option options[5] {};
    LV2_Worker_Schedule workerScheduleData {};
    const LV2_Worker_Interface* workerInterface = nullptr;
    static constexpr size_t kAtomSequenceBufferBytes = 8192;
    LV2_URID atomIntUrid = 0;
    LV2_URID atomFloatUrid = 0;
    LV2_URID maxBlockLengthUrid = 0;
    LV2_URID minBlockLengthUrid = 0;
    LV2_URID nominalBlockLengthUrid = 0;
    LV2_URID sampleRateUrid = 0;
    int32_t maxBlockOptionValue = 0;
    int32_t minBlockOptionValue = 0;
    int32_t nominalBlockOptionValue = 0;
    float sampleRateOptionValue = 0.0f;

    std::vector<uint32_t> audioInputPorts;
    std::vector<uint32_t> audioOutputPorts;
    std::vector<std::vector<float>> audioInputBuffers;
    std::vector<std::vector<float>> audioOutputBuffers;
    std::vector<Lv2ControlPort> controlPorts;
    std::unordered_map<std::string, size_t> controlBySymbol;
    std::vector<uint32_t> outputControlPorts;
    std::vector<float> outputControlValues;
    std::vector<Lv2AtomPort> atomInputPorts;
    std::vector<Lv2AtomPort> atomOutputPorts;
    std::thread workerThread;
    std::mutex workerMutex;
    std::condition_variable workerCondition;
    std::queue<Lv2WorkerMessage> workerQueue;
    bool stopWorker = false;
    std::mutex workerResponseMutex;
    std::queue<Lv2WorkerMessage> workerResponses;
};

class NamProcessorInstance final : public ProcessorInstance
{
public:
    NamProcessorInstance(std::unique_ptr<nam::DSP> dsp_model, int sample_rate, int channels, int max_block_frames)
        : model(std::move(dsp_model)),
          sampleRate(std::max(1, sample_rate)),
          channelCount(std::max(1, channels)),
          maxBlockFrames(std::max(1, max_block_frames))
    {
        inputBuffers.resize(std::max(1, model->NumInputChannels()));
        outputBuffers.resize(std::max(1, model->NumOutputChannels()));
        inputPtrs.resize(inputBuffers.size());
        outputPtrs.resize(outputBuffers.size());
        resize_buffers(this->maxBlockFrames);
        reset();
    }

    void reset() override
    {
        if (model)
            model->ResetAndPrewarm(static_cast<double>(sampleRate), maxBlockFrames);
    }

    void set_parameter(const char* parameter_id, float value) override
    {
        if (equals_parameter(parameter_id, "input_trim_db"))
            inputTrimDb.store(value, std::memory_order_relaxed);
        else if (equals_parameter(parameter_id, "output_trim_db"))
            outputTrimDb.store(value, std::memory_order_relaxed);
        else if (equals_parameter(parameter_id, "mix"))
            mix.store(clamp01(value), std::memory_order_relaxed);
    }

    bool process_interleaved(float* interleaved_audio, int frames, int channels) override
    {
        if (!model || interleaved_audio == nullptr || frames <= 0 || channels <= 0)
            return false;

        int processedFrames = 0;
        while (processedFrames < frames)
        {
            int chunkFrames = std::min(maxBlockFrames, frames - processedFrames);
            resize_buffers(chunkFrames);
            fill_input(interleaved_audio, processedFrames, chunkFrames, channels);

            try
            {
                model->process(inputPtrs.data(), outputPtrs.data(), chunkFrames);
            }
            catch (...)
            {
                return false;
            }

            write_output(interleaved_audio, processedFrames, chunkFrames, channels);
            processedFrames += chunkFrames;
        }

        return true;
    }

private:
    void resize_buffers(int frames)
    {
        int safeFrames = std::max(1, frames);
        for (size_t i = 0; i < inputBuffers.size(); ++i)
        {
            if (static_cast<int>(inputBuffers[i].size()) < safeFrames)
                inputBuffers[i].assign(static_cast<size_t>(safeFrames), 0.0);
            inputPtrs[i] = inputBuffers[i].data();
        }

        for (size_t i = 0; i < outputBuffers.size(); ++i)
        {
            if (static_cast<int>(outputBuffers[i].size()) < safeFrames)
                outputBuffers[i].assign(static_cast<size_t>(safeFrames), 0.0);
            else
                std::fill(outputBuffers[i].begin(), outputBuffers[i].begin() + safeFrames, 0.0);
            outputPtrs[i] = outputBuffers[i].data();
        }
    }

    void fill_input(const float* interleaved_audio, int frame_offset, int frames, int source_channels)
    {
        float inputGain = db_to_linear(inputTrimDb.load(std::memory_order_relaxed));
        if (inputBuffers.size() == 1)
        {
            NAM_SAMPLE* mono = inputBuffers[0].data();
            for (int frame = 0; frame < frames; ++frame)
            {
                int sourceIndex = (frame_offset + frame) * source_channels;
                float sum = 0.0f;
                for (int channel = 0; channel < source_channels; ++channel)
                    sum += interleaved_audio[sourceIndex + channel];
                mono[frame] = static_cast<NAM_SAMPLE>((sum / static_cast<float>(source_channels)) * inputGain);
            }
            return;
        }

        for (size_t input = 0; input < inputBuffers.size(); ++input)
        {
            NAM_SAMPLE* destination = inputBuffers[input].data();
            int sourceChannel = std::min(static_cast<int>(input), source_channels - 1);
            for (int frame = 0; frame < frames; ++frame)
            {
                int sourceIndex = ((frame_offset + frame) * source_channels) + sourceChannel;
                destination[frame] = static_cast<NAM_SAMPLE>(interleaved_audio[sourceIndex] * inputGain);
            }
        }
    }

    void write_output(float* interleaved_audio, int frame_offset, int frames, int destination_channels)
    {
        float outputGain = db_to_linear(outputTrimDb.load(std::memory_order_relaxed));
        float wetMix = clamp01(mix.load(std::memory_order_relaxed));
        float dryMix = 1.0f - wetMix;

        if (outputBuffers.size() == 1)
        {
            const NAM_SAMPLE* mono = outputBuffers[0].data();
            for (int frame = 0; frame < frames; ++frame)
            {
                float wet = static_cast<float>(mono[frame]) * outputGain;
                int destinationIndex = (frame_offset + frame) * destination_channels;
                for (int channel = 0; channel < destination_channels; ++channel)
                {
                    float dry = interleaved_audio[destinationIndex + channel];
                    interleaved_audio[destinationIndex + channel] = (dry * dryMix) + (wet * wetMix);
                }
            }
            return;
        }

        for (int frame = 0; frame < frames; ++frame)
        {
            int destinationIndex = (frame_offset + frame) * destination_channels;
            for (int channel = 0; channel < destination_channels; ++channel)
            {
                int outputChannel = std::min(channel, static_cast<int>(outputBuffers.size()) - 1);
                float wet = static_cast<float>(outputBuffers[outputChannel][frame]) * outputGain;
                float dry = interleaved_audio[destinationIndex + channel];
                interleaved_audio[destinationIndex + channel] = (dry * dryMix) + (wet * wetMix);
            }
        }
    }

    std::unique_ptr<nam::DSP> model;
    int sampleRate;
    int channelCount;
    int maxBlockFrames;
    std::vector<std::vector<NAM_SAMPLE>> inputBuffers;
    std::vector<std::vector<NAM_SAMPLE>> outputBuffers;
    std::vector<NAM_SAMPLE*> inputPtrs;
    std::vector<NAM_SAMPLE*> outputPtrs;
    std::atomic<float> inputTrimDb { 0.0f };
    std::atomic<float> outputTrimDb { 0.0f };
    std::atomic<float> mix { 1.0f };
};

} // namespace

extern "C"
{
ST_TONE_HOST_API int st_get_api_version(void)
{
    return kApiVersion;
}

ST_TONE_HOST_API void* st_create_lv2_instance(
    const char* plugin_uri,
    const char* lv2_search_path,
    int sample_rate,
    int channels,
    int max_block_frames)
{
    if (plugin_uri == nullptr || plugin_uri[0] == '\0')
        return nullptr;

    try
    {
        std::shared_ptr<Lv2WorldContext> context = get_lv2_context(lv2_search_path);
        if (!context || !context->is_valid())
            return nullptr;

        const LilvPlugin* plugin = context->find_plugin(plugin_uri);
        if (!plugin)
            return nullptr;

        int safeMaxBlockFrames = max_block_frames > 0 ? max_block_frames : kDefaultMaxBlockFrames;
        std::unique_ptr<Lv2ProcessorInstance> processor =
            std::make_unique<Lv2ProcessorInstance>(context, plugin, sample_rate, channels, safeMaxBlockFrames);
        if (!processor->is_valid())
            return nullptr;

        return processor.release();
    }
    catch (...)
    {
        return nullptr;
    }
}

ST_TONE_HOST_API void* st_create_lv2_instance_legacy(
    const char* plugin_uri,
    int sample_rate,
    int channels,
    int max_block_frames)
{
    return st_create_lv2_instance(plugin_uri, nullptr, sample_rate, channels, max_block_frames);
}

ST_TONE_HOST_API void* st_create_nam_instance(
    const char* model_path,
    int sample_rate,
    int channels,
    int max_block_frames)
{
    if (model_path == nullptr || model_path[0] == '\0')
        return nullptr;

    try
    {
        std::filesystem::path path = path_from_utf8(model_path);
        if (!std::filesystem::exists(path) || !std::filesystem::is_regular_file(path))
            return nullptr;

        std::unique_ptr<nam::DSP> model = nam::get_dsp(path);
        if (!model)
            return nullptr;

        int safeMaxBlockFrames = max_block_frames > 0 ? max_block_frames : kDefaultMaxBlockFrames;
        return new NamProcessorInstance(std::move(model), sample_rate, channels, safeMaxBlockFrames);
    }
    catch (...)
    {
        return nullptr;
    }
}

ST_TONE_HOST_API void st_destroy_instance(void* instance)
{
    delete static_cast<ProcessorInstance*>(instance);
}

ST_TONE_HOST_API void st_reset_instance(void* instance)
{
    if (instance != nullptr)
        static_cast<ProcessorInstance*>(instance)->reset();
}

ST_TONE_HOST_API void st_set_parameter(void* instance, const char* parameter_id, float value)
{
    if (instance != nullptr)
        static_cast<ProcessorInstance*>(instance)->set_parameter(parameter_id, value);
}

ST_TONE_HOST_API int st_process_interleaved(void* instance, float* interleaved_audio, int frames, int channels)
{
    if (instance == nullptr)
        return 0;

    return static_cast<ProcessorInstance*>(instance)->process_interleaved(interleaved_audio, frames, channels) ? 1 : 0;
}
}
