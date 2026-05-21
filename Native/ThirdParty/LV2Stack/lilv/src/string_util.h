// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef LILV_STRING_UTIL_H
#define LILV_STRING_UTIL_H

#include <sord/sord.h>
#include <zix/string_view.h>

#include <stdint.h>

char*
lilv_strjoin(const char* first, ...);

char*
lilv_strdup(const char* str);

ZixStringView
lilv_node_string_view(const SordNode* node);

uint8_t*
lilv_manifest_uri(const SordNode* node);

#endif /* LILV_STRING_UTIL_H */
