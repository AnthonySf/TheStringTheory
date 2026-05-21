// Copyright 2012-2024 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/environment.h>

#include <zix/allocator.h>

#include <stdbool.h>
#include <stdlib.h>
#include <string.h>

static bool
is_path_delim(const char c)
{
  return c == '/' || c == ':' || c == '\0';
}

static bool
is_var_name_char(const char c)
{
  return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c == '_');
}

// Append suffix to dst, update dst_len, and return the realloc'd result
static char*
append_str(ZixAllocator* const allocator,
           size_t* const       dst_len,
           char* const         dst,
           const size_t        suffix_len,
           const char* const   suffix)
{
  const size_t out_len = *dst_len + suffix_len;
  char* const  out     = (char*)zix_realloc(allocator, dst, out_len + 1U);
  if (!out) {
    zix_free(allocator, dst);
    return NULL;
  }

  memcpy(out + *dst_len, suffix, suffix_len);
  out[(*dst_len += suffix_len)] = '\0';
  return out;
}

// Append the value of an environment variable to dst if it's set
static char*
append_var(ZixAllocator* const allocator,
           size_t* const       dst_len,
           char* const         dst,
           const size_t        ref_len,
           const char* const   ref)
{
  // Get value from environment
  const char* const val = getenv(ref + 1U); // NOLINT(concurrency-mt-unsafe)
  if (val) {
    return append_str(allocator, dst_len, dst, strlen(val), val);
  }

  // No value found, append variable reference as-is
  return append_str(allocator, dst_len, dst, ref_len, ref);
}

// Copy a variable reference string like $NAME to a null-terminated string
static char*
set_ref(ZixAllocator* const allocator,
        char** const        buffer,
        const size_t        ref_len,
        const char* const   ref)
{
  char* const out = (char*)zix_realloc(allocator, *buffer, ref_len + 1U);

  if (out) {
    memcpy(out, ref, ref_len);
    out[ref_len] = '\0';
  } else {
    zix_free(allocator, *buffer);
  }

  return out;
}

char*
zix_expand_environment_strings(ZixAllocator* const allocator,
                               const char* const   string)
{
  char*  ref = NULL; // Reference string like $NAME
  char*  out = NULL; // Expanded result
  size_t len = 0U;   // Length of expanded result

  size_t start = 0U; // Start of current chunk to copy
  for (size_t s = 0U; string[s];) {
    const char c = string[s];
    if (c == '$' && is_var_name_char(string[s + 1U])) {
      // Hit $ (variable reference like $VAR_NAME)
      for (size_t t = 1U;; ++t) {
        if (!is_var_name_char(string[s + t])) {
          const char* const prefix     = string + start;
          const size_t      prefix_len = s - start;
          if ((prefix_len &&
               !(out = append_str(allocator, &len, out, prefix_len, prefix))) ||
              !(ref = set_ref(allocator, &ref, t, string + s)) ||
              !(out = append_var(allocator, &len, out, t, ref))) {
            zix_free(allocator, ref);
            zix_free(allocator, out);
            return NULL;
          }
          start = s = t;
          break;
        }
      }
    } else if (c == '~' && is_path_delim(string[s + 1U])) {
      // Hit ~ before delimiter or end of string (home directory reference)
      const char* const prefix     = string + start;
      const size_t      prefix_len = s - start;
      if ((prefix_len &&
           !(out = append_str(allocator, &len, out, prefix_len, prefix))) ||
          !(out = append_var(allocator, &len, out, 5U, "$HOME"))) {
        zix_free(allocator, ref);
        return NULL;
      }
      start = ++s;
    } else {
      ++s;
    }
  }

  if (string[start]) {
    const char* const tail     = string + start;
    const size_t      tail_len = strlen(tail);
    out = append_str(allocator, &len, out, tail_len, tail);
  }

  zix_free(allocator, ref);
  return out;
}
