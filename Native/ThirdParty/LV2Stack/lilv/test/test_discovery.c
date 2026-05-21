// Copyright 2007-2020 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "lilv_test_utils.h"

#include <lilv/lilv.h>

#include <assert.h>
#include <string.h>

static const char* const plugin_ttl = "\
:plug a lv2:Plugin ;\n\
	doap:name \"Test plugin\" ;\n\
	lv2:port [\n\
		a lv2:ControlPort ;\n\
		a lv2:InputPort ;\n\
		lv2:index 0 ;\n\
		lv2:symbol \"foo\" ;\n\
		lv2:name \"bar\" ;\n\
	] .\n";

static int discovery_plugin_found = 0;

static void
discovery_verify_plugin(const LilvTestEnv* env, const LilvPlugin* plugin)
{
  const LilvNode* value = lilv_plugin_get_uri(plugin);
  if (lilv_node_equals(value, env->plugin1_uri)) {
    const LilvNode* lib_uri = NULL;
    assert(!lilv_node_equals(value, env->plugin2_uri));
    discovery_plugin_found = 1;
    lib_uri                = lilv_plugin_get_library_uri(plugin);
    assert(lib_uri);
    assert(lilv_node_is_uri(lib_uri));
    assert(lilv_node_as_uri(lib_uri));
    assert(strstr(lilv_node_as_uri(lib_uri), "foo" SHLIB_EXT));
    assert(lilv_plugin_verify(plugin));
  }
}

int
main(void)
{
  LilvTestEnv* const     env   = lilv_test_env_new();
  const LilvWorld* const world = env->world;

  if (create_bundle(env, "discovery.lv2", SIMPLE_MANIFEST_TTL, plugin_ttl)) {
    return 1;
  }

  lilv_world_load_specifications(env->world);
  lilv_world_load_bundle(env->world, env->test_bundle_uri);

  const LilvPlugins* plugins = lilv_world_get_all_plugins(world);
  assert(lilv_plugins_size(plugins) > 0);

  const LilvPlugin* plug1 = lilv_plugins_get_by_uri(plugins, env->plugin1_uri);

  const LilvPlugin* plug2 = lilv_plugins_get_by_uri(plugins, env->plugin2_uri);

  assert(plug1);
  assert(!plug2);

  {
    LilvNode* name = lilv_plugin_get_name(plug1);
    assert(!strcmp(lilv_node_as_string(name), "Test plugin"));
    lilv_node_free(name);
  }

  discovery_plugin_found = 0;
  LILV_FOREACH (plugins, i, plugins) {
    discovery_verify_plugin(env, lilv_plugins_get(plugins, i));
  }

  assert(discovery_plugin_found);
  plugins = NULL;

  delete_bundle(env);
  lilv_test_env_free(env);

  return 0;
}
