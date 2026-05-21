// Copyright 2007-2020 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef LILV_TEST_URI_MAP_H
#define LILV_TEST_URI_MAP_H

#include <lv2/urid/urid.h>
#include <serd/serd.h>

#include <assert.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

typedef struct {
  char**   uris;
  uint32_t n_uris;
} LilvTestUriMap;

static inline void
lilv_test_uri_map_init(LilvTestUriMap* const map)
{
  map->uris   = NULL;
  map->n_uris = 0;
}

static inline void
lilv_test_uri_map_clear(LilvTestUriMap* const map)
{
  for (uint32_t i = 0; i < map->n_uris; ++i) {
    free(map->uris[i]);
  }

  free(map->uris);
  map->uris   = NULL;
  map->n_uris = 0;
}

static inline LV2_URID
map_uri(LV2_URID_Map_Handle handle, const char* uri)
{
  LilvTestUriMap* map = (LilvTestUriMap*)handle;

  for (uint32_t i = 0; i < map->n_uris; ++i) {
    if (!strcmp(map->uris[i], uri)) {
      return i + 1;
    }
  }

  const size_t uri_len = strlen(uri);
  assert(serd_uri_string_has_scheme((const uint8_t*)uri));

  map->uris = (char**)realloc(map->uris, ++map->n_uris * sizeof(char*));
  assert(map->uris);
  map->uris[map->n_uris - 1] = (char*)calloc(1, uri_len + 1);
  assert(map->uris[map->n_uris - 1]);
  memcpy(map->uris[map->n_uris - 1], uri, uri_len + 1);
  return map->n_uris;
}

static inline const char*
unmap_uri(LV2_URID_Map_Handle handle, LV2_URID urid)
{
  LilvTestUriMap* map = (LilvTestUriMap*)handle;

  if (urid > 0 && urid <= map->n_uris) {
    return map->uris[urid - 1];
  }

  return NULL;
}

#endif // LILV_TEST_URI_MAP_H
