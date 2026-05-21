// Copyright 2014-2023 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "failing_allocator.h"

#include <zix/allocator.h>
#include <zix/bump_allocator.h>

#include <assert.h>
#include <stddef.h>
#include <stdint.h>

static void
test_allocator(void)
{
  // Just a basic smoke test to check that things seem to be working

  ZixAllocator* const allocator = zix_default_allocator();

  char* const malloced = (char*)zix_malloc(allocator, 4);
  malloced[0]          = 0;
  malloced[3]          = 3;
  assert(malloced[0] == 0);
  assert(malloced[3] == 3);

  char* const calloced = (char*)zix_calloc(allocator, 4, 1);
  assert(calloced[0] == 0);
  assert(calloced[1] == 0);
  assert(calloced[2] == 0);
  assert(calloced[3] == 0);

  char* const realloced = (char*)zix_realloc(allocator, calloced, 8);
  assert(realloced[0] == 0);
  assert(realloced[1] == 0);
  assert(realloced[2] == 0);
  assert(realloced[3] == 0);
  realloced[4] = 4;
  realloced[5] = 5;
  realloced[6] = 6;
  realloced[7] = 7;
  assert(realloced[4] == 4);
  assert(realloced[5] == 5);
  assert(realloced[6] == 6);
  assert(realloced[7] == 7);

  char* const aligned = (char*)zix_aligned_alloc(allocator, 4096, 4096);
  assert((uintptr_t)aligned % 4096 == 0);
  aligned[0] = 0;
  aligned[3] = 3;
  assert(aligned[0] == 0);
  assert(aligned[3] == 3);

  zix_aligned_free(allocator, aligned);
  zix_free(allocator, realloced);
  zix_free(allocator, malloced);
}

static void
test_bump_allocator(void)
{
  char             buffer[1024] = {0};
  ZixBumpAllocator allocator    = zix_bump_allocator(sizeof(buffer), buffer);

  assert(!zix_malloc(&allocator.base, 1025));

  char* const malloced = (char*)zix_malloc(&allocator.base, 3);
  assert(malloced >= buffer);
  assert(malloced <= buffer + sizeof(buffer));
  assert((uintptr_t)malloced % sizeof(uintmax_t) == 0U);

  assert(!zix_calloc(&allocator.base, 1017, 1));

  char* const calloced = (char*)zix_calloc(&allocator.base, 4, 1);
  assert(calloced > malloced);
  assert(calloced <= buffer + sizeof(buffer));
  assert((uintptr_t)calloced % sizeof(uintmax_t) == 0U);
  assert(!calloced[0]);
  assert(!calloced[1]);
  assert(!calloced[2]);
  assert(!calloced[3]);

  char* const realloced = (char*)zix_realloc(&allocator.base, calloced, 8);
  assert(realloced == calloced);

  assert(!zix_realloc(&allocator.base, malloced, 8));     // Not the top
  assert(!zix_realloc(&allocator.base, realloced, 4089)); // No space
  assert(!zix_calloc(&allocator.base, 4089, 1));          // No space

  zix_free(&allocator.base, realloced);

  char* const reclaimed = (char*)zix_malloc(&allocator.base, 512);
  assert(reclaimed);
  assert(reclaimed == realloced);

  assert(!zix_aligned_alloc(&allocator.base, sizeof(uintmax_t), 1024));
  assert(!zix_aligned_alloc(&allocator.base, 1024, 1024));
  assert(!zix_aligned_alloc(&allocator.base, 2048, 2048));
  assert(!zix_aligned_alloc(&allocator.base, 4096, 4096));
  assert(!zix_aligned_alloc(&allocator.base, 8192, 8192));
  assert(!zix_aligned_alloc(&allocator.base, 4096, 4096));
  assert(!zix_aligned_alloc(&allocator.base, 2048, 2048));
  assert(!zix_aligned_alloc(&allocator.base, 1024, 1024));
  assert(!zix_aligned_alloc(&allocator.base, 512, 512));

  char* const aligned = (char*)zix_aligned_alloc(&allocator.base, 128, 128);
  assert(aligned);
  assert(aligned >= reclaimed);
  assert(aligned <= buffer + sizeof(buffer));
  assert((uintptr_t)aligned % 128 == 0U);

  assert(!zix_aligned_alloc(&allocator.base, 8, 896));
  assert(zix_aligned_alloc(&allocator.base, 8, 8));

  zix_aligned_free(&allocator.base, aligned);
  zix_free(&allocator.base, reclaimed); // Correct, but a noop
  zix_free(&allocator.base, malloced);  // Correct, but a noop
}

static void
test_failing_allocator(void)
{
  ZixFailingAllocator allocator = zix_failing_allocator();
  zix_failing_allocator_reset(&allocator, 0);

  assert(!zix_malloc(&allocator.base, 16U));
  assert(!zix_calloc(&allocator.base, 16U, 1U));
  assert(!zix_realloc(&allocator.base, NULL, 32U));
  zix_free(&allocator.base, NULL);
  assert(!zix_aligned_alloc(&allocator.base, 8U, 16U));
  zix_aligned_free(&allocator.base, NULL);
}

int
main(void)
{
  test_allocator();
  test_bump_allocator();
  test_failing_allocator();

  return 0;
}
