// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef LILV_INTERNAL_H
#define LILV_INTERNAL_H

#ifdef __cplusplus
extern "C" {
#endif

#include "node_hash.h"
#include "uris.h"

#include <lilv/lilv.h>
#include <lv2/core/lv2.h>
#include <serd/serd.h>
#include <sord/sord.h>
#include <zix/tree.h>

#include <stdbool.h>
#include <stdint.h>

#ifdef LILV_DYN_MANIFEST
#  include <lv2/dynmanifest/dynmanifest.h>
#endif

/*
 *
 * Types
 *
 */

typedef void LilvCollection;

struct LilvPortImpl {
  LilvNode*  node;    ///< RDF node
  uint32_t   index;   ///< lv2:index
  LilvNode*  symbol;  ///< lv2:symbol
  LilvNodes* classes; ///< rdf:type
};

typedef struct LilvSpecImpl {
  SordNode*            spec;
  SordNode*            bundle;
  LilvNodes*           data_uris;
  struct LilvSpecImpl* next;
} LilvSpec;

/**
   Header of an LilvPlugin, LilvPluginClass, or LilvUI.
   Any of these structs may be safely casted to LilvHeader, which is used to
   implement collections using the same comparator.
*/
struct LilvHeader {
  LilvWorld* world;
  LilvNode*  uri;
};

#ifdef LILV_DYN_MANIFEST
typedef struct {
  LilvNode*               bundle;
  void*                   lib;
  LV2_Dyn_Manifest_Handle handle;
  uint32_t                refs;
} LilvDynManifest;
#endif

typedef struct {
  LilvWorld*                world;
  LilvNode*                 uri;
  char*                     bundle_path;
  void*                     lib;
  LV2_Descriptor_Function   lv2_descriptor;
  const LV2_Lib_Descriptor* desc;
  uint32_t                  refs;
} LilvLib;

struct LilvPluginImpl {
  LilvWorld* world;
  LilvNode*  plugin_uri;
  LilvNode*  bundle_uri; ///< Bundle plugin was loaded from
  LilvNode*  binary_uri; ///< lv2:binary
#ifdef LILV_DYN_MANIFEST
  LilvDynManifest* dynmanifest;
#endif
  const LilvPluginClass* plugin_class;
  LilvNodes*             data_uris; ///< rdfs::seeAlso
  LilvPort**             ports;
  uint32_t               num_ports;
  bool                   loaded;
  bool                   parse_errors;
  bool                   replaced;
};

struct LilvPluginClassImpl {
  LilvWorld* world;
  LilvNode*  uri;
  LilvNode*  parent_uri;
  LilvNode*  label;
};

struct LilvInstancePimpl {
  LilvWorld* world;
  LilvLib*   lib;
};

typedef struct {
  bool  dyn_manifest;
  bool  filter_lang;
  bool  object_index;
  char* lv2_path;
} LilvOptions;

struct LilvWorldImpl {
  SordWorld*         world;
  SordModel*         model;
  char*              lang;
  SerdReader*        reader;
  unsigned           n_read_files;
  LilvPluginClass*   lv2_plugin_class;
  LilvPluginClasses* plugin_classes;
  LilvSpec*          specs;
  LilvPlugins*       plugins;
  LilvPlugins*       zombies;
  NodeHash*          loaded_files;
  NodeHash*          replaced;
  ZixTree*           libs;
  SordModel*         applications;
  SordModel*         subclasses;
  LilvURIs           uris;
  LilvOptions        opt;
};

typedef enum {
  LILV_VALUE_URI,
  LILV_VALUE_STRING,
  LILV_VALUE_INT,
  LILV_VALUE_FLOAT,
  LILV_VALUE_BOOL,
  LILV_VALUE_BLANK,
  LILV_VALUE_BLOB
} LilvNodeType;

struct LilvNodeImpl {
  LilvWorld*   world;
  SordNode*    node;
  LilvNodeType type;
  union {
    int   int_val;
    float float_val;
    bool  bool_val;
  } val;
};

struct LilvScalePointImpl {
  LilvNode* value;
  LilvNode* label;
};

struct LilvUIImpl {
  LilvWorld* world;
  LilvNode*  uri;
  LilvNode*  bundle_uri;
  LilvNode*  binary_uri;
  LilvNodes* classes;
};

typedef struct LilvVersion {
  int minor;
  int micro;
} LilvVersion;

/*
 *
 * Functions
 *
 */

LilvPort*
lilv_port_new(LilvWorld*      world,
              const SordNode* node,
              uint32_t        index,
              const char*     symbol);
void
lilv_port_free(const LilvPlugin* plugin, LilvPort* port);

LilvPlugin*
lilv_plugin_new(LilvWorld* world, LilvNode* uri, LilvNode* bundle_uri);

void
lilv_plugin_clear(LilvPlugin* plugin, LilvNode* bundle_uri);

void
lilv_plugin_load_if_necessary(const LilvPlugin* plugin);

void
lilv_plugin_free(LilvPlugin* plugin);

const SordNode*
lilv_plugin_get_unique_internal(const LilvPlugin* plugin,
                                const SordNode*   subject,
                                const SordNode*   predicate);

LilvNode*
lilv_plugin_get_unique(const LilvPlugin* plugin,
                       const SordNode*   subject,
                       const SordNode*   predicate);

void*
lilv_collection_get(const LilvCollection* collection, const LilvIter* i);

LilvPluginClass*
lilv_plugin_class_new(LilvWorld*      world,
                      const SordNode* parent_node,
                      LilvNode*       uri,
                      const char*     label);

void
lilv_plugin_class_free(LilvPluginClass* plugin_class);

LilvLib*
lilv_lib_open(LilvWorld*                world,
              const LilvNode*           uri,
              const char*               bundle_path,
              const LV2_Feature* const* features);

const LV2_Descriptor*
lilv_lib_get_plugin(LilvLib* lib, uint32_t index);

void
lilv_lib_close(LilvLib* lib);

LilvNodes*
lilv_nodes_new(void);

LilvPlugins*
lilv_plugins_new(void);

LilvScalePoints*
lilv_scale_points_new(void);

LilvPluginClasses*
lilv_plugin_classes_new(void);

LilvUIs*
lilv_uis_new(void);

const SordNode*
lilv_world_get_unique(LilvWorld*      world,
                      const SordNode* subject,
                      const SordNode* predicate);

const uint8_t*
lilv_world_blank_node_prefix(LilvWorld* world);

int
lilv_world_load_resource_internal(LilvWorld* world, const SordNode* resource);

SerdStatus
lilv_world_load_file(LilvWorld* world, SerdReader* reader, const SordNode* uri);

LilvUI*
lilv_ui_new(LilvWorld*      world,
            const SordNode* uri,
            const SordNode* type_uri,
            const SordNode* binary_uri);

void
lilv_ui_free(LilvUI* ui);

LilvNode*
lilv_node_new(LilvWorld* world, LilvNodeType type, const char* str);

LilvNode*
lilv_node_new_from_node(LilvWorld* world, const SordNode* node);

int
lilv_header_compare_by_uri(const void* a, const void* b, const void* user_data);

int
lilv_ptr_cmp(const void* a, const void* b, const void* user_data);

int
lilv_resource_node_cmp(const void* a, const void* b, const void* user_data);

static inline int
lilv_version_cmp(const LilvVersion* a, const LilvVersion* b)
{
  if (a->minor == b->minor && a->micro == b->micro) {
    return 0;
  }

  if ((a->minor < b->minor) || (a->minor == b->minor && a->micro < b->micro)) {
    return -1;
  }

  return 1;
}

struct LilvHeader*
lilv_collection_get_by_uri(const ZixTree* seq, const LilvNode* uri);

LilvScalePoint*
lilv_scale_point_new(LilvWorld*      world,
                     const SordNode* value,
                     const SordNode* label);

void
lilv_scale_point_free(LilvScalePoint* point);

#ifdef LILV_DYN_MANIFEST
static const LV2_Feature* const dman_features = {NULL};

void
lilv_dynmanifest_free(LilvDynManifest* dynmanifest);
#endif

#ifdef __cplusplus
}
#endif

#endif /* LILV_INTERNAL_H */
