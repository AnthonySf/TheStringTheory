# Copyright 2021-2022 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

doxygen = find_program('doxygen')

config = configuration_data()
config.set('LILV_SRCDIR', lilv_src_root)
config.set('DOX_OUTPUT', meson.current_build_dir() / '..')

doxyfile = configure_file(
  configuration: config,
  input: files('../Doxyfile.in'),
  output: 'Doxyfile',
)

doxygen_xml = custom_target(
  'index.xml',
  command: [doxygen, '@INPUT0@'],
  input: [doxyfile] + c_headers,
  output: [
    'index.xml',

    'group__lilv.xml',
    'group__lilv__class.xml',
    'group__lilv__collections.xml',
    'group__lilv__instance.xml',
    'group__lilv__node.xml',
    'group__lilv__plugin.xml',
    'group__lilv__port.xml',
    'group__lilv__scalepoint.xml',
    'group__lilv__state.xml',
    'group__lilv__ui.xml',
    'group__lilv__world.xml',
    'lilv_8h.xml',
  ],
)
