// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "../system.h"
#include "../zix_config.h"

#include <fcntl.h>
#include <io.h>
#include <share.h>
#include <windows.h>

#include <limits.h>
#include <stdint.h>

uint32_t
zix_system_page_size(void)
{
  SYSTEM_INFO info;
  GetSystemInfo(&info);

  return (info.dwPageSize > 0 && info.dwPageSize < UINT32_MAX)
           ? (uint32_t)info.dwPageSize
           : 512U;
}

uint32_t
zix_system_max_block_size(const struct stat* const s1,
                          const struct stat* const s2,
                          const uint32_t           fallback)
{
  (void)s1;
  (void)s2;

  /* Windows doesn't provide st_blksize, so to implement this properly, we'd
     need to do something like get the corresponding device handle from the
     files and use DeviceIoControl to query it. This is pretty tedious, and
     shouldn't affect performance very much (if at all) in most cases, so just
     use the fallback for now. */

  return fallback;
}

int
zix_system_open(const char* const path, const int flags, const mode_t mode)
{
  const int oflag = flags | O_BINARY; //  NOLINT(hicpp-signed-bitwise)

#if USE_SOPEN_S
  int           fd  = 0;
  const errno_t err = _sopen_s(&fd, path, oflag, _SH_DENYNO, mode);
  return err ? -1 : fd;
#else
  return _open(path, oflag, mode);
#endif
}

int
zix_system_close(const int fd)
{
  return _close(fd);
}

ssize_t
zix_system_read(const int fd, void* const buf, const size_t count)
{
  return _read(fd, buf, (unsigned)count);
}
