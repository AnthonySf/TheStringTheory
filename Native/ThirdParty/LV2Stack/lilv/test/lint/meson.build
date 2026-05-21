# Copyright 2020-2024 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

# Check licensing metadata
reuse = find_program('reuse', required: false)
if reuse.found()
  reuse_args = ['--root', lilv_src_root, 'lint']
  test('REUSE', reuse, args: reuse_args, suite: 'data')
endif

if not meson.is_subproject()
  # Check release metadata
  autoship = find_program('autoship', required: false)
  if autoship.found()
    test('autoship', autoship, args: ['test', lilv_src_root], suite: 'data')
  endif

  # Check code with cppcheck
  cppcheck = find_program('cppcheck', required: false)
  if cppcheck.found()
    compdb_path = join_paths(lilv_build_root, 'compile_commands.json')
    suppress_path = join_paths(lilv_src_root, '.suppress.cppcheck')
    test(
      'cppcheck',
      cppcheck,
      args: [
        '--cppcheck-build-dir=' + meson.current_build_dir(),
        '--enable=warning,style,performance,portability',
        '--error-exitcode=1',
        '--project=' + compdb_path,
        '--suppressions-list=' + suppress_path,
        '-DLILV_VERSION="\"@0@\""'.format(meson.project_version()),
        '-q',
      ],
      suite: 'code',
    )
  endif
endif
