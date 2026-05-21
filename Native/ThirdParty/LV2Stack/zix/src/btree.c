// Copyright 2011-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/btree.h>

#include <zix/allocator.h>
#include <zix/attributes.h>
#include <zix/status.h>

#include <assert.h>
#include <stdint.h>
#include <string.h>

// #define ZIX_BTREE_SORTED_CHECK 1

// Define ZixShort as an integer type half the size of a pointer
#if UINTPTR_MAX > UINT32_MAX // 64-bit
typedef uint32_t ZixShort;
#else // 32-bit
typedef uint16_t ZixShort;
#endif

#ifndef ZIX_BTREE_PAGE_SIZE
#  define ZIX_BTREE_PAGE_SIZE 4096U
#endif

#define ZIX_BTREE_NODE_SPACE (ZIX_BTREE_PAGE_SIZE - (2U * sizeof(ZixShort)))
#define ZIX_BTREE_LEAF_VALS ((ZIX_BTREE_NODE_SPACE / sizeof(void*)) - 1U)
#define ZIX_BTREE_INODE_VALS (ZIX_BTREE_LEAF_VALS / 2U)

struct ZixBTreeImpl {
  ZixAllocator*       allocator;
  ZixBTreeNode*       root;
  ZixBTreeCompareFunc cmp;
  const void*         cmp_data;
  size_t              size;
};

struct ZixBTreeNodeImpl {
  ZixShort is_leaf;
  ZixShort n_vals;

  union {
    struct {
      void* vals[ZIX_BTREE_LEAF_VALS];
    } leaf;

    struct {
      void*         vals[ZIX_BTREE_INODE_VALS];
      ZixBTreeNode* children[ZIX_BTREE_INODE_VALS + 1U];
    } inode;
  } data;
};

#if ((defined(__STDC_VERSION__) && __STDC_VERSION__ >= 201112L) || \
     (defined(__cplusplus) && __cplusplus >= 201103L))
static_assert(sizeof(ZixBTree) <= ZIX_BTREE_PAGE_SIZE, "");
static_assert(sizeof(ZixBTreeNode) <= ZIX_BTREE_PAGE_SIZE, "");
static_assert(sizeof(ZixBTreeNode) >=
                ZIX_BTREE_PAGE_SIZE - 2U * sizeof(ZixBTreeNode*),
              "");
#endif

static ZixBTreeNode*
zix_btree_node_new(ZixAllocator* const allocator, const bool leaf)
{
#if !((defined(__STDC_VERSION__) && __STDC_VERSION__ >= 201112L) || \
      (defined(__cplusplus) && __cplusplus >= 201103L))
  assert(sizeof(ZixBTreeNode) <= ZIX_BTREE_PAGE_SIZE);
  assert(sizeof(ZixBTreeNode) >=
         ZIX_BTREE_PAGE_SIZE - (2U * sizeof(ZixBTreeNode*)));
#endif

  ZixBTreeNode* const node = (ZixBTreeNode*)zix_aligned_alloc(
    allocator, ZIX_BTREE_PAGE_SIZE, ZIX_BTREE_PAGE_SIZE);

  if (node) {
    node->is_leaf = leaf;
    node->n_vals  = 0U;
  }

  return node;
}

ZIX_PURE_FUNC static ZixBTreeNode*
zix_btree_child(const ZixBTreeNode* const node, const unsigned i)
{
  assert(!node->is_leaf);
  assert(i <= ZIX_BTREE_INODE_VALS);
  return node->data.inode.children[i];
}

ZixBTree*
zix_btree_new(ZixAllocator* const       allocator,
              const ZixBTreeCompareFunc cmp,
              const void* const         cmp_data)
{
#if !((defined(__STDC_VERSION__) && __STDC_VERSION__ >= 201112L) || \
      (defined(__cplusplus) && __cplusplus >= 201103L))
  assert(sizeof(ZixBTree) <= ZIX_BTREE_PAGE_SIZE);
#endif

  assert(cmp);

  ZixBTree* const t = (ZixBTree*)zix_malloc(allocator, sizeof(ZixBTree));
  if (t) {
    t->allocator = allocator;
    t->root      = NULL;
    t->cmp       = cmp;
    t->cmp_data  = cmp_data;
    t->size      = 0U;
  }

  return t;
}

static void
zix_btree_free_children(ZixBTree* const           t,
                        ZixBTreeNode* const       n,
                        const ZixBTreeDestroyFunc destroy,
                        const void* const         destroy_user_data)
{
  if (!n->is_leaf) {
    for (ZixShort i = 0U; i < n->n_vals + 1U; ++i) {
      zix_btree_free_children(
        t, zix_btree_child(n, i), destroy, destroy_user_data);
      zix_aligned_free(t->allocator, zix_btree_child(n, i));
    }
  }

  if (destroy) {
    if (n->is_leaf) {
      for (ZixShort i = 0U; i < n->n_vals; ++i) {
        destroy(n->data.leaf.vals[i], destroy_user_data);
      }
    } else {
      for (ZixShort i = 0U; i < n->n_vals; ++i) {
        destroy(n->data.inode.vals[i], destroy_user_data);
      }
    }
  }
}

void
zix_btree_free(ZixBTree* const           t,
               const ZixBTreeDestroyFunc destroy,
               const void* const         destroy_user_data)
{
  if (t) {
    zix_btree_clear(t, destroy, destroy_user_data);
    zix_aligned_free(t->allocator, t->root);
    zix_free(t->allocator, t);
  }
}

void
zix_btree_clear(ZixBTree* const           t,
                const ZixBTreeDestroyFunc destroy,
                const void* const         destroy_user_data)
{
  if (t->root) {
    zix_btree_free_children(t, t->root, destroy, destroy_user_data);
    zix_aligned_free(t->allocator, t->root);
    t->root = NULL;
  }

  t->size = 0U;
}

ZIX_REALTIME size_t
zix_btree_size(const ZixBTree* const t)
{
  assert(t);
  return t->size;
}

static ZixShort
zix_btree_max_vals(const ZixBTreeNode* const node)
{
  return node->is_leaf ? ZIX_BTREE_LEAF_VALS : ZIX_BTREE_INODE_VALS;
}

static ZixShort
zix_btree_min_vals(const ZixBTreeNode* const node)
{
  return (ZixShort)(((zix_btree_max_vals(node) + 1U) / 2U) - 1U);
}

/// Shift pointers in `array` of length `n` right starting at `i`
static void
zix_btree_ainsert(void** const   array,
                  const unsigned n,
                  const unsigned i,
                  void* const    e)
{
  memmove(array + i + 1U, array + i, ((size_t)n - i) * sizeof(e));
  array[i] = e;
}

/// Erase element `i` in `array` of length `n` and return erased element
static void*
zix_btree_aerase(void** const array, const unsigned n, const unsigned i)
{
  void* const ret = array[i];
  memmove(array + i, array + i + 1U, ((size_t)n - i) * sizeof(ret));
  return ret;
}

/// Split lhs, the i'th child of `n`, into two nodes
static ZixBTreeNode*
zix_btree_split_child(ZixAllocator* const allocator,
                      ZixBTreeNode* const n,
                      const unsigned      i,
                      ZixBTreeNode* const lhs)
{
  assert(lhs->n_vals == zix_btree_max_vals(lhs));
  assert(n->n_vals < ZIX_BTREE_INODE_VALS);
  assert(i < n->n_vals + 1U);
  assert(zix_btree_child(n, i) == lhs);

  const unsigned max_n_vals = zix_btree_max_vals(lhs);
  ZixBTreeNode*  rhs        = zix_btree_node_new(allocator, lhs->is_leaf);
  if (!rhs) {
    return NULL;
  }

  // LHS and RHS get roughly half, less the middle value which moves up
  lhs->n_vals /= 2U;
  rhs->n_vals = (ZixShort)(max_n_vals - lhs->n_vals - 1U);

  if (lhs->is_leaf) {
    // Copy large half from LHS to new RHS node
    memcpy(rhs->data.leaf.vals,
           lhs->data.leaf.vals + lhs->n_vals + 1U,
           rhs->n_vals * sizeof(void*));

    // Move middle value up to parent
    zix_btree_ainsert(
      n->data.inode.vals, n->n_vals, i, lhs->data.leaf.vals[lhs->n_vals]);
  } else {
    // Copy large half from LHS to new RHS node
    memcpy(rhs->data.inode.vals,
           lhs->data.inode.vals + lhs->n_vals + 1U,
           rhs->n_vals * sizeof(void*));
    memcpy(rhs->data.inode.children,
           lhs->data.inode.children + lhs->n_vals + 1U,
           ((size_t)rhs->n_vals + 1U) * sizeof(ZixBTreeNode*));

    // Move middle value up to parent
    zix_btree_ainsert(
      n->data.inode.vals, n->n_vals, i, lhs->data.inode.vals[lhs->n_vals]);
  }

  // Insert new RHS node in parent at position i
  zix_btree_ainsert((void**)n->data.inode.children, ++n->n_vals, i + 1U, rhs);

  return rhs;
}

#ifdef ZIX_BTREE_SORTED_CHECK
/// Check that `n` is sorted with respect to search key `e`
static bool
zix_btree_node_is_sorted_with_respect_to(const ZixCompareFunc compare,
                                         const void* const    compare_user_data,
                                         void* const* const   values,
                                         const unsigned       n_values,
                                         const void* const    key)
{
  if (n_values <= 1U) {
    return true;
  }

  int cmp = compare(values[0U], key, compare_user_data);
  for (unsigned i = 1U; i < n_values; ++i) {
    const int next_cmp = compare(values[i], key, compare_user_data);
    if ((cmp >= 0 && next_cmp < 0) || (cmp > 0 && next_cmp <= 0)) {
      return false;
    }

    cmp = next_cmp;
  }

  return true;
}
#endif

static unsigned
zix_btree_find_value(const ZixBTreeCompareFunc compare,
                     const void* const         compare_user_data,
                     void* const* const        values,
                     const unsigned            n_values,
                     const void* const         key,
                     bool* const               equal)
{
  unsigned first = 0U;
  unsigned count = n_values;

  while (count > 0U) {
    const unsigned half  = count >> 1U;
    const unsigned i     = first + half;
    void* const    value = values[i];
    const int      cmp   = compare(value, key, compare_user_data);

    if (!cmp) {
      *equal = true;
      return i;
    }

    if (cmp < 0) {
      first += half + 1U;
      count -= half + 1U;
    } else {
      count = half;
    }
  }

  assert(first == n_values || compare(values[first], key, compare_user_data));
  *equal = false;
  return first;
}

static unsigned
zix_btree_find_pattern(const ZixBTreeCompareFunc compare_key,
                       const void* const         compare_key_user_data,
                       void* const* const        values,
                       const unsigned            n_values,
                       const void* const         key,
                       bool* const               equal)
{
#ifdef ZIX_BTREE_SORTED_CHECK
  assert(zix_btree_node_is_sorted_with_respect_to(
    compare_key, compare_key_user_data, values, n_values, key));
#endif

  unsigned first = 0U;
  unsigned count = n_values;

  while (count > 0U) {
    const unsigned half  = count >> 1U;
    const unsigned i     = first + half;
    void* const    value = values[i];
    const int      cmp   = compare_key(value, key, compare_key_user_data);

    if (cmp == 0) {
      // Found a match, but keep searching for the leftmost one
      *equal = true;
      count  = half;

    } else if (cmp < 0) {
      // Search right half
      first += half + 1U;
      count -= half + 1U;

    } else {
      // Search left half
      count = half;
    }
  }

  assert(!*equal ||
         (compare_key(values[first], key, compare_key_user_data) == 0 &&
          (first == 0U ||
           (compare_key(values[first - 1U], key, compare_key_user_data) < 0))));

  return first;
}

/// Convenience wrapper to find a value in an internal node
static unsigned
zix_btree_inode_find(const ZixBTree* const     t,
                     const ZixBTreeNode* const n,
                     const void* const         e,
                     bool* const               equal)
{
  assert(!n->is_leaf);

  return zix_btree_find_value(
    t->cmp, t->cmp_data, n->data.inode.vals, n->n_vals, e, equal);
}

/// Convenience wrapper to find a value in a leaf node
static unsigned
zix_btree_leaf_find(const ZixBTree* const     t,
                    const ZixBTreeNode* const n,
                    const void* const         e,
                    bool* const               equal)
{
  assert(n->is_leaf);

  return zix_btree_find_value(
    t->cmp, t->cmp_data, n->data.leaf.vals, n->n_vals, e, equal);
}

ZIX_PURE_FUNC static inline bool
zix_btree_can_remove_from(const ZixBTreeNode* const n)
{
  assert(n->n_vals >= zix_btree_min_vals(n));
  return n->n_vals > zix_btree_min_vals(n);
}

ZIX_PURE_FUNC static inline bool
zix_btree_is_full(const ZixBTreeNode* const n)
{
  assert(n->n_vals <= zix_btree_max_vals(n));
  return n->n_vals == zix_btree_max_vals(n);
}

static ZixStatus
zix_btree_grow_up(ZixBTree* const t)
{
  ZixBTreeNode* const new_root = zix_btree_node_new(t->allocator, false);
  if (!new_root) {
    return ZIX_STATUS_NO_MEM;
  }

  // Set old root as the only child of the new root
  new_root->data.inode.children[0U] = t->root;

  // Split the old root to get two balanced siblings
  zix_btree_split_child(t->allocator, new_root, 0U, t->root);
  t->root = new_root;

  return ZIX_STATUS_SUCCESS;
}

ZixStatus
zix_btree_insert(ZixBTree* const t, void* const e)
{
  assert(t);

  ZixStatus st = ZIX_STATUS_SUCCESS;

  if (!t->root) {
    // Empty tree, create a new leaf root
    if (!(t->root = zix_btree_node_new(t->allocator, true))) {
      return ZIX_STATUS_NO_MEM;
    }
  } else if (zix_btree_is_full(t->root)) {
    // Grow up if necessary to ensure the root is not full
    if ((st = zix_btree_grow_up(t))) {
      return st;
    }
  }

  // Walk down from the root until we reach a suitable leaf
  ZixBTreeNode* node = t->root;
  while (!node->is_leaf) {
    // Search for the value in this node
    bool           equal = false;
    const unsigned i     = zix_btree_inode_find(t, node, e, &equal);
    if (equal) {
      return ZIX_STATUS_EXISTS;
    }

    // Value not in this node, but may be in the ith child
    ZixBTreeNode* child = node->data.inode.children[i];
    if (zix_btree_is_full(child)) {
      // The child is full, split it before continuing
      ZixBTreeNode* const rhs =
        zix_btree_split_child(t->allocator, node, i, child);

      if (!rhs) {
        return ZIX_STATUS_NO_MEM;
      }

      // Compare with new split value to determine which side to use
      const int cmp = t->cmp(node->data.inode.vals[i], e, t->cmp_data);
      if (cmp < 0) {
        child = rhs; // Split value is less than the new value, move right
      } else if (cmp == 0) {
        return ZIX_STATUS_EXISTS; // Split value is exactly the value to insert
      }
    }

    // Descend to child node and continue
    node = child;
  }

  // Search for the value in the leaf
  bool           equal = false;
  const unsigned i     = zix_btree_leaf_find(t, node, e, &equal);
  if (equal) {
    return ZIX_STATUS_EXISTS;
  }

  // The value is not in the tree, insert into the leaf
  zix_btree_ainsert(node->data.leaf.vals, node->n_vals++, i, e);
  ++t->size;
  return ZIX_STATUS_SUCCESS;
}

static void
zix_btree_iter_set_frame(ZixBTreeIter* const ti,
                         ZixBTreeNode* const n,
                         const unsigned      i)
{
  assert(i <= UINT16_MAX);
  ti->nodes[ti->level]   = n;
  ti->indexes[ti->level] = (uint16_t)i;
}

static void
zix_btree_iter_push(ZixBTreeIter* const ti,
                    ZixBTreeNode* const n,
                    const ZixShort      i)
{
  assert(ti->level < ZIX_BTREE_MAX_HEIGHT - 1U);
  ++ti->level;
  ti->nodes[ti->level]   = n;
  ti->indexes[ti->level] = (uint16_t)i;
}

static void
zix_btree_iter_pop(ZixBTreeIter* const ti)
{
  assert(ti->level > 0U);
  ti->nodes[ti->level]   = NULL;
  ti->indexes[ti->level] = 0U;
  --ti->level;
}

/// Enlarge left child by stealing a value from its right sibling
static ZixBTreeNode*
zix_btree_rotate_left(ZixBTreeNode* const parent, const unsigned i)
{
  ZixBTreeNode* const lhs = zix_btree_child(parent, i);
  ZixBTreeNode* const rhs = zix_btree_child(parent, i + 1U);

  assert(lhs->is_leaf == rhs->is_leaf);

  if (lhs->is_leaf) {
    // Move parent value to end of LHS
    lhs->data.leaf.vals[lhs->n_vals++] = parent->data.inode.vals[i];

    // Move first value in RHS to parent
    parent->data.inode.vals[i] =
      zix_btree_aerase(rhs->data.leaf.vals, rhs->n_vals, 0U);
  } else {
    // Move parent value to end of LHS
    lhs->data.inode.vals[lhs->n_vals++] = parent->data.inode.vals[i];

    // Move first value in RHS to parent
    parent->data.inode.vals[i] =
      zix_btree_aerase(rhs->data.inode.vals, rhs->n_vals, 0U);

    // Move first child pointer from RHS to end of LHS
    lhs->data.inode.children[lhs->n_vals] = (ZixBTreeNode*)zix_btree_aerase(
      (void**)rhs->data.inode.children, rhs->n_vals, 0U);
  }

  --rhs->n_vals;

  return lhs;
}

/// Enlarge a child by stealing a value from its left sibling
static ZixBTreeNode*
zix_btree_rotate_right(ZixBTreeNode* const parent, const unsigned i)
{
  ZixBTreeNode* const lhs = zix_btree_child(parent, i - 1U);
  ZixBTreeNode* const rhs = zix_btree_child(parent, i);

  assert(lhs->is_leaf == rhs->is_leaf);

  if (lhs->is_leaf) {
    // Prepend parent value to RHS
    zix_btree_ainsert(
      rhs->data.leaf.vals, rhs->n_vals++, 0U, parent->data.inode.vals[i - 1U]);

    // Move last value from LHS to parent
    parent->data.inode.vals[i - 1U] = lhs->data.leaf.vals[--lhs->n_vals];
  } else {
    // Prepend parent value to RHS
    zix_btree_ainsert(
      rhs->data.inode.vals, rhs->n_vals++, 0U, parent->data.inode.vals[i - 1U]);

    // Move last child pointer from LHS and prepend to RHS
    zix_btree_ainsert((void**)rhs->data.inode.children,
                      rhs->n_vals,
                      0U,
                      lhs->data.inode.children[lhs->n_vals]);

    // Move last value from LHS to parent
    parent->data.inode.vals[i - 1U] = lhs->data.inode.vals[--lhs->n_vals];
  }

  return rhs;
}

/// Move n[i] down, merge the left and right child, return the merged node
static ZixBTreeNode*
zix_btree_merge(ZixBTree* const t, ZixBTreeNode* const n, const unsigned i)
{
  ZixBTreeNode* const lhs = zix_btree_child(n, i);
  ZixBTreeNode* const rhs = zix_btree_child(n, i + 1U);

  assert(lhs->is_leaf == rhs->is_leaf);
  assert(lhs->n_vals + rhs->n_vals < zix_btree_max_vals(lhs));

  // Move parent value to end of LHS
  if (lhs->is_leaf) {
    lhs->data.leaf.vals[lhs->n_vals++] =
      zix_btree_aerase(n->data.inode.vals, n->n_vals, i);
  } else {
    lhs->data.inode.vals[lhs->n_vals++] =
      zix_btree_aerase(n->data.inode.vals, n->n_vals, i);
  }

  // Erase corresponding child pointer (to RHS) in parent
  zix_btree_aerase((void**)n->data.inode.children, n->n_vals, i + 1U);

  // Add everything from RHS to end of LHS
  if (lhs->is_leaf) {
    memcpy(lhs->data.leaf.vals + lhs->n_vals,
           rhs->data.leaf.vals,
           rhs->n_vals * sizeof(void*));
  } else {
    memcpy(lhs->data.inode.vals + lhs->n_vals,
           rhs->data.inode.vals,
           rhs->n_vals * sizeof(void*));
    memcpy(lhs->data.inode.children + lhs->n_vals,
           rhs->data.inode.children,
           ((size_t)rhs->n_vals + 1U) * sizeof(void*));
  }

  lhs->n_vals += rhs->n_vals;
  if (--n->n_vals == 0U) {
    // Root is now empty, replace it with its only child
    assert(n == t->root);
    t->root = lhs;
    zix_aligned_free(t->allocator, n);
  }

  zix_aligned_free(t->allocator, rhs);
  return lhs;
}

/// Remove and return the min value from the subtree rooted at `n`
static void*
zix_btree_remove_min(ZixBTree* const t, ZixBTreeNode* n)
{
  assert(zix_btree_can_remove_from(n));

  while (!n->is_leaf) {
    ZixBTreeNode* const* const children = n->data.inode.children;

    n = zix_btree_can_remove_from(children[0U])   ? children[0U]
        : zix_btree_can_remove_from(children[1U]) ? zix_btree_rotate_left(n, 0U)
                                                  : zix_btree_merge(t, n, 0U);
  }

  return zix_btree_aerase(n->data.leaf.vals, --n->n_vals, 0U);
}

/// Remove and return the max value from the subtree rooted at `n`
static void*
zix_btree_remove_max(ZixBTree* const t, ZixBTreeNode* n)
{
  assert(zix_btree_can_remove_from(n));

  while (!n->is_leaf) {
    ZixBTreeNode* const* const children = n->data.inode.children;

    const unsigned y = n->n_vals - 1U;
    const unsigned z = n->n_vals;

    n = zix_btree_can_remove_from(children[z])   ? children[z]
        : zix_btree_can_remove_from(children[y]) ? zix_btree_rotate_right(n, z)
                                                 : zix_btree_merge(t, n, y);
  }

  return n->data.leaf.vals[--n->n_vals];
}

static ZixBTreeNode*
zix_btree_fatten_child(ZixBTree* const t, ZixBTreeIter* const iter)
{
  ZixBTreeNode* const n = iter->nodes[iter->level];
  const ZixShort      i = iter->indexes[iter->level];

  assert(n);
  assert(!n->is_leaf);
  assert(n->n_vals);
  ZixBTreeNode* const* const children = n->data.inode.children;

  if (i > 0U && zix_btree_can_remove_from(children[i - 1U])) {
    return zix_btree_rotate_right(n, i); // Steal a key from left sibling
  }

  if (i < n->n_vals && zix_btree_can_remove_from(children[i + 1U])) {
    return zix_btree_rotate_left(n, i); // Steal a key from right sibling
  }

  // Both child's siblings are minimal, merge them

  if (i == n->n_vals) {
    --iter->indexes[iter->level];
    return zix_btree_merge(t, n, i - 1U); // Merge last two children
  }

  return zix_btree_merge(t, n, i); // Merge left and right siblings
}

/// Replace the ith value in `n` with one from a child if possible
static ZixStatus
zix_btree_replace_value(ZixBTree* const     t,
                        ZixBTreeNode* const n,
                        const unsigned      i,
                        void** const        out)
{
  ZixBTreeNode* const lhs = zix_btree_child(n, i);
  ZixBTreeNode* const rhs = zix_btree_child(n, i + 1U);
  if (!zix_btree_can_remove_from(lhs) && !zix_btree_can_remove_from(rhs)) {
    return ZIX_STATUS_NOT_FOUND;
  }

  // Stash the value for the caller before it is replaced
  *out = n->data.inode.vals[i];

  n->data.inode.vals[i] =
    // Left child has more values, steal its largest
    (lhs->n_vals > rhs->n_vals) ? zix_btree_remove_max(t, lhs)

    // Right child has more values, steal its smallest
    : (rhs->n_vals > lhs->n_vals) ? zix_btree_remove_min(t, rhs)

    // Children are balanced, use index parity as a low-bias tie breaker
    : (i & 1U) ? zix_btree_remove_max(t, lhs)
               : zix_btree_remove_min(t, rhs);

  return ZIX_STATUS_SUCCESS;
}

ZixStatus
zix_btree_remove(ZixBTree* const     t,
                 const void* const   e,
                 void** const        out,
                 ZixBTreeIter* const next)
{
  assert(t);
  assert(out);

  if (!t->root) {
    return ZIX_STATUS_NOT_FOUND;
  }

  ZixBTreeNode* n  = t->root;
  ZixBTreeIter* ti = next;
  ZixStatus     st = ZIX_STATUS_SUCCESS;

  *ti = zix_btree_end_iter;

  /* To remove in a single walk down, the tree is adjusted along the way so
     that the current node always has at least one more value than the
     minimum.  This ensures that there is always room to remove, without
     having to merge nodes again on a traversal back up. */

  if (!n->is_leaf && n->n_vals == 1U &&
      !zix_btree_can_remove_from(n->data.inode.children[0U]) &&
      !zix_btree_can_remove_from(n->data.inode.children[1U])) {
    // Root has only two children, both minimal, merge them into a new root
    n = zix_btree_merge(t, n, 0U);
  }

  while (!n->is_leaf) {
    assert(n == t->root || zix_btree_can_remove_from(n));

    // Search for the value in the current node and update the iterator
    bool           equal = false;
    const unsigned i     = zix_btree_inode_find(t, n, e, &equal);

    zix_btree_iter_set_frame(ti, n, i);

    if (equal) {
      // Found in internal node
      if (!(st = zix_btree_replace_value(t, n, i, out))) {
        // Replaced hole with a value from a direct child
        --t->size;
        return st;
      }

      // Both preceding and succeeding child are minimal, merge and continue
      n = zix_btree_merge(t, n, i);

    } else {
      // Not found in internal node, is in the ith child if anywhere
      n = zix_btree_can_remove_from(zix_btree_child(n, i))
            ? zix_btree_child(n, i)
            : zix_btree_fatten_child(t, ti);
    }

    ++ti->level;
  }

  // We're at the leaf the value may be in, search for the value in it
  bool           equal = false;
  const unsigned i     = zix_btree_leaf_find(t, n, e, &equal);

  if (!equal) { // Not found in tree
    *ti = zix_btree_end_iter;
    return ZIX_STATUS_NOT_FOUND;
  }

  // Erase from leaf node
  *out = zix_btree_aerase(n->data.leaf.vals, --n->n_vals, i);

  // Update next iterator
  if (n->n_vals == 0U) {
    // Removed the last element in the tree
    assert(n == t->root);
    assert(t->size == 1U);
    *ti = zix_btree_end_iter;
  } else if (i == n->n_vals) {
    // Removed the largest element in this leaf, increment to the next
    zix_btree_iter_set_frame(ti, n, i - 1U);
    zix_btree_iter_increment(ti);
  } else {
    zix_btree_iter_set_frame(ti, n, i);
  }

  --t->size;
  return ZIX_STATUS_SUCCESS;
}

ZixStatus
zix_btree_find(const ZixBTree* const t,
               const void* const     e,
               ZixBTreeIter* const   ti)
{
  assert(t);
  assert(ti);

  ZixBTreeNode* n = t->root;

  *ti = zix_btree_end_iter;
  if (!n) {
    return ZIX_STATUS_NOT_FOUND;
  }

  while (!n->is_leaf) {
    bool           equal = false;
    const unsigned i     = zix_btree_inode_find(t, n, e, &equal);

    zix_btree_iter_set_frame(ti, n, i);

    if (equal) {
      return ZIX_STATUS_SUCCESS;
    }

    ++ti->level;
    n = zix_btree_child(n, i);
  }

  bool           equal = false;
  const unsigned i     = zix_btree_leaf_find(t, n, e, &equal);
  if (equal) {
    zix_btree_iter_set_frame(ti, n, i);
    return ZIX_STATUS_SUCCESS;
  }

  *ti = zix_btree_end_iter;
  return ZIX_STATUS_NOT_FOUND;
}

ZixStatus
zix_btree_lower_bound(const ZixBTree* const     t,
                      const ZixBTreeCompareFunc compare_key,
                      const void* const         compare_key_user_data,
                      const void* const         key,
                      ZixBTreeIter* const       ti)
{
  assert(t);
  assert(ti);

  *ti = zix_btree_end_iter;

  ZixBTreeNode* n           = t->root; // Current node
  uint16_t      found_level = 0U;      // Lowest level a match was found at
  bool          found       = false;   // True if a match was ever found
  if (!n) {
    return ZIX_STATUS_SUCCESS;
  }

  // Search down until we reach a leaf
  while (!n->is_leaf) {
    bool equal = false;

    const unsigned i = zix_btree_find_pattern(compare_key,
                                              compare_key_user_data,
                                              n->data.inode.vals,
                                              n->n_vals,
                                              key,
                                              &equal);

    zix_btree_iter_set_frame(ti, n, i);
    if (equal) {
      found_level = ti->level;
      found       = true;
    }

    ++ti->level;
    n = zix_btree_child(n, i);
  }

  bool equal = false;

  const unsigned i = zix_btree_find_pattern(compare_key,
                                            compare_key_user_data,
                                            n->data.leaf.vals,
                                            n->n_vals,
                                            key,
                                            &equal);

  zix_btree_iter_set_frame(ti, n, i);
  if (equal) {
    return ZIX_STATUS_SUCCESS;
  }

  if (ti->indexes[ti->level] == ti->nodes[ti->level]->n_vals) {
    if (found) {
      // Found on a previous level but went too far
      ti->level = found_level;
    } else {
      // Reached end (key is greater than everything in tree)
      *ti = zix_btree_end_iter;
    }
  }

  return ZIX_STATUS_SUCCESS;
}

ZIX_REALTIME void*
zix_btree_get(const ZixBTreeIter ti)
{
  const ZixBTreeNode* const node  = ti.nodes[ti.level];
  const unsigned            index = ti.indexes[ti.level];

  assert(node);
  assert(index < node->n_vals);

  return node->is_leaf ? node->data.leaf.vals[index]
                       : node->data.inode.vals[index];
}

ZIX_NONBLOCKING ZixBTreeIter
zix_btree_begin(const ZixBTree* const t)
{
  assert(t);

  ZixBTreeIter iter = zix_btree_end_iter;

  if (t->size > 0U) {
    ZixBTreeNode* n = t->root;
    zix_btree_iter_set_frame(&iter, n, 0U);

    while (!n->is_leaf) {
      n = zix_btree_child(n, 0U);
      zix_btree_iter_push(&iter, n, 0U);
    }
  }

  return iter;
}

ZIX_REALTIME ZixBTreeIter
zix_btree_end(const ZixBTree* const t)
{
  (void)t;

  return zix_btree_end_iter;
}

ZIX_REALTIME bool
zix_btree_iter_equals(const ZixBTreeIter lhs, const ZixBTreeIter rhs)
{
  const size_t indexes_size = ((size_t)lhs.level + 1U) * sizeof(uint16_t);

  return (lhs.level == rhs.level) && (lhs.nodes[0U] == rhs.nodes[0U]) &&
         (!lhs.nodes[0U] || !memcmp(lhs.indexes, rhs.indexes, indexes_size));
}

ZIX_NONBLOCKING ZixStatus
zix_btree_iter_increment(ZixBTreeIter* const i)
{
  assert(i);
  assert(!zix_btree_iter_is_end(*i));

  // Move to the next value in the current node
  const uint16_t index = ++i->indexes[i->level];

  if (i->nodes[i->level]->is_leaf) {
    // Leaf, move up if necessary until we're not at the end of the node
    while (i->indexes[i->level] >= i->nodes[i->level]->n_vals) {
      if (i->level == 0U) {
        // End of root, end of tree
        i->nodes[0U] = NULL;
        return ZIX_STATUS_REACHED_END;
      }

      // At end of internal node, move up
      zix_btree_iter_pop(i);
    }

  } else {
    // Internal node, move down to next child
    const ZixBTreeNode* const node  = i->nodes[i->level];
    ZixBTreeNode* const       child = node->data.inode.children[index];

    zix_btree_iter_push(i, child, 0U);

    // Move down and left until we hit a leaf
    while (!i->nodes[i->level]->is_leaf) {
      zix_btree_iter_push(i, i->nodes[i->level]->data.inode.children[0U], 0U);
    }
  }

  return ZIX_STATUS_SUCCESS;
}

ZIX_NONBLOCKING ZixBTreeIter
zix_btree_iter_next(const ZixBTreeIter iter)
{
  ZixBTreeIter next = iter;

  zix_btree_iter_increment(&next);

  return next;
}
