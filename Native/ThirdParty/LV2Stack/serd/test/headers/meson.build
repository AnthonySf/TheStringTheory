# Copyright 2020-2023 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

header_c_suppressions = []

if get_option('warning_level') == 'everything'
  if cc.get_id() == 'clang'
    header_c_suppressions += [
      '-Wno-padded',
    ]

    if not meson.is_cross_build()
      header_c_suppressions += ['-Wno-poison-system-directories']
    endif

  elif cc.get_id() == 'gcc'
    header_c_suppressions += [
      '-Wno-padded',
      '-Wno-unused-const-variable',
    ]
  elif cc.get_id() == 'msvc'
    header_c_suppressions += [
      '/wd4820', # padding added after construct
    ]
  endif
endif

if cc.get_id() == 'clang'
  header_c_suppressions += [
    '-Wno-nullability-extension',
  ]
endif

test(
  'headers',
  executable(
    'test_headers',
    files('test_headers.c'),
    c_args: header_c_suppressions,
    dependencies: serd_dep,
    implicit_include_directories: false,
  ),
  suite: 'unit',
)
