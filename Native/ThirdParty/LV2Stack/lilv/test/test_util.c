// Copyright 2007-2022 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include "lilv_test_utils.h"

#include <lilv/lilv.h>
#include <zix/filesystem.h>
#include <zix/path.h>

#include <assert.h>
#include <stdio.h>

int
main(void)
{
  char* const dir = lilv_create_temporary_directory("lilv_test_util_XXXXXX");

  char* const a_path = zix_path_join(NULL, dir, "copy_a_XXXXXX");
  char* const b_path = zix_path_join(NULL, dir, "copy_b_XXXXXX");

  FILE* const fa = fopen(a_path, "w");
  FILE* const fb = fopen(b_path, "w");
  assert(fa);
  assert(fb);
  fprintf(fa, "AA\n");
  fprintf(fb, "AB\n");
  fclose(fb);
  fclose(fa);

  assert(!zix_remove(a_path));
  assert(!zix_remove(b_path));
  assert(!zix_remove(dir));

  lilv_free(b_path);
  lilv_free(a_path);
  lilv_free(dir);
  return 0;
}
