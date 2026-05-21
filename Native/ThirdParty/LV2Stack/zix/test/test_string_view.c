// Copyright 2021-2024 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "failing_allocator.h"

#include <zix/allocator.h>
#include <zix/string_view.h>

#include <assert.h>
#include <string.h>

static void
test_static_init(void)
{
  static const ZixStringView a  = ZIX_STATIC_STRING("a");
  static const ZixStringView ab = ZIX_STATIC_STRING("ab");

  assert(a.length == 1U);
  assert(a.data);
  assert(a.data[0] == 'a');
  assert(a.data[1] == '\0');

  assert(ab.length == 2U);
  assert(ab.data[0] == 'a');
  assert(ab.data[1] == 'b');
  assert(ab.data[2] == '\0');
}

static void
test_empty(void)
{
  const ZixStringView empty = zix_empty_string();

  assert(!empty.length);
  assert(empty.data);
  assert(empty.data[0] == '\0');
}

static void
test_string(void)
{
  const ZixStringView nodata = zix_string(NULL);
  const ZixStringView empty  = zix_string("");

  assert(!nodata.length);
  assert(nodata.data);
  assert(nodata.data[0] == '\0');

  assert(!empty.length);
  assert(empty.data);
  assert(empty.data[0] == '\0');
}

static void
test_equals(void)
{
  static const char* const prefix_str = "prefix";

  const ZixStringView prefix  = zix_string(prefix_str);
  const ZixStringView pre     = zix_substring(prefix_str, 3U);
  const ZixStringView fix     = zix_substring(prefix_str + 3U, 3U);
  const ZixStringView suffix1 = zix_substring("suffix_1", 6U);
  const ZixStringView suffix2 = zix_substring("suffix_2", 6U);

  assert(prefix.length == 6U);
  assert(pre.length == 3U);
  assert(fix.length == 3U);
  assert(suffix1.length == 6U);
  assert(suffix2.length == 6U);

  assert(zix_string_view_equals(prefix, zix_string("prefix")));
  assert(zix_string_view_equals(pre, zix_string("pre")));
  assert(zix_string_view_equals(fix, zix_string("fix")));
  assert(zix_string_view_equals(suffix1, zix_string("suffix")));
  assert(zix_string_view_equals(suffix2, zix_string("suffix")));

  assert(zix_string_view_equals(prefix, prefix));
  assert(zix_string_view_equals(suffix1, suffix2));

  assert(!zix_string_view_equals(prefix, pre));
  assert(!zix_string_view_equals(pre, prefix));
  assert(!zix_string_view_equals(pre, fix));
  assert(!zix_string_view_equals(fix, prefix));
  assert(!zix_string_view_equals(suffix1, prefix));
  assert(!zix_string_view_equals(prefix, suffix1));
}

static void
test_copy(void)
{
  static const ZixStringView orig = ZIX_STATIC_STRING("string");

  ZixFailingAllocator allocator = zix_failing_allocator();

  // Copying a string takes exactly one allocation
  zix_failing_allocator_reset(&allocator, 1U);

  char* const copy = zix_string_view_copy(&allocator.base, orig);
  assert(copy);
  assert(!strcmp(copy, "string"));
  zix_free(&allocator.base, copy);

  // Check that allocation failure is handled gracefully
  zix_failing_allocator_reset(&allocator, 0U);
  assert(!zix_string_view_copy(&allocator.base, orig));
}

int
main(void)
{
  test_static_init();
  test_empty();
  test_string();
  test_equals();
  test_copy();
  return 0;
}
