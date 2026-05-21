# Copyright 2021-2026 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

project(
  'serd',
  ['c'],
  default_options: [
    'b_ndebug=if-release',
    'buildtype=release',
    'c_std=c99',
    'c_winlibs=[]',
  ],
  license: 'ISC',
  meson_version: '>= 0.56.0',
  version: '0.32.9',
)

serd_src_root = meson.current_source_dir()
serd_build_root = meson.current_build_dir()
major_version = meson.project_version().split('.')[0]
version_suffix = '-@0@'.format(major_version)
versioned_name = 'serd' + version_suffix

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
      '-Wno-format-nonliteral',
      '-Wno-padded',
      '-Wno-switch-default',
      '-Wno-unsafe-buffer-usage',
    ]

    if host_machine.system() == 'windows'
      c_suppressions += [
        '-Wno-deprecated-declarations',
        '-Wno-nonportable-system-include-path',
        '-Wno-unused-macros',
      ]
    endif
  endif

  if warning_level in ['everything', '3']
    c_suppressions += ['-Wno-nullability-extension']

    if host_machine.system() == 'freebsd'
      c_suppressions += ['-Wno-c11-extensions']
    endif
  endif

  if not meson.is_cross_build()
    c_suppressions += ['-Wno-poison-system-directories']
  endif

elif cc.get_id() == 'gcc'
  if warning_level == 'everything'
    c_suppressions += [
      '-Wno-cast-align',
      '-Wno-cast-qual',
      '-Wno-format-nonliteral',
      '-Wno-inline',
      '-Wno-padded',
      '-Wno-switch-default',
      '-Wno-unsuffixed-float-constants',
      '-Wno-unused-const-variable',
    ]

    if host_machine.system() == 'windows'
      c_suppressions += ['-Wno-float-conversion']
    endif
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
      '/wd4800', # implicit conversion from int to bool
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
      '/wd4996', # POSIX name for this item is deprecated
    ]
  endif
endif

c_suppressions = cc.get_supported_arguments(c_suppressions)

################
# Dependencies #
################

m_dep = cc.find_library('m', required: false)

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

# Request POSIX APIs from standard headers if necessary
system_c_args = []
if host_machine.system() in ['cygwin', 'gnu', 'linux']
  system_c_args += ['-D_POSIX_C_SOURCE=200809L']
endif

# Build platform-specific configuration arguments
platform_c_args = []
if get_option('checks').disabled()
  # Generic build without platform-specific features
  platform_c_args += ['-DSERD_NO_DEFAULT_CONFIG']
elif get_option('checks').auto()
  # Statically detect configuration from the build environment
  platform_c_args += system_c_args
else
  # Only use the features detected by the build system
  platform_c_args += ['-DSERD_NO_DEFAULT_CONFIG'] + system_c_args

  # Feature checks have a header to include and code for the body of main()
  template = '#include <@0@>\nint main(void) { @1@; }'
  feature_tests = {
    'fileno': [
      'stdio.h',
      'return fileno(stdin);',
    ],
    'posix_fadvise': [
      'fcntl.h',
      'posix_fadvise(0, 0, 4096, POSIX_FADV_SEQUENTIAL);',
    ],
    'posix_memalign': [
      'stdlib.h',
      'void* mem=NULL; posix_memalign(&mem, 8U, 8U);',
    ],
  }

  # Define HAVE_SOMETHING symbols for all detected features
  foreach name, args : feature_tests
    code = template.format(args[0], args[1])
    if cc.links(code, args: system_c_args, name: name)
      platform_c_args += ['-DHAVE_' + name.to_upper()]
    endif
  endforeach
endif

###########
# Library #
###########

include_dirs = include_directories('include')

c_headers = files(
  'include/serd/serd.h',
)

sources = files(
  'src/base64.c',
  'src/byte_source.c',
  'src/env.c',
  'src/n3.c',
  'src/node.c',
  'src/read_utf8.c',
  'src/reader.c',
  'src/string.c',
  'src/system.c',
  'src/uri.c',
  'src/writer.c',
)

# Set appropriate arguments for building against the library type
extra_c_args = []
if get_option('default_library') == 'static'
  extra_c_args = ['-DSERD_STATIC']
endif

# Build shared and/or static library
libserd = library(
  versioned_name,
  sources,
  c_args: [
    '-DSERD_INTERNAL',
    '-DSERD_VERSION="@0@"'.format(meson.project_version()),
  ] + c_suppressions + extra_c_args + platform_c_args,
  darwin_versions: [major_version + '.0.0', meson.project_version()],
  dependencies: m_dep,
  gnu_symbol_visibility: 'hidden',
  implicit_include_directories: false,
  include_directories: include_dirs,
  install: true,
  soversion: soversion,
  version: meson.project_version(),
)

# Declare dependency for internal meson dependants
serd_dep = declare_dependency(
  compile_args: extra_c_args,
  include_directories: include_dirs,
  link_with: libserd,
)

# Generate pkg-config file for external dependants
pkg.generate(
  libserd,
  description: 'Lightweight C library for working with RDF data',
  extra_cflags: extra_c_args,
  filebase: versioned_name,
  name: get_option('title'),
  subdirs: [versioned_name],
  version: meson.project_version(),
)

# Override pkg-config dependency for internal meson dependants
meson.override_dependency(versioned_name, serd_dep)

# Install header to a versioned include directory
install_headers(c_headers, subdir: versioned_name / 'serd')

#########
# Tools #
#########

# Build serdi command line utility
if not get_option('tools').disabled()
  tool_link_args = []
  if get_option('static')
    tool_link_args += ['-static']
  endif

  serdi = executable(
    'serdi',
    files('src/serdi.c'),
    c_args: c_suppressions + platform_c_args,
    dependencies: serd_dep,
    implicit_include_directories: false,
    install: true,
    link_args: tool_link_args,
  )

  meson.override_find_program('serdi', serdi)
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
      },
      section: 'Directories',
    )
  endif
endif

###########
# Support #
###########

subdir('scripts')

if not get_option('tests').disabled()
  subdir('test')
endif

subdir('doc')
