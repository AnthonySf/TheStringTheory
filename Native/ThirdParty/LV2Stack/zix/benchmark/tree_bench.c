// Copyright 2011-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "../test/test_data.h"
#include "bench.h"
#include "warnings.h"

#include <zix/attributes.h>
#include <zix/btree.h>
#include <zix/status.h>
#include <zix/tree.h>
#include <zix/warnings.h>

ZIX_DISABLE_GLIB_WARNINGS
#include <glib.h>
ZIX_RESTORE_WARNINGS

#include <assert.h>
#include <inttypes.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>

#ifndef MIN
#  define MIN(a, b) (((a) < (b)) ? (a) : (b))
#endif

#ifndef MAX
#  define MAX(a, b) (((a) > (b)) ? (a) : (b))
#endif

static int
int_cmp(const void* a, const void* b, const void* ZIX_UNUSED(user_data))
{
  const intptr_t ia = (intptr_t)a;
  const intptr_t ib = (intptr_t)b;

  // note the (ia - ib) trick here would overflow

  if (ia == ib) {
    return 0;
  }

  if (ia < ib) {
    return -1;
  }

  return 1;
}

static int
g_int_cmp(const void* a, const void* b, void* user_data)
{
  return int_cmp(a, b, user_data);
}

static int
test_fail(const char* const prefix, const uintptr_t value)
{
  fprintf(stderr, "error: ");
  fprintf(stderr, "%s %" PRIuPTR "\n", prefix, value);
  return EXIT_FAILURE;
}

static void
start_test(const char* name)
{
  fprintf(stderr, "Benchmarking %s\n", name);
}

static int
bench_zix_tree(size_t n_elems,
               FILE*  insert_dat,
               FILE*  search_dat,
               FILE*  iter_dat,
               FILE*  del_dat)
{
  start_test("ZixTree");

  uintptr_t    r  = 0;
  ZixTreeIter* ti = NULL;
  ZixTree*     t  = zix_tree_new(NULL, false, int_cmp, NULL, NULL, NULL);

  // Insert n_elems elements
  BenchmarkTime insert_start = bench_start();
  for (size_t i = 0; i < n_elems; i++) {
    r = unique_rand(i);

    ZixStatus status = zix_tree_insert(t, (void*)r, &ti);
    if (status) {
      return test_fail("Failed to insert", r);
    }
  }
  fprintf(insert_dat, "\t%lf", bench_end(&insert_start));

  // Search for all elements
  BenchmarkTime search_start = bench_start();
  for (size_t i = 0; i < n_elems; i++) {
    r = unique_rand(i);
    if (zix_tree_find(t, (void*)r, &ti)) {
      return test_fail("Failed to find", r);
    }
    if ((uintptr_t)zix_tree_get(ti) != r) {
      return test_fail("Failed to get", r);
    }
  }
  fprintf(search_dat, "\t%lf", bench_end(&search_start));

  // Iterate over all elements
  BenchmarkTime iter_start = bench_start();
  for (ZixTreeIter* iter = zix_tree_begin(t); !zix_tree_iter_is_end(iter);
       iter              = zix_tree_iter_next(iter)) {
    volatile void* const value = zix_tree_get(iter);
    (void)value;
  }
  fprintf(iter_dat, "\t%lf", bench_end(&iter_start));

  // Delete all elements
  BenchmarkTime del_start = bench_start();
  for (size_t i = 0; i < n_elems; i++) {
    r = unique_rand(i);

    ZixTreeIter* item = NULL;
    if (zix_tree_find(t, (void*)r, &item)) {
      return test_fail("Failed on delete to find", r);
    }
    if (zix_tree_remove(t, item)) {
      return test_fail("Failed to remove", r);
    }
  }
  fprintf(del_dat, "\t%lf", bench_end(&del_start));

  zix_tree_free(t);

  return EXIT_SUCCESS;
}

static int
bench_zix_btree(size_t n_elems,
                FILE*  insert_dat,
                FILE*  search_dat,
                FILE*  iter_dat,
                FILE*  del_dat)
{
  start_test("ZixBTree");

  uintptr_t    r  = 0U;
  ZixBTreeIter ti = zix_btree_end_iter;
  ZixBTree*    t  = zix_btree_new(NULL, int_cmp, NULL);

  // Insert n_elems elements
  BenchmarkTime insert_start = bench_start();
  for (size_t i = 0; i < n_elems; i++) {
    r = unique_rand(i);

    ZixStatus status = zix_btree_insert(t, (void*)r);
    if (status) {
      return test_fail("Failed to insert", r);
    }
  }
  fprintf(insert_dat, "\t%lf", bench_end(&insert_start));

  // Search for all elements
  BenchmarkTime search_start = bench_start();
  for (size_t i = 0; i < n_elems; i++) {
    r = unique_rand(i);
    if (zix_btree_find(t, (void*)r, &ti)) {
      return test_fail("Failed to find", r);
    }
    if ((uintptr_t)zix_btree_get(ti) != r) {
      return test_fail("Failed to get", r);
    }
  }
  fprintf(search_dat, "\t%lf", bench_end(&search_start));

  // Iterate over all elements
  BenchmarkTime iter_start = bench_start();
  ZixBTreeIter  iter       = zix_btree_begin(t);
  for (; !zix_btree_iter_is_end(iter); zix_btree_iter_increment(&iter)) {
    volatile void* const value = zix_btree_get(iter);
    (void)value;
  }
  fprintf(iter_dat, "\t%lf", bench_end(&iter_start));

  // Delete all elements
  BenchmarkTime del_start = bench_start();
  for (size_t i = 0; i < n_elems; i++) {
    r = unique_rand(i);

    void*        removed = NULL;
    ZixBTreeIter next    = zix_btree_end(t);
    if (zix_btree_remove(t, (void*)r, &removed, &next)) {
      return test_fail("Failed to remove", r);
    }
  }
  fprintf(del_dat, "\t%lf", bench_end(&del_start));

  zix_btree_free(t, NULL, NULL);

  return EXIT_SUCCESS;
}

static int
bench_glib(size_t n_elems,
           FILE*  insert_dat,
           FILE*  search_dat,
           FILE*  iter_dat,
           FILE*  del_dat)
{
  start_test("GSequence");

  uintptr_t  r = 0U;
  GSequence* t = g_sequence_new(NULL);

  // Insert n_elems elements
  BenchmarkTime insert_start = bench_start();
  for (size_t i = 0; i < n_elems; ++i) {
    r = unique_rand(i);

    GSequenceIter* iter =
      g_sequence_insert_sorted(t, (void*)r, g_int_cmp, NULL);
    if (!iter || g_sequence_iter_is_end(iter)) {
      return test_fail("Failed to insert", r);
    }
  }
  fprintf(insert_dat, "\t%lf", bench_end(&insert_start));

  // Search for all elements
  BenchmarkTime search_start = bench_start();
  for (size_t i = 0; i < n_elems; ++i) {
    r                   = unique_rand(i);
    GSequenceIter* iter = g_sequence_lookup(t, (void*)r, g_int_cmp, NULL);
    if (!iter || g_sequence_iter_is_end(iter)) {
      return test_fail("Failed to find", r);
    }
  }
  fprintf(search_dat, "\t%lf", bench_end(&search_start));

  // Iterate over all elements
  BenchmarkTime iter_start = bench_start();
  for (GSequenceIter* iter = g_sequence_get_begin_iter(t);
       !g_sequence_iter_is_end(iter);
       iter = g_sequence_iter_next(iter)) {
    g_sequence_get(iter);
  }
  fprintf(iter_dat, "\t%lf", bench_end(&iter_start));

  // Delete all elements
  BenchmarkTime del_start = bench_start();
  for (size_t i = 0; i < n_elems; ++i) {
    r                   = unique_rand(i);
    GSequenceIter* iter = g_sequence_lookup(t, (void*)r, g_int_cmp, NULL);
    if (!iter || g_sequence_iter_is_end(iter)) {
      return test_fail("Failed to remove", r);
    }
    g_sequence_remove(iter);
  }
  fprintf(del_dat, "\t%lf", bench_end(&del_start));

  g_sequence_free(t);

  return EXIT_SUCCESS;
}

static FILE*
open_output(const char* const path)
{
  FILE* const f = fopen(path, "w");
  if (!f) {
    fprintf(stderr, "error: Failed to open %s\n", path);
  }
  return f;
}

static void
close_output(FILE* const f)
{
  if (f) {
    fclose(f);
  }
}

int
main(int argc, char** argv)
{
  if (argc != 3) {
    fprintf(stderr, "USAGE: %s MIN_N MAX_N\n", argv[0]);
    return 1;
  }

  const size_t min_n = MAX(2U, MIN(1U << 29U, strtoul(argv[1], NULL, 10)));
  const size_t max_n = MAX(4U, MIN(1U << 30U, strtoul(argv[2], NULL, 10)));

  fprintf(stderr, "Benchmarking %zu .. %zu elements\n", min_n, max_n);

#define HEADER "# n\tZixTree\tZixBTree\tGSequence\n"

  FILE* const insert_dat = open_output("tree_insert.txt");
  FILE* const search_dat = open_output("tree_search.txt");
  FILE* const iter_dat   = open_output("tree_iterate.txt");
  FILE* const del_dat    = open_output("tree_delete.txt");
  if (!insert_dat || !search_dat || !iter_dat || !del_dat) {
    close_output(del_dat);
    close_output(iter_dat);
    close_output(search_dat);
    close_output(insert_dat);
    return EXIT_FAILURE;
  }

  assert(insert_dat);
  assert(search_dat);
  assert(iter_dat);
  assert(del_dat);

  fprintf(insert_dat, HEADER);
  fprintf(search_dat, HEADER);
  fprintf(iter_dat, HEADER);
  fprintf(del_dat, HEADER);
  for (size_t n = min_n; n <= max_n; n *= 2) {
    fprintf(stderr, "n = %zu\n", n);
    fprintf(insert_dat, "%zu", n);
    fprintf(search_dat, "%zu", n);
    fprintf(iter_dat, "%zu", n);
    fprintf(del_dat, "%zu", n);
    bench_zix_tree(n, insert_dat, search_dat, iter_dat, del_dat);
    bench_zix_btree(n, insert_dat, search_dat, iter_dat, del_dat);
    bench_glib(n, insert_dat, search_dat, iter_dat, del_dat);
    fprintf(insert_dat, "\n");
    fprintf(search_dat, "\n");
    fprintf(iter_dat, "\n");
    fprintf(del_dat, "\n");
  }
  close_output(del_dat);
  close_output(iter_dat);
  close_output(search_dat);
  close_output(insert_dat);

  fprintf(
    stderr,
    "Wrote tree_insert.txt tree_search.txt tree_iterate.txt tree_del.txt\n");

  return EXIT_SUCCESS;
}
