// Copyright 2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef LILV_SYNTAX_SKIMMER_H
#define LILV_SYNTAX_SKIMMER_H

#include <serd/serd.h>
#include <sord/sord.h>
#include <zix/allocator.h>
#include <zix/attributes.h>

/// A function to skim input while it's being read
typedef SerdStatus (*SyntaxSkimmerFunc)( //
  void* SERD_UNSPECIFIED      skim_handle,
  SerdEnv* ZIX_NONNULL        env,
  const SerdNode* ZIX_NONNULL subject,
  const SerdNode* ZIX_NONNULL predicate,
  const SerdNode* ZIX_NONNULL object,
  const SerdNode* ZIX_NONNULL object_datatype,
  const SerdNode* ZIX_NONNULL object_lang);

/**
   A reader that skims statements.

   This filters syntactic input for statements about some "topic" (a fixed
   subject, predicate, or object) and calls the provided `skim` function with
   any matching statements.
*/
typedef struct SyntaxSkimmerImpl {
  ZixAllocator* ZIX_NULLABLE    allocator;
  SerdEnv* ZIX_NONNULL          env;
  SerdReader* ZIX_ALLOCATED     reader;
  SordQuadIndex                 topic_field;
  const SerdNode* ZIX_ALLOCATED topic;
  const SerdNode* ZIX_ALLOCATED base;
  void* ZIX_UNSPECIFIED         skim_handle;
  SyntaxSkimmerFunc ZIX_NONNULL skim;
} SyntaxSkimmer;

/// Create a new syntax skimmer
SyntaxSkimmer* ZIX_ALLOCATED
syntax_skimmer_new(ZixAllocator* ZIX_NULLABLE    allocator,
                   SerdEnv* ZIX_NONNULL          env,
                   SordQuadIndex                 topic_field,
                   const SerdNode* ZIX_NONNULL   topic,
                   void* ZIX_UNSPECIFIED         skim_handle,
                   SyntaxSkimmerFunc ZIX_NONNULL skim);

/// Clean up after syntax_skimmer_new()
void
syntax_skimmer_free(SyntaxSkimmer* ZIX_NULLABLE skimmer);

#endif // LILV_SYNTAX_SKIMMER_H
