// Copyright 2007-2020 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "lilv_test_utils.h"

#include <lilv/lilv.h>

#include <assert.h>
#include <string.h>

static const char* const plugin_ttl = "\
:plug\n\
	a lv2:Plugin;\n\
	a lv2:CompressorPlugin;\n\
	doap:name \"Test plugin with project\" ;\n\
	lv2:project [\n\
		doap:maintainer [\n\
			foaf:name \"David Robillard\" ;\n\
			foaf:homepage <http://drobilla.net> ;\n\
			foaf:mbox <mailto:d@drobilla.net>\n\
		] ;\n\
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
	] .\n";

int
main(void)
{
  LilvTestEnv* const     env   = lilv_test_env_new();
  const LilvWorld* const world = env->world;

  if (create_bundle(env, "project.lv2", SIMPLE_MANIFEST_TTL, plugin_ttl)) {
    return 1;
  }

  lilv_world_load_specifications(env->world);
  lilv_world_load_bundle(env->world, env->test_bundle_uri);

  const LilvPlugins* plugins = lilv_world_get_all_plugins(world);
  const LilvPlugin*  plug = lilv_plugins_get_by_uri(plugins, env->plugin1_uri);
  assert(plug);

  LilvNode* author_name = lilv_plugin_get_author_name(plug);
  assert(!strcmp(lilv_node_as_string(author_name), "David Robillard"));
  lilv_node_free(author_name);

  LilvNode* author_email = lilv_plugin_get_author_email(plug);
  assert(!strcmp(lilv_node_as_string(author_email), "mailto:d@drobilla.net"));
  lilv_node_free(author_email);

  LilvNode* author_homepage = lilv_plugin_get_author_homepage(plug);
  assert(!strcmp(lilv_node_as_string(author_homepage), "http://drobilla.net"));
  lilv_node_free(author_homepage);

  delete_bundle(env);
  lilv_test_env_free(env);

  return 0;
}
