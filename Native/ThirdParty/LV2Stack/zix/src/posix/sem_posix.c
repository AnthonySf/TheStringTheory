// Copyright 2012-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <zix/sem.h>

#include "../errno_status.h"
#include "../zix_config.h"

#include <zix/attributes.h>
#include <zix/status.h>
#include <zix/warnings.h>

#include <semaphore.h>

#include <errno.h>
#include <stdint.h>
#include <time.h>

ZixStatus
zix_sem_init(ZixSem* sem, unsigned initial)
{
  return zix_errno_status_if(sem_init(&sem->sem, 0, initial));
}

ZixStatus
zix_sem_destroy(ZixSem* sem)
{
  return zix_errno_status_if(sem_destroy(&sem->sem));
}

ZIX_REALTIME ZixStatus
zix_sem_post(ZixSem* sem)
{
  ZIX_DISABLE_EFFECT_WARNINGS
  return zix_errno_status_if(sem_post(&sem->sem));
  ZIX_RESTORE_WARNINGS
}

ZixStatus
zix_sem_wait(ZixSem* sem)
{
  int r = 0;
  while ((r = sem_wait(&sem->sem)) && errno == EINTR) {
    // Interrupted, try again
  }

  return zix_errno_status_if(r);
}

ZixStatus
zix_sem_try_wait(ZixSem* sem)
{
  int r = 0;
  while ((r = sem_trywait(&sem->sem)) && errno == EINTR) {
    // Interrupted, try again
  }

  return zix_errno_status_if(r);
}

ZixStatus
zix_sem_timed_wait(ZixSem*        sem,
                   const uint32_t seconds,
                   const uint32_t nanoseconds)
{
#if !USE_CLOCK_GETTIME || !USE_SEM_TIMEDWAIT

  (void)sem;
  (void)seconds;
  (void)nanoseconds;
  return ZIX_STATUS_NOT_SUPPORTED;

#else
#  define NS_PER_SECOND 1000000000L

  struct timespec ts = {0, 0};

  int r = 0;
  if (!(r = clock_gettime(CLOCK_REALTIME, &ts))) {
    ts.tv_sec += (time_t)seconds;
    ts.tv_nsec += (long)nanoseconds;
    if (ts.tv_nsec >= NS_PER_SECOND) {
      ts.tv_nsec -= NS_PER_SECOND;
      ts.tv_sec++;
    }

    while ((r = sem_timedwait(&sem->sem, &ts)) && errno == EINTR) {
      // Interrupted, try again
    }
  }

  return zix_errno_status_if(r);

#  undef NS_PER_SECOND
#endif
}
