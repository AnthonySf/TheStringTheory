// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "lilv_internal.h"
#include "log.h"
#include "query.h"

#include <lilv/lilv.h>
#include <sord/sord.h>
#include <zix/tree.h>

#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdlib.h>

LilvPort*
lilv_port_new(LilvWorld*      world,
              const SordNode* node,
              uint32_t        index,
              const char*     symbol)
{
  LilvPort* port = (LilvPort*)malloc(sizeof(LilvPort));
  port->node     = lilv_node_new_from_node(world, node);
  port->index    = index;
  port->symbol   = lilv_node_new(world, LILV_VALUE_STRING, symbol);
  port->classes  = lilv_nodes_new();
  return port;
}

void
lilv_port_free(const LilvPlugin* plugin, LilvPort* port)
{
  (void)plugin;

  if (port) {
    lilv_node_free(port->node);
    lilv_nodes_free(port->classes);
    lilv_node_free(port->symbol);
    free(port);
  }
}

bool
lilv_port_is_a(const LilvPlugin* plugin,
               const LilvPort*   port,
               const LilvNode*   port_class)
{
  (void)plugin;

  LILV_FOREACH (nodes, i, port->classes) {
    if (lilv_node_equals(lilv_nodes_get(port->classes, i), port_class)) {
      return true;
    }
  }

  return false;
}

bool
lilv_port_has_property(const LilvPlugin* plugin,
                       const LilvPort*   port,
                       const LilvNode*   property)
{
  return sord_ask(plugin->world->model,
                  port->node->node,
                  plugin->world->uris.lv2_portProperty,
                  property->node,
                  NULL);
}

bool
lilv_port_supports_event(const LilvPlugin* plugin,
                         const LilvPort*   port,
                         const LilvNode*   event_type)
{
  const SordNode* predicates[] = {plugin->world->uris.event_supportsEvent,
                                  plugin->world->uris.atom_supports,
                                  NULL};

  for (const SordNode** pred = predicates; *pred; ++pred) {
    if (sord_ask(plugin->world->model,
                 port->node->node,
                 *pred,
                 event_type->node,
                 NULL)) {
      return true;
    }
  }

  return false;
}

static LilvNodes*
lilv_port_get_value_by_node(const LilvPlugin* plugin,
                            const LilvPort*   port,
                            const SordNode*   predicate)
{
  return lilv_nodes_from_matches(
    plugin->world, port->node->node, predicate, NULL, NULL);
}

const LilvNode*
lilv_port_get_node(const LilvPlugin* plugin, const LilvPort* port)
{
  (void)plugin;

  return port->node;
}

LilvNodes*
lilv_port_get_value(const LilvPlugin* plugin,
                    const LilvPort*   port,
                    const LilvNode*   predicate)
{
  if (!lilv_node_is_uri(predicate)) {
    LILV_ERRORF("Predicate \"%s\" is not a URI\n",
                sord_node_get_string(predicate->node));
    return NULL;
  }

  return lilv_port_get_value_by_node(plugin, port, predicate->node);
}

LilvNode*
lilv_port_get(const LilvPlugin* plugin,
              const LilvPort*   port,
              const LilvNode*   predicate)
{
  LilvNodes* values = lilv_port_get_value(plugin, port, predicate);

  LilvNode* value =
    lilv_node_duplicate(values ? lilv_nodes_get_first(values) : NULL);

  lilv_nodes_free(values);
  return value;
}

uint32_t
lilv_port_get_index(const LilvPlugin* plugin, const LilvPort* port)
{
  (void)plugin;

  return port->index;
}

const LilvNode*
lilv_port_get_symbol(const LilvPlugin* plugin, const LilvPort* port)
{
  (void)plugin;

  return port->symbol;
}

LilvNode*
lilv_port_get_name(const LilvPlugin* plugin, const LilvPort* port)
{
  LilvNodes* results =
    lilv_port_get_value_by_node(plugin, port, plugin->world->uris.lv2_name);

  LilvNode* ret = NULL;
  if (results) {
    const LilvNode* val = lilv_nodes_get_first(results);
    if (lilv_node_is_string(val)) {
      ret = lilv_node_duplicate(val);
    }
    lilv_nodes_free(results);
  }

  if (!ret) {
    LILV_WARNF("Plugin <%s> port has no (mandatory) doap:name\n",
               lilv_node_as_string(lilv_plugin_get_uri(plugin)));
  }

  return ret;
}

const LilvNodes*
lilv_port_get_classes(const LilvPlugin* plugin, const LilvPort* port)
{
  (void)plugin;

  return port->classes;
}

void
lilv_port_get_range(const LilvPlugin* plugin,
                    const LilvPort*   port,
                    LilvNode**        def,
                    LilvNode**        min,
                    LilvNode**        max)
{
  if (def) {
    LilvNodes* defaults = lilv_port_get_value_by_node(
      plugin, port, plugin->world->uris.lv2_default);
    *def =
      defaults ? lilv_node_duplicate(lilv_nodes_get_first(defaults)) : NULL;
    lilv_nodes_free(defaults);
  }

  if (min) {
    LilvNodes* minimums = lilv_port_get_value_by_node(
      plugin, port, plugin->world->uris.lv2_minimum);
    *min =
      minimums ? lilv_node_duplicate(lilv_nodes_get_first(minimums)) : NULL;
    lilv_nodes_free(minimums);
  }

  if (max) {
    LilvNodes* maximums = lilv_port_get_value_by_node(
      plugin, port, plugin->world->uris.lv2_maximum);
    *max =
      maximums ? lilv_node_duplicate(lilv_nodes_get_first(maximums)) : NULL;
    lilv_nodes_free(maximums);
  }
}

LilvScalePoints*
lilv_port_get_scale_points(const LilvPlugin* plugin, const LilvPort* port)
{
  SordIter* points = sord_search(plugin->world->model,
                                 port->node->node,
                                 plugin->world->uris.lv2_scalePoint,
                                 NULL,
                                 NULL);

  if (!points) {
    return NULL;
  }

  LilvScalePoints* ret = lilv_scale_points_new();

  FOREACH_MATCH (points) {
    const SordNode* point = sord_iter_get_node(points, SORD_OBJECT);

    const SordNode* value = lilv_plugin_get_unique_internal(
      plugin, point, plugin->world->uris.rdf_value);

    const SordNode* label = lilv_plugin_get_unique_internal(
      plugin, point, plugin->world->uris.rdfs_label);

    if (value && label) {
      zix_tree_insert(
        (ZixTree*)ret, lilv_scale_point_new(plugin->world, value, label), NULL);
    }
  }
  sord_iter_free(points);

  assert(lilv_nodes_size(ret) > 0);
  return ret;
}

LilvNodes*
lilv_port_get_properties(const LilvPlugin* plugin, const LilvPort* port)
{
  LilvNode* pred = lilv_node_new_from_node(
    plugin->world, plugin->world->uris.lv2_portProperty);
  LilvNodes* ret = lilv_port_get_value(plugin, port, pred);
  lilv_node_free(pred);
  return ret;
}
