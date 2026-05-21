// Copyright 2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef SRATOM_TEST_TEST_URI_MAP_H
#define SRATOM_TEST_TEST_URI_MAP_H

#include <lv2/urid/urid.h>

#include <stddef.h>

/// Simple O(n) URI map
typedef struct {
  char** uris;
  size_t n_uris;
} Uris;

LV2_URID
urid_map(LV2_URID_Map_Handle handle, const char* uri);

const char*
urid_unmap(LV2_URID_Unmap_Handle handle, LV2_URID urid);

void
free_uris(Uris* uris);

#endif // SRATOM_TEST_TEST_URI_MAP_H
