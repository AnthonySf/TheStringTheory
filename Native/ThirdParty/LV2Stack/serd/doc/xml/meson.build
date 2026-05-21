# Copyright 2020-2022 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

config = configuration_data()
config.set('SERD_SRCDIR', serd_src_root)
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

    'group__serd.xml',
    'group__serd__env.xml',
    'group__serd__event.xml',
    'group__serd__node.xml',
    'group__serd__reader.xml',
    'group__serd__status.xml',
    'group__serd__streams.xml',
    'group__serd__string.xml',
    'group__serd__uri.xml',
    'group__serd__writer.xml',
    'serd_8h.xml',
    'structSerdChunk.xml',
    'structSerdError.xml',
    'structSerdNode.xml',
    'structSerdURI.xml',
  ],
)
