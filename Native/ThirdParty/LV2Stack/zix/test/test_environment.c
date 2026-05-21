// Copyright 2012-2024 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "failing_allocator.h"

#include <zix/allocator.h>
#include <zix/environment.h>
#include <zix/path.h>

#include <assert.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32

#  define HOME_NAME "USERPROFILE"
#  define HOME_VAR "%USERPROFILE%"

#else

#  define HOME_NAME "HOME"
#  define HOME_VAR "$HOME"

static char*
concat(ZixAllocator* const allocator,
       const char* const   prefix,
       const char* const   suffix)
{
  const size_t prefix_len = strlen(prefix);
  const size_t suffix_len = strlen(suffix);
  const size_t result_len = prefix_len + suffix_len;

  char* const result = (char*)zix_calloc(allocator, 1U, result_len + 1U);
  assert(result);
  memcpy(result, prefix, prefix_len + 1U);
  memcpy(result + prefix_len, suffix, suffix_len + 1U);
  return result;
}

#endif

static void
check_expansion(const char* const path, const char* const expected)
{
  char* const expanded = zix_expand_environment_strings(NULL, path);
  assert(expanded);
  assert(!strcmp(expanded, expected));
  zix_free(NULL, expanded);
}

static void
test_expansion(void)
{
  // Check non-expansion of hopefully unset variables
  check_expansion("$ZIX_UNSET0", "$ZIX_UNSET0");
  check_expansion("$ZIX_unset0", "$ZIX_unset0");
  check_expansion("%ZIX_UNSET0%", "%ZIX_UNSET0%");
  check_expansion("%ZIX_unset0%", "%ZIX_unset0%");

  // Check non-expansion of invalid variable names
  check_expansion("$%INVALID", "$%INVALID");
  check_expansion("$<INVALID>", "$<INVALID>");
  check_expansion("$[INVALID]", "$[INVALID]");
  check_expansion("$invalid", "$invalid");
  check_expansion("${INVALID}", "${INVALID}");

  const char* const home = getenv(HOME_NAME);
  if (home) {
    char* const var_foo  = zix_path_join(NULL, HOME_VAR, "foo");
    char* const home_foo = zix_path_join(NULL, home, "foo");

    check_expansion(var_foo, home_foo);

#ifndef _WIN32
    char* const tilde_foo      = zix_path_join(NULL, "~", "foo");
    char* const home_and_other = concat(NULL, home, ":/other");
    char* const other_and_home = concat(NULL, "/other:", home);
    check_expansion("~other", "~other");
    check_expansion("~", home);
    check_expansion("~/foo", home_foo);
    check_expansion("~:/other", home_and_other);
    check_expansion("/other:~", other_and_home);
    check_expansion("$HO", "$HO");
    check_expansion("$HOMEZIX", "$HOMEZIX");
    zix_free(NULL, other_and_home);
    zix_free(NULL, home_and_other);
    zix_free(NULL, tilde_foo);
#endif

    zix_free(NULL, home_foo);
    zix_free(NULL, var_foo);
  }
}

static void
test_failed_alloc(void)
{
  ZixFailingAllocator allocator = zix_failing_allocator();

  zix_failing_allocator_reset(&allocator, 0U);
  assert(!zix_expand_environment_strings(&allocator.base, "/one:~"));
  assert(!zix_expand_environment_strings(&allocator.base, "/only"));
  assert(!zix_expand_environment_strings(&allocator.base, "~"));

#ifndef _WIN32
  zix_failing_allocator_reset(&allocator, 1U);
  assert(!zix_expand_environment_strings(&allocator.base, "/one:~"));
  assert(!zix_expand_environment_strings(&allocator.base, "/one:$HOME/two"));

  zix_failing_allocator_reset(&allocator, 2U);
  assert(!zix_expand_environment_strings(&allocator.base, "/one:$HOME/two"));

  zix_failing_allocator_reset(&allocator, 1U);
  assert(!zix_expand_environment_strings(&allocator.base, "/one:$UNSET/two"));
#endif
}

#ifndef _WIN32

// NOLINTNEXTLINE(cppcoreguidelines-avoid-non-const-global-variables)
extern char** environ;

static void
test_null_environ(void)
{
  environ = NULL;
  check_expansion(HOME_VAR, HOME_VAR);
}

#endif

int
main(void)
{
  test_expansion();
  test_failed_alloc();
#ifndef _WIN32
  test_null_environ();
#endif
  return 0;
}
