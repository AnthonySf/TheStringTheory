// Copyright 2007-2020 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "lilv_test_utils.h"

#include <lilv/lilv.h>

#include <assert.h>
#include <string.h>

static const char* const manifest_ttl = "\
:prot\n\
	a lv2:PluginBase ;\n\
	rdfs:seeAlso <plugin.ttl> .\n\
\n\
:plug\n\
	a lv2:Plugin ;\n\
	lv2:binary <inst" SHLIB_EXT "> ;\n\
	lv2:prototype :prot .\n";

static const char* const plugin_ttl = "\
:prot\n\
	a lv2:Plugin ;\n\
	a lv2:CompressorPlugin ;\n\
	lv2:project [\n\
		doap:name \"Fake project\" ;\n\
	] ;\n\
	lv2:port [\n\
		a lv2:ControlPort ;\n\
		a lv2:InputPort ;\n\
		lv2:index 0 ;\n\
		lv2:symbol \"foo\" ;\n\
		lv2:name \"bar\" ;\n\
		lv2:minimum -1.0 ;\n\
		lv2:maximum 1.0 ;\n\
		lv2:default 0.5\n\
	] , [\n\
		a lv2:ControlPort ;\n\
		a lv2:InputPort ;\n\
		lv2:index 1 ;\n\
		lv2:symbol \"bar\" ;\n\
		lv2:name \"Baz\" ;\n\
		lv2:minimum -2.0 ;\n\
		lv2:maximum 2.0 ;\n\
		lv2:default 1.0\n\
	] , [\n\
		a lv2:ControlPort ;\n\
		a lv2:OutputPort ;\n\
		lv2:index 2 ;\n\
		lv2:symbol \"latency\" ;\n\
		lv2:name \"Latency\" ;\n\
		lv2:portProperty lv2:reportsLatency ;\n\
		lv2:designation lv2:latency\n\
	] .\n\
\n\
:plug doap:name \"Instance\" .\n";

int
main(void)
{
  LilvTestEnv* const     env   = lilv_test_env_new();
  const LilvWorld* const world = env->world;

  if (create_bundle(env, "prototype.lv2", manifest_ttl, plugin_ttl)) {
    return 1;
  }

  lilv_world_load_specifications(env->world);
  lilv_world_load_bundle(env->world, env->test_bundle_uri);

  const LilvPlugins* plugins = lilv_world_get_all_plugins(world);
  const LilvPlugin*  plug = lilv_plugins_get_by_uri(plugins, env->plugin1_uri);
  assert(plug);

  // Test non-inherited property
  LilvNode* name = lilv_plugin_get_name(plug);
  assert(!strcmp(lilv_node_as_string(name), "Instance"));
  lilv_node_free(name);

  // Test inherited property
  const LilvNode* binary = lilv_plugin_get_library_uri(plug);
  assert(strstr(lilv_node_as_string(binary), "inst" SHLIB_EXT));

  delete_bundle(env);
  lilv_test_env_free(env);

  return 0;
}
