// Copyright 2022 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/allocator.h>      // IWYU pragma: keep
#include <zix/attributes.h>     // IWYU pragma: keep
#include <zix/btree.h>          // IWYU pragma: keep
#include <zix/bump_allocator.h> // IWYU pragma: keep
#include <zix/digest.h>         // IWYU pragma: keep
#include <zix/environment.h>    // IWYU pragma: keep
#include <zix/filesystem.h>     // IWYU pragma: keep
#include <zix/hash.h>           // IWYU pragma: keep
#include <zix/path.h>           // IWYU pragma: keep
#include <zix/ring.h>           // IWYU pragma: keep
#include <zix/sem.h>            // IWYU pragma: keep
#include <zix/status.h>         // IWYU pragma: keep
#include <zix/string_view.h>    // IWYU pragma: keep
#include <zix/thread.h>         // IWYU pragma: keep
#include <zix/tree.h>           // IWYU pragma: keep
#include <zix/zix.h>            // IWYU pragma: keep

#ifdef __GNUC__
__attribute__((const))
#endif
int
main(void)
{
  return 0;
}
