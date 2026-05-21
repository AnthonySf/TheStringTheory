# Copyright 2021-2026 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

###################
# Header Warnings #
###################

subdir('headers')

##############
# Unit Tests #
##############

common_test_sources = files(
  'forge_test_object.c',
  'test_uri_map.c',
)

unit_test_names = [
  'trip',
  'write',
]

unit_test_sources = common_test_sources

foreach name : unit_test_names
  sources = files('test_@0@.c'.format(name), 'test_uri_map.c')
  unit_test_sources += sources

  test(
    name,
    executable(
      'test_' + name,
      common_test_sources + sources,
      c_args: c_suppressions,
      dependencies: [lv2_dep, serd_dep, sratom_dep],
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
