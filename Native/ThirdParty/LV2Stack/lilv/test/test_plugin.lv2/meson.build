# Copyright 2021-2022 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

module = shared_module(
  'test_plugin',
  files('test_plugin.c'),
  c_args: c_suppressions,
  dependencies: [lv2_dep, zix_dep],
  gnu_symbol_visibility: 'hidden',
  name_prefix: '',
)

extension = '.' + module.full_path().split('.')[-1]
config = configuration_data({'SHLIB_EXT': extension})
ttl_files = ['manifest.ttl', 'test_plugin.ttl']

foreach f : ttl_files
  configure_file(
    input: files(f + '.in'),
    output: f,
    configuration: config,
  )
endforeach

