// Copyright 2011-2023 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "ensure.h"
#include "failing_allocator.h"
#include "test_args.h"
#include "test_data.h"

#include <zix/allocator.h>
#include <zix/attributes.h>
#include <zix/status.h>
#include <zix/tree.h>

#include <assert.h>
#include <inttypes.h>
#include <stdarg.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <time.h>

static size_t seed = 1;

static int
int_cmp(const void* a, const void* b, const void* ZIX_UNUSED(user_data))
{
  const uintptr_t ia = (uintptr_t)a;
  const uintptr_t ib = (uintptr_t)b;

  return ia < ib ? -1 : ia > ib ? 1 : 0;
}

static uintptr_t
ith_elem(unsigned test_num, size_t n_elems, size_t i)
{
  switch (test_num % 3) {
  case 0:
    return i; // Increasing (worst case)
  case 1:
    return n_elems - i; // Decreasing (worse case)
  case 2:
  default:
    return lcg(seed + i) % 100U; // Random
  }
}

ZIX_LOG_FUNC(2, 3)
static int
test_fail(ZixTree* t, const char* fmt, ...)
{
  zix_tree_free(t);

  va_list args; // NOLINT(cppcoreguidelines-init-variables)
  va_start(args, fmt);
  fprintf(stderr, "error: ");
  vfprintf(stderr, fmt, args);
  va_end(args);

  return EXIT_FAILURE;
}

static void
test_duplicate_insert(void)
{
  const uintptr_t r  = 0xDEADBEEF;
  ZixTreeIter*    ti = NULL;
  ZixTree*        t  = zix_tree_new(NULL, false, int_cmp, NULL, NULL, NULL);

  assert(!zix_tree_begin(t));
  assert(!zix_tree_end(t));
  assert(!zix_tree_rbegin(t));
  assert(!zix_tree_rend(t));

  assert(!zix_tree_insert(t, (void*)r, &ti));
  assert((uintptr_t)zix_tree_get(ti) == r);
  assert(zix_tree_insert(t, (void*)r, &ti) == ZIX_STATUS_EXISTS);

  zix_tree_free(t);
}

static int
check_tree_size(const size_t actual, const size_t expected)
{
  return (expected == actual)
           ? 0
           : test_fail(NULL, // FIXME
                       "Tree size %" PRIuPTR " != %" PRIuPTR "\n",
                       actual,
                       expected);
}

static int
insert_elements(ZixTree* const t, const unsigned test_num, const size_t n_elems)
{
  ZixTreeIter* ti = NULL;

  for (size_t i = 0; i < n_elems; ++i) {
    const uintptr_t r = ith_elem(test_num, n_elems, i);

    const ZixStatus st = zix_tree_insert(t, (void*)r, &ti);
    ENSURE(NULL, !st, "Insert failed\n");

    const uintptr_t value = (uintptr_t)zix_tree_get(ti);
    ENSUREV(
      NULL, value == r, "Insert %" PRIuPTR " != %" PRIuPTR "\n", value, r);
  }

  return 0;
}

static int
stress(ZixAllocator* allocator, unsigned test_num, size_t n_elems)
{
  uintptr_t    r  = 0U;
  ZixTreeIter* ti = NULL;
  ZixTree*     t  = zix_tree_new(allocator, true, int_cmp, NULL, NULL, NULL);

  ENSURE(t, t, "Failed to allocate tree\n");
  ENSURE(t, !zix_tree_begin(t), "Empty tree has begin iterator\n");
  ENSURE(t, !zix_tree_end(t), "Empty tree has end iterator\n");
  ENSURE(t, !zix_tree_rbegin(t), "Empty tree has reverse begin iterator\n");
  ENSURE(t, !zix_tree_rend(t), "Empty tree has reverse end iterator\n");

  // Insert n_elems elements
  ENSURE(t,
         !insert_elements(t, test_num, n_elems),
         "Failed to insert initial elements\n");

  // Ensure tree size is correct
  ENSUREV(t,
          zix_tree_size(t) == n_elems,
          "Tree size %" PRIuPTR " != %" PRIuPTR "\n",
          zix_tree_size(t),
          n_elems);

  // Search for all elements
  for (size_t i = 0; i < n_elems; ++i) {
    r = ith_elem(test_num, n_elems, i);
    ENSURE(t, !zix_tree_find(t, (void*)r, &ti), "Find failed\n");

    const uintptr_t value = (uintptr_t)zix_tree_get(ti);
    ENSUREV(t, value == r, "Value %" PRIuPTR " != %" PRIuPTR "\n", value, r);
  }

  // Iterate over all elements
  size_t    i    = 0;
  uintptr_t last = 0;
  for (ZixTreeIter* iter = zix_tree_begin(t); !zix_tree_iter_is_end(iter);
       iter              = zix_tree_iter_next(iter), ++i) {
    const uintptr_t iter_data = (uintptr_t)zix_tree_get(iter);
    ENSUREV(t,
            iter_data >= last,
            "Iter corrupt (%" PRIuPTR " < %" PRIuPTR ")\n",
            iter_data,
            last);
    last = iter_data;
  }
  ENSUREV(t,
          i == n_elems,
          "Iteration stopped at %" PRIuPTR "/%" PRIuPTR " elements\n",
          i,
          n_elems);

  // Iterate over all elements backwards
  i    = 0;
  last = INTPTR_MAX;
  for (ZixTreeIter* iter = zix_tree_rbegin(t); !zix_tree_iter_is_rend(iter);
       iter              = zix_tree_iter_prev(iter), ++i) {
    const uintptr_t iter_data = (uintptr_t)zix_tree_get(iter);
    ENSUREV(t,
            iter_data <= last,
            "Iter corrupt (%" PRIuPTR " < %" PRIuPTR ")\n",
            iter_data,
            last);
    last = iter_data;
  }

  // Delete all elements
  for (size_t e = 0; e < n_elems; e++) {
    r = ith_elem(test_num, n_elems, e);

    ZixTreeIter* item = NULL;
    ENSURE(t,
           zix_tree_find(t, (void*)r, &item) == ZIX_STATUS_SUCCESS,
           "Failed to find item to remove\n");
    ENSURE(t, !zix_tree_remove(t, item), "Error removing item\n");
  }

  // Ensure the tree is empty
  ENSURE(t, zix_tree_size(t) == 0U, "Tree isn't empty\n");

  // Insert n_elems elements again (to test non-empty destruction)
  ENSURE(t, !insert_elements(t, test_num, n_elems), "Reinsertion failed\n");

  // Ensure tree size is correct
  const int ret = check_tree_size(zix_tree_size(t), n_elems);

  zix_tree_free(t);

  return ret ? EXIT_FAILURE : EXIT_SUCCESS;
}

static void
test_failed_alloc(void)
{
  ZixFailingAllocator allocator = zix_failing_allocator();

  // Successfully stress test the tree to count the number of allocations
  assert(!stress(&allocator.base, 0, 16));

  // Test that each allocation failing is handled gracefully
  const size_t n_new_allocs = zix_failing_allocator_reset(&allocator, 0);
  for (size_t i = 0U; i < n_new_allocs; ++i) {
    zix_failing_allocator_reset(&allocator, i);
    assert(stress(&allocator.base, 0, 16));
  }
}

int
main(int argc, char** argv)
{
  const unsigned n_tests = 3;
  size_t         n_elems = 0;

  assert(!zix_tree_iter_next(NULL));
  assert(!zix_tree_iter_prev(NULL));

  test_duplicate_insert();
  test_failed_alloc();

  if (argc == 1) {
    n_elems = 100000U;
  } else {
    n_elems = zix_test_size_arg(argv[1], 4U, 1U << 20U);
    if (argc > 2) {
      seed = strtoul(argv[2], NULL, 10);
    } else {
      seed = (size_t)time(NULL);
    }
  }

  printf(
    "Running %u tests with %zu elements (seed %zu)", n_tests, n_elems, seed);

  int st = 0;
  for (unsigned i = 0; !st && i < n_tests; ++i) {
    printf(".");
    fflush(stdout);
    st = stress(NULL, i, n_elems);
  }

  printf("\n");
  return EXIT_SUCCESS;
}
