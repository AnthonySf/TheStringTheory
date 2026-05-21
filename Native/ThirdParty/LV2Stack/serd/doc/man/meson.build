# Copyright 2020-2023 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: 0BSD OR ISC

mandoc_css = files('mandoc.css')

if get_option('lint')
  stylelint = find_program('stylelint', required: get_option('tests'))
  if stylelint.found()
    test('stylelint', stylelint, args: [mandoc_css], suite: 'data')
  endif

  prettier = find_program('prettier', required: get_option('tests'))
  if prettier.found()
    test(
      'prettier',
      prettier,
      args: ['--parser', 'css', '-c', mandoc_css],
      suite: 'data',
      workdir: meson.current_source_dir(),
    )
  endif
endif

if not get_option('tools').disabled()
  # Install tool man pages
  if not get_option('man').disabled()
    if not meson.is_subproject()
      summary(
        'Man pages',
        get_option('prefix') / get_option('mandir'),
        section: 'Directories',
      )
    endif

    install_man(files('serdi.1'))
  endif

  # Build/install HTML man pages if mandoc is present
  mandoc = find_program('mandoc', required: get_option('man_html'))
  if mandoc.found()
    configure_file(
      copy: true,
      input: mandoc_css,
      output: 'mandoc.css',
      install_dir: docdir / versioned_name / 'man',
    )

    mandoc_html_command = [
      mandoc,
      '-Kutf-8',
      '-Ostyle=mandoc.css,man=%N.html',
      '-Ios=Serd @0@'.format(meson.project_version()),
      '-Thtml',
      '-Wwarning,stop',
      '@INPUT@',
    ]

    page_names = [
      'serdi',
    ]

    html_mandir = docdir / versioned_name / 'man'
    foreach name : page_names
      custom_target(
        name + '.html',
        capture: true,
        command: mandoc_html_command,
        input: files(name + '.1'),
        install: true,
        install_dir: html_mandir,
        output: name + '.html',
      )
    endforeach

    if not meson.is_subproject()
      summary(
        'HTML man pages',
        get_option('prefix') / html_mandir,
        section: 'Directories',
      )
    endif
  endif
endif
