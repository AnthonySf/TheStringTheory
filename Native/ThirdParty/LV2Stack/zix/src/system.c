// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "system.h"

#include "errno_status.h"

#include <zix/allocator.h>
#include <zix/status.h>

#include <stddef.h>

ZixStatus
zix_system_close_fds(const int fd1, const int fd2)
{
  // Careful: we need to always close both files, but catch errno at any point

  const int       r1  = fd1 >= 0 ? zix_system_close(fd1) : 0;
  const ZixStatus st1 = zix_errno_status_if(r1);
  const int       r2  = fd2 >= 0 ? zix_system_close(fd2) : 0;
  const ZixStatus st2 = zix_errno_status_if(r2);

  return st1 ? st1 : st2;
}

BlockBuffer
zix_system_new_block(ZixAllocator* const allocator,
                     const uint32_t      align,
                     const uint32_t      size)
{
  BlockBuffer block = {size, NULL, {'\0'}};

  if (!(block.buffer = zix_aligned_alloc(allocator, align, size))) {
    block.size = ZIX_STACK_BLOCK_SIZE;
  }

  return block;
}

void
zix_system_free_block(ZixAllocator* const allocator, const BlockBuffer block)
{
  zix_aligned_free(allocator, block.buffer);
}
