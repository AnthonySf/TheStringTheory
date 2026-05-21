// Copyright 2011-2019 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <lv2/core/lv2.h>

#include <stdlib.h>

LV2_SYMBOL_EXPORT
const LV2_Lib_Descriptor*
lv2_lib_descriptor(const char* bundle_path, const LV2_Feature* const* features)
{
  (void)bundle_path;
  (void)features;

  return NULL;
}
