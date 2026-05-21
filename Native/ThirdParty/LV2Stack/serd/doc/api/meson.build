# Copyright 2020-2023 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

api_serd_rst = custom_target(
  'serd.rst',
  command: [sphinxygen, '-f', '@INPUT0@', '@OUTDIR@'],
  input: doxygen_xml,
  output: 'serd.rst',
)
