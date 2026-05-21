// Copyright 2011-2024 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_STRING_VIEW_H
#define ZIX_STRING_VIEW_H

#include <zix/allocator.h>
#include <zix/attributes.h>

#include <stdbool.h>
#include <string.h>

ZIX_BEGIN_DECLS

/**
   @defgroup zix_string_view String View
   @ingroup zix_utilities
   @{
*/

/**
   An immutable slice of a string.

   This type is used for many string parameters, to allow referring to slices
   of strings in-place and to avoid redundant string measurement.
*/
typedef struct {
  const char* ZIX_NONNULL data;   ///< Pointer to the first character
  size_t                  length; ///< Length of string in bytes
} ZixStringView;

/// Initialize a string view from a string literal
// clang-format off
#if (defined(__STDC_VERSION__) && __STDC_VERSION__ >= 201112L)
#  define ZIX_STATIC_STRING(s) (ZixStringView) {s, (sizeof(s) - 1U)}
#else
#  define ZIX_STATIC_STRING(s) { s, (sizeof(s) - 1U) }
#endif
// clang-format on

/// Return a view of an empty string
ZIX_ALWAYS_INLINE_FUNC ZIX_CONST_FUNC static inline ZixStringView
zix_empty_string(void)
{
  const ZixStringView view = {"", 0U};
  return view;
}

/**
   Return a view of a substring, or a premeasured string.

   This makes either a view of a slice of a string (which may not be null
   terminated), or a view of a string that has already been measured.  This is
   faster than zix_string() for dynamic strings since it does not call
   `strlen`, so should be used when the length of the string is already known.

   @param str Pointer to the start of the substring.

   @param len Length of the substring in bytes, not including the trailing null
   terminator if present.
*/
ZIX_ALWAYS_INLINE_FUNC ZIX_CONST_FUNC static inline ZixStringView
zix_substring(const char* const ZIX_NONNULL str, const size_t len)
{
  const ZixStringView view = {str, len};
  return view;
}

/**
   Return a view of an entire string by measuring it.

   This makes a view of the given string by measuring it with `strlen`.

   @param str Pointer to the start of a null-terminated C string, or null.
*/
ZIX_ALWAYS_INLINE_FUNC ZIX_PURE_FUNC static inline ZixStringView
// NOLINTNEXTLINE(clang-diagnostic-unused-function)
zix_string(const char* const ZIX_NULLABLE str)
{
  return str ? zix_substring(str, strlen(str)) : zix_empty_string();
}

/**
   Copy a string view into a newly allocated null-terminated string.
*/
ZIX_MALLOC_API char* ZIX_ALLOCATED
zix_string_view_copy(ZixAllocator* ZIX_NULLABLE allocator, ZixStringView view);

/**
   Return true if both string views refer to equal strings.

   This may be significantly faster than a full string comparison, because it
   has fast paths for when the operands have different lengths, or point to the
   same string data.
*/
ZIX_PURE_API ZIX_NONBLOCKING bool
zix_string_view_equals(ZixStringView lhs, ZixStringView rhs);

/**
   @}
*/

ZIX_END_DECLS

#endif // ZIX_STRING_VIEW_H
