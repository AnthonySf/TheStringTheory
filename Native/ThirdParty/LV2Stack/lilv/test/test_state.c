// Copyright 2007-2020 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "lilv_test_uri_map.h"
#include "lilv_test_utils.h"

#include <lilv/lilv.h>
#include <lv2/core/lv2.h>
#include <lv2/state/state.h>
#include <lv2/urid/urid.h>
#include <serd/serd.h>
#include <zix/allocator.h>
#include <zix/filesystem.h>
#include <zix/path.h>

#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define TEST_PLUGIN_URI "http://example.org/lilv-test-plugin"

typedef struct {
  LilvTestEnv*        env;
  LilvTestUriMap      uri_map;
  LV2_URID_Map        map;
  LV2_Feature         map_feature;
  LV2_URID_Unmap      unmap;
  LV2_Feature         unmap_feature;
  LV2_State_Free_Path free_path;
  LV2_Feature         free_path_feature;
  const LV2_Feature*  features[4];
  LV2_URID            atom_Float;
  float               in;
  float               out;
  float               control;
} TestContext;

typedef struct {
  char* top;     ///< Temporary directory that contains everything
  char* shared;  ///< Common parent for shared directoryes (top/shared)
  char* scratch; ///< Scratch file directory (top/shared/scratch)
  char* copy;    ///< Copy directory (top/shared/scratch)
  char* link;    ///< Link directory (top/shared/scratch)
} TestDirectories;

static void
lilv_free_path(LV2_State_Free_Path_Handle handle, char* path)
{
  (void)handle;

  lilv_free(path);
}

static TestContext*
test_context_new(void)
{
  TestContext* ctx = (TestContext*)calloc(1, sizeof(TestContext));
  assert(ctx);

  lilv_test_uri_map_init(&ctx->uri_map);

  ctx->env                    = lilv_test_env_new();
  ctx->map.handle             = &ctx->uri_map;
  ctx->map.map                = map_uri;
  ctx->map_feature.URI        = LV2_URID_MAP_URI;
  ctx->map_feature.data       = &ctx->map;
  ctx->unmap.handle           = &ctx->uri_map;
  ctx->unmap.unmap            = unmap_uri;
  ctx->unmap_feature.URI      = LV2_URID_UNMAP_URI;
  ctx->unmap_feature.data     = &ctx->unmap;
  ctx->free_path.free_path    = lilv_free_path;
  ctx->free_path_feature.URI  = LV2_STATE__freePath;
  ctx->free_path_feature.data = &ctx->free_path;
  ctx->features[0]            = &ctx->map_feature;
  ctx->features[1]            = &ctx->unmap_feature;
  ctx->features[2]            = &ctx->free_path_feature;
  ctx->features[3]            = NULL;

  ctx->atom_Float =
    map_uri(&ctx->uri_map, "http://lv2plug.in/ns/ext/atom#Float");

  ctx->in      = 1.0;
  ctx->out     = 42.0;
  ctx->control = 1234.0;

  return ctx;
}

static void
test_context_free(TestContext* ctx)
{
  lilv_test_uri_map_clear(&ctx->uri_map);
  lilv_test_env_free(ctx->env);
  free(ctx);
}

static TestDirectories
create_test_directories(void)
{
  TestDirectories dirs;

  char* const top = lilv_create_temporary_directory("lilv_XXXXXX");
  assert(top);

  /* On MacOS, temporary directories from mkdtemp involve symlinks, so
     resolve it here so that path comparisons in tests work. */

  dirs.top     = zix_canonical_path(NULL, top);
  dirs.shared  = zix_path_join(NULL, dirs.top, "shared");
  dirs.scratch = zix_path_join(NULL, dirs.shared, "scratch");
  dirs.copy    = zix_path_join(NULL, dirs.shared, "copy");
  dirs.link    = zix_path_join(NULL, dirs.shared, "link");

  assert(dirs.top);
  assert(dirs.shared);
  assert(dirs.scratch);
  assert(dirs.copy);
  assert(dirs.link);

  assert(!zix_create_directory(dirs.shared));
  assert(!zix_create_directory(dirs.scratch));
  assert(!zix_create_directory(dirs.copy));
  assert(!zix_create_directory(dirs.link));

  free(top);

  return dirs;
}

static TestDirectories
no_test_directories(void)
{
  TestDirectories dirs = {NULL, NULL, NULL, NULL, NULL};

  return dirs;
}

static void
remove_file(const char* path, const char* name, void* data)
{
  (void)data;

  char* const full_path = zix_path_join(NULL, path, name);
  assert(!zix_remove(full_path));
  free(full_path);
}

static void
cleanup_test_directories(const TestDirectories dirs)
{
  zix_dir_for_each(dirs.scratch, NULL, remove_file);
  zix_dir_for_each(dirs.copy, NULL, remove_file);
  zix_dir_for_each(dirs.link, NULL, remove_file);

  assert(!zix_remove(dirs.link));
  assert(!zix_remove(dirs.copy));
  assert(!zix_remove(dirs.scratch));
  assert(!zix_remove(dirs.shared));
  assert(!zix_remove(dirs.top));

  zix_free(NULL, dirs.link);
  zix_free(NULL, dirs.copy);
  zix_free(NULL, dirs.scratch);
  zix_free(NULL, dirs.shared);
  free(dirs.top);
}

static const void*
get_port_value(const char* port_symbol,
               void*       user_data,
               uint32_t*   size,
               uint32_t*   type)
{
  TestContext* ctx = (TestContext*)user_data;

  if (!strcmp(port_symbol, "input")) {
    *size = sizeof(float);
    *type = ctx->atom_Float;
    return &ctx->in;
  }

  if (!strcmp(port_symbol, "output")) {
    *size = sizeof(float);
    *type = ctx->atom_Float;
    return &ctx->out;
  }

  if (!strcmp(port_symbol, "control")) {
    *size = sizeof(float);
    *type = ctx->atom_Float;
    return &ctx->control;
  }

  fprintf(
    stderr, "error: get_port_value for nonexistent port `%s'\n", port_symbol);
  *size = *type = 0;
  return NULL;
}

static void
set_port_value(const char* port_symbol,
               void*       user_data,
               const void* value,
               uint32_t    size,
               uint32_t    type)
{
  (void)size;
  (void)type;

  TestContext* ctx = (TestContext*)user_data;

  if (!strcmp(port_symbol, "input")) {
    ctx->in = *(const float*)value;
  } else if (!strcmp(port_symbol, "output")) {
    ctx->out = *(const float*)value;
  } else if (!strcmp(port_symbol, "control")) {
    ctx->control = *(const float*)value;
  } else {
    fprintf(
      stderr, "error: set_port_value for nonexistent port `%s'\n", port_symbol);
  }
}

static char*
make_scratch_path(LV2_State_Make_Path_Handle handle, const char* path)
{
  const TestDirectories* dirs = (TestDirectories*)handle;

  return zix_path_join(NULL, dirs->scratch, path);
}

static const LilvPlugin*
load_test_plugin(const TestContext* const ctx)
{
  LilvWorld* world      = ctx->env->world;
  LilvNode*  bundle_uri = lilv_new_file_uri(world, NULL, LILV_TEST_BUNDLE);
  LilvNode*  plugin_uri = lilv_new_uri(world, TEST_PLUGIN_URI);

  lilv_world_load_bundle(world, bundle_uri);

  const LilvPlugins* plugins = lilv_world_get_all_plugins(world);
  const LilvPlugin*  plugin  = lilv_plugins_get_by_uri(plugins, plugin_uri);

  lilv_node_free(plugin_uri);
  lilv_node_free(bundle_uri);

  assert(plugin);
  return plugin;
}

static LilvState*
state_from_instance(const LilvPlugin* const      plugin,
                    LilvInstance* const          instance,
                    TestContext* const           ctx,
                    const TestDirectories* const dirs,
                    const char* const            bundle_path)
{
  return lilv_state_new_from_instance(plugin,
                                      instance,
                                      &ctx->map,
                                      dirs->scratch,
                                      dirs->copy,
                                      dirs->link,
                                      bundle_path,
                                      get_port_value,
                                      ctx,
                                      0,
                                      NULL);
}

static void
test_instance_state(void)
{
  TestContext* const      ctx    = test_context_new();
  const TestDirectories   dirs   = no_test_directories();
  const LilvPlugin* const plugin = load_test_plugin(ctx);
  LilvInstance* const     instance =
    lilv_plugin_instantiate(plugin, 48000.0, ctx->features);

  assert(instance);

  // Get instance state
  LilvState* const state =
    state_from_instance(plugin, instance, ctx, &dirs, NULL);

  // Check that state contains properties saved by the plugin
  assert(lilv_state_get_num_properties(state) == 8);

  // Check that state has no URI
  assert(!lilv_state_get_uri(state));

  // Check that state can't be saved without a URI
  assert(!lilv_state_to_string(
    ctx->env->world, &ctx->map, &ctx->unmap, state, NULL, NULL));

  // Check that we can't delete unsaved state
  assert(lilv_state_delete(ctx->env->world, state));

  lilv_state_free(state);
  lilv_instance_free(instance);
  test_context_free(ctx);
}

static void
test_default_state(void)
{
  TestContext* const      ctx        = test_context_new();
  const LilvPlugin* const plugin     = load_test_plugin(ctx);
  const LilvNode* const   plugin_uri = lilv_plugin_get_uri(plugin);

  LilvState* const state =
    lilv_state_new_from_world(ctx->env->world, &ctx->map, plugin_uri);

  assert(lilv_state_get_num_properties(state) == 0);
  assert(lilv_node_equals(lilv_state_get_uri(state), plugin_uri));

  char* const bundle_path = lilv_file_uri_parse(
    lilv_node_as_string(lilv_plugin_get_bundle_uri(plugin)), NULL);

  LilvState* const bundle_state =
    lilv_state_new_from_file(ctx->env->world, &ctx->map, NULL, bundle_path);
  assert(bundle_state);
  assert(lilv_state_equals(bundle_state, state));

  lilv_state_free(bundle_state);
  lilv_free(bundle_path);
  lilv_state_free(state);
  test_context_free(ctx);
}

static void
test_equal(void)
{
  TestContext* const      ctx    = test_context_new();
  const TestDirectories   dirs   = no_test_directories();
  const LilvPlugin* const plugin = load_test_plugin(ctx);
  LilvInstance* const     instance =
    lilv_plugin_instantiate(plugin, 48000.0, ctx->features);

  assert(instance);

  // Get instance state
  LilvState* const state_1 =
    state_from_instance(plugin, instance, ctx, &dirs, NULL);

  // Get another instance state
  LilvState* const state_2 =
    state_from_instance(plugin, instance, ctx, &dirs, NULL);

  // Ensure they are equal
  assert(lilv_state_equals(state_1, state_2));

  // Set a label on the second state
  assert(lilv_state_get_label(state_2) == NULL);
  lilv_state_set_label(state_2, "Test State Old Label");

  // Ensure they are no longer equal
  assert(!lilv_state_equals(state_1, state_2));

  lilv_state_free(state_2);
  lilv_state_free(state_1);
  lilv_instance_free(instance);
  test_context_free(ctx);
}

static void
test_changed_plugin_data(void)
{
  TestContext* const      ctx    = test_context_new();
  const TestDirectories   dirs   = no_test_directories();
  const LilvPlugin* const plugin = load_test_plugin(ctx);
  LilvInstance* const     instance =
    lilv_plugin_instantiate(plugin, 48000.0, ctx->features);

  assert(instance);

  // Get initial state
  LilvState* const initial_state =
    state_from_instance(plugin, instance, ctx, &dirs, NULL);

  // Run plugin to change internal state
  lilv_instance_activate(instance);
  lilv_instance_connect_port(instance, 0, &ctx->in);
  lilv_instance_connect_port(instance, 1, &ctx->out);
  lilv_instance_run(instance, 1);
  assert(ctx->in == 1.0);
  assert(ctx->out == 1.0);

  // Get a new instance state (which should now differ)
  LilvState* const changed_state =
    state_from_instance(plugin, instance, ctx, &dirs, NULL);

  // Ensure state has changed
  assert(!lilv_state_equals(initial_state, changed_state));

  lilv_state_free(changed_state);
  lilv_state_free(initial_state);
  lilv_instance_free(instance);
  test_context_free(ctx);
}

static void
test_changed_metadata(void)
{
  TestContext* const      ctx    = test_context_new();
  const TestDirectories   dirs   = no_test_directories();
  LV2_URID_Map* const     map    = &ctx->map;
  const LilvPlugin* const plugin = load_test_plugin(ctx);
  LilvInstance* const     instance =
    lilv_plugin_instantiate(plugin, 48000.0, ctx->features);

  assert(instance);

  // Get initial state
  LilvState* const initial_state =
    state_from_instance(plugin, instance, ctx, &dirs, NULL);

  // Save initial state to a string
  char* const initial_string =
    lilv_state_to_string(ctx->env->world,
                         &ctx->map,
                         &ctx->unmap,
                         initial_state,
                         "http://example.org/initial",
                         NULL);

  // Get another state
  LilvState* const changed_state =
    state_from_instance(plugin, instance, ctx, &dirs, NULL);

  // Set a metadata property
  const LV2_URID key   = map->map(map->handle, "http://example.org/extra");
  const int32_t  value = 1;
  const LV2_URID type =
    map->map(map->handle, "http://lv2plug.in/ns/ext/atom#Int");
  lilv_state_set_metadata(changed_state,
                          key,
                          &value,
                          sizeof(value),
                          type,
                          LV2_STATE_IS_PORTABLE | LV2_STATE_IS_POD);

  // Save changed state to a string
  char* const changed_string =
    lilv_state_to_string(ctx->env->world,
                         &ctx->map,
                         &ctx->unmap,
                         changed_state,
                         "http://example.org/changed",
                         NULL);

  // Ensure that strings differ (metadata does not affect state equality)
  assert(strcmp(initial_string, changed_string));

  free(changed_string);
  lilv_state_free(changed_state);
  free(initial_string);
  lilv_state_free(initial_state);
  lilv_instance_free(instance);
  test_context_free(ctx);
}

static void
test_to_string(void)
{
  TestContext* const      ctx    = test_context_new();
  const TestDirectories   dirs   = no_test_directories();
  const LilvPlugin* const plugin = load_test_plugin(ctx);
  LilvInstance* const     instance =
    lilv_plugin_instantiate(plugin, 48000.0, ctx->features);

  assert(instance);

  // Get initial state
  LilvState* const initial_state =
    state_from_instance(plugin, instance, ctx, &dirs, NULL);

  // Run plugin to change internal state
  lilv_instance_activate(instance);
  lilv_instance_connect_port(instance, 0, &ctx->in);
  lilv_instance_connect_port(instance, 1, &ctx->out);
  lilv_instance_run(instance, 1);
  assert(ctx->in == 1.0);
  assert(ctx->out == 1.0);

  // Restore instance state to original state
  lilv_state_restore(initial_state, instance, set_port_value, ctx, 0, NULL);

  // Take a new snapshot of the state
  LilvState* const restored =
    state_from_instance(plugin, instance, ctx, &dirs, NULL);

  // Check that new state matches the initial state
  assert(lilv_state_equals(initial_state, restored));

  lilv_state_free(restored);
  lilv_state_free(initial_state);
  lilv_instance_free(instance);
  test_context_free(ctx);
}

static void
test_string_round_trip(void)
{
  TestContext* const      ctx    = test_context_new();
  const TestDirectories   dirs   = no_test_directories();
  const LilvPlugin* const plugin = load_test_plugin(ctx);
  LilvInstance* const     instance =
    lilv_plugin_instantiate(plugin, 48000.0, ctx->features);

  assert(instance);

  // Get initial state
  LilvState* const initial_state =
    state_from_instance(plugin, instance, ctx, &dirs, NULL);

  // Save state to a string
  char* const string = lilv_state_to_string(ctx->env->world,
                                            &ctx->map,
                                            &ctx->unmap,
                                            initial_state,
                                            "http://example.org/string",
                                            NULL);

  // Restore from string
  LilvState* const restored =
    lilv_state_new_from_string(ctx->env->world, &ctx->map, string);

  // Ensure they are equal
  assert(restored);
  assert(lilv_state_equals(initial_state, restored));

  // Check that the restored state refers to the correct plugin
  const LilvNode* state_plugin_uri = lilv_state_get_plugin_uri(restored);
  assert(!strcmp(lilv_node_as_string(state_plugin_uri), TEST_PLUGIN_URI));

  lilv_state_free(restored);
  free(string);
  lilv_state_free(initial_state);
  lilv_instance_free(instance);
  test_context_free(ctx);
}

static SerdStatus
count_sink(void* const              handle,
           const SerdStatementFlags flags,
           const SerdNode* const    graph,
           const SerdNode* const    subject,
           const SerdNode* const    predicate,
           const SerdNode* const    object,
           const SerdNode* const    object_datatype,
           const SerdNode* const    object_lang)
{
  (void)flags;
  (void)graph;
  (void)subject;
  (void)predicate;
  (void)object;
  (void)object_datatype;
  (void)object_lang;

  size_t* const n_statements = (size_t*)handle;

  ++(*n_statements);

  return SERD_SUCCESS;
}

static size_t
count_statements(const char* path)
{
  size_t n_statements = 0;

  SerdReader* reader = serd_reader_new(
    SERD_TURTLE, &n_statements, NULL, NULL, NULL, count_sink, NULL);

  SerdNode uri = serd_node_new_file_uri((const uint8_t*)path, NULL, NULL, true);

  assert(uri.buf);
  assert(!serd_reader_read_file(reader, (const uint8_t*)uri.buf));

  serd_node_free(&uri);
  serd_reader_free(reader);

  return n_statements;
}

static void
test_to_files(void)
{
  TestContext* const      ctx    = test_context_new();
  TestDirectories         dirs   = create_test_directories();
  const LilvPlugin* const plugin = load_test_plugin(ctx);

  LV2_State_Make_Path make_path         = {&dirs, make_scratch_path};
  LV2_Feature         make_path_feature = {LV2_STATE__makePath, &make_path};

  const LV2_Feature* const instance_features[] = {
    &ctx->map_feature, &ctx->free_path_feature, &make_path_feature, NULL};

  LilvInstance* const instance =
    lilv_plugin_instantiate(plugin, 48000.0, instance_features);

  assert(instance);

  // Run plugin to generate some recording file data
  lilv_instance_activate(instance);
  lilv_instance_connect_port(instance, 0, &ctx->in);
  lilv_instance_connect_port(instance, 1, &ctx->out);
  lilv_instance_run(instance, 1);
  lilv_instance_run(instance, 2);
  assert(ctx->in == 1.0);
  assert(ctx->out == 1.0);

  // Check that the test plugin has made its recording scratch file
  char* const recfile_path = zix_path_join(NULL, dirs.scratch, "recfile");
  assert(zix_file_type(recfile_path) == ZIX_FILE_TYPE_REGULAR);

  // Get state
  char* const      bundle_1_path = zix_path_join(NULL, dirs.top, "state1.lv2");
  LilvState* const state_1 =
    state_from_instance(plugin, instance, ctx, &dirs, bundle_1_path);

  // Check that state contains properties saved by the plugin (with files)
  assert(lilv_state_get_num_properties(state_1) == 10);

  // Check that a snapshop of the recfile was created
  char* const recfile_copy_1 = zix_path_join(NULL, dirs.copy, "recfile");
  assert(zix_file_type(recfile_copy_1) == ZIX_FILE_TYPE_REGULAR);

  // Save state to a bundle
  assert(!lilv_state_save(ctx->env->world,
                          &ctx->map,
                          &ctx->unmap,
                          state_1,
                          "http://example.org/state1",
                          bundle_1_path,
                          "state.ttl"));

  // Check that a manifest exists
  char* const manifest_path =
    zix_path_join(NULL, bundle_1_path, "manifest.ttl");
  assert(zix_file_type(manifest_path) == ZIX_FILE_TYPE_REGULAR);

  // Check that the expected statements are in the manifest file
  assert(count_statements(manifest_path) == 3);

  // Check that a link to the recfile exists in the saved bundle
  char* const recfile_link_1 = zix_path_join(NULL, bundle_1_path, "recfile");
  assert(zix_file_type(recfile_link_1) == ZIX_FILE_TYPE_REGULAR);
#ifndef _WIN32
  assert(zix_symlink_type(recfile_link_1) == ZIX_FILE_TYPE_SYMLINK);
#endif

  // Check that link points to the corresponding copy
  assert(zix_file_equals(NULL, recfile_link_1, recfile_copy_1));

  // Run plugin again to modify recording file data
  lilv_instance_run(instance, 2);

  // Get updated state
  char* const      bundle_2_path = zix_path_join(NULL, dirs.top, "state2.lv2");
  LilvState* const state_2 =
    state_from_instance(plugin, instance, ctx, &dirs, bundle_2_path);

  // Save updated state to a bundle
  assert(!lilv_state_save(ctx->env->world,
                          &ctx->map,
                          &ctx->unmap,
                          state_2,
                          NULL,
                          bundle_2_path,
                          "state.ttl"));

  // Check that a new snapshop of the recfile was created
  char* const recfile_copy_2 = zix_path_join(NULL, dirs.copy, "recfile.2");
  assert(zix_file_type(recfile_copy_2) == ZIX_FILE_TYPE_REGULAR);

  // Check that a link to the recfile exists in the updated bundle
  char* const recfile_link_2 = zix_path_join(NULL, bundle_2_path, "recfile");
  assert(zix_file_type(recfile_link_2) == ZIX_FILE_TYPE_REGULAR);
#ifndef _WIN32
  assert(zix_symlink_type(recfile_link_2) == ZIX_FILE_TYPE_SYMLINK);
#endif

  // Check that link points to the corresponding copy
  assert(zix_file_equals(NULL, recfile_link_2, recfile_copy_2));

  lilv_instance_free(instance);
  zix_dir_for_each(bundle_2_path, NULL, remove_file);
  zix_dir_for_each(bundle_1_path, NULL, remove_file);
  assert(!zix_remove(bundle_2_path));
  assert(!zix_remove(bundle_1_path));
  cleanup_test_directories(dirs);

  zix_free(NULL, recfile_link_2);
  zix_free(NULL, recfile_copy_2);
  lilv_state_free(state_2);
  zix_free(NULL, bundle_2_path);
  zix_free(NULL, recfile_link_1);
  zix_free(NULL, manifest_path);
  zix_free(NULL, recfile_copy_1);
  lilv_state_free(state_1);
  zix_free(NULL, bundle_1_path);
  zix_free(NULL, recfile_path);
  test_context_free(ctx);
}

static void
test_multi_save(void)
{
  TestContext* const      ctx    = test_context_new();
  TestDirectories         dirs   = create_test_directories();
  const LilvPlugin* const plugin = load_test_plugin(ctx);

  LV2_State_Make_Path make_path         = {&dirs, make_scratch_path};
  LV2_Feature         make_path_feature = {LV2_STATE__makePath, &make_path};

  const LV2_Feature* const instance_features[] = {
    &ctx->map_feature, &ctx->free_path_feature, &make_path_feature, NULL};

  LilvInstance* const instance =
    lilv_plugin_instantiate(plugin, 48000.0, instance_features);

  assert(instance);

  // Get state
  char* const      bundle_1_path = zix_path_join(NULL, dirs.top, "state1.lv2");
  LilvState* const state_1 =
    state_from_instance(plugin, instance, ctx, &dirs, bundle_1_path);

  // Save state to a bundle
  assert(!lilv_state_save(ctx->env->world,
                          &ctx->map,
                          &ctx->unmap,
                          state_1,
                          "http://example.org/state1",
                          bundle_1_path,
                          "state.ttl"));

  // Check that a manifest exists
  char* const manifest_path =
    zix_path_join(NULL, bundle_1_path, "manifest.ttl");
  assert(zix_file_type(manifest_path) == ZIX_FILE_TYPE_REGULAR);

  // Check that the state file exists
  char* const state_path = zix_path_join(NULL, bundle_1_path, "state.ttl");
  assert(zix_file_type(state_path) == ZIX_FILE_TYPE_REGULAR);

  // Check that the expected statements are in the files
  assert(count_statements(manifest_path) == 3);
  assert(count_statements(state_path) == 21);

  // Save state again to the same bundle
  assert(!lilv_state_save(ctx->env->world,
                          &ctx->map,
                          &ctx->unmap,
                          state_1,
                          "http://example.org/state1",
                          bundle_1_path,
                          "state.ttl"));

  // Check that everything is the same
  assert(zix_file_type(manifest_path) == ZIX_FILE_TYPE_REGULAR);
  assert(zix_file_type(state_path) == ZIX_FILE_TYPE_REGULAR);
  assert(count_statements(manifest_path) == 3);
  assert(count_statements(state_path) == 21);

  lilv_instance_free(instance);
  zix_dir_for_each(bundle_1_path, NULL, remove_file);
  assert(!zix_remove(bundle_1_path));
  cleanup_test_directories(dirs);

  free(state_path);
  free(manifest_path);
  lilv_state_free(state_1);
  free(bundle_1_path);
  test_context_free(ctx);
}

static void
test_files_round_trip(void)
{
  TestContext* const      ctx    = test_context_new();
  TestDirectories         dirs   = create_test_directories();
  const LilvPlugin* const plugin = load_test_plugin(ctx);

  LV2_State_Make_Path make_path         = {&dirs, make_scratch_path};
  LV2_Feature         make_path_feature = {LV2_STATE__makePath, &make_path};

  const LV2_Feature* const instance_features[] = {
    &ctx->map_feature, &ctx->free_path_feature, &make_path_feature, NULL};

  LilvInstance* const instance =
    lilv_plugin_instantiate(plugin, 48000.0, instance_features);

  assert(instance);

  // Run plugin to generate some recording file data
  lilv_instance_activate(instance);
  lilv_instance_connect_port(instance, 0, &ctx->in);
  lilv_instance_connect_port(instance, 1, &ctx->out);
  lilv_instance_run(instance, 1);
  lilv_instance_run(instance, 2);
  assert(ctx->in == 1.0);
  assert(ctx->out == 1.0);

  // Save first state to a bundle
  char* const bundle_1_1_path = zix_path_join(NULL, dirs.top, "state1_1.lv2");
  LilvState* const state_1_1 =
    state_from_instance(plugin, instance, ctx, &dirs, bundle_1_1_path);

  assert(!lilv_state_save(ctx->env->world,
                          &ctx->map,
                          &ctx->unmap,
                          state_1_1,
                          NULL,
                          bundle_1_1_path,
                          "state.ttl"));

  // Save first state to another bundle
  char* const bundle_1_2_path = zix_path_join(NULL, dirs.top, "state1_2.lv2");
  LilvState* const state_1_2 =
    state_from_instance(plugin, instance, ctx, &dirs, bundle_1_2_path);

  assert(!lilv_state_save(ctx->env->world,
                          &ctx->map,
                          &ctx->unmap,
                          state_1_2,
                          NULL,
                          bundle_1_2_path,
                          "state.ttl"));

  // Load both first state bundles and check that the results are equal
  char* const state_1_1_path =
    zix_path_join(NULL, bundle_1_1_path, "state.ttl");
  char* const state_1_2_path =
    zix_path_join(NULL, bundle_1_2_path, "state.ttl");

  LilvState* state_1_1_loaded =
    lilv_state_new_from_file(ctx->env->world, &ctx->map, NULL, state_1_1_path);

  LilvState* state_1_2_loaded =
    lilv_state_new_from_file(ctx->env->world, &ctx->map, NULL, state_1_2_path);

  assert(state_1_1_loaded);
  assert(state_1_2_loaded);
  assert(lilv_state_equals(state_1_1_loaded, state_1_2_loaded));

  // Run plugin again to modify recording file data
  lilv_instance_run(instance, 2);

  // Save updated state to a bundle
  char* const      bundle_2_path = zix_path_join(NULL, dirs.top, "state2.lv2");
  LilvState* const state_2 =
    state_from_instance(plugin, instance, ctx, &dirs, bundle_2_path);

  assert(!lilv_state_save(ctx->env->world,
                          &ctx->map,
                          &ctx->unmap,
                          state_2,
                          NULL,
                          bundle_2_path,
                          "state.ttl"));

  // Load updated state bundle and check that it differs from the others
  LilvState* state_2_loaded =
    lilv_state_new_from_file(ctx->env->world, &ctx->map, NULL, bundle_2_path);

  assert(state_2_loaded);
  assert(!lilv_state_equals(state_1_1_loaded, state_2_loaded));

  lilv_instance_free(instance);
  zix_dir_for_each(bundle_1_1_path, NULL, remove_file);
  zix_dir_for_each(bundle_1_2_path, NULL, remove_file);
  zix_dir_for_each(bundle_2_path, NULL, remove_file);
  assert(!zix_remove(bundle_1_1_path));
  assert(!zix_remove(bundle_1_2_path));
  assert(!zix_remove(bundle_2_path));
  cleanup_test_directories(dirs);

  lilv_state_free(state_2_loaded);
  lilv_state_free(state_2);
  free(bundle_2_path);
  lilv_state_free(state_1_2_loaded);
  lilv_state_free(state_1_1_loaded);
  free(state_1_2_path);
  free(state_1_1_path);
  lilv_state_free(state_1_2);
  free(bundle_1_2_path);
  lilv_state_free(state_1_1);
  free(bundle_1_1_path);
  test_context_free(ctx);
}

static void
test_world_round_trip(void)
{
  TestContext* const       ctx       = test_context_new();
  LilvWorld* const         world     = ctx->env->world;
  TestDirectories          dirs      = create_test_directories();
  const LilvPlugin* const  plugin    = load_test_plugin(ctx);
  static const char* const state_uri = "http://example.org/worldState";

  LV2_State_Make_Path make_path         = {&dirs, make_scratch_path};
  LV2_Feature         make_path_feature = {LV2_STATE__makePath, &make_path};

  const LV2_Feature* const instance_features[] = {
    &ctx->map_feature, &ctx->free_path_feature, &make_path_feature, NULL};

  LilvInstance* const instance =
    lilv_plugin_instantiate(plugin, 48000.0, instance_features);

  assert(instance);

  // Run plugin to generate some recording file data
  lilv_instance_activate(instance);
  lilv_instance_connect_port(instance, 0, &ctx->in);
  lilv_instance_connect_port(instance, 1, &ctx->out);
  lilv_instance_run(instance, 1);
  lilv_instance_run(instance, 2);
  assert(ctx->in == 1.0);
  assert(ctx->out == 1.0);

  // Save state to a bundle
  char* const      bundle_path = zix_path_join(NULL, dirs.top, "state.lv2/");
  LilvState* const start_state =
    state_from_instance(plugin, instance, ctx, &dirs, bundle_path);

  assert(!lilv_state_save(ctx->env->world,
                          &ctx->map,
                          &ctx->unmap,
                          start_state,
                          state_uri,
                          bundle_path,
                          "state.ttl"));

  // Load state bundle into world
  SerdNode bundle_uri =
    serd_node_new_file_uri((const uint8_t*)bundle_path, 0, 0, true);
  LilvNode* const bundle_node =
    lilv_new_uri(world, (const char*)bundle_uri.buf);
  LilvNode* const state_node = lilv_new_uri(world, state_uri);
  lilv_world_load_bundle(world, bundle_node);
  lilv_world_load_resource(world, state_node);

  // Ensure the state loaded from the world matches
  LilvState* const restored =
    lilv_state_new_from_world(world, &ctx->map, state_node);
  assert(restored);
  assert(lilv_state_equals(start_state, restored));

  // Unload state from world
  lilv_world_unload_resource(world, state_node);
  lilv_world_unload_bundle(world, bundle_node);

  // Ensure that it is no longer present
  assert(!lilv_state_new_from_world(world, &ctx->map, state_node));

  lilv_instance_free(instance);
  lilv_state_delete(world, start_state);
  cleanup_test_directories(dirs);

  lilv_state_free(restored);
  lilv_node_free(state_node);
  lilv_node_free(bundle_node);
  serd_node_free(&bundle_uri);
  lilv_state_free(start_state);
  free(bundle_path);
  test_context_free(ctx);
}

static void
test_label_round_trip(void)
{
  TestContext* const      ctx    = test_context_new();
  const TestDirectories   dirs   = create_test_directories();
  const LilvPlugin* const plugin = load_test_plugin(ctx);
  LilvInstance* const     instance =
    lilv_plugin_instantiate(plugin, 48000.0, ctx->features);

  assert(instance);

  // Get initial state
  LilvState* const state =
    state_from_instance(plugin, instance, ctx, &dirs, NULL);

  // Set a label
  lilv_state_set_label(state, "Monopoly on violence");

  // Save to a bundle
  char* const bundle_path = zix_path_join(NULL, dirs.top, "state.lv2/");
  assert(!lilv_state_save(ctx->env->world,
                          &ctx->map,
                          &ctx->unmap,
                          state,
                          NULL,
                          bundle_path,
                          "state.ttl"));

  // Load bundle and check the label and that the states are equal
  char* const state_path = zix_path_join(NULL, bundle_path, "state.ttl");

  LilvState* const loaded =
    lilv_state_new_from_file(ctx->env->world, &ctx->map, NULL, state_path);

  assert(loaded);
  assert(lilv_state_equals(state, loaded));
  assert(!strcmp(lilv_state_get_label(loaded), "Monopoly on violence"));

  lilv_instance_free(instance);
  lilv_state_delete(ctx->env->world, state);
  cleanup_test_directories(dirs);

  lilv_state_free(loaded);
  free(state_path);
  free(bundle_path);
  lilv_state_free(state);
  test_context_free(ctx);
}

static void
test_bad_subject(void)
{
  TestContext* const ctx    = test_context_new();
  LilvNode* const    string = lilv_new_string(ctx->env->world, "Not a URI");

  const LilvState* const file_state = lilv_state_new_from_file(
    ctx->env->world, &ctx->map, string, "/I/do/not/matter");

  assert(!file_state);

  const LilvState* const world_state =
    lilv_state_new_from_world(ctx->env->world, &ctx->map, string);

  assert(!world_state);

  lilv_node_free(string);
  test_context_free(ctx);
}

static void
test_missing_path(void)
{
  TestContext* const     ctx   = test_context_new();
  const LilvState* const state = lilv_state_new_from_file(
    ctx->env->world, &ctx->map, NULL, "/does/not/exist");

  assert(!state);

  test_context_free(ctx);
}

static void
count_file(const char* path, const char* name, void* data)
{
  (void)path;
  (void)name;

  *(unsigned*)data += 1;
}

static void
test_delete(void)
{
  TestContext* const      ctx    = test_context_new();
  TestDirectories         dirs   = create_test_directories();
  const LilvPlugin* const plugin = load_test_plugin(ctx);

  LV2_State_Make_Path make_path         = {&dirs, make_scratch_path};
  LV2_Feature         make_path_feature = {LV2_STATE__makePath, &make_path};

  const LV2_Feature* const instance_features[] = {
    &ctx->map_feature, &ctx->free_path_feature, &make_path_feature, NULL};

  LilvInstance* const instance =
    lilv_plugin_instantiate(plugin, 48000.0, instance_features);

  assert(instance);

  // Run plugin to generate some recording file data
  lilv_instance_activate(instance);
  lilv_instance_connect_port(instance, 0, &ctx->in);
  lilv_instance_connect_port(instance, 1, &ctx->out);
  lilv_instance_run(instance, 1);
  lilv_instance_run(instance, 2);
  assert(ctx->in == 1.0);
  assert(ctx->out == 1.0);

  // Save state to a bundle
  char* const      bundle_path = zix_path_join(NULL, dirs.top, "state.lv2/");
  LilvState* const state =
    state_from_instance(plugin, instance, ctx, &dirs, bundle_path);

  assert(!lilv_state_save(ctx->env->world,
                          &ctx->map,
                          &ctx->unmap,
                          state,
                          NULL,
                          bundle_path,
                          "state.ttl"));

  // Count the number of shared files before doing anything
  unsigned n_shared_files_before = 0;
  zix_dir_for_each(dirs.shared, &n_shared_files_before, count_file);

  lilv_instance_free(instance);

  // Delete the state
  assert(!lilv_state_delete(ctx->env->world, state));

  // Ensure the number of shared files is the same after deletion
  unsigned n_shared_files_after = 0;
  zix_dir_for_each(dirs.shared, &n_shared_files_after, count_file);
  assert(n_shared_files_before == n_shared_files_after);

  // Ensure the state directory has been deleted
  assert(zix_file_type(bundle_path) == ZIX_FILE_TYPE_NONE);

  cleanup_test_directories(dirs);

  lilv_state_free(state);
  free(bundle_path);
  test_context_free(ctx);
}

int
main(void)
{
  test_instance_state();
  test_default_state();
  test_equal();
  test_changed_plugin_data();
  test_changed_metadata();
  test_to_string();
  test_string_round_trip();
  test_to_files();
  test_multi_save();
  test_files_round_trip();
  test_world_round_trip();
  test_label_round_trip();
  test_bad_subject();
  test_missing_path();
  test_delete();

  return 0;
}
