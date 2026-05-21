// Copyright 2012-2021 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#undef NDEBUG

#include <zix/attributes.h>
#include <zix/sem.h>
#include <zix/status.h>
#include <zix/thread.h>

#include <assert.h>
#include <stdio.h>
#include <stdlib.h>

static ZixSem   sem;
static unsigned n_signals = 1024;

static ZixThreadResult ZIX_THREAD_FUNC
reader(void* ZIX_UNUSED(arg))
{
  printf("Reader starting\n");

  for (unsigned i = 0; i < n_signals; ++i) {
    assert(!zix_sem_wait(&sem));
  }

  printf("Reader finished\n");
  return ZIX_THREAD_RESULT;
}

static ZixThreadResult ZIX_THREAD_FUNC
writer(void* ZIX_UNUSED(arg))
{
  printf("Writer starting\n");

  for (unsigned i = 0; i < n_signals; ++i) {
    assert(!zix_sem_post(&sem));
  }

  printf("Writer finished\n");
  return ZIX_THREAD_RESULT;
}

static void
test_try_wait(void)
{
  assert(!zix_sem_init(&sem, 0));
  assert(zix_sem_try_wait(&sem) == ZIX_STATUS_UNAVAILABLE);
  assert(!zix_sem_post(&sem));
  assert(!zix_sem_try_wait(&sem));
  assert(zix_sem_try_wait(&sem) == ZIX_STATUS_UNAVAILABLE);
  assert(!zix_sem_destroy(&sem));
}

static void
test_timed_wait(void)
{
  assert(!zix_sem_init(&sem, 0));
  assert(zix_sem_timed_wait(&sem, 0, 0) == ZIX_STATUS_TIMEOUT);
  assert(zix_sem_timed_wait(&sem, 0, 999999999) == ZIX_STATUS_TIMEOUT);
  assert(!zix_sem_post(&sem));
  assert(!zix_sem_timed_wait(&sem, 5, 0));
  assert(!zix_sem_post(&sem));
  assert(!zix_sem_timed_wait(&sem, 1000, 0));
  assert(!zix_sem_destroy(&sem));
}

int
main(int argc, char** argv)
{
  if (argc > 2) {
    printf("Usage: %s N_SIGNALS\n", argv[0]);
    return 1;
  }

  if (argc > 1) {
    n_signals = (unsigned)strtol(argv[1], NULL, 10);
  }

  test_try_wait();
  test_timed_wait();

  printf("Testing %u signals...\n", n_signals);

  assert(!zix_sem_init(&sem, 0));

  ZixThread reader_thread; // NOLINT
  assert(!zix_thread_create(&reader_thread, 128, reader, NULL));

  ZixThread writer_thread; // NOLINT
  assert(!zix_thread_create(&writer_thread, 128, writer, NULL));

  assert(!zix_thread_join(reader_thread));
  assert(!zix_thread_join(writer_thread));
  assert(!zix_sem_destroy(&sem));
  return 0;
}
