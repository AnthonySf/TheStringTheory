// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "../system.h"
#include "../zix_config.h"

#include <fcntl.h>
#include <stddef.h>
#include <stdint.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <unistd.h>

#ifndef MAX
#  define MAX(a, b) (((a) > (b)) ? (a) : (b))
#endif

#ifdef PAGE_SIZE
#  define ZIX_DEFAULT_PAGE_SIZE PAGE_SIZE
#else
#  define ZIX_DEFAULT_PAGE_SIZE 4096U
#endif

uint32_t
zix_system_page_size(void)
{
#if USE_SYSCONF
  const long r = sysconf(_SC_PAGE_SIZE);
  return r > 0L ? (uint32_t)r : ZIX_DEFAULT_PAGE_SIZE;
#else
  return (uint32_t)ZIX_DEFAULT_PAGE_SIZE;
#endif
}

uint32_t
zix_system_max_block_size(const struct stat* const s1,
                          const struct stat* const s2,
                          const uint32_t           fallback)
{
  const blksize_t size = MAX(s1->st_blksize, s2->st_blksize);

  return (size > 0) ? (uint32_t)size : fallback;
}

int
zix_system_open(const char* const path, const int flags, const mode_t mode)
{
#ifdef O_CLOEXEC
  return open(path, flags | O_CLOEXEC, mode); // NOLINT(hicpp-signed-bitwise)
#else
  return open(path, flags, mode);
#endif
}

int
zix_system_close(const int fd)
{
  return close(fd);
}

ssize_t
zix_system_read(const int fd, void* const buf, const size_t count)
{
  return read(fd, buf, count);
}
