// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/string_view.h>

#include <zix/allocator.h>
#include <zix/attributes.h>

#include <stdbool.h>
#include <string.h>

char*
zix_string_view_copy(ZixAllocator* const allocator, const ZixStringView view)
{
  char* const copy = (char*)zix_malloc(allocator, view.length + 1U);
  if (copy) {
    memcpy(copy, view.data, view.length);
    copy[view.length] = '\0';
  }
  return copy;
}

ZIX_NONBLOCKING bool
zix_string_view_equals(const ZixStringView lhs, const ZixStringView rhs)
{
  if (lhs.length != rhs.length) {
    return false;
  }

  if (lhs.data != rhs.data) {
    for (size_t i = 0U; i < lhs.length; ++i) {
      if (lhs.data[i] != rhs.data[i]) {
        return false;
      }
    }
  }

  return true;
}
