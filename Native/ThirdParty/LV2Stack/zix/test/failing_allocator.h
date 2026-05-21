// Copyright 2021-2024 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_TEST_FAILING_ALLOCATOR_H
#define ZIX_TEST_FAILING_ALLOCATOR_H

#include <zix/allocator.h>

#include <stddef.h>

/// An allocator that fails after some number of successes for testing
typedef struct {
  ZixAllocator base;          ///< Base allocator instance
  size_t       n_allocations; ///< Number of attempted allocations
  size_t       n_remaining;   ///< Number of remaining successful allocations
} ZixFailingAllocator;

/// Return an allocator configured by default to succeed
ZixFailingAllocator
zix_failing_allocator(void);

/// Reset an allocator to fail after some number of "allowed" allocations
size_t
zix_failing_allocator_reset(ZixFailingAllocator* allocator, size_t n_allowed);

#endif // ZIX_TEST_FAILING_ALLOCATOR_H
