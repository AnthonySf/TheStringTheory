# Copyright 2021-2022 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

docdir = get_option('datadir') / 'doc'

# Find required programs
doxygen = find_program('doxygen', required: get_option('docs'))
sphinx_build = find_program('sphinx-build', required: get_option('docs'))

# Find sphinxygen or fall back to subproject
sphinxygen = disabler()
if doxygen.found() and sphinx_build.found()
  sphinxygen = find_program('sphinxygen', required: false)
  if not sphinxygen.found()
    subproject('sphinxygen')
    sphinxygen = find_program('sphinxygen', required: get_option('docs'))
  endif
endif

# Build documentation if all required tools are found
build_docs = doxygen.found() and sphinxygen.found() and sphinx_build.found()
if build_docs
  # Configure conf.py for Sphinx
  conf_config = configuration_data()
  conf_config.set('LILV_SRCDIR', lilv_src_root)
  conf_config.set('LILV_TITLE', get_option('title'))
  conf_config.set('LILV_VERSION', meson.project_version())
  conf_py = configure_file(
    configuration: conf_config,
    input: files('conf.py.in'),
    output: 'conf.py',
  )

  # Copy hand-written documentation files
  rst_sources = files(
    'index.rst',
    'overview.rst',
    'plugins.rst',
    'uis.rst',
    'world.rst',
  )
  sphinx_input = []
  foreach f : rst_sources
    sphinx_input += [
      configure_file(copy: true, input: f, output: '@PLAINNAME@'),
    ]
  endforeach

  # Generate reference documentation input with Doxygen and Sphinxygen
  subdir('xml')
  subdir('api')

  # Build strict Sphinx flags, with termination on warnings if werror=true
  sphinx_in_dir = meson.current_build_dir()
  sphinx_flags = ['-E', '-a', '-q']
  if get_option('werror')
    sphinx_flags += ['-W']
  endif

  # Run Sphinx to generate final documentation for each format
  sphinx_build_command = [sphinx_build] + sphinx_flags
  foreach format : ['html', 'singlehtml']
    if not get_option(format).disabled()
      subdir(format)
    endif
  endforeach
endif

if not meson.is_subproject()
  summary('Documentation', build_docs, bool_yn: true, section: 'Components')
endif
