// Copyright 2014-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/attributes.h>
#include <zix/status.h>

ZIX_REALTIME const char*
zix_strerror(const ZixStatus status)
{
  switch (status) {
  case ZIX_STATUS_SUCCESS:
    return "Success";
  case ZIX_STATUS_ERROR:
    return "Unknown error";
  case ZIX_STATUS_NO_MEM:
    return "Out of memory";
  case ZIX_STATUS_NOT_FOUND:
    return "Not found";
  case ZIX_STATUS_EXISTS:
    return "Exists";
  case ZIX_STATUS_BAD_ARG:
    return "Bad argument";
  case ZIX_STATUS_BAD_PERMS:
    return "Bad permissions";
  case ZIX_STATUS_REACHED_END:
    return "Reached end";
  case ZIX_STATUS_TIMEOUT:
    return "Timeout";
  case ZIX_STATUS_OVERFLOW:
    return "Overflow";
  case ZIX_STATUS_NOT_SUPPORTED:
    return "Not supported";
  case ZIX_STATUS_UNAVAILABLE:
    return "Resource unavailable";
  case ZIX_STATUS_NO_SPACE:
    return "Out of storage space";
  case ZIX_STATUS_MAX_LINKS:
    return "Too many links";
  }
  return "Unknown error";
}
