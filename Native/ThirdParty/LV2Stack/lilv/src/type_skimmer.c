// Copyright 2025-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "type_skimmer.h"

#include "node_hash.h"

#include <serd/serd.h>
#include <sord/sord.h>
#include <zix/allocator.h>

#include <stdbool.h>
#include <stddef.h>

static bool
node_equals(const SordNode* const lhs, const SordNode* const rhs)
{
  return lhs == rhs; // Nodes are interned
}

static void
add_node(NodeHash** const field, const SordNode* const node)
{
  if (field) {
    if (!*field) {
      *field = lilv_node_hash_new(NULL);
    }
    if (*field) {
      lilv_node_hash_insert_copy(*field, node);
    }
  }
}

static void
add_statement(SordModel* const model, const SordQuad tup)
{
  sord_add(model, tup);
}

static SerdStatus
skim_type(TypeSkimmer* const    skimmer,
          const SordNode* const subject,
          const SordNode* const predicate,
          const SordNode* const object)
{
  if (node_equals(predicate, skimmer->uris->rdf_type)) {
    if (node_equals(object, skimmer->uris->lv2_Plugin)) {
      add_node(skimmer->plugins, subject);
    } else if (node_equals(object, skimmer->uris->pset_Preset)) {
      add_node(skimmer->presets, subject);
    } else if (node_equals(object, skimmer->uris->lv2_Specification) ||
               node_equals(object, skimmer->uris->owl_Ontology)) {
      add_node(skimmer->specs, subject);
    }
  } else if (skimmer->replaced &&
             node_equals(predicate, skimmer->uris->dc_replaces)) {
    add_node(skimmer->replaced, object);
  } else if (skimmer->applications &&
             node_equals(predicate, skimmer->uris->lv2_appliesTo)) {
    const SordQuad tup = {subject, predicate, object, NULL};
    add_statement(skimmer->applications, tup);
  } else if (skimmer->subclasses &&
             node_equals(predicate, skimmer->uris->rdfs_subClassOf)) {
    const SordQuad tup = {subject, predicate, object, NULL};
    add_statement(skimmer->subclasses, tup);
  }

  return SERD_SUCCESS;
}

TypeSkimmer*
type_skimmer_new(SordWorld* const      world,
                 const LilvURIs* const uris,
                 const SerdNode* const base,
                 SordModel* const      model,
                 NodeHash** const      plugins,
                 NodeHash** const      presets,
                 NodeHash** const      specs,
                 NodeHash** const      replaced,
                 SordModel* const      applications,
                 SordModel* const      subclasses)
{
  TypeSkimmer* skimmer = (TypeSkimmer*)zix_malloc(NULL, sizeof(TypeSkimmer));

  if (skimmer) {
    SerdEnv* const env = serd_env_new(base);

    load_skimmer_init(
      &skimmer->base, world, env, model, skimmer, (LoadSkimmerFunc)skim_type);

    skimmer->uris         = uris;
    skimmer->plugins      = plugins;
    skimmer->presets      = presets;
    skimmer->specs        = specs;
    skimmer->replaced     = replaced;
    skimmer->applications = applications;
    skimmer->subclasses   = subclasses;
  }

  return skimmer;
}

void
type_skimmer_free(TypeSkimmer* const skimmer)
{
  if (skimmer) {
    load_skimmer_cleanup(&skimmer->base);
    serd_env_free(skimmer->base.env);
    zix_free(NULL, skimmer);
  }
}
