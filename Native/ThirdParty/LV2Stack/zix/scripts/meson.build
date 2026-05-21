# Copyright 2020-2022 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

if not meson.is_subproject() and get_option('lint')
  simple_scripts = files('benchmark.py')
  plot_scripts = files('plot.py')
  all_scripts = simple_scripts + plot_scripts

  # Check formatting with black
  black = find_program('black', required: get_option('tests'))
  if black.found()
    black_opts = ['-l', '79', '-q', '--check']
    test('black', black, args: black_opts + all_scripts, suite: 'scripts')
  endif

  # Check for errors with flake8
  flake8 = find_program('flake8', required: get_option('tests'))
  if flake8.found()
    test('flake8', flake8, args: all_scripts, suite: 'scripts')
  endif

  # Check for errors with pylint
  pylint = find_program('pylint', required: get_option('tests'))
  if pylint.found()
    pymod = import('python')
    plot_py = pymod.find_installation(
      'python3',
      modules: ['matplotlib'],
      required: false,
    )

    all_scripts = simple_scripts + plot_scripts
    lint_scripts = simple_scripts
    if plot_py.found()
      lint_scripts += plot_scripts
    endif

    test('pylint', pylint, args: lint_scripts, suite: 'scripts')
  endif
endif
