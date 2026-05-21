// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/filesystem.h>

#include "../errno_status.h"
#include "../qualifiers.h"
#include "../system.h"
#include "../zix_config.h"

#include <zix/allocator.h>
#include <zix/attributes.h>
#include <zix/status.h>

#if USE_FLOCK && USE_FILENO
#  include <sys/file.h>
#endif

#if USE_CLONEFILE
#  include <sys/attr.h>
#  include <sys/clonefile.h>
#endif

#if USE_REALPATH
#  include <limits.h>
#endif

#include <dirent.h>
#include <fcntl.h>
#include <stdint.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <unistd.h>

#include <errno.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static inline ZixStatus
zix_posix_status(const int rc)
{
  return rc ? zix_errno_status(errno) : ZIX_STATUS_SUCCESS;
}

static ZixStatus
finish_copy(const int dst_fd, const int src_fd, const ZixStatus status)
{
#ifdef __APPLE__
  const int rc = dst_fd >= 0 ? fcntl(dst_fd, F_FULLFSYNC) : 0;
#else
  const int rc = dst_fd >= 0 ? fdatasync(dst_fd) : 0;
#endif

  const ZixStatus st0 = zix_posix_status(rc);
  const ZixStatus st1 = zix_system_close_fds(dst_fd, src_fd);

  return status ? status : st0 ? st0 : st1;
}

static char*
copy_path(ZixAllocator* const allocator,
          const char* const   path,
          const size_t        length)
{
  char* result = NULL;

  if (path) {
    if ((result = (char*)zix_calloc(allocator, length + 1U, 1U))) {
      memcpy(result, path, length + 1U);
    }
  }

  return result;
}

#if !defined(PATH_MAX) && USE_PATHCONF

static size_t
max_path_size(const char* const path)
{
  const long path_max = pathconf(path, _PC_PATH_MAX);
  return (path_max > 0) ? (size_t)path_max : zix_system_page_size();
}

#elif !defined(PATH_MAX)

static size_t
max_path_size(const char* const path)
{
  (void)path;

  return zix_system_page_size();
}

#endif

#if USE_CLONEFILE

static ZixStatus
zix_clonefile(const char* const    src,
              const char* const    dst,
              const ZixCopyOptions options)
{
  errno = 0;

  ZixStatus         st        = ZIX_STATUS_SUCCESS;
  const ZixFileType dst_type  = zix_file_type(dst);
  const bool        overwrite = (options == ZIX_COPY_OPTION_OVERWRITE_EXISTING);
  if (overwrite && dst_type == ZIX_FILE_TYPE_REGULAR) {
    st = zix_remove(dst);
  } else if (dst_type != ZIX_FILE_TYPE_NONE) {
    st = ZIX_STATUS_NOT_SUPPORTED;
  }

  return st ? st : zix_posix_status(clonefile(src, dst, 0));
}

#endif

#if USE_COPY_FILE_RANGE

static ZixStatus
zix_copy_file_range(const int src_fd, const int dst_fd, const size_t size)
{
  errno = 0;

  size_t  remaining = size;
  ssize_t r         = 0;
  while (remaining > 0 &&
         (r = copy_file_range(src_fd, NULL, dst_fd, NULL, remaining, 0U)) > 0) {
    remaining -= (size_t)r;
  }

  return (r >= 0) ? ZIX_STATUS_SUCCESS
                  : zix_errno_status(
                      (errno == EXDEV || errno == EINVAL) ? ENOSYS : errno);
}

#endif

static ZixStatus
copy_blocks(const int    src_fd,
            const int    dst_fd,
            void* const  block,
            const size_t block_size)
{
  ZixStatus st     = ZIX_STATUS_SUCCESS;
  ssize_t   n_read = 0;

  while (!st && (n_read = read(src_fd, block, block_size)) > 0) {
    if (write(dst_fd, block, (size_t)n_read) != n_read) {
      st = zix_errno_status(errno);
    }
  }

  return st;
}

ZixStatus
zix_copy_file(ZixAllocator* const  allocator,
              const char* const    src,
              const char* const    dst,
              const ZixCopyOptions options)
{
  ZixStatus st = ZIX_STATUS_SUCCESS;

  errno = 0;

#if USE_CLONEFILE
  // Try to copy via the kernel on MacOS to take advantage of CoW
  st = zix_clonefile(src, dst, options);
  if (st != ZIX_STATUS_NOT_SUPPORTED) {
    return st;
  }
#endif

  // Open source file and get its status
  const int   src_fd = zix_system_open(src, O_RDONLY, 0);
  struct stat src_stat;
  if (src_fd < 0 || fstat(src_fd, &src_stat)) {
    return finish_copy(-1, src_fd, zix_errno_status(errno));
  }

  // Fail if the source is not a regular file (since we need a size)
  if (!S_ISREG(src_stat.st_mode)) {
    return finish_copy(-1, src_fd, ZIX_STATUS_BAD_ARG);
  }

  // Open a new destination file
  const bool  overwrite = (options == ZIX_COPY_OPTION_OVERWRITE_EXISTING);
  const int   dst_flags = O_WRONLY | O_CREAT | (overwrite ? O_TRUNC : O_EXCL);
  const int   dst_fd    = zix_system_open(dst, dst_flags, 0644);
  struct stat dst_stat;
  if (dst_fd < 0 || fstat(dst_fd, &dst_stat)) {
    return finish_copy(dst_fd, src_fd, zix_errno_status(errno));
  }

#if USE_COPY_FILE_RANGE
  // Try to copy via the kernel on Linux/BSD to take advantage of CoW
  st = zix_copy_file_range(src_fd, dst_fd, (size_t)src_stat.st_size);
  if (st != ZIX_STATUS_NOT_SUPPORTED) {
    return finish_copy(dst_fd, src_fd, st);
  }
#endif

  // Set sequential hints so the kernel can optimize the page cache
#if USE_POSIX_FADVISE
  (void)posix_fadvise(src_fd, 0, src_stat.st_size, POSIX_FADV_SEQUENTIAL);
  (void)posix_fadvise(dst_fd, 0, src_stat.st_size, POSIX_FADV_SEQUENTIAL);
#endif

  errno = 0;

  // Allocate a block and copy file content one block at a time
  const uint32_t align = zix_system_page_size();
  const uint32_t size  = zix_system_max_block_size(&src_stat, &dst_stat, align);
  BlockBuffer    block = zix_system_new_block(allocator, align, size);
  void* const    data  = block.buffer ? block.buffer : block.fallback;
  st                   = copy_blocks(src_fd, dst_fd, data, block.size);

  zix_system_free_block(allocator, block);
  return finish_copy(dst_fd, src_fd, st);
}

ZixStatus
zix_create_directory(const char* const dir_path)
{
  return (!dir_path[0]) ? ZIX_STATUS_BAD_ARG
                        : zix_posix_status(mkdir(dir_path, 0777));
}

ZixStatus
zix_create_directory_like(const char* const dir_path,
                          const char* const existing_path)
{
  struct stat sb;
  return !dir_path[0] ? ZIX_STATUS_BAD_ARG
         : stat(existing_path, &sb)
           ? zix_errno_status(errno)
           : zix_posix_status(mkdir(dir_path, sb.st_mode));
}

ZixStatus
zix_create_hard_link(const char* const target_path, const char* const link_path)
{
  return zix_posix_status(link(target_path, link_path));
}

ZixStatus
zix_create_symlink(const char* const target_path, const char* const link_path)
{
  return zix_posix_status(symlink(target_path, link_path));
}

ZixStatus
zix_create_directory_symlink(const char* const target_path,
                             const char* const link_path)
{
  return zix_create_symlink(target_path, link_path);
}

char*
zix_create_temporary_directory(ZixAllocator* const allocator,
                               const char* const   path_pattern)
{
  const size_t length = strlen(path_pattern);
  char* const  result = (char*)zix_calloc(allocator, length + 1U, 1U);
  if (result) {
    memcpy(result, path_pattern, length + 1U);
    if (!mkdtemp(result)) {
      zix_free(allocator, result);
      return NULL;
    }
  }

  return result;
}

ZixStatus
zix_remove(const char* const path)
{
  return zix_posix_status(remove(path));
}

void
zix_dir_for_each(const char* const          path,
                 void* const                data,
                 const ZixDirEntryVisitFunc f)
{
  DIR* dir = opendir(path);
  if (dir) {
    // NOLINTNEXTLINE(concurrency-mt-unsafe)
    for (struct dirent* entry = NULL; (entry = readdir(dir));) {
      if (!!strcmp(entry->d_name, ".") && !!strcmp(entry->d_name, "..")) {
        f(path, entry->d_name, data);
      }
    }
    closedir(dir);
  }
}

char*
zix_canonical_path(ZixAllocator* const allocator, const char* const path)
{
  if (!path) {
    return NULL;
  }

#if USE_REALPATH && defined(PATH_MAX)
  // Some POSIX systems have a static PATH_MAX so we can resolve on the stack
  char        buffer[PATH_MAX] = {0};
  char* const canonical        = realpath(path, buffer);
  if (canonical) {
    return copy_path(allocator, buffer, strlen(buffer));
  }

#elif USE_REALPATH && USE_PATHCONF
  // Others don't so we have to query PATH_MAX at runtime to allocate the result
  const size_t size      = max_path_size(path);
  char* const  buffer    = (char*)zix_calloc(allocator, size, 1);
  char* const  canonical = realpath(path, buffer);
  if (canonical) {
    return canonical;
  }

  zix_free(allocator, buffer);

#else
  (void)allocator;
#endif

  return NULL;
}

ZixStatus
zix_file_lock(FILE* const file, const ZixFileLockMode mode)
{
#if USE_FLOCK && USE_FILENO
  return zix_posix_status(
    flock(fileno(file),
          (mode == ZIX_FILE_LOCK_BLOCK) ? LOCK_EX : (LOCK_EX | LOCK_NB)));

#else
  (void)file;
  (void)mode;
  return ZIX_STATUS_NOT_SUPPORTED;
#endif
}

ZixStatus
zix_file_unlock(FILE* const file, const ZixFileLockMode mode)
{
#if USE_FLOCK && USE_FILENO
  return zix_posix_status(
    flock(fileno(file),
          (mode == ZIX_FILE_LOCK_BLOCK) ? LOCK_UN : (LOCK_UN | LOCK_NB)));

#else
  (void)file;
  (void)mode;
  return ZIX_STATUS_NOT_SUPPORTED;
#endif
}

ZIX_CONST_FUNC static ZixFileType
stat_file_type(const struct stat* sb)
{
  typedef struct {
    unsigned    mask;
    ZixFileType type;
  } Mapping;

  ZIX_CONSTEXPR Mapping map[] = {
    {S_IFREG, ZIX_FILE_TYPE_REGULAR},
    {S_IFDIR, ZIX_FILE_TYPE_DIRECTORY},
    {S_IFLNK, ZIX_FILE_TYPE_SYMLINK},
    {S_IFBLK, ZIX_FILE_TYPE_BLOCK},
    {S_IFCHR, ZIX_FILE_TYPE_CHARACTER},
    {S_IFIFO, ZIX_FILE_TYPE_FIFO},
    {S_IFSOCK, ZIX_FILE_TYPE_SOCKET},
    {0U, ZIX_FILE_TYPE_UNKNOWN},
  };

  const unsigned mask = (unsigned)sb->st_mode & (unsigned)S_IFMT;
  unsigned       m    = 0U;
  while (map[m].mask && map[m].mask != mask) {
    ++m;
  }

  return map[m].type;
}

ZixFileType
zix_file_type(const char* const path)
{
  struct stat sb;
  return stat(path, &sb) ? ZIX_FILE_TYPE_NONE : stat_file_type(&sb);
}

ZixFileType
zix_symlink_type(const char* const path)
{
  struct stat sb;
  return lstat(path, &sb) ? ZIX_FILE_TYPE_NONE : stat_file_type(&sb);
}

char*
zix_temp_directory_path(ZixAllocator* const allocator)
{
  const char* tmpdir = getenv("TMPDIR"); // NOLINT(concurrency-mt-unsafe)

  tmpdir = tmpdir ? tmpdir : "/tmp";

  return copy_path(allocator, tmpdir, strlen(tmpdir));
}

char*
zix_current_path(ZixAllocator* const allocator)
{
#ifdef PATH_MAX
  // Some POSIX systems have a static PATH_MAX so we can store it on the stack
  char        buffer[PATH_MAX] = {0};
  char* const cwd              = getcwd(buffer, PATH_MAX);
  return cwd ? copy_path(allocator, cwd, strlen(cwd)) : NULL;

#elif USE_PATHCONF
  // Others don't so we have to query PATH_MAX at runtime to allocate the result
  const size_t size    = max_path_size(".");
  char* const  buffer  = (char*)zix_calloc(allocator, size, 1);
  char* const  current = getcwd(buffer, size);
  if (!current) {
    zix_free(allocator, buffer);
  }

  return current;

#else
  (void)allocator;
  return NULL;

#endif
}
