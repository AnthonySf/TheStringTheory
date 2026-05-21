// Copyright 2011-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "sord_internal.h"

#include <serd/serd.h>
#include <sord/sord.h>

#define ZIX_HASH_KEY_TYPE SordNode
#define ZIX_HASH_RECORD_TYPE SordNode
#define ZIX_HASH_SEARCH_DATA_TYPE SordWorld
#include <zix/btree.h>
#include <zix/digest.h>
#include <zix/hash.h>
#include <zix/status.h>

#include <assert.h>
#include <stdarg.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef __GNUC__
#  define SORD_LOG_FUNC(fmt, arg1) __attribute__((format(printf, fmt, arg1)))
#else
#  define SORD_LOG_FUNC(fmt, arg1)
#endif

#define SORD_LOG(prefix, ...) fprintf(stderr, "[Sord::" prefix "] " __VA_ARGS__)

#ifdef SORD_DEBUG_ITER
#  define SORD_ITER_LOG(...) SORD_LOG("iter", __VA_ARGS__)
#else
#  define SORD_ITER_LOG(...)
#endif
#ifdef SORD_DEBUG_SEARCH
#  define SORD_FIND_LOG(...) SORD_LOG("search", __VA_ARGS__)
#else
#  define SORD_FIND_LOG(...)
#endif
#ifdef SORD_DEBUG_WRITE
#  define SORD_WRITE_LOG(...) SORD_LOG("write", __VA_ARGS__)
#else
#  define SORD_WRITE_LOG(...)
#endif

#define NUM_ORDERS 12U
#define TUP_LEN 4U
#define DEFAULT_ORDER SPO
#define DEFAULT_GRAPH_ORDER GSPO

#define TUP_FMT "(%s %s %s %s)"
#define TUP_FMT_ELEM(e) ((e) ? sord_node_get_string(e) : (const uint8_t*)"*")
#define TUP_FMT_ARGS(t)                                             \
  TUP_FMT_ELEM((t)[0]), TUP_FMT_ELEM((t)[1]), TUP_FMT_ELEM((t)[2]), \
    TUP_FMT_ELEM((t)[3])

#define TUP_G 3U

/** Triple ordering */
typedef enum {
  SPO,  ///<         Subject,   Predicate, Object
  SOP,  ///<         Subject,   Object,    Predicate
  OPS,  ///<         Object,    Predicate, Subject
  OSP,  ///<         Object,    Subject,   Predicate
  PSO,  ///<         Predicate, Subject,   Object
  POS,  ///<         Predicate, Object,    Subject
  GSPO, ///< Graph,  Subject,   Predicate, Object
  GSOP, ///< Graph,  Subject,   Object,    Predicate
  GOPS, ///< Graph,  Object,    Predicate, Subject
  GOSP, ///< Graph,  Object,    Subject,   Predicate
  GPSO, ///< Graph,  Predicate, Subject,   Object
  GPOS  ///< Graph,  Predicate, Object,    Subject
} SordOrder;

#ifdef SORD_DEBUG_SEARCH
/** String name of each ordering (array indexed by SordOrder) */
static const char* const order_names[NUM_ORDERS] = {"spo",
                                                    "sop",
                                                    "ops",
                                                    "osp",
                                                    "pso",
                                                    "pos",
                                                    "gspo",
                                                    "gsop",
                                                    "gops",
                                                    "gosp",
                                                    "gpso",
                                                    "gpos"};
#endif

/**
   Quads of indices for each order, from most to least significant
   (array indexed by SordOrder)
*/
static const uint8_t orderings[NUM_ORDERS][TUP_LEN] = {
  {0, 1, 2, 3}, // SPO
  {0, 2, 1, 3}, // SOP
  {2, 1, 0, 3}, // OPS
  {2, 0, 1, 3}, // OSP
  {1, 0, 2, 3}, // PSO
  {1, 2, 0, 3}, // POS
  {3, 0, 1, 2}, // GSPO
  {3, 0, 2, 1}, // GSOP
  {3, 2, 1, 0}, // GOPS
  {3, 2, 0, 1}, // GOSP
  {3, 1, 0, 2}, // GPSO
  {3, 1, 2, 0}  // GPOS
};

/** World */
struct SordWorldImpl {
  ZixHash*      nodes;
  SerdErrorSink error_sink;
  void*         error_handle;
};

/** Store */
struct SordModelImpl {
  SordWorld* world;

  /** Index for each possible triple ordering (may or may not exist).
   * Each index is a tree of SordQuad with the appropriate ordering.
   */
  ZixBTree* indices[NUM_ORDERS];

  size_t n_quads;
  size_t n_iters;
};

/** Mode for searching or iteration */
typedef enum {
  ALL,          ///< Iterate over entire store
  SINGLE,       ///< Iteration over a single element (exact search)
  RANGE,        ///< Iterate over range with equal prefix
  FILTER_RANGE, ///< Iterate over range with equal prefix, filtering
  FILTER_ALL    ///< Iterate to end of store, filtering
} SearchMode;

/** Iterator over some range of a store */
struct SordIterImpl {
  const SordModel* sord;        ///< Model being iterated over
  ZixBTreeIter     cur;         ///< Current DB cursor
  SordQuad         pat;         ///< Pattern (in ordering order)
  SordOrder        order;       ///< Store order (which index)
  SearchMode       mode;        ///< Iteration mode
  uint8_t          n_prefix;    ///< Prefix for RANGE and FILTER_RANGE
  bool             end;         ///< True iff reached end
  bool             skip_graphs; ///< Iteration should ignore graphs
};

static uint8_t*
sord_strndup(const uint8_t* str, size_t len)
{
  uint8_t* dup = (uint8_t*)malloc(len + 1);
  memcpy(dup, str, len + 1);
  return dup;
}

static const SordNode*
sord_node_record_key(const SordNode* n)
{
  return n;
}

static size_t
sord_node_hash(const SordNode* const node)
{
  const size_t head = node->node.type;

  size_t hash = 0U;

  hash = zix_digest(hash, (const uint8_t*)node->node.buf, node->node.n_bytes);
  hash = zix_digest(hash, &head, sizeof(head));
  if (node->node.type == SERD_LITERAL) {
    hash = zix_digest(hash, &node->meta.lit, sizeof(node->meta.lit));
  }

  return hash;
}

static bool
sord_node_hash_equal(const SordNode* const a, const SordNode* const b)
{
  return ((a == b) || ((a->node.type == b->node.type) &&
                       (a->node.type != SERD_LITERAL ||
                        (a->meta.lit.datatype == b->meta.lit.datatype &&
                         !strncmp(a->meta.lit.lang,
                                  b->meta.lit.lang,
                                  sizeof(a->meta.lit.lang)))) &&
                       (serd_node_equals(&a->node, &b->node))));
}

static SordNode*
sord_node_create(const SordNode* const node)
{
  SordNode* const copy = node ? (SordNode*)malloc(sizeof(SordNode)) : NULL;

  if (copy) {
    memcpy(copy, node, sizeof(SordNode));
    copy->node.buf = sord_strndup(copy->node.buf, copy->node.n_bytes);
    if (copy->node.type == SERD_LITERAL) {
      copy->meta.lit.datatype = sord_node_copy(copy->meta.lit.datatype);
    }
  }

  return copy;
}

SORD_LOG_FUNC(3, 4) static void
error(SordWorld* world, SerdStatus st, const char* fmt, ...)
{
  va_list args;
  va_start(args, fmt);
  const SerdError e = {st, NULL, 0, 0, fmt, &args};
  if (world->error_sink) {
    world->error_sink(world->error_handle, &e);
  } else {
    fprintf(stderr, "error: ");
    vfprintf(stderr, fmt, args);
  }
  va_end(args);
}

SordWorld*
sord_world_new(void)
{
  SordWorld* world    = (SordWorld*)malloc(sizeof(SordWorld));
  world->error_sink   = NULL;
  world->error_handle = NULL;

  world->nodes = zix_hash_new(
    NULL, sord_node_record_key, sord_node_hash, sord_node_hash_equal);

  return world;
}

static void
free_node_entry(SordNode* const node)
{
  free((uint8_t*)node->node.buf);
  free(node);
}

void
sord_world_free(SordWorld* world)
{
  if (!world) {
    return;
  }

  for (ZixHashIter i = zix_hash_begin(world->nodes);
       i != zix_hash_end(world->nodes);
       i = zix_hash_next(world->nodes, i)) {
    free_node_entry(zix_hash_get(world->nodes, i));
  }

  zix_hash_free(world->nodes);
  free(world);
}

void
sord_world_set_error_sink(SordWorld*    world,
                          SerdErrorSink error_sink,
                          void*         handle)
{
  world->error_sink   = error_sink;
  world->error_handle = handle;
}

static inline int
sord_node_compare_literal(const SordNode* a, const SordNode* b)
{
  const int cmp = strcmp((const char*)sord_node_get_string(a),
                         (const char*)sord_node_get_string(b));
  if (cmp != 0) {
    return cmp;
  }

  const bool a_has_lang     = a->meta.lit.lang[0];
  const bool b_has_lang     = b->meta.lit.lang[0];
  const bool a_has_datatype = a->meta.lit.datatype;
  const bool b_has_datatype = b->meta.lit.datatype;
  const bool a_has_meta     = a_has_lang || a_has_datatype;
  const bool b_has_meta     = b_has_lang || b_has_datatype;

  assert(!(a_has_lang && a_has_datatype));
  assert(!(b_has_lang && b_has_datatype));

  if (!a_has_meta && !b_has_meta) {
    return 0;
  } else if (!a_has_meta || (a_has_lang && b_has_datatype)) {
    return -1;
  } else if (!b_has_meta || (a_has_datatype && b_has_lang)) {
    return 1;
  } else if (a_has_lang) {
    assert(b_has_lang);
    return strcmp(a->meta.lit.lang, b->meta.lit.lang);
  }

  assert(a_has_datatype);
  assert(b_has_datatype);
  return strcmp((const char*)a->meta.lit.datatype->node.buf,
                (const char*)b->meta.lit.datatype->node.buf);
}

/** Compare nodes, considering NULL a wildcard match. */
static inline int
sord_node_compare(const SordNode* a, const SordNode* b)
{
  if (a == b || !a || !b) {
    return 0; // Exact or wildcard match
  } else if (a->node.type != b->node.type) {
    return (a->node.type < b->node.type) ? -1 : 1;
  }

  int cmp = 0;
  switch (a->node.type) {
  case SERD_URI:
  case SERD_BLANK:
    return strcmp((const char*)a->node.buf, (const char*)b->node.buf);
  case SERD_LITERAL:
    cmp = sord_node_compare_literal(a, b);
    break;
  default:
    break;
  }
  return cmp;
}

bool
sord_node_equals(const SordNode* a, const SordNode* b)
{
  return a == b; // Nodes are interned
}

/** Return true iff IDs are equivalent, or one is a wildcard */
static inline bool
sord_id_match(const SordNode* a, const SordNode* b)
{
  return !a || !b || (a == b);
}

static inline bool
sord_quad_match_inline(const SordQuad x, const SordQuad y)
{
  return (sord_id_match(x[0], y[0]) && //
          sord_id_match(x[1], y[1]) && //
          sord_id_match(x[2], y[2]) && //
          sord_id_match(x[3], y[3]));
}

bool
sord_quad_match(const SordQuad x, const SordQuad y)
{
  return sord_quad_match_inline(x, y);
}

/**
   Compare two quad IDs lexicographically.
   NULL IDs (equal to 0) are treated as wildcards, always less than every
   other possible ID, except itself.
*/
static int
sord_quad_compare(const void* x_ptr, const void* y_ptr, const void* user_data)
{
  const uint8_t* const         ordering = (const uint8_t*)user_data;
  const SordNode* const* const x        = (const SordNode* const*)x_ptr;
  const SordNode* const* const y        = (const SordNode* const*)y_ptr;

  for (uint8_t i = 0U; i < TUP_LEN; ++i) {
    const uint8_t idx = ordering[i];
    const int     cmp = sord_node_compare(x[idx], y[idx]);
    if (cmp) {
      return cmp;
    }
  }

  return 0;
}

static inline bool
sord_iter_forward(SordIter* iter)
{
  if (!iter->skip_graphs) {
    zix_btree_iter_increment(&iter->cur);
    return zix_btree_iter_is_end(iter->cur);
  }

  SordNode**     key     = (SordNode**)zix_btree_get(iter->cur);
  const SordQuad initial = {key[0], key[1], key[2], key[3]};
  zix_btree_iter_increment(&iter->cur);
  while (!zix_btree_iter_is_end(iter->cur)) {
    key = (SordNode**)zix_btree_get(iter->cur);
    for (int i = 0; i < 3; ++i) {
      if (key[i] != initial[i]) {
        return false;
      }
    }

    zix_btree_iter_increment(&iter->cur);
  }

  return true;
}

/**
   Seek forward as necessary until `iter` points at a match.
   @return true iff iterator reached end of valid range.
*/
static inline bool
sord_iter_seek_match(SordIter* iter)
{
  for (iter->end = true; !zix_btree_iter_is_end(iter->cur);
       sord_iter_forward(iter)) {
    const SordNode** const key = (const SordNode**)zix_btree_get(iter->cur);
    if (key && sord_quad_match_inline(key, iter->pat)) {
      return (iter->end = false);
    }
  }
  return true;
}

/**
   Seek forward as necessary until `iter` points at a match, or the prefix
   no longer matches iter->pat.
   @return true iff iterator reached end of valid range.
*/
static inline bool
sord_iter_seek_match_range(SordIter* iter)
{
  assert(!iter->end);

  do {
    const SordNode** key = (const SordNode**)zix_btree_get(iter->cur);

    if (sord_quad_match_inline(key, iter->pat)) {
      return false; // Found match
    }

    for (uint8_t i = 0U; i < iter->n_prefix; ++i) {
      const uint8_t idx = orderings[iter->order][i];
      if (!sord_id_match(key[idx], iter->pat[idx])) {
        iter->end = true; // Reached end of valid range
        return true;
      }
    }
  } while (!sord_iter_forward(iter));

  return (iter->end = true); // Reached end
}

static SordIter*
sord_iter_new(const SordModel*   sord,
              const ZixBTreeIter cur,
              const SordQuad     pat,
              SordOrder          order,
              SearchMode         mode,
              uint8_t            n_prefix)
{
  SordIter* iter    = (SordIter*)malloc(sizeof(SordIter));
  iter->sord        = sord;
  iter->cur         = cur;
  iter->order       = order;
  iter->mode        = mode;
  iter->n_prefix    = n_prefix;
  iter->end         = false;
  iter->skip_graphs = order < GSPO;
  for (uint8_t i = 0U; i < TUP_LEN; ++i) {
    iter->pat[i] = pat[i];
  }

  switch (iter->mode) {
  case ALL:
  case SINGLE:
  case RANGE:
    assert(sord_quad_match_inline((const SordNode**)zix_btree_get(iter->cur),
                                  iter->pat));
    break;
  case FILTER_RANGE:
    sord_iter_seek_match_range(iter);
    break;
  case FILTER_ALL:
    sord_iter_seek_match(iter);
    break;
  }

#ifdef SORD_DEBUG_ITER
  SordQuad value;
  sord_iter_get(iter, value);
  SORD_ITER_LOG("New %p pat=" TUP_FMT " cur=" TUP_FMT " end=%d skip=%d\n",
                (void*)iter,
                TUP_FMT_ARGS(pat),
                TUP_FMT_ARGS(value),
                iter->end,
                iter->skip_graphs);
#endif

  ++((SordModel*)sord)->n_iters;
  return iter;
}

const SordModel*
sord_iter_get_model(SordIter* iter)
{
  return iter ? iter->sord : NULL;
}

void
sord_iter_get(const SordIter* iter, SordQuad tup)
{
  SordNode** key = (SordNode**)zix_btree_get(iter->cur);
  for (uint8_t i = 0U; i < TUP_LEN; ++i) {
    tup[i] = key[i];
  }
}

const SordNode*
sord_iter_get_node(const SordIter* iter, SordQuadIndex index)
{
  return (!sord_iter_end(iter) ? ((SordNode**)zix_btree_get(iter->cur))[index]
                               : NULL);
}

static bool
sord_iter_scan_next(SordIter* iter)
{
  if (iter->end) {
    return true;
  }

  const SordNode** key = NULL;
  if (!iter->end) {
    switch (iter->mode) {
    case ALL:
      // At the end if the cursor is (assigned above)
      break;
    case SINGLE:
      iter->end = true;
      SORD_ITER_LOG("%p reached single end\n", (void*)iter);
      break;
    case RANGE:
      SORD_ITER_LOG("%p range next\n", (void*)iter);
      // At the end if the MSNs no longer match
      key = (const SordNode**)zix_btree_get(iter->cur);
      assert(key);
      for (uint8_t i = 0U; i < iter->n_prefix; ++i) {
        const uint8_t idx = orderings[iter->order][i];
        if (!sord_id_match(key[idx], iter->pat[idx])) {
          iter->end = true;
          SORD_ITER_LOG("%p reached non-match end\n", (void*)iter);
          break;
        }
      }
      break;
    case FILTER_RANGE:
      // Seek forward to next match, stopping if prefix changes
      sord_iter_seek_match_range(iter);
      break;
    case FILTER_ALL:
      // Seek forward to next match
      sord_iter_seek_match(iter);
      break;
    }
  } else {
    SORD_ITER_LOG("%p reached index end\n", (void*)iter);
  }

  if (iter->end) {
    SORD_ITER_LOG("%p Reached end\n", (void*)iter);
    return true;
  } else {
#ifdef SORD_DEBUG_ITER
    SordQuad tup;
    sord_iter_get(iter, tup);
    SORD_ITER_LOG(
      "%p Increment to " TUP_FMT "\n", (void*)iter, TUP_FMT_ARGS(tup));
#endif
    return false;
  }
}

bool
sord_iter_next(SordIter* iter)
{
  if (!iter || iter->end) {
    return true;
  }

  iter->end = sord_iter_forward(iter);
  return sord_iter_scan_next(iter);
}

bool
sord_iter_end(const SordIter* iter)
{
  return !iter || iter->end;
}

void
sord_iter_free(SordIter* iter)
{
  SORD_ITER_LOG("%p Free\n", (void*)iter);
  if (iter) {
    --((SordModel*)iter->sord)->n_iters;
    free(iter);
  }
}

/**
   Return true iff `sord` has an index for `order`.
   If `graphs` is true, `order` will be modified to be the
   corresponding order with a G prepended (so G will be the MSN).
*/
static inline bool
sord_has_index(const SordModel* model,
               SordOrder*       order,
               uint8_t*         n_prefix,
               bool             graphs)
{
  if (graphs) {
    *order = (SordOrder)(*order + GSPO);
    ++*n_prefix;
  }

  return model->indices[*order];
}

/**
   Return the best available index for a pattern.
   @param pat Pattern in standard (S P O G) order
   @param mode Set to the (best) iteration mode for iterating over results
   @param n_prefix Set to the length of the range prefix
   (for `mode` == RANGE and `mode` == FILTER_RANGE)
*/
static inline SordOrder
sord_best_index(const SordModel* sord,
                const SordQuad   pat,
                SearchMode*      mode,
                uint8_t*         n_prefix)
{
  const bool graph_search = (pat[TUP_G] != 0);

  const unsigned sig = (((pat[0] ? 1U : 0U) * 0x100U) + //
                        ((pat[1] ? 1U : 0U) * 0x010U) + //
                        ((pat[2] ? 1U : 0U) * 0x001U));

  SordOrder good[2] = {(SordOrder)-1, (SordOrder)-1};

#define PAT_CASE(sig, m, g0, g1, np) \
  case sig:                          \
    *mode     = m;                   \
    good[0]   = g0;                  \
    good[1]   = g1;                  \
    *n_prefix = np;                  \
    break

  // Good orderings that don't require filtering
  *mode     = RANGE;
  *n_prefix = 0U;
  switch (sig) {
  case 0x000:
    assert(graph_search);
    *mode     = RANGE;
    *n_prefix = 1U;
    return DEFAULT_GRAPH_ORDER;
  case 0x111:
    *mode = SINGLE;
    return graph_search ? DEFAULT_GRAPH_ORDER : DEFAULT_ORDER;

    PAT_CASE(0x001, RANGE, OPS, OSP, 1);
    PAT_CASE(0x010, RANGE, POS, PSO, 1);
    PAT_CASE(0x011, RANGE, OPS, POS, 2);
    PAT_CASE(0x100, RANGE, SPO, SOP, 1);
    PAT_CASE(0x101, RANGE, SOP, OSP, 2);
    PAT_CASE(0x110, RANGE, SPO, PSO, 2);
  }

  if (*mode == RANGE) {
    if (sord_has_index(sord, &good[0], n_prefix, graph_search)) {
      return good[0];
    } else if (sord_has_index(sord, &good[1], n_prefix, graph_search)) {
      return good[1];
    }
  }

  // Not so good orderings that require filtering, but can
  // still be constrained to a range
  switch (sig) {
    PAT_CASE(0x011, FILTER_RANGE, OSP, PSO, 1);
    PAT_CASE(0x101, FILTER_RANGE, SPO, OPS, 1);
    // SPO is always present, so 0x110 is never reached here
  default:
    break;
  }

  if (*mode == FILTER_RANGE) {
    if (sord_has_index(sord, &good[0], n_prefix, graph_search)) {
      return good[0];
    } else if (sord_has_index(sord, &good[1], n_prefix, graph_search)) {
      return good[1];
    }
  }

  if (graph_search) {
    *mode     = FILTER_RANGE;
    *n_prefix = 1U;
    return DEFAULT_GRAPH_ORDER;
  } else {
    *mode = FILTER_ALL;
    return DEFAULT_ORDER;
  }
}

SordModel*
sord_new(SordWorld* world, unsigned indices, bool graphs)
{
  static const unsigned half = (NUM_ORDERS >> 1U);

  SordModel* model = (SordModel*)malloc(sizeof(struct SordModelImpl));
  model->world     = world;
  model->n_quads   = 0;
  model->n_iters   = 0;

  for (unsigned i = 0; i < half; ++i) {
    if (indices & (1U << i)) {
      model->indices[i] =
        zix_btree_new(NULL, sord_quad_compare, (void*)orderings[i]);
      if (graphs) {
        model->indices[i + half] =
          zix_btree_new(NULL, sord_quad_compare, (void*)orderings[i + half]);
      } else {
        model->indices[i + half] = NULL;
      }
    } else {
      model->indices[i]        = NULL;
      model->indices[i + half] = NULL;
    }
  }

  if (!model->indices[DEFAULT_ORDER]) {
    model->indices[DEFAULT_ORDER] =
      zix_btree_new(NULL, sord_quad_compare, (void*)orderings[DEFAULT_ORDER]);
  }
  if (graphs && !model->indices[DEFAULT_GRAPH_ORDER]) {
    model->indices[DEFAULT_GRAPH_ORDER] = zix_btree_new(
      NULL, sord_quad_compare, (void*)orderings[DEFAULT_GRAPH_ORDER]);
  }

  return model;
}

static void
sord_node_free_internal(SordWorld* world, SordNode* node)
{
  assert(node->refs == 0);

  // If you hit this, the world has probably been destroyed too early
  assert(world);

  // Remove node from the hash table
  ZixHashRecord* removed = NULL;
  if (!zix_hash_remove(world->nodes, node, &removed)) {
    free((uint8_t*)removed->node.buf);
    if (removed->node.type == SERD_LITERAL) {
      sord_node_free(world, removed->meta.lit.datatype);
    }
    free(removed);
  } else {
    error(world, SERD_ERR_INTERNAL, "failed to remove node from hash\n");
  }
}

static void
sord_add_quad_ref(SordModel* model, const SordNode* node, SordQuadIndex i)
{
  (void)model;

  if (node) {
    assert(node->refs > 0);
    ++((SordNode*)node)->refs;
    if (node->node.type != SERD_LITERAL && i == SORD_OBJECT) {
      ++((SordNode*)node)->meta.res.refs_as_obj;
    }
  }
}

static void
sord_drop_quad_ref(SordModel* model, const SordNode* node, SordQuadIndex i)
{
  if (!node) {
    return;
  }

  assert(node->refs > 0);
  if (node->node.type != SERD_LITERAL && i == SORD_OBJECT) {
    assert(node->meta.res.refs_as_obj > 0);
    --((SordNode*)node)->meta.res.refs_as_obj;
  }
  if (--((SordNode*)node)->refs == 0) {
    sord_node_free_internal(sord_get_world(model), (SordNode*)node);
  }
}

void
sord_free(SordModel* model)
{
  if (!model) {
    return;
  }

  // Free nodes
  SordQuad  tup;
  SordIter* i = sord_begin(model);
  for (; !sord_iter_end(i); sord_iter_next(i)) {
    sord_iter_get(i, tup);
    for (uint8_t t = 0U; t < TUP_LEN; ++t) {
      sord_drop_quad_ref(model, tup[t], (SordQuadIndex)t);
    }
  }
  sord_iter_free(i);

  // Free quads
  ZixBTreeIter t = zix_btree_begin(model->indices[DEFAULT_ORDER]);
  for (; !zix_btree_iter_is_end(t); zix_btree_iter_increment(&t)) {
    free(zix_btree_get(t));
  }

  // Free indices
  for (unsigned o = 0; o < NUM_ORDERS; ++o) {
    if (model->indices[o]) {
      zix_btree_free(model->indices[o], NULL, NULL);
    }
  }

  free(model);
}

SordWorld*
sord_get_world(SordModel* model)
{
  return model->world;
}

size_t
sord_num_quads(const SordModel* model)
{
  return model ? model->n_quads : 0U;
}

size_t
sord_num_nodes(const SordWorld* world)
{
  return world ? zix_hash_size(world->nodes) : 0U;
}

SordIter*
sord_begin(const SordModel* model)
{
  if (sord_num_quads(model) == 0) {
    return NULL;
  } else {
    const ZixBTreeIter cur = zix_btree_begin(model->indices[DEFAULT_ORDER]);
    SordQuad           pat = {0, 0, 0, 0};
    return sord_iter_new(model, cur, pat, DEFAULT_ORDER, ALL, 0);
  }
}

SordIter*
sord_find(const SordModel* model, const SordQuad pat)
{
  if (!model || (!pat[0] && !pat[1] && !pat[2] && !pat[3])) {
    return sord_begin(model);
  }

  SearchMode      mode        = ALL;
  uint8_t         n_prefix    = 0U;
  const SordOrder index_order = sord_best_index(model, pat, &mode, &n_prefix);

  SORD_FIND_LOG("Find " TUP_FMT "  index=%s  mode=%u  n_prefix=%u\n",
                TUP_FMT_ARGS(pat),
                order_names[index_order],
                mode,
                n_prefix);

  if (pat[0] && pat[1] && pat[2] && pat[3]) {
    mode = SINGLE; // No duplicate quads (Sord is a set)
  }

  const uint8_t* const ordering = orderings[index_order];
  ZixBTree* const      db       = model->indices[index_order];
  ZixBTreeIter         cur      = zix_btree_end(db);

  if (mode == FILTER_ALL) {
    // No prefix shared with an index at all, linear search (worst case)
    cur = zix_btree_begin(db);
  } else if (mode == FILTER_RANGE) {
    /* Some prefix, but filtering still required.  Build a search pattern
       with only the prefix to find the lower bound in log time. */
    SordQuad prefix_pat = {NULL, NULL, NULL, NULL};
    for (uint8_t i = 0U; i < n_prefix; ++i) {
      prefix_pat[ordering[i]] = pat[ordering[i]];
    }

    zix_btree_lower_bound(db, sord_quad_compare, ordering, prefix_pat, &cur);
  } else {
    // Ideal case, pattern matches an index with no filtering required
    zix_btree_lower_bound(db, sord_quad_compare, ordering, pat, &cur);
  }

  if (zix_btree_iter_is_end(cur)) {
    SORD_FIND_LOG("No match found\n");
    return NULL;
  }

  const SordNode** const key = (const SordNode**)zix_btree_get(cur);
  if (!key || ((mode == RANGE || mode == SINGLE) &&
               !sord_quad_match_inline(pat, key))) {
    SORD_FIND_LOG("No match found\n");
    return NULL;
  }

  return sord_iter_new(model, cur, pat, index_order, mode, n_prefix);
}

SordIter*
sord_search(const SordModel* model,
            const SordNode*  s,
            const SordNode*  p,
            const SordNode*  o,
            const SordNode*  g)
{
  SordQuad pat = {s, p, o, g};
  return sord_find(model, pat);
}

SordNode*
sord_get(const SordModel* model,
         const SordNode*  s,
         const SordNode*  p,
         const SordNode*  o,
         const SordNode*  g)
{
  if (!!s + !!p + !!o != 2) {
    return NULL;
  }

  SordIter* i   = sord_search(model, s, p, o, g);
  SordNode* ret = NULL;
  if (!s) {
    ret = sord_node_copy(sord_iter_get_node(i, SORD_SUBJECT));
  } else if (!p) {
    ret = sord_node_copy(sord_iter_get_node(i, SORD_PREDICATE));
  } else if (!o) {
    ret = sord_node_copy(sord_iter_get_node(i, SORD_OBJECT));
  }

  sord_iter_free(i);
  return ret;
}

bool
sord_ask(const SordModel* model,
         const SordNode*  s,
         const SordNode*  p,
         const SordNode*  o,
         const SordNode*  g)
{
  SordQuad pat = {s, p, o, g};
  return sord_contains(model, pat);
}

uint64_t
sord_count(const SordModel* model,
           const SordNode*  s,
           const SordNode*  p,
           const SordNode*  o,
           const SordNode*  g)
{
  SordIter* i = sord_search(model, s, p, o, g);
  uint64_t  n = 0;
  for (; !sord_iter_end(i); sord_iter_next(i)) {
    ++n;
  }
  sord_iter_free(i);
  return n;
}

bool
sord_contains(const SordModel* model, const SordQuad pat)
{
  SordIter* iter = sord_find(model, pat);
  bool      ret  = (iter != NULL);
  sord_iter_free(iter);
  return ret;
}

SordNodeType
sord_node_get_type(const SordNode* node)
{
  switch (node->node.type) {
  case SERD_URI:
    return SORD_URI;
  case SERD_BLANK:
    return SORD_BLANK;
  default:
    return SORD_LITERAL;
  }
  SORD_UNREACHABLE();
}

const uint8_t*
sord_node_get_string(const SordNode* node)
{
  const uint8_t* const string = node->node.buf;
  assert(string);
  return string;
}

const uint8_t*
sord_node_get_string_counted(const SordNode* node, size_t* bytes)
{
  *bytes = node->node.n_bytes;
  return sord_node_get_string(node);
}

const uint8_t*
sord_node_get_string_measured(const SordNode* node,
                              size_t*         bytes,
                              size_t*         chars)
{
  *bytes = node->node.n_bytes;
  *chars = node->node.n_chars;
  return sord_node_get_string(node);
}

const char*
sord_node_get_language(const SordNode* node)
{
  if (node->node.type != SERD_LITERAL || !node->meta.lit.lang[0]) {
    return NULL;
  }
  return node->meta.lit.lang;
}

SordNode*
sord_node_get_datatype(const SordNode* node)
{
  return (node->node.type == SERD_LITERAL) ? node->meta.lit.datatype : NULL;
}

SerdNodeFlags
sord_node_get_flags(const SordNode* node)
{
  return node->node.flags;
}

bool
sord_node_is_inline_object(const SordNode* node)
{
  return (node->node.type == SERD_BLANK) && (node->meta.res.refs_as_obj == 1);
}

static SordNode*
sord_insert_node(SordWorld* world, const SordNode* key)
{
  // "Plan" the insertion (that is, search) with the given constant key
  const ZixHashInsertPlan plan     = zix_hash_plan_insert(world->nodes, key);
  SordNode* const         existing = zix_hash_record_at(world->nodes, plan);
  if (existing) {
    ++existing->refs;
    return existing;
  }

  // Insert a new node into hash table, transferring ownership
  SordNode* const node = sord_node_create(key);
  const ZixStatus st   = zix_hash_insert_at(world->nodes, plan, node);
  if (st) {
    free((uint8_t*)node->node.buf);
    free(node);
    error(
      world, SERD_ERR_INTERNAL, "error inserting node `%s'\n", key->node.buf);
    return NULL;
  }

  return node;
}

static SordNode*
sord_new_uri_counted(SordWorld*     world,
                     const uint8_t* str,
                     size_t         n_bytes,
                     size_t         n_chars)
{
  if (!serd_uri_string_has_scheme(str)) {
    error(world, SERD_ERR_BAD_ARG, "attempt to map invalid URI `%s'\n", str);
    return NULL; // Can't intern relative URIs
  }

  const SordNode key = {{str, n_bytes, n_chars, 0, SERD_URI}, 1, {{0}}};

  return sord_insert_node(world, &key);
}

SordNode*
sord_new_uri(SordWorld* world, const uint8_t* uri)
{
  const SerdNode node = serd_node_from_string(SERD_URI, uri);
  return sord_new_uri_counted(world, uri, node.n_bytes, node.n_chars);
}

SordNode*
sord_new_relative_uri(SordWorld*     world,
                      const uint8_t* uri,
                      const uint8_t* base_uri)
{
  if (serd_uri_string_has_scheme(uri)) {
    return sord_new_uri(world, uri);
  }

  SerdURI  buri = SERD_URI_NULL;
  SerdNode base = serd_node_new_uri_from_string(base_uri, NULL, &buri);
  SerdNode node = serd_node_new_uri_from_string(uri, &buri, NULL);

  SordNode* ret =
    sord_new_uri_counted(world, node.buf, node.n_bytes, node.n_chars);

  serd_node_free(&node);
  serd_node_free(&base);
  return ret;
}

static SordNode*
sord_new_blank_counted(SordWorld*     world,
                       const uint8_t* str,
                       size_t         n_bytes,
                       size_t         n_chars)
{
  const SordNode key = {{str, n_bytes, n_chars, 0, SERD_BLANK}, 1, {{0}}};

  return sord_insert_node(world, &key);
}

SordNode*
sord_new_blank(SordWorld* world, const uint8_t* str)
{
  const SerdNode node = serd_node_from_string(SERD_URI, str);
  return sord_new_blank_counted(world, str, node.n_bytes, node.n_chars);
}

static SordNode*
sord_new_literal_counted(SordWorld*     world,
                         SordNode*      datatype,
                         const uint8_t* str,
                         size_t         n_bytes,
                         size_t         n_chars,
                         SerdNodeFlags  flags,
                         const char*    lang)
{
  SordNode key = {{str, n_bytes, n_chars, flags, SERD_LITERAL}, 1, {{0}}};
  key.meta.lit.datatype = sord_node_copy(datatype);
  memset(key.meta.lit.lang, 0, sizeof(key.meta.lit.lang));
  if (lang) {
    strncpy(key.meta.lit.lang, lang, sizeof(key.meta.lit.lang) - 1);
  }

  return sord_insert_node(world, &key);
}

SordNode*
sord_new_literal(SordWorld*     world,
                 SordNode*      datatype,
                 const uint8_t* str,
                 const char*    lang)
{
  SerdNodeFlags flags   = 0;
  size_t        n_bytes = 0;
  size_t        n_chars = serd_strlen(str, &n_bytes, &flags);
  return sord_new_literal_counted(
    world, datatype, str, n_bytes, n_chars, flags, lang);
}

static SordNode*
sord_node_from_serd_node_internal(SordWorld*      world,
                                  SerdEnv*        env,
                                  const SerdNode* node,
                                  const SerdNode* datatype,
                                  const SerdNode* lang)
{
  SordNode* datatype_node = NULL;
  SordNode* ret           = NULL;
  switch (node->type) {
  case SERD_NOTHING:
    return NULL;
  case SERD_LITERAL:
    datatype_node = sord_node_from_serd_node(world, env, datatype, NULL, NULL);

    ret = sord_new_literal_counted(world,
                                   datatype_node,
                                   node->buf,
                                   node->n_bytes,
                                   node->n_chars,
                                   node->flags,
                                   lang ? (const char*)lang->buf : NULL);

    sord_node_free(world, datatype_node);
    return ret;
  case SERD_URI:
    if (serd_uri_string_has_scheme(node->buf)) {
      return sord_new_uri_counted(
        world, node->buf, node->n_bytes, node->n_chars);
    } else {
      SerdURI base_uri;
      serd_env_get_base_uri(env, &base_uri);
      SerdURI  abs_uri;
      SerdNode abs_uri_node =
        serd_node_new_uri_from_node(node, &base_uri, &abs_uri);

      ret = sord_new_uri_counted(
        world, abs_uri_node.buf, abs_uri_node.n_bytes, abs_uri_node.n_chars);

      serd_node_free(&abs_uri_node);
      return ret;
    }
  case SERD_CURIE: {
    SerdChunk uri_prefix;
    SerdChunk uri_suffix;
    if (serd_env_expand(env, node, &uri_prefix, &uri_suffix)) {
      error(
        world, SERD_ERR_BAD_CURIE, "failed to expand CURIE `%s'\n", node->buf);
      return NULL;
    }
    const size_t uri_len = uri_prefix.len + uri_suffix.len;
    uint8_t*     buf     = (uint8_t*)malloc(uri_len + 1);
    memcpy(buf, uri_prefix.buf, uri_prefix.len);
    memcpy(buf + uri_prefix.len, uri_suffix.buf, uri_suffix.len);
    buf[uri_len] = '\0';

    ret =
      sord_new_uri_counted(world, buf, uri_len, serd_strlen(buf, NULL, NULL));

    free(buf);
    return ret;
  }
  case SERD_BLANK:
    return sord_new_blank_counted(
      world, node->buf, node->n_bytes, node->n_chars);
  }
  return NULL;
}

SordNode*
sord_node_from_serd_node(SordWorld*      world,
                         SerdEnv*        env,
                         const SerdNode* node,
                         const SerdNode* datatype,
                         const SerdNode* lang)
{
  return (node && env)
           ? sord_node_from_serd_node_internal(world, env, node, datatype, lang)
           : NULL;
}

const SerdNode*
sord_node_to_serd_node(const SordNode* node)
{
  return node ? &node->node : &SERD_NODE_NULL;
}

void
sord_node_free(SordWorld* world, SordNode* node)
{
  if (!node) {
    return;
  } else if (node->refs == 0) {
    error(world, SERD_ERR_BAD_ARG, "attempt to free garbage node\n");
  } else if (--node->refs == 0) {
    sord_node_free_internal(world, node);
  }
}

SordNode*
sord_node_copy(const SordNode* node)
{
  SordNode* copy = (SordNode*)node;
  if (copy) {
    ++copy->refs;
  }
  return copy;
}

static inline bool
sord_add_to_index(SordModel* model, const SordNode** tup, SordOrder order)
{
  return !zix_btree_insert(model->indices[order], tup);
}

bool
sord_add(SordModel* model, const SordQuad tup)
{
  SORD_WRITE_LOG("Add " TUP_FMT "\n", TUP_FMT_ARGS(tup));
  if (!tup[0] || !tup[1] || !tup[2]) {
    error(
      model->world, SERD_ERR_BAD_ARG, "attempt to add quad with NULL field\n");
    return false;
  } else if (model->n_iters > 0) {
    error(model->world, SERD_ERR_BAD_ARG, "added tuple during iteration\n");
  }

  const SordNode** quad = (const SordNode**)malloc(sizeof(SordQuad));
  memcpy(quad, tup, sizeof(SordQuad));

  for (unsigned i = 0; i < NUM_ORDERS; ++i) {
    if (model->indices[i] && (i < GSPO || tup[3])) {
      if (!sord_add_to_index(model, quad, (SordOrder)i)) {
        assert(i == 0); // Assuming index coherency
        free(quad);
        return false; // Quad already stored, do nothing
      }
    }
  }

  for (uint8_t i = 0U; i < TUP_LEN; ++i) {
    sord_add_quad_ref(model, tup[i], (SordQuadIndex)i);
  }

  ++model->n_quads;
  return true;
}

void
sord_remove(SordModel* model, const SordQuad tup)
{
  SORD_WRITE_LOG("Remove " TUP_FMT "\n", TUP_FMT_ARGS(tup));
  if (model->n_iters > 0) {
    error(model->world, SERD_ERR_BAD_ARG, "remove with iterator\n");
  }

  SordNode* quad = NULL;
  for (unsigned i = 0; i < NUM_ORDERS; ++i) {
    if (model->indices[i] && (i < GSPO || tup[3])) {
      ZixBTreeIter r = zix_btree_end_iter;
      if (zix_btree_remove(model->indices[i], tup, (void**)&quad, &r)) {
        assert(i == 0); // Assuming index coherency
        return;         // Quad not found, do nothing
      }
    }
  }

  free(quad);

  for (uint8_t i = 0U; i < TUP_LEN; ++i) {
    sord_drop_quad_ref(model, tup[i], (SordQuadIndex)i);
  }

  --model->n_quads;
}

SerdStatus
sord_erase(SordModel* model, SordIter* iter)
{
  if (!iter) {
    return SERD_SUCCESS;
  }

  if (model->n_iters > 1) {
    error(model->world, SERD_ERR_BAD_ARG, "erased with many iterators\n");
    return SERD_ERR_BAD_ARG;
  }

  SordQuad tup;
  sord_iter_get(iter, tup);

  SORD_WRITE_LOG("Remove " TUP_FMT "\n", TUP_FMT_ARGS(tup));

  SordNode* quad = NULL;
  for (unsigned i = 0; i < NUM_ORDERS; ++i) {
    if (model->indices[i] && (i < GSPO || tup[3])) {
      ZixBTreeIter r = zix_btree_end_iter;
      if (zix_btree_remove(model->indices[i],
                           tup,
                           (void**)&quad,
                           (SordOrder)i == iter->order ? &iter->cur : &r)) {
        return (i == 0) ? SERD_ERR_NOT_FOUND : SERD_ERR_INTERNAL;
      }
    }
  }
  iter->end = zix_btree_iter_is_end(iter->cur);
  sord_iter_scan_next(iter);

  free(quad);

  for (uint8_t i = 0U; i < TUP_LEN; ++i) {
    sord_drop_quad_ref(model, tup[i], (SordQuadIndex)i);
  }

  --model->n_quads;
  return SERD_SUCCESS;
}
