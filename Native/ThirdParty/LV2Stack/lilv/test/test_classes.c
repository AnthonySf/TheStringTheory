// Copyright 2007-2020 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "lilv_test_utils.h"

#include <lilv/lilv.h>

#include <assert.h>
#include <string.h>

static const char* const plugin_ttl = "\
:plug a lv2:Plugin ;\n\
	a lv2:CompressorPlugin ;\n\
	doap:name \"Test plugin\" ;\n\
	lv2:port [\n\
		a lv2:ControlPort ;\n\
		a lv2:InputPort ;\n\
		lv2:index 0 ;\n\
		lv2:symbol \"foo\" ;\n\
		lv2:name \"Foo\" ;\n\
] .";

int
main(void)
{
  LilvTestEnv* const env   = lilv_test_env_new();
  LilvWorld* const   world = env->world;

  lilv_world_load_all(world);

  if (create_bundle(env, "classes.lv2", SIMPLE_MANIFEST_TTL, plugin_ttl)) {
    return 1;
  }

  lilv_world_load_specifications(env->world);
  lilv_world_load_bundle(env->world, env->test_bundle_uri);

  const LilvPluginClass*   plugin   = lilv_world_get_plugin_class(world);
  const LilvPluginClasses* classes  = lilv_world_get_plugin_classes(world);
  LilvPluginClasses*       children = lilv_plugin_class_get_children(plugin);

  assert(lilv_plugin_class_get_parent_uri(plugin) == NULL);
  assert(lilv_plugin_classes_size(classes) >
         lilv_plugin_classes_size(children));
  assert(!strcmp(lilv_node_as_string(lilv_plugin_class_get_label(plugin)),
                 "Plugin"));
  assert(!strcmp(lilv_node_as_string(lilv_plugin_class_get_uri(plugin)),
                 "http://lv2plug.in/ns/lv2core#Plugin"));

  LILV_FOREACH (plugin_classes, i, children) {
    assert(lilv_node_equals(
      lilv_plugin_class_get_parent_uri(lilv_plugin_classes_get(children, i)),
      lilv_plugin_class_get_uri(plugin)));
  }

  LilvNode* some_uri = lilv_new_uri(world, "http://example.org/whatever");
  assert(lilv_plugin_classes_get_by_uri(classes, some_uri) == NULL);
  lilv_node_free(some_uri);

  lilv_plugin_classes_free(children);
  delete_bundle(env);
  lilv_test_env_free(env);

  return 0;
}
