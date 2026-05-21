// Copyright 2012-2019 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "dylib.h"
#include "lilv_internal.h"
#include "log.h"
#include "string_util.h"

#include <lilv/lilv.h>
#include <lv2/core/lv2.h>
#include <serd/serd.h>
#include <zix/tree.h>

#include <stdint.h>
#include <stdlib.h>

LilvLib*
lilv_lib_open(LilvWorld*                world,
              const LilvNode*           uri,
              const char*               bundle_path,
              const LV2_Feature* const* features)
{
  ZixTreeIter*  i   = NULL;
  const LilvLib key = {
    world, (LilvNode*)uri, (char*)bundle_path, NULL, NULL, NULL, 0};
  if (!zix_tree_find(world->libs, &key, &i)) {
    LilvLib* llib = (LilvLib*)zix_tree_get(i);
    ++llib->refs;
    return llib;
  }

  const char* const lib_uri = lilv_node_as_uri(uri);
  char* const       lib_path =
    (char*)serd_file_uri_parse((const uint8_t*)lib_uri, NULL);
  if (!lib_path) {
    return NULL;
  }

  dylib_error();
  void* lib = dylib_open(lib_path, DYLIB_NOW);
  if (!lib) {
    LILV_ERRORF("Failed to open library %s (%s)\n", lib_path, dylib_error());
    serd_free(lib_path);
    return NULL;
  }

  LV2_Descriptor_Function df =
    (LV2_Descriptor_Function)dylib_func(lib, "lv2_descriptor");

  LV2_Lib_Descriptor_Function ldf =
    (LV2_Lib_Descriptor_Function)dylib_func(lib, "lv2_lib_descriptor");

  const LV2_Lib_Descriptor* desc = NULL;
  if (ldf) {
    desc = ldf(bundle_path, features);
    if (!desc) {
      LILV_ERRORF("Call to %s:lv2_lib_descriptor failed\n", lib_path);
      dylib_close(lib);
      serd_free(lib_path);
      return NULL;
    }
  } else if (!df) {
    LILV_ERRORF("No lv2_descriptor or lv2_lib_descriptor in %s\n", lib_path);
    dylib_close(lib);
    serd_free(lib_path);
    return NULL;
  }
  serd_free(lib_path);

  LilvLib* llib        = (LilvLib*)malloc(sizeof(LilvLib));
  llib->world          = world;
  llib->uri            = lilv_node_duplicate(uri);
  llib->bundle_path    = lilv_strdup(bundle_path);
  llib->lib            = lib;
  llib->lv2_descriptor = df;
  llib->desc           = desc;
  llib->refs           = 1;

  zix_tree_insert(world->libs, llib, NULL);
  return llib;
}

const LV2_Descriptor*
lilv_lib_get_plugin(LilvLib* lib, uint32_t index)
{
  if (lib->lv2_descriptor) {
    return lib->lv2_descriptor(index);
  }

  if (lib->desc) {
    return lib->desc->get_plugin(lib->desc->handle, index);
  }

  return NULL;
}

void
lilv_lib_close(LilvLib* lib)
{
  if (--lib->refs == 0) {
    dylib_close(lib->lib);

    ZixTreeIter* i = NULL;
    if (lib->world->libs && !zix_tree_find(lib->world->libs, lib, &i)) {
      zix_tree_remove(lib->world->libs, i);
    }

    lilv_node_free(lib->uri);
    free(lib->bundle_path);
    free(lib);
  }
}
