// Copyright 2011-2019 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <lv2/core/lv2.h>

#include <stdint.h>
#include <stdlib.h>

static const LV2_Descriptor descriptor = {
  "http://example.org/not-the-plugin-you-are-looking-for",
  NULL,
  NULL,
  NULL,
  NULL,
  NULL,
  NULL,
  NULL};

LV2_SYMBOL_EXPORT
const LV2_Descriptor*
lv2_descriptor(uint32_t index)
{
  if (index == 0) {
    return &descriptor;
  }

  return NULL;
}
