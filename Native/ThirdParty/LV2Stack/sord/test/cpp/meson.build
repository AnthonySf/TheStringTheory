# Copyright 2021-2025 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

if add_languages(['cpp'], native: false, required: false)
  cpp = meson.get_compiler('cpp')

  cpp_suppressions = []
  if cpp.get_id() == 'gcc'
    if warning_level == 'everything'
      cpp_suppressions += [
        '-Wno-abi-tag',
        '-Wno-conversion',
        '-Wno-effc++',
        '-Wno-inline',
        '-Wno-multiple-inheritance',
        '-Wno-switch-enum',
      ]
    endif
  elif cpp.get_id() == 'clang'
    if warning_level == 'everything'
      cpp_suppressions += [
        '-Wno-c++98-compat',
        '-Wno-cast-function-type-strict',
        '-Wno-documentation-unknown-command',
        '-Wno-implicit-float-conversion',
        '-Wno-nullability-extension',
        '-Wno-poison-system-directories',
        '-Wno-shorten-64-to-32',
        '-Wno-switch-enum',
        '-Wno-unsafe-buffer-usage',
      ]
    endif
  elif cpp.get_id() == 'msvc'
    if warning_level == 'everything'
      cpp_suppressions += [
        '/wd4061', # enumerator in switch is not explicitly handled
        '/wd4244', # lossy conversion from 'double' to 'float'
        '/wd4514', # unreferenced inline function has been removed
        '/wd4625', # copy constructor implicitly deleted
        '/wd4626', # copy assignment operator implicitly deleted
        '/wd4710', # function not inlined
        '/wd4711', # function selected for automatic inline expansion
        '/wd4820', # padding added after construct
      ]
    endif

    if warning_level in ['everything', '3', '2']
      cpp_suppressions += [
        '/wd4244', # lossy conversion from 'double' to 'float'
        '/wd4996', # POSIX name for this item is deprecated
      ]
    endif
  endif

  test(
    'sordmm',
    executable(
      'sordmm_test',
      files('sordmm_test.cpp'),
      cpp_args: cpp_suppressions,
      dependencies: sord_dep,
      implicit_include_directories: false,
    ),
    suite: 'unit',
  )
endif
