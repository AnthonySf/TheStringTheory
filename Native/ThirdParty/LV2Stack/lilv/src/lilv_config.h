// Copyright 2021-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef LILV_CONFIG_H
#define LILV_CONFIG_H

// Define version unconditionally so a warning will catch a mismatch
#define LILV_VERSION "0.26.5"

// Default configuration

// Separator between entries in variables like PATH
#ifndef LILV_PATH_SEP
#  ifdef _WIN32
#    define LILV_PATH_SEP ";"
#  else
#    define LILV_PATH_SEP ":"
#  endif
#endif

// Default value for LV2_PATH environment variable
#ifndef LILV_DEFAULT_LV2_PATH
#  if defined(__APPLE__)
#    define LILV_DEFAULT_LV2_PATH            \
      "~/.lv2:~/Library/Audio/Plug-Ins/LV2:" \
      "/usr/local/lib/lv2:/usr/lib/lv2:"     \
      "/Library/Audio/Plug-Ins/LV2"
#  elif defined(_WIN32)
#    define LILV_DEFAULT_LV2_PATH "%APPDATA%\\LV2;%COMMONPROGRAMFILES%\\LV2"
#  else
#    define LILV_DEFAULT_LV2_PATH "~/.lv2:/usr/local/lib/lv2:/usr/lib/lv2"
#  endif
#endif

// We need unistd.h to (include features.h to) check __GLIBC__
#ifdef __has_include
#  if __has_include(<unistd.h>)
#    include <unistd.h>
#  endif
#elif defined(__APPLE__) || defined(__unix__)
#  include <unistd.h>
#endif

// glibc 2.7: fopen() "e" mode (O_CLOEXEC)
#ifndef LILV_HAVE_FOPEN_E_MODE
#  if (defined(__GLIBC__) && \
       (__GLIBC__ > 2 || __GLIBC__ == 2 && __GLIBC_MINOR__ >= 7))
#    define LILV_HAVE_FOPEN_E_MODE 1
#  endif
#endif

// Unconditionally defined feature symbols

#if defined(LILV_HAVE_FOPEN_E_MODE) && LILV_HAVE_FOPEN_E_MODE
#  define USE_FOPEN_E_MODE 1
#else
#  define USE_FOPEN_E_MODE 0
#endif

#endif // LILV_CONFIG_H
