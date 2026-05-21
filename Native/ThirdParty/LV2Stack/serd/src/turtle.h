// Copyright 2011-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef SERD_SRC_TURTLE_H
#define SERD_SRC_TURTLE_H

#include "ntriples.h"

#include <stdbool.h>

/// [164s] PN_CHARS_U
static inline bool
is_PN_CHARS_U(const int c)
{
  return c == '_' || is_PN_CHARS_BASE(c);
}

/// [166s] PN_CHARS
static inline bool
is_PN_CHARS(const int c)
{
  return (is_PN_CHARS_U(c) || c == '-' || (c >= '0' && c <= '9') || c == 0xB7 ||
          (c >= 0x0300 && c <= 0x036F) || (c >= 0x203F && c <= 0x2040));
}

/// [172s] PN_LOCAL_ESC
static inline bool
is_PN_LOCAL_ESC(const int c)
{
  return (c >= '#' && c <= '/') || (c == '!') || (c == ';') || (c == '=') ||
         (c == '?') || (c == '@') || (c == '_') || (c == '~');
}

#endif // SERD_SRC_TURTLE_H
