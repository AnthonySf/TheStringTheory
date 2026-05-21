// Copyright 2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_WARNINGS_H
#define ZIX_WARNINGS_H

/**
   @defgroup zix_attributes Attributes
   @ingroup zix_utilities
   @{
*/

#ifdef __clang__

#  if __clang_major__ >= 20
#    define ZIX_DISABLE_EFFECT_WARNINGS \
      _Pragma("clang diagnostic push")  \
      _Pragma("clang diagnostic ignored \"-Wfunction-effects\"")
#  else
#    define ZIX_DISABLE_EFFECT_WARNINGS _Pragma("clang diagnostic push")
#  endif

#  define ZIX_RESTORE_WARNINGS _Pragma("clang diagnostic pop")

#elif defined(__GNUC__)

#  define ZIX_DISABLE_EFFECT_WARNINGS _Pragma("GCC diagnostic push")
#  define ZIX_RESTORE_WARNINGS _Pragma("GCC diagnostic pop")

#else

#  define ZIX_DISABLE_EFFECT_WARNINGS ///< Disable function effect warnings
#  define ZIX_RESTORE_WARNINGS        ///< Restore disabled warnings

#endif

/**
   @}
*/

#endif /* ZIX_WARNINGS_H */
