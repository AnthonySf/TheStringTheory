// Copyright 2007-2020 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "lilv_test_utils.h"

#include <lilv/lilv.h>

#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static const char* const plugin_ttl = "\
@prefix lv2ui: <http://lv2plug.in/ns/extensions/ui#> .\n\
:plug a lv2:Plugin ;\n\
	a lv2:CompressorPlugin ;\n\
	doap:name \"Test plugin\" ;\n\
	lv2:optionalFeature lv2:hardRTCapable ;\n\
	lv2:requiredFeature <http://lv2plug.in/ns/ext/event> ;\n\
	lv2ui:ui :ui , :ui2 , :ui3 , :ui4 ;\n\
	doap:maintainer [\n\
		foaf:name \"David Robillard\" ;\n\
		foaf:homepage <http://drobilla.net> ;\n\
		foaf:mbox <mailto:d@drobilla.net>\n\
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
		lv2:portProperty lv2:reportsLatency\n\
	] .\n\
\n\
	:ui\n\
		a lv2ui:GtkUI ;\n\
		lv2ui:requiredFeature lv2ui:makeResident ;\n\
		lv2ui:binary <ui" SHLIB_EXT "> ;\n\
		lv2ui:optionalFeature lv2ui:ext_presets .\n\
\n\
	:ui2\n\
		a lv2ui:GtkUI ;\n\
		lv2ui:binary <ui2" SHLIB_EXT "> .\n\
\n\
	:ui3\n\
		a lv2ui:GtkUI ;\n\
		lv2ui:binary <ui3" SHLIB_EXT "> .\n\
\n\
	:ui4\n\
		a lv2ui:GtkUI ;\n\
		lv2ui:binary <ui4" SHLIB_EXT "> .\n";

static unsigned
ui_supported(const char* container_type_uri, const char* ui_type_uri)
{
  return !strcmp(container_type_uri, ui_type_uri);
}

int
main(void)
{
  LilvTestEnv* const env   = lilv_test_env_new();
  LilvWorld* const   world = env->world;

  if (create_bundle(env, "ui.lv2", SIMPLE_MANIFEST_TTL, plugin_ttl)) {
    return 1;
  }

  lilv_world_load_specifications(env->world);
  lilv_world_load_bundle(env->world, env->test_bundle_uri);

  const LilvPlugins* plugins = lilv_world_get_all_plugins(world);
  const LilvPlugin*  plug = lilv_plugins_get_by_uri(plugins, env->plugin1_uri);
  assert(plug);

  LilvUIs* uis = lilv_plugin_get_uis(plug);
  assert(uis);
  assert(lilv_uis_size(uis) == 4);

  const LilvUI* ui0 = lilv_uis_get(uis, lilv_uis_begin(uis));
  assert(ui0);

  LilvNode* ui_uri   = lilv_new_uri(world, "http://example.org/ui");
  LilvNode* ui2_uri  = lilv_new_uri(world, "http://example.org/ui3");
  LilvNode* ui3_uri  = lilv_new_uri(world, "http://example.org/ui4");
  LilvNode* noui_uri = lilv_new_uri(world, "http://example.org/notaui");

  const LilvUI* ui0_2 = lilv_uis_get_by_uri(uis, ui_uri);
  assert(ui0 == ui0_2);
  assert(lilv_node_equals(lilv_ui_get_uri(ui0_2), ui_uri));

  const LilvUI* ui2 = lilv_uis_get_by_uri(uis, ui2_uri);
  assert(ui2 != ui0);

  const LilvUI* ui3 = lilv_uis_get_by_uri(uis, ui3_uri);
  assert(ui3 != ui0);

  const LilvUI* noui = lilv_uis_get_by_uri(uis, noui_uri);
  assert(noui == NULL);

  const LilvNodes* classes = lilv_ui_get_classes(ui0);
  assert(lilv_nodes_size(classes) == 1);

  LilvNode* ui_class_uri =
    lilv_new_uri(world, "http://lv2plug.in/ns/extensions/ui#GtkUI");

  LilvNode* unknown_ui_class_uri =
    lilv_new_uri(world, "http://example.org/mysteryUI");

  assert(lilv_node_equals(lilv_nodes_get_first(classes), ui_class_uri));
  assert(lilv_ui_is_a(ui0, ui_class_uri));

  const LilvNode* ui_type = NULL;
  assert(lilv_ui_is_supported(ui0, ui_supported, ui_class_uri, &ui_type));
  assert(
    !lilv_ui_is_supported(ui0, ui_supported, unknown_ui_class_uri, &ui_type));
  assert(lilv_node_equals(ui_type, ui_class_uri));

  const LilvNode* plug_bundle_uri = lilv_plugin_get_bundle_uri(plug);
  const LilvNode* ui_bundle_uri   = lilv_ui_get_bundle_uri(ui0);
  assert(lilv_node_equals(plug_bundle_uri, ui_bundle_uri));

  const size_t ui_binary_uri_str_len =
    strlen(lilv_node_as_string(plug_bundle_uri)) + strlen("ui" SHLIB_EXT);

  char* ui_binary_uri_str = (char*)calloc(1, ui_binary_uri_str_len + 1);
  assert(ui_binary_uri_str);
  snprintf(ui_binary_uri_str,
           ui_binary_uri_str_len + 1,
           "%s%s",
           lilv_node_as_string(plug_bundle_uri),
           "ui" SHLIB_EXT);

  const LilvNode* ui_binary_uri = lilv_ui_get_binary_uri(ui0);

  LilvNode* expected_uri = lilv_new_uri(world, ui_binary_uri_str);
  assert(lilv_node_equals(expected_uri, ui_binary_uri));

  free(ui_binary_uri_str);
  lilv_node_free(unknown_ui_class_uri);
  lilv_node_free(ui_class_uri);
  lilv_node_free(ui_uri);
  lilv_node_free(ui2_uri);
  lilv_node_free(ui3_uri);
  lilv_node_free(noui_uri);
  lilv_node_free(expected_uri);
  lilv_uis_free(uis);

  delete_bundle(env);
  lilv_test_env_free(env);

  return 0;
}
