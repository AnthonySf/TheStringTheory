// Copyright 2012-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include "forge_test_object.h"
#include "test_uri_map.h"

#include <lv2/atom/atom.h>
#include <lv2/atom/forge.h>
#include <lv2/atom/util.h>
#include <lv2/urid/urid.h>
#include <serd/serd.h>
#include <sratom/sratom.h>

#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define NS_RDF "http://www.w3.org/1999/02/22-rdf-syntax-ns#"

#define USTR(s) ((const uint8_t*)(s))

static int
test_fail(const char* const msg)
{
  fprintf(stderr, "error: %s\n", msg);
  return 1;
}

static int
test(SerdEnv* env, const char* base_uri, bool top_level, bool pretty_numbers)
{
  Uris           uris  = {NULL, 0};
  LV2_URID_Map   map   = {&uris, urid_map};
  LV2_URID_Unmap unmap = {&uris, urid_unmap};
  LV2_Atom_Forge forge;
  lv2_atom_forge_init(&forge, &map);

  Sratom* sratom = sratom_new(&map);
  sratom_set_env(sratom, env);
  sratom_set_pretty_numbers(sratom, pretty_numbers);
  sratom_set_object_mode(sratom,
                         top_level ? SRATOM_OBJECT_MODE_BLANK_SUBJECT
                                   : SRATOM_OBJECT_MODE_BLANK);

  LV2_Atom buf[144];
  lv2_atom_forge_set_buffer(&forge, (uint8_t*)buf, sizeof(buf));

  const char* const obj_uri = "http://example.org/obj";
  const LV2_URID    obj_id  = urid_map(&uris, obj_uri);
  forge_test_object(&forge, &map, &uris, top_level ? obj_id : 0U);

  SerdNode s = serd_node_from_string(SERD_URI, USTR("http://example.org/obj"));
  SerdNode p = serd_node_from_string(SERD_URI, USTR(NS_RDF "value"));

  const SerdNode* subj = top_level ? NULL : &s;
  const SerdNode* pred = top_level ? NULL : &p;

  char* outstr = sratom_to_turtle(sratom,
                                  &unmap,
                                  base_uri,
                                  subj,
                                  pred,
                                  buf->type,
                                  buf->size,
                                  LV2_ATOM_BODY(buf));

  printf("# Atom => Turtle\n\n%s", outstr);

  LV2_Atom* parsed = NULL;
  if (top_level) {
    SerdNode o = serd_node_from_string(SERD_URI, (const uint8_t*)obj_uri);
    parsed     = sratom_from_turtle(sratom, base_uri, &o, NULL, outstr);
  } else {
    parsed = sratom_from_turtle(sratom, base_uri, subj, pred, outstr);
  }

  if (!pretty_numbers) {
    if (!lv2_atom_equals(buf, parsed)) {
      return test_fail("Parsed atom does not match original");
    }

    char* instr = sratom_to_turtle(sratom,
                                   &unmap,
                                   base_uri,
                                   subj,
                                   pred,
                                   parsed->type,
                                   parsed->size,
                                   LV2_ATOM_BODY(parsed));
    printf("# Turtle => Atom\n\n%s", instr);

    if (!!strcmp(outstr, instr)) {
      return test_fail("Re-serialized string differs from original");
    }
    free(instr);
  }

  printf("All tests passed.\n");

  free(parsed);
  free(outstr);
  sratom_free(sratom);
  free_uris(&uris);
  return 0;
}

static int
test_env(SerdEnv* env)
{
  if (test(env, "file:///tmp/base/", false, false) || //
      test(env, "file:///tmp/base/", true, false) ||  //
      test(env, "file:///tmp/base/", false, true) ||  //
      test(env, "file:///tmp/base/", true, true) ||   //
      test(env, "http://example.org/", true, true)) {
    return 1;
  }

  return 0;
}

int
main(void)
{
  // Test with no environment
  if (test_env(NULL)) {
    return 1;
  }

  // Test with a prefix defined
  SerdEnv* env = serd_env_new(NULL);
  serd_env_set_prefix_from_strings(
    env, (const uint8_t*)"eg", (const uint8_t*)"http://example.org/");

  test_env(env);
  serd_env_free(env);

  return 0;
}
