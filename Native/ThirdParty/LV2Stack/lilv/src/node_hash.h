// Copyright 2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef LILV_NODE_HASH_H
#define LILV_NODE_HASH_H

#include <zix/allocator.h>
#include <zix/attributes.h>
#include <zix/status.h>

#include <stddef.h>

typedef struct SordWorldImpl SordWorld;
typedef struct SordNodeImpl  SordNode;
typedef struct ZixHashImpl   NodeHash;
typedef size_t               NodeHashIter;

#define NODE_HASH_FOREACH(lh_iter, lh_hash)                                 \
  /* NOLINTNEXTLINE(bugprone-macro-parentheses) */                          \
  for (NodeHashIter lh_iter = lh_hash ? lilv_node_hash_begin(lh_hash) : 0U; \
       lh_hash && ((lh_iter) != lilv_node_hash_end(lh_hash));               \
       (lh_iter) = lh_hash ? lilv_node_hash_next(lh_hash, lh_iter) : 0U)

/// Return a new hash of interned node pointers compared by pointer value
NodeHash* ZIX_ALLOCATED
lilv_node_hash_new(ZixAllocator* ZIX_NULLABLE allocator);

/// Free a node pointer hash and dereference every node in it
void
lilv_node_hash_free(NodeHash* ZIX_NULLABLE hash, SordWorld* ZIX_NULLABLE world);

/// Return the number of node pointers in the hash
size_t
lilv_node_hash_size(const NodeHash* ZIX_NONNULL hash);

/// Insert a node pointer into the node hash (taking ownership)
ZixStatus
lilv_node_hash_insert(NodeHash* ZIX_NONNULL hash, SordNode* ZIX_NONNULL node);

/// Copy then retain a node pointer
ZixStatus
lilv_node_hash_insert_copy(NodeHash* ZIX_NONNULL       hash,
                           const SordNode* ZIX_NONNULL node);

/// Return an iterator to the first record in a hash, or the end if it is empty
ZIX_PURE_FUNC NodeHashIter
lilv_node_hash_begin(const NodeHash* ZIX_NONNULL hash);

/// Return an iterator one past the last possible node pointer in the hash
ZIX_PURE_FUNC NodeHashIter
lilv_node_hash_end(const NodeHash* ZIX_NONNULL hash);

/// Return the node pointer at the given position or null
ZIX_PURE_FUNC const SordNode* ZIX_UNSPECIFIED
lilv_node_hash_get(const NodeHash* ZIX_NONNULL hash, NodeHashIter i);

/// Return an iterator that has been advanced to the next node pointer
ZIX_PURE_FUNC NodeHashIter
lilv_node_hash_next(const NodeHash* ZIX_NONNULL hash, NodeHashIter i);

/// Erase and dereference a node pointer
ZixStatus
lilv_node_hash_remove(NodeHash* ZIX_NONNULL       hash,
                      SordWorld* ZIX_NONNULL      world,
                      const SordNode* ZIX_NONNULL node);

/// Find a specific node pointer
NodeHashIter
lilv_node_hash_find(const NodeHash* ZIX_NONNULL hash,
                    const SordNode* ZIX_NONNULL key);

#endif // LILV_NODE_HASH_H
