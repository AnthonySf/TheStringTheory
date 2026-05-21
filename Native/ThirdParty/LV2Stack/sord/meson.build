# Copyright 2021-2025 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

project(
  'sord',
  ['c'],
  default_options: [
    'b_ndebug=if-release',
    'buildtype=release',
    'c_std=c99',
    'c_winlibs=',
    'cpp_winlibs=',
  ],
  license: 'ISC',
  meson_version: '>= 0.56.0',
  version: '0.16.22',
)

sord_src_root = meson.current_source_dir()
major_version = meson.project_version().split('.')[0]
version_suffix = '-@0@'.format(major_version)
versioned_name = 'sord' + version_suffix

#######################
# Compilers and Flags #
#######################

# Required tools
pkg = import('pkgconfig')
cc = meson.get_compiler('c')

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
      '-Wno-double-promotion',
      '-Wno-format-nonliteral',
      '-Wno-padded',
      '-Wno-reserved-id-macro',
      '-Wno-switch-default',
      '-Wno-switch-enum',
      '-Wno-unsafe-buffer-usage',
      '-Wno-unused-macros',
    ]

    if not meson.is_cross_build()
      c_suppressions += ['-Wno-poison-system-directories']
    endif
  endif

  if warning_level in ['everything', '3']
    c_suppressions += ['-Wno-nullability-extension']

    if host_machine.system() == 'freebsd'
      c_suppressions += ['-Wno-c11-extensions']
    endif

    if host_machine.system() == 'windows'
      c_suppressions += [
        '-Wno-deprecated-declarations',
        '-Wno-nonportable-system-include-path',
      ]
    endif
  endif

elif cc.get_id() == 'gcc'
  if warning_level == 'everything'
    c_suppressions += [
      '-Wno-cast-align',
      '-Wno-cast-qual',
      '-Wno-format-nonliteral',
      '-Wno-inline',
      '-Wno-padded',
      '-Wno-strict-overflow',
      '-Wno-suggest-attribute=const',
      '-Wno-suggest-attribute=pure',
      '-Wno-switch-default',
      '-Wno-switch-enum',
      '-Wno-unsuffixed-float-constants',
      '-Wno-unused-const-variable',
      '-Wno-unused-macros',
    ]
  endif

  if warning_level in ['everything', '3']
    c_suppressions += ['-Wno-format']
  endif

  if host_machine.system() == 'windows'
    c_suppressions += [
      '-Wno-float-conversion',
      '-Wno-suggest-attribute=format',
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
      '/wd4514', # unreferenced inline function has been removed
      '/wd4710', # function not inlined
      '/wd4711', # function selected for automatic inline expansion
      '/wd4820', # padding added after construct
      '/wd5045', # will insert Spectre mitigation for memory load
    ]
  endif

  if warning_level in ['everything', '3']
    c_suppressions += [
      '/wd4706', # assignment within conditional expression
    ]
  endif

  if warning_level in ['everything', '3', '2']
    c_suppressions += [
      '/wd4996', # function or variable may be unsafe
    ]
  endif
endif

c_suppressions = cc.get_supported_arguments(c_suppressions)

################
# Dependencies #
################

m_dep = cc.find_library('m', required: false)

zix_dep = dependency('zix-0', include_type: 'system', version: '>= 0.4.0')
serd_dep = dependency('serd-0', include_type: 'system', version: '>= 0.30.10')

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

c_headers = files('include/sord/sord.h')
cpp_headers = files('include/sord/sordmm.hpp')

sources = files(
  'src/sord.c',
  'src/syntax.c',
)

# Set appropriate arguments for building against the library type
extra_c_args = []
if get_option('default_library') == 'static'
  extra_c_args = ['-DSORD_STATIC']
endif

# Build shared and/or static library
libsord = library(
  versioned_name,
  sources,
  c_args: c_suppressions + extra_c_args + [
    '-DSORD_INTERNAL',
    '-DSORD_VERSION="@0@"'.format(meson.project_version()),
  ],
  darwin_versions: [major_version + '.0.0', meson.project_version()],
  dependencies: [m_dep, zix_dep, serd_dep],
  gnu_symbol_visibility: 'hidden',
  implicit_include_directories: false,
  include_directories: include_directories('include'),
  install: true,
  soversion: soversion,
  version: meson.project_version(),
)

# Declare dependency for internal meson dependants
sord_dep = declare_dependency(
  compile_args: extra_c_args,
  dependencies: [m_dep, zix_dep, serd_dep],
  include_directories: include_directories('include'),
  link_with: libsord,
)

# Generate pkg-config file for external dependants
pkg.generate(
  libsord,
  description: 'Lightweight C library for storing RDF in memory',
  extra_cflags: extra_c_args,
  filebase: versioned_name,
  name: 'Sord',
  requires: ['serd-0'],
  subdirs: [versioned_name],
  version: meson.project_version(),
)

# Override pkg-config dependency for internal meson dependants
meson.override_dependency(versioned_name, sord_dep)

# Install headers to a versioned include directory
install_headers(c_headers, subdir: versioned_name / 'sord')
install_headers(cpp_headers, subdir: versioned_name / 'sord')

#########
# Tools #
#########

# Build sordi command line utility
if not get_option('tools').disabled()
  sordi = executable(
    'sordi',
    files('src/sordi.c'),
    c_args: c_suppressions,
    dependencies: sord_dep,
    implicit_include_directories: false,
    install: true,
  )

  meson.override_find_program('sordi', sordi)

  pcre2_dep = dependency('libpcre2-8', include_type: 'system', required: false)

  if pcre2_dep.found()
    sord_validate = executable(
      'sord_validate',
      files('src/sord_validate.c'),
      c_args: c_suppressions,
      dependencies: [sord_dep, pcre2_dep],
      implicit_include_directories: false,
      install: true,
    )

    meson.override_find_program('sord_validate', sord_validate)
  endif

  if not get_option('man').disabled()
    install_man(files('doc/sord_validate.1', 'doc/sordi.1'))
  endif
endif

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

  summary(
    {
      'Install prefix': get_option('prefix'),
      'Headers': get_option('prefix') / get_option('includedir'),
      'Libraries': get_option('prefix') / get_option('libdir'),
    },
    section: 'Directories',
  )

  if not get_option('tools').disabled()
    summary(
      {
        'Executables': get_option('prefix') / get_option('bindir'),
        'Man pages': get_option('prefix') / get_option('mandir'),
      },
      section: 'Directories',
    )
  endif
endif

###########
# Support #
###########

if not get_option('tests').disabled()
  subdir('test')
endif

subdir('doc')
