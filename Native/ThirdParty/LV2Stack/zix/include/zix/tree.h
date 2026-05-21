// Copyright 2011-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_TREE_H
#define ZIX_TREE_H

#include <zix/allocator.h>
#include <zix/attributes.h>
#include <zix/status.h>

#include <stdbool.h>
#include <stddef.h>

ZIX_BEGIN_DECLS

/**
   @defgroup zix_tree Tree
   @ingroup zix_data_structures
   @{
*/

/**
   @defgroup zix_tree_types Types
   @{
*/

/// A balanced binary search tree
typedef struct ZixTreeImpl ZixTree;

/// Function for comparing two Tree elements
typedef int (*ZixTreeCompareFunc)(const void* ZIX_UNSPECIFIED a,
                                  const void* ZIX_UNSPECIFIED b,
                                  const void* ZIX_UNSPECIFIED user_data);

/// Function to destroy a Tree element
typedef void (*ZixTreeDestroyFunc)(void* ZIX_UNSPECIFIED       ptr,
                                   const void* ZIX_UNSPECIFIED user_data);

/**
   @}
   @defgroup zix_tree_setup Setup
   @{
*/

/// Create a new (empty) tree
ZIX_API ZIX_NODISCARD ZixTree* ZIX_ALLOCATED
zix_tree_new(ZixAllocator* ZIX_NULLABLE      allocator,
             bool                            allow_duplicates,
             ZixTreeCompareFunc ZIX_NONNULL  cmp,
             void* ZIX_UNSPECIFIED           cmp_data,
             ZixTreeDestroyFunc ZIX_NULLABLE destroy,
             const void* ZIX_UNSPECIFIED     destroy_user_data);

/// Free `t`
ZIX_API void
zix_tree_free(ZixTree* ZIX_NULLABLE t);

/// Return the number of elements in `t`
ZIX_PURE_API ZIX_REALTIME size_t
zix_tree_size(const ZixTree* ZIX_NONNULL t);

/**
   @}
   @defgroup zix_tree_iteration Iteration
   @{
*/

/// An iterator over a @ref ZixTree
typedef struct ZixTreeNodeImpl ZixTreeIter;

/// Return the data associated with the given tree item
ZIX_PURE_API ZIX_REALTIME void* ZIX_UNSPECIFIED
zix_tree_get(const ZixTreeIter* ZIX_NULLABLE ti);

/// Return an iterator to the first (smallest) element in `t`
ZIX_PURE_API ZIX_NONBLOCKING ZixTreeIter* ZIX_NULLABLE
zix_tree_begin(ZixTree* ZIX_NONNULL t);

/// Return an iterator the the element one past the last element in `t`
ZIX_CONST_API ZIX_REALTIME ZixTreeIter* ZIX_NULLABLE
zix_tree_end(ZixTree* ZIX_NONNULL t);

/// Return true iff `i` is an iterator to the end of its tree
ZIX_CONST_API ZIX_REALTIME bool
zix_tree_iter_is_end(const ZixTreeIter* ZIX_NULLABLE i);

/// Return an iterator to the last (largest) element in `t`
ZIX_PURE_API ZIX_NONBLOCKING ZixTreeIter* ZIX_NULLABLE
zix_tree_rbegin(ZixTree* ZIX_NONNULL t);

/// Return an iterator the the element one before the first element in `t`
ZIX_CONST_API ZIX_REALTIME ZixTreeIter* ZIX_NULLABLE
zix_tree_rend(ZixTree* ZIX_NONNULL t);

/// Return true iff `i` is an iterator to the reverse end of its tree
ZIX_CONST_API ZIX_REALTIME bool
zix_tree_iter_is_rend(const ZixTreeIter* ZIX_NULLABLE i);

/// Return an iterator that points to the element one past `i`
ZIX_PURE_API ZIX_NONBLOCKING ZixTreeIter* ZIX_NULLABLE
zix_tree_iter_next(ZixTreeIter* ZIX_NULLABLE i);

/// Return an iterator that points to the element one before `i`
ZIX_PURE_API ZIX_NONBLOCKING ZixTreeIter* ZIX_NULLABLE
zix_tree_iter_prev(ZixTreeIter* ZIX_NULLABLE i);

/**
   @}
   @defgroup zix_tree_modification Modification
   @{
*/

/// Insert the element `e` into `t` and point `ti` at the new element
ZIX_API ZixStatus
zix_tree_insert(ZixTree* ZIX_NONNULL                    t,
                void* ZIX_UNSPECIFIED                   e,
                ZixTreeIter* ZIX_NULLABLE* ZIX_NULLABLE ti);

/// Remove the item pointed at by `ti` from `t`
ZIX_API ZixStatus
zix_tree_remove(ZixTree* ZIX_NONNULL t, ZixTreeIter* ZIX_NONNULL ti);

/**
   @}
   @defgroup zix_tree_searching Searching
   @{
*/

/**
   Set `ti` to an element equal to `e` in `t`.

   If no such item exists, `ti` is set to NULL.
*/
ZIX_API ZixStatus
zix_tree_find(const ZixTree* ZIX_NONNULL             t,
              const void* ZIX_UNSPECIFIED            e,
              ZixTreeIter* ZIX_NULLABLE* ZIX_NONNULL ti);

/**
   @}
   @}
*/

ZIX_END_DECLS

#endif /* ZIX_TREE_H */
