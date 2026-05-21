// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/filesystem.h>

#include "../errno_status.h"
#include "../qualifiers.h"
#include "../zix_config.h"
#include "win32_util.h"

#include <zix/allocator.h>
#include <zix/path.h>
#include <zix/status.h>

#include <direct.h>
#include <fcntl.h>
#include <io.h>
#include <limits.h>
#include <sys/stat.h>
#include <tchar.h>
#include <windows.h>

#include <errno.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef UNICODE

static char*
path_result(ZixAllocator* const allocator, wchar_t* const path)
{
  if (!path) {
    return NULL;
  }

  ZIX_CONSTEXPR wchar_t* const long_prefix = L"\\\\?\\";

  const size_t p      = !wcsncmp(path, long_prefix, 4U) ? 4U : 0U;
  char* const  result = zix_wchar_to_utf8(allocator, path + p);
  zix_free(allocator, path);
  return result;
}

#else // !defined(UNICODE)

static char*
path_result(ZixAllocator* const allocator, char* const path)
{
  (void)allocator;
  return path;
}

#endif

static inline ZixStatus
zix_winerror_status(const DWORD e)
{
  switch (e) {
  case ERROR_NOT_ENOUGH_MEMORY:
  case ERROR_OUTOFMEMORY:
    return ZIX_STATUS_NO_MEM;
  case ERROR_SEM_TIMEOUT:
    return ZIX_STATUS_TIMEOUT;
  case ERROR_FILE_NOT_FOUND:
  case ERROR_PATH_NOT_FOUND:
    return ZIX_STATUS_NOT_FOUND;
  case ERROR_BUFFER_OVERFLOW:
    return ZIX_STATUS_OVERFLOW;
  case ERROR_DISK_FULL:
    return ZIX_STATUS_NO_SPACE;
  case ERROR_ALREADY_EXISTS:
  case ERROR_FILE_EXISTS:
    return ZIX_STATUS_EXISTS;
  case ERROR_PRIVILEGE_NOT_HELD:
    return ZIX_STATUS_BAD_PERMS;
  case ERROR_LOCK_VIOLATION:
    return ZIX_STATUS_UNAVAILABLE;
  }

  return ZIX_STATUS_ERROR;
}

static inline ZixStatus
zix_windows_status(const bool success)
{
  return success ? ZIX_STATUS_SUCCESS : zix_winerror_status(GetLastError());
}

ZixStatus
zix_copy_file(ZixAllocator* const  allocator,
              const char* const    src,
              const char* const    dst,
              const ZixCopyOptions options)
{
  ArgPathChar* const wsrc = arg_path_new(allocator, src);
  ArgPathChar* const wdst = arg_path_new(allocator, dst);

  const BOOL ret =
    CopyFile(wsrc, wdst, !(options & ZIX_COPY_OPTION_OVERWRITE_EXISTING));

  arg_path_free(allocator, wdst);
  arg_path_free(allocator, wsrc);
  return zix_windows_status(ret);
}

/// Linear Congruential Generator for making random 32-bit integers
static inline uint32_t
lcg32(const uint32_t i)
{
  ZIX_CONSTEXPR uint32_t a = 134775813U;
  ZIX_CONSTEXPR uint32_t c = 1U;

  return (a * i) + c;
}

char*
zix_create_temporary_directory(ZixAllocator* const allocator,
                               const char* const   path_pattern)
{
  ZIX_CONSTEXPR char   chars[] = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
  ZIX_CONSTEXPR size_t n_chars = sizeof(chars) - 1;

  // Ensure that the pattern ends with "XXXXXX"
  const size_t length = strlen(path_pattern);
  if (length < 7 || !!strcmp(path_pattern + length - 6, "XXXXXX")) {
    errno = EINVAL;
    return NULL;
  }

  // Allocate a result to manipulate as we search for paths
  char* const result = (char*)zix_calloc(allocator, length + 1U, 1U);
  if (!result) {
    return NULL;
  }

  // Repeatedly try creating a directory with random suffixes
  memcpy(result, path_pattern, length + 1U);
  char* const suffix = result + length - 6U;
  uint32_t    seed   = GetCurrentProcessId();
  for (unsigned attempt = 0U; attempt < 128U; ++attempt) {
    for (unsigned i = 0U; i < 6U; ++i) {
      seed      = lcg32(seed);
      suffix[i] = chars[seed % n_chars];
    }

    if (!zix_create_directory(result)) {
      return result;
    }
  }

  zix_free(allocator, result);
  return NULL;
}

ZixStatus
zix_remove(const char* const path)
{
  ArgPathChar* const wpath = arg_path_new(NULL, path);
  const DWORD        attrs = GetFileAttributes(wpath);

  const BOOL success =
    ((attrs & FILE_ATTRIBUTE_DIRECTORY) ? RemoveDirectory(wpath)
                                        : DeleteFile(wpath));

  arg_path_free(NULL, wpath);
  return zix_windows_status(success);
}

void
zix_dir_for_each(const char* const          path,
                 void* const                data,
                 const ZixDirEntryVisitFunc f)
{
  ZIX_CONSTEXPR TCHAR* const dot    = TEXT(".");
  ZIX_CONSTEXPR TCHAR* const dotdot = TEXT("..");

#ifdef UNICODE
  const int path_size = MultiByteToWideChar(CP_UTF8, 0, path, -1, NULL, 0);
  if (path_size < 1) {
    return;
  }

  const size_t path_len = (size_t)path_size - 1U;
  TCHAR* const pat = (TCHAR*)zix_calloc(NULL, path_len + 4U, sizeof(TCHAR));
  if (!pat) {
    return;
  }

  MultiByteToWideChar(CP_UTF8, 0, path, -1, pat, path_size);
#else
  const size_t path_len = strlen(path);
  TCHAR* const pat = (TCHAR*)zix_calloc(NULL, path_len + 2U, sizeof(TCHAR));
  memcpy(pat, path, path_len + 1U);
#endif

  pat[path_len]      = '\\';
  pat[path_len + 1U] = '*';
  pat[path_len + 2U] = '\0';

  WIN32_FIND_DATA fd;
  HANDLE          fh = FindFirstFile(pat, &fd);
  if (fh != INVALID_HANDLE_VALUE) {
    do {
      if (!!_tcscmp(fd.cFileName, dot) && !!_tcscmp(fd.cFileName, dotdot)) {
#ifdef UNICODE
        char* const name = zix_wchar_to_utf8(NULL, fd.cFileName);
        f(path, name, data);
        zix_free(NULL, name);
#else
        f(path, fd.cFileName, data);
#endif
      }
    } while (FindNextFile(fh, &fd));
  }
  FindClose(fh);
}

ZixStatus
zix_file_lock(FILE* const file, const ZixFileLockMode mode)
{
#ifdef __clang__
  (void)file;
  (void)mode;
  return ZIX_STATUS_NOT_SUPPORTED;
#else

  const intptr_t handle     = _get_osfhandle(fileno(file));
  OVERLAPPED     overlapped = {0};

  const DWORD flags =
    (LOCKFILE_EXCLUSIVE_LOCK |
     (mode == ZIX_FILE_LOCK_TRY ? LOCKFILE_FAIL_IMMEDIATELY : 0));

  return zix_windows_status(
    LockFileEx((HANDLE)handle, flags, 0, UINT32_MAX, UINT32_MAX, &overlapped));
#endif
}

ZixStatus
zix_file_unlock(FILE* const file, const ZixFileLockMode mode)
{
  (void)mode;
#ifdef __clang__
  (void)file;
  return ZIX_STATUS_NOT_SUPPORTED;
#else

  const intptr_t handle     = _get_osfhandle(fileno(file));
  OVERLAPPED     overlapped = {0};

  return zix_windows_status(
    UnlockFileEx((HANDLE)handle, 0, UINT32_MAX, UINT32_MAX, &overlapped));
#endif
}

#if USE_GETFINALPATHNAMEBYHANDLE && USE_CREATEFILE2

static HANDLE
open_attribute_handle(const char* const path)
{
  wchar_t* const wpath = zix_utf8_to_wchar(NULL, path);

  CREATEFILE2_EXTENDED_PARAMETERS params = {
    sizeof(CREATEFILE2_EXTENDED_PARAMETERS),
    0U,
    FILE_FLAG_BACKUP_SEMANTICS,
    0U,
    NULL,
    NULL};

  const HANDLE handle =
    CreateFile2(wpath, FILE_READ_ATTRIBUTES, 0U, OPEN_EXISTING, &params);

  zix_free(NULL, wpath);
  return handle;
}

#elif USE_GETFINALPATHNAMEBYHANDLE

static HANDLE
open_attribute_handle(const char* const path)
{
  ArgPathChar* const wpath = arg_path_new(NULL, path);

  const HANDLE handle = CreateFile(wpath,
                                   FILE_READ_ATTRIBUTES,
                                   0U,
                                   NULL,
                                   OPEN_EXISTING,
                                   FILE_FLAG_BACKUP_SEMANTICS,
                                   NULL);

  arg_path_free(NULL, wpath);
  return handle;
}

#endif

char*
zix_canonical_path(ZixAllocator* const allocator, const char* const path)
{
  if (!path) {
    return NULL;
  }

#if USE_GETFINALPATHNAMEBYHANDLE // Vista+

  const HANDLE h     = open_attribute_handle(path);
  TCHAR*       final = NULL;
  if (h != INVALID_HANDLE_VALUE) {
    const DWORD flags  = FILE_NAME_NORMALIZED | VOLUME_NAME_DOS;
    const DWORD length = GetFinalPathNameByHandle(h, NULL, 0U, flags);
    if (length) {
      final = (TCHAR*)zix_calloc(allocator, (size_t)length + 1U, sizeof(TCHAR));
      if (final) {
        GetFinalPathNameByHandle(h, final, length + 1U, flags);
      }
    }
  }

  CloseHandle(h);
  return path_result(allocator, final);

#else // Fall back to "full path iff it exists" for older Windows

  ArgPathChar* const wpath = arg_path_new(allocator, path);
  TCHAR*             full  = NULL;
  if (GetFileAttributes(wpath) != INVALID_FILE_ATTRIBUTES) {
    const DWORD length = GetFullPathName(wpath, 0U, NULL, NULL);
    if (length) {
      full = (TCHAR*)zix_calloc(allocator, (size_t)length + 1U, sizeof(TCHAR));
      if (full) {
        GetFullPathName(wpath, length + 1U, full, NULL);
      }
    }
  }

  arg_path_free(allocator, wpath);
  return path_result(allocator, full);

#endif
}

static ZixFileType
attrs_file_type(const DWORD attrs)
{
  if (attrs == INVALID_FILE_ATTRIBUTES) {
    return ZIX_FILE_TYPE_NONE;
  }

  if (attrs & FILE_ATTRIBUTE_DIRECTORY) {
    return ZIX_FILE_TYPE_DIRECTORY;
  }

  if (attrs & FILE_ATTRIBUTE_REPARSE_POINT) {
    return ZIX_FILE_TYPE_SYMLINK;
  }

  if (attrs & (FILE_ATTRIBUTE_DEVICE)) {
    return ZIX_FILE_TYPE_UNKNOWN;
  }

  return ZIX_FILE_TYPE_REGULAR;
}

ZixFileType
zix_file_type(const char* const path)
{
  const ZixFileType type = zix_symlink_type(path);
  if (type != ZIX_FILE_TYPE_SYMLINK) {
    return type;
  }

  // Resolve symlink to find the canonical type
  char* const       canonical = zix_canonical_path(NULL, path);
  const ZixFileType real_type =
    canonical ? zix_symlink_type(canonical) : ZIX_FILE_TYPE_NONE;

  zix_free(NULL, canonical);
  return real_type;
}

ZixFileType
zix_symlink_type(const char* const path)
{
  ArgPathChar* const wpath = arg_path_new(NULL, path);
  if (!wpath) {
    return ZIX_FILE_TYPE_NONE;
  }

  const ZixFileType type = attrs_file_type(GetFileAttributes(wpath));
  arg_path_free(NULL, wpath);
  return type;
}

ZixStatus
zix_create_directory(const char* const dir_path)
{
  if (!dir_path[0]) {
    return ZIX_STATUS_BAD_ARG;
  }

  ArgPathChar* const wpath = arg_path_new(NULL, dir_path);
  const ZixStatus    st    = zix_windows_status(CreateDirectory(wpath, NULL));
  arg_path_free(NULL, wpath);
  return st;
}

ZixStatus
zix_create_directory_like(const char* const dir_path,
                          const char* const existing_path)
{
  return (zix_file_type(existing_path) != ZIX_FILE_TYPE_DIRECTORY)
           ? ZIX_STATUS_NOT_FOUND
           : zix_create_directory(dir_path);
}

ZixStatus
zix_create_symlink(const char* const target_path, const char* const link_path)
{
#if USE_CREATESYMBOLICLINK
  ZIX_CONSTEXPR DWORD flags = SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE;

  ArgPathChar* const wtarget = arg_path_new(NULL, target_path);
  ArgPathChar* const wlink   = arg_path_new(NULL, link_path);

  const BOOL success = CreateSymbolicLink(wlink, wtarget, flags);

  arg_path_free(NULL, wlink);
  arg_path_free(NULL, wtarget);
  return zix_windows_status(success);

#else
  (void)target_path;
  (void)link_path;
  return ZIX_STATUS_NOT_SUPPORTED;
#endif
}

ZixStatus
zix_create_directory_symlink(const char* const target_path,
                             const char* const link_path)
{
#if USE_CREATESYMBOLICLINK
  ZIX_CONSTEXPR DWORD flags =
    SYMBOLIC_LINK_FLAG_DIRECTORY | SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE;

  ArgPathChar* const wtarget = arg_path_new(NULL, target_path);
  ArgPathChar* const wlink   = arg_path_new(NULL, link_path);

  const BOOL success = CreateSymbolicLink(wlink, wtarget, flags);

  arg_path_free(NULL, wlink);
  arg_path_free(NULL, wtarget);
  return zix_windows_status(success);

#else
  (void)target_path;
  (void)link_path;
  return ZIX_STATUS_NOT_SUPPORTED;
#endif
}

ZixStatus
zix_create_hard_link(const char* const target_path, const char* const link_path)
{
#if USE_CREATEHARDLINK
  ArgPathChar* const wtarget = arg_path_new(NULL, target_path);
  ArgPathChar* const wlink   = arg_path_new(NULL, link_path);

  const BOOL success = CreateHardLink(wlink, wtarget, NULL);

  arg_path_free(NULL, wlink);
  arg_path_free(NULL, wtarget);
  return zix_windows_status(success);

#else
  (void)target_path;
  (void)link_path;
  return ZIX_STATUS_NOT_SUPPORTED;
#endif
}

char*
zix_temp_directory_path(ZixAllocator* const allocator)
{
  const DWORD  size = GetTempPath(0U, NULL);
  TCHAR* const buf  = (TCHAR*)zix_calloc(allocator, size, sizeof(TCHAR));
  if (buf && (GetTempPath(size, buf) != size - 1U)) {
    zix_free(allocator, buf);
    return NULL;
  }

  return path_result(allocator, buf);
}

char*
zix_current_path(ZixAllocator* const allocator)
{
  const DWORD  size = GetCurrentDirectory(0U, NULL);
  TCHAR* const buf  = (TCHAR*)zix_calloc(allocator, size, sizeof(TCHAR));
  if (buf && (GetCurrentDirectory(size, buf) != size - 1U)) {
    zix_free(allocator, buf);
    return NULL;
  }

  return path_result(allocator, buf);
}
