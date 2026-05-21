// Copyright 2019-2024 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_WIN32_UTIL_H
#define ZIX_WIN32_UTIL_H

#include <zix/allocator.h>

#ifdef UNICODE
typedef wchar_t ArgPathChar;
#else
typedef const char ArgPathChar;
#endif

/// Copy and convert a path argument if necessary
ArgPathChar*
arg_path_new(ZixAllocator* allocator, const char* path);

/// Free a path from arg_path_new() if necessary
void
arg_path_free(ZixAllocator* allocator, ArgPathChar* path);

/// Convert from (user) UTF-8 to (Windows) UTF-16
wchar_t*
zix_utf8_to_wchar(ZixAllocator* allocator, const char* utf8);

/// Convert from (Windows) UTF-16 to (user) UTF-8
char*
zix_wchar_to_utf8(ZixAllocator* allocator, const wchar_t* wstr);

#endif // ZIX_WIN32_UTIL_H
