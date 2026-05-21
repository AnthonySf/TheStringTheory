// Copyright 2011-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_BTREE_H
#define ZIX_BTREE_H

#include <zix/allocator.h>
#include <zix/attributes.h>
#include <zix/status.h>

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

ZIX_BEGIN_DECLS

/**
   @defgroup zix_btree BTree
   @ingroup zix_data_structures
   @{
*/

/**
   @defgroup zix_btree_types Types
   @{
*/

/**
   The maximum height of a ZixBTree.

   This is exposed because it determines the size of iterators, which are
   statically sized so they can used on the stack.  The usual degree (or
   "fanout") of a B-Tree is high enough that a relatively short tree can
   contain many elements.  With the default page size of 4 KiB, the default
   height of 6 is enough to store trillions.
*/
#ifndef ZIX_BTREE_MAX_HEIGHT
#  define ZIX_BTREE_MAX_HEIGHT 6U
#endif

/// A B-Tree
typedef struct ZixBTreeImpl ZixBTree;

/// Function for comparing two B-Tree elements
typedef int (*ZixBTreeCompareFunc)(const void* ZIX_UNSPECIFIED a,
                                   const void* ZIX_UNSPECIFIED b,
                                   const void* ZIX_UNSPECIFIED user_data);

/// Function to destroy a B-Tree element
typedef void (*ZixBTreeDestroyFunc)(void* ZIX_UNSPECIFIED       ptr,
                                    const void* ZIX_UNSPECIFIED user_data);

/**
   @}
   @defgroup zix_btree_setup Setup
   @{
*/

/**
   Create a new (empty) B-Tree.

   The given comparator must be a total ordering and is used to internally
   organize the tree and look for values exactly.

   Searching can be done with a custom comparator that supports wildcards, see
   zix_btree_lower_bound() for details.
*/
ZIX_API ZIX_NODISCARD ZixBTree* ZIX_ALLOCATED
zix_btree_new(ZixAllocator* ZIX_NULLABLE      allocator,
              ZixBTreeCompareFunc ZIX_NONNULL cmp,
              const void* ZIX_UNSPECIFIED     cmp_data);

/**
   Free `t` and all the nodes it contains.

   @param t The tree to free.

   @param destroy Function to call once for every value in the tree.  This can
   be used to free values if they are dynamically allocated.

   @param destroy_data Opaque user data pointer to pass to `destroy`.
*/
ZIX_API void
zix_btree_free(ZixBTree* ZIX_NULLABLE           t,
               ZixBTreeDestroyFunc ZIX_NULLABLE destroy,
               const void* ZIX_UNSPECIFIED      destroy_data);

/**
   Clear everything from `t`, leaving it empty.

   @param t The tree to clear.

   @param destroy Function called exactly once for every value in the tree,
   just before that value is removed from the tree.

   @param destroy_data Opaque user data pointer to pass to `destroy`.
*/
ZIX_API void
zix_btree_clear(ZixBTree* ZIX_NONNULL            t,
                ZixBTreeDestroyFunc ZIX_NULLABLE destroy,
                const void* ZIX_UNSPECIFIED      destroy_data);

/// Return the number of elements in `t`
ZIX_PURE_API ZIX_REALTIME size_t
zix_btree_size(const ZixBTree* ZIX_NONNULL t);

/**
   @}
   @defgroup zix_btree_iteration Iteration
   @{
*/

/// An opaque node in a B-Tree
typedef struct ZixBTreeNodeImpl ZixBTreeNode;

/**
   An iterator over a B-Tree.

   Note that modifying the tree invalidates all iterators.

   The contents of this type are considered an implementation detail and should
   not be used directly by clients.  They are nevertheless exposed here so that
   iterators can be allocated on the stack.
*/
typedef struct {
  ZixBTreeNode* ZIX_NULLABLE nodes[ZIX_BTREE_MAX_HEIGHT];   ///< Node stack
  uint16_t                   indexes[ZIX_BTREE_MAX_HEIGHT]; ///< Index stack
  uint16_t                   level;                         ///< Current level
} ZixBTreeIter;

/// A static end iterator for convenience
static const ZixBTreeIter zix_btree_end_iter = {
  {NULL, NULL, NULL, NULL, NULL, NULL},
  {0U, 0U, 0U, 0U, 0U, 0U},
  0U,
};

/// Return the data at the given position in the tree
ZIX_PURE_API ZIX_REALTIME void* ZIX_UNSPECIFIED
zix_btree_get(ZixBTreeIter ti);

/// Return an iterator to the first (smallest) element in `t`
ZIX_PURE_API ZIX_NONBLOCKING ZixBTreeIter
zix_btree_begin(const ZixBTree* ZIX_NONNULL t);

/// Return an iterator to the end of `t` (one past the last element)
ZIX_CONST_API ZIX_REALTIME ZixBTreeIter
zix_btree_end(const ZixBTree* ZIX_NULLABLE t);

/// Return true iff `lhs` is equal to `rhs`
ZIX_CONST_API ZIX_REALTIME bool
zix_btree_iter_equals(ZixBTreeIter lhs, ZixBTreeIter rhs);

/// Return true iff `i` is an iterator at the end of a tree
ZIX_NODISCARD static inline bool
zix_btree_iter_is_end(const ZixBTreeIter i)
{
  return i.level == 0 && !i.nodes[0];
}

/// Increment `i` to point to the next element in the tree
ZIX_API ZIX_NONBLOCKING ZixStatus
zix_btree_iter_increment(ZixBTreeIter* ZIX_NONNULL i);

/// Return an iterator one past `iter`
ZIX_API ZIX_NONBLOCKING ZIX_NODISCARD ZixBTreeIter
zix_btree_iter_next(ZixBTreeIter iter);

/**
   @}
   @defgroup zix_btree_modification Modification
   @{
*/

/**
   Insert the element `e` into `t`.

   @return #ZIX_STATUS_SUCCESS on success, #ZIX_STATUS_EXISTS, or
   #ZIX_STATUS_NO_MEM.
*/
ZIX_API ZixStatus
zix_btree_insert(ZixBTree* ZIX_NONNULL t, void* ZIX_UNSPECIFIED e);

/**
   Remove the element `e` from `t`.

   @param t Tree to remove from.

   @param e Value to remove.

   @param out Set to point to the removed pointer (which may not equal `e`).

   @param next On successful return, set to point at element immediately
   following `e`.

   @return #ZIX_STATUS_SUCCESS on success, or #ZIX_STATUS_NOT_FOUND.
*/
ZIX_API ZixStatus
zix_btree_remove(ZixBTree* ZIX_NONNULL              t,
                 const void* ZIX_UNSPECIFIED        e,
                 void* ZIX_UNSPECIFIED* ZIX_NONNULL out,
                 ZixBTreeIter* ZIX_NONNULL          next);

/**
   @}
   @defgroup zix_btree_searching Searching
   @{
*/

/**
   Set `ti` to an element exactly equal to `e` in `t`.

   If no such item exists, `ti` is set to the end.

   @return #ZIX_STATUS_SUCCESS on success, or #ZIX_STATUS_NOT_FOUND.
*/
ZIX_API ZixStatus
zix_btree_find(const ZixBTree* ZIX_NONNULL t,
               const void* ZIX_UNSPECIFIED e,
               ZixBTreeIter* ZIX_NONNULL   ti);

/**
   Set `ti` to the smallest element in `t` that is not less than `e`.

   The given comparator must be compatible with the tree comparator, that is,
   any two values must have the same ordering according to both.  Within this
   constraint, it may implement fuzzier searching by handling special search
   key values, for example with wildcards.

   If the search key `e` compares equal to many values in the tree, then `ti`
   will be set to the least such element.

   The comparator is always called with an actual value in the tree as the
   first argument, and `key` as the second argument.

   @return #ZIX_STATUS_SUCCESS.
*/
ZIX_API ZixStatus
zix_btree_lower_bound(const ZixBTree* ZIX_NONNULL      t,
                      ZixBTreeCompareFunc ZIX_NULLABLE compare_key,
                      const void* ZIX_UNSPECIFIED      compare_key_data,
                      const void* ZIX_UNSPECIFIED      key,
                      ZixBTreeIter* ZIX_NONNULL        ti);

/**
   @}
   @}
*/

ZIX_END_DECLS

#endif /* ZIX_BTREE_H */
