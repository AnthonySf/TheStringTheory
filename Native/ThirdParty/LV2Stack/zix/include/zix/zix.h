// Copyright 2016-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_ZIX_H
#define ZIX_ZIX_H

// IWYU pragma: begin_exports

/**
   @defgroup zix Zix C API
   @{
*/

/**
   @defgroup zix_utilities Utilities
   @{
*/

#include <zix/attributes.h>
#include <zix/status.h>
#include <zix/string_view.h>
#include <zix/warnings.h>

/**
   @}
   @defgroup zix_allocation Allocation
   @{
*/

#include <zix/allocator.h>
#include <zix/bump_allocator.h>

/**
   @}
   @defgroup zix_algorithms Algorithms
   @{
*/

#include <zix/digest.h>

/**
   @}
   @defgroup zix_data_structures Data Structures
   @{
*/

#include <zix/btree.h>
#include <zix/hash.h>
#include <zix/ring.h>
#include <zix/tree.h>

/**
   @}
   @defgroup zix_threading Threading
   @{
*/

#include <zix/sem.h>
#include <zix/thread.h>

/**
   @}
   @defgroup zix_file_system File System
   @{
*/

#include <zix/filesystem.h>
#include <zix/path.h>

/**
   @}
   @defgroup zix_environment Environment
   @{
*/

#include <zix/environment.h>

/**
   @}
   @}
*/

// IWYU pragma: end_exports

#endif /* ZIX_ZIX_H */
