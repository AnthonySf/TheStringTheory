// Copyright 2022-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_ERRNO_STATUS_H
#define ZIX_ERRNO_STATUS_H

#include <zix/attributes.h>
#include <zix/status.h>

/// Return an errno value converted to a status code
ZIX_CONST_FUNC ZIX_REALTIME ZixStatus
zix_errno_status(int e);

/// Return success if `r` is non-zero, or `errno` as a status code otherwise
ZIX_PURE_FUNC ZIX_REALTIME ZixStatus
zix_errno_status_if(int r);

#endif // ZIX_ERRNO_STATUS_H
