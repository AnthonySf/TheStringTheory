// Copyright 2011-2015 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef SORD_SORD_INTERNAL_H
#define SORD_SORD_INTERNAL_H

#include <serd/serd.h>
#include <sord/sord.h>

#include <stddef.h>

#if defined(__clang__) || (defined(__GNUC__) && __GNUC__ > 4)
#  define SORD_UNREACHABLE() __builtin_unreachable()
#elif defined(_MSC_VER)
#  define SORD_UNREACHABLE() __assume(0)
#else
#  define SORD_UNREACHABLE()
#endif

/** Resource node metadata */
typedef struct {
  size_t refs_as_obj; ///< References as a quad object
} SordResourceMetadata;

/** Literal node metadata */
typedef struct {
  SordNode* datatype; ///< Optional literal data type URI
  char      lang[16]; ///< Optional language tag
} SordLiteralMetadata;

/** Node */
struct SordNodeImpl {
  SerdNode node; ///< Serd node
  size_t   refs; ///< Reference count (# of containing quads)
  union {
    SordResourceMetadata res;
    SordLiteralMetadata  lit;
  } meta;
};

#endif /* SORD_SORD_INTERNAL_H */
