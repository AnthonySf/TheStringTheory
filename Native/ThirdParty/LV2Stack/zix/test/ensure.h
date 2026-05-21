// Copyright 2023 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_TEST_ENSURE_H
#define ZIX_TEST_ENSURE_H

#define ENSURE(ctx, condition, fmt) \
  do {                              \
    if (!(condition)) {             \
      return test_fail(ctx, fmt);   \
    }                               \
  } while (0)

#define ENSUREV(ctx, condition, fmt, ...)      \
  do {                                         \
    if (!(condition)) {                        \
      return test_fail(ctx, fmt, __VA_ARGS__); \
    }                                          \
  } while (0)

#endif // ZIX_TEST_ENSURE_H
