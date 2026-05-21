// Copyright 2011-2021 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/allocator.h>

#include "zix_config.h"

#include <zix/attributes.h>

#ifdef _WIN32
#  include <malloc.h>
#  include <windows.h>
#endif

#include <stdlib.h>

ZIX_MALLOC_FUNC static void*
zix_default_malloc(ZixAllocator* const allocator, const size_t size)
{
  (void)allocator;
  return malloc(size);
}

ZIX_MALLOC_FUNC static void*
zix_default_calloc(ZixAllocator* const allocator,
                   const size_t        nmemb,
                   const size_t        size)
{
  (void)allocator;
  return calloc(nmemb, size);
}

static void*
zix_default_realloc(ZixAllocator* const allocator,
                    void* const         ptr,
                    const size_t        size)
{
  (void)allocator;
  return realloc(ptr, size);
}

static void
zix_default_free(ZixAllocator* const allocator, void* const ptr)
{
  (void)allocator;
  free(ptr);
}

ZIX_MALLOC_FUNC static void*
zix_default_aligned_alloc(ZixAllocator* const allocator,
                          const size_t        alignment,
                          const size_t        size)
{
  (void)allocator;

#ifdef _WIN32
  return _aligned_malloc(size, alignment);
#elif USE_POSIX_MEMALIGN
  void* ptr = NULL;
  return posix_memalign(&ptr, alignment, size) ? NULL : ptr;
#else
  (void)alignment;
  (void)size;
  return NULL;
#endif
}

static void
zix_default_aligned_free(ZixAllocator* const allocator, void* const ptr)
{
  (void)allocator;

#ifdef _WIN32
  _aligned_free(ptr);
#else
  free(ptr);
#endif
}

ZixAllocator*
zix_default_allocator(void)
{
  static ZixAllocator default_allocator = {
    zix_default_malloc,
    zix_default_calloc,
    zix_default_realloc,
    zix_default_free,
    zix_default_aligned_alloc,
    zix_default_aligned_free,
  };

  return &default_allocator;
}
