# Copyright 2020-2022 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

cpp_suppressions = []
if cpp.get_id() == 'gcc'
  cpp_suppressions += [
    '-Wno-cast-align',
    '-Wno-padded',
  ]
elif cpp.get_id() == 'clang'
  cpp_suppressions += [
    '-Wno-c++98-compat',
    '-Wno-cast-align',
    '-Wno-cast-qual',
    '-Wno-documentation-unknown-command',
    '-Wno-nullability-extension',
    '-Wno-padded',
    '-Wno-poison-system-directories',
    '-Wno-reserved-id-macro',
  ]
elif cpp.get_id() == 'msvc'
  cpp_suppressions += [
    '/wd4514', # unreferenced inline function has been removed
    '/wd4820', # padding added after construct
  ]
endif

test(
  'lilv_hpp',
  executable(
    'test_lilv_hpp',
    files('test_lilv_hpp.cpp'),
    cpp_args: test_args + cpp_suppressions,
    dependencies: [lv2_dep, lilv_dep],
    implicit_include_directories: false,
  ),
  suite: 'unit',
)
