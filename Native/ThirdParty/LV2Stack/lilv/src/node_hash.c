// Copyright 2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#define ZIX_HASH_KEY_TYPE SordNode
#define ZIX_HASH_RECORD_TYPE SordNode
#define ZIX_HASH_SEARCH_DATA_TYPE SordNode

#include "node_hash.h"

#include <sord/sord.h>
#include <zix/allocator.h>
#include <zix/attributes.h>
#include <zix/digest.h>
#include <zix/hash.h>
#include <zix/status.h>

#include <assert.h>
#include <stdbool.h>
#include <stddef.h>

ZIX_PURE_FUNC static const SordNode*
node_ptr_identity(const SordNode* const record)
{
  return record;
}

ZIX_PURE_FUNC static size_t
node_ptr_hash(const SordNode* const node)
{
  return zix_digest_aligned(0U, &node, sizeof(SordNode*));
}

static bool
node_ptr_equal(const SordNode* const lhs, const SordNode* const rhs)
{
  return lhs == rhs;
}

NodeHash*
lilv_node_hash_new(ZixAllocator* const allocator)
{
  return zix_hash_new(
    allocator, node_ptr_identity, node_ptr_hash, node_ptr_equal);
}

void
lilv_node_hash_free(NodeHash* const hash, SordWorld* const world)
{
  if (hash && world) {
    NODE_HASH_FOREACH (i, hash) {
      sord_node_free(world, zix_hash_get(hash, i));
    }
  }

  zix_hash_free(hash);
}

size_t
lilv_node_hash_size(const NodeHash* const hash)
{
  return zix_hash_size(hash);
}

ZixStatus
lilv_node_hash_insert(NodeHash* const hash, SordNode* const node)
{
  return zix_hash_insert(hash, node);
}

ZixStatus
lilv_node_hash_insert_copy(NodeHash* const hash, const SordNode* const node)
{
  return lilv_node_hash_insert(hash, sord_node_copy(node));
}

NodeHashIter
lilv_node_hash_begin(const NodeHash* const hash)
{
  return zix_hash_begin(hash);
}

NodeHashIter
lilv_node_hash_end(const NodeHash* const hash)
{
  return zix_hash_end(hash);
}

const SordNode*
lilv_node_hash_get(const NodeHash* const hash, const NodeHashIter i)
{
  return (const SordNode*)zix_hash_get(hash, i);
}

NodeHashIter
lilv_node_hash_next(const NodeHash* ZIX_NONNULL hash, NodeHashIter i)
{
  return zix_hash_next(hash, i);
}

ZixStatus
lilv_node_hash_remove(NodeHash* const       hash,
                      SordWorld* const      world,
                      const SordNode* const node)
{
  ZixStatus st = ZIX_STATUS_SUCCESS;

  const ZixHashIter i = zix_hash_find(hash, node);
  if (i != zix_hash_end(hash)) {
    SordNode* removed = NULL;
    st                = zix_hash_erase(hash, i, &removed);
    assert(!removed || sord_node_equals(removed, node));
    sord_node_free(world, removed);
  }

  return st;
}

NodeHashIter
lilv_node_hash_find(const NodeHash* ZIX_NONNULL hash,
                    const SordNode* ZIX_NONNULL key)
{
  return zix_hash_find(hash, key);
}
