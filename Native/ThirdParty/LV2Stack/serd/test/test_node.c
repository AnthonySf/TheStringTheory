// Copyright 2011-2020 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "expect_string.h"

#include <serd/serd.h>

#include <assert.h>
#include <float.h>
#include <math.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define USTR(s) ((const uint8_t*)(s))

#ifndef INFINITY
#  define INFINITY (DBL_MAX + DBL_MAX)
#endif
#ifndef NAN
#  define NAN (INFINITY - INFINITY)
#endif

static void
check_strtod(const double dbl, const double max_delta)
{
  char buf[1024];
  snprintf(buf, sizeof(buf), "%f", dbl);

  char*        endptr = NULL;
  const double out    = serd_strtod(buf, &endptr);
  const double diff   = fabs(out - dbl);

  assert(diff <= max_delta);
}

static void
test_string_to_double(void)
{
  const double expt_test_nums[] = {
    2.0E18, -5e19, +8e20, 2e+22, -5e-5, 8e0, 9e-0, 2e+0};

  const char* expt_test_strs[] = {"02e18",
                                  "-5e019",
                                  " +8e20",
                                  "\f2E+22",
                                  "\n-5E-5",
                                  "\r8E0",
                                  "\t9e-0",
                                  "\v2e+0"};

  for (size_t i = 0; i < sizeof(expt_test_nums) / sizeof(double); ++i) {
    const double num   = serd_strtod(expt_test_strs[i], NULL);
    const double delta = fabs(num - expt_test_nums[i]);
    assert(delta <= DBL_EPSILON);

    check_strtod(expt_test_nums[i], DBL_EPSILON);
  }
}

static void
test_double_to_node(void)
{
  const double dbl_test_nums[] = {0.0,
                                  9.0,
                                  10.0,
                                  .01,
                                  2.05,
                                  -16.00001,
                                  5.000000005,
                                  0.0000000001,
                                  (double)NAN,
                                  (double)INFINITY};

  const char* dbl_test_strs[] = {"0.0",
                                 "9.0",
                                 "10.0",
                                 "0.01",
                                 "2.05",
                                 "-16.00001",
                                 "5.00000001",
                                 "0.0",
                                 NULL,
                                 NULL};

  for (size_t i = 0; i < sizeof(dbl_test_nums) / sizeof(double); ++i) {
    SerdNode   node = serd_node_new_decimal(dbl_test_nums[i], 8);
    const bool pass = (node.buf && dbl_test_strs[i])
                        ? expect_string((const char*)node.buf, dbl_test_strs[i])
                        : ((const char*)node.buf == dbl_test_strs[i]);
    assert(pass);
    const size_t len = node.buf ? strlen((const char*)node.buf) : 0;
    assert(node.n_bytes == len && node.n_chars == len);
    serd_node_free(&node);
  }
}

static void
test_integer_to_node(void)
{
#define N_TEST_NUMS 7U

  const long int_test_nums[N_TEST_NUMS] = {0, -0, -23, 23, -12340, 1000, -1000};

  const char* int_test_strs[N_TEST_NUMS] = {
    "0", "0", "-23", "23", "-12340", "1000", "-1000"};

  for (size_t i = 0; i < N_TEST_NUMS; ++i) {
    SerdNode node = serd_node_new_integer(int_test_nums[i]);
    assert(expect_string((const char*)node.buf, (const char*)int_test_strs[i]));
    const size_t len = strlen((const char*)node.buf);
    assert(node.n_bytes == len && node.n_chars == len);
    serd_node_free(&node);
  }

#undef N_TEST_NUMS
}

static void
test_blob_to_node(void)
{
  for (size_t size = 1; size < 256; ++size) {
    uint8_t* const data = (uint8_t*)malloc(size);
    assert(data);
    for (size_t i = 0; i < size; ++i) {
      data[i] = (uint8_t)((size + i) % 256);
    }

    SerdNode             blob     = serd_node_new_blob(data, size, size % 5);
    const uint8_t* const blob_str = blob.buf;

    assert(blob_str);
    assert(blob.n_bytes == blob.n_chars);
    assert(blob.n_bytes == strlen((const char*)blob_str));

    size_t   out_size = 0;
    uint8_t* out =
      (uint8_t*)serd_base64_decode(blob_str, blob.n_bytes, &out_size);
    assert(out_size == size);

    for (size_t i = 0; i < size; ++i) {
      assert(out[i] == data[i]);
    }

    serd_node_free(&blob);
    serd_free(out);
    free(data);
  }
}

static void
test_base64_decode(void)
{
  static const char* const decoded     = "test";
  static const size_t      decoded_len = 4U;

  // Test decoding clean base64
  {
    static const char* const encoded     = "dGVzdA==";
    static const size_t      encoded_len = 8U;

    size_t      size = 0U;
    void* const data =
      serd_base64_decode((const uint8_t*)encoded, encoded_len, &size);

    assert(data);
    assert(size == decoded_len);
    assert(!strncmp((const char*)data, decoded, decoded_len));
    serd_free(data);
  }

  // Test decoding equivalent dirty base64 with ignored junk characters
  {
    static const char* const encoded     = "d-G#V!z*d(A$%==";
    static const size_t      encoded_len = 13U;

    size_t      size = 0U;
    void* const data =
      serd_base64_decode((const uint8_t*)encoded, encoded_len, &size);

    assert(data);
    assert(size == decoded_len);
    assert(!strncmp((const char*)data, decoded, decoded_len));
    serd_free(data);
  }

  // Test decoding effectively nothing
  {
    static const char* const encoded     = "@#$%";
    static const size_t      encoded_len = 4U;

    size_t      size = 0U;
    void* const data =
      serd_base64_decode((const uint8_t*)encoded, encoded_len, &size);

    assert(data);
    assert(!size);
    // Contents of data are undefined
    serd_free(data);
  }
}

static void
test_node_equals(void)
{
  const uint8_t replacement_char_str[] = {0xEF, 0xBF, 0xBD, 0};
  SerdNode      lhs = serd_node_from_string(SERD_LITERAL, replacement_char_str);
  SerdNode      rhs = serd_node_from_string(SERD_LITERAL, USTR("123"));
  assert(!serd_node_equals(&lhs, &rhs));

  SerdNode qnode = serd_node_from_string(SERD_CURIE, USTR("foo:bar"));
  assert(!serd_node_equals(&lhs, &qnode));
  assert(serd_node_equals(&lhs, &lhs));

  SerdNode null_copy = serd_node_copy(&SERD_NODE_NULL);
  assert(serd_node_equals(&SERD_NODE_NULL, &null_copy));
}

static void
test_node_from_string(void)
{
  SerdNode node =
    serd_node_from_string(SERD_LITERAL, (const uint8_t*)"hello\"");

  assert(node.n_bytes == 6 && node.n_chars == 6 &&
         node.flags == SERD_HAS_QUOTE &&
         expect_string((const char*)node.buf, "hello\""));

  node = serd_node_from_string(SERD_URI, NULL);
  assert(serd_node_equals(&node, &SERD_NODE_NULL));
}

static void
test_node_from_substring(void)
{
  static const uint8_t utf8_str[] = {'l', 0xC3, 0xB6, 'n', 'g', 0};

  SerdNode empty = serd_node_from_substring(SERD_LITERAL, NULL, 32);
  assert(!empty.buf && !empty.n_bytes && !empty.n_chars && !empty.flags &&
         !empty.type);

  SerdNode a_b = serd_node_from_substring(SERD_LITERAL, USTR("a\"bc"), 3);
  assert(a_b.n_bytes == 3 && a_b.n_chars == 3 && a_b.flags == SERD_HAS_QUOTE &&
         !strncmp((const char*)a_b.buf, "a\"b", 3));

  a_b = serd_node_from_substring(SERD_LITERAL, USTR("a\"bc"), 10);
  assert(a_b.n_bytes == 4 && a_b.n_chars == 4 && a_b.flags == SERD_HAS_QUOTE &&
         !strncmp((const char*)a_b.buf, "a\"bc", 4));

  SerdNode utf8 = serd_node_from_substring(SERD_LITERAL, utf8_str, 5);
  assert(utf8.n_bytes == 5 && utf8.n_chars == 4 && !utf8.flags &&
         !strncmp((const char*)utf8.buf, (const char*)utf8_str, 6));
}

static void
test_uri_node_from_node(void)
{
  const SerdNode string      = serd_node_from_string(SERD_LITERAL, USTR("s"));
  SerdNode       string_node = serd_node_new_uri_from_node(&string, NULL, NULL);
  assert(!string_node.n_bytes);
  serd_node_free(&string_node);

  const SerdNode nouri      = {NULL, 0U, 0U, 0U, SERD_URI};
  SerdNode       nouri_node = serd_node_new_uri_from_node(&nouri, NULL, NULL);
  assert(!nouri_node.n_bytes);
  serd_node_free(&nouri_node);

  const SerdNode uri =
    serd_node_from_string(SERD_URI, USTR("http://example.org/p"));
  SerdNode uri_node = serd_node_new_uri_from_node(&uri, NULL, NULL);
  assert(uri_node.n_bytes == 20U);
  serd_node_free(&uri_node);
}

int
main(void)
{
  test_string_to_double();
  test_double_to_node();
  test_integer_to_node();
  test_blob_to_node();
  test_base64_decode();
  test_node_equals();
  test_node_from_string();
  test_node_from_substring();
  test_uri_node_from_node();
  return 0;
}
