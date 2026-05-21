..
   Copyright 2020-2023 David Robillard <d@drobilla.net>
   SPDX-License-Identifier: ISC

String Views
============

.. default-domain:: c
.. highlight:: c

For performance reasons,
many functions in zix that take a string take a :struct:`ZixStringView`,
rather than a bare pointer.
This forces code to be explicit about string measurement,
which discourages common patterns of repeated measurement of the same string.
For convenience, several macros and functions are provided for constructing string views:

:macro:`ZIX_STATIC_STRING`

   Initializes a string view from a string literal.  Note that this may only be
   used with inlined string literals, passing a pointer to an arbitrary string
   is undefined behaviour, for example:

   .. literalinclude:: overview_code.c
      :start-after: begin make-static-string
      :end-before: end make-static-string
      :dedent: 2

:func:`zix_empty_string`

   Constructs a view of an empty string, for example:

   .. literalinclude:: overview_code.c
      :start-after: begin make-empty-string
      :end-before: end make-empty-string
      :dedent: 2

:func:`zix_string`

   Constructs a view of an arbitrary string, for example:

   .. literalinclude:: overview_code.c
      :start-after: begin measure-string
      :end-before: end measure-string
      :dedent: 2

   This calls ``strlen`` to measure the string.
   Modern compilers should optimize this away if the parameter is a literal.

:func:`zix_substring`

   Constructs a view of a slice of a string with an explicit length,
   for example:

   .. literalinclude:: overview_code.c
      :start-after: begin make-string-view
      :end-before: end make-string-view
      :dedent: 2

   This can also be used to create a view of a pre-measured string.
   If the length a dynamic string is already known,
   this is faster than :func:`zix_string`,
   since it avoids redundant measurement.

These constructors can be used inline when passing parameters,
but if the same dynamic string is used several times,
it is better to make a string view variable to avoid redundant measurement.
