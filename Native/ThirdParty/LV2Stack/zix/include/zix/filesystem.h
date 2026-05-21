// Copyright 2007-2022 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_FILESYSTEM_H
#define ZIX_FILESYSTEM_H

#include <zix/allocator.h>
#include <zix/attributes.h>
#include <zix/status.h>

#if !(defined(_FILE_OFFSET_BITS) && _FILE_OFFSET_BITS == 64)
#  include <stddef.h>
#endif

#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>

ZIX_BEGIN_DECLS

/**
   @defgroup zix_fs_ops Operations
   @ingroup zix_file_system
   @{
*/

/**
   @defgroup zix_fs_creation Creation and Removal
   @{
*/

/// Options to control filesystem copy operations
typedef enum {
  ZIX_COPY_OPTION_NONE               = 0U,       ///< Report any error
  ZIX_COPY_OPTION_OVERWRITE_EXISTING = 1U << 0U, ///< Replace existing file
} ZixCopyOption;

/// Bitwise OR of ZixCopyOptions values
typedef uint32_t ZixCopyOptions;

/**
   Copy the file at path `src` to path `dst`.

   If supported by the system, a lightweight copy will be made to take
   advantage of copy-on-write support in the filesystem.  Otherwise, a simple
   deep copy will be made.

   @param allocator Allocator used for a memory block for copying if necessary.
   @param src Path to source file to copy.
   @param dst Path to destination file to create.
   @param options Options to control the kind of copy and error conditions.
   @return #ZIX_STATUS_SUCCESS if `dst` was successfully created, or an error.
*/
ZIX_API ZixStatus
zix_copy_file(ZixAllocator* ZIX_NULLABLE allocator,
              const char* ZIX_NONNULL    src,
              const char* ZIX_NONNULL    dst,
              ZixCopyOptions             options);

/**
   Create the directory `dir_path` with all available permissions.

   @return #ZIX_STATUS_SUCCESS if `dir_path` was successfully created, or an
   error.
*/
ZIX_API ZixStatus
zix_create_directory(const char* ZIX_NONNULL dir_path);

/**
   Create the directory `dir_path` with the permissions of another.

   This is like zix_create_directory(), but will copy the permissions from
   another directory.

   @return #ZIX_STATUS_SUCCESS if `dir_path` was successfully created, or an
   error.
*/
ZIX_API ZixStatus
zix_create_directory_like(const char* ZIX_NONNULL dir_path,
                          const char* ZIX_NONNULL existing_path);

/**
   Create the directory `dir_path` and any parent directories if necessary.

   @param allocator Allocator used for a temporary path buffer if necessary.

   @param dir_path The path to the deepest directory to create.

   @return #ZIX_STATUS_SUCCESS if all directories in `dir_path` were
   successfully created (or already existed), or an error.
*/
ZIX_API ZixStatus
zix_create_directories(ZixAllocator* ZIX_NULLABLE allocator,
                       const char* ZIX_NONNULL    dir_path);

/**
   Create a hard link at path `link` that points to path `target`.

   @return #ZIX_STATUS_SUCCESS, or an error.
*/
ZIX_API ZixStatus
zix_create_hard_link(const char* ZIX_NONNULL target_path,
                     const char* ZIX_NONNULL link_path);

/**
   Create a symbolic link at path `link` that points to path `target`.

   Note that portable code should use zix_create_directory_symlink() if the
   target is a directory, since this function won't work for that on some
   systems (like Windows).

   @return #ZIX_STATUS_SUCCESS, or an error.
*/
ZIX_API ZixStatus
zix_create_symlink(const char* ZIX_NONNULL target_path,
                   const char* ZIX_NONNULL link_path);

/**
   Create a symbolic link at path `link` that points to the directory `target`.

   This is a separate function from zix_create_symlink() because some systems
   (like Windows) require directory symlinks to be created specially.

   @return #ZIX_STATUS_SUCCESS, or an error.
*/
ZIX_API ZixStatus
zix_create_directory_symlink(const char* ZIX_NONNULL target_path,
                             const char* ZIX_NONNULL link_path);

/**
   Create a unique temporary directory at a given path pattern.

   The last six characters of `pattern` must be "XXXXXX" and will be replaced
   with unique characters in the result.

   @param allocator Allocator used for the returned path.

   @param path_pattern A path pattern ending in "XXXXXX".

   @return The path of the created directory, or null.
*/
ZIX_MALLOC_API char* ZIX_NULLABLE
zix_create_temporary_directory(ZixAllocator* ZIX_NULLABLE allocator,
                               const char* ZIX_NONNULL    path_pattern);

/// Remove the file or empty directory at `path`
ZIX_API ZixStatus
zix_remove(const char* ZIX_NONNULL path);

/**
   @}
   @defgroup zix_fs_access Access
   @{
*/

/**
   Function for reading input bytes from a stream.

   @param path Path to the directory being visited.
   @param name Name of the directory entry.
   @param data Opaque user data.
*/
typedef void (*ZixDirEntryVisitFunc)(const char* ZIX_NONNULL path,
                                     const char* ZIX_NONNULL name,
                                     void* ZIX_NONNULL       data);

/**
   Visit every file in the directory at `path`.

   @param path A path to a directory.

   @param data Opaque user data that is passed to `f`.

   @param f A function called on every entry in the directory.  The `path`
   parameter is always the directory path passed to this function, the `name`
   parameter is the name of the directory entry (not its full path).
*/
ZIX_API void
zix_dir_for_each(const char* ZIX_NONNULL          path,
                 void* ZIX_NULLABLE               data,
                 ZixDirEntryVisitFunc ZIX_NONNULL f);

/**
   Return whether the given paths point to files with identical contents.

   @param allocator Allocator used for a memory block for comparison if
   necessary.

   @param a_path Path to the first file to compare

   @param b_path Path to the second file to compare

   @return True if the two files have byte-for-byte identical contents.
*/
ZIX_API ZIX_NODISCARD bool
zix_file_equals(ZixAllocator* ZIX_NULLABLE allocator,
                const char* ZIX_NONNULL    a_path,
                const char* ZIX_NONNULL    b_path);

/**
   @}
   @defgroup zix_fs_resolution Resolution
   @{
*/

/**
   Return `path` as a canonical absolute path to a "real" file.

   This expands all symbolic links, relative references, and removes extra
   directory separators.

   Since this function may return null anyway, it accepts a null parameter to
   allow easier chaining of path functions when only the final result is
   required, for example:

   @code{.c}
   char* path      = zix_path_join(alloc, "/some/dir", "filename.txt");
   char* canonical = zix_canonical_path(path);
   if (canonical) {
     // Do something with the canonical path...
   } else {
     // No canonical path for some reason, we don't care which...
   }
   @endcode

   @return A new canonical version of `path`, or null if it doesn't exist.
*/
ZIX_MALLOC_API char* ZIX_NULLABLE
zix_canonical_path(ZixAllocator* ZIX_NULLABLE allocator,
                   const char* ZIX_NULLABLE   path);

/**
   @}
   @defgroup zix_fs_locking Locking
   @{
*/

/**
   A mode for locking files.

   The same mode should be used for the lock and the corresponding unlock.
*/
typedef enum {
  ZIX_FILE_LOCK_BLOCK, ///< Block until the operation succeeds
  ZIX_FILE_LOCK_TRY,   ///< Fail if the operation would block
} ZixFileLockMode;

/**
   Set an advisory exclusive lock on `file`.

   @param file Handle for open file to lock.
   @param mode Lock mode.
   @return #ZIX_STATUS_SUCCESS if the file was locked, or an error.
*/
ZIX_API ZixStatus
zix_file_lock(FILE* ZIX_NONNULL file, ZixFileLockMode mode);

/**
   Remove an advisory exclusive lock on `file`.

   @param file Handle for open file to lock.
   @param mode Lock mode.
   @return #ZIX_STATUS_SUCCESS if the file was unlocked, or an error.
*/
ZIX_API ZixStatus
zix_file_unlock(FILE* ZIX_NONNULL file, ZixFileLockMode mode);

/**
   @}
   @defgroup zix_fs_queries Queries
   @{
*/

/**
   An offset into a file or a file size in bytes.

   This is signed, and may be 64 bits even on 32-bit systems.
*/
#if defined(_FILE_OFFSET_BITS) && _FILE_OFFSET_BITS == 64
typedef int64_t ZixFileOffset;
#else
typedef ptrdiff_t ZixFileOffset;
#endif

/**
   A type of file.

   Note that not all types may be supported, and the system may support
   additional types not enumerated here.
*/
typedef enum {
  ZIX_FILE_TYPE_NONE,      ///< Non-existent file
  ZIX_FILE_TYPE_REGULAR,   ///< Regular file
  ZIX_FILE_TYPE_DIRECTORY, ///< Directory
  ZIX_FILE_TYPE_SYMLINK,   ///< Symbolic link
  ZIX_FILE_TYPE_BLOCK,     ///< Special block file
  ZIX_FILE_TYPE_CHARACTER, ///< Special character file
  ZIX_FILE_TYPE_FIFO,      ///< FIFO
  ZIX_FILE_TYPE_SOCKET,    ///< Socket
  ZIX_FILE_TYPE_UNKNOWN,   ///< Existing file with unknown type
} ZixFileType;

/**
   Return the type of a file or directory, resolving symlinks.
*/
ZIX_API ZixFileType
zix_file_type(const char* ZIX_NONNULL path);

/**
   Return the type of a file or directory or symlink.

   On Windows, a directory symlink (actually a "reparse point") always appears
   as a directory.
*/
ZIX_API ZixFileType
zix_symlink_type(const char* ZIX_NONNULL path);

/**
   Return the size of a file.

   Note that the returned value is signed and must be checked for errors.
   Non-negative values can be thought of as the "end" offset just past the last
   byte.

   @return A non-negative size in bytes, or -1 on error.
*/
ZIX_API ZixFileOffset
zix_file_size(const char* ZIX_NONNULL path);

/**
   @}
   @defgroup zix_fs_environment Environment
   @{
*/

/**
   Return the current working directory.

   @param allocator Allocator used for the returned path.
*/
ZIX_MALLOC_API char* ZIX_ALLOCATED
zix_current_path(ZixAllocator* ZIX_NULLABLE allocator);

/**
   Return the path to a directory suitable for making temporary files.

   @param allocator Allocator used for the returned path.

   @return A new path to a temporary directory, or null on error.
*/
ZIX_MALLOC_API char* ZIX_ALLOCATED
zix_temp_directory_path(ZixAllocator* ZIX_NULLABLE allocator);

/**
   @}
   @}
*/

ZIX_END_DECLS

#endif /* ZIX_FILESYSTEM_H */
