// Copyright 2019-2024 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "win32_util.h"

#include <limits.h>
#include <windows.h>

#ifdef UNICODE

ArgPathChar*
arg_path_new(ZixAllocator* const allocator, const char* const path)
{
  return zix_utf8_to_wchar(allocator, path);
}

void
arg_path_free(ZixAllocator* const allocator, ArgPathChar* const path)
{
  zix_free(allocator, path);
}

#else // !defined(UNICODE)

ArgPathChar*
arg_path_new(ZixAllocator* const allocator, const char* const path)
{
  (void)allocator;
  return path;
}

void
arg_path_free(ZixAllocator* const allocator, ArgPathChar* const path)
{
  (void)allocator;
  (void)path;
}

#endif

wchar_t*
zix_utf8_to_wchar(ZixAllocator* const allocator, const char* const utf8)
{
  const int rc = utf8 ? MultiByteToWideChar(CP_UTF8, 0, utf8, -1, NULL, 0) : 0;
  if (rc <= 0 || rc == INT_MAX) {
    return NULL;
  }

  wchar_t* const result =
    (wchar_t*)zix_calloc(allocator, (size_t)rc, sizeof(wchar_t));
  if (result) {
    MultiByteToWideChar(CP_UTF8, 0, utf8, -1, result, rc);
  }

  return result;
}

char*
zix_wchar_to_utf8(ZixAllocator* const allocator, const wchar_t* const wstr)
{
  const int rc =
    wstr ? WideCharToMultiByte(CP_UTF8, 0, wstr, -1, NULL, 0, NULL, NULL) : 0;
  if (rc <= 0 || rc == INT_MAX) {
    return NULL;
  }

  char* const result = (char*)zix_calloc(allocator, (size_t)rc, sizeof(char));
  if (result) {
    WideCharToMultiByte(CP_UTF8, 0, wstr, -1, result, rc, NULL, NULL);
  }

  return result;
}
