// Copyright 2020 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include <serd/serd.h>

#include <stddef.h>

int
main(void)
{
  serd_free(NULL);
  serd_node_free(NULL);
  serd_env_free(NULL);
  serd_reader_free(NULL);
  serd_writer_free(NULL);

  return 0;
}
