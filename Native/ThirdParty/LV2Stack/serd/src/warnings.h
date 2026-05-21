// Copyright 2019-2024 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef SERD_SRC_WARNINGS_H
#define SERD_SRC_WARNINGS_H

#ifdef __clang__

/// Clang 15 null checking regressed, so we need to suppress it sometimes
#  define SERD_DISABLE_NULL_WARNINGS \
    _Pragma("clang diagnostic push") \
    _Pragma("clang diagnostic ignored \"-Wnullable-to-nonnull-conversion\"")

#  define SERD_RESTORE_WARNINGS _Pragma("clang diagnostic pop")

#else

#  define SERD_DISABLE_NULL_WARNINGS
#  define SERD_RESTORE_WARNINGS

#endif

#endif // SERD_SRC_WARNINGS_H
