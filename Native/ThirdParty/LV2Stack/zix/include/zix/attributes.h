// Copyright 2021-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_ATTRIBUTES_H
#define ZIX_ATTRIBUTES_H

/**
   @defgroup zix_attributes Attributes
   @ingroup zix_utilities
   @{
*/

// Public declaration scope
#ifdef __cplusplus
#  define ZIX_BEGIN_DECLS extern "C" {
#  define ZIX_END_DECLS }
#else
#  define ZIX_BEGIN_DECLS // Begin public API definitions
#  define ZIX_END_DECLS   // End public API definitions
#endif

// ZIX_API must be used to decorate things in the public API
#ifndef ZIX_API
#  if defined(_WIN32) && !defined(ZIX_STATIC) && defined(ZIX_INTERNAL)
#    define ZIX_API __declspec(dllexport)
#  elif defined(_WIN32) && !defined(ZIX_STATIC)
#    define ZIX_API __declspec(dllimport)
#  elif defined(__GNUC__)
#    define ZIX_API __attribute__((visibility("default")))
#  else
#    define ZIX_API
#  endif
#endif

// GCC function attributes
#ifdef __GNUC__
#  define ZIX_ALWAYS_INLINE_FUNC __attribute__((always_inline))
#  define ZIX_PURE_FUNC __attribute__((pure))
#  define ZIX_CONST_FUNC __attribute__((const))
#  define ZIX_MALLOC_FUNC __attribute__((malloc))
#  define ZIX_NODISCARD __attribute__((warn_unused_result))
#else
#  define ZIX_ALWAYS_INLINE_FUNC ///< Function should always be inlined
#  define ZIX_PURE_FUNC          ///< Function only reads memory
#  define ZIX_CONST_FUNC         ///< Function only reads its parameters
#  define ZIX_MALLOC_FUNC        ///< Function allocates pointer-free memory
#  define ZIX_NODISCARD          ///< Function return value must be used
#endif

// Clang nonallocating/nonblocking function attributes
#if defined(__clang__) && __clang_major__ >= 20
#  define ZIX_NONBLOCKING __attribute__((nonblocking))
#  define ZIX_REALTIME ZIX_NONBLOCKING
#else
#  define ZIX_NONBLOCKING ///< Function doesn't allocate or block
#  define ZIX_REALTIME    ///< Function is nonblocking and constant time
#endif

#define ZIX_PURE_API ZIX_API ZIX_PURE_FUNC ZIX_NODISCARD
#define ZIX_CONST_API ZIX_API ZIX_CONST_FUNC ZIX_NODISCARD
#define ZIX_MALLOC_API ZIX_API ZIX_MALLOC_FUNC ZIX_NODISCARD

// Malloc and calloc-like functions with count/size arguments
#if (defined(__GNUC__) && __GNUC__ >= 11) || \
  (defined(__clang__) && __clang_major__ >= 4)
#  define ZIX_ALLOC_SIZE(s) __attribute__((alloc_size(s)))
#  define ZIX_ALLOC_COUNT_SIZE(n, s) __attribute__((alloc_size(n, s)))
#else
/// Function with malloc-like parameters
/// @param s 1-based index of size parameter
#  define ZIX_ALLOC_SIZE(s)

/// Function with calloc-like parameters
/// @param n 1-based index of number of elements parameter
/// @param s 1-based index of element size parameter
#  define ZIX_ALLOC_COUNT_SIZE(n, s)
#endif

// Printf-like function with format arguments
#ifdef __GNUC__
#  define ZIX_LOG_FUNC(fmt, arg1) __attribute__((format(printf, fmt, arg1)))
#else
/// Function with printf-like parameters
/// @param fmt 1-based index of format string parameter
/// @param arg1 1-based index of first format argument parameter
#  define ZIX_LOG_FUNC(fmt, arg1)
#endif

// Unused parameter macro to suppresses warnings and make it impossible to use
#ifdef __cplusplus
#  define ZIX_UNUSED(name)
#elif defined(__GNUC__) || defined(__clang__)
#  define ZIX_UNUSED(name) name##_unused __attribute__((__unused__))
#elif defined(_MSC_VER)
#  define ZIX_UNUSED(name) __pragma(warning(suppress : 4100)) name
#else
#  define ZIX_UNUSED(name) name ///< Unused parameter
#endif

// Clang nullability annotations
#if defined(__clang__) && __clang_major__ >= 7
#  define ZIX_NONNULL _Nonnull
#  define ZIX_NULLABLE _Nullable
#  define ZIX_ALLOCATED _Null_unspecified
#  define ZIX_UNSPECIFIED _Null_unspecified
#else
#  define ZIX_NONNULL     ///< Non-null pointer
#  define ZIX_NULLABLE    ///< Nullable pointer
#  define ZIX_ALLOCATED   ///< Allocated pointer
#  define ZIX_UNSPECIFIED ///< Pointer with unspecified nullability
#endif

/**
   @}
*/

#endif /* ZIX_ATTRIBUTES_H */
