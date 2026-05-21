// Copyright 2012-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <sratom/sratom.h>

#include <lv2/atom/atom.h>
#include <lv2/atom/forge.h>
#include <lv2/atom/util.h>
#include <lv2/midi/midi.h>
#include <lv2/urid/urid.h>
#include <serd/serd.h>
#include <sord/sord.h>

#include <assert.h>
#include <ctype.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define NS_RDF (const uint8_t*)"http://www.w3.org/1999/02/22-rdf-syntax-ns#"
#define NS_XSD (const uint8_t*)"http://www.w3.org/2001/XMLSchema#"

#define USTR(str) ((const uint8_t*)(str))

static const SerdStyle style =
  (SerdStyle)(SERD_STYLE_ABBREVIATED | SERD_STYLE_RESOLVED | SERD_STYLE_CURIED);

typedef enum { MODE_SUBJECT, MODE_BODY, MODE_SEQUENCE } ReadMode;

typedef struct {
  Sratom*            sratom;
  const SerdNode*    subject;
  const SerdNode*    predicate;
  SerdStatementFlags flags;
  uint8_t            idbuf[12];
  uint8_t            nodebuf[12];
  SerdNode           id;
  SerdNode           node;
} WriteContext;

struct SratomImpl {
  LV2_URID_Map*     map;
  LV2_Atom_Forge    forge;
  SerdEnv*          env;
  SerdNode          base_uri;
  SerdURI           base;
  SerdStatementSink write_statement;
  SerdEndSink       end_anon;
  void*             handle;
  LV2_URID          atom_Event;
  LV2_URID          atom_frameTime;
  LV2_URID          atom_beatTime;
  LV2_URID          midi_MidiEvent;
  unsigned          next_id;
  SratomObjectMode  object_mode;
  uint32_t          seq_unit;
  struct {
    SordNode* atom_childType;
    SordNode* atom_frameTime;
    SordNode* atom_beatTime;
    SordNode* rdf_first;
    SordNode* rdf_rest;
    SordNode* rdf_type;
    SordNode* rdf_value;
    SordNode* xsd_base64Binary;
  } nodes;

  bool pretty_numbers;
};

static void
read_node(Sratom*         sratom,
          LV2_Atom_Forge* forge,
          SordWorld*      world,
          SordModel*      model,
          const SordNode* node,
          ReadMode        mode);

Sratom*
sratom_new(LV2_URID_Map* map)
{
  Sratom* sratom = (Sratom*)calloc(1, sizeof(Sratom));
  if (sratom) {
    sratom->map            = map;
    sratom->atom_Event     = map->map(map->handle, LV2_ATOM__Event);
    sratom->atom_frameTime = map->map(map->handle, LV2_ATOM__frameTime);
    sratom->atom_beatTime  = map->map(map->handle, LV2_ATOM__beatTime);
    sratom->midi_MidiEvent = map->map(map->handle, LV2_MIDI__MidiEvent);
    sratom->object_mode    = SRATOM_OBJECT_MODE_BLANK;
    lv2_atom_forge_init(&sratom->forge, map);
  }
  return sratom;
}

void
sratom_free(Sratom* sratom)
{
  if (sratom) {
    serd_node_free(&sratom->base_uri);
    free(sratom);
  }
}

void
sratom_set_env(Sratom* sratom, SerdEnv* env)
{
  sratom->env = env;
}

void
sratom_set_sink(Sratom*           sratom,
                const char*       base_uri,
                SerdStatementSink sink,
                SerdEndSink       end_sink,
                void*             handle)
{
  if (base_uri) {
    serd_node_free(&sratom->base_uri);
    sratom->base_uri =
      serd_node_new_uri_from_string(USTR(base_uri), NULL, &sratom->base);
  }
  sratom->write_statement = sink;
  sratom->end_anon        = end_sink;
  sratom->handle          = handle;
}

void
sratom_set_pretty_numbers(Sratom* sratom, bool pretty_numbers)
{
  sratom->pretty_numbers = pretty_numbers;
}

void
sratom_set_object_mode(Sratom* sratom, SratomObjectMode object_mode)
{
  sratom->object_mode = object_mode;
}

static void
gensym(SerdNode* out, char c, unsigned num)
{
  out->n_bytes = out->n_chars = snprintf((char*)out->buf, 12, "%c%u", c, num);
}

static SerdStatus
list_append(Sratom*         sratom,
            LV2_URID_Unmap* unmap,
            unsigned*       flags,
            SerdNode*       s,
            SerdNode*       p,
            SerdNode*       node,
            uint32_t        size,
            uint32_t        type,
            const void*     body)
{
  int st = 0;

  // Generate a list node
  gensym(node, 'l', sratom->next_id);
  if ((st = sratom->write_statement(
         sratom->handle, *flags, NULL, s, p, node, NULL, NULL))) {
    return (SerdStatus)st;
  }

  // _:node rdf:first value
  *flags = SERD_LIST_CONT;
  *p     = serd_node_from_string(SERD_URI, NS_RDF "first");
  st     = sratom_write(sratom, unmap, *flags, node, p, type, size, body);

  // Set subject to node and predicate to rdf:rest for next time
  gensym(node, 'l', ++sratom->next_id);
  *s = *node;
  *p = serd_node_from_string(SERD_URI, NS_RDF "rest");
  return (SerdStatus)st;
}

static SerdStatus
list_end(SerdStatementSink sink,
         void*             handle,
         const unsigned    flags,
         SerdNode*         s,
         SerdNode*         p)
{
  // _:node rdf:rest rdf:nil
  const SerdNode nil = serd_node_from_string(SERD_URI, NS_RDF "nil");
  return sink(handle, flags, NULL, s, p, &nil, NULL, NULL);
}

static SerdStatus
start_object(Sratom*         sratom,
             uint32_t*       flags,
             const SerdNode* subject,
             const SerdNode* predicate,
             const SerdNode* node,
             const char*     type)
{
  SerdStatus st = SERD_SUCCESS;

  if (subject && predicate) {
    st = sratom->write_statement(sratom->handle,
                                 *flags | SERD_ANON_O_BEGIN,
                                 NULL,
                                 subject,
                                 predicate,
                                 node,
                                 NULL,
                                 NULL);

    // Start abbreviating object properties
    *flags |= SERD_ANON_CONT;

    // Object is in a list, stop list abbreviating if necessary
    *flags &= ~(uint32_t)SERD_LIST_CONT;
  }

  if (!st && type) {
    SerdNode p = serd_node_from_string(SERD_URI, NS_RDF "type");
    SerdNode o = serd_node_from_string(SERD_URI, USTR(type));

    st = sratom->write_statement(
      sratom->handle, *flags, NULL, node, &p, &o, NULL, NULL);
  }

  return st;
}

static bool
path_is_absolute(const char* path)
{
  return (path[0] == '/' || (isalpha(path[0]) && path[1] == ':' &&
                             (path[2] == '/' || path[2] == '\\')));
}

static SerdNode
number_type(const Sratom* sratom, const uint8_t* type)
{
  if (sratom->pretty_numbers &&
      (!strcmp((const char*)type, (const char*)NS_XSD "int") ||
       !strcmp((const char*)type, (const char*)NS_XSD "long"))) {
    return serd_node_from_string(SERD_URI, NS_XSD "integer");
  }

  if (sratom->pretty_numbers &&
      (!strcmp((const char*)type, (const char*)NS_XSD "float") ||
       !strcmp((const char*)type, (const char*)NS_XSD "double"))) {
    return serd_node_from_string(SERD_URI, NS_XSD "decimal");
  }

  return serd_node_from_string(SERD_URI, type);
}

static SerdStatus
write_node(const WriteContext* const ctx,
           const SerdNode            object,
           const SerdNode            datatype,
           const SerdNode            language)
{
  const SerdNode def_s = serd_node_from_string(SERD_BLANK, USTR("atom"));
  const SerdNode def_p = serd_node_from_string(SERD_URI, USTR(NS_RDF "value"));
  return ctx->sratom->write_statement(ctx->sratom->handle,
                                      ctx->flags,
                                      NULL,
                                      ctx->subject ? ctx->subject : &def_s,
                                      ctx->predicate ? ctx->predicate : &def_p,
                                      &object,
                                      &datatype,
                                      &language);
}

static SerdStatus
write_free_node(const WriteContext* const ctx,
                SerdNode                  object,
                const SerdNode            datatype,
                const SerdNode            language)
{
  const SerdStatus st = write_node(ctx, object, datatype, language);
  serd_node_free(&object);
  return st;
}

static const char*
unmap_uri(LV2_URID_Unmap* const unmap, const LV2_URID urid)
{
  return unmap->unmap(unmap->handle, urid);
}

static SerdStatus
write_literal(const WriteContext* const          ctx,
              LV2_URID_Unmap* const              unmap,
              const LV2_Atom_Literal_Body* const lit)
{
  const uint8_t* const str = USTR(lit + 1);

  const SerdNode object = serd_node_from_string(SERD_LITERAL, str);
  if (lit->datatype) {
    return write_node(
      ctx,
      object,
      serd_node_from_string(SERD_URI, USTR(unmap_uri(unmap, lit->datatype))),
      SERD_NODE_NULL);
  }

  if (lit->lang) {
    const char* const lang       = unmap_uri(unmap, lit->lang);
    const char* const prefix     = "http://lexvo.org/id/iso639-3/";
    const size_t      prefix_len = strlen(prefix);
    if (!lang || !!strncmp(lang, prefix, prefix_len)) {
      fprintf(stderr, "Unknown language URID %u\n", lit->lang);
      return SERD_ERR_BAD_ARG;
    }

    return write_node(
      ctx,
      object,
      SERD_NODE_NULL,
      serd_node_from_string(SERD_LITERAL, USTR(lang + prefix_len)));
  }

  return write_node(ctx, object, SERD_NODE_NULL, SERD_NODE_NULL);
}

static SerdStatus
write_path(const WriteContext* const ctx, const uint8_t* const str)
{
  SerdNode object   = SERD_NODE_NULL;
  SerdNode datatype = SERD_NODE_NULL;
  if (path_is_absolute((const char*)str)) {
    object = serd_node_new_file_uri(str, NULL, NULL, true);
  } else if (!ctx->sratom->base_uri.buf ||
             !!strncmp((const char*)ctx->sratom->base_uri.buf, "file://", 7)) {
    fprintf(stderr, "warning: Relative path but base is not a file URI.\n");
    fprintf(stderr, "warning: Writing ambiguous atom:Path literal.\n");
    SerdNode string = serd_node_from_string(SERD_LITERAL, str);
    object          = serd_node_copy(&string);
    datatype        = serd_node_from_string(SERD_URI, USTR(LV2_ATOM__Path));
  } else {
    SerdNode rel = serd_node_new_file_uri(str, NULL, NULL, true);
    object       = serd_node_new_uri_from_node(&rel, &ctx->sratom->base, NULL);
    serd_node_free(&rel);
  }

  return write_free_node(ctx, object, datatype, SERD_NODE_NULL);
}

static SerdStatus
write_midi_event(const WriteContext* const ctx,
                 const uint32_t            size,
                 const void* const         body)
{
  static const char hex_chars[] = "0123456789ABCDEF";

  const size_t len = (size_t)size * 2U;
  char* const  str = (char*)calloc(len + 1, 1);
  for (size_t i = 0U; i < size; ++i) {
    const uint8_t byte = ((const uint8_t*)body)[i];
    str[2U * i]        = hex_chars[byte >> 4U];
    str[(2U * i) + 1U] = hex_chars[byte & 0x0FU];
  }

  const SerdStatus st =
    write_node(ctx,
               serd_node_from_string(SERD_LITERAL, USTR(str)),
               serd_node_from_string(SERD_URI, USTR(LV2_MIDI__MidiEvent)),
               SERD_NODE_NULL);
  free(str);
  return st;
}

static SerdStatus
write_event(WriteContext* const         ctx,
            LV2_URID_Unmap* const       unmap,
            const LV2_Atom_Event* const ev)
{
  gensym(&ctx->id, 'e', ctx->sratom->next_id++);

  SerdStatus st = start_object(
    ctx->sratom, &ctx->flags, ctx->subject, ctx->predicate, &ctx->id, NULL);
  if (st) {
    return st;
  }

  SerdNode time     = SERD_NODE_NULL;
  SerdNode p        = SERD_NODE_NULL;
  SerdNode datatype = SERD_NODE_NULL;
  SerdNode language = SERD_NODE_NULL;
  if (ctx->sratom->seq_unit == ctx->sratom->atom_beatTime) {
    time     = serd_node_new_decimal(ev->time.beats, 16);
    p        = serd_node_from_string(SERD_URI, USTR(LV2_ATOM__beatTime));
    datatype = number_type(ctx->sratom, NS_XSD "double");
  } else {
    time     = serd_node_new_integer(ev->time.frames);
    p        = serd_node_from_string(SERD_URI, USTR(LV2_ATOM__frameTime));
    datatype = number_type(ctx->sratom, NS_XSD "long");
  }

  st = ctx->sratom->write_statement(ctx->sratom->handle,
                                    SERD_ANON_CONT,
                                    NULL,
                                    &ctx->id,
                                    &p,
                                    &time,
                                    &datatype,
                                    &language);
  serd_node_free(&time);
  if (st) {
    return st;
  }

  p  = serd_node_from_string(SERD_URI, NS_RDF "value");
  st = (SerdStatus)sratom_write(ctx->sratom,
                                unmap,
                                SERD_ANON_CONT,
                                &ctx->id,
                                &p,
                                ev->body.type,
                                ev->body.size,
                                LV2_ATOM_BODY(&ev->body));

  return st ? st
         : ctx->sratom->end_anon
           ? ctx->sratom->end_anon(ctx->sratom->handle, &ctx->id)
           : SERD_SUCCESS;
}

static SerdStatus
write_tuple(WriteContext* const   ctx,
            LV2_URID_Unmap* const unmap,
            const char* const     type_uri,
            const uint32_t        size,
            const void*           body)
{
  SerdStatus st = SERD_SUCCESS;

  gensym(&ctx->id, 't', ctx->sratom->next_id++);

  if ((st = start_object(ctx->sratom,
                         &ctx->flags,
                         ctx->subject,
                         ctx->predicate,
                         &ctx->id,
                         type_uri))) {
    return st;
  }

  SerdNode p = serd_node_from_string(SERD_URI, NS_RDF "value");
  ctx->flags |= SERD_LIST_O_BEGIN;
  for (const LV2_Atom* i = (const LV2_Atom*)body;
       !lv2_atom_tuple_is_end(body, size, i);
       i = lv2_atom_tuple_next(i)) {
    if ((st = list_append(ctx->sratom,
                          unmap,
                          &ctx->flags,
                          &ctx->id,
                          &p,
                          &ctx->node,
                          i->size,
                          i->type,
                          LV2_ATOM_BODY(i)))) {
      return st;
    }
  }

  st = list_end(ctx->sratom->write_statement,
                ctx->sratom->handle,
                ctx->flags,
                &ctx->id,
                &p);

  return st ? st
         : ctx->sratom->end_anon
           ? ctx->sratom->end_anon(ctx->sratom->handle, &ctx->id)
           : SERD_SUCCESS;
}

static SerdStatus
write_vector(WriteContext* const   ctx,
             LV2_URID_Unmap* const unmap,
             const char* const     type_uri,
             const uint32_t        size,
             const void*           body)
{
  SerdStatus st = SERD_SUCCESS;

  const LV2_Atom_Vector_Body* vec = (const LV2_Atom_Vector_Body*)body;
  if (!vec->child_size) {
    return SERD_ERR_BAD_ARG;
  }

  gensym(&ctx->id, 'v', ctx->sratom->next_id++);
  if ((st = start_object(ctx->sratom,
                         &ctx->flags,
                         ctx->subject,
                         ctx->predicate,
                         &ctx->id,
                         type_uri))) {
    return st;
  }

  SerdNode p = serd_node_from_string(SERD_URI, USTR(LV2_ATOM__childType));
  SerdNode child_type =
    serd_node_from_string(SERD_URI, USTR(unmap_uri(unmap, vec->child_type)));

  if ((st = ctx->sratom->write_statement(ctx->sratom->handle,
                                         ctx->flags,
                                         NULL,
                                         &ctx->id,
                                         &p,
                                         &child_type,
                                         NULL,
                                         NULL))) {
    return st;
  }

  p = serd_node_from_string(SERD_URI, NS_RDF "value");
  ctx->flags |= SERD_LIST_O_BEGIN;
  for (const char* i = (const char*)(vec + 1); i < (const char*)vec + size;
       i += vec->child_size) {
    if ((st = list_append(ctx->sratom,
                          unmap,
                          &ctx->flags,
                          &ctx->id,
                          &p,
                          &ctx->node,
                          vec->child_size,
                          vec->child_type,
                          i))) {
      return st;
    }
  }

  st = list_end(ctx->sratom->write_statement,
                ctx->sratom->handle,
                ctx->flags,
                &ctx->id,
                &p);

  return st ? st
         : ctx->sratom->end_anon
           ? ctx->sratom->end_anon(ctx->sratom->handle, &ctx->id)
           : SERD_SUCCESS;
}

static SerdStatus
write_atom_object(WriteContext* const   ctx,
                  LV2_URID_Unmap* const unmap,
                  const uint32_t        type_urid,
                  const uint32_t        size,
                  const void*           body)
{
  int st = SERD_SUCCESS;

  const LV2_Atom_Object_Body* const obj   = (const LV2_Atom_Object_Body*)body;
  const char* const                 otype = unmap_uri(unmap, obj->otype);

  if (lv2_atom_forge_is_blank(&ctx->sratom->forge, type_urid, obj)) {
    gensym(&ctx->id, 'b', ctx->sratom->next_id++);
    st = start_object(
      ctx->sratom, &ctx->flags, ctx->subject, ctx->predicate, &ctx->id, otype);
  } else {
    ctx->id = serd_node_from_string(SERD_URI, USTR(unmap_uri(unmap, obj->id)));
    ctx->flags = 0U;
    st = start_object(ctx->sratom, &ctx->flags, NULL, NULL, &ctx->id, otype);
  }

  for (const LV2_Atom_Property_Body* p = lv2_atom_object_begin(obj);
       !st && !lv2_atom_object_is_end(obj, size, p);
       p = lv2_atom_object_next(p)) {
    const char* const key  = unmap_uri(unmap, p->key);
    SerdNode          pred = serd_node_from_string(SERD_URI, USTR(key));

    st = sratom_write(ctx->sratom,
                      unmap,
                      ctx->flags,
                      &ctx->id,
                      &pred,
                      p->value.type,
                      p->value.size,
                      LV2_ATOM_BODY(&p->value));
  }

  return st ? (SerdStatus)st
         : (ctx->sratom->end_anon && (ctx->flags & SERD_ANON_CONT))
           ? ctx->sratom->end_anon(ctx->sratom->handle, &ctx->id)
           : SERD_SUCCESS;
}

static SerdStatus
write_sequence(WriteContext* const   ctx,
               LV2_URID_Unmap* const unmap,
               const char* const     type_uri,
               const uint32_t        size,
               const void* const     body)
{
  SerdStatus st = SERD_SUCCESS;

  const LV2_Atom_Sequence_Body* seq = (const LV2_Atom_Sequence_Body*)body;
  gensym(&ctx->id, 'v', ctx->sratom->next_id++);
  if ((st = start_object(ctx->sratom,
                         &ctx->flags,
                         ctx->subject,
                         ctx->predicate,
                         &ctx->id,
                         type_uri))) {
    return st;
  }

  SerdNode p = serd_node_from_string(SERD_URI, NS_RDF "value");
  ctx->flags |= SERD_LIST_O_BEGIN;
  for (const LV2_Atom_Event* ev = lv2_atom_sequence_begin(seq);
       !lv2_atom_sequence_is_end(seq, size, ev);
       ev = lv2_atom_sequence_next(ev)) {
    ctx->sratom->seq_unit = seq->unit;
    if ((st = list_append(ctx->sratom,
                          unmap,
                          &ctx->flags,
                          &ctx->id,
                          &p,
                          &ctx->node,
                          sizeof(LV2_Atom_Event) + ev->body.size,
                          ctx->sratom->atom_Event,
                          ev))) {
      return st;
    }
  }

  st = list_end(ctx->sratom->write_statement,
                ctx->sratom->handle,
                ctx->flags,
                &ctx->id,
                &p);

  return st ? st
         : (ctx->sratom->end_anon && ctx->subject && ctx->predicate)
           ? ctx->sratom->end_anon(ctx->sratom->handle, &ctx->id)
           : SERD_SUCCESS;
}

static SerdStatus
write_value_object(WriteContext* const ctx,
                   const char* const   type_uri,
                   const uint32_t      size,
                   const void* const   body)
{
  SerdStatus st = SERD_SUCCESS;

  gensym(&ctx->id, 'b', ctx->sratom->next_id++);
  if ((st = start_object(ctx->sratom,
                         &ctx->flags,
                         ctx->subject,
                         ctx->predicate,
                         &ctx->id,
                         type_uri))) {
    return st;
  }

  SerdNode p        = serd_node_from_string(SERD_URI, NS_RDF "value");
  SerdNode o        = serd_node_new_blob(body, size, true);
  SerdNode datatype = serd_node_from_string(SERD_URI, NS_XSD "base64Binary");

  st = ctx->sratom->write_statement(
    ctx->sratom->handle, ctx->flags, NULL, &ctx->id, &p, &o, &datatype, NULL);

  if (!st && ctx->sratom->end_anon && ctx->subject && ctx->predicate) {
    st = ctx->sratom->end_anon(ctx->sratom->handle, &ctx->id);
  }

  serd_node_free(&o);
  return st;
}

int
sratom_write(Sratom*         sratom,
             LV2_URID_Unmap* unmap,
             uint32_t        flags,
             const SerdNode* subject,
             const SerdNode* predicate,
             uint32_t        type_urid,
             uint32_t        size,
             const void*     body)
{
  WriteContext ctx = {
    sratom,
    subject,
    predicate,
    flags,
    {'b', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '\0'},
    {'b', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '\0'},
    SERD_NODE_NULL,
    SERD_NODE_NULL};

  ctx.id   = serd_node_from_string(SERD_BLANK, ctx.idbuf);
  ctx.node = serd_node_from_string(SERD_BLANK, ctx.nodebuf);

  const char* const type = unmap_uri(unmap, type_urid);
  if (type_urid == 0 && size == 0) {
    return write_node(&ctx,
                      serd_node_from_string(SERD_URI, USTR(NS_RDF "nil")),
                      SERD_NODE_NULL,
                      SERD_NODE_NULL);
  }

  if (type_urid == sratom->forge.String) {
    return write_node(&ctx,
                      serd_node_from_string(SERD_LITERAL, USTR(body)),
                      SERD_NODE_NULL,
                      SERD_NODE_NULL);
  }

  if (type_urid == sratom->forge.Chunk) {
    return write_free_node(
      &ctx,
      serd_node_new_blob(body, size, true),
      serd_node_from_string(SERD_URI, NS_XSD "base64Binary"),
      SERD_NODE_NULL);
  }

  if (type_urid == sratom->forge.Literal) {
    return write_literal(&ctx, unmap, (const LV2_Atom_Literal_Body*)body);
  }

  if (type_urid == sratom->forge.URID) {
    return write_node(
      &ctx,
      serd_node_from_string(SERD_URI,
                            USTR(unmap_uri(unmap, *(const uint32_t*)body))),
      SERD_NODE_NULL,
      SERD_NODE_NULL);
  }

  if (type_urid == sratom->forge.Path) {
    return write_path(&ctx, USTR(body));
  }

  if (type_urid == sratom->forge.URI) {
    return write_node(&ctx,
                      serd_node_from_string(SERD_URI, USTR(body)),
                      SERD_NODE_NULL,
                      SERD_NODE_NULL);
  }

  if (type_urid == sratom->forge.Int) {
    return write_free_node(&ctx,
                           serd_node_new_integer(*(const int32_t*)body),
                           number_type(sratom, NS_XSD "int"),
                           SERD_NODE_NULL);
  }

  if (type_urid == sratom->forge.Long) {
    return write_free_node(&ctx,
                           serd_node_new_integer(*(const int64_t*)body),
                           number_type(sratom, NS_XSD "long"),
                           SERD_NODE_NULL);
  }

  if (type_urid == sratom->forge.Float) {
    return write_free_node(&ctx,
                           serd_node_new_decimal(*(const float*)body, 8),
                           number_type(sratom, NS_XSD "float"),
                           SERD_NODE_NULL);
  }

  if (type_urid == sratom->forge.Double) {
    return write_free_node(&ctx,
                           serd_node_new_decimal(*(const double*)body, 16),
                           number_type(sratom, NS_XSD "double"),
                           SERD_NODE_NULL);
  }

  if (type_urid == sratom->forge.Bool) {
    return write_node(
      &ctx,
      serd_node_from_string(SERD_LITERAL,
                            USTR(*(const int32_t*)body ? "true" : "false")),
      serd_node_from_string(SERD_URI, NS_XSD "boolean"),
      SERD_NODE_NULL);
  }

  if (type_urid == sratom->midi_MidiEvent) {
    return write_midi_event(&ctx, size, body);
  }

  if (type_urid == sratom->atom_Event) {
    return write_event(&ctx, unmap, (const LV2_Atom_Event*)body);
  }

  if (type_urid == sratom->forge.Tuple) {
    return write_tuple(&ctx, unmap, type, size, body);
  }

  if (type_urid == sratom->forge.Vector) {
    return write_vector(&ctx, unmap, type, size, body);
  }

  if (lv2_atom_forge_is_object_type(&sratom->forge, type_urid)) {
    return write_atom_object(&ctx, unmap, type_urid, size, body);
  }

  if (type_urid == sratom->forge.Sequence) {
    return write_sequence(&ctx, unmap, type, size, body);
  }

  return write_value_object(&ctx, type, size, body);
}

char*
sratom_to_turtle(Sratom*         sratom,
                 LV2_URID_Unmap* unmap,
                 const char*     base_uri,
                 const SerdNode* subject,
                 const SerdNode* predicate,
                 uint32_t        type,
                 uint32_t        size,
                 const void*     body)
{
  SerdURI  buri = SERD_URI_NULL;
  SerdNode base =
    serd_node_new_uri_from_string(USTR(base_uri), &sratom->base, &buri);
  SerdEnv*    env = sratom->env ? sratom->env : serd_env_new(NULL);
  SerdChunk   str = {NULL, 0};
  SerdWriter* writer =
    serd_writer_new(SERD_TURTLE, style, env, &buri, serd_chunk_sink, &str);

  serd_env_set_base_uri(env, &base);
  sratom_set_sink(sratom,
                  base_uri,
                  (SerdStatementSink)serd_writer_write_statement,
                  (SerdEndSink)serd_writer_end_anon,
                  writer);

  const int st = sratom_write(
    sratom, unmap, SERD_EMPTY_S, subject, predicate, type, size, body);

  serd_writer_finish(writer);

  serd_writer_free(writer);
  if (!sratom->env) {
    serd_env_free(env);
  }

  serd_node_free(&base);
  if (st) {
    free((void*)str.buf);
    return NULL;
  }

  return (char*)serd_chunk_sink_finish(&str);
}

static void
read_list_value(Sratom*         sratom,
                LV2_Atom_Forge* forge,
                SordWorld*      world,
                SordModel*      model,
                const SordNode* node,
                ReadMode        mode)
{
  SordNode* fst = sord_get(model, node, sratom->nodes.rdf_first, NULL, NULL);
  SordNode* rst = sord_get(model, node, sratom->nodes.rdf_rest, NULL, NULL);
  if (fst && rst) {
    read_node(sratom, forge, world, model, fst, mode);
    read_list_value(sratom, forge, world, model, rst, mode);
  }

  sord_node_free(world, rst);
  sord_node_free(world, fst);
}

static void
read_resource(Sratom*         sratom,
              LV2_Atom_Forge* forge,
              SordWorld*      world,
              SordModel*      model,
              const SordNode* node,
              LV2_URID        otype)
{
  LV2_URID_Map* map = sratom->map;
  SordQuad      q   = {node, NULL, NULL, NULL};
  SordIter*     i   = sord_find(model, q);
  SordQuad      match;
  for (; !sord_iter_end(i); sord_iter_next(i)) {
    sord_iter_get(i, match);
    const SordNode* p      = match[SORD_PREDICATE];
    const SordNode* o      = match[SORD_OBJECT];
    const char*     p_uri  = (const char*)sord_node_get_string(p);
    uint32_t        p_urid = map->map(map->handle, p_uri);
    if (!(sord_node_equals(p, sratom->nodes.rdf_type) &&
          sord_node_get_type(o) == SORD_URI &&
          map->map(map->handle, (const char*)sord_node_get_string(o)) ==
            otype)) {
      lv2_atom_forge_key(forge, p_urid);
      read_node(sratom, forge, world, model, o, MODE_BODY);
    }
  }
  sord_iter_free(i);
}

static uint32_t
atom_size(const Sratom* sratom, uint32_t type_urid)
{
  if (type_urid == sratom->forge.Int || type_urid == sratom->forge.Bool) {
    return sizeof(int32_t);
  }

  if (type_urid == sratom->forge.Long) {
    return sizeof(int64_t);
  }

  if (type_urid == sratom->forge.Float) {
    return sizeof(float);
  }

  if (type_urid == sratom->forge.Double) {
    return sizeof(double);
  }

  if (type_urid == sratom->forge.URID) {
    return sizeof(uint32_t);
  }

  return 0;
}

static inline uint8_t
hex_digit_value(const uint8_t c)
{
  return (uint8_t)((c > '9') ? ((c & ~0x20) - 'A' + 10) : (c - '0'));
}

static void
read_literal(Sratom* sratom, LV2_Atom_Forge* forge, const SordNode* node)
{
  assert(sord_node_get_type(node) == SORD_LITERAL);

  size_t          len = 0;
  const char*     str = (const char*)sord_node_get_string_counted(node, &len);
  const SordNode* datatype = sord_node_get_datatype(node);
  const char*     language = sord_node_get_language(node);
  if (datatype) {
    const char* type_uri = (const char*)sord_node_get_string(datatype);
    if (!strcmp(type_uri, (const char*)NS_XSD "int") ||
        !strcmp(type_uri, (const char*)NS_XSD "integer")) {
      lv2_atom_forge_int(forge, strtol(str, NULL, 10));
    } else if (!strcmp(type_uri, (const char*)NS_XSD "long")) {
      lv2_atom_forge_long(forge, strtol(str, NULL, 10));
    } else if (!strcmp(type_uri, (const char*)NS_XSD "float") ||
               !strcmp(type_uri, (const char*)NS_XSD "decimal")) {
      lv2_atom_forge_float(forge, (float)serd_strtod(str, NULL));
    } else if (!strcmp(type_uri, (const char*)NS_XSD "double")) {
      lv2_atom_forge_double(forge, serd_strtod(str, NULL));
    } else if (!strcmp(type_uri, (const char*)NS_XSD "boolean")) {
      lv2_atom_forge_bool(forge, !strcmp(str, "true"));
    } else if (!strcmp(type_uri, (const char*)NS_XSD "base64Binary")) {
      size_t size = 0;
      void*  body = serd_base64_decode(USTR(str), len, &size);
      lv2_atom_forge_atom(forge, size, forge->Chunk);
      lv2_atom_forge_write(forge, body, size);
      free(body);
    } else if (!strcmp(type_uri, LV2_ATOM__Path)) {
      lv2_atom_forge_path(forge, str, len);
    } else if (!strcmp(type_uri, LV2_MIDI__MidiEvent)) {
      lv2_atom_forge_atom(forge, len / 2, sratom->midi_MidiEvent);
      for (const char* s = str; s < str + len; s += 2) {
        const uint8_t hi = hex_digit_value(s[0]);
        const uint8_t lo = hex_digit_value(s[1]);
        const uint8_t c  = (uint8_t)(((unsigned)hi << 4U) | lo);
        lv2_atom_forge_raw(forge, &c, 1);
      }
      lv2_atom_forge_pad(forge, len / 2);
    } else {
      lv2_atom_forge_literal(
        forge, str, len, sratom->map->map(sratom->map->handle, type_uri), 0);
    }
  } else if (language) {
    static const char* const prefix       = "http://lexvo.org/id/iso639-3/";
    const size_t             prefix_len   = strlen(prefix);
    const size_t             language_len = strlen(language);
    const size_t             lang_uri_len = prefix_len + language_len;
    char*                    lang_uri     = (char*)calloc(lang_uri_len + 1, 1);

    memcpy(lang_uri, prefix, prefix_len + 1);
    memcpy(lang_uri + prefix_len, language, language_len + 1);

    lv2_atom_forge_literal(
      forge, str, len, 0, sratom->map->map(sratom->map->handle, lang_uri));
    free(lang_uri);
  } else {
    lv2_atom_forge_string(forge, str, len);
  }
}

static void
read_object(Sratom*         sratom,
            LV2_Atom_Forge* forge,
            SordWorld*      world,
            SordModel*      model,
            const SordNode* node,
            ReadMode        mode)
{
  LV2_URID_Map* map = sratom->map;
  size_t        len = 0;
  const char*   str = (const char*)sord_node_get_string_counted(node, &len);

  SordNode* type  = sord_get(model, node, sratom->nodes.rdf_type, NULL, NULL);
  SordNode* value = sord_get(model, node, sratom->nodes.rdf_value, NULL, NULL);

  const uint8_t* type_uri  = NULL;
  uint32_t       type_urid = 0;
  if (type) {
    type_uri  = sord_node_get_string(type);
    type_urid = map->map(map->handle, (const char*)type_uri);
  }

  LV2_Atom_Forge_Frame frame = {0, 0};
  if (mode == MODE_SEQUENCE) {
    SordNode* time =
      sord_get(model, node, sratom->nodes.atom_beatTime, NULL, NULL);
    uint32_t seq_unit = 0U;
    if (time) {
      const char* time_str = (const char*)sord_node_get_string(time);
      lv2_atom_forge_beat_time(forge, serd_strtod(time_str, NULL));
      seq_unit = sratom->atom_beatTime;
    } else {
      time = sord_get(model, node, sratom->nodes.atom_frameTime, NULL, NULL);
      const char* time_str =
        time ? (const char*)sord_node_get_string(time) : "";
      lv2_atom_forge_frame_time(forge, serd_strtod(time_str, NULL));
      seq_unit = sratom->atom_frameTime;
    }
    read_node(sratom, forge, world, model, value, MODE_BODY);
    sord_node_free(world, time);
    sratom->seq_unit = seq_unit;
  } else if (type_urid == sratom->forge.Tuple) {
    lv2_atom_forge_tuple(forge, &frame);
    read_list_value(sratom, forge, world, model, value, MODE_BODY);
  } else if (type_urid == sratom->forge.Sequence) {
    const LV2_Atom_Forge_Ref ref =
      lv2_atom_forge_sequence_head(forge, &frame, 0);
    sratom->seq_unit = 0;
    read_list_value(sratom, forge, world, model, value, MODE_SEQUENCE);

    LV2_Atom_Sequence* seq =
      (LV2_Atom_Sequence*)lv2_atom_forge_deref(forge, ref);
    seq->body.unit =
      (sratom->seq_unit == sratom->atom_frameTime) ? 0 : sratom->seq_unit;
  } else if (type_urid == sratom->forge.Vector) {
    SordNode* child_type_node =
      sord_get(model, node, sratom->nodes.atom_childType, NULL, NULL);
    if (child_type_node) {
      uint32_t child_type = map->map(
        map->handle, (const char*)sord_node_get_string(child_type_node));
      uint32_t child_size = atom_size(sratom, child_type);
      if (child_size > 0) {
        LV2_Atom_Forge_Ref ref =
          lv2_atom_forge_vector_head(forge, &frame, child_size, child_type);
        read_list_value(sratom, forge, world, model, value, MODE_BODY);
        lv2_atom_forge_pop(forge, &frame);
        frame.ref = 0;
        lv2_atom_forge_pad(forge, lv2_atom_forge_deref(forge, ref)->size);
      }
    }
    sord_node_free(world, child_type_node);
  } else if (value && sord_node_equals(sord_node_get_datatype(value),
                                       sratom->nodes.xsd_base64Binary)) {
    size_t         vlen = 0;
    const uint8_t* vstr = sord_node_get_string_counted(value, &vlen);
    size_t         size = 0;
    void*          body = serd_base64_decode(vstr, vlen, &size);
    lv2_atom_forge_atom(forge, size, type_urid);
    lv2_atom_forge_write(forge, body, size);
    free(body);
  } else if (sord_node_get_type(node) == SORD_URI) {
    lv2_atom_forge_object(forge, &frame, map->map(map->handle, str), type_urid);
    read_resource(sratom, forge, world, model, node, type_urid);
  } else {
    lv2_atom_forge_object(forge, &frame, 0, type_urid);
    read_resource(sratom, forge, world, model, node, type_urid);
  }

  if (frame.ref) {
    lv2_atom_forge_pop(forge, &frame);
  }
  sord_node_free(world, value);
  sord_node_free(world, type);
}

static void
read_node(Sratom*         sratom,
          LV2_Atom_Forge* forge,
          SordWorld*      world,
          SordModel*      model,
          const SordNode* node,
          ReadMode        mode)
{
  LV2_URID_Map* map = sratom->map;
  size_t        len = 0;
  const char*   str = (const char*)sord_node_get_string_counted(node, &len);
  if (sord_node_get_type(node) == SORD_LITERAL) {
    read_literal(sratom, forge, node);
  } else if (sord_node_get_type(node) == SORD_URI &&
             !(sratom->object_mode == SRATOM_OBJECT_MODE_BLANK_SUBJECT &&
               mode == MODE_SUBJECT)) {
    if (!strcmp(str, (const char*)NS_RDF "nil")) {
      lv2_atom_forge_atom(forge, 0, 0);
    } else if (!strncmp(str, "file://", 7)) {
      SerdURI uri;
      serd_uri_parse(USTR(str), &uri);

      SerdNode rel =
        serd_node_new_relative_uri(&uri, &sratom->base, &sratom->base, NULL);

      uint8_t* const path = serd_file_uri_parse(USTR(rel.buf), NULL);

      if (path) {
        lv2_atom_forge_path(
          forge, (const char*)path, strlen((const char*)path));
        serd_free(path);
      } else {
        // FIXME: Report errors (required API change)
        lv2_atom_forge_atom(forge, 0, 0);
      }
      serd_node_free(&rel);
    } else {
      lv2_atom_forge_urid(forge, map->map(map->handle, str));
    }
  } else {
    read_object(sratom, forge, world, model, node, mode);
  }
}

void
sratom_read(Sratom*         sratom,
            LV2_Atom_Forge* forge,
            SordWorld*      world,
            SordModel*      model,
            const SordNode* node)
{
  sratom->nodes.atom_childType = sord_new_uri(world, USTR(LV2_ATOM__childType));
  sratom->nodes.atom_frameTime = sord_new_uri(world, USTR(LV2_ATOM__frameTime));
  sratom->nodes.atom_beatTime  = sord_new_uri(world, USTR(LV2_ATOM__beatTime));
  sratom->nodes.rdf_first      = sord_new_uri(world, NS_RDF "first");
  sratom->nodes.rdf_rest       = sord_new_uri(world, NS_RDF "rest");
  sratom->nodes.rdf_type       = sord_new_uri(world, NS_RDF "type");
  sratom->nodes.rdf_value      = sord_new_uri(world, NS_RDF "value");
  sratom->nodes.xsd_base64Binary = sord_new_uri(world, NS_XSD "base64Binary");

  sratom->next_id = 1;
  read_node(sratom, forge, world, model, node, MODE_SUBJECT);

  sord_node_free(world, sratom->nodes.xsd_base64Binary);
  sord_node_free(world, sratom->nodes.rdf_value);
  sord_node_free(world, sratom->nodes.rdf_type);
  sord_node_free(world, sratom->nodes.rdf_rest);
  sord_node_free(world, sratom->nodes.rdf_first);
  sord_node_free(world, sratom->nodes.atom_frameTime);
  sord_node_free(world, sratom->nodes.atom_beatTime);
  sord_node_free(world, sratom->nodes.atom_childType);

  sratom->nodes.xsd_base64Binary = NULL;
  sratom->nodes.rdf_value        = NULL;
  sratom->nodes.rdf_type         = NULL;
  sratom->nodes.rdf_rest         = NULL;
  sratom->nodes.rdf_first        = NULL;
  sratom->nodes.atom_beatTime    = NULL;
  sratom->nodes.atom_frameTime   = NULL;
  sratom->nodes.atom_childType   = NULL;
}

LV2_Atom_Forge_Ref
sratom_forge_sink(LV2_Atom_Forge_Sink_Handle handle,
                  const void*                buf,
                  uint32_t                   size)
{
  SerdChunk*               chunk = (SerdChunk*)handle;
  const LV2_Atom_Forge_Ref ref   = chunk->len + 1;
  serd_chunk_sink(buf, size, chunk);
  return ref;
}

LV2_Atom*
sratom_forge_deref(LV2_Atom_Forge_Sink_Handle handle, LV2_Atom_Forge_Ref ref)
{
  const SerdChunk* chunk = (const SerdChunk*)handle;
  return (LV2_Atom*)(chunk->buf + ref - 1);
}

LV2_Atom*
sratom_from_turtle(Sratom*         sratom,
                   const char*     base_uri,
                   const SerdNode* subject,
                   const SerdNode* predicate,
                   const char*     str)
{
  SerdChunk out = {NULL, 0};
  SerdNode  base =
    serd_node_new_uri_from_string(USTR(base_uri), &sratom->base, NULL);
  SordWorld*  world  = sord_world_new();
  SordModel*  model  = sord_new(world, SORD_SPO, false);
  SerdEnv*    env    = sratom->env ? sratom->env : serd_env_new(&base);
  SerdReader* reader = sord_new_reader(model, env, SERD_TURTLE, NULL);

  if (!serd_reader_read_string(reader, USTR(str))) {
    const SordNode* s = sord_node_from_serd_node(world, env, subject, 0, 0);
    lv2_atom_forge_set_sink(
      &sratom->forge, sratom_forge_sink, sratom_forge_deref, &out);
    if (subject && predicate) {
      const SordNode* p = sord_node_from_serd_node(world, env, predicate, 0, 0);
      SordNode*       o = sord_get(model, s, p, NULL, NULL);
      if (o) {
        sratom_read(sratom, &sratom->forge, world, model, o);
        sord_node_free(world, o);
      } else {
        fprintf(stderr, "Failed to find node\n");
      }
    } else {
      sratom_read(sratom, &sratom->forge, world, model, s);
    }
  } else {
    fprintf(stderr, "Failed to read Turtle\n");
  }

  serd_reader_free(reader);
  if (!sratom->env) {
    serd_env_free(env);
  }

  sord_free(model);
  sord_world_free(world);
  serd_node_free(&base);

  return (LV2_Atom*)out.buf;
}
