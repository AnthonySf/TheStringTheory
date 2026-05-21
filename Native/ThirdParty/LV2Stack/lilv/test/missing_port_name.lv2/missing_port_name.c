// Copyright 2011-2019 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <lv2/core/lv2.h>

#include <stdint.h>
#include <stdlib.h>

#define PLUGIN_URI "http://example.org/missing-port-name"

enum { TEST_INPUT = 0, TEST_OUTPUT = 1 };

typedef struct {
  float* input;
  float* output;
} Test;

static void
cleanup(LV2_Handle instance)
{
  free((Test*)instance);
}

static void
connect_port(LV2_Handle instance, uint32_t port, void* data)
{
  Test* test = (Test*)instance;
  switch (port) {
  case TEST_INPUT:
    test->input = (float*)data;
    break;
  case TEST_OUTPUT:
    test->output = (float*)data;
    break;
  default:
    break;
  }
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

  Test* test = (Test*)calloc(1, sizeof(Test));
  if (!test) {
    return NULL;
  }

  return (LV2_Handle)test;
}

static void
run(LV2_Handle instance, uint32_t sample_count)
{
  (void)sample_count;

  Test* test = (Test*)instance;

  *test->output = *test->input;
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
