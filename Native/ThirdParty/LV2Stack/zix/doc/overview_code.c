// Copyright 2021-2024 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

/*
  Example code that is included in the documentation.  Code in the
  documentation is included from here rather than written inline so that it can
  be tested and avoid rotting.  The code here doesn't make much sense, but is
  written such that it at least compiles and will run without crashing.
*/

#include <zix/attributes.h>
#include <zix/string_view.h>

static void
string_views(void)
{
  const char* const string_pointer = "some string";

  // begin make-empty-string
  ZixStringView empty = zix_empty_string();
  // end make-empty-string
  (void)empty;

  // begin make-static-string
  static const ZixStringView hello = ZIX_STATIC_STRING("hello");
  // end make-static-string
  (void)hello;

  // begin measure-string
  ZixStringView view = zix_string(string_pointer);
  // end measure-string
  (void)view;

  // begin make-string-view
  ZixStringView slice = zix_substring(string_pointer, 4);
  // end make-string-view
  (void)slice;
}

ZIX_CONST_FUNC int
main(void)
{
  string_views();
  return 0;
}
