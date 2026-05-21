// Copyright 2021-2024 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "failing_allocator.h"

#include <zix/allocator.h>
#include <zix/attributes.h>

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

static bool
attempt(ZixFailingAllocator* const allocator)
{
  ++allocator->n_allocations;

  if (!allocator->n_remaining) {
    return false;
  }

  --allocator->n_remaining;
  return true;
}

ZIX_MALLOC_FUNC static void*
zix_failing_malloc(ZixAllocator* const allocator, const size_t size)
{
  ZixFailingAllocator* const state = (ZixFailingAllocator*)allocator;
  ZixAllocator* const        base  = zix_default_allocator();

  return attempt(state) ? base->malloc(base, size) : NULL;
}

ZIX_MALLOC_FUNC static void*
zix_failing_calloc(ZixAllocator* const allocator,
                   const size_t        nmemb,
                   const size_t        size)
{
  ZixFailingAllocator* const state = (ZixFailingAllocator*)allocator;
  ZixAllocator* const        base  = zix_default_allocator();

  return attempt(state) ? base->calloc(base, nmemb, size) : NULL;
}

static void*
zix_failing_realloc(ZixAllocator* const allocator,
                    void* const         ptr,
                    const size_t        size)
{
  ZixFailingAllocator* const state = (ZixFailingAllocator*)allocator;
  ZixAllocator* const        base  = zix_default_allocator();

  return attempt(state) ? base->realloc(base, ptr, size) : NULL;
}

static void
zix_failing_free(ZixAllocator* const allocator, void* const ptr)
{
  (void)allocator;

  ZixAllocator* const base = zix_default_allocator();

  base->free(base, ptr);
}

ZIX_MALLOC_FUNC static void*
zix_failing_aligned_alloc(ZixAllocator* const allocator,
                          const size_t        alignment,
                          const size_t        size)
{
  ZixFailingAllocator* const state = (ZixFailingAllocator*)allocator;
  ZixAllocator* const        base  = zix_default_allocator();

  return attempt(state) ? base->aligned_alloc(base, alignment, size) : NULL;
}

static void
zix_failing_aligned_free(ZixAllocator* const allocator, void* const ptr)
{
  (void)allocator;

  ZixAllocator* const base = zix_default_allocator();

  base->aligned_free(base, ptr);
}

ZIX_CONST_FUNC ZixFailingAllocator
zix_failing_allocator(void)
{
  ZixFailingAllocator failing_allocator = {
    {
      zix_failing_malloc,
      zix_failing_calloc,
      zix_failing_realloc,
      zix_failing_free,
      zix_failing_aligned_alloc,
      zix_failing_aligned_free,
    },
    0,
    SIZE_MAX,
  };

  return failing_allocator;
}

size_t
zix_failing_allocator_reset(ZixFailingAllocator* const allocator,
                            const size_t               n_allowed)
{
  const size_t n_allocations = allocator->n_allocations;

  allocator->n_allocations = 0U;
  allocator->n_remaining   = n_allowed;

  return n_allocations;
}
