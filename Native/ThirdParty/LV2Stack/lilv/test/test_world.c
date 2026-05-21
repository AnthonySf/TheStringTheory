// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "lilv_test_utils.h"

#include <lilv/lilv.h>

#include <assert.h>
#include <stddef.h>

static void
test_free(void)
{
  lilv_world_free(NULL);
}

static void
test_set_option(void)
{
  LilvTestEnv* const env        = lilv_test_env_new();
  LilvNode* const    not_leaked = lilv_new_string(env->world, "/not/leaked");
  LilvNode* const    new_path   = lilv_new_string(env->world, "/new/path");

  lilv_world_set_option(env->world, LILV_OPTION_LV2_PATH, not_leaked);

  // Rely on sanitizers to catch a potential memory leak here
  lilv_world_set_option(env->world, LILV_OPTION_LV2_PATH, new_path);

  lilv_node_free(new_path);
  lilv_node_free(not_leaked);
  lilv_test_env_free(env);
}

static void
test_load_plugin_classes(void)
{
  LilvTestEnv* const env = lilv_test_env_new();

  lilv_world_load_all(env->world);

  // Rely on sanitizers to catch a potential memory leak here
  lilv_world_load_plugin_classes(env->world);

  lilv_test_env_free(env);
}

static void
test_search(void)
{
  LilvTestEnv* const env   = lilv_test_env_new();
  LilvWorld* const   world = env->world;

  LilvNode* num = lilv_new_int(env->world, 4);
  LilvNode* uri = lilv_new_uri(env->world, "http://example.org/object");

  const LilvNodes* matches = lilv_world_find_nodes(world, num, NULL, NULL);
  assert(!matches);

  matches = lilv_world_find_nodes(world, NULL, num, NULL);
  assert(!matches);

  matches = lilv_world_find_nodes(world, NULL, uri, NULL);
  assert(!matches);

  matches = lilv_world_find_nodes(world, uri, NULL, uri);
  assert(!matches);

  lilv_node_free(uri);
  lilv_node_free(num);

  lilv_world_unload_bundle(world, NULL);
  lilv_test_env_free(env);
}

int
main(void)
{
  test_free();
  test_set_option();
  test_load_plugin_classes();
  test_search();
}
