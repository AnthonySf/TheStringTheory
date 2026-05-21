# Copyright 2024 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

if not meson.is_subproject()
  # Check release metadata
  autoship = find_program('autoship', required: get_option('tests'))
  if autoship.found()
    test('autoship', autoship, args: ['test', zix_src_root], suite: 'data')
  endif

  # Check code with cppcheck
  cppcheck = find_program('cppcheck', required: false)
  if cppcheck.found()
    compdb_path = join_paths(zix_build_root, 'compile_commands.json')
    cppcheck_args = [
      '--cppcheck-build-dir=' + meson.current_build_dir(),
      '--enable=warning,style,performance,portability',
      '--error-exitcode=1',
      '--project=' + compdb_path,
      '--suppress=arrayIndexOutOfBoundsCond',
      '--suppress=constParameterCallback',
      '--suppress=constParameterPointer',
      '--suppress=normalCheckLevelMaxBranches',
      '--suppress=unreadVariable',
      '-q',
    ]
    test('cppcheck', cppcheck, args: cppcheck_args, suite: 'code')
  endif
endif

# Check licensing metadata
reuse = find_program('reuse', required: get_option('tests'))
if reuse.found()
  reuse_args = ['--root', zix_src_root, 'lint']
  test('REUSE', reuse, args: reuse_args, suite: 'data')
endif

# Check code formatting
clang_format = find_program('clang-format', required: false)
if clang_format.found()
  clang_format_args = ['--Werror', '--dry-run'] + c_headers + sources
  test('format', clang_format, args: clang_format_args, suite: 'code')
endif
