# Copyright 2020-2022 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

###################
# Header Warnings #
###################

subdir('headers')

##############
# Test Setup #
##############

test_args = []
if cc.get_id() == 'msvc'
  test_args += [
    '/wd4464', # relative include path contains '..'
  ]
endif

subdir('lv2/core.lv2')

################
# Bundle Tests #
################

subdir('bad_syntax.lv2')
subdir('failed_instantiation.lv2')
subdir('failed_lib_descriptor.lv2')
subdir('lib_descriptor.lv2')
subdir('missing_descriptor.lv2')
subdir('missing_name.lv2')
subdir('missing_plugin.lv2')
subdir('missing_port.lv2')
subdir('missing_port_name.lv2')
subdir('new_version.lv2')
subdir('old_version.lv2')
subdir('test_plugin.lv2')

################
# C++ Bindings #
################

if is_variable('cpp')
  subdir('cpp')
endif

##############
# Unit Tests #
##############

unit_tests = [
  'bad_port_index',
  'bad_port_symbol',
  'classes',
  'discovery',
  'get_symbol',
  'no_author',
  'no_verify',
  'plugin',
  'port',
  'preset',
  'project',
  'project_no_author',
  'prototype',
  'reload_bundle',
  'replace_version',
  'state',
  'ui',
  'util',
  'value',
  'verify',
  'world',
]

define_args = [
  '-DLILV_TEST_BUNDLE="@0@/"'.format(
    meson.current_build_dir() / 'test_plugin.lv2',
  ),
  '-DLILV_TEST_DIR="@0@/"'.format(meson.current_build_dir()),
]

foreach unit : unit_tests
  test(
    unit,
    executable(
      'test_@0@'.format(unit),
      files('lilv_test_utils.c', 'test_@0@.c'.format(unit)),
      c_args: define_args + test_args + c_suppressions,
      dependencies: [lv2_dep, lilv_dep],
      implicit_include_directories: false,
    ),
    suite: 'unit',
  )
endforeach

########
# Lint #
########

if get_option('lint')
  subdir('lint')
endif
