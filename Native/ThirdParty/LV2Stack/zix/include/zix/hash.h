// Copyright 2011-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef ZIX_HASH_H
#define ZIX_HASH_H

#include <zix/allocator.h>
#include <zix/attributes.h>
#include <zix/status.h>

#include <stdbool.h>
#include <stddef.h>

ZIX_BEGIN_DECLS

/**
   @defgroup zix_hash Hash
   @ingroup zix_data_structures
   @{
*/

/**
   @defgroup zix_hash_types Types
   @{
*/

// ZIX_HASH_KEY_TYPE can be defined to make the API more type-safe
#ifdef ZIX_HASH_KEY_TYPE
typedef ZIX_HASH_KEY_TYPE ZixHashKey;
#else
typedef void ZixHashKey; ///< The type of a key within a record
#endif

// ZIX_HASH_RECORD_TYPE can be defined to make the API more type-safe
#ifdef ZIX_HASH_RECORD_TYPE
typedef ZIX_HASH_RECORD_TYPE ZixHashRecord;
#else
typedef void ZixHashRecord; ///< The type of a hash table record
#endif

// ZIX_HASH_SEARCH_DATA_TYPE can be defined to make the API more type-safe
#ifdef ZIX_HASH_SEARCH_DATA_TYPE
typedef ZIX_HASH_SEARCH_DATA_TYPE ZixHashSearchData;
#else
typedef void ZixHashSearchData; ///< User data for key comparison function
#endif

/**
   A hash table.

   This is an open addressing hash table that stores pointers to arbitrary user
   data.  Internally, everything is stored in a single flat array that is
   resized when necessary.

   The single user-provided pointer that is stored in the table is called a
   "record".  A record contains a "key", which is accessed via a user-provided
   function.  This design supports storing large records (which may be
   expensive to allocate/construct) without requiring an entire record to
   search.  Simple atomic values can be stored by providing a trivial identity
   function as a key function.

   The table uses power of 2 sizes with a growth factor of 2, so that hash
   values can be folded into an array index using bitwise AND as a fast modulo.
   This means that only the necessary low bits of the hash value will be used,
   so the hash function must be well-balanced within this range.  More or less
   any good modern hash algorithm will be fine, but beware, for example, hash
   functions that assume they are targeting a table with a prime size.

   Since this doubles and halves in size, it may not be an optimal choice if
   memory reuse is a priority.  A growth factor of 1.5 with fast range
   reduction may be a better choice there, at the cost of requiring 128-bit
   arithmetic on 64-bit platforms, and indexing operations being slightly more
   expensive.
*/
typedef struct ZixHashImpl ZixHash;

/// A full hash code for a key which is not folded down to the table size
typedef size_t ZixHashCode;

/// User function for getting the key of a record
typedef const ZixHashKey* ZIX_NONNULL (*ZixKeyFunc)(
  const ZixHashRecord* ZIX_NONNULL record);

/// User function for computing the hash of a key
typedef ZixHashCode (*ZixHashFunc)(const ZixHashKey* ZIX_NONNULL key);

/// User function for determining if two keys are truly equal
typedef bool (*ZixKeyEqualFunc)(const ZixHashKey* ZIX_NONNULL a,
                                const ZixHashKey* ZIX_NONNULL b);

/**
   @}
   @defgroup zix_hash_setup Setup
   @{
*/

/**
   Create a new hash table.

   @param allocator Allocator used for the internal array.
   @param key_func A function to retrieve the key from a record.
   @param hash_func The key hashing function.
   @param equal_func A function to test keys for equality.
*/
ZIX_API ZIX_NODISCARD ZixHash* ZIX_ALLOCATED
zix_hash_new(ZixAllocator* ZIX_NULLABLE  allocator,
             ZixKeyFunc ZIX_NONNULL      key_func,
             ZixHashFunc ZIX_NONNULL     hash_func,
             ZixKeyEqualFunc ZIX_NONNULL equal_func);

/// Free `hash`
ZIX_API void
zix_hash_free(ZixHash* ZIX_NULLABLE hash);

/// Return the number of elements in the hash
ZIX_PURE_API ZIX_REALTIME size_t
zix_hash_size(const ZixHash* ZIX_NONNULL hash);

/**
   @}
   @defgroup zix_hash_iteration Iteration
   @{
*/

/**
   An iterator to an entry in a hash table.

   This is really just an index, but should be considered opaque to the user
   and only used via the provided API and equality comparison.
*/
typedef size_t ZixHashIter;

/// Return an iterator to the first record in a hash, or the end if it is empty
ZIX_PURE_API ZIX_NONBLOCKING ZixHashIter
zix_hash_begin(const ZixHash* ZIX_NONNULL hash);

/// Return an iterator one past the last possible record in a hash
ZIX_PURE_API ZIX_REALTIME ZixHashIter
zix_hash_end(const ZixHash* ZIX_NONNULL hash);

/// Return the record pointed to by an iterator
ZIX_PURE_API ZIX_REALTIME ZixHashRecord* ZIX_NULLABLE
zix_hash_get(const ZixHash* ZIX_NONNULL hash, ZixHashIter i);

/// Return an iterator that has been advanced to the next record in a hash
ZIX_PURE_API ZIX_NONBLOCKING ZixHashIter
zix_hash_next(const ZixHash* ZIX_NONNULL hash, ZixHashIter i);

/**
   @}
   @defgroup zix_hash_modification Modification
   @{
*/

/// User function for determining if a key matches in a custom search
typedef bool (*ZixKeyMatchFunc)(
  const ZixHashKey* ZIX_NONNULL            key,
  const ZixHashSearchData* ZIX_UNSPECIFIED user_data);

/**
   A "plan" (position) to insert a record in a hash table.

   This contains everything necessary to insert a record, except the record
   itself.  It is exposed to split up insertion so that records only need to be
   allocated if an existing record is not found, but the contents should be
   considered opaque to the user.
*/
typedef struct {
  ZixHashCode code;  ///< Hash code used to find this position
  ZixHashIter index; ///< Index into hash table
} ZixHashInsertPlan;

/**
   Find the best position to insert a record with the given key.

   This searches the hash table and returns either the position of an existing
   equivalent record, or the best available position to insert a new one.  The
   difference can be determined with zix_hash_record_at().

   If the returned position is not occupied, then it is valid to insert a
   record with this key using zix_hash_insert_at() until the hash table is
   modified (which invalidates the position).
*/
ZIX_API ZixHashInsertPlan
zix_hash_plan_insert(const ZixHash* ZIX_NONNULL    hash,
                     const ZixHashKey* ZIX_NONNULL key);

/**
   Find the best position to insert a record with a custom search.

   This is an advanced low-level version of zix_hash_plan_insert().  It allows
   a precalculated hash code to be given, along with a custom search predicate.
   These must be compatible with the functions that manage the hash table:
   trivial usage would be to pass the equality function used by the hash and
   the key to search for.

   This is exposed to make it possible to avoid constructing a key at all when
   potentially inserting.  For example, if keys are structures with a fixed
   header and a variably-sized body, and you have a separate header and body
   this can be used to find an insert position without having to allocate a
   contiguous key.

   Note that care must be taken when using this function: improper use can
   corrupt the hash table.  The hash code given must be correct for the key to
   be inserted, and the predicate must return true only if the key it is called
   with (the first argument) matches the key to be inserted.
*/
ZIX_API ZixHashInsertPlan
zix_hash_plan_insert_prehashed(
  const ZixHash* ZIX_NONNULL               hash,
  ZixHashCode                              code,
  ZixKeyMatchFunc ZIX_NONNULL              predicate,
  const ZixHashSearchData* ZIX_UNSPECIFIED user_data);

/**
   Return the record at the given position, or null.

   This is used to determine if a position returned from zix_hash_plan_insert()
   can be used to insert a new record, or to access the existing matching
   record.
*/
ZIX_PURE_API ZIX_REALTIME ZixHashRecord* ZIX_NULLABLE
zix_hash_record_at(const ZixHash* ZIX_NONNULL hash, ZixHashInsertPlan position);

/**
   Insert a record at a specific position.

   The position must have been found with an earlier call to
   zix_hash_plan_insert(), and no modifications must have been made to the hash
   table since.

   @param hash The hash table.

   @param position The position for the new record.

   @param record The record to insert which, on success, can now be considered
   owned by the hash table.

   @return ZIX_STATUS_SUCCESS, ZIX_STATUS_EXISTS if a record already exists at
   this position, or ZIX_STATUS_NO_MEM if growing the hash table failed.
*/
ZIX_API ZixStatus
zix_hash_insert_at(ZixHash* ZIX_NONNULL       hash,
                   ZixHashInsertPlan          position,
                   ZixHashRecord* ZIX_NONNULL record);

/**
   Insert a record.

   This is a trivial wrapper for zix_hash_plan_insert() and
   zix_hash_insert_at() that is more convenient when record construction is not
   expensive.

   @param hash The hash table.

   @param record The record to insert which, on success, can now be considered
   owned by the hash table.

   @return ZIX_STATUS_SUCCESS, ZIX_STATUS_EXISTS, or ZIX_STATUS_NO_MEM.
*/
ZIX_API ZixStatus
zix_hash_insert(ZixHash* ZIX_NONNULL hash, ZixHashRecord* ZIX_NONNULL record);

/**
   Erase a record at a specific position.

   @param hash The hash table to remove the record from.

   @param i Iterator to the record to remove.  This must be a valid iterator
   from an earlier call to zix_hash_find(), that is, the hash table must not
   have been modified since.

   @param removed Set to the removed record, or null.

   @return ZIX_STATUS_SUCCES or ZIX_STATUS_BAD_ARG if `i` does not point at a
   removable record.
*/
ZIX_API ZixStatus
zix_hash_erase(ZixHash* ZIX_NONNULL                     hash,
               ZixHashIter                              i,
               ZixHashRecord* ZIX_NULLABLE* ZIX_NONNULL removed);

/**
   Remove a record.

   @param hash The hash table.
   @param key The key of the record to remove.
   @param removed Set to the removed record, or null.
   @return ZIX_STATUS_SUCCES or ZIX_STATUS_NOT_FOUND.
*/
ZIX_API ZixStatus
zix_hash_remove(ZixHash* ZIX_NONNULL                     hash,
                const ZixHashKey* ZIX_NONNULL            key,
                ZixHashRecord* ZIX_NULLABLE* ZIX_NONNULL removed);

/**
   @}
   @defgroup zix_hash_searching Searching
   @{
*/

/**
   Find the position of a record with a given key.

   @param hash The hash table to search.

   @param key The key of the desired record.

   @return An iterator to the matching record, or the end iterator if no such
   record exists.
*/
ZIX_API ZixHashIter
zix_hash_find(const ZixHash* ZIX_NONNULL    hash,
              const ZixHashKey* ZIX_NONNULL key);

/**
   Find a record with a given key.

   This is essentially the same as zix_hash_find(), but returns a pointer to
   the record for convenience.

   @param hash The hash table to search.

   @param key The key of the desired record.

   @return A pointer to the matching record, of null if no such record exists.
*/
ZIX_API ZixHashRecord* ZIX_NULLABLE
zix_hash_find_record(const ZixHash* ZIX_NONNULL    hash,
                     const ZixHashKey* ZIX_NONNULL key);

/**
   @}
   @}
*/

ZIX_END_DECLS

#endif /* ZIX_HASH_H */
