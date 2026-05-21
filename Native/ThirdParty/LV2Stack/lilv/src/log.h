// Copyright 2007-2025 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef LILV_LOG_H
#define LILV_LOG_H

#define LILV_ERROR(str) fprintf(stderr, "%s(): error: " str, __func__)
#define LILV_ERRORF(fmt, ...) \
  fprintf(stderr, "%s(): error: " fmt, __func__, __VA_ARGS__)
#define LILV_WARN(str) fprintf(stderr, "%s(): warning: " str, __func__)
#define LILV_WARNF(fmt, ...) \
  fprintf(stderr, "%s(): warning: " fmt, __func__, __VA_ARGS__)
#define LILV_NOTE(str) fprintf(stderr, "%s(): note: " str, __func__)
#define LILV_NOTEF(fmt, ...) \
  fprintf(stderr, "%s(): note: " fmt, __func__, __VA_ARGS__)

#endif /* LILV_LOG_H */
