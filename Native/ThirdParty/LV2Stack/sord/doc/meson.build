# Copyright 2021-2022 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

docdir = get_option('datadir') / 'doc'
doxygen = find_program('doxygen', required: get_option('docs'))

build_docs = doxygen.found()
if build_docs
  config = configuration_data()
  config.set('SORD_VERSION', meson.project_version())
  config.set('SORD_SRCDIR', sord_src_root)
  config.set('DOX_OUTPUT', meson.current_build_dir())

  c_doxyfile = configure_file(
    configuration: config,
    input: files('reference.doxygen.in'),
    output: 'reference.doxygen',
  )

  custom_target(
    'html',
    build_by_default: true,
    command: [doxygen, '@INPUT0@'],
    input: [c_doxyfile] + c_headers,
    install: true,
    install_dir: docdir / versioned_name,
    output: 'html',
  )
endif

if not meson.is_subproject()
  summary('API Documentation', build_docs, bool_yn: true, section: 'Components')
endif
