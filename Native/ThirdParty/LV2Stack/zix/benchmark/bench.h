// Copyright 2011-2024 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef BENCH_H
#define BENCH_H

#include <glib.h>

typedef gint64 BenchmarkTime;

static inline double
bench_elapsed_s(const BenchmarkTime* start, const BenchmarkTime* end)
{
  return (double)(*end - *start) * 0.000001;
}

static inline BenchmarkTime
bench_start(void)
{
  return g_get_monotonic_time();
}

static inline double
bench_end(const BenchmarkTime* start_t)
{
  const BenchmarkTime end_t = g_get_monotonic_time();
  return bench_elapsed_s(start_t, &end_t);
}

#endif // BENCH_H
