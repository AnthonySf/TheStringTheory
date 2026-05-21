# Copyright 2020-2025 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

########
# Lint #
########

if get_option('lint')
  subdir('lint')
endif

##############
# Unit Tests #
##############

# Set warning suppression flags specific to tests
test_suppressions = []
if cc.get_id() in ['clang', 'emscripten']
  if warning_level == 'everything'
    test_suppressions += ['-Wno-bad-function-cast']

    if host_machine.system() == 'windows'
      if cc.get_id() in ['clang', 'emscripten']
        test_suppressions += ['-Wno-format-nonliteral']
      endif
    endif
  endif
elif cc.get_id() == 'gcc'
  test_suppressions += [
    '-Wno-analyzer-malloc-leak',
    '-Wno-bad-function-cast',
  ]

  if host_machine.system() == 'windows'
    test_suppressions += ['-Wno-format']
  endif
endif

common_test_sources = files('failing_allocator.c')

# Single-threaded tests that should run everywhere
sequential_tests = {
  'allocator': {'': []},
  'btree': {
    '': [],
    '_small': ['4'],
  },
  'digest': {'': []},
  'environment': {'': []},
  'filesystem': {'': files('../README.md')},
  'hash': {'': []},
  'path': {'': []},
  'status': {'': []},
  'string_view': {'': []},
  'tree': {
    '': [],
    '_seed': ['8', '314159'],
    '_small': ['4'],
  },
}

# Multi-threaded tests that require thread support
threaded_tests = {
  'ring': {
    '': [],
    'small': ['4', '1024'],
  },
  'sem': {
    '': [],
    'one': ['1'],
  },
  'thread': {'': []},
}

# Bad command-line argument (meta-)tests
bad_tests = {
  'btree': {
    '_extra': ['4', '1337'],
  },
  'ring': {
    '_extra': ['4', '1024', '1337'],
  },
  'sem': {
    '_extra': ['4', '1337'],
  },
}

# Test single-threaded
test_executables = {}
foreach test, cases : sequential_tests
  sources = common_test_sources + files('test_@0@.c'.format(test))
  exe = executable(
    'test_@0@'.format(test),
    sources,
    c_args: c_suppressions + program_c_args + test_suppressions,
    dependencies: [zix_dep],
    implicit_include_directories: false,
    include_directories: include_dirs,
    link_args: program_link_args,
  )

  test_executables += {test: exe}
  foreach suffix, args : cases
    test(test + suffix, exe, args: args, suite: 'unit', timeout: 120)
  endforeach
endforeach

# Test multi-threaded
if thread_dep.found()
  foreach test, cases : threaded_tests
    sources = common_test_sources + files('test_@0@.c'.format(test))
    exe = executable(
      'test_@0@'.format(test),
      sources,
      c_args: c_suppressions + program_c_args + test_suppressions,
      dependencies: [zix_dep, thread_dep],
      implicit_include_directories: false,
      include_directories: include_dirs,
      link_args: program_link_args,
    )

    test_executables += {test: exe}
    foreach suffix, args : cases
      test(test + suffix, exe, args: args, suite: 'unit', timeout: 120)
    endforeach
  endforeach
endif

# Test bad cases
foreach test, cases : bad_tests
  foreach suffix, args : cases
    test(
      test + suffix,
      test_executables[test],
      args: args,
      should_fail: true,
      suite: 'unit',
    )
  endforeach
endforeach

# Test that headers have (almost) no warnings
if cc.get_id() != 'emscripten'
  header_suppressions = []
  if cc.get_id() == 'clang'
    header_suppressions += [
      '-Wno-declaration-after-statement',
      '-Wno-nullability-extension',
      '-Wno-padded',
      '-Wno-pre-c23-compat',
    ]

    if not meson.is_cross_build()
      header_suppressions += ['-Wno-poison-system-directories']
    endif

    if host_machine.system() == 'windows'
      header_suppressions += ['-Wno-nonportable-system-include-path']
    endif

  elif cc.get_id() == 'gcc'
    header_suppressions += [
      '-Wno-padded',
      '-Wno-unused-const-variable',
    ]

  elif cc.get_id() == 'msvc'
    header_suppressions += [
      '/experimental:external',
      '/external:W0',
      '/external:anglebrackets',

      '/wd4820', # padding added after construct
    ]
  endif

  header_suppressions = cc.get_supported_arguments(header_suppressions)

  test(
    'headers',
    executable(
      'test_headers',
      files('headers/test_headers.c'),
      c_args: header_suppressions + program_c_args,
      dependencies: zix_dep,
      implicit_include_directories: false,
      include_directories: include_dirs,
    ),
    suite: 'build',
  )
endif

if add_languages(['cpp'], native: false, required: get_option('tests_cpp'))
  cpp = meson.get_compiler('cpp')

  cpp_test_args = []
  if cpp.get_id() in ['clang', 'emscripten']
    cpp_test_args = [
      '-Weverything',
      '-Wno-c++98-compat',
      '-Wno-c++98-compat-pedantic',
      '-Wno-nullability-extension',
      '-Wno-padded',
      '-Wno-unsafe-buffer-usage-in-libc-call',
      '-Wno-zero-as-null-pointer-constant',
    ]

    if not meson.is_cross_build()
      cpp_test_args += ['-Wno-poison-system-directories']
    endif

    if host_machine.system() == 'windows'
      cpp_test_args += ['-Wno-nonportable-system-include-path']
    endif

  elif cpp.get_id() == 'gcc'
    cpp_test_args = [
      '-Wall',
      '-Wno-padded',
      '-Wno-unused-const-variable',
    ]

  elif cpp.get_id() == 'msvc'
    cpp_test_args = [
      '/Wall',
      '/experimental:external',
      '/external:W0',
      '/external:anglebrackets',

      '/wd4514', # unreferenced inline function has been removed
      '/wd4710', # function not inlined
      '/wd4711', # function selected for automatic inline expansion
      '/wd4820', # padding added after construct
      '/wd5039', # throwing function passed to C (winbase.h)
      '/wd5045', # will insert Spectre mitigation for memory load
      '/wd5262', # implicit fall-through
      '/wd5264', # const variable is not used
    ]
  endif

  cpp_test_args = cpp.get_supported_arguments(cpp_test_args)

  test(
    'headers_cpp',
    executable(
      'test_headers_cpp',
      files('cpp/test_headers_cpp.cpp'),
      cpp_args: cpp_test_args + program_c_args,
      dependencies: [zix_dep],
      implicit_include_directories: false,
      include_directories: include_dirs,
      link_args: program_link_args,
    ),
    suite: 'build',
  )

  filesystem_code = '''#include <filesystem>
int main(void) { return 0; }'''

  if cpp.links(filesystem_code, name: 'filesystem')
    test(
      'path_std',
      executable(
        'test_path_std',
        files('cpp/test_path_std.cpp'),
        cpp_args: cpp_test_args + program_c_args,
        dependencies: [zix_dep],
        implicit_include_directories: false,
        include_directories: include_dirs,
        link_args: program_link_args,
      ),
      suite: 'unit',
    )
  endif
endif
