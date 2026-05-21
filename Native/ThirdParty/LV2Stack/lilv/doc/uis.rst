..
   Copyright 2020-2022 David Robillard <d@drobilla.net>
   SPDX-License-Identifier: ISC

.. default-domain:: c
.. highlight:: c

###############
User Interfaces
###############

Plugins may have custom user interfaces, or `UIs`,
which are installed in bundles just like plugins.

The available UIs for a plugin can be accessed with :func:`lilv_plugin_get_uis`:

.. code-block:: c

   LilvUIs* uis = lilv_plugin_get_uis(plugin);

:struct:`LilvUIs` is a collection much like `LilvPlugins`,
except it is of course a set of :struct:`LilvUI` rather than a set of :struct:`LilvPlugin`.
Also like plugins,
the :struct:`LilvUI` class has various accessors that can be used to get information about the UI.
See the :doc:`API reference <api/lilv_ui>` for details.
