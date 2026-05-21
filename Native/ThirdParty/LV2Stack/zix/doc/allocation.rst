..
   Copyright 2022 David Robillard <d@drobilla.net>
   SPDX-License-Identifier: ISC

Allocation
==========

.. default-domain:: c
.. highlight:: c

Functions or objects that allocate memory take a pointer to a :struct:`ZixAllocator`,
which can be used to customize the allocation scheme.
To simply use the system allocator,
callers can pass ``NULL``,
which is equivalent to :func:`zix_default_allocator()`.

Zix includes one implementation of a custom allocator,
a very simple bump-pointer allocator which can be created with :func:`zix_bump_allocator`.

A convenience API for using custom allocators
(or the system allocator as a fallback)
is provided with functions like :func:`zix_malloc` that reflect the standard C API.

Memory allocated with an allocator must be freed by calling :func:`zix_free` with the same allocator.
