// Copyright 2007-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "lilv_internal.h"
#include "log.h"
#include "node_hash.h"
#include "query.h"

#ifdef LILV_DYN_MANIFEST
#  include "dylib.h"
#  include <lv2/dynmanifest/dynmanifest.h>
#endif

#include <lilv/lilv.h>
#include <lv2/core/lv2.h>
#include <serd/serd.h>
#include <sord/sord.h>
#include <zix/tree.h>

#include <assert.h>
#include <math.h>
#include <stdarg.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static void
lilv_plugin_init(LilvPlugin* plugin, LilvNode* bundle_uri)
{
  plugin->bundle_uri = bundle_uri;
  plugin->binary_uri = NULL;
#ifdef LILV_DYN_MANIFEST
  plugin->dynmanifest = NULL;
#endif
  plugin->plugin_class = NULL;
  plugin->data_uris    = lilv_nodes_new();
  plugin->ports        = NULL;
  plugin->num_ports    = 0;
  plugin->loaded       = false;
  plugin->parse_errors = false;
}

// Ownership of `uri` and `bundle` is taken
LilvPlugin*
lilv_plugin_new(LilvWorld* world, LilvNode* uri, LilvNode* bundle_uri)
{
  LilvPlugin* plugin = (LilvPlugin*)malloc(sizeof(LilvPlugin));

  plugin->world      = world;
  plugin->plugin_uri = uri;

  lilv_plugin_init(plugin, bundle_uri);
  return plugin;
}

void
lilv_plugin_clear(LilvPlugin* plugin, LilvNode* bundle_uri)
{
  lilv_node_free(plugin->bundle_uri);
  lilv_node_free(plugin->binary_uri);
  lilv_nodes_free(plugin->data_uris);
  lilv_plugin_init(plugin, bundle_uri);
}

static void
lilv_plugin_free_ports(LilvPlugin* plugin)
{
  if (plugin->ports) {
    for (uint32_t i = 0; i < plugin->num_ports; ++i) {
      lilv_port_free(plugin, plugin->ports[i]);
    }
    free(plugin->ports);
    plugin->num_ports = 0;
    plugin->ports     = NULL;
  }
}

void
lilv_plugin_free(LilvPlugin* plugin)
{
#ifdef LILV_DYN_MANIFEST
  if (plugin->dynmanifest && --plugin->dynmanifest->refs == 0) {
    lilv_dynmanifest_free(plugin->dynmanifest);
  }
#endif

  lilv_node_free(plugin->plugin_uri);
  plugin->plugin_uri = NULL;

  lilv_node_free(plugin->bundle_uri);
  plugin->bundle_uri = NULL;

  lilv_node_free(plugin->binary_uri);
  plugin->binary_uri = NULL;

  lilv_plugin_free_ports(plugin);

  lilv_nodes_free(plugin->data_uris);
  plugin->data_uris = NULL;

  free(plugin);
}

const SordNode*
lilv_plugin_get_unique_internal(const LilvPlugin* const plugin,
                                const SordNode* const   subject,
                                const SordNode* const   predicate)
{
  const SordNode* const ret =
    lilv_world_get_unique(plugin->world, subject, predicate);
  if (!ret) {
    LILV_ERRORF("No value found for (%s %s ...) property\n",
                sord_node_get_string(subject),
                sord_node_get_string(predicate));
  }
  return ret;
}

LilvNode*
lilv_plugin_get_unique(const LilvPlugin* plugin,
                       const SordNode*   subject,
                       const SordNode*   predicate)
{
  return lilv_node_new_from_node(
    plugin->world, lilv_plugin_get_unique_internal(plugin, subject, predicate));
}

static void
load_prototypes(LilvPlugin* const plugin)
{
  NodeHash* const prots =
    lilv_hash_from_matches(plugin->world->model,
                           plugin->plugin_uri->node,
                           plugin->world->uris.lv2_prototype,
                           NULL,
                           NULL);
  if (!prots) {
    return;
  }

  SordModel* skel = sord_new(plugin->world->world, SORD_SPO, false);

  NODE_HASH_FOREACH (p, prots) {
    const SordNode* prototype = lilv_node_hash_get(prots, p);

    lilv_world_load_resource_internal(plugin->world, prototype);

    SordIter* statements =
      sord_search(plugin->world->model, prototype, NULL, NULL, NULL);
    FOREACH_MATCH (statements) {
      SordQuad quad;
      sord_iter_get(statements, quad);
      quad[0] = plugin->plugin_uri->node;
      sord_add(skel, quad);
    }
    sord_iter_free(statements);
  }

  lilv_node_hash_free(prots, plugin->world->world);

  SordIter* iter = NULL;
  for (iter = sord_begin(skel); !sord_iter_end(iter); sord_iter_next(iter)) {
    SordQuad quad;
    sord_iter_get(iter, quad);
    sord_add(plugin->world->model, quad);
  }
  sord_iter_free(iter);
  sord_free(skel);
}

static void
lilv_plugin_load(LilvPlugin* plugin)
{
  load_prototypes(plugin);

  SordNode*   bundle_node = plugin->bundle_uri->node;
  SerdEnv*    env         = serd_env_new(sord_node_to_serd_node(bundle_node));
  SerdReader* reader =
    sord_new_reader(plugin->world->model, env, SERD_TURTLE, bundle_node);

  // Parse all the plugin's data files into RDF model
  SerdStatus st = SERD_SUCCESS;
  LILV_FOREACH (nodes, i, plugin->data_uris) {
    const LilvNode* data_uri = lilv_nodes_get(plugin->data_uris, i);

    serd_env_set_base_uri(env, sord_node_to_serd_node(data_uri->node));
    st = lilv_world_load_file(plugin->world, reader, data_uri->node);
    if (st > SERD_FAILURE) {
      break;
    }
  }

  if (st > SERD_FAILURE) {
    plugin->loaded       = true;
    plugin->parse_errors = true;
    serd_reader_free(reader);
    serd_env_free(env);
    return;
  }

#ifdef LILV_DYN_MANIFEST
  // Load and parse dynamic manifest data, if this is a library
  if (plugin->dynmanifest) {
    typedef int (*GetDataFunc)(
      LV2_Dyn_Manifest_Handle handle, FILE* fp, const char* uri);
    GetDataFunc get_data_func = (GetDataFunc)dylib_func(
      plugin->dynmanifest->lib, "lv2_dyn_manifest_get_data");
    if (get_data_func) {
      const SordNode* bundle = plugin->dynmanifest->bundle->node;
      serd_env_set_base_uri(env, sord_node_to_serd_node(bundle));
      FILE* fd = tmpfile();
      assert(fd);
      get_data_func(plugin->dynmanifest->handle,
                    fd,
                    lilv_node_as_string(plugin->plugin_uri));
      fseek(fd, 0, SEEK_SET);
      serd_reader_add_blank_prefix(reader,
                                   lilv_world_blank_node_prefix(plugin->world));
      serd_reader_read_file_handle(
        reader, fd, (const uint8_t*)"(dyn-manifest)");
      fclose(fd);
    }
  }
#endif
  serd_reader_free(reader);
  serd_env_free(env);

  plugin->loaded = true;
}

static bool
is_symbol(const char* str)
{
  for (const char* s = str; *s; ++s) {
    if (!((*s >= 'a' && *s <= 'z') || (*s >= 'A' && *s <= 'Z') ||
          (s > str && *s >= '0' && *s <= '9') || *s == '_')) {
      return false;
    }
  }
  return true;
}

static void
lilv_plugin_load_ports_if_necessary(const LilvPlugin* const_plugin)
{
  LilvPlugin* plugin = (LilvPlugin*)const_plugin;

  lilv_plugin_load_if_necessary(plugin);

  if (!plugin->ports) {
    plugin->ports    = (LilvPort**)malloc(sizeof(LilvPort*));
    plugin->ports[0] = NULL;

    SordIter* ports = sord_search(plugin->world->model,
                                  plugin->plugin_uri->node,
                                  plugin->world->uris.lv2_port,
                                  NULL,
                                  NULL);

    FOREACH_MATCH (ports) {
      const SordNode* port = sord_iter_get_node(ports, SORD_OBJECT);

      const SordNode* symbol = lilv_plugin_get_unique_internal(
        plugin, port, plugin->world->uris.lv2_symbol);

      const char* const symbol_str =
        symbol ? (const char*)sord_node_get_string(symbol) : "";

      if (!symbol || sord_node_get_type(symbol) != SORD_LITERAL ||
          !is_symbol(symbol_str)) {
        LILV_ERRORF("Plugin <%s> port symbol \"%s\" is invalid\n",
                    lilv_node_as_uri(plugin->plugin_uri),
                    symbol_str);
        lilv_plugin_free_ports(plugin);
        break;
      }

      const SordNode* index = lilv_plugin_get_unique_internal(
        plugin, port, plugin->world->uris.lv2_index);

      if (!index || sord_node_get_type(index) != SORD_LITERAL ||
          !sord_node_equals(sord_node_get_datatype(index),
                            plugin->world->uris.xsd_integer)) {
        LILV_ERRORF("Plugin <%s> port index is not an integer\n",
                    lilv_node_as_uri(plugin->plugin_uri));
        lilv_plugin_free_ports(plugin);
        break;
      }

      const char* const index_str  = (const char*)sord_node_get_string(index);
      const uint32_t    this_index = (uint32_t)strtol(index_str, NULL, 10);
      LilvPort*         this_port  = NULL;
      if (plugin->num_ports > this_index) {
        this_port = plugin->ports[this_index];
      } else {
        plugin->ports = (LilvPort**)realloc(
          plugin->ports, (this_index + 1) * sizeof(LilvPort*));
        memset(plugin->ports + plugin->num_ports,
               '\0',
               (this_index - plugin->num_ports) * sizeof(LilvPort*));
        plugin->num_ports = this_index + 1;
      }

      // Haven't seen this port yet, add it to array
      if (!this_port) {
        this_port = lilv_port_new(plugin->world, port, this_index, symbol_str);
        plugin->ports[this_index] = this_port;
      }

      SordIter* types = sord_search(
        plugin->world->model, port, plugin->world->uris.rdf_type, NULL, NULL);
      FOREACH_MATCH (types) {
        const SordNode* type = sord_iter_get_node(types, SORD_OBJECT);
        if (sord_node_get_type(type) == SORD_URI) {
          zix_tree_insert((ZixTree*)this_port->classes,
                          lilv_node_new_from_node(plugin->world, type),
                          NULL);
        } else {
          LILV_WARNF("Plugin <%s> port type is not a URI\n",
                     lilv_node_as_uri(plugin->plugin_uri));
        }
      }
      sord_iter_free(types);
    }
    sord_iter_free(ports);

    // Check sanity
    for (uint32_t i = 0; i < plugin->num_ports; ++i) {
      if (!plugin->ports[i]) {
        LILV_ERRORF("Plugin <%s> is missing port %u/%u\n",
                    lilv_node_as_uri(plugin->plugin_uri),
                    i,
                    plugin->num_ports);
        lilv_plugin_free_ports(plugin);
        break;
      }
    }
  }
}

void
lilv_plugin_load_if_necessary(const LilvPlugin* plugin)
{
  assert(plugin);
  if (!plugin->loaded) {
    lilv_plugin_load((LilvPlugin*)plugin);
  }
}

const LilvNode*
lilv_plugin_get_uri(const LilvPlugin* plugin)
{
  return plugin->plugin_uri;
}

const LilvNode*
lilv_plugin_get_bundle_uri(const LilvPlugin* plugin)
{
  return plugin->bundle_uri;
}

const LilvNode*
lilv_plugin_get_library_uri(const LilvPlugin* plugin)
{
  lilv_plugin_load_if_necessary((LilvPlugin*)plugin);
  if (!plugin->binary_uri) {
    // <plugin> lv2:binary ?binary
    SordIter* i = sord_search(plugin->world->model,
                              plugin->plugin_uri->node,
                              plugin->world->uris.lv2_binary,
                              NULL,
                              NULL);
    FOREACH_MATCH (i) {
      const SordNode* binary_node = sord_iter_get_node(i, SORD_OBJECT);
      if (sord_node_get_type(binary_node) == SORD_URI) {
        ((LilvPlugin*)plugin)->binary_uri =
          lilv_node_new_from_node(plugin->world, binary_node);
        break;
      }
    }
    sord_iter_free(i);
  }
  if (!plugin->binary_uri) {
    LILV_WARNF("Plugin <%s> has no lv2:binary\n",
               lilv_node_as_uri(lilv_plugin_get_uri(plugin)));
  }
  return plugin->binary_uri;
}

const LilvNodes*
lilv_plugin_get_data_uris(const LilvPlugin* plugin)
{
  return plugin->data_uris;
}

const LilvPluginClass*
lilv_plugin_get_class(const LilvPlugin* plugin)
{
  lilv_plugin_load_if_necessary((LilvPlugin*)plugin);
  if (!plugin->plugin_class) {
    // <plugin> a ?class
    SordIter* c = sord_search(plugin->world->model,
                              plugin->plugin_uri->node,
                              plugin->world->uris.rdf_type,
                              NULL,
                              NULL);
    FOREACH_MATCH (c) {
      const SordNode* class_node = sord_iter_get_node(c, SORD_OBJECT);
      if (sord_node_get_type(class_node) != SORD_URI) {
        continue;
      }

      LilvNode* klass = lilv_node_new_from_node(plugin->world, class_node);
      if (!lilv_node_equals(klass, plugin->world->lv2_plugin_class->uri)) {
        const LilvPluginClass* pclass =
          lilv_plugin_classes_get_by_uri(plugin->world->plugin_classes, klass);

        if (pclass) {
          ((LilvPlugin*)plugin)->plugin_class = pclass;
          lilv_node_free(klass);
          break;
        }
      }

      lilv_node_free(klass);
    }
    sord_iter_free(c);

    if (plugin->plugin_class == NULL) {
      ((LilvPlugin*)plugin)->plugin_class = plugin->world->lv2_plugin_class;
    }
  }
  return plugin->plugin_class;
}

static LilvNodes*
lilv_plugin_get_value_internal(const LilvPlugin* plugin,
                               const SordNode*   predicate)
{
  lilv_plugin_load_if_necessary(plugin);
  return lilv_nodes_from_matches(
    plugin->world, plugin->plugin_uri->node, predicate, NULL, NULL);
}

bool
lilv_plugin_verify(const LilvPlugin* plugin)
{
  lilv_plugin_load_if_necessary(plugin);
  if (plugin->parse_errors) {
    return false;
  }

  LilvNode*  rdf_type = lilv_new_uri(plugin->world, LILV_NS_RDF "type");
  LilvNodes* results  = lilv_plugin_get_value(plugin, rdf_type);
  lilv_node_free(rdf_type);
  if (!results) {
    return false;
  }

  lilv_nodes_free(results);
  results =
    lilv_plugin_get_value_internal(plugin, plugin->world->uris.doap_name);
  if (!results) {
    return false;
  }

  lilv_nodes_free(results);
  LilvNode* lv2_port = lilv_new_uri(plugin->world, LV2_CORE__port);
  results            = lilv_plugin_get_value(plugin, lv2_port);
  lilv_node_free(lv2_port);
  if (!results) {
    return false;
  }

  lilv_nodes_free(results);
  return true;
}

LilvNode*
lilv_plugin_get_name(const LilvPlugin* plugin)
{
  LilvNodes* results =
    lilv_plugin_get_value_internal(plugin, plugin->world->uris.doap_name);

  LilvNode* ret = NULL;
  if (results) {
    const LilvNode* val = lilv_nodes_get_first(results);
    if (lilv_node_is_string(val)) {
      ret = lilv_node_duplicate(val);
    }
    lilv_nodes_free(results);
  }

  if (!ret) {
    LILV_WARNF("Plugin <%s> has no (mandatory) doap:name\n",
               lilv_node_as_string(lilv_plugin_get_uri(plugin)));
  }

  return ret;
}

LilvNodes*
lilv_plugin_get_value(const LilvPlugin* plugin, const LilvNode* predicate)
{
  lilv_plugin_load_if_necessary(plugin);
  return lilv_world_find_nodes(
    plugin->world, plugin->plugin_uri, predicate, NULL);
}

uint32_t
lilv_plugin_get_num_ports(const LilvPlugin* plugin)
{
  lilv_plugin_load_ports_if_necessary(plugin);
  return plugin->num_ports;
}

void
lilv_plugin_get_port_ranges_float(const LilvPlugin* plugin,
                                  float*            min_values,
                                  float*            max_values,
                                  float*            def_values)
{
  lilv_plugin_load_ports_if_necessary(plugin);
  LilvNode*  min    = NULL;
  LilvNode*  max    = NULL;
  LilvNode*  def    = NULL;
  LilvNode** minptr = min_values ? &min : NULL;
  LilvNode** maxptr = max_values ? &max : NULL;
  LilvNode** defptr = def_values ? &def : NULL;

  for (uint32_t i = 0; i < plugin->num_ports; ++i) {
    lilv_port_get_range(plugin, plugin->ports[i], defptr, minptr, maxptr);

    if (min_values) {
      if (lilv_node_is_float(min) || lilv_node_is_int(min)) {
        min_values[i] = lilv_node_as_float(min);
      } else {
        min_values[i] = NAN;
      }
    }

    if (max_values) {
      if (lilv_node_is_float(max) || lilv_node_is_int(max)) {
        max_values[i] = lilv_node_as_float(max);
      } else {
        max_values[i] = NAN;
      }
    }

    if (def_values) {
      if (lilv_node_is_float(def) || lilv_node_is_int(def)) {
        def_values[i] = lilv_node_as_float(def);
      } else {
        def_values[i] = NAN;
      }
    }

    lilv_node_free(def);
    lilv_node_free(min);
    lilv_node_free(max);
  }
}

uint32_t
lilv_plugin_get_num_ports_of_class_va(
  const LilvPlugin* plugin,
  const LilvNode*   class_1,
  va_list           args // NOLINT(readability-non-const-parameter)
)
{
  lilv_plugin_load_ports_if_necessary(plugin);

  uint32_t count = 0;

  // Build array of classes from args so we can walk it several times
  size_t           n_classes = 0;
  const LilvNode** classes   = NULL;
  for (LilvNode* c = NULL; (c = va_arg(args, LilvNode*));) {
    classes =
      (const LilvNode**)realloc(classes, ++n_classes * sizeof(LilvNode*));
    classes[n_classes - 1] = c;
  }

  // Check each port against every type
  for (unsigned i = 0; i < plugin->num_ports; ++i) {
    const LilvPort* port = plugin->ports[i];
    if (port && lilv_port_is_a(plugin, port, class_1)) {
      bool matches = true;
      for (size_t j = 0; j < n_classes; ++j) {
        if (!lilv_port_is_a(plugin, port, classes[j])) {
          matches = false;
          break;
        }
      }

      if (matches) {
        ++count;
      }
    }
  }

  free(classes);
  return count;
}

uint32_t
lilv_plugin_get_num_ports_of_class(const LilvPlugin* plugin,
                                   const LilvNode*   class_1,
                                   ...)
{
  va_list args; // NOLINT(cppcoreguidelines-init-variables)
  va_start(args, class_1);

  uint32_t count = lilv_plugin_get_num_ports_of_class_va(plugin, class_1, args);

  va_end(args);
  return count;
}

bool
lilv_plugin_has_latency(const LilvPlugin* plugin)
{
  lilv_plugin_load_if_necessary(plugin);
  SordIter* ports = sord_search(plugin->world->model,
                                plugin->plugin_uri->node,
                                plugin->world->uris.lv2_port,
                                NULL,
                                NULL);

  bool ret = false;
  FOREACH_MATCH (ports) {
    const SordNode* port = sord_iter_get_node(ports, SORD_OBJECT);

    SordIter* prop = sord_search(plugin->world->model,
                                 port,
                                 plugin->world->uris.lv2_portProperty,
                                 plugin->world->uris.lv2_reportsLatency,
                                 NULL);

    SordIter* des = sord_search(plugin->world->model,
                                port,
                                plugin->world->uris.lv2_designation,
                                plugin->world->uris.lv2_latency,
                                NULL);

    const bool latent = !sord_iter_end(prop) || !sord_iter_end(des);
    sord_iter_free(prop);
    sord_iter_free(des);
    if (latent) {
      ret = true;
      break;
    }
  }
  sord_iter_free(ports);

  return ret;
}

static const LilvPort*
lilv_plugin_get_port_by_property(const LilvPlugin* plugin,
                                 const SordNode*   port_property)
{
  lilv_plugin_load_ports_if_necessary(plugin);
  for (uint32_t i = 0; i < plugin->num_ports; ++i) {
    LilvPort* port = plugin->ports[i];
    SordIter* iter = sord_search(plugin->world->model,
                                 port->node->node,
                                 plugin->world->uris.lv2_portProperty,
                                 port_property,
                                 NULL);

    const bool found = !sord_iter_end(iter);
    sord_iter_free(iter);

    if (found) {
      return port;
    }
  }

  return NULL;
}

const LilvPort*
lilv_plugin_get_port_by_designation(const LilvPlugin* plugin,
                                    const LilvNode*   port_class,
                                    const LilvNode*   designation)
{
  LilvWorld* world = plugin->world;
  lilv_plugin_load_ports_if_necessary(plugin);
  for (uint32_t i = 0; i < plugin->num_ports; ++i) {
    LilvPort* port = plugin->ports[i];
    SordIter* iter = sord_search(world->model,
                                 port->node->node,
                                 world->uris.lv2_designation,
                                 designation->node,
                                 NULL);

    const bool found =
      !sord_iter_end(iter) &&
      (!port_class || lilv_port_is_a(plugin, port, port_class));
    sord_iter_free(iter);

    if (found) {
      return port;
    }
  }

  return NULL;
}

uint32_t
lilv_plugin_get_latency_port_index(const LilvPlugin* plugin)
{
  LilvNode* lv2_OutputPort = lilv_new_uri(plugin->world, LV2_CORE__OutputPort);
  LilvNode* lv2_latency    = lilv_new_uri(plugin->world, LV2_CORE__latency);

  const LilvPort* prop_port = lilv_plugin_get_port_by_property(
    plugin, plugin->world->uris.lv2_reportsLatency);
  const LilvPort* des_port =
    lilv_plugin_get_port_by_designation(plugin, lv2_OutputPort, lv2_latency);

  lilv_node_free(lv2_latency);
  lilv_node_free(lv2_OutputPort);

  if (prop_port) {
    return prop_port->index;
  }

  if (des_port) {
    return des_port->index;
  }

  return (uint32_t)-1;
}

bool
lilv_plugin_has_feature(const LilvPlugin* plugin, const LilvNode* feature)
{
  lilv_plugin_load_if_necessary(plugin);
  const SordNode* predicates[] = {plugin->world->uris.lv2_requiredFeature,
                                  plugin->world->uris.lv2_optionalFeature,
                                  NULL};

  for (const SordNode** pred = predicates; *pred; ++pred) {
    if (sord_ask(plugin->world->model,
                 plugin->plugin_uri->node,
                 *pred,
                 feature->node,
                 NULL)) {
      return true;
    }
  }
  return false;
}

LilvNodes*
lilv_plugin_get_supported_features(const LilvPlugin* plugin)
{
  LilvNodes* optional = lilv_plugin_get_optional_features(plugin);
  LilvNodes* required = lilv_plugin_get_required_features(plugin);
  LilvNodes* result   = lilv_nodes_merge(optional, required);
  lilv_nodes_free(optional);
  lilv_nodes_free(required);
  return result;
}

LilvNodes*
lilv_plugin_get_optional_features(const LilvPlugin* plugin)
{
  lilv_plugin_load_if_necessary(plugin);
  return lilv_nodes_from_matches(plugin->world,
                                 plugin->plugin_uri->node,
                                 plugin->world->uris.lv2_optionalFeature,
                                 NULL,
                                 NULL);
}

LilvNodes*
lilv_plugin_get_required_features(const LilvPlugin* plugin)
{
  lilv_plugin_load_if_necessary(plugin);
  return lilv_nodes_from_matches(plugin->world,
                                 plugin->plugin_uri->node,
                                 plugin->world->uris.lv2_requiredFeature,
                                 NULL,
                                 NULL);
}

bool
lilv_plugin_has_extension_data(const LilvPlugin* plugin, const LilvNode* uri)
{
  if (!lilv_node_is_uri(uri)) {
    LILV_ERRORF("Extension data \"%s\" is not a URI\n",
                sord_node_get_string(uri->node));
    return false;
  }

  lilv_plugin_load_if_necessary(plugin);
  return sord_ask(plugin->world->model,
                  plugin->plugin_uri->node,
                  plugin->world->uris.lv2_extensionData,
                  uri->node,
                  NULL);
}

LilvNodes*
lilv_plugin_get_extension_data(const LilvPlugin* plugin)
{
  return lilv_plugin_get_value_internal(plugin,
                                        plugin->world->uris.lv2_extensionData);
}

const LilvPort*
lilv_plugin_get_port_by_index(const LilvPlugin* plugin, uint32_t index)
{
  lilv_plugin_load_ports_if_necessary(plugin);
  if (index < plugin->num_ports) {
    return plugin->ports[index];
  }

  return NULL;
}

const LilvPort*
lilv_plugin_get_port_by_symbol(const LilvPlugin* plugin, const LilvNode* symbol)
{
  lilv_plugin_load_ports_if_necessary(plugin);
  for (uint32_t i = 0; i < plugin->num_ports; ++i) {
    LilvPort* port = plugin->ports[i];
    if (lilv_node_equals(port->symbol, symbol)) {
      return port;
    }
  }

  return NULL;
}

LilvNode*
lilv_plugin_get_project(const LilvPlugin* plugin)
{
  lilv_plugin_load_if_necessary(plugin);

  SordIter* projects = sord_search(plugin->world->model,
                                   plugin->plugin_uri->node,
                                   plugin->world->uris.lv2_project,
                                   NULL,
                                   NULL);

  if (sord_iter_end(projects)) {
    sord_iter_free(projects);
    return NULL;
  }

  const SordNode* project = sord_iter_get_node(projects, SORD_OBJECT);

  sord_iter_free(projects);
  return lilv_node_new_from_node(plugin->world, project);
}

static const SordNode*
lilv_plugin_get_author(const LilvPlugin* plugin)
{
  lilv_plugin_load_if_necessary(plugin);

  const SordNode* doap_maintainer = plugin->world->uris.doap_maintainer;

  SordIter* maintainers = sord_search(plugin->world->model,
                                      plugin->plugin_uri->node,
                                      doap_maintainer,
                                      NULL,
                                      NULL);

  if (sord_iter_end(maintainers)) {
    sord_iter_free(maintainers);

    LilvNode* project = lilv_plugin_get_project(plugin);
    if (!project) {
      return NULL;
    }

    maintainers = sord_search(
      plugin->world->model, project->node, doap_maintainer, NULL, NULL);

    lilv_node_free(project);
  }

  if (sord_iter_end(maintainers)) {
    sord_iter_free(maintainers);
    return NULL;
  }

  const SordNode* author = sord_iter_get_node(maintainers, SORD_OBJECT);

  sord_iter_free(maintainers);
  return author;
}

static LilvNode*
lilv_plugin_get_author_property(const LilvPlugin* plugin, const SordNode* pred)
{
  const SordNode* author = lilv_plugin_get_author(plugin);

  return author ? lilv_node_from_object(plugin->world, author, pred) : NULL;
}

LilvNode*
lilv_plugin_get_author_name(const LilvPlugin* plugin)
{
  return lilv_plugin_get_author_property(plugin, plugin->world->uris.foaf_name);
}

LilvNode*
lilv_plugin_get_author_email(const LilvPlugin* plugin)
{
  return lilv_plugin_get_author_property(plugin, plugin->world->uris.foaf_mbox);
}

LilvNode*
lilv_plugin_get_author_homepage(const LilvPlugin* plugin)
{
  return lilv_plugin_get_author_property(plugin,
                                         plugin->world->uris.foaf_homepage);
}

bool
lilv_plugin_is_replaced(const LilvPlugin* plugin)
{
  return lilv_node_hash_find(plugin->world->replaced,
                             lilv_plugin_get_uri(plugin)->node) !=
         lilv_node_hash_end(plugin->world->replaced);
}

LilvUIs*
lilv_plugin_get_uis(const LilvPlugin* plugin)
{
  lilv_plugin_load_if_necessary(plugin);

  LilvUIs* result = lilv_uis_new();

  SordIter* uis = sord_search(plugin->world->model,
                              plugin->plugin_uri->node,
                              plugin->world->uris.ui_ui,
                              NULL,
                              NULL);

  FOREACH_MATCH (uis) {
    const SordNode* ui = sord_iter_get_node(uis, SORD_OBJECT);

    const SordNode* const type =
      lilv_world_get_unique(plugin->world, ui, plugin->world->uris.rdf_type);
    const SordNode* binary =
      lilv_world_get_unique(plugin->world, ui, plugin->world->uris.lv2_binary);
    if (!binary) {
      binary =
        lilv_world_get_unique(plugin->world, ui, plugin->world->uris.ui_binary);
    }

    if (!type || !binary || sord_node_get_type(ui) != SORD_URI ||
        sord_node_get_type(type) != SORD_URI ||
        sord_node_get_type(binary) != SORD_URI) {
      LILV_ERRORF("Corrupt UI <%s>\n", sord_node_get_string(ui));
      continue;
    }

    LilvUI* const lilv_ui = lilv_ui_new(plugin->world, ui, type, binary);
    zix_tree_insert((ZixTree*)result, lilv_ui, NULL);
  }
  sord_iter_free(uis);

  if (lilv_uis_size(result) > 0) {
    return result;
  }

  lilv_uis_free(result);
  return NULL;
}

LilvNodes*
lilv_plugin_get_related(const LilvPlugin* plugin, const LilvNode* type)
{
  lilv_plugin_load_if_necessary(plugin);

  LilvWorld* const world = plugin->world;

  SordIter* const i = sord_search(world->applications,
                                  NULL,
                                  world->uris.lv2_appliesTo,
                                  lilv_plugin_get_uri(plugin)->node,
                                  NULL);
  if (sord_iter_end(i)) {
    sord_iter_free(i);
    return NULL;
  }

  LilvNodes* matches = lilv_nodes_new();
  FOREACH_MATCH (i) {
    const SordNode* node = sord_iter_get_node(i, SORD_SUBJECT);
    if (!type ||
        sord_ask(world->model, node, world->uris.rdf_type, type->node, NULL)) {
      zix_tree_insert(
        (ZixTree*)matches, lilv_node_new_from_node(world, node), NULL);
    }
  }

  sord_iter_free(i);
  return matches;
}

static SerdEnv*
new_lv2_env(const SerdNode* base)
{
  SerdEnv* env = serd_env_new(base);

#define USTR(s) ((const uint8_t*)(s))

  serd_env_set_prefix_from_strings(env, USTR("doap"), USTR(LILV_NS_DOAP));
  serd_env_set_prefix_from_strings(env, USTR("foaf"), USTR(LILV_NS_FOAF));
  serd_env_set_prefix_from_strings(env, USTR("lv2"), USTR(LILV_NS_LV2));
  serd_env_set_prefix_from_strings(env, USTR("owl"), USTR(LILV_NS_OWL));
  serd_env_set_prefix_from_strings(env, USTR("rdf"), USTR(LILV_NS_RDF));
  serd_env_set_prefix_from_strings(env, USTR("rdfs"), USTR(LILV_NS_RDFS));
  serd_env_set_prefix_from_strings(env, USTR("xsd"), USTR(LILV_NS_XSD));

  return env;
}

static void
maybe_write_prefixes(SerdWriter* writer, const SerdEnv* env, FILE* file)
{
  fseek(file, 0, SEEK_END);
  if (ftell(file) == 0) {
    serd_env_foreach(env, (SerdPrefixSink)serd_writer_set_prefix, writer);
  } else {
    fprintf(file, "\n");
  }
}

void
lilv_plugin_write_description(LilvWorld*        world,
                              const LilvPlugin* plugin,
                              const LilvNode*   base_uri,
                              FILE*             plugin_file)
{
  const LilvNode* subject   = lilv_plugin_get_uri(plugin);
  const uint32_t  num_ports = lilv_plugin_get_num_ports(plugin);
  const SerdNode* base      = sord_node_to_serd_node(base_uri->node);
  SerdEnv*        env       = new_lv2_env(base);

  SerdWriter* writer =
    serd_writer_new(SERD_TURTLE,
                    (SerdStyle)(SERD_STYLE_ABBREVIATED | SERD_STYLE_CURIED),
                    env,
                    NULL,
                    serd_file_sink,
                    plugin_file);

  // Write prefixes if this is a new file
  maybe_write_prefixes(writer, env, plugin_file);

  // Write plugin description
  SordIter* plug_iter =
    sord_search(world->model, subject->node, NULL, NULL, NULL);
  sord_write_iter(plug_iter, writer);

  // Write port descriptions
  for (uint32_t i = 0; i < num_ports; ++i) {
    const LilvPort* port = plugin->ports[i];
    SordIter*       port_iter =
      sord_search(world->model, port->node->node, NULL, NULL, NULL);
    sord_write_iter(port_iter, writer);
  }

  serd_writer_free(writer);
  serd_env_free(env);
}

void
lilv_plugin_write_manifest_entry(LilvWorld*        world,
                                 const LilvPlugin* plugin,
                                 const LilvNode*   base_uri,
                                 FILE*             manifest_file,
                                 const char*       plugin_file_path)
{
  (void)world;

  const LilvNode* subject = lilv_plugin_get_uri(plugin);
  const SerdNode* base    = sord_node_to_serd_node(base_uri->node);
  SerdEnv*        env     = new_lv2_env(base);

  SerdWriter* writer =
    serd_writer_new(SERD_TURTLE,
                    (SerdStyle)(SERD_STYLE_ABBREVIATED | SERD_STYLE_CURIED),
                    env,
                    NULL,
                    serd_file_sink,
                    manifest_file);

  // Write prefixes if this is a new file
  maybe_write_prefixes(writer, env, manifest_file);

  // Write manifest entry
  serd_writer_write_statement(
    writer,
    0,
    NULL,
    sord_node_to_serd_node(subject->node),
    sord_node_to_serd_node(plugin->world->uris.rdf_type),
    sord_node_to_serd_node(plugin->world->uris.lv2_Plugin),
    0,
    0);

  const SerdNode file_node =
    serd_node_from_string(SERD_URI, (const uint8_t*)plugin_file_path);
  serd_writer_write_statement(
    writer,
    0,
    NULL,
    sord_node_to_serd_node(subject->node),
    sord_node_to_serd_node(plugin->world->uris.rdfs_seeAlso),
    &file_node,
    0,
    0);

  serd_writer_free(writer);
  serd_env_free(env);
}
