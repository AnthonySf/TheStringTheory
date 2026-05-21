#!/usr/bin/env python

# Copyright 2007-2011 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: ISC

import lilv

world = lilv.World()
world.load_all()

for i in world.get_all_plugins():
    print(i.get_uri())
