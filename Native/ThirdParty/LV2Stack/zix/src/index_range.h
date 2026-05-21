// Copyright 2022 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_INDEX_RANGE_H
#define ZIX_INDEX_RANGE_H

#include <stdbool.h>
#include <stddef.h>

typedef struct {
  size_t begin; ///< Index to the first character
  size_t end;   ///< Index one past the last character
} ZixIndexRange;

static inline ZixIndexRange
zix_make_range(const size_t begin, const size_t end)
{
  const ZixIndexRange result = {begin, end};
  return result;
}

static inline bool
zix_is_empty_range(const ZixIndexRange range)
{
  return range.begin == range.end;
}

#endif // ZIX_INDEX_RANGE_H
