// Copyright 2012-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

/// @file sratom.h Public API for Sratom

#ifndef SRATOM_SRATOM_H
#define SRATOM_SRATOM_H

#include <lv2/atom/atom.h>
#include <lv2/atom/forge.h>
#include <lv2/urid/urid.h>
#include <serd/serd.h>
#include <sord/sord.h>

#include <stdbool.h>
#include <stdint.h>

#if defined(_WIN32) && !defined(SRATOM_STATIC) && defined(SRATOM_INTERNAL)
#  define SRATOM_API __declspec(dllexport)
#elif defined(_WIN32) && !defined(SRATOM_STATIC)
#  define SRATOM_API __declspec(dllimport)
#elif defined(__GNUC__)
#  define SRATOM_API __attribute__((visibility("default")))
#else
#  define SRATOM_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

/**
   @defgroup sratom Sratom C API
   @{
*/

/// Atom serializer
typedef struct SratomImpl Sratom;

/**
   Mode for reading resources to LV2 Objects.

   This affects how resources (which are either blank nodes or have URIs) are
   read by sratom_read(), since they may be read as simple references (a URI or
   blank node ID) or a complete description (an atom "Object").

   Currently, blank nodes are always read as Objects, but support for reading
   blank node IDs may be added in the future.
*/
typedef enum {
  /// Read blank nodes as Objects, and named resources as URIs
  SRATOM_OBJECT_MODE_BLANK,

  /**
     Read blank nodes and the main subject as Objects.

     Any other named resources are read as URIs.  The "main subject" is the
     subject parameter passed to sratom_read(); if this is a resource it will
     be read as an Object, but all other named resources encountered will be
     read as URIs.
  */
  SRATOM_OBJECT_MODE_BLANK_SUBJECT
} SratomObjectMode;

/// Create a new Atom serializer
SRATOM_API Sratom* SERD_ALLOCATED
sratom_new(LV2_URID_Map* SERD_NONNULL map);

/// Free an Atom serializer
SRATOM_API void
sratom_free(Sratom* SERD_NULLABLE sratom);

/**
   Set the environment for reading or writing Turtle.

   This can be used to set namespace prefixes and a base URI for
   sratom_to_turtle() and sratom_from_turtle().
*/
SRATOM_API void
sratom_set_env(Sratom* SERD_NONNULL sratom, SerdEnv* SERD_NULLABLE env);

/**
   Set the sink(s) where sratom will write its output.

   This must be called before calling sratom_write().
*/
SRATOM_API void
sratom_set_sink(Sratom* SERD_NONNULL           sratom,
                const char* SERD_NULLABLE      base_uri,
                SerdStatementSink SERD_NONNULL sink,
                SerdEndSink SERD_NULLABLE      end_sink,
                void* SERD_UNSPECIFIED         handle);

/**
   Write pretty numeric literals.

   If `pretty_numbers` is true, numbers will be written as pretty Turtle
   literals, rather than string literals with precise types.  The cost of this
   is that the types might get fudged on a round-trip to RDF and back.
*/
SRATOM_API void
sratom_set_pretty_numbers(Sratom* SERD_NONNULL sratom, bool pretty_numbers);

/// Configure how resources will be read to form LV2 Objects
SRATOM_API void
sratom_set_object_mode(Sratom* SERD_NONNULL sratom,
                       SratomObjectMode     object_mode);

/**
   Write an Atom to RDF.

   The serialized atom is written to the sink set by sratom_set_sink().

   @return 0 on success, or a non-zero error code otherwise.
*/
SRATOM_API int
sratom_write(Sratom* SERD_NONNULL             sratom,
             LV2_URID_Unmap* SERD_UNSPECIFIED unmap,
             uint32_t                         flags,
             const SerdNode* SERD_NULLABLE    subject,
             const SerdNode* SERD_NULLABLE    predicate,
             uint32_t                         type_urid,
             uint32_t                         size,
             const void* SERD_NONNULL         body);

/**
   Read an Atom from RDF.

   The resulting atom will be written to `forge`.
*/
SRATOM_API void
sratom_read(Sratom* SERD_NONNULL         sratom,
            LV2_Atom_Forge* SERD_NONNULL forge,
            SordWorld* SERD_NONNULL      world,
            SordModel* SERD_NONNULL      model,
            const SordNode* SERD_NONNULL node);

/**
   Serialize an Atom to a Turtle string.

   The returned string must be free()'d by the caller.
*/
SRATOM_API char* SERD_ALLOCATED
sratom_to_turtle(Sratom* SERD_NONNULL             sratom,
                 LV2_URID_Unmap* SERD_UNSPECIFIED unmap,
                 const char* SERD_NONNULL         base_uri,
                 const SerdNode* SERD_UNSPECIFIED subject,
                 const SerdNode* SERD_UNSPECIFIED predicate,
                 uint32_t                         type,
                 uint32_t                         size,
                 const void* SERD_NONNULL         body);

/**
   Read an Atom from a Turtle string.

   The returned atom must be free()'d by the caller.
*/
SRATOM_API LV2_Atom* SERD_ALLOCATED
sratom_from_turtle(Sratom* SERD_NONNULL             sratom,
                   const char* SERD_NONNULL         base_uri,
                   const SerdNode* SERD_UNSPECIFIED subject,
                   const SerdNode* SERD_UNSPECIFIED predicate,
                   const char* SERD_NONNULL         str);

/**
   A convenient resizing sink for LV2_Atom_Forge.

   The handle must point to an initialized SerdChunk.
*/
SRATOM_API LV2_Atom_Forge_Ref
sratom_forge_sink(LV2_Atom_Forge_Sink_Handle SERD_UNSPECIFIED handle,
                  const void* SERD_NONNULL                    buf,
                  uint32_t                                    size);

/// The corresponding deref function for sratom_forge_sink
SRATOM_API LV2_Atom* SERD_NULLABLE
sratom_forge_deref(LV2_Atom_Forge_Sink_Handle SERD_UNSPECIFIED handle,
                   LV2_Atom_Forge_Ref                          ref);

/**
   @}
*/

#ifdef __cplusplus
} /* extern "C" */
#endif

#endif /* SRATOM_SRATOM_H */
