// Copyright 2007-2020 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "lilv_test_utils.h"

#include <lilv/lilv.h>

#include <assert.h>
#include <stdbool.h>
#include <string.h>

static const char* const plugin_ttl = "\
@prefix lv2ev: <http://lv2plug.in/ns/ext/event#> . \n\
:plug\n\
	a lv2:Plugin ;\n\
	doap:name \"Test plugin\" ;\n\
	doap:homepage <http://example.org/someplug> ;\n\
	lv2:port [\n\
		a lv2:ControlPort ;\n\
		a lv2:InputPort ;\n\
		lv2:index 0 ;\n\
		lv2:symbol \"foo\" ;\n\
		lv2:name \"store\" ;\n\
		lv2:name \"Laden\"@de-de ;\n\
		lv2:name \"Geschaeft\"@de-at ;\n\
		lv2:name \"tienda\"@es ;\n\
		rdfs:comment \"comment\"@en , \"commentaires\"@fr ;\n\
		lv2:portProperty lv2:integer ;\n\
		lv2:minimum -1.0 ;\n\
		lv2:maximum 1.0 ;\n\
		lv2:default 0.5 ;\n\
		lv2:scalePoint [\n\
			rdfs:label \"Sin\";\n\
			rdf:value 3\n\
		] ;\n\
		lv2:scalePoint [\n\
			rdfs:label \"Cos\";\n\
			rdf:value 4\n\
		]\n\
	] , [\n\
		a lv2:EventPort ;\n\
		a lv2:InputPort ;\n\
		lv2:index 1 ;\n\
		lv2:symbol \"event_in\" ;\n\
		lv2:name \"Event Input\" ;\n\
		lv2ev:supportsEvent <http://example.org/event> ;\n\
		atom:supports <http://example.org/atomEvent>\n\
	] , [\n\
		a lv2:AudioPort ;\n\
		a lv2:InputPort ;\n\
		lv2:index 2 ;\n\
		lv2:symbol \"audio_in\" ;\n\
		lv2:name \"Audio Input\" ;\n\
	] , [\n\
		a lv2:AudioPort ;\n\
		a lv2:OutputPort ;\n\
		lv2:index 3 ;\n\
		lv2:symbol \"audio_out\" ;\n\
		lv2:name \"Audio Output\" ;\n\
	] .\n";

static void
set_world_lang(LilvWorld* const world, const char* const lang)
{
  LilvNode* const lang_node = lilv_new_string(world, lang);
  lilv_world_set_option(world, LILV_OPTION_LANG, lang_node);
  lilv_node_free(lang_node);
}

int
main(void)
{
  LilvTestEnv* const env   = lilv_test_env_new();
  LilvWorld* const   world = env->world;

  if (create_bundle(env, "port.lv2", SIMPLE_MANIFEST_TTL, plugin_ttl)) {
    return 1;
  }

  lilv_world_load_specifications(env->world);
  lilv_world_load_bundle(env->world, env->test_bundle_uri);

  const LilvPlugins* plugins = lilv_world_get_all_plugins(world);
  const LilvPlugin*  plug = lilv_plugins_get_by_uri(plugins, env->plugin1_uri);
  assert(plug);

  LilvNode*       psym = lilv_new_string(world, "foo");
  const LilvPort* p    = lilv_plugin_get_port_by_index(plug, 0);
  const LilvPort* p2   = lilv_plugin_get_port_by_symbol(plug, psym);
  lilv_node_free(psym);
  assert(p != NULL);
  assert(p2 != NULL);
  assert(p == p2);

  LilvNode*       nopsym = lilv_new_string(world, "thisaintnoportfoo");
  const LilvPort* p3     = lilv_plugin_get_port_by_symbol(plug, nopsym);
  assert(p3 == NULL);
  lilv_node_free(nopsym);

  // Try getting an invalid property
  LilvNode*        num     = lilv_new_int(world, 1);
  const LilvNodes* nothing = lilv_port_get_value(plug, p, num);
  assert(!nothing);
  lilv_node_free(num);

  LilvNode* audio_class =
    lilv_new_uri(world, "http://lv2plug.in/ns/lv2core#AudioPort");
  LilvNode* control_class =
    lilv_new_uri(world, "http://lv2plug.in/ns/lv2core#ControlPort");
  LilvNode* in_class =
    lilv_new_uri(world, "http://lv2plug.in/ns/lv2core#InputPort");
  LilvNode* out_class =
    lilv_new_uri(world, "http://lv2plug.in/ns/lv2core#OutputPort");

  assert(lilv_nodes_size(lilv_port_get_classes(plug, p)) == 2);
  assert(lilv_plugin_get_num_ports(plug) == 4);
  assert(lilv_port_is_a(plug, p, control_class));
  assert(lilv_port_is_a(plug, p, in_class));
  assert(!lilv_port_is_a(plug, p, audio_class));

  LilvNodes* port_properties = lilv_port_get_properties(plug, p);
  assert(lilv_nodes_size(port_properties) == 1);
  lilv_nodes_free(port_properties);

  // Untranslated name (current locale is set to "C" in main)
  assert(!strcmp(lilv_node_as_string(lilv_port_get_symbol(plug, p)), "foo"));
  LilvNode* name = lilv_port_get_name(plug, p);
  assert(!strcmp(lilv_node_as_string(name), "store"));
  lilv_node_free(name);

  // Exact language match
  set_world_lang(world, "de_DE");
  name = lilv_port_get_name(plug, p);
  assert(!strcmp(lilv_node_as_string(name), "Laden"));
  lilv_node_free(name);

  // Exact language match (with charset suffix)
  set_world_lang(world, "de_AT.utf8");
  name = lilv_port_get_name(plug, p);
  assert(!strcmp(lilv_node_as_string(name), "Geschaeft"));
  lilv_node_free(name);

  // Partial language match (choose value translated for different country)
  set_world_lang(world, "de_CH");
  name = lilv_port_get_name(plug, p);
  assert((!strcmp(lilv_node_as_string(name), "Laden")) ||
         (!strcmp(lilv_node_as_string(name), "Geschaeft")));
  lilv_node_free(name);

  // Partial language match (choose country-less language tagged value)
  set_world_lang(world, "es_MX");
  name = lilv_port_get_name(plug, p);
  assert(!strcmp(lilv_node_as_string(name), "tienda"));
  lilv_node_free(name);

  // No language match (choose untranslated value)
  set_world_lang(world, "cn");
  name = lilv_port_get_name(plug, p);
  assert(!strcmp(lilv_node_as_string(name), "store"));
  lilv_node_free(name);

  // Invalid language
  set_world_lang(world, "1!");
  name = lilv_port_get_name(plug, p);
  assert(!strcmp(lilv_node_as_string(name), "store"));
  lilv_node_free(name);

  set_world_lang(world, "en_CA.utf-8");

  // Language tagged value with no untranslated values
  LilvNode*  rdfs_comment = lilv_new_uri(world, LILV_NS_RDFS "comment");
  LilvNodes* comments     = lilv_port_get_value(plug, p, rdfs_comment);
  assert(
    !strcmp(lilv_node_as_string(lilv_nodes_get_first(comments)), "comment"));
  LilvNode* comment = lilv_port_get(plug, p, rdfs_comment);
  assert(!strcmp(lilv_node_as_string(comment), "comment"));
  lilv_node_free(comment);
  lilv_nodes_free(comments);

  set_world_lang(world, "fr");

  comments = lilv_port_get_value(plug, p, rdfs_comment);
  assert(!strcmp(lilv_node_as_string(lilv_nodes_get_first(comments)),
                 "commentaires"));
  lilv_nodes_free(comments);

  set_world_lang(world, "cn");

  comments = lilv_port_get_value(plug, p, rdfs_comment);
  assert(!comments);
  lilv_nodes_free(comments);

  lilv_node_free(rdfs_comment);

  set_world_lang(world, "C"); // Reset locale

  LilvScalePoints* points = lilv_port_get_scale_points(plug, p);
  assert(lilv_scale_points_size(points) == 2);

  LilvIter*             sp_iter = lilv_scale_points_begin(points);
  const LilvScalePoint* sp0     = lilv_scale_points_get(points, sp_iter);
  assert(sp0);
  sp_iter                   = lilv_scale_points_next(points, sp_iter);
  const LilvScalePoint* sp1 = lilv_scale_points_get(points, sp_iter);
  assert(sp1);

  assert(
    ((!strcmp(lilv_node_as_string(lilv_scale_point_get_label(sp0)), "Sin") &&
      lilv_node_as_float(lilv_scale_point_get_value(sp0)) == 3) &&
     (!strcmp(lilv_node_as_string(lilv_scale_point_get_label(sp1)), "Cos") &&
      lilv_node_as_float(lilv_scale_point_get_value(sp1)) == 4)) ||
    ((!strcmp(lilv_node_as_string(lilv_scale_point_get_label(sp0)), "Cos") &&
      lilv_node_as_float(lilv_scale_point_get_value(sp0)) == 4) &&
     (!strcmp(lilv_node_as_string(lilv_scale_point_get_label(sp1)), "Sin") &&
      lilv_node_as_float(lilv_scale_point_get_value(sp1)) == 3)));

  LilvNode* homepage_p =
    lilv_new_uri(world, "http://usefulinc.com/ns/doap#homepage");
  LilvNodes* homepages = lilv_plugin_get_value(plug, homepage_p);
  assert(lilv_nodes_size(homepages) == 1);
  assert(!strcmp(lilv_node_as_string(lilv_nodes_get_first(homepages)),
                 "http://example.org/someplug"));

  LilvNode* min = NULL;
  LilvNode* max = NULL;
  LilvNode* def = NULL;
  lilv_port_get_range(plug, p, &def, &min, &max);
  assert(def);
  assert(min);
  assert(max);
  assert(lilv_node_as_float(def) == 0.5);
  assert(lilv_node_as_float(min) == -1.0);
  assert(lilv_node_as_float(max) == 1.0);

  LilvNode* integer_prop =
    lilv_new_uri(world, "http://lv2plug.in/ns/lv2core#integer");
  LilvNode* toggled_prop =
    lilv_new_uri(world, "http://lv2plug.in/ns/lv2core#toggled");

  assert(lilv_port_has_property(plug, p, integer_prop));
  assert(!lilv_port_has_property(plug, p, toggled_prop));

  const LilvPort* ep = lilv_plugin_get_port_by_index(plug, 1);

  LilvNode* event_type   = lilv_new_uri(world, "http://example.org/event");
  LilvNode* event_type_2 = lilv_new_uri(world, "http://example.org/otherEvent");
  LilvNode* atom_event   = lilv_new_uri(world, "http://example.org/atomEvent");
  assert(lilv_port_supports_event(plug, ep, event_type));
  assert(!lilv_port_supports_event(plug, ep, event_type_2));
  assert(lilv_port_supports_event(plug, ep, atom_event));

  LilvNode*  name_p = lilv_new_uri(world, "http://lv2plug.in/ns/lv2core#name");
  LilvNodes* names  = lilv_port_get_value(plug, p, name_p);
  assert(lilv_nodes_size(names) == 1);
  assert(!strcmp(lilv_node_as_string(lilv_nodes_get_first(names)), "store"));
  lilv_nodes_free(names);

  LilvNode* true_val  = lilv_new_bool(world, true);
  LilvNode* false_val = lilv_new_bool(world, false);

  assert(!lilv_node_equals(true_val, false_val));

  lilv_world_set_option(world, LILV_OPTION_FILTER_LANG, false_val);
  names = lilv_port_get_value(plug, p, name_p);
  assert(lilv_nodes_size(names) == 4);
  lilv_nodes_free(names);
  lilv_world_set_option(world, LILV_OPTION_FILTER_LANG, true_val);

  lilv_node_free(false_val);
  lilv_node_free(true_val);

  names = lilv_port_get_value(plug, ep, name_p);
  assert(lilv_nodes_size(names) == 1);
  assert(
    !strcmp(lilv_node_as_string(lilv_nodes_get_first(names)), "Event Input"));

  const LilvPort* ap_in = lilv_plugin_get_port_by_index(plug, 2);

  assert(lilv_port_is_a(plug, ap_in, in_class));
  assert(!lilv_port_is_a(plug, ap_in, out_class));
  assert(lilv_port_is_a(plug, ap_in, audio_class));
  assert(!lilv_port_is_a(plug, ap_in, control_class));

  const LilvPort* ap_out = lilv_plugin_get_port_by_index(plug, 3);

  assert(lilv_port_is_a(plug, ap_out, out_class));
  assert(!lilv_port_is_a(plug, ap_out, in_class));
  assert(lilv_port_is_a(plug, ap_out, audio_class));
  assert(!lilv_port_is_a(plug, ap_out, control_class));

  assert(lilv_plugin_get_num_ports_of_class(
           plug, control_class, in_class, NULL) == 1);
  assert(
    lilv_plugin_get_num_ports_of_class(plug, audio_class, in_class, NULL) == 1);
  assert(lilv_plugin_get_num_ports_of_class(
           plug, audio_class, out_class, NULL) == 1);

  lilv_nodes_free(names);
  lilv_node_free(name_p);

  lilv_node_free(integer_prop);
  lilv_node_free(toggled_prop);
  lilv_node_free(event_type);
  lilv_node_free(event_type_2);
  lilv_node_free(atom_event);

  lilv_node_free(min);
  lilv_node_free(max);
  lilv_node_free(def);

  lilv_node_free(homepage_p);
  lilv_nodes_free(homepages);

  lilv_scale_points_free(points);
  lilv_node_free(control_class);
  lilv_node_free(audio_class);
  lilv_node_free(out_class);
  lilv_node_free(in_class);

  delete_bundle(env);
  lilv_test_env_free(env);

  return 0;
}
