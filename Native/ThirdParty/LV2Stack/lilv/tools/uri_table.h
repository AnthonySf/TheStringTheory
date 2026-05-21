// Copyright 2011-2019 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef URI_TABLE_H
#define URI_TABLE_H

#include <lv2/urid/urid.h>

#include <stdlib.h>
#include <string.h>

typedef struct {
  char** uris;
  size_t n_uris;
} URITable;

static void
uri_table_init(URITable* table)
{
  table->uris   = NULL;
  table->n_uris = 0;
}

static void
uri_table_destroy(URITable* table)
{
  for (size_t i = 0; i < table->n_uris; ++i) {
    free(table->uris[i]);
  }

  free(table->uris);
}

static LV2_URID
uri_table_map(LV2_URID_Map_Handle handle, const char* uri)
{
  URITable* table = (URITable*)handle;
  for (size_t i = 0; i < table->n_uris; ++i) {
    if (!strcmp(table->uris[i], uri)) {
      return i + 1;
    }
  }

  const size_t len = strlen(uri);
  table->uris = (char**)realloc(table->uris, ++table->n_uris * sizeof(char*));
  table->uris[table->n_uris - 1] = (char*)malloc(len + 1);
  memcpy(table->uris[table->n_uris - 1], uri, len + 1);
  return table->n_uris;
}

static const char*
uri_table_unmap(LV2_URID_Map_Handle handle, LV2_URID urid)
{
  URITable* table = (URITable*)handle;
  if (urid > 0 && urid <= table->n_uris) {
    return table->uris[urid - 1];
  }
  return NULL;
}

#endif /* URI_TABLE_H */
