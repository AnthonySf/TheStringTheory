// Copyright 2011-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "bench.h"
#include "warnings.h"

typedef struct {
  char*  buf;
  size_t len;
} ZixChunk;

#define ZIX_HASH_KEY_TYPE ZixChunk
#define ZIX_HASH_RECORD_TYPE ZixChunk

#include <zix/attributes.h>
#include <zix/digest.h>
#include <zix/hash.h>
#include <zix/status.h>
#include <zix/warnings.h>

ZIX_DISABLE_GLIB_WARNINGS
#include <glib.h>
ZIX_RESTORE_WARNINGS

#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef struct {
  ZixChunk* chunks;
  size_t    n_chunks;
  char*     buf;
} Inputs;

/// Linear Congruential Generator for making random 64-bit integers
static inline uint64_t
lcg64(const uint64_t i)
{
  static const uint64_t a = 6364136223846793005ULL;
  static const uint64_t c = 1ULL;

  return (a * i) + c;
}

ZIX_PURE_FUNC static const ZixChunk*
identity(const ZixChunk* record)
{
  return record;
}

static size_t
zix_chunk_hash(const ZixChunk* const chunk)
{
  return zix_digest(0U, chunk->buf, chunk->len);
}

static bool
zix_chunk_equal(const ZixChunk* a, const ZixChunk* b)
{
  return a->len == b->len && !memcmp(a->buf, b->buf, a->len);
}

static const unsigned seed = 1;

static void
free_inputs(const Inputs* const inputs)
{
  for (size_t i = 0; i < inputs->n_chunks; ++i) {
    free(inputs->chunks[i].buf);
  }

  free(inputs->chunks);
  free(inputs->buf);
}

static Inputs
read_inputs(FILE* const fd)
{
  static const size_t max_n_strings = 1U << 20U;
  static const Inputs no_inputs     = {NULL, 0U, NULL};

  Inputs inputs       = {NULL, 0U, NULL};
  size_t buf_len      = 1;
  size_t this_str_len = 0;
  for (int c = 0; (c = fgetc(fd)) != EOF;) {
    if (c == '\n') {
      if (this_str_len == 0) {
        continue;
      }

      ZixChunk* const new_chunks = (ZixChunk*)realloc(
        inputs.chunks, (inputs.n_chunks + 1) * sizeof(ZixChunk));

      if (!new_chunks) {
        free_inputs(&inputs);
        return no_inputs;
      }

      inputs.chunks                      = new_chunks;
      inputs.chunks[inputs.n_chunks].buf = (char*)malloc(buf_len);
      inputs.chunks[inputs.n_chunks].len = this_str_len;
      assert(inputs.chunks[inputs.n_chunks].buf);
      memcpy(inputs.chunks[inputs.n_chunks].buf, inputs.buf, buf_len);
      this_str_len = 0;
      if (++inputs.n_chunks == max_n_strings) {
        break;
      }
    } else {
      ++this_str_len;
      if (buf_len < this_str_len + 1) {
        buf_len = this_str_len + 1;

        char* const new_buf = (char*)realloc(inputs.buf, buf_len);
        if (!new_buf) {
          free_inputs(&inputs);
          return no_inputs;
        }

        inputs.buf = new_buf;
      }
      inputs.buf[this_str_len - 1] = (char)c;
      inputs.buf[this_str_len]     = '\0';
    }
  }

  return inputs;
}

static int
run(FILE* const fd)
{
  Inputs inputs = read_inputs(fd);

  fclose(fd);

  FILE* insert_dat = fopen("dict_insert.txt", "w");
  if (!insert_dat) {
    fprintf(stderr, "error: Failed to open dict_insert.txt\n");
    free_inputs(&inputs);
    return 1;
  }

  FILE* search_dat = fopen("dict_search.txt", "w");
  if (!search_dat) {
    fclose(insert_dat);
    free_inputs(&inputs);
    fprintf(stderr, "error: Failed to open dict_search.txt\n");
    return 1;
  }

  assert(insert_dat);
  assert(search_dat);
  fprintf(insert_dat, "# n\tGHashTable\tZixHash\n");
  fprintf(search_dat, "# n\tGHashTable\tZixHash\n");

  for (size_t n = inputs.n_chunks / 16; n <= inputs.n_chunks; n *= 2) {
    printf("Benchmarking n = %zu\n", n);
    GHashTable* hash = g_hash_table_new(g_str_hash, g_str_equal);

    ZixHash* zhash = zix_hash_new(NULL,
                                  identity,
                                  (ZixHashFunc)zix_chunk_hash,
                                  (ZixKeyEqualFunc)zix_chunk_equal);

    fprintf(insert_dat, "%zu", n);
    fprintf(search_dat, "%zu", n);

    // Benchmark insertion

    // GHashTable
    BenchmarkTime insert_start = bench_start();
    for (size_t i = 0; i < n; ++i) {
      g_hash_table_insert(hash, inputs.chunks[i].buf, inputs.chunks[i].buf);
    }
    fprintf(insert_dat, "\t%lf", bench_end(&insert_start));

    // ZixHash
    insert_start = bench_start();
    for (size_t i = 0; i < n; ++i) {
      ZixStatus st = zix_hash_insert(zhash, &inputs.chunks[i]);
      assert(!st || st == ZIX_STATUS_EXISTS);
      (void)st;
    }
    fprintf(insert_dat, "\t%lf\n", bench_end(&insert_start));

    // Benchmark search

    // GHashTable
    BenchmarkTime search_start = bench_start();
    for (size_t i = 0; i < n; ++i) {
      const size_t index = (size_t)(lcg64(seed + i) % n);
      char* volatile match =
        (char*)g_hash_table_lookup(hash, inputs.chunks[index].buf);

#ifndef NDEBUG
      const char* const m = match;
      assert(!strcmp(m, inputs.chunks[index].buf));
#endif

      (void)match;
    }
    fprintf(search_dat, "\t%lf", bench_end(&search_start));

    // ZixHash
    search_start = bench_start();
    for (size_t i = 0; i < n; ++i) {
      const size_t index = (size_t)(lcg64(seed + i) % n);
      const ZixChunk* volatile match =
        (const ZixChunk*)zix_hash_find_record(zhash, &inputs.chunks[index]);

#ifndef NDEBUG
      const ZixChunk* const m = match;
      assert(m);
      assert(!strcmp(m->buf, inputs.chunks[index].buf));
#endif

      (void)match;
    }
    fprintf(search_dat, "\t%lf\n", bench_end(&search_start));

    zix_hash_free(zhash);
    g_hash_table_unref(hash);
  }

  fclose(search_dat);
  fclose(insert_dat);
  free_inputs(&inputs);

  fprintf(stderr, "Wrote dict_insert.txt dict_search.txt\n");
  return 0;
}

int
main(int argc, char** argv)
{
  if (argc != 2) {
    fprintf(stderr, "Usage: %s INPUT_FILE\n", argv[0]);
    return 1;
  }

  const char* file = argv[1];
  FILE*       fd   = fopen(file, "r");
  if (!fd) {
    fprintf(stderr, "error: Failed to open file %s\n", file);
    return 1;
  }

  return run(fd);
}
