// Copyright 2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef LILV_NODE_SKIMMER_H
#define LILV_NODE_SKIMMER_H

#include "load_skimmer.h"
#include "node_hash.h"

#include <serd/serd.h>
#include <sord/sord.h>
#include <zix/attributes.h>

#include <stdbool.h>

/// A LoadSkimmer that skims either subjects or objects of a given predicate
typedef struct NodeSkimmerImpl {
  LoadSkimmer                 base;
  const SordNode* ZIX_NONNULL predicate;
  NodeHash* ZIX_ALLOCATED     nodes;
  const SordNode* ZIX_NONNULL topic;
  SordQuadIndex               topic_field;
  SerdStatus                  topical_status;
} NodeSkimmer;

NodeSkimmer* ZIX_ALLOCATED
node_skimmer_new(SordWorld* ZIX_NONNULL      world,
                 const SerdNode* ZIX_NONNULL base,
                 SordModel* ZIX_NONNULL      model,
                 SordQuadIndex               topic_field,
                 const SordNode* ZIX_NONNULL topic,
                 const SordNode* ZIX_NONNULL predicate,
                 bool                        drop_topic);

ZIX_NODISCARD NodeHash* ZIX_ALLOCATED
node_skimmer_free(NodeSkimmer* ZIX_NULLABLE skimmer);

#endif // LILV_NODE_SKIMMER_H
