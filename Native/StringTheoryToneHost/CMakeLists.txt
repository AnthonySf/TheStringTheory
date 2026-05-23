cmake_minimum_required(VERSION 3.20)
project(StringTheoryToneHost LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_C_STANDARD 17)
set(CMAKE_C_STANDARD_REQUIRED ON)

set(NAM_ROOT ${CMAKE_CURRENT_SOURCE_DIR}/../ThirdParty/NeuralAmpModelerCore)
set(LV2_ROOT ${CMAKE_CURRENT_SOURCE_DIR}/../ThirdParty/LV2Stack)

set(ZIX_PLATFORM_SOURCES
    ${LV2_ROOT}/zix/src/posix/environment_posix.c
    ${LV2_ROOT}/zix/src/posix/filesystem_posix.c
    ${LV2_ROOT}/zix/src/posix/system_posix.c
    ${LV2_ROOT}/zix/src/posix/thread_posix.c
)

if(APPLE)
    list(APPEND ZIX_PLATFORM_SOURCES ${LV2_ROOT}/zix/src/darwin/sem_darwin.c)
else()
    list(APPEND ZIX_PLATFORM_SOURCES ${LV2_ROOT}/zix/src/posix/sem_posix.c)
endif()

add_library(StringTheoryToneHost SHARED
    StringTheoryToneHost.cpp
    ${NAM_ROOT}/NAM/activations.cpp
    ${NAM_ROOT}/NAM/container.cpp
    ${NAM_ROOT}/NAM/conv1d.cpp
    ${NAM_ROOT}/NAM/convnet.cpp
    ${NAM_ROOT}/NAM/dsp.cpp
    ${NAM_ROOT}/NAM/get_dsp.cpp
    ${NAM_ROOT}/NAM/lstm.cpp
    ${NAM_ROOT}/NAM/ring_buffer.cpp
    ${NAM_ROOT}/NAM/util.cpp
    ${NAM_ROOT}/NAM/wavenet/a2_fast.cpp
    ${NAM_ROOT}/NAM/wavenet/model.cpp
    ${NAM_ROOT}/NAM/wavenet/slimmable.cpp
    ${LV2_ROOT}/zix/src/allocator.c
    ${LV2_ROOT}/zix/src/btree.c
    ${LV2_ROOT}/zix/src/bump_allocator.c
    ${LV2_ROOT}/zix/src/digest.c
    ${LV2_ROOT}/zix/src/errno_status.c
    ${LV2_ROOT}/zix/src/filesystem.c
    ${LV2_ROOT}/zix/src/hash.c
    ${LV2_ROOT}/zix/src/path.c
    ${LV2_ROOT}/zix/src/ring.c
    ${LV2_ROOT}/zix/src/status.c
    ${LV2_ROOT}/zix/src/string_view.c
    ${LV2_ROOT}/zix/src/system.c
    ${LV2_ROOT}/zix/src/tree.c
    ${ZIX_PLATFORM_SOURCES}
    ${LV2_ROOT}/serd/src/base64.c
    ${LV2_ROOT}/serd/src/byte_source.c
    ${LV2_ROOT}/serd/src/env.c
    ${LV2_ROOT}/serd/src/n3.c
    ${LV2_ROOT}/serd/src/node.c
    ${LV2_ROOT}/serd/src/read_utf8.c
    ${LV2_ROOT}/serd/src/reader.c
    ${LV2_ROOT}/serd/src/string.c
    ${LV2_ROOT}/serd/src/system.c
    ${LV2_ROOT}/serd/src/uri.c
    ${LV2_ROOT}/serd/src/writer.c
    ${LV2_ROOT}/sord/src/sord.c
    ${LV2_ROOT}/sord/src/syntax.c
    ${LV2_ROOT}/sratom/src/sratom.c
    ${LV2_ROOT}/lilv/src/collections.c
    ${LV2_ROOT}/lilv/src/dylib.c
    ${LV2_ROOT}/lilv/src/instance.c
    ${LV2_ROOT}/lilv/src/lib.c
    ${LV2_ROOT}/lilv/src/load_skimmer.c
    ${LV2_ROOT}/lilv/src/node.c
    ${LV2_ROOT}/lilv/src/node_hash.c
    ${LV2_ROOT}/lilv/src/node_skimmer.c
    ${LV2_ROOT}/lilv/src/plugin.c
    ${LV2_ROOT}/lilv/src/pluginclass.c
    ${LV2_ROOT}/lilv/src/port.c
    ${LV2_ROOT}/lilv/src/query.c
    ${LV2_ROOT}/lilv/src/scalepoint.c
    ${LV2_ROOT}/lilv/src/state.c
    ${LV2_ROOT}/lilv/src/string_util.c
    ${LV2_ROOT}/lilv/src/syntax_skimmer.c
    ${LV2_ROOT}/lilv/src/sys_util.c
    ${LV2_ROOT}/lilv/src/type_skimmer.c
    ${LV2_ROOT}/lilv/src/ui.c
    ${LV2_ROOT}/lilv/src/uris.c
    ${LV2_ROOT}/lilv/src/world.c
)

target_include_directories(StringTheoryToneHost PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${NAM_ROOT}
    ${NAM_ROOT}/NAM
    ${NAM_ROOT}/Dependencies/eigen
    ${NAM_ROOT}/Dependencies/nlohmann
    ${NAM_ROOT}/Dependencies/AudioDSPTools
    ${LV2_ROOT}/lilv/include
    ${LV2_ROOT}/serd/include
    ${LV2_ROOT}/sord/include
    ${LV2_ROOT}/sratom/include
    ${LV2_ROOT}/zix/include
    ${LV2_ROOT}/lv2/include
)

target_compile_definitions(StringTheoryToneHost PRIVATE
    NAM_ENABLE_A2_FAST
    LILV_STATIC
    SERD_STATIC
    SORD_STATIC
    SRATOM_STATIC
    ZIX_STATIC
    LILV_INTERNAL
    SERD_INTERNAL
    SORD_INTERNAL
    SRATOM_INTERNAL
    ZIX_INTERNAL
)

if(APPLE)
    set_target_properties(StringTheoryToneHost PROPERTIES
        OUTPUT_NAME "StringTheoryToneHost"
        PREFIX "lib"
        INSTALL_NAME_DIR "@loader_path"
        INSTALL_RPATH "@loader_path"
        BUILD_WITH_INSTALL_RPATH TRUE
    )
elseif(UNIX)
    target_link_libraries(StringTheoryToneHost PRIVATE dl pthread m)
endif()
