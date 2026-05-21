// Copyright 2012-2022 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/sem.h>

#include <zix/status.h>

#include <windows.h>

#include <limits.h>
#include <stdint.h>
#include <time.h>

ZixStatus
zix_sem_init(ZixSem* sem, unsigned initial)
{
  sem->sem = CreateSemaphore(NULL, (LONG)initial, LONG_MAX, NULL);
  return sem->sem ? ZIX_STATUS_SUCCESS : ZIX_STATUS_ERROR;
}

ZixStatus
zix_sem_destroy(ZixSem* sem)
{
  return CloseHandle(sem->sem) ? ZIX_STATUS_SUCCESS : ZIX_STATUS_ERROR;
}

ZixStatus
zix_sem_post(ZixSem* sem)
{
  return ReleaseSemaphore(sem->sem, 1, NULL) ? ZIX_STATUS_SUCCESS
                                             : ZIX_STATUS_ERROR;
}

ZixStatus
zix_sem_wait(ZixSem* sem)
{
  return WaitForSingleObject(sem->sem, INFINITE) == WAIT_OBJECT_0
           ? ZIX_STATUS_SUCCESS
           : ZIX_STATUS_ERROR;
}

ZixStatus
zix_sem_try_wait(ZixSem* sem)
{
  const DWORD r = WaitForSingleObject(sem->sem, 0);

  return (r == WAIT_OBJECT_0)  ? ZIX_STATUS_SUCCESS
         : (r == WAIT_TIMEOUT) ? ZIX_STATUS_UNAVAILABLE
                               : ZIX_STATUS_ERROR;
}

ZixStatus
zix_sem_timed_wait(ZixSem*        sem,
                   const uint32_t seconds,
                   const uint32_t nanoseconds)
{
  const uint32_t milliseconds = seconds * 1000U + nanoseconds / 1000000U;
  const DWORD    r            = WaitForSingleObject(sem->sem, milliseconds);

  return (r == WAIT_OBJECT_0)  ? ZIX_STATUS_SUCCESS
         : (r == WAIT_TIMEOUT) ? ZIX_STATUS_TIMEOUT
                               : ZIX_STATUS_ERROR;
}
