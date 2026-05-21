// Copyright 2007-2019 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <lilv/lilv.h>

#include <stdbool.h>
#include <stdio.h>
#include <string.h>

static void
list_plugins(const LilvPlugins* list, bool show_names)
{
  LILV_FOREACH (plugins, i, list) {
    const LilvPlugin* p = lilv_plugins_get(list, i);
    if (show_names) {
      LilvNode* n = lilv_plugin_get_name(p);
      printf("%s\n", lilv_node_as_string(n));
      lilv_node_free(n);
    } else {
      printf("%s\n", lilv_node_as_uri(lilv_plugin_get_uri(p)));
    }
  }
}

static void
print_version(void)
{
  printf("lv2ls (lilv) " LILV_VERSION "\n");
}

static int
print_usage(const char* const name, const int status)
{
  FILE* const out = status ? stderr : stdout;
  fprintf(out, "Usage: %s [OPTION]...\n", name);
  fprintf(out,
          "List installed LV2 plugins.\n\n"
          "  -V, --version  Print version information and exit\n"
          "  -h, --help     Print this help and exit\n"
          "  -n, --names    Show names instead of URIs\n");
  return status;
}

int
main(int argc, char** argv)
{
  bool show_names = false;
  for (int a = 1; a < argc; ++a) {
    if (!strcmp(argv[a], "-V") || !strcmp(argv[a], "--version")) {
      print_version();
      return 0;
    }

    if (!strcmp(argv[a], "-h") || !strcmp(argv[a], "--help")) {
      return print_usage(argv[0], 0);
    }

    if (!strcmp(argv[a], "--names") || !strcmp(argv[a], "-n")) {
      show_names = true;
    } else {
      return print_usage(argv[0], 1);
    }
  }

  LilvWorld* world = lilv_world_new();
  lilv_world_set_option(world, LILV_OPTION_OBJECT_INDEX, NULL);
  lilv_world_load_all(world);

  const LilvPlugins* plugins = lilv_world_get_all_plugins(world);

  list_plugins(plugins, show_names);

  lilv_world_free(world);

  return 0;
}
