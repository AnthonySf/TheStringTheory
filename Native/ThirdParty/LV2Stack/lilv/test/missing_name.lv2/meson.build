# Copyright 2021-2022 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

module = shared_module(
  'missing_name',
  files('missing_name.c'),
  c_args: c_suppressions,
  dependencies: lv2_dep,
  gnu_symbol_visibility: 'hidden',
  name_prefix: '',
)

extension = '.' + module.full_path().split('.')[-1]
config = configuration_data({'SHLIB_EXT': extension})
ttl_files = ['manifest.ttl', 'missing_name.ttl']

foreach f : ttl_files
  configure_file(
    input: files(f + '.in'),
    output: f,
    configuration: config,
  )
endforeach

test(
  'missing_name',
  executable(
    'test_missing_name',
    files('test_missing_name.c'),
    c_args: c_suppressions + test_args,
    dependencies: [lv2_dep, lilv_dep],
    implicit_include_directories: false,
  ),
  args: [meson.current_build_dir() / ''],
  suite: 'plugin',
)

