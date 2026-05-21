// Copyright 2020-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef WARNINGS_H
#define WARNINGS_H

#include <zix/warnings.h>

#ifdef __clang__

#  define ZIX_DISABLE_GLIB_WARNINGS                                         \
    _Pragma("clang diagnostic push")                                        \
    _Pragma("clang diagnostic ignored \"-Wdocumentation-unknown-command\"") \
    _Pragma("clang diagnostic ignored \"-Wdocumentation\"")                 \
    _Pragma("clang diagnostic ignored \"-Wreserved-id-macro\"")

#elif defined(__GNUC__)

#  define ZIX_DISABLE_GLIB_WARNINGS _Pragma("GCC diagnostic push")

#else

#  define ZIX_DISABLE_GLIB_WARNINGS

#endif

#endif // WARNINGS_H
