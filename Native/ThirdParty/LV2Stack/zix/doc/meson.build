# Copyright 2021-2023 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

docdir = get_option('datadir') / 'doc'

# Find required programs
doxygen = find_program('doxygen', required: get_option('docs'))
sphinx_build = find_program('sphinx-build', required: get_option('docs'))

# Find sphinxygen or fall back to subproject
if doxygen.found() and sphinx_build.found()
  sphinxygen = find_program('sphinxygen', required: false)
  if not sphinxygen.found()
    subproject('sphinxygen')
    sphinxygen = find_program('sphinxygen', required: get_option('docs'))
  endif
else
  sphinxygen = disabler()
endif

# Build documentation if all required tools are found
build_docs = doxygen.found() and sphinx_build.found() and sphinxygen.found()
if build_docs
  # Warn if the "official" theme isn't present
  pymod = import('python')
  doc_modules = ['sphinx_lv2_theme']
  py = pymod.find_installation('python3', modules: doc_modules, required: false)
  if not py.found()
    warning('Missing sphinx_lv2_theme module, falling back to alabaster')
  endif

  # Documentation Code

  if not get_option('tests').disabled()
    test(
      'overview_code',
      executable(
        'overview_code',
        files('overview_code.c'),
        c_args: c_suppressions,
        dependencies: [zix_dep],
        implicit_include_directories: false,
      ),
      suite: 'doc',
    )
  endif

  # Generated API Reference

  # Configure conf.py for Sphinx
  config = configuration_data()
  config.set('ZIX_SRCDIR', zix_src_root)
  config.set('ZIX_TITLE', get_option('title'))
  config.set('ZIX_VERSION', meson.project_version())
  conf_py = configure_file(
    configuration: config,
    input: files('conf.py.in'),
    output: 'conf.py',
  )

  # Copy hand-written documentation files
  rst_sources = files(
    'allocation.rst',
    'error_handling.rst',
    'index.rst',
    'string_views.rst',
    'using_zix.rst',
  )
  sphinx_input = []
  foreach f : rst_sources
    sphinx_input += [
      configure_file(copy: true, input: f, output: '@PLAINNAME@'),
    ]
  endforeach

  # Copy documentation code
  doc_c_sources = files('overview_code.c')
  configure_file(copy: true, input: doc_c_sources, output: '@PLAINNAME@')

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
