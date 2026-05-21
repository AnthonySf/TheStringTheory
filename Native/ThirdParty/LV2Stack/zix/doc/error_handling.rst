..
   Copyright 2022 David Robillard <d@drobilla.net>
   SPDX-License-Identifier: ISC

Error Handling
==============

.. default-domain:: c
.. highlight:: c

Most functions return a :enum:`ZixStatus` which describes whether they succeeded (with zero, or :enumerator:`ZIX_STATUS_SUCCESS`)
or why they failed.
The human-readable message for a status can be retrieved with :func:`zix_strerror`.
