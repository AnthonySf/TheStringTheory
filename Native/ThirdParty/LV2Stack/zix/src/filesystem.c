// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/filesystem.h>

#include "path_iter.h"
#include "system.h"

#include <zix/allocator.h>
#include <zix/status.h>

#include <fcntl.h>
#include <sys/stat.h>
#include <sys/types.h>

#include <errno.h>
#include <stdbool.h>
#include <string.h>

ZixStatus
zix_create_directories(ZixAllocator* const allocator,
                       const char* const   dir_path)
{
  if (!dir_path[0]) {
    return ZIX_STATUS_BAD_ARG;
  }

  // Allocate a working copy of the path to chop along the way
  const size_t path_len = strlen(dir_path);
  char* const  path     = (char*)zix_malloc(allocator, path_len + 1U);
  if (!path) {
    return ZIX_STATUS_NO_MEM;
  }

  // Copy directory path as prefix
  memcpy(path, dir_path, path_len + 1U);

  // Start at the root directory (past any name)
  ZixPathIter p = zix_path_begin(path);
  while (p.state < ZIX_PATH_FILE_NAME) {
    p = zix_path_next(path, p);
  }

  // Create each directory down the path
  ZixStatus st = ZIX_STATUS_SUCCESS;
  while (p.state != ZIX_PATH_END) {
    char* const end      = &path[p.range.end];
    const char  old_last = *end;

    *end = '\0';
    if (zix_file_type(path) != ZIX_FILE_TYPE_DIRECTORY) {
      if ((st = zix_create_directory(path))) {
        break;
      }
    }

    *end = old_last;
    p    = zix_path_next(path, p);
  }

  zix_free(allocator, path);
  return st;
}

ZixFileOffset
zix_file_size(const char* const path)
{
  struct stat sb;
  return stat(path, &sb) ? (off_t)0 : sb.st_size;
}

// Wrapper for read() that transparently deals with short reads
static ssize_t
full_read(const int fd, void* const buf, const size_t count)
{
  size_t  n = 0;
  ssize_t r = 0;

  while (n < count &&
         (r = zix_system_read(fd, (char*)buf + n, count - n)) > 0) {
    n += (size_t)r;
  }

  return (ssize_t)n;
}

bool
zix_file_equals(ZixAllocator* const allocator,
                const char* const   path_a,
                const char* const   path_b)
{
  if (!strcmp(path_a, path_b)) {
    return true; // Paths match
  }

  errno = 0;

  // Open files and get file information
  const int   fd_a = zix_system_open(path_a, O_RDONLY, 0);
  const int   fd_b = zix_system_open(path_b, O_RDONLY, 0);
  struct stat stat_a;
  struct stat stat_b;
  if (fd_a < 0 || fd_b < 0 || fstat(fd_a, &stat_a) || fstat(fd_b, &stat_b)) {
    zix_system_close_fds(fd_b, fd_a);
    return false;
  }

  bool match = false;
  if (stat_a.st_dev == stat_b.st_dev && stat_a.st_ino &&
      stat_a.st_ino == stat_b.st_ino) {
    match = true; // Fast path: paths refer to the same file
  } else if (stat_a.st_size == stat_b.st_size) {
    // Slow path: files have equal size, compare contents

    // Allocate two blocks in a single buffer (to simplify error handling)
    const uint32_t align  = zix_system_page_size();
    const uint32_t size   = zix_system_max_block_size(&stat_a, &stat_b, align);
    BlockBuffer    blocks = zix_system_new_block(allocator, align, 2U * size);
    void* const    data   = blocks.buffer ? blocks.buffer : blocks.fallback;

    // Compare files a block at a time
    const uint32_t block_size = blocks.size / 2U;
    void* const    block_a    = data;
    void* const    block_b    = (void*)((char*)data + block_size);
    match                     = true;
    for (ssize_t n = 0; n < stat_a.st_size && match;) {
      const ssize_t r = zix_system_read(fd_a, block_a, block_size);
      if (r <= 0 || full_read(fd_b, block_b, (uint32_t)r) != r ||
          !!memcmp(block_a, block_b, (size_t)r)) {
        match = false;
      }
      n += r;
    }

    zix_system_free_block(allocator, blocks);
  }

  return !zix_system_close_fds(fd_b, fd_a) && match;
}
