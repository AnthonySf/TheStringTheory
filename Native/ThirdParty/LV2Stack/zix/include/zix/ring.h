// Copyright 2011-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_RING_H
#define ZIX_RING_H

#include <zix/allocator.h>
#include <zix/attributes.h>
#include <zix/status.h>

#include <stdint.h>

ZIX_BEGIN_DECLS

/**
   @defgroup zix_ring Ring
   @ingroup zix_data_structures
   @{
*/

/**
   @defgroup zix_ring_types Types
   @{
*/

/**
   A lock-free ring buffer.

   Thread-safe (with a few noted exceptions) for a single reader and single
   writer, and realtime-safe on both ends.
*/
typedef struct ZixRingImpl ZixRing;

/**
   @}
   @defgroup zix_ring_setup Setup
   @{
*/

/**
   Create a new ring.

   @param allocator Allocator for the ring object and its array.

   @param size Minimum size of the ring array in bytes.  This must be between 2
   and 2147483648 inclusive, and is rounded up to the next power of 2
   internally.  Note that one byte is reserved, so in order to be able to write
   `n` bytes to the ring at once, `size` must be `n + 1`.
*/
ZIX_API ZIX_NODISCARD ZixRing* ZIX_ALLOCATED
zix_ring_new(ZixAllocator* ZIX_NULLABLE allocator, uint32_t size);

/**
   Destroy a ring.

   This frees the ring structure and its buffer, discarding its contents.
*/
ZIX_API void
zix_ring_free(ZixRing* ZIX_NULLABLE ring);

/**
   Lock the ring data into physical memory.

   This function is NOT thread safe or real-time safe, but it should be called
   after zix_ring_new() to lock all ring memory to avoid page faults while
   using the ring.
*/
ZIX_API ZixStatus
zix_ring_mlock(ZixRing* ZIX_NONNULL ring);

/**
   Reset (empty) a ring.

   This function is NOT thread-safe, it may only be called when there is no
   reader or writer.
*/
ZIX_API ZIX_REALTIME void
zix_ring_reset(ZixRing* ZIX_NONNULL ring);

/**
   Return the capacity (the total write space when empty).

   This function returns a constant for any given ring, and may (but usually
   shouldn't) be called anywhere.
*/
ZIX_PURE_API ZIX_REALTIME uint32_t
zix_ring_capacity(const ZixRing* ZIX_NONNULL ring);

/**
   @}
   @defgroup zix_ring_read Reading
   Functions that may only be called by the read thread.
   @{
*/

/**
   Return the number of bytes available for reading.

   This function returns at most one less than the ring's buffer size.
*/
ZIX_PURE_API ZIX_REALTIME uint32_t
zix_ring_read_space(const ZixRing* ZIX_NONNULL ring);

/**
   Read from the ring without advancing the read head.

   @param ring The ring to read data from.
   @param dst The buffer to write data to.
   @param size The number of bytes to read from `ring` and write to `dst`.

   @return The number of bytes read, which is either `size` on success, or zero
   on failure.
*/
ZIX_API ZIX_NONBLOCKING uint32_t
zix_ring_peek(ZixRing* ZIX_NONNULL ring, void* ZIX_NONNULL dst, uint32_t size);

/**
   Read from the ring and advance the read head.

   @param ring The ring to read data from.
   @param dst The buffer to write data to.
   @param size The number of bytes to read from `ring` and write to `dst`.

   @return The number of bytes read, which is either `size` on success, or zero
   on failure.
*/
ZIX_API ZIX_NONBLOCKING uint32_t
zix_ring_read(ZixRing* ZIX_NONNULL ring, void* ZIX_NONNULL dst, uint32_t size);

/**
   Advance the read head, ignoring any data.

   @return Either `size` on success, or zero if there aren't enough bytes to
   skip.
*/
ZIX_API ZIX_REALTIME uint32_t
zix_ring_skip(ZixRing* ZIX_NONNULL ring, uint32_t size);

/**
   @}
   @defgroup zix_ring_write Writing
   Functions that may only be called by the write thread.
   @{
*/

/**
   A transaction for writing data in multiple parts.

   The simple zix_ring_write() can be used to write an atomic message that will
   immediately be visible to the reader, but transactions allow data to be
   written in several chunks before being "committed" and becoming readable.
   This can be useful for things like prefixing messages with a header without
   needing an allocated buffer to construct the "packet".

   The contents of this structure are an implementation detail and must not be
   manipulated by the user.
*/
typedef struct {
  uint32_t read_head;  ///< Read head at the start of the transaction
  uint32_t write_head; ///< Write head if the transaction were committed
} ZixRingTransaction;

/**
   Return the number of bytes available for writing.

   This function returns at most one less than the ring's buffer size.
*/
ZIX_PURE_API ZIX_REALTIME uint32_t
zix_ring_write_space(const ZixRing* ZIX_NONNULL ring);

/**
   Write data to the ring.

   This writes a contiguous input buffer of bytes to the ring.

   @param ring The ring to write data to.
   @param src The buffer to read data from.
   @param size The number of bytes to read from `src` and write to `ring`.

   @return The number of bytes written, which is either `size` on success, or
   zero on failure.
*/
ZIX_API ZIX_NONBLOCKING uint32_t
zix_ring_write(ZixRing* ZIX_NONNULL    ring,
               const void* ZIX_NONNULL src,
               uint32_t                size);

/**
   Begin a write.

   The returned transaction is initially empty.  Data can be written to it by
   calling zix_ring_amend_write() one or more times, then finishing with
   zix_ring_commit_write().

   Note that the returned "transaction" is not meant to be long-lived: a call
   to this function should be (more or less) immediately followed by calls to
   zix_ring_amend_write() then a call to zix_ring_commit_write().

   @param ring The ring to write data to.
   @return A new empty transaction.
*/
ZIX_API ZIX_REALTIME ZIX_NODISCARD ZixRingTransaction
zix_ring_begin_write(ZixRing* ZIX_NONNULL ring);

/**
   Amend the current write with some data.

   The data is written immediately after the previously amended data, as if
   they were written contiguously with a single write call.  This data is not
   visible to the reader until zix_ring_commit_write() is called.

   If any call to this function returns an error, then the transaction is
   invalid and must not be committed.  No cleanup is necessary for an invalid
   transaction.  Any bytes written while attempting the transaction will remain
   in the free portion of the buffer and be overwritten by subsequent writes.

   @param ring The ring this transaction is writing to.
   @param tx The active transaction, from zix_ring_begin_write().
   @param src Pointer to the data to write.
   @param size Length of data to write in bytes.
   @return #ZIX_STATUS_NO_MEM or #ZIX_STATUS_SUCCESS.
*/
ZIX_API ZIX_NONBLOCKING ZixStatus
zix_ring_amend_write(ZixRing* ZIX_NONNULL            ring,
                     ZixRingTransaction* ZIX_NONNULL tx,
                     const void* ZIX_NONNULL         src,
                     uint32_t                        size);

/**
   Commit the current write.

   This atomically updates the state of the ring, so that the reader will
   observe the data written during the transaction.

   This function usually shouldn't be called for any transaction which
   zix_ring_amend_write() returned an error for.

   @param ring The ring this transaction is writing to.
   @param tx The active transaction, from zix_ring_begin_write().
   @return #ZIX_STATUS_SUCCESS.
*/
ZIX_API ZIX_REALTIME ZixStatus
zix_ring_commit_write(ZixRing* ZIX_NONNULL                  ring,
                      const ZixRingTransaction* ZIX_NONNULL tx);

/**
   @}
   @}
*/

ZIX_END_DECLS

#endif /* ZIX_RING_H */
