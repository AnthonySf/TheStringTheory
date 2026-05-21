// Copyright 2022-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_SYSTEM_H
#define ZIX_SYSTEM_H

#include <zix/allocator.h>
#include <zix/attributes.h>
#include <zix/status.h>

#include <stddef.h>
#include <stdint.h>
#include <sys/stat.h> // IWYU pragma: keep
#include <sys/types.h>

#ifndef ZIX_STACK_BLOCK_SIZE
#  define ZIX_STACK_BLOCK_SIZE 512U
#endif

#if defined(_WIN32) && !defined(__GNUC__)
typedef long ssize_t;
typedef int  mode_t;
#endif

typedef struct {
  uint32_t           size;                           ///< Buffer size
  void* ZIX_NULLABLE buffer;                         ///< Allocated buffer
  char               fallback[ZIX_STACK_BLOCK_SIZE]; ///< Stack fallback
} BlockBuffer;

uint32_t
zix_system_page_size(void);

int
zix_system_open(const char* ZIX_NONNULL path, int flags, mode_t mode);

int
zix_system_close(int fd);

ZixStatus
zix_system_close_fds(int fd1, int fd2);

ssize_t
zix_system_read(int fd, void* ZIX_NONNULL buf, size_t count);

ZIX_PURE_FUNC uint32_t
zix_system_max_block_size(const struct stat* ZIX_NONNULL s1,
                          const struct stat* ZIX_NONNULL s2,
                          uint32_t                       fallback);

BlockBuffer
zix_system_new_block(ZixAllocator* ZIX_NULLABLE allocator,
                     uint32_t                   align,
                     uint32_t                   size);

void
zix_system_free_block(ZixAllocator* ZIX_NULLABLE allocator, BlockBuffer block);

#endif // ZIX_SYSTEM_H
