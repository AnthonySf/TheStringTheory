// Copyright 2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "forge_test_object.h"
#include "test_uri_map.h"

#include <lv2/atom/atom.h>
#include <lv2/atom/forge.h>
#include <lv2/urid/urid.h>
#include <serd/serd.h>
#include <sratom/sratom.h>

#include <assert.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#define NS_EG "http://example.org/"

#define USTR(s) ((const uint8_t*)(s))

typedef struct {
  unsigned fail_on;  ///< Index of statement/end to fail on
  unsigned n_events; ///< Number of statements so far
} WriteContext;

static SerdStatus
on_statement(void* const              handle,
             const SerdStatementFlags flags,
             const SerdNode* const    graph,
             const SerdNode* const    subject,
             const SerdNode* const    predicate,
             const SerdNode* const    object,
             const SerdNode* const    object_datatype,
             const SerdNode* const    object_lang)
{
  (void)flags;
  (void)graph;
  (void)subject;
  (void)predicate;
  (void)object;
  (void)object_datatype;
  (void)object_lang;

  WriteContext* const ctx = (WriteContext*)handle;

  return (ctx->n_events++ == ctx->fail_on) ? SERD_ERR_BAD_WRITE : SERD_SUCCESS;
}

static SerdStatus
on_end(void* const handle, const SerdNode* const node)
{
  (void)handle;
  (void)node;

  WriteContext* const ctx = (WriteContext*)handle;

  return (ctx->n_events++ == ctx->fail_on) ? SERD_ERR_BAD_WRITE : SERD_SUCCESS;
}

static void
check_turtle(Sratom* const         sratom,
             LV2_URID_Unmap* const unmap,
             const LV2_Atom* const atom,
             const char* const     expected)
{
  const SerdNode s = serd_node_from_string(SERD_URI, USTR(NS_EG "s"));
  const SerdNode p = serd_node_from_string(SERD_URI, USTR(NS_EG "p"));

  char* const ttl = sratom_to_turtle(
    sratom, unmap, NS_EG, &s, &p, atom->type, atom->size, LV2_ATOM_BODY(atom));

  assert(ttl);
  assert(!strcmp(ttl, expected));
  free(ttl);
}

// Not covered by round-trip test because it comes back as a string
static void
test_bare_literal(void)
{
  Uris           uris   = {NULL, 0};
  LV2_URID_Map   map    = {&uris, urid_map};
  LV2_URID_Unmap unmap  = {&uris, urid_unmap};
  Sratom* const  sratom = sratom_new(&map);

  LV2_Atom_Forge forge;
  LV2_Atom       buf[8];
  lv2_atom_forge_init(&forge, &map);
  lv2_atom_forge_set_buffer(&forge, (uint8_t*)buf, sizeof(buf));
  lv2_atom_forge_literal(&forge, "test", 4, 0, 0);

  check_turtle(sratom,
               &unmap,
               buf,
               "<http://example.org/s>\n\t<http://example.org/p> \"test\" .\n");

  sratom_free(sratom);
  free_uris(&uris);
}

// Not covered by round-trip test because it comes back as a URID
static void
test_uri(void)
{
  Uris           uris   = {NULL, 0};
  LV2_URID_Map   map    = {&uris, urid_map};
  LV2_URID_Unmap unmap  = {&uris, urid_unmap};
  Sratom* const  sratom = sratom_new(&map);

  LV2_Atom_Forge forge;
  LV2_Atom       buf[8];
  lv2_atom_forge_init(&forge, &map);
  lv2_atom_forge_set_buffer(&forge, (uint8_t*)buf, sizeof(buf));
  lv2_atom_forge_uri(&forge, NS_EG "o", strlen(NS_EG "o"));

  check_turtle(sratom,
               &unmap,
               buf,
               "<http://example.org/s>\n\t<http://example.org/p> "
               "<http://example.org/o> .\n");

  sratom_free(sratom);
  free_uris(&uris);
}

static void
test_bad_language(void)
{
  Uris           uris   = {NULL, 0};
  LV2_URID_Map   map    = {&uris, urid_map};
  LV2_URID_Unmap unmap  = {&uris, urid_unmap};
  const LV2_URID lang   = urid_map(&uris, NS_EG "l");
  WriteContext   ctx    = {0, 0};
  Sratom* const  sratom = sratom_new(&map);

  sratom_set_sink(sratom, NS_EG, on_statement, on_end, &ctx);

  LV2_Atom_Forge forge;
  LV2_Atom       buf[8];
  lv2_atom_forge_init(&forge, &map);
  lv2_atom_forge_set_buffer(&forge, (uint8_t*)buf, sizeof(buf));
  lv2_atom_forge_literal(&forge, "test", 4, 0, lang);

  const SerdNode        s   = serd_node_from_string(SERD_URI, USTR(NS_EG "s"));
  const SerdNode        p   = serd_node_from_string(SERD_URI, USTR(NS_EG "p"));
  const LV2_Atom* const obj = buf;
  assert(
    sratom_write(
      sratom, &unmap, 0U, &s, &p, obj->type, obj->size, LV2_ATOM_BODY(obj)) ==
    SERD_ERR_BAD_ARG);

  assert(!ctx.n_events);
  sratom_free(sratom);
  free_uris(&uris);
}

static void
test_bad_vector_child_size(void)
{
  Uris           uris        = {NULL, 0};
  LV2_URID_Map   map         = {&uris, urid_map};
  LV2_URID_Unmap unmap       = {&uris, urid_unmap};
  const LV2_URID atom_Int    = map.map(map.handle, LV2_ATOM__Int);
  const LV2_URID atom_Vector = map.map(map.handle, LV2_ATOM__Vector);
  WriteContext   ctx         = {0, 0};
  Sratom* const  sratom      = sratom_new(&map);

  sratom_set_sink(sratom, NS_EG, on_statement, on_end, &ctx);

  const SerdNode        s   = serd_node_from_string(SERD_URI, USTR(NS_EG "s"));
  const SerdNode        p   = serd_node_from_string(SERD_URI, USTR(NS_EG "p"));
  const LV2_Atom_Vector vec = {{sizeof(LV2_Atom_Vector), atom_Vector},
                               {0U, atom_Int}};

  assert(sratom_write(sratom,
                      &unmap,
                      0U,
                      &s,
                      &p,
                      vec.atom.type,
                      vec.atom.size,
                      LV2_ATOM_BODY(&vec)) == SERD_ERR_BAD_ARG);

  assert(!ctx.n_events);
  sratom_free(sratom);
  free_uris(&uris);
}

static void
test_write_errors(void)
{
  Uris           uris   = {NULL, 0};
  LV2_URID_Map   map    = {&uris, urid_map};
  LV2_URID_Unmap unmap  = {&uris, urid_unmap};
  WriteContext   ctx    = {0, 0};
  Sratom* const  sratom = sratom_new(&map);

  sratom_set_sink(sratom, NS_EG, on_statement, on_end, &ctx);

  LV2_Atom_Forge forge;
  LV2_Atom       buf[192];
  lv2_atom_forge_init(&forge, &map);
  lv2_atom_forge_set_buffer(&forge, (uint8_t*)buf, sizeof(buf));
  forge_test_object(&forge, &map, &uris, 0U);

  const SerdNode s = serd_node_from_string(SERD_URI, USTR(NS_EG "s"));
  const SerdNode p = serd_node_from_string(SERD_URI, USTR(NS_EG "p"));

  for (unsigned i = 0U; i < 157; ++i) {
    ctx.fail_on  = i;
    ctx.n_events = 0;

    const int st = sratom_write(
      sratom, &unmap, 0U, &s, &p, buf->type, buf->size, LV2_ATOM_BODY(buf));

    assert(st == SERD_ERR_BAD_WRITE);
    assert(ctx.n_events == ctx.fail_on + 1U);
  }

  sratom_free(sratom);
  free_uris(&uris);
}

int
main(void)
{
  test_bare_literal();
  test_uri();
  test_bad_language();
  test_bad_vector_child_size();
  test_write_errors();
  return 0;
}
