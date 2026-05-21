// Copyright 2015-2020 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include <lilv/lilv.h>

#include <assert.h>
#include <stdio.h>

#define PLUGIN_URI "http://example.org/bad-syntax"

int
main(int argc, char** argv)
{
  if (argc != 2) {
    fprintf(stderr, "USAGE: %s BUNDLE\n", argv[0]);
    return 1;
  }

  const char* bundle_path = argv[1];
  LilvWorld*  world       = lilv_world_new();

  // Load test plugin bundle
  LilvNode* bundle_uri = lilv_new_file_uri(world, NULL, bundle_path);
  lilv_world_load_bundle(world, bundle_uri);
  lilv_node_free(bundle_uri);

  LilvNode*          plugin_uri = lilv_new_uri(world, PLUGIN_URI);
  const LilvPlugins* plugins    = lilv_world_get_all_plugins(world);
  const LilvPlugin*  plugin     = lilv_plugins_get_by_uri(plugins, plugin_uri);

  assert(!lilv_plugin_get_name(plugin));
  assert(!lilv_plugin_instantiate(plugin, 48000, NULL));

  lilv_node_free(plugin_uri);
  lilv_world_free(world);

  return 0;
}
