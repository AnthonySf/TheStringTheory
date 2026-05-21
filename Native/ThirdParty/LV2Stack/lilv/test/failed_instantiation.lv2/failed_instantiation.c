// Copyright 2011-2019 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <lv2/core/lv2.h>

#include <stdint.h>
#include <stdlib.h>

#define PLUGIN_URI "http://example.org/failed-instantiation"

static void
cleanup(LV2_Handle instance)
{
  (void)instance;
}

static void
connect_port(LV2_Handle instance, uint32_t port, void* data)
{
  (void)instance;
  (void)port;
  (void)data;
}

static LV2_Handle
instantiate(const LV2_Descriptor*     descriptor,
            double                    rate,
            const char*               path,
            const LV2_Feature* const* features)
{
  (void)descriptor;
  (void)rate;
  (void)path;
  (void)features;

  return NULL;
}

static void
run(LV2_Handle instance, uint32_t sample_count)
{
  (void)instance;
  (void)sample_count;
}

static const LV2_Descriptor descriptor = {
  PLUGIN_URI,
  instantiate,
  connect_port,
  NULL, // activate,
  run,
  NULL, // deactivate,
  cleanup,
  NULL // extension_data
};

LV2_SYMBOL_EXPORT
const LV2_Descriptor*
lv2_descriptor(uint32_t index)
{
  return (index == 0) ? &descriptor : NULL;
}
