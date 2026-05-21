// Copyright 2011-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "read_utf8.h"

#include "reader.h"
#include "string_utils.h"

#include <serd/serd.h>

#include <stdio.h>

enum { MAX_UTF8_BYTES = 4U };

static SerdStatus
bad_byte(SerdReader* const reader, const char* const kind, const uint8_t c)
{
  return r_err(reader, SERD_ERR_BAD_TEXT, "bad %s byte 0x%X\n", kind, c);
}

static SerdStatus
read_utf8_continuation_bytes(SerdReader* const reader,
                             uint8_t           bytes[MAX_UTF8_BYTES],
                             uint8_t* const    size,
                             const uint8_t     lead)
{
  if (!(*size = utf8_num_bytes(lead))) {
    return bad_byte(reader, "UTF-8 leading", lead);
  }

  bytes[0] = lead;
  for (uint8_t i = 1U; i < *size; ++i) {
    const int b = peek_byte(reader);
    if (b == EOF) {
      return r_err(reader, SERD_ERR_BAD_SYNTAX, "unexpected end of input\n");
    }

    const uint8_t byte = (uint8_t)b;
    if (!is_utf8_continuation(byte)) {
      return bad_byte(reader, "UTF-8 continuation", byte);
    }

    skip_byte(reader, b);
    bytes[i] = byte;
  }

  return SERD_SUCCESS;
}

SerdStatus
read_utf8_continuation(SerdReader* const reader,
                       const Ref         dest,
                       const uint8_t     lead)
{
  uint8_t size                  = 0U;
  uint8_t bytes[MAX_UTF8_BYTES] = {lead, 0U, 0U, 0U};

  SerdStatus st = read_utf8_continuation_bytes(reader, bytes, &size, lead);
  if (st) {
    return reader->strict ? st : push_bytes(reader, dest, replacement_char, 3);
  }

  return push_bytes(reader, dest, bytes, size);
}

SerdStatus
read_utf8_code_point(SerdReader* const reader,
                     const Ref         dest,
                     uint32_t* const   code,
                     const uint8_t     lead)
{
  uint8_t size                  = 0U;
  uint8_t bytes[MAX_UTF8_BYTES] = {lead, 0U, 0U, 0U};

  *code = 0U;

  skip_byte(reader, lead);

  SerdStatus st = read_utf8_continuation_bytes(reader, bytes, &size, lead);
  if (st) {
    return reader->strict ? st : push_bytes(reader, dest, replacement_char, 3);
  }

  if (!(st = push_bytes(reader, dest, bytes, size))) {
    *code = parse_counted_utf8_char(bytes, size);
  }

  return st;
}
