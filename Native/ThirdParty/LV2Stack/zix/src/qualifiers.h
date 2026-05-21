// Copyright 2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_QUALIFIERS_H
#define ZIX_QUALIFIERS_H

/// A static constant expression usable at compile time
#if ((defined(__STDC_VERSION__) && __STDC_VERSION__ >= 202311L) || \
     (defined(__cplusplus) && __cplusplus >= 201103L))
#  define ZIX_CONSTEXPR constexpr
#else
#  define ZIX_CONSTEXPR const
#endif

#endif // ZIX_QUALIFIERS_H
