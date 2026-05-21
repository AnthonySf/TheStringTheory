// Copyright 2011-2023 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#define ZIX_HASH_KEY_TYPE const char
#define ZIX_HASH_RECORD_TYPE const char

#include "ensure.h"
#include "failing_allocator.h"
#include "test_data.h"

#include <zix/allocator.h>
#include <zix/attributes.h>
#include <zix/digest.h>
#include <zix/hash.h>
#include <zix/status.h>

#include <assert.h>
#include <inttypes.h>
#include <stdarg.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef struct {
  ZixHash* hash;
  char*    buffer;
  char**   strings;
} TestState;

ZIX_LOG_FUNC(2, 3)
static int
test_fail(TestState* const state, const char* fmt, ...)
{
  va_list args; // NOLINT(cppcoreguidelines-init-variables)
  va_start(args, fmt);
  fprintf(stderr, "error: ");
  vfprintf(stderr, fmt, args);
  va_end(args);

  zix_hash_free(state->hash);
  free(state->buffer);
  free(state->strings);
  return EXIT_FAILURE;
}

ZIX_PURE_FUNC static const char*
identity(const char* record)
{
  return record;
}

/// Decent hash function using zix_digest (murmur2)
ZIX_PURE_FUNC static size_t
decent_string_hash(const char* const str)
{
  return zix_digest(0U, str, strlen(str));
}

/// Terrible hash function from K&R first edition
ZIX_PURE_FUNC static size_t
terrible_string_hash(const char* str)
{
  size_t  hash = 0U;
  uint8_t c    = 0U;

  while ((c = (uint8_t)*str++)) {
    hash += c;
  }

  return hash;
}

ZIX_PURE_FUNC static size_t
string_hash_aligned(const char* const str)
{
  size_t length = strlen(str);

  length = (length + (sizeof(size_t) - 1)) / sizeof(size_t) * sizeof(size_t);
  return zix_digest_aligned(0U, str, length);
}

ZIX_PURE_FUNC static size_t
string_hash32(const char* const str)
{
  return (size_t)zix_digest32(0U, str, strlen(str));
}

ZIX_PURE_FUNC static size_t
string_hash64(const char* const str)
{
  return (size_t)zix_digest64(0U, str, strlen(str));
}

ZIX_PURE_FUNC static size_t
string_hash32_aligned(const char* const str)
{
  size_t length = strlen(str);

  length = (length + 3U) / 4U * 4U;
  return (size_t)zix_digest32_aligned(0U, str, length);
}

#if UINTPTR_MAX >= UINT64_MAX

ZIX_PURE_FUNC static size_t
string_hash64_aligned(const char* const str)
{
  size_t length = strlen(str);

  length = (length + 7U) / 8U * 8U;
  return (size_t)zix_digest64_aligned(0U, str, length);
}

#endif

static bool
string_equal(const char* const a, const char* const b)
{
  return !strcmp(a, b);
}

static int
stress_with(ZixAllocator* const allocator,
            const ZixHashFunc   hash_func,
            const size_t        n_elems)
{
  ZixHash*  hash  = zix_hash_new(allocator, identity, hash_func, string_equal);
  TestState state = {hash, NULL, NULL};
  ENSURE(&state, hash, "Failed to allocate hash\n");

  static const size_t string_length = 15;

  char* const  buffer  = (char*)calloc(1, n_elems * (string_length + 1));
  char** const strings = state.strings = (char**)calloc(n_elems, sizeof(char*));
  state.buffer                         = buffer;
  state.strings                        = strings;
  ENSURE(&state, buffer && state.strings, "Failed to allocate strings\n");

  uint32_t seed = 1U;
  for (size_t i = 0U; i < n_elems; ++i) {
    strings[i] = buffer + (i * (string_length + 1));
    assert((uintptr_t)strings[i] % sizeof(size_t) == 0);
    assert((uintptr_t)strings[i] % sizeof(uint32_t) == 0);

    for (size_t j = 0U; j < string_length; ++j) {
      seed          = lcg32(seed);
      strings[i][j] = (char)('!' + (seed % 92));
    }
  }

  // Insert each string
  for (size_t i = 0; i < n_elems; ++i) {
    ZixStatus st = zix_hash_insert(hash, strings[i]);
    ENSUREV(&state, !st, "Failed to insert `%s'\n", strings[i]);
  }

  // Ensure hash size is correct
  ENSUREV(&state,
          zix_hash_size(hash) == n_elems,
          "Hash size %" PRIuPTR " != %" PRIuPTR "\n",
          zix_hash_size(hash),
          n_elems);

  // Attempt to insert each string again
  for (size_t i = 0; i < n_elems; ++i) {
    ZixStatus st = zix_hash_insert(hash, strings[i]);
    ENSUREV(&state,
            st == ZIX_STATUS_EXISTS,
            "Double inserted `%s' (%s)\n",
            strings[i],
            zix_strerror(st));
  }

  // Search for each string
  for (size_t i = 0; i < n_elems; ++i) {
    const char* match = (const char*)zix_hash_find_record(hash, strings[i]);
    ENSUREV(&state, match, "Failed to find `%s'\n", strings[i]);
    ENSUREV(&state,
            match == strings[i],
            "Bad match for `%s': `%s'\n",
            strings[i],
            match);
  }

  static const char* const not_indexed_string = "__not__indexed__";

  // Do a quick smoke test to ensure that false matches don't succeed
  char* const not_indexed = (char*)calloc(1, strlen(not_indexed_string) + 1);
  if (not_indexed) {
    memcpy(not_indexed, not_indexed_string, strlen(not_indexed_string) + 1);
    const char* match = (const char*)zix_hash_find_record(hash, not_indexed);
    free(not_indexed);
    ENSUREV(&state, !match, "Unexpectedly found `%s'\n", not_indexed_string);
  }

  // Remove strings
  for (size_t i = 0; i < n_elems; ++i) {
    const size_t initial_size = zix_hash_size(hash);

    // Remove string
    const char* removed = NULL;
    ZixStatus   st      = zix_hash_remove(hash, strings[i], &removed);
    ENSUREV(&state, !st, "Failed to remove `%s'\n", strings[i]);

    // Ensure the removed value is what we expected
    ENSUREV(&state,
            removed == strings[i],
            "Removed `%s` instead of `%s`\n",
            removed,
            strings[i]);

    // Ensure size is updated
    ENSURE(&state,
           zix_hash_size(hash) == initial_size - 1,
           "Removing node did not decrease hash size\n");

    // Ensure second removal fails
    st = zix_hash_remove(hash, strings[i], &removed);
    ENSUREV(&state,
            st == ZIX_STATUS_NOT_FOUND,
            "Unexpectedly removed `%s' twice\n",
            strings[i]);

    // Ensure value can no longer be found
    assert(zix_hash_find(hash, strings[i]) == zix_hash_end(hash));

    // Check to ensure remaining strings are still present
    for (size_t j = i + 1; j < n_elems; ++j) {
      const char* match = (const char*)zix_hash_find_record(hash, strings[j]);
      ENSUREV(&state, match, "Failed to find `%s' after remove\n", strings[j]);
      ENSUREV(&state,
              match == strings[j],
              "Bad match for `%s' after remove\n",
              strings[j]);
    }
  }

  // Insert each string again (to check non-empty destruction)
  for (size_t i = 0; i < n_elems; ++i) {
    ZixHashInsertPlan plan = zix_hash_plan_insert(hash, strings[i]);

    assert(!zix_hash_record_at(hash, plan));
    ZixStatus st = zix_hash_insert_at(hash, plan, strings[i]);
    ENSUREV(&state, !st, "Failed to insert `%s'\n", strings[i]);
  }

  // Check key == value (and test zix_hash_foreach)
  size_t n_checked = 0U;
  for (ZixHashIter i = zix_hash_begin(hash); i != zix_hash_end(hash);
       i             = zix_hash_next(hash, i)) {
    const char* const string = (const char*)zix_hash_get(hash, i);
    assert(string);
    ENSUREV(&state, strlen(string) >= 3, "Corrupt value `%s'\n", string);

    ++n_checked;
  }

  ENSURE(&state, n_checked == n_elems, "Check failed\n");

  free(strings);
  free(buffer);
  zix_hash_free(hash);

  return 0;
}

static int
stress(ZixAllocator* const allocator, const size_t n_elems)
{
  if (stress_with(allocator, decent_string_hash, n_elems) ||
      stress_with(allocator, terrible_string_hash, n_elems / 4) ||
      stress_with(allocator, string_hash_aligned, n_elems / 4) ||
      stress_with(allocator, string_hash32, n_elems / 4) ||
      stress_with(allocator, string_hash64, n_elems / 4) ||
      stress_with(allocator, string_hash32_aligned, n_elems / 4)) {
    return 1;
  }

#if UINTPTR_MAX >= UINT64_MAX
  if (stress_with(allocator, string_hash64_aligned, n_elems / 4)) {
    return 1;
  }
#endif

  return 0;
}

/// Identity hash function for numeric strings for explicitly hitting cases
ZIX_PURE_FUNC static size_t
identity_index_hash(const char* const str)
{
  return strtoul(str, NULL, 10);
}

static void
test_all_tombstones(void)
{
  /* This tests an edge case where a minimum-sized table can be entirely full
     of tombstones.  If the search loop is not written carefully, then this can
     result in a hang.  This has been seen to occur in real code, though it's
     very hard to hit with a decent hash function.  So, we use the above
     degenerate index hashing function to explicitly place elements exactly
     where we want to hit this case. */

#define N_STRINGS 4

  static const char* original_strings[N_STRINGS] = {
    "0 a",
    "1 a",
    "2 a",
    "3 a",
  };

  static const char* collision_strings[N_STRINGS] = {
    "0 b",
    "1 b",
    "2 b",
    "3 b",
  };

  ZixStatus st = ZIX_STATUS_SUCCESS;
  ZixHash*  hash =
    zix_hash_new(NULL, identity, identity_index_hash, string_equal);

  // Insert each element then immediately remove it
  for (unsigned i = 0U; i < N_STRINGS; ++i) {
    const char* removed = NULL;

    assert(!zix_hash_insert(hash, original_strings[i]));
    assert(!zix_hash_remove(hash, original_strings[i], &removed));
  }

  // Now the table should be "empty" but contain tombstones
  assert(zix_hash_size(hash) == 0);

  // Insert clashing elements which should hit the "all tombstones" case
  for (unsigned i = 0U; i < N_STRINGS; ++i) {
    assert(!zix_hash_insert(hash, collision_strings[i]));
    assert(!st);
  }

  zix_hash_free(hash);

#undef N_STRINGS
}

static void
test_failed_alloc(void)
{
  ZixFailingAllocator allocator = zix_failing_allocator();

  // Successfully stress test the tree to count the number of allocations
  assert(!stress(&allocator.base, 16));

  // Test that each allocation failing is handled gracefully
  const size_t n_new_allocs = zix_failing_allocator_reset(&allocator, 0);
  for (size_t i = 0U; i < n_new_allocs; ++i) {
    zix_failing_allocator_reset(&allocator, i);
    assert(stress(&allocator.base, 16));
  }
}

int
main(void)
{
  zix_hash_free(NULL);

  test_all_tombstones();
  test_failed_alloc();

  static const size_t n_elems = 1024U;

  return stress(NULL, n_elems);
}
