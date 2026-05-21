# Copyright 2020-2025 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

project(
  'zix',
  ['c'],
  default_options: [
    'b_ndebug=if-release',
    'buildtype=release',
    'c_std=c99',
    'c_winlibs=',
    'cpp_std=c++17',
    'cpp_winlibs=',
  ],
  license: 'ISC',
  meson_version: '>= 0.56.0',
  version: '0.8.1',
)

zix_src_root = meson.current_source_dir()
zix_build_root = meson.current_build_dir()
major_version = meson.project_version().split('.')[0]
version_suffix = '-@0@'.format(major_version)
versioned_name = 'zix' + version_suffix

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
      '-Wno-c++98-compat',
      '-Wno-declaration-after-statement',
      '-Wno-implicit-fallthrough', # Really for clang < 12
      '-Wno-nrvo',
      '-Wno-padded',
      '-Wno-pre-c11-compat',
      '-Wno-pre-c23-compat',
      '-Wno-switch-default',
      '-Wno-unsafe-buffer-usage',
      '-Wno-unsafe-buffer-usage-in-libc-call',
    ]

    if not meson.is_cross_build()
      c_suppressions += ['-Wno-poison-system-directories']
    endif

    if host_machine.system() == 'windows'
      c_suppressions += [
        '-Wno-deprecated-declarations',
        '-Wno-nonportable-system-include-path',
      ]
    endif
  endif

  if warning_level in ['everything', '3']
    c_suppressions += ['-Wno-nullability-extension']
  endif

  if cc.get_id() == 'emscripten'
    c_suppressions += ['-Wno-format']
  endif

elif cc.get_id() == 'gcc'
  if warning_level == 'everything'
    c_suppressions += [
      '-Wno-analyzer-too-complex',
      '-Wno-inline',
      '-Wno-padded',
      '-Wno-strict-overflow',
      '-Wno-switch-default',
      '-Wno-unsuffixed-float-constants',
    ]

    if host_machine.system() == 'windows'
      c_suppressions += [
        '-Wno-suggest-attribute=const',
        '-Wno-suggest-attribute=format',
        '-Wno-suggest-attribute=pure',
      ]
    endif
  endif

  if warning_level in ['everything', '3']
    c_suppressions += ['-Wno-pedantic'] # C11
  endif

elif cc.get_id() == 'msvc'
  c_suppressions += [
    '/experimental:external',
    '/external:W0',
    '/external:anglebrackets',
  ]

  if warning_level == 'everything'
    c_suppressions += [
      '/wd4464', # relative include path contains ".."
      '/wd4514', # unreferenced inline function has been removed
      '/wd4710', # function not inlined
      '/wd4711', # function selected for automatic inline expansion
      '/wd4800', # implicit conversion to bool
      '/wd4820', # padding added after construct
      '/wd5045', # will insert Spectre mitigation for memory load
    ]
  endif

  if warning_level in ['everything', '3']
    c_suppressions += [
      '/wd4706', # assignment within conditional expression
    ]
  endif

  if get_option('win_ver') in ['nt4', 'xp']
    c_suppressions += ['/D_CRT_SECURE_NO_WARNINGS']
  endif
endif

c_suppressions = cc.get_supported_arguments(c_suppressions)

##########################
# Platform Configuration #
##########################

# Determine system dependencies of the library
dependencies = []
thread_dep = dependency('threads', required: get_option('threads'))
dependencies = [thread_dep]

# Use versioned name everywhere to support parallel major version installations
if host_machine.system() == 'windows'
  if get_option('default_library') == 'both'
    error('default_library=both is not supported on Windows')
  endif
  soversion = ''
else
  soversion = meson.project_version().split('.')[0]
endif

# Request POSIX and system APIs from standard headers if necessary
system_c_args = []
if host_machine.system() == 'darwin'
  system_c_args += [
    '-D_DARWIN_C_SOURCE',
  ]
elif host_machine.system() in ['gnu', 'linux']
  system_c_args += [
    '-D_GNU_SOURCE',
    '-D_POSIX_C_SOURCE=200809L',
    '-D_XOPEN_SOURCE=600',
  ]
elif host_machine.system() in ['dragonfly', 'freebsd', 'netbsd', 'openbsd']
  system_c_args += [
    '-D_BSD_SOURCE'
  ]
elif host_machine.system() == 'windows'
  if not get_option('win_wchar').disabled()
    system_c_args += ['-DUNICODE', '-D_UNICODE']
  endif

  winvers = {
    'nt4': '0x0400',
    'winxp': '0x0501',
    'vista': '0x0600',
    'win8': '0x0602',
  }
  system_c_args += [
    # Set minimum target version
    '-DWINVER=' + winvers[get_option('win_ver')],
    '-D_WIN32_WINNT=' + winvers[get_option('win_ver')],

    # Disable as many things from windows.h as possible
    '-DWIN32_LEAN_AND_MEAN',
    '-DNOGDICAPMASKS', # CC_*, LC_*, PC_*, CP_*, TC_*, RC_
    '-DNOVIRTUALKEYCODES', # VK_*
    '-DNOWINMESSAGES', # WM_*, EM_*, LB_*, CB_*
    '-DNOWINSTYLES', # WS_*, CS_*, ES_*, LBS_*, SBS_*, CBS_*
    '-DNOSYSMETRICS', # SM_*
    '-DNOMENUS', # MF_*
    '-DNOICONS', # IDI_*
    '-DNOKEYSTATES', # MK_*
    '-DNOSYSCOMMANDS', # SC_*
    '-DNORASTEROPS', # Binary and Tertiary raster ops
    '-DNOSHOWWINDOW', # SW_*
    '-DOEMRESOURCE', # OEM Resource values
    '-DNOATOM', # Atom Manager routines
    '-DNOCLIPBOARD', # Clipboard routines
    '-DNOCOLOR', # Screen colors
    '-DNOCTLMGR', # Control and Dialog routines
    '-DNODRAWTEXT', # DrawText() and DT_*
    '-DNOGDI', # All GDI defines and routines
    '-DNOKERNEL', # All KERNEL defines and routines
    '-DNOUSER', # All USER defines and routines
    # Needed for MultiByteToWideChar/WideCharToMultiByte:
    # '-DNONLS', # All NLS defines and routines
    '-DNOMB', # MB_* and MessageBox()
    '-DNOMEMMGR', # GMEM_*, LMEM_*, GHND, LHND, associated routines
    '-DNOMETAFILE', # typedef METAFILEPICT
    '-DNOMINMAX', # Macros min(a,b) and max(a,b)
    '-DNOMSG', # typedef MSG and associated routines
    '-DNOOPENFILE', # OpenFile(), OemToAnsi, AnsiToOem, and OF_*
    '-DNOSCROLL', # SB_* and scrolling routines
    '-DNOSERVICE', # All Service Controller routines, SERVICE_ equates, etc.
    '-DNOSOUND', # Sound driver routines
    '-DNOTEXTMETRIC', # typedef TEXTMETRIC and associated routines
    '-DNOWH', # SetWindowsHook and WH_*
    '-DNOWINOFFSETS', # GWL_*, GCL_*, associated routines
    '-DNOCOMM', # COMM driver routines
    '-DNOKANJI', # Kanji support stuff
    '-DNOHELP', # Help engine interface
    '-DNOPROFILER', # Profiler interface
    '-DNODEFERWINDOWPOS', # DeferWindowPos routines
    '-DNOMCX', # Modem Configuration Extensions
  ]
else
  system_c_args += [
    '-D_POSIX_C_SOURCE=200809L',
    '-D_XOPEN_SOURCE=600',
  ]
endif

# Build platform-specific configuration arguments
platform_c_args = []
if get_option('checks').disabled()
  # Generic build without platform-specific features
  platform_c_args += ['-DZIX_NO_DEFAULT_CONFIG'] + system_c_args
elif get_option('checks').auto()
  # Statically detect configuration from the build environment
  platform_c_args += system_c_args
else
  # Only use the features detected by the build system
  platform_c_args += ['-DZIX_NO_DEFAULT_CONFIG'] + system_c_args

  # Define HAVE_SOMETHING symbols for all detected features
  template = '#include <@0@>\nint main(void) { @1@ }'

  mac_checks = {
    'clonefile': template.format(
      'sys/clonefile.h',
      'return clonefile("/src", "/dst", 0);',
    ),
  }

  posix_checks = {
    'clock_gettime': template.format(
      'time.h',
      'struct timespec t; return clock_gettime(CLOCK_MONOTONIC, &t);',
    ),

    'copy_file_range': template.format(
      'unistd.h',
      'return copy_file_range(0, NULL, 1, NULL, 0U, 0U);',
    ),

    'fileno': template.format('stdio.h', 'return fileno(stdin);'),
    'flock': template.format('sys/file.h', 'return flock(0, 0);'),

    'lstat': template.format(
      'sys/stat.h',
      'struct stat s; return lstat("/", &s);',
    ),

    'mlock': template.format('sys/mman.h', 'return mlock(0, 0);'),

    'pathconf': template.format(
      'unistd.h',
      'return pathconf("/", _PC_PATH_MAX) > 0L;',
    ),

    'posix_fadvise': template.format(
      'fcntl.h',
      'posix_fadvise(0, 0, 4096, POSIX_FADV_SEQUENTIAL);',
    ),

    'posix_memalign': template.format(
      'stdlib.h',
      'void* mem; posix_memalign(&mem, 8, 8);',
    ),

    'realpath': template.format(
      'stdlib.h',
      'return realpath("/", NULL) != NULL;',
    ),

    'sysconf': template.format(
      'unistd.h',
      'return sysconf(_SC_PAGE_SIZE) > 0L;',
    ),
  }

  windows_checks = {
    'sopen_s': template.format(
      'io.h',
      'int fd; return (int)_sopen_s(&fd, "/", 0, 0, 0644);',
    ),
    'CreateFile2': template.format(
      'windows.h',
      'CREATEFILE2_EXTENDED_PARAMETERS p = {0};\n'
      + 'return CreateFile2(NULL, FILE_READ_ATTRIBUTES, 0U, OPEN_EXISTING, &p);',
    ),
    'CreateHardLink': template.format(
      'windows.h',
      'return CreateHardLink(TEXT("l"), TEXT("t"), NULL);',
    ),
    'CreateSymbolicLink': template.format(
      'windows.h',
      'return CreateSymbolicLink(TEXT("l"), TEXT("t"), SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE);',
    ),
    'GetFinalPathNameByHandle': template.format(
      'windows.h',
      'return GetFinalPathNameByHandle(NULL, NULL, 0U, 0U);',
    ),
    'VirtualLock': template.format(
      'windows.h',
      'return VirtualLock(NULL, sizeof(void*));',
    ),
  }

  check_c_args = system_c_args
  if host_machine.system() == 'darwin'
    checks = posix_checks + mac_checks
  elif host_machine.system() == 'windows'
    checks = windows_checks
    if cc.get_id() == 'msvc'
      check_c_args += ['/Wall', '/WX']
    endif
  else
    checks = posix_checks

    if thread_dep.found()
      if cc.links(
        '''#include <semaphore.h>
#include <time.h>
int main(void) { sem_t s; struct timespec t; return sem_timedwait(&s, &t); }''',
        args: check_c_args,
        dependencies: thread_dep,
        name: 'sem_timedwait',
      )
        platform_c_args += ['-DHAVE_SEM_TIMEDWAIT']
      endif
    endif
  endif

  foreach name, check_code : checks
    is_mingw = host_machine.system() == 'windows' and cc.get_id() == 'gcc'
    if is_mingw and name in ['CreateFile2', 'CreateSymbolicLink']
      message('Ignoring MinGW stub @0@'.format(name))
      continue
    endif

    if cc.get_id() == 'emscripten' and name in ['flock', 'fileno']
      message('Ignoring emscripten stub @0@'.format(name))
      continue
    endif

    if cc.links(check_code, args: check_c_args, name: name)
      platform_c_args += ['-DHAVE_@0@'.format(name.to_upper())]
    endif
  endforeach
endif

###########
# Library #
###########

include_dirs = include_directories(['include'])

c_headers = files(
  'include/zix/allocator.h',
  'include/zix/attributes.h',
  'include/zix/warnings.h',
  'include/zix/btree.h',
  'include/zix/bump_allocator.h',
  'include/zix/digest.h',
  'include/zix/environment.h',
  'include/zix/filesystem.h',
  'include/zix/hash.h',
  'include/zix/path.h',
  'include/zix/ring.h',
  'include/zix/sem.h',
  'include/zix/status.h',
  'include/zix/string_view.h',
  'include/zix/thread.h',
  'include/zix/tree.h',
  'include/zix/zix.h',
)

sources = files(
  'src/allocator.c',
  'src/btree.c',
  'src/bump_allocator.c',
  'src/digest.c',
  'src/errno_status.c',
  'src/filesystem.c',
  'src/hash.c',
  'src/path.c',
  'src/ring.c',
  'src/status.c',
  'src/string_view.c',
  'src/system.c',
  'src/tree.c',
)

if host_machine.system() == 'windows'
  sources += files(
    'src/win32/environment_win32.c',
    'src/win32/filesystem_win32.c',
    'src/win32/system_win32.c',
    'src/win32/win32_util.c',
  )
else
  sources += files(
    'src/posix/environment_posix.c',
    'src/posix/filesystem_posix.c',
    'src/posix/system_posix.c',
  )
endif

if thread_dep.found()
  if host_machine.system() == 'darwin'
    sources += files(
      'src/darwin/sem_darwin.c',
      'src/posix/thread_posix.c',
    )

  elif host_machine.system() == 'windows'
    sources += files(
      'src/win32/sem_win32.c',
      'src/win32/thread_win32.c',
    )
  else
    sources += files(
      'src/posix/sem_posix.c',
      'src/posix/thread_posix.c',
    )
  endif
endif

# Set appropriate arguments for building against the library type
extra_c_args = []
if get_option('default_library') == 'static'
  extra_c_args = ['-DZIX_STATIC']
endif

# Set any additional arguments required for building libraries or programs
library_c_args = platform_c_args + extra_c_args + ['-DZIX_INTERNAL']
library_link_args = []
program_c_args = extra_c_args
program_link_args = []
if cc.get_id() == 'emscripten'
  wasm_c_args = [
    '-matomics',
    '-mbulk-memory',
    '-pthread',
  ]

  wasm_link_args = wasm_c_args + [
    ['-s', 'ENVIRONMENT=node,worker'],
    ['-s', 'INITIAL_MEMORY=33554432'],
  ]

  library_c_args += wasm_c_args
  program_c_args += wasm_c_args
  library_link_args += wasm_link_args
  program_link_args += wasm_link_args
  program_link_args += [
    ['-s', 'EXIT_RUNTIME'],
    ['-s', 'PROXY_TO_PTHREAD'],
  ]
endif

# Build shared and/or static library
libzix = library(
  versioned_name,
  sources,
  c_args: c_suppressions + library_c_args,
  darwin_versions: ['0.8.0', meson.project_version()],
  dependencies: dependencies,
  gnu_symbol_visibility: 'hidden',
  implicit_include_directories: false,
  include_directories: include_dirs,
  install: true,
  link_args: library_link_args,
  soversion: soversion,
  version: meson.project_version(),
)

# Declare dependency for internal meson dependants
zix_dep = declare_dependency(
  compile_args: extra_c_args,
  include_directories: include_dirs,
  link_with: libzix,
)

# Generate pkg-config file for external dependants
pkg.generate(
  libzix,
  description: 'Lightweight C library of portability wrappers and data structures',
  extra_cflags: extra_c_args,
  filebase: versioned_name,
  name: 'Zix',
  requires: dependencies,
  subdirs: [versioned_name],
  version: meson.project_version(),
)

# Override pkg-config dependency for internal meson dependants
meson.override_dependency(versioned_name, zix_dep)

# Install headers to a versioned include directory
install_headers(c_headers, subdir: versioned_name / 'zix')

#########
# Tests #
#########

if not get_option('tests').disabled()
  subdir('test')
endif

##############
# Benchmarks #
##############

build_benchmarks = false
if not get_option('benchmarks').disabled()
  subdir('benchmark')
endif

# Display top-level summary (augmented in subdirectories)
if not meson.is_subproject()
  summary(
    {
      'Tests': not get_option('tests').disabled(),
      'Benchmarks': build_benchmarks,
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

#############################
# Scripts and Documentation #
#############################

subdir('scripts')

if not get_option('docs').disabled()
  subdir('doc')
endif
