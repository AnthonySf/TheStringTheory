// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "lilv_test_utils.h"

#include <lilv/lilv.h>

#include <assert.h>
#include <float.h>
#include <math.h>
#include <stdbool.h>
#include <string.h>

static const char* const plugin_ttl = "\
:plug a lv2:Plugin ;\n\
	a lv2:CompressorPlugin ;\n\
	doap:name \"Test plugin\" ;\n\
	:a-bool true ;\n\
	:a-integer 234 ;\n\
	:a-decimal 1.5 ;\n\
	:a-float \"5.65E1\"^^<http://www.w3.org/2001/XMLSchema#float> ;\n\
	:a-double \"7.8025E2\"^^<http://www.w3.org/2001/XMLSchema#double> ;\n\
	:a-inf \"INF\"^^<http://www.w3.org/2001/XMLSchema#float> ;\n\
	:a-p-inf \"+INF\"^^<http://www.w3.org/2001/XMLSchema#float> ;\n\
	:a-n-inf \"-INF\"^^<http://www.w3.org/2001/XMLSchema#float> ;\n\
	:a-nan \"NaN\"^^<http://www.w3.org/2001/XMLSchema#float> ;\n\
	lv2:port [\n\
		a lv2:ControlPort ; a lv2:InputPort ;\n\
		lv2:index 0 ;\n\
		lv2:symbol \"foo\" ;\n\
		lv2:name \"Foo\" ;\n\
	] .";

static void
test_file_uris(void)
{
  LilvTestEnv* const env   = lilv_test_env_new();
  LilvWorld* const   world = env->world;

#ifdef __clang__
#  pragma clang diagnostic push
#  pragma clang diagnostic ignored "-Wdeprecated-declarations"
#elif defined(__GNUC__) && __GNUC__ > 4
#  pragma GCC diagnostic push
#  pragma GCC diagnostic ignored "-Wdeprecated-declarations"
#endif

  const char* const path = lilv_uri_to_path("file:///foo");
  assert(path);
  assert(!strcmp(path, "/foo"));

#ifdef __clang__
#  pragma clang diagnostic pop
#elif defined(__GNUC__) && __GNUC__ > 4
#  pragma GCC diagnostic pop
#endif

  LilvNode* loc_abs  = lilv_new_file_uri(world, NULL, "/foo/bar");
  LilvNode* loc_rel  = lilv_new_file_uri(world, NULL, "foo");
  LilvNode* host_abs = lilv_new_file_uri(world, "host", "/foo/bar");
  LilvNode* host_rel = lilv_new_file_uri(world, "host", "foo");

  assert(!strcmp(lilv_node_as_uri(loc_abs), "file:///foo/bar"));
  assert(!strncmp(lilv_node_as_uri(loc_rel), "file:///", 8));
  assert(!strcmp(lilv_node_as_uri(host_abs), "file://host/foo/bar"));
  assert(!strncmp(lilv_node_as_uri(host_rel), "file://host/", 12));

  lilv_node_free(host_rel);
  lilv_node_free(host_abs);
  lilv_node_free(loc_rel);
  lilv_node_free(loc_abs);
  lilv_test_env_free(env);
}

static void
test_constructed(void)
{
  LilvTestEnv* const env   = lilv_test_env_new();
  LilvWorld* const   world = env->world;

  assert(!create_bundle(env, "value.lv2", SIMPLE_MANIFEST_TTL, plugin_ttl));

  assert(!lilv_node_is_uri(NULL));
  assert(!lilv_node_is_blank(NULL));
  assert(!lilv_node_is_string(NULL));
  assert(!lilv_node_is_float(NULL));
  assert(!lilv_node_is_int(NULL));
  assert(!lilv_node_is_bool(NULL));

  LilvNode* uval = lilv_new_uri(world, "http://example.org");
  LilvNode* sval = lilv_new_string(world, "Foo");
  LilvNode* ival = lilv_new_int(world, 42);
  LilvNode* fval = lilv_new_float(world, 1.6180f);
  LilvNode* bval = lilv_new_bool(world, true);

  assert(lilv_node_is_uri(uval));
  assert(lilv_node_is_string(sval));
  assert(lilv_node_is_int(ival));
  assert(lilv_node_is_float(fval));
  assert(lilv_node_is_bool(bval));

  assert(!lilv_node_is_literal(NULL));
  assert(!lilv_node_is_literal(uval));
  assert(lilv_node_is_literal(sval));
  assert(lilv_node_is_literal(ival));
  assert(lilv_node_is_literal(fval));
  assert(lilv_node_is_literal(bval));
  assert(!lilv_node_get_path(fval, NULL));

  assert(!strcmp(lilv_node_as_uri(uval), "http://example.org"));
  assert(!strcmp(lilv_node_as_string(sval), "Foo"));
  assert(lilv_node_as_int(ival) == 42);
  assert(fabs(lilv_node_as_float(fval) - 1.6180) < FLT_EPSILON);
  assert(isnan(lilv_node_as_float(sval)));

  char* tok = lilv_node_get_turtle_token(uval);
  assert(!strcmp(tok, "<http://example.org>"));
  lilv_free(tok);
  tok = lilv_node_get_turtle_token(sval);
  assert(!strcmp(tok, "Foo"));
  lilv_free(tok);
  tok = lilv_node_get_turtle_token(ival);
  assert(!strcmp(tok, "42"));
  lilv_free(tok);
  tok = lilv_node_get_turtle_token(fval);
  assert(!strncmp(tok, "1.6180", 6));
  lilv_free(tok);

  LilvNode* uval_e  = lilv_new_uri(world, "http://example.org");
  LilvNode* sval_e  = lilv_new_string(world, "Foo");
  LilvNode* ival_e  = lilv_new_int(world, 42);
  LilvNode* fval_e  = lilv_new_float(world, 1.6180f);
  LilvNode* bval_e  = lilv_new_bool(world, true);
  LilvNode* uval_ne = lilv_new_uri(world, "http://no-example.org");
  LilvNode* sval_ne = lilv_new_string(world, "Bar");
  LilvNode* ival_ne = lilv_new_int(world, 24);
  LilvNode* fval_ne = lilv_new_float(world, 3.14159f);
  LilvNode* bval_ne = lilv_new_bool(world, false);

  assert(lilv_node_equals(uval, uval_e));
  assert(lilv_node_equals(sval, sval_e));
  assert(lilv_node_equals(ival, ival_e));
  assert(lilv_node_equals(fval, fval_e));
  assert(lilv_node_equals(bval, bval_e));

  assert(!lilv_node_equals(uval, uval_ne));
  assert(!lilv_node_equals(sval, sval_ne));
  assert(!lilv_node_equals(ival, ival_ne));
  assert(!lilv_node_equals(fval, fval_ne));
  assert(!lilv_node_equals(bval, bval_ne));

  assert(!lilv_node_equals(uval, sval));
  assert(!lilv_node_equals(sval, ival));
  assert(!lilv_node_equals(ival, fval));
  assert(!lilv_node_equals(ival, bval));

  LilvNode* uval_dup = lilv_node_duplicate(uval);
  assert(lilv_node_equals(uval, uval_dup));

  LilvNode* ifval = lilv_new_float(world, 42.0);
  assert(!lilv_node_equals(ival, ifval));
  lilv_node_free(ifval);

  const LilvNode* nil = NULL;
  assert(!lilv_node_equals(uval, nil));
  assert(!lilv_node_equals(nil, uval));
  assert(lilv_node_equals(nil, nil));

  LilvNode* nil2 = lilv_node_duplicate(nil);
  assert(lilv_node_equals(nil, nil2));

  lilv_node_free(uval);
  lilv_node_free(sval);
  lilv_node_free(ival);
  lilv_node_free(fval);
  lilv_node_free(bval);
  lilv_node_free(uval_e);
  lilv_node_free(sval_e);
  lilv_node_free(ival_e);
  lilv_node_free(fval_e);
  lilv_node_free(bval_e);
  lilv_node_free(uval_ne);
  lilv_node_free(sval_ne);
  lilv_node_free(ival_ne);
  lilv_node_free(fval_ne);
  lilv_node_free(bval_ne);
  lilv_node_free(uval_dup);
  lilv_node_free(nil2);

  lilv_test_env_free(env);
}

static LilvNode*
load_node(LilvWorld* const        world,
          const LilvPlugin* const plug,
          const char* const       predicate_uri)
{
  LilvNode* const  predicate = lilv_new_uri(world, predicate_uri);
  LilvNodes* const values    = lilv_plugin_get_value(plug, predicate);

  assert(lilv_nodes_size(values) == 1);

  LilvNode* const node = lilv_node_duplicate(lilv_nodes_get_first(values));
  lilv_node_free(predicate);
  lilv_nodes_free(values);
  return node;
}

static void
test_loaded(void)
{
  LilvTestEnv* const env   = lilv_test_env_new();
  LilvWorld* const   world = env->world;

  assert(!create_bundle(env, "value.lv2", SIMPLE_MANIFEST_TTL, plugin_ttl));

  lilv_world_load_specifications(env->world);
  lilv_world_load_bundle(env->world, env->test_bundle_uri);

  const LilvPlugins* plugins = lilv_world_get_all_plugins(world);
  const LilvPlugin*  plug = lilv_plugins_get_by_uri(plugins, env->plugin1_uri);

  LilvNode* const a_bool = load_node(world, plug, "http://example.org/a-bool");
  assert(lilv_node_is_bool(a_bool));
  assert(lilv_node_as_bool(a_bool));
  lilv_node_free(a_bool);

  LilvNode* const a_integer =
    load_node(world, plug, "http://example.org/a-integer");
  assert(lilv_node_is_int(a_integer));
  assert(lilv_node_as_int(a_integer) == 234);
  lilv_node_free(a_integer);

  LilvNode* const a_decimal =
    load_node(world, plug, "http://example.org/a-decimal");
  assert(lilv_node_is_float(a_decimal));
  assert(lilv_node_as_float(a_decimal) == 1.5);
  lilv_node_free(a_decimal);

  LilvNode* const a_float =
    load_node(world, plug, "http://example.org/a-float");
  assert(lilv_node_is_float(a_float));
  assert(lilv_node_as_float(a_float) == 56.5);
  lilv_node_free(a_float);

  LilvNode* const a_double =
    load_node(world, plug, "http://example.org/a-double");
  assert(lilv_node_is_float(a_double));
  assert(lilv_node_as_float(a_double) == 780.25);
  lilv_node_free(a_double);

  LilvNode* const a_inf = load_node(world, plug, "http://example.org/a-inf");
  assert(lilv_node_is_float(a_inf));
  assert(isinf(lilv_node_as_float(a_inf)));
  lilv_node_free(a_inf);

  delete_bundle(env);
  lilv_test_env_free(env);
}

int
main(void)
{
  test_file_uris();
  test_constructed();
  test_loaded();
  return 0;
}
