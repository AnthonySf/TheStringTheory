// Copyright 2007-2020 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "lilv_test_utils.h"

#include <lilv/lilv.h>

#include <assert.h>
#include <stdint.h>

static const char* const plugin_ttl = "\
:plug a lv2:Plugin ;\n\
	doap:name \"Test plugin\" ;\n\
	doap:homepage <http://example.org/someplug> ;\n\
	lv2:port [\n\
		a lv2:ControlPort ;\n\
		a lv2:InputPort ;\n\
		lv2:index \"notaninteger\" ;\n\
		lv2:symbol \"invalid\" ;\n\
		lv2:name \"Invalid\" ;\n\
	] .";

int
main(void)
{
  LilvTestEnv* const     env   = lilv_test_env_new();
  const LilvWorld* const world = env->world;

  if (create_bundle(
        env, "bad_port_index.lv2", SIMPLE_MANIFEST_TTL, plugin_ttl)) {
    return 1;
  }

  lilv_world_load_specifications(env->world);
  lilv_world_load_bundle(env->world, env->test_bundle_uri);

  const LilvPlugins* plugins = lilv_world_get_all_plugins(world);
  const LilvPlugin*  plug = lilv_plugins_get_by_uri(plugins, env->plugin1_uri);

  uint32_t n_ports = lilv_plugin_get_num_ports(plug);
  assert(n_ports == 0);

  delete_bundle(env);
  lilv_test_env_free(env);

  return 0;
}
