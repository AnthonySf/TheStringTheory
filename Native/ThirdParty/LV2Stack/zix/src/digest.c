// Copyright 2012-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/attributes.h>
#include <zix/digest.h>

#include "qualifiers.h"

#include <assert.h>
#include <stdint.h>
#include <string.h>

#if defined(__clang__) && __clang_major__ >= 12
#  define FALLTHROUGH() __attribute__((fallthrough))
#elif defined(__GNUC__) && !defined(__clang__) && __GNUC__ >= 7
#  define FALLTHROUGH() __attribute__((fallthrough))
#else
#  define FALLTHROUGH()
#endif

/*
  64-bit hash: Essentially fasthash64, implemented here in an aligned/padded
  and a general UB-free variant.
*/

static inline uint64_t
mix64(uint64_t h)
{
  h ^= h >> 23U;
  h *= 0x2127599BF4325C37ULL;
  h ^= h >> 47U;
  return h;
}

ZIX_NONBLOCKING uint64_t
zix_digest64(const uint64_t seed, const void* const buf, const size_t len)
{
  ZIX_CONSTEXPR uint64_t m = 0x880355F21E6D1965ULL;

  // Process as many 64-bit blocks as possible
  const size_t         n_blocks   = len / sizeof(uint64_t);
  const uint8_t*       data       = (const uint8_t*)buf;
  const uint8_t* const blocks_end = data + (n_blocks * sizeof(uint64_t));
  uint64_t             h          = seed ^ (len * m);
  for (; data != blocks_end; data += sizeof(uint64_t)) {
    uint64_t k = 0U;
    memcpy(&k, data, sizeof(uint64_t));

    h ^= mix64(k);
    h *= m;
  }

  // Process any trailing bytes
  const uint8_t* const tail = blocks_end;
  uint64_t             v    = 0U;
  switch (len & 7U) {
  case 7:
    v |= (uint64_t)tail[6] << 48U;
    FALLTHROUGH();
  case 6:
    v |= (uint64_t)tail[5] << 40U;
    FALLTHROUGH();
  case 5:
    v |= (uint64_t)tail[4] << 32U;
    FALLTHROUGH();
  case 4:
    v |= (uint64_t)tail[3] << 24U;
    FALLTHROUGH();
  case 3:
    v |= (uint64_t)tail[2] << 16U;
    FALLTHROUGH();
  case 2:
    v |= (uint64_t)tail[1] << 8U;
    FALLTHROUGH();
  case 1:
    v |= (uint64_t)tail[0];

    h ^= mix64(v);
    h *= m;
  }

  return mix64(h);
}

ZIX_NONBLOCKING uint64_t
zix_digest64_aligned(const uint64_t seed, const void* const buf, size_t len)
{
  ZIX_CONSTEXPR uint64_t m = 0x880355F21E6D1965ULL;

  assert((uintptr_t)buf % sizeof(uint64_t) == 0U);
  assert(len % sizeof(uint64_t) == 0U);

  const uint64_t* const blocks   = (const uint64_t*)buf;
  const size_t          n_blocks = len / sizeof(uint64_t);
  uint64_t              h        = seed ^ (len * m);

  for (size_t i = 0U; i < n_blocks; ++i) {
    h ^= mix64(blocks[i]);
    h *= m;
  }

  return mix64(h);
}

/*
  32-bit hash: Essentially murmur3, reimplemented here in an aligned/padded and
  a general UB-free variant.
*/

/**
   Rotate left by some count of bits.

   This is recognized by any halfway decent compiler and compiled to a single
   instruction on architectures that have one.
*/
static inline uint32_t
rotl32(const uint32_t val, const uint32_t bits)
{
  return ((val << bits) | (val >> (32U - bits)));
}

static inline uint32_t
mix32(uint32_t h)
{
  h ^= h >> 16U;
  h *= 0x85EBCA6BU;
  h ^= h >> 13U;
  h *= 0xC2B2AE35U;
  h ^= h >> 16U;
  return h;
}

ZIX_NONBLOCKING uint32_t
zix_digest32(const uint32_t seed, const void* const buf, const size_t len)
{
  ZIX_CONSTEXPR uint32_t c1 = 0xCC9E2D51U;
  ZIX_CONSTEXPR uint32_t c2 = 0x1B873593U;

  // Process as many 32-bit blocks as possible
  const size_t         n_blocks   = len / sizeof(uint32_t);
  const uint8_t*       data       = (const uint8_t*)buf;
  const uint8_t* const blocks_end = data + (n_blocks * sizeof(uint32_t));
  uint32_t             h          = seed;
  for (; data != blocks_end; data += sizeof(uint32_t)) {
    uint32_t k = 0U;
    memcpy(&k, data, sizeof(uint32_t));

    k *= c1;
    k = rotl32(k, 15);
    k *= c2;

    h ^= k;
    h = rotl32(h, 13);
    h = (h * 5U) + 0xE6546B64U;
  }

  // Process any trailing bytes
  uint32_t k = 0U;
  switch (len & 3U) {
  case 3U:
    k ^= (uint32_t)data[2U] << 16U;
    FALLTHROUGH();
  case 2U:
    k ^= (uint32_t)data[1U] << 8U;
    FALLTHROUGH();
  case 1U:
    k ^= (uint32_t)data[0U];

    k *= c1;
    k = rotl32(k, 15U);
    k *= c2;
    h ^= k;
  }

  return mix32(h ^ (uint32_t)len);
}

ZIX_NONBLOCKING uint32_t
zix_digest32_aligned(const uint32_t    seed,
                     const void* const buf,
                     const size_t      len)
{
  ZIX_CONSTEXPR uint32_t c1 = 0xCC9E2D51U;
  ZIX_CONSTEXPR uint32_t c2 = 0x1B873593U;

  assert((uintptr_t)buf % sizeof(uint32_t) == 0U);
  assert(len % sizeof(uint32_t) == 0U);

  const uint32_t* const blocks   = (const uint32_t*)buf;
  const size_t          n_blocks = len / sizeof(uint32_t);
  uint32_t              h        = seed;
  for (size_t i = 0U; i < n_blocks; ++i) {
    uint32_t k = blocks[i];

    k *= c1;
    k = rotl32(k, 15);
    k *= c2;

    h ^= k;
    h = rotl32(h, 13);
    h = (h * 5U) + 0xE6546B64U;
  }

  return mix32(h ^ (uint32_t)len);
}

// Native word size wrapper

ZIX_NONBLOCKING size_t
zix_digest(const size_t seed, const void* const buf, const size_t len)
{
#if UINTPTR_MAX >= UINT64_MAX
  return zix_digest64(seed, buf, len);
#else
  return zix_digest32(seed, buf, len);
#endif
}

ZIX_NONBLOCKING size_t
zix_digest_aligned(const size_t seed, const void* const buf, const size_t len)
{
#if UINTPTR_MAX >= UINT64_MAX
  return zix_digest64_aligned(seed, buf, len);
#else
  return zix_digest32_aligned(seed, buf, len);
#endif
}
