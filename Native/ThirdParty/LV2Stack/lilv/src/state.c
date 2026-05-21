// Copyright 2007-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "lilv_config.h"
#include "lilv_internal.h"
#include "log.h"
#include "node_hash.h"
#include "node_skimmer.h"
#include "query.h"
#include "string_util.h"
#include "sys_util.h"
#include "type_skimmer.h"

#include <lilv/lilv.h>
#include <lv2/atom/atom.h>
#include <lv2/atom/forge.h>
#include <lv2/core/lv2.h>
#include <lv2/presets/presets.h>
#include <lv2/state/state.h>
#include <lv2/urid/urid.h>
#include <serd/serd.h>
#include <sord/sord.h>
#include <sratom/sratom.h>
#include <zix/allocator.h>
#include <zix/filesystem.h>
#include <zix/path.h>
#include <zix/status.h>
#include <zix/string_view.h>
#include <zix/tree.h>

#include <assert.h>
#include <errno.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#if USE_FOPEN_E_MODE
#  define FOPEN_MODE(m) m "e"
#else
#  define FOPEN_MODE(m) m
#endif

#define USTR(s) ((const uint8_t*)(s))

typedef struct {
  void*    value; ///< Value/Object
  size_t   size;  ///< Size of value
  uint32_t key;   ///< Key/Predicate (URID)
  uint32_t type;  ///< Type of value (URID)
  uint32_t flags; ///< State flags (POD, etc)
} Property;

typedef struct {
  char*     symbol; ///< Symbol of port
  LV2_Atom* atom;   ///< Value in port
} PortValue;

typedef struct {
  char* abs; ///< Absolute path of actual file
  char* rel; ///< Abstract path (relative path in state dir)
} PathMap;

typedef struct {
  size_t    n;
  Property* props;
} PropertyArray;

struct LilvStateImpl {
  LilvNode*     plugin_uri;  ///< Plugin URI
  LilvNode*     uri;         ///< State/preset URI
  char*         dir;         ///< Save directory (if saved)
  char*         scratch_dir; ///< Directory for files created by plugin
  char*         copy_dir;    ///< Directory for snapshots of external files
  char*         link_dir;    ///< Directory for links to external files
  char*         label;       ///< State/Preset label
  ZixTree*      abs2rel;     ///< PathMap sorted by abs
  ZixTree*      rel2abs;     ///< PathMap sorted by rel
  PropertyArray props;       ///< State properties
  PropertyArray metadata;    ///< State metadata
  PortValue*    values;      ///< Port values
  uint32_t      atom_Path;   ///< atom:Path URID
  uint32_t      n_values;    ///< Number of port values
};

static int
abs_cmp(const void* a, const void* b, const void* user_data)
{
  (void)user_data;

  return strcmp(((const PathMap*)a)->abs, ((const PathMap*)b)->abs);
}

static int
rel_cmp(const void* a, const void* b, const void* user_data)
{
  (void)user_data;

  return strcmp(((const PathMap*)a)->rel, ((const PathMap*)b)->rel);
}

static int
property_cmp(const void* a, const void* b)
{
  const uint32_t a_key = ((const Property*)a)->key;
  const uint32_t b_key = ((const Property*)b)->key;

  if (a_key < b_key) {
    return -1;
  }

  if (b_key < a_key) {
    return 1;
  }

  return 0;
}

static int
value_cmp(const void* a, const void* b)
{
  return strcmp(((const PortValue*)a)->symbol, ((const PortValue*)b)->symbol);
}

static void
map_free(void* ptr, const void* user_data)
{
  (void)user_data;
  free(((PathMap*)ptr)->abs);
  free(((PathMap*)ptr)->rel);
  free(ptr);
}

static PortValue*
append_port_value(LilvState*  state,
                  const char* port_symbol,
                  const void* value,
                  uint32_t    size,
                  uint32_t    type)
{
  PortValue* pv = NULL;
  if (value) {
    state->values = (PortValue*)realloc(
      state->values, (++state->n_values) * sizeof(PortValue));

    pv             = &state->values[state->n_values - 1];
    pv->symbol     = lilv_strdup(port_symbol);
    pv->atom       = (LV2_Atom*)malloc(sizeof(LV2_Atom) + size);
    pv->atom->size = size;
    pv->atom->type = type;
    memcpy(pv->atom + 1, value, size);
  }
  return pv;
}

static ZixTreeIter*
find_path(const LilvState* state, const char* path)
{
  ZixTreeIter*  i = NULL;
  const PathMap k = {NULL, (char*)path};
  return (state->rel2abs && !zix_tree_find(state->rel2abs, &k, &i)) ? i : NULL;
}

static const char*
resolve_path(const LilvState* state, const char* path)
{
  ZixTreeIter* const i = find_path(state, path);
  return i ? ((const PathMap*)zix_tree_get(i))->abs : path;
}

static void
append_property(const LilvState* state,
                PropertyArray*   array,
                uint32_t         key,
                const void*      value,
                size_t           size,
                uint32_t         type,
                uint32_t         flags)
{
  array->props =
    (Property*)realloc(array->props, (++array->n) * sizeof(Property));

  Property* const prop = &array->props[array->n - 1];
  if ((flags & LV2_STATE_IS_POD) || type == state->atom_Path) {
    prop->value = malloc(size);
    memcpy(prop->value, value, size);
  } else {
    prop->value = (void*)value;
  }

  prop->size  = size;
  prop->key   = key;
  prop->type  = type;
  prop->flags = flags;
}

static const Property*
find_property(const LilvState* const state, const uint32_t key)
{
  if (!state->props.props) {
    return NULL;
  }

  const Property search_key = {NULL, 0, key, 0, 0};

  return (const Property*)bsearch(&search_key,
                                  state->props.props,
                                  state->props.n,
                                  sizeof(Property),
                                  property_cmp);
}

static LV2_State_Status
store_callback(LV2_State_Handle handle,
               uint32_t         key,
               const void*      value,
               size_t           size,
               uint32_t         type,
               uint32_t         flags)
{
  LilvState* const state = (LilvState*)handle;

  if (!key) {
    return LV2_STATE_ERR_UNKNOWN; // TODO: Add status for bad arguments
  }

  if (find_property((const LilvState*)handle, key)) {
    return LV2_STATE_ERR_UNKNOWN; // TODO: Add status for duplicate keys
  }

  append_property(state, &state->props, key, value, size, type, flags);
  return LV2_STATE_SUCCESS;
}

static const void*
retrieve_callback(LV2_State_Handle handle,
                  uint32_t         key,
                  size_t*          size,
                  uint32_t*        type,
                  uint32_t*        flags)
{
  const Property* const prop = find_property((const LilvState*)handle, key);

  if (prop) {
    if (size) {
      *size = prop->size;
    }
    if (type) {
      *type = prop->type;
    }
    if (flags) {
      *flags = prop->flags;
    }
    return prop->value;
  }
  return NULL;
}

static LilvPathStatus
file_path_exists(const char* path, const void* handle)
{
  (void)handle;

  return (zix_file_type(path) != ZIX_FILE_TYPE_NONE) ? LILV_PATH_IS_TAKEN
                                                     : LILV_PATH_IS_FREE;
}

static LilvPathStatus
state_path_exists(const char* path, const void* handle)
{
  return !!find_path((const LilvState*)handle, path) ? LILV_PATH_IS_TAKEN
                                                     : LILV_PATH_IS_FREE;
}

static char*
make_path(LV2_State_Make_Path_Handle handle, const char* path)
{
  const LilvState* state = (const LilvState*)handle;
  return !zix_create_directories(NULL, state->dir)
           ? zix_path_join(NULL, state->dir, path)
           : NULL;
}

static bool
is_dir_sep(const char c)
{
  if (c == '/') {
    return true;
  }

#ifdef _WIN32
  return c == '\\';
#endif

  return false;
}

static bool
path_is_child(const char* path, const char* dir)
{
  if (path && dir) {
    size_t i        = 0U;
    char   dir_last = '\0';
    for (; dir[i]; ++i) {
      dir_last = dir[i];
      if (path[i] != dir[i]) {
        return false;
      }
    }

    return is_dir_sep(dir_last) || is_dir_sep(path[i]);
  }

  return false;
}

static char*
abstract_path(LV2_State_Map_Path_Handle handle, const char* abs_path)
{
  LilvState* state     = (LilvState*)handle;
  char*      path      = NULL;
  char*      real_path = zix_canonical_path(NULL, abs_path);
  if (!real_path) {
    real_path = zix_path_lexically_normal(NULL, abs_path);
  }

  const PathMap key  = {real_path, NULL};
  ZixTreeIter*  iter = NULL;

  if (abs_path[0] == '\0') {
    return lilv_strdup(abs_path);
  }

  if (!zix_tree_find(state->abs2rel, &key, &iter)) {
    // Already mapped path in a previous call
    const PathMap* pm = (const PathMap*)zix_tree_get(iter);
    zix_free(NULL, real_path);
    return lilv_strdup(pm->rel);
  }

  if (path_is_child(real_path, state->dir)) {
    // File in state directory (loaded, or created by plugin during save)
    path = zix_path_lexically_relative(NULL, real_path, state->dir);
  } else if (path_is_child(real_path, state->scratch_dir)) {
    // File created by plugin earlier
    path = zix_path_lexically_relative(NULL, real_path, state->scratch_dir);
    if (state->copy_dir) {
      ZixStatus st = zix_create_directories(NULL, state->copy_dir);
      if (st) {
        LILV_ERRORF("Error creating directory %s (%s)\n",
                    state->copy_dir,
                    zix_strerror(st));
      }

      char* copy_path = zix_path_join(NULL, state->copy_dir, path);
      char* copy      = lilv_get_latest_copy(real_path, copy_path);
      if (!copy || !zix_file_equals(NULL, real_path, copy)) {
        // No recent enough copy, make a new one
        free(copy);
        copy = lilv_find_path(copy_path, file_path_exists, NULL);
        if ((st = zix_copy_file(NULL, real_path, copy, 0U))) {
          LILV_ERRORF(
            "Error copying state file %s (%s)\n", copy, zix_strerror(st));
        }
      }
      zix_free(NULL, real_path);
      zix_free(NULL, copy_path);

      // Refer to the latest copy in plugin state
      real_path = copy;
    }
  } else if (state->link_dir) {
    // New path outside state directory, make a link
    const ZixStringView name = zix_path_filename(real_path);

    // Find a free name in the (virtual) state directory
    path = lilv_find_path(name.data, state_path_exists, state);
  } else {
    // No link directory, preserve absolute path
    path = lilv_strdup(abs_path);
  }

  // Add record to path mapping
  PathMap* pm = (PathMap*)malloc(sizeof(PathMap));
  pm->abs     = real_path;
  pm->rel     = lilv_strdup(path);
  zix_tree_insert(state->abs2rel, pm, NULL);
  zix_tree_insert(state->rel2abs, pm, NULL);

  return path;
}

static char*
absolute_path(LV2_State_Map_Path_Handle handle, const char* state_path)
{
  const LilvState* state = (const LilvState*)handle;
  char*            path  = NULL;
  if (zix_path_is_absolute(state_path)) {
    // Absolute path, return identical path
    path = lilv_strdup(state_path);
  } else if (state->dir) {
    // Relative path inside state directory
    path = zix_path_join(NULL, state->dir, state_path);
  } else {
    // State has not been saved, unmap
    path = lilv_strdup(resolve_path(state, state_path));
  }

  return path;
}

// Return a new features array with built-in features added to `features`
static const LV2_Feature**
add_features(const LV2_Feature* const* features,
             const LV2_Feature*        map,
             const LV2_Feature*        make,
             const LV2_Feature*        free)
{
  size_t n_features = 0;
  for (; features && features[n_features]; ++n_features) {
    if (!strcmp(features[n_features]->URI, LV2_STATE__mapPath)) {
      map = NULL;
    }
    if (!strcmp(features[n_features]->URI, LV2_STATE__makePath)) {
      make = NULL;
    }
    if (!strcmp(features[n_features]->URI, LV2_STATE__freePath)) {
      free = NULL;
    }
  }

  const LV2_Feature** ret =
    (const LV2_Feature**)calloc(n_features + 4, sizeof(LV2_Feature*));

  if (features) {
    memcpy(ret, features, n_features * sizeof(LV2_Feature*));
  }

  size_t i = n_features;
  if (map) {
    ret[i++] = map;
  }
  if (make) {
    ret[i++] = make;
  }
  if (free) {
    ret[i++] = free;
  }

  return ret;
}

// Return a normal path for a directory with a trailing separator
static char*
normal_dir(const char* path)
{
  char* const normal_path = zix_path_lexically_normal(NULL, path);
  char* const base_path   = zix_path_join(NULL, normal_path, NULL);

  zix_free(NULL, normal_path);
  return base_path;
}

static const char*
state_strerror(LV2_State_Status st)
{
  switch (st) {
  case LV2_STATE_SUCCESS:
    return "Completed successfully";
  case LV2_STATE_ERR_BAD_TYPE:
    return "Unsupported type";
  case LV2_STATE_ERR_BAD_FLAGS:
    return "Unsupported flags";
  case LV2_STATE_ERR_NO_FEATURE:
    return "Missing features";
  case LV2_STATE_ERR_NO_PROPERTY:
    return "Missing property";
  default:
    return "Unknown error";
  }
}

static void
free_path(LV2_State_Free_Path_Handle handle, char* path)
{
  (void)handle;

  lilv_free(path);
}

LilvState*
lilv_state_new_from_instance(const LilvPlugin*         plugin,
                             LilvInstance*             instance,
                             LV2_URID_Map*             map,
                             const char*               scratch_dir,
                             const char*               copy_dir,
                             const char*               link_dir,
                             const char*               save_dir,
                             LilvGetPortValueFunc      get_value,
                             void*                     user_data,
                             uint32_t                  flags,
                             const LV2_Feature* const* features)
{
  const LV2_Feature** sfeatures = NULL;
  LilvWorld* const    world     = plugin->world;
  LilvState* const    state     = (LilvState*)calloc(1, sizeof(LilvState));
  state->plugin_uri  = lilv_node_duplicate(lilv_plugin_get_uri(plugin));
  state->abs2rel     = zix_tree_new(NULL, false, abs_cmp, NULL, map_free, NULL);
  state->rel2abs     = zix_tree_new(NULL, false, rel_cmp, NULL, NULL, NULL);
  state->scratch_dir = scratch_dir ? normal_dir(scratch_dir) : NULL;
  state->copy_dir    = copy_dir ? normal_dir(copy_dir) : NULL;
  state->link_dir    = link_dir ? normal_dir(link_dir) : NULL;
  state->dir         = save_dir ? normal_dir(save_dir) : NULL;
  state->atom_Path   = map->map(map->handle, LV2_ATOM__Path);

  LV2_State_Map_Path  pmap          = {state, abstract_path, absolute_path};
  LV2_Feature         pmap_feature  = {LV2_STATE__mapPath, &pmap};
  LV2_State_Make_Path pmake         = {state, make_path};
  LV2_Feature         pmake_feature = {LV2_STATE__makePath, &pmake};
  LV2_State_Free_Path pfree         = {NULL, free_path};
  LV2_Feature         pfree_feature = {LV2_STATE__freePath, &pfree};
  features = sfeatures = add_features(
    features, &pmap_feature, save_dir ? &pmake_feature : NULL, &pfree_feature);

  // Store port values
  if (get_value) {
    LilvNode* lv2_ControlPort = lilv_new_uri(world, LILV_URI_CONTROL_PORT);
    LilvNode* lv2_InputPort   = lilv_new_uri(world, LILV_URI_INPUT_PORT);
    for (uint32_t i = 0; i < plugin->num_ports; ++i) {
      const LilvPort* const port = plugin->ports[i];
      if (lilv_port_is_a(plugin, port, lv2_ControlPort) &&
          lilv_port_is_a(plugin, port, lv2_InputPort)) {
        uint32_t    size  = 0;
        uint32_t    type  = 0;
        const char* sym   = lilv_node_as_string(port->symbol);
        const void* value = get_value(sym, user_data, &size, &type);
        append_port_value(state, sym, value, size, type);
      }
    }
    lilv_node_free(lv2_ControlPort);
    lilv_node_free(lv2_InputPort);
  }

  // Store properties
  const LV2_Descriptor*      desc = instance->lv2_descriptor;
  const LV2_State_Interface* iface =
    (desc->extension_data)
      ? (const LV2_State_Interface*)desc->extension_data(LV2_STATE__interface)
      : NULL;

  if (iface) {
    LV2_State_Status st =
      iface->save(instance->lv2_handle, store_callback, state, flags, features);
    if (st) {
      LILV_ERRORF("Error saving plugin state: %s\n", state_strerror(st));
      free(state->props.props);
      state->props.props = NULL;
      state->props.n     = 0;
    } else {
      qsort(state->props.props, state->props.n, sizeof(Property), property_cmp);
    }
  }

  if (state->values) {
    qsort(state->values, state->n_values, sizeof(PortValue), value_cmp);
  }

  free(sfeatures);
  return state;
}

void
lilv_state_emit_port_values(const LilvState*     state,
                            LilvSetPortValueFunc set_value,
                            void*                user_data)
{
  for (uint32_t i = 0; i < state->n_values; ++i) {
    const PortValue* value = &state->values[i];
    const LV2_Atom*  atom  = value->atom;
    set_value(value->symbol, user_data, atom + 1, atom->size, atom->type);
  }
}

void
lilv_state_restore(const LilvState*          state,
                   LilvInstance*             instance,
                   LilvSetPortValueFunc      set_value,
                   void*                     user_data,
                   uint32_t                  flags,
                   const LV2_Feature* const* features)
{
  if (!state) {
    LILV_ERROR("lilv_state_restore() called on NULL state\n");
    return;
  }

  void* const         handle           = (void*)state;
  LV2_State_Map_Path  map_path         = {handle, abstract_path, absolute_path};
  LV2_Feature         map_path_feature = {LV2_STATE__mapPath, &map_path};
  LV2_State_Free_Path pfree            = {handle, free_path};
  LV2_Feature         pfree_feature    = {LV2_STATE__freePath, &pfree};

  if (instance) {
    const LV2_Descriptor* desc = instance->lv2_descriptor;
    if (desc->extension_data) {
      const LV2_State_Interface* iface =
        (const LV2_State_Interface*)desc->extension_data(LV2_STATE__interface);

      if (iface && iface->restore) {
        const LV2_Feature** sfeatures =
          add_features(features, &map_path_feature, NULL, &pfree_feature);

        iface->restore(instance->lv2_handle,
                       retrieve_callback,
                       (LV2_State_Handle)state,
                       flags,
                       sfeatures);

        free(sfeatures);
      }
    }
  }

  if (set_value) {
    lilv_state_emit_port_values(state, set_value, user_data);
  }
}

static void
set_state_dir_from_model(LilvState* state, const SordNode* graph)
{
  if (!state->dir && graph) {
    const char* uri  = (const char*)sord_node_get_string(graph);
    char*       path = lilv_file_uri_parse(uri, NULL);

    state->dir = zix_path_join(NULL, path, NULL);
    free(path);
  }
  assert(!state->dir || zix_path_is_absolute(state->dir));
}

static LilvState*
new_state_from_model(LilvWorld*      world,
                     LV2_URID_Map*   map,
                     SordModel*      model,
                     const SordNode* node,
                     const char*     dir)
{
  // Check that we know at least something about this state subject
  if (!sord_ask(model, node, 0, 0, 0)) {
    return NULL;
  }

  // Allocate state
  LilvState* const state = (LilvState*)calloc(1, sizeof(LilvState));
  state->dir             = dir ? zix_path_join(NULL, dir, NULL) : NULL;
  state->atom_Path       = map->map(map->handle, LV2_ATOM__Path);
  state->uri             = lilv_node_new_from_node(world, node);

  // Get the plugin URI this state applies to
  SordIter* i = NULL;
  if (sord_ask(model, node, world->uris.rdf_type, world->uris.lv2_Plugin, 0)) {
    // Loading plugin description as state (default state)
    state->plugin_uri = lilv_node_new_from_node(world, node);

    // Load plugin data if necessary
    const LilvPlugin* const plugin =
      lilv_plugins_get_by_uri(world->plugins, state->plugin_uri);
    if (plugin) {
      lilv_plugin_load_if_necessary(plugin);
    }
  } else if ((i = sord_search(model, node, world->uris.lv2_appliesTo, 0, 0))) {
    const SordNode* object = sord_iter_get_node(i, SORD_OBJECT);
    const SordNode* graph  = sord_iter_get_node(i, SORD_GRAPH);
    state->plugin_uri      = lilv_node_new_from_node(world, object);
    set_state_dir_from_model(state, graph);
    sord_iter_free(i);
  } else {
    LILV_ERRORF("State <%s> missing lv2:appliesTo property\n",
                sord_node_get_string(node));
  }

  // Get the state label
  i = sord_search(model, node, world->uris.rdfs_label, NULL, NULL);
  if (i) {
    const SordNode* object = sord_iter_get_node(i, SORD_OBJECT);
    const SordNode* graph  = sord_iter_get_node(i, SORD_GRAPH);
    state->label = lilv_strdup((const char*)sord_node_get_string(object));
    set_state_dir_from_model(state, graph);
    sord_iter_free(i);
  }

  Sratom*        sratom = sratom_new(map);
  SerdChunk      chunk  = {NULL, 0};
  LV2_Atom_Forge forge;
  lv2_atom_forge_init(&forge, map);
  lv2_atom_forge_set_sink(
    &forge, sratom_forge_sink, sratom_forge_deref, &chunk);

  // Get port values
  SordIter* ports = sord_search(model, node, world->uris.lv2_port, 0, 0);
  FOREACH_MATCH (ports) {
    const SordNode* port = sord_iter_get_node(ports, SORD_OBJECT);

    SordNode* label  = sord_get(model, port, world->uris.rdfs_label, 0, 0);
    SordNode* symbol = sord_get(model, port, world->uris.lv2_symbol, 0, 0);
    SordNode* value  = sord_get(model, port, world->uris.pset_value, 0, 0);
    if (!value) {
      value = sord_get(model, port, world->uris.lv2_default, 0, 0);
    }
    if (!symbol) {
      LILV_ERRORF("State <%s> port missing symbol\n",
                  sord_node_get_string(node));
    } else if (value) {
      chunk.len = 0;
      sratom_read(sratom, &forge, world->world, model, value);
      const LV2_Atom* atom = (const LV2_Atom*)chunk.buf;
      if (atom) {
        append_port_value(state,
                          (const char*)sord_node_get_string(symbol),
                          LV2_ATOM_BODY_CONST(atom),
                          atom->size,
                          atom->type);
      }

      if (label) {
        lilv_state_set_label(state, (const char*)sord_node_get_string(label));
      }
    }
    sord_node_free(world->world, value);
    sord_node_free(world->world, symbol);
    sord_node_free(world->world, label);
  }
  sord_iter_free(ports);

  // Get properties
  SordNode* state_node =
    sord_get(model, node, world->uris.state_state, NULL, NULL);
  if (state_node) {
    SordIter* props = sord_search(model, state_node, 0, 0, 0);
    FOREACH_MATCH (props) {
      const SordNode* p   = sord_iter_get_node(props, SORD_PREDICATE);
      const SordNode* o   = sord_iter_get_node(props, SORD_OBJECT);
      const char*     key = (const char*)sord_node_get_string(p);

      chunk.len = 0;
      lv2_atom_forge_set_sink(
        &forge, sratom_forge_sink, sratom_forge_deref, &chunk);

      sratom_read(sratom, &forge, world->world, model, o);
      const LV2_Atom* atom  = (const LV2_Atom*)chunk.buf;
      uint32_t        flags = LV2_STATE_IS_POD | LV2_STATE_IS_PORTABLE;
      Property        prop  = {NULL, 0, 0, 0, flags};
      if (!atom) {
        continue;
      }

      prop.key   = map->map(map->handle, key);
      prop.type  = atom->type;
      prop.size  = atom->size;
      prop.value = malloc(atom->size);
      memcpy(prop.value, LV2_ATOM_BODY_CONST(atom), atom->size);
      if (atom->type == forge.Path) {
        prop.flags = LV2_STATE_IS_POD;
      }

      if (prop.value) {
        state->props.props = (Property*)realloc(
          state->props.props, (++state->props.n) * sizeof(Property));
        state->props.props[state->props.n - 1] = prop;
      }
    }
    sord_iter_free(props);
  }
  sord_node_free(world->world, state_node);

  serd_free((void*)chunk.buf);
  sratom_free(sratom);

  if (state->props.props) {
    qsort(state->props.props, state->props.n, sizeof(Property), property_cmp);
  }
  if (state->values) {
    qsort(state->values, state->n_values, sizeof(PortValue), value_cmp);
  }

  return state;
}

LilvState*
lilv_state_new_from_world(LilvWorld*      world,
                          LV2_URID_Map*   map,
                          const LilvNode* node)
{
  if (!lilv_node_is_uri(node) && !lilv_node_is_blank(node)) {
    LILV_ERRORF("Subject \"%s\" is not a URI or blank node\n",
                lilv_node_as_string(node));
    return NULL;
  }

  return new_state_from_model(world, map, world->model, node->node, NULL);
}

LilvState*
lilv_state_new_from_file(LilvWorld*      world,
                         LV2_URID_Map*   map,
                         const LilvNode* subject,
                         const char*     path)
{
  if (subject && !lilv_node_is_uri(subject) && !lilv_node_is_blank(subject)) {
    LILV_ERRORF("Subject \"%s\" is not a URI or blank node\n",
                lilv_node_as_string(subject));
    return NULL;
  }

  char* const abs_path = zix_canonical_path(NULL, path);
  if (!abs_path) {
    return NULL;
  }

  const uint8_t* file_path     = (const uint8_t*)abs_path;
  char*          dir_path      = NULL;
  char*          manifest_path = NULL;
  if (zix_file_type(abs_path) == ZIX_FILE_TYPE_DIRECTORY) {
    dir_path      = zix_path_join(NULL, abs_path, NULL);
    manifest_path = zix_path_join(NULL, dir_path, "manifest.ttl");
    file_path     = (uint8_t*)manifest_path;
  } else {
    char* const parent = zix_string_view_copy(NULL, zix_path_parent_path(path));

    dir_path = zix_path_join(NULL, parent, NULL);

    zix_free(NULL, parent);
  }

  SordModel* model    = sord_new(world->world, SORD_SPO, false);
  SerdNode   file_uri = serd_node_new_file_uri(file_path, NULL, NULL, true);

  NodeHash*          plugins = NULL;
  NodeHash*          presets = NULL;
  TypeSkimmer* const skimmer = type_skimmer_new(world->world,
                                                &world->uris,
                                                &file_uri,
                                                model,
                                                &plugins,
                                                &presets,
                                                NULL,
                                                NULL,
                                                NULL,
                                                NULL);

  serd_reader_read_file(skimmer->base.reader, (const uint8_t*)file_uri.buf);

  const SordNode* subject_node = subject ? subject->node : NULL;
  if (!subject_node) {
    if (presets && lilv_node_hash_size(presets) == 1U) {
      subject_node = lilv_node_hash_get(presets, lilv_node_hash_begin(presets));
    } else if (plugins && lilv_node_hash_size(plugins) == 1U) {
      subject_node = lilv_node_hash_get(plugins, lilv_node_hash_begin(plugins));
    }
  }

  // Load any rdfs:seeAlso files
  NodeHash* const files = lilv_hash_from_matches(
    model, subject_node, world->uris.rdfs_seeAlso, NULL, NULL);
  NODE_HASH_FOREACH (f, files) {
    const SordNode* const link      = lilv_node_hash_get(files, f);
    const char* const     link_uri  = (const char*)sord_node_get_string(link);
    char* const           link_path = lilv_file_uri_parse(link_uri, NULL);

    serd_env_set_base_uri(skimmer->base.env, sord_node_to_serd_node(link));
    serd_reader_read_file(skimmer->base.reader, (const uint8_t*)link_path);
    lilv_free(link_path);
  }
  lilv_node_hash_free(files, world->world);

  LilvState* const state =
    new_state_from_model(world, map, model, subject_node, dir_path);

  type_skimmer_free(skimmer);
  lilv_node_hash_free(presets, world->world);
  lilv_node_hash_free(plugins, world->world);
  serd_node_free(&file_uri);
  zix_free(NULL, manifest_path);
  zix_free(NULL, dir_path);
  zix_free(NULL, abs_path);
  sord_free(model);
  return state;
}

static void
set_prefixes(SerdEnv* env)
{
#define SET_PSET(e, p, u) serd_env_set_prefix_from_strings(e, p, u)
  SET_PSET(env, USTR("atom"), USTR(LV2_ATOM_PREFIX));
  SET_PSET(env, USTR("lv2"), USTR(LV2_CORE_PREFIX));
  SET_PSET(env, USTR("pset"), USTR(LV2_PRESETS_PREFIX));
  SET_PSET(env, USTR("rdf"), USTR(LILV_NS_RDF));
  SET_PSET(env, USTR("rdfs"), USTR(LILV_NS_RDFS));
  SET_PSET(env, USTR("state"), USTR(LV2_STATE_PREFIX));
  SET_PSET(env, USTR("xsd"), USTR(LILV_NS_XSD));
}

LilvState*
lilv_state_new_from_string(LilvWorld* world, LV2_URID_Map* map, const char* str)
{
  if (!str) {
    return NULL;
  }

  SordModel* model = sord_new(world->world, SORD_SPO, false);

  NodeSkimmer* skimmer = node_skimmer_new(world->world,
                                          &SERD_NODE_NULL,
                                          model,
                                          SORD_OBJECT,
                                          world->uris.pset_Preset,
                                          world->uris.rdf_type,
                                          false);

  set_prefixes(skimmer->base.env);
  serd_reader_read_string(skimmer->base.reader, USTR(str));

  NodeHash* const nodes = node_skimmer_free(skimmer);
  LilvState*      state = NULL;
  if (nodes && lilv_node_hash_size(nodes) == 1U) {
    const SordNode* const node =
      lilv_node_hash_get(nodes, lilv_node_hash_begin(nodes));
    state = new_state_from_model(world, map, model, node, NULL);
  }

  lilv_node_hash_free(nodes, world->world);
  sord_free(model);

  return state;
}

static SerdWriter*
ttl_writer(SerdSink sink, void* stream, const SerdNode* base, SerdEnv** new_env)
{
  SerdURI base_uri = SERD_URI_NULL;
  if (base && base->buf) {
    serd_uri_parse((const uint8_t*)base->buf, &base_uri);
  }

  SerdEnv* env = *new_env ? *new_env : serd_env_new(base);
  set_prefixes(env);

  SerdWriter* writer =
    serd_writer_new(SERD_TURTLE,
                    (SerdStyle)(SERD_STYLE_RESOLVED | SERD_STYLE_ABBREVIATED |
                                SERD_STYLE_CURIED),
                    env,
                    &base_uri,
                    sink,
                    stream);

  if (!*new_env) {
    *new_env = env;
  }

  return writer;
}

static SerdWriter*
ttl_file_writer(FILE* fd, const SerdNode* node, SerdEnv** env)
{
  SerdWriter* writer = ttl_writer(serd_file_sink, fd, node, env);

  fseek(fd, 0, SEEK_END);
  if (ftell(fd) == 0) {
    serd_env_foreach(*env, (SerdPrefixSink)serd_writer_set_prefix, writer);
  } else {
    fprintf(fd, "\n");
  }

  return writer;
}

static void
add_to_model(SordWorld*     world,
             SerdEnv*       env,
             SordModel*     model,
             const SerdNode s,
             const SerdNode p,
             const SerdNode o)
{
  SordNode* ss = sord_node_from_serd_node(world, env, &s, NULL, NULL);
  SordNode* sp = sord_node_from_serd_node(world, env, &p, NULL, NULL);
  SordNode* so = sord_node_from_serd_node(world, env, &o, NULL, NULL);

  SordQuad quad = {ss, sp, so, NULL};
  sord_add(model, quad);

  sord_node_free(world, ss);
  sord_node_free(world, sp);
  sord_node_free(world, so);
}

static void
remove_manifest_entry(SordWorld* world, SordModel* model, const char* subject)
{
  SordNode* s = sord_new_uri(world, USTR(subject));
  SordIter* i = sord_search(model, s, NULL, NULL, NULL);
  while (!sord_iter_end(i)) {
    (void)sord_erase(model, i);
  }
  sord_iter_free(i);
  sord_node_free(world, s);
}

static int
write_manifest(LilvWorld*      world,
               SerdEnv*        env,
               SordModel*      model,
               const SerdNode* file_uri)
{
  (void)world;

  char* const path =
    (char*)serd_file_uri_parse((const uint8_t*)file_uri->buf, NULL);

  FILE* const wfd = path ? fopen(path, FOPEN_MODE("wb")) : NULL;
  if (!wfd) {
    LILV_ERRORF(
      "Failed to open \"%s\" for writing (%s)\n", path, strerror(errno));
    serd_free(path);
    return 1;
  }

  SerdWriter* writer = ttl_file_writer(wfd, file_uri, &env);
  sord_write(model, writer, NULL);
  serd_writer_free(writer);
  fclose(wfd);
  serd_free(path);
  return 0;
}

static int
add_state_to_manifest(LilvWorld*      lworld,
                      const LilvNode* plugin_uri,
                      const char*     manifest_path,
                      const char*     state_uri,
                      const char*     state_path)
{
  SordWorld* world    = lworld->world;
  SerdNode   manifest = serd_node_new_file_uri(USTR(manifest_path), 0, 0, 1);
  SerdNode   file     = serd_node_new_file_uri(USTR(state_path), 0, 0, 1);
  SerdEnv*   env      = serd_env_new(&manifest);
  SordModel* model    = sord_new(world, SORD_SPO, false);

  const uint8_t* const manifest_uri = manifest.buf;
  if (manifest_uri && zix_file_type(manifest_path) == ZIX_FILE_TYPE_REGULAR) {
    // Read manifest into model
    SerdReader* reader = sord_new_reader(model, env, SERD_TURTLE, NULL);
    SerdStatus  st     = serd_reader_read_file(reader, manifest_uri);
    if (st) {
      LILV_WARNF("Failed to read manifest (%s)\n", serd_strerror(st));
    }
    serd_reader_free(reader);
  }

  // Choose state URI (use file URI if not given)
  if (!state_uri) {
    state_uri = (const char*)file.buf;
  }

  // Remove any existing manifest entries for this state
  remove_manifest_entry(world, model, state_uri);

  // Add manifest entry for this state to model
  SerdNode s = serd_node_from_string(SERD_URI, USTR(state_uri));

  // <state> a pset:Preset
  add_to_model(world,
               env,
               model,
               s,
               serd_node_from_string(SERD_URI, USTR(LILV_NS_RDF "type")),
               serd_node_from_string(SERD_URI, USTR(LV2_PRESETS__Preset)));

  // <state> a pset:Preset
  add_to_model(world,
               env,
               model,
               s,
               serd_node_from_string(SERD_URI, USTR(LILV_NS_RDF "type")),
               serd_node_from_string(SERD_URI, USTR(LV2_PRESETS__Preset)));

  // <state> rdfs:seeAlso <file>
  add_to_model(world,
               env,
               model,
               s,
               serd_node_from_string(SERD_URI, USTR(LILV_NS_RDFS "seeAlso")),
               file);

  // <state> lv2:appliesTo <plugin>
  add_to_model(
    world,
    env,
    model,
    s,
    serd_node_from_string(SERD_URI, USTR(LV2_CORE__appliesTo)),
    serd_node_from_string(SERD_URI, USTR(lilv_node_as_string(plugin_uri))));

  /* Re-open manifest for locked writing.  We need to do this because it may
     need to be truncated, and the file can only be open once on Windows. */

  FILE* const wfd = fopen(manifest_path, FOPEN_MODE("wb"));
  int         r   = 0;
  if (!wfd) {
    LILV_ERRORF("Failed to open \"%s\" for writing (%s)\n",
                manifest_path,
                strerror(errno));
    r = 1;
  } else {
    SerdWriter* writer = ttl_file_writer(wfd, &manifest, &env);
    zix_file_lock(wfd, ZIX_FILE_LOCK_BLOCK);
    sord_write(model, writer, NULL);
    zix_file_unlock(wfd, ZIX_FILE_LOCK_BLOCK);
    serd_writer_free(writer);
    fclose(wfd);
  }

  sord_free(model);
  serd_node_free(&file);
  serd_node_free(&manifest);
  serd_env_free(env);

  return r;
}

static LilvPathStatus
link_exists_to(const char* path, const void* data)
{
  if (zix_symlink_type(path) == ZIX_FILE_TYPE_NONE) {
    return LILV_PATH_IS_FREE;
  }

  const char* target    = (const char*)data;
  char* const real_path = zix_canonical_path(NULL, path);
  const bool  matches   = real_path && !strcmp(real_path, target);
  zix_free(NULL, real_path);

  return matches ? LILV_PATH_SHOULD_REUSE : LILV_PATH_IS_TAKEN;
}

static ZixStatus
create_link(const char* target_path, const char* link_path)
{
  ZixStatus           st          = ZIX_STATUS_SUCCESS;
  const ZixStringView parent_path = zix_path_parent_path(link_path);
  char* const         parent      = zix_string_view_copy(NULL, parent_path);

  char* const rel = zix_path_lexically_relative(NULL, target_path, parent);
  if ((st = zix_create_symlink(rel, link_path))) {
    if ((st = zix_create_hard_link(target_path, link_path))) {
      LILV_ERRORF("Failed to link \"%s\" => \"%s\" (%s)\n",
                  link_path,
                  target_path,
                  zix_strerror(st));
    }
  }

  zix_free(NULL, rel);
  zix_free(NULL, parent);
  return st;
}

static ZixStatus
maybe_symlink(const char* target_path, const char* link_path)
{
  ZixStatus            st  = ZIX_STATUS_SUCCESS;
  const LilvPathStatus pst = link_exists_to(link_path, target_path);

  if (pst == LILV_PATH_SHOULD_REUSE) {
    return ZIX_STATUS_SUCCESS; // Link exists and points to the correct target
  }

  if (pst == LILV_PATH_IS_TAKEN) {
    st = zix_remove(link_path);
  }

  return st ? st : create_link(target_path, link_path);
}

static void
write_property_array(const LilvState*     state,
                     const PropertyArray* array,
                     Sratom*              sratom,
                     uint32_t             flags,
                     const SerdNode*      subject,
                     LV2_URID_Unmap*      unmap,
                     const char*          dir)
{
  for (uint32_t i = 0; i < array->n; ++i) {
    Property*   prop = &array->props[i];
    const char* key  = unmap->unmap(unmap->handle, prop->key);

    const SerdNode p = serd_node_from_string(SERD_URI, USTR(key));
    if (prop->type == state->atom_Path && !dir) {
      const char* path     = (const char*)prop->value;
      const char* abs_path = resolve_path(state, path);
      LILV_WARNF("Writing absolute path %s\n", abs_path);
      sratom_write(sratom,
                   unmap,
                   flags,
                   subject,
                   &p,
                   prop->type,
                   strlen(abs_path) + 1,
                   abs_path);
    } else if ((prop->flags & LV2_STATE_IS_POD) ||
               prop->type == state->atom_Path) {
      sratom_write(
        sratom, unmap, flags, subject, &p, prop->type, prop->size, prop->value);
    } else {
      LILV_WARNF("Lost non-POD property <%s> on save\n", key);
    }
  }
}

static int
write_state_description(LV2_URID_Map*    map,
                        LV2_URID_Unmap*  unmap,
                        const LilvState* state,
                        SerdWriter*      writer,
                        const char*      uri,
                        const char*      dir)
{
  SerdNode lv2_appliesTo =
    serd_node_from_string(SERD_CURIE, USTR("lv2:appliesTo"));

  const SerdNode* plugin_uri = sord_node_to_serd_node(state->plugin_uri->node);

  SerdNode subject = serd_node_from_string(SERD_URI, USTR(uri ? uri : ""));

  // <subject> a pset:Preset
  SerdNode p = serd_node_from_string(SERD_URI, USTR(LILV_NS_RDF "type"));
  SerdNode o = serd_node_from_string(SERD_URI, USTR(LV2_PRESETS__Preset));
  serd_writer_write_statement(writer, 0, NULL, &subject, &p, &o, NULL, NULL);

  // <subject> lv2:appliesTo <http://example.org/plugin>
  serd_writer_write_statement(
    writer, 0, NULL, &subject, &lv2_appliesTo, plugin_uri, NULL, NULL);

  // <subject> rdfs:label label
  if (state->label) {
    p = serd_node_from_string(SERD_URI, USTR(LILV_NS_RDFS "label"));
    o = serd_node_from_string(SERD_LITERAL, USTR(state->label));
    serd_writer_write_statement(writer, 0, NULL, &subject, &p, &o, NULL, NULL);
  }

  const SerdEnv*  env  = serd_writer_get_env(writer);
  const SerdNode* base = serd_env_get_base_uri(env, NULL);

  Sratom* sratom = sratom_new(map);
  sratom_set_sink(sratom,
                  (const char*)base->buf,
                  (SerdStatementSink)serd_writer_write_statement,
                  (SerdEndSink)serd_writer_end_anon,
                  writer);

  // Write metadata
  sratom_set_pretty_numbers(sratom, false); // Use precise types
  write_property_array(
    state, &state->metadata, sratom, 0, &subject, unmap, dir);

  // Write port values
  sratom_set_pretty_numbers(sratom, true); // Use pretty numbers
  for (uint32_t i = 0; i < state->n_values; ++i) {
    const PortValue* const value = &state->values[i];

    const SerdNode port =
      serd_node_from_string(SERD_BLANK, USTR(value->symbol));

    // <> lv2:port _:symbol
    p = serd_node_from_string(SERD_URI, USTR(LV2_CORE__port));
    serd_writer_write_statement(
      writer, SERD_ANON_O_BEGIN, NULL, &subject, &p, &port, NULL, NULL);

    // _:symbol lv2:symbol "symbol"
    p = serd_node_from_string(SERD_URI, USTR(LV2_CORE__symbol));
    o = serd_node_from_string(SERD_LITERAL, USTR(value->symbol));
    serd_writer_write_statement(
      writer, SERD_ANON_CONT, NULL, &port, &p, &o, NULL, NULL);

    // _:symbol pset:value value
    p = serd_node_from_string(SERD_URI, USTR(LV2_PRESETS__value));
    sratom_write(sratom,
                 unmap,
                 SERD_ANON_CONT,
                 &port,
                 &p,
                 value->atom->type,
                 value->atom->size,
                 value->atom + 1);

    serd_writer_end_anon(writer, &port);
  }

  // Write properties
  const SerdNode body = serd_node_from_string(SERD_BLANK, USTR("body"));
  if (state->props.n > 0) {
    p = serd_node_from_string(SERD_URI, USTR(LV2_STATE__state));
    serd_writer_write_statement(
      writer, SERD_ANON_O_BEGIN, NULL, &subject, &p, &body, NULL, NULL);
  }
  sratom_set_pretty_numbers(sratom, false); // Use precise types
  write_property_array(
    state, &state->props, sratom, SERD_ANON_CONT, &body, unmap, dir);

  if (state->props.n > 0) {
    serd_writer_end_anon(writer, &body);
  }

  sratom_free(sratom);
  return 0;
}

static void
make_links(const LilvState* state, const char* dir)
{
  // Create symlinks to files
  for (ZixTreeIter* i = zix_tree_begin(state->abs2rel);
       i != zix_tree_end(state->abs2rel);
       i = zix_tree_iter_next(i)) {
    const PathMap* const pm = (const PathMap*)zix_tree_get(i);
    if (zix_path_is_absolute(pm->rel)) {
      continue; // Ignore absolute path in state
    }

    char* const save_path = zix_path_join(NULL, dir, pm->rel);
    if (state->copy_dir && path_is_child(pm->abs, state->copy_dir) &&
        !!strcmp(state->copy_dir, dir)) {
      // Link directly to snapshot in the copy directory
      maybe_symlink(pm->abs, save_path);
    } else if (state->link_dir && !path_is_child(pm->abs, dir)) {
      char* const ideal_link_path =
        zix_path_join(NULL, state->link_dir, pm->rel);

      if (!strcmp(dir, state->link_dir)) {
        // Link directory is save directory, make link at exact path
        (void)zix_remove(ideal_link_path);
        maybe_symlink(pm->abs, ideal_link_path);
      } else {
        // Make a link in the link directory to external file
        char* lpath = lilv_find_path(ideal_link_path, link_exists_to, pm->abs);
        if (zix_file_type(lpath) == ZIX_FILE_TYPE_NONE) {
          create_link(pm->abs, lpath);
        }

        // Make a link in the save directory to the external link
        maybe_symlink(lpath, save_path);
        free(lpath);
      }
      free(ideal_link_path);
    }
    free(save_path);
  }
}

int
lilv_state_save(LilvWorld*       world,
                LV2_URID_Map*    map,
                LV2_URID_Unmap*  unmap,
                const LilvState* state,
                const char*      uri,
                const char*      dir,
                const char*      filename)
{
  if (!filename || !dir || zix_create_directories(NULL, dir)) {
    return 1;
  }

  char* const abs_dir = zix_canonical_path(NULL, dir);
  if (!abs_dir) {
    return 2;
  }

  char* const path = zix_path_join(NULL, abs_dir, filename);
  FILE* const fd   = path ? fopen(path, FOPEN_MODE("wb")) : NULL;
  if (!fd) {
    LILV_ERRORF("Failed to open \"%s\" (%s)\n", path, strerror(errno));
    zix_free(NULL, abs_dir);
    zix_free(NULL, path);
    return 4;
  }

  // Create symlinks to files if necessary
  make_links(state, abs_dir);

  // Write state to Turtle file
  SerdNode    file = serd_node_new_file_uri(USTR(path), NULL, NULL, true);
  SerdNode    node = uri ? serd_node_from_string(SERD_URI, USTR(uri)) : file;
  SerdEnv*    env  = NULL;
  SerdWriter* ttl  = ttl_file_writer(fd, &file, &env);
  int         ret =
    write_state_description(map, unmap, state, ttl, (const char*)node.buf, dir);

  // Set saved dir and uri (FIXME: const violation)
  zix_free(NULL, state->dir);
  lilv_node_free(state->uri);
  ((LilvState*)state)->dir = zix_path_join(NULL, abs_dir, "");
  ((LilvState*)state)->uri = lilv_new_uri(world, (const char*)node.buf);

  serd_node_free(&file);
  serd_writer_free(ttl);
  serd_env_free(env);
  fclose(fd);

  // Add entry to manifest
  if (!ret) {
    char* const manifest = zix_path_join(NULL, abs_dir, "manifest.ttl");

    ret = add_state_to_manifest(world, state->plugin_uri, manifest, uri, path);

    zix_free(NULL, manifest);
  }

  zix_free(NULL, abs_dir);
  zix_free(NULL, path);
  return ret;
}

char*
lilv_state_to_string(LilvWorld*       world,
                     LV2_URID_Map*    map,
                     LV2_URID_Unmap*  unmap,
                     const LilvState* state,
                     const char*      uri,
                     const char*      base_uri)
{
  (void)world;

  if (!uri) {
    LILV_ERROR("Attempt to serialise state with no URI\n");
    return NULL;
  }

  SerdChunk   chunk  = {NULL, 0};
  SerdEnv*    env    = NULL;
  SerdNode    base   = serd_node_from_string(SERD_URI, USTR(base_uri));
  SerdWriter* writer = ttl_writer(serd_chunk_sink, &chunk, &base, &env);

  write_state_description(map, unmap, state, writer, uri, NULL);

  serd_writer_free(writer);
  serd_env_free(env);
  char* str    = (char*)serd_chunk_sink_finish(&chunk);
  char* result = lilv_strdup(str);
  serd_free(str);
  return result;
}

static void
try_remove(const char* const state_dir, const char* const path)
{
  // Resolve relative path in state directory if necessary
  // Note that this must be purely lexical, not canonicalized!
  char* resolved_path = NULL;
  if (zix_path_is_relative(path)) {
    resolved_path = zix_path_join(NULL, state_dir, path);
  }

  // Only try to remove the file if it's a child of the state directory
  const char* const abs_path = resolved_path ? resolved_path : path;
  if (path_is_child(abs_path, state_dir)) {
    if (zix_symlink_type(abs_path) != ZIX_FILE_TYPE_NONE &&
        zix_remove(abs_path)) {
      LILV_ERRORF("Failed to remove \"%s\" (%s)\n", abs_path, strerror(errno));
    }
  }

  zix_free(NULL, resolved_path);
}

int
lilv_state_delete(LilvWorld* world, const LilvState* state)
{
  if (!state->dir) {
    LILV_ERROR("Attempt to delete unsaved state\n");
    return -1;
  }

  LilvNode*      bundle        = lilv_new_file_uri(world, NULL, state->dir);
  uint8_t* const manifest_uri  = lilv_manifest_uri(bundle->node);
  const SerdNode manifest_node = serd_node_from_string(SERD_URI, manifest_uri);
  char* const    manifest_path = (char*)serd_file_uri_parse(manifest_uri, NULL);
  const bool     has_manifest =
    manifest_path && zix_file_type(manifest_path) == ZIX_FILE_TYPE_REGULAR;

  SordModel* model    = sord_new(world->world, SORD_SPO, false);
  NodeHash*  see_also = NULL;

  if (state->uri && has_manifest) {
    // Read manifest into temporary local model
    NodeSkimmer* const skimmer = node_skimmer_new(world->world,
                                                  &manifest_node,
                                                  model,
                                                  SORD_SUBJECT,
                                                  state->uri->node,
                                                  world->uris.rdfs_seeAlso,
                                                  true);

    serd_reader_read_file(skimmer->base.reader, USTR(manifest_path));
    see_also = node_skimmer_free(skimmer);
  }

  if (state->uri) {
    // Remove state rdfs:seeAlso files
    NODE_HASH_FOREACH (s, see_also) {
      const SordNode* node = lilv_node_hash_get(see_also, s);
      const uint8_t*  uri  = sord_node_get_string(node);
      char* const     path = (char*)serd_file_uri_parse(uri, NULL);
      try_remove(state->dir, path);
      serd_free(path);
    }

    // Remove any existing manifest entries for this state
    const char* state_uri_str = lilv_node_as_string(state->uri);
    remove_manifest_entry(world->world, world->model, state_uri_str);
  }

  lilv_node_hash_free(see_also, world->world);

  // Drop bundle from model
  lilv_world_unload_bundle(world, bundle);

  if (sord_num_quads(model) == 0) {
    // Manifest is empty, attempt to remove bundle entirely
    if (has_manifest) {
      try_remove(state->dir, manifest_path);
    }

    // Remove all known files from state bundle
    if (state->abs2rel) {
      // State created from instance, get paths from map
      for (ZixTreeIter* i = zix_tree_begin(state->abs2rel);
           i != zix_tree_end(state->abs2rel);
           i = zix_tree_iter_next(i)) {
        const PathMap* pm = (const PathMap*)zix_tree_get(i);
        try_remove(state->dir, pm->rel);
      }
    } else {
      // State loaded from model, get paths from loaded properties
      for (uint32_t i = 0; i < state->props.n; ++i) {
        const Property* const p = &state->props.props[i];
        if (p->type == state->atom_Path) {
          try_remove(state->dir, (const char*)p->value);
        }
      }
    }

    if (zix_remove(state->dir)) {
      LILV_ERRORF("Failed to remove directory \"%s\" (%s)\n",
                  state->dir,
                  strerror(errno));
    }
  } else {
    // Still something in the manifest, update and reload bundle
    SerdEnv* env = serd_env_new(&manifest_node);
    write_manifest(world, env, model, &manifest_node);
    lilv_world_load_bundle(world, bundle);
    serd_env_free(env);
  }

  sord_free(model);
  zix_free(NULL, manifest_path);
  zix_free(NULL, manifest_uri);
  lilv_node_free(bundle);

  return 0;
}

static void
free_property_array(const LilvState* state, PropertyArray* array)
{
  for (uint32_t i = 0; i < array->n; ++i) {
    Property* prop = &array->props[i];
    if ((prop->flags & LV2_STATE_IS_POD) || prop->type == state->atom_Path) {
      free(prop->value);
    }
  }
  free(array->props);
}

void
lilv_state_free(LilvState* state)
{
  if (state) {
    free_property_array(state, &state->props);
    free_property_array(state, &state->metadata);
    for (uint32_t i = 0; i < state->n_values; ++i) {
      free(state->values[i].atom);
      free(state->values[i].symbol);
    }
    lilv_node_free(state->plugin_uri);
    lilv_node_free(state->uri);
    zix_tree_free(state->abs2rel);
    zix_tree_free(state->rel2abs);
    free(state->values);
    free(state->label);
    free(state->dir);
    free(state->scratch_dir);
    free(state->copy_dir);
    free(state->link_dir);
    free(state);
  }
}

bool
lilv_state_equals(const LilvState* a, const LilvState* b)
{
  if (!lilv_node_equals(a->plugin_uri, b->plugin_uri) ||
      (a->label && !b->label) || (b->label && !a->label) ||
      (a->label && b->label && !!strcmp(a->label, b->label)) ||
      a->props.n != b->props.n || a->n_values != b->n_values) {
    return false;
  }

  for (uint32_t i = 0; i < a->n_values; ++i) {
    const PortValue* const av = &a->values[i];
    const PortValue* const bv = &b->values[i];
    if (av->atom->size != bv->atom->size || av->atom->type != bv->atom->type ||
        !!strcmp(av->symbol, bv->symbol) ||
        !!memcmp(av->atom + 1, bv->atom + 1, av->atom->size)) {
      return false;
    }
  }

  for (uint32_t i = 0; i < a->props.n; ++i) {
    const Property* const ap = &a->props.props[i];
    const Property* const bp = &b->props.props[i];
    if (ap->key != bp->key || ap->type != bp->type || ap->flags != bp->flags) {
      return false;
    }

    if (ap->type == a->atom_Path) {
      if (!zix_file_equals(NULL,
                           resolve_path(a, (const char*)ap->value),
                           resolve_path(b, (const char*)bp->value))) {
        return false;
      }
    } else if (ap->size != bp->size ||
               !!memcmp(ap->value, bp->value, ap->size)) {
      return false;
    }
  }

  return true;
}

unsigned
lilv_state_get_num_properties(const LilvState* state)
{
  return state->props.n;
}

const LilvNode*
lilv_state_get_plugin_uri(const LilvState* state)
{
  return state->plugin_uri;
}

const LilvNode*
lilv_state_get_uri(const LilvState* state)
{
  return state->uri;
}

const char*
lilv_state_get_label(const LilvState* state)
{
  return state->label;
}

void
lilv_state_set_label(LilvState* state, const char* label)
{
  const size_t len = strlen(label);
  state->label     = (char*)realloc(state->label, len + 1);
  memcpy(state->label, label, len + 1);
}

int
lilv_state_set_metadata(LilvState*  state,
                        uint32_t    key,
                        const void* value,
                        size_t      size,
                        uint32_t    type,
                        uint32_t    flags)
{
  append_property(state, &state->metadata, key, value, size, type, flags);
  return LV2_STATE_SUCCESS;
}
