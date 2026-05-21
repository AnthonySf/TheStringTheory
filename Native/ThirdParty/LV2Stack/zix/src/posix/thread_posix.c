// Copyright 2012-2020 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/thread.h>

#include "../errno_status.h"

#include <zix/status.h>

#include <pthread.h>

#include <stddef.h>

ZixStatus
zix_thread_create(ZixThread*    thread,
                  size_t        stack_size,
                  ZixThreadFunc function,
                  void*         arg)
{
  pthread_attr_t attr;
  pthread_attr_init(&attr);
  pthread_attr_setstacksize(&attr, stack_size);

  const int ret = pthread_create(thread, NULL, function, arg);

  pthread_attr_destroy(&attr);
  return zix_errno_status(ret);
}

ZixStatus
zix_thread_join(ZixThread thread)
{
  return pthread_join(thread, NULL) ? ZIX_STATUS_ERROR : ZIX_STATUS_SUCCESS;
}
