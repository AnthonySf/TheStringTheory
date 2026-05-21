# Copyright 2021-2023 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

html_dir = docdir / versioned_name / 'html'

# TODO: Add install_tag: 'doc' after requiring meson 0.60.0

custom_target(
  'html',
  build_by_default: true,
  command: sphinx_build_command + [
    '-b', 'html',
    '-t', 'html',
    sphinx_in_dir,
    '@OUTDIR@',
  ],
  input: [api_serd_rst, conf_py, sphinx_input],
  install: true,
  install_dir: html_dir,
  output: [
    'index.html',

    '_static',
    'api',
    'genindex.html',
    'overview.html',
  ],
)

if not meson.is_subproject()
  summary('HTML', get_option('prefix') / html_dir, section: 'Directories')
endif
