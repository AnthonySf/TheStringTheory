// Copyright 2022 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_TEST_ARGS_H
#define ZIX_TEST_ARGS_H

#include <stddef.h>
#include <stdlib.h>

static inline size_t
zix_test_size_arg(const char* const string, const size_t min, const size_t max)
{
  const size_t size = strtoul(string, NULL, 10);

  return size < min ? min : size > max ? max : size;
}

#endif // ZIX_TEST_ARGS_H
