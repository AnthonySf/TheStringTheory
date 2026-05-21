// Copyright 2012-2024 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "win32_util.h"

#include <zix/environment.h>

#include <windows.h>

char*
zix_expand_environment_strings(ZixAllocator* const allocator,
                               const char* const   string)
{
  ArgPathChar* const wstring = arg_path_new(allocator, string);

  const DWORD size = ExpandEnvironmentStrings(wstring, NULL, 0U);
  if (!size) {
    arg_path_free(allocator, wstring);
    return NULL;
  }

  TCHAR* const out =
    (TCHAR*)zix_calloc(allocator, (size_t)size + 1U, sizeof(TCHAR));
  if (out) {
    ExpandEnvironmentStrings(wstring, out, size + 1U);
  }

  arg_path_free(allocator, wstring);

#ifdef UNICODE
  char* const result = zix_wchar_to_utf8(allocator, out);
  zix_free(allocator, out);
  return result;
#else
  return out;
#endif
}
