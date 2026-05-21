// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "query.h"
#include "lilv_internal.h"
#include "node_hash.h"

#include <lilv/lilv.h>
#include <sord/sord.h>
#include <zix/tree.h>

#include <string.h>

typedef enum {
  LILV_LANG_MATCH_NONE,    ///< Language does not match at all
  LILV_LANG_MATCH_PARTIAL, ///< Partial (language, but not country) match
  LILV_LANG_MATCH_EXACT    ///< Exact (language and country) match
} LilvLangMatch;

static LilvLangMatch
lilv_lang_matches(const char* a, const char* b)
{
  if (!a || !b) {
    return LILV_LANG_MATCH_NONE;
  }

  if (!strcmp(a, b)) {
    return LILV_LANG_MATCH_EXACT;
  }

  const char*  a_dash     = strchr(a, '-');
  const size_t a_lang_len = a_dash ? (size_t)(a_dash - a) : strlen(a);
  const char*  b_dash     = strchr(b, '-');
  const size_t b_lang_len = b_dash ? (size_t)(b_dash - b) : strlen(b);

  if (a_lang_len == b_lang_len && !strncmp(a, b, a_lang_len)) {
    return LILV_LANG_MATCH_PARTIAL;
  }

  return LILV_LANG_MATCH_NONE;
}

static LilvNodes*
lilv_nodes_from_matches_i18n(LilvWorld* const world, SordIter* const stream)
{
  LilvNodes*      values  = lilv_nodes_new();
  const SordNode* partial = NULL; // Partial language match
  const char*     syslang = world->lang;
  FOREACH_MATCH (stream) {
    const SordNode* value = sord_iter_get_node(stream, SORD_OBJECT);
    if (sord_node_get_type(value) == SORD_LITERAL) {
      const char* lang = sord_node_get_language(value);

      if (!lang) {
        if (!partial) {
          partial = value;
        }
      } else {
        switch (lilv_lang_matches(lang, syslang)) {
        case LILV_LANG_MATCH_EXACT:
          // Exact language match, add to results
          zix_tree_insert(
            (ZixTree*)values, lilv_node_new_from_node(world, value), NULL);
          break;
        case LILV_LANG_MATCH_PARTIAL:
          // Partial language match, save in case we find no exact
          partial = value;
          break;
        case LILV_LANG_MATCH_NONE:
          break;
        }
      }
    } else {
      zix_tree_insert(
        (ZixTree*)values, lilv_node_new_from_node(world, value), NULL);
    }
  }
  sord_iter_free(stream);

  if (lilv_nodes_size(values) > 0) {
    return values;
  }

  if (partial) {
    zix_tree_insert(
      (ZixTree*)values, lilv_node_new_from_node(world, partial), NULL);
  } else {
    // No matches whatsoever
    lilv_nodes_free(values);
    values = NULL;
  }

  return values;
}

static LilvNodes*
lilv_nodes_from_matches_all(LilvWorld* const    world,
                            SordIter* const     stream,
                            const SordQuadIndex field)
{
  LilvNodes* const values = lilv_nodes_new();
  FOREACH_MATCH (stream) {
    const SordNode* value = sord_iter_get_node(stream, field);
    LilvNode*       node  = lilv_node_new_from_node(world, value);
    if (node) {
      zix_tree_insert((ZixTree*)values, node, NULL);
    }
  }
  sord_iter_free(stream);
  return values;
}

LilvNode*
lilv_node_from_object(LilvWorld* const      world,
                      const SordNode* const s,
                      const SordNode* const p)
{
  SordIter* const i = sord_search(world->model, s, p, NULL, NULL);
  if (sord_iter_end(i)) {
    sord_iter_free(i);
    return NULL;
  }

  const char* const syslang = world->lang;
  const SordNode*   best    = NULL;
  const SordNode*   partial = NULL;
  FOREACH_MATCH (i) {
    const SordNode* const node = sord_iter_get_node(i, SORD_OBJECT);
    if (sord_node_get_type(node) != SORD_LITERAL) {
      best = node;
      break; // Treat a non-literal as an exact match
    }

    const char* lang = sord_node_get_language(node);
    if (!lang) {
      if (!partial) {
        partial = node;
      }
    } else {
      const LilvLangMatch match = lilv_lang_matches(lang, syslang);
      if (match == LILV_LANG_MATCH_PARTIAL) {
        partial = node;
      } else if (match == LILV_LANG_MATCH_EXACT) {
        best = node;
        break;
      }
    }
  }

  sord_iter_free(i);

  return lilv_node_new_from_node(world, best ? best : partial);
}

LilvNodes*
lilv_nodes_from_matches(LilvWorld* const      world,
                        const SordNode* const s,
                        const SordNode* const p,
                        const SordNode* const o,
                        const SordNode* const g)
{
  SordIter* const stream = sord_search(world->model, s, p, o, g);
  if (sord_iter_end(stream)) {
    sord_iter_free(stream);
    return NULL;
  }

  const SordQuadIndex field = o ? SORD_SUBJECT : SORD_OBJECT;

  return (field == SORD_OBJECT && world->opt.filter_lang)
           ? lilv_nodes_from_matches_i18n(world, stream)
           : lilv_nodes_from_matches_all(world, stream, field);
}

NodeHash*
lilv_hash_from_matches(SordModel* const      model,
                       const SordNode* const s,
                       const SordNode* const p,
                       const SordNode* const o,
                       const SordNode* const g)
{
  NodeHash*       hash = NULL;
  SordIter* const i    = sord_search(model, s, p, o, g);
  if (!sord_iter_end(i)) {
    if ((hash = lilv_node_hash_new(NULL))) {
      const SordQuadIndex field = s ? SORD_OBJECT : SORD_SUBJECT;
      FOREACH_MATCH (i) {
        lilv_node_hash_insert_copy(hash, sord_iter_get_node(i, field));
      }
    }
  }

  sord_iter_free(i);
  return hash;
}
