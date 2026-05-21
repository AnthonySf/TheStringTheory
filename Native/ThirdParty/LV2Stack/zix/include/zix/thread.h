// Copyright 2012-2022 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_THREAD_H
#define ZIX_THREAD_H

#include <zix/attributes.h>
#include <zix/status.h>

#ifdef _WIN32
#  include <windows.h>
#else
#  include <pthread.h>
#endif

#include <stddef.h>

ZIX_BEGIN_DECLS

/**
   @defgroup zix_thread Thread
   @ingroup zix_threading
   @{
*/

#ifdef _WIN32

#  define ZIX_THREAD_RESULT 0
#  define ZIX_THREAD_FUNC __stdcall

typedef HANDLE ZixThread;
typedef DWORD  ZixThreadResult;

#else

#  define ZIX_THREAD_RESULT NULL ///< Result returned from a thread function
#  define ZIX_THREAD_FUNC        ///< Thread function attribute

typedef pthread_t ZixThread;       ///< A thread
typedef void*     ZixThreadResult; ///< Thread function return type

#endif

/**
   A thread function.

   For portability reasons, the return type varies between platforms, and the
   return value is ignored.  Thread functions should always return
   #ZIX_THREAD_RESULT.  This allows thread functions to be used directly by the
   system without any wrapper overhead.

   "Returning" a result, and communicating with the parent thread in general,
   can be done through the pointer argument.
*/
typedef ZixThreadResult(ZIX_THREAD_FUNC* ZixThreadFunc)(void*);

/**
   Initialize `thread` to a new thread.

   The thread will immediately be launched, calling `function` with `arg`
   as the only parameter.

   @return #ZIX_STATUS_SUCCESS on success, or #ZIX_STATUS_ERROR.
*/
ZIX_API ZixStatus
zix_thread_create(ZixThread*    thread,
                  size_t        stack_size,
                  ZixThreadFunc function,
                  void*         arg);

/**
   Join `thread` (block until `thread` exits).

   @return #ZIX_STATUS_SUCCESS on success, or #ZIX_STATUS_ERROR.
*/
ZIX_API ZixStatus
zix_thread_join(ZixThread thread);

/**
   @}
*/

ZIX_END_DECLS

#endif /* ZIX_THREAD_H */
