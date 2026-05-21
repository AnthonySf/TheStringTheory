// Copyright 2021 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_BUMP_ALLOCATOR_H
#define ZIX_BUMP_ALLOCATOR_H

#include <zix/allocator.h>
#include <zix/attributes.h>

#include <stddef.h>

/**
   @defgroup bump_allocator Bump Allocator
   @ingroup zix_allocation
   @{
*/

/**
   A simple bump-pointer allocator that never frees.

   This is about the simplest possible allocator that is useful in practice.
   It uses a user-provided memory buffer with a fixed size, and allocates by
   simply "bumping" the top offset like a stack.  This approach is simple,
   extremely fast, and hard real-time safe, but at the cost of being limited to
   narrow use cases since there is (almost) no support for deallocation.

   Using this allocator requires knowing up-front the total amount of memory
   that will be allocated (without reuse).  Typically this makes sense in short
   scopes like a function call.

   This allocator adheres to standard C semantics as much as possible, but is
   much more restrictive.  Specifically:

   - All allocations are aligned to sizeof(uintmax_t).

   - Both free() and realloc() only work on the most recently allocated
     pointer, essentially serving as a cheap pop and pop-push, respectively.

   - Calling free() means that realloc() will fail and free() will do nothing
     until the next allocation.  In other words, free() can't be used twice
     in a row.

   - There is no relocation: realloc() always returns either the input pointer,
     or null.
*/
typedef struct {
  ZixAllocator      base;     ///< Base allocator instance
  void* ZIX_NONNULL buffer;   ///< User-owned memory buffer
  size_t            last;     ///< Last allocation offset in bytes
  size_t            top;      ///< Stack top/end offset in bytes
  size_t            capacity; ///< Size of buffer in bytes (the maximum top)
} ZixBumpAllocator;

/// Return a bump allocator that works within a provided buffer
ZIX_API ZIX_REALTIME ZixBumpAllocator
zix_bump_allocator(size_t capacity, void* ZIX_NONNULL buffer);

/**
   @}
*/

#endif // ZIX_BUMP_ALLOCATOR_H
