Zix
===

Zix is a lightweight C library of portability wrappers and data structures.

Components
----------

* Allocation

  * `ZixAllocator`: A customizable allocator.
    * `ZixBumpAllocator`: A simple realtime-safe "bump-pointer" allocator.

* Algorithms

  * `zix/digest.h`: Digest functions suitable for hashing arbitrary data.

* Data Structures

  * `ZixBTree`: A page-allocated B-tree.
  * `ZixHash`: An open-addressing hash table.
  * `ZixRing`: A lock-free realtime-safe ring buffer.
  * `ZixTree`: A binary search tree.

* Threading

  * `ZixSem`: A portable semaphore wrapper.
  * `ZixThread`: A portable thread wrapper.

* File System

  * `zix/filesystem.h`: Functions for working with filesystems.
  * `zix/path.h`: Functions for working with filesystem paths lexically.

* Environment

  * `zix/environment.h`: Function to expand shell-style variables in a string.

Platforms
---------

Zix is continually tested on:

  * Debian 11 (x86, x64, arm32, and arm64)
  * Fedora 36 (x64)
  * FreeBSD 14 (x64)
  * MacOS 14 (M2)
  * Node 12 (as wasm via emscripten 2.0.12)
  * Windows 10 (x64)

Dependencies
------------

None,
except the C standard library,
and some POSIX and platform-specific APIs where necessary.

Documentation
-------------

  * [Installation Instructions](INSTALL.md)
  * [API reference (single page)](https://drobilla.gitlab.io/zix/doc/singlehtml/)
  * [API reference (paginated)](https://drobilla.gitlab.io/zix/doc/html/)
  * [Test Coverage](https://drobilla.gitlab.io/zix/coverage/)

 -- David Robillard <d@drobilla.net>
