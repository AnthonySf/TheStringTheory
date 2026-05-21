// Copyright 2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef LILV_TYPE_SKIMMER_H
#define LILV_TYPE_SKIMMER_H

#include "load_skimmer.h"
#include "node_hash.h"
#include "uris.h"

#include <serd/serd.h>
#include <sord/sord.h>
#include <zix/attributes.h>

/// A LoadSkimmer that skims types of interest from manifest and spec data
typedef struct TypeSkimmerImpl {
  LoadSkimmer                          base;
  const LilvURIs* ZIX_NONNULL          uris;
  NodeHash* ZIX_NULLABLE* ZIX_NULLABLE plugins;
  NodeHash* ZIX_NULLABLE* ZIX_NULLABLE presets;
  NodeHash* ZIX_NULLABLE* ZIX_NULLABLE specs;
  NodeHash* ZIX_NULLABLE* ZIX_NULLABLE replaced;
  SordModel* ZIX_NULLABLE              applications;
  SordModel* ZIX_NULLABLE              subclasses;
} TypeSkimmer;

TypeSkimmer* ZIX_ALLOCATED
type_skimmer_new(SordWorld* ZIX_NONNULL               world,
                 const LilvURIs* ZIX_NONNULL          uris,
                 const SerdNode* ZIX_NONNULL          base,
                 SordModel* ZIX_NONNULL               model,
                 NodeHash* ZIX_NULLABLE* ZIX_NULLABLE plugins,
                 NodeHash* ZIX_NULLABLE* ZIX_NULLABLE presets,
                 NodeHash* ZIX_NULLABLE* ZIX_NULLABLE specs,
                 NodeHash* ZIX_NULLABLE* ZIX_NULLABLE replaced,
                 SordModel* ZIX_NULLABLE              applications,
                 SordModel* ZIX_NULLABLE              subclasses);

void
type_skimmer_free(TypeSkimmer* ZIX_NULLABLE skimmer);

#endif // LILV_TYPE_SKIMMER_H
