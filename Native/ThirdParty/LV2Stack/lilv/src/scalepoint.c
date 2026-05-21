// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "lilv_internal.h"

#include <lilv/lilv.h>
#include <sord/sord.h>

#include <stdlib.h>

LilvScalePoint*
lilv_scale_point_new(LilvWorld* const      world,
                     const SordNode* const value,
                     const SordNode* const label)
{
  LilvScalePoint* point = (LilvScalePoint*)malloc(sizeof(LilvScalePoint));
  point->value          = lilv_node_new_from_node(world, value);
  point->label          = lilv_node_new_from_node(world, label);
  return point;
}

void
lilv_scale_point_free(LilvScalePoint* point)
{
  if (point) {
    lilv_node_free(point->value);
    lilv_node_free(point->label);
    free(point);
  }
}

const LilvNode*
lilv_scale_point_get_value(const LilvScalePoint* point)
{
  return point->value;
}

const LilvNode*
lilv_scale_point_get_label(const LilvScalePoint* point)
{
  return point->label;
}
