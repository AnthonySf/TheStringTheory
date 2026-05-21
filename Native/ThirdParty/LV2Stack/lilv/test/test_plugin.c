// Copyright 2007-2020 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "lilv_test_utils.h"

#include <lilv/lilv.h>

#include <assert.h>
#include <float.h>
#include <math.h>
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>

static const char* const plugin_ttl = "\
:plug\n\
	a lv2:Plugin ;\n\
	a lv2:CompressorPlugin ;\n\
	doap:name \"Test plugin\" ;\n\
	lv2:optionalFeature lv2:hardRTCapable ;\n\
	lv2:requiredFeature <http://lv2plug.in/ns/ext/event> ;\n\
	lv2:extensionData <http://example.org/extdata> ;\n\
	:foo 1.6180 ;\n\
	:bar true ;\n\
	:baz false ;\n\
	:blank [ a <http://example.org/blank> ] ;\n\
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
		lv2:portProperty lv2:reportsLatency ;\n\
		lv2:designation lv2:latency\n\
] .\n\
\n\
:thing doap:name \"Another\"@en , \"Andere\"@de .\n";

static void
set_language(LilvWorld* const world, const char* const lang)
{
  LilvNode* node = lilv_new_string(world, lang);
  lilv_world_set_option(world, LILV_OPTION_LANG, node);
  lilv_node_free(node);
}

static void
check_thing_name(LilvWorld* const  world,
                 const char* const subject_uri,
                 const char* const expected)
{
  LilvNode*  thing_uri = lilv_new_uri(world, subject_uri);
  LilvNode*  name_p = lilv_new_uri(world, "http://usefulinc.com/ns/doap#name");
  LilvNodes* thing_names =
    lilv_world_find_nodes(world, thing_uri, name_p, NULL);

  if (lilv_nodes_size(thing_names) == 1) {
    assert(expected);

    // Check name in nodes collection
    const LilvNode* thing_name = lilv_nodes_get_first(thing_names);
    assert(thing_name);
    assert(lilv_node_is_string(thing_name));
    assert(expected);
    assert(!strcmp(lilv_node_as_string(thing_name), expected));

    // Check that it matches the name via lilv_world_get()
    LilvNode* thing_name2 = lilv_world_get(world, thing_uri, name_p, NULL);
    assert(lilv_node_equals(thing_name, thing_name2));
    lilv_node_free(thing_name2);

  } else {
    assert(!expected);
    assert(!lilv_nodes_size(thing_names));
  }

  lilv_nodes_free(thing_names);
  lilv_node_free(name_p);
  lilv_node_free(thing_uri);
}

int
main(void)
{
  LilvTestEnv* const env   = lilv_test_env_new();
  LilvWorld* const   world = env->world;

  lilv_world_load_all(world);

  if (create_bundle(env, "plugin.lv2", SIMPLE_MANIFEST_TTL, plugin_ttl)) {
    return 1;
  }

  lilv_world_load_specifications(env->world);
  lilv_world_load_bundle(env->world, env->test_bundle_uri);

  const LilvPlugins* plugins = lilv_world_get_all_plugins(world);
  const LilvPlugin*  plug = lilv_plugins_get_by_uri(plugins, env->plugin1_uri);
  const LilvNode*    plug_uri = lilv_plugin_get_uri(plug);
  assert(plug);

  const LilvPluginClass* klass     = lilv_plugin_get_class(plug);
  const LilvNode*        klass_uri = lilv_plugin_class_get_uri(klass);
  assert(!strcmp(lilv_node_as_string(klass_uri),
                 "http://lv2plug.in/ns/lv2core#CompressorPlugin"));

  LilvNode* rdf_type =
    lilv_new_uri(world, "http://www.w3.org/1999/02/22-rdf-syntax-ns#type");
  assert(lilv_world_ask(world, plug_uri, rdf_type, klass_uri));
  lilv_node_free(rdf_type);

  assert(!lilv_plugin_is_replaced(plug));
  assert(!lilv_plugin_get_related(plug, NULL));

  const LilvNode* plug_bundle_uri = lilv_plugin_get_bundle_uri(plug);
  assert(lilv_node_equals(plug_bundle_uri, env->test_bundle_uri));

  const LilvNodes* data_uris = lilv_plugin_get_data_uris(plug);
  assert(lilv_nodes_size(data_uris) == 2);

  const LilvNode* project = lilv_plugin_get_project(plug);
  assert(!project);

  char* manifest_uri =
    string_concat(lilv_node_as_string(plug_bundle_uri), "manifest.ttl");

  char* data_uri =
    string_concat(lilv_node_as_string(plug_bundle_uri), "plugin.ttl");

  LilvNode* manifest_uri_val = lilv_new_uri(world, manifest_uri);
  assert(lilv_nodes_contains(data_uris, manifest_uri_val));
  lilv_node_free(manifest_uri_val);

  LilvNode* data_uri_val = lilv_new_uri(world, data_uri);
  assert(lilv_nodes_contains(data_uris, data_uri_val));
  lilv_node_free(data_uri_val);

  LilvNode* unknown_uri_val = lilv_new_uri(world, "http://example.org/unknown");
  assert(!lilv_nodes_contains(data_uris, unknown_uri_val));
  lilv_node_free(unknown_uri_val);

  free(manifest_uri);
  free(data_uri);

  float mins[3];
  float maxs[3];
  float defs[3];
  lilv_plugin_get_port_ranges_float(plug, mins, maxs, defs);
  assert(mins[0] == -1.0f);
  assert(maxs[0] == 1.0f);
  assert(defs[0] == 0.5f);

  LilvNode* audio_class =
    lilv_new_uri(world, "http://lv2plug.in/ns/lv2core#AudioPort");
  LilvNode* control_class =
    lilv_new_uri(world, "http://lv2plug.in/ns/lv2core#ControlPort");
  LilvNode* in_class =
    lilv_new_uri(world, "http://lv2plug.in/ns/lv2core#InputPort");
  LilvNode* out_class =
    lilv_new_uri(world, "http://lv2plug.in/ns/lv2core#OutputPort");

  assert(lilv_plugin_get_num_ports_of_class(plug, control_class, NULL) == 3);
  assert(lilv_plugin_get_num_ports_of_class(plug, audio_class, NULL) == 0);
  assert(lilv_plugin_get_num_ports_of_class(plug, in_class, NULL) == 2);
  assert(lilv_plugin_get_num_ports_of_class(plug, out_class, NULL) == 1);
  assert(lilv_plugin_get_num_ports_of_class(
           plug, control_class, in_class, NULL) == 2);
  assert(lilv_plugin_get_num_ports_of_class(
           plug, control_class, out_class, NULL) == 1);
  assert(
    lilv_plugin_get_num_ports_of_class(plug, audio_class, in_class, NULL) == 0);
  assert(lilv_plugin_get_num_ports_of_class(
           plug, audio_class, out_class, NULL) == 0);

  assert(lilv_plugin_has_latency(plug));
  assert(lilv_plugin_get_latency_port_index(plug) == 2);

  LilvNode* lv2_latency =
    lilv_new_uri(world, "http://lv2plug.in/ns/lv2core#latency");
  const LilvPort* latency_port =
    lilv_plugin_get_port_by_designation(plug, out_class, lv2_latency);
  lilv_node_free(lv2_latency);

  assert(latency_port);
  assert(lilv_port_get_index(plug, latency_port) == 2);
  assert(lilv_node_is_blank(lilv_port_get_node(plug, latency_port)));

  LilvNode* rt_feature =
    lilv_new_uri(world, "http://lv2plug.in/ns/lv2core#hardRTCapable");
  LilvNode* event_feature =
    lilv_new_uri(world, "http://lv2plug.in/ns/ext/event");
  LilvNode* pretend_feature =
    lilv_new_uri(world, "http://example.org/solvesWorldHunger");

  assert(lilv_plugin_has_feature(plug, rt_feature));
  assert(lilv_plugin_has_feature(plug, event_feature));
  assert(!lilv_plugin_has_feature(plug, pretend_feature));

  lilv_node_free(rt_feature);
  lilv_node_free(event_feature);
  lilv_node_free(pretend_feature);

  LilvNodes* supported = lilv_plugin_get_supported_features(plug);
  LilvNodes* required  = lilv_plugin_get_required_features(plug);
  LilvNodes* optional  = lilv_plugin_get_optional_features(plug);
  assert(lilv_nodes_size(supported) == 2);
  assert(lilv_nodes_size(required) == 1);
  assert(lilv_nodes_size(optional) == 1);
  lilv_nodes_free(supported);
  lilv_nodes_free(required);
  lilv_nodes_free(optional);

  LilvNode*  foo_p = lilv_new_uri(world, "http://example.org/foo");
  LilvNodes* foos  = lilv_plugin_get_value(plug, foo_p);
  assert(lilv_nodes_size(foos) == 1);
  assert(fabs(lilv_node_as_float(lilv_nodes_get_first(foos)) - 1.6180) <
         FLT_EPSILON);
  lilv_node_free(foo_p);
  lilv_nodes_free(foos);

  LilvNode*  bar_p = lilv_new_uri(world, "http://example.org/bar");
  LilvNodes* bars  = lilv_plugin_get_value(plug, bar_p);
  assert(lilv_nodes_size(bars) == 1);
  assert(lilv_node_as_bool(lilv_nodes_get_first(bars)) == true);
  lilv_node_free(bar_p);
  lilv_nodes_free(bars);

  LilvNode*  baz_p = lilv_new_uri(world, "http://example.org/baz");
  LilvNodes* bazs  = lilv_plugin_get_value(plug, baz_p);
  assert(lilv_nodes_size(bazs) == 1);
  assert(lilv_node_as_bool(lilv_nodes_get_first(bazs)) == false);
  lilv_node_free(baz_p);
  lilv_nodes_free(bazs);

  LilvNode*  blank_p = lilv_new_uri(world, "http://example.org/blank");
  LilvNodes* blanks  = lilv_plugin_get_value(plug, blank_p);
  assert(lilv_nodes_size(blanks) == 1);
  const LilvNode* blank = lilv_nodes_get_first(blanks);
  assert(lilv_node_is_blank(blank));
  const char* blank_str = lilv_node_as_blank(blank);
  char*       blank_tok = lilv_node_get_turtle_token(blank);
  assert(!strncmp(blank_tok, "_:", 2));
  assert(!strcmp(blank_tok + 2, blank_str));
  lilv_free(blank_tok);
  lilv_node_free(blank_p);
  lilv_nodes_free(blanks);

  LilvNode* author_name = lilv_plugin_get_author_name(plug);
  assert(!strcmp(lilv_node_as_string(author_name), "David Robillard"));
  lilv_node_free(author_name);

  LilvNode* author_email = lilv_plugin_get_author_email(plug);
  assert(!strcmp(lilv_node_as_string(author_email), "mailto:d@drobilla.net"));
  lilv_node_free(author_email);

  LilvNode* author_homepage = lilv_plugin_get_author_homepage(plug);
  assert(!strcmp(lilv_node_as_string(author_homepage), "http://drobilla.net"));
  lilv_node_free(author_homepage);

  set_language(world, "C");
  check_thing_name(world, "http://example.org/plug", "Test plugin");
  set_language(world, "en_CA");
  check_thing_name(world, "http://example.org/plug", "Test plugin");
  check_thing_name(world, "http://example.org/thing", "Another");
  set_language(world, "de");
  check_thing_name(world, "http://example.org/plug", "Test plugin");
  check_thing_name(world, "http://example.org/thing", "Andere");
  set_language(world, "es");
  check_thing_name(world, "http://example.org/plug", "Test plugin");
  check_thing_name(world, "http://example.org/thing", NULL);

  LilvUIs* uis = lilv_plugin_get_uis(plug);
  assert(lilv_uis_size(uis) == 0);
  lilv_uis_free(uis);

  LilvNode*  extdata   = lilv_new_uri(world, "http://example.org/extdata");
  LilvNode*  noextdata = lilv_new_uri(world, "http://example.org/noextdata");
  LilvNodes* extdatas  = lilv_plugin_get_extension_data(plug);
  assert(lilv_plugin_has_extension_data(plug, extdata));
  assert(!lilv_plugin_has_extension_data(plug, noextdata));
  assert(lilv_nodes_size(extdatas) == 1);
  assert(lilv_node_equals(lilv_nodes_get_first(extdatas), extdata));
  lilv_node_free(noextdata);
  lilv_node_free(extdata);
  lilv_nodes_free(extdatas);

  LilvNode* unknown_p = lilv_new_uri(world, "http://example.org/unknown");
  assert(!lilv_world_get(world, plug_uri, unknown_p, NULL));
  lilv_node_free(unknown_p);

  lilv_node_free(control_class);
  lilv_node_free(audio_class);
  lilv_node_free(in_class);
  lilv_node_free(out_class);

  delete_bundle(env);
  lilv_test_env_free(env);

  return 0;
}
