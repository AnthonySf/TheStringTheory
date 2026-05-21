// Copyright 2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "load_skimmer.h"

#include <serd/serd.h>
#include <sord/sord.h>

#include <stddef.h>

static SerdStatus
on_base(LoadSkimmer* const skimmer, const SerdNode* const uri)
{
  return serd_env_set_base_uri(skimmer->env, uri);
}

static SerdStatus
on_prefix(LoadSkimmer* const    skimmer,
          const SerdNode* const name,
          const SerdNode* const uri)
{
  return serd_env_set_prefix(skimmer->env, name, uri);
}

static SerdStatus
on_statement(LoadSkimmer* const       skimmer,
             const SerdStatementFlags flags,
             const SerdNode* const    graph,
             const SerdNode* const    subject,
             const SerdNode* const    predicate,
             const SerdNode* const    object,
             const SerdNode* const    object_datatype,
             const SerdNode* const    object_lang)
{
  (void)flags;

  SordWorld* world = skimmer->world;
  SerdEnv*   env   = skimmer->env;

  SordNode* g = sord_node_from_serd_node(world, env, graph, NULL, NULL);
  SordNode* s = sord_node_from_serd_node(world, env, subject, NULL, NULL);
  SordNode* p = sord_node_from_serd_node(world, env, predicate, NULL, NULL);
  SordNode* o =
    sord_node_from_serd_node(world, env, object, object_datatype, object_lang);

  if (!s || !p || !o) {
    return SERD_ERR_BAD_ARG;
  }

  // Call skim function and add statement to model if it wasn't dropped
  SerdStatus st = skimmer->skim(skimmer->skim_handle, s, p, o);
  if (!st) {
    const SordQuad tup = {s, p, o, g};
    sord_add(skimmer->model, tup);
  }

  sord_node_free(world, o);
  sord_node_free(world, p);
  sord_node_free(world, s);
  sord_node_free(world, g);

  return (st > SERD_FAILURE) ? st : SERD_SUCCESS;
}

void
load_skimmer_init(LoadSkimmer* const    skimmer,
                  SordWorld* const      world,
                  SerdEnv* const        env,
                  SordModel* const      model,
                  void* const           skim_handle,
                  const LoadSkimmerFunc skim)
{
  skimmer->world = world;
  skimmer->env   = env;
  skimmer->model = model;

  skimmer->reader = serd_reader_new(SERD_TURTLE,
                                    skimmer,
                                    NULL,
                                    (SerdBaseSink)on_base,
                                    (SerdPrefixSink)on_prefix,
                                    (SerdStatementSink)on_statement,
                                    NULL);

  skimmer->skim_handle = skim_handle;
  skimmer->skim        = skim;
}

void
load_skimmer_cleanup(LoadSkimmer* const skimmer)
{
  serd_reader_free(skimmer->reader);
  skimmer->reader = NULL;
}
