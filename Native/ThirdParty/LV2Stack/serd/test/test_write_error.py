#!/usr/bin/env python3

# Copyright 2022-2025 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: ISC

"""Test errors writing to a file."""

import sys
import shlex
import subprocess
import os

import serd_test_util as util

args = util.wrapper_args(__doc__, True)
command = shlex.split(args.wrapper) + [args.serdi, args.input]

if os.path.exists("/dev/full"):
    with open("/dev/full", "w", encoding="utf-8") as out:
        proc = subprocess.run(
            command,
            encoding="utf-8",
            check=False,
            stdout=out,
            stderr=subprocess.PIPE,
        )

    assert proc.returncode != 0
    assert "error" in proc.stderr

else:
    sys.stderr.write("warning: /dev/full not present, skipping test")
