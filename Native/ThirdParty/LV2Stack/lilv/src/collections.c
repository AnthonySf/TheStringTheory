// Copyright 2008-2019 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "lilv_internal.h"

#include <lilv/lilv.h>
#include <sord/sord.h>
#include <zix/tree.h>

#include <stdbool.h>
#include <stddef.h>

typedef void (*LilvFreeFunc)(void* ptr);

int
lilv_ptr_cmp(const void* a, const void* b, const void* user_data)
{
  (void)user_data;

  return a < b ? -1 : b < a ? 1 : 0;
}

int
lilv_resource_node_cmp(const void* a, const void* b, const void* user_data)
{
  (void)user_data;

  const SordNode* an = ((const LilvNode*)a)->node;
  const SordNode* bn = ((const LilvNode*)b)->node;

  return an < bn ? -1 : bn < an ? 1 : 0;
}

/* Generic collection functions */

static void
destroy(void* const ptr, const void* const user_data)
{
  if (user_data) {
    ((LilvFreeFunc)user_data)(ptr);
  }
}

static inline LilvCollection*
lilv_collection_new(ZixTreeCompareFunc cmp, LilvFreeFunc free_func)
{
  return zix_tree_new(NULL, false, cmp, NULL, destroy, (const void*)free_func);
}

static void
lilv_collection_free(LilvCollection* collection)
{
  if (collection) {
    zix_tree_free((ZixTree*)collection);
  }
}

static unsigned
lilv_collection_size(const LilvCollection* collection)
{
  return (collection ? zix_tree_size((const ZixTree*)collection) : 0);
}

static LilvIter*
lilv_collection_begin(const LilvCollection* collection)
{
  return collection ? (LilvIter*)zix_tree_begin((ZixTree*)collection) : NULL;
}

void*
lilv_collection_get(const LilvCollection* collection, const LilvIter* i)
{
  (void)collection;

  return zix_tree_get((const ZixTreeIter*)i);
}

/* Constructors */

LilvScalePoints*
lilv_scale_points_new(void)
{
  return lilv_collection_new(lilv_ptr_cmp, (LilvFreeFunc)lilv_scale_point_free);
}

LilvNodes*
lilv_nodes_new(void)
{
  return lilv_collection_new(lilv_ptr_cmp, (LilvFreeFunc)lilv_node_free);
}

LilvUIs*
lilv_uis_new(void)
{
  return lilv_collection_new(lilv_header_compare_by_uri,
                             (LilvFreeFunc)lilv_ui_free);
}

LilvPluginClasses*
lilv_plugin_classes_new(void)
{
  return lilv_collection_new(lilv_header_compare_by_uri,
                             (LilvFreeFunc)lilv_plugin_class_free);
}

/* URI based accessors (for collections of things with URIs) */

const LilvPluginClass*
lilv_plugin_classes_get_by_uri(const LilvPluginClasses* classes,
                               const LilvNode*          uri)
{
  return (LilvPluginClass*)lilv_collection_get_by_uri((const ZixTree*)classes,
                                                      uri);
}

const LilvUI*
lilv_uis_get_by_uri(const LilvUIs* uis, const LilvNode* uri)
{
  return (LilvUI*)lilv_collection_get_by_uri((const ZixTree*)uis, uri);
}

/* Plugins */

LilvPlugins*
lilv_plugins_new(void)
{
  return lilv_collection_new(lilv_header_compare_by_uri, NULL);
}

const LilvPlugin*
lilv_plugins_get_by_uri(const LilvPlugins* plugins, const LilvNode* uri)
{
  return (LilvPlugin*)lilv_collection_get_by_uri((const ZixTree*)plugins, uri);
}

/* Nodes */

bool
lilv_nodes_contains(const LilvNodes* nodes, const LilvNode* value)
{
  LILV_FOREACH (nodes, i, nodes) {
    if (lilv_node_equals(lilv_nodes_get(nodes, i), value)) {
      return true;
    }
  }

  return false;
}

LilvNodes*
lilv_nodes_merge(const LilvNodes* a, const LilvNodes* b)
{
  LilvNodes* result = lilv_nodes_new();

  LILV_FOREACH (nodes, i, a) {
    zix_tree_insert(
      (ZixTree*)result, lilv_node_duplicate(lilv_nodes_get(a, i)), NULL);
  }

  LILV_FOREACH (nodes, i, b) {
    zix_tree_insert(
      (ZixTree*)result, lilv_node_duplicate(lilv_nodes_get(b, i)), NULL);
  }

  return result;
}

/* Iterator */

#define LILV_COLLECTION_IMPL(prefix, CT, ET)                      \
                                                                  \
  unsigned prefix##_size(const CT* collection)                    \
  {                                                               \
    return lilv_collection_size(collection);                      \
  }                                                               \
                                                                  \
  LilvIter* prefix##_begin(const CT* collection)                  \
  {                                                               \
    return lilv_collection_begin(collection);                     \
  }                                                               \
                                                                  \
  const ET* prefix##_get(const CT* collection, const LilvIter* i) \
  {                                                               \
    return (ET*)lilv_collection_get(collection, i);               \
  }                                                               \
                                                                  \
  LilvIter* prefix##_next(const CT* collection, LilvIter* i)      \
  {                                                               \
    (void)collection;                                             \
    return zix_tree_iter_next((ZixTreeIter*)i);                   \
  }                                                               \
                                                                  \
  bool prefix##_is_end(const CT* collection, const LilvIter* i)   \
  {                                                               \
    (void)collection;                                             \
    return zix_tree_iter_is_end((const ZixTreeIter*)i);           \
  }

LILV_COLLECTION_IMPL(lilv_plugin_classes, LilvPluginClasses, LilvPluginClass)
LILV_COLLECTION_IMPL(lilv_scale_points, LilvScalePoints, LilvScalePoint)
LILV_COLLECTION_IMPL(lilv_uis, LilvUIs, LilvUI)
LILV_COLLECTION_IMPL(lilv_nodes, LilvNodes, LilvNode)
LILV_COLLECTION_IMPL(lilv_plugins, LilvPlugins, LilvPlugin)

void
lilv_plugin_classes_free(LilvPluginClasses* collection)
{
  lilv_collection_free(collection);
}

void
lilv_scale_points_free(LilvScalePoints* collection)
{
  lilv_collection_free(collection);
}

void
lilv_uis_free(LilvUIs* collection)
{
  lilv_collection_free(collection);
}

void
lilv_nodes_free(LilvNodes* collection)
{
  lilv_collection_free(collection);
}

LilvNode*
lilv_nodes_get_first(const LilvNodes* collection)
{
  return (LilvNode*)lilv_collection_get(collection,
                                        lilv_collection_begin(collection));
}
