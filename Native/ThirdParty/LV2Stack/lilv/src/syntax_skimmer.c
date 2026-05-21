// Copyright 2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "syntax_skimmer.h"

#include <serd/serd.h>
#include <sord/sord.h>
#include <zix/allocator.h>
#include <zix/attributes.h>

#include <stddef.h>

static SerdStatus
on_base(SyntaxSkimmer* const skimmer, const SerdNode* const uri)
{
  return serd_env_set_base_uri(skimmer->env, uri);
}

static SerdStatus
on_prefix(SyntaxSkimmer* const  skimmer,
          const SerdNode* const name,
          const SerdNode* const uri)
{
  return serd_env_set_prefix(skimmer->env, name, uri);
}

static SerdStatus
on_statement(SyntaxSkimmer* const     skimmer,
             const SerdStatementFlags flags,
             const SerdNode* const    graph,
             const SerdNode* const    subject,
             const SerdNode* const    predicate,
             const SerdNode* const    object,
             const SerdNode* const    object_datatype,
             const SerdNode* const    object_lang)
{
  (void)flags;
  (void)graph;

  SerdStatus st = SERD_SUCCESS;

  // Get the node from this statement that corresponds to our topic field
  const SerdNode* const topic =
    ((skimmer->topic_field == SORD_SUBJECT)     ? subject
     : (skimmer->topic_field == SORD_PREDICATE) ? predicate
                                                : object);

  // Call skim function for any statements about our topic
  if (topic->type == SERD_CURIE || topic->type == SERD_URI) {
    SerdNode topic_uri = serd_env_expand_node(skimmer->env, topic);
    if (serd_node_equals(&topic_uri, skimmer->topic)) {
      // Set the topic field to the expanded version
      const SerdNode* nodes[5] = {
        subject, predicate, object, object_datatype, object_lang};
      nodes[skimmer->topic_field] = &topic_uri;

      st = skimmer->skim(skimmer->skim_handle,
                         skimmer->env,
                         nodes[0],
                         nodes[1],
                         nodes[2],
                         nodes[3],
                         nodes[4]);
    }
    serd_node_free(&topic_uri);
  }

  return st;
}

SyntaxSkimmer* ZIX_ALLOCATED
syntax_skimmer_new(ZixAllocator* const     allocator,
                   SerdEnv* const          env,
                   const SordQuadIndex     topic_field,
                   const SerdNode* const   topic,
                   void* const             skim_handle,
                   const SyntaxSkimmerFunc skim)
{
  SyntaxSkimmer* const skimmer =
    (SyntaxSkimmer*)zix_malloc(allocator, sizeof(SyntaxSkimmer));

  SerdReader* const reader = serd_reader_new(SERD_TURTLE,
                                             skimmer,
                                             NULL,
                                             (SerdBaseSink)on_base,
                                             (SerdPrefixSink)on_prefix,
                                             (SerdStatementSink)on_statement,
                                             NULL);
  if (!reader) {
    zix_free(allocator, skimmer);
    return NULL;
  }

  if (skimmer) {
    skimmer->allocator   = allocator;
    skimmer->env         = env;
    skimmer->reader      = reader;
    skimmer->topic_field = topic_field;
    skimmer->topic       = topic;
    skimmer->skim_handle = skim_handle;
    skimmer->skim        = skim;
  }

  return skimmer;
}

void
syntax_skimmer_free(SyntaxSkimmer* const skimmer)
{
  if (skimmer) {
    serd_reader_free(skimmer->reader);
    zix_free(skimmer->allocator, skimmer);
  }
}
