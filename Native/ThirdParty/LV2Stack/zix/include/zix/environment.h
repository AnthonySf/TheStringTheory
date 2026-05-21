// Copyright 2024 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_ENVIRONMENT_H
#define ZIX_ENVIRONMENT_H

#include <zix/allocator.h>
#include <zix/attributes.h>

ZIX_BEGIN_DECLS

/**
   @defgroup zix_expand Variable Expansion
   @ingroup zix_environment
   @{
*/

/**
   Expand shell-style variables in a string.

   On Windows, this expands environment variable references like
   "%USERPROFILE%".  On POSIX systems, it expands environment variable
   references like "$HOME", and the special path component "~".

   @param allocator Allocator used for returned string.
   @param string Input string to expand.
   @return A newly allocated copy of `string` with variables expanded, or null.
*/
ZIX_MALLOC_API char* ZIX_ALLOCATED
zix_expand_environment_strings(ZixAllocator* ZIX_NULLABLE allocator,
                               const char* ZIX_NONNULL    string);

/**
   @}
*/

ZIX_END_DECLS

#endif /* ZIX_ENVIRONMENT_H */
