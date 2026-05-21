// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "lilv_internal.h"
#include "string_util.h"

#include <lilv/lilv.h>
#include <sord/sord.h>
#include <zix/tree.h>

#include <assert.h>
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>

LilvUI*
lilv_ui_new(LilvWorld* const      world,
            const SordNode* const uri,
            const SordNode* const type_uri,
            const SordNode* const binary_uri)
{
  assert(uri);
  assert(type_uri);
  assert(binary_uri);

  LilvUI* ui     = (LilvUI*)malloc(sizeof(LilvUI));
  ui->world      = world;
  ui->uri        = lilv_node_new_from_node(world, uri);
  ui->binary_uri = lilv_node_new_from_node(world, binary_uri);

  // FIXME: kludge
  char* bundle     = lilv_strdup(lilv_node_as_string(ui->binary_uri));
  char* last_slash = strrchr(bundle, '/') + 1;
  *last_slash      = '\0';
  ui->bundle_uri   = lilv_new_uri(world, bundle);
  free(bundle);

  ui->classes = lilv_nodes_new();
  zix_tree_insert(
    (ZixTree*)ui->classes, lilv_node_new_from_node(world, type_uri), NULL);

  return ui;
}

void
lilv_ui_free(LilvUI* ui)
{
  lilv_node_free(ui->uri);
  lilv_node_free(ui->bundle_uri);
  lilv_node_free(ui->binary_uri);
  lilv_nodes_free(ui->classes);
  free(ui);
}

const LilvNode*
lilv_ui_get_uri(const LilvUI* ui)
{
  return ui->uri;
}

unsigned
lilv_ui_is_supported(const LilvUI*       ui,
                     LilvUISupportedFunc supported_func,
                     const LilvNode*     container_type,
                     const LilvNode**    ui_type)
{
  const LilvNodes* classes = lilv_ui_get_classes(ui);
  LILV_FOREACH (nodes, c, classes) {
    const LilvNode* type = lilv_nodes_get(classes, c);
    const unsigned  q =
      supported_func(lilv_node_as_uri(container_type), lilv_node_as_uri(type));
    if (q) {
      if (ui_type) {
        *ui_type = type;
      }
      return q;
    }
  }

  return 0;
}

const LilvNodes*
lilv_ui_get_classes(const LilvUI* ui)
{
  return ui->classes;
}

bool
lilv_ui_is_a(const LilvUI* ui, const LilvNode* class_uri)
{
  return lilv_nodes_contains(ui->classes, class_uri);
}

const LilvNode*
lilv_ui_get_bundle_uri(const LilvUI* ui)
{
  return ui->bundle_uri;
}

const LilvNode*
lilv_ui_get_binary_uri(const LilvUI* ui)
{
  return ui->binary_uri;
}
