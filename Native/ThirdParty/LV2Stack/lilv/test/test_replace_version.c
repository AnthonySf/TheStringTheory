// Copyright 2007-2020 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "lilv_test_utils.h"

#include <lilv/lilv.h>
#include <lv2/core/lv2.h>

#include <assert.h>
#include <stdlib.h>
#include <string.h>

int
main(void)
{
  LilvTestEnv* const env   = lilv_test_env_new();
  LilvWorld* const   world = env->world;

  LilvNode* plug_uri = lilv_new_uri(world, "http://example.org/versioned");
  LilvNode* lv2_minorVersion = lilv_new_uri(world, LV2_CORE__minorVersion);
  LilvNode* lv2_microVersion = lilv_new_uri(world, LV2_CORE__microVersion);
  LilvNode* minor            = NULL;
  LilvNode* micro            = NULL;

  char* old_bundle_path = string_concat(LILV_TEST_DIR, "old_version.lv2/");

  // Load plugin from old bundle
  LilvNode* old_bundle = lilv_new_file_uri(world, NULL, old_bundle_path);
  lilv_world_load_bundle(world, old_bundle);
  lilv_world_load_resource(world, plug_uri);

  // Check version
  const LilvPlugins* plugins  = lilv_world_get_all_plugins(world);
  const LilvPlugin*  old_plug = lilv_plugins_get_by_uri(plugins, plug_uri);
  assert(old_plug);
  minor = lilv_world_get(world, plug_uri, lv2_minorVersion, 0);
  micro = lilv_world_get(world, plug_uri, lv2_microVersion, 0);
  assert(!strcmp(lilv_node_as_string(minor), "1"));
  assert(!strcmp(lilv_node_as_string(micro), "0"));
  lilv_node_free(micro);
  lilv_node_free(minor);

  char* new_bundle_path = string_concat(LILV_TEST_DIR, "new_version.lv2/");

  // Load plugin from new bundle
  LilvNode* new_bundle = lilv_new_file_uri(world, NULL, new_bundle_path);
  lilv_world_load_bundle(world, new_bundle);
  lilv_world_load_resource(world, plug_uri);

  // Check that version in the world model has changed
  plugins                    = lilv_world_get_all_plugins(world);
  const LilvPlugin* new_plug = lilv_plugins_get_by_uri(plugins, plug_uri);
  assert(new_plug);
  assert(lilv_node_equals(lilv_plugin_get_bundle_uri(new_plug), new_bundle));
  minor = lilv_world_get(world, plug_uri, lv2_minorVersion, 0);
  micro = lilv_world_get(world, plug_uri, lv2_microVersion, 0);
  assert(!strcmp(lilv_node_as_string(minor), "2"));
  assert(!strcmp(lilv_node_as_string(micro), "1"));
  lilv_node_free(micro);
  lilv_node_free(minor);

  // Try to load the old version again
  lilv_world_unload_bundle(world, old_bundle);
  lilv_world_load_bundle(world, old_bundle);
  lilv_world_load_resource(world, plug_uri);

  // Check that version in the world model has not changed
  plugins  = lilv_world_get_all_plugins(world);
  new_plug = lilv_plugins_get_by_uri(plugins, plug_uri);
  assert(new_plug);
  minor = lilv_world_get(world, plug_uri, lv2_minorVersion, 0);
  micro = lilv_world_get(world, plug_uri, lv2_microVersion, 0);
  assert(!strcmp(lilv_node_as_string(minor), "2"));
  assert(!strcmp(lilv_node_as_string(micro), "1"));
  lilv_node_free(micro);
  lilv_node_free(minor);

  lilv_node_free(new_bundle);
  lilv_node_free(old_bundle);
  free(new_bundle_path);
  free(old_bundle_path);
  lilv_node_free(plug_uri);
  lilv_node_free(lv2_minorVersion);
  lilv_node_free(lv2_microVersion);

  lilv_test_env_free(env);

  return 0;
}
