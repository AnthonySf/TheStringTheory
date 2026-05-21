# Copyright 2020-2023 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

plot_script_paths = [
  '../../scripts/serd_bench.py',
]

simple_script_paths = [
  '../../scripts/check_formatting.py',
  '../serd_test_util/__init__.py',
  '../run_suite.py',
  '../test_quiet.py',
  '../test_stdin.py',
  '../test_write_error.py',
]

ttl_metadata_file_paths = [
  '../../serd.ttl',
  '../extra/abbreviate/manifest.ttl',
  '../extra/bad/manifest.ttl',
  '../extra/big/manifest.ttl',
  '../extra/full/manifest.ttl',
  '../extra/good/manifest.ttl',
  '../extra/lax/manifest.ttl',
  '../extra/perfect/manifest.ttl',
  '../extra/prefix/manifest.ttl',
  '../extra/pretty/manifest.ttl',
  '../extra/qualify/manifest.ttl',
  '../extra/root/manifest.ttl',
]

plot_scripts = files(plot_script_paths)
simple_scripts = files(simple_script_paths)
python_script_paths = simple_script_paths + plot_script_paths
python_scripts = plot_scripts + simple_scripts

all_sources = sources + unit_test_sources + files('../../src/serdi.c')

# Check licensing metadata
reuse = find_program('reuse', required: false)
if reuse.found()
  test(
    'REUSE',
    reuse,
    args: ['--root', serd_src_root, 'lint'],
    suite: 'data',
  )
endif

# Check code formatting
clang_format = find_program('clang-format', required: false)
if clang_format.found()
  test(
    'format',
    clang_format,
    args: ['--Werror', '--dry-run'] + c_headers + all_sources,
    suite: 'code',
  )
endif

# Check script formatting
black = find_program('black', required: false)
if black.found()
  black_opts = ['--check', '-q', '-l', '79']
  foreach script_path : python_script_paths
    script = files(script_path)
    name = 'black_' + script_path.substring(3).underscorify()
    test(name, black, args: black_opts + [script], suite: 'scripts')
  endforeach
endif

# Check scripts for errors with flake8
flake8 = find_program('flake8', required: false)
if flake8.found()
  test('flake8', flake8, args: python_scripts, suite: 'scripts')
endif

# Check scripts for errors with pylint
pylint = find_program('pylint', required: false)
if pylint.found()
  pymod = import('python')
  plot_py = pymod.find_installation(
    'python3',
    modules: ['matplotlib'],
    required: false,
  )

  pylint_args = ['--disable', 'bad-option-value'] + simple_scripts
  if plot_py.found()
    pylint_args += plot_scripts
  endif

  test('pylint', pylint, args: pylint_args, suite: 'scripts')
endif

# Check Turtle formatting with serdi
if is_variable('serdi')
  foreach ttl_file_path : ttl_metadata_file_paths
    test(
      ttl_file_path.substring(3).underscorify(),
      check_formatting_py,
      args: [files(ttl_file_path), serdi, '-o', 'turtle'],
      suite: 'data',
    )
  endforeach
endif

if not meson.is_subproject()
  # Check release metadata
  autoship = find_program('autoship', required: false)
  if autoship.found()
    test('autoship', autoship, args: ['test', serd_src_root], suite: 'data')
  endif

  # Check code with cppcheck
  cppcheck = find_program('cppcheck', required: false)
  if cppcheck.found()
    compdb_path = join_paths(serd_build_root, 'compile_commands.json')
    suppress_path = join_paths(serd_src_root, '.suppress.cppcheck')
    test(
      'cppcheck',
      cppcheck,
      args: [
        '--enable=warning,style,performance,portability',
        '--error-exitcode=1',
        '--project=' + compdb_path,
        '--suppressions-list=' + suppress_path,
        '-q',
      ],
      suite: 'code',
    )
  endif
endif
