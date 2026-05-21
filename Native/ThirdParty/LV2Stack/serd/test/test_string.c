// Copyright 2011-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "expect_string.h"

#include <serd/serd.h>

#include <assert.h>
#include <stdint.h>
#include <string.h>

static void
test_expect_string(void)
{
  assert(expect_string("match", "match"));
  assert(!expect_string("intentional", "failure"));
}

static void
check_strlen(const char* const   str,
             const size_t        expected_n_bytes,
             const size_t        expected_n_chars,
             const SerdNodeFlags expected_flags)
{
  size_t        n_bytes = 0U;
  SerdNodeFlags flags   = 0U;
  const size_t  n_chars = serd_strlen((const uint8_t*)str, &n_bytes, &flags);

  assert(n_bytes == expected_n_bytes);
  assert(n_chars == expected_n_chars);
  assert(flags == expected_flags);
}

static void
test_strlen(void)
{
  static const uint8_t utf8[] = {'"', '5', 0xE2, 0x82, 0xAC, '"', '\n', 0};

  check_strlen("\"quotes\"", 8U, 8U, SERD_HAS_QUOTE);
  check_strlen("newline\n", 8U, 8U, SERD_HAS_NEWLINE);
  check_strlen("\rreturn", 7U, 7U, SERD_HAS_NEWLINE);
  check_strlen((const char*)utf8, 7U, 5U, SERD_HAS_QUOTE | SERD_HAS_NEWLINE);

  assert(serd_strlen((const uint8_t*)"nulls", NULL, NULL) == 5U);
}

static void
test_strerror(void)
{
  const uint8_t* msg = serd_strerror(SERD_SUCCESS);
  assert(expect_string((const char*)msg, "Success"));
  for (int i = SERD_FAILURE; i <= SERD_ERR_BAD_TEXT; ++i) {
    msg = serd_strerror((SerdStatus)i);
    assert(strcmp((const char*)msg, "Success"));
  }

  msg = serd_strerror((SerdStatus)-1);
  assert(expect_string((const char*)msg, "Unknown error"));
}

int
main(void)
{
  test_expect_string();
  test_strlen();
  test_strerror();
  return 0;
}
