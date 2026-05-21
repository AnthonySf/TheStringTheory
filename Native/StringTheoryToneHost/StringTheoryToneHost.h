#pragma once

#ifdef _WIN32
#define ST_TONE_HOST_API __declspec(dllexport)
#else
#define ST_TONE_HOST_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

ST_TONE_HOST_API int st_get_api_version(void);

ST_TONE_HOST_API void* st_create_lv2_instance(
    const char* plugin_uri,
    const char* lv2_search_path,
    int sample_rate,
    int channels,
    int max_block_frames);

ST_TONE_HOST_API void* st_create_lv2_instance_legacy(
    const char* plugin_uri,
    int sample_rate,
    int channels,
    int max_block_frames);

ST_TONE_HOST_API void* st_create_nam_instance(
    const char* model_path,
    int sample_rate,
    int channels,
    int max_block_frames);

ST_TONE_HOST_API void st_destroy_instance(void* instance);
ST_TONE_HOST_API void st_reset_instance(void* instance);
ST_TONE_HOST_API void st_set_parameter(void* instance, const char* parameter_id, float value);
ST_TONE_HOST_API int st_process_interleaved(void* instance, float* interleaved_audio, int frames, int channels);

#ifdef __cplusplus
}
#endif
