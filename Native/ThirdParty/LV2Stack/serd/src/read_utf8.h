// Copyright 2011-2026 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef SERD_SRC_READ_UTF8_H
#define SERD_SRC_READ_UTF8_H

#include "reader.h"

#include <serd/serd.h>

#include <stdint.h>

/// Read a UTF-8 character continuation (starting after the lead byte)
SerdStatus
read_utf8_continuation(SerdReader* reader, Ref dest, uint8_t lead);

/// Read a single UTF-8 character and parse it to a code point
SerdStatus
read_utf8_code_point(SerdReader* reader,
                     Ref         dest,
                     uint32_t*   code,
                     uint8_t     lead);

#endif // SERD_SRC_READ_UTF8_H
