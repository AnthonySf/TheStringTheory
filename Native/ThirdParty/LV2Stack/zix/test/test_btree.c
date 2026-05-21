// Copyright 2011-2023 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "ensure.h"
#include "failing_allocator.h"
#include "test_args.h"
#include "test_data.h"

#include <zix/allocator.h>
#include <zix/attributes.h>
#include <zix/btree.h>
#include <zix/status.h>

#include <assert.h>
#include <inttypes.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>

ZIX_PURE_FUNC static int
int_cmp(const void* a, const void* b, const void* ZIX_UNUSED(user_data))
{
  const uintptr_t ia = (uintptr_t)a;
  const uintptr_t ib = (uintptr_t)b;

  assert(ia != 0U); // No wildcards
  assert(ib != 0U); // No wildcards

  return ia < ib ? -1 : ia > ib ? 1 : 0;
}

static uintptr_t
ith_elem(const unsigned test_num, const size_t n_elems, const size_t i)
{
  switch (test_num % 3) {
  case 0:
    return i + 1; // Increasing
  case 1:
    return n_elems - i; // Decreasing
  default:
    return 1U + unique_rand(i); // Pseudo-random
  }
}

typedef struct {
  unsigned test_num;
  size_t   n_elems;
} TestContext;

static uintptr_t
wildcard_cut(unsigned test_num, size_t n_elems)
{
  return ith_elem(test_num, n_elems, n_elems / 3);
}

/// Wildcard comparator where 0 matches anything >= wildcard_cut(n_elems)
static int
wildcard_cmp(const void* a, const void* b, const void* user_data)
{
  const TestContext* ctx      = (const TestContext*)user_data;
  const size_t       n_elems  = ctx->n_elems;
  const unsigned     test_num = ctx->test_num;
  const uintptr_t    ia       = (uintptr_t)a;
  const uintptr_t    ib       = (uintptr_t)b;

  return (ia == 0)
           ? ((ib >= wildcard_cut(test_num, n_elems)) ? 0  // Wildcard match
                                                      : 1) // Wildcard a > b
         : (ib == 0)
           ? ((ia >= wildcard_cut(test_num, n_elems)) ? 0   // Wildcard match
                                                      : -1) // Wildcard b > a
           : int_cmp(a, b, user_data);
}

ZIX_LOG_FUNC(2, 3)
static int
test_fail(ZixBTree* t, const char* fmt, ...)
{
  zix_btree_free(t, NULL, NULL);

  va_list args; // NOLINT(cppcoreguidelines-init-variables)
  va_start(args, fmt);
  fprintf(stderr, "error: ");
  vfprintf(stderr, fmt, args);
  va_end(args);
  return EXIT_FAILURE;
}

static const size_t n_clear_insertions = 1024U;
static size_t       n_destroy_calls    = 0U;

static void
destroy(void* const ptr, const void* const user_data)
{
  (void)user_data;
  ++n_destroy_calls;
  assert(ptr);
  assert((uintptr_t)ptr <= n_clear_insertions);
}

static void
test_empty(void)
{
  ZixBTree* const t = zix_btree_new(NULL, int_cmp, NULL);
  assert(t);

  // Check that reading functions work properly with an empty (rootless) tree
  const int    e  = 42;
  ZixBTreeIter ti = zix_btree_end(t);
  zix_btree_clear(t, NULL, NULL);
  assert(!zix_btree_size(t));
  assert(zix_btree_find(t, &e, &ti) == ZIX_STATUS_NOT_FOUND);
  assert(!zix_btree_lower_bound(t, int_cmp, NULL, &e, &ti));

  zix_btree_free(t, destroy, NULL);
}

static void
test_clear(void)
{
  ZixBTree* t = zix_btree_new(NULL, int_cmp, NULL);

  for (uintptr_t r = 0U; r < n_clear_insertions; ++r) {
    assert(!zix_btree_insert(t, (void*)(r + 1U)));
  }

  // Clear and check that destroy is called once for each element
  assert(n_destroy_calls == 0U);
  zix_btree_clear(t, destroy, &n_destroy_calls);
  assert(zix_btree_size(t) == 0U);
  assert(n_destroy_calls == n_clear_insertions);

  // Clear the now-empty tree and check that destroy isn't called again
  zix_btree_clear(t, destroy, NULL);
  assert(n_destroy_calls == n_clear_insertions);

  zix_btree_free(t, destroy, NULL);
}

static void
test_free(void)
{
  ZixBTree* t = zix_btree_new(NULL, int_cmp, NULL);

  for (uintptr_t r = 0U; r < n_clear_insertions; ++r) {
    assert(!zix_btree_insert(t, (void*)(r + 1U)));
  }

  assert(zix_btree_size(t) == n_clear_insertions);

  zix_btree_free(t, destroy, NULL);
}

static void
test_iter_comparison(void)
{
  static const size_t n_elems = 4096U;

  ZixBTree* const t = zix_btree_new(NULL, int_cmp, NULL);

  // Store increasing numbers from 1 (jammed into the pointers themselves)
  for (uintptr_t r = 1U; r < n_elems; ++r) {
    assert(!zix_btree_insert(t, (void*)r));
  }

  // Check that begin and end work sensibly
  const ZixBTreeIter begin = zix_btree_begin(t);
  const ZixBTreeIter end   = zix_btree_end(t);
  assert(!zix_btree_iter_is_end(begin));
  assert(zix_btree_iter_is_end(end));
  assert(!zix_btree_iter_equals(begin, end));
  assert(!zix_btree_iter_equals(end, begin));

  // Make another begin iterator
  ZixBTreeIter j = zix_btree_begin(t);
  assert(zix_btree_iter_equals(begin, j));

  // Advance it and check that they are no longer equal
  for (size_t r = 1U; r < n_elems - 1U; ++r) {
    j = zix_btree_iter_next(j);
    assert(!zix_btree_iter_is_end(j));
    assert(!zix_btree_iter_equals(begin, j));
    assert(!zix_btree_iter_equals(end, j));
    assert(!zix_btree_iter_equals(j, end));
  }

  // Advance it to the end
  zix_btree_iter_increment(&j);
  assert(zix_btree_iter_is_end(j));
  assert(!zix_btree_iter_equals(begin, j));
  assert(zix_btree_iter_equals(end, j));
  assert(zix_btree_iter_equals(j, end));

  zix_btree_free(t, NULL, NULL);
}

static void
test_insert_split_value(void)
{
  static const size_t    n_insertions = 767U; // Number of insertions to split
  static const uintptr_t split_value  = 512U; // Value that will be pulled up

  ZixBTree* const t = zix_btree_new(NULL, int_cmp, NULL);

  // Insert right up until it would cause a split
  for (uintptr_t r = 1U; r < n_insertions; ++r) {
    assert(!zix_btree_insert(t, (void*)r));
  }

  // Insert the element that will be chosen as the split pivot
  assert(zix_btree_insert(t, (void*)split_value) == ZIX_STATUS_EXISTS);

  zix_btree_free(t, NULL, NULL);
}

static void
test_remove_cases(void)
{
  /* Insert and remove in several "phases" with different strides that are not
     even multiples.  This spreads the load around to hit as many cases as
     possible. */

  static const uintptr_t s1           = 3U;
  static const uintptr_t s2           = 511U;
  const size_t           n_insertions = s1 * s2 * 450U;

  ZixBTree* const t = zix_btree_new(NULL, int_cmp, NULL);

  {
    // Try to remove from an emptry tree
    ZixBTreeIter    next  = zix_btree_end(t);
    const uintptr_t value = 42U;
    void*           out   = NULL;
    assert(zix_btree_remove(t, (void*)value, &out, &next) ==
           ZIX_STATUS_NOT_FOUND);
  }

  // Insert in s1-sized chunks
  for (uintptr_t phase = 0U; phase < s1; ++phase) {
    for (uintptr_t r = 0U; r < n_insertions / s1; ++r) {
      const uintptr_t value = (s1 * r) + phase + 1U;

      assert(!zix_btree_insert(t, (void*)value));
    }
  }

  // Remove in s2-sized chunks
  ZixBTreeIter next = zix_btree_end(t);
  for (uintptr_t phase = 0U; phase < s2; ++phase) {
    for (uintptr_t r = 0U; r < n_insertions / s2; ++r) {
      const uintptr_t value = (s2 * r) + phase + 1U;
      void*           out   = NULL;

      assert(!zix_btree_remove(t, (void*)value, &out, &next));
      assert((uintptr_t)out == value);
    }
  }

  assert(!zix_btree_size(t));
  zix_btree_free(t, NULL, NULL);
}

static int
stress(ZixAllocator* const allocator,
       const unsigned      test_num,
       const size_t        n_elems)
{
  assert(n_elems > 0U);

  uintptr_t r  = 0;
  ZixBTree* t  = zix_btree_new(allocator, int_cmp, NULL);
  ZixStatus st = ZIX_STATUS_SUCCESS;

  ENSURE(t, t, "Failed to allocate tree\n");

  // Ensure begin iterator is end on empty tree
  ZixBTreeIter ti  = zix_btree_begin(t);
  ZixBTreeIter end = zix_btree_end(t);

  ENSURE(
    t, zix_btree_iter_is_end(ti), "Begin iterator on empty tree is not end\n");

  ENSURE(t,
         zix_btree_iter_equals(ti, end),
         "Begin and end of empty tree are not equal\n");

  // Insert n_elems elements
  for (size_t i = 0; i < n_elems; ++i) {
    r  = ith_elem(test_num, n_elems, i);
    st = zix_btree_insert(t, (void*)r);
    ENSUREV(t, !st, "Insert %" PRIuPTR " failed (%s)\n", r, zix_strerror(st));
  }

  // Ensure tree size is correct
  ENSUREV(t,
          zix_btree_size(t) == n_elems,
          "Tree size %" PRIuPTR " != %" PRIuPTR "\n",
          zix_btree_size(t),
          n_elems);

  // Ensure begin no longer equals end
  ti  = zix_btree_begin(t);
  end = zix_btree_end(t);
  ENSURE(t,
         !zix_btree_iter_equals(ti, end),
         "Begin and end of non-empty tree are equal\n");

  // Search for all elements
  for (size_t i = 0; i < n_elems; ++i) {
    r = ith_elem(test_num, n_elems, i);
    ENSUREV(t,
            !zix_btree_find(t, (void*)r, &ti),
            "Find %" PRIuPTR " @ %" PRIuPTR " failed\n",
            r,
            i);

    ENSUREV(t,
            (uintptr_t)zix_btree_get(ti) == r,
            "Search data corrupt (%" PRIuPTR " != %" PRIuPTR ")\n",
            (uintptr_t)zix_btree_get(ti),
            r);
  }

  // Find the lower bound of all elements and ensure it's exact
  for (size_t i = 0; i < n_elems; ++i) {
    r = ith_elem(test_num, n_elems, i);
    ENSUREV(t,
            !zix_btree_lower_bound(t, int_cmp, NULL, (void*)r, &ti),
            "Lower bound %" PRIuPTR " @ %" PRIuPTR " failed\n",
            r,
            i);

    ENSUREV(t,
            !zix_btree_iter_is_end(ti),
            "Lower bound %" PRIuPTR " @ %" PRIuPTR " hit end\n",
            r,
            i);

    ENSUREV(t,
            (uintptr_t)zix_btree_get(ti) == r,
            "Lower bound corrupt (%" PRIuPTR " != %" PRIuPTR "\n",
            (uintptr_t)zix_btree_get(ti),
            r);
  }

  // Search for elements that don't exist
  for (size_t i = 0; i < n_elems; ++i) {
    r = ith_elem(test_num, n_elems * 3, n_elems + i);
    ENSUREV(t,
            zix_btree_find(t, (void*)r, &ti),
            "Unexpectedly found %" PRIuPTR "\n",
            r);
  }

  // Iterate over all elements
  size_t    i    = 0;
  uintptr_t last = 0;
  for (ti = zix_btree_begin(t); !zix_btree_iter_is_end(ti);
       zix_btree_iter_increment(&ti), ++i) {
    const uintptr_t iter_data = (uintptr_t)zix_btree_get(ti);
    ENSUREV(t,
            iter_data >= last,
            "Iter @ %" PRIuPTR " corrupt (%" PRIuPTR " < %" PRIuPTR ")\n",
            i,
            iter_data,
            last);

    last = iter_data;
  }

  ENSUREV(t,
          i == n_elems,
          "Iteration stopped at %" PRIuPTR "/%" PRIuPTR " elements\n",
          i,
          n_elems);

  // Insert n_elems elements again, ensuring duplicates fail
  for (i = 0; i < n_elems; ++i) {
    r = ith_elem(test_num, n_elems, i);
    ENSURE(t, zix_btree_insert(t, (void*)r), "Duplicate insert succeeded\n");
  }

  // Search for the middle element then iterate from there
  r = ith_elem(test_num, n_elems, n_elems / 2);
  ENSUREV(
    t, !zix_btree_find(t, (void*)r, &ti), "Find %" PRIuPTR " failed\n", r);
  last = (uintptr_t)zix_btree_get(ti);
  zix_btree_iter_increment(&ti);
  for (i = 1; !zix_btree_iter_is_end(ti); zix_btree_iter_increment(&ti), ++i) {
    ENSUREV(t,
            (uintptr_t)zix_btree_get(ti) != last,
            "Duplicate element @ %" PRIuPTR " %" PRIuPTR "\n",
            i,
            last);
    last = (uintptr_t)zix_btree_get(ti);
  }

  // Remove all elements
  ZixBTreeIter next = zix_btree_end_iter;
  for (size_t e = 0; e < n_elems; e++) {
    r = ith_elem(test_num, n_elems, e);

    uintptr_t removed = 0;
    ENSUREV(t,
            !zix_btree_remove(t, (void*)r, (void**)&removed, &next),
            "Error removing item %" PRIuPTR "\n",
            r);

    ENSUREV(t,
            removed == r,
            "Removed wrong item %" PRIuPTR " != %" PRIuPTR "\n",
            removed,
            r);

    if (test_num == 0) {
      const uintptr_t next_value = ith_elem(test_num, n_elems, e + 1);
      ENSUREV(t,
              (zix_btree_iter_is_end(next) && e == n_elems - 1) ||
                ((uintptr_t)zix_btree_get(next) == next_value),
              "Remove-all next value %" PRIuPTR " != %" PRIuPTR "\n",
              (uintptr_t)zix_btree_get(next),
              next_value);
    }
  }

  // Ensure the tree is empty
  ENSUREV(t,
          zix_btree_size(t) == 0U,
          "Tree size %" PRIuPTR " != 0\n",
          zix_btree_size(t));

  // Insert n_elems elements again (to test non-empty destruction)
  for (size_t e = 0; e < n_elems; ++e) {
    r = ith_elem(test_num, n_elems, e);
    ENSURE(t, !zix_btree_insert(t, (void*)r), "Post-deletion insert failed\n");
  }

  // Try to remove elements that don't exist
  for (size_t e = 0; e < n_elems; e++) {
    r                 = ith_elem(test_num, n_elems * 3, n_elems + e);
    uintptr_t removed = 0;
    ENSUREV(t,
            zix_btree_remove(t, (void*)r, (void**)&removed, &next),
            "Removal of non-existent %" PRIuPTR " succeeded\n",
            r);
  }

  // Ensure tree size is still correct
  ENSUREV(t,
          zix_btree_size(t) == n_elems,
          "Tree size %" PRIuPTR " != %" PRIuPTR "\n",
          zix_btree_size(t),
          n_elems);

  // Delete some elements towards the end
  for (size_t e = 0; e < n_elems / 4; e++) {
    r = ith_elem(test_num, n_elems, n_elems - (n_elems / 4) + e);
    uintptr_t removed = 0;
    ENSUREV(t,
            !zix_btree_remove(t, (void*)r, (void**)&removed, &next),
            "Deletion of %" PRIuPTR " failed\n",
            r);

    ENSUREV(t,
            removed == r,
            "Removed wrong item %" PRIuPTR " != %" PRIuPTR "\n",
            removed,
            r);

    if (test_num == 0) {
      const uintptr_t next_value = ith_elem(test_num, n_elems, e + 1);
      ENSUREV(t,
              zix_btree_iter_is_end(next) ||
                (uintptr_t)zix_btree_get(next) != next_value,
              "Next iterator %" PRIuPTR " == %" PRIuPTR "\n",
              (uintptr_t)zix_btree_get(next),
              next_value);
    }
  }

  // Check tree size
  ENSUREV(t,
          zix_btree_size(t) == n_elems - (n_elems / 4),
          "Tree size %" PRIuPTR " != %" PRIuPTR "\n",
          zix_btree_size(t),
          n_elems);

  // Delete some elements in a random order
  for (size_t e = 0; e < zix_btree_size(t) / 2; e++) {
    r = ith_elem(test_num, n_elems, unique_rand(e) % n_elems);

    uintptr_t removed = 0;
    ZixStatus rst     = zix_btree_remove(t, (void*)r, (void**)&removed, &next);
    ENSUREV(t,
            rst == ZIX_STATUS_SUCCESS || rst == ZIX_STATUS_NOT_FOUND,
            "Error deleting %" PRIuPTR "\n",
            r);
  }

  // Delete all remaining elements via next iterator
  next                 = zix_btree_begin(t);
  uintptr_t last_value = 0;
  while (!zix_btree_iter_is_end(next)) {
    const uintptr_t value   = (uintptr_t)zix_btree_get(next);
    uintptr_t       removed = 0;
    ENSUREV(t,
            !zix_btree_remove(t, zix_btree_get(next), (void**)&removed, &next),
            "Error removing next item %" PRIuPTR "\n",
            r);

    ENSUREV(t,
            removed == value,
            "Removed wrong next item %" PRIuPTR " != %" PRIuPTR "\n",
            removed,
            (uintptr_t)value);

    ENSUREV(t,
            removed >= last_value,
            "Removed unordered next item %" PRIuPTR " < %" PRIuPTR "\n",
            removed,
            (uintptr_t)value);

    last_value = removed;
  }

  assert(zix_btree_size(t) == 0);
  zix_btree_free(t, NULL, NULL);

  // Test lower_bound with wildcard comparator

  TestContext ctx = {test_num, n_elems};
  t               = zix_btree_new(NULL, wildcard_cmp, &ctx);
  ENSURE(t, t, "Failed to allocate tree\n");

  // Insert n_elems elements
  for (i = 0; i < n_elems; ++i) {
    r  = ith_elem(test_num, n_elems, i);
    st = zix_btree_insert(t, (void*)r);
    ENSUREV(t, !st, "Insert %" PRIuPTR " failed (%s)\n", r, zix_strerror(st));
  }

  // Find lower bound of wildcard
  const uintptr_t wildcard = 0;
  ENSURE(t,
         !zix_btree_lower_bound(t, wildcard_cmp, &ctx, (void*)wildcard, &ti),
         "Wildcard lower bound failed\n");

  ENSURE(t, !zix_btree_iter_is_end(ti), "Lower bound reached end\n");

  // Check value
  const uintptr_t iter_data = (uintptr_t)zix_btree_get(ti);
  const uintptr_t cut       = wildcard_cut(test_num, n_elems);
  ENSUREV(t,
          iter_data == cut,
          "Lower bound %" PRIuPTR " != %" PRIuPTR "\n",
          iter_data,
          cut);

  ENSUREV(t,
          !wildcard_cmp((void*)wildcard, (void*)iter_data, &ctx),
          "Wildcard lower bound %" PRIuPTR " != %" PRIuPTR "\n",
          iter_data,
          cut);

  // Find lower bound of value past end
  const uintptr_t max = (uintptr_t)-1;
  ENSURE(t,
         !zix_btree_lower_bound(t, wildcard_cmp, &ctx, (void*)max, &ti),
         "End lower bound failed\n");

  ENSURE(
    t, zix_btree_iter_is_end(ti), "Lower bound of maximum value is not end\n");

  zix_btree_free(t, NULL, NULL);

  return EXIT_SUCCESS;
}

static void
test_failed_alloc(void)
{
  ZixFailingAllocator allocator = zix_failing_allocator();

  // Successfully stress test the tree to count the number of allocations
  assert(!stress(&allocator.base, 0, 4096));

  // Test that each allocation failing is handled gracefully
  const size_t n_new_allocs = zix_failing_allocator_reset(&allocator, 0);
  for (size_t i = 0U; i < n_new_allocs; ++i) {
    zix_failing_allocator_reset(&allocator, i);
    assert(stress(&allocator.base, 0, 4096));
  }
}

int
main(int argc, char** argv)
{
  if (argc > 2) {
    fprintf(stderr, "Usage: %s [N_ELEMS]\n", argv[0]);
    return EXIT_FAILURE;
  }

  test_empty();
  test_clear();
  test_free();
  test_iter_comparison();
  test_insert_split_value();
  test_remove_cases();
  test_failed_alloc();

  const unsigned n_tests  = 3U;
  const char*    size_arg = (argc > 1) ? argv[1] : "65536";
  const size_t   n_elems  = zix_test_size_arg(size_arg, 4U, 1U << 20U);

  printf("Running %u tests with %zu elements", n_tests, n_elems);

  int st = 0;
  for (unsigned i = 0; !st && i < n_tests; ++i) {
    printf(".");
    fflush(stdout);
    st = stress(NULL, i, n_elems);
  }

  printf("\n");
  return st;
}
