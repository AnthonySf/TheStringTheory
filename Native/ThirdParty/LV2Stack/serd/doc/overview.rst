..
   Copyright 2020-2021 David Robillard <d@drobilla.net>
   SPDX-License-Identifier: ISC

########
Overview
########

.. default-domain:: c
.. highlight:: c

The API revolves around two main types: the :doc:`api/serd_reader`,
which reads text and fires callbacks,
and the :doc:`api/serd_writer`,
which writes text when driven by corresponding functions.
Both work in a streaming fashion but still support pretty-printing,
so the pair can be used to pretty-print, translate,
or otherwise process arbitrarily large documents very quickly.
The context of a stream is tracked by the :doc:`api/serd_env`,
which stores the current base URI and set of namespace prefixes.

The complete API is declared in ``serd.h``:

.. code-block:: c

   #include <serd/serd.h>
