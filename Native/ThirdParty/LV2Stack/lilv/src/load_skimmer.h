// Copyright 2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef LILV_LOAD_SKIMMER_H
#define LILV_LOAD_SKIMMER_H

#include <serd/serd.h>
#include <sord/sord.h>
#include <zix/attributes.h>

/// A function to skim interned input before it's inserted
typedef SerdStatus (*LoadSkimmerFunc)( //
  void* SERD_UNSPECIFIED      handle,
  const SordNode* ZIX_NONNULL subject,
  const SordNode* ZIX_NONNULL predicate,
  const SordNode* ZIX_NONNULL object);

/**
   An inserter that skims interned input.

   This filters input like SyntaxSkimmer, but using the interned node pointers
   before they're inserted into a model (making filter conditions faster since
   node equality is a simple pointer comparison).
*/
typedef struct LoadSkimmerImpl {
  SordWorld* ZIX_NONNULL      world;
  SerdEnv* ZIX_NONNULL        env;
  SordModel* ZIX_NONNULL      model;
  SerdReader* ZIX_ALLOCATED   reader;
  void* ZIX_NONNULL           skim_handle;
  LoadSkimmerFunc ZIX_NONNULL skim;
} LoadSkimmer;

void
load_skimmer_init(LoadSkimmer* ZIX_NONNULL    skimmer,
                  SordWorld* ZIX_NONNULL      world,
                  SerdEnv* ZIX_NONNULL        env,
                  SordModel* ZIX_NONNULL      model,
                  void* ZIX_UNSPECIFIED       skim_handle,
                  LoadSkimmerFunc ZIX_NONNULL skim);

void
load_skimmer_cleanup(LoadSkimmer* ZIX_NONNULL skimmer);

#endif // LILV_LOAD_SKIMMER_H
