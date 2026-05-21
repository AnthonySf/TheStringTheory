// Copyright 2021-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_ALLOCATOR_H
#define ZIX_ALLOCATOR_H

#include <zix/attributes.h>

#include <stddef.h>

ZIX_BEGIN_DECLS

/**
   @defgroup zix_allocator Allocator
   @ingroup zix_allocation
   @{
*/

/**
   A memory allocator.

   This object-like structure provides an interface like the standard C
   functions malloc(), calloc(), realloc(), free(), and aligned_alloc().  It
   contains function pointers that differ from their standard counterparts by
   taking a context parameter (a pointer to this struct), which allows the user
   to implement custom stateful allocators.
*/
typedef struct ZixAllocatorImpl ZixAllocator;

/**
   General malloc-like memory allocation function.

   This works like the standard C malloc(), except has an additional handle
   parameter for implementing stateful allocators without static data.
*/
typedef void* ZIX_ALLOCATED (*ZixMallocFunc)( //
  ZixAllocator* ZIX_NULLABLE allocator,
  size_t                     size);

/**
   General calloc-like memory allocation function.

   This works like the standard C calloc(), except has an additional handle
   parameter for implementing stateful allocators without static data.
*/
typedef void* ZIX_ALLOCATED (*ZixCallocFunc)( //
  ZixAllocator* ZIX_NULLABLE allocator,
  size_t                     nmemb,
  size_t                     size);

/**
   General realloc-like memory reallocation function.

   This works like the standard C remalloc(), except has an additional handle
   parameter for implementing stateful allocators without static data.
*/
typedef void* ZIX_ALLOCATED (*ZixReallocFunc)( //
  ZixAllocator* ZIX_NULLABLE allocator,
  void* ZIX_NULLABLE         ptr,
  size_t                     size);

/**
   General free-like memory deallocation function.

   This works like the standard C remalloc(), except has an additional handle
   parameter for implementing stateful allocators without static data.
*/
typedef void (*ZixFreeFunc)( //
  ZixAllocator* ZIX_NULLABLE allocator,
  void* ZIX_NULLABLE         ptr);

/**
   General aligned_alloc-like memory deallocation function.

   This works like the standard C aligned_alloc(), except has an additional
   handle parameter for implementing stateful allocators without static data.
*/
typedef void* ZIX_ALLOCATED (*ZixAlignedAllocFunc)( //
  ZixAllocator* ZIX_NULLABLE allocator,
  size_t                     alignment,
  size_t                     size);

/**
   General aligned memory deallocation function.

   This works like the standard C free(), but must be used to free memory
   allocated with the aligned_alloc() method of the allocator.  This allows
   portability to systems (like Windows) that can't use the same free function
   in these cases.
*/
typedef void (*ZixAlignedFreeFunc)( //
  ZixAllocator* ZIX_NULLABLE allocator,
  void* ZIX_NULLABLE         ptr);

/// Definition of ZixAllocator
struct ZixAllocatorImpl {
  ZixMallocFunc ZIX_UNSPECIFIED       malloc;        ///< Allocate
  ZixCallocFunc ZIX_UNSPECIFIED       calloc;        ///< Allocate and zero
  ZixReallocFunc ZIX_UNSPECIFIED      realloc;       ///< Reallocate
  ZixFreeFunc ZIX_UNSPECIFIED         free;          ///< Free
  ZixAlignedAllocFunc ZIX_UNSPECIFIED aligned_alloc; ///< Allocate aligned
  ZixAlignedFreeFunc ZIX_UNSPECIFIED  aligned_free;  ///< Free aligned
};

/// Return the default allocator which simply uses the system allocator
ZIX_CONST_API ZixAllocator* ZIX_NONNULL
zix_default_allocator(void);

/// Convenience wrapper that defers to malloc() if allocator is null
ZIX_MALLOC_FUNC ZIX_ALLOC_SIZE(2) static inline void* ZIX_ALLOCATED
zix_malloc(ZixAllocator* const ZIX_NULLABLE allocator, const size_t size)
{
  ZixAllocator* const actual = allocator ? allocator : zix_default_allocator();

  return actual->malloc(actual, size);
}

/// Convenience wrapper that defers to calloc() if allocator is null
ZIX_MALLOC_FUNC ZIX_ALLOC_COUNT_SIZE(2, 3) static inline void* ZIX_ALLOCATED
zix_calloc(ZixAllocator* const ZIX_NULLABLE allocator,
           const size_t                     nmemb,
           const size_t                     size)
{
  ZixAllocator* const actual = allocator ? allocator : zix_default_allocator();

  return actual->calloc(actual, nmemb, size);
}

/// Convenience wrapper that defers to realloc() if allocator is null
ZIX_NODISCARD ZIX_ALLOC_SIZE(3) static inline void* ZIX_ALLOCATED
zix_realloc(ZixAllocator* const ZIX_NULLABLE allocator,
            void* const ZIX_NULLABLE         ptr,
            const size_t                     size)
{
  ZixAllocator* const actual = allocator ? allocator : zix_default_allocator();

  return actual->realloc(actual, ptr, size);
}

/// Convenience wrapper that defers to free() if allocator is null
static inline void
zix_free(ZixAllocator* const ZIX_NULLABLE allocator,
         void* const ZIX_NULLABLE         ptr)
{
  ZixAllocator* const actual = allocator ? allocator : zix_default_allocator();

  actual->free(actual, ptr);
}

/// Convenience wrapper that defers to the system allocator if allocator is null
ZIX_MALLOC_FUNC ZIX_ALLOC_SIZE(3) static inline void* ZIX_ALLOCATED
zix_aligned_alloc(ZixAllocator* const ZIX_NULLABLE allocator,
                  const size_t                     alignment,
                  const size_t                     size)
{
  ZixAllocator* const actual = allocator ? allocator : zix_default_allocator();

  return actual->aligned_alloc(actual, alignment, size);
}

/// Convenience wrapper that defers to the system allocator if allocator is null
static inline void
zix_aligned_free(ZixAllocator* const ZIX_NULLABLE allocator,
                 void* const ZIX_NULLABLE         ptr)
{
  ZixAllocator* const actual = allocator ? allocator : zix_default_allocator();

  actual->aligned_free(actual, ptr);
}

/**
   @}
*/

ZIX_END_DECLS

#endif /* ZIX_ALLOCATOR_H */
