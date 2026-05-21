// Copyright 2021-2024 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

/*
  Configuration header that defines reasonable defaults at compile time.

  This allows compile-time configuration from the command line (typically via
  the build system) while still allowing the source to be built without any
  configuration.  The build system can define SORD_NO_DEFAULT_CONFIG to disable
  defaults, in which case it must define things like HAVE_FEATURE to enable
  features.  The design here ensures that compiler warnings or
  include-what-you-use will catch any mistakes.
*/

#ifndef SORD_CONFIG_H
#define SORD_CONFIG_H

// Define version unconditionally so a warning will catch a mismatch
#define SORD_VERSION "0.16.22"

#ifndef SORD_NO_DEFAULT_CONFIG

// The validator uses PCRE2 for literal pattern matching
#  ifndef HAVE_PCRE2
#    ifdef __has_include
#      if __has_include(<pcre2.h>)
#        define HAVE_PCRE2 1
#      endif
#    endif
#  endif

#endif // !defined(SORD_NO_DEFAULT_CONFIG)

/*
  Make corresponding USE_FEATURE defines based on the HAVE_FEATURE defines from
  above or the command line.  The code checks for these using #if (not #ifdef),
  so there will be an undefined warning if it checks for an unknown feature,
  and this header is always required by any code that checks for features, even
  if the build system defines them all.
*/

#ifdef HAVE_PCRE2
#  define USE_PCRE2 1
#else
#  define USE_PCRE2 0
#endif

#endif // SORD_CONFIG_H
