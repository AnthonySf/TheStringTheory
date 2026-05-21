// Copyright 2011-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "attributes.h"
#include "byte_sink.h"
#include "serd_internal.h"
#include "stack.h"
#include "string_utils.h"
#include "try.h"
#include "turtle.h"
#include "uri_utils.h"
#include "warnings.h"

#include <serd/serd.h>

#include <assert.h>
#include <errno.h>
#include <stdarg.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef enum {
  CTX_NAMED, ///< Normal non-anonymous context
  CTX_BLANK, ///< Anonymous blank node
  CTX_LIST,  ///< Anonymous list
} ContextType;

typedef enum {
  FIELD_NONE,
  FIELD_SUBJECT,
  FIELD_PREDICATE,
  FIELD_OBJECT,
  FIELD_GRAPH,
} Field;

typedef struct {
  ContextType type;
  bool        comma_indented;
  SerdNode    graph;
  SerdNode    subject;
  SerdNode    predicate;
} WriteContext;

/// A status for an operation that reads/writes variable numbers of bytes
typedef struct {
  SerdStatus status;
  size_t     read_count;
  size_t     write_count;
} VariableResult;

typedef enum {
  SEP_NONE,     ///< Sentinel after "nothing"
  SEP_STOP,     ///< End of a subject or directive ('.')
  SEP_END_P,    ///< End of a predicate (';')
  SEP_END_O_N,  ///< End of an object before a named one (',')
  SEP_END_O_NA, ///< End of named object before an anonymous one (',')
  SEP_END_O_AA, ///< End of anonymous object before another (',')
  SEP_S_P,      ///< Between a subject and predicate (whitespace)
  SEP_P_O,      ///< Between a predicate and object (whitespace)
  SEP_GRAPH_L,  ///< Start of graph ('{')
  SEP_GRAPH_R,  ///< End of graph ('}')
  SEP_ANON_L,   ///< Start of anonymous node ('[')
  SEP_ANON_R,   ///< End of anonymous node (']')
  SEP_LIST_L,   ///< Start of list ('(')
  SEP_LIST_SEP, ///< List separator (whitespace)
  SEP_LIST_R,   ///< End of list (')')
} Sep;

typedef enum {
  PRE_SPACE  = (1U << 0U), ///< Leading space
  PRE_LINE   = (1U << 1U), ///< Leading newline
  POST_SPACE = (1U << 2U), ///< Trailing space
  POST_LINE  = (1U << 3U), ///< Trailing newline
} SepFlag;

typedef struct {
  char    sep;        ///< Sep character
  int8_t  indent : 4; ///< Indent delta
  uint8_t flags : 4;  ///< Whitespace flags
} SepRule;

#define NIL '\0'

static const SepRule rules[] = {
  {NIL, +0, 0U},
  {'.', +0, PRE_SPACE},
  {';', +0, PRE_SPACE | POST_LINE},
  {',', +0, PRE_SPACE | POST_LINE},
  {',', +0, PRE_SPACE | POST_LINE},
  {',', +0, PRE_SPACE | POST_SPACE},
  {NIL, +1, POST_LINE},
  {' ', +0, 0U},
  {'{', +1, PRE_SPACE | POST_LINE},
  {'}', -1, POST_LINE},
  {'[', +1, 0U},
  {']', -1, PRE_LINE},
  {'(', +1, POST_LINE},
  {NIL, +0, PRE_LINE},
  {')', -1, PRE_LINE},
};

#undef NIL

struct SerdWriterImpl {
  SerdSyntax    syntax;
  SerdStyle     style;
  SerdEnv*      env;
  SerdNode      root_node;
  SerdURI       root_uri;
  SerdURI       base_uri;
  SerdStack     anon_stack;
  SerdByteSink  byte_sink;
  SerdErrorSink error_sink;
  void*         error_handle;
  WriteContext  context;
  uint8_t*      bprefix;
  size_t        bprefix_len;
  Sep           last_sep;
  int           indent;
};

typedef enum { WRITE_STRING, WRITE_LONG_STRING } TextContext;
typedef enum { RESET_GRAPH = 1U << 0U, RESET_INDENT = 1U << 1U } ResetFlag;

typedef bool (*BytePredicate)(uint8_t) SERD_NODISCARD;

SERD_NODISCARD static bool
supports_abbrev(const SerdWriter* const writer)
{
  return writer->syntax == SERD_TURTLE || writer->syntax == SERD_TRIG;
}

static SerdStatus
free_context(WriteContext* const ctx)
{
  serd_node_free(&ctx->graph);
  serd_node_free(&ctx->subject);
  serd_node_free(&ctx->predicate);
  ctx->graph.type     = SERD_NOTHING;
  ctx->subject.type   = SERD_NOTHING;
  ctx->predicate.type = SERD_NOTHING;
  return SERD_SUCCESS;
}

SERD_LOG_FUNC(3, 4)
static SerdStatus
w_err(SerdWriter* const writer, const SerdStatus st, const char* const fmt, ...)
{
  /* TODO: This results in errors with no file information, which is not
     helpful when re-serializing a file (particularly for "undefined
     namespace prefix" errors.  The statement sink API needs to be changed to
     add a Cursor parameter so the source can notify the writer of the
     statement origin for better error reporting. */

  va_list args; // NOLINT(cppcoreguidelines-init-variables)
  va_start(args, fmt);
  const SerdError e = {st, (const uint8_t*)"", 0, 0, fmt, &args};
  serd_error(writer->error_sink, writer->error_handle, &e);
  va_end(args);
  return st;
}

static void
copy_node(SerdNode* const dst, const SerdNode* const src)
{
  const size_t   new_size = src->n_bytes + 1U;
  uint8_t* const new_buf  = (uint8_t*)realloc((char*)dst->buf, new_size);
  if (new_buf) {
    dst->buf     = new_buf;
    dst->n_bytes = src->n_bytes;
    dst->n_chars = src->n_chars;
    dst->flags   = src->flags;
    dst->type    = src->type;
    memcpy((char*)dst->buf, src->buf, new_size);
  }
}

static void
push_context(SerdWriter* const writer,
             const ContextType type,
             const SerdNode    graph,
             const SerdNode    subject,
             const SerdNode    predicate)
{
  // Push the current context to the stack
  void* const top = serd_stack_push(&writer->anon_stack, sizeof(WriteContext));
  *(WriteContext*)top = writer->context;

  // Update the current context
  const WriteContext current = {type, false, graph, subject, predicate};
  writer->context            = current;
}

static void
pop_context(SerdWriter* const writer)
{
  // Replace the current context with the top of the stack
  free_context(&writer->context);
  writer->context =
    *(WriteContext*)(writer->anon_stack.buf + writer->anon_stack.size -
                     sizeof(WriteContext));

  // Pop the top of the stack away
  serd_stack_pop(&writer->anon_stack, sizeof(WriteContext));
}

SERD_NODISCARD static size_t
sink(const void* const buf, const size_t len, SerdWriter* const writer)
{
  const size_t written = serd_byte_sink_write(buf, len, &writer->byte_sink);
  if (written != len) {
    if (errno) {
      const char* const message = strerror(errno);
      w_err(writer, SERD_ERR_BAD_WRITE, "write error (%s)\n", message);
    } else {
      w_err(writer, SERD_ERR_BAD_WRITE, "write error\n");
    }
  }

  return written;
}

SERD_NODISCARD static SerdStatus
esink(const void* const buf, const size_t len, SerdWriter* const writer)
{
  return sink(buf, len, writer) == len ? SERD_SUCCESS : SERD_ERR_BAD_WRITE;
}

/// Variable sink that returns an updated variable result
SERD_NODISCARD static VariableResult
vsink(SerdWriter* const    writer,
      const VariableResult vr,
      const size_t         len,
      const void* const    buf)
{
  const size_t         n  = sink(buf, len, writer);
  const SerdStatus     st = (n == len) ? SERD_SUCCESS : SERD_ERR_BAD_WRITE;
  const VariableResult r  = {st, vr.read_count, vr.write_count + n};
  return r;
}

SERD_NODISCARD static SerdStatus
write_hex_byte(SerdWriter* const writer, const unsigned byte)
{
  static const char hex_chars[] = "0123456789ABCDEF";

  const char digits[2] = {hex_chars[byte >> 4U], hex_chars[byte & 0x0FU]};

  return esink(digits, 2, writer);
}

static VariableResult
write_UCHAR(SerdWriter* const writer, const uint8_t* const utf8)
{
  uint8_t        c_size = 0U;
  const uint32_t c      = parse_utf8_char(utf8, &c_size);
  VariableResult vr     = {SERD_SUCCESS, c_size, 0U};

  vr.read_count = c_size;
  if (vr.read_count == 0U) {
    vr.status =
      w_err(writer, SERD_ERR_BAD_TEXT, "invalid UTF-8 start: %X\n", utf8[0]);
  } else if (c <= 0xFFFF) {
    // Write short (4 digit) escape
    if (!(vr = vsink(writer, vr, 2, "\\u")).status &&
        !(vr.status = write_hex_byte(writer, (c & 0xFF00U) >> 8U)) &&
        !(vr.status = write_hex_byte(writer, (c & 0x00FFU)))) {
      vr.write_count += 4U;
    }
  } else {
    // Write long (8 digit) escape
    if (!(vr = vsink(writer, vr, 2, "\\U")).status &&
        !(vr.status = write_hex_byte(writer, (c & 0xFF000000U) >> 24U)) &&
        !(vr.status = write_hex_byte(writer, (c & 0x00FF0000U) >> 16U)) &&
        !(vr.status = write_hex_byte(writer, (c & 0x0000FF00U) >> 8U)) &&
        !(vr.status = write_hex_byte(writer, (c & 0x000000FFU)))) {
      vr.write_count += 8U;
    }
  }

  return vr;
}

static VariableResult
write_text_character(SerdWriter* const writer, const uint8_t* const utf8)
{
  const uint8_t c = utf8[0];
  if ((writer->style & SERD_STYLE_ASCII) || c < 0x20U || c == 0x7FU) {
    // Write ASCII-compatible UCHAR escape like "\u1234"
    return write_UCHAR(writer, utf8);
  }

  // Parse the leading byte to get the UTF-8 encoding size
  const size_t read_count = utf8_num_bytes(c);
  if (!read_count) {
    const VariableResult result = {SERD_ERR_BAD_TEXT, 0U, 0U};
    return result;
  }

  // Write the UTF-8 encoding directly to the output
  const size_t write_count = sink(utf8, read_count, writer);
  SerdStatus   st          = SERD_SUCCESS;
  if (write_count != read_count) {
    st = SERD_ERR_BAD_WRITE;
  }

  const VariableResult result = {st, read_count, write_count};
  return result;
}

static VariableResult
write_uri_character(SerdWriter* const writer, const uint8_t* const utf8)
{
  const uint8_t c = utf8[0];
  if (!(c & 0x80U) || (writer->style & SERD_STYLE_ASCII)) {
    return write_UCHAR(writer, utf8);
  }

  // Parse the leading byte to get the UTF-8 encoding size
  VariableResult result = {SERD_ERR_BAD_TEXT, 0U, 0U};
  if ((result.read_count = utf8_num_bytes(c))) {
    // Write the UTF-8 encoding directly to the output
    result.write_count = sink(utf8, result.read_count, writer);

    result.status = (result.write_count == result.read_count)
                      ? SERD_SUCCESS
                      : SERD_ERR_BAD_WRITE;
  }

  return result;
}

SERD_NODISCARD static bool
uri_must_escape(const uint8_t c)
{
  return (c == '"') || (c == '<') || (c == '>') || (c == '\\') || (c == '^') ||
         (c == '`') || in_range(c, '{', '}') || !in_range(c, 0x21, 0x7E);
}

static size_t
next_text_index(const uint8_t* const utf8,
                const size_t         begin,
                const size_t         end,
                const BytePredicate  predicate)
{
  size_t i = begin;
  while (i < end && !predicate(utf8[i])) {
    ++i;
  }
  return i;
}

static VariableResult
write_uri_text(SerdWriter* const    writer,
               const uint8_t* const utf8,
               const size_t         n_bytes)
{
  VariableResult result = {SERD_SUCCESS, 0U, 0U};

  for (size_t i = 0; i < n_bytes;) {
    // Write leading chunk as a single fast bulk write
    const size_t j = next_text_index(utf8, i, n_bytes, uri_must_escape);
    result.status  = esink(&utf8[i], j - i, writer);
    if ((i = j) == n_bytes) {
      break; // Reached end
    }

    // Write character (escape or UTF-8)
    const VariableResult r = write_uri_character(writer, utf8 + i);
    i += r.read_count;
    result.write_count += r.write_count;
    if (r.status && (writer->style & SERD_STYLE_STRICT)) {
      result.status = r.status;
      break;
    }

    if (!r.read_count) {
      // Corrupt input, write percent-encoded bytes and scan to next start
      for (; !result.status && i < n_bytes && !is_utf8_leading(utf8[i]); ++i) {
        result = vsink(writer, result, 1, "%");
        if (!(result.status = write_hex_byte(writer, utf8[i]))) {
          result.write_count += 2U;
        }
      }
    }
  }

  return result;
}

SERD_NODISCARD static SerdStatus
ewrite_uri(SerdWriter* const    writer,
           const uint8_t* const utf8,
           const size_t         n_bytes)
{
  const VariableResult r = write_uri_text(writer, utf8, n_bytes);

  return (r.status == SERD_ERR_BAD_WRITE || (writer->style & SERD_STYLE_STRICT))
           ? r.status
           : SERD_SUCCESS;
}

SERD_NODISCARD static SerdStatus
write_utf8_percent_escape(SerdWriter* const writer,
                          const char* const utf8,
                          const size_t      n_bytes)
{
  SerdStatus st = SERD_SUCCESS;

  for (size_t i = 0U; i < n_bytes; ++i) {
    TRY(st, esink("%", 1, writer));
    TRY(st, write_hex_byte(writer, (uint8_t)utf8[i]));
  }

  return st;
}

SERD_NODISCARD static SerdStatus
write_PN_LOCAL_ESC(SerdWriter* const writer, const char c)
{
  const char buf[2] = {'\\', c};

  return esink(buf, sizeof(buf), writer);
}

SERD_NODISCARD static SerdStatus
write_lname_escape(SerdWriter* const writer,
                   const char* const utf8,
                   const size_t      n_bytes)
{
  return is_PN_LOCAL_ESC(utf8[0])
           ? write_PN_LOCAL_ESC(writer, utf8[0])
           : write_utf8_percent_escape(writer, utf8, n_bytes);
}

SERD_NODISCARD static SerdStatus
write_lname(SerdWriter* const    writer,
            const uint8_t* const utf8,
            const size_t         n_bytes)
{
  SerdStatus st = SERD_SUCCESS;
  if (!n_bytes) {
    return st;
  }

  /* The grammar for prefixed names is unfortunately complicated.  We need to
     handle the first character separately, and take care to only escape where
     necessary. */

  // Write first character
  uint8_t   first_size = 0U;
  const int first      = (int)parse_utf8_char(utf8, &first_size);
  if (is_PN_CHARS_U(first) || first == ':' || is_digit(first)) {
    TRY(st, esink(utf8, first_size, writer));
  } else {
    TRY(st, write_lname_escape(writer, (const char*)utf8, first_size));
  }

  // Write middle and last characters
  for (size_t i = first_size; i < n_bytes;) {
    uint8_t   c_size = 0U;
    const int c      = (int)parse_utf8_char(utf8 + i, &c_size);

    if (is_PN_CHARS(c) || c == ':' || (c == '.' && (i + 1U < n_bytes))) {
      TRY(st, esink(&utf8[i], c_size, writer));
    } else {
      TRY(st, write_lname_escape(writer, (const char*)&utf8[i], c_size));
    }

    i += c_size;
  }

  return st;
}

SERD_NODISCARD static size_t
write_long_string_escape(SerdWriter* const writer, const char c)
{
  switch (c) {
  case '\\':
    return sink("\\\\", 2, writer);

  case '\b':
    return sink("\\b", 2, writer);

  case '\n':
  case '\r':
  case '\t':
  case '\f':
    return sink(&c, 1, writer); // Write character as-is

  case '"':
    return sink("\\\"", 2, writer);

  default:
    break;
  }

  return 0;
}

SERD_NODISCARD static size_t
write_short_string_escape(SerdWriter* const writer, const char c)
{
  switch (c) {
  case '\\':
    return sink("\\\\", 2, writer);
  case '\n':
    return sink("\\n", 2, writer);
  case '\r':
    return sink("\\r", 2, writer);
  case '\t':
    return sink("\\t", 2, writer);
  case '"':
    return sink("\\\"", 2, writer);
  default:
    break;
  }

  if (writer->syntax == SERD_TURTLE) {
    switch (c) {
    case '\b':
      return sink("\\b", 2, writer);
    case '\f':
      return sink("\\f", 2, writer);
    default:
      break;
    }
  }

  return 0;
}

SERD_NODISCARD static bool
text_must_escape(const uint8_t c)
{
  return c == '\\' || c == '"' || !in_range(c, 0x20, 0x7E);
}

static VariableResult
write_literal_character(SerdWriter* const    writer,
                        const uint8_t* const utf8,
                        const size_t         i,
                        const size_t         n_bytes)
{
  VariableResult vr = {SERD_SUCCESS, 0U, 0U};

  vr = write_text_character(writer, utf8 + i);
  if (!vr.read_count && !(writer->style & SERD_STYLE_STRICT)) {
    // Corrupt input, write replacement char and scan to the next start
    vr.status     = esink(replacement_char, sizeof(replacement_char), writer);
    vr.read_count = next_text_index(utf8, i, n_bytes, is_utf8_leading);
  }

  return vr;
}

SERD_NODISCARD static SerdStatus
write_short_text(SerdWriter* const    writer,
                 const uint8_t* const utf8,
                 const size_t         n_bytes)
{
  VariableResult vr = {SERD_SUCCESS, 0U, 0U};
  for (size_t i = 0; !vr.status && i < n_bytes;) {
    // Write leading chunk as a single fast bulk write
    const size_t j = next_text_index(utf8, i, n_bytes, text_must_escape);
    vr.status      = esink(&utf8[i], j - i, writer);
    if (vr.status || ((i = j) == n_bytes)) {
      break; // Error or reached end
    }

    // Try to write character as a special escape
    const size_t escape_len = write_short_string_escape(writer, (char)utf8[i]);
    if (!escape_len) {
      // No special escape for this character, write full Unicode escape
      vr = write_literal_character(writer, utf8, i, n_bytes);
      i += vr.read_count;
    } else {
      ++i;
    }
  }

  return vr.status;
}

SERD_NODISCARD static SerdStatus
write_long_text(SerdWriter* const    writer,
                const uint8_t* const utf8,
                const size_t         n_bytes)
{
  size_t         n_quotes = 0;
  VariableResult vr       = {SERD_SUCCESS, 0U, 0U};
  for (size_t i = 0; !vr.status && i < n_bytes;) {
    if (utf8[i] != '"') {
      n_quotes = 0;
    }

    // Write leading chunk as a single fast bulk write
    const size_t j = next_text_index(utf8, i, n_bytes, text_must_escape);
    vr.status      = esink(&utf8[i], j - i, writer);
    if (vr.status || ((i = j) == n_bytes)) {
      break; // Error or reached end
    }

    // Update consecutive quote count
    n_quotes = (utf8[i] == '"') ? (n_quotes + 1U) : 0;

    // Try to write character as a special escape
    const char   c         = (char)utf8[i];
    const bool   raw_quote = (n_quotes && n_quotes < 3 && i + 1U != n_bytes);
    const size_t escape_len =
      raw_quote ? sink(&c, 1, writer) : write_long_string_escape(writer, c);

    if (!escape_len) {
      // No special escape for this character, write full Unicode escape
      vr = write_literal_character(writer, utf8, i, n_bytes);
      i += vr.read_count;
    } else {
      ++i;
    }
  }

  return vr.status;
}

typedef struct {
  SerdWriter* writer;
  SerdStatus  status;
} UriSinkContext;

SERD_NODISCARD static size_t
uri_sink(const void* const buf, const size_t len, void* const stream)
{
  UriSinkContext* const context = (UriSinkContext*)stream;
  SerdWriter* const     writer  = context->writer;
  const VariableResult  r = write_uri_text(writer, (const uint8_t*)buf, len);

  context->status = r.status;
  return r.write_count;
}

SERD_NODISCARD static SerdStatus
write_newline(SerdWriter* const writer)
{
  SerdStatus st = SERD_SUCCESS;

  TRY(st, esink("\n", 1, writer));
  for (int i = 0; i < writer->indent; ++i) {
    TRY(st, esink("\t", 1, writer));
  }

  return st;
}

SERD_NODISCARD static SerdStatus
write_space(SerdWriter* const writer, const uint8_t flags)
{
  return (flags & PRE_LINE)    ? write_newline(writer)
         : (flags & PRE_SPACE) ? esink(" ", 1, writer)
                               : SERD_SUCCESS;
}

SERD_NODISCARD static SerdStatus
write_sep(SerdWriter* const writer, const Sep sep)
{
  SerdStatus           st   = SERD_SUCCESS;
  const SepRule* const rule = &rules[sep];

  // Adjust indent, but tolerate if it would become negative
  if (rule->indent && (rule->flags & (PRE_LINE | POST_LINE))) {
    writer->indent += rule->indent;
  }

  // Adjust indentation for object comma if necessary
  if (sep == SEP_END_O_N && !writer->context.comma_indented) {
    ++writer->indent;
    writer->context.comma_indented = true;
  } else if (sep == SEP_END_P && writer->context.comma_indented) {
    --writer->indent;
    writer->context.comma_indented = false;
  }

  // Write newline or space before separator if necessary
  TRY(st, write_space(writer, rule->flags));

  // Write actual separator string
  if (rule->sep) {
    TRY(st, esink(&rule->sep, 1, writer));
  }

  // Write newline after separator if necessary
  TRY(st, write_space(writer, rule->flags >> 2U));

  // Reset context and write a blank line after ends of subjects
  if (sep == SEP_STOP) {
    writer->indent                 = writer->context.graph.type ? 1 : 0;
    writer->context.comma_indented = false;
    TRY(st, esink("\n", 1, writer));
  }

  writer->last_sep = sep;
  return st;
}

static void
free_anon_stack(SerdWriter* const writer)
{
  while (!serd_stack_is_empty(&writer->anon_stack)) {
    pop_context(writer);
  }
}

static SerdStatus
reset_context(SerdWriter* const writer, const unsigned flags)
{
  free_anon_stack(writer);

  if (flags & RESET_GRAPH) {
    writer->context.graph.type = SERD_NOTHING;
  }

  if (flags & RESET_INDENT) {
    writer->indent = 0;
  }

  writer->context.type           = CTX_NAMED;
  writer->context.subject.type   = SERD_NOTHING;
  writer->context.predicate.type = SERD_NOTHING;
  writer->context.comma_indented = false;
  return SERD_SUCCESS;
}

// Return the name of the XSD datatype referred to by `datatype`, if any
static const char*
get_xsd_name(const SerdEnv* const env, const SerdNode* const datatype)
{
  const char* const datatype_str = (const char*)datatype->buf;

  if (datatype->type == SERD_URI &&
      (!strncmp(datatype_str, NS_XSD, sizeof(NS_XSD) - 1))) {
    return datatype_str + sizeof(NS_XSD) - 1U;
  }

  if (datatype->type == SERD_CURIE) {
    SerdChunk prefix = {NULL, 0};
    SerdChunk suffix = {NULL, 0};
    // We can be a bit lazy/presumptive here due to grammar limitations
    if (!serd_env_expand(env, datatype, &prefix, &suffix)) {
      if (!strcmp((const char*)prefix.buf, NS_XSD)) {
        return (const char*)suffix.buf;
      }
    }
  }

  return "";
}

static SerdStatus
write_IRIREF(SerdWriter* const writer, const SerdNode* const node)
{
  SerdStatus st = SERD_SUCCESS;

  TRY(st, esink("<", 1, writer));

  // Write the string and return early if resolution is disabled
  if (!(writer->style & SERD_STYLE_RESOLVED)) {
    TRY(st, ewrite_uri(writer, node->buf, node->n_bytes));
    return esink(">", 1, writer);
  }

  // Resolve the input URI reference to a (hopefully) absolute URI
  SerdURI in_base_uri;
  SerdURI uri;
  SerdURI abs_uri;
  serd_env_get_base_uri(writer->env, &in_base_uri);
  SERD_DISABLE_NULL_WARNINGS
  serd_uri_parse(node->buf, &uri);
  SERD_RESTORE_WARNINGS
  serd_uri_resolve(&uri, &in_base_uri, &abs_uri);

  // Determine if the absolute URI should be written, or make it relative again
  const bool     rooted = uri_is_under(&writer->base_uri, &writer->root_uri);
  const SerdURI* root   = rooted ? &writer->root_uri : &writer->base_uri;
  UriSinkContext ctx    = {writer, SERD_SUCCESS};
  if (!uri_is_under(&abs_uri, root) || writer->syntax == SERD_NTRIPLES ||
      writer->syntax == SERD_NQUADS) {
    serd_uri_serialise(&abs_uri, uri_sink, &ctx);
  } else {
    serd_uri_serialise_relative(&uri, &writer->base_uri, root, uri_sink, &ctx);
  }

  return esink(">", 1, writer);
}

SERD_NODISCARD static SerdStatus
write_uri_node(SerdWriter* const writer, const SerdNode* const node)
{
  SerdStatus st     = SERD_SUCCESS;
  SerdNode   prefix = SERD_NODE_NULL;
  SerdChunk  suffix = {NULL, 0U};

  const bool has_scheme = serd_uri_string_has_scheme(node->buf);
  if (supports_abbrev(writer)) {
    if (!strcmp((const char*)node->buf, NS_RDF "nil")) {
      return esink("()", 2, writer);
    }

    if (has_scheme && (writer->style & SERD_STYLE_CURIED) &&
        serd_env_qualify(writer->env, node, &prefix, &suffix)) {
      TRY(st, write_lname(writer, prefix.buf, prefix.n_bytes));
      TRY(st, esink(":", 1, writer));
      return write_lname(writer, suffix.buf, suffix.len);
    }
  }

  if (!has_scheme &&
      (writer->syntax == SERD_NTRIPLES || writer->syntax == SERD_NQUADS) &&
      !serd_env_get_base_uri(writer->env, NULL)->buf) {
    return w_err(writer,
                 SERD_ERR_BAD_ARG,
                 "syntax does not support URI reference <%s>\n",
                 node->buf);
  }

  return write_IRIREF(writer, node);
}

SERD_NODISCARD static SerdStatus
write_curie(SerdWriter* const writer, const SerdNode* const node)
{
  SerdChunk  prefix = {NULL, 0};
  SerdChunk  suffix = {NULL, 0};
  SerdStatus st     = SERD_SUCCESS;

  // In fast-and-loose Turtle/TriG mode CURIEs are simply passed through
  const bool fast =
    !(writer->style & (SERD_STYLE_CURIED | SERD_STYLE_RESOLVED));

  if (!supports_abbrev(writer) || !fast) {
    if ((st = serd_env_expand(writer->env, node, &prefix, &suffix))) {
      return w_err(writer, st, "undefined namespace prefix '%s'\n", node->buf);
    }
  }

  if (!supports_abbrev(writer)) {
    TRY(st, esink("<", 1, writer));
    TRY(st, ewrite_uri(writer, prefix.buf, prefix.len));
    TRY(st, ewrite_uri(writer, suffix.buf, suffix.len));
    TRY(st, esink(">", 1, writer));
  } else {
    TRY(st, write_lname(writer, node->buf, node->n_bytes));
  }

  return st;
}

SERD_NODISCARD static SerdStatus
write_iri(SerdWriter* const writer, const SerdNode* const node)
{
  return (node->type == SERD_URI) ? write_uri_node(writer, node)
                                  : write_curie(writer, node);
}

SERD_NODISCARD static SerdStatus
write_literal(SerdWriter* const     writer,
              const SerdNode* const node,
              const SerdNode* const datatype,
              const SerdNode* const lang)
{
  SerdStatus st = SERD_SUCCESS;

  const uint8_t* const node_str = node->buf;
  const size_t         node_len = node->n_bytes;

  if (supports_abbrev(writer) && datatype && datatype->buf) {
    const char* const xsd_name = get_xsd_name(writer->env, datatype);
    if (!strcmp(xsd_name, "boolean") || !strcmp(xsd_name, "integer") ||
        (!strcmp(xsd_name, "decimal") && strchr((const char*)node->buf, '.') &&
         node->buf[node->n_bytes - 1] != '.')) {
      return esink(node->buf, node->n_bytes, writer);
    }
  }

  if (supports_abbrev(writer) &&
      (node->flags & (SERD_HAS_NEWLINE | SERD_HAS_QUOTE))) {
    TRY(st, esink("\"\"\"", 3, writer));
    TRY(st, write_long_text(writer, node_str, node_len));
    st = esink("\"\"\"", 3, writer);
  } else {
    TRY(st, esink("\"", 1, writer));
    TRY(st, write_short_text(writer, node_str, node_len));
    st = esink("\"", 1, writer);
  }
  if (lang && lang->buf) {
    TRY(st, esink("@", 1, writer));
    st = esink(lang->buf, lang->n_bytes, writer);
  } else if (datatype && datatype->buf) {
    TRY(st, esink("^^", 2, writer));
    st = write_iri(writer, datatype);
  }

  return st;
}

SERD_NODISCARD static SerdStatus
write_blank(SerdWriter* const        writer,
            const SerdNode* const    node,
            const Field              field,
            const SerdStatementFlags flags)
{
  SerdStatus st = SERD_SUCCESS;

  if (supports_abbrev(writer)) {
    if ((field == FIELD_SUBJECT && (flags & SERD_ANON_S_BEGIN)) ||
        (field == FIELD_OBJECT && (flags & SERD_ANON_O_BEGIN))) {
      return write_sep(writer, SEP_ANON_L);
    }

    if ((field == FIELD_SUBJECT && (flags & SERD_LIST_S_BEGIN)) ||
        (field == FIELD_OBJECT && (flags & SERD_LIST_O_BEGIN))) {
      return write_sep(writer, SEP_LIST_L);
    }

    if ((field == FIELD_SUBJECT && (flags & SERD_EMPTY_S)) ||
        (field == FIELD_OBJECT && (flags & SERD_EMPTY_O))) {
      return esink("[]", 2, writer);
    }
  }

  TRY(st, esink("_:", 2, writer));
  if (writer->bprefix && !strncmp((const char*)node->buf,
                                  (const char*)writer->bprefix,
                                  writer->bprefix_len)) {
    TRY(st,
        esink(node->buf + writer->bprefix_len,
              node->n_bytes - writer->bprefix_len,
              writer));
  } else {
    TRY(st, esink(node->buf, node->n_bytes, writer));
  }

  return st;
}

SERD_NODISCARD static SerdStatus
write_node(SerdWriter* const        writer,
           const SerdNode* const    node,
           const SerdNode* const    datatype,
           const SerdNode* const    lang,
           const Field              field,
           const SerdStatementFlags flags)
{
  return (node->type == SERD_LITERAL)
           ? write_literal(writer, node, datatype, lang)
         : (node->type == SERD_URI)   ? write_uri_node(writer, node)
         : (node->type == SERD_CURIE) ? write_curie(writer, node)
         : (node->type == SERD_BLANK) ? write_blank(writer, node, field, flags)
                                      : SERD_SUCCESS;
}

static bool
is_resource(const SerdNode* const node)
{
  return node->buf && node->type > SERD_LITERAL;
}

SERD_NODISCARD static SerdStatus
write_pred(SerdWriter* const writer, const SerdNode* const pred)
{
  SerdStatus st =
    (pred->type == SERD_URI && !strcmp((const char*)pred->buf, NS_RDF "type"))
      ? esink("a", 1, writer)
      : write_iri(writer, pred);

  if (!st) {
    st = write_sep(writer, SEP_P_O);
  }

  copy_node(&writer->context.predicate, pred);
  writer->context.comma_indented = false;
  return st;
}

SERD_NODISCARD static SerdStatus
write_list_next(SerdWriter* const        writer,
                const SerdStatementFlags flags,
                const SerdNode* const    predicate,
                const SerdNode* const    object,
                const SerdNode* const    datatype,
                const SerdNode* const    lang)
{
  SerdStatus st = SERD_SUCCESS;

  if (!strcmp((const char*)object->buf, NS_RDF "nil")) {
    TRY(st, write_sep(writer, SEP_LIST_R));
    return SERD_FAILURE;
  }

  if (!strcmp((const char*)predicate->buf, NS_RDF "first")) {
    TRY(st, write_node(writer, object, datatype, lang, FIELD_OBJECT, flags));
  } else {
    TRY(st, write_sep(writer, SEP_LIST_SEP));
  }

  return st;
}

SERD_NODISCARD static SerdStatus
terminate_context(SerdWriter* const writer)
{
  SerdStatus st = SERD_SUCCESS;

  if (writer->context.subject.type) {
    TRY(st, write_sep(writer, SEP_STOP));
  }

  if (writer->context.graph.type) {
    TRY(st, write_sep(writer, SEP_GRAPH_R));
  }

  return st;
}

SerdStatus
serd_writer_write_statement(SerdWriter* const     writer,
                            SerdStatementFlags    flags,
                            const SerdNode* const graph,
                            const SerdNode* const subject,
                            const SerdNode* const predicate,
                            const SerdNode* const object,
                            const SerdNode* const datatype,
                            const SerdNode* const lang)
{
  assert(writer);
  assert(subject);
  assert(predicate);
  assert(object);

  SerdStatus st = SERD_SUCCESS;

  if ((flags & SERD_LIST_O_BEGIN) &&
      !strcmp((const char*)object->buf, NS_RDF "nil")) {
    /* Tolerate LIST_O_BEGIN for "()" objects, even though it doesn't make
       much sense, because older versions handled this gracefully.  Consider
       making this an error in a later major version. */
    flags &= (SerdStatementFlags)~SERD_LIST_O_BEGIN;
  }

  // Refuse to write incoherent statements
  if (!is_resource(subject) || !is_resource(predicate) ||
      object->type == SERD_NOTHING || !object->buf ||
      (datatype && datatype->buf && lang && lang->buf) ||
      ((flags & SERD_ANON_S_BEGIN) && (flags & SERD_LIST_S_BEGIN)) ||
      ((flags & SERD_EMPTY_S) && (flags & SERD_LIST_S_BEGIN)) ||
      ((flags & SERD_ANON_O_BEGIN) && (flags & SERD_LIST_O_BEGIN)) ||
      ((flags & SERD_EMPTY_O) && (flags & SERD_LIST_O_BEGIN))) {
    return SERD_ERR_BAD_ARG;
  }

  // Simple case: write a line of NTriples or NQuads
  if (writer->syntax == SERD_NTRIPLES || writer->syntax == SERD_NQUADS) {
    TRY(st, write_node(writer, subject, NULL, NULL, FIELD_SUBJECT, flags));
    TRY(st, esink(" ", 1, writer));
    TRY(st, write_node(writer, predicate, NULL, NULL, FIELD_PREDICATE, flags));
    TRY(st, esink(" ", 1, writer));
    TRY(st, write_node(writer, object, datatype, lang, FIELD_OBJECT, flags));
    if (writer->syntax == SERD_NQUADS && graph) {
      TRY(st, esink(" ", 1, writer));
      TRY(st, write_node(writer, graph, datatype, lang, FIELD_GRAPH, flags));
    }
    TRY(st, esink(" .\n", 3, writer));
    return SERD_SUCCESS;
  }

  // Separate graphs if necessary
  const SerdNode* const out_graph = writer->syntax == SERD_TRIG ? graph : NULL;
  if ((out_graph && !serd_node_equals(out_graph, &writer->context.graph)) ||
      (!out_graph && writer->context.graph.type)) {
    TRY(st, terminate_context(writer));
    reset_context(writer, RESET_GRAPH | RESET_INDENT);
    TRY(st, write_newline(writer));
    if (out_graph) {
      TRY(st,
          write_node(writer, out_graph, datatype, lang, FIELD_GRAPH, flags));
      TRY(st, write_sep(writer, SEP_GRAPH_L));
      copy_node(&writer->context.graph, out_graph);
    }
  }

  if ((flags & SERD_LIST_CONT)) {
    // Continue a list
    if (!strcmp((const char*)predicate->buf, NS_RDF "first") &&
        !strcmp((const char*)object->buf, NS_RDF "nil")) {
      return esink("()", 2, writer);
    }

    TRY_FAILING(
      st, write_list_next(writer, flags, predicate, object, datatype, lang));

    if (st == SERD_FAILURE) { // Reached end of list
      pop_context(writer);
      return SERD_SUCCESS;
    }

  } else if (serd_node_equals(subject, &writer->context.subject)) {
    if (serd_node_equals(predicate, &writer->context.predicate)) {
      // Elide S P (write O)

      const Sep  last        = writer->last_sep;
      const bool anon_o      = flags & SERD_ANON_O_BEGIN;
      const bool list_o      = flags & SERD_LIST_O_BEGIN;
      const bool before_name = !anon_o && !list_o;
      const bool after_end   = (last == SEP_ANON_R) || (last == SEP_LIST_R);

      TRY(st,
          write_sep(writer,
                    before_name ? SEP_END_O_N
                    : after_end ? SEP_END_O_AA
                                : SEP_END_O_NA));

    } else {
      // Elide S (write P and O)

      const bool first = !writer->context.predicate.type;
      TRY(st, write_sep(writer, first ? SEP_S_P : SEP_END_P));
      TRY(st, write_pred(writer, predicate));
    }

    TRY(st, write_node(writer, object, datatype, lang, FIELD_OBJECT, flags));

  } else {
    // No abbreviation

    if (!serd_stack_is_empty(&writer->anon_stack)) {
      return SERD_ERR_BAD_ARG;
    }

    if (writer->context.subject.type) {
      TRY(st, write_sep(writer, SEP_STOP));
    }

    if (writer->last_sep == SEP_STOP) {
      TRY(st, write_newline(writer));
    }

    TRY(st, write_node(writer, subject, NULL, NULL, FIELD_SUBJECT, flags));
    if (!(flags & SERD_LIST_S_BEGIN)) {
      TRY(st, write_sep(writer, SEP_S_P));
    }

    reset_context(writer, 0U);
    copy_node(&writer->context.subject, subject);

    if (!(flags & SERD_LIST_S_BEGIN)) {
      TRY(st, write_pred(writer, predicate));
    }

    TRY(st, write_node(writer, object, datatype, lang, FIELD_OBJECT, flags));
  }

  if (flags & (SERD_ANON_S_BEGIN | SERD_LIST_S_BEGIN)) {
    // Push context for anonymous or list subject
    const bool is_list = (flags & SERD_LIST_S_BEGIN);
    push_context(writer,
                 is_list ? CTX_LIST : CTX_BLANK,
                 serd_node_copy(out_graph),
                 serd_node_copy(subject),
                 is_list ? SERD_NODE_NULL : serd_node_copy(predicate));
  }

  if (flags & (SERD_ANON_O_BEGIN | SERD_LIST_O_BEGIN)) {
    // Push context for anonymous or list object if necessary
    push_context(writer,
                 (flags & SERD_LIST_O_BEGIN) ? CTX_LIST : CTX_BLANK,
                 serd_node_copy(out_graph),
                 serd_node_copy(object),
                 SERD_NODE_NULL);
  }

  return st;
}

SerdStatus
serd_writer_end_anon(SerdWriter* const writer, const SerdNode* const node)
{
  assert(writer);

  SerdStatus st = SERD_SUCCESS;

  if (writer->syntax == SERD_NTRIPLES || writer->syntax == SERD_NQUADS) {
    return SERD_SUCCESS;
  }

  if (serd_stack_is_empty(&writer->anon_stack)) {
    return w_err(
      writer, SERD_ERR_UNKNOWN, "unexpected end of anonymous node\n");
  }

  // Decrease indent if we're current comma-indented (multiple objects at end)
  if (writer->context.comma_indented) {
    assert(writer->indent);
    --writer->indent;
    writer->context.comma_indented = false;
  }

  // Write the end separator ']' and pop the context
  TRY(st, write_sep(writer, SEP_ANON_R));
  pop_context(writer);

  if (node && serd_node_equals(node, &writer->context.subject)) {
    // Now-finished anonymous node is the new subject with no other context
    writer->context.predicate.type = SERD_NOTHING;
  }

  return st;
}

SerdStatus
serd_writer_finish(SerdWriter* const writer)
{
  assert(writer);

  const SerdStatus st0 = terminate_context(writer);
  const SerdStatus st1 = serd_byte_sink_flush(&writer->byte_sink);
  free_anon_stack(writer);
  reset_context(writer, RESET_GRAPH | RESET_INDENT);
  return st0 ? st0 : st1;
}

SerdWriter*
serd_writer_new(const SerdSyntax     syntax,
                const SerdStyle      style,
                SerdEnv* const       env,
                const SerdURI* const base_uri,
                SerdSink             ssink,
                void* const          stream)
{
  assert(env);
  assert(ssink);

  SerdWriter* writer = (SerdWriter*)calloc(1, sizeof(SerdWriter));

  writer->syntax     = syntax;
  writer->style      = style;
  writer->env        = env;
  writer->root_node  = SERD_NODE_NULL;
  writer->root_uri   = SERD_URI_NULL;
  writer->base_uri   = base_uri ? *base_uri : SERD_URI_NULL;
  writer->anon_stack = serd_stack_new(SERD_PAGE_SIZE);
  writer->byte_sink  = serd_byte_sink_new(
    ssink, stream, (style & SERD_STYLE_BULK) ? SERD_PAGE_SIZE : 1);

  return writer;
}

void
serd_writer_set_error_sink(SerdWriter* const   writer,
                           const SerdErrorSink error_sink,
                           void* const         error_handle)
{
  assert(writer);
  assert(error_sink);
  writer->error_sink   = error_sink;
  writer->error_handle = error_handle;
}

void
serd_writer_chop_blank_prefix(SerdWriter* const    writer,
                              const uint8_t* const prefix)
{
  assert(writer);

  free(writer->bprefix);
  writer->bprefix_len = 0;
  writer->bprefix     = NULL;

  const size_t prefix_len = prefix ? strlen((const char*)prefix) : 0;
  if (prefix_len) {
    writer->bprefix_len = prefix_len;
    writer->bprefix     = (uint8_t*)malloc(writer->bprefix_len + 1);
    memcpy(writer->bprefix, prefix, writer->bprefix_len + 1);
  }
}

SerdStatus
serd_writer_set_base_uri(SerdWriter* const writer, const SerdNode* const uri)
{
  assert(writer);

  SerdStatus st = SERD_SUCCESS;

  TRY(st, serd_env_set_base_uri(writer->env, uri));

  serd_env_get_base_uri(writer->env, &writer->base_uri);

  if (uri && (writer->syntax == SERD_TURTLE || writer->syntax == SERD_TRIG)) {
    const bool had_subject = writer->context.subject.type;
    TRY(st, terminate_context(writer));
    if (had_subject) {
      TRY(st, esink("\n", 1, writer));
    }

    TRY(st, esink("@base <", 7, writer));
    TRY(st, esink(uri->buf, uri->n_bytes, writer));
    TRY(st, esink(">", 1, writer));
    TRY(st, write_sep(writer, SEP_STOP));
  }

  return reset_context(writer, RESET_GRAPH | RESET_INDENT);
}

SerdStatus
serd_writer_set_root_uri(SerdWriter* const writer, const SerdNode* const uri)
{
  assert(writer);

  serd_node_free(&writer->root_node);

  if (uri && uri->buf) {
    writer->root_node = serd_node_copy(uri);
    SERD_DISABLE_NULL_WARNINGS
    serd_uri_parse(uri->buf, &writer->root_uri);
    SERD_RESTORE_WARNINGS
  } else {
    writer->root_node = SERD_NODE_NULL;
    writer->root_uri  = SERD_URI_NULL;
  }

  return SERD_SUCCESS;
}

SerdStatus
serd_writer_set_prefix(SerdWriter* const     writer,
                       const SerdNode* const name,
                       const SerdNode* const uri)
{
  assert(writer);
  assert(name);
  assert(uri);

  SerdStatus st = SERD_SUCCESS;

  TRY(st, serd_env_set_prefix(writer->env, name, uri));

  if (writer->syntax == SERD_TURTLE || writer->syntax == SERD_TRIG) {
    const bool had_subject = writer->context.subject.type;
    TRY(st, terminate_context(writer));
    if (had_subject) {
      TRY(st, esink("\n", 1, writer));
    }

    TRY(st, esink("@prefix ", 8, writer));
    TRY(st, esink(name->buf, name->n_bytes, writer));
    TRY(st, esink(": <", 3, writer));
    TRY(st, ewrite_uri(writer, uri->buf, uri->n_bytes));
    TRY(st, esink(">", 1, writer));
    TRY(st, write_sep(writer, SEP_STOP));
  }

  return reset_context(writer, RESET_GRAPH | RESET_INDENT);
}

void
serd_writer_free(SerdWriter* const writer)
{
  if (!writer) {
    return;
  }

  serd_writer_finish(writer);
  free_context(&writer->context);
  free_anon_stack(writer);
  serd_stack_free(&writer->anon_stack);
  free(writer->bprefix);
  serd_byte_sink_free(&writer->byte_sink);
  serd_node_free(&writer->root_node);
  free(writer);
}

SerdEnv*
serd_writer_get_env(SerdWriter* const writer)
{
  assert(writer);
  return writer->env;
}

size_t
serd_file_sink(const void* const buf, const size_t len, void* const stream)
{
  assert(buf);
  assert(stream);
  return fwrite(buf, 1, len, (FILE*)stream);
}

size_t
serd_chunk_sink(const void* const buf, const size_t len, void* const stream)
{
  assert(buf);
  assert(stream);

  SerdChunk* chunk = (SerdChunk*)stream;
  uint8_t* new_buf = (uint8_t*)realloc((uint8_t*)chunk->buf, chunk->len + len);
  if (new_buf) {
    memcpy(new_buf + chunk->len, buf, len);
    chunk->buf = new_buf;
    chunk->len += len;
  }
  return len;
}

uint8_t*
serd_chunk_sink_finish(SerdChunk* const stream)
{
  assert(stream);
  serd_chunk_sink("", 1, stream);
  return (uint8_t*)stream->buf;
}
