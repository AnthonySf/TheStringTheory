// Copyright 2011-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "expect_string.h"

#include <serd/serd.h>

#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define NS_EG "http://example.org/"

#define USTR(s) ((const uint8_t*)(s))

static void
test_write_long_literal(void)
{
  SerdEnv*    env    = serd_env_new(NULL);
  SerdChunk   chunk  = {NULL, 0};
  SerdWriter* writer = serd_writer_new(
    SERD_TURTLE, (SerdStyle)0, env, NULL, serd_chunk_sink, &chunk);
  assert(writer);

  SerdNode s = serd_node_from_string(SERD_URI, USTR(NS_EG "s"));
  SerdNode p = serd_node_from_string(SERD_URI, USTR(NS_EG "p"));
  SerdNode o =
    serd_node_from_string(SERD_LITERAL, USTR("hello \"\"\"world\"\"\"!"));

  assert(!serd_writer_write_statement(writer, 0, NULL, &s, &p, &o, NULL, NULL));
  assert(!serd_writer_finish(writer));

  static const char* const expected =
    "<http://example.org/s>\n"
    "\t<http://example.org/p> \"\"\"hello \"\"\\\"world\"\"\\\"!\"\"\" .\n";

  uint8_t* const out = serd_chunk_sink_finish(&chunk);
  assert(expect_string((char*)out, expected));
  serd_free(out);

  serd_writer_free(writer);
  serd_env_free(env);
}

static void
test_write_nested_anon(void)
{
  SerdEnv*    env    = serd_env_new(NULL);
  SerdChunk   chunk  = {NULL, 0};
  SerdWriter* writer = serd_writer_new(
    SERD_TURTLE, (SerdStyle)0, env, NULL, serd_chunk_sink, &chunk);
  assert(writer);

  SerdNode s0  = serd_node_from_string(SERD_URI, USTR(NS_EG "s0"));
  SerdNode p0  = serd_node_from_string(SERD_URI, USTR(NS_EG "p0"));
  SerdNode b0  = serd_node_from_string(SERD_BLANK, USTR("b0"));
  SerdNode p1  = serd_node_from_string(SERD_URI, USTR(NS_EG "p1"));
  SerdNode b1  = serd_node_from_string(SERD_BLANK, USTR("b1"));
  SerdNode p2  = serd_node_from_string(SERD_URI, USTR(NS_EG "p2"));
  SerdNode o2  = serd_node_from_string(SERD_URI, USTR(NS_EG "o2"));
  SerdNode p3  = serd_node_from_string(SERD_URI, USTR(NS_EG "p3"));
  SerdNode p4  = serd_node_from_string(SERD_URI, USTR(NS_EG "p4"));
  SerdNode o4  = serd_node_from_string(SERD_URI, USTR(NS_EG "o4"));
  SerdNode nil = serd_node_from_string(
    SERD_URI, USTR("http://www.w3.org/1999/02/22-rdf-syntax-ns#nil"));

  assert(!serd_writer_write_statement(
    writer, SERD_ANON_O_BEGIN, NULL, &s0, &p0, &b0, NULL, NULL));

  assert(!serd_writer_write_statement(writer,
                                      SERD_ANON_O_BEGIN | SERD_ANON_CONT,
                                      NULL,
                                      &b0,
                                      &p1,
                                      &b1,
                                      NULL,
                                      NULL));

  assert(!serd_writer_write_statement(
    writer, SERD_ANON_CONT, NULL, &b1, &p2, &o2, NULL, NULL));

  assert(!serd_writer_write_statement(writer,
                                      SERD_ANON_CONT | SERD_LIST_O_BEGIN,
                                      NULL,
                                      &b1,
                                      &p3,
                                      &nil,
                                      NULL,
                                      NULL));

  assert(!serd_writer_end_anon(writer, &b1));
  assert(!serd_writer_write_statement(
    writer, SERD_ANON_CONT, NULL, &b0, &p4, &o4, NULL, NULL));

  assert(!serd_writer_end_anon(writer, &b0));
  assert(!serd_writer_finish(writer));

  static const char* const expected =
    "<http://example.org/s0>\n"
    "\t<http://example.org/p0> [\n"
    "\t\t<http://example.org/p1> [\n"
    "\t\t\t<http://example.org/p2> <http://example.org/o2> ;\n"
    "\t\t\t<http://example.org/p3> ()\n"
    "\t\t] ;\n"
    "\t\t<http://example.org/p4> <http://example.org/o4>\n"
    "\t] .\n";

  uint8_t* const out = serd_chunk_sink_finish(&chunk);
  assert(expect_string((char*)out, expected));
  serd_free(out);

  serd_writer_free(writer);
  serd_env_free(env);
}

static size_t
null_sink(const void* const buf, const size_t len, void* const stream)
{
  (void)buf;
  (void)stream;

  return len;
}

static void
test_writer_cleanup(void)
{
  SerdStatus  st  = SERD_SUCCESS;
  SerdEnv*    env = serd_env_new(NULL);
  SerdWriter* writer =
    serd_writer_new(SERD_TURTLE, (SerdStyle)0U, env, NULL, null_sink, NULL);
  assert(writer);

  SerdNode s = serd_node_from_string(SERD_URI, USTR(NS_EG "s"));
  SerdNode p = serd_node_from_string(SERD_URI, USTR(NS_EG "p"));

  char     o_buf[12] = {'b', '0', '\0'};
  SerdNode o         = serd_node_from_string(SERD_BLANK, USTR(o_buf));

  st = serd_writer_write_statement(
    writer, SERD_ANON_O_BEGIN, NULL, &s, &p, &o, NULL, NULL);

  assert(!st);

  // Write the start of several nested anonymous objects
  for (unsigned i = 1U; !st && i < 9U; ++i) {
    char next_o_buf[12] = {'\0'};
    snprintf(next_o_buf, sizeof(next_o_buf), "b%u", i);

    SerdNode next_o = serd_node_from_string(SERD_BLANK, USTR(next_o_buf));

    st = serd_writer_write_statement(writer,
                                     SERD_ANON_O_BEGIN | SERD_ANON_CONT,
                                     NULL,
                                     &o,
                                     &p,
                                     &next_o,
                                     NULL,
                                     NULL);

    assert(!st);

    memcpy(o_buf, next_o_buf, sizeof(o_buf));
  }

  // Finish writing without terminating nodes
  st = serd_writer_finish(writer);
  assert(!st);

  // Set the base to an empty URI
  st = serd_writer_set_base_uri(writer, NULL);
  assert(!st);

  // Free (which could leak if the writer doesn't clean up the stack properly)
  serd_writer_free(writer);
  serd_env_free(env);
}

static void
test_write_bad_anon_stack(void)
{
  SerdStatus  st  = SERD_SUCCESS;
  SerdEnv*    env = serd_env_new(NULL);
  SerdWriter* writer =
    serd_writer_new(SERD_TURTLE, (SerdStyle)0U, env, NULL, null_sink, NULL);
  assert(writer);

  SerdNode s  = serd_node_from_string(SERD_URI, USTR(NS_EG "s"));
  SerdNode p  = serd_node_from_string(SERD_URI, USTR(NS_EG "p"));
  SerdNode b0 = serd_node_from_string(SERD_BLANK, USTR("b0"));
  SerdNode b1 = serd_node_from_string(SERD_BLANK, USTR("b1"));
  SerdNode b2 = serd_node_from_string(SERD_BLANK, USTR("b2"));

  st = serd_writer_write_statement(
    writer, SERD_ANON_O_BEGIN, NULL, &s, &p, &b0, NULL, NULL);
  assert(!st);

  // (missing call to end the anonymous node here)

  st = serd_writer_write_statement(
    writer, SERD_ANON_O_BEGIN, NULL, &b1, &p, &b2, NULL, NULL);

  assert(st == SERD_ERR_BAD_ARG);

  st = serd_writer_finish(writer);
  assert(!st);
  serd_writer_free(writer);
  serd_env_free(env);
}

static void
test_strict_write(void)
{
  SerdEnv* const    env = serd_env_new(NULL);
  SerdWriter* const writer =
    serd_writer_new(SERD_TURTLE,
                    (SerdStyle)(SERD_STYLE_STRICT | SERD_STYLE_ASCII),
                    env,
                    NULL,
                    null_sink,
                    NULL);
  assert(writer);

  static const uint8_t bad_uri_buf[]   = {'f', 't', 'p', ':', 0xFF, 0x90, 0};
  static const uint8_t bad_short_buf[] = {0xFF, 0x90, 'h', 'i', 0};
  static const uint8_t bad_long_buf[]  = {'e', '\n', 'r', 0xFF, 0x90, 0};

  SerdNode s             = serd_node_from_string(SERD_URI, USTR(NS_EG "s"));
  SerdNode p             = serd_node_from_string(SERD_URI, USTR(NS_EG "p"));
  SerdNode bad_uri       = serd_node_from_string(SERD_URI, bad_uri_buf);
  SerdNode bad_short_lit = serd_node_from_string(SERD_LITERAL, bad_short_buf);
  SerdNode bad_long_lit  = serd_node_from_string(SERD_LITERAL, bad_long_buf);

  assert(serd_writer_write_statement(
           writer, 0, NULL, &s, &p, &bad_uri, NULL, NULL) == SERD_ERR_BAD_TEXT);

  assert(serd_writer_write_statement(
           writer, 0, NULL, &s, &p, &bad_short_lit, NULL, NULL) ==
         SERD_ERR_BAD_TEXT);

  assert(serd_writer_write_statement(
           writer, 0, NULL, &s, &p, &bad_long_lit, NULL, NULL) ==
         SERD_ERR_BAD_TEXT);

  serd_writer_free(writer);
  serd_env_free(env);
}

// Produce a write error without setting errno
static size_t
error_sink(const void* const buf, const size_t len, void* const stream)
{
  (void)buf;
  (void)len;
  (void)stream;
  return 0U;
}

static void
test_write_error(void)
{
  SerdEnv* const env = serd_env_new(NULL);

  SerdWriter* const writer =
    serd_writer_new(SERD_TURTLE, (SerdStyle)0, env, NULL, error_sink, NULL);
  assert(writer);

  SerdNode u = serd_node_from_string(SERD_URI, USTR("http://example.com/u"));

  const SerdStatus st =
    serd_writer_write_statement(writer, 0U, NULL, &u, &u, &u, NULL, NULL);
  assert(st == SERD_ERR_BAD_WRITE);

  serd_writer_free(writer);
  serd_env_free(env);
}

static void
test_chunk_sink(void)
{
  SerdEnv* const env = serd_env_new(NULL);
  assert(env);

  SerdChunk         chunk  = {NULL, 0};
  SerdWriter* const writer = serd_writer_new(
    SERD_TURTLE, (SerdStyle)0, env, NULL, serd_chunk_sink, &chunk);
  assert(writer);

  const SerdNode base =
    serd_node_from_string(SERD_URI, USTR("http://example.org/base"));
  assert(!serd_writer_set_base_uri(writer, &base));
  assert(!serd_writer_finish(writer));

  uint8_t* const out = serd_chunk_sink_finish(&chunk);
  assert(
    expect_string((const char*)out, "@base <http://example.org/base> .\n"));
  serd_free(out);

  serd_writer_free(writer);
  serd_env_free(env);
}

static void
test_write_nothing_node(void)
{
  SerdEnv* const env = serd_env_new(NULL);
  assert(env);

  SerdChunk         chunk  = {NULL, 0};
  SerdWriter* const writer = serd_writer_new(
    SERD_TURTLE, (SerdStyle)0, env, NULL, serd_chunk_sink, &chunk);
  assert(writer);

  SerdNode s = serd_node_from_string(SERD_URI, USTR(""));
  SerdNode p = serd_node_from_string(SERD_URI, USTR("http://example.org/pred"));
  SerdNode o = serd_node_from_string(SERD_NOTHING, USTR(""));
  assert(serd_writer_write_statement(writer, 0, NULL, &s, &p, &o, NULL, NULL) ==
         SERD_ERR_BAD_ARG);

  assert(!chunk.buf);
  serd_writer_free(writer);
  serd_env_free(env);
}

static void
test_write_bad_statement(void)
{
  SerdEnv* const env = serd_env_new(NULL);
  assert(env);

  SerdChunk         chunk  = {NULL, 0};
  SerdWriter* const writer = serd_writer_new(
    SERD_TURTLE, (SerdStyle)0, env, NULL, serd_chunk_sink, &chunk);
  assert(writer);

  SerdNode s = serd_node_from_string(SERD_URI, USTR("http://example.org/s"));
  SerdNode p = serd_node_from_string(SERD_URI, USTR("http://example.org/p"));
  SerdNode o = serd_node_from_string(SERD_URI, USTR("http://example.org/o"));
  SerdNode l = serd_node_from_string(SERD_LITERAL, USTR("lang"));

  assert(serd_writer_write_statement(
           writer,
           (SerdStatementFlags)(SERD_ANON_S_BEGIN | SERD_LIST_S_BEGIN),
           NULL,
           &s,
           &p,
           &o,
           NULL,
           NULL) == SERD_ERR_BAD_ARG);

  assert(serd_writer_write_statement(
           writer,
           (SerdStatementFlags)(SERD_EMPTY_S | SERD_LIST_S_BEGIN),
           NULL,
           &s,
           &p,
           &o,
           NULL,
           NULL) == SERD_ERR_BAD_ARG);

  assert(serd_writer_write_statement(
           writer,
           (SerdStatementFlags)(SERD_ANON_O_BEGIN | SERD_LIST_O_BEGIN),
           NULL,
           &s,
           &p,
           &o,
           NULL,
           NULL) == SERD_ERR_BAD_ARG);

  assert(serd_writer_write_statement(
           writer,
           (SerdStatementFlags)(SERD_EMPTY_O | SERD_LIST_O_BEGIN),
           NULL,
           &s,
           &p,
           &o,
           NULL,
           NULL) == SERD_ERR_BAD_ARG);

  assert(serd_writer_write_statement(writer, 0U, NULL, &s, &p, &o, &o, &l) ==
         SERD_ERR_BAD_ARG);

  serd_writer_free(writer);
  serd_env_free(env);
}

static void
check_pname_escape(const char* const lname, const char* const expected)
{
  SerdEnv* const env   = serd_env_new(NULL);
  SerdChunk      chunk = {NULL, 0};

  SerdWriter* const writer = serd_writer_new(SERD_TURTLE,
                                             (SerdStyle)SERD_STYLE_CURIED,
                                             env,
                                             NULL,
                                             serd_chunk_sink,
                                             &chunk);
  assert(writer);

  static const char* const prefix     = NS_EG;
  const size_t             prefix_len = strlen(prefix);

  serd_env_set_prefix_from_strings(env, USTR("eg"), USTR(NS_EG));

  SerdNode s = serd_node_from_string(SERD_URI, USTR(NS_EG "s"));
  SerdNode p = serd_node_from_string(SERD_URI, USTR(NS_EG "p"));

  char* const uri = (char*)calloc(1, prefix_len + strlen(lname) + 1);
  assert(uri);
  memcpy(uri, prefix, prefix_len + 1);
  memcpy(uri + prefix_len, lname, strlen(lname) + 1);

  SerdNode o = serd_node_from_string(SERD_URI, USTR(uri));

  assert(
    !serd_writer_write_statement(writer, 0U, NULL, &s, &p, &o, NULL, NULL));
  assert(!serd_writer_finish(writer));
  free(uri);

  uint8_t* const out = serd_chunk_sink_finish(&chunk);
  assert(out);
  assert(!strcmp((const char*)out, expected));
  serd_free(out);

  serd_writer_free(writer);
  serd_env_free(env);
}

static void
test_write_pname_escapes(void)
{
  // Check that '.' is escaped only at the start and end
  check_pname_escape(".xyz", "eg:s\n\teg:p eg:\\.xyz .\n");
  check_pname_escape("w.yz", "eg:s\n\teg:p eg:w.yz .\n");
  check_pname_escape("wx.z", "eg:s\n\teg:p eg:wx.z .\n");
  check_pname_escape("wxy.", "eg:s\n\teg:p eg:wxy\\. .\n");

  // Check that ':' is not escaped anywhere
  check_pname_escape(":xyz", "eg:s\n\teg:p eg::xyz .\n");
  check_pname_escape("w:yz", "eg:s\n\teg:p eg:w:yz .\n");
  check_pname_escape("wx:z", "eg:s\n\teg:p eg:wx:z .\n");
  check_pname_escape("wxy:", "eg:s\n\teg:p eg:wxy: .\n");

  // Check that special characters like '~' are escaped everywhere
  check_pname_escape("~xyz", "eg:s\n\teg:p eg:\\~xyz .\n");
  check_pname_escape("w~yz", "eg:s\n\teg:p eg:w\\~yz .\n");
  check_pname_escape("wx~z", "eg:s\n\teg:p eg:wx\\~z .\n");
  check_pname_escape("wxy~", "eg:s\n\teg:p eg:wxy\\~ .\n");

  // Check that out of range multi-byte characters are escaped everywhere
  static const char first_escape[] = {(char)0xC3U, (char)0xB7U, 'y', 'z', 0};
  static const char mid_escape[]   = {'w', (char)0xC3U, (char)0xB7U, 'z', 0};
  static const char last_escape[]  = {'w', 'x', (char)0xC3U, (char)0xB7U, 0};
  check_pname_escape((const char*)first_escape, "eg:s\n\teg:p eg:%C3%B7yz .\n");
  check_pname_escape((const char*)mid_escape, "eg:s\n\teg:p eg:w%C3%B7z .\n");
  check_pname_escape((const char*)last_escape, "eg:s\n\teg:p eg:wx%C3%B7 .\n");
}

int
main(void)
{
  test_write_long_literal();
  test_write_nested_anon();
  test_writer_cleanup();
  test_write_bad_anon_stack();
  test_strict_write();
  test_write_error();
  test_chunk_sink();
  test_write_nothing_node();
  test_write_bad_statement();
  test_write_pname_escapes();

  return 0;
}

#undef NS_EG
