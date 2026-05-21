..
   Copyright 2020-2022 David Robillard <d@drobilla.net>
   SPDX-License-Identifier: ISC

.. default-domain:: c
.. highlight:: c

#######
Plugins
#######

After bundles are loaded,
all discovered plugins can be accessed via :func:`lilv_world_get_all_plugins`:

.. code-block:: c

   LilvPlugins* plugins = lilv_world_get_all_plugins(world);

:struct:`LilvPlugins` is a collection of plugins that can be accessed by index or plugin URI.
The convenicne macro :macro:`LILV_FOREACH` is provided to make iterating over collections simple.
For example, to print the URI of every plugin:

.. code-block:: c

   LILV_FOREACH (plugins, i, list) {
     const LilvPlugin* p = lilv_plugins_get(list, i);
       printf("%s\n", lilv_node_as_uri(lilv_plugin_get_uri(p)));
     }
   }

More typically,
you want to load a specific plugin,
which can be done with :func:`lilv_plugins_get_by_uri`:

.. code-block:: c

   LilvNode* plugin_uri = lilv_new_uri(world, "http://example.org/Osc");

   const LilvPlugin* plugin = lilv_plugins_get_by_uri(list, plugin_uri);

:struct:`LilvPlugin` has various accessors that can be used to get information about the plugin.
See the :doc:`API reference <api/lilv_plugin>` for details.

*********
Instances
*********

:struct:`LilvPlugin` only represents the data of the plugin,
it does not load or access the actual plugin code.
To do that, you must instantiate a plugin to create :struct:`LilvInstance`:

.. code-block:: c

   LilvInstance* instance = lilv_plugin_instantiate(plugin, 48000.0, NULL);

Connecting Ports
================

Before running a plugin instance, its ports must be connected to some data.
This is done with :func:`lilv_instance_connect_port`.
Assuming the plugins has two control input ports and one audio output port,
in that order:

.. code-block:: c

   float control_in_1 = 0.0f;
   float control_in_2 = 0.0f;

   float audio_out[128];

   lilv_instance_connect_port(instance, 0, &control_in_1);
   lilv_instance_connect_port(instance, 1, &control_in_2);
   lilv_instance_connect_port(instance, 2, &audio_out);

Processing Data
===============

Once the ports are connected, the instance can be activated and run:

.. code-block:: c

   lilv_instance_activate(instance);

   lilv_instance_run(instance, 128);
   // Copy buffers around and probably run several times here...

   lilv_instance_deactivate(instance);

Once you are done with an instance,
it can be destroyed with :func:`lilv_instance_free`:

.. code-block:: c

   lilv_instance_free(instance);
