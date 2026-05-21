# Copyright 2021-2025 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

project(
  'sratom',
  ['c'],
  default_options: [
    'b_ndebug=if-release',
    'buildtype=release',
    'c_std=c99',
    'c_winlibs=',
  ],
  license: 'ISC',
  meson_version: '>= 0.56.0',
  version: '0.6.22',
)

sratom_src_root = meson.current_source_dir()
sratom_build_root = meson.current_build_dir()
major_version = meson.project_version().split('.')[0]
version_suffix = '-@0@'.format(major_version)
versioned_name = 'sratom' + version_suffix

#######################
# Compilers and Flags #
#######################

# Required tools
pkg = import('pkgconfig')
cc = meson.get_compiler('c')

# Set global warning suppressions
warning_level = get_option('warning_level')
c_suppressions = []
if cc.get_id() == 'clang'
  if warning_level == 'everything'
    c_suppressions += [
      '-Wno-bad-function-cast',
      '-Wno-cast-align',
      '-Wno-cast-function-type-strict',
      '-Wno-cast-qual',
      '-Wno-declaration-after-statement',
      '-Wno-documentation-unknown-command',
      '-Wno-double-promotion',
      '-Wno-float-conversion',
      '-Wno-implicit-float-conversion',
      '-Wno-implicit-int-conversion',
      '-Wno-padded',
      '-Wno-shorten-64-to-32',
      '-Wno-sign-conversion',
      '-Wno-unsafe-buffer-usage',
    ]

    if host_machine.system() == 'windows'
      c_suppressions += [
        '-Wno-deprecated-declarations',
        '-Wno-format-nonliteral',
      ]
    endif

    if not meson.is_cross_build()
      c_suppressions += ['-Wno-poison-system-directories']
    endif
  endif

  if warning_level in ['everything', '3']
    c_suppressions += ['-Wno-nullability-extension']
  endif

elif cc.get_id() == 'gcc'
  if warning_level == 'everything'
    c_suppressions += [
      '-Wno-bad-function-cast',
      '-Wno-cast-align',
      '-Wno-cast-qual',
      '-Wno-conversion',
      '-Wno-inline',
      '-Wno-padded',
      '-Wno-suggest-attribute=pure',
      '-Wno-unsuffixed-float-constants',
      '-Wno-unused-const-variable',
    ]

    if host_machine.system() == 'windows'
      c_suppressions += ['-Wno-suggest-attribute=format']
    endif
  endif

elif cc.get_id() == 'msvc'
  if warning_level == 'everything'
    c_suppressions += [
      '/wd4242', # conversion, possible loss of data
      '/wd4365', # signed/unsigned mismatch
      '/wd4514', # unreferenced inline function has been removed
      '/wd4706', # assignment within conditional expression
      '/wd4710', # function not inlined
      '/wd4711', # function selected for automatic inline expansion
      '/wd4820', # padding added after construct
      '/wd5045', # will insert Spectre mitigation for memory load
    ]
  endif

  if warning_level in ['everything', '3', '2']
    c_suppressions += [
      '/wd4267', # conversion from size_t, possible loss of data
      '/wd4996', # function or variable may be unsafe
    ]
  endif

  if warning_level in ['everything', '3', '2', '1']
    c_suppressions += [
      '/wd4244', # conversion from floating point, possible loss of data
    ]
  endif
endif

c_suppressions = cc.get_supported_arguments(c_suppressions)

################
# Dependencies #
################

m_dep = cc.find_library('m', required: false)

serd_dep = dependency('serd-0', include_type: 'system', version: '>= 0.30.10')
sord_dep = dependency('sord-0', include_type: 'system', version: '>= 0.16.16')
lv2_dep = dependency('lv2', include_type: 'system', version: '>= 1.18.4')

##########################
# Platform Configuration #
##########################

# Use versioned name everywhere to support parallel major version installations
if host_machine.system() == 'windows'
  if get_option('default_library') == 'both'
    error('default_library=both is not supported on Windows')
  endif
  soversion = ''
else
  soversion = meson.project_version().split('.')[0]
endif

###########
# Library #
###########

include_dirs = include_directories('include')
c_headers = files('include/sratom/sratom.h')
sources = files('src/sratom.c')

# Set appropriate arguments for building against the library type
extra_c_args = []
if get_option('default_library') == 'static'
  extra_c_args = ['-DSRATOM_STATIC']
endif

# Build shared and/or static library
libsratom = library(
  versioned_name,
  sources,
  c_args: c_suppressions + extra_c_args + ['-DSRATOM_INTERNAL'],
  darwin_versions: [major_version + '.0.0', meson.project_version()],
  dependencies: [m_dep, lv2_dep, serd_dep, sord_dep],
  gnu_symbol_visibility: 'hidden',
  implicit_include_directories: false,
  include_directories: include_dirs,
  install: true,
  soversion: soversion,
  version: meson.project_version(),
)

# Declare dependency for internal meson dependants
sratom_dep = declare_dependency(
  compile_args: extra_c_args,
  dependencies: [m_dep, lv2_dep, serd_dep, sord_dep],
  include_directories: include_dirs,
  link_with: libsratom,
)

# Generate pkg-config file for external dependants
pkg.generate(
  libsratom,
  description: 'Small library for serializing LV2 atoms',
  extra_cflags: extra_c_args,
  filebase: versioned_name,
  name: 'Sratom',
  requires: ['serd-0', 'sord-0', 'lv2'],
  subdirs: [versioned_name],
  version: meson.project_version(),
)

# Override pkg-config dependency for internal meson dependants
meson.override_dependency(versioned_name, sratom_dep)

# Install header to a versioned include directory
install_headers(c_headers, subdir: versioned_name / 'sratom')

# Display top-level summary (before subdirectories to appear first)
if not meson.is_subproject()
  summary(
    {
      'Tests': not get_option('tests').disabled(),
    },
    bool_yn: true,
    section: 'Components',
  )

  summary(
    {
      'Install prefix': get_option('prefix'),
      'Headers': get_option('prefix') / get_option('includedir'),
      'Libraries': get_option('prefix') / get_option('libdir'),
    },
    section: 'Directories',
  )
endif

###########
# Support #
###########

if not get_option('tests').disabled()
  subdir('test')
endif

if not get_option('docs').disabled()
  subdir('doc')
endif
