// Copyright 2007-2019 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "lilv_internal.h"

#include <lilv/lilv.h>
#include <sord/sord.h>
#include <zix/tree.h>

#include <stdbool.h>
#include <stdlib.h>

LilvPluginClass*
lilv_plugin_class_new(LilvWorld*      world,
                      const SordNode* parent_node,
                      LilvNode*       uri,
                      const char*     label)
{
  LilvPluginClass* pc = (LilvPluginClass*)malloc(sizeof(LilvPluginClass));
  pc->world           = world;
  pc->uri             = uri;
  pc->label           = lilv_node_new(world, LILV_VALUE_STRING, label);
  pc->parent_uri =
    (parent_node ? lilv_node_new_from_node(world, parent_node) : NULL);
  return pc;
}

void
lilv_plugin_class_free(LilvPluginClass* plugin_class)
{
  if (!plugin_class) {
    return;
  }

  lilv_node_free(plugin_class->uri);
  lilv_node_free(plugin_class->parent_uri);
  lilv_node_free(plugin_class->label);
  free(plugin_class);
}

const LilvNode*
lilv_plugin_class_get_parent_uri(const LilvPluginClass* plugin_class)
{
  return plugin_class->parent_uri ? plugin_class->parent_uri : NULL;
}

const LilvNode*
lilv_plugin_class_get_uri(const LilvPluginClass* plugin_class)
{
  return plugin_class->uri;
}

const LilvNode*
lilv_plugin_class_get_label(const LilvPluginClass* plugin_class)
{
  return plugin_class->label;
}

LilvPluginClasses*
lilv_plugin_class_get_children(const LilvPluginClass* plugin_class)
{
  // Returned list doesn't own categories
  LilvPluginClasses* all = plugin_class->world->plugin_classes;
  LilvPluginClasses* result =
    zix_tree_new(NULL, false, lilv_header_compare_by_uri, NULL, NULL, NULL);

  for (ZixTreeIter* i = zix_tree_begin((ZixTree*)all);
       i != zix_tree_end((ZixTree*)all);
       i = zix_tree_iter_next(i)) {
    const LilvPluginClass* c      = (LilvPluginClass*)zix_tree_get(i);
    const LilvNode*        parent = lilv_plugin_class_get_parent_uri(c);
    if (parent &&
        lilv_node_equals(lilv_plugin_class_get_uri(plugin_class), parent)) {
      zix_tree_insert((ZixTree*)result, (LilvPluginClass*)c, NULL);
    }
  }

  return result;
}
