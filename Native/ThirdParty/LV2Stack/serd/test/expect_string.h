// Copyright 2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef SERD_TEST_EXPECT_STRING_H
#define SERD_TEST_EXPECT_STRING_H

#include <assert.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>

static inline bool
expect_string(const char* const actual, const char* const expected)
{
  assert(expected);
  const bool equal = actual && !strcmp(actual, expected);
  if (!equal) {
    fprintf(stderr, "Expected:\n%s\n\n", expected);
    fprintf(stderr, "Actual:\n%s\n\n", actual ? actual : "(null)");
  }
  return equal;
}

#endif // SERD_TEST_EXPECT_STRING_H
