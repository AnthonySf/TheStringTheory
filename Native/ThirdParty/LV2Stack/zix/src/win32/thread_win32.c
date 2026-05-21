// Copyright 2012-2022 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/thread.h>

#include <zix/status.h>

#include <windows.h>

#include <stddef.h>

ZixStatus
zix_thread_create(ZixThread*    thread,
                  size_t        stack_size,
                  ZixThreadFunc function,
                  void*         arg)
{
  *thread = CreateThread(NULL, stack_size, function, arg, 0, NULL);
  return *thread ? ZIX_STATUS_SUCCESS : ZIX_STATUS_ERROR;
}

ZixStatus
zix_thread_join(ZixThread thread)
{
  return (WaitForSingleObject(thread, INFINITE) == WAIT_OBJECT_0)
           ? ZIX_STATUS_SUCCESS
           : ZIX_STATUS_ERROR;
}
