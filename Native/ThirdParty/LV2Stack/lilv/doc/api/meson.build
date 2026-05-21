# Copyright 2021-2022 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

api_lilv_rst = custom_target(
  'lilv.rst',
  command: [sphinxygen, '-f', '@INPUT0@', '@OUTDIR@'],
  input: doxygen_xml,
  output: 'lilv.rst',
)
