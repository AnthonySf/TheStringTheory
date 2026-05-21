// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "string_util.h"

#include <lilv/lilv.h>
#include <serd/serd.h>
#include <sord/sord.h>
#include <zix/string_view.h>

#include <stdarg.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

void
lilv_free(void* ptr)
{
  free(ptr);
}

char*
lilv_strjoin(const char* first, ...)
{
  size_t len    = strlen(first);
  char*  result = (char*)malloc(len + 1);

  memcpy(result, first, len);

  va_list args; // NOLINT(cppcoreguidelines-init-variables)
  va_start(args, first);
  while (1) {
    const char* const s = va_arg(args, const char*);
    if (s == NULL) {
      break;
    }

    const size_t this_len   = strlen(s);
    char*        new_result = (char*)realloc(result, len + this_len + 1);
    if (!new_result) {
      va_end(args);
      free(result);
      return NULL;
    }

    result = new_result;
    memcpy(result + len, s, this_len);
    len += this_len;
  }
  va_end(args);

  result[len] = '\0';

  return result;
}

char*
lilv_strdup(const char* str)
{
  if (!str) {
    return NULL;
  }

  const size_t len  = strlen(str);
  char*        copy = (char*)malloc(len + 1);
  memcpy(copy, str, len + 1);
  return copy;
}

ZixStringView
lilv_node_string_view(const SordNode* const node)
{
  if (!node) {
    return zix_empty_string();
  }

  size_t               len = 0U;
  const uint8_t* const str = sord_node_get_string_counted(node, &len);

  return zix_substring((const char*)str, len);
}

uint8_t*
lilv_manifest_uri(const SordNode* const node)
{
  size_t               len = 0U;
  const uint8_t* const str = sord_node_get_string_counted(node, &len);
  if (!len) {
    return NULL;
  }

  const char        last = str[len - 1U];
  const char* const sep  = (last == '/') ? "" : "/";
  return (uint8_t*)lilv_strjoin((const char*)str, sep, "manifest.ttl", NULL);
}

const char*
lilv_uri_to_path(const char* uri)
{
#if defined(__GNUC__) && __GNUC__ > 4
#  pragma GCC diagnostic push
#  pragma GCC diagnostic ignored "-Wdeprecated-declarations"
#endif

  return (const char*)serd_uri_to_path((const uint8_t*)uri);

#if defined(__GNUC__) && __GNUC__ > 4
#  pragma GCC diagnostic pop
#endif
}

char*
lilv_file_uri_parse(const char* uri, char** hostname)
{
  return (char*)serd_file_uri_parse((const uint8_t*)uri, (uint8_t**)hostname);
}
