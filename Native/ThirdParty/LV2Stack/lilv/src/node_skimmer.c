// Copyright 2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "node_skimmer.h"

#include "load_skimmer.h"

#include <serd/serd.h>
#include <sord/sord.h>
#include <zix/allocator.h>

#include <assert.h>
#include <stddef.h>

static bool
node_equals(const SordNode* const lhs, const SordNode* const rhs)
{
  return lhs == rhs; // Nodes are interned
}

static SerdStatus
skim_nodes(NodeSkimmer*          skimmer,
           const SordNode* const subject,
           const SordNode* const predicate,
           const SordNode* const object)
{
  // Get the node from this statement that corresponds to our topic field
  const SordNode* const topic =
    ((skimmer->topic_field == SORD_SUBJECT)     ? subject
     : (skimmer->topic_field == SORD_PREDICATE) ? predicate
                                                : object);

  // Pass through any statements that aren't about our topic
  if (!node_equals(topic, skimmer->topic)) {
    return SERD_SUCCESS;
  }

  if (node_equals(predicate, skimmer->predicate)) {
    // Add matching node (opposite the topic) to result set
    if (!skimmer->nodes) {
      skimmer->nodes = lilv_node_hash_new(NULL);
    }
    if (skimmer->nodes) {
      lilv_node_hash_insert_copy(
        skimmer->nodes,
        (skimmer->topic_field == SORD_SUBJECT) ? object : subject);
    }
  }

  return skimmer->topical_status;
}

NodeSkimmer*
node_skimmer_new(SordWorld* const      world,
                 const SerdNode* const base,
                 SordModel* const      model,
                 const SordQuadIndex   topic_field,
                 const SordNode* const topic,
                 const SordNode* const predicate,
                 const bool            drop_topic)
{
  assert(topic_field != SORD_PREDICATE);
  assert(topic_field != SORD_GRAPH);

  NodeSkimmer* skimmer = (NodeSkimmer*)zix_malloc(NULL, sizeof(NodeSkimmer));

  if (skimmer) {
    SerdEnv* const env = serd_env_new(base);

    load_skimmer_init(
      &skimmer->base, world, env, model, skimmer, (LoadSkimmerFunc)skim_nodes);

    skimmer->predicate      = predicate;
    skimmer->nodes          = NULL;
    skimmer->topic          = topic;
    skimmer->topic_field    = topic_field;
    skimmer->topical_status = drop_topic ? SERD_FAILURE : SERD_SUCCESS;
  }

  return skimmer;
}

NodeHash*
node_skimmer_free(NodeSkimmer* const skimmer)
{
  NodeHash* nodes = NULL;
  if (skimmer) {
    nodes = skimmer->nodes;
    load_skimmer_cleanup(&skimmer->base);
    serd_env_free(skimmer->base.env);
    zix_free(NULL, skimmer);
  }
  return nodes;
}
