// Copyright 2011-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "expect_string.h"

#include <serd/serd.h>

#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <string.h>

#define USTR(s) ((const uint8_t*)(s))

static void
test_uri_string_has_scheme(void)
{
  assert(!serd_uri_string_has_scheme(NULL));

  assert(!serd_uri_string_has_scheme(USTR("relative")));
  assert(!serd_uri_string_has_scheme(USTR("http")));
  assert(!serd_uri_string_has_scheme(USTR("5nostartdigit")));
  assert(!serd_uri_string_has_scheme(USTR("+nostartplus")));
  assert(!serd_uri_string_has_scheme(USTR("-nostartminus")));
  assert(!serd_uri_string_has_scheme(USTR(".nostartdot")));
  assert(!serd_uri_string_has_scheme(USTR(":missing")));
  assert(!serd_uri_string_has_scheme(USTR("a/slash/is/not/a/scheme/char")));

  assert(serd_uri_string_has_scheme(USTR("http://example.org/")));
  assert(serd_uri_string_has_scheme(USTR("https://example.org/")));
  assert(serd_uri_string_has_scheme(USTR("allapha:path")));
  assert(serd_uri_string_has_scheme(USTR("w1thd1g1t5:path")));
  assert(serd_uri_string_has_scheme(USTR("with.dot:path")));
  assert(serd_uri_string_has_scheme(USTR("with+plus:path")));
  assert(serd_uri_string_has_scheme(USTR("with-minus:path")));
}

static void
check_file_uri(const char* const hostname,
               const char* const path,
               const bool        escape,
               const char* const expected_uri,
               const char*       expected_path)
{
  if (!expected_path) {
    expected_path = path;
  }

  SerdNode node = serd_node_new_file_uri(USTR(path), USTR(hostname), 0, escape);

  uint8_t* out_hostname = NULL;
  uint8_t* out_path =
    serd_file_uri_parse((const uint8_t*)node.buf, &out_hostname);

  assert(expect_string((const char*)node.buf, expected_uri));
  assert((hostname && out_hostname) || (!hostname && !out_hostname));
  assert(!hostname || expect_string(hostname, (const char*)out_hostname));
  assert(expect_string((const char*)out_path, (const char*)expected_path));

  serd_free(out_path);
  serd_free(out_hostname);
  serd_node_free(&node);
}

#ifdef __GNUC__
#  pragma GCC diagnostic push
#  pragma GCC diagnostic ignored "-Wdeprecated-declarations"
#endif

static void
check_uri_to_path(const char* const uri, const char* const expected_path)
{
  const uint8_t* const path = serd_uri_to_path((const uint8_t*)uri);
  assert(expect_string((const char*)path, expected_path));
}

static void
test_uri_to_path(void)
{
  assert(!serd_uri_to_path((const uint8_t*)"file:invalid/file/uri"));
  assert(!serd_uri_to_path((const uint8_t*)"http://example.org/path"));

  check_uri_to_path("file:///home/user/foo.ttl", "/home/user/foo.ttl");
  check_uri_to_path("file://localhost/home/user/foo.ttl", "/home/user/foo.ttl");
  check_uri_to_path("file:///c:/awful/system", "c:/awful/system");
  check_uri_to_path("file:///c:awful/system", "/c:awful/system");
  check_uri_to_path("file:///0/1", "/0/1");
  check_uri_to_path("C:\\Windows\\Sucks", "C:\\Windows\\Sucks");
  check_uri_to_path("C|/Windows/Sucks", "C|/Windows/Sucks");
  check_uri_to_path("rel", "rel");
}

#ifdef __GNUC__
#  pragma GCC diagnostic pop
#endif

static void
test_uri_parsing(void)
{
  check_file_uri(NULL, "C:/My 100%", true, "file:///C:/My%20100%%", NULL);
  check_file_uri(NULL, "/foo/bar", true, "file:///foo/bar", NULL);
  check_file_uri("bhost", "/foo/bar", true, "file://bhost/foo/bar", NULL);
  check_file_uri(NULL, "a/relative path", false, "a/relative path", NULL);
  check_file_uri(
    NULL, "a/relative <path>", true, "a/relative%20%3Cpath%3E", NULL);

#ifdef _WIN32
  check_file_uri(
    NULL, "C:\\My 100%", true, "file:///C:/My%20100%%", "C:/My 100%");

  check_file_uri(NULL,
                 "\\drive\\relative",
                 true,
                 "file:///drive/relative",
                 "/drive/relative");

  check_file_uri(NULL,
                 "C:\\Program Files\\Serd",
                 true,
                 "file:///C:/Program%20Files/Serd",
                 "C:/Program Files/Serd");

  check_file_uri("ahost",
                 "C:\\Pointless Space",
                 true,
                 "file://ahost/C:/Pointless%20Space",
                 "C:/Pointless Space");
#else
  /* What happens with Windows paths on other platforms is a bit weird, but
     more or less unavoidable.  It doesn't work to interpret backslashes as
     path separators on any other platform. */

  check_file_uri("ahost",
                 "C:\\Pointless Space",
                 true,
                 "file://ahost/C:%5CPointless%20Space",
                 "/C:\\Pointless Space");

  check_file_uri(NULL,
                 "\\drive\\relative",
                 true,
                 "%5Cdrive%5Crelative",
                 "\\drive\\relative");

  check_file_uri(NULL,
                 "C:\\Program Files\\Serd",
                 true,
                 "file:///C:%5CProgram%20Files%5CSerd",
                 "/C:\\Program Files\\Serd");

  check_file_uri("ahost",
                 "C:\\Pointless Space",
                 true,
                 "file://ahost/C:%5CPointless%20Space",
                 "/C:\\Pointless Space");
#endif

  // Test tolerance of NULL hostname parameter
  uint8_t* const hosted = serd_file_uri_parse(USTR("file://host/path"), NULL);
  assert(expect_string((const char*)hosted, "/path"));
  serd_free(hosted);

  // Test tolerance of parsing junk URI escapes

  uint8_t* const junk1 = serd_file_uri_parse(USTR("file:///foo/%0Xbar"), NULL);
  assert(expect_string((const char*)junk1, "/foo/bar"));
  serd_free(junk1);

  uint8_t* const junk2 = serd_file_uri_parse(USTR("file:///foo/%X0bar"), NULL);
  assert(expect_string((const char*)junk2, "/foo/bar"));
  serd_free(junk2);
}

static void
test_uri_from_string(void)
{
  SerdNode nonsense = serd_node_new_uri_from_string(NULL, NULL, NULL);
  assert(nonsense.type == SERD_NOTHING);

  SerdURI  base_uri;
  SerdNode base =
    serd_node_new_uri_from_string(USTR("http://example.org/"), NULL, &base_uri);
  SerdNode nil  = serd_node_new_uri_from_string(NULL, &base_uri, NULL);
  SerdNode nil2 = serd_node_new_uri_from_string(USTR(""), &base_uri, NULL);
  assert(nil.type == SERD_URI);
  assert(expect_string((const char*)nil.buf, (const char*)base.buf));
  assert(nil2.type == SERD_URI);
  assert(expect_string((const char*)nil2.buf, (const char*)base.buf));
  serd_node_free(&nil);
  serd_node_free(&nil2);

  serd_node_free(&base);
}

static bool
chunk_equals(const SerdChunk* const a, const SerdChunk* const b)
{
  return (!a->len && !b->len && !a->buf && !b->buf) ||
         (a->len && b->len && a->buf && b->buf &&
          !strncmp((const char*)a->buf, (const char*)b->buf, a->len));
}

static void
check_relative_uri(const char* const uri_string,
                   const char* const base_string,
                   const char* const root_string,
                   const char* const expected_string)
{
  assert(uri_string);
  assert(base_string);
  assert(expected_string);

  SerdURI uri    = SERD_URI_NULL;
  SerdURI base   = SERD_URI_NULL;
  SerdURI result = SERD_URI_NULL;

  SerdNode uri_node =
    serd_node_new_uri_from_string(USTR(uri_string), NULL, &uri);

  SerdNode base_node =
    serd_node_new_uri_from_string(USTR(base_string), NULL, &base);

  SerdNode result_node = SERD_NODE_NULL;
  if (root_string) {
    SerdURI  root = SERD_URI_NULL;
    SerdNode root_node =
      serd_node_new_uri_from_string(USTR(root_string), NULL, &root);

    result_node = serd_node_new_relative_uri(&uri, &base, &root, &result);
    serd_node_free(&root_node);
  } else {
    result_node = serd_node_new_relative_uri(&uri, &base, NULL, &result);
  }

  assert(expect_string((const char*)result_node.buf, expected_string));

  SerdURI expected = SERD_URI_NULL;
  assert(!serd_uri_parse(USTR(expected_string), &expected));
  assert(chunk_equals(&result.scheme, &expected.scheme));
  assert(chunk_equals(&result.authority, &expected.authority));
  assert(chunk_equals(&result.path_base, &expected.path_base));
  assert(chunk_equals(&result.path, &expected.path));
  assert(chunk_equals(&result.query, &expected.query));
  assert(chunk_equals(&result.fragment, &expected.fragment));

  serd_node_free(&result_node);
  serd_node_free(&base_node);
  serd_node_free(&uri_node);
}

static void
test_relative_uri(void)
{
  // Unrelated base

  check_relative_uri("http://example.org/a/b",
                     "ftp://example.org/",
                     NULL,
                     "http://example.org/a/b");

  check_relative_uri("http://example.org/a/b",
                     "http://example.com/",
                     NULL,
                     "http://example.org/a/b");

  // Related base

  check_relative_uri(
    "http://example.org/a/b", "http://example.org/", NULL, "a/b");

  check_relative_uri(
    "http://example.org/a/b", "http://example.org/a/", NULL, "b");

  check_relative_uri(
    "http://example.org/a/b", "http://example.org/a/b", NULL, "");

  check_relative_uri(
    "http://example.org/a/b", "http://example.org/a/b/", NULL, "../b");

  check_relative_uri(
    "http://example.org/a/b/", "http://example.org/a/b/", NULL, "");

  check_relative_uri("http://example.org/", "http://example.org/", NULL, "");

  check_relative_uri("http://example.org/", "http://example.org/a", NULL, "");

  check_relative_uri(
    "http://example.org/", "http://example.org/a/", NULL, "../");

  check_relative_uri(
    "http://example.org/", "http://example.org/a/b", NULL, "../");

  check_relative_uri(
    "http://example.org/", "http://example.org/a/b/", NULL, "../../");

  // Unrelated root

  check_relative_uri("http://example.org/",
                     "http://example.org/a/b",
                     "relative",
                     "http://example.org/");

  check_relative_uri("http://example.org/",
                     "http://example.org/a/b",
                     "ftp://example.org/",
                     "http://example.org/");

  check_relative_uri("http://example.org/",
                     "http://example.org/a/b",
                     "http://example.com/",
                     "http://example.org/");

  // Related root

  check_relative_uri("http://example.org/a/b",
                     "http://example.org/",
                     "http://example.org/c/d",
                     "http://example.org/a/b");

  check_relative_uri("http://example.org/",
                     "http://example.org/a/b",
                     "http://example.org/a/b",
                     "http://example.org/");

  check_relative_uri("http://example.org/a/b",
                     "http://example.org/a/b",
                     "http://example.org/a/b",
                     "");

  check_relative_uri("http://example.org/a/",
                     "http://example.org/a/",
                     "http://example.org/a/",
                     "");

  check_relative_uri("http://example.org/a/b",
                     "http://example.org/a/b/c",
                     "http://example.org/a/b",
                     "../b");

  check_relative_uri("http://example.org/a",
                     "http://example.org/a/b/c",
                     "http://example.org/a/b",
                     "http://example.org/a");

  // Tolerance of NULL URI output parameter
  {
    SerdURI uri = SERD_URI_NULL;
    assert(!serd_uri_parse(USTR("http://example.org/path"), &uri));

    SerdURI base = SERD_URI_NULL;
    assert(!serd_uri_parse(USTR("http://example.org/"), &base));

    SerdNode result_node = serd_node_new_relative_uri(&uri, &base, NULL, NULL);

    assert(result_node.n_bytes == 4U);
    assert(expect_string((const char*)result_node.buf, "path"));

    serd_node_free(&result_node);
  }
}

int
main(void)
{
  test_uri_to_path();
  test_uri_string_has_scheme();
  test_uri_parsing();
  test_uri_from_string();
  test_relative_uri();
  return 0;
}
