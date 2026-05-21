// Copyright 2012-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_SEM_H
#define ZIX_SEM_H

#include <zix/attributes.h>
#include <zix/status.h>

#ifdef __APPLE__
#  include <mach/mach.h>
#elif defined(_WIN32)
#  include <windows.h>
#else
#  include <semaphore.h>
#endif

#include <stdint.h>

ZIX_BEGIN_DECLS

/**
   @defgroup zix_sem Semaphore
   @ingroup zix_threading
   @{
*/

/**
   A counting semaphore.

   This is an integer that is never negative, and has two main operations:
   increment (post) and decrement (wait).  If a decrement can't be performed
   (because the value is 0) the caller will be blocked until another thread
   posts and the operation can succeed.

   Semaphores can be created with any starting value, but typically this will
   be 0 so the semaphore can be used as a simple signal where each post
   corresponds to one wait.

   Semaphores are very efficient (compared to a mutex/cond pair).  In
   particular, at least on Linux, post is async-signal-safe, which means it
   does not block and will not be interrupted.  If you need to signal from
   a realtime thread, this is the most appropriate primitive to use.
*/
typedef struct ZixSemImpl ZixSem;

/**
   Create `sem` with the given `initial` value.

   @return #ZIX_STATUS_SUCCESS, or an unlikely error.
*/
ZIX_API ZixStatus
zix_sem_init(ZixSem* ZIX_NONNULL sem, unsigned initial);

/**
   Destroy `sem`.

   @return #ZIX_STATUS_SUCCESS, or an error.
*/
ZIX_API ZixStatus
zix_sem_destroy(ZixSem* ZIX_NONNULL sem);

/**
   Increment and signal any waiters.

   Realtime safe.

   @return #ZIX_STATUS_SUCCESS if `sem` was incremented, #ZIX_STATUS_OVERFLOW
   if the maximum possible value would have been exceeded, or
   #ZIX_STATUS_BAD_ARG if `sem` is invalid.
*/
ZIX_API ZIX_REALTIME ZixStatus
zix_sem_post(ZixSem* ZIX_NONNULL sem);

/**
   Wait until count is > 0, then decrement.

   Obviously not realtime safe.

   @return #ZIX_STATUS_SUCCESS if `sem` was decremented, or #ZIX_STATUS_BAD_ARG
   if `sem` is invalid.
*/
ZIX_API ZixStatus
zix_sem_wait(ZixSem* ZIX_NONNULL sem);

/**
   Non-blocking version of wait().

   @return #ZIX_STATUS_SUCCESS if `sem` was decremented,
   #ZIX_STATUS_UNAVAILABLE if it was already zero, or #ZIX_STATUS_BAD_ARG if
   `sem` is invalid.
*/
ZIX_API ZixStatus
zix_sem_try_wait(ZixSem* ZIX_NONNULL sem);

/**
   Wait for an amount of time until count is > 0, then decrement if possible.

   Obviously not realtime safe.

   @return #ZIX_STATUS_SUCCESS if `sem` was decremented, #ZIX_STATUS_TIMEOUT if
   it was still zero when the timeout was reached, #ZIX_STATUS_NOT_SUPPORTED if
   the system does not support timed waits, or #ZIX_STATUS_BAD_ARG if `sem` is
   invalid.
*/
ZIX_API ZixStatus
zix_sem_timed_wait(ZixSem* ZIX_NONNULL sem,
                   uint32_t            seconds,
                   uint32_t            nanoseconds);

/**
   @cond
*/

#ifdef __APPLE__

struct ZixSemImpl {
  semaphore_t sem;
};

#elif defined(_WIN32)

struct ZixSemImpl {
  HANDLE ZIX_NONNULL sem;
};

#else /* !defined(__APPLE__) && !defined(_WIN32) */

struct ZixSemImpl {
  sem_t sem;
};

#endif

/**
   @endcond
   @}
*/

ZIX_END_DECLS

#endif /* ZIX_SEM_H */
