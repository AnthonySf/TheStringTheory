// Copyright 2012-2022 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include <zix/status.h>
#include <zix/thread.h>

#include <assert.h>
#include <string.h>

typedef struct {
  int input;
  int output;
} SharedData;

static ZixThreadResult ZIX_THREAD_FUNC
thread_func(void* const arg)
{
  SharedData* const data = (SharedData*)arg;

  data->output = data->input * 7;

  return ZIX_THREAD_RESULT;
}

int
main(int argc, char** argv)
{
  (void)argv;

  ZixThread thread; // NOLINT

  SharedData data = {argc + (int)strlen(argv[0]), 0};

  ZixStatus st = zix_thread_create(&thread, 128, thread_func, &data);
  assert(!st);

  st = zix_thread_join(thread);
  assert(!st);

  assert(data.output == data.input * 7);

  return 0;
}
