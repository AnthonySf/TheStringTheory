# Copyright 2021-2022 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

module = shared_module(
  'failed_instantiation',
  files('failed_instantiation.c'),
  c_args: c_suppressions,
  dependencies: lv2_dep,
  gnu_symbol_visibility: 'hidden',
  name_prefix: '',
)

extension = '.' + module.full_path().split('.')[-1]
config = configuration_data({'SHLIB_EXT': extension})
ttl_files = ['manifest.ttl', 'failed_instantiation.ttl']

foreach f : ttl_files
  configure_file(
    configuration: config,
    input: files(f + '.in'),
    output: f,
  )
endforeach

test(
  'failed_instantiation',
  executable(
    'test_failed_instantiation',
    files('test_failed_instantiation.c'),
    c_args: c_suppressions + test_args,
    dependencies: [lv2_dep, lilv_dep],
    implicit_include_directories: false,
  ),
  args: [meson.current_build_dir() / ''],
  suite: 'plugin',
)

