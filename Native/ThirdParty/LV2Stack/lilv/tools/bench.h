// Copyright 2011-2019 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#ifndef BENCH_H
#define BENCH_H

#include <time.h>

typedef struct timespec BenchmarkTime;

static inline double
bench_elapsed_s(const BenchmarkTime* start, const BenchmarkTime* end)
{
  return ((end->tv_sec - start->tv_sec) +
          ((end->tv_nsec - start->tv_nsec) * 0.000000001));
}

static inline BenchmarkTime
bench_start(void)
{
  BenchmarkTime start_t;
  clock_gettime(CLOCK_MONOTONIC, &start_t);
  return start_t;
}

static inline double
bench_end(const BenchmarkTime* start_t)
{
  BenchmarkTime end_t;
  clock_gettime(CLOCK_MONOTONIC, &end_t);
  return bench_elapsed_s(start_t, &end_t);
}

#endif /* BENCH_H */
