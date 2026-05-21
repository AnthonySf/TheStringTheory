// Copyright 2022 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_PATH_ITER_H
#define ZIX_PATH_ITER_H

#include "index_range.h"

#include <zix/attributes.h>

typedef enum {
  ZIX_PATH_ROOT_NAME,
  ZIX_PATH_ROOT_DIRECTORY,
  ZIX_PATH_FILE_NAME,
  ZIX_PATH_END,
} ZixPathIterState;

typedef struct {
  ZixIndexRange    range;
  ZixPathIterState state;
} ZixPathIter;

ZIX_PURE_FUNC ZixPathIter
zix_path_begin(const char* ZIX_NULLABLE path);

ZIX_PURE_FUNC ZixPathIter
zix_path_next(const char* ZIX_NONNULL path, ZixPathIter iter);

#endif // ZIX_PATH_ITER_H
