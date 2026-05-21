// Copyright 2012-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "test_uri_map.h"

#include <assert.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

static char*
copy_string(const char* str)
{
  const size_t len = strlen(str);
  char*        dup = (char*)malloc(len + 1);
  assert(dup);
  memcpy(dup, str, len + 1);
  return dup;
}

LV2_URID
urid_map(LV2_URID_Map_Handle handle, const char* uri)
{
  Uris* const uris = (Uris*)handle;

  for (size_t i = 0; i < uris->n_uris; ++i) {
    if (!strcmp(uris->uris[i], uri)) {
      return i + 1;
    }
  }

  uris->uris = (char**)realloc(uris->uris, ++uris->n_uris * sizeof(char*));
  uris->uris[uris->n_uris - 1] = copy_string(uri);
  return uris->n_uris;
}

const char*
urid_unmap(LV2_URID_Unmap_Handle handle, LV2_URID urid)
{
  Uris* const uris = (Uris*)handle;

  if (urid > 0 && urid <= uris->n_uris) {
    return uris->uris[urid - 1];
  }

  return NULL;
}

void
free_uris(Uris* uris)
{
  for (uint32_t i = 0; i < uris->n_uris; ++i) {
    free(uris->uris[i]);
  }

  free(uris->uris);
}
