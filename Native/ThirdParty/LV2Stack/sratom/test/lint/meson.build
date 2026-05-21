# Copyright 2021-2024 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

all_sources = sources + unit_test_sources

# Check licensing metadata
reuse = find_program('reuse', required: false)
if reuse.found()
  reuse_args = ['--root', sratom_src_root, 'lint']
  test('REUSE', reuse, args: reuse_args, suite: 'data')
endif

# Check code formatting
clang_format = find_program('clang-format', required: false)
if clang_format.found()
  clang_format_args = ['--Werror', '--dry-run'] + c_headers + all_sources
  test('format', clang_format, args: clang_format_args, suite: 'code')
endif

if not meson.is_subproject()
  # Check release metadata
  autoship = find_program('autoship', required: false)
  if autoship.found()
    test('autoship', autoship, args: ['test', sratom_src_root], suite: 'data')
  endif

  # Check code with cppcheck
  cppcheck = find_program('cppcheck', required: false)
  if cppcheck.found()
    compdb_path = join_paths(sratom_build_root, 'compile_commands.json')
    suppress_path = join_paths(sratom_src_root, '.suppress.cppcheck')
    cppcheck_args = [
      '--enable=warning,style,performance,portability',
      '--error-exitcode=1',
      '--project=' + compdb_path,
      '--suppressions-list=' + suppress_path,
      '-q',
    ]
    test('cppcheck', cppcheck, args: cppcheck_args, suite: 'code')
  endif
endif
