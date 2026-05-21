..
   Copyright 2020-2022 David Robillard <d@drobilla.net>
   SPDX-License-Identifier: ISC

.. default-domain:: c
.. highlight:: c

#####
World
#####

The world is the top-level object which represents an instance of Lilv.
It is used to discover and load LV2 data,
and stores an internal cache of loaded data for fast searching.

An application typically has a single world,
which is constructed once on startup and used throughout the application's lifetime.

************
Construction
************

A world must be created before anything else.
A world is created with :func:`lilv_world_new`, for example:

.. code-block:: c

   LilvWorld* world = lilv_world_new();

***************
Setting Options
***************

Various options to control Lilv's behavior can be set with :func:`lilv_world_set_option`.
The currently supported options are :c:macro:`LILV_OPTION_FILTER_LANG`,
:c:macro:`LILV_OPTION_DYN_MANIFEST`,
:c:macro:`LILV_OPTION_LV2_PATH`,
and :c:macro:`LILV_OPTION_OBJECT_INDEX`.

For example, to set the LV2 path to only load plugins bundled in the application:

.. code-block:: c

   LilvNode* lv2_path = lilv_new_string(world, "/myapp/lv2");

   lilv_world_set_option(world, LILV_OPTION_LV2_PATH, lv2_path);

************
Loading Data
************

Before using anything, data must be loaded from disk.
All LV2 data (plugins, UIs, specifications, presets, and so on) is installed in `bundles`,
a standard directory format

Discovering and Loading Bundles
===============================

Typical hosts will simply load all bundles in the standard LV2 locations on the system:

.. code-block:: c

   lilv_world_load_all(world);

This will discover all bundles on the system,
as well as load the required data defined in any discovered specifications.

It is also possible to load a specific bundle:

.. code-block:: c

   LilvNode* bundle_uri = lilv_new_file_uri(world, NULL, "/some/plugin.lv2");

   lilv_world_load_bundle(world, bundle_uri);

The LV2 specification itself is also installed in bundles,
so if you are not using :func:`lilv_world_load_all`,
it is necessary to manually load the discovered specification data:

.. code-block:: c

   lilv_world_load_specifications(world);
   lilv_world_load_plugin_classes(world);

*************
Querying Data
*************

The world contains a model of all the loaded data in memory which can be queried.

Data Model
==========

LV2 data is a set of "statements",
where a statement is a bit like a simple machine-readable sentence.
The "subject" and "object" are as in natural language,
and the "predicate" is like the verb, but more general.

For example, we could make a statement about a plugin in english:

   MyOsc has the name "Super Oscillator"

We can break this statement into 3 pieces like so:

.. list-table::
   :header-rows: 1

   * - Subject
     - Predicate
     - Object
   * - MyOsc
     - has the name
     - "My Super Oscillator"

Statements use URIs to identify things.
In this case, we assume that this plugin has the URI ``http://example.org/Osc``.
The LV2 specification defines that ``http://usefulinc.com/ns/doap#name`` is the property used to describe a plugin's name.
So, this statement is:

.. list-table::
   :header-rows: 1

   * - Subject
     - Predicate
     - Object
   * - ``http://example.org/Osc``
     - ``http://usefulinc.com/ns/doap#name``
     - "My Oscillator"

Finding Values
==============

Based on this model, you can find all values that match a certain pattern.
Patterns are just statements,
but with ``NULL`` used as a wildcard that matches anything.
So, for example, you can get the name of a plugin using :func:`lilv_world_find_nodes`:

.. code-block:: c

   LilvNode* plugin_uri = lilv_new_uri(world, "http://example.org/Osc");
   LilvNode* doap_name  = lilv_new_uri(world, "http://usefulinc.com/ns/doap#name");

   LilvNodes* values = lilv_world_find_nodes(world, plugin_uri, doap_name, NULL);

Note that a set of values is returned,
because some properties may have several values.
When you are only interested in one value,
you can use the simpler :func:`lilv_world_get` instead:

.. code-block:: c

   LilvNode* value = lilv_world_get(world, plugin_uri, doap_name, NULL);

If you are only interested if a value exists at all,
use :func:`lilv_world_ask`:

.. code-block:: c

   bool has_name = lilv_world_ask(world, plugin_uri, doap_name, NULL);
