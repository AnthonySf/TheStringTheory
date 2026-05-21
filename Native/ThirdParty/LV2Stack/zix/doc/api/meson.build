# Copyright 2021-2023 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

api_zix_rst = custom_target(
  'zix.rst',
  command: [sphinxygen, '-f', '@INPUT0@', '@OUTDIR@'],
  input: doxygen_xml,
  output: 'zix.rst',
)
