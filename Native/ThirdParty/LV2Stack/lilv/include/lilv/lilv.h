// Copyright 2007-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef LILV_LILV_H
#define LILV_LILV_H

#include <lv2/core/lv2.h>
#include <lv2/urid/urid.h>

#include <stdarg.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>

// LILV_API must be used to decorate things in the public API
#ifndef LILV_API
#  if defined(_WIN32) && !defined(LILV_STATIC) && defined(LILV_INTERNAL)
#    define LILV_API __declspec(dllexport)
#  elif defined(_WIN32) && !defined(LILV_STATIC)
#    define LILV_API __declspec(dllimport)
#  elif defined(__GNUC__)
#    define LILV_API __attribute__((visibility("default")))
#  else
#    define LILV_API
#  endif
#endif

#if defined(__clang__) && __clang_major__ >= 7
#  define LILV_NONNULL _Nonnull
#  define LILV_NULLABLE _Nullable
#  define LILV_ALLOCATED _Null_unspecified
#  define LILV_UNSPECIFIED _Null_unspecified
#else
#  define LILV_NONNULL
#  define LILV_NULLABLE
#  define LILV_ALLOCATED
#  define LILV_UNSPECIFIED
#endif

#if defined(__GNUC__) && \
  (__GNUC__ > 3 || (__GNUC__ == 3 && __GNUC_MINOR__ >= 1))
#  define LILV_DEPRECATED __attribute__((__deprecated__))
#else
#  define LILV_DEPRECATED
#endif

#ifdef __cplusplus
extern "C" {
#  if defined(__clang__)
#    pragma clang diagnostic push
#    pragma clang diagnostic ignored "-Wzero-as-null-pointer-constant"
#  endif
#endif

#define LILV_NS_DOAP "http://usefulinc.com/ns/doap#"
#define LILV_NS_FOAF "http://xmlns.com/foaf/0.1/"
#define LILV_NS_LILV "http://drobilla.net/ns/lilv#"
#define LILV_NS_LV2 "http://lv2plug.in/ns/lv2core#"
#define LILV_NS_OWL "http://www.w3.org/2002/07/owl#"
#define LILV_NS_RDF "http://www.w3.org/1999/02/22-rdf-syntax-ns#"
#define LILV_NS_RDFS "http://www.w3.org/2000/01/rdf-schema#"
#define LILV_NS_XSD "http://www.w3.org/2001/XMLSchema#"

#define LILV_URI_ATOM_PORT "http://lv2plug.in/ns/ext/atom#AtomPort"
#define LILV_URI_AUDIO_PORT "http://lv2plug.in/ns/lv2core#AudioPort"
#define LILV_URI_CONTROL_PORT "http://lv2plug.in/ns/lv2core#ControlPort"
#define LILV_URI_CV_PORT "http://lv2plug.in/ns/lv2core#CVPort"
#define LILV_URI_EVENT_PORT "http://lv2plug.in/ns/ext/event#EventPort"
#define LILV_URI_INPUT_PORT "http://lv2plug.in/ns/lv2core#InputPort"
#define LILV_URI_MIDI_EVENT "http://lv2plug.in/ns/ext/midi#MidiEvent"
#define LILV_URI_OUTPUT_PORT "http://lv2plug.in/ns/lv2core#OutputPort"
#define LILV_URI_PORT "http://lv2plug.in/ns/lv2core#Port"

struct LilvInstanceImpl;

/**
   @defgroup lilv Lilv C API
   This is the complete public C API of lilv.
   @{
*/

typedef struct LilvPluginImpl      LilvPlugin;      /**< LV2 Plugin. */
typedef struct LilvPluginClassImpl LilvPluginClass; /**< Plugin Class. */
typedef struct LilvPortImpl        LilvPort;        /**< Port. */
typedef struct LilvScalePointImpl  LilvScalePoint;  /**< Scale Point. */
typedef struct LilvUIImpl          LilvUI;          /**< Plugin UI. */
typedef struct LilvNodeImpl        LilvNode;        /**< Typed Value. */
typedef struct LilvWorldImpl       LilvWorld;       /**< Lilv World. */
typedef struct LilvInstanceImpl    LilvInstance;    /**< Plugin instance. */
typedef struct LilvStateImpl       LilvState;       /**< Plugin state. */

typedef void LilvIter;          /**< Collection iterator */
typedef void LilvPluginClasses; /**< A set of #LilvPluginClass. */
typedef void LilvPlugins;       /**< A set of #LilvPlugin. */
typedef void LilvScalePoints;   /**< A set of #LilvScalePoint. */
typedef void LilvUIs;           /**< A set of #LilvUI. */
typedef void LilvNodes;         /**< A set of #LilvNode. */

/**
   Free memory allocated by Lilv.

   This function exists because some systems require memory allocated by a
   library to be freed by code in the same library.  It is otherwise equivalent
   to the standard C free() function.
*/
LILV_API void
lilv_free(void* LILV_NULLABLE ptr);

/**
   @defgroup lilv_node Nodes
   @{
*/

/**
   Convert a file URI string to a local path string.

   For example, "file://foo/bar/baz.ttl" returns "/foo/bar/baz.ttl".
   Return value is shared and must not be deleted by caller.
   This function does not handle escaping correctly and should not be used for
   general file URIs.  Use lilv_file_uri_parse() instead.

   @return `uri` converted to a path, or NULL on failure (URI is not local).
*/
LILV_API LILV_DEPRECATED const char* LILV_NULLABLE
lilv_uri_to_path(const char* LILV_NONNULL uri);

/**
   Convert a file URI string to a local path string.

   For example, "file://foo/bar%20one/baz.ttl" returns "/foo/bar one/baz.ttl".
   Return value must be freed by caller with lilv_free().

   @param uri The file URI to parse.
   @param hostname If non-NULL, set to the hostname in the URI, if any.
   @return `uri` converted to a path, or NULL on failure (URI is not local).
*/
LILV_API char* LILV_ALLOCATED
lilv_file_uri_parse(const char* LILV_NONNULL              uri,
                    char* LILV_UNSPECIFIED* LILV_NULLABLE hostname);

/**
   Create a new URI value.

   Returned value must be freed by caller with lilv_node_free().
*/
LILV_API LilvNode* LILV_ALLOCATED
lilv_new_uri(LilvWorld* LILV_NONNULL world, const char* LILV_NONNULL uri);

/**
   Create a new file URI value.

   Relative paths are resolved against the current working directory.  Note
   that this may yield unexpected results if `host` is another machine.

   @param world The world.
   @param host Host name, or NULL.
   @param path Path on host.
   @return A new node that must be freed by caller.
*/
LILV_API LilvNode* LILV_ALLOCATED
lilv_new_file_uri(LilvWorld* LILV_NONNULL   world,
                  const char* LILV_NULLABLE host,
                  const char* LILV_NONNULL  path);

/**
   Create a new string value (with no language).

   Returned value must be freed by caller with lilv_node_free().
*/
LILV_API LilvNode* LILV_ALLOCATED
lilv_new_string(LilvWorld* LILV_NONNULL world, const char* LILV_NONNULL str);

/**
   Create a new integer value.

   Returned value must be freed by caller with lilv_node_free().
*/
LILV_API LilvNode* LILV_ALLOCATED
lilv_new_int(LilvWorld* LILV_NONNULL world, int val);

/**
   Create a new floating point value.

   Returned value must be freed by caller with lilv_node_free().
*/
LILV_API LilvNode* LILV_ALLOCATED
lilv_new_float(LilvWorld* LILV_NONNULL world, float val);

/**
   Create a new boolean value.

   Returned value must be freed by caller with lilv_node_free().
*/
LILV_API LilvNode* LILV_ALLOCATED
lilv_new_bool(LilvWorld* LILV_NONNULL world, bool val);

/**
   Free a LilvNode.

   It is safe to call this function on NULL.
*/
LILV_API void
lilv_node_free(LilvNode* LILV_NULLABLE val);

/**
   Duplicate a LilvNode.
*/
LILV_API LilvNode* LILV_ALLOCATED
lilv_node_duplicate(const LilvNode* LILV_NULLABLE val);

/**
   Return whether two values are equivalent.
*/
LILV_API bool
lilv_node_equals(const LilvNode* LILV_NULLABLE value,
                 const LilvNode* LILV_NULLABLE other);

/**
   Return this value as a Turtle/SPARQL token.

   Returned value must be freed by caller with lilv_free().

   Example tokens:

   - URI:     &lt;http://example.org/foo&gt;
   - QName:   doap:name
   - String:  "this is a string"
   - Float:   1.0
   - Integer: 1
   - Boolean: true
*/
LILV_API char* LILV_ALLOCATED
lilv_node_get_turtle_token(const LilvNode* LILV_NONNULL value);

/**
   Return whether the value is a URI (resource).
*/
LILV_API bool
lilv_node_is_uri(const LilvNode* LILV_NULLABLE value);

/**
   Return this value as a URI string, like "http://example.org/foo".

   Valid to call only if `lilv_node_is_uri(value)` returns true.
   Returned value is owned by `value` and must not be freed by caller.
*/
LILV_API const char* LILV_UNSPECIFIED
lilv_node_as_uri(const LilvNode* LILV_NULLABLE value);

/**
   Return whether the value is a blank node (resource with no URI).
*/
LILV_API bool
lilv_node_is_blank(const LilvNode* LILV_NULLABLE value);

/**
   Return this value as a blank node identifier, like "genid03".

   Valid to call only if `lilv_node_is_blank(value)` returns true.
   Returned value is owned by `value` and must not be freed by caller.
*/
LILV_API const char* LILV_UNSPECIFIED
lilv_node_as_blank(const LilvNode* LILV_NULLABLE value);

/**
   Return whether this value is a literal (that is, not a URI).

   Returns true if `value` is a string or numeric value.
*/
LILV_API bool
lilv_node_is_literal(const LilvNode* LILV_NULLABLE value);

/**
   Return whether this value is a string literal.

   Returns true if `value` is a string value (and not numeric).
*/
LILV_API bool
lilv_node_is_string(const LilvNode* LILV_NULLABLE value);

/**
   Return `value` as a string.
*/
LILV_API const char* LILV_UNSPECIFIED
lilv_node_as_string(const LilvNode* LILV_NULLABLE value);

/**
   Return the path of a file URI node.

   Returns NULL if `value` is not a file URI.
   Returned value must be freed by caller with lilv_free().
*/
LILV_API char* LILV_UNSPECIFIED
lilv_node_get_path(const LilvNode* LILV_NULLABLE         value,
                   char* LILV_UNSPECIFIED* LILV_NULLABLE hostname);

/**
   Return whether this value is a decimal literal.
*/
LILV_API bool
lilv_node_is_float(const LilvNode* LILV_NULLABLE value);

/**
   Return `value` as a float.

   Valid to call only if `lilv_node_is_float(value)` or
   `lilv_node_is_int(value)` returns true.
*/
LILV_API float
lilv_node_as_float(const LilvNode* LILV_NULLABLE value);

/**
   Return whether this value is an integer literal.
*/
LILV_API bool
lilv_node_is_int(const LilvNode* LILV_NULLABLE value);

/**
   Return `value` as an integer.

   Valid to call only if `lilv_node_is_int(value)` returns true.
*/
LILV_API int
lilv_node_as_int(const LilvNode* LILV_NULLABLE value);

/**
   Return whether this value is a boolean.
*/
LILV_API bool
lilv_node_is_bool(const LilvNode* LILV_NULLABLE value);

/**
   Return `value` as a bool.

   Valid to call only if `lilv_node_is_bool(value)` returns true.
*/
LILV_API bool
lilv_node_as_bool(const LilvNode* LILV_NULLABLE value);

/**
   @}
   @defgroup lilv_collections Collections

   Lilv has several collection types for holding various types of value.
   Each collection type supports a similar basic API, except #LilvPlugins which
   is internal and thus lacks a free function:

   - void PREFIX_free (coll)
   - unsigned PREFIX_size (coll)
   - LilvIter* PREFIX_begin (coll)

   The types of collection are:

   - LilvPlugins, with function prefix `lilv_plugins_`.
   - LilvPluginClasses, with function prefix `lilv_plugin_classes_`.
   - LilvScalePoints, with function prefix `lilv_scale_points_`.
   - LilvNodes, with function prefix `lilv_nodes_`.
   - LilvUIs, with function prefix `lilv_uis_`.

   @{
*/

/* Collections */

/**
   Iterate over each element of a collection.

   @code
   LILV_FOREACH(plugin_classes, i, classes) {
      LilvPluginClass c = lilv_plugin_classes_get(classes, i);
      // ...
   }
   @endcode
*/
#define LILV_FOREACH(colltype, iter, collection)             \
  /* NOLINTNEXTLINE(bugprone-macro-parentheses) */           \
  for (LilvIter* iter = lilv_##colltype##_begin(collection); \
       !lilv_##colltype##_is_end(collection, iter);          \
       (iter) = lilv_##colltype##_next(collection, iter))

/* LilvPluginClasses */

LILV_API void
lilv_plugin_classes_free(LilvPluginClasses* LILV_NULLABLE collection);

LILV_API unsigned
lilv_plugin_classes_size(const LilvPluginClasses* LILV_NULLABLE collection);

LILV_API LilvIter* LILV_NULLABLE
lilv_plugin_classes_begin(const LilvPluginClasses* LILV_NULLABLE collection);

LILV_API const LilvPluginClass* LILV_UNSPECIFIED
lilv_plugin_classes_get(const LilvPluginClasses* LILV_UNSPECIFIED collection,
                        const LilvIter* LILV_NULLABLE             i);

LILV_API LilvIter* LILV_NULLABLE
lilv_plugin_classes_next(const LilvPluginClasses* LILV_UNSPECIFIED collection,
                         LilvIter* LILV_NULLABLE                   i);

LILV_API bool
lilv_plugin_classes_is_end(const LilvPluginClasses* LILV_UNSPECIFIED collection,
                           const LilvIter* LILV_NULLABLE             i);

/**
   Get a plugin class from `classes` by URI.

   Return value is shared (stored in `classes`) and must not be freed or
   modified by the caller in any way.

   @return NULL if no plugin class with `uri` is found in `classes`.
*/
LILV_API const LilvPluginClass* LILV_NULLABLE
lilv_plugin_classes_get_by_uri(const LilvPluginClasses* LILV_NONNULL classes,
                               const LilvNode* LILV_NONNULL          uri);

/* ScalePoints */

LILV_API void
lilv_scale_points_free(LilvScalePoints* LILV_NULLABLE collection);

LILV_API unsigned
lilv_scale_points_size(const LilvScalePoints* LILV_NULLABLE collection);

LILV_API LilvIter* LILV_NULLABLE
lilv_scale_points_begin(const LilvScalePoints* LILV_NULLABLE collection);

LILV_API const LilvScalePoint* LILV_UNSPECIFIED
lilv_scale_points_get(const LilvScalePoints* LILV_UNSPECIFIED collection,
                      const LilvIter* LILV_NULLABLE           i);

LILV_API LilvIter* LILV_NULLABLE
lilv_scale_points_next(const LilvScalePoints* LILV_UNSPECIFIED collection,
                       LilvIter* LILV_NULLABLE                 i);

LILV_API bool
lilv_scale_points_is_end(const LilvScalePoints* LILV_UNSPECIFIED collection,
                         const LilvIter* LILV_NULLABLE           i);

/* UIs */

LILV_API void
lilv_uis_free(LilvUIs* LILV_NULLABLE collection);

LILV_API unsigned
lilv_uis_size(const LilvUIs* LILV_NULLABLE collection);

LILV_API LilvIter* LILV_NULLABLE
lilv_uis_begin(const LilvUIs* LILV_NULLABLE collection);

LILV_API const LilvUI* LILV_UNSPECIFIED
lilv_uis_get(const LilvUIs* LILV_UNSPECIFIED collection,
             const LilvIter* LILV_NULLABLE   i);

LILV_API LilvIter* LILV_NULLABLE
lilv_uis_next(const LilvUIs* LILV_UNSPECIFIED collection,
              LilvIter* LILV_NULLABLE         i);

LILV_API bool
lilv_uis_is_end(const LilvUIs* LILV_UNSPECIFIED collection,
                const LilvIter* LILV_NULLABLE   i);

/**
   Get a UI from `uis` by URI.

   Return value is shared (stored in `uis`) and must not be freed or
   modified by the caller in any way.

   @return NULL if no UI with `uri` is found in `list`.
*/
LILV_API const LilvUI* LILV_NULLABLE
lilv_uis_get_by_uri(const LilvUIs* LILV_NONNULL  uis,
                    const LilvNode* LILV_NONNULL uri);

/* Nodes */

LILV_API void
lilv_nodes_free(LilvNodes* LILV_NULLABLE collection);

LILV_API unsigned
lilv_nodes_size(const LilvNodes* LILV_NULLABLE collection);

LILV_API LilvIter* LILV_NULLABLE
lilv_nodes_begin(const LilvNodes* LILV_NULLABLE collection);

LILV_API const LilvNode* LILV_UNSPECIFIED
lilv_nodes_get(const LilvNodes* LILV_UNSPECIFIED collection,
               const LilvIter* LILV_NULLABLE     i);

LILV_API LilvIter* LILV_NULLABLE
lilv_nodes_next(const LilvNodes* LILV_UNSPECIFIED collection,
                LilvIter* LILV_NULLABLE           i);

LILV_API bool
lilv_nodes_is_end(const LilvNodes* LILV_UNSPECIFIED collection,
                  const LilvIter* LILV_NULLABLE     i);

LILV_API LilvNode* LILV_UNSPECIFIED
lilv_nodes_get_first(const LilvNodes* LILV_NULLABLE collection);

/**
   Return whether `values` contains `value`.
*/
LILV_API bool
lilv_nodes_contains(const LilvNodes* LILV_NONNULL nodes,
                    const LilvNode* LILV_NONNULL  value);

/**
   Return a new LilvNodes that contains all nodes from both `a` and `b`.
*/
LILV_API LilvNodes* LILV_ALLOCATED
lilv_nodes_merge(const LilvNodes* LILV_NULLABLE a,
                 const LilvNodes* LILV_NULLABLE b);

/* Plugins */

LILV_API unsigned
lilv_plugins_size(const LilvPlugins* LILV_NULLABLE collection);

LILV_API LilvIter* LILV_NULLABLE
lilv_plugins_begin(const LilvPlugins* LILV_NULLABLE collection);

LILV_API const LilvPlugin* LILV_UNSPECIFIED
lilv_plugins_get(const LilvPlugins* LILV_UNSPECIFIED collection,
                 const LilvIter* LILV_NULLABLE       i);

LILV_API LilvIter* LILV_NULLABLE
lilv_plugins_next(const LilvPlugins* LILV_UNSPECIFIED collection,
                  LilvIter* LILV_NULLABLE             i);

LILV_API bool
lilv_plugins_is_end(const LilvPlugins* LILV_UNSPECIFIED collection,
                    const LilvIter* LILV_UNSPECIFIED    i);

/**
   Get a plugin from `plugins` by URI.

   Return value is shared (stored in `plugins`) and must not be freed or
   modified by the caller in any way.

   @return NULL if no plugin with `uri` is found in `plugins`.
*/
LILV_API const LilvPlugin* LILV_UNSPECIFIED
lilv_plugins_get_by_uri(const LilvPlugins* LILV_NONNULL plugins,
                        const LilvNode* LILV_NONNULL    uri);

/**
   @}
   @defgroup lilv_world World

   The "world" represents all Lilv state, and is used to discover/load/cache
   LV2 data (plugins, UIs, and extensions).
   Normal hosts which just need to load plugins by URI should simply use
   lilv_world_load_all() to discover/load the system's LV2 resources.

   @{
*/

/**
   Initialize a new, empty world.

   If initialization fails, NULL is returned.
*/
LILV_API LilvWorld* LILV_ALLOCATED
lilv_world_new(void);

/**
   Enable/disable dynamic manifest support.

   Dynamic manifest data will only be loaded if this option is true.
*/
#define LILV_OPTION_DYN_MANIFEST "http://drobilla.net/ns/lilv#dyn-manifest"

/**
   Enable/disable language filtering.

   Language filtering applies to any functions that return (a) value(s).
   With filtering enabled, Lilv will automatically return the best value(s)
   for the current LANG.  With filtering disabled, all matching values will
   be returned regardless of language tag.  Filtering is enabled by default.
*/
#define LILV_OPTION_FILTER_LANG "http://drobilla.net/ns/lilv#filter-lang"

/**
   Set application-specific LANG.

   This overrides the LANG from the environment.
*/
#define LILV_OPTION_LANG "http://drobilla.net/ns/lilv#lang"

/**
   Set application-specific LV2_PATH.

   This overrides the LV2_PATH from the environment, so that lilv will only
   look inside the given path.  This can be used to make self-contained
   applications.
*/
#define LILV_OPTION_LV2_PATH "http://drobilla.net/ns/lilv#lv2-path"

/**
   Enable/disable object-first index for queries with subject wildcards.

   This enables additional indexing so that arbitrary query functions like
   lilv_world_find_nodes() efficiently support queries with subject wildcards,
   at the cost of increased load time and memory consumption.

   This option is enabled by default for backwards compatibility, although most
   applications can and should disable it to improve performance.
*/
#define LILV_OPTION_OBJECT_INDEX "http://drobilla.net/ns/lilv#object-index"

/**
   Set an option for `world`.

   For boolean options, passing a null value sets the option to false.  For
   options of any other type, null is considered invalid and will produce a
   warning.

   Currently recognized options:

   - #LILV_OPTION_DYN_MANIFEST
   - #LILV_OPTION_FILTER_LANG
   - #LILV_OPTION_LANG
   - #LILV_OPTION_LV2_PATH
   - #LILV_OPTION_OBJECT_INDEX
*/
LILV_API void
lilv_world_set_option(LilvWorld* LILV_NONNULL       world,
                      const char* LILV_NONNULL      uri,
                      const LilvNode* LILV_NULLABLE value);

/**
   Destroy the world.

   It is safe to call this function on NULL.  Note that destroying `world` will
   destroy all the objects it contains.  Do not destroy the world until you are
   finished with all objects that came from it.
*/
LILV_API void
lilv_world_free(LilvWorld* LILV_NULLABLE world);

/**
   Load all installed LV2 bundles on the system.

   This is the recommended way for hosts to load LV2 data.  It implements the
   established/standard best practice for discovering all LV2 data on the
   system.  The environment variable LV2_PATH may be used to control where
   this function will look for bundles.

   Hosts should use this function rather than explicitly load bundles, except
   in special circumstances such as development utilities, or hosts that ship
   with special plugin bundles which are installed to a known location.
*/
LILV_API void
lilv_world_load_all(LilvWorld* LILV_NONNULL world);

/**
   Load a specific bundle.

   `bundle_uri` must be a fully qualified URI to the bundle directory,
   with the trailing slash, eg. file:///usr/lib/lv2/foo.lv2/

   Normal hosts should not need this function (use lilv_world_load_all()).

   Hosts MUST NOT attach any long-term significance to bundle paths
   (for example in save files), since there are no guarantees they will remain
   unchanged between (or even during) program invocations. Plugins (among
   other things) MUST be identified by URIs (not paths) in save files.
*/
LILV_API void
lilv_world_load_bundle(LilvWorld* LILV_NONNULL      world,
                       const LilvNode* LILV_NONNULL bundle_uri);

/**
   Load all specifications from currently loaded bundles.

   This is for hosts that explicitly load specific bundles, its use is not
   necessary when using lilv_world_load_all().  This function parses the
   specifications and adds them to the model.
*/
LILV_API void
lilv_world_load_specifications(LilvWorld* LILV_NONNULL world);

/**
   Load all plugin classes from currently loaded specifications.

   Must be called after lilv_world_load_specifications().  This is for hosts
   that explicitly load specific bundles, its use is not necessary when using
   lilv_world_load_all().
*/
LILV_API void
lilv_world_load_plugin_classes(LilvWorld* LILV_NONNULL world);

/**
   Unload a specific bundle.

   This unloads statements loaded by lilv_world_load_bundle().  Note that this
   is not necessarily all information loaded from the bundle.  If any resources
   have been separately loaded with lilv_world_load_resource(), they must be
   separately unloaded with lilv_world_unload_resource().
*/
LILV_API int
lilv_world_unload_bundle(LilvWorld* LILV_NONNULL          world,
                         const LilvNode* LILV_UNSPECIFIED bundle_uri);

/**
   Load all the data associated with the given `resource`.

   All accessible data files linked to `resource` with rdfs:seeAlso will be
   loaded into the world model.

   @param world The world.
   @param resource Must be a subject (a URI or a blank node).
   @return The number of files parsed, or -1 on error
*/
LILV_API int
lilv_world_load_resource(LilvWorld* LILV_NONNULL      world,
                         const LilvNode* LILV_NONNULL resource);

/**
   Unload all the data associated with the given `resource`.

   This unloads all data loaded by a previous call to
   lilv_world_load_resource() with the given `resource`.

   @param world The world.
   @param resource Must be a subject (a URI or a blank node).
*/
LILV_API int
lilv_world_unload_resource(LilvWorld* LILV_NONNULL      world,
                           const LilvNode* LILV_NONNULL resource);

/**
   Get the parent of all other plugin classes, lv2:Plugin.
*/
LILV_API const LilvPluginClass* LILV_NONNULL
lilv_world_get_plugin_class(const LilvWorld* LILV_NONNULL world);

/**
   Return a list of all found plugin classes.

   Returned list is owned by world and must not be freed by the caller.
*/
LILV_API const LilvPluginClasses* LILV_NONNULL
lilv_world_get_plugin_classes(const LilvWorld* LILV_NONNULL world);

/**
   Return a list of all found plugins.

   The returned list contains just enough references to query
   or instantiate plugins.  The data for a particular plugin will not be
   loaded into memory until a call to an lilv_plugin_* function results in
   a query (at which time the data is cached with the LilvPlugin so future
   queries are very fast).

   The returned list and the plugins it contains are owned by `world`
   and must not be freed by caller.
*/
LILV_API const LilvPlugins* LILV_NONNULL
lilv_world_get_all_plugins(const LilvWorld* LILV_NONNULL world);

/**
   Find nodes matching a triple pattern.

   Either `subject` or `object` may be NULL (a wildcard), but not both.

   @return All matches for the wildcard field, or NULL.
*/
LILV_API LilvNodes* LILV_NULLABLE
lilv_world_find_nodes(LilvWorld* LILV_NONNULL          world,
                      const LilvNode* LILV_NULLABLE    subject,
                      const LilvNode* LILV_UNSPECIFIED predicate,
                      const LilvNode* LILV_NULLABLE    object);

/**
   Find a single node that matches a pattern.

   Either `subject` or `object` may be NULL (a wildcard), but not both.  This
   is simplified alternative to lilv_world_find_nodes() and
   lilv_nodes_get_first() for when only a single value is needed.

   @return The first matching node, or NULL if no matches are found.
*/
LILV_API LilvNode* LILV_NULLABLE
lilv_world_get(LilvWorld* LILV_NONNULL       world,
               const LilvNode* LILV_NULLABLE subject,
               const LilvNode* LILV_NONNULL  predicate,
               const LilvNode* LILV_NULLABLE object);

/**
   Return true iff a statement matching a certain pattern exists.

   This is useful for checking if particular statement exists without having to
   bother with collections and memory management.

   @param world The world.
   @param subject Subject of statement, or NULL for anything.
   @param predicate Predicate (key) of statement, or NULL for anything.
   @param object Object (value) of statement, or NULL for anything.
*/
LILV_API bool
lilv_world_ask(LilvWorld* LILV_NONNULL       world,
               const LilvNode* LILV_NULLABLE subject,
               const LilvNode* LILV_NULLABLE predicate,
               const LilvNode* LILV_NULLABLE object);

/**
   Get an LV2 symbol for some subject.

   This will return the lv2:symbol property of the subject if it is given
   explicitly, and otherwise will attempt to derive a symbol from the URI.

   @return A string node that is a valid LV2 symbol, or NULL on error.
*/
LILV_API LilvNode* LILV_NULLABLE
lilv_world_get_symbol(LilvWorld* LILV_NONNULL      world,
                      const LilvNode* LILV_NONNULL subject);

/**
   @}
   @defgroup lilv_plugin Plugins
   @{
*/

/**
   Check if `plugin` is valid.

   This is not a rigorous validator, but can be used to reject some malformed
   plugins that could cause bugs (for example, plugins with missing required
   fields).

   Note that normal hosts do NOT need to use this - lilv does not
   load invalid plugins into plugin lists.  This is included for plugin
   testing utilities, etc.

   @return True iff `plugin` is valid.
*/
LILV_API bool
lilv_plugin_verify(const LilvPlugin* LILV_NONNULL plugin);

/**
   Get the URI of `plugin`.

   Any serialization that refers to plugins should refer to them by this.
   Hosts SHOULD NOT save any filesystem paths, plugin indexes, etc. in saved
   files; save only the URI.

   The URI is a globally unique identifier for one specific plugin.  Two
   plugins with the same URI are compatible in port signature, and should
   be guaranteed to work in a compatible and consistent way.  If a plugin
   is upgraded in an incompatible way (eg if it has different ports), it
   MUST have a different URI than it's predecessor.

   @return A shared URI value which must not be modified or freed.
*/
LILV_API const LilvNode* LILV_NONNULL
lilv_plugin_get_uri(const LilvPlugin* LILV_NONNULL plugin);

/**
   Get the (resolvable) URI of the plugin's "main" bundle.

   This returns the URI of the bundle where the plugin itself was found.  Note
   that the data for a plugin may be spread over many bundles, that is,
   lilv_plugin_get_data_uris() may return URIs which are not within this
   bundle.

   Typical hosts should not need to use this function.
   Note this always returns a fully qualified URI.  If you want a local
   filesystem path, use lilv_file_uri_parse().

   @return A shared string which must not be modified or freed.
*/
LILV_API const LilvNode* LILV_NONNULL
lilv_plugin_get_bundle_uri(const LilvPlugin* LILV_NONNULL plugin);

/**
   Get the (resolvable) URIs of the RDF data files that define a plugin.

   Typical hosts should not need to use this function.
   Note this always returns fully qualified URIs.  If you want local
   filesystem paths, use lilv_file_uri_parse().

   @return A list of complete URLs eg. "file:///foo/ABundle.lv2/aplug.ttl",
   which is shared and must not be modified or freed.
*/
LILV_API const LilvNodes* LILV_UNSPECIFIED
lilv_plugin_get_data_uris(const LilvPlugin* LILV_NONNULL plugin);

/**
   Get the (resolvable) URI of the shared library for `plugin`.

   Note this always returns a fully qualified URI.  If you want a local
   filesystem path, use lilv_file_uri_parse().

   @return A shared string which must not be modified or freed.
*/
LILV_API const LilvNode* LILV_NULLABLE
lilv_plugin_get_library_uri(const LilvPlugin* LILV_NONNULL plugin);

/**
   Get the name of `plugin`.

   This returns the name (doap:name) of the plugin.  The name may be
   translated according to the current locale, this value MUST NOT be used
   as a plugin identifier (use the URI for that).

   Returned value must be freed by the caller.
*/
LILV_API LilvNode* LILV_NULLABLE
lilv_plugin_get_name(const LilvPlugin* LILV_NONNULL plugin);

/**
   Get the class this plugin belongs to (like "Filters" or "Effects").
*/
LILV_API const LilvPluginClass* LILV_NONNULL
lilv_plugin_get_class(const LilvPlugin* LILV_NONNULL plugin);

/**
   Get a value associated with the plugin in a plugin's data files.

   `predicate` must be either a URI or a QName.

   Returns the ?object of all triples found of the form:

   <code>&lt;plugin-uri&gt; predicate ?object</code>

   May return NULL if the property was not found, or if object(s) is not
   sensibly represented as a LilvNodes.

   Return value must be freed by caller with lilv_nodes_free().
*/
LILV_API LilvNodes* LILV_NULLABLE
lilv_plugin_get_value(const LilvPlugin* LILV_NONNULL plugin,
                      const LilvNode* LILV_NONNULL   predicate);

/**
   Return whether a feature is supported by a plugin.

   This will return true if the feature is an optional or required feature
   of the plugin.
*/
LILV_API bool
lilv_plugin_has_feature(const LilvPlugin* LILV_NONNULL plugin,
                        const LilvNode* LILV_NONNULL   feature);

/**
   Get the LV2 Features supported (required or optionally) by a plugin.

   A feature is "supported" by a plugin if it is required OR optional.

   Since required features have special rules the host must obey, this function
   probably shouldn't be used by normal hosts.  Using
   lilv_plugin_get_optional_features() and lilv_plugin_get_required_features()
   separately is best in most cases.

   Returned value must be freed by caller with lilv_nodes_free().
*/
LILV_API LilvNodes* LILV_NONNULL
lilv_plugin_get_supported_features(const LilvPlugin* LILV_NONNULL plugin);

/**
   Get the LV2 Features required by a plugin.

   If a feature is required by a plugin, hosts MUST NOT use the plugin if they
   do not understand (or are unable to support) that feature.

   All values returned here MUST be passed to the plugin's instantiate method
   (along with data, if necessary, as defined by the feature specification)
   or plugin instantiation will fail.

   Return value must be freed by caller with lilv_nodes_free().
*/
LILV_API LilvNodes* LILV_NULLABLE
lilv_plugin_get_required_features(const LilvPlugin* LILV_NONNULL plugin);

/**
   Get the LV2 Features optionally supported by a plugin.

   Hosts MAY ignore optional plugin features for whatever reasons.  Plugins
   MUST operate (at least somewhat) if they are instantiated without being
   passed optional features.

   Return value must be freed by caller with lilv_nodes_free().
*/
LILV_API LilvNodes* LILV_NULLABLE
lilv_plugin_get_optional_features(const LilvPlugin* LILV_NONNULL plugin);

/**
   Return whether or not a plugin provides a specific extension data.
*/
LILV_API bool
lilv_plugin_has_extension_data(const LilvPlugin* LILV_NONNULL plugin,
                               const LilvNode* LILV_NONNULL   uri);

/**
   Get a sequence of all extension data provided by a plugin.

   This can be used to find which URIs lilv_instance_get_extension_data()
   will return a value for without instantiating the plugin.
*/
LILV_API LilvNodes* LILV_NULLABLE
lilv_plugin_get_extension_data(const LilvPlugin* LILV_NONNULL plugin);

/**
   Get the number of ports on this plugin.
*/
LILV_API uint32_t
lilv_plugin_get_num_ports(const LilvPlugin* LILV_NONNULL plugin);

/**
   Get the port ranges (minimum, maximum and default values) for all ports.

   `min_values`, `max_values` and `def_values` must either point to an array
   of N floats, where N is the value returned by lilv_plugin_get_num_ports()
   for this plugin, or NULL.  The elements of the array will be set to the
   the minimum, maximum and default values of the ports on this plugin,
   with array index corresponding to port index.  If a port doesn't have a
   minimum, maximum or default value, or the port's type is not float, the
   corresponding array element will be set to NAN.

   This is a convenience method for the common case of getting the range of
   all float ports on a plugin, and may be significantly faster than
   repeated calls to lilv_port_get_range().
*/
LILV_API void
lilv_plugin_get_port_ranges_float(const LilvPlugin* LILV_NONNULL plugin,
                                  float* LILV_NULLABLE           min_values,
                                  float* LILV_NULLABLE           max_values,
                                  float* LILV_NULLABLE           def_values);

/**
   Get the number of ports on this plugin that are members of some class(es).

   Note that this is a varargs function so ports fitting any type 'profile'
   desired can be found quickly.  REMEMBER TO TERMINATE THE PARAMETER LIST
   OF THIS FUNCTION WITH NULL OR VERY NASTY THINGS WILL HAPPEN.
*/
LILV_API uint32_t
lilv_plugin_get_num_ports_of_class(const LilvPlugin* LILV_NONNULL plugin,
                                   const LilvNode* LILV_NONNULL   class_1,
                                   ...);

/**
   Variant of lilv_plugin_get_num_ports_of_class() that takes a va_list.

   This function calls va_arg() on `args` but does not call va_end().
*/
LILV_API uint32_t
lilv_plugin_get_num_ports_of_class_va(const LilvPlugin* LILV_NONNULL plugin,
                                      const LilvNode* LILV_NONNULL   class_1,
                                      va_list                        args);

/**
   Return whether or not the plugin introduces (and reports) latency.

   The index of the latency port can be found with
   lilv_plugin_get_latency_port() ONLY if this function returns true.
*/
LILV_API bool
lilv_plugin_has_latency(const LilvPlugin* LILV_NONNULL plugin);

/**
   Return the index of the plugin's latency port.

   It is a fatal error to call this on a plugin without checking if the port
   exists by first calling lilv_plugin_has_latency().

   Any plugin that introduces unwanted latency that should be compensated for
   (by hosts with the ability/need) MUST provide this port, which is a control
   rate output port that reports the latency for each cycle in frames.
*/
LILV_API uint32_t
lilv_plugin_get_latency_port_index(const LilvPlugin* LILV_NONNULL plugin);

/**
   Get a port on `plugin` by `index`.
*/
LILV_API const LilvPort* LILV_UNSPECIFIED
lilv_plugin_get_port_by_index(const LilvPlugin* LILV_NONNULL plugin,
                              uint32_t                       index);

/**
   Get a port on `plugin` by `symbol`.

   Note this function is slower than lilv_plugin_get_port_by_index(),
   especially on plugins with a very large number of ports.
*/
LILV_API const LilvPort* LILV_NULLABLE
lilv_plugin_get_port_by_symbol(const LilvPlugin* LILV_NONNULL plugin,
                               const LilvNode* LILV_NONNULL   symbol);

/**
   Get a port on `plugin` by its lv2:designation.

   The designation of a port describes the meaning, assignment, allocation or
   role of the port, like "left channel" or "gain".  If found, the port with
   matching `port_class` and `designation` is be returned, otherwise NULL is
   returned.  The `port_class` can be used to distinguish the input and output
   ports for a particular designation.  If `port_class` is NULL, any port with
   the given designation will be returned.
*/
LILV_API const LilvPort* LILV_NULLABLE
lilv_plugin_get_port_by_designation(const LilvPlugin* LILV_NONNULL plugin,
                                    const LilvNode* LILV_NONNULL   port_class,
                                    const LilvNode* LILV_NONNULL   designation);

/**
   Get the project the plugin is a part of.

   More information about the project can be read via lilv_world_find_nodes(),
   typically using properties from DOAP (such as doap:name).
*/
LILV_API LilvNode* LILV_NULLABLE
lilv_plugin_get_project(const LilvPlugin* LILV_NONNULL plugin);

/**
   Get the full name of the plugin's author.

   Returns NULL if author name is not present.
   Returned value must be freed by caller.
*/
LILV_API LilvNode* LILV_NULLABLE
lilv_plugin_get_author_name(const LilvPlugin* LILV_NONNULL plugin);

/**
   Get the email address of the plugin's author.

   Returns NULL if author email address is not present.
   Returned value must be freed by caller.
*/
LILV_API LilvNode* LILV_NULLABLE
lilv_plugin_get_author_email(const LilvPlugin* LILV_NONNULL plugin);

/**
   Get the address of the plugin author's home page.

   Returns NULL if author homepage is not present.
   Returned value must be freed by caller.
*/
LILV_API LilvNode* LILV_NULLABLE
lilv_plugin_get_author_homepage(const LilvPlugin* LILV_NONNULL plugin);

/**
   Return true iff `plugin` has been replaced by another plugin.

   The plugin will still be usable, but hosts should hide them from their
   user interfaces to prevent users from using deprecated plugins.
*/
LILV_API bool
lilv_plugin_is_replaced(const LilvPlugin* LILV_NONNULL plugin);

/**
   Write the Turtle description of `plugin` to `plugin_file`.

   This function is particularly useful for porting plugins in conjunction with
   an LV2 bridge such as NASPRO.
*/
LILV_API void
lilv_plugin_write_description(LilvWorld* LILV_NONNULL        world,
                              const LilvPlugin* LILV_NONNULL plugin,
                              const LilvNode* LILV_NONNULL   base_uri,
                              FILE* LILV_NONNULL             plugin_file);

/**
   Write a manifest entry for `plugin` to `manifest_file`.

   This function is intended for use with lilv_plugin_write_description() to
   write a complete description of a plugin to a bundle.
*/
LILV_API void
lilv_plugin_write_manifest_entry(LilvWorld* LILV_NONNULL        world,
                                 const LilvPlugin* LILV_NONNULL plugin,
                                 const LilvNode* LILV_NONNULL   base_uri,
                                 FILE* LILV_NONNULL             manifest_file,
                                 const char* LILV_NONNULL plugin_file_path);

/**
   Get the resources related to `plugin` with lv2:appliesTo.

   Some plugin-related resources are not linked directly to the plugin with
   rdfs:seeAlso and thus will not be automatically loaded along with the plugin
   data (usually for performance reasons).  All such resources of the given @c
   type related to `plugin` can be accessed with this function.

   If `type` is NULL, all such resources will be returned, regardless of type.

   To actually load the data for each returned resource, use
   lilv_world_load_resource().
*/
LILV_API LilvNodes* LILV_NULLABLE
lilv_plugin_get_related(const LilvPlugin* LILV_NONNULL plugin,
                        const LilvNode* LILV_NULLABLE  type);

/**
   @}
   @defgroup lilv_port Ports
   @{
*/

/**
   Get the RDF node of `port`.

   Ports nodes may be may be URIs or blank nodes.

   @return A shared node which must not be modified or freed.
*/
LILV_API const LilvNode* LILV_NONNULL
lilv_port_get_node(const LilvPlugin* LILV_NONNULL plugin,
                   const LilvPort* LILV_NONNULL   port);

/**
   Port analog of lilv_plugin_get_value().
*/
LILV_API LilvNodes* LILV_NULLABLE
lilv_port_get_value(const LilvPlugin* LILV_NONNULL plugin,
                    const LilvPort* LILV_NONNULL   port,
                    const LilvNode* LILV_NONNULL   predicate);

/**
   Get a single property value of a port.

   This is equivalent to lilv_nodes_get_first(lilv_port_get_value(...)) but is
   simpler to use in the common case of only caring about one value.  The
   caller is responsible for freeing the returned node.
*/
LILV_API LilvNode* LILV_NULLABLE
lilv_port_get(const LilvPlugin* LILV_NONNULL plugin,
              const LilvPort* LILV_NONNULL   port,
              const LilvNode* LILV_NONNULL   predicate);

/**
   Return the LV2 port properties of a port.
*/
LILV_API LilvNodes* LILV_NULLABLE
lilv_port_get_properties(const LilvPlugin* LILV_NONNULL plugin,
                         const LilvPort* LILV_NONNULL   port);

/**
   Return whether a port has a certain property.
*/
LILV_API bool
lilv_port_has_property(const LilvPlugin* LILV_NONNULL plugin,
                       const LilvPort* LILV_NONNULL   port,
                       const LilvNode* LILV_NONNULL   property);

/**
   Return whether a port supports a certain event type.

   More precisely, this returns true iff the port has an atom:supports or an
   ev:supportsEvent property with `event_type` as the value.
*/
LILV_API bool
lilv_port_supports_event(const LilvPlugin* LILV_NONNULL plugin,
                         const LilvPort* LILV_NONNULL   port,
                         const LilvNode* LILV_NONNULL   event_type);

/**
   Get the index of a port.

   The index is only valid for the life of the plugin and may change between
   versions.  For a stable identifier, use the symbol.
*/
LILV_API uint32_t
lilv_port_get_index(const LilvPlugin* LILV_NONNULL plugin,
                    const LilvPort* LILV_NONNULL   port);

/**
   Get the symbol of a port.

   The 'symbol' is a short string, a valid C identifier.
   Returned value is owned by `port` and must not be freed.
*/
LILV_API const LilvNode* LILV_NONNULL
lilv_port_get_symbol(const LilvPlugin* LILV_NONNULL plugin,
                     const LilvPort* LILV_NONNULL   port);

/**
   Get the name of a port.

   This is guaranteed to return the untranslated name (the doap:name in the
   data file without a language tag).  Returned value must be freed by
   the caller.
*/
LILV_API LilvNode* LILV_NULLABLE
lilv_port_get_name(const LilvPlugin* LILV_NONNULL plugin,
                   const LilvPort* LILV_NONNULL   port);

/**
   Get all the classes of a port.

   This can be used to determine if a port is an input, output, audio,
   control, midi, etc, etc, though it's simpler to use lilv_port_is_a().
   The returned list does not include lv2:Port, which is implied.
   Returned value is shared and must not be destroyed by caller.
*/
LILV_API const LilvNodes* LILV_NONNULL
lilv_port_get_classes(const LilvPlugin* LILV_NONNULL plugin,
                      const LilvPort* LILV_NONNULL   port);

/**
   Determine if a port is of a given class (input, output, audio, etc).

   For convenience/performance/extensibility reasons, hosts are expected to
   create a LilvNode for each port class they "care about".  Well-known type
   URI strings like `LILV_URI_INPUT_PORT` are defined for convenience, but
   this function is designed so that Lilv is usable with any port types
   without requiring explicit support in Lilv.
*/
LILV_API bool
lilv_port_is_a(const LilvPlugin* LILV_NONNULL plugin,
               const LilvPort* LILV_NONNULL   port,
               const LilvNode* LILV_NONNULL   port_class);

/**
   Get the default, minimum, and maximum values of a port.

   `def`, `min`, and `max` are outputs, pass pointers to uninitialized
   LilvNode* variables.  These will be set to point at new values (which must
   be freed by the caller using lilv_node_free()), or NULL if the value does
   not exist.
*/
LILV_API void
lilv_port_get_range(const LilvPlugin* LILV_NONNULL        plugin,
                    const LilvPort* LILV_NONNULL          port,
                    LilvNode* LILV_NONNULL* LILV_NULLABLE def,
                    LilvNode* LILV_NONNULL* LILV_NULLABLE min,
                    LilvNode* LILV_NONNULL* LILV_NULLABLE max);

/**
   Get the scale points (enumeration values) of a port.

   This returns a collection of 'interesting' named values of a port, which for
   example might be appropriate entries for a value selector in a UI.

   Returned value may be NULL if `port` has no scale points, otherwise it
   must be freed by caller with lilv_scale_points_free().
*/
LILV_API LilvScalePoints* LILV_NULLABLE
lilv_port_get_scale_points(const LilvPlugin* LILV_NONNULL plugin,
                           const LilvPort* LILV_NONNULL   port);

/**
   @}
   @defgroup lilv_state Plugin State
   @{
*/

/**
   Load a state snapshot from the world RDF model.

   If `node` is a known plugin URI, then that plugin's default state is
   returned.  Otherwise, the saved state or preset `node` (which lv2:appliesTo
   some plugin, has an rdfs:label, and so on) is returned.

   @param world The world.
   @param map URID mapper.
   @param node The subject of the state description (such as a preset URI).
   @return A new LilvState which must be freed with lilv_state_free(), or NULL.
*/
LILV_API LilvState* LILV_ALLOCATED
lilv_state_new_from_world(LilvWorld* LILV_NONNULL      world,
                          LV2_URID_Map* LILV_NONNULL   map,
                          const LilvNode* LILV_NONNULL node);

/**
   Load a state snapshot from a file or directory.

   If no `subject` is given, then it will be automatically determined from
   `path`, possibly by loading the file and investigating its contents for a
   unique instance of a plugin or preset.

   @param world The world.
   @param map URID mapper.
   @param subject The subject of the state description (such as a preset URI).
   @param path The path of the file containing the state description.
   @return A new LilvState which must be freed with lilv_state_free().
*/
LILV_API LilvState* LILV_ALLOCATED
lilv_state_new_from_file(LilvWorld* LILV_NONNULL       world,
                         LV2_URID_Map* LILV_NONNULL    map,
                         const LilvNode* LILV_NULLABLE subject,
                         const char* LILV_NONNULL      path);

/**
   Load a state snapshot from a string made by lilv_state_to_string().
*/
LILV_API LilvState* LILV_ALLOCATED
lilv_state_new_from_string(LilvWorld* LILV_NONNULL    world,
                           LV2_URID_Map* LILV_NONNULL map,
                           const char* LILV_NONNULL   str);

/**
   Function to get a port value.

   This function MUST set `size` and `type` appropriately.

   @param port_symbol The symbol of the port.
   @param user_data The user_data passed to lilv_state_new_from_instance().
   @param size (Output) The size of the returned value.
   @param type (Output) The URID of the type of the returned value.
   @return A pointer to the port value.
*/
typedef const void* LILV_NULLABLE (*LilvGetPortValueFunc)(
  const char* LILV_NONNULL port_symbol,
  void* LILV_UNSPECIFIED   user_data,
  uint32_t* LILV_NONNULL   size,
  uint32_t* LILV_NONNULL   type);

/**
   Create a new state snapshot from a plugin instance.


   This function may be called simultaneously with any instance function
   (except discovery functions) unless the threading class of that function
   explicitly disallows this.

   To support advanced file functionality, there are several directory
   parameters.  The multiple parameters are necessary to support saving an
   instance's state many times, or saving states from multiple instances, while
   avoiding any duplication of data.  For example, a host could pass the same
   `copy_dir` and `link_dir` for all plugins in a session (for example
   `session/shared/copy/` `session/shared/link/`), while the `save_dir` would
   be unique to each plugin instance (for example `session/states/state1.lv2`
   for one instance and `session/states/state2.lv2` for another instance).
   Simple hosts that only wish to save a single plugin's state once may simply
   use the same directory for all of them, or pass NULL to not support files at
   all.

   If supported (via state:makePath passed to LV2_Descriptor::instantiate()),
   `scratch_dir` should be the directory where any files created by the plugin
   (for example during instantiation or while running) are stored.  Any files
   here that are referred to in the state will be copied to preserve their
   contents at the time of the save.  Lilv will assume any files within this
   directory (recursively) are created by the plugin and that all other files
   are immutable.  Note that this function does not completely save the state,
   use lilv_state_save() for that.

   See <a href="http://lv2plug.in/ns/ext/state/state.h">state.h</a> from the
   LV2 State extension for details on the `flags` and `features` parameters.

   @param plugin The plugin this state applies to.

   @param instance An instance of `plugin`.

   @param map The map to use for mapping URIs in state.

   @param scratch_dir Directory of files created by the plugin earlier, or
   NULL.  This is for hosts that support file creation at any time with state
   state:makePath.  These files will be copied as necessary to `copy_dir` and
   not be referred to directly in state (a temporary directory is appropriate).

   @param copy_dir Directory of copies of files in `scratch_dir`, or NULL.
   This directory will have the same structure as `scratch_dir` but with
   possibly modified file names to distinguish revisions.  This allows the
   saved state to contain the exact contents of the scratch file at save time,
   so that the state is not ruined if the file is later modified (for example,
   by the plugin continuing to record).  This can be the same as `save_dir` to
   create a copy in the state bundle, but can also be a separate directory
   which allows multiple state snapshots to share a single copy if the file has
   not changed.

   @param link_dir Directory of links to external files, or NULL.  Links will
   be made here to any external file referred to in plugin state.  In turn,
   these will be linked to from `save_dir` (like save_dir/file => link_dir/file
   => /some/file).  This allows several state snapshots to share links to
   external files, so an archive (created by naive tools like `tar`) will only
   contain one copy of the file.  This can be the same as `save_dir` to link
   directly from there (without sharing).  If NULL, the state will contain
   absolute paths to any any external files.

   @param save_dir Directory of files created by plugin during save (or NULL).
   This is typically the bundle directory later passed to lilv_state_save().

   @param get_value Function to get port values (or NULL).  If NULL, the
   returned state will not represent port values.  This should only be NULL in
   hosts that save and restore port values via some other mechanism.

   @param user_data User data to pass to `get_value`.

   @param flags Bitwise OR of LV2_State_Flags values.

   @param features Features to pass LV2_State_Interface.save().

   @return A new LilvState which must be freed with lilv_state_free().
*/
LILV_API LilvState* LILV_ALLOCATED
lilv_state_new_from_instance(
  const LilvPlugin* LILV_NONNULL                           plugin,
  LilvInstance* LILV_NONNULL                               instance,
  LV2_URID_Map* LILV_NONNULL                               map,
  const char* LILV_NULLABLE                                scratch_dir,
  const char* LILV_NULLABLE                                copy_dir,
  const char* LILV_NULLABLE                                link_dir,
  const char* LILV_NULLABLE                                save_dir,
  LilvGetPortValueFunc LILV_NULLABLE                       get_value,
  void* LILV_UNSPECIFIED                                   user_data,
  uint32_t                                                 flags,
  const LV2_Feature* LILV_NULLABLE const* LILV_UNSPECIFIED features);

/**
   Free `state`.
*/
LILV_API void
lilv_state_free(LilvState* LILV_NULLABLE state);

/**
   Return true iff `a` is equivalent to `b`.
*/
LILV_API bool
lilv_state_equals(const LilvState* LILV_NONNULL a,
                  const LilvState* LILV_NONNULL b);

/**
   Return the number of properties in `state`.
*/
LILV_API unsigned
lilv_state_get_num_properties(const LilvState* LILV_NONNULL state);

/**
   Get the URI of the plugin `state` applies to.
*/
LILV_API const LilvNode* LILV_NONNULL
lilv_state_get_plugin_uri(const LilvState* LILV_NONNULL state);

/**
   Get the URI of `state`.

   This may return NULL if the state has not been saved and has no URI.
*/
LILV_API const LilvNode* LILV_UNSPECIFIED
lilv_state_get_uri(const LilvState* LILV_NONNULL state);

/**
   Get the label of `state`.
*/
LILV_API const char* LILV_UNSPECIFIED
lilv_state_get_label(const LilvState* LILV_NONNULL state);

/**
   Set the label of `state`.
*/
LILV_API void
lilv_state_set_label(LilvState* LILV_NONNULL  state,
                     const char* LILV_NONNULL label);

/**
   Set a metadata property on `state`.

   This is a generic version of lilv_state_set_label(), which sets metadata
   properties visible to hosts, but not plugins.  This allows storing useful
   information such as comments or preset banks.

   @param state The state to set the metadata for.
   @param key The key to store `value` under (URID).
   @param value Pointer to the value to be stored.
   @param size The size of `value` in bytes.
   @param type The type of `value` (URID).
   @param flags LV2_State_Flags for `value`.
   @return Zero on success.
*/
LILV_API int
lilv_state_set_metadata(LilvState* LILV_NONNULL  state,
                        uint32_t                 key,
                        const void* LILV_NONNULL value,
                        size_t                   size,
                        uint32_t                 type,
                        uint32_t                 flags);

/**
   Function to set a port value.

   @param port_symbol The symbol of the port.
   @param user_data The user_data passed to lilv_state_restore().
   @param size The size of `value`.
   @param type The URID of the type of `value`.
   @param value A pointer to the port value.
*/
typedef void (*LilvSetPortValueFunc)(const char* LILV_NONNULL port_symbol,
                                     void* LILV_UNSPECIFIED   user_data,
                                     const void* LILV_NONNULL value,
                                     uint32_t                 size,
                                     uint32_t                 type);

/**
   Enumerate the port values in a state snapshot.

   This function is a subset of lilv_state_restore() that only fires the
   `set_value` callback and does not directly affect a plugin instance.  This
   is useful in hosts that need to retrieve the port values in a state snapshot
   for special handling.

   @param state The state to retrieve port values from.
   @param set_value A function to receive port values.
   @param user_data User data to pass to `set_value`.
*/
LILV_API void
lilv_state_emit_port_values(const LilvState* LILV_NONNULL     state,
                            LilvSetPortValueFunc LILV_NONNULL set_value,
                            void* LILV_UNSPECIFIED            user_data);

/**
   Restore a plugin instance from a state snapshot.

   This will set all the properties of `instance`, if given, to the values
   stored in `state`.  If `set_value` is provided, it will be called (with the
   given `user_data`) to restore each port value, otherwise the host must
   restore the port values itself (using lilv_state_get_port_value()) in order
   to completely restore `state`.

   If the state has properties and `instance` is given, this function is in
   the "instantiation" threading class, so it MUST NOT be called
   simultaneously with any function on the same plugin instance.  If the state
   has no properties, only port values are set via `set_value`.

   See <a href="http://lv2plug.in/ns/ext/state/state.h">state.h</a> from the
   LV2 State extension for details on the `flags` and `features` parameters.

   @param state The state to restore, which must apply to the correct plugin.
   @param instance An instance of the plugin `state` applies to, or NULL.
   @param set_value A function to set a port value (may be NULL).
   @param user_data User data to pass to `set_value`.
   @param flags Bitwise OR of LV2_State_Flags values.
   @param features Features to pass LV2_State_Interface.restore().
*/
LILV_API void
lilv_state_restore(
  const LilvState* LILV_NONNULL                            state,
  LilvInstance* LILV_NULLABLE                              instance,
  LilvSetPortValueFunc LILV_UNSPECIFIED                    set_value,
  void* LILV_UNSPECIFIED                                   user_data,
  uint32_t                                                 flags,
  const LV2_Feature* LILV_NULLABLE const* LILV_UNSPECIFIED features);

/**
   Save state to a file.

   The format of state on disk is compatible with that defined in the LV2
   preset extension, so this function may be used to save presets which can
   be loaded by any host.

   If `uri` is NULL, the preset URI will be a file URI, but the bundle
   can safely be moved (the state file will use `<>` as the subject).

   @param world The world.
   @param map URID mapper.
   @param unmap URID unmapper.
   @param state State to save.
   @param uri URI of state, may be NULL.
   @param dir Path of the bundle directory to save into.
   @param filename Path of the state file relative to `dir`.
*/
LILV_API int
lilv_state_save(LilvWorld* LILV_NONNULL       world,
                LV2_URID_Map* LILV_NONNULL    map,
                LV2_URID_Unmap* LILV_NONNULL  unmap,
                const LilvState* LILV_NONNULL state,
                const char* LILV_NULLABLE     uri,
                const char* LILV_NONNULL      dir,
                const char* LILV_NONNULL      filename);

/**
   Save state to a string.

   This function does not use the filesystem.

   @param world The world.

   @param map URID mapper.

   @param unmap URID unmapper.

   @param state The state to serialize.

   @param uri URI for the state description (mandatory).

   @param base_uri Base URI for serialisation.  Unless you know what you are
   doing, pass NULL for this, otherwise the state may not be restorable via
   lilv_state_new_from_string().
*/
LILV_API char* LILV_ALLOCATED
lilv_state_to_string(LilvWorld* LILV_NONNULL       world,
                     LV2_URID_Map* LILV_NONNULL    map,
                     LV2_URID_Unmap* LILV_NONNULL  unmap,
                     const LilvState* LILV_NONNULL state,
                     const char* LILV_UNSPECIFIED  uri,
                     const char* LILV_NULLABLE     base_uri);

/**
   Unload a state from the world and delete all associated files.

   This function DELETES FILES/DIRECTORIES FROM THE FILESYSTEM!  It is intended
   for removing user-saved presets, but can delete any state the user has
   permission to delete, including presets shipped with plugins.

   The rdfs:seeAlso file for the state will be removed.  The entry in the
   bundle's manifest.ttl is removed, and if this results in an empty manifest,
   then the manifest file is removed.  If this results in an empty bundle, then
   the bundle directory is removed as well.

   @param world The world.
   @param state State to remove from the system.
*/
LILV_API int
lilv_state_delete(LilvWorld* LILV_NONNULL       world,
                  const LilvState* LILV_NONNULL state);

/**
   @}
   @defgroup lilv_scalepoint Scale Points
   @{
*/

/**
   Get the label of this scale point (enumeration value).

   Returned value is owned by `point` and must not be freed.
*/
LILV_API const LilvNode* LILV_NONNULL
lilv_scale_point_get_label(const LilvScalePoint* LILV_NONNULL point);

/**
   Get the value of this scale point (enumeration value).

   Returned value is owned by `point` and must not be freed.
*/
LILV_API const LilvNode* LILV_NONNULL
lilv_scale_point_get_value(const LilvScalePoint* LILV_NONNULL point);

/**
   @}
   @defgroup lilv_class Plugin Classes
   @{
*/

/**
   Get the URI of this class' superclass.

   Returned value is owned by `plugin_class` and must not be freed by caller.
   Returned value may be NULL, if class has no parent.
*/
LILV_API const LilvNode* LILV_NULLABLE
lilv_plugin_class_get_parent_uri(
  const LilvPluginClass* LILV_NONNULL plugin_class);

/**
   Get the URI of this plugin class.

   Returned value is owned by `plugin_class` and must not be freed by caller.
*/
LILV_API const LilvNode* LILV_NONNULL
lilv_plugin_class_get_uri(const LilvPluginClass* LILV_NONNULL plugin_class);

/**
   Get the label of this plugin class, like "Oscillators".

   Returned value is owned by `plugin_class` and must not be freed by caller.
*/
LILV_API const LilvNode* LILV_NONNULL
lilv_plugin_class_get_label(const LilvPluginClass* LILV_NONNULL plugin_class);

/**
   Get the subclasses of this plugin class.

   Returned value must be freed by caller with lilv_plugin_classes_free().
*/
LILV_API LilvPluginClasses* LILV_ALLOCATED
lilv_plugin_class_get_children(
  const LilvPluginClass* LILV_NONNULL plugin_class);

/**
   @}
   @defgroup lilv_instance Plugin Instances
   @{
*/

/**
   @cond LILV_DOCUMENT_INSTANCE_IMPL
*/

/* Instance of a plugin.

   This is exposed in the ABI to allow inlining of performance critical
   functions like lilv_instance_run() (simple wrappers of functions in lv2.h).
   This is for performance reasons, user code should not use this definition
   in any way (which is why it is not machine documented).
   Truly private implementation details are hidden via `pimpl`.
*/
struct LilvInstanceImpl {
  const LV2_Descriptor* LILV_UNSPECIFIED lv2_descriptor;
  LV2_Handle LILV_UNSPECIFIED            lv2_handle;
  void* LILV_UNSPECIFIED                 pimpl;
};

/**
   @endcond
*/

/**
   Instantiate a plugin.

   The returned value is a lightweight handle for an LV2 plugin instance,
   it does not refer to `plugin`, or any other Lilv state.  The caller must
   eventually free it with lilv_instance_free().
   `features` is a NULL-terminated array of features the host supports.
   NULL may be passed if the host supports no additional features.

   This function is in the "discovery" threading class: it isn't real-time
   safe, and may not be called concurrently with itself for the same plugin.

   @return NULL if instantiation failed.
*/
LILV_API LilvInstance* LILV_ALLOCATED
lilv_plugin_instantiate(
  const LilvPlugin* LILV_NONNULL                        plugin,
  double                                                sample_rate,
  const LV2_Feature* LILV_NULLABLE const* LILV_NULLABLE features);

/**
   Free a plugin instance.

   It is safe to call this function on NULL.  The `instance` is invalid after
   this call.

   This function is in the "discovery" threading class: it isn't real-time
   safe, and may not be called concurrently with any other function for the
   same instance, or with lilv_plugin_instantiate() for the same plugin.
*/
LILV_API void
lilv_instance_free(LilvInstance* LILV_NULLABLE instance);

#ifndef LILV_INTERNAL

/**
   Get the URI of the plugin which `instance` is an instance of.

   This function is a simple accessor and may be called at any time.  The
   returned string is shared and must not be modified or deleted.
*/
static inline const char* LILV_UNSPECIFIED
lilv_instance_get_uri(const LilvInstance* LILV_NONNULL instance)
{
  return instance->lv2_descriptor->URI;
}

/**
   Connect a port to a data location.

   This may be called regardless of whether the plugin is activated,
   activation and deactivation does not destroy port connections.

   This function is in the "audio" threading class: it's real-time safe if the
   plugin is <http://lv2plug.in/ns/lv2core#hardRTCapable>, but may not be
   called concurrently with any other function for the same instance.
*/
static inline void
lilv_instance_connect_port(LilvInstance* LILV_NONNULL instance,
                           uint32_t                   port_index,
                           void* LILV_NULLABLE        data_location)
{
  instance->lv2_descriptor->connect_port(
    instance->lv2_handle, port_index, data_location);
}

/**
   Activate a plugin instance.

   This resets all state information in the plugin, except for port data
   locations (as set by lilv_instance_connect_port()).  This MUST be called
   before calling lilv_instance_run().

   This function is in the "instantiation" threading class: it isn't real-time
   safe, and may not be called concurrently with any other function for the
   same instance.
*/
static inline void
lilv_instance_activate(LilvInstance* LILV_NONNULL instance)
{
  if (instance->lv2_descriptor->activate) {
    instance->lv2_descriptor->activate(instance->lv2_handle);
  }
}

/**
   Run `instance` for `sample_count` frames.

   If the hint lv2:hardRTCapable is set for this plugin, this function is
   guaranteed not to block.

   This function is in the "audio" threading class: it's real-time safe if the
   plugin is <http://lv2plug.in/ns/lv2core#hardRTCapable>, but may not be
   called concurrently with any other function for the same instance.
*/
static inline void
lilv_instance_run(LilvInstance* LILV_NONNULL instance, uint32_t sample_count)
{
  instance->lv2_descriptor->run(instance->lv2_handle, sample_count);
}

/**
   Deactivate a plugin instance.

   Note that to run the plugin after this you must activate it, which will
   reset all state information (except port connections).

   This function is in the "instantiation" threading class: it isn't real-time
   safe and may not be called concurrently with any other function for the same
   instance.
*/
static inline void
lilv_instance_deactivate(LilvInstance* LILV_NONNULL instance)
{
  if (instance->lv2_descriptor->deactivate) {
    instance->lv2_descriptor->deactivate(instance->lv2_handle);
  }
}

/**
   Get extension data from the plugin instance.

   The type and semantics of the data returned is specific to the particular
   extension, though in all cases it is shared and must not be deleted.

   This function is in the "discovery" threading class: it isn't real-time safe
   and may not be called concurrently with any other function for the same
   instance.
*/
static inline const void* LILV_UNSPECIFIED
lilv_instance_get_extension_data(const LilvInstance* LILV_NONNULL instance,
                                 const char* LILV_NONNULL         uri)
{
  if (instance->lv2_descriptor->extension_data) {
    return instance->lv2_descriptor->extension_data(uri);
  }

  return NULL;
}

/**
   Get the LV2_Descriptor of the plugin instance.

   Normally hosts should not need to access the LV2_Descriptor directly,
   use the lilv_instance_* functions.

   The returned descriptor is shared and must not be deleted.

   This function is a simple accessor and may be called at any time.
*/
static inline const LV2_Descriptor* LILV_NONNULL
lilv_instance_get_descriptor(const LilvInstance* LILV_NONNULL instance)
{
  return instance->lv2_descriptor;
}

/**
   Get the LV2_Handle of the plugin instance.

   Normally hosts should not need to access the LV2_Handle directly,
   use the lilv_instance_* functions.

   The returned handle is shared and must not be deleted.

   This function is a simple accessor and may be called at any time.
*/
static inline LV2_Handle LILV_UNSPECIFIED
lilv_instance_get_handle(const LilvInstance* LILV_NONNULL instance)
{
  return instance->lv2_handle;
}

#endif /* LILV_INTERNAL */

/**
   @}
   @defgroup lilv_ui Plugin UIs
   @{
*/

/**
   Get all UIs for `plugin`.

   Returned value must be freed by caller using lilv_uis_free().
*/
LILV_API LilvUIs* LILV_ALLOCATED
lilv_plugin_get_uis(const LilvPlugin* LILV_NONNULL plugin);

/**
   Get the URI of a Plugin UI.

   @return A shared value which must not be modified or freed.
*/
LILV_API const LilvNode* LILV_NONNULL
lilv_ui_get_uri(const LilvUI* LILV_NONNULL ui);

/**
   Get the types (URIs of RDF classes) of a Plugin UI.

   Note that in most cases lilv_ui_is_supported() should be used, which avoids
   the need to use this function (and type specific logic).

   @return A shared value which must not be modified or freed.
*/
LILV_API const LilvNodes* LILV_NONNULL
lilv_ui_get_classes(const LilvUI* LILV_NONNULL ui);

/**
   Check whether a plugin UI has a given type.

   @param ui        The Plugin UI
   @param class_uri The URI of the LV2 UI type to check this UI against
*/
LILV_API bool
lilv_ui_is_a(const LilvUI* LILV_NONNULL   ui,
             const LilvNode* LILV_NONNULL class_uri);

/**
   Function to determine whether a UI type is supported.

   This is provided by the user and must return non-zero iff using a UI of type
   `ui_type_uri` in a container of type `container_type_uri` is supported.
*/
typedef unsigned (*LilvUISupportedFunc)(
  const char* LILV_NONNULL container_type_uri,
  const char* LILV_NONNULL ui_type_uri);

/**
   Return true iff a Plugin UI is supported as a given widget type.

   @param ui The Plugin UI

   @param supported_func User provided supported predicate.

   @param container_type The widget type to host the UI within.

   @param ui_type (Output) If non-NULL, set to the native type of the UI
   which is owned by `ui` and must not be freed by the caller.

   @return The embedding quality level returned by `supported_func`.
*/
LILV_API unsigned
lilv_ui_is_supported(const LilvUI* LILV_NONNULL       ui,
                     LilvUISupportedFunc LILV_NONNULL supported_func,
                     const LilvNode* LILV_NONNULL     container_type,
                     const LilvNode* LILV_NULLABLE* LILV_NULLABLE ui_type);

/**
   Get the URI for a Plugin UI's bundle.

   @return A shared value which must not be modified or freed.
*/
LILV_API const LilvNode* LILV_NONNULL
lilv_ui_get_bundle_uri(const LilvUI* LILV_NONNULL ui);

/**
   Get the URI for a Plugin UI's shared library.

   @return A shared value which must not be modified or freed.
*/
LILV_API const LilvNode* LILV_NONNULL
lilv_ui_get_binary_uri(const LilvUI* LILV_NONNULL ui);

/**
   @}
   @}
*/

#ifdef __cplusplus
#  if defined(__clang__)
#    pragma clang diagnostic pop
#  endif
} /* extern "C" */
#endif

#endif /* LILV_LILV_H */
