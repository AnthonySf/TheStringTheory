// Copyright 2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef SRATOM_TEST_FORGE_TEST_OBJECT_H
#define SRATOM_TEST_FORGE_TEST_OBJECT_H

#include "test_uri_map.h"

#include <lv2/atom/forge.h>
#include <lv2/urid/urid.h>

void
forge_test_object(LV2_Atom_Forge* forge,
                  LV2_URID_Map*   map,
                  Uris*           uris,
                  LV2_URID        obj_id);

#endif // SRATOM_TEST_FORGE_TEST_OBJECT_H
