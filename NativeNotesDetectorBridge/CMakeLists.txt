cmake_minimum_required(VERSION 3.20)
project(NativeNotesDetectorBridgeNative_v6 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)

add_library(NativeNotesDetectorBridgeNative_v6 SHARED
    NativeNotesDetectorBridge.cpp
    ../External/aubio/src/cvec.c
    ../External/aubio/src/fmat.c
    ../External/aubio/src/fvec.c
    ../External/aubio/src/lvec.c
    ../External/aubio/src/mathutils.c
    ../External/aubio/src/musicutils.c
    ../External/aubio/src/vecutils.c
    ../External/aubio/src/onset/onset.c
    ../External/aubio/src/onset/peakpicker.c
    ../External/aubio/src/pitch/pitch.c
    ../External/aubio/src/pitch/pitchfcomb.c
    ../External/aubio/src/pitch/pitchmcomb.c
    ../External/aubio/src/pitch/pitchschmitt.c
    ../External/aubio/src/pitch/pitchspecacf.c
    ../External/aubio/src/pitch/pitchyin.c
    ../External/aubio/src/pitch/pitchyinfast.c
    ../External/aubio/src/pitch/pitchyinfft.c
    ../External/aubio/src/spectral/awhitening.c
    ../External/aubio/src/spectral/fft.c
    ../External/aubio/src/spectral/ooura_fft8g.c
    ../External/aubio/src/spectral/phasevoc.c
    ../External/aubio/src/spectral/specdesc.c
    ../External/aubio/src/spectral/statistics.c
    ../External/aubio/src/temporal/biquad.c
    ../External/aubio/src/temporal/c_weighting.c
    ../External/aubio/src/temporal/filter.c
    ../External/aubio/src/utils/hist.c
    ../External/aubio/src/utils/log.c
    ../External/aubio/src/utils/scale.c
    ../External/libsamplerate/src/samplerate.c
    ../External/libsamplerate/src/src_linear.c
    ../External/libsamplerate/src/src_sinc.c
    ../External/libsamplerate/src/src_zoh.c
)

target_include_directories(NativeNotesDetectorBridgeNative_v6 PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/../External/aubio/src
    ${CMAKE_CURRENT_SOURCE_DIR}/../External/libsamplerate/include
)

target_compile_definitions(NativeNotesDetectorBridgeNative_v6 PRIVATE
    NATIVE_NOTES_DETECTOR_BRIDGE_EXPORTS
    HAVE_CONFIG_H
    ENABLE_SINC_FAST_CONVERTER
    ENABLE_SINC_MEDIUM_CONVERTER
    ENABLE_SINC_BEST_CONVERTER
)

if(APPLE)
    set_target_properties(NativeNotesDetectorBridgeNative_v6 PROPERTIES
        OUTPUT_NAME "NativeNotesDetectorBridgeNative_v6"
        PREFIX "lib"
        INSTALL_NAME_DIR "@loader_path"
        INSTALL_RPATH "@loader_path"
        BUILD_WITH_INSTALL_RPATH TRUE
    )
elseif(UNIX)
    target_link_libraries(NativeNotesDetectorBridgeNative_v6 PRIVATE dl pthread m)
endif()
