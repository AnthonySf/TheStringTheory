// Copyright 2014-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "errno_status.h"

#include <zix/attributes.h>
#include <zix/status.h>
#include <zix/warnings.h>

#include <errno.h>
#include <stddef.h>

#ifdef ENOTSUP
#  define N_ERRNO_MAPPINGS 14U
#else
#  define N_ERRNO_MAPPINGS 13U
#endif

typedef struct {
  int       code;
  ZixStatus status;
} ErrnoMapping;

static const ErrnoMapping errno_map[N_ERRNO_MAPPINGS] = {
  {0, ZIX_STATUS_SUCCESS},
  {EACCES, ZIX_STATUS_BAD_PERMS},
  {EAGAIN, ZIX_STATUS_UNAVAILABLE},
  {EEXIST, ZIX_STATUS_EXISTS},
  {EINVAL, ZIX_STATUS_BAD_ARG},
  {EMLINK, ZIX_STATUS_MAX_LINKS},
  {ENOENT, ZIX_STATUS_NOT_FOUND},
  {ENOMEM, ZIX_STATUS_NO_MEM},
  {ENOSPC, ZIX_STATUS_NO_SPACE},
  {ENOSYS, ZIX_STATUS_NOT_SUPPORTED},
  {EPERM, ZIX_STATUS_BAD_PERMS},
  {ETIMEDOUT, ZIX_STATUS_TIMEOUT},
#ifdef ENOTSUP
  {ENOTSUP, ZIX_STATUS_NOT_SUPPORTED},
#endif
  {0, ZIX_STATUS_ERROR}, // Fallback mapping
};

ZIX_REALTIME ZixStatus
zix_errno_status_if(const int r)
{
  ZIX_DISABLE_EFFECT_WARNINGS // errno is sometimes a sneaky function call

  return r ? zix_errno_status(errno) : ZIX_STATUS_SUCCESS;

  ZIX_RESTORE_WARNINGS
}

ZIX_REALTIME ZixStatus
zix_errno_status(const int e)
{
  // Find the index of the matching mapping (or leave it at the fallback entry)
  size_t m = 0;
  while (m < (N_ERRNO_MAPPINGS - 1) && errno_map[m].code != e) {
    ++m;
  }

  return errno_map[m].status;
}
