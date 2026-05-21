// Copyright 2012-2022 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_DIGEST_H
#define ZIX_DIGEST_H

#include <zix/attributes.h>

#include <stddef.h>
#include <stdint.h>

ZIX_BEGIN_DECLS

/**
   @defgroup zix_digest Digest
   @ingroup zix_algorithms

   Functions to generate a short "digest" of data with minimal collisions.

   These are good general-purpose hash functions for indexing arbitrary data,
   but are not necessarily stable across platforms and should never be used for
   cryptographic purposes.

   @{
*/

/**
   Return a 32-bit hash of a buffer.

   This can be used for any size or alignment.
*/
ZIX_PURE_API ZIX_NONBLOCKING uint32_t
zix_digest32(uint32_t seed, const void* ZIX_NONNULL buf, size_t len);

/**
   Return a 32-bit hash of an aligned buffer.

   Both the buffer and size must be aligned to 32 bits.  For data that fits
   these requirements, this is equivalent to, but faster than, zix_digest32().
*/
ZIX_PURE_API ZIX_NONBLOCKING uint32_t
zix_digest32_aligned(uint32_t seed, const void* ZIX_NONNULL buf, size_t len);

/**
   Return a 64-bit hash of a buffer.

   This can be used for any size or alignment.
*/
ZIX_PURE_API ZIX_NONBLOCKING uint64_t
zix_digest64(uint64_t seed, const void* ZIX_NONNULL buf, size_t len);

/**
   Return a 64-bit hash of an aligned buffer.

   Both the buffer and size must be aligned to 64 bits.  For data that fits
   these requirements, this is equivalent to, but faster than, zix_digest64().
*/
ZIX_PURE_API ZIX_NONBLOCKING uint64_t
zix_digest64_aligned(uint64_t seed, const void* ZIX_NONNULL buf, size_t len);

/**
   Return a pointer-sized hash of a buffer.

   This can be used for any size or alignment.

   Internally, this simply dispatches to zix_digest32() or zix_digest64() as
   appropriate.
*/
ZIX_PURE_API ZIX_NONBLOCKING size_t
zix_digest(size_t seed, const void* ZIX_NONNULL buf, size_t len);

/**
   Return a pointer-sized hash of an aligned buffer.

   Both the buffer and size must be aligned to the pointer size.  For data that
   fits these requirements, this is equivalent to, but faster than,
   zix_digest().

   Internally, this simply dispatches to zix_digest32_aligned() or
   zix_digest64_aligned() as appropriate.
*/
ZIX_PURE_API ZIX_NONBLOCKING size_t
zix_digest_aligned(size_t seed, const void* ZIX_NONNULL buf, size_t len);

/**
   @}
*/

ZIX_END_DECLS

#endif /* ZIX_DIGEST_H */
