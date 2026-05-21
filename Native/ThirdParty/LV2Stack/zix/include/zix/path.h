// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_PATH_H
#define ZIX_PATH_H

#include <zix/allocator.h>
#include <zix/attributes.h>
#include <zix/string_view.h>

#include <stdbool.h>

/// A pure API function on Windows, a constant stub everywhere else
#ifdef _WIN32
#  define ZIX_PURE_WIN_API ZIX_PURE_API
#else
#  define ZIX_PURE_WIN_API ZIX_CONST_API
#endif

ZIX_BEGIN_DECLS

/**
   @defgroup zix_path Paths
   @ingroup zix_file_system

   Functions for interpreting and manipulating paths.  These functions are
   purely lexical and do not access any filesystem.

   @{
*/

/**
   @defgroup zix_path_concatenation Concatenation
   @{
*/

/// Join path `a` and path `b` with a single directory separator between them
ZIX_MALLOC_API char* ZIX_ALLOCATED
zix_path_join(ZixAllocator* ZIX_NULLABLE allocator,
              const char* ZIX_NULLABLE   a,
              const char* ZIX_NULLABLE   b);

/**
   @}
   @defgroup zix_path_lexical Lexical Transformations
   @{
*/

/**
   Return `path` with preferred directory separators.

   The returned path will be a copy of `path` with any directory separators
   converted to the preferred separator (backslash on Windows, slash everywhere
   else).
*/
ZIX_MALLOC_API char* ZIX_ALLOCATED
zix_path_preferred(ZixAllocator* ZIX_NULLABLE allocator,
                   const char* ZIX_NONNULL    path);

/**
   Return `path` converted to normal form.

   Paths in normal form have all dot segments removed and use only a single
   preferred separator for all separators (that is, any number of separators is
   replaced with a single "\" on Windows, and a single "/" everywhere else).

   Note that this function doesn't access the filesystem, so won't do anything
   like case normalization or symbolic link dereferencing.  For that, use
   zix_canonical_path().
*/
ZIX_MALLOC_API char* ZIX_ALLOCATED
zix_path_lexically_normal(ZixAllocator* ZIX_NULLABLE allocator,
                          const char* ZIX_NONNULL    path);

/**
   Return `path` relative to `base` if possible.

   If `path` is not within `base`, a copy is returned.  Otherwise, an
   equivalent path relative to `base` is returned (which may contain
   up-references).
*/
ZIX_MALLOC_API char* ZIX_ALLOCATED
zix_path_lexically_relative(ZixAllocator* ZIX_NULLABLE allocator,
                            const char* ZIX_NONNULL    path,
                            const char* ZIX_NONNULL    base);

/**
   @}
   @defgroup zix_path_decomposition Decomposition
   @{
*/

/// Return the root name of `path` like "C:", or null
ZIX_PURE_WIN_API ZIX_NONBLOCKING ZixStringView
zix_path_root_name(const char* ZIX_NONNULL path);

/// Return the root directory of `path` like "/" or "\", or null
ZIX_PURE_API ZIX_NONBLOCKING ZixStringView
zix_path_root_directory(const char* ZIX_NONNULL path);

/**
   Return the root path of `path`, or null.

   The universal root path (in normal form) is "/".  Root paths are their own
   roots, but note that the path returned by this function may be partially
   normalized.  For example, "/" is the root of "/", "//", "/.", and "/..".

   On Windows, the root may additionally be an absolute drive root like "C:\",
   a relative drive root like "C:", or a network root like "\\HOST\".

   @return A view of the root path within `path`, or an empty string if it has
   no root.
*/
ZIX_PURE_API ZIX_NONBLOCKING ZixStringView
zix_path_root_path(const char* ZIX_NONNULL path);

/**
   Return the relative path component of path without the root directory.

   If the path has no relative path (because it is empty or a root path), this
   returns null.
*/
ZIX_PURE_API ZIX_NONBLOCKING ZixStringView
zix_path_relative_path(const char* ZIX_NONNULL path);

/**
   Return the path to the directory that contains `path`.

   The parent of a root path is itself, but note that the path returned by this
   function may be partially normalized.  For example, "/" is the parent of
   "/", "//", "/.", and "/..".

   If `path` has a trailing separator, it is treated like an empty filename in
   a directory.  For example, the parent of "/a/" is "/a".

   If `path` is relative, then this returns either a relative path to the
   parent if possible, or null.  For example, the parent of "a/b" is "a".

   @return A view of the parent path within `path`, or the empty string if it
   has no parent.
*/
ZIX_PURE_API ZIX_NONBLOCKING ZixStringView
zix_path_parent_path(const char* ZIX_NONNULL path);

/**
   Return the filename component of `path` without any directories.

   The filename is the name after the last directory separator.  If the path
   has no filename, this returns null.
*/
ZIX_PURE_API ZIX_NONBLOCKING ZixStringView
zix_path_filename(const char* ZIX_NONNULL path);

/**
   Return the stem of the filename component of `path`.

   The "stem" is the filename without the extension, that is, everything up to
   the last "." if "." is not the first character.
*/
ZIX_PURE_API ZIX_NONBLOCKING ZixStringView
zix_path_stem(const char* ZIX_NONNULL path);

/**
   Return the extension of the filename component of `path`.

   The "extension" is everything past the last "." in the filename, if "." is
   not the first character.
*/
ZIX_PURE_API ZIX_NONBLOCKING ZixStringView
zix_path_extension(const char* ZIX_NONNULL path);

/**
   @}
   @defgroup zix_path_queries Queries
   @{
*/

/// Return true if `path` has a root path like "/" or "C:\"
ZIX_PURE_API ZIX_NONBLOCKING bool
zix_path_has_root_path(const char* ZIX_NULLABLE path);

/// Return true if `path` has a root name like "C:"
ZIX_PURE_WIN_API ZIX_NONBLOCKING bool
zix_path_has_root_name(const char* ZIX_NULLABLE path);

/// Return true if `path` has a root directory like "/" or "\"
ZIX_PURE_API ZIX_NONBLOCKING bool
zix_path_has_root_directory(const char* ZIX_NULLABLE path);

/// Return true if `path` has a relative path "dir/file.txt"
ZIX_PURE_API ZIX_NONBLOCKING bool
zix_path_has_relative_path(const char* ZIX_NULLABLE path);

/// Return true if `path` has a parent path like "dir/"
ZIX_PURE_API ZIX_NONBLOCKING bool
zix_path_has_parent_path(const char* ZIX_NULLABLE path);

/// Return true if `path` has a filename like "file.txt"
ZIX_PURE_API ZIX_NONBLOCKING bool
zix_path_has_filename(const char* ZIX_NULLABLE path);

/// Return true if `path` has a stem like "file"
ZIX_PURE_API ZIX_NONBLOCKING bool
zix_path_has_stem(const char* ZIX_NULLABLE path);

/// Return true if `path` has an extension like ".txt"
ZIX_PURE_API ZIX_NONBLOCKING bool
zix_path_has_extension(const char* ZIX_NULLABLE path);

/// Return true if `path` is an absolute path
ZIX_PURE_API ZIX_NONBLOCKING bool
zix_path_is_absolute(const char* ZIX_NULLABLE path);

/// Return true if `path` is a relative path
ZIX_PURE_API ZIX_NONBLOCKING bool
zix_path_is_relative(const char* ZIX_NULLABLE path);

/**
   @}
   @}
*/

ZIX_END_DECLS

#endif /* ZIX_PATH_H */
