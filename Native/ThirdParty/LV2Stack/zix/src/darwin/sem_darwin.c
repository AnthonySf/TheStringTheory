// Copyright 2012-2022 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/sem.h>

#include <zix/status.h>

#include <mach/mach.h>

#include <stdint.h>
#include <time.h>

ZixStatus
zix_sem_init(ZixSem* sem, unsigned val)
{
  return semaphore_create(
           mach_task_self(), &sem->sem, SYNC_POLICY_FIFO, (int)val)
           ? ZIX_STATUS_ERROR
           : ZIX_STATUS_SUCCESS;
}

ZixStatus
zix_sem_destroy(ZixSem* sem)
{
  return semaphore_destroy(mach_task_self(), sem->sem) ? ZIX_STATUS_ERROR
                                                       : ZIX_STATUS_SUCCESS;
}

ZixStatus
zix_sem_post(ZixSem* sem)
{
  return semaphore_signal(sem->sem) ? ZIX_STATUS_ERROR : ZIX_STATUS_SUCCESS;
}

ZixStatus
zix_sem_wait(ZixSem* sem)
{
  kern_return_t r = 0;
  while ((r = semaphore_wait(sem->sem)) && r == KERN_ABORTED) {
    // Interrupted, try again
  }

  return r ? ZIX_STATUS_ERROR : ZIX_STATUS_SUCCESS;
}

ZixStatus
zix_sem_try_wait(ZixSem* sem)
{
  const mach_timespec_t zero = {0, 0};
  const kern_return_t   r    = semaphore_timedwait(sem->sem, zero);

  return (r == KERN_SUCCESS)               ? ZIX_STATUS_SUCCESS
         : (r == KERN_OPERATION_TIMED_OUT) ? ZIX_STATUS_UNAVAILABLE
                                           : ZIX_STATUS_ERROR;
}

ZixStatus
zix_sem_timed_wait(ZixSem*        sem,
                   const uint32_t seconds,
                   const uint32_t nanoseconds)
{
  const mach_timespec_t interval = {seconds, (clock_res_t)nanoseconds};
  const kern_return_t   r        = semaphore_timedwait(sem->sem, interval);

  return (r == KERN_SUCCESS)               ? ZIX_STATUS_SUCCESS
         : (r == KERN_OPERATION_TIMED_OUT) ? ZIX_STATUS_TIMEOUT
                                           : ZIX_STATUS_ERROR;
}
