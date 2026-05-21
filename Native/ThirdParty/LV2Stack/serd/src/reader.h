// Copyright 2011-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef SERD_SRC_READER_H
#define SERD_SRC_READER_H

#include "attributes.h"
#include "byte_source.h"
#include "stack.h"

#include <serd/serd.h>

#include <assert.h>
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#ifdef SERD_STACK_CHECK
#  define SERD_STACK_ASSERT_TOP(reader, ref) \
    assert(ref == reader->allocs[reader->n_allocs - 1])
#else
#  define SERD_STACK_ASSERT_TOP(reader, ref)
#endif

/* Reference to a node in the stack (we can not use pointers since the
   stack may be reallocated, invalidating any pointers to elements).
*/
typedef size_t Ref;

typedef struct {
  Ref                 graph;
  Ref                 subject;
  Ref                 predicate;
  Ref                 object;
  Ref                 datatype;
  Ref                 lang;
  SerdStatementFlags* flags;
} ReadContext;

struct SerdReaderImpl {
  void* handle;
  void (*free_handle)(void* ptr);
  SerdBaseSink      base_sink;
  SerdPrefixSink    prefix_sink;
  SerdStatementSink statement_sink;
  SerdEndSink       end_sink;
  SerdErrorSink     error_sink;
  void*             error_handle;
  Ref               rdf_first;
  Ref               rdf_rest;
  Ref               rdf_nil;
  SerdNode          default_graph;
  SerdByteSource    source;
  SerdStack         stack;
  SerdSyntax        syntax;
  unsigned          next_id;
  uint8_t*          buf;
  uint8_t*          bprefix;
  size_t            bprefix_len;
  bool              strict; ///< True iff strict parsing
  bool              seen_genid;
#ifdef SERD_STACK_CHECK
  Ref*   allocs;   ///< Stack of push offsets
  size_t n_allocs; ///< Number of stack pushes
#endif
};

SERD_LOG_FUNC(3, 4)
SerdStatus
r_err(SerdReader* reader, SerdStatus st, const char* fmt, ...);

SerdStatus
r_err_char(SerdReader* reader, const char* kind, int c);

Ref
push_node_padded(SerdReader* reader,
                 size_t      maxlen,
                 SerdType    type,
                 const char* str,
                 size_t      n_bytes);

Ref
push_node(SerdReader* reader, SerdType type, const char* str, size_t n_bytes);

SERD_PURE_FUNC size_t
genid_size(const SerdReader* reader);

Ref
blank_id(SerdReader* reader);

void
set_blank_id(SerdReader* reader, Ref ref, size_t buf_size);

SerdNode*
deref(SerdReader* reader, Ref ref);

bool
pop_last_node_char(SerdReader* reader, SerdNode* node);

Ref
pop_node(SerdReader* reader, Ref ref);

SerdStatus
emit_statement(SerdReader* reader, ReadContext ctx, Ref o, Ref d, Ref l);

SerdStatus
read_n3_statement(SerdReader* reader);

SerdStatus
read_nquads_statement(SerdReader* reader);

SerdStatus
read_nquadsDoc(SerdReader* reader);

SerdStatus
read_turtleTrigDoc(SerdReader* reader);

static inline int
peek_byte(SerdReader* const reader)
{
  SerdByteSource* source = &reader->source;

  return source->eof ? -1 : (int)source->read_buf[source->read_head];
}

static inline SerdStatus
skip_byte(SerdReader* const reader, const int byte)
{
  (void)byte;

  assert(peek_byte(reader) == byte);

  return serd_byte_source_advance(&reader->source);
}

SERD_NODISCARD static inline int
eat_byte_safe(SerdReader* const reader, const int byte)
{
  (void)byte;

  assert(peek_byte(reader) == byte);

  serd_byte_source_advance(&reader->source);
  return byte;
}

SERD_NODISCARD static inline SerdStatus
eat_byte_check(SerdReader* const reader, const int byte)
{
  const int c = peek_byte(reader);
  if (c != byte) {
    return r_err(reader, SERD_ERR_BAD_SYNTAX, "expected '%c'\n", byte);
  }

  const SerdStatus st = serd_byte_source_advance(&reader->source);
  return (st > SERD_FAILURE) ? st : SERD_SUCCESS;
}

static inline SerdStatus
eat_string(SerdReader* const reader, const char* const str, const unsigned n)
{
  for (unsigned i = 0; i < n; ++i) {
    if (eat_byte_check(reader, ((const uint8_t*)str)[i])) {
      return SERD_ERR_BAD_SYNTAX;
    }
  }
  return SERD_SUCCESS;
}

static inline SerdStatus
push_byte(SerdReader* const reader, const Ref ref, const int c)
{
  assert(c >= 0);
  SERD_STACK_ASSERT_TOP(reader, ref);

  uint8_t* const  s    = (uint8_t*)serd_stack_push(&reader->stack, 1);
  SerdNode* const node = (SerdNode*)(reader->stack.buf + ref);

  ++node->n_bytes;
  if (!(c & 0x80)) { // Starts with 0 bit, start of new character
    ++node->n_chars;
  }

  *(s - 1) = (uint8_t)c;
  *s       = '\0';
  return SERD_SUCCESS;
}

static inline SerdStatus
push_bytes(SerdReader* const    reader,
           const Ref            ref,
           const uint8_t* const bytes,
           const unsigned       len)
{
  SerdStatus st = SERD_SUCCESS;
  for (unsigned i = 0; !st && i < len; ++i) {
    st = push_byte(reader, ref, bytes[i]);
  }
  return st;
}

#endif // SERD_SRC_READER_H
