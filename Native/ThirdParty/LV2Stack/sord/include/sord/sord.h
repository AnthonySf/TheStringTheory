// Copyright 2011-2016 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

/**
   @file sord.h API for Sord, a lightweight RDF model library.
*/

#ifndef SORD_SORD_H
#define SORD_SORD_H

#include <serd/serd.h>

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

// SORD_API must be used to decorate things in the public API
#ifndef SORD_API
#  if defined(_WIN32) && !defined(SORD_STATIC) && defined(SORD_INTERNAL)
#    define SORD_API __declspec(dllexport)
#  elif defined(_WIN32) && !defined(SORD_STATIC)
#    define SORD_API __declspec(dllimport)
#  elif defined(__GNUC__)
#    define SORD_API __attribute__((visibility("default")))
#  else
#    define SORD_API
#  endif
#endif

#ifdef __cplusplus
extern "C" {
#endif

/**
   @defgroup sord Sord
   A lightweight RDF model library.

   Sord stores RDF (subject object predicate context) quads, where the context
   may be omitted (to represent triples in the default graph).
   @{
*/

/**
   Sord World.
   The World represents all library state, including interned strings.
*/
typedef struct SordWorldImpl SordWorld;

/**
   Sord Model.

   A model is an indexed set of Quads (i.e. it can contain several RDF
   graphs).  It may be searched using various patterns depending on which
   indices are enabled.
*/
typedef struct SordModelImpl SordModel;

/**
   Model Inserter.

   An inserter is used for writing statements to a model using the Serd sink
   interface.  This makes it simple to write to a model directly using a
   SerdReader, or any other code that writes statements to a SerdStatementSink.
*/
typedef struct SordInserterImpl SordInserter;

/**
   Model Iterator.
*/
typedef struct SordIterImpl SordIter;

/**
   RDF Node.
   A Node is a component of a Quad.  Nodes may be URIs, blank nodes, or
   (in the case of quad objects only) string literals. Literal nodes may
   have an associate language or datatype (but not both).
*/
typedef struct SordNodeImpl SordNode;

/**
   Quad of nodes (a statement), or a quad pattern.

   Nodes are ordered (S P O G).  The ID of the default graph is 0.
*/
typedef const SordNode* SordQuad[4];

/**
   Index into a SordQuad.
*/
typedef enum {
  SORD_SUBJECT   = 0U, /**< Subject */
  SORD_PREDICATE = 1U, /**< Predicate ("key") */
  SORD_OBJECT    = 2U, /**< Object    ("value") */
  SORD_GRAPH     = 3U  /**< Graph     ("context") */
} SordQuadIndex;

/**
   Type of a node.
*/
typedef enum {
  SORD_URI     = 1U, /**< URI */
  SORD_BLANK   = 2U, /**< Blank node identifier */
  SORD_LITERAL = 3U  /**< Literal (string with optional lang or datatype) */
} SordNodeType;

/**
   Indexing option.
*/
typedef enum {
  SORD_SPO = 1U,       /**< Subject,   Predicate, Object */
  SORD_SOP = 1U << 1U, /**< Subject,   Object,    Predicate */
  SORD_OPS = 1U << 2U, /**< Object,    Predicate, Subject */
  SORD_OSP = 1U << 3U, /**< Object,    Subject,   Predicate */
  SORD_PSO = 1U << 4U, /**< Predicate, Subject,   Object */
  SORD_POS = 1U << 5U  /**< Predicate, Object,    Subject */
} SordIndexOption;

/**
   @name World
   @{
*/

/**
   Create a new Sord World.
   It is safe to use multiple worlds in one process, though no data
   (e.g. nodes) can be shared between worlds, and this should be avoided if
   possible for performance reasons.
*/
SORD_API SordWorld* SERD_ALLOCATED
sord_world_new(void);

/**
   Free `world`.
*/
SORD_API void
sord_world_free(SordWorld* SERD_NULLABLE world);

/**
   Set a function to be called when errors occur.

   The `error_sink` will be called with `handle` as its first argument.  If
   no error function is set, errors are printed to stderr.
*/
SORD_API void
sord_world_set_error_sink(SordWorld* SERD_NONNULL     world,
                          SerdErrorSink SERD_NULLABLE error_sink,
                          void* SERD_UNSPECIFIED      handle);

/**
   @}
   @name Node
   @{
*/

/**
   Get a URI node from a string.

   Note this function measures `str`, which is a common bottleneck.
   Use sord_node_from_serd_node() instead if `str` is already measured.
*/
SORD_API SordNode* SERD_ALLOCATED
sord_new_uri(SordWorld* SERD_NONNULL world, const uint8_t* SERD_NONNULL uri);

/**
   Get a URI node from a relative URI string.
*/
SORD_API SordNode* SERD_ALLOCATED
sord_new_relative_uri(SordWorld* SERD_NONNULL      world,
                      const uint8_t* SERD_NONNULL  uri,
                      const uint8_t* SERD_NULLABLE base_uri);

/**
   Get a blank node from a string.

   Note this function measures `str`, which is a common bottleneck.
   Use sord_node_from_serd_node() instead if `str` is already measured.
*/
SORD_API SordNode* SERD_ALLOCATED
sord_new_blank(SordWorld* SERD_NONNULL world, const uint8_t* SERD_NONNULL str);

/**
   Get a literal node from a string.

   Note this function measures `str`, which is a common bottleneck.
   Use sord_node_from_serd_node() instead if `str` is already measured.
*/
SORD_API SordNode* SERD_ALLOCATED
sord_new_literal(SordWorld* SERD_NONNULL     world,
                 SordNode* SERD_NULLABLE     datatype,
                 const uint8_t* SERD_NONNULL str,
                 const char* SERD_NULLABLE   lang);

/**
   Copy a node (obtain a reference).

   Node that since nodes are interned and reference counted, this does not
   actually create a deep copy of `node`.
*/
SORD_API SordNode* SERD_ALLOCATED
sord_node_copy(const SordNode* SERD_NULLABLE node);

/**
   Free a node (drop a reference).
*/
SORD_API void
sord_node_free(SordWorld* SERD_NONNULL world, SordNode* SERD_NULLABLE node);

/**
   Return the type of a node (SORD_URI, SORD_BLANK, or SORD_LITERAL).
*/
SORD_API SordNodeType
sord_node_get_type(const SordNode* SERD_NONNULL node);

/**
   Return the string value of a node.
*/
SORD_API const uint8_t* SERD_NONNULL
sord_node_get_string(const SordNode* SERD_NONNULL node);

/**
   Return the string value of a node, and set `bytes` to its length in bytes.
*/
SORD_API const uint8_t* SERD_NONNULL
sord_node_get_string_counted(const SordNode* SERD_NONNULL node,
                             size_t* SERD_NONNULL         bytes);

/**
   Return the string value of a node, and set `bytes` to its length in bytes,
   and `count` to its length in characters.
*/
SORD_API const uint8_t* SERD_NONNULL
sord_node_get_string_measured(const SordNode* SERD_NONNULL node,
                              size_t* SERD_NONNULL         bytes,
                              size_t* SERD_NONNULL         chars);

/**
   Return the language of a literal node (or NULL).
*/
SORD_API const char* SERD_NULLABLE
sord_node_get_language(const SordNode* SERD_NONNULL node);

/**
   Return the datatype URI of a literal node (or NULL).
*/
SORD_API SordNode* SERD_NULLABLE
sord_node_get_datatype(const SordNode* SERD_NONNULL node);

/**
   Return the flags (string attributes) of a node.
*/
SORD_API SerdNodeFlags
sord_node_get_flags(const SordNode* SERD_NONNULL node);

/**
   Return true iff node can be serialised as an inline object.

   More specifically, this returns true iff the node is the object field
   of exactly one statement, and therefore can be inlined since it needn't
   be referred to by name.
*/
SORD_API bool
sord_node_is_inline_object(const SordNode* SERD_NONNULL node);

/**
   Return true iff `a` is equal to `b`.

   Note this is much faster than comparing the node's strings.
*/
SORD_API bool
sord_node_equals(const SordNode* SERD_NULLABLE a,
                 const SordNode* SERD_NULLABLE b);

/**
   Return a SordNode as a SerdNode.

   The returned node is shared and must not be freed or modified.
*/
SORD_API const SerdNode* SERD_NONNULL
sord_node_to_serd_node(const SordNode* SERD_NULLABLE node);

/**
   Create a new SordNode from a SerdNode.

   The returned node must be freed using sord_node_free().
*/
SORD_API SordNode* SERD_ALLOCATED
sord_node_from_serd_node(SordWorld* SERD_NONNULL       world,
                         SerdEnv* SERD_NULLABLE        env,
                         const SerdNode* SERD_NULLABLE node,
                         const SerdNode* SERD_NULLABLE datatype,
                         const SerdNode* SERD_NULLABLE lang);

/**
   @}
   @name Model
   @{
*/

/**
   Create a new model.

   @param world The world in which to make this model.

   @param indices SordIndexOption flags (e.g. SORD_SPO|SORD_OPS).  Be sure to
   enable an index where the most significant node(s) are not variables in your
   queries (e.g. to make (? P O) queries, enable either SORD_OPS or SORD_POS).

   @param graphs If true, store (and index) graph contexts.
*/
SORD_API SordModel* SERD_ALLOCATED
sord_new(SordWorld* SERD_NONNULL world, unsigned indices, bool graphs);

/**
   Close and free `model`.
*/
SORD_API void
sord_free(SordModel* SERD_NULLABLE model);

/**
   Get the world associated with `model`.
*/
SORD_API SordWorld* SERD_NONNULL
sord_get_world(SordModel* SERD_NONNULL model);

/**
   Return the number of nodes stored in `world`.

   Nodes are included in this count iff they are a part of a quad in `world`.
*/
SORD_API size_t
sord_num_nodes(const SordWorld* SERD_NULLABLE world);

/**
   Return the number of quads stored in `model`.
*/
SORD_API size_t
sord_num_quads(const SordModel* SERD_NULLABLE model);

/**
   Return an iterator to the start of `model`.
*/
SORD_API SordIter* SERD_NULLABLE
sord_begin(const SordModel* SERD_NULLABLE model);

/**
   Search for statements by a quad pattern.
   @return an iterator to the first match, or NULL if no matches found.
*/
SORD_API SordIter* SERD_NULLABLE
sord_find(const SordModel* SERD_NONNULL model, const SordQuad SERD_NONNULL pat);

/**
   Search for statements by nodes.
   @return an iterator to the first match, or NULL if no matches found.
*/
SORD_API SordIter* SERD_NULLABLE
sord_search(const SordModel* SERD_NONNULL model,
            const SordNode* SERD_NULLABLE s,
            const SordNode* SERD_NULLABLE p,
            const SordNode* SERD_NULLABLE o,
            const SordNode* SERD_NULLABLE g);

/**
   Search for a single node that matches a pattern.
   Exactly one of `s`, `p`, `o` must be NULL.
   This function is mainly useful for predicates that only have one value.
   The returned node must be freed using sord_node_free().
   @return the first matching node, or NULL if no matches are found.
*/
SORD_API SordNode* SERD_NULLABLE
sord_get(const SordModel* SERD_NONNULL model,
         const SordNode* SERD_NULLABLE s,
         const SordNode* SERD_NULLABLE p,
         const SordNode* SERD_NULLABLE o,
         const SordNode* SERD_NULLABLE g);

/**
   Return true iff a statement exists.
*/
SORD_API bool
sord_ask(const SordModel* SERD_NONNULL model,
         const SordNode* SERD_NULLABLE s,
         const SordNode* SERD_NULLABLE p,
         const SordNode* SERD_NULLABLE o,
         const SordNode* SERD_NULLABLE g);

/**
   Return the number of matching statements.
*/
SORD_API uint64_t
sord_count(const SordModel* SERD_NONNULL model,
           const SordNode* SERD_NULLABLE s,
           const SordNode* SERD_NULLABLE p,
           const SordNode* SERD_NULLABLE o,
           const SordNode* SERD_NULLABLE g);

/**
   Check if `model` contains a triple pattern.

   @return true if `model` contains a match for `pat`, otherwise false.
*/
SORD_API bool
sord_contains(const SordModel* SERD_NONNULL model,
              const SordQuad SERD_NONNULL   pat);

/**
   Add a quad to a model.

   Calling this function invalidates all iterators on `model`.

   @return true on success, false, on error.
*/
SORD_API bool
sord_add(SordModel* SERD_NONNULL model, const SordQuad SERD_NONNULL tup);

/**
   Remove a quad from a model.

   Calling this function invalidates all iterators on `model`.  To remove quads
   while iterating, use sord_erase() instead.
*/
SORD_API void
sord_remove(SordModel* SERD_NONNULL model, const SordQuad SERD_NONNULL tup);

/**
   Remove a quad from a model via an iterator.

   Calling this function invalidates all iterators on `model` except `iter`.

   @param model The model which `iter` points to.
   @param iter Iterator to the element to erase, which is incremented to the
   next value on return.
*/
SORD_API SerdStatus
sord_erase(SordModel* SERD_NONNULL model, SordIter* SERD_NULLABLE iter);

/**
   @}
   @name Inserter
   @{
*/

/**
   Create an inserter for writing statements to a model.
*/
SORD_API SordInserter* SERD_ALLOCATED
sord_inserter_new(SordModel* SERD_NONNULL model, SerdEnv* SERD_NONNULL env);

/**
   Free an inserter.
*/
SORD_API void
sord_inserter_free(SordInserter* SERD_NULLABLE inserter);

/**
   Set the current base URI for writing to the model.

   Note this function can be safely casted to SerdBaseSink.
*/
SORD_API SerdStatus
sord_inserter_set_base_uri(SordInserter* SERD_NONNULL    inserter,
                           const SerdNode* SERD_NULLABLE uri);

/**
   Set a namespace prefix for writing to the model.

   Note this function can be safely casted to SerdPrefixSink.
*/
SORD_API SerdStatus
sord_inserter_set_prefix(SordInserter* SERD_NONNULL   inserter,
                         const SerdNode* SERD_NONNULL name,
                         const SerdNode* SERD_NONNULL uri);

/**
   Write a statement to the model.

   Note this function can be safely casted to SerdStatementSink.
*/
SORD_API SerdStatus
sord_inserter_write_statement(SordInserter* SERD_NONNULL    inserter,
                              SerdStatementFlags            flags,
                              const SerdNode* SERD_NULLABLE graph,
                              const SerdNode* SERD_NONNULL  subject,
                              const SerdNode* SERD_NONNULL  predicate,
                              const SerdNode* SERD_NONNULL  object,
                              const SerdNode* SERD_NULLABLE object_datatype,
                              const SerdNode* SERD_NULLABLE object_lang);

/**
   @}
   @name Iteration
   @{
*/

/**
   Set `quad` to the quad pointed to by `iter`.
*/
SORD_API void
sord_iter_get(const SordIter* SERD_UNSPECIFIED iter, SordQuad SERD_NONNULL tup);

/**
   Return a field of the quad pointed to by `iter`.

   Returns NULL if `iter` is NULL or is at the end.
*/
SORD_API const SordNode* SERD_UNSPECIFIED
sord_iter_get_node(const SordIter* SERD_UNSPECIFIED iter, SordQuadIndex index);

/**
   Return the store pointed to by `iter`.
*/
SORD_API const SordModel* SERD_UNSPECIFIED
sord_iter_get_model(SordIter* SERD_UNSPECIFIED iter);

/**
   Increment `iter` to point to the next statement.
*/
SORD_API bool
sord_iter_next(SordIter* SERD_NULLABLE iter);

/**
   Return true iff `iter` is at the end of its range.
*/
SORD_API bool
sord_iter_end(const SordIter* SERD_NULLABLE iter);

/**
   Free `iter`.
*/
SORD_API void
sord_iter_free(SordIter* SERD_NULLABLE iter);

/**
   @}
   @name Utilities
   @{
*/

/**
   Match two quads (using ID comparison only).

   This function is a straightforward and fast equivalence match with wildcard
   support (ID 0 is a wildcard). It does not actually read node data.
   @return true iff `x` and `y` match.
*/
SORD_API bool
sord_quad_match(const SordQuad SERD_NONNULL x, const SordQuad SERD_NONNULL y);

/**
   @}
   @name Serialisation
   @{
*/

/**
   Return a reader that will read into `model`.
*/
SORD_API SerdReader* SERD_ALLOCATED
sord_new_reader(SordModel* SERD_NONNULL       model,
                SerdEnv* SERD_NONNULL         env,
                SerdSyntax                    syntax,
                const SordNode* SERD_NULLABLE graph);

/**
   Write a model to a writer.
*/
SORD_API bool
sord_write(SordModel* SERD_NONNULL       model,
           SerdWriter* SERD_NONNULL      writer,
           const SordNode* SERD_NULLABLE graph);

/**
   Write a range to a writer.

   This increments `iter` to its end, then frees it.
*/
SORD_API bool
sord_write_iter(SordIter* SERD_NULLABLE iter, SerdWriter* SERD_NULLABLE writer);

/**
   @}
   @}
*/

#ifdef __cplusplus
} /* extern "C" */
#endif

#endif /* SORD_SORD_H */
