// Copyright 2011 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

#include <sord/sordmm.hpp>

int
main()
{
  Sord::World world;
  Sord::Model model(world, "http://example.org/");
  return 0;
}
