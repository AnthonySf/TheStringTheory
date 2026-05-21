// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef LILV_QUERY_H
#define LILV_QUERY_H

#include "node_hash.h"

#include <lilv/lilv.h>
#include <sord/sord.h>

#define FOREACH_MATCH(iter) for (; !sord_iter_end(iter); sord_iter_next(iter))

LilvNode*
lilv_node_from_object(LilvWorld* world, const SordNode* s, const SordNode* p);

LilvNodes*
lilv_nodes_from_matches(LilvWorld*      world,
                        const SordNode* s,
                        const SordNode* p,
                        const SordNode* o,
                        const SordNode* g);

NodeHash*
lilv_hash_from_matches(SordModel*      model,
                       const SordNode* s,
                       const SordNode* p,
                       const SordNode* o,
                       const SordNode* g);

#endif /* LILV_QUERY_H */
