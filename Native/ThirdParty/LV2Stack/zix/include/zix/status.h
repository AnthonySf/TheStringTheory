// Copyright 2016-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_STATUS_H
#define ZIX_STATUS_H

#include <zix/attributes.h>

ZIX_BEGIN_DECLS

/**
   @defgroup zix_status Status Codes
   @ingroup zix_utilities
   @{
*/

/// A status code returned by functions
typedef enum {
  ZIX_STATUS_SUCCESS,       ///< Success
  ZIX_STATUS_ERROR,         ///< Unknown error
  ZIX_STATUS_NO_MEM,        ///< Out of memory
  ZIX_STATUS_NOT_FOUND,     ///< Not found
  ZIX_STATUS_EXISTS,        ///< Exists
  ZIX_STATUS_BAD_ARG,       ///< Bad argument
  ZIX_STATUS_BAD_PERMS,     ///< Bad permissions
  ZIX_STATUS_REACHED_END,   ///< Reached end
  ZIX_STATUS_TIMEOUT,       ///< Timeout
  ZIX_STATUS_OVERFLOW,      ///< Overflow
  ZIX_STATUS_NOT_SUPPORTED, ///< Not supported
  ZIX_STATUS_UNAVAILABLE,   ///< Resource unavailable
  ZIX_STATUS_NO_SPACE,      ///< Out of storage space
  ZIX_STATUS_MAX_LINKS,     ///< Too many links
} ZixStatus;

/**
   Return a string describing a status code in plain English.

   The returned string is always one sentence, with an uppercase first
   character, and no trailing period.
*/
ZIX_CONST_API ZIX_REALTIME const char*
zix_strerror(ZixStatus status);

/**
   @}
*/

ZIX_END_DECLS

#endif /* ZIX_STATUS_H */
