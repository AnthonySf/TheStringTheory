// Copyright 2020-2024 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "dylib.h"

#ifdef _WIN32

#  include <stdlib.h>
#  include <windows.h>

void*
dylib_open(const char* const filename, const unsigned flags)
{
  (void)flags;

  const int wide_length =
    MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, filename, -1, NULL, 0);

  if (wide_length > 1) {
    wchar_t* const wide_filename =
      (wchar_t*)calloc((size_t)wide_length, sizeof(wchar_t));

    if (wide_filename) {
      MultiByteToWideChar(
        CP_UTF8, MB_ERR_INVALID_CHARS, filename, -1, wide_filename, wide_length);

      HMODULE const handle =
        LoadLibraryExW(wide_filename, NULL, LOAD_WITH_ALTERED_SEARCH_PATH);

      free(wide_filename);
      return handle;
    }
  }

  return LoadLibraryExA(filename, NULL, LOAD_WITH_ALTERED_SEARCH_PATH);
}

int
dylib_close(DylibLib* const handle)
{
  return !FreeLibrary((HMODULE)handle);
}

const char*
dylib_error(void)
{
  return "Unknown error";
}

DylibFunc
dylib_func(DylibLib* handle, const char* symbol)
{
  return (DylibFunc)GetProcAddress((HMODULE)handle, symbol);
}

#else

#  include <dlfcn.h>

void*
dylib_open(const char* const filename, const unsigned flags)
{
  return dlopen(filename, flags == DYLIB_LAZY ? RTLD_LAZY : RTLD_NOW);
}

int
dylib_close(DylibLib* const handle)
{
  return dlclose(handle);
}

const char*
dylib_error(void)
{
  return dlerror();
}

DylibFunc
dylib_func(DylibLib* handle, const char* symbol)
{
  typedef DylibFunc (*VoidFuncGetter)(void*, const char*);

  VoidFuncGetter dlfunc = (VoidFuncGetter)dlsym;

  return dlfunc(handle, symbol);
}

#endif
