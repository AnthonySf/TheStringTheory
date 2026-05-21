# Copyright 2025 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

pymod = import('python')
py = pymod.find_installation('python3', required: get_option('bindings_py'))

if py.found() and not meson.is_subproject()
  test(
    'python',
    py,
    args: ['-m', 'unittest', 'discover', meson.current_source_dir()],
    depends: [liblilv],
    env: ['PYTHONPATH=' + meson.current_source_dir() / '..' / 'python'],
    suite: 'bindings',
  )
endif
