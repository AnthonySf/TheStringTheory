# Copyright 2021-2025 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

project(
  'lilv',
  ['c'],
  default_options: [
    'b_ndebug=if-release',
    'buildtype=release',
    'c_std=c99',
    'c_winlibs=',
    'cpp_std=c++11',
    'cpp_winlibs=',
  ],
  license: 'ISC',
  meson_version: '>= 0.56.0',
  version: '0.26.5',
)

lilv_src_root = meson.current_source_dir()
lilv_build_root = meson.current_build_dir()
major_version = meson.project_version().split('.')[0]
version_suffix = '-@0@'.format(major_version)
versioned_name = 'lilv' + version_suffix

#######################
# Compilers and Flags #
#######################

# Required tools
pkg = import('pkgconfig')
cc = meson.get_compiler('c')

# Enable C++ support if bindings aren't disabled
if not get_option('bindings_cpp').disabled()
  if add_languages(['cpp'], native: false, required: false)
    cpp = meson.get_compiler('cpp')
  endif
endif

# Set global warning suppressions
warning_level = get_option('warning_level')
c_suppressions = []
if cc.get_id() in ['clang', 'emscripten']
  if warning_level == 'everything'
    c_suppressions += [
      '-Wno-cast-align',
      '-Wno-cast-function-type-strict',
      '-Wno-cast-qual',
      '-Wno-declaration-after-statement',
      '-Wno-documentation-unknown-command',
      '-Wno-double-promotion',
      '-Wno-float-equal',
      '-Wno-format-nonliteral',
      '-Wno-implicit-float-conversion',
      '-Wno-implicit-int-conversion',
      '-Wno-padded',
      '-Wno-reserved-id-macro',
      '-Wno-shorten-64-to-32',
      '-Wno-sign-conversion',
      '-Wno-switch-default',
      '-Wno-switch-enum',
      '-Wno-unsafe-buffer-usage',
    ]

    if not meson.is_cross_build()
      c_suppressions += ['-Wno-poison-system-directories']
    endif
  endif

  if warning_level in ['everything', '3']
    c_suppressions += ['-Wno-nullability-extension']

    if host_machine.system() == 'freebsd'
      c_suppressions += ['-Wno-c11-extensions']
    elif host_machine.system() == 'darwin'
      c_suppressions += ['-Wno-unused-macros']
    elif host_machine.system() == 'windows'
      c_suppressions += [
        '-Wno-deprecated-declarations',
        '-Wno-nonportable-system-include-path',
        '-Wno-unused-macros',
      ]
    endif
  endif

elif cc.get_id() == 'gcc'
  if warning_level == 'everything'
    c_suppressions += [
      '-Wno-cast-align',
      '-Wno-cast-qual',
      '-Wno-conversion',
      '-Wno-double-promotion',
      '-Wno-float-equal',
      '-Wno-format-nonliteral',
      '-Wno-format-truncation',
      '-Wno-inline',
      '-Wno-padded',
      '-Wno-stack-protector',
      '-Wno-strict-overflow',
      '-Wno-suggest-attribute=const',
      '-Wno-suggest-attribute=pure',
      '-Wno-switch-default',
      '-Wno-switch-enum',
      '-Wno-unsuffixed-float-constants',
      '-Wno-unused-const-variable',
      '-Wno-unused-parameter',
    ]
  endif

  if warning_level in ['everything', '3']
    c_suppressions += ['-Wno-pedantic']
  endif

  if host_machine.system() == 'windows'
    c_suppressions += [
      '-Wno-bad-function-cast',
      '-Wno-unused-macros',
    ]
  endif

elif cc.get_id() == 'msvc'
  c_suppressions += [
    '/experimental:external',
    '/external:W0',
    '/external:anglebrackets',
  ]

  if warning_level == 'everything'
    c_suppressions += [
      '/wd4061', # enumerator in switch is not explicitly handled
      '/wd4191', # unsafe conversion from FARPROC
      '/wd4365', # signed/unsigned mismatch
      '/wd4514', # unreferenced inline function has been removed
      '/wd4710', # function not inlined
      '/wd4711', # function selected for automatic inline expansion
      '/wd4774', # format string is not a string literal
      '/wd4800', # implicit conversion to bool
      '/wd4820', # padding added after construct
      '/wd5045', # will insert Spectre mitigation for memory load
    ]
  endif

  if warning_level in ['everything', '3']
    c_suppressions += [
      '/wd4090', # different const qualifiers
      '/wd4706', # assignment within conditional expression
    ]
  endif

  if warning_level in ['everything', '3', '2']
    c_suppressions += [
      '/wd4244', # conversion from floating point, possible loss of data
      '/wd4267', # conversion from size_t, possible loss of data
      '/wd4996', # POSIX name for this item is deprecated
    ]
  endif
endif

c_suppressions = cc.get_supported_arguments(c_suppressions)

##########################
# Platform Configuration #
##########################

platform_defines = ['-DLILV_VERSION="@0@"'.format(meson.project_version())]
if host_machine.system() in ['gnu', 'linux']
  platform_defines += [
    '-D_POSIX_C_SOURCE=200809L',
  ]
endif

default_lv2_path = get_option('default_lv2_path')
if default_lv2_path == ''
  if host_machine.system() == 'darwin'
    lv2_dirs = [
      '~/.lv2',
      '~/Library/Audio/Plug-Ins/LV2',
      '/usr/local/lib/lv2',
      '/usr/lib/lv2',
      '/Library/Audio/Plug-Ins/LV2',
    ]

    default_lv2_path = ':'.join(lv2_dirs)

  elif host_machine.system() == 'haiku'
    default_lv2_path = ':'.join(['~/.lv2', '/boot/common/add-ons/lv2'])

  elif host_machine.system() == 'windows'
    if build_machine.system() == 'windows'
      lv2_dirs = ['%%APPDATA%%\\\\LV2', '%%COMMONPROGRAMFILES%%\\\\LV2']
    else
      lv2_dirs = ['%APPDATA%\\\\LV2', '%COMMONPROGRAMFILES%\\\\LV2']
    endif
    default_lv2_path = ';'.join(lv2_dirs)

  else
    lv2_dirs = [
      '~' / '.lv2',
      '/usr/local' / get_option('libdir') / 'lv2',
      '/usr' / get_option('libdir') / 'lv2',
    ]

    default_lv2_path = ':'.join(lv2_dirs)
  endif
endif

platform_defines += ['-DLILV_DEFAULT_LV2_PATH="@0@"'.format(default_lv2_path)]

# Use versioned name everywhere to support parallel major version installations
if host_machine.system() == 'windows'
  if get_option('default_library') == 'both'
    error('default_library=both is not supported on Windows')
  endif
  soversion = ''
else
  soversion = meson.project_version().split('.')[0]
endif

add_project_arguments(platform_defines, language: ['c'])

################
# Dependencies #
################

m_dep = cc.find_library('m', required: false)
dl_dep = cc.find_library('dl', required: false)

zix_dep = dependency(
  'zix-0',
  include_type: 'system',
  version: '>= 0.6.0',
)
serd_dep = dependency(
  'serd-0',
  include_type: 'system',
  version: '>= 0.30.10',
)
sord_dep = dependency(
  'sord-0',
  include_type: 'system',
  version: '>= 0.16.20',
)
lv2_dep = dependency(
  'lv2',
  include_type: 'system',
  version: '>= 1.18.2',
)
sratom_dep = dependency(
  'sratom-0',
  include_type: 'system',
  version: '>= 0.6.10',
)

###########
# Library #
###########

c_headers = files('include/lilv/lilv.h')
cpp_headers = files('include/lilv/lilvmm.hpp')

sources = files(
  'src/collections.c',
  'src/dylib.c',
  'src/instance.c',
  'src/lib.c',
  'src/load_skimmer.c',
  'src/node.c',
  'src/node_hash.c',
  'src/node_skimmer.c',
  'src/plugin.c',
  'src/pluginclass.c',
  'src/port.c',
  'src/query.c',
  'src/scalepoint.c',
  'src/state.c',
  'src/string_util.c',
  'src/syntax_skimmer.c',
  'src/sys_util.c',
  'src/type_skimmer.c',
  'src/ui.c',
  'src/uris.c',
  'src/world.c',
)

common_dependencies = [
  dl_dep,
  lv2_dep,
  m_dep,
  serd_dep,
  sord_dep,
  sratom_dep,
  zix_dep,
]

# Set appropriate arguments for building against the library type
extra_c_args = []
if get_option('default_library') == 'static'
  extra_c_args = ['-DLILV_STATIC']
endif

# Set arguments for building library internals
lib_c_args = c_suppressions + extra_c_args + ['-DLILV_INTERNAL']
if not get_option('dynmanifest').disabled()
  lib_c_args += ['-DLILV_DYN_MANIFEST']
endif

# Build main shared and/or static library
liblilv = library(
  versioned_name,
  sources,
  c_args: lib_c_args,
  darwin_versions: [major_version + '.0.0', meson.project_version()],
  dependencies: common_dependencies,
  gnu_symbol_visibility: 'hidden',
  implicit_include_directories: false,
  include_directories: include_directories('include'),
  install: true,
  soversion: soversion,
  version: meson.project_version(),
)

# Declare dependency for internal meson dependants
lilv_dep = declare_dependency(
  compile_args: extra_c_args,
  dependencies: common_dependencies,
  include_directories: include_directories('include'),
  link_with: liblilv,
)

# Generate pkg-config file for external dependants
pkg.generate(
  liblilv,
  description: 'Library for hosting LV2 plugins',
  extra_cflags: extra_c_args,
  filebase: versioned_name,
  name: 'Lilv',
  requires: ['lv2'],
  subdirs: [versioned_name],
  version: meson.project_version(),
)

# Override pkg-config dependency for internal meson dependants
meson.override_dependency(versioned_name, lilv_dep)

# Install headers to a versioned include directory
install_headers(c_headers, subdir: versioned_name / 'lilv')
install_headers(cpp_headers, subdir: versioned_name / 'lilv')

# Display top-level summary (before subdirectories to appear first)
if not meson.is_subproject()
  summary(
    {
      'Tests': not get_option('tests').disabled(),
      'Tools': not get_option('tools').disabled(),
    },
    bool_yn: true,
    section: 'Components',
  )

  summary('Default LV2_PATH', default_lv2_path, section: 'Configuration')

  summary(
    {
      'Install prefix': get_option('prefix'),
      'Headers': get_option('prefix') / get_option('includedir'),
      'Libraries': get_option('prefix') / get_option('libdir'),
      'Executables': get_option('prefix') / get_option('bindir'),
      'Man pages': get_option('prefix') / get_option('mandir'),
    },
    section: 'Directories',
  )
endif

#########
# Tools #
#########

if not get_option('tools').disabled()
  subdir('tools')
endif

############
# Bindings #
############

if not get_option('bindings_py').disabled()
  subdir('bindings/bindings_test_plugin.lv2')
  subdir('bindings/python')
  subdir('bindings/test')
endif

###########
# Support #
###########

if not get_option('docs').disabled()
  subdir('doc')
endif

if not get_option('tests').disabled()
  subdir('test')
endif
