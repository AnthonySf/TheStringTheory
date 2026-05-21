// Copyright 2021 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/bump_allocator.h>

#include <zix/allocator.h>
#include <zix/attributes.h>

#include <assert.h>
#include <stddef.h>
#include <stdint.h>
#include <string.h>

static const size_t min_alignment = sizeof(uintmax_t);

ZIX_PURE_FUNC static size_t
round_up_multiple(const size_t number, const size_t factor)
{
  assert(factor);                         // Factor must be non-zero
  assert((factor & (factor - 1U)) == 0U); // Factor must be a power of two

  return (number + factor - 1U) & ~(factor - 1U);
}

ZIX_MALLOC_FUNC static void*
zix_bump_malloc(ZixAllocator* const allocator, const size_t size)
{
  ZixBumpAllocator* const state = (ZixBumpAllocator*)allocator;

  assert((uintptr_t)((char*)state->buffer + state->top) % min_alignment == 0U);

  /* For C malloc(), the result is guaranteed to be aligned for any variable.
     What that means is deliberately vague to accommodate diverse platforms,
     but sizeof(uintmax_t) is more than enough on all the common ones. */

  const size_t real_size = round_up_multiple(size, min_alignment);
  if (state->top + real_size > state->capacity) {
    return NULL;
  }

  state->last = state->top;
  state->top += real_size;

  return (void*)((char*)state->buffer + state->last);
}

ZIX_MALLOC_FUNC static void*
zix_bump_calloc(ZixAllocator* const allocator,
                const size_t        nmemb,
                const size_t        size)
{
  const size_t total_size = nmemb * size;
  void* const  ptr        = zix_bump_malloc(allocator, total_size);
  if (ptr) {
    memset(ptr, 0, total_size);
  }

  return ptr;
}

static void*
zix_bump_realloc(ZixAllocator* const allocator,
                 void* const         ptr,
                 const size_t        size)
{
  ZixBumpAllocator* const state = (ZixBumpAllocator*)allocator;

  if ((char*)ptr != (char*)state->buffer + state->last) {
    return NULL;
  }

  const size_t new_top = state->last + size;
  if (new_top > state->capacity) {
    return NULL;
  }

  state->top = new_top;
  return ptr;
}

static void
zix_bump_free(ZixAllocator* const allocator, void* const ptr)
{
  ZixBumpAllocator* const state = (ZixBumpAllocator*)allocator;

  if (ptr == (char*)state->buffer + state->last) {
    state->top = state->last; // Reclaim the space of the last allocation
  }
}

ZIX_MALLOC_FUNC static void*
zix_bump_aligned_alloc(ZixAllocator* const allocator,
                       const size_t        alignment,
                       const size_t        size)
{
  ZixBumpAllocator* const state    = (ZixBumpAllocator*)allocator;
  const size_t            old_last = state->last;
  const size_t            old_top  = state->top;

  assert(alignment >= min_alignment);
  assert(size % alignment == 0U);

  /* First, calculate how much we need to offset the top to achieve this
     alignment.  Note that it's not the offset that needs to be aligned (since
     the buffer may not be), but the final address, so we do the calculation
     with "global" addresses and calculate the offset from that. */

  const char*     top_ptr          = (char*)state->buffer + state->top;
  const uintptr_t top_addr         = (uintptr_t)top_ptr;
  const uintptr_t aligned_top_addr = round_up_multiple(top_addr, alignment);
  const uintptr_t offset           = aligned_top_addr - top_addr;
  if (state->top + offset > state->capacity) {
    return NULL;
  }

  /* Then, adjust the top to be aligned and try to allocate.  If that fails,
     reset everything to how it was before this call. */

  state->top += offset;

  void* const ptr = zix_bump_malloc(allocator, size);
  if (!ptr) {
    state->last = old_last;
    state->top  = old_top;
    return NULL;
  }

  return ptr;
}

static void
zix_bump_aligned_free(ZixAllocator* const allocator, void* const ptr)
{
  zix_bump_free(allocator, ptr);
}

ZIX_CONST_FUNC ZIX_REALTIME ZixBumpAllocator
zix_bump_allocator(const size_t capacity, void* buffer)
{
  const size_t aligned_top = (uintptr_t)buffer % min_alignment;

  ZixBumpAllocator bump_allocator = {
    {
      zix_bump_malloc,
      zix_bump_calloc,
      zix_bump_realloc,
      zix_bump_free,
      zix_bump_aligned_alloc,
      zix_bump_aligned_free,
    },
    buffer,
    aligned_top,
    aligned_top,
    capacity,
  };

  return bump_allocator;
}
