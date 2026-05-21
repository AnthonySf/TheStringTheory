// Copyright 2020-2024 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef LILV_DYLIB_H
#define LILV_DYLIB_H

/// Flags for dylib_open()
enum DylibFlags {
  DYLIB_LAZY = 1U << 0U, ///< Resolve symbols only when referenced
  DYLIB_NOW  = 1U << 1U, ///< Resolve all symbols on library load
};

/// An opaque dynamically loaded shared library
typedef void DylibLib;

/// A function from a shared library
typedef void (*DylibFunc)(void);

/// Open a shared library
DylibLib*
dylib_open(const char* filename, unsigned flags);

/// Close a shared library opened with dylib_open()
int
dylib_close(DylibLib* handle);

/// Return a human-readable description of any error since the last call
const char*
dylib_error(void);

/// Return a pointer to a function in a shared library, or null
DylibFunc
dylib_func(DylibLib* handle, const char* symbol);

#endif // LILV_DYLIB_H
